# 星图 UI (Star Map UI)

> **Status**: In Design
> **Author**: game-designer (Claude Code) + user
> **Last Updated**: 2026-04-12
> **Implements Pillar**: 支柱2 一目了然的指挥 / 支柱3 我的星际帝国

## Overview

星图 UI（Star Map UI）是《星链霸权》战略层的视觉与交互界面：它将星图系统的节点图数据渲染为玩家可见的星域地图，并接收玩家的触屏操作（点击节点、派遣舰队、查看信息），将意图转化为对舰队调度系统和殖民地系统的调用。从数据层看，它是一个只读渲染层加上一个触屏输入路由层——它不拥有任何游戏状态，只负责把状态变成像素，把手指变成指令。从玩家感受看，它是「帝国的全貌」：你在这里看到自己打下的每一个节点、正在行军的每一艘飞船、还没探索的每一片黑暗——然后你在这里做出下一个决定。MVP 阶段星图 UI 覆盖以下核心功能：节点渲染（颜色/类型/归属）、连线渲染、飞船图标（DOCKED/IN_TRANSIT 状态）、行军路径预览线、派遣确认卡、取消按钮、战争迷雾遮罩（UNEXPLORED 节点不可见）。资源存量显示（矿石/能源）在 MVP 阶段以最小化形式呈现（角落数字），完整经济 UI 延至 Vertical Slice。

## Player Fantasy

星图 UI 的玩家幻想是**三重身份的同时在场**——你是俯视全局的指挥官，是亲手建造帝国的缔造者，也是在棋盘上算三步棋的猎手。这三种身份不是分开的，它们在同一个界面上同时共振。

**作战室：掌控感**

你打开星图，第一眼看到的是颜色分布——蓝色集群、灰色边境、橙红色威胁点。你不需要读任何数字，颜色本身就是战情报告。飞船图标在路径线上缓缓移动，那是你的命令正在被执行。资源数字在角落微微跳动，那是你的帝国在为你工作。这个界面是你的作战室：你站在这里，你看到一切，你做出决定。

**星际画布：缔造者骄傲**

每一个蓝色节点都是你的笔触。你打开星图，看到的不是「游戏状态」，而是你过去二十分钟的决策记录——哪条路是你先走的，哪个节点是你亲手打下来的，哪片区域还是灰色是因为你还没有去。帝国的轮廓是你画的，不是程序分配的。战争迷雾的边界是你的知识边界，不是随机生成的障碍。

**态势图：猎手算计**

星图 UI 是一张实时决策仪器。飞船图标的位置告诉你时序，节点颜色告诉你控制，战争迷雾告诉你风险，派遣确认卡告诉你成本。你在这里运行的不是一个操作，而是一个计算：「我的飞船 4.5 秒后到达 C 节点，C 节点变蓝后 E 节点（敌方）就暴露了，那场仗——我要亲自去飞。」星图是你决定「这场战斗值得我跳进驾驶舱」的地方。这是整个游戏最独特的时刻，而它发生在这里。

**三个锚点时刻**

- *第一次缩放全图*：你打开星图，捏合缩小，看到整张地图。你的蓝色集群在左下角，灰色节点向右延伸，最远处有一个橙红色的光点。你不需要任何教程告诉你该做什么——颜色已经告诉你了。
- *行军线亮起的瞬间*：你点击飞船，点击目标节点，确认。蓝色路径线从飞船图标延伸到目标节点，飞船开始移动。你转身去做别的事——但你知道那条线在那里，你的意志正在被执行。
- *决定跳进驾驶舱的那一刻*：你看到飞船快到一个橙红色节点了。你算了一下：这场仗用自动结算也能赢，但那个节点是关键节点，你想亲自去。你点击飞船，点击「进入驾驶舱」。星图消失，驾驶舱出现。这个决定是在星图 UI 上做的——它是整个游戏体验的枢纽。

**支柱对齐**
- **支柱2（一目了然的指挥）**：颜色即信息，一眼读懂全局——这是星图 UI 存在的根本理由
- **支柱3（我的星际帝国）**：每个蓝色节点是永久的，地图是你帝国历史的可视化记录
- **支柱1（经济即军事）**：资源数字和节点颜色同屏共存，经济状态和军事态势一目了然
- **支柱4（从星图到驾驶舱）**：星图 UI 是「跳进驾驶舱」这个决定的发生地——它是两层体验的枢纽

## Detailed Design

### Core Rules

#### UI-R-1 节点渲染

每个 `VISIBLE` 或 `EXPLORED` 节点按以下规则渲染：

**形状与尺寸**

| nodeType | 形状 | 视觉尺寸 | 触控热区 |
|---------|------|---------|---------|
| `HOME_BASE` | 六边形 | 56dp | 64dp |
| `STANDARD` | 圆形 | 44dp | 56dp |
| `RICH` | 菱形（旋转 45°） | 48dp | 60dp |

所有触控热区 ≥ 48dp（Android 最小触控目标要求）。节点中心 = `StarNode.position`（dp 坐标）。

**颜色矩阵（ownershipState × fogState）**

| fogState | PLAYER | ENEMY | NEUTRAL |
|---------|--------|-------|---------|
| `VISIBLE` | `#2266CC`（实色） | `#FF4400`（实色） | `#888888`（实色） |
| `EXPLORED` | `#2266CC` 50% 透明 | `#FF4400` 50% 透明 | `#888888` 50% 透明 |
| `UNEXPLORED` | 不渲染 | 不渲染 | 不渲染 |

`EXPLORED` 节点显示上次已知的 `ownershipState`，颜色降透明度表示信息可能过时。节点上叠加「?」图标（12dp）明确表示信息不确定。

**节点标签**：`VISIBLE` 节点显示 `displayName`（12sp，白色，节点正下方 6dp）；`EXPLORED` 节点显示 `displayName`（12sp，`#AAAAAA`）；`UNEXPLORED` 不显示标签。

**节点类型图标**（仅 `VISIBLE` 状态）：`HOME_BASE` → 星形图标（16dp）；`RICH` → 矿石晶体图标（16dp）；`STANDARD` → 无图标。

#### UI-R-2 连接线渲染

使用 `Painter2D` API 绘制（Unity 6 UI Toolkit 矢量绘制）。只渲染两端至少一端为 `EXPLORED` 或 `VISIBLE` 的连边。

| 状态 | 颜色 | 宽度 | 线型 |
|------|------|------|------|
| 默认 | `#444466` | 1.5dp | 实线 |
| 有舰队在途 | `#4488FF` | 2dp | 实线 |
| 已走过路段 | `#4488FF` 40% 透明 | 2dp | 实线 |
| 可达路径预览 | `#4488FF` 60% 透明 | 2dp | 虚线（8dp 线段，4dp 间隔） |

连线从节点边缘开始，不穿过节点中心。

#### UI-R-3 舰队图标渲染

**DOCKED 状态**：飞船轮廓图（24dp × 24dp，`#2266CC`），停靠节点中心偏移 (+20dp, -20dp)，静止无动画。

**IN_TRANSIT 状态**：同图标 + 外圈 `#4488FF` 脉冲光晕（周期 1.2s，透明度 0→0.6→0）。位置由 `FleetDispatchSystem.visualPosition`（F-3 插值）每帧驱动，使用 `style.left / style.top` 定位（不使用 `VisualElement.transform`，Unity 6.2 已废弃）。图标朝向 = `Vector2.SignedAngle(Vector2.up, toNode.position - fromNode.position)`。触控热区扩展为 48dp × 48dp（视觉保持 24dp）。

