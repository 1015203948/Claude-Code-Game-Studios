# Story 005: ShipDataModel — Runtime State Authority

> **Epic**: foundation-infrastructure
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Manifest Version**: 2026-04-14
> **Estimate**: 2-3 hours

## Context

**GDD**: `design/gdd/ship-system.md`
**Requirement**: `TR-ship-004`, `TR-ship-002`

**ADR Governing Implementation**: ADR-0004 — Runtime State owned by GameDataManager in MasterScene
**ADR Decision Summary**: ShipDataModel 为 MasterScene 持有的运行时状态权威来源；ShipState 状态机转换须原子化；BlueprintId 不存在时实例创建失败。

**Engine**: Unity 6.3 LTS | **Risk**: LOW

**Control Manifest Rules (Foundation)**:
- Required: ShipDataModel lives in MasterScene as single authoritative source
- Required: GameDataManager.Instance 持有所有运行时状态
- Forbidden: 运行时状态不允许存 ScriptableObject

---

## Acceptance Criteria

*From `design/gdd/ship-system.md`:*

- [ ] AC-SHIP-04：实例属性（CurrentHull、ShipState、位置）通过蓝图 ID 引用获取默认值
- [ ] AC-SHIP-07：IN_COCKPIT 互斥约束 — 全局同时最多 1 艘飞船处于 IN_COCKPIT
- [ ] AC-SHIP-08：DESTROYED 飞船拒绝所有指令
- [ ] AC-SHIP-12：IsPlayerControlled 标记不可变（只在构造时设置）
- [ ] ShipState 状态转换：StateMachine 定义的状态 + 合法转换序列（见 GDD 状态机图）
- [ ] `SetState(newState)` 验证合法性后原子更新，触发 ShipStateChannel 广播

---

## Implementation Notes

*From ADR-0004 + GDD ship-system.md:*

1. **ShipDataModel 类**：
   ```csharp
   public class ShipDataModel {
       public string InstanceId { get; }
       public string BlueprintId { get; }
       public float CurrentHull { get; private set; }
       public ShipState State { get; private set; }
       public string DockedNodeId { get; set; }  // nullable
       public bool IsPlayerControlled { get; }  // 构造后不可变
       public string CarrierInstanceId { get; set; }  // nullable，航母才有用
   }
   ```

2. **IN_COCKPIT 互斥**：GameDataManager 维护 `_activeCockpitShipId`（string or null）；`EnterCockpit(shipId)` 时校验当前无其他 IN_COCKPIT 飞船

3. **DESTROYED 门控**：`SetState(DESTROYED)` 后所有后续状态变更请求均拒绝（Assert 或 return false）

4. **ShipStateChannel 广播**：`OnShipStateChanged(instanceId, newState)` 在 `SetState()` 成功后立即广播

---

## Out of Scope

- Scene Management 的 ViewLayerManager 触发 EnterCockpit 序列（Epic B）
- CombatSystem 触发 ShipState → IN_COMBAT / DESTROYED（Core 层）

---

## QA Test Cases

- **AC-SHIP-04: BlueprintId 引用获取默认属性**
  - Given: `ShipDataModel("ship_1", "generic_v1")` 构造
  - When: 实例创建
  - Then: CurrentHull = blueprint.MaxHull；State = DOCKED；IsPlayerControlled = false（默认）

- **AC-SHIP-07: IN_COCKPIT 互斥**
  - Given: Ship_1 已处于 IN_COCKPIT
  - When: Ship_2 调用 `EnterCockpit()`
  - Then: 返回 false；Ship_2 状态不变；Ship_1 仍为 IN_COCKPIT

- **AC-SHIP-08: DESTROYED 拒绝所有指令**
  - Given: Ship 已处于 DESTROYED 状态
  - When: 调用 `SetState(IN_TRANSIT)`
  - Then: 返回 false；状态不变

- **AC-SHIP-12: IsPlayerControlled 不可变**
  - Given: `new ShipDataModel(..., isPlayerControlled: true)`
  - When: 尝试修改 IsPlayerControlled
  - Then: 编译错误 或 运行时 Assert

- **ShipStateChannel 广播时序**
  - Given: ShipDataModel 实例，ShipStateChannel 已订阅
  - When: `SetState(IN_TRANSIT)`
  - Then: ShipStateChannel.Raise(instanceId, IN_TRANSIT) 在 SetState 返回 true 之后被调用

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/ship/ship_data_model_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 004（ShipBlueprintRegistry 须已完成）
- Unlocks: Story 006（ShipStateChannel 广播）；Epic B（ViewLayerManager 的 EnterCockpit 依赖 ShipDataModel）
