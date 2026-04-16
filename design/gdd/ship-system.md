# 飞船系统 (Ship System)

> **Status**: Designed (pending /design-review)
> **Author**: game-designer (Claude Code) + user
> **Last Updated**: 2026-04-14
> **Implements Pillar**: 支柱1 经济即军事 / 支柱4 从星图到驾驶舱

## Overview

飞船系统是《星链霸权》所有飞行与战斗体验的数据根基：它定义了每艘飞船的核心属性集合——生命上限、操控响应、武器挂点与推进性能——并在运行时为每一个舰队单元维护这些数据的活跃实例。玩家不会直接"感受"到飞船系统本身，而是通过它所支撑的五个子系统——生命值、操控、战斗、敌人以及舰队调度——间接体验它的存在：当你在星图上下令建造一艘耗费 30 矿石与 15 能源的战舰，随后切换视角亲自驾驶它冲入战场时，飞船系统已经悄然决定了这艘船能承受多少伤害、飞得多快、打得多准。MVP 阶段仅有一种飞船类型，但正是这一份蓝图的严谨定义，构成了整个战斗体系得以运转的共同语言。

## Player Fantasy

飞船系统的玩家幻想是**投射感与牵挂**——玩家不是在管理一组数值，而是在为帝国铸造血肉。

每一艘飞船都是经济决策的具现：它消耗了你花心思挖出来的 30 矿石和 15 能源，带着你亲手建立的造船厂的烙印出现在星图上。玩家不会直接"接触"飞船系统，但他们会因为飞船系统的存在而感到**牵挂**——一艘被打残的侦察艇图标不是一个数字变红，而是"那是我的"。

**核心情感：帝国的出征者，而非棋盘上的棋子**

玩家感受到的是：这艘船是我的资源变成的，是我的帝国派出去的，因此值得我切换视角亲自驾驶它去战场。飞船的属性——生命上限、推进性能、武器挂点——通过生命值警示、操控手感、命中反馈安静地传递给玩家，不需要阅读数值面板，凭直觉就知道"这艘船还能战"还是"该撤了"。

**支柱对齐：**
- **支柱1（经济即军事）**：飞船是经济产出的军事化身——造船的资源决定了你有多少"血肉"可以投入战场
- **支柱4（从星图到驾驶舱）**：正是"这艘船是我的"这种牵挂感，驱动玩家从抽象的星图指挥跳入第一人称驾驶舱
- **支柱3（我的星际帝国）**：每一艘飞船的存亡都是帝国历史的一部分，不是可随时刷新的消耗品

**锚点时刻**：玩家在星图调兵时停顿了——要不要把那艘低血量的老船派去侦察那个稀有矿节点？它建于帝国最艰难的起步期，用的是最早的矿石。玩家决定亲自去开，跳进驾驶舱，感受到推进器略显吃力的响应——飞船系统正在无声地讲述这艘船的状态。

## Detailed Design

### Core Rules

**1. 飞船蓝图（ShipBlueprint）**

蓝图是只读静态配置，不持有运行时状态。每种飞船类型对应一条蓝图记录。

| 字段 | 类型 | MVP 值 | 说明 |
|------|------|--------|------|
| `BlueprintId` | string | `"generic_v1"` | 唯一标识；扩展新飞船时新增条目 |
| `DisplayName` | string | `"通用战舰"` | UI 显示名 |
| `MaxHull` | float | *（待定，原型验证后填入）* | 飞船最大生命值上限 |
| `ThrustPower` | float | *（待定，原型验证后填入）* | 加速力，单位 m/s² |
| `TurnSpeed` | float | *（待定，原型验证后填入）* | 转向角速度，单位 deg/s |
| `WeaponSlots` | int | `1` | 可装配武器槽数（MVP：1 个前向武器） |
| `BuildCost` | ResourceBundle | 30矿/15能源 | 建造费用（已由资源系统锁定） |
| `RequiredShipyardTier` | int | `1` | 建造所需造船厂等级（1=基础船坞，2=升级船坞） |
| `HangarCapacity` | int | `0` | 可搭载的战斗机数量上限（0=不是航母） |
| `CarrierOwned` | bool | `false` | true=战斗机，归属航母管理，不单独停靠节点 |

> `BuildCost` 使用 `ResourceBundle` 值对象（资源类型→数量映射），而非两个分散字段，为后续多资源类型扩展预留。

**已注册蓝图清单**

| BlueprintId | DisplayName | MaxHull | ThrustPower | TurnSpeed | WeaponSlots | BuildCost | RequiredShipyardTier | HangarCapacity |
|-------------|-------------|---------|-------------|-----------|-------------|-----------|---------------------|----------------|
| `generic_v1` | 通用战舰 | *待定* | *待定* | *待定* | 1 | 30矿/15能源 | 1 | 0 |
| `carrier_v1` | 星链航母 | 150 | 40 | 15 | 2 | 120矿/60能源 | 2 | 动态（见规则H-2）|
| `fighter_v1` | 舰载战斗机 | 20 | 120 | 80 | 1 | 20矿/10能源 | 1（停靠节点） | 0 |

> `carrier_v1.HangarCapacity` 为动态值，在航母**离港时快照**停靠节点的 `ShipyardTier`：Tier 1 = 1架，Tier 2 = 3架。快照后不随节点升级自动更新，航母返港重新停靠时刷新。

**2. 飞船实例（ShipInstance）**

