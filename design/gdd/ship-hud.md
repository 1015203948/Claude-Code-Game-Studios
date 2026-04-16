# 飞船 HUD (Ship HUD)

> **Status**: Designed (pending /design-review)
> **Author**: game-designer (Claude Code) + user
> **Last Updated**: 2026-04-13
> **Implements Pillar**: 支柱4 从星图到驾驶舱 / 支柱2 一目了然的指挥

## Overview

飞船 HUD（Ship HUD）是《星链霸权》驾驶舱层的**态势感知显示层**：它将来自三个上游系统的实时状态数据——`CurrentHull`（飞船生命值系统）、软锁定目标与开火状态（飞船操控系统）、战斗状态机事件（飞船战斗系统）——聚合渲染为驾驶舱屏幕上玩家可直接读取的视觉信息界面。

从数据层看，HUD 是纯粹的只读渲染层：它订阅事件（`OnHullChanged`、`OnLockAcquired`、`OnLockLost`、`OnShipDying`），从物理层轮询速度值（`Rigidbody.linearVelocity`），但不持有任何游戏状态，不执行任何逻辑判断。从玩家感受层看，它是「驾驶舱存在感」的视觉锚点：血条缩短时直觉告诉你危险，锁定准星出现时直觉告诉你可以开火，速度表的数字告诉你正在以多快的速度刺破星图的虚空——这些感受必须在高速机动中即时可读，不能要求玩家停下来思考。

玩家与 HUD 的交互是混合式的：绝大多数元素是**被动**的（事件驱动自动更新，玩家只读），但视角切换按钮是一个**主动**交互点，允许玩家在第三人称与第一人称视角之间切换，且该按钮必须在战斗状态下也可触达。MVP 阶段 HUD 覆盖以下核心元素：**血条**（`CurrentHull / MaxHull`）、**速度表**（`Rigidbody.linearVelocity` 模长）、**软锁定准星**（跟随敌方目标，颜色 `#00FFAA`）、**视角切换按钮**（第三人称 ↔ 第一人称）、**战斗状态指示**（进入/退出 `IN_COMBAT` 时的视觉反馈）。经济信息（矿石/能源）不显示在驾驶舱 HUD 内——战场不是记账的地方。

若没有飞船 HUD，驾驶舱体验将同时失去两样东西：其一是**战斗的可读性**——血量归零是随机事件还是可预测的压力曲线，玩家无从判断；锁定准星不显示，自动开火对玩家而言是黑盒，机动走位失去反馈闭环。其二是**驾驶舱临场感的视觉锚点**——从星图切换到驾驶舱时，若没有专属的信息界面，两个视角在视觉上是可互换的，「跳进去」的仪式感消失，支柱4（从星图到驾驶舱）的承诺就落空了。

## Player Fantasy

飞船 HUD 的玩家幻想是**「驾驶舱里的帝国神经中枢」**——你不是一个孤独的飞行员在读仪表盘，你是一个帝国的大脑，暂时将意识压缩进了这艘船的感知接口。

**你比战场提前一步**

血条在你视野的左上角，不是数字，是弧线——你用余光扫一眼就知道还剩多少边距，就像你不需要看时钟就知道会议快结束了。锁定准星从无到有地出现在敌舰轮廓上，颜色从灰白跳到 `#00FFAA` 的那一刻，你知道武器已经就绪——甚至在武器开火之前，那个颜色已经告诉你：这场遭遇战，胜算在我。HUD 给你的不是信息，是**早一步的判断**。

**帝国在这里呼吸**

你切进驾驶舱，但你的帝国没有暂停。速度表跳动的数字告诉你这艘船花了多少矿石建造——`SHIP_MAX_SPEED` 不是参数，是你当初批下矿石预算的结果，现在它以 m/s 的形式在你眼前流动。血条不只是「这艘船的状态」，它是「这艘船还值多少」的无声报告。你在战场上用余光读 HUD，感受到的不是数字压力，而是**整个帝国在支撑你这一刻的重量**——那些矿石、能源、建造时间，全部凝缩在这几道光里。

**什么都不多余，什么都不缺**

MVP 阶段的飞船 HUD 是被刻意克制过的：没有雷达、没有地图、没有队友状态。你能看到的每一个元素，都是战场决策必须知道的：*我还能撑多久？我锁定目标了吗？我飞得有多快？我想切换视角吗？* 四个问题，四个答案，没有第五个。这种克制不是设计的妥协，而是支柱2「一目了然的指挥」在驾驶舱层的直接实现——驾驶舱的信息密度必须低于星图，因为手速比星图更高。

**支柱对齐：**
- **支柱2（一目了然的指挥）**：四个核心元素覆盖四个战场决策问题；无冗余信息；高速机动中可用余光读取
- **支柱4（从星图到驾驶舱）**：HUD 是两层体验切换的「签名」——玩家看到它，就知道自己「在里面了」；速度表的数字连接着星图层的经济重量
- **支柱1（经济即军事）**：血条残缺的每一格都是帝国的损耗；速度表的数值是矿石预算的具现

**锚点时刻**：战斗刚开始，你注意到血条已经少了一截——上一场战斗留下的伤痕（来自生命值系统的跨战斗持久化）。你没有时间在意，因为锁定准星刚刚亮起，你的武器开始自动开火。你用余光维持着三个信息点的频率：血条在哪里、准星是什么颜色、速度表说我飞得够快吗。这三个余光扫视不是意识行为——它们是驾驶员在高压下的呼吸节奏，而 HUD 就是这个节奏的视觉载体。

## Detailed Design

### Core Rules

#### 显示状态门控规则

**规则 H-1（HUD 激活窗口）**
HUD 仅在飞船状态 ∈ {`IN_COCKPIT`, `IN_COMBAT`} 时完整显示。状态为 `DOCKED` / `IN_TRANSIT` / `DESTROYED` 时，HUD 完整元素集隐藏（不接收事件，不渲染）。

**规则 H-2（元素透明度状态）**

| HUD 状态 | 血条 | 速度表 | 准星 | 视角切换 | 战斗状态条 |
|----------|------|--------|------|----------|-----------|
| `IN_COCKPIT`（非战斗） | 60% | 50% | 隐藏 | 70% | 隐藏 |
| `IN_COMBAT` | 90% | 70% | 跟随目标显示 | 70% | 80% |
| `DESTROYED` | 渐出 0.5s | 渐出 0.5s | 立即隐藏 | 渐出 0.5s | 渐出 0.5s |

进入 `IN_COMBAT` 后 200ms 内线性过渡至战斗透明度。

#### 血条规则（Hull Bar）

**规则 HC-1（数据来源）**
血条订阅 `OnHullChanged(instanceId, currentHull, maxHull)` 事件（来自飞船生命值系统）。仅在事件触发时更新，禁止 `Update()` 轮询（TR-HUD-001）。

**规则 HC-2（显示比例）**
血条填充比例 = `health_ratio = CurrentHull / MaxHull`（归一化 0.0–1.0，来自注册表 `health_ratio` 公式）。填充方向：从左向右满血，向左缩减。

**规则 HC-3（颜色阶梯）**

| health_ratio | 填充颜色 | 语义 | 切换方式 |
|---|---|---|---|
| 0.66–1.00 | `#00DD88`（冷绿） | 安全 | 即时切换（无渐变） |
| 0.33–0.65 | `#FFAA00`（琥珀橙） | 警戒 | 即时切换 |
| 0.01–0.32 | `#FF3333`（警告红） | 危险 | 即时切换 |
| ≤ 0.20 | `#FF3333` 闪烁 | 急迫 | 见规则 HC-4 |

**规则 HC-4（危险闪烁）**
`health_ratio ≤ 0.20` 时，血条在 `#FF3333`（100% 不透明）和 `#FF3333`（40% 不透明）之间循环，周期 0.8s（0.4s 亮 → 0.4s 暗）。恢复至 0.21 后立即停止，切换为静止红色。

