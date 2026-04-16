# ADR-0002: Event/Communication Architecture

## Status
Accepted

## Date
2026-04-14

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Core / Event Architecture |
| **Knowledge Risk** | LOW — ScriptableObject Channel 模式和 C# event Action<T> 均自 Unity 2019 起未变；UniTask destroyCancellationToken 自 Unity 2022.2 起可用（6.3 LTS 有效） |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `docs/engine-reference/unity/breaking-changes.md`, `docs/engine-reference/unity/deprecated-apis.md`, `docs/engine-reference/unity/current-best-practices.md` |
| **Post-Cutoff APIs Used** | `this.destroyCancellationToken`（UniTask，Unity 2022.2+ 可用，6.3 LTS 确认有效）；ScriptableObject Channel 模式无 post-cutoff 变更 |
| **Verification Required** | (1) SO Channel 在场景卸载后不产生 null ref（StarMapScene 存活时 CockpitScene 卸载）；(2) OnEnable/OnDisable 订阅在 CockpitScene 热重载时正确重新订阅；(3) `destroyCancellationToken` 在目标 Android 设备上正确取消挂起的 UniTask；(4) CombatChannel.RaiseBegin() 在 sceneLoaded 回调后调用，无竞争条件 |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0001（场景管理架构）— 本 ADR 的 Tier 1 / Tier 2 分类规则依赖 ADR-0001 确认的场景拓扑（MasterScene / StarMapScene / CockpitScene） |
| **Enables** | ADR-0003（输入系统架构）— 输入上下文切换需订阅 ViewLayerChannel.OnViewLayerChanged（Tier 1 SO Channel），依赖本 ADR 确认的订阅规范 |
| **Blocks** | Foundation Epic — 跨系统通信规范是 Foundation 层第二个必须决策的模式，所有跨系统事件的实现依赖本决策 |
| **Ordering Note** | 必须在 ADR-0001 Accepted 后 Proposed；必须在任何跨系统事件实现前 Accepted |

## Context

### Problem Statement
游戏拥有 MasterScene（常驻）、StarMapScene（常驻）、CockpitScene（按需）三个场景，多个系统需要在场景边界内外通信。若统一使用直接引用，Unity 场景生命周期将导致 null ref；若统一使用事件总线，则简单的内类调用也引入不必要的间接层。需要一套分层通信规则，按照通信双方的场景归属自动选择最小成本的方式。

### Constraints
- **跨场景通信**：Unity 场景生命周期禁止跨场景直接引用（ADR-0001 forbidden_pattern: cross_scene_direct_reference）
- **内存效率**：SO Channel 实例数量应最小化，每个跨场景事件主题对应一个 Channel SO 资产
- **Android 性能**：避免 GC 压力——高频事件参数优先使用 struct / 基础类型
- **生命周期安全**：订阅/取消订阅必须与 Unity MonoBehaviour 生命周期对齐，防止 MissingReferenceException
- **异步安全**：跨 await 边界的方法必须处理 GameObject 销毁时的取消

### Requirements
- 必须支持跨场景事件（MasterScene ↔ StarMapScene ↔ CockpitScene）
- 同场景跨系统事件不得引入 SO Asset 依赖（保持模块可测试性）
- 所有事件订阅必须防止"订阅泄漏"（MonoBehaviour 销毁后残留订阅）
- 通信模式必须可从 GDD 事件签名直接推导（无需逐案决策）

## Decision

采用 **三层分级通信规则**，按照生产者与消费者的场景归属机械地分类：

### 分类规则（三问法）

```
Q1: 生产者和消费者是否在不同 Unity 场景？
    YES → Tier 1: ScriptableObject Channel
    NO  → Q2

Q2: 两者是否属于不同系统（不同 MonoBehaviour 类）？
    YES → Tier 2: C# event Action<T>
    NO  → Tier 3: 直接方法调用
```

### Tier 1 — ScriptableObject Channel（跨场景）

**适用场景**：生产者和消费者位于不同 Unity 场景，或消费者在场景加载/卸载时动态变化。

