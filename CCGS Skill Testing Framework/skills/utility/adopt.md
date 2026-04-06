# Skill Test Spec: /adopt

## Skill Summary

`/adopt` performs brownfield onboarding: it reads an existing non-Claude-Code
project's source files, detects the engine and language, and generates a
matching CLAUDE.md stub plus a populated `technical-preferences.md` to bring
the project under the Claude Code Game Studios framework. It may also produce
skeleton GDD files if enough design intent can be inferred from the code.

Each generated file is gated behind a "May I write" ask. If an existing CLAUDE.md
or `technical-preferences.md` is detected, the skill offers to merge rather than
overwrite. The skill has no director gates. Verdicts: COMPLETE (full analysis done
and files written), PARTIAL (analysis complete but some fields are ambiguous),
or BLOCKED (cannot proceed — no source code found or user declined all writes).

---

## Static Assertions (Structural)

Verified automatically by `/skill-test static` — no fixture needed.

- [ ] Has required frontmatter fields: `name`, `description`, `argument-hint`, `user-invocable`, `allowed-tools`
- [ ] Has ≥2 phase headings
- [ ] Contains verdict keywords: COMPLETE, PARTIAL, BLOCKED
- [ ] Contains "May I write" collaborative protocol language before each file creation
- [ ] Has a next-step handoff at the end (e.g., `/setup-engine` to refine or `/brainstorm`)

---

## Director Gate Checks

None. `/adopt` is a brownfield onboarding utility. No director gates apply.

---

## Test Cases

### Case 1: Happy Path — Existing Unity project with C# code detected

**Fixture:**
- `src/` contains `.cs` files with Unity-specific namespaces (`using UnityEngine;`)
- No CLAUDE.md overrides, no `technical-preferences.md` beyond placeholders
- Project has a recognizable folder structure (Assets/, Scripts/)

**Input:** `/adopt`

**Expected behavior:**
1. Skill scans `src/` and detects C# files with Unity API imports
2. Skill identifies engine as Unity, language as C#
3. Skill produces a draft `technical-preferences.md` with engine/language fields populated
4. Skill produces a draft CLAUDE.md stub with detected project structure
5. Skill asks "May I write `technical-preferences.md`?" and then "May I write CLAUDE.md?"
6. Files are written after approval; verdict is COMPLETE

**Assertions:**
- [ ] Engine detected as Unity (not Godot or Unreal)
- [ ] Language detected as C#
- [ ] Draft is shown to user before any "May I write" ask
- [ ] "May I write" is asked separately for each file
- [ ] Verdict is COMPLETE after both files are written

---

### Case 2: Mixed Languages — Partial analysis, asks user to clarify

**Fixture:**
- `src/` contains both `.gd` (GDScript) and `.cs` (C#) files
- Engine cannot be definitively identified from the mix

**Input:** `/adopt`

**Expected behavior:**
1. Skill scans source and detects conflicting language signals
2. Skill reports: "Mixed language signals detected (GDScript + C#) — cannot auto-identify engine"
3. Skill presents the ambiguous findings and asks the user to confirm: Godot with C# or Unity?
4. After user clarifies, skill resumes analysis with confirmed engine
5. Produces a PARTIAL analysis noting fields that required manual clarification

**Assertions:**
- [ ] Skill does NOT guess or silently pick an engine when signals conflict
- [ ] Ambiguous findings are reported to the user explicitly
- [ ] User choice is incorporated into the generated config
- [ ] Verdict is PARTIAL (not COMPLETE) when manual clarification was required

---

### Case 3: CLAUDE.md Already Exists — Offers merge rather than overwrite

**Fixture:**
- `CLAUDE.md` exists with custom content (project name, existing imports)
- `technical-preferences.md` exists with some fields populated

**Input:** `/adopt`

**Expected behavior:**
1. Skill reads existing CLAUDE.md and detects it is already populated
2. Skill reports: "CLAUDE.md already exists — offering to merge, not overwrite"
3. Skill presents a diff of new fields vs. existing content
4. Skill asks "May I merge new fields into CLAUDE.md?" (not "May I write")
5. If user approves: only new or changed fields are added; existing content preserved

**Assertions:**
- [ ] Skill does NOT overwrite existing CLAUDE.md without explicit user approval for a full replace
- [ ] Merge option is offered when the file already exists
- [ ] Diff is shown before the merge ask
- [ ] Existing custom content is preserved in the merged output

---

### Case 4: No Source Code Found — Stops with error

**Fixture:**
- Repository has only documentation files (`.md`) and no source code in `src/`
- No engine-identifiable files anywhere in the repo

**Input:** `/adopt`

**Expected behavior:**
1. Skill scans `src/` and all likely code locations — finds nothing
2. Skill outputs: "No source code detected — cannot perform brownfield analysis"
3. Skill suggests alternatives: run `/start` for a new project, or point to a
   different directory if source is located elsewhere
4. No files are written

**Assertions:**
- [ ] Verdict is BLOCKED
- [ ] Error message explicitly states no source code was found
- [ ] Alternatives (`/start` or directory guidance) are provided
- [ ] No "May I write" prompts appear (nothing to write)

---

### Case 5: Director Gate Check — No gate; adopt is a utility onboarding skill

**Fixture:**
- Existing project with detectable source code

**Input:** `/adopt`

**Expected behavior:**
1. Skill completes full brownfield analysis and produces config files
2. No director agents are spawned at any point
3. No gate IDs (CD-*, TD-*, AD-*, PR-*) appear in output

**Assertions:**
- [ ] No director gate is invoked
- [ ] No gate skip messages appear
- [ ] Skill reaches COMPLETE or PARTIAL without any gate verdict

---

## Protocol Compliance

- [ ] Scans source before generating any config content
- [ ] Shows draft config to user before asking to write
- [ ] Asks "May I write" (or "May I merge") before each file operation
- [ ] Detects existing files and offers merge path rather than silent overwrite
- [ ] Ends with COMPLETE, PARTIAL, or BLOCKED verdict

---

## Coverage Notes

- The Unreal Engine + Blueprint detection case (`.uasset`, `.umap` files)
  follows the same happy path pattern as Case 1 and is not separately tested.
- Multi-directory source layouts (monorepo style) are not tested; the skill
  assumes a conventional single-project structure.
- GDD skeleton generation from inferred design intent is noted as a capability
  but not fixture-tested here — it follows from the PARTIAL analysis pattern.
