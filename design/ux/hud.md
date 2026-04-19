# UX Spec: 驾驶舱 HUD (Cockpit HUD)

> **Status**: In Design
> **Author**: UX Designer
> **Last Updated**: 2026-04-18
> **Journey Phase(s)**: 核心游戏循环 — 驾驶舱战斗
> **GDD Source**: `design/gdd/ship-hud.md`, `docs/architecture/adr-0019-ship-hud-architecture.md`
> **Template**: UX Spec

---

## Purpose & Player Need

驾驶舱 HUD 是玩家在战斗中的态势感知层，让玩家无需离开操控流就能实时了解自身状态。

**核心需求**：
- 实时了解飞船 Hull（生命值）状态
- 了解武器冷却进度（何时可以开火）
- 了解当前速度
- 识别软锁定目标（准星）
- 了解当前战斗状态（IN COMBAT / VICTORY / DEFEAT）
- 随时退出驾驶舱返回星图

**非需求**：
- HUD 不修改任何游戏状态
- HUD 不控制飞船操控（操控由双虚拟摇杆负责）
- 武器自动开火，不需要手动射击按钮

---

## Player Context on Arrival

### 进入驾驶舱时
玩家从星图 Tap「进入驾驶舱」进入。此时 HUD 从隐藏变为显示，战斗可能在数秒内触发。HUD 初始状态：Hull 满，冷却条满，战速 0，战斗指示器隐藏。

### 战斗中
- Hull 随受伤减少，颜色从绿→黄→红
- 冷却条随开火逐渐填充，满时变绿
- 速度随移动实时变化
- 软锁定目标出现时准星跟随目标
- 敌人被锁定时准星出现

### 战斗结束时
- VICTORY：显示「VICTORY」2 秒后淡出，Hull 保持不变
- DEFEAT：显示「DEFEAT」2 秒后淡出，画面切换

---

## Navigation Position

```
驾驶舱屏幕
├── 左上角        [返回星图]  ← 退出驾驶舱
├── 右上角        [暂停]      ← 打开暂停菜单
│
├── 屏幕左侧中央   [左虚拟摇杆区域]
│                  （触控区，非 HUD 元素）
│
├── 屏幕右侧      [武器冷却条]  ← 右下角，垂直条
│                  [右虚拟摇杆区域]
│                  （触控区，非 HUD 元素）
│
├── 屏幕下方      [Hull 条]  ← 底部中央，水平条
│                  [速度值]
│
└── 准星层        [软锁定准星] ← 跟随锁定目标的世界位置
                  [战斗指示器] ← 屏幕中央上侧
```

---

## Entry & Exit Points

| 入口 | 触发条件 | HUD 行为 |
|------|---------|---------|
| 进入驾驶舱 | 星图→驾驶舱切换完成 | HUD 淡入（300ms） |
| 战斗触发 | 敌人进入射程 | 「COMBAT IN」显示 |

| 出口 | 目的地 | HUD 行为 |
|------|--------|---------|
| Tap [返回星图] | 星图视图 | HUD 淡出（200ms） |
| Tap [暂停] | 暂停菜单 | HUD 保持可见（暂停状态） |
| 战斗失败 | 主菜单 | HUD 随场景切换消失 |

---

## Layout Specification

```
┌─────────────────────────────────────┐
│[←星图]                      [⏸暂停] │
│                                     │
│                                     │
│              ╳ 准星                  │  ← 屏幕中央偏上，随目标移动
│            [COMBAT IN]              │  ← 战斗指示器（战斗中显示）
│                                     │
│  ←左摇杆→              [====]       │  ← 武器冷却条（右侧垂直）
│  (触控区)              冷却: 1.0s   │
│                         100%         │
│                                     │
│───────────────────────────────────── │
│  ████████████░░░░  280/400  [Hull] │  ← Hull 条，底部
│  速度: 25 m/s                        │
└─────────────────────────────────────┘
```

### HUD 组件布局（屏幕坐标系）

