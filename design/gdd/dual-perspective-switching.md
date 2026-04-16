# 双视角切换系统 (Dual-Perspective Switching System)

> **Status**: In Design
> **Author**: Game Designer + Unity Specialist
> **Last Updated**: 2026-04-14
> **Implements Pillar**: 支柱 4 — 从星图到驾驶舱（任意时刻无缝切换，同一存档中两层体验共存）
> **Design Reference**: X4: Foundations 风格视角切换，移动端适配版

## Overview

双视角切换系统是星链霸权的核心架构整合点。玩家始终以一名驾驶员的身份存在于宇宙中，绑定一艘「当前座驾（ActiveShip）」。系统提供三种交互维度：

1. **星图 ↔ 驾驶舱全切换**：从星图全局视角完整进入驾驶舱操控层，或从驾驶舱返回星图
2. **驾驶舱内星图叠加层**：在不退出驾驶舱的情况下，叠加显示星图进行战略查阅或发布指令
3. **直接跳船（飞船间传送）**：在叠加星图内选中另一艘己方飞船，立即传送至其驾驶舱，无需经过星图视图中转

时间压缩控制（SimClock）贯穿两个视图，让玩家可以随时暂停或加速策略层时间。

驾驶舱物理在任何切换操作期间始终以真实时间运行——这是设计的有意选择：进入驾驶舱的代价是你的注意力必须在这里，你无法在飞行时暂停飞船物理。

## Player Fantasy

**将军亲征 × 始终在场**

你是一名驾驶员，同时也是一个帝国的缔造者。你的身体在某艘飞船里——星图是你的指挥台，你可以随时展开它，但你从未离开你的船。

**展开星图的时刻：** 战斗正在进行，你右手操控飞船规避，左手把星图叠加层划开——不是为了逃离战场，而是因为你需要同时知道左翼舰队在哪里。这是 X4 式的「高度」：你可以同时活在两个信息层里，但你仍然在船上，引擎仍然在震动。

**传送到另一艘船的时刻：** 旗舰陷入困境，你判断巡洋舰的位置更具战略价值。叠加星图里，你点击那艘船——不是在玩弄棋子，而是你这个人正在从一艘船传送到另一艘船。传送完成的瞬间，新驾驶舱的视野涌入，你感受到的是「我现在在这里了」，而不是「摄像机切换了」。

**返回星图的时刻：** 有时候你需要完整地鸟瞰全局——不是叠加层的局部预览，而是铺开整张星图，让所有信息同时可见。这时候你才「离开」飞船，进入纯粹的战略视角。返回时，ActiveShip 仍然是那艘船，你的位置没有丢失。

*此系统直接服务于支柱 4（从星图到驾驶舱），通过「始终在场」的设计强化支柱 3（张力时刻）的情感密度。*

## Detailed Design

### Core Concepts

**ActiveShip（当前座驾）**
玩家在任何时刻都绑定一艘 ActiveShip。进入驾驶舱操作时，ActiveShip 即当前驾驶的飞船。在星图视图中，ActiveShip 在星图上有特殊高亮标识（区别于其他己方飞船）。存档时保存 ActiveShipId。

**SimClock（模拟时钟）**
策略层（舰队移动、资源产出、殖民地计时）以 SimClock 驱动，而非 Unity 的 `Time.deltaTime`。SimClock 维护 `SimRate` 倍率（0=暂停, 1×, 5×, 20×）。驾驶舱物理永远使用真实时间（`Time.deltaTime`），不受 SimRate 影响。

### Core Rules

**场景架构（Unity 6.3 双场景 Additive）**

| 场景 | 职责 | 生命周期 |
|------|------|----------|
| MasterScene | GameManager、SimClock、持久化数据模型、SO 事件总线 | 游戏全程常驻 |
| StarMapScene | 策略摄像机、星图 GameObject 层级、策略层 Update 循环 | 游戏开始时加载，始终保持加载状态 |
| CockpitScene | 飞船物理、驾驶舱摄像机、飞船 HUD | 进入驾驶舱时加载；返回星图或跳船时卸载 |

