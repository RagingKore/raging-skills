# Changelog

## [1.0.0] - 2026-03-16

### Added

- Agent context generator skill with parallel discovery agents (compliance, build, structure)
- `AGENTS.md` as single source of truth following the [AGENTS.md convention](https://agents.md/)
- Auto-detection of installed agent tools to offer symlinks (`CLAUDE.md`, `GEMINI.md`,
  `.github/copilot-instructions.md`, `.junie/guidelines.md`)
- `.agents/context/{topic}.md` reference files for progressive disclosure
- `.project/` directory scaffolding (docs/specs, docs/plans, docs/research, .scratch)
- Editorial philosophy enforced in the `AGENTS.md` template: document only what breaks convention
- Templates for agents.md, topic files, setup reports, and review reports
- Fresh mode (full scan) and review mode (delta analysis) with `force` argument to reset
- Upgrade mode: auto-detect legacy `project-setup` layout (`.project/project.md`) and migrate to `AGENTS.md` +
  `.agents/context/` with symlink repointing
- Memory preservation: `.agents/memory/` is never discarded even in `force` mode
- Optional project-scoped memory at `.agents/memory/` with `SessionStart` hook for auto-configuration
- `SessionStart` hook auto-triggers initialization on first use and monitors for context drift
