# Game Concept: 星链霸权 (Starchain Hegemony)

*Created: 2026-04-11*
*Status: Draft*

---

## Elevator Pitch

> 一款安卓太空即时战略游戏，你在星图上建设殖民地、经营经济网络、指挥舰队扩张领土——同时可以随时跳进任何一艘船，切换到第一或第三人称亲自驾驶战斗。经济驱动军事，军事开辟经济，打造属于你的持续成长的星际帝国。

---

## Core Identity

| Aspect | Detail |
| ---- | ---- |
| **Genre** | 太空即时战略 + 太空飞行模拟（双层玩法） |
| **Platform** | 纯安卓（手机/平板） |
| **Target Audience** | 探索者型玩家，X4/星际类游戏爱好者，寻找手机端深度策略体验的中核玩家 |
| **Player Count** | 单人游戏 |
| **Session Length** | 碎片化可玩（5-15 分钟短操作），但存档持续，帝国持续成长 |
| **Monetization** | 待定（建议买断制或免费核心体验） |
| **Estimated Scope** | 大型（完整版 8-12 个月，独立开发；MVP 约 4-6 周） |
| **Comparable Titles** | X4: Foundations, Stellaris Mobile, Infinite Galaxy |

---

## Core Fantasy

你是一个新生星际文明的缔造者。从一颗孤零零的殖民星出发，你建立矿场、造船厂、能源网络，将资源转化为舰队战力，用舰队征服新星域，用新星域的资源建更大的舰队。

而你不只是在星图上移动棋子——你可以在任意时刻切换视角，钻进旗舰驾驶舱，亲手驾驶它突入战场。宏观与微观，战略与操控，都是你的。

**核心情感承诺**: 同时拥有上帝视角的运筹帷幄感，和第一人称驾驶舱的沉浸临场感。

---

## Unique Hook

像简化版 X4 奠基的经济-军事循环，**AND ALSO** 专为触屏设计的无缝"星图指挥 ↔ 亲自驾驶"切换——手机上从未有过这种体验。

---

## Player Experience Analysis (MDA Framework)

### Target Aesthetics (What the player FEELS)

| Aesthetic | Priority | How We Deliver It |
| ---- | ---- | ---- |
| **Sensation** (sensory pleasure) | 3 | 飞船驾驶的视觉冲击、资源流动的反馈动效 |
| **Fantasy** (make-believe, role-playing) | 2 | 亲自指挥和驾驶自己建造的舰队 |
| **Narrative** (drama, story arc) | N/A | 不是叙事驱动的游戏 |
| **Challenge** (obstacle course, mastery) | 4 | 资源分配决策、扩张时机判断 |
| **Fellowship** (social connection) | N/A | 单人游戏 |
| **Discovery** (exploration, secrets) | 5 | 探索程序生成的星图、发现新资源节点 |
| **Expression** (self-expression, creativity) | 1 | 殖民地布局、舰队配置、发展路线自由度 |
| **Submission** (relaxation, comfort zone) | N/A | 不是放置类体验 |

### Key Dynamics (Emergent player behaviors)

- 玩家会自然形成"优先经济还是优先军事扩张"的个人风格
- 玩家会在关键战役中主动切换到驾驶舱亲自操控，形成策略性微操时机判断
- 玩家会不断优化殖民地布局和生产链，追求更高效的资源循环
- 玩家会探索程序生成星图的边界，寻找稀有资源节点

### Core Mechanics (Systems we build)

1. **星图经济系统** — 殖民地建筑生产资源链，资源驱动舰队建造和维持
2. **舰队调度系统** — 在星图上拖拽/指令派遣舰队执行任务（侦察/占领/防守）
3. **双视角切换系统** — 任意时刻从星图策略视角无缝切换到任意飞船的第一/第三人称驾驶视角
4. **飞船操控系统** — 触屏虚拟摇杆/手势控制的飞船飞行和战斗
5. **程序星图生成** — 每次新存档生成独特的星域布局，保证重玩性

---

## Player Motivation Profile

### Primary Psychological Needs Served

