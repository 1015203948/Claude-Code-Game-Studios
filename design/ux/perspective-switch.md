# UX Spec: 双视角切换流程 (Perspective Switch Flow)

> **Status**: In Design — v2 (X4 风格改造)
> **Author**: UX Designer
> **Last Updated**: 2026-04-14
> **Journey Phase(s)**: 核心游戏循环 — 贯穿全程
> **GDD Source**: `design/gdd/dual-perspective-switching.md`
> **Template**: UX Spec

---

## Purpose & Player Need

这是「将军亲征 × 始终在场」体验的 UX 入口。玩家以驾驶员身份存在于宇宙中，始终绑定一艘「当前座驾（ActiveShip）」。

**入场需求（星图 → 驾驶舱）**：从星图切换到 ActiveShip 的驾驶舱，完整进入操控层。感受从俯瞰变为沉浸的切换重量。

**叠加层需求（驾驶舱内开图）**：不退出驾驶舱，叠加显示星图进行战略查阅或发布指令。「左手操控飞船，右手看全局」的 X4 式双层意识。

**跳船需求（飞船间传送）**：在叠加星图内选择另一艘己方飞船，立即传送过去。不需要先返回星图中转——你是在传送，不是在切换摄像机。

**抽身需求（驾驶舱 → 星图）**：需要完整的鸟瞰全局时，才切回纯星图视图。

**时间控制需求**：随时调整策略层时间倍率，无论身处哪个视图。

---

## Player Context on Arrival

### 进入驾驶舱时

玩家来自星图，携带战略决策背景。触发时刻通常是关键战役或主动探索。到达时情绪是「决断」，设计强化主动跳入的仪式感。

### 返回星图时

玩家来自驾驶舱，需要完整全局视野时触发。返回后信息涌入（缺席期间的战局变化），星图摄像机直接到位，不做动画。

### 打开叠加层时

玩家已在驾驶舱内，不想中断操控但需要查看全局。情绪通常是「多任务紧张感」，驾驶舱物理继续运行。

### 跳船传送时

玩家在叠加层内发现另一艘船需要亲自驾驶，决定传送过去。是一个明确的战略决策动作，应有传送感（区别于普通切换）。

---

## Navigation Position

```
游戏主循环
├── 星图视图（STARMAP）                    ← 常驻
│   ├── 飞船选中面板
│   │   └── [进入驾驶舱] 按钮             ← 入口 A
│   └── 时间控制条                        ← 全局控件
│
└── 驾驶舱视图（COCKPIT）                  ← 按需加载
    ├── 飞船 HUD
    │   ├── [返回星图] 按钮               ← 出口
    │   └── [开叠加层] 按钮              ← 入口 B
    └── 时间控制条                        ← 全局控件
        │
        └── 叠加层（COCKPIT_WITH_OVERLAY）← 驾驶舱内叠加
            ├── 星图（可滚动/缩放）
            ├── 飞船选中 → [立即传送]     ← 入口 C（跳船）
            └── [关闭叠加层] 按钮
```

> **水平跃迁规则**：不支持从叠加层直接返回星图（必须先关闭叠加层，再点「返回星图」），以避免状态机歧义。

---

## Entry & Exit Points

### 入口

| 入口 | 来源 | 触发方式 | 玩家携带的上下文 |
|------|------|---------|----------------|
| A — 全切换进入 | 星图飞船选中面板 | 点击「进入驾驶舱」 | 目标 shipId，当前星图摄像机位置 |
| B — 打开叠加层 | 驾驶舱 HUD | 点击「开叠加层」按钮 | 当前 ActiveShipId，驾驶舱状态 |
| C — 直接跳船 | 叠加层内飞船确认卡 | 点击「立即传送」 | 目标 shipId，当前 ActiveShip 状态 |

### 出口

