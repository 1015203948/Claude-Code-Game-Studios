# Systems Index: 星链霸权 (Starchain Hegemony)

> **Status**: Draft
> **Created**: 2026-04-12
> **Last Updated**: 2026-04-12
> **Source Concept**: design/gdd/game-concept.md

---

## Overview

星链霸权是一款双层玩法太空策略游戏：宏观星图经营层（殖民地建设、资源网络、舰队调度）与微观飞船驾驶层（触屏操控、实时战斗）通过无缝视角切换系统整合为同一个体验。游戏需要三组核心系统：经济-战略系统（资源、建筑、殖民地、舰队、星图）定义了帝国建设的宏观决策；飞船-战斗系统（飞船数据模型、操控、战斗、生命值、敌人）实现了驾驶舱的沉浸体验；双视角切换架构是连接两层的核心技术，也是 MVP 核心假说的验证点。支柱1（经济即军事）要求所有经济决策必须对军事循环有意义，支柱4（从星图到驾驶舱）要求这两层体验在同一存档中无缝共存。

---

## Systems Enumeration

| # | 系统名 | 分类 | 优先级 | 状态 | 设计文档 | 依赖项 |
|---|--------|------|--------|------|----------|--------|
| 1 | 资源系统 | 经济 | MVP | Designed | design/gdd/resource-system.md | — |
| 2 | 星图系统 | 战略 | MVP | Designed | design/gdd/star-map-system.md | — |
| 3 | 飞船系统 | 飞行战斗 | MVP | Designed | design/gdd/ship-system.md | — |
| 4 | 建筑系统 | 经济 | MVP | Designed | design/gdd/building-system.md | 资源系统, 星图系统 |
| 5 | 飞船生命值系统 | 飞行战斗 | MVP | Designed | design/gdd/ship-health-system.md | 飞船系统 |
| 6 | 殖民地系统 | 经济 | MVP | Designed | design/gdd/colony-system.md | 建筑系统, 资源系统, 星图系统 |
| 7 | 飞船操控系统 | 飞行战斗 | MVP | Designed | design/gdd/ship-control-system.md | 飞船系统 |
| 8 | 飞船战斗系统 | 飞行战斗 | MVP | Designed | design/gdd/ship-combat-system.md | 飞船系统, 飞船生命值系统, 飞船操控系统, 星图系统 |
| 9 | 敌人系统 (inferred) | 飞行战斗 | MVP | Designed | design/gdd/enemy-system.md | 飞船系统, 飞船生命值系统, 飞船战斗系统 |
| 10 | 舰队调度系统 | 战略 | MVP | Designed | design/gdd/fleet-dispatch-system.md | 星图系统, 飞船系统 |
| 11 | 星图 UI (inferred) | UI | MVP | Designed | design/gdd/star-map-ui.md | 星图系统, 殖民地系统, 舰队调度系统, 飞船系统 |
| 12 | 飞船 HUD (inferred) | UI | MVP | Designed | design/gdd/ship-hud.md | 飞船生命值系统, 飞船战斗系统, 飞船操控系统, 飞船系统 |
| 13 | 双视角切换系统 | 架构 | MVP | Designed | design/gdd/dual-perspective-switching.md | 星图 UI, 飞船 HUD |
| 14 | 程序星图生成系统 | 世界生成 | Vertical Slice | Not Started | — | 星图系统 |
| 15 | 经济 UI (inferred) | UI | Vertical Slice | Not Started | — | 资源系统, 建筑系统, 殖民地系统 |
| 16 | 舰队 UI (inferred) | UI | Vertical Slice | Not Started | — | 舰队调度系统 |
| 17 | 存档/读档系统 (inferred) | 架构 | Vertical Slice | Not Started | — | 星图系统, 殖民地系统, 舰队调度系统 |
| 18 | 主菜单 UI (inferred) | UI | Vertical Slice | Not Started | — | 存档/读档系统 |

---

## Categories

