# Cross-GDD Review Report — 星链霸权 (Starchain Hegemony)

**Date**: 2026-04-13
**GDDs Reviewed**: 13
**Systems Covered**: resource-system, star-map-system, ship-system, building-system, ship-health-system, colony-system, ship-control-system, ship-combat-system, enemy-system, fleet-dispatch-system, star-map-ui, ship-hud, dual-perspective-switching
**Pillars**: 经济即军事 · 一目了然的指挥 · 我的星际帝国 · 从星图到驾驶舱
**Anti-Pillars**: 不做 4X 深度外交/文化；不做放置/挂机
**Entity Registry**: entities.yaml — 1,049 lines, 18 formulas, 40+ constants

---

## Consistency Issues

### Blocking (must resolve before architecture begins)

🔴 **IC-01 — star-map-system.md 下游依赖表缺失建筑系统**

- `building-system.md` 声明上游依赖 star-map-system（消费 `ownershipState`、`GetOreMultiplier`、`OnNodeOwnershipChanged`、`StarNode.Buildings`、`HasShipyard`）
- `star-map-system.md` 的下游依赖列表中**没有**列出 building-system
- **修复**: star-map-system.md 下游依赖表新增建筑系统一行

🔴 **IC-02 — ship-system.md 下游依赖表严重不完整**

- `star-map-ui.md` 声明消费 `ShipData.GetState(shipId)` 和 `OnShipStateChanged`，但 ship-system.md 下游表未列出星图 UI
- `ship-hud.md` 声明强依赖 ship-system（`OnStateChanged` 事件），但 ship-system.md 下游表未列出飞船 HUD
- `colony-system.md` 调用 `BuildShip(nodeId)`、`RefundResources`，但 ship-system.md 下游表未列出殖民地系统
- **修复**: ship-system.md 下游依赖表需新增：星图 UI、飞船 HUD、殖民地系统

🔴 **IC-03 — 退出驾驶舱后 ShipState 值冲突**

- `ship-system.md` 第 103 行：`IN_COCKPIT → DOCKED`（玩家主动退出驾驶舱）
- `dual-perspective-switching.md` 第 91 行：`ShipState → IN_TRANSIT`（出场序列 Step 4）
- `dual-perspective-switching.md` 第 330 行 AC-DVS-02：`ShipState 变为 IN_TRANSIT`
- 两份 GDD 对同一操作写入不同状态值，且验收标准也指向不同答案
- **建议**: 明确规则——退出驾驶舱恢复"进入前的状态"（DOCKED 进 → DOCKED 出；IN_TRANSIT 进 → IN_TRANSIT 出），然后更新两份 GDD 保持一致

🔴 **IC-04 — systems-index.md DAG 与各 GDD 实际依赖不匹配**

systems-index.md 的依赖 DAG 与各 GDD 内部 Dependencies 章节存在以下差异：

| 系统 | index 缺失的依赖项 |
|------|-------------------|
| 建筑系统 | 缺星图系统 |
| 飞船战斗系统 | 缺飞船操控系统、缺星图系统 |
| 星图 UI | 缺飞船系统 |
| 飞船 HUD | 缺飞船系统 |

- **修复**: 同步更新 systems-index.md 的 DAG 表

### Warnings (should resolve, but won't block)

⚠️ **W-01 — 血量阈值双重定义（设计意图已声明，但存在越权描述）**

- `ship-health-system.md` 定义 3 状态：HEALTHY(>0.50) / DAMAGED(0.25–0.50) / CRITICAL(≤0.25)
- `ship-hud.md` 定义 4 状态：SAFE(≥0.66) / CAUTION(0.33–0.65) / DANGER(0.21–0.32) / CRITICAL(≤0.20)
- `ship-hud.md` 第 247 行已声明"两套阈值独立调节，互不绑定"——这是有意设计
- `entities.yaml` notes 字段也已记录此独立关系
- **问题**: `ship-health-system.md` 第 108–110 行的表格中写了"血条绿色/黄色/红色"，这是**视觉描述越权**——实际血条颜色由 HUD 的 D-HUD-2 公式控制，数值/颜色与 health-system 声明的不一致
- **修复**: ship-health-system.md 第 108–110 行应移除具体颜色描述，改为"视觉表现见 ship-hud.md D-HUD-2"，避免实现时混淆

⚠️ **W-02 — ship-combat-system.md 飞船 HUD 依赖标注为"待设计"**

- ship-combat-system.md 下游表中飞船 HUD 一行备注"待设计……GDD 完成后需补充反向引用"
- 两个 GDD 都已为 Designed 状态——此注记应更新为已确认
- **修复**: 30 秒编辑，更新 ship-combat-system.md 注记

