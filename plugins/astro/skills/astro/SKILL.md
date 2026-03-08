---
name: astro
description: |
  Comprehensive guidance for building and deploying static websites with the Astro framework.
  This skill should be used when asked to "create astro site", "deploy astro to firebase",
  "deploy astro to cloudflare", "set up content collections", "add mermaid diagrams to astro",
  "configure astro i18n", "build static blog", "astro markdown setup", "create docs site",
  "starlight docs", or "astro documentation site". Covers SSG fundamentals, Content Collections,
  Markdown/MDX, partial hydration, islands architecture, Starlight documentation framework, and
  deployment to Cloudflare, Netlify, Vercel, GitHub Pages, Firebase, or AWS/GCP.
license: MIT
metadata:
  version: 2.0.0
  category: web-development
  author: Claude Code Skills
  triggers:
    - astro
    - astro website
    - astro static site
    - astro content collections
    - astro deployment
    - astro firebase
    - astro cloudflare
    - astro mermaid
    - starlight
    - starlight docs
    - astro docs site
    - astro documentation
    - build astro site
    - deploy astro
  tags:
    - astro
    - static-site-generation
    - starlight
    - markdown
    - deployment
---

# Publishing Astro Websites

Build fast, content-driven static websites with Astro's zero-runtime SSG approach, partial hydration, and extensive
Markdown support. All patterns in this skill target **Astro 5.x** with the Content Layer API.

## Contents