#### UI-R-4 触控交互规则

| 手势 | 行为 |
|------|------|
| 单指点击 | 选中飞船/节点，或确认操作 |
| 单指拖拽 | 平移星图视口 |
| 双指捏合/展开 | 缩放（范围 0.5× ~ 2.0×，clamp，不弹性回弹） |
| 点击空白区域 | 取消当前选中 |

**点击优先级**（同位置多元素）：飞船图标 > 节点 > 背景。

**不可交互元素**：`UNEXPLORED` 节点（不渲染）、`EXPLORED` 节点（不可作为派遣目标）、连接线、`IN_COMBAT` 飞船（点击显示提示，无操作）。

#### UI-R-5 驾驶舱入口（两步流程）

1. 玩家点击 DOCKED 飞船 → UI 进入 `SHIP_SELECTED` 状态，飞船图标旁显示「进入驾驶舱」悬浮按钮（24dp × 24dp，驾驶舱图标，触控热区 48dp）
2. 玩家点击「进入驾驶舱」按钮 → 调用 `CockpitSystem.EnterCockpit(shipId)`
3. 玩家点击可达节点 → 进入派遣确认流程（驾驶舱按钮消失）

两个操作（派遣 vs 驾驶舱）在同一选中状态下共存，互不冲突。

#### UI-R-6 派遣确认卡（底部抽屉式）

确认卡固定显示在屏幕底部（高度 120dp，全宽），从底部滑入（动画 0.2s）：

| 内容 | 格式 |
|------|------|
| 目的地名称 | 大字（18sp，白色） |
| 路径跳数 | 「X 跳」（14sp，灰色） |
| 预计到达时间 | 「约 Xs」（14sp，灰色，来自 F-2） |
| 确认按钮 | 蓝色（`#2266CC`），全宽 50%，高度 44dp |
| 取消按钮 | 灰色（`#666666`），全宽 50%，高度 44dp |

点击确认卡外部区域（星图区域）= 取消，确认卡滑出。

#### UI-R-7 资源角标（MVP 最小显示）

屏幕左上角固定显示，不随星图平移/缩放：

| 显示内容 | 格式 | 颜色 |
|---------|------|------|
| 矿石存量 | `⬡ 240` | 白色 |
| 矿石净产量 | `+N/s` | 绿色 `#44DD44`；`0/s` 灰色 |
| 能源存量 | `⚡ 18` | 白色；赤字时 `#FF4400` |

刷新：订阅 `ColonyManager.OnResourcesUpdated` 事件，不每帧轮询。

#### MVP 排除项

| 编号 | 排除内容 |
|------|---------|
| UI-EX-1 | 节点占领颜色渐变动画（即时切换） |
| UI-EX-2 | 完整经济 UI（建筑列表、建造按钮、产量详情） |
| UI-EX-3 | 节点详情面板（点击节点展开详情） |
| UI-EX-4 | 缩放时节点标签 LOD（远景隐藏标签） |
| UI-EX-5 | 多场战斗通知 UI（待 Q-2 决策后设计） |
| UI-EX-6 | 星图小地图 / 全局视图 |

### States and Transitions

#### UI 状态定义

| 状态 | 描述 | 显示内容 |
|------|------|---------|
| `IDLE` | 无选中，无确认卡 | 所有节点/连线/舰队默认渲染 |
| `SHIP_SELECTED` | 一艘 DOCKED 飞船已选中 | 选中飞船高亮 + 可达节点虚线环 + 路径预览虚线 + 「进入驾驶舱」按钮 |
| `CONFIRM_CARD` | 飞船已选中 + 目标节点已点击 | 底部确认卡 + 路径预览实线 |
| `IN_TRANSIT_SELECTED` | 一艘 IN_TRANSIT 飞船被点击 | 取消按钮面板（仅「取消派遣」） |
| `LOCKED` | 驾驶舱激活时 | 所有元素正常渲染 + 半透明遮罩（`#000000` 20%），不响应任何触控 |

#### 状态转换表

| 当前状态 | 触发条件 | 目标状态 |
|---------|---------|---------|
| `IDLE` | 点击己方 DOCKED 飞船 | `SHIP_SELECTED` |
| `IDLE` | 点击 IN_TRANSIT 飞船 | `IN_TRANSIT_SELECTED` |
| `IDLE` | 驾驶舱激活 | `LOCKED` |
| `SHIP_SELECTED` | 点击可达目标节点 | `CONFIRM_CARD` |
| `SHIP_SELECTED` | 点击空白区域 / 再次点击同一飞船 | `IDLE` |
| `SHIP_SELECTED` | 点击「进入驾驶舱」按钮 | `LOCKED`（驾驶舱激活） |
| `CONFIRM_CARD` | 点击「确认派遣」 | `IDLE`（派遣指令发出） |
| `CONFIRM_CARD` | 点击「取消」/ 点击确认卡外部 | `SHIP_SELECTED` |
| `IN_TRANSIT_SELECTED` | 点击「取消派遣」 | `IDLE`（取消指令发出） |
| `IN_TRANSIT_SELECTED` | 点击空白区域 | `IDLE` |
| `LOCKED` | 驾驶舱关闭 | `IDLE` |

> **注**：`CONFIRM_CARD` 取消后回到 `SHIP_SELECTED`（不是 `IDLE`），玩家可重新选择目标，无需重新点击飞船。

### Interactions with Other Systems

| 调用方向 | 接口 | 时机 | 说明 |
|---------|------|------|------|
| UI → 星图系统 | `StarMapData.GetAllNodes()` | 初始化时一次 | 初始渲染所有节点和连线 |
| 星图系统 → UI | `OnOwnershipChanged(nodeId, newState)` 事件 | 节点归属变更时 | 更新节点颜色（需在星图系统 GDD 中补充此事件） |
| 星图系统 → UI | `OnFogStateChanged(nodeId, newState)` 事件 | 节点探索状态变更时 | 更新节点可见性（需在星图系统 GDD 中补充此事件） |
| UI → 调度系统 | 读取 `visualPosition`（每帧） | `Update()` | IN_TRANSIT 飞船图标位置 |
| UI → 调度系统 | 读取 `LockedPath`（派遣确认后） | 订阅派遣确认事件 | 路径线渲染 |
| UI → 调度系统 | `FleetDispatchSystem.Dispatch(shipId, targetNodeId)` | 玩家点击「确认派遣」 | 发起派遣 |
| UI → 调度系统 | `FleetDispatchSystem.CancelDispatch(shipId)` | 玩家点击「取消派遣」 | 取消在途飞船 |
| 殖民地系统 → UI | `ColonyManager.OnResourcesUpdated` 事件 | 资源变更时 | 更新角落资源数字 |
| UI → 驾驶舱系统 | `CockpitSystem.EnterCockpit(shipId)` | 玩家点击「进入驾驶舱」 | 切换到驾驶舱视角 |

> **⚠️ 接口缺口**：`StarMapData` 目前未定义 `OnOwnershipChanged` 和 `OnFogStateChanged` 事件。若不补充，UI 只能每帧轮询（性能差）。需在星图系统 GDD 中补充这两个事件接口，或在实现阶段通过 ADR 决定轮询 vs 事件驱动方案。

## Formulas

### F-UI-01：飞船图标归一化进度（引用调度系统）

The `icon_lerp_progress` formula is defined as:

