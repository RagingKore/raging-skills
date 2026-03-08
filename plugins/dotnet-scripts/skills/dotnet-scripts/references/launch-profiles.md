# Launch Profiles

File-based apps support a flat launch settings file named `<app>.run.json` in the same directory.
Each file-based app in a directory can have its own `.run.json`, so launch configuration
stays co-located with the source file it belongs to.

## File Layout

```text
myapps/
  foo.cs
  foo.run.json
  bar.cs
  bar.run.json
```

## Example `app.run.json`

A single file can contain multiple profiles. The first profile in the file acts as the
default when no explicit selection is made.

```json
{
  "profiles": {
    "http": {
      "commandName": "Project",
      "applicationUrl": "http://localhost:5000",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    },
    "https": {
      "commandName": "Project",
      "applicationUrl": "https://localhost:5001;http://localhost:5000",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

## Profile Selection Priority

The runtime resolves which profile to use in this order:

1. The `--launch-profile` command-line option (highest priority)
2. The `DOTNET_LAUNCH_PROFILE` environment variable
3. The first profile defined in the file (default)

If none of these resolve to a valid profile name, the app starts without any launch
profile settings applied.

## Usage

Run with a specific profile:

```bash
dotnet run app.cs --launch-profile https
```

Run using the environment variable instead:

```bash
export DOTNET_LAUNCH_PROFILE=https
dotnet run app.cs
```

Omit both to use the first profile in the file (in the example above, `http`):

```bash
dotnet run app.cs
```

## Traditional `launchSettings.json`

The traditional `Properties/launchSettings.json` file is also supported for file-based
apps. If both `<app>.run.json` and `Properties/launchSettings.json` exist in the same
directory, the traditional file takes priority and a warning is emitted.

Prefer `.run.json` for file-based apps since it keeps configuration next to the source
file and avoids the nested `Properties/` folder.
