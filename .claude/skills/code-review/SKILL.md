---
name: code-review
description: "Performs an architectural and quality code review on a specified file or set of files. Checks for coding standard compliance, architectural pattern adherence, SOLID principles, testability, and performance concerns."
argument-hint: "[path-to-file-or-directory]"
user-invocable: true
allowed-tools: Read, Glob, Grep, Bash, Task
context: fork
agent: lead-programmer
---

When this skill is invoked:

1. **Read the target file(s)** in full.

2. **Read the CLAUDE.md** for project coding standards.

2.5. **Identify the active engine specialists** by reading `.claude/docs/technical-preferences.md`, section `## Engine Specialists`. Note:
   - The **Primary** specialist (used for architecture and broad engine concerns)
   - The **Language/Code Specialist** (used when reviewing the project's primary language files)
   - The **Shader Specialist** (used when reviewing shader files)
   - The **UI Specialist** (used when reviewing UI code)
   - If the section reads `[TO BE CONFIGURED]`, no engine is pinned — skip engine specialist steps below.

3. **ADR Compliance Check**:

   a. Search for ADR references in: the story file associated with this work (if
      provided), any commit message context, and header comments in the files being
      reviewed. Look for patterns like `ADR-NNN`, `ADR-[name]`, or
      `docs/architecture/ADR-`.

   b. If no ADR references are found, note:
      > "No ADR references found — skipping ADR compliance check."
      Then proceed to step 4.

   c. For each referenced ADR: read `docs/architecture/ADR-NNN-*.md` and extract
      the **Decision** and **Consequences** sections.

   d. Check the implementation against each ADR:
      - What pattern/approach was chosen in the Decision?
      - Are there alternatives explicitly rejected in the ADR?
      - Are there required guardrails or constraints in the Consequences?

   e. Classify any deviation found:
      - **ARCHITECTURAL VIOLATION** (BLOCKING): Implementation uses a pattern
        explicitly rejected in the ADR (e.g., ADR rejected singletons for game
        state, but the code uses a singleton).
      - **ADR DRIFT** (WARNING): Implementation diverges meaningfully from the
        chosen approach without using an explicitly forbidden pattern (e.g., ADR
        chose event-based communication but code uses direct method calls).
      - **MINOR DEVIATION** (INFO): Small difference from ADR guidance that does
        not affect the overall architecture (e.g., slightly different naming from
        the ADR's example code).

   f. Include ADR compliance findings in the review output under
      `### ADR Compliance` before the Standards Compliance section.

4. **Identify the system category** (engine, gameplay, AI, networking, UI, tools)
   and apply category-specific standards.

5. **Evaluate against coding standards**:
   - [ ] Public methods and classes have doc comments
   - [ ] Cyclomatic complexity under 10 per method
   - [ ] No method exceeds 40 lines (excluding data declarations)
   - [ ] Dependencies are injected (no static singletons for game state)
   - [ ] Configuration values loaded from data files
   - [ ] Systems expose interfaces (not concrete class dependencies)

6. **Check architectural compliance**:
   - [ ] Correct dependency direction (engine <- gameplay, not reverse)
   - [ ] No circular dependencies between modules
   - [ ] Proper layer separation (UI does not own game state)
   - [ ] Events/signals used for cross-system communication
   - [ ] Consistent with established patterns in the codebase

7. **Check SOLID compliance**:
   - [ ] Single Responsibility: Each class has one reason to change
   - [ ] Open/Closed: Extendable without modification
   - [ ] Liskov Substitution: Subtypes substitutable for base types
   - [ ] Interface Segregation: No fat interfaces
   - [ ] Dependency Inversion: Depends on abstractions, not concretions

8. **Check for common game development issues**:
   - [ ] Frame-rate independence (delta time usage)
   - [ ] No allocations in hot paths (update loops)
   - [ ] Proper null/empty state handling
   - [ ] Thread safety where required
   - [ ] Resource cleanup (no leaks)

9. **Engine Specialist Review** — If an engine is configured (step 2.5), spawn engine specialists via Task in parallel with your own review above:
   - Determine which specialist applies to each file being reviewed:
     - Primary language files (`.gd`, `.cs`, `.cpp`) → Language/Code Specialist
     - Shader files (`.gdshader`, `.hlsl`, shader graph) → Shader Specialist
     - UI screen/widget code → UI Specialist
     - Cross-cutting or unclear → Primary Specialist
   - Spawn the relevant specialist(s) with: the file(s), the engine reference docs path (`docs/engine-reference/[engine]/`), and the task: "Review for engine-idiomatic patterns, deprecated or incorrect API usage, engine-specific performance concerns, and any patterns the engine's documentation recommends against."
   - Also spawn the **Primary Specialist** for any file that touches engine architecture (scene structure, node hierarchy, component design, lifecycle hooks).
   - Collect findings and include them in the review output under `### Engine Specialist Findings` (placed between `### Game-Specific Concerns` and `### Positive Observations`).
   - If no engine is configured, omit the `### Engine Specialist Findings` section.

10. **Output the review** in this format:

```
## Code Review: [File/System Name]

### Engine Specialist Findings: [N/A — no engine configured / CLEAN / ISSUES FOUND]
[Findings from engine specialist(s), or "No engine configured." if skipped]

### ADR Compliance: [NO ADRS FOUND / COMPLIANT / DRIFT / VIOLATION]
[List each ADR checked, result, and any deviations with severity]

### Standards Compliance: [X/6 passing]
[List failures with line references]

### Architecture: [CLEAN / MINOR ISSUES / VIOLATIONS FOUND]
[List specific architectural concerns]

### SOLID: [COMPLIANT / ISSUES FOUND]
[List specific violations]

### Game-Specific Concerns
[List game development specific issues]

### Positive Observations
[What is done well -- always include this section]

### Required Changes
[Must-fix items before approval — ARCHITECTURAL VIOLATIONs always appear here]

### Suggestions
[Nice-to-have improvements]

### Verdict: [APPROVED / APPROVED WITH SUGGESTIONS / CHANGES REQUIRED]
```