| Need | How This Game Satisfies It | Strength |
| ---- | ---- | ---- |
| **Autonomy** (freedom, meaningful choice) | 多种发展路径（经济流/军事流/混合流），自由配置舰队和殖民地 | 核心 |
| **Competence** (mastery, skill growth) | 帝国管理从混乱到流畅的成长弧线，驾驶技术不断提升 | 核心 |
| **Relatedness** (connection, belonging) | 与"自己建造的帝国"的情感归属，每艘船都是自己造的 | 支撑 |

### Player Type Appeal (Bartle Taxonomy)

- [x] **Achievers** — 帝国扩张进度、解锁新舰船/建筑、领土数量
- [x] **Explorers** — 探索程序生成星图、发现系统交互规律
- [ ] **Socializers** — 单人游戏，不适用
- [ ] **Killers/Competitors** — 目前无PvP，不适用

### Flow State Design

- **Onboarding curve**: 从单个殖民星 + 1 艘侦察船开始，教程引导第一个资源链建立
- **Difficulty scaling**: 程序生成的敌对势力强度随玩家帝国规模动态调整
- **Feedback clarity**: 星图上的资源流动动效、建筑产出数字、舰队战斗结果即时显示
- **Recovery from failure**: 帝国持续存档，失去星域后可以重新夺回，不存在"游戏结束重来"

---

## Core Loop

### Moment-to-Moment (30 秒)

点击星域 → 下达指令（建造/派舰队/切换视角）→ 看到即时反馈（建筑开始建造/舰队出发/进入驾驶舱）。每个操作都在 1-2 次点击内完成。可随时暂停从容规划，继续后看计划展开。

### Short-Term (5-15 分钟)

一次扩张行动的完整流程：发现目标星域 → 确认舰队力量充足 → 派遣舰队 → 可选择跳入旗舰亲自参与战斗 → 占领 → 建立殖民地 → 接入经济网络，开始产出新资源。

### Session-Level (可变，任意时刻保存)

完成 1-3 次星域扩张，优化现有殖民地产业链，解锁 1-2 个新科技或舰船蓝图。每次游玩都有明确的进展感，但没有强制性的会话结构——放下随时可以继续。

### Long-Term Progression

- 解锁更强大的舰船蓝图（侦察艇 → 驱逐舰 → 巡洋舰）
- 建筑科技树扩展（更高效的矿场、更大的造船厂）
- 帝国领土从 3-5 个星域扩张到覆盖整个星图
- 遭遇不同类型的敌对势力和特殊星域事件

### Retention Hooks

- **好奇心**: 星图边缘还有什么？程序生成的稀有资源节点在哪里？
- **投入感**: 花时间建设的帝国、精心配置的舰队，玩家不舍得抛弃
- **精通感**: 越来越流畅地管理经济链条和舰队调度，从手忙脚乱到游刃有余
- **驾驶体验**: 每次指挥大战役前跳入驾驶舱的期待感

---

## Game Pillars

### Pillar 1: 经济即军事
殖民地经济产出和舰队战力是同一个循环的两面——没有经济就没有舰队，没有舰队就无法扩张经济。两个系统永远互相驱动。

*Design test*: 如果在"添加纯装饰性殖民地建筑"和"添加有经济产出的功能建筑"之间选择，这个支柱说选后者——每个建筑都必须对经济-军事循环有直接意义。

### Pillar 2: 一目了然的指挥
所有信息应该一眼可读，所有操作应该在 1-2 次点击内完成。玩家是指挥官，不是数据分析师。UI 为手机触屏优化。

*Design test*: 如果在"显示详细数值面板"和"用颜色/图标直观显示状态"之间选择，这个支柱说选后者——情报密度不能压倒直觉判断。

### Pillar 3: 我的星际帝国
玩家的帝国是一个持续存在、不断成长的单存档——每次打开游戏都是在上次的基础上继续扩张、优化、应对挑战。玩家对帝国的归属感和投入感是核心体验。

*Design test*: 如果在"roguelike 局式重开结构"和"持续经营单存档模式"之间选择，这个支柱说选后者——玩家必须感觉是在建设"自己的"帝国。

