# MCP Element Format

Reference for the Excalidraw MCP `create_view` element format. Elements are a JSON array (no file wrapper)
processed by `convertToExcalidrawElements` via the skeleton API.

## Required Fields

Every element needs: `type`, `id` (unique string), `x`, `y`, `width`, `height`.

## Our Enforced Defaults

Override these on every element (MCP defaults differ):

| Property     | Value | Why                          |
|--------------|-------|------------------------------|
| `roughness`  | `0`   | Clean, crisp edges           |
| `fontFamily` | `3`   | Cascadia (monospace)         |
| `opacity`    | `100` | No transparency; use color   |

Note: the MCP widget overrides `fontFamily` to Excalifont (5) at render time. Set `fontFamily: 3` in the JSON
anyway to signal intent; if the MCP ever stops overriding, the correct value is already in place.

## Element Types

### Shapes

**Rectangle**

```json
{ "type": "rectangle", "id": "r1", "x": 100, "y": 100, "width": 200, "height": 100,
  "roughness": 0, "opacity": 100,
  "roundness": { "type": 3 }, "backgroundColor": "#a5d8ff", "fillStyle": "solid",
  "label": { "text": "My Label", "fontSize": 20 } }
```

**Ellipse**

```json
{ "type": "ellipse", "id": "e1", "x": 100, "y": 100, "width": 150, "height": 150,
  "roughness": 0, "opacity": 100,
  "backgroundColor": "#b2f2bb", "fillStyle": "solid",
  "label": { "text": "Start", "fontSize": 20 } }
```

**Diamond** (allowed in MCP; connections work correctly)

```json
{ "type": "diamond", "id": "d1", "x": 100, "y": 100, "width": 150, "height": 150,
  "roughness": 0, "opacity": 100,
  "backgroundColor": "#ffd8a8", "fillStyle": "solid",
  "label": { "text": "Decision?", "fontSize": 18 } }
```

### Text

**Standalone text** (titles, annotations only; for shape labels use `label` shorthand):

```json
{ "type": "text", "id": "t1", "x": 150, "y": 50, "text": "Architecture Overview", "fontSize": 24,
  "roughness": 0, "opacity": 100, "strokeColor": "#1e1e1e" }
```

Positioning: `x` is the LEFT edge. To center at position `cx`: set `x = cx - (text.length * fontSize * 0.5) / 2`.

### Arrows

```json
{ "type": "arrow", "id": "a1", "x": 300, "y": 150, "width": 200, "height": 0,
  "roughness": 0, "opacity": 100,
  "points": [[0,0],[200,0]], "endArrowhead": "arrow",
  "startBinding": { "elementId": "r1", "fixedPoint": [1, 0.5] },
  "endBinding": { "elementId": "r2", "fixedPoint": [0, 0.5] } }
```

**Labeled arrow:**

```json
{ "type": "arrow", "id": "a2", "x": 300, "y": 150, "width": 200, "height": 0,
  "roughness": 0, "opacity": 100,
  "points": [[0,0],[200,0]], "endArrowhead": "arrow",
  "label": { "text": "sends data", "fontSize": 14 } }
```

### Lines

```json
{ "type": "line", "id": "l1", "x": 100, "y": 100, "width": 0, "height": 300,
  "roughness": 0, "opacity": 100,
  "points": [[0,0],[0,300]], "strokeColor": "#b0b0b0", "strokeWidth": 1, "strokeStyle": "dashed" }
```

### Frames

```json
{ "type": "frame", "children": ["r1", "r2", "a1"], "name": "API Layer" }
```

Frame dimensions auto-calculate from children if `x`/`y`/`width`/`height` are omitted.

## Label Shorthand

Add `label` to any shape or arrow for auto-centered text. No separate text element needed.

```json
{ "type": "rectangle", "id": "svc", "x": 100, "y": 100, "width": 200, "height": 80,
  "label": { "text": "API Gateway", "fontSize": 20 } }
```

- Works on `rectangle`, `ellipse`, `diamond`, and `arrow`
- Text auto-centers; container auto-resizes to fit
- Saves tokens vs. separate text elements with `containerId`

## Arrow Bindings (fixedPoint)

Bind arrow endpoints to shapes using normalized coordinates:

| Edge   | fixedPoint   |
|--------|--------------|
| Top    | `[0.5, 0]`   |
| Bottom | `[0.5, 1]`   |
| Left   | `[0, 0.5]`   |
| Right  | `[1, 0.5]`   |

```json
"startBinding": { "elementId": "source-box", "fixedPoint": [1, 0.5] },
"endBinding": { "elementId": "target-box", "fixedPoint": [0, 0.5] }
```

