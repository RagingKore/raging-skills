---
name: excalidraw
description: Generate Excalidraw diagram JSON files that make visual arguments. Use when the user asks to create diagrams, visualize workflows, architectures, system designs, or generate excalidraw files.
---

# Excalidraw Diagram Generator

Generate `.excalidraw` JSON files that **argue visually**, not just display information.

## Color Palette Initialization (Mandatory First Step)

The plugin's `settings.json` defines where the color palette lives via `colorPalettePath` (default:
`.excalidraw/color-palette.yml`). Before generating any diagram, you **MUST** check if that file exists in the project.

**If palette file exists** — read it. Use it as the single source of truth for all color choices (fills, strokes,
text colors, evidence artifacts, everything).

**If palette file does NOT exist** — this is first-time setup. Run this procedure:

1. Read `colorPalettePath` from the plugin's `settings.json`
2. Use the **AskUserQuestion** tool to confirm:
   > "I'll create your Excalidraw color palette at `<path from settings>`. This YAML file
   > controls all diagram colors and you can edit it anytime to match your brand. OK?"
3. Read the built-in template from `references/color-palette-template.yml`
4. Write it to the configured path (create directories if needed)
5. Continue with diagram generation using the palette you just wrote

**On all subsequent runs**, the palette file exists, so skip straight to reading it.

For color system rules and design guidance, see `references/color-palette.md`.

---

## Quick Start

**User asks:**

```text
"Generate an architecture diagram for this project"
"Create an excalidraw diagram of the system"
"Visualize this workflow as an excalidraw file"
```

**Claude Code will:**

1. Assess diagram depth (simple vs. comprehensive)
2. Research the system (analyze codebase or specs)
3. Plan layout using visual patterns
4. Generate valid `.excalidraw` JSON with dynamic IDs and labels
5. Render to PNG and validate visually

**No prerequisites:** Works without existing diagrams, Terraform, or specific file types.

---

## Core Philosophy

**Diagrams should ARGUE, not DISPLAY.**

A diagram is a visual argument that shows relationships, causality, and flow that words alone cannot express. The
shape should BE the meaning.

- **Isomorphism Test**: If you removed all text, would the structure alone communicate the concept? If not, redesign.
- **Education Test**: Could someone learn something concrete, or does it just label boxes?

**Full methodology:** See `references/design-methodology.md`

---

## Critical Rules

### 1. NEVER Use Diamond Shapes

Diamond arrow connections are broken in raw Excalidraw JSON. Use styled rectangles instead:

| Semantic Meaning | Rectangle Style                              |
|------------------|----------------------------------------------|
| Orchestrator/Hub | Coral (`#ffa8a8`/`#c92a2a`) + strokeWidth: 3 |
| Decision Point   | Orange (`#ffd8a8`/`#e8590c`) + dashed stroke |

### 2. Labels Require TWO Elements

The `label` property does NOT work in raw JSON. Every labeled shape needs:

```json
// 1. Shape with boundElements reference
{
  "id": "my-box",
  "type": "rectangle",
  "boundElements": [{ "type": "text", "id": "my-box-text" }]
}

// 2. Separate text element with containerId
{
  "id": "my-box-text",
  "type": "text",
  "containerId": "my-box",
  "text": "My Label"
}
```

### 3. Elbow Arrows Need Three Properties

For 90-degree corners (not curved):

```json
{
  "type": "arrow",
  "roughness": 0,
  "roundness": null,
  "elbowed": true
}
```

### 4. Arrow Edge Calculations

Arrows must start/end at shape edges, not centers:

| Edge   | Formula                     |
|--------|-----------------------------|
| Top    | `(x + width/2, y)`          |
| Bottom | `(x + width/2, y + height)` |
| Left   | `(x, y + height/2)`         |
| Right  | `(x + width, y + height/2)` |

**Detailed arrow routing:** See `references/arrows.md`

### 5. Default Element Properties

All elements use these defaults unless specifically overridden:

