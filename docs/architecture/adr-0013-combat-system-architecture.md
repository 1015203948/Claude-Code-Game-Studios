# ADR-0013: Combat System Architecture

## Status
Accepted

## Date
2026-04-15

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Combat / Physics |
| **Knowledge Risk** | MEDIUM — PhysX 5.1 collision APIs (`Physics.Raycast`, `Physics.OverlapSphereNonAlloc`) unchanged from Unity 2022 LTS. `CollisionDetectionMode.ContinuousDynamic` exists pre-cutoff. No post-cutoff physics APIs required. |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `docs/engine-reference/unity/modules/physics.md`, `docs/engine-reference/unity/breaking-changes.md` |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | (1) Raycast hit detection on fast-moving enemy at 200m range — verify no tunneling with `ContinuousDynamic`; (2) `OverlapSphereNonAlloc` zero-GC in combat loop on Android; (3) Weapon fire rate timer accurate across frame rate variations (use accumulated deltaTime, not real-time) |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0001（场景管理 — ViewLayer + ShipDataModel 单一权威数据源）；ADR-0002（事件通信 — CombatChannel Tier 1 SO Channel）；ADR-0004（数据模型 — ShipDataModel 状态机接口）；ADR-0014（Health System — ApplyDamage + OnShipDying）；ADR-0015（Enemy System — SpawnEnemy + enemy AI） |
| **Enables** | Core Epic — Combat System 实现；Story: 战斗驾驶舱状态机、无人值守结算、武器系统 |
| **Blocks** | Core Epic — Ship Control System（武器系统依赖 CombatSystem 的 FireRequested 接口）；Story 实施依赖本 ADR Accepted |

## Context

### Problem Statement
战斗系统需要同时支持两种完全不同的作战模式：玩家亲自操控驾驶舱时的实时弹道射击（依赖 HealthSystem 的生命值追踪），和玩家不在线时星图层的无人值守自动结算（直接销毁飞船，不经过 HealthSystem）。这两种模式共享「战斗」这个概念，但数据路径完全不同。此外，敌方单位由 EnemySystem 管理，CombatSystem 只负责判定命中和结算胜负。

### Constraints
- **移动端性能**：60fps，16.6ms 帧预算；禁止每帧 GC 分配（`Physics.RaycastNonAlloc`，`OverlapSphereNonAlloc`）
- **帧率独立**：武器射速计时器必须用 `Time.deltaTime` 累加，不能依赖实时间隔
- **无人值守绕过 HealthSystem**：GDD U-4 规则 — 无人值守失败路径 `DestroyShip()` 直接调用，不触发 `OnShipDying`
- **跨场景通信**：CombatSystem 位于 CockpitScene，战斗结果（胜/负）需通知 StarMapScene（节点状态更新），使用 CombatChannel Tier 1 SO Channel
- **武器自动开火**：MVP 无手动射击按钮，满足 `aim_angle ≤ FIRE_ANGLE_THRESHOLD` 即触发开火（`aim_angle` 由 ShipControlSystem 计算并暴露）

### Requirements
- 驾驶舱战斗状态机：IN_COCKPIT → IN_COMBAT → IN_COCKPIT（胜）或 DESTROYED（败）
- 武器射速控制：`_fireTimer += Time.deltaTime`，达阈值触发一次 Raycast 射击
- 命中检测：`Physics.Raycast` 沿飞船前向发射，命中碰撞体调用 `HealthSystem.ApplyDamage`
- 无人值守结算：双方各损失 P 和 E 艘飞船，同步单帧完成，败方直接 `DestroyShip()`
- 敌方 AI 查询：`Physics.OverlapSphereNonAlloc` 获取玩家位置，`Physics.Raycast` 用于敌方武器命中
- 战斗结果广播：`CombatChannel.RaiseBegin()`（SceneManager.sceneLoaded 后调用）、`CombatChannel.RaiseVictory()`、`CombatChannel.RaiseDefeat()`

## Decision

### 系统职责划分

采用**混合委托架构**，符合 GDD B/U/V/L 规则设计：

```
CombatSystem（CockpitScene）
├── 驾驶舱战斗状态机（IN_COCKPIT → IN_COMBAT → IN_COCKPIT/DESTROYED）
├── 武器射速计时器（_fireTimer 累加，每帧检测 aim_angle）
├── 玩家武器 Raycast 发射 + 命中判定
├── 伤害委托：HealthSystem.ApplyDamage(playerInstanceId, damage)
├── 无人值守结算：P/E 回合循环，败方直接 DestroyShip()
├── 战斗结果广播：CombatChannel.RaiseVictory/Defeat
└── 敌方 AI 触发：接收 EnemySystem 敌人生成完成事件

HealthSystem（MasterScene 单例）
├── 玩家飞船 Hull 追踪（CurrentHull）
├── ApplyDamage(instanceId, rawDamage, damageType) — 接收 CombatSystem 伤害
├── OnShipDying 事件广播 — 通知 CombatSystem 目标死亡
└── DestroyShip 触发 — 仅由 CombatSystem 无人值守路径调用

EnemySystem（CockpitScene）
├── EnemyInstance 管理（ai-0, ai-1）
├── 侧翼包抄 AI（APPROACHING → FLANKING 两阶段）
├── 敌方武器射速计时 + Raycast
├── 敌方武器命中：HealthSystem.ApplyDamage(playerInstanceId, damage)
└── 敌人生成位置：相对玩家位置圆弧分布
```