### Pillar 4: 从星图到驾驶舱
玩家可以在任意时刻从星图指挥视角无缝切换到任何一艘船的第一/第三人称视角，亲手驾驶、战斗、探索。宏观与微观都是同一个游戏体验的一部分。

*Design test*: 如果在"舰队战斗只用自动结算"和"让玩家能跳进任意飞船亲自指挥战斗"之间选择，这个支柱说选后者——玩家的自由度比效率更重要。

### Anti-Pillars (What This Game Is NOT)

- **NOT 4X 深度模拟**: 不做外交系统、间谍机制、文化胜利——这些会把范围炸开，稀释核心体验
- **NOT 放置/挂机游戏**: 离线时帝国保持当前状态，游戏内每一刻都需要玩家主动决策——不能"挂着自己跑"

---

## Inspiration and References

| Reference | What We Take From It | What We Do Differently | Why It Matters |
| ---- | ---- | ---- | ---- |
| **X4: Foundations** | 经济-军事循环、可亲自驾驶任意船只的自由感 | 精简范围适配手机，触屏优先设计，无外交/贸易复杂度 | 直接验证了核心幻想的市场吸引力 |
| **Stellaris** | 星图扩张的宏观策略感、程序生成星域 | 不做4X深度，专注经济-军事双层玩法 | 证明太空策略在手机端有受众 |
| **Infinite Galaxy** | 手机太空策略的 UI/UX 参考 | 不做社交/联盟/氪金驱动，做单人深度体验 | 证明这个题材在手机端的可行性 |

**非游戏灵感**:
- 科幻小说：《银河帝国》系列的帝国建立幻想
- 纪录片：宏观的宇宙尺度 + 微观的飞行器驾驶感的对比

---

## Target Player Profile

| Attribute | Detail |
| ---- | ---- |
| **Age range** | 18-35 |
| **Gaming experience** | 中核到硬核（玩过 X4、Stellaris 或类似复杂策略游戏） |
| **Time availability** | 碎片化游玩，通勤/午休 5-15 分钟，周末可能连续游玩 |
| **Platform preference** | 手机为主，习惯手机游戏 |
| **Current games they play** | X4: Foundations（PC）、Stellaris、星际争霸类 |
| **What they're looking for** | 手机上真正有深度的太空策略体验，不是挂机氪金游戏 |
| **What would turn them away** | 强制社交、Pay-to-Win、过度简化失去策略深度 |

---

## Technical Considerations

| Consideration | Assessment |
| ---- | ---- |
| **Recommended Engine** | 待定——运行 `/setup-engine` 进行完整决策（安卓3D考虑Unity或Godot） |
| **Key Technical Challenges** | 星图策略层与第一/第三人称飞船层的无缝视角切换；触屏飞船操控手感；安卓3D性能优化 |
| **Art Style** | 低多边形/风格化太空（控制美术工作量）；星图用2D俯视，驾驶舱用3D视角 |
| **Art Pipeline Complexity** | 中等（飞船模型+殖民地建筑+太空背景，风格化降低精度要求） |
| **Audio Needs** | 中等（飞船引擎声、武器音效、环境太空音乐） |
| **Networking** | 无（单人游戏） |
| **Content Volume** | MVP: 1种船/3种建筑; 完整版: 8+种船/10+种建筑/程序星图 |
| **Procedural Systems** | 程序生成星图（节点布局、资源分布），降低关卡设计工作量 |

---

## Risks and Open Questions

### Design Risks
- 双层玩法（策略层 + 飞船操控层）可能导致两层都做得不够深，两者都不出色
- 持续存档模式需要足够丰富的长期进展内容，否则玩家很快到达"天花板"
- 触屏上的第一人称飞船操控手感可能难以达到预期——虚拟摇杆体验普遍差

### Technical Risks
- 安卓3D性能：大规模舰队 + 实时星图 + 飞船驾驶对移动设备GPU压力大
- 两个视角层之间的无缝切换涉及场景/相机/UI的复杂状态管理
- 第一次做游戏 + 如此复杂的系统组合 = 技术风险极高

