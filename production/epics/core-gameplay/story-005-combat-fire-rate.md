# Story 005: CombatSystem — Fire Rate Timer + Auto-Fire

> **Epic**: core-gameplay
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Manifest Version**: 2026-04-14

## Context

**GDD**: `design/gdd/ship-combat-system.md`
**Requirement**: `TR-combat-002`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0013: Combat System Architecture
**ADR Decision Summary**: 武器射速计时器用 Time.deltaTime 累加（帧率独立）；每帧检测 aimAngle ≤ FIRE_ANGLE_THRESHOLD(15°) 时触发 FireWeapon()；_fireTimer 达 1/WEAPON_FIRE_RATE 后重置。

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: 验证：Weapon fire rate timer accurate across frame rate variations (use accumulated deltaTime, not real-time)

**Control Manifest Rules (this layer)**:
- Required: _fireTimer += Time.deltaTime（不用实时）
- Forbidden: 禁止在 15° 外提前开火
- Guardrail: 武器射速精度 60fps 60帧恰好触发 60 次

---

## Acceptance Criteria

*From GDD `design/gdd/ship-combat-system.md` B-2, B-3, weapon_fire_rate_timer formula:*

- [ ] _fireTimer 初始值 = 0f
- [ ] 每帧 _fireTimer += Time.deltaTime（帧率独立）
- [ ] aimAngle ≤ 15° 且 _fireTimer ≥ 1/WEAPON_FIRE_RATE 时 FireWeapon() 执行，_fireTimer = 0f
- [ ] aimAngle > 15° 时 FireWeapon() 不执行（即使 _fireTimer 已就绪）
- [ ] _fireTimer 累积超过 2× WEAPON_FIRE_RATE 时最多一次开火（不能"充能"）
- [ ] 60fps 下 1 秒恰好触发 1 次开火（WEAPON_FIRE_RATE = 1.0）

---

## Implementation Notes

*Derived from ADR-0013 Decision section:*

```csharp
// 玩家武器
_weaponFireTimer += Time.deltaTime;  // ⚠️ 用 Time.deltaTime，不用实时
if (_weaponFireTimer >= (1f / WEAPON_FIRE_RATE) && aimAngle <= FIRE_ANGLE_THRESHOLD) {
    FireWeapon();
    _weaponFireTimer = 0f;
}

// 常量（来自 GDD）
const float FIRE_ANGLE_THRESHOLD = 15f;  // degrees
const float WEAPON_FIRE_RATE = 1.0f;    // shots/sec
const float WEAPON_RANGE = 200f;          // meters

// aimAngle 由 ShipControlSystem 通过 C# event 或只读属性提供
// 订阅: ShipControlSystem.FireRequested 事件（ADR-0018 L-3）
```

aimAngle 从 ShipControlSystem 订阅（ShipControlSystem.OnAimAngleChanged event 或 FireRequested 事件）。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 006: FireWeapon() 中 Raycast 命中检测
- Story 021: ShipControlSystem 的 aimAngle 计算 + FireRequested 事件发布

---

## QA Test Cases

*Written by qa-lead at story creation.*

- **AC-1**: Fire rate timer accumulates at 60fps
  - Given: WEAPON_FIRE_RATE = 1.0, _weaponFireTimer = 0, aimAngle = 0°
  - When: 60 frames pass at 60fps (each deltaTime ≈ 0.0167s)
  - Then: _weaponFireTimer = 1.0; next frame fire is triggered; _weaponFireTimer resets to 0

- **AC-2**: Fire blocked when aimAngle > 15°
  - Given: _weaponFireTimer >= 1.0, aimAngle = 20°
  - When: FireWeapon() condition checked
  - Then: FireWeapon() is NOT called; _weaponFireTimer remains >= 1.0

- **AC-3**: Fire triggers when both conditions met
  - Given: _weaponFireTimer = 0.9, aimAngle = 10°
  - When: one frame passes (deltaTime = 0.0167s)
  - Then: _weaponFireTimer = 0.9167; condition now met; FireWeapon() called; _weaponFireTimer = 0

- **AC-4**: No over-firing from accumulated timer
  - Given: _weaponFireTimer = 2.0 (accumulated 2 seconds worth)
  - When: FireWeapon() condition is true
  - Then: FireWeapon() called once; _weaponFireTimer = 0; no second fire in same frame

- **AC-5**: Frame-rate independence at 30fps
  - Given: WEAPON_FIRE_RATE = 1.0, aimAngle = 0°
  - When: game runs at 30fps for 1 second
  - Then: 30 frames pass; _weaponFireTimer ≈ 1.0; exactly 1 fire triggered

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/combat/fire_rate_timer_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 004 (state machine active); ShipControlSystem aimAngle available (Story 021)
- Unlocks: Story 006 (Raycast hit detection integrates with fire trigger)