| 出口目标 | 触发方式 | 备注 |
|---------|---------|------|
| 星图视图 | 点击「返回星图」（来自驾驶舱或关闭叠加层后） | 摄像机恢复至记录位置；飞船数据回写 |
| 另一艘船的驾驶舱 | 叠加层内点击「立即传送」 | ActiveShipId 更新；旧船数据回写 |
| 驾驶舱（关闭叠加层） | 叠加层内点击关闭 | 仅关闭叠加层，驾驶舱状态不变 |
| 星图视图（强制）| 切换中飞船被摧毁（GDD E-1） | 切换中断，回落 STARMAP |

---

## Layout Specification

### Information Hierarchy

| 优先级 | 信息元素 | 视觉权重 |
|--------|---------|---------|
| 1 | 「进入驾驶舱」/ 「立即传送」按钮（主 CTA） | 最大，全宽主按钮 |
| 2 | CurrentHull / MaxHull 血条 | 高，带数值标签 |
| 3 | 飞船名称 / 类型 | 中，面板标题 |
| 4 | ShipState 文字标签（仅禁用时显示） | 低 |
| 5 | 飞船位置 | 最低，小字灰色 |

### Layout Zones

**A — 飞船选中面板（星图层，底部抽屉式）**
从底部滑出，固定高度 200dp（长文本时弹性扩展至 220dp），不遮挡星图主区域。

**B — 「返回星图」按钮（驾驶舱 HUD，左上角固定）**
固定于左上 Safe Area，任何驾驶舱状态下均可见、均可触达。

**C — 「开叠加层」按钮（驾驶舱 HUD，右上角固定）**
固定于右上 Safe Area，ViewLayer == COCKPIT 时可用。

**D — 星图叠加层面板（驾驶舱内叠加，占屏 70%，从右侧滑入）**
半透明背景（OVERLAY_OPACITY = 0.85），可滚动/缩放的星图视图。右上角关闭按钮。

**E — 时间控制条（两个视图均有）**
星图：右上角。驾驶舱：与「开叠加层」按钮同区域。按钮：⏸ / 1× / 5× / 20×。

**F — 叠加层内飞船确认卡（选中目标飞船后出现，底部弹出）**
显示目标飞船简要信息 + 「立即传送」CTA 按钮。

**G — 切换过渡遮罩（全屏，最高层级）**
所有涉及 CockpitScene 加载/卸载的切换期间显示。

### Component Inventory

**面板 A — 飞船选中面板（星图）**

| 组件 | 类型 | 内容 | 交互 |
|------|------|------|------|
| 飞船名称 + 类型 | Label | 「旗舰 · 星斗号」 | 否 |
| ActiveShip 标识 | Icon（条件显示）| ⚓ 图标，仅当此船为 ActiveShip 时显示 | 否 |
| 飞船位置 | Label（小字） | 「天狼星域 · 节点 #12」 | 否 |
| 血条 | ProgressBar + Label | `CurrentHull / MaxHull` | 否 |
| 「进入驾驶舱」按钮 | Button（全宽） | 默认：「进入驾驶舱」；DESTROYED：「飞船已销毁」 | 是 |
| ShipState 提示标签 | Label（条件） | 按钮禁用时显示，如「飞船正在战斗中」 | 否 |

**面板 B — 驾驶舱 HUD 出口**

| 组件 | 类型 | 内容 | 交互 |
|------|------|------|------|
| 「返回星图」按钮 | Button | ← 返回星图 | 是 |
| 「开叠加层」按钮 | Button（图标） | 雷达/星图图标 | 是 |

**面板 E — 时间控制条**

| 组件 | 类型 | 内容 | 交互 |
|------|------|------|------|
| SimRate 状态标签 | Label | 「⏸」/ 「1×」/ 「5×」/ 「20×」 | 否 |
| SimRate 切换按钮 | ButtonGroup | 四档切换 | 是 |

**面板 F — 叠加层飞船确认卡**

