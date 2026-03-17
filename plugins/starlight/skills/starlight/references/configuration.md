# Starlight Configuration Reference

Complete reference for `starlight()` integration options and page frontmatter.

## Integration Options (`astro.config.mjs`)

All options are passed to `starlight({})` inside your Astro config.

### Required

| Option | Type | Description |
|---|---|---|
| `title` | `string \| Record<string, string>` | Site title. Used in browser tab, metadata, and nav bar. For multilingual sites, pass an object keyed by BCP-47 locale. |

### Site Identity

| Option | Type | Default | Description |
|---|---|---|---|
| `description` | `string` | — | Site description for `<meta name="description">`. Overridden by page frontmatter. |
| `logo` | `LogoConfig` | — | Logo image in nav bar. Supports `src` or separate `light`/`dark` sources. Set `replacesTitle: true` to hide the text title. |
| `favicon` | `string` | `'/favicon.svg'` | Path to favicon in `public/`. |

```javascript
// Logo example
starlight({
  title: 'My Docs',
  logo: {
    light: './src/assets/logo-light.svg',
    dark: './src/assets/logo-dark.svg',
    alt: 'My Project',
    replacesTitle: true,
  },
  favicon: '/favicon.png',
})
```

### Navigation & Sidebar

| Option | Type | Default | Description |
|---|---|---|---|
| `sidebar` | `SidebarItem[]` | Auto from file structure | Sidebar navigation items. See SKILL.md sidebar section. |
| `tableOfContents` | `false \| { min, max }` | `{ minHeadingLevel: 2, maxHeadingLevel: 3 }` | Right-side TOC config. Set `false` to disable globally. |
| `pagination` | `boolean` | `true` | Show prev/next links at page bottom. |

### Social & Links

| Option | Type | Description |
|---|---|---|
| `social` | `SocialLink[]` | Social media links in the header. Each item has `icon`, `label`, and `href`. |
| `editLink` | `{ baseUrl: string }` | Enable "Edit this page" links. The final URL is `baseUrl + page path`. |

Available social icons: `github`, `gitlab`, `bitbucket`, `discord`, `slack`, `twitter`, `x.com`,
`mastodon`, `threads`, `youtube`, `twitch`, `linkedin`, `reddit`, `stackOverflow`, `telegram`,
`rss`, and more.

```javascript
starlight({
  social: [
    { icon: 'github', label: 'GitHub', href: 'https://github.com/org/repo' },
    { icon: 'discord', label: 'Discord', href: 'https://discord.gg/invite' },
  ],
  editLink: {
    baseUrl: 'https://github.com/org/repo/edit/main/docs/',
  },
})
```

### Content & Features

| Option | Type | Default | Description |
|---|---|---|---|
| `lastUpdated` | `boolean` | `false` | Show last-updated date on pages (from git history). |
| `customCss` | `string[]` | `[]` | CSS files to apply globally. Paths relative to project root. |
| `components` | `Record<string, string>` | `{}` | Override built-in components. See `customization.md`. |
| `credits` | `boolean` | `false` | Show "Built with Starlight" in the footer. |
| `head` | `HeadConfig[]` | `[]` | Custom tags to add to `<head>` on every page. |
| `disable404Route` | `boolean` | `false` | Disable Starlight's default 404 page. |

```javascript
// Custom head tags example
starlight({
  head: [
    { tag: 'script', attrs: { src: '/analytics.js', defer: true } },
    { tag: 'meta', attrs: { property: 'og:image', content: '/og.png' } },
  ],
})
```

### Sidebar Item Types

```typescript
type SidebarItem =
  | { label: string; link: string; badge?: BadgeConfig; attrs?: Record<string, string> }
  | { label?: string; slug: string; badge?: BadgeConfig; attrs?: Record<string, string> }
  | { label: string; items: SidebarItem[]; collapsed?: boolean; badge?: BadgeConfig }
  | { label: string; autogenerate: { directory: string; collapsed?: boolean }; collapsed?: boolean; badge?: BadgeConfig };
```

Shorthand: internal links can be strings instead of objects with `slug`:
```javascript
items: ['guides/intro', 'guides/setup']  // same as [{ slug: 'guides/intro' }, ...]
```

## Page Frontmatter

### Required

| Field | Type | Description |
|---|---|---|
| `title` | `string` | Page title. Becomes the `<h1>` and browser tab title. |

### Optional

