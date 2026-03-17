# Migrating from Other Documentation Tools to Starlight

Guides for migrating from Docusaurus, MkDocs, GitBook, and other documentation platforms.

## General Migration Strategy

Regardless of your source tool, the approach is the same:

1. **Create a fresh Starlight project** — don't try to convert in-place
2. **Move content** — copy Markdown files, fix frontmatter
3. **Migrate config** — sidebar, navigation, theme settings
4. **Fix tool-specific syntax** — custom components, admonitions, tabs
5. **Migrate assets** — images, downloads, static files
6. **Test and deploy**

## From Docusaurus

### File structure mapping

```
Docusaurus                      Starlight
──────────                      ─────────
docs/                           src/content/docs/
docusaurus.config.js            astro.config.mjs
src/css/custom.css              src/styles/custom.css
static/                         public/
sidebars.js                     sidebar config in astro.config.mjs
blog/ (if present)              separate — Starlight is docs-only
```

### Frontmatter

```markdown
# Docusaurus
---
id: my-page
title: My Page
sidebar_label: Custom Label
sidebar_position: 3
description: Page description
slug: /custom-slug
---

# Starlight
---
title: My Page
description: Page description
slug: custom-slug
sidebar:
  label: Custom Label
  order: 3
---
```

### Syntax differences

**Admonitions:**
```markdown
# Docusaurus
:::note
Content
:::

:::info
Content
:::

# Starlight
:::note
Content
:::

:::tip     # closest to Docusaurus "info"
Content
:::
```

Mapping: Docusaurus `note` → Starlight `note`, `tip` → `tip`, `info` → `tip`,
`caution`/`warning` → `caution`, `danger` → `danger`.

**Tabs:**
```jsx
// Docusaurus
import Tabs from '@theme/Tabs';
import TabItem from '@theme/TabItem';

<Tabs>
  <TabItem value="npm" label="npm">...</TabItem>
</Tabs>

// Starlight (MDX)
import { Tabs, TabItem } from '@astrojs/starlight/components';

<Tabs>
  <TabItem label="npm">...</TabItem>
</Tabs>
```

**Code blocks with title:**
```markdown
# Docusaurus
```js title="example.js"
// code
```

# Starlight
```js title="example.js"
// code — same syntax, works natively
```
```

### Blog content

Starlight is docs-only. If you have a Docusaurus blog, you have options:
- Add an Astro blog alongside Starlight (Starlight coexists with other Astro pages)
- Use a separate blog platform and link to it
- Use the `StarlightPage` component to create blog-like pages within Starlight

## From MkDocs / Material for MkDocs

### File structure mapping

```
MkDocs                          Starlight
──────                          ─────────
docs/                           src/content/docs/
mkdocs.yml                      astro.config.mjs
docs/assets/ or docs/img/       src/assets/ or public/
overrides/                      src/components/ (overrides)
```

### Configuration

```yaml
# mkdocs.yml
site_name: My Docs
theme:
  name: material
  palette:
    scheme: slate
nav:
  - Home: index.md
  - Guide:
    - Getting Started: guide/getting-started.md
    - Advanced: guide/advanced.md
```

```javascript
// astro.config.mjs equivalent
starlight({
  title: 'My Docs',
  sidebar: [
    { label: 'Home', slug: 'index' },
    {
      label: 'Guide',
      items: [
        { slug: 'guide/getting-started' },
        { slug: 'guide/advanced' },
      ],
    },
  ],
})
```

### Syntax differences

**Admonitions:**
```markdown
# MkDocs Material
!!! note "Custom Title"
    Admonition content.

!!! warning
    Warning content.

??? tip "Collapsible"
    Hidden by default.

# Starlight
:::note[Custom Title]
Admonition content.
:::

:::caution
Warning content.
:::

<!-- No collapsible admonitions — use <details> HTML -->
<details>
<summary>Collapsible Tip</summary>
Hidden by default.
</details>
```

**Content tabs:**
```markdown
# MkDocs Material
=== "npm"
    ```bash
    npm install
    ```

=== "pip"
    ```bash
    pip install
    ```

# Starlight (MDX)
import { Tabs, TabItem } from '@astrojs/starlight/components';

<Tabs>
  <TabItem label="npm">`npm install`</TabItem>
  <TabItem label="pip">`pip install`</TabItem>
</Tabs>
```

### Python-specific features

MkDocs plugins like `mkdocstrings` (auto-generated API docs from Python docstrings) have
no direct Starlight equivalent. Options:
- Generate Markdown from docstrings as a build step, then include in Starlight
- Use TypeDoc or similar for TypeScript/JavaScript API docs
- Write API reference pages manually

## From GitBook

### Content export

1. Export your GitBook content as Markdown (GitBook → Export)
2. The export gives you `.md` files with frontmatter

### Structure mapping

```
GitBook                         Starlight
───────                         ─────────
SUMMARY.md                      sidebar config in astro.config.mjs
.gitbook/assets/                public/ or src/assets/
README.md                       src/content/docs/index.md
group/page.md                   src/content/docs/group/page.md
```

### Key differences

- GitBook's `SUMMARY.md` defines navigation → translate to Starlight sidebar config
- GitBook hints → Starlight asides (`:::note`, `:::tip`, etc.)
- GitBook embeds → replace with Astro components or HTML in MDX
- GitBook API blocks → rewrite as code blocks or custom components

## Common Migration Tasks

### Fixing internal links

Most documentation tools use file-path links (`./page.md`), but Starlight uses URL paths:

```markdown
# Before (file-based)
[Link](./getting-started.md)
[Link](../api/reference.md#section)

# After (URL-based)
[Link](./getting-started/)
[Link](../api/reference/#section)
```

A find-and-replace for `.md)` → `/)` handles most cases, but verify links manually.

### Moving images

```
# From tool-specific locations
docs/assets/screenshot.png    →  src/assets/screenshot.png (optimized)
docs/static/logo.png          →  public/logo.png (unprocessed)
```

Images in `src/assets/` are optimized by Astro's image pipeline. Images in `public/` are
served as-is. Use `src/assets/` for content images, `public/` for favicons and OG images.

```markdown
# Referencing images in content
![Screenshot](../../assets/screenshot.png)
```

### Redirects

If your URL structure changes, set up redirects on your hosting platform:

- **GitHub Pages**: No built-in redirects (use a JS redirect in a 404 page, or use
  `astro-redirect` integration)
- **Cloudflare Pages**: Use a `public/_redirects` file
- **Netlify**: Use a `public/_redirects` file

```
# public/_redirects (Cloudflare/Netlify)
/old-path /new-path 301
/guide/intro /guides/getting-started 301
```
