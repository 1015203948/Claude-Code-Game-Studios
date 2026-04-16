# ADR-0014: Health System Architecture

## Status
Accepted

## Date
2026-04-15

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Core / Health |
| **Knowledge Risk** | LOW — HealthSystem 是纯数据追踪（float CurrentHull），无物理 API 调用，不涉及 post-cutoff Unity API。所有伤害来源（CombatSystem / EnemySystem）提供原始数值，HealthSystem 只做 Clamp 和事件广播。 |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `docs/engine-reference/unity/modules/physics.md` |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | (1) CurrentHull 在 ShipDataModel 和 HealthSystem 之间无数据不一致（单一致知来源）；(2) Hull=0 时 OnShipDying 广播一次，不重复广播；(3) 伤害在 IN_COCKPIT 和 IN_COMBAT 之外被静默忽略（DOCKED / IN_TRANSIT / DESTROYED） |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | None（HealthSystem 是基础层系统，不依赖其他 ADR） |
| **Enables** | ADR-0013（CombatSystem — ApplyDamage + OnShipDying）；EnemySystem ADR（ApplyDamage）；ship-combat-system.md AC 验证 |
| **Blocks** | Core Epic — HealthSystem 是驾驶舱战斗的前置依赖；Story 实施依赖本 ADR Accepted |
| **Ordering Note** | 本 ADR 应在 ADR-0013（CombatSystem）之前或同时 Proposed；避免 CombatSystem 基于不存在的 ApplyDamage 接口先行实现 |

## Context

### Problem Statement
HealthSystem 需要成为玩家飞船 Hull 值的唯一权威来源：接收来自 CombatSystem 和 EnemySystem 的伤害输入（`ApplyDamage`），在 CurrentHull 归零时触发死亡序列（H-5：OnShipDying → DestroyShip → OnPlayerShipDestroyed），并在每帧广播 `OnHullChanged` 供 HUD 订阅。同时，无人值守战斗失败路径（U-4）完全绕过 HealthSystem，不触发任何 HealthSystem 事件。

### Constraints
- **ShipState 门控**：DOCKED / IN_TRANSIT 状态不接受伤害（静默忽略）；IN_COCKPIT / IN_COMBAT 接受；DESTROYED 记录警告日志
- **Hull 单一来源**：`ShipDataModel.CurrentHull` 是唯一权威值，HealthSystem 只负责计算和广播，不独立存储
- **无人值守绕过**：U-4 路径（FleetDispatchSystem → ShipDataModel.DestroyShip）不触发 HealthSystem 任何事件
- **零 GC**：OnHullChanged 每次广播需避免委托分配（使用 pre-allocated 列表或 C# event）

### Requirements
- `ApplyDamage(instanceId, rawDamage, damageType)` — 接收任意来源的原始伤害，输出 Clamp 后的 Hull 值
- `HullRatio`（只读属性）— 供 HUD 计算血条百分比
- `OnHullChanged(instanceId, newHull, maxHull)` — 每次 Hull 变化时广播（newHull > 0 时）
- `OnShipDying(instanceId)` — Hull 归零时触发（H-5 Step 1），供 HUD/SFX/VFX 订阅
- `OnPlayerShipDestroyed(instanceId)` — 仅玩家飞船（IsPlayerControlled==true）Hull=0 时触发（H-5 Step 3），双视角切换系统订阅
- `OnShipDestroyed(instanceId)` — 通用销毁完成广播（H-5 Step 4）
- 死亡序列（H-5）同一帧内按 Step 1→2→3→4 顺序执行

## Decision

### HealthSystem 职责划分

```
HealthSystem（MasterScene 单例，跨场景可用）
├── 唯一 ApplyDamage 入口：finalDamage 计算 → Clamp → 回写 ShipDataModel.CurrentHull
├── Hull 变化广播：OnHullChanged(newHull, maxHull)（newHull > 0 时）
├── Hull=0 死亡序列（H-5）：
│   ├── Step 1：广播 OnShipDying(instanceId)
│   ├── Step 2：调用 ShipDataModel.DestroyShip(instanceId)
│   ├── Step 3：如果是玩家飞船 → 广播 OnPlayerShipDestroyed(instanceId)
│   └── Step 4：广播 OnShipDestroyed(instanceId)
└── HullRatio 只读查询（供 HUD 显示血条百分比）
```

### 关键接口定义

#### ApplyDamage（主入口）

