# Story 015: FleetDispatch — Enemy Arrival + U-4 Path

> **Epic**: core-gameplay
> **Status**: Ready
> **Layer**: Core
> **Type**: Integration
> **Manifest Version**: 2026-04-14

## Context

**GDD**: `design/gdd/fleet-dispatch-system.md`
**Requirement**: `TR-fleet-005`, `TR-fleet-006`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0017: Fleet Dispatch Architecture
**ADR Decision Summary**: 到达 ENEMY 节点：玩家在驾驶舱 → CombatSystem.BeginCombat()；无人值守 → ResolveUnattendedCombat(U-4)；ShipDataModel.Destroy() 时 FleetDispatch.OnShipDestroyed() 清理孤立订单。

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: 纯逻辑集成。

**Control Manifest Rules (this layer)**:
- Required: CombatSystem.BeginCombat 调用在 CockpitScene 加载完成后
- Forbidden: ShipDataModel.Destroy() 不触发 HealthSystem 事件（U-4）
- Guardrail: OnShipDestroyed 回调防止孤立订单残留

---

## Acceptance Criteria

*From GDD `design/gdd/fleet-dispatch-system.md` D-9, D-11:*

- [ ] 到达 ENEMY 节点 + 玩家在驾驶舱 → CombatSystem.BeginCombat(shipId, nodeId)
- [ ] 到达 ENEMY 节点 + 无人值守 → ResolveUnattendedCombat(shipId, nodeId)（U-4 路径）
- [ ] 到达 NEUTRAL/PLAYER 节点 → ShipDataModel.SetState(DOCKED)；StarMapSystem.OnFleetArrived
- [ ] ShipDataModel.Destroy() 在 IN_TRANSIT 期间被调用 → FleetDispatch.OnShipDestroyed() → RemoveOrder(order)
- [ ] CombatSystem.BeginCombat 前检查 ShipState（必须是 IN_TRANSIT）

---

## Implementation Notes

*Derived from ADR-0017 Decision section:*

```csharp
void ArrivedAtDestination(DispatchOrder order) {
    string arrivalNodeId = order.LockedPath[^1];
    var node = StarMapData.GetNode(arrivalNodeId);
    var ship = ShipDataModel.GetShip(order.ShipId);

    if (ship == null) {
        // EC-8：ShipDataModel.Destroy() 已调用，清理孤立订单
        RemoveOrder(order);
        return;
    }

    // 更新飞船位置
    ship.DockedNodeId = arrivalNodeId;

    if (node.NodeType == StarNodeType.ENEMY) {
        if (ship.IsPlayerControlled) {
            // 玩家飞船在驾驶舱中：触发驾驶舱战斗
            CombatSystem.Instance.BeginCombat(order.ShipId, arrivalNodeId);
        } else {
            // 无人值守：U-4 路径
            ResolveUnattendedCombat(order.ShipId, arrivalNodeId, node);
        }
    } else {
        // 占领节点 → DOCKED
        ship.SetState(ShipState.DOCKED);
        StarMapSystem.OnFleetArrived(order.ShipId, arrivalNodeId);
    }

    RemoveOrder(order);
}

public void OnShipDestroyed(string shipId) {
    var order = _orders.Values.FirstOrDefault(o => o.ShipId == shipId);
    if (order != null) {
        Debug.Log($"[FleetDispatch] Removing orphaned order {order.OrderId} for destroyed ship {shipId}");
        RemoveOrder(order);
    }
}
```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 004: CombatSystem.BeginCombat 内部状态机
- Story 007: ResolveUnattendedCombat 详细逻辑

---

## QA Test Cases

*Written by qa-lead at story creation.*

- **AC-1**: Enemy node arrival → cockpit combat triggered
  - Given: ArrivedAtDestination called; node.NodeType = ENEMY; ship.IsPlayerControlled = true; ShipState = IN_TRANSIT
  - When: ArrivedAtDestination processes
  - Then: CombatSystem.Instance.BeginCombat(shipId, nodeId) called; ShipState unchanged (combat system will change it)

- **AC-2**: Enemy node arrival → unattended combat U-4
  - Given: ArrivedAtDestination called; node.NodeType = ENEMY; ship.IsPlayerControlled = false
  - When: ArrivedAtDestination processes
  - Then: ResolveUnattendedCombat(shipId, nodeId) called; ShipDataModel.Destroy() called bypassing HealthSystem

- **AC-3**: ShipDataModel.Destroy() cleans orphaned order
  - Given: DispatchOrder exists for "ship-1"; "ship-1" is destroyed via ShipDataModel.Destroy()
  - When: FleetDispatch.OnShipDestroyed("ship-1") is called
  - Then: DispatchOrder is removed; no orphaned order remains in registry

- **AC-4**: Neutral/Player node → DOCKED
  - Given: ArrivedAtDestination called; node.NodeType = NEUTRAL; ship exists
  - When: ArrivedAtDestination processes
  - Then: ShipDataModel.SetState(DOCKED) called; StarMapSystem.OnFleetArrived called; order removed

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/fleet/enemy_arrival_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 004 (CombatSystem.BeginCombat); Story 007 (ResolveUnattendedCombat); Story 012 (DispatchOrder)
- Unlocks: Story 016 (colony resource tick integration)