**规则 HC-5（伤害脉冲）**
`OnHullChanged` 触发且新值 < 旧值时，血条整体叠加白色（`#FFFFFF` 覆盖层，透明度 0% → 60% → 0%，时长 0.2s，Ease Out）。提供即时伤害反馈，不依赖颜色感知。

**规则 HC-6（数字标注条件）**
- **手机**：不显示数值，仅显示比例条
- **平板**：血条右端显示 `CurrentHull` 整数值，12sp，颜色与当前血条颜色一致
- **任意设备**：`health_ratio ≤ 0.32` 进入危险区时，在血条上方 8dp 处显示当前数值，2s 后自动隐藏

#### 速度表规则（Speed Gauge）

**规则 SP-1（数据来源与更新）**
每帧（`Update()`）读取 `Rigidbody.linearVelocity`（Unity 6 API）模长，经阻尼平滑后显示整数。禁止每帧直接显示原始值（会产生 ±0.1–0.3 的数字抖动）。

阻尼公式：`displaySpeed = Lerp(displaySpeed, velocity.magnitude, 0.15)`（每帧执行）

变化阈值过滤：`|Round(velocity.magnitude) - lastDisplayedSpeed| < 0.5` 时跳过 UI 更新，避免无效 GC（TR-HUD-002）。显示格式：`{整数} m/s`（如 `18 m/s`）。

**规则 SP-2（颜色阶梯）**

| 速度区间 | 颜色 | 语义 |
|---------|------|------|
| 0–30% `SHIP_MAX_SPEED` | `#AAAAAA`（灰） | 低速 |
| 31–70% `SHIP_MAX_SPEED` | `#FFFFFF`（白） | 巡航 |
| 71–99% `SHIP_MAX_SPEED` | `#FFDD44`（金黄） | 高速 |
| 100% `SHIP_MAX_SPEED` | `#FF8800`（橙）+ 0.15s 脉冲 | 软上限 |

#### 软锁定准星规则（Soft Lock Reticle）

**规则 SL-1（数据来源）**
准星订阅 `OnLockAcquired(targetId)` 和 `OnLockLost(targetId)` 事件（来自飞船操控系统）。

**规则 SL-2（World Space 跟踪）**
准星使用 UGUI Screen Space Overlay Canvas 实现（TR-HUD-003）。每帧 `LateUpdate()` 执行：
1. `screenPos = Camera.main.WorldToScreenPoint(target.position)`
2. 若 `screenPos.z < 0`（目标在相机背后）→ 立即隐藏准星（TR-HUD-004）
3. 否则：`RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, null, out localPoint)` → 设置 `reticleRect.anchoredPosition = localPoint`

**规则 SL-3（视觉规格继承）**
以下规格继承自飞船操控系统 GDD §Visual/Audio Requirements §2，本 GDD 不重复定义，仅引用：
- 形状：四段弧线（不闭合），围绕目标包围盒 1.3 倍，线宽 2px
- 颜色：`#00FFAA`
- `OnLockAcquired`：0.15s 扩散动画（Ease Out Cubic）
- `OnLockLost`：0.1s 淡出 + `#FF4444` 0.05s 闪烁

#### 视角切换按钮规则（Camera Toggle）

**规则 CAM-1（交互）**
点击切换 `CameraMode`：`THIRD_PERSON` ↔ `FIRST_PERSON`（调用飞船操控系统接口）。

**规则 CAM-2（冷却锁）**
连续两次切换之间强制冷却 `CAMERA_SWITCH_DURATION`（等于操控系统 GDD 的 `CAMERA_ROTATION_SMOOTH` 动画时长 0.15s–0.5s 中取最大值）。冷却期间按钮透明度降至 40%，禁用输入，冷却结束后立即恢复。

**规则 CAM-3（首次战斗提示）**
每次安装首次进入 `IN_COMBAT` 时，按钮旁显示气泡提示「切换视角不会暂停战斗」，淡入 0.2s → 停留 2s → 淡出 0.3s。触发后写入 `PlayerPrefs HUD_CamTip_Shown = true`，永久不再触发。

#### 战斗状态指示规则（Combat State Indicator）

**规则 CS-1（进入 IN_COMBAT）**
状态切换为 `IN_COMBAT` 时，同帧触发两个效果：
- **战斗状态条**：屏幕顶部中央宽 60% 线条（`#FF3333`，高 4dp），从中心向两端展开 0.25s Ease Out Cubic，常驻至退出战斗
- **边缘脉冲**：屏幕四角 Vignette（`#FF3333`，峰值透明度 20%），0→20%→0，总时长 0.4s，仅触发一次（TR-HUD-005）

**规则 CS-2（IN_COMBAT 持续指示）**
战斗状态条左端附加 8dp 圆形指示点（`#FF3333`），1.5s 周期缓慢脉冲（透明度 80%→40%→80%），常驻至退出战斗。

**规则 CS-3（胜利退出）**
`IN_COMBAT` 退出且战斗结果为胜利时：状态条颜色 `#FF3333` → `#00DD88` 0.3s 渐变；随后从两端向中心收缩消失 0.2s；顶部中央显示「战斗结束」文字，12sp，`#00DD88`，淡入 0.2s → 停留 1.5s → 淡出 0.3s。

**规则 CS-4（飞船摧毁）**
飞船状态切换为 `DESTROYED` 时：全屏白色闪光（`#FFFFFF`，0%→80%→0%，0.15s）；随后所有 HUD 元素在 0.5s 内淡出（不得低于 0.3s，保障死亡叙事感）；淡出完成后由死亡系统接管。

**技术约束汇总（TR）**

| ID | 约束 |
|----|------|
| TR-HUD-001 | 血条使用事件驱动，禁止 `Update()` 轮询 |
| TR-HUD-002 | 速度表每帧更新须设 ≥0.5 变化阈值，显示精度为整数 |
| TR-HUD-003 | 准星用 UGUI Screen Space Overlay Canvas + `LateUpdate` 坐标转换 |
| TR-HUD-004 | 准星在 `screenPos.z < 0` 时立即隐藏 |
| TR-HUD-005 | 战斗边缘效果用 URP Volume Override Vignette 或 `AddRenderPasses`；禁止 `SetupRenderPasses`（Unity 6.2 废弃） |
| TR-HUD-006 | 战斗边缘效果在非 `IN_COMBAT` 时禁用 RendererFeature |
| TR-HUD-007 | `VisualElement` 位移/旋转禁止使用 `.transform`（Unity 6.1 废弃），须用 `style.translate` / `style.rotate` |
| TR-HUD-008 | UGUI Canvas 禁止开启 Pixel Perfect 模式 |
| TR-HUD-009 | UI 参考分辨率：1080×1920（竖屏），同时验证 4:3 平板布局 |

#### 星图事件通知徽章规则（Starmap Notification Badge）

**规则 NB-1（显示条件）**
通知徽章仅在以下两个条件同时满足时显示：
1. `ViewLayer == COCKPIT`（驾驶舱视角激活）
2. 存在至少一个未处理的星图事件（舰队抵达、无人值守战斗等）

不满足任一条件时，徽章立即隐藏。

**规则 NB-2（视觉设计）**
- **图标**：⚔️ 加事件计数数字（如 `⚔️ 2`）
- **位置**：驾驶舱 HUD 右上角，与其他 HUD 元素共用同一安全区（距屏幕边缘 ≥ 24dp）
- **尺寸**：触控热区 ≥ 48×48dp（Android 最小触控目标要求）
- **透明度**：跟随 H-2 门控（`IN_COCKPIT` 时 70%，`IN_COMBAT` 时 70%）

**规则 NB-3（交互行为）**
- **点击展开**：点击徽章弹出事件列表浮层，每条显示简要描述（如「舰队 A 抵达 节点-3」）
- **非阻断**：展开浮层期间，驾驶舱操控输入**不受影响**，飞船继续响应移动/战斗操作
- **浮层关闭**：点击徽章以外区域或点击浮层中的「关闭」按钮即可收起；不设超时自动收起