**规则**：
- 每个跨场景事件主题创建一个 `ScriptableObject` 子类，资产存放于 `assets/data/channels/`
- 文件命名：`[ProducerSystem]Channel.cs`，如 `ViewLayerChannel`、`CombatChannel`
- SO Channel 实例在 MasterScene 中持有，通过 `[SerializeField]` 注入到各场景订阅者
- 订阅者在 `OnEnable()` 订阅，在 `OnDisable()` 取消订阅（**强制**，详见实现约束）

**本项目 Tier 1 SO Channels 清单**：

| Channel | 事件 | 生产者 | 消费者 | 原因 |
|---------|------|--------|--------|------|
| ViewLayerChannel | OnViewLayerChanged(ViewLayer) | ViewLayerManager (Master) | StarMapUI (StarMap), ShipHUD (Cockpit), ShipControlSystem (Cockpit) | 跨 Master → StarMap/Cockpit |
| CombatChannel | OnCombatBegin() | CombatSystem (Cockpit) | StarMapSystem (StarMap), ShipHUD (Cockpit) | Cockpit → StarMap 跨场景 |
| CombatChannel | OnCombatVictory(string nodeId) | CombatSystem (Cockpit) | StarMapSystem (StarMap) | Cockpit → StarMap 跨场景 |
| CombatChannel | OnCombatDefeat(string nodeId) | CombatSystem (Cockpit) | StarMapSystem (StarMap) | Cockpit → StarMap 跨场景 |
| ColonyShipChannel | OnShipBuilt(string nodeId, string shipInstanceId) | ColonySystem (StarMap) | ShipSystem (Master) | StarMap → Master 跨场景 |
| ShipStateChannel | OnShipStateChanged(string instanceId, ShipState) | ShipDataModel (Master) | StarMapUI (StarMap), ShipHUD (Cockpit) | Master → StarMap/Cockpit 跨场景 |

### Tier 2 — C# event Action\<T\>（同场景跨系统）

**适用场景**：生产者和消费者在同一 Unity 场景，但属于不同 MonoBehaviour 系统。

**规则**：
- 在生产者 MonoBehaviour 上直接声明 `public event Action<T> OnXxx`
- 消费者通过依赖注入（Inspector 或 Awake 查找）获取生产者引用后订阅
- 同样必须在 `OnEnable()` 订阅，在 `OnDisable()` 取消订阅（**强制**）

**本项目 Tier 2 事件清单**：

| 系统 | 事件 | 消费者 | 场景 |
|------|------|--------|------|
| BuildingSystem | OnBuildingConstructed(string nodeId, BuildingType) | ColonySystem, StarMapUI | StarMapScene |
| ColonySystem | OnResourcesUpdated(ResourceSnapshot) | StarMapUI | StarMapScene |
| ColonySystem | OnProductionRateChanged(float netOre, float netEnergy) | StarMapUI | StarMapScene |
| ColonySystem | OnEnergyDeficit(float deficitRate) | StarMapUI | StarMapScene |
| ColonySystem | OnEnergyDeficitCleared() | StarMapUI | StarMapScene |
| StarMapSystem | OnNodeOwnershipChanged(string nodeId, NodeOwnership) | StarMapUI | StarMapScene |
| StarMapSystem | OnFleetArrived(string fleetId, string nodeId) | StarMapUI | StarMapScene |
| HealthSystem | OnShipDying(string instanceId) | CombatSystem | CockpitScene |

### Tier 3 — 直接方法调用（类内或紧耦合对）

**适用场景**：同一 MonoBehaviour 内部调用，或同一场景内已有直接引用的紧耦合系统对。

**规则**：无额外约束，标准 C# 方法调用。

---

### 强制实现约束

#### ADV-01 / ADV-02：OnEnable/OnDisable 订阅配对（MANDATORY）

所有事件订阅（Tier 1 SO Channel 和 Tier 2 C# event）**必须**在 `OnEnable()` 订阅、`OnDisable()` 取消订阅。禁止在 `Awake()` / `Start()` 订阅而仅在 `OnDestroy()` 取消——CockpitScene 热重载时 `OnEnable/OnDisable` 会触发但 `Awake/OnDestroy` 不会，导致重复订阅或泄漏。

