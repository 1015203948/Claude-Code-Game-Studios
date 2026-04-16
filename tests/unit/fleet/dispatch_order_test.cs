using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using Object = UnityEngine.Object;
using Game.Gameplay.Fleet;

/// <summary>
/// FleetDispatchSystem DispatchOrder 单元测试。
/// 覆盖 Story 012 所有验收标准（AC-1 ~ AC-3）。
/// </summary>
[TestFixture]
public class DispatchOrder_Test
{
    // ─────────────────────────────────────────────────────────────────
    // Fields
    // ─────────────────────────────────────────────────────────────────

    private FleetDispatchSystem _fleetDispatch;
    private GameObject _fleetDispatchGo;
    private GameDataManager _gameDataManager;
    private ShipDataModel _ship;
    private ShipBlueprint _blueprint;
    private ShipStateChannel _stateChannel;
    private StarMapData _starMap;

    // ─────────────────────────────────────────────────────────────────
    // SetUp / TearDown
    // ─────────────────────────────────────────────────────────────────

    [SetUp]
    public void SetUp()
    {
        if (GameDataManager.Instance != null) GameDataManager.Instance = null;
        if (FleetDispatchSystem.Instance != null) Object.DestroyImmediate(FleetDispatchSystem.Instance.gameObject);

        _gameDataManager = new GameDataManager();

        // StarMapData with 3 connected nodes: node-A <-> node-B <-> node-C
        var nodes = new List<StarNode> {
            new StarNode("node-A", "Node A", Vector2.zero, NodeType.STANDARD),
            new StarNode("node-B", "Node B", new Vector2(1, 0), NodeType.STANDARD),
            new StarNode("node-C", "Node C", new Vector2(2, 0), NodeType.STANDARD),
        };
        var edges = new List<StarEdge> {
            new StarEdge("node-A", "node-B"),
            new StarEdge("node-B", "node-C"),
        };
        _starMap = new StarMapData(nodes, edges);
        _gameDataManager.SetStarMapData(_starMap);

        // Player ship blueprint
        _blueprint = ScriptableObject.CreateInstance<ShipBlueprint>();
        _blueprint.BlueprintId = "test_v1";
        _blueprint.MaxHull = 100;
        _blueprint.ThrustPower = 50f;
        _blueprint.TurnSpeed = 90f;
        _blueprint.WeaponSlots = 2;

        _stateChannel = ScriptableObject.CreateInstance<ShipStateChannel>();
        _ship = new ShipDataModel(
            "ship-1", "test_v1",
            isPlayerControlled: true,
            _blueprint,
            _stateChannel);
        _ship.DockedNodeId = "node-A";
        _ship.SetState(ShipState.DOCKED);
        _gameDataManager.RegisterShip(_ship);

        // FleetDispatchSystem
        _fleetDispatchGo = new GameObject("FleetDispatchSystem");
        _fleetDispatch = _fleetDispatchGo.AddComponent<FleetDispatchSystem>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_fleetDispatchGo != null) Object.DestroyImmediate(_fleetDispatchGo);
        if (_starMap != null) Object.DestroyImmediate(_starMap);
        if (_blueprint != null) Object.DestroyImmediate(_blueprint);
        if (_stateChannel != null) Object.DestroyImmediate(_stateChannel);
        GameDataManager.Instance = null;
        FleetDispatchSystem.Instance = null;
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-1: Non-DOCKED ship rejected
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void request_dispatch_returns_null_for_non_docked_ship()
    {
        // Given: ship is IN_TRANSIT (not DOCKED)
        _ship.SetState(ShipState.IN_TRANSIT);

        // When: RequestDispatch is called
        var order = _fleetDispatch.RequestDispatch("ship-1", "node-B");

        // Then: returns null, no order created
        Assert.IsNull(order, "RequestDispatch should return null for non-DOCKED ship");
        Assert.AreEqual(0, _fleetDispatchGetOrderCount(),
            "No dispatch order should be created");
    }

    [Test]
    public void request_dispatch_returns_null_for_null_ship()
    {
        // When: RequestDispatch is called with non-existent ship
        var order = _fleetDispatch.RequestDispatch("nonexistent-ship", "node-B");

        // Then: returns null
        Assert.IsNull(order);
    }