**规则 NB-4（已读/消除逻辑）**
- **手动确认**：玩家在浮层中查看事件后，该事件标记为已读；已读事件不再计入徽章计数
- **返回星图**：玩家切换回星图视角（`ViewLayer == STARMAP`）时，所有当前事件自动标记为已读，徽章重置为 0
- **计数更新**：新事件产生时徽章计数即时更新（不等待玩家操作）

**规则 NB-5（事件来源）**
通知徽章订阅星图系统的事件总线（具体接口待星图 GDD 定义）。当前已知事件类型：
- 舰队抵达指定节点
- 节点发生战斗且无玩家介入

> **依赖说明**：NB-5 接口依赖星图系统 GDD 定义。星图系统 GDD 完成后须反向将 HUD 徽章列为下游依赖。

---

### States and Transitions

| 触发条件 | HUD 状态变化 | 元素行为 |
|---|---|---|
| `ShipState` → `IN_COCKPIT` | 激活 HUD | H-1 门控开放；血条/速度表/视角按钮以非战斗透明度显示；准星隐藏；战斗状态条隐藏 |
| `ShipState` → `IN_COMBAT` | 升级至战斗 HUD | 200ms 过渡至战斗透明度；触发 CS-1 进入动画；准星等待 `OnLockAcquired` |
| `OnLockAcquired(targetId)` | 准星激活 | SL-2 跟踪开始；SL-3 出现动画 |
| `OnLockLost(targetId)` | 准星失活 | SL-3 消失动画；跟踪停止 |
| `OnHullChanged(id, new, max)` | 血条更新 | HC-2 比例刷新；HC-3 颜色判断；HC-5 伤害脉冲（如适用）；HC-4 危险闪烁（如适用） |
| 战斗结果 → `VICTORY` | 胜利退出 | CS-3 胜利动画序列 |
| `ShipState` → `DESTROYED` | HUD 关闭 | CS-4 死亡淡出；H-1 门控关闭 |
| `ShipState` → `DOCKED`/`IN_TRANSIT` | 隐藏 HUD | 所有元素立即隐藏（不触发淡出动画） |

---

### Interactions with Other Systems

| 依赖系统 | 接口方向 | 具体接口 |
|----------|---------|---------|
| **飞船生命值系统** | 订阅事件 | `OnHullChanged(instanceId, currentHull, maxHull)` → 驱动血条 |
| **飞船生命值系统** | 订阅事件 | `OnShipDying(instanceId)` → 触发 CS-4 死亡淡出序列 |
| **飞船操控系统** | 订阅事件 | `OnLockAcquired(targetId)` → 激活准星跟踪 |
| **飞船操控系统** | 订阅事件 | `OnLockLost(targetId)` → 停止准星跟踪 |
| **飞船操控系统** | 读取值 | 每帧读取飞船 `Rigidbody.linearVelocity` → 速度表 |
| **飞船操控系统** | 调用接口 | 视角切换按钮调用 `SetCameraMode(THIRD_PERSON\|FIRST_PERSON)` |
| **飞船操控系统** | 继承规格 | 准星视觉规格直接引用操控系统 GDD §Visual/Audio Requirements §2 |
| **飞船战斗系统** | 订阅事件 | `BeginCombat` / `EndCombat(VICTORY\|DEFEAT)` → 驱动 CS-1/CS-3/CS-4 |
| **飞船系统** | 订阅事件 | `OnStateChanged(instanceId, newState)` → 驱动 H-1 激活/关闭门控 |
| **双视角切换系统** | 被依赖（下游） | 切换系统协调 HUD 的 `CameraMode` 同步，确保视角状态与按钮图标一致 |

## Formulas

### D-HUD-1: health_ratio — 生命值显示比率

> **注**：本公式由 `ship-health-system.md` 权威定义（§D-3）。飞船 HUD 仅读取该值，不重新计算。以下为引用定义。

`health_ratio = CurrentHull / MaxHull`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|----------|--------|------|-------|-------------|
| 当前船体值 | `CurrentHull` | float | 0.0 – MaxHull | 飞船当前剩余船体值；由 `OnHullChanged` 事件推送至 HUD |
| 最大船体值 | `MaxHull` | float | 1.0 – 500.0 | 飞船上限值；MVP 参考值 = 100.0；来自 `ShipData.GetMaxHull(instanceId)` |
| 生命值比率 | `health_ratio` | float | 0.0 – 1.0 | 血条填充宽度的驱动值；0.0 = 空，1.0 = 满 |

**Output Range:** 0.0 到 1.0。`CurrentHull` 由 `apply_damage` 公式（ship-health-system.md §D-4）保证永不低于 0 或超过 MaxHull；除零情况不会在正常游戏中出现。

**HUD 用途：** 血条填充宽度 = `bar_max_width × health_ratio`。颜色状态由 D-HUD-2 基于本值派生。

**Example:** `CurrentHull = 42`，`MaxHull = 100` → `health_ratio = 0.42` → 血条填充至 42% 宽度，颜色 CAUTION（橙）。

---

### D-HUD-2: hull_color_state — 血条颜色阶梯映射

```
hull_color_state =
  if health_ratio >= HUD_HULL_SAFE_THRESHOLD          → SAFE
  if health_ratio >= HUD_HULL_CAUTION_THRESHOLD       → CAUTION
  if health_ratio > HUD_HULL_CRITICAL_PULSE_THRESHOLD → DANGER
  else                                                → CRITICAL
```

**Variables:**
| Variable | Symbol | Type | Range | Description |
|----------|--------|------|-------|-------------|
| 生命值比率 | `health_ratio` | float | 0.0 – 1.0 | 输入值；来自 D-HUD-1（ship-health-system.md §D-3 权威定义） |
| 安全阈值 | `HUD_HULL_SAFE_THRESHOLD` | float（常量） | 0.0 – 1.0 | ≥ 此值时 SAFE；默认值 **0.66** |
| 警戒阈值 | `HUD_HULL_CAUTION_THRESHOLD` | float（常量） | 0.0 – 1.0 | ≥ 此值且 < SAFE 时 CAUTION；默认值 **0.33** |
| 危急脉冲阈值 | `HUD_HULL_CRITICAL_PULSE_THRESHOLD` | float（常量） | 0.0 – 1.0 | ≤ 此值时叠加闪烁动画；默认值 **0.20** |
| 颜色状态 | `hull_color_state` | enum | {SAFE, CAUTION, DANGER, CRITICAL} | 血条当前颜色档位 |

**状态-颜色映射：**
| hull_color_state | health_ratio 条件 | 颜色 | 效果 |
|---|---|---|---|
| SAFE | ≥ 0.66 | `#00DD88` | 静止 |
| CAUTION | 0.33 – 0.65 | `#FFAA00` | 静止 |
| DANGER | 0.21 – 0.32 | `#FF3333` | 静止 |
| CRITICAL | ≤ 0.20 | `#FF3333` | 红色闪烁 0.8s 周期 |

> **阈值独立声明**：生命值系统使用 `HULL_THRESHOLD_DAMAGED = 0.50` / `HULL_THRESHOLD_CRITICAL = 0.25` 控制飞船本体特效和音效；HUD 血条颜色使用本公式的 HUD 专属阈值，**两套阈值独立调节，互不绑定**。

**Output Range:** 枚举值之一，每次 `OnHullChanged` 重新计算，1 帧内即时切换（无过渡动画）。

**Example（边界验证）：**
- `health_ratio = 0.66` → SAFE（绿）；`health_ratio = 0.65` → CAUTION（橙）
- `health_ratio = 0.33` → CAUTION（橙）；`health_ratio = 0.32` → DANGER（红）
- `health_ratio = 0.20` → CRITICAL（红 + 闪烁）；`health_ratio = 0.21` → DANGER（红）

---

