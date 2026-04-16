# Story 009: EnemySystem — SpawnEnemy × 2 + Position

> **Epic**: core-gameplay
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Manifest Version**: 2026-04-14

## Context

**GDD**: `design/gdd/enemy-system.md`
**Requirement**: `TR-enemy-001`, `TR-enemy-002`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0015: Enemy System Architecture
**ADR Decision Summary**: SpawnEnemy(blueprintId, position) 生成 2 个敌方实例；生成位置半径 150m，角间距 ≥ 90°；SPAWNING 状态带独立 RandomDelay ∈ [3s, 5s]。

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: Physics query APIs (OverlapSphereNonAlloc, RaycastNonAlloc) pre-cutoff；无 post-cutoff API。

**Control Manifest Rules (this layer)**:
- Required: 预分配 OverlapSphereNonAlloc Collider[10] 和 RaycastHit[1] 缓冲区
- Forbidden: EnemyInstance.CurrentHull 不经过 HealthSystem
- Guardrail: 2 个实例各自独立 RandomDelay

---

## Acceptance Criteria

*From GDD `design/gdd/enemy-system.md` E-1~E-4, F-1 formula:*

- [ ] SpawnEnemy × 2 调用 → 2 个不同 InstanceId（格式 "enemy_[uuid]"）
- [ ] 两个实例角间距 ≥ 90°（SPAWN_RADIUS = 150m）
- [ ] ai-0 RandomDelay ∈ [3s, 5s]；ai-1 独立 RandomDelay ∈ [3s, 5s]
- [ ] EnemyInstance 数据模型完整：InstanceId, BlueprintId, CurrentHull, MaxHull, AiState, FireTimer, RandomDelay, TargetPlayerId
- [ ] SPAWNING 状态：保持静止，不开火，不执行距离检测

---

## Implementation Notes

*Derived from ADR-0015 Decision section:*

```csharp
public string SpawnEnemy(string blueprintId, Vector3 position) {
    var go = Object.Instantiate(_enemyPrefab, position, Quaternion.identity);
    var controller = go.GetComponent<EnemyAIController>();

    string instanceId = $"enemy_{Guid.NewGuid():N}";
    controller.Initialize(instanceId, blueprintId, position, _playerShipId);

    _registry[instanceId] = controller;
    return instanceId;
}

// ComputeSpawnPosition（ADR-0015）
Vector3 ComputeSpawnPosition(int index, Vector3 playerPosition) {
    float baseAngle = Random.Range(0f, 360f);
    float angleOffset = index == 0 ? 0f : Random.Range(90f, 270f);
    float angle = baseAngle + angleOffset;
    float distance = SPAWN_RADIUS; // 150m

    Vector3 offset = new Vector3(
        Mathf.Cos(angle * Mathf.Deg2Rad) * distance,
        0f,
        Mathf.Sin(angle * Mathf.Deg2Rad) * distance);
    return playerPosition + offset;
}

// EnemyAIController.Initialize
void Initialize(string instanceId, string blueprintId, Vector3 spawnPos, string playerId) {
    InstanceId = instanceId;
    BlueprintId = blueprintId;
    CurrentHull = 100f;
    MaxHull = 100f;
    AiState = EnemyAiState.SPAWNING;
    FireTimer = 0f;
    RandomDelay = Random.Range(3f, 5f);
    TargetPlayerId = playerId;
    SpawnPosition = spawnPos;
}
```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 010: AI 状态机 APPROACHING → FLANKING → DYING
- Story 011: 物理查询零 GC

---

## QA Test Cases

*Written by qa-lead at story creation.*

- **AC-1**: SpawnEnemy × 2 produces distinct instance IDs
  - Given: player at (0,0,0)
  - When: EnemySystem.Instance.SpawnEnemy called twice with different index positions
  - Then: two distinct InstanceIds returned; both have format "enemy_[uuid]"

- **AC-2**: Angular separation ≥ 90°
  - Given: player at origin
  - When: two spawn positions computed
  - Then: angle between vectors ≥ 90°

- **AC-3**: RandomDelay in [3, 5] range for each instance
  - Given: two EnemyInstance created
  - When: RandomDelay values are checked
  - Then: both are in [3.0, 5.0] range; values are independent (different)
  - Edge cases: exactly 3.0 or 5.0 boundary values

- **AC-4**: SPAWNING state is stationary
  - Given: EnemyInstance in SPAWNING state
  - When: UpdateAI() is called for 10 frames
  - Then: transform.position does not change; no fire raycast sent

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/enemy/spawn_enemy_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Foundation (ShipDataModel); CombatSystem triggers spawn (Story 004)
- Unlocks: Story 010 (AI state machine), Story 011 (physics queries)
