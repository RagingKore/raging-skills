# Execution Patterns

Working `bun -e` commands for rendering diagrams with beautiful-mermaid.

## Bootstrap Script

Run this ONCE per session before any rendering. It is idempotent.

```bash
# Full bootstrap: check bun, install package
MERMAID_DIR="/tmp/beautiful-mermaid-workspace"
which bun > /dev/null 2>&1 || { echo "ERROR: bun is required. Install: curl -fsSL https://bun.sh/install | bash"; exit 1; }
if [ ! -d "$MERMAID_DIR/node_modules/beautiful-mermaid" ]; then
  mkdir -p "$MERMAID_DIR" && cd "$MERMAID_DIR" && bun add beautiful-mermaid 2>&1
fi
echo "beautiful-mermaid ready at $MERMAID_DIR"
```

## ASCII Rendering

### Basic Flowchart

```bash
bun -e "
import { renderMermaidAscii } from 'beautiful-mermaid'
console.log(renderMermaidAscii(\`graph LR
  A[Start] --> B{Decision}
  B -->|Yes| C[Action]
  B -->|No| D[End]\`))
" --cwd /tmp/beautiful-mermaid-workspace
```

### Sequence Diagram

```bash
bun -e "
import { renderMermaidAscii } from 'beautiful-mermaid'
console.log(renderMermaidAscii(\`sequenceDiagram
  participant C as Client
  participant S as Server
  participant DB as Database
  C->>S: POST /api/data
  S->>DB: INSERT query
  DB-->>S: Result
  S-->>C: 200 OK\`))
" --cwd /tmp/beautiful-mermaid-workspace
```

### Class Diagram

```bash
bun -e "
import { renderMermaidAscii } from 'beautiful-mermaid'
console.log(renderMermaidAscii(\`classDiagram
  class Animal {
    +String name
    +eat() void
    +sleep() void
  }
  class Dog {
    +String breed
    +bark() void
  }
  Animal <|-- Dog\`))
" --cwd /tmp/beautiful-mermaid-workspace
```

### ER Diagram

```bash
bun -e "
import { renderMermaidAscii } from 'beautiful-mermaid'
console.log(renderMermaidAscii(\`erDiagram
  CUSTOMER ||--o{ ORDER : places
  ORDER ||--|{ LINE_ITEM : contains
  PRODUCT ||--o{ LINE_ITEM : includes\`))
" --cwd /tmp/beautiful-mermaid-workspace
```

### State Diagram

```bash
bun -e "
import { renderMermaidAscii } from 'beautiful-mermaid'
console.log(renderMermaidAscii(\`stateDiagram-v2
  [*] --> Idle
  Idle --> Processing : submit
  Processing --> Done : complete
  Processing --> Error : fail
  Error --> Idle : retry
  Done --> [*]\`))
" --cwd /tmp/beautiful-mermaid-workspace
```

### Pure ASCII Mode (no Unicode)

```bash
bun -e "
import { renderMermaidAscii } from 'beautiful-mermaid'
console.log(renderMermaidAscii(\`graph LR
  A --> B --> C\`, { useAscii: true }))
" --cwd /tmp/beautiful-mermaid-workspace
```

Output uses `+`, `-`, `|`, `>` instead of Unicode box-drawing characters.

### Custom Spacing

```bash
bun -e "
import { renderMermaidAscii } from 'beautiful-mermaid'
console.log(renderMermaidAscii(\`graph LR
  A --> B --> C\`, { paddingX: 3, paddingY: 3, boxBorderPadding: 1 }))
" --cwd /tmp/beautiful-mermaid-workspace
```

## SVG Rendering

### Default Theme (white background)

```bash
bun -e "
import { renderMermaid } from 'beautiful-mermaid'
import { writeFileSync } from 'fs'
const svg = await renderMermaid(\`graph TD
  A[Start] --> B{Decision}
  B -->|Yes| C[Process]
  B -->|No| D[End]
  C --> D\`)
writeFileSync('diagram.svg', svg)
console.log('Saved diagram.svg')
" --cwd /tmp/beautiful-mermaid-workspace
```

### With Built-in Theme

```bash
bun -e "
import { renderMermaid, THEMES } from 'beautiful-mermaid'
import { writeFileSync } from 'fs'
const svg = await renderMermaid(\`graph TD
  A[Start] --> B{Decision}
  B -->|Yes| C[Process]
  B -->|No| D[End]\`, THEMES['tokyo-night'])
writeFileSync('diagram.svg', svg)
console.log('Saved diagram.svg (tokyo-night theme)')
" --cwd /tmp/beautiful-mermaid-workspace
```