实例是运行时对象，持有当前状态。实例属性通过 `BlueprintId` 引用蓝图查询，不直接存储属性副本。

| 字段 | 类型 | 说明 |
|------|------|------|
| `InstanceId` | string | 运行时唯一 ID（如 `"ship_001"`） |
| `BlueprintId` | string | 引用蓝图（解耦实例与静态配置） |
| `CurrentHull` | float | 当前生命值（由飞船生命值系统写入） |
| `ShipState` | enum | 当前状态（见状态机） |
| `DockedNodeId` | string? | 停靠节点 ID；`IN_TRANSIT` / `IN_COCKPIT` / `IN_COMBAT` 时为 null |
| `IsPlayerControlled` | bool | 身份标记（固定，创建时指定）：true = 玩家旗舰；false = AI 调度单元 |
| `CarrierInstanceId` | string? | 归属航母 ID（`fighter_v1` 专属）；null = 无主战斗机或非战斗机 |
| `CurrentHangarSlots` | int | 航母专属：当前可搭载战斗机数量（离港时快照，返港时刷新）；非航母固定为 0 |

> `IsPlayerControlled` 是身份标记而非行为标记，创建时固定，不随 `ShipState` 改变。

**3. 建造与销毁规则**

```
规则 B-1（建造 generic_v1）：
  前提：玩家占领节点 ownershipState == PLAYER
        AND 资源余额 >= BuildCost（30矿石 / 15能源）
        AND 目标节点当前无已驻扎舰队（星图规则 M-3）
        AND 目标节点 ShipyardTier >= 1（需建筑系统先建造基础船坞）
  触发：扣除 BuildCost → 创建 ShipInstance（ShipState = DOCKED）→ 写入 StarNode.dockedFleet

规则 B-2（建造 carrier_v1）：
  前提：玩家占领节点 ownershipState == PLAYER
        AND 资源余额 >= BuildCost（120矿石 / 60能源）
        AND 目标节点当前无已驻扎舰队（星图规则 M-3）
        AND 目标节点 ShipyardTier >= 2（需建筑系统升级船坞至 Tier 2）
  触发：扣除 BuildCost → 创建 ShipInstance（ShipState = DOCKED，CurrentHangarSlots 快照 ShipyardTier=2 → 3）→ 写入 StarNode.dockedFleet

规则 B-3（建造 fighter_v1）：
  前提：玩家占领节点 ownershipState == PLAYER
        AND 资源余额 >= BuildCost（20矿石 / 10能源）
        AND 目标节点 ShipyardTier >= 1
        AND 目标节点有已停靠的航母（carrier_v1 实例，ShipState = DOCKED）
        AND 该航母当前搭载数量 < CurrentHangarSlots
  触发：扣除 BuildCost → 创建 ShipInstance（BlueprintId=fighter_v1，CarrierInstanceId=航母 InstanceId，ShipState = DOCKED）
  注：战斗机不占用节点 dockedFleet 槽位（由航母的 HangarSlots 管理）

规则 H-1（航母离港快照）：
  航母从 DOCKED → IN_TRANSIT 转换时：CurrentHangarSlots = 停靠节点 ShipyardTier==1 ? 1 : 3
  已搭载战斗机数量不受影响；快照后不随节点升级变化，直到下次返港停靠

规则 H-2（战斗机视角切换）：
  玩家在战斗机A的 IN_COCKPIT 期间，可触发 TransferCockpitControl(from=A, to=B)：
  → 战斗机A: IN_COCKPIT → IN_COMBAT（AI 接管）
  → 战斗机B: IN_COMBAT → IN_COCKPIT（玩家接管）
  此操作必须是原子操作；失败时A保持 IN_COCKPIT，B保持 IN_COMBAT，不产生中间态

规则 H-3（航母摧毁后战斗机独立）：
  航母进入 DESTROYED 状态时：
  → 停靠在航母内（ShipState = DOCKED）的战斗机：同步 DESTROYED
  → 已出击（ShipState = IN_TRANSIT / IN_COMBAT / IN_COCKPIT）的战斗机：独立存续
    CarrierInstanceId 置为 null；战斗结束后就近停靠己方节点（ShipState = DOCKED）
  → 若玩家正在驾驶的战斗机 IN_COCKPIT 且其航母摧毁：战斗机独立存续，玩家继续控制
    退出驾驶舱时恢复 _preEnterState（已强制修正为星图状态，不再指向已摧毁的航母）

规则 D-1（战斗击毁）：CurrentHull 降至 0 → ShipState = DESTROYED → 从 StarNode.dockedFleet 移除
规则 D-2（节点沦陷）：驻扎节点被 AI 战斗胜利夺取且飞船未逃离 → ShipState = DESTROYED
```

**4. 数量约束**

MVP 阶段飞船数量无上限，资源是唯一约束。玩家只要有足够的矿石和能源，可以建造任意数量的飞船（受限于占领的节点数量，星图规则 M-3：每节点最多驻扎 1 艘飞船）。

---

### States and Transitions

```
状态枚举（ShipState）：
  DOCKED      → 停靠在星图节点，等待指令（默认状态）
  IN_TRANSIT  → 在星图上飞行中（逐跳移动）
  IN_COCKPIT  → 玩家正在驾驶（已进入驾驶舱视角，未遭遇战斗）
  IN_COMBAT   → 战斗中（玩家驾驶舱内交战，或无人值守战斗解算中）
  DESTROYED   → 已被摧毁（终态，不可恢复）
```