**规则 S-1（策略层时间驱动）：** StarMapScene 的所有策略系统以 `SimClock.DeltaTime`（`= Time.unscaledDeltaTime × SimRate`）驱动。当 `SimRate = 0` 时策略层暂停；CockpitScene 内的飞船物理始终使用 `Time.deltaTime`，不受 SimRate 影响。

**规则 S-2（切换锁定）：** 系统维护布尔标志 `_isSwitching`。切换进行中时，所有切换请求（进入/返回/跳船）静默丢弃；切换完成后解锁。

**规则 S-3（摄像机管理）：** 切换时通过 `Camera.enabled = false/true` 停用/启用摄像机，而非 SetActive；避免 URP 执行无效 Culling Pass。

**规则 S-4（UI 层切换）：** UI Toolkit 面板用 `style.display = DisplayStyle.None/Flex`；UGUI Canvas 用 `Canvas.enabled`。

**规则 S-5（幂等性）：** 点击已在驾驶舱的同一飞船的「传送」→ 静默忽略。

**规则 S-6（IN_COMBAT 保护）：** ActiveShip 的 ShipState == IN_COMBAT 时，「返回星图」按钮保持可用（玩家可以在战斗中撤到星图）；但「传送到其他飞船」按钮置灰——战斗中不可换船。其他己方飞船的叠加层传送按钮在目标船 ShipState == IN_COMBAT 时置灰。

**规则 S-7（ActiveShip 绑定）：** 玩家始终拥有 `ActiveShipId`（存档持久化）。进入驾驶舱 = 将 ActiveShipId 设为目标飞船 id；返回星图不清空 ActiveShipId（玩家仍然「在」那艘船上，只是切换到了鸟瞰视角）。

**规则 S-8（星图叠加层）：** ViewLayer == COCKPIT 时，玩家可触发叠加层（OPENING_OVERLAY）。叠加层打开期间：StarMapScene UI 以 UI Toolkit ScreenOverlay 渲染（不依赖 Camera A）；驾驶舱摄像机和物理继续运行；`_isSwitching` 保持 false（叠加层不是切换，不锁定输入）；驾驶舱触摸输入路由至叠加层（叠加层关闭前不处理飞船操控）。

**规则 S-9（直接跳船）：** 在叠加星图内选中另一艘己方飞船后触发「立即传送」，进入 SWITCHING_SHIP 序列。全程 `_isSwitching = true`，不经过 `ViewLayer = STARMAP` 中间状态。序列完成后 ActiveShipId 更新为目标飞船。

**规则 S-10（时间压缩控件）：** SimClock 的 SimRate 可在两个视图（星图和驾驶舱）中随时调整。驾驶舱内调整 SimRate 不触发任何切换流程。SimRate 变更立即生效；存档时保存当前 SimRate。

**切换前提条件**

| 切换方向 | 必须满足的条件 |
|----------|--------------|
| 星图 → 驾驶舱 | `!_isSwitching` AND 目标飞船 ShipState ∈ {DOCKED, IN_TRANSIT} AND 目标飞船 ShipState ≠ DESTROYED |
| 驾驶舱 → 星图 | `!_isSwitching` AND ViewLayer == COCKPIT |
| 打开叠加层 | `!_isSwitching` AND ViewLayer == COCKPIT |
| 关闭叠加层 | ViewLayer == COCKPIT_WITH_OVERLAY |
| 直接跳船（叠加层内） | `!_isSwitching` AND ViewLayer == COCKPIT_WITH_OVERLAY AND 目标飞船 ≠ ActiveShip AND 目标飞船 ShipState ∈ {DOCKED, IN_TRANSIT} AND ActiveShip.ShipState ∉ {IN_COMBAT} |
| 任何方向 | 目标飞船 ShipState ≠ DESTROYED（按钮置灰拦截）|

---

### States and Transitions

**ViewLayer 状态机**

```
STARMAP
  ├─→ SWITCHING_IN ──────────────────────────────► COCKPIT
  │                                                    │
  │   ◄─ SWITCHING_OUT ◄─────────────────────────────┤
  │                                                    │
  │                                             OPENING_OVERLAY
  │                                                    │
  │                                          COCKPIT_WITH_OVERLAY
  │                                            │          │
  │                                   CLOSING_OVERLAY   SWITCHING_SHIP
  │                                            │          │
  │                                          COCKPIT ◄────┘
  │                                            │
  └────────────────────── SWITCHING_OUT ◄──────┘
```

