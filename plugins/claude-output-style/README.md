## claude-output-style

Create and review custom output styles for Claude Code.

## What it does

Output styles are markdown files that control how Claude Code formats its responses.
They can enhance coding behavior (security reviewer, TDD coach) or replace it entirely
to turn Claude Code into a non-coding agent (business analyst, research assistant).

This plugin teaches Claude how to write effective output styles and includes a reviewer
agent that catches common mistakes before they cause unexpected behavior.

## Components

### Skill: claude-output-style (auto-loaded)

Activates when creating, editing, or reviewing output styles. Covers:

- File format (YAML frontmatter, body conventions)
- The `keep-coding-instructions` decision (the most common source of confusion)
- Writing effective instructions (specific behavior, imperative mood, boundaries)
- How output styles interact with CLAUDE.md, skills, agents, and hooks
- Anti-patterns and testing workflow

### Agent: output-style-reviewer

Reviews output style files for quality. Checks:

- Frontmatter correctness (name, description, keep-coding-instructions)
- Instruction clarity and specificity
- Potential conflicts with Claude Code's tool system
- Common mistakes (forgetting keep-coding-instructions, overly vague instructions)

Returns a structured pass/fail report with concrete fixes.

## Reference guides

- [writing-guide.md](skills/claude-output-style/references/writing-guide.md):
  Deep guide on writing effective instructions with good/bad examples, decision
  trees, and anti-patterns
- [style-gallery.md](skills/claude-output-style/references/style-gallery.md):
  12 curated output style templates organized by use case (coding enhancement,
  non-coding personas, creative styles, specialized workflows)

## Installation

```sh
claude --plugin-dir /path/to/claude-output-style
```

Or add to your project's `.claude/plugins` configuration.

## Usage

The skill loads automatically when you ask Claude to create or edit output styles:

- "Create an output style for security-focused code review"
- "Make a custom output style that responds in bullet points only"
- "Turn Claude Code into a research assistant"
- "Review my output style for issues"

The reviewer agent triggers when you ask to review an output style or can be
invoked after creating one to verify quality.

## Quick start

Create a file at `~/.claude/output-styles/my-style.md`:

```markdown
---
name: My Style
description: Brief description for the picker
keep-coding-instructions: true
---
Your instructions here...
```

Activate it: `/output-style My Style`
