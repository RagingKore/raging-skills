# MCP Examples

Full working examples for the Excalidraw MCP `create_view` tool. Each example is a complete JSON array ready to use.

## Two Connected Boxes (Basic)

Two labeled boxes connected by an arrow with `fixedPoint` bindings.

```json
[
  { "type": "cameraUpdate", "width": 800, "height": 600, "x": 50, "y": 50 },
  { "type": "rectangle", "id": "b1", "x": 100, "y": 100, "width": 200, "height": 100,
    "roughness": 0, "opacity": 100,
    "roundness": { "type": 3 }, "backgroundColor": "#a5d8ff", "fillStyle": "solid",
    "label": { "text": "Start", "fontSize": 20 } },
  { "type": "rectangle", "id": "b2", "x": 450, "y": 100, "width": 200, "height": 100,
    "roughness": 0, "opacity": 100,
    "roundness": { "type": 3 }, "backgroundColor": "#b2f2bb", "fillStyle": "solid",
    "label": { "text": "End", "fontSize": 20 } },
  { "type": "arrow", "id": "a1", "x": 300, "y": 150, "width": 150, "height": 0,
    "roughness": 0, "opacity": 100,
    "points": [[0,0],[150,0]], "endArrowhead": "arrow",
    "startBinding": { "elementId": "b1", "fixedPoint": [1, 0.5] },
    "endBinding": { "elementId": "b2", "fixedPoint": [0, 0.5] } }
]
```

## Photosynthesis (Camera Animation, Zones)

Demonstrates camera transitions and zone-based layout. Starts zoomed in on the title (M camera), then zooms out
(L camera) to reveal the full diagram.

```json
[
  {"type":"cameraUpdate","width":400,"height":300,"x":200,"y":-20},
  {"type":"text","id":"ti","x":280,"y":10,"text":"Photosynthesis","fontSize":28,
    "roughness":0,"opacity":100,"strokeColor":"#1e1e1e"},
  {"type":"text","id":"fo","x":245,"y":48,"text":"6CO2 + 6H2O --> C6H12O6 + 6O2","fontSize":16,
    "roughness":0,"opacity":100,"strokeColor":"#757575"},

  {"type":"cameraUpdate","width":800,"height":600,"x":0,"y":-20},
  {"type":"rectangle","id":"lf","x":150,"y":90,"width":520,"height":380,
    "roughness":0,"opacity":35,
    "backgroundColor":"#d3f9d8","fillStyle":"solid","roundness":{"type":3},
    "strokeColor":"#22c55e","strokeWidth":1},
  {"type":"text","id":"lfl","x":170,"y":96,"text":"Inside the Leaf","fontSize":16,
    "roughness":0,"opacity":100,"strokeColor":"#15803d"},

  {"type":"rectangle","id":"lr","x":190,"y":190,"width":160,"height":70,
    "roughness":0,"opacity":100,
    "backgroundColor":"#fff3bf","fillStyle":"solid","roundness":{"type":3},
    "strokeColor":"#f59e0b",
    "label":{"text":"Light Reactions","fontSize":16}},
  {"type":"arrow","id":"a1","x":350,"y":225,"width":120,"height":0,
    "roughness":0,"opacity":100,
    "points":[[0,0],[120,0]],"strokeColor":"#1e1e1e","strokeWidth":2,
    "endArrowhead":"arrow","label":{"text":"ATP","fontSize":14}},
  {"type":"rectangle","id":"cc","x":470,"y":190,"width":160,"height":70,
    "roughness":0,"opacity":100,
    "backgroundColor":"#d0bfff","fillStyle":"solid","roundness":{"type":3},
    "strokeColor":"#8b5cf6",
    "label":{"text":"Calvin Cycle","fontSize":16}},

  {"type":"rectangle","id":"sl","x":10,"y":200,"width":120,"height":50,
    "roughness":0,"opacity":100,
    "backgroundColor":"#fff3bf","fillStyle":"solid","roundness":{"type":3},
    "strokeColor":"#f59e0b","label":{"text":"Sunlight","fontSize":16}},
  {"type":"arrow","id":"a2","x":130,"y":225,"width":60,"height":0,
    "roughness":0,"opacity":100,
    "points":[[0,0],[60,0]],"strokeColor":"#f59e0b","strokeWidth":2,"endArrowhead":"arrow"},

  {"type":"rectangle","id":"wa","x":200,"y":360,"width":140,"height":50,
    "roughness":0,"opacity":100,
    "backgroundColor":"#a5d8ff","fillStyle":"solid","roundness":{"type":3},
    "strokeColor":"#4a9eed","label":{"text":"Water (H2O)","fontSize":16}},
  {"type":"arrow","id":"a3","x":270,"y":360,"width":0,"height":-100,
    "roughness":0,"opacity":100,
    "points":[[0,0],[0,-100]],"strokeColor":"#4a9eed","strokeWidth":2,"endArrowhead":"arrow"},

  {"type":"rectangle","id":"co","x":480,"y":360,"width":130,"height":50,
    "roughness":0,"opacity":100,
    "backgroundColor":"#ffd8a8","fillStyle":"solid","roundness":{"type":3},
    "strokeColor":"#f59e0b","label":{"text":"CO2","fontSize":16}},
  {"type":"arrow","id":"a4","x":545,"y":360,"width":0,"height":-100,
    "roughness":0,"opacity":100,
    "points":[[0,0],[0,-100]],"strokeColor":"#f59e0b","strokeWidth":2,"endArrowhead":"arrow"},

  {"type":"rectangle","id":"gl","x":690,"y":195,"width":120,"height":60,
    "roughness":0,"opacity":100,
    "backgroundColor":"#c3fae8","fillStyle":"solid","roundness":{"type":3},
    "strokeColor":"#22c55e","label":{"text":"Glucose","fontSize":18}},
  {"type":"arrow","id":"a6","x":630,"y":225,"width":60,"height":0,
    "roughness":0,"opacity":100,
    "points":[[0,0],[60,0]],"strokeColor":"#22c55e","strokeWidth":2,"endArrowhead":"arrow"}
]
```

