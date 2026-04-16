# ADR-0018: Ship Control System Architecture

## Status
Accepted

## Date
2026-04-15

## Accepted
2026-04-15 — Accepted during architecture review (architecture-review-2026-04-15.md). All TR-shipctrl-* requirements are formally covered by this ADR. No blocking issues remain.

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Core / Physics + Input |
| **Knowledge Risk** | MEDIUM — `Rigidbody.AddForce` / `MoveRotation` / `linearDamping` API 在 Unity 6.3 与训练数据一致；`Rigidbody.drag` 已重命名为 `linearDamping`（Unity 6+）需注意；PhysX 5.1 稳定性改进已知 |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `docs/engine-reference/unity/modules/physics.md`, `docs/engine-reference/unity/deprecated-apis.md`, `docs/engine-reference/unity/breaking-changes.md` |
| **Post-Cutoff APIs Used** | `Rigidbody.linearDamping`（Unity 6 重命名，原 `.drag`）；`Rigidbody.angularVelocity`（无变化）；`CollisionDetectionMode.ContinuousDynamic`（PhysX 5.1 改进） |
| **Verification Required** | (1) 双摇杆输入时 aimAngle 计算正确（规则 L-2）；(2) 软锁定目标在进入/离开 LOCK_RANGE 时正确激活/失效；(3) 视角切换按钮触发相机平滑过渡；(4) FixedUpdate 物理帧独立于渲染帧运行；(5) 60fps 移动端无 GC 分配（触屏事件处理路径） |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0003（Input System — CockpitActions ActionMap、EnhancedTouchSupport 虚拟摇杆 fingerId 追踪）；ADR-0013（Combat System — 武器触发接口 FireRequested、aimAngle 消费）；ADR-0014（Health System — 伤害入口 ApplyDamage，无直接依赖但通过 CombatSystem 间接耦合） |
| **Enables** | Core Epic — 驾驶舱飞行操控、软锁定自动开火、第三/第一人称相机切换 |
| **Blocks** | Core Epic — ShipControlSystem 实现所有 stories 均依赖本 ADR；ShipHUD（武器冷却显示、锁定准星）依赖本 ADR 的 OnLockAcquired/OnLockLost 事件 |
| **Ordering Note** | 本 ADR 依赖 ADR-0003 Accepted 状态（ActionMap 切换协议是本 ADR 输入门控的基础）；ADR-0013 的软锁定逻辑（L 组规则）依赖本 ADR 提供 aimAngle，建议同时 Accepted 或本 ADR 先 Accepted |

## Context

### Problem Statement

《星链霸权》驾驶舱层需要将玩家触屏双摇杆输入转化为带惯性的刚体飞行物理。系统必须：

1. 将 `EnhancedTouchSupport` 追踪的虚拟摇杆偏移量（ADR-0003）转换为推力（thrust）和转向（steer）输入
2. 应用 Asteroids 式飞行物理——推力沿当前朝向，速度方向不跟随朝向旋转（惯性保留）
3. 实现软锁定追踪（Soft Lock）并在准星对准敌人时触发武器开火（ADR-0013）
4. 支持第三人称跟随和第一人称驾驶舱两种相机模式

### Constraints

- **帧预算**：移动端 16.6ms/帧，输入处理和物理计算必须在 FixedUpdate 完成，禁止每帧 GC 分配
- **物理更新**：必须使用 FixedUpdate（物理帧独立于渲染帧），Rigidbody API 调用在 FixedUpdate 内
- **跨场景引用禁令**（ADR-0001）：禁止跨场景持有 GameObject/Component 直接引用；跨场景状态通过 SO Channel
- **输入门控**（ADR-0003）：CockpitActions 启用后，驾驶舱物理控制进一步由 `ShipState ∈ {IN_COCKPIT, IN_COMBAT}` 门控
- **触屏专用**：全触屏操作，禁止 hover 依赖，所有交互支持手指触控
- **水平面约束**：MVP 阶段飞船 Y 轴位置固定（FLIGHT_PLANE_Y），不提供垂直飞行

### Requirements

