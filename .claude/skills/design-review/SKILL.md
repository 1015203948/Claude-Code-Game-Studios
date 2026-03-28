---
name: design-review
description: "Reviews a game design document for completeness, internal consistency, implementability, and adherence to project design standards. Run this before handing a design document to programmers."
argument-hint: "[path-to-design-doc]"
user-invocable: true
allowed-tools: Read, Glob, Grep
context: fork
# Read-only diagnostic skill — no specialist agent delegation needed
---

## Phase 1: Load Documents

Read the target design document in full. Read CLAUDE.md to understand project context and standards. Read related design documents referenced or implied by the target doc (check `design/gdd/` for related systems).

---

## Phase 2: Completeness Check

Evaluate against the Design Document Standard checklist:

- [ ] Has Overview section (one-paragraph summary)
- [ ] Has Player Fantasy section (intended feeling)
- [ ] Has Detailed Rules section (unambiguous mechanics)
- [ ] Has Formulas section (all math defined with variables)
- [ ] Has Edge Cases section (unusual situations handled)
- [ ] Has Dependencies section (other systems listed)
- [ ] Has Tuning Knobs section (configurable values identified)
- [ ] Has Acceptance Criteria section (testable success conditions)

---

## Phase 3: Consistency and Implementability

**Internal consistency:**
- Do the formulas produce values that match the described behavior?
- Do edge cases contradict the main rules?
- Are dependencies bidirectional (does the other system know about this one)?

**Implementability:**
- Are the rules precise enough for a programmer to implement without guessing?
- Are there any "hand-wave" sections where details are missing?
- Are performance implications considered?

**Cross-system consistency:**
- Does this conflict with any existing mechanic?
- Does this create unintended interactions with other systems?
- Is this consistent with the game's established tone and pillars?

---

## Phase 4: Output Review

```
## Design Review: [Document Title]

### Completeness: [X/8 sections present]
[List missing sections]

### Consistency Issues
[List any internal or cross-system contradictions]

### Implementability Concerns
[List any vague or unimplementable sections]

### Balance Concerns
[List any obvious balance risks]

### Recommendations
[Prioritized list of improvements]

### Verdict: [APPROVED / NEEDS REVISION / MAJOR REVISION NEEDED]
```

This skill is read-only — no files are written.

---

## Phase 5: Next Steps

If the document being reviewed is `game-concept.md` or `game-pillars.md`:
- Check if `design/gdd/systems-index.md` exists. If not, recommend: "Run `/map-systems` to break the concept down into individual systems with dependencies and priorities, then write per-system GDDs."

If the document is an individual system GDD:
- If verdict is APPROVED: suggest updating the system's status to 'Approved' in the systems index.
- If verdict is NEEDS REVISION or MAJOR REVISION NEEDED: suggest updating the status to 'In Review'.

Next skill options:
- APPROVED → `/create-epics` or `/map-systems`
- NEEDS REVISION → revise the doc then re-run `/design-review`
