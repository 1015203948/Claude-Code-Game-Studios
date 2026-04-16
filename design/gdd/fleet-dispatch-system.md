# 舰队调度系统 (Fleet Dispatch System)

> **Status**: Designed (Pending Review)
> **Author**: game-designer (Claude Code) + user
> **Last Updated**: 2026-04-12
> **Implements Pillar**: 支柱1 经济即军事 / 支柱2 一目了然的指挥 / 支柱3 我的星际帝国

## Overview

舰队调度系统（Fleet Dispatch System）是《星链霸权》星图层玩家意志的执行机构：它接收玩家对飞船的移动指令，按照星图系统的路径约束驱动飞船逐跳在节点间移动，并在到达目的地时触发占领或战斗。从数据层看，它管理所有处于 IN_TRANSIT 状态的飞船实例——维护每艘飞船当前所在边、剩余行驶时间和目标节点，并在到达时通过 `ShipSystem.SetState()` 和 `StarMapSystem.OnFleetArrived()` 完成状态交接。从玩家感受看，它是「把帝国意志变成领土扩张」的一次点击：你在星图上看到一个可触达的节点，你点它，飞船出发，你回去做其他事，飞船自己到达。这个循环的核心是**零摩擦的操控**和**可信赖的执行**——玩家不需要盯着飞船飞，只需要看着颜色在地图上扩散。MVP 阶段每个节点最多驻扎一艘飞船，飞船逐跳移动（`FLEET_TRAVEL_TIME` = 3.0秒/跳），取消移动时原路返回。

## Player Fantasy

舰队调度的玩家幻想是**帝国意志的落地执行**——你不是在移动一个游戏单位，你是在把经济计算变成领土现实。

**情感弧线**

它从你手指点下目标节点的那一刻开始。你已经算过了：那个 RICH 节点的矿石产出值得你派这艘船，抵达后的产能跃升足以覆盖空窗期的损失。你点下去，蓝色行军线亮起——这不是一次尝试，这是一次**执行**。落子无悔。

然后是行军过程中的无声倒计时。飞船每跳一格，你的决策就离兑现更近一步。你转身去安排其他殖民地的建筑，但你脑子里有一条线——「那里到了吗？」「那里有敌人吗？」「到达之后我的布局就闭合了。」这 3 秒、9 秒、12 秒不是等待时间，它们是你的指挥官身份的一部分：你下达命令，然后你信任你自己的判断。

最后是缩放星图的那一刻。节点变蓝，你退出全局视角——帝国的蓝色集群又多了一个外延点。那不是一条数据更新，那是你用一次点击**画上去的笔触**。你的帝国正在呼吸，每一次舰队出发都是它向外扩张的脉搏。你已经在想下一笔画在哪里了。

**三个锚点时刻**

- *出发前*：你在星图上盯着那个灰色节点算了十秒，矿石产出、行驶跳数、当前存量三个数字在脑子里碰撞。答案出来了，手指落下。蓝色行军线亮起。你不再盯着它了。
- *行军中*：星图角落的光点悄悄跳了一格，再一格。你在安排另一个殖民地的建筑，但你的一部分注意力在那条路径上。到了没有？——到了。
- *到达后*：退回全局视角，帝国的边界又向外推了一格。那个节点颜色的改变是你经济计算和执行决策的物理证明——不是程序自动分配的结果，是你选的、你算的、你派的。

**支柱对齐**
- **支柱1（经济即军事）**：舰队调度是经济积累（我造了船）到军事效果（我拿下了节点）之间的转化器——每一次出发都是对「这笔投资值不值」的答卷
- **支柱2（一目了然的指挥）**：一次点击出发，一次点击取消，行军过程自动执行——操控零摩擦，让玩家专注于决策而非执行细节
- **支柱3（我的星际帝国）**：被占领的节点不会归还，帝国边界只扩不退——每一次调度都是帝国历史的永久添砖

## Detailed Design

### Core Rules

#### D-1 派遣前置条件

玩家只能派遣满足以下全部条件的飞船：

| 条件 | 说明 |
|------|------|
| 飞船当前状态为 `DOCKED` | IN_TRANSIT、IN_COCKPIT、DESTROYED 状态的飞船不可派遣 |
| 飞船所在节点归属 `PLAYER_OWNED` | 只能从自己的节点出发 |
| 目标节点已被 `EXPLORED` 或 `VISIBLE` | 未探索节点不可直接指派 |
| 目标节点无其他己方飞船正在 `DOCKED` | 遵守单节点最多 1 艘飞船规则（M-3） |
| 从出发节点到目标节点存在可用路径 | BFS 无法找到路径时派遣被拒绝（见 D-3） |

> **注**：目标节点当前可以是 NEUTRAL 或 ENEMY，也可以是 PLAYER_OWNED 的中间节点——只要满足上述条件均可作为终点。

#### D-2 操作流程（两步派遣）

```
Step 1：玩家点击星图上己方飞船图标
         → 飞船高亮，显示所有可达节点的路径预览（蓝色虚线）

Step 2：玩家点击目标节点
         → 弹出确认卡（显示：目的地名称、跳数、预计到达时间）
         → 玩家点击确认 → 创建 DispatchOrder，飞船状态切换为 IN_TRANSIT
         → 玩家点击取消 → 确认卡消失，飞船仍 DOCKED
```

确认卡不计入游戏时间（时钟继续运行）；确认卡弹出期间不暂停星图。

