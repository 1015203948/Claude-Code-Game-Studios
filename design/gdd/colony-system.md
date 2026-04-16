# 殖民地系统 (Colony System)

> **Status**: Designed
> **Author**: Game Designer + Claude Agents
> **Last Updated**: 2026-04-12
> **Implements Pillar**: 支柱1（经济即军事）/ 支柱3（我的星际帝国）

## Overview

殖民地系统是《星链霸权》经济循环的运转引擎——它持有玩家帝国所有的运行时资源存量（矿石与能源），驱动产出节拍，并处理所有建造扣费。每秒一次，它遍历所有玩家占领的星图节点，累加每个节点上建筑的净产出（矿石 +N/秒、能源 ±N/秒），更新全局存量，并将当前余额暴露给建筑系统、飞船系统和经济 UI。

资源规则本身由资源系统定义（只读配置层）；建筑产出数据由建筑系统提供（`GetNodeProductionDelta`）；星图节点归属由星图系统维护。殖民地系统是这三者的聚合点：它知道「现在帝国有多少矿石和能源」，知道「每个节点每秒产出多少」，知道「一笔建造能不能付得起」。除此之外，它不拥有任何其他数据。

对玩家来说，殖民地系统就是「帝国经济的血液循环」。矿石存量是攻势资本——积累够了才能造船、打仗；能源存量是帝国的代谢率——每座矿场和船坞都在消耗它，能源赤字是一个无声的警报。玩家感受不到系统本身，他们感受到的是：占领一个 RICH 节点后矿石产量的飞跃，以及在建造第四座矿场之前盯着能源数字犹豫的那一秒。

## Player Fantasy

殖民地系统的幻想不是「管理资源」，而是「感受帝国在呼吸」。

每一次 tick，矿石默默增加——不是因为你按了什么按钮，而是因为你几分钟前在那个星域节点上做了一个决定。那座矿场是你建的，那个节点是你打下来的，那条产出曲线是你设计的。你在星图上什么都没做，帝国却在为你运转。这是一种罕见的幻想：**你不在现场，但你的意志无处不在**。

然后你跳进驾驶舱，飞过自己的领地边境。黑暗中，远处的殖民节点亮着冷蓝色的光——那不是「资源产出点 #3」，那是你某一次战斗的遗址，是你某一个午夜的决策变成的光点。能源数字在 HUD 一角微微跳动，矿石存量还在稳步攀升。帝国在你身后工作，你在前线。

**帝国感** 来自两个层次的同时共振：
- **数字层**：产量曲线告诉你布局是否正确——占领那个 RICH 节点之后的产量跃升，是策略兑现的即时反馈
- **空间层**：从驾驶舱看到自己领地的轮廓，感受到「这片星域是我一个节点一个节点拿下来的」的沉甸

**核心锚点时刻**：占领第三个星域节点后回到星图，看到矿石净产量从 +10/秒跳到 +30/秒——不是意外之喜，而是「我早就知道会是这样」的确认。那一刻你不只是在看数字，你在检阅自己的帝国。

**支柱对齐**：
- 支柱1（经济即军事）：矿石和能源不是游戏数值，是舰队战力的「前置工作」——每一秒产出都是在为下一次扩张积蓄动能
- 支柱3（我的星际帝国）：归属感来自「我建的」「我打的」「我的布局」，而不是随机奖励

## Detailed Design

### Core Rules

**C-1：全局资源池**

殖民地系统持有两个全局存量，代表整个玩家帝国的经济状态：

```
_oreCurrent    : int    — 当前矿石存量，范围 [0, ORE_CAP]
_energyCurrent : int    — 当前能源存量，范围 [0, ∞)
```

- 所有 PLAYER 节点的建筑产出统一汇入同一个矿石池和能源池，不区分节点来源
- `_oreCurrent` 不可为负（clamp 下限为 0）；矿石满仓时（`_oreCurrent == ORE_CAP`）溢出静默丢弃
- `_energyCurrent` 不可为负（clamp 下限为 0）；能源净产出为负时触发警告事件，但不扣减存量

---

**C-2：产出 Tick 执行序列（每秒一次）**

产出节拍由 `ColonyManager` 的协程驱动（`WaitForSecondsRealtime(1f)`），不依赖帧率和 `Time.timeScale`：

```
TICK 序列（每 1 秒精确触发一次）

T-1  ColonyManager.OnTick() 触发
     — 来源：WaitForSecondsRealtime(1f) 协程
     — 本 tick 前，_oreCurrent / _energyCurrent 保持上一 tick 结束值

T-2  快照所有 PLAYER 节点（本 tick 内只读，不响应归属变更）
     nodeIds = StarMapData.GetAllNodesByOwner(PLAYER)

T-3  遍历节点，累加净产出
     for each nodeId in nodeIds:
         delta = BuildingSystem.GetNodeProductionDelta(nodeId)
         totalOreDelta   += delta.orePerSec
         totalEnergyDelta += delta.energyPerSec

T-4  更新矿石存量（含 clamp）
     _oreCurrent = Clamp(_oreCurrent + totalOreDelta, 0, ORE_CAP)

T-5  更新能源存量（下限 0，净产出为负时不扣减超出部分）
     _energyCurrent = Max(0, _energyCurrent + totalEnergyDelta)

T-6  发出状态变更事件
     OnResourcesUpdated(oreNew, energyNew, totalOreDelta, totalEnergyDelta)
     — 订阅方：经济 UI（刷新显示数字）

T-7  能源警告判断（仅在状态切换时发出，不每 tick 重复）
     if (totalEnergyDelta < 0) → OnEnergyDeficit(totalEnergyDelta)
     else → OnEnergyDeficitCleared()
```

