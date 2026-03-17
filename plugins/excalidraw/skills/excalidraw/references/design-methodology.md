# Design Methodology

Design philosophy, visual patterns, and strategies for creating diagrams that argue visually.

---

## Core Philosophy

**Diagrams should ARGUE, not DISPLAY.**

A diagram is a visual argument that shows relationships, causality, and flow that words alone cannot express. The
shape should BE the meaning.

**The Isomorphism Test**: If you removed all text, would the structure alone communicate the concept? If not, redesign.

**The Education Test**: Could someone learn something concrete from this diagram, or does it just label boxes? A good
diagram teaches; it shows actual formats, real event names, concrete examples.

---

## Depth Assessment (Do This First)

Before designing, determine what level of detail this diagram needs.

### Simple / Conceptual Diagrams

Use abstract shapes when:

- Explaining a mental model or philosophy
- The audience does not need technical specifics
- The concept IS the abstraction (e.g., "separation of concerns")

### Comprehensive / Technical Diagrams

Use concrete examples when:

- Diagramming a real system, protocol, or architecture
- The diagram will be used to teach or explain (e.g., video, talk)
- The audience needs to understand what things actually look like
- You are showing how multiple technologies integrate

**For technical diagrams, you MUST include evidence artifacts** (see below).

| Simple Diagram                                   | Comprehensive Diagram                                     |
|--------------------------------------------------|-----------------------------------------------------------|
| Generic labels: "Input" -> "Process" -> "Output" | Specific: shows what the input/output actually looks like |
| Named boxes: "API", "Database", "Client"         | Named boxes + examples of actual requests/responses       |
| "Events" or "Messages" label                     | Timeline with real event/message names from the spec      |
| ~30 seconds to explain                           | ~2-3 minutes of teaching content                          |
| Viewer learns the structure                      | Viewer learns the structure AND the details               |

---

## Research Mandate (For Technical Diagrams)

**Before drawing anything technical, research the actual specifications.**

If you are diagramming a protocol, API, or framework:

1. Look up the actual JSON/data formats
2. Find the real event names, method names, or API endpoints
3. Understand how the pieces actually connect
4. Use real terminology, not generic placeholders

Bad: "Protocol" -> "Frontend"
Good: "AG-UI streams events (RUN_STARTED, STATE_DELTA)" -> "CopilotKit renders via createA2UIMessageRenderer()"

---

## Evidence Artifacts

Evidence artifacts are concrete examples that prove your diagram is accurate and help viewers learn. Include them
in technical diagrams.

| Artifact Type          | When to Use                          | How to Render                                               |
|------------------------|--------------------------------------|-------------------------------------------------------------|
| Code snippets          | APIs, integrations, implementation   | Dark rectangle + syntax-colored text (see `color-palette.yml`)      |
| Data/JSON examples     | Data formats, schemas, payloads      | Dark rectangle + colored text (see `color-palette.yml`)             |
| Event/step sequences   | Protocols, workflows, lifecycles     | Timeline pattern (line + dots + labels)                     |
| UI mockups             | Showing actual output/results        | Nested rectangles mimicking real UI                         |
| Real input content     | Showing what goes IN to a system     | Rectangle with sample content visible                       |
| API/method names       | Real function calls, endpoints       | Use actual names from docs, not placeholders                |

**Key principle: show what things actually look like**, not just what they are called.

---

## Multi-Zoom Architecture

Comprehensive diagrams operate at multiple zoom levels simultaneously, like a map that shows both country borders
and street names.

### Level 1: Summary Flow

A simplified overview showing the full pipeline or process at a glance. Often placed at the top or bottom of the
diagram.

### Level 2: Section Boundaries

Labeled regions that group related components. These create visual "rooms" that help viewers understand what
belongs together.

### Level 3: Detail Inside Sections

Evidence artifacts, code snippets, and concrete examples within each section. This is where the educational value lives.

**For comprehensive diagrams, aim to include all three levels.** The summary gives context, the sections organize,
and the details teach.

---

## Visual Pattern Library

### Fan-Out (One-to-Many)

Central element with arrows radiating to multiple targets. Use for: sources, PRDs, root causes, central hubs.

```text
        o
       /
  [] --o
       \
        o
```

### Convergence (Many-to-One)

Multiple inputs merging through arrows to single output. Use for: aggregation, funnels, synthesis.

```text
  o \
  o --> []
  o /
```

### Tree (Hierarchy)

Parent-child branching with connecting lines and free-floating text (no boxes needed). Use for: file systems, org
charts, taxonomies. Use `line` elements for trunk and branches, free-floating text for labels.

```text
  label
  +-- label
  |   +-- label
  |   +-- label
  +-- label
```

### Timeline (Sequence)

Horizontal or vertical line with small dots (10-20px ellipses) at intervals, free-floating labels beside each
dot. Use for: sequences, step-by-step processes, lifecycles.

```text
  *--- Label 1
  |
  *--- Label 2
  |
  *--- Label 3
```

### Spiral/Cycle (Continuous Loop)

Elements in sequence with arrow returning to start. Use for: feedback loops, iterative processes, evolution.

```text
  [] --> []
  ^       |
  |       v
  [] <-- []
```

### Cloud (Abstract State)

Overlapping ellipses with varied sizes. Use for: context, memory, conversations, mental states.

### Assembly Line (Transformation)

Input -> Process Box -> Output with clear before/after. Use for: transformations, processing, conversion.

```text
  ooo --> [PROCESS] --> [][]
  chaos                 order
```

### Side-by-Side (Comparison)

Two parallel structures with visual contrast. Use for: before/after, options, trade-offs.

### Gap/Break (Separation)

Visual whitespace or barrier between sections. Use for: phase changes, context resets, boundaries.

### Lines as Structure

