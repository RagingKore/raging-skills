# Deploying Astro to Netlify

## Quick Setup

1. Push code to GitHub or GitLab
2. Connect repository in Netlify dashboard
3. Set build command: `npm run build`
4. Set publish directory: `dist`

## SSR with @astrojs/netlify

For server-side rendering, install the Netlify adapter:

```bash
npx astro add netlify
```

```javascript
// astro.config.mjs
import { defineConfig } from 'astro/config';
import netlify from '@astrojs/netlify';

export default defineConfig({
  output: 'server',
  adapter: netlify(),
});
```

## netlify.toml

```toml
[build]
  command = "npm run build"
  publish = "dist"

[build.environment]
  NODE_VERSION = "18"

# Redirects for SPA-style routing (if needed)
[[redirects]]
  from = "/*"
  to = "/index.html"
  status = 200

# Custom headers
[[headers]]
  for = "/*"
    [headers.values]
    X-Frame-Options = "DENY"
    X-Content-Type-Options = "nosniff"

# Trailing slashes
[build.processing]
  skip_processing = false
[build.processing.html]
  pretty_urls = true
```

## Environment Variables

Set environment variables in the Netlify dashboard or in `netlify.toml`. You can scope them by deploy context.

```toml
[context.production.environment]
  API_URL = "https://api.example.com"

[context.deploy-preview.environment]
  API_URL = "https://staging-api.example.com"
```

## Netlify Forms

Netlify can detect forms in your static HTML and handle submissions without a backend. Add a `netlify` attribute
to any `<form>` element and Netlify will process submissions automatically. Form data is accessible from the
Netlify dashboard or via webhook notifications.
