# Story 013: FleetDispatch — Transit Hop Advancement

> **Epic**: core-gameplay
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Manifest Version**: 2026-04-14

## Context

**GDD**: `design/gdd/fleet-dispatch-system.md`
**Requirement**: `TR-fleet-003`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0017: Fleet Dispatch Architecture
**ADR Decision Summary**: Update() 每帧推进所有活跃 DispatchOrder：HopProgress += SimClock.Instance.DeltaTime；到达 FLEET_TRAVEL_TIME(3s/hop) 时 CurrentHopIndex++，触发状态转换。

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: 纯数据追踪，无 Unity 物理 API。

**Control Manifest Rules (this layer)**:
- Required: simDelta 来自 SimClock.Instance.DeltaTime（帧率独立）
- Forbidden: 禁止使用 Time.deltaTime（应用 SimClock 而非渲染帧）
- Guardrail: SimRate=0 时 Transit 不推进

---

## Acceptance Criteria

*From GDD `design/gdd/fleet-dispatch-system.md` D-5, D-6:*

- [ ] Update() 每帧：HopProgress += SimClock.Instance.DeltaTime
- [ ] HopProgress ≥ FLEET_TRAVEL_TIME(3.0s) → HopProgress -= FLEET_TRAVEL_TIME，CurrentHopIndex++
- [ ] 到达目的地（CurrentHopIndex ≥ LockedPath.Count - 1）→ ArrivedAtDestination() 调用
- [ ] SimRate=0 时 Transit 不推进
- [ ] 每帧遍历所有活跃订单，O(order_count)

---

## Implementation Notes

*Derived from ADR-0017 Decision section:*

```csharp
private const float FLEET_TRAVEL_TIME = 3.0f; // 秒/hop

void Update() {
    if (SimClock.Instance == null) return;
    float simDelta = SimClock.Instance.DeltaTime; // 帧率独立
    if (simDelta <= 0f) return;

    var orders = _orders.Values.ToList(); // 复制快照避免迭代中修改
    foreach (var order in orders) {
        AdvanceOrder(order, simDelta);
    }
}

void AdvanceOrder(DispatchOrder order, float delta) {
    if (order.IsReturning) {
        AdvanceReturn(order, delta);
        return;
    }

    order.HopProgress += delta;

    while (order.HopProgress >= FLEET_TRAVEL_TIME) {
        order.HopProgress -= FLEET_TRAVEL_TIME;
        order.CurrentHopIndex++;

        if (order.CurrentHopIndex >= order.LockedPath.Count - 1) {
            ArrivedAtDestination(order);
            return;
        }
    }
}
```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 012: RequestDispatch 创建订单
- Story 014: CancelDispatch 返回路径
- Story 015: ArrivedAtDestination 状态转换逻辑

---

## QA Test Cases

*Written by qa-lead at story creation.*

- **AC-1**: 10 ticks × 3s/hop = 30s 后到达目的地
  - Given: DispatchOrder with 10-hop path; FLEET_TRAVEL_TIME = 3.0s
  - When: SimClock.DeltaTime = 1.0s for 3 ticks (each tick advances one hop)
  - Then: after 10 ticks (30s): CurrentHopIndex = 9 (last index); ArrivedAtDestination called

- **AC-2**: HopProgress fractional carry-over
  - Given: HopProgress = 2.5s; SimDeltaTime = 1.0s; FLEET_TRAVEL_TIME = 3.0s
  - When: AdvanceOrder is called
  - Then: HopProgress = 3.5s → loop fires → HopProgress = 0.5s, CurrentHopIndex++

- **AC-3**: SimRate=0 no advancement
  - Given: SimClock.Instance.DeltaTime = 0 (SimRate = 0)
  - When: Update() is called
  - Then: no advancement; HopProgress unchanged

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/fleet/transit_hop_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 012 (DispatchOrder creation); SimClock (Foundation)
- Unlocks: Story 015 (arrival integration)