| 组件 | 类型 | 内容 | 交互 |
|------|------|------|------|
| 飞船名称 + 状态 | Label | 「巡洋舰 · 银河号 · 航行中」 | 否 |
| 血条 | ProgressBar | `CurrentHull / MaxHull` | 否 |
| 「立即传送」按钮 | Button（全宽，高亮） | 「立即传送」 | 是 |
| 禁用说明 | Label（条件） | 「战斗中无法换船」等 | 否 |

### ASCII Wireframe

**星图层 — 飞船选中面板（手机竖屏底部）**

```
┌─────────────────────────────────┐
│         [  星图主视口  ]          │    [⏸ 1× 5× 20×]  ← 时间控制条
│                                 │
│                                 │
├─────────────────────────────────┤
│  ⚓ 旗舰 · 星斗号      [×关闭]  │  ← ⚓ = ActiveShip 标识
│  天狼星域 · 节点 #12             │
│  ████████████░░░░  75 / 100 HP  │
│  ┌─────────────────────────┐    │
│  │      进入驾驶舱          │    │
│  └─────────────────────────┘    │
│  [飞船正在战斗中 — 战斗结束可进入] │
└─────────────────────────────────┘
```

**驾驶舱层 — HUD 布局**

```
┌──────────────────────────────────────┐
│ [← 返回星图]         [⏸ 1× 5× 20×] [🗺] │  ← 左上返回 / 右上时间+叠加层
│                                      │
│         [  驾驶舱视口  ]              │
│                                      │
│   [血条]  [速度表]  [准星]             │
└──────────────────────────────────────┘
```

**驾驶舱 + 叠加层（COCKPIT_WITH_OVERLAY）**

```
┌──────────────────────────────────────┐
│ [← 返回星图]         [⏸ 1× 5× 20×] [×] │
│                       ┌────────────┐ │
│   [驾驶舱仍在渲染]    │ 星图叠加层  │ │
│                       │  (70% 宽)  │ │
│                       │  可滚动缩放 │ │
│                       │            │ │
│   [血条] [速度表]     │ ┌────────┐  │ │
│                       │ │立即传送│  │ │  ← 选中目标船后显示
│                       └─┴────────┴──┘ │
└──────────────────────────────────────┘
```

---

## States & Variants

| 状态 / 变体 | 触发条件 | UI 变化 |
|------------|---------|--------|
| **默认（可进入）** | ShipState ∈ {DOCKED, IN_TRANSIT}，`!_isSwitching` | 按钮蓝色可点击 |
| **禁用 — 战斗中** | ShipState = IN_COMBAT | 按钮灰色；显示「飞船正在战斗中」 |
| **禁用 — 已销毁** | ShipState = DESTROYED | 按钮灰色，文字「飞船已销毁」 |
| **禁用 — 切换锁定** | `_isSwitching = true` | 两个切换按钮灰色 + 加载指示器 |
| **切换遮罩中** | SWITCHING_IN / SWITCHING_OUT / SWITCHING_SHIP | 全屏黑色遮罩；所有交互禁用 |
| **叠加层打开** | COCKPIT_WITH_OVERLAY | 叠加面板可见；驾驶舱继续渲染（背景可见）；「开叠加层」按钮变为「关闭」状态 |
| **叠加层 — 战斗警告** | COCKPIT_WITH_OVERLAY AND ActiveShip.IN_COMBAT | 叠加层顶部显示战斗警告 Banner；「立即传送」按钮置灰 |
| **SimRate > 1x 提示** | SimRate ∈ {5, 20} | 时间控制条高亮（黄色边框）；进入驾驶舱时显示 Toast「策略层当前 Nx 加速中」 |
| **强制中断** | 切换中飞船被摧毁（GDD E-1） | 遮罩渐出；Toast「飞船已在切换中被摧毁」 |

---

## Interaction Map

输入方式：纯触屏 Android，无手柄，无键盘。

