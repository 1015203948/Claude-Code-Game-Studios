# 敌人系统 (Enemy System)

> **Status**: Designed (Pending Review)
> **Author**: game-designer (Claude Code) + user
> **Last Updated**: 2026-04-12
> **Implements Pillar**: 支柱4 从星图到驾驶舱 / 支柱1 经济即军事

## Overview

敌人系统（Enemy System）是《星链霸权》驾驶舱层威胁来源的完整实现：它负责在战斗触发时生成敌方飞船实例、驱动敌方 AI 的决策与机动逻辑（侧翼包抄两阶段行为），并在战斗结束时销毁敌方实例、向星图层反馈结果。从数据层看，它接受飞船战斗系统的生成/销毁指令（`SpawnEnemy`、`DespawnEnemy`），以玩家相同的蓝图（`generic_v1`）为基础创建敌方实例，调用飞船生命值系统的 `ApplyDamage` 接口对玩家飞船造成武器伤害，并独立维护自身的 AI 状态机。从玩家感受看，它是让「跳进驾驶舱」这个动作有真实意义的核心威胁：没有敌人，驾驶舱只是一个飞行模拟器；有了敌人，每一次进入驾驶舱都是一个必须用操控技术和战术判断来解决的真实问题。MVP 阶段仅实现一种敌方单位类型（复用玩家蓝图，橙红阵营色），敌方 AI 行为聚焦于侧翼包抄——这是一种简单但有效的压迫策略，迫使玩家主动机动而非被动挨打。

## Player Fantasy

敌人系统的玩家幻想是**有分量的收复**——敌方飞船不是随机刷新的清理目标，而是占据着你想要的星域的竞争者，而你是比它们更精确、更有判断力的那个人。

**情感核心**

从星图层看，一个橙红色的节点不只是「还没拿下的格子」，它是 30 矿石 + 15 能源的机会成本。你决定值不值得亲自去打之前，你在做一道很具体的经济题：损失一艘船的代价 vs. 少派一艘船靠自己技术去拿的代价。这道题每次答案都不一样，但每次都必须由你来决定。

进入驾驶舱后，威胁变成了具体的、有逻辑可读的对手。敌方飞船不乱飞——它有目的地绕弧，试图占据你的后方。你能读懂这个逻辑，就意味着你能提前截断它。它用的武器和你一样（8 点伤害），它的 HP 也和你一样（100 HP）：你赢，不是因为数值优势，而是因为你的每一个机动决定都比它更精确。

**三个锚点时刻**

- *在星图上*：你在地图上看到一个丰矿节点被橙红色标记——它被占了。你在脑子里快速计算：派两艘无人值守稳赢，但另一个节点也需要防守。你选择派一艘，自己跳进驾驶舱亲自开，用操控技巧代替第二艘飞船的资源成本。

- *在驾驶舱里*：敌方飞船从正前方冲过来，你知道它的下一步是向你的后方弧切。你有三秒钟决定：是急转迎击，还是加速拉距等它暴露侧翼。你选对了，它还在弧线路径上时，你的瞄准指示器已经锁定了它。开火。它没打到你一次。

- *节点拿下后*：退出驾驶舱，星图上那个节点的颜色从橙红变成中立，然后你下令建殖民地。「别人占的地方」变成了「我的帝国」。这个转变因为你刚刚亲手打的那一仗而有了具体的重量——不是程序自动结算出来的，是你用判断和操控换来的。

**支柱对齐**
- **支柱1（经济即军事）**：击败敌人是资源扩张的前置成本，不是独立的「战斗关卡」；每艘被击沉的敌方飞船都代表你避免了一次经济损失
- **支柱3（我的星际帝国）**：从敌手中收复的每个节点都让帝国边界向外推进——战斗是帝国建设的动作，不是对建设的打断
- **支柱4（从星图到驾驶舱）**：你对敌人行为模式的认知，是跳进驾驶舱比自动结算更值钱的理由——自动结算不会读敌人的意图，你会

## Detailed Design

### Core Rules

#### E-1 敌方实例数据模型

