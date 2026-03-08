---
name: beautiful-mermaid
description: |
  Render Mermaid diagrams as ASCII art or themed SVG files using beautiful-mermaid via bun.
  Use when asked to create a diagram, flowchart, sequence diagram, class diagram, ER diagram,
  state diagram, visualize architecture, render mermaid, generate ASCII art, or produce SVG
  diagrams. Handles environment bootstrapping, correct Mermaid syntax, and rendering execution.
---

# beautiful-mermaid

Render Mermaid diagrams to ASCII/Unicode terminal art or themed SVG files using the `beautiful-mermaid` library via `bun -e`.

## When to Use

- User asks to "draw a diagram", "create a flowchart", "visualize", "render mermaid"
- User asks for ASCII art of architecture, flows, sequences, classes, or ER schemas
- User wants an SVG diagram file saved to disk
- User mentions "mermaid", "diagram", "flowchart", "sequence diagram", "class diagram", "ER diagram", "state machine"

## Environment Bootstrap

**Before rendering any diagram**, ensure the environment is ready. Run this bootstrap check:

```bash
# Check if bun exists
which bun > /dev/null 2>&1 || { echo "ERROR: bun not found. Install via: curl -fsSL https://bun.sh/install | bash"; exit 1; }

# Install beautiful-mermaid if not already present
MERMAID_DIR="/tmp/beautiful-mermaid-workspace"
if [ ! -d "$MERMAID_DIR/node_modules/beautiful-mermaid" ]; then
  mkdir -p "$MERMAID_DIR" && cd "$MERMAID_DIR" && bun add beautiful-mermaid
fi
```

All `bun -e` commands must use `--cwd /tmp/beautiful-mermaid-workspace` to find the package.

## Quick Reference

### ASCII to Terminal

```bash
bun -e "
import { renderMermaidAscii } from 'beautiful-mermaid'
console.log(renderMermaidAscii(\`graph LR
  A[Start] --> B{Decision}
  B -->|Yes| C[Action]
  B -->|No| D[End]\`))
" --cwd /tmp/beautiful-mermaid-workspace
```

### SVG to File

```bash
bun -e "
import { renderMermaid, THEMES } from 'beautiful-mermaid'
import { writeFileSync } from 'fs'
const svg = await renderMermaid(\`graph TD
  A[Start] --> B[End]\`, THEMES['tokyo-night'])
writeFileSync('diagram.svg', svg)
console.log('Saved diagram.svg')
" --cwd /tmp/beautiful-mermaid-workspace
```

## Supported Diagram Types

### 1. Flowchart (`graph` / `flowchart`)

**Header**: `graph TD`, `graph LR`, `graph BT`, `graph RL`, `flowchart TD`, etc.

**Directions**: `TD`/`TB` (top-down), `LR` (left-right), `BT` (bottom-top), `RL` (right-left)

**12 Node Shapes**:

| Shape         | Syntax                | Example             |
|---------------|-----------------------|---------------------|
| Rectangle     | `[text]`              | `A[Server]`         |
| Rounded       | `(text)`              | `B(Process)`        |
| Diamond       | `{text}`              | `C{Decision}`       |
| Stadium       | `([text])`            | `D([Deploy])`       |
| Circle        | `((text))`            | `E((Start))`        |
| Subroutine    | `[[text]]`            | `F[[Routine]]`      |
| Double Circle | `(((text)))`          | `G(((Target)))`     |
| Hexagon       | <code>{{text}}</code> | `H{{Prepare}}`      |
| Cylinder      | `[(text)]`            | `I[(Database)]`     |
| Asymmetric    | `>text]`              | `J>Flag]`           |
| Trapezoid     | `[/text\]`            | `K[/Wider Bottom\]` |
| Inverse Trap  | `[\text/]`            | `L[\Wider Top/]`    |

**Edge Styles**:

| Style             | Forward | Reverse | Bidirectional | With Label      |
|-------------------|---------|---------|---------------|-----------------|
| Solid             | `-->`   | `<--`   | `<-->`        | `-->\|label\|`  |
| Dotted            | `-.->`  | `<-.-`  | `<-.->`       | `-.->\|label\|` |
| Thick             | `==>`   | `<==`   | `<==>`        | `==>\|label\|`  |
| No arrow (solid)  | `---`   |         |               | `---\|label\|`  |
| No arrow (dotted) | `-.-`   |         |               |                 |
| No arrow (thick)  | `===`   |         |               |                 |

