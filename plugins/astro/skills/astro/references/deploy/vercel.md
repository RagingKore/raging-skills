# Deploying Astro to Vercel

## Quick Setup

1. Install Vercel CLI: `npm i -g vercel`
2. Run `vercel` in project root
3. Follow prompts to link or create a project

Alternatively, connect your GitHub repository directly in the Vercel dashboard.

## SSR with @astrojs/vercel

For server-side rendering, install the Vercel adapter:

```bash
npx astro add vercel
```

```javascript
// astro.config.mjs
import { defineConfig } from 'astro/config';
import vercel from '@astrojs/vercel';

export default defineConfig({
  output: 'server',
  adapter: vercel(),
});
```

## vercel.json

```json
{
  "buildCommand": "npm run build",
  "outputDirectory": "dist",
  "framework": "astro",
  "routes": [
    {
      "src": "/assets/(.*)",
      "headers": {
        "Cache-Control": "public, max-age=31536000, immutable"
      }
    }
  ],
  "trailingSlash": false
}
```

## ISR (Incremental Static Regeneration)

Vercel supports ISR for Astro pages when using the `@astrojs/vercel` adapter. This allows you to serve
pre-rendered pages that revalidate in the background after a configurable time interval; combining the speed of
static pages with the freshness of server-rendered content.

## Edge Middleware

The Vercel adapter supports Edge Middleware for request interception, redirects, and header modification at the
edge. Configure it through the adapter options.

## Preview Deployments

Every pull request gets an automatic preview deployment. Each preview has a unique URL following the pattern
`project-git-branch-username.vercel.app`, making it easy to review changes before merging.
