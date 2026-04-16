# 星图系统 (Star Map System)

> **Status**: In Design
> **Author**: game-designer (Claude Code) + user
> **Last Updated**: 2026-04-12
> **Implements Pillar**: 支柱2 一目了然的指挥 / 支柱3 我的星际帝国 / 支柱4 从星图到驾驶舱

## Overview

星图系统是《星链霸权》战略层的数据骨架，它维护一张由**星域节点（StarNode）**和**可领路径（Edge）**构成的无向图。每个 StarNode 代表一个星域——它可以是尚未开发的空旷星域、玩家的殖民地，或敌对势力的据点。连边定义了哪些节点相邻；只有相邻的节点之间才能直接派遣舰队。系统本身不运行任何物理或视觉逻辑——它是纯数据层，为殖民地系统（持有每个节点的资源状态）、舰队调度系统（读取相邻关系规划路线）和星图 UI（将图渲染为玩家看到的星图界面）提供数据来源。对于玩家而言，星图系统就是"整个帝国"的俯视地图：所有占领行动、所有建设决策，都在这张图的节点上发生。

## Player Fantasy

星图对玩家来说不是一张抽象的拓扑图——它是**你正在建造的帝国的全貌**。

**核心情感：缔造者的骄傲 + 猎手的算计快感**

每次打开星图，玩家的第一感受是看见自己的成果：冷蓝色光点从一颗孤星蔓延成三颗、五颗、一片星团——每一个亮起的节点都是一次经济决策和一次军事行动的结晶。这不是随机奖励，这是你亲手建立的版图。

然后玩家的视线移向边境：灰暗的未探索区域，敌对节点的橙红色闪烁，自己舰队的蓝色行军线。星图变成了一张态势图——哪条路是瓶颈，哪个节点是必争之地，哪支舰队可以抢先到位。玩家在图上布好局，然后做出最能体现游戏独特性的那个选择：**跳进旗舰驾驶舱，亲自去那个截击点**。

**核心情感承诺**：
- **归属感**：「这片星图上每一个亮着的节点都是我打下来的」
- **算计快感**：「我在对手行动之前就已经在等他了」
- **自由度峰值**：「我在星图上布好了局，然后跳进去亲自执行」

**支柱对齐**：
- 支柱 3（我的星际帝国）：缔造感和归属感是帝国持续成长的情感核心
- 支柱 1（经济即军事）：版图上的每个亮点都是经济 → 军事 → 扩张循环的可视化
- 支柱 4（从星图到驾驶舱）：截击布局 → 驾驶舱执行，是四大支柱同时共振的标志性时刻
- 支柱 2（一目了然）：以上所有情感都依赖星图信息一眼可读——看不清就没有缔造感也没有算计感

## Detailed Design

### Core Rules

**1. 数据模型**

星图系统维护一张**无向图 G = (V, E)**：
- **V（节点集）**：所有星域节点（StarNode），每个节点是图中的一个顶点
- **E（边集）**：节点间的可领路径（Edge），无权重，无方向

每个 StarNode 包含以下属性：

| 属性 | 类型 | 说明 |
|------|------|------|
| `id` | string | 唯一标识符（如 `"sigma-7"`） |
| `displayName` | string | 显示名称（如 `"西格玛-7"`） |
| `position` | Vector2 | 星图上的 UI 坐标（dp），用于渲染 |
| `nodeType` | enum | `HOME_BASE` / `STANDARD` / `RICH` |
| `ownershipState` | enum | `NEUTRAL` / `PLAYER` / `ENEMY` |
| `fogState` | enum | `UNEXPLORED` / `EXPLORED` / `VISIBLE` |
| `dockedFleet` | Fleet? | 当前停靠在本节点的舰队（nullable） |

每条 Edge 只记录两个节点 ID 的无序对，无额外属性（MVP）。

---

**2. 节点类型规则**

| 节点类型 | 世界含义 | 游戏效果 |
|---------|---------|---------|
| `HOME_BASE` | 玩家/AI 的起始星域 | 矿石产量乘数 = 1.0；失去则触发游戏失败判定（`HOME_BASE_LOSS_CONDITION = true`） |
| `STANDARD` | 普通可开发星域 | 矿石产量乘数 = 1.0 |
| `RICH` | 稀有资源星域 | 矿石产量乘数 = `RICH_NODE_ORE_MULTIPLIER`（默认 2.0）；关键争夺目标 |

> **注**：产量乘数应用于殖民地系统的 `net_ore_production`，星图系统通过接口暴露 `GetOreMultiplier(nodeId)` 供殖民地系统查询。

---

**3. 占领规则**

- **规则 C-1（中立占领）**：玩家舰队到达 `NEUTRAL` 节点时，节点立即切换为 `PLAYER`，无等待时间。
- **规则 C-2（战斗占领）**：玩家舰队到达 `ENEMY` 节点时，触发飞船战斗系统处理战斗；战斗系统通知结果后，星图系统更新状态（胜利→`PLAYER`，失败→状态不变）。
- **规则 C-3（视角不阻塞）**：玩家切换到驾驶舱视角期间，星图层的移动和占领逻辑**实时运行，不暂停**。

