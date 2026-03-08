# Deploying Astro to Cloudflare

Cloudflare acquired Astro in January 2026. The integration between the platforms is deepening; Astro 6 Beta
includes native Cloudflare Workers support.

## Cloudflare Pages (SSG)

Connect your GitHub repository in the Cloudflare dashboard. The Astro framework preset is auto-detected, so build
command and output directory are configured automatically.

1. Connect GitHub repo in Cloudflare dashboard
2. Set framework preset: Astro
3. Build command and output directory are auto-detected

## Cloudflare Workers/Pages (SSR)

For server-side rendering, use the `@astrojs/cloudflare` adapter.

### Install the Adapter

```bash
npx astro add cloudflare
```

### Configuration

```javascript
// astro.config.mjs
import { defineConfig } from 'astro/config';
import cloudflare from '@astrojs/cloudflare';

export default defineConfig({
  output: 'server',
  adapter: cloudflare(),
});
```

### wrangler.toml

```toml
name = "my-astro-site"
compatibility_date = "2024-01-01"

[site]
bucket = "./dist"
```

## Custom Headers

Create `public/_headers` to set custom response headers:

```text
/*
  X-Frame-Options: DENY
  X-Content-Type-Options: nosniff

/assets/*
  Cache-Control: public, max-age=31536000, immutable
```

## Environment Variables

Set environment variables through the Cloudflare dashboard or in `wrangler.toml`:

```toml
[vars]
API_URL = "https://api.example.com"
```

For secrets that should not be committed to source control, use the Cloudflare dashboard or the CLI:

```bash
wrangler secret put SECRET_KEY
```
