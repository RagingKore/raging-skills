#!/usr/bin/env dotnet

// format-tables.cs
// Reads markdown from stdin or a file argument.
// Aligns all markdown tables so pipes form straight vertical lines.
// Non-table content passes through unchanged.

using System.Text;

var input = args.Length > 0
    ? File.ReadAllText(args[0])
    : Console.In.ReadToEnd();

Console.Write(FormatTables(input));

static string FormatTables(string text) {
    var lines = text.Split('\n');
    var result = new StringBuilder();
    var tableLines = new List<string>();

    for (var i = 0; i < lines.Length; i++) {
        if (IsTableLine(lines[i])) {
            tableLines.Add(lines[i]);
        } else {
            if (tableLines.Count > 0) {
                FlushTable(tableLines, result);
                tableLines.Clear();
            }
            result.Append(lines[i]);
            if (i < lines.Length - 1)
                result.Append('\n');
        }
    }

    if (tableLines.Count > 0)
        FlushTable(tableLines, result);

    return result.ToString();
}

static bool IsTableLine(string line) {
    var trimmed = line.TrimStart();
    return trimmed.StartsWith('|') && trimmed.TrimEnd().EndsWith('|');
}

static void FlushTable(List<string> lines, StringBuilder result) {
    // Parse each row into cells, preserving original separator row detection.
    var parsed = new List<(string[] Cells, bool IsSeparator)>();
    var colCount = 0;

    foreach (var line in lines) {
        var cells = ParseRow(line);
        if (cells.Length > colCount)
            colCount = cells.Length;
        parsed.Add((cells, IsSeparatorRow(line)));
    }

    // Compute max width per column from content rows only.
    var widths = new int[colCount];
    foreach (var (cells, isSep) in parsed) {
        if (isSep) continue;
        for (var c = 0; c < cells.Length; c++) {
            if (cells[c].Length > widths[c])
                widths[c] = cells[c].Length;
        }
    }

    // Emit aligned rows.
    foreach (var (cells, isSep) in parsed) {
        result.Append('|');
        for (var c = 0; c < colCount; c++) {
            if (isSep) {
                // Separator: dashes filling the column width + 2 for padding.
                result.Append(new string('-', widths[c] + 2));
            } else {
                var cell = c < cells.Length ? cells[c] : "";
                result.Append(' ');
                result.Append(cell.PadRight(widths[c]));
                result.Append(' ');
            }
            result.Append('|');
        }
        result.Append('\n');
    }
}

static string[] ParseRow(string line) {
    // Strip leading/trailing pipe, split on pipe, trim each cell.
    var trimmed = line.Trim();
    if (trimmed.StartsWith('|')) trimmed = trimmed[1..];
    if (trimmed.EndsWith('|')) trimmed = trimmed[..^1];

    var parts = trimmed.Split('|');
    for (var i = 0; i < parts.Length; i++)
        parts[i] = parts[i].Trim();

    return parts;
}

static bool IsSeparatorRow(string line) {
    var cells = ParseRow(line);
    return cells.All(c => c.Length > 0 && c.Trim('-', ':').Length == 0);
}
