# Story 019: ShipControlSystem — Physics Core (Thrust + Velocity Clamp + Turn)

> **Epic**: core-gameplay
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Manifest Version**: 2026-04-14

## Context

**GDD**: `design/gdd/ship-control-system.md`
**Requirement**: `TR-shipctrl-001`, `TR-shipctrl-002`, `TR-shipctrl-003`, `TR-shipctrl-006`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0018: Ship Control System Architecture
**ADR Decision Summary**: Rigidbody.AddForce 施推力；软速度上限；MoveRotation 转向；角速度锁定；Y 轴位置固定（FLIGHT_PLANE_Y）。

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: Rigidbody.AddForce / MoveRotation / linearDamping API 一致；Rigidbody.drag 已重命名为 linearDamping（Unity 6+）

**Control Manifest Rules (this layer)**:
- Required: FixedUpdate 内执行物理（Time.fixedDeltaTime）
- Forbidden: 禁止 rb.velocity 直接赋值（只能用 AddForce）
- Guardrail: 软截断保留惯性

---

## Acceptance Criteria

*From GDD `design/gdd/ship-control-system.md` P-1~P-6, L-1:*

- [ ] P-1: thrust_input > 0 时 rb.AddForce(transform.forward * ThrustPower * thrust_input, ForceMode.Force)
- [ ] P-2: velocity.magnitude > SHIP_MAX_SPEED 时施加反向力（软上限）
- [ ] P-3: 不调用 rb.velocity = ...（惯性保留）
- [ ] P-4: rb.MoveRotation(rb.rotation * Quaternion.Euler(0, TurnSpeed * steer * dt, 0))
- [ ] P-5: rb.angularVelocity = Vector3.zero 每 FixedUpdate 开始时重置
- [ ] P-6: rb.position.y = FLIGHT_PLANE_Y；速度 Y 分量归零

---

## Implementation Notes

*Derived from ADR-0018 Decision section:*

```csharp
void FixedUpdate() {
    // P-5: 角速度锁定（每帧重置）
    rb.angularVelocity = Vector3.zero;

    float thrust_input = GetThrustInput();  // C-2, C-3 处理
    float steer = GetSteerInput();           // C-4 处理

    // P-1: 推力
    if (thrust_input > 0f) {
        rb.AddForce(transform.forward * ThrustPower * thrust_input, ForceMode.Force);
    }

    // P-4: 转向
    rb.MoveRotation(rb.rotation * Quaternion.Euler(
        0f,
        TurnSpeed * steer * Time.fixedDeltaTime,
        0f));

    // P-2: 软速度上限
    float excess = rb.velocity.magnitude - SHIP_MAX_SPEED;
    if (excess > 0f) {
        rb.AddForce(-rb.velocity.normalized * excess * SPEED_CLAMP_STIFFNESS, ForceMode.Force);
    }

    // P-6: 水平面约束
    if (rb.position.y != FLIGHT_PLANE_Y) {
        rb.position = new Vector3(rb.position.x, FLIGHT_PLANE_Y, rb.position.z);
    }
    Vector3 v = rb.velocity;
    if (Mathf.Abs(v.y) > 0.001f) {
        rb.velocity = new Vector3(v.x, 0f, v.z);
    }
}
```

ThrustPower 和 TurnSpeed 从 ShipDataModel 缓存（S-1 初始化时）。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 020: 输入处理（死区、后退推力禁止、多点触控）
- Story 021: SoftLockTarget + aimAngle + FireRequested
- Story 023: 状态初始化/清理

---

## QA Test Cases

*Written by qa-lead at story creation.*

- **AC-1**: Thrust applies correct force
  - Given: rb.velocity = Vector3.zero; thrust_input = 1.0; ThrustPower = 50
  - When: FixedUpdate runs one frame (dt = 0.02s)
  - Then: rb.velocity magnitude > 0; force applied in transform.forward direction

- **AC-2**: Soft speed clamp applies braking force
  - Given: rb.velocity.magnitude = SHIP_MAX_SPEED + 5; excess = 5
  - When: FixedUpdate runs
  - Then: negative force applied; next frame velocity magnitude < previous

- **AC-3**: Y position fixed to FLIGHT_PLANE_Y
  - Given: rb.position.y = FLIGHT_PLANE_Y + 10
  - When: FixedUpdate runs
  - Then: rb.position.y = FLIGHT_PLANE_Y; Y velocity = 0

- **AC-4**: Angular velocity reset each frame
  - Given: rb.angularVelocity = (0, 10, 0) from previous physics update
  - When: FixedUpdate runs
  - Then: rb.angularVelocity = Vector3.zero after reset

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/shipctrl/physics_core_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: foundation-runtime stories 011-016 (ShipInputManager, DualJoystick, ShipControlSystem core)
- Unlocks: Story 020 (input processing), Story 021 (soft lock), Story 023 (state transitions)
