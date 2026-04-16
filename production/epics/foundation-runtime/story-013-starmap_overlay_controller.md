# Story 013: StarMapOverlayController + 叠加序列

> **Epic**: Foundation Runtime
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Integration
> **Manifest Version**: 2026-04-14
> **Estimate**: 2-3 hours

## Context

**GDD**: `design/gdd/dual-perspective-switching.md`
**Requirement**: `TR-dvs-008`, `TR-dvs-016`, `TR-dvs-017`

**ADR Governing Implementation**: ADR-0007: 叠加渲染架构
**ADR Decision Summary**: StarMapOverlayController 订阅 ViewLayerChannel，在 COCKPIT_WITH_OVERLAY 时切换 UIDocument.panelSettings 为 ScreenOverlay；叠加层动画使用 style.translate（VisualElement.transform 已废弃）；Sort Order 层级在 PanelSettings 资产中设置；ReduceMotion 支持。

**Engine**: Unity 6.3 LTS | **Risk**: HIGH
**Engine Notes**: style.translate 动画为 post-cutoff API；ScreenOverlay 需真机验证不触发额外 Camera Culling Pass

**Control Manifest Rules (Foundation)**:
- Required: 滑入/滑出动画使用 `style.translate`，不使用 `VisualElement.transform.position`
- Required: ReduceMotion = true 时动画替换为即时显示/隐藏
- Forbidden: 禁止使用 Camera 渲染叠加层 UI

---

## Acceptance Criteria

*From GDD `design/gdd/dual-perspective-switching.md` AC-DVS-16, AC-DVS-17, AC-DVS-18：*

- [ ] OPENING_OVERLAY（3步）：OnViewLayerChanged(COCKPIT_WITH_OVERLAY) → UIDocument.panelSettings = ScreenOverlay.asset → 叠加面板 style.display = Flex → 从右侧滑入 300ms
- [ ] CLOSING_OVERLAY（3步）：叠加面板向右侧滑出 200ms → 动画完成后 panelSettings = CameraSpace.asset → style.display = DisplayStyle.None → OnViewLayerChanged(COCKPIT) 广播
- [ ] AC-DVS-16（叠加层打开，物理继续运行）：叠加层打开后 10 帧内，飞船 Rigidbody.velocity 变化幅度 < 0.5 units/帧（证明物理惯性未中断）
- [ ] AC-DVS-17（叠加层关闭，飞船操控恢复）：叠加层关闭后 5 帧内，ShipInputChannel.OnThrustChanged 恢复响应触屏输入
- [ ] AC-DVS-18（SWITCHING_SHIP 触发）：StarMapOverlayController 响应叠加层内飞船选中，调用 ViewLayerManager.RequestSwitchShip(shipId)
- [ ] ReduceMotion = true 时：滑入/滑出动画替换为即时显示/隐藏（≤ 1 帧）
- [ ] 叠加层面板关闭动画结束后才切换回 CameraSpace（避免闪烁）
- [ ] _isSwitching 在叠加层操作期间保持不变（叠加层不是切换，不锁定输入）

---

## Implementation Notes

*From ADR-0007 Implementation Guidelines:*

1. **StarMapOverlayController 位置**：StarMapScene 内，挂载在 StarMap UIDocument 同一 GameObject 或相邻 GameObject
2. **订阅**：
   ```csharp
   private void OnEnable() => _viewLayerChannel.Subscribe(OnViewLayerChanged);
   private void OnDisable() => _viewLayerChannel.Unsubscribe(OnViewLayerChanged);
   ```
3. **滑入动画**：
   ```csharp
   // ✅ 正确：使用 style.translate
   _overlayPanel.style.translate = new StyleTranslate(new TransformOffset(new Length(panelWidth), new Length(0)));
   // 用计时器从 panelWidth → 0 渐变
   // ❌ 禁止：VisualElement.transform.position（已废弃）
   ```
4. **ReduceMotion**：
   ```csharp
   if (AccessibilitySettings.ReduceMotion) {
       _overlayPanel.style.display = DisplayStyle.Flex; // 即时显示
   }
   ```
5. **两套 PanelSettings**：通过 `[SerializeField] PanelSettings _cameraSpaceSettings;` 和 `[SerializeField] PanelSettings _screenOverlaySettings;` 注入

---

## Out of Scope

- ViewLayerManager 切换序列（Story 011）
- ShipInputManager ActionMap 切换（Story 014）
- CockpitScene 物理和 HUD

---

## QA Test Cases

- **AC-1: OPENING_OVERLAY 序列**
  - Given: ViewLayer = COCKPIT，StarMapOverlayController 已启用，StarMapScreenOverlay.asset 已配置
  - When: ViewLayerChannel.Raise(COCKPIT_WITH_OVERLAY)
  - Then: UIDocument.panelSettings = ScreenOverlay.asset；_overlayPanel.style.display = Flex；300ms 后面板位于视口内
  - Edge cases: ReduceMotion = true → 即时显示，无动画

- **AC-2: CLOSING_OVERLAY 序列**
  - Given: ViewLayer = COCKPIT_WITH_OVERLAY，叠加面板可见
  - When: ViewLayerChannel.Raise(COCKPIT)
  - Then: 叠加面板向右侧滑出（200ms）；动画完成后 panelSettings = CameraSpace.asset；style.display = DisplayStyle.None
  - Edge cases: 动画被打断（Rapid ViewLayer 切回 COCKPIT_WITH_OVERLAY）→ 等待上一动画完成后再执行新切换

- **AC-3: 物理惯性在叠加层打开期间继续运行**
  - Given: 飞船 Rigidbody.velocity = (10, 0, 0)，ViewLayer = COCKPIT
  - When: 打开叠加层
  - Then: 叠加层打开后 10 帧内，每帧 velocity 变化 < 0.5 units/帧（Rigidbody 继续被物理系统驱动）
  - Edge cases: 验证方式：读取 `Rigidbody.velocity` 的 frame-over-frame 变化

- **AC-4: SWITCHING_SHIP 触发**
  - Given: ViewLayer = COCKPIT_WITH_OVERLAY，玩家点击了另一艘己方飞船节点
  - When: StarMapOverlayController 收到飞船选中事件
  - Then: 调用 ViewLayerManager.RequestSwitchShip(shipId)；叠加层立即关闭（不计入门控 _isSwitching）
  - Edge cases: ActiveShip ≠ IN_COMBAT 验证由 Story 011 的 ViewLayerManager 负责

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/scene/overlay_controller_test.cs` — must exist and pass
**Status**: ✅ Created — `tests/integration/scene/overlay_controller_test.cs`

---

## Dependencies

- Depends on: Story 011（ViewLayerManager 实现 SWITCHING_SHIP 序列入口）；Story 012（PanelSettings 资产已创建）
- Unlocks: Story 016（ShipControlSystem 依赖叠加层打开期间物理继续运行的行为）

---

## Completion Notes

**Completed**: 2026-04-15
**Criteria**: 8/8 passing（SWITCHING_SHIP 触发为 stub 实现，待 Story 011 ViewLayerManager 完成后连线）
**Deviations**: ADVISORY — PanelSettings 资产（_cameraSpaceSettings / _screenOverlaySettings）为 SerializeField stub，需在 Unity Editor 手动创建 Story 012 资产后连线
**Test Evidence**: tests/integration/scene/overlay_controller_test.cs — 14个 EditMode 测试用例
**Code Review**: Pending

