# Changelog

## [2.0.0] - 2026-03-15

### Added

- `/markdown-setup` skill: analyzes a repo's markdown conventions via parallel subagents and generates a tailored
  `markdownlint-cli2` config and `.claude/rules/markdown.md` rules file
- `markdown-expert` agent for on-demand deep style reviews (uses Haiku for cost efficiency)
- PostToolUse command hook that runs `markdownlint-cli2 --fix` after every `.md` edit
- Config template (`templates/markdownlint-cli2.yaml`) with all rules annotated
- Rules template (`templates/claude-rules-markdown.md`) with `globs: ["*.md"]` for auto-loading

### Changed

- `markdown-style` skill now adapts to project config: checks `.claude/rules/markdown.md`,
  `.markdownlint-cli2.yaml`, and `.editorconfig` before falling back to static defaults
- Plugin description updated to reflect adaptive behavior

## [1.0.0] - 2026-03-08

### Added

- Markdown conventions skill covering prose, structure, links, lists, code blocks, and tables
- 120 character line limit and em dash prohibition rules
- Table formatting script for automatic column alignment