#### D-3 路径计算（BFS）

路径由系统在玩家确认时一次性计算并锁定：

- **算法**：广度优先搜索（BFS），找到最短跳数路径
- **可通行节点**：`EXPLORED` 或 `VISIBLE` 状态的节点（`UNEXPLORED` 节点不可通行）
- **阻断规则**：中间节点若有己方飞船正在 `DOCKED`，该节点被视为阻断，BFS 绕路（目标节点除外）
- **平局打破**：若存在多条等长路径，取节点 ID 字典序最小的路径（保证行为确定性，便于测试）
- **路径快照**：派遣确认后，路径被快照为有序节点列表，锁定在 `DispatchOrder.LockedPath` 中；此后星图拓扑变化不影响在途路径

> **路径无法找到时**：系统拒绝派遣，向玩家显示提示「无法抵达该节点」（见 UI Requirements）。

#### D-4 DispatchOrder 数据结构

系统为每次派遣维护一个 `DispatchOrder` 实例：

| 字段 | 类型 | 说明 |
|------|------|------|
| `ShipId` | string | 被派遣飞船的唯一标识 |
| `OriginNodeId` | string | 出发节点 ID |
| `DestNodeId` | string | 最终目标节点 ID |
| `LockedPath` | List\<nodeId\> | BFS 计算后锁定的完整路径（含出发节点和目标节点） |
| `CurrentHopIdx` | int | 当前正在行驶的跳的索引（0 = 第 1 跳） |
| `HopProgress` | float | 当前跳内累计行驶时间（秒），范围 [0, FLEET_TRAVEL_TIME] |
| `IsCancelled` | bool | 是否已触发取消，为 true 时飞船沿反向路径返回 |

#### D-5 逐跳推进逻辑

每帧在 `Update()` 中对所有活跃 `DispatchOrder` 执行以下逻辑：

```
HopProgress += Time.deltaTime

if HopProgress ≥ FLEET_TRAVEL_TIME:
    HopProgress -= FLEET_TRAVEL_TIME   // 保留余量（不丢失时间）
    CurrentHopIdx += 1（正向）或 −1（取消反向）
    执行到达逻辑（见 D-7 / D-8 / D-9）
```

`FLEET_TRAVEL_TIME` = 3.0 秒 / 跳（全局常量，来源：star-map-system.md）

#### D-6 飞船视觉位置计算

飞船的渲染位置每帧通过线性插值计算，**仅用于显示，不影响任何游戏逻辑**：

```
fromNode = LockedPath[CurrentHopIdx]
toNode   = LockedPath[CurrentHopIdx + 1]

visualPosition = lerp(fromNode.position, toNode.position,
                      HopProgress / FLEET_TRAVEL_TIME)
```

逻辑意义上，飞船在 `HopProgress < FLEET_TRAVEL_TIME` 期间仍属于 `fromNode`；到达 `toNode` 的事件在 `HopProgress ≥ FLEET_TRAVEL_TIME` 时触发。

#### D-7 到达中立节点

```
条件：到达节点归属 = NEUTRAL
结果：
  1. StarMapSystem.SetNodeOwnership(nodeId, PLAYER_OWNED)
  2. 若 nodeId == DestNodeId：
       ShipSystem.SetState(shipId, DOCKED)
       FleetDispatchSystem.CloseOrder(orderId)
  3. 若 nodeId ≠ DestNodeId：
       继续推进（CurrentHopIdx + 1，HopProgress 保留余量）
```

#### D-8 到达己方节点（中间跳）

```
条件：到达节点归属 = PLAYER_OWNED 且 nodeId ≠ DestNodeId
结果：直接继续推进（CurrentHopIdx + 1），不触发任何额外事件
```

到达终点的己方节点：

```
条件：到达节点归属 = PLAYER_OWNED 且 nodeId == DestNodeId
结果：ShipSystem.SetState(shipId, DOCKED)；CloseOrder(orderId)
```

#### D-9 到达敌方节点

```
条件：到达节点归属 = ENEMY（无论中间跳还是终点）
结果：
  1. ShipSystem.SetState(shipId, IN_COMBAT)
  2. CombatSystem.TriggerCombat(shipId, nodeId)
  3. 战斗结果回调：
     - VICTORY  → StarMapSystem.SetNodeOwnership(nodeId, PLAYER_OWNED)
                  若 nodeId == DestNodeId → ShipSystem.SetState(shipId, DOCKED)；CloseOrder
                  若 nodeId ≠ DestNodeId → 飞船停留 DOCKED，**不自动继续路径**（玩家须手动重新派遣）
     - DEFEAT   → ShipSystem.DestroyShip(shipId)；CloseOrder(orderId)
```

> **设计决策**：战胜中间节点的敌人后不自动续航——玩家需要重新评估局势并主动派遣。此决策降低了实现复杂度，并给玩家一个自然的战略评估节点。

#### D-10 取消派遣

玩家可以在飞船处于 `IN_TRANSIT` 时取消当前派遣：

```
触发：玩家点击 IN_TRANSIT 飞船 → 点击「取消」按钮

执行：
  1. IsCancelled = true
  2. 路径反向：飞船沿 LockedPath 原路返回
  3. HopProgress（取消后）= FLEET_TRAVEL_TIME − HopProgress（取消前）
     // 保持视觉连续性：飞船从当前位置向相反方向继续移动，不发生跳变
  4. 飞船逐跳原路退回，到达 OriginNodeId 后：
       ShipSystem.SetState(shipId, DOCKED)；CloseOrder(orderId)
```