```csharp
// ✅ CORRECT
public class StarMapUI : MonoBehaviour
{
    [SerializeField] private ViewLayerChannel _viewLayerChannel;

    private void OnEnable()
    {
        _viewLayerChannel.OnViewLayerChanged += OnViewLayerChanged;
    }

    private void OnDisable()
    {
        _viewLayerChannel.OnViewLayerChanged -= OnViewLayerChanged;
    }

    private void OnViewLayerChanged(ViewLayer newLayer) { /* ... */ }
}

// ❌ FORBIDDEN — 场景重载时产生重复订阅
private void Awake()   => _channel.OnXxx += Handler;
private void OnDestroy() => _channel.OnXxx -= Handler;
```

#### ADV-03：UniTask destroyCancellationToken（MANDATORY）

所有跨越 `await` 边界的 `async UniTask` 方法**必须**传入 `this.destroyCancellationToken`，防止 GameObject 销毁后回调继续执行：

```csharp
// ✅ CORRECT
private async UniTask LoadAndNotifyAsync()
{
    await SceneManager.LoadSceneAsync("CockpitScene", LoadSceneMode.Additive)
        .ToUniTask(cancellationToken: this.destroyCancellationToken);
    _combatChannel.RaiseBegin();  // 仅在未销毁时执行
}

// ❌ FORBIDDEN — GameObject 销毁后仍可能执行回调
private async UniTask LoadAndNotifyAsync()
{
    await SceneManager.LoadSceneAsync("CockpitScene", LoadSceneMode.Additive);
    _combatChannel.RaiseBegin();
}
```

#### ADV-05：CombatChannel.RaiseBegin() 时机（MANDATORY）

`CombatChannel.RaiseBegin()` 必须在 CockpitScene 完全加载后（`SceneManager.sceneLoaded` 回调触发后）调用，防止 ShipHUD 等消费者尚未完成 `OnEnable` 订阅时错过事件：

```csharp
// ✅ CORRECT — 在 sceneLoaded 回调中调用
SceneManager.sceneLoaded += OnCockpitSceneLoaded;
// ...
private void OnCockpitSceneLoaded(Scene scene, LoadSceneMode mode)
{
    if (scene.name == "CockpitScene")
    {
        SceneManager.sceneLoaded -= OnCockpitSceneLoaded;
        _combatChannel.RaiseBegin();
    }
}
```

---

### Architecture Diagram

```
MasterScene（常驻）
├── ViewLayerChannel SO ──────────────────────────────────────────────────┐
├── CombatChannel SO  ←──────────────────────────────────────────────────┐│
├── ColonyShipChannel SO ←──────────────────────────────────────┐        ││
├── ShipStateChannel SO ──────────────────────────────────────── ┼───────┼┤
│                                                                 │       ││
│  ViewLayerManager ──→ ViewLayerChannel.Raise()                  │       ││
│  ShipDataModel ──────→ ShipStateChannel.Raise()                 │       ││
│                                                                 │       ││
StarMapScene（常驻）                                               │       ││
├── StarMapUI [Tier 1] ←── ViewLayerChannel.OnViewLayerChanged    │       ││
│            [Tier 1] ←── ShipStateChannel.OnShipStateChanged     │       ││
│            [Tier 1] ←── CombatChannel.OnCombatVictory/Defeat    │       ││
├── ColonySystem [Tier 2] ──→ OnShipBuilt ─────────────────────── ┘       ││
│              [Tier 2] ──→ OnResourcesUpdated → StarMapUI (Tier 2)       ││
├── BuildingSystem [Tier 2] ──→ OnBuildingConstructed → ColonySystem      ││
├── StarMapSystem [Tier 2] ──→ OnFleetArrived/OnNodeOwnershipChanged      ││
│                                                                         ││
CockpitScene（按需）                                                       ││
├── ShipHUD [Tier 1] ←── ViewLayerChannel.OnViewLayerChanged              ││
│          [Tier 1] ←── ShipStateChannel.OnShipStateChanged               ││
│          [Tier 1] ←── CombatChannel.OnCombatBegin                       ││
├── CombatSystem ──→ CombatChannel.RaiseBegin/Victory/Defeat ─────────── ──┘
│             [Tier 2] ←── HealthSystem.OnShipDying
├── HealthSystem [Tier 2] ──→ OnShipDying → CombatSystem
```