每个 `EnemyInstance` 独立维护以下字段：

| 字段 | 类型 | 初始值 | 说明 |
|------|------|--------|------|
| `InstanceId` | string | SpawnEnemy 分配 | 全局唯一，格式 `enemy_[uuid]` |
| `BlueprintId` | string | `"generic_v1"` | MVP 固定，从 ShipSystem 读取蓝图参数 |
| `CurrentHull` | float | 100 | 内部维护，不经过 HealthSystem |
| `MaxHull` | float | 100 | 从蓝图 `MaxHull` 字段读取 |
| `AiState` | enum | SPAWNING | 见状态机定义 |
| `TargetInstanceId` | string | 玩家飞船 InstanceId | 战斗期间固定 |
| `FireTimer` | float | 0 | 上次开火后的累计时间，单位秒 |
| `SpawnPosition` | Vector3 | 计算值 | 见 E-3 |

> **注**：敌方 HP 独立维护，不注册至 HealthSystem。HealthSystem 仅管理玩家飞船持久化生命值。

#### E-2 MVP 实例数量

MVP 阶段生成恰好 **2 个**敌方实例（`ai-0` 和 `ai-1`），均使用 `generic_v1` 蓝图，均使用橙红阵营色。差异化敌方类型（属性不同）延至 Vertical Slice。

#### E-3 生成位置规则

生成坐标相对于玩家飞船当前位置计算：

- 两个实例均分布在以玩家为圆心、半径 `SPAWN_RADIUS`（= 150m）的圆弧上
- 角度分配：`θ_A = Random(0°, 360°)`，`θ_B = θ_A + Random(90°, 270°)`，保证两实例至少 90° 角间距
- 生成点不得与现有几何体重叠；若碰撞，沿角度方向平移 10m 后重试，最多重试 3 次

#### E-4 生成时序

- `ai-0`：生成后等待 `Random(3s, 5s)` 的独立随机延迟，然后进入 APPROACHING 状态
- `ai-1`：独立计算自己的随机延迟 `Random(3s, 5s)`
- SPAWNING 期间：保持静止，不开火，不执行距离检测

#### E-5 AI 行为规则

**APPROACHING 阶段**：
- 以直线路径向玩家飞船移动，速度 `ENEMY_MOVE_SPEED`（= 15 m/s）
- 旋转速度上限 `ENEMY_TURN_SPEED`（= 90°/s），使用 `Quaternion.RotateTowards`
- **不开火**（给玩家战术读取窗口）
- 每帧检测与玩家距离，当距离 ≤ `FLANK_ENGAGE_RANGE`（= 80m）时切换至 FLANKING

**FLANKING 阶段**：
- 目标点差异化（防止两实例重叠）：
  - `ai-0`：玩家飞船后方 `FLANK_OFFSET`（= 30m）+ 左侧 5m（本地坐标 X = −5m）
  - `ai-1`：玩家飞船后方 `FLANK_OFFSET`（= 30m）+ 右侧 5m（本地坐标 X = +5m）
- 每帧更新目标点（跟踪玩家移动），以弧线路径机动至目标点
- 开火条件：`aim_angle ≤ FIRE_ANGLE_THRESHOLD` 且 `FireTimer ≥ 1 / WEAPON_FIRE_RATE`
- 开火后重置 `FireTimer = 0`；调用 `HealthSystem.ApplyDamage(playerInstanceId, BASE_DAMAGE, KINETIC)`
- 两实例共享单一物理查询缓冲区（`Physics.OverlapSphereNonAlloc`），避免重复 GC

#### E-6 死亡规则

- 当 `CurrentHull ≤ 0`：进入 DYING 状态
- DYING：停止所有移动和开火，播放销毁 VFX，1.2 秒后自动调用 `EnemySystem.DespawnEnemy(instanceId)`
- 广播 `OnEnemyDied(instanceId)` 事件，让战斗系统判断胜利条件（所有敌方实例均已销毁）

#### MVP 排除项