**关键约束**：
- T-3 至 T-5 在同一帧内顺序执行，中间不允许建造操作插入（Unity 主线程单线程保证）
- 若 T-3 遍历抛出异常，本次 tick 中止，存量保持 tick 前值，记录 Error 日志

---

**C-3：产出缓存机制**

殖民地系统维护一个产出速率缓存（`_netOreProduction`，`_netEnergyProduction`），供 UI 实时显示净产量和计算 `time_to_afford`：

- 缓存在 tick 时更新（T-3 结果），也在每次建造完成后立即刷新（`RefreshProductionCache()`）
- UI 读取缓存值，不在每帧重新遍历所有节点计算
- 缓存不作为 tick 扣费的依据（tick 时重新调用 `GetNodeProductionDelta`）

---

**C-4：建筑建造扣费规则**

建筑建造的发起方是 UI 层（通过 `BuildingSystem.RequestBuild`），实际资源扣费由 `ColonyManager.DeductResources` 执行：

```
建筑建造前置检查序列（由 BuildingSystem 执行）：
  检查 BC-1（节点归属）：node.ownershipState == PLAYER
  检查 BC-2（资源充足）：CanAfford(ore_current, energy_current, oreCost, energyCost)

执行序列（检查全部通过后）：
  步骤 1：ColonyManager.DeductResources(oreCost, energyCost)
            — 原子扣除 _oreCurrent / _energyCurrent
            — 返回 {Success: true} 后才执行步骤 2
  步骤 2：BuildingSystem 创建 BuildingInstance，写入 node.Buildings
  步骤 3：BuildingSystem 调用 ColonyManager.RefreshProductionCache()
  步骤 4：BuildingSystem 发出 OnBuildingConstructed 事件
```

回滚规则（步骤 2 失败时）：
- 立即回滚 `_oreCurrent` / `_energyCurrent` 至扣费前快照值
- 不发出 `OnBuildingConstructed` 事件
- 记录 Error 日志，UI 显示「建造失败，资源已退还」

---

**C-5：飞船建造规则**

飞船建造由 UI 层直接调用 `ColonyManager.BuildShip(nodeId)`：

```
飞船建造前置检查序列：
  检查 B-1（节点归属）：StarMapData.GetOwnership(nodeId) == PLAYER
                          → 失败：FailReason = "NODE_NOT_PLAYER"
  检查 B-2（HasShipyard）：node.HasShipyard == true
                          → 失败：FailReason = "NO_SHIPYARD"
  检查 B-3（资源充足）：CanAfford(ore_current, energy_current,
                                   SHIP_ORE_COST=30, SHIP_ENERGY_COST=15)
                          → 失败：FailReason = "INSUFFICIENT_RESOURCES"

执行序列（检查全部通过后）：
  步骤 1：记录回滚快照 {snapshotOre, snapshotEnergy}
  步骤 2：原子扣费：_oreCurrent -= 30, _energyCurrent -= 15
  步骤 3：ShipSystem.CreateShip(nodeId)
            — 若失败：回滚至快照值，FailReason = "SHIP_CREATION_FAILED"，终止
  步骤 4：发出 OnResourcesUpdated（UI 刷新）
  步骤 5：发出 OnShipBuilt(nodeId, shipInstanceId)
```

**MVP 约定**：
- 建造即时完成（无建造时间，无进度条）
- 允许同一节点多次建造（资源是唯一约束）
- 建造成功后飞船立即出现在该节点的星图上（状态 = DOCKED）

---

**C-6：MVP 明确不在范围内（Out of Scope）**

| # | 排除项 |
|---|--------|
| OOS-1 | AI 资源经济（AI 节点的建筑产出不计入任何资源池）|
| OOS-2 | 飞船建造队列 / 建造时间 |
| OOS-3 | 资源节点间转移 / 贸易 |
| OOS-4 | 离线进度补算 |
| OOS-5 | 多种飞船类型差异化建造成本 |
| OOS-6 | 能源赤字惩罚（MVP 只警告）|

### States and Transitions

殖民地系统是无状态机的纯数据层——它没有自己的运行状态机，但维护两类状态：

**资源存量状态：**

| 状态标签 | 条件 | 触发事件 |
|---------|------|---------|
| `ENERGY_OK` | totalEnergyDelta ≥ 0 | OnEnergyDeficitCleared（首次进入时）|
| `ENERGY_DEFICIT` | totalEnergyDelta < 0 | OnEnergyDeficit（首次进入时）|

> 状态仅在首次切换时发出事件，不每 tick 重复发。实现用 `_lastEnergyDeficitState: bool` 记录上次状态。

**矿石满仓状态：**

| 状态 | 条件 | UI 提示 |
|------|------|---------|
| `ORE_NORMAL` | _oreCurrent < ORE_CAP | 正常显示 |
| `ORE_FULL` | _oreCurrent == ORE_CAP | 满仓图标 + 矿石颜色变化（UI 层处理）|

> 满仓状态检查在 T-4 之后、T-6 事件里通过 OnResourcesUpdated 通知 UI，UI 自行判断。

### Interactions with Other Systems

**殖民地系统对外暴露的接口：**