| Property     | Default | Why                           |
|--------------|---------|-------------------------------|
| `roughness`  | `0`     | Clean, crisp edges            |
| `fontFamily` | `3`     | Monospace for technical text  |
| `opacity`    | `100`   | No transparency; use color    |

---

## Element Types

| Type        | Use For                                        |
|-------------|------------------------------------------------|
| `rectangle` | Services, databases, containers, decision pts  |
| `ellipse`   | Users, external systems, start/end points      |
| `text`      | Labels inside shapes, titles, annotations      |
| `arrow`     | Data flow, connections, dependencies           |
| `line`      | Grouping boundaries, separators, tree trunks   |
| `frame`     | Grouping containers with clipping and labels   |

**Full JSON format:** See `references/json-format.md`
**Copy-paste templates:** See `references/element-templates.md`

---

## Unified Workflow

### Step 0: Assess Depth

Before anything else, determine the diagram type:

- **Simple/Conceptual**: Abstract shapes, labels, relationships (mental models)
- **Comprehensive/Technical**: Concrete examples, code snippets, real data (systems, tutorials)

**If comprehensive**: Do research first. Look up actual specs, formats, event names, APIs.
See `references/design-methodology.md` for depth assessment details.

### Step 1: Analyze Codebase or Content

For architecture diagrams, discover components by looking for:

| Codebase Type | What to Look For                             |
|---------------|----------------------------------------------|
| Monorepo      | `packages/*/package.json`, workspace configs |
| Microservices | `docker-compose.yml`, k8s manifests          |
| IaC           | Terraform/Pulumi resource definitions        |
| Backend API   | Route definitions, controllers, DB models    |
| Frontend      | Component hierarchy, API calls               |

For non-codebase diagrams, research the topic deeply before designing.

### Step 2: Map Concepts to Visual Patterns

For each concept, find the pattern that mirrors its behavior:

| If the concept...              | Use this pattern     |
|--------------------------------|----------------------|
| Spawns multiple outputs        | Fan-out (radial)     |
| Combines inputs into one       | Convergence (funnel) |
| Has hierarchy/nesting          | Tree (lines + text)  |
| Is a sequence of steps         | Timeline (dots)      |
| Loops or improves continuously | Spiral/Cycle         |
| Transforms input to output     | Assembly line        |
| Compares two things            | Side-by-side         |

**Each major concept should use a different visual pattern.** No uniform cards or grids.

**Full pattern library:** See `references/design-methodology.md`

### Step 3: Plan Layout

**Vertical flow (most common):**

```text
Row 1: Users/Entry points (y: 100)
Row 2: Frontend/Gateway (y: 230)
Row 3: Orchestration (y: 380)
Row 4: Services (y: 530)
Row 5: Data layer (y: 680)

Columns: x = 100, 300, 500, 700, 900
Element size: 160-200px x 80-90px
```

**Other patterns:** See `references/examples.md`

### Step 4: Generate Elements

For each component:

1. Create shape with unique `id` (use descriptive strings like `"api-gateway"`)
2. Add `boundElements` referencing text
3. Create text with `containerId`
4. Choose color based on semantic purpose

**Colors:** Read from the project's `color-palette.yml` (initialized above)
**Color rules:** See `references/color-palette.md`

For large diagrams, build one section at a time — do NOT generate everything in a single pass. See
`references/design-methodology.md` for the section-by-section strategy.

### Step 5: Add Connections and Grouping

For each relationship:

1. Calculate source edge point
2. Plan elbow route (avoid overlaps)
3. Create arrow with `points` array
4. Match stroke color to source type

For grouping, use either frame elements (`type: "frame"`) or dashed rectangles.

**Arrow patterns:** See `references/arrows.md`

### Step 6: Render and Validate (Mandatory)

After generating the JSON, you **MUST** render to PNG and visually inspect. This is not optional.

```bash
cd <references-dir>
uv run python render_excalidraw.py <path-to-file.excalidraw>
```

Then Read the PNG, audit against your original vision, check for visual defects, fix, and re-render. Typically
takes 2-4 iterations.

