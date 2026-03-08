#!/usr/bin/env bash
# Setup Crawl4AI via pip — install package and run post-install setup.
# Usage: bash scripts/setup_pip.sh [--extras EXTRAS]
#   EXTRAS: default, all, torch, transformer, pdf, cosine

set -euo pipefail

EXTRAS=""

while [[ $# -gt 0 ]]; do
    case $1 in
        --extras) EXTRAS="$2"; shift 2 ;;
        *) shift ;;
    esac
done

echo "==> Checking Python..."
PYTHON=""
for cmd in python3 python; do
    if command -v "$cmd" &>/dev/null; then
        PYTHON="$cmd"
        break
    fi
done

if [[ -z "$PYTHON" ]]; then
    echo "ERROR: Python not found. Install Python 3.10+ first."
    exit 1
fi

PY_VERSION=$($PYTHON -c "import sys; print(f'{sys.version_info.major}.{sys.version_info.minor}')")
echo "    Found $PYTHON ($PY_VERSION)"

# Check Python version >= 3.10
PY_MINOR=$($PYTHON -c "import sys; print(sys.version_info.minor)")
PY_MAJOR=$($PYTHON -c "import sys; print(sys.version_info.major)")
if [[ "$PY_MAJOR" -lt 3 ]] || [[ "$PY_MAJOR" -eq 3 && "$PY_MINOR" -lt 10 ]]; then
    echo "ERROR: Python 3.10+ required (found $PY_VERSION)"
    exit 1
fi

echo "==> Installing crawl4ai..."
if [[ -n "$EXTRAS" && "$EXTRAS" != "default" ]]; then
    pip install -U "crawl4ai[$EXTRAS]"
else
    pip install -U crawl4ai
fi

echo "==> Running post-install setup (installs Playwright browsers)..."
crawl4ai-setup

echo "==> Verifying installation..."
crawl4ai-doctor

echo "==> Done! Crawl4AI is ready."
echo "    Test with: python -c \"import crawl4ai; print(crawl4ai.__version__)\""
