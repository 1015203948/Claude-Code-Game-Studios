# Architecture Review Report
**Date:** 2026-04-15
**Engine:** Unity 6.3 LTS
**GDDs Reviewed:** 15 (note: `ship-combat-system.md` was accidentally overwritten during this review — 76 bytes remain; requirements reconstructed from ADR-0013 and session logs)
**ADRs Reviewed:** 18 (12 Accepted, 1 Proposed, 5 not yet needed)
**Review Mode:** Full

---

## ⚠️ DATA LOSS INCIDENT

During this review, a subagent accidentally **overwrote** `design/gdd/ship-combat-system.md` (reduced to 76 bytes). The file was never committed to git. All combat system requirements were reconstructed from:
- ADR-0013 (Combat System Architecture, Accepted 2026-04-15) — which extensively maps to every combat rule
- Session log (`production/session-logs/session-log.md`) — which records the GDD completion summary

**Action Required:** This GDD must be restored from an external backup or re-authored before Pre-Production entry.

---

## Traceability Summary

| Metric | Count | % |
|--------|-------|---|
| Total requirements | ~50 | 100% |
| ✅ Covered | ~45 | ~90% |
| ⚠️ Partial | 2 | ~4% |
| ❌ Gaps | 3 | ~6% |

**Gap breakdown:**
- ❌ GAP-1: TR-hud-002 (weapon cooldown HUD display) — no ADR exists
- ⚠️ PARTIAL-1: TR-hud-001 (hull bar) — HealthSystem exposes HullRatio but no ADR formally requires HUD subscription
- ⚠️ PARTIAL-2: TR-hud-003 (soft-lock reticle) — ADR-0018 (Proposed) defines events but is not yet Accepted

---

## Coverage Gaps

### ❌ GAP-1: TR-hud-002 — Weapon Cooldown Display
| Field | Value |
|-------|-------|
| **TR-ID** | TR-hud-002 |
| **GDD** | ship-hud.md |
| **System** | HUD / Presentation |
| **Requirement** | Ship HUD displays weapon cooldown timer, synced to `_fireTimer` (weapon fire rate accumulator) |
| **Suggested ADR** | ADR-0019: ShipHUD Architecture |
| **Domain** | UI / Integration |
| **Engine Risk** | LOW |

### ❌ GAP-2: TR-shipctrl-010 — FireRequested Event Contract
| Field | Value |
|-------|-------|
| **TR-ID** | TR-shipctrl-010 |
| **GDD** | ship-control-system.md |
| **System** | Ship Control / Feature |
| **Requirement** | `FireRequested` event interface consumed by CombatSystem for auto-fire trigger |
| **Suggested ADR** | ADR-0018 acceptance (merge into existing Proposed ADR) |
| **Domain** | Integration |
| **Engine Risk** | LOW |

### ❌ GAP-3: ADR-0007 Engine Compatibility Section
| Field | Value |
|-------|-------|
| **ADR** | ADR-0007 (Overlay Rendering) |
| **Issue** | No formal "Engine Compatibility" section exists |
| **Missing** | Post-cutoff API verification for `UIDocument.panelSettings` runtime assignment and `VisualElement.style.translate` |
| **Suggested Fix** | Add Engine Compatibility section per ADR template |

---

## Cross-ADR Conflicts

### 🔴 CONFLICT-1: Production Tick Implementation — ADR-0004 vs ADR-0016
**Type:** Integration / Implementation Pattern Mismatch

**ADR-0004 (Data Model, Accepted) states:**
> "Colony resource accumulation runs in StarMapScene as a UniTask coroutine:
> `await UniTask.WaitForSeconds(1f, ignoreTimeScale: true, cancellationToken: ct)`"

**ADR-0016 (Colony & Building, Accepted) actually implements:**
> "ColonyManager uses `_tickAccumulator` in `Update()`:
> `float simDelta = SimClock.Instance.DeltaTime;`
> `while (_tickAccumulator >= 1f) { _tickAccumulator -= 1f; ExecuteTick(); }`"

