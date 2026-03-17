# {Project Name} Agent Context

## Summary

{Brief observations about the project: interesting patterns found, architectural surprises, areas of note. 1-2
paragraphs. Mention main sources that shaped the output.}

## Findings

{Key non-standard patterns discovered by the team, organized by theme. Include security notes if relevant. Reference
file:line pointers.}

## What was created

### Agent infrastructure

| Path                         | What it does                                                   |
|------------------------------|----------------------------------------------------------------|
| `AGENTS.md`                  | Briefing with stack, protocols, structure, and reference table |
| `.agents/context/{topic}.md` | {Brief description of topic and why it warranted its own file} |
| `.agents/context/`           | Topic reference files loaded on demand                         |
| `.agents/memory/`            | Project-scoped auto-memory; committed and shared               |

### Project knowledge base

| Path                      | What it does                     |
|---------------------------|----------------------------------|
| `.project/.scratch/`      | Throwaway files and scratch work |
| `.project/docs/specs/`    | Design documents                 |
| `.project/docs/plans/`    | Implementation plans             |
| `.project/docs/research/` | Research findings and references |

## Symlinks

| Path        | Target      | Status                      |
|-------------|-------------|-----------------------------|
| `CLAUDE.md` | `AGENTS.md` | {created / already correct} |

## Next steps

{Any follow-up actions: optional symlinks to configure, additional documentation areas to consider, or notes for
ongoing maintenance.}
