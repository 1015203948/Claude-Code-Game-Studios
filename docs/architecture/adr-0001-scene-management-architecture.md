# ADR-0001: Scene Management Architecture

## Status
Accepted

## Date
2026-04-14

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Core / Scene Management |
| **Knowledge Risk** | MEDIUM — Unity 6.0+ has URP Renderer Feature API changes; SceneManager Additive path is unchanged |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `docs/engine-reference/unity/breaking-changes.md`, `docs/engine-reference/unity/deprecated-apis.md`, `docs/engine-reference/unity/current-best-practices.md` |
| **Post-Cutoff APIs Used** | None — `SceneManager.LoadSceneAsync(LoadSceneMode.Additive)` is unchanged from Unity 2022 LTS to 6.3 LTS. ScriptableObject Channel 模式仍为推荐方式，Unity 6 无内置替代 |
| **Verification Required** | (1) CockpitScene Additive 加载在目标低端 Android 设备上 ≤ 1.0s；(2) StarMapScene Update() 在 CockpitScene 激活时持续运转（验证不被暂停）；(3) Camera.enabled 切换后 URP 无 Culling Pass 报错；(4) 低端 Android（2GB RAM）下 StarMapScene 常驻 + CockpitScene 预加载同时存在时内存不超预算；(5) `UnloadSceneAsync` 的 AsyncOperation await 完成后再清理引用，避免 GC 时机不确定 |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | None |
| **Enables** | ADR-0002（事件/通信架构）— 跨场景通信方案依赖本 ADR 确认的场景拓扑；ADR-0003（输入系统架构）— 输入上下文切换依赖 ViewLayer 状态定义 |
| **Blocks** | Foundation Epic — 场景管理是 Foundation 层第一个需要实现的系统，所有其他系统的 Unity 场景分配依赖本决策 |
| **Ordering Note** | 必须在 ADR-0002 和 ADR-0003 之前 Accepted，否则两者无法确定跨场景通信拓扑 |

## Context

### Problem Statement
游戏需要两个独立的游戏空间——星图策略层和驾驶舱飞行层——同时运行，并在不超过 1 秒的过渡时间内切换，同时保持飞船状态（血量、位置）的跨层一致性。

### Constraints
- **性能**：Android 移动端，60fps，16.6ms 帧预算，<200 draw calls
- **切换时间**：≤ 1.0 秒（SWITCH_TIME_LIMIT，来自 dual-perspective-switching.md）
- **策略层不暂停**：进入驾驶舱后，星图的舰队运动、资源生产 tick 必须继续运行
- **数据一致性**：飞船血量、ShipState 在两层间必须共享单一权威数据源
- **平台**：Android 触屏优先，需兼容多种屏幕比例（16:9 和 4:3）

### Requirements
- 必须支持星图层（2D 策略 UI）与驾驶舱层（3D 飞行）的无缝切换
- 切换过程中星图层逻辑（资源 tick、舰队调度）不得暂停
- 飞船数据（CurrentHull、ShipState）必须有唯一的 MasterScene 权威来源
- 驾驶舱层必须按需加载/卸载，不常驻内存（节省 Android 内存）
- 跨场景通信不得使用跨场景 GameObject 引用（Unity 场景生命周期限制）

## Decision

采用 **Additive Multi-Scene 三层拓扑**：

```
MasterScene（常驻，永不卸载）
├── 持有 ShipDataModel（飞船权威数据：CurrentHull, ShipState, position）
├── 持有 ViewLayerManager（全局 ViewLayer 状态机）
├── 持有 ScriptableObject Channel 实例（跨场景事件总线）
└── 持有 ColonyDataModel（资源产出状态）

StarMapScene（随 MasterScene 同时加载，永不卸载）
├── 星图 2D 摄像机（Camera A）
├── 星图 UI（UI Toolkit / Canvas）
├── 星图节点图、舰队图标
└── Update() 持续运行：资源 tick、舰队移动

CockpitScene（按需 Additive 加载，不用时 Unload）
├── 3D 飞行摄像机（Camera B）
├── 飞船物理对象（读取 ShipDataModel 初始化血量）
├── 飞船 HUD（驾驶舱层 UI）
└── 卸载前将 CurrentHull 回写至 ShipDataModel
```

### 切换序列（星图 → 驾驶舱）