---

**4. 舰队移动规则**

- **规则 M-1（逐跳移动）**：舰队每次只能移动到相邻节点（图中直接连边），不可跨节点跳跃。
- **规则 M-2（路径约束）**：目标节点必须已达 `EXPLORED` 或 `VISIBLE` 状态才能被选为派遣目标（`UNEXPLORED` 节点不可派遣）。
- **规则 M-3（单舰队 MVP）**：MVP 阶段每个节点驻扎唯一舰队单位（1 艘飞船），简化调度逻辑。
- **规则 M-4（在途状态）**：舰队在路上时（`IN_TRANSIT`）不属于任何节点；经过 `FLEET_TRAVEL_TIME` 秒后到达目标节点并触发占领检查。
- **规则 M-5（取消移动）**：玩家取消途中舰队的移动命令时，舰队原路返回出发节点，花费相同时间。

---

**5. 战争迷雾规则**

节点探索状态（`fogState`）的三值状态机：

| 状态 | 显示内容 | 可操作性 |
|------|---------|---------|
| `UNEXPLORED` | 暗淡坐标点，无名称，无类型 | 不可点击，不可作为派遣目标 |
| `EXPLORED` | 节点轮廓 + 名称 + 类型，显示**上次已知的** ownershipState | 可查看，相邻时可作为派遣目标，信息可能过时 |
| `VISIBLE` | 完整实时信息（类型、占领状态、驻军、资源） | 全部操作可用 |

**初始状态**：
- 玩家 `HOME_BASE`：`VISIBLE`
- HOME_BASE 的所有直接相邻节点：`EXPLORED`
- 其余节点：`UNEXPLORED`

**可见性来源**：玩家占领的节点 + 停靠在节点的舰队，各提供以下视野：
- 本节点：`VISIBLE`
- 所有直接相邻节点：`VISIBLE`

**降级规则**：失去节点（被 AI 夺取）且无其他视野源覆盖时，该节点降级为 `EXPLORED`（保留上次已知状态，永不回退到 `UNEXPLORED`）。

---

**6. 玩家操作约束（明确禁止）**

- ❌ 不可跨越非相邻节点直接派遣舰队
- ❌ 不可将 `UNEXPLORED` 节点作为派遣目标
- ❌ 不可在 `ENEMY` 或 `NEUTRAL` 节点建造建筑
- ❌ 不可拆除已建成的建筑（MVP）
- ❌ 同一节点不可同时建造多个建筑（建造队列长度 = 1）

---

**7. MVP 星图布局（静态配置）**

MVP 使用固定的钻石型 5 节点布局：

```
      [S] HOME_BASE（玩家起始）
      / \
    [A] [B]   ← A = STANDARD / B = RICH（岔路选择）
      \ /
      [C] RICH（瓶颈节点，图论割点）
       |
      [E] HOME_BASE（AI 起始）
```

**策略价值**：速攻路（S→A→C→E）vs 经济路（S→B→C→E，先抢稀有资源但让出 C 的时间窗口）。

### States and Transitions

**节点占领状态机（ownershipState）：**

| 当前状态 | 触发条件 | 新状态 |
|----------|----------|--------|
| `NEUTRAL` | 玩家舰队到达 | `PLAYER` |
| `NEUTRAL` | AI 舰队到达 | `ENEMY` |
| `PLAYER` | AI 战斗胜利 | `ENEMY` |
| `ENEMY` | 玩家战斗胜利 | `PLAYER` |
| `ENEMY` | 玩家战斗失败 | `ENEMY`（不变） |

**节点探索状态机（fogState）：**

| 当前状态 | 触发条件 | 新状态 |
|----------|----------|--------|
| `UNEXPLORED` | 玩家占领相邻节点（产生视野覆盖） | `EXPLORED` |
| `UNEXPLORED` | 玩家舰队到达本节点 | `VISIBLE` |
| `EXPLORED` | 玩家获得覆盖本节点的视野 | `VISIBLE` |
| `VISIBLE` | 玩家失去所有覆盖本节点的视野源 | `EXPLORED` |
| `EXPLORED` | 任意条件 | 不可降级回 `UNEXPLORED` |

**舰队状态机（Fleet.state）：**

| 当前状态 | 触发条件 | 新状态 |
|----------|----------|--------|
| `DOCKED` | 玩家下达移动指令（目标为相邻节点） | `IN_TRANSIT` |
| `IN_TRANSIT` | 经过 `FLEET_TRAVEL_TIME` 秒 | `DOCKED`（目标节点） |
| `IN_TRANSIT` | 玩家取消移动命令 | `DOCKED`（返回出发节点） |
| `DOCKED` | 所在节点被 AI 战斗胜利夺取 | `DESTROYED` |

### Interactions with Other Systems

**星图系统对外暴露的接口（只读规则查询）：**