### CombatSystem 详细设计

#### 驾驶舱战斗状态机

```
状态：IDLE（CockpitScene 内无战斗）
触发 BeginCombat：
  → 状态 = COMBAT_ACTIVE
  → 生成 2 个敌方实例（EnemySystem.Spawn）
  → 订阅 HealthSystem.OnShipDying
  → 广播 CombatChannel.RaiseBegin()

胜条件（OnShipDying 且敌方全部 HP=0）：
  → 状态 = COMBAT_VICTORY
  → 销毁所有敌方实例
  → ShipDataModel.SetState(IN_COCKPIT)
  → 广播 CombatChannel.RaiseVictory(nodeId)
  → 状态 → IDLE

败条件（OnShipDying 且玩家 HP=0）：
  → 状态 = COMBAT_DEFEAT
  → ShipDataModel.Destroy()（不走 HealthSystem）
  → 广播 CombatChannel.RaiseDefeat(nodeId)
  → 状态 → IDLE
```

#### 武器射速计时器（帧率独立）

```csharp
// 玩家武器
_weaponFireTimer += Time.deltaTime;  // ⚠️ 用 Time.deltaTime，不用实时
if (_weaponFireTimer >= (1f / WEAPON_FIRE_RATE) && aimAngle <= FIRE_ANGLE_THRESHOLD) {
    FireWeapon();
    _weaponFireTimer = 0f;
}

// 敌方武器（EnemySystem 内同等逻辑）
_enemyFireTimer += Time.deltaTime;
if (_enemyFireTimer >= (1f / WEAPON_FIRE_RATE) && enemyAimAngle <= FIRE_ANGLE_THRESHOLD) {
    enemyFireRaycast();
    _enemyFireTimer = 0f;
}
```

#### 命中检测（零 GC）

```csharp
// 玩家武器 Raycast
RaycastHit[] _hits = new RaycastHit[1];  // 预分配，类成员，不在循环内分配
int count = Physics.RaycastNonAlloc(
    fireOrigin,
    aimDirection,
    _hits,
    WEAPON_RANGE,
    enemyLayerMask);

if (count > 0) {
    string enemyId = _hits[0].collider.GetComponent<EnemyCollider>().InstanceId;
    HealthSystem.Instance.ApplyDamage(enemyId, BASE_DAMAGE, DamageType.KINETIC);
}

// 敌方武器 Raycast（EnemySystem 内）
int enemyHitCount = Physics.RaycastNonAlloc(
    enemyFireOrigin,
    enemyAimDirection,
    _enemyHits,  // 预分配
    WEAPON_RANGE,
    playerLayerMask);

if (enemyHitCount > 0) {
    HealthSystem.Instance.ApplyDamage(playerInstanceId, BASE_DAMAGE, DamageType.KINETIC);
}
```

#### 无人值守战斗结算（单帧同步）

```csharp
// 无人值守战斗在 StarMapScene 的 FleetDispatchSystem 内执行
// 不进入 CockpitScene，不触发 CombatSystem
void ResolveUnattended(int playerFleetSize, int enemyFleetSize, string nodeId) {
    int P = playerFleetSize;
    int E = enemyFleetSize;

    while (P > 0 && E > 0) {
        P -= 1;  // 每轮双方各损失 1 艘
        E -= 1;
    }

    if (E <= 0 && P > 0) {
        // 玩家胜利：节点状态更新，无飞船销毁
        StarMapSystem.OnCombatVictory(nodeId);
    } else {
        // 玩家失败：直接销毁，不经过 HealthSystem
        foreach (string shipId in GetPlayerShipsOnNode(nodeId)) {
            ShipDataModel.DestroyShip(shipId);  // 绕过 HealthSystem
        }
        StarMapSystem.OnCombatDefeat(nodeId);
    }
}
```

### CombatChannel SO Channel 定义

```csharp
// assets/data/channels/CombatChannel.asset（Tier 1 SO Channel）
[CreateAssetMenu(fileName = "CombatChannel", menuName = "Game/Channels/CombatChannel")]
public class CombatChannel : GameEvent<(string NodeId, CombatResult Result)> {
    // RaiseBegin() — 战斗开始（CockpitScene 完全加载后调用）
    // RaiseVictory(string nodeId) — 战斗胜利
    // RaiseDefeat(string nodeId) — 战斗失败
}

// CombatResult 枚举
public enum CombatResult { Victory, Defeat }
```

