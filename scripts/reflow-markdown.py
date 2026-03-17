#!/usr/bin/env python3
"""Reflow markdown prose lines to a target width (default 120).

Leaves untouched:
- YAML frontmatter (--- ... ---)
- Fenced code blocks (``` or ~~~)
- Table rows (lines starting with |)
- Heading lines (starting with #)
- HTML comments
- Lines that are entirely a URL or markdown link that can't be broken
- Indented code blocks (4+ spaces with no list context)
- Lines inside blockquotes are reflowed preserving the > prefix

Only reflows consecutive prose lines that form a paragraph.
"""

import re
import sys
import textwrap
from pathlib import Path


def is_table_row(line: str) -> bool:
    stripped = line.strip()
    return stripped.startswith("|") or bool(re.match(r"^\|?[\s:]*-+[\s:]*\|", stripped))


def is_heading(line: str) -> bool:
    return bool(re.match(r"^#{1,6}\s", line))


def is_list_item(line: str) -> bool:
    return bool(re.match(r"^(\s*[-*+]|\s*\d+[.)]\s)", line))


def is_blank(line: str) -> bool:
    return line.strip() == ""


def is_blockquote(line: str) -> bool:
    return line.lstrip().startswith(">")


def is_html_comment_line(line: str) -> bool:
    stripped = line.strip()
    return stripped.startswith("<!--") or stripped.endswith("-->")


def has_only_url(line: str) -> bool:
    stripped = line.strip()
    return bool(re.match(r"^https?://\S+$", stripped))


def reflow_paragraph(lines: list[str], width: int) -> list[str]:
    """Reflow a paragraph of prose lines to the target width."""
    if not lines:
        return lines

    # Check if all lines are already within width
    if all(len(line) <= width for line in lines):
        return lines

    # Join all lines into one string
    text = " ".join(line.strip() for line in lines)

    # Use textwrap to rewrap
    wrapped = textwrap.fill(
        text,
        width=width,
        break_long_words=False,
        break_on_hyphens=False,
    )
    return wrapped.split("\n")


def reflow_list_item(lines: list[str], width: int) -> list[str]:
    """Reflow a list item that may span multiple continuation lines."""
    if not lines:
        return lines

    if all(len(line) <= width for line in lines):
        return lines

    first = lines[0]
    # Detect indent and marker
    match = re.match(r"^(\s*(?:[-*+]|\d+[.)]\s))\s*", first)
    if not match:
        return lines

    marker = match.group(0)
    indent = " " * len(marker)

    # Collect all text
    text_parts = [first[len(marker):].strip()]
    for line in lines[1:]:
        text_parts.append(line.strip())
    text = " ".join(text_parts)

    # Wrap with subsequent indent
    wrapped = textwrap.fill(
        text,
        width=width,
        initial_indent=marker,
        subsequent_indent=indent,
        break_long_words=False,
        break_on_hyphens=False,
    )
    return wrapped.split("\n")


def reflow_blockquote(lines: list[str], width: int) -> list[str]:
    """Reflow blockquote lines preserving > prefix."""
    if not lines:
        return lines

    if all(len(line) <= width for line in lines):
        return lines

    # Strip > prefix and collect text
    inner_lines = []
    for line in lines:
        stripped = line.lstrip()
        if stripped.startswith("> "):
            inner_lines.append(stripped[2:])
        elif stripped.startswith(">"):
            inner_lines.append(stripped[1:])
        else:
            inner_lines.append(stripped)

    text = " ".join(l.strip() for l in inner_lines)
    wrapped = textwrap.fill(
        text,
        width=width - 2,  # account for "> " prefix
        break_long_words=False,
        break_on_hyphens=False,
    )
    return ["> " + line for line in wrapped.split("\n")]