**Impact:** ADR-0004's code sample is misleading — the canonical production tick implementation is ADR-0016's Update-based approach. Both use `SimClock.DeltaTime` (correct), but ADR-0004's UniTask cancellation token pattern and ADR-0016's Update approach have different lifecycle behaviors.

**Resolution:** Update ADR-0004's "Migration Plan" section to state: *"the WaitForSeconds code sample shown above is illustrative only; the canonical production tick implementation is in ADR-0016."*

**Severity:** MEDIUM — both produce correct results but ADR-0004 could mislead future implementers.

---

### ⚠️ STALE-1: ADR-0013 Dependency Annotations
**Type:** Documentation Staleness

ADR-0013 (Combat System, Accepted 2026-04-15) still lists as dependencies:
> "ADR Dependencies: HealthSystem ADR (待创建), EnemySystem ADR (待创建)"

Both ADR-0014 (Health System) and ADR-0015 (Enemy System) were Accepted on 2026-04-15 — the same day as ADR-0013. The "待创建" qualifiers are stale and should be removed.

**Resolution:** Update ADR-0013 "ADR Dependencies" section — remove "待创建" qualifiers; the dependencies are satisfied.

---

## ADR Dependency Order (Topologically Sorted)

```
Foundation (no dependencies):
  1. ADR-0001: Scene Management         ✅ Accepted
  2. ADR-0002: Event Communication      ✅ Accepted
  3. ADR-0004: Data Model Framework    ✅ Accepted
  4. ADR-0012: SimClock                ✅ Accepted

Foundation → Core/Feature:
  5. ADR-0003: Input System            ✅ Accepted  (requires 0001, 0002)
  6. ADR-0014: Health System            ✅ Accepted  (no deps)
  7. ADR-0013: Combat System            ✅ Accepted  (requires 0001, 0002, 0004, 0014)
  8. ADR-0015: Enemy System             ✅ Accepted  (requires 0013, 0014)
  9. ADR-0016: Colony & Building        ✅ Accepted  (requires 0002, 0004)
 10. ADR-0017: Fleet Dispatch            ✅ Accepted  (requires 0004, 0013, 0014)
 11. ADR-0007: Overlay Rendering        ✅ Accepted  (requires 0001)
 12. ADR-0018: Ship Control System      🔵 Proposed  (requires 0003, 0013, 0014)

No dependency cycles detected.
No Accepted ADR depends on a Proposed ADR (ADR-0018 is not a dependency of any Accepted ADR).
```

---

## GDD Revision Flags

| Flag | GDD | Assumption | Reality | Action |
|------|-----|-----------|---------|--------|
| 🔴 CRITICAL | `ship-combat-system.md` | GDD content exists | 76 bytes on disk; requirements reconstructed from ADR-0013 | Restore from backup or re-author |
| 🟡 NOTE | `dual-perspective-switching.md` | Uses `aim_angle` (snake_case) | ADR-0018 uses `aimAngle` (camelCase) | Align naming before Pre-Production |

---

## Engine Compatibility Issues

### Deprecated API Check
All ADRs correctly avoid deprecated APIs:
- ✅ `Rigidbody.linearDamping` used correctly (ADR-0003, ADR-0018) — confirmed as Unity 6 rename from `drag`
- ✅ No use of deprecated `Input.GetKey()`, `Input.GetAxis()`, etc.
- ✅ No use of deprecated `Physics.RaycastAll()`
- ✅ No use of `Object.FindObjectsOfType<T>()` (deprecated in Unity 6.0+)

### Post-Cutoff API Status

