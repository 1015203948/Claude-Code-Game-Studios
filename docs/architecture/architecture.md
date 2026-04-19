# 星链霸权 (Starchain Hegemony) — Master Architecture

## Document Status

| Field | Value |
|-------|-------|
| **Version** | 1 |
| **Last Updated** | 2026-04-14 |
| **Engine** | Unity 6.3 LTS |
| **GDDs Covered** | resource-system, star-map-system, ship-system, building-system, ship-health-system, colony-system, ship-control-system, ship-combat-system, enemy-system, fleet-dispatch-system, star-map-ui, ship-hud, dual-perspective-switching |
| **ADRs Referenced** | ADR-0001, ADR-0002, ADR-0003, ADR-0004 |
| **Technical Director Sign-Off** | 2026-04-14 — APPROVED |
| **Lead Programmer Feasibility** | Skipped — Lean mode |

---

## Engine Knowledge Gap Summary

| Risk Level | Domain | Key Implication |
|-----------|--------|----------------|
| HIGH | Input System | `finger.index` 是池槽位非稳定 ID — 已在 ADR-0003 修正，使用 `Finger` 对象引用 |
| HIGH | Async Lifecycle | `destroyCancellationToken` 在 MasterScene 永久对象上永不触发 — 永久对象须用 `Application.exitCancellationToken` |
| HIGH | Game Logic Timing | `UniTask.WaitForSeconds` 受 Android 后台节流 — 殖民地计时须改为时间戳 + 离线补偿 |
| MEDIUM | UI Toolkit | `VisualElement.transform` 在 Unity 6.2 废弃 — 使用 `style.translate/rotate/scale` |
| MEDIUM | Scene Loading | `allowSceneActivation=false` 时 `progress` 上限 0.9f — 就绪检测须用 `>= 0.9f` |
| MEDIUM | Audio | 双场景 AudioListener 冲突 — AudioListener 仅挂载在 MasterScene |
| LOW | SceneManager | `LoadSceneAsync(Additive)` 未变更，可靠 |
| LOW | SO Channel | ScriptableObject Channel 模式在 Unity 6.3 LTS 推荐，无替代 |
| LOW | Physics | `Rigidbody.linearDamping` 为 Unity 6 重命名（原 `.drag`），ADR-0003 已采用 |

---

## System Layer Map

### 架构层级图

```
┌──────────────────────────────────────────────────────────────────┐
│  PRESENTATION LAYER                                              │
│  星图 UI · 飞船 HUD · 双视角切换系统                              │
│  [VS] 经济 UI · 舰队 UI · 主菜单 UI                              │
├──────────────────────────────────────────────────────────────────┤
│  FEATURE LAYER                                                   │
│  殖民地系统 · 飞船操控系统 · 飞船战斗系统                          │
│  敌人系统 · 舰队调度系统                                          │
│  [VS] 存档/读档系统                                               │
├──────────────────────────────────────────────────────────────────┤
│  CORE LAYER                                                      │
│  资源系统 · 星图系统 · 飞船系统                                   │
│  建筑系统 · 飞船生命值系统                                        │
│  [VS] 程序星图生成系统                                            │
├──────────────────────────────────────────────────────────────────┤
│  FOUNDATION LAYER                                                │
│  场景管理 (ADR-0001) · 事件总线 (ADR-0002)                        │
│  输入系统 (ADR-0003) · 数据模型框架 (ADR-0004)                    │
├──────────────────────────────────────────────────────────────────┤
│  PLATFORM LAYER  [外部，非我们实现]                               │
│  Unity 6.3 LTS · PhysX · UI Toolkit · Android OS                │
└──────────────────────────────────────────────────────────────────┘
```

### 系统分层详表

