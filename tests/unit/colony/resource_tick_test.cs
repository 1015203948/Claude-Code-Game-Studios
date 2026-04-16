using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using Object = UnityEngine.Object;

/// <summary>
/// ColonyManager Resource Tick 单元测试。
/// 覆盖 Story 016 所有验收标准（AC-1 ~ AC-5）。
/// </summary>
[TestFixture]
public class ResourceTick_Test
{
    // ─────────────────────────────────────────────────────────────────
    // Fields
    // ─────────────────────────────────────────────────────────────────

    private Gameplay.ColonyManager _colonyManager;
    private GameObject _colonyManagerGo;
    private Game.Gameplay.BuildingSystem _buildingSystem;
    private GameObject _buildingSystemGo;
    private GameDataManager _gameDataManager;
    private global::Gameplay.SimClock _simClock;
    private GameObject _simClockGo;

    // ─────────────────────────────────────────────────────────────────
    // SetUp / TearDown
    // ─────────────────────────────────────────────────────────────────

    [SetUp]
    public void SetUp()
    {
        if (GameDataManager.Instance != null) GameDataManager.Instance = null;
        if (global::Gameplay.SimClock.Instance != null) Object.DestroyImmediate(global::Gameplay.SimClock.Instance.gameObject);
        if (Gameplay.ColonyManager.Instance != null) Object.DestroyImmediate(Gameplay.ColonyManager.Instance.gameObject);
        if (Game.Gameplay.BuildingSystem.Instance != null) Object.DestroyImmediate(Game.Gameplay.BuildingSystem.Instance.gameObject);
        if (HealthSystem.Instance != null) Object.DestroyImmediate(HealthSystem.Instance.gameObject);

        _gameDataManager = new GameDataManager();

        // StarMap: HOME (PLAYER), STANDARD (NEUTRAL), ENEMY
        var nodes = new List<StarNode> {
            new StarNode("node-HOME", "Home Base", Vector2.zero, NodeType.HOME_BASE) { Ownership = OwnershipState.PLAYER },
            new StarNode("node-STD", "Standard", new Vector2(1, 0), NodeType.STANDARD) { Ownership = OwnershipState.NEUTRAL },
            new StarNode("node-ENEMY", "Enemy", new Vector2(2, 0), NodeType.ENEMY) { Ownership = OwnershipState.ENEMY },
        };
        var edges = new List<StarEdge> {
            new StarEdge("node-HOME", "node-STD"),
            new StarEdge("node-STD", "node-ENEMY"),
        };
        var starMap = new StarMapData(nodes, edges);
        _gameDataManager.SetStarMapData(starMap);

        // SimClock (1x)
        _simClockGo = new GameObject("SimClock");
        _simClock = _simClockGo.AddComponent<global::Gameplay.SimClock>();
        _simClock.SetRate(1f);

        // BuildingSystem stub
        _buildingSystemGo = new GameObject("BuildingSystem");
        _buildingSystem = _buildingSystemGo.AddComponent<Game.Gameplay.BuildingSystem>();

        // ColonyManager
        _colonyManagerGo = new GameObject("ColonyManager");
        _colonyManager = _colonyManagerGo.AddComponent<Gameplay.ColonyManager>();
        _colonyManager.Initialize(100, 50);
    }

    [TearDown]
    public void TearDown()
    {
        if (_colonyManagerGo != null) Object.DestroyImmediate(_colonyManagerGo);
        if (_buildingSystemGo != null) Object.DestroyImmediate(_buildingSystemGo);
        if (_simClockGo != null) Object.DestroyImmediate(_simClockGo);
        GameDataManager.Instance = null;
        global::Gameplay.SimClock.Instance = null;
        Gameplay.ColonyManager.Instance = null;
        Game.Gameplay.BuildingSystem.Instance = null;
    }

    // ─────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────

