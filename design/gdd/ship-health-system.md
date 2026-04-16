# 飞船生命值系统 (Ship Health System)

> **Status**: In Design
> **Author**: game-designer (Claude Code) + user
> **Last Updated**: 2026-04-12
> **Implements Pillar**: 支柱1 经济即军事 / 支柱4 从星图到驾驶舱

## Overview

飞船生命值系统是《星链霸权》飞行战斗层的生死仲裁者：它追踪每艘飞船的当前船体值（`CurrentHull`），接收来自战斗系统的伤害输入，并在 `CurrentHull` 归零时触发飞船销毁——通知飞船系统将状态切换为 `DESTROYED`，同时将玩家踢出驾驶舱或结束战斗。

从数据层看，它是飞船系统与战斗系统之间的中间人：飞船系统提供 `MaxHull` 上限，战斗系统提供伤害量，生命值系统负责计算、存储和广播结果。从玩家感受看，它是驾驶舱里最直接的紧张感来源——血条下降的速度告诉你"还能撑多久"，而归零的那一刻是整个游戏中最有重量的事件之一：你花了 30 矿石和 15 能源建造的飞船，就这样消失了。

MVP 阶段，生命值系统只处理一种飞船类型，不含护盾、护甲或再生机制——纯粹的"受伤→死亡"路径，为后续扩展预留接口。

## Player Fantasy

飞船生命值系统的玩家幻想是**伤痕即历史**——`CurrentHull` 不是一个抽象的百分比，而是这艘船经历过什么的无声记录。

一艘满血的新船和一艘只剩 30% 血量的老船，在星图上是两个完全不同的故事：前者刚从造船厂出来，带着你 30 矿石和 15 能源的期待；后者从上一场战斗中活着回来了，它的每一格缺损都是帝国战史的一部分。玩家不会直接"操作"生命值系统，但他们会因为它而做出最有重量的决策——在星图上看到一艘低血量飞船的图标，犹豫要不要把它派去下一个任务：它已经为你打过一仗了，再派它去，可能回不来。

**驾驶舱层（直接感受）**：血条下降的速度是最诚实的战场报告——不需要读数字，凭直觉就知道"还能撑住"还是"该撤了"。这种感受是即时的、内脏的，是支柱4（从星图到驾驶舱）的核心体验之一。

**星图层（间接感受）**：飞船图标的状态变化是帝国健康度的缩影。一个变红的图标不是一个数字警告，而是"那艘船正在受苦"的信号——而你不在那里。这种距离感和无力感，反过来驱动玩家在下次战斗前主动切换视角亲自驾驶。

**支柱对齐：**
- **支柱1（经济即军事）**：血量归零意味着 30 矿石和 15 能源永久消失，帝国的一部分死了
- **支柱3（我的星际帝国）**：每艘飞船的伤痕都是帝国历史的一部分，不是可随时刷新的消耗品
- **支柱4（从星图到驾驶舱）**：星图上的受损图标是驱动玩家跳入驾驶舱的核心信号之一

**锚点时刻**：玩家在星图上看到一艘低血量飞船的图标，停顿了——要不要把它派去侦察那个稀有矿节点？它从上一场战斗中活着回来，但只剩三成血。玩家决定亲自去开，跳进驾驶舱，看到血条已经残缺——生命值系统正在无声地讲述这艘船的状态。

## Detailed Design

### Core Rules

**规则 H-1（初始化）**

```
触发：ShipInstance 创建完成（ShipState = DOCKED）
操作：CurrentHull ← ShipData.GetMaxHull(instanceId)
约束：CurrentHull 初始值必须 == MaxHull，不允许以残血状态创建飞船
执行方：飞船生命值系统（唯一有权调用 SetCurrentHull 的系统）
```

**规则 H-2（伤害应用）**

```
触发：生命值系统收到 ApplyDamage(instanceId, rawDamage, damageType) 调用
前提：ShipState ∈ {IN_COCKPIT, IN_COMBAT}（DOCKED / IN_TRANSIT 飞船不接受伤害）
操作：
  1. 计算 finalDamage（见 Formulas §D-1）
  2. newHull ← Clamp(CurrentHull - finalDamage, 0, MaxHull)
  3. 调用 ShipData.SetCurrentHull(instanceId, newHull)
  4. 如果 newHull == 0 → 触发规则 H-5（死亡序列）
  5. 否则 → 广播 OnHullChanged(instanceId, newHull, MaxHull) 事件
约束：finalDamage 最小值为 0（不允许负伤害变成治疗）
```

**规则 H-3（武器命中伤害）**

```
触发源：飞船战斗系统调用 ApplyDamage(instanceId, rawDamage, KINETIC)
输入：instanceId（受击飞船）、rawDamage（武器基础伤害，来自武器配置）、damageType（MVP 固定 KINETIC）
处理：走规则 H-2 标准路径
注意：MVP 阶段 damageType 不影响计算，仅预留供后续扩展
```

**规则 H-4（碰撞伤害）**

```
触发源：Unity Physics 碰撞回调（OnCollisionEnter / OnTriggerEnter）
触发条件：飞船 Collider 与 EnemyShip / Obstacle / EnemyProjectile 层碰撞
输入：instanceId、relative_speed（碰撞相对速度，m/s，由 Physics 提供）
处理：
  1. 计算 rawDamage（见 Formulas §D-2）
  2. 调用 ApplyDamage(instanceId, rawDamage, COLLISION)，走规则 H-2
冷却约束：同一碰撞对象对同一飞船的伤害冷却 = COLLISION_DAMAGE_COOLDOWN（0.5 秒）
  防止持续接触造成每帧伤害
```

**规则 H-5（死亡序列）**

```
触发：规则 H-2 中 newHull == 0
执行顺序（严格有序，同一帧内同步完成）：
  Step 1：广播 OnShipDying(instanceId)（供 HUD、音效、VFX 订阅）
  Step 2：调用 ShipData.DestroyShip(instanceId)
           → ShipState = DESTROYED + 通知星图清空 dockedFleet
  Step 3：如果 IsPlayerControlled == true
           → 广播 OnPlayerShipDestroyed(instanceId)（双视角切换系统订阅，强制退出驾驶舱）
  Step 4：广播 OnShipDestroyed(instanceId)（通用销毁完成事件）
约束：Step 2 必须在 Step 3 之前完成（状态先变，视角再退出）
```

**规则 H-6（范围边界）**