| 分类 | 描述 | 系统 |
|------|------|------|
| **经济** | 资源产出与消耗的规则层 | 资源系统, 建筑系统, 殖民地系统 |
| **战略** | 星图层的数据模型和调度逻辑 | 星图系统, 舰队调度系统 |
| **飞行战斗** | 飞船驾驶舱层的核心游戏性系统 | 飞船系统, 飞船操控系统, 飞船战斗系统, 飞船生命值系统, 敌人系统 |
| **世界生成** | 程序化内容生成 | 程序星图生成系统 |
| **架构** | 跨层技术系统，不属于单一玩法层 | 双视角切换系统, 存档/读档系统 |
| **UI** | 玩家可见的信息显示和交互界面 | 星图 UI, 经济 UI, 舰队 UI, 飞船 HUD, 主菜单 UI |

---

## Priority Tiers

| 里程碑 | 定义 | 目标时间 | 设计优先级 |
|--------|------|----------|-----------|
| **MVP** | 核心循环可运行——经济→舰队→切换→驾驶→战斗→回到星图的完整闭环，验证"无缝切换"核心假说 | 4-6 周 | 优先设计 |
| **Vertical Slice** | 完整的一次扩张体验，含持久化存档、程序生成星图和完整 UI | +2-3 个月 | 次优先 |
| **Alpha** | 所有功能系统到位（当前 18 个系统全覆盖），内容扩展开始 | +4-6 个月 | 按序 |
| **Full Vision** | 内容完整（8+种飞船、10+种建筑）、打磨、成就系统 | +8-12 个月 | 按需 |

---

## Dependency Map

### Foundation 层（无依赖，优先设计）

1. **资源系统** — 纯数据定义层，无上游依赖；所有经济规则的基础
2. **星图系统** — 星域节点数据模型和连接图；整个战略层的骨架
3. **飞船系统** — 飞船属性、蓝图、实例数据模型；整个飞行战斗层的核心对象

### Core 层（仅依赖 Foundation）

1. **建筑系统** — 依赖：资源系统（建筑的产出和建造消耗以资源单位表示）, 星图系统（读取节点类型资源加成、归属状态、建筑列表及造船厂条件）
2. **飞船生命值系统** — 依赖：飞船系统（生命值是飞船的属性之一）
3. **程序星图生成系统** — 依赖：星图系统（生成符合星图数据模型的节点布局）

### Feature 层（依赖 Core 以上）

1. **殖民地系统** — 依赖：建筑系统, 资源系统, 星图系统
2. **飞船操控系统** — 依赖：飞船系统（操控逻辑作用于飞船实例）
3. **飞船战斗系统** — 依赖：飞船系统, 飞船生命值系统, 飞船操控系统, 星图系统
4. **敌人系统** — 依赖：飞船系统, 飞船生命值系统, 飞船战斗系统
5. **舰队调度系统** — 依赖：星图系统, 飞船系统
6. **存档/读档系统** — 依赖：星图系统, 殖民地系统, 舰队调度系统

### Presentation 层（依赖 Feature/Core）

1. **星图 UI** — 依赖：星图系统, 殖民地系统, 舰队调度系统, 飞船系统
2. **经济 UI** — 依赖：资源系统, 建筑系统, 殖民地系统
3. **舰队 UI** — 依赖：舰队调度系统
4. **飞船 HUD** — 依赖：飞船生命值系统, 飞船战斗系统, 飞船操控系统, 飞船系统
5. **双视角切换系统** — 依赖：星图 UI, 飞船 HUD（连接两个视角层的最终集成点）

### Polish 层

1. **主菜单 UI** — 依赖：存档/读档系统

---

## Recommended Design Order

