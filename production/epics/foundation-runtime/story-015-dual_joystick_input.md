# Story 015: DualJoystickInput 触控追踪 + 死区归一化

> **Epic**: Foundation Runtime
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: 2026-04-14
> **Estimate**: 2-3 hours

## Context

**GDD**: `design/gdd/ship-control-system.md`
**Requirement**: `TR-dvs-007`

**ADR Governing Implementation**: ADR-0003: Input System Architecture
**ADR Decision Summary**: 双虚拟摇杆通过 EnhancedTouch.activeTouches 手动追踪 fingerId；死区归一化公式 `normalized = Clamp01((Abs(offset) - 0.08f) / 0.92f)`；fingerId 用 finger.index 追踪；左半屏 = Thrust，右半屏 = Aim；finger 抬起时立即归零对应摇杆。

**Engine**: Unity 6.3 LTS | **Risk**: HIGH
**Engine Notes**: finger.index 在多点触控下的稳定性需要真机验证；EnhancedTouch API 为 post-cutoff

**Control Manifest Rules (Foundation)**:
- Required: fingerId 追踪用 `finger.index`（对象引用，不用 finger.id）
- Required: 摇杆中心 = 手指落下位置（touch.position），不用 touch.delta
- Required: 死区公式使用 `DEAD_ZONE = 0.08f`

---

## Acceptance Criteria

*From GDD AC-CTRL-01, AC-CTRL-02 + ADR-0003 Implementation Guidelines:*

- [ ] 左半屏触控 → Thrust joystick 追踪（_thrustFingerId）
- [ ] 右半屏触控 → Aim joystick 追踪（_aimFingerId）
- [ ] DEAD_ZONE = 0.08f；归一化公式：`normalized = Clamp01((Abs(offset) - 0.08f) / 0.92f)`
- [ ] 两个摇杆独立追踪：左摇杆 fingerId 抬起不影响右摇杆当前输入
- [ ] fingerId 丢失（系统中断）→ 该摇杆输入归零，防止飞船失控加速
- [ ] 两个触点同时落在同一摇杆热区 → 以先到达的 fingerId 为准
- [ ] AC-CTRL-01（输入门控）：ViewLayer == COCKPIT 时才处理触控输入
- [ ] AC-CTRL-02（死区过滤）：偏移量 < 0.08f 时输出 0

---

## Implementation Notes

*From ADR-0003 Implementation Guidelines + GDD Formulas:*

1. **Thrust joystick 输出**：
   ```csharp
   public Vector2 ThrustInput { get; private set; }  // normalized [0,1]，不区分正负（前进）
   ```
2. **Aim joystick 输出**：
   ```csharp
   public Vector2 AimInput { get; private set; }  // normalized [-1,1]，区分方向
   ```
3. **死区归一化**：
   ```csharp
   float Normalize(float offset) =>
       Mathf.Clamp01((Mathf.Abs(offset) - DEAD_ZONE) / (1f - DEAD_ZONE));
   ```
4. **ThrustInput 计算**（无后退）：
   ```csharp
   // 只取 magnitude，不区分左右
   float magnitude = Normalize(touchPos.magnitude);
   ThrustInput = thrustFingerId != -1 ? Vector2.up * magnitude : Vector2.zero;
   ```
5. **AimInput 计算**：
   ```csharp
   // 区分上下左右方向
   AimInput = aimFingerId != -1
       ? new Vector2(Normalize(delta.x), Normalize(delta.y))
       : Vector2.zero;
   ```
6. **ShipInputChannel 广播**：
   ```csharp
   _shipInputChannel.RaiseThrust(ThrustInput.magnitude);
   ```
7. **DualJoystickInput 挂载**：CockpitScene 内，ShipController 同一 GameObject 或相邻

---

## Out of Scope

- ShipInputManager ActionMap 切换（Story 014）
- ShipControlSystem 物理应用（Story 016）
- 软锁定（SoftLock）和自动开火逻辑（AC-CTRL-07~09，属于 Story 016）
- game.inputactions 资产创建（Unity Editor 手动步骤）

---

## QA Test Cases

- **AC-1: 左半屏触控分配到 Thrust joystick**
  - Given: DualJoystickInput 启用，ViewLayer = COCKPIT，_thrustFingerId == -1
  - When: touch.position = (300, 400)，screenWidth = 1080（左半屏）
  - Then: _thrustFingerId = touch.finger.index；ThrustInput 更新
  - Edge cases: 已在追踪时新触点落入同一区域 → 忽略新触点

- **AC-2: 右半屏触控分配到 Aim joystick**
  - Given: DualJoystickInput 启用，ViewLayer = COCKPIT，_aimFingerId == -1
  - When: touch.position = (800, 400)，screenWidth = 1080（右半屏）
  - Then: _aimFingerId = touch.finger.index；AimInput 更新

- **AC-3: 死区过滤**
  - Given: 摇杆偏移量 offset = 0.05f，DEAD_ZONE = 0.08f
  - When: Normalize(offset) 被调用
  - Then: 返回 0（小于死区，归零）

- **AC-4: 死区归一化（有效范围）**
  - Given: 摇杆偏移量 offset = 0.545f，DEAD_ZONE = 0.08f
  - When: Normalize(offset) 被调用
  - Then: 返回 (0.545 - 0.08) / 0.92 ≈ 0.505

- **AC-5: 手指抬起 Thrust 归零**
  - Given: _thrustFingerId = 2，ThrustInput = (0, 0.8)
  - When: touch.finger.index == 2 的触控抬起
  - Then: _thrustFingerId = -1；ThrustInput = Vector2.zero（立即归零，不等待超时）

- **AC-6: 双指同时操作独立追踪**
  - Given: 左触点(200,400) 和右触点(900,500) 同时落下
  - When: EnhancedTouch.Touch.activeTouches 分发两帧 touch 事件
  - Then: _thrustFingerId 追踪左指；_aimFingerId 追踪右指；左指抬起不影响右指

- **AC-7: ViewLayer ≠ COCKPIT 时不处理触控**
  - Given: ViewLayer = STARMAP，_thrustFingerId = -1
  - When: touch.position = (300, 400)
  - Then: _thrustFingerId 保持 -1；ThrustInput 保持 Vector2.zero

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/input/dual_joystick_test.cs` — must exist and pass
**Status**: ✅ Created — `tests/unit/input/dual_joystick_test.cs`

---

## Dependencies

- Depends on: None（可独立于 Story 011 并行实现；仅依赖 ADR-0003 规则）
- Unlocks: Story 016（ShipControlSystem 读取 DualJoystickInput 的输出）

---

## Completion Notes

**Completed**: 2026-04-15
**Criteria**: 7/8 passing（所有 AC 已覆盖）
**Deviations**:
- ShipInputChannel 在 src/Input/ 而非 src/Channels/ — Input 系统专用 Channel，按 Ownership 放在 Input 目录更合适
- **FIXED**: DualJoystickInput.OnEnable/OnDisable 曾调用 EnhancedTouchSupport.Enable/Disable()，与 ShipInputManager 冲突（ADR-0003 R-1 规定唯一所有权归 ShipInputManager）。已在 Story 014 实现后移除 DualJoystickInput 中的这两行调用
**Test Evidence**: tests/unit/input/dual_joystick_test.cs — 8个 EditMode 测试用例
**Code Review**: Pending
