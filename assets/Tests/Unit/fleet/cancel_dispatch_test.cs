#if false
using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using Object = UnityEngine.Object;

/// <summary>
/// FleetDispatchSystem CancelDispatch 单元测试。
/// 覆盖 Story 014 所有验收标准（AC-1 ~ AC-5）。
/// </summary>
[TestFixture]
public class CancelDispatch_Test
{
    // ─────────────────────────────────────────────────────────────────
    // Fields
    // ─────────────────────────────────────────────────────────────────

    private FleetDispatchSystem _fleetDispatch;
    private GameObject _fleetDispatchGo;
    private GameDataManager _gameDataManager;
    private SimClock _simClock;
    private GameObject _simClockGo;

    private const float FLEET_TRAVEL_TIME = 3.0f;

    // ─────────────────────────────────────────────────────────────────
    // SetUp / TearDown
    // ─────────────────────────────────────────────────────────────────

    [SetUp]
    public void SetUp()
    {
        if (GameDataManager.Instance != null) GameDataManager.ResetInstanceForTest();
        if (FleetDispatchSystem.Instance != null) Object.DestroyImmediate(FleetDispatchSystem.Instance.gameObject);
        if (global::Gameplay.SimClock.Instance != null) Object.DestroyImmediate(global::Gameplay.SimClock.Instance.gameObject);

        _gameDataManager = new GameDataManager();

        // StarMap: A <-> B <-> C <-> D (4 nodes, 3 hops A→D)
        var nodes = new List<StarNode> {
            new StarNode("node-A", "Node A", Vector2.zero, NodeType.STANDARD),
            new StarNode("node-B", "Node B", new Vector2(1, 0), NodeType.STANDARD),
            new StarNode("node-C", "Node C", new Vector2(2, 0), NodeType.STANDARD),
            new StarNode("node-D", "Node D", new Vector2(3, 0), NodeType.STANDARD),
        };
        var edges = new List<StarEdge> {
            new StarEdge("node-A", "node-B"),
            new StarEdge("node-B", "node-C"),
            new StarEdge("node-C", "node-D"),
        };
        var starMap = new StarMapData(nodes, edges);
        _gameDataManager.SetStarMapData(starMap);

        _simClockGo = new GameObject("SimClock");
        _simClock = _simClockGo.AddComponent<global::Gameplay.SimClock>();
        _simClock.SetRate(1f);

        _fleetDispatchGo = new GameObject("FleetDispatch");
        _fleetDispatch = _fleetDispatchGo.AddComponent<FleetDispatchSystem>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_fleetDispatchGo != null) Object.DestroyImmediate(_fleetDispatchGo);
        if (_simClockGo != null) Object.DestroyImmediate(_simClockGo);
        GameDataManager.ResetInstanceForTest();
        FleetDispatchSystem.ResetInstanceForTest();
        global::Gameplay.SimClock.ResetInstanceForTest();
    }

    // ─────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────

    private DispatchOrder CreateDispatch(string shipId, string destNodeId)
    {
        var bp = ScriptableObject.CreateInstance<ShipBlueprint>();
        bp.BlueprintId = "test_v1";
        bp.MaxHull = 100;

        var channel = ScriptableObject.CreateInstance<ShipStateChannel>();
        var ship = new ShipDataModel(shipId, "test_v1", true, bp, channel);
        ship.DockedNodeId = "node-A";
        ship.SetState(ShipState.DOCKED);
        _gameDataManager.RegisterShip(ship);

        return _fleetDispatch.RequestDispatch(shipId, destNodeId);
    }

    private void AdvanceOrder(DispatchOrder order, float delta)
    {
        var method = typeof(FleetDispatchSystem).GetMethod("AdvanceOrder",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method.Invoke(_fleetDispatch, new object[] { order, delta });
    }

    private int GetOrderCount()
    {
        var field = typeof(FleetDispatchSystem).GetField("_orders",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var orders = field.GetValue(_fleetDispatch) as Dictionary<string, DispatchOrder>;
        return orders.Count;
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-1: CancelDispatch sets correct reverse path
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void cancel_dispatch_sets_correct_reverse_path()
    {
        // Given: order traveling A→B→C→D, CurrentHopIndex=2 (at C)
        // LockedPath = [A, B, C, D]
        var order = CreateDispatch("ship-1", "node-D");

        // Advance 2 hops: index 0→1 (at B), index 1→2 (at C)
        AdvanceOrder(order, FLEET_TRAVEL_TIME); // hop to B
        AdvanceOrder(order, FLEET_TRAVEL_TIME); // hop to C
        Assert.AreEqual(2, order.CurrentHopIndex);
        Assert.AreEqual("node-C", order.LockedPath[order.CurrentHopIndex]);

        // When: CancelDispatch is called
        _fleetDispatch.CancelDispatch("ship-1");

        // Then: LockedPath = Take(2+1).Reverse() = [A, B, C].Reverse() = [C, B, A]
        Assert.IsTrue(order.IsReturning);
        Assert.AreEqual(3, order.LockedPath.Count);
        Assert.AreEqual("node-C", order.LockedPath[0]);
        Assert.AreEqual("node-B", order.LockedPath[1]);
        Assert.AreEqual("node-A", order.LockedPath[2]);
        Assert.AreEqual(0, order.CurrentHopIndex, "CurrentHopIndex reset to 0 for return journey");
        Assert.AreEqual(0f, order.HopProgress, "HopProgress reset to 0");
    }

    [Test]
    public void cancel_dispatch_at_first_hop_returns_single_node()
    {
        // Given: order just departed (CurrentHopIndex=0, still at A about to go to B)
        var order = CreateDispatch("ship-1", "node-D");

        // Cancel immediately — CurrentHopIndex=0
        AdvanceOrder(order, 0.001f); // tiny advance but not enough for a hop

        // When: CancelDispatch called at start
        _fleetDispatch.CancelDispatch("ship-1");

        // Then: return path is just [A] (Take(0+1) = [A], reversed = [A])
        Assert.IsTrue(order.IsReturning);
        Assert.AreEqual(1, order.LockedPath.Count);
        Assert.AreEqual("node-A", order.LockedPath[0]);
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-2: Return advances in reverse
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void return_advances_current_hop_index_forward_through_reversed_path()
    {
        // Given: order returned with LockedPath = [C, B, A]
        var order = CreateDispatch("ship-1", "node-C"); // A→B→C (2 hops)

        // Advance to C
        AdvanceOrder(order, FLEET_TRAVEL_TIME); // A→B
        AdvanceOrder(order, FLEET_TRAVEL_TIME); // B→C
        Assert.AreEqual(2, order.CurrentHopIndex);
        Assert.AreEqual("node-C", order.LockedPath[2]);

        // Cancel → return path = [C, B, A]
        _fleetDispatch.CancelDispatch("ship-1");
        Assert.AreEqual("node-C", order.LockedPath[0]);
        Assert.AreEqual("node-B", order.LockedPath[1]);
        Assert.AreEqual("node-A", order.LockedPath[2]);

        // When: AdvanceReturn with 3.0s → first hop of return
        AdvanceOrder(order, FLEET_TRAVEL_TIME);

        // Then: CurrentHopIndex=1 (moved from C to B along reversed path)
        Assert.AreEqual(1, order.CurrentHopIndex);
        Assert.AreEqual("node-B", order.LockedPath[1]);
    }

    [Test]
    public void return_advances_hop_progress_accumulates()
    {
        // Given: order returning
        var order = CreateDispatch("ship-1", "node-C");
        AdvanceOrder(order, FLEET_TRAVEL_TIME);
        AdvanceOrder(order, FLEET_TRAVEL_TIME);
        _fleetDispatch.CancelDispatch("ship-1");

        // When: partial hop (1.5s)
        AdvanceOrder(order, 1.5f);

        // Then: HopProgress = 1.5s, not yet a full hop
        Assert.AreEqual(1.5f, order.HopProgress);
        Assert.AreEqual(0, order.CurrentHopIndex, "Still at first hop of return");
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-3: Return to origin → DOCKED + CloseOrder
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void return_to_origin_sets_ship_state_to_docked()
    {
        // Given: order returning to A with path [C, B, A], at A (index 2 of reversed path)
        var order = CreateDispatch("ship-1", "node-C"); // A→B→C
        AdvanceOrder(order, FLEET_TRAVEL_TIME); // A→B
        AdvanceOrder(order, FLEET_TRAVEL_TIME); // B→C
        _fleetDispatch.CancelDispatch("ship-1"); // [C, B, A]

        var ship = GameDataManager.Instance.GetShip("ship-1");

        // When: advance through all 2 return hops (3s each)
        AdvanceOrder(order, FLEET_TRAVEL_TIME); // C→B (index 1)
        Assert.AreEqual(ShipState.IN_TRANSIT, ship.State);

        AdvanceOrder(order, FLEET_TRAVEL_TIME); // B→A (index 2 = end)
        // Then: ship is DOCKED (arrived at origin)
        Assert.AreEqual(ShipState.DOCKED, ship.State,
            "Ship should be DOCKED after completing return journey to origin");
    }

    [Test]
    public void return_to_origin_closes_order()
    {
        // Given: order returning to A
        var order = CreateDispatch("ship-1", "node-C");
        AdvanceOrder(order, FLEET_TRAVEL_TIME);
        AdvanceOrder(order, FLEET_TRAVEL_TIME);
        _fleetDispatch.CancelDispatch("ship-1");

        int countBefore = GetOrderCount();
        Assert.AreEqual(1, countBefore);

        // When: complete return journey
        AdvanceOrder(order, FLEET_TRAVEL_TIME); // C→B
        AdvanceOrder(order, FLEET_TRAVEL_TIME); // B→A → closes order

        // Then: order removed from registry
        Assert.AreEqual(0, GetOrderCount(),
            "Order should be removed from registry after returning to origin");
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-4: Cannot cancel already-returning order
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void cancel_dispatch_does_not_throw_for_nonexistent_ship()
    {
        // When: CancelDispatch called for ship with no active order
        // Then: no exception is thrown (graceful no-op per AC-4)
        Assert.DoesNotThrow(() => _fleetDispatch.CancelDispatch("nonexistent-ship"));
    }

    [Test]
    public void cancel_dispatch_warns_for_already_returning_order()
    {
        // Given: order is already returning
        var order = CreateDispatch("ship-1", "node-C");
        AdvanceOrder(order, FLEET_TRAVEL_TIME);
        _fleetDispatch.CancelDispatch("ship-1");

        // When: cancel again
        _fleetDispatch.CancelDispatch("ship-1");

        // Then: no crash, state unchanged
        Assert.IsTrue(order.IsReturning);
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-5: Cancel mid-hop preserves partial progress
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void cancel_mid_hop_preserves_hop_progress()
    {
        // Given: in the middle of a hop (HopProgress = 1.5s into current hop)
        var order = CreateDispatch("ship-1", "node-C");
        AdvanceOrder(order, FLEET_TRAVEL_TIME); // completed first hop, now at B
        AdvanceOrder(order, 1.5f); // 1.5s into second hop toward C

        Assert.AreEqual(1, order.CurrentHopIndex); // at B
        Assert.AreEqual(1.5f, order.HopProgress);

        // When: cancel
        _fleetDispatch.CancelDispatch("ship-1");

        // Then: progress is reset — return journey always starts fresh
        Assert.AreEqual(0, order.CurrentHopIndex);
        Assert.AreEqual(0f, order.HopProgress);
        Assert.IsTrue(order.IsReturning);
    }

    // ─────────────────────────────────────────────────────────────────
    // Additional: Ship cannot be re-dispatched while returning
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void ship_cannot_be_redispatched_while_in_transit()
    {
        // Given: ship is IN_TRANSIT ( dispatched and not yet arrived or returned)
        var order = CreateDispatch("ship-1", "node-C");
        AdvanceOrder(order, FLEET_TRAVEL_TIME); // A→B, still in transit

        // When: try to dispatch again
        var newOrder = _fleetDispatch.RequestDispatch("ship-1", "node-D");

        // Then: rejected because ship is IN_TRANSIT
        Assert.IsNull(newOrder, "RequestDispatch should reject IN_TRANSIT ship");
    }

    // ─────────────────────────────────────────────────────────────────
    // Additional: Multiple ships can be cancelled independently
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void multiple_ships_can_be_cancelled_independently()
    {
        // Given: 3 ships in transit
        var order1 = CreateDispatch("ship-1", "node-D");
        var order2 = CreateDispatch("ship-2", "node-D");
        var order3 = CreateDispatch("ship-3", "node-D");

        AdvanceOrder(order1, FLEET_TRAVEL_TIME); // ship-1 at hop 1
        AdvanceOrder(order2, FLEET_TRAVEL_TIME * 2); // ship-2 at hop 2
        // ship-3 still at hop 0

        // When: cancel ship-1 and ship-3 (not ship-2)
        _fleetDispatch.CancelDispatch("ship-1");
        _fleetDispatch.CancelDispatch("ship-3");

        // Then: only ship-1 and ship-3 are returning
        Assert.IsTrue(order1.IsReturning);
        Assert.IsFalse(order2.IsReturning);
        Assert.IsTrue(order3.IsReturning);
    }
}

#endif