**Parallel edges with `&`**:

```
A & B --> C & D
```

**Subgraphs**:

```
graph TD
  subgraph Frontend
    direction LR
    A[React] --> B[State]
  end
  subgraph Backend
    C[API] --> D[DB]
  end
  B --> C
```

**Styling**:

```
classDef highlight fill:#fbbf24,stroke:#d97706
A[Node]:::highlight
style B fill:#3b82f6,stroke:#1d4ed8,color:#ffffff
```

**Comments**: Lines starting with `%%` are ignored.

### 2. State Diagram (`stateDiagram-v2`)

```
stateDiagram-v2
  [*] --> Idle
  Idle --> Processing : submit
  Processing --> Done : complete
  Processing --> Error : fail
  Error --> Idle : retry
  Done --> [*]
```

**Composite states**:

```
stateDiagram-v2
  state Processing {
    parse --> validate
    validate --> execute
  }
```

`[*]` = start/end pseudostate. Transitions use ` : label` syntax.

### 3. Sequence Diagram (`sequenceDiagram`)

```
sequenceDiagram
  participant A as Alice
  participant B as Bob
  A->>B: Hello Bob!
  B-->>A: Hi Alice!
```

**Arrow Types**:

| Arrow  | Meaning                            |
|--------|------------------------------------|
| `->>`  | Solid arrow (synchronous)          |
| `-->>` | Dashed arrow (return/async)        |
| `-)`   | Open arrow (async fire-and-forget) |
| `--)`  | Open dashed arrow                  |

**Actors**: Use `actor U as User` for stick figures instead of boxes.

**Activation**: `+`/`-` after participant name for activation boxes.

```
C->>+S: Request
S-->>-C: Response
```

**Blocks**:

```
loop Every 30s
  C->>S: Heartbeat
end

alt Valid
  S-->>C: 200 OK
else Invalid
  S-->>C: 401
end

opt Cache miss
  A->>DB: Query
end

par Fetch user
  G->>U: Get profile
and Fetch orders
  G->>O: Get orders
end

critical Transaction
  A->>DB: UPDATE
break Failure
  DB-->>S: Error
end
end

rect rgb(200, 220, 255)
  A->>B: Highlighted section
end
```

**Notes**:

```
Note left of A: Prepares
Note right of B: Thinks
Note over A,B: Complete
```

### 4. Class Diagram (`classDiagram`)

```
classDiagram
  class Animal {
    <<abstract>>
    +String name
    -int age
    #validate() void
    ~notify() void
    +eat() void
  }
  Animal <|-- Dog
  Animal <|-- Cat
```

**Visibility**: `+` public, `-` private, `#` protected, `~` package

**Method modifiers**: `$` suffix = static, `*` suffix = abstract

```
+staticMethod$() void
+abstractMethod*() void
```

**Generics**: `class List~T~` renders as `List<T>`

**Annotations**: `<<interface>>`, `<<abstract>>`, `<<enumeration>>`, `<<service>>`

**6 Relationship Types**:

| Relationship | Syntax  | Meaning                                 |
|--------------|---------|-----------------------------------------|
| Inheritance  | `<\|--` | "extends" (hollow triangle)             |
| Composition  | `*--`   | "owns" (filled diamond)                 |
| Aggregation  | `o--`   | "has" (hollow diamond)                  |
| Association  | `-->`   | directed arrow                          |
| Dependency   | `..>`   | dashed arrow                            |
| Realization  | `..\|>` | "implements" (dashed + hollow triangle) |

**Labels**: `Teacher --> Course : teaches`

**Cardinality**: `A "1" --> "*" B : has`

### 5. ER Diagram (`erDiagram`)

```
erDiagram
  CUSTOMER {
    int id PK
    string name
    string email UK
  }
  ORDER {
    int id PK
    int customer_id FK
  }
  CUSTOMER ||--o{ ORDER : places
```

**Cardinality Notation**:

| Left   | Right  | Meaning                     |
|--------|--------|-----------------------------|
| `\|\|` | `\|\|` | Exactly one to exactly one  |
| `\|\|` | `o{`   | One to zero-or-many         |
| `\|o`  | `\|{`  | Zero-or-one to one-or-many  |
| `}\|`  | `o{`   | One-or-more to zero-or-many |

