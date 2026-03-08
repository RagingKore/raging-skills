# Plugin Marketplace Structure

Raging Skills is a Claude Code plugin marketplace containing a curated collection of skills, commands, agents, 
and hooks for .NET development and general productivity. 

Most skills are packaged as plugins so users can choose exactly which capabilities to install.

## Organization

Plugins are organized as independent directories under `plugins/`:

```text
plugins/<plugin-name>/
│
├── CHANGELOG.md                 # Plugin changelog
├── README.md                    # Plugin documentation
├── LICENSE                      # License file
│
├── .claude-plugin/
│   └── plugin.json              # Plugin manifest (only file that belongs here)
│
├── skills/                      # Skill definitions
│   └── <skill-name>/
│       ├── SKILL.md
│       ├── assets/              # Images, data files
│       ├── scripts/             # Executable scripts for deterministic tasks
│       ├── templates/           # Prompt templates, config templates, reports
│       └── references/          # Reference docs, code samples
│
├── agents/                      # Autonomous subagents
│   └── agent-name.md
│
├── commands/                    # Slash commands (legacy; prefer skills/ for new work)
│   └── command-name.md
│
├── hooks/                       # Hook configurations
│   └── hooks.json               # Main hook config (additional JSON files supported)
│
├── scripts/                     # Hook and utility scripts
│   └── format-code.sh
│
├── settings.json                # Default plugin settings (applied when enabled)
├── .mcp.json                    # MCP server definitions
└── .lsp.json                    # LSP server configurations
```

## Adding a Plugin

Each plugin is self-contained and independently versioned. 

Run `/forge-plugin` to scaffold a new plugin from concept to tested implementation, or follow these steps manually:

1. Create the plugin directory under `plugins/<plugin-name>/` following the structure above
2. Add a `.claude-plugin/plugin.json` manifest (Claude Code's standard format) and a `README.md`
3. Add a `CHANGELOG.md` in Keep a Changelog format
4. Register the plugin in `.claude-plugin/marketplace.json` with a name and source path entry
5. For each skill, run `/skill-creator` to review, evaluate, and optimize the skill description for triggering
6. Test locally with the dev scripts in `.project/.scratch/` using `--plugin-dir` flags
7. Regenerate the skill index: `./scripts/generate-skill-index.sh --update`

The marketplace is distributed via GitHub and can be added to Claude Code with a single command.

For detailed guidance on plugin components, use the [plugin-dev toolkit](https://docs.anthropic.com/en/docs/claude-code/plugins).

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
|dotnet:{configuration,csharp,dependency-injection,logging,resx,source-generators,telemetry}
|dotnet-scripts:{dotnet-scripts,dotnet-tools,incrementalist,bullseye}
|architecture:{domain-driven-design,dcb,kurrentdb}
|diagrams:{mermaid,beautiful-mermaid,excalidraw}
|protobuf:{buf-breaking-changes,buf-cli,buf-code-generation,buf-configuration,buf-linting,protobuf-style-guide,proto-style}
|web:{crawl4ai,astro}
|conventions:{conventional-commits,keep-a-changelog,markdown-style}
|agent-workflow:{project-setup,claude-output-style}
|agents:{excalidraw,buf-migration-helper,buf-workspace-designer,protobuf-reviewer,script-to-tool-promoter,script-migrator}
```
<!-- END RAGING-SKILLS INDEX -->

## Git Commit Conventions

Follow the conventions in [.github/git-commit-instructions.md](.github/git-commit-instructions.md).