⚠️ **W-03 — ship-combat-system.md 下游缺双视角切换系统**

- dual-perspective-switching.md 的下游依赖中列出 ship-combat-system（E-3 战斗保护逻辑）
- ship-combat-system.md 上下游表中均未提及双视角切换系统
- **修复**: ship-combat-system.md 需新增一行下游依赖

⚠️ **W-04 — ship-system.md Foundation 层"无上游依赖"声明可能不准确**

- ship-system.md 声明"无上游依赖（Foundation 层）"
- 但其 `BuildShip` 逻辑需读取 `StarNode.HasShipyard`（由 building-system 维护）
- 这是软依赖（读取另一系统维护的字段），是否打破 Foundation 层定义需架构决策
- **建议**: 记录为 ADR 或在 ship-system.md 中标注为"运行时软依赖，不影响初始化顺序"

⚠️ **W-05 — `can_afford` 公式分散在两个 GDD 中**

- `resource-system.md`: `can_afford = (ore_current >= ore_cost) AND (energy_current >= energy_cost)`（2 条件）
- `colony-system.md`: BuildShip 前提 B-1/B-2/B-3 添加了 ownership + HasShipyard 检查
- 不是真正冲突——但文档未明确说明分层关系，可能引起实现混淆
- **修复**: resource-system.md 添加注释说明 `can_afford` 是纯资源检查，业务前置条件由各功能系统独立检查

⚠️ **W-06 — 悬挂接口：`OnShipStateChanged` 事件未在 ship-system.md 中定义**

- star-map-ui.md 声明消费 `OnShipStateChanged` 事件（标注"需确认"）
- ship-system.md 中没有定义此事件
- **修复**: 在 ship-system.md 接口表或事件列表中新增 `OnShipStateChanged` 定义

---

## Game Design Issues

### Blocking

🔴 **DES-01 — 驾驶舱内对星图事件完全无感知（fleet-dispatch Q-2 未解决）**

- 玩家在驾驶舱内时，星图逻辑继续运行（C-3 规则）
- 舰队可到达 ENEMY 节点并触发无人值守战斗
- 无人值守战斗可判负（P=E → DEFEAT），飞船被销毁
- **玩家在驾驶舱内无任何 UI 通知**——退出后发现飞船消失
- fleet-dispatch-system.md Q-2 已标记为 "critical design decision" 但仍未解决
- **建议**: 必须在架构前决策——推荐驾驶舱内显示小型星图通知 badge（不打断驾驶舱体验），然后更新 ship-hud.md 和 fleet-dispatch-system.md

### Warnings

⚠️ **DES-02 — 驾驶舱内无空间导航辅助**

- 玩家进入驾驶舱后在 3D 空间飞行，但 GDD 未定义如何知道目标节点方向
- ship-hud.md 未包含方向指示器或节点空间标记
- **建议**: 在 ship-hud.md 中添加"目标方向指示器"（MVP 可以是简单的屏幕边缘箭头）

⚠️ **DES-03 — 能源赤字在 MVP 中无惩罚，削弱"经济即军事"支柱**

- resource-system.md 明确规定：能源赤字仅触发 HUD 警告，不惩罚
- 这意味着玩家可无限建矿场造成严重能源赤字而无代价
- MVP 简化是有意为之——但应在文档中明确标注此设计意图
- **建议**: resource-system.md Open Questions 中新增"Vertical Slice 是否引入能源惩罚机制"

⚠️ **DES-04 — MVP 5 节点星图 + 1 种飞船 + 2 种建筑，重玩性极低**

- 符合 MVP "验证核心假说"的定位，不阻塞
- 但需确认：MVP 验证成功后，Vertical Slice 的内容扩展规划是否已考虑
- game-concept.md 已列出 Vertical Slice 范围——此项确认通过

⚠️ **DES-05 — 所有飞控数值均为 TBD，飞行手感完全悬空**

以下常量必须在原型后回填：

| 常量 | 当前参考值 | 影响范围 |
|------|-----------|---------|
| `SHIP_THRUST_POWER` | 15 m/s² | 飞行手感 |
| `SHIP_TURN_SPEED` | 120 deg/s | 飞行手感 |
| `SHIP_MAX_SPEED` | 20 m/s | 战斗节奏 |
| `LOCK_RANGE` | 50 m | 战斗启动距离 |
| `FIRE_ANGLE_THRESHOLD` | 15° | 自动开火难度 |
| `ORE_CAP` | TBD | 经济节奏 |