生命值系统不负责：护盾、护甲减伤、生命值再生、维修/回血（均为 MVP 范围外）；无人值守战斗失败和节点沦陷导致的销毁（由飞船战斗系统直接调用 `DestroyShip`，绕过生命值系统，不触发 `OnShipDying` 驾驶舱反馈链）；伤害数字 UI 和血条渲染（归属：飞船 HUD GDD）。

---

### States and Transitions

生命值系统**没有独立的状态机**。通过 `CurrentHull / MaxHull` 比值暴露健康阈值，供 HUD 和星图 UI 读取：

| 阈值名 | 条件 | HUD 表现（归属：飞船 HUD GDD） | 星图图标（归属：星图 UI GDD） |
|--------|------|-------------------------------|-------------------------------|
| HEALTHY | ratio > 0.50 | 见 ship-hud.md D-HUD-2 | 图标正常亮度 |
| DAMAGED | 0.25 < ratio ≤ 0.50 | 见 ship-hud.md D-HUD-2 | 图标轻微变暗 |
| CRITICAL | 0 < ratio ≤ 0.25 | 见 ship-hud.md D-HUD-2 | 图标红色警示 |
| DESTROYED | ratio == 0 | 触发死亡序列（规则 H-5） | 图标消失 |

> 阈值是 Tuning Knobs（`HULL_THRESHOLD_DAMAGED = 0.50`，`HULL_THRESHOLD_CRITICAL = 0.25`），存放在外部配置，不硬编码。

**飞船系统状态与伤害接受的关系：**

| ShipState | 接受 ApplyDamage | 说明 |
|-----------|-----------------|------|
| DOCKED | 否（静默忽略） | 停靠中不受伤 |
| IN_TRANSIT | 否（静默忽略） | 星图飞行中不受伤 |
| IN_COCKPIT | 是 | 驾驶舱中可受伤 |
| IN_COMBAT | 是 | 战斗中可受伤 |
| DESTROYED | 否（记录警告日志） | 终态，不接受任何调用 |

---

### Interactions with Other Systems

| 系统 | 数据流向 | 接口 | 说明 |
|------|---------|------|------|
| **飞船系统** | 飞船系统 → 生命值系统 | `ShipData.GetMaxHull(instanceId)` | 初始化 CurrentHull 上限 |
| **飞船系统** | 生命值系统 → 飞船系统 | `ShipData.SetCurrentHull(instanceId, float)` | 写回当前血量（唯一调用方） |
| **飞船系统** | 生命值系统 → 飞船系统 | `ShipData.DestroyShip(instanceId)` | 触发销毁（H-5 Step 2） |
| **飞船战斗系统** | 战斗系统 → 生命值系统 | `HealthSystem.ApplyDamage(instanceId, rawDamage, damageType)` | 武器命中伤害输入 |
| **Unity Physics** | Physics → 生命值系统 | `OnCollisionEnter / OnTriggerEnter` 回调 | 碰撞伤害触发源 |
| **飞船 HUD** | 生命值系统 → HUD | `OnHullChanged(instanceId, currentHull, maxHull)` 事件 | HUD 血条更新 |
| **飞船 HUD** | 生命值系统 → HUD | `OnShipDying(instanceId)` 事件 | 触发死亡 VFX / 音效 |
| **双视角切换系统** | 生命值系统 → 切换系统 | `OnPlayerShipDestroyed(instanceId)` 事件 | 强制退出驾驶舱 |
| **飞船战斗系统** | 生命值系统 → 战斗系统 | `OnShipDestroyed(instanceId)` 事件 | 战斗系统清理战斗状态 |
| **星图系统** | 间接（通过 DestroyShip） | `ShipData.DestroyShip` 内部通知星图 | 星图图标消失 |

## Formulas

### D-1: weapon_damage — 武器命中伤害

The weapon_damage formula is defined as:

`weapon_damage = BASE_DAMAGE × ACCURACY_COEFF`

**Variables:**

| Variable | Symbol | Type | Range | Description |
|----------|--------|------|-------|-------------|
| 基础伤害 | BASE_DAMAGE | float | 1–10 | 武器配置中定义的固定伤害值；MVP 推荐值 8 |
| 命中系数 | ACCURACY_COEFF | float | 0.0–1.0 | 命中精度修正系数；MVP 固定为 1.0，预留供未来扩展（如散射武器、距离衰减） |
| 武器伤害 | weapon_damage | float | 0.0–10.0 | 本次武器命中造成的原始伤害量，作为 apply_damage 的 finalDamage 输入 |

**Output Range:** 0.0 到 10.0（正常游戏中）。MVP 阶段 ACCURACY_COEFF = 1.0，输出等于 BASE_DAMAGE。输出不做额外截断，由 apply_damage（D-4）负责最终边界保护。

**Example:** BASE_DAMAGE = 8，ACCURACY_COEFF = 1.0 → weapon_damage = 8 × 1.0 = **8.0 HP**。100 HP 飞船需要 ⌈100 / 8⌉ = **13 次命中**才能摧毁。

---

### D-2: collision_damage — 碰撞伤害

The collision_damage formula is defined as:

`collision_damage = clamp(COLLISION_COEFF × relative_speed², COLLISION_MIN, COLLISION_MAX)`

**Variables:**

| Variable | Symbol | Type | Range | Description |
|----------|--------|------|-------|-------------|
| 碰撞系数 | COLLISION_COEFF | float | 0.001–0.1 | 将相对速度平方映射为伤害的缩放系数；起始推荐值 0.02 |
| 相对速度 | relative_speed | float | 0.0–∞ m/s | 碰撞时两物体的相对速度，由 Unity Physics 的碰撞回调提供 |
| 碰撞最小伤害 | COLLISION_MIN | float | 0.0–2.0 | 任何有效碰撞的最低伤害；防止低速擦碰无感；起始推荐值 1.0 |
| 碰撞最大伤害 | COLLISION_MAX | float | 1.0–MaxHull×0.10 | 单次碰撞的伤害上限；= SHIP_MAX_HULL × 0.10；MVP 值 = 10.0 |
| 碰撞伤害 | collision_damage | float | COLLISION_MIN–COLLISION_MAX | 本次碰撞造成的原始伤害量，作为 apply_damage 的 finalDamage 输入 |

**Output Range:** 始终在 [COLLISION_MIN, COLLISION_MAX] 之间，即 [1.0, 10.0]（MVP 值）。clamp 保证：低速擦碰不会造成 0 伤害（最低 1 HP），高速正面碰撞不会一击致命（最高 10 HP = MaxHull 的 10%）。