| 组件 | 位置 | 尺寸 | 说明 |
|------|------|------|------|
| [返回星图] | 左上角 (16dp, 16dp) | 48×48dp 热区 | 返回按钮 |
| [暂停] | 右上角 (-64dp, 16dp) | 48×48dp 热区 | 暂停按钮 |
| Hull 条 | 底部中央，距底 80dp | 宽=屏宽×0.6，高=20dp | 血条填充 |
| Hull 数值 | Hull 条左侧 | 文字 14sp | "280/400" |
| 速度显示 | Hull 条下方，距底 56dp | 文字 14sp | "速度: XX m/s" |
| 武器冷却条 | 右侧，距右 24dp，居中 | 宽=16dp，高=120dp | 垂直填充条 |
| 冷却数值 | 冷却条左侧 | 文字 12sp | "1.0s / 100%" |
| 战斗指示器 | 屏幕顶部中央，距顶 100dp | 文字 24sp，中黑体 | COMBAT IN / VICTORY / DEFEAT |
| 准星 | 跟随 SoftLockTarget 世界位置 | 40×40dp | 使用 Camera.WorldToScreenPoint 投影 |

### 颜色常量（来自 ADR-0019）

| 元素 | 条件 | 颜色值 |
|------|------|--------|
| Hull bar | ratio > 50% | #33E666 (绿色) |
| Hull bar | 25% < ratio ≤ 50% | #FF991A (警告黄) |
| Hull bar | ratio ≤ 25% | #FF2626 (危险红) |
| Cooldown bar | progress ≥ 1.0 (就绪) | #00FFAA |
| Cooldown bar | progress < 1.0 (充能中) | #808080 |
| 战斗指示器文字 | — | #FFFFFF，带黑色描边 |
| 按钮文字 | — | #FFFFFF |

### 战斗指示器

| 状态 | 显示文字 | 动画 |
|------|---------|------|
| 战斗开始 | COMBAT IN | 淡入，2.0s 后 alpha→0 |
| 战斗胜利 | VICTORY | 淡入，2.0s 后 alpha→0 |
| 战斗失败 | DEFEAT | 淡入，保持直到场景切换 |

---

## States & Variants

### State 1: Idle（和平飞行）

```
- Hull bar: 满（绿色）
- Speed: 实时速度
- Cooldown: 满（绿色，已就绪）
- 战斗指示器: 隐藏
- 准星: 隐藏
```

### State 2: In Combat（战斗中）

```
- Hull bar: 实时比例（绿/黄/红）
- Speed: 实时速度
- Cooldown: 实时进度（绿/灰）
- 战斗指示器: 「COMBAT IN」（战斗中显示，2s 淡出）
- 准星: 跟随 SoftLockTarget 位置
```

### State 3: Victory（战斗胜利）

```
- Hull bar: 保持当前值
- Speed: 0（战斗结束停止移动）
- Cooldown: 满
- 战斗指示器: 「VICTORY」（2s 淡出）
- 准星: 隐藏
```

### State 4: Defeat（战斗失败）

```
- Hull bar: 0（红色）
- Speed: 0
- Cooldown: 空
- 战斗指示器: 「DEFEAT」（保持）
- 准星: 隐藏
- 后续: 场景切换到主菜单
```

### State 5: Paused（暂停中）

```
- 所有元素保持最后状态（静态）
- 暂停菜单覆盖在上方
```

---

## Interaction Map

```
HUD 层
├── [返回星图] — Tap → ViewLayerChannel.RaiseExitCockpit() → 切换到星图
├── [暂停]     — Tap → PauseRequested → 暂停菜单打开
│
├── Hull 条    — 无交互（纯显示）
├── Speed      — 无交互（纯显示）
├── Cooldown   — 无交互（纯显示）
├── 准星       — 无交互（纯显示，跟随目标）
└── 战斗指示器 — 无交互（纯显示）

注：武器开火是自动的（aim_angle ≤ 阈值），不需要 HUD 按钮
```

---

## Events Fired

| 事件 | 参数 | 触发时机 |
|------|------|---------|
| `OnCockpitHUDOpened` | — | 驾驶舱视图激活，HUD 淡入 |
| `OnReturnToStarMapClicked` | — | Tap 返回星图按钮 |
| `OnPauseClicked` | — | Tap 暂停按钮 |
| `OnHullChanged` | `(float ratio, float current, float max)` | Hull 值变化（订阅 HealthSystem） |
| `OnCooldownReady` | — | 冷却条满（进度≥1.0） |
| `OnCombatStarted` | — | CombatChannel.RaiseBegin() |
| `OnVictory` | — | CombatChannel.RaiseVictory() |
| `OnDefeat` | — | CombatChannel.RaiseDefeat() |

**HUD 不广播任何事件** — 是纯显示组件。

---

## Transitions & Animations

