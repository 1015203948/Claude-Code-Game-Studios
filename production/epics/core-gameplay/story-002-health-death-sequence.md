# Story 002: HealthSystem — Death Sequence H-5

> **Epic**: core-gameplay
> **Status**: Complete
> **Layer**: Core
> **Type**: State Machine
> **Manifest Version**: 2026-04-14

## Context

**GDD**: `design/gdd/ship-health-system.md`
**Requirement**: `TR-health-002`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0014: Health System Architecture
**ADR Decision Summary**: Hull=0 时 ExecuteDeathSequence() 严格按 H-5 Step 1→2→3→4 顺序执行：OnShipDying → DestroyShip → [IsPlayerControlled?] OnPlayerShipDestroyed → OnShipDestroyed，同帧完成。

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: 无 post-cutoff API；事件广播使用 C# event。

**Control Manifest Rules (this layer)**:
- Required: 死亡序列四步骤必须同帧按序执行
- Forbidden: 禁止在 Step 1 (OnShipDying) 之后再次调用 HealthSystem
- Guardrail: OnShipDying 触发后 CombatSystem 不应再调用 ApplyDamage（防御性设计）

---

## Acceptance Criteria

*From GDD `design/gdd/ship-health-system.md` H-5, scoped to this story:*

- [ ] Hull=0 时 OnShipDying(instanceId) 在同一帧内广播一次（不多次）
- [ ] Step 2：调用 ShipDataModel.DestroyShip(instanceId)（状态变为 DESTROYED）
- [ ] Step 3：如果是玩家飞船（IsPlayerControlled==true）→ 广播 OnPlayerShipDestroyed(instanceId)
- [ ] Step 4：广播 OnShipDestroyed(instanceId)（通用销毁完成）
- [ ] 四步骤顺序：Step1 → Step2 → Step3/4，无重排
- [ ] 非玩家飞船（enemy instance）：跳过 Step 3
- [ ] OnShipDying 在 Step 2 之前广播，供 CombatSystem 订阅者检测胜负

---

## Implementation Notes

*Derived from ADR-0014 Decision section:*

```csharp
private void ExecuteDeathSequence(string instanceId) {
    // Step 1：广播 OnShipDying — 通知所有订阅者（CombatSystem、HUD、SFX、VFX）
    OnShipDying?.Invoke(instanceId);

    // Step 2：调用 DestroyShip — 状态变为 DESTROYED，通知星图清空 dockedFleet
    ShipDataModel.DestroyShip(instanceId);

    // Step 3：如果是玩家飞船，广播 OnPlayerShipDestroyed
    // （双视角切换系统订阅，强制退出驾驶舱）
    if (ShipDataModel.IsPlayerControlled(instanceId)) {
        OnPlayerShipDestroyed?.Invoke(instanceId);
    }

    // Step 4：广播 OnShipDestroyed — 通用销毁完成
    OnShipDestroyed?.Invoke(instanceId);
}

// C# event 定义
public event Action<string> OnShipDying;
public event Action<string> OnPlayerShipDestroyed;
public event Action<string> OnShipDestroyed;
```

ShipDataModel.IsPlayerControlled 需在 Foundation 层暴露。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: ApplyDamage 入口和 ShipState 门控
- Story 003: OnHullChanged（非死亡路径的 Hull 变化）

---

## QA Test Cases

*Written by qa-lead at story creation.*

- **AC-1**: Death sequence fires once on Hull=0
  - Given: ShipDataModel.GetState("ship-1") = IN_COCKPIT, Hull = 8
  - When: ApplyDamage("ship-1", 8f, DamageType.KINETIC) is called (Hull reaches 0)
  - Then: OnShipDying is invoked exactly once; OnShipDestroyed is invoked exactly once
  - Edge cases: multiple rapid ApplyDamage calls at Hull=1

- **AC-2**: Player ship triggers OnPlayerShipDestroyed
  - Given: ShipDataModel.IsPlayerControlled("player-ship") = true, Hull = 0
  - When: ExecuteDeathSequence("player-ship") is called
  - Then: OnShipDying → DestroyShip → OnPlayerShipDestroyed → OnShipDestroyed in order
  - Edge cases: non-player ship skips OnPlayerShipDestroyed

- **AC-3**: Steps execute in strict order
  - Given: A mock that tracks invocation order
  - When: ApplyDamage reduces Hull to 0
  - Then: Order is: (1) OnShipDying, (2) ShipDataModel.DestroyShip called, (3) [if player] OnPlayerShipDestroyed, (4) OnShipDestroyed

---

## Test Evidence

**Story Type**: State Machine
**Required evidence**: `tests/unit/health/death_sequence_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Completion Notes
**Completed**: 2026-04-15
**Criteria**: 7/7 passing
**Deviations**: ExecuteDeathSequence 已由 Story 001 完整实现（H-5 四步），Story 002 仅补充测试验证
**Test Evidence**: State Machine (Logic): `tests/unit/health/death_sequence_test.cs` — 7 test functions
**Code Review**: Skipped (lean mode)

## Dependencies

- Depends on: Story 001 (ApplyDamage 入口); Foundation layer (ShipDataModel.IsPlayerControlled)
- Unlocks: Story 004 (CombatSystem 订阅 OnShipDying 判定胜负)
