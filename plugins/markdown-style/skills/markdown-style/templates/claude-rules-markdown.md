---
globs: ["*.md"]
---

## Markdown conventions

Machine-enforceable rules are defined in `.markdownlint-cli2.yaml` at the repo root. A PostToolUse hook runs the
linter with `--fix` after every `.md` edit. The rules below cover concerns the linter cannot enforce.

NEVER add inline markdownlint comments (e.g., `<!-- markdownlint-disable -->`) to any markdown file. All rule
configuration belongs in `.markdownlint-cli2.yaml`. If a rule causes false positives, disable it in the config.

### Prose

- Never use em dashes, en dashes, or hyphens to join phrases. Use periods, semicolons, or restructure instead.
- Keep lines close to the configured max length. Do not cap at 80 characters out of habit.

### Lists

- Keep list items parallel in grammar. If one starts with a verb, all should start with a verb.

### Links

- Link text should be a natural description or filename, not a raw URL or full path.

### Tables

- If a table exceeds 5 columns, consider restructuring as a list or subsections.

### Long files

- Consider adding a table of contents (`## Contents` with anchor links) when a file exceeds 100 lines.
