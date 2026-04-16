# ADR-0003: Input System Architecture

## Status
Accepted

## Date
2026-04-14

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Core / Input |
| **Knowledge Risk** | HIGH — Legacy `Input.*` 类在 Unity 6.3 中已弃用；必须使用 `com.unity.inputsystem@1.11`（New Input System）。LLM 训练数据可能包含旧 API 用法 |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `docs/engine-reference/unity/modules/input.md`, `docs/engine-reference/unity/breaking-changes.md`, `docs/engine-reference/unity/deprecated-apis.md` |
| **Post-Cutoff APIs Used** | `EnhancedTouchSupport.Enable()` / `Disable()`（New Input System 多点触控，Unity 6 推荐）；`InputActionMap.Enable()` / `Disable()`（Action Map 上下文切换）；`Rigidbody.linearDamping`（Unity 6.x 重命名，原 `.drag`）|
| **Verification Required** | (1) Android 设备上 `EnhancedTouchSupport` 的 `activeTouches` 在 CockpitScene 卸载后不残留 touch 事件；(2) `InputActionMap.Enable()` / `Disable()` 在 ViewLayer 高速切换时无竞争条件；(3) 虚拟摇杆死区公式在各屏幕分辨率下触点偏移归一化正确；(4) `ShipInputChannel` SO 在 MasterScene 持有，CockpitScene 卸载后 StarMapScene 不收到残留事件 |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0001（场景管理架构）— ViewLayer 枚举和场景拓扑（MasterScene / StarMapScene / CockpitScene）是本 ADR 输入上下文切换规则的基础；ADR-0002（事件/通信架构）— 输入上下文切换须订阅 ViewLayerChannel.OnViewLayerChanged（Tier 1 SO Channel），新增 ShipInputChannel 须遵循 Tier 1 规范 |
| **Enables** | Foundation Epic — 输入系统是所有可交互 UI 和飞船操控的前提 |
| **Blocks** | Foundation Epic — 飞船操控实现（CockpitScene）依赖本 ADR 确认的 ActionMap 切换规范和虚拟摇杆接口 |
| **Ordering Note** | 必须在 ADR-0001 和 ADR-0002 Accepted 后 Proposed；必须在任何输入相关代码实现前 Accepted |

## Context

### Problem Statement
游戏有两个完全不同的交互模式——星图策略层（触屏 UI 点击/拖拽）和驾驶舱飞行层（双虚拟摇杆 + 武器开火按钮）。Unity 6.3 Legacy Input Manager 已弃用，必须迁移至 New Input System。输入上下文切换必须与 ViewLayer 状态机严格同步，且不得跨 Unity 场景边界持有直接对象引用。

### Constraints
- **Legacy Input 已弃用**：Unity 6.3 LTS 中 `Input.*` 静态类已弃用，必须使用 `com.unity.inputsystem@1.11`
- **触屏专属**：目标平台 Android，全触屏，禁止依赖鼠标/手柄/键盘（可预留开发期键盘模拟）
- **多点触控**：双虚拟摇杆需独立追踪两根手指的 `fingerId`，必须使用 `EnhancedTouchSupport`
- **跨场景引用禁令**：ADR-0001 `forbidden_pattern: cross_scene_direct_reference` — 输入系统不得跨场景持有 GameObject/Component 直接引用
- **事件通信规范**：ADR-0002 三问法 — 跨场景输入状态变化必须走 Tier 1 SO Channel
- **帧预算**：移动端 16.6ms，输入处理必须在主线程 Update() 中完成，禁止每帧 GC 分配

### Requirements
- 星图层输入上下文（StarMapActions）：触屏点击节点、拖拽星图、按钮交互
- 驾驶舱层输入上下文（CockpitActions）：左摇杆推进/方向、右摇杆瞄准、武器开火按钮
- 输入上下文必须随 ViewLayer 状态切换自动激活/停用
- ShipState（IN_COCKPIT / IN_COMBAT）门控驾驶舱输入，防止 ShipState 不匹配时误触发飞船操控
- 虚拟摇杆死区必须可配置（JOYSTICK_DEAD_ZONE）

## Decision

采用 **New Input System 双 ActionMap 上下文切换架构**：

