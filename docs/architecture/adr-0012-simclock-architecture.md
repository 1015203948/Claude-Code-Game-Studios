# ADR-0012: SimClock 策略层时间管理架构

## Status

Accepted

## Date

2026-04-14

## Last Verified

2026-04-14

## Decision Makers

Technical Director, Unity Specialist

## Summary

双视角切换系统需要独立控制策略层时间（星图上的舰队移动、资源产出等）而不影响驾驶舱物理。决策：禁止使用 `Time.timeScale` 控制策略层（它是全局的，会破坏驾驶舱物理）；在 MasterScene 引入 `SimClock` 单例，维护 `SimRate ∈ {0, 1, 5, 20}` 倍率，策略层所有系统改用 `SimClock.DeltaTime = Time.unscaledDeltaTime × SimRate`，驾驶舱物理继续使用 `Time.deltaTime`。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Core / Scripting |
| **Knowledge Risk** | LOW — `Time.unscaledDeltaTime` 是长期稳定 API，无 post-cutoff 变化 |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md` |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | 验证 `Time.unscaledDeltaTime` 在 `Time.timeScale = 1`（保持默认）时行为与 `Time.deltaTime` 等价；验证 `FixedUpdate`（驾驶舱物理）不受 `Time.unscaledDeltaTime` 影响 |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0001（MasterScene 为全局单例容器）、ADR-0002（事件总线，SimRate 变更通知） |
| **Enables** | ColonySystem、FleetDispatch、ResourceSystem 的时间驱动迁移；ViewLayerManager 的 SimRate 显示 |
| **Blocks** | 任何策略层系统的实现（在此 ADR Accepted 之前，不允许使用 `Time.deltaTime` 驱动策略层） |
| **Ordering Note** | 必须在任何策略层系统（殖民地、舰队调度、资源）实现之前 Accepted |

## Context

### Problem Statement

双视角切换系统（GDD dual-perspective-switching.md，规则 S-1 / S-10）要求：
1. 策略层时间可暂停、加速（SimRate 0/1×/5×/20×）
2. 驾驶舱飞船物理**永远**使用真实时间，不受策略层速率影响

Unity 的 `Time.timeScale` 是全局设置，修改它会同时影响所有使用 `Time.deltaTime` 的系统，包括 `FixedUpdate`（物理）。无法用它实现「策略层加速但驾驶舱物理不变」的需求。

### Current State

项目尚未实现任何策略层系统。此 ADR 是预防性决策，在所有依赖系统实现之前确立规则，防止错误模式被引入。

### Constraints

- **驾驶舱物理不可干扰**：`Rigidbody` / `Rigidbody2D` 使用 `FixedUpdate`，受 `Time.timeScale` 影响
- **SimRate 必须跨视图保持**：星图和驾驶舱均可修改 SimRate（UX 规格，面板 E）
- **SimRate 须存档**：存档时保存当前 SimRate（GDD 规则 S-10）
- **单例限制**：SimClock 必须是 MasterScene 持有的单例，不能依赖任何视角状态

### Requirements

- SimRate 可取值 `{0, 1, 5, 20}`（整数或 float，对应 GDD tuning knob `SIM_RATE_OPTIONS`）
- `SimClock.DeltaTime` 在策略层 `Update` 中可替代 `Time.deltaTime`
- SimRate = 0 时策略层完全暂停（DeltaTime = 0）
- SimRate 变更立即生效（≤ 1 帧）
- `Time.timeScale` 保持默认值 1，不修改
- SimRate 变更通过事件总线广播（ADR-0002），订阅方可响应 UI 更新

## Decision

在 MasterScene 引入 `SimClock` MonoBehaviour 单例：

```
SimClock.DeltaTime = Time.unscaledDeltaTime × SimRate
```

- `Time.timeScale` **永远不修改**（保持 1）
- 策略层所有系统的 `Update` 必须使用 `SimClock.DeltaTime` 替代 `Time.deltaTime`
- 驾驶舱物理（`FixedUpdate`、`Rigidbody` AddForce 等）继续使用 `Time.deltaTime`，不订阅 SimClock
- SimRate 变更时广播 `SimRateChanged` ScriptableObject 事件（ADR-0002 事件总线）

### Architecture

```
MasterScene
  └─ SimClock (MonoBehaviour, DontDestroyOnLoad)
       ├─ SimRate: float  {0, 1, 5, 20}
       ├─ DeltaTime: float  = Time.unscaledDeltaTime × SimRate  (read-only property)
       └─ SetRate(float rate)  → 验证值域 → 更新 SimRate → 广播 SimRateChanged 事件

