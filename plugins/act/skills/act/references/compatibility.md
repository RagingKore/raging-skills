# Action Compatibility and Troubleshooting

## Actions with known compatibility considerations

### `actions/checkout`

Works well with act. Key differences:

- `fetch-depth: 0` works correctly for full history
- The `token` input defaults to `GITHUB_TOKEN`; provide it if the workflow uses it for API calls
- Submodules work if the host has access to the submodule repositories

### `actions/setup-node`, `actions/setup-python`, `actions/setup-dotnet`, `actions/setup-java`

Generally work. The setup actions download and cache tool versions. Considerations:

- First run is slow because tools are downloaded fresh
- Use `--action-offline-mode` after the first successful run to skip re-downloading
- Some versions may not be available for all architectures (e.g., `linux/arm64` images may lack certain tool
  versions)

### `actions/cache`

Works with limitations:

- Caching uses a local server that act manages
- Cross-run caching does not persist by default; the cache is ephemeral per act invocation
- Use `--reuse` flag to keep containers between runs for warm caches
- The cache key logic works the same as on GitHub

### `actions/upload-artifact` and `actions/download-artifact`

Require explicit opt-in:

```sh
act --artifact-server-path ./artifacts
```

Without this flag, artifact actions silently succeed but do nothing. Cross-run artifact downloads are not
supported.

### `docker/build-push-action` and `docker/login-action`

Work when Docker-in-Docker is available:

- Act containers need access to the Docker socket
- Use `--privileged` if the workflow builds Docker images
- `docker/login-action` works if credentials are provided via secrets

### `dorny/paths-filter`

Works correctly. Requires `fetch-depth: 0` on the checkout step so the full git history is available for
diffing.

### Service containers (`services:`)

Partially supported. Act can start service containers defined in the workflow, but:

- Networking between the job container and service containers may differ from GitHub
- Health checks and readiness probes work but timing may vary
- `--bind` mode can help with network connectivity issues

### `github-script` and API-calling actions

Work if `GITHUB_TOKEN` is provided:

```sh
act -s GITHUB_TOKEN="$(gh auth token)"
```

Rate limiting applies to the token. For heavy API usage, consider using a fine-grained personal access token
with appropriate permissions.

### Composite actions and reusable workflows

Composite actions work. Reusable workflows (`workflow_call`) have partial support:

- Local reusable workflows (in the same repository) work
- Remote reusable workflows may not resolve correctly depending on the act version

## Common issues and solutions

### Workflow runs on GitHub but fails locally

**Missing tools**: The default micro/medium images do not include all tools available on GitHub-hosted runners.
Solutions:

- Switch to the large image (`catthehacker/ubuntu:full-latest`)
- Install missing tools in a setup step
- Use `-self-hosted` mode to use host tools

**Missing secrets**: Workflows that use secrets fail silently or with empty values. Always provide required
secrets via `--secret-file`.

**Architecture mismatch**: On Apple Silicon Macs, x86 images may fail. Add to `.actrc`:

```text
--container-architecture linux/amd64
```

**GITHUB_TOKEN not set**: Many actions implicitly require `GITHUB_TOKEN`. Even if the workflow does not
explicitly reference it, composite actions or scripts may use it.

### Workflow fails locally but passes on GitHub

**Environment differences**: Act's Docker images differ from GitHub's hosted runner images. Installed tool
versions, system libraries, and PATH entries may vary.

**Network issues**: Some actions fetch resources from the internet. Docker containers may have DNS or proxy
issues. Check Docker network configuration.

**Permissions**: File permissions inside containers differ from the host. Actions that modify file permissions
may behave differently.

### `MODULE_NOT_FOUND` errors

A known issue with some Node.js-based actions. Solutions:

- Update act to the latest version
- Try a different runner image
- Use `--action-offline-mode` if the action was previously cached successfully

### Container architecture errors

On Apple Silicon, some images only support `linux/amd64`. Rosetta 2 emulation handles most cases, but some
binaries may fail. Force the architecture:

```text
--container-architecture linux/amd64
```

### Slow first run

The first run downloads Docker images and action repositories. Subsequent runs are faster. To speed up:

- Use `--pull=false` to skip image checks after the first successful run
- Use `--action-offline-mode` for fully cached execution
- Use `--bind` to mount the workdir instead of copying it (faster for large repos)

### Docker socket permission denied

Act needs access to the Docker daemon. Common fixes:

- Ensure Docker Desktop is running (macOS/Windows)
- Add your user to the `docker` group (Linux): `sudo usermod -aG docker $USER`
- Check the socket path: `--container-daemon-socket /var/run/docker.sock`

## Feature support matrix

| Feature                      | Support level | Notes                                    |
|------------------------------|---------------|------------------------------------------|
| Workflow YAML parsing        | Full          |                                          |
| Job dependency graphs        | Full          |                                          |
| Matrix strategies            | Full          | Can filter but not add new values        |
| Secrets and variables        | Full          | Via files or CLI flags                   |
| Environment files            | Full          |                                          |
| Composite actions            | Full          |                                          |
| Docker container actions     | Full          |                                          |
| JavaScript actions           | Full          |                                          |
| Reusable workflows (local)   | Full          |                                          |
| Reusable workflows (remote)  | Partial       | May not resolve all references           |
| Service containers           | Partial       | Networking differs from GitHub           |
| Artifacts                    | Partial       | Requires `--artifact-server-path`        |
| Caching                      | Partial       | Ephemeral per run                        |
| OIDC tokens                  | Not supported |                                          |
| GitHub-hosted runner GPU     | Not supported |                                          |
| Larger runners               | Not supported | Use custom Docker images instead         |
| Docker context               | Not supported |                                          |