- 唯一的 `.inputactions` 资产定义两个 ActionMap：`StarMapActions` 和 `CockpitActions`
- 场景启动时：StarMapActions 启用，CockpitActions 禁用
- `ShipInputManager`（挂载于 MasterScene）订阅 `ViewLayerChannel.OnViewLayerChanged`（Tier 1），根据 ViewLayer 状态互斥切换 ActionMap
- 驾驶舱层虚拟摇杆通过 `EnhancedTouchSupport.activeTouches` 手动追踪 `fingerId`，唯一所有权归 `ShipInputManager`
- 跨场景输入事件（CockpitScene → StarMapScene）通过新增 `ShipInputChannel`（Tier 1 SO Channel）广播，禁止直接引用

### 分类规则（输入事件三问法）

```
Q1: 输入结果是否需要跨 Unity 场景传递？
    YES → ShipInputChannel SO Channel（Tier 1）广播
    NO  → Q2

Q2: 输入结果是否由不同 MonoBehaviour 消费？
    YES → C# event Action<T>（Tier 2）
    NO  → 直接方法调用（Tier 3）
```

### ActionMap 切换规则

| ViewLayer 状态 | StarMapActions | CockpitActions |
|---|---|---|
| STARMAP | ✅ 启用 | ❌ 禁用 |
| COCKPIT | ❌ 禁用 | ✅ 启用（需 ShipState 检查）|
| SWITCHING_IN / SWITCHING_OUT | ❌ 禁用（两者） | ❌ 禁用（两者） |

> **ShipState 门控**：CockpitActions 启用后，飞船物理控制（推进/旋转）进一步由 `ShipState ∈ {IN_COCKPIT, IN_COMBAT}` 门控，由 `ShipControlSystem` 负责（不由本 ADR 决定，但接口预留）。

### 虚拟摇杆实现规则

**屏幕分区**（基于 GDD ship-control-system.md）：
- 左半屏：推进/方向摇杆（thrust + 航向）
- 右半屏：瞄准摇杆（fine aim）

**死区公式**（来自 GDD）：
```
const float DEAD_ZONE = 0.08f;  // JOYSTICK_DEAD_ZONE，可配置
float normalizedInput = Mathf.Clamp01((Mathf.Abs(offset) - DEAD_ZONE) / (1.0f - DEAD_ZONE));
```

**fingerId 追踪**：每个摇杆区域锁定首个进入该区域的 `fingerId`，指抬起时释放。禁止按 `touchIndex` 追踪（Touch 数量变化时 index 不稳定）。

**EnhancedTouchSupport 所有权约束（MANDATORY — 修复 B-1）**：
- `EnhancedTouchSupport.Enable()` 和 `Disable()` **唯一由 `ShipInputManager` 调用**
- 其他任何系统禁止独立调用这两个方法
- `ShipInputManager.OnEnable()` → `EnhancedTouchSupport.Enable()`
- `ShipInputManager.OnDisable()` → `EnhancedTouchSupport.Disable()`
- 若未来需要多系统共享触摸数据，必须通过 `ShipInputChannel` 事件分发，而非直接调用 `activeTouches`

### 跨场景输入事件架构（修复 B-2）

本 ADR 新增 `ShipInputChannel`（Tier 1 SO Channel），用于将驾驶舱操控结果广播至 StarMapScene（如飞船速度变化影响星图 UI）。直接跨场景引用方案（方案 A）**已明确禁止**（违反 ADR-0001 `cross_scene_direct_reference`）。

| Channel | 事件 | 生产者 | 消费者 | 原因 |
|---|---|---|---|---|
| ShipInputChannel | OnThrustChanged(float normalizedThrust) | ShipControlSystem (Cockpit) | StarMapUI (StarMap) | Cockpit → StarMap 跨场景 |
| ShipInputChannel | OnAimChanged(Vector2 aimDirection) | ShipControlSystem (Cockpit) | （可选，预留） | Cockpit 跨场景 |

> 注：如果飞船推进数据只在 CockpitScene 内消费（HUD 显示），则不需要 `ShipInputChannel`，使用 Tier 2 C# event 即可。本 ADR 预留接口，具体事件列表在 Foundation Epic 实现时确定。

### StarMap UI 事件优先级（修复 A-3）