| 当前状态 | 触发条件 | 新状态 | 发起方 |
|----------|----------|--------|--------|
| `DOCKED` | 玩家下达移动指令（相邻节点） | `IN_TRANSIT` | 舰队调度系统 |
| `DOCKED` | 玩家点击"进入驾驶舱" | `IN_COCKPIT` | 双视角切换系统 |
| `DOCKED` | 所在节点被 AI 战斗夺取 | `DESTROYED` | 飞船战斗系统 |
| `IN_TRANSIT` | 经过 FLEET_TRAVEL_TIME 秒到达目标节点（非 ENEMY） | `DOCKED` | 舰队调度系统 |
| `IN_TRANSIT` | 到达目标节点（ENEMY 节点） | `IN_COMBAT` | 飞船战斗系统 |
| `IN_TRANSIT` | 玩家取消移动命令 | `DOCKED`（返回出发节点） | 舰队调度系统 |
| `IN_COCKPIT` | 玩家主动退出驾驶舱 | `_preEnterState`（DOCKED 或 IN_TRANSIT） | 双视角切换系统 |
| `IN_COCKPIT` | 遭遇敌人（进入战斗区域） | `IN_COMBAT` | 飞船战斗系统 |
| `IN_COCKPIT` | 飞船生命值降至 0 | `DESTROYED` | 飞船生命值系统 |
| `IN_COMBAT` | 战斗胜利（玩家驾驶） | `IN_COCKPIT` | 飞船战斗系统 |
| `IN_COMBAT` | 战斗失败（玩家驾驶） | `DESTROYED` | 飞船生命值系统 |
| `IN_COMBAT` | 无人值守战斗胜利 | `DOCKED`（目标节点） | 飞船战斗系统 |
| `IN_COMBAT` | 无人值守战斗失败 | `DESTROYED` | 飞船战斗系统 |
| `IN_COCKPIT` | 玩家触发战斗机视角切换（TransferCockpitControl，作为 from） | `IN_COMBAT` | 飞船系统（规则H-2） |
| `IN_COMBAT` | 玩家触发战斗机视角切换（TransferCockpitControl，作为 to） | `IN_COCKPIT` | 飞船系统（规则H-2） |
| `DESTROYED` | 任意 | `DESTROYED`（终态） | — |

> **互斥约束**：任一时刻，全局最多只有 1 艘飞船处于 `IN_COCKPIT` 状态（无论 `IsPlayerControlled` 值）。系统在所有 `→ IN_COCKPIT` 转换时验证此约束，包括 TransferCockpitControl 中的接管步骤。

---

### Interactions with Other Systems

**飞船系统对外暴露的接口：**

```csharp
// 蓝图查询（静态只读）
ShipBlueprint ShipData.GetBlueprint(string blueprintId)

// 实例属性查询（运行时）
float      ShipData.GetMaxHull(string instanceId)       // → Blueprint.MaxHull
float      ShipData.GetThrustPower(string instanceId)   // → Blueprint.ThrustPower
float      ShipData.GetTurnSpeed(string instanceId)     // → Blueprint.TurnSpeed
int        ShipData.GetWeaponSlots(string instanceId)   // → Blueprint.WeaponSlots
ShipState  ShipData.GetState(string instanceId)
bool       ShipData.IsPlayerShip(string instanceId)

// 写回接口（由下游系统调用）
void ShipData.SetState(string instanceId, ShipState newState)
void ShipData.SetCurrentHull(string instanceId, float hull)   // 仅飞船生命值系统调用
void ShipData.DestroyShip(string instanceId)                   // 设为 DESTROYED + 通知星图
void ShipData.TransferCockpitControl(string fromId, string toId) // 原子切换 IN_COCKPIT（规则H-2）
event Action<string, ShipState> OnShipStateChanged              // 参数：instanceId, newState；每次 SetState() 成功后触发

// 航母/战斗机接口
List<string>  ShipData.GetDockedFighters(string carrierId)          // 返回搭载战斗机 InstanceId 列表
int           ShipData.GetHangarSlotsAvailable(string carrierId)    // 剩余可搭载数量
void          ShipData.OnCarrierDestroyed(string carrierId)         // 处理在外战斗机 CarrierInstanceId 置 null
```

**各下游系统的读写关系：**

| 下游系统 | 读取（从飞船系统） | 写回（到飞船系统） |
|----------|------------------|------------------|
| **飞船生命值系统** | `GetMaxHull()` — 初始化 CurrentHull | `SetCurrentHull()` — 扣/回血；Hull=0 时调用 `DestroyShip()` |
| **飞船操控系统** | `GetThrustPower()`、`GetTurnSpeed()` — 驱动物理运动 | 无 |
| **飞船战斗系统** | `GetWeaponSlots()`、`GetState()` — 验证战斗合法性 | `SetState(IN_COMBAT / DOCKED)` — 战斗开始/结束 |
| **敌人系统** | `GetBlueprint()` — 创建敌人实例时参照属性 | 无（敌人是独立实例） |
| **舰队调度系统** | `GetState()` — 验证是否可调度（仅 DOCKED 可接受移动指令） | `SetState(IN_TRANSIT / DOCKED)` — 移动开始/结束 |
| **双视角切换系统** | `GetState()` — 验证切换合法性 | `SetState(IN_COCKPIT / _preEnterState)` — 切换进出驾驶舱；`TransferCockpitControl()` — 战斗机间切换 |
| **星图系统** | `GetState()` — 读取 dockedFleet 状态 | 间接：`DestroyShip()` 通知星图清空 `StarNode.dockedFleet` |
| **星图 UI** | `GetState()`、订阅 `OnShipStateChanged` | — |
| **飞船 HUD** | 订阅 `OnShipStateChanged` | — |
| **殖民地系统** | — | 调用 `RequestBuildShip()` 触发建造 |

