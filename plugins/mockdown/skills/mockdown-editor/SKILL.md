---
name: mockdown-editor
description: >-
  Internal skill for agents that need to drive the Mockdown editor via Chrome browser automation.
  Contains DOM selectors, toolbar structure, component placement mechanics, keyboard shortcuts, and
  automation patterns for the Mockdown ASCII wireframe editor. Not intended for direct user triggering.
disable-model-invocation: true
user-invocable: false
---

# Mockdown Editor Automation

This skill teaches agents how to interact with the Mockdown ASCII wireframe editor at<https://www.mockdown.design/> 
via Chrome browser extension tools.

## Key Actions

- **Select a tool**: use `find` to locate the toolbar button by label, then `computer` to click it
- **Place a component**: select the tool, then click on the canvas (all single-click, no drag-sizing)
- **Edit text**: select the component, then use `find` to locate the label input in the Inspect panel
  (right sidebar); use `form_input` to change the value
- **Move a component**: arrow keys (1 grid unit per press) or edit X/Y in the Inspect panel
- **Export**: find and click the "Copy Markdown" button; read clipboard via `javascript_tool` with
  `navigator.clipboard.readText()`

## Critical Notes

- The canvas is an HTML `<canvas>` element; placed components have no DOM representation
- There is no inline text editing; all editing goes through the Inspect panel
- There is no drag-to-move; use arrow keys or Inspect panel X/Y inputs
- Toolbar buttons have no IDs or aria-labels; find them by text content
- The "more" toggles in UI Elements and Draw groups expand items inline (not popovers)
- The eraser is cosmetic only and does not affect markdown export
- After placing a component, the tool auto-switches back to Select mode

## Editor Layout

The interface has three main areas:

1. **Left sidebar** (`aside:first-of-type`): toolbar with tool selection and component palette
2. **Canvas** (center): an HTML `<canvas>` element rendering the 80x40 character grid
3. **Right sidebar** (`aside:nth-of-type(2)`): Layers panel and Inspect panel

All wireframe content is rendered via the Canvas 2D API. There are no DOM elements for placed components;
everything is drawn on the `<canvas>`. The canvas renders at 2x resolution for retina displays.

## DOM Selectors

### Selector Strategy

Toolbar buttons have no `data-*` attributes, no `aria-label`, and no `id`. The most reliable way to target
them is by **text content** within the left sidebar: `aside:first-of-type button` filtered by
`textContent.trim()`.

Use `mcp__claude-in-chrome__find` with the button's text label to locate it.

### Key Selectors

| Element           | Selector                                               |
|-------------------|--------------------------------------------------------|
| Left sidebar      | `aside:first-of-type`                                  |
| Right sidebar     | `aside:nth-of-type(2)`                                 |
| Canvas element    | `.relative.overflow-auto.flex-1 canvas.block`          |
| Canvas container  | `.relative.overflow-auto.flex-1`                       |
| Undo button       | `button[title="Undo (Ctrl+Z)"]`                        |
| Redo button       | `button[title="Redo (Ctrl+Shift+Z)"]`                  |
| Clear Canvas      | `button[title="Clear Canvas"]`                         |
| Toggle grid lines | `button[title="Toggle grid lines"]`                    |
| Dark mode         | `button[title="Dark mode"]`                            |
| Copy Markdown     | Button containing text "Copy Markdown" in left sidebar |

## Toolbar Structure

The left sidebar contains three tool groups. Each group has primary buttons (always visible) and a "more"
toggle that expands additional buttons inline (not a popover).

### Basics Group

All direct buttons, always visible:

| Label  | Position in group |
|--------|-------------------|
| Select | nth-child(2)      |
| Text   | nth-child(3)      |
| Box    | nth-child(4)      |
| Line   | nth-child(5)      |
| Arrow  | nth-child(6)      |

### UI Elements Group

Primary buttons (always visible):

| Label  | Position in group |
|--------|-------------------|
| Button | nth-child(2)      |
| Input  | nth-child(3)      |
| Card   | nth-child(4)      |
| Table  | nth-child(5)      |
| Modal  | nth-child(6)      |

Click "more" (nth-child(7)) to expand additional components inline:

| Label       | Position after expand |
|-------------|-----------------------|
| Checkbox    | nth-child(8)          |
| Radio       | nth-child(9)          |
| Dropdown    | nth-child(10)         |
| Toggle      | nth-child(11)         |
| Tabs        | nth-child(12)         |
| Search      | nth-child(13)         |
| Progress    | nth-child(14)         |
| Breadcrumb  | nth-child(15)         |
| Pagination  | nth-child(16)         |
| Nav Bar     | nth-child(17)         |
| List        | nth-child(18)         |
| Placeholder | nth-child(19)         |
| HSplit      | nth-child(20)         |
| Image       | nth-child(21)         |

### Draw Group

Primary buttons (always visible):

| Label  | Position in group |
|--------|-------------------|
| Pencil | nth-child(2)      |
| Eraser | nth-child(3)      |

Click "more" (nth-child(4)) to expand:

| Label   | Position after expand |
|---------|-----------------------|
| Brush   | nth-child(5)          |
| Spray   | nth-child(6)          |
| Shade   | nth-child(7)          |
| Fill    | nth-child(8)          |
| Smudge  | nth-child(9)          |
| Scatter | nth-child(10)         |

### "More" Toggle Behavior

Both "more" buttons are **inline toggles**, not popovers. Clicking "more" expands the hidden items as sibling
buttons within the same parent `div`. Clicking "more" again collapses them. After expanding, you must click
"more" before counting child positions for the additional buttons.

## Component Placement

All components use **single-click placement**. No drag-sizing is needed for any component.

1. Click the desired tool button in the toolbar
2. Click on the canvas at the target grid position
3. The component appears at its default size
4. The tool automatically switches back to Select mode after placement

### Default Component Sizes

| Component   | Width x Height | Content Properties                              |
|-------------|----------------|-------------------------------------------------|
| Button      | 6 x 1          | label: "OK"                                     |
| Input       | 13 x 1         | hint: ""                                        |
| Checkbox    | 7 x 1          | label: "Label"                                  |
| Radio       | 7 x 1          | label: "Label"                                  |
| Dropdown    | 14 x 1         | label: "Option"                                 |
| Toggle      | 10 x 1         | label: "Label"                                  |
| Search      | 20 x 1         | hint: "Search..."                               |
| Progress    | 20 x 1         | value: "60"                                     |
| Nav Bar     | 40 x 2         | group with 6 sub-items                          |
| Tabs        | 26 x 2         | group                                           |
| Breadcrumb  | 21 x 1         | items: "Home, Section, Page"                    |
| Pagination  | 19 x 1         | current: "3", total: "10"                       |
| Card        | 20 x 8         | group with 3 sub-items                          |
| Modal       | 30 x 10        | group                                           |
| Table       | 31 x 6         | columns: "Col A, Col B, Col C", rows: "3"       |
| List        | 15 x 5         | items: "Item 1, Item 2, Item 3, Item 4, Item 5" |
| Placeholder | 16 x 6         | label: "Content"                                |
| HSplit      | 24 x 8         | group                                           |
| Image       | 10 x 6         | stroke type, 71 cells                           |

## Editing Components

**There is no inline text editing on the canvas.** All editing happens through the **Inspect panel** in the
right sidebar.

### Editing via Inspect Panel

1. Select a component (click it on canvas or click its layer in the Layers panel)
2. The Inspect panel populates with the component's properties
3. Edit properties using the form inputs:
   - **Layer section**: Name (editable text input)
   - **Frame section**: X, Y, Width, Height (number inputs)
   - **Content section**: varies by component type (Label, Hint, Columns, Rows, Items, Value, etc.)
4. Changes apply immediately to the canvas rendering

### Editing via Backspace

When a component is selected and focus is on the canvas, pressing Backspace removes the last character of
the component's label text. This provides a quick way to shorten labels without using the Inspect panel.

## Moving and Resizing

**There is no drag-to-move.** Components are repositioned using:

- **Arrow keys**: Up/Down/Left/Right moves the selected component by 1 grid unit per press
- **Inspect panel**: directly edit the X and Y number inputs in the Frame section

Resizing is done via the **Width and Height inputs** in the Inspect panel's Frame section.

## Layers Panel

Located in the right sidebar (`aside:nth-of-type(2)`), first section.

- Lists all placed components with an icon and name
- Shows a count badge in the header
- Click a layer entry to select it on the canvas and populate the Inspect panel
- Groups (Nav Bar, Card, Modal, HSplit) show a disclosure triangle to expand/collapse child layers
- Each layer entry has a visibility toggle button (`title="Hide layer"`)