策略层系统 (StarMapScene / MasterScene)
  ColonySystem.Update()    → SimClock.DeltaTime ✓
  FleetDispatch.Update()   → SimClock.DeltaTime ✓
  ResourceSystem.Update()  → SimClock.DeltaTime ✓

驾驶舱系统 (CockpitScene)
  ShipController.FixedUpdate()  → Time.deltaTime ✓  (永远不使用 SimClock)
  PhysicsSystem.FixedUpdate()   → Time.deltaTime ✓

UI 层 (MasterScene / StarMapScene)
  SimRateDisplay → 订阅 SimRateChanged → 更新按钮高亮和标签
  ViewLayerManager → 不依赖 SimRate（SimClock 是独立系统）

时间流：
  真实时间 ──► Time.unscaledDeltaTime ──► × SimRate ──► SimClock.DeltaTime ──► 策略层
            └─────────────────────────────────────────► Time.deltaTime ──► 驾驶舱物理
```

### Key Interfaces

```csharp
// MasterScene 持有，DontDestroyOnLoad
// 通过静态 Instance 访问（策略层系统只读取 DeltaTime 和 SimRate，不持有引用）
public class SimClock : MonoBehaviour
{
    public static SimClock Instance { get; private set; }

    // 当前倍率。只通过 SetRate() 修改，不直接赋值
    public float SimRate { get; private set; } = 1f;

    // 策略层系统用此替代 Time.deltaTime
    public float DeltaTime => Time.unscaledDeltaTime * SimRate;

    // 合法值：{0f, 1f, 5f, 20f}；非法值静默忽略（或 Assert）
    public void SetRate(float rate);

    // ScriptableObject 事件（ADR-0002 模式）
    [SerializeField] private GameEvent _simRateChangedEvent;
    // 广播 payload: float newRate
}

// 策略层系统示例（FleetDispatch）
public class FleetDispatch : MonoBehaviour
{
    private void Update()
    {
        float dt = SimClock.Instance.DeltaTime;  // ← 必须用此
        // 使用 dt 推进舰队位置
        // 禁止: Time.deltaTime, Time.unscaledDeltaTime（直接使用）
    }
}

