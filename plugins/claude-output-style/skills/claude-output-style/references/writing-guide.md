## Writing Guide for Claude Code Output Styles

Output styles control how Claude Code communicates. They shape tone, structure, verbosity, and
persona across an entire session. This guide covers the file format, the critical
`keep-coding-instructions` decision, writing techniques, interactions with other features, common
anti-patterns, and how to iterate toward a polished style.

## Contents

- [File Format](#file-format)
- [Storage Locations](#storage-locations)
- [Built-in Styles](#built-in-styles)
- [The keep-coding-instructions Decision](#the-keep-coding-instructions-decision)
- [Writing Effective Instructions](#writing-effective-instructions)
- [How Output Styles Interact with Other Features](#how-output-styles-interact-with-other-features)
- [Anti-patterns](#anti-patterns)
- [Testing and Iteration](#testing-and-iteration)
- [Complete Examples](#complete-examples)

## File Format

An output style is a markdown file with YAML frontmatter. The frontmatter carries metadata; the
body carries the behavioral instructions Claude will follow.

```markdown
---
name: Style Name
description: Shown in the /output-style picker
keep-coding-instructions: true
---
Body instructions here...
```

| Field                      | Required | Default | Purpose                                         |
|----------------------------|----------|---------|-------------------------------------------------|
| `name`                     | yes      |         | Display name in the style picker                |
| `description`              | yes      |         | One-line summary shown next to the name         |
| `keep-coding-instructions` | no       | `false` | Whether Claude retains its SE persona and rules |

## Storage Locations

- Global styles: `~/.claude/output-styles/`
- Project styles: `.claude/output-styles/`

Project styles override global styles when the names collide.

## Built-in Styles

Claude Code ships with three built-in styles:

- **Default**: Standard coding assistant behavior. Concise responses, no extra explanation unless
  asked.
- **Explanatory**: Adds reasoning and context to every response. Useful when learning a new
  codebase.
- **Learning**: Teaches as it codes. Explains concepts, tradeoffs, and alternatives. Aimed at
  developers building new skills.

## The keep-coding-instructions Decision

This is the single most important decision in any output style. It controls whether Claude retains
its software engineering personality or adopts an entirely new persona.

### Decision tree

```
Do you want Claude to write, review, or reason about code?
├── YES → keep-coding-instructions: true
│         Claude keeps all SE behavior. Your style adds tone, format, or focus on top.
│         Examples: security reviewer, TDD coach, verbose explainer, documentation writer
│
└── NO  → keep-coding-instructions: false (the default)
          Claude replaces the SE personality with whatever you define.
          You become responsible for defining how Claude should behave.
          Examples: business analyst, research assistant, content strategist, interview coach
```

### What `true` preserves

- Code quality heuristics (error handling, edge cases, naming conventions)
- Tool usage patterns (reading before editing, targeted diffs over full rewrites)
- Engineering judgment (when to refactor, when to leave things alone)
- Standard communication style for code-related output

### What `false` removes

- The software engineering persona and all associated behavioral rules
- Default preferences around code structure and style

Note: even with `false`, Claude retains full tool access. It can still read files, write files, run
bash commands, and search the web. The style only changes the personality, not the capabilities.

### Common mistake

Forgetting that the default is `false`. If you write a style that says "Always explain your
refactoring decisions" but omit `keep-coding-instructions`, Claude loses its SE behavior entirely.
The style becomes a general-purpose assistant that happens to explain things. Always set this field
explicitly.

## Writing Effective Instructions

### Be specific about behavior

Describe observable actions, not personality traits.

```markdown
# Bad: personality trait
Be test-driven and thoughtful.

# Good: observable behavior
Always show test code before implementation.
When a function has more than three parameters, suggest a parameter object.
```

### Use imperative mood

Direct commands are clearer than suggestions.

```markdown
# Bad: suggestion
You should explain each decision you make.

# Good: command
Explain each decision. State what you chose, what you rejected, and why.
```

### Define output format when you want structure

If you expect a particular shape, spell it out.

```markdown
# Bad: vague
Give me structured feedback.

# Good: explicit
Format every code review as:
## Summary
One paragraph overview.
## Issues
Numbered list. Each item: severity (critical/warning/info), file path, description.
## Suggestions
Bulleted list of optional improvements.
```

### Set boundaries on inclusion and exclusion

Tell Claude what to skip, not just what to include.

```markdown
# Bad: unbounded
Explain everything.

# Good: bounded
Explain architectural decisions and non-obvious tradeoffs.
Skip explanations for standard library usage, simple variable assignments,
and boilerplate code.
```

### Address interaction patterns

Define how Claude should handle ambiguity and multi-step work.

```markdown
# Examples of interaction directives
Ask clarifying questions before starting any task that involves more than one file.
Proceed autonomously on single-file changes; ask for confirmation on cross-file refactors.
Never ask "would you like me to continue?" Just continue.
When you encounter an error, fix it and explain what happened. Do not ask for permission to retry.
```

### Keep instructions concise

Output styles work best when short and direct. Claude follows a few clear rules better than a wall
of text. If your style exceeds 40-50 lines of body instructions, look for redundancy or
over-specification.

## How Output Styles Interact with Other Features

### CLAUDE.md

CLAUDE.md is injected as a user message after the system prompt. It supplements the output style
rather than conflicting with it. If your output style says "respond in bullet points" and CLAUDE.md
says "keep lines under 120 characters", both apply. If they directly contradict, CLAUDE.md generally
wins because it appears later in the context.

### Skills

Skills are task-specific instructions invoked on demand (e.g., `/markdown-style`). Output styles are
always active for the entire session. They operate at different scopes: the output style sets the
baseline tone and format; skills add domain knowledge for a particular task.

### Agents and subagents

Output styles apply to the main conversation only. Subagents spawned via the Task tool have their
own context and are not affected by the parent session's output style.

### --append-system-prompt

The `--append-system-prompt` flag appends text to the system prompt alongside the output style. Both
are active simultaneously. Use `--append-system-prompt` for one-off instructions that do not warrant
a full style file.

### Hooks

Hooks (pre/post-tool execution scripts) run independently of output styles. An output style cannot
enable or disable hooks, and hooks cannot modify the active output style.

## Anti-patterns

### Too vague

```markdown
---
name: Helpful
description: A helpful assistant
---
Be helpful and clear.
```

This adds nothing. Claude is already helpful and clear by default. Every instruction should change
behavior in a measurable way.

### Too restrictive

```markdown
---
name: Terse
description: Ultra-short responses
---
Never use more than 10 words per response.
Never use code blocks.
Never show file paths.
```

This breaks tool usage. Claude needs code blocks to show diffs and file paths to communicate what it
changed. Restrict length and format loosely enough that tool output remains functional.

### Conflicting with tool capabilities

```markdown
---
name: Read-Only Analyst
description: Analysis without changes
---
Never write files. Never run commands. Only analyze.
```

If you actually want read-only behavior, use the `--allowedTools` flag to restrict tool access at
the CLI level. An output style instruction to "never write files" is a soft suggestion that Claude
may override when it judges a write is necessary.

### Missing format specification

```markdown
---
name: Code Reviewer
description: Reviews pull requests
keep-coding-instructions: true
---
Review code thoroughly.
```

"Thoroughly" is subjective. Define what thorough means: line-by-line comments? A summary table?
Severity ratings? Without format specification, output varies unpredictably between sessions.

### Forgetting the default for keep-coding-instructions

```markdown
---
name: Verbose Coder
description: Explains code decisions in detail
---
Explain every code decision in detail before implementing.
Show alternatives you considered.
```

Because `keep-coding-instructions` defaults to `false`, this style accidentally strips all SE
behavior. Claude becomes a general explainer rather than a coding assistant that explains. The fix:

```markdown
---
name: Verbose Coder
description: Explains code decisions in detail
keep-coding-instructions: true
---
Explain every code decision in detail before implementing.
Show alternatives you considered.
```

### Overly long instructions

```markdown
---
name: Everything Style
description: Covers every possible scenario
keep-coding-instructions: true
---
When writing code, always consider performance implications...
[...80 more lines of increasingly specific rules...]
```

Long styles dilute focus. Claude follows a handful of clear directives more reliably than dozens of
nuanced rules. If your style needs that much specification, consider splitting concerns: put coding
rules in CLAUDE.md, domain knowledge in a skill, and only tone/format directives in the output
style.

## Testing and Iteration

### How to test

1. Activate the style: `/output-style My Style`
2. Run your typical tasks: write code, ask questions, trigger tool usage
3. Try edge cases: error scenarios, multi-file changes, ambiguous requests

### What to check

- **Tone consistency**: Does Claude maintain the persona across different types of requests?
- **Tool compatibility**: Can Claude still read, write, search, and run commands effectively?
- **Format adherence**: Does the output match the structure you specified?
- **Edge cases**: What happens when Claude encounters an error? A large file? A request outside the
  style's focus area?

### Iteration approach

Start minimal. A good first draft has 5-10 lines of body instructions. Use the style for a few
sessions, then add constraints only where you notice gaps. Resist the urge to pre-specify
everything.

```
Draft 1:  5 lines  → covers tone and format basics
Draft 2: 10 lines  → adds boundaries discovered during testing
Draft 3: 15 lines  → refines interaction patterns
```

Stop when adding a new line does not change observable behavior.

## Complete Examples

### Coding style: Security Reviewer

```markdown
---
name: Security Reviewer
description: Reviews code with a security-first lens
keep-coding-instructions: true
---
Review every code change for security implications before discussing functionality.

Flag these categories explicitly:
- Input validation gaps
- Authentication and authorization issues
- Data exposure risks
- Injection vectors (SQL, command, path traversal)
- Hardcoded secrets or credentials

Format each finding as: [SEVERITY] file:line - description.

When writing new code, apply the principle of least privilege by default.
If a security concern is theoretical but low-risk, mention it as an aside
rather than blocking the task.
```

### Coding style: TDD Coach

```markdown
---
name: TDD Coach
description: Guides test-first development workflow
keep-coding-instructions: true
---
Always write tests before implementation.

Workflow for every code task:
1. Write a failing test that captures the requirement.
2. Show the test. Wait for confirmation before proceeding.
3. Write the minimal implementation to pass the test.
4. Suggest refactoring opportunities.

When the user asks for a feature, respond with a test first. Do not show
implementation code until the test is written and reviewed.

If the user provides implementation code without tests, write tests for it
before making any changes.
```

### Non-coding style: Research Assistant

```markdown
---
name: Research Assistant
description: Structured research and analysis
keep-coding-instructions: false
---
Respond to every question with structured research output.

Format:
## Key Findings
Numbered list of 3-5 findings, most important first.

## Evidence
For each finding, cite the source (URL, file path, or reasoning chain).

## Confidence
Rate your confidence: high, medium, or low. Explain what would increase it.

## Open Questions
List 1-3 follow-up questions that would deepen understanding.

When using web search, prefer primary sources over aggregators.
When reading files, quote relevant passages directly rather than paraphrasing.
Ask one clarifying question before starting research if the topic is ambiguous.
```

### Non-coding style: Business Analyst

```markdown
---
name: Business Analyst
description: Translates technical work into business language
keep-coding-instructions: false
---
Translate all technical concepts into business language.

Never use jargon without defining it. Assume the reader is a non-technical
stakeholder who cares about cost, timeline, risk, and user impact.

Format recommendations as:
- What: one sentence describing the change
- Why: business value or risk mitigated
- Cost: rough effort estimate (hours or days)
- Risk: what could go wrong

When reading code or technical documents, summarize them in terms of
user-facing behavior and business process impact. Skip implementation
details unless the user explicitly asks for them.
```
