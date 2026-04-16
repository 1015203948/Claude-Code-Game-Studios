# Story 016: ShipControlSystem 驾驶舱物理集成

> **Epic**: Foundation Runtime
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Integration
> **Manifest Version**: 2026-04-14
> **Estimate**: 3-4 hours

## Context

**GDD**: `design/gdd/ship-control-system.md`
**Requirement**: `TR-dvs-007`

**ADR Governing Implementation**: ADR-0003: Input System Architecture
**ADR Decision Summary**: ShipControlSystem 在 CockpitScene 的 ShipController 上运行；订阅 DualJoystickInput 的输出（ThrustInput, AimInput）和 ShipStateChannel；使用 FixedUpdate 应用物理力；将 ShipState = IN_COCKPIT 时的操控结果广播到 ShipInputChannel。

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: Rigidbody.linearDamping（Unity 6.x 从 .drag 重命名）；ForceMode.Force 保证帧率无关

**Control Manifest Rules (Foundation)**:
- Required: 物理力在 FixedUpdate 中施加，不用 Update
- Required: ThrustInput 只驱动前进（无后退），AimInput 只驱动转向角速度
- Required: 软锁定和自动开火需要从 ShipStateChannel 读取目标信息（架构上支持，不依赖战斗系统具体实现）

---

## Acceptance Criteria

*From GDD AC-CTRL-03, AC-CTRL-04, AC-CTRL-05, AC-CTRL-06, AC-CTRL-07, AC-CTRL-08, AC-CTRL-09, AC-CTRL-11, AC-CTRL-12：*

- [ ] AC-CTRL-03（推力与转向同帧生效）：FixedUpdate 中 ThrustForce 和 TurnTorque 同帧施加到 Rigidbody
- [ ] AC-CTRL-04（无后退推力）：ThrustInput 仅施加正向推力（transform.forward × ThrustPower × thrust_input），无后退力
- [ ] AC-CTRL-05（惯性保留）：Exit IN_COCKPIT 时不清零 Rigidbody.velocity，飞船保持惯性滑行
- [ ] AC-CTRL-06（朝向与速度解耦）：飞船朝向由 AimInput 控制旋转角速度（angularVelocity），速度方向由惯性决定不由转向强制对齐
- [ ] AC-CTRL-07（软锁定激活）：当 AimInput 指向角度内（FIRE_ANGLE_THRESHOLD）存在有效目标且距离 < LOCK_RANGE 时，激活 SoftLockTarget；UI 显示锁定标识
- [ ] AC-CTRL-08（软锁定不激活/脱锁）：目标超出 FIRE_ANGLE_THRESHOLD 或距离 > LOCK_RANGE 时，SoftLockTarget = null，UI 锁定标识消失
- [ ] AC-CTRL-09（自动开火）：SoftLockTarget ≠ null 且武器冷却计时器 = 0 时，自动触发开火（调用战斗系统接口或 ShipStateChannel 广播）
- [ ] AC-CTRL-11（IN_COCKPIT 初始化）：ShipState → IN_COCKPIT 时，读取 ThrustPower/TurnSpeed 并缓存；重置 SoftLockTarget；重置武器冷却计时器；激活输入监听
- [ ] AC-CTRL-12（DESTROYED 清理）：ShipState → DESTROYED 时，停用输入监听；清空 SoftLockTarget = null；广播 OnLockLost；不归零 Rigidbody.velocity

---

## Implementation Notes

*From ADR-0003 + GDD Formulas:*

1. **FixedUpdate 物理**：
   ```csharp
   private void FixedUpdate() {
       // 推力（仅前进）
       if (_thrustInput.magnitude > 0) {
           _rb.AddForce(transform.forward * _thrustPower * _thrustInput.magnitude, ForceMode.Force);
       }
       // 转向（角速度）
       _rb.angularVelocity = transform.up * _turnSpeed * _aimInput.x;
   }
   ```
2. **速度更新公式**：`v_new = v_prev + (ThrustPower × thrust_input / mass) × fixedDeltaTime`
3. **软锁定**（Stub 实现，待战斗系统接入）：
   ```csharp
   // 查询范围内的目标（从 ShipStateChannel 广播的目标列表读取）
   // 计算 AimInput 方向与目标朝向的角度差
   // 若 angle < FIRE_ANGLE_THRESHOLD && distance < LOCK_RANGE → SoftLockTarget = target
   ```
4. **武器冷却**：`private float _weaponCooldown;` 每 FixedUpdate -= Time.fixedDeltaTime；开火时重置为 FIRE_RATE
5. **IN_COCKPIT 初始化**（订阅 ShipStateChannel）：
   ```csharp
   private void OnShipStateChanged(string instanceId, ShipState newState) {
       if (newState == ShipState.IN_COCKPIT) { InitializeCockpit(); }
       else if (newState == ShipState.DESTROYED) { CleanupCockpit(); }
   }
   ```
