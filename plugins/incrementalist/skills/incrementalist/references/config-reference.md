## Contents

- [Configuration settings](#configuration-settings)
- [Config examples](#config-examples)
- [CLI reference](#cli-reference)
- [Glob patterns](#glob-patterns)
- [Solution-wide changes](#solution-wide-changes)
- [Troubleshooting](#troubleshooting)
- [Additional resources](#additional-resources)

## Configuration settings

Incrementalist reads JSON config from `.incrementalist/incrementalist.json` by default. Override the path
with `-c` / `--config`.

### Schema

Add the `$schema` property for IDE IntelliSense, auto-completion, and validation:

```json
{
  "$schema": "https://raw.githubusercontent.com/petabridge/Incrementalist/dev/src/Incrementalist.Cmd/Config/incrementalist.schema.json"
}
```

### All settings

| Setting                  | Type     | Default | Description                                                  | CLI flag                |
|--------------------------|----------|---------|--------------------------------------------------------------|-------------------------|
| `gitBranch`              | string   |         | Branch to compare against (e.g., `main`, `dev`)              | `-b`, `--branch`        |
| `solutionFilePath`       | string   |         | Path to the `.sln` / `.slnx` file to analyze                 | `-s`, `--sln`           |
| `outputFile`             | string   |         | Write affected project paths to this file                    | `-f`, `--file`          |
| `workingDirectory`       | string   |         | Working directory for analysis                               | `-d`, `--dir`           |
| `verbose`                | boolean  | `false` | Enable debug logging                                         | `--verbose`             |
| `timeoutMinutes`         | number   | `2`     | Solution loading timeout in minutes                          | `-t`, `--timeout`       |
| `continueOnError`        | boolean  | `true`  | Continue executing when a project command fails              | `--continue-on-error`   |
| `runInParallel`          | boolean  | `false` | Execute commands concurrently across projects                | `--parallel`            |
| `parallelLimit`          | number   | `0`     | Max concurrent projects (0 = unlimited)                      | `--parallel-limit`      |
| `failOnNoProjects`       | boolean  | `false` | Exit with error if no projects are affected                  | `--fail-on-no-projects` |
| `skip`                   | string[] | `[]`    | Glob patterns excluding projects from the final list         | `--skip-glob`           |
| `target`                 | string[] | `[]`    | Glob patterns; only matching projects are included           | `--target-glob`         |
| `nameApplicationToStart` | string   |         | Application to start (used with `run-process`)               | N/A                     |

CLI arguments override config file values.

## Config examples

### Tests only (unit)

```json
{
  "$schema": "https://raw.githubusercontent.com/petabridge/Incrementalist/dev/src/Incrementalist.Cmd/Config/incrementalist.schema.json",
  "gitBranch": "main",
  "verbose": false,
  "timeoutMinutes": 10,
  "continueOnError": true,
  "target": ["**/*.Tests.csproj"],
  "skip": ["**/*.Integration.Tests.csproj", "**/*.Benchmarks.csproj", "examples/**/*.csproj"]
}
```

### Integration tests only

```json
{
  "$schema": "https://raw.githubusercontent.com/petabridge/Incrementalist/dev/src/Incrementalist.Cmd/Config/incrementalist.schema.json",
  "gitBranch": "main",
  "continueOnError": true,
  "target": ["**/*.Integration.Tests.csproj"],
  "skip": ["examples/**/*.csproj"]
}
```

### Build all (skip examples and benchmarks)

```json
{
  "$schema": "https://raw.githubusercontent.com/petabridge/Incrementalist/dev/src/Incrementalist.Cmd/Config/incrementalist.schema.json",
  "gitBranch": "main",
  "continueOnError": false,
  "skip": ["examples/**/*.csproj", "**/*.Benchmarks.csproj"]
}
```

### Multiple configs per CI job

```
.incrementalist/
  incrementalist.json      # default: all projects (skip examples/benchmarks)
  testsOnly.json           # unit tests
  integrationOnly.json     # integration tests
  benchmarksOnly.json      # perf tests
```

Each job passes its config via `--config .incrementalist/<name>.json`.

## CLI reference

### Verbs

| Verb                    | Purpose                                             |
|-------------------------|-----------------------------------------------------|
| (default, no verb)      | List affected projects and write to file             |
| `run`                   | Execute a dotnet command against affected projects   |
| `list-affected-folders` | List affected folders instead of project files       |
| `create-config`         | Generate a config file from current CLI flags        |
| `run-process`           | Alias for `run` (currently dotnet only)              |

### Run verb syntax

```sh
dotnet incrementalist run [options] -- [dotnet command and arguments]
```

Everything after `--` is passed to `dotnet` for each affected project.

### Common flags

| Flag                          | Description                                     |
|-------------------------------|-------------------------------------------------|
| `-s`, `--sln <path>`         | Solution file path                              |
| `-b`, `--branch <name>`      | Target branch to diff against                   |
| `-f`, `--file <path>`        | Output affected projects to file                |
| `-c`, `--config <path>`      | Config file path                                |
| `-d`, `--dir <path>`         | Working directory                               |
| `-t`, `--timeout <minutes>`  | Solution load timeout                           |
| `--verbose`                   | Debug logging                                   |
| `--dry`                       | Preview commands without executing              |
| `--parallel`                  | Run concurrently                                |
| `--parallel-limit <n>`       | Cap concurrency                                 |
| `--continue-on-error`         | Continue past failures                          |
| `--fail-on-no-projects`       | Fail if nothing affected                        |
| `--target-glob "<pattern>"`  | Include only matching projects                  |
| `--skip-glob "<pattern>"`    | Exclude matching projects                       |

## Glob patterns

The `target` and `skip` arrays use glob syntax to filter the final project list after dependency analysis.

### Syntax

| Pattern | Matches                                            |
|---------|----------------------------------------------------|
| `*`     | Any characters within a single path segment        |
| `**`    | Any number of path segments (recursive)            |
| `?`     | Any single character                               |

### Skip patterns and .gitignore

Do not add skip patterns for paths already covered by `.gitignore` (e.g., `bin/`, `obj/`, `.worktrees/`).
Incrementalist analyzes git diffs, so files excluded by `.gitignore` never appear in the diff and cannot
affect the analysis. Adding redundant skip patterns adds noise.

Only use `skip` for paths tracked in git but that should be excluded from CI builds: example projects,
documentation-only projects, benchmark projects, or platform-specific projects.

### Evaluation order

1. Dependency analysis produces the full affected project list
2. If `target` is non-empty, only projects matching at least one target pattern survive
3. Projects matching any `skip` pattern are removed

Both filters are applied after dependency resolution. A project excluded by `skip` does not cause its
dependents to be excluded; the filters operate on the flat list.

## Solution-wide changes

Certain files affect every project. When Incrementalist detects changes to these files, it triggers a full
build regardless of which `.csproj` files were modified.

Solution-wide files:

- `Directory.Build.props` and `Directory.Build.targets`
- `Directory.Packages.props` (central package management)
- `global.json`
- `.sln` / `.slnx` solution files
- `nuget.config`

This is intentional. A change to `Directory.Build.props` can affect compilation of every project, so the
safe default is to rebuild everything.

## Troubleshooting

### "No affected projects found" in CI

- Verify `fetch-depth: 0` in the checkout step. Shallow clones prevent branch comparison.
- Confirm `gitBranch` matches the actual target branch name (e.g., `main` not `master`).
- Run with `--verbose` and `--dry` to see what Incrementalist detects.

### Timeout loading large solutions

Increase `timeoutMinutes` in the config. The default is 2 minutes; large solutions with many projects may
need 10-20 minutes for the initial Roslyn analysis.

### All projects build despite small change

Check if the changed file is solution-wide (see [Solution-wide changes](#solution-wide-changes)). Changes
to `Directory.Build.props`, `global.json`, or `.sln` files intentionally trigger a full build.

### Source generators and multi-TFM builds

Source generator projects target `netstandard2.0`, not the same TFMs as the consuming projects. If the CI
workflow uses a matrix strategy on TFM, the build step must use the solution's full TFM list (not just the
matrix value) so source generator projects compile successfully. Alternatively, build once with all TFMs
and pass `--no-build` to the test step.

## Additional resources

- [Incrementalist GitHub repository](https://github.com/petabridge/Incrementalist)
- [Configuration schema](https://github.com/petabridge/Incrementalist/blob/dev/src/Incrementalist.Cmd/Config/incrementalist.schema.json)
- [Real-world CI examples](https://github.com/petabridge/Incrementalist/blob/dev/docs/examples.md)
  (Akka.NET, Akka.Management, Azure DevOps)