取消中的飞船不能被重新派遣，直到它返回 `DOCKED`。

#### D-11 与飞船驾驶舱的交互约束

| 情形 | 行为 |
|------|------|
| 玩家尝试手动进入处于 `IN_TRANSIT` 飞船的驾驶舱 | 禁止，显示提示「飞船正在航行中」 |
| 玩家处于 `IN_COCKPIT` 时尝试派遣另一艘飞船 | 禁止，星图操作在驾驶舱模式下被锁定 |
| 飞船到达敌方节点触发战斗 → 系统自动进入驾驶舱 | 合法（系统触发，非玩家手动进入） |

#### MVP 排除项

| 编号 | 排除内容 |
|------|---------|
| D-EX-1 | 多舰队同时派遣路径冲突检测（MVP 每次只派 1 艘，无协同调度） |
| D-EX-2 | 航行速度差异化（所有飞船均使用 FLEET_TRAVEL_TIME = 3.0s/跳） |
| D-EX-3 | 待命指令（抵达后自动执行下一个派遣指令） |
| D-EX-4 | 星图拓扑变化的动态重路径（路径锁定后不重算） |
| D-EX-5 | 舰队编队（多艘飞船同步移动） |

### States and Transitions

#### 飞船状态与调度系统关系

调度系统不维护自己的状态机——它通过 `ShipSystem.SetState()` 驱动飞船的状态转换。

| 状态 | 调度系统行为 | 进入条件 |
|------|-------------|---------|
| `DOCKED` | 可接受新派遣指令 | 初始状态 / 到达目的地 / 取消返回 / 战斗胜利（终点） |
| `IN_TRANSIT` | 每帧推进 HopProgress | 玩家确认派遣 |
| `IN_COMBAT` | 暂停 HopProgress 推进，等待战斗结算 | 到达 ENEMY 节点 |
| `IN_COCKPIT` | 调度系统不处理此状态 | 系统触发战斗后由驾驶舱系统接管 |
| `DESTROYED` | CloseOrder，无后续 | 战斗 DEFEAT |

#### 状态转换表（合法转换）

| 触发条件 | 当前状态 | 目标状态 | 执行方 |
|---------|---------|---------|--------|
| 玩家确认派遣 | `DOCKED` | `IN_TRANSIT` | FleetDispatchSystem |
| 到达 NEUTRAL 终点 | `IN_TRANSIT` | `DOCKED` | FleetDispatchSystem |
| 到达 PLAYER_OWNED 终点 | `IN_TRANSIT` | `DOCKED` | FleetDispatchSystem |
| 到达 ENEMY 节点 | `IN_TRANSIT` | `IN_COMBAT` | FleetDispatchSystem |
| 战斗 VICTORY（终点） | `IN_COMBAT` | `DOCKED` | FleetDispatchSystem（结果回调） |
| 战斗 VICTORY（中间节点） | `IN_COMBAT` | `DOCKED`（滞留） | FleetDispatchSystem（结果回调） |
| 战斗 DEFEAT | `IN_COMBAT` | `DESTROYED` | FleetDispatchSystem（结果回调） |
| 玩家取消派遣 | `IN_TRANSIT` | `IN_TRANSIT`（反向） | FleetDispatchSystem |
| 取消返回完成 | `IN_TRANSIT`（反向） | `DOCKED` | FleetDispatchSystem |
| 系统触发驾驶舱 | `IN_COMBAT` | `IN_COCKPIT` | CombatSystem |

#### 非法转换（明确禁止）

| 尝试操作 | 当前状态 | 结果 |
|---------|---------|------|
| 玩家尝试派遣 | `IN_TRANSIT` | 拒绝，提示「飞船正在航行中」 |
| 玩家尝试派遣 | `IN_COMBAT` | 拒绝，提示「飞船正在战斗中」 |
| 玩家尝试派遣 | `IN_COCKPIT` | 拒绝（整个星图操作被锁定） |
| 玩家尝试手动进入驾驶舱 | `IN_TRANSIT` | 拒绝，提示「飞船正在航行中」 |
| 玩家尝试取消 | `IN_COMBAT` | 拒绝，提示「飞船正在战斗中」 |
| 到达节点已有己方飞船 | `IN_TRANSIT` | 不应发生（BFS 阶段已过滤）；若发生，记录错误日志，飞船原地 DOCKED |

### Interactions with Other Systems