`icon_lerp_progress = HopProgress / FLEET_TRAVEL_TIME`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|----------|--------|------|-------|-------------|
| 当前跳内行驶时间 | `HopProgress` | float | [0, FLEET_TRAVEL_TIME] | 由 FleetDispatchSystem 维护，每帧更新 |
| 每跳行驶时间 | `FLEET_TRAVEL_TIME` | float | 3.0s（锁定） | 来自 star-map-system.md |

**Output Range:** [0.0, 1.0]
**Example:** HopProgress = 1.5s → icon_lerp_progress = 0.5（图标在两节点正中）

> **注**：此公式是对 fleet-dispatch-system.md F-3 的引用。UI 层直接读取 `FleetDispatchSystem.visualPosition`（已由调度系统计算为世界坐标），本公式仅说明其语义，不重复计算。

---

### F-UI-02：节点触控命中检测（矩形扩展热区）

The `tap_hit` formula is defined as:

`tap_hit(node, touchPos) = (|touchPos.x − node.screenPos.x| ≤ hotzone/2) AND (|touchPos.y − node.screenPos.y| ≤ hotzone/2)`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|----------|--------|------|-------|-------------|
| 触点屏幕坐标 | `touchPos` | Vector2 | [0, screenWidth] × [0, screenHeight] | 原始触屏输入坐标（dp） |
| 节点屏幕坐标 | `node.screenPos` | Vector2 | 星图视口范围内 | 经 F-UI-03 变换后的屏幕坐标 |
| 热区尺寸 | `hotzone` | float | 48–64dp | HOME_BASE=64dp；RICH=60dp；STANDARD=56dp（来自 UI-R-1） |

**Output Range:** bool（true = 命中）
**Example:** HOME_BASE 节点，hotzone=64dp；触点距节点中心 30dp → 命中；35dp → 命中；33dp → 命中；33dp > 32dp → 未命中（33 > 64/2）

> **命中优先级**：若飞船图标与节点热区重叠，飞船图标优先判定（见 UI-R-4）。

---

### F-UI-03：星图世界坐标 → 屏幕坐标变换

The `world_to_screen` formula is defined as:

`screenPos = (worldPos − cameraOrigin) × zoomScale + screenCenter`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|----------|--------|------|-------|-------------|
| 世界坐标 | `worldPos` | Vector2 | 星图设计空间（dp） | 节点的 `StarNode.position` 字段 |
| 视口原点偏移 | `cameraOrigin` | Vector2 | 无限制 | 玩家单指拖拽改变的星图中心偏移 |
| 缩放倍率 | `zoomScale` | float | [0.5, 2.0] | 双指捏合/展开控制；clamp 于 [0.5, 2.0] |
| 屏幕中心 | `screenCenter` | Vector2 | (screenWidth/2, screenHeight/2) | 固定值，随设备分辨率变化 |

**Output Range:** 屏幕空间坐标（dp），范围随 zoomScale 和 cameraOrigin 变化
**Example:** worldPos=(100,200)，cameraOrigin=(0,0)，zoomScale=1.0，screenCenter=(540,960) → screenPos=(640,1160)

> **逆变换**（触屏坐标 → 世界坐标，用于点击命中判断）：
> `worldPos = (touchPos − screenCenter) / zoomScale + cameraOrigin`

---

### F-UI-04：矿石满仓进度条比例

The `ore_bar_ratio` formula is defined as:

`ore_bar_ratio = ore_current / ORE_CAP`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|----------|--------|------|-------|-------------|
| 当前矿石存量 | `ore_current` | int | [0, ORE_CAP] | 来自 `ColonyManager.GetOreAmount()` |
| 矿石存储上限 | `ORE_CAP` | int | TBD（原型后填入） | 来自 resource-system.md |

**Output Range:** [0.0, 1.0]；ore_bar_ratio = 1.0 时触发满仓色（`#FFAA00` 替换正常白色）
**Example:** ore_current=240，ORE_CAP=1000 → ore_bar_ratio=0.24（进度条填充 24%，颜色正常）；ore_current=1000 → 1.0（满仓警告色）

## Edge Cases

### 触控输入边界

- **If DOCKED 飞船图标与节点热区完全重叠时玩家点击该位置**：激活飞船选中（进入 `SHIP_SELECTED`），飞船图标热区在 Z 轴上优先于节点热区；不得同时触发飞船选中和节点操作两个响应。

- **If 飞船处于 `IN_TRANSIT`，玩家点击其图标**：热区中心必须每帧跟随 `visualPosition`（F-UI-03 变换后的屏幕坐标），而非锁定在逻辑出发节点；热区最小半径 48dp 不随缩放比例缩小。

- **If `CONFIRM_CARD` 状态下玩家在 150ms 内快速点击确认卡外区域两次**：第一次点击关闭确认卡，状态退回 `SHIP_SELECTED`（飞船仍高亮）；第二次点击若落在背景上，取消飞船选中，状态退到 `IDLE`。不得将第二次点击误识别为「选择新目标节点」。

- **If 玩家执行双指缩放手势期间，某根手指抬起坐标恰好落在飞船热区内**：该 TouchPhase.Ended 事件不触发飞船选中；双指手势激活期间屏蔽所有单指 Tap 识别（手势互斥锁）。

### 状态机边界

- **If `SHIP_SELECTED` 状态下所选飞船被系统切换为 `IN_TRANSIT`（极端竞态）**：UI 在弹出确认卡前二次检查 `ShipData.GetState(shipId) == DOCKED`；若检查失败，立即撤销所有节点高亮，状态退回 `IDLE`，不弹确认卡。

- **If 驾驶舱激活（进入 `LOCKED`）时，UI 底层正处于 `CONFIRM_CARD` 或 `SHIP_SELECTED` 状态**：进入 `LOCKED` 前强制清理所有中间 UI 状态（关闭确认卡、撤销飞船高亮）；驾驶舱结束退出 `LOCKED` 后恢复为干净的 `IDLE` 状态，不恢复 `LOCKED` 前的任意中间态。

- **If `IN_TRANSIT_SELECTED` 状态下（取消按钮已显示），所选飞船在同帧完成最后一跳变为 `DOCKED`**：「取消派遣」按钮的点击回调执行前检查 `ShipData.GetState(shipId) == IN_TRANSIT`；若飞船已 `DOCKED`，忽略点击，关闭取消面板，状态退回 `IDLE`。

- **If `SHIP_SELECTED` 状态下所选飞船因无人值守战斗失败变为 `DESTROYED`**：UI 订阅飞船状态变更事件；收到 `DESTROYED` 通知后立即清除所有节点高亮和路径预览线，状态退回 `IDLE`。不得保留已销毁飞船的选中环。

### 渲染边界

- **If 玩家将星图缩放至 0.5×，相邻节点视觉尺寸压缩至原始值的一半**：触控热区最小尺寸固定为 48dp（屏幕空间），不随 `zoomScale` 等比缩小；视觉图标尺寸可缩小，热区不跟随缩放（视觉图标 < 热区时，热区为不可见扩散区）。

- **If 玩家平移星图使节点靠近屏幕边缘，点击该边缘节点后确认卡的计算位置超出屏幕**：确认卡位置做 Safe Area clamp，按「节点右侧 → 左侧 → 上方 → 下方」顺序依次检测越界，取第一个完整在 Safe Area 内的位置渲染。确认按钮不得被屏幕边缘裁剪。

- **If `IN_TRANSIT` 飞船的 `LockedPath` 路径线延伸超出当前视口范围**：禁用 Unity 默认 Frustum Culling（整体剔除会导致视口内可见路径线段消失）；手动将 LineRenderer 的 Bounds 设置为覆盖全星图范围，或改用 UI Toolkit Canvas 空间绘制。

