#!/usr/bin/env bash
# run-affected-tests.sh — Run tests for projects listed in an affected-projects file.
#
# Supports both `dotnet test` and `dotnet run` (for TUnit and similar frameworks).
# If the affected-projects file does not exist (e.g., detection step was skipped in full mode),
# falls back to discovering all test projects matching --fallback-glob.
#
# Usage:
#   ./scripts/run-affected-tests.sh <affected-file> [options]
#
# Options:
#   --runner test|run       dotnet verb to use (default: test)
#   --frameworks f1,f2,...  comma-separated TFMs to iterate (default: single run, no --framework flag)
#   --configuration cfg     build configuration (default: Release)
#   --no-build              pass --no-build to dotnet
#   --results-dir path      test results directory (default: ./artifacts/test-results)
#   --extra-args "..."      additional arguments passed after -- (for TUnit etc.)
#   --fallback-glob "pat"   glob pattern to find all test projects when affected file is missing
#                           (default: **/*.Tests.csproj)
#
# Examples:
#   # Standard dotnet test across two TFMs (incremental: uses affected file; full: discovers all)
#   ./scripts/run-affected-tests.sh affected-unit-tests.txt \
#     --runner test --frameworks net8.0,net9.0 --no-build \
#     --fallback-glob "test/**/*.Tests.csproj"
#
#   # TUnit (requires dotnet run)
#   ./scripts/run-affected-tests.sh affected-unit-tests.txt \
#     --runner run --frameworks net8.0,net9.0,net10.0 --no-build \
#     --extra-args "--no-progress --output Detailed"

set -euo pipefail

AFFECTED_FILE="${1:?Usage: run-affected-tests.sh <affected-file> [options]}"
shift

RUNNER="test"
FRAMEWORKS=""
CONFIGURATION="Release"
NO_BUILD=""
RESULTS_DIR="./artifacts/test-results"
EXTRA_ARGS=""
FALLBACK_GLOB="**/*.Tests.csproj"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --runner)        RUNNER="$2"; shift 2 ;;
    --frameworks)    FRAMEWORKS="$2"; shift 2 ;;
    --configuration) CONFIGURATION="$2"; shift 2 ;;
    --no-build)      NO_BUILD="--no-build"; shift ;;
    --results-dir)   RESULTS_DIR="$2"; shift 2 ;;
    --extra-args)    EXTRA_ARGS="$2"; shift 2 ;;
    --fallback-glob) FALLBACK_GLOB="$2"; shift 2 ;;
    *) echo "Unknown option: $1"; exit 1 ;;
  esac
done

# If the affected file does not exist (detection step was skipped in full mode),
# discover all test projects matching the fallback glob and run them all.
if [ ! -f "$AFFECTED_FILE" ]; then
  echo "Affected file not found (full mode). Discovering all test projects via: $FALLBACK_GLOB"
  AFFECTED_FILE=$(mktemp)
  # shellcheck disable=SC2086
  find . -path "$FALLBACK_GLOB" -name "*.csproj" > "$AFFECTED_FILE"
fi

if [ ! -s "$AFFECTED_FILE" ]; then
  echo "No test projects found — skipping."
  exit 0
fi

mkdir -p "$RESULTS_DIR"
FAILED=0

# Split frameworks into array (or use empty for single run)
if [ -n "$FRAMEWORKS" ]; then
  IFS=',' read -ra TFM_LIST <<< "$FRAMEWORKS"
else
  TFM_LIST=("")
fi

while IFS= read -r PROJECT; do
  [ -z "$PROJECT" ] && continue
  PROJECT_NAME=$(basename "$(dirname "$PROJECT")")

  for TFM in "${TFM_LIST[@]}"; do
    LABEL="$PROJECT_NAME"
    [ -n "$TFM" ] && LABEL="$PROJECT_NAME ($TFM)"

    echo "::group::Testing $LABEL"

    TFM_FLAG=""
    [ -n "$TFM" ] && TFM_FLAG="--framework $TFM"

    TRX_NAME="${PROJECT_NAME}"
    [ -n "$TFM" ] && TRX_NAME="${PROJECT_NAME}-${TFM}"

    CMD="dotnet $RUNNER --project \"$PROJECT\" --configuration $CONFIGURATION $NO_BUILD $TFM_FLAG"

    if [ "$RUNNER" = "test" ]; then
      CMD="$CMD --logger \"trx;LogFileName=${TRX_NAME}.trx\" --results-directory \"$RESULTS_DIR\""
      [ -n "$EXTRA_ARGS" ] && CMD="$CMD $EXTRA_ARGS"
    elif [ "$RUNNER" = "run" ]; then
      CMD="$CMD --"
      CMD="$CMD --results-directory \"$RESULTS_DIR\""
      CMD="$CMD --report-trx --report-trx-filename \"${TRX_NAME}.trx\""
      [ -n "$EXTRA_ARGS" ] && CMD="$CMD $EXTRA_ARGS"
    fi

    if ! eval "$CMD"; then
      echo "::error::Tests failed: $LABEL"
      FAILED=$((FAILED + 1))
    fi

    echo "::endgroup::"
  done
done < "$AFFECTED_FILE"

if [ "$FAILED" -gt 0 ]; then
  echo "::error::$FAILED test run(s) failed."
  exit 1
fi

echo "All affected tests passed."