### D-HUD-3: display_speed — 速度表显示值（阻尼平滑）

`display_speed_t = Floor( Lerp(display_speed_{t-1}, velocity_magnitude_t, SPEED_LERP_COEFF) )`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|----------|--------|------|-------|-------------|
| 当前帧显示速度 | `display_speed_t` | float→int | 0 – SHIP_MAX_SPEED | 最终输出至 UI 文本的速度值（取整后，m/s） |
| 上一帧显示速度 | `display_speed_{t-1}` | float | 0 – SHIP_MAX_SPEED | 上一帧 Lerp 中间值（保留小数；首帧初始化为 0.0） |
| 实时速度 | `velocity_magnitude_t` | float | 0.0 – ∞ m/s | `Rigidbody.linearVelocity.magnitude`（Unity 6 API） |
| 平滑系数 | `SPEED_LERP_COEFF` | float（常量） | 0.0 – 1.0 | 每帧插值比例；**本 GDD 定义值 = 0.15**（基于 60fps 校准） |

**变化阈值过滤（TR-HUD-002）：** `|Round(velocity_magnitude_t) - lastDisplayedSpeed| < 0.5` 时跳过 UI 更新，避免无效 GC。

**帧率说明：** 系数 0.15 基于 60fps。帧率无关修正公式为 `1 - (1-0.15)^(deltaTime×60)`，MVP 阶段暂缓实现（见 Tuning Knobs TODO）。

**Output Range:** 0 到 `SHIP_MAX_SPEED`（整数）。`velocity_magnitude = 0` 时约 20 帧内衰减至 0。

**Example:**
- `display_speed_0 = 0`，`velocity_magnitude = 80`（SHIP_MAX_SPEED = 100）
- 第 1 帧：`Lerp(0, 80, 0.15) = 12.0` → **12 m/s**
- 第 5 帧：约 **44 m/s**；第 25 帧稳定至 **80 m/s**

---

### D-HUD-4: speed_color_state — 速度颜色阶梯映射

`speed_ratio = velocity_magnitude / SHIP_MAX_SPEED`

```
speed_color_state =
  if speed_ratio <= SPEED_SLOW_THRESHOLD   → SLOW
  if speed_ratio <= SPEED_CRUISE_THRESHOLD → CRUISE
  if speed_ratio < 1.0                     → FAST
  else                                     → MAX
```

**Variables:**
| Variable | Symbol | Type | Range | Description |
|----------|--------|------|-------|-------------|
| 实时速度 | `velocity_magnitude` | float | 0.0 – ∞ m/s | 直接使用 `Rigidbody.linearVelocity.magnitude`（不用 display_speed，保证即时响应） |
| 最大速度 | `SHIP_MAX_SPEED` | float（常量） | >0 m/s | **权威定义在 ship-control-system.md；值 TBD，待原型验证** |
| 速度比率 | `speed_ratio` | float | 0.0 – 1.0+ | 实时速度占最大速度的比例 |
| 慢速阈值 | `SPEED_SLOW_THRESHOLD` | float（常量） | 0.0 – 1.0 | 默认值 **0.30** |
| 巡航阈值 | `SPEED_CRUISE_THRESHOLD` | float（常量） | 0.0 – 1.0 | 默认值 **0.70** |
| 颜色状态 | `speed_color_state` | enum | {SLOW, CRUISE, FAST, MAX} | 速度指示器当前颜色档位 |

**状态-颜色映射：**
| speed_color_state | speed_ratio 条件 | 颜色 | 效果 |
|---|---|---|---|
| SLOW | 0 – 30% | `#AAAAAA` | 静止 |
| CRUISE | 31% – 70% | `#FFFFFF` | 静止 |
| FAST | 71% – 99% | `#FFDD44` | 静止 |
| MAX | 100%+ | `#FF8800` | 脉冲动画 2Hz |

> **颜色用实时值、数字用平滑值**：颜色响应用 `velocity_magnitude`（即时反映操控），数字显示用 `display_speed`（阻尼平滑避免抖动），两者职责分离。

**Output Range:** 枚举值之一，每帧 Update 重新评估。

**Example（SHIP_MAX_SPEED = 100 m/s）：**
- `velocity_magnitude = 70` → `speed_ratio = 0.70` → **CRUISE**（白）
- `velocity_magnitude = 71` → `speed_ratio = 0.71` → **FAST**（金黄）
- `velocity_magnitude = 100` → `speed_ratio = 1.00` → **MAX**（橙 + 脉冲）

---

### D-HUD-5: reticle_screen_position — 准星屏幕坐标

**步骤 1 — 世界坐标转屏幕坐标：**
`screen_pos = Camera.main.WorldToScreenPoint(target.worldPosition)`

**步骤 2 — 有效性检验：**
`is_visible = (screen_pos.z > 0)`

**步骤 3 — 屏幕坐标转 Canvas 坐标：**
`RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas_rect, screen_pos_xy, null, out canvas_pos)`

**最终输出：**
`reticle_screen_position = canvas_pos`（仅当 `is_visible == true` 时有效）

**Variables:**
| Variable | Symbol | Type | Range | Description |
|----------|--------|------|-------|-------------|
| 目标世界坐标 | `target.worldPosition` | Vector3 | 场景空间任意点 | 锁定目标的 `Transform.position` |
| 屏幕坐标 | `screen_pos` | Vector3 | x:0–Screen.width, y:0–Screen.height, z:任意 | `WorldToScreenPoint` 输出；z > 0 表示目标在相机前方 |
| 可见性标志 | `is_visible` | bool | {true, false} | z ≤ 0 时目标在相机后方，准星隐藏（TR-HUD-004） |
| Canvas 坐标 | `canvas_pos` | Vector2 | Canvas 局部坐标系 | 准星 `RectTransform.anchoredPosition` 的最终赋值 |

**坐标系转换链：**
```
世界空间 (Vector3)
  ↓ Camera.WorldToScreenPoint()
屏幕像素坐标 (Vector3) → z > 0 检验
  ↓ RectTransformUtility.ScreenPointToLocalPointInRectangle()
Canvas 局部坐标 (Vector2)
  ↓ 赋值给
reticle RectTransform.anchoredPosition
```

**Output Range:** Canvas 矩形范围内。目标在相机背后（z ≤ 0）时准星 `SetActive(false)`。

**Example（Canvas 参考分辨率 1920×1080）：**
- 目标在视野正中心 → `screen_pos = (960, 540, 50)` → `canvas_pos ≈ (0, 0)` → 准星在屏幕中央
- 目标在相机左后方 → `screen_pos.z = -12` → `is_visible = false` → 准星隐藏

---

### 公式归属声明

| 公式 ID | 公式名 | 权威来源 | 说明 |
|---------|--------|---------|------|
| D-HUD-1 | `health_ratio` | ship-health-system.md §D-3 | HUD 只读引用，不重新实现 |
| D-HUD-2 | `hull_color_state` | 本文档 | HUD 专属阈值，独立于生命值系统阈值 |
| D-HUD-3 | `display_speed` | 本文档 | `SPEED_LERP_COEFF = 0.15` 为本 GDD 新定义常量 |
| D-HUD-4 | `speed_color_state` | 本文档 | `SHIP_MAX_SPEED` 值由 ship-control-system.md 权威定义，TBD |
| D-HUD-5 | `reticle_screen_position` | 本文档 | `WorldToScreenPoint` + Canvas 坐标两步转换 |

## Edge Cases

- **E-1（同帧 OnHullChanged + OnShipDying）**：`OnHullChanged(newHull=0)` 与 `OnShipDying` 同帧触发时，忽略 `OnHullChanged` 的颜色/闪烁更新，直接进入 CS-4 死亡淡出序列。血条保持最后一次有效颜色冻结，不触发 HC-5 伤害脉冲。`OnShipDying` 为终止状态最高优先级。