```
1. ViewLayerManager._isSwitching = true
2. LoadSceneAsync(CockpitScene, LoadSceneMode.Additive) —— allowSceneActivation = false
   └── 预加载触发时机：ON_SHIP_SELECT（玩家点选飞船时，提前预加载至 90%）
3. CockpitScene 读取 MasterScene.ShipDataModel 初始化飞船状态
4. Camera A（星图）.enabled = false    ← 正确：避免 URP 执行无效 Culling Pass
   Camera B（驾驶舱）.enabled = true
5. StarMapScene UI（Canvas/UIDocument）隐藏：Canvas.enabled = false / DisplayStyle.None
6. allowSceneActivation = true —— CockpitScene 激活
7. ShipState → IN_COCKPIT（由飞船系统写入）
8. ViewLayer → COCKPIT（ViewLayerManager 发出 OnViewLayerChanged 事件）
9. ViewLayerManager._isSwitching = false
```

### 切换序列（驾驶舱 → 星图）

```
1. ViewLayerManager._isSwitching = true
2. 将 CockpitScene.CurrentHull 回写至 MasterScene.ShipDataModel（D-DVS-3 校验）
3. ShipState → 恢复 _preEnterState（由飞船系统写入；_preEnterState 在进入驾驶舱时快照）
4. Camera B（驾驶舱）.enabled = false
   Camera A（星图）.enabled = true
5. CockpitScene UI 隐藏（DisplayStyle.None）
6. StarMapScene UI 恢复：Canvas.enabled = true / DisplayStyle.Flex
7. ViewLayer → STARMAP（ViewLayerManager 发出 OnViewLayerChanged 事件）
8. UnloadSceneAsync(CockpitScene)
9. ViewLayerManager._isSwitching = false
```

### Architecture Diagram

```
┌─────────────────────────────────────────────────────────┐
│  MasterScene（常驻）                                       │
│  ┌──────────────────┐  ┌─────────────────────────────┐  │
│  │ ViewLayerManager │  │ ShipDataModel               │  │
│  │ ViewLayer: enum  │  │ CurrentHull: float          │  │
│  │ _isSwitching:bool│  │ ShipState: enum             │  │
│  │ _preEnterState   │  │ LastSyncTime: float         │  │
│  └────────┬─────────┘  └──────────────┬──────────────┘  │
│           │ OnViewLayerChanged         │ read/write       │
│           │ (ScriptableObject Channel) │                  │
└───────────┼────────────────────────────┼─────────────────┘
            │                            │
      ┌─────▼──────────────┐   ┌────────▼──────────────────┐
      │  StarMapScene      │   │  CockpitScene（按需）       │
      │  Camera A          │   │  Camera B                  │
      │  2D UI (Canvas)    │   │  3D Ship GameObject        │
      │  Fleet / Nodes     │   │  Ship HUD (UI Toolkit)     │
      │  Update() 常驻     │   │  读取 ShipDataModel 初始化  │
      │                    │   │  退出时回写 CurrentHull    │
      └────────────────────┘   └────────────────────────────┘
```

### Key Interfaces

```csharp
// ViewLayerManager — 场景管理权威接口（挂载于 MasterScene）
public class ViewLayerManager : MonoBehaviour
{
    public static ViewLayerManager Instance { get; }

    // 当前视角层（只读，外部系统监听事件获取变化）
    public ViewLayer CurrentViewLayer { get; private set; }

    // 进入驾驶舱请求（星图 UI 按钮调用）
    public void RequestEnterCockpit(string shipId);

    // 返回星图请求（飞船 HUD 按钮调用）
    public void RequestReturnToStarMap();
}

// ScriptableObject Channel（跨场景事件总线，MasterScene 实例化）
[CreateAssetMenu]
public class ViewLayerChannel : ScriptableObject
{
    public event Action<ViewLayer> OnViewLayerChanged;
    public void Raise(ViewLayer newLayer);
}

// ShipDataModel（MasterScene 单例数据容器）
public class ShipDataModel : MonoBehaviour
{
    public float CurrentHull { get; set; }
    public ShipState ShipState { get; set; }
    public ShipState PreEnterState { get; set; }  // _preEnterState 快照
    public float LastSyncTime { get; private set; }

    public void SyncFromCockpit(float hull);  // 回写 + 更新 LastSyncTime
}

// ViewLayer 枚举
public enum ViewLayer { STARMAP, COCKPIT }
```