**进入驾驶舱序列（SWITCHING_IN，10 步）**

1. `_isSwitching = true`；禁用切换按钮
2. 缓存 `_preEnterState` = 目标飞船当前 ShipState
3. `ActiveShipId` = 目标飞船 id
4. 全屏遮罩渐入（300ms）；星图 UI 交互区域立即禁用
5. CockpitScene 异步加载（`allowSceneActivation = false`）
6. 目标飞船 ShipState → `IN_COCKPIT`
7. 飞船当前数据（Hull、位置、旋转）写入 CockpitScene 飞船物理对象
8. `_cockpitLoad.allowSceneActivation = true`；等待激活完成（progress ≥ 0.9f）
9. ViewLayer → `COCKPIT`；广播 `OnViewLayerChanged(COCKPIT)`
10. 星图摄像机 `enabled = false`；驾驶舱摄像机 `enabled = true`；全屏遮罩渐出；`_isSwitching = false`

**返回星图序列（SWITCHING_OUT，9 步）**

1. `_isSwitching = true`；禁用切换按钮
2. 全屏遮罩渐入（300ms）；飞船 HUD 交互区域立即禁用
3. 飞船最终状态（Hull、位置）从 CockpitScene 回写至 MasterScene ShipDataModel
4. ActiveShip ShipState → `_preEnterState`（恢复进入前状态）
5. ViewLayer → `STARMAP`；广播 `OnViewLayerChanged(STARMAP)`
6. 驾驶舱摄像机 `enabled = false`；星图摄像机 `enabled = true`
7. 飞船 HUD Canvas `enabled = false`；星图 UI `style.display = Flex`
8. 星图摄像机定位至上次记录的位置/缩放（直接到位，不做动画）
9. `SceneManager.UnloadSceneAsync("CockpitScene")`；全屏遮罩渐出；`_isSwitching = false`

**打开叠加层序列（OPENING_OVERLAY，3 步）**

1. ViewLayer → `COCKPIT_WITH_OVERLAY`；广播 `OnViewLayerChanged(COCKPIT_WITH_OVERLAY)`
2. StarMapScene UI 切换至叠加渲染模式（ScreenOverlay）；叠加层面板从右侧滑入（300ms）
3. 驾驶舱触摸输入路由切换至叠加层（飞船操控暂停响应）

**关闭叠加层序列（CLOSING_OVERLAY，3 步）**

1. 叠加层面板向右侧滑出（200ms）
2. StarMapScene UI 退出叠加渲染模式
3. ViewLayer → `COCKPIT`；广播 `OnViewLayerChanged(COCKPIT)`；驾驶舱触摸输入恢复

**直接跳船序列（SWITCHING_SHIP，12 步）**

1. 玩家在叠加星图内选中目标飞船并点击「立即传送」
2. `_isSwitching = true`；叠加层关闭（即时）；全屏遮罩渐入（300ms）
3. ActiveShip（旧船）最终状态回写至 ShipDataModel（Hull + 位置）
4. 旧船 ShipState → `_preEnterState`（恢复 DOCKED 或 IN_TRANSIT）
5. 缓存目标飞船当前 ShipState 为新的 `_preEnterState`
6. `ActiveShipId` = 目标飞船 id；目标飞船 ShipState → `IN_COCKPIT`
7. 目标飞船数据（Hull、位置）写入新 CockpitScene 加载参数
8. 旧 CockpitScene 异步卸载（`UnloadSceneAsync`）
9. 新 CockpitScene 异步加载（`allowSceneActivation = false`）
10. 等待旧场景卸载完成 AND 新场景 progress ≥ 0.9f
11. `allowSceneActivation = true`；等待激活完成
12. ViewLayer 保持 `COCKPIT`；广播 `OnActiveShipChanged(newShipId)`；全屏遮罩渐出；`_isSwitching = false`

---

### Interactions with Other Systems

（与 v1 相同，新增以下）