## Formulas

> **注意**：飞船系统是纯数据模型层，绝大多数公式由下游系统定义并消费飞船属性。本节只定义属于飞船系统职责的公式，并注明哪些公式交叉引用其他系统。

---

**建造可行性**（交叉引用资源系统）

飞船系统不重复定义建造可行性公式——直接引用资源系统已定义的 `can_afford` 公式：

```
前置条件（建造合法）：
  can_afford(ORE,    SHIP_ORE_COST)    = true   // SHIP_ORE_COST = 30
  can_afford(ENERGY, SHIP_ENERGY_COST) = true   // SHIP_ENERGY_COST = 15

两个条件必须同时满足，建造请求才合法。
扣除逻辑由资源系统负责执行。
参见：design/gdd/resource-system.md §Formulas — can_afford
```

---

**飞船实例验证公式**

`is_valid_ship_instance` 公式定义如下：

`is_valid_ship_instance(ship) = (ship.MaxHull > 0) AND (ship.ThrustPower > 0) AND (ship.TurnSpeed > 0) AND (ship.WeaponSlots >= 0)`

**Variables:**

| Variable | Symbol | Type | Range | Description |
|----------|--------|------|-------|-------------|
| 最大生命值 | `ship.MaxHull` | float | > 0 | 飞船最大船体值（HP），= 0 为非法数据 |
| 推进强度 | `ship.ThrustPower` | float | > 0 | 飞船推力，= 0 表示飞船无法移动 |
| 转向速度 | `ship.TurnSpeed` | float | > 0 | 飞船转向角速度（deg/s），= 0 表示飞船无法转向 |
| 武器挂点 | `ship.WeaponSlots` | int | >= 0 | 可装配武器槽数，= 0 合法（无武器飞船，如侦察艇） |

**Output Range:** `true`（合法实例）或 `false`（数据错误，拒绝创建）

**Example:**
- MaxHull=100, ThrustPower=5.0, TurnSpeed=90, WeaponSlots=1 → `true`
- MaxHull=0, ThrustPower=5.0, TurnSpeed=90, WeaponSlots=1 → `false`（拒绝）

---

**舰队战力（Stub — 待定）**

```
MVP 阶段：战力 ≈ 飞船数量（所有飞船属性相同，无需公式）
正式公式归属：飞船战斗系统 GDD §Formulas — Fleet Power
依赖项：MaxHull（待定）、WeaponSlots = 1、ThrustPower（待定）
TODO: MaxHull 和武器系统确认后，由战斗系统 GDD 定义并反向引用本系统属性。
```

---

**公式归属声明（其他公式属于下游系统）**

| 公式类型 | 归属系统 | 飞船系统处理方式 |
|----------|----------|----------------|
| 建造可行性 | 资源系统 | 交叉引用 `can_afford` |
| 移动/操控计算 | 飞船操控系统 | 消费 ThrustPower / TurnSpeed |
| 伤害/生命值计算 | 飞船生命值系统 + 战斗系统 | 消费 MaxHull |
| 舰队战力评估 | 飞船战斗系统 | Stub 占位（见上） |
| 飞船实例验证 | **飞船系统**（本文档） | 完整定义 ✅ |

## Edge Cases

**状态机边缘**

- **如果**对 `DESTROYED` 飞船发出任何指令（移动、进入驾驶舱）：拒绝指令，`DESTROYED` 为终态，不接受任何状态转换请求，飞船实例标记为待清理。
- **如果**`IN_TRANSIT` 飞船收到"进入驾驶舱"指令：拒绝，`IN_TRANSIT → IN_COCKPIT` 为非法转换，玩家必须等飞船抵达目标节点并 `DOCKED` 后才能进入。
- **如果**`IN_COMBAT` 飞船收到"星图移动"指令：拒绝，战斗必须先有结果（胜利或 `DESTROYED`）再处理移动指令。
- **如果**`IN_COCKPIT` 飞船收到"星图移动"指令：拒绝，玩家必须先退出驾驶舱（`IN_COCKPIT → DOCKED`），再发起移动。
- **如果**飞船已处于 `IN_COCKPIT`，再次收到"进入驾驶舱"指令：幂等操作，静默忽略，状态不变。
- **如果**`IsPlayerControlled = false` 的飞船收到玩家驾驶舱指令：拒绝，AI 舰队不可进入 `IN_COCKPIT`。

**驾驶舱互斥边缘**

