# 建筑系统 (Building System)

> **Status**: In Design
> **Author**: Game Designer + Claude Agents
> **Last Updated**: 2026-04-14
> **Implements Pillar**: 支柱1（经济即军事）/ 支柱2（一目了然的指挥）

## Overview

建筑系统定义了殖民地节点上可建造的建筑类型、建造规则，以及建筑对资源网络的持续影响。它是经济层的"生产工具层"——资源系统定义了数值单位和上限，建筑系统决定这些数字从何而来。MVP 阶段包含两种建筑类型：**基础矿场**（增加矿石产出，消耗能源）和**船坞**（启用飞船建造，消耗能源）。建造是即时的（无建造时间）；建筑一经建造即永久生效，不可移除（MVP 范围内）；每座建筑的产出/消耗以常量定义，通过资源系统的生产公式叠加计算。对玩家而言，每一次"建矿场还是建船坞"的决策都是一次军事-经济权衡：建矿场意味着更快的资源积累和更大的扩张能力，建船坞意味着更强的战斗投射能力——两者都需要资源，而资源永远不够用。

## Player Fantasy

建筑系统的核心幻想是：**你是一台战争引擎的设计师**。矿场、船坞——这两种建筑不是装饰，它们是管线的接头。每建一座矿场，矿石产出的数字就往上攀一格；每建一座船坞，舰队生产线就向前延伸一段。当你在后方节点密布矿场、在前线节点排列船坞，然后看着矿石化为飞船、飞船冲向敌方星域，你感受到的是整条经济-军事管线**顺畅运转**的满足——不是单个建筑的意义，而是你亲手接通的那条因果链在工作。

但每一个决策都带着一点重量：建筑不可移除，资源有限，前线节点的建造槽就那么多。当你在资源紧张的时刻悬着手指，想着「再多一座矿场，还是现在就建船坞？」，你感受到的是围棋般的**落子压力**——你在下注。建筑系统不追求宏大的帝国叙事，它追求的是更理性、更机械的快感：每一个决策都有成本，每一笔投入都可以在后续的战局里找到回报（或者代价）。

## Detailed Design

### Core Rules

**建筑类型（BuildingType 枚举）**

| 类型 | 常量标识 | 矿石产出 | 能源消耗 | 建造/升级费用 | 特殊能力 |
|------|---------|---------|---------|---------|---------|
| 基础矿场 | `BasicMine` | +10 矿石/秒 | -2 能源/秒 | 50矿石 + 20能源 | — |
| 船坞（Tier 1） | `Shipyard` | 0 矿石/秒 | -3 能源/秒 | 80矿石 + 30能源 | 解锁 `generic_v1` 飞船建造；`ShipyardTier = 1` |
| 船坞升级（Tier 2） | `ShipyardUpgrade` | 0 矿石/秒 | -2 能源/秒（额外） | 80矿石 + 40能源 | 解锁 `carrier_v1` 建造；航母停靠时可搭载3架战斗机；`ShipyardTier = 2` |

> 以上数值均为只读常量，通过 `ResourceConfig.GetBuildCost(BuildingType)` 和 `ResourceConfig.GetProductionRate(BuildingType)` 查询，不存储在建筑实例中。
>
> **ShipyardTier** 是节点上的整数字段（0 = 无船坞，1 = 基础，2 = 升级），取代旧的 `HasShipyard : bool`。`ShipyardUpgrade` 仅在同节点 `ShipyardTier == 1` 时可建造，就地升级（Tier 值 +1），不新增建筑实例，不触发同类唯一约束。

**建筑实例数据模型（BuildingInstance）**

```
BuildingInstance:
  InstanceId    : string       — 运行时唯一 ID（如 "bld_001"）
  BuildingType  : BuildingType — 建筑类型枚举
  NodeId        : string       — 所在星图节点 ID
  IsActive      : bool         — 是否正在产出（MVP 阶段始终 true）
```

> `IsActive` 为预留字段，MVP 阶段建筑始终 `true`（归属转移后仍产出，只是归属方变更）。产出速率不存储在实例中，运行时通过 `ResourceConfig.GetProductionRate(BuildingType)` 查询。

**每节点建筑数量限制**

每节点同种类型建筑数量**无上限**，资源（矿石 + 能源）是唯一约束。

**建造流程（完整规则序列）**

前置检查（串行验证，任一失败则拒绝并向 UI 返回 FailReason，不执行后续步骤）：

```
检查 C-1（节点归属）：node.ownershipState == PLAYER
检查 C-2（资源充足）：ResourceConfig.CanAfford(currentOre, currentEnergy, BuildingType) == true
```

执行序列（原子操作，不可中断）：