### Key Interfaces

```csharp
// ─── Tier 1: ScriptableObject Channels ───────────────────────────────────────
// 资产路径：assets/data/channels/[ChannelName].asset
// 命名规则：[ProducerSystem]Channel.cs

[CreateAssetMenu(menuName = "Channels/ViewLayerChannel")]
public class ViewLayerChannel : ScriptableObject
{
    public event Action<ViewLayer> OnViewLayerChanged;
    public void Raise(ViewLayer newLayer) => OnViewLayerChanged?.Invoke(newLayer);
}

[CreateAssetMenu(menuName = "Channels/CombatChannel")]
public class CombatChannel : ScriptableObject
{
    public event Action OnCombatBegin;
    public event Action<string> OnCombatVictory;   // nodeId
    public event Action<string> OnCombatDefeat;    // nodeId

    public void RaiseBegin()                  => OnCombatBegin?.Invoke();
    public void RaiseVictory(string nodeId)   => OnCombatVictory?.Invoke(nodeId);
    public void RaiseDefeat(string nodeId)    => OnCombatDefeat?.Invoke(nodeId);
}

[CreateAssetMenu(menuName = "Channels/ColonyShipChannel")]
public class ColonyShipChannel : ScriptableObject
{
    public event Action<string, string> OnShipBuilt;  // nodeId, shipInstanceId
    public void Raise(string nodeId, string shipId)   => OnShipBuilt?.Invoke(nodeId, shipId);
}

[CreateAssetMenu(menuName = "Channels/ShipStateChannel")]
public class ShipStateChannel : ScriptableObject
{
    public event Action<string, ShipState> OnShipStateChanged;  // instanceId, newState
    public void Raise(string id, ShipState s) => OnShipStateChanged?.Invoke(id, s);
}

// ─── Tier 2: Same-Scene C# Events ────────────────────────────────────────────

public class BuildingSystem : MonoBehaviour
{
    public event Action<string, BuildingType> OnBuildingConstructed;  // nodeId, type
}

public class ColonySystem : MonoBehaviour
{
    public event Action<ResourceSnapshot> OnResourcesUpdated;
    public event Action<float, float>     OnProductionRateChanged;   // netOre, netEnergy
    public event Action<float>            OnEnergyDeficit;           // deficitRate
    public event Action                   OnEnergyDeficitCleared;
}

public class StarMapSystem : MonoBehaviour
{
    public event Action<string, NodeOwnership> OnNodeOwnershipChanged;  // nodeId, ownership
    public event Action<string, string>        OnFleetArrived;          // fleetId, nodeId
}

public class HealthSystem : MonoBehaviour
{
    public event Action<string> OnShipDying;  // instanceId
}

// ─── Value Object: ResourceSnapshot ─────────────────────────────────────────
// 替代 4-参数 Action<int,int,float,float>，零分配，可读性高

public readonly struct ResourceSnapshot
{
    public readonly int   OreAmount;
    public readonly int   EnergyAmount;
    public readonly float OreDelta;
    public readonly float EnergyDelta;

    public ResourceSnapshot(int ore, int energy, float oreDelta, float energyDelta)
    {
        OreAmount    = ore;
        EnergyAmount = energy;
        OreDelta     = oreDelta;
        EnergyDelta  = energyDelta;
    }
}

// ─── 强制订阅模式（所有 Tier 1 和 Tier 2 订阅者必须遵循）─────────────────────

public class ExampleSubscriber : MonoBehaviour
{
    [SerializeField] private ViewLayerChannel _viewLayerChannel;  // Tier 1: Inspector 注入
    [SerializeField] private ColonySystem     _colonySystem;      // Tier 2: Inspector 注入

    private void OnEnable()
    {
        _viewLayerChannel.OnViewLayerChanged += OnViewLayerChanged;
        _colonySystem.OnResourcesUpdated      += OnResourcesUpdated;
    }

    private void OnDisable()
    {
        _viewLayerChannel.OnViewLayerChanged -= OnViewLayerChanged;
        _colonySystem.OnResourcesUpdated      -= OnResourcesUpdated;
    }

    private void OnViewLayerChanged(ViewLayer layer) { /* ... */ }
    private void OnResourcesUpdated(ResourceSnapshot snapshot) { /* ... */ }
}
```