- **E-2（OnLockAcquired + OnLockLost 同帧）**：以事件队列中**最后一个**事件为准（Last-Write-Wins）。同帧内不允许中间状态渲染至屏幕；`LateUpdate` 统一消费后只执行最终状态。

- **E-3（MaxHull ≤ 0 除零防御）**：`MaxHull ≤ 0` 时，`health_ratio` 强制钳位为 `1.0f`，血条显示满值绿色，不执行闪烁。输出 `[HUD][ERROR] MaxHull <= 0 on ship {shipId}` 警告。HUD 不应因数据配置错误而崩溃。

- **E-4（SHIP_MAX_SPEED = 0 除零防御）**：`SHIP_MAX_SPEED == 0` 时，`speed_ratio` 强制为 `0.0f`，速度表显示最低色阶（灰色）和数字 `0`。不允许 `NaN` 或 `Infinity` 流入后续计算。输出 `[HUD][ERROR] SHIP_MAX_SPEED is 0`。

- **E-5（HC-5 伤害脉冲连续触发）**：第二次 `OnHullChanged` 在当前脉冲动画（0.2s）未结束时到达：**重置**脉冲计时器至 0，重新开始 0.2s 动画，不叠加多层透明度。单次最长仍为 0.2s，不累计。

- **E-6（胜利动画与死亡淡出同帧争抢）**：若 CS-3（胜利）与 CS-4（死亡）在同一帧触发，**CS-4 优先**——立即取消 CS-3 动画序列（Tween Kill），进入死亡流程。玩家已死，胜利动画不播放。

- **E-7（锁定目标 GameObject 在 OnLockLost 前被销毁）**：`LateUpdate` 执行 `WorldToScreenPoint` 前，先做 `null 检查`（`if (lockedTarget == null)`）。目标为 null 时立即隐藏准星并清除锁定引用，不等待 `OnLockLost` 事件。

- **E-8（CS-4 死亡淡出期间触发视角切换）**：飞船状态为 `DESTROYED` 时，视角切换按钮输入**被吞掉**，不执行任何状态切换，不中断淡出动画。淡出完成后由死亡系统接管，按钮响应在接管后恢复。

- **E-9（HUD 激活时积压 OnHullChanged 事件）**：HUD 从隐藏状态切换至 `IN_COCKPIT` 激活时，**主动拉取一次当前血量快照**（`currentHull / maxHull`）初始化血条，忽略积压队列中的中间状态事件。快照完成后恢复事件驱动模式。

- **E-10（低帧率下速度表滞后）**：正常掉帧（< 30fps）时，Lerp 系数 0.15 不做帧率补偿（已知 trade-off，见 Tuning Knobs TODO）。若 `deltaTime > 0.2s`（极端卡顿/后台恢复），强制 `displaySpeed = velocity.magnitude`，跳过平滑，防止速度表滞后超过 1 秒。

- **E-11（准星目标在屏幕边缘外）**：目标 `z > 0` 但 `WorldToScreenPoint` 返回坐标超出 Canvas 矩形范围时：**隐藏准星**（本 HUD 无边缘箭头指示功能）。边界判断使用 `RectTransform.rect`，不硬编码分辨率。

- **E-12（IN_COMBAT 进出时战斗条动画叠加）**：CS-1 战斗条入场动画未完成时退出再重入 `IN_COMBAT`：立即取消当前动画（`Tween.Kill()`），保持战斗条当前可见状态，**不重新播放**入场动画，直接显示为完全展开态。退出动画同理——退出动画未完成又重入时，取消退出动画，保持展开。

## Dependencies

**飞船 HUD 对外依赖（上游）：**

| 依赖系统 | 依赖类型 | 数据接口 | 所在 GDD |
|----------|---------|---------|---------|
| **飞船生命值系统** | 强依赖 | 订阅 `OnHullChanged(instanceId, currentHull, maxHull)` → 驱动血条；订阅 `OnShipDying(instanceId)` → 触发 CS-4 死亡淡出 | ship-health-system.md |
| **飞船操控系统** | 强依赖 | 订阅 `OnLockAcquired(targetId)` / `OnLockLost(targetId)` → 驱动准星；每帧读取飞船 `Rigidbody.linearVelocity` → 速度表；继承准星视觉规格（§Visual/Audio Requirements §2） | ship-control-system.md |
| **飞船战斗系统** | 强依赖 | 订阅 `BeginCombat` / `EndCombat(VICTORY\|DEFEAT)` → 驱动战斗状态指示 CS-1/CS-3 | ship-combat-system.md |
| **飞船系统** | 强依赖 | 订阅 `OnStateChanged(instanceId, newState)` → 驱动 H-1 HUD 激活/关闭门控；HUD 激活时拉取血量快照（`GetCurrentHull` / `GetMaxHull`） | ship-system.md |

**飞船 HUD 被依赖（下游）：**

| 依赖系统 | 依赖类型 | 数据接口 |
|----------|---------|---------|
| **双视角切换系统** | 软依赖（协调） | 切换系统协调 HUD 的 `CameraMode` 同步，确保视角状态与视角切换按钮图标一致；HUD 不拥有摄像机状态，只读取并显示 | 双视角切换系统 GDD（Not Started）|

> **双向一致性说明**：
> - 飞船生命值系统 GDD 已在 Dependencies 中列出「飞船 HUD：强依赖，OnHullChanged/OnShipDying 事件订阅者」✅
> - 飞船操控系统 GDD 已在 Dependencies 中列出「飞船 HUD：强依赖，订阅 OnLockAcquired/OnLockLost，读取 linearVelocity」✅
> - 飞船战斗系统 GDD 需在完成后补充「飞船 HUD 订阅 BeginCombat/EndCombat 事件」的反向引用
> - 双视角切换系统 GDD（Not Started）完成后需补充对本 HUD 的引用

## Tuning Knobs

| 调节旋钮 | 当前值 | 安全范围 | 过高后果 | 过低后果 |
|----------|--------|----------|----------|----------|
| `HUD_HULL_SAFE_THRESHOLD` | 0.66 | 0.50–0.80 | 安全区过小，玩家血量到 70% 就显示橙色，引发不必要的焦虑 | 安全区过大，橙色来得太晚，玩家来不及警觉 |
| `HUD_HULL_CAUTION_THRESHOLD` | 0.33 | 0.20–0.50 | 警戒区过小，直接从橙色跳红色，丢失警告缓冲层 | 警戒区过大，红色来得太晚，玩家来不及应对危机 |
| `HUD_HULL_CRITICAL_PULSE_THRESHOLD` | 0.20 | 0.10–0.35 | 危急闪烁过早，玩家还有 30% 血就一直闪烁，容易疲劳 | 危急闪烁过晚，接近死亡时玩家才收到最强警告 |
| `SPEED_LERP_COEFF` | 0.15 | 0.05–0.40 | 数字追踪过快，与原始值几乎相同，仍有抖动 | 数字响应过慢，踩满油门后要等 40+ 帧才到最高显示速度 |
| `SPEED_SLOW_THRESHOLD` | 0.30 | 0.15–0.45 | 灰色区间过大，玩家大部分时间看到灰色速度表，缺乏动感 | 灰色区间过小，低速灰色几乎不可见，丧失速度档位感知 |
| `SPEED_CRUISE_THRESHOLD` | 0.70 | 0.50–0.85 | 金黄/橙色区间被压缩，高速驾驶体验的视觉奖励消失 | 巡航白色区间过小，速度稍高就变金黄，缺乏巡航沉稳感 |
| `CAMERA_SWITCH_DURATION`（引用操控系统） | 0.15–0.50s | — | 见飞船操控系统 GDD Tuning Knobs | — |

**⚠️ TODO（MVP 暂缓，Vertical Slice 前解决）：**

> `SPEED_LERP_COEFF = 0.15` 基于 60fps 校准。帧率无关修正公式为 `1 - (1-0.15)^(deltaTime×60)`，在 60fps 以外帧率下可能产生不一致的速度表响应速度。Vertical Slice 阶段如果目标设备帧率不稳定（如低端 Android 设备），应实现帧率无关修正。

