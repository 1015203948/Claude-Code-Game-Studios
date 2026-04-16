# Story 004: CombatSystem — Cockpit State Machine

> **Epic**: core-gameplay
> **Status**: Ready
> **Layer**: Core
> **Type**: State Machine
> **Manifest Version**: 2026-04-14

## Context

**GDD**: `design/gdd/ship-combat-system.md`
**Requirement**: `TR-combat-001`, `TR-combat-006`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0013: Combat System Architecture
**ADR Decision Summary**: CombatSystem 管理驾驶舱战斗状态机 IN_COCKPIT → IN_COMBAT → IN_COCKPIT（胜）或 DESTROYED（败）；订阅 HealthSystem.OnShipDying 判定胜负。

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: PhysX 5.1 collision APIs (Physics.Raycast, OverlapSphereNonAlloc) 无变化；无 post-cutoff API。

**Control Manifest Rules (this layer)**:
- Required: 战斗状态机仅在 CockpitScene 内运行
- Forbidden: 无人值守战斗不实例化 CockpitScene，不触发 CombatSystem
- Guardrail: 武器 Raycast 预分配缓冲区

---

## Acceptance Criteria

*From GDD `design/gdd/ship-combat-system.md` B-5, scoped to this story:*

- [ ] BeginCombat() 调用：ShipState IN_COCKPIT → IN_COMBAT；生成 2 个敌方实例（EnemySystem.SpawnEnemy）；订阅 HealthSystem.OnShipDying；广播 CombatChannel.RaiseBegin()
- [ ] 胜条件（OnShipDying 且敌方全部 HP=0）：→ COMBAT_VICTORY → 销毁所有敌方实例 → ShipDataModel.SetState(IN_COCKPIT) → CombatChannel.RaiseVictory(nodeId) → 状态 → IDLE
- [ ] 败条件（OnShipDying 且玩家 HP=0）：→ COMBAT_DEFEAT → ShipDataModel.Destroy()（不走 HealthSystem）→ CombatChannel.RaiseDefeat(nodeId) → 状态 → IDLE
- [ ] OnShipDying 事件需判断是敌方死亡还是玩家死亡

---

## Implementation Notes

*Derived from ADR-0013 Decision section:*

```csharp
// CombatSystem 状态
enum CombatState { IDLE, COMBAT_ACTIVE, COMBAT_VICTORY, COMBAT_DEFEAT }
CombatState _state = CombatState.IDLE;

// BeginCombat
public void BeginCombat(string shipId, string nodeId) {
    _state = CombatState.COMBAT_ACTIVE;
    _playerShipId = shipId;
    _nodeId = nodeId;

    // 生成 2 个敌方实例
    _enemyIds = new List<string>();
    _enemyIds.Add(EnemySystem.Instance.SpawnEnemy("generic_v1", ComputeSpawnPos(0)));
    _enemyIds.Add(EnemySystem.Instance.SpawnEnemy("generic_v1", ComputeSpawnPos(1)));

    // 订阅
    HealthSystem.Instance.OnShipDying += OnAnyShipDying;

    // 广播
    CombatChannel.Instance.RaiseBegin(_nodeId);
}

// OnShipDying 回调
void OnAnyShipDying(string instanceId) {
    if (_state != CombatState.COMBAT_ACTIVE) return;

    if (IsPlayerShip(instanceId)) {
        // 玩家死亡 → 败
        _state = CombatState.COMBAT_DEFEAT;
        ShipDataModel.Destroy(_playerShipId);  // 绕过 HealthSystem
        CombatChannel.Instance.RaiseDefeat(_nodeId);
    } else if (_enemyIds.Contains(instanceId)) {
        // 敌方死亡 → 检查是否全灭
        _enemyIds.Remove(instanceId);
        if (_enemyIds.Count == 0) {
            // 胜利
            _state = CombatState.COMBAT_VICTORY;
            foreach (var eid in _enemyIds) EnemySystem.Instance.DespawnEnemy(eid);
            ShipDataModel.SetState(_playerShipId, ShipState.IN_COCKPIT);
            CombatChannel.Instance.RaiseVictory(_nodeId);
        }
    }
    _state = CombatState.IDLE;
}
```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 005: 武器射速计时器 + 自动开火
- Story 006: Raycast 命中检测
- Story 008: CombatChannel SO Channel 定义

---

## QA Test Cases

*Written by qa-lead at story creation.*

- **AC-1**: BeginCombat transitions to COMBAT_ACTIVE
  - Given: CombatSystem is IDLE
  - When: BeginCombat("ship-1", "node-A") is called
  - Then: _state = COMBAT_ACTIVE; EnemySystem.SpawnEnemy called twice; HealthSystem.OnShipDying subscribed; CombatChannel.RaiseBegin called

- **AC-2**: Victory when all enemies destroyed and player alive
  - Given: COMBAT_ACTIVE; player hull > 0; enemy-0 HP reaches 0 (OnShipDying fired); enemy-1 already HP=0
  - When: OnAnyShipDying("enemy-0") is called
  - Then: state → COMBAT_VICTORY; ShipDataModel.SetState(IN_COCKPIT); CombatChannel.RaiseVictory called; both enemies despawned; state → IDLE

- **AC-3**: Defeat when player hull reaches 0
  - Given: COMBAT_ACTIVE; player hull = 0 (OnShipDying fired for player ship)
  - When: OnAnyShipDying("ship-1") is called
  - Then: state → COMBAT_DEFEAT; ShipDataModel.Destroy("ship-1") called (not via HealthSystem); CombatChannel.RaiseDefeat called; state → IDLE

- **AC-4**: OnShipDying ignored when not in COMBAT_ACTIVE
  - Given: CombatSystem is IDLE
  - When: OnAnyShipDying("ship-1") is called
  - Then: no state change; no channel broadcast

---

## Test Evidence

**Story Type**: State Machine
**Required evidence**: `tests/unit/combat/combat_state_machine_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002 (HealthSystem.OnShipDying working); EnemySystem spawned (Story 009); CombatChannel exists
- Unlocks: Story 005 (fire rate), Story 006 (Raycast), Story 008 (channel integration)
