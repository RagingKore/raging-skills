---
name: starlight
description: >
  Expert guide for building and deploying static documentation sites with Astro Starlight.
  Use this skill whenever the user mentions Starlight, Astro docs sites, documentation website
  creation, deploying docs to Cloudflare Pages or GitHub Pages, migrating from VuePress or
  Docusaurus or MkDocs to Starlight, customizing a Starlight theme, or working with
  @astrojs/starlight configuration. Also trigger when users want a static documentation site
  recommendation, ask about doc site generators, need help with Starlight sidebar, navigation,
  i18n, versioning, components, or theming — even if they don't say "Starlight" by name.
---

# Starlight Documentation Sites

You are a Starlight expert. Starlight is Astro's official documentation framework — it generates
fast, accessible, full-featured doc sites from Markdown, MDX, or Markdoc content.

## Core Principle: Start Simple, Scale Up

Documentation sites succeed when they ship. The fastest path to a great doc site:

1. Scaffold the project
2. Write content in Markdown
3. Deploy

Starlight's defaults are carefully designed — search, dark mode, responsive layout, navigation,
and accessibility all work out of the box with zero configuration. Custom themes, component
overrides, i18n, and versioning can all be added incrementally after the site is live.

Only add complexity when the user asks for it or the project clearly needs it. Three pages of
real content beat a perfectly configured empty site every time.

## Decision Tree

Match the user's situation to the right approach:

| Situation                                | Action                                           |
|------------------------------------------|--------------------------------------------------|
| New documentation site                   | Quick Start below                                |
| Add docs to existing Astro project       | Integration section below                        |
| Migrating from VuePress                  | Read `references/migration-vuepress.md`          |
| Migrating from Docusaurus, MkDocs, other | Read `references/migration-other.md`             |
| Deploying to GitHub Pages                | Read `references/deployment-github-pages.md`     |
| Deploying to Cloudflare Pages            | Read `references/deployment-cloudflare-pages.md` |
| Custom theme, CSS, branding              | Read `references/customization.md`               |
| Multi-language site (i18n)               | Read `references/i18n.md`                        |
| Versioned documentation                  | Read `references/versioned-docs.md`              |
| Deep configuration questions             | Read `references/configuration.md`               |

## Quick Start

### Scaffold a new project

```bash
# npm
npm create astro@latest -- --template starlight

# pnpm
pnpm create astro --template starlight

# yarn
yarn create astro --template starlight
```

Run `npm run dev` — your docs site is live at http://localhost:4321.

### Project structure

```
.
├── astro.config.mjs          # Astro + Starlight config
├── src/
│   ├── content/
│   │   └── docs/             # Your documentation pages
│   │       ├── index.mdx     # Home page
│   │       └── guides/
│   │           └── example.md
│   ├── content.config.ts     # Content collection schema
│   ├── assets/               # Optimized images
│   └── styles/               # Custom CSS (optional)
├── public/                   # Static assets (favicon, robots.txt)
└── package.json
```

### Minimal config

```javascript
// astro.config.mjs
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';

export default defineConfig({
  integrations: [
    starlight({
      title: 'My Docs',
    }),
  ],
});
```

That's the entire config needed for a working site. Starlight handles navigation, search
(Pagefind), dark mode, responsive layout, and accessibility automatically.

### Content collection setup

```typescript
// src/content.config.ts
import { defineCollection } from 'astro:content';
import { docsLoader } from '@astrojs/starlight/loaders';
import { docsSchema } from '@astrojs/starlight/schema';

export const collections = {
  docs: defineCollection({ loader: docsLoader(), schema: docsSchema() }),
};
```

### Writing content

Every `.md` or `.mdx` file in `src/content/docs/` becomes a page:

```markdown
---
title: Getting Started
description: Learn how to set up the project.
---

Your content here. Standard Markdown works perfectly.
```

Key frontmatter fields:
- `title` (required) — page title, becomes the `<h1>`
- `description` — SEO meta description
- `sidebar` — override label, order, or add a badge
- `tableOfContents` — configure or disable TOC for this page
- `hero` — add a hero section (for landing pages)
- `template` — use `splash` for landing pages instead of default doc layout

## Adding Starlight to an Existing Astro Project

```bash
npx astro add starlight
```

This installs `@astrojs/starlight` and updates your config. Create `src/content/docs/`
and start adding content.

If you already have content collections, merge the Starlight docs collection into your
existing `content.config.ts`:

```typescript
import { defineCollection } from 'astro:content';
import { docsLoader } from '@astrojs/starlight/loaders';
import { docsSchema } from '@astrojs/starlight/schema';

const blog = defineCollection({ /* your existing collection */ });

export const collections = {
  docs: defineCollection({ loader: docsLoader(), schema: docsSchema() }),
  blog,
};
```

## Sidebar Configuration

### Autogenerated (simplest)

```javascript
starlight({
  sidebar: [
    { label: 'Guides', autogenerate: { directory: 'guides' } },
    { label: 'Reference', autogenerate: { directory: 'reference' } },
  ],
})
```

Files sort alphabetically by filename. Control order with frontmatter:

```markdown
---
title: First Guide
sidebar:
  order: 1
---
```

### Manual