StarMap 触屏交互**优先通过 UI Toolkit EventSystem 处理**（节点点击、按钮、滚动等）。`StarMapActions` 中的动作仅覆盖 UI Toolkit EventSystem 无法处理的场景（如星图背景区域的双指缩放手势）。禁止在 `StarMapActions` 中重复声明 UI Toolkit 已处理的点击/拖拽事件。

### Architecture Diagram

```
MasterScene（常驻）
├── ShipInputManager
│   ├── [SerializeField] ViewLayerChannel _viewLayerChannel （Tier 1）
│   ├── [SerializeField] ShipInputChannel _shipInputChannel （新增 Tier 1）
│   ├── PlayerInputActions _controls （InputActionAsset 生成类）
│   │
│   ├── OnEnable()
│   │   ├── _viewLayerChannel.OnViewLayerChanged += OnViewLayerChanged
│   │   ├── EnhancedTouchSupport.Enable()  ← 唯一所有权
│   │   └── _controls.StarMapActions.Enable()  ← 初始状态
│   │
│   ├── OnDisable()
│   │   ├── _viewLayerChannel.OnViewLayerChanged -= OnViewLayerChanged
│   │   ├── EnhancedTouchSupport.Disable()
│   │   └── _controls.Disable()
│   │
│   └── OnViewLayerChanged(ViewLayer layer)
│       ├── STARMAP → StarMapActions.Enable(), CockpitActions.Disable()
│       ├── COCKPIT → StarMapActions.Disable(), CockpitActions.Enable()
│       └── SWITCHING_* → StarMapActions.Disable(), CockpitActions.Disable()
│
StarMapScene（常驻）
└── StarMapUI
    ├── [Tier 1] ← ShipInputChannel.OnThrustChanged（如需显示飞船速度）
    └── UI Toolkit EventSystem 处理节点点击/拖拽（优先于 StarMapActions）
│
CockpitScene（按需）
└── ShipControlSystem
    ├── Update() → 读取 EnhancedTouch.activeTouches → 计算死区归一化输入
    ├── FixedUpdate() → Rigidbody.AddForce（thrust）+ AddTorque（aim）
    └── [Tier 2] → OnThrustChanged → ShipHUD（同场景）
```

### Key Interfaces