- **如果**玩家在飞船A的 `IN_COCKPIT` 期间，飞船B抵达 `ENEMY` 节点触发 `IN_COMBAT`：飞船B合法进入 `IN_COMBAT`（互斥约束仅作用于 `IN_COCKPIT`），飞船A保持 `IN_COCKPIT`，战斗逻辑在后台实时运算（依据星图规则 C-3）。
- **如果**玩家在飞船A的 `IN_COCKPIT` 期间，试图让飞船B也进入 `IN_COCKPIT`：拒绝，系统强制执行单一 `IN_COCKPIT` 约束，玩家必须先退出飞船A。
- **如果**玩家在飞船A的 `IN_COCKPIT` 期间，飞船A所在节点遭到攻击触发 `IN_COMBAT`：飞船A状态转换 `IN_COCKPIT → IN_COMBAT`，驾驶舱互斥锁自动释放。
- **如果**同一飞船同时收到"进入驾驶舱"和"星图移动"两条并发指令：以先到达状态机的指令为准，后到指令拒绝（先到先得，无队列）。

**建造边缘**

- **如果**建造检查通过后、扣款前资源被其他操作耗尽：以扣款时刻的资源量为准，扣款失败则建造取消，向玩家提示资源不足，不产生飞船实例。
- **如果**建造过程中目标节点被敌方占领：建造立即取消，**已扣除资源不返还**（视为建造中损毁）。
- **如果**建造检查通过后目标节点已有其他飞船停靠（调度冲突）：建造取消，**已扣除资源返还**（系统调度错误，非玩家失误），记录调度冲突日志。
- **如果**`BlueprintId` 引用了不存在的蓝图：飞船实例创建失败，拒绝建造，记录数据错误。

**销毁边缘（节点沦陷）**

- **如果**`IN_TRANSIT` 飞船的起点节点被夺取：飞船已离开起点，不受影响，继续飞行，正常转换。
- **如果**`IN_TRANSIT` 飞船的目标节点在飞行途中被敌方占领：飞船抵达后节点为 `ENEMY`，触发 `IN_COMBAT`（符合现有规则，无异常）。
- **如果**`IN_COCKPIT` 飞船所在节点被敌方占领：**飞船先销毁**（`ShipState = DESTROYED`），节点再更新归属；玩家被强制踢出驾驶舱视角，返回星图层。
- **如果**`DOCKED` 飞船所在节点被直接沦陷（无战斗）：**飞船先销毁**，节点再更新归属。
- **如果**`IN_COMBAT` 飞船所在节点在战斗进行中被宣告沦陷：**节点沦陷判定优先**，飞船直接 `DESTROYED`，不等战斗结算完成。

**数量边缘**

- **如果**玩家占领所有节点（最大舰队规模 = 节点总数）：合法状态，资源生产速率是实际软上限，无需特殊处理。
- **如果**玩家同时拥有大量飞船（性能边界）：数据模型无需限制，建议飞船实例软上限参见 Tuning Knobs。

## Dependencies

**飞船系统对外依赖（上游）：** 无。飞船系统是 Foundation 层，无上游系统依赖。

**飞船系统被依赖（下游）：**

| 依赖系统 | 依赖类型 | 数据接口 |
|----------|----------|---------|
| **飞船生命值系统** | 强依赖 | 读取 `GetMaxHull()` 初始化生命值；写回 `SetCurrentHull()`、`DestroyShip()` |
| **飞船操控系统** | 强依赖 | 读取 `GetThrustPower()`、`GetTurnSpeed()` 驱动物理运动 |
| **飞船战斗系统** | 强依赖 | 读取 `GetWeaponSlots()`、`GetState()` 验证战斗合法性；写回 `SetState()` |
| **敌人系统** | 软依赖 | 读取 `GetBlueprint()` 作为敌人实例创建的属性参照 |
| **舰队调度系统** | 强依赖 | 读取 `GetState()` 验证可调度性；写回 `SetState(IN_TRANSIT/DOCKED)` |
| **双视角切换系统** | 强依赖 | 读取 `GetState()` 验证切换合法性；写回 `SetState(IN_COCKPIT/DOCKED)` |
| **星图系统** | 间接依赖 | `DestroyShip()` 调用后通知星图清空对应 `StarNode.dockedFleet` |
| **星图 UI** | 强依赖 | 读取 `GetState()`、订阅 `OnShipStateChanged` 事件以同步星图图标显示 |
| **飞船 HUD** | 强依赖 | 订阅 `OnShipStateChanged` 事件以更新 HUD 状态显示 |
| **殖民地系统** | 强依赖 | 调用 `BuildShip()` 触发建造；调用 `RefundResources()` 处理建造取消退款 |

> **强依赖**：下游系统无法在无飞船系统的情况下运作。**软依赖**：下游系统可以独立初始化，但行为会有所降级（如敌人系统使用硬编码属性）。

## Tuning Knobs