| 调用方向 | 接口 | 时机 | 说明 |
|---------|------|------|------|
| 调度系统 → 星图系统 | `StarMapSystem.GetNode(nodeId)` | 路径计算时（BFS） | 读取节点连通性、归属状态、可通行性 |
| 调度系统 → 星图系统 | `StarMapSystem.SetNodeOwnership(nodeId, PLAYER_OWNED)` | 到达 NEUTRAL / ENEMY（胜利后）节点时 | 占领节点，触发颜色更新 |
| 调度系统 → 星图系统 | `StarMapSystem.OnFleetArrived(shipId, nodeId)` | 到达最终目的地时 | 通知星图更新 UI 状态 |
| 调度系统 → 飞船系统 | `ShipSystem.SetState(shipId, state)` | 状态转换各节点 | 转换为 IN_TRANSIT / DOCKED / IN_COMBAT / DESTROYED |
| 调度系统 → 飞船系统 | `ShipSystem.DestroyShip(shipId)` | 战斗 DEFEAT | 销毁飞船实例 |
| 调度系统 → 战斗系统 | `CombatSystem.TriggerCombat(shipId, nodeId)` | 到达 ENEMY 节点 | 触发战斗流程，注册结果回调 |
| 战斗系统 → 调度系统 | `OnCombatResult(shipId, result)` 回调 | 战斗结算后 | result = VICTORY / DEFEAT，调度系统根据结果执行 D-9 |
| 调度系统 → 驾驶舱系统 | 读取 `ShipSystem.GetState(shipId)` | 派遣前置检查 D-1 | 确认飞船未处于 IN_COCKPIT |
| 调度系统 → UI 系统 | 发布 `visualPosition`（每帧） | D-6 插值计算后 | 供星图 UI 渲染飞船图标的世界位置 |
| 调度系统 → UI 系统 | 发布 `LockedPath`（派遣确认时） | D-3 路径锁定后 | 供星图 UI 绘制行军路径预览线 |
| 游戏时钟 → 调度系统 | `Time.deltaTime` | 每帧 `Update()` | 推进 HopProgress |

## Formulas

### F-1 跳行驶时间

```
hop_travel_time = FLEET_TRAVEL_TIME

// 每帧推进：
HopProgress(t+1) = HopProgress(t) + Time.deltaTime

// 到达条件：
HopProgress ≥ FLEET_TRAVEL_TIME
```

| 变量 | 定义 | 值 |
|------|------|-----|
| `FLEET_TRAVEL_TIME` | 每跳标准行驶时间 | 3.0 秒（locked，来源：star-map-system.md） |
| `Time.deltaTime` | Unity 帧间隔时间 | 运行时动态（目标 16.6ms @ 60fps） |
| `HopProgress` | 当前跳内累计行驶时间 | [0, FLEET_TRAVEL_TIME] |

**示例**：60fps 下，一跳需要 3.0 / 0.0167 ≈ 180 帧推进完成。

---

### F-2 总行程时间估算

```
total_travel_time = hop_count × FLEET_TRAVEL_TIME
```

| 变量 | 定义 | 值域 |
|------|------|------|
| `hop_count` | 路径总跳数（LockedPath.Count − 1） | ≥ 1 |
| `FLEET_TRAVEL_TIME` | 每跳行驶时间 | 3.0 秒 |

**示例**：3 跳路径 → 总行程 = 3 × 3.0s = 9.0 秒；显示在确认卡中（「预计 9 秒后到达」）。

---

### F-3 飞船视觉位置插值

```
t = HopProgress / FLEET_TRAVEL_TIME          // 归一化进度 [0, 1]

visualPosition = lerp(fromNode.position, toNode.position, t)

其中：
  fromNode = LockedPath[CurrentHopIdx]
  toNode   = LockedPath[CurrentHopIdx + 1]
```

| 变量 | 定义 | 值域 |
|------|------|------|
| `t` | 当前跳内归一化进度 | [0.0, 1.0] |
| `fromNode.position` | 当前跳出发节点的世界坐标 | Vector3 |
| `toNode.position` | 当前跳目标节点的世界坐标 | Vector3 |
| `visualPosition` | 飞船图标渲染位置（不影响逻辑） | Vector3 |

**示例**：fromNode = (0,0,0)，toNode = (100,0,0)，HopProgress = 1.5s → t = 0.5 → visualPosition = (50,0,0)

---

### F-4 取消后 HopProgress 重置

取消派遣时，飞船从当前位置开始向反方向移动，不发生位置跳变：

```
HopProgress_after_cancel = FLEET_TRAVEL_TIME − HopProgress_before_cancel
```

| 变量 | 定义 |
|------|------|
| `HopProgress_before_cancel` | 取消时当前跳内已行驶时间 |
| `HopProgress_after_cancel` | 反向路径第一跳的初始进度（从当前视觉位置向反方向出发） |

**示例**：取消前 HopProgress = 1.0s（已走 1/3 路程）→ 取消后 HopProgress = 2.0s（从 1/3 处向反向出发，等效已走了 2/3 的反向路程）→ 视觉上飞船从当前位置直接向出发节点移动，无跳变。

---

**示例验证**（3 跳行程，中途取消）：

| 时刻 | 事件 | CurrentHopIdx | HopProgress |
|------|------|--------------|------------|
| t=0s | 派遣确认 | 0 | 0.0 |
| t=3s | 抵达节点 A | 1 | 0.0 |
| t=4.5s | 玩家取消 | 1（反向） | 3.0 − 1.5 = 1.5 |
| t=6.0s | 抵达节点 A（反向） | 0（反向） | 0.0 |
| t=9.0s | 返回出发节点，DOCKED | — | — |

## Edge Cases

**EC-1 BFS 找不到可用路径**
- **触发条件**：目标节点虽然 EXPLORED/VISIBLE，但由于中间所有路径被 UNEXPLORED 节点或 DOCKED 己方飞船封堵，BFS 无法找到连通路径
- **处理方式**：拒绝派遣，UI 显示提示「无法抵达该节点」；目标节点在星图上不进入高亮可选状态（派遣选择 UI 只显示可达节点）
- **不得发生**：不得创建 `LockedPath` 为空的 `DispatchOrder`

