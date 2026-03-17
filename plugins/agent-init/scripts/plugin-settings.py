#!/usr/bin/env python3
"""Read and write plugin settings in settings.json.

Usage:
    plugin-settings.py get <key>       # prints value, exits 1 if missing
    plugin-settings.py set '<json>'    # merges JSON into settings
"""

import json
import os
import sys
from pathlib import Path

def usage():
    print("Usage: plugin-settings.py get <key> | set '<json>'", file=sys.stderr)
    sys.exit(2)

if len(sys.argv) < 3:
    usage()

plugin_root = os.environ.get("CLAUDE_PLUGIN_ROOT", "")
settings_path = Path(plugin_root) / "settings.json"
action = sys.argv[1]

if action == "get":
    key = sys.argv[2]
    if not settings_path.exists():
        sys.exit(1)
    d = json.loads(settings_path.read_text())
    if key not in d:
        sys.exit(1)
    print(d[key])

elif action == "set":
    patch = json.loads(sys.argv[2])
    d = json.loads(settings_path.read_text()) if settings_path.exists() else {}
    d.update(patch)
    settings_path.parent.mkdir(parents=True, exist_ok=True)
    settings_path.write_text(json.dumps(d, indent=2) + "\n")

else:
    usage()