```csharp
// 状态读取接口（只读，任何系统可调用，无副作用）
ColonyManager.GetOreAmount()         → int        // 当前矿石存量
ColonyManager.GetEnergyAmount()      → int        // 当前能源存量
ColonyManager.GetOreCap()            → int        // 矿石存储上限
ColonyManager.GetNetOreRate()        → float      // 上一 tick 净矿石产出（供 UI 显示）
ColonyManager.GetNetEnergyRate()     → float      // 上一 tick 净能源产出（正负均可）
ColonyManager.CanAfford(ore, energy) → bool       // 无副作用预检（UI 按钮状态）

// 指令接口（有副作用，写操作）
ColonyManager.DeductResources(oreCost, energyCost)
    → { Success: bool, FailReason: string? }      // 建筑系统在扣费时调用

ColonyManager.BuildShip(nodeId)
    → { Success: bool, FailReason: string?, ShipInstanceId: string? }

ColonyManager.RefreshProductionCache()
    → void                                        // 建筑建造完成后由 BuildingSystem 调用

// 事件（发布，供 UI 和其他系统订阅）
event OnResourcesUpdated(int oreNew, int energyNew, float oreDelta, float energyDelta)
event OnProductionRateChanged(float netOre, float netEnergy)
event OnEnergyDeficit(float deficitRate)
event OnEnergyDeficitCleared()
event OnShipBuilt(string nodeId, string shipInstanceId)
```

**调用关系：**

```
调用方                  调用的接口
──────────────────────────────────────────────────────
经济 UI             GetOreAmount / GetEnergyAmount / GetNetOreRate / GetNetEnergyRate
                    订阅 OnResourcesUpdated / OnProductionRateChanged
HUD 警告系统        订阅 OnEnergyDeficit / OnEnergyDeficitCleared
建造按钮 UI         CanAfford（预检）→ DeductResources / BuildShip
BuildingSystem      DeductResources（扣费）→ RefreshProductionCache（建造完成后）
ShipSystem          被 ColonyManager 内部调用 CreateShip（非反向调用）
StarMapData         被 ColonyManager 查询（GetAllNodesByOwner）
ResourceConfig      被 ColonyManager 查询（GetStorageCap / CanAfford）
```

## Formulas

### 引用的上游公式（本系统不重复定义）

| 引用公式 | 来源 | 本系统用途 |
|---------|------|-----------|
| `net_ore_production` | 资源系统 | `global_ore_production` 的节点级基础项 |
| `net_energy_production` | 资源系统 | `global_energy_production` 的节点级基础项 |
| `can_afford` | 资源系统 | `can_build_ship` 的资源充足检查项 |
| `node_ore_output` | 建筑系统 | `global_ore_production` 的求和被加项 |

---

### 公式 1：global_ore_production（需注册 registry）

The global_ore_production formula is defined as:

`global_ore_production = Σ node_ore_output(n)  for all n where n.ownershipState == PLAYER`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|----------|--------|------|-------|-------------|
| 节点集合 | `n` | StarNode | — | 当前归属 PLAYER 的所有星图节点 |
| 单节点矿石产出 | `node_ore_output(n)` | float | 0–∞ | `mine_count × ORE_PER_MINE × GetOreMultiplier(n.id)`；来自 `BuildingSystem.GetNodeProductionDelta(nodeId).orePerSec` |

**Output Range:** 0（无 PLAYER 节点或无矿场）到 ∞（实际受 ORE_CAP 截断于 `tick_ore_update`）

**Example:** 3 个 PLAYER 节点：N-01（STANDARD，2 矿场）→ 20、N-02（RICH，1 矿场）→ 20、N-03（0 矿场）→ 0；
`global_ore_production = 20 + 20 + 0 = 40 ore/sec`

---

### 公式 2：global_energy_production（需注册 registry）

The global_energy_production formula is defined as:

`global_energy_production = Σ node_energy_delta(n)  for all n where n.ownershipState == PLAYER`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|----------|--------|------|-------|-------------|
| 节点集合 | `n` | StarNode | — | 当前归属 PLAYER 的所有星图节点 |
| 单节点能源变化量 | `node_energy_delta(n)` | float | -∞–+∞ | `COLONY_BASE_ENERGY + mine_count×ENERGY_PER_MINE + shipyard_count×ENERGY_PER_SHIPYARD`；来自 `BuildingSystem.GetNodeProductionDelta(nodeId).energyPerSec`；可为负数 |

**Output Range:** -∞ 到 +∞（为负时触发 HUD 警告，MVP 无实际惩罚）

**Example:** N-01（2矿场+1船坞）→ node_energy_delta = 5+(2×−2)+(1×−3) = −2；
N-02（1矿场，无船坞）→ node_energy_delta = 5+(1×−2)+(0×−3) = +3；
`global_energy_production = −2 + 3 = +1 energy/sec`

---

### 公式 3：tick_ore_update（殖民地系统内部，不注册 registry）

The tick_ore_update formula is defined as:

`tick_ore_update = clamp(ore_current + global_ore_production × TICK_INTERVAL, 0, ORE_CAP)`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|----------|--------|------|-------|-------------|
| tick 前矿石存量 | `ore_current` | float | 0–ORE_CAP | 本次 tick 开始时的矿石存量 |
| 全局矿石净产出 | `global_ore_production` | float | 0–∞ | 公式 1 输出，单位 ore/sec |
| tick 间隔 | `TICK_INTERVAL` | float | 固定 1.0 | 单位：秒；MVP 固定 1 秒，不随帧率浮动 |
| 矿石上限 | `ORE_CAP` | int | TBD | 来自资源系统常量；原型验证后填入 |

**Output Range:** 0 到 ORE_CAP

**Example:** `ore_current = 980`，`global_ore_production = 40`，`ORE_CAP = 1000`
→ raw = 1020 → `clamp(1020, 0, 1000) = 1000`（满仓截断，超出 20 矿石静默丢弃）

---

### 公式 4：tick_energy_update（殖民地系统内部，不注册 registry）

> **注**：资源系统定义能源为「流量型，无上限」。本公式仅 clamp 下限为 0，不设上限。

The tick_energy_update formula is defined as:

`tick_energy_update = max(0, energy_current + global_energy_production × TICK_INTERVAL)`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|----------|--------|------|-------|-------------|
| tick 前能源存量 | `energy_current` | float | 0–∞ | 本次 tick 开始时的能源存量 |
| 全局能源净产出 | `global_energy_production` | float | -∞–+∞ | 公式 2 输出，单位 energy/sec；可为负 |
| tick 间隔 | `TICK_INTERVAL` | float | 固定 1.0 | 单位：秒 |

**Output Range:** 0 到 ∞（无上限；下限 clamp 为 0，存量不会变负）

**Example:** `energy_current = 3`，`global_energy_production = −5`
→ raw = 3 + (−5) × 1.0 = −2 → `max(0, −2) = 0`（归零，不变负；MVP 无额外惩罚）

---

### 公式 5：can_build_ship（殖民地系统内部门控，不注册 registry）

The can_build_ship formula is defined as:

`can_build_ship(nodeId) = (node.ownershipState == PLAYER) AND (node.HasShipyard == true) AND can_afford(ore_current, energy_current, SHIP_ORE_COST, SHIP_ENERGY_COST)`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|----------|--------|------|-------|-------------|
| 节点归属 | `node.ownershipState` | enum | PLAYER/ENEMY/NEUTRAL | 星图系统维护；非 PLAYER 则短路返回 false |
| 节点有船坞 | `node.HasShipyard` | bool | true/false | 建筑系统维护（写），殖民地系统读取；false 则短路返回 false |
| 矿石存量 | `ore_current` | float | 0–ORE_CAP | 殖民地系统持有的全局存量 |
| 能源存量 | `energy_current` | float | 0–∞ | 殖民地系统持有的全局存量 |
| 飞船矿石造价 | `SHIP_ORE_COST` | int | 固定 30 | 常量，来自资源系统 |
| 飞船能源造价 | `SHIP_ENERGY_COST` | int | 固定 15 | 常量，来自资源系统 |
| 资源充足判断 | `can_afford(...)` | bool | true/false | 引用资源系统公式 |

**Output Range:** boolean；短路求值顺序：归属 → 船坞 → 资源

**Example A（通过）：** 节点 PLAYER，HasShipyard=true，ore=50≥30，energy=20≥15 → **true**

**Example B（无船坞，最常见失败）：** PLAYER 节点，HasShipyard=false → **false**（短路，不检查资源；UI 显示「需要船坞」）

**Example C（非己方节点）：** ownershipState=ENEMY → **false**（第一条就短路；UI 建造入口整体隐藏）

## Edge Cases

**EC-COL-01：tick 进行中节点归属变更**

- **场景**：T-2 快照后，某 PLAYER 节点在本 tick 内被敌方占领（T-3 至 T-5 期间）
- **行为**：本 tick 继续使用快照集合，该节点仍参与本次产出计算；下一 tick 重新快照时该节点已不在 PLAYER 集合，自动排除
- **理由**：快照机制保证单次 tick 内数据一致性；归属变更在星图系统中已有时序保证，殖民地系统无需重复处理

---

**EC-COL-02：矿石满仓（_oreCurrent == ORE_CAP）**

- **场景**：tick 后 raw 值超过 ORE_CAP
- **行为**：`clamp(raw, 0, ORE_CAP)` 静默截断；超出部分丢弃，不累积、不回退
- **UI**：OnResourcesUpdated 中携带新存量，UI 层检测到 `oreNew == ORE_CAP` 时显示满仓图标并将矿石数字颜色变为警告色
- **无事件**：满仓不单独发出事件；UI 自行从 OnResourcesUpdated 推断状态

---

**EC-COL-03：能源归零后持续负产出**

- **场景**：`_energyCurrent == 0` 且 `totalEnergyDelta < 0`（如多座船坞）
- **行为**：`max(0, 0 + negative) = 0`，存量保持 0，不变负；OnEnergyDeficit 在首次进入赤字时发出一次，后续 tick 不重复
- **MVP 范围内**：无产出惩罚（矿场不停产、建造不禁用）；仅 HUD 显示警告色
- **恢复**：玩家拆除耗能建筑（MVP 不支持）或占领新产能节点后 totalEnergyDelta 转正，发出 OnEnergyDeficitCleared

---

**EC-COL-04：飞船建造扣费成功但 ShipSystem.CreateShip 失败**

- **场景**：BuildShip 步骤 2 完成资源扣除后，步骤 3 ShipSystem.CreateShip 抛出异常或返回失败
- **行为**：ShipSystem 调用 `ColonyManager.RefundResources(30, 15)` 回滚扣款至快照值；ColonyManager 不发出 OnShipBuilt；UI 层收到失败通知后显示「建造失败，资源已退还」
- **日志**：记录 Error 级日志，包含 nodeId 和失败原因
- **接口**：RefundResources(oreCost, energyCost) 由 ShipSystem 在失败路径上调用；ColonyManager 内部执行 `_oreCurrent = snapshotOre; _energyCurrent = snapshotEnergy`

---

**EC-COL-05：同帧多次 BuildShip 调用（并发建造）**

- **场景**：UI 层在同一帧内连续触发两次 BuildShip（如双击、快速重复点击）
- **行为**：Unity 主线程串行执行；第一次调用扣费后 _oreCurrent 已减少，第二次调用的资源充足检查（B-3）使用最新存量——若余量不足则拒绝，返回 INSUFFICIENT_RESOURCES
- **追加约束**：BuildShip 仅检查目标节点资源是否充足，MVP 不限制"单节点同时只能有一艘飞船在建造中"（建造即时完成，无队列状态）；每次调用独立校验三项前置条件

---

**EC-COL-06：节点沦陷后的 tick 产出**

