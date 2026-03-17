# Versioned Documentation in Starlight

Starlight does not have built-in versioning as of early 2026. This reference covers practical
strategies for maintaining versioned documentation, from simple to sophisticated.

## Choosing a Strategy

| Strategy | Best for | Complexity |
|---|---|---|
| Single version (latest only) | Most projects, early-stage products | None |
| Banner with link to old versions | Projects with occasional major releases | Low |
| Branch-per-version | Projects needing full parallel version docs | Medium |
| Directory-per-version | All versions in one build | Medium-High |
| `starlight-versions` plugin | Built-in version switcher UI | Low-Medium |

**Start with the simplest strategy that works.** Most documentation sites — especially for
libraries and tools — do fine with just the latest version. Add versioning only when users
actively need to reference older versions.

## Strategy 1: Single Version with Migration Guides

The simplest approach. Keep only the latest docs, and include a migration guide for each
major version.

```
src/content/docs/
├── index.mdx
├── guides/
│   └── ...
├── reference/
│   └── ...
└── migration/
    ├── v3-to-v4.md
    └── v2-to-v3.md
```

Use a banner on pages that changed significantly:

```markdown
---
title: Configuration Reference
banner:
  content: >
    This page documents v4.x. For v3.x docs, see the
    <a href="https://v3.docs.example.com">v3 documentation</a>.
---
```

## Strategy 2: Branch-Per-Version

Maintain a separate Git branch for each major version. Deploy each branch to a different
URL (subdomain or path).

### Setup

```
main branch        → docs.example.com (latest, v4)
v3 branch          → v3.docs.example.com
v2 branch          → v2.docs.example.com
```

### Workflow

1. When releasing a new major version, create a branch from `main`:
   ```bash
   git checkout -b v3
   git push -u origin v3
   ```

2. Update `main` with the new version's docs

3. Deploy each branch separately:
   - **GitHub Pages**: Use separate repos or a workflow that deploys branches to different paths
   - **Cloudflare Pages**: Each branch auto-deploys to `v3.my-docs.pages.dev`; add custom
     domains for clean URLs

### Linking between versions

Add a banner or header component on older version sites:

```markdown
---
title: Getting Started
banner:
  content: >
    You're viewing docs for v3. <a href="https://docs.example.com">Switch to latest (v4)</a>.
---
```

For the latest version, add links to older versions in the sidebar or footer:

```javascript
// astro.config.mjs
starlight({
  sidebar: [
    // ... your regular sidebar
    {
      label: 'Older Versions',
      items: [
        { label: 'v3 docs', link: 'https://v3.docs.example.com', attrs: { target: '_blank' } },
        { label: 'v2 docs', link: 'https://v2.docs.example.com', attrs: { target: '_blank' } },
      ],
    },
  ],
})
```

### Pros and cons

**Pros:**
- Clean separation between versions
- Each version is independently buildable and deployable
- Easy to archive old versions (just stop deploying the branch)
- Cherry-pick fixes between versions

**Cons:**
- Maintaining multiple branches can be tedious
- No built-in version switcher UI (must build a custom component or use banners)
- Cross-version search requires external tooling
- Needs an external routing layer for same-domain serving (hosting platform rewrites,
  CDN path routing, or subdomains)

## Strategy 3: Directory-Per-Version

Keep all versions in a single repository and build. Each version lives in its own directory.

### File structure

```
src/content/docs/
├── index.mdx              # Landing page with version picker
├── v4/                    # Current version
│   ├── index.md
│   ├── guides/
│   └── reference/
├── v3/                    # Previous version
│   ├── index.md
│   ├── guides/
│   └── reference/
└── v2/                    # Older version
    └── ...
```

### Sidebar configuration

```javascript
starlight({
  sidebar: [
    { label: 'Home', slug: 'index' },
    {
      label: 'v4 (Latest)',
      items: [
        { slug: 'v4' },
        { label: 'Guides', autogenerate: { directory: 'v4/guides' } },
        { label: 'Reference', autogenerate: { directory: 'v4/reference' } },
      ],
    },
    {
      label: 'v3',
      collapsed: true,
      items: [
        { slug: 'v3' },
        { label: 'Guides', autogenerate: { directory: 'v3/guides' } },
        { label: 'Reference', autogenerate: { directory: 'v3/reference' } },
      ],
    },
  ],
})
```

### Pros and cons

**Pros:**
- Single deployment, single build
- Built-in search covers all versions
- Easy to link between versions

**Cons:**
- Build time grows with each version
- Sidebar gets long with many versions
- Duplicated content increases repo size

## Strategy 4: `starlight-versions` Plugin