| 调节旋钮 | 当前值 | 安全范围 | 过高后果 | 过低后果 |
|----------|--------|----------|----------|----------|
| `SHIP_MAX_HULL` | 100（原型起始值） | 50–500 | 飞船难以被摧毁，战斗缺乏张力 | 飞船太脆，玩家不敢进驾驶舱 |
| `SHIP_THRUST_POWER` | *待定（原型验证后填入）* | 视触屏手感测试 | 飞船过快难以控制，触屏操作失准 | 飞船响应迟钝，驾驶体验差 |
| `SHIP_TURN_SPEED` | *待定（原型验证后填入）* | 视触屏手感测试 | 转向过灵敏，难以精确瞄准 | 转向过慢，追击/逃脱均无趣 |
| `SHIP_WEAPON_SLOTS` | 1 | 1–4（MVP 固定为 1） | 武器系统复杂度超出 MVP 范围 | 0 = 无武器飞船（侦察用途，测试合法） |
| `SHIP_ORE_COST` | 30 | 15–100 | 建造门槛过高，玩家长时间无舰队 | 建造过廉，玩家无限刷船，经济循环崩溃 |
| `SHIP_ENERGY_COST` | 15 | 5–50 | 能源约束过强，压缩建造频率 | 能源约束失效，经济支柱弱化 |
| `CARRIER_ORE_COST` | 120 | 60–250 | 航母准入门槛过高，玩家无力建造 | 航母太廉价，失去里程碑感 |
| `CARRIER_ENERGY_COST` | 60 | 30–120 | 能源约束过强，推迟航母解锁 | 能源无约束，经济深度降低 |
| `CARRIER_MAX_HULL` | 150 | 80–400 | 航母几乎无敌，失去保护动机 | 航母太脆，不值得建造 |
| `FIGHTER_ORE_COST` | 20 | 10–50 | 战斗机建造太贵，舰载战术无法展开 | 战斗机免费感，失去资源权衡 |
| `FIGHTER_ENERGY_COST` | 10 | 5–30 | 能源约束压制战斗机数量 | 战斗机对能源无压力 |
| `SHIP_INSTANCE_SOFT_LIMIT` | 无（推荐 ≤ 20 用于性能测试参考） | N/A | 大量飞船实例造成帧率下降（工程边界，非设计边界） | N/A |

> **`SHIP_MAX_HULL`、`SHIP_THRUST_POWER`、`SHIP_TURN_SPEED` 具体值待飞船操控系统原型验证后填入。** 应优先运行 `/prototype 飞船驾驶舱操控` 确认触屏手感基准线，再回填本表。`SHIP_ORE_COST` 和 `SHIP_ENERGY_COST` 已由资源系统锁定，调整须同步更新 `design/registry/entities.yaml`。

## Visual/Audio Requirements

> **范畴声明**：飞船系统是数据-状态层（蓝图、实例、状态机）。本章节定义视觉/音效的**触发源**和**规格约束**；具体渲染实现归属见本章末尾的「归属边界」表。

### 1. 飞船视觉形态规格

**形态语言原则**

| 原则 | 规格 |
|------|------|
| **硬边几何** | 所有飞船轮廓为棱角分明的多边形剪影；禁止有机曲线或圆润外形 |
| **工业感分层** | 舰体由 2–3 个几何体组合：主舰体（最大）+ 侧翼/引擎舱（对称）+ 武装硬点（可选） |
| **低多边形** | 移动端预算目标：每艘飞船 200–400 三角面；URP Lit Shader，1 张 256×256 图集 |
| **发光作为存在感** | 飞船本体暗色（深灰/炭黑）；Emission 区域（引擎尾焰、武器硬点指示灯）是视觉重心 |
| **方向性可读** | 飞船朝向在星图小图标（约 24×24dp）和驾驶舱视角下均需一眼可辨别前后方向 |

**色彩编码（与游戏全局色彩哲学对齐）**

| 所属阵营 | 舰体基色 | Emission 颜色 | 备注 |
|---------|---------|--------------|------|
| **玩家** | 深炭灰 `#1A1F2E` | 冷蓝 `#4FC3F7` | 与玩家殖民地冷蓝色系统一 |
| **敌对** | 深炭灰 `#1A1F2E` | 橙红 `#FF5722` | 与敌方占领节点色系统一 |
| **中立/未知** | 深灰 `#2E2E2E` | 无 Emission | 灰色哑光，无发光 |

### 2. 关键状态变化的视觉反馈规格

**状态 DOCKED（停靠在星图节点）**
- 星图图标：静态飞船剪影 + 阵营 Emission 颜色；尺寸 24dp × 24dp，节点图标上方偏移 8dp
- Emission 做 1Hz 慢速呼吸脉冲（亮度 ±20%），区分于节点本身的静态光点
- *归属：星图 UI GDD*

**状态 IN_TRANSIT（星图上飞行中）**
- 飞船图标沿路径 Edge 线性移动，朝向跟随运动方向旋转
- 引擎尾焰粒子：4–6 个屏幕空间粒子，阵营颜色，持续跟随图标；Particle Count ≤ 8/艘
- *归属：星图 UI GDD*

**状态切入驾驶舱（视角过渡）**
- 0.4 秒推进动画：相机从俯视视角推向飞船，伴随 URP Motion Blur（Intensity = 0.6）
- 飞船以 Emission 纯色剪影呈现，0.4 秒后渐入完整 Lit 材质
- 驾驶舱背景为全黑深空 + 星点粒子层，飞船是唯一发光实体
- *归属：双视角切换系统 GDD（动画逻辑）；飞船材质规格定义于本章*

**状态 DESTROYED（飞船被摧毁）**

3 阶段效果，总时长 1.2 秒：

| 阶段 | 时机 | 内容 |
|------|------|------|
| **Flash** | 第 0 帧 | 全屏 Emission 白色闪光，持续 2 帧（Bloom Intensity 峰值） |
| **Breakup** | 0–0.5 秒 | 飞船几何体拆分为 4–6 个碎片向外散射；碎片保留 Emission（橙色渐变）|
| **Dissipate** | 0.5–1.2 秒 | 碎片 Alpha Fade 消失；残留橙色烟雾粒子 8–12 个，生命周期 1.5 秒 |

