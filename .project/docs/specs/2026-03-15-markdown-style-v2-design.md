# Adaptive Markdown Style Plugin v2.0

## Contents

- [Problem statement](#problem-statement)
- [Goals](#goals)
- [Non-goals](#non-goals)
- [Architecture](#architecture)
- [Components](#components)
  - [markdown-setup skill](#1-markdown-setup-skill)
  - [markdown-expert agent](#2-markdown-expert-agent)
  - [Command hook](#3-command-hook)
  - [markdown-style skill](#4-markdown-style-skill-updated)
  - [Templates](#5-templates)
- [Setup workflow](#setup-workflow)
- [Analysis strategy](#analysis-strategy)
- [Generated artifacts](#generated-artifacts)
- [Plugin file structure](#plugin-file-structure)
- [Migration from v1](#migration-from-v1)

## Problem statement

The markdown-style plugin v1.0 ships a static set of conventions (120-char lines, `-` list markers, no em dashes,
etc.) that reflect one author's preferences. When installed into a project that uses different conventions, the skill
fights the codebase instead of helping it. There is no linter integration, no enforcement mechanism, and no way for
the skill to adapt.

## Goals

- Discover a project's existing markdown conventions automatically
- Generate a tailored `markdownlint-cli2` config that codifies those conventions
- Create Claude Code rules (`.claude/rules/markdown.md`) with glob-based auto-loading for LLM-judgment concerns the
  linter cannot enforce
- Bundle a command hook that auto-fixes markdown after every edit
- Provide a markdown-expert agent for on-demand deep reviews
- Keep the setup user-driven; never silently override project conventions
- Make the plugin useful out of the box (static fallback defaults) even without running setup

## Non-goals

- Replacing markdownlint with a custom linter
- Enforcing conventions in CI/CD (users can add that themselves using the generated config)
- Supporting non-markdown file formats
- Building a web UI or interactive config editor

## Architecture

Three tiers of enforcement, each cheaper than the last:

```text
┌─────────────────────────────────────────────────────────────────────────┐
│ TIER 1: Rules file (zero cost)                                         │
│                                                                         │
│ .claude/rules/markdown.md  (globs: ["*.md"])                           │
│ Auto-loaded into context for every .md interaction.                     │
│ Claude self-corrects as it writes. No extra tokens.                    │
└─────────────────────────────────────────────────────────────────────────┘
                              │
                    Claude writes .md file
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────────┐
│ TIER 2: Command hook (zero token cost)                                  │
│                                                                         │
│ PostToolUse hook (type: "command", matcher: "Edit|Write")              │
│ Runs markdownlint-cli2 --fix on the file.                              │
│ Auto-repairs mechanical issues (trailing spaces, blank lines, etc.)    │
│ Reports remaining violations back to Claude for self-correction.       │
└─────────────────────────────────────────────────────────────────────────┘
                              │
                    When deeper review is needed
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────────┐
│ TIER 3: markdown-expert agent (on demand)                               │
│                                                                         │
│ Dispatched by the model when it needs a thorough review.               │
│ Reads config + rules, runs linter, reviews for judgment-based issues.  │
│ Used for PR reviews, bulk doc checks, or complex style questions.      │
└─────────────────────────────────────────────────────────────────────────┘
```

**Setup flow** (one-time, user-initiated):

```text
                    User runs /markdown-setup
                              │
                              ▼
                   ┌─────────────────────┐
                   │  markdown-setup      │
                   │  (user-invocable     │
                   │   skill)             │
                   └──────┬──────────────┘
                          │
              ┌───────────┼───────────────┐
              ▼           ▼               ▼
        ┌──────────┐ ┌──────────┐  ┌──────────┐
        │ Agent A  │ │ Agent B  │  │ Agent C  │
        │ Env scan │ │ Pattern  │  │ Linter   │
        │          │ │ counting │  │ pass     │
        └────┬─────┘ └────┬─────┘  └────┬─────┘
             └─────────────┼─────────────┘
                           ▼
                  ┌─────────────────┐
                  │  Merge results  │
                  │  Apply 70%      │
                  │  threshold      │
                  │  AskUserQuestion│
                  │  on ambiguous   │
                  └────────┬────────┘
                           │
                    ┌──────┴──────┐
                    ▼             ▼
           .markdownlint-   .claude/rules/
           cli2.yaml         markdown.md
```

## Components

### 1. `markdown-setup` skill

**Location**: `skills/markdown-setup/SKILL.md`

**Invocation**: User-invocable only (`/markdown-setup`). The model cannot invoke this skill on its own. Setting up
linting is a project-level decision that requires user intent.

**Responsibility**: Orchestrate the full setup workflow. Dispatch parallel subagents for analysis, merge results,
resolve ambiguities with the user, and generate the two output artifacts.

**Frontmatter**:

```yaml
name: markdown-setup
description: >
  Analyze a repo's markdown conventions and generate a tailored markdownlint config and Claude Code rules.
  Run /markdown-setup to set up or re-run analysis.
user_invocable: true
```

**Behavior**:

1. Check for existing `.markdownlint-cli2.yaml`. If found, ask if the user wants to regenerate
2. Dispatch three parallel subagents (see [Analysis strategy](#analysis-strategy))
3. Merge results, apply 70% threshold per rule
4. For ambiguous rules (below 70%), ask via `AskUserQuestion` with stats
5. Generate `.markdownlint-cli2.yaml` from the `templates/markdownlint-cli2.yaml` baseline
6. Generate `.claude/rules/markdown.md` from the `templates/claude-rules-markdown.md` baseline
7. Present a summary of all decisions for final review before writing files

### 2. `markdown-expert` agent

**Location**: `agents/markdown-expert.md`

**Responsibility**: On-demand deep review of markdown files for style compliance. Dispatched by the model when it
needs a thorough review; not in the hot path of every edit.

**Use cases**:

- "Review all the docs in this PR for style"
- "Check if this README follows our conventions"
- Model encounters linter violations it cannot resolve from the rules file alone

**Capabilities**:

- Read `.markdownlint-cli2.yaml` to understand the machine-enforceable rules
- Read `.claude/rules/markdown.md` to understand the LLM-judgment rules
- Run `npx markdownlint-cli2 --fix <file>` to auto-repair mechanical issues
- Review files for remaining issues the linter cannot catch
- Report violations with file path, line number, and suggested fix

**Context**: The agent instructions are self-contained. They do not assume the markdown-style skill is loaded. The
agent reads the project's generated config files directly.

### 3. Command hook

**Location**: `hooks/hooks.json`

**Event**: `PostToolUse`

**Matcher**: `Edit|Write`

**Type**: `command`

A lightweight shell hook that fires after every `Edit` or `Write` tool call. It checks whether the edited file is
a `.md` file and if so, runs the linter with auto-fix. Zero token cost.

**Configuration**:

```json
{
  "hooks": {
    "PostToolUse": [
      {
        "matcher": "Edit|Write",
        "hooks": [
          {
            "type": "command",
            "command": "FILE=$(jq -r '.tool_input.file_path // .tool_input.file // empty') && [ \"${FILE##*.}\" = 'md' ] && npx markdownlint-cli2 --fix \"$FILE\" 2>&1 && npx markdownlint-cli2 \"$FILE\" 2>&1 || true"
          }
        ]
      }
    ]
  }
}
```

**Flow**:

1. Extract file path from the hook's JSON input
2. Check if the file has a `.md` extension; exit silently if not
3. Run `markdownlint-cli2 --fix` to auto-repair mechanical issues
4. Run `markdownlint-cli2` (without fix) to check for remaining violations
5. If violations remain, the output goes to stderr and Claude receives it as feedback
6. Claude already has `.claude/rules/markdown.md` in context (via glob-based loading) and can self-correct

**Why command, not agent**: The linter handles mechanical fixes deterministically. The rules file is already in
Claude's context via glob-based auto-loading, so Claude can handle judgment-based self-correction without spawning
a subagent. The agent is reserved for on-demand deep reviews where the extra cost is justified.

### 4. `markdown-style` skill (updated)

**Location**: `skills/markdown-style/SKILL.md`

**Role change**: From "here are the rules" to "here is how to find and apply the rules."

**Behavior**:

- If `.markdownlint-cli2.yaml` and `.claude/rules/markdown.md` exist: instruct the agent to follow them. The skill
  becomes a lightweight router.
- If neither exists: provide static fallback defaults (the current v1.0 rules) so the plugin is useful out of the
  box. Mention that `/markdown-setup` is available for tailored rules.
- Keep the table formatter script reference regardless of setup state.

**Opportunistic detection**: When the skill loads before an `.md` edit and no `.markdownlint-cli2.yaml` exists,
briefly mention that `/markdown-setup` can generate a tailored config. Do not prompt or block; just inform.

### 5. Templates

Templates provide baselines that the setup skill fills in based on analysis results.

**`templates/markdownlint-cli2.yaml`**: A commented YAML file with every supported rule, each annotated with what it
controls. The setup skill enables/disables rules and sets values based on analysis. Rules are grouped by category
(prose, structure, lists, code blocks, tables, HTML).

**`templates/claude-rules-markdown.md`**: A Claude Code rules file with `globs: ["*.md"]` in the frontmatter.
Contains placeholder sections for:

- Reference to `.markdownlint-cli2.yaml` as the machine-enforced baseline
- Prose rules (em dash policy, sentence structure guidance)
- List rules (parallel grammar)
- Link rules (link text conventions)
- Table rules (complexity guidance, formatter script reference)
- Any project-specific notes the user adds during setup

## Setup workflow

### Step-by-step flow

```text
1. User runs /markdown-setup
2. Skill checks for existing config
   ├─ Found: "Config exists. Regenerate?" (AskUserQuestion)
   └─ Not found: proceed
3. Dispatch three parallel subagents:
   ├─ Agent A: Environment scan
   ├─ Agent B: Pattern counting
   └─ Agent C: Linter pass
4. Collect results from all three agents
5. For each configurable rule:
   ├─ 70%+ files agree: auto-pick majority convention
   └─ Below 70%: ask user via AskUserQuestion with stats
6. Generate .markdownlint-cli2.yaml
7. Generate .claude/rules/markdown.md
8. Present summary of all decisions
9. User approves or requests changes
10. Write files
```

### Subagent definitions

**Agent A: Environment scan**

- Read `.editorconfig` for `max_line_length`, `indent_size`, `indent_style`, `trim_trailing_whitespace`,
  `insert_final_newline` (both `[*]` and `[*.md]` sections)
- Check for existing `.markdownlint-cli2.yaml`, `.markdownlint.json`, `.markdownlint.jsonc`, `.markdownlint.yaml`
- Check for `.prettierrc` or similar formatters that might handle markdown
- Report all findings as structured data

**Agent B: Pattern counting**

- Sample `.md` files across the repo (all files if under 50; random sample of 50 if more)
- Count: list markers (`-` vs `*`), emphasis markers (`*` vs `_`), strong markers (`**` vs `__`), heading style
  (atx vs setext), ordered list style (`1.` everywhere vs sequential), code block style (backtick vs tilde),
  whether code blocks specify languages, presence of inline HTML
- Measure line length distribution: p50, p75, p90, p95, max across all files
- Report counts and percentages per pattern

**Agent C: Linter pass**

- Run `npx markdownlint-cli2 "**/*.md"` with all rules enabled
- Parse the output; count violations per rule across all files
- Report: rule ID, alias, total violations, number of files affected, percentage of files affected
- High violation count + high file percentage = the repo does not follow that rule
- Low/zero violations = the repo already follows that rule

## Analysis strategy

### Decision logic per rule

For each markdownlint rule, the setup skill combines signals from all three agents:

| Signal source     | What it tells us                                                                  |
| :---------------- | :-------------------------------------------------------------------------------- |
| `.editorconfig`   | Authoritative for overlapping settings (line length, indent, trailing whitespace) |
| Pattern counting  | What the majority of files actually do for style-preference rules                 |
| Linter violations | Which rules the repo already follows vs. violates                                 |

**Priority**: `.editorconfig` values override pattern counting when they overlap. For example, if `.editorconfig`
says `max_line_length = 100` but most files have lines up to 120, the config uses 100 because it was an explicit
project decision.

**Threshold**: 70% agreement among files means auto-pick. Below 70%, ask the user.

**Example decisions**:

- 95% of files use `-` for lists: auto-set `MD004: { style: dash }`
- `.editorconfig` says `max_line_length = 120`: auto-set `MD013: { line_length: 120 }` regardless of actual lengths
- 55% of files use `*` emphasis, 45% use `_`: ask user via `AskUserQuestion`
- 0 violations for MD001 (heading increment): enable the rule (repo already follows it)
- 80% of files violate MD041 (first-line heading): disable the rule (repo intentionally skips it)

### Rules that need LLM judgment

Some conventions cannot be expressed as markdownlint rules. These go into `.claude/rules/markdown.md` instead:

| Convention         | Why linter cannot enforce it                       | Detection method                              |
| :----------------- | :------------------------------------------------- | :-------------------------------------------- |
| No em/en dashes    | Linter has no dash-style rule                      | Agent B scans for `—`, `–` usage              |
| Parallel grammar   | Requires semantic understanding                    | Cannot detect; included as guidance if desired |
| Natural link text  | Requires judgment on what is "natural"             | Cannot detect; included as guidance if desired |
| Table complexity   | "More than 5 columns" is guidance, not a hard rule | Agent B counts max columns in existing tables  |
| TOC recommendations | Context-dependent (file length, document type)    | Included as guidance                           |

During setup, the skill asks the user which of these judgment-based rules they want to include. Presented as a
checklist via `AskUserQuestion`.

## Generated artifacts

### `.markdownlint-cli2.yaml`

Machine-enforceable rules. Example output for a repo that uses `-` lists, 120-char lines, and atx headings:

```yaml
# Generated by /markdown-setup on 2026-03-15
# Re-run /markdown-setup to update

config:
  default: true

  MD004: { style: dash }
  MD007: { indent: 2 }
  MD013:
    line_length: 120
    heading_line_length: 120
    code_block_line_length: 120
    tables: false
  MD024: false
  MD033: true
  MD040: true
  MD041: false
  MD047: true
  MD048: { style: backtick }
  MD049: { style: asterisk }
  MD050: { style: asterisk }
```

### `.claude/rules/markdown.md`

LLM-judgment rules with glob-based auto-loading. The `globs` frontmatter ensures these rules are in context
whenever Claude interacts with a `.md` file.

```markdown
---
globs: ["*.md"]
---

## Markdown conventions

Machine-enforceable rules are defined in `.markdownlint-cli2.yaml` at the repo root. A PostToolUse hook runs
the linter with `--fix` after every `.md` edit. The rules below cover concerns the linter cannot enforce.

### Prose

- Never use em dashes, en dashes, or hyphens to join phrases. Use periods, semicolons, or restructure instead.
- Keep lines close to the configured max length (120 characters). Do not cap at 80 out of habit.

### Lists

- Keep list items parallel in grammar. If one starts with a verb, all should start with a verb.

### Links

- Link text should be a natural description or filename, not a raw URL or full path.

### Tables

- If a table exceeds 5 columns, consider restructuring as a list or subsections.
- Format tables with `npx markdownlint-cli2 --fix` or the table formatter script if available.
```

## Plugin file structure

```text
plugins/markdown-style/
├── .claude-plugin/
│   └── plugin.json              # Updated: add agent, hook, new skill
├── CHANGELOG.md                 # Updated: v2.0.0 entry
├── README.md                    # Updated: new capabilities
├── hooks/
│   └── hooks.json               # Command hook: PostToolUse markdownlint --fix
├── agents/
│   └── markdown-expert.md       # On-demand style review agent
└── skills/
    ├── markdown-style/
    │   ├── SKILL.md             # Updated: adaptive router with fallback
    │   ├── scripts/
    │   │   └── format-tables.cs # Existing: unchanged
    │   └── templates/
    │       ├── markdownlint-cli2.yaml   # Baseline config template
    │       └── claude-rules-markdown.md # Rules file template
    └── markdown-setup/
        └── SKILL.md             # User-invocable setup workflow
```

## Migration from v1

Users upgrading from v1.0 to v2.0:

- The plugin still works without running `/markdown-setup`. The `markdown-style` skill provides the same static
  defaults as v1.0 when no config exists.
- Running `/markdown-setup` generates the config and rules files, which then take priority over the static defaults.
- The command hook is bundled with the plugin and active as soon as the plugin is enabled. Without a
  `.markdownlint-cli2.yaml`, the linter runs with its own defaults.
- No breaking changes. The table formatter script is unchanged. The skill still triggers before `.md` edits.

### Resolved decisions

- **npx cold start**: Accept the first-run delay. No need to install as a dev dependency.
- **Hook location**: Bundled in the plugin's `hooks/hooks.json`. Active when the plugin is enabled.
- **Hook type**: `type: "command"`. Runs the linter deterministically at zero token cost. Claude already has the
  judgment rules in context via glob-based auto-loading of `.claude/rules/markdown.md`.
- **Re-run behavior**: `/markdown-setup` is idempotent. It regenerates and overwrites the config and rules files.
- **Deep reviews**: The markdown-expert agent is available on demand, not triggered automatically by the hook. The
  model dispatches it when a thorough review is needed (PR reviews, bulk checks, complex style questions).
