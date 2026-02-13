# Documentation & Workflow Improvements

## Analysis Date: 2026-02-13

After analyzing the current workflow guide and system capabilities, here are recommended improvements organized by impact and effort.

---

## ✅ RECENTLY IMPLEMENTED (2026-02-13)

### Best Practices Alignment — Phase 7

**Implemented Features:**

1. **CLAUDE.md slimmed from 159 to ~60 lines** — Extracted 6 sections to `@` imports:
   - `.claude/docs/directory-structure.md`
   - `.claude/docs/coordination-rules.md`
   - `.claude/docs/coding-standards.md` (merged coding + design standards, fixed 8-section consistency)
   - `.claude/docs/review-workflow.md`
   - `.claude/docs/context-management.md` (merged context management + compaction instructions)
   - Aligns with Anthropic's recommendation of CLAUDE.md under 80 lines

2. **Technical Preferences file** — `.claude/docs/technical-preferences.md`
   - Single source of truth for engine, naming conventions, performance budgets, testing, forbidden patterns
   - Populated by `/setup-engine`, updated as decisions are made
   - All agents can reference project-specific standards

3. **`/gate-check` skill** — Phase gate validation with PASS/CONCERNS/FAIL verdict
   - 5 phase transitions: Concept -> Pre-production -> Production -> Alpha -> Beta -> Release
   - Checks required artifacts + quality standards + blockers
   - Complements `/project-stage-detect` (diagnostic) with prescriptive validation

4. **`/setup-engine` updated** — Now populates technical preferences with engine-appropriate
   naming conventions and suggests performance budgets

5. **Design doc 8-section inconsistency fixed** — CLAUDE.md previously listed 5 sections,
   design-review skill checked for 8. Now consistently 8 everywhere.

**Motivation:** Research into BMAD method, Claude Flow, and Anthropic's official guidance
identified CLAUDE.md bloat, missing phase gates, and lack of centralized technical
preferences as the top gaps vs industry best practices.

---

### Project Stage Detection & Reverse Documentation

**Implemented Features:**

1. **`/project-stage-detect` skill** — Automatically analyzes project state, classifies stage (Concept/Pre-production/Production/Post-Launch), identifies documentation gaps, and recommends next steps
   - Template: `project-stage-report.md`
   - Full gap detection with clarifying questions

2. **`/reverse-document` skill** — Generates design or architecture docs from existing implementation
   - Works backwards from code to create missing GDDs or ADRs
   - Templates: `design-doc-from-implementation.md`, `architecture-doc-from-code.md`, `concept-doc-from-prototype.md`
   - Follows collaborative protocol (asks about intent vs accidents)

3. **`detect-gaps.sh` hook** — Runs on session start to flag missing documentation
   - Detects code without design docs
   - Detects undocumented prototypes
   - Detects core systems without ADRs
   - Suggests `/reverse-document` or `/project-stage-detect`

**Impact:** Addresses two major user workflow needs:

- "Does the system understand what stage I'm in?" → Yes, via `/project-stage-detect`
- "Can I work backwards from existing code?" → Yes, via `/reverse-document`

**Files Added:**

- `.claude/skills/project-stage-detect/SKILL.md`
- `.claude/skills/reverse-document/SKILL.md`
- `.claude/docs/templates/project-stage-report.md`
- `.claude/docs/templates/design-doc-from-implementation.md`
- `.claude/docs/templates/architecture-doc-from-code.md`
- `.claude/docs/templates/concept-doc-from-prototype.md`
- `.claude/hooks/detect-gaps.sh`

---

## 🎯 HIGH IMPACT, LOW EFFORT

### 1. Visual Workflow Diagrams

**Problem:** The workflow guide is text-heavy and hard to scan quickly.

**Solution:** Add Mermaid diagrams for key workflows.

**Example to Add:**

```markdown
### Sprint Workflow (Visual)

```mermaid
graph TD
    A[Sprint Start] --> B[/sprint-plan new]
    B --> C[Pick Task]
    C --> D{Task Type?}
    D -->|Feature| E[Design → Implement → Test]
    D -->|Bug| F[Investigate → Fix → Verify]
    E --> G[/code-review]
    F --> G
    G --> H{Review Pass?}
    H -->|No| C
    H -->|Yes| I[Commit]
    I --> J{Sprint Done?}
    J -->|No| C
    J -->|Yes| K[/retrospective]