| 系统 | 架构层 | GDD | 里程碑 | 引擎风险 |
|------|--------|-----|--------|---------|
| 场景管理 | Foundation | — (ADR-0001) | MVP | MEDIUM |
| 事件总线 | Foundation | — (ADR-0002) | MVP | LOW |
| 输入系统 | Foundation | — (ADR-0003) | MVP | HIGH ✓修正 |
| 数据模型框架 | Foundation | — (ADR-0004) | MVP | LOW |
| 资源系统 | Core | resource-system.md | MVP | LOW |
| 星图系统 | Core | star-map-system.md | MVP | LOW |
| 飞船系统 | Core | ship-system.md | MVP | MEDIUM |
| 建筑系统 | Core | building-system.md | MVP | LOW |
| 飞船生命值系统 | Core | ship-health-system.md | MVP | LOW |
| 程序星图生成系统 | Core | — | Vertical Slice | LOW |
| 殖民地系统 | Feature | colony-system.md | MVP | HIGH ✓记录 |
| 飞船操控系统 | Feature | ship-control-system.md | MVP | HIGH ✓修正 |
| 飞船战斗系统 | Feature | ship-combat-system.md | MVP | LOW |
| 敌人系统 | Feature | enemy-system.md | MVP | LOW |
| 舰队调度系统 | Feature | fleet-dispatch-system.md | MVP | LOW |
| 存档/读档系统 | Feature | — | Vertical Slice | LOW |
| 星图 UI | Presentation | star-map-ui.md | MVP | MEDIUM |
| 飞船 HUD | Presentation | ship-hud.md | MVP | MEDIUM |
| 双视角切换系统 | Presentation | dual-perspective-switching.md | MVP | HIGH |
| 经济 UI | Presentation | — | Vertical Slice | MEDIUM |
| 舰队 UI | Presentation | — | Vertical Slice | MEDIUM |
| 主菜单 UI | Presentation | — | Vertical Slice | LOW |

> **[VS]** = Vertical Slice 里程碑，不在 MVP 范围内。
> **HIGH ✓修正** = 已在对应 ADR 中记录并给出修正方案。
> **HIGH ✓记录** = 已在 ADR 中记录，等待 ADR-0013/0014/0015 (Combat/Health/Enemy) 给出最终实现方案。

---

## Module Ownership

### Foundation Layer

| 模块 | Owns | Exposes | Consumes | 引擎 API |
|------|------|---------|----------|---------|
| **场景管理** | ViewLayer 状态机；场景加载队列；`_isSwitching` 守卫标志 | `ViewLayerManager.SwitchTo(ViewLayer)`；`ViewLayerChannel` (SO) | 无（最底层） | `SceneManager.LoadSceneAsync(Additive)` · `UnloadSceneAsync` · `Application.exitCancellationToken` ⚠️HIGH |
| **事件总线** | SO Channel 资产（`assets/data/channels/`） | 各 SO Channel 实例（`ShipStateChannel`、`ViewLayerChannel`、`CombatChannel` 等） | 无（被动基础设施） | `ScriptableObject`（Unity 原生）LOW |
| **输入系统** | `StarMapActions` / `CockpitActions` InputActionMap；当前活跃 ActionMap | `ShipInputChannel`（SO，当前帧输入快照）；`InputRouter.SetActiveMap(ActionMap)` | `ViewLayerChannel`（ViewLayer 变更触发 ActionMap 切换） | `InputSystem` · `EnhancedTouchSupport.Enable()` · `Finger` 对象引用（非 `finger.index`）⚠️HIGH ✓修正 |
| **数据模型框架** | SO Config 加载规范；运行时 C# 数据类生命周期约定 | 约定接口：Config SO 位于 `assets/data/config/`；运行时状态为纯 C# 类 | 无（框架约定，非运行时组件） | `ScriptableObject`（只读配置层）LOW |
| **SimClock** | `SimRate ∈ {0, 1, 5, 20}`；`DeltaTime = Time.unscaledDeltaTime × SimRate` | `SimClock.Instance.DeltaTime`（策略层 Update 替代 Time.deltaTime）；`SimRateChanged` 事件（SO） | 无（最底层基础设施） | `Time.unscaledDeltaTime`（长期稳定 API，无 post-cutoff 变化）⚠️LOW |

**Foundation 内部依赖：**
```
数据模型框架 ──（约定）──► 场景管理、事件总线
输入系统    ──消费──► ViewLayerChannel (事件总线)
场景管理    ──发布──► ViewLayerChannel (事件总线)
```