**Example:** relative_speed = 15 m/s，COLLISION_COEFF = 0.02，COLLISION_MIN = 1.0，COLLISION_MAX = 10.0 → 原始值 = 0.02 × 15² = 4.5 → clamp(4.5, 1.0, 10.0) = **4.5 HP**。relative_speed = 30 m/s → 原始值 = 0.02 × 900 = 18.0 → clamp → **10.0 HP**（上限截断）。

---

### D-3: health_ratio — 生命值比率

The health_ratio formula is defined as:

`health_ratio = CurrentHull / MaxHull`

**Variables:**

| Variable | Symbol | Type | Range | Description |
|----------|--------|------|-------|-------------|
| 当前生命值 | CurrentHull | float | 0.0–MaxHull | 飞船当前剩余船体值，由 ShipData 存储 |
| 最大生命值 | MaxHull | float | 1.0–500.0 | 飞船最大船体值上限，由 ShipData 提供；MVP 值 = 100.0 |
| 生命值比率 | health_ratio | float | 0.0–1.0 | 当前生命值占最大生命值的比例，供 HUD 和星图 UI 读取 |

**Output Range:** 0.0 到 1.0，无需截断（CurrentHull 由 apply_damage 保证不超过 MaxHull，不低于 0）。MaxHull 不允许为 0（由 is_valid_ship_instance 在创建时拦截）。

**阈值映射（供 HUD 和星图 UI 使用）：**

| 阈值名 | 条件 | 含义 |
|--------|------|------|
| HEALTHY | health_ratio > HULL_THRESHOLD_DAMAGED | 正常状态 |
| DAMAGED | HULL_THRESHOLD_CRITICAL < health_ratio ≤ HULL_THRESHOLD_DAMAGED | 受损（≤ 50%） |
| CRITICAL | 0 < health_ratio ≤ HULL_THRESHOLD_CRITICAL | 濒死（≤ 25%） |
| DESTROYED | health_ratio == 0.0 | 触发死亡序列（规则 H-5） |

其中 HULL_THRESHOLD_DAMAGED = 0.50，HULL_THRESHOLD_CRITICAL = 0.25（均为 Tuning Knobs）。

**Example:** CurrentHull = 30，MaxHull = 100 → health_ratio = 30 / 100 = **0.30**。0.30 ≤ 0.50 且 0.30 > 0.25 → 状态为 **DAMAGED**，HUD 血条显示黄色。

---

### D-4: apply_damage — 伤害应用（核心公式）

The apply_damage formula is defined as:

`newHull = Clamp(CurrentHull - finalDamage, 0, MaxHull)`

**Variables:**

| Variable | Symbol | Type | Range | Description |
|----------|--------|------|-------|-------------|
| 当前生命值 | CurrentHull | float | 0.0–MaxHull | 受击前飞船的当前船体值 |
| 最终伤害 | finalDamage | float | 0.0–MaxHull | 本次伤害量；来源为 weapon_damage（D-1）或 collision_damage（D-2）；负值被视为 0 |
| 最大生命值 | MaxHull | float | 1.0–500.0 | 飞船最大船体值上限，作为 Clamp 上界 |
| 新生命值 | newHull | float | 0.0–MaxHull | 伤害应用后的船体值；写回 ShipData.SetCurrentHull |

**Output Range:** 始终在 [0.0, MaxHull] 之间。Clamp 下界 0 保证 CurrentHull 不会变为负数；Clamp 上界 MaxHull 保证不会因异常输入超出上限。newHull == 0 时触发规则 H-5（死亡序列）。

**Example:** CurrentHull = 24，finalDamage = 8，MaxHull = 100 → newHull = Clamp(16, 0, 100) = **16.0 HP** → health_ratio = 0.16 → CRITICAL。超额伤害：CurrentHull = 5，finalDamage = 8 → Clamp(-3, 0, 100) = **0.0 HP** → 触发死亡序列。

---

### Formula Ownership Declaration

| 公式 | 归属系统 | 所在 GDD | 说明 |
|------|---------|---------|------|
| `weapon_damage` (D-1) | 飞船生命值系统 | 本文档 | BASE_DAMAGE 值由飞船战斗系统的武器配置提供 |
| `collision_damage` (D-2) | 飞船生命值系统 | 本文档 | relative_speed 由 Unity Physics 提供 |
| `health_ratio` (D-3) | 飞船生命值系统 | 本文档 | HUD/星图 UI 只读不写 |
| `apply_damage` (D-4) | 飞船生命值系统 | 本文档 | 唯一有权修改 CurrentHull 的计算路径 |
| 武器开火频率 | 飞船战斗系统（待建） | ship-combat-system.md | 控制 weapon_damage 被调用的频率，不属于本系统 |
| `is_valid_ship_instance` | 飞船系统 | ship-system.md | 保证 MaxHull > 0，是 health_ratio 除法安全的前提 |

## Edge Cases

**边界条件**

- **如果 `health_ratio` 恰好等于 0.50**：状态为 DAMAGED（不是 HEALTHY）。条件 `ratio > 0.50` 为 HEALTHY，等于 0.50 落入 DAMAGED。这是有意为之——阈值边界偏向保守，确保玩家在恰好半血时看到黄色警告。
- **如果 `health_ratio` 恰好等于 0.25**：状态为 CRITICAL（不是 DAMAGED）。条件 `ratio ≤ 0.25` 为 CRITICAL，等于 0.25 落入 CRITICAL。
- **如果 `finalDamage` 恰好等于 `CurrentHull`**：`newHull = Clamp(0, 0, MaxHull) = 0.0` → 触发死亡序列。死亡触发条件使用 `newHull <= 0.0f`（不用 `== 0`），避免浮点减法累积误差导致漏判。
- **如果 `finalDamage` 为 0.0**：`newHull = CurrentHull`，不变。不广播 `OnHullChanged`（无状态变化，跳过事件广播）。

**并发事件**

