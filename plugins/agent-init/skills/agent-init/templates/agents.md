# {Project Name}

> {One sentence: what this is and who it's for.}

This file documents only what breaks convention. If a senior developer would guess it correctly from the codebase, it
does not belong here. Do not add standard patterns, paraphrased docs, or facts discoverable from a single config file.
When editing, preserve this bar.

## Stack

{Non-standard elements only. Omit entirely if the stack is conventional. Example: "Multi-target (.NET 8/9/10) with
preview language features." Use file:line pointers to authoritative config.}

## Build & Test

{Build, test, and benchmark commands when they are non-obvious. Omit entirely if the toolchain is standard (e.g.
`npm test`, `dotnet test`). Document multi-step workflows, required setup (containers, env vars, fixtures), multiple
test suites, and non-standard runners. Include benchmark commands if regressions matter. Use file:line pointers to
build configs and CI definitions.}

## Protocols

{Each protocol or transport: REST, gRPC, GraphQL, WebSocket, message queue, etc. One bullet per protocol with its role
and entry point (file:line). Omit if the project uses one obvious protocol.}

## Structure

{Cross-module or multi-workspace notes. Include only if the project has non-obvious structure (monorepo layout, shared
libraries, unusual module boundaries). Omit for single-module projects. Point to the directories that define the
boundaries.}

## Security & Deployment

{Non-obvious security patterns, secrets management, or deployment workflows. Omit if standard. Reference auth entry
points, credential handling, and CI/CD config by file:line.}

## Project context

```text
.project/
├── .scratch/                    # throwaway files; never promote to permanent docs
└── docs/                        # all documentation; use typed subdirectories only
    ├── specs/                   # design documents (YYYY-MM-DD-<topic>-design.md)
    ├── plans/                   # implementation plans (YYYY-MM-DD-<feature-name>.md)
    └── research/                # research findings (YYYY-MM-DD-<topic>-research.md)

.agents/
├── context/                     # topic reference files; read on demand, not upfront
└── memory/                      # project-scoped auto-memory; committed and shared
```

All docs must be dated and named after their topic. Files in `docs/` go in a typed subdirectory; never in `docs/`
directly.

When a document spans categories, file it under its primary purpose and cross-reference from the other directory.

TRIPWIRE: If you find yourself writing a doc into the wrong subdirectory, move it immediately.

TRIPWIRE: If you find yourself writing a doc inline in conversation, stop and write it to `.project/docs/`.

## Reference

Read these before working on related areas. Do not read all upfront.

| Document                              | When to read                       |
|---------------------------------------|------------------------------------|
| [{Topic}](.agents/context/{topic}.md) | {When this topic becomes relevant} |