```csharp
// 查询两节点是否相邻
StarMapData.AreAdjacent(nodeId_A, nodeId_B) → bool

// 获取节点的所有相邻节点 ID 列表
StarMapData.GetNeighbors(nodeId) → List<string>

// 查询节点占领状态
StarMapData.GetOwnership(nodeId) → OwnershipState

// 查询节点探索状态
StarMapData.GetFogState(nodeId) → FogState

// 查询节点类型的矿石产量乘数
StarMapData.GetOreMultiplier(nodeId) → float

// 通知：战斗结果（由飞船战斗系统调用）
StarMapData.OnCombatResult(nodeId, bool playerWon) → void

// 通知：舰队到达（内部调用，触发占领检查）
StarMapData.OnFleetArrived(fleetId, nodeId) → void
```

**下游系统依赖关系：**

| 系统 | 依赖类型 | 接口 | 说明 |
|------|---------|------|------|
| 殖民地系统 | 硬依赖 | `GetOreMultiplier()` | 读取节点类型的资源加成 |
| 舰队调度系统 | 硬依赖 | `AreAdjacent()`、`GetNeighbors()`、`GetOwnership()`、`GetFogState()` | 路径验证和派遣条件检查 |
| 星图 UI | 硬依赖 | 所有只读接口 | 渲染节点状态、连边、视野 |
| 飞船战斗系统 | 软依赖（单向通知） | `OnCombatResult()` | 战斗结束后通知星图更新占领状态 |
| 程序星图生成系统 | 硬依赖（写） | StarMapData 数据结构 | Vertical Slice 阶段负责生成符合数据模型的节点/边集合 |
| 存档/读档系统 | 硬依赖 | 所有节点属性序列化 | Vertical Slice 阶段 |

## Formulas

### F-STAR-01：节点可见性规则（IsVisible）

The IsVisible formula is defined as:

`IsVisible(n) = ∃p ∈ P : GraphDistance(p, n) ≤ VISION_RADIUS`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|----------|--------|------|-------|-------------|
| 目标节点 | `n` | StarNode | — | 待判断可见性的节点 |
| 玩家占领集合 | `P` | Set\<StarNode\> | \|P\| ≥ 0 | 所有 ownershipState = PLAYER 的节点集合 |
| 图距离 | `GraphDistance(p, n)` | int | [0, ∞) | 无向图中两节点间最短跳数（不考虑权重） |
| 视野半径 | `VISION_RADIUS` | int | [1, 3]（默认：1） | 玩家占领节点所能照亮的最大跳数 |

**输出范围**：bool；true = 该节点应设置为 VISIBLE

**FogState 配套规则**（由本公式驱动）：
```
若 IsVisible(n) = true                              → fogState = VISIBLE
若 IsVisible(n) = false 且 曾经被设为 VISIBLE 过    → fogState = EXPLORED
若 IsVisible(n) 从未为 true                         → fogState = UNEXPLORED
```

**Example：** VISION_RADIUS = 1，玩家占领节点 A，A 与 B、C 相邻，B 与 D 相邻：
- A → `VISIBLE`（GraphDistance = 0）
- B、C → `VISIBLE`（GraphDistance = 1，等于 VISION_RADIUS）
- D → `UNEXPLORED`（GraphDistance = 2，超出半径，且从未 VISIBLE 过）

---

### F-STAR-02：节点资源产量乘数（GetOreMultiplier）

The GetOreMultiplier formula is defined as:

```
GetOreMultiplier(nodeType) =
  RICH_NODE_ORE_MULTIPLIER   if nodeType = RICH
  1.0                         otherwise (HOME_BASE or STANDARD)
```

**Variables:**
| Variable | Symbol | Type | Range | Description |
|----------|--------|------|-------|-------------|
| 节点类型 | `nodeType` | Enum | HOME\_BASE \| STANDARD \| RICH | 查询目标的节点类型 |
| 富矿乘数 | `RICH_NODE_ORE_MULTIPLIER` | float | (1.0, 5.0]（默认：2.0） | RICH 节点的矿石产量放大系数 |

**输出范围**：float，取值为 1.0（STANDARD/HOME_BASE）或 `RICH_NODE_ORE_MULTIPLIER`（RICH）

**与资源系统的衔接**（引用，不重新定义）：
```
// 资源系统已定义（此处仅引用）：
net_ore_production = mine_count × ORE_PER_MINE

// 殖民地系统在调用矿石产量时，应用本系统的乘数：
adjusted_ore_production = net_ore_production × GetOreMultiplier(nodeType)
```

**Example 1（STANDARD 节点）：** 2 座矿场，ORE_PER_MINE = 10 → `20 × 1.0 = 20 ore/sec`

**Example 2（RICH 节点）：** 2 座矿场，ORE_PER_MINE = 10，RICH_NODE_ORE_MULTIPLIER = 2.0 → `20 × 2.0 = 40 ore/sec`

## Edge Cases

### 图结构边界