- **场景**：玩家在 T-2 快照前失去某节点（星图系统已更新归属）
- **行为**：T-2 `GetAllNodesByOwner(PLAYER)` 不包含该节点；该节点产出本 tick 即不计入全局池
- **建筑归属**：建筑实例随节点归属转移（由建筑系统处理）；殖民地系统不持有建筑数据，无需额外处理
- **收复**：节点重新归属 PLAYER 后，下一 tick 快照自动包含，产出恢复

---

**EC-COL-07：所有 PLAYER 节点归零（帝国全灭）**

- **场景**：玩家失去最后一个 PLAYER 节点
- **行为**：两路独立触发，任意一路到达先生效——
  - **主路**：星图系统监听 HOME_BASE 节点归属变更，直接触发游戏失败流程（不经过殖民地系统）
  - **备路（保险）**：殖民地系统 tick 时 `nodeIds.Count == 0`，发出 `OnAllColoniesLost()` 事件；游戏管理器订阅此事件作为兜底触发
- **全灭后 tick**：`totalOreDelta = 0, totalEnergyDelta = 0`；存量保持不变（不归零）；游戏失败前 UI 最后一帧数据保留

---

**EC-COL-08：建造操作与 tick 的原子性**

- **场景**：DeductResources 或 BuildShip 在 tick 的 T-3 至 T-5 执行期间被调用
- **行为**：Unity 主线程单线程执行，协程的 `WaitForSecondsRealtime` 在帧边界挂起；建造操作和 tick 更新不会真正并发，天然串行
- **保证**：若建造在 tick 之前完成，新建筑的产出在下一 tick 生效（RefreshProductionCache 更新缓存，但 tick 已使用本次快照）；若建造在 tick 之后，新建筑下一 tick 起算
- **不需要互斥锁**：C# 事件和字段更新均为同步操作，无异步竞争

---

**EC-COL-09：time_to_afford 计算中产出基数变化**

- **场景**：UI 正在显示「还需 X 秒可建造」，此时玩家建造了新矿场使净产出增加
- **行为**：UI 每帧读取 `GetNetOreRate()`（来自缓存 `_netOreProduction`）重新计算 `time_to_afford = (cost - current) / rate`；BuildingSystem 调用 RefreshProductionCache 后缓存立即更新，下一帧 UI 自动读到新速率
- **边界值**：`rate ≤ 0` 时显示「—」而非负数或除零；`current ≥ cost` 时显示 0（资源已够）

---

**EC-COL-10：离线进度（玩家关闭应用后重开）**

- **MVP 范围**：OOS-4（离线进度补算）明确排除
- **实际行为**：存档保存当前 `_oreCurrent` / `_energyCurrent`；读档后从存档值恢复，tick 协程重新启动；离线期间视为资源冻结，不累加产出
- **UI 提示**：读档后无需提示离线进度（因为无补算）；存量数字直接显示存档值

## Dependencies

### 上游依赖（本系统消费的接口）

| 依赖系统 | 使用的接口 / 数据 | 本系统用途 | 依赖 GDD |
|---------|-----------------|-----------|---------|
| **资源系统** | 常量：SHIP_ORE_COST=30、SHIP_ENERGY_COST=15、ORE_CAP（TBD）；公式：`can_afford` | 飞船建造资源充足检查；矿石存量上限 clamp | design/gdd/resource-system.md ✅ |
| **资源系统** | 常量：ORE_PER_MINE、ENERGY_PER_MINE、ENERGY_PER_SHIPYARD、COLONY_BASE_ENERGY | tick 产出公式的参数（通过 GetNodeProductionDelta 间接消费） | design/gdd/resource-system.md ✅ |
| **建筑系统** | `BuildingSystem.GetNodeProductionDelta(nodeId)` → `{orePerSec, energyPerSec}` | tick T-3 遍历每节点产出 | design/gdd/building-system.md ✅ |
| **建筑系统** | `node.HasShipyard : bool` | BuildShip B-2 前置检查 | design/gdd/building-system.md ✅ |
| **星图系统** | `StarMapData.GetAllNodesByOwner(PLAYER)` → `IEnumerable<nodeId>` | tick T-2 快照 PLAYER 节点集合 | design/gdd/star-map-system.md ✅ |
| **星图系统** | `StarMapData.GetOwnership(nodeId)` → `OwnershipState` | BuildShip B-1 前置检查 | design/gdd/star-map-system.md ✅ |
| **星图系统** | HOME_BASE 节点归属变更事件 | EC-COL-07 游戏失败主路径（星图系统监听，非殖民地系统负责触发） | design/gdd/star-map-system.md ✅ |

### 下游依赖（消费本系统输出的系统）

| 下游系统 | 消费的接口 / 事件 | 用途 | 依赖 GDD |
|---------|-----------------|------|---------|
| **飞船系统** | `BuildShip(nodeId)` 指令接口 | 发起飞船建造（UI 通过飞船系统调用） | design/gdd/ship-system.md ✅ |
| **飞船系统** | `RefundResources(oreCost, energyCost)` | 飞船创建失败时回滚扣款（EC-COL-04） | design/gdd/ship-system.md ✅ |
| **经济 UI** | 所有只读接口（GetOreAmount / GetEnergyAmount / GetNetOreRate / GetNetEnergyRate / CanAfford）；事件：OnResourcesUpdated / OnProductionRateChanged / OnEnergyDeficit / OnEnergyDeficitCleared | 资源存量显示、净产量显示、建造按钮状态、能源警告 | design/gdd/economy-ui.md（未设计）|
| **星图 UI** | 事件：OnResourcesUpdated | 星图层资源 HUD 刷新 | design/gdd/star-map-ui.md（未设计）|
| **HUD 警告系统** | 事件：OnEnergyDeficit / OnEnergyDeficitCleared | 驾驶舱能源赤字警告显示 | 飞船 HUD GDD（未设计）|
| **存档/读档系统** | `_oreCurrent` / `_energyCurrent` 数据字段 | 序列化帝国资源状态；读档后恢复存量并重启 tick 协程 | design/gdd/save-load-system.md（未设计）|

