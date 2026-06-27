#!/bin/bash
# sync-to-csharp.sh — Synchroniser .build-info vers BuildInfoConstants.cs

set -euo pipefail

BUILD_INFO_FILE="${1:-.build-info}"
OUTPUT_FILE="${2:-src/apps/Lama.WebApp/Services/BuildInfoConstants.cs}"

version=$(grep -o '"version": "[^"]*"' "$BUILD_INFO_FILE" | cut -d'"' -f4)
build_number=$(grep -o '"buildNumber": [0-9]*' "$BUILD_INFO_FILE" | grep -o '[0-9]*')
timestamp=$(grep -o '"buildTimestamp": "[^"]*"' "$BUILD_INFO_FILE" | cut -d'"' -f4)

cat > "$OUTPUT_FILE" <<CSCODE
namespace Lama.WebApp.Services;

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

echo "✓ BuildInfoConstants.cs mis à jour: v$version build #$build_number"