```

**Where to Add:**
- Agent selection flowchart (Appendix A)
- Phase transition diagram (shows all 10 phases visually)
- Team skill orchestration diagram
- Context management decision tree

---

### 2. Quick-Start Cheat Sheet

**Problem:** WORKFLOW-GUIDE.md is 1,198 lines. New users are overwhelmed.

**Solution:** Create `docs/QUICK-START-CHEAT-SHEET.md` - a single-page reference.

**Contents:**
```markdown
# Quick-Start Cheat Sheet

## Your First 10 Minutes
1. `/setup-engine godot 4.6` ← Lock your engine
2. `/brainstorm` ← Generate game ideas
3. Create `design/gdd/core-loop.md` ← Document your core mechanic
4. `/design-review` ← Validate design
5. `/sprint-plan new` ← Start first sprint

## Agent Selection (30-Second Rule)
- **Design a mechanic?** → `game-designer`
- **Write code?** → `gameplay-programmer`
- **Review code?** → `lead-programmer`
- **Fix a bug?** → `debugger`
- **Plan work?** → `producer`
- **Make creative decision?** → `creative-director`
- **Make technical decision?** → `technical-director`

## Essential Slash Commands
| Command | When to Use |
|---------|-------------|
| `/design-review` | Before any code is written |
| `/code-review` | After any major code change |
| `/sprint-plan` | Start of each sprint |
| `/team-*` | Multi-agent features (combat, UI, narrative) |
| `/balance-check` | After changing game data |

## Emergency Commands
- **Production bug?** → `/hotfix "description"`
- **Scope exploding?** → `/scope-check`
- **Context full?** → `/clear` (or compact at ~70%)
```

---

### 3. Troubleshooting Guide

**Problem:** Users don't know what to do when things go wrong.

**Solution:** Add `docs/TROUBLESHOOTING.md`.

**Contents:**

```markdown
# Troubleshooting Guide

## Hooks Not Working

**Symptom:** No session-start banner when opening Claude Code

**Diagnosis:**
1. Check `.claude/settings.json` exists
2. Verify hook paths use forward slashes even on Windows
3. Test hook manually: `bash .claude/hooks/session-start.sh`

**Fix:** If `jq` missing, hooks fall back to `grep` automatically. No action needed.

---

## Agent Gives Wrong Engine Advice

**Symptom:** Agent suggests Unity API when you're using Godot

**Root Cause:** CLAUDE.md not properly configured OR wrong agent used

**Fix:**
1. Check `CLAUDE.md` Technology Stack section has engine specified
2. Use engine-specific agent (`godot-specialist`, not generic `gameplay-programmer`)
3. Pin engine version with `/setup-engine godot 4.6`

---

## Context Window Full

**Symptom:** Claude says "approaching context limit"

**Prevention (Best):**
- Compact proactively at ~65-70% usage
- Use `/clear` between unrelated tasks
- Delegate to subagents for deep research

**When It Happens:**
1. The `pre-compact.sh` hook saves your session state automatically
2. Click "Compact" when prompted
3. After compaction, verify: modified files, active tasks, decisions made

**Recovery:**
- Read `~/.claude/sessions/[session-id]/pre-compact-notes.md`
- This file has your session summary before compaction

---

## Design Review Keeps Failing

**Symptom:** `/design-review` always reports missing sections

**Cause:** GDD doesn't match 8-section template

**Fix:**
1. Read `.claude/docs/templates/game-design-document.md`
2. Ensure ALL 8 sections exist:
   - Overview, Player Fantasy, Detailed Rules, Formulas,
     Edge Cases, Dependencies, Tuning Knobs, Acceptance Criteria
3. Edge cases cannot say "TBD" - must be explicitly resolved

---

## Commit Hook Blocks My Commit

**Symptom:** `validate-commit.sh` hook fails with error

**Common Causes:**

| Error | Meaning | Fix |
|-------|---------|-----|
| "No design doc reference" | Commit message doesn't link to a GDD | Add `per design/gdd/[file].md` to message |
| "Invalid JSON" | Data file in `assets/data/` is malformed | Run `python -m json.tool [file]` to find syntax error |
| "Hardcoded value detected" | Code has magic numbers | Move to `assets/data/` config file |
| "TODO format invalid" | TODO comment doesn't match standard | Use format: `# TODO(system): description` |

**Override (Emergency Only):**
```bash
git commit --no-verify -m "message"
# WARNING: Only use when hook is WRONG, not when YOU are wrong
```

---

## Tests Failing After Refactor