### 双向一致性要求

- **建筑系统 ↔ 殖民地系统**：`GetNodeProductionDelta(nodeId)` 的返回结构须为 `{ float orePerSec, float energyPerSec }`；双方 GDD 对此接口的描述必须一致
- **星图系统 ↔ 殖民地系统**：`GetAllNodesByOwner` 须保证 tick 内结果稳定（调用一次后不会中途变化）；快照策略由殖民地系统负责（T-2 一次性获取）
- **飞船系统 ↔ 殖民地系统**：`HasShipyard` 写权归属为建筑系统，殖民地系统和飞船系统均只读；飞船系统 GDD 应在 Dependencies 中标注此只读约定
- **存档/读档系统 ↔ 殖民地系统**：离线期间资源冻结（OOS-4），读档后从存档值恢复 `_oreCurrent` / `_energyCurrent` 并重启 tick 协程；两侧均须对齐此约定

## Tuning Knobs

以下旋钮均为全局参数，调整后无需修改代码——通过 ScriptableObject（ResourceConfig）热加载。
所有旋钮均定义于资源系统（source GDD），此处列出殖民地系统侧的消费含义和安全范围。

| 旋钮名 | 当前值 | 安全范围 | 影响的游戏体验 | 调整建议 |
|--------|--------|---------|--------------|---------|
| `ORE_CAP` | TBD（原型后填入） | ORE_PER_MINE × 50 至 ORE_PER_MINE × 200 | 矿石满仓频率 → 过低：玩家扩张速度受限，溢出浪费严重；过高：积累安全感过强，失去危机张力 | 建议从 ORE_PER_MINE × 100（=1000）开始，根据原型中"扩张三个节点后多久满仓"调整 |
| `SHIP_ORE_COST` | 30 ore | 10–100 | 飞船建造门槛 → 过低：玩家随意建造，战略深度下降；过高：新玩家积累太慢，进入战斗太晚 | 标杆：3 个 STANDARD 节点满建矿场后约 10 秒可建一艘（30 ore / 30 ore·s⁻¹）|
| `SHIP_ENERGY_COST` | 15 energy | 0–30 | 飞船建造的能源门控 → 过高：能源赤字时无法建造，卡死局面；设为 0：无能源约束 | 与 COLONY_BASE_ENERGY=5 协调——单节点基础能源 = 5，一艘飞船消耗 15，约需 3 节点基础能源才能建造 |
| `ORE_PER_MINE` | 10 ore/s | 5–30 | 矿石积累速度 → 影响扩张节奏整体快慢；所有"time_to_afford"计算的基数 | 与 MINE_ORE_COST 联动：希望玩家建矿场后约 5 秒内回本，则 MINE_ORE_COST / ORE_PER_MINE ≈ 5 |
| `ENERGY_PER_MINE` | −2 energy/s | −5 至 0 | 矿场的能源消耗 → 决定能源赤字出现时机；= 0 则能源无约束 | 与 COLONY_BASE_ENERGY=5 和 ENERGY_PER_SHIPYARD=−3 协调：单节点最多建 1 矿场 + 1 船坞才不赤字（5−2−3=0）|
| `ENERGY_PER_SHIPYARD` | −3 energy/s | −10 至 0 | 船坞的能源消耗 → 船坞的策略成本；= 0 则免费 | 船坞是高价值建筑，维持较高能源成本（≥ ENERGY_PER_MINE 的 1.5×）强制玩家在矿场和船坞之间取舍 |
| `COLONY_BASE_ENERGY` | +5 energy/s | 0–15 | 每个 PLAYER 节点的基础能源收入 → 决定能源赤字发生的难易度 | = 0 时无基础能源，首座矿场或船坞即进入赤字；建议维持在 ENERGY_PER_MINE + ENERGY_PER_SHIPYARD 的绝对值之间 |
| `TICK_INTERVAL` | 1.0 秒 | 固定，不调整 | 产出节拍频率 → MVP 固定为 1 秒，不作为调节旋钮 | 调整会影响所有 time_to_afford 计算和玩家体验节奏；留为 Vertical Slice 后的扩展点 |

### 联动调节注意事项

- **SHIP_ORE_COST × tick 速率 → 飞船建造等待感**：SHIP_ORE_COST / (节点数 × ORE_PER_MINE) = 玩家等待秒数；过长（> 60 秒）失去节奏，过短（< 5 秒）失去策略权衡
- **能源平衡公式**：单节点最大无赤字建筑组合 = `COLONY_BASE_ENERGY / |ENERGY_PER_MINE + ENERGY_PER_SHIPYARD|`；建议此值在 1–2 之间（每节点只能放 1–2 座建筑才不赤字）
- **ORE_CAP 与扩张节奏**：满仓时间 = `ORE_CAP / global_ore_production`；建议在 3 个节点满建时满仓时间约 30–60 秒，引导玩家持续扩张而非囤积

## Visual/Audio Requirements

殖民地系统是纯数据-逻辑层，无直接的视觉或音效资产需求。所有视觉/音频反馈由订阅方系统负责，规格在其对应 GDD 中定义。

| 反馈类型 | 触发事件 | 处理方 | 规格所在 GDD |
|---------|---------|--------|-------------|
| 资源数字更新动画（矿石/能源跳动） | OnResourcesUpdated | 经济 UI | 经济 UI GDD（未设计）|
| 矿石满仓视觉提示（图标 + 颜色变化） | OnResourcesUpdated（oreNew == ORE_CAP） | 经济 UI | 经济 UI GDD（未设计）|
| 能源赤字 HUD 警告（警告色 + 图标闪烁） | OnEnergyDeficit | 飞船 HUD | 飞船 HUD GDD（未设计）|
| 飞船建造成功音效 + 视觉 | OnShipBuilt | 飞船系统 / 星图 UI | 飞船 HUD GDD（未设计）|
| 帝国全灭失败画面 | OnAllColoniesLost | 游戏管理器 | 双视角切换系统 GDD（未设计）|