```
步骤 1：从当前资源中扣除建筑费用（ResourceConfig.GetBuildCost(BuildingType)）
步骤 2：创建 BuildingInstance，写入 node.Buildings 列表，IsActive = true
步骤 3：若 BuildingType == Shipyard，设置 node.ShipyardTier = 1
步骤 3b：若 BuildingType == ShipyardUpgrade（前提：node.ShipyardTier == 1），设置 node.ShipyardTier = 2
步骤 4：发出 BuildingSystem.OnBuildingConstructed(nodeId, BuildingType)
```

> 步骤 1 和步骤 2 必须是原子操作：资源扣除后必须立即写入建筑实例，不允许中间态（参照资源系统 Edge Case EC-3）。

**产出激活**：建造完成即立即激活产出，无延迟。

**资源产出更新模型**：遵循资源系统定义的固定 1 秒 tick 机制，不依赖帧率。殖民地系统在每次 tick 时调用 `BuildingSystem.GetNodeProductionDelta(nodeId)` 累加产出；建筑系统不自行定义 tick 节拍。

### States and Transitions

建筑采用两态模型：

| 状态 | 含义 | 产出归谁 |
|------|------|---------|
| `ACTIVE` | 正常运作 | 归当前节点归属方（PLAYER 或 ENEMY） |
| `SUSPENDED` | 暂停（预留，MVP 不使用） | 无 |

**状态转换触发条件**：

```
节点归属 PLAYER → ENEMY（沦陷）：
  建筑 IsActive 保持 true
  产出开始计入敌方殖民地经济（由 AI 殖民地系统消费）
  node.ShipyardTier 保持原值（AI 可查询）

节点归属 ENEMY → PLAYER（收复）：
  建筑 IsActive 保持 true
  产出重新计入玩家殖民地经济
  无需重建——收复即恢复
```

> MVP 简化：若 AI 殖民地系统尚未实现对占领节点建筑产出的消费逻辑，可临时忽略归属转移的 AI 侧产出（产出不计入任何人）。等 AI 系统完善后补全，不影响建筑系统接口设计。

### Interactions with Other Systems

**接口定义**

```
BuildingSystem.RequestBuild(nodeId, BuildingType) → BuildResult
  调用方：UI 层（星图节点建造面板）
  前置检查 C-1 / C-2 在此方法内执行
  返回：{ Success: bool, FailReason: string? }

BuildingSystem.GetNodeProductionDelta(nodeId) → ProductionRate
  调用方：殖民地系统（每次 tick）
  返回：{ orePerSec: float, energyPerSec: float }，合计所有 IsActive 建筑产出
  注：已含能源消耗（如矿场返回 ore=+10, energy=-2）

BuildingSystem.OnNodeOwnershipChanged(nodeId, newOwner) → void
  调用方：星图系统 / 战斗系统（节点归属变更时通知）
  MVP：无实际状态变更（建筑始终 ACTIVE）；保留接口供 AI 消费逻辑扩展

node.ShipyardTier : int（快查字段，由建筑系统维护）
  写入方：BuildingSystem（建造 Shipyard 时置 1；升级时置 2）
  读取方：飞船系统（建造飞船前置检查 RequiredShipyardTier）；舰队调度系统（战斗机停靠容量查询）
```

**事件（发布）**

```
BuildingSystem.OnBuildingConstructed(nodeId, BuildingType)
  订阅方：UI 层（建造动效 / 音效）、殖民地系统（刷新产出计算）
```

**数据流关系**

```
UI 层     → RequestBuild(nodeId, type)    → 建筑系统
建筑系统   → CanAfford(cost)              → 资源系统（前置检查 C-2）
建筑系统   → GetBuildCost(type)           → 资源系统（步骤 1 扣款）
殖民地系统 → GetNodeProductionDelta(id)   → 建筑系统（每 tick 累加产出）
飞船系统   → node.ShipyardTier             → 建筑系统维护（只读）
星图系统   → OnNodeOwnershipChanged()     → 建筑系统（节点归属变更通知）
```

## Formulas

### 引用的上游公式（不重复定义）

| 公式 | 来源 | 建筑系统用途 |
|------|------|------------|
| `net_ore_production` | 资源系统 | `node_ore_output` 的基础项 |
| `net_energy_production` | 资源系统 | 节点能源变化量（限定 nodeId 作用域） |
| `can_afford` | 资源系统 | `is_valid_build_request` 的资源充足检查项 |
| `GetOreMultiplier` | 星图系统 | `node_ore_output` 的节点乘数项 |

---

### 公式 1：node_ore_output（本系统定义，需注册 registry）

```
node_ore_output(nodeId) = mine_count × ORE_PER_MINE × GetOreMultiplier(nodeId)
```

