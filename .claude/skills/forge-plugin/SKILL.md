---
name: forge-plugin
description: |
  Guide users through creating a complete Claude Code plugin from concept to tested implementation.
  Use when:
    - User wants to create a new plugin but isn't sure where to start
    - User has a plugin idea but needs help fleshing out requirements and design
    - User has a plugin partially created but needs help completing it and adding it to the marketplace
    - User wants to ensure their plugin follows best practices and is high quality
    - User wants to improve the quality of their existing plugin
argument-hint: [plugin purpose or idea]
---

# Plugin Creation Workflow

Guide the user through creating a complete, high-quality Claude Code plugin from initial concept to tested implementation. Follow a systematic approach: understand requirements, design components, clarify details, implement following best practices, validate, and test.

## Core Principles

- **Ask clarifying questions**: Identify all ambiguities about plugin purpose, triggering, scope, and components. Ask specific, concrete questions rather than making assumptions. Wait for user answers before proceeding with implementation.
- **Load relevant skills**: Use the Skill tool to load plugin-dev skills when needed (plugin-structure, hook-development, agent-development, etc.)
- **Use specialized agents**: Delegate work to agent teams (TeamCreate) or specific agents (Task tool with subagent_type) for parallel, AI-assisted development.
- **Use structured user input**: Leverage AskUserQuestion tool for key decision points with concrete options.
- **Track progress**: Use TaskCreate/TaskUpdate/TaskList for systematic progress tracking throughout all phases.
- **Follow best practices**: Apply patterns from plugin-dev's own implementation
- **Progressive disclosure**: Create lean skills with references/examples

**Initial request:** $ARGUMENTS

---

## Phase 0: Triage

**Goal**: Determine the workflow mode based on what the user already has.

**Actions**:
1. Analyze the initial request and any referenced files/directories to detect the mode.
2. If ambiguous, ask the user: "What are we working with?" via AskUserQuestion:
   - Option 1: "🆕 Create a new plugin from scratch"
   - Option 2: "🔧 Improve an existing plugin"
   - Option 3: "📦 Import a WIP or external plugin"

### 🆕 Create mode (no existing plugin)

Proceed with Phases 1–8 as normal.

### 🔧 Improve mode (existing plugin provided)

1. Read the plugin directory structure and plugin.json
2. Run plugin-validator agent to assess current state
3. Run skill-reviewer agent on each skill
4. Present findings summary to the user using [plugin-health-report.md template](templates/plugin-health-report.md)
5. Ask the user: "Which issues should I fix?" via AskUserQuestion:
   - Option 1: "✨ Fix all issues" (Recommended)
   - Option 2: "🚨 Fix critical only"
   - Option 3: "📋 Let me pick from the list"
6. Apply fixes, then jump to Phase 6 (Validation) to verify.

### 📦 Import mode (WIP, external, or partially built plugin)

1. Read the existing files and identify what's already done vs what's missing
2. Gap-analyze against plugin-dev patterns (structure, naming, frontmatter, references/)
3. Run skill-reviewer agent on each skill to identify quality issues
4. Present a migration plan to the user using [migration-plan.md template](templates/migration-plan.md)
5. Ask the user: "Does this migration plan look right?" via AskUserQuestion:
   - Option 1: "✅ Approve and execute" (Recommended)
   - Option 2: "🔧 Adjust the plan"
   - Option 3: "🏗 Just fix structure, skip content"
6. Execute the plan, then jump to Phase 6 (Validation) to verify.

---

## Phase 1: Discovery

**Goal**: Understand what plugin needs to be built and what problem it solves

**Actions**:
1. Create tasks for all 8 phases:
   - Use TaskCreate for each phase as a separate task
   - Include activeForm (e.g., "Discovering plugin requirements", "Planning components") for spinner display
2. If plugin purpose is clear from arguments:
   - Summarize understanding
   - Identify plugin type (integration, workflow, analysis, toolkit, etc.)
3. If plugin purpose is unclear, ask the user: "🤔 Help me understand what you're building" via AskUserQuestion:
   - What problem does this plugin solve?
   - Who will use it and when?
   - What should it do?
   - Any similar plugins to reference?
4. Summarize understanding and confirm with user before proceeding

**Output**: Clear statement of plugin purpose and target users

---

## Phase 2: Component Planning

**Goal**: Determine what plugin components are needed

**MUST load plugin-structure skill** using Skill tool before this phase.