- 不阻塞架构，但必须在 Vertical Slice 前通过原型验证确定

---

## Cross-System Scenario Issues

Scenarios walked: 3

1. **场景 A**: 玩家首次进入驾驶舱打一场战斗
2. **场景 B**: 经济决策 → 建矿场 → 建船厂 → 造船
3. **场景 C**: 无人值守舰队战斗（玩家在驾驶舱内）

### Blockers

🔴 **场景 A — ship-system × dual-perspective-switching**

- 退出驾驶舱时 ShipState 写入值冲突（同 IC-03）
- ship-system.md 说写 DOCKED，dual-perspective-switching.md 说写 IN_TRANSIT
- 如果飞船进入驾驶舱前是 DOCKED 状态，退出后设为 IN_TRANSIT 在语义上是错误的（飞船并没有在飞行）
- **必须在实现前解决**

🔴 **场景 C — fleet-dispatch × ship-hud × dual-perspective-switching**

- 玩家在驾驶舱内，另一艘舰队到达 ENEMY 节点
- 无人值守战斗 P=E → DEFEAT → 飞船销毁
- 玩家无感知，退出驾驶舱后发现飞船消失
- **同 DES-01，必须在架构前决策通知机制**

### Warnings

⚠️ **场景 A — ship-hud 缺空间导航**

- 玩家进入驾驶舱后在 3D 空间飞行，不知道目标在哪个方向
- 同 DES-02

### Info

ℹ️ **场景 B — 经济链完整**

- 建矿场 → 积累矿石 → 建船厂 → 造船，完整跑通无阻断
- `can_afford` 分层不影响功能，仅需文档注释（W-05）

ℹ️ **场景 A — 战斗结束后状态回落**

- 战斗胜利 → ship.State IN_COMBAT → IN_COCKPIT：ship-combat-system.md V-1/L-2 与 ship-system.md 一致
- 无冲突

---

## Pillar Alignment Check

| 支柱 | 评级 | 说明 |
|------|------|------|
| 经济即军事 | ✅ PASS | 矿石/能源 → 建筑 → 飞船 → 战斗 → 占领 → 更多资源，闭环完整。飞船损失 = 30 矿 + 15 能源永久消失。⚠️ 能源赤字无惩罚（DES-03） |
| 一目了然的指挥 | ✅ PASS | 颜色编码、自动开火、资源徽章实时更新。⚠️ time_to_afford 倒计时为 Vertical Slice 阶段 |
| 我的星际帝国 | ✅ PASS | 单存档、飞船持久血量、节点历史。⚠️ MVP 无存档（有意排除） |
| 从星图到驾驶舱 | ✅ PASS | Additive 场景、切换 ≤ 1.0s、星图逻辑持续运行。🔴 驾驶舱内星图通知缺失（DES-01） |

无 Anti-Pillar 违规。

---

## Player Fantasy Coherence

所有 13 个系统的 Player Fantasy 指向兼容身份：

- **策略层**: "帝国统帅在星图上运筹帷幄"
- **战斗层**: "将军亲征，亲自驾驶旗舰冲入战场"
- **经济层**: "每一吨矿石都是军事力量的种子"
- **切换层**: "从上帝视角到驾驶舱视角的身份无缝切换"

所有 Fantasy 服务同一核心身份："**拥有自己星际帝国的太空统帅**"。无身份冲突。✅

---

## Economic Loop Analysis

### 资源: 矿石 (Ore)

| 来源 (Source) | 速率 |
|--------------|------|
| 基础矿场 | +10/s per mine |
| RICH 节点倍率 | ×2.0 |

| 消耗 (Sink) | 数量 |
|-------------|------|
| 矿场建造 | 50 |
| 船厂建造 | 80 |
| 飞船建造 | 30 |
| 飞船损失 | 30（永久） |

- **评估**: 有上限 ORE_CAP（待定），矿场数量有限（受节点数量和建筑槽位约束），飞船损失是永久消耗
- ⚠️ 如果 ORE_CAP 设置过高或无限，后期矿石会溢出——需要确定 ORE_CAP 值

### 资源: 能源 (Energy)

| 来源 | 速率 |
|------|------|
| 殖民地基础 | +5/s per colony |

| 消耗 | 速率 |
|------|------|
| 矿场运营 | -2/s per mine |
| 船厂待机 | -3/s per shipyard |
| 建造消耗 | 一次性 |

- **评估**: 流量型无上限，但有持续消耗。⚠️ 赤字无惩罚（DES-03）

### 正向反馈环检测