**Line styles**: `--` (solid, identifying), `..` (dashed, non-identifying)

**Key badges**: `PK` (primary key), `FK` (foreign key), `UK` (unique key)

## Rendering Options

### ASCII Options

```typescript
renderMermaidAscii(text, {
  useAscii: false,      // true = +--| chars, false = Unicode box-drawing (default)
  paddingX: 5,          // Horizontal spacing between nodes (default: 5)
  paddingY: 5,          // Vertical spacing between nodes (default: 5)
  boxBorderPadding: 1,  // Padding inside node boxes (default: 1)
})
```

### SVG Options

```typescript
renderMermaid(text, {
  // Required base colors (defaults: white/#27272A)
  bg: '#1a1b26',
  fg: '#a9b1d6',
  // Optional enrichment (falls back to color-mix derivations)
  line: '#3d59a1',      // Edge/connector color
  accent: '#7aa2f7',    // Arrow heads, highlights
  muted: '#565f89',     // Secondary text, labels
  surface: '#292e42',   // Node fill tint
  border: '#3d59a1',    // Node stroke
  // Layout
  font: 'Inter',        // Font family (default: Inter)
  padding: 40,          // Canvas padding in px (default: 40)
  nodeSpacing: 24,      // Horizontal spacing between nodes (default: 24)
  layerSpacing: 40,     // Vertical spacing between layers (default: 40)
  transparent: false,   // Transparent background (default: false)
})
```

### Built-in Themes

Use `THEMES['name']` to apply a preset:

| Theme               | Type  | Colors                                   |
|---------------------|-------|------------------------------------------|
| `zinc-light`        | Light | `#FFFFFF` / `#27272A`                    |
| `zinc-dark`         | Dark  | `#18181B` / `#FAFAFA`                    |
| `tokyo-night`       | Dark  | `#1a1b26` / `#a9b1d6` + accent `#7aa2f7` |
| `tokyo-night-storm` | Dark  | `#24283b` / `#a9b1d6`                    |
| `tokyo-night-light` | Light | `#d5d6db` / `#343b58`                    |
| `catppuccin-mocha`  | Dark  | `#1e1e2e` / `#cdd6f4` + accent `#cba6f7` |
| `catppuccin-latte`  | Light | `#eff1f5` / `#4c4f69`                    |
| `nord`              | Dark  | `#2e3440` / `#d8dee9` + accent `#88c0d0` |
| `nord-light`        | Light | `#eceff4` / `#2e3440`                    |
| `dracula`           | Dark  | `#282a36` / `#f8f8f2` + accent `#bd93f9` |
| `github-light`      | Light | `#ffffff` / `#1f2328`                    |
| `github-dark`       | Dark  | `#0d1117` / `#e6edf3`                    |
| `solarized-light`   | Light | `#fdf6e3` / `#657b83`                    |
| `solarized-dark`    | Dark  | `#002b36` / `#839496`                    |
| `one-dark`          | Dark  | `#282c34` / `#abb2bf`                    |

## Critical Gotchas

1. **Header must be on its own line** -- The diagram type header (`graph LR`, `sequenceDiagram`, etc.) MUST be on its own line or semicolon-separated BEFORE any node/edge definitions. `graph LR; A --> B` works but `graph LR; A --> B --> C` may fail because the parser matches the header strictly.

2. **Use template literals** -- For multiline diagrams in `bun -e`, always use backtick template literals, not string concatenation with `\n`.

3. **BT direction** -- Bottom-to-top is rendered as top-down then vertically flipped. Works correctly but keep in mind for complex layouts.

4. **RL direction** -- Right-to-left is currently treated as LR internally.

5. **ASCII doesn't support all shapes** – The ASCII renderer shows all nodes as rectangles regardless of shape syntax. Shape syntax matters for SVG output.

6. **`renderMermaid` is async** – SVG rendering returns a Promise. Use `await` or `.then()`. ASCII rendering (`renderMermaidAscii`) is synchronous.

7. **Always use `--cwd`** -- Every `bun -e` command needs `--cwd /tmp/beautiful-mermaid-workspace` to resolve the import.

## Advanced Usage

For detailed bun execution patterns and real-world examples, see:
- [references/execution_patterns.md](references/execution_patterns.md) - Bootstrap scripts, ASCII and SVG rendering commands, batch rendering
- [references/diagram_examples.md](references/diagram_examples.md) - Complete real-world diagram examples for every type
