# {Project Name} Upgrade Report

## Migration

Migrated from legacy `project-setup` format to `agent-init`.

### Files migrated

| Legacy path                    | New path                     | Status   |
|--------------------------------|------------------------------|----------|
| `.project/project.md`          | `AGENTS.md`                  | Migrated |
| `.project/project-{topic}.md`  | `.agents/context/{topic}.md` | Migrated |

### Symlinks repointed

| Path        | Old target             | New target  |
|-------------|------------------------|-------------|
| `CLAUDE.md` | `.project/project.md`  | `AGENTS.md` |

### Legacy files removed

{List of `.project/project*.md` files that were deleted after migration. Note any files left in `.project/` (docs,
.scratch, non-project-* files).}

## Review findings

{Key changes proposed by the discovery agents after scanning the migrated content. Include security notes if relevant.
Reference file:line pointers.}

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

## Next steps

{Any follow-up actions: memory files copied from global path, additional symlinks to configure, content areas flagged
for future review.}