| Field | Type | Default | Description |
|---|---|---|---|
| `description` | `string` | — | Page meta description for SEO. |
| `slug` | `string` | Derived from file path | Override the URL slug for this page. |
| `sidebar` | `SidebarConfig` | — | Control how this page appears in the sidebar. |
| `tableOfContents` | `false \| { min, max }` | Site default | Override TOC for this page. |
| `template` | `'doc' \| 'splash'` | `'doc'` | Page template. Use `splash` for landing pages (no sidebar). |
| `hero` | `HeroConfig` | — | Hero section at the top of the page. |
| `lastUpdated` | `Date \| boolean` | Site default | Override last-updated behavior. |
| `editUrl` | `string \| false` | Auto from `editLink` | Override or disable edit link for this page. |
| `head` | `HeadConfig[]` | `[]` | Extra `<head>` tags for this page only. |
| `pagefind` | `boolean` | `true` | Include this page in search results. |
| `draft` | `boolean` | `false` | Exclude from production builds. Visible in dev only. |
| `banner` | `{ content: string }` | — | Display a banner at the top of this page. |
| `prev` | `false \| string \| { label, link }` | Auto | Override or disable the previous page link. |
| `next` | `false \| string \| { label, link }` | Auto | Override or disable the next page link. |

### Sidebar frontmatter

```markdown
---
title: My Page
sidebar:
  label: Custom Label    # Override the sidebar text
  order: 5               # Sort order (lower = higher)
  hidden: false          # Hide from sidebar but keep the page accessible
  badge:
    text: New
    variant: tip         # note | tip | caution | danger | default
---
```

### Hero frontmatter

```markdown
---
title: Welcome
template: splash
hero:
  title: My Documentation
  tagline: Fast, accessible, beautiful docs.
  image:
    file: ../../assets/hero.png
    alt: Hero illustration
  actions:
    - text: Get Started
      link: /guides/getting-started/
      icon: right-arrow
      variant: primary
    - text: View on GitHub
      link: https://github.com/org/repo
      icon: external
      variant: minimal
---
```

## Content Collection Config

### Standard setup

```typescript
// src/content.config.ts
import { defineCollection } from 'astro:content';
import { docsLoader } from '@astrojs/starlight/loaders';
import { docsSchema } from '@astrojs/starlight/schema';

export const collections = {
  docs: defineCollection({ loader: docsLoader(), schema: docsSchema() }),
};
```

### Extended schema (custom frontmatter fields)

```typescript
import { defineCollection, z } from 'astro:content';
import { docsLoader } from '@astrojs/starlight/loaders';
import { docsSchema } from '@astrojs/starlight/schema';

export const collections = {
  docs: defineCollection({
    loader: docsLoader(),
    schema: docsSchema({
      extend: z.object({
        // Add custom frontmatter fields
        category: z.enum(['tutorial', 'reference', 'guide']).optional(),
        difficulty: z.enum(['beginner', 'intermediate', 'advanced']).optional(),
      }),
    }),
  }),
};
```

### With i18n

```typescript
import { defineCollection } from 'astro:content';
import { docsLoader, i18nLoader } from '@astrojs/starlight/loaders';
import { docsSchema, i18nSchema } from '@astrojs/starlight/schema';

export const collections = {
  docs: defineCollection({ loader: docsLoader(), schema: docsSchema() }),
  i18n: defineCollection({ loader: i18nLoader(), schema: i18nSchema() }),
};
```

## Expressive Code

Starlight includes Expressive Code for advanced code blocks. No separate install is required.

Supported features:

- File name tabs via the `title` meta attribute
- Copy button (enabled by default)
- Line highlighting with `{1,3-5}` syntax
- Word/text markers with `"search term"` or `/regex/` syntax
- Diff highlighting with `ins={1-2}` and `del={3}`
- Terminal window frames via `frame="terminal"`
- Collapsible sections

Example with title and line markers:

````markdown
```js title="src/example.js" {2} ins={4} del={5}
const config = {
  title: 'My Docs',       // highlighted
  description: 'A guide',
  feature: true,           // inserted
  legacy: false,           // deleted
};
```
````

Configure Expressive Code via the `expressiveCode` option in Starlight config:

```javascript
starlight({
  expressiveCode: {
    themes: ['dracula', 'github-light'],
    useStarlightDarkModeSwitch: true,
    useStarlightUiThemeColors: true,
    styleOverrides: {
      borderRadius: '0.5rem',
      codeFontFamily: 'Fira Code, monospace',
    },
  },
});
```

Set `expressiveCode: false` to disable it entirely if you need a different code highlighter.

## Search (Pagefind)

Pagefind is built in and requires zero configuration. Content is automatically indexed at build time,
and a search modal is available in the site header.

Customization options:

- Exclude a page from the index by setting `pagefind: false` in its frontmatter.
- Fine-tune indexing with HTML attributes on any element:
  - `data-pagefind-body` — only index content inside this element
  - `data-pagefind-ignore` — exclude an element from the index
  - `data-pagefind-weight` — boost or lower ranking weight for an element
- Translate Pagefind UI strings via the `i18n` collection using `pagefind.*` keys.

Search only works in production builds. Run `npm run build && npm run preview` to test locally.
