# Implicit Build Files

File-based apps inherit MSBuild/NuGet configuration from parent directories.

## Inherited Files

| File                       | Effect                                                 |
|----------------------------|--------------------------------------------------------|
| `Directory.Build.props`    | MSBuild properties applied to all projects in the tree |
| `Directory.Build.targets`  | MSBuild targets and custom build logic                 |
| `Directory.Packages.props` | Central package management versions                    |
| `nuget.config`             | NuGet package sources and settings                     |
| `global.json`              | .NET SDK version pinning                               |

## What Each File Means for File-Based Apps

**Directory.Build.props** – MSBuild walks up until it finds this file.
File-based apps have no `.csproj`, but the SDK synthesizes one at build
time, so the same walk-up logic applies.

**Directory.Build.targets** – Evaluated after the project body. Targets
that assume a standard project layout can break file-based apps.

**Directory.Packages.props** – Enables Central Package Management (CPM).
`#:package` directives may conflict when CPM is active in a parent directory.

**nuget.config** – Controls package sources and credentials. A parent
config pointing to a private feed will fail without matching credentials.

**global.json** – Pins the SDK version. A parent pinning a pre-.NET 9
SDK will block file-based app builds entirely.

## Isolation Pattern

Place minimal override files in the script directory. MSBuild stops walking
up once it finds the nearest match, giving the script a clean environment.

```xml
<!-- scripts/Directory.Build.props - blocks props inheritance -->
<Project>
  <!-- Intentionally empty -->
</Project>

<!-- scripts/Directory.Build.targets - blocks targets inheritance -->
<Project />
```

## Problematic Scenario

A parent `Directory.Build.props` sets `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`
and `<Nullable>enable</Nullable>`. A utility script in a subdirectory now
fails because every missing null check becomes a build error – the script
author never opted in. Drop an empty `Directory.Build.props` next to the
script (see isolation pattern above) to fix this.

## Build Cache Interaction

The `dotnet run` build cache keys on the source file and resolved build
configuration. Changes to implicit build files do not always invalidate
the cache automatically:

- Editing props or targets files may leave a stale cached binary.
- Adding or removing `global.json` can switch the SDK version silently.

Force a clean build when behavior seems stale:

```bash
dotnet clean app.cs
dotnet build app.cs
```
