# Migrating from VuePress to Starlight

This guide covers migrating from VuePress 1.x and VuePress 2.x to Starlight.

## Why Migrate

- **Performance**: Starlight sites are static HTML with near-zero JavaScript by default.
  VuePress ships the Vue runtime to every page.
- **Modern tooling**: Astro's build pipeline is significantly faster than VuePress/Webpack.
- **Active development**: Starlight is actively maintained by the Astro team.
  VuePress 1.x is in maintenance mode; VuePress 2.x development has slowed.
- **Built-in features**: Starlight includes Pagefind search, dark mode, and accessibility
  out of the box — features that require plugins in VuePress.

## Migration Checklist

1. **Don't migrate in-place.** Create a fresh Starlight project alongside your VuePress site.
2. Copy content files (Markdown)
3. Rename `README.md` files to `index.md`:
   ```bash
   find src/content/docs -name "README.md" -exec sh -c \
     'mv "$1" "$(dirname "$1")/index.md"' _ {} \;
   ```
4. Migrate configuration (sidebar, nav, theme)
5. Migrate custom components (Vue → Astro)
6. Update frontmatter (see conversion table below)
7. Replace VuePress-specific syntax (custom containers, code groups)
8. Move static assets: `.vuepress/public/` → `public/`
9. Update internal links — VuePress auto-resolves `.md` extensions, Starlight uses URL paths
10. Test: `npm run dev` and click through every page. Run `npx astro check`.
11. Update CI/CD deploy paths — VuePress outputs to `docs/.vuepress/dist/`, Astro outputs to `dist/`

## Content Migration

### File structure mapping

```
VuePress                        Starlight
─────────                       ─────────
docs/                           src/content/docs/
docs/.vuepress/                 astro.config.mjs + src/
docs/.vuepress/config.js        astro.config.mjs
docs/.vuepress/styles/          src/styles/
docs/.vuepress/components/      src/components/
docs/.vuepress/public/          public/
docs/README.md                  src/content/docs/index.mdx
docs/guide/README.md            src/content/docs/guide/index.md
```

### Moving Markdown files

1. Copy all `.md` files from `docs/` to `src/content/docs/`
2. Rename any `README.md` files to `index.md` (Starlight uses `index` for directory pages)
3. Remove VuePress-specific syntax (see below)

### Frontmatter differences

```markdown
# VuePress
---
title: My Page
sidebar: auto
prev: ./getting-started
next: ./advanced
meta:
  - name: description
    content: Page description
---

# Starlight
---
title: My Page
description: Page description
prev: Getting Started
next: Advanced Guide
---
```

Full frontmatter conversion table:

| VuePress Frontmatter | Starlight Frontmatter |
|----------------------|----------------------|
| `title: 'Page Title'` | `title: 'Page Title'` (same) |
| `description: '...'` | `description: '...'` (same) |
| `sidebar: auto` | Not needed — Starlight auto-generates by default |
| `sidebar: false` | `sidebar: { hidden: true }` |
| `sidebarDepth: 2` | `tableOfContents: { minHeadingLevel: 2, maxHeadingLevel: 3 }` |
| `meta` | Use `description` directly |
| `prev: './foo'` | `prev: 'Label'` or `prev: { label: '...', link: '/guide/foo' }` |
| `next: './bar'` | `next: 'Label'` or `next: { label: '...', link: '/guide/bar' }` |
| `editLink: false` | `editUrl: false` |
| `lastUpdated: false` | `lastUpdated: false` |
| `layout: 'LayoutName'` | `template: 'doc'` or `template: 'splash'` |
| `home: true` (VuePress 1.x) | Use `template: 'splash'` with `hero` frontmatter |
| `lang: 'en-US'` | Handled by i18n config, not per-page frontmatter |
| `navbar: false` | No equivalent |

## Configuration Migration

### Sidebar

```javascript
// VuePress config.js
module.exports = {
  themeConfig: {
    sidebar: [
      { title: 'Guide', children: ['/guide/', '/guide/getting-started'] },
      { title: 'API', children: ['/api/'] },
    ],
  },
};

// Starlight astro.config.mjs
starlight({
  sidebar: [
    {
      label: 'Guide',
      items: [
        { slug: 'guide' },
        { slug: 'guide/getting-started' },
      ],
    },
    { label: 'API', autogenerate: { directory: 'api' } },
  ],
})
```

### Navigation bar

VuePress has a top nav bar with links. Starlight doesn't have a traditional nav bar with
arbitrary links — the header has the site title, search, social links, and theme toggle.

For top-level navigation, use sidebar groups or social links.

### Search

- **VuePress**: Requires Algolia DocSearch plugin or similar
- **Starlight**: Built-in Pagefind search, zero configuration

Remove any Algolia/search plugin configuration.

## Syntax Differences

### Containers / Callouts

```markdown
# VuePress
::: tip
This is a tip.
:::

::: warning
Be careful!
:::

::: danger
Don't do this!
:::

# Starlight (same syntax, different type names)
:::note
This is a note.
:::

:::tip
This is a tip.
:::

:::caution
Be careful!
:::

:::danger
Don't do this!
:::
```

Type mapping: VuePress `tip` → Starlight `tip`, VuePress `warning` → Starlight `caution`,
VuePress `danger` → Starlight `danger`. VuePress `details` → no direct equivalent (use
`<details>` HTML).

### Code groups