| 编号 | 排除内容 |
|------|---------|
| M-E-1 | 增援波次（MVP 固定 2 实例，无新生成） |
| M-E-2 | 护盾 / 护甲层（无伤害减免） |
| M-E-3 | 撤退行为（敌方不会主动脱离战斗） |
| M-E-4 | 多种敌方单位类型 |
| M-E-5 | 战斗碎片 / 残骸（销毁后无持久化物体） |
| M-E-6 | 敌方 HP 注册至 HealthSystem |
| M-E-7 | 友方 AI 交互 |
| M-E-8 | 战利品 / 奖励掉落 |

### States and Transitions

#### AI 状态行为表

| 状态 | 移动 | 开火 | 距离检测 | 进入条件 |
|------|------|------|---------|---------|
| **SPAWNING** | 否（静止） | 否 | 否 | `SpawnEnemy()` 调用后立即 |
| **APPROACHING** | 是（直线向玩家） | 否 | 是（每帧） | 随机延迟计时器到期 |
| **FLANKING** | 是（弧线到目标点） | 是（条件满足时） | 否 | 距离 ≤ FLANK_ENGAGE_RANGE |
| **DYING** | 否（停止） | 否 | 否 | `CurrentHull ≤ 0` |

#### 状态转换表

| 触发条件 | 当前状态 | 目标状态 |
|---------|---------|---------|
| 随机延迟（3–5s）到期 | SPAWNING | APPROACHING |
| 与玩家距离 ≤ FLANK_ENGAGE_RANGE | APPROACHING | FLANKING |
| `CurrentHull ≤ 0` | APPROACHING | DYING |
| `CurrentHull ≤ 0` | FLANKING | DYING |
| 1.2s DYING 计时器到期 | DYING | （销毁，无后继状态） |

> 无从 FLANKING 回 APPROACHING 的退出路径（MVP 排除撤退行为，见 M-E-3）。

### Interactions with Other Systems

| 调用方向 | 接口 | 时机 | 说明 |
|---------|------|------|------|
| 战斗系统 → 敌人系统 | `EnemySystem.SpawnEnemy(blueprintId, position)` → `instanceId` | 战斗开始时（各 2 次） | 返回分配的 InstanceId |
| 战斗系统 → 敌人系统 | `EnemySystem.DespawnEnemy(instanceId)` | 战斗强制结束时 | 若 instanceId 不存在则静默忽略 |
| 敌人系统 → 生命值系统 | `HealthSystem.ApplyDamage(playerInstanceId, 8, KINETIC)` | 开火命中时 | 仅对玩家飞船，不对敌方实例 |
| 敌人系统 → 飞船系统 | `ShipSystem.GetBlueprint("generic_v1")` | 生成时（一次） | 读取 MaxHull、视觉参数 |
| 敌人系统 → 战斗系统 | 广播 `OnEnemyDied(instanceId)` | 每次敌方实例销毁时 | 战斗系统统计剩余实例数以判断胜利 |

## Formulas

### F-1 生成角度公式

```
θ_A = Random(0°, 360°)
θ_B = θ_A + Random(90°, 270°)

spawn_x[i] = player_x + SPAWN_RADIUS × cos(θ_i)
spawn_z[i] = player_z + SPAWN_RADIUS × sin(θ_i)
spawn_y[i] = player_y   // 同高度生成
```

| 变量 | 定义 | 值域 |
|------|------|------|
| `θ_A` | ai-0 的生成角度（随机） | [0°, 360°) |
| `θ_B` | ai-1 的生成角度 | θ_A + [90°, 270°) |
| `SPAWN_RADIUS` | 生成半径 | 150m（候选值，待原型验证） |
| `player_x/y/z` | 战斗触发时玩家世界坐标 | — |

**示例**：θ_A = 45°，SPAWN_RADIUS = 150m → spawn_x[0] = 150 × cos(45°) ≈ 106m，spawn_z[0] ≈ 106m

---

### F-2 生成延迟公式

```
spawn_delay[i] = Random(SPAWN_DELAY_MIN, SPAWN_DELAY_MAX)   // 各实例独立抽取
```

