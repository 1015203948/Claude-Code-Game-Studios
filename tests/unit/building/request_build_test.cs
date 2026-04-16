using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using Object = UnityEngine.Object;

/// <summary>
/// BuildingSystem RequestBuild + GetNodeProductionDelta 单元测试。
/// 覆盖 Story 018 所有验收标准（AC-1 ~ AC-5）。
/// </summary>
[TestFixture]
public class RequestBuild_Test
{
    // ─────────────────────────────────────────────────────────────────
    // Fields
    // ─────────────────────────────────────────────────────────────────

    private Game.Gameplay.BuildingSystem _buildingSystem;
    private GameObject _buildingSystemGo;
    private Gameplay.ColonyManager _colonyManager;
    private GameObject _colonyManagerGo;
    private GameDataManager _gameDataManager;
    private global::Gameplay.SimClock _simClock;
    private GameObject _simClockGo;

    private int _onBuildingConstructedCallCount;
    private string _lastBuildingConstructedNodeId;
    private Game.Data.BuildingType _lastBuildingConstructedType;

    // ─────────────────────────────────────────────────────────────────
    // SetUp / TearDown
    // ─────────────────────────────────────────────────────────────────

    [SetUp]
    public void SetUp()
    {
        if (GameDataManager.Instance != null) GameDataManager.Instance = null;
        if (global::Gameplay.SimClock.Instance != null) Object.DestroyImmediate(global::Gameplay.SimClock.Instance.gameObject);
        if (Game.Gameplay.BuildingSystem.Instance != null) Object.DestroyImmediate(Game.Gameplay.BuildingSystem.Instance.gameObject);
        if (Gameplay.ColonyManager.Instance != null) Object.DestroyImmediate(Gameplay.ColonyManager.Instance.gameObject);
        if (Game.Gameplay.ShipSystem.Instance != null) Object.DestroyImmediate(Game.Gameplay.ShipSystem.Instance.gameObject);
        if (HealthSystem.Instance != null) Object.DestroyImmediate(HealthSystem.Instance.gameObject);

        _gameDataManager = new GameDataManager();

        // StarMap: PLAYER node (mine-worthy), PLAYER_NOYARD, ENEMY node
        var nodes = new List<StarNode> {
            new StarNode("node-PLAYER", "Player Node", Vector2.zero, NodeType.STANDARD) {
                Ownership = OwnershipState.PLAYER,
                HasShipyard = false,
                ShipyardTier = 0
            },
            new StarNode("node-PLAYER_YARD", "Player Yard", new Vector2(1, 0), NodeType.STANDARD) {
                Ownership = OwnershipState.PLAYER,
                HasShipyard = true,
                ShipyardTier = 1
            },
            new StarNode("node-ENEMY", "Enemy", new Vector2(2, 0), NodeType.ENEMY) {
                Ownership = OwnershipState.ENEMY,
                HasShipyard = false,
                ShipyardTier = 0
            },
        };
        var edges = new List<StarEdge> {
            new StarEdge("node-PLAYER", "node-PLAYER_YARD"),
            new StarEdge("node-PLAYER_YARD", "node-ENEMY"),
        };
        var starMap = new StarMapData(nodes, edges);
        _gameDataManager.SetStarMapData(starMap);

        _simClockGo = new GameObject("SimClock");
        _simClock = _simClockGo.AddComponent<global::Gameplay.SimClock>();
        _simClock.SetRate(1f);

        _buildingSystemGo = new GameObject("BuildingSystem");
        _buildingSystem = _buildingSystemGo.AddComponent<Game.Gameplay.BuildingSystem>();

        _colonyManagerGo = new GameObject("ColonyManager");
        _colonyManager = _colonyManagerGo.AddComponent<Gameplay.ColonyManager>();
        _colonyManager.Initialize(200, 100); // rich resources for building tests

        _onBuildingConstructedCallCount = 0;
        _lastBuildingConstructedNodeId = null;
        _lastBuildingConstructedType = Game.Data.BuildingType.BasicMine;

        _buildingSystem.OnBuildingConstructed += (nodeId, type) => {
            _onBuildingConstructedCallCount++;
            _lastBuildingConstructedNodeId = nodeId;
            _lastBuildingConstructedType = type;
        };
    }

    [TearDown]
    public void TearDown()
    {
        if (_buildingSystemGo != null) Object.DestroyImmediate(_buildingSystemGo);
        if (_colonyManagerGo != null) Object.DestroyImmediate(_colonyManagerGo);
        if (_simClockGo != null) Object.DestroyImmediate(_simClockGo);
        GameDataManager.Instance = null;
        global::Gameplay.SimClock.Instance = null;
        Game.Gameplay.BuildingSystem.Instance = null;
        Gameplay.ColonyManager.Instance = null;
        Game.Gameplay.ShipSystem.Instance = null;
    }

    // ─────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────