### 数据变更与 UI 状态冲突

- **If `CONFIRM_CARD` 状态下目标节点归属发生变更（防御性处理）**：确认卡不自动关闭；玩家点击「确认派遣」时实时重检 D-1 前置条件；若条件不满足，拒绝派遣，确认卡关闭，显示提示「目标节点状态已变化，派遣取消」。

### 接口缺口与降级策略

- **If 星图系统未定义 `OnOwnershipChanged` / `OnFogStateChanged` 事件推送接口**：MVP 阶段 UI 采用**事件推送方案**——星图系统 GDD 须补充这两个事件（本 GDD 在 Dependencies 章节作为接口需求提出）；若实现阶段确认无法补充，降级为每帧轮询所有 5 个节点状态（MVP 节点数少，性能可接受，但 Vertical Slice 阶段必须切换为事件方案）。

### Android 生命周期

- **If 玩家在 `CONFIRM_CARD`、`SHIP_SELECTED`、`IN_TRANSIT_SELECTED` 任意状态下按 Android 系统返回键**：`CONFIRM_CARD` → 关闭确认卡，退回 `SHIP_SELECTED`；`SHIP_SELECTED` → 取消选中，退回 `IDLE`；`IN_TRANSIT_SELECTED` → 关闭取消面板，退回 `IDLE`。`LOCKED` 状态下返回键由驾驶舱系统处理，星图 UI 层不拦截。

- **If 应用在任意 UI 状态下切换至后台（`onPause`），随后玩家返回（`onResume`）**：`onResume` 时强制将 UI 状态重置为 `IDLE`（调用 `UIStateMachine.ResetToIdle()`）；不恢复 `onPause` 前的任意中间态，因 `onPause` 期间游戏时间可能仍流逝，原选中飞船状态可能已变更。

## Dependencies

### 上游依赖（星图 UI 消费的接口）

| 依赖系统 | 接口 | 用途 | 依赖 GDD |
|---------|------|------|---------|
| **星图系统** | `StarMapData.GetAllNodes()` → 全节点列表 | 初始化渲染所有节点和连线 | star-map-system.md ✅ |
| **星图系统** | `StarMapData.GetOwnership(nodeId)` | 轮询降级：节点颜色更新 | star-map-system.md ✅ |
| **星图系统** | `StarMapData.GetFogState(nodeId)` | 轮询降级：节点探索状态 | star-map-system.md ✅ |
| **星图系统** | `OnOwnershipChanged(nodeId, newState)` ⚠️ *待补充* | 事件推送：节点颜色实时更新 | star-map-system.md（**接口缺口**） |
| **星图系统** | `OnFogStateChanged(nodeId, newState)` ⚠️ *待补充* | 事件推送：探索状态实时更新 | star-map-system.md（**接口缺口**） |
| **舰队调度系统** | `FleetDispatchSystem.visualPosition`（每帧） | IN_TRANSIT 飞船图标渲染位置 | fleet-dispatch-system.md ✅ |
| **舰队调度系统** | `FleetDispatchSystem.LockedPath`（派遣确认后） | 行军路径线渲染 | fleet-dispatch-system.md ✅ |
| **舰队调度系统** | `FleetDispatchSystem.Dispatch(shipId, targetNodeId)` | 玩家点击「确认派遣」时调用 | fleet-dispatch-system.md ✅ |
| **舰队调度系统** | `FleetDispatchSystem.CancelDispatch(shipId)` | 玩家点击「取消派遣」时调用 | fleet-dispatch-system.md ✅ |
| **殖民地系统** | `ColonyManager.GetOreAmount()` / `GetNetOreRate()` / `GetEnergyAmount()` | 资源角标数字显示 | colony-system.md ✅ |
| **殖民地系统** | `ColonyManager.OnResourcesUpdated` 事件 | 资源角标刷新触发 | colony-system.md ✅ |
| **飞船系统** | `ShipData.GetState(shipId)` | EC-UI-05/07 二次状态检查 | ship-system.md ✅ |
| **飞船系统** | `OnShipStateChanged(shipId, newState)` ⚠️ *需确认* | EC-UI-12：感知飞船 DESTROYED，清除高亮 | ship-system.md（需确认是否已定义）|

### 下游依赖（消费星图 UI 输出的系统）

| 下游系统 | 接口 | 用途 | 依赖 GDD |
|---------|------|------|---------|
| **双视角切换系统** | `CockpitSystem.EnterCockpit(shipId)` | 玩家点击「进入驾驶舱」按钮触发 | 双视角切换系统 GDD（未设计）|
| **双视角切换系统** | 驾驶舱激活/退出事件（订阅方） | 驱动星图 UI 进入/退出 `LOCKED` 状态 | 双视角切换系统 GDD（未设计）|

### 接口需求（向上游提出的待补充接口）

以下接口在本 GDD 设计中被依赖，但在对应系统 GDD 中尚未定义，需在实现阶段前补充：

| 需求方 | 需要的接口 | 目标系统 | 优先级 |
|--------|----------|---------|--------|
| 星图 UI | `OnOwnershipChanged(nodeId, newState)` | 星图系统 | **高**（MVP 实现前必须确认，否则降级为轮询）|
| 星图 UI | `OnFogStateChanged(nodeId, newState)` | 星图系统 | **高**（同上）|
| 星图 UI | `OnShipStateChanged(shipId, newState)` | 飞船系统 | 中（EC-UI-12 防御性处理用）|

### 双向一致性要求

- **舰队调度系统 ↔ 星图 UI**：调度系统 GDD 已声明「向 UI 系统发布 `visualPosition`（每帧）和 `LockedPath`（派遣确认时）」，与本 GDD 消费方一致 ✅
- **殖民地系统 ↔ 星图 UI**：殖民地系统 GDD 下游依赖表中已列出「星图 UI 消费 `OnResourcesUpdated` 事件」✅
- **星图系统 ↔ 星图 UI**：星图系统 GDD 下游依赖中已标注「星图 UI 依赖所有只读接口」，但尚无事件定义——本 GDD 作为接口需求提出，需在架构决策阶段补充 ⚠️

## Tuning Knobs

以下旋钮均为 UI 层视觉参数，调整无需修改游戏逻辑代码。建议通过 ScriptableObject 或 USS 变量热加载。

| 旋钮名 | 默认值 | 安全范围 | 影响的游戏体验 |
|--------|--------|---------|--------------|
| `ZOOM_MIN` | 0.5× | 0.3–0.8 | 最小缩放比例；过小→节点图标不可读且热区重叠，过大→无法总览全局 |
| `ZOOM_MAX` | 2.0× | 1.5–3.0 | 最大缩放比例；过大→屏幕被单节点占满，感觉失去方位感 |
| `NODE_SIZE_HOME_BASE` | 56dp | 40–72dp | HOME_BASE 节点视觉尺寸；应明显大于 STANDARD，强化「重要地标」感知 |
| `NODE_SIZE_STANDARD` | 44dp | 32–60dp | STANDARD 节点视觉尺寸；参考值比 HOME_BASE 小 20%+（56→44） |
| `NODE_SIZE_RICH` | 48dp | 36–64dp | RICH 节点视觉尺寸；应与 STANDARD 有区别但小于 HOME_BASE |
| `CONFIRM_CARD_SLIDE_DURATION` | 0.2s | 0.1–0.4s | 确认卡底部滑入/滑出动画时长；过快→突兀，过慢→操作节奏拖沓 |
| `FLEET_ICON_PULSE_PERIOD` | 1.2s | 0.6–2.0s | IN_TRANSIT 飞船脉冲光晕周期；过快→视觉噪音干扰，过慢→移动状态不直观 |
| `PATH_LINE_DIMMED_OPACITY` | 40% | 20–60% | 已走过路段路径线透明度；过低→路径消失感，过高→与未走路段难以区分 |
| `EXPLORED_NODE_OPACITY` | 50% | 30–70% | EXPLORED 节点颜色透明度；过低→节点几乎不可见，过高→与 VISIBLE 节点无视觉层级 |

