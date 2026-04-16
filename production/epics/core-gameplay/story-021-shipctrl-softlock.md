# Story 021: ShipControlSystem — Soft Lock + FireRequested

> **Epic**: core-gameplay
> **Status**: Ready
> **Layer**: Core
> **Type**: Integration
> **Manifest Version**: 2026-04-14

## Context

**GDD**: `design/gdd/ship-control-system.md`
**Requirement**: `TR-shipctrl-006`, `TR-shipctrl-009`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0018: Ship Control System Architecture
**ADR Decision Summary**: SoftLockTarget 在 LOCK_RANGE(80m) 内最近敌人中选取；aimAngle 每帧计算；aimAngle ≤ FIRE_ANGLE_THRESHOLD(15°) 时触发 FireRequested 事件；目标稳定（仅在离开范围或被摧毁时重新选取）。

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: 无 post-cutoff API。

**Control Manifest Rules (this layer)**:
- Required: aimAngle = Vector3.Angle(transform.forward, toEnemy.normalized)
- Forbidden: 禁止频繁跳变 SoftLockTarget（稳定性规则）
- Guardrail: FireRequested 在 aimAngle 满足时每帧触发（由 CombatSystem 消费）

---

## Acceptance Criteria

*From GDD `design/gdd/ship-control-system.md` L-1~L-3, B-2 formula:*

- [ ] SoftLockTarget 在 LOCK_RANGE(80m) 内最近敌人中选取
- [ ] 目标稳定：仅在目标离开范围或被摧毁时重新选取
- [ ] aimAngle = Vector3.Angle(ship.forward, toEnemy.normalized)
- [ ] aimAngle ≤ FIRE_ANGLE_THRESHOLD(15°) 时触发 FireRequested 事件
- [ ] aimAngle 超出阈值时 FireRequested 不触发

---

## Implementation Notes

*Derived from ADR-0018 Decision section and ship-combat-system.md B-2:*

```csharp
// aimAngle 计算（每帧更新）
float CalculateAimAngle() {
    if (_softLockTarget == null) return 360f;
    Vector3 toEnemy = (_softLockTarget.position - transform.position).normalized;
    return Vector3.Angle(transform.forward, toEnemy);
}

// L-1: SoftLockTarget 选择
void UpdateSoftLock() {
    if (_softLockTarget != null) {
        // 稳定性：检查目标是否仍在范围内或存在
        if (_softLockTarget == null || !IsInLockRange(_softLockTarget)) {
            ClearSoftLock();
        }
    }

    // 重新选取最近的
    if (_softLockTarget == null) {
        _softLockTarget = FindNearestEnemyInLockRange();
    }
}

// L-3: FireRequested 触发
void Update() {
    UpdateSoftLock();
    _aimAngle = CalculateAimAngle();
    OnAimAngleChanged?.Invoke(_aimAngle); // 供 HUD 使用

    if (_aimAngle <= FIRE_ANGLE_THRESHOLD) {
        FireRequested?.Invoke(); // CombatSystem 订阅此事件
    }
}
```

CombatSystem 订阅 `ShipControlSystem.FireRequested` 事件（ADR-0013 B-2）。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 005: CombatSystem 订阅 FireRequested 并执行 FireWeapon
- Story 004: CombatSystem 状态机

---

## QA Test Cases

*Written by qa-lead at story creation.*

- **AC-1**: SoftLockTarget selects nearest in LOCK_RANGE
  - Given: enemy-A at 60m; enemy-B at 40m; both in LOCK_RANGE(80m)
  - When: UpdateSoftLock() runs (target is null)
  - Then: SoftLockTarget = enemy-B (nearest)
  - Edge cases: no enemies in range → SoftLockTarget remains null

- **AC-2**: SoftLockTarget persists until out of range
  - Given: SoftLockTarget = enemy-A at 60m; new enemy-B at 30m enters range
  - When: UpdateSoftLock() runs
  - Then: SoftLockTarget remains enemy-A (stability rule)
  - When: enemy-A moves to 90m (outside LOCK_RANGE)
  - Then: SoftLockTarget cleared; enemy-B selected

- **AC-3**: FireRequested fires when aimAngle ≤ 15°
  - Given: SoftLockTarget set; aimAngle = 12°
  - When: Update() runs
  - Then: FireRequested event is invoked
  - Given: aimAngle = 18°
  - When: Update() runs
  - Then: FireRequested NOT invoked

- **AC-4**: aimAngle calculation correct
  - Given: ship.forward = (0, 0, 1); enemy at (0, 0, 101) relative → toEnemy = (0, 0, 1)
  - When: CalculateAimAngle() is called
  - Then: returns 0° (perfect alignment)
  - Given: enemy at (50, 0, 87) → toEnemy normalized = (0.5, 0, 0.866)
  - When: CalculateAimAngle() is called
  - Then: returns approximately 30°

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/unit/shipctrl/soft_lock_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: foundation-runtime (ShipInputManager); Story 019 (physics); Story 004 (CombatSystem subscribes FireRequested)
- Unlocks: Story 005 (fire rate integration)