## Sequence Diagram (Progressive Camera Panning)

UML-style sequence diagram with actors, dashed lifelines, and labeled message arrows. Camera pans progressively
to follow the flow.

```json
[
  {"type":"cameraUpdate","width":600,"height":450,"x":80,"y":-10},
  {"type":"text","id":"title","x":200,"y":15,"text":"MCP Apps -- Sequence Flow","fontSize":24,
    "roughness":0,"opacity":100,"strokeColor":"#1e1e1e"},

  {"type":"cameraUpdate","width":400,"height":300,"x":450,"y":-5},
  {"type":"rectangle","id":"sHead","x":600,"y":60,"width":130,"height":40,
    "roughness":0,"opacity":100,
    "backgroundColor":"#ffd8a8","fillStyle":"solid","roundness":{"type":3},
    "strokeColor":"#f59e0b","strokeWidth":2,
    "label":{"text":"MCP Server","fontSize":16}},
  {"type":"arrow","id":"sLine","x":665,"y":100,"width":0,"height":490,
    "roughness":0,"opacity":100,
    "points":[[0,0],[0,490]],"strokeColor":"#b0b0b0","strokeWidth":1,
    "strokeStyle":"dashed","endArrowhead":null},

  {"type":"cameraUpdate","width":400,"height":300,"x":250,"y":-5},
  {"type":"rectangle","id":"appHead","x":400,"y":60,"width":130,"height":40,
    "roughness":0,"opacity":100,
    "backgroundColor":"#b2f2bb","fillStyle":"solid","roundness":{"type":3},
    "strokeColor":"#22c55e","strokeWidth":2,
    "label":{"text":"App iframe","fontSize":16}},
  {"type":"arrow","id":"appLine","x":465,"y":100,"width":0,"height":490,
    "roughness":0,"opacity":100,
    "points":[[0,0],[0,490]],"strokeColor":"#b0b0b0","strokeWidth":1,
    "strokeStyle":"dashed","endArrowhead":null},

  {"type":"cameraUpdate","width":400,"height":300,"x":80,"y":-5},
  {"type":"rectangle","id":"aHead","x":230,"y":60,"width":100,"height":40,
    "roughness":0,"opacity":100,
    "backgroundColor":"#d0bfff","fillStyle":"solid","roundness":{"type":3},
    "strokeColor":"#8b5cf6","strokeWidth":2,
    "label":{"text":"Agent","fontSize":16}},
  {"type":"arrow","id":"aLine","x":280,"y":100,"width":0,"height":490,
    "roughness":0,"opacity":100,
    "points":[[0,0],[0,490]],"strokeColor":"#b0b0b0","strokeWidth":1,
    "strokeStyle":"dashed","endArrowhead":null},

  {"type":"cameraUpdate","width":400,"height":300,"x":-10,"y":-5},
  {"type":"rectangle","id":"uHead","x":60,"y":60,"width":100,"height":40,
    "roughness":0,"opacity":100,
    "backgroundColor":"#a5d8ff","fillStyle":"solid","roundness":{"type":3},
    "strokeColor":"#4a9eed","strokeWidth":2,
    "label":{"text":"User","fontSize":16}},
  {"type":"arrow","id":"uLine","x":110,"y":100,"width":0,"height":490,
    "roughness":0,"opacity":100,
    "points":[[0,0],[0,490]],"strokeColor":"#b0b0b0","strokeWidth":1,
    "strokeStyle":"dashed","endArrowhead":null},

  {"type":"cameraUpdate","width":600,"height":450,"x":170,"y":25},
  {"type":"arrow","id":"m1","x":110,"y":135,"width":170,"height":0,
    "roughness":0,"opacity":100,
    "points":[[0,0],[170,0]],"strokeColor":"#1e1e1e","strokeWidth":2,
    "endArrowhead":"arrow","label":{"text":"prompt","fontSize":14}},
  {"type":"arrow","id":"m2","x":280,"y":210,"width":385,"height":0,
    "roughness":0,"opacity":100,
    "points":[[0,0],[385,0]],"strokeColor":"#8b5cf6","strokeWidth":2,
    "endArrowhead":"arrow","label":{"text":"tools/call","fontSize":16}},
  {"type":"arrow","id":"m3","x":665,"y":250,"width":-385,"height":0,
    "roughness":0,"opacity":100,
    "points":[[0,0],[-385,0]],"strokeColor":"#f59e0b","strokeWidth":2,
    "endArrowhead":"arrow","strokeStyle":"dashed",
    "label":{"text":"tool result","fontSize":16}},
  {"type":"arrow","id":"m4","x":280,"y":290,"width":185,"height":0,
    "roughness":0,"opacity":100,
    "points":[[0,0],[185,0]],"strokeColor":"#8b5cf6","strokeWidth":2,
    "endArrowhead":"arrow","strokeStyle":"dashed",
    "label":{"text":"result -> app","fontSize":16}},

  {"type":"cameraUpdate","width":800,"height":600,"x":-5,"y":2}
]
```