- **If P 为空集（无玩家占领节点）**：`IsVisible(n)` 对所有 n 返回 false；fogState 维持当前值不变；系统等待 HOME_BASE_LOSS_CONDITION 触发游戏结束判定，不主动修改任何状态。
- **If 某节点无连边（孤立节点）**：停靠舰队无法派遣，UI 不显示可选目标；系统不崩溃；MVP 静态图不应出现孤立节点，启动时须进行图结构验证。
- **If 图连通性分裂（两个联通分量）**：`GraphDistance` 对跨分量节点返回 ∞；`IsVisible` 对跨分量节点返回 false；舰队无法跨分量派遣，派遣验证直接拒绝。
- **If 舰队 IN_TRANSIT 途中，目标节点（含割点 C）被 AI 夺取**：到达后按规则 C-2 触发战斗；到达事件不因目标节点所有权变化取消或中断。

### 同步 / 竞态条件

- **If 玩家舰队和 AI 舰队同帧到达同一 NEUTRAL 节点**：玩家优先占领（节点变 PLAYER），AI 舰队随即触发规则 C-2 战斗。
- **If 玩家取消移动命令与舰队到达目标在同帧发生**：到达事件优先；取消命令丢弃，舰队 DOCKED 于目标节点。
- **If 同一节点已有战斗进行中，AI 发起第二次进攻**：节点标记为 COMBAT_LOCKED；第二次进攻排队等待当前战斗结束后再触发。
- **If 视野重算与节点所有权写入在同帧发生**：先写入 ownershipState，再运行视野计算；保证视野结果基于最新所有权，不出现「已失去节点仍提供视野」的瞬态帧。
- **If 建造指令写入与节点在同帧被 AI 夺取**：节点夺取优先执行；建造成本退还，建造队列清空，UI 提示「建造中断：节点已失守」。

### 舰队状态非法转换

- **If 玩家对 IN_TRANSIT 舰队下达第二次移动指令**：拒绝；只接受「取消当前移动」操作；必须 DOCKED 后才能接受新移动指令。
- **If DOCKED 舰队被 AI 战斗胜利夺取节点**：舰队 → DESTROYED（MVP 终态，无复活机制）；游戏失败判定仅由 HOME_BASE ownershipState 变化触发，与舰队存活状态无关。
- **If 返航中的 IN_TRANSIT 舰队，其出发节点（返航目标）被 AI 夺取**：舰队继续返航，到达后触发规则 C-2 战斗；返航路径不因目标节点所有权变化中断。
- **If 玩家在驾驶舱期间，其某支舰队 IN_TRANSIT 到达 ENEMY 节点**：触发无人值守战斗；战斗解算规则待飞船战斗系统 GDD 定义（见 Open Questions Q-1）。

### 视野计算边界

- **If VISION_RADIUS = 0（极端调参）**：仅被占领节点本身为 VISIBLE，所有邻居保持 EXPLORED/UNEXPLORED；派遣逻辑依赖 fogState 不依赖 VISION_RADIUS，不崩溃。Tuning Knobs 标注 safe_min = 1。
- **If 节点被多个视野源同时覆盖，其中一个视野源失去**：只要剩余任一视野源满足 `IsVisible(n) = true`，节点保持 VISIBLE（视野覆盖为 OR 逻辑，单源失去不降级）。
- **If EXPLORED 节点的上次已知 ownershipState 与当前实际状态不一致**：UI 继续显示上次已知状态；玩家须重新获得视野覆盖才能看到真实状态（战争迷雾设计意图，过时信息是合法状态）。
- **If HOME_BASE 无相邻节点（数据配置错误）**：HOME_BASE 自身 VISIBLE（GraphDistance = 0），无 EXPLORED 邻居；系统不崩溃，但启动时关卡验证应捕获并报错。

### 游戏结束条件

- **If 玩家 HOME_BASE 被 AI 夺取（无论玩家是否在驾驶舱）**：立即触发游戏失败流程；驾驶舱战斗强制中断，不允许「打完这场再说」。
- **If 玩家正在驾驶的飞船被摧毁**：战斗以失败结算，玩家强制返回星图；若该节点为 HOME_BASE，触发游戏失败；若为普通节点，玩家返回星图，舰队进入 DESTROYED 状态。
- **If 玩家占领 AI 的 HOME_BASE 与玩家 HOME_BASE 失守在同帧发生**：玩家胜利优先处理，不触发失败流程。
- **If AI 唯一舰队被摧毁但 AI 仍持有 HOME_BASE**：游戏继续；AI 失败仅由 AI HOME_BASE 的 ownershipState 变为 PLAYER 触发，与舰队存活状态无关。

### 驾驶舱视角切换期间

