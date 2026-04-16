# 飞船操控系统 (Ship Control System)

> **Status**: Designed (pending /design-review)
> **Author**: game-designer (Claude Code) + user
> **Last Updated**: 2026-04-12
> **Implements Pillar**: 支柱4 从星图到驾驶舱 / 支柱2 一目了然的指挥

## Overview

飞船操控系统是《星链霸权》驾驶舱层的感官核心：它将玩家的触屏手势——左侧虚拟摇杆输入的推力方向、右摇杆的战斗辅助指令——转化为作用于飞船物理体的力与角速度，使飞船在战场空间中以带有惯性的弧线轨迹飞行。系统工作于 `ShipState = IN_COCKPIT` 状态窗口内，从飞船系统读取当前飞船的推进性能参数（`ThrustPower`、`TurnSpeed`），应用到 Unity PhysX 刚体之上，并将飞船当前速度与朝向信息暴露给飞船 HUD 和战斗系统消费。MVP 阶段提供两套视角（第三人称跟随相机 / 第一人称驾驶舱），玩家可在驾驶中随时切换；战斗时采用"朝向自动追踪"机制——飞船朝向目标锁定方向自动开火，让玩家专注于飞行路径而非精确点击。所有操控交互不依赖任何 hover 状态，完全兼容触屏单点和双指操作。

## Player Fantasy

飞船操控系统的玩家幻想是**三层重叠的临场感**——指挥官的下凡、驾驶员的精通、存在于黑暗中的飞行器。

**第一层：指挥官就位**
你不是在"玩飞船"。你是一位拥有星际帝国的人，选择在这个时刻放下宏观视图，亲手驾驶一艘用自己矿石建造的战舰冲入战场。推力启动的那一刻，星图上的战略判断压缩成了驾驶舱里最直接的问题：怎么飞、从哪里绕、什么时候开火。两种责任感同时存在于你的手指上。

**第二层：手感精通**
飞船不听命令，它服从物理定律。第一次开，你会因为惯性飞过头；第二十次，你开始下意识地在接近目标前松开摇杆，让飞船靠惯性滑行过弯，在弧线尽头恰好对准敌舰。那一刻你意识到：这艘船已经是我身体的延伸了。精通感是身体层面的——拇指的角度就是推力的方向。

**第三层：黑暗中的存在感**
深空是空旷的。你的引擎尾焰是你在这片虚空存在过的唯一痕迹。有时候切入驾驶舱不是为了战斗——是为了确认：帝国不只是星图上的数字，它是这条穿过黑暗的航迹，是这艘发着冷蓝色光的飞船。

**支柱对齐：**
- **支柱4（从星图到驾驶舱）**：操控系统是这个支柱的直接实现——切换后，玩家的身体取代了星图上的指令箭头
- **支柱1（经济即军事）**：每一次推动摇杆都在消费那些花了时间积累的矿石和能源的转化物——牵挂感使操控本身有了重量
- **支柱3（我的星际帝国）**：不是驾驶一艘飞船，是驾驶"我的"飞船

**锚点时刻**：防线快撑不住了。你在星图上看到那艘血量刚回满的旗舰停在最近的有利位置。你点下"进入驾驶舱"——相机骤然推近，引擎声灌满耳朵，左手推摇杆，右手准备。惯性带着飞船画出一条弧线，飞进那片火光里。这不是自动战斗结算，这是你来了。

## Detailed Design

### Core Rules

**输入处理规则（C 组）**

**规则 C-1（输入门控）**
GIVEN 飞船实例存在，WHEN 任意输入事件到达操控系统，THEN 系统首先调用 `ShipData.GetState(instanceId)`；若返回值 ≠ `IN_COCKPIT` 且 ≠ `IN_COMBAT`，则丢弃该输入，不执行任何物理操作。

**规则 C-2（左摇杆死区过滤）**
GIVEN 玩家触摸左摇杆区域，WHEN 摇杆偏移量的模长 `|offset|` < `JOYSTICK_DEAD_ZONE`（推荐值 0.08，归一化单位），THEN 该帧推力和转向输入均视为零；摇杆视觉位置仍跟随手指（UI 层不受影响）。

**规则 C-3（左摇杆方向语义）**
GIVEN 左摇杆偏移量已通过死区过滤，WHEN 系统计算本帧输入，THEN：
- **推力大小** = `ThrustPower × thrust_input`，其中 `thrust_input = clamp((|offset| - DEAD_ZONE) / (1.0 - DEAD_ZONE), 0, 1)`（线性重映射）
- **推力方向** = 飞船当前朝向（`transform.forward`），与摇杆方向无关
- **转向方向** = 摇杆偏移向量的水平分量（`offset.x`），正值右转，负值左转
- **转向速度** = `TurnSpeed × offset.x`（归一化后的水平分量）
- 推力和转向同时生效，互不干扰

**规则 C-4（无后退推力）**
GIVEN 左摇杆向下偏移（`offset.y < 0`），WHEN 系统计算推力，THEN 推力大小为零（不产生反向推力）；转向分量（`offset.x`）仍然有效。

**规则 C-5（右摇杆辅助瞄准）**
GIVEN 右摇杆有效偏移量存在（模长 ≥ `JOYSTICK_DEAD_ZONE`），WHEN 系统处理右摇杆输入，THEN 右摇杆水平分量叠加到飞船转向，叠加角速度 = `TurnSpeed × AIM_ASSIST_COEFF × offset.x`（`AIM_ASSIST_COEFF` 推荐值 0.5，使右摇杆转向比左摇杆更精细）；右摇杆不产生推力。

