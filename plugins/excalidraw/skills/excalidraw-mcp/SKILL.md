---
name: excalidraw-mcp
description: Create diagrams with live browser preview using the Excalidraw MCP server. Use when MCP tools (mcp__excalidraw__*) are available AND the user asks for diagrams, visualizations, flowcharts, architecture, or system designs. Prefer this over raw JSON generation when the MCP server is connected.
---

# Excalidraw MCP Diagram Generator

Create diagrams with **live browser preview** via the Excalidraw MCP server. Diagrams render inline as the
elements stream, with camera animations guiding attention.

## When to Use This Skill

Use this skill when **both** conditions are true:

1. Excalidraw MCP tools are available (`mcp__excalidraw__create_view`, `mcp__excalidraw__read_me`, etc.)
2. The user asks for diagrams, visualizations, flowcharts, architecture, or system designs

If MCP tools are not available, fall back to the raw JSON `excalidraw` skill.

## Mandatory First Step: read_me

Call `mcp__excalidraw__read_me` **once per conversation** before the first `create_view`. This returns the
element format reference. Do not call it again after the first time.

## Color Palette Initialization

The plugin's `settings.json` defines where the color palette lives via `colorPalettePath` (default:
`.excalidraw/color-palette.yml`). Before generating any diagram, check if that file exists in the project.

**If palette file exists:** read it. Use it as the single source of truth for all color choices.

**If palette file does NOT exist:** this is first-time setup:

1. Read `colorPalettePath` from the plugin's `settings.json`
2. Use the **AskUserQuestion** tool to confirm:
   > "I'll create your Excalidraw color palette at `<path>`. This YAML file controls all diagram colors and you can
   > edit it anytime to match your brand. OK?"
3. Read the built-in template from `../excalidraw/references/color-palette-template.yml`
4. Write it to the configured path
5. Continue with diagram generation using the palette

For color system rules and design guidance, see `../excalidraw/references/color-palette.md`.

## Enforced Defaults

Override these on every element (MCP defaults differ from ours):

| Property     | Value | Why                          |
|--------------|-------|------------------------------|
| `roughness`  | `0`   | Clean, crisp edges           |
| `fontFamily` | `3`   | Cascadia monospace           |
| `opacity`    | `100` | No transparency; use color   |

## Unified Workflow

### Step 0: Assess Depth

Before anything else, determine the diagram type:

- **Simple/Conceptual**: abstract shapes, labels, relationships (mental models)
- **Comprehensive/Technical**: concrete examples, code snippets, real data

If comprehensive, research first. See `../excalidraw/references/design-methodology.md` for depth assessment.

### Step 1: Research

For architecture diagrams, discover components from the codebase. For non-codebase diagrams, research the topic
deeply before designing. See `../excalidraw/references/design-methodology.md` for the research mandate.

### Step 2: Design Layout

Map concepts to visual patterns. Each major concept should use a different pattern. See
`../excalidraw/references/design-methodology.md` for the visual pattern library.

Plan the camera strategy:

- Start with a close-up (S or M) on the title or first element group
- Pan/zoom to each section as you draw it
- End with a full overview (L, XL, or XXL)

### Step 3: Build with create_view

Call `mcp__excalidraw__create_view` with a JSON array of elements. Key rules:

**Camera first.** Always start with a `cameraUpdate` as the first element:

```json
{ "type": "cameraUpdate", "width": 800, "height": 600, "x": 0, "y": 0 }
```

Camera sizes must be 4:3 ratio: S (400x300), M (600x450), L (800x600), XL (1200x900), XXL (1600x1200).

**Labels use shorthand.** Add `label` directly on shapes and arrows. No separate text elements needed:

```json
{ "type": "rectangle", "id": "svc", "x": 100, "y": 100, "width": 200, "height": 80,
  "roughness": 0, "opacity": 100,
  "roundness": { "type": 3 }, "backgroundColor": "#a5d8ff", "fillStyle": "solid",
  "label": { "text": "API Gateway", "fontSize": 20 } }
```

**Diamonds are allowed.** The MCP handles arrow connections to diamonds correctly.

**Arrows use fixedPoint bindings.** No manual edge calculations:

```json
"startBinding": { "elementId": "source", "fixedPoint": [1, 0.5] },
"endBinding": { "elementId": "target", "fixedPoint": [0, 0.5] }
```

fixedPoint values: top `[0.5, 0]`, bottom `[0.5, 1]`, left `[0, 0.5]`, right `[1, 0.5]`.

**Progressive drawing order.** Emit elements in visual groups for streaming:

```text
bg zone -> shape1 -> its label -> its arrows -> shape2 -> its label -> ...
```

Not: all rectangles -> all texts -> all arrows.

**Use camera animations.** Insert `cameraUpdate` entries between sections to guide the viewer's attention.
Camera animates smoothly between positions. Users love this.