### 关键接口

| 接口 | 调用方 | 接收方 | 说明 |
|------|--------|--------|------|
| `HealthSystem.ApplyDamage(id, rawDamage, KINETIC)` | CombatSystem, EnemySystem | HealthSystem | 驾驶舱战斗伤害入口 |
| `HealthSystem.OnShipDying` | HealthSystem | CombatSystem | 订阅以检测战斗结束 |
| `EnemySystem.SpawnEnemy(blueprintId, position)` | CombatSystem | EnemySystem | 战斗开始时调用 |
| `EnemySystem.DespawnEnemy(instanceId)` | CombatSystem | EnemySystem | 战斗结束时调用 |
| `ShipDataModel.SetState(IN_COMBAT/IN_COCKPIT)` | CombatSystem | ShipDataModel | 战斗状态转换 |
| `ShipDataModel.Destroy()` | CombatSystem（败）, FleetDispatchSystem（无人值守） | ShipDataModel | 飞船销毁（无人值守绕过 HealthSystem） |
| `CombatChannel.RaiseBegin/Victory/Defeat()` | CombatSystem | StarMapSystem, ShipHUD | 跨场景广播 |
| `aim_angle` | CombatSystem | ShipControlSystem | 只读，武器自动触发条件 |

### CombatSystem 挂载位置

`CombatSystem` 挂载于 **CockpitScene**（与飞船控制器同一场景）。

理由：CockpitScene 按需加载/卸载，战斗系统仅在玩家进入驾驶舱后存在。无人值守战斗不实例化 CockpitScene，由 StarMapScene 内 FleetDispatchSystem 直接调用 `ShipDataModel.Destroy()`。

## Alternatives Considered

### Alternative 1: CombatSystem 集中所有逻辑
- **Description**: 伤害计算、敌人生成、敌 AI 全部内聚在 CombatSystem
- **Pros**: 最简单，单个类，无跨系统依赖
- **Cons**: 违反单一职责；敌 AI 逻辑混入战斗判定，难以独立测试；与 EnemySystem GDD 矛盾
- **Rejection Reason**: 违反 GDD enemy-system.md 的 E-1~E-5 规则，敌方实例应独立于 CombatSystem 管理

### Alternative 2: 伤害计算全委托 HealthSystem
- **Description**: 所有伤害计算（Hull 更新、死亡判定）全由 HealthSystem 处理，CombatSystem 只管「是否命中」
- **Pros**: HealthSystem 统一所有伤害入口
- **Cons**: HealthSystem 需要知道战斗上下文（是敌方打的还是玩家自己撞的）；无人值守绕过路径（U-4）需要 HealthSystem 做特判；OnShipDying 事件归属不清
- **Rejection Reason**: GDD H-2 规则明确「伤害必须经过 HealthSystem」，但 U-4 规则明确「无人值守绕过 HealthSystem」——两个规则并存，HealthSystem 内部需要 if/else 特判，反而不如外部分开更清晰

## Consequences

### Positive
- 职责清晰：CombatSystem 管战斗流程，HealthSystem 管生命值，EnemySystem 管敌方 AI
- 帧率独立：射速计时器用 `Time.deltaTime` 累加，不依赖实时间隔
- 零 GC：`Physics.RaycastNonAlloc` 和 `OverlapSphereNonAlloc` 预分配缓冲区
- 无人值守高效：单帧循环结算，无需场景加载
- 可测试：三个系统可独立单元测试，通过接口 mock 交互

### Negative
- 三个系统紧耦关于同一战斗上下文，需要共享 `playerInstanceId` 和 `nodeId`（通过 GameDataManager 或构造注入）
- HealthSystem 需要区分「驾驶舱伤害」（触发 OnShipDying）和「无人值守销毁」（不触发）
- CombatSystem 在 CockpitScene，EnemySystem 也在 CockpitScene，两个系统需 Scene 内通信（C# event Tier 2，非 SO Channel）

### Risks
- **风险 1**：若 HealthSystem 未同步实现（ADR 未 Accepted），CombatSystem 无法完成 ApplyDamage 调用绑定 → Block Story 实施
  - 缓解：ADR-0013 先 Proposed；等 HealthSystem ADR Accepted 后 Story 才 READY
- **风险 2**：`aim_angle` 从 ShipControlSystem 暴露的接口未定义 → 自动开火条件无法实现
  - 缓解：在 ship-control-system.md 内补充 `aim_angle` 属性暴露（或在 ADR-0013 内定义标准接口）