- **If 玩家在驾驶舱时 AI 占领 NEUTRAL 节点或夺取 PLAYER 节点（非 HOME_BASE）**：星图实时更新 ownershipState，视野实时重算；玩家返回星图时看到已完成的结果，无回放，无补偿。
- **If 建造队列中的建筑在驾驶舱期间完成**：建筑效果立即生效，殖民地系统实时更新产量；玩家返回星图时 UI 直接反映已建成状态，无需额外确认。
- **If 玩家试图在驾驶舱内下达星图操作（派遣/建造）**：操作拒绝；驾驶舱和星图是互斥操作视角，必须主动退出驾驶舱才能操作星图。

## Dependencies

**上游依赖（星图系统依赖的系统）**
- 无。星图系统是 Foundation 层，无上游依赖。

**下游依赖（依赖星图系统的系统）**

| 系统 | 依赖类型 | 接口 | 依赖方向 |
|------|---------|------|---------|
| 殖民地系统 | 硬依赖 | `GetOreMultiplier(nodeId)`、`GetOwnership(nodeId)` | 星图 → 殖民地 |
| 舰队调度系统 | 硬依赖 | `AreAdjacent()`、`GetNeighbors()`、`GetOwnership()`、`GetFogState()` | 星图 → 舰队调度 |
| 星图 UI | 硬依赖 | 所有只读接口（完整节点/边数据） | 星图 → 星图 UI |
| 飞船战斗系统 | 软依赖（单向通知） | `OnCombatResult(nodeId, playerWon)` | 飞船战斗 → 星图（通知） |
| 程序星图生成系统 | 硬依赖（写） | StarMapData 数据结构 | 生成器 → 星图（初始化写入） |
| 存档/读档系统 | 硬依赖（序列化） | 所有 StarNode 属性 + Edge 集合 | 存档 ↔ 星图（双向） |
| 建筑系统 | 硬依赖 | `GetOreMultiplier(nodeId)`、`ownershipState`、`OnNodeOwnershipChanged`、`StarNode.Buildings`、`HasShipyard(nodeId)` | 建筑 → 星图（读取节点类型资源加成、归属状态、建筑列表及造船厂条件） |

**间接依赖**
- **资源系统**：星图系统的 `GetOreMultiplier()` 将乘数应用于资源系统公式的输出 `net_ore_production`；殖民地系统负责组合两者，星图不直接调用资源系统。

**双向一致性声明**
- 殖民地系统 GDD 应列出「依赖星图系统（`GetOreMultiplier`、`GetOwnership`）」
- 舰队调度系统 GDD 应列出「依赖星图系统（邻接查询、视野查询）」
- 星图 UI GDD 应列出「依赖星图系统（所有只读接口）」
- 飞船战斗系统 GDD 应列出「需向星图系统发送 `OnCombatResult` 通知」
- 程序星图生成系统 GDD 应列出「向星图系统写入初始图数据」

## Tuning Knobs

| 旋钮名 | 默认值 | 安全范围 | 影响 | 交互关系 |
|--------|--------|---------|------|---------|
| `VISION_RADIUS` | 1 | 1–3（safe_min = 1） | 玩家占领节点能照亮的最大跳数；过低→玩家几乎无视野，过高→战争迷雾失去意义 | 与图密度交互：稀疏图中 VISION_RADIUS=1 即可看到大多数节点 |
| `RICH_NODE_ORE_MULTIPLIER` | 2.0 | 1.2–4.0 | RICH 节点的矿石产量放大系数；过低→RICH 节点失去争夺价值，过高→占领 RICH 节点后经济碾压 | 与 `ORE_PER_MINE`（资源系统）交互：两者共同决定 RICH 节点的实际产出 |
| `FLEET_TRAVEL_TIME` | 3.0 秒 | 1.0–30.0 秒 | 舰队跨一条边的飞行时间；过低→扩张太快，星图缺乏战略等待感，过高→玩家等待太久，碎片化游玩体验变差 | 与图的节点数量交互：小图应设置较短时间以保持节奏感 |
| `HOME_BASE_LOSS_CONDITION` | true | true / false | 是否开启大本营失守即游戏失败；false 时玩家可在无大本营状态下继续（测试用） | — |

**关键平衡关系**：
- `VISION_RADIUS = 1` 时，玩家始终能看到所有相邻可扩张节点，是「一目了然的指挥」支柱的最低保障
- `FLEET_TRAVEL_TIME = 3.0` 在 MVP 5 节点图上，从 HOME_BASE 到 AI 的 HOME_BASE 需要 3 跳 × 3 秒 = 约 9 秒最短战略路程，给玩家足够时间做决策

## Visual/Audio Requirements

星图系统是纯数据层，不直接负责任何渲染或音频逻辑。视觉与音频需求由下游系统承担：

- **节点/连边渲染**、**战争迷雾遮罩**、**占领状态色彩编码**、**舰队在途动画** → 见「星图 UI GDD」
- **占领音效**、**舰队移动音效**、**战斗触发音效** → 见「音频系统 GDD」

本系统只负责暴露状态（`ownershipState`、`fogState`、舰队位置），由 UI 层订阅并驱动视觉表现。

## UI Requirements