| 变量 | 类型 | 范围 | 来源 |
|------|------|------|------|
| `mine_count` | int | 0–∞ | 运行时（该节点 BasicMine 建筑数量） |
| `ORE_PER_MINE` | int | 固定 10 | 常量，来自资源系统 |
| `GetOreMultiplier(nodeId)` | float | 1.0–`RICH_NODE_ORE_MULTIPLIER`（默认 2.0） | 星图系统接口 |

- **输出范围**：0（无矿场）到 ∞（实际受 `ORE_CAP` 截断）
- **示例计算**：
  - STANDARD 节点 3 座矿场：`3 × 10 × 1.0 = 30 ore/sec`
  - RICH 节点 3 座矿场：`3 × 10 × 2.0 = 60 ore/sec`
  - 无矿场（任意节点）：`0 × 10 × any = 0 ore/sec`

> HOME_BASE 和 STANDARD 节点 GetOreMultiplier 返回 1.0，RICH 类型返回 `RICH_NODE_ORE_MULTIPLIER`（默认 2.0）。

---

### 公式 2：node_energy_delta（引用资源系统，限定 nodeId 作用域）

```
node_energy_delta(nodeId) ≡ net_energy_production
  = COLONY_BASE_ENERGY + (mine_count × ENERGY_PER_MINE) + (shipyard_count × ENERGY_PER_SHIPYARD)
```

与资源系统 `net_energy_production` 表达式完全相同，变量均取自 `nodeId` 对应节点内的建筑数量。能源无节点类型乘数。此公式不在 registry 新增条目，仅在 `net_energy_production.referenced_by` 加入 `building-system.md`。

---

### 规格 3：is_valid_build_request（内部门控，不注册 registry）

```
is_valid_build_request(nodeId, buildingType) =
    (node.ownershipState == PLAYER)
    AND can_afford(ore_current, energy_current, ore_cost(buildingType), energy_cost(buildingType))
```

| 变量 | 类型 | 来源 |
|------|------|------|
| `node.ownershipState` | enum | 星图系统（只读） |
| `ore_current` | int | 殖民地系统（运行时全局存量） |
| `energy_current` | int | 殖民地系统（运行时全局存量） |
| `ore_cost(buildingType)` | int | `ResourceConfig.GetBuildCost()` |
| `energy_cost(buildingType)` | int | `ResourceConfig.GetBuildCost()` |

- **输出范围**：boolean
- **短路求值**：`ownershipState` 检查先于 `can_afford`；ENEMY/NEUTRAL 节点直接返回 false，不检查资源
- **示例**：
  - ENEMY 节点，资源充足 → `false`（ownershipState 未满足）
  - PLAYER 节点，ore_current=30 < MINE_ORE_COST=50 → `false`（can_afford=false）
  - PLAYER 节点，ore_current=60，energy_current=25 → `true`（矿场建造满足）

---

### 规格 4：GetNodeProductionDelta（对外接口规格，不注册 registry）

```
GetNodeProductionDelta(nodeId) → { orePerSec, energyPerSec }
  orePerSec    = node_ore_output(nodeId)      [公式 1]
  energyPerSec = node_energy_delta(nodeId)    [引用资源系统公式 2]
```

- **调用方**：殖民地系统（每次固定 1 秒 tick 对所有 PLAYER 节点累加）
- `energyPerSec` 可为负数（矿场 -2、船坞 -3）

## Edge Cases

### EC-BUILD-01：扣费成功但实例创建失败

**场景**：步骤 1（扣费）完成后，步骤 2（创建 BuildingInstance）因任何原因（InstanceId 冲突、内存分配失败）失败。

**规则**：
- 必须立即回滚全部扣除的资源（金额与扣除时完全一致）
- `node.ShipyardTier` 不更新
- `OnBuildingConstructed` 事件不发出
- 向日志写入 Error 级记录；UI 显示"建造失败，资源已退还"

---

### EC-BUILD-02：能源存量为负时发起建造

**场景**：玩家当前净能源产出为负（消耗 > 产出），但 MVP 阶段能源系统仅警告不惩罚。玩家点击建造。

**规则**：
- 能源余额为负时，建造操作**照常执行**，不额外拦截
- 前置检查 C-2 仅检查建造所需的**一次性费用**是否能负担，不检查能源产出是否为正
- **MVP 临时约定**：若能源系统升级为"不足即惩罚"模式，须同步修改 C-2 检查逻辑

---

### EC-BUILD-03：同一节点同帧并发建造

**场景**：玩家在同一帧内连续发出两次对同一节点的建造指令（如快速双击）。

**规则**：
- `BuildingManager` 内部以串行队列处理所有建造请求，同一节点请求不得并发执行
- 第一条指令处理完成（成功或失败）后，才开始处理第二条指令
- 每条指令独立执行完整的 C-1、C-2 检查；第一次建造消耗资源后第二次 C-2 不通过，则第二次建造静默丢弃（不回滚，未扣费）
- UI 层负责建造按钮防抖，但建筑系统不依赖 UI 防抖作为唯一保障