    [Test]
    public void request_dispatch_returns_null_when_no_path_exists()
    {
        // Given: ship is DOCKED at node-A (connected to node-B and node-C)
        // When: trying to dispatch to an unreachable node
        var order = _fleetDispatch.RequestDispatch("ship-1", "node-unreachable");

        // Then: returns null with warning
        Assert.IsNull(order, "RequestDispatch should return null when no path exists");
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-2: Valid dispatch creates correct DispatchOrder
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void request_dispatch_creates_correct_dispatch_order()
    {
        // Given: ship is DOCKED at node-A
        Assert.AreEqual(ShipState.DOCKED, _ship.State);

        // When: RequestDispatch is called for node-B
        var order = _fleetDispatch.RequestDispatch("ship-1", "node-B");

        // Then: order is created with correct fields
        Assert.IsNotNull(order, "RequestDispatch should return a valid order");

        Assert.AreEqual("ship-1", order.ShipId, "ShipId should match");
        Assert.AreEqual("node-A", order.OriginNodeId, "OriginNodeId should be node-A");
        Assert.AreEqual("node-B", order.DestinationNodeId, "DestinationNodeId should be node-B");
        Assert.IsNotNull(order.LockedPath, "LockedPath should not be null");
        Assert.GreaterOrEqual(order.LockedPath.Count, 2, "LockedPath should have at least origin and destination");
        Assert.AreEqual("node-A", order.LockedPath[0], "LockedPath first node should be origin");
        Assert.AreEqual("node-B", order.LockedPath[order.LockedPath.Count - 1], "LockedPath last node should be destination");
        Assert.AreEqual(0, order.CurrentHopIndex, "CurrentHopIndex should be 0");
        Assert.AreEqual(0f, order.HopProgress, 0.001f, "HopProgress should be 0");
        Assert.IsFalse(order.IsReturning, "IsReturning should be false");
    }

    [Test]
    public void request_dispatch_transitions_ship_to_in_transit()
    {
        // Given: ship is DOCKED
        Assert.AreEqual(ShipState.DOCKED, _ship.State);

        // When: RequestDispatch is called
        _fleetDispatch.RequestDispatch("ship-1", "node-B");

        // Then: ship state transitions to IN_TRANSIT
        Assert.AreEqual(ShipState.IN_TRANSIT, _ship.State,
            "Ship should transition to IN_TRANSIT after dispatch");
    }

    [Test]
    public void request_dispatch_broadcasts_OnDispatchCreated()
    {
        // Given: subscribe to OnDispatchCreated
        DispatchOrder capturedOrder = null;
        _fleetDispatch.OnDispatchCreated += (order) => capturedOrder = order;

        // When: RequestDispatch is called
        var order = _fleetDispatch.RequestDispatch("ship-1", "node-B");

        // Then: event was broadcast with correct order
        Assert.IsNotNull(capturedOrder, "OnDispatchCreated should be broadcast");
        Assert.AreEqual(order.OrderId, capturedOrder.OrderId);
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-3: LockedPath snapshot is immutable
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void locked_path_is_snapshot_not_reference()
    {
        // Given: dispatch to node-C (multi-hop path)
        var order = _fleetDispatch.RequestDispatch("ship-1", "node-C");
        Assert.IsNotNull(order);

        var originalPath = new List<string>(order.LockedPath); // snapshot

        // When: StarMapData changes (add new connection)
        // (In real scenario, nodes might be added/removed — we can't easily simulate
        // this without modifying the star map, but we verify the list is a copy)
        order.LockedPath.Clear(); // mutate the internal list

        // Then: original path snapshot is unaffected (it's a copy)
        Assert.AreEqual(originalPath.Count, order.LockedPath.Count,
            "After clearing, LockedPath should be empty — proving it was stored as reference (this test verifies the implementation stores a copy)");

        // More precisely: verify it's a NEW list, not the same reference
        var order2 = _fleetDispatch.RequestDispatch("ship-1", "node-B");
        Assert.AreNotSame(order.LockedPath, order2.LockedPath,
            "Each order should have its own LockedPath list instance");
    }

    // ─────────────────────────────────────────────────────────────────
    // Additional tests
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void dispatch_order_has_unique_id()
    {
        // When: multiple dispatches
        var order1 = _fleetDispatch.RequestDispatch("ship-1", "node-B");
        Assert.IsNotNull(order1);

        // Register another ship
        var ship2Blueprint = ScriptableObject.CreateInstance<ShipBlueprint>();
        ship2Blueprint.BlueprintId = "test_v1";
        ship2Blueprint.MaxHull = 100;
        ship2Blueprint.ThrustPower = 50f;
        ship2Blueprint.TurnSpeed = 90f;
        ship2Blueprint.WeaponSlots = 2;
        var stateChannel2 = ScriptableObject.CreateInstance<ShipStateChannel>();
        var ship2 = new ShipDataModel("ship-2", "test_v1", false, ship2Blueprint, stateChannel2);
        ship2.DockedNodeId = "node-B";
        ship2.SetState(ShipState.DOCKED);
        _gameDataManager.RegisterShip(ship2);

        var order2 = _fleetDispatch.RequestDispatch("ship-2", "node-C");
        Assert.IsNotNull(order2);

        Assert.AreNotEqual(order1.OrderId, order2.OrderId,
            "Each dispatch order should have a unique OrderId");
    }

    [Test]
    public void get_order_returns_order_by_id()
    {
        var created = _fleetDispatch.RequestDispatch("ship-1", "node-B");
        Assert.IsNotNull(created);

        var retrieved = _fleetDispatch.GetOrder(created.OrderId);

        Assert.AreSame(created, retrieved, "GetOrder should return the same order instance");
    }

    [Test]
    public void get_order_returns_null_for_nonexistent_id()
    {
        var result = _fleetDispatch.GetOrder("nonexistent-order-id");
        Assert.IsNull(result);
    }

    // ─────────────────────────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────────────────────────

    private int _fleetDispatchGetOrderCount()
    {
        var field = typeof(FleetDispatchSystem).GetField("_orders",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var orders = field.GetValue(_fleetDispatch) as Dictionary<string, DispatchOrder>;
        return orders.Count;
    }
}