**EC-2 BFS 计算时目标节点已被另一艘己方飞船占据**
- **触发条件**：玩家正在确认派遣卡的瞬间（极短窗口），另一艘飞船恰好 DOCKED 到目标节点
- **处理方式**：确认派遣时重新检查前置条件 D-1（目标节点无 DOCKED 己方飞船）；若不满足，拒绝派遣，UI 显示提示「目标节点已有飞船驻扎」
- **理由**：MVP 为单线程执行，此竞争条件极低概率，防御性检查即可

**EC-3 取消发生在第一跳刚开始（HopProgress ≈ 0）**
- **触发条件**：玩家在派遣确认后的第 1 帧内立即取消
- **处理方式**：`HopProgress_after_cancel = FLEET_TRAVEL_TIME − 0 = 3.0s` → 取消后 HopProgress = 3.0s，等效反向第一跳立即完成 → 飞船直接 DOCKED 在出发节点
- **不得发生**：不得出现 HopProgress > FLEET_TRAVEL_TIME 的状态（到达条件应在取消处理之前已检查完毕）

**EC-4 取消发生在最后一跳接近终点（HopProgress ≈ FLEET_TRAVEL_TIME）**
- **触发条件**：玩家在飞船几乎到达终点时取消
- **处理方式**：`HopProgress_after_cancel = FLEET_TRAVEL_TIME − (FLEET_TRAVEL_TIME − ε) = ε`（接近 0）→ 飞船从终点附近开始反向，需要接近 FLEET_TRAVEL_TIME 时间才能回到上一个节点
- **注意**：不得将「接近终点」误触发为「已到达」——到达判定严格为 `HopProgress ≥ FLEET_TRAVEL_TIME`，精确时序由帧循环保证

**EC-5 飞船正在 IN_TRANSIT 时目标节点归属发生变化（防御性处理）**
- **触发条件**：MVP 阶段星图节点归属不会被外部力量改变（无敌方主动扩张）；此边界条件仅为防御
- **处理方式**：到达时按到达瞬间的实际节点归属状态执行对应逻辑（D-7/D-8/D-9），不在 BFS 时锁定归属期望值
- **理由**：路径快照锁定拓扑（节点连通性），但节点归属始终读取实时状态

**EC-6 战斗触发时另一艘飞船也到达敌方节点**
- **触发条件**：玩家已在驾驶舱（IN_COCKPIT），另一艘飞船同时抵达第二个 ENEMY 节点
- **处理方式**：第二艘飞船保持 IN_COMBAT 状态，战斗触发进入等待；MVP 阶段驾驶舱为单实例，同时只允许一个 IN_COCKPIT；待第一场战斗结算后再触发第二场（见 Open Questions Q-2）
- **不得发生**：不得同时存在两艘飞船处于 IN_COCKPIT

**EC-7 取消中的飞船经过中间节点**
- **触发条件**：取消后反向路径经过中间节点（例如原路径 A→B→C，取消后反向 C→B→A，经过节点 B）
- **处理方式**：中间节点 B 不触发任何占领逻辑，节点归属不因反向经过而改变；仅到达出发节点 A 时触发 DOCKED 结算
- **规则**：反向路径的占领触发只在抵达原出发节点时执行，中间节点一律跳过

**EC-8 飞船销毁（DESTROYED）时存在活跃 DispatchOrder**
- **触发条件**：战斗 DEFEAT 时飞船被销毁
- **处理方式**：战斗系统结果回调按顺序执行：① `FleetDispatchSystem.CloseOrder(orderId)` 清除订单 → ② `ShipSystem.DestroyShip(shipId)` 销毁飞船；CloseOrder 先于 DestroyShip，防止孤立订单继续执行 Update
- **不得发生**：不得存在 ShipId 已 DESTROYED 但 DispatchOrder 仍在活跃列表中的孤立订单

## Dependencies

### 上游依赖（本系统依赖这些系统的输出）

| 依赖系统 | 依赖内容 | 接口 |
|---------|---------|------|
| **星图系统** | 节点连通性（BFS 寻路） | `StarMapData.GetNeighbors(nodeId) → List<string>` |
| **星图系统** | 节点相邻验证 | `StarMapData.AreAdjacent(nodeId_A, nodeId_B) → bool` |
| **星图系统** | 节点归属状态（到达判断） | `StarMapData.GetOwnership(nodeId) → OwnershipState` |
| **星图系统** | 节点探索状态（BFS 过滤） | `StarMapData.GetFogState(nodeId) → FogState` |
| **飞船系统** | 读取飞船当前状态（派遣前置检查） | `ShipData.GetState(instanceId) → ShipState` |
| **飞船系统** | 写入飞船状态 | `ShipData.SetState(instanceId, newState) → void` |
| **飞船系统** | 销毁飞船（战斗 DEFEAT） | `ShipData.DestroyShip(instanceId) → void` |

### 下游依赖（这些系统依赖本系统的输出）

| 依赖系统 | 依赖内容 | 接口 |
|---------|---------|------|
| **飞船战斗系统** | 舰队到达 ENEMY 节点时触发战斗 | 调度系统调用 `CombatSystem.TriggerCombat(shipId, nodeId)`；接收 `OnCombatResult(shipId, result)` 回调 |
| **星图系统** | 舰队到达时通知星图更新 UI 状态 | `StarMapData.OnFleetArrived(fleetId, nodeId)` |
| **星图 UI** | 飞船每帧视觉位置（图标插值渲染） | 读取 `visualPosition`（来自 F-3） |
| **星图 UI** | 行军路径预览线 | 读取 `LockedPath`（派遣确认后） |
| **存档/读档系统** | 持久化所有活跃 `DispatchOrder` | 读取 `FleetDispatchSystem.GetAllActiveOrders()` |

