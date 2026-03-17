# HTML Mapping

How to map Mockdown components to semantic HTML elements. Use this reference when generating web UI code from
wireframes.

## Component Mapping

| Component      | HTML Element                        | Notes                                            |
|----------------|-------------------------------------|--------------------------------------------------|
| Button         | `<button>`                          | Label as text content                            |
| Input          | `<input type="text">`               | Displayed text as `placeholder`                  |
| Checkbox       | `<input type="checkbox">` + `label` | `☑` sets `checked` attribute                     |
| Radio          | `<input type="radio">` + `label`    | `●` sets `checked` attribute                     |
| Dropdown       | `<select>` + `<option>`             | Displayed text is the selected option            |
| Search         | `<input type="search">`             | Text after `/` as `placeholder`                  |
| Toggle         | Custom toggle switch                | Track `[●━]` (on) vs `[━●]` (off) state          |
| Progress Bar   | `<progress>`                        | `value` from `█`/`░` ratio, `max="100"`          |
| Nav Bar        | `<nav>` with logo, links, button    | Logo as `<span>`, links as `<a>`, CTA `<button>` |
| Tabs           | Tab component or role="tablist"     | Bracketed tab gets `aria-selected="true"`        |
| Breadcrumb     | `<nav>` with `<ol>` breadcrumb list | Last item is plain text (not a link)             |
| Pagination     | `<nav>` with page links             | Bracketed page gets `aria-current="page"`        |
| Card           | `<article>` or card component       | Header/body split at `├──┤` separator            |
| Dialog / Modal | `<dialog>`                          | `×` maps to close button, bottom buttons footer  |
| Split Panel    | CSS Grid or Flexbox two-column      | `┬`/`┴` divider marks the column split           |
| Table          | `<table>` + `<thead>` + `<tbody>`   | First row = `<th>`, body rows = `<td>`           |
| List           | `<ul>` + `<li>`                     | `•` bullet items become list items               |
| Box            | `<div>` or `<section>`              | Context determines semantic element              |
| Placeholder    | `<img>` with `alt` text             | Or placeholder `<div>` with dimensions           |
| Text           | `<p>`, `<h1>`-`<h6>`, or `<span>`   | Size and position determine heading level        |
| Line           | `<hr>`                              | Or CSS border-bottom                             |
| Arrow          | Decorative (usually omitted)        | Use CSS or SVG if needed in output               |

## Layout Rules

- Use CSS Grid or Flexbox to preserve spatial relationships from the wireframe
- Components placed side by side map to a flex row or grid columns
- Components stacked vertically map to a flex column or grid rows
- Containers (cards, dialogs, split panels) become parent elements wrapping their children
- Do not add styling beyond layout and basic structure unless asked
- Do not add JavaScript behavior unless asked

## Framework Variants

When the user specifies a component framework, generate idiomatic code for that framework:

- **React**: JSX with functional components; use `className` instead of `class`
- **Vue**: SFC template syntax with `<template>`, `<script setup>`
- **Svelte**: `.svelte` component syntax
- **Blazor**: Razor component syntax (`.razor`)
- **Tailwind**: Replace CSS Grid/Flexbox with Tailwind utility classes
- **Bootstrap**: Use Bootstrap component classes (`btn`, `form-control`, `card`, etc.)