**与 SimClock（新系统）**
- 流入：时间控制控件调用 `SimClock.SetRate(float rate)`
- 流出：策略层所有系统订阅 `SimClock.DeltaTime` 替代 `Time.deltaTime`
- 接口：SimClock 是 MasterScene 持有的单例，不依赖任何视角状态

**与星图叠加层（新交互）**
- 叠加层打开时，星图 UI 系统切换为叠加渲染模式（ScreenOverlay）
- 叠加层内的飞船选中操作触发 `ViewLayerManager.RequestSwitchShip(shipId)` 而非原有的 `RequestEnterCockpit`
- 驾驶舱 ShipControlSystem 在 ViewLayer == COCKPIT_WITH_OVERLAY 时暂停输入响应（但物理继续运行，飞船保持惯性）

## Formulas

### D-DVS-1：切换允许判定（更新版）

**进入驾驶舱 / 直接跳船：**
```
can_enter(ship) =
  NOT _isSwitching
  AND ship.State ∈ {DOCKED, IN_TRANSIT}
  AND ship.State ≠ DESTROYED
  AND (for SWITCHING_SHIP: ship.id ≠ ActiveShipId AND ActiveShip.State ∉ {IN_COMBAT})
```

**返回星图：**
```
can_return =
  NOT _isSwitching
  AND ViewLayer ∈ {COCKPIT, COCKPIT_WITH_OVERLAY}
```

**打开叠加层：**
```
can_open_overlay =
  NOT _isSwitching
  AND ViewLayer == COCKPIT
```

### D-DVS-2：切换时间预算（不变）

同 v1，所有时间以 `Time.unscaledTime` 计（不受 SimRate 影响）。

### D-DVS-3：回写校验（不变）

同 v1。适用于 SWITCHING_OUT 和 SWITCHING_SHIP 序列的步骤 3。

### D-DVS-4：SimClock DeltaTime

```
SimClock.DeltaTime = Time.unscaledDeltaTime × SimRate
  where SimRate ∈ {0, 1, 5, 20}
```

策略层系统必须用此公式替代 `Time.deltaTime`。驾驶舱物理系统禁止使用此公式。

## Edge Cases

**E-1（切换中飞船被摧毁）**：同 v1，切换强制中断，ViewLayer 回落至 STARMAP。SWITCHING_SHIP 中若目标飞船被摧毁，序列中止，ActiveShip 恢复为旧船（旧船数据已在步骤 3 回写）。

**E-2（回写失败 Fallback）**：同 v1，Cockpit 数据优先。

**E-3（IN_COMBAT 时的保护规则更新）**：ActiveShip 处于 IN_COMBAT 时：
- 「返回星图」**可用**（玩家可撤到星图观察全局）
- 「直接跳船」**禁用**（战斗中不可换船）
- 叠加层**可打开**（战斗中可查看全局，但只能发指令，不能传送）

**E-4（切换锁定期间重复请求）**：同 v1，静默丢弃。

**E-5（幂等性处理）**：同 v1，跳船目标 == ActiveShip 时静默忽略。

**E-6（直接跳船并发保护）[翻转]**：`_isSwitching = true` 期间，叠加层内的「立即传送」按钮置灰不可点击。叠加层本身不关闭，玩家可以等待切换完成后重试。

**E-7（跳船中旧船被摧毁）**：SWITCHING_SHIP 序列步骤 3 回写完成后，旧船 ShipState 已恢复为 IN_TRANSIT/DOCKED。若旧船在此之后被摧毁，属正常战斗结算，不影响跳船序列（新船加载继续）。

**E-8（叠加层打开时收到战斗触发）**：叠加层打开期间，如果 ActiveShip 进入 IN_COMBAT：战斗正常触发，飞船物理继续（驾驶舱仍在运行）；叠加层顶部显示战斗警告 Banner；「直接跳船」按钮即时置灰。玩家可关闭叠加层后恢复手动驾驶。

**E-9（SimRate > 1 时进入驾驶舱）**：进入驾驶舱时 SimRate 不自动重置。玩家需手动将 SimRate 降回 1× 以正常驾驶。推荐在进入驾驶舱时显示提示：「策略层当前以 5× 速度运行」。