**不可调整的固定值**（Android 规范强制）：
- 最小触控热区：**48dp**（所有节点和飞船图标的最小可触区域，不随缩放或旋钮调整）

**联动调节注意事项**：
- `ZOOM_MIN`（0.5×）与 5 节点地图布局联动——若地图节点间距 < 96dp（屏幕坐标），则 0.5× 时相邻热区重叠；调整 `ZOOM_MIN` 须同时验证最密集节点对的最小间距
- `NODE_SIZE_*` 三者应保持层级关系：`NODE_SIZE_HOME_BASE` > `NODE_SIZE_RICH` > `NODE_SIZE_STANDARD`，且最小值须 ≥ 48dp（热区要求下限）
- `FLEET_TRAVEL_TIME`（3.0s）来自 star-map-system.md，不在本系统调整，但影响确认卡显示的「约 X 秒到达」体感——调整 `FLEET_TRAVEL_TIME` 时需同步审视确认卡文案格式

## Visual/Audio Requirements

### VA-1. 视觉风格总纲

**风格定位：「战术全息投影图」（Tactical Holographic Display）**

星图不是真实宇宙的俯视图，而是指挥官面前的**战术桌面投影**。所有视觉元素应有轻微的「数字/电子」质感——线条精准，颜色克制，偶有数据噪声。

- **参考方向**：FTL: Faster Than Light（简洁节点图）、Into the Breach（战术棋盘清晰感）、Endless Space 2 星图层（扁平化全息）
- **禁止参考**：No Man's Sky（有机生命感）、星际争霸（写实载具感）
- **核心质感**：深空底色 `#0A0A1A`、冷蓝主调、信息优先，克制使用发光效果

**全局动效参数基准**：

| 类别 | 时长范围 | 缓动曲线 | 说明 |
|------|---------|---------|------|
| 微交互（点击反馈） | 80–120ms | `EaseOutQuad` | 立即响应，无延迟感 |
| 状态切换（颜色/图标） | 150–200ms | `EaseInOutCubic` | 平滑但不拖沓 |
| 面板进出（确认卡） | 180–220ms | `EaseOutBack(overshoot=1.1)` | 轻弹感，不超过 10% overshoot |
| 长时循环（脉冲/呼吸） | 800–1200ms | `SinusoidalInOut` | 平滑循环，不刺眼 |
| 场景切换（进驾驶舱） | 400–600ms | `EaseInExpo` | 加速感，象征「出发」 |

**颜色扩展规范（与节点系统兼容）**：

| 用途 | 颜色值 | 使用规则 |
|------|--------|---------|
| 玩家主色 | `#2266CC` | 节点填充、飞船图标、选中高亮 |
| 玩家发光色 | `#4488FF` | 光晕、脉冲、选中环，不超过节点面积的 1.5 倍 |
| 敌方主色 | `#FF4400` | 节点填充 |
| 敌方发光色 | `#FF6633` | 危险提示，严格限制——红色是警报 |
| UI 底层 | `#0A0A1A` | 背景底色，接近纯黑但微带蓝紫 |
| 确认卡背景 | `#12142A` + 60% 不透明度 | 毛玻璃感磨砂效果 |
| 警告文字 | `#FF6633` | 仅用于强警报，每屏同时不超过 1 处 |

同屏不能出现超过 3 种发光色。Bloom 通过 URP Post-Processing Volume 实现（Intensity ≤ 0.4，Threshold ≥ 0.8）。

---

### VA-2. VFX 与视觉反馈规格

#### VA-2.1 节点被玩家占领（中立/敌方 → 玩家）

**触发时机**：飞船到达目标节点，所有权写入完成后立即触发。

1. **颜色洪流**（主效果）：从节点中心向外扩散圆形色块，过渡至 `#2266CC`。扩散范围 = 节点半径 × 2.5，时长 **300ms**，`EaseOutCirc`。
2. **边框脉冲**：描边从 1.5dp → 3dp → 1.5dp，峰值在 150ms，回落在 300ms，颜色 `#4488FF`。
3. **微型粒子扩散**：8–12 颗小粒子从节点中心弹出，半径 40–60dp，存活时间 400ms，透明度 100% → 0%。粒子无物理，使用对象池，**禁止使用碰撞体**。
4. **节点图标弹跳**：图标缩放 1.0 → 1.15 → 1.0，时长 250ms，`EaseOutBack(overshoot=1.2)`。

#### VA-2.2 节点被敌方占领（玩家/中立 → 敌方）

**触发时机**：敌方 AI 完成节点夺取计算后。

1. **颜色洪流**：颜色改为 `#FF4400`，时长 **350ms**（比玩家稍慢，制造压迫感）。
2. **边框警报闪烁**：描边在 0–200ms 内快速闪烁 2 次（颜色 `#FF6633`，频率 100ms/次），然后稳定保持 `#FF4400` 描边。
3. **震动（Shake）**：节点 RectTransform 水平轻震：振幅 ±4dp，3 次衰减震荡，总时长 **300ms**。
4. **无扩散粒子**：敌方占领不使用向外扩散粒子（蚕食感≠扩张感）。

#### VA-2.3 飞船选中（进入 SHIP_SELECTED 状态）

**触发时机**：玩家点击已 DOCKED 飞船图标。

1. **选中环**：描边圆环从半径 8dp 扩展到 16dp，透明度 0% → 100%，时长 **150ms**，`EaseOutQuad`。选中后缓慢旋转（**360°/4s，匀速**）。
2. **图标放大**：飞船图标 24dp → 28dp，时长 **120ms**，`EaseOutBack(overshoot=1.1)`。
3. **所在节点高亮**：亮度 +15%，时长 150ms。
4. **可达节点高亮**：边框透明度 +30%，亮度 +10%，时长 **200ms**，`EaseInOutQuad`。
5. **不可达节点暗化**：亮度 -20%，Alpha 80%，时长 **200ms**。

#### VA-2.4 路径预览线

**触发时机**：`SHIP_SELECTED` 状态下玩家手指滑向目标节点。

1. **材质**：虚线风格，线段长 12dp，间隔 8dp，颜色 `#4488FF`，线宽 1.5dp，透明度 80%。
2. **流动动画**：虚线 UV offset 向目标方向平移，速度 **60dp/s**（「能量流向目标」方向感）。
3. **出现动画**：路径线从飞船位置向目标延伸生长，全程 **180ms**，`EaseOutCubic`。
4. **目标节点预确认**：边框单次 pulse（`#4488FF`，亮度 +30%，100ms）。
5. **禁止**：路径预览期间禁止使用粒子效果（高频触发，性能风险）。

#### VA-2.5 确认卡滑入 / 滑出

- **滑入**：Y 轴 +120dp → 底部，**200ms**，`EaseOutBack(overshoot=1.05)`；背景遮罩 0 → 30%，180ms。
- **取消滑出**：Y 轴退出，**160ms**，`EaseInQuad`（加速退出，干脆；无弹性感）。
- **确认消失**：缩放 1.0 → 1.03 → 0，**200ms**，`EaseInBack(overshoot=1.5)`（操作被吸收/执行的感觉）。

