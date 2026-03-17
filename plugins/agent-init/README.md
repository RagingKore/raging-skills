# Agent Init

Prepare any codebase for agent collaboration. Analyzes the project using parallel discovery agents and generates
persistent agent context that follows a strict editorial philosophy: document only what breaks convention.

Follows the [AGENTS.md](https://agents.md/) convention; one file works across 25+ agent tools including Claude
Code, OpenAI Codex, Google Jules, Cursor, and GitHub Copilot.

## Why

Most CLAUDE.md and AGENTS.md files are bloat. They restate obvious conventions, paraphrase framework docs, and
list things any senior developer would infer from the codebase in seconds. Agents dutifully read all of it,
burning context on information they already know.

This plugin takes the opposite approach: discover what actually breaks convention and document only that. Three
parallel agents scan the codebase, a human approves the findings, and the output is a lean briefing that earns
every token it costs.

## Skills

### User-invoked

**Agent Init** (`/agent-init [force]`)

Dispatches three parallel agents (compliance, build, structure) to scan the codebase, synthesizes findings,
presents topics for user approval, and generates context files from templates. Supports two modes:

- **Fresh mode**: full codebase scan and generation from scratch
- **Review mode**: delta analysis against existing `AGENTS.md` and `.agents/context/` files with targeted updates

Auto-detects installed agent tools and offers symlinks for `CLAUDE.md`, `GEMINI.md`,
`.github/copilot-instructions.md`, and `.junie/guidelines.md` pointing to `AGENTS.md`.