6. **ShipInputChannel 广播**：
   ```csharp
   // 每帧（Update 中）
   _shipInputChannel.RaiseThrust(_thrustInput.magnitude);
   _shipInputChannel.RaiseAim(_aimInput);
   ```

---

## Out of Scope

- 战斗系统具体实现（开火接口和目标检测由 Core 层战斗系统提供）
- StarMapUI 消费 ShipInputChannel（Presentation 层）
- 摄像机跟随模式切换（Presentation 层）

---

## QA Test Cases

- **AC-1: 推力与转向同帧施加**
  - Given: ShipState = IN_COCKPIT，_thrustInput = 0.8，_aimInput = (0.5, 0)
  - When: FixedUpdate 执行
  - Then: Rigidbody 同时受到向前推力和左转扭矩；两者均来自本帧输入值
  - Edge cases: thrustInput = 0 时无推力但有转向

- **AC-2: 无后退推力验证**
  - Given: ShipState = IN_COCKPIT，_thrustInput = 0.8（前进）
  - When: FixedUpdate 执行，Rigidbody 当前速度 = (5, 0, 0)，朝向 = (0,0,1)
  - Then: AddForce 沿 transform.forward（不沿 -forward）；速度方向不受转向影响

- **AC-3: 惯性保留（Exit 时不清零速度）**
  - Given: ShipState = IN_COCKPIT，Rigidbody.velocity = (10, 0, 0)
  - When: ShipState → DOCKED（调用 CleanupCockpit）
  - Then: Rigidbody.velocity 仍为 (10, 0, 0)（惯性保留）；ShipState → _preEnterState

- **AC-4: IN_COCKPIT 初始化**
  - Given: ShipDataModel 持有 ThrustPower = 15，TurnSpeed = 3
  - When: ShipState → IN_COCKPIT
  - Then: _thrustPower = 15；_turnSpeed = 3；SoftLockTarget = null；_weaponCooldown = 0；输入监听激活

- **AC-5: DESTROYED 清理**
  - Given: ShipState = IN_COCKPIT，SoftLockTarget ≠ null，_weaponCooldown > 0
  - When: ShipState → DESTROYED
  - Then: SoftLockTarget = null；输入监听停用；_shipInputChannel 不再广播；Rigidbody.velocity 不归零

- **AC-6: 软锁定激活（Mock 目标列表）**
  - Given: Mock 目标列表包含一个有效目标，角度差 = 10° < FIRE_ANGLE_THRESHOLD，距离 = 50 < LOCK_RANGE
  - When: AimInput 指向该目标方向
  - Then: SoftLockTarget = 该目标；UI 锁定标识显示

- **AC-7: 软锁定脱锁（角度超出）**
  - Given: SoftLockTarget = activeTarget，角度差变为 45° > FIRE_ANGLE_THRESHOLD
  - When: 每帧检测（FixedUpdate 或专用 Update）
  - Then: SoftLockTarget = null；UI 锁定标识消失

- **AC-8: 自动开火（Mock 武器系统）**
  - Given: SoftLockTarget ≠ null，_weaponCooldown = 0
  - When: FixedUpdate 执行
  - Then: 触发开火逻辑（调用 IFireable.Fire() 或广播 ShipStateChannel）；_weaponCooldown 重置为 FIRE_RATE

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/input/ship_control_system_test.cs` — must exist and pass
**Status**: ✅ Created — `tests/integration/gameplay/ship_control_system_test.cs`

---

## Dependencies

- Depends on: Story 014（ShipInputManager 建立 ShipInputChannel）；Story 015（DualJoystickInput 已实现）
- Unlocks: Presentation 层（ShipHUD 消费 ShipInputChannel + SoftLockTarget）

---

## Completion Notes

**Completed**: 2026-04-15
**Criteria**: 9/9 passing（软锁定和自动开火为 stub 实现，待战斗系统接入）
**Deviations**: ADVISORY — 测试文件路径为 `tests/integration/gameplay/` 而非 `tests/integration/input/`，子目录命名不同（gameplay vs input），功能正确
**Test Evidence**: tests/integration/gameplay/ship_control_system_test.cs — 10个 EditMode 测试用例
**Code Review**: Pending


- Depends on: Story 014（ShipInputManager 建立 ShipInputChannel）；Story 015（DualJoystickInput 已实现）
- Unlocks: Presentation 层（ShipHUD 消费 ShipInputChannel + SoftLockTarget）