| 变量 | 定义 | 候选值 | 安全范围 |
|------|------|--------|---------|
| `SPAWN_DELAY_MIN` | 最短生成延迟 | 3s | [1s, 5s] |
| `SPAWN_DELAY_MAX` | 最长生成延迟 | 5s | [3s, 10s] |

**示例**：ai-0 抽到 3.7s，ai-1 抽到 4.2s → 两实例以不同时间进入 APPROACHING，形成时间差压力。

---

### F-3 AI 移动距离检测

```
distance_to_player = Vector3.Distance(enemy_position, player_position)

// APPROACHING → FLANKING 转换条件：
if distance_to_player ≤ FLANK_ENGAGE_RANGE → 切换至 FLANKING
```

| 变量 | 定义 | 值 |
|------|------|-----|
| `FLANK_ENGAGE_RANGE` | 触发包抄的距离阈值 | 80m（locked，来源：ship-combat-system.md） |

---

### F-4 包抄目标点公式

```
// 玩家后方向量（本地坐标 → 世界坐标）
behind_dir = -player_forward

// ai-0 目标点（左侧偏移）
flank_target[0] = player_position
                + behind_dir × FLANK_OFFSET
                + player_right × (-FLANK_SIDE_OFFSET)

// ai-1 目标点（右侧偏移）
flank_target[1] = player_position
                + behind_dir × FLANK_OFFSET
                + player_right × (+FLANK_SIDE_OFFSET)
```

| 变量 | 定义 | 值 |
|------|------|-----|
| `FLANK_OFFSET` | 目标点在玩家后方的距离 | 30m（locked，来源：ship-combat-system.md） |
| `FLANK_SIDE_OFFSET` | 左右差异化偏移量 | 5m（候选值，待原型验证） |
| `player_right` | 玩家飞船本地右向量（世界空间） | 运行时动态 |

**示例**：玩家朝 +Z 方向，ai-0 目标点 = 玩家位置 + (0,0,−30) + (−5,0,0) = 左后方 30m、左偏 5m

---

### F-5 开火条件公式（cross-reference）

开火逻辑复用 `ship-combat-system.md` 中已定义的公式，不在本文档重复定义：

```
// 引用自 ship-combat-system.md
fire_condition = (aim_angle ≤ FIRE_ANGLE_THRESHOLD) AND (FireTimer ≥ 1 / WEAPON_FIRE_RATE)

// 开火后：
enemy.FireTimer = 0
HealthSystem.ApplyDamage(playerInstanceId, BASE_DAMAGE, KINETIC)
```

| 引用实体 | 来源文档 | 锁定值 |
|---------|---------|--------|
| `aim_angle` 公式 | ship-control-system.md | — |
| `FIRE_ANGLE_THRESHOLD` | ship-combat-system.md | TBD（参考 15°） |
| `WEAPON_FIRE_RATE` | ship-combat-system.md | TBD（参考 1.0 shots/s） |
| `BASE_DAMAGE` | ship-health-system.md | 8 HP |
| `FLANK_ENGAGE_RANGE` | ship-combat-system.md | 80m |
| `ENEMY_MOVE_SPEED` | ship-combat-system.md | 15 m/s |
| `ENEMY_TURN_SPEED` | ship-combat-system.md | 90°/s |

---

### F-6 敌方伤害承受

```
enemy.CurrentHull = enemy.CurrentHull − weapon_damage

// 死亡判定：
if enemy.CurrentHull ≤ 0 → AiState = DYING
```

`weapon_damage` = 玩家武器基础伤害（来源：ship-health-system.md，`BASE_DAMAGE` = 8 HP，`ACCURACY_COEFF` = 1.0）

## Edge Cases

**EC-1 两个敌方实例同时死亡**
- **触发条件**：玩家射击在同一帧内让 ai-0 和 ai-1 的 `CurrentHull` 均降至 ≤ 0（极低概率，但须处理）
- **处理方式**：两者各自独立进入 DYING 状态，各自独立播放 VFX，各自独立广播 `OnEnemyDied`；战斗系统在收到最后一个 `OnEnemyDied` 后触发胜利结算
- **不得发生**：不得因双重广播顺序导致胜利判定被调用两次（战斗系统需幂等处理）