**继承自依赖系统的调节旋钮（本 GDD 不重复定义）：**

| 旋钮名 | 所有者 | 影响 HUD 的方式 |
|--------|--------|---------------|
| `SHIP_MAX_SPEED` | ship-control-system.md | 影响 `speed_ratio` 计算，进而影响速度颜色阶梯的绝对速度阈值 |
| `SHIP_MAX_HULL` | ship-health-system.md（待原型验证） | 影响 `health_ratio`，进而影响血条绝对血量值的显示 |

## Visual/Audio Requirements

### 7.1 整体 HUD 视觉语言——「战术玻璃板」

| 属性 | 规格 |
|------|------|
| 背景遮罩材质 | 半透明面板，`#0A1A2E`（深海军蓝），不透明度 **65%** |
| 面板边框 | 1dp 直线，`#4488FF` 20% 透明度，2dp 圆角 |
| 整体发光基调 | 活跃 UI 元素 Bloom **0.4**（不抢视觉焦点） |
| 玻璃质感 | 背景面板 Gaussian 模糊 Blur Radius **8dp** |
| 非活跃元素颜色 | `#AABBCC`（冷灰蓝），亮度降低，不发光 |

**字体规格（全局两种角色，不混用）：**

| 角色 | 特征 | 用途 |
|------|------|------|
| 数字字体（Tabular） | 等宽、无衬线、中等字重、Tabular Figures | 速度数值、血量、一切需要高速读取的数字 |
| 标注字体（Label） | 等宽、无衬线、细字重、字母间距 +0.08em | `M/S`、按钮标签、状态文字 |

禁止：衬线字体、草书、装饰性字体；在数字显示区域禁止使用比例字体。

---

### 7.2 血条视觉细节

| 属性 | 规格 |
|------|------|
| 填充材质 | 纯色 + 水平线性渐变（左端 100%，右端 85% 不透明），无纹理 |
| 填充 Bloom | **0.6** |
| 空槽（已损血量） | `#0A1A2E` 55% 透明度 |
| 边框 | 1dp，`#FFFFFF` 15% 透明度 |
| 高度 | **8dp**（危险闪烁峰值 10dp） |

**HC-5 伤害脉冲（白色叠加 0.2s）视觉方向：** 「电子过载」感——快进慢出（0.05s 进 → 0.05s 峰值 → 0.1s 淡出 Ease-Out），峰值透明度 70%，覆盖整个血条宽度，位于填充色之上、边框之下。

**HC-4 危险闪烁追加效果（≤20% 时）：**
- 血条高度脉冲：8dp ↔ 10dp，与颜色闪烁同周期 0.8s，Ease-In-Out
- 面板边框切换至 `#FF3333` 40%，同步闪烁
- Bloom 在闪烁峰值升至 1.0，谷值维持 0.6

---

### 7.3 速度表视觉细节

| 属性 | 规格 |
|------|------|
| 数字字体 | Tabular（见 7.1），28sp，`#E8F4FF` |
| 数字对齐 | 右对齐，定宽 4 位占位（前置零不显示但占位） |
| 单位标注 `M/S` | Label 字体 11sp，`#4488FF` 80% 透明度，位于数字右下角，基线对齐数字底部，间距 2dp |

**MAX 档位脉冲（达到 SHIP_MAX_SPEED）：**
- 数字颜色：`#E8F4FF` → `#4488FF`（0.2s 渐变）
- 数字 Scale：1.0 → 1.08 → 1.0，周期 0.8s，Ease-In-Out，循环
- `MAX` 标签：数字右侧淡入 0.2s，13sp，`#4488FF`，Bloom 0.8
- 退出 MAX：标签 0.15s 淡出，颜色 0.2s 还原

---

### 7.4 视角切换按钮视觉细节

| 属性 | 规格 |
|------|------|
| 图标类型 | 线框图标（Outlined），笔画宽度 1.5dp，28×28dp |
| 图标颜色（非激活） | `#AABBCC` |
| 图标颜色（激活） | `#4488FF`，Bloom 0.5 |
| 按钮背景 | 圆形，`#0A1A2E` 70% 透明度，直径 44dp |
| 按钮边框 | 1dp，激活 `#4488FF` 60%，非激活 `#AABBCC` 20% |

**图标区分方式（形态差异而非仅颜色区分）：**
- **第三人称**：飞船线框轮廓（俯视 45°，可见机翼）+ 右上角小摄像机符号，语义「从外部看」
- **第一人称**：矩形线框（驾驶舱视窗）+ 中央准星/十字，语义「从内部瞄准」

**切换反馈：** 点击时 Scale 0.9→1.0，0.1s Ease-Out；切换中两按钮均变灰 `#AABBCC`；切换完成后新视角 0.1s 淡入高亮 `#4488FF`。

---

### 7.5 音效事件列表

> **分工说明**：HUD 音效层负责「信息反馈」音效（状态变化、阈值提醒）；飞船/驾驶/战斗系统层负责「物理/环境」音效（引擎声、武器射击、碰撞、爆炸）。两层不重叠。

**血条音效：**

| 事件 ID | 触发条件 | 音效描述 | 时长 |
|---------|---------|---------|------|
| `SFX_HUD_HP_DANGER_ENTER` | 血量首次跌入 CAUTION 区（< 0.66） | 低沉双音警报 tone，第二音比第一音低 3 半音 | 0.4s |
| `SFX_HUD_HP_CRITICAL_ENTER` | 血量首次跌入 CRITICAL 区（≤ 0.20） | 刺耳高频单音警报 tone，带短促尾音 | 0.3s |
| `SFX_HUD_HP_CRITICAL_LOOP` | 血量维持 ≤ 0.20 期间 | 低频心跳式脉冲（90 BPM），音量 30%（背景感） | 循环，血量 > 0.20 或死亡时停止 |
| `SFX_HUD_HP_CRITICAL_EXIT` | 血量从 ≤ 0.20 回升 > 0.20 | LOOP 0.3s 淡出，无附加音效 | 0.3s 淡出 |

**战斗状态音效：**

| 事件 ID | 触发条件 | 音效描述 | 时长 |
|---------|---------|---------|------|
| `SFX_HUD_COMBAT_ENTER` | 进入 IN_COMBAT（战斗条出现）| 低频「锁定」机械音：短促 click + 低鸣 tone | 0.5s |
| `SFX_HUD_COMBAT_EXIT` | 退出 IN_COMBAT（战斗条消失）| 下降 tone，「解除警报」感 | 0.6s |

**视角切换与速度表音效：**

| 事件 ID | 触发条件 | 音效描述 | 时长 |
|---------|---------|---------|------|
| `SFX_HUD_VIEW_SWITCH` | 点击视角切换按钮 | 机械 click，金属质感，干脆无尾音 | 0.08s |
| `SFX_HUD_SPEED_MAX_ENTER` | 首次达到 SHIP_MAX_SPEED | 高频短促 rising tone，「突破」感 | 0.2s |

**胜利/死亡 HUD 专属音效：**

| 事件 ID | 归属 | 描述 |
|---------|------|------|
| `SFX_HUD_PLAYER_DEATH` | HUD 层 | 短暂静音（0.05s）+ 白噪声炸裂（0.1s），对应死亡白色闪光；飞船爆炸音效由飞船系统层处理 |
| `SFX_HUD_VICTORY` | HUD 层 | 上升三音 chime（轻快），在战斗系统胜利 BGM 前 0.1s 触发，作为「HUD 解锁」提示音 |

---

### 7.6 继承规格引用

以下规格不在本 GDD 重复定义，引用原始文档：