**Symptom:** Test suite broke after code change

**Diagnosis Steps:**
1. `pytest -v` ← See which specific tests fail
2. Read test failure messages carefully
3. Check if test expectations need updating (if behavior intentionally changed)
4. Check if refactor broke dependency injection (common issue)

**Prevent:**
- Run tests BEFORE committing: `pytest tests/`
- Use `/code-review` before major refactors
- Keep tests independent (no shared state)
```

---

### 4. Agent Selection Flowchart

**Problem:** Users don't know which of 48 agents to use.

**Solution:** Add to Appendix A of WORKFLOW-GUIDE.md:

```markdown
## Agent Selection Flowchart

```mermaid
graph TD
    Start{What are you doing?}
    Start -->|Creating/designing| Design
    Start -->|Coding| Code
    Start -->|Testing/fixing| Test
    Start -->|Planning/managing| Plan
    Start -->|Deciding| Decide

    Design{What kind of design?}
    Design -->|Game mechanic| A1[game-designer]
    Design -->|Formulas/balance| A2[systems-designer]
    Design -->|Level layout| A3[level-designer]
    Design -->|Story/lore| A4[narrative-director]
    Design -->|World/factions| A5[world-builder]

    Code{What system?}
    Code -->|Gameplay| B1[gameplay-programmer]
    Code -->|AI| B2[ai-programmer]
    Code -->|UI| B3[ui-programmer]
    Code -->|Multiplayer| B4[network-programmer]
    Code -->|Shaders/VFX| B5[technical-artist]
    Code -->|Engine-specific| B6{Which engine?}
    B6 -->|Godot| B6a[godot-specialist]
    B6 -->|Unity| B6b[unity-specialist]
    B6 -->|Unreal| B6c[unreal-specialist]

    Test{What kind of test?}
    Test -->|Write tests| C1[qa-tester]
    Test -->|Debug/fix| C2[debugger]
    Test -->|Performance| C3[performance-analyst]
    Test -->|Balance| C4[/balance-check]

    Plan{What scope?}
    Plan -->|Sprint| D1[producer]
    Plan -->|Feature estimate| D2[/estimate]
    Plan -->|Milestone| D3[/milestone-review]

    Decide{What domain?}
    Decide -->|Creative/design| E1[creative-director]
    Decide -->|Technical/arch| E2[technical-director]
    Decide -->|Scope/priority| E3[producer]
```

---

## 🎯 HIGH IMPACT, MEDIUM EFFORT

### 5. Context Management Deep-Dive

**Problem:** Context management is mentioned but not deeply explained.

**Solution:** Add `docs/CONTEXT-MANAGEMENT-GUIDE.md`.

**Contents:**