```
占领节点 → 建矿场 → 更多矿石 → 更多飞船 → 占领更多节点
```
- 经典雪球效应——MVP 5 节点有限，不会失控
- Vertical Slice 扩大后需引入制衡机制（敌人进攻、维护费等）

---

## Difficulty Curve Consistency

| 系统 | 缩放变量 | 缩放方式 | 缩放触发 |
|------|---------|---------|---------|
| enemy-system | 敌人数量 | 按节点固定（1–2 per node） | 空间推进 |
| resource-system | 产出速率 | 线性（矿场数量） | 玩家建造 |
| ship-combat-system | 战斗难度 | 固定（1v1 或 1v2） | 无缩放 |

- **评估**: MVP 难度基本平坦（固定敌人数量 + 固定武器伤害），符合 MVP 验证目的
- Vertical Slice 引入多敌类型后需关注曲线设计

---

## Cognitive Load Assessment

**星图阶段同时活跃系统**: 3 个（资源管理、舰队调度、建筑决策）✅ 在安全范围
**驾驶舱阶段同时活跃系统**: 2 个（飞行操控、战斗瞄准——但自动开火大幅降低瞄准负担）✅ 在安全范围
**切换本身**: 1 次认知模式转换（策略 ↔ 动作）——这是核心假说的验证目标

无认知过载风险。✅

---

## GDDs Flagged for Revision

| GDD | Reason | Type | Priority |
|-----|--------|------|----------|
| `ship-system.md` | 下游依赖表缺 3 个系统（IC-02）+ Foundation 层软依赖（W-04） | Consistency | Blocking |
| `dual-perspective-switching.md` | 退出驾驶舱 ShipState 值与 ship-system.md 冲突（IC-03） | Consistency | Blocking |
| `star-map-system.md` | 下游依赖表缺建筑系统（IC-01） | Consistency | Blocking |
| `systems-index.md` | DAG 缺 4 条依赖边（IC-04） | Consistency | Blocking |
| `fleet-dispatch-system.md` | Q-2 "驾驶舱内战斗通知" 未决策（DES-01） | Design | Blocking |
| `ship-health-system.md` | 第 108–110 行颜色描述越权（W-01） | Consistency | Warning |
| `ship-combat-system.md` | 飞船 HUD 注记待更新 + 缺双视角切换系统依赖（W-02, W-03） | Consistency | Warning |
| `ship-hud.md` | 缺空间导航辅助设计（DES-02） | Design | Warning |
| `resource-system.md` | can_afford 分层说明 + 能源赤字惩罚声明（W-05, DES-03） | Design | Warning |

---

## Open Design Decisions (require user input)

| 决策 | 优先级 | 背景 |
|------|--------|------|
| **fleet-dispatch Q-2**: 驾驶舱内收到星图战斗事件时如何通知玩家 | CRITICAL | 影响 ship-hud.md、fleet-dispatch-system.md |
| **IC-03**: 退出驾驶舱的 ShipState → 恢复进入前状态 vs 固定 DOCKED | HIGH | 影响 ship-system.md、dual-perspective-switching.md |
| **W-04**: ship-system.md Foundation 层是否允许软依赖 building-system | MEDIUM | 影响层级定义和初始化顺序 |
| **DES-02**: 驾驶舱内是否需要导航辅助 HUD 元素 | MEDIUM | 影响 ship-hud.md |
| **DES-03**: 确认能源赤字无惩罚为 MVP 有意妥协 | LOW | 文档注释即可 |

---

## Verdict: CONCERNS

**总结**: 13 个 MVP 系统 GDD 质量良好——每份文档均完整包含 8 个必需章节，验收标准可测试，公式有变量定义和示例。核心游戏循环逻辑闭环，四大支柱对齐，无 Anti-Pillar 违规，无认知过载，无经济崩溃。Player Fantasy 指向统一身份。

**存在 5 个 Blocking 问题需解决**:
- IC-01~IC-04 是依赖表不完整/不同步——纯文档编辑，约 30 分钟可全部修复
- IC-03 + DES-01 涉及设计决策——需用户输入后才能锁定

**不存在核心循环断裂、支柱冲突、或不可修复的架构缺陷**。所有 Blocking 问题均可通过文档修订解决，不需要重新设计任何系统。

### Recommended next steps:
1. 用户决策 IC-03（退出驾驶舱状态）和 DES-01（驾驶舱内星图通知）
2. 批量修复 IC-01、IC-02、IC-04 的依赖表（纯文档编辑）
3. 处理 W-01~W-06 的 Warning 级修复
4. 重新运行 `/review-all-gdds` 验证修复
5. 运行 `/gate-check` 通过 Systems Design 阶段门