- **风险 3**：Android 低端设备 `ContinuousDynamic` CCD 性能开销高 → 200m 射程 Raycast 穿透
  - 缓解：先用 `Discrete` 检测；如穿透问题出现，改用 `Continuous`；真机验证

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| ship-combat-system.md §B-2 | 自动开火：aim_angle ≤ FIRE_ANGLE_THRESHOLD 时触发 | CombatSystem 每帧检测 `aim_angle`（只读），满足条件则 FireWeapon() |
| ship-combat-system.md §B-3 | 射速：_fireTimer += Time.deltaTime，达 1/WEAPON_FIRE_RATE 触发 | weapon_fire_rate_timer 公式实现，帧率独立 |
| ship-combat-system.md §B-4 | 命中：Physics.Raycast，命中调用 HealthSystem.ApplyDamage | RaycastNonAlloc 预分配，命中后 ApplyDamage(finalDamage, KINETIC) |
| ship-combat-system.md §U-2 | 无人值守：P/E 各减 1，循环直到一方归零 | FleetDispatchSystem 内同步循环，败方直接 DestroyShip() |
| ship-combat-system.md §U-4 | 无人值守失败路径：DestroyShip() 绕过 HealthSystem，不触发 OnShipDying | 专用路径，ShipDataModel.Destroy() 直接调用，不经过 HealthSystem |
| ship-combat-system.md §V-1/2/3 | 胜负条件：胜利→IN_COCKPIT，失败→DESTROYED | CombatSystem 监听 OnShipDying，根据死亡目标判定胜负，调用对应 ShipDataModel 状态转换 |
| ship-health-system.md §H-2 | ApplyDamage 接口 | CombatSystem/EnemySystem 均调用此接口 |
| ship-health-system.md §H-5 | OnShipDying 事件 | CombatSystem 订阅此事件以检测敌方/玩家死亡 |
| enemy-system.md §E-5 | 敌方 AI APPROACHING/FLANKING | EnemySystem 内部状态机，CombatSystem 提供目标位置查询接口 |

## Performance Implications

| 项目 | 影响 | 缓解 |
|------|------|------|
| **CPU（武器 Raycast）** | 每武器每帧 1 次 Raycast；2 武器=每帧 2 次 | LayerMask 过滤；`RaycastNonAlloc` 零分配 |
| **CPU（敌 AI 查询）** | APPROACHING 每帧 1 次 `OverlapSphereNonAlloc` | 预分配缓冲区；结果数量少（≤2 敌） |
| **CPU（无人值守循环）** | 最坏 O(min(P,E))，P/E ≤ 舰队上限 | 单帧同步，无 GC |
| **Memory** | 预分配 RaycastHit[1] + OverlapSphere Collider[10] | 类成员，循环复用 |
| **Load Time** | 战斗触发时 EnemySystem.Spawn 产生 2 个敌方实例 | 对象池预热 |

## Migration Plan

本 ADR 是全新系统，无现有代码迁移需求。

实施顺序：
1. 先创建 HealthSystem ADR（CombatSystem 依赖 ApplyDamage）
2. 再创建 EnemySystem ADR（CombatSystem 依赖敌人生成）
3. CombatSystem Story 基于本 ADR 实施
4. FleetDispatchSystem（无人值守结算）基于 ship-combat-system.md U-1~U-4 实施

## Validation Criteria

| 验证条件 | 验证方法 |
|----------|----------|
| 武器射速：1.0 发/秒，经过 60fps 60帧后恰好触发 60 次开火 | 单元测试：_fireTimer 累加验证 |
| RaycastNonAlloc 零 GC：1000 次 FireWeapon 调用，Profiler 无 GC Allocations | Profiler 内存标记 |
| 无人值守 P=3, E=2 → 玩家胜利（P=1, E=0）；P=2, E=3 → 玩家失败 | 单元测试：unattended_combat_result(P, E) |
| aim_angle > 15° 时不开火 | 集成测试：mock aim_angle，验证 FIRE_ANGLE_THRESHOLD=15° |
| 驾驶舱战斗败：ShipState = DESTROYED，CombatChannel.RaiseDefeat() 广播 | 集成测试 |
| 无人值守败：ShipDataModel.Destroy() 被调用，HealthSystem.OnShipDying 不触发 | 集成测试：验证 OnShipDying 订阅者未被调用 |

## Related Decisions

- [ADR-0001: Scene Management Architecture](adr-0001-scene-management-architecture.md) — ViewLayer + ShipDataModel 单一权威来源
- [ADR-0002: Event Communication Architecture](adr-0002-event-communication-architecture.md) — CombatChannel Tier 1 SO Channel 规范
- [ADR-0004: Data Model Architecture](adr-0004-data-model-architecture.md) — ShipDataModel 状态机接口
- [ADR-0003: Input System Architecture](adr-0003-input-system-architecture.md) — aim_angle 由 ShipControlSystem 计算
