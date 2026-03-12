# Skill Flow Diagrams

Visual maps of how skills chain together across the 7 development phases.
These show what runs before and after each skill, and what artifacts flow between them.

---

## Full Pipeline Overview (Zero to Ship)

```
PHASE 1: CONCEPT
  /start ──────────────────────────────────────────────────────► routes to A/B/C/D
  /brainstorm ──────────────────────────────────────────────────► design/gdd/game-concept.md
  /setup-engine ────────────────────────────────────────────────► CLAUDE.md + technical-preferences.md
  /design-review [game-concept.md] ────────────────────────────► concept validated
  /gate-check ─────────────────────────────────────────────────► PASS → advance to systems-design
        │
        ▼
PHASE 2: SYSTEMS DESIGN
  /map-systems ────────────────────────────────────────────────► design/gdd/systems-index.md
        │
        ▼ (for each system, in dependency order)
  /design-system [name] ──────────────────────────────────────► design/gdd/[system].md
  /design-review [system].md ─────────────────────────────────► per-GDD review comments
        │
        ▼ (after all MVP GDDs done)
  /review-all-gdds ────────────────────────────────────────────► design/gdd/gdd-cross-review-[date].md
  /gate-check ─────────────────────────────────────────────────► PASS → advance to technical-setup
        │
        ▼
PHASE 3: TECHNICAL SETUP
  /create-architecture ────────────────────────────────────────► docs/architecture/master.md
  /architecture-decision (×N) ─────────────────────────────────► docs/architecture/[adr-nnn].md
  /architecture-review ────────────────────────────────────────► review report + docs/architecture/tr-registry.yaml
  /create-control-manifest ────────────────────────────────────► docs/architecture/control-manifest.md
  /gate-check ─────────────────────────────────────────────────► PASS → advance to pre-production
        │
        ▼
PHASE 4: PRE-PRODUCTION
  /ux-design [screen/hud/patterns] ────────────────────────────► design/ux/*.md
  /ux-review ──────────────────────────────────────────────────► UX specs approved
  /create-epics-stories ───────────────────────────────────────► production/stories/STORY-*.md
  /prototype [core-mechanic] ──────────────────────────────────► prototypes/[name]/
  /playtest-report ────────────────────────────────────────────► tests/playtest/vertical-slice.md
  /sprint-plan new ────────────────────────────────────────────► production/sprints/sprint-01.md
  /gate-check ─────────────────────────────────────────────────► PASS → advance to production
        │
        ▼
PHASE 5: PRODUCTION (repeating sprint loop)
  /sprint-status ──────────────────────────────────────────────► sprint snapshot
  /story-readiness [story] ────────────────────────────────────► story validated READY
        │
        ▼ implement (gameplay-programmer, etc.)
  /story-done [story] ─────────────────────────────────────────► story closed + next surfaced
  /sprint-plan [next] ─────────────────────────────────────────► next sprint
        │
        ▼ (after Production milestone)
  /milestone-review ───────────────────────────────────────────► milestone report
  /gate-check ─────────────────────────────────────────────────► PASS → advance to polish
        │
        ▼
PHASE 6: POLISH
  /perf-profile ───────────────────────────────────────────────► perf report + fixes
  /balance-check ──────────────────────────────────────────────► balance report + fixes
  /tech-debt ──────────────────────────────────────────────────► docs/tech-debt-register.md
  /team-polish ────────────────────────────────────────────────► polish sprint orchestrated
  /gate-check ─────────────────────────────────────────────────► PASS → advance to release
        │
        ▼
PHASE 7: RELEASE
  /launch-checklist ───────────────────────────────────────────► launch readiness report
  /release-checklist ──────────────────────────────────────────► platform-specific checklist
  /changelog ──────────────────────────────────────────────────► CHANGELOG.md
  /patch-notes ────────────────────────────────────────────────► player-facing notes
  /team-release ───────────────────────────────────────────────► release pipeline orchestrated
```

---

## Skill Chain: /design-system in Detail

How a single GDD gets authored, reviewed, and handed to architecture:

