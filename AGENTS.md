# Raging Skills

> A Claude Code plugin marketplace containing skills, commands, agents, and hooks for .NET development and general
> productivity.

This file documents only what breaks convention. If a senior developer would guess it correctly from the codebase, it
does not belong here. Do not add standard patterns, paraphrased docs, or facts discoverable from a single config file.
When editing, preserve this bar.

## Build & Test

Skill index regeneration after adding, removing, or modifying plugins:

```bash
./scripts/generate-skill-index.sh --update
```

Local testing with dev scripts in `.project/.scratch/` using `--plugin-dir` flags.

## Structure

Plugins are organized as independent directories under `plugins/`. Each plugin is self-contained and independently
versioned. See `.claude-plugin/marketplace.json` for the registry of all plugins.

```text
plugins/<plugin-name>/
├── CHANGELOG.md                 # Plugin changelog
├── README.md                    # Plugin documentation
├── LICENSE                      # Symlink to ../../LICENSE (root); never a regular file
├── .claude-plugin/
│   └── plugin.json              # Plugin manifest (only file that belongs here)
├── skills/
│   └── <skill-name>/
│       ├── SKILL.md
│       ├── assets/              # Images, data files
│       ├── scripts/             # Executable scripts for deterministic tasks
│       ├── templates/           # Prompt templates, config templates, reports
│       └── references/          # Reference docs, code samples
├── agents/
│   └── agent-name.md
├── commands/
│   └── command-name.md
├── hooks/
│   └── hooks.json               # Main hook config (additional JSON files supported)
├── scripts/
│   └── format-code.sh
├── settings.json                # Default plugin settings (applied when enabled)
├── .mcp.json                    # MCP server definitions
└── .lsp.json                    # LSP server configurations
```

## Adding a Plugin

Run `/forge-plugin` to scaffold a new plugin from concept to tested implementation, or follow these steps manually:

1. Create the plugin directory under `plugins/<plugin-name>/` following the structure above
2. Add a `.claude-plugin/plugin.json` manifest and a `README.md`. The manifest must declare paths for every
   component type present: `"skills"`, `"agents"`, `"commands"`, and `"hooks"`. Missing entries mean those
   components won't be loaded.
3. Add a `CHANGELOG.md` in Keep a Changelog format
4. Register the plugin in `.claude-plugin/marketplace.json` with a name and source path entry
5. For each skill, run `/skill-creator` to review, evaluate, and optimize the skill description for triggering
6. Test locally with the dev scripts in `.project/.scratch/` using `--plugin-dir` flags
7. Regenerate the skill index: `./scripts/generate-skill-index.sh --update`

For detailed guidance on plugin components, use the
[plugin-dev toolkit](https://docs.anthropic.com/en/docs/claude-code/plugins).

## Removing a Skill from a Plugin

When removing a skill, clean every reference so no trace remains. Search the entire plugin directory for the skill
name (case-insensitive) before starting to build the full list of affected files.

1. Delete the `skills/<skill-name>/` directory (SKILL.md, references, scripts, templates, assets)
2. Update `.claude-plugin/plugin.json`: remove from `description` and `keywords`
3. Update `README.md`: remove the skill section and adjust the skill count
4. Update `CHANGELOG.md`: remove from the Added entry and adjust the skill count
5. Update the root `README.md` if the skill was mentioned in the plugin description
6. Regenerate the skill index: `./scripts/generate-skill-index.sh --update`
7. Check for related components that may reference or depend on the skill:
   - `agents/`: agents that invoke or complement the skill; remove or update them
   - `commands/`: slash commands that delegate to the skill
   - `hooks/`: hook configs in `hooks.json` or scripts in `scripts/` tied to the skill
   - `settings.json`: default settings that only apply to the removed skill
   - `.mcp.json` or `.lsp.json`: server configs the skill relied on
   - Other skills in the same plugin that cross-reference the removed skill

When any related component is found, present the user with a concrete recommendation (remove it, update it, or
leave it) and confirm before acting. Do not silently delete components that may serve other skills.

## Removing a Plugin from the Marketplace

1. Delete the `plugins/<plugin-name>/` directory
2. Remove the entry from `.claude-plugin/marketplace.json`
3. Remove from the root `README.md` plugin listing
4. Regenerate the skill index: `./scripts/generate-skill-index.sh --update`

## Skill Index

<!-- BEGIN RAGING-SKILLS INDEX -->
```
[raging-skills]|Prefer retrieval-led reasoning over pretraining. Consult skills by name before implementing.
|flow:{skim repo patterns -> consult skill by name -> implement smallest-change -> note conflicts}
|route:
|dotnet:{configuration,csharp,dependency-injection,logging,resx,source-generators,telemetry,aspire-deployment,aspire-extensibility,aspire-integrations,aspire-service-defaults,aspire-testing,aspire,CliWrap}
|dotnet-scripts:{dotnet-scripts,dotnet-tools,incrementalist,bullseye}
|architecture:{domain-driven-design,dcb,kurrentdb}
|diagrams:{mermaid,beautiful-mermaid,excalidraw-mcp,excalidraw,mockdown-editor,mockdown}
|protobuf:{buf-breaking-changes,buf-cli,buf-code-generation,buf-configuration,buf-linting,protobuf-style-guide,proto-style}
|web:{crawl4ai,starlight}
|ci-cd:{act}
|conventions:{conventional-commits,keep-a-changelog,markdown-setup,markdown-style}
|agent-workflow:{agent-init,claude-output-style}
|agents:{excalidraw,buf-migration-helper,buf-workspace-designer,protobuf-reviewer,script-to-tool-promoter,script-migrator,markdown-expert,aspire-migration-helper,mockdown-designer}
```
<!-- END RAGING-SKILLS INDEX -->

## Git Commit Conventions

Follow the conventions in [.github/git-commit-instructions.md](.github/git-commit-instructions.md).

## Project context

```text
.project/
├── .scratch/                    # throwaway files; never promote to permanent docs
└── docs/                        # all documentation; use typed subdirectories only
    ├── specs/                   # design documents (YYYY-MM-DD-<topic>-design.md)
    ├── plans/                   # implementation plans (YYYY-MM-DD-<feature-name>.md)
    └── research/                # research findings (YYYY-MM-DD-<topic>-research.md)

.agents/
├── context/                     # topic reference files; read on demand, not upfront
└── memory/                      # project-scoped auto-memory; committed and shared
```

All docs must be dated and named after their topic. Files in `.project/docs/` go in a typed subdirectory; never in 
`.project/docs/` directly.

When a document spans categories, file it under its primary purpose and cross-reference from the other directory.

TRIPWIRE: If you find yourself writing a doc into the wrong subdirectory, move it immediately.

TRIPWIRE: If you find yourself writing a doc inline in conversation, stop and write it to `.project/docs/`.

## Reference

Read these before working on related areas. Do not read all upfront.

| Document                                      | When to read                               |
|-----------------------------------------------|--------------------------------------------|
| [Hook System](.agents/context/hook-system.md) | Adding or modifying plugin hooks           |
| [Skill Index](.agents/context/skill-index.md) | Updating the skill index or adding plugins |