The [`starlight-versions`](https://github.com/HiDeoo/starlight-versions) plugin by HiDeoo is
the primary community solution for versioned docs. It provides a built-in version switcher UI,
handles routing between versions, and integrates with Astro's Content Layer API.

- **npm**: `starlight-versions` (v0.8.x as of early 2026)
- **Approach**: Folder-based — archives the current docs into a versioned snapshot in the
  same repository and build
- **URL pattern**: Current version at `/page-slug`, archived at `/1.0/page-slug`
- **Status**: Actively maintained but early-stage — expect breaking changes between releases

### Setup

```bash
npm install starlight-versions
```

```javascript
// astro.config.mjs
import starlight from '@astrojs/starlight';
import starlightVersions from 'starlight-versions';

export default defineConfig({
  integrations: [
    starlight({
      title: 'My Docs',
      plugins: [
        starlightVersions({
          versions: [
            { slug: '1.0' },            // archived version
            { slug: '0.9', label: 'Legacy' },  // optional label
          ],
          current: { label: 'v2.0 (Latest)' },
        }),
      ],
    }),
  ],
});
```

```typescript
// src/content.config.ts
import { defineCollection } from 'astro:content';
import { docsLoader } from '@astrojs/starlight/loaders';
import { docsSchema } from '@astrojs/starlight/schema';
import { docsVersionsLoader } from 'starlight-versions/loader';

export const collections = {
  docs: defineCollection({ loader: docsLoader(), schema: docsSchema() }),
  versions: defineCollection({ loader: docsVersionsLoader() }),
};
```

### Configuration options

| Option | Type | Description |
|---|---|---|
| `versions[].slug` | `string` (required) | Used in URLs — e.g., `1.0`, `2-1-0` |
| `versions[].label` | `string` | UI display name, defaults to slug |
| `versions[].redirect` | `'same-page' \| 'root'` | Behavior when switching versions |
| `current.label` | `string` | Display name for the current/latest version |
| `current.redirect` | `'same-page' \| 'root'` | Same as above |

### Pros and cons

**Pros:**
- Built-in version switcher dropdown in the UI
- Single build, single deployment
- i18n-aware (works with multilingual sites)
- Active maintenance from a prolific Starlight plugin author

**Cons:**
- Early-stage — API may change between releases
- All versions in one build (build time grows with versions)
- Opinionated approach may not fit all workflows

## Version Switcher Component

If no plugin fits, build a simple version switcher as a component override:

```astro
---
// src/components/VersionSelect.astro
const versions = [
  { label: 'v4 (Latest)', href: 'https://docs.example.com' },
  { label: 'v3', href: 'https://v3.docs.example.com' },
  { label: 'v2', href: 'https://v2.docs.example.com' },
];
const currentVersion = versions[0].label;
---

<label class="version-select">
  <span class="sr-only">Version</span>
  <select onchange="window.location.href = this.value">
    {versions.map(v => (
      <option value={v.href} selected={v.label === currentVersion}>
        {v.label}
      </option>
    ))}
  </select>
</label>

<style>
  .version-select select {
    appearance: auto;
    background: var(--sl-color-gray-6);
    color: var(--sl-color-white);
    border: 1px solid var(--sl-color-gray-5);
    border-radius: 0.25rem;
    padding: 0.25rem 0.5rem;
    font-size: var(--sl-text-sm);
  }
</style>
```

Register it as a component override:

```javascript
// astro.config.mjs
starlight({
  components: {
    // Add it next to the site title or in the header
    SiteTitle: './src/components/SiteTitleWithVersion.astro',
  },
})
```

Then wrap the default SiteTitle to include the version picker:

```astro
---
// src/components/SiteTitleWithVersion.astro
import Default from '@astrojs/starlight/components/SiteTitle.astro';
import VersionSelect from './VersionSelect.astro';
---

<div style="display: flex; align-items: center; gap: 0.75rem;">
  <Default {...Astro.props}><slot /></Default>
  <VersionSelect />
</div>
```

## Recommendations

1. **Don't version prematurely.** If you haven't shipped v2 yet, you don't need versioning.
2. **Try `starlight-versions` first** if you need versioning — it's the lowest-effort path
   to a working version switcher. If it doesn't fit your workflow, fall back to
   branch-per-version.
3. **Branch-per-version** is the cleanest separation for teams with strong CI/CD automation.
   It fits naturally with Git workflows and avoids coupling all versions into one build.
4. **Cloudflare Pages** makes branch-per-version easy because each branch auto-deploys to
   `<branch>.<project>.pages.dev` — add custom domains for clean URLs.
5. **Archive aggressively.** If a version is truly end-of-life, consider making it a static
   archive (freeze the build output) rather than maintaining a live branch.
6. **Keep URLs stable.** Whatever strategy you choose, try not to break existing links.
   Use redirects when URLs change.

## Background

Starlight has no built-in versioning and no committed timeline for adding it. The feature
was discussed in [GitHub Discussion #957](https://github.com/withastro/starlight/discussions/957)
where the team acknowledged it as a desired feature but noted the significant development
effort required. The i18n locale routing system is the closest structural analog to how
versioning might eventually be natively implemented.