**规则 C-6（多点触控隔离）**
GIVEN 玩家双手同时操作两个摇杆，WHEN Unity 触控系统分发 Touch 事件，THEN 左摇杆区域和右摇杆区域各自独立追踪自己的 `fingerId`；一个摇杆的触控抬起不影响另一个摇杆的当前状态。

---

**飞行物理规则（P 组）**

**规则 P-1（推力施加方式）**
GIVEN 本帧 `thrust_input > 0`，WHEN `FixedUpdate` 执行，THEN 系统调用 `Rigidbody.AddForce(transform.forward × ThrustPower × thrust_input, ForceMode.Force)`；使用 `ForceMode.Force` 保证帧率无关的连续加速感。

**规则 P-2（速度软上限）**
GIVEN Rigidbody 当前速度模长 `v`，WHEN `FixedUpdate` 执行完 `AddForce` 后，THEN 若 `v > SHIP_MAX_SPEED`，则对超出部分施加反向力（软截断）：`AddForce(-excess × SPEED_CLAMP_STIFFNESS, ForceMode.Force)`，其中 `excess = velocity.normalized × (v - SHIP_MAX_SPEED)`；保留惯性感，不硬截断速度向量。

**规则 P-3（惯性保留）**
GIVEN 玩家松开左摇杆（`thrust_input = 0`），WHEN 后续帧执行，THEN 系统不施加任何制动力；`Rigidbody.linearDamping`（Unity 6 API）设为 `SHIP_LINEAR_DRAG`（推荐起始值 0.5），由 Unity 物理引擎自然衰减速度。

**规则 P-4（转向施加方式）**
GIVEN 左摇杆水平分量 `steer_x`（归一化，-1 到 1），WHEN `FixedUpdate` 执行，THEN 系统调用 `Rigidbody.MoveRotation`，每帧旋转角度 = `TurnSpeed × steer_x × Time.fixedDeltaTime`；转向与推力独立，可同时进行。

**规则 P-5（朝向与速度解耦）**
GIVEN 飞船正在高速飞行（速度方向为 A），WHEN 玩家转向（飞船朝向变为 B），THEN Rigidbody 的速度向量方向保持 A 不变（惯性）；速度方向不跟随朝向自动旋转（Asteroids 式物理）。

**规则 P-6（角速度锁定）**
GIVEN 飞船处于 `IN_COCKPIT` 或 `IN_COMBAT` 状态，WHEN 任意帧，THEN `Rigidbody.angularVelocity` 被强制归零；飞船旋转完全由规则 P-4 的受控转向驱动，不受物理碰撞产生的角冲量影响。

**规则 P-7（水平面约束）**
GIVEN 飞船在 3D 空间中运动，WHEN 任意帧，THEN 飞船的 Y 轴位置被钳制在 `FLIGHT_PLANE_Y`（固定高度），速度向量的 Y 分量强制归零；MVP 阶段无垂直飞行。

---

**软锁定追踪规则（L 组）**

**规则 L-1（锁定激活条件）**
GIVEN 飞船处于 `IN_COCKPIT` 或 `IN_COMBAT` 状态，WHEN 场景中存在至少一个敌方目标且距离 ≤ `LOCK_RANGE`，THEN 系统将距离最近的敌方目标标记为 `SoftLockTarget`；若无目标在范围内，`SoftLockTarget = null`。

**规则 L-2（锁定角度判定）**
GIVEN `SoftLockTarget != null`，WHEN 系统每帧计算，THEN `aim_angle = Vector3.Angle(transform.forward, (target.position - transform.position).normalized)`。

**规则 L-3（锁定目标稳定性）**
GIVEN 当前 `SoftLockTarget` 已存在，WHEN 有另一个敌方目标进入 `LOCK_RANGE` 且距离更近，THEN 系统不立即切换目标；仅当当前目标离开 `LOCK_RANGE` 或被摧毁时，才重新选取最近目标（防止目标频繁跳变）。

**规则 L-4（自动开火触发）**
GIVEN `SoftLockTarget != null`，WHEN `aim_angle ≤ FIRE_ANGLE_THRESHOLD` 且武器冷却计时器已归零，THEN 系统自动触发一次武器开火，调用飞船战斗系统的开火接口；开火后重置武器冷却计时器。

**规则 L-5（锁定 UI 事件）**
GIVEN `SoftLockTarget != null`，WHEN `aim_angle ≤ FIRE_ANGLE_THRESHOLD`，THEN 系统广播 `OnLockAcquired(targetId)` 事件；当 `aim_angle > FIRE_ANGLE_THRESHOLD` 时广播 `OnLockLost(targetId)` 事件；HUD 系统订阅这两个事件渲染锁定准星。

---

**视角切换规则（V 组）**

**规则 V-1（第三人称为默认视角）**
GIVEN 飞船从任意状态进入 `IN_COCKPIT`，WHEN 视角切换系统完成过渡动画，THEN 默认激活第三人称跟随相机（`CameraMode = THIRD_PERSON`）。

**规则 V-2（视角切换触发方式）**
GIVEN `CameraMode = THIRD_PERSON` 或 `FIRST_PERSON`，WHEN 玩家点击 HUD 上的视角切换按钮，THEN 系统切换到另一种相机模式；切换动画时长 `CAMERA_SWITCH_DURATION`（推荐值 0.3 秒）；切换期间输入处理不中断。

**规则 V-3（第三人称相机跟随参数）**
GIVEN `CameraMode = THIRD_PERSON`，WHEN 飞船移动，THEN 相机位置 = 飞船位置 + `CAMERA_OFFSET`（推荐值：后方 8 m，上方 3 m）；相机朝向通过 `Mathf.SmoothDamp` 平滑跟随，平滑时间 `CAMERA_ROTATION_SMOOTH` = 0.15 秒；位置平滑时间 `CAMERA_POSITION_SMOOTH` = 0.1 秒。

