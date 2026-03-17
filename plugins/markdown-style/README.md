# Markdown Style

Adaptive markdown conventions with markdownlint integration.

## Overview

This plugin helps maintain consistent markdown formatting across a project. Instead of imposing a fixed set of rules,
it discovers your project's existing conventions and generates a tailored linter config and Claude Code rules file.

Three tiers of enforcement work together:

1. **Rules file** (`.claude/rules/markdown.md`): auto-loaded into context for every `.md` interaction via
   glob-based matching. Claude self-corrects as it writes. Zero token cost.
2. **Command hook**: runs `markdownlint-cli2 --fix` after every `.md` edit. Auto-repairs mechanical issues like
   trailing spaces, missing blank lines, and inconsistent markers. Zero token cost.
3. **markdown-expert agent**: available on demand for thorough style reviews (PR doc reviews, bulk checks).
   Uses Haiku for cost efficiency.

The plugin works out of the box with sensible defaults. Run `/markdown-setup` to generate project-specific config.

## Skills

### markdown-style (auto-loaded)

Activates before writing or editing any `.md` file. Checks for project-specific config first
(`.claude/rules/markdown.md`, `.markdownlint-cli2.yaml`, `.editorconfig`). Falls back to built-in defaults if
no config exists.

### markdown-setup (user-invocable)

Run `/markdown-setup` to analyze the repo's markdown files and generate:

- `.markdownlint-cli2.yaml`: machine-enforceable linter config tailored to your conventions
- `.claude/rules/markdown.md`: LLM-judgment rules with `globs: ["*.md"]` for auto-loading

The setup dispatches parallel subagents to scan `.editorconfig`, count patterns across files, and run the linter.
Rules with 70%+ agreement are auto-picked; ambiguous ones are presented for your decision.

## Agents

### markdown-expert

On-demand style reviewer. Dispatched by the model when a thorough review is needed. Reads the project's config and
rules, runs the linter, and reports violations with file path, line number, and suggested fix.

## Hooks

### PostToolUse (Edit|Write)

Runs `markdownlint-cli2 --fix` on edited `.md` files. Mechanical issues are auto-repaired; remaining violations
are reported back to Claude for self-correction.
