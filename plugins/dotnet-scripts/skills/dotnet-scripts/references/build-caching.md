# Build Caching

The SDK caches build outputs to improve performance on subsequent runs of `dotnet run`.
This caching system is unique to file-based apps.

Cache location: `<temp>/dotnet/runfile/<appname>-<appfilesha>/bin/<configuration>/`.

## What Triggers Cache Invalidation

The SDK recomputes the cache when any of the following change:

- **Source file content** – any edit to the `.cs` file itself.
- **Directive configuration** – adding, removing, or changing `#:` directives
  (`#:package`, `#:property`, `#:sdk`, `#:project`).
- **SDK version** – updating the .NET SDK or pinning a different version
  via `global.json`.
- **Implicit build files** – changes to the existence or content of
  `Directory.Build.props`, `Directory.Build.targets`,
  `Directory.Packages.props`, `nuget.config`, or `global.json` in the same
  or parent directories.

## What Does NOT Trigger Rebuilds

Two situations can cause stale-cache confusion:

- **Changes to implicit build files sometimes go unnoticed.** Edits to files
  like `Directory.Build.props` do not always invalidate the cache, depending
  on when and how the SDK detects them.
- **Moving a file to a different directory does not invalidate the cache.**
  The cached output is keyed to the original file path and content hash, so
  relocating the source file can leave the old cache in place.

## Clearing the Cache

```bash
dotnet clean hello.cs                  # clean a specific app
dotnet clean file-based-apps           # clean all file-based app caches
dotnet clean file-based-apps --days 7  # only if unused for 7+ days
```

The `--days` option specifies how many days an artifact folder must be unused
before removal. The default is **30 days** when `--days` is omitted.

## Force Clean Build

To bypass the cache entirely, clean and then rebuild:

```bash
dotnet clean hello.cs
dotnet build hello.cs
```

This is the recommended workaround when implicit build file changes are not
picked up automatically or when you suspect a stale cache.

## Concurrent Runs

Running the same file-based app concurrently can cause errors due to
contention over the build output files. Pre-build once, then launch
multiple instances with `--no-build`:

```bash
dotnet build hello.cs
dotnet run hello.cs --no-build &
dotnet run hello.cs --no-build &
```