**规则 V-4（第一人称相机绑定）**
GIVEN `CameraMode = FIRST_PERSON`，WHEN 飞船移动或转向，THEN 相机硬绑定到飞船的驾驶舱锚点（`CockpitAnchor` Transform），相机旋转与飞船朝向完全同步，无平滑延迟；MVP 阶段无自由视角。

---

**状态相关规则（S 组）**

**规则 S-1（进入 IN_COCKPIT 时初始化）**
GIVEN 飞船状态转换为 `IN_COCKPIT`，WHEN 操控系统收到状态变更通知，THEN 按顺序执行：(1) 从 `ShipData` 读取 `ThrustPower` 和 `TurnSpeed` 并缓存；(2) 重置 `SoftLockTarget = null`；(3) 重置武器冷却计时器为 0；(4) 激活输入监听；(5) 设置 `CameraMode = THIRD_PERSON`。

**规则 S-2（退出 IN_COCKPIT 时清理）**
GIVEN 飞船状态从 `IN_COCKPIT` 转换为 `DOCKED`，WHEN 操控系统收到状态变更通知，THEN 按顺序执行：(1) 停用输入监听；(2) 清空 `SoftLockTarget = null`；(3) 广播 `OnLockLost` 事件（若有锁定目标）；(4) 停止施加推力（不清零 Rigidbody 速度，保留惯性）；(5) 释放两个摇杆的 `fingerId` 追踪。

**规则 S-3（IN_COMBAT 状态下操控继续）**
GIVEN 飞船状态从 `IN_COCKPIT` 转换为 `IN_COMBAT`，WHEN 操控系统收到状态变更通知，THEN 操控系统不执行清理；输入处理、锁定逻辑、推力施加继续正常运行；`IN_COMBAT` 状态下操控规则与 `IN_COCKPIT` 完全相同。

**规则 S-4（DESTROYED 状态强制清理）**
GIVEN 飞船状态变为 `DESTROYED`，WHEN 操控系统收到 `OnShipDestroyed` 事件，THEN 立即执行规则 S-2 的全部清理步骤，并额外将 `Rigidbody.linearVelocity = Vector3.zero`、`Rigidbody.isKinematic = true`；防止已销毁飞船的物理体继续漂移。

---

### States and Transitions

操控系统本身无独立状态机——它是飞船系统状态机的**消费者**，在 `IN_COCKPIT` 和 `IN_COMBAT` 状态窗口内激活，其余状态下休眠。

| 飞船状态 | 操控系统状态 | 输入处理 | 物理施力 | 锁定逻辑 |
|---------|------------|---------|---------|---------|
| `DOCKED` | 休眠 | ❌ | ❌ | ❌ |
| `IN_TRANSIT` | 休眠 | ❌ | ❌ | ❌ |
| `IN_COCKPIT` | **激活** | ✅ | ✅ | ✅ |
| `IN_COMBAT` | **激活** | ✅ | ✅ | ✅ |
| `DESTROYED` | 强制清理 | ❌ | ❌ | ❌ |

### Interactions with Other Systems

| 系统 | 方向 | 数据接口 |
|------|------|---------|
| **飞船系统** | 读取 | `GetThrustPower()`、`GetTurnSpeed()`、`GetState()` — 进入 IN_COCKPIT 时缓存属性 |
| **飞船战斗系统** | 写出 | 调用战斗系统的开火接口（规则 L-4）；广播 `OnLockAcquired` / `OnLockLost` 事件 |
| **飞船 HUD** | 写出（事件） | 广播 `OnLockAcquired(targetId)`、`OnLockLost(targetId)`；HUD 订阅后渲染锁定准星 |
| **双视角切换系统** | 被调用 | 切换系统调用 `ShipData.SetState(IN_COCKPIT)` 触发操控系统初始化（规则 S-1） |
| **飞船生命值系统** | 被通知 | 订阅 `OnShipDestroyed` 事件，触发规则 S-4 强制清理 |
| **敌人系统** | 读取 | 查询场景中敌方目标位置，用于软锁定计算（规则 L-1 至 L-3） |

## Formulas

**公式 1：thrust_input（摇杆输入到推力强度的映射）**

`thrust_input = clamp((|offset| - DEAD_ZONE) / (1.0 - DEAD_ZONE), 0.0, 1.0)`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|----------|--------|------|-------|-------------|
| 摇杆偏移模长 | `\|offset\|` | float | 0.0–1.0 | 左摇杆偏移向量的模长，由 UI 层归一化提供 |
| 死区阈值 | `DEAD_ZONE` | float | 0.0–0.5 | 低于此值视为无输入；推荐值 0.08 |

**Output Range:** 0.0 到 1.0；死区内恒为 0，摇杆推到底时恒为 1.0
**Example:** `|offset|=0.6`，`DEAD_ZONE=0.08` → `clamp(0.52/0.92, 0, 1) = 0.565`

---

**公式 2：ship_velocity_after_thrust（施加推力后的速度变化，含软上限）**

步骤 A（推力加速）：`v_new = v_prev + (ThrustPower × thrust_input / mass) × fixedDeltaTime`

