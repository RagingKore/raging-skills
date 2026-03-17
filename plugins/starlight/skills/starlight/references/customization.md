# Starlight Customization

How to customize Starlight's appearance and behavior beyond the defaults.

## Custom CSS

The simplest way to customize Starlight. Add CSS files via the `customCss` config:

```javascript
// astro.config.mjs
starlight({
  customCss: ['./src/styles/custom.css'],
})
```

### Theme colors

Starlight uses CSS custom properties for theming. Override them to match your brand:

```css
/* src/styles/custom.css */
:root {
  --sl-color-accent-low: #1a1a2e;
  --sl-color-accent: #4361ee;
  --sl-color-accent-high: #b8c0ff;
  --sl-color-white: #ffffff;
  --sl-color-gray-1: #eceef2;
  --sl-color-gray-2: #c0c2c7;
  --sl-color-gray-3: #888b96;
  --sl-color-gray-4: #545861;
  --sl-color-gray-5: #353841;
  --sl-color-gray-6: #24272f;
  --sl-color-black: #17181c;
}

/* Dark mode uses the same properties — Starlight swaps them automatically.
   To customize dark mode specifically: */
:root[data-theme='dark'] {
  --sl-color-accent-low: #0d1b2a;
  --sl-color-accent: #7b9ef0;
  --sl-color-accent-high: #c8d6f5;
}
```

### Typography

```css
:root {
  /* Font families */
  --sl-font: 'Inter', system-ui, sans-serif;
  --sl-font-mono: 'JetBrains Mono', monospace;

  /* Font sizes — Starlight uses a fluid type scale */
  --sl-text-base: 1rem;
  --sl-text-sm: 0.875rem;
  --sl-text-lg: 1.125rem;
  --sl-text-xl: 1.25rem;
  --sl-text-2xl: 1.5rem;
  --sl-text-3xl: 1.875rem;
  --sl-text-4xl: 2.25rem;

  /* Line heights */
  --sl-line-height: 1.8;
  --sl-line-height-headings: 1.2;
}
```

To use custom web fonts, import them in your CSS or add a `<link>` via the `head` config:

```javascript
starlight({
  head: [
    {
      tag: 'link',
      attrs: {
        rel: 'preconnect',
        href: 'https://fonts.googleapis.com',
      },
    },
    {
      tag: 'link',
      attrs: {
        rel: 'stylesheet',
        href: 'https://fonts.googleapis.com/css2?family=Inter:wght@400;600;700&display=swap',
      },
    },
  ],
  customCss: ['./src/styles/custom.css'],
})
```

### Layout adjustments

```css
/* Wider content area */
:root {
  --sl-content-width: 50rem;   /* default: 45rem */
  --sl-sidebar-width: 18rem;   /* default: 16rem */
}

/* Hide the right sidebar (TOC) globally */
.right-sidebar {
  display: none;
}

main {
  --sl-content-width: 100%;
}
```

## Component Overrides

Starlight lets you replace any built-in component with your own. This is the most powerful
customization mechanism — use it when CSS alone isn't enough.

### How it works

Map a Starlight component name to your custom component file:

```javascript
// astro.config.mjs
starlight({
  components: {
    // Replace the default Header
    Header: './src/components/CustomHeader.astro',
    // Replace the footer
    Footer: './src/components/CustomFooter.astro',
  },
})
```

### Available override slots

| Component | Description |
|---|---|
| `Head` | `<head>` contents |
| `Header` | Top navigation bar |
| `SiteTitle` | Site title in the header |
| `SocialIcons` | Social links in the header |
| `Search` | Search component |
| `Sidebar` | Full sidebar |
| `PageSidebar` | Right sidebar (TOC area) |
| `TableOfContents` | Table of contents |
| `MobileTableOfContents` | Mobile TOC |
| `Pagination` | Prev/next links |
| `Footer` | Page footer |
| `Hero` | Hero section on splash pages |
| `ContentPanel` | Main content wrapper |
| `PageTitle` | `<h1>` on each page |
| `EditLink` | "Edit this page" link |
| `LastUpdated` | Last updated timestamp |
| `Banner` | Page banner |
| `ThemeSelect` | Light/dark mode toggle |
| `LanguageSelect` | Language picker (i18n) |

### Writing a custom component

Your component receives the same props as the default. Use Starlight's built-in component
as a starting point, then modify:

```astro
---
// src/components/CustomFooter.astro
// Access page data from Astro.props if needed
const { slug } = Astro.props;
---

<footer class="custom-footer">
  <p>&copy; {new Date().getFullYear()} My Company. All rights reserved.</p>
  <nav>
    <a href="/privacy">Privacy</a>
    <a href="/terms">Terms</a>
  </nav>
</footer>

<style>
  .custom-footer {
    display: flex;
    justify-content: space-between;
    padding: 1rem var(--sl-content-pad-x);
    border-top: 1px solid var(--sl-color-gray-5);
  }
  .custom-footer a {
    color: var(--sl-color-gray-3);
  }
</style>
```

### Wrapping (extending) default components

Instead of replacing a component entirely, you can wrap it to add extra content:

```astro
---
// src/components/ExtendedFooter.astro
import Default from '@astrojs/starlight/components/Footer.astro';
---

<Default {...Astro.props}>
  <slot />
</Default>
<div class="extra-footer">
  <p>Extra content below the default footer</p>
</div>
```

## Custom Pages

You can add non-documentation pages (e.g., a blog, changelog) alongside your Starlight docs
by creating `.astro` files in `src/pages/`:

```astro
---
// src/pages/changelog.astro
import StarlightPage from '@astrojs/starlight/components/StarlightPage.astro';
---

<StarlightPage
  frontmatter={{
    title: 'Changelog',
    description: 'Release history',
    tableOfContents: false,
  }}
>
  <h2>v2.0.0</h2>
  <p>Major release with breaking changes...</p>
</StarlightPage>
```

`StarlightPage` gives you the full Starlight layout (sidebar, header, search) for pages
that aren't part of the content collection.

## Tailwind CSS

Starlight has an official Tailwind plugin:

```bash
npx astro add tailwind @astrojs/starlight-tailwind
```

```javascript
// astro.config.mjs
import starlight from '@astrojs/starlight';
import tailwind from '@astrojs/tailwind';

export default defineConfig({
  integrations: [
    starlight({ title: 'My Docs' }),
    tailwind({ applyBaseStyles: false }),
  ],
});
```

The Starlight Tailwind plugin preserves Starlight's default styles while making
Tailwind utilities available in your custom components and MDX content.

## Plugin Ecosystem

Starlight supports a plugin API for extending functionality. Plugins are added to the `plugins` array
in the Starlight config.

```typescript
import starlightBlog from 'starlight-blog';

starlight({
  plugins: [starlightBlog()],
});
```

Notable community plugins:

| Plugin                      | Purpose                                                   |
|-----------------------------|-----------------------------------------------------------|
| `starlight-blog`            | Add a blog section to a Starlight docs site               |
| `starlight-links-validator` | Check for broken internal links at build time             |
| `starlight-typedoc`         | Auto-generate API reference pages from TypeScript sources |
| `starlight-versions`        | Multi-version documentation support                       |
| `starlight-image-zoom`      | Add click-to-zoom to documentation images                 |
| `starlight-sidebar-topics`  | Group sidebar items into separate topic areas             |

Install plugins as npm packages and reference them in the config.
