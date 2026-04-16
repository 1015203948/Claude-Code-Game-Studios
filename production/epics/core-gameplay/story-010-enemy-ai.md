# Story 010: EnemySystem — AI State Machine (APPROACHING → FLANKING → DYING)

> **Epic**: core-gameplay
> **Status**: Ready
> **Layer**: Core
> **Type**: State Machine
> **Manifest Version**: 2026-04-14

## Context

**GDD**: `design/gdd/enemy-system.md`
**Requirement**: `TR-enemy-003`, `TR-enemy-004`, `TR-enemy-005`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0015: Enemy System Architecture
**ADR Decision Summary**: EnemyAIController.UpdateAI() 驱动 4 状态机（SPAWNING→APPROACHING→FLANKING→DYING）；FLANKING 阶段弧形绕至玩家侧后方，满足角度条件时开火。

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: Physics.RaycastNonAlloc pre-cutoff；Quaternion.RotateTowards 无 post-cutoff API。

**Control Manifest Rules (this layer)**:
- Required: APPROACHING 不开火（给玩家战术读取窗口）
- Forbidden: APPROACHING 期间禁止 FLANKING 弧形逻辑
- Guardrail: 快速移动目标 CCD 防穿透（ContinuousDynamic）

---

## Acceptance Criteria

*From GDD `design/gdd/enemy-system.md` E-5, F-3~F-4 formulas:*

- [ ] SPAWNING：RandomDelay 计时器到期 → AiState → APPROACHING
- [ ] APPROACHING：直线向玩家移动（ENEMY_MOVE_SPEED=15m/s）；每帧距离检测，距离 ≤ 80m → FLANKING
- [ ] FLANKING：弧形路径向玩家侧后方（FLANK_OFFSET=30m）；满足 aimAngle ≤ 15° 且 FireTimer ≥ 1/WEAPON_FIRE_RATE → 开火
- [ ] DYING：CurrentHull ≤ 0 → AiState → DYING；1.2s 后自动 DespawnEnemy()
- [ ] HealthSystem.OnShipDying 订阅：敌方死亡时进入 DYING 状态

---

## Implementation Notes

*Derived from ADR-0015 Decision section:*

```csharp
void UpdateAI() {
    switch (AiState) {
        case EnemyAiState.SPAWNING:
            _spawnTimer += Time.deltaTime;
            if (_spawnTimer >= RandomDelay) AiState = EnemyAiState.APPROACHING;
            break;

        case EnemyAiState.APPROACHING:
            Vector3 toPlayer = GetPlayerPosition() - transform.position;
            Quaternion targetRot = Quaternion.LookRotation(toPlayer);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, ENEMY_TURN_SPEED * Time.deltaTime);
            transform.position += transform.forward * ENEMY_MOVE_SPEED * Time.deltaTime;
            if (toPlayer.magnitude <= FLANK_ENGAGE_RANGE) AiState = EnemyAiState.FLANKING;
            break;

        case EnemyAiState.FLANKING:
            Vector3 toFlankTarget = _flankTarget - transform.position;
            Quaternion flankRot = Quaternion.LookRotation(toFlankTarget);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, flankRot, ENEMY_TURN_SPEED * Time.deltaTime);
            transform.position += transform.forward * ENEMY_MOVE_SPEED * Time.deltaTime;
            FireTimer += Time.deltaTime;
            if (FireTimer >= (1f / WEAPON_FIRE_RATE) && EvaluateAimAngle() <= FIRE_ANGLE_THRESHOLD) {
                FireRaycast();
                FireTimer = 0f;
            }
            break;

        case EnemyAiState.DYING:
            _dyingTimer += Time.deltaTime;
            if (_dyingTimer >= 1.2f) EnemySystem.Instance.DespawnEnemy(InstanceId);
            break;
    }
}

// ComputeFlankingTarget（E-5）
void ComputeFlankingTarget() {
    Vector3 playerForward = GetPlayerTransform().forward;
    Vector3 playerRight = GetPlayerTransform().right;
    float offsetX = (this.InstanceId.Contains("0")) ? -5f : +5f;
    _flankTarget = GetPlayerPosition() + (-playerForward * FLANK_OFFSET) + (playerRight * offsetX);
}
```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 006: FireRaycast() 中 Physics.RaycastNonAlloc 命中检测
- Story 011: GetPlayerPosition() 的 OverlapSphereNonAlloc 零 GC 实现

---

## QA Test Cases

*Written by qa-lead at story creation.*

- **AC-1**: SPAWNING → APPROACHING transition after RandomDelay
  - Given: EnemyInstance with RandomDelay = 4.0s, AiState = SPAWNING
  - When: 4.0 seconds of game time elapse
  - Then: AiState transitions to APPROACHING
  - Edge cases: exactly at boundary (4.0s)

- **AC-2**: APPROACHING → FLANKING at FLANK_ENGAGE_RANGE = 80m
  - Given: AiState = APPROACHING; distance to player = 85m
  - When: one frame moves enemy to 79m
  - Then: AiState transitions to FLANKING; ComputeFlankingTarget called

- **AC-3**: FLANKING fires when aimAngle ≤ 15° and FireTimer ready
  - Given: AiState = FLANKING; aimAngle = 10°; FireTimer = 1.0
  - When: UpdateAI() is called
  - Then: FireRaycast() is called; FireTimer resets to 0
  - Edge cases: aimAngle = 15.01° (should not fire); FireTimer = 0.99 (should not fire)

- **AC-4**: Dying state 1.2s auto-despawn
  - Given: AiState = APPROACHING; CurrentHull reaches 0
  - When: HealthSystem.OnShipDying fires for this instance
  - Then: AiState → DYING; _dyingTimer = 0
  - When: 1.2 seconds elapse
  - Then: EnemySystem.DespawnEnemy called

---

## Test Evidence

**Story Type**: State Machine
**Required evidence**: `tests/unit/enemy/ai_state_machine_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 009 (SpawnEnemy, EnemyInstance data model); Story 006 (FireRaycast)
- Unlocks: Story 011 (physics queries optimization)