### Core Layer

| 模块 | Owns | Exposes | Consumes | 引擎 API |
|------|------|---------|----------|---------|
| **资源系统** | `ResourceConfig` SO（矿石/能量基础产率）；`ORE_CAP` / `ENERGY_CAP` 常量 | `ResourceConfig.CanAfford(cost)` | 无（纯数据定义层） | `ScriptableObject` LOW |
| **星图系统** | 星域图 `G=(V,E)`；节点所有权/迷雾/类型状态；`IsVisible(n)` 可见性逻辑 | `StarMapData`（运行时图对象）；`NodeOwnershipChangedChannel` (SO) | 无 | 无引擎特定 API LOW |
| **飞船系统** | `ShipBlueprint` SO（MaxHull/ThrustPower/TurnSpeed 等）；`ShipDataModel`（MasterScene 权威数据：CurrentHull、ShipState、位置） | `ShipDataModel`（全局单例读写）；`ShipStateChannel` (SO) | `ResourceConfig`（建造费用校验） | `ScriptableObject` · `Application.exitCancellationToken` ⚠️MEDIUM |
| **建筑系统** | `BuildingConfig` SO；`BuildingInstance`（InstanceId/BuildingType/NodeId/IsActive）；`ShipyardTier` 整数字段 | `BuildingRegistry.GetBuildings(nodeId)`；`OnBuildingConstructedChannel` (SO) | `ResourceConfig.CanAfford()`；`StarMapData`（节点类型/资源加成） | `ScriptableObject` LOW |
| **飞船生命值系统** | `CurrentHull` 当前值；伤害计算逻辑；`DESTROYED` 触发序列 | `HealthSystem.ApplyDamage(instanceId, rawDamage, damageType)`；`OnHullChangedChannel` (SO) | `ShipDataModel.ShipState`（状态门控）；`ShipDataModel.MaxHull` | 无特定引擎 API LOW |

**Core 内部依赖（单向）：**
```
资源系统 ◄── 建筑系统（CanAfford）
         ◄── 飞船系统（建造费用）
星图系统 ◄── 建筑系统（节点类型查询）
飞船系统 ◄── 飞船生命值系统（ShipState 读取）
```

### Feature Layer

| 模块 | Owns | Exposes | Consumes | 引擎 API |
|------|------|---------|----------|---------|
| **殖民地系统** | 全局资源池（`_oreCurrent`/`_energyCurrent`，StarMapScene）；产出 tick 逻辑 | `OnResourcesUpdatedChannel`（`ResourceSnapshot`，每 tick）；`OnShipBuiltChannel` (SO)；`OnEnergyDeficitChannel` (SO) | `BuildingRegistry`（各节点产出/消耗）；`ResourceConfig`（上限常量）；`ShipDataModel`（建造状态） | ⚠️HIGH：`WaitForSecondsRealtime` 须改为**时间戳+离线补偿**方案 |
| **飞船操控系统** | 左/右摇杆输入状态；`_leftFinger`/`_rightFinger` 追踪引用；死区归一化后的推力/转向向量 | 无（直接驱动 Rigidbody） | `ShipInputChannel`（输入快照）；`ShipDataModel.ShipState`（C-1 门控） | `Rigidbody.AddForce` · `Rigidbody.linearDamping` · `EnhancedTouch.Finger` 引用 ⚠️HIGH ✓修正 |
| **飞船战斗系统** | 战斗状态（进行中/结束）；自动开火计时；`FIRE_ANGLE_THRESHOLD` | `CombatSystem.BeginCombat()`；`CombatVictoryChannel` / `CombatDefeatChannel` (SO) | `ShipDataModel.ShipState`；`EnemySystem`（目标位置/角度）；`HealthSystem.ApplyDamage()` | `Physics.Raycast`（命中检测）LOW |
| **敌人系统** | 各敌人实例 HP；AI 状态机（SPAWNING/APPROACHING/FLANKING/DYING）；生成位置逻辑 | `OnEnemyDiedChannel` (SO) | `CombatSystem.BeginCombat` 触发生成；`ShipDataModel`（玩家位置，寻敌） | `Rigidbody`（敌人移动）LOW |
| **舰队调度系统** | 舰队实例移动状态（In Transit/Docked）；BFS 路径；`FLEET_TRAVEL_TIME=3s` 计时 | `FleetDispatch.Dispatch(shipId, targetNodeId)`；`FleetArrivedChannel` (SO) | `StarMapData`（邻接图，BFS 输入）；`ShipDataModel.ShipState`（D-1 前置条件） | `UniTask.Delay`（WaitForSecondsRealtime 模式）LOW |

