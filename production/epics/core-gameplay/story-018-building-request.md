# Story 018: BuildingSystem — RequestBuild Atomicity + RefreshProductionCache

> **Epic**: core-gameplay
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Manifest Version**: 2026-04-14

## Context

**GDD**: `design/gdd/building-system.md`
**Requirement**: `TR-building-001`, `TR-building-002`, `TR-building-003`, `TR-building-004`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0016: Colony & Building Architecture
**ADR Decision Summary**: BuildingSystem.RequestBuild：CanAfford 预检 → DeductResources 扣费 → 创建 BuildingInstance → RefreshProductionCache；ShipyardTier 节点独占（Shipyard=1, ShipyardUpgrade=Tier+1）。

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: 纯逻辑。

**Control Manifest Rules (this layer)**:
- Required: 建造原子性：ResourceConfig.CanAfford → DeductResources → BuildingInstance → Refresh
- Forbidden: 禁止创建后不刷新缓存
- Guardrail: ShipyardTier 就地升级，不新增实例

---

## Acceptance Criteria

*From GDD `design/gdd/building-system.md` C-1~C-5, TR-building-001~004:*

- [ ] 节点归属 PLAYER 验证；资源充足验证（CanAfford）
- [ ] 建造 BasicMine → node.Buildings 添加 BuildingInstance；ShipyardTier 不变
- [ ] 建造 Shipyard → node.ShipyardTier = 1；建造 ShipyardUpgrade → node.ShipyardTier++（就地）
- [ ] RefreshProductionCache() 在每次建造后调用
- [ ] GetNodeProductionDelta(nodeId) 返回 {orePerSec, energyPerSec}；基础矿场 ore=+10, energy=-2；船坞 energy=-3

---

## Implementation Notes

*Derived from ADR-0016 Decision section and GDD building-system.md:*

```csharp
public BuildResult RequestBuild(string nodeId, BuildingType type) {
    // C-1: 节点归属
    if (StarMapData.GetOwnership(nodeId) != Ownership.PLAYER)
        return new BuildResult { Success = false, FailReason = "NODE_NOT_PLAYER" };

    // C-2: 资源充足
    var cost = ResourceConfig.GetBuildCost(type);
    if (!ColonyManager.Instance.CanAfford(cost.ore, cost.energy))
        return new BuildResult { Success = false, FailReason = "INSUFFICIENT_RESOURCES" };

    // 建造序列（原子）
    ColonyManager.Instance.DeductResources(cost.ore, cost.energy);

    // 创建 BuildingInstance
    var instance = new BuildingInstance {
        InstanceId = $"bld_{Guid.NewGuid():N}",
        BuildingType = type,
        NodeId = nodeId,
        IsActive = true
    };

    // 注册到节点
    StarMapData.AddBuildingToNode(nodeId, instance);

    // ShipyardTier 更新
    if (type == BuildingType.Shipyard) {
        StarMapData.SetShipyardTier(nodeId, 1);
    } else if (type == BuildingType.ShipyardUpgrade) {
        StarMapData.IncrementShipyardTier(nodeId);
    }

    // 刷新缓存
    ColonyManager.Instance.RefreshProductionCache();

    OnBuildingConstructed?.Invoke(nodeId, type);
    return new BuildResult { Success = true };
}
```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 016: ColonyManager tick 读取 GetNodeProductionDelta

---

## QA Test Cases

*Written by qa-lead at story creation.*

- **AC-1**: BasicMine build creates instance and updates cache
  - Given: node PLAYER_OWNED; ore=60, energy=40 (enough for mine 50+20); RefreshProductionCache not yet called
  - When: RequestBuild(nodeId, BasicMine) succeeds
  - Then: BuildingInstance created with type=BasicMine; DeductResources(50,20) called; RefreshProductionCache called; OnBuildingConstructed broadcast

- **AC-2**: Shipyard sets ShipyardTier=1
  - Given: node PLAYER_OWNED; ShipyardTier=0; resources sufficient
  - When: RequestBuild(nodeId, Shipyard) succeeds
  - Then: ShipyardTier set to 1; no new BuildingInstance for shipyard itself (Tier is a property, not an instance)

- **AC-3**: ShipyardUpgrade increments existing tier
  - Given: ShipyardTier=1
  - When: RequestBuild(nodeId, ShipyardUpgrade) succeeds
  - Then: ShipyardTier becomes 2; no new building instance

- **AC-4**: GetNodeProductionDelta returns correct values
  - Given: node with 2 BasicMine (ore=+20, energy=-4) and 1 Shipyard (energy=-3)
  - When: GetNodeProductionDelta(nodeId) is called
  - Then: returns { orePerSec=20, energyPerSec=-7 }

- **AC-5**: Insufficient resources → no state change
  - Given: ore=30, energy=10; mine costs 50 ore + 20 energy
  - When: RequestBuild(nodeId, BasicMine) is called
  - Then: returns FailReason="INSUFFICIENT_RESOURCES"; no resource deducted; no instance created

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/building/request_build_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 016 (ColonyManager tick); ResourceConfig (Foundation)
- Unlocks: Story 016 integration (tick reads from building cache)
