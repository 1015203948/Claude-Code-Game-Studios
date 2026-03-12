---
name: code-review
description: "Performs an architectural and quality code review on a specified file or set of files. Checks for coding standard compliance, architectural pattern adherence, SOLID principles, testability, and performance concerns."
argument-hint: "[path-to-file-or-directory]"
user-invocable: true
allowed-tools: Read, Glob, Grep, Bash
context: fork
agent: code-reviewer
---

When this skill is invoked:

1. **Read the target file(s)** in full.

2. **Read the CLAUDE.md** for project coding standards.

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

9. **Output the review** in this format:

```
## Code Review: [File/System Name]

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
