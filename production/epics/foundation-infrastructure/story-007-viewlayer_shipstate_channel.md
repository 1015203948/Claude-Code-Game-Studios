# Story 007: ViewLayerChannel + ShipStateChannel — Broadcast & Consume

> **Epic**: foundation-infrastructure
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Manifest Version**: 2026-04-14
> **Estimate**: 2 hours

## Context

**GDD**: `design/gdd/dual-perspective-switching.md`；`design/gdd/ship-system.md`
**Requirement**: `TR-dvs-006`, `TR-ship-003`

**ADR Governing Implementation**: ADR-0002 — Tier 1 SO Channel；ADR-0004 — ShipDataModel 权威广播
**ADR Decision Summary**: ShipDataModel 持有并广播 ShipStateChannel；ViewLayerChannel 由 ViewLayerManager 广播；所有订阅在 OnEnable/OnDisable 中配对。

**Engine**: Unity 6.3 LTS | **Risk**: LOW

**Control Manifest Rules (Foundation)**:
- Required: Tier 1 跨场景事件必须用 SO Channel
- Required: 所有订阅必须在 OnEnable/OnDisable 配对

---

## Acceptance Criteria

*From `design/gdd/dual-perspective-switching.md` + `ship-system.md`:*

- [ ] ViewLayerChannel 广播 `OnViewLayerChanged(ViewLayer)` 负载（ViewLayer 为 enum）
- [ ] ShipStateChannel 广播 `OnShipStateChanged(string instanceId, ShipState newState)`
- [ ] StarMapUI、ShipHUD、ShipInputManager 订阅 ViewLayerChannel（OnEnable/OnDisable 配对）
- [ ] StarMapUI、ShipHUD 订阅 ShipStateChannel（OnEnable/OnDisable 配对）
- [ ] ViewLayerChannel 位于 `Assets/Data/Channels/ViewLayerChannel.asset`
- [ ] ShipStateChannel 位于 `Assets/Data/Channels/ShipStateChannel.asset`

---

## Implementation Notes

1. **ViewLayer enum**（在 Shared/Types/ 或 ViewLayerManager 附近）：
   ```csharp
   public enum ViewLayer { STARMAP, COCKPIT, COCKPIT_WITH_OVERLAY }
   ```

2. **ViewLayerChannel SO**：
   ```csharp
   [CreateAssetMenu(menuName = "Channels/ViewLayerChannel")]
   public class ViewLayerChannel : GameEvent<ViewLayer> { }
   ```

3. **ShipStateChannel SO**：
   ```csharp
   [CreateAssetMenu(menuName = "Channels/ShipStateChannel")]
   public class ShipStateChannel : GameEvent<(string instanceId, ShipState newState)> { }
   ```

4. **ViewLayerManager 广播**（Epic B 实现，本 Story 仅确认接口）：
   - SWITCHING_IN 完成后调用 `ViewLayerChannel.Raise(ViewLayer.COCKPIT)`
   - SWITCHING_OUT 完成后调用 `ViewLayerChannel.Raise(ViewLayer.STARMAP)`

5. **ShipDataModel.SetState() 广播**：
   ```csharp
   private void SetState(ShipState newState) {
       State = newState;
       _shipStateChannel.Raise((InstanceId, newState));
   }
   ```

---

## Out of Scope

- ViewLayerManager 切换逻辑（Epic B — Story B1）
- StarMapUI、ShipHUD 具体 UI 响应逻辑（Presentation 层）
- ShipInputManager ActionMap 切换（Epic B）

---

## QA Test Cases

- **AC-1: ViewLayerChannel 广播和订阅**
  - Given: ViewLayerChannel SO + 订阅者 MonoBehaviour
  - When: `ViewLayerChannel.Raise(ViewLayer.COCKPIT)`
  - Then: 订阅者 OnViewLayerChanged(ViewLayer.COCKPIT) 被调用

- **AC-2: ShipStateChannel 广播和订阅**
  - Given: ShipStateChannel SO + ShipDataModel
  - When: ShipDataModel.SetState(IN_TRANSIT) 被调用
  - Then: 订阅者 OnShipStateChanged("ship_1", IN_TRANSIT) 被调用

- **AC-3: OnDisable 后不接收广播**
  - Given: 订阅者已调用 OnDisable（Unsubscribe）
  - When: ViewLayerChannel.Raise(COCKPIT)
  - Then: 订阅者 handler 不被调用

- **AC-4: ViewLayer == COCKPIT_WITH_OVERLAY 枚举值存在**
  - Given: ViewLayer enum
  - When: 检查枚举包含 STARMAP, COCKPIT, COCKPIT_WITH_OVERLAY
  - Then: 三个值均存在

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/event/viewlayer_shipstate_channel_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 006（Channel 架构须已完成）
- Unlocks: Epic B（ViewLayerManager、ShipInputManager 消费 Channel）
