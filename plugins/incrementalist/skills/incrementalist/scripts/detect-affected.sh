#!/usr/bin/env bash
# detect-affected.sh — Detect affected projects using Incrementalist.
#
# Usage:
#   ./scripts/detect-affected.sh <config> <output-file>
#   ./scripts/detect-affected.sh .incrementalist/testsOnly.json affected-unit-tests.txt
#
# Writes affected project paths (one per line) to <output-file>.
# Exits 0 with an empty file when nothing is affected.

set -euo pipefail

CONFIG="${1:-.incrementalist/incrementalist.json}"
OUTPUT="${2:-affected-projects.txt}"

: > "$OUTPUT"  # truncate

echo "::group::Detecting affected projects (config: $CONFIG)"

dotnet incrementalist \
  --config "$CONFIG" \
  --verbose \
  -f "$OUTPUT"

if [ ! -s "$OUTPUT" ]; then
  echo "No affected projects detected."
  echo "::endgroup::"
  exit 0
fi

COUNT=$(wc -l < "$OUTPUT" | tr -d ' ')
echo "Affected projects ($COUNT):"
cat "$OUTPUT"
echo "::endgroup::"
