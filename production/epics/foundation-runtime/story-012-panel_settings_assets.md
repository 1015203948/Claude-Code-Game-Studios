# Story 012: PanelSettings SO 资产创建

> **Epic**: Foundation Runtime
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Config/Data
> **Manifest Version**: 2026-04-14
> **Estimate**: 1 hour

## Context

**GDD**: `design/gdd/dual-perspective-switching.md`（规则 S-8）
**Requirement**: `TR-dvs-008`

**ADR Governing Implementation**: ADR-0007: 叠加渲染架构
**ADR Decision Summary**: StarMapScene 的 UIDocument 使用两种 PanelRenderMode：CameraSpace（正常星图）和 ScreenSpaceOverlay（叠加层）；两套 PanelSettings 均为 Inspector 配置的 SO 资产；Sort Order 层级：HUD=10，叠加层=20，遮罩=100。

**Engine**: Unity 6.3 LTS | **Risk**: HIGH
**Engine Notes**: UIDocument.panelSettings 运行时赋值为 post-cutoff API，需真机验证 ScreenOverlay 不触发额外 Camera Culling Pass

**Control Manifest Rules (Foundation)**:
- Required: PanelSettings 为 SO 资产，在 Inspector 中创建，不在代码中 new 实例
- Required: Sort Order 层级在 PanelSettings 资产中设置，运行时不修改

---

## Acceptance Criteria

*From ADR-0007 Decision + Implementation Guidelines:*

- [ ] `StarMapCameraSpace.asset`：renderMode = CameraSpace；sortingOrder = 0；绑定 StarMap Camera
- [ ] `StarMapScreenOverlay.asset`：renderMode = ScreenSpaceOverlay；sortingOrder = 20；不绑定任何 Camera
- [ ] 两个 PanelSettings 资产位于 `Assets/Data/PanelSettings/`
- [ ] StarMapScene 的 UIDocument 组件默认引用 `StarMapCameraSpace.asset`
- [ ] `_screenOverlaySettings` 和 `_cameraSpaceSettings` 通过 SerializeField 暴露，供 StarMapOverlayController 运行时切换

---

## Implementation Notes

*From ADR-0007 Implementation Guidelines:*

1. **资产路径**：`Assets/Data/PanelSettings/StarMapCameraSpace.asset` 和 `Assets/Data/PanelSettings/StarMapScreenOverlay.asset`
2. **StarMapCameraSpace.asset 配置**：
   - Create → UI Toolkit → Panel Settings
   - renderMode = CameraSpace
   - camera = StarMapCamera（场景中的 Camera 引用）
   - sortingOrder = 0
3. **StarMapScreenOverlay.asset 配置**：
   - Create → UI Toolkit → Panel Settings
   - renderMode = ScreenSpaceOverlay
   - sortingOrder = 20（不在运行时修改）
4. **Unity Editor 手动步骤**：在 Project 窗口创建，拖拽到 StarMapOverlayController 的 SerializeField

---

## Out of Scope

- StarMapOverlayController 代码实现（Story 013）
- UIDocument 组件配置（Story 013 的一部分）

---

## QA Test Cases

*No automated test — this is a Config/Data story.*

- **AC-1: PanelSettings 资产存在性**
  - Given: Unity Editor
  - When: 检查 `Assets/Data/PanelSettings/StarMapCameraSpace.asset` 和 `StarMapScreenOverlay.asset`
  - Then: 两个资产均存在，类型为 PanelSettings

- **AC-2: CameraSpace.asset renderMode 正确**
  - Given: StarMapCameraSpace.asset
  - When: 检查 renderMode 和 camera 字段
  - Then: renderMode = CameraSpace；camera = StarMapCamera

- **AC-3: ScreenOverlay.asset renderMode 正确**
  - Given: StarMapScreenOverlay.asset
  - When: 检查 renderMode 和 sortingOrder
  - Then: renderMode = ScreenSpaceOverlay；sortingOrder = 20

---

## Test Evidence

**Story Type**: Config/Data
**Required evidence**: smoke check — Unity Editor 检查通过即可
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: None（纯资产创建，无代码依赖）
- Unlocks: Story 013（StarMapOverlayController 引用这两个资产）