```markdown
# Context Management Guide

Context is your most precious resource. This guide shows how to manage it effectively.

## Understanding Context Budgets

| Session Type | Startup Cost | Per-Agent Cost | Recommended Max Turns |
|--------------|--------------|----------------|---------------------|
| Simple edit | ~2k tokens | N/A | Unlimited |
| Feature implementation | ~8k tokens | +2k per agent | 15-20 turns |
| Multi-system refactor | ~15k tokens | +2k per agent | 10-15 turns |
| Research/exploration | ~5k tokens | +3k per agent (w/ search) | 8-10 turns |

**Startup cost includes:**
- CLAUDE.md (~4k)
- Imported docs (~3k)
- Memory files (~1k)

## When to Compact

**Proactive compaction signals:**
- You've completed 3+ unrelated tasks
- You're at ~65-70% context usage
- You're about to start a new major feature
- You've spawned 3+ subagents

**Reactive compaction signals (too late):**
- Claude says "approaching context limit"
- You're at 85%+ usage
- Responses getting slower

## What Gets Preserved in Compaction

The `pre-compact.sh` hook automatically saves:
```bash
# Saved to ~/.claude/sessions/[id]/pre-compact-notes.md
- Modified files list
- Active TODO list
- Sprint tasks status
- Architectural decisions made
- Agent invocations and outcomes
- Test results
- Open blockers
```

## Context-Saving Strategies

### 1. Use Subagents for Deep Research

**Bad (fills context):**
```
User: "Find all uses of 'damage_multiplier' in the codebase"
Claude: [searches 20 files, reads 15 files, shows 200 lines of results]
Context cost: +8k tokens
```

**Good (delegates):**
```
User: "Use the Explore agent to find all uses of 'damage_multiplier'"
Claude: [spawns subagent]
Subagent: [does research in its own context]
Subagent result: "Found in 5 files, here's the summary"
Context cost: +1k tokens (just the summary)
```

### 2. Clear Between Unrelated Tasks

**Scenario:** You finish implementing combat, now want to work on UI.

```
User: "/clear"  ← Wipes context but keeps files
User: "Implement the main menu UI per design/gdd/ui-main-menu.md"
```

You don't need combat context for UI work. Start fresh.

### 3. Use File References Instead of Reading

**Bad:**
```
User: "What does the combat system do?"
Claude: [reads 8 combat files, 400 lines]
```

**Good:**
```
User: "Read design/gdd/combat-system.md and summarize"
Claude: [reads 1 file, 50 lines]
```

The design doc is the source of truth. Code is implementation details.

### 4. Batch Related Work

**Bad:**
```
[Implement feature A]
[Compact]
[Implement feature B]  ← Feature B needs Feature A context!
```

**Good:**
```
[Implement feature A]
[Implement feature B]  ← Related, keep context
[Compact now]
```

## Emergency: Context Limit Reached

If you hit the limit mid-task:

1. **Save state manually:**
   ```
   User: "Summarize what we've done this session and what's left to do"
   Claude: [provides summary]
   User: [copy summary to notes.txt]
   ```

2. **Compact or restart:**
   - Compact if you need partial context (files modified, decisions made)
   - `/clear` if starting completely new work

3. **Resume:**
   ```
   User: [paste summary from notes.txt]
   User: "Continue where we left off"
   ```

## Measuring Context Usage

You can't see exact percentage, but estimate:

**Low usage signals:**
- Responses instant (<1 sec)
- No warnings
- <10 tool calls this session

**Medium usage signals:**
- Responses fast (1-2 sec)
- 10-20 tool calls
- 2-3 subagents spawned

**High usage signals:**
- Responses slower (3-5 sec)
- 20+ tool calls
- 4+ subagents OR deep file searches

**Critical signals:**
- Claude warns about context
- Responses >5 seconds
- 30+ tool calls

## Best Practices

1. **One major feature per session** (unless tightly related)
2. **Compact at natural boundaries** (end of sprint, after milestone review)
3. **Use `/clear` liberally** (it's fast to reload)
4. **Let subagents handle breadth** (searching many files)
5. **Keep design docs concise** (they're loaded into context)
6. **Archive old ADRs** (move to `docs/architecture/archive/` after 6 months)
```

---

### 6. Real-World Example Session Logs

**Problem:** Guide shows commands but not full realistic sessions.

**Solution:** Add `docs/examples/` directory with complete session transcripts.

**File: `docs/examples/session-implementing-inventory.md`:**

