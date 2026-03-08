---
name: claude-output-style
description: >
  Use when creating, editing, or reviewing custom output styles for Claude Code.
  Also use when someone asks about output styles, the /output-style command,
  how to customize Claude Code's response format, or how to turn Claude Code
  into a non-coding agent. Triggers on phrases like "create an output style",
  "custom output style", "change how Claude responds", or "output style format".
---

## Overview

Output styles control how Claude Code formats its responses. They are markdown files
with YAML frontmatter that modify or replace Claude Code's system prompt. This skill
teaches how to write effective output styles.

Output styles live in `~/.claude/output-styles/` (global) or `.claude/output-styles/`
(project-level). Activate them with `/output-style [name]` or set `outputStyle` in
settings JSON.

## File Format

```markdown
---
name: Style Name
description: Shown in the /output-style picker UI
keep-coding-instructions: true
---
Your custom instructions here...
```

### Frontmatter fields

- **name** (required): Display name used to reference the style in settings and the
  `/output-style` picker
- **description** (required): One-line summary shown in the picker UI. Keep it under
  80 characters
- **keep-coding-instructions** (optional, default `false`): Whether to retain Claude
  Code's built-in software engineering instructions

### The keep-coding-instructions decision

This is the most important choice:

- Set to `true` when the style **enhances** coding behavior. Claude keeps all SE
  instructions (test verification, file editing, code review patterns) and your
  style adds to them. Use for: security reviewer, TDD coach, verbose explainer.
- Leave as `false` (default) when the style **replaces** the SE personality. Claude
  becomes whatever the style defines while retaining full tool access (file read/write,
  bash, web search). Use for: business analyst, research assistant, content strategist.

Common mistake: forgetting the default is `false`. If you want a coding-focused style,
you must explicitly set `keep-coding-instructions: true`.

## Writing Effective Instructions

### Be specific about behavior

State concrete actions, not abstract qualities.

```
Good: "Before writing any code, write a failing test that captures the requirement."
Bad:  "Be test-driven."
```

### Use imperative mood

Direct commands, not suggestions.

```
Good: "Explain each decision before implementing it."
Bad:  "You should try to explain your decisions when possible."
```

### Define output format when needed

If you want structured responses, specify the format explicitly.

```
Good: "Format every code review as: Summary, Issues (with severity), Recommendations."
Bad:  "Give structured feedback."
```

### Set boundaries

Tell the style what NOT to do as clearly as what to do.

```
Good: "Never modify code without explaining the change first. Ask before refactoring."
Bad:  "Be careful with changes."
```

### Keep it concise

Output styles work best when short and focused. Claude follows 5-15 lines of clear
directives better than 50 lines of detailed instructions. Start minimal; add constraints
only as you discover gaps through testing.

## How Output Styles Interact with Other Features

- **CLAUDE.md**: Injected as a user message after the system prompt. Supplements your
  style; does not conflict
- **Skills**: Invoked on demand for specific tasks. Output styles are always active
- **Agents/subagents**: Have their own context. Output styles do not affect subagent
  behavior
- **--append-system-prompt**: Appends to the system prompt alongside the output style
- **Hooks**: Run independently of output styles

## Anti-patterns

- **Too vague**: "Be helpful and clear" adds nothing over the default
- **Too restrictive**: "Never use more than 10 words" breaks tool usage explanations
- **Conflicting with tools**: "Never write files" when Claude needs to write code
- **Missing format spec**: Expecting structured output without defining the structure
- **Forgetting the default**: Accidentally removing SE behavior by not setting
  `keep-coding-instructions: true`
- **Overly long**: 50+ lines of instructions dilute the most important directives

## Built-in Styles

Claude Code ships with three built-in output styles:

- **Default**: Standard SE-focused behavior. Concise, action-oriented
- **Explanatory**: Adds educational "Insights" blocks alongside coding. Explains
  implementation choices and design tradeoffs
- **Learning**: Collaborative learn-by-doing mode. Shares insights and asks you to
  contribute code via `TODO(human)` markers

## Testing and Iteration

1. Activate: `/output-style My Style`
2. Test with typical tasks: Does the tone hold? Do tools work? Is the format consistent?
3. Iterate: Start with 5 lines, test, then add constraints as gaps appear

## References

- [writing-guide.md](references/writing-guide.md): Deep guide on writing effective
  output style instructions with good/bad examples
- [style-gallery.md](references/style-gallery.md): 12 curated output style templates
  organized by use case, each copy-ready
