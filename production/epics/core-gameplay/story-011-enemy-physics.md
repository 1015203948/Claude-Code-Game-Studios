# Story 011: EnemySystem — Physics Queries Zero GC

> **Epic**: core-gameplay
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Manifest Version**: 2026-04-14

## Context

**GDD**: `design/gdd/enemy-system.md`
**Requirement**: `TR-enemy-006`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0015: Enemy System Architecture
**ADR Decision Summary**: APPROACHING 每帧用 OverlapSphereNonAlloc 查询玩家位置（零 GC）；FLANKING 每帧用 RaycastNonAlloc 射击（零 GC）；预分配缓冲区为类成员。

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: Physics.RaycastNonAlloc 和 OverlapSphereNonAlloc 来自 physics.md pre-cutoff；PhysX 5.1 稳定性改进已知。

**Control Manifest Rules (this layer)**:
- Required: Collider[10] 和 RaycastHit[1] 预分配为类 static readonly 成员
- Forbidden: 禁止每帧 new Collider[] 或 new RaycastHit[]
- Guardrail: 1000 次调用 Profiler 无 GC Allocations

---

## Acceptance Criteria

*From GDD `design/gdd/enemy-system.md` E-5 physics queries:*

- [ ] GetPlayerPosition() 使用 Physics.OverlapSphereNonAlloc，预分配缓冲区
- [ ] FireRaycast() 使用 Physics.RaycastNonAlloc，预分配 RaycastHit[1]
- [ ] 1000 次调用后 Profiler 无 GC Allocations
- [ ] 碰撞体重叠时沿角度方向平移 10m，重试最多 3 次

---

## Implementation Notes

*Derived from ADR-0015 Decision section:*

```csharp
// 类 static readonly 预分配（零 GC）
private static readonly Collider[] _playerQueryBuffer = new Collider[10];
private static readonly RaycastHit[] _fireHitBuffer = new RaycastHit[1];

Vector3 GetPlayerPosition() {
    int count = Physics.OverlapSphereNonAlloc(
        transform.position,
        FLANK_ENGAGE_RANGE * 2f, // 较大范围确保能查询到玩家
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
    Vector3 fireOrigin = transform.position + transform.forward * 1f;
    int hitCount = Physics.RaycastNonAlloc(
        fireOrigin,
        transform.forward,
        _fireHitBuffer,
        WEAPON_RANGE,
        playerLayerMask);

    if (hitCount > 0) {
        HealthSystem.Instance.ApplyDamage(
            TargetPlayerId,
            BASE_DAMAGE,
            DamageType.KINETIC);
    }
}
```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 010: AI 状态机 UpdateAI() 循环调用 GetPlayerPosition 和 FireRaycast
- Story 006: CombatSystem 的 FireWeapon Raycast（独立路径）

---

## QA Test Cases

*Written by qa-lead at story creation.*

- **AC-1**: OverlapSphereNonAlloc zero allocation over 1000 calls
  - Given: EnemyAIController with pre-allocated _playerQueryBuffer
  - When: GetPlayerPosition() is called 1000 times
  - Then: Unity Profiler shows 0 GC Allocations

- **AC-2**: RaycastNonAlloc zero allocation over 1000 calls
  - Given: pre-allocated _fireHitBuffer
  - When: FireRaycast() is called 1000 times
  - Then: Unity Profiler shows 0 GC Allocations

- **AC-3**: GetPlayerPosition returns correct player position
  - Given: PlayerShip at world position (100, 0, 0); Enemy at (0, 0, 0)
  - When: GetPlayerPosition() is called
  - Then: returns (100, 0, 0)
  - Edge cases: no player in range (returns Vector3.zero)

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/enemy/physics_query_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 009 (SpawnEnemy); Story 006 (FireRaycast for enemy)
- Unlocks: Story 010 integration (AI state machine uses these queries)