星图系统的 UI 需求将在「星图 UI GDD」中完整定义。本节记录来自本系统设计的 UI 约束，供 UI GDD 作者参考：

- 玩家必须能一眼区分节点的 `ownershipState`（NEUTRAL / PLAYER / ENEMY）和 `fogState`（UNEXPLORED / EXPLORED / VISIBLE）——需在「一目了然的指挥」支柱下提供明确的视觉编码
- `UNEXPLORED` 节点在 UI 上不可点击，不可作为派遣目标
- `IN_TRANSIT` 舰队必须在星图上有明确的移动进度表现（玩家需要知道舰队还有多久到达）
- 驾驶舱视角与星图视角互斥；切换时 UI 需要明确的状态转换反馈
- 触屏操作：所有交互目标（节点、舰队、路径）的触控热区必须满足 Android 最小触控目标尺寸（≥ 48dp）

> **UX Flag**：本系统涉及实质性 UI 设计决策（派遣目标选择流程、战争迷雾视觉层级、驾驶舱切换动画），建议在「星图 UI GDD」中专项设计，并通过 `/ux-design` 技能走完整 UX 设计流程。

## Acceptance Criteria

共 18 条可测试 AC，全部可对应自动化测试用例，无主观判断项。

> **测试分类说明**：AC-STAR-03、AC-STAR-07、AC-STAR-08 需要帧循环，归入 **PlayMode 集成测试**；其余均可作为 **EditMode 单元测试**实现。

---

```
AC-STAR-01：NEUTRAL 节点立即占领
Given: 节点 [C] 状态为 NEUTRAL，玩家舰队当前位于节点 [A]
When: 玩家舰队完成从 [A] 到 [C] 的移动（经过 FLEET_TRAVEL_TIME 秒后到达）
Then: GetOwnership([C]) 返回 PLAYER，且状态切换发生在同一帧到达事件触发时，无延迟
Pass/Fail: GetOwnership([C]) == PLAYER → PASS；返回任何其他值 → FAIL
```

```
AC-STAR-02：到达 ENEMY 节点触发战斗而非立即占领
Given: 节点 [E] 状态为 ENEMY（AI HOME_BASE），玩家舰队当前位于节点 [C]
When: 玩家舰队完成从 [C] 到 [E] 的移动并到达
Then: 战斗系统收到 CombatRequested([E]) 事件；GetOwnership([E]) 不变，仍为 ENEMY，
      直到战斗系统回调通知结果
Pass/Fail: 到达 [E] 后 GetOwnership([E]) == ENEMY 且 CombatRequested 事件已发出 → PASS；
           到达后直接切换为 PLAYER 或未发出事件 → FAIL
```

```
AC-STAR-03：驾驶舱期间星图逻辑不暂停（PlayMode）
Given: 玩家舰队正在从 [A] 到 [C] 途中（剩余 FLEET_TRAVEL_TIME 的 50% 时间），
       玩家进入驾驶舱模式
When: 驾驶舱模式保持激活状态，经过剩余 50% 的 FLEET_TRAVEL_TIME
Then: 舰队正常到达 [C]，GetOwnership([C]) 更新为 PLAYER（若 [C] 为 NEUTRAL），
      不因驾驶舱激活而延迟或暂停
Pass/Fail: 驾驶舱期间舰队按时到达且节点归属正确更新 → PASS；
           到达时间延长或归属更新延迟 → FAIL
```

```
AC-STAR-04：舰队不可跨节点移动（逐跳强制）
Given: MVP 拓扑中，节点 [S] 与 [C] 不直接相邻（路径为 S→A→C 或 S→B→C）
When: 玩家尝试将舰队从 [S] 直接派遣至 [C]
Then: 系统拒绝该指令，舰队不移动；返回错误或无效操作提示
Pass/Fail: 舰队保持在 [S] 不动，且未出现在 [C] → PASS；
           舰队直接出现在 [C] 跳过中间节点 → FAIL
```

```
AC-STAR-05：UNEXPLORED 节点不可作为派遣目标
Given: 节点 [E]（AI HOME_BASE）对玩家的 FogState 为 UNEXPLORED
When: 玩家尝试将舰队派遣至 [E]
Then: 系统拒绝该指令，舰队不移动；[E] 不出现在可选目标列表中
Pass/Fail: 派遣指令被拒绝且舰队不移动 → PASS；
           舰队开始移动向 [E] → FAIL
```

```
AC-STAR-06：单节点最多驻扎 1 支舰队（MVP）
Given: 玩家舰队 Fleet-1 已驻扎在节点 [A]，玩家还有第二支舰队 Fleet-2 在节点 [S]
When: 玩家尝试将 Fleet-2 派遣至 [A]
Then: 系统拒绝该指令；Fleet-2 保持在 [S]；[A] 只有 Fleet-1 驻扎
Pass/Fail: Fleet-2 不出现在 [A]，且 [A] 仍只驻扎 Fleet-1 → PASS；
           [A] 出现两支舰队 → FAIL
```

