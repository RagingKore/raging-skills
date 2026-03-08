# Deploying Astro to GitHub Pages

## Recommended Approach

Use the official `withastro/action@v5` GitHub Action. It is maintained by the Astro team and handles Node setup,
dependency installation, and building automatically.

### Workflow with withastro/action

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
      - uses: actions/checkout@v4
      - uses: withastro/action@v5

  deploy:
    needs: build
    runs-on: ubuntu-latest
    environment:
      name: github-pages
      url: ${{ steps.deployment.outputs.page_url }}
    steps:
      - uses: actions/deploy-pages@v4
        id: deployment
```

## Manual Workflow Alternative

If you need more control over the build process, you can configure each step manually.

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
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: 20
          cache: npm

      - run: npm ci
      - run: npm run build

      - uses: actions/upload-pages-artifact@v3
        with:
          path: dist

  deploy:
    needs: build
    runs-on: ubuntu-latest
    environment:
      name: github-pages
      url: ${{ steps.deployment.outputs.page_url }}
    steps:
      - uses: actions/deploy-pages@v4
        id: deployment
```

## Configuring site and base for Subpaths

When deploying to `https://username.github.io/repository-name`, set `site` and `base` in your Astro config so
that asset paths and links resolve correctly.

```javascript
// astro.config.mjs
import { defineConfig } from 'astro/config';

export default defineConfig({
  site: 'https://username.github.io',
  base: '/repository-name',
});
```

Update internal links to include the base path:

```astro
<a href={`${import.meta.env.BASE_URL}about`}>About</a>
```