```javascript
starlight({
  sidebar: [
    { label: 'Home', link: '/' },
    {
      label: 'Guides',
      items: [
        { slug: 'guides/getting-started' },
        { slug: 'guides/configuration' },
      ],
    },
    { label: 'API Reference', link: '/api' },
  ],
})
```

### Collapsible groups

```javascript
{
  label: 'Advanced Topics',
  collapsed: true,
  autogenerate: { directory: 'advanced' },
}
```

### Mixed approach

Combine manual items with autogenerated sections — this is the most common pattern for
real projects:

```javascript
starlight({
  sidebar: [
    { label: 'Overview', slug: 'index' },
    {
      label: 'Getting Started',
      items: [
        { slug: 'guides/installation' },
        { slug: 'guides/first-steps' },
      ],
    },
    { label: 'Guides', autogenerate: { directory: 'guides/topics' } },
    {
      label: 'API',
      collapsed: true,
      autogenerate: { directory: 'reference' },
    },
  ],
})
```

## Practical Configuration

A realistic config for a project that's ready to share publicly:

```javascript
// astro.config.mjs
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';

export default defineConfig({
  site: 'https://docs.example.com',
  integrations: [
    starlight({
      title: 'My Project',
      description: 'Documentation for My Project',
      social: [
        { icon: 'github', label: 'GitHub', href: 'https://github.com/org/repo' },
      ],
      editLink: {
        baseUrl: 'https://github.com/org/repo/edit/main/',
      },
      lastUpdated: true,
      sidebar: [
        { label: 'Overview', slug: 'index' },
        { label: 'Guides', autogenerate: { directory: 'guides' } },
        { label: 'Reference', autogenerate: { directory: 'reference' } },
      ],
    }),
  ],
});
```

## Deployment Quick Reference

### GitHub Pages

1. Set `site` (and `base` if not using a custom domain) in `astro.config.mjs`
2. Add the workflow file below
3. Enable GitHub Pages → Source: "GitHub Actions" in repo settings
4. Push to `main`

```yaml
# .github/workflows/deploy.yml
name: Deploy to GitHub Pages
on:
  push:
    branches: [main]
permissions:
  contents: read
  pages: write
  id-token: write
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v5
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

### Cloudflare Pages

No adapter needed for static sites. Build output is `dist/`.

**Git integration (simplest):** Connect your repo in Cloudflare dashboard → Workers & Pages →
Create → Pages → Connect to Git. Framework preset: "Astro", build command: `npm run build`,
output directory: `dist`.

**Wrangler CLI:**

```bash
npm run build
npx wrangler pages deploy dist --project-name my-docs
```

For CI workflows, custom domains, and advanced setup, read the deployment reference files.

## Using MDX and Built-in Components

Install MDX support for using components inside docs:

```bash
npx astro add mdx
```

Rename `.md` files to `.mdx` where you need components:

```mdx
---
title: Installation Guide
---

import { Tabs, TabItem } from '@astrojs/starlight/components';

<Tabs>
  <TabItem label="npm">`npm install my-package`</TabItem>
  <TabItem label="pnpm">`pnpm add my-package`</TabItem>
  <TabItem label="yarn">`yarn add my-package`</TabItem>
</Tabs>
```

Starlight ships these components — use them before building custom ones:

| Component           | Purpose                                              |
|---------------------|------------------------------------------------------|
| `Tabs` / `TabItem`  | Tabbed content (install commands, platform variants) |
| `Card` / `CardGrid` | Highlighted content cards                            |
| `LinkCard`          | Card with a link                                     |
| `Aside`             | Callout boxes (note, tip, caution, danger)           |
| `Steps`             | Numbered step-by-step instructions                   |
| `FileTree`          | Directory structure visualization                    |
| `Badge`             | Inline status badges                                 |
| `Icon`              | Iconography                                          |
| `Code`              | Enhanced code blocks                                 |

You can also use Asides without MDX, using the `:::` syntax in regular Markdown:

```markdown
:::note
This is a note callout.
:::

:::tip
Helpful tip here.
:::

:::caution
Watch out for this.
:::
```

## Built-in Search

Starlight includes Pagefind — a fast, full-text search engine that indexes your built site
automatically. Zero configuration. Users access it with `Ctrl+K` / `Cmd+K`.

## Reference Files

Read these when you need depth. The skill body above covers 90% of use cases.

| Reference                                   | Read when...                                                                 |
|---------------------------------------------|------------------------------------------------------------------------------|
| `references/configuration.md`               | You need the full list of config options or frontmatter fields               |
| `references/customization.md`               | The user wants custom colors, fonts, CSS, or component overrides             |
| `references/deployment-github-pages.md`     | GitHub Pages setup needs custom domains, monorepo config, or troubleshooting |
| `references/deployment-cloudflare-pages.md` | Cloudflare Pages setup, Wrangler CLI, CI pipelines, or custom domains        |
| `references/i18n.md`                        | The site needs multiple languages                                            |
| `references/versioned-docs.md`              | The project needs versioned documentation                                    |
| `references/migration-vuepress.md`          | Migrating from VuePress 1.x or 2.x                                           |
| `references/migration-other.md`             | Migrating from Docusaurus, MkDocs, GitBook, or other tools                   |
