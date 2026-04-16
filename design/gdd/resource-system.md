# 资源系统 (Resource System)

> **Status**: Designed (pending /design-review)
> **Author**: game-designer (Claude Code)
> **Last Updated**: 2026-04-12
> **Last Verified**: 2026-04-12
> **Implements Pillar**: 支柱1 经济即军事 / 支柱2 一目了然的指挥

## Summary

资源系统定义了《星链霸权》中所有资源的类型、积累规则和消耗规则，是整个经济-军事循环的数据基础。它规定：有哪些资源、建筑每秒产出多少、建造建筑和舰船需要花费多少。该系统不持有运行时状态——实际资源数量由殖民地系统持有，资源系统是其他所有经济系统查询规则的只读数据层。

> **Quick reference** — Layer: `Foundation` · Priority: `MVP` · Key deps: `None`

## Overview

资源系统是一个纯规则定义层，不包含任何游戏对象或可见 UI。它定义三类规则：**资源类型定义**（游戏世界中存在哪些资源种类、每种资源是否有上限、是否在殖民地间全局共享）；**生产速率定义**（每种建筑每单位时间产出多少某种资源）；**消耗代价定义**（建造一个建筑或生产一艘飞船需要花费多少每种资源）。运行时的资源数量由殖民地系统持有和更新；资源系统提供规则，不持有状态。建筑系统、殖民地系统和经济 UI 三个下游系统都通过查询资源系统来获取规则定义，任何资源规则的变更（如调整矿场产出速率）会即时影响整个经济平衡，无需修改其他系统。

## Player Fantasy

资源系统的玩家幻想不是"管理一个电子表格"，而是"亲手启动了一个越滚越大的雪球"。

玩家从不直接与资源系统交互——他们建造矿场，看到造船厂的进度条开始走动；他们占领新星域，感受到帝国产能悄然跃升。资源系统是幕后的引擎，玩家感受到的是引擎驱动的加速度：每一个建造决策都在为下一次扩张积蓄动能。

**核心情感**：创造者的骄傲——"这个帝国的成长曲线是我画的"。每一次产能跳跃都是对之前某个布局决策的回报，而不是随机的奖励。

**锚点时刻**：玩家花了几分钟纠结：先在新星域建矿场还是造船厂？选了矿场。三分钟后，发现自己同时在两个造船厂出船，而下一个目标星域看起来突然不那么遥远了。那个选择刚刚把时间表压缩了一半——这不是运气，这是布局的回报。

**支柱对齐**：
- 支柱1（经济即军事）：雪球的每一层都是"经济 → 军事 → 更多经济"的一次循环，资源系统是这个循环的规则定义者
- 支柱2（一目了然）：玩家凭直觉感受到"帝国在加速"，而不是通过分析数字——资源系统的规则必须足够简单，让这种直觉判断成为可能

## Detailed Design

### Core Rules

**资源类型**

| 资源 | 类型 | 存储上限 | 下限 | 世界含义 |
|------|------|---------|------|---------|
| 矿石 (Ore) | 积累型 | `ORE_CAP`（占位，原型后定） | 0 | 星球地壳原材料，帝国扩张的物质基础 |
| 能源 (Energy) | 流量型 | 无上限 | 0（净产出可为负，但不扣减存量） | 星球电网输出，维持帝国运转的动力 |

**生产节拍**：每秒 tick 一次（连续生产）

**生产速率**