```csharp
// ─── InputActionAsset 生成类（PlayerInputActions.cs，由 Unity 自动生成）─────────
// 资产路径：Assets/Settings/Input/PlayerInputActions.inputactions
// 勾选 Generate C# Class，生成文件放置于 Assets/Scripts/Generated/

// ─── ShipInputManager（MasterScene）────────────────────────────────────────────
public class ShipInputManager : MonoBehaviour
{
    [SerializeField] private ViewLayerChannel _viewLayerChannel;   // ADR-0002 Tier 1
    [SerializeField] private ShipInputChannel _shipInputChannel;   // 新增 Tier 1

    private PlayerInputActions _controls;

    private void Awake()
    {
        _controls = new PlayerInputActions();
    }

    private void OnEnable()
    {
        Debug.Assert(_viewLayerChannel != null, "[ShipInputManager] ViewLayerChannel not wired!");
        _viewLayerChannel.OnViewLayerChanged += OnViewLayerChanged;

        // 唯一所有权：禁止其他系统调用 EnhancedTouchSupport.Enable/Disable
        EnhancedTouchSupport.Enable();

        // 初始状态：StarMap 激活，Cockpit 停用
        _controls.StarMapActions.Enable();
        _controls.CockpitActions.Disable();
    }

    private void OnDisable()
    {
        _viewLayerChannel.OnViewLayerChanged -= OnViewLayerChanged;
        EnhancedTouchSupport.Disable();
        _controls.Disable();
    }

    private void OnViewLayerChanged(ViewLayer layer)
    {
        switch (layer)
        {
            case ViewLayer.STARMAP:
                _controls.CockpitActions.Disable();
                _controls.StarMapActions.Enable();
                break;
            case ViewLayer.COCKPIT:
                _controls.StarMapActions.Disable();
                _controls.CockpitActions.Enable();
                break;
            default:  // SWITCHING_IN / SWITCHING_OUT
                _controls.StarMapActions.Disable();
                _controls.CockpitActions.Disable();
                break;
        }
    }
}

// ─── ShipControlSystem（CockpitScene）─────────────────────────────────────────
public class ShipControlSystem : MonoBehaviour
{
    [SerializeField] private ShipInputChannel _shipInputChannel;

    private const float DEAD_ZONE = 0.08f;  // JOYSTICK_DEAD_ZONE，可在 Inspector 覆盖
    [SerializeField] private float _deadZone = DEAD_ZONE;

    private const float JOYSTICK_MAX_RADIUS = 80f; // 屏幕像素，原型阶段通过 Inspector 调参

    private int     _thrustFingerId  = -1;
    private int     _aimFingerId     = -1;
    private Vector2 _thrustStartPos;             // Touch.Began 时记录的起点位置
    private Vector2 _aimStartPos;
    private Vector2 _thrustInput;
    private Vector2 _aimInput;

    private Rigidbody _rb;

    private void Awake() => _rb = GetComponent<Rigidbody>();

    private void Update()
    {
        // 仅在 ShipState ∈ {IN_COCKPIT, IN_COMBAT} 时处理（ShipControlSystem 自行检查）
        ProcessTouchInput();
    }

    private void FixedUpdate()
    {
        if (_thrustInput.sqrMagnitude > 0f)
        {
            float thrust = ApplyDeadZone(_thrustInput.magnitude);
            _rb.AddForce(transform.forward * ThrustPower * thrust, ForceMode.Force);
        }
        // Rigidbody.linearDamping（Unity 6 API，原 .drag）由 Inspector 配置
    }

    private void ProcessTouchInput()
    {
        foreach (var touch in EnhancedTouch.Touch.activeTouches)
        {
            bool isLeftHalf = touch.screenPosition.x < Screen.width * 0.5f;

            if (touch.phase == TouchPhase.Began)
            {
                if (isLeftHalf && _thrustFingerId == -1)
                {
                    _thrustFingerId = touch.finger.index;
                    _thrustStartPos = touch.screenPosition;   // 记录摇杆起点
                }
                else if (!isLeftHalf && _aimFingerId == -1)
                {
                    _aimFingerId = touch.finger.index;
                    _aimStartPos = touch.screenPosition;      // 记录摇杆起点
                }
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                if (touch.finger.index == _thrustFingerId) { _thrustFingerId = -1; _thrustInput = Vector2.zero; }
                if (touch.finger.index == _aimFingerId)    { _aimFingerId    = -1; _aimInput    = Vector2.zero; }
            }
            else if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
            {
                if (touch.finger.index == _thrustFingerId)
                {
                    // 使用起点偏移量（而非每帧 delta），帧率无关
                    Vector2 offset = touch.screenPosition - _thrustStartPos;
                    float   mag    = Mathf.Clamp01(offset.magnitude / JOYSTICK_MAX_RADIUS);
                    _thrustInput   = offset.normalized * ApplyDeadZone(mag);
                }
                if (touch.finger.index == _aimFingerId)
                {
                    Vector2 offset = touch.screenPosition - _aimStartPos;
                    float   mag    = Mathf.Clamp01(offset.magnitude / JOYSTICK_MAX_RADIUS);
                    _aimInput      = offset.normalized * ApplyDeadZone(mag);
                }
            }
        }
    }

    private float ApplyDeadZone(float raw)
        => Mathf.Clamp01((raw - _deadZone) / (1.0f - _deadZone));

    [SerializeField] private float ThrustPower = 10f;  // 待原型验证
}

// ─── ShipInputChannel（新增 Tier 1 SO Channel）──────────────────────────────────
[CreateAssetMenu(menuName = "Channels/ShipInputChannel")]
public class ShipInputChannel : ScriptableObject
{
    public event Action<float>   OnThrustChanged;   // normalizedThrust 0–1
    public event Action<Vector2> OnAimChanged;      // 归一化方向

    public void RaiseThrust(float thrust) => OnThrustChanged?.Invoke(thrust);
    public void RaiseAim(Vector2 aim)     => OnAimChanged?.Invoke(aim);
}
```

## Alternatives Considered

### Alternative A: 跨场景直接引用 ShipInputManager
- **Description**: CockpitScene 的 ShipControlSystem 直接持有 MasterScene 中 ShipInputManager 的组件引用
- **Pros**: 实现简单，无需额外 SO Channel
- **Cons**: **违反 ADR-0001 `cross_scene_direct_reference` 禁令** — CockpitScene 卸载时引用变为 null，产生 MissingReferenceException
- **Rejection Reason**: BLOCKING — 与已注册的架构约束直接冲突，不可接受