```markdown
# VuePress 2.x
<CodeGroup>
  <CodeGroupItem title="npm">
  ```bash
  npm install
  ```
  </CodeGroupItem>
</CodeGroup>

# Starlight (MDX)
import { Tabs, TabItem } from '@astrojs/starlight/components';

<Tabs>
  <TabItem label="npm">`npm install`</TabItem>
  <TabItem label="pnpm">`pnpm add`</TabItem>
</Tabs>
```

### Vue components in Markdown

VuePress allows using Vue components directly in Markdown. Starlight uses Astro components
(or React/Vue/Svelte via MDX). You'll need to:

1. Rename files from `.md` to `.mdx` where components are used
2. Rewrite Vue SFCs as Astro components (or keep Vue components and use them via MDX
   with `@astrojs/vue` integration)
3. Import components explicitly — Starlight doesn't auto-register components

### Links

```markdown
# VuePress (auto-resolves .md extensions)
[Link](./other-page.md)
[Link](../guide/README.md)

# Starlight (use URL paths)
[Link](./other-page/)
[Link](../guide/)
```

## Custom Theme Migration

If you have a custom VuePress theme (`docs/.vuepress/theme/`), use Starlight's component
override system instead:

1. Identify which parts of the theme you customized
2. Map them to Starlight's override slots (see `customization.md`)
3. Rewrite as Astro components

Common overrides:
- Custom header → override `Header` component
- Custom footer → override `Footer` component
- Custom sidebar → override `Sidebar` component
- Custom homepage → use `template: splash` with `hero` frontmatter

## Plugin Equivalents

| VuePress Plugin | Starlight Equivalent |
|---|---|
| `@vuepress/plugin-search` | Built-in (Pagefind) |
| Algolia DocSearch | Starlight DocSearch plugin |
| `@vuepress/plugin-back-to-top` | Not built-in (add via custom CSS/component) |
| `@vuepress/plugin-medium-zoom` | `starlight-image-zoom` community plugin |
| `vuepress-plugin-mermaid` | Use `rehype-mermaid` integration |
| `@vuepress/plugin-google-analytics` | Use `head` config to add the GA script |
| `@vuepress/plugin-pwa` | Use `head` config for manifest + service worker |

Useful community plugins for migration:
- `starlight-links-validator` — catch broken internal links at build time
- `starlight-image-zoom` — medium-zoom equivalent for images
- `starlight-blog` — add a blog section to a Starlight docs site
- `starlight-sidebar-topics` — per-section sidebar grouping

## VitePress-Specific Migration

VitePress shares VuePress's sidebar/nav configuration patterns, so the sidebar and configuration
migration sections above apply. These additional items are VitePress-specific:

### Features with no Starlight equivalent

- **`<script setup>` blocks in Markdown** — VitePress lets you write Vue `<script setup>` in
  Markdown files. Rewrite as Astro components or MDX.
- **`useData()` composable** — VitePress runtime API for accessing page/site data. Use
  `Astro.props` in components or `astro:content` queries instead.
- **Runtime API** (`useRoute()`, `useRouter()`, etc.) — no equivalent in Starlight's static
  output. Rewrite any dynamic logic as static Astro components.

### VitePress 2.x sidebar config

VitePress uses the same `themeConfig.sidebar` structure as VuePress 2.x:

```javascript
// VitePress config.mts
export default {
  themeConfig: {
    sidebar: [
      { text: 'Guide', items: [
        { text: 'Introduction', link: '/guide/' },
        { text: 'Getting Started', link: '/guide/getting-started' },
      ]},
    ]
  }
};
```

Convert using the same patterns shown in the sidebar section above.

## Gotchas

**SPA → MPA behavior:** VuePress and VitePress are SPAs with client-side navigation. Astro is
MPA — page transitions are full page loads by default. Users may notice the difference. Astro's
`<ClientRouter />` (view transitions) can partially restore SPA-like feel if needed.

**Vue 2 components (VuePress 1.x):** Astro's Vue integration uses Vue 3. If migrating from
VuePress 1.x, all Vue components must be upgraded to Vue 3 syntax before they can be used with
`@astrojs/vue`.

**No per-path sidebar:** VuePress supports different sidebars per URL prefix
(`sidebar: { '/guide/': [...], '/api/': [...] }`). Starlight has a single global sidebar. Use the
`starlight-sidebar-topics` community plugin for topic-based grouping, or organize content into
clear directory groups with `autogenerate`.

**Emoji shortcodes:** VuePress supports `:tada:` emoji shortcodes via `markdown-it-emoji`.
Astro/Starlight does not — use actual Unicode emoji characters or install `remark-emoji`.

**`:::details` collapsible:** VuePress `:::details` has no direct Starlight equivalent. Use
`<details><summary>Title</summary>Content</details>` HTML instead.

**No global component registration:** VuePress registers components globally via
`.vuepress/enhanceApp.js` or `.vuepress/client.js`. Starlight has no global component
registration — import components explicitly in each MDX file. This is intentional: it keeps
the dependency tree explicit and tree-shakeable.

**Internal link format:** VuePress auto-resolves `.md` extensions and uses directory-based URLs
with trailing slashes. Starlight uses slug-based links. Run a global find-and-replace to convert
file-path links (`.md`) to URL paths. Set `trailingSlash: 'always'` in `astro.config.mjs` to
match VuePress URL format if needed.

**Search only in production builds:** Pagefind indexes at build time. Search does not work in
`astro dev`. Always test with `npm run build && npm run preview`.