No manual edge-point calculations needed; the MCP resolves coordinates from `fixedPoint`.

## Pseudo-Elements

### cameraUpdate (viewport control)

Not drawn. Controls the camera position with smooth animation.

```json
{ "type": "cameraUpdate", "width": 800, "height": 600, "x": 0, "y": 0 }
```

- `x`, `y`: top-left corner of visible area (scene coordinates)
- `width`, `height`: visible area size. **Must be 4:3 ratio**
- Always emit BEFORE the elements it frames
- Use multiple `cameraUpdate` entries to guide attention as you draw

**Camera sizes (4:3 only):**

| Size | Width | Height | Use                                                             |
|------|-------|--------|-----------------------------------------------------------------|
| S    | 400   | 300    | Close-up on 2-3 elements                                       |
| M    | 600   | 450    | Medium view, a diagram section                                  |
| L    | 800   | 600    | Standard full diagram (default)                                 |
| XL   | 1200  | 900    | Large overview. Min readable font: 18                           |
| XXL  | 1600  | 1200   | Panorama for complex diagrams. Min readable font: 21            |

### delete (remove elements)

```json
{ "type": "delete", "ids": "b2,a1,t3" }
```

- Comma-separated list of element IDs to remove
- Also removes bound text elements (matching `containerId`)
- Place AFTER the elements you want to remove
- Never reuse a deleted ID; always assign new IDs to replacements

### restoreCheckpoint (resume from saved state)

```json
{ "type": "restoreCheckpoint", "id": "<checkpointId>" }
```

- Must be the FIRST element in the array
- Loads the saved state (including user edits made in fullscreen)
- New elements after it are appended on top
- Saves tokens by not resending the entire diagram

## Font Size Rules

| Context              | Minimum fontSize |
|----------------------|------------------|
| Body text, labels    | 16               |
| Titles, headings     | 20               |
| Secondary annotations| 14 (sparingly)   |
| XL camera            | 18               |
| XXL camera           | 21               |

Never use fontSize below 14.

## Element Sizing

- Minimum shape size for labeled rectangles/ellipses: 120x60
- Leave 20-30px gaps between elements minimum
- Prefer fewer, larger elements over many tiny ones

## Progressive Drawing Order

Array order equals z-order (first = back, last = front). Emit progressively for streaming:

**Good:** bg zone -> shape1 -> label1 -> arrow1 -> shape2 -> label2 -> arrow2

**Bad:** all rectangles -> all texts -> all arrows

This ensures the viewer sees coherent groups appear during streaming, not disconnected shapes.

## Dark Mode

Use a massive dark background rectangle as the FIRST element (before `cameraUpdate`), 10x the camera size:

```json
{ "type": "rectangle", "id": "darkbg", "x": -4000, "y": -3000, "width": 10000, "height": 7500,
  "backgroundColor": "#1e1e2e", "fillStyle": "solid", "strokeColor": "transparent", "strokeWidth": 0 }
```

**Text colors on dark background:**

| Color | Hex       | Use                              |
|-------|-----------|----------------------------------|
| White | `#e5e5e5` | Primary text, titles             |
| Muted | `#a0a0a0` | Secondary text, annotations      |

Never use `#555` or darker on dark backgrounds.

**Shape fills on dark background:**

| Color       | Hex       | Use                |
|-------------|-----------|--------------------|
| Dark Blue   | `#1e3a5f` | Primary nodes      |
| Dark Green  | `#1a4d2e` | Success, output    |
| Dark Purple | `#2d1b69` | Processing, special|
| Dark Orange | `#5c3d1a` | Warning, pending   |
| Dark Red    | `#5c1a1a` | Error, critical    |
| Dark Teal   | `#1a4d4d` | Storage, data      |

For strokes/arrows on dark: use primary colors (bright enough on dark backgrounds).

## Text Contrast Rules

- Minimum text color on white background: `#757575`
- For colored text on light fills, use dark variants (`#15803d` not `#22c55e`)
- White text needs dark backgrounds (`#9a5030` not `#c4795b`)
- Never use light gray (`#b0b0b0`, `#999`) on white

## Arrowhead Types

| Value              | Visual      |
|--------------------|-------------|
| `null`             | No head     |
| `"arrow"`          | Standard    |
| `"bar"`            | Bar/stop    |
| `"dot"`            | Dot         |
| `"triangle"`       | Filled tri  |
| `"diamond"`        | Diamond     |
| `"crowfoot_one"`   | ER one      |
| `"crowfoot_many"`  | ER many     |

## Stroke Styles

| Style      | Use                           |
|------------|-------------------------------|
| `"solid"`  | Default connections           |
| `"dashed"` | Optional, async, return paths |
| `"dotted"` | Weak associations             |