**Actions**:
1. Load plugin-structure skill to understand component types
2. Analyze plugin requirements and determine needed components:
   - **Skills (auto-loaded)**: Does it need specialized knowledge that Claude loads automatically? (hooks API, MCP patterns, etc.)
   - **Skills (user-invoked)**: User-initiated actions with side effects? (deploy, configure, analyze). These use `disable-model-invocation: true` and create a `/slash-command`. Note: custom slash commands have been merged into skills; the `commands/` directory still works but skills are the preferred approach.
   - **Agents**: Autonomous tasks? (validation, generation, analysis)
   - **Hooks**: Event-driven automation? (validation, notifications)
   - **MCP**: External service integration? (databases, APIs)
   - **Settings**: User configuration? (.local.md files)
3. For each component type needed, identify:
   - How many of each type
   - What each one does
   - Rough triggering/usage patterns
4. Present component plan to user using [component-plan.md template](templates/component-plan.md)
5. Ask the user: "Does this component plan look right to you?" via AskUserQuestion:
   - Option 1: "✅ Approve this plan" (Recommended)
   - Option 2: "🔧 Adjust components"
   - Option 3: "🔄 Start over with different approach"

**Output**: Confirmed list of components to create

---

## Phase 3: Detailed Design & Clarifying Questions

**Goal**: Specify each component in detail and resolve all ambiguities

**CRITICAL**: This is one of the most important phases. DO NOT SKIP.

**Actions**:
1. For each component in the plan, identify underspecified aspects:
   - **Skills (auto-loaded)**: What triggers them? What knowledge do they provide? How detailed?
   - **Skills (user-invoked)**: What arguments? What tools? Interactive or automated? What side effects?
   - **Agents**: When to trigger (proactive/reactive)? What tools? Output format?
   - **Hooks**: Which events? Prompt or command based? Validation criteria?
   - **MCP**: What server type? Authentication? Which tools?
   - **Settings**: What fields? Required vs optional? Defaults?

2. **Present all questions to user in organized sections** (one section per component type)

3. **Use AskUserQuestion for key decisions**:
   - Group decisions by component type (e.g., "Skills Configuration", "Agent Behavior")
   - Provide concrete options with recommendations marked "(Recommended)"
   - If user selects "Other", accept custom input

4. **Wait for answers before proceeding to implementation**

5. If user says "whatever you think is best" or similar, provide specific recommendations and get explicit confirmation via AskUserQuestion

**Example questions for a skill**:
- What specific user queries should trigger this skill?
- Should it include utility scripts? What functionality?
- How detailed should the core SKILL.md be vs references/?
- Any real-world examples to include?

**Example questions for an agent**:
- Should this agent trigger proactively after certain actions, or only when explicitly requested?
- What tools does it need (Read, Write, Bash, etc.)?
- What should the output format be?
- Any specific quality standards to enforce?

**Output**: Detailed specification for each component

---

## Phase 4: Plugin Structure Creation

**Goal**: Create plugin directory structure and manifest

**Actions**:
1. Determine plugin name (kebab-case, descriptive)
2. Ask the user: "Where should I create the plugin?" via AskUserQuestion:
   - Option 1: "📂 Current directory" (Recommended)
   - Option 2: "📂 Parent directory (../plugin-name)"
   - Option 3: "📂 Custom path"
3. Create directory structure using bash:
   ```bash
   mkdir -p plugin-name/.claude-plugin
   mkdir -p plugin-name/skills     # if needed
   mkdir -p plugin-name/agents     # if needed
   mkdir -p plugin-name/hooks      # if needed
   ```
