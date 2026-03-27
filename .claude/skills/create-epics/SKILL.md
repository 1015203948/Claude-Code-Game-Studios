---
name: create-epics
description: "Translate approved GDDs + architecture into epics — one epic per architectural module. Defines scope, governing ADRs, engine risk, and untraced requirements. Does NOT break into stories — run /create-stories [epic-slug] after each epic is created."
argument-hint: "[system-name | layer: foundation|core|feature|presentation | all]"
user-invocable: true
allowed-tools: Read, Glob, Grep, Write
context: fork
agent: technical-director
---

# Create Epics

An epic is a named, bounded body of work that maps to one architectural module.
It defines **what** needs to be built and **who owns it architecturally**. It
does not prescribe implementation steps — that is the job of stories.

**Run this skill once per layer** as you approach that layer in development.
Do not create Feature layer epics until Core is nearly complete — the design
will have changed.

**Output:** `production/epics/[epic-slug]/EPIC.md` + `production/epics/index.md`

**Next step after each epic:** `/create-stories [epic-slug]`

**When to run:** After `/create-control-manifest` and `/architecture-review` pass.

---

## 1. Parse Arguments

**Modes:**
- `/create-epics all` — process all systems in layer order
- `/create-epics layer: foundation` — Foundation layer only
- `/create-epics layer: core` — Core layer only
- `/create-epics layer: feature` — Feature layer only
- `/create-epics layer: presentation` — Presentation layer only
- `/create-epics [system-name]` — one specific system
- No argument — ask: "Which layer or system would you like to create epics for?"

---

## 2. Load Inputs

### Step 2a — Summary scan (fast)

Grep all GDDs for their `## Summary` sections before reading anything fully:

```
Grep pattern="## Summary" glob="design/gdd/*.md" output_mode="content" -A 5
```

For `layer:` or `[system-name]` modes: filter to only in-scope GDDs based on
the Summary quick-reference. Skip full-reading anything out of scope.

### Step 2b — Full document load

Read for in-scope systems:

- `design/gdd/systems-index.md` — authoritative system list, layers, priority
- In-scope GDDs (Approved or Designed status)
- `docs/architecture/architecture.md` — module ownership and API boundaries
- All Accepted ADRs — read the "GDD Requirements Addressed", "Decision", and "Engine Compatibility" sections
- `docs/architecture/control-manifest.md` — manifest version date from header
- `docs/architecture/tr-registry.yaml` — for tracing requirements to ADR coverage
- `docs/engine-reference/[engine]/VERSION.md` — engine name, version, risk levels

Report: "Loaded [N] GDDs, [M] ADRs, engine: [name + version]."

---

## 3. Processing Order

Process in dependency-safe layer order:
1. **Foundation** (no dependencies)
2. **Core** (depends on Foundation)
3. **Feature** (depends on Core)
4. **Presentation** (depends on Feature + Core)

Within each layer, use the order from `systems-index.md`.

---

## 4. Define Each Epic

For each system, map it to an architectural module from `architecture.md`.

Check ADR coverage against the TR registry:
- **Traced requirements**: TR-IDs that have an Accepted ADR covering them
- **Untraced requirements**: TR-IDs with no ADR — warn before proceeding

Present to user before writing anything:

```
## Epic: [System Name]

**Layer**: [Foundation / Core / Feature / Presentation]
**GDD**: design/gdd/[filename].md
**Architecture Module**: [module name from architecture.md]
**Governing ADRs**: [ADR-NNNN, ADR-MMMM]
**Engine Risk**: [LOW / MEDIUM / HIGH — highest risk among governing ADRs]
**GDD Requirements Covered by ADRs**: [N / total]
**Untraced Requirements**: [list TR-IDs with no ADR, or "None"]
```

If there are untraced requirements:
> "⚠️ [N] requirements in [system] have no ADR. The epic can be created, but
> stories for these requirements will be marked Blocked until ADRs exist.
> Run `/architecture-decision` first, or proceed with placeholders."

Ask: "Shall I create Epic: [name]?"
Options: "Yes, create it", "Skip", "Pause — I need to write ADRs first"

---

## 5. Write Epic Files

After approval, ask: "May I write the epic file to `production/epics/[epic-slug]/EPIC.md`?"

After user confirms, write:

### `production/epics/[epic-slug]/EPIC.md`

```markdown
# Epic: [System Name]

> **Layer**: [Foundation / Core / Feature / Presentation]
> **GDD**: design/gdd/[filename].md
> **Architecture Module**: [module name]
> **Status**: Ready
> **Stories**: Not yet created — run `/create-stories [epic-slug]`

## Overview

[1 paragraph describing what this epic implements, derived from the GDD Overview
and the architecture module's stated responsibilities]

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-NNNN: [title] | [1-line summary] | LOW/MEDIUM/HIGH |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-[system]-001 | [requirement text from registry] | ADR-NNNN ✅ |
| TR-[system]-002 | [requirement text] | ❌ No ADR |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/[filename].md` are verified
- All Logic and Integration stories have passing test files in `tests/`
- All Visual/Feel and UI stories have evidence docs with sign-off in `production/qa/evidence/`

## Next Step

Run `/create-stories [epic-slug]` to break this epic into implementable stories.
```

### Update `production/epics/index.md`

Create or update the master index:

```markdown
# Epics Index

Last Updated: [date]
Engine: [name + version]

| Epic | Layer | System | GDD | Stories | Status |
|------|-------|--------|-----|---------|--------|
| [name] | Foundation | [system] | [file] | Not yet created | Ready |
```

---

## 6. Gate-Check Reminder

After writing all epics for the requested scope:

- **Foundation + Core complete**: These are required for the Pre-Production →
  Production gate. Run `/gate-check production` to check readiness.
- **Reminder**: Epics define scope. Stories define implementation steps. Run
  `/create-stories [epic-slug]` for each epic before developers can pick up work.

---

## Collaborative Protocol

1. **One epic at a time** — present each epic definition before asking to create it
2. **Warn on gaps** — flag untraced requirements before proceeding
3. **Ask before writing** — per-epic approval before writing any file
4. **No invention** — all content comes from GDDs, ADRs, and architecture docs
5. **Never create stories** — this skill stops at the epic level

After all requested epics are processed:

- **Verdict: COMPLETE** — [N] epic(s) written. Run `/create-stories [epic-slug]` per epic.
- **Verdict: BLOCKED** — user declined all epics, or no eligible systems found.