步骤 B（软上限，仅当 `|v_new| > SHIP_MAX_SPEED` 时执行）：
`F_clamp = -(v_new.normalized × (|v_new| - SHIP_MAX_SPEED)) × SPEED_CLAMP_STIFFNESS`
`v_final = v_new + (F_clamp / mass) × fixedDeltaTime`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|----------|--------|------|-------|-------------|
| 飞船推进力 | `ThrustPower` | float | >0，TBD | 来自注册表 `SHIP_THRUST_POWER`；参考起始值 15 m/s² |
| 归一化推力强度 | `thrust_input` | float | 0.0–1.0 | 来自公式 1 的输出 |
| 速度软上限 | `SHIP_MAX_SPEED` | float | >0 | 推荐起始值 20 m/s，待原型验证 |
| 软上限刚度系数 | `SPEED_CLAMP_STIFFNESS` | float | >0 | 推荐起始值 5.0；越大越接近硬截断 |
| 上帧速度向量 | `v_prev` | Vector3 | 无约束 | 本帧 FixedUpdate 开始时的速度向量 |

**Output Range:** `v_final` 模长在稳态下趋近 `SHIP_MAX_SPEED`；短暂超速允许（保留惯性感）
**Example:** `v_prev=20.5 m/s`，超速 0.5 m/s，`SPEED_CLAMP_STIFFNESS=5.0`，`fixedDeltaTime=0.02` → 每帧减少 0.05 m/s，逐渐收敛

---

**公式 3：turn_delta（每帧转向角度变化量）**

`turn_delta = TurnSpeed × steer_x × Time.fixedDeltaTime`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|----------|--------|------|-------|-------------|
| 最大转向角速度 | `TurnSpeed` | float | >0，TBD | 来自注册表 `SHIP_TURN_SPEED`；参考起始值 120 deg/s |
| 摇杆水平分量 | `steer_x` | float | -1.0–1.0 | 左摇杆偏移向量的归一化水平分量；正值右转，负值左转 |
| 物理时间步长 | `fixedDeltaTime` | float | 0.01–0.02 | Unity FixedUpdate 时间步长，默认 0.02 s |

**Output Range:** `[-TurnSpeed × fixedDeltaTime, +TurnSpeed × fixedDeltaTime]`，即 `[-2.4°, +2.4°]`（以 120 deg/s、0.02 s 计）
**Example:** `TurnSpeed=120`，`steer_x=0.75`，`fixedDeltaTime=0.02` → `120 × 0.75 × 0.02 = 1.8°`（本帧右转 1.8 度）

---

**公式 4：aim_angle（飞船朝向与目标方向的夹角）**

`toTarget = normalize(target.position - transform.position)`
`aim_angle = Vector3.Angle(transform.forward, toTarget)`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|----------|--------|------|-------|-------------|
| 目标世界坐标 | `target.position` | Vector3 | 场景空间 | 软锁定目标的世界坐标 |
| 飞船世界坐标 | `transform.position` | Vector3 | 场景空间 | 飞船自身的世界坐标 |
| 飞船朝向 | `transform.forward` | Vector3（单位向量） | 模长=1 | 飞船当前朝向的单位向量 |

**Output Range:** 0.0° 到 180.0°；0° = 正对目标，180° = 背对目标
**Example:** 飞船朝正东 `(1,0,0)`，目标在 `(10,0,5)` → `toTarget=(0.894,0,0.447)` → `aim_angle ≈ 26.6°`

---

**公式 5：soft_lock_assist（右摇杆辅助瞄准叠加量）**

`assist_delta = TurnSpeed × AIM_ASSIST_COEFF × aim_steer_x × fixedDeltaTime`

最终每帧总转向量：
`total_turn_delta = TurnSpeed × fixedDeltaTime × (steer_x + AIM_ASSIST_COEFF × aim_steer_x)`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|----------|--------|------|-------|-------------|
| 最大转向角速度 | `TurnSpeed` | float | >0，TBD | 来自注册表 `SHIP_TURN_SPEED` |
| 辅助瞄准系数 | `AIM_ASSIST_COEFF` | float | 0.0–1.0 | 右摇杆转向缩放系数；推荐值 0.5 |
| 右摇杆水平分量 | `aim_steer_x` | float | -1.0–1.0 | 右摇杆偏移向量的归一化水平分量（经死区过滤） |
| 左摇杆水平分量 | `steer_x` | float | -1.0–1.0 | 来自公式 3 |

**Output Range:** `total_turn_delta` 最大值 = `TurnSpeed × (1 + AIM_ASSIST_COEFF) × fixedDeltaTime = 3.6°/帧`（以 120 deg/s、系数 0.5、0.02 s 计）
**Example:** 左摇杆 `steer_x=0.5`，右摇杆 `aim_steer_x=1.0`，`AIM_ASSIST_COEFF=0.5` → `turn_delta=1.2°`，`assist_delta=1.2°`，`total=2.4°`

## Edge Cases

**输入边缘**

- **如果左右摇杆同时满偏**：两路输入独立处理，推力和辅助瞄准叠加正常执行。C-6 多点触控隔离已保证两个 fingerId 独立追踪，不存在互斥关系。
- **如果触控在死区范围内抬起（从未超出死区）**：视为零输入，不产生任何推力或转向增量。防止手指轻触屏幕时飞船意外漂移。
- **如果正在控制摇杆的 fingerId 因系统中断（来电、通知遮挡）丢失**：立即将该摇杆输入归零，等效于手指抬起。不等待超时，防止飞船持续加速失控。
- **如果两个触点同时落在同一摇杆热区**：以先到达的 fingerId 为准，后续触点忽略，直到先到达的 fingerId 抬起。防止摇杆输入跳变。

**物理边缘**