| 动画 | 时长 | 缓动 | 说明 |
|------|------|------|------|
| HUD 淡入 | 300ms | ease-out | 进入驾驶舱 |
| HUD 淡出 | 200ms | ease-in | 退出驾驶舱 |
| Hull bar 颜色变化 | 实时 | 无缓动 | 颜色阈值切换立即 |
| Cooldown bar 填充 | 实时 | 无缓动 | 随 fireTimer 实时更新 |
| 准星跟随 | 实时 | 无缓动 | Camera.WorldToScreenPoint 每帧更新 |
| 战斗指示器淡入 | 100ms | ease-out | 出现时 |
| 战斗指示器淡出 | 500ms | linear | 2.0s 后开始 alpha 衰减 |

**性能要求**：所有动画使用 CanvasGroup.alpha 或 Image.fillAmount，不使用动画组件（Animator）。

---

## Data Requirements

| 数据 | 来源 | 更新频率 |
|------|------|---------|
| Hull ratio | `HealthSystem.HullRatio(instanceId)` | 每帧（订阅 OnHullChanged 事件） |
| Speed | `ShipControlSystem.GetHorizontalSpeed()` | 每帧（Update 轮询） |
| Cooldown progress | `CombatSystem.FireCooldownProgress` | 每帧（Update 轮询） |
| SoftLock target | `ShipControlSystem.SoftLockTarget` | 每帧（Update 轮询） |
| Combat state | `CombatChannel` 订阅 | 事件触发 |
| ViewLayer | `ViewLayerChannel` 订阅 | 事件触发 |

---

## Accessibility

| 要求 | 实现 |
|------|------|
| A1: 热区 | 所有触控按钮热区 ≥48×48dp |
| A2: 色盲安全 | Hull 颜色变化同时有文字数值（280/400）；不使用纯色作为唯一指示 |
| A3: 文字缩放 | 所有文字支持系统字体缩放，最小 14sp |
| A4: 震动反馈 | 战斗开始/胜利/失败时触发中等震动 |
| 对比度 | 所有文字与背景对比度 ≥4.5:1 |
| A5: 屏幕阅读 | 战斗状态变化时播报（预留） |

**额外无障碍**：Hull ≤ 25% 时添加屏幕红色闪烁效果（2Hz），提醒玩家注意。

---

## Localization Considerations

| 字符串 | 中文 | 英文 |
|--------|------|------|
| 返回星图 | 返回星图 | Return to Star Map |
| 暂停 | 暂停 | Pause |
| COMBAT IN | 战斗中 | COMBAT IN |
| VICTORY | 胜利 | VICTORY |
| DEFEAT | 失败 | DEFEAT |
| Hull 标签 | 船体 | Hull |
| 速度标签 | 速度 | Speed |
| 单位 m/s | m/s | m/s |
| 冷却就绪 | 就绪 | Ready |
| 冷却充能中 | 充能 | Charging |

---

## Acceptance Criteria

- [ ] 驾驶舱激活时 HUD 在 300ms 内淡入
- [ ] Hull 满时显示绿色（#33E666），≤50% 显示黄色（#FF991A），≤25% 显示红色（#FF2626）
- [ ] Hull 条变化时有对应文字数值（current/max）
- [ ] 武器冷却条实时更新，≥1.0 时显示绿色（#00FFAA），<1.0 显示灰色（#808080）
- [ ] 速度值每帧更新（m/s 单位）
- [ ] 软锁定目标在屏幕上有准星跟随其位置
- [ ] 「COMBAT IN」在战斗开始时显示，2.0s 后淡出
- [ ] 「VICTORY」在战斗胜利时显示，2.0s 后淡出
- [ ] 「DEFEAT」在战斗失败时显示，保持直到场景切换
- [ ] [返回星图] 按钮在左上角，Tap 后退出驾驶舱
- [ ] [暂停] 按钮在右上角，Tap 后打开暂停菜单
- [ ] 所有触控按钮热区 ≥48×48dp
- [ ] 暂停时 HUD 保持可见（静态）
- [ ] 星图视图时 HUD 隐藏
- [ ] 文字支持系统字体缩放

---

## Open Questions

| # | 问题 | 状态 | 备注 |
|---|------|------|------|
| 1 | Hull ≤ 25% 红色闪烁是否需要？ | 开放 | 可作为可选无障碍功能 |
| 2 | 准星大小是否需要随距离变化？ | 开放 | MVP 固定 40px |
| 3 | 多个软锁定目标时如何显示？ | 开放 | MVP 仅支持单一软锁定 |
| 4 | TPV（第三人称）是否需要单独的 HUD？ | 开放 | 暂不在本规格范围内 |
