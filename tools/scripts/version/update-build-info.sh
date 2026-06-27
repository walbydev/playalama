#!/bin/bash
# update-build-info.sh — Générer le fichier .build-info avec timestamp et build number

set -euo pipefail

BUILD_INFO_FILE="${1:-.build-info}"
OPERATION="${2:-generate}"
VERSION="${3:-}"

read_build_info() {
  local key="$1"
  grep -o "\"$key\": \"[^\"]*\"" "$BUILD_INFO_FILE" | cut -d'"' -f4
}

read_build_number() {
  grep -o '"buildNumber": [0-9]*' "$BUILD_INFO_FILE" | grep -o '[0-9]*'
}

write_build_info() {
  local version="$1"
  local build_number="$2"
  local timestamp="$3"
  
  cat > "$BUILD_INFO_FILE" <<EOF
{
  "version": "$version",
  "buildNumber": $build_number,
  "buildTimestamp": "$timestamp"
}
EOF
}

case "$OPERATION" in
  generate)
    version=$(read_build_info "version")
    build_number=$(read_build_number)
    timestamp=$(date -u +"%Y-%m-%dT%H:%M:%SZ")
    write_build_info "$version" "$build_number" "$timestamp"
    echo "✓ Build info généré : v$version build #$build_number à $timestamp"
    ;;
    
  increment)
    version=$(read_build_info "version")
    build_number=$(read_build_number)
    timestamp=$(date -u +"%Y-%m-%dT%H:%M:%SZ")
    build_number=$((build_number + 1))
    write_build_info "$version" "$build_number" "$timestamp"
    echo "✓ Build numéro incrémenté: #$build_number à $timestamp"
    ;;
    
  set-version)
    if [ -z "$VERSION" ]; then
      echo "❌ Usage: $0 <file> set-version <VERSION>" >&2
      exit 1
    fi
    build_number=$(read_build_number)
    timestamp=$(date -u +"%Y-%m-%dT%H:%M:%SZ")
    write_build_info "$VERSION" "$build_number" "$timestamp"
    echo "✓ Version fixée: v$VERSION build #$build_number à $timestamp"
    ;;
    
  *)
    echo "❌ Opération inconnue: $OPERATION" >&2
    echo "   Usage: $0 <file> <generate|increment|set-version> [VERSION]" >&2
    exit 1
    ;;
esac