**EC-2 一个实例死亡，另一个继续战斗**
- **触发条件**：正常，ai-0 先死
- **处理方式**：ai-1 不受影响，继续其 AI 状态机；无 AI 状态调整（MVP 无"孤立敌人"特殊行为）
- **玩家可感知**：一个销毁 VFX 出现，另一个继续追击

**EC-3 敌方在 APPROACHING 阶段被击杀**
- **触发条件**：玩家在敌方到达 FLANK_ENGAGE_RANGE 之前将其击毁
- **处理方式**：直接进入 DYING 状态（APPROACHING → DYING 转换已在状态机中定义）
- **不得发生**：不得在 DYING 之前执行任何 FLANKING 阶段逻辑

**EC-4 生成位置碰撞重试失败**
- **触发条件**：在 θ_i 角度方向上连续 3 次 10m 平移后仍与几何体重叠
- **处理方式**：强制在重试终点生成，忽略碰撞；记录警告日志（`[EnemySystem] Spawn position forced after 3 retries`）
- **理由**：MVP 地图空旷，此情况概率极低；不阻塞战斗流程

**EC-5 `DespawnEnemy` 对 DYING 中的实例调用**
- **触发条件**：战斗系统因玩家死亡或其他原因调用 `DespawnEnemy` 时，被销毁的实例已处于 DYING 状态
- **处理方式**：`DespawnEnemy` 对已处于 DYING 的实例立即销毁，跳过 1.2s 等待计时器（防止孤立 VFX 残留）；若 instanceId 不存在，静默忽略

**EC-6 `TargetInstanceId` 指向的玩家实例已销毁**
- **触发条件**：玩家飞船在敌方 AI 帧更新前已销毁（极端时序）
- **处理方式**：AI 帧更新开始时检测 `TargetInstanceId` 是否有效；若无效，进入 DYING 状态，广播 `OnEnemyDied`（战斗结算由战斗系统统一处理）

**EC-7 SPAWNING 期间战斗被强制结束**
- **触发条件**：玩家在生成延迟计时器未到期时即死亡
- **处理方式**：战斗系统调用 `DespawnEnemy`；处于 SPAWNING 状态的实例立即销毁，不播放 DYING VFX，不广播 `OnEnemyDied`（因为它从未真正进入战斗）

**EC-8 两个实例生成角度差恰好落在边界值**
- **触发条件**：`Random(90°, 270°)` 抽到边界值（浮点精度问题）
- **处理方式**：设计上允许恰好 90° 间距（已是最小分离要求），不需要额外处理；边界包含在允许范围内

## Dependencies

### 上游依赖（本系统依赖这些系统的输出）

| 依赖系统 | 依赖内容 | 接口 |
|---------|---------|------|
| **飞船系统** | 蓝图数据（MaxHull、视觉参数） | `ShipSystem.GetBlueprint("generic_v1")` |
| **飞船生命值系统** | 对玩家飞船造成伤害 | `HealthSystem.ApplyDamage(instanceId, damage, type)` |
| **飞船战斗系统** | 生成 / 销毁指令；锁定 FLANK_ENGAGE_RANGE、FLANK_OFFSET、ENEMY_MOVE_SPEED、ENEMY_TURN_SPEED、WEAPON_FIRE_RATE | `SpawnEnemy()` / `DespawnEnemy()` 调用方；常量来源 |

### 下游依赖（这些系统依赖本系统的输出）

| 依赖系统 | 依赖内容 | 接口 |
|---------|---------|------|
| **飞船战斗系统** | 敌方死亡事件（胜利判断） | 监听 `OnEnemyDied(instanceId)` |
| **飞船 HUD** | 敌方 HP 显示（可选，MVP 最低要求：健康状态颜色指示） | 读取 `EnemyInstance.CurrentHull / MaxHull` |

### 反向引用（本系统需在这些文档中体现）