## Alternatives Considered

### Alternative A: Unity Events (UnityEvent\<T\>)
- **Description**: 使用 `UnityEvent<T>` 替代 C# `event Action<T>`，可在 Inspector 中配置
- **Pros**: Inspector 可视化配置；无需代码级订阅
- **Cons**: 运行时性能低于 C# event（反射调用）；序列化的 `PersistentCall` 在场景重载时容易产生悬空引用；GC 压力高于 `event Action`
- **Rejection Reason**: 移动端帧预算（16.6ms）不接受 UnityEvent 的反射开销；悬空引用问题与 ADR-0001 的 cross_scene_direct_reference 禁令冲突

### Alternative B: 统一 MessageBus 单例
- **Description**: 全局静态或 Singleton EventBus，所有事件通过字符串 key 订阅/发布
- **Pros**: 完全解耦；无需持有生产者引用
- **Cons**: 字符串 key 无类型安全；Singleton 耦合违反 ADR-0001 风险 R-3（Singleton 蔓延）；调试困难——无法追溯谁发布了某个事件
- **Rejection Reason**: 字符串 key 运行时错误难以追踪；与 ADR-0001 的 Singleton 蔓延风险直接冲突

### Alternative C: Observable/Reactive（UniRx / R3）
- **Description**: 使用响应式扩展（UniRx 或 R3）替代 C# event，提供 LINQ 操作符
- **Pros**: 强大的事件流变换；自动生命周期管理（CompositeDisposable）
- **Cons**: 需要引入额外依赖包；学习曲线陡峭；过度设计——本项目事件模型不需要流变换
- **Rejection Reason**: 在无额外依赖的情况下，`event Action<T>` + OnEnable/OnDisable 完全满足需求；R3 留待复杂性确实出现时评估

## Consequences

### Positive
- 三问法使通信层选择完全机械化，无需逐案讨论
- SO Channel 免疫 Unity 场景生命周期 null ref（ADR-0001 的核心要求）
- Tier 2 C# event 保持系统可单元测试（无 SO Asset 依赖）
- OnEnable/OnDisable 强制约束消除"订阅泄漏"整类 bug
- `ResourceSnapshot` readonly struct 在高频资源更新时零 GC 分配

### Negative
- 每个跨场景事件主题需要创建独立 SO 资产（6 个 Channel），初期资产管理成本略高
- SO Channel 需要通过 Inspector `[SerializeField]` 注入，场景搭建时需手动连线
- `OnEnable/OnDisable` 强制要求意味着禁止使用 `Awake/OnDestroy` 订阅——需要代码审查执行

### Risks
- **风险 R-1：SO Channel Inspector 连线遗漏**
  缓解：在 `OnEnable()` 中添加 `Debug.Assert(_channel != null, "[ClassName] Channel not wired!")` 进行开发期检查
- **风险 R-2：CombatChannel.RaiseBegin() 在 ShipHUD 订阅前触发**
  缓解：ADV-05 约束——必须在 `SceneManager.sceneLoaded` 回调中调用（详见强制实现约束）
- **风险 R-3：Tier 误分类（开发者将同场景事件误用 SO Channel）**
  缓解：三问法规则简单明确；代码审查时检查 SO Channel 使用是否跨越场景边界
