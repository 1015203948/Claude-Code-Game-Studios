# Architecture Traceability Index
Last Updated: 2026-04-18
Engine: Unity 6.3 LTS

## Coverage Summary
- Total requirements: 56
- Covered: 56 (100%)
- Partial: 0 (~0%)
- Gaps: 0 (~0%)

> Previous review (2026-04-15): 52 total, 48 covered (92%), 6 partial (12%), 2 gaps (4%)
> This update: Ship HUD (TR-hud-001~003) and StarMap UI (TR-starmapui-001~004) fully implemented; ship-equipment epic merged without new TRs (implemented outside original scope).

## ADR Coverage Map

| ADR | System | Status | TRs Covered | GDD Requirements Addressed |
|-----|--------|--------|-------------|--------------------------|
| ADR-0001 | Scene Management | ✅ Accepted | — | 3-scene Additive topology, Camera.enabled switching, ShipDataModel authority |
| ADR-0002 | Event Communication | ✅ Accepted | TR-event-001~003, TR-ship-003 | SO Channel pattern, Tier 1/2/3 rules, OnEnable/OnDisable mandate |
| ADR-0003 | Input System | ✅ Accepted | — | New Input System, EnhancedTouch, dual ActionMap, dead zone formula |
| ADR-0004 | Data Model | ✅ Accepted | TR-resource-001~003, TR-starmap-001~003, TR-ship-001~005 | Config SO, runtime state, BFS, production tick |
| ADR-0007 | Overlay Rendering | ✅ Accepted | — | ScreenOverlay UIDocument, panelSettings runtime switch |
| ADR-0012 | SimClock | ✅ Accepted | TR-dvs-009 | DeltaTime = unscaledDeltaTime × SimRate |
| ADR-0013 | Combat System | ✅ Accepted | TR-combat-001~006 | Combat state machine, auto-fire, Raycast hit, unattended resolution |
| ADR-0014 | Health System | ✅ Accepted | TR-health-001~004 | ApplyDamage, death sequence H-5, ShipState gating |
| ADR-0015 | Enemy System | ✅ Accepted | TR-enemy-001~007 | Enemy AI (4-state), OverlapSphere, enemy weapons |
| ADR-0016 | Colony & Building | ✅ Accepted | TR-colony-001~003, TR-building-001~004 | Tick loop, atomic build, ShipyardTier |
| ADR-0017 | Fleet Dispatch | ✅ Accepted | TR-fleet-001~006 | DispatchOrder, BFS path, SimRate, cancel/return |
| ADR-0018 | Ship Control System | ✅ Accepted | TR-shipctrl-001~009 | Flight physics, soft lock, camera switch, state init/cleanup |
| ADR-0019 | Ship HUD | ✅ Accepted | TR-hud-001~003 | ShipHUD: hull bar, speed, cooldown, soft-lock reticle, combat indicator |
| ADR-0020 | StarMap UI | ✅ Accepted | TR-starmapui-001~004 | StarMapUI: Painter2D rendering, node selection, fleet icons, dispatch flow |

## Full Traceability Matrix

