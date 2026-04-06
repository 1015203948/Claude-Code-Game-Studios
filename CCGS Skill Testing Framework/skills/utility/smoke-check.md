# Skill Test Spec: /smoke-check

## Skill Summary

`/smoke-check` runs the critical path smoke test checklist for a build. It reads
the QA plan from `production/qa/` and checks each critical path item against the
acceptance criteria defined in the current sprint's stories. Items that can be
evaluated analytically are assessed; items that require runtime verification or
visual inspection are flagged as NEEDS MANUAL CHECK.

The skill produces no file writes — output is conversational. No director gates
apply. Verdicts: PASS (all critical items verified), FAIL (at least one critical
item fails), or NEEDS MANUAL CHECK (critical items exist that require human verification).

---

## Static Assertions (Structural)

Verified automatically by `/skill-test static` — no fixture needed.

- [ ] Has required frontmatter fields: `name`, `description`, `argument-hint`, `user-invocable`, `allowed-tools`
- [ ] Has ≥2 phase headings
- [ ] Contains verdict keywords: PASS, FAIL, NEEDS MANUAL CHECK
- [ ] Does NOT contain "May I write" language (skill is read-only)
- [ ] Has a next-step handoff (e.g., `/bug-report` on FAIL, `/release-checklist` on PASS)

---

## Director Gate Checks

None. `/smoke-check` is a QA utility skill. No director gates apply.

---

## Test Cases

### Case 1: Happy Path — All critical path items verifiable, PASS

**Fixture:**
- `production/qa/qa-plan-sprint-005.md` exists with 4 critical path items
- All 4 items are logic or integration type (analytically assessable)
- Corresponding story ACs are defined and met per sprint stories

**Input:** `/smoke-check`

**Expected behavior:**
1. Skill reads the QA plan and identifies 4 critical path items
2. Skill evaluates each item against the story's acceptance criteria
3. All 4 items pass
4. Skill outputs a checklist: each item with a PASS marker
5. Verdict is PASS with summary: "4/4 critical path items verified"

**Assertions:**
- [ ] All 4 items appear in the checklist output
- [ ] Each item is marked PASS
- [ ] Verdict is PASS
- [ ] No files are written

---

### Case 2: Failure Path — One critical item fails, FAIL verdict

**Fixture:**
- QA plan has 3 critical path items
- Item 2 ("Player health does not go below 0") fails — story AC indicates
  clamping logic was not implemented

**Input:** `/smoke-check`

**Expected behavior:**
1. Skill evaluates all 3 items
2. Item 1 and Item 3 pass; Item 2 fails
3. Skill outputs checklist with specific failure: "Item 2 FAIL — Health clamping not verified"
4. Verdict is FAIL
5. Skill suggests running `/bug-report` for the failing item

**Assertions:**
- [ ] Verdict is FAIL (not PARTIAL or NEEDS MANUAL CHECK)
- [ ] Failing item is identified by name/description
- [ ] Passing items are also shown (not hidden)
- [ ] `/bug-report` is suggested for the failure

---

### Case 3: Visual Item Cannot Be Auto-Verified — NEEDS MANUAL CHECK

**Fixture:**
- QA plan has 3 items: 2 logic items (PASS) and 1 visual item
  ("Explosion VFX triggers correctly on enemy death" — ADVISORY, visual type)

**Input:** `/smoke-check`

**Expected behavior:**
1. Skill evaluates the 2 logic items — both pass
2. Skill evaluates the visual item — cannot be verified analytically
3. Visual item is marked NEEDS MANUAL CHECK with a note: "Visual quality requires
   human verification — see production/qa/evidence/"
4. Verdict is NEEDS MANUAL CHECK (not PASS, because human action is required)
5. Guidance on how to perform manual check is provided

**Assertions:**
- [ ] Verdict is NEEDS MANUAL CHECK (not PASS or FAIL)
- [ ] Visual item is marked with explicit NEEDS MANUAL CHECK tag
- [ ] Guidance for manual verification process is included
- [ ] Logic items are still shown as PASS

---

### Case 4: No Smoke Test Plan — Guidance to run /qa-plan

**Fixture:**
- `production/qa/` directory exists but contains no QA plan file for the
  current sprint
- Current sprint is sprint-006

**Input:** `/smoke-check`

**Expected behavior:**
1. Skill looks for QA plan for the current sprint — not found
2. Skill outputs: "No smoke test plan found for sprint-006"
3. Skill suggests running `/qa-plan sprint-006` first
4. No checklist is produced

**Assertions:**
- [ ] Error message names the missing sprint's plan
- [ ] `/qa-plan` is suggested with the correct sprint argument
- [ ] Skill does not produce a checklist with no plan
- [ ] Verdict is not PASS (error state, no checklist evaluated)

---

### Case 5: Director Gate Check — No gate; smoke-check is a QA utility

**Fixture:**
- Valid QA plan with assessable items

**Input:** `/smoke-check`

**Expected behavior:**
1. Skill runs the smoke check and produces a verdict
2. No director agents are spawned
3. No gate IDs appear in output

**Assertions:**
- [ ] No director gate is invoked
- [ ] No write tool is called
- [ ] Verdict is PASS, FAIL, or NEEDS MANUAL CHECK — no gate verdict involved

---

## Protocol Compliance

- [ ] Reads QA plan before evaluating any items
- [ ] Evaluates each item explicitly (no silent skips)
- [ ] Visual/feel items are always flagged NEEDS MANUAL CHECK (not auto-passed)
- [ ] FAIL verdict triggers on first critical failure (not advisory)
- [ ] Verdict is PASS, FAIL, or NEEDS MANUAL CHECK — no other verdicts

---

## Coverage Notes

- The case where the QA plan exists but has no critical path items (all items
  are ADVISORY) is not tested; PASS would be returned with a note that no
  critical items were checked.
- The distinction between BLOCKING and ADVISORY gate levels from coding-standards.md
  is relied upon to determine which items can produce a FAIL.
- Build-specific failures (runtime crashes) that occur during manual testing are
  outside the scope of this skill — use `/bug-report` for those.
