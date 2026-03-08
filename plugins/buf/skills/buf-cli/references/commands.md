# Buf CLI Commands Reference

Complete reference for all `buf` CLI commands, subcommands, and flags.

## `buf build`

Compile Protobuf files and output a Buf image.

```bash
buf build                          # Build current workspace
buf build -o image.binpb           # Output binary image
buf build -o image.json            # Output JSON image
buf build --exclude-source-info    # Exclude source info (smaller output)
buf build --path proto/foo         # Build specific path only
```

## `buf lint`

Lint Protobuf files according to configured rules.

```bash
buf lint                                    # Lint current workspace
buf lint --error-format=json                # JSON output
buf lint --path path/to/file.proto          # Lint specific file
buf lint --config buf.yaml                  # Use specific config
buf lint buf.build/acme/petapis             # Lint BSR module
buf lint 'https://github.com/org/repo.git' # Lint remote repo
buf lint --config '{"version":"v2","lint":{"use":["BASIC"]}}'  # Inline config
```

## `buf format`

Format Protobuf files for consistency.

```bash
buf format             # Output formatted to stdout
buf format -w          # Write formatted files in place
buf format -d          # Show diff
buf format --exit-code # Exit non-zero if changes needed
buf format -o output/  # Write to different directory
```

Flags:
- `-w` / `--write`: Rewrite files in place
- `-d` / `--diff`: Display diff
- `--exit-code`: Non-zero exit code on diff
- `-o` / `--output`: Output path (file or directory)
- `--only-main-content`: Strip navigation (for URLs)

## `buf breaking`

Detect breaking changes between current and reference schema.

```bash
# Against local git branch
buf breaking --against '.git#branch=main'

# Against git tag
buf breaking --against '.git#tag=v1.0.0'

# Against git tag with subdirectory
buf breaking --against '.git#tag=v1.0.0,subdir=proto'

# Against remote repository
buf breaking --against 'https://github.com/org/repo.git'

# Against BSR module
buf breaking --against buf.build/acme/petapis

# Against all BSR modules (all workspace modules must have names)
buf breaking --against-registry

# Against tarball
buf breaking --against "https://github.com/org/repo/archive/${COMMIT}.tar.gz#strip_components=1"

# JSON output
buf breaking --against '.git#branch=main' --error-format=json

# Limit to specific files
buf breaking --against '.git#branch=main' --path path/to/file.proto

# Custom config override
buf breaking --against '.git#branch=main' --config '{"version":"v2","breaking":{"use":["PACKAGE"]}}'
```

Flags:
- `--against`: Reference input to compare against (required unless `--against-registry`)
- `--against-registry`: Compare all modules against their BSR versions
- `--error-format`: Output format (`text` or `json`)
- `--path`: Limit to specific files
- `--exclude-path`: Exclude specific files
- `--config`: Override config

## `buf generate`

Generate code stubs from Protobuf files.

```bash
buf generate                                    # Generate from current workspace
buf generate buf.build/bufbuild/protovalidate   # Generate from BSR module
buf generate buf.build/foo/bar:v1.0.0           # Generate from specific version
buf generate https://github.com/foo/bar.git     # Generate from GitHub repo
buf generate --template buf.gen.go.yaml         # Use specific template
buf generate --path proto/foo                   # Generate for specific path
buf generate --exclude-path proto/baz           # Exclude paths
buf generate --type foo.v1.User                 # Generate for specific type
buf generate -o bar/                            # Output to specific directory
buf generate --include-imports                  # Include imported files
buf generate --include-imports --include-wkt    # Include Well-Known Types
buf generate --template '{"version":"v2","plugins":[{"protoc_builtin":"go","out":"gen/go"}]}'
```

Flags:
- `--template`: Path to buf.gen.yaml (or inline JSON)
- `--path`: Limit to specific files/dirs
- `--exclude-path`: Exclude paths
- `--type`: Limit to specific fully-qualified type names
- `-o` / `--output`: Prepend output directory to `out` paths
- `--include-imports`: Include imports in generation
- `--include-wkt`: Include Well-Known Types (requires `--include-imports`)

## `buf dep`

Manage dependencies.

```bash
buf dep update    # Update buf.lock with latest dependency versions
buf dep prune     # Remove unused dependencies from buf.lock
```

## `buf push`

Push modules to the Buf Schema Registry.

```bash
buf push                    # Push all workspace modules
buf push --label v1.0.0     # Push with a label
```

## `buf export`

Export module content to a directory.

```bash
buf export -o output/                      # Export workspace
buf export buf.build/acme/petapis -o out/  # Export BSR module
```

## `buf convert`

Convert Protobuf messages between binary and JSON.

```bash
buf convert --type foo.v1.Message --from input.bin
buf convert --type foo.v1.Message --from input.json --to binary
```

## `buf curl`

Invoke RPC endpoints.

```bash
buf curl --data '{"name": "world"}' \
  http://localhost:8080/package.v1.Service/Method
```

## `buf config`

Generate and manage configuration files.

```bash
buf config init                 # Create default buf.yaml
buf config ls-lint-rules        # List all lint rules and categories
buf config ls-breaking-rules    # List all breaking change rules and categories
buf config migrate              # Migrate v1 config to v2
```

## `buf registry`

Manage BSR resources.

```bash
buf registry login              # Authenticate with BSR
buf registry logout             # Remove authentication
```

## Global Flags

These flags apply to most commands:

| Flag | Description |
|------|-------------|
| `--debug` | Enable debug logging |
| `--timeout` | RPC timeout duration |
| `--log-format` | Log format (text or json) |
| `--version` | Print version |
| `--help` | Show help |
