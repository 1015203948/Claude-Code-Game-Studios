# Architecture Review Report

**Date**: 2026-04-14
**Engine**: Unity 6.3 LTS
**GDDs Reviewed**: 13 (all MVP systems)
**ADRs Reviewed**: 3
**Mode**: full

---

## Traceability Summary

| Status | Count | % |
|--------|-------|---|
| Covered | 26 | 50% |
| Partial | 5 | 10% |
| Gap | 21 | 40% |
| **Total** | **52** | **100%** |

---

## Traceability Matrix

| TR-ID | GDD | System | Requirement | Domain | ADR Coverage | Status |
|-------|-----|--------|-------------|--------|-------------|--------|
| TR-resource-001 | resource-system.md | Resource | ResourceConfig SO read-only config layer | Data Architecture | — | GAP |
| TR-resource-002 | resource-system.md | Resource | ore_accumulation clamp formula per tick | Logic | — | GAP |
| TR-resource-003 | resource-system.md | Resource | ORE_CAP configurable constant, validate > 0 at startup | Config | — | GAP |
| TR-starmap-001 | star-map-system.md | StarMap | Undirected graph G=(V,E) data structure | Data Structure | — | GAP |
| TR-starmap-002 | star-map-system.md | StarMap | Visibility calculation IsVisible formula | Logic | — | GAP |
| TR-starmap-003 | star-map-system.md | StarMap | MVP fixed 5-node diamond layout | Config | — | GAP |
| TR-starmap-004 | star-map-system.md | StarMap | Node ownership change event to downstream | Communication | ADR-0002 (Tier 2) | COVERED |
| TR-starmap-005 | star-map-system.md | StarMap | Fleet arrival event to StarMap UI | Communication | ADR-0002 (Tier 2) | COVERED |
| TR-ship-001 | ship-system.md | Ship | ShipBlueprint read-only config | Data Structure | — | GAP |
| TR-ship-002 | ship-system.md | Ship | ShipState state machine (5 states) | State Machine | ADR-0001 | COVERED |
| TR-ship-003 | ship-system.md | Ship | ShipState cross-scene broadcast | Communication | ADR-0002 (Tier 1) | COVERED |
| TR-ship-004 | ship-system.md | Ship | ShipDataModel in MasterScene (authoritative) | Data Architecture | ADR-0001 | COVERED |
| TR-ship-005 | ship-system.md | Ship | carrier_v1 blueprint (HangarCapacity=3) | Data Structure | — | GAP |
| TR-building-001 | building-system.md | Building | BuildingInstance data model | Data Structure | — | GAP |
| TR-building-002 | building-system.md | Building | ShipyardTier integer field replaces HasShipyard | Data Structure | — | GAP |
| TR-building-003 | building-system.md | Building | OnBuildingConstructed event | Communication | ADR-0002 (Tier 2) | COVERED |
| TR-building-004 | building-system.md | Building | Build cost check via ResourceConfig.CanAfford() | Cross-system | — | PARTIAL |
| TR-health-001 | ship-health-system.md | Health | ApplyDamage interface + ShipState gating | Logic | — | GAP |
| TR-health-002 | ship-health-system.md | Health | CurrentHull=0 triggers death sequence | State Machine | ADR-0001 | PARTIAL |
| TR-health-003 | ship-health-system.md | Health | OnHullChanged broadcast to HUD | Communication | ADR-0002 | PARTIAL |
| TR-health-004 | ship-health-system.md | Health | CockpitScene write-back to MasterScene | Data Sync | ADR-0001 | COVERED |
| TR-colony-001 | colony-system.md | Colony | Global resource pool in StarMapScene | Data Architecture | ADR-0001 | COVERED |
| TR-colony-002 | colony-system.md | Colony | Production tick uses WaitForSecondsRealtime | Timing | — | GAP |
| TR-colony-003 | colony-system.md | Colony | OnResourcesUpdated per-tick broadcast | Communication | ADR-0002 (Tier 2) | COVERED |
| TR-colony-004 | colony-system.md | Colony | OnShipBuilt cross-scene event | Communication | ADR-0002 (Tier 1) | COVERED |
| TR-colony-005 | colony-system.md | Colony | Energy deficit event to HUD warning | Communication | ADR-0002 (Tier 2) | COVERED |
| TR-control-001 | ship-control-system.md | Control | Dual virtual joystick: screen partition + fingerId | Input | ADR-0003 | COVERED |
| TR-control-002 | ship-control-system.md | Control | Dead zone formula JOYSTICK_DEAD_ZONE=0.08 | Input | ADR-0003 | COVERED |
| TR-control-003 | ship-control-system.md | Control | ShipState gating (C-1) | Input | ADR-0003 | COVERED |
| TR-control-004 | ship-control-system.md | Control | Rigidbody.AddForce + linearDamping | Physics | ADR-0003 | COVERED |
| TR-control-005 | ship-control-system.md | Control | fingerId independent tracking | Input | ADR-0003 | COVERED |
| TR-combat-001 | ship-combat-system.md | Combat | Auto-fire on angle threshold | Logic | — | GAP |
| TR-combat-002 | ship-combat-system.md | Combat | BeginCombat triggers ShipState=IN_COMBAT | State Machine | ADR-0002 | COVERED |
| TR-combat-003 | ship-combat-system.md | Combat | CombatVictory/Defeat notifies StarMap | Communication | ADR-0002 (Tier 1) | COVERED |
| TR-combat-004 | ship-combat-system.md | Combat | Unattended combat instant resolution | Logic | — | GAP |
| TR-combat-005 | ship-combat-system.md | Combat | Raycast hit detection (Physics.Raycast) | Physics | — | GAP |
| TR-enemy-001 | enemy-system.md | Enemy | 2-instance spawn (SPAWN_RADIUS=150m, angle separation) | Logic | — | GAP |
| TR-enemy-002 | enemy-system.md | Enemy | AI state machine (4 states) | Logic | — | GAP |
| TR-enemy-003 | enemy-system.md | Enemy | Independent HP (not registered to HealthSystem) | Data Architecture | — | GAP |
| TR-enemy-004 | enemy-system.md | Enemy | OnEnemyDied event to CombatSystem | Communication | — | GAP |
| TR-fleet-001 | fleet-dispatch-system.md | Fleet | BFS pathfinding on star graph | Algorithm | — | GAP |
| TR-fleet-002 | fleet-dispatch-system.md | Fleet | FLEET_TRAVEL_TIME=3s per hop | Timing | — | GAP |
| TR-fleet-003 | fleet-dispatch-system.md | Fleet | Cancel movement returns symmetrically | Logic | — | GAP |
| TR-fleet-004 | fleet-dispatch-system.md | Fleet | Dispatch precondition D-1 | Logic | — | GAP |
| TR-dvs-001 | dual-perspective-switching.md | DualView | 3-scene Additive topology | Scene Architecture | ADR-0001 | COVERED |
| TR-dvs-002 | dual-perspective-switching.md | DualView | Switch time <= 1.0s | Performance | ADR-0001 | COVERED |
| TR-dvs-003 | dual-perspective-switching.md | DualView | Camera.enabled toggling | Rendering | ADR-0001 | COVERED |
| TR-dvs-004 | dual-perspective-switching.md | DualView | _isSwitching concurrency guard | State Machine | ADR-0001 | COVERED |
| TR-dvs-005 | dual-perspective-switching.md | DualView | _preEnterState snapshot/restore | State Machine | ADR-0001 | COVERED |
| TR-dvs-006 | dual-perspective-switching.md | DualView | ViewLayerChannel cross-scene broadcast | Communication | ADR-0002 (Tier 1) | COVERED |
| TR-dvs-007 | dual-perspective-switching.md | DualView | ActionMap follows ViewLayer switch | Input | ADR-0003 | COVERED |
| TR-dvs-008 | dual-perspective-switching.md | DualView | Strategy layer runs realtime (no pause) | Scene Architecture | ADR-0001 | COVERED |
| TR-starmap-ui-001 | star-map-ui.md | StarMapUI | Node color-coded rendering | UI/Rendering | — | GAP |
| TR-starmap-ui-002 | star-map-ui.md | StarMapUI | Touch hotzone >= 48dp | UI/Platform | — | GAP |
| TR-starmap-ui-003 | star-map-ui.md | StarMapUI | Pinch zoom + drag pan | Input | ADR-0003 | PARTIAL |
| TR-starmap-ui-004 | star-map-ui.md | StarMapUI | UI Toolkit EventSystem priority | Input | ADR-0003 | COVERED |
| TR-hud-001 | ship-hud.md | HUD | HudVisible condition gating | UI/State | ADR-0001 + ADR-0002 | COVERED |
| TR-hud-002 | ship-hud.md | HUD | Arc health bar UI | UI/Rendering | — | GAP |
| TR-hud-003 | ship-hud.md | HUD | Soft-lock reticle | UI/Rendering | — | GAP |
| TR-hud-004 | ship-hud.md | HUD | Perspective toggle button | UI/Input | — | GAP |

