# 星链霸权 (Starchain Hegemony)

> A mobile space strategy game built with Unity 6.3 LTS.

**Genre**: Real-time space combat with fleet management and territory control
**Platform**: Android (touch-first)
**Engine**: Unity 6.3 LTS | URP | C#

---

## Game Overview

Command a fleet of starships in a galaxy contested by hostile forces. Engage enemies in cockpit-based third/first-person combat, dispatch fleets across the star map, build colonies on captured nodes, and grow your fleet from a single flagship into a dominating armada.

**Core Loops**:
1. **Star Map** — Select nodes, dispatch ships, manage territory
2. **Cockpit Combat** — Pilot your ship in real-time, aim and fire to destroy enemies
3. **Fleet Management** — Build ships at colonies, manage resources, expand across the map
4. **Strategic Depth** — Unattended combat resolves automatically when NPC fleets meet enemies

---

## Project Status

| Component | Status |
|-----------|--------|
| Core Gameplay Systems | ✅ Complete (Stories 001-023) |
| Unit + Integration Tests | ✅ 39 tests passing |
| Architecture ADRs | ✅ 18 ADRs documented |
| GDD / Design Docs | ✅ 13 documents |
| Unity Scenes / Prefabs | 🔜 In Progress |
| Art Assets | 🔜 In Progress |
| Playable Build | 🔜 Pending |

**Current Stage**: Pre-Production — transitioning to Production

---

## Systems Implemented

### Combat (`src/Gameplay/CombatSystem.cs`)
- State machine: IDLE → COMBAT_ACTIVE → VICTORY / DEFEAT
- Fire rate timer: frame-rate independent via `Time.deltaTime`
- Raycast hit detection: zero-GC `Physics.RaycastNonAlloc` with pre-allocated buffer
- Win/lose detection via `HealthSystem.OnShipDying`

### Enemy AI (`src/Gameplay/EnemyAIController.cs`)
- 4-state AI: SPAWNING → APPROACHING → FLANKING → DYING
- Zero-GC physics: `OverlapSphereNonAlloc` + `RaycastNonAlloc`
- Per-instance buffers (not static — fast-forward safe)
- Subscribes to `ShipStateChannel` for U-4 path player death detection

### Fleet Dispatch (`src/Gameplay/fleet/FleetDispatchSystem.cs`)
- SimClock-driven transit: `HopProgress += SimClock.DeltaTime`
- Multi-hop paths with fractional carry-over
- CancelDispatch with return journey
- Arrival routing: ENEMY → Combat/U-4 | NEUTRAL/PLAYER → DOCKED
- Idempotent `CloseOrder()` prevents double-close

### Colony & Building (`src/Gameplay/ColonyManager.cs`, `BuildingSystem.cs`)
- Resource tick: ore (clamped) + energy per production cycle
- Atomic `BuildShip()` with rollback on failure
- `RequestBuild()` with `RefreshProductionCache()`

### Ship Control (`src/Gameplay/ShipControlSystem.cs`)
- Physics: `Rigidbody2D.AddForce` thrust, `MoveRotation` turn, soft speed clamp
- Input: dead zone (0.08), aim assist coefficient (0.5)
- Soft-lock: `EnemySystem.GetNearestEnemyInRange()`, `FireRequested` event
- Camera: `CameraRig` with SmoothDamp third-person / hard-bind first-person
- State transitions: IN_COCKPIT → IN_COMBAT → DOCKED / DESTROYED

### Data Layer (`src/Data/`)
- `ShipDataModel` — hull, state, blueprint, events
- `StarMapData` — nodes, edges, BFS pathfinding
- `GameDataManager` — ship registry, active cockpit tracking
- `ShipBlueprintRegistry` — blueprint lookup

---

## Architecture

All systems documented in `docs/architecture/`:

| ADR | Subject |
|-----|---------|
| ADR-0001 | Scene Management |
| ADR-0002 | Event Communication (GameEvent<T> channels) |
| ADR-0003 | Input System |
| ADR-0004 | Data Model |
| ADR-0007 | Overlay Rendering |
| ADR-0012 | SimClock (frame-rate independent time) |
| ADR-0013 | Combat System |
| ADR-0014 | Health System |
| ADR-0015 | Enemy System |
| ADR-0016 | Colony & Building |
| ADR-0017 | Fleet Dispatch |
| ADR-0018 | Ship Control System |

---

## Testing

Tests live in `tests/` with 39 unit and integration tests:

```
tests/
├── unit/
│   ├── combat/       # Fire rate, raycast, state machine
│   ├── enemy/        # AI state, physics queries
│   ├── fleet/        # Transit, cancel, unattended combat
│   ├── health/       # Apply damage, death sequence
│   ├── shipctrl/     # Physics, input, soft lock, state
│   └── ...
├── integration/
│   ├── fleet/        # Enemy arrival routing
│   └── ...
└── smoke/
```

Run via GitHub Actions CI (`game-ci/unity-test-runner@v4`) on every push.

---

## Tech Stack

| Layer | Technology |
|-------|------------|
| Engine | Unity 6.3 LTS |
| Language | C# |
| Rendering | Universal Render Pipeline (URP) |
| Physics | PhysX (Unity 6 default) |
| UI | UI Toolkit (runtime) |
| Input | New Input System |
| Asset Loading | Unity Addressables |
| Testing | Unity Test Framework (NUnit) |
| CI | GitHub Actions + game-ci |

---

## Directory Structure

```
src/
├── Channels/         # ScriptableObject event channels
├── Data/            # Data models, config, blueprints
├── Gameplay/        # Core systems (Combat, Enemy, Fleet, Colony, Ship)
├── Input/           # Input processing
├── Scene/           # View layer, camera, overlay
└── UI/              # HUD, star map UI

assets/data/
├── channels/        # Channel SO instances
└── config/          # ResourceConfig, ShipBlueprint assets

design/
├── gdd/              # Game design documents
└── ux/               # UX specifications

docs/architecture/    # Architecture decision records
production/epics/     # Story manifests (001-023)
tests/                # Unit and integration tests
```

---

## Getting Started

### Prerequisites

- Unity 6.3 LTS
- Git
- (Optional) GitHub account for CI

### Clone & Open

```bash
git clone https://github.com/Donchitos/Claude-Code-Game-Studios.git
# Open in Unity Hub → open project folder
```

### Run Tests

Tests run automatically via GitHub Actions on push. To run locally:

```bash
# In Unity: Window → General → Test Runner → Run All
```

---

## Contributing

See [CLAUDE.md](CLAUDE.md) for development workflow, agent coordination rules, and coding standards.

---

## License

MIT License. See [LICENSE](LICENSE).