| 规格项 | 原始定义来源 |
|--------|-------------|
| 软锁定准星（颜色 `#00FFAA`、四段弧线、Bloom、动画时序） | 飞船操控系统 GDD §Visual/Audio Requirements §2 |
| 弹道 LineRenderer `#FFDD44`、Bloom 0.8 | 飞船操控系统 GDD §Visual/Audio Requirements §3 |
| 推进器尾焰冷蓝系、Bloom 1.2 | 飞船操控系统 GDD §Visual/Audio Requirements §1 |
| 视角切换过渡时长 0.25s/0.2s | 飞船操控系统 GDD §Visual/Audio Requirements §4 |

📌 **Asset Spec** — Visual/Audio 需求已定义。艺术圣经批准后，运行 `/asset-spec system:ship-hud` 生成每个资产的视觉描述、尺寸规格和生成提示词。

## UI Requirements

### 布局规范

**坐标基准：** 所有位置参数以「底部 1/3 分界线」为垂直基准（此线由操控系统 GDD 中的摇杆区域决定），不使用固定像素值，确保自适应。

**参考分辨率：** 1080×1920（竖屏）。同时验证 4:3 平板布局（见下表）。

### 元素布局表

| 元素 | 锚点 | 手机位置 | 平板位置 | 手机尺寸 | 平板尺寸 |
|------|------|---------|---------|---------|---------|
| 战斗状态条 | Top-Center | 距顶边 24dp | 同左 | w: 60% 屏宽，h: 4dp | w: 50% 屏宽，h: 4dp |
| 血条 | 左侧中部 | 左边缘 24dp，底部 1/3 分界线上方 32dp | 同左 | 180×8dp | 240×10dp |
| 速度表 | 底部中央 | 底部 1/3 区域中央死区，距底边 48dp | 距底边 64dp | 60×32dp | 80×40dp |
| 视角切换按钮 | 右侧中部 | 右边缘 24dp，与血条同高 | 同左 | 触控热区 56×56dp | 触控热区 68×68dp |
| 软锁定准星 | World Space | 跟随目标（Canvas 坐标系） | 同左 | 目标包围盒 1.3 倍 | 同左 |

**焦点保护区：** 屏幕中央 30% 区域（准星活动范围）不放置任何 Screen Space HUD 元素，准星是唯一允许出现于此区域的 UI 内容。

**安全区：** 距屏幕边缘 ≥ 24dp（继承操控系统 GDD 约束）。

**所有触控热区 ≥ 48dp**（Android 最小触控目标要求）。

### 视觉权重优先级

```
优先级 1（余光必读）：血条
优先级 2（视线焦点跟随）：软锁定准星（World Space，天然跟随视线）
优先级 3（状态变化触发感知）：战斗状态条
优先级 4（偶尔检查）：速度表
优先级 5（主动操作，非常态读取）：视角切换按钮
```

### 动态透明度规则

| HUD 状态 | 血条 | 速度表 | 视角切换 |
|---------|------|--------|---------|
| IN_COCKPIT（非战斗） | 60% | 50% | 70% |
| IN_COMBAT | 90% | 70% | 70% |

进入 IN_COMBAT 后 200ms 线性过渡。非战斗时降低透明度减少信息噪声，不增加视觉负担。

### 飞船操控系统虚拟摇杆（继承，不重复定义）

虚拟摇杆布局（左半屏底部 1/3 + 右半屏底部 1/3）由飞船操控系统 GDD §UI Requirements 定义，本 GDD 不重复。HUD 元素布局已据此避开该区域。

📌 **UX Flag — 飞船 HUD**：本系统有 UI 需求（HUD 元素布局、战斗状态反馈、视角切换按钮）。在 Pre-Production 阶段，运行 `/ux-design` 为驾驶舱 HUD 创建完整 UX 规格（`design/ux/cockpit-hud.md`），在写 Epic 之前完成。参考飞船操控系统 GDD 中的 UX Flag 说明。

## Acceptance Criteria

### 血条系统（Hull Bar）

**AC-HUD-01**（HC-2 公式）
**GIVEN** 飞船 MaxHull = 200、CurrentHull = 130，**WHEN** HUD 血条渲染更新，**THEN** 血条填充宽度占总宽度的 65%（±1px 误差内）。

**AC-HUD-02**（HC-3 安全色）
**GIVEN** CurrentHull = 140、MaxHull = 200（health_ratio = 0.70），**WHEN** HUD 渲染血条，**THEN** 血条颜色为 `#00DD88`（绿），无闪烁效果。

**AC-HUD-03**（HC-3 警戒色）
**GIVEN** CurrentHull = 80、MaxHull = 200（health_ratio = 0.40），**WHEN** HUD 渲染血条，**THEN** 血条颜色为 `#FFAA00`（橙），无闪烁效果。

**AC-HUD-04**（HC-3/HC-4 危急色+闪烁）
**GIVEN** CurrentHull = 30、MaxHull = 200（health_ratio = 0.15），**WHEN** HUD 渲染血条，**THEN** 血条颜色为 `#FF3333`（红），以 0.8s 为周期交替显示/暗淡；在 2.4 秒内可观测到至少 3 次完整闪烁周期。

**AC-HUD-05**（HC-4 闪烁阈值边界）
**GIVEN** CurrentHull = 40、MaxHull = 200（health_ratio = 0.20，恰好等于阈值），**WHEN** HUD 渲染血条，**THEN** 血条为红色且显示闪烁效果（0.20 命中 CRITICAL 分支）。

**AC-HUD-06**（HC-1 事件驱动无轮询）
**GIVEN** 飞船处于稳定状态（无伤害、无回血），**WHEN** 经过 60 帧，**THEN** HullBar 组件未调用任何读取 CurrentHull 的代码路径（通过 Unity Profiler 或 Frame Debugger 验证，更新帧数为 0）。

**AC-HUD-07**（HC-5 受伤脉冲）
**GIVEN** 飞船 HUD 正常显示，**WHEN** 飞船受到任意伤害触发 OnHullChanged，**THEN** 血条叠加白色高亮，叠加层在 0.2s（±0.05s）后完全消失；叠加期间底层血条颜色仍可辨别。

**AC-HUD-08**（E-5 脉冲连续触发计时器重置）
**GIVEN** 飞船在第 0s 受伤（脉冲已触发，尚未结束），**WHEN** 在第 0.1s 再次受伤，**THEN** 白色叠加计时器从 0 重置，将在第二次触发后再持续完整 0.2s，不在第一次触发后 0.2s 时提前结束。

---

### 速度表（Speed Gauge）

**AC-HUD-09**（SP-1 阻尼平滑）
**GIVEN** 飞船当前显示速度为 50、实际速度骤变为 100，**WHEN** HUD 在后续帧更新速度表，**THEN** 显示值不立即跳变为 100，而是逐帧平滑过渡（Lerp factor=0.15）；显示值为整数；最终收敛至 100。

**AC-HUD-10**（SP-1 变化阈值抑制抖动）
**GIVEN** 实际速度在 50.3 附近轻微波动（±0.3），**WHEN** HUD 更新速度显示值，**THEN** 屏幕数字保持不变（变化量 < 0.5，不触发 UI 更新）。

**AC-HUD-11**（SP-2 速度颜色三档）
**GIVEN** SHIP_MAX_SPEED = 100，**WHEN** 显示速度分别为 25、55、85 时，**THEN** 三档颜色各不相同：分别对应 `#AAAAAA`（灰）/ `#FFFFFF`（白）/ `#FFDD44`（金黄）。

---

### 软锁定准星（Soft Lock Reticle）

**AC-HUD-12**（SL-1/2/3 坐标对齐）
**GIVEN** 场景中有合法目标且飞船已软锁定，**WHEN** 目标在 3D 空间移动，HUD 在 LateUpdate 更新准星，**THEN** 准星屏幕坐标与 `Camera.WorldToScreenPoint(目标位置)` 的计算结果偏差不超过 2 像素。

**AC-HUD-13**（SL-3 目标在摄像机背后时隐藏）
**GIVEN** 软锁定目标存在，**WHEN** 目标移动到摄像机背后（WorldToScreenPoint.z < 0），**THEN** 准星 UI 立即隐藏，不显示在屏幕任何位置。