    private StarNode GetNode(string nodeId)
    {
        foreach (var n in _gameDataManager.GetStarMapData().Nodes) {
            if (n.Id == nodeId) return n;
        }
        return null;
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-1: BasicMine build creates instance and updates cache
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void basic_mine_success_creates_building_instance()
    {
        // Given: node-PLAYER has no buildings
        var node = GetNode("node-PLAYER");
        Assert.AreEqual(0, node.Buildings.Count);

        // When: RequestBuild BasicMine (cost: 50 ore + 20 energy)
        var result = _buildingSystem.RequestBuild("node-PLAYER", Game.Data.BuildingType.BasicMine);

        // Then: success
        Assert.IsTrue(result.Success);

        // Building instance added
        Assert.AreEqual(1, node.Buildings.Count);
        Assert.AreEqual(Game.Data.BuildingType.BasicMine, node.Buildings[0].BuildingType);
        Assert.IsTrue(node.Buildings[0].IsActive);
    }

    [Test]
    public void basic_mine_success_deducts_resources()
    {
        // Given: ore=200, energy=100; mine costs 50+20=70 total
        int oreBefore = _colonyManager.OreCurrent;
        int energyBefore = _colonyManager.EnergyCurrent;

        // When: RequestBuild BasicMine
        _buildingSystem.RequestBuild("node-PLAYER", Game.Data.BuildingType.BasicMine);

        // Then: deducted 50 ore, 20 energy
        Assert.AreEqual(oreBefore - 50, _colonyManager.OreCurrent);
        Assert.AreEqual(energyBefore - 20, _colonyManager.EnergyCurrent);
    }

    [Test]
    public void basic_mine_success_broadcasts_OnBuildingConstructed()
    {
        // When: RequestBuild BasicMine
        _buildingSystem.RequestBuild("node-PLAYER", Game.Data.BuildingType.BasicMine);

        // Then: event fired
        Assert.AreEqual(1, _onBuildingConstructedCallCount);
        Assert.AreEqual("node-PLAYER", _lastBuildingConstructedNodeId);
        Assert.AreEqual(Game.Data.BuildingType.BasicMine, _lastBuildingConstructedType);
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-2: Shipyard sets ShipyardTier=1
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void shipyard_sets_shipyard_tier_to_1()
    {
        // Given: node-PLAYER (no shipyard)
        var node = GetNode("node-PLAYER");
        Assert.AreEqual(0, node.ShipyardTier);

        // When: RequestBuild Shipyard
        var result = _buildingSystem.RequestBuild("node-PLAYER", Game.Data.BuildingType.Shipyard);

        // Then: success
        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, node.ShipyardTier);
        Assert.IsTrue(node.HasShipyard);
    }

    [Test]
    public void shipyard_does_not_add_building_instance()
    {
        // Shipyard is a property on the node, not a building instance
        var node = GetNode("node-PLAYER");

        _buildingSystem.RequestBuild("node-PLAYER", Game.Data.BuildingType.Shipyard);

        // No building instance added (shipyard is tracked via ShipyardTier)
        Assert.AreEqual(0, node.Buildings.Count,
            "Shipyard does not add a building instance — it's a node property");
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-3: ShipyardUpgrade increments existing tier
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void shipyard_upgrade_increments_tier()
    {
        // Given: node-PLAYER already has shipyard (tier=1)
        var node = GetNode("node-PLAYER");
        node.HasShipyard = true;
        node.ShipyardTier = 1;

        // When: RequestBuild ShipyardUpgrade
        var result = _buildingSystem.RequestBuild("node-PLAYER", Game.Data.BuildingType.ShipyardUpgrade);

        // Then: tier becomes 2
        Assert.IsTrue(result.Success);
        Assert.AreEqual(2, node.ShipyardTier);
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-4: GetNodeProductionDelta returns correct values
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void get_node_production_delta_single_mine()
    {
        // Given: node with 1 BasicMine (ore=+10, energy=-2)
        var node = GetNode("node-PLAYER");
        node.AddBuilding(new Game.Data.BuildingInstance("bld_1", Game.Data.BuildingType.BasicMine, "node-PLAYER"));

        // When: GetNodeProductionDelta
        var delta = _buildingSystem.GetNodeProductionDelta("node-PLAYER");

        // Then: ore=+10, energy=-2
        Assert.AreEqual(10, delta.OrePerSec);
        Assert.AreEqual(-2, delta.EnergyPerSec);
    }

    [Test]
    public void get_node_production_delta_multiple_mines()
    {
        // Given: node with 2 BasicMines (ore=+20, energy=-4)
        var node = GetNode("node-PLAYER");
        node.AddBuilding(new Game.Data.BuildingInstance("bld_1", Game.Data.BuildingType.BasicMine, "node-PLAYER"));
        node.AddBuilding(new Game.Data.BuildingInstance("bld_2", Game.Data.BuildingType.BasicMine, "node-PLAYER"));

        var delta = _buildingSystem.GetNodeProductionDelta("node-PLAYER");

        Assert.AreEqual(20, delta.OrePerSec);
        Assert.AreEqual(-4, delta.EnergyPerSec);
    }

    [Test]
    public void get_node_production_delta_zero_when_no_buildings()
    {
        var delta = _buildingSystem.GetNodeProductionDelta("node-PLAYER");
        Assert.AreEqual(0, delta.OrePerSec);
        Assert.AreEqual(0, delta.EnergyPerSec);
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-5: Insufficient resources → no state change
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void insufficient_resources_rejected_without_change()
    {
        // Given: ore=10 (< 50 cost), energy=100
        _colonyManager.Initialize(10, 100);
        var node = GetNode("node-PLAYER");

        // When: RequestBuild BasicMine
        var result = _buildingSystem.RequestBuild("node-PLAYER", Game.Data.BuildingType.BasicMine);

        // Then: rejected
        Assert.IsFalse(result.Success);
        Assert.AreEqual("INSUFFICIENT_RESOURCES", result.FailReason);

        // No building added
        Assert.AreEqual(0, node.Buildings.Count);

        // Resources unchanged (still 10, 100)
        Assert.AreEqual(10, _colonyManager.OreCurrent);
    }

    [Test]
    public void enemy_node_rejected()
    {
        // Given: node-ENEMY is ENEMY owned
        var result = _buildingSystem.RequestBuild("node-ENEMY", Game.Data.BuildingType.BasicMine);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("NODE_NOT_PLAYER", result.FailReason);
    }

    // ─────────────────────────────────────────────────────────────────
    // Additional: BuildingCosts
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void building_costs_basic_mine()
    {
        var (ore, energy) = Game.Data.BuildingCosts.GetCost(Game.Data.BuildingType.BasicMine);
        Assert.AreEqual(50, ore);
        Assert.AreEqual(20, energy);
    }

    [Test]
    public void building_costs_shipyard()
    {
        var (ore, energy) = Game.Data.BuildingCosts.GetCost(Game.Data.BuildingType.Shipyard);
        Assert.AreEqual(80, ore);
        Assert.AreEqual(40, energy);
    }

    [Test]
    public void building_costs_shipyard_upgrade()
    {
        var (ore, energy) = Game.Data.BuildingCosts.GetCost(Game.Data.BuildingType.ShipyardUpgrade);
        Assert.AreEqual(100, ore);
        Assert.AreEqual(50, energy);
    }

    // ─────────────────────────────────────────────────────────────────
    // Additional: BuildingProduction
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void building_production_basic_mine()
    {
        var (ore, energy) = Game.Data.BuildingProduction.GetDelta(Game.Data.BuildingType.BasicMine);
        Assert.AreEqual(10, ore);
        Assert.AreEqual(-2, energy);
    }

    [Test]
    public void building_production_shipyard()
    {
        var (ore, energy) = Game.Data.BuildingProduction.GetDelta(Game.Data.BuildingType.Shipyard);
        Assert.AreEqual(0, ore);
        Assert.AreEqual(-3, energy);
    }

    [Test]
    public void building_production_shipyard_upgrade_is_zero()
    {
        var (ore, energy) = Game.Data.BuildingProduction.GetDelta(Game.Data.BuildingType.ShipyardUpgrade);
        Assert.AreEqual(0, ore);
        Assert.AreEqual(0, energy);
    }

    // ─────────────────────────────────────────────────────────────────
    // Additional: inactive building skipped
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void inactive_building_ignored_in_production()
    {
        var node = GetNode("node-PLAYER");
        var inactiveMine = new Game.Data.BuildingInstance("bld_inactive", Game.Data.BuildingType.BasicMine, "node-PLAYER");
        inactiveMine.IsActive = false;
        node.AddBuilding(inactiveMine);

        var delta = _buildingSystem.GetNodeProductionDelta("node-PLAYER");

        Assert.AreEqual(0, delta.OrePerSec,
            "Inactive buildings should not contribute to production");
    }

    // ─────────────────────────────────────────────────────────────────
    // Additional: CanAffordResources
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void can_afford_resources_true_when_sufficient()
    {
        _colonyManager.Initialize(100, 50);
        Assert.IsTrue(_colonyManager.CanAffordResources(50, 20));
        Assert.IsTrue(_colonyManager.CanAffordResources(100, 50));
    }

    [Test]
    public void can_afford_resources_false_when_insufficient_ore()
    {
        _colonyManager.Initialize(30, 50);
        Assert.IsFalse(_colonyManager.CanAffordResources(50, 20));
    }

    [Test]
    public void can_afford_resources_false_when_insufficient_energy()
    {
        _colonyManager.Initialize(100, 10);
        Assert.IsFalse(_colonyManager.CanAffordResources(50, 20));
    }

    [Test]
    public void can_afford_resources_exact_boundaries()
    {
        _colonyManager.Initialize(50, 20);
        Assert.IsTrue(_colonyManager.CanAffordResources(50, 20),
            "Exact resources should be affordable");
    }
}