| 交互元素 | 操作 | 即时反馈 | 结果 |
|---------|------|---------|------|
| 「进入驾驶舱」（可用） | Tap | scale 0.95 + 触觉 10ms | `RequestEnterCockpit(shipId)` |
| 「进入驾驶舱」（禁用 IN_COMBAT） | Tap | 否定音效 | 无变更 |
| 「进入驾驶舱」（禁用其他） | Tap | 无响应 | 无变更 |
| 「返回星图」（可用） | Tap | scale 0.95 + 触觉 10ms | `RequestReturnToStarMap()` |
| 「返回星图」（`_isSwitching`） | Tap | 无响应 | 无变更 |
| 「开叠加层」（可用） | Tap | scale 0.95 | `RequestOpenOverlay()` |
| 叠加层关闭按钮 | Tap | 叠加层向右滑出 | `RequestCloseOverlay()` |
| 叠加层背景区域（Tap 非交互区） | Tap | 无 | 无（不关闭叠加层）|
| 叠加层飞船节点 | Tap | 确认卡弹出（底部滑入） | 显示飞船信息 + 「立即传送」 |
| 「立即传送」（可用） | Tap | scale 0.95 + 触觉长振 20ms | `RequestSwitchShip(targetId)` |
| 「立即传送」（禁用） | Tap | 无响应 | 无变更 |
| SimRate 按钮（任意） | Tap | 按钮高亮切换 | `SimClock.SetRate(rate)` |
| 飞船选中面板 [×关闭] | Tap | 面板向下滑出 | 取消选中，面板收起 |

### 触控热区规格

| 组件 | 最小热区 | 建议尺寸 |
|------|---------|---------|
| 「进入驾驶舱」/ 「立即传送」按钮 | 全宽 × 48dp | 全宽 × 56dp |
| 「返回星图」按钮 | 48 × 48dp | 64 × 48dp |
| 「开叠加层」按钮 | 48 × 48dp | 48 × 48dp |
| 叠加层关闭按钮 | 44 × 44dp | 48 × 48dp |
| SimRate 单个按钮 | 44 × 44dp | 48 × 44dp |
| [×关闭] | 44 × 44dp | 48 × 48dp |

---

## Events Fired

| 玩家操作 | 触发事件 | Payload |
|---------|---------|--------|
| 点击「进入驾驶舱」（成功） | `ViewLayerManager.RequestEnterCockpit` | `shipId` |
| 切换完成 → 进入驾驶舱 | `OnViewLayerChanged(COCKPIT)` | `ViewLayer` |
| 点击「返回星图」（成功） | `ViewLayerManager.RequestReturnToStarMap` | 无 |
| 切换完成 → 返回星图 | `OnViewLayerChanged(STARMAP)` | `ViewLayer` |
| 点击「开叠加层」 | `ViewLayerManager.RequestOpenOverlay` | 无 |
| 叠加层打开完成 | `OnViewLayerChanged(COCKPIT_WITH_OVERLAY)` | `ViewLayer` |
| 叠加层关闭完成 | `OnViewLayerChanged(COCKPIT)` | `ViewLayer` |
| 点击「立即传送」（成功） | `ViewLayerManager.RequestSwitchShip` | `targetShipId` |
| 跳船完成 | `OnActiveShipChanged` | `newShipId` |
| SimRate 变更 | `SimClock.SetRate` | `rate` |
| 切换中断 | `OnSwitchAborted` | `reason`, `shipId` |

> **持久化状态变更**：`RequestEnterCockpit` 和 `RequestSwitchShip` 均写入 ShipDataModel 并修改 ActiveShipId（存档数据）。`SimClock.SetRate` 修改 SimRate（存档数据）。

---

## Transitions & Animations