- **如果同一帧内同一飞船收到两次 `ApplyDamage` 调用**：Unity 单线程 Update 顺序处理，第二次调用必须读取第一次写入后的 `CurrentHull`（不允许缓存旧值）。这是实现约束，不是设计选择。
- **如果两次 `ApplyDamage` 都足以致命（同帧）**：第一次触发 H-5，`ShipState = DESTROYED`。第二次调用到达时，H-2 的状态检查发现 `DESTROYED`，记录警告日志并拒绝——这是预期行为，不是 bug。
- **如果 `OnShipDying` 事件处理器（H-5 Step 1）内部调用了 `ApplyDamage`**（如链式爆炸 VFX）：此时飞船尚未 `DESTROYED`，会触发重入死亡序列。通过 `_isDying` 标志防止重入：H-5 开始时设置标志，`ApplyDamage` 检查标志后拒绝。

**状态机边缘**

- **如果 `DestroyShip` 被飞船战斗系统直接调用（节点沦陷路径，H-6）时，`ApplyDamage` 正在执行中**：`DestroyShip` 将 `ShipState = DESTROYED`，后续 `ApplyDamage` 的 `newHull <= 0` 检查若再次调用 `DestroyShip`，飞船系统必须幂等——对已 `DESTROYED` 飞船调用 `DestroyShip` 为无操作（不重复触发事件）。
- **如果节点沦陷路径销毁了玩家正在驾驶的飞船**：`OnPlayerShipDestroyed` 不会触发（H-6 绕过 H-5）。双视角切换系统通过监听 `ShipState → DESTROYED` 变化来处理驾驶舱退出（将在双视角切换系统 GDD 中定义）。

**碰撞冷却边缘**

- **如果 `relative_speed` 为 0.0 m/s**（飞船静止贴着障碍物）：`collision_damage = clamp(0, 1.0, 10.0) = 1.0`，飞船每 0.5 秒受到 1 HP 伤害直至死亡。添加最低速度门控：`relative_speed < COLLISION_MIN_SPEED`（推荐值 0.5 m/s）时跳过碰撞伤害计算。
- **如果同一物体有多个 Collider**（如敌方飞船的舰体 + 武器硬点）：碰撞冷却键必须是 `(shipId, colliderInstanceId)` 对，而非 `(shipId, gameObjectId)`，防止多 Collider 绕过冷却。
- **如果飞船在碰撞接触期间被销毁**：Unity 会继续对已销毁对象触发 `OnCollisionStay`。在 H-5 Step 2 中禁用飞船 Collider，或在碰撞回调顶部检查 `ShipState == DESTROYED` 后立即返回。
- **如果 `COLLISION_MIN > COLLISION_MAX`**（配置错误）：`Mathf.Clamp` 行为未定义。启动时添加断言：`COLLISION_MIN <= COLLISION_MAX`。

**公式边缘**

- **如果 `BASE_DAMAGE` 为 0.0**：`weapon_damage = 0`，不造成伤害。武器配置加载时输出验证警告（非硬拒绝），提示设计师检查数据。
- **如果 `MaxHull` 为 0 或负数**（数据损坏）：`health_ratio` 除法将产生除零错误。在 `health_ratio` 计算中添加防御性守卫：`if (MaxHull <= 0) return 0.0`（视为已销毁）。

**初始化边缘**

- **如果 `GetMaxHull()` 返回 0 或负数**（`is_valid_ship_instance` 之后的数据损坏）：H-1 拒绝初始化，记录数据错误，不创建生命值记录。
- **如果 `InitializeHealth` 对同一 `instanceId` 被调用两次**（重生系统或创建管线 bug）：第二次调用记录警告并跳过（不覆盖现有记录），防止意外满血重置。

## Dependencies

**飞船生命值系统对外依赖（上游）：**

| 依赖系统 | 依赖类型 | 数据接口 |
|----------|----------|---------|
| **飞船系统** | 强依赖 | 读取 `GetMaxHull()` 初始化 CurrentHull；写回 `SetCurrentHull()`；调用 `DestroyShip()` 触发销毁 |
| **Unity Physics** | 技术依赖 | `OnCollisionEnter / OnTriggerEnter` 回调提供碰撞事件和 `relative_speed` |

**飞船生命值系统被依赖（下游）：**

| 依赖系统 | 依赖类型 | 数据接口 |
|----------|----------|---------|
| **飞船战斗系统** | 强依赖 | 调用 `ApplyDamage(instanceId, rawDamage, KINETIC)` 输入武器命中伤害；订阅 `OnShipDestroyed` 清理战斗状态 |
| **敌人系统** | 强依赖 | 调用 `ApplyDamage(instanceId, rawDamage, KINETIC)` 输入敌方武器伤害 |
| **飞船 HUD** | 强依赖 | 订阅 `OnHullChanged(instanceId, currentHull, maxHull)` 更新血条；订阅 `OnShipDying(instanceId)` 触发死亡 VFX/音效 |
| **双视角切换系统** | 强依赖 | 订阅 `OnPlayerShipDestroyed(instanceId)` 处理正常死亡路径的驾驶舱退出；同时监听 `ShipState → DESTROYED` 变化处理节点沦陷路径（见 Edge Cases） |

> **双向一致性注记**：飞船系统 GDD 的 Dependencies 节已列出"飞船生命值系统：强依赖，读取 GetMaxHull()，写回 SetCurrentHull()、DestroyShip()"。本节与之一致。飞船战斗系统和敌人系统尚未设计，其 GDD 完成后需在各自的 Dependencies 节中反向引用本系统。

## Tuning Knobs

| 调节旋钮 | 当前值 | 安全范围 | 过高后果 | 过低后果 |
|----------|--------|----------|----------|----------|
| `SHIP_MAX_HULL` | 100 HP（原型起始值，待原型验证后确认） | 50–500 | 飞船难以被摧毁，战斗缺乏张力 | 飞船太脆，玩家不敢进驾驶舱 |
| `BASE_DAMAGE` | 8 HP（13 次命中击毁，MaxHull=100） | 1–10 | 战斗节奏过快，玩家无反应时间 | 战斗拖沓，移动端玩家耐心有限 |
| `ACCURACY_COEFF` | 1.0（MVP 固定值） | 0.5–1.0 | N/A（MVP 固定） | N/A（MVP 固定） |
| `COLLISION_COEFF` | 0.02 | 0.001–0.1 | 碰撞伤害过高，触屏操控精度不足导致玩家频繁受伤 | 碰撞无感，失去操作失误的惩罚感 |
| `COLLISION_MIN` | 1.0 HP | 0.0–2.0 | 轻微擦碰也造成明显伤害，玩家感到不公平 | 低速碰撞无伤害，失去碰撞反馈 |
| `COLLISION_MAX` | 10.0 HP（MaxHull 的 10%） | MaxHull×0.05–MaxHull×0.20 | 单次碰撞过于致命，触屏操控容错率过低 | 碰撞威胁感不足，玩家无视障碍物 |
| `COLLISION_DAMAGE_COOLDOWN` | 0.5 秒 | 0.1–2.0 秒 | 持续接触伤害过慢，玩家可以"蹭墙"规避 | 持续接触每帧扣血，飞船瞬间死亡 |
| `COLLISION_MIN_SPEED` | 0.5 m/s | 0.0–2.0 m/s | 低速移动也触发碰撞伤害，停靠操作受惩罚 | 高速碰撞才触发，失去碰撞反馈 |
| `HULL_THRESHOLD_DAMAGED` | 0.50（50%） | 0.30–0.70 | 玩家在高血量时就看到黄色警告，警告失去意义 | 玩家在极低血量才看到警告，反应时间不足 |
| `HULL_THRESHOLD_CRITICAL` | 0.25（25%） | 0.10–0.40 | 玩家在较高血量时就进入红色警报，过度紧张 | 玩家在极低血量才进入红色警报，警报失去意义 |