```csharp
public enum DamageType { KINETIC /* MVP only, extendable */ }

/// <summary>
/// 应用伤害到指定飞船。
/// DOCKED / IN_TRANSIT 状态：静默忽略，返回 false。
/// DESTROYED 状态：记录警告日志，返回 false。
/// newHull == 0：触发死亡序列（H-5）。
/// </summary>
/// <returns>true if damage was applied; false if rejected (wrong state or ship not found)</returns>
public bool ApplyDamage(string instanceId, float rawDamage, DamageType damageType) {
    if (rawDamage < 0f) {
        Debug.LogWarning($"[HealthSystem] ApplyDamage: rawDamage must be >= 0. Clamping to 0.");
        rawDamage = 0f;
    }

    // ShipState 门控
    ShipState state = ShipDataModel.GetState(instanceId);
    if (state == ShipState.DESTROYED) {
        Debug.LogWarning($"[HealthSystem] {instanceId}: DESTROYED — ApplyDamage rejected.");
        return false;
    }
    if (state != ShipState.IN_COCKPIT && state != ShipState.IN_COMBAT) {
        // DOCKED / IN_TRANSIT — 静默忽略
        return false;
    }

    float maxHull = ShipDataModel.GetMaxHull(instanceId);
    float finalDamage = rawDamage; // D-1 / D-2 计算后的结果（调用方提供）
    float newHull = Mathf.Clamp(ShipDataModel.GetCurrentHull(instanceId) - finalDamage, 0f, maxHull);

    ShipDataModel.SetCurrentHull(instanceId, newHull);  // 回写到 ShipDataModel

    if (newHull <= 0f) {
        ExecuteDeathSequence(instanceId);
    } else {
        OnHullChanged?.Invoke(instanceId, newHull, maxHull);
    }
    return true;
}
```

#### 死亡序列（H-5，严格顺序，同帧完成）

```csharp
private void ExecuteDeathSequence(string instanceId) {
    // Step 1：广播 OnShipDying — 通知所有订阅者（CombatSystem、HUD、SFX、VFX）
    OnShipDying?.Invoke(instanceId);

    // Step 2：调用 DestroyShip — 状态变为 DESTROYED，通知星图清空 dockedFleet
    ShipDataModel.DestroyShip(instanceId);

    // Step 3：如果是玩家飞船，广播 OnPlayerShipDestroyed
    // （双视角切换系统订阅，强制退出驾驶舱）
    if (ShipDataModel.IsPlayerControlled(instanceId)) {
        OnPlayerShipDestroyed?.Invoke(instanceId);
    }

    // Step 4：广播 OnShipDestroyed — 通用销毁完成
    OnShipDestroyed?.Invoke(instanceId);
}
```

#### C# event 定义

```csharp
// HealthSystem.cs（MonoBehaviour 单例，挂载 MasterScene）
public class HealthSystem {
    public static HealthSystem Instance { get; private set; }

    // Hull 变化广播（newHull > 0 时）
    public event Action<string, float, float> OnHullChanged;  // (instanceId, newHull, maxHull)

    // 死亡序列广播
    public event Action<string> OnShipDying;                  // (instanceId) — 触发 CombatSystem 胜负判定
    public event Action<string> OnPlayerShipDestroyed;       // (instanceId) — 触发双视角切换退出驾驶舱
    public event Action<string> OnShipDestroyed;              // (instanceId) — 通用销毁完成
}
```

### 数据流图

```
CombatSystem / EnemySystem
        │
        ▼
ApplyDamage(instanceId, rawDamage, damageType)
        │
        ├── [Hull > 0] ──→ OnHullChanged(instanceId, newHull, maxHull)
        │                        ├──→ ShipHUD（订阅，更新血条 UI）
        │                        └──→ StarMapUI（订阅，更新节点图标血条）
        │
        └── [Hull == 0] ──→ ExecuteDeathSequence()
                                  ├── Step1: OnShipDying ──→ CombatSystem（订阅，检测胜负）
                                  ├── Step2: ShipDataModel.DestroyShip() ──→ ShipState = DESTROYED
                                  ├── Step3: [IsPlayerControlled?] ──→ OnPlayerShipDestroyed ──→ DualViewSystem
                                  └── Step4: OnShipDestroyed ──→ StarMapSystem（清空 dockedFleet）

FleetDispatchSystem（无人值守战斗）
        │
        ▼  [U-4 路径：绕过 HealthSystem]
ShipDataModel.DestroyShip() ──→ [无 HealthSystem 事件广播]
```