### Market Risks
- 手机玩家对深度策略游戏的付费意愿有限
- 题材竞争：太空策略在手机端已有成熟商业产品（Infinite Galaxy等）

### Scope Risks
- **最大风险**: RTS层 + 第一人称飞船层 = 两种游戏类型的完整工作量。几周内完成MVP已是极限
- 第一人称飞船操控单独就是一个完整的小游戏系统
- 内容量（多种舰船、建筑、星域事件）可能超出独立开发的内容产能

### Open Questions
- 飞船操控在触屏上怎么做才好玩？（需要最优先原型验证）
- 策略层暂停后驾驶舱是否也暂停？（时间流逝规则需要设计清楚）
- 经济循环的复杂度上限在哪里才能保持"一目了然"而不失去深度？

---

## MVP Definition

**Core hypothesis**: 玩家会享受在星图指挥视角和第一/第三人称驾驶舱视角之间无缝切换的体验，并因为这个自由度而对这款太空策略游戏产生持续游玩动力。

**Required for MVP**:
1. 星图界面，包含 3-5 个星域节点，可点击派遣舰队
2. 1 种殖民地类型 + 2 种建筑（基础矿场 + 造船厂），简单资源经济
3. 1 种飞船（可在星图调度，也可切入第一/第三人称亲自驾驶）
4. 基本战斗（飞船可攻击固定目标/简单敌人）
5. 星图视角 ↔ 飞船驾驶视角的无缝切换

**Explicitly NOT in MVP** (defer to later):
- AI 对手帝国
- 科技树
- 多种舰船类型
- 经济平衡调整
- 存档系统（MVP阶段不需要）
- 音效和配乐

### Scope Tiers (if budget/time shrinks)

| Tier | Content | Features | Timeline |
| ---- | ---- | ---- | ---- |
| **MVP** | 1种飞船、2种建筑、静态小星图 | 星图+驾驶舱切换、基本战斗 | 4-6 周，独立开发 |
| **Vertical Slice** | 3种飞船、4种建筑、小型程序星图 | +资源经济、简单AI、存档 | 2-3 个月，独立开发 |
| **Alpha** | 5种飞船、6种建筑、完整程序星图 | +科技树、AI对手策略、完整经济 | 4-6 个月，独立开发 |
| **Full Vision** | 8+种飞船、10+种建筑 | +多样AI行为、成就系统、打磨 | 8-12 个月，独立开发 |

---

## Visual Identity Anchor

*（待 `/art-bible` 技能完善——以下为初步方向）*

**方向**: 「深空极简」
- **视觉规则**: 太空的空旷感衬托出每一个人造物的存在感——殖民地、飞船、激光都在黑暗中发光
- **氛围目标**: 孤独而壮阔；你的帝国是黑暗宇宙中的光点
- **形态语言**: 飞船为硬边几何体（工业感）；殖民地为模块化六边形网格；星图为简洁连线图
- **色彩哲学**: 深空黑色背景；殖民地用冷蓝色系（科技感）；敌对势力用红/橙色系；资源流动用金色粒子效果

---

## Next Steps

- [ ] 运行 `/setup-engine` 配置引擎并填充版本参考文档
- [ ] 运行 `/art-bible` 建立完整视觉身份规范（在写任何 GDD 之前）
- [ ] 运行 `/design-review design/gdd/game-concept.md` 验证概念完整性
- [ ] 与 `creative-director` 讨论支柱细化
- [ ] 运行 `/map-systems` 将概念分解为独立系统（含依赖关系）
- [ ] 运行 `/design-system [系统名]` 为每个系统撰写详细 GDD
- [ ] 运行 `/create-architecture` 生成主架构蓝图
- [ ] 运行 `/architecture-decision` × N 记录关键架构决策
- [ ] 运行 `/gate-check` 进入制作阶段前的阶段门控验证
- [ ] 运行 `/prototype 飞船驾驶舱切换` 原型验证最高风险系统
- [ ] 运行 `/playtest-report` 验证核心假说
- [ ] 运行 `/sprint-plan new` 规划第一个 Sprint