4. Create plugin.json manifest using [plugin.json template](templates/plugin.json)
5. Create README.md using [README.md template](templates/README.md)
6. Create .gitignore if needed (for .claude/*.local.md, etc.)
7. Initialize git repo if creating new directory

**Output**: Plugin directory structure created and ready for components

---

## Phase 5: Component Implementation

**Goal**: Create each component following best practices

**LOAD RELEVANT SKILLS** before implementing each component type:
- Skills (both auto-loaded and user-invoked): Load skill-development skill
- Agents: Load agent-development skill
- Hooks: Load hook-development skill
- MCP: Load mcp-integration skill
- Settings: Load plugin-settings skill

**Setup**:
- For simple plugins (1–2 component types): Implement sequentially, loading skills and delegating to specialized agents as needed.
- For complex plugins (3+ component types): Create agent team with TeamCreate to parallelize work across component types.

**For agent team workflows**:
1. Use TeamCreate to create a team named after the plugin (e.g., `team_name: "database-migrations"`)
2. Create one task per component type using TaskCreate
3. Spawn parallel teammates using Task tool with appropriate subagent_types
4. Teammates coordinate via shared task list (TaskList)
5. Use SendMessage to provide feedback or unblock teammates

**Actions for each component**:

### For Skills:
1. Load skill-development skill using Skill tool
2. For each skill:
   - Ask user for concrete usage examples (or use from Phase 3)
   - Plan resources (scripts/, references/, examples/)
   - Create skill directory structure
   - Write SKILL.md with:
     - Third-person description with specific trigger phrases
     - Lean body (1,500–2,000 words) in imperative form
     - References to supporting files
   - Create reference files for detailed content with progressive disclosure
   - Create example files for working code
   - Create utility scripts if needed
3. Delegate validation: Use Task tool with `subagent_type: plugin-dev:skill-reviewer` to validate each skill

### For User-Invoked Skills (slash commands):
Custom slash commands have been merged into skills. Create these as skills with
`disable-model-invocation: true` in frontmatter so only the user can invoke them.
Use this for workflows with side effects or that the user wants to control timing
(e.g., /deploy, /commit, /send-slack-message).

1. Load skill-development skill using Skill tool
2. For each user-invoked skill:
   - Create skill directory under `skills/`
   - Write SKILL.md with frontmatter including `disable-model-invocation: true` and `argument-hint`
   - Write instructions FOR Claude (not TO user)
   - Reference supporting files in references/ if needed

### For Agents:
1. Load agent-development skill using Skill tool
2. For each agent, delegate to agent-creator:
   - Use Task tool with `subagent_type: plugin-dev:agent-creator`
   - Provide description of what agent should do
   - Agent-creator generates: identifier, whenToUse with examples, systemPrompt
   - Create agent markdown file with frontmatter and system prompt
   - Add appropriate model, color, and tools
   - Validate with validate-agent.sh script

### For Hooks:
1. Load hook-development skill using Skill tool
2. For each hook:
   - Create hooks/hooks.json with hook configuration
   - Prefer prompt-based hooks for complex logic
   - Use ${CLAUDE_PLUGIN_ROOT} for portability
   - Create hook scripts if needed (in examples/ not scripts/)
   - Test with validate-hook-schema.sh and test-hook.sh utilities

### For MCP:
1. Load mcp-integration skill using Skill tool
2. Create .mcp.json configuration with:
   - Server type (stdio for local, SSE for hosted)
   - Command and args (with ${CLAUDE_PLUGIN_ROOT})
   - extensionToLanguage mapping if LSP
   - Environment variables as needed
3. Document required env vars in README
4. Provide setup instructions

### For Settings:
1. Load plugin-settings skill using Skill tool
2. Create settings template in README
3. Create example .claude/plugin-name.local.md file (as documentation)
4. Implement settings reading in hooks/commands as needed
5. Add to .gitignore: `.claude/*.local.md`

**Progress tracking**: Update task status with TaskUpdate as each component is completed. Mark as `in_progress` when starting, `completed` when done.

**Output**: All plugin components implemented

---

## Phase 6: Validation & Quality Check

**Goal**: Ensure plugin meets quality standards and works correctly

**Actions**:
1. **Run plugin-validator agent**:
   - Use plugin-validator agent to comprehensively validate plugin
   - Check: manifest, structure, naming, components, security
   - Review validation report

2. **Fix critical issues**:
   - Address any critical errors from validation
   - Fix any warnings that indicate real problems

3. **Review with skill-reviewer** (if plugin has skills):
   - For each skill, use skill-reviewer agent
   - Check description quality, progressive disclosure, writing style
   - Apply recommendations

4. **Test agent triggering** (if plugin has agents):
   - For each agent, verify <example> blocks are clear
   - Check triggering conditions are specific
   - Run validate-agent.sh on agent files

5. **Test hook configuration** (if plugin has hooks):
   - Run validate-hook-schema.sh on hooks/hooks.json
   - Test hook scripts with test-hook.sh
   - Verify ${CLAUDE_PLUGIN_ROOT} usage

6. **Present findings** using [validation-report.md template](templates/validation-report.md):
   - Summary of validation results
   - Any remaining issues
   - Overall quality assessment

7. Ask the user: "Validation complete. Would you like me to fix the issues now, or proceed to testing?" via AskUserQuestion:
   - Option 1: "🔧 Fix issues now" (Recommended if critical issues exist)
   - Option 2: "🔍 Review details first"
   - Option 3: "⏭️ Proceed to testing anyway"

**Output**: Plugin validated and ready for testing

---

## Phase 7: Testing & Verification

**Goal**: Test that plugin works correctly in Claude Code

**Actions**:
1. **Installation instructions**:
   - Show user how to test locally:
     ```bash
     cc --plugin-dir /path/to/plugin-name
     ```
   - Or copy to `.claude-plugin/` for project testing

2. **Verification checklist** using [verification-checklist.md template](templates/verification-checklist.md):
   - [ ] Auto-loaded skills activate when triggered (ask questions with trigger phrases)
   - [ ] User-invoked skills appear as `/slash-commands` and execute correctly
   - [ ] Agents trigger on appropriate scenarios
   - [ ] Hooks activate on events (if applicable)
   - [ ] MCP servers connect (if applicable)
   - [ ] Settings files work (if applicable)

3. **Testing recommendations**:
   - For auto-loaded skills: Ask questions using trigger phrases from descriptions
   - For user-invoked skills: Run `/plugin-name:skill-name` with various arguments
   - For agents: Create scenarios matching agent examples
   - For hooks: Use `claude --debug` to see hook execution
   - For MCP: Use `/mcp` to verify servers and tools

4. Ask the user: "I've prepared the plugin for testing. Would you like me to guide you through testing each component?" via AskUserQuestion:
   - Option 1: "🧭 Guide me through testing" (Recommended for first-time plugins)
   - Option 2: "🛠️ I'll test it myself"
   - Option 3: "📋 Show me the checklist only"

5. **If user wants guidance**, walk through testing each component with specific test cases

**Output**: Plugin tested and verified working

---

## Phase 8: Documentation & Next Steps

**Goal**: Ensure plugin is well-documented and ready for distribution

**Actions**:
1. **Verify README completeness**:
   - Check README has: overview, features, installation, prerequisites, usage
   - For MCP plugins: Document required environment variables
   - For hook plugins: Explain hook activation
   - For settings: Provide configuration templates

2. **Add marketplace entry** (if publishing):
   - Show user how to add to marketplace.json
   - Help draft marketplace description
   - Suggest category and tags

3. **Create summary** using [plugin-summary.md template](templates/plugin-summary.md):
   - Mark all tasks complete with TaskUpdate (set status: "completed")
   - List what was created:
     - Plugin name and purpose
     - Components created (X skills, Y commands, Z agents, etc.)
     - Key files and their purposes
     - Total file count and structure
   - Next steps:
     - Testing recommendations
     - Publishing to marketplace (if desired)
     - Iteration based on usage

4. **Suggest improvements** (optional):
   - Additional components that could enhance plugin
   - Integration opportunities
   - Testing strategies

**Output**: Complete, documented plugin ready for use or publication

---

## Important Notes

### Throughout All Phases

- **Track progress with tasks**: Use TaskCreate at phase start, TaskUpdate to mark progress, TaskList to check status.
- **Use AskUserQuestion** at key decision points with concrete options and recommendations.
- **Load skills with Skill tool** when working on specific component types.
- **Delegate to specialized agents** via Task tool with appropriate subagent_type (agent-creator, plugin-validator, skill-reviewer).
- **Use agent teams** (TeamCreate) for complex plugins with 3+ component types to parallelize work.
- **Follow plugin-dev's own patterns** as reference examples.
- **Apply best practices**:
  - Third-person descriptions for skills
  - Imperative form in skill bodies
  - User-invoked skills use `disable-model-invocation: true`
  - Strong trigger phrases
  - ${CLAUDE_PLUGIN_ROOT} for portability
  - Progressive disclosure
  - Security-first (HTTPS, no hardcoded credentials)

### Key Decision Points (Wait for User)

1. After Phase 1: Confirm plugin purpose
2. After Phase 2: Approve component plan
3. After Phase 3: Proceed to implementation
4. After Phase 6: Fix issues or proceed
5. After Phase 7: Continue to documentation

### Skills to Load by Phase

- **Phase 2**: plugin-structure
- **Phase 5**: skill-development, agent-development, hook-development, mcp-integration, plugin-settings (as needed)
- **Phase 6**: (agents will use skills automatically)

### Quality Standards

Every component must meet these standards:
- ✅ Follows plugin-dev's proven patterns
- ✅ Uses correct naming conventions
- ✅ Has strong trigger conditions (skills/agents)
- ✅ Includes working examples
- ✅ Properly documented
- ✅ Validated with utilities
- ✅ Tested in Claude Code

---

## Example Workflow

### User Request
"Create a plugin for managing database migrations"

### Phase 1: Discovery
- Understand: Migration management, database schema versioning
- Confirm: User wants to create, run, rollback migrations

### Phase 2: Component Planning
- Skills (auto): 1 (migration best practices)
- Skills (user): 3 (/create-migration, /run-migrations, /rollback)
- Agents: 1 (migration-validator)
- MCP: 1 (database connection)

### Phase 3: Clarifying Questions
- Which databases? (PostgreSQL, MySQL, etc.)
- Migration file format? (SQL, code-based?)
- Should agent validate before applying?
- What MCP tools needed? (query, execute, schema)

### Phase 4–8: Implementation, Validation, Testing, Documentation

---

**Begin with Phase 1: Discovery**
