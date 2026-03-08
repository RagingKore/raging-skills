---
name: keep-a-changelog
description: |
  This skill should be used when the user asks to "create a changelog",
  "update the changelog", "add a changelog entry", "cut a release",
  "move unreleased to a version", "format changelog", "add comparison links",
  "document changes", or when creating, editing, or reviewing CHANGELOG.md
  files. Also use when the user mentions "keep a changelog", "changelog format",
  "release notes", "what changed", "yanked release", or needs guidance on
  changelog section types (Added,
  Changed, Deprecated, Removed, Fixed, Security).
---

# Keep a Changelog

Guide for creating and maintaining changelogs following the [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/) specification.

A changelog is a curated, chronologically ordered list of notable changes for each version of a project. It is written for humans, not machines.

## When to Use

- Creating a new `CHANGELOG.md` from scratch
- Adding entries under `[Unreleased]`
- Cutting a release (moving unreleased changes to a versioned section)
- Formatting or fixing an existing changelog
- Adding version comparison links
- Marking a release as yanked

## Quick Reference

### File

- Name the file `CHANGELOG.md` at the project root
- Start with a title: `# Changelog`
- Optionally include a preamble referencing the spec and SemVer adherence

### Section Types (in order)

| Section      | Purpose                            |
|--------------|------------------------------------|
| `Added`      | New features                       |
| `Changed`    | Changes to existing features       |
| `Deprecated` | Features marked for removal        |
| `Removed`    | Previously deprecated, now removed |
| `Fixed`      | Bug fixes                          |
| `Security`   | Vulnerability patches              |

Only include sections that have entries. Do not add empty sections.

### Version Header Format

```markdown
## [X.Y.Z] - YYYY-MM-DD
```

- Wrap the version in square brackets and link it to a comparison URL
- Use ISO 8601 date format (e.g., `2025-06-15`)
- Latest version goes first, `[Unreleased]` always at the top

### Entry Format

```markdown
### Added

- Description of the new feature
- Another new feature with [#123](link-to-issue) reference
```

Write entries as complete sentences or clear phrases. Each entry gets its own bullet. Reference issue/PR numbers where applicable.

## Creating a New Changelog

Start every new `CHANGELOG.md` with this structure:

```markdown
# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]
```

The `[Unreleased]` section captures changes that have not yet been assigned to a version. Add entries here as development progresses.

## Adding Entries

Place new entries under `[Unreleased]` in the appropriate section type. Create the section heading if it does not exist yet.

```markdown
## [Unreleased]

### Added

- User avatar upload support
- Export to CSV for reports

### Fixed

- Login timeout on slow connections
```

### Writing Good Entries

- Describe the change from the user's perspective, not the developer's
- Be specific: "Fix crash when uploading files over 10MB" not "Fix upload bug"
- Group related changes under a single entry when appropriate
- Reference issues or PRs: `Fix memory leak in worker pool ([#456](link))`

## Cutting a Release

To release a version, move `[Unreleased]` contents to a new versioned section:

1. Create a new version header below `[Unreleased]` with the version number and today's date
2. Move all section groups (Added, Changed, etc.) from `[Unreleased]` into the new version
3. Leave `[Unreleased]` empty (keep the heading)
4. Add a comparison link at the bottom of the file

**Before:**

```markdown
## [Unreleased]

### Added

- Dark mode support

### Fixed

- Header alignment on mobile
```

**After:**

```markdown
## [Unreleased]

## [1.2.0] - 2025-06-15

### Added

- Dark mode support

### Fixed

- Header alignment on mobile
```

## Version Comparison Links

At the bottom of `CHANGELOG.md`, maintain comparison links for every version. These let readers click a version header to see the full diff.

```markdown
[Unreleased]: https://github.com/user/repo/compare/v1.2.0...HEAD
[1.2.0]: https://github.com/user/repo/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/user/repo/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/user/repo/releases/tag/v1.0.0
```

- `[Unreleased]` always compares latest tag to `HEAD`
- Each version compares to its predecessor
- The first version links to its release tag (no comparison)
- Update the `[Unreleased]` link when cutting a new release

Infer the repository URL and tag format from the project's existing git remote and tags. Common tag formats: `v1.2.0`, `1.2.0`, `release/1.2.0`.

## Yanked Releases

Mark releases pulled from distribution with `[YANKED]`:

```markdown
## [1.1.1] - 2025-03-10 [YANKED]
```

Do not remove yanked versions from the changelog. The `[YANKED]` suffix signals the release should not be used.

## Guiding Principles

- Changelogs are for humans, not machines
- Every version gets its own section
- Group changes by type using the six standard sections
- Versions and sections must be linkable
- Latest version comes first
- Show release dates in ISO 8601 format
- Follow Semantic Versioning

## Additional Resources

For the complete specification including anti-patterns, edge cases, and a full example changelog:

- **[`references/specification.md`](references/specification.md)** - Full Keep a Changelog 1.1.0 specification reference