性能约束：碎片数 ≤ 6 个 MeshRenderer；粒子 ≤ 16 个；不触发额外 Draw Call。

**状态 IN_COMBAT（进入战斗）**
- 引擎 Emission 强度提升 40%；武器硬点指示灯切换为 3Hz 快速闪烁
- 驾驶舱屏幕边缘红色警戒光晕（URP Vignette，颜色 `#FF2200`，Intensity 0.3）
- 战斗开始帧：镜头轻微震动（Camera Shake，Amplitude 0.05，Duration 0.3 秒）
- *归属：屏幕光晕归飞船 HUD GDD；相机震动归飞船战斗系统 GDD*

**建造完成（飞船出现在星图）**
- 飞船图标从 0 Scale 弹出（0.3 秒 Ease-Out Bounce 动画）+ 冷蓝色粒子爆散 12 个
- 节点光环短暂扩张（Scale × 1.4，0.3 秒后回弹）
- *归属：星图 UI GDD；本 GDD 定义触发时机（BUILDING → DOCKED 事件）*

### 3. 音效事件列表

> 飞船系统负责触发以下事件；具体音效文件、混音参数由音频系统 GDD 定义。

| 事件 ID | 触发条件 | 播放方式 | 优先级 |
|---------|---------|---------|--------|
| `SFX_SHIP_BUILD_COMPLETE` | BUILDING → DOCKED | 一次性，2D | 中 |
| `SFX_SHIP_ENGINE_IDLE` | 进入 DOCKED（仅驾驶舱层） | 循环，3D | 低 |
| `SFX_SHIP_ENGINE_THRUST` | 进入 IN_TRANSIT | 循环，2D（低音量）| 中 |
| `SFX_COCKPIT_ENTER` | 视角切换开始 | 一次性，2D | 高 |
| `SFX_COMBAT_ALERT` | 进入 IN_COMBAT | 一次性，2D | 高 |
| `SFX_SHIP_DESTROYED` | 进入 DESTROYED | 一次性，2D | 最高 |
| `SFX_WEAPON_FIRE` | 武器开火（每次） | 一次性，3D | 中 |

### 4. 归属边界

| 视觉/音效内容 | 飞船系统定义 | 归属方 |
|-------------|:-----------:|--------|
| 飞船几何体形态规格（面数、分层、方向可读性） | ✅ | — |
| 飞船阵营色彩编码（Emission 颜色规格） | ✅ | — |
| DESTROYED 爆炸效果的阶段规格和性能预算 | ✅ | — |
| 切入驾驶舱时飞船材质切换规格 | ✅ | — |
| 飞船状态机音效触发事件列表 | ✅ | — |
| 星图上 DOCKED/IN_TRANSIT/建造完成动画 | ❌ | 星图 UI GDD |
| 星图→驾驶舱视角推进动画（相机逻辑） | ❌ | 双视角切换系统 GDD |
| IN_COMBAT 屏幕边缘红色警戒光晕 | ❌ | 飞船 HUD GDD |
| 战斗开始相机震动 | ❌ | 飞船战斗系统 GDD |
| 音效文件资产、混音参数、AudioMixer 配置 | ❌ | 音频系统 GDD |
| 武器开火的弹道/激光视效 | ❌ | 飞船战斗系统 GDD |

📌 **Asset Spec** — Visual/Audio 需求已定义。艺术圣经批准后，运行 `/asset-spec system:ship-system` 生成每个资产的视觉描述、尺寸规格和生成提示词。

## UI Requirements

飞船系统是纯数据模型层，本身不负责任何 UI 渲染。玩家看到的飞船相关界面由以下系统负责：

| UI 元素 | 负责系统 |
|---------|---------|
| 星图上的飞船图标（停靠/移动中）| 星图 UI GDD |
| 飞船状态信息（生命值条、战斗状态指示）| 飞船 HUD GDD |
| 建造飞船的触发按钮和确认对话 | 殖民地系统 UI / 星图 UI GDD |
| 资源不足时的拒绝提示 | 经济 UI GDD |
| 驾驶舱视角的抬头显示（HUD）| 飞船 HUD GDD |

> 飞船系统通过状态变更事件（`SetState`、`DestroyShip`）通知 UI 层更新显示，不直接持有或操作任何 UI 组件。

## Acceptance Criteria

**AC-SHIP-01：蓝图建造条件验证**
GIVEN 玩家拥有一个 PLAYER 节点且持有 ≥30矿、≥15能源，节点无驻扎舰队，WHEN 玩家在该节点发起飞船建造指令，THEN 建造成功，矿减少30、能源减少15，节点出现一艘新飞船实例（ShipState = DOCKED）。

**AC-SHIP-02：建造条件不足时拒绝**
GIVEN 玩家持有 20矿、10能源（均不足），WHEN 玩家尝试在 PLAYER 节点建造飞船，THEN 建造被拒绝，资源数值不变，不生成飞船实例，界面显示资源不足提示。

**AC-SHIP-03：节点有驻扎舰队时拒绝建造**
GIVEN 一个 PLAYER 节点当前已有飞船驻扎（DOCKED 状态），WHEN 玩家再次对该节点发起建造指令，THEN 建造被拒绝，已有飞船不受影响，不生成新飞船实例。

