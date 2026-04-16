# Story 004: ShipBlueprint + ShipBlueprintRegistry — Blueprint Config

> **Epic**: foundation-infrastructure
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: 2026-04-14
> **Estimate**: 1-2 hours

## Context

**GDD**: `design/gdd/ship-system.md`
**Requirement**: `TR-ship-001`, `TR-ship-005`

**ADR Governing Implementation**: ADR-0004 — Layer 1 Config ScriptableObject
**ADR Decision Summary**: ShipBlueprint 为只读 SO；ShipBlueprintRegistry 为单例查询接口；BlueprintId 引用不存在的蓝图时实例创建失败。

**Engine**: Unity 6.3 LTS | **Risk**: LOW

**Control Manifest Rules (Foundation)**:
- Required: Config SO 位于 `assets/data/config/`，运行时只读
- Required: ShipBlueprintRegistry 单例查询

---

## Acceptance Criteria

*From `design/gdd/ship-system.md`:*

- [ ] `ShipBlueprint` ScriptableObject 字段：BlueprintId、MaxHull、ThrustPower、TurnSpeed、WeaponSlots(int)、BuildCost(ResourceBundle)、RequiredShipyardTier(int)、CarrierInstanceId(string, nullable)
- [ ] `is_valid_ship_instance(blueprint)` — MaxHull>0、ThrustPower>0、TurnSpeed>0、WeaponSlots≥0 时返回 true
- [ ] BlueprintId = "generic_v1"（普通飞船）；BlueprintId = "carrier_v1"（航母，HangarCapacity=3）
- [ ] `ShipBlueprintRegistry.Instance.GetBlueprint(id)` 返回 ShipBlueprint 或 null
- [ ] `GetAllBlueprints()` 返回所有已注册 Blueprint

---

## Implementation Notes

*From ADR-0004:*

1. **ShipBlueprint SO 资产**：
   - `Assets/Data/Config/ShipBlueprint_generic_v1.asset`
   - `Assets/Data/Config/ShipBlueprint_carrier_v1.asset`

2. **ShipBlueprintRegistry**：
   ```csharp
   public class ShipBlueprintRegistry {
       public static ShipBlueprintRegistry Instance { get; private set; }
       private Dictionary<string, ShipBlueprint> _blueprints;
       public void Register(ShipBlueprint bp);  // Editor 或初始化时调用
       public ShipBlueprint GetBlueprint(string blueprintId);
       public IReadOnlyList<ShipBlueprint> GetAllBlueprints();
   }
   ```

3. **is_valid_ship_instance 校验**（在 ShipBlueprint 内部或 GameDataManager 中）：
   ```csharp
   public static bool IsValid(ShipBlueprint bp) =>
       bp.MaxHull > 0 && bp.ThrustPower > 0 && bp.TurnSpeed > 0 && bp.WeaponSlots >= 0;
   ```

4. **carrier_v1 特殊字段**：HangarCapacity = 3（对应 H-1 规则：最多 3 艘战斗机）

---

## Out of Scope

- ShipDataModel 实例化（Story 005）
- 建造费用校验（Story 010 或 ColonySystem）

---

## QA Test Cases

- **AC-1: generic_v1 蓝图注册和查询**
  - Given: ShipBlueprintRegistry 已注册 generic_v1
  - When: `GetBlueprint("generic_v1")`
  - Then: 返回非 null Blueprint；BlueprintId = "generic_v1"；MaxHull > 0

- **AC-2: carrier_v1 蓝图注册和查询**
  - Given: ShipBlueprintRegistry 已注册 carrier_v1
  - When: `GetBlueprint("carrier_v1")`
  - Then: 返回非 null；BlueprintId = "carrier_v1"；CarrierInstanceId = null；HangarCapacity = 3

- **AC-3: 不存在的 blueprintId**
  - Given: ShipBlueprintRegistry
  - When: `GetBlueprint("non_existent_v1")`
  - Then: 返回 `null`

- **AC-4: is_valid_ship_instance 合法蓝图**
  - Given: generic_v1 blueprint，MaxHull=100, ThrustPower=8, TurnSpeed=180, WeaponSlots=1
  - When: `IsValid(blueprint)`
  - Then: 返回 `true`

- **AC-5: is_valid_ship_instance 非法蓝图**
  - Given: blueprint，MaxHull = 0
  - When: `IsValid(blueprint)`
  - Then: 返回 `false`

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/ship/blueprint_registry_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: None（可独立创建）
- Unlocks: Story 005（ShipDataModel 实例化需引用 BlueprintRegistry）