### 反向引用（本系统需在这些文档中体现）

| 文档 | 需更新内容 |
|------|-----------|
| star-map-system.md | `GetNeighbors`、`GetOwnership`、`GetFogState`、`AreAdjacent`、`OnFleetArrived` 的 `referenced_by` 加入 fleet-dispatch-system.md |
| ship-system.md | `GetState`、`SetState`、`DestroyShip` 的 `referenced_by` 加入 fleet-dispatch-system.md |
| entities.yaml | `FLEET_TRAVEL_TIME` 的 `referenced_by` 加入 fleet-dispatch-system.md（已在 star-map-system.md 注册） |

## Tuning Knobs

| 常量 | 候选值 | 安全范围 | 影响的游戏性 |
|------|--------|---------|------------|
| `FLEET_TRAVEL_TIME` | 3.0 秒 | [1.0s, 8.0s] | 每跳行驶时长——直接决定扩张节奏感；值越小星图层操作越快节奏，值越大战略规划时间越长（locked in star-map-system.md，修改需同步更新） |

> **注**：MVP 阶段舰队调度系统的可调旋钮极少——核心时间参数 `FLEET_TRAVEL_TIME` 已在星图系统 GDD 中锁定。调度系统本身不引入新的待验证常量，设计决策（BFS、取消规则、占领逻辑）均为确定性逻辑。
>
> **标注 "locked in star-map-system.md" 的值**：修改时需同步更新两份文档及实体注册表。

## Visual/Audio Requirements

### 视觉需求

**V-1 飞船图标插值移动**
- 处于 `IN_TRANSIT` 状态的飞船图标在星图上沿节点连线方向平滑移动，位置由 F-3 插值公式驱动
- 图标朝向始终面向当前目标节点（使用 `Quaternion.RotateTowards` 或 `Vector2.SignedAngle` 计算）
- 取消返回时，图标平滑反向移动，无位置跳变

**V-2 行军路径预览线**
- 玩家点击飞船后，所有可达节点高亮，并从飞船当前位置到每个可达节点绘制蓝色虚线预览
- 选定目标后，路径从出发节点到目标节点以 **实线蓝色** 持续显示（`IN_TRANSIT` 期间保持可见）
- 已完成的跳（CurrentHopIdx 之前）路径线变暗（降低不透明度至 40%），表示已走过的路段
- 颜色：行进中路径线 `#4488FF`；已走过路段 `#4488FF` 40% 透明度；预览虚线 `#4488FF` 60% 透明度

**V-3 节点占领颜色变化**
- 飞船到达 NEUTRAL 节点后，节点颜色立即从灰色（`#888888`）变为玩家阵营蓝色（`#2266CC`）
- 颜色切换无过渡动画（MVP），即时响应——体现「落子即成」的决策实体感

**V-4 确认卡 UI**
- 弹出位置：目标节点旁边（屏幕空间投影，避免遮挡路径线）
- 内容：目的地名称 + 跳数 + 预计到达时间（来自 F-2）
- 样式：半透明深色背景卡片，确认按钮蓝色，取消按钮灰色

**V-5 IN_TRANSIT 期间飞船图标样式**
- 飞船图标在行进中使用「移动状态」变体（可使用相同图标 + 动态光晕效果区分静止与移动）
- 取消按钮在点击 IN_TRANSIT 飞船时显示（叠加在飞船图标旁）

### 音效需求

**A-1 派遣确认音效**
派遣确认时播放短促的发令音（`sfx_fleet_dispatch`，0.3s），与行军线出现同步触发。

**A-2 节点到达音效**
每次完成一跳（中间节点）播放轻微音效（`sfx_fleet_hop`，0.1s）；到达最终目的地播放较响亮的占领音效（`sfx_fleet_arrive`，0.5s）。

**A-3 取消音效**
取消派遣时播放短促撤令音（`sfx_fleet_cancel`，0.2s）。

**A-4 占领音效**
节点颜色变蓝时（占领 NEUTRAL 节点），播放占领成功音效（`sfx_node_capture`，0.8s），宏观感、有分量——这是帝国扩张的物理反馈。

## UI Requirements

**UI-1 飞船选择交互（必需，MVP）**
- 玩家点击星图上己方 DOCKED 飞船图标 → 飞船进入「已选中」状态（图标高亮，周围显示选中环）
- 同时显示：所有从当前节点可 BFS 抵达的目标节点高亮（灰色节点变亮），不可抵达节点不高亮
- 点击空白区域或已选中飞船 → 取消选中

**UI-2 派遣确认卡（必需，MVP）**
- 玩家点击高亮目标节点后弹出确认卡，内容：
  - 目的地名称（节点 ID 或名称）
  - 路径跳数（`hop_count`）
  - 预计到达时间（`total_travel_time`，来自 F-2，格式：「约 X 秒」）
- 按钮：「确认派遣」（蓝色）/ 「取消」（灰色）
- 点击确认卡外部区域 = 取消

