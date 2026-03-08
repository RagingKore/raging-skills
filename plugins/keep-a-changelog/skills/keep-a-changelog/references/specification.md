# Keep a Changelog 1.1.0 Specification Reference

Complete reference for the [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) specification.

## What Is a Changelog?

A changelog is a file containing a curated, chronologically ordered list of notable changes for each version of a project. Unlike a git log or commit history, a changelog is written for human readers and focuses on what matters to users of the project.

## File Convention

- File name: `CHANGELOG.md`
- Location: project root
- Format: Markdown

## Document Structure

```markdown
# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.1.0] - 2025-06-15

### Added

- New feature description

### Fixed

- Bug fix description

## [1.0.0] - 2025-01-01

### Added

- Initial release features

[Unreleased]: https://github.com/user/repo/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/user/repo/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/user/repo/releases/tag/v1.0.0
```

### Structural Rules

1. **Title**: Start with `# Changelog`
2. **Preamble**: Optional paragraph describing the format and SemVer adherence
3. **`[Unreleased]` section**: Always present at the top, tracks pending changes
4. **Version sections**: Reverse chronological order (newest first)
5. **Comparison links**: At the bottom of the file, one per version plus `[Unreleased]`

## Section Types

The specification defines exactly six section types. Always use them in this order when present. Omit sections with no entries.

### Added

New features introduced in this version.

```markdown
### Added

- User profile avatars
- CSV export for transaction reports
- Dark mode toggle in settings
```

### Changed

Changes to existing functionality. Includes behavior changes, UI redesigns, and API modifications.

```markdown
### Changed

- Upgrade minimum Node.js version from 16 to 18
- Dashboard layout uses responsive grid instead of fixed columns
- `GET /api/users` now returns paginated results by default
```

### Deprecated

Features that still work but are scheduled for removal in a future version. This section signals to users that they should migrate away.

```markdown
### Deprecated

- `GET /api/v1/users` endpoint, use `/api/v2/users` instead
- `--legacy-auth` flag, will be removed in 3.0.0
```

### Removed

Features that were previously deprecated and are now removed.

```markdown
### Removed

- Python 3.7 support
- `GET /api/v1/legacy` endpoint
- `--xml-output` flag
```

### Fixed

Bug fixes.

```markdown
### Fixed

- Crash when uploading files larger than 10MB ([#456](https://github.com/user/repo/issues/456))
- Incorrect timezone conversion for UTC+13 regions
- Memory leak in background worker pool
```

### Security

Changes that address security vulnerabilities. This section exists to draw attention to security-sensitive changes so users can update promptly.

```markdown
### Security

- Patch XSS vulnerability in markdown renderer (CVE-2025-1234)
- Upgrade dependency `libxml2` to fix buffer overflow
- Enforce HTTPS for all API endpoints
```

## Version Headers

### Format

```
## [VERSION] - YYYY-MM-DD
```

- **Version**: Semantic version number wrapped in square brackets
- **Date**: ISO 8601 format (year-month-day), e.g., `2025-06-15`
- **Separator**: Single hyphen surrounded by spaces between version and date

### Date Format

Always use ISO 8601: `YYYY-MM-DD`

- Correct: `2025-06-15`
- Wrong: `06/15/2025`, `15-06-2025`, `2025/06/15`, `June 15, 2025`

ISO 8601 is unambiguous across cultures and sorts correctly.

### Semantic Versioning