#### VA-2.6 IN_TRANSIT 飞船脉冲

1. **光晕脉冲**：光晕从 12dp → 24dp，透明度 60% → 0%，循环间隔 **1200ms**，`SinusoidalOut`。
2. **图标自旋**：360°/3s，匀速顺时针（发动机运转中）。
3. **路径线流动**：UV 流动速度 **40dp/s**（比预览慢——已执行，非预览）。

性能约束：同屏最多 3 艘 IN_TRANSIT，脉冲光晕每周期最多 1 颗粒子，禁止使用粒子集群。

#### VA-2.7 LOCKED 状态遮罩

1. **全局遮罩**：`#0A0A1A` 从 0% → 70% Alpha，时长 **250ms**，`EaseInOutQuad`（不完全黑屏，玩家仍可隐约看到星图）。
2. **扫描线纹理**（可选）：水平扫描线纹理（透明度 10%），1×2 像素 tiled，零额外 DrawCall。
3. **循环动效暂停**：进入 LOCKED 时所有节点/飞船循环动效暂停（通过动画控制器状态控制，`Time.timeScale` 不归零）。

#### VA-2.8 资源更新跳动

1. **数字 Counter Roll**：旧值滚动至新值，时长 **400ms**，`EaseOutCubic`；增加时绿色（`#44CC88`），减少时红色（`#FF6633`），持续 **600ms** 后回白色。
2. **图标弹跳**：Scale 1.0 → 1.2 → 1.0，时长 **200ms**，`EaseOutBack(overshoot=1.3)`。
3. **浮动文字**：临时文字（如 `+12⚡`）从资源角标位置向上飘动 20dp，透明度 100% → 0%，时长 **800ms**。使用对象池，每种资源独立 1 个浮动文字对象，不叠加。

**移动端性能硬约束**：

| 约束项 | 上限 |
|--------|------|
| 同屏活跃粒子数量 | ≤ 50 颗 |
| 单事件粒子发射数 | ≤ 12 颗 |
| UI Shader 变体数（星图场景） | ≤ 4 个 |
| 音效同时播放通道数 | ≤ 6（UI SFX×2 + 环境×1 + BGM×1 + 保留×2） |
| Bloom Intensity | ≤ 0.4，Threshold ≥ 0.8 |

---

### VA-3. 音频需求规格

**整体风格：「克制的电子战术音效」**
参考：FTL UI 音效（干净、有信息量）、Hades 菜单音效（有力但不刺耳）
全局混响：轻微 Room 混响（衰减 0.3s），制造「密闭驾驶舱内操作触摸屏」空间感

| ID | 事件 | 音效描述 | 时长 | 音调 | 优先级 |
|----|------|---------|------|------|--------|
| SFX-01 | 节点点击 | 短促电子「嘀」，800–1000Hz 正弦波，轻微高频包络 | 80ms | 中高 | 低（可被打断） |
| SFX-02 | 飞船选中 | 两段上升「嘀哒」600Hz→900Hz，间隔 60ms，Synth Pad 拖尾 | 220ms | 中高 | 中 |
| SFX-03 | 确认派遣 | 厚实「嗡」+ 扫频 200→1200Hz，有「发射」动势 | 350ms | 低→高 | 高（不被打断） |
| SFX-04 | 取消操作 | 下降「嘀」700Hz→400Hz，短促干脆 | 120ms | 中→低 | 低 |
| SFX-05 | 玩家占领节点 | 三音上升和弦「叮叮叮」500/700/1000Hz，间隔 80ms，Pad 拖尾 0.5s | 600ms | 中高 | 高 |
| SFX-06 | 敌方占领节点 | 低沉双音「轰咚」150Hz+80Hz，间隔 100ms，轻微警报失真 | 500ms | 极低 | 高（警报级） |
| SFX-07 | 飞船到达目的地 | 上扬「叮」600→900Hz + Light Reverb 尾音 | 200ms | 中高 | 中 |
| SFX-08 | 进入驾驶舱 | 蓄力 300ms + 扫频 200ms + 引擎点火低频 100Hz（600ms 淡出），Large Hall 混响 | 1100ms | 低→高→低 | **独占**（不叠加其它音） |
| SFX-09 | 矿石更新 | 轻敲金属「叮」，800Hz，无混响 | 80ms | 高 | 最低（可抛弃） |
| SFX-10 | 能源更新 | 电子充能「嗞」，600Hz + 高次谐波 | 80ms | 中 | 最低（可抛弃） |

**资源音效触发规则**：
- ≤ 5 单位变化：播放 1 次
- 5–20 单位：播放 1 次，音量 +3dB
- > 20 单位：播放 2 次，间隔 80ms，音量 +6dB
- 同帧多资源更新：去重，只播放一次（避免音效堆叠）

**星图缩放 / 平移**：无专用音效。维持低音量星图环境 Ambience（宇宙底噪 + 轻微静电感，循环，-18dBFS）；停止触摸输入 1.5s 后 Ambience +3dB，手势操作时回基准值。

---

### VA-4. 关键艺术准则

| 准则 | 核心原则 |
|------|---------|
| **A：颜色是信息，不是装饰** | 玩家在 0.5s 内必须能仅通过颜色读出任何节点的归属；禁止装饰性颜色影响节点主色区分度；必须通过色盲可读性测试 |
| **B：动效为信息服务，不是表演** | 非直接响应玩家操作的背景动效不得吸引视觉注意力超过 1s；同屏同时播放 VFX 动效不超过 3 种 |
| **C：移动端性能硬约束** | 任何单个 VFX 事件不得导致帧率跌破 55fps（低端 Android 设备基准） |
| **D：玩家操作必须有立即反馈** | 任何触摸事件在 80ms 内必须有可感知的视觉或音频反馈；选中环等视觉反馈必须在第 1 帧开始 |
| **E：音效是信息的第二层** | SFX-05（玩家占领）和 SFX-06（敌方占领）仅凭声音能区分好坏（盲测正确率 ≥ 80%）；不同事件音调区分度 ≥ 200Hz |

---

### VA-5. 视觉/音频验收标准

| ID | 标准 | 测试方式 | 级别 |
|----|------|---------|------|
| VA-ACC-01 | 低端 Android（Snapdragon 665 等效）稳定 55fps+ | 设备实机录屏 + Profiler | **BLOCKING** |
| VA-ACC-02 | 节点占领颜色切换在 300ms 内完成全部动效 | 录屏逐帧分析 | ADVISORY |
| VA-ACC-03 | 5 名非开发人员测试者能在 5s 内指出节点归属 | 用户测试 | **BLOCKING** |
| VA-ACC-04 | SFX-05/06 盲测区分正确率 ≥ 80% | 盲测（关闭画面） | ADVISORY |
| VA-ACC-05 | 所有触摸事件在 80ms 内触发对应音效 | Profiler Audio 通道时序检查 | **BLOCKING** |
| VA-ACC-06 | SFX-08 独占播放，无其它音效叠加 | 手动测试 + 音频通道监控 | ADVISORY |
| VA-ACC-07 | 同屏粒子总数 ≤ 50 颗 | Unity Profiler 截图 | **BLOCKING** |
| VA-ACC-08 | 色盲模拟工具验证：节点归属可通过形状区分（不依赖颜色） | 色盲模拟器截图存档 | ADVISORY |

## UI Requirements

### UI-TECH-1. 实现框架

