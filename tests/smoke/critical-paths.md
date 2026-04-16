# Smoke Test: Critical Paths

**Purpose**: Run these checks in under 15 minutes before any QA hand-off.
**Run via**: `/smoke-check` (reads this file)
**Update**: Add new entries when new core systems are implemented.

---

## Core Stability (always run)

1. Game launches to main menu without crash
2. New game / session can be started from the main menu
3. Main menu responds to all touch inputs without freezing

## Dual-Perspective Switch (implement Sprint 1)

<!-- Update when ViewLayerManager is implemented -->
4. [Star map view loads without error]
5. [Switch to cockpit view completes in ≤ 1.0s on minimum-spec Android]
6. [Switch back to star map restores correct camera state]

## Ship Control (implement after cockpit prototype)

<!-- Update when ShipControlSystem is implemented -->
7. [Ship responds to virtual joystick thrust input]
8. [Dead zone threshold correctly filters micro-inputs]
9. [Ship state gates input (no movement in star map view)]

## Data Integrity

10. Save game completes without error (implement when save system is built)
11. Load game restores correct state across sessions

## Performance

12. No visible frame rate drops below 60fps on target hardware
13. No memory growth over 5 minutes of continuous play

---

## How to Run

Manual: open the build, step through each item, mark PASS / FAIL / SKIP.

Record results in `production/qa/smoke-[date].md`.

A PASS or PASS WITH WARNINGS verdict is required before `/gate-check Production → Polish`.