| 文档 | 已更新内容 |
|------|-----------|
| ship-combat-system.md | M-5 已更新（1 → 2 实例）✅；B-5 包抄规则由本系统实现 |
| ship-health-system.md | `ApplyDamage` 的 `referenced_by` 需加入 enemy-system.md（注册表更新阶段处理） |
| ship-system.md | `GetBlueprint` 接口的 `referenced_by` 需加入 enemy-system.md（注册表更新阶段处理） |

## Tuning Knobs

| 常量 | 候选值 | 安全范围 | 影响的游戏性 |
|------|--------|---------|------------|
| `SPAWN_RADIUS` | 150m | [80m, 300m] | 入场感知窗口大小——值越大，玩家有更多时间在敌方到达前准备；值越小，战斗节奏越紧张 |
| `SPAWN_DELAY_MIN` | 3s | [1s, 5s] | 最短入场预期时间——低于 2s 玩家来不及定向；影响驾驶舱初始化的心理缓冲 |
| `SPAWN_DELAY_MAX` | 5s | [3s, 10s] | 最长入场等待时间——过高会让入场感无聊；与 MIN 共同决定两实例的时间差分布 |
| `FLANK_SIDE_OFFSET` | 5m | [2m, 15m] | ai-0 和 ai-1 的左右间距——值太小视觉重叠，值太大两实例看起来互不相关 |
| `ENEMY_MOVE_SPEED` | 15 m/s | [8m/s, 25m/s] | 接近速度——直接决定 APPROACHING 阶段的压迫感和玩家机动窗口（locked in ship-combat-system.md，修改需同步更新） |
| `ENEMY_TURN_SPEED` | 90°/s | [45°/s, 180°/s] | AI 转向灵活度——值过高则 AI 无法被侧翼截断；值过低则 AI 无法跟踪机动的玩家（locked in ship-combat-system.md） |
| `FLANK_ENGAGE_RANGE` | 80m | [40m, 150m] | APPROACHING → FLANKING 的切换距离——决定玩家开始被侧翼包抄前的可机动空间（locked in ship-combat-system.md） |
| `FLANK_OFFSET` | 30m | [15m, 60m] | 包抄目标点与玩家的距离——值越小 AI 越贴近玩家（压迫感强但击中率高）；值越大越容易被截断（locked in ship-combat-system.md） |

> **注**：标注 "locked in ship-combat-system.md" 的值在两个 GDD 中共用，修改时需同步更新两份文档及实体注册表。

## Visual/Audio Requirements

### 视觉需求

**V-1 阵营色**
敌方飞船使用橙红色 `#FF4400`（对应蓝图 `generic_v1` 的阵营色参数覆盖），与玩家飞船的中性灰 / 蓝区分。推力尾焰颜色同步为橙红（复用飞船操控系统的引擎粒子系统，覆盖颜色即可）。

**V-2 生成 VFX**
- 每个实例生成时，在 `SpawnPosition` 播放「闪现入场」效果（类 hyperspace 跃迁闪光，单帧高亮 + 0.3s 淡出）
- SPAWNING 期间飞船主体可见（不隐身），但推力尾焰不激活（静止感）

**V-3 被击中反馈**
- 敌方飞船被玩家武器命中时：播放命中火花粒子（颜色 `#FF8800`，0.1s 持续）
- 无数值弹出（无伤害数字浮字）——简洁风格，HP 状态通过颜色指示（见 UI Requirements）

**V-4 销毁 VFX**
- 进入 DYING 后播放爆炸效果（参考飞船系统 GDD 中的 `DESTROYED` 视觉需求一致）
- 1.2s 内：爆炸粒子 + 碎片飞溅（简易几何碎片，不持久化）
- 1.2s 后：实例完全消失，无残留物

### 音效需求

**A-1 生成音效**
入场时播放短促的能量脉冲音（`sfx_enemy_spawn`，0.2s），与视觉闪现 VFX 同步触发。

**A-2 推力引擎音**
APPROACHING 和 FLANKING 阶段持续播放引擎推力循环音（`sfx_enemy_engine_loop`），音量随距离衰减（3D 空间音效，最大衰减距离 = SPAWN_RADIUS）。

