---
name: markdown-style
description: |
  Markdown writing conventions for creating, editing, or reviewing markdown files (.md). Covers README files,
  documentation, changelogs, skill files, project plans, and any prose written in markdown format. Adapts to
  project-specific markdownlint config and Claude Code rules when they exist; falls back to built-in conventions
  otherwise.
---

## How to find the rules

This skill adapts to the project. Check for these files before applying any conventions:

1. **`.claude/rules/markdown.md`** (glob-loaded for `*.md` files): LLM-judgment rules the linter cannot enforce.
   If this file exists, it is already in your context. Follow it.
2. **`.markdownlint-cli2.yaml`** (or equivalent markdownlint config): machine-enforceable rules. The PostToolUse hook
   runs the linter with `--fix` after every `.md` edit. You do not need to run it manually.
3. **`.editorconfig`**: baseline settings like `max_line_length` and `indent_size`.

If none of these exist, use the fallback defaults below and mention that `/markdown-setup` can generate tailored
rules for this project.

## Fallback defaults

Use these only when no project-specific config exists.

### Prose

- Keep lines under yet close to 120 characters. This improves readability in side-by-side diffs and on smaller
  screens.
- Never use em dashes, en dashes, or hyphens to join phrases. Use periods, semicolons, or restructure the sentence
  instead.

TRIPWIRE: If you find yourself capping lines at 80 characters out of habit, reflow the text to use more of the line.

### Structure

- One blank line before and after headings
- One blank line before and after lists
- One blank line before and after code blocks
- No skipping heading levels (h1 -> h3). Always step incrementally.
- No decorative separators (ASCII art, `---` between every section, banner comments)
- Use `##` as the top-level heading in most files. Reserve `#` for the document title only.
- Consider adding a TOC (`## Contents` with anchor links) when the file exceeds 100 lines, unless working on
  system documents like:
  - SKILL.md
  - Agent instructions
  - Slash commands

### Links

- Link text should be a natural description or filename, not the full path.

### Lists

- Use `-` for unordered lists, not `*`
- Use `1. 2. 3. ...` for ordered lists only when sequence matters
- Keep list items parallel in grammar. If one starts with a verb, all should start with a verb.

### Code blocks

- Always specify the language for fenced code blocks (`sh`, `csharp`, `json`, etc.)
- Use inline backticks for code references within prose (`ClassName`, `methodName`)

### Tables

- Always align columns with padding so the pipes form straight vertical lines
- Keep tables simple. If a table has more than 5 columns, consider restructuring as a list or subsections.
- Format tables with the `format-tables.cs` script from this plugin's `scripts/` directory, or pipe content via
  stdin: `dotnet <plugin-path>/skills/markdown-style/scripts/format-tables.cs <file>`

TRIPWIRE: if you notice misaligned tables, run the formatter script instead of manually adjusting spaces.

### What NOT to do

- Do not add "last updated" timestamps unless the user explicitly asks for it
- Do not use emoji unless the user explicitly asks for it
- Do not wrap prose in HTML tags