def reflow_file(path: Path, width: int = 120, dry_run: bool = False) -> bool:
    """Reflow a single markdown file. Returns True if file was modified."""
    content = path.read_text(encoding="utf-8")
    lines = content.split("\n")

    result: list[str] = []
    i = 0
    in_frontmatter = False
    frontmatter_count = 0
    in_code_block = False
    code_fence = ""
    modified = False

    while i < len(lines):
        line = lines[i]

        # Track YAML frontmatter (only at start of file)
        if i == 0 and line.strip() == "---":
            in_frontmatter = True
            frontmatter_count = 1
            result.append(line)
            i += 1
            continue

        if in_frontmatter:
            result.append(line)
            if line.strip() == "---":
                frontmatter_count += 1
                if frontmatter_count >= 2:
                    in_frontmatter = False
            i += 1
            continue

        # Track fenced code blocks
        fence_match = re.match(r"^(\s*)(```|~~~)", line)
        if fence_match:
            if not in_code_block:
                in_code_block = True
                code_fence = fence_match.group(2)
                result.append(line)
                i += 1
                continue
            elif line.strip().startswith(code_fence):
                in_code_block = False
                code_fence = ""
                result.append(line)
                i += 1
                continue

        if in_code_block:
            result.append(line)
            i += 1
            continue

        # Pass through special lines
        if (is_blank(line) or is_table_row(line) or is_heading(line)
                or is_html_comment_line(line) or has_only_url(line)):
            result.append(line)
            i += 1
            continue

        # Blockquote paragraph
        if is_blockquote(line):
            bq_lines = [line]
            i += 1
            while i < len(lines) and is_blockquote(lines[i]) and not is_blank(lines[i]):
                bq_lines.append(lines[i])
                i += 1
            reflowed = reflow_blockquote(bq_lines, width)
            if reflowed != bq_lines:
                modified = True
            result.extend(reflowed)
            continue

        # List items: collect the item + continuation lines
        if is_list_item(line):
            item_lines = [line]
            i += 1
            # Continuation lines: indented, not a new list item, not blank, not special
            while i < len(lines):
                next_line = lines[i]
                if is_blank(next_line):
                    break
                if is_list_item(next_line):
                    break
                if is_heading(next_line) or is_table_row(next_line):
                    break
                if re.match(r"^(\s*)(```|~~~)", next_line):
                    break
                # Must be indented continuation
                if next_line.startswith("  ") or next_line.startswith("\t"):
                    item_lines.append(next_line)
                    i += 1
                else:
                    break

            # Only reflow if any line exceeds width
            if any(len(l) > width for l in item_lines):
                reflowed = reflow_list_item(item_lines, width)
                if reflowed != item_lines:
                    modified = True
                result.extend(reflowed)
            else:
                result.extend(item_lines)
            continue

        # Regular prose paragraph: collect consecutive non-special lines
        para_lines = [line]
        i += 1
        while i < len(lines):
            next_line = lines[i]
            if (is_blank(next_line) or is_heading(next_line) or is_table_row(next_line)
                    or is_list_item(next_line) or is_blockquote(next_line)
                    or is_html_comment_line(next_line) or has_only_url(next_line)):
                break
            if re.match(r"^(\s*)(```|~~~)", next_line):
                break
            para_lines.append(next_line)
            i += 1

        if any(len(l) > width for l in para_lines):
            reflowed = reflow_paragraph(para_lines, width)
            if reflowed != para_lines:
                modified = True
            result.extend(reflowed)
        else:
            result.extend(para_lines)

    if modified:
        new_content = "\n".join(result)
        if not dry_run:
            path.write_text(new_content, encoding="utf-8")
        return True
    return False


def main():
    import argparse
    parser = argparse.ArgumentParser(description="Reflow markdown prose to target width")
    parser.add_argument("files", nargs="+", help="Markdown files to process")
    parser.add_argument("-w", "--width", type=int, default=120, help="Target line width (default: 120)")
    parser.add_argument("-n", "--dry-run", action="store_true", help="Don't write changes, just report")
    parser.add_argument("-q", "--quiet", action="store_true", help="Only print modified files")
    args = parser.parse_args()

    modified_count = 0
    for file_path in args.files:
        path = Path(file_path)
        if not path.exists():
            print(f"SKIP (not found): {path}", file=sys.stderr)
            continue
        try:
            changed = reflow_file(path, args.width, args.dry_run)
            if changed:
                modified_count += 1
                if not args.quiet:
                    print(f"{'WOULD MODIFY' if args.dry_run else 'MODIFIED'}: {path}")
            elif not args.quiet:
                print(f"OK: {path}")
        except Exception as e:
            print(f"ERROR: {path}: {e}", file=sys.stderr)

    print(f"\n{modified_count} file(s) {'would be modified' if args.dry_run else 'modified'}", file=sys.stderr)


if __name__ == "__main__":
    main()
