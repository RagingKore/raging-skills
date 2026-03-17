# CliWrap Plugin

Comprehensive guidance for using [CliWrap](https://github.com/Tyrrrz/CliWrap) to interact with command-line
interfaces in .NET. Covers all execution models, piping patterns, cancellation strategies, and common pitfalls
with detailed examples.

## Features

- **1 skill** (`cliwrap`) with 6 reference files covering the full CliWrap API
- Execution model decision tree (ExecuteAsync, Buffered, ListenAsync, Observe, Pipe-based)
- Complete piping patterns with `PipeSource`, `PipeTarget`, and the `|` operator
- Cancellation and timeout strategies including graceful vs forceful termination
- Configuration options: arguments, environment variables, credentials, validation
- Common pitfalls with explanations and correct alternatives
- Real-world compound examples combining multiple features

## Installation

```sh
claude plugin add --from petabridge/raging-skills --path plugins/cliwrap
```

## Prerequisites

- .NET SDK 8.0+
- `CliWrap` NuGet package (`dotnet add package CliWrap`)
- `System.Reactive` NuGet package (only for push-based event streams with `Observe`)

## Usage

The skill activates automatically when working with CliWrap or process execution in .NET. Trigger phrases
include:

- "run a CLI command"
- "execute a process"
- "pipe command output"
- "use CliWrap"
- "stream process output"
- "cancel a running process"

## Skill: `cliwrap`

Auto-loaded skill providing CliWrap best practices and patterns.

| Reference file                     | Content                                                     |
| ---------------------------------- | ----------------------------------------------------------- |
| `references/execution-models.md`   | All five execution models with complete code examples        |
| `references/piping.md`             | PipeSource, PipeTarget, pipe operator, command chaining      |
| `references/cancellation.md`       | Cancellation tokens, graceful vs forceful, timeouts          |
| `references/configuration.md`      | Arguments, env vars, credentials, working directory          |
| `references/pitfalls.md`           | Common mistakes with explanations and correct alternatives   |
| `references/examples.md`           | Real-world compound scenarios combining multiple features    |

## License

MIT
