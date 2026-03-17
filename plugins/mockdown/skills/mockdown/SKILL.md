---
name: mockdown
description: >-
  Reads and writes ASCII wireframes in the Mockdown format. Trigger when the user pastes ASCII
  wireframes (box-drawing characters like ┌ │ └ ─ or patterns like [ Button ] and [___]), mentions
  "wireframe", "mockup", "mockdown", wants to convert a wireframe to code, or asks to sketch a
  wireframe. Interprets wireframe components, layout structure, and hierarchy, then generates code
  for the target platform. Produces wireframes directly in conversation without external tools.
---

# Mockdown: ASCII Wireframe Format

ASCII wireframes are the fastest way to reason about UI structure. No colors, no themes; just components,
spatial arrangement, and hierarchy. This skill teaches how to read and write ASCII wireframes for any target:
web pages, terminal UIs (TUI), CLI interfaces, dashboards, forms, dialogs, and more.

[Mockdown](https://www.mockdown.design/) is a free browser-based ASCII wireframe editor with 20+ components.
This skill understands its output format natively.

## Canvas Properties

- Default grid size: 80 columns x 40 rows
- Components are positioned by row and column coordinates
- The "Copy Markdown" export wraps the ASCII output in a markdown code fence (triple backticks)
- Trailing whitespace is trimmed per line; trailing blank lines are collapsed

## Reading Wireframes

When the user pastes an ASCII wireframe (typically inside a code fence), follow this process:

### Step 1: Identify Components

Scan the ASCII art and map each visual element to its component type using the patterns in
[component-catalog.md](references/component-catalog.md). Components are positioned on the 80x40 grid.

### Step 2: Determine Layout Structure

- Identify rows and columns by the spatial arrangement of components
- Components placed side by side are in the same row
- Components stacked vertically form a column
- Containers (cards, dialogs, split panels) establish nesting relationships
- Nav bars and tabs at the top indicate page-level navigation

### Step 3: Extract Content

- Read text labels, placeholder text, column headers, list items, and breadcrumb paths
- Note active/selected states (filled radio `●`, checked checkbox `☑`, active tab in brackets `[ Tab 1 ]`,
  active pagination page in brackets `[3]`)
- Capture toggle states (`[●━]` = on, `[━●]` = off)
- Read progress bar fill percentage from the ratio of `█` to `░` characters

### Step 4: Generate Code

Infer the target platform from conversation context and consult the appropriate mapping reference:

- **Semantic HTML**: [html-mapping.md](references/html-mapping.md) for clean HTML with CSS Grid/Flexbox; also
  covers React, Vue, Svelte, Blazor, Bootstrap
- **Tailwind + Lucide**: [tailwind-mapping.md](references/tailwind-mapping.md) for polished single-file HTML via
  CDN with no build step
- **shadcn/ui**: [shadcn-mapping.md](references/shadcn-mapping.md) for React + Tailwind + Radix UI components
- **Spectre.Console**: [spectre-mapping.md](references/spectre-mapping.md) for .NET terminal UIs; adapt for other
  TUI frameworks (Ink, Textual, blessed, curses) based on the user's stack

**Code generation rules:**

A wireframe is a sketch. The generated code should be a **realized, polished version** of that sketch — not a bare
mechanical 1:1 mapping. When someone opens the output in a browser (or runs it in a terminal), it should look like a
real, presentable page with proper spacing, visual hierarchy, and theming.

- Generate a **complete, self-contained HTML document** (DOCTYPE, head, body) that opens directly in a browser
- Apply the target framework's theming system: background color, text color, container width, section spacing. The
  page must look good out of the box without additional CSS work.
- Preserve the spatial relationships and hierarchy from the wireframe
- Keep text labels, headings, and placeholder text exactly as shown in the wireframe
- When table rows or list items are empty in the wireframe, populate them with realistic sample data that fits the
  column headers (names, emails, roles, dates, etc.). Empty rows in a wireframe mean "there will be data here"; they
  are not a request for blank cells.
- Do not add behavior or interactivity unless asked
- If the wireframe structure is ambiguous or components overlap in ways that are unclear, ask the user for
  clarification before generating code

### Example

**Input wireframe:**

```text
Logo   Link   Link   Link     [ Action ]
────────────────────────────────────────

[/ Search...       ]

┌─────────┬─────────┬─────────┐
│ Name    │ Role    │ Status  │
├─────────┼─────────┼─────────┤
│         │         │         │
│         │         │         │
└─────────┴─────────┴─────────┘

< 1 2 [3] 4 5 ... >
```

**Output (semantic HTML):**

A complete, openable HTML page with proper structure. The nav bar, search, table (with sample data), and pagination
are all styled with basic CSS so the page looks presentable:

```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <title>Dashboard</title>
  <style>
    body { font-family: system-ui, sans-serif; margin: 0; color: #1a1a1a; }
    nav { display: flex; align-items: center; gap: 1.5rem; padding: 1rem 2rem; border-bottom: 2px solid #e5e5e5; }
    nav .logo { font-weight: 700; }
    nav a { text-decoration: none; color: inherit; }
    nav button { margin-left: auto; padding: 0.5rem 1rem; }
    .container { max-width: 800px; margin: 2rem auto; padding: 0 2rem; }
    input[type="search"] { width: 100%; padding: 0.5rem; margin-bottom: 1.5rem; }
    table { width: 100%; border-collapse: collapse; }
    th, td { text-align: left; padding: 0.75rem; border-bottom: 1px solid #e5e5e5; }
    .pagination { display: flex; gap: 0.5rem; margin-top: 1.5rem; justify-content: center; }
    .pagination .active { font-weight: 700; }
  </style>
</head>
<body>
  <nav>
    <span class="logo">Logo</span>
    <a href="#">Link</a><a href="#">Link</a><a href="#">Link</a>
    <button>Action</button>
  </nav>
  <div class="container">
    <input type="search" placeholder="Search...">
    <table>
      <thead><tr><th>Name</th><th>Role</th><th>Status</th></tr></thead>
      <tbody>
        <tr><td>Alice Johnson</td><td>Admin</td><td>Active</td></tr>
        <tr><td>Bob Smith</td><td>Editor</td><td>Active</td></tr>
      </tbody>
    </table>
    <div class="pagination">
      <a href="#">&lt;</a>
      <a href="#">1</a><a href="#">2</a>
      <span class="active">3</span>
      <a href="#">4</a><a href="#">5</a><span>...</span>
      <a href="#">&gt;</a>
    </div>
  </div>
</body>
</html>
```

The output is a complete page you can open in a browser. For a TUI context, the same wireframe would map to
framework-specific widgets (e.g., Spectre.Console `Table`, `TextPrompt`) instead of HTML.

## Writing Wireframes

Produce ASCII wireframes directly in conversation using the Mockdown component patterns. Use the component quick
reference below and the full catalog in [component-catalog.md](references/component-catalog.md).

When composing a wireframe:

- Use an 80-column grid (narrower for terminal UIs if appropriate)
- Position components with spaces to indicate spatial relationships
- Use box-drawing characters (`┌ ─ ┐ │ └ ┘ ├ ┤ ┬ ┴ ┼`) for containers
- Nest components inside containers to show hierarchy
- Add realistic sample data in text inputs (e.g., `[Jane Doe         ]`) so the wireframe conveys intent
- Ensure all vertical borders (`│`) in the same column align precisely — count characters carefully in split panels
- Wrap the final wireframe in a code fence

### Nesting Example

Components nest inside containers. The container boundary establishes a parent-child relationship in the
generated code:

```text
┌──────────────────────────┐
│ Login                    │
├──────────────────────────┤
│                          │
│ [Enter email...       ]  │
│ [_____________________]  │
│ ☐ Remember me            │
│          [ Sign In ]     │
│                          │
└──────────────────────────┘
```

The card wraps two inputs, a checkbox, and a button. In generated code, the card becomes a form or card wrapper
and the inner components become its children.

## Component Quick Reference

| Component   | ASCII Pattern                          | Maps to               |
|-------------|----------------------------------------|-----------------------|
| Button      | `[ Label ]`                            | button                |
| Input       | `[___________]`                        | text input            |
| Checkbox    | `☐ Label` / `☑ Label`                  | checkbox              |
| Radio       | `○ Label` / `● Label`                  | radio button          |
| Dropdown    | `[▾ Option    ]`                       | select / dropdown     |
| Search      | `[/ Search...       ]`                 | search input          |
| Toggle      | `[●━] Label` / `[━●] Label`            | toggle switch         |
| Progress    | `[████░░░░] 50%`                       | progress bar          |
| Nav Bar     | `Logo  Link  Link  [ Action ]` + line  | navigation bar        |
| Tabs        | `[ Active ]  Tab 2  Tab 3` + line      | tab component         |
| Breadcrumb  | `Home > Section > Page`                | breadcrumb nav        |
| Pagination  | `< 1 2 [3] 4 5 ... >`                  | pagination            |
| Card        | Box-drawn border, title, separator     | card / article        |
| Dialog      | Box-drawn border, title + `x`, buttons | dialog / modal        |
| Table       | `┌─┬─┐ │ │ ├─┼─┤ └─┴─┘`                | table                 |
| List        | `• Item 1` bullets                     | unordered list        |
| Placeholder | Box with `\ /` crosshatch + `IMG`      | image placeholder     |
| Split Panel | Two-column box with `┬`/`┴` divider    | split layout          |
| Box         | `┌──┐ │  │ └──┘`                       | container / section   |
| Text        | Plain text block                       | paragraph / heading   |

For the full catalog with rendering details and draw tools, see
[component-catalog.md](references/component-catalog.md).