**AC-HUD-14**（E-7 准星目标为 null 无崩溃）
**GIVEN** 软锁定目标在锁定期间被摧毁（对象变为 null），**WHEN** HUD 执行下一次 LateUpdate，**THEN** 不抛出 NullReferenceException；准星隐藏；Console 无错误日志；游戏帧率正常。

---

### 战斗状态指示（Combat State Indicator）

**AC-HUD-15**（CS-1 进入战斗）
**GIVEN** 飞船处于 IN_COCKPIT，**WHEN** 状态切换至 IN_COMBAT，**THEN** 战斗状态条在 1 帧内出现（或按设计淡入），屏幕边缘脉冲效果开始播放；两者均在状态切换后第一帧可观测。

**AC-HUD-16**（CS-3 胜利退出）
**GIVEN** 飞船处于 IN_COMBAT，**WHEN** 以胜利条件退出战斗，**THEN** 战斗状态指示器颜色执行渐变过渡（不是瞬间切换），屏幕出现「战斗结束」文字并持续至少 1 帧可读。

**AC-HUD-17**（CS-4 DESTROYED 淡出 ≥ 0.3s）
**GIVEN** 飞船处于 IN_COMBAT，**WHEN** 状态切换至 DESTROYED，**THEN** 触发白色全屏闪光；HUD 整体 alpha 在 ≥ 0.3s 内渐变至 0（不立即消失）；0.3s 计时结束后 HUD 不可见。

**AC-HUD-18**（E-6 CS-3 vs CS-4 优先级）
**GIVEN** 飞船处于 IN_COMBAT，**WHEN** 同一帧内同时触发胜利退出事件和 DESTROYED 事件，**THEN** 仅执行 CS-4（白色闪光 + HUD 渐出）；不显示「战斗结束」文字；CS-3 渐变效果未启动。

---

### 状态门控（State Gate）

**AC-HUD-19**（H-1 非驾驶舱状态隐藏）
**GIVEN** 飞船处于星图状态（非 IN_COCKPIT / IN_COMBAT），**WHEN** 查看游戏画面，**THEN** 血条、速度表、准星、战斗状态条均不可见（alpha = 0 或未激活）。

**AC-HUD-20**（E-8 DESTROYED 状态吞掉视角切换输入）
**GIVEN** 飞船处于 DESTROYED 状态（HUD 渐出进行中），**WHEN** 玩家点击视角切换按钮，**THEN** 视角不切换；输入被忽略；无错误日志；HUD 渐出动画不中断。

---

### 跨系统接口验证

**AC-HUD-21**（跨系统：生命值系统 → HUD 血条）
**GIVEN** ShipHealthSystem 与 HUD 已连接，飞船 MaxHull = 200、CurrentHull = 200，**WHEN** 外部调用 `TakeDamage(50)`，**THEN** 血条在同一帧或下一帧更新为 75% 宽度；OnHullChanged 触发恰好 1 次；血条颜色为绿色（health_ratio = 0.75 > 0.66）。

**AC-HUD-22**（跨系统：操控系统速度 → HUD 速度表）
**GIVEN** ShipControlSystem 已连接 HUD，当前实际速度 = 0，**WHEN** 速度推进至 SHIP_MAX_SPEED × 0.8，**THEN** 速度表在 Lerp 平滑后收敛至对应整数值（±1）；速度颜色切换为金黄色（FAST 档）。

**AC-HUD-23**（E-3 MaxHull ≤ 0 防御）
**GIVEN** 因异常数据导致飞船 MaxHull = 0，**WHEN** HUD 尝试渲染血条，**THEN** 不抛出除零异常；血条显示为安全默认值（满血或空血）；Console 无崩溃日志。

---

### 星图事件通知徽章（Starmap Notification Badge）

**AC-HUD-24**（NB-1 显示条件 — 驾驶舱 + 有事件）
**GIVEN** `ViewLayer == COCKPIT` 且存在 1 个未处理星图事件，**WHEN** HUD 渲染，**THEN** 通知徽章可见，显示计数 `⚔️ 1`。

**AC-HUD-25**（NB-1 显示条件 — 无事件时隐藏）
**GIVEN** `ViewLayer == COCKPIT` 且未处理事件数为 0，**WHEN** HUD 渲染，**THEN** 通知徽章不可见（alpha = 0 或未激活）。

**AC-HUD-26**（NB-1 显示条件 — 星图视角时隐藏）
**GIVEN** 存在 2 个未处理星图事件，**WHEN** `ViewLayer` 切换为 `STARMAP`，**THEN** 通知徽章立即隐藏，不显示任何时长的过渡。

**AC-HUD-27**（NB-3 非阻断交互）
**GIVEN** 通知徽章已展开浮层，飞船处于 `IN_COCKPIT`，**WHEN** 玩家在浮层展开期间操作虚拟摇杆，**THEN** 飞船正常响应移动输入；浮层不关闭；无输入冲突日志。

**AC-HUD-28**（NB-4 返回星图自动已读）
**GIVEN** 存在 3 个未读事件且徽章显示 `⚔️ 3`，**WHEN** `ViewLayer` 切换为 `STARMAP` 再切换回 `COCKPIT`，**THEN** 徽章计数归零并隐藏（事件已全部标为已读）。

---

> **QA 执行注意事项：**
> - AC-HUD-04 的闪烁验证建议录制 3 秒视频作为测试证据
> - AC-HUD-06 需要 Unity Profiler 工具支持，属于工具级验证
> - H-2 各状态透明度值的专项验证可在 Visual QA Pass 阶段补充

## Open Questions

| # | 问题 | 影响范围 | 负责人 | 目标解决时间 |
|---|------|---------|--------|------------|
| Q-1 | `SHIP_MAX_SPEED` 的具体值（影响速度表颜色阶梯的绝对阈值） | D-HUD-4、SP-2、Tuning Knobs | game-designer + 原型测试 | `/prototype 飞船驾驶舱操控` 完成后 |
| Q-2 | `SHIP_MAX_HULL` 的具体值（影响血条绝对数值显示） | D-HUD-1、HC-6 平板数字显示 | game-designer + 原型测试 | `/prototype 飞船驾驶舱操控` 完成后 |
| Q-3 | `SFX_HUD_HP_CRITICAL_LOOP` 的 90 BPM 是否需要与战斗 BGM 节拍同步？还是刻意错开制造紧张感？ | 7.5 血条音效 | audio-director | 音频系统 GDD 设计时 |
| Q-4 | 视角切换按钮图标的最终形态需要美术确认（线框飞船轮廓 vs 其他符号） | 7.4 视角切换视觉 | art-director | 艺术圣经批准后 |
| Q-5 | `SFX_HUD_PLAYER_DEATH` 的「短暂静音」时长（建议 0.05s）需要与音频设计师确认，避免与飞船爆炸音效产生时序冲突 | 7.5 胜利/死亡音效 | audio-director + sound-designer | 音频系统 GDD 设计时 |
| Q-6 | `SPEED_LERP_COEFF = 0.15` 基于 60fps 校准，低端 Android 设备帧率不稳定时是否需要帧率无关修正（见 Tuning Knobs TODO）？ | D-HUD-3、SP-1 | gameplay-programmer | Vertical Slice 阶段前 |
| Q-7 | ✅ **已解答**（见 design/gdd/dual-perspective-switching.md）：HudVisible = (ViewLayer == COCKPIT) AND (ShipState ∈ {IN_COCKPIT, IN_COMBAT})。H-1 门控由 ShipState 事件驱动；ViewLayer 广播事件 `OnViewLayerChanged` 控制 HUD Canvas 整体显隐。ViewLayerManager 持有 CurrentViewLayer 属性（enum {STARMAP, COCKPIT}）供 HUD 订阅。 | H-1 状态门控、Interactions with Other Systems | game-designer | ✅ 已解答 2026-04-13 |