**Full render-validate loop:** See `references/validation.md`
**Structural validation algorithm:** See `references/validation.md`

---

## Quick Arrow Reference

**Straight down:**

```json
{ "points": [[0, 0], [0, 110]], "x": 590, "y": 290 }
```

**L-shape (left then down):**

```json
{ "points": [[0, 0], [-325, 0], [-325, 125]], "x": 525, "y": 420 }
```

**U-turn (callback):**

```json
{ "points": [[0, 0], [50, 0], [50, -125], [20, -125]], "x": 710, "y": 440 }
```

**Arrow width/height** = bounding box of points:

```text
points [[0,0], [-440,0], [-440,70]] -> width=440, height=70
```

**Multiple arrows from same edge** — stagger positions:

```text
5 arrows: 20%, 35%, 50%, 65%, 80% across edge width
```

---

## Diagram Type Suggestions

| Diagram Type  | Recommended Layout | Key Elements                                  |
|---------------|--------------------|-----------------------------------------------|
| Microservices | Vertical flow      | Services, databases, queues, API gateway      |
| Data Pipeline | Horizontal flow    | Sources, transformers, sinks, storage         |
| Event-Driven  | Hub-and-spoke      | Event bus center, producers/consumers         |
| Kubernetes    | Layered groups     | Namespace boxes, pods inside deployments      |
| CI/CD         | Horizontal flow    | Source -> Build -> Test -> Deploy -> Monitor  |
| Network       | Hierarchical       | Internet -> LB -> VPC -> Subnets -> Instances |
| User Flow     | Swimlanes          | User actions, system responses, external calls|

---

## Quick Validation Checklist

Before writing file:

- [ ] Every shape with label has boundElements + text element
- [ ] Text elements have containerId matching shape
- [ ] Multi-point arrows have `elbowed: true`, `roundness: null`, `roughness: 0`
- [ ] Arrow x,y = source shape edge point
- [ ] Arrow final point offset reaches target edge
- [ ] No diamond shapes
- [ ] No duplicate IDs
- [ ] Frame children have correct `frameId`
- [ ] `roughness: 0` and `fontFamily: 3` on all elements
- [ ] Rendered to PNG and visually inspected

**Full validation algorithm:** See `references/validation.md`

---

## Common Issues

| Issue               | Fix                                                    |
|---------------------|--------------------------------------------------------|
| Labels don't appear | Use TWO elements (shape + text), not `label` property  |
| Arrows curved       | Add `elbowed: true`, `roundness: null`, `roughness: 0` |
| Arrows floating     | Calculate x,y from shape edge, not center              |
| Arrows overlapping  | Stagger start positions across edge                    |

**Detailed bug fixes:** See `references/validation.md`

---

## Reference Files

| File                               | Contents                                            |
|------------------------------------|-----------------------------------------------------|
| `references/color-palette-template.yml` | YAML template copied to project on first run       |
| `references/color-palette.md`      | Color system rules, design guidance, cloud palettes     |
| `references/json-format.md`        | Element types, required properties, text bindings   |
| `references/json-schema.md`        | Quick-reference for properties and bindings         |
| `references/arrows.md`             | Routing algorithm, patterns, bindings, staggering   |
| `references/examples.md`           | Complete JSON examples, layout patterns             |
| `references/validation.md`         | Checklists, validation algorithm, render loop       |
| `references/design-methodology.md` | Design philosophy, visual patterns, large diagrams  |
| `references/element-templates.md`  | Copy-paste JSON templates for each element type     |
| `references/render_excalidraw.py`  | PNG renderer (Playwright + Chromium)                |
| `references/render_template.html`  | Browser template for rendering                      |
| `references/pyproject.toml`        | Python dependencies for renderer                    |

---

## Output

- **Location:** `docs/architecture/` or user-specified
- **Filename:** Descriptive, e.g., `system-architecture.excalidraw`
- **Testing:** Open in https://excalidraw.com or VS Code extension
