using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using Object = UnityEngine.Object;

/// <summary>
/// FleetDispatchSystem Unattended Combat 单元测试。
/// 覆盖 Story 007 所有验收标准（AC-1 ~ AC-5）。
/// </summary>
[TestFixture]
public class UnattendedCombat_Test
{
    // ─────────────────────────────────────────────────────────────────
    // Fields
    // ─────────────────────────────────────────────────────────────────

    private FleetDispatchSystem _fleetDispatch;
    private GameObject _fleetDispatchGo;
    private GameDataManager _gameDataManager;
    private StarMapData _starMap;
    private HealthSystem _healthSystem;
    private GameObject _healthGo;

    // Tracks whether OnShipDying was fired
    private bool _onShipDyingFired;
    private string _onShipDyingInstanceId;

    // ─────────────────────────────────────────────────────────────────
    // SetUp / TearDown
    // ─────────────────────────────────────────────────────────────────

    [SetUp]
    public void SetUp()
    {
        if (GameDataManager.Instance != null) GameDataManager.Instance = null;
        if (FleetDispatchSystem.Instance != null) Object.DestroyImmediate(FleetDispatchSystem.Instance.gameObject);
        if (HealthSystem.Instance != null) Object.DestroyImmediate(HealthSystem.Instance.gameObject);

        _gameDataManager = new GameDataManager();

        // StarMap: node-A ↔ node-B (enemy node)
        var nodes = new List<StarNode> {
            new StarNode("node-A", "Node A", Vector2.zero, NodeType.STANDARD),
            new StarNode("node-B", "Node B", new Vector2(1, 0), NodeType.STANDARD),
        };
        var edges = new List<StarEdge> {
            new StarEdge("node-A", "node-B"),
        };
        _starMap = new StarMapData(nodes, edges);
        _gameDataManager.SetStarMapData(_starMap);

        // HealthSystem (for verifying OnShipDying is NOT called on U-4 defeat)
        _healthGo = new GameObject("HealthSystem");
        _healthSystem = _healthGo.AddComponent<HealthSystem>();
        _healthSystem.OnShipDying += (id) => {
            _onShipDyingFired = true;
            _onShipDyingInstanceId = id;
        };

        _fleetDispatchGo = new GameObject("FleetDispatch");
        _fleetDispatch = _fleetDispatchGo.AddComponent<FleetDispatchSystem>();

        ResetDyingTracker();
    }

    [TearDown]
    public void TearDown()
    {
        if (_fleetDispatchGo != null) Object.DestroyImmediate(_fleetDispatchGo);
        if (_healthGo != null) Object.DestroyImmediate(_healthGo);
        if (_starMap != null) Object.DestroyImmediate(_starMap);
        GameDataManager.Instance = null;
        FleetDispatchSystem.Instance = null;
        HealthSystem.Instance = null;
    }

    // ─────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────

    private void ResetDyingTracker()
    {
        _onShipDyingFired = false;
        _onShipDyingInstanceId = null;
    }