---

### EC-BUILD-04：建造完成后同帧节点沦陷

**场景**：`OnBuildingConstructed` 事件发出后，同一帧内节点归属变为 ENEMY。

**规则**：
- `ShipyardTier` 已在步骤 3b 中写入，归属转移由节点沦陷流程统一处理
- 节点沦陷时，`ShipyardTier` 保持原值，归属方标记从 PLAYER 切换为 ENEMY
- 敌方从**下一个完整产出周期（tick）**开始享有该建筑产出，不按帧内比例折算
- 玩家不获得任何退款（与飞船系统"建造中沦陷不退款"原则一致）
- 帧内事件处理顺序由技术架构保证（设计意图：归属切换优先于产出计算）

---

### EC-BUILD-05：收复节点后在敌方建筑基础上叠建

**场景**：节点被敌方占领期间，敌方在节点上建造了建筑。玩家收复后尝试再建造。

**规则**：
- 收复后，节点上所有既存建筑（含敌方建造的）归属统一切换为 PLAYER，无需重建
- 玩家可在收复节点上叠加建造新建筑（每节点同类无上限规则适用）
- 收复瞬间，所有既存建筑立即开始为玩家产出，不存在冷却期
- 建筑的原始建造方信息不持久化：收复后敌方建的建筑与玩家建的建筑功能完全等同
- 玩家无法拆除任何建筑（建筑不可移除，与建造方无关）

---

### EC-BUILD-06：节点类型变更时产出生效时机

**场景**：星图系统将节点从 STANDARD 升级为 RICH（或降级），该节点上已有矿场建筑。

**规则**：
- 产出变化**在下一个完整产出周期（tick）开始时生效**，不按当前周期剩余时间折算
- 产出计算在每次 tick 时实时调用 `GetOreMultiplier(nodeId)`，读取当前节点类型（建筑系统是产出公式权威方）
- 降级（RICH→STANDARD）适用完全相同规则（下 tick 生效）

## Dependencies

### 上游依赖（本系统消费的接口/数据）

**资源系统** (`design/gdd/resource-system.md`)

- `ResourceConfig.GetBuildCost(BuildingType)` — 建造费用查询（前置检查 C-2 + 步骤 1 扣费）
- `ResourceConfig.GetProductionRate(BuildingType)` — 产出速率查询（公式 1/2 的基础数据）
- `can_afford(ore, energy, ore_cost, energy_cost)` — 前置检查 C-2 直接引用
- `RefundResources(cost)` — EC-BUILD-01 回滚用
- 常量引用：`ORE_PER_MINE=10`、`ENERGY_PER_MINE=-2`、`ENERGY_PER_SHIPYARD=-3`、`COLONY_BASE_ENERGY=5`、`MINE_ORE_COST=50`、`MINE_ENERGY_COST=20`、`SHIPYARD_ORE_COST=80`、`SHIPYARD_ENERGY_COST=30`

**星图系统** (`design/gdd/star-map-system.md`)

- `StarNode.ownershipState` — 前置检查 C-1 使用（只读）
- `GetOreMultiplier(nodeId)` — 公式 1 `node_ore_output` 中的节点类型乘数
- `OnNodeOwnershipChanged(nodeId, newOwner)` 事件 — 节点归属变更通知，触发建筑系统归属转移处理
- `StarNode.Buildings : List<BuildingInstance>` — 建筑列表附加在星图节点数据上（⚠️ 星图系统 GDD 需新增此字段）
- `StarNode.ShipyardTier : int` — 快查字段（0/1/2），建筑系统维护（写），其他系统读

### 下游依赖（依赖本系统的其他系统）

**殖民地系统**（待设计，`design/gdd/colony-system.md`）

- 消费 `BuildingSystem.GetNodeProductionDelta(nodeId)` — 每 tick 叠加所有 PLAYER 节点矿石/能源产出
- 订阅 `BuildingSystem.OnBuildingConstructed` — 刷新产出计算

**飞船系统** (`design/gdd/ship-system.md`)

- 读取 `StarNode.ShipyardTier` — 飞船建造前置检查（`generic_v1` 需 Tier≥1，`carrier_v1` 需 Tier≥2）
- ⚠️ 飞船 GDD 规则 B-1 已更新为 `ShipyardTier >= RequiredShipyardTier` 检查

**星图 UI**（待设计）

- 读取建筑列表 — 节点信息面板显示建筑图标
- 调用 `BuildingSystem.RequestBuild(nodeId, type)` — 建造按钮触发入口

### 双向依赖说明

- **资源系统**：建造成本和产出速率常量由资源系统定义；建筑系统是最主要的消费方。新增资源类型时两系统均需同步变更。
- **殖民地系统**：`GetNodeProductionDelta` 接口是殖民地系统产出计算的必要前提；殖民地系统无法在建筑系统设计完成前确定其产出计算结构。