> **旋钮交互注意**：`HULL_THRESHOLD_CRITICAL` 必须始终 < `HULL_THRESHOLD_DAMAGED`（否则阈值逻辑失效）。`COLLISION_MAX` 应随 `SHIP_MAX_HULL` 同步调整（保持 10% 比例关系）。`BASE_DAMAGE` 和 `SHIP_MAX_HULL` 共同决定战斗节奏，调整其中一个时需重新验证另一个。

> **来自飞船系统的旋钮**：`SHIP_MAX_HULL` 的权威定义在 `design/gdd/ship-system.md`，本系统引用该值。调整须同步更新 `design/registry/entities.yaml`。

## Visual/Audio Requirements

> **范畴声明**：本章节定义生命值系统所有权的视觉/音效规格。DESTROYED 爆炸效果已在 ship-system.md §Visual/Audio Requirements 完整定义，本章节仅引用，不重复定义。HUD 血条的完整交互规格归属飞船 HUD GDD；星图图标的完整规格归属星图 UI GDD。

### 1. 飞船本体视觉反馈

#### HEALTHY → DAMAGED（hull_ratio 跌破 0.50）

**飞船本体（驾驶舱视角）**
- Emission 颜色从阵营色（玩家 `#4FC3F7` / 敌方 `#FF5722`）向暖黄 `#FFC107` 偏移，混合比例 30%（70% 阵营色 + 30% 暖黄）
- 引擎尾焰粒子尺寸缩小 20%，表现推力受损
- 受损火花粒子：在飞船几何体随机位置持续生成，Particle Count ≤ 6，颜色 `#FF8C00`，生命周期 0.8 秒，循环播放

**星图图标（委托）**
- 图标 Emission 呼吸脉冲频率从 1Hz 提升至 2Hz；图标下方出现橙色状态点（直径 4dp，颜色 `#FFC107`）
- *归属：星图 UI GDD — 本 GDD 定义触发事件 `OnHullChanged` 阈值穿越*

#### DAMAGED → CRITICAL（hull_ratio 跌破 0.25）

**飞船本体（驾驶舱视角）**
- Emission 颜色进一步偏移至橙红 `#FF5722`，混合比例 70%（30% 阵营色 + 70% 橙红）；玩家飞船此时 Emission 与敌方飞船相近——视觉上传达"濒死"而非"阵营"
- 受损火花粒子密度翻倍：Particle Count ≤ 12，新增黑烟粒子 ≤ 8（颜色 `#333333`，Alpha 0.6，生命周期 1.2 秒）
- 引擎尾焰做不规则闪烁：以 4–6Hz 随机频率在 60%–100% 亮度之间抖动，模拟引擎故障

**星图图标（委托）**
- 状态点颜色切换为红色 `#FF2200`，直径扩大至 6dp；图标整体亮度降低 30%
- *归属：星图 UI GDD*

#### CRITICAL → HEALTHY（回血越过 0.50 阈值，如有修复机制）

- 受损粒子效果在 0.5 秒内线性淡出；Emission 颜色在 0.8 秒内插值回阵营色
- *注：MVP 阶段无修复机制（参见 Open Questions），此规格为预留*

---

### 2. `OnShipDying` 事件视觉（死亡序列启动帧）

`OnShipDying` 在 hull == 0 时触发，是 DESTROYED 爆炸效果的**前置帧**，持续约 0.15 秒：

- 飞船所有 Emission 在 0.1 秒内骤升至峰值白色 `#FFFFFF`（Bloom Intensity × 2.0），随后立即进入 ship-system.md 定义的 Flash 阶段
- 受损粒子全部停止生成（清空循环粒子系统）
- 引擎尾焰在同一帧熄灭（Emission 归零）

> 后续 Flash → Breakup → Dissipate 三阶段效果完整定义见 ship-system.md §Visual/Audio Requirements — DESTROYED。本 GDD 不重复定义。

---

### 3. `OnPlayerShipDestroyed` 屏幕空间效果

仅在 `IsPlayerControlled == true` 的飞船触发 `OnShipDying` 时激活（驾驶舱视角下）：

- **屏幕红色闪光**：全屏 Overlay，颜色 `#FF0000`，Alpha 峰值 0.6，持续 0.2 秒后线性淡出至 0（总时长 0.5 秒）
- **相机震动**：Amplitude 0.15，Frequency 12Hz，Duration 0.4 秒
- **画面失真**：URP Chromatic Aberration，Intensity 峰值 0.8，在 0.3 秒内衰减至 0
- **强制视角退出**：0.5 秒后触发双视角切换系统将玩家踢回星图层（视觉过渡归属双视角切换系统 GDD）

*归属：屏幕红色闪光、相机震动、画面失真归本 GDD（生命值系统触发）；视角退出动画归双视角切换系统 GDD*

---

### 4. 音效事件规格

> 具体音效文件、混音参数、AudioMixer 配置由音频系统 GDD 定义。本节定义事件 ID、触发条件和意图。

