# Deploying Astro to Firebase Hosting

## Initial Setup

```bash
# Install Firebase CLI
npm install -g firebase-tools

# Login and initialize
firebase login
firebase init hosting
```

## firebase.json

```json
{
  "hosting": {
    "public": "dist",
    "ignore": [
      "firebase.json",
      "**/.*",
      "**/node_modules/**"
    ],
    "rewrites": [],
    "headers": [
      {
        "source": "/assets/**",
        "headers": [
          {
            "key": "Cache-Control",
            "value": "public, max-age=31536000, immutable"
          }
        ]
      },
      {
        "source": "**",
        "headers": [
          {
            "key": "X-Frame-Options",
            "value": "DENY"
          }
        ]
      }
    ],
    "cleanUrls": true,
    "trailingSlash": false
  }
}
```

## URL Alignment (Trailing Slashes)

Firebase and Astro must agree on trailing slash behavior. A mismatch causes redirect loops.

| Firebase Setting       | Astro Setting             | Result                 |
|------------------------|---------------------------|------------------------|
| `trailingSlash: false` | `trailingSlash: 'never'`  | `/about` (no slash)    |
| `trailingSlash: true`  | `trailingSlash: 'always'` | `/about/` (with slash) |
| Mismatch               | Mismatch                  | Redirect loops!        |

## Deploy Commands

```bash
# Build and deploy
npm run build
firebase deploy --only hosting

# Deploy to preview channel
firebase hosting:channel:deploy preview-name

# Deploy with custom site ID (multiple sites)
firebase deploy --only hosting:my-site-id
```

## GitHub Actions Workflow

```yaml
# .github/workflows/firebase-deploy.yml
name: Deploy to Firebase

on:
  push:
    branches: [main]

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: 20
          cache: npm

      - run: npm ci
      - run: npm run build

      - uses: FirebaseExtended/action-hosting-deploy@v0
        with:
          repoToken: ${{ secrets.GITHUB_TOKEN }}
          firebaseServiceAccount: ${{ secrets.FIREBASE_SERVICE_ACCOUNT }}
          channelId: live
          projectId: your-project-id
```

## Multiple Sites

```json
{
  "hosting": [
    {
      "target": "main-site",
      "public": "dist",
      "ignore": ["firebase.json", "**/.*"]
    },
    {
      "target": "docs-site",
      "public": "docs/dist",
      "ignore": ["firebase.json", "**/.*"]
    }
  ]
}
```

```bash
firebase target:apply hosting main-site my-main-site
firebase target:apply hosting docs-site my-docs-site
firebase deploy --only hosting
```

## SSR Note

There is no official Firebase SSR adapter for Astro. For server-side rendering on Google Cloud, use
`@astrojs/node` with Cloud Run instead.