**A-3 武器开火音**
敌方开火时播放武器音效（`sfx_enemy_fire`），与玩家武器音效区分（音色更低沉或更尖锐，待音频总监定义）。

**A-4 销毁音效**
DYING 播放爆炸音效（`sfx_enemy_explode`），与 VFX 同步。

## UI Requirements

**UI-1 敌方 HP 血条（必需，MVP）**
- 每个敌方实例上方显示一条血条（3D 世界空间 → 屏幕空间投影，宽度 60px）
- 颜色渐变：`CurrentHull / MaxHull > 0.5` → 绿色 `#44DD44`；0.2–0.5 → 橙色 `#FFAA00`；< 0.2 → 红色 `#FF2222`
- 无数值文字，仅颜色 + 长度变化
- 两实例各自独立血条，跟随对应飞船 3D 位置（Billboard 朝向相机）

**UI-2 敌方数量计数（必需，MVP）**
- 当任一敌方实例死亡时，在 HUD 角落短暂显示（1.5s）计数变化：「× 2」→「× 1」→「× 0」
- 样式：白色文字，字号 14sp，无背景框，淡出消失

**UI-3 MVP 排除的 HUD 元素**
- 无锁定标记 / 威胁指示器 / 方位罗盘（Vertical Slice 阶段加入飞船 HUD GDD）

> 📌 **UX Flag — 敌人系统**：本系统有 UI 需求（血条、计数显示）。在 Pre-Production 阶段，运行 `/ux-design` 为飞船 HUD 创建 UX spec，战斗期间敌方状态显示应在 `design/ux/hud.md` 中详细规范，而非在本 GDD 直接定义布局。

## Acceptance Criteria

**AC-1 [spawn]** — GIVEN 战斗场景初始化完成，WHEN 敌人系统执行生成逻辑，THEN 场景中恰好存在 2 个敌人实例（InstanceId 分别为 ai-0、ai-1），两者均使用 BlueprintId `generic_v1`，CurrentHull = MaxHull = 100；不得多于或少于 2 个实例。

**AC-2 [spawn][formula]** — GIVEN 玩家位置为原点，WHEN 系统计算生成位置（F-1），THEN ai-0 生成半径距玩家恰好 150m（误差 ±0.1m）；θ_B = θ_A + Random(90°, 270°)，两实例角度间隔在 [90°, 270°] 内。

**AC-3 [spawn][formula]** — GIVEN 系统为两实例计算生成延迟（F-2），WHEN 独立抽取延迟，THEN 两者均在 [3.0s, 5.0s] 内，相互独立；延迟期间 AiState 保持 SPAWNING。

**AC-4 [spawn]** — GIVEN 某实例处于 SPAWNING，WHEN 检查其运动和开火，THEN 位置不发生位移（速度 = 0），不执行开火，不执行距离检查。

**AC-5 [ai-state][formula]** — GIVEN ai-0 进入 APPROACHING，WHEN 每帧检测距离，THEN 以 15 m/s（误差 ±0.5）向玩家直线移动；距离 ≤ 80m（F-3）时当帧切换至 FLANKING。

**AC-6 [ai-state][formula]** — GIVEN ai-0 进入 FLANKING，WHEN 系统计算目标点（F-4），THEN ai-0 目标 = 玩家左后方 30m + 左偏 5m（误差 ±0.1m）；ai-1 目标 = 玩家右后方 30m + 右偏 5m。

**AC-7 [ai-state][combat][formula]** — GIVEN 某实例处于 FLANKING，WHEN aim_angle ≤ FIRE_ANGLE_THRESHOLD 且 FireTimer ≥ 1/WEAPON_FIRE_RATE（F-5），THEN 调用 `HealthSystem.ApplyDamage(playerInstanceId, 8, KINETIC)`，伤害严格为 8 HP，类型严格为 KINETIC；不满足条件时本帧不开火。

**AC-8 [ai-state]** — GIVEN 某实例 CurrentHull 降至 ≤ 0，WHEN 状态机处理（F-6），THEN AiState 立即切换 DYING；停止移动和开火；播放 VFX；1.2s（误差 ±16.7ms @ 60fps）后调用 OnEnemyDied 并销毁实例。