| 事件 ID | 触发条件 | 播放方式 | 优先级 | 意图 |
|---------|---------|---------|--------|------|
| `SFX_HULL_HIT` | `OnHullChanged`（hull 减少） | 一次性，3D（驾驶舱）/ 2D（星图） | 中 | 受击反馈，每次扣血触发 |
| `SFX_HULL_HIT_CRITICAL` | `OnHullChanged` 且 hull_ratio ≤ 0.25 | 一次性，3D | 高 | 危急受击，音调更低沉 |
| `SFX_DAMAGE_LOOP_DAMAGED` | 进入 DAMAGED 阈值 | 循环，3D（驾驶舱层） | 低 | 持续受损环境音（电气故障嗡鸣） |
| `SFX_DAMAGE_LOOP_CRITICAL` | 进入 CRITICAL 阈值 | 循环，3D（驾驶舱层） | 中 | 替换 DAMAGED 循环，更紧张的警报音 |
| `SFX_DAMAGE_LOOP_STOP` | hull_ratio 回升越过 0.50 | 停止循环 | — | 停止所有受损环境音循环 |
| `SFX_SHIP_DYING` | `OnShipDying` | 一次性，2D | 最高 | 死亡序列启动音（金属撕裂感） |
| `SFX_PLAYER_SHIP_DESTROYED` | `OnPlayerShipDestroyed` | 一次性，2D | 最高 | 玩家飞船专属死亡音（更具冲击力，区别于 AI 飞船） |

> `SFX_SHIP_DESTROYED`（爆炸完成音）已在 ship-system.md §Visual/Audio Requirements 定义，本 GDD 不重复定义。

---

### 5. HUD 视觉反馈（委托规格）

> 完整 HUD 规格归属飞船 HUD GDD。本节定义生命值系统对 HUD 的数据输出和阈值语义。

**血条颜色语义（HUD GDD 必须遵守）**

| 阈值 | hull_ratio 范围 | 血条颜色 |
|------|----------------|---------|
| HEALTHY | > 0.50 | 冷蓝 `#4FC3F7` |
| DAMAGED | 0.25–0.50 | 暖黄 `#FFC107` |
| CRITICAL | 0–0.25 | 红色 `#FF2200`（1Hz 闪烁，HUD GDD 定义实现） |

**屏幕空间效果（本 GDD 定义参数，HUD GDD 实现）**
- CRITICAL 状态持续期间：URP Vignette，颜色 `#FF0000`，Intensity 0.15
- 每次受击（`OnHullChanged` hull 减少）：屏幕轻微震动，Amplitude 0.03，Duration 0.1 秒

---

### 6. 星图视觉反馈（委托规格）

> 完整星图图标规格归属星图 UI GDD。本节定义生命值系统对星图层的数据输出语义。

| 阈值 | 图标表现 | 触发事件 |
|------|---------|---------|
| HEALTHY | 阵营色 Emission，1Hz 呼吸脉冲，无状态点 | `OnHullChanged` ratio > 0.50 |
| DAMAGED | 2Hz 呼吸脉冲，橙色状态点 `#FFC107`（4dp） | `OnHullChanged` ratio ≤ 0.50 |
| CRITICAL | 图标亮度 -30%，红色状态点 `#FF2200`（6dp） | `OnHullChanged` ratio ≤ 0.25 |
| DESTROYED | 图标立即消失 | `OnShipDestroyed` |

---

### 7. 归属边界汇总

| 视觉/音效内容 | 本 GDD 定义 | 归属方 |
|-------------|:-----------:|--------|
| 阈值穿越时飞船 Emission 颜色偏移规格 | ✅ | — |
| 受损火花 + 黑烟粒子规格 | ✅ | — |
| 引擎尾焰故障闪烁规格 | ✅ | — |
| `OnShipDying` 前置帧 Emission 骤升规格 | ✅ | — |
| `OnPlayerShipDestroyed` 屏幕红闪 + 相机震动 + 色差规格 | ✅ | — |
| 生命值音效事件 ID 列表 | ✅ | — |
| HUD 血条颜色语义（阈值→颜色映射） | ✅（语义） | 飞船 HUD GDD（实现） |
| CRITICAL 状态 Vignette 强度参数 | ✅（参数） | 飞船 HUD GDD（实现） |
| 受击屏幕震动强度参数 | ✅（参数） | 飞船 HUD GDD（实现） |
| 星图图标阈值状态映射 | ✅（语义） | 星图 UI GDD（实现） |
| DESTROYED 爆炸三阶段效果 | ❌（引用） | ship-system.md §Visual/Audio Requirements |
| 音效文件、混音参数、AudioMixer 配置 | ❌ | 音频系统 GDD |
| 视角退出动画（玩家死亡后返回星图） | ❌ | 双视角切换系统 GDD |

📌 **Asset Spec** — Visual/Audio 需求已定义。艺术圣经批准后，运行 `/asset-spec system:ship-health-system` 生成每个资产的视觉描述、尺寸规格和生成提示词。

## UI Requirements

飞船生命值系统是纯数据-状态层，本身不负责任何 UI 渲染。玩家看到的生命值相关界面由以下系统负责：

| UI 元素 | 负责系统 |
|---------|---------|
| 驾驶舱血条（颜色、闪烁、动画） | 飞船 HUD GDD |
| 受击屏幕震动（Amplitude 0.03，Duration 0.1s） | 飞船 HUD GDD（参数由本 GDD 定义） |
| CRITICAL 状态 Vignette（Intensity 0.15） | 飞船 HUD GDD（参数由本 GDD 定义） |
| 星图飞船图标状态变化（颜色、状态点、亮度） | 星图 UI GDD |
| 玩家死亡屏幕红闪 + 色差 | 本 GDD 定义触发，飞船 HUD GDD 实现 |

> 生命值系统通过 `OnHullChanged`、`OnShipDying`、`OnPlayerShipDestroyed`、`OnShipDestroyed` 事件通知 UI 层更新显示，不直接持有或操作任何 UI 组件。

## Acceptance Criteria

### 初始化（Initialization）

**AC-HEALTH-01（对应 H-1）**
GIVEN 一艘飞船实例通过 `ShipData.GetMaxHull(instanceId)` 返回值为 100，WHEN 飞船生命值系统完成该实例的初始化，THEN `CurrentHull == 100`，`health_ratio == 1.0`，飞船处于 HEALTHY 阈值区间。

**AC-HEALTH-02（对应 D-3）**
GIVEN 一艘飞船 `CurrentHull = 60`、`MaxHull = 100`，WHEN 系统计算 `health_ratio`，THEN 返回值为 `0.60`（精度误差 ≤ 0.001），阈值判定为 HEALTHY（ratio > 0.50）。

### 伤害应用（Damage Application）