### HealthSystem 挂载位置

`HealthSystem` 作为 `MonoBehaviour` 单例挂载于 **MasterScene**。

理由：HealthSystem 的事件（OnHullChanged、OnPlayerShipDestroyed）需要跨场景广播（CockpitScene ↔ StarMapScene）。MasterScene 永不卸载，是跨场景共享状态的天然宿主。

### 与 CombatSystem 的协作关系

| 交互 | 方向 | 接口 |
|------|------|------|
| 伤害委托 | CombatSystem → HealthSystem | `ApplyDamage(instanceId, rawDamage, KINETIC)` |
| 战斗结束检测 | HealthSystem → CombatSystem | `OnShipDying`（C# event，Tier 2） |
| 状态变更 | HealthSystem → ShipDataModel | `DestroyShip()` |

### 无人值守战斗路径（U-4）

FleetDispatchSystem 在无人值守结算失败时，直接调用 `ShipDataModel.DestroyShip(shipId)`，**完全不经过 HealthSystem**。这是设计决策（U-4），不在 HealthSystem 中做特殊处理。

## Alternatives Considered

### Alternative 1: HealthSystem 内聚死亡序列 + CombatSystem 只订阅结果
- **Description**: HealthSystem 在 ApplyDamage → Hull=0 时直接执行 H-5 全部步骤；CombatSystem 只订阅 `OnShipDying`，不负责 ShipState 转换
- **Pros**: 死亡序列封装在 HealthSystem 内，CombatSystem 无需知道 ShipState 转换细节
- **Cons**: HealthSystem 需要引用 ShipDataModel.DestroyShip()，与 ShipDataModel 紧耦合
- **Rejection Reason**: 与 GDD H-5 文字冲突 — H-5 Step 2 明确「调用 ShipData.DestroyShip()」，这是 HealthSystem 的职责，不是 CombatSystem 的

### Alternative 2: CombatSystem 全权控制死亡序列，HealthSystem 只提供 Hull=0 信号
- **Description**: ApplyDamage 返回 Hull 是否归零；CombatSystem 收到 Hull=0 信号后负责执行 H-5 Step 2~4
- **Pros**: CombatSystem 集中控制战斗结果逻辑
- **Cons**: HealthSystem 不知道自己的信号触发了什么后续；CombatSystem 需要显式调用 ShipDataModel.DestroyShip()，违反单一职责
- **Rejection Reason**: 与 GDD H-5 文字冲突 — H-5 明确规定死亡序列由 HealthSystem 执行，不由 CombatSystem 负责

## Consequences

### Positive
- Hull 唯一权威来源：ShipDataModel 是 Hull 的存储方，HealthSystem 是计算+广播方，职责分离
- ShipState 门控清晰：DOCKED / IN_TRANSIT 不接受伤害是防御性设计，防止意外路径触发战斗
- H-5 死亡序列内聚：OnShipDying + DestroyShip + OnPlayerShipDestroyed + OnShipDestroyed 都在 HealthSystem 内按序执行，调用方无感知
- OnHullChanged 每帧广播：HUD 无需轮询，直接订阅事件更新血条

### Negative
- HealthSystem 单例紧耦合：所有需要 ApplyDamage 的系统（CombatSystem、EnemySystem、碰撞系统）都依赖 HealthSystem.Instance
- 无人值守路径（U-4）绕过 HealthSystem 意味着：HUD 无法收到 OnShipDying 来显示「飞船被摧毁」的特效；这是设计决策（U-4），不是 bug
- IsPlayerControlled 判断放在 HealthSystem 内（Step 3 条件）：HealthSystem 需要知道 ShipDataModel 的 IsPlayerControlled 字段

### Risks
- **风险 1**：`OnHullChanged` 每帧广播 → 大量 HUD 更新 → UI 卡顿
  - 缓解：HUD 只订阅 Hull 变化事件，不在 Update() 中轮询；如果性能仍成问题，添加节流（throttle）为每 0.1 秒更新一次
- **风险 2**：多个伤害源同时在同帧调用 ApplyDamage → Hull 变化被覆盖
  - 缓解：ApplyDamage 按调用顺序串行处理，每次回写 ShipDataModel；同帧多伤害由调用方在战斗系统层合并