**Camera 切换规则**（来自 GDD，基于 URP 要求）：
- ✅ 使用 `Camera.enabled = false/true`
- ❌ 禁止 `camera.gameObject.SetActive(false)` — 会导致 URP 执行无效 Culling Pass

**UI 隐藏规则**：
- UI Toolkit：`element.style.display = DisplayStyle.None / Flex`（非 OnEnable/Disable）
- UGUI Canvas：`canvas.enabled = false / true`

## Alternatives Considered

### Alternative A: 单场景 + 动态 Prefab 激活/停用
- **Description**: 所有内容在同一 Unity 场景，驾驶舱系统通过 Prefab 实例激活/停用切换
- **Pros**: 无场景加载开销；对象间引用直接
- **Cons**: 星图节点数量扩展时内存常驻；两层渲染对象同时存在导致 draw call 超出移动端预算；无法隔离 3D 飞行物理与 2D 星图 UI 的 Camera Culling Mask 冲突
- **Rejection Reason**: 内存和 draw call 预算不可控；物理层与 UI 层在同一场景造成摄像机管理复杂性不可接受

### Alternative B: 独立场景切换（LoadScene 替代 Additive）
- **Description**: 切换时完全卸载当前场景，加载目标场景（非 Additive）
- **Pros**: 内存最省；实现最简单
- **Cons**: 星图层资源 tick 和舰队运动在切换期间完全停止，违反"策略层不暂停"核心需求；每次切换需重新初始化全部系统状态；切换时间因全场景加载大幅增加（>1s）
- **Rejection Reason**: 违反核心设计约束（策略层持续运行）

## Consequences

### Positive
- 星图层（资源 tick、舰队移动）在驾驶舱激活期间持续运转，满足"不暂停"需求
- ShipDataModel 作为单一权威来源，消除两层间数据同步的竞态风险
- CockpitScene 按需加载/卸载，移动端内存可控
- ScriptableObject Channel 作为跨场景总线，避免了 Unity 跨场景直接引用的生命周期陷阱
- 预加载策略（ON_SHIP_SELECT）将切换体感降至 ≤0.3s（热路径）

### Negative
- 需要维护三个 Unity 场景文件，合并冲突管理复杂度提高
- ViewLayerManager 是 Singleton 风格组件（挂载于 MasterScene），需谨慎避免 Singleton 滥用蔓延
- CockpitScene 回写逻辑（步骤 2）是数据安全关键路径，需要专项测试

### Risks
- **风险 R-1：低端设备冷加载超过 1s**
  缓解：ON_SHIP_SELECT 预加载策略；目标设备最低配置在 Technical Setup 阶段确定后需实测
- **风险 R-2：_isSwitching 标志在异常中途失效（E-1 场景）**
  缓解：所有切换路径（包括异常中断）必须在 finally 块中清除 _isSwitching；进入驾驶舱时飞船被摧毁的中断路径需专项测试（AC-DVS 验收标准）
- **风险 R-3：MasterScene Singleton 蔓延**
  缓解：ADR-0002（事件/通信架构）将进一步约束跨系统通信模式，防止所有系统直接引用 ViewLayerManager
- **风险 R-4：低端 Android（2GB RAM）内存压力（Unity Specialist 建议）**
  缓解：StarMapScene 常驻 + CockpitScene 预加载同时存在时需建立内存预算；建议在 Technical Setup 阶段通过 Addressables 内存上限测试确认，并在 `production/stage.txt` 推进 Production 前完成
- **风险 R-5：UnloadSceneAsync GC 时机（Unity Specialist 建议）**
  缓解：退出序列步骤 8 中 `UnloadSceneAsync` 的 AsyncOperation 必须 await 完成后再清理引用，否则 GC 时机不确定；在集成测试中验证