- **如果飞船当前速度为零，玩家仅拨动左摇杆横轴**：转向正常执行（turn_delta 公式不依赖速度），飞船原地旋转。朝向/速度解耦（P-5）保证此行为合法。
- **如果当前速度已达到 SHIP_MAX_SPEED，玩家持续推力**：软上限衰减项将净加速度压缩至趋近零，速度不再增长但也不反弹。玩家感知为"推力饱和"而非突然截断。
- **如果飞船位置触及战斗区域边界（水平面约束）**：位置硬截断至边界内，速度的越界分量归零，切线分量保留。不反弹，防止触屏操控下的弹跳失控感。
- **如果帧时间 Δt 异常大（如后台恢复后首帧，Δt > 0.05s）**：将 Δt 钳制到 `MAX_DELTA_TIME`（推荐值 0.05s）后再计算 turn_delta 和推力增量，防止物理积分爆炸。

**锁定边缘**

- **如果目标位于飞船朝向正后方（aim_angle ≈ 180°）**：超出软锁定激活角度阈值，不触发锁定，辅助瞄准不施加任何偏转。玩家需手动转向后才能重新进入锁定范围。
- **如果当前软锁定目标在锁定激活后同一帧被摧毁或离开有效范围**：立即清除锁定状态，触发 `OnLockLost` 事件，辅助瞄准归零。MVP 阶段不自动切换到次优目标，防止锁定跳变造成操控混乱。
- **如果同一帧有多个目标满足软锁定激活条件**：选取 aim_angle 最小（最正对）的目标作为锁定对象；若 aim_angle 相等，取距离最近者。不同时锁定多个目标（MVP 单锁定约束）。

**视角切换边缘**

- **如果视角切换动画进行中飞船被摧毁**：立即中止切换动画，强制进入 DESTROYED 状态处理流程。不等待动画完成，防止死亡序列被动画锁阻塞。
- **如果玩家在视角切换动画期间持续拨动摇杆**：飞船物理（推力/转向）继续正常响应输入，切换动画不冻结操控。视角切换是纯表现层，不影响操控层。

**状态转换边缘**

- **如果玩家在 IN_COCKPIT 状态下持续推力，同帧触发 IN_COMBAT 转换**：输入不中断，推力和转向在新状态下继续响应（C-1 规则两种状态均接受输入）。状态转换对操控层透明。
- **如果飞船进入 DESTROYED 状态**：立即执行：(a) 所有摇杆输入归零；(b) 推力施加停止；(c) 角速度归零；(d) 碰撞体禁用，防止尸体继续触发碰撞伤害；(e) 软锁定状态清除。物理刚体保留当前速度用于死亡动画漂移，不立即冻结位置。
- **如果飞船处于 IN_TRANSIT 时玩家尝试进入驾驶舱**：拒绝操作，C-1 输入门控不接受该状态的输入。UI 按钮应在 IN_TRANSIT 期间置灰，防止状态机进入非法状态。

## Dependencies

**飞船操控系统对外依赖（上游）：**

| 依赖系统 | 依赖类型 | 数据接口 |
|----------|----------|---------|
| **飞船系统** | 强依赖 | 读取 `GetThrustPower()`、`GetTurnSpeed()`、`GetState()` — 进入 IN_COCKPIT 时缓存属性；订阅 `OnShipDestroyed` 事件 |

**飞船操控系统被依赖（下游）：**

| 依赖系统 | 依赖类型 | 数据接口 |
|----------|----------|---------|
| **飞船战斗系统** | 强依赖 | 消费操控系统的开火触发接口（规则 L-4）；订阅 `OnLockAcquired` / `OnLockLost` 事件 |
| **飞船 HUD** | 强依赖 | 订阅 `OnLockAcquired(targetId)`、`OnLockLost(targetId)` 事件渲染锁定准星；读取飞船速度（`Rigidbody.linearVelocity`）用于速度表显示 |
| **双视角切换系统** | 强依赖 | 调用 `ShipData.SetState(IN_COCKPIT)` 触发操控系统初始化（规则 S-1）；操控系统的 `CameraMode` 状态由切换系统协调 |
| **敌人系统** | 软依赖 | 操控系统查询敌方目标位置用于软锁定计算（规则 L-1 至 L-3）；无敌人时操控系统仍可正常运行（锁定功能降级） |

> **双向一致性**：飞船系统 GDD 已在 Dependencies 中列出"飞船操控系统：强依赖，读取 GetThrustPower()、GetTurnSpeed()" ✅。飞船 HUD 和双视角切换系统尚无 GDD（Not Started），需在其 GDD 完成后补充反向引用。

## Tuning Knobs