- **渲染方案**：Unity 6 UI Toolkit（Runtime UI），基于 UXML/USS 构建。星图节点图使用 `Painter2D` API 绘制（矢量，无纹理开销）。
- **坐标体系**：所有布局单位使用 **dp（density-independent pixels）**，运行时通过 `Screen.dpi` 换算为物理像素。
- **`VisualElement.transform` 禁用**：Unity 6.2 已废弃；飞船图标位置通过 `style.left / style.top` 驱动（见 UI-R-3）。

### UI-TECH-2. 屏幕适配

- **目标比例**：16:9（手机竖屏）和 4:3（平板）均需可用。
- **基准分辨率**：1080×1920（16:9）；适配时等比缩放，不拉伸。
- **安全区（Safe Area）**：所有交互元素必须位于 `Screen.safeArea` 范围内。确认卡底部距 Safe Area 下边缘 **≥ 16dp**；资源角标顶部/左侧距 Safe Area 边缘 **≥ 12dp**。
- **平板适配**：4:3 比例下星图视口宽高比不变，两侧留空以深色填充（`#0A0A1A`），不拉伸地图。

### UI-TECH-3. 组件布局规格

| 组件 | 锚点 | 尺寸 | 层级（Z-order） |
|------|------|------|-----------------|
| 星图视口（Painter2D Canvas） | 全屏 | 100% 宽高 | Layer 0（底层） |
| 路径预览线层 | 全屏 | 100% 宽高 | Layer 1（线在图标下） |
| 飞船/节点图标层 | 全屏 | 100% 宽高 | Layer 2 |
| 资源角标 | 左上角 | 自适应内容宽度，高度 48dp | Layer 3（始终置顶） |
| 飞船选中悬浮按钮 | 跟随飞船图标位置 | 48dp × 48dp 热区 | Layer 3 |
| 派遣确认卡 | 底部全宽 | 全宽 × 120dp | Layer 4（覆盖星图） |
| 取消派遣面板 | 底部全宽 | 全宽 × 80dp | Layer 4 |
| LOCKED 遮罩层 | 全屏 | 100% 宽高 | Layer 5（最顶层） |

### UI-TECH-4. 触控输入管理

- **输入系统**：Unity 新 Input System（`EnhancedTouch`），禁用 Legacy Input Manager。
- **手势互斥锁**：双指手势激活期间，所有单指 Tap 事件挂起；双指松开后 150ms 内的单指事件同样忽略（防误触）。
- **点击去抖**：同一触点在 100ms 内的重复 `TouchPhase.Began` 忽略（防多次注册）。
- **Android 返回键**：由星图 UI 状态机优先处理（见 EC-UI-17/18），不透传至系统。

### UI-TECH-5. UX Spec 需求

以下屏幕/流程需要在实现前单独撰写 UX Spec（Pre-Production 阶段，Epic 立项前完成）：

| 屏幕/流程 | UX Spec 文件 | 优先级 |
|----------|-------------|--------|
| 星图主视图（节点交互完整流程） | `design/ux/star-map-view.md` | **高**（MVP 核心） |
| 派遣确认卡流程 | `design/ux/dispatch-confirm-card.md` | **高**（MVP 核心） |
| 驾驶舱入口（两步流程） | `design/ux/cockpit-entry-flow.md` | 中（与双视角切换系统联动） |

## Acceptance Criteria

> **测试类型说明**：「EditMode」= Unity Test Framework EditMode 静态验证，无需场景启动；「PlayMode」= 需帧循环和场景，验证运行时行为；「PlayMode Android」= 须在 Android 真机或官方模拟器执行，Editor Play Mode 不可替代（DPI 换算依赖真实设备）。

### 节点渲染

**AC-UI-01** | EditMode | GIVEN 星图加载完成，存在一个属于玩家的 HOME_BASE 节点，WHEN QA 在 UI Inspector 中测量该节点的视觉尺寸与碰撞热区，THEN 节点视觉直径必须为 **56dp ±2dp**，可交互热区直径必须为 **64dp ±2dp**，节点填充色必须为 **#2266CC（偏差 ≤5%）**。
Pass/Fail: 视觉或热区尺寸超出容差 → FAIL；颜色偏差超标 → FAIL。

**AC-UI-02** | EditMode | GIVEN 星图中同时存在 STANDARD 和 RICH 两种节点类型，WHEN QA 截图并测量各节点，THEN STANDARD 节点形状为**圆形**，视觉 **44dp ±2dp**，热区 **56dp ±2dp**；RICH 节点形状为**菱形（旋转 45°）**，视觉 **48dp ±2dp**，热区 **60dp ±2dp**。
Pass/Fail: 形状类型错误直接 FAIL；尺寸超出容差 → FAIL。

**AC-UI-03** | EditMode | GIVEN 星图中存在 VISIBLE PLAYER / ENEMY / NEUTRAL 三类归属节点，WHEN QA 取色工具分别采样节点填充色，THEN PLAYER = **#2266CC**，ENEMY = **#FF4400**，NEUTRAL = **#888888**，各允许 ±5 十六进制偏差。
Pass/Fail: 任一颜色超标 → FAIL；ENEMY 与 PLAYER 颜色互换 → S1 FAIL。

**AC-UI-04** | EditMode | GIVEN 星图中存在一个 EXPLORED 节点（上次已知归属为 PLAYER）和一个 UNEXPLORED 节点，WHEN QA 截图观察，THEN EXPLORED 节点渲染为 **#2266CC 50% 透明**并叠加 **"?" 图标（12dp）**；UNEXPLORED 节点**不渲染任何图形和标签**。
Pass/Fail: EXPLORED 节点无 "?" 图标 → FAIL；透明度偏差 >10% → FAIL；UNEXPLORED 节点可见 → FAIL。

### 连接线渲染

**AC-UI-05** | PlayMode | GIVEN 星图已渲染，存在三种状态连接线（默认、有舰队在途、已走过路段），WHEN QA 截图并测量颜色/宽度/线型，THEN 默认线：**#444466 / 1.5dp / 实线**；在途线：**#4488FF / 2dp / 实线**；已走过路段：**#4488FF 40% 透明 / 2dp**；可达路径预览：**#4488FF 60% 透明虚线**。
Pass/Fail: 颜色偏差 >5% 或线宽偏差 >0.5dp 或线型错误 → FAIL。

### 触控热区（Android 48dp 最小值）

**AC-UI-06** | PlayMode Android | GIVEN 星图在 Android 真机运行，缩放比例为最大值 **2.0×**，WHEN QA 用手指点击 STANDARD 节点视觉图标边缘（距圆心 22dp 处），THEN 节点**仍响应点击**；热区有效半径任何缩放级别下 ≥ **24dp**（屏幕物理坐标，不随缩放缩小）。
Pass/Fail: 边缘点击无响应 → FAIL；热区随缩放压缩 → FAIL。

**AC-UI-07** | PlayMode Android | GIVEN 星图缩放比例为最小值 **0.5×**，WHEN QA 连续点击 HOME_BASE 节点边缘（距视觉中心 32dp 处），THEN 点击**仍被捕获**，热区有效半径 ≥ **32dp**（热区直径 64dp / 2），不随缩放压缩。
Pass/Fail: 边缘点击无响应或需二次点击 → FAIL。

### 触控优先级与状态机

**AC-UI-08** | PlayMode | GIVEN 星图处于 IDLE 状态，一艘 IN_TRANSIT 飞船图标与某节点在屏幕上视觉重叠，WHEN QA 点击重叠区域，THEN 系统进入 **IN_TRANSIT_SELECTED** 状态（飞船优先），而非进入 SHIP_SELECTED（节点）或 CONFIRM_CARD 流程。
Pass/Fail: 触发节点选中而非飞船选中 → FAIL；进入 CONFIRM_CARD → S1 FAIL。