## 扩展参考
本 ADR 定义了基础场景拓扑（MasterScene + StarMapScene + CockpitScene Additive）。以下新增系统扩展了本 ADR 的 Consequences：
- **ADR-0007（叠加渲染）**：星图叠加层使用 ScreenOverlay（不依赖 Camera A），扩展了 ADR-0001 的场景渲染架构，无需修改场景拓扑
- **ADR-0012（SimClock）**：策略层时间独立于驾驶舱物理，扩展了 ADR-0001 的时间管理约定；SimClock 挂载于 MasterScene（与 ViewLayerManager 同节点）

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| dual-perspective-switching.md | 三场景 Additive 架构（MasterScene + StarMapScene + CockpitScene） | 直接采用 GDD 规定的场景拓扑，不更改 |
| dual-perspective-switching.md | 切换时间 ≤ SWITCH_TIME_LIMIT（1.0s），含预加载策略 ON_SHIP_SELECT | 切换序列步骤 2 中 allowSceneActivation 延迟激活 + 预加载触发时机均已编码 |
| dual-perspective-switching.md | Camera.enabled 切换（非 SetActive，避免 URP Culling Pass 问题） | Key Interfaces 中明确规定，并标注 ❌ 禁止 SetActive |
| dual-perspective-switching.md | ShipDataModel 作为跨场景数据总线；ScriptableObject Channel 作为事件总线 | Key Interfaces 中定义 ShipDataModel 和 ViewLayerChannel 接口 |
| dual-perspective-switching.md | _preEnterState 快照（进入时保存，退出时恢复；IC-03 修复） | ShipDataModel.PreEnterState 字段；退出序列步骤 3 明确"恢复 _preEnterState" |
| dual-perspective-switching.md | _isSwitching 防并发标志（E-4 规则） | ViewLayerManager 接口包含 _isSwitching；切换序列起止均操作此标志 |
| star-map-system.md | 星图节点状态（归属、建筑列表）在驾驶舱期间持续更新 | StarMapScene 保持 Additive 常驻，Update() 不中断 |
| ship-system.md | ShipState 状态机（IN_COCKPIT / IN_TRANSIT / DOCKED / DESTROYED） | ShipDataModel.ShipState 是权威来源；切换序列中的状态写入步骤与 ship-system.md 状态机对齐 |
| colony-system.md | 资源产出 tick（固定 1 秒间隔）在驾驶舱期间不暂停 | StarMapScene 常驻，殖民地系统的 tick MonoBehaviour 在此场景中运行 |

## Performance Implications
- **CPU**: 场景加载时 LoadSceneAsync 异步运行，主线程开销约 0.5–2ms/帧（加载期间）；切换完成后 CockpitScene 增加约 3–5ms/帧（3D 物理 + 飞行逻辑）
- **Memory**: CockpitScene 按需加载（预估 50–120MB，含 3D 飞船资产）；卸载后内存释放；StarMapScene 常驻（预估 30–80MB，含 2D UI 和节点图）
- **Load Time**: 热路径（预加载）约 0.2–0.4s；冷路径约 0.6–1.0s；超过 1.0s 记录警告
- **Network**: 不适用（单机游戏）

## Migration Plan
首次实现（无现有代码需迁移）：
1. 创建 MasterScene（空场景，挂载 ViewLayerManager + ShipDataModel + Channel SO 实例）
2. 创建 StarMapScene（2D 星图内容）
3. 创建 CockpitScene（3D 飞行内容，初始为空壳）
4. 在 MasterScene 的 Awake 中 LoadSceneAsync(StarMapScene, Additive) 实现双场景启动

## Validation Criteria
- **AC-SCN-01**：进入驾驶舱，等待 2 个 colony tick（≤2.2s），确认玩家矿石增加（星图层持续运行）
- **AC-SCN-02**：冷加载驾驶舱（首次进入），计时 ≤1.0s（低端目标设备）
- **AC-SCN-03**：在驾驶舱中飞船受伤至 70 HP，返回星图，确认星图 UI 显示血量 70（ShipDataModel 回写正确）
- **AC-SCN-04**：切换动画进行中，触发飞船销毁（E-1），确认 ViewLayer 回落 STARMAP，_isSwitching 清除，无场景挂起
- **AC-SCN-05**：连续快速点击"进入驾驶舱"（E-4），确认只触发一次切换，mineCount / ShipState 无异常

## Related Decisions
- ADR-0002（事件/通信架构）— 定义 ScriptableObject Channel 的具体使用规范（依赖本 ADR 的场景拓扑）
- ADR-0003（输入系统架构）— ViewLayer 状态决定输入上下文激活哪个 InputActionMap（依赖本 ADR 的 ViewLayer 定义）
- design/gdd/dual-perspective-switching.md — 本 ADR 的主要 GDD 来源
- design/gdd/ship-system.md — ShipState 状态机定义
- design/gdd/colony-system.md — 资源 tick 不暂停需求