- 双摇杆输入映射：左摇杆 = thrust + steer，右摇杆 = aim assist（叠加到转向）
- Asteroids 式惯性物理：速度方向与朝向解耦，软上限速度钳制
- 软锁定目标追踪：最近敌人在 LOCK_RANGE 内被标记，FIRE_ANGLE_THRESHOLD 触发武器开火
- 视角切换：第三人称跟随（平滑）/ 第一人称（硬绑定驾驶舱锚点）
- 状态初始化/清理：进入/退出 IN_COCKPIT 时正确初始化和清理状态

## Decision

采用 **Rigidbody.AddForce + MoveRotation 飞行物理模型**，配合 SO Channel 信号与 C# event Tier 2 事件总线。

### 架构概览

```
EnhancedTouchSupport
        ↓
ShipInputManager（MasterScene）— 追踪 fingerId，调用 ShipControlSystem
        ↓ (Tier 2 C# event)
ShipControlSystem（CockpitScene）
    ├── 读取 ShipDataModel.ThrustPower / TurnSpeed（缓存）
    ├── 计算 aimAngle（供 CombatSystem）
    ├── 应用 Rigidbody.AddForce + MoveRotation
    └── 广播 OnLockAcquired / OnLockLost / OnAimAngleChanged
        ↓
ShipHUD（CockpitScene）— 订阅锁定事件渲染准星
CombatSystem（CockpitScene）— 订阅 FireRequested 触发自动开火
```

### 核心组件

| 组件 | 职责 | 挂载位置 |
|------|------|---------|
| `ShipControlSystem` | 物理施力、aimAngle 计算、软锁定追踪、状态初始化/清理 | CockpitScene |
| `ShipInputManager` | fingerId 追踪（虚拟摇杆区域）、将 Touch 偏移量转为归一化输入、订阅 ViewLayerChannel 和 ShipStateChannel | MasterScene |
| `CameraRig` | 第三人称跟随/第一人称硬绑定切换 | CockpitScene |

### 飞行物理规则

**P-1（推力施加）：**
```csharp
// GIVEN thrust_input > 0，FixedUpdate 内执行
rb.AddForce(transform.forward * ThrustPower * thrust_input, ForceMode.Force);
// ForceMode.Force 保证帧率无关连续加速
```

**P-2（速度软上限）：**
```csharp
float excess = velocity.magnitude - SHIP_MAX_SPEED;
if (excess > 0) {
    rb.AddForce(-velocity.normalized * excess * SPEED_CLAMP_STIFFNESS, ForceMode.Force);
}
// 软截断保留惯性，不硬截断速度向量
```

**P-3（惯性保留）：** 速度方向由 Unity 物理引擎自然保持（不调用 `rb.velocity = ...`），与朝向解耦。

**P-4（转向）：**
```csharp
rb.MoveRotation(rb.rotation * Quaternion.Euler(0, TurnSpeed * steer_x * Time.fixedDeltaTime, 0));
```

**P-5（角速度锁定）：** `rb.angularVelocity = Vector3.zero` 在每帧 FixedUpdate 开始时强制执行，防止物理碰撞引入非预期旋转。

**P-6（水平面约束）：** 固定 `rb.position.y = FLIGHT_PLANE_Y`，速度 Y 分量归零。

### 输入门控规则

**C-1（ShipState 门控）：** CockpitActions 收到输入后，首先检查 `ShipDataModel.GetState(instanceId)`：
- 返回 `IN_COCKPIT` 或 `IN_COMBAT` → 正常处理
- 其他状态 → 丢弃输入，不执行任何物理操作

**C-2（左摇杆死区）：**
```csharp
float normalizedThrust = Mathf.Clamp01((Mathf.Abs(offset) - DEAD_ZONE) / (1.0f - DEAD_ZONE));
// DEAD_ZONE = 0.08f（JOYSTICK_DEAD_ZONE，可配置）
```

**C-3（左摇杆无后退推力）：** `offset.y < 0` 时 thrust = 0，steer_x 仍然有效。

**C-4（右摇杆 aim assist）：** 右摇杆水平分量叠加到转向：`steer_total = steer_left + AIM_ASSIST_COEFF * steer_right`（`AIM_ASSIST_COEFF` = 0.5）。