**Feature 内部依赖：**
```
殖民地系统   ◄── 建筑系统（产出计算）
飞船操控系统 ◄── 输入系统（ShipInputChannel）
飞船战斗系统 ◄── 飞船操控系统（方向/速度状态）
             ◄── 敌人系统（目标数据）
             ◄── 飞船生命值系统（伤害接口）
舰队调度系统 ◄── 星图系统（BFS 图数据）
             ◄── 飞船系统（ShipState 门控）
```

### Presentation Layer

| 模块 | Owns | Exposes | Consumes | 引擎 API |
|------|------|---------|----------|---------|
| **星图 UI** | 节点 UI 元素（颜色/标签/选中态）；触摸手势状态（捏合缩放/拖拽平移）；节点点击事件路由 | `OnNodeSelectedChannel` (SO) | `StarMapData`（图结构/归属/迷雾）；`FleetArrivedChannel`；`OnResourcesUpdatedChannel` | `UI Toolkit EventSystem`（优先级高于 StarMapActions）⚠️MEDIUM · 触摸热区 ≥ 48dp |
| **飞船 HUD** | 弧形血条渲染状态；软锁定瞄准具位置；视角切换按钮交互 | 无（纯消费/显示层） | `OnHullChangedChannel`；`ShipDataModel.ShipState`（可见性门控）；`ViewLayerChannel` | `UI Toolkit`（弧形进度条、覆盖层）⚠️MEDIUM |
| **双视角切换系统** | 场景加载/卸载序列；`_isSwitching` 并发守卫；`_preEnterState` 快照/恢复；摄像机启用切换 | `ViewLayerManager.SwitchTo(ViewLayer)` | `ViewLayerChannel`（广播切换结果）；`ShipDataModel`（切换前状态快照）；`InputRouter`（ActionMap 切换） | `Camera.enabled`（非 SetActive）· `SceneManager.LoadSceneAsync(Additive)` · progress ≥ 0.9f ⚠️HIGH |

**Presentation 内部依赖：**
```
双视角切换系统 ──触发──► 星图 UI 显示/隐藏
               ──触发──► 飞船 HUD 显示/隐藏
飞船 HUD       ◄── 飞船生命值系统（OnHullChanged）
星图 UI        ◄── 殖民地系统（OnResourcesUpdated）
               ◄── 舰队调度系统（FleetArrived）
```

**全局跨层依赖图：**
```
Platform ◄── Foundation ◄── Core ◄── Feature ◄── Presentation
                  ▲                        │
                  └────────────────────────┘
                    （事件总线 SO Channel 上行通知）
```

---

## Data Flow

### 1. 帧更新路径

```
[Android 触摸事件]
     │ Enhanced Touch API（Finger 引用，非 finger.index）
     ▼
InputSystem (Foundation)
     │ ShipInputChannel SO（归一化推力/转向向量，当前帧快照）
     ▼
飞船操控系统 (Feature)
     │ Rigidbody.AddForce / linearDamping（同步调用）
     ▼
PhysX (Platform)
     │ 物理结算后位置/速度更新
     ▼
ShipDataModel（位置写回）
     │
     ▼
飞船 HUD / 星图 UI（消费位置数据，下一帧渲染）
```

- 全链路同步调用，无跨线程
- `ShipInputChannel` 是当前帧快照，Feature 层读取后处理

---

### 2. 事件通知路径（SO Channel）