**AC-9 [ai-state]** — GIVEN 敌方飞船存在于场景，WHEN 检查 HealthSystem，THEN HealthSystem 中无任何敌方实例的血量记录；敌方 HP 仅存在于 EnemyInstance.CurrentHull 字段。

**AC-10 [combat]** — GIVEN 两实例均已死亡（均广播 OnEnemyDied），WHEN combat system 接收事件，THEN 胜利结算触发且仅触发一次（幂等性）；两事件同帧到达时调用次数 = 1。

**AC-11 [combat]** — GIVEN 任一实例完成生成（AiState 从 SPAWNING 进入 APPROACHING），WHEN 检查其渲染颜色，THEN 色相在 [0°, 30°] 橙红色系，饱和度 ≥ 0.7，亮度 ≥ 0.5；两实例颜色一致。

**AC-12 [edge-case]** — GIVEN ai-0 在 APPROACHING 阶段被击杀（EC-3），WHEN CurrentHull ≤ 0，THEN ai-0 直接进入 DYING（不经过 FLANKING）；ai-1 AiState 不受影响。

**AC-13 [edge-case]** — GIVEN ai-0 处于 DYING（1.2s 计时中），WHEN 调用 DespawnEnemy(ai-0)（EC-5），THEN ai-0 立即销毁，计时器取消，不等待剩余时间。

**AC-14 [edge-case]** — GIVEN ai-1 处于 SPAWNING，WHEN 调用 DespawnEnemy(ai-1)（EC-7），THEN ai-1 立即销毁；不播放 DYING VFX；不广播 OnEnemyDied；监听方事件计数不增加。

**AC-15 [perf]** — GIVEN 2 个实例均处于 FLANKING（最高计算负载），WHEN 在目标 Android 设备运行 60 秒连续战斗，THEN 帧率 ≥ 55fps；EnemySystem Update 单帧 CPU 耗时 ≤ 1.0ms；AI Update 路径零 GC Alloc。

## Open Questions

**Q-1 FLANK_SIDE_OFFSET 最优值（待原型验证）**
- 当前候选值 5m，需在驾驶舱原型中验证是否足够视觉区分 ai-0 和 ai-1 的位置，同时不让两实例看起来完全无关联。
- 负责人：原型阶段 → unity-specialist
- 目标：`/prototype 飞船驾驶舱操控` 完成后验证

**Q-2 SPAWN_RADIUS 最优值（待原型验证）**
- 当前候选值 150m，需在实际驾驶舱中测试入场感知窗口是否合理——150m 处是否能在移动端屏幕上看到敌方出现，还是太远导致入场 VFX 不可见。
- 负责人：原型阶段 → unity-specialist
- 目标：`/prototype 飞船驾驶舱操控` 完成后验证

**Q-3 FLANKING 无超时退出的拉锯风险**
- 当前设计中，FLANKING 状态无超时退出——如果玩家通过持续高速机动让 AI 始终追不到目标点，战斗是否会进入无限拉锯状态？
- 候选解决方案：FLANKING 超时（如 30s）后直接向玩家冲锋；或保持当前设计（高技术玩家的奖励）
- 目标：原型测试后决定，Vertical Slice 前须锁定

**Q-4 sfx_enemy_fire 音色差异化方案**
- 当前仅标注"待音频总监定义"，具体是比玩家武器音更低沉还是更尖锐，需在音效风格确定后决定
- 目标：飞船 HUD GDD 完成、音效风格确定后处理

**Q-5 舰队调度系统与敌人系统的交互边界**
- 当多艘无人值守舰队与敌方同一节点交战时，是否触发敌人系统的驾驶舱实例？还是纯粹用 `unattended_combat_result` 公式结算？
- 当前假设：无人值守战斗**不**触发敌人系统，敌人系统仅在玩家亲自跳进驾驶舱时激活。需在舰队调度系统 GDD 中明确确认。
- 目标：设计顺序第 10 个（舰队调度系统 GDD）时解决
