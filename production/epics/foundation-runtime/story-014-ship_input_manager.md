# Story 014: ShipInputManager ActionMap 切换

> **Epic**: Foundation Runtime
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Integration
> **Manifest Version**: 2026-04-14
> **Estimate**: 2-3 hours

## Context

**GDD**: `design/gdd/ship-control-system.md`
**Requirement**: `TR-dvs-007`

**ADR Governing Implementation**: ADR-0003: Input System Architecture
**ADR Decision Summary**: ShipInputManager 订阅 ViewLayerChannel.OnViewLayerChanged，根据 ViewLayer 互斥切换 StarMapActions 和 CockpitActions；EnhancedTouchSupport 唯一由 ShipInputManager 管理（OnEnable Enable / OnDisable Disable）；ShipInputChannel 广播驾驶舱操控数据到其他场景。

**Engine**: Unity 6.3 LTS | **Risk**: HIGH
**Engine Notes**: EnhancedTouch API 为 post-cutoff；finger.index 在多点触控下的稳定性需要验证

**Control Manifest Rules (Foundation)**:
- Required: EnhancedTouchSupport.Enable/Disable 唯一由 ShipInputManager 调用，禁止其他 MonoBehaviour 调用
- Required: ActionMap 切换在 OnViewLayerChanged 回调中执行，OnEnable/OnDisable 中执行订阅
- Required: ViewLayer == SWITCHING_* 或 OVERLAY 时两个 ActionMap 均禁用

---

## Acceptance Criteria

*From ADR-0003 Decision + Implementation Guidelines + GDD AC-CTRL-10：*

- [ ] ViewLayer = STARMAP → StarMapActions.Enable()，CockpitActions.Disable()
- [ ] ViewLayer = COCKPIT → StarMapActions.Disable()，CockpitActions.Enable()
- [ ] ViewLayer ∈ {SWITCHING_IN, SWITCHING_OUT, SWITCHING_SHIP, OPENING_OVERLAY, CLOSING_OVERLAY} → StarMapActions.Disable()，CockpitActions.Disable()
- [ ] OnViewLayerChanged 订阅在 OnEnable/OnDisable 中配对（禁止 Awake/Start 订阅）
- [ ] EnhancedTouchSupport 仅在 OnEnable 中 Enable，OnDisable 中 Disable（OnDisable 不能只调用 Disable 而不匹配 Enable）
- [ ] AC-CTRL-10：视角切换时 InputActionMap 正确切换，输入路由对应变化

---

## Implementation Notes

*From ADR-0003 Decision + Implementation Guidelines:*

1. **ShipInputManager 挂载**：MasterScene 内，与 ViewLayerManager 同一 GameObject 或相邻
2. **InputActionAsset**：`Assets/Data/Inputs/game.inputactions`（单一资产）
3. **订阅模板**：
   ```csharp
   private void OnEnable() {
       _viewLayerChannel.Subscribe(OnViewLayerChanged);
       EnhancedTouchSupport.Enable();
   }
   private void OnDisable() {
       _viewLayerChannel.Unsubscribe(OnViewLayerChanged);
       EnhancedTouchSupport.Disable();
   }
   private void OnViewLayerChanged(ViewLayer layer) => SetActiveMap(layer);
   ```
4. **ActionMap 禁用时机**：SWITCHING_* 和 OVERLAY 状态均禁用两个 ActionMap（防止误触发）
5. **ShipInputChannel 注入**：`[SerializeField] private ShipInputChannel _shipInputChannel;`

---

## Out of Scope

- DualJoystickInput 触控逻辑（Story 015）
- ShipControlSystem 物理应用（Story 016）
- game.inputactions 资产创建（Unity Editor 手动步骤）

---

## QA Test Cases

- **AC-1: ViewLayer = STARMAP 时 ActionMap 正确**
  - Given: ShipInputManager 已启用，InputActionAsset 已加载
  - When: OnViewLayerChanged(STARMAP) 被调用
  - Then: StarMapActions 控制启用；CockpitActions 控制禁用

- **AC-2: ViewLayer = COCKPIT 时 ActionMap 正确**
  - Given: ShipInputManager 已启用
  - When: OnViewLayerChanged(COCKPIT) 被调用
  - Then: StarMapActions 控制禁用；CockpitActions 控制启用

- **AC-3: SWITCHING_* 和 OVERLAY 期间两个 ActionMap 均禁用**
  - Given: ShipInputManager 已启用
  - When: OnViewLayerChanged(SWITCHING_IN) 和 OnViewLayerChanged(COCKPIT_WITH_OVERLAY) 被调用
  - Then: StarMapActions.IsEnabled() = false；CockpitActions.IsEnabled() = false

- **AC-4: EnhancedTouchSupport 生命周期配对**
  - Given: ShipInputManager GameObject 被 Disable 然后重新 Enable
  - When: GameObject 经历 Disable → Enable
  - Then: EnhancedTouchSupport.Disable() 在 Disable 时调用；EnhancedTouchSupport.Enable() 在 Enable 时调用；无重入或重复调用

- **AC-5: OnDisable 后不接收广播**
  - Given: ShipInputManager 已调用 OnDisable
  - When: ViewLayerChannel.Raise(STARMAP)
  - Then: SetActiveMap() 不被调用（订阅已取消）

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/input/ship_input_manager_test.cs` — must exist and pass
**Status**: ✅ Created — `tests/integration/input/ship_input_manager_test.cs`

---

## Dependencies

- Depends on: Story 011（ViewLayerManager 建立 ViewLayerChannel）；Story 006（SO Channel 架构）
- Unlocks: Story 016（ShipControlSystem 消费 ShipInputChannel）

---

## Completion Notes

**Completed**: 2026-04-15
**Criteria**: 6/6 passing
**Deviations**: ADVISORY — Story 006 Status 为 "Ready" 而非 "Complete"；ViewLayerChannel stub 已存在于 src/Channels/，满足实现需求
**Test Evidence**: tests/integration/input/ship_input_manager_test.cs — 11个 EditMode 测试用例
**Code Review**: Pending


- Depends on: Story 011（ViewLayerManager 建立 ViewLayerChannel）；Story 006（SO Channel 架构）
- Unlocks: Story 016（ShipControlSystem 消费 ShipInputChannel）