**C-5（多点触控隔离）：** 左/右摇杆区域各自锁定首个进入的 `fingerId`，手指抬起时释放；禁止按 `touchIndex` 追踪。

### 软锁定追踪规则（L 组）

**L-1~L-5（见 GDD ship-control-system.md 规则 L-1 至 L-5）：**

- `SoftLockTarget` 在 `LOCK_RANGE` 内最近敌人中选取
- 目标稳定（不频繁跳变）：仅在目标离开范围或被摧毁时重新选取
- `aimAngle ≤ FIRE_ANGLE_THRESHOLD` 时触发 `FireRequested` 事件

### 视角切换规则（V 组）

| CameraMode | 位置更新 | 旋转更新 |
|---|---|---|
| `THIRD_PERSON` | `SmoothDamp` 平滑跟随（0.1s） | `SmoothDamp` 平滑跟随（0.15s） |
| `FIRST_PERSON` | 硬绑定 `CockpitAnchor` | 与飞船朝向同步，无延迟 |

切换动画时长 `CAMERA_SWITCH_DURATION`（推荐 0.3s），切换期间输入不中断。

### 状态初始化/清理规则（S 组）

| 状态转换 | 初始化动作 |
|---|---|
| → `IN_COCKPIT` | 缓存 ThrustPower/TurnSpeed；重置 SoftLockTarget 和武器冷却；激活输入监听；设置 CameraMode = THIRD_PERSON |
| `IN_COCKPIT` → `IN_COMBAT` | 不清理；操控继续运行 |
| `IN_COCKPIT` → `DOCKED` | 停用输入监听；清空 SoftLockTarget；广播 OnLockLost；不重置 Rigidbody 速度；释放 fingerId 追踪 |
| → `DESTROYED` | 执行完整 S-2 清理；`velocity = Vector3.zero`；`isKinematic = true` |

## Alternatives Considered

### Alternative 1: 纯 Transform 位移（不使用 Rigidbody）

- **Description**: 每帧直接修改 `transform.position` 和 `transform.rotation`
- **Pros**: 无物理引擎开销；精确控制位置；调试直观
- **Cons**: 无法利用 Unity 物理引擎的碰撞检测、连续碰撞检测（CCD）、物理材质摩擦等；需要手动实现速度衰减（`linearDamping`）；与 ADR-0013 战斗系统的碰撞检测集成困难
- **Rejection Reason**: MVP 阶段需要基础碰撞检测和 CCD；自己实现 damping 与 Unity 物理引擎行为不一致且增加工作量；不符合项目选用 PhysX 的技术决策

### Alternative 2: Kinematic Character Controller

- **Description**: 使用 Unity 的 KinematicBody 或 CharacterController 处理移动
- **Pros**: 比 Dynamic Rigidbody 更轻量；自带地面检测
- **Cons**: Unity 6.3 CharacterController 仍基于 Old Input System；KinematicBody 是新 API（2024+）且 LLM 知识不足；不支持 3D 空间 Asteroids 式惯性物理（速度/朝向解耦）
- **Rejection Reason**: 不适合 Asteroids 式飞行物理模型；项目技术栈明确使用 PhysX 而非 CharacterController

## Consequences

### Positive

- Asteroids 式物理提供玩家精通感——惯性滑行、弧线转弯是核心手感
- Soft Lock 系统降低触屏操作难度，使战斗在手机上可玩
- 第三人称/第一人称切换提供两种体验：第三人称看全貌，第一人称增强临场感
- 与 ADR-0003 无缝集成：ActionMap 切换自动门控输入处理

### Negative

- FixedUpdate 物理与渲染帧分离，调试时需要理解双帧结构
- 软锁定在目标密集时可能不稳定（L-3 稳定性规则缓解）
- 第三人称相机的 `SmoothDamp` 参数需要大量调参以达到手感预期

### Risks

