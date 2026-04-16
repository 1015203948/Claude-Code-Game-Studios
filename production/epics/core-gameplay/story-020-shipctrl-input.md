# Story 020: ShipControlSystem — Input Processing (Dead Zone + Aim Assist + Multi-Touch)

> **Epic**: core-gameplay
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Manifest Version**: 2026-04-14

## Context

**GDD**: `design/gdd/ship-control-system.md`
**Requirement**: `TR-shipctrl-004`, `TR-shipctrl-005`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0018: Ship Control System Architecture
**ADR Decision Summary**: C-1 ShipState 门控；C-2 死区公式；C-3 禁止后退推力；C-4 右摇杆 aim assist 叠加；C-5 多点触控左右区域 fingerId 隔离。

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: EnhancedTouchSupport 虚拟摇杆 fingerId 追踪（ADR-0003）

**Control Manifest Rules (this layer)**:
- Required: C-1 CockpitActions 在 ShipState ∉ {IN_COCKPIT, IN_COMBAT} 时丢弃输入
- Forbidden: offset.y < 0 时 thrust > 0（后退推力禁止）
- Guardrail: 多指触控时 fingerId 追踪隔离

---

## Acceptance Criteria

*From GDD `design/gdd/ship-control-system.md` C-1~C-5:*

- [ ] C-1: ShipState ∉ {IN_COCKPIT, IN_COMBAT} 时丢弃所有输入
- [ ] C-2: 左摇杆死区 normalized = Clamp01((|offset| - 0.08) / (1 - 0.08))；DEAD_ZONE = 0.08
- [ ] C-3: offset.y < 0 时 thrust = 0（只允许前进推力）
- [ ] C-4: steer_total = steer_left + 0.5 × steer_right（AIM_ASSIST_COEFF = 0.5）
- [ ] C-5: 左/右摇杆区域各自锁定首个 fingerId，手指抬起时释放

---

## Implementation Notes

*Derived from ADR-0018 Decision section:*

```csharp
// C-1: ShipState 门控
void OnCockpitInputs(CockpitInputs inputs) {
    ShipState state = ShipDataModel.GetState(_playerInstanceId);
    if (state != ShipState.IN_COCKPIT && state != ShipState.IN_COMBAT) return;

    ProcessThrust(inputs.leftStick);
    ProcessSteer(inputs.leftStick, inputs.rightStick);
}

// C-2: 死区
float ApplyDeadZone(float offset) {
    const float DEAD_ZONE = 0.08f;
    if (Mathf.Abs(offset) < DEAD_ZONE) return 0f;
    return Mathf.Clamp01((Mathf.Abs(offset) - DEAD_ZONE) / (1f - DEAD_ZONE))
           * Mathf.Sign(offset);
}

// C-3: 禁止后退
float GetThrustInput() {
    float normalized = ApplyDeadZone(_leftStickY);
    return normalized > 0f ? normalized : 0f; // y<0 → thrust=0
}

// C-4: Aim assist
float GetSteerInput() {
    float steerLeft = ApplyDeadZone(_leftStickX);
    float steerRight = ApplyDeadZone(_rightStickX) * AIM_ASSIST_COEFF;
    return steerLeft + 0.5f * steerRight; // AIM_ASSIST_COEFF = 0.5
}
```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 019: FixedUpdate 物理执行（AddForce + MoveRotation）
- Story 021: SoftLockTarget + aimAngle

---

## QA Test Cases

*Written by qa-lead at story creation.*

- **AC-1**: ShipState gate blocks input when not in cockpit
  - Given: ShipState = DOCKED; CockpitInputs received
  - When: OnCockpitInputs is called
  - Then: no thrust applied; no steer applied

- **AC-2**: Dead zone eliminates small inputs
  - Given: offset = 0.05; DEAD_ZONE = 0.08
  - When: ApplyDeadZone(offset) is called
  - Then: returns 0

- **AC-3**: No reverse thrust (offset.y < 0)
  - Given: left stick y = -0.5 (down)
  - When: GetThrustInput() is called
  - Then: returns 0 (no backward thrust)

- **AC-4**: Aim assist adds right stick to turn
  - Given: steer_left = 0.4; steer_right = 0.6; AIM_ASSIST_COEFF = 0.5
  - When: GetSteerInput() is called
  - Then: returns 0.4 + (0.5 * 0.6) = 0.7

- **AC-5**: Multi-touch finger isolation
  - Given: finger 3 touches left zone first; finger 7 touches right zone first
  - When: finger 5 then touches left zone
  - Then: left zone still tracked by finger 3; finger 5 is ignored
  - When: finger 3 lifts
  - Then: left zone tracking released; next touch in left zone captures new fingerId

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/shipctrl/input_processing_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: foundation-runtime stories 011-016 (ShipInputManager); Story 019 (physics core)
- Unlocks: Story 021 (soft lock + FireRequested), Story 023 (state init)
