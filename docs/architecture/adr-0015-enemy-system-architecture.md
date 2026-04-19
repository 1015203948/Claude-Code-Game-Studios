# ADR-0015: Enemy System Architecture

## Status
Accepted

## Date
2026-04-15

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | AI / Physics |
| **Knowledge Risk** | MEDIUM — Enemy AI behavior uses Update() loop + Quaternion.RotateTowards（无 post-cutoff API）；Physics.RaycastNonAlloc 和 OverlapSphereNonAlloc 来自 physics.md（pre-cutoff，verified）；无 post-cutoff physics API |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `docs/engine-reference/unity/modules/physics.md` |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | (1) OverlapSphereNonAlloc 在 APPROACHING 每帧调用时 Android 上无 GC 分配；(2) 敌方 Raycast 在 200m 射程内无穿透（ContinuousDynamic CCD）；(3) 2 个敌方实例同时 FLANKING 时无碰撞体重叠干扰；(4) DYING 状态 1.2s 计时器在战斗强制结束时（DespawnEnemy）被清除 |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0014（HealthSystem — ApplyDamage 接口）；ADR-0018（ShipControlSystem — aim_angle 只读属性，用于敌 AI 瞄准判定） |
| **Enables** | ADR-0013（CombatSystem — SpawnEnemy/DespawnEnemy 接口调用方）；Core Epic — 敌方系统和驾驶舱战斗完整闭环 |
| **Blocks** | Core Epic — CombatSystem 依赖本 ADR 定义的 SpawnEnemy/DespawnEnemy 接口 |
| **Ordering Note** | ADR-0014 应先于本 ADR Accepted；本 ADR 可与 ADR-0014 同时 Proposed |

## Context

### Problem Statement
EnemySystem 需要在战斗触发时生成 2 个敌方飞船实例（ai-0, ai-1），驱动侧翼包抄 AI（APPROACHING → FLANKING 两阶段），在满足角度条件时用 Raycast 射击玩家飞船（通过 HealthSystem.ApplyDamage），并在死亡或战斗结束时销毁实例。

### Constraints
- **MVP 固定数量**：恰好 2 个敌方实例，不支持动态波次（M-E-1）
- **零 GC**：每帧物理查询必须预分配缓冲区（`OverlapSphereNonAlloc`，`RaycastNonAlloc`）
- **帧率独立**：计时器用 `Time.deltaTime` 累加，不依赖实时间隔
- **同场景通信**：EnemySystem 与 CombatSystem 同在 CockpitScene，使用 C# event Tier 2（非 SO Channel）
- **Player 位置查询**：APPROACHING 每帧用 `OverlapSphereNonAlloc` 查询玩家位置（零 GC）

### Requirements
- `SpawnEnemy(blueprintId, position)` → `instanceId`：生成 2 个敌方实例（各独立 AI 状态机）
- `DespawnEnemy(instanceId)`：销毁指定实例（Dying 状态直接销毁，跳过 1.2s VFX）
- 敌方武器射速：`_fireTimer += Time.deltaTime`，达阈值触发 `Physics.RaycastNonAlloc`
- APPROACHING：直线向玩家移动，速度 `ENEMY_MOVE_SPEED`，旋转 `ENEMY_TURN_SPEED`
- FLANKING：弧形路径到玩家侧后方，条件满足时开火
- 命中玩家：调用 `HealthSystem.ApplyDamage(playerId, BASE_DAMAGE, KINETIC)`

## Decision

### EnemySystem 架构