- **风险 1：触感参数不符合预期** — 调参阶段需要反复 playtest。建议在 story 中明确标注调参 milestone。
- **风险 2：aimAngle 计算与战斗系统不兼容** — 需在集成测试中验证 aimAngle 跨系统传递路径（ShipControlSystem → CombatChannel → CombatSystem）。
- **风险 3：多指触控时 fingerId 混淆** — 需在真机上测试（模拟器可能不准确）。
- **缓解**：所有调参相关的 AC 在 story 中标记为 `playtest-gated`，由 QA 在真机执行。

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|---|---|---|
| `ship-control-system.md` | C 组规则（输入门控、死区、无后退推力、aim assist、多点触控隔离） | ShipControlSystem 在 FixedUpdate 内对每个 CockpitActions 回调执行 C-1~C-5 规则 |
| `ship-control-system.md` | P 组规则（AddForce 推力、速度软上限、惯性保留、MoveRotation 转向、朝向速度解耦、角速度锁定、水平面约束） | ShipControlSystem 在 FixedUpdate 内对 Rigidbody 执行 P-1~P-7 规则 |
| `ship-control-system.md` | L 组规则（软锁定激活/稳定/触发） | ShipControlSystem 维护 SoftLockTarget，每帧计算 aimAngle 并在阈值内触发 FireRequested |
| `ship-control-system.md` | V 组规则（第三人称/第一人称切换） | CameraRig 组件实现 V-1~V-4，订阅 ShipStateChannel 和本地 CameraMode 切换事件 |
| `ship-control-system.md` | S 组规则（初始化/清理） | ShipControlSystem 订阅 ViewLayerChannel.OnViewLayerChanged 和 ShipStateChannel，在状态转换时执行 S-1~S-4 |
| `ship-combat-system.md` | 自动开火（aimAngle ≤ FIRE_ANGLE_THRESHOLD） | CombatSystem 订阅 ShipControlSystem.FireRequested 事件，ShipControlSystem 是 aimAngleProvider |

## Performance Implications

| Metric | Value | Notes |
|---|---|---|
| **CPU** | ~0.3–0.5ms/帧 | FixedUpdate 内：AddForce + MoveRotation + aimAngle 计算；无 GC 分配 |
| **Memory** | 极低 | 纯值类型计算；无每帧堆分配 |
| **Load Time** | 无影响 | 无场景加载依赖 |
| **Network** | N/A | 单机游戏 |

## Migration Plan

本 ADR 是新增系统，无现有代码迁移。ShipControlSystem 实现遵循以下顺序：

1. `ShipControlSystem` 核心物理循环（FixedUpdate P-1~P-7）
2. 输入门控（C-1~C-5）
3. `ShipInputManager` fingerId 追踪集成（依赖 ADR-0003）
4. 软锁定逻辑（L-1~L-5）+ `FireRequested` 事件
5. CameraRig 视角切换（V-1~V-4）
6. 状态初始化/清理（S-1~S-4）

## Validation Criteria

| 验证项 | 方法 |
|---|---|
| 双摇杆在真机上响应正确（无死区误触发、无后退推力） | 手动 playtest（Android 真机） |
| aimAngle 在目标进入/离开 LOCK_RANGE 时正确更新 | EditMode 测试：Mock ShipControlSystem，设置敌人在范围内/外 |
| SoftLockTarget 在目标离开范围时正确清空，新目标进入时正确选取 | EditMode 测试 |
| 第三人称相机平滑跟随，第一人称无延迟 | 手动 playtest |
| 60fps 移动端无 GC 分配（输入处理路径） | Unity Profiler |
| 视角切换动画期间输入不中断 | 手动 playtest |

## Related Decisions

- `docs/architecture/adr-0003-input-system-architecture.md` — CockpitActions 定义、EnhancedTouchSupport 所有权
- `docs/architecture/adr-0013-combat-system-architecture.md` — FireRequested 接口消费、aimAngleProvider
- `docs/architecture/adr-0014-health-system-architecture.md` — DESTROYED 状态处理（间接）
- `design/gdd/ship-control-system.md` — GDD 详细规则（P/C/L/V/S 六组规则）
- `design/gdd/ship-combat-system.md` — 自动开火与 aimAngle 消费