- **风险 3**：`OnShipDying` 触发后 CombatSystem 和 HealthSystem 同时操作 ShipDataModel（竞态）
  - 缓解：H-5 顺序已固定（Step 2 DestroyShip 在 Step 1 OnShipDying 之后）；CombatSystem 在 OnShipDying 回调中不应再调用 HealthSystem（防御性设计）

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| ship-health-system.md §H-1 | 初始化：CurrentHull ← MaxHull | ShipDataModel 构造函数中由蓝图初始化，HealthSystem 在收到 ApplyDamage 前不做初始化 |
| ship-health-system.md §H-2 | ApplyDamage 路径：finalDamage → Clamp → SetCurrentHull → OnHullChanged/死亡序列 | ApplyDamage() 实现完全符合 H-2 流程 |
| ship-health-system.md §H-3 | 武器命中伤害：调用 ApplyDamage(id, rawDamage, KINETIC) | CombatSystem/EnemySystem 调用 ApplyDamage 时传入 KINETIC |
| ship-health-system.md §H-4 | 碰撞伤害：调用 ApplyDamage(id, rawDamage, COLLISION) | 碰撞系统调用 ApplyDamage 时传入 COLLISION |
| ship-health-system.md §H-5 | 死亡序列：Step 1→2→3→4 顺序执行 | ExecuteDeathSequence() 实现 H-5 全部四个步骤 |
| ship-health-system.md §D-4 | apply_damage：Clamp(CurrentHull - finalDamage, 0, MaxHull) | ApplyDamage() 内使用 Mathf.Clamp 实现 |
| ship-health-system.md §D-3 | health_ratio = CurrentHull / MaxHull | HealthSystem.HullRatio 只读属性暴露 |
| ship-combat-system.md §U-4 | 无人值守失败：绕过 HealthSystem，直接 DestroyShip | U-4 路径由 FleetDispatchSystem 直接调用 ShipDataModel.DestroyShip()，HealthSystem 不介入 |

## Performance Implications

| 项目 | 影响 | 缓解 |
|------|------|------|
| **CPU** | OnHullChanged 每帧广播（每帧 Hull 变化时） | HUD 更新在主线程；事件广播本身 O(1)；无 GC |
| **Memory** | 4 个 C# event（OnHullChanged/OnShipDying/OnPlayerShipDestroyed/OnShipDestroyed） | 每个 event 约 ~40B 委托引用；总计 ~160B；可接受 |
| **Load Time** | 无影响（纯逻辑系统，无资源加载） | — |

## Migration Plan

本 ADR 是全新系统，无现有代码迁移需求。

实施顺序：
1. 创建 `src/Gameplay/HealthSystem.cs`（MonoBehaviour 单例，挂载 MasterScene）
2. 在 `ShipDataModel` 添加 `SetCurrentHull()` 和 `GetCurrentHull()` 方法
3. 在 `ShipDataModel` 添加 `IsPlayerControlled` 属性暴露
4. 创建 `OnHullChangedChannel` SO Channel（如需要跨场景 HUD 订阅）或使用 C# event Tier 2（同场景）
5. CombatSystem 在收到 OnShipDying 后执行胜负判定

## Validation Criteria

| 验证条件 | 验证方法 |
|----------|----------|
| ApplyDamage(Hull=30, damage=8) → newHull = 22 | 单元测试 |
| ApplyDamage(Hull=5, damage=8) → Hull=0 + 触发 OnShipDying 一次 | 单元测试：验证 OnShipDying 只触发一次 |
| DOCKED 状态 ApplyDamage → 静默忽略，不广播事件 | 单元测试 |
| IN_COCKPIT ApplyDamage → OnHullChanged 广播 Hull > 0 | 单元测试 |
| 死亡序列 H-5：OnShipDying → DestroyShip → OnPlayerShipDestroyed → OnShipDestroyed 顺序 | 集成测试：mock 所有事件，验证调用顺序 |
| HullRatio = CurrentHull / MaxHull，在 [0, 1] 范围内 | 单元测试 |
| 负 rawDamage → Clamp 到 0，不报错 | 单元测试 |

## Related Decisions

- [ADR-0001: Scene Management Architecture](adr-0001-scene-management-architecture.md) — MasterScene 拓扑，HealthSystem 挂载位置
- [ADR-0004: Data Model Architecture](adr-0004-data-model-architecture.md) — ShipDataModel 单一权威数据来源
- [ADR-0013: Combat System Architecture](adr-0013-combat-system-architecture.md) — ApplyDamage 调用方，OnShipDying 订阅方