```
EnemySystem（MonoBehaviour 单例，CockpitScene）
├── EnemyInstance Registry（Dictionary<string, EnemyAIController>）
├── SpawnEnemy(blueprintId, position) → instanceId
├── DespawnEnemy(instanceId)
├── Update() — 驱动所有活跃实例的 AI 状态机
└── 订阅 HealthSystem.OnShipDying — 敌方死亡时触发 DYING 状态

EnemyAIController（MonoBehaviour，每个敌方实例一个）
├── EnemyInstance Data:
│   ├── InstanceId, BlueprintId, CurrentHull, MaxHull
│   ├── AiState: SPAWNING / APPROACHING / FLANKING / DYING
│   ├── FireTimer, RandomDelay
│   └── TargetPlayerInstanceId
├── UpdateAI() — 状态机逻辑
└── 物理查询：OverlapSphereNonAlloc（玩家位置）+ RaycastNonAlloc（射击）

Prefabs:
└── EnemyShipPrefab — 基于 generic_v1 蓝图，橙红色材质
```

### EnemyInstance 数据模型

```csharp
public enum EnemyAiState { SPAWNING, APPROACHING, FLANKING, DYING }

public class EnemyInstance {
    public string InstanceId;        // "enemy_[uuid]"
    public string BlueprintId;       // "generic_v1"（MVP 固定）
    public float CurrentHull;        // 独立维护，不经过 HealthSystem
    public float MaxHull;           // 从蓝图读取
    public EnemyAiState AiState;
    public string TargetPlayerId;   // 玩家飞船 InstanceId
    public float FireTimer;         // 武器射速计时器
    public float RandomDelay;       // SPAWNING → APPROACHING 延迟
    public float DyingTimer;        // DYING 状态 1.2s 计时
    public Vector3 SpawnPosition;   // 初始生成位置
}
```

### SpawnEnemy / DespawnEnemy

```csharp
// EnemySystem.cs
private readonly Dictionary<string, EnemyAIController> _registry = new Dictionary<string, EnemyAIController>();

public string SpawnEnemy(string blueprintId, Vector3 position) {
    // 1. 创建 GameObject（从 Prefab 实例化）
    var go = Object.Instantiate(_enemyPrefab, position, Quaternion.identity);
    var controller = go.GetComponent<EnemyAIController>();

    // 2. 初始化 EnemyInstance
    string instanceId = $"enemy_{Guid.NewGuid():N}";
    controller.Initialize(instanceId, blueprintId, position);

    // 3. 注册
    _registry[instanceId] = controller;
    return instanceId;
}

public void DespawnEnemy(string instanceId) {
    if (!_registry.TryGetValue(instanceId, out var controller)) {
        return; // 静默忽略不存在的实例
    }

    // 如果正在 DYING 状态，跳过 1.2s VFX 等待，直接销毁
    controller.ForceDespawn();
    _registry.Remove(instanceId);
}
```

### AI 状态机（UpdateAI）

```csharp
// EnemyAIController.cs — 每帧调用
void UpdateAI() {
    switch (AiState) {
        case EnemyAiState.SPAWNING:
            _spawnTimer += Time.deltaTime;
            if (_spawnTimer >= RandomDelay) {
                AiState = EnemyAiState.APPROACHING;
            }
            break;

        case EnemyAiState.APPROACHING:
            // 直线向玩家移动
            Vector3 toPlayer = GetPlayerPosition() - transform.position;
            Quaternion targetRot = Quaternion.LookRotation(toPlayer);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRot, ENEMY_TURN_SPEED * Time.deltaTime);
            transform.position += transform.forward * ENEMY_MOVE_SPEED * Time.deltaTime;

            // 距离检测 → 切换 FLANKING
            if (toPlayer.magnitude <= FLANK_ENGAGE_RANGE) {
                AiState = EnemyAiState.FLANKING;
                ComputeFlankingTarget(); // 计算弧形目标点
            }
            break;

        case EnemyAiState.FLANKING:
            // 弧形路径向侧后方移动
            Vector3 toFlankTarget = _flankTarget - transform.position;
            Quaternion flankRot = Quaternion.LookRotation(toFlankTarget);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, flankRot, ENEMY_TURN_SPEED * Time.deltaTime);
            transform.position += transform.forward * ENEMY_MOVE_SPEED * Time.deltaTime;

            // 武器射击
            FireTimer += Time.deltaTime;
            if (FireTimer >= (1f / WEAPON_FIRE_RATE) && EvaluateAimAngle() <= FIRE_ANGLE_THRESHOLD) {
                FireRaycast();
                FireTimer = 0f;
            }
            break;

        case EnemyAiState.DYING:
            _dyingTimer += Time.deltaTime;
            if (_dyingTimer >= 1.2f) {
                EnemySystem.Instance.DespawnEnemy(InstanceId); // 触发 EnemySystem 移除
            }
            break;
    }
}
```

