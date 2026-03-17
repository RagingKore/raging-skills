#!/usr/bin/env bash
# Check if the Excalidraw color palette has been initialized for this project.
# Reads the palette path from the plugin's settings.json and checks if the file exists.

set -euo pipefail

# Find settings.json relative to this script (plugin root)
script_dir="$(cd "$(dirname "$0")" && pwd)"
plugin_root="$(dirname "$script_dir")"
settings_file="$plugin_root/settings.json"

if [ ! -f "$settings_file" ]; then
  echo "EXCALIDRAW_ERROR: Plugin settings.json not found at $settings_file"
  exit 0
fi

# Read colorPalettePath from settings.json
palette_path=$(python3 -c "import json; print(json.load(open('$settings_file')).get('colorPalettePath', ''))" 2>/dev/null || true)

if [ -z "$palette_path" ]; then
  echo "EXCALIDRAW_ERROR: No colorPalettePath in settings.json"
  exit 0
fi

if [ ! -f "$palette_path" ]; then
  echo "EXCALIDRAW_PALETTE_NOT_INITIALIZED: Color palette not found at $palette_path. Run the Color Palette Initialization from the excalidraw skill before generating diagrams."
  exit 0
fi

# All good — palette exists
exit 0
