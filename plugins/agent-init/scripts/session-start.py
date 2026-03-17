#!/usr/bin/env python3
"""SessionStart hook for agent-init plugin.

Pure router: reads plugin settings, returns additionalContext telling Claude what to do.
Calls enable-local-memory.py if project memory needs configuring.
"""

import json
import subprocess
import sys
from pathlib import Path

script_dir = Path(__file__).parent


def context(msg: str):
    """Print hookSpecificOutput JSON and exit."""
    print(json.dumps({
        "hookSpecificOutput": {
            "hookEventName": "SessionStart",
            "additionalContext": msg,
        }
    }))
    sys.exit(0)


def get_setting(key: str) -> str | None:
    """Read a key from plugin settings."""
    try:
        result = subprocess.run(
            [sys.executable, str(script_dir / "plugin-settings.py"), "get", key],
            capture_output=True, text=True,
        )
        return result.stdout.strip() if result.returncode == 0 else None
    except Exception:
        return None


# Not initialized: tell Claude to invoke agent-init
if get_setting("initialized") != "True":
    context(
        "The agent-init plugin is installed but has not been initialized for this project. "
        "Invoke the agent-init skill now to set up AGENTS.md and agent context files."
    )

# Initialized with project memory: ensure config, request restart if just configured
if get_setting("useProjectMemory") == "True":
    result = subprocess.run(
        [sys.executable, str(script_dir / "enable-local-memory.py")],
        capture_output=True, text=True,
    )
    if result.stdout.strip() == "configured":
        context(
            "Project memory was just configured to use .agents/memory/. "
            "Tell the user to restart Claude Code for this to take effect."
        )

# Everything configured: remind Claude to check breadcrumb for drift
context(
    "Agent context is initialized. Check your auto-memory breadcrumb for the agent-init entry. "
    "If the project structure has diverged from the signals recorded there, "
    "invoke the agent-init skill to refresh context."
)