**AC-HEALTH-03（对应 H-2 — IN_COCKPIT 接受伤害）**
GIVEN 一艘飞船处于 `IN_COCKPIT` 状态，`CurrentHull = 80`，WHEN 系统调用 `ApplyDamage(instanceId, 20)`，THEN `CurrentHull` 变为 `60`，`OnHullChanged` 事件触发一次。

**AC-HEALTH-04（对应 H-2 — IN_COMBAT 接受伤害）**
GIVEN 一艘飞船处于 `IN_COMBAT` 状态，`CurrentHull = 50`，WHEN 系统调用 `ApplyDamage(instanceId, 10)`，THEN `CurrentHull` 变为 `40`，`OnHullChanged` 事件触发一次。

**AC-HEALTH-05（对应 H-2 — DOCKED 静默忽略）**
GIVEN 一艘飞船处于 `DOCKED` 状态，`CurrentHull = 80`，WHEN 系统调用 `ApplyDamage(instanceId, 20)`，THEN `CurrentHull` 保持 `80` 不变，`OnHullChanged` 事件不触发，无任何错误日志。

**AC-HEALTH-06（对应 H-2 — IN_TRANSIT 静默忽略）**
GIVEN 一艘飞船处于 `IN_TRANSIT` 状态，`CurrentHull = 80`，WHEN 系统调用 `ApplyDamage(instanceId, 20)`，THEN `CurrentHull` 保持 `80` 不变，`OnHullChanged` 事件不触发，无任何错误日志。

**AC-HEALTH-07（对应 H-2 — DESTROYED 记录警告）**
GIVEN 一艘飞船处于 `DESTROYED` 状态，WHEN 系统调用 `ApplyDamage(instanceId, 20)`，THEN `CurrentHull` 不变，`OnHullChanged` 不触发，系统日志中出现一条包含该 `instanceId` 的 Warning 级别日志。

**AC-HEALTH-08（对应 D-4 — 伤害下限钳制）**
GIVEN 一艘飞船 `CurrentHull = 5`，WHEN 系统调用 `ApplyDamage(instanceId, 100)`（伤害远超当前血量），THEN `CurrentHull` 被钳制为 `0`，不出现负值，死亡序列触发。

**AC-HEALTH-09（对应 D-4 — 零伤害跳过事件）**
GIVEN 一艘飞船 `CurrentHull = 80`，WHEN 系统调用 `ApplyDamage(instanceId, 0)`，THEN `CurrentHull` 保持 `80` 不变，`OnHullChanged` 事件不触发。

### 武器伤害公式（Weapon Damage Formula）

**AC-HEALTH-10（对应 H-3 / D-1 — MVP 武器伤害值）**
GIVEN `BASE_DAMAGE = 8`，`ACCURACY_COEFF = 1.0`，WHEN 战斗系统计算一次武器命中的 `weapon_damage`，THEN `weapon_damage = 8.0`，`ApplyDamage` 接收到的 `finalDamage` 参数值为 `8.0`。

**AC-HEALTH-11（对应 D-1 — 公式结构验证）**
GIVEN `BASE_DAMAGE = 8`，`ACCURACY_COEFF = 0.5`，WHEN 计算 `weapon_damage`，THEN `weapon_damage = 4.0`，结果与公式 `BASE_DAMAGE × ACCURACY_COEFF` 一致。

### 碰撞伤害公式（Collision Damage Formula）

**AC-HEALTH-12（对应 H-4 / D-2 — 正常碰撞伤害计算）**
GIVEN `COLLISION_COEFF = 0.02`，两物体相对速度为 `15.0 m/s`，WHEN 碰撞事件触发，THEN `collision_damage = clamp(0.02 × 225, 1.0, 10.0) = clamp(4.5, 1.0, 10.0) = 4.5`，`ApplyDamage` 接收到 `4.5`。

**AC-HEALTH-13（对应 D-2 — 碰撞伤害上限钳制）**
GIVEN 两物体相对速度为 `30.0 m/s`（高速碰撞），WHEN 碰撞事件触发，THEN `collision_damage` 被钳制为 `COLLISION_MAX = 10.0`，不超过上限。

**AC-HEALTH-14（对应 D-2 — 碰撞伤害下限钳制）**
GIVEN 两物体相对速度为 `0.6 m/s`（刚超过最低速度阈值），WHEN 碰撞事件触发，THEN `collision_damage` 被钳制为 `COLLISION_MIN = 1.0`，不低于下限。

**AC-HEALTH-15（对应 H-4 — 最低速度阈值过滤）**
GIVEN 两物体相对速度为 `0.4 m/s`（低于 `COLLISION_MIN_SPEED = 0.5 m/s`），WHEN 碰撞事件触发，THEN `ApplyDamage` 不被调用，`CurrentHull` 不变，`OnHullChanged` 不触发。

**AC-HEALTH-16（对应 H-4 — 碰撞冷却时间）**
GIVEN 飞船A与飞船B发生碰撞并触发伤害，WHEN 在 `0.5` 秒内同一碰撞对（A-B）再次发生碰撞，THEN 第二次碰撞的 `ApplyDamage` 不被调用，`CurrentHull` 不变。

**AC-HEALTH-17（对应 H-4 — 冷却时间独立性）**
GIVEN 飞船A与飞船B发生碰撞（A-B 对进入冷却），WHEN 在冷却期间飞船A与飞船C发生碰撞，THEN A-C 碰撞对正常触发伤害（冷却时间按碰撞对独立计算）。

### 死亡序列（Death Sequence）

**AC-HEALTH-18（对应 H-5 — 非玩家飞船死亡序列）**
GIVEN 一艘 `IsPlayerControlled = false` 的飞船 `CurrentHull = 5`，WHEN `ApplyDamage(instanceId, 10)` 使 `newHull <= 0`，THEN 同一帧内按顺序触发：① `OnShipDying`，② `DestroyShip`，③ `OnShipDestroyed`；`OnPlayerShipDestroyed` 不触发。

**AC-HEALTH-19（对应 H-5 — 玩家飞船死亡序列）**
GIVEN 一艘 `IsPlayerControlled = true` 的飞船 `CurrentHull = 3`，WHEN `ApplyDamage(instanceId, 5)` 使 `newHull <= 0`，THEN 同一帧内按顺序触发：① `OnShipDying`，② `DestroyShip`，③ `OnPlayerShipDestroyed`，④ `OnShipDestroyed`。

