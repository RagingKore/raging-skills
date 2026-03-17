# Excalidraw

Generate Excalidraw diagrams that make visual arguments; not just labeled boxes, but architecture, workflows, concepts,
and more.

## Overview

The excalidraw plugin provides two complementary skills for creating Excalidraw diagrams, plus an agent definition that
ensures all Excalidraw operations are delegated to subagents to prevent context exhaustion from verbose JSON.

Both skills share a design methodology, color palette system, and the same enforced defaults (`roughness: 0`,
`fontFamily: 3`, `opacity: 100`).

## Skills

### Auto-Loaded

**excalidraw** (raw JSON)

Activates when you mention `.excalidraw` files, ask for diagrams, or request visualization. Generates `.excalidraw`
JSON files directly. Provides:

- Design methodology with visual pattern library (fan-out, convergence, tree, timeline, etc.)
- Excalidraw JSON format reference with element templates
- Arrow routing algorithms and edge calculations
- Semantic and component-type color palettes (default, AWS, Azure, GCP, K8s)
- Structural validation algorithm and render-to-PNG loop
- Section-by-section building strategy for large diagrams

**excalidraw-mcp** (MCP live preview)

Activates when Excalidraw MCP tools are available AND you ask for diagrams or visualization. Creates diagrams with live
browser preview via the Excalidraw MCP server. Provides:

- Live inline rendering as elements stream
- Camera animations that guide attention between sections
- Label shorthand on shapes and arrows (no separate text elements)
- Diamond shapes (allowed in MCP; arrow connections work correctly)
- fixedPoint arrow bindings (no manual edge-point calculations)
- Iterative editing via checkpoints and delete pseudo-elements
- Export to shareable Excalidraw URLs

## Agents

**excalidraw**

Enforces subagent delegation for all Excalidraw operations, including both raw JSON file operations and MCP
`create_view` calls. Delegates reads, modifications, comparisons, creation, and export tasks to subagents so the main
agent's context stays clean. Triggers on `.excalidraw` or `.excalidraw.json` file paths, or when you request diagram
operations such as "explain diagram", "update diagram", "show architecture", or "visualize flow".

## MCP Server

The plugin configures the Excalidraw MCP server (`https://mcp.excalidraw.com`) automatically via `.mcp.json`. This
enables the `excalidraw-mcp` skill's live preview workflow. No manual MCP setup required.