### With Custom Colors

```bash
bun -e "
import { renderMermaid } from 'beautiful-mermaid'
import { writeFileSync } from 'fs'
const svg = await renderMermaid(\`sequenceDiagram
  Alice->>Bob: Hello
  Bob-->>Alice: Hi\`, {
  bg: '#0f0f0f',
  fg: '#e0e0e0',
  accent: '#ff6b6b',
  muted: '#666666',
})
writeFileSync('sequence.svg', svg)
console.log('Saved sequence.svg')
" --cwd /tmp/beautiful-mermaid-workspace
```

### Transparent Background

```bash
bun -e "
import { renderMermaid, THEMES } from 'beautiful-mermaid'
import { writeFileSync } from 'fs'
const svg = await renderMermaid(\`graph LR
  A --> B --> C\`, { ...THEMES['dracula'], transparent: true })
writeFileSync('transparent.svg', svg)
console.log('Saved transparent.svg')
" --cwd /tmp/beautiful-mermaid-workspace
```

### Save to Specific Path

```bash
bun -e "
import { renderMermaid, THEMES } from 'beautiful-mermaid'
import { writeFileSync } from 'fs'
const svg = await renderMermaid(\`erDiagram
  USER ||--o{ POST : writes
  POST ||--o{ COMMENT : has\`, THEMES['github-dark'])
writeFileSync('/Users/me/docs/er-diagram.svg', svg)
console.log('Saved to /Users/me/docs/er-diagram.svg')
" --cwd /tmp/beautiful-mermaid-workspace
```

### Custom Font

```bash
bun -e "
import { renderMermaid } from 'beautiful-mermaid'
import { writeFileSync } from 'fs'
const svg = await renderMermaid(\`graph TD
  A --> B\`, { bg: '#fff', fg: '#333', font: 'JetBrains Mono' })
writeFileSync('mono-font.svg', svg)
" --cwd /tmp/beautiful-mermaid-workspace
```

## Batch Rendering

### Multiple Diagrams to Separate Files

```bash
bun -e "
import { renderMermaid, THEMES } from 'beautiful-mermaid'
import { writeFileSync } from 'fs'

const diagrams = [
  { name: 'flow', source: \`graph TD\n  A --> B --> C\` },
  { name: 'sequence', source: \`sequenceDiagram\n  A->>B: Hello\n  B-->>A: Hi\` },
  { name: 'class', source: \`classDiagram\n  class Foo {\n    +bar() void\n  }\` },
]

for (const d of diagrams) {
  const svg = await renderMermaid(d.source, THEMES['nord'])
  writeFileSync(\`\${d.name}.svg\`, svg)
  console.log(\`Saved \${d.name}.svg\`)
}
" --cwd /tmp/beautiful-mermaid-workspace
```

### All Themes Preview for One Diagram

```bash
bun -e "
import { renderMermaid, THEMES } from 'beautiful-mermaid'
import { writeFileSync, mkdirSync } from 'fs'

mkdirSync('theme-preview', { recursive: true })
const diagram = \`graph LR
  A[Input] --> B{Process}
  B -->|ok| C[Output]
  B -->|err| D[Error]\`

for (const [name, colors] of Object.entries(THEMES)) {
  const svg = await renderMermaid(diagram, colors)
  writeFileSync(\`theme-preview/\${name}.svg\`, svg)
  console.log(\`Saved theme-preview/\${name}.svg\`)
}
" --cwd /tmp/beautiful-mermaid-workspace
```

## Reading Diagram from File

```bash
bun -e "
import { renderMermaidAscii } from 'beautiful-mermaid'
import { readFileSync } from 'fs'
const source = readFileSync('/path/to/diagram.mmd', 'utf8')
console.log(renderMermaidAscii(source))
" --cwd /tmp/beautiful-mermaid-workspace
```

## Error Handling Pattern

```bash
bun -e "
import { renderMermaidAscii } from 'beautiful-mermaid'
try {
  console.log(renderMermaidAscii(\`graph TD
    A --> B\`))
} catch (e) {
  console.error('Render error:', e.message)
}
" --cwd /tmp/beautiful-mermaid-workspace
```

Common errors:
- `Invalid mermaid header` -- The first line doesn't match a known diagram type
- Parser errors – Malformed syntax (check edge/node format)
