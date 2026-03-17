# Spectre.Console Mapping

How to map Mockdown components to [Spectre.Console](https://spectreconsole.net/) widgets for .NET terminal UIs.
All widgets implement `IRenderable` and compose via `AnsiConsole.Write()`.

## Component Mapping

| Component      | Spectre.Console Widget                | Notes                                                |
|----------------|---------------------------------------|------------------------------------------------------|
| Button         | `Markup` styled as action text        | No native button; use `[blue][ OK ][/]` markup       |
| Input          | `TextPrompt<string>`                  | `.DefaultValue()` for placeholder text               |
| Checkbox       | `MultiSelectionPrompt<string>`        | Pre-select items with `.Select()` for `☑` state      |
| Radio          | `SelectionPrompt<string>`             | Single-select; highlighted item is `●`               |
| Dropdown       | `SelectionPrompt<string>`             | Same as radio; `.Title()` for the label              |
| Search         | `TextPrompt<string>`                  | No native search; use text prompt with hint          |
| Toggle         | `ConfirmationPrompt`                  | Yes/No maps to on/off state                          |
| Progress Bar   | `Progress` with `ProgressBarColumn`   | `.AddTask()` with value from `█`/`░` ratio           |
| Nav Bar        | `Grid` with `Markup` columns + `Rule` | Logo, links as grid columns; `Rule` as separator     |
| Tabs           | `Grid` with `Markup` + `Rule`         | Active tab in `[bold][ Tab ][/]`, inactive plain     |
| Breadcrumb     | `TextPath` or `Markup`                | `TextPath` for file-like paths; `Markup` for custom  |
| Pagination     | `Markup`                              | Render `< 1 2 [bold][3][/] 4 5 ... >` as styled text |
| Card           | `Panel` with `.Header()`              | `.Border(BoxBorder.Rounded)` for box-drawn look      |
| Dialog / Modal | `Panel` with header + nested `Grid`   | Buttons as `Markup` in the bottom grid row           |
| Split Panel    | `Layout` with `.SplitColumns()`       | Left/right panels as named layout children           |
| Table          | `Table`                               | `.AddColumn()`, `.AddRow()`, border styles           |
| List           | `Rows` of `Markup` with `•` prefix    | Or `Tree` for hierarchical lists                     |
| Box            | `Panel`                               | `.Expand()` for full width                           |
| Placeholder    | `Panel` with `Markup`                 | Render crosshatch as styled text inside panel        |
| Text           | `Markup` or `FigletText`              | `FigletText` for large headings                      |
| Line           | `Rule`                                | `.RuleStyle()` for custom appearance                 |
| Arrow          | `Markup`                              | Render `──────>` as styled text                      |

## Layout Patterns

Spectre.Console uses composable widgets rather than CSS-based layout.

### Side-by-side (row)

Use `Grid` or `Columns` to arrange widgets horizontally:

```csharp
var grid = new Grid();
grid.AddColumn();
grid.AddColumn();
grid.AddRow(new Panel("Left"), new Panel("Right"));
AnsiConsole.Write(grid);
```

### Stacked (column)

Use `Rows` to stack widgets vertically:

```csharp
AnsiConsole.Write(new Rows(
    new Rule("Header"),
    new Panel("Content"),
    new Rule("Footer")));
```

### Split layout

Use `Layout` for named, resizable regions:

```csharp
var layout = new Layout("Root")
    .SplitColumns(
        new Layout("Sidebar"),
        new Layout("Main")
            .SplitRows(
                new Layout("Content"),
                new Layout("Footer")));

layout["Sidebar"].Update(new Panel("Navigation").Expand());
layout["Content"].Update(new Panel("Page content").Expand());
AnsiConsole.Write(layout);
```

### Nesting

All `IRenderable` widgets nest inside each other. A `Table` can go inside a `Panel`, a `Panel` inside a
`Layout`, a `Tree` node can contain a `Table`:

```csharp
var table = new Table().AddColumn("Name").AddColumn("Role").AddRow("Alice", "Admin");
var card = new Panel(table).Header("Users").Border(BoxBorder.Rounded);
AnsiConsole.Write(card);
```

## Interactive vs Rendered

Spectre.Console distinguishes between rendered widgets (written to output) and interactive prompts (which wait
for user input):

- **Rendered**: `Table`, `Panel`, `Grid`, `Layout`, `Tree`, `Rule`, `Markup`, `BarChart`, `Rows`, `Columns`
- **Interactive**: `TextPrompt`, `SelectionPrompt`, `MultiSelectionPrompt`, `ConfirmationPrompt`
- **Live**: `Progress`, `Status` (animated, blocking)

Interactive prompts cannot be nested inside rendered widgets. When a wireframe shows a form with inputs and
buttons, generate the rendered layout first, then the interactive prompts sequentially.
