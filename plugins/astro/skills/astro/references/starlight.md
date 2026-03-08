# Starlight Documentation Framework

## Contents

- [Setup](#setup)
- [Configuration](#configuration)
- [Content Collections](#content-collections)
- [Frontmatter](#frontmatter)
- [Built-in Components](#built-in-components)
- [Expressive Code](#expressive-code)
- [Search](#search)
- [Internationalization](#internationalization)
- [CSS Customization](#css-customization)
- [Component Overrides](#component-overrides)
- [Versioning](#versioning)
- [Plugin Ecosystem](#plugin-ecosystem)
- [Extending Starlight](#extending-starlight)

## Setup

Create a new project from the Starlight template.

```bash
npm create astro@latest -- --template starlight
cd my-docs
npm run dev
```

To add Starlight to an existing Astro project, install and configure the integration manually in `astro.config.mjs`.

```bash
npx astro add starlight
```

## Configuration

Configure Starlight through the integration options in `astro.config.mjs`. The integration accepts sidebar definitions,
social links, edit links, locales, custom CSS, and more.

```typescript
// astro.config.mjs
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';

export default defineConfig({
  site: 'https://example.com',
  integrations: [
    starlight({
      title: 'My Docs',
      logo: {
        src: './src/assets/logo.svg',
        replacesTitle: true,
      },
      social: [
        { icon: 'github', label: 'GitHub', href: 'https://github.com/org/repo' },
      ],
      editLink: {
        baseUrl: 'https://github.com/org/repo/edit/main/docs/',
      },
      sidebar: [
        { label: 'Home', slug: '' },
        {
          label: 'Guides',
          items: [
            { label: 'Getting Started', slug: 'guides/getting-started' },
            { label: 'Advanced', slug: 'guides/advanced' },
          ],
        },
        {
          label: 'Reference',
          autogenerate: { directory: 'reference' },
        },
        {
          label: 'Resources',
          items: [
            { label: 'FAQ', slug: 'resources/faq', badge: 'New' },
            {
              label: 'External Link',
              link: 'https://example.com',
              attrs: { target: '_blank' },
            },
          ],
        },
      ],
      customCss: ['./src/styles/custom.css'],
    }),
  ],
});
```

### Sidebar

Sidebar items support several forms.

| Form              | Description                                              |
| ----------------- | -------------------------------------------------------- |
| `slug`            | Internal link to a doc page by its content collection id |
| `link`            | External or absolute URL                                 |
| `items`           | Nested group of sidebar items                            |
| `autogenerate`    | Auto-populate from a directory of docs                   |
| `badge`           | Display a label; accepts a string or `{ text, variant }` |
| `collapsed: true` | Start a group collapsed by default                       |

Badge variants: `note`, `tip`, `caution`, `danger`, `success`, or `default`.

## Content Collections

Starlight uses its own content collection loader and schema helpers. The config file must be `src/content.config.ts`
(not `src/content/config.ts`).

```typescript
// src/content.config.ts
import { defineCollection, z } from 'astro:content';
import { docsLoader, i18nLoader } from '@astrojs/starlight/loaders';
import { docsSchema, i18nSchema } from '@astrojs/starlight/schema';

export const collections = {
  docs: defineCollection({ loader: docsLoader(), schema: docsSchema() }),
  i18n: defineCollection({ loader: i18nLoader(), schema: i18nSchema() }),
};
```

You can extend the schema to add custom frontmatter fields.

```typescript
docs: defineCollection({
  loader: docsLoader(),
  schema: docsSchema({
    extend: z.object({
      category: z.enum(['tutorial', 'guide', 'reference']).optional(),
    }),
  }),
}),
```

Content lives in `src/content/docs/`. Markdown, MDX, and Markdoc files are all supported.

## Frontmatter

Docs pages support the following frontmatter properties.

| Property      | Type                          | Description                                          |
| ------------- | ----------------------------- | ---------------------------------------------------- |
| `title`       | `string` (required)           | Page title                                           |
| `description` | `string`                      | Page meta description                                |
| `template`    | `'doc' \| 'splash'`          | Layout template; `splash` hides sidebars             |
| `sidebar`     | `object`                      | Override label, order, badge, or hidden in sidebar    |
| `editUrl`     | `string \| false`             | Override or disable the edit link for this page       |
| `head`        | `HeadConfig[]`                | Add custom tags to the page `<head>`                 |
| `hero`        | `object`                      | Hero section with title, tagline, image, actions      |
| `banner`      | `{ content: string }`         | Banner displayed at the top of the page              |
| `pagefind`    | `boolean`                     | Include or exclude page from search index            |
| `draft`       | `boolean`                     | Mark page as draft; excluded from production builds  |

Sidebar frontmatter example:

```yaml
---
title: My Page
sidebar:
  label: Custom Label
  order: 2
  badge:
    text: Beta
    variant: caution
  hidden: false
---
```

## Built-in Components

All built-in components are imported from `@astrojs/starlight/components`.

```typescript
import { Tabs, TabItem, Card, CardGrid, LinkCard, Aside, Steps, FileTree, Icon, Badge } from '@astrojs/starlight/components';
```

### Tabs and TabItem

Group content into switchable tabs. Use `syncKey` to synchronize tab selection across multiple `<Tabs>` blocks on
the same page. Each `<TabItem>` accepts a `label` and an optional `icon`.

```mdx
import { Tabs, TabItem } from '@astrojs/starlight/components';

<Tabs syncKey="pkg">
  <TabItem label="npm" icon="seti:npm">

    ```bash
    npm install my-package
    ```

  </TabItem>
  <TabItem label="pnpm" icon="seti:pnpm">

    ```bash
    pnpm add my-package
    ```

  </TabItem>
</Tabs>
```

### Card and CardGrid

Display content in styled boxes. Cards accept `title` and an optional `icon`. Wrap multiple cards in `<CardGrid>`
for a side-by-side layout. Add the `stagger` prop for a staggered entrance animation.

```mdx
import { Card, CardGrid } from '@astrojs/starlight/components';

<CardGrid stagger>
  <Card title="Feature One" icon="rocket">
    Description of the first feature.
  </Card>
  <Card title="Feature Two" icon="document">
    Description of the second feature.
  </Card>
</CardGrid>
```

### LinkCard

Clickable link cards for prominent navigation. Combine with `<CardGrid>` for multi-column layout.

```mdx
import { LinkCard, CardGrid } from '@astrojs/starlight/components';

<CardGrid>
  <LinkCard title="Getting Started" href="/guides/getting-started/" description="Learn the basics." />
  <LinkCard title="API Reference" href="/reference/api/" description="Explore the full API." />
</CardGrid>
```

### Aside

Callout blocks for supplementary information. Types: `note` (default, blue), `tip` (purple), `caution` (yellow),
`danger` (red). Accepts optional `title` and `icon` props.

```mdx
import { Aside } from '@astrojs/starlight/components';

<Aside type="tip" title="Pro tip">
  Use the `syncKey` prop on Tabs to keep selections in sync.
</Aside>
```

Markdown shorthand syntax is also available (no import needed).

```markdown
:::note
This is a note.
:::

:::tip[Did you know?]
You can add a custom title in square brackets.
:::

:::caution
Proceed with care.
:::

:::danger
This action is irreversible.
:::
```

### Steps

Style an ordered list as a step-by-step guide with visual connectors and numbered circles. Wrap a standard
Markdown ordered list inside `<Steps>`.

```mdx
import { Steps } from '@astrojs/starlight/components';

<Steps>

1. Install the package:

   ```bash
   npm install @astrojs/starlight
   ```

2. Add the integration to `astro.config.mjs`.

3. Create your first doc page in `src/content/docs/`.

</Steps>
```

### FileTree

Render visual directory structures. Use a Markdown unordered list inside `<FileTree>`. Bold a filename to
highlight it. Add a trailing `/` to mark directories. Use `...` as a placeholder for omitted content.

```mdx
import { FileTree } from '@astrojs/starlight/components';

<FileTree>
- astro.config.mjs
- package.json
- src/
  - content/
    - config.ts
    - docs/
      - **index.mdx**
      - guides/
        - ...
  - styles/
    - custom.css
</FileTree>
```

### Icon

Render one of Starlight's built-in icons inline. Common names: `open-book`, `rocket`, `pencil`, `document`,
`star`, `warning`, `error`, `information`, `heart`.

```mdx
import { Icon } from '@astrojs/starlight/components';

<Icon name="rocket" size="1.5rem" />
```

### Badge

Inline status label. Variants: `note`, `tip`, `caution`, `danger`, `success`, `default`.

```mdx
import { Badge } from '@astrojs/starlight/components';

<Badge text="New" variant="tip" />
<Badge text="Deprecated" variant="danger" />
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

Configure Expressive Code via the `expressiveCode` option in Starlight config.

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

## Search

Pagefind is built in and requires zero configuration. Content is automatically indexed at build time, and a search
modal is available in the site header.

Customization options:

- Exclude a page from the index by setting `pagefind: false` in its frontmatter.
- Fine-tune indexing with `data-pagefind-body`, `data-pagefind-ignore`, or `data-pagefind-weight` HTML attributes.
- Translate Pagefind UI strings via the `i18n` collection using `pagefind.*` keys.

Search only works in production builds. Run `npm run build && npm run preview` to test locally.

## Internationalization

Starlight ships with translated UI strings for 30+ languages and provides built-in routing for multilingual sites.

### Locale Configuration

```typescript
// astro.config.mjs
starlight({
  title: 'My Docs',
  defaultLocale: 'en',
  locales: {
    en: { label: 'English' },
    fr: { label: 'Francais', lang: 'fr' },
    ar: { label: 'Arabic', dir: 'rtl' },
  },
});
```

Content for each locale lives in a corresponding subdirectory: `src/content/docs/en/`, `src/content/docs/fr/`, etc.
Pages are matched across locales by file name.

### Root Locale

Serve the default language without a URL prefix by using `root` as the key.

```typescript
locales: {
  root: { label: 'English', lang: 'en' },
  fr: { label: 'Francais', lang: 'fr' },
},
```

With a root locale, English pages live directly in `src/content/docs/` instead of `src/content/docs/en/`.

### Custom UI Translations

Override or add UI strings by creating JSON files in `src/content/i18n/`. The `i18n` collection must be configured
in `src/content.config.ts` (see Content Collections section above).

```json
// src/content/i18n/fr.json
{
  "search.label": "Rechercher",
  "tableOfContents.onThisPage": "Sur cette page",
  "aside.tip": "Astuce"
}
```

Expressive Code and Pagefind strings are also overridable in the same file using `expressiveCode.*` and
`pagefind.*` keys.

## CSS Customization

Add custom styles via the `customCss` array in the Starlight config.

```typescript
starlight({
  customCss: ['./src/styles/custom.css'],
});
```

Override CSS custom properties on `:root` to change colors, fonts, and spacing globally.

```css
/* src/styles/custom.css */
:root {
  --sl-color-accent-low: #1a1a2e;
  --sl-color-accent: #4f46e5;
  --sl-color-accent-high: #c7d2fe;
  --sl-color-text: #e2e8f0;
  --sl-color-text-accent: #818cf8;
  --sl-font: 'Inter', sans-serif;
  --sl-font-mono: 'Fira Code', monospace;
  --sl-content-width: 50rem;
  --sl-text-5xl: 3.5rem;
}
```

Key custom properties:

| Property                  | Controls                                 |
| ------------------------- | ---------------------------------------- |
| `--sl-color-accent`       | Links, active sidebar items, highlights  |
| `--sl-color-accent-low`   | Low-contrast accent background           |
| `--sl-color-accent-high`  | High-contrast accent foreground          |
| `--sl-color-text`         | Body text color                          |
| `--sl-color-text-accent`  | Accent-tinted text                       |
| `--sl-font`               | Body and UI font family                  |
| `--sl-font-mono`          | Code font family                         |
| `--sl-content-width`      | Maximum width of the main content area   |

Starlight also supports Tailwind CSS. Install `@astrojs/starlight-tailwind` and configure it in
`src/styles/global.css` using Tailwind `@theme` directives.

## Component Overrides

Replace any built-in UI component by mapping its name to your custom component in the `components` config.

```typescript
// astro.config.mjs
starlight({
  components: {
    SocialIcons: './src/components/CustomSocial.astro',
    Footer: './src/components/CustomFooter.astro',
    Head: './src/components/CustomHead.astro',
  },
});
```

Common override targets: `Header`, `Sidebar`, `PageTitle`, `Footer`, `Head`, `Hero`, `SocialIcons`,
`ThemeSelect`, `Search`, `PageFrame`, `DraftContentNotice`.

Your replacement component receives the same props as the original. Import and re-export the original component
to wrap rather than fully replace it.

```astro
---
// src/components/CustomFooter.astro
import type { Props } from '@astrojs/starlight/props';
import Default from '@astrojs/starlight/components/Footer.astro';
---
<Default {...Astro.props}><slot /></Default>
<p class="custom-footer">Custom footer content</p>
```

See the full list of overridable components in the Overrides Reference.

## Versioning

Use the `starlight-versions` community plugin to maintain multiple doc versions.

```bash
npm install starlight-versions
```

```typescript
// astro.config.mjs
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';
import starlightVersions from 'starlight-versions';

export default defineConfig({
  integrations: [
    starlight({
      title: 'My Docs',
      plugins: [
        starlightVersions({
          versions: [
            { slug: '2.0' },
            { slug: '1.0', label: 'v1.0 (Legacy)' },
          ],
        }),
      ],
    }),
  ],
});
```

Versioned content is stored in subdirectories. Current (unversioned) docs remain in `src/content/docs/`.

## Plugin Ecosystem

Starlight supports a plugin API for extending functionality. Plugins are added to the `plugins` array in the
Starlight config.

Notable community plugins:

| Plugin                       | Purpose                                                    |
| ---------------------------- | ---------------------------------------------------------- |
| `starlight-blog`             | Add a blog section to a Starlight docs site                |
| `starlight-links-validator`  | Check for broken internal links at build time              |
| `starlight-typedoc`          | Auto-generate API reference pages from TypeScript sources  |
| `starlight-versions`         | Multi-version documentation support                        |
| `starlight-image-zoom`       | Add click-to-zoom to documentation images                  |
| `starlight-sidebar-topics`   | Group sidebar items into separate topic areas              |

Install plugins as npm packages and reference them in the config.

```typescript
import starlightBlog from 'starlight-blog';

starlight({
  plugins: [starlightBlog()],
});
```

## Extending Starlight

### Custom Pages Outside Docs

Create non-Starlight pages by adding `.astro` files to `src/pages/`. These pages use Astro's default routing and
do not inherit Starlight layout or styles. Use the `<StarlightPage>` component to opt in to the Starlight layout
from a custom page.

```astro
---
// src/pages/about.astro
import StarlightPage from '@astrojs/starlight/components/StarlightPage.astro';
---
<StarlightPage frontmatter={{ title: 'About Us' }}>
  <p>This is a custom page using the Starlight layout.</p>
</StarlightPage>
```

### Using Other Astro Integrations

Starlight is a standard Astro integration. Other integrations work alongside it without conflict.

```typescript
// astro.config.mjs
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';
import react from '@astrojs/react';
import tailwind from '@astrojs/tailwind';

export default defineConfig({
  integrations: [
    starlight({ title: 'My Docs' }),
    react(),
    tailwind({ applyBaseStyles: false }),
  ],
});
```

React, Vue, Svelte, Solid, and other framework components can be used inside MDX doc pages once the
corresponding Astro integration is installed.
