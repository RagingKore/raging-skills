# Excalidraw

Generate architecture diagrams as `.excalidraw` files from codebase analysis.

## Overview

The excalidraw plugin enables Claude Code to generate valid Excalidraw JSON files directly
from codebase analysis. It handles label binding, elbow arrow routing, edge-point calculations,
color palettes for different component types, and validation rules. An agent definition ensures
Excalidraw files are delegated to subagents to prevent context exhaustion from verbose JSON.

## Skills

### Auto-Loaded

**excalidraw**

Activates when you mention `.excalidraw` files, ask for architecture diagrams, or request
codebase visualization. Provides the Excalidraw JSON format reference, arrow routing
algorithms, color palettes, and validation rules.

## Agents

**excalidraw**

Enforces subagent delegation for all Excalidraw file operations. Delegates reads, modifications,
comparisons, and creation tasks to subagents so the main agent's context stays clean. Triggers
on `.excalidraw` or `.excalidraw.json` file paths, or when you request diagram operations such
as "explain diagram", "update diagram", "show architecture", or "visualize flow".