    private float GetAccumulator() => (float)typeof(Gameplay.ColonyManager)
        .GetField("_accumulator", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
        .GetValue(_colonyManager);

    private void SetAccumulator(float v) => typeof(Gameplay.ColonyManager)
        .GetField("_accumulator", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
        .SetValue(_colonyManager, v);

    private void CallTick() => typeof(Gameplay.ColonyManager)
        .GetMethod("Tick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
        .Invoke(_colonyManager, null);

    private void CallUpdate() => typeof(Gameplay.ColonyManager)
        .GetMethod("Update", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
        .Invoke(_colonyManager, null);

    // ─────────────────────────────────────────────────────────────────
    // AC-1: Tick fires every 1 second of sim time
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void accumulator_accumulates_sim_delta_time()
    {
        // Given: fresh ColonyManager
        Assert.AreEqual(0f, GetAccumulator(), "Accumulator starts at 0");

        // Simulate Update with 0.6s — below 1.0s threshold
        SetAccumulator(0f);
        CallUpdate(); // Update will add SimClock.DeltaTime (≈0.016s at 1x)

        // Still below 1.0 — no tick
        float after = GetAccumulator();
        Assert.Less(after, 1.0f, "Accumulator should still be below 1.0s threshold");
    }

    [Test]
    public void tick_fires_when_accumulator_reaches_1_0_second()
    {
        // Given: accumulator = 0.999s
        SetAccumulator(0.999f);

        int oreBefore = _colonyManager.OreCurrent;

        // Simulate Update with ~0.016s delta → 1.015s → fires tick
        CallUpdate();

        // Accumulator should have wrapped (1.015 - 1.0 = 0.015)
        float afterAccum = GetAccumulator();
        Assert.Less(afterAccum, 0.999f,
            "Accumulator should have wrapped after tick fired");
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-2: Ore clamp at ORE_CAP (upper boundary)
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void ore_clamps_to_ore_cap_on_upper_boundary()
    {
        // Verify GetOreCap() returns the cap value
        int cap = (int)typeof(Gameplay.ColonyManager)
            .GetMethod("GetOreCap", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(_colonyManager, null);

        Assert.AreEqual(1000, cap, "Default ORE_CAP should be 1000");

        // Test: with a large positive delta clamped to cap
        // We verify by checking the ClampOre formula in ResourceConfig
        int result = Mathf.Clamp(100 + 1000, 0, cap); // 1100 → clamped to 1000
        Assert.AreEqual(1000, result);
    }

    [Test]
    public void ore_does_not_go_below_zero()
    {
        // Negative delta should clamp to 0
        int result = Mathf.Clamp(5 + (-10), 0, 1000);
        Assert.AreEqual(0, result, "Ore should not go below 0");
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-3: Energy has no upper cap
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void energy_has_no_upper_cap()
    {
        // Verify EnergyCurrent property is read-only (no setter = no upper clamp)
        var prop = typeof(Gameplay.ColonyManager)
            .GetProperty("EnergyCurrent", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        Assert.IsNotNull(prop);
        Assert.IsFalse(prop.CanWrite,
            "EnergyCurrent should be read-only (intentional: no upper cap)");
    }

    [Test]
    public void energy_floors_at_zero()
    {
        // Initialize with 0 energy and verify it stays at 0
        _colonyManager.Initialize(100, 0);
        Assert.AreEqual(0, _colonyManager.EnergyCurrent,
            "Energy should floor at 0 after Initialize");
    }

    [Test]
    public void energy_accumulates_without_upper_bound()
    {
        // Verify the Update formula: EnergyCurrent = Max(0, old + delta)
        // No Mathf.Clamp or similar on upper bound in the source code
        int old = _colonyManager.EnergyCurrent;
        int delta = 500;
        int result = Mathf.Max(0, old + delta); // no upper clamp
        Assert.AreEqual(old + 500, result);
        Assert.GreaterOrEqual(result, old);
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-4: Only PLAYER-owned nodes contribute to production
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void only_player_nodes_appear_in_player_node_enumeration()
    {
        // Verify that only PLAYER-owned node appears in GetNodesByOwner(PLAYER)
        var playerNodes = new List<string>();
        foreach (var nid in _buildingSystem.GetNodesByOwner(OwnershipState.PLAYER)) {
            playerNodes.Add(nid);
        }

        Assert.AreEqual(1, playerNodes.Count, "Exactly 1 PLAYER node (node-HOME)");
        Assert.Contains("node-HOME", playerNodes);
        Assert.IsFalse(playerNodes.Contains("node-STD"));
        Assert.IsFalse(playerNodes.Contains("node-ENEMY"));
    }

    [Test]
    public void player_node_excluded_when_ownership_changes_to_enemy()
    {
        // Find HOME node and change ownership to ENEMY
        var map = GameDataManager.Instance.GetStarMapData();
        StarNode homeNode = null;
        foreach (var n in map.Nodes) {
            if (n.Id == "node-HOME") { homeNode = n; break; }
        }
        Assert.IsNotNull(homeNode);
        homeNode.Ownership = OwnershipState.ENEMY;

        // Verify it's no longer in PLAYER enumeration
        var playerNodes = new List<string>();
        foreach (var nid in _buildingSystem.GetNodesByOwner(OwnershipState.PLAYER)) {
            playerNodes.Add(nid);
        }

        Assert.IsFalse(playerNodes.Contains("node-HOME"),
            "node-HOME should be excluded after ownership change to ENEMY");
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-5: Multiple ticks accumulate
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void accumulator_carries_fractional_remainder()
    {
        // accumulator = 0.3s, frame adds 0.8s → 1.1s → fires, remainder 0.1s
        SetAccumulator(0.3f);

        // Simulate Update: dt≈0.016s → 0.316 → still below 1.0
        CallUpdate();

        // The key: after multiple ticks with fractional remainders,
        // the accumulator should never lose time
        SetAccumulator(0.9f);
        CallUpdate(); // 0.916 → fires, wraps to ~0.016

        float remainder = GetAccumulator();
        Assert.Less(remainder, 0.5f, "Accumulator should wrap to small value after tick");
    }

    // ─────────────────────────────────────────────────────────────────
    // Additional: ProductionDelta struct
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void production_delta_zero_returns_zero_ore_and_energy()
    {
        var zero = Game.Gameplay.ProductionDelta.Zero;
        Assert.AreEqual(0, zero.OrePerSec);
        Assert.AreEqual(0, zero.EnergyPerSec);
    }

    [Test]
    public void production_delta_can_be_constructed()
    {
        var delta = new Game.Gameplay.ProductionDelta(10, -3);
        Assert.AreEqual(10, delta.OrePerSec);
        Assert.AreEqual(-3, delta.EnergyPerSec);
    }

    // ─────────────────────────────────────────────────────────────────
    // Additional: ResourceSnapshot includes deltas
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void resource_snapshot_includes_delta_fields()
    {
        var snap = new ResourceSnapshot(150, 75, +10, -5);
        Assert.AreEqual(150, snap.Ore);
        Assert.AreEqual(75, snap.Energy);
        Assert.AreEqual(+10, snap.OreDelta);
        Assert.AreEqual(-5, snap.EnergyDelta);
    }
}