| 调节旋钮 | 当前值 | 安全范围 | 过高后果 | 过低后果 |
|----------|--------|----------|----------|----------|
| `SHIP_THRUST_POWER` | TBD（参考起始值 15 m/s²） | 8–40 m/s² | 飞船过快难以控制，触屏操作失准 | 飞船响应迟钝，驾驶体验差 |
| `SHIP_TURN_SPEED` | TBD（参考起始值 120 deg/s） | 60–240 deg/s | 转向过灵敏，难以精确瞄准 | 转向过慢，追击/逃脱均无趣 |
| `SHIP_MAX_SPEED` | TBD（参考起始值 20 m/s） | 10–60 m/s | 飞船飞出战斗区域边界，战场感消失 | 飞船感觉"粘滞"，惯性感弱 |
| `JOYSTICK_DEAD_ZONE` | 0.08（归一化） | 0.05–0.25 | 死区过大，摇杆响应迟钝，需要大幅拨动才有反应 | 死区过小，手指微抖动触发意外推力 |
| `SPEED_CLAMP_STIFFNESS` | 5.0 | 2.0–20.0 | 接近硬截断，速度上限感觉生硬 | 软上限失效，飞船可无限加速 |
| `SHIP_LINEAR_DRAG` | 0.5 | 0.0–3.0 | 惯性消失过快，飞船感觉像在大气层中飞行 | 惯性永不衰减，飞船难以停止（太空感过强） |
| `LOCK_RANGE` | TBD（参考起始值 50 m） | 20–200 m | 锁定范围过大，玩家无需接近敌人即可开火，战斗缺乏张力 | 锁定范围过小，玩家需要贴脸才能锁定，触屏操控精度不足 |
| `FIRE_ANGLE_THRESHOLD` | TBD（参考起始值 15°） | 5°–30° | 瞄准容错过大，战斗缺乏技巧感 | 瞄准容错过小，触屏操控精度不足导致几乎无法开火 |
| `AIM_ASSIST_COEFF` | 0.5 | 0.0–1.0 | 右摇杆转向过快，与左摇杆无差异 | 右摇杆辅助无效，精细瞄准无法实现 |
| `CAMERA_OFFSET` | (0, 3, -8)（后方 8m，上方 3m） | 后方 4–15m，上方 1–6m | 相机过远，飞船图标过小，战斗细节不可见 | 相机过近，视野受限，难以预判敌人位置 |
| `CAMERA_ROTATION_SMOOTH` | 0.15 秒 | 0.05–0.5 秒 | 相机旋转过于滞后，玩家感到晕眩 | 相机旋转过于生硬，无跟随感 |
| `MAX_DELTA_TIME` | 0.05 秒 | 0.02–0.1 秒 | 帧时间钳制过宽，后台恢复后仍可能产生物理积分爆炸 | 帧时间钳制过严，正常帧率下也会截断，导致物理不连续 |

> **`SHIP_THRUST_POWER`、`SHIP_TURN_SPEED`、`SHIP_MAX_SPEED`、`LOCK_RANGE`、`FIRE_ANGLE_THRESHOLD` 的具体值待 `/prototype 飞船驾驶舱操控` 完成后填入。** 上表中的参考起始值仅供原型测试使用，不作为最终设计值。

## Visual/Audio Requirements

> **范畴声明**：飞船操控系统定义操控事件的视觉/音效**触发源**和**规格约束**；具体渲染实现归属见本章末尾的「归属边界」表。

### 1. 推进器尾焰视觉（Thruster VFX）

| 参数 | 待机（thrust = 0） | 低推力（0–0.5） | 满推力（thrust = 1.0） |
|---|---|---|---|
| 粒子数 | 0 | 4 | 8 |
| 尾焰长度（本地空间） | 0 | 0.3–0.8 单位 | 1.2 单位 |
| Emission 颜色 | — | `#4488FF`（冷蓝，50% 亮度） | `#88CCFF`（冷蓝，100% 亮度） |
| Bloom 强度 | — | 0.4 | 1.2 |
| 粒子生命周期 | — | 0.15s | 0.08s |

满推力时触发一次 0.05s 的 Bloom 脉冲（Intensity 1.2 → 2.0 → 1.2），强化"推力爆发"感。用单一 `ParticleSystem`，通过 `thrust_input` 驱动 `emission.rateOverTime` 和 `startSize` 曲线。

### 2. 软锁定准星视觉（Soft Lock Reticle）

- 形状：四段弧线（不闭合的方形括号），围绕目标飞船包围盒
- 颜色：`#00FFAA`（冷青绿），与玩家冷蓝 `#4488FF` 形成色相区分
- 尺寸：目标包围盒的 1.3 倍；线宽 2px（物理像素，DPI 无关）
- **OnLockAcquired**：从目标中心向外扩散 0.15s，然后收缩至最终尺寸（Ease Out Cubic）；Bloom Intensity 0.3
- **OnLockLost**：四段弧线向外扩散并淡出 0.1s（Ease In）；消失前闪烁一次 `#FF4444`（警告红），持续 0.05s

### 3. 自动开火视觉（Auto-Fire VFX）

- **弹道**：`LineRenderer` 实现（0 粒子开销），细长胶囊体，长宽比 8:1，颜色 `#FFDD44`（金色），Bloom Intensity 0.8
- **命中效果**：4 个粒子（Burst 模式，不循环），颜色 `#FFDD44` → `#FF8800`（0.2s 内消散），尺寸 0.3 单位
- **枪口闪光**：单帧 Sprite，`#FFFFFF` 核心 + `#4488FF` 外晕，持续 1 帧（0.016s）

### 4. 视角切换过渡（Camera Transition）

- **第三人称 → 第一人称**：0.25s，Ease In-Out Cubic；镜头沿飞船前向轴推进，FOV 60° → 75°；过渡中 0.1s 全屏 Vignette 加深（边缘暗化）
- **第一人称 → 第三人称**：0.2s，Ease Out Cubic；镜头向后拉出，FOV 75° → 60°；Vignette 快速消退

### 5. 速度感知视觉（Speed Perception）

| 速度区间 | 激活效果 | 实现方式 |
|---|---|---|
| 0–30% 软上限 | 尾焰长度线性增长 | ParticleSystem 曲线 |
| 30–70% 软上限 | 飞船轮廓 Emission 亮度微增（+20%） | Material Property Block |
| 70–100% 软上限 | 屏幕边缘径向速度线（Speed Lines，透明度 0–0.15） | URP 自定义 Blit Pass，4 条静态线段 |
| 达到软上限 | 尾焰颜色从 `#4488FF` → `#AADDFF`，0.3s 脉冲 | 颜色插值 |

### 6. 音效事件列表