| 动画 | 触发 | 时长 | 形式 | Reduce Motion |
|------|------|------|------|---------------|
| 面板 A 滑入 | 飞船被选中 | 200ms | 底部向上，ease-out | 即时显示 |
| 面板 A 滑出 | 点击关闭 | 200ms | 向下，ease-in | 即时隐藏 |
| 进入/返回遮罩渐入 | `_isSwitching = true` | 300ms | 全屏黑 → 不透明，linear | 即时切黑 |
| 进入/返回遮罩渐出 | 场景激活完成 | 200ms | 不透明 → 透明 | 即时清除 |
| 跳船遮罩渐入 | SWITCHING_SHIP 开始 | 200ms | 全屏黑 → 不透明，更快 | 即时切黑 |
| 叠加层滑入 | OPENING_OVERLAY | 300ms | 从右侧滑入，ease-out | 即时显示 |
| 叠加层滑出 | CLOSING_OVERLAY | 200ms | 向右侧滑出，ease-in | 即时隐藏 |
| 确认卡弹入 | 叠加层内选中飞船 | 150ms | 底部弹入 | 即时显示 |
| 按钮按下反馈 | Tap | 50ms | scale 0.95 → 1.0 | 保留 |

**遮罩颜色**：`#000000`，符合美术圣经「黑暗是画布」原则。

**Reduce Motion 规则**：`AccessibilitySettings.ReduceMotion = true` 时，所有滑入/渐变动画替换为即时切换；按钮 scale 反馈保留。

---

## Data Requirements

| 数据 | 来源系统 | 读/写 | 备注 |
|------|---------|------|------|
| `shipId` / `targetShipId` | 星图 UI 选中事件 | 读 | 切换目标 |
| `ActiveShipId` | ShipDataModel（MasterScene） | 读/写 | 进入/跳船时写入 |
| `ShipState` | ShipDataModel | 读 | 按钮可用性，订阅事件驱动 |
| `CurrentHull / MaxHull` | ShipDataModel | 读 | 血条显示 |
| 飞船名称 / 类型 | ShipBlueprintRegistry（SO） | 读 | 静态配置 |
| 飞船位置 | StarMapData（MasterScene） | 读 | 节点名 |
| `_isSwitching` | ViewLayerManager | 读 | 按钮禁用状态 |
| 星图摄像机位置/缩放 | ViewLayerManager | 读/写 | 进入前记录，返回时恢复 |
| `CurrentHull`（退出时） | CockpitScene → MasterScene | **写** | 回写存档数据 |
| 飞船位置（退出时） | CockpitScene → MasterScene | **写** | 回写存档数据 |
| `SimRate` | SimClock（MasterScene） | 读/写 | 时间控制条双向绑定 |
| `AccessibilitySettings.ReduceMotion` | AccessibilitySettings | 读 | 动画控制 |

---

## Accessibility

基于 `design/accessibility-requirements.md` Standard 层级。

| 要求 | 来源 | 实现方式 |
|------|------|---------|
| 触控热区 ≥ 48dp | AC-A11Y-03 | 所有主要按钮 ≥ 48 × 48dp（见触控热区规格表） |
| 相邻目标间距 ≥ 8dp | Standard 层 | 驾驶舱右上角「时间控制条」与「叠加层按钮」间距 ≥ 8dp |
| Reduce Motion | AC-A11Y-02 | 所有滑入/渐变动画即时切换；按钮 scale 保留 |
| 状态不依赖颜色 | Standard 层 | 禁用状态通过文字标签传达；SimRate 高亮通过标签数字区分 |
| 字体大小 | Standard 层 | 主按钮 ≥ 16sp；状态标签 ≥ 14sp；位置小字 ≥ 12sp |
| 系统字体缩放 1.3× | AC-A11Y-04 | 面板最小高度 + 弹性扩展；「立即传送」全宽按钮文字单行 |
| 音频信息配对视觉 | Standard 层 | 拒绝音效配文字标签；SimRate 变更音配状态标签变化 |

---

## Localization Considerations

