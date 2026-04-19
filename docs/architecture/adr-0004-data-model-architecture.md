# ADR-0004: Data Model Architecture

## Status
Accepted

## Date
2026-04-14

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Core / Data |
| **Knowledge Risk** | LOW — data model patterns are stable across Unity versions |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `docs/engine-reference/unity/deprecated-apis.md` |
| **Post-Cutoff APIs Used** | `UniTask.WaitForSeconds(ignoreTimeScale: true)` — requires UniTask 2.x; equivalent to `WaitForSecondsRealtime`. Lock to `com.cysharp.unitask@2.5.x` in Package Manager. |
| **Verification Required** | (1) Confirm `CanAfford()` on ResourceConfig SO is a pure function (no runtime state refs). (2) Confirm UniTask `ignoreTimeScale: true` compiles and behaves as realtime in Play Mode. (3) Confirm BFS < 1ms on target Android device (call from profiler once starmap has >= 15 nodes). |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0001 (MasterScene topology — GameDataManager must live in MasterScene); ADR-0002 (SO Channel pattern — ColonySystem tick events use Tier 2 C# events within StarMapScene) |
| **Enables** | ADR-0013/0014/0015 (Combat/Health/Enemy System — needs ShipBlueprintRegistry and StarMapData) |
| **Blocks** | Core Epic (data layer must be decided before BuildingSystem, ColonySystem, FleetDispatch stories can begin) |
| **Ordering Note** | ADR-0001 and ADR-0002 must both be Accepted before implementation begins |

## Context

### Problem Statement

Thirteen technical requirements from six GDDs (resource-system, star-map-system,
ship-system, building-system, colony-system, fleet-dispatch-system) have no
architectural coverage. All require decisions about: how read-only config data
is structured and accessed, how runtime game state is owned and located, and
how the production tick timer is implemented in a time-scale-independent way.
These decisions must be made before any Core Epic story can begin.

### Constraints

- All runtime data must be accessible from MasterScene (ADR-0001: cross-scene
  data lives in MasterScene, not in scene-specific MonoBehaviours)
- No cross-scene direct references (ADR-0001 forbidden pattern)
- Production tick must be independent of `Time.timeScale` (GDD requirement:
  colony output continues even if game is conceptually "paused" for UI)
- Inspector-tunable config (data-driven design principle from coding standards)
- Android mobile performance: GC allocations minimised in hot tick loop

### Requirements

- Must provide read-only config for: ResourceConfig (ORE_CAP, production rates,
  build costs, storage caps), ShipBlueprint (MaxHull, ThrustPower, TurnSpeed,
  WeaponSlots, BuildCost, RequiredShipyardTier, HangarCapacity)
- Must provide runtime data structures for: StarMapData (graph G=(V,E) with
  NodeType, OwnershipState, FogState, ShipyardTier, dockedFleet, buildings),
  DispatchOrderRegistry (fleet in-transit tracking), BuildingRegistry
- Must provide a single authoritative data owner visible to all scenes
- Must implement colony production tick at 1s realtime intervals
- Must implement BFS shortest-hop pathfinding over StarMapData adjacency graph

---

## Decision

### Two-Layer Data Architecture

Data is split into two strictly separated layers:

**Layer 1 — Read-Only Config (ScriptableObject)**
Static game constants that define "how the game works". Set by designers in
the Inspector, never written at runtime. Loaded once at startup.

**Layer 2 — Runtime State (C# objects owned by GameDataManager)**
Mutable game state representing "what is happening right now". Lives in
MasterScene, accessible from any scene via `GameDataManager.Instance`.

---

### Layer 1: Read-Only Config ScriptableObjects

#### ResourceConfig

```csharp
[CreateAssetMenu(menuName = "Config/ResourceConfig")]
public class ResourceConfig : ScriptableObject
{
    [Header("Storage")]
    public int OreCap = 500;          // ORE_CAP — validate > 0 on OnValidate()

    [Header("Base Output")]
    public float BaseColonyOreOutput;       // ore/sec from bare colony
    public float BaseColonyEnergyOutput;    // energy/sec from bare colony

    [Header("Building Rates")]
    public BuildingRateEntry[] buildingRates;  // indexed by BuildingType

    [Header("Build Costs")]
    public BuildCostEntry[] buildCosts;        // indexed by BuildingType

    // Pure functions — no runtime state refs
    public float GetProductionRate(BuildingType type, ResourceType res) { ... }
    public (int ore, int energy) GetBuildCost(BuildingType type) { ... }
    public int   GetStorageCap(ResourceType type) { ... }
    public bool  CanAfford(int currentOre, int currentEnergy, BuildingType type) { ... }

    private void OnValidate()
    {
        if (OreCap <= 0) Debug.LogError("[ResourceConfig] ORE_CAP must be > 0");
    }
}
```

Asset path: `Assets/Data/Config/ResourceConfig.asset`

#### ShipBlueprintRegistry

```csharp
[CreateAssetMenu(menuName = "Config/ShipBlueprintRegistry")]
public class ShipBlueprintRegistry : ScriptableObject
{
    public ShipBlueprint[] blueprints;

    public ShipBlueprint GetBlueprint(string blueprintId)
        => Array.Find(blueprints, b => b.BlueprintId == blueprintId);
}

[Serializable]
public class ShipBlueprint
{
    public string  BlueprintId;          // "generic_v1", "carrier_v1"
    public string  DisplayName;
    public float   MaxHull;
    public float   ThrustPower;
    public float   TurnSpeed;
    public int     WeaponSlots;
    public int     BuildCostOre;
    public int     BuildCostEnergy;
    public int     RequiredShipyardTier; // 1 or 2
    public int     HangarCapacity;       // 0 for non-carriers; snapshot at departure
}
```

Asset path: `Assets/Data/Config/ShipBlueprintRegistry.asset`

---

### Layer 2: Runtime State — GameDataManager

`GameDataManager` is the sole MasterScene MonoBehaviour that owns all runtime
game state. It is the project's one justified singleton (MasterScene is always
loaded; there is exactly one instance).

```csharp
public class GameDataManager : MonoBehaviour
{
    // ── Config refs (set in Inspector) ─────────────────────────────────────
    [SerializeField] public ResourceConfig        ResourceConfig;
    [SerializeField] public ShipBlueprintRegistry BlueprintRegistry;

    // ── Runtime state (owned, newed in Awake) ──────────────────────────────
    public StarMapData          StarMap          { get; private set; }
    public DispatchOrderRegistry FleetRegistry   { get; private set; }
    public BuildingRegistry      Buildings       { get; private set; }

    // ── Singleton accessor ─────────────────────────────────────────────────
    public static GameDataManager Instance { get; private set; }

    private void Awake()
    {
        Instance    = this;
        StarMap     = new StarMapData();
        FleetRegistry = new DispatchOrderRegistry();
        Buildings   = new BuildingRegistry();
    }
}
```

---

#### StarMapData

```csharp
public class StarMapData
{
    public List<StarNode> Nodes { get; } = new();
    public List<StarEdge> Edges { get; } = new();

    // Adjacency index: nodeId → list of neighbour nodeIds
    private readonly Dictionary<string, List<string>> _adjacency = new();

    public void BuildAdjacency() { /* populate _adjacency from Edges */ }
    public IReadOnlyList<string> GetNeighbours(string nodeId) => _adjacency[nodeId];
    public StarNode GetNode(string nodeId) { ... }
}

public class StarNode
{
    public string          NodeId;
    public Vector2         Position;          // UI coordinates (dp)
    public NodeType        Type;              // HOME_BASE | NEUTRAL | ENEMY_BASE
    public OwnershipState  Ownership;         // PLAYER | ENEMY | NEUTRAL
    public FogState        Fog;              // EXPLORED | UNEXPLORED
    public string          DockedFleetId;     // null if empty
    public int             ShipyardTier;      // 0 = none, 1 = basic, 2 = upgraded
    public List<string>    BuildingIds;       // FK → BuildingRegistry
    public float           OreMultiplier;     // base 1.0; modified by node type
}

public class StarEdge
{
    public string FromNodeId;
    public string ToNodeId;
}
```

---

#### DispatchOrderRegistry + BFS

```csharp
public class DispatchOrderRegistry
{
    // Key: shipId — only ships with ShipState = IN_TRANSIT have an entry
    private readonly Dictionary<string, DispatchOrder> _orders = new();

    public bool TryGetOrder(string shipId, out DispatchOrder order)
        => _orders.TryGetValue(shipId, out order);

    public void Register(DispatchOrder order)   => _orders[order.ShipId] = order;
    public void Deregister(string shipId)        => _orders.Remove(shipId);
    public IEnumerable<DispatchOrder> All        => _orders.Values;
}

public class DispatchOrder
{
    public string       ShipId;
    public List<string> LockedPath;       // BFS-computed, snapshot at dispatch
    public int          CurrentHopIndex;  // index into LockedPath (from node)
    public float        HopProgress;      // seconds into current hop [0, FLEET_TRAVEL_TIME]
    public string       DestNodeId;
    public bool         IsCancelled;      // if true, traversing LockedPath in reverse
}

// ── BFS Pathfinding (main thread, pure C#) ────────────────────────────────
public static class StarMapPathfinder
{
    /// <summary>
    /// Returns shortest-hop path from start to dest (inclusive), or null if
    /// no path exists. Tie-broken by lexicographic nodeId order.
    /// Blocked nodes (DOCKED friendly ship) are treated as impassable
    /// except for the destination node.
    /// </summary>
    public static List<string> FindPath(
        StarMapData map, string startId, string destId,
        Func<string, bool> isBlocked)
    {
        // Standard BFS — O(V+E), <= 20 nodes: < 1ms on any device
        var parent  = new Dictionary<string, string>();
        var queue   = new Queue<string>();
        queue.Enqueue(startId);
        parent[startId] = null;

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == destId) break;

            var neighbours = map.GetNeighbours(current)
                                .OrderBy(n => n)         // lexicographic tie-break
                                .ToList();
            foreach (var next in neighbours)
            {
                if (parent.ContainsKey(next)) continue;
                if (next != destId && isBlocked(next))   continue;
                parent[next] = current;
                queue.Enqueue(next);
            }
        }

        if (!parent.ContainsKey(destId)) return null;  // no path

        // Reconstruct
        var path = new List<string>();
        for (var n = destId; n != null; n = parent[n])
            path.Add(n);
        path.Reverse();
        return path;
    }
}
```

---

#### BuildingRegistry

```csharp
public class BuildingRegistry
{
    private readonly Dictionary<string, BuildingInstance> _all      = new(); // key: InstanceId
    private readonly Dictionary<string, List<string>>    _byNode   = new(); // key: NodeId

    public BuildingInstance Get(string instanceId) => _all[instanceId];
    public IReadOnlyList<string> GetByNode(string nodeId)
        => _byNode.TryGetValue(nodeId, out var list) ? list : Array.Empty<string>();

    public void Add(BuildingInstance b)    { _all[b.InstanceId] = b; /* update _byNode */ }
    public void Remove(string instanceId)  { /* remove from both indexes */ }
}

public class BuildingInstance
{
    public string       InstanceId;    // e.g. "bld_001"
    public BuildingType Type;
    public string       NodeId;
    public bool         IsActive;      // MVP: always true
}
```

---

### Production Tick (ColonySystem)

Colony resource accumulation runs in StarMapScene as a UniTask coroutine:

```csharp
private async UniTaskVoid RunProductionLoop(CancellationToken ct)
{
    while (!ct.IsCancellationRequested)
    {
        // ignoreTimeScale: true → realtime, independent of Time.timeScale
        // (GDD requirement: colony output continues regardless of pause state)
        await UniTask.WaitForSeconds(1f, ignoreTimeScale: true,
                                    cancellationToken: ct);
        TickProduction();
    }
}

private void TickProduction()
{
    var data = GameDataManager.Instance;
    float netOre    = CalculateNetOreProduction(data);   // sum building rates
    float netEnergy = CalculateNetEnergyProduction(data);

    _oreCurrent    = Mathf.Clamp(_oreCurrent    + netOre,    0, data.ResourceConfig.OreCap);
    _energyCurrent = Mathf.Clamp(_energyCurrent + netEnergy, 0, float.MaxValue);

    // Tier 2 C# event — same-scene consumers (HUD, BuildingUI)
    OnResourcesUpdated?.Invoke(new ResourceSnapshot(_oreCurrent, _energyCurrent));
}
```

---

### Architecture Diagram

```
MasterScene (always loaded)
├── GameDataManager
│   ├── [SerializeField] ResourceConfig        (SO asset — read-only)
│   ├── [SerializeField] ShipBlueprintRegistry (SO asset — read-only)
│   ├── StarMapData        (C# — runtime graph G=(V,E))
│   ├── DispatchOrderRegistry (C# — in-transit fleet orders)
│   └── BuildingRegistry   (C# — all BuildingInstance records)
│
StarMapScene (toggles)
└── ColonySystem
    ├── Reads: GameDataManager.Instance.StarMap / ResourceConfig
    ├── Writes: own _oreCurrent / _energyCurrent
    └── UniTask loop — WaitForSeconds(1f, ignoreTimeScale:true)

CockpitScene (toggles)
└── (reads GameDataManager.Instance for blueprint lookups, hull sync)
```

---

### Key Interfaces

| Interface | Owner | Consumers |
|-----------|-------|-----------|
| `GameDataManager.Instance.ResourceConfig` | GameDataManager | ColonySystem, BuildingSystem, ShipSystem |
| `GameDataManager.Instance.BlueprintRegistry.GetBlueprint(id)` | GameDataManager | ShipSystem, ColonySystem |
| `GameDataManager.Instance.StarMap.GetNode(id)` | GameDataManager | StarMapUI, FleetSystem, ColonySystem |
| `GameDataManager.Instance.StarMap.GetNeighbours(id)` | GameDataManager | StarMapPathfinder |
| `GameDataManager.Instance.FleetRegistry` | GameDataManager | FleetDispatchSystem |
| `GameDataManager.Instance.Buildings` | GameDataManager | BuildingSystem, ColonySystem |
| `StarMapPathfinder.FindPath(map, start, dest, isBlocked)` | Static util | FleetDispatchSystem |
| `ColonySystem.OnResourcesUpdated(ResourceSnapshot)` | ColonySystem (StarMapScene) | ShipHUD, BuildingUI (Tier 2 C# event) |

---

## Alternatives Considered

### Alternative A: All Data as ScriptableObjects (including runtime state)
- **Description**: Use SO as runtime containers, mutate fields at play time
- **Pros**: Inspector visibility of live values during debugging
- **Cons**: SO mutations persist to disk in Editor; PlayMode changes survive
  scene reload; breaks SO read-only contract; causes hard-to-find test bugs
- **Rejection Reason**: SO mutation at runtime is an anti-pattern. Runtime state
  belongs in plain C# objects.

### Alternative B: Data Per-Scene (StarMapData in StarMapScene, etc.)
- **Description**: Each scene owns its data; cross-scene sync via ADR-0002 events
- **Pros**: Looser coupling between scenes
- **Cons**: StarMapScene unloading destroys its data; CockpitScene cannot query
  star map adjacency for fleet dispatch without a cross-scene read (violates
  ADR-0001 forbidden pattern); data sync doubles event volume
- **Rejection Reason**: ADR-0001 established MasterScene as the authoritative
  data holder. Moving runtime data to StarMapScene contradicts this stance.

### Alternative C: Addressables for Config
- **Description**: Load ResourceConfig and blueprints via Addressables at runtime
- **Pros**: Hot-swappable config; supports remote config updates
- **Cons**: Async loading complicates startup sequence; overkill for MVP;
  Addressables adds dependency and complexity
- **Rejection Reason**: Inspector SerializeField reference is synchronous,
  simpler, and sufficient for a single-player mobile game.

---

## Consequences

### Positive
- Single authoritative data location: any scene can call `GameDataManager.Instance`
  without cross-scene references (satisfies ADR-0001 constraint)
- Config is Inspector-tunable with zero code changes (data-driven principle)
- `CanAfford()` and all config lookups are pure functions — fully unit-testable
  without Unity scene loading
- BFS is deterministic (lexicographic tie-breaking) — testable with fixed graphs
- Production tick is realtime-stable regardless of `Time.timeScale`

### Negative
- `GameDataManager.Instance` is a static singleton — tests that need a fresh
  GameDataManager must construct one manually or use a test scene
- `Dictionary<string, DispatchOrder>` allocates on heap; GC pressure during
  frequent fleet dispatches (acceptable for MVP, monitor in profiler)
- `List<string> LockedPath` allocates on dispatch — 1 allocation per dispatch
  (not per-frame), acceptable

### Risks
- **R-1: UniTask `ignoreTimeScale` parameter availability**
  Mitigation: lock `com.cysharp.unitask@2.5.x` in Package Manager manifest;
  verify in first play session that tick fires every ~1 real second.
- **R-2: GameDataManager.Instance accessed before Awake()**
  Mitigation: GameDataManager is on a MasterScene root GameObject with
  Script Execution Order set to -100 (earliest). All scene-specific systems
  in StarMapScene/CockpitScene load after MasterScene.
- **R-3: StarMapData initialised empty — must be populated before use**
  Mitigation: StarMapInitialiser (MasterScene) runs in Start() after GameDataManager
  Awake(), populates StarMapData with MVP 5-node diamond layout, then calls
  BuildAdjacency(). Systems that query StarMap must not do so before Start().
- **R-4: GC pressure from LockedPath List allocation**
  Mitigation: MVP fleet count is <= 5 simultaneous dispatches; allocation is
  one-time per dispatch, not per-frame. If fleet count grows, pool LockedPath
  lists via `List<string>` object pool.

---

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| resource-system.md | ResourceConfig SO read-only config layer | `ResourceConfig : ScriptableObject` — Inspector-editable, referenced via SerializeField |
| resource-system.md | ore_accumulation clamp formula per tick | `TickProduction()` clamps with `Mathf.Clamp(ore + net, 0, OreCap)` |
| resource-system.md | ORE_CAP configurable constant, validate > 0 | `ResourceConfig.OreCap` field; `OnValidate()` logs error if <= 0 |
| star-map-system.md | Undirected graph G=(V,E) data structure | `StarMapData` with `List<StarNode>` + `List<StarEdge>` + adjacency index |
| star-map-system.md | Visibility IsVisible formula (GraphDistance) | BFS on `StarMapData._adjacency` — distance computable from same graph |
| star-map-system.md | MVP fixed 5-node diamond layout | `StarMapInitialiser` populates `StarMapData` at Start() with static config |
| ship-system.md | ShipBlueprint read-only config | `ShipBlueprintRegistry : ScriptableObject` with `GetBlueprint(id)` |
| ship-system.md | carrier_v1 blueprint (HangarCapacity snapshot) | `ShipBlueprint.HangarCapacity`; snapshot logic at departure is in FleetDispatchSystem |
| building-system.md | BuildingInstance data model | `BuildingInstance` class (InstanceId, BuildingType, NodeId, IsActive) in `BuildingRegistry` |
| building-system.md | ShipyardTier int field replaces HasShipyard | `StarNode.ShipyardTier : int` — 0/1/2; maintained by BuildingSystem |
| colony-system.md | Production tick WaitForSecondsRealtime | `UniTask.WaitForSeconds(1f, ignoreTimeScale: true)` in ColonySystem |
| fleet-dispatch-system.md | BFS pathfinding on star graph | `StarMapPathfinder.FindPath()` — O(V+E) BFS with lexicographic tie-break |
| fleet-dispatch-system.md | FLEET_TRAVEL_TIME=3s per hop | `DispatchOrder.HopProgress` advanced by `Time.deltaTime` in FleetSystem.Update() |
| fleet-dispatch-system.md | Cancel movement returns symmetrically | `DispatchOrder.IsCancelled = true` reverses traversal direction |
| fleet-dispatch-system.md | Dispatch precondition D-1 | FleetDispatchSystem checks: ShipState==DOCKED, PLAYER_OWNED node, EXPLORED, no conflict |

---

## Performance Implications
- **CPU**: BFS O(V+E) <= 20 nodes < 0.1ms; production tick 1 Hz, negligible;
  `GetProductionRate()` is array lookup O(BuildingType count)
- **Memory**: `GameDataManager` holds all runtime data — estimated ~50KB for a
  full MVP graph (20 nodes, 5 buildings, 5 fleets); well within budget
- **GC**: 1 `DispatchOrder` allocation per fleet dispatch (not per-frame);
  1 `List<string>` for LockedPath per dispatch; zero GC in tick loop
- **Load Time**: SO assets loaded synchronously at scene load; estimated < 1ms

---

## Migration Plan

First implementation (no existing code to migrate):

1. Create `Assets/Data/Config/` directory
2. Create `ResourceConfig.asset` in Inspector; populate from GDD tuning knobs
3. Create `ShipBlueprintRegistry.asset`; add `generic_v1` and `carrier_v1` entries
4. Create `GameDataManager.cs` in MasterScene; wire SO refs in Inspector
5. Create `StarMapInitialiser.cs` in MasterScene; populate 5-node diamond layout
6. Implement `ColonySystem.cs` in StarMapScene with UniTask production loop
7. Implement `FleetDispatchSystem.cs` with `StarMapPathfinder`

> **Note on ColonySystem production tick**: The `UniTask.WaitForSeconds` code sample shown above is illustrative of the realtime timing concept. The **canonical production tick implementation** is in **ADR-0016 (Colony & Building System)** — it uses `Update()` + `_tickAccumulator` driven by `SimClock.Instance.DeltaTime`. When implementing ColonySystem, follow ADR-0016's approach, not the UniTask sample in this document.

---

## Validation Criteria

- **AC-DATA-01**: `ResourceConfig.CanAfford(400, 20, BuildingType.Shipyard)` returns
  correct bool based on GDD build costs — verifiable as pure unit test
  (EditMode, no scene required)
- **AC-DATA-02**: BFS on 5-node diamond graph finds correct shortest path with
  deterministic tie-breaking — verifiable as pure unit test
- **AC-DATA-03**: Production tick fires at ~1s realtime intervals when
  `Time.timeScale = 0` (UI pause simulation) — verifiable in PlayMode test
- **AC-DATA-04**: `GameDataManager.Instance` is non-null in StarMapScene Start()
  callbacks (Script Execution Order respected)
- **AC-DATA-05**: `StarNode.ShipyardTier` increments from 0→1 on Shipyard build,
  1→2 on ShipyardUpgrade — verifiable via BuildingSystem integration test

---

## Related Decisions
- ADR-0001 (Scene Management) — establishes MasterScene as data authority;
  GameDataManager directly implements this contract
- ADR-0002 (Event/Communication) — ColonySystem.OnResourcesUpdated uses Tier 2
  C# event (same scene); cross-scene fleet events use Tier 1 SO Channel
- ADR-0003 (Input System) — no direct interaction; both systems are in MasterScene
- design/gdd/resource-system.md — ORE_CAP, production formulas, ResourceConfig interface
- design/gdd/star-map-system.md — graph structure, IsVisible formula, MVP 5-node layout
- design/gdd/ship-system.md — ShipBlueprint fields, carrier snapshot rule
- design/gdd/building-system.md — BuildingInstance schema, ShipyardTier field
- design/gdd/colony-system.md — production tick timing, realtime requirement
- design/gdd/fleet-dispatch-system.md — BFS algorithm, DispatchOrder structure