| 事件 ID | 触发条件 | 播放方式 |
|---|---|---|
| `SFX_THRUST_START` | thrust_input 离开死区 | OneShot，0.1s |
| `SFX_THRUST_LOOP` | thrust_input > 0 持续 | Loop，音量随 thrust_input 线性 |
| `SFX_THRUST_STOP` | thrust_input 回到死区 | OneShot，0.15s |
| `SFX_THRUST_MAX` | thrust_input = 1.0 | OneShot，叠加在 Loop 上 |
| `SFX_LOCK_ACQUIRED` | OnLockAcquired | OneShot，0.2s |
| `SFX_LOCK_LOST` | OnLockLost | OneShot，0.15s |
| `SFX_AUTOFIRE` | 每次自动开火 | OneShot，限速（最小间隔 = 射速倒数） |
| `SFX_HIT_CONFIRM` | 弹道命中目标 | OneShot |
| `SFX_CAM_SWITCH` | 视角切换触发 | OneShot，0.05s |
| `SFX_SPEED_LIMIT` | 速度首次达到软上限 | OneShot，冷却 3s |
| `SFX_ROTATE_IDLE` | 无推力仅转向时 | Loop，低音量（-12dB） |

### 7. 归属边界

| 视觉/音效内容 | 操控系统定义 | 归属方 |
|---|:-----------:|---|
| 推进器尾焰规格（粒子数、颜色、Bloom 参数） | ✅ | — |
| 软锁定准星颜色、动画、尺寸规格 | ✅ | — |
| 弹道 LineRenderer 规格（颜色、长宽比） | ✅ | — |
| 视角切换过渡时长和曲线规格 | ✅ | — |
| 速度感知视觉层次规格 | ✅ | — |
| 音效事件触发列表 | ✅ | — |
| 音效文件资产、混音参数、AudioMixer 配置 | ❌ | 音频系统 GDD |
| 弹道命中的伤害计算 | ❌ | 飞船战斗系统 GDD |
| 飞船 HUD 上的速度表显示 | ❌ | 飞船 HUD GDD |
| 锁定准星的 UI 渲染实现 | ❌ | 飞船 HUD GDD |

📌 **Asset Spec** — Visual/Audio 需求已定义。艺术圣经批准后，运行 `/asset-spec system:ship-control-system` 生成每个资产的视觉描述、尺寸规格和生成提示词。

## UI Requirements

飞船操控系统的 UI 需求分为两类：**操控输入 UI**（虚拟摇杆）和 **状态反馈 UI**（锁定准星、视角切换按钮）。

### 操控输入 UI（虚拟摇杆）

| 元素 | 规格 |
|------|------|
| 左摇杆底盘直径 | 手机 100dp / 平板 130dp |
| 左摇杆帽直径 | 手机 44dp / 平板 56dp |
| 右摇杆底盘直径 | 手机 100dp / 平板 130dp |
| 右摇杆帽直径 | 手机 44dp / 平板 56dp |
| 触控热区 | 底盘直径 + 20dp 外扩（四周各 10dp） |
| 锚点类型 | 动态锚点（触点生成），限制在左/右半屏 |
| 底盘透明度 | 待机 40%，激活 85%（0.1s 淡入） |
| 布局区域 | 左摇杆：左半屏底部 1/3；右摇杆：右半屏底部 1/3 |
| 安全区 | 距屏幕边缘 ≥ 24dp |
| 误触保护 | 触点接触面积 > 200px² 视为手掌，忽略；80ms 内位移 < 3dp 视为误触 |

### 状态反馈 UI（归属飞船 HUD GDD）

| 元素 | 规格来源 |
|------|---------|
| 软锁定准星 | 本 GDD §Visual/Audio Requirements §2 |
| 视角切换按钮 | 飞船 HUD GDD（按钮位置、尺寸、图标） |
| 速度表 | 飞船 HUD GDD（读取 `Rigidbody.linearVelocity` 模长） |

> 操控系统不直接持有或操作任何 UI 组件。虚拟摇杆 UI 由 UI 层实现，通过回调将归一化输入值传递给操控系统。

📌 **UX Flag — 飞船操控系统**：本系统有 UI 需求（虚拟摇杆布局、触控热区规格）。在 Pre-Production 阶段，运行 `/ux-design` 为驾驶舱 HUD 创建完整 UX 规格（`design/ux/cockpit-hud.md`），在写 Epic 之前完成。

## Acceptance Criteria

**AC-CTRL-01：输入门控**
GIVEN 飞船当前状态为 `DOCKED`（非 `IN_COCKPIT` / `IN_COMBAT`），WHEN 测试工具向左摇杆输入幅度 = 0.8 的推力指令，THEN `Rigidbody.linearVelocity` 在该帧及后续 3 帧内保持不变（delta = 0），不触发任何推力音效或推进器粒子效果。

**AC-CTRL-02：死区过滤**
GIVEN 飞船处于 `IN_COCKPIT`，初始速度为零，WHEN 左摇杆输入幅度 = `JOYSTICK_DEAD_ZONE × 0.9`（低于死区阈值），THEN `thrust_input` 计算结果为 0.0，`Rigidbody.linearVelocity` 在该帧保持零，推进器动画不播放。

**AC-CTRL-03：推力与转向同帧生效**
GIVEN 飞船处于 `IN_COCKPIT`，当前朝向角 = 0°，速度 = 0，WHEN 左摇杆输入方向角 = 45°（斜右上方），幅度 = 1.0，持续 1 帧，THEN 飞船朝向角变化量 > 0（右转），且 `Rigidbody.linearVelocity` 模长 > 0（推力同帧施加）；两者在同一帧内均不为零。

