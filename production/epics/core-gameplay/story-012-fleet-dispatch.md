# Story 012: FleetDispatch — DispatchOrder Creation + State Transition

> **Epic**: core-gameplay
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Manifest Version**: 2026-04-14

## Context

**GDD**: `design/gdd/fleet-dispatch-system.md`
**Requirement**: `TR-fleet-001`, `TR-fleet-002`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0017: Fleet Dispatch Architecture
**ADR Decision Summary**: RequestDispatch 验证 ShipState=DOCKED → StarMapPathfinder.FindPath BFS → 创建 DispatchOrder → ShipState → IN_TRANSIT；LockedPath 快照不受后续星图变化影响。

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: 纯数据追踪 + 时间积分，无 Unity 物理/渲染 API。

**Control Manifest Rules (this layer)**:
- Required: DispatchOrder.LockedPath 为路径快照副本
- Forbidden: 禁止修改已有 DispatchOrder 的 ShipId
- Guardrail: ShipState 校验失败时静默返回 null

---

## Acceptance Criteria

*From GDD `design/gdd/fleet-dispatch-system.md` D-1~D-4, DispatchOrder:*

- [ ] RequestDispatch(shipId, destNodeId) ShipState 非 DOCKED 时返回 null
- [ ] 路径不存在时返回 null，输出 Debug.LogWarning
- [ ] 创建 DispatchOrder：ShipId, OriginNodeId, DestinationNodeId, LockedPath(快照), CurrentHopIndex=0, HopProgress=0, IsReturning=false
- [ ] 创建后 ShipDataModel.SetState(IN_TRANSIT) 被调用
- [ ] LockedPath 在 RequestDispatch 后不受星图节点变化影响

---

## Implementation Notes

*Derived from ADR-0017 Decision section:*

```csharp
public DispatchOrder RequestDispatch(string shipId, string destinationNodeId) {
    // 1. 验证飞船状态
    var ship = ShipDataModel.GetShip(shipId);
    if (ship == null || ship.State != ShipState.DOCKED) {
        Debug.LogWarning($"[FleetDispatch] {shipId} is not DOCKED — cannot dispatch.");
        return null;
    }

    // 2. 路径计算（BFS）
    var path = StarMapPathfinder.FindPath(ship.DockedNodeId, destinationNodeId);
    if (path == null || path.Count < 1) {
        Debug.LogWarning($"[FleetDispatch] No path found from {ship.DockedNodeId} to {destinationNodeId}");
        return null;
    }

    // 3. 创建 DispatchOrder（路径快照）
    var order = new DispatchOrder {
        OrderId = $"order_{Guid.NewGuid():N}",
        ShipId = shipId,
        OriginNodeId = ship.DockedNodeId,
        DestinationNodeId = destinationNodeId,
        LockedPath = new List<string>(path), // 快照，不受后续变化影响
        CurrentHopIndex = 0,
        HopProgress = 0f,
        IsReturning = false,
    };

    _orders[order.OrderId] = order;

    // 4. 更新 ShipState → IN_TRANSIT
    ship.SetState(ShipState.IN_TRANSIT);

    // 5. 广播派遣事件
    OnDispatchCreated?.Invoke(order);

    return order;
}
```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 013: Update() 中 HopProgress 推进
- Story 014: CancelDispatch 返回路径
- Story 015: 到达 ENEMY 节点触发战斗或 U-4

---

## QA Test Cases

*Written by qa-lead at story creation.*

- **AC-1**: Non-DOCKED ship rejected
  - Given: ShipDataModel.GetState("ship-1") = IN_TRANSIT
  - When: RequestDispatch("ship-1", "node-B") is called
  - Then: returns null; no DispatchOrder created; ShipState unchanged

- **AC-2**: Valid dispatch creates correct DispatchOrder
  - Given: ShipDataModel.GetState("ship-1") = DOCKED at "node-A"; path exists to "node-B"
  - When: RequestDispatch("ship-1", "node-B") is called
  - Then: returns DispatchOrder with ShipId="ship-1", Origin="node-A", Dest="node-B", LockedPath=["node-A",...,"node-B"], CurrentHopIndex=0, HopProgress=0, IsReturning=false; ShipState → IN_TRANSIT

- **AC-3**: LockedPath snapshot is immutable after creation
  - Given: DispatchOrder created with path ["node-A", "node-B", "node-C"]
  - When: StarMapPathfinder.FindPath is called again and returns different path
  - Then: DispatchOrder.LockedPath still equals ["node-A", "node-B", "node-C"]

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/fleet/dispatch_order_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Foundation (ShipDataModel.GetShip, SetState; StarMapPathfinder.FindPath)
- Unlocks: Story 013 (hop advancement), Story 014 (cancel)