### Alternative B: 每帧轮询 InputSnapshot（共享静态数据）
- **Description**: ShipInputManager 将每帧输入结果写入静态 `InputSnapshot` 结构体，CockpitScene 每帧读取
- **Pros**: 无场景引用问题
- **Cons**: 静态全局状态违反 ADR-0002 `static_event_bus` 禁令（字符串 key 替换为静态字段，本质相同）；每帧轮询增加 GC 压力
- **Rejection Reason**: 违反 ADR-0002 `static_event_bus` 禁令

### Alternative C: ShipInputChannel SO Channel 广播（本决策）
- **Description**: 输入结果通过 Tier 1 SO Channel 广播，跨场景消费者订阅
- **Pros**: 完全符合 ADR-0001 和 ADR-0002 约束；与 ViewLayerChannel、CombatChannel 架构对称
- **Cons**: 需新增一个 SO Channel 资产，高频输入事件若每帧广播可能产生微量开销
- **Adoption Reason**: 符合已确立的架构规范，一致性最优

### Alternative D: PlayerInput 组件自动切换
- **Description**: 使用 Unity 内置 `PlayerInput` 组件，配置 Action Map 自动切换
- **Pros**: 引擎原生支持，配置化
- **Cons**: `PlayerInput` 的自动切换基于 GameObject 激活/停用，与 ADR-0001 场景拓扑（MasterScene 持有 InputManager）不兼容；切换时序不受 ViewLayer 状态机控制
- **Rejection Reason**: 切换时序无法保证与 ViewLayer._isSwitching 原子化同步

## Consequences

### Positive
- ActionMap 互斥切换消除了星图/驾驶舱输入状态混用的整类 bug
- `EnhancedTouchSupport` 唯一所有权约束防止多系统竞争触摸数据
- 死区公式统一在 `ShipControlSystem` 中，可在 Inspector 调整 `_deadZone` 无需改代码
- 与 ADR-0001 ViewLayer 枚举和 ADR-0002 SO Channel 规范完全对齐

### Negative
- 开发期需手动管理 `.inputactions` 资产和生成的 C# 类（每次 Action 变更需重新生成）
- `ShipInputManager` 挂载于 MasterScene——Singleton 风险（与 ADR-0001 的 R-3 相同缓解措施）
- 虚拟摇杆触点归一化依赖 `Screen.dpi`，在不同屏幕密度的设备上需验证手感一致性

### Risks
- **风险 R-1：ActionMap 切换与 ViewLayer 切换存在单帧延迟**
  缓解：`ViewLayerChannel.OnViewLayerChanged` 在 MasterScene Update() 同帧广播，`ShipInputManager` 在同帧的事件处理中切换 ActionMap；切换期间（SWITCHING_*）两个 ActionMap 均禁用，防止误输入
- **风险 R-2：CockpitScene 热重载时 EnhancedTouchSupport 残留触点**
  缓解：`ShipInputManager` 在 MasterScene（常驻），`EnhancedTouchSupport` 生命周期与 MasterScene 一致，不受 CockpitScene 重载影响；CockpitScene 卸载时 `ShipControlSystem` 的 `_thrustFingerId` / `_aimFingerId` 随场景销毁，下次加载重新初始化为 -1
- **风险 R-3：`_deadZone` 值待原型验证**
  缓解：`JOYSTICK_DEAD_ZONE = 0.08f` 为 GDD 初始值；实际值在驾驶舱操控原型验证后调整，通过 `[SerializeField]` 在 Inspector 直接调参
- **风险 R-4：高频 ShipInputChannel 事件（每帧广播）GC 分配**
  缓解：若需每帧广播推进值，改用轮询（CockpitScene 的 ShipControlSystem 直接读取 MasterScene 的 `ShipInputManager.CurrentThrustInput` 属性），仅在状态**变化**时广播事件