| 顺序 | 系统 | 优先级 | 层次 | 主责 Agent | 预估工作量 |
|------|------|--------|------|-----------|-----------|
| 1 | 资源系统 | MVP | Foundation | game-designer | S |
| 2 | 星图系统 | MVP | Foundation | game-designer | M |
| 3 | 飞船系统 | MVP | Foundation | game-designer | S |
| 4 | 建筑系统 | MVP | Core | game-designer | S |
| 5 | 飞船生命值系统 | MVP | Core | game-designer | S |
| 6 | 殖民地系统 | MVP | Feature | game-designer | M |
| 7 | 飞船操控系统 | MVP | Feature | game-designer + unity-specialist | L |
| 8 | 飞船战斗系统 | MVP | Feature | game-designer | M |
| 9 | 敌人系统 | MVP | Feature | game-designer | S |
| 10 | 舰队调度系统 | MVP | Feature | game-designer | M |
| 11 | 星图 UI | MVP | Presentation | game-designer + unity-ui-specialist | M |
| 12 | 飞船 HUD | MVP | Presentation | game-designer + unity-ui-specialist | S |
| 13 | 双视角切换系统 | MVP | Presentation | game-designer + unity-specialist | L |
| 14 | 程序星图生成系统 | Vertical Slice | Core | game-designer | M |
| 15 | 经济 UI | Vertical Slice | Presentation | game-designer + unity-ui-specialist | S |
| 16 | 舰队 UI | Vertical Slice | Presentation | game-designer + unity-ui-specialist | S |
| 17 | 存档/读档系统 | Vertical Slice | Feature | game-designer + unity-specialist | M |
| 18 | 主菜单 UI | Vertical Slice | Polish | game-designer + unity-ui-specialist | S |

> **工作量说明**: S = 1 次设计会话（产出完整 GDD），M = 2-3 次会话，L = 4+ 次会话。
> 独立系统可以并行设计（同层且无互相依赖），例如 #1、#2、#3 可以同时开始。

---

## Circular Dependencies

- **无循环依赖** — 依赖图为干净的有向无环图（DAG）。所有系统可按 Foundation → Core → Feature → Presentation → Polish 顺序设计和实现。

---

## High-Risk Systems

| 系统 | 风险类型 | 风险描述 | 缓解策略 |
|------|---------|---------|---------|
| **飞船操控系统** | 设计 + 技术 | 触屏虚拟摇杆手感是公认的难题；X4 等游戏在 PC 上好玩，但触屏飞行操控很容易让人沮丧 | **最优先原型验证** — 在写 GDD 前先用 `/prototype 飞船驾驶舱操控` 验证触屏手感基准线 |
| **双视角切换系统** | 技术 | 切换涉及策略层 + 驾驶层两套场景/相机/UI 状态机同步；是最后集成的系统，但 MVP 假说依赖它 | 提前确定时间暂停规则（切换时策略层是否暂停），作为架构决策先锁定 |
| **星图系统** | 范围 | 瓶颈系统——5 个系统依赖它；数据模型设计失误会导致 殖民地/舰队/程序生成/UI 等大范围返工 | 第 2 个设计，投入充足的设计时间；ADR 锁定核心数据结构后再开始后续设计 |
| **资源系统** | 范围 | 瓶颈系统——3 个系统依赖它；"一目了然"（支柱2）和"足够深度"之间的张力必须在此处解决 | 第 1 个设计，在"资源类型数量"和"复杂度上限"上提前做设计决策 |
| **飞船战斗系统** | 范围 | 飞船战斗单独就是一个完整的小游戏系统，容易无限扩展——需要明确的 MVP 范围边界 | GDD 中明确"MVP 战斗只做：前向武器 + 命中检测 + 伤害扣血"，后续扩展另写 ADR |

---

## Progress Tracker

| 指标 | 数量 |
|------|------|
| 总系统数 | 18 |
| 设计文档已启动 | 13 |
| 设计文档已完成审查 | 0 |
| 设计文档已批准 | 0 |
| MVP 系统已设计 | 13 / 13 |
| Vertical Slice 系统已设计 | 0 / 5 |

---

## Next Steps

- [ ] 运行 `/prototype 飞船驾驶舱操控` — **最优先**，在所有 GDD 之前验证触屏飞行手感
- [ ] 运行 `/design-system 资源系统` — 设计顺序第 1 个
- [ ] 运行 `/design-system 星图系统` — 设计顺序第 2 个（可与资源系统并行）
- [ ] 运行 `/design-system 飞船系统` — 设计顺序第 3 个（可与上两个并行）
- [ ] 运行 `/architecture-decision` — 锁定双视角切换的时间暂停规则（优先于 GDD）
- [ ] 运行 `/design-review design/gdd/[system].md` — 每个 GDD 完成后在新会话中运行
- [ ] 运行 `/gate-check pre-production` — 所有 MVP 系统 GDD 完成并审查后