**UI-3 行军路径线（必需，MVP）**
- 派遣确认后，星图持续显示完整路径线（实线，颜色见 V-2）
- 已走过路段颜色变暗（不透明度 40%）
- IN_TRANSIT 飞船图标显示「移动中」状态（区别于 DOCKED）

**UI-4 取消派遣按钮（必需，MVP）**
- 点击 IN_TRANSIT 飞船 → 弹出小型操作面板，仅有「取消派遣」按钮（灰色）
- 不显示重新选择目的地的选项（取消 = 返回，不支持改道，见 D-EX-4）

**UI-5 无法派遣反馈（必需，MVP）**
- 玩家尝试派遣 IN_TRANSIT / IN_COMBAT / IN_COCKPIT 飞船时：显示灰色提示文字（1.5s 淡出）
  - IN_TRANSIT → 「飞船正在航行中」
  - IN_COMBAT → 「飞船正在战斗中」
  - IN_COCKPIT → 星图整体锁定，无需提示
- BFS 找不到路径时：目标节点不高亮（不可点击），不弹出确认卡

**UI-6 MVP 排除的 UI 元素**
- 无跳数上限提示 / 行程消耗资源显示（MVP 派遣免费）
- 无多舰队同屏调度 UI（MVP 单飞船）
- 无途经节点的预览占领动画（占领即时生效，无预览）

> 📌 **UX Flag — 舰队调度系统**：本系统 UI 需求与星图 UI 系统高度耦合。在 Pre-Production 阶段，运行 `/ux-design` 为星图 UI 创建 UX spec，派遣流程的完整交互细节（手势、触屏热区、确认卡布局）应在 `design/ux/star-map.md` 中详细规范，而非在本 GDD 直接定义布局。

## Acceptance Criteria

**AC-1 [dispatch]** — GIVEN 玩家有一艘 `IN_TRANSIT` 状态飞船，WHEN 玩家点击该飞船，THEN 系统不显示确认卡，显示提示「飞船正在航行中」；D-1 的 5 项前置条件中任意单独破坏一项均应阻断派遣并给出对应提示（IN_TRANSIT/IN_COMBAT/IN_COCKPIT → 对应提示；目标节点 DOCKED 冲突 → 「目标节点已有飞船驻扎」；BFS 无路径 → 目标节点不高亮、确认卡不弹出）。

**AC-2 [dispatch]** — GIVEN 玩家有一艘合法 DOCKED 飞船，WHEN 玩家依次点击飞船 → 高亮目标节点，THEN 步骤①后飞船高亮，合法目标节点显示可达标记，不可达节点无标记；步骤②后确认卡弹出，展示路径跳数和预计到达时间；确认后飞船状态变为 `IN_TRANSIT`，图标立即向第一跳目标节点方向移动。

**AC-3 [pathfinding]** — GIVEN 存在一条途经 UNEXPLORED 节点的较短路径和一条全程 EXPLORED 的较长路径，WHEN 玩家派遣，THEN `LockedPath` 记录全程 EXPLORED 的路径，飞船不经过 UNEXPLORED 节点。

**AC-4 [pathfinding]** — GIVEN 最短路径 A→B→C 中节点 B 有友方 DOCKED 飞船，备选路径 A→D→C 存在，WHEN 玩家从 A 派遣到 C，THEN `LockedPath = [A, D, C]`，不包含 B。

**AC-5 [pathfinding]** — GIVEN A→Z 存在两条等长路径 `[A,M,Z]` 和 `[A,B,Z]`，所有节点均 EXPLORED 且无 DOCKED 阻塞，WHEN 玩家派遣，THEN `LockedPath = [A, B, Z]`（字典序更小）；相同配置下结果确定性一致。

**AC-6 [pathfinding]** — GIVEN 飞船已在途，`LockedPath = [A,B,C,D]`，飞船正在 B→C 跳，WHEN 节点 C 上新停靠了友方飞船，THEN 飞船继续沿原锁定路径飞行，`LockedPath` 不变，不重新计算（路径快照不受后续星图变化影响）。

**AC-7 [timing]** — GIVEN `HopProgress = 2.8s`，`FLEET_TRAVEL_TIME = 3.0s`，WHEN 下一帧 `Time.deltaTime = 0.4s`，THEN 本跳触发到达事件；`CurrentHopIdx` 递增 1；新跳 `HopProgress` 初始化为 `0.2s`（余量 = 3.2 − 3.0，不丢弃）；飞船位置渲染在新跳约 6.7% 处。

**AC-8 [timing]** — GIVEN fromNode.position = (0,0,0)，toNode.position = (100,0,0)，`HopProgress = 1.5s`，WHEN 渲染帧更新，THEN 飞船图标世界坐标 X = 50（误差 ±0.1）；该渲染位置不影响 `DispatchOrder` 任何字段；节点占领和战斗触发均以到达事件（HopProgress ≥ 3.0s）为准，与视觉位置无关。

**AC-9 [arrival]** — GIVEN 路径 `[A(PLAYER_OWNED), B(NEUTRAL), C(NEUTRAL)]`，WHEN 飞船到达中间节点 B，THEN `SetNodeOwnership(B, PLAYER_OWNED)` 调用一次，飞船保持 `IN_TRANSIT` 立即继续向 C；到达终点 C 时 `SetNodeOwnership(C, PLAYER_OWNED)` 再次调用，飞船状态变 `DOCKED`，`CloseOrder` 执行。