```
systems-index.md (input)
game-concept.md (input)
upstream GDDs (input, if any)
        │
        ▼
/design-system [name]
        │
        ├── Pre-check: feasibility table + engine risk flags
        │
        ├── Section cycle × 8:
        │     question → options → decision → draft → approval → WRITE
        │     [each section written to file immediately after approval]
        │
        └── Output: design/gdd/[system].md (complete, all 8 sections)
                │
                ▼
        /design-review design/gdd/[system].md
                │
                ├── APPROVED → mark DONE in systems-index, proceed to next system
                ├── NEEDS REVISION → agent shows specific issues, re-enter section cycle
                └── MAJOR REVISION → significant redesign needed before next system
                        │
                        ▼ (after all MVP GDDs + cross-review)
                /review-all-gdds
                        │
                        └── Output: gdd-cross-review-[date].md
```

---

## Skill Chain: Story Lifecycle in Detail

How a story gets from backlog to closed:

```
/create-epics-stories
        │
        └── Output: production/stories/STORY-[SYS]-NNN.md
                    (Status: backlog or blocked if ADR is Proposed)
                │
                ▼
        /story-readiness [story]
                │
                ├── READY → Status: ready-for-dev → pick up for implementation
                ├── NEEDS WORK → agent shows specific gaps → resolve → re-run readiness
                └── BLOCKED → ADR still Proposed, or upstream story incomplete
                        │
                        ▼ (after READY)
                Implementation (gameplay-programmer, etc.)
                        │
                        ▼
                /story-done [story]
                        │
                        ├── COMPLETE → Status: Complete, sprint-status.yaml updated, next story surfaced
                        ├── COMPLETE WITH NOTES → complete but some criteria deferred (logged)
                        └── BLOCKED → acceptance criteria cannot be verified → investigate blocker
```

---

## Skill Chain: UX Pipeline in Detail

```
design/gdd/*.md (UX requirements extracted)
design/player-journey.md (emotional arc)
        │
        ▼
/ux-design hud              → design/ux/hud.md
/ux-design screen [name]    → design/ux/screens/[name].md
/ux-design patterns         → design/ux/interaction-patterns.md
        │
        ▼
/ux-review design/ux/
        │
        ├── APPROVED → all specs ready for /team-ui
        ├── NEEDS REVISION → blocking issues listed → fix → re-run review
        └── MAJOR REVISION → fundamental UX problems → significant redesign
                │
                ▼ (after APPROVED)
        /team-ui
                │
                ├── Phase 1: context load + /ux-design (if specs missing)
                ├── Phase 2: visual design (art-director)
                ├── Phase 3: layout implementation (ui-programmer)
                ├── Phase 4: accessibility audit (accessibility-specialist)
                └── Phase 5: final review
```

---

## Brownfield Onboarding Flow

For projects with existing work (use `/start` option D or run directly):

```
/project-stage-detect    → stage detection report
        │
        ▼
/adopt
        │
        ├── Phase 1: detect what exists
        ├── Phase 2: FORMAT audit (not just existence)
        ├── Phase 3: classify gaps (BLOCKING / HIGH / MEDIUM / LOW)
        ├── Phase 4: ordered migration plan
        ├── Phase 5: write docs/adoption-plan-[date].md
        └── Phase 6: fix most urgent gap inline (optional)
                │
                ▼
        /design-system retrofit [path]    → fills missing GDD sections
        /architecture-decision retrofit [path] → fills missing ADR sections
        /gate-check                       → where are you in the pipeline?
```

---

## How to Read These Diagrams

| Symbol | Meaning |
|--------|---------|
| `──►` | Produces this artifact |
| `│ ▼` | Flows into next step |
| `├──` | Branch (multiple possible outcomes) |
| `×N` | Runs N times (once per system, story, etc.) |
| `(input)` | Read by the skill but not produced here |
| `[optional]` | Not required for the gate to pass |
| `WRITE` (caps) | File written to disk immediately |

---

## Common Entry Points

| Where you are | Run this |
|---------------|---------|
| Brand new, no idea | `/start` → `/brainstorm` |
| Have a concept, no engine | `/setup-engine` |
| Have concept + engine | `/map-systems` |
| Mid-systems design | `/design-system [next system]` or `/map-systems next` |
| All GDDs done | `/review-all-gdds` → `/gate-check` |
| In technical setup | `/create-architecture` → `/architecture-decision` |
| Have stories, ready to code | `/story-readiness [story]` |
| Story done | `/story-done [story]` |
| Not sure | `/help` |
| Existing project | `/adopt` |
