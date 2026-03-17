# Deploying Starlight to GitHub Pages

## Prerequisites

- A GitHub repository with your Starlight project
- GitHub Pages enabled in repo Settings → Pages → Source: **GitHub Actions**

## Configuration

### Site URL and base path

```javascript
// astro.config.mjs
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';

export default defineConfig({
  // For custom domain (e.g., docs.example.com):
  site: 'https://docs.example.com',

  // For username.github.io (user/org site):
  // site: 'https://username.github.io',

  // For username.github.io/repo-name (project site without custom domain):
  // site: 'https://username.github.io',
  // base: '/repo-name',

  integrations: [
    starlight({ title: 'My Docs' }),
  ],
});
```

**When to use `base`:** Only when deploying to a subpath like `username.github.io/repo-name`.
If you use a custom domain or it's a user/org site, omit `base`.

## GitHub Actions Workflow

### Standard workflow

```yaml
# .github/workflows/deploy.yml
name: Deploy to GitHub Pages

on:
  push:
    branches: [main]
  workflow_dispatch:

permissions:
  contents: read
  pages: write
  id-token: write

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v5

      - name: Build Astro site
        uses: withastro/action@v5
        # Uncomment to customize:
        # with:
        #   path: .             # Project root (for monorepos, set to subdir)
        #   node-version: 22    # Node.js version
        #   package-manager: pnpm@latest

  deploy:
    needs: build
    runs-on: ubuntu-latest
    environment:
      name: github-pages
      url: ${{ steps.deployment.outputs.page_url }}
    steps:
      - name: Deploy to GitHub Pages
        id: deployment
        uses: actions/deploy-pages@v4
```

The `withastro/action@v5` handles everything: detects your package manager from the lockfile,
installs dependencies, runs the build, and uploads the artifact for Pages.

### Monorepo workflow

If your docs are in a subdirectory (e.g., `docs/`):

```yaml
      - name: Build Astro site
        uses: withastro/action@v5
        with:
          path: docs
```

### With environment variables

```yaml
      - name: Build Astro site
        uses: withastro/action@v5
        env:
          PUBLIC_SITE_URL: 'https://docs.example.com'
```

## Custom Domain Setup

1. Add a `CNAME` file to `public/` with your domain:

```
docs.example.com
```

2. Configure DNS:
   - For apex domain (`example.com`): Add `A` records pointing to GitHub Pages IPs
   - For subdomain (`docs.example.com`): Add a `CNAME` record pointing to `username.github.io`

3. In repo Settings → Pages → Custom domain, enter your domain

4. Enable "Enforce HTTPS"

GitHub Pages IPs (for A records):
```
185.199.108.153
185.199.109.153
185.199.110.153
185.199.111.153
```

## Troubleshooting

### 404 on page refresh

If you get 404s on direct URL access or page refresh, ensure GitHub Pages source is set to
"GitHub Actions" (not "Deploy from a branch").

### Assets not loading

Check your `base` config matches your repo name exactly. A missing or wrong `base` causes
broken asset paths.

```javascript
// Wrong — missing leading slash or trailing slash issues
base: 'repo-name'

// Correct
base: '/repo-name'
```

### Build fails in CI

The `withastro/action@v5` auto-detects your package manager. If detection fails, specify it:

```yaml
with:
  package-manager: pnpm@latest
```

Make sure your lockfile (`package-lock.json`, `pnpm-lock.yaml`, or `yarn.lock`) is committed.