```markdown
# Example Session: Implementing Inventory System

**Context:** Solo dev, week 3 of project, implementing inventory per design doc.

**Starting context:** Fresh session, just design doc loaded.

---

## Turn 1: Read Design Doc

**User:**
> Read design/gdd/inventory-system.md and summarize the requirements

**Claude:**
> I've read the inventory design. Key requirements:
> - Grid-based inventory (8x6 = 48 slots)
> - Items have size (1x1 to 3x3)
> - Stackable items (max stack from item data)
> - Weight limit (derived from player stats)
> - Drag-and-drop UI
> - Persistence (save/load)
>
> Formulas section defines:
> - Weight capacity = base_capacity + (strength * 2.5)
> - Stack limit per item type in assets/data/items.json

---

## Turn 2: Architecture Decision

**User:**
> What's the best way to implement the grid system? I'm using Godot 4.6.

**Claude:**
> I'll use the godot-specialist agent to recommend an architecture.
>
> [Spawns godot-specialist subagent]
>
> **godot-specialist recommendation:**
> - Use a `GridContainer` node for visual representation
> - Backend: 2D array `item_grid: Array[Array[ItemSlot]]`
> - Each `ItemSlot` is a custom Resource with properties:
>   - `item: ItemData` (null if empty)
>   - `stack_count: int`
>   - `top_left_of_item: bool` (for multi-slot items)
> - Drag-and-drop: Override `_can_drop_data()` and `_drop_data()` on UI
>
> This follows Godot's node/resource patterns and makes save/load trivial
> (Resources auto-serialize).

---

## Turn 3: Implementation

**User:**
> Implement that architecture in src/gameplay/inventory/

**Claude:**
> [Creates 4 files]
> 1. `src/gameplay/inventory/item_slot.gd` (Resource)
> 2. `src/gameplay/inventory/inventory.gd` (Grid logic)
> 3. `src/ui/inventory/inventory_ui.gd` (UI scene script)
> 4. `assets/data/inventory_config.json` (Constants)
>
> [Shows code]

---

## Turn 4: Validation Against Rules

**Claude (automatic):**
> The `gameplay-code` rule flagged:
> - ❌ Grid size hardcoded (8x6) in inventory.gd line 12
>
> Fixing: Moving to inventory_config.json...
> ✓ Fixed

---

## Turn 5: Create Tests

**User:**
> Write tests for this

**Claude:**
> [Delegates to qa-tester]
> [Creates tests/inventory/test_inventory_grid.gd with 8 test cases covering acceptance criteria]

---

## Turn 6: Review

**User:**
> /code-review src/gameplay/inventory/inventory.gd

**Claude:**
> **Review Result:**
> ✓ Coding standards: PASS
> ✓ Architecture: Follows ADR for resource-based inventory
> ✓ SOLID principles: PASS (Single responsibility clear)
> ✓ Testability: PASS (Dependency injection used)
> ⚠️ Performance concern: `_find_empty_slot()` is O(n²) for 8x6 grid.
>    This is fine for 48 slots, but document the constraint.
>
> **Recommendation:** Add comment documenting grid size limit (max 20x20).

---

## Turn 7: Commit

**User:**
> Commit this with message "Implement grid-based inventory system per design/gdd/inventory-system.md"

**Claude:**
> [Runs git add, git commit]
> `validate-commit.sh` hook:
> ✓ Design doc referenced
> ✓ JSON files valid
> ✓ No hardcoded values
> ✓ TODO format correct
>
> Commit successful: abc1234

---

**Session stats:**
- Turns: 7
- Time: ~15 minutes
- Files created: 4 source + 1 test + 1 config
- Context usage: ~40% (estimated)
- Blockers: 0
- Result: ✓ Working inventory system
```

---

### 7. Timing Estimates for Phases

**Problem:** Users don't know how long each phase takes.

**Solution:** Add realistic timelines to WORKFLOW-GUIDE.md Phase headers.

**Example:**

```markdown
## Phase 1: Ideation & Concept

**Typical Duration:**
- Solo dev: 1-3 days
- Small team (2-4 people): 3-7 days
- Large team (5+ people): 1-2 weeks

**Depends on:**
- How clear your vision is (clearer = faster)
- How many concepts you explore (more = slower)
- How much research you do on comparables

**Output:**
- Game concept document
- Game pillars + anti-pillars
- Core loop diagram
- Audience definition

**When to move on:**
You can explain your game in one sentence AND you have 3-5 non-negotiable
design pillars. If you can't do both, you're not ready for pre-production.
```

---

## 🎯 MEDIUM IMPACT, LOW EFFORT

### 8. Migration Guide for Existing Projects

**Problem:** Users with existing games don't know how to adopt this system.

**Solution:** Add `docs/MIGRATION-GUIDE.md`.

**Contents:**

```markdown
# Migrating Existing Projects to Agent Architecture

## Option 1: Fresh Start (Recommended for Small Projects <10k LOC)

Best if: Your project is small and structure is messy.

1. Create new repo from this template
2. Copy over working code to `src/`
3. Reverse-engineer design docs from code
4. `/setup-engine` to match your current engine
5. `/sprint-plan` to organize remaining work

**Time:** 1-2 days
**Risk:** Low

---

## Option 2: Incremental Adoption (Recommended for Large Projects >10k LOC)

Best if: You have significant working code and can't afford disruption.

### Week 1: Add Infrastructure

1. Copy `.claude/` directory to your project root
2. Copy `CLAUDE.md` and customize Technology Stack section
3. Add `.claude/settings.json`
4. Test hooks: Start a session, check for session-start banner

No code changes yet.

### Week 2: Add Design Docs

1. Create `design/gdd/` directory
2. For each major system, create a GDD using the template
3. Run `/design-review` on each
4. Fix gaps

You now have documentation.

### Week 3: Adopt Rules Gradually

1. Move one system's code to match directory structure (e.g., `src/gameplay/`)
2. The relevant rule (e.g., `gameplay-code.md`) now applies
3. Fix violations flagged by rules
4. Repeat for other systems

Incremental code cleanup.

### Week 4: Start Using Agents

1. Next new feature: Use `/team-*` skills
2. Bug fixes: Use `debugger` agent
3. Code reviews: Use `/code-review`

From this point forward, new work uses the system. Old code migrates gradually.

**Time:** 4+ weeks
**Risk:** Medium (phased rollout reduces risk)
```