**AC-SHIP-04：实例属性通过蓝图引用获取**
GIVEN 存在一艘飞船实例（BlueprintId = "generic_v1"），WHEN 系统查询该实例的 MaxHull、ThrustPower、TurnSpeed，THEN 返回值与对应蓝图记录完全一致，实例自身不存储这三项数值的副本。

**AC-SHIP-05：is_valid_ship_instance — 合法蓝图通过**
GIVEN 一条蓝图记录：MaxHull=100、ThrustPower=50、TurnSpeed=30、WeaponSlots=1，WHEN 执行 is_valid_ship_instance 校验，THEN 返回 true，该蓝图可被用于建造。

**AC-SHIP-06：is_valid_ship_instance — 非法蓝图拒绝**
GIVEN 一条蓝图记录：MaxHull=0、ThrustPower=50、TurnSpeed=30、WeaponSlots=1（MaxHull 违反 >0 约束），WHEN 执行 is_valid_ship_instance 校验，THEN 返回 false，系统拒绝以此蓝图创建任何实例。

**AC-SHIP-07：IN_COCKPIT 互斥约束**
GIVEN 玩家已有一艘飞船处于 IN_COCKPIT 状态，WHEN 玩家尝试对另一艘飞船发出"进入驾驶舱"指令，THEN 第二艘飞船维持原状态不变，系统拒绝该指令，全局任何时刻 IN_COCKPIT 状态的飞船数量不超过 1。

**AC-SHIP-08：DESTROYED 飞船拒绝所有指令**
GIVEN 一艘飞船当前状态为 DESTROYED，WHEN 对该飞船发出任意指令（移动、攻击、进入驾驶舱等），THEN 所有指令均被拒绝，飞船状态保持 DESTROYED，不执行任何动作。

**AC-SHIP-09：建造中节点沦陷——资源不返还**
GIVEN 一艘飞船正在 PLAYER 节点建造中（已扣除30矿/15能源），WHEN 该节点在建造完成前被敌方占领，THEN 建造取消，已扣除的资源**不返还**，玩家资源保持扣除后的状态。

**AC-SHIP-10：节点直接沦陷——飞船先销毁再更新节点**
GIVEN 一艘飞船 DOCKED 于某 PLAYER 节点，WHEN 敌方触发节点直接沦陷（无战斗），THEN 飞船状态先变为 DESTROYED，然后节点所有权更新为敌方（飞船销毁事件早于节点归属变更）。

**AC-SHIP-11：战斗中节点沦陷——节点沦陷优先**
GIVEN 一艘飞船处于 IN_COMBAT 状态，其驻扎节点同时被敌方占领，WHEN 节点沦陷事件触发，THEN 节点沦陷优先处理，飞船直接切换至 DESTROYED，不触发正常战斗死亡结算流程。

**AC-SHIP-12：IsPlayerControlled 标记不可变**
GIVEN 飞船创建时 IsPlayerControlled = true，WHEN 飞船经历任意状态转换（DOCKED → IN_TRANSIT → IN_COMBAT → DESTROYED），THEN IsPlayerControlled 始终保持创建时指定的值，任何系统均无法在运行时修改该字段。

## Open Questions

| # | 问题 | 影响范围 | 负责人 | 目标解决时间 |
|---|------|---------|--------|------------|
| Q-1 | `SHIP_MAX_HULL`、`SHIP_THRUST_POWER`、`SHIP_TURN_SPEED` 的具体数值是多少？ | 飞船生命值系统、飞船操控系统、Acceptance Criteria AC-SHIP-05 | game-designer + 原型测试 | `/prototype 飞船驾驶舱操控` 完成后 |
| Q-2 | 无人值守战斗（玩家飞船抵达 ENEMY 节点但玩家未在驾驶舱）的解算规则是什么？（自动胜利？固定胜负比？ | 飞船战斗系统 GDD | game-designer | 飞船战斗系统 GDD 设计时 |
| Q-3 | 飞船被摧毁后是否有"残骸"状态，或者直接彻底移除？ | 星图 UI、敌人系统（掠夺机制？） | game-designer | Vertical Slice 阶段前 |
| Q-4 | 未来是否引入飞船维修机制？如有，修复成本和时间如何计算，是否在飞船系统 GDD 中定义？ | 飞船生命值系统、资源系统 | game-designer | Vertical Slice 设计阶段 |
| Q-5 | 玩家在驾驶舱（`IN_COCKPIT`）时，其他飞船的无人值守战斗（`IN_COMBAT`）失败后，玩家是否收到通知，以什么形式？ | 飞船 HUD GDD、星图 UI GDD | ux-designer | 飞船 HUD GDD 设计时 |
| Q-6 | 战斗机建造地点：必须停靠有航母的节点，还是任意 ShipyardTier≥1 的己方节点？（影响建筑系统 Q-6） | 建筑系统、殖民地系统 | game-designer | Vertical Slice 设计前 |
| Q-7 | 无主战斗机（航母摧毁后独立存续）的长期归属：可以手动分配给新航母，还是永久独立存续？ | 舰队调度系统、星图 UI | game-designer | Vertical Slice 设计阶段 |
| Q-8 | `fighter_v1` 的 `SHIP_THRUST_POWER` 和 `SHIP_TURN_SPEED` 与 `generic_v1` 需要独立原型测试，还是可以从 `generic_v1` 推导（如 ×2.0 / ×2.5）？ | 飞船操控系统原型 | game-designer + 原型测试 | `/prototype 飞船驾驶舱操控` 完成后 |
