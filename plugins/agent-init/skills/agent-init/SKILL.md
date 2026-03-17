---
name: agent-init
description: Scan a codebase and generate AGENTS.md briefing with .agents/context/ reference
  files. Use when initializing agent context for a new project, onboarding, or refreshing
  outdated agent instructions. Manages symlinks for CLAUDE.md, GEMINI.md, and other agent
  instruction files.
argument-hint: "[force]"
---

Analyze this codebase and prepare it for agent collaboration. Generate (or update) the `AGENTS.md` briefing and
`.agents/context/` reference files. This is the agent's persistent context for working in this codebase.

## Contents

- [Arguments](#arguments)
- [Philosophy](#philosophy)
- [Editorial standard](#editorial-standard)
- [Phase 1: Discovery](#phase-1-discovery)
  - [Mode detection](#mode-detection)
  - [Migration](#migration)
- [Phase 2: User review](#phase-2-user-review)
  - [Fresh mode](#fresh-mode)
  - [Review mode](#review-mode)
  - [Upgrade mode](#upgrade-mode)
- [Phase 3: Generation](#phase-3-generation)
- [Phase 4: Completion](#phase-4-completion)

## Arguments

- No argument: update existing files if `AGENTS.md` exists, upgrade from legacy `project-setup` if `.project/project.md`
  exists, otherwise generate from scratch
- `force`: discard existing `AGENTS.md` and `.agents/context/` files and generate everything from scratch.
  `.agents/memory/` is **never** discarded — memory persists across all modes including force.

## Philosophy

Document **only what breaks convention**. If a senior developer would guess it correctly from the codebase, omit
it. Custom patterns, unorthodox tooling, surprising constraints, multi-step procedures, and anything that would
save time for someone joining deserve documentation.

This principle governs every content decision. When in doubt, leave it out.

## Editorial standard

These rules constrain all phases. Read before discovery; apply during generation.

### Include

- Anything that would surprise a senior developer examining this codebase
- Cross-cutting patterns not obvious from any single file
- Non-obvious build, test, deployment, or setup workflows
- Security-relevant patterns (auth flows, secrets management, threat model)
- References to where rules or conventions live; do not copy their content
- `file:line` pointers to authoritative code, not pasted snippets

### Exclude

- Deep file trees; only include top-level structure if it has non-standard patterns. Do not document every
  directory.
- Directory listings, dependency lists, API surface enumerations
- Generic practices ("write tests", "handle errors", "use meaningful names")
- Facts discoverable from a single config file (`package.json`, `pyproject.toml`, `*.csproj`, etc.)
- Code style rules enforced by `.editorconfig`, linters, or formatters
- Standard project structure narration ("src/ contains source code")
- Explanations of standard technologies; the agent already knows them
- Paraphrased external documentation; point to official sources instead
- Speculative "tips" sections not grounded in project files
- Information already in `README.md`
- Timestamps and version numbers unless they determine code behavior

### Format

- Each fact lives in one place. Do not repeat information across files.
- Follow the structure in the corresponding template (see [Templates](#templates)).

## Phase 1: Discovery

### Mode detection

If the `force` argument was passed, enter fresh mode regardless of existing files.

Otherwise, check in order:

1. **`AGENTS.md` exists**: enter review mode. Read existing `AGENTS.md` and all `.agents/context/` files first, then
   scan the codebase for deltas: what is outdated, missing, or violates the philosophy.
2. **`.project/project.md` exists (no `AGENTS.md`)**: enter upgrade mode. This is a legacy `project-setup` layout.
   Run the [upgrade migration](#upgrade-migration) first, then continue with parallel agents.
3. **Neither exists**: enter fresh mode. Full codebase scan.

### Migration

The predecessor skill (`project-setup`) stored its output differently:

| Legacy path                      | New path                     |
|----------------------------------|------------------------------|
| `.project/project.md`            | `AGENTS.md`                  |
| `.project/project-{topic}.md`    | `.agents/context/{topic}.md` |
| Symlinks → `.project/project.md` | Symlinks → `AGENTS.md`       |

Run this migration before dispatching the parallel agents:

1. **Read** `.project/project.md` and every `.project/project-*.md` file.
2. **Create** `.agents/context/` if it does not exist.
3. **Write** `AGENTS.md` using the content from `.project/project.md`, adapting it to the `agents.md` template
   structure. Preserve all meaningful content; reformat section headings and layout to match the new template.
4. **Move** each `.project/project-{topic}.md` to `.agents/context/{topic}.md`, adapting content to the `topic.md`
   template structure.
5. **Delete** the migrated files: `.project/project.md` and all `.project/project-{topic}.md`. Leave `.project/docs/`,
   `.project/.scratch/`, and any non-`project-*` files untouched.
6. **Fix symlinks**: find every symlink in the repo that targets `.project/project.md` (commonly `CLAUDE.md`,
   `GEMINI.md`, `.github/copilot-instructions.md`, `.junie/guidelines.md`) and repoint it to `AGENTS.md`. Use
   relative paths appropriate for each symlink's location. Dangling or wrong-target symlinks are expected here;
   replace them without asking.

After migration, the repo looks like a review-mode project. The parallel agents then scan for deltas against the
freshly migrated content.

### Parallel agents

Dispatch three read-only agents in parallel using the `Agent` tool (`subagent_type: "Explore"`) to scan
independent categories. You (the lead) act as strategist and synthesize their findings.

Do not scan the codebase yourself. The discovery work happens entirely through these three agents. Your job is to
write good prompts, wait for results, and synthesize. This parallelization is what makes the skill fast; doing the
work inline defeats the purpose.

TRIPWIRE: If you find yourself reading source files to discover project patterns instead of dispatching agents,
stop. Write the agent prompts and delegate.

**Compliance agent**: Scan agent instruction files (CLAUDE.md, AGENTS.md, GEMINI.md,
`.github/copilot-instructions.md`, `.junie/guidelines.md`), validate policy consistency, check for secrets or
sensitive config in tracked files.

**Build agent**: Analyze build pipelines, tooling, CI/CD patterns, test infrastructure. Identify non-standard
workflows, custom build steps, unusual tooling choices.

**Structure agent**: Examine codebase layout, entry points, module boundaries, protocol layers. Identify monorepo
structures, multi-workspace patterns, or unusual architectural boundaries.

Each agent prompt must include:

- The agent's specific focus area (from above)
- The editorial standard (include/exclude lists from this skill)
- Instruction to use `file:line` pointers for every finding, not prose descriptions
- Output format: key findings, risks identified, `file:line` pointers
- In review mode: the existing `AGENTS.md` and `.agents/context/` content relevant to their area

### Synthesis

After all agents report back:

- Consolidate findings across all three agents
- Flag monorepos, multi-workspace projects, or unusual structures that affect the documentation approach
- Identify security implications (secrets in config, credential patterns)
- Detect documentation gaps
- Determine which topics warrant their own `.agents/context/{topic}.md` file. The bar: if working in that area
  without reading the reference file would lead to mistakes or wasted time, it deserves its own file.
- In review mode: identify which existing files need updates, additions, or removals

## Phase 2: User review

Present findings and gather approvals in a single consolidated interaction. Use `AskUserQuestion` with concrete
options. Minimize the number of sequential prompts.

### Fresh mode

One `AskUserQuestion` call with up to 3 questions:

**Q1 (required)**: Topic approval.

- header: `Topics` (max 12 chars)
- multiSelect: true
- List each discovered topic as an option with a description explaining why it warrants its own file
- If more than 4 topics: batch across multiple questions or group into categories
- The automatic "Other" option lets the user suggest additional topics

**Q2 (conditional)**: Symlink configuration. Only ask if at least one symlink path needs action (see
[Symlinks](#symlinks) for detection logic). Skip entirely when all symlinks are already correct.

- header: `Symlinks`
- multiSelect: true
- For each candidate path that needs action: list with a description of which tool it supports
- For paths where a regular file already exists: append a data loss warning to the description
- Pre-select `CLAUDE.md` if it needs creating (most common case)

**Q3 (conditional)**: Ambiguity resolution. Only if the agents found conflicting patterns or the codebase has
structural ambiguity (monorepo scope, competing conventions).

- header: `Scope` or `Conflict`
- Single-select with 2-3 options describing the competing approaches

### Review mode

One `AskUserQuestion` call with up to 3 questions:

**Q1**: Proposed changes approval.

- header: `Changes`
- multiSelect: true
- Group changes by action: updates to existing content, new sections, removals
- Use `markdown` preview on options to show before/after comparisons when helpful

**Q2 (conditional)**: New topics. Only if the review discovered areas that lack coverage.

- header: `New Topics`
- multiSelect: true

**Q3 (conditional)**: Symlink changes. Same logic as fresh mode Q2.

### Upgrade mode

One `AskUserQuestion` call with up to 3 questions:

**Q1 (required)**: Migration summary.

- header: `Upgrade`
- multiSelect: true
- Summarize what was migrated: files moved, topics carried over, symlinks repointed
- List each migrated topic as an option so the user can deselect any they do not want to keep
- Pre-select all topics (the default is to keep everything)

**Q2 (conditional)**: Proposed changes from discovery agents. Only if the agents found deltas against the migrated
content (outdated information, missing coverage, stale references).

- header: `Changes`
- multiSelect: true
- Same format as review mode Q1: group by action (updates, additions, removals)

**Q3 (conditional)**: Symlink configuration. Only if new symlink candidates were detected beyond what the migration
already fixed, or if the migration found regular files at symlink paths.

- header: `Symlinks`
- Same logic as fresh mode Q2

### Project-scoped memory (all modes)

After the mode-specific questions, add one more question if `useProjectMemory` is not already `true` in
`${CLAUDE_PLUGIN_ROOT}/settings.json`:

**Memory question**: Project-scoped memory opt-in.

- header: `Memory`
- question: "Store auto-memory in `.agents/memory/` (committed, shared with collaborators) instead of the default
  global path?"
- Options: "Yes, use project memory" / "No, keep default"

If the user opts in, set `useProjectMemory` to `true` in `${CLAUDE_PLUGIN_ROOT}/settings.json`. The `SessionStart`
hook will then configure `autoMemoryDirectory` on subsequent sessions.

Skip this question if `useProjectMemory` is already `true` (the user already opted in on a previous run).

### Fallback behaviors

- **User rejects all topics**: generate `AGENTS.md` only. Note in the final report that no topic files were
  created.
- **Discovery finds nothing non-standard**: report this to the user. Confirm whether to generate a minimal
  `AGENTS.md` or skip entirely.
- **Agent returns no findings**: proceed with available data. Note the gap in synthesis.

## Phase 3: Generation

### Templates

Read templates from `${CLAUDE_SKILL_DIR}/templates/` before generating files.

| Template             | Use when                           | Output path                  |
|----------------------|------------------------------------|------------------------------|
| `agents.md`          | Always; the briefing file          | `AGENTS.md`                  |
| `topic.md`           | Each approved topic                | `.agents/context/{topic}.md` |
| `init-report.md`     | Fresh mode; final summary          | Displayed to user, not saved |
| `review-report.md`   | Review mode; changes               | Displayed to user, not saved |
| `upgrade-report.md`  | Upgrade mode; migration + changes  | Displayed to user, not saved |

Omit any template section that would be empty. Never create files without meaningful content.

### File operations

- **Fresh mode**: use `Write` to create new files from templates.
- **Review mode**: use `Edit` for targeted changes to existing files. Do not rewrite entire files; preserve
  inline comments, formatting nuances, and content you did not change.
- **Upgrade mode**: migration already created the files in Phase 1. Apply any user-approved changes from Phase 2
  using `Edit`, same as review mode.

### Directory scaffolding

Create these directory structures if they do not exist:

```text
.project/
├── .scratch/
└── docs/
    ├── specs/
    ├── plans/
    └── research/

.agents/
├── context/       # topic reference files generated by this skill
└── memory/        # project-scoped auto-memory (committed, shared across collaborators)
```

Do not create files inside these directories; only ensure the directories exist.

### Memory preservation

`.agents/memory/` is a **protected directory** across all modes, including `force`. Only `AGENTS.md` and
`.agents/context/` are ever discarded — memory is never deleted or overwritten.

When the user opts into project memory, the skill sets `autoMemoryDirectory` to `.agents/memory/` in
`.claude/settings.local.json`. After a restart, Claude automatically begins writing to the new path. No manual
copying is needed.

### Symlinks

`AGENTS.md` is the single source of truth. All symlinks are optional and user-approved via the Phase 2 question.
Some tools (Cursor, Codex, Jules, Roo Code) read `AGENTS.md` natively and need no symlink.

#### Detection and candidate list

Run these checks to build the candidate list. Use system-level signals (CLIs in PATH, global config dirs,
IDE installations); do not rely on repo contents since the repo may be empty.

| Symlink                           | Tool            | Detect via                                                                                       |
|-----------------------------------|-----------------|--------------------------------------------------------------------------------------------------|
| `CLAUDE.md`                       | Claude Code     | `which claude` or `~/.claude/` exists                                                            |
| `GEMINI.md`                       | Gemini CLI      | `which gemini` or `~/.gemini/` exists                                                            |
| `.github/copilot-instructions.md` | GitHub Copilot  | `which copilot` or `~/.vscode/extensions/GitHub.copilot*` exists                                 |
| `.junie/guidelines.md`            | JetBrains Junie | `~/Library/Application Support/JetBrains/` (macOS) or `~/.local/share/JetBrains/` (Linux) exists |

If detection finds no tools, skip the symlink question entirely. The user can re-run the skill later if they
install new tools.

#### Status detection

For each detected candidate, determine status:

- **Already a symlink pointing to `AGENTS.md`**: no action needed; exclude from the question
- **Symlink pointing to a wrong or deleted target**: broken — recreate it pointing to `AGENTS.md` automatically
- **Regular file exists**: include with a data loss warning in the description
- **Does not exist**: include with a description of which tool it supports
- **Parent directory missing**: create the directory when the user approves the symlink

Skip the symlink question entirely if all candidates are already correct or no tools were detected.

### Final report

Present the appropriate report template to the user:

- Fresh mode: use `init-report.md`
- Review mode: use `review-report.md`
- Upgrade mode: use `upgrade-report.md`

## Phase 4: Completion

Ask the user one final question via `AskUserQuestion`:

- header: `Review`
- question: "Agent context generated. Want to adjust anything?"
- Options: "Looks good" / "Make changes" (with description: "Describe what to adjust using the Other option or
  select this to review specific files")

If the user requests changes, make the edits and present a final diff. Do not re-enter the full workflow.

### Plugin settings

After the user approves the final output, execute these scripts via `Bash`:

1. Update plugin settings in a single call:
   - User opted into project memory:
     `${CLAUDE_PLUGIN_ROOT}/scripts/plugin-settings.py set '{"initialized": true, "useProjectMemory": true}'`
   - User declined project memory:
     `${CLAUDE_PLUGIN_ROOT}/scripts/plugin-settings.py set '{"initialized": true}'`
2. If the user opted into project memory, run via `Bash`:
   `${CLAUDE_PLUGIN_ROOT}/scripts/enable-local-memory.py` — then tell the user to restart Claude Code.

### Memory breadcrumb

After updating plugin settings, write a concise entry to the project's auto-memory recording:

- Date of generation or review
- Key structural signals discovered (build system, package count, protocols, module boundaries)
- A note: "Suggest `/agent-init` if project structure has diverged from these signals"

Keep the entry short (5-10 lines). In future sessions, Claude sees this breadcrumb at startup and can judge
whether the project has evolved enough to warrant re-running the skill.