## Dependencies

（与 v1 相同，新增以下）

**SimClock（新依赖）**
- 所有策略系统（ColonySystem、FleetDispatch、ResourceSystem）必须迁移至 `SimClock.DeltaTime`
- ViewLayerManager 不拥有 SimClock，SimClock 是独立系统

**叠加渲染系统（新依赖，见 ADR-0004）**
- StarMapScene UI 需要支持双模式渲染：Camera-based（正常星图视图）和 ScreenOverlay（叠加层模式）
- 模式切换由 `OnViewLayerChanged` 事件触发

## Tuning Knobs

| 旋钮名 | 默认值 | 安全范围 | 说明 |
|--------|--------|----------|------|
| `SWITCH_TIME_LIMIT` | 1.0s | 0.5s – 3.0s | 同 v1 |
| `COCKPIT_PRELOAD_TRIGGER` | ON_SHIP_SELECT | {ON_SHIP_SELECT, ON_ENTER_BUTTON, DISABLED} | 同 v1 |
| `SIM_RATE_OPTIONS` | [0, 1, 5, 20] | 任意正整数数组 | 时间压缩倍率选项 |
| `OVERLAY_OPACITY` | 0.85 | 0.6 – 1.0 | 叠加星图面板不透明度 |
| `OVERLAY_COVER_RATIO` | 0.70 | 0.5 – 0.85 | 叠加层占屏幕高度比例 |
| `SWITCH_LOCK_VISUAL_FEEDBACK` | true | {true, false} | 同 v1 |
| `SYNC_FALLBACK_AUTHORITY` | COCKPIT | {COCKPIT, MASTER} | 同 v1 |

## Visual/Audio Requirements

### 视觉需求

**V-1（切换遮罩）**：同 v1，纯黑渐变。适用于 SWITCHING_IN、SWITCHING_OUT、SWITCHING_SHIP。

**V-2（切换锁定反馈）**：同 v1。

**V-3（返回星图后摄像机定位）**：同 v1，直接到位。

**V-4（星图叠加层）**：半透明面板（OVERLAY_OPACITY），从屏幕右侧滑入。面板内容为星图的缩略可交互版本，支持滚动和双指缩放。ActiveShip 在叠加层内有特殊标识（脉冲光环）。

**V-5（ActiveShip 在星图中的标识）**：星图视图和叠加层中，ActiveShip 节点旁显示驾驶员图标（⚓ 或类似），与其他己方飞船区分。

**V-6（时间压缩状态指示）**：两个视图均在角落显示当前 SimRate（暂停显示 ⏸，其他显示 Nx）。SimRate > 1 时轻微黄色边框提示玩家策略层加速中。

### 音效需求

**A-1（切换音效）**：同 v1。
**A-2（拒绝音效）**：同 v1。
**A-3（叠加层开/关音效）**：轻微「展开」音效（约 0.15s），区别于全切换音效。
**A-4（直接跳船音效）**：比普通切换更具传送感的音效（推荐「量子传送」类短促声效）。
**A-5（时间压缩切换音效）**：SimRate 变更时播放短促滴声（区分暂停/恢复/加速三种）。

## UI Requirements

**UI-1（进入驾驶舱按钮）**：同 v1，位于星图飞船选中面板。

**UI-2（返回星图按钮）**：同 v1，位于驾驶舱 HUD 左上角。

**UI-3（切换遮罩层级）**：同 v1，最高 Sort Order。

**UI-4（触控热区）**：同 v1，最小 48×48dp。

**UI-5（星图叠加层按钮）**：
- 位置：驾驶舱 HUD 右侧固定区域
- 图标：星图/雷达图标
- 可交互条件：ViewLayer == COCKPIT AND !_isSwitching
- 最小热区：48×48dp

**UI-6（时间压缩控件）**：
- 位置：星图右上角 / 驾驶舱右上角（两个视图均有）
- 显示：当前 SimRate 标签 + 快速切换按钮（⏸ / 1× / 5× / 20×）
- 最小热区：每个按钮 44×44dp，总控件区域 ≥ 180dp 宽
- 可交互条件：无限制（任何视图、任何时候均可调整）

