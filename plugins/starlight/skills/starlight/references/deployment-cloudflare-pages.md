# Deploying Starlight to Cloudflare Pages

For static Starlight sites, no Astro adapter is needed. Cloudflare Pages serves the built
`dist/` directory directly.

## Prerequisites

- A Cloudflare account (free tier works for Pages)
- Your Starlight project in a Git repository (GitHub or GitLab)
- Wrangler CLI (optional, for CLI-based deploys): `npm install -g wrangler`

## Option A: Git Integration (Recommended)

The simplest approach — Cloudflare builds and deploys automatically on every push.

### Setup

1. Go to Cloudflare dashboard → **Workers & Pages** → **Create** → **Pages** tab
2. **Connect to Git** → select your repository
3. Configure build settings:
   - **Framework preset**: Astro
   - **Build command**: `npm run build`
   - **Build output directory**: `dist`
4. Click **Save and Deploy**

Cloudflare auto-detects your package manager from the lockfile. Every push to your production
branch triggers a new build and deploy.

### Environment variables

Add environment variables in the Cloudflare Pages project settings, or in `wrangler.toml`:

```
PUBLIC_SITE_URL = "https://docs.example.com"
```

### Preview deployments

Cloudflare Pages automatically creates preview deployments for non-production branches and
pull requests. Each branch gets a stable URL at `<branch-name>.my-docs.pages.dev`.

## Option B: Wrangler CLI (Direct Upload)

Deploy from your terminal or CI without connecting a Git repo.

### First-time setup

```bash
# Install Wrangler globally (or use npx)
npm install -g wrangler

# Authenticate with Cloudflare
wrangler login

# Optionally create the project explicitly
npx wrangler pages project create my-docs --production-branch=main
```

### Manual deploy

```bash
# Build the site
npm run build

# Deploy to Cloudflare Pages
npx wrangler pages deploy dist --project-name my-docs
```

On first run, Wrangler creates the Pages project if it doesn't exist. Subsequent deploys
update the same project.

### Wrangler deploy flags

| Flag | Description |
|---|---|
| `--project-name` | The Pages project to deploy to |
| `--branch` | Branch name (determines production vs preview) |
| `--commit-hash` | Git SHA to associate with the deployment |
| `--commit-message` | Commit message for the deployment |
| `--skip-caching` | Bypass asset caching to force a fresh upload |

### Deploy to a specific branch/environment

```bash
# Production deployment
npx wrangler pages deploy dist --project-name my-docs --branch main

# Preview deployment
npx wrangler pages deploy dist --project-name my-docs --branch feature-xyz
```

## Option C: GitHub Actions with Wrangler

Automate deploys via CI without using Cloudflare's Git integration.

```yaml
# .github/workflows/deploy-cloudflare.yml
name: Deploy to Cloudflare Pages

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  deploy:
    runs-on: ubuntu-latest
    permissions:
      contents: read
      deployments: write
    steps:
      - name: Checkout
        uses: actions/checkout@v5

      - name: Setup Node
        uses: actions/setup-node@v4
        with:
          node-version: 22
          cache: npm

      - name: Install dependencies
        run: npm ci

      - name: Build
        run: npm run build

      - name: Deploy to Cloudflare Pages
        uses: cloudflare/wrangler-action@v3
        with:
          apiToken: ${{ secrets.CLOUDFLARE_API_TOKEN }}
          accountId: ${{ secrets.CLOUDFLARE_ACCOUNT_ID }}
          command: pages deploy dist --project-name my-docs
          gitHubToken: ${{ secrets.GITHUB_TOKEN }}
```

The `gitHubToken` enables Cloudflare to post deployment status back to the GitHub UI
(the `deployments: write` permission is required for this).

### Required secrets

1. **`CLOUDFLARE_API_TOKEN`**: Create at Cloudflare dashboard → My Profile → API Tokens →
   Create Token → "Edit Cloudflare Workers" template (includes Pages permissions)
2. **`CLOUDFLARE_ACCOUNT_ID`**: Found on the Workers & Pages overview page in the right sidebar

## Custom Domain

1. In Cloudflare Pages project → **Custom domains** → **Set up a custom domain**
2. Enter your domain (e.g., `docs.example.com`)
3. Cloudflare configures DNS automatically if the domain is on Cloudflare

If the domain is not on Cloudflare DNS, add a CNAME record:
```
docs.example.com  CNAME  my-docs.pages.dev
```

SSL/TLS is automatic — Cloudflare provisions a certificate.

## Astro Configuration

For static Cloudflare Pages deployment, your config is straightforward:

```javascript
// astro.config.mjs
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';

export default defineConfig({
  site: 'https://docs.example.com',
  integrations: [
    starlight({
      title: 'My Docs',
      // ... rest of config
    }),
  ],
});
```

No adapter, no `output` setting, no special build config needed for static sites.

### Optional: `wrangler.toml`

For CLI-based deploys, you can add a `wrangler.toml` to your project root. The
`pages_build_output_dir` key tells Wrangler this is a Pages project (not Workers):

```toml
name = "my-docs"
pages_build_output_dir = "./dist"
compatibility_date = "2026-03-17"
```

With this file, you can deploy with just `npx wrangler pages deploy` (no flags needed).

## Headers and Redirects

Cloudflare Pages supports `_headers` and `_redirects` files in your output directory.
Place them in `public/` so they're copied to `dist/` at build time.

### Custom headers

```
# public/_headers

# Cache static assets aggressively
/_astro/*
  Cache-Control: public, max-age=31536000, immutable

# Security headers for all pages
/*
  X-Content-Type-Options: nosniff
  X-Frame-Options: DENY
  Referrer-Policy: strict-origin-when-cross-origin
  Permissions-Policy: camera=(), microphone=(), geolocation=()
```

Limits: 100 header rules max, 2,000 characters per line.

### Redirects

```
# public/_redirects

# Redirect old paths to new ones
/old-page /new-page 301
/blog/* /guides/:splat 301

# Redirect www to apex
https://www.docs.example.com/* https://docs.example.com/:splat 301
```

Supported status codes: `301`, `302`, `303`, `307`, `308`. Default is `302` if omitted.
Limits: 2,000 static + 100 dynamic redirect rules.

## Troubleshooting

### Build fails on Cloudflare

Cloudflare Pages uses a specific Node.js version. Set it explicitly:
- In dashboard: Environment variables → `NODE_VERSION` = `22`
- Or add a `.node-version` file to your repo root: `22`

### Assets not loading

Ensure `site` in your Astro config matches your actual deployment URL. Don't set `base`
unless you're deploying to a subpath (uncommon with Cloudflare Pages).

### Large sites and build limits

Cloudflare Pages free tier:
- 500 builds per month
- 25 MiB max file size
- 20,000 files max
- 1 build at a time

These limits are generous for documentation sites. If you hit the file limit, check if
you have unnecessary files in `public/`.
