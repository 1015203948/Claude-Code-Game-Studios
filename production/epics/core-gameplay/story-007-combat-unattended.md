# Story 007: CombatSystem — Unattended Combat U-4

> **Epic**: core-gameplay
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Manifest Version**: 2026-04-14

## Context

**GDD**: `design/gdd/ship-combat-system.md`
**Requirement**: `TR-combat-004`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0013: Combat System Architecture
**ADR Decision Summary**: 无人值守战斗在 FleetDispatchSystem 内执行（不在 CockpitScene）；每轮 P--, E--，循环直到一方归零；败方直接调用 ShipDataModel.DestroyShip()，绕过 HealthSystem，不触发 OnShipDying。

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: 纯逻辑实现，无 Unity 物理 API。

**Control Manifest Rules (this layer)**:
- Required: U-4 路径绕过 HealthSystem
- Forbidden: 无人值守战斗不实例化 CockpitScene
- Guardrail: 单帧同步完成，无 GC

---

## Acceptance Criteria

*From GDD `design/gdd/ship-combat-system.md` U-2~U-4, unattended_combat_result formula:*

- [ ] P=3, E=2 → 返回 VICTORY（P=1, E=0）；调用 StarMapSystem.OnCombatVictory(nodeId)
- [ ] P=1, E=3 → 返回 DEFEAT；直接调用 ShipDataModel.DestroyShip()（绕过 HealthSystem）；OnShipDying 未触发
- [ ] P=1, E=1 → 返回 DEFEAT（平局保守判定）
- [ ] 无人值守胜利后舰队状态 → DOCKED（占领节点）
- [ ] 无人值守失败后 ShipDataModel.DestroyShip 调用，HealthSystem.OnShipDying 不触发

---

## Implementation Notes

*Derived from ADR-0013 Decision section (FleetDispatchSystem 内 ResolveUnattendedCombat):*

```csharp
// FleetDispatchSystem.ResolveUnattendedCombat — 无人值守战斗结算
void ResolveUnattendedCombat(string shipId, string nodeId) {
    int playerFleetSize = GetPlayerShipsOnNode(nodeId); // 当前节点玩家舰队数量
    int enemyFleetSize = 2; // MVP 固定 2 个敌方

    int P = playerFleetSize;
    int E = enemyFleetSize;

    while (P > 0 && E > 0) {
        P -= 1;
        E -= 1;
    }

    if (E <= 0 && P > 0) {
        // 胜利：占领节点
        StarMapSystem.OnCombatVictory(nodeId);
        var ship = ShipDataModel.GetShip(shipId);
        if (ship != null) ship.SetState(ShipState.DOCKED);
    } else {
        // 失败（U-4）：直接 DestroyShip，绕过 HealthSystem
        foreach (var s in GetPlayerShipsOnNode(nodeId)) {
            ShipDataModel.GetShip(s)?.Destroy(); // 不经过 HealthSystem
        }
        StarMapSystem.OnCombatDefeat(nodeId);
    }
}
```

注意：此逻辑在 FleetDispatchSystem（StarMapScene）中实现，不在 CombatSystem（CockpitScene）中。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 004: 驾驶舱战斗状态机
- Story 015: FleetDispatch 的 ArrivedAtDestination → 触发 U-4

---

## QA Test Cases

*Written by qa-lead at story creation.*

- **AC-1**: unattended_combat_result(P=3, E=2) → VICTORY
  - Given: GetPlayerShipsOnNode("node-A") returns 3; enemyFleetSize = 2
  - When: ResolveUnattendedCombat runs
  - Then: loop runs 2 times (P=1, E=0); E<=0 && P>0 → OnCombatVictory called; ships set DOCKED; no DestroyShip

- **AC-2**: unattended_combat_result(P=1, E=3) → DEFEAT, U-4 path
  - Given: GetPlayerShipsOnNode("node-A") returns 1; enemyFleetSize = 3
  - When: ResolveUnattendedCombat runs
  - Then: loop runs 1 time (P=0, E=2); else branch → ShipDataModel.Destroy() called; HealthSystem.OnShipDying NOT called

- **AC-3**: P=1, E=1 → DEFEAT (tie goes to defeat)
  - Given: P=1, E=1
  - When: loop runs once (P=0, E=0)
  - Then: else branch (E<=0 is true but P<=0 also true) → DEFEAT

- **AC-4**: U-4 bypasses HealthSystem
  - Given: ShipDataModel.Destroy() is called for unattended defeat
  - When: HealthSystem.OnShipDying is checked
  - Then: OnShipDying event was not fired (U-4 path skips HealthSystem entirely)

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/combat/unattended_combat_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: FleetDispatchSystem DispatchOrder creation (Story 012); ShipDataModel.Destroy() (Foundation)
- Unlocks: Story 015 (FleetDispatch enemy arrival integration)