```
AC-STAR-07：舰队在途时不属于任何节点（PlayMode）
Given: 玩家舰队从节点 [A] 出发前往节点 [C]，出发后经过 0 < t < FLEET_TRAVEL_TIME 的时间
When: 查询该舰队当前所在节点
Then: 舰队位置状态为 IN_TRANSIT，不归属于 [A] 也不归属于 [C]；
      [A] 和 [C] 的驻扎舰队查询均不包含该舰队
Pass/Fail: 在途期间舰队所在节点返回 IN_TRANSIT 或 null → PASS；
           返回 [A] 或 [C] → FAIL
```

```
AC-STAR-08：取消移动原路返回且花费相同时间（PlayMode）
Given: 玩家舰队从 [A] 出发前往 [C]，在经过 T_elapsed（0 < T_elapsed < FLEET_TRAVEL_TIME）后取消
When: 玩家发出取消移动指令
Then: 舰队改为返回 [A]，返回所需时间 = T_elapsed（已行驶时间），
      抵达 [A] 后归属节点 [A] 的驻扎舰队列表恢复包含该舰队
Pass/Fail: 舰队在 T_elapsed 后抵达 [A] 且驻扎确认 → PASS；
           返回时间不等于 T_elapsed，或舰队未返回 [A] → FAIL
```

```
AC-STAR-09：初始迷雾状态——HOME_BASE VISIBLE，直接邻居 EXPLORED，其余 UNEXPLORED
Given: 游戏刚开始，玩家 HOME_BASE = [S]，MVP 拓扑：[S] 邻居为 [A][B]，其余为 [C][E]
When: 查询各节点的 GetFogState()
Then: GetFogState([S]) == VISIBLE
      GetFogState([A]) == EXPLORED
      GetFogState([B]) == EXPLORED
      GetFogState([C]) == UNEXPLORED
      GetFogState([E]) == UNEXPLORED
Pass/Fail: 全部 5 项均符合 → PASS；任意一项不符 → FAIL
```

```
AC-STAR-10：占领节点后本节点及所有直接邻居变为 VISIBLE（F-STAR-01 核心验证）
Given: 玩家占领节点 [C]（VISION_RADIUS=1），[C] 的直接邻居为 [A]、[B]、[E]
When: GetOwnership([C]) == PLAYER，调用 IsVisible(n) 或 GetFogState(n)
Then: GetFogState([C]) == VISIBLE
      GetFogState([A]) == VISIBLE
      GetFogState([B]) == VISIBLE
      GetFogState([E]) == VISIBLE
Pass/Fail: 全部 4 项均为 VISIBLE → PASS；任意一项不为 VISIBLE → FAIL
```

```
AC-STAR-11：F-STAR-01 距离超过 VISION_RADIUS 的节点不可见
Given: 玩家只占领节点 [S]（HOME_BASE），VISION_RADIUS=1，
       [C] 与 [S] 的 GraphDistance = 2
When: 调用 GetFogState([C])
Then: GetFogState([C]) != VISIBLE（为 UNEXPLORED 或 EXPLORED）
Pass/Fail: GetFogState([C]) 不为 VISIBLE → PASS；返回 VISIBLE → FAIL
```

```
AC-STAR-12：失去节点后视野降为 EXPLORED，永不回退到 UNEXPLORED
Given: 玩家曾占领 [C]，[E] 因此变为 VISIBLE；之后玩家失去 [C]，
       且无其他节点与 [E] 距离 ≤ 1
When: GetOwnership([C]) 变为 ENEMY 或 NEUTRAL，调用 GetFogState([E])
Then: GetFogState([E]) == EXPLORED（不得回退为 UNEXPLORED）
Pass/Fail: GetFogState([E]) == EXPLORED → PASS；
           返回 UNEXPLORED → FAIL（严重回退 bug）
```

```
AC-STAR-13：F-STAR-02 RICH 节点返回正确矿石倍率
Given: 节点 [B] 和 [C] 类型均为 RICH，RICH_NODE_ORE_MULTIPLIER = 2.0
When: 调用 GetOreMultiplier([B]) 和 GetOreMultiplier([C])
Then: GetOreMultiplier([B]) == 2.0（误差 < 0.001）
      GetOreMultiplier([C]) == 2.0（误差 < 0.001）
Pass/Fail: 两项均符合 → PASS；任意一项不为 2.0 → FAIL
```

```
AC-STAR-14：F-STAR-02 STANDARD 和 HOME_BASE 节点返回 1.0 倍率
Given: 节点 [A] 类型为 STANDARD，节点 [S][E] 类型为 HOME_BASE
When: 调用 GetOreMultiplier([A])、GetOreMultiplier([S])、GetOreMultiplier([E])
Then: 三项均返回 1.0（误差 < 0.001）
Pass/Fail: 全部 3 项符合 → PASS；任意一项不为 1.0 → FAIL
```