## Tuning Knobs

| 参数名 | 当前值 | 单位 | 安全调参范围 | 影响的游戏体验 |
|--------|--------|------|------------|--------------|
| `ORE_PER_MINE` | 10 | 矿石/秒 | 5–20 | 经济节奏快慢；值越高积累越快，决策压力越低 |
| `ENERGY_PER_MINE` | -2 | 能源/秒 | -5 到 -1 | 矿场的能源"税"；绝对值越大，能源管理越紧张 |
| `ENERGY_PER_SHIPYARD` | -3 | 能源/秒 | -8 到 -1 | 船坞的能源"税"；绝对值越大，军事扩张越消耗能源 |
| `MINE_ORE_COST` | 50 | 矿石 | 20–100 | 建矿场的门槛；越低则铺矿越容易，越高则前期决策更谨慎 |
| `MINE_ENERGY_COST` | 20 | 能源 | 0–50 | 矿场的一次性能源投入 |
| `SHIPYARD_ORE_COST` | 80 | 矿石 | 40–150 | 军事能力的经济门槛；越高则"先经济后军事"策略越被强化 |
| `SHIPYARD_ENERGY_COST` | 30 | 能源 | 10–60 | 船坞的一次性能源投入 |
| `SHIPYARD_UPGRADE_ORE_COST` | 80 | 矿石 | 40–200 | 船坞升级为 Tier 2 的矿石费用；越高则航母准入门槛越高 |
| `SHIPYARD_UPGRADE_ENERGY_COST` | 40 | 能源 | 20–80 | 船坞升级的能源费用 |
| `ENERGY_PER_SHIPYARD_TIER2_EXTRA` | -2 | 能源/秒 | -5 到 -1 | Tier 2 升级额外增加的能源维持消耗（叠加在 Tier 1 的 -3 之上）|
| `RICH_NODE_ORE_MULTIPLIER` | 2.0 | 倍数 | 1.2–4.0 | RICH 节点战略价值；越高则争夺动机越强（来自星图系统，建筑产出公式引用） |

**调参注意事项**：

- **回本周期**：`MINE_ORE_COST / ORE_PER_MINE = 5 秒`。调整任一参数时需评估回本周期是否在合理范围（建议 3–15 秒）。
- **能源平衡点**：默认 1 矿场 + 1 船坞 = `5 - 2 - 3 = 0`（恰好平衡殖民地基础能源）。调参时建议保持 `COLONY_BASE_ENERGY >= |ENERGY_PER_MINE| + |ENERGY_PER_SHIPYARD|`，否则默认混合节点能源为负，管理压力大幅提升。
- 以上常量均定义于资源系统 GDD，变更须同步更新 `design/registry/entities.yaml`。

## Visual/Audio Requirements

> **范畴声明**：建筑系统是数据-逻辑层。本章节定义触发源和规格约束；视觉渲染实现归属星图 UI GDD，音频实现归属音频系统 GDD。

### 建筑图标规格

| 建筑 | 符号形状 | 尺寸 | 节点位置 |
|------|---------|------|---------|
| BasicMine | 向下钻头箭头 ▼，下方横线（「开采」通用符号） | 16dp × 16dp | 节点**右下角**偏移 |
| Shipyard | 简化飞船侧视剪影外加方括号框 `[▶]`（「生产线」语义） | 18dp × 16dp | 节点**左下角**偏移 |

- 图标线条粗细 ≥ 2dp，保证小尺寸可见
- 颜色随节点归属：玩家节点 `#4FC3F7`，敌方节点 `#FF5722`
- 图标不得遮挡节点中心圆点或舰队图标

**多矿场叠加显示**（BasicMine 可建多座）：

| 数量 | 显示 |
|------|------|
| 0 | 无图标 |
| 1 | 矿场图标（无徽章） |
| 2–9 | 矿场图标 + 右上角数字徽章（Bold，10dp） |
| ≥ 10 | 矿场图标 + `9+` 徽章 |

徽章样式：白字，深色圆角矩形背景（`#1A1F2E`，85% 透明度）

**船坞显示**：有/无二态（无徽章），每节点至多 1 座

**RICH 节点矿场区分**：图标加金色外光晕（`#FFD54F`，Glow Radius 3dp，Opacity 70%）+ 右上角金色菱形点标 ◆（4dp），形状不变，降低认知负担

**触控热区**：节点含建筑区域整体触控热区 ≥ 48dp × 48dp（Android 最小目标）；建筑图标不设独立热区，点击节点整体触发详情面板

### 建造视觉反馈

**建造成功动效**（事件源：`OnBuildingConstructed`）

