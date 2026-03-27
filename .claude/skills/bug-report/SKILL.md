---
name: bug-report
description: "Creates a structured bug report from a description, or analyzes code to identify potential bugs. Ensures every bug report has full reproduction steps, severity assessment, and context."
argument-hint: "[description] | analyze [path-to-file]"
user-invocable: true
allowed-tools: Read, Glob, Grep, Write
---

## Phase 1: Parse Arguments

Determine the mode from the argument:

- No `analyze` keyword → **Description Mode**: generate a structured bug report from the provided description
- `analyze [path]` → **Analyze Mode**: read the target file(s) and identify potential bugs

If no argument is provided, ask the user for a bug description before proceeding.

---

## Phase 2A: Description Mode

1. **Parse the description** for key information: what broke, when, how to reproduce it, and what the expected behavior is.

2. **Search the codebase** for related files using Grep/Glob to add context (affected system, likely files).

3. **Draft the bug report**:

```markdown
# Bug Report

## Summary
**Title**: [Concise, descriptive title]
**ID**: BUG-[NNNN]
**Severity**: [S1-Critical / S2-Major / S3-Minor / S4-Trivial]
**Priority**: [P1-Immediate / P2-Next Sprint / P3-Backlog / P4-Wishlist]
**Status**: Open
**Reported**: [Date]
**Reporter**: [Name]

## Classification
- **Category**: [Gameplay / UI / Audio / Visual / Performance / Crash / Network]
- **System**: [Which game system is affected]
- **Frequency**: [Always / Often (>50%) / Sometimes (10-50%) / Rare (<10%)]
- **Regression**: [Yes/No/Unknown -- was this working before?]

## Environment
- **Build**: [Version or commit hash]
- **Platform**: [OS, hardware if relevant]
- **Scene/Level**: [Where in the game]
- **Game State**: [Relevant state -- inventory, quest progress, etc.]

## Reproduction Steps
**Preconditions**: [Required state before starting]

1. [Exact step 1]
2. [Exact step 2]
3. [Exact step 3]

**Expected Result**: [What should happen]
**Actual Result**: [What actually happens]

## Technical Context
- **Likely affected files**: [List of files based on codebase search]
- **Related systems**: [What other systems might be involved]
- **Possible root cause**: [If identifiable from the description]

## Evidence
- **Logs**: [Relevant log output if available]
- **Visual**: [Description of visual evidence]

## Related Issues
- [Links to related bugs or design documents]

## Notes
[Any additional context or observations]
```

---

## Phase 2B: Analyze Mode

1. **Read the target file(s)** specified in the argument.

2. **Identify potential bugs**: null references, off-by-one errors, race conditions, unhandled edge cases, resource leaks, incorrect state transitions.

3. **For each potential bug**, generate a bug report using the template above, with the likely trigger scenario and recommended fix filled in.

---

## Phase 3: Save Report

Present the completed bug report(s) to the user.

Ask: "May I write this to `production/qa/bugs/BUG-[NNNN].md`?"

If yes, write the file, creating the directory if needed. Verdict: **COMPLETE** — bug report filed.

If no, stop here. Verdict: **BLOCKED** — user declined write.

---

## Phase 4: Next Steps

After saving, suggest:

- Run `/bug-triage` to prioritize this bug alongside existing open bugs.
- If S1 or S2 severity, consider `/hotfix` for an emergency fix workflow.