| 来源 | 矿石/秒 | 能源/秒 |
|------|---------|---------|
| 殖民地基础产出（无需建筑） | 0 | +5 |
| 基础矿场 (Basic Mine) | +10 | -2 |
| 造船厂 (Shipyard，待机） | 0 | -3 |

**建造成本（一次性，开始时扣除）**

| 建筑/单位 | 矿石 | 能源 |
|-----------|------|------|
| 基础矿场 | 50 | 20 |
| 造船厂 | 80 | 30 |
| 舰船（MVP 单一类型） | 30 | 15 |

**消耗规则**
1. 建造开始时立即扣除全部成本
2. 资源不足时操作被拒绝，显示"资源不足"提示，不进入等待队列
3. 矿石不可为负；能源净产出可为负，但不扣减存量（仅触发 HUD 警告）
4. MVP 阶段舰船无维护成本

**能源赤字处理（MVP）**
- 当能源净产出 < 0 时：HUD 显示红色能源警告图标
- 无实际惩罚（MVP 简化）
- 矿石和能源产出继续正常运行

### States and Transitions

资源系统是纯规则层，无运行时状态机。运行时资源数量由殖民地系统持有。

资源系统定义的状态判断（供殖民地系统调用）：

| 判断 | 条件 | 结果 |
|------|------|------|
| 可建造 | `currentOre >= oreCost && currentEnergy >= energyCost` | 允许建造，立即扣除 |
| 资源不足 | 任一资源低于建造成本 | 拒绝操作，显示提示 |
| 能源警告 | 能源净产出 < 0 | HUD 警告（无惩罚） |
| 矿石满仓 | `currentOre >= ORE_CAP` | 矿场产出暂停（溢出丢弃） |

### Interactions with Other Systems

**资源系统对外暴露的接口（只读规则查询）：**

```csharp
// 查询生产速率
ResourceConfig.GetProductionRate(BuildingType) → (orePerSec, energyPerSec)

// 查询建造成本
ResourceConfig.GetBuildCost(BuildingType | UnitType) → (oreCost, energyCost)

// 查询存储上限
ResourceConfig.GetStorageCap(ResourceType) → int

// 查询殖民地基础产出
ResourceConfig.GetBaseColonyOutput() → energyPerSec

// 验证是否可建造（无副作用）
ResourceConfig.CanAfford(currentOre, currentEnergy, target) → bool
```

**下游系统依赖关系：**
- **殖民地系统**：持有运行时资源数量，每秒调用生产速率规则更新数值
- **建筑系统**：建造前调用 `CanAfford()` 验证，建造开始时调用扣除逻辑
- **经济 UI**：读取殖民地系统的运行时数值，不直接查询资源系统

## Formulas

### 1. net_ore_production

The net_ore_production formula is defined as:

`net_ore_production = mine_count × ORE_PER_MINE`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|----------|--------|------|-------|-------------|
| Number of Basic Mines in colony | mine_count | int | 0–∞ | 当前殖民地内已建成的基础矿场数量 |
| Ore output per mine per second | ORE_PER_MINE | int | 1–50 (default: 10) | 每座基础矿场每秒产出的矿石量（调参旋钮） |

**Output Range:** 0 to ∞（实际受 ORE_CAP 截断）；mine_count = 0 时输出为 0。

**Example:** 殖民地有 3 座矿场 → net_ore_production = 3 × 10 = **30 ore/sec**

---

### 2. net_energy_production

The net_energy_production formula is defined as:

`net_energy_production = COLONY_BASE_ENERGY + (mine_count × ENERGY_PER_MINE) + (shipyard_count × ENERGY_PER_SHIPYARD)`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|----------|--------|------|-------|-------------|
| Colony base energy output | COLONY_BASE_ENERGY | int | 0–20 (default: +5) | 每个殖民地固有的每秒能源产出，无需建筑 |
| Number of Basic Mines | mine_count | int | 0–∞ | 当前殖民地内已建成的基础矿场数量 |
| Energy delta per mine per second | ENERGY_PER_MINE | int | -10–0 (default: -2) | 每座矿场每秒消耗的能源（负值） |
| Number of Shipyards (idle) | shipyard_count | int | 0–∞ | 当前殖民地内处于待机状态的造船厂数量 |
| Energy delta per idle shipyard per second | ENERGY_PER_SHIPYARD | int | -10–0 (default: -3) | 每座待机造船厂每秒消耗的能源（负值） |

**Output Range:** 理论上 -∞ 到 +∞；MVP 阶段负值仅触发 HUD 警告，不扣减存量。正常游戏中预期范围约 -20 到 +30。

**Example:** 殖民地有 2 座矿场、1 座造船厂 → net_energy_production = 5 + (2 × -2) + (1 × -3) = **-2 energy/sec**（触发能源警告）

---

### 3. ore_accumulation

The ore_accumulation formula is defined as:

`ore_new = clamp(ore_current + net_ore_production × delta_time, 0, ORE_CAP)`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|----------|--------|------|-------|-------------|
| Ore amount after this tick | ore_new | int | 0–ORE_CAP | 本次 tick 结束后的矿石存量 |
| Ore amount before this tick | ore_current | int | 0–ORE_CAP | 本次 tick 开始前的矿石存量 |
| Net ore production rate | net_ore_production | int | 0–∞ | 由公式 1 计算得出的每秒净产出 |
| Time elapsed since last tick | delta_time | float | 0.0–1.0 sec | 距上次 tick 的秒数；固定 tick 时为 1.0 |
| Maximum ore storage capacity | ORE_CAP | int | 100–10000 (TBD) | 殖民地矿石存储上限（占位，原型后定） |

**Output Range:** 始终在 [0, ORE_CAP] 区间内；超出上限时溢出部分丢弃。

**Example:** ore_current = 480，net_ore_production = 30，delta_time = 1.0，ORE_CAP = 500 → ore_new = clamp(510, 0, 500) = **500**（满仓，溢出 10 矿石）

---

### 4. can_afford

The can_afford formula is defined as:

`can_afford = (ore_current >= ore_cost) AND (energy_current >= energy_cost)`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|----------|--------|------|-------|-------------|
| Current ore in colony | ore_current | int | 0–ORE_CAP | 殖民地当前矿石存量 |
| Current energy in colony | energy_current | int | 0–∞ | 殖民地当前能源存量 |
| Ore cost of target build | ore_cost | int | 0–∞ | 目标建筑/单位的矿石建造成本 |
| Energy cost of target build | energy_cost | int | 0–∞ | 目标建筑/单位的能源建造成本 |

**Output Range:** boolean；两个条件必须同时满足，任一不足即返回 false。

> **分层说明**：`can_afford` 是纯资源量检查。建造前置条件（节点所有权、HasShipyard 等）由各功能系统（colony-system、building-system）独立检查，不属于本公式职责。

**Example 1（通过）:** ore_current = 60，energy_current = 25，建造基础矿场（50 ore, 20 energy）→ **true**

**Example 2（拒绝）:** ore_current = 40，energy_current = 35，建造造船厂（80 ore, 30 energy）→ **false**（矿石不足）

---

### 5. time_to_afford

The time_to_afford formula is defined as:

```
ore_deficit    = max(0, ore_cost  - ore_current)
energy_deficit = max(0, energy_cost - energy_current)

time_to_afford_ore    = ore_deficit / net_ore_production        (if net_ore_production > 0)
time_to_afford_energy = energy_deficit / net_energy_production  (if net_energy_production > 0)

time_to_afford = max(time_to_afford_ore, time_to_afford_energy)
```

**Variables:**
| Variable | Symbol | Type | Range | Description |
|----------|--------|------|-------|-------------|
| Ore shortfall | ore_deficit | int | 0–ORE_CAP | 当前矿石与目标成本的差值，已足够时为 0 |
| Energy shortfall | energy_deficit | int | 0–∞ | 当前能源与目标成本的差值，已足够时为 0 |
| Net ore production rate | net_ore_production | int | 0–∞ | 公式 1 的输出 |
| Net energy production rate | net_energy_production | int | -∞–∞ | 公式 2 的输出 |
| Seconds until affordable | time_to_afford | float | 0.0–∞ | UI 显示的倒计时秒数；0 表示立即可建 |

**Special cases:**
- `can_afford = true` → time_to_afford = 0（UI 显示"可建造"）
- `net_ore_production = 0` 且 `ore_deficit > 0` → time_to_afford = ∞（UI 显示"需要矿场"）
- `net_energy_production <= 0` 且 `energy_deficit > 0` → time_to_afford = ∞（UI 显示"能源不足"）
- 结果向上取整到整秒（UI 显示用）

**Output Range:** 0 到 ∞；正常游戏中预期 0–300 秒。

**Example:** ore_current = 20，energy_current = 10，建造造船厂（80 ore, 30 energy），net_ore_production = 10，net_energy_production = 3
- ore_deficit = 60，energy_deficit = 20
- time_to_afford_ore = 6.0 sec，time_to_afford_energy = 6.67 sec
- time_to_afford = max(6.0, 6.67) = **6.67 sec → UI 显示 "7 秒"**

## Edge Cases

- **If ore_current = 0 AND net_ore_production = 0**: ore_accumulation = 0，矿石永远不增加。UI 显示 "+0/s"，time_to_afford 返回 Infinity，显示"需要建造矿场"。
- **If mine_count = 0 AND shipyard_count = 0**: net_energy_production = +5（仅殖民地基础），net_ore_production = 0。合法初始状态，玩家从零开始建设。
- **If ore_current = ORE_CAP AND net_ore_production > 0**: 溢出部分静默截断，不报错。UI 必须显示"满载"状态（颜色变化或图标），否则玩家不知道矿石在浪费。
- **If two build commands arrive in the same tick**: 串行处理，先到先得。第一个建造扣除后，第二个用更新后的余额重新检查 can_afford。禁止并发读取同一余额快照（防止双重扣除漏洞）。
- **If a tick fires during a build deduction**: 建造扣除是原子操作。执行顺序：① 扣除建造成本 → ② 更新建筑数量 → ③ 计算本 tick 产量。不允许在步骤①和②之间插入 tick。
- **If ore_current < 0**: 实现 bug，非法状态。断言失败，记录错误日志，强制 clamp 到 0。
- **If mine_count < 0 or shipyard_count < 0**: 实现 bug。断言失败，强制 clamp 到 0。
- **If ORE_CAP = 0**: 配置错误，非法状态。启动时验证 ORE_CAP > 0，否则拒绝启动并报错。
- **If net_ore_production = 0 AND ore_deficit > 0**: time_to_afford 中除零。返回 Infinity，UI 显示"需要建造矿场"，不显示数字倒计时。
- **If net_energy_production ≤ 0 AND energy_deficit > 0**: 能源赤字持续扩大，无法自然恢复。time_to_afford 返回 Infinity，UI 显示"能源不足，无法建造"。
- **If delta_time is large（离线进度）**: 支持离线进度，delta_time 无上限。ore_accumulation 公式的 clamp 保证矿石不超过 ORE_CAP；能源是流量型无上限，离线期间能源存量可能大幅增加（合法）。离线期间建筑持续产出，但不执行任何建造操作（建造需要玩家主动触发）。
- **If build is rejected due to insufficient resources**: UI 必须明确指出哪种资源不足（矿石 / 能源），不能只显示通用的"无法建造"。

## Dependencies

**上游依赖（资源系统依赖的系统）**
- 无。资源系统是 Foundation 层，无上游依赖。

**下游依赖（依赖资源系统的系统）**

| 系统 | 依赖类型 | 接口 | 方向 |
|------|---------|------|------|
| 建筑系统 | 硬依赖 | 查询建造成本（`GetBuildCost`）、验证可建造（`CanAfford`） | 资源系统 → 建筑系统 |
| 殖民地系统 | 硬依赖 | 查询生产速率（`GetProductionRate`）、基础产出（`GetBaseColonyOutput`）、存储上限（`GetStorageCap`） | 资源系统 → 殖民地系统 |
| 经济 UI | 软依赖 | 读取殖民地系统的运行时数值（不直接查询资源系统） | 间接依赖 |

**接口说明**
- 资源系统是只读规则层，所有接口均为查询（无副作用）
- 运行时资源数量由殖民地系统持有，资源系统不持有状态
- 建筑系统和殖民地系统必须在资源系统初始化后才能初始化

**双向一致性说明**
- 建筑系统 GDD 应列出"依赖资源系统"
- 殖民地系统 GDD 应列出"依赖资源系统"
- 经济 UI GDD 应列出"间接依赖资源系统（通过殖民地系统）"

## Tuning Knobs

| 旋钮名 | 默认值 | 安全范围 | 影响 | 交互关系 |
|--------|--------|---------|------|---------|
| `ORE_PER_MINE` | 10 ore/sec | 1–50 | 矿石积累速度；过高→玩家资源溢出，过低→建造等待时间过长 | 与 `ORE_CAP` 交互：ORE_CAP / ORE_PER_MINE = 填满时间 |
| `ENERGY_PER_MINE` | -2 energy/sec | -10–0 | 矿场的能源成本；过高（绝对值大）→能源赤字频繁，过低→能源管理无意义 | 与 `COLONY_BASE_ENERGY` 交互：决定"几座矿场开始赤字" |
| `ENERGY_PER_SHIPYARD` | -3 energy/sec | -10–0 | 造船厂的能源成本；过高→玩家无法同时运营多个造船厂 | 与 `COLONY_BASE_ENERGY` 和 `ENERGY_PER_MINE` 共同决定能源平衡点 |
| `COLONY_BASE_ENERGY` | +5 energy/sec | 0–20 | 殖民地基础能源产出；过低→玩家一开始就能源赤字，过高→能源管理无意义 | 设为 `\|ENERGY_PER_MINE\| + \|ENERGY_PER_SHIPYARD\|` 时，1矿场+1造船厂刚好平衡 |
| `ORE_CAP` | TBD（占位） | 100–10000 | 矿石存储上限；过低→玩家频繁满仓浪费，过高→存储压力消失 | 建议 = ORE_PER_MINE × 100（约 100 秒填满） |
| `MINE_ORE_COST` | 50 ore | 10–200 | 第一座矿场的门槛；过高→游戏开局太慢 | 与 `COLONY_BASE_ENERGY` 决定开局节奏 |
| `MINE_ENERGY_COST` | 20 energy | 0–100 | 建矿场的能源门槛 | 开局能源充足，此值不应超过 `COLONY_BASE_ENERGY × 10` |
| `SHIPYARD_ORE_COST` | 80 ore | 20–300 | 第一座造船厂的门槛；决定"矿场→造船厂"的等待时间 | 建议 = MINE_ORE_COST × 1.5–2.0 |
| `SHIPYARD_ENERGY_COST` | 30 energy | 0–100 | 建造船厂的能源门槛 | — |
| `SHIP_ORE_COST` | 30 ore | 10–150 | 造船速度；过低→舰队扩张太快，过高→军事循环太慢 | 与 `ORE_PER_MINE` 决定"1 矿场能多快造出 1 艘船" |
| `SHIP_ENERGY_COST` | 15 energy | 0–80 | 造船的能源门槛 | — |

**关键平衡关系**
- `COLONY_BASE_ENERGY` = `|ENERGY_PER_MINE| + |ENERGY_PER_SHIPYARD|` 时，1矿场+1造船厂刚好能源平衡（MVP 锚点场景）
- `ORE_CAP` 建议 = `ORE_PER_MINE × 100`（约 100 秒填满，给玩家足够的积累窗口）
- `SHIPYARD_ORE_COST / ORE_PER_MINE` = 建造造船厂需要的等待秒数（目标：30–60 秒）

## Visual/Audio Requirements

N/A — 资源系统是纯规则/数据层，不直接产生视觉或音频表现。视觉反馈（矿石满载动效、能源警告闪烁）属于经济 UI 系统的职责，在 UX 规格文档中定义。

## UI Requirements

资源系统本身不包含 UI，但其规则决定了经济 UI 必须显示的信息（数据由殖民地系统在运行时提供）。

**经济 UI 必须显示：**

1. **矿石当前值 / ORE_CAP**（格式：`当前值 / 上限`）
   - 满载时：特殊颜色或图标提示（防止玩家不知道矿石溢出浪费）

2. **矿石净产量**（格式：`+10/s`，无矿场时显示 `+0/s`）

3. **能源净产量**（格式：`+5/s`，赤字时显示红色负值如 `-2/s`）
   - 赤字时：红色能源警告图标（MVP 要求，无惩罚但必须可见）

4. **建造按钮状态**（每种建筑/舰船一套）：
   - 可建造：正常显示矿石 + 能源成本
   - 资源不足：灰色显示，明确指出哪种资源不足（矿石 / 能源），不能只显示通用"无法建造"
   - 时间倒计时（`time_to_afford` 公式输出）：`X 秒后可建`
   - 永远无法建造（time_to_afford = Infinity）：文字提示原因（`需要矿场` / `能源不足`）

**飞船 HUD 不显示资源信息**：资源是星图策略层的信息，切换到驾驶舱视角后不显示资源数值。

> 📌 **UX Flag** — 此系统有 UI 需求。在 Pre-Production 阶段运行 `/ux-design` 为经济 UI 创建 UX 规格文档（`design/ux/economy-ui.md`），在写 Epics 前完成。引用经济 UI 的 Stories 应引用 `design/ux/economy-ui.md`，而非直接引用本 GDD。

## Acceptance Criteria

### 核心规则覆盖

**AC-01 — 矿石上限（ORE_CAP）**
GIVEN 矿石当前值为 ORE_CAP，WHEN 经过任意正数 delta_time，THEN 矿石值保持等于 ORE_CAP，不超出上限。

**AC-02 — 能源无上限**
GIVEN 殖民地基地 + 3 座矿场处于运行状态，WHEN 经过 60 秒，THEN 能源值等于初始值加上 net_energy_production × 60，无任何截断。

**AC-03 — 建造扣费在操作开始时执行**
GIVEN 玩家矿石为 50，建造一座矿场需要 50 矿石，WHEN 玩家发起建造指令，THEN 矿石立即扣减至 0，建造开始；若此时再次发起建造，操作被拒绝。

**AC-04 — 资源不足时操作被拒绝**
GIVEN 玩家矿石为 30，建造成本为 50 矿石，WHEN 玩家尝试建造，THEN 操作被拒绝，矿石值不变，无任何扣减。

**AC-05 — 能源赤字仅触发警告**
GIVEN 矿场数量使 net_energy_production < 0，WHEN 经过任意 delta_time，THEN 系统显示能源警告，但不扣减矿石、不阻止建造、不施加任何惩罚。

### 公式覆盖

**AC-06 — 公式 1：矿石净产量**
GIVEN 矿场数量为 3，ORE_PER_MINE = 10，WHEN 查询 net_ore_production，THEN 返回值为 30 ore/sec。

**AC-07 — 公式 2：能源净产量**
GIVEN 殖民地基础产出（+5 energy/sec）、矿场 2 座（各 -2 energy/sec）、造船厂 1 座（-3 energy/sec），WHEN 查询 net_energy_production，THEN 返回值为 -2 energy/sec。

**AC-08 — 公式 3：矿石累积（含 clamp）**
GIVEN ore_current = ORE_CAP - 5，net_ore_production = 10，delta_time = 2，WHEN 执行一次 tick，THEN ore_current = ORE_CAP（clamp 生效，不超出上限）。

**AC-09 — 公式 4：can_afford 判断**
GIVEN ore_current = 100，energy_current = 20，建造成本为 ore_cost = 100 且 energy_cost = 20，WHEN 调用 can_afford，THEN 返回 true；若任一资源减少 1，THEN 返回 false。

**AC-10 — 公式 5：time_to_afford 计算**
GIVEN ore_current = 0，ore_cost = 50，net_ore_production = 10，energy_current = 30，energy_cost = 30，WHEN 查询 time_to_afford，THEN 返回 5 秒（能源已满足，矿石需 5 秒）。

### 边界与边缘情况

**AC-11 — 零产量时 time_to_afford 行为**
GIVEN ore_current = 0，ore_cost = 50，net_ore_production = 0（无矿场），WHEN 查询 time_to_afford，THEN 系统返回无穷大（Infinity），不发生除以零崩溃，UI 显示"需要建造矿场"。

**AC-12 — 离线进度（delta_time 无上限）**
GIVEN 玩家离线 8 小时（delta_time = 28800 秒），矿场 2 座，ORE_PER_MINE = 10，WHEN 玩家重新登录，THEN ore_current = clamp(离线前矿石 + 20 × 28800, 0, ORE_CAP)，delta_time 不被截断。

**AC-13 — 同时建造两座建筑（串行处理）**
GIVEN 玩家矿石恰好够建造两座矿场，WHEN 两条建造指令在同一帧到达，THEN 第一条指令成功扣费并开始建造，第二条指令因资源不足被拒绝；最终只有一座矿场开始建造。

**AC-14 — ore_current 不得为负**
GIVEN 任意合法游戏状态，WHEN 执行任意数量的 tick 或建造操作，THEN ore_current 始终 ≥ 0；若检测到负值，系统触发断言失败并将值 clamp 至 0。

## Open Questions

1. **ORE_CAP 具体值**：占位值，需原型验证后确定。参考公式：ORE_CAP = ORE_PER_MINE × 100（约 100 秒填满）。
2. **离线进度的用户体验**：玩家回来看到大量积累的矿石/能源时，是否需要"离线报告"UI？MVP 阶段可能不需要，但这个 UX 细节需要在 UX 规格阶段决策。
3. **舰船维护成本**：MVP 阶段舰船无维护成本，后续是否引入能源或矿石维护，以及何时引入，需要在 Alpha 阶段的资源系统修订版中决策。
4. **Q-X: 能源赤字惩罚机制**：Vertical Slice 阶段是否引入能源赤字惩罚机制？MVP 中赤字仅触发 HUD 警告（无实际惩罚）是有意简化，但可能削弱"经济即军事"支柱。候选方案：(A) 保持现状——惩罚由殖民地系统层实现；(B) 引入渐进式惩罚——赤字持续时建筑效率下降；(C) 引入硬停——赤字超阈值时暂停新建造。