| 元素 | 简中字符数 | 风险 | 处理方式 |
|------|----------|------|---------|
| 「进入驾驶舱」 | 6 字 | 低 | 全宽按钮 |
| 「立即传送」 | 4 字 | 低 | 全宽按钮，充足空间 |
| 「返回星图」 | 4 字 | **中** | 溢出时仅显示 ← 图标 |
| 「飞船正在战斗中 — 战斗结束可进入」 | 16 字 | **高** | 允许两行；面板弹性高度 |
| 「战斗中无法换船」 | 7 字 | 低 | 确认卡内，空间充足 |
| SimRate 按钮文字（⏸/1×/5×/20×） | 符号/数字 | 低 | 不需本地化 |

---

## Acceptance Criteria

- [ ] **AC-UX-PS-01** [性能] 点击「进入驾驶舱」后，遮罩在 ≤100ms 内开始渐入；完整切换在 ≤1000ms 内完成。
- [ ] **AC-UX-PS-02** [导航] 进入驾驶舱后，「返回星图」和「开叠加层」按钮均可见且热区 ≥ 48dp。
- [ ] **AC-UX-PS-03** [导航] 点击「返回星图」→ 星图摄像机恢复至进入前的位置/缩放（误差 ≤ 1 帧）。
- [ ] **AC-UX-PS-04** [禁用状态] IN_COMBAT 时「进入驾驶舱」按钮灰色 + 文字提示，点击播放否定音效。
- [ ] **AC-UX-PS-05** [禁用状态] DESTROYED 时按钮文字「飞船已销毁」，灰色，点击无响应。
- [ ] **AC-UX-PS-06** [无障碍] ReduceMotion = true 时，所有切换/叠加层动画即时执行，按钮 scale 反馈保留。
- [ ] **AC-UX-PS-07** [无障碍] Android 字体缩放 1.3× 下，所有面板文字完整显示，无截断。
- [ ] **AC-UX-PS-08** [中断] 切换中飞船被摧毁时，遮罩渐出，Toast 显示「飞船已在切换中被摧毁」。
- [ ] **AC-UX-PS-09** [叠加层] 点击「开叠加层」后，驾驶舱视图仍可见（叠加层透明背景），叠加层面板从右侧滑入。
- [ ] **AC-UX-PS-10** [叠加层] 叠加层打开期间，飞船物理继续（血条数据实时更新，或在 SimRate=0 时停止）。
- [ ] **AC-UX-PS-11** [叠加层] 叠加层内选中己方飞船后，确认卡从底部弹出，「立即传送」CTA 可见，热区 ≥ 全宽 × 48dp。
- [ ] **AC-UX-PS-12** [跳船] 点击「立即传送」后，遮罩渐入；切换完成后 ViewLayer 仍为 COCKPIT（不经过 STARMAP）；新 ActiveShip 标识出现在叠加层/星图上。
- [ ] **AC-UX-PS-13** [时间控制] 点击 SimRate 按钮，对应按钮高亮，标签即时更新（SimRate 切换 ≤ 1 帧）。
- [ ] **AC-UX-PS-14** [ActiveShip 标识] 星图视图和叠加层中，ActiveShip 节点有 ⚓ 图标，区别于其他己方飞船。

---

## Open Questions

- **OQ-UX-01（过渡动画内容）**：MVP 纯黑遮罩。Vertical Slice 阶段是否需要叙事性特效？待创意总监确认。对应 GDD Q-1。

- **OQ-UX-02（叠加层战斗中可见性）**：当驾驶舱 VFX 密集时，叠加层按钮需要最低不透明度保证？待真机测试确认。对应 GDD Q-6。

- **OQ-UX-03（SimRate 进入驾驶舱 Toast）**：SimRate > 1× 时进入驾驶舱显示 Toast「策略层当前 Nx 加速中」，Toast 持续时间建议 3s，是否需要可配置？待产品确认。