> 殖民地系统本身无需实现任何渲染或音频代码。

## UI Requirements

殖民地系统通过事件和只读接口向 UI 层暴露数据，自身不实现任何 UI 逻辑。以下为 UI 层需要消费的数据规格（详细 UI 设计在经济 UI GDD 中定义）。

### 经济 UI 需要的数据接口

| UI 元素 | 数据来源 | 刷新时机 | 格式要求 |
|--------|---------|---------|---------|
| 矿石存量数字 | `GetOreAmount()` | 订阅 `OnResourcesUpdated` | 整数，无小数点 |
| 能源存量数字 | `GetEnergyAmount()` | 订阅 `OnResourcesUpdated` | 整数，无小数点 |
| 矿石净产量（+N/s） | `GetNetOreRate()` | 订阅 `OnProductionRateChanged` | 一位小数，加 "/s" 后缀 |
| 能源净产量（±N/s） | `GetNetEnergyRate()` | 订阅 `OnProductionRateChanged` | 一位小数，负数显示红色 |
| 矿石进度条 / 满仓状态 | `GetOreAmount()` / `GetOreCap()` | 订阅 `OnResourcesUpdated` | `_oreCurrent / ORE_CAP`；满仓时颜色变警告色 |
| 建造按钮可用性 | `CanAfford(oreCost, energyCost)` | 每帧轮询（建造菜单打开时）| bool → 按钮灰显 / 正常 |
| 预计等待时间（time_to_afford） | `GetNetOreRate()` + `GetOreAmount()` | 每帧重算（建造菜单打开时）| "约 Xs"；rate ≤ 0 时显示"—" |

### HUD 警告系统需要的事件

| 警告类型 | 触发事件 | 清除事件 | UI 行为 |
|---------|---------|---------|---------|
| 能源赤字警告 | `OnEnergyDeficit(deficitRate)` | `OnEnergyDeficitCleared()` | 驾驶舱 HUD 显示能源图标闪烁 + 赤字数值（详见飞船 HUD GDD）|

## Acceptance Criteria

> **测试类型说明**：「单元」= Unity Test Framework EditMode，使用 mock/stub 隔离外部依赖，无场景加载；「集成」= PlayMode，需真实场景和帧循环，验证跨系统协作。

### 一、Tick 产出正确性

**AC-COL-01** | 单元 | Given 两个 PLAYER 节点，矿石产出分别为 +3 和 +5，能源产出分别为 +2 和 +4；When 一次 tick 执行完毕；Then `_oreCurrent` 精确增加 8，`_energyCurrent` 精确增加 6，`OnResourcesUpdated` 携带的 ore delta = 8，energy delta = 6。

**AC-COL-02** | 单元 | Given 一个 PLAYER RICH 节点，基础矿石产出 = 4，RICH_NODE_ORE_MULTIPLIER = 2.0；When 一次 tick 执行完毕；Then `GetNodeProductionDelta` 返回 orePerSec = 8（4 × 2.0），`_oreCurrent` 增量 = 8。

**AC-COL-03** | 单元 | Given 所有节点归属均为 ENEMY（零个 PLAYER 节点）；When 一次 tick 执行完毕；Then `_oreCurrent` 和 `_energyCurrent` 均无变化（delta = 0），`OnResourcesUpdated` 仍被发出一次（ore delta = 0，energy delta = 0），系统不抛出异常。

**AC-COL-04** | 单元 | Given tick T 开始时快照了 3 个 PLAYER 节点；When tick T 运行至 T-3 期间，其中一个节点归属变为 ENEMY；Then tick T 仍累计 3 个节点的产出（快照不可变），变更效果仅在 tick T+1 生效。

### 二、资源存量边界

**AC-COL-05** | 单元 | Given `_oreCurrent = ORE_CAP - 2`，本 tick 净矿石产出 = 10；When tick 执行 T-4（clamp）；Then `_oreCurrent == ORE_CAP`（而非 ORE_CAP + 8），溢出的 8 静默丢弃，无异常，无错误日志。

**AC-COL-06** | 单元 | Given `_energyCurrent = 2`，所有节点能源净产出合计 = -5；When tick 执行 T-5（floor=0）；Then `_energyCurrent == 0`（而非 -3），`OnEnergyDeficit` 被发出恰好 1 次（首次进入赤字状态）。

**AC-COL-07** | 单元 | Given `_oreCurrent = 30`，`_energyCurrent = 15`；When 调用 `CanAfford(30, 15)` → 返回 true；When 调用 `CanAfford(31, 15)` 或 `CanAfford(30, 16)` → 返回 false（精确判断，无浮点容差）。

### 三、飞船建造全路径

**AC-COL-08** | 单元 | Given 节点归属 PLAYER，`HasShipyard = true`，`_oreCurrent = 50`，`_energyCurrent = 20`；When 调用 `BuildShip(nodeId)`；Then `_oreCurrent = 20`，`_energyCurrent = 5`，`ShipSystem.CreateShip` 被调用，`OnShipBuilt` 事件发出，返回 `{Success: true}`。

**AC-COL-09** | 单元 | Given 目标节点归属 ENEMY；When 调用 `BuildShip(nodeId)`；Then B-1 失败，`DeductResources` 不被调用，存量不变，返回 `{Success: false, FailReason: "NODE_NOT_PLAYER"}`。