BasicMine（0.5 秒，两段式）：
1. Pop-in：图标从 Scale 0 弹出，Ease-Out Back（轻微过冲回弹），0.25 秒
2. Ring Pulse：节点处向外扩散圆环（线框），颜色 `#4FC3F7`，0.4 秒内 Scale 1.0 → 2.5，Opacity 100% → 0%

Shipyard（0.6 秒，三段式）：
1. Pop-in：同 BasicMine
2. Ring Pulse：同 BasicMine，Emission 强度 ×1.3
3. Flash：节点图标短暂白色闪光（2 帧），传达「解锁」感，Shipyard 专有

性能约束：粒子 ≤ 8 个/事件；复用节点 UI Shader；不阻塞游戏逻辑

**建造失败反馈**（事件源：`RequestBuild()` 返回 FailReason）：
- 节点图标水平抖动 3 次（Amplitude 4dp，Duration 0.3 秒）
- Toast 提示浮现，1.5 秒后消失（文案由 UI 系统定义）
- 无粒子特效

### 归属转移视觉

**节点沦陷（PLAYER → ENEMY）**：图标颜色 0.3 秒线性渐变 `#4FC3F7` → `#FF5722`；无额外粒子

**节点收复（ENEMY → PLAYER）**：图标颜色 0.3 秒线性渐变 `#FF5722` → `#4FC3F7`；叠加一次 Ring Pulse（`#4FC3F7`，0.4 秒）——收复需比沦陷更强的视觉正向强化

### 音频规格

| SFX 事件 ID | 触发时机 | 时长 | 音效描述 |
|------------|---------|------|---------|
| `SFX_BUILD_MINE_SUCCESS` | `OnBuildingConstructed(_, BasicMine)` | 0.4–0.6 秒 | 金属锁扣声 + 低频机械启动共鸣；调性：工业、踏实；无音调上扬 |
| `SFX_BUILD_SHIPYARD_SUCCESS` | `OnBuildingConstructed(_, Shipyard)` | 0.5–0.8 秒 | 能量充能声上扬「嗡——」+ 清脆金属敲击「叮」；调性：科技感、有仪式感 |
| `SFX_BUILD_DENIED_RESOURCES` | `RequestBuild()` → `FailReason: RESOURCES` | 0.2–0.3 秒 | 短促低频「嗡」（拒绝音，无音调上扬）；与节点抖动同步 |
| `SFX_BUILD_DENIED_OWNERSHIP` | `RequestBuild()` → `FailReason: OWNERSHIP` | 0.2–0.3 秒 | MVP 复用 `SFX_BUILD_DENIED_RESOURCES` |

混音规范：分类 `SFX_UI_BUILD`；0 dB 参考响度；不启用 3D 空间化（星图层为 2D UI）；优先级：中

> 节点归属转移的音效（沦陷/收复）由星图 UI/战斗系统统一触发一次，不在建筑层为每座建筑单独触发（多建筑节点会导致音效堆叠）。

## UI Requirements

**建造操作入口**

- 玩家点击星图上任意节点 → 弹出节点详情面板（Node Detail Panel）
- 面板内显示：节点名称、归属方、当前建筑列表、可建造建筑列表
- 可建造建筑的按钮：每种建筑一个按钮，显示图标 + 名称 + 建造费用（矿石/能源）
- 若 C-2 不通过（资源不足）：按钮显示为 Disabled 状态（变暗，费用数字变红）
- 若 C-1 不通过（节点非 PLAYER）：建造按钮区整体隐藏

**建造费用显示**

- 费用格式：`[矿石图标] 50  [能源图标] 20`
- 资源不足时对应数字变红，并显示缺口（如 `-20矿石`）
- 建造后能源将变负时，按钮下方显示警告：「⚠ 建造后能源产出将为负」（对应 EC-BUILD-02）

**建筑数量显示**

节点详情面板内显示当前建筑列表：
- `基础矿场 ×N`（N 为数量）
- `船坞 Tier 1`（存在时）/ `无船坞`（不存在时）
- `船坞 Tier 2 ★`（升级后，加以区分标记）
- ShipyardTier≥1 时，面板额外显示「飞船建造已解锁」标识；Tier=2 时，额外显示「航母建造已解锁」标识

**节点产出摘要**

面板底部显示产出预览：
- `当前产出：+X 矿石/秒，Y 能源/秒`（Y 为负时用红色显示）
- 选中待建建筑时，预览建造后变化：`→ +X' 矿石/秒，Y' 能源/秒`

**触控要求**

- 所有建造按钮触控热区 ≥ 48dp × 48dp
- 建造按钮防抖 200ms（配合 BuildingManager 内部串行队列）
- Disabled 状态按钮仍响应触控（触发 `SFX_BUILD_DENIED_RESOURCES` + Toast 提示），不是无响应
- 禁止仅依赖 hover 状态的交互（Android 触屏，无 hover）

