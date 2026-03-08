---
name: incrementalist
description: |
  Integrate Incrementalist into .NET monorepos for git-based incremental builds and testing in GitHub
  Actions CI/CD pipelines. Use this skill whenever:
  - Working on CI/CD pipelines for .NET monorepos or multi-project solutions
  - Optimizing build times, reducing CI minutes, or speeding up PR validation
  - Setting up incremental or selective testing (build/test only what changed)
  - Writing or editing GitHub Actions workflows that build or test .NET projects
  - Encountering an incrementalist.json config file, or any JSON file with the schema URL
    "https://raw.githubusercontent.com/petabridge/Incrementalist/dev/src/Incrementalist.Cmd/Config/incrementalist.schema.json"
  - Discussing monorepo build strategies, project dependency analysis, or affected-project detection
  - Seeing references to Incrementalist, Incrementalist.Cmd, `dotnet incrementalist`, or `.incrementalist/`
  - Configuring which projects to build or skip in CI based on git changes
  Even if the user does not mention Incrementalist by name, use this skill when the task involves making a
  .NET monorepo CI pipeline smarter about what it builds or tests. Covers tool installation, configuration
  files, GitHub Actions workflow authoring, glob patterns, and solution-wide change detection.
---

## Contents

- [What Incrementalist does](#what-incrementalist-does)
- [Setup workflow](#setup-workflow)
  - [Phase 1: Installation](#phase-1-installation)
  - [Phase 2: Repository analysis](#phase-2-repository-analysis)
  - [Phase 3: Configuration](#phase-3-configuration)
  - [Phase 4: Workflow authoring](#phase-4-workflow-authoring)
  - [Phase 5: Verification](#phase-5-verification)
  - [Phase 6: Deliverables](#phase-6-deliverables)
- [Editing existing configs](#editing-existing-configs)
- [Key awareness](#key-awareness)
- [Reference material](#reference-material)

## What Incrementalist does

Incrementalist analyzes git diffs between the current branch and a target branch, maps changed files to
affected `.csproj` projects through Roslyn-based dependency analysis, and executes dotnet commands against
only those projects. This turns O(all-projects) CI builds into O(changed-projects) builds.

The analysis flow:

1. Diff the current branch against the target branch (e.g., `main`)
2. Check whether any changed files are solution-wide (triggers full build)
3. Map remaining changed files to their owning `.csproj` projects
4. Walk the project dependency graph to include downstream dependents
5. Apply `target` and `skip` glob filters
6. Execute the specified dotnet command against each surviving project

## Setup workflow

Follow these phases in order when integrating Incrementalist into a repository. Each phase has mandatory
outputs. Do not skip phases or reorder them. The phases contain decision points where the user must be
consulted before proceeding.

### Phase 1: Installation

Install Incrementalist as a dotnet local tool:

```sh
dotnet new tool-manifest    # if .config/dotnet-tools.json does not exist
dotnet tool install Incrementalist.Cmd
```

This adds an entry to `.config/dotnet-tools.json`. Commit it so CI can run `dotnet tool restore`.

#### `dnx` shorthand (.NET 10+)

On .NET 10 and later, the SDK ships a `dnx` script that wraps `dotnet tool exec`. If the repo targets
.NET 10+, you can invoke `dnx incrementalist run --dry` instead of `dotnet incrementalist run --dry`. Both
are equivalent; `dnx` is shorter. Note this requires .NET 10 SDK installed on the runner.

### Phase 2: Repository analysis

Before writing any configuration or workflow code, analyze the repository to understand its structure. This
phase produces the foundation for everything that follows.

#### Step 2.1: Build the dependency graph

Scan all `.csproj` files for `<ProjectReference>` items. Produce a dependency graph document showing:

- Every source project with its target frameworks and dependencies
- Every test project with its dependencies
- An impact matrix: when project X changes, which test projects must run
- Which projects are high-fan-out (changes cascade widely) vs. leaf nodes (isolated)

Present the dependency graph to the user as a numbered finding. This graph is a mandatory deliverable.

#### Step 2.2: Detect external file references

Scan `.csproj` files for items that reference files outside the project directory tree:

- `<Protobuf Include="../../proto/**/*.proto" />` or similar proto references
- `<Compile Include="../../shared/..." />` or `<Link>` items
- `<None Include="..." CopyToOutputDirectory="..." />` pointing outside the project

These files live outside all project directories. When only they change, Incrementalist cannot detect
affected projects because its file-to-project mapping is directory-based.

Report each external reference as a numbered finding with the source project and external path.

#### Step 2.3: Detect source generators and multi-TFM requirements

Check for source generator projects. These typically target `netstandard2.0` and are referenced as
analyzers via `<ProjectReference ... OutputItemType="Analyzer" ReferenceOutputAssembly="false" />`.

Source generators have an important implication for CI: a workflow matrix that tests across TFMs (e.g.,
`net8.0`, `net9.0`, `net10.0`) must still build all TFMs first (including `netstandard2.0` for the
generators). Do not assume a single TFM build is sufficient. Build once with the full solution, then run
tests per-TFM with `--no-build`.

#### Step 2.4: Identify test runner framework

Check the test projects' NuGet references and determine which test framework they use:

- **xUnit / NUnit / MSTest**: Standard. Use `dotnet test`.
- **TUnit**: Requires `dotnet run`, not `dotnet test`. Use Incrementalist in list-only mode.
- **Expecto**: Requires `dotnet run`. Same approach as TUnit.

Research the specific test framework CLI before finalizing the workflow. Do not assume `dotnet test` works
for all runners.

#### Step 2.5: Identify example, benchmark, and non-test projects

Categorize projects that should not be tested but may still need to build:

- **Examples / samples / cookbooks**: Must build to verify they compile, but have no tests.
- **Benchmarks**: Typically run separately, not in PR CI. May have their own CI job.

Use `AskUserQuestion` to confirm with the user:

> "I found these project categories. How should CI handle each?"
>
> - Examples (N projects): [Build-only in CI / Skip entirely / Other]
> - Benchmarks (N projects): [Separate CI job / Skip in PR CI / Other]
> - Other: [describe]

The user's answer determines the `skip` patterns in each config.

#### Step 2.6: Estimate time savings

Based on the dependency graph, estimate the expected CI time savings for common PR scenarios:

- PR touching a leaf project (e.g., single test fix): estimate % reduction
- PR touching a mid-graph project: estimate % reduction
- PR touching a high-fan-out project or solution-wide file: full build (no savings)

Present the estimates as a numbered finding. This is a mandatory deliverable.

### Phase 3: Configuration

Create Incrementalist config files based on the Phase 2 analysis.

#### Default config

Every setup needs at minimum `.incrementalist/incrementalist.json`:

```json
{
  "$schema": "https://raw.githubusercontent.com/petabridge/Incrementalist/dev/src/Incrementalist.Cmd/Config/incrementalist.schema.json",
  "gitBranch": "main",
  "verbose": false,
  "timeoutMinutes": 10,
  "continueOnError": false,
  "skip": ["examples/**/*.csproj", "**/*.Benchmarks.csproj"]
}
```

Always include `skip` patterns for examples and benchmarks (or whatever the user decided in Step 2.5).
Never leave configs without addressing these categories.

#### Per-job configs

Create separate configs for each CI job that needs different project filtering:

| Config                        | `target`                            | `skip`                                                                    |
|-------------------------------|-------------------------------------|---------------------------------------------------------------------------|
| `incrementalist.json`         | (empty = all)                       | `examples/**`, `**/*.Benchmarks.csproj`                                   |
| `testsOnly.json`              | `**/*.Tests.csproj`                 | `**/*.Integration.Tests.csproj`, `**/*.Benchmarks.csproj`, `examples/**`  |
| `integrationOnly.json`        | `**/*.Integration.Tests.csproj`     | `examples/**`                                                             |

#### Decision point: solution filter

If the repo contains many projects that CI should never touch, use `AskUserQuestion`:

> "The repo has N example/benchmark projects. Should I create a `.slnf` solution filter that excludes them
> from CI builds entirely? This would mean Incrementalist only analyzes the filtered solution."

If yes, create a `.slnf` and reference it via the `solutionFilePath` config field.

For the full settings table and more config examples, read `references/config-reference.md`.

### Phase 4: Workflow authoring

#### Core workflow structure

The GitHub Actions workflow must include:

1. **Checkout with full history**: `fetch-depth: 0` (Incrementalist needs full git history for diffing)
2. **NuGet package caching**: `actions/cache@v4` keyed on `*.csproj` and `Directory.Packages.props`
3. **Tool restore**: `dotnet tool restore`
4. **External file detection**: `dorny/paths-filter@v3` or git-diff check for files outside project trees
5. **Scope determination**: Decide whether this run is full or incremental (single step)
6. **Dry-run preview**: `dotnet incrementalist run --dry --verbose` (incremental runs only, for CI debugging)
7. **Build**: Always full (`dotnet build -c Release` on the whole solution)
8. **Test detection + execution**: Using bundled scripts (see below)

#### External file detection with `dorny/paths-filter`

When Phase 1 found external file references (proto files, shared schemas), pair Incrementalist with
`dorny/paths-filter@v3` to detect changes in directories that Incrementalist cannot map:

```yaml
      - uses: dorny/paths-filter@v3
        id: changes
        with:
          filters: |
            external:
              - 'proto/**'
              - 'shared/**'
            infra:
              - 'Directory.Build.props'
              - 'Directory.Packages.props'
              - 'global.json'
              - '*.slnx'
```

When `steps.changes.outputs.external == 'true'` or `steps.changes.outputs.infra == 'true'`, skip
Incrementalist and run a full build. This covers the edge case where only external files change and
Incrementalist would report zero affected projects.

#### Bundled scripts for test detection and execution

The inline bash for detecting and running affected tests is complex. This skill bundles two template
scripts at `scripts/detect-affected.sh` and `scripts/run-affected-tests.sh`. Read them, adapt for the
repo's specifics (test runner, TFMs, project paths), and write them to the repo at a location like
`.incrementalist/scripts/` or `scripts/ci/`. Then reference them from the workflow:

```yaml
      - name: Detect affected unit tests
        run: ./scripts/ci/detect-affected.sh .incrementalist/testsOnly.json affected-unit-tests.txt

      - name: Run affected unit tests
        run: >-
          ./scripts/ci/run-affected-tests.sh affected-unit-tests.txt
          --runner run --frameworks net8.0,net9.0,net10.0 --no-build
          --extra-args "--no-progress --treenode-filter '/*/*/*/*[Category!=Integration]'"
```

This keeps the workflow YAML concise and the test logic reusable.

#### Unified pipeline design

The goal is a single pipeline that works for both full and incremental runs. Do not duplicate steps with mirrored
`if:` conditions for full vs. incremental. Instead, determine the scope once and let the rest of the pipeline use it.

The build step is always full (`dotnet build` on the whole solution). This is necessary because source generators
need all TFMs compiled, NuGet pack needs the full output, and full builds are the simplest correctness guarantee.
Incrementalist narrows only the **test execution** scope; it does not narrow the build.

On a PR where only leaf projects changed, the build still runs fast (most projects are up-to-date and `dotnet build`
skips them). The real savings come from running fewer test projects.

#### Decision point: single file vs. separate workflow files

Use `AskUserQuestion` to present this choice:

> "For the CI pipeline, which structure do you prefer?"
>
> A. **Single workflow file** with a scope-determination step (full on main push or force-full; incremental on PR).
>    Simplest to maintain; one file, one set of steps.
> B. **Separate workflow files** (`ci-main.yml` and `ci-pr.yml`). Each file is simpler on its own; no conditional
>    logic. Better when main and PR pipelines diverge significantly (e.g., main publishes packages, PR does not).

Proceed with the user's choice. Both are valid. The templates below show option A; for option B, split the template
into two files with no `if:` conditions.

#### Workflow template (single unified file)

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: |
            8.0.x
            9.0.x
            10.0.x

      - name: Cache NuGet packages
        uses: actions/cache@v4
        with:
          path: ~/.nuget/packages
          key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj', '**/Directory.Packages.props') }}
          restore-keys: ${{ runner.os }}-nuget-

      - name: Restore tools
        run: dotnet tool restore

      - uses: dorny/paths-filter@v3
        id: changes
        if: github.event_name == 'pull_request'
        with:
          filters: |
            external:
              - 'proto/**'

      # Scope determination: one step, referenced by all downstream steps
      - name: Determine build scope
        id: scope
        run: |
          if [[ "${{ github.event_name }}" == "push" ]] || \
             [[ "${{ steps.changes.outputs.external }}" == "true" ]]; then
            echo "mode=full" >> "$GITHUB_OUTPUT"
          else
            echo "mode=incremental" >> "$GITHUB_OUTPUT"
          fi
          echo "Build scope: $(cat "$GITHUB_OUTPUT")"

      - name: Preview affected projects (dry run)
        if: steps.scope.outputs.mode == 'incremental'
        run: dotnet incrementalist run --dry --verbose -- build -c Release

      # Always build the full solution (source generators, NuGet pack, correctness)
      - name: Build
        run: dotnet restore && dotnet build -c Release --no-restore

      # Detect affected test projects (incremental only; full runs all)
      - name: Detect affected unit tests
        if: steps.scope.outputs.mode == 'incremental'
        run: ./scripts/ci/detect-affected.sh .incrementalist/testsOnly.json affected-unit-tests.txt

      # Single test step: reads the affected list if it exists, otherwise runs all
      - name: Run unit tests
        run: >-
          ./scripts/ci/run-affected-tests.sh affected-unit-tests.txt
          --runner test --frameworks net8.0,net9.0,net10.0 --no-build

      - name: Detect affected integration tests
        if: steps.scope.outputs.mode == 'incremental'
        run: ./scripts/ci/detect-affected.sh .incrementalist/integrationOnly.json affected-integration-tests.txt

      - name: Run integration tests
        run: >-
          ./scripts/ci/run-affected-tests.sh affected-integration-tests.txt
          --runner test --frameworks net10.0 --no-build
```

The `run-affected-tests.sh` script handles both cases: if the affected-projects file exists and is non-empty, it
runs only those projects. If the file does not exist (full mode, detection step was skipped), it discovers all
matching test projects and runs them. This eliminates duplicated steps.

Adapt the template for the repo's test runner, TFMs, and project structure.

### Phase 5: Verification

This is a mandatory checklist. Execute each step and verify the output before proceeding to the next.
Do not skip steps. Do not present these as suggestions; run them.

- [ ] **5.1 Dry run locally**: Run `dotnet incrementalist run --dry --verbose -- build -c Release` in the
  repo root. Inspect the output. Confirm the correct projects are listed. If unexpected projects appear or
  expected projects are missing, revisit the configs.

- [ ] **5.2 Validate skip patterns**: Run the dry run with each per-job config (`testsOnly.json`,
  `integrationOnly.json`). Confirm examples and benchmarks are excluded. Confirm the target patterns match
  only the intended projects.

- [ ] **5.3 Test external file detection**: If the repo has external files (proto, shared), verify the
  `dorny/paths-filter` or git-diff step would detect a proto-only change and force a full build. Check by
  inspecting the workflow conditions.

- [ ] **5.4 Verify test runner compatibility**: If the repo uses a non-standard test runner (TUnit,
  Expecto), run the detect-and-execute scripts locally against one test project. Confirm tests actually
  execute and produce results.

- [ ] **5.5 Review multi-TFM build**: If the repo multi-targets, confirm the build step compiles all TFMs
  (including `netstandard2.0` for source generators). A per-TFM matrix build will fail if source generators
  cannot compile against the matrix TFM.

Once all checks pass, present the results to the user with the dry-run output.

### Phase 6: Deliverables

Every Incrementalist setup must produce these deliverables. Present them as a numbered summary to the user.

1. **Dependency graph** — The project dependency graph with impact matrix (from Step 2.1)
2. **Numbered findings** — External file references, source generator TFM requirements, test runner
   framework, example/benchmark categorization
3. **Time savings estimate** — Per-scenario estimates (from Step 2.6)
4. **Configuration files** — All `.incrementalist/*.json` configs
5. **CI scripts** — `detect-affected.sh` and `run-affected-tests.sh` (adapted from templates)
6. **Workflow YAML** — The GitHub Actions workflow file
7. **Tool manifest** — `.config/dotnet-tools.json` with Incrementalist registered
8. **Verification results** — Dry-run output confirming correct project detection

## Editing existing configs

When the user has an existing `incrementalist.json` and asks for modifications:

1. Read the existing file and the Incrementalist schema (the `$schema` URL provides IDE validation)
2. Apply changes using the exact field names from the schema. The parallel execution fields are
   `runInParallel` (boolean) and `parallelLimit` (number); do not invent nested objects or alternative
   names
3. Preserve all existing fields unchanged unless the user explicitly asks to modify them
4. Verify the updated config is valid against the schema

For the full settings reference, read `references/config-reference.md`.

## Key awareness

### Source generators and multi-TFM

Source generator projects target `netstandard2.0`. If the CI workflow uses a per-TFM matrix for testing,
the build step must compile all TFMs first. A matrix build that only compiles `net10.0` will fail because
the source generator project cannot target `net10.0`. Build the full solution once, then use `--no-build`
in the per-TFM test steps.

### Files outside project directories

Incrementalist maps files to projects by directory containment. Files in `proto/`, `shared/`, or similar
top-level directories are invisible to this mapping. Use `dorny/paths-filter@v3` alongside Incrementalist
to detect changes in these directories and force a full build when they change.

### Non-standard test runners

Incrementalist's `run` verb executes `dotnet <command>` per project. For frameworks requiring `dotnet run`
(TUnit, Expecto), use list-only mode with the bundled `detect-affected.sh` script, then iterate with
`run-affected-tests.sh` using `--runner run`.

### Skip patterns and .gitignore

Do not add skip patterns for paths in `.gitignore` (e.g., `bin/`, `obj/`, `.worktrees/`). Incrementalist
analyzes git diffs; files not tracked by git never appear in the diff.

### `fetch-depth: 0`

Incrementalist needs full git history for branch comparison. Without `fetch-depth: 0`, the checkout action
creates a shallow clone and the diff fails. This is non-negotiable for the checkout step.

## Reference material

### Bundled references

- **Config settings and examples**: Read `references/config-reference.md`
- **Detection script template**: Read `scripts/detect-affected.sh`
- **Test runner script template**: Read `scripts/run-affected-tests.sh`

### Official Incrementalist documentation

These are copies of the upstream docs bundled for offline reference:

- **How it works**: Read `references/how-it-works.md`
- **Configuration**: Read `references/official-config.md`
- **Building from source**: Read `references/building.md`
- **Examples**: Read `references/official-examples.md`

### External links

- [Incrementalist GitHub repository](https://github.com/petabridge/Incrementalist)
- [Configuration schema](https://github.com/petabridge/Incrementalist/blob/dev/src/Incrementalist.Cmd/Config/incrementalist.schema.json)