Use lines (`type: "line"`, not arrows) as primary structural elements instead of boxes:

- **Timelines**: Line with small dots at intervals, free-floating labels beside each dot
- **Tree structures**: Vertical trunk line + horizontal branch lines, free-floating text
- **Dividers**: Thin dashed lines to separate sections
- **Flow spines**: A central line that elements relate to

Lines + free-floating text often creates a cleaner result than boxes + contained text.

---

## Container vs. Free-Floating Text

**Not every piece of text needs a shape around it.** Default to free-floating text. Add containers only when they
serve a purpose.

| Use a Container When                            | Use Free-Floating Text When             |
|-------------------------------------------------|-----------------------------------------|
| It is the focal point of a section              | It is a label or description            |
| It needs visual grouping with other elements    | It is supporting detail or metadata     |
| Arrows need to connect to it                    | It describes something nearby           |
| The shape itself carries meaning                | Typography alone creates hierarchy      |
| It represents a distinct "thing" in the system  | It is a section title or annotation     |

**Typography as hierarchy**: Use font size, weight, and color to create visual hierarchy without boxes. A 28px
title does not need a rectangle around it.

**The container test**: For each boxed element, ask "Would this work as free-floating text?" If yes, remove the
container.

**Target**: Less than 30% of text elements should be inside containers.

---

## Shape Meaning

Choose shape based on what it represents, or use no shape at all:

| Concept Type                    | Shape                         | Why                          |
|---------------------------------|-------------------------------|------------------------------|
| Labels, descriptions, details   | **none** (free-floating text) | Typography creates hierarchy |
| Section titles, annotations     | **none** (free-floating text) | Font size/weight is enough   |
| Markers on a timeline           | small `ellipse` (10-20px)     | Visual anchor, not container |
| Start, trigger, input           | `ellipse`                     | Soft, origin-like            |
| End, output, result             | `ellipse`                     | Completion, destination      |
| Process, action, step, service  | `rectangle`                   | Contained action             |
| Abstract state, context         | overlapping `ellipse`         | Fuzzy, cloud-like            |
| Hierarchy node                  | lines + text (no boxes)       | Structure through lines      |
| Decision point                  | styled `rectangle`            | Diamond is banned in JSON    |

---

## Layout Principles

### Hierarchy Through Scale

- **Hero**: 300x150 — visual anchor, most important element
- **Primary**: 180x90
- **Secondary**: 120x60
- **Small**: 60x40

### Whitespace = Importance

The most important element has the most empty space around it (200px+).

### Flow Direction

Guide the eye: typically left-to-right or top-to-bottom for sequences, radial for hub-and-spoke.

### Connections Required

Position alone does not show relationships. If A relates to B, there must be an arrow.

---

## Large / Comprehensive Diagram Strategy

**For comprehensive or technical diagrams, build the JSON one section at a time.** Do NOT generate the entire
file in a single pass. This is a hard constraint — Claude Code has a ~32,000 token output limit per response,
and a comprehensive diagram easily exceeds that. Section-by-section also produces better quality.

### The Section-by-Section Workflow

**Phase 1: Build each section**

1. Create the base file with the JSON wrapper (`type`, `version`, `appState`, `files`) and the first section of
   elements.
2. Add one section per edit. Each section gets its own dedicated pass — think carefully about layout, spacing,
   and how this section connects to existing elements.
3. Use descriptive string IDs (e.g., `"trigger_rect"`, `"arrow_fan_left"`) so cross-section references are
   readable.
4. Namespace seeds by section (e.g., section 1 uses 100xxx, section 2 uses 200xxx) to avoid collisions.
5. Update cross-section bindings as you go. When a new element needs to bind to an element from a previous
   section, edit the earlier element's `boundElements` array at the same time.

**Phase 2: Review the whole**

After all sections are in place, read through the complete JSON and check:

- Are cross-section arrows bound correctly on both ends?
- Is the overall spacing balanced?
- Do IDs and bindings all reference elements that actually exist?

**Phase 3: Render and validate**

Run the render-view-fix loop from `validation.md`. This catches visual issues not obvious from JSON.

### Section Boundaries

Plan sections around natural visual groupings:

- **Section 1**: Entry point / trigger
- **Section 2**: First decision or routing
- **Section 3**: Main content (hero section — may be the largest)
- **Section 4-N**: Remaining phases, outputs, etc.

Each section should be independently understandable: its elements, internal arrows, and any cross-references to
adjacent sections.

### What NOT to Do

- **Do not generate the entire diagram in one response.** You will hit the output token limit and produce
  truncated, broken JSON. Even if the diagram fits, splitting into sections produces better results.
- **Do not use a coding agent** to generate the JSON. The agent won't have sufficient context about the skill's
  rules.
- **Do not write a Python generator script.** The templating and coordinate math seem helpful but introduce
  indirection that makes debugging harder. Hand-crafted JSON with descriptive IDs is more maintainable.

---

## Modern Aesthetics

### Roughness

- `roughness: 0` — Clean, crisp edges. Use for modern/technical diagrams.
- `roughness: 1` — Hand-drawn, organic feel. Use for brainstorming/informal diagrams.

**Default to 0** for most professional use cases.

### Stroke Width

- `strokeWidth: 1` — Thin, elegant. Good for lines, dividers, subtle connections.
- `strokeWidth: 2` — Standard. Good for shapes and primary arrows.
- `strokeWidth: 3` — Bold. Use sparingly for emphasis.

### Opacity

**Always use `opacity: 100` for all elements.** Use color, size, and stroke width to create hierarchy instead
of transparency.

### Small Markers Instead of Shapes

Use small dots (10-20px ellipses) as:

- Timeline markers
- Bullet points
- Connection nodes
- Visual anchors for free-floating text