**UI-7（直接跳船按钮）**：
- 位置：叠加层内，选中目标飞船后的确认卡片
- 文本：「立即传送」
- 可交互条件：目标飞船 ≠ ActiveShip AND can_enter(targetShip) = true AND ActiveShip.State ∉ {IN_COMBAT}
- 置灰条件：IN_COMBAT 或 _isSwitching 或目标不可进入
- 最小热区：全宽 × 48dp

## Acceptance Criteria

（保留 v1 全部 AC-DVS-01 ~ 15，补充以下）

**AC-DVS-16：叠加层打开 — 驾驶舱继续运行**
前提：ViewLayer = COCKPIT，飞船在飞行中。
操作：打开叠加层。
预期：ViewLayer → COCKPIT_WITH_OVERLAY；叠加层面板可见；飞船物理继续运行（惯性不停止）；驾驶舱渲染不中断；`_isSwitching` 保持 false。

**AC-DVS-17：叠加层关闭 — 飞船操控恢复**
前提：ViewLayer = COCKPIT_WITH_OVERLAY。
操作：点击叠加层关闭按钮。
预期：ViewLayer → COCKPIT；叠加层面板消失；驾驶舱触摸输入恢复响应飞船操控。

**AC-DVS-18：直接跳船 — 正常路径**
前提：ViewLayer = COCKPIT_WITH_OVERLAY，目标飞船 ShipState = IN_TRANSIT，ActiveShip ≠ IN_COMBAT。
操作：点击「立即传送」。
预期：旧 ActiveShip ShipState 恢复 _preEnterState；新 ActiveShipId = 目标飞船；新飞船 ShipState → IN_COCKPIT；全程耗时 ≤ SWITCH_TIME_LIMIT；ViewLayer 始终为 COCKPIT（不经过 STARMAP）。

**AC-DVS-19：直接跳船 — IN_COMBAT 拒绝**
前提：ActiveShip.ShipState = IN_COMBAT，叠加层打开。
操作：点击另一艘飞船的「立即传送」按钮。
预期：按钮为 Disabled 状态，不可点击，显示提示「战斗中无法换船」；切换不执行。

**AC-DVS-20：ActiveShip 在星图中有特殊标识**
前提：进入星图视图。
操作：查看星图。
预期：ActiveShip 所在节点旁有驾驶员图标，区别于其他己方飞船；返回星图后标识保持（ActiveShipId 未清空）。

**AC-DVS-21：时间压缩 — SimRate 切换生效**
前提：存在舰队正在航行（FleetState = IN_TRANSIT）。
操作：将 SimRate 调至 5×，等待 1 秒真实时间后调回 1×。
预期：1 秒内舰队移动距离约等于 5 秒常速的移动距离（误差 ≤ 1 帧）；驾驶舱物理（如正在驾驶）速度不变。

**AC-DVS-22：时间压缩 — 暂停行为**
前提：SimRate = 1×，有资源正在产出。
操作：将 SimRate 设为 0（暂停）。
预期：策略层时间停止（资源不增加，舰队不移动）；驾驶舱飞船物理继续（如在驾驶则飞船继续响应操控）。

## Open Questions

**Q-1（过渡动画内容）**：MVP 纯黑渐变。Vertical Slice 是否需要叙事性切换特效？待创意总监确认。

**Q-2（预加载触发时机）**：同 v1，待原型验证后确认。

**Q-3（直接跳船）**：✅ 已决策 — 支持，经由叠加层触发 SWITCHING_SHIP 序列。

**Q-4（驾驶舱摄像机初始位置）**：每次进入（含跳船）重置到默认位置。详见 ship-control-system.md。

**Q-5（ship-hud.md Q-7）**：✅ 已解答（见 v1）。

**Q-6（叠加层与战斗 HUD 的 Z 层级）**：叠加层打开时，战斗 HUD 元素（血条、准星）是否仍然可见？建议：保留，叠加层 Z 层级在游戏 UI 之下但在驾驶舱场景之上。待 Vertical Slice 阶段真机测试确认视觉清晰度。

**Q-7（SimRate 存档）**：建议进入新存档时 SimRate 默认为 1×，不恢复上次设置；待产品设计确认。
