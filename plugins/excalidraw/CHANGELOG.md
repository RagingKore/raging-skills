# Changelog

## [3.0.0] - 2026-03-11

### Added

- New `excalidraw-mcp` skill for live browser preview via the Excalidraw MCP server
- MCP element format reference (`references/mcp-format.md`) covering element types, pseudo-elements,
  camera sizes, dark mode, and text contrast rules
- MCP element templates (`references/mcp-templates.md`) with copy-paste skeletons for all element types
- MCP examples (`references/mcp-examples.md`) with full working diagrams including camera animations,
  sequence diagrams, animation mode, and layout patterns
- Plugin-level `.mcp.json` for automatic MCP server configuration on install
- MCP-specific subagent delegation templates in agent definition (create, modify, export)

### Changed

- Agent definition updated with MCP delegation patterns alongside existing raw JSON patterns
- Plugin description updated to reflect both skills (raw JSON + MCP live preview)
- Added `mcp`, `live-preview`, `camera` keywords to plugin manifest

## [2.0.0] - 2026-03-11

### Added

- Design methodology reference with core philosophy, depth assessment, visual pattern library,
  container discipline, and large diagram strategy
- Element templates reference with copy-paste JSON for each element type
- Render-to-PNG pipeline (Playwright + Chromium) for mandatory visual validation
- YAML color palette template (`color-palette-template.yml`) with automatic first-run
  initialization via `settings.json` and AskUserQuestion
- Semantic and component-type color palettes in a single customizable YAML file
- Color system documentation (`color-palette.md`) with rules, guidance, and cloud palettes
- JSON schema quick-reference (`json-schema.md`)
- PreToolUse hook to check palette initialization before writing diagrams
- Render-validate loop section in validation reference
- Step 0 (Assess Depth) in unified workflow

### Changed

- Broadened scope from architecture-only to any diagram type
- Default `roughness` changed from 1 to 0 (clean, crisp edges)
- Default `fontFamily` changed from 1 to 3 (monospace for technical text)
- Default `opacity` enforced at 100 (no transparency)
- SKILL.md rewritten with merged workflow (Steps 0-6)
- Replaced `colors.md` with `color-palette.md` (rules) + `color-palette-template.yml` (values)
- Validation reference updated with render-validate loop

## [1.0.0] - 2026-03-08

### Added

- Architecture diagram generation skill producing .excalidraw files from codebase analysis
- Arrow routing reference with elbow arrow algorithm
- Color palettes for default, AWS, Azure, GCP, and Kubernetes
- Frame-based grouping with JSON layout patterns
- Validation reference with pre-flight checks and common bug fixes