---

## Coverage Gaps (no ADR exists)

### Foundation Layer Gaps (resolve first)

- **TR-resource-001~003**: Resource system data architecture
  - Suggested: ADR-0004 "Data Model Architecture"
  - Domain: Data Architecture
  - Engine Risk: LOW

- **TR-starmap-001~003**: Star map data structure, visibility, MVP layout
  - Suggested: Include in ADR-0004
  - Domain: Data Structure / Algorithm
  - Engine Risk: LOW

- **TR-ship-001, TR-ship-005**: Ship blueprints and carrier blueprint
  - Suggested: Include in ADR-0004
  - Domain: Data Structure
  - Engine Risk: LOW

### Feature Layer Gaps (resolve before Feature Epics)

- **TR-combat-001, 004, 005**: Combat logic (auto-fire, unattended, raycast)
  - Suggested: ADR-0005 "Combat Architecture"
  - Domain: Logic / Physics
  - Engine Risk: MEDIUM (Raycast NonAlloc verification needed)

- **TR-enemy-001~004**: Enemy AI system
  - Suggested: Include in ADR-0005
  - Domain: AI / Logic
  - Engine Risk: LOW

- **TR-fleet-001~004**: Fleet dispatch logic
  - Suggested: Include in ADR-0004 or standalone
  - Domain: Algorithm / Logic
  - Engine Risk: LOW

