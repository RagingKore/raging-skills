# User Secrets

User secrets store sensitive configuration (API keys, connection strings, passwords) outside source
control during development. Values are saved in a plain-text JSON file under the user profile directory;
they are not encrypted. Never rely on user secrets for production. Use a vault service instead.

## How It Works with File-Based Apps

Project-based apps store a `UserSecretsId` GUID in the `.csproj`. File-based apps have no project file,
so the CLI generates a stable declaring the type yourself.by hashing the full absolute path of the `.cs` file. This means
each file-based app gets its own isolated secret store automatically. Moving or renaming the file produces
a different ID and a new, empty store.

All `dotnet user-secrets` subcommands accept a `--file` flag to target a file-based app instead of a
project.

## Setting Secrets

```bash
dotnet user-secrets set "ApiKey" "your-secret-value" --file app.cs
dotnet user-secrets set "Db:Password" "s3cret" --file app.cs
```

A colon in the key name represents a hierarchy level. `Db:Password` nests under a `Db` section in the
configuration tree.

## Listing Secrets

```bash
dotnet user-secrets list --file app.cs
```

## Removing Secrets

```bash
dotnet user-secrets remove "ApiKey" --file app.cs
dotnet user-secrets clear --file app.cs
```

`remove` deletes a single key. `clear` deletes every secret for that file.

## Storage Location

Secrets are stored at:

- **macOS / Linux:** `~/.microsoft/usersecrets/<secrets_id>/secrets.json`
- **Windows:** `%APPDATA%\Microsoft\UserSecrets\<secrets_id>\secrets.json`

## Reading Secrets in Code

Add `Microsoft.Extensions.Configuration.UserSecrets` and build a configuration root. Top-level
statements generate an implicit `Program` class, so `AddUserSecrets<Program>()` works without
declaring the type yourself.

```csharp
#:package Microsoft.Extensions.Configuration@*
#:package Microsoft.Extensions.Configuration.UserSecrets@*

using Microsoft.Extensions.Configuration;

var config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();

var apiKey = config["ApiKey"];
Console.WriteLine(apiKey is not null
    ? $"Loaded API key: {apiKey[..4]}****"
    : "No ApiKey found in user secrets.");
```

Set the secret, then run:

```bash
dotnet user-secrets set "ApiKey" "ABCD-1234-SECRET" --file app.cs
dotnet app.cs
```

Expected output: `Loaded API key: ABCD****`