**Font size rules:**

| Context               | Minimum fontSize |
|-----------------------|------------------|
| Body text, labels     | 16               |
| Titles, headings      | 20               |
| Secondary annotations | 14 (sparingly)   |
| XL camera             | 18               |
| XXL camera            | 21               |

**Element sizing:** minimum 120x60 for labeled shapes. Leave 20-30px gaps between elements.

### Step 4: Iterate via Visual Feedback

After `create_view`, the diagram renders in the browser. Review the visual output:

- Are elements overlapping?
- Are labels readable?
- Is the camera framing appropriate?
- Are arrow connections correct?

Fix issues by calling `create_view` again with corrections. For modifications to an existing diagram, use
`restoreCheckpoint` + `delete` instead of resending everything (see below).

Optionally, render to PNG for additional validation using the render pipeline in
`../excalidraw/references/render_excalidraw.py`.

### Step 5: Export

When the diagram is complete, call `mcp__excalidraw__export_to_excalidraw` to generate a shareable Excalidraw URL.

## Checkpoint and Iterative Editing

Every `create_view` call returns a `checkpointId`. To modify an existing diagram:

```json
[
  { "type": "restoreCheckpoint", "id": "<checkpointId>" },
  { "type": "delete", "ids": "old-box,old-arrow" },
  { "type": "rectangle", "id": "new-box", "x": 100, "y": 100, "width": 200, "height": 80,
    "roughness": 0, "opacity": 100,
    "label": { "text": "Replacement", "fontSize": 20 } }
]
```

This loads the saved state, removes specified elements, and appends new ones. Saves tokens by not resending
the full diagram.

**Rules:**

- `restoreCheckpoint` must be the FIRST element
- Never reuse a deleted ID; assign new IDs to replacements
- User edits made in fullscreen mode are preserved in the checkpoint

## Dark Mode

Use a massive dark background rectangle as the FIRST element (before `cameraUpdate`), sized 10x the camera:

```json
{ "type": "rectangle", "id": "darkbg", "x": -4000, "y": -3000, "width": 10000, "height": 7500,
  "backgroundColor": "#1e1e2e", "fillStyle": "solid", "strokeColor": "transparent", "strokeWidth": 0 }
```

Then use light text colors (`#e5e5e5` primary, `#a0a0a0` secondary) and dark fills (`#1e3a5f`, `#1a4d2e`, etc.).

## Text Contrast Rules

- Minimum text color on white background: `#757575`
- For colored text on light fills: use dark variants (`#15803d` not `#22c55e`)
- Never use light gray (`#b0b0b0`, `#999`) on white backgrounds
- No emoji in text (Excalidraw fonts do not render them)

## Critical Differences from Raw JSON Skill

| Feature               | Raw JSON skill         | MCP skill (this one)           |
|-----------------------|------------------------|--------------------------------|
| Labels                | Two elements required  | `label` shorthand on shape     |
| Diamonds              | Banned (broken)        | Allowed (MCP handles them)     |
| Arrow routing         | Manual edge math       | `fixedPoint` bindings          |
| Validation            | Render-to-PNG required | Live browser preview           |
| Camera                | Not available          | `cameraUpdate` with animation  |
| Iterative editing     | Edit full JSON file    | `restoreCheckpoint` + `delete` |
| Export                | `.excalidraw` file     | Shareable URL via export tool  |
| Output format         | `.excalidraw` file     | Inline browser preview         |

## Reference Files

| File                                           | Contents                                         |
|------------------------------------------------|--------------------------------------------------|
| `references/mcp-format.md`                     | MCP element format, pseudo-elements, dark mode   |
| `references/mcp-templates.md`                  | Copy-paste MCP element templates                 |
| `references/mcp-examples.md`                   | Full working examples with camera animations     |
| `../excalidraw/references/design-methodology.md` | Design philosophy, visual patterns, large diagrams |
| `../excalidraw/references/color-palette.md`    | Color system rules and design guidance            |
| `../excalidraw/references/color-palette-template.yml` | YAML template copied to project on first run |
| `../excalidraw/references/arrows.md`           | Arrow routing patterns (detailed)                |

## Quick Validation Checklist

- [ ] `roughness: 0` and `opacity: 100` on all elements
- [ ] `cameraUpdate` as first element with 4:3 ratio
- [ ] Camera animations between sections
- [ ] Labels use `label` shorthand (not separate text elements)
- [ ] Arrows use `fixedPoint` bindings
- [ ] Font size >= 16 for body, >= 20 for titles, >= 14 for annotations
- [ ] No emoji in text
- [ ] Text contrast passes (no light text on light backgrounds)
- [ ] Progressive drawing order (groups, not types)
- [ ] No duplicate IDs