### 物理查询（零 GC）

```csharp
// 类成员预分配（每 EnemyAIController）
private static readonly Collider[] _playerQueryBuffer = new Collider[10];
private static readonly RaycastHit[] _fireHitBuffer = new RaycastHit[1];

Vector3 GetPlayerPosition() {
    // 使用OverlapSphereNonAlloc，零GC
    int count = Physics.OverlapSphereNonAlloc(
        transform.position,
        FLANK_ENGAGE_RANGE * 2f,  // 较大范围确保能查询到玩家
        _playerQueryBuffer,
        playerLayerMask);

    for (int i = 0; i < count; i++) {
        if (_playerQueryBuffer[i].CompareTag("PlayerShip")) {
            return _playerQueryBuffer[i].transform.position;
        }
    }
    return Vector3.zero;
}

void FireRaycast() {
    Vector3 fireOrigin = transform.position + transform.forward * 1f; // 前方 1m
    int hitCount = Physics.RaycastNonAlloc(
        fireOrigin,
        transform.forward,
        _fireHitBuffer,
        WEAPON_RANGE,
        playerLayerMask);

    if (hitCount > 0) {
        // 命中玩家：通过 HealthSystem 造成伤害
        HealthSystem.Instance.ApplyDamage(
            TargetPlayerId,
            BASE_DAMAGE,
            DamageType.KINETIC);
    }
}

float EvaluateAimAngle() {
    // 计算当前前向与玩家方向的夹角
    Vector3 toPlayer = (GetPlayerPosition() - transform.position).normalized;
    return Vector3.Angle(transform.forward, toPlayer);
}
```

### 生成位置规则（E-3）

```csharp
Vector3 ComputeSpawnPosition(int index, Vector3 playerPosition) {
    float baseAngle = Random.Range(0f, 360f);
    float angleOffset = index == 0 ? 0f : Random.Range(90f, 270f);
    float angle = baseAngle + angleOffset;
    float distance = SPAWN_RADIUS; // 150m

    Vector3 offset = new Vector3(
        Mathf.Cos(angle * Mathf.Deg2Rad) * distance,
        0f,
        Mathf.Sin(angle * Mathf.Deg2Rad) * distance);

    Vector3 spawnPos = playerPosition + offset;

    // 碰撞检测：如果与现有几何体重叠，沿角度方向平移 10m，重试最多 3 次
    for (int retry = 0; retry < 3; retry++) {
        if (!Physics.CheckSphere(spawnPos, 5f, obstacleLayerMask)) {
            return spawnPos;
        }
        spawnPos += new Vector3(
            Mathf.Cos(angle * Mathf.Deg2Rad) * 10f,
            0f,
            Mathf.Sin(angle * Mathf.Deg2Rad) * 10f);
    }
    return spawnPos; // 最终结果，即使有重叠
}
```

### 与 HealthSystem 的交互

```csharp
// EnemyAIController.cs
void Start() {
    // 订阅 HealthSystem.OnShipDying — 敌方死亡时进入 DYING 状态
    HealthSystem.Instance.OnShipDying += OnEnemyDying;
}

void OnEnemyDying(string instanceId) {
    if (instanceId == InstanceId && AiState != EnemyAiState.DYING) {
        AiState = EnemyAiState.DYING;
        _dyingTimer = 0f;
    }
}

void OnDestroy() {
    if (HealthSystem.Instance != null) {
        HealthSystem.Instance.OnShipDying -= OnEnemyDying;
    }
}
```

### 关键常量（来自 GDD Tuning Knobs）

