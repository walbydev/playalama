#!/usr/bin/env bash
# =============================================================================
# package-cli-singlefile.sh
#
# Publie la console LAMA en executable unique (single-file, self-contained)
# pour plusieurs OS/architectures, puis genere un ZIP par cible.
#
# Usage :
#   ./tools/scripts/package-cli-singlefile.sh [--rids "linux-x64 win-x64 ..."] [--version "1.0.0"]
#
# Sorties :
#   artifacts/publish/<rid>/lama[.exe]   — binaire autonome
#   artifacts/zip/lama-<version>-<rid>.zip — archive telechargeable
# =============================================================================
set -euo pipefail

# ── Chemins ────────────────────────────────────────────────────────────────────
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/../.." && pwd)"
CONSOLE_PROJ="${ROOT_DIR}/src/apps/Lama.Console/Lama.Console.csproj"
README_SRC="${ROOT_DIR}/tools/distribution/README.txt"
ARTIFACTS_DIR="${ROOT_DIR}/artifacts"
PUBLISH_BASE="${ARTIFACTS_DIR}/publish"
ZIP_DIR="${ARTIFACTS_DIR}/zip"

# ── Valeurs par defaut ─────────────────────────────────────────────────────────
DEFAULT_RIDS="linux-x64 linux-arm64 win-x64 win-arm64 osx-x64 osx-arm64"
VERSION="$(date +%Y%m%d)"    # ex : 20260619 si pas de --version

# ── Arguments ─────────────────────────────────────────────────────────────────
RIDS="${DEFAULT_RIDS}"

while [[ $# -gt 0 ]]; do
    case "$1" in
        --rids)
            RIDS="$2"; shift 2 ;;
        --version)
            VERSION="$2"; shift 2 ;;
        *)
            echo "Option inconnue : $1" >&2
            echo "Usage : $0 [--rids \"rid1 rid2 ...\"] [--version X.Y.Z]" >&2
            exit 1 ;;
    esac
done

# ── Verifications ──────────────────────────────────────────────────────────────
if ! command -v dotnet &>/dev/null; then
    echo "[ERREUR] dotnet SDK non trouve dans PATH." >&2
    exit 1
fi

if [[ ! -f "${CONSOLE_PROJ}" ]]; then
    echo "[ERREUR] Projet console introuvable : ${CONSOLE_PROJ}" >&2
    exit 1
fi

if [[ ! -f "${README_SRC}" ]]; then
    echo "[ERREUR] README de distribution introuvable : ${README_SRC}" >&2
    exit 1
fi

if ! command -v zip &>/dev/null; then
    echo "[ERREUR] La commande 'zip' est requise (apt install zip / brew install zip)." >&2
    exit 1
fi

# ── Preparation ────────────────────────────────────────────────────────────────
mkdir -p "${ZIP_DIR}"

echo "============================================================"
echo "  LAMA — Packaging console single-file v${VERSION}"
echo "  RIDs : ${RIDS}"
echo "============================================================"
echo ""

FAILED_RIDS=""

# ── Boucle sur chaque RID ──────────────────────────────────────────────────────
for RID in ${RIDS}; do
    echo "────────────────────────────────────────────────────────────"
    echo "  [${RID}] Publication..."
    echo "────────────────────────────────────────────────────────────"

    OUT_DIR="${PUBLISH_BASE}/${RID}"
    mkdir -p "${OUT_DIR}"

    # dotnet publish
    if dotnet publish "${CONSOLE_PROJ}" \
        --configuration Release \
        --runtime "${RID}" \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        -p:EnableCompressionInSingleFile=true \
        -p:PublishTrimmed=false \
        -p:DebugType=None \
        -p:DebugSymbols=false \
        --output "${OUT_DIR}" \
        --nologo \
        -v q; then
        echo "  [${RID}] Publication OK -> ${OUT_DIR}"
    else
        echo "  [${RID}] ECHEC de publication" >&2
        FAILED_RIDS="${FAILED_RIDS} ${RID}"
        continue
    fi

    # Determiner le nom du binaire selon la cible
    if [[ "${RID}" == win-* ]]; then
        BINARY_NAME="lama.exe"
    else
        BINARY_NAME="lama"
        # S'assurer que le binaire est executable
        chmod +x "${OUT_DIR}/${BINARY_NAME}" 2>/dev/null || true
    fi

    # Verifier que le binaire existe
    if [[ ! -f "${OUT_DIR}/${BINARY_NAME}" ]]; then
        echo "  [${RID}] AVERTISSEMENT : binaire '${BINARY_NAME}' non trouve dans ${OUT_DIR}" >&2
        ls "${OUT_DIR}" >&2
        FAILED_RIDS="${FAILED_RIDS} ${RID}"
        continue
    fi

    # Copier le README dans le dossier de publication
    cp "${README_SRC}" "${OUT_DIR}/README.txt"

    # Creer le ZIP
    ZIP_NAME="lama-${VERSION}-${RID}.zip"
    ZIP_PATH="${ZIP_DIR}/${ZIP_NAME}"

    echo "  [${RID}] Creation du ZIP : ${ZIP_NAME}"
    (
        cd "${OUT_DIR}"
        zip -q "${ZIP_PATH}" "${BINARY_NAME}" README.txt
    )

    # Taille du ZIP
    ZIP_SIZE="$(du -sh "${ZIP_PATH}" | cut -f1)"
    echo "  [${RID}] ZIP pret (${ZIP_SIZE}) -> ${ZIP_PATH}"
    echo ""
done

# ── Rapport final ──────────────────────────────────────────────────────────────
echo "============================================================"
echo "  Recapitulatif"
echo "============================================================"

if [[ -z "${FAILED_RIDS}" ]]; then
    echo "  Tous les ZIPs ont ete generes avec succes :"
    echo ""
    for RID in ${RIDS}; do
        ZIP_PATH="${ZIP_DIR}/lama-${VERSION}-${RID}.zip"
        if [[ -f "${ZIP_PATH}" ]]; then
            ZIP_SIZE="$(du -sh "${ZIP_PATH}" | cut -f1)"
            printf "  %-40s %s\n" "lama-${VERSION}-${RID}.zip" "${ZIP_SIZE}"
        fi
    done
    echo ""
    echo "  Dossier ZIP : ${ZIP_DIR}"
    echo "============================================================"
    exit 0
else
    echo "  Certains RIDs ont echoue :${FAILED_RIDS}" >&2
    echo ""
    echo "  ZIPs disponibles :"
    for RID in ${RIDS}; do
        ZIP_PATH="${ZIP_DIR}/lama-${VERSION}-${RID}.zip"
        if [[ -f "${ZIP_PATH}" ]]; then
            ZIP_SIZE="$(du -sh "${ZIP_PATH}" | cut -f1)"
            printf "  %-40s %s\n" "lama-${VERSION}-${RID}.zip" "${ZIP_SIZE}"
        fi
    done
    echo "============================================================"
    exit 1
fi

