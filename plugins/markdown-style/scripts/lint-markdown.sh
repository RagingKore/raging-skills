#!/bin/bash
# lint-markdown.sh - Async PostToolUse hook: runs markdownlint-cli2 --fix on edited .md files
# Receives JSON on stdin from Claude Code with tool_input.file_path
# Returns JSON with systemMessage for async hook delivery

INPUT=$(cat)
FILE_PATH=$(echo "$INPUT" | jq -r '.tool_input.file_path // empty')

# Skip if no file path or not a markdown file
if [ -z "$FILE_PATH" ] || [ "${FILE_PATH##*.}" != "md" ]; then
  exit 0
fi

# Skip if file was deleted
if [ ! -f "$FILE_PATH" ]; then
  exit 0
fi

# Auto-fix mechanical issues
npx markdownlint-cli2 --fix "$FILE_PATH" 2>/dev/null

# Check for remaining violations
if ! VIOLATIONS=$(npx markdownlint-cli2 "$FILE_PATH" 2>&1); then
  MESSAGE="markdownlint violations in $FILE_PATH:\n$VIOLATIONS"
  echo "{\"systemMessage\": \"$MESSAGE\"}"
fi

exit 0
