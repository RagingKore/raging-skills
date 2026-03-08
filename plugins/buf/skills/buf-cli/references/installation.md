# Buf CLI Installation Reference

## macOS / Linux — Homebrew (Recommended)

```bash
brew install bufbuild/buf/buf
```

Installs `buf`, `protoc-gen-buf-breaking`, `protoc-gen-buf-lint`, and shell completions (bash, fish, zsh).

## npm

```bash
npm install @bufbuild/buf
npx buf --version
```

Installs `buf`, `protoc-gen-buf-breaking`, `protoc-gen-buf-lint` for use within your project.

## Windows — Scoop

```bash
scoop install buf
scoop update buf
```

## Windows — WinGet

```bash
winget install bufbuild.buf
winget upgrade bufbuild.buf
```

## GitHub Releases — Binary

```bash
BIN="/usr/local/bin" && \
VERSION="$(curl -s https://api.github.com/repos/bufbuild/buf/releases/latest | grep tag_name | cut -d '"' -f4 | sed 's/^v//')" && \
curl -sSL \
  "https://github.com/bufbuild/buf/releases/download/v${VERSION}/buf-$(uname -s)-$(uname -m)" \
  -o "${BIN}/buf" && \
chmod +x "${BIN}/buf"
```

## GitHub Releases — Tarball (with completions)

```bash
PREFIX="/usr/local" && \
VERSION="$(curl -s https://api.github.com/repos/bufbuild/buf/releases/latest | grep tag_name | cut -d '"' -f4 | sed 's/^v//')" && \
curl -sSL \
  "https://github.com/bufbuild/buf/releases/download/v${VERSION}/buf-$(uname -s)-$(uname -m).tar.gz" | \
tar -xvzf - -C "${PREFIX}" --strip-components 1
```

Installs binaries to `$PREFIX/bin/` and completions to `$PREFIX/etc/`.

## Go Install

```bash
# Unix
GOBIN=/usr/local/bin go install github.com/bufbuild/buf/cmd/buf@latest

# Windows
GOBIN=C:\dev\go\bin go install github.com/bufbuild/buf/cmd/buf@latest
```

Do NOT use `tools.go` or `go tool` — Buf doesn't recommend it due to dependency resolution issues.

## Docker

```bash
docker run --volume "$(pwd):/workspace" --workdir /workspace bufbuild/buf lint
docker run --volume "$(pwd):/workspace" --workdir /workspace bufbuild/buf format -d
```

The Docker image does NOT include `protoc` or plugins. For remote plugins, no extra config needed. For local plugins, build a custom image:

```dockerfile
FROM bufbuild/buf:latest
RUN apk add --no-cache protobuf-dev
WORKDIR /workspace
```

## Verify Release (minisign)

Public key: `RWQ/i9xseZwBVE7pEniCNjlNOeeyp4BQgdZDLQcAohxEAH5Uj5DEKjv6`

```bash
VERSION="$(curl -s https://api.github.com/repos/bufbuild/buf/releases/latest | grep tag_name | cut -d '"' -f4 | sed 's/^v//')" && \
curl -OL https://github.com/bufbuild/buf/releases/download/v${VERSION}/sha256.txt && \
curl -OL https://github.com/bufbuild/buf/releases/download/v${VERSION}/sha256.txt.minisig && \
minisign -Vm sha256.txt -P RWQ/i9xseZwBVE7pEniCNjlNOeeyp4BQgdZDLQcAohxEAH5Uj5DEKjv6
```

## Version Check

```bash
buf --version
```