- [Quick Start](#quick-start)
- [When Not to Use](#when-not-to-use)
- [Project Structure](#project-structure)
- [SSG vs SSR vs Hybrid](#ssg-vs-ssr-vs-hybrid)
- [Content Collections](#content-collections)
- [Islands Architecture and Hydration](#islands-architecture-and-hydration)
- [Syntax Highlighting](#syntax-highlighting)
- [Diagrams and Search](#diagrams-and-search)
- [Starlight Documentation Framework](#starlight-documentation-framework)
- [Internationalization](#internationalization-i18n)
- [Common Patterns](#common-patterns)
- [.astro File Anatomy](#astro-file-anatomy)
- [File-Based Routing](#file-based-routing)
- [SEO Essentials](#seo-essentials)
- [Performance Best Practices](#performance-best-practices)
- [Deployment](#deployment)
- [Pre-Deploy Checklist](#pre-deploy-checklist)
- [Testing](#testing)
- [Essential Integrations](#essential-integrations)
- [Troubleshooting](#troubleshooting)
- [References](#references)

## Quick Start

```bash
# Standard Astro project (use Blog template for Markdown sites)
npm create astro@latest

# Documentation site with Starlight
npm create astro@latest -- --template starlight

# Development / Build / Preview
npm run dev          # http://localhost:4321
npm run build        # Static files in dist/
npm run preview      # Preview production build
```

## When Not to Use

This skill focuses on **static site generation (SSG)**. Consider other approaches for:

- **Real-time data applications**: Use SSR mode with database connections
- **User authentication flows**: Requires server-side session handling
- **E-commerce with dynamic inventory**: Use hybrid mode or full SSR
- **Single-page applications (SPAs)**: Consider React/Vue frameworks directly

For hybrid SSG+SSR patterns, see Astro's adapter documentation.

## Project Structure

```
src/
  components/          # Astro, React, Vue, Svelte components
  content/
    docs/              # Content collection entries (Markdown/MDX)
  layouts/             # Page wrappers with slots
  pages/               # File-based routing
src/content.config.ts  # Collection schemas and loaders
public/                # Static assets (images, fonts, favicons)
astro.config.mjs       # Framework configuration
```

## SSG vs SSR vs Hybrid

| Mode              | When Pages Render | Use Case                         |
|-------------------|-------------------|----------------------------------|
| **SSG** (default) | Build time        | Blogs, docs, marketing sites     |
| **SSR**           | Each request      | Dynamic data, personalization    |
| **Hybrid**        | Mix of both       | Static pages + dynamic endpoints |

For pure static sites, use default `output: 'static'`. No adapter needed.

## Content Collections

Astro 5.x uses the Content Layer API with `glob()` loader. The config file is `src/content.config.ts` (not
`src/content/config.ts`).

### Defining Collections

```typescript
// src/content.config.ts
import { defineCollection, z, reference } from 'astro:content';
import { glob } from 'astro/loaders';

const blog = defineCollection({
  loader: glob({ pattern: '**/*.md', base: './src/data/blog' }),
  schema: ({ image }) => z.object({
    title: z.string(),
    pubDate: z.coerce.date(),
    draft: z.boolean().default(false),
    cover: image(),
    author: reference('authors'),
  })
});

export const collections = { blog };
```

The `glob()` loader delivers up to 75% faster builds for large content sites compared to the legacy pattern.

### Advanced Schema Patterns

```typescript
schema: ({ image }) => z.object({
  cover: image(),                         // Validates image exists in src/
  category: z.enum(['tech', 'news']),
  author: reference('authors'),           // Cross-collection reference
  relatedPosts: z.array(reference('blog')).optional(),
})
```

Custom loaders can fetch content from external APIs (GitHub releases, CMS, etc.) by implementing the `Loader`
interface. See the Astro docs for the full loader API.

### Querying and Rendering

```astro
---
import { getCollection, render } from 'astro:content';

export async function getStaticPaths() {
  const docs = await getCollection('docs');
  return docs.map(doc => ({
    params: { id: doc.id },
    props: { doc }
  }));
}

const { doc } = Astro.props;
const { Content } = await render(doc);
---

<article>
  <h1>{doc.data.title}</h1>
  <Content />
</article>
```

Key differences from Astro 4.x: use `doc.id` (not `doc.slug`), call `render(doc)` (not `doc.render()`), and import
`render` from `astro:content`.

For advanced Markdown/MDX patterns, filtering, component mapping, remark/rehype plugins, and reading time, see
[markdown-deep-dive.md](references/markdown-deep-dive.md).

## Islands Architecture and Hydration

Most content remains static HTML. Use `client:*` directives only where interactivity is needed:

| Directive              | When It Hydrates                 | Use Case                        |
|------------------------|----------------------------------|---------------------------------|
| `client:load`          | Immediately on page load         | Above-the-fold interactive UI   |
| `client:idle`          | When browser is idle             | Non-critical widgets            |
| `client:visible`       | When element enters viewport     | Below-the-fold components       |
| `client:media="(...)"`  | On media query match             | Mobile-only interactions        |
| `client:only="react"`  | Client-only, skip server render  | Browser-dependent components    |

```astro
---
import Counter from '../components/Counter.jsx';
import Newsletter from '../components/Newsletter.svelte';
---

<Counter client:visible />
<Newsletter client:idle />
```

## Syntax Highlighting

Astro uses Shiki for build-time syntax highlighting with zero client JS. Supports dual light/dark themes,
line highlighting, and `@shikijs/transformers` for notation-based markers. For documentation sites, Expressive Code
adds copy buttons, filenames, and terminal frames. Starlight includes it built in.

See [markdown-deep-dive.md](references/markdown-deep-dive.md) for full configuration examples.

## Diagrams and Search

Use `rehype-mermaid` for build-time SVG diagram rendering (replaces the outdated `astro-mermaid` package). For
search, Starlight includes Pagefind built in; standalone Astro sites can use `astro-pagefind` or Fuse.js.

See [search-and-diagrams.md](references/search-and-diagrams.md) for setup, Pagefind vs Fuse.js comparison,
dark mode theming strategies, and PlantUML.

## Starlight Documentation Framework

Starlight is Astro's purpose-built documentation framework with batteries included: search (Pagefind), i18n (30+
languages), sidebar navigation, dark mode, Expressive Code, and component overrides.

```bash
npm create astro@latest -- --template starlight
```

```typescript
// astro.config.mjs
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';

export default defineConfig({
  integrations: [
    starlight({
      title: 'My Docs',
      sidebar: [
        { label: 'Guides', autogenerate: { directory: 'guides' } },
        { label: 'Reference', autogenerate: { directory: 'reference' } },
      ],
    }),
  ],
});
```

Starlight uses its own content collection helpers. The config file must be `src/content.config.ts`:

```typescript
// src/content.config.ts
import { defineCollection } from 'astro:content';
import { docsLoader } from '@astrojs/starlight/loaders';
import { docsSchema } from '@astrojs/starlight/schema';

export const collections = {
  docs: defineCollection({ loader: docsLoader(), schema: docsSchema() }),
};
```

### Built-in Components

Import from `@astrojs/starlight/components`:

| Component              | Purpose                                         |
|------------------------|-------------------------------------------------|
| `Tabs` / `TabItem`    | Grouped content tabs with `syncKey` support     |
| `Card` / `CardGrid`   | Feature cards with optional `stagger` animation |
| `Aside`               | Callout blocks (note, tip, caution, danger)     |
| `Steps`               | Numbered step-by-step instructions              |
| `FileTree`            | Visual directory structures                     |
| `Icon`                | Built-in icon set                               |
| `Badge`               | Inline status labels with color variants        |

Markdown shorthand for asides (no import needed): `:::note`, `:::tip`, `:::caution`, `:::danger`.

For full Starlight configuration, CSS customization, component overrides, versioning, i18n setup, and the plugin
ecosystem, see [starlight.md](references/starlight.md).

## Internationalization (i18n)

```javascript
// astro.config.mjs
export default defineConfig({
  i18n: {
    defaultLocale: 'en',
    locales: ['en', 'fr', 'es'],
    routing: { prefixDefaultLocale: false }
  }
});
```

Structure content by locale: `src/content/docs/en/`, `src/content/docs/fr/`, etc.

Detect locale in components:

```astro
---
const locale = Astro.currentLocale || 'en';
---
```

For Starlight sites, i18n is configured directly in the Starlight integration with built-in UI string translations.
See [starlight.md](references/starlight.md) for locale routing and custom translation overrides.

## Common Patterns

### Paginated Listings

```astro
---
// src/pages/blog/page/[page].astro
import { getCollection } from 'astro:content';
const POSTS_PER_PAGE = 10;

export async function getStaticPaths() {
  const allPosts = await getCollection('blog');
  const totalPages = Math.ceil(allPosts.length / POSTS_PER_PAGE);
  return Array.from({ length: totalPages }, (_, i) => ({ params: { page: String(i + 1) } }));
}

const pageNum = parseInt(Astro.params.page);
const allPosts = (await getCollection('blog')).sort((a, b) => b.data.pubDate.getTime() - a.data.pubDate.getTime());
const start = (pageNum - 1) * POSTS_PER_PAGE;
const posts = allPosts.slice(start, start + POSTS_PER_PAGE);
---

{posts.map(post => <article>{post.data.title}</article>)}
```

### Tag/Category Archives

```astro
---
// src/pages/tags/[tag].astro
import { getCollection } from 'astro:content';

export async function getStaticPaths() {
  const allPosts = await getCollection('blog');
  const tags = new Set();
  allPosts.forEach(post => post.data.tags?.forEach(tag => tags.add(tag)));
  return Array.from(tags).map(tag => ({ params: { tag }, props: { tag } }));
}

const { tag } = Astro.props;
const postsWithTag = (await getCollection('blog')).filter(post => post.data.tags?.includes(tag));
---

<h1>Posts tagged: {tag}</h1>
{postsWithTag.map(post => <a href={`/blog/${post.id}`}>{post.data.title}</a>)}
```

### RSS Feed

```javascript
// src/pages/rss.xml.js
import rss from '@astrojs/rss';
import { getCollection } from 'astro:content';

export async function GET(context) {
  const blog = await getCollection('blog');
  return rss({
    title: 'My Blog',
    description: 'A blog about Astro',
    site: context.site,
    items: blog.map(post => ({
      title: post.data.title,
      description: post.data.description,
      pubDate: post.data.pubDate,
      link: `/blog/${post.id}/`,
    })),
  });
}
```

For forms, JSON-LD structured data, and dark mode toggle, see
[markdown-deep-dive.md](references/markdown-deep-dive.md).

## .astro File Anatomy

```astro
---
// Frontmatter: runs at build time (server-side)
import Layout from '../layouts/Layout.astro';
import { getCollection } from 'astro:content';

const { title } = Astro.props;
const posts = await getCollection('blog');
---

<!-- Template: HTML with JSX expressions -->
<Layout title={title}>
  <h1>{title}</h1>
  <ul>
    {posts.map(post => (
      <li><a href={`/blog/${post.id}`}>{post.data.title}</a></li>
    ))}
  </ul>
</Layout>

<style>
  /* Scoped to this component by default */
  h1 { color: navy; }
</style>

<script>
  // Client-side JavaScript (bundled, deduped)
  console.log('Runs in browser');
</script>
```

## File-Based Routing

| File                          | Route                   |
|-------------------------------|-------------------------|
| `src/pages/index.astro`       | `/`                     |
| `src/pages/about.astro`       | `/about`                |
| `src/pages/blog/index.astro`  | `/blog`                 |
| `src/pages/blog/[id].astro`   | `/blog/:id` (dynamic)   |
| `src/pages/[...path].astro`   | Catch-all               |

Dynamic routes require `getStaticPaths()` in SSG mode.

## SEO Essentials

```astro
---
const { title, description, image } = Astro.props;
const canonicalURL = new URL(Astro.url.pathname, Astro.site);
---

<head>
  <title>{title}</title>
  <meta name="description" content={description} />
  <link rel="canonical" href={canonicalURL} />
  <meta property="og:title" content={title} />
  <meta property="og:description" content={description} />
  <meta property="og:image" content={image} />
  <meta property="og:type" content="website" />
  <meta name="twitter:card" content="summary_large_image" />
</head>
```

## Performance Best Practices

1. **Partial Hydration**: Use `client:*` directives only where needed (see Islands table above)
2. **Image Optimization**: Use Astro's `<Image />` component from `astro:assets`
3. **Stay Static**: Islands architecture means most content remains static HTML with zero JS
4. **Prefetching**: Auto-load links before user clicks

```javascript
// astro.config.mjs
export default defineConfig({
  prefetch: {
    prefetchAll: true,
    defaultStrategy: 'viewport'
  }
});
```

Options: `'tap'` (on hover/focus), `'viewport'` (when visible), `'load'` (on page load).

5. **Critical CSS**: Inline above-the-fold CSS with `astro-critters`

```bash
npm install astro-critters
```

```javascript
import critters from 'astro-critters';
export default defineConfig({ integrations: [critters()] });
```

## Deployment

| Platform            | CI/CD      | Free Tier     | SSR Support               | Guide                                         |
|---------------------|------------|---------------|---------------------------|-----------------------------------------------|
| **Cloudflare**      | Auto       | Unlimited     | `@astrojs/cloudflare`     | [cloudflare.md](references/deploy/cloudflare.md)         |
| **Netlify**         | Auto       | 100 GB/mo     | `@astrojs/netlify`        | [netlify.md](references/deploy/netlify.md)               |
| **Vercel**          | Auto       | 100 GB/mo     | `@astrojs/vercel`         | [vercel.md](references/deploy/vercel.md)                 |
| **GitHub Pages**    | via Action | Unlimited     | SSG only                  | [github-pages.md](references/deploy/github-pages.md)     |
| **Firebase**        | Manual     | 10 GB/mo      | `@astrojs/node` + Run     | [firebase.md](references/deploy/firebase.md)             |
| **AWS S3/GCS**      | Manual     | Pay-as-you-go | N/A (SSG only)            | [aws-gcs.md](references/deploy/aws-gcs.md)               |

Cloudflare acquired Astro in January 2026. The platform integration is deepening; Astro 6 Beta includes native
Cloudflare Workers support.

### Deployment Workflow

1. **Build**: `npm run build` and verify `dist/` output
2. **Preview**: `npm run preview` at localhost:4321
3. **Configure**: Set `site`, `base`, and `trailingSlash` in `astro.config.mjs`
4. **Deploy**: Push to platform or run deploy command
5. **Verify**: Check live URL, test 404 page, validate assets load

### Common Deployment Gotchas

| Issue                         | Solution                                       |
|-------------------------------|-------------------------------------------------|
| Trailing slash problems       | Set `trailingSlash: 'always'` or `'never'`     |
| Assets not loading on subpath | Configure `base` in `astro.config.mjs`         |
| 404 not working               | Create `src/pages/404.astro`                   |
| Build fails on deploy         | Check Node version matches local (v18+)        |

## Pre-Deploy Checklist

- [ ] `npm run build` completes without errors
- [ ] `npm run preview` renders correctly at localhost:4321
- [ ] `astro check` passes (Content Collection schema validation)
- [ ] Images use `<Image />` or are in `public/`
- [ ] SEO metadata on all pages (title, description, og:*)
- [ ] `404.astro` exists and renders correctly
- [ ] `base` path set if deploying to subdirectory
- [ ] Environment variables set on deployment platform
- [ ] `trailingSlash` matches hosting platform expectations
- [ ] RSS feed works (`/rss.xml`), sitemap generated (`/sitemap-index.xml`)

## Testing

Static analysis with `npx astro check`, component testing with Vitest (using `experimental_AstroContainer`), E2E
with Playwright, and link checking with `linkinator`.

See [markdown-deep-dive.md](references/markdown-deep-dive.md) for full setup examples.

## Essential Integrations

```bash
npx astro add react      # React components
npx astro add tailwind   # Tailwind CSS
npx astro add mdx        # MDX support
npx astro add sitemap    # Auto-generate sitemap
npm install @astrojs/rss # RSS feed
```

## Troubleshooting

**"Works locally but breaks on deploy"**

- Check environment variables are set on the hosting platform
- Verify `base` path configuration matches deploy target
- Ensure Node version matches (v18+ required)

**Dynamic routes missing pages**

- Verify `getStaticPaths()` returns all needed paths
- Check for typos in params

**Content Collection schema errors**

- Run `npx astro check` for validation details
- Ensure frontmatter matches the Zod schema exactly

**`post.render()` is not a function**

- Astro 5.x changed the render API
- Import `render` from `astro:content` and call `render(post)` instead

**Assets not loading**

- Use `import` for processed assets in `src/`
- Use `public/` for unprocessed static files

**Mermaid diagrams not rendering**

- Use `rehype-mermaid` (not the outdated `astro-mermaid`)
- Run `npx playwright install --with-deps chromium` for the build-time renderer

## References

Detailed guides for specific topics:

- [markdown-deep-dive.md](references/markdown-deep-dive.md): Markdown/MDX patterns, Shiki, Expressive Code, testing
- [starlight.md](references/starlight.md): Starlight config, components, i18n, CSS, plugins
- [search-and-diagrams.md](references/search-and-diagrams.md): Pagefind, Fuse.js, Mermaid, PlantUML
- [deploy/](references/deploy/): Per-platform deployment guides (Cloudflare, Netlify, Vercel, GitHub Pages, Firebase,
  AWS/GCS)

## Key Resources

- [Astro Documentation](https://docs.astro.build/)
- [Content Collections](https://docs.astro.build/en/guides/content-collections/)
- [Starlight](https://starlight.astro.build/)
- [Deploy Astro](https://docs.astro.build/en/guides/deploy/)
