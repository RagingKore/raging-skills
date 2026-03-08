# Verification Checklist

Complete this checklist to verify your plugin works correctly in Claude Code.

## Installation

- [ ] Plugin installed successfully
- [ ] No console errors on load
- [ ] Plugin appears in `/list-plugins`

## Auto-Loaded Skills

[For each auto-loaded skill]

**[Skill Name]**

- [ ] Skill activates when given trigger phrase: "[trigger phrase 1]"
- [ ] Skill activates when given trigger phrase: "[trigger phrase 2]"
- [ ] Content is accurate and helpful
- [ ] References and examples load correctly
- [ ] No errors in output

## User-Invoked Commands

[For each user-invoked command/skill]

**`/plugin-name:command-name`**

- [ ] Command appears in slash command menu
- [ ] Command runs without errors
- [ ] Command accepts expected arguments
- [ ] Side effects occur as expected: [specify side effects]
- [ ] Help text is clear

## Agents

[For each agent]

**[Agent Name]**

- [ ] Agent triggers in expected scenario: [describe scenario]
- [ ] Agent accepts correct tools: [list tools]
- [ ] Agent produces expected output
- [ ] Agent examples in description work as written

## Hooks

[If applicable]

- [ ] Hooks activate on correct events
- [ ] Hook validation works as designed
- [ ] Hook prompts execute without errors
- [ ] Hook scripts run successfully

## MCP Integration

[If applicable]

- [ ] MCP servers initialize successfully
- [ ] All required tools are available via `/mcp`
- [ ] Tools work with expected parameters
- [ ] Authentication credentials work (if applicable)

## Settings

[If applicable]

- [ ] .local.md file is properly ignored in git
- [ ] Settings are read correctly by plugin
- [ ] Defaults work when no settings provided
- [ ] Custom settings override defaults

## Overall

- [ ] No unexpected errors in console
- [ ] Plugin integrates well with Claude Code
- [ ] All documented features work as described
- [ ] Plugin is ready for use