```
AC-STAR-15：玩家 HOME_BASE 被夺取 → 立即游戏失败
Given: 玩家 HOME_BASE = [S]，GetOwnership([S]) == PLAYER；AI 舰队赢得战斗
When: OnCombatResult([S], playerWon=false) 被调用，GetOwnership([S]) 设置为 ENEMY
Then: 游戏结束事件 GameOver(result=DEFEAT) 在同一帧或当帧结束前被触发；
      玩家无法继续执行星图或驾驶舱操作
Pass/Fail: GameOver(DEFEAT) 事件触发 → PASS；游戏继续运行 → FAIL
```

```
AC-STAR-16：AI HOME_BASE 被玩家占领 → 玩家胜利
Given: AI HOME_BASE = [E]，GetOwnership([E]) == ENEMY；玩家舰队赢得战斗
When: OnCombatResult([E], playerWon=true) 被调用，GetOwnership([E]) 设置为 PLAYER
Then: 游戏结束事件 GameOver(result=VICTORY) 在同一帧或当帧结束前被触发
Pass/Fail: GameOver(VICTORY) 事件触发 → PASS；游戏继续运行 → FAIL
```

```
AC-STAR-17：接口契约——AreAdjacent 和 GetNeighbors 返回正确拓扑值
Given: MVP 地图拓扑已初始化
When: 调用拓扑查询接口
Then: AreAdjacent([S],[A]) == true
      AreAdjacent([S],[B]) == true
      AreAdjacent([A],[C]) == true
      AreAdjacent([B],[C]) == true
      AreAdjacent([C],[E]) == true
      AreAdjacent([S],[C]) == false（非直接相邻）
      AreAdjacent([S],[E]) == false（非直接相邻）
      GetNeighbors([S]) == {A, B}（不多不少）
      GetNeighbors([C]) == {A, B, E}（不多不少）
Pass/Fail: 全部 9 项均符合 → PASS；任意一项不符 → FAIL
```

```
AC-STAR-18：接口契约——只读接口不修改状态
Given: 游戏运行中，节点 [A] 归属为 PLAYER，FogState 为 VISIBLE
When: 连续调用 GetOwnership([A]) 和 GetFogState([A]) 各 100 次
Then: 每次调用返回值一致（PLAYER / VISIBLE）；
      调用后节点实际状态不发生任何变化
Pass/Fail: 100 次调用结果全部一致且节点状态未被修改 → PASS；
           任意一次返回值不同或状态被意外修改 → FAIL
```

---

| 规则 / 公式 | AC 编号 |
|---|---|
| C-1 占领 NEUTRAL | AC-STAR-01 |
| C-2 到达 ENEMY 触发战斗 | AC-STAR-02 |
| C-3 驾驶舱期间不暂停 | AC-STAR-03 |
| M-1 逐跳移动 | AC-STAR-04 |
| M-2 UNEXPLORED 不可派遣 | AC-STAR-05 |
| M-3 单节点最多 1 舰队 | AC-STAR-06 |
| M-4 在途不属于任何节点 | AC-STAR-07 |
| M-5 取消原路返回 | AC-STAR-08 |
| 迷雾初始状态 | AC-STAR-09 |
| F-STAR-01 IsVisible（占领后扩视野） | AC-STAR-10 |
| F-STAR-01 IsVisible（距离超出不可见） | AC-STAR-11 |
| 迷雾降级 EXPLORED 永不回退 | AC-STAR-12 |
| F-STAR-02 RICH 节点倍率 | AC-STAR-13 |
| F-STAR-02 STANDARD/HOME_BASE 倍率 | AC-STAR-14 |
| 游戏失败条件 | AC-STAR-15 |
| 游戏胜利条件 | AC-STAR-16 |
| 接口契约（拓扑查询） | AC-STAR-17 |
| 接口契约（只读不修改） | AC-STAR-18 |

## Open Questions

**Q-1（阻塞飞船战斗系统 GDD）：无人值守战斗如何解算？**

场景：玩家正在驾驶舱操作某艘飞船进行战斗，与此同时其 **另一支** IN_TRANSIT 舰队到达 ENEMY 节点，触发战斗（规则 C-2）。此时玩家无法同时操控两场战斗。

- 选项 A：无人值守战斗自动解算（AI 控制玩家舰队），结果立即通知星图
- 选项 B：无人值守战斗暂时挂起，玩家退出驾驶舱后弹出战斗界面
- 选项 C：玩家舰队在无人值守时自动撤退（返回出发节点），不触发战斗

**决策方**：飞船战斗系统 GDD 作者。本系统需要在飞船战斗系统 GDD 完成后更新 Edge Cases 中的 EC-COCKPIT-04 条目。

---

**Q-2（后续考虑，非 MVP 阻塞）：MVP 后是否引入 VISION_RADIUS > 1 的探索飞船？**

目前视野唯一来源是占领节点（VISION_RADIUS=1）。如果引入专用侦察舰，视野计算需要将舰队位置也加入视野源集合 P。此扩展不影响 MVP 公式，但应在进入 Vertical Slice 前确认是否列入设计范围。