- **风险 R-5：UniTask 包依赖（来自 ADR-0002 ADV-03）**
  缓解：Foundation Epic 实现前需确认 UniTask 已加入 Unity Package Manager（此风险与 ADR-0002 共享，不重复）

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| ship-control-system.md | 双虚拟摇杆：左推进/方向，右瞄准；JOYSTICK_DEAD_ZONE = 0.08 | CockpitActions ActionMap + EnhancedTouch fingerId 追踪；死区公式直接采用 GDD 值 |
| ship-control-system.md | 独立 fingerId 追踪（每个摇杆锁定首个进入该区的手指） | `_thrustFingerId` / `_aimFingerId` 分离追踪，Touch.Began/Ended/Canceled 独立管理 |
| ship-control-system.md | `Rigidbody.linearDamping`（Unity 6 API）在 FixedUpdate 应用 | ShipControlSystem.FixedUpdate() 中调用 AddForce；`linearDamping` 字段名已验证为 Unity 6 正确 API |
| dual-perspective-switching.md | 输入处理在 ShipState = IN_COCKPIT 后启动 | CockpitActions 启用后，ShipControlSystem 进一步检查 ShipState；ViewLayer COCKPIT 时才启用 CockpitActions |
| dual-perspective-switching.md | 视角切换时 _isSwitching = true 防并发 | SWITCHING_* 状态下两个 ActionMap 均禁用，防止切换中间帧产生误输入 |
| dual-perspective-switching.md | ViewLayerChannel SO Channel 作为跨场景事件总线 | ShipInputManager 订阅 ViewLayerChannel.OnViewLayerChanged（Tier 1），完全遵循 ADR-0002 规范 |

## Performance Implications
- **CPU**: ActionMap.Enable/Disable 为 O(1) 操作，每次 ViewLayer 切换约 <0.1ms；EnhancedTouch.activeTouches 遍历为 O(touch_count)，移动端最多 5–10 点，可忽略不计
- **Memory**: `PlayerInputActions` 对象约 ~4KB，常驻 MasterScene；ShipInputChannel SO ~200B，与其他 Channel 量级相同
- **GC**: 死区公式和 fingerId 追踪均为值类型操作，零 GC 分配；`EnhancedTouch.Touch.activeTouches` 返回 ReadOnlyArray，无 GC
- **Load Time**: `.inputactions` 资产随 MasterScene 加载，预估 <1ms

## Migration Plan
首次实现（无现有代码需迁移）：
1. 在 Unity Package Manager 中安装 `com.unity.inputsystem@1.11`
2. 在 Project Settings → Input System → Active Input Handling 中切换为 "New Input System Only"
3. 创建 `Assets/Settings/Input/PlayerInputActions.inputactions`，定义 `StarMapActions` 和 `CockpitActions` 两个 ActionMap，勾选 Generate C# Class
4. 创建 `ShipInputManager.cs`（MasterScene）和 `ShipInputChannel.asset`（`assets/data/channels/`）
5. 在 MasterScene 的 ShipInputManager GameObject 上连线 ViewLayerChannel 和 ShipInputChannel（Inspector）
6. 创建 `ShipControlSystem.cs`（CockpitScene），连线 ShipInputChannel

## Validation Criteria
- **AC-INP-01**：进入驾驶舱后，左半屏触点驱动飞船前进，右半屏触点驱动瞄准旋转（CockpitActions 正常激活）
- **AC-INP-02**：返回星图后，星图节点可正常点击，触屏不触发飞船推进（StarMapActions 正常互斥切换）
- **AC-INP-03**：双指同时触屏（左右各一），两个摇杆独立响应，不互相干扰（fingerId 追踪正确）
- **AC-INP-04**：死区归一化——偏移量 ≤ 0.08 时推进力为 0，偏移量 = 1.0 时推进力为 1.0（死区公式正确）
- **AC-INP-05**：ViewLayer 切换过渡期间（SWITCHING_*），触屏对星图和驾驶舱均无响应（双 ActionMap 禁用）
- **AC-INP-06**：帧率一致性——同样的手指移速在 30fps 与 60fps 下产生的推进力差异 ≤ 5%（起点偏移量归一化，而非每帧 delta）

## Related Decisions
- ADR-0001（场景管理架构）— 场景拓扑（MasterScene 常驻）决定了 ShipInputManager 的挂载位置
- ADR-0002（事件/通信架构）— ViewLayerChannel 订阅规范（Tier 1）和 OnEnable/OnDisable 配对强制约束
- design/gdd/ship-control-system.md — 虚拟摇杆设计、死区值、推进公式的 GDD 来源
- design/gdd/dual-perspective-switching.md — ViewLayer 状态机和输入门控规则的 GDD 来源