- **风险 R-4：UniTask 未配置导致 destroyCancellationToken 不可用**
  缓解：ADR-0003（输入系统）或 Foundation Epic 实现前，需确认 UniTask 包已加入 Unity Package Manager

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| dual-perspective-switching.md | ScriptableObject Channel 作为跨场景事件总线 | Tier 1 规则正式化 SO Channel 为唯一跨场景通信机制；ViewLayerChannel、ShipStateChannel 已定义 |
| dual-perspective-switching.md | _isSwitching 防并发标志需原子化管理 | ViewLayerChannel 单向广播确保 ViewLayerManager 是唯一写入者，无并发冲突 |
| ship-system.md | OnShipStateChanged 通知星图 UI 和驾驶舱 HUD | ShipStateChannel（Tier 1）定义跨 MasterScene → StarMapScene/CockpitScene 广播 |
| ship-combat-system.md | OnCombatBegin/Victory/Defeat 通知星图系统更新节点归属 | CombatChannel（Tier 1）从 CockpitScene 广播到 StarMapScene；RaiseBegin() 时机约束防止竞争 |
| colony-system.md | OnShipBuilt 触发舰队系统注册新飞船 | ColonyShipChannel（Tier 1）从 StarMapScene → MasterScene 跨场景通知 |
| colony-system.md | OnResourcesUpdated 高频更新（每资源 tick）| ResourceSnapshot readonly struct 保证零 GC 分配 |
| building-system.md | OnBuildingConstructed 触发殖民地产出更新 | Tier 2 C# event（同场景，BuildingSystem → ColonySystem）|
| star-map-system.md | OnFleetArrived / OnNodeOwnershipChanged 更新星图 UI | Tier 2 C# event（同场景，StarMapSystem → StarMapUI）|
| fleet-dispatch-system.md | OnFleetArrived 触发节点归属变更 | Tier 2 C# event（同场景，StarMapSystem → StarMapUI）|

## Performance Implications
- **CPU**: SO Channel 调用路径为 `Raise()` → `event?.Invoke()`，与直接 C# event 调用等价，无反射开销；Tier 2 C# event 同样零反射
- **Memory**: SO Channel 为 ScriptableObject，运行时常驻内存约 ~200B/Channel × 6 = ~1.2KB；可忽略不计
- **GC**: `event Action<T>` 委托调用零 GC 分配；`ResourceSnapshot` readonly struct 参数传递零装箱
- **Load Time**: SO Channel 资产随 MasterScene 加载，预估 <1ms 额外加载时间

## Migration Plan
首次实现（无现有代码需迁移）：
1. 在 `assets/data/channels/` 创建 4 个 SO Channel ScriptableObject 资产（ViewLayerChannel.asset, CombatChannel.asset, ColonyShipChannel.asset, ShipStateChannel.asset）
2. 将资产引用挂载到 MasterScene 中的 ViewLayerManager / ShipDataModel GameObject 上
3. 各订阅者通过 Inspector `[SerializeField]` 接收对应 Channel 引用
4. 实现 Tier 2 C# event 声明（在各系统 MonoBehaviour 上）
5. 所有订阅统一使用 `OnEnable/OnDisable` 模式（CI code review checklist 添加检查项）

## Validation Criteria
- **AC-EVT-01**：进入驾驶舱后，StarMapUI 收到 `OnViewLayerChanged(COCKPIT)` 并正确隐藏（ViewLayerChannel Tier 1 正常广播）
- **AC-EVT-02**：战斗胜利后，StarMapSystem 收到 `OnCombatVictory(nodeId)` 并更新节点归属（CombatChannel Tier 1 跨场景正常广播）
- **AC-EVT-03**：CockpitScene 卸载后重新加载，ShipHUD 能正确订阅 ViewLayerChannel（OnEnable 重新订阅有效，无重复/泄漏）
- **AC-EVT-04**：殖民地建造飞船后，ShipSystem（MasterScene）收到 `OnShipBuilt`（ColonyShipChannel Tier 1 StarMap → Master 跨场景正常）
- **AC-EVT-05**：飞船受伤后 `OnResourcesUpdated(ResourceSnapshot)` 每 tick 触发，Unity Profiler 显示无 GC Alloc（readonly struct 零分配）

## Related Decisions
- ADR-0001（场景管理架构）— 场景拓扑是本 ADR Tier 1/2 分类规则的基础
- ADR-0003（输入系统架构）— 输入上下文切换订阅 ViewLayerChannel（Tier 1）
- design/gdd/dual-perspective-switching.md — 跨场景通信主要 GDD 来源
- design/gdd/ship-system.md — ShipDataModel.OnShipStateChanged 定义
- design/gdd/ship-combat-system.md — CombatChannel 事件源
- design/gdd/colony-system.md — ColonyShipChannel 和 ResourceSnapshot 来源
