#!/bin/bash
# sync-to-csharp.sh — Synchroniser .build-info vers BuildInfoConstants.cs (WebApp + Server)

set -euo pipefail

BUILD_INFO_FILE="${1:-.build-info}"
WEBAPP_FILE="src/apps/Lama.WebApp/Services/BuildInfoConstants.cs"
SERVER_FILE="src/apps/Lama.Server/Services/BuildInfoConstants.cs"

version=$(grep -o '"version": "[^"]*"' "$BUILD_INFO_FILE" | cut -d'"' -f4)
build_number=$(grep -o '"buildNumber": [0-9]*' "$BUILD_INFO_FILE" | grep -o '[0-9]*')
timestamp=$(grep -o '"buildTimestamp": "[^"]*"' "$BUILD_INFO_FILE" | cut -d'"' -f4)

generate_file() {
  local output_file="$1"
  local namespace="$2"
  cat > "$output_file" <<CSCODE
namespace $namespace;

/// <summary>
/// Infos de build générées depuis .build-info
/// Mis à jour automatiquement par make build-generate
/// </summary>
public static class BuildInfoConstants
{
    public const string Version = "$version";
    public const int BuildNumber = $build_number;
    public const string BuildTimestamp = "$timestamp";
}
CSCODE
}

generate_file "$WEBAPP_FILE" "Lama.WebApp.Services"
generate_file "$SERVER_FILE" "Lama.Server.Services"

echo "✓ BuildInfoConstants.cs mis à jour: v$version build #$build_number (WebApp + Server)"