| ADR | Post-Cutoff API | Risk | Verified? |
|-----|----------------|------|----------|
| ADR-0002 | `this.destroyCancellationToken` (UniTask) | LOW | ✅ |
| ADR-0003 | `EnhancedTouchSupport.Enable/Disable` | MEDIUM | ⚠️ Not in physics.md |
| ADR-0003 | `Rigidbody.linearDamping` | MEDIUM | ✅ Verified |
| ADR-0007 | `UIDocument.panelSettings` runtime switch | HIGH | ❌ Not verified |
| ADR-0007 | `VisualElement.style.translate` | MEDIUM | ⚠️ In deprecated-apis.md but not explicitly for overlay use |
| ADR-0018 | `Rigidbody.linearDamping` | MEDIUM | ✅ Verified |
| ADR-0018 | `CollisionDetectionMode.ContinuousDynamic` | MEDIUM | ⚠️ Not in physics.md |

### Missing Engine Compatibility Sections
- **ADR-0007**: Does not have a formal "Engine Compatibility" section — only "Summary", "Decision Makers", "Last Verified"

---

## Architecture Document Coverage

`docs/architecture/architecture.md` ✅ exists and is comprehensive.

| Check | Status |
|-------|--------|
| All 18 systems from systems-index.md appear in architecture layers | ✅ |
| Data flow covers all cross-system communication | ✅ (SO Channel + C# Event + Direct Call three-tier) |
| API boundaries support all integration requirements | ✅ |
| Orphaned architecture (no GDD) | ⚠️ ADR-0007 and ADR-0012 have no corresponding GDD — they are infrastructure additions not derived from a design doc |

---

## Verdict: CONCERNS

**Criteria:**
- ✅ No foundational requirements uncovered
- ✅ No dependency cycles
- ✅ No two Accepted ADRs with contradictory hard requirements
- ⚠️ ADR-0018 (Ship Control System) is Proposed — its TR coverage gaps are pending acceptance
- ⚠️ One critical GDD lost (`ship-combat-system.md`)
- ⚠️ One implementation ambiguity (ADR-0004 vs ADR-0016 production tick)
- ⚠️ One confirmed coverage gap (TR-hud-002)
- ⚠️ One missing Engine Compatibility section (ADR-0007)

### Blocking Issues (must resolve before PASS)
1. **`ship-combat-system.md` must be restored or re-authored** — critical GDD loss
2. **ADR-0007 needs a formal Engine Compatibility section** — blind spot
3. **ADR-0013 "ADR Dependencies" field must be updated** — stale "待创建" qualifiers
4. **TR-hud-002 gap must be formally addressed** — weapon cooldown HUD display

---

## Required ADRs (Priority Order)

| Priority | ID | Title | Addresses | Layer |
|----------|-----|-------|-----------|-------|
| **1** | ADR-0019 | ShipHUD Architecture | TR-hud-002 (weapon cooldown), TR-hud-001 (hull bar), TR-hud-003 (soft-lock reticle) | Presentation |
| **2** | ADR-0018 (Accept) | Ship Control System | All TR-shipctrl-* requirements (Proposed since 2026-04-15, ready for acceptance) | Feature |
| **3** | ADR-0007 (Update) | Overlay Rendering — Add EC section | ADR-0007 missing Engine Compatibility section | Presentation |

---

## Recommended Immediate Actions

1. **Restore or re-author `design/gdd/ship-combat-system.md`** — reconstruct from ADR-0013 + session log entry (session-log.md lines 570–620)
2. **Accept ADR-0018** — it has been Proposed since 2026-04-15; unblocks Core Epic story creation
3. **Update ADR-0013 ADR Dependencies** — remove "待创建" qualifiers for ADR-0014 and ADR-0015
4. **Add Engine Compatibility section to ADR-0007** — formalize post-cutoff API verification for overlay rendering

---

## Files Written/Updated by This Review

| File | Action |
|------|--------|
| `docs/architecture/architecture-review-2026-04-15.md` | Written (this file) |
| `docs/architecture/tr-registry.yaml` | Updated (new TR entries appended, revision dates set) |
| `docs/architecture/traceability-index.md` | Updated (current matrix replaced, statistics refreshed) |
| `docs/architecture/control-manifest.md` | No change (existing) |
| `production/session-state/active.md` | Updated (session extract appended) |

---

*Review conducted by: Architecture Review skill (full mode)*
*Next review trigger: After each new ADR is written, or when Pre-Production gate is requested*
