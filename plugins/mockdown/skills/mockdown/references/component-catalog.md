# Mockdown Component Catalog

Complete reference for every Mockdown component and draw tool pattern, with default properties and semantic roles.

## Contents

- [Box-Drawing Characters](#box-drawing-characters)
- [Form Controls](#form-controls): Button, Input, Checkbox, Radio, Dropdown, Search, Toggle, Progress Bar
- [Navigation](#navigation): Nav Bar, Tabs, Breadcrumb, Pagination
- [Containers](#containers): Card, Dialog / Modal, Split Panel
- [Data](#data): Table, List
- [Drawing Elements](#drawing-elements): Box, Placeholder / Image, Text, Line, Arrow
- [Draw Tools](#draw-tools): Pencil, Brush, Spray, Shade, Fill, Smudge, Scatter, Eraser

## Box-Drawing Characters

Mockdown uses Unicode box-drawing characters for structured components:

- Corners: `┌` `┐` `└` `┘`
- Lines: `─` (horizontal) `│` (vertical)
- Junctions: `├` `┤` `┬` `┴` `┼`
- Double-line variants: `╔` `╗` `╚` `╝` `═` `║` `╠` `╣` `╦` `╩` `╬`

## Form Controls

### Button

```text
[ OK ]
[ Submit ]
[ Cancel ]
```

- Square brackets with padding around the label
- Default label: `OK`, default width: 6
- Semantic role: action trigger / interactive button

### Input

```text
[___________]
[Enter text...]
```

- Square brackets with underscores (empty) or placeholder text
- Default width: 13 characters
- Semantic role: single-line text entry field

### Checkbox

```text
☐ Disabled
☑ Enabled
```

- `☐` = unchecked, `☑` = checked
- Semantic role: boolean toggle with label; note the checked/unchecked state

### Radio

```text
○ Option A
● Option B
```

- `○` = unselected, `●` = selected
- Semantic role: single-select option within a group; note the selected state

### Dropdown

```text
[▾ Option    ]
[▾ Select    ]
```

- Square brackets with `▾` triangle prefix
- Semantic role: single-select from a list of options; displayed text is the current selection

### Search

```text
[/ Search...       ]
```

- Square brackets with `/` prefix indicating a search icon
- Semantic role: search input field with placeholder text

### Toggle

```text
[●━] On
[━●] Off
```

- `[●━]` = on (dot on left, line on right)
- `[━●]` = off (line on left, dot on right)
- Semantic role: binary on/off switch; note the state from dot position

### Progress Bar

```text
[████████░░░░░░] 60%
[████░░░░] 50%
```

- `█` = filled portion, `░` = empty portion
- Percentage is calculated from the ratio of `█` to total `█` + `░`
- The percentage label may appear after the bar
- Semantic role: progress or completion indicator with a value

## Navigation

### Nav Bar

```text
Logo   Link   Link   Link     [ Action ]
────────────────────────────────────────
```

- First item is the logo/brand text
- Middle items are navigation links
- Last item in brackets is the action button (CTA)
- A horizontal line `────` appears below as a separator
- Default logo: `Logo`, default links: `Link, Link, Link`, default action: `Action`
- Semantic role: primary navigation bar with brand, links, and call-to-action

### Tabs

```text
[ Tab 1 ]   Tab 2   Tab 3
──────────────────────────
```

- Active tab is wrapped in `[ brackets ]`
- Inactive tabs are plain text
- A horizontal line `────` appears below
- Default tabs: `Tab 1, Tab 2, Tab 3`
- Semantic role: tabbed navigation; bracketed tab is active

### Breadcrumb

```text
Home > Section > Page
```

- Items separated by `>`
- Last item is the current page
- Semantic role: hierarchical navigation trail; last item is not a link

### Pagination

```text
< 1 2 [3] 4 5 ... >
```

- `<` and `>` are previous/next controls
- Active page number is in `[brackets]`
- `...` indicates truncated page numbers
- Semantic role: page navigation with active page highlighted

## Containers

### Card

```text
┌──────────────────┐
│ Title            │
├──────────────────┤
│                  │
│                  │
│                  │
└──────────────────┘
```

- Box-drawn border with a title row
- A horizontal separator `├──┤` divides the title from the body
- Default title: `Title`, default size: 20 wide x 8 tall
- Semantic role: grouped content container with header and body

### Dialog / Modal

```text
┌────────────────────────────┐
│ Dialog                   × │
├────────────────────────────┤
│                            │
│                            │
│          [ Cancel ] [ OK ] │
└────────────────────────────┘
```

- Box-drawn border with a title and `×` close button in the header row
- A horizontal separator below the header
- Action buttons typically at the bottom
- Semantic role: overlay dialog with header (title + close), body, and footer (action buttons)

### Split Panel

```text
┌───────┬──────────────┐
│       │              │
│       │              │
│       │              │
│       │              │
└───────┴──────────────┘
```

- Two-column box with `┬` at the top divider and `┴` at the bottom divider
- Left and right panels can contain any content
- Semantic role: two-column layout with resizable split

## Data

### Table

```text
┌─────────┬─────────┬─────────┐
│ Col A   │ Col B   │ Col C   │
├─────────┼─────────┼─────────┤
│         │         │         │
│         │         │         │
└─────────┴─────────┴─────────┘
```

- Box-drawn grid with column headers in the first row
- `├──┼──┤` separator between header and body rows
- Default columns: `Col A, Col B, Col C`, default column width: 10
- Semantic role: tabular data with headers and rows

### List

```text
• Item 1
• Item 2
• Item 3
• Item 4
• Item 5
```

- Bullet character `•` followed by item text
- Default width: 15, default height: 5 items
- Semantic role: unordered list of items

## Drawing Elements

### Box

```text
┌──────────────┐
│              │
│   Content    │
│              │
└──────────────┘
```

- Simple box-drawn rectangle
- May contain text content centered or aligned
- Semantic role: generic container or section boundary

### Placeholder / Image

```text
┌────────┐
│\      /│
│  \  /  │
│  IMG\  │
│/      \│
└────────┘
```

- Box with diagonal crosshatch lines (`\` and `/`) and `IMG` text
- Represents an image placeholder
- Semantic role: image or media placeholder with dimensions

### Text

Plain text blocks without borders. Semantic role depends on size and position: heading, paragraph, or label.

### Line

```text
──────────────
```

- Horizontal line using `─` characters
- Semantic role: visual separator or divider

### Arrow

```text
──────────>
<──────────
```

- Horizontal line with arrowhead (`>` or `<`)
- Indicates flow or direction; usually decorative rather than an interactive element

---

## Draw Tools

Draw tools produce freehand ASCII art on the canvas. Each stroke creates a "Stroke" layer. These patterns
represent decorative or structural elements rather than interactive UI components. When interpreting a wireframe
that contains these characters, treat them as visual embellishment or layout emphasis.

### Pencil

```text
█████████
█       █
█       █
█████████
```

- Character: `█` (U+2588, Full Block)
- Drag-based, 1x1 brush width
- Interpret as: decorative border, separator, or structural element

### Brush

```text
░░░░░░░░░░░░░
▓▓▓▓▓▓▓▓▓▓▓▓▓
█████████████
▓▓▓▓▓▓▓▓▓▓▓▓▓
░░░░░░░░░░░░░
```

- Characters: `░` (light shade) at edges, `▓` (dark shade) middle, `█` (full block) center
- Creates a gradient soft-edge stroke, 3-4 rows wide
- Interpret as: decorative divider, emphasis area, or background shading

### Spray

```text
   ▒░
▓ ▓ ▒    ∙
   ░
```

- Characters: `░` `▒` `▓` `∙` (U+2219, Bullet Operator) scattered randomly
- Configurable radius (default 3) and density (default 5)
- Interpret as: texture, noise, or decorative background pattern

### Shade

```text
░░░░░░░░░░
░░░░░░░░░░
```

- Character: `░` (U+2591, Light Shade) exclusively
- Drag-based, 1x1 brush width (like Pencil but with shade character)
- Interpret as: background area, inactive region, or placeholder surface

### Fill

```text
┌─────────────────┐
│█████████████████│
│█████████████████│
│█████████████████│
└─────────────────┘
```

- Character: `█` (U+2588, Full Block) flood fill
- Click-based (not drag); fills inside enclosed areas
- Interpret as: solid background, filled container, or active/highlighted area

### Smudge

- Does not produce new characters; **moves existing characters** in the drag direction
- Displaces content, leaving gaps in the original position
- Rarely appears in exported wireframes; treat displaced characters in their new positions

### Scatter

```text
★
   ☆
♥
         ✴
      ♠
         ✦
```

- Characters: `★` `☆` `♥` `♠` `✴` `✦` (random decorative Unicode symbols)
- Sparse random placement along drag path
- Interpret as: decorative elements, ratings (stars), or iconographic indicators

### Eraser

- Removes characters visually on the canvas
- **Does not affect markdown export**; erased content still appears in exported output
- This is a known Mockdown behavior; the eraser operates at the render layer only

### Draw Tool Summary

| Tool    | Characters       | Method | Brush    | Notes                  |
|---------|------------------|--------|----------|------------------------|
| Pencil  | `█`              | Drag   | 1x1      | Solid line             |
| Brush   | `░▓█` gradient   | Drag   | 3-4 rows | Soft-edge stroke       |
| Spray   | `░▒▓∙` random    | Drag   | Radius   | Configurable density   |
| Shade   | `░`              | Drag   | 1x1      | Light shade only       |
| Fill    | `█` flood        | Click  | N/A      | Fills enclosed areas   |
| Smudge  | (moves existing) | Drag   | 1x1      | Displaces characters   |
| Scatter | `★☆♥♠✴✦` random  | Drag   | Sparse   | Decorative symbols     |
| Eraser  | (visual only)    | Drag   | 1x1      | Does not affect export |