**AC-HEALTH-20（对应 H-5 — 死亡触发条件使用 <= 0）**
GIVEN 一艘飞船 `CurrentHull = 1`，WHEN `ApplyDamage(instanceId, 5)`（`newHull = -4`），THEN 死亡序列正常触发（验证触发条件为 `newHull <= 0.0f`，不依赖精确等于 0）。

**AC-HEALTH-21（对应 H-5 — _isDying 防重入）**
GIVEN 一艘飞船正在执行死亡序列（`_isDying = true`），WHEN `OnShipDying` 回调中有代码再次调用 `ApplyDamage`，THEN 第二次死亡序列不触发，`DestroyShip` 只被调用一次，无重复事件。

### 生命值阈值转换（Health Threshold Transitions）

**AC-HEALTH-22（对应 HEALTHY → DAMAGED 转换）**
GIVEN 一艘飞船 `CurrentHull = 60`、`MaxHull = 100`（ratio = 0.60，HEALTHY），WHEN `ApplyDamage(instanceId, 15)` 使 `CurrentHull = 45`（ratio = 0.45），THEN 系统判定阈值从 HEALTHY 变为 DAMAGED。

**AC-HEALTH-23（对应 DAMAGED → CRITICAL 转换）**
GIVEN 一艘飞船 `CurrentHull = 30`、`MaxHull = 100`（ratio = 0.30，DAMAGED），WHEN `ApplyDamage(instanceId, 10)` 使 `CurrentHull = 20`（ratio = 0.20），THEN 系统判定阈值从 DAMAGED 变为 CRITICAL。

**AC-HEALTH-24（对应 CRITICAL → DESTROYED 转换）**
GIVEN 一艘飞船 `CurrentHull = 10`、`MaxHull = 100`（ratio = 0.10，CRITICAL），WHEN `ApplyDamage(instanceId, 10)` 使 `CurrentHull = 0`，THEN 系统判定阈值变为 DESTROYED，死亡序列触发。

**AC-HEALTH-25（对应阈值边界 — HEALTHY/DAMAGED 边界）**
GIVEN 一艘飞船 `CurrentHull = 51`、`MaxHull = 100`（ratio = 0.51），WHEN 系统查询当前阈值，THEN 返回 HEALTHY（ratio > 0.50 为 HEALTHY，边界值 0.50 本身属于 DAMAGED）。

**AC-HEALTH-26（对应阈值边界 — DAMAGED/CRITICAL 边界）**
GIVEN 一艘飞船 `CurrentHull = 25`、`MaxHull = 100`（ratio = 0.25），WHEN 系统查询当前阈值，THEN 返回 CRITICAL（ratio ≤ 0.25 为 CRITICAL，边界值 0.25 本身属于 CRITICAL）。

### 跨系统交互（Cross-System Interaction）

**AC-HEALTH-27（对应 H-6 — 节点捕获绕过生命值系统）**
GIVEN 一艘飞船 `CurrentHull = 80`，WHEN 节点捕获逻辑触发飞船销毁（非战斗路径），THEN `ShipData.DestroyShip()` 被直接调用，`ApplyDamage` 不被调用，`CurrentHull` 不变为 0，飞船状态变为 DESTROYED。

**AC-HEALTH-28（对应 H-6 — 无人值守战斗绕过生命值系统）**
GIVEN 一艘飞船处于无人值守 `IN_COMBAT` 状态，WHEN 无人值守战斗解算判定飞船失败，THEN 飞船战斗系统直接调用 `ShipData.DestroyShip()`，不经过 `ApplyDamage` 路径，`CurrentHull` 不被修改。

**AC-HEALTH-29（对应飞船系统接口集成）**
GIVEN 一艘飞船实例刚创建，`ShipData.GetMaxHull(instanceId)` 返回 `MaxHull`，WHEN 飞船生命值系统初始化该实例，THEN `ShipData.SetCurrentHull(instanceId, MaxHull)` 被调用一次，`CurrentHull` 与 `MaxHull` 相等，`health_ratio = 1.0`。

**AC-HEALTH-30（对应状态机与生命值系统联动）**
GIVEN 一艘 `IN_COCKPIT` 飞船 `CurrentHull = 8`，WHEN `ApplyDamage(instanceId, 8)` 触发死亡序列，THEN `DestroyShip()` 调用后飞船 `ShipState` 变为 `DESTROYED`，双视角切换系统收到通知将玩家强制返回星图层。

---

> **测试类型分类**：AC-HEALTH-01 ~ 26 为 Logic 类型（单元测试，BLOCKING）；AC-HEALTH-27 ~ 30 为 Integration 类型（集成测试，BLOCKING）。所有标准均需自动化测试覆盖，无例外。
>
> **可测试性注记**：AC-HEALTH-18/19（死亡序列顺序）和 AC-HEALTH-21（防重入）需在 Unity Test Framework PlayMode 测试中通过 Mock 事件监听器验证；AC-HEALTH-16（碰撞冷却）需使用可注入时间源（`ITimeProvider`）验证边界条件。

## Open Questions

| # | 问题 | 影响范围 | 负责人 | 目标解决时间 |
|---|------|---------|--------|------------|
| Q-1 | `SHIP_MAX_HULL` 最终值是多少？（当前原型值 100 HP） | 所有伤害公式、战斗节奏、Acceptance Criteria | game-designer + 原型测试 | `/prototype 飞船驾驶舱操控` 完成后 |
| Q-2 | `BASE_DAMAGE` 最终值是多少？（当前推荐值 8 HP） | 战斗节奏、Acceptance Criteria AC-HEALTH-10 | game-designer + 原型测试 | 飞船战斗系统 GDD 设计时 |
| Q-3 | 碰撞伤害的 Unity 物理层配置：EnemyShip / Obstacle / EnemyProjectile 层如何定义？ | H-4 实现、碰撞冷却键结构 | unity-specialist | 架构决策阶段 |
| Q-4 | MVP 之后是否引入飞船维修/回血机制？如有，修复成本和时间如何计算？ | 飞船生命值系统（需新增规则）、资源系统（修复成本） | game-designer | Vertical Slice 设计阶段 |
| Q-5 | 双视角切换系统监听 `ShipState → DESTROYED` 变化的具体实现方式（轮询 vs. 事件订阅）？ | 双视角切换系统 GDD、节点沦陷路径的驾驶舱退出 | unity-specialist | 双视角切换系统 GDD 设计时 |