```
[生产者系统（如殖民地系统）]
     │ channel.Raise(payload)（同步广播）
     ▼
SO Channel 资产（事件总线 Foundation）
     │ 遍历订阅者列表
     ▼
消费者 A（如星图 UI）─── 刷新资源显示
消费者 B（如飞船 HUD）── 刷新能量警告
```

- 同步广播，调用栈内完成
- **Tier 1**（跨场景）：SO Channel — 生产者/消费者可在不同 Unity Scene
- **Tier 2**（同场景跨系统）：C# `event Action<T>`
- **Tier 3**（系统内）：直接方法调用
- `ResourceSnapshot` 须为 `readonly struct`，避免 `UnityEvent<T>` 装箱

---

### 3. 存档/读档路径（Vertical Slice）

```
[玩家触发存档]
     ▼
存档系统（Feature）
     │ 序列化运行时状态：
     ├── StarMapData（节点归属/迷雾/连接）
     ├── ColonySystem（资源当前值/各节点建筑列表）
     ├── FleetDispatch（在途舰队位置+目标）
     └── ShipDataModel（CurrentHull/ShipState）
     │ 写入 Application.persistentDataPath
     ▼
[读档] 反序列化 → 写入各系统运行时状态
     │ ColonySystem 计算 Δt 离线补偿产出
     ▼
各系统广播 StateRestored → UI 刷新
```

- 序列化对象：纯 C# 运行时状态类（非 MonoBehaviour / SO）
- SO Config 资产**不序列化**（只读，重启后从 `assets/data/config/` 重新加载）
- 离线补偿：`Δt = 当前时间戳 − 存档时间戳`，按公式补算产出

---

### 4. 初始化顺序（Boot Order）

```
1. MasterScene（永远第一）
   ├── DataModelFramework：加载所有 SO Config 资产
   ├── EventBus：SO Channel 资产就绪
   ├── InputSystem：EnhancedTouchSupport.Enable()（全局一次）
   └── ShipDataModel / ViewLayerManager：初始化运行时状态

2. StarMapScene（Additive，紧随 MasterScene）
   ├── ColonySystem：订阅 BuildingRegistry，启动产出 tick
   ├── StarMapUI：订阅 StarMapData，渲染初始星图
   └── FleetDispatch：读取 StarMapData，就绪

3. CockpitScene（按需 Additive 加载）
   ├── ShipControlSystem：订阅 ShipInputChannel
   ├── CombatSystem：待机（等待 BeginCombat()）
   └── ShipHUD：订阅 OnHullChanged / ViewLayerChannel
```

- MasterScene 必须在所有场景之前完全初始化（Awake 完成后再加载子场景）
- StarMapScene 订阅须在 OnEnable 中完成（晚于 MasterScene Awake）
- CockpitScene 每次加载后须执行"拉取订阅"（pull-on-subscribe），防止错过 MasterScene 广播

---

## API Boundaries

### Foundation Layer

```csharp
// 场景管理
public class ViewLayerManager : MonoBehaviour   // MasterScene 单例
{
    // 调用方保证：调用前 ShipState 须为合法切换状态
    // 保证：切换期间 _isSwitching=true，拒绝重入；切换后广播 ViewLayerChannel
    public UniTask SwitchTo(ViewLayer target);
    public ViewLayer Current { get; }
}

// 输入系统
[CreateAssetMenu] public class ShipInputChannelSO : ScriptableObject
{
    public Vector2 ThrustInput;   // 左摇杆，归一化，死区已剔除
    public Vector2 AimInput;      // 右摇杆，归一化
    // 每帧由 InputRouter 写入；Feature 层只读
}

// SO Channel 基类（事件总线）
public abstract class ChannelSO<T> : ScriptableObject
{
    // 保证：同步广播，不跨帧；订阅/取消订阅在 OnEnable/OnDisable 成对调用
    public void Raise(T payload);
    public void Subscribe(Action<T> listener);
    public void Unsubscribe(Action<T> listener);
}
```

### Core Layer

