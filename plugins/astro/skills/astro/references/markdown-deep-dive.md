# Markdown Deep Dive for Astro

Advanced patterns for Markdown and MDX content in Astro static sites.

## Contents

- [Content Collection Best Practices](#content-collection-best-practices)
- [MDX Integration](#mdx-integration)
- [Remark and Rehype Plugins](#remark-and-rehype-plugins)
- [Table of Contents Generation](#table-of-contents-generation)
- [Images in Markdown](#images-in-markdown)
- [Syntax Highlighting](#syntax-highlighting)
- [Testing and Quality](#testing-and-quality)
- [Forms for Static Sites](#forms-for-static-sites)
- [JSON-LD Structured Data](#json-ld-structured-data)
- [Dark Mode Toggle](#dark-mode-toggle)
- [Performance Considerations](#performance-considerations)
- [Troubleshooting Markdown Issues](#troubleshooting-markdown-issues)

## Content Collection Best Practices

### Schema Patterns

```typescript
// src/content.config.ts
import { defineCollection, z, reference } from 'astro:content';
import { glob } from 'astro/loaders';

const blog = defineCollection({
  loader: glob({ pattern: '**/*.md', base: './src/data/blog' }),
  schema: z.object({
    title: z.string(),
    description: z.string(),
    pubDate: z.coerce.date(),
    updatedDate: z.coerce.date().optional(),
    heroImage: z.string().optional(),
    author: reference('authors'),
    tags: z.array(z.string()).default([]),
    draft: z.boolean().default(false)
  })
});

const authors = defineCollection({
  loader: glob({ pattern: '**/*.json', base: './src/data/authors' }),
  schema: z.object({
    name: z.string(),
    avatar: z.string(),
    bio: z.string()
  })
});

const docs = defineCollection({
  loader: glob({ pattern: '**/*.md', base: './src/data/docs' }),
  schema: z.object({
    title: z.string(),
    description: z.string().optional(),
    sidebar: z.object({
      order: z.number(),
      label: z.string().optional()
    }).optional()
  })
});

export const collections = { blog, authors, docs };
```

### Filtering Collections

```astro
---
import { getCollection } from 'astro:content';

// Filter out drafts in production
const publishedPosts = await getCollection('blog', ({ data }) => {
  return import.meta.env.PROD ? data.draft !== true : true;
});

// Sort by date
const sortedPosts = publishedPosts.sort(
  (a, b) => b.data.pubDate.valueOf() - a.data.pubDate.valueOf()
);

// Filter by tag
const rustPosts = await getCollection('blog', ({ data }) => {
  return data.tags.includes('rust');
});
---
```

## MDX Integration

```bash
npx astro add mdx
```

### Using Components in MDX

```mdx
---
title: My Article
---
import Callout from '../components/Callout.astro';
import CodeDemo from '../components/CodeDemo.jsx';

# {frontmatter.title}

<Callout type="warning">
  This is an important warning!
</Callout>

<CodeDemo client:visible />
```

### Component Mapping

Override default HTML elements rendered from Markdown:

```astro
---
// src/pages/blog/[id].astro
import { getEntry, render } from 'astro:content';
import CustomHeading from '../components/CustomHeading.astro';
import CustomCode from '../components/CustomCode.astro';

const { id } = Astro.params;
const post = await getEntry('blog', id);
const { Content } = await render(post);
---

<Content components={{
  h1: CustomHeading,
  h2: CustomHeading,
  pre: CustomCode
}} />
```

## Remark and Rehype Plugins

### Common Plugin Setup

```javascript
// astro.config.mjs
import { defineConfig } from 'astro/config';
import remarkToc from 'remark-toc';
import remarkMath from 'remark-math';
import rehypeKatex from 'rehype-katex';
import rehypeSlug from 'rehype-slug';
import rehypeAutolinkHeadings from 'rehype-autolink-headings';

export default defineConfig({
  markdown: {
    remarkPlugins: [remarkToc, remarkMath],
    rehypePlugins: [
      rehypeSlug,
      [rehypeAutolinkHeadings, { behavior: 'wrap' }],
      rehypeKatex
    ]
  }
});
```

### Reading Time Plugin

```javascript
// plugins/remark-reading-time.mjs
import getReadingTime from 'reading-time';
import { toString } from 'mdast-util-to-string';

export function remarkReadingTime() {
  return (tree, { data }) => {
    data.astro.frontmatter.minutesRead = getReadingTime(toString(tree)).text;
  };
}
```

Register it in `astro.config.mjs` under `markdown.remarkPlugins`.

## Table of Contents Generation

The `render()` function returns a `headings` array of `{ depth, slug, text }` objects:

```astro
---
import { getEntry, render } from 'astro:content';

const post = await getEntry('blog', 'my-post');
const { Content, headings } = await render(post);
---

<nav class="toc">
  <h2>On This Page</h2>
  <ul>
    {headings.filter(h => h.depth <= 3).map(h => (
      <li style={`margin-left: ${(h.depth - 2) * 1}rem`}>
        <a href={`#${h.slug}`}>{h.text}</a>
      </li>
    ))}
  </ul>
</nav>
<Content />
```

## Images in Markdown

Relative paths in Markdown trigger automatic optimization: `![Alt text](./hero.png)`. In MDX, import
the image and use `<Image />` from `astro:assets` for full control over width, height, and format.

For remote images, allowlist domains in `astro.config.mjs` under `image.domains` or `image.remotePatterns`.

## Syntax Highlighting

### Shiki Configuration

```javascript
// astro.config.mjs  (single theme)
export default defineConfig({
  markdown: {
    shikiConfig: { theme: 'github-dark', wrap: true }
  }
});
```

For dual light/dark themes, replace `theme` with `themes` and add the CSS toggle:

```javascript
shikiConfig: {
  themes: { light: 'github-light', dark: 'github-dark' },
}
```

```css
@media (prefers-color-scheme: dark) {
  .astro-code, .astro-code span {
    color: var(--shiki-dark) !important;
    background-color: var(--shiki-dark-bg) !important;
  }
}
```

### Line Highlighting and Transformers

Use line range syntax in fenced code blocks: ````typescript {2,4}`. Install `@shikijs/transformers`
(Astro 4.14+) for notation-based highlighting:

```javascript
// astro.config.mjs
import { transformerNotationFocus, transformerNotationDiff } from '@shikijs/transformers';

export default defineConfig({
  markdown: {
    shikiConfig: {
      transformers: [transformerNotationFocus(), transformerNotationDiff()],
    }
  }
});
```

Notation comments inside code blocks:

- `// [!code focus]` focuses that line (dims surrounding lines)
- `// [!code ++]` marks as addition (green)
- `// [!code --]` marks as deletion (red)

### Expressive Code (Recommended for Docs)

Copy buttons, filenames, diff highlighting, and terminal frames. Starlight includes it built in;
standalone sites install `astro-expressive-code`:

```javascript
// astro.config.mjs
import expressiveCode from 'astro-expressive-code';
export default defineConfig({ integrations: [expressiveCode()] });
```

## Testing and Quality

### Static Analysis

```bash
npx astro check    # Type checking and Content Collection schema validation
npx eslint .       # Linting (install eslint as a dev dependency)
```

Build-time validation happens automatically with Content Collections. Schema errors fail the build.

### Component Testing with Vitest

```bash
npm install -D vitest
```

```typescript
// vitest.config.ts
import { getViteConfig } from 'astro/config';

export default getViteConfig({
  test: { include: ['src/**/*.test.ts'] },
});
```

```typescript
// src/components/Button.test.ts
import { experimental_AstroContainer as AstroContainer } from 'astro/container';
import { expect, test } from 'vitest';
import Button from './Button.astro';

test('Button renders with text', async () => {
  const container = await AstroContainer.create();
  const result = await container.renderToString(Button, {
    props: { text: 'Click me' }
  });
  expect(result).toContain('Click me');
});
```

### E2E Testing with Playwright

```bash
npm install -D @playwright/test
npx playwright install
```

```typescript
// tests/homepage.spec.ts
import { test, expect } from '@playwright/test';

test('homepage loads correctly', async ({ page }) => {
  await page.goto('/');
  await expect(page).toHaveTitle(/My Site/);
  await expect(page.locator('h1')).toBeVisible();
});
```

### Link Checking

```bash
npm install -D linkinator
npx linkinator dist --recurse
```

## Forms for Static Sites

SSG sites cannot handle form submissions server-side. Use third-party form handlers.

**Formspree:** point `action` to `https://formspree.io/f/YOUR_FORM_ID` with `method="POST"`.

**Netlify Forms:** add `data-netlify="true"` and a hidden `form-name` field. Netlify provisions the
endpoint automatically at deploy time:

```html
<form name="contact" method="POST" data-netlify="true">
  <input type="hidden" name="form-name" value="contact" />
  <input type="text" name="name" required />
  <input type="email" name="email" required />
  <textarea name="message" required></textarea>
  <button type="submit">Send</button>
</form>
```

## JSON-LD Structured Data

Add schema.org rich snippets to blog posts for improved search engine results:

```astro
---
const jsonLd = {
  "@context": "https://schema.org",
  "@type": "BlogPosting",
  headline: entry.data.title,
  description: entry.data.description,
  datePublished: entry.data.pubDate?.toISOString(),
  author: { "@type": "Person", name: entry.data.author },
};
---

<script type="application/ld+json" set:html={JSON.stringify(jsonLd)} />
```

Place the `<script>` tag inside `<head>` via a layout component. The `set:html` directive tells Astro to
inject the string as raw HTML without escaping.

## Dark Mode Toggle

A localStorage-based toggle that respects the system preference on first visit:

```astro
<button id="theme-toggle" aria-label="Toggle dark mode">Toggle Theme</button>

<script>
  const toggle = document.getElementById('theme-toggle');
  const html = document.documentElement;

  const savedTheme = localStorage.getItem('theme');
  const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;

  if (savedTheme === 'dark' || (!savedTheme && prefersDark)) {
    html.classList.add('dark');
  }

  toggle?.addEventListener('click', () => {
    html.classList.toggle('dark');
    localStorage.setItem('theme', html.classList.contains('dark') ? 'dark' : 'light');
  });
</script>
```

If using Tailwind CSS, enable `darkMode: 'class'` in `tailwind.config.mjs` so that the `.dark` class on
`<html>` activates dark variants.

## Performance Considerations

1. **Build-time only**: All Markdown processing happens at build
2. **No runtime parsing**: Output is pure HTML
3. **Syntax highlighting**: Pre-computed with Shiki; no client-side JS required
4. **Lazy loading**: Use `loading="lazy"` on images below the fold
5. **Content caching**: Astro caches processed Markdown between builds

## Troubleshooting Markdown Issues

**Frontmatter validation errors:**

```bash
npx astro check  # Shows detailed schema errors
```

**Code block not highlighting:**

- Ensure language tag is specified: ````javascript`
- Check theme is properly configured in `shikiConfig`

**MDX components not rendering:**

- Verify component is imported at top of the MDX file
- Check component path is correct relative to the MDX file
- Ensure MDX integration is installed (`npx astro add mdx`)

**Slow builds with many Markdown files:**

- Use the Content Layer API with `glob()` loader for up to 75% faster builds
- Use `content.config.ts` type generation caching

**`post.render()` is not a function:**

- Astro 5.x changed the render API. Import `render` from `astro:content` and call `render(post)` instead.
