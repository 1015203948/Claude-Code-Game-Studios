# Story 014: FleetDispatch — CancelDispatch Return Path

> **Epic**: core-gameplay
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Manifest Version**: 2026-04-14

## Context

**GDD**: `design/gdd/fleet-dispatch-system.md`
**Requirement**: `TR-fleet-004`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0017: Fleet Dispatch Architecture
**ADR Decision Summary**: CancelDispatch 反向路径 Take(CurrentHopIndex+1).Reverse()；重置 CurrentHopIndex=0，HopProgress=0，IsReturning=true；到达 OriginNodeId 后 ShipState → DOCKED。

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: 纯数据操作，无 Unity API。

**Control Manifest Rules (this layer)**:
- Required: CancelDispatch 后 ShipState 保持 IN_TRANSIT 直到返回 DOCKED
- Forbidden: 取消中飞船不能被重新派遣
- Guardrail: 返回路径使用 Take(CurrentHopIndex+1)

---

## Acceptance Criteria

*From GDD `design/gdd/fleet-dispatch-system.md` D-10:*

- [ ] CancelDispatch(shipId) 找到对应 DispatchOrder
- [ ] 反向路径 = LockedPath.Take(CurrentHopIndex+1).Reverse()
- [ ] IsReturning = true；CurrentHopIndex = 0；HopProgress = 0
- [ ] 返回过程中 AdvanceReturn 减少 CurrentHopIndex
- [ ] 到达 OriginNodeId 后 ShipState → DOCKED，CloseOrder

---

## Implementation Notes

*Derived from ADR-0017 Decision section:*

```csharp
public void CancelDispatch(string shipId) {
    var order = _orders.Values.FirstOrDefault(o => o.ShipId == shipId);
    if (order == null) return;

    // 计算反向路径（Origin → CurrentHop-1）
    var returnPath = order.LockedPath.Take(order.CurrentHopIndex + 1).Reverse().ToList();

    order.LockedPath = returnPath;
    order.CurrentHopIndex = 0;
    order.HopProgress = 0f;
    order.IsReturning = true;
}

void AdvanceReturn(DispatchOrder order, float delta) {
    order.HopProgress += delta;

    while (order.HopProgress >= FLEET_TRAVEL_TIME) {
        order.HopProgress -= FLEET_TRAVEL_TIME;
        order.CurrentHopIndex++;

        if (order.CurrentHopIndex >= order.LockedPath.Count - 1) {
            // 到达原点
            var ship = ShipDataModel.GetShip(order.ShipId);
            if (ship != null) ship.SetState(ShipState.DOCKED);
            RemoveOrder(order);
            return;
        }
    }
}
```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 012: RequestDispatch 创建订单
- Story 013: AdvanceOrder 正向推进

---

## QA Test Cases

*Written by qa-lead at story creation.*

- **AC-1**: CancelDispatch sets correct reverse path
  - Given: LockedPath = ["A", "B", "C", "D"]; CurrentHopIndex = 2 (at C)
  - When: CancelDispatch("ship-1") is called
  - Then: LockedPath = ["A", "B", "C"] (reversed = ["C", "B", "A"]; then Take(3) = ["C", "B", "A"]); IsReturning = true; CurrentHopIndex = 0

- **AC-2**: Return advances in reverse
  - Given: order.IsReturning = true; LockedPath = ["C", "B", "A"]; CurrentHopIndex = 0
  - When: AdvanceReturn called with delta = 3.0
  - Then: HopProgress = 3.0 → CurrentHopIndex = 1 (at B)

- **AC-3**: Return to origin → DOCKED
  - Given: IsReturning = true; LockedPath = ["C"] (at last index); CurrentHopIndex = 0; delta = 3.0
  - When: AdvanceReturn processes
  - Then: CurrentHopIndex = 1 ≥ LockedPath.Count-1 (0); ShipDataModel.SetState(DOCKED); RemoveOrder called

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/fleet/cancel_dispatch_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 012 (DispatchOrder creation); Story 013 (AdvanceOrder)
- Unlocks: Story 015 (enemy arrival integration)
