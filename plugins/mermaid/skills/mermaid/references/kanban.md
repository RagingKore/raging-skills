# Kanban Diagram

## Declaration

The diagram begins with the `kanban` keyword, followed by column and task definitions using indentation.

```
kanban
  columnId[Column Title]
    taskId[Task Description]
```

## Complete Syntax Reference

### Columns

Columns represent workflow stages (e.g., "Todo", "In Progress", "Done").

| Syntax | Description |
|--------|-------------|
| `columnId[Column Title]` | Column with explicit ID and title |
| `[Column Title]` | Column with auto-generated ID (title only) |
| `ColumnTitle` | Column using the identifier as both ID and title (no spaces allowed) |

- Column identifiers must be unique within the diagram.
- Column titles are enclosed in square brackets.
- Columns are defined at the root indentation level (no indent or one level of indent).

### Tasks

Tasks are listed under their parent column with additional indentation.

| Syntax | Description |
|--------|-------------|
| `taskId[Task Description]` | Task with explicit ID and description |
| `[Task Description]` | Task with auto-generated ID (description only) |

- Task identifiers should be unique within the diagram.
- Task descriptions are enclosed in square brackets.
- Tasks must be indented under their parent column.

### Task Metadata

Metadata is appended to a task using the `@{ ... }` syntax with key-value pairs.

```
taskId[Task Description]@{ key: value, key2: 'value2' }
```

| Metadata Key | Type | Description | Allowed Values |
|-------------|------|-------------|----------------|
| `assigned` | String | Person responsible for the task | Any string (e.g., `'knsv'`, `'K.Sveidqvist'`) |
| `ticket` | String | Ticket or issue reference number | Any string (e.g., `'MC-2037'`) |
| `priority` | String | Task priority level | `'Very High'`, `'High'`, `'Low'`, `'Very Low'` |

- Values containing spaces must be wrapped in single quotes.
- Multiple metadata keys are separated by commas.
- Metadata is rendered as part of the task card.

### Indentation Rules

Indentation is critical and defines the hierarchy:

```
kanban
  column1[Title]          <-- Column level (indented under kanban)
    task1[Description]    <-- Task level (indented under column)
  column2[Title]          <-- Another column
    task2[Description]    <-- Task under column2
```

| Level | Indentation | Element |
|-------|-------------|---------|
| 0 | None / base | `kanban` keyword |
| 1 | 1 level | Column definitions |
| 2 | 2 levels | Task definitions |

### Comments

```
%% This is a comment
```

## Styling & Configuration

### Configuration via Frontmatter

```yaml
---
config:
  kanban:
    ticketBaseUrl: 'https://yourproject.atlassian.net/browse/#TICKET#'
---
```

### Configuration Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `ticketBaseUrl` | String | Base URL for external ticket links. The placeholder `#TICKET#` is replaced with the task's `ticket` metadata value. |

When `ticketBaseUrl` is set and a task has a `ticket` metadata value, the ticket number in the rendered diagram becomes a clickable link to the external system.

**URL template example:**

| Template | Ticket Value | Resulting URL |
|----------|-------------|---------------|
| `https://jira.example.com/browse/#TICKET#` | `MC-2037` | `https://jira.example.com/browse/MC-2037` |
| `https://github.com/org/repo/issues/#TICKET#` | `42` | `https://github.com/org/repo/issues/42` |

## Practical Examples

### Example 1 -- Minimal Kanban Board

```mermaid
kanban
  todo[Todo]
    task1[Write tests]
    task2[Fix bug]
  done[Done]
    task3[Deploy v1.0]
```

### Example 2 -- Board with Metadata

```mermaid
kanban
  todo[Todo]
    id1[Create Documentation]
    id2[Update API endpoints]@{ assigned: 'alice', priority: 'High' }
  inProgress[In Progress]
    id3[Implement login]@{ ticket: PROJ-101, assigned: 'bob', priority: 'Very High' }
  done[Done]
    id4[Setup CI/CD]@{ assigned: 'charlie' }
```

### Example 3 -- Auto-ID Columns and Tasks

```mermaid
kanban
  Todo
    [Create Documentation]
    [Review pull requests]
  [In Progress]
    [Build dashboard component]
  [Done]
    [Setup database]
```

### Example 4 -- Full Board with Ticket Links

```mermaid
---
config:
  kanban:
    ticketBaseUrl: 'https://mermaidchart.atlassian.net/browse/#TICKET#'
---
kanban
  Todo
    [Create Documentation]
    docs[Create Blog about the new diagram]
  [In progress]
    id6[Create renderer so that it works in all cases. We also add some extra text here for testing purposes. And some more just for the extra flare.]
  id9[Ready for deploy]
    id8[Design grammar]@{ assigned: 'knsv' }
  id10[Ready for test]
    id4[Create parsing tests]@{ ticket: MC-2038, assigned: 'K.Sveidqvist', priority: 'High' }
    id66[last item]@{ priority: 'Very Low', assigned: 'knsv' }
  id11[Done]
    id5[define getData]
    id2[Title of diagram is more than 100 chars when user duplicates diagram with 100 char]@{ ticket: MC-2036, priority: 'Very High'}
    id3[Update DB function]@{ ticket: MC-2037, assigned: knsv, priority: 'High' }
  id12[Can't reproduce]
    id3[Weird flickering in Firefox]
```

### Example 5 -- Sprint Board

```mermaid
---
config:
  kanban:
    ticketBaseUrl: 'https://github.com/myorg/myrepo/issues/#TICKET#'
---
kanban
  backlog[Backlog]
    b1[Research caching strategies]@{ priority: 'Low' }
    b2[Write architecture doc]@{ assigned: 'dana' }
  todo[Sprint Todo]
    t1[Add rate limiting]@{ ticket: 142, assigned: 'eve', priority: 'High' }
    t2[Fix memory leak]@{ ticket: 138, assigned: 'frank', priority: 'Very High' }
  inProgress[In Progress]
    p1[Refactor auth module]@{ ticket: 135, assigned: 'eve', priority: 'High' }
  review[Code Review]
    r1[Add unit tests for parser]@{ ticket: 130, assigned: 'dana' }
  done[Done]
    d1[Setup monitoring]@{ ticket: 125, assigned: 'frank' }
    d2[Update dependencies]@{ ticket: 128, assigned: 'eve' }
```

## Common Gotchas

| Issue | Cause | Fix |
|-------|-------|-----|
| Tasks appear as columns | Incorrect indentation | Tasks must be indented one level deeper than their parent column |
| Diagram won't render | Missing `kanban` keyword | Ensure the diagram starts with `kanban` on the first line |
| Ticket links not working | `ticketBaseUrl` not set or missing `#TICKET#` placeholder | Set `ticketBaseUrl` in frontmatter config with `#TICKET#` in the URL |
| Metadata not rendering | Incorrect `@{}` syntax | Use `@{ key: 'value' }` with proper comma separation |
| Priority not recognized | Invalid priority string | Use exactly: `'Very High'`, `'High'`, `'Low'`, or `'Very Low'` |
| Column title with spaces breaks | Using spaces without brackets | Wrap column titles in square brackets: `[In Progress]` |
| Duplicate IDs cause merge | Same ID used for different tasks/columns | Ensure all identifiers are unique across the diagram |
| Metadata values with spaces | Unquoted multi-word values | Wrap values in single quotes: `assigned: 'John Doe'` |
