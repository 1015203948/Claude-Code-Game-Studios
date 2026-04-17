# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Added
- **Ship HUD (Story 024)** — hull bar with color thresholds (green/warning/critical), weapon
  cooldown bar, speed indicator, combat indicator with 2-second fade, soft-lock reticle,
  ViewLayer visibility (COCKPIT shows / STARMAP hides)
- **StarMap UI (Story 024)** — Painter2D node rendering (shape/color by type × fogState),
  edge rendering, zoom/pan [0.5×–2×], interaction state machine (IDLE→NODE_SELECTED→
  SHIP_SELECTED→DISPATCH_CONFIRM), fleet icon pool, resource corner, dispatch flow
- **Ship Equipment System (Story 026)** — SlotType/ModuleTier enums, EquipmentModule and
  HullBlueprint ScriptableObjects, ShipDataModel equipment fields, ShipEquipmentSystem
  equip/unequip, ShipLootTable weighted random drop, LootDropSystem on enemy death
- **Inventory & Module UI (Story 026)** — InventoryUI module list, ModuleSelectionPanel
  slot assignment, ShipEquipmentUI hull/slot overview, 16 module assets across
  Weapon/Engine/Shield/Cargo categories, 3 hull blueprint assets (Fighter/Cruiser/Destroyer)
- Initial core-gameplay implementation (Stories 001-023)
- `CombatSystem` — state machine, fire rate timer, raycast hit detection
- `EnemyAIController` — 4-state AI (SPAWNING→APPROACHING→FLANKING→DYING), zero-GC physics
- `FleetDispatchSystem` — transit, cancel, arrival routing, unattended combat U-4
- `ColonyManager` — resource tick, build ship atomicity
- `BuildingSystem` — request build, production cache
- `ShipControlSystem` — physics, input, soft-lock, camera, state transitions
- `SimClock` — frame-rate independent time for strategy layer
- Full test suite — 57 unit and integration tests (ship-hud + ship-equipment + prior)
- 18 Architecture Decision Records (ADRs)
- 13 Game Design Documents (GDDs)
- GitHub Actions CI (game-ci/unity-test-runner)
- ScriptableObject data assets (configs, channels)
- Unity project scaffolding — MasterScene, StarMapScene, CockpitScene
- Ship prefabs — PlayerShip, EnemyShip, ShipHUD, StarMapUI
- Materials — ShipStandard, StarMapNode
- Cockpit controls prototype — DualJoystickInput, JoystickVisual, ShipController
- Unity 6.3 LTS engine reference — breaking changes, best practices, deprecated APIs
- Claude + Codex collaboration rules in coordination-rules.md
- state_ownership section in architecture.yaml (ADR-0017)
- 5 UX P0 conflicts documented in ux-designer agent memory

### Fixed
- EnemyAIController: `Time.deltaTime` → `SimClock.DeltaTime` for fast-forward correctness
- EnemyAIController: static buffers → per-instance readonly (ADR-0015 compliance)
- EnemyAIController: subscribe `ShipStateChannel` for U-4 player death detection
- FleetDispatchSystem: `CloseOrder()` made idempotent
- ADR-0017: `RemoveOrder` → `CloseOrder`, orphaned cleanup description updated
- Canvas scale zero → {1,1,1} in CockpitScene and StarMapScene
- Missing `PlayerShip` tag added to TagManager.asset
- `Assert.Pass` → `Assert.DoesNotThrow` in cancel_dispatch_test

---

## [1.0.0-beta] — 2026-04-16

**Initial beta release.**

> Note: v1.0.0-beta was a template-only release. Gameplay systems were added in the
> Unreleased changes above.