**Layer entry selector**: `div` elements with class pattern
`group relative flex h-7 items-center gap-1.5 rounded-md pr-1 text-xs`. Selected layers gain
`bg-[#2563eb]/10 text-foreground`.

## Inspect Panel

Located in the right sidebar, second section. Shows editable properties for the selected component.

### Sections

- **Layer**: Type (read-only), Name (editable text input)
- **Frame**: X, Y, Width, Height (number inputs)
- **Content**: varies by component type

### Input Selector Pattern

All label/input pairs use: `grid grid-cols-[56px_minmax(0,1fr)] items-center gap-2 text-xs`.
Input elements use: `w-full rounded-md border border-border/70 bg-background px-2 py-1.5 text-xs`.

## Exporting

1. Click the **"Copy Markdown"** button (bottom of the left sidebar, blue button with white text)
2. The ASCII wireframe is copied **directly to the clipboard** as a markdown code fence; no dialog appears
3. A brief "Copied!" toast appears at the bottom-right corner
4. The markdown content is not stored in the DOM; read it via `navigator.clipboard.readText()`

## Keyboard Shortcuts

| Shortcut                     | Behavior                                               |
|------------------------------|--------------------------------------------------------|
| Delete (fn+Backspace on Mac) | Delete selected component                              |
| Backspace                    | Remove last character of selected component's label    |
| Cmd+Z / Ctrl+Z               | Undo                                                   |
| Cmd+Shift+Z / Ctrl+Shift+Z   | Redo                                                   |
| Cmd+C / Ctrl+C               | Copy canvas markdown to clipboard (not component copy) |
| Cmd+V / Ctrl+V               | Paste clipboard text as a "Pasted Text" layer          |
| Escape                       | Deselect current component; switch to Select tool      |
| Arrow keys                   | Move selected component by 1 grid unit per press       |

No single-letter tool shortcuts (V, T, B, etc.) are available; these keys type into the canvas. No keyboard
shortcut help panel exists in the editor.

## Automation Patterns

### Opening the Editor

```text
1. Use tabs_context_mcp to get current tabs
2. Use tabs_create_mcp to create a new tab
3. Use navigate to open https://www.mockdown.design/
4. Wait for the canvas to render before interacting
```

### Selecting a Tool

```text
1. Use find to locate the toolbar button by its text label (e.g., "Button", "Card", "Table")
2. For "more" items: first find and click "more" to expand, then find the target button
3. Use computer with click action to select the tool
```

### Placing a Component

```text
1. Select the desired tool (see above)
2. Use computer with click action on the canvas element at the desired position
3. The component appears at default size; the tool auto-switches to Select mode
```

### Editing a Component

```text
1. Select the component by clicking it on canvas or clicking its layer in the Layers panel
2. Use find to locate the desired input in the Inspect panel (e.g., the "label" input)
3. Use form_input to change the value
4. The canvas updates immediately
```

### Moving a Component

```text
Option A (arrow keys):
1. Select the component
2. Use computer with key action to press arrow keys repeatedly

Option B (Inspect panel):
1. Select the component
2. Use find to locate the X or Y input in the Inspect panel
3. Use form_input to set the new coordinate value
```

### Exporting the Wireframe

```text
1. Use find to locate the "Copy Markdown" button
2. Click it with computer
3. Use javascript_tool to read clipboard: navigator.clipboard.readText()
4. The result is the wireframe wrapped in a markdown code fence
```

### Reading Canvas State

Since the canvas is a `<canvas>` element with no DOM representation of components, read the current state
by checking the Layers panel entries or by exporting the markdown.

### Recording a Design Session

```text
Use gif_creator to record the design process.
Capture extra frames before and after actions for smooth playback.
Name the file meaningfully (e.g., "login-wireframe-design.gif").
```

## Tips

- Wait for page load before interacting; the canvas renders after JavaScript initializes
- All toolbar buttons must be found by text content since they lack stable selectors
- After placing a component, the tool auto-switches to Select mode; no need to manually switch back
- Components cannot be moved by dragging; use arrow keys or Inspect panel X/Y inputs
- Text cannot be edited by double-clicking; use the Inspect panel's Content section inputs
- Use the Layers panel to verify components were placed correctly
- Use `navigator.clipboard.readText()` via `javascript_tool` to read exported markdown
- The canvas trims trailing whitespace per line on export
- Group components (Nav Bar, Card, Modal, HSplit) have child layers; expand them in the Layers panel
  to access individual sub-components