## Animation Mode (Delete + Replace)

A pixel snake moves right by adding a head segment and deleting the tail each frame. Camera nudges between
frames add subtle motion. Demonstrates the `delete` pseudo-element for in-place transforms.

```json
[
  {"type":"cameraUpdate","width":400,"height":300,"x":0,"y":0},
  {"type":"ellipse","id":"ap","x":260,"y":78,"width":20,"height":20,
    "roughness":0,"opacity":100,
    "backgroundColor":"#ef4444","fillStyle":"solid","strokeColor":"#ef4444"},
  {"type":"rectangle","id":"s0","x":60,"y":130,"width":28,"height":28,
    "roughness":0,"opacity":100,
    "backgroundColor":"#22c55e","fillStyle":"solid","strokeColor":"#15803d","strokeWidth":1},
  {"type":"rectangle","id":"s1","x":88,"y":130,"width":28,"height":28,
    "roughness":0,"opacity":100,
    "backgroundColor":"#22c55e","fillStyle":"solid","strokeColor":"#15803d","strokeWidth":1},
  {"type":"rectangle","id":"s2","x":116,"y":130,"width":28,"height":28,
    "roughness":0,"opacity":100,
    "backgroundColor":"#22c55e","fillStyle":"solid","strokeColor":"#15803d","strokeWidth":1},
  {"type":"rectangle","id":"s3","x":144,"y":130,"width":28,"height":28,
    "roughness":0,"opacity":100,
    "backgroundColor":"#22c55e","fillStyle":"solid","strokeColor":"#15803d","strokeWidth":1},

  {"type":"cameraUpdate","width":400,"height":300,"x":1,"y":0},
  {"type":"rectangle","id":"s4","x":172,"y":130,"width":28,"height":28,
    "roughness":0,"opacity":100,
    "backgroundColor":"#22c55e","fillStyle":"solid","strokeColor":"#15803d","strokeWidth":1},
  {"type":"delete","ids":"s0"},

  {"type":"cameraUpdate","width":400,"height":300,"x":0,"y":1},
  {"type":"rectangle","id":"s5","x":200,"y":130,"width":28,"height":28,
    "roughness":0,"opacity":100,
    "backgroundColor":"#22c55e","fillStyle":"solid","strokeColor":"#15803d","strokeWidth":1},
  {"type":"delete","ids":"s1"},

  {"type":"cameraUpdate","width":400,"height":300,"x":1,"y":0},
  {"type":"rectangle","id":"s6","x":228,"y":130,"width":28,"height":28,
    "roughness":0,"opacity":100,
    "backgroundColor":"#22c55e","fillStyle":"solid","strokeColor":"#15803d","strokeWidth":1},
  {"type":"delete","ids":"s2"},

  {"type":"cameraUpdate","width":400,"height":300,"x":0,"y":0},
  {"type":"rectangle","id":"s7","x":256,"y":130,"width":28,"height":28,
    "roughness":0,"opacity":100,
    "backgroundColor":"#22c55e","fillStyle":"solid","strokeColor":"#15803d","strokeWidth":1},
  {"type":"delete","ids":"s3"},

  {"type":"cameraUpdate","width":400,"height":300,"x":1,"y":1},
  {"type":"rectangle","id":"s8","x":256,"y":102,"width":28,"height":28,
    "roughness":0,"opacity":100,
    "backgroundColor":"#22c55e","fillStyle":"solid","strokeColor":"#15803d","strokeWidth":1},
  {"type":"delete","ids":"s4"},

  {"type":"cameraUpdate","width":400,"height":300,"x":0,"y":0},
  {"type":"rectangle","id":"s9","x":256,"y":74,"width":28,"height":28,
    "roughness":0,"opacity":100,
    "backgroundColor":"#22c55e","fillStyle":"solid","strokeColor":"#15803d","strokeWidth":1},
  {"type":"delete","ids":"ap"},

  {"type":"cameraUpdate","width":400,"height":300,"x":1,"y":0},
  {"type":"rectangle","id":"s10","x":256,"y":46,"width":28,"height":28,
    "roughness":0,"opacity":100,
    "backgroundColor":"#22c55e","fillStyle":"solid","strokeColor":"#15803d","strokeWidth":1},
  {"type":"delete","ids":"s5"}
]
```