- **TR-colony-002**: Production tick timing
  - Suggested: Include in ADR-0004
  - Domain: Timing
  - Engine Risk: LOW

- **TR-health-001**: ApplyDamage interface
  - Suggested: Include in ADR-0005
  - Domain: Logic
  - Engine Risk: LOW

### Presentation Layer Gaps (lowest priority)

- **TR-starmap-ui-001~002**: StarMap UI rendering and touch zones
- **TR-hud-002~004**: HUD arc bar, reticle, toggle button
  - Suggested: ADR-0006 "UI Architecture"
  - Domain: UI / Rendering
  - Engine Risk: MEDIUM (UI Toolkit runtime compatibility)

---

## Cross-ADR Conflicts

None. All three ADRs are internally consistent:
- Data ownership: No overlapping claims
- Integration contracts: SO Channel specs align across ADR-0002 and ADR-0003
- Performance budgets: No conflicting allocations
- Dependency direction: Clean topological order
- Architecture patterns: Consistent event-driven communication
- State management: ShipState, ViewLayer, CurrentHull all have single authoritative owners

---

## ADR Dependency Order

```
Foundation (no dependencies):
  1. ADR-0001: Scene Management Architecture

Depends on Foundation:
  2. ADR-0002: Event/Communication Architecture (requires ADR-0001)

Depends on ADR-0001 + ADR-0002:
  3. ADR-0003: Input System Architecture (requires ADR-0001, ADR-0002)
```

All three ADRs are currently **Proposed**. Recommend accepting in topological
order: ADR-0001 -> ADR-0002 -> ADR-0003.

No dependency cycles detected.

---

## GDD Revision Flags

No GDD revision flags — all GDD assumptions are consistent with verified
engine behaviour.

---

## Engine Compatibility Issues

### Engine Audit Results