// 驾驶舱物理示例（ShipController）
public class ShipController : MonoBehaviour
{
    private void FixedUpdate()
    {
        float dt = Time.deltaTime;  // ← 驾驶舱物理永远用此
        // 禁止: SimClock.Instance.DeltaTime
    }
}
```

### Implementation Guidelines

1. **`Time.timeScale` 禁止修改**——任何地方修改 `Time.timeScale`（哪怕是调试代码）均视为违规，会破坏驾驶舱物理的确定性
2. **策略层系统必须迁移**——ColonySystem、FleetDispatch、ResourceSystem 实现时，首行检查是否使用了 `Time.deltaTime`；若是，替换为 `SimClock.Instance.DeltaTime`
3. **SimRate = 0 不是暂停游戏**——`DeltaTime` 返回 0，但 `Update()` 仍然执行；物理仍然运行；驾驶舱仍可操控。这是**策略层暂停**，不是全局暂停
4. **SimClock 不归 ViewLayerManager 所有**——SimClock 是独立系统；ViewLayerManager 不修改 SimRate，只在 UI 层展示当前值
5. **合法 SimRate 值验证**：在 `SetRate()` 内使用 `Assert` 或日志警告拒绝非 `{0, 1, 5, 20}` 的值；不静默接受任意 float
6. **存档**：`ShipDataModel.SaveData` 包含 `SimRate` 字段；存档时写入当前值；读档时恢复（默认 1）
7. **单元测试**：`SimClock.DeltaTime` 的计算逻辑（乘法公式）是 EditMode 测试的优先目标

## Alternatives Considered

### Alternative 1: 修改 Time.timeScale 暂停/加速策略层

- **Description**: SimRate 变更时设置 `Time.timeScale = SimRate`（0 = 暂停，5 = 5× 加速）
- **Pros**: 零实现成本——所有系统自动受影响
- **Cons**: `FixedUpdate` 执行频率随 `timeScale` 变化，导致驾驶舱 Rigidbody 物理在 SimRate > 1 时超速，SimRate = 0 时冻结；驾驶舱物理与策略层无法解耦
- **Estimated Effort**: Minimal
- **Rejection Reason**: 根本性地违反「驾驶舱物理永远使用真实时间」的核心设计规则（GDD 规则 S-1）

### Alternative 2: 每个策略层系统自己维护暂停状态（bool isPaused）

- **Description**: 各系统订阅「暂停」事件，内部维护布尔标志，在 `Update` 首行 `if (isPaused) return`
- **Pros**: 不依赖全局时钟；各系统独立控制
- **Cons**: 仅支持暂停，不支持 5× / 20× 加速；每个系统都要重复实现暂停逻辑；加速逻辑无法复用；不满足 GDD 对时间压缩的需求
- **Estimated Effort**: Medium（每个系统单独改）
- **Rejection Reason**: 无法支持时间加速功能；代码重复；需求不完整

### Alternative 3: 使用 Unity Job System / DOTS 时间域

- **Description**: 策略层使用 DOTS World 的独立时间域（`World.Time.DeltaTime`）
- **Pros**: 原生支持独立时间域；高性能
- **Cons**: 需要全量迁移策略层到 ECS 架构；大幅增加技术复杂度；与当前 MonoBehaviour 架构（ADR-0001）不兼容；对一个移动端策略游戏过度设计
- **Estimated Effort**: Very High
- **Rejection Reason**: 过度复杂；当前项目不使用 DOTS；`SimClock` 方案足以满足需求

## Consequences

### Positive

- 驾驶舱物理与策略层时间完全解耦，可独立验证
- SimRate 支持 0/1/5/20 四档，满足 GDD tuning knob `SIM_RATE_OPTIONS` 需求
- 公式简单（一次乘法），单元测试友好
- `Time.timeScale` 保持默认值，不破坏任何 Unity 内置系统（动画、物理、粒子）

### Negative

- 所有策略层系统必须主动替换 `Time.deltaTime` → `SimClock.Instance.DeltaTime`；遗漏会导致 SimRate 失效但无错误提示
- `SimClock.Instance` 空引用风险（如在 MasterScene 初始化前访问）——需确保 SimClock 在所有策略系统之前 Awake
- 不能用 Unity 的 `Time.captureFramerate` / `Time.captureDeltaTime` 进行帧率锁定（这些依赖 timeScale）

### Neutral

- `Time.timeScale = 1` 永远保持——Unity 的全局暂停菜单无法通过 `timeScale = 0` 实现（需要通过 `SimRate = 0` + 输入屏蔽实现）
- 存档中需增加 `SimRate` 字段

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| 某策略层系统遗漏迁移，仍使用 Time.deltaTime | MEDIUM | MEDIUM | `/architecture-review` 中增加 Grep 检查：策略层文件中禁止出现裸 `Time.deltaTime` |
| SimClock 初始化顺序错误，策略层 Awake 时 Instance 为 null | LOW | HIGH | SimClock 的 Script Execution Order 设置为最高优先级（-1000）；Awake 中使用 Assert |
| SimRate = 0 时，依赖 Time.deltaTime 的驾驶舱 ParticleSystem / Animator 表现异常 | LOW | LOW | Time.timeScale 保持 1，不影响这些系统；SimRate = 0 对它们透明 |

## Performance Implications

| Metric | Before | Expected After | Budget |
|--------|--------|---------------|--------|
| CPU (SimClock.Update) | 0ms | < 0.01ms（单次乘法） | 16.6ms |
| Memory | 0 | < 1KB（单例对象） | TBD |
| 策略层 GC | 基准 | 无变化（float 运算，无 GC） | — |

## Migration Plan

此 ADR 是新增功能，无现有策略层系统需迁移。

实施步骤：
1. 在 MasterScene 创建 `SimClock.cs`，实现单例模式和 `SetRate()` 方法
2. 在 MasterScene 的 GameObject 上挂载 `SimClock`，设置 Script Execution Order = -1000
3. 创建 `SimRateChanged` ScriptableObject 事件资产（ADR-0002 模式）
4. 实现 `SimRateDisplay` UI 控件，订阅 `SimRateChanged`，更新 ⏸/1×/5×/20× 按钮高亮
5. 每个新策略层系统实现时，PR checklist 中包含「是否使用 SimClock.DeltaTime」检查项
6. 在 `tests/EditMode/` 中为 SimClock.DeltaTime 公式编写 EditMode 单元测试

**Rollback plan**: SimClock 是新系统，无回退成本。若需移除，将策略层系统中 `SimClock.Instance.DeltaTime` 替换回 `Time.deltaTime`，删除 SimClock.cs 和相关事件资产。

## Validation Criteria

- [ ] `SimClock.DeltaTime` EditMode 单元测试通过（覆盖 SimRate = 0/1/5/20 的四种情况）
- [ ] SimRate = 5 时，策略层 1 秒真实时间内，舰队移动距离约等于 5 秒 1× 速率的移动距离（误差 ≤ 1 帧，对应 AC-DVS-21）
- [ ] SimRate = 0 时，策略层完全暂停，驾驶舱飞船物理继续响应操控（对应 AC-DVS-22）
- [ ] `Time.timeScale` 在运行时保持为 1（可通过 Profiler 或 Debug.Log 验证）
- [ ] `/architecture-review` Grep 检查：`CockpitScene` 目录下不出现 `SimClock.Instance`

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `design/gdd/dual-perspective-switching.md` | 双视角切换 | 规则 S-1：StarMapScene 策略系统以 `SimClock.DeltaTime` 驱动；SimRate = 0 时策略层暂停 | SimClock 单例提供 DeltaTime 属性，SimRate = 0 时返回 0 |
| `design/gdd/dual-perspective-switching.md` | 双视角切换 | 规则 S-10：SimRate 可在两个视图随时调整；驾驶舱物理不受 SimRate 影响 | Time.timeScale 保持 1；SimClock 仅影响订阅 DeltaTime 的策略层系统 |
| `design/gdd/dual-perspective-switching.md` | 双视角切换 | 公式 D-DVS-4：`SimClock.DeltaTime = Time.unscaledDeltaTime × SimRate` | 此 ADR 的核心实现公式 |
| `design/gdd/dual-perspective-switching.md` | 双视角切换 | AC-DVS-21：SimRate 5× 时舰队移动距离约等于 5× 常速 | 由 SimClock.DeltaTime 直接保证 |
| `design/gdd/dual-perspective-switching.md` | 双视角切换 | AC-DVS-22：SimRate = 0 时策略层暂停，驾驶舱物理继续 | SimRate = 0 → DeltaTime = 0；FixedUpdate 使用 Time.deltaTime，不受影响 |

## Related

- ADR-0001: 场景管理架构（MasterScene 为 SimClock 容器）
- ADR-0002: 事件通信架构（`SimRateChanged` 事件广播模式）
- ADR-0007: 叠加渲染架构（并列新增系统，不相互依赖）