## Layout Patterns Reference

Common coordinate patterns for positioning elements.

### Vertical Flow (Top to Bottom)

```text
Row 1 (entry):    y = 100,  elements at x = 100, 350, 600
Row 2 (process):  y = 250,  elements at x = 100, 350, 600
Row 3 (output):   y = 400,  elements at x = 100, 350, 600
Element size:     200x80
Vertical gap:     70px between rows (250 - 100 - 80 = 70)
Horizontal gap:   50px between columns (350 - 100 - 200 = 50)
Camera:           L (800x600) starting at x=50, y=50
```

### Horizontal Flow (Left to Right)

```text
Col 1 (input):    x = 100,  elements at y = 150, 300
Col 2 (process):  x = 400,  elements at y = 150, 300
Col 3 (output):   x = 700,  elements at y = 150, 300
Element size:     200x80
Horizontal gap:   100px between columns (400 - 100 - 200 = 100)
Camera:           L (800x600) or XL (1200x900) starting at x=50, y=50
```

### Hub-and-Spoke

```text
Hub:              x = 350, y = 250, size 200x100
Spoke top:        x = 375, y = 80
Spoke left:       x = 80,  y = 225
Spoke right:      x = 620, y = 225
Spoke bottom:     x = 375, y = 420
Spoke size:       150x70
Camera:           L (800x600) starting at x=20, y=20
```

### Grid (Dashboard)

```text
Cell size:        160x120
H-gap:            40px
V-gap:            40px
Row 1:            y = 100, x = 100, 300, 500
Row 2:            y = 260, x = 100, 300, 500
Row 3:            y = 420, x = 100, 300, 500
Camera:           L (800x600) starting at x=50, y=50
```