Keep a Changelog recommends following [Semantic Versioning 2.0.0](https://semver.org/):

- **MAJOR**: Incompatible API changes
- **MINOR**: Backward-compatible new functionality
- **PATCH**: Backward-compatible bug fixes

The preamble should state whether the project adheres to SemVer.

## Unreleased Section

The `[Unreleased]` section sits at the top of the changelog and tracks changes that have not been assigned to a version.

### Purpose

- Provides a running list of what is coming in the next release
- Simplifies the release process: move entries from `[Unreleased]` to a new version section
- Gives contributors visibility into pending changes

### Maintaining It

- Add entries as changes are merged, not at release time
- Organize entries into the standard section types
- When cutting a release, move all entries to a new version section and leave `[Unreleased]` empty

## Yanked Releases

A yanked release is one that was pulled from distribution due to a serious bug or security issue.

### Format

```markdown
## [1.0.1] - 2025-03-10 [YANKED]
```

### Rules

- Append `[YANKED]` after the date
- Do not delete yanked versions from the changelog
- The yanked release should still have its comparison link
- Explain in the entries why it was yanked, if appropriate

## Version Comparison Links

Every version header is a markdown link. The link definitions go at the bottom of the file.

### Link Format

```markdown
[Unreleased]: https://github.com/user/repo/compare/v1.2.0...HEAD
[1.2.0]: https://github.com/user/repo/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/user/repo/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/user/repo/releases/tag/v1.0.0
```

### Rules

1. `[Unreleased]` compares the latest release tag to `HEAD`
2. Each version compares to the previous version
3. The very first version links to its release tag (no predecessor to compare)
4. Update the `[Unreleased]` link every time a new version is cut

### Platform Variations

**GitHub**:
```
https://github.com/user/repo/compare/v1.0.0...v1.1.0
```

**GitLab**:
```
https://gitlab.com/user/repo/-/compare/v1.0.0...v1.1.0
```

**Bitbucket**:
```
https://bitbucket.org/user/repo/branches/compare/v1.1.0%0Dv1.0.0
```

**Azure DevOps**:
```
https://dev.azure.com/org/project/_git/repo/branchCompare?baseVersion=GTv1.0.0&targetVersion=GTv1.1.0
```

## Anti-Patterns

### Dumping Git Logs

Do not use `git log --oneline` as a changelog. Git logs contain noise (merge commits, WIP commits, typo fixes) and are written for developers, not users.

**Bad:**
```markdown
- Merge branch 'feature/dark-mode'
- fix typo
- WIP
- Update package.json
- Add dark mode toggle
```

**Good:**
```markdown
### Added

- Dark mode toggle in settings
```

### Ignoring Deprecations

Failing to document deprecations leaves users unprepared for breaking changes. Always add a `Deprecated` entry before removing a feature so users can migrate.

### Inconsistent Formatting

Mixing date formats, omitting sections, or using different heading styles reduces readability. Follow the spec format consistently.

### No Unreleased Section

Without an `[Unreleased]` section, there is no visible record of pending changes and the release process requires manually gathering changes.

### Missing Comparison Links

Without comparison links, version headers are just text. Clickable links let readers inspect exactly what changed between versions.

## Complete Example

```markdown
# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Greek translation

## [1.1.1] - 2023-03-05

### Added

- Arabic translation

### Changed

- Upgrade dependencies

### Fixed

- Fix typos in Indonesian translation

## [1.1.0] - 2019-02-15

### Added

- Danish translation

### Changed

- Update year in README

### Fixed

- Improve French translation

## [1.0.0] - 2017-06-20

### Added

- New visual identity
- Version navigation
- Links to latest released version
- "Why keep a changelog?" section
- "Who needs a changelog?" section
- "How do I make a changelog?" section
- "Frequently Asked Questions" section
- Simplified and Traditional Chinese translations
- French translation

### Changed

- Upgrade project to Jekyll 3.7.4
- Use `markdownify` filter for homepage description

### Removed

- Unused includes directory

[Unreleased]: https://github.com/olivierlacan/keep-a-changelog/compare/v1.1.1...HEAD
[1.1.1]: https://github.com/olivierlacan/keep-a-changelog/compare/v1.1.0...v1.1.1
[1.1.0]: https://github.com/olivierlacan/keep-a-changelog/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/olivierlacan/keep-a-changelog/releases/tag/v1.0.0
```
