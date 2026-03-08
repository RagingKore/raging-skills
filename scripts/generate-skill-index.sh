#!/usr/bin/env bash

# Generates a compressed skills index from the raging-skills marketplace.
# Reads marketplace.json, walks each plugin's plugin.json, extracts skill/agent
# names from SKILL.md and agent frontmatter, then outputs a Vercel-style
# compressed index grouped by category.
#
# Usage:
#   ./scripts/generate-skill-index.sh                # print to stdout
#   ./scripts/generate-skill-index.sh --update       # inject into CLAUDE.md

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(dirname "$SCRIPT_DIR")"
MARKETPLACE_JSON="$REPO_ROOT/.claude-plugin/marketplace.json"
CLAUDE_MD="$REPO_ROOT/.project/project.md"

UPDATE=false
if [[ "${1-}" == "--update" ]]; then
  UPDATE=true
fi

# --- helpers ----------------------------------------------------------------

skill_name_from_dir() {
  local dir="$1"
  for file in "$dir"/*/SKILL.md; do
    [[ -f "$file" ]] || continue
    grep -m1 '^name:' "$file" | sed 's/^name:[[:space:]]*//'
  done
}

agent_name_from_file() {
  local file="$1"
  [[ -f "$file" ]] || return 1
  grep -m1 '^name:' "$file" | sed 's/^name:[[:space:]]*//'
}

join_csv() {
  local IFS=','
  echo "$*"
}

# --- categories -------------------------------------------------------------

declare -a dotnet=()
declare -a dotnet_scripts=()
declare -a architecture=()
declare -a diagrams=()
declare -a protobuf=()
declare -a web=()
declare -a conventions=()
declare -a agent_workflow=()
declare -a all_agents=()

# --- walk plugins -----------------------------------------------------------

while IFS=$'\t' read -r plugin_name plugin_source; do
  plugin_dir="$REPO_ROOT/${plugin_source#./}"
  plugin_json="$plugin_dir/.claude-plugin/plugin.json"
  [[ -f "$plugin_json" ]] || continue

  # collect skills
  skills_dirs=()
  while IFS= read -r sd; do
    skills_dirs+=("$plugin_dir/${sd#./}")
  done < <(jq -r '.skills // [] | .[]' "$plugin_json")

  for sd in "${skills_dirs[@]}"; do
    while IFS= read -r name; do
      [[ -n "$name" ]] || continue
      case "$plugin_name" in
        dotnet)                    dotnet+=("$name") ;;
        dotnet-scripts|bullseye|incrementalist) dotnet_scripts+=("$name") ;;
        domain-driven-design|dcb|kurrentdb) architecture+=("$name") ;;
        mermaid|beautiful-mermaid|excalidraw) diagrams+=("$name") ;;
        buf|proto-style)           protobuf+=("$name") ;;
        astro|crawl4ai)            web+=("$name") ;;
        conventional-commits|keep-a-changelog|markdown-style) conventions+=("$name") ;;
        claude-output-style|project-setup) agent_workflow+=("$name") ;;
      esac
    done < <(skill_name_from_dir "$sd")
  done

  # collect agents
  while IFS= read -r agent_path; do
    agent_file="$plugin_dir/${agent_path#./}"
    name="$(agent_name_from_file "$agent_file" 2>/dev/null)" || continue
    [[ -n "$name" ]] && all_agents+=("$name")
  done < <(jq -r '.agents // [] | .[]' "$plugin_json")

done < <(jq -r '.plugins[] | [.name, .source] | @tsv' "$MARKETPLACE_JSON")

# --- output -----------------------------------------------------------------

build_line() {
  local label="$1"; shift
  local -a items=("$@")
  if [[ ${#items[@]} -gt 0 ]]; then
    echo "|${label}:{$(join_csv "${items[@]}")}"
  fi
}

compressed="[raging-skills]|Prefer retrieval-led reasoning over pretraining. Consult skills by name before implementing.
|flow:{skim repo patterns -> consult skill by name -> implement smallest-change -> note conflicts}
|route:
$(build_line "dotnet" "${dotnet[@]}")
$(build_line "dotnet-scripts" "${dotnet_scripts[@]}")
$(build_line "architecture" "${architecture[@]}")
$(build_line "diagrams" "${diagrams[@]}")
$(build_line "protobuf" "${protobuf[@]}")
$(build_line "web" "${web[@]}")
$(build_line "conventions" "${conventions[@]}")
$(build_line "agent-workflow" "${agent_workflow[@]}")
$(build_line "agents" "${all_agents[@]}")"

# trim blank lines from empty categories
compressed="$(echo "$compressed" | sed '/^$/d')"

if $UPDATE; then
  COMPRESSED="$compressed" TARGET_PATH="$CLAUDE_MD" python3 - <<'PY'
import os
import pathlib
import re
import sys

target_path = pathlib.Path(os.environ["TARGET_PATH"])
start = "<!-- BEGIN RAGING-SKILLS INDEX -->"
end = "<!-- END RAGING-SKILLS INDEX -->"
compressed = os.environ["COMPRESSED"].strip()

text = target_path.read_text(encoding="utf-8")
pattern = re.compile(re.escape(start) + r".*?" + re.escape(end), re.S)

if not pattern.search(text):
    sys.stderr.write("Markers not found: add BEGIN/END RAGING-SKILLS INDEX to CLAUDE.md\n")
    sys.exit(1)

replacement = f"{start}\n```\n{compressed}\n```\n{end}"
updated = pattern.sub(replacement, text)
target_path.write_text(updated, encoding="utf-8")
PY
else
  printf '%s\n' "$compressed"
fi