**AC-COL-10** | 单元 | Given 节点归属 PLAYER，`HasShipyard = false`；When 调用 `BuildShip(nodeId)`；Then B-2 失败，`DeductResources` 不被调用，返回 `{Success: false, FailReason: "NO_SHIPYARD"}`。

**AC-COL-11** | 单元 | Given 节点归属 PLAYER，`HasShipyard = true`，`_oreCurrent = 20`（< 30）；When 调用 `BuildShip(nodeId)`；Then `CanAfford` 返回 false，`DeductResources` 不被调用，返回 `{Success: false, FailReason: "INSUFFICIENT_RESOURCES"}`。

**AC-COL-12** | 单元 | Given 所有前置条件满足，`DeductResources` 成功扣除（ore-30, energy-15），`ShipSystem.CreateShip` 随后返回失败；When BuildShip 收到创建失败信号；Then `RefundResources(30, 15)` 被调用，`_oreCurrent` 恢复扣费前值，`_energyCurrent` 恢复扣费前值，`OnShipBuilt` 不发出（EC-COL-04 回滚验证）。

**AC-COL-13** | 单元 | Given `_oreCurrent = 35`，`_energyCurrent = 20`，同帧连续调用 `BuildShip` 两次；When 串行执行完毕；Then 第一次成功（`_oreCurrent = 5`，`_energyCurrent = 5`），第二次因 `CanAfford(30, 15)` 返回 false 被拒绝，`_oreCurrent` 仍 = 5，`_energyCurrent` 仍 = 5。

### 四、建筑建造扣费

**AC-COL-14** | 单元 | Given `_oreCurrent = 100`，`_energyCurrent = 50`，建筑费用 ore=40，energy=20；When 调用 `DeductResources(40, 20)`；Then `_oreCurrent = 60`，`_energyCurrent = 30`，返回 `{Success: true}`。

**AC-COL-15** | 单元 | Given `_oreCurrent = 20`，建筑费用 ore=40；When 调用 `DeductResources(40, any_energy_cost)`；Then `CanAfford` 返回 false，`_oreCurrent` 和 `_energyCurrent` 保持不变，返回 `{Success: false}`，无部分扣费（原子性保证）。

### 五、事件正确性

**AC-COL-16** | 单元 | Given 能源已处于赤字状态（`_energyCurrent = 0`，净产出 < 0）；When 连续执行第 2、第 3 次 tick，`_energyCurrent` 保持 0；Then `OnEnergyDeficit` 在后续 tick 中均不再发出（仅状态首次切换时发出一次）。Given 随后净能源产出 ≥ 0，`_energyCurrent` 恢复正值；When 执行下一 tick；Then `OnEnergyDeficitCleared` 发出恰好 1 次。

**AC-COL-17** | 集成 | Given 至少 1 个 PLAYER 节点存在；When 每次 tick 执行完 T-6；Then `OnResourcesUpdated` 在每个 tick 内恰好发出 1 次，事件参数中 oreNew、energyNew 与系统内部 `_oreCurrent`、`_energyCurrent` 完全一致。

### 六、缓存一致性

**AC-COL-18** | 单元 | Given `GetNetOreRate()` 当前返回 20（两个节点各 +10）；When 建筑系统在某节点新建矿场（使该节点产出增加 +10 ore/s），随后调用 `RefreshProductionCache()`；Then `GetNetOreRate()` 立即返回 30（无需等待下一 tick），`_netEnergyProduction` 不受影响。

### 七、边界场景

**AC-COL-19** | 集成 | Given 节点 A 在 tick T 快照后归属变为 ENEMY（EC-COL-06）；When tick T+1 执行；Then 节点 A 的产出不再累加，`_oreCurrent` 和 `_energyCurrent` 的增量仅来自其余 PLAYER 节点。

**AC-COL-20** | 集成 | Given 游戏中仅剩 1 个 PLAYER 节点，该节点在 tick T 快照后归属变为 ENEMY（EC-COL-07）；When tick T+1 执行（PLAYER 节点数 = 0）；Then `OnAllColoniesLost` 事件发出恰好 1 次，tick delta 均为 0，系统不崩溃，不抛出 NullReferenceException。

## Open Questions

| # | 问题 | 重要性 | 最晚解决时机 |
|---|------|--------|------------|
| OQ-1 | `ORE_CAP` 具体值？当前为 TBD，原型建议起始值 1000（ORE_PER_MINE × 100）。需 `/prototype 飞船驾驶舱操控` 完成后根据实际扩张节奏确定。 | 高 | Vertical Slice 开始前 |
| OQ-2 | 能源赤字 MVP 仅警告，不惩罚——是否足够？玩家是否会忽视警告，使能源系统失去策略意义？需要 Playtest 后评估。 | 中 | 首次 Playtest 后 |
| OQ-3 | `RefundResources` 的调用方是 ShipSystem（飞船系统失败时回调），还是 ColonyManager 内部捕获异常自行回滚？EC-COL-04 当前设计为 ShipSystem 主动回调——需在飞船系统 GDD 中确认接口约定。 | 高 | 飞船系统 GDD 设计时 |
| OQ-4 | 多玩家 / AI 敌方是否需要资源池？MVP OOS-1 已排除 AI 经济，但 Vertical Slice 后的 AI 扩张系统是否需要镜像一套 `EnemyColonyManager`？需在敌人系统 GDD 设计时决策。 | 低（MVP 外） | 敌人系统 GDD 设计时 |
| OQ-5 | 建筑建造失败回滚的 UI 提示「建造失败，资源已退还」——触发频率是否会让玩家困惑？需确认 BuildingSystem.RequestBuild 的前置检查是否足够严格，避免服务端拒绝但 UI 已显示成功的情况。 | 中 | 经济 UI GDD 设计时 |