```csharp
// 资源系统
[CreateAssetMenu] public class ResourceConfigSO : ScriptableObject
{
    public float BaseOreRate;
    public float BaseEnergyRate;
    public int   OreCap;         // 须 > 0，启动时校验
    public int   EnergyCap;
    // 调用方保证：cost 所有字段 >= 0
    public bool CanAfford(ResourceCost cost);
}

// 星图系统
public class StarMapData                         // MasterScene 持有
{
    // 保证：返回值为防御性拷贝，外部不可修改图结构
    public IReadOnlyList<StarNode> GetAllNodes();
    public IReadOnlyList<StarEdge> GetEdges(int nodeId);
    public bool IsVisible(int nodeId);
    // 调用方保证：nodeId 存在于图中
    public void SetOwnership(int nodeId, Owner owner);
}

// 飞船系统
public class ShipDataModel : MonoBehaviour        // MasterScene 单例
{
    public ShipState State { get; private set; }
    public int CurrentHull { get; set; }          // 仅 HealthSystem 写入
    public int MaxHull { get; }
    // 保证：状态转换合法性由内部状态机校验
    public void TransitionTo(ShipState next);
}

// 飞船生命值系统
public class HealthSystem : MonoBehaviour
{
    // 调用方保证：rawDamage >= 0；instanceId 存在
    // 保证：ShipState 非 IN_COCKPIT/IN_COMBAT 时静默忽略
    public void ApplyDamage(int instanceId, float rawDamage, DamageType type);
}
```

### Feature Layer

```csharp
// 飞船战斗系统
public class CombatSystem : MonoBehaviour
{
    // 调用方保证：ShipState==IN_COCKPIT 时才可调用
    // 保证：调用后 ShipState 切换至 IN_COMBAT，触发敌人生成
    public void BeginCombat(int enemyNodeId);
}

// 舰队调度系统
public class FleetDispatch : MonoBehaviour
{
    // 调用方保证：满足 D-1 前置条件（DOCKED + PLAYER_OWNED + EXPLORED + 无冲突）
    // 保证：UniTask 延迟后广播 FleetArrivedChannel；取消时原路返回
    public UniTask Dispatch(int shipId, int targetNodeId, CancellationToken ct);
}
```

### Presentation Layer

```csharp
// 双视角切换系统的对外入口即 ViewLayerManager.SwitchTo()（见 Foundation）
// 星图 UI 和飞船 HUD 为纯消费层，无对外公共接口
```

---

## ADR Audit

### ADR 质量审查

| ADR | 引擎兼容性节 | 版本记录 | GDD 需求链接 | 与本架构冲突 | 有效性 |
|-----|------------|---------|-------------|------------|--------|
| ADR-0001 场景管理 | ✅ | ✅ Unity 6.3 LTS | ✅ TR-dvs-001~008, TR-ship-004 | 无 | ✅ |
| ADR-0002 事件通信 | ✅ | ✅ Unity 6.3 LTS | ✅ TR-starmap-004/005, TR-ship-003, TR-colony-003~005 等 | 无 | ✅ |
| ADR-0003 输入系统 | ✅ | ✅ Unity 6.3 LTS | ✅ TR-control-001~005, TR-dvs-007, TR-starmap-ui-003/004 | 无 | ✅ |
| ADR-0004 数据模型 | ✅ | ✅ Unity 6.3 LTS | ✅ TR-resource-001, TR-ship-001/004, TR-colony-001 等 | 无 | ✅ |

### 可追溯性覆盖

| 状态 | 数量 | 占比 |
|------|------|------|
| ✅ COVERED | 26 | 50% |
| ⚠️ PARTIAL | 5 | 10% |
| ❌ GAP | 21 | 40% |

21 个缺口须通过新增 ADR 覆盖（见下节）。

---

## Required ADRs

### P1 — Foundation 后、Core Epic 前（必须）

