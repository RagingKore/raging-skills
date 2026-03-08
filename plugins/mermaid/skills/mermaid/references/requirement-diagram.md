# Requirement Diagram

## Declaration

Use the keyword `requirementDiagram` to start a requirement diagram. This diagram follows the SysML v1.6 modeling specification.

```
requirementDiagram
```

## Complete Syntax Reference

A requirement diagram has three component types: **requirements**, **elements**, and **relationships**.

### Direction

Set the layout direction immediately after the declaration:

```
requirementDiagram
direction LR
```

| Value | Direction       |
|-------|-----------------|
| `TB`  | Top to Bottom (default) |
| `BT`  | Bottom to Top   |
| `LR`  | Left to Right   |
| `RL`  | Right to Left   |

### Text Quoting

All user-defined text can optionally be surrounded in quotes. Unquoted input will fail if it contains a reserved keyword. Quoted text supports markdown formatting: `"**bold** and *italics*"`.

## Components / Elements

### Requirements

```
<type> <name> {
    id: <user_defined_id>
    text: <user_defined_text>
    risk: <risk_level>
    verifymethod: <method>
}
```

All fields inside the braces are optional -- you can include any combination.

#### Requirement Types

| Type                       | Description                      |
|----------------------------|----------------------------------|
| `requirement`              | Generic requirement              |
| `functionalRequirement`    | Functional requirement           |
| `interfaceRequirement`     | Interface requirement            |
| `performanceRequirement`   | Performance requirement          |
| `physicalRequirement`      | Physical requirement             |
| `designConstraint`         | Design constraint                |

#### Risk Levels

| Value    |
|----------|
| `low`    |
| `medium` |
| `high`   |

#### Verification Methods

| Value           |
|-----------------|
| `analysis`      |
| `inspection`    |
| `test`          |
| `demonstration` |

### Elements

Elements are lightweight references to external documents or system components that connect to requirements.

```
element <name> {
    type: <user_defined_type>
    docRef: <user_defined_reference>
}
```

| Field    | Required | Description                                  |
|----------|----------|----------------------------------------------|
| `type`   | No       | User-defined type (e.g., `simulation`, `"test suite"`) |
| `docRef` | No       | Reference to external document or location   |

## Connections / Relationships

### Syntax

Forward direction:

```
{source_name} - <type> -> {destination_name}
```

Reverse direction:

```
{destination_name} <- <type> - {source_name}
```

### Relationship Types

| Type        | Description                                          |
|-------------|------------------------------------------------------|
| `contains`  | Source contains the destination                       |
| `copies`    | Source copies the destination                         |
| `derives`   | Source derives from the destination                  |
| `satisfies` | Source satisfies the destination requirement         |
| `verifies`  | Source verifies the destination requirement          |
| `refines`   | Source refines the destination                       |
| `traces`    | Source traces to the destination                     |

Each relationship is automatically labeled in the rendered diagram.

## Styling & Configuration

### Direct Styling

Apply CSS styles directly to requirements or elements using the `style` keyword:

```
style <name> fill:#color,stroke:#color,color:#color
```

Multiple names can be styled in a single statement:

```
style name1,name2 fill:#ffa,stroke:#000
```

### Class Definitions

Define reusable styles with `classDef`:

```
classDef <className> fill:#color,stroke:#color,stroke-width:2px
```

### Default Class

A class named `default` is applied to all nodes automatically:

```
classDef default fill:#f9f,stroke:#333,stroke-width:4px;
```

### Applying Classes

Two methods to apply classes:

1. Using the `class` keyword (supports multiple nodes and classes):

```
class name1,name2 className
```

2. Using the `:::` shorthand (single node, supports multiple classes):

During definition:

```
requirement my_req:::important {
    id: 1
    text: example
    risk: low
    verifymethod: test
}
```

After definition:

```
element my_elem {
}
my_elem:::myClass
```

## Practical Examples

### Example 1: Basic Requirement and Element

```mermaid
requirementDiagram

requirement login_req {
    id: REQ-001
    text: Users must authenticate before accessing the system
    risk: high
    verifymethod: test
}

element login_module {
    type: software module
}

login_module - satisfies -> login_req
```

### Example 2: Requirement Hierarchy

```mermaid
requirementDiagram

requirement system_req {
    id: REQ-001
    text: System must process 1000 requests per second
    risk: high
    verifymethod: demonstration
}

performanceRequirement perf_req {
    id: REQ-001.1
    text: API response time must be under 200ms
    risk: medium
    verifymethod: test
}

designConstraint db_constraint {
    id: REQ-001.2
    text: Must use connection pooling
    risk: low
    verifymethod: inspection
}

system_req - contains -> perf_req
system_req - contains -> db_constraint
perf_req - derives -> db_constraint
```

### Example 3: Traceability with Elements and Multiple Relationship Types

```mermaid
requirementDiagram

requirement main_req {
    id: 1
    text: the main requirement.
    risk: high
    verifymethod: test
}

functionalRequirement func_req {
    id: 1.1
    text: functional sub-requirement.
    risk: low
    verifymethod: inspection
}

performanceRequirement perf_req {
    id: 1.2
    text: performance sub-requirement.
    risk: medium
    verifymethod: demonstration
}

element test_suite {
    type: "test suite"
    docRef: github.com/all_the_tests
}

element spec_doc {
    type: word doc
    docRef: reqs/spec
}

test_suite - verifies -> perf_req
main_req - contains -> func_req
main_req - contains -> perf_req
func_req - traces -> perf_req
main_req <- copies - spec_doc
```

### Example 4: Left-to-Right with Styling

```mermaid
requirementDiagram

direction LR

requirement high_risk_req:::critical {
    id: SEC-001
    text: All data must be encrypted at rest
    risk: high
    verifymethod: inspection
}

element encryption_service {
    type: service
}

encryption_service - satisfies -> high_risk_req

classDef critical fill:#f96,stroke:#333,stroke-width:4px
style encryption_service fill:#9f9,stroke:#333
```

### Example 5: Markdown Formatting in Requirements

```mermaid
requirementDiagram

requirement "__Security Policy__" {
    id: SEC-100
    text: "*All endpoints* must use **TLS 1.3+**"
    risk: high
    verifymethod: test
}

element test_suite {
    type: "automated tests"
    docRef: github.com/security-tests
}

test_suite - verifies -> "__Security Policy__"
```

## Common Gotchas

- **Unquoted text with keywords fails**: If user-defined text contains a keyword like `test`, `risk`, or `requirement`, wrap the text in quotes to avoid parser errors.
- **Relationship direction matters**: `A - satisfies -> B` means A satisfies B. The arrow direction indicates the relationship target.
- **Reverse syntax exists**: `B <- satisfies - A` is equivalent to `A - satisfies -> B`. Use whichever reads more naturally.
- **The `:::` shorthand only applies to one node**: Unlike `class` which accepts multiple comma-separated names, `:::` can only be attached to a single requirement or element at a time.
- **`classDef default` applies to everything**: If you define a `default` class, all nodes inherit it. Override specific nodes with additional styles defined afterward.
- **`docRef` vs `docref`**: The field name is case-sensitive in the source -- `docRef` is the documented form, but the parser accepts `docref` as well.
