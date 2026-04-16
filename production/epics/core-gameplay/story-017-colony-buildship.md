# Story 017: ColonySystem — BuildShip + DeductResources Atomicity

> **Epic**: core-gameplay
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Manifest Version**: 2026-04-14

## Context

**GDD**: `design/gdd/colony-system.md`
**Requirement**: `TR-colony-003`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0016: Colony & Building Architecture
**ADR Decision Summary**: BuildShip 前置检查 B-1/B-2/B-3；通过后原子扣费（30 ore + 15 energy）；ShipSystem.CreateShip；失败时回滚资源快照。

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: 纯逻辑。

**Control Manifest Rules (this layer)**:
- Required: 扣费 → 创建飞船为原子操作（任一失败回滚）
- Forbidden: 建造期间禁止 tick 修改资源
- Guardrail: 回滚后资源状态与扣费前完全一致

---

## Acceptance Criteria

*From GDD `design/gdd/colony-system.md` C-5:*

- [ ] ShipState = DOCKED 验证；HasShipyard=true 验证；资源充足验证
- [ ] 资源不足 → 返回 FailReason，不扣费
- [ ] 扣费 → ShipSystem.CreateShip 原子执行
- [ ] CreateShip 失败 → 回滚资源快照
- [ ] 建造成功后 OnShipBuilt(nodeId, shipInstanceId) 广播

---

## Implementation Notes

*Derived from ColonySystem GDD C-5:*

```csharp
public BuildShipResult BuildShip(string nodeId) {
    // B-1: 节点归属
    if (StarMapData.GetOwnership(nodeId) != Ownership.PLAYER)
        return new BuildShipResult { Success = false, FailReason = "NODE_NOT_PLAYER" };

    // B-2: HasShipyard
    if (!StarMapData.HasShipyard(nodeId))
        return new BuildShipResult { Success = false, FailReason = "NO_SHIPYARD" };

    // B-3: 资源充足
    const int SHIP_ORE_COST = 30;
    const int SHIP_ENERGY_COST = 15;
    if (_oreCurrent < SHIP_ORE_COST || _energyCurrent < SHIP_ENERGY_COST)
        return new BuildShipResult { Success = false, FailReason = "INSUFFICIENT_RESOURCES" };

    // 记录快照
    var snapshotOre = _oreCurrent;
    var snapshotEnergy = _energyCurrent;

    // 原子扣费
    _oreCurrent -= SHIP_ORE_COST;
    _energyCurrent -= SHIP_ENERGY_COST;

    // 创建飞船
    var shipResult = ShipSystem.Instance.CreateShip(nodeId);
    if (!shipResult.Success) {
        // 回滚
        _oreCurrent = snapshotOre;
        _energyCurrent = snapshotEnergy;
        return new BuildShipResult { Success = false, FailReason = shipResult.FailReason };
    }

    // 成功
    OnShipBuilt?.Invoke(nodeId, shipResult.InstanceId);
    return new BuildShipResult { Success = true, InstanceId = shipResult.InstanceId };
}
```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 016: ColonyManager tick 逻辑
- Story 018: BuildingSystem 建造

---

## QA Test Cases

*Written by qa-lead at story creation.*

- **AC-1**: Successful build deducts resources and creates ship
  - Given: _oreCurrent = 50, _energyCurrent = 30; nodeId has shipyard
  - When: BuildShip(nodeId) is called
  - Then: _oreCurrent = 20; _energyCurrent = 15; ShipSystem.CreateShip called; OnShipBuilt broadcast

- **AC-2**: Insufficient resources → no deduction, no creation
  - Given: _oreCurrent = 20, _energyCurrent = 10; nodeId has shipyard
  - When: BuildShip(nodeId) is called
  - Then: returns FailReason="INSUFFICIENT_RESOURCES"; _oreCurrent=20 unchanged; _energyCurrent=10 unchanged; CreateShip not called

- **AC-3**: CreateShip failure → rollback
  - Given: _oreCurrent = 50, _energyCurrent = 30; ShipSystem.CreateShip returns FailReason="SHIP_CREATION_FAILED"
  - When: BuildShip(nodeId) is called
  - Then: _oreCurrent = 50 (rolled back); _energyCurrent = 30 (rolled back); OnShipBuilt NOT broadcast

- **AC-4**: No shipyard → rejection
  - Given: nodeId has no shipyard
  - When: BuildShip(nodeId) is called
  - Then: returns FailReason="NO_SHIPYARD"; no resource deduction

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/colony/build_ship_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 016 (ColonyManager tick); ShipSystem.CreateShip (Foundation)
- Unlocks: Story 018 (BuildingSystem integration)
