---
name: markdown-setup
description: >
  One-time setup that analyzes a repo's markdown files, discovers existing conventions (list markers, heading style,
  line length, emphasis), and generates two artifacts: a .markdownlint-cli2.yaml linter config and a
  .claude/rules/markdown.md rules file. Idempotent; re-run to regenerate after conventions evolve.
user_invocable: true
---

Analyze this project's markdown files to discover existing conventions, then generate a tailored linter config and
Claude Code rules file. Idempotent; re-run to regenerate.

## Prerequisites

The linter (`markdownlint-cli2`) is fetched via `npx` on first use. The PostToolUse hook bundled with this plugin
requires `jq` to extract file paths.

## Step 1: Check for existing config

Look for `.markdownlint-cli2.yaml`, `.markdownlint.json`, `.markdownlint.jsonc`, or `.markdownlint.yaml` at the
repo root.

- If found: ask the user via `AskUserQuestion` whether to regenerate from scratch
- If not found: proceed

## Step 2: Check file count

Count markdown files in the repo: `find . -name '*.md' -not -path './.git/*' -not -path './node_modules/*' | wc -l`

If fewer than 3 markdown files exist, skip the analysis and inform the user: "This repo has very few markdown files.
I'll generate config from the template defaults. You can customize the generated files afterward." Jump to step 5
using the template defaults directly.

## Step 3: Dispatch parallel analysis

Spawn three subagents in parallel using the Agent tool:

**Agent A: Environment scan**

> Research only; do not write any files.
> Scan the repo for markdown-related configuration:
>
> 1. Read `.editorconfig` for `max_line_length`, `indent_size`, `indent_style`, `trim_trailing_whitespace`,
>    `insert_final_newline` (check both `[*]` and `[*.md]` sections)
> 2. Check for existing markdownlint configs (`.markdownlint-cli2.yaml`, `.markdownlint.json`, etc.)
> 3. Check for `.prettierrc` or similar formatters that handle markdown
>
> Report findings as a markdown table with columns: Setting, Value, Source. Example:
>
> | Setting             | Value | Source               |
> |:--------------------|:------|:---------------------|
> | max_line_length     | 120   | .editorconfig [*]    |
> | indent_size (md)    | 2     | .editorconfig [*.md] |
> | trim_trailing_ws    | false | .editorconfig [*.md] |
> | existing_linter_cfg | none  | not found            |

**Agent B: Pattern counting**

> Research only; do not write any files. Find all `.md` files: `find . -name '*.md' -not -path './.git/*' -not -path
> './node_modules/*' -not -path './vendor/*'` If more than 50 files, sample 50: pipe the find command through `shuf -n
> 50` (or `head -50` if `shuf` is unavailable).  Exclude common generated files: `CHANGELOG.md` at the repo root, any
> files in `node_modules/` or `vendor/`.  For each file, count:  - List markers: `-` vs `*` - Emphasis markers: `*` vs
> `_` - Strong markers: `**` vs `__` - Heading style: atx (`##`) vs setext (underlines) - Ordered list style: all `1.`
> vs sequential numbering - Code block style: backtick vs tilde fences - Whether code blocks specify a language -
> Presence of inline HTML - Em dash (`—`) and en dash (`–`) usage - Line length distribution: note the p75, p90, and max
> values  Report as a markdown table with columns: Pattern, Count A, Count B, % A, % B. Example:  | Pattern         |
> Option A   | Count A | Option B    | Count B | % A |
> |:----------------|:-----------|--------:|:------------|--------:|----:| | List marker     | `-` (dash) |      42 |
> `*` (star)  |       8 | 84% | | Emphasis        | `*`        |      30 | `_`         |      20 | 60% | | Code block
> lang | specified  |      45 | unspecified |       5 | 90% |

**Agent C: Linter pass**

> Research only; do not write any files.
> Create a temporary config to ensure all rules are tested regardless of any existing config:
> `echo '{"config":{"default":true}}' > /tmp/.markdownlint-cli2.jsonc`
> Run: `npx markdownlint-cli2 --config /tmp/.markdownlint-cli2.jsonc "**/*.md" 2>&1 || true`
> Delete the temp config after.
>
> Parse the output and count violations per rule across all files.
> Report as a markdown table with columns: Rule, Alias, Violations, Files Affected, % Files.
>
> | Rule  | Alias              | Violations | Files Affected | % Files |
> |:------|:-------------------|-----------:|---------------:|--------:|
> | MD001 | heading-increment  |          0 |              0 |      0% |
> | MD004 | ul-style           |         12 |              3 |     15% |
> | MD013 | line-length        |        234 |             18 |     90% |
> | MD041 | first-line-heading |         45 |             16 |     80% |

## Step 4: Merge results and decide

For each markdownlint rule, combine signals from all three agents:

**Priority order**:

1. `.editorconfig` values are authoritative for overlapping settings (line length, indent size, trailing whitespace).
   If `.editorconfig` has no relevant setting, fall through to signal 2.
2. Pattern counting determines style-preference rules (list markers, emphasis, headings)
3. Linter violation rates confirm which rules the repo already follows

**Decision threshold**:

- 70%+ of files agree on a pattern: auto-pick the majority convention
- Below 70%: ask the user via `AskUserQuestion` with the actual stats. Example: "60% of your files use `-` for
  list markers, 40% use `*`. Which do you prefer?"

**Linter rules**:

- 0 violations across all files: enable the rule (repo follows it)
- Over 70% of files violate: disable the rule (repo intentionally diverges)
- Between 0% and 70%: use the pattern counting signal or ask the user

## Step 5: Choose judgment-based rules

Present a numbered list via `AskUserQuestion` and ask the user to reply with the numbers they want. These are
rules the linter cannot enforce that go into `.claude/rules/markdown.md`:

> Which of these judgment-based rules would you like to include? Reply with the numbers (e.g., "1, 3, 5"):
>
> 1. No em dashes or en dashes (use periods, semicolons, or restructure)
> 1. Parallel grammar in list items (if one starts with a verb, all should)
> 1. Natural link text (descriptions or filenames, not raw URLs or full paths)
> 1. Table complexity limit (restructure if more than 5 columns)
> 1. TOC recommendations for files exceeding 100 lines

Include any that the user selects. Skip the rest.

## Step 6: Generate artifacts

Generate two files based on the decisions:

### `.markdownlint-cli2.yaml`

Read the template at `templates/markdownlint-cli2.yaml` (relative to the `markdown-style` skill directory in this
plugin) for reference on rule IDs and their config options. Generate a config with only the rules that were decided,
grouped by category. Add a header comment:

```yaml
# Generated by /markdown-setup
# Re-run /markdown-setup to regenerate
```

### `.claude/rules/markdown.md`

Read the template at `templates/claude-rules-markdown.md` (relative to the `markdown-style` skill directory in this
plugin) for reference on the structure. Generate a rules file with `globs: ["*.md"]` frontmatter. Include:

- A reference to `.markdownlint-cli2.yaml` as the machine-enforced baseline
- A note that the PostToolUse hook runs the linter with `--fix` after every `.md` edit
- The judgment-based rules the user selected in step 5
- Any project-specific notes

## Step 7: Present and write

Present a summary of all decisions to the user:

- Which rules were auto-picked (with the signal that decided them)
- Which rules were user-chosen
- Which judgment-based rules were included

Ask for final approval via `AskUserQuestion`. Do not write files until the user approves. If the user requests
changes, apply them to the in-memory decisions and re-present the updated summary.
