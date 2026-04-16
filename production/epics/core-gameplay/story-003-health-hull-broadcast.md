# Story 003: HealthSystem — HullRatio + OnHullChanged Broadcast

> **Epic**: core-gameplay
> **Status**: Complete
> **Layer**: Core
> **Type**: Integration
> **Manifest Version**: 2026-04-14

## Context

**GDD**: `design/gdd/ship-health-system.md`
**Requirement**: `TR-health-003`, `TR-health-004`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0014: Health System Architecture
**ADR Decision Summary**: HealthSystem 单例挂载于 MasterScene；HullRatio = CurrentHull / MaxHull [0,1] 只读属性；OnHullChanged 在 Hull > 0 时广播（Hull=0 走死亡序列，不走 OnHullChanged）。

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: 无 post-cutoff API。

**Control Manifest Rules (this layer)**:
- Required: HealthSystem 单例挂载 MasterScene（跨场景可用）
- Forbidden: 禁止 OnHullChanged 在 Hull=0 时广播
- Guardrail: C# event ~40B/调用，零 GC

---

## Acceptance Criteria

*From GDD `design/gdd/ship-health-system.md`, scoped to this story:*

- [ ] HullRatio = CurrentHull / MaxHull，返回值在 [0.0, 1.0] 范围内
- [ ] Hull 从 30→22 时（OnHullChanged）广播 OnHullChanged(instanceId, 22, 100)
- [ ] Hull 从 22→0 时不广播 OnHullChanged（走死亡序列）
- [ ] HealthSystem.Instance 单例在 MasterScene 激活时可用
- [ ] OnHullChanged 每次 Hull > 0 变化时广播，不遗漏

---

## Implementation Notes

*Derived from ADR-0014 Decision section:*

```csharp
public float HullRatio {
    get {
        float maxHull = ShipDataModel.GetMaxHull(instanceId);
        float currentHull = ShipDataModel.GetCurrentHull(instanceId);
        return Mathf.Clamp01(currentHull / maxHull);
    }
}

// ApplyDamage 中：
if (newHull <= 0f) {
    ExecuteDeathSequence(instanceId);  // 不走 OnHullChanged
} else {
    OnHullChanged?.Invoke(instanceId, newHull, maxHull);
}
```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: ApplyDamage 入口
- Story 002: 死亡序列
- Story 016 (ColonySystem): OnResourcesUpdated 集成

---

## QA Test Cases

*Written by qa-lead at story creation.*

- **AC-1**: HullRatio calculation correct at boundaries
  - Given: CurrentHull = 0, MaxHull = 100
  - When: HullRatio is read
  - Then: returns 0.0
  - Given: CurrentHull = 100, MaxHull = 100
  - When: HullRatio is read
  - Then: returns 1.0
  - Given: CurrentHull = 50, MaxHull = 100
  - When: HullRatio is read
  - Then: returns 0.5

- **AC-2**: OnHullChanged not fired at Hull=0
  - Given: Hull = 1, ApplyDamage called with rawDamage = 2
  - When: HealthSystem.ApplyDamage processes
  - Then: ExecuteDeathSequence is called; OnHullChanged is NOT called

- **AC-3**: HealthSystem singleton accessible
  - Given: MasterScene is active
  - When: HealthSystem.Instance is accessed
  - Then: returns non-null reference

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/unit/health/hull_broadcast_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Completion Notes
**Completed**: 2026-04-15
**Criteria**: 5/5 passing
**Deviations**: None — HullRatio + OnHullChanged 已由 Story 001 实现，Story 003 仅补充测试
**Test Evidence**: Integration: `tests/unit/health/hull_broadcast_test.cs` — 9 test functions
**Code Review**: Skipped (lean mode)

## Dependencies

- Depends on: Story 001 (ApplyDamage), Story 002 (death sequence); Foundation layer (ShipDataModel)
- Unlocks: Story 016 (ColonySystem integration), HUD stories (not yet created)