    private ShipDataModel CreateAndRegisterShip(string id, string dockedNodeId, bool isPlayer)
    {
        var bp = ScriptableObject.CreateInstance<ShipBlueprint>();
        bp.BlueprintId = "test_v1";
        bp.MaxHull = 100;
        bp.ThrustPower = 50f;
        bp.TurnSpeed = 90f;
        bp.WeaponSlots = 2;

        var channel = ScriptableObject.CreateInstance<ShipStateChannel>();
        var ship = new ShipDataModel(id, "test_v1", isPlayer, bp, channel);
        ship.DockedNodeId = dockedNodeId;
        ship.SetState(ShipState.DOCKED);
        _gameDataManager.RegisterShip(ship);
        return ship;
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-1: P=3, E=2 → VICTORY (P=1, E=0)
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void unattended_combat_P3_E2_is_VICTORY()
    {
        // Given: 3 player ships at node-A
        CreateAndRegisterShip("ship-1", "node-A", true);
        CreateAndRegisterShip("ship-2", "node-A", true);
        CreateAndRegisterShip("ship-3", "node-A", true);

        string victoryNodeId = null;
        _fleetDispatch.OnUnattendedVictory += (nodeId) => victoryNodeId = nodeId;

        // When: ResolveUnattendedCombat is called
        _fleetDispatch.ResolveUnattendedCombat("ship-1", "node-A");

        // Then: VICTORY broadcast
        Assert.AreEqual("node-A", victoryNodeId, "Victory should fire for the node");
        // Ships should remain DOCKED
        foreach (var ship in _gameDataManager.AllShips) {
            Assert.AreEqual(ShipState.DOCKED, ship.State,
                $"Ship {ship.InstanceId} should remain DOCKED after victory");
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-2: P=1, E=3 → DEFEAT (P=0)
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void unattended_combat_P1_E3_is_DEFEAT()
    {
        // Given: 1 player ship at node-A
        var ship = CreateAndRegisterShip("ship-1", "node-A", true);

        string defeatNodeId = null;
        _fleetDispatch.OnUnattendedDefeat += (nodeId) => defeatNodeId = nodeId;

        // When: ResolveUnattendedCombat is called
        _fleetDispatch.ResolveUnattendedCombat("ship-1", "node-A");

        // Then: DEFEAT broadcast
        Assert.AreEqual("node-A", defeatNodeId, "Defeat should fire for the node");
        // Ship should be DESTROYED
        Assert.AreEqual(ShipState.DESTROYED, ship.State,
            "Player ship should be DESTROYED after defeat");
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-3: P=1, E=1 → DEFEAT (tie)
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void unattended_combat_P1_E1_is_DEFEAT_tie()
    {
        // Given: 1 player ship at node-A
        var ship = CreateAndRegisterShip("ship-1", "node-A", true);

        bool victoryFired = false;
        bool defeatFired = false;
        _fleetDispatch.OnUnattendedVictory += (_) => victoryFired = true;
        _fleetDispatch.OnUnattendedDefeat += (_) => defeatFired = true;

        // When: ResolveUnattendedCombat is called (P=1, E=1 → P=0, E=0)
        _fleetDispatch.ResolveUnattendedCombat("ship-1", "node-A");

        // Then: DEFEAT (not VICTORY — tie goes to defeat)
        Assert.IsTrue(defeatFired, "Tie should result in DEFEAT");
        Assert.IsFalse(victoryFired, "Tie should NOT result in VICTORY");
        Assert.AreEqual(ShipState.DESTROYED, ship.State,
            "Player ship should be DESTROYED after tie");
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-4: Unattended victory → fleet state DOCKED
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void victory_does_not_change_ship_state()
    {
        // Given: 2 player ships at node-A
        var ship1 = CreateAndRegisterShip("ship-1", "node-A", true);
        var ship2 = CreateAndRegisterShip("ship-2", "node-A", true);

        _fleetDispatch.ResolveUnattendedCombat("ship-1", "node-A");

        // Then: both ships remain DOCKED
        Assert.AreEqual(ShipState.DOCKED, ship1.State);
        Assert.AreEqual(ShipState.DOCKED, ship2.State);
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-5: U-4 bypasses HealthSystem — OnShipDying NOT triggered
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void defeat_U4_path_bypasses_HealthSystem()
    {
        // Given: 1 player ship
        CreateAndRegisterShip("ship-1", "node-A", true);
        ResetDyingTracker();

        // When: U-4 defeat occurs
        _fleetDispatch.ResolveUnattendedCombat("ship-1", "node-A");

        // Then: OnShipDying was NOT fired (U-4 bypasses HealthSystem)
        Assert.IsFalse(_onShipDyingFired,
            "OnShipDying should NOT fire on U-4 defeat path — HealthSystem is bypassed");
        Assert.IsNull(_onShipDyingInstanceId,
            "No instanceId should be broadcast via OnShipDying on U-4 path");
    }

    // ─────────────────────────────────────────────────────────────────
    // Additional: P=0 (no ships) at node
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void unattended_combat_P0_is_DEFEAT()
    {
        // Given: no player ships at node-A (empty fleet)
        bool defeatFired = false;
        _fleetDispatch.OnUnattendedDefeat += (_) => defeatFired = true;

        // When: ResolveUnattendedCombat is called
        _fleetDispatch.ResolveUnattendedCombat("nonexistent-ship", "node-A");

        // Then: DEFEAT fires (no ships to defend)
        Assert.IsTrue(defeatFired, "P=0 should result in immediate DEFEAT");
    }

    // ─────────────────────────────────────────────────────────────────
    // Additional: Victory event contains nodeId
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void victory_broadcast_contains_correct_node_id()
    {
        CreateAndRegisterShip("ship-1", "node-A", true);
        CreateAndRegisterShip("ship-2", "node-A", true);

        string capturedNodeId = null;
        _fleetDispatch.OnUnattendedVictory += (nodeId) => capturedNodeId = nodeId;

        _fleetDispatch.ResolveUnattendedCombat("ship-1", "node-A");

        Assert.AreEqual("node-A", capturedNodeId,
            "OnUnattendedVictory should broadcast the correct nodeId");
    }

    [Test]
    public void defeat_broadcast_contains_correct_node_id()
    {
        CreateAndRegisterShip("ship-1", "node-A", true);

        string capturedNodeId = null;
        _fleetDispatch.OnUnattendedDefeat += (nodeId) => capturedNodeId = nodeId;

        _fleetDispatch.ResolveUnattendedCombat("ship-1", "node-A");

        Assert.AreEqual("node-A", capturedNodeId,
            "OnUnattendedDefeat should broadcast the correct nodeId");
    }
}