> 节点详情面板的具体 UX 设计（布局、动画、导航流程）归属星图 UI GDD。本章节仅定义建筑系统需要 UI 层支持的数据显示和交互要求。

## Acceptance Criteria

**AC-BLDG-01：成功建造 BasicMine**
操作：节点 N-01（PLAYER），矿石=100、能源=50，执行"建造 BasicMine"
期望：矿石→50，能源→30；节点 mineCount +1；下一个 tick（≤1.1 秒内）矿石产出增加 `10 × GetOreMultiplier(N-01)`，能源消耗增加 2/秒
→ **通过**：扣费精确，mineCount 正确递增，下 tick 产出符合公式

---

**AC-BLDG-02：成功建造 Shipyard（Tier 1）并设置 ShipyardTier**
操作：节点 N-02（PLAYER，ShipyardTier=0），矿石=150、能源=80，执行"建造 Shipyard"
期望：矿石→70，能源→50；`node.ShipyardTier == 1`（同帧生效）；下一个 tick 能源消耗增加 3/秒
→ **通过**：扣费精确，ShipyardTier=1，tick 能耗正确

---

**AC-BLDG-03：C-1 检查——非己方节点拒绝建造**
操作：节点 N-03（ENEMY 或 NEUTRAL），资源充足（矿石=500），执行"建造 BasicMine"
期望：建造请求被拒绝，返回"节点不属于玩家"；矿石仍=500；mineCount 未变
→ **通过**：操作被阻止，零扣费，零状态变化

---

**AC-BLDG-04：C-2 检查——资源不足拒绝建造**
操作 A：节点 PLAYER，矿石=49（<50）、能源充足 → 建造 BasicMine
操作 B：节点 PLAYER，矿石充足、能源=19（<20）→ 建造 BasicMine
期望：两次均被拒绝，返回"资源不足"；玩家资源不变；mineCount 未增加
→ **通过**：矿石或能源任一不足均触发拒绝，零副作用

---

**AC-BLDG-05：产出公式验证（多矿节点）**
操作：节点 N-05（GetOreMultiplier=1.5），建造 2 座 BasicMine，等待 3 个完整 tick
期望：`node_ore_output = 2 × 10 × 1.5 = 30 矿石/tick`；3 tick 总产出 90 矿石（允许 ±1 浮点误差）；能源消耗 `2 × 2 = 4/tick`
→ **通过**：3 tick 后矿石 +90 ±1，能源 -12 ±1

---

**AC-BLDG-06：节点沦陷后产出归敌方**
操作：节点 N-06（PLAYER），建有 1 座 BasicMine；将节点切换为 ENEMY；等待 2 个完整 tick
期望：玩家矿石在 2 tick 内无增加；建筑保留（mineCount=1）；敌方每 tick 获得 `10 × GetOreMultiplier(N-06)` 矿石
→ **通过**：玩家无收入，敌方有收入，建筑数据未清空

---

**AC-BLDG-07：收复节点后产出恢复给玩家**
操作：沿用 AC-BLDG-06 的节点（ENEMY，mineCount=1）；切换回 PLAYER；等待 2 个完整 tick
期望：切换后第 1 个 tick 起玩家每 tick 获得 `10 × GetOreMultiplier(N-06)` 矿石；原有建筑数据完整保留；无需重建
→ **通过**：2 tick 后玩家矿石按公式增加，建筑状态与沦陷前一致

---

**AC-BLDG-08：扣费成功但实例创建失败时回滚（EC-BUILD-01）**
操作：注入故障（BuildingFactory 创建时返回 null）；矿石=100、能源=50；节点 PLAYER；执行"建造 BasicMine"
期望：玩家矿石恢复 100，能源恢复 50（零净扣费）；mineCount 未变；日志含 Error 记录
→ **通过**：资源完整回滚，节点状态未变，错误日志可查

---

**AC-BLDG-09：建造后能源变负照常放行（EC-BUILD-02）**
操作：节点 PLAYER；矿石=60、能源=5；执行"建造 BasicMine"（费用：50矿石+20能源；C-2 通过因能源 5 < 20 → 实为不通过）

> **注**：EC-BUILD-02 的场景是"建造后能源产出变负"（净产出 < 0），而非"能源存量不足以支付建造费用"。C-2 检查两种资源的一次性建造费用，若能源存量不足则正常拒绝。

操作（修正）：节点 PLAYER；矿石=100、能源=25；净能源产出=-5（已处于负值）；执行"建造 BasicMine"（费用 50矿石+20能源，能源 25 ≥ 20，C-2 通过）
期望：建造成功（矿石→50，能源→5）；下一个 tick 产出正常计入；UI 显示能源产出警告（不阻止操作）
→ **通过**：建造成功，资源正确扣除，能源产出为负不阻断操作

---