**AC-CTRL-04：无后退推力**
GIVEN 飞船处于 `IN_COCKPIT`，朝向角 = 0°（正前方为 +Z），WHEN 左摇杆向下满偏（`offset.y = -1.0`，`offset.x = 0`），持续 5 帧，THEN `Rigidbody.linearVelocity` 的 Z 分量不向负方向增加；若当前有前向速度，速度因惯性自然衰减但不反向加速。

**AC-CTRL-05：惯性保留**
GIVEN 飞船处于 `IN_COCKPIT`，当前速度 = `SHIP_MAX_SPEED × 0.8`（朝 +Z 方向），WHEN 玩家松开摇杆（输入幅度 = 0），持续 10 帧，THEN 第 10 帧结束时飞船速度模长 > 0（未立即停止），速度方向保持 +Z（方向偏差 < 2°）。

**AC-CTRL-06：朝向与速度解耦**
GIVEN 飞船处于 `IN_COCKPIT`，当前速度方向 = +Z（0°），速度模长 = `SHIP_MAX_SPEED × 0.5`，WHEN 玩家将飞船朝向旋转 90°（不施加额外推力），持续 5 帧，THEN 速度方向仍保持 +Z（偏差 < 2°），速度模长变化量 < 5%（仅因阻力衰减，不因转向而改变）。

**AC-CTRL-07：软锁定激活**
GIVEN 飞船处于 `IN_COMBAT`，目标在 `LOCK_RANGE` 内，`aim_angle` = 10°（小于 `FIRE_ANGLE_THRESHOLD`），WHEN 该状态持续 1 帧，THEN `SoftLockTarget != null`，`OnLockAcquired` 事件触发，锁定指示器显示在目标上。

**AC-CTRL-08：软锁定不激活（角度超出）**
GIVEN 飞船处于 `IN_COMBAT`，目标在 `LOCK_RANGE` 内，`aim_angle` = 90°（远超 `FIRE_ANGLE_THRESHOLD`），WHEN 该状态持续 5 帧，THEN `SoftLockTarget = null`，`OnLockAcquired` 事件不触发，锁定指示器不显示。

**AC-CTRL-09：自动开火**
GIVEN 飞船处于 `IN_COMBAT`，`SoftLockTarget != null`，武器冷却计时器 = 0，`aim_angle ≤ FIRE_ANGLE_THRESHOLD`，WHEN 该状态持续 1 帧（无需玩家额外输入），THEN 武器发射事件触发，武器冷却计时器重置为配置值；若 `SoftLockTarget = null`，则自动开火不触发。

**AC-CTRL-10：视角切换**
GIVEN 飞船处于 `IN_COCKPIT`，当前视角 = 第三人称，WHEN 玩家点击 HUD 视角切换按钮，THEN 相机切换至第一人称（相机位置绑定到 `CockpitAnchor`，偏差 < 0.01 单位）；再次点击后切回第三人称（相机位置恢复至 `CAMERA_OFFSET`，偏差 < 0.01 单位）；视角切换不影响飞船当前速度或朝向。

**AC-CTRL-11：IN_COCKPIT 初始化**
GIVEN 飞船当前状态为 `DOCKED`，WHEN 触发进入驾驶舱事件（状态切换至 `IN_COCKPIT`），THEN 在状态切换完成后的第 1 帧内：输入系统激活（C-1 门控开放），`SoftLockTarget = null`，武器冷却计时器 = 0，`CameraMode = THIRD_PERSON`。

**AC-CTRL-12：DESTROYED 状态清理**
GIVEN 飞船处于 `IN_COMBAT`，`SoftLockTarget != null`，推进器正在运行，WHEN 飞船 `CurrentHull` 降至 0（状态切换至 `DESTROYED`），THEN 在同一帧内：后续摇杆输入不产生任何速度变化，`SoftLockTarget = null`，`OnLockLost` 事件触发，`Rigidbody.isKinematic = true`（5 帧后验证）。

## Open Questions

| # | 问题 | 影响范围 | 负责人 | 目标解决时间 |
|---|------|---------|--------|------------|
| Q-1 | `SHIP_THRUST_POWER`、`SHIP_TURN_SPEED`、`SHIP_MAX_SPEED`、`LOCK_RANGE`、`FIRE_ANGLE_THRESHOLD` 的最终值是多少？ | 所有 Formulas、Tuning Knobs、Acceptance Criteria | game-designer + 原型测试 | `/prototype 飞船驾驶舱操控` 完成后 |
| Q-2 | 触觉反馈（震动）是否列为 P0 必须实现？部分低端 Android 设备震动马达精度差，可能产生反效果。 | UI Requirements、飞船 HUD GDD | game-designer | Pre-Production 阶段前 |
| Q-3 | 是否需要支持摇杆灵敏度自定义（玩家设置页）？这会影响 `JOYSTICK_DEAD_ZONE` 和 `AIM_ASSIST_COEFF` 的实现复杂度。 | UI Requirements、设置系统 | game-designer | Vertical Slice 设计阶段 |
| Q-4 | 软锁定目标切换策略：MVP 阶段不自动切换到次优目标（EC-L-02），Vertical Slice 是否升级为"自动切换到最近目标"？ | 规则 L-3、敌人系统 GDD | game-designer | 飞船战斗系统 GDD 设计时 |
| Q-5 | 第一人称视角是否需要独立的 FOV 设置（玩家可调）？部分玩家对高 FOV 有晕动症风险。 | 规则 V-4、飞船 HUD GDD | ux-designer | 飞船 HUD GDD 设计时 |
| Q-6 | 战斗区域边界（规则 P-7 水平面约束）的具体尺寸是多少？边界是固定的还是随战斗场景动态生成？ | 规则 P-7、敌人系统 GDD、飞船战斗系统 GDD | game-designer | 飞船战斗系统 GDD 设计时 |
