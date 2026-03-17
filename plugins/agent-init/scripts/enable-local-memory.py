#!/usr/bin/env python3
"""Set autoMemoryDirectory to .agents/memory/ in .claude/settings.local.json. Idempotent.

Prints 'configured' if a change was made, 'ok' if already configured.
"""

import json
from pathlib import Path

Path(".claude").mkdir(exist_ok=True)
Path(".agents/memory").mkdir(parents=True, exist_ok=True)

p = Path(".claude/settings.local.json")
d = json.loads(p.read_text()) if p.exists() else {}
changed = "autoMemoryDirectory" not in d
d.setdefault("autoMemoryDirectory", ".agents/memory/")
p.write_text(json.dumps(d, indent=2) + "\n")
print("configured" if changed else "ok")