---

### 9. Common Pitfalls Section

**Problem:** New users make predictable mistakes.

**Solution:** Add to WORKFLOW-GUIDE.md or QUICK-START-CHEAT-SHEET.md:

```markdown
## 10 Common Pitfalls (and How to Avoid Them)

### 1. Coding Before Designing
❌ **Mistake:** Jump straight to implementation without a GDD
✅ **Fix:** ALWAYS create design/gdd/[system].md first, then /design-review it

### 2. Using the Wrong Agent Tier
❌ **Mistake:** Asking `creative-director` to write shader code
✅ **Fix:** Use the agent hierarchy - specialists for implementation, leads for decisions

### 3. Ignoring Hook Warnings
❌ **Mistake:** Committing despite `validate-commit.sh` errors
✅ **Fix:** Hooks encode best practices - fix the issue, don't bypass with --no-verify

### 4. Hardcoding Balance Values
❌ **Mistake:** `var damage = 15` in code
✅ **Fix:** `var damage = Config.get("combat.base_damage")` from assets/data/

### 5. Letting Context Fill Up
❌ **Mistake:** Working until Claude says "context limit reached"
✅ **Fix:** Compact proactively at ~70%, or /clear between unrelated tasks

### 6. Skipping Tests
❌ **Mistake:** "I'll write tests later"
✅ **Fix:** Use verification-driven development - tests prove it works

### 7. Scope Creep in Sprints
❌ **Mistake:** Adding "just one more small feature" to a sprint
✅ **Fix:** Run `/scope-check` weekly - protect your scope religiously

### 8. Not Using Team Skills for Cross-Cutting Features
❌ **Mistake:** Manually coordinating 5 agents yourself
✅ **Fix:** `/team-combat`, `/team-ui`, etc. handle orchestration

### 9. Treating Prototypes as Production Code
❌ **Mistake:** Shipping prototype code with hardcoded values
✅ **Fix:** Prototypes in `prototypes/` are throwaway - rewrite for production

### 10. Forgetting to Pin Engine Version
❌ **Mistake:** "I'm using Godot" without version
✅ **Fix:** `/setup-engine godot 4.6` - version matters for API correctness
```

---

### 10. Success Metrics

**Problem:** Users don't know if they're using the system effectively.

**Solution:** Add to docs/WORKFLOW-GUIDE.md:

```markdown
## How to Know If You're Using the System Effectively

### Green Flags (You're doing great!)

✅ Every commit references a design doc
✅ Hooks catch issues before you notice them
✅ You `/design-review` before implementing
✅ You `/code-review` after implementing
✅ Sprint scope stays stable (no more than +10% growth)
✅ Tests exist for all gameplay systems
✅ Context never hits limit (you compact proactively)
✅ You use team skills for multi-agent work
✅ Architecture decisions are documented in ADRs
✅ You can onboard a contributor in <1 hour

### Yellow Flags (Needs improvement)

⚠️ Commits sometimes lack design doc references
⚠️ You bypass hooks with `--no-verify` occasionally
⚠️ Context hits 90%+ before compacting
⚠️ Some systems lack tests
⚠️ Sprint scope grows by 20-30%
⚠️ You manually coordinate 3+ agents instead of using team skills
⚠️ Some major decisions aren't in ADRs

### Red Flags (Major issues)

❌ No design docs exist for major systems
❌ Hooks disabled or frequently bypassed
❌ No tests
❌ Sprint scope doubles mid-sprint
❌ Context constantly maxed out
❌ No ADRs for any technical decisions
❌ Balance values hardcoded in gameplay code
❌ You don't know which agent to use for a task

### Metrics to Track

| Metric | Target | How to Measure |
|--------|--------|----------------|
| Design coverage | 100% of systems | Every src/ directory has corresponding design/gdd/ doc |
| Test coverage | 80%+ for gameplay | Run coverage tool |
| Hook compliance | 95%+ | Check git log for `--no-verify` |
| Sprint scope stability | ±15% | /scope-check at sprint end |
| ADR coverage | 1 per major decision | Count docs/architecture/ files |
| Hardcoded values | 0 in gameplay code | grep -r "TODO.*hardcoded" src/ |
