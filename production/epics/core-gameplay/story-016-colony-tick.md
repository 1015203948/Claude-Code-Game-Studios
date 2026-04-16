# Story 016: ColonySystem — Resource Tick + OnResourcesUpdated

> **Epic**: core-gameplay
> **Status**: Ready
> **Layer**: Core
> **Type**: Integration
> **Manifest Version**: 2026-04-14

## Context

**GDD**: `design/gdd/colony-system.md`
**Requirement**: `TR-colony-001`, `TR-colony-002`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0016: Colony & Building Architecture
**ADR Decision Summary**: ColonyManager SimClock tick：SimClock.DeltaTime 累加，≥1s 触发一次产出计算；ore clamp 到 [0, ORE_CAP]；energy 无上限；每 tick 广播 OnResourcesUpdated。

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: 纯逻辑无 Unity API。

**Control Manifest Rules (this layer)**:
- Required: 每 tick ore clamp [0, ORE_CAP]；energy 下限 0，无上限
- Forbidden: 禁止在 tick 期间插入建造（原子性 T-3→T-5）
- Guardrail: OnResourcesUpdated 每 tick 最多一次广播

---

## Acceptance Criteria

*From GDD `design/gdd/colony-system.md` C-2, TR-colony-001, TR-colony-002:*

- [ ] SimClock.DeltaTime 累加到 ≥ 1.0s 时触发一次产出 tick
- [ ] 每 tick 遍历所有 PLAYER 节点，累加 BuildingSystem.GetNodeProductionDelta
- [ ] oreNew = Clamp(oreOld + totalOreDelta, 0, ORE_CAP)；energyNew = Max(0, energyOld + totalEnergyDelta)
- [ ] 每 tick 广播 OnResourcesUpdated(oreNew, energyNew, totalOreDelta, totalEnergyDelta)
- [ ] 节点归属变更时（PLAYER → ENEMY）该节点自动排除在产出计算外

---

## Implementation Notes

*Derived from ADR-0016 Decision section and ColonySystem GDD C-2:*

```csharp
// ColonyManager.cs（挂载 StarMapScene）
void Update() {
    if (SimClock.Instance == null) return;

    _accumulator += SimClock.Instance.DeltaTime;
    if (_accumulator < 1.0f) return;

    _accumulator -= 1.0f;
    OnTick();
}

void OnTick() {
    // T-2: 快照所有 PLAYER 节点
    var nodeIds = StarMapData.GetAllNodesByOwner(Ownership.PLAYER);

    // T-3: 遍历节点累加产出
    float totalOreDelta = 0f;
    float totalEnergyDelta = 0f;
    foreach (var nodeId in nodeIds) {
        var delta = BuildingSystem.Instance.GetNodeProductionDelta(nodeId);
        totalOreDelta += delta.orePerSec;
        totalEnergyDelta += delta.energyPerSec;
    }

    // T-4: 更新矿石（含 clamp）
    int prevOre = _oreCurrent;
    _oreCurrent = Mathf.Clamp(_oreCurrent + (int)totalOreDelta, 0, ORE_CAP);

    // T-5: 更新能源（下限 0，无上限）
    _energyCurrent = Mathf.Max(0, _energyCurrent + (int)totalEnergyDelta);

    // T-6: 广播
    OnResourcesUpdated?.Invoke(new ResourceSnapshot {
        ore = _oreCurrent,
        energy = _energyCurrent,
        oreDelta = totalOreDelta,
        energyDelta = totalEnergyDelta
    });

    // T-7: 能源警告判断
    CheckEnergyDeficit(totalEnergyDelta);
}
```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 017: BuildShip 建造逻辑
- Story 018: BuildingSystem.GetNodeProductionDelta

---

## QA Test Cases

*Written by qa-lead at story creation.*

- **AC-1**: Tick fires every 1 second of sim time
  - Given: SimClock.DeltaTime = 1.0s; _accumulator = 0
  - When: Update() is called
  - Then: OnTick() fires; _accumulator resets; resource values updated

- **AC-2**: ORE_CAP clamp at upper boundary
  - Given: _oreCurrent = 95; totalOreDelta = 10; ORE_CAP = 100
  - When: OnTick() runs
  - Then: _oreCurrent = 100 (clamped); excess 5 ore silently discarded

- **AC-3**: Energy has no upper cap
  - Given: _energyCurrent = 1000; totalEnergyDelta = 500
  - When: OnTick() runs
  - Then: _energyCurrent = 1500 (no upper clamp)

- **AC-4**: OnResourcesUpdated fired with correct deltas
  - Given: _oreCurrent = 50, totalOreDelta = +10
  - When: OnTick() runs
  - Then: OnResourcesUpdated fired with ore=60, oreDelta=+10; energy unchanged

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/unit/colony/resource_tick_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: SimClock (Foundation); BuildingSystem.GetNodeProductionDelta (Story 018); StarMapData.GetAllNodesByOwner (Foundation)
- Unlocks: Story 017 (BuildShip)