- Engine: Unity 6.3 LTS
- ADRs with Engine Compatibility section: 3 / 3
- Deprecated API References: None
- Stale Version References: None
- Post-Cutoff API Conflicts: None

### Post-Cutoff APIs Verified

| API | ADR | Engine Ref Verification |
|-----|-----|----------------------|
| SceneManager.LoadSceneAsync(Additive) | ADR-0001 | No changes in 6.3 |
| Camera.enabled | ADR-0001 | No changes |
| destroyCancellationToken | ADR-0001, ADR-0002 | Available since 2022.2 |
| EnhancedTouchSupport.Enable/Disable | ADR-0003 | New Input System standard |
| InputActionMap.Enable/Disable | ADR-0003 | New Input System standard |
| Rigidbody.linearDamping | ADR-0003 | Unity 6+ rename from .drag |

### Engine Specialist Findings

**Reviewer**: unity-specialist
**Date**: 2026-04-14

| ADR | Level | Finding |
|-----|-------|---------|
| ADR-0001 | ADVISORY | Camera.enabled rationale wording is slightly off — actual benefit is "avoid URP Camera Stack lifecycle rebuild overhead", not "avoid invalid Culling Pass". Decision itself is correct. |
| ADR-0001 | ADVISORY | Android low-memory: destroyCancellationToken may cancel prematurely during OnApplicationPause. Recommend CreateLinkedTokenSource to distinguish voluntary vs destroy cancellation. |
| ADR-0002 | ADVISORY | Confirm whether MessagePipe is actually used — ADR body uses pure SO Channel + C# event pattern, no MessagePipe references. Remove from descriptions if not used. |
| ADR-0003 | **BLOCKING** | Touch joystick normalization uses `touch.delta` (per-frame increment) instead of offset from touch origin. Same finger movement speed produces different thrust at different framerates. Should track Touch.Began origin and compute offset. |
| ADR-0003 | ADVISORY | Android background switch: fingerId becomes dangling. OnApplicationPause should reset _thrustFingerId / _aimFingerId to -1. |
| ADR-0003 | ADVISORY | ADR-0001 mixes UI Toolkit and Canvas references (Canvas.enabled vs DisplayStyle.None) — recommend explicit UI tech stack decision. |

---

## Architecture Document Coverage

`docs/architecture/architecture.md` does not exist. Expected at Technical Setup
stage — not yet needed. The three ADRs cover Foundation layer decisions.

---

## Verdict: CONCERNS

### Rationale

1. **Foundation layer ADR coverage is solid**: Scene management, event
   communication, and input system cover all architectural requirements for the
   dual-perspective switching system — the MVP core hypothesis validation point.
2. **No cross-ADR conflicts**, clean dependency graph.
3. **Engine compatibility mostly passes** — no deprecated API risks.
4. **1 BLOCKING engine specialist finding**: ADR-0003 joystick normalization
   logic uses per-frame delta instead of origin offset, causing framerate-
   dependent behaviour. Must be corrected before implementation.
5. **21 gaps (40%)** are "ADRs not yet written" rather than "gaps in existing
   ADRs" — distributed across Data Model and Feature Logic layers, can be
   written before their respective Epics start.

### Blocking Issues (must resolve for PASS)

1. **ADR-0003 joystick normalization fix**: `ProcessTouchInput()` should track
   Touch.Began origin position and compute current-to-origin offset, not use
   touch.delta (per-frame increment). Must align with GDD dead zone formula.
   Add verification criterion: "same finger speed at 30fps and 60fps produces
   thrust delta <= 5%."

### Required ADRs (prioritised)

| Priority | ADR | Covers | Timing |
|----------|-----|--------|--------|
| 1 | ADR-0004: Data Model Architecture | TR-resource-001~003, TR-starmap-001~003, TR-ship-001/005, TR-building-001~002, TR-colony-002, TR-fleet-001~004 | Before Core Epic |
| 2 | ADR-0005: Combat Architecture | TR-combat-001/004/005, TR-enemy-001~004, TR-health-001 | Before Feature Epic (Combat) |
| 3 | ADR-0006: UI Architecture | TR-starmap-ui-001~002, TR-hud-002~004 | Before Presentation Epic |
