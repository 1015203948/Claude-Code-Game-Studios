# Story 006: CombatSystem — Raycast Hit Detection (Zero GC)

> **Epic**: core-gameplay
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Manifest Version**: 2026-04-14

## Context

**GDD**: `design/gdd/ship-combat-system.md`
**Requirement**: `TR-combat-003`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0013: Combat System Architecture
**ADR Decision Summary**: Physics.RaycastNonAlloc 预分配 RaycastHit[1] 缓冲区，零 GC；命中后调用 HealthSystem.ApplyDamage(enemyId, BASE_DAMAGE, KINETIC)；快速移动目标使用 CollisionDetectionMode.ContinuousDynamic。

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: 验证：(1) Raycast hit detection on fast-moving enemy at 200m range — verify no tunneling with ContinuousDynamic; (2) OverlapSphereNonAlloc zero-GC in combat loop on Android

**Control Manifest Rules (this layer)**:
- Required: RaycastHit[1] 预分配为类成员，不在循环内分配
- Forbidden: 禁止每帧 Physics.Raycast（产生 GC）
- Guardrail: 200m 射程快速目标防穿透（ContinuousDynamic CCD）

---

## Acceptance Criteria

*From GDD `design/gdd/ship-combat-system.md` B-4, weapon_fire_rate_timer formula:*

- [ ] FireWeapon() 中使用 Physics.RaycastNonAlloc，RaycastHit[1] 预分配为类成员
- [ ] 命中碰撞体后调用 HealthSystem.ApplyDamage(enemyId, BASE_DAMAGE, DamageType.KINETIC)
- [ ] 武器射程 WEAPON_RANGE = 200m
- [ ] 1000 次 FireWeapon 调用后 Unity Profiler 无 GC Allocations
- [ ] 快速移动目标使用 ContinuousDynamic CCD 防穿透

---

## Implementation Notes

*Derived from ADR-0013 Decision section:*

```csharp
// 类成员预分配（零 GC）
private RaycastHit[] _hits = new RaycastHit[1];  // 预分配，不在循环内 new

void FireWeapon() {
    Vector3 fireOrigin = transform.position + transform.forward * 1f;
    int count = Physics.RaycastNonAlloc(
        fireOrigin,
        transform.forward,
        _hits,
        WEAPON_RANGE,
        enemyLayerMask);

    if (count > 0) {
        string enemyId = _hits[0].collider.GetComponent<EnemyCollider>().InstanceId;
        HealthSystem.Instance.ApplyDamage(enemyId, BASE_DAMAGE, DamageType.KINETIC);
    }
}

// 敌方预制体需有 EnemyCollider 组件
// 快速移动目标 CCD 在 prefab 上设置 Rigidbody.collisionDetectionMode
```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 005: FireWeapon 触发条件（_fireTimer + aimAngle）
- Story 009: EnemySystem EnemyCollider 组件

---

## QA Test Cases

*Written by qa-lead at story creation.*

- **AC-1**: RaycastNonAlloc zero allocation
  - Given: FireWeapon() is called 1000 times
  - When: Unity Profiler Memory panel is inspected
  - Then: 0 GC Allocations from Physics.RaycastNonAlloc path
  - Edge cases: count=0 (miss); count>0 (hit)

- **AC-2**: Hit detection calls ApplyDamage with correct params
  - Given: enemy ship collider in range at fireOrigin + forward * 200m
  - When: FireWeapon() is called
  - Then: HealthSystem.Instance.ApplyDamage called with (enemyId, 8f, DamageType.KINETIC)
  - Edge cases: no collider in range (count=0 → no ApplyDamage call)

- **AC-3**: WEAPON_RANGE = 200m respected
  - Given: enemy collider at exactly 200m from fireOrigin
  - When: FireWeapon() is called
  - Then: hit detected (Raycast goes to 200m)
  - Given: enemy collider at 201m
  - Then: not detected (beyond range)

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/combat/raycast_hit_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 005 (FireWeapon trigger); EnemySystem EnemyCollider component (Story 009)
- Unlocks: Integration with EnemySystem (Story 010)
