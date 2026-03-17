# MCP Element Templates

Copy-paste skeleton templates for the Excalidraw MCP `create_view` tool. All templates include our enforced
defaults (`roughness: 0`, `opacity: 100`). Replace placeholder values (`id`, `x`, `y`, colors, text) before use.

## Camera Update

```json
{ "type": "cameraUpdate", "width": 800, "height": 600, "x": 0, "y": 0 }
```

**All standard sizes:**

```json
{ "type": "cameraUpdate", "width": 400, "height": 300, "x": 0, "y": 0 }
{ "type": "cameraUpdate", "width": 600, "height": 450, "x": 0, "y": 0 }
{ "type": "cameraUpdate", "width": 800, "height": 600, "x": 0, "y": 0 }
{ "type": "cameraUpdate", "width": 1200, "height": 900, "x": 0, "y": 0 }
{ "type": "cameraUpdate", "width": 1600, "height": 1200, "x": 0, "y": 0 }
```

## Labeled Rectangle

```json
{
  "type": "rectangle", "id": "UNIQUE_ID",
  "x": 100, "y": 100, "width": 200, "height": 80,
  "roughness": 0, "opacity": 100,
  "roundness": { "type": 3 },
  "backgroundColor": "#a5d8ff", "fillStyle": "solid",
  "strokeColor": "#4a9eed", "strokeWidth": 2,
  "label": { "text": "Label Text", "fontSize": 20 }
}
```

## Labeled Ellipse

```json
{
  "type": "ellipse", "id": "UNIQUE_ID",
  "x": 100, "y": 100, "width": 150, "height": 150,
  "roughness": 0, "opacity": 100,
  "backgroundColor": "#b2f2bb", "fillStyle": "solid",
  "strokeColor": "#22c55e", "strokeWidth": 2,
  "label": { "text": "Label Text", "fontSize": 20 }
}
```

## Labeled Diamond

```json
{
  "type": "diamond", "id": "UNIQUE_ID",
  "x": 100, "y": 100, "width": 150, "height": 150,
  "roughness": 0, "opacity": 100,
  "backgroundColor": "#ffd8a8", "fillStyle": "solid",
  "strokeColor": "#f59e0b", "strokeWidth": 2,
  "label": { "text": "Decision?", "fontSize": 18 }
}
```

## Standalone Text (Titles)

```json
{
  "type": "text", "id": "UNIQUE_ID",
  "x": 150, "y": 50,
  "text": "Diagram Title",
  "fontSize": 24,
  "roughness": 0, "opacity": 100,
  "strokeColor": "#1e1e1e"
}
```

## Standalone Text (Annotations)

```json
{
  "type": "text", "id": "UNIQUE_ID",
  "x": 150, "y": 50,
  "text": "annotation text",
  "fontSize": 14,
  "roughness": 0, "opacity": 100,
  "strokeColor": "#757575"
}
```

## Arrow with fixedPoint Bindings

```json
{
  "type": "arrow", "id": "UNIQUE_ID",
  "x": 300, "y": 150, "width": 150, "height": 0,
  "roughness": 0, "opacity": 100,
  "points": [[0,0],[150,0]],
  "strokeColor": "#1e1e1e", "strokeWidth": 2,
  "endArrowhead": "arrow",
  "startBinding": { "elementId": "SOURCE_ID", "fixedPoint": [1, 0.5] },
  "endBinding": { "elementId": "TARGET_ID", "fixedPoint": [0, 0.5] }
}
```

## Arrow with Label

```json
{
  "type": "arrow", "id": "UNIQUE_ID",
  "x": 300, "y": 150, "width": 200, "height": 0,
  "roughness": 0, "opacity": 100,
  "points": [[0,0],[200,0]],
  "strokeColor": "#1e1e1e", "strokeWidth": 2,
  "endArrowhead": "arrow",
  "label": { "text": "label text", "fontSize": 14 },
  "startBinding": { "elementId": "SOURCE_ID", "fixedPoint": [1, 0.5] },
  "endBinding": { "elementId": "TARGET_ID", "fixedPoint": [0, 0.5] }
}
```

## Vertical Arrow (Top to Bottom)

```json
{
  "type": "arrow", "id": "UNIQUE_ID",
  "x": 200, "y": 200, "width": 0, "height": 120,
  "roughness": 0, "opacity": 100,
  "points": [[0,0],[0,120]],
  "strokeColor": "#1e1e1e", "strokeWidth": 2,
  "endArrowhead": "arrow",
  "startBinding": { "elementId": "SOURCE_ID", "fixedPoint": [0.5, 1] },
  "endBinding": { "elementId": "TARGET_ID", "fixedPoint": [0.5, 0] }
}
```

## Dashed Lifeline (Sequence Diagrams)

```json
{
  "type": "arrow", "id": "UNIQUE_ID",
  "x": 200, "y": 100, "width": 0, "height": 500,
  "roughness": 0, "opacity": 100,
  "points": [[0,0],[0,500]],
  "strokeColor": "#b0b0b0", "strokeWidth": 1,
  "strokeStyle": "dashed",
  "endArrowhead": null
}
```

## Delete

```json
{ "type": "delete", "ids": "id1,id2,id3" }
```

## Restore Checkpoint

```json
{ "type": "restoreCheckpoint", "id": "CHECKPOINT_ID" }
```

Must be the first element in the array. Append new elements after it.

## Dark Mode Background

```json
{
  "type": "rectangle", "id": "darkbg",
  "x": -4000, "y": -3000, "width": 10000, "height": 7500,
  "backgroundColor": "#1e1e2e", "fillStyle": "solid",
  "strokeColor": "transparent", "strokeWidth": 0
}
```

Place as the FIRST element, before any `cameraUpdate`.

## Background Zone (Layered Sections)

```json
{
  "type": "rectangle", "id": "UNIQUE_ID",
  "x": 50, "y": 80, "width": 500, "height": 350,
  "roughness": 0, "opacity": 35,
  "backgroundColor": "#dbe4ff", "fillStyle": "solid",
  "roundness": { "type": 3 },
  "strokeColor": "#4a9eed", "strokeWidth": 1
}
```

Zone colors: `#dbe4ff` (UI/frontend), `#e5dbff` (logic/agent), `#d3f9d8` (data/tool).

## Note/Callout Box

```json
{
  "type": "rectangle", "id": "UNIQUE_ID",
  "x": 100, "y": 100, "width": 300, "height": 30,
  "roughness": 0, "opacity": 50,
  "backgroundColor": "#fff3bf", "fillStyle": "solid",
  "roundness": { "type": 3 },
  "strokeColor": "#f59e0b", "strokeWidth": 1,
  "label": { "text": "Note text here", "fontSize": 14 }
}
```
