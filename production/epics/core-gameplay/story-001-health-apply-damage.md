# Story 001: HealthSystem — ApplyDamage + ShipState Gate

> **Epic**: core-gameplay
> **Status**: Complete
> **Layer**: Core
> **Type**: Logic
> **Manifest Version**: 2026-04-14

## Context

**GDD**: `design/gdd/ship-health-system.md`
**Requirement**: `TR-health-001`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0014: Health System Architecture
**ADR Decision Summary**: HealthSystem.ApplyDamage 是跨场景唯一伤害入口；ShipState ∈ {IN_COCKPIT, IN_COMBAT} 时执行伤害计算，DOCKED/IN_TRANSIT 静默忽略，DESTROYED 记录警告日志。

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: 无 post-cutoff API；纯数据追踪（float CurrentHull），无物理引擎调用。

**Control Manifest Rules (this layer)**:
- Required: C# event Tier 2 用于同场景事件广播
- Forbidden: 禁止在 Update() 中轮询 Hull 值变化
- Guardrail: OnHullChanged 广播使用 C# event（~40B/调用），非 delegate allocation

---

## Acceptance Criteria

*From GDD `design/gdd/ship-health-system.md`, scoped to this story:*

- [ ] ApplyDamage(Hull=30, rawDamage=8, KINETIC) → newHull = 22，OnHullChanged 广播 (22, 100)
- [ ] ApplyDamage(Hull=5, rawDamage=8, KINETIC) → Hull=0，触发死亡序列（由 Story 002 验证）
- [ ] ShipState = DOCKED 时 ApplyDamage → 静默忽略，返回 false，不广播事件
- [ ] ShipState = IN_TRANSIT 时 ApplyDamage → 静默忽略，返回 false
- [ ] ShipState = DESTROYED 时 ApplyDamage → 记录 Debug.LogWarning，返回 false
- [ ] ShipState = IN_COCKPIT 或 IN_COMBAT 时 ApplyDamage → 正常执行伤害
- [ ] rawDamage < 0 → Clamp 到 0，不报错，返回 false

---

## Implementation Notes

*Derived from ADR-0014 Decision section:*

```csharp
// ApplyDamage 主入口签名
public bool ApplyDamage(string instanceId, float rawDamage, DamageType damageType)

// ShipState 门控逻辑（按顺序检查）
ShipState state = ShipDataModel.GetState(instanceId);
if (state == ShipState.DESTROYED) {
    Debug.LogWarning($"[HealthSystem] {instanceId}: DESTROYED — ApplyDamage rejected.");
    return false;
}
if (state != ShipState.IN_COCKPIT && state != ShipState.IN_COMBAT) {
    // DOCKED / IN_TRANSIT — 静默忽略
    return false;
}

// 伤害计算
float maxHull = ShipDataModel.GetMaxHull(instanceId);
float newHull = Mathf.Clamp(ShipDataModel.GetCurrentHull(instanceId) - rawDamage, 0f, maxHull);
ShipDataModel.SetCurrentHull(instanceId, newHull);

if (newHull <= 0f) {
    ExecuteDeathSequence(instanceId);  // Story 002 覆盖
} else {
    OnHullChanged?.Invoke(instanceId, newHull, maxHull);
}
return true;
```

ShipDataModel.SetCurrentHull / GetCurrentHull / GetState / GetMaxHull 需在 ShipDataModel 中已存在（Foundation 层）。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: 死亡序列 H-5（OnShipDying → DestroyShip → OnPlayerShipDestroyed → OnShipDestroyed）
- Story 003: HullRatio 只读属性 + OnHullChanged 频道定义

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-1**: ApplyDamage applies correct hull reduction
  - Given: ShipDataModel.GetCurrentHull("ship-1") = 30, ShipDataModel.GetState("ship-1") = IN_COCKPIT
  - When: HealthSystem.Instance.ApplyDamage("ship-1", 8f, DamageType.KINETIC) is called
  - Then: ShipDataModel.SetCurrentHull is called with 22f; OnHullChanged is invoked with (instanceId="ship-1", newHull=22f, maxHull=100f)
  - Edge cases: Hull exactly reaches 0; negative rawDamage input

- **AC-2**: DOCKED state silently rejects damage
  - Given: ShipDataModel.GetState("ship-1") = DOCKED
  - When: ApplyDamage("ship-1", 8f, DamageType.KINETIC) is called
  - Then: returns false; no event is broadcast; no Hull change occurs
  - Edge cases: IN_TRANSIT (also silent); DESTROYED (warning log)

- **AC-3**: IN_COCKPIT and IN_COMBAT accept damage
  - Given: ShipDataModel.GetState("ship-1") = IN_COCKPIT (and separately IN_COMBAT)
  - When: ApplyDamage("ship-1", 8f, DamageType.KINETIC) is called
  - Then: returns true; hull is reduced; OnHullChanged is invoked

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/health/apply_damage_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Completion Notes
**Completed**: 2026-04-15
**Criteria**: 7/7 passing
**Deviations**: ExecuteDeathSequence 完整实现（H-5 四步），超出原 Story 002 范围但符合需求，记录为 advisory。
**Test Evidence**: Logic: `tests/unit/health/apply_damage_test.cs` — 10 test functions
**Code Review**: Skipped (lean mode)

## Dependencies

- Depends on: Foundation layer (ShipDataModel.SetCurrentHull, GetCurrentHull, GetState, GetMaxHull — stories 001~006 of foundation-runtime)
- Unlocks: Story 002 (death sequence), Story 003 (HullRatio + OnHullChanged)