**AC-UI-09** | PlayMode | GIVEN 玩家完成「选中飞船 → 选择目标节点」进入 CONFIRM_CARD 状态，WHEN QA 点击**取消**按钮，THEN 确认卡以 **0.2s ±0.03s 滑出动画**收回，状态机返回 **IDLE**，无飞船被派遣，节点高亮消除。
Pass/Fail: 动画缺失或 <0.15s → FAIL；状态未回 IDLE → FAIL；飞船被错误派遣 → S1 FAIL。

### 确认卡内容准确性

**AC-UI-10** | PlayMode | GIVEN 玩家选中飞船并选择目的地（路径跳数为已知值 N，F-2 公式计算时间为 T = N × 3.0s），WHEN 确认卡从底部滑入（动画时长 **0.2s ±0.03s**，卡片高度 **120dp ±4dp**），THEN 卡内显示：正确**目的地名称**、**跳数 = N**、**预计到达时间 = T 秒**；确认按钮蓝色占卡宽 **50% ±5%**，取消按钮灰色占卡宽 **50% ±5%**。
Pass/Fail: 跳数或时间错误 → FAIL；按钮宽度偏差 >5% → FAIL。

### 资源角标

**AC-UI-11** | PlayMode | GIVEN 资源角标已显示在屏幕左上角，WHEN QA 单指拖拽星图至极限边界，再双指捏合缩放至 0.5×，THEN 资源角标**位置固定不动**，始终锚定屏幕左上角，数值内容不被屏幕边缘裁切。
Pass/Fail: 角标随星图移动任何像素 → FAIL；内容被裁切 → FAIL。

**AC-UI-12** | PlayMode | GIVEN 能源存量 >0，WHEN 测试脚本触发 **OnResourcesUpdated 事件**，将能源净产量改为负值（赤字），THEN 能源数字在**同一帧或下一帧（≤16.6ms）**切换为**红色（#FF4400）**；矿石净产量格式为 `+N/s`（正值）/ `-N/s`（负值）。
Pass/Fail: 颜色未变红 → FAIL；刷新延迟超过 2 帧 → FAIL。

### 驾驶舱入口（LOCKED 状态）

**AC-UI-13** | PlayMode | GIVEN 星图处于 **CONFIRM_CARD** 或 **SHIP_SELECTED** 状态，WHEN 外部系统触发驾驶舱激活事件，THEN 状态机立即进入 **LOCKED**：确认卡消失（无残留），飞船选中高亮清除，所有中间 UI 状态完全清理；驾驶舱 UI 可正常显示。
Pass/Fail: CONFIRM_CARD 残留可见 → S1 FAIL；飞船高亮残留 → FAIL；状态机未进入 LOCKED → FAIL。

**AC-UI-14** | PlayMode | GIVEN 星图处于 **LOCKED** 状态，WHEN 驾驶舱关闭（或 Android `onResume` 回调触发），THEN 星图状态机强制重置为 **IDLE**；所有节点可正常交互；不存在半途派遣流程或残留选中状态。
Pass/Fail: 状态未回 IDLE → FAIL；节点点击无响应 → FAIL；存在半途派遣任务 → S1 FAIL。

### 缩放边界（zoomScale Clamp）

**AC-UI-15** | PlayMode | GIVEN 星图缩放为任意值，WHEN QA 持续双指放大超过 2.0× 极限，再持续双指缩小低于 0.5× 极限，THEN `zoomScale` 被硬性 clamp 在 **[0.5, 2.0]** 区间内；松手后**不出现任何弹性回弹**（松手即停，无反向位移 >2dp）；可通过代码断言 `clamp(zoom, 0.5f, 2.0f) == zoom` 验证。
Pass/Fail: 缩放超出 [0.5, 2.0] → FAIL；存在弹性回弹 >2dp → FAIL。

---

| AC 编号 | 覆盖域 | 测试类型 |
|---------|--------|---------|
| AC-UI-01 | 节点尺寸/热区/颜色（HOME_BASE） | EditMode |
| AC-UI-02 | 节点形状/尺寸（STANDARD/RICH） | EditMode |
| AC-UI-03 | 节点颜色三类归属 | EditMode |
| AC-UI-04 | EXPLORED "?" 图标 / UNEXPLORED 隐藏 | EditMode |
| AC-UI-05 | 连接线颜色/宽度/线型 | PlayMode |
| AC-UI-06 | 触控热区 48dp（最大缩放 2.0×） | PlayMode Android |
| AC-UI-07 | 触控热区 48dp（最小缩放 0.5×） | PlayMode Android |
| AC-UI-08 | 点击优先级（飞船 > 节点） | PlayMode |
| AC-UI-09 | 派遣取消流程 / 状态回 IDLE | PlayMode |
| AC-UI-10 | 确认卡内容准确性（跳数/时间） | PlayMode |
| AC-UI-11 | 资源角标固定锚点（平移/缩放不移动） | PlayMode |
| AC-UI-12 | 资源角标刷新时机（事件驱动 ≤16.6ms） | PlayMode |
| AC-UI-13 | 驾驶舱激活 → LOCKED + 中间态清理 | PlayMode |
| AC-UI-14 | onResume / 驾驶舱退出 → 强制 IDLE | PlayMode |
| AC-UI-15 | zoomScale clamp [0.5, 2.0] 无弹性回弹 | PlayMode |

## Open Questions

| # | 问题 | 来源 | 优先级 | 目标解决时机 |
|---|------|------|--------|-------------|
| Q-1 | `OnOwnershipChanged` 和 `OnFogStateChanged` 事件接口尚未在星图系统 GDD 中定义。MVP 实现前必须确认：采用事件推送还是降级为每帧轮询（5 节点 MVP 可接受，Vertical Slice 后不可用）？ | Interactions（Section C） | **高** | MVP 架构阶段（实现前） |
| Q-2 | `OnShipStateChanged(shipId, newState)` 事件在飞船系统 GDD 中是否已定义？EC-UI-12（飞船 DESTROYED 清除高亮）依赖此事件。 | Edge Cases | **中** | 实现阶段确认 |
| Q-3 | 多场战斗并发时的通知 UI（UI-EX-5）设计待决，依赖舰队调度系统 Q-2 的决策。当 Q-2 确定后，需补充本 GDD 的多战斗通知 UI 规格。 | Detailed Design（MVP 排除项） | 中 | 舰队调度系统 Q-2 锁定后 |
| Q-4 | `Painter2D` API 在低端 Android 设备（Snapdragon 665）上的连线渲染性能尚未验证。若性能不达标，需评估改用 `GL.Lines` 或预烘焙纹理方案。 | UI-TECH-1 | 中 | Vertical Slice 原型阶段 |
| Q-5 | 确认卡「预计到达时间」显示格式：当路径跳数 ≥ 3 时，总时间 ≥ 9s，是否需要改为「约 Xs」以外的更友好格式（如「约 X 秒 / X.Xs」）？目前按 F-UI-01 直接显示秒数。 | Formulas（F-UI-02） | 低 | Playtest 后决定 |
| Q-6 | 星图视口的初始 `cameraOrigin` 和 `zoomScale` 起始值尚未确定。建议：初始缩放 1.0×，`cameraOrigin` 居中（HOME_BASE 节点在屏幕中心）。需在原型阶段验证体感。 | Formulas（F-UI-03） | 低 | Vertical Slice 原型阶段 |
