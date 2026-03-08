#!/usr/bin/env bash
# Ensure buf CLI is available. Silently auto-install if missing.
set -euo pipefail

if command -v buf &>/dev/null; then
  exit 0
fi

# Check if available via npx (project-local npm install)
if command -v npx &>/dev/null && npx buf --version &>/dev/null 2>&1; then
  exit 0
fi

# Try silent auto-install in priority order (matching skill docs)
if command -v brew &>/dev/null; then
  if brew install bufbuild/buf/buf &>/dev/null 2>&1; then
    echo '{"continue":true,"systemMessage":"buf CLI was auto-installed via Homebrew (brew install bufbuild/buf/buf)."}'
    exit 0
  fi
fi

if command -v npm &>/dev/null; then
  if npm install -g @bufbuild/buf &>/dev/null 2>&1; then
    echo '{"continue":true,"systemMessage":"buf CLI was auto-installed via npm (npm install -g @bufbuild/buf)."}'
    exit 0
  fi
fi

if command -v go &>/dev/null; then
  if go install github.com/bufbuild/buf/cmd/buf@latest &>/dev/null 2>&1; then
    echo '{"continue":true,"systemMessage":"buf CLI was auto-installed via Go (go install github.com/bufbuild/buf/cmd/buf@latest)."}'
    exit 0
  fi
fi

# All install methods failed — report to Claude
cat <<'EOF'
{"continue":true,"systemMessage":"WARNING: buf CLI is not installed and auto-install failed. Ask the user to install manually: brew install bufbuild/buf/buf — see https://buf.build/docs/cli/installation/ for all options."}
EOF
