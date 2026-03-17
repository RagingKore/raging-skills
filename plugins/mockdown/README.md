# Mockdown

Design, sketch, and reason about UI layout and structure using ASCII wireframes.

## Overview

The mockdown plugin connects Claude Code to [Mockdown](https://www.mockdown.design/), a free browser-based ASCII
wireframe editor. It provides knowledge of the ASCII wireframe format for reading, writing, and interpreting
wireframes, plus Chrome automation for driving the editor interactively.

## Skills

### Auto-Loaded

**mockdown**

Activates when you paste an ASCII wireframe, mention "mockdown", "wireframe", "mockup", discuss UI layout, or
ask to sketch a screen. Teaches the full ASCII wireframe format: all 20+ components, their patterns, and how to
read and write wireframes. Generates framework-agnostic UI code from wireframes. Works for web UIs, terminal
UIs, CLI interfaces, dashboards, forms, and dialogs.

### Internal

**mockdown-editor**

Not user-triggerable. Provides Chrome automation knowledge for driving the Mockdown editor: DOM selectors,
toolbar structure, component placement, keyboard shortcuts, and interaction patterns. Preloaded by agents that
need to interact with the editor.

## Agents

**mockdown-designer**

Preloads both skills. Drives the Mockdown editor autonomously via Chrome extension tools. Handles interactive
design sessions: discusses layout with the user, plans the wireframe, builds it in the editor, iterates on
feedback, and exports the result. Triggers when you ask to "design a wireframe", "create a mockup", or "sketch
this screen".

## Requirements

- **Wireframe interpretation and writing**: no requirements; works in any conversation
- **Interactive design sessions**: requires the Claude-in-Chrome extension to be installed and connected

## Example Usage

### Interpret a wireframe

Paste an ASCII wireframe into the conversation:

````text
```
Logo   Link   Link   Link     [ Action ]
────────────────────────────────────────

[ Tab 1 ]   Tab 2   Tab 3
──────────────────────────

┌──────────────────┐  ┌─────────┬─────────┬─────────┐
│ Title            │  │ Col A   │ Col B   │ Col C   │
├──────────────────┤  ├─────────┼─────────┼─────────┤
│                  │  │         │         │         │
│                  │  └─────────┴─────────┴─────────┘
└──────────────────┘
```
````

Claude generates semantic HTML and CSS matching the layout.

### Design a wireframe

```text
"Design a login page wireframe with email, password, submit button, and a forgot password link"
```

Claude opens Mockdown, places the components, and exports the result.
