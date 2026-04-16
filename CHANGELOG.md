# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Added
- Initial core-gameplay implementation (Stories 001-023)
- `CombatSystem` — state machine, fire rate timer, raycast hit detection
- `EnemyAIController` — 4-state AI (SPAWNING→APPROACHING→FLANKING→DYING), zero-GC physics
- `FleetDispatchSystem` — transit, cancel, arrival routing, unattended combat U-4
- `ColonyManager` — resource tick, build ship atomicity
- `BuildingSystem` — request build, production cache
- `ShipControlSystem` — physics, input, soft-lock, camera, state transitions
- `SimClock` — frame-rate independent time for strategy layer
- Full test suite — 39 unit and integration tests
- 18 Architecture Decision Records (ADRs)
- 13 Game Design Documents (GDDs)
- GitHub Actions CI (game-ci/unity-test-runner)
- ScriptableObject data assets (configs, channels)

### Fixed
- EnemyAIController: `Time.deltaTime` → `SimClock.DeltaTime` for fast-forward correctness
- EnemyAIController: static buffers → per-instance readonly (ADR-0015 compliance)
- EnemyAIController: subscribe `ShipStateChannel` for U-4 player death detection
- FleetDispatchSystem: `CloseOrder()` made idempotent
- ADR-0017: `RemoveOrder` → `CloseOrder`, orphaned cleanup description updated

---

## [1.0.0-beta] — 2026-04-16

**Initial beta release.**

> Note: v1.0.0-beta was a template-only release. Gameplay systems were added in the
> Unreleased changes above.