| TR-ID | GDD | System | Requirement | Domain | ADR | Status |
|-------|-----|--------|-------------|--------|-----|--------|
| TR-resource-001 | resource-system.md | Resource | ResourceConfig SO: ORE_CAP, ENERGY_CAP, ORE_PER_MINE | Logic | ADR-0004 | ✅ |
| TR-resource-002 | resource-system.md | Resource | CanAfford() pure function, no state mutation | Logic | ADR-0004 | ✅ |
| TR-resource-003 | resource-system.md | Resource | ore clamp [0, ORE_CAP]; net_ore/net_energy formula | Logic | ADR-0004 | ✅ |
| TR-starmap-001 | star-map-system.md | StarMap | Graph G=(V,E): StarNode + StarEdge, O(V+E) adjacency | Logic | ADR-0004 | ✅ |
| TR-starmap-002 | star-map-system.md | StarMap | IsVisible(fogState): UNEXPLORED/EXPLORED/VISIBLE | Logic | ADR-0004 | ✅ |
| TR-starmap-003 | star-map-system.md | StarMap | GetNeighbors(nodeId), AreAdjacent(a,b) — read-only | Logic | ADR-0004 | ✅ |
| TR-ship-001 | ship-system.md | Ship | ShipBlueprint SO: MaxHull, ThrustPower, TurnSpeed, WeaponSlots | Logic | ADR-0004 | ✅ |
| TR-ship-002 | ship-system.md | Ship | is_valid_ship_instance(blueprint) validation | Logic | ADR-0004 | ✅ |
| TR-ship-003 | ship-system.md | Ship | ShipStateChannel broadcast with OnEnable/OnDisable pairing | Integration | ADR-0002 | ✅ |
| TR-ship-004 | ship-system.md | Ship | ShipDataModel as MasterScene authority: CurrentHull, ShipState | Integration | ADR-0001 | ✅ |
| TR-ship-005 | ship-system.md | Ship | ShipBlueprintRegistry.GetBlueprint(id) singleton | Logic | ADR-0004 | ✅ |
| TR-event-001 | dual-perspective-switching.md | EventBus | SO Channel OnEnable/OnDisable mandatory pairing | Integration | ADR-0002 | ✅ |
| TR-event-002 | dual-perspective-switching.md | EventBus | UniTask await with destroyCancellationToken | Integration | ADR-0002 | ✅ |
| TR-event-003 | dual-perspective-switching.md | EventBus | Channel Raise() → event?.Invoke(), zero reflection | Integration | ADR-0002 | ✅ |
| TR-dvs-006 | dual-perspective-switching.md | DualView | ViewLayerChannel broadcast, OnEnable/OnDisable subscription | Integration | ADR-0001 | ✅ |
| TR-dvs-009 | dual-perspective-switching.md | DualView | SimClock.DeltaTime = unscaledDeltaTime × SimRate, SimRate ∈ {0,1,5,20} | Logic | ADR-0012 | ✅ |
| TR-dvs-016 | dual-perspective-switching.md | DualView | Cockpit overlay: physics continues, inertia intact | Integration | ADR-0012 | ✅ |
| TR-dvs-017 | dual-perspective-switching.md | DualView | Cockpit overlay close: 5 frames to restore input response | Integration | ADR-0012 | ✅ |
| TR-colony-001 | colony-system.md | Colony | ColonyManager Update tick loop with SimClock.DeltaTime | Integration | ADR-0016 | ✅ |
| TR-colony-002 | colony-system.md | Colony | OnResourcesUpdated(ResourceSnapshot) per tick, ore clamp | Integration | ADR-0016 | ✅ |
| TR-colony-003 | colony-system.md | Colony | ColonyShipChannel ship built broadcast | Integration | ADR-0002 | ✅ |
| TR-health-001 | ship-health-system.md | Health | ApplyDamage with ShipState gating (IN_COCKPIT/IN_COMBAT accept; DOCKED/IN_TRANSIT ignore) | Logic | ADR-0014 | ✅ |
| TR-health-002 | ship-health-system.md | Health | Death sequence H-5: OnShipDying → DestroyShip → OnPlayerShipDestroyed → OnShipDestroyed | State Machine | ADR-0014 | ✅ |
| TR-health-003 | ship-health-system.md | Health | OnHullChanged broadcast per change, HullRatio [0,1] | Integration | ADR-0014 | ✅ |
| TR-health-004 | ship-health-system.md | Health | MasterScene singleton; U-4 bypasses HealthSystem directly | Integration | ADR-0014 | ✅ |
| TR-combat-001 | ship-combat-system.md | Combat | Combat state machine: IN_COCKPIT → IN_COMBAT → IN_COCKPIT/DESTROYED | State Machine | ADR-0013 | ✅ |
| TR-combat-002 | ship-combat-system.md | Combat | Auto-fire: aimAngle ≤ FIRE_ANGLE_THRESHOLD, _fireTimer += Time.deltaTime | Logic | ADR-0013 | ✅ |
| TR-combat-003 | ship-combat-system.md | Combat | RaycastNonAlloc → HealthSystem.ApplyDamage | Physics | ADR-0013 | ✅ |
| TR-combat-004 | ship-combat-system.md | Combat | Unattended: P/E each -1/turn loop, U-4 bypasses HealthSystem | Logic | ADR-0013 | ✅ |
| TR-combat-005 | ship-combat-system.md | Combat | CombatChannel.RaiseVictory/Defeat, RaiseBegin after sceneLoaded | Integration | ADR-0013 | ✅ |
| TR-combat-006 | ship-combat-system.md | Combat | Victory: IN_COCKPIT; Defeat: DESTROYED | State Machine | ADR-0013 | ✅ |
| TR-enemy-001 | enemy-system.md | Enemy | EnemyInstance: InstanceId, AiState, CurrentHull, FireTimer, TargetPlayerId | Data Structure | ADR-0015 | ✅ |
| TR-enemy-002 | enemy-system.md | Enemy | Spawn 2 instances, radius 150m, angular spacing ≥ 90° | Logic | ADR-0015 | ✅ |
| TR-enemy-003 | enemy-system.md | Enemy | SPAWNING: RandomDelay 3-5s → APPROACHING; DYING: 1.2s → Despawn | Logic | ADR-0015 | ✅ |
| TR-enemy-004 | enemy-system.md | Enemy | APPROACHING: straight-line; FLANKING: arc path to player flank | Logic | ADR-0015 | ✅ |
| TR-enemy-005 | enemy-system.md | Enemy | Enemy weapons: RaycastNonAlloc → HealthSystem.ApplyDamage | Physics | ADR-0015 | ✅ |
| TR-enemy-006 | enemy-system.md | Enemy | OverlapSphereNonAlloc zero-GC player query; collision retry 3x | Physics | ADR-0015 | ✅ |
| TR-enemy-007 | enemy-system.md | Enemy | C# event Tier 2 with CombatSystem; OnShipDying subscription | Integration | ADR-0015 | ✅ |
| TR-building-001 | building-system.md | Building | BuildingInstance: InstanceId, BuildingType, NodeId, IsActive | Data Structure | ADR-0016 | ✅ |
| TR-building-002 | building-system.md | Building | ShipyardTier per node: Shipyard→1, Upgrade→+1, captured→0 | Logic | ADR-0016 | ✅ |
| TR-building-003 | building-system.md | Building | Atomic build: CanAfford → DeductResources → create → rollback on failure | Logic | ADR-0016 | ✅ |
| TR-building-004 | building-system.md | Building | GetNodeProductionDelta; RefreshProductionCache on build | Logic | ADR-0016 | ✅ |
| TR-fleet-001 | fleet-dispatch-system.md | Fleet | DispatchOrder: LockedPath snapshot, FLEET_TRAVEL_TIME=3s/hop | Data Structure | ADR-0017 | ✅ |
| TR-fleet-002 | fleet-dispatch-system.md | Fleet | RequestDispatch: validate DOCKED → BFS → create order → IN_TRANSIT | Logic | ADR-0017 | ✅ |
| TR-fleet-003 | fleet-dispatch-system.md | Fleet | Update: HopProgress += SimClock.DeltaTime, arrival triggers state change | Logic | ADR-0017 | ✅ |
| TR-fleet-004 | fleet-dispatch-system.md | Fleet | CancelDispatch: reverse path, IsReturning=true | Logic | ADR-0017 | ✅ |
| TR-fleet-005 | fleet-dispatch-system.md | Fleet | ENEMY arrival → CombatSystem.BeginCombat or U-4 unattended | Integration | ADR-0017 | ✅ |
| TR-fleet-006 | fleet-dispatch-system.md | Fleet | ShipDataModel.Destroy() → orphan order cleanup | Integration | ADR-0017 | ✅ |
| TR-shipctrl-001 | ship-control-system.md | ShipControl | P-1: AddForce thrust, P-2: soft speed cap | Physics | ADR-0018 | ✅ (Proposed) |
| TR-shipctrl-002 | ship-control-system.md | ShipControl | P-4: MoveRotation steering, P-5: angularVelocity zero | Physics | ADR-0018 | ✅ (Proposed) |
| TR-shipctrl-003 | ship-control-system.md | ShipControl | P-6: FLIGHT_PLANE_Y lock, no direct velocity assignment | Physics | ADR-0018 | ✅ (Proposed) |
| TR-shipctrl-004 | ship-control-system.md | ShipControl | C-1: ShipState gate, C-2: dead zone, C-3: no backward thrust | Input | ADR-0018 | ✅ (Proposed) |
| TR-shipctrl-005 | ship-control-system.md | ShipControl | C-4: aim assist, C-5: fingerId isolation per zone | Input | ADR-0018 | ✅ (Proposed) |
| TR-shipctrl-006 | ship-control-system.md | ShipControl | L-1~L-3: soft lock acquire/stable/fire-trigger | Logic | ADR-0018 | ✅ (Proposed) |
| TR-shipctrl-007 | ship-control-system.md | ShipControl | V-1~V-4: third-person/first-person switch, SmoothDamp | Presentation | ADR-0018 | ✅ (Proposed) |
| TR-shipctrl-008 | ship-control-system.md | ShipControl | S-1~S-4: state init/cleanup on IN_COCKPIT enter/exit | State Machine | ADR-0018 | ✅ (Proposed) |
| TR-shipctrl-009 | ship-control-system.md | ShipControl | aimAngle computation: Vector3.Angle to target | Logic | ADR-0018 | ✅ (Proposed) |
| TR-hud-001 | ship-hud.md | HUD | Hull bar: HealthSystem.HullRatio subscription | UI | ADR-0019 | ✅ (Proposed) |
| TR-hud-002 | ship-hud.md | HUD | Weapon cooldown display synced to _fireTimer | UI | ADR-0019 | ✅ (Proposed) |
| TR-hud-003 | ship-hud.md | HUD | Soft-lock reticle via OnLockAcquired/OnLockLost | UI | ADR-0019 | ✅ (Proposed) |
| TR-starmapui-001 | star-map-ui.md | StarMapUI | UI Toolkit EventSystem for node selection, ≥48dp hotzone | UI/Input | ADR-0020 | ✅ (Proposed) |
| TR-starmapui-002 | star-map-ui.md | StarMapUI | Node color coding (PLAYER/ENEMY/NEUTRAL), fleet icons | UI | ADR-0020 | ✅ (Proposed) |
| TR-starmapui-003 | star-map-ui.md | StarMapUI | Fleet dispatch request to FleetDispatchSystem | UI | ADR-0020 | ✅ (Proposed) |
| TR-starmapui-004 | star-map-ui.md | StarMapUI | OnResourcesUpdated subscription; SimRate panel | UI | ADR-0020 | ✅ (Proposed) |