| 常量 | 值 | 说明 |
|------|-----|------|
| `ENEMY_MOVE_SPEED` | 15 m/s | 移动速度 |
| `ENEMY_TURN_SPEED` | 90°/s | 旋转速度上限 |
| `FLANK_ENGAGE_RANGE` | 80 m | 触发 FLANKING 的距离阈值 |
| `SPAWN_RADIUS` | 150 m | 生成位置与玩家的距离 |
| `FLANK_OFFSET` | 30 m | 弧形目标点偏移量 |
| `FIRE_ANGLE_THRESHOLD` | 15° | 自动开火角度阈值 |
| `WEAPON_FIRE_RATE` | 1.0 shots/sec | 射速 |
| `WEAPON_RANGE` | 200 m | Raycast 最大距离 |
| `DYING_DURATION` | 1.2 s | DYING 状态 VFX 持续时间 |

## Alternatives Considered

### Alternative 1: EnemyInstance 纯数据结构 + EnemySystem 批量驱动
- **Description**: EnemyInstance 是纯 C# class，EnemySystem 在自己的 Update() 中用 for 循环批量处理所有实例逻辑；GameObject 仅负责渲染（无 MonoBehaviour AI）
- **Pros**: 内存布局紧凑，批量处理缓存友好；所有 AI 逻辑集中，易于调试
- **Cons**: 每个实例需要手动同步 Transform（位置更新 → 游戏对象移动），代码更复杂；Prefab 上的渲染组件需要单独寻址
- **Rejection Reason**: MVP 阶段 GameObject + MonoBehaviour 更简单直观；额外内存开销可控（仅 2 个实例）；AI 状态机封装在 MonoBehaviour 内，生命周期清晰

### Alternative 2: 协程驱动状态机
- **Description**: AI 状态转换用 `StartCoroutine` 协程实现，不用 `Update()` 每帧 switch
- **Pros**: 代码更线性，状态逻辑容易阅读
- **Cons**: 协程无法每帧访问 `Time.deltaTime`（需要额外状态传递）；SPAWNING 随机延迟和 DYING 倒计时更适合计时器模式；移动逻辑每帧需要更新位置，协程反而不方便
- **Rejection Reason**: APPROACHING/FLANKING 需要每帧移动，Update() 更自然；SPAWNING/DYING 的计时器在 Update() 中管理也很简单

## Consequences

### Positive
- 每个敌方实例独立 MonoBehaviour，生命周期清晰（SPAWNING → APPROACHING → FLANKING → DYING）
- 预分配物理缓冲区（OverlapSphereNonAlloc + RaycastNonAlloc），零 GC
- 与 HealthSystem 通过 C# event 解耦，测试方便
- DYING 状态 1.2s VFX 等待后自动 Despawn，自动清理

### Negative
- EnemyAIController 需要订阅 HealthSystem.OnShipDying，OnDestroy 需取消订阅（怕遗漏）
- 2 个实例各自有独立的 OverlapSphereNonAlloc 调用（共 2 次/帧）
- EnemyInstance.CurrentHull 独立维护，不经过 HealthSystem（MVP 设计如此）

### Risks
- **风险 1**：FLANKING 阶段多个敌人同时向同一目标点移动 → 重叠碰撞
  - 缓解：FLANK_OFFSET 在生成时随机化（±10° 扰动）；碰撞体用简单 SphereCollider
- **风险 2**：DespawnEnemy 在 DYING 1.2s 期间被调用 → VFX 截断
  - 缓解：EC-5 规定立即销毁，不等待（DyingTimer 重置为 0）
