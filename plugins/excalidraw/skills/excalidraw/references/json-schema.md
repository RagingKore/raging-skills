# Excalidraw JSON Schema

Quick-reference for element types, common properties, and bindings. For full details with
examples, see `json-format.md`.

## Element Types

| Type        | Use For                             |
|-------------|-------------------------------------|
| `rectangle` | Processes, actions, components      |
| `ellipse`   | Entry/exit points, external systems |
| `arrow`     | Connections between shapes          |
| `text`      | Labels inside shapes                |
| `line`      | Non-arrow connections               |
| `frame`     | Grouping containers                 |

**Diamond is banned** — see `json-format.md` for details and alternatives.

## Common Properties

All elements share these:

| Property          | Type   | Description                        |
|-------------------|--------|------------------------------------|
| `id`              | string | Unique identifier                  |
| `type`            | string | Element type                       |
| `x`, `y`          | number | Position in pixels                 |
| `width`, `height` | number | Size in pixels                     |
| `strokeColor`     | string | Border color (hex)                 |
| `backgroundColor` | string | Fill color (hex or "transparent")  |
| `fillStyle`       | string | "solid", "hachure", "cross-hatch"  |
| `strokeWidth`     | number | 1, 2, or 4                         |
| `strokeStyle`     | string | "solid", "dashed", "dotted"        |
| `roughness`       | number | 0 (default), 1 (hand-drawn)        |
| `opacity`         | number | 0-100 (default: 100)               |
| `seed`            | number | Random seed for roughness          |

## Text-Specific Properties

| Property        | Description                        |
|-----------------|------------------------------------|
| `text`          | The display text                   |
| `originalText`  | Same as text                       |
| `fontSize`      | Size in pixels (16-20 recommended) |
| `fontFamily`    | 3 for monospace (default)          |
| `textAlign`     | "left", "center", "right"          |
| `verticalAlign` | "top", "middle", "bottom"          |
| `containerId`   | ID of parent shape                 |

## Arrow-Specific Properties

| Property         | Description                             |
|------------------|-----------------------------------------|
| `points`         | Array of [x, y] coordinates             |
| `startBinding`   | Connection to start shape               |
| `endBinding`     | Connection to end shape                 |
| `startArrowhead` | null, "arrow", "bar", "dot", "triangle" |
| `endArrowhead`   | null, "arrow", "bar", "dot", "triangle" |

## Binding Format

```json
{
  "elementId": "shapeId",
  "focus": 0,
  "gap": 2
}
```

## Rectangle Roundness

Add for rounded corners:

```json
"roundness": { "type": 3 }
```