## Known Gaps

### Non-Critical (Resolved)

| TR-ID | GDD | Requirement | Resolution |
|-------|-----|-------------|------------|
| TR-hud-001 | ship-hud.md | Hull bar via HullRatio subscription | ✅ ADR-0019 Accepted |
| TR-hud-002 | ship-hud.md | Weapon cooldown display synced to _fireTimer | ✅ ADR-0019 Accepted |
| TR-hud-003 | ship-hud.md | Soft-lock reticle via events | ✅ ADR-0019 Accepted |
| TR-starmapui-001~004 | star-map-ui.md | Full StarMapUI spec | ✅ ADR-0020 Proposed |

## Superseded Requirements

The following TR-IDs from the previous (2026-04-14) review are now **superseded** because they were merged into newer TR entries during this review:

| Old TR-ID | Reason | Replaced By |
|-----------|--------|-------------|
| TR-ship-002 (old) | "ShipState state machine (5 states)" — too broad, now covered by multiple specific TRs | TR-health-001, TR-combat-001, TR-fleet-002 |
| TR-building-004 (old) | "CanAfford cross-system check" — merged into TR-building-003 | TR-building-003 |
| TR-combat-001 (old, 2026-04-14) | GDD was overwritten during this review; requirements now traced directly to ADR-0013 | TR-combat-001~006 (new) |

## History

| Date | Full Coverage % | Total TRs | Notes |
|------|----------------|-----------|-------|
| 2026-04-14 | 50% | 52 | Previous review — 26 covered, 5 partial, 21 gaps |
| 2026-04-15 | ~92% | 52 | This review — ADRs 0013-0018 Accepted; coverage improved dramatically |
| 2026-04-17 | 100% | 56 | ship-hud + ship-equipment merged to main; ADR-0019/0020 implemented |
| 2026-04-18 | 100% | 56 | ADR-0019 created (was file-missing); Accepted |