**AC-BLDG-10：并发建造请求串行化（EC-BUILD-03）**
操作：矿石=60、能源=25（仅够 1 座 BasicMine）；同一帧内对节点 N-09 发起 2 次"建造 BasicMine"
期望：第 1 次成功（矿石→10，能源→5）；第 2 次因资源不足被拒绝；mineCount=1（非 2）；无双重扣费
→ **通过**：mineCount=1，资源仅扣一次，无竞态异常日志

---

**AC-BLDG-11：建造后同帧节点沦陷，下 tick 产出归敌方（EC-BUILD-04）**
操作：同一帧内执行"建造 BasicMine" + 触发节点沦陷（ENEMY）；等待 1 个完整 tick
期望：mineCount 保留（建造已生效）；第 1 个 tick 起产出归敌方；玩家无该节点矿石收入；无退款
→ **通过**：tick 后玩家无新增矿石，敌方获得产出，mineCount 保留

---

**AC-BLDG-12：收复后直接在既有建筑基础上叠建（EC-BUILD-05）**
操作：节点 N-11（收复，mineCount=1，ShipyardTier=0）；资源充足；立即执行"建造第 2 座 BasicMine"
期望：mineCount→2；下 tick 产出按 `2 × 10 × GetOreMultiplier(N-11)` 计算；无需任何重新初始化步骤
→ **通过**：mineCount=2，扣费正确，下 tick 产出翻倍

**AC-BLDG-13：船坞升级 Tier 1 → Tier 2**
操作：节点 N-12（PLAYER，ShipyardTier=1），矿石=200、能源=100，执行"升级船坞"
期望：矿石→120，能源→60；`node.ShipyardTier == 2`（同帧生效）；下一个 tick 能源额外消耗增加 2/秒（总 Shipyard 能耗 = -5/秒）
→ **通过**：扣费精确，ShipyardTier=2，tick 能耗正确

**AC-BLDG-14：无 Tier 1 船坞时拒绝升级**
操作：节点 N-13（PLAYER，ShipyardTier=0），资源充足，执行"升级船坞"
期望：建造请求被拒绝，返回"前置条件不满足（需先建基础船坞）"；资源不变；ShipyardTier 仍为 0
→ **通过**：操作被阻止，零扣费，零状态变化

**AC-BLDG-15：Tier 2 船坞节点沦陷后 ShipyardTier 保留**
操作：节点 N-14（ShipyardTier=2，PLAYER）；触发节点沦陷（ENEMY）
期望：`node.ShipyardTier` 保持 2（不重置为 0）；敌方可读取 ShipyardTier 查询能力
→ **通过**：ShipyardTier 沦陷后保留，不被清零

## Open Questions

**Q-1：每节点建筑总数上限（Vertical Slice 设计决策）**
当前 MVP：同类型无上限（资源唯一约束）。Vertical Slice 阶段是否引入每节点总建筑数量上限 N（如 N=3），强制"专职节点"策略（矿产节点 vs 军事节点）？需要原型数据验证"无限堆矿"是否在实际游戏中形成 Dominant Strategy。

**Q-2：建筑是否可以移除（Vertical Slice 功能）**
当前 MVP：建筑不可移除。玩家是否希望在后期有拆除能力（支付一定费用/资源损失）？拆除引入新战略深度，但增加系统复杂度。待 MVP 原型验证后决定。

**Q-3：多种建筑类型扩展（Vertical Slice 范围）**
MVP 仅 2 种建筑。Vertical Slice 可能引入：防御炮台、研究中心等。建议在实现建筑系统前确认扩展计划，避免 BuildingType 枚举穷举导致代码破坏性修改（可改用 ScriptableObject 列表替代枚举）。

**Q-4：AI 对占领节点建筑产出的消费逻辑**
MVP 简化：AI 占领含建筑节点后，产出可能不计入 AI 经济（接口已定义，等待 AI 殖民地系统设计时对接）。完整语义需要 AI 系统消费 `GetNodeProductionDelta(nodeId)` 并按归属方合并计算。

**Q-5：✅ 已解决 — ShipyardTier 整数字段已正式启用（2026-04-14）**
`HasShipyard : bool` 已由 `ShipyardTier : int`（0/1/2）取代。Tier 1 解锁 `generic_v1`，Tier 2 解锁 `carrier_v1` 及航母搭载容量（3架战斗机）。升级通过 `ShipyardUpgrade` 建筑类型就地触发，不新增建筑实例。

**Q-6：战斗机独立建造时的造船厂前置条件（新增）**
战斗机（`fighter_v1`）需要在航母停靠的节点建造（需 ShipyardTier≥1 还是仅需有航母停靠？），还是在任意己方节点均可？建议在飞船系统 GDD 中明确 `fighter_v1` 的 `RequiredShipyardTier` 值。