**AC-10 [arrival]** — GIVEN 路径终点为 ENEMY 节点 E，WHEN 飞船到达 E，THEN 状态变 `IN_COMBAT`；VICTORY → 状态变 `DOCKED`（不自动续航），节点变 `PLAYER_OWNED`；DEFEAT → `CloseOrder(orderId)` 先调用，`DestroyShip(shipId)` 后调用（EC-8，顺序可通过日志时间戳验证，CloseOrder 时间戳严格早于 DestroyShip）。

**AC-11 [cancel]** — GIVEN `LockedPath = [A,B,C,D]`，`CurrentHopIdx = 1`（B→C 跳），`HopProgress = 1.2s`，WHEN 玩家取消，THEN `IsCancelled = true`；路径反转；`HopProgress_after = 3.0 − 1.2 = 1.8s`（误差 ±0.017ms @ 60fps）；飞船图标平滑反向移动，取消前后图标位置差 < 1px（视觉无跳变）。

**AC-12 [cancel][edge-case]** — GIVEN 飞船反向路径经过中间节点 B（NEUTRAL），WHEN 飞船在反向途中抵达 B，THEN `SetNodeOwnership` 不被调用，B 归属不变；飞船继续飞向出发节点 A 直至 DOCKED（EC-7）。

**AC-13 [dispatch][edge-case]** — GIVEN 飞船 X 处于 `IN_TRANSIT`，WHEN 玩家尝试进入 X 的驾驶舱，THEN 驾驶舱入口被禁用，提示「飞船正在航行中」，`IN_COCKPIT` 不触发；反向测试：飞船处于 `IN_COCKPIT` 时，星图派遣操作被整体锁定（D-11）。

**AC-14 [edge-case]** — GIVEN 全局已有一艘 `IN_COCKPIT` 飞船，WHEN 系统尝试触发第二艘飞船进入 `IN_COCKPIT`（如另一艘飞船到达 ENEMY 节点），THEN 第二个 `IN_COCKPIT` 触发被阻断或排队等待；任意时刻 `IN_COCKPIT` 飞船数量 ≤ 1（EC-6）。

**AC-15 [perf]** — GIVEN 目标 Android 设备，星图上 5 艘飞船同时处于 `IN_TRANSIT` 并实时插值移动，WHEN 连续运行 60 秒，THEN 帧率 ≥ 60fps；Draw Call ≤ 200；`FleetDispatchSystem.Update()` 单帧 CPU 耗时 ≤ 2ms；GC Alloc ≤ 1KB/帧（零 per-hop GC Alloc）。

## Open Questions

**Q-1 FLEET_TRAVEL_TIME 最优值（待原型验证）**
- 当前值 3.0 秒来自星图系统 GDD，但未经实际星图交互验证。3.0s 是「快感决策」还是「等待焦虑」的边界，需在星图原型中测试。
- 候选方案：2.0s（节奏更快）/ 3.0s（当前值）/ 5.0s（更具战略分量感）
- 目标：`/prototype 星图交互` 完成后验证；锁定前同步更新 star-map-system.md

**Q-2 多场战斗同时触发的处理策略（待决策）**
- 当前设计：第二艘到达 ENEMY 节点的飞船保持 IN_COMBAT 状态，等待第一场战斗结算后再触发（AC-14）。但「等待队列」的实现细节未定——是自动进入下一场，还是需要玩家手动确认？
- 候选方案 A：自动按到达时序依次触发（对玩家透明，但可能出现玩家不在场的连续战斗）
- 候选方案 B：第二场战斗弹出通知，等玩家确认后进入驾驶舱（给玩家控制感，但需要 UI）
- 候选方案 C：MVP 直接排除多场并发战斗（每次只允许 1 艘飞船在 IN_COMBAT / IN_COCKPIT）
- 目标：在星图 UI GDD 设计前锁定，影响 UI 通知系统设计

**Q-3 取消派遣是否需要二次确认（待 UX 决策）**
- 当前设计：点击 IN_TRANSIT 飞船 → 直接弹出「取消派遣」按钮，点击即执行取消，无二次确认弹窗。
- 考量：MVP「一目了然的指挥」支柱倾向于少弹窗；但触屏误触风险较高，取消是不可逆操作（飞船需要重新派遣）。
- 目标：UX 设计阶段（`/ux-design star-map`）决定；在 `design/ux/star-map.md` 中明确

**Q-4 无人值守战斗与调度系统的边界（来自 enemy-system.md Q-5）**
- 当前假设：无人值守战斗（`unattended_combat_result` 公式）不触发驾驶舱，敌人系统仅在玩家亲自进入驾驶舱时激活。
- 需要确认：`CombatSystem.TriggerCombat()` 是否区分「有人值守」和「无人值守」两种路径？调度系统触发的战斗是否总是「有人值守」？
- 目标：飞船战斗系统 Vertical Slice 前锁定；enemy-system.md Q-5 已同步标记

**Q-5 BFS 路径权重扩展（Vertical Slice 候选）**
- 当前 BFS 以跳数为最短路径标准（每跳等价）。未来是否引入加权 BFS——经过敌方节点的路径「成本」更高，反映实际战略风险？
- MVP 阶段：纯跳数，无权重（设计简单，实现确定）
- Vertical Slice 扩展候选：经过 ENEMY 节点路径成本加权
- 目标：Vertical Slice 路线图决定，不影响 MVP 实现