| ADR | 覆盖系统 | TR-ID |
|-----|---------|-------|
| **ADR-0005（已拆分→0013/0014/0015）：Combat Architecture** | 星图图结构、ShipBlueprint/carrier_v1、BuildingInstance/ShipyardTier、ResourceConfig ORE_CAP 校验 | TR-resource-001~003, TR-starmap-001~003, TR-ship-001/005, TR-building-001~002 |
| **ADR-0007：叠加渲染架构** | 星图叠加层 ScreenOverlay 方案（不依赖 Camera A），COCKPIT_WITH_OVERLAY 状态渲染模式 | TR-dvs-008 |
| **ADR-0012：SimClock 架构** | 策略层独立时间系统（Time.unscaledDeltaTime × SimRate），禁止 timeScale 控制策略层 | TR-dvs-009 |

### P2 — Feature Epic 前（必须）

| ADR | 覆盖系统 | TR-ID |
|-----|---------|-------|
| **ADR-0006：战斗 & 敌人架构** | 自动开火逻辑、无人值守即时结算、Raycast 命中、敌人独立 HP、AI 状态机、生成逻辑、OnEnemyDied 事件链、ApplyDamage 完整接口 | TR-combat-001/004/005, TR-enemy-001~004, TR-health-001 |
| **ADR-0007：殖民地计时 & 离线补偿架构** | 生产 tick 改为时间戳方案（替代 UniTask.WaitForSeconds）、Android 后台节流处理、离线补偿公式 | TR-colony-002, TR-fleet-002 |
| **ADR-0008：舰队调度 & BFS 寻路** | BFS 算法实现、路径缓存策略、FLEET_TRAVEL_TIME 计时机制、取消对称返回、D-1 前置条件校验 | TR-fleet-001~004 |

### P3 — Presentation Epic 前（必须）

| ADR | 覆盖系统 | TR-ID |
|-----|---------|-------|
| **ADR-0009：UI 架构（UI Toolkit）** | 节点色码渲染、触摸热区 ≥ 48dp、弧形血条、软锁定瞄准具、视角切换按钮 | TR-starmap-ui-001~002, TR-hud-002~004 |

### 可推迟至实现阶段（建议）

- 存档/读档序列化格式 ADR（Vertical Slice 前）
- 程序星图生成算法选型 ADR（Vertical Slice 前）

---

## Architecture Principles

1. **层级单向依赖**：上层可依赖下层，下层绝不引用上层。Presentation 不持有 Feature 对象引用，仅通过 SO Channel 消费数据。

2. **MasterScene 是唯一权威**：所有跨场景共享状态（ShipDataModel、ViewLayerManager、SO Channel 资产）只存在于 MasterScene。场景卸载不影响权威数据。

3. **SO Config 只读，C# 运行时状态可写**：游戏数值（公式参数、蓝图属性）存于 ScriptableObject 只读；运行时变化状态（血量、资源当前值）存于纯 C# 类，可序列化。两者严格分离。

4. **异步操作必须携带 CancellationToken**：永久 MasterScene 对象使用 `Application.exitCancellationToken`；场景生命周期组件使用 `destroyCancellationToken`。禁止无 Token 的裸 `await`。

5. **游戏逻辑计时禁用 Unity PlayerLoop**：所有影响游戏状态的计时（资源 tick、舰队到达）必须基于系统时间戳 + 离线补偿，不依赖 `Update()`、`WaitForSeconds` 或 `UniTask.Delay`，以兼容 Android 后台节流。

---

## Open Questions

| # | 问题 | 影响范围 | 须在何时解决 |
|---|------|---------|------------|
| OQ-01 | 殖民地计时最终方案：纯时间戳补偿 vs. 前台实时 tick + 后台补偿混合？ | ADR-0007，ColonySystem 实现 | Core Epic 开始前 |
| OQ-02 | 双视角切换期间策略层是否暂停 `Time.timeScale`？（当前方案：不暂停，Strategy 层实时运行） | 双视角切换系统 UX | Presentation Epic 开始前 |
| OQ-03 | UI Toolkit 弧形血条：Mesh 自定义 vs. 遮罩旋转方案？性能对比待测 | ADR-0009，飞船 HUD 实现 | Presentation Epic 开始前 |
| OQ-04 | 低端 Android（2GB RAM）下 StarMapScene 常驻 + CockpitScene 预加载内存预算是否满足？ | ADR-0001 Verification，性能预算 | 原型阶段验证 |