- **风险 3**：OverlapSphereNonAlloc 每帧 2 次 → Android 低端机性能
  - 缓解：缓冲区数组很小（10 elements）；结果数量少（2 enemies）

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| enemy-system.md §E-1 | EnemyInstance 数据模型：InstanceId, AiState, CurrentHull, FireTimer | EnemyInstance class 完全实现 E-1 所有字段 |
| enemy-system.md §E-2 | MVP 固定 2 个实例（ai-0, ai-1） | SpawnEnemy 调用 2 次，index 区分 |
| enemy-system.md §E-3 | 生成位置：半径 150m，角间距 ≥ 90° | ComputeSpawnPosition() 实现 E-3 规则 |
| enemy-system.md §E-4 | SPAWNING 随机延迟 3-5s | RandomDelay 在 Initialize 时随机分配 |
| enemy-system.md §E-5 | APPROACHING：直线向玩家，FLANKING：弧形路径 | UpdateAI() 中 APPROACHING/FLANKING 分支实现 E-5 |
| enemy-system.md §E-5 | FLANKING 阶段满足角度条件时开火 | FireRaycast() + EvaluateAimAngle() 实现 |
| enemy-system.md §FLANKING→DYING | HP ≤ 0 → DYING → 1.2s 后 Despawn | OnEnemyDying() → DYING → 1.2s → DespawnEnemy |
| enemy-system.md §EC-5 | DespawnEnemy 对 DYING 中实例立即销毁 | ForceDespawn() 跳过 DyingTimer |
| ship-combat-system.md §B-4 | 敌方武器命中调用 HealthSystem.ApplyDamage | FireRaycast() → HealthSystem.Instance.ApplyDamage |
| ship-combat-system.md §B-5 | 敌方侧翼包抄行为 | APPROACHING → FLANKING 状态机实现 |

## Performance Implications

| 项目 | 影响 | 缓解 |
|------|------|------|
| **CPU** | APPROACHING 每帧 2× OverlapSphereNonAlloc + 2× RaycastNonAlloc | 预分配缓冲区，零 GC；magnitude 比较代替 Distance() |
| **CPU** | FLANKING 每帧 EvaluateAimAngle + RaycastNonAlloc | 同上 |
| **Memory** | 2× EnemyAIController + GameObject + Renderer | 可接受（仅 2 实例） |
| **Load Time** | 2 个 Prefab 实例化 | 体积小（generic_v1 简单模型） |

## Migration Plan

本 ADR 是全新系统，无现有代码迁移。

实施顺序：
1. 创建 `EnemyAIController.cs`（MonoBehaviour Prefab）
2. 创建 `EnemySystem.cs`（单例，Registry 管理）
3. 创建 Prefab `EnemyShipPrefab.prefab`（基于 generic_v1）
4. 实现 SpawnEnemy/DespawnEnemy
5. 实现 UpdateAI() 状态机（4 个状态）
6. 实现物理查询（OverlapSphereNonAlloc + RaycastNonAlloc）
7. HealthSystem.OnShipDying 订阅集成
8. 集成测试：验证 Spawn → APPROACHING → FLANKING → DYING → Despawn 完整生命周期

## Validation Criteria

| 验证条件 | 验证方法 |
|----------|----------|
| SpawnEnemy × 2 → 2 个不同 InstanceId，角间距 ≥ 90° | 单元测试 |
| SPAWNING 随机延迟 3-5s 后进入 APPROACHING | 单元测试（mock Time.deltaTime） |
| APPROACHING 距离 ≤ 80m → 进入 FLANKING | 单元测试 |
| FLANKING aim_angle ≤ 15° → 触发 FireRaycast | 单元测试（mock aim_angle） |
| HP ≤ 0 → OnShipDying → DYING → 1.2s → DespawnEnemy | 集成测试 |
| DespawnEnemy 对 DYING 实例立即销毁（跳过 1.2s） | 集成测试 |
| Physics.RaycastNonAlloc 零 GC（1000 次调用 Profiler 无分配） | 性能测试 |
| OverlapSphereNonAlloc 零 GC | 性能测试 |

## Related Decisions

- [ADR-0013: Combat System Architecture](adr-0013-combat-system-architecture.md) — SpawnEnemy/DespawnEnemy 调用方，aim_angle 只读
- [ADR-0014: Health System Architecture](adr-0014-health-system-architecture.md) — ApplyDamage 调用，OnShipDying 订阅
