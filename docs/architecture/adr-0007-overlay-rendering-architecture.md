# ADR-0007: 驾驶舱内星图叠加层渲染架构

## Status

Accepted

## Date

2026-04-14

## Last Verified

2026-04-14

## Decision Makers

Technical Director, Unity Specialist

## Summary

驾驶舱内需要叠加显示星图（COCKPIT_WITH_OVERLAY 状态），但不能开启第二摄像机（URP 会为每个 Camera 执行完整 Culling Pass，造成 draw call 倍增）。决策：StarMapScene 的 UI 系统支持双模式渲染——正常星图时使用 Camera-based，叠加层模式时切换为 UI Toolkit ScreenOverlay（不依赖 Camera A），由 `OnViewLayerChanged` 事件驱动模式切换。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Rendering / UI |
| **Knowledge Risk** | HIGH — UI Toolkit ScreenOverlay 是 Unity 6 功能，post-cutoff，需验证 |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md` |
| **Post-Cutoff APIs Used** | `UIDocument.panelSettings.renderMode = PanelRenderMode.ScreenSpaceOverlay`；`VisualElement.transform`（Unity 6.1 已废弃，改用 `style.translate`） |
| **Verification Required** | 验证 ScreenOverlay UIDocument 在 URP 管线下不触发额外 Camera Culling Pass；验证叠加层与 URP Post-Processing 的 Z-order 正确（叠加层在场景之上、系统 UI 之下）|

> **Note**: Knowledge Risk HIGH — 项目升级 Unity 版本时须重新验证此 ADR。

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0001（场景管理：StarMapScene 常驻，MasterScene 事件总线） |
| **Enables** | COCKPIT_WITH_OVERLAY 状态的全部 UI 实现 |
| **Blocks** | ViewLayerManager 的 OPENING_OVERLAY / CLOSING_OVERLAY 序列实现 |
| **Ordering Note** | 必须在 ViewLayerManager 实现叠加层序列前完成验证 |

## Context

### Problem Statement

双视角切换系统（GDD dual-perspective-switching.md，规则 S-8）要求驾驶舱内可叠加显示星图，且驾驶舱物理和摄像机继续运行。需要一种方法在不增加第二 Camera 的情况下渲染星图 UI。

### Current State

StarMapScene 使用 Camera-based UI（UI Toolkit 绑定到 StarMap Camera 的 PanelSettings）。驾驶舱场景使用独立摄像机。两摄像机同时 Active 时，URP 为每个摄像机执行 Culling + Shadow Pass，移动端帧预算无法承受。

### Constraints

- **性能**：移动端 draw call 目标 < 200；双 Camera 活跃时总 draw call 约翻倍
- **URP 限制**：Unity 6.3 URP 中，每个 `Camera` 组件触发独立 Culling Pass（即使该摄像机只渲染 UI）
- **StarMapScene 常驻**：ADR-0001 要求 StarMapScene 始终加载，不可卸载后重载作为叠加层
- **驾驶舱物理必须继续**：GDD 规则 S-8：叠加层打开期间驾驶舱摄像机和物理继续运行

### Requirements

- 叠加星图 UI 必须在驾驶舱摄像机之上渲染，不使用第二 Camera
- 叠加层打开/关闭的切换延迟 ≤ 1 帧（不涉及场景加载）
- 叠加层内的星图 UI 必须支持滚动和双指缩放（触屏交互）
- `_isSwitching` 在叠加层操作期间保持 `false`（叠加层不是切换，不锁定其他输入）
- 渲染模式切换由现有事件总线（ADR-0002）驱动，不引入新的通信机制

## Decision

StarMapScene 的 UI 系统在 `UIDocument` 组件上使用两种 `PanelRenderMode`：

- **STARMAP 状态**（正常）：`PanelRenderMode.CameraSpace`，绑定 StarMap Camera
- **COCKPIT_WITH_OVERLAY 状态**（叠加）：`PanelRenderMode.ScreenSpaceOverlay`，不依赖任何 Camera

`ViewLayerManager` 在广播 `OnViewLayerChanged(COCKPIT_WITH_OVERLAY)` 时，同时通过事件通知 `StarMapOverlayController`（StarMapScene 内的组件）切换渲染模式。叠加层面板的显示/隐藏通过 `style.display = DisplayStyle.None/Flex` 控制（ADR-0001 规则 S-4）。

### Architecture

```
MasterScene
  ViewLayerManager
    │
    ├─ OnViewLayerChanged(COCKPIT_WITH_OVERLAY)
    │     │
    │     ▼
    │   StarMapOverlayController (StarMapScene)
    │     ├─ UIDocument.panelSettings.renderMode → ScreenSpaceOverlay
    │     ├─ overlayPanel.style.display = Flex
    │     └─ overlayPanel 从右侧滑入（300ms，UI Toolkit animation）
    │
    └─ OnViewLayerChanged(COCKPIT)
          │
          ▼
        StarMapOverlayController
          ├─ overlayPanel 向右滑出（200ms）
          └─ UIDocument.panelSettings.renderMode → CameraSpace（StarMap Camera）

渲染层级（从后到前）：
  [1] CockpitScene（驾驶舱 3D 场景，Camera B）
  [2] 驾驶舱 HUD（UI Toolkit ScreenOverlay，Sort Order = 10）
  [3] 星图叠加层（UI Toolkit ScreenOverlay，Sort Order = 20）
  [4] 全屏切换遮罩（UI Toolkit ScreenOverlay，Sort Order = 100）
```

### Key Interfaces

```csharp
// StarMapScene 内，订阅 ViewLayerManager 事件
public class StarMapOverlayController : MonoBehaviour
{
    [SerializeField] private UIDocument _starMapDocument;
    [SerializeField] private PanelSettings _screenOverlaySettings;   // renderMode = ScreenSpaceOverlay
    [SerializeField] private PanelSettings _cameraSpaceSettings;     // renderMode = CameraSpace

    // 叠加层根 VisualElement（从 uxml 查询）
    private VisualElement _overlayPanel;

    // 由 ViewLayerManager.OnViewLayerChanged 调用
    public void OnViewLayerChanged(ViewLayer layer)
    {
        switch (layer)
        {
            case ViewLayer.CockpitWithOverlay:
                _starMapDocument.panelSettings = _screenOverlaySettings;
                ShowOverlay();
                break;
            case ViewLayer.Cockpit:
            case ViewLayer.StarMap:
                HideOverlay();
                // 延迟至动画结束后切回 CameraSpace（200ms 后）
                break;
        }
    }

    private void ShowOverlay() { /* 300ms 滑入动画 */ }
    private void HideOverlay() { /* 200ms 滑出动画，完成后切换 panelSettings */ }
}

// PanelSettings 资产配置（Inspector 配置，不在代码中硬写 sortingOrder）
// _screenOverlaySettings: renderMode = ScreenSpaceOverlay, sortingOrder = 20
// _cameraSpaceSettings:   renderMode = CameraSpace, camera = StarMapCamera, sortingOrder = 0
```

### Implementation Guidelines

1. **禁止为叠加层创建第二 Camera**——任何使用 Camera 渲染星图 UI 的方案均被拒绝
2. **PanelSettings 是资产（ScriptableObject）**——在 Inspector 中创建两个 PanelSettings 资产，运行时通过 `UIDocument.panelSettings` 属性切换（不 new 新实例）
3. **VisualElement.transform 已废弃（Unity 6.1）**——滑入/滑出动画使用 `style.translate`，不使用 `transform.position`
4. **叠加层关闭时**先播完 200ms 动画，**动画结束后**才切回 CameraSpace——避免切换瞬间画面闪烁
5. **Sort Order 规则**：驾驶舱 HUD = 10，星图叠加层 = 20，全屏切换遮罩 = 100；所有 UI 层必须在 PanelSettings 资产中设置，不在运行时修改
6. **触屏交互路由**：叠加层打开时，`ShipControlSystem` 暂停触屏输入响应（GDD 规则 S-8）；关闭后恢复——此路由逻辑由 `ViewLayerManager` 负责，不由 `StarMapOverlayController` 负责
7. **`_isSwitching` 不变**：叠加层操作不设置 `_isSwitching = true`，区别于全切换序列

## Alternatives Considered

### Alternative 1: 第二 Camera（CameraSpace，Camera B 用于 UI）

- **Description**: 为星图叠加层启用第二个 Camera，使用 CameraSpace 渲染模式
- **Pros**: 与现有 CameraSpace 方案一致，无需 PanelSettings 切换逻辑
- **Cons**: URP 为每个激活的 Camera 执行完整 Culling Pass；移动端帧预算翻倍；draw call 超出 < 200 目标
- **Estimated Effort**: Low（现有模式延伸）
- **Rejection Reason**: 性能不可接受——移动端 draw call 预算严格，双 Camera 活跃时无法维持 60fps

### Alternative 2: RenderTexture 合成

- **Description**: 星图 UI 渲染到 RenderTexture，作为 Quad 叠加在驾驶舱场景上
- **Pros**: 完全控制 Z-order 和混合模式
- **Cons**: 每帧额外 RenderTexture 写入；UI 触屏交互需要自行映射屏幕坐标；实现复杂度高；移动端 GPU 带宽消耗大
- **Estimated Effort**: High
- **Rejection Reason**: 复杂度与性能开销均不合理；UI Toolkit ScreenOverlay 原生支持触屏且无 RT 开销

### Alternative 3: 将星图 UI 从 StarMapScene 移出，独立为 MasterScene 常驻 UI

- **Description**: 星图 UI 作为 MasterScene 的常驻 UI，在两种模式下均使用 ScreenOverlay
- **Pros**: 消除模式切换逻辑
- **Cons**: 违反 ADR-0001 的场景所有权原则；星图 UI 与星图 Camera 解耦后，CameraSpace 模式下无法正确绑定摄像机视口；场景依赖关系复杂化
- **Estimated Effort**: High
- **Rejection Reason**: 违反既有场景架构规则，且不解决根本问题

## Consequences

### Positive

- 叠加层渲染零额外 Camera，不增加 draw call
- 模式切换在 1 帧内完成，无场景加载开销
- UI Toolkit ScreenOverlay 原生支持触屏事件，无需坐标映射

### Negative

- `UIDocument.panelSettings` 运行时切换是 post-cutoff API，需真机验证
- `StarMapOverlayController` 需要维护两套 PanelSettings 资产
- 叠加层关闭时有 200ms 延迟才切回 CameraSpace——需确保动画结束回调可靠触发

### Neutral

- StarMapScene 现在有「正常模式」和「叠加模式」两种渲染配置
- Sort Order 层级成为全局约定，必须在 Control Manifest 中记录

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| Unity 6.3 LTS 中 `UIDocument.panelSettings` 运行时赋值行为未验证 | MEDIUM | HIGH | 在 Vertical Slice 阶段优先做真机验证；若不可行，回退到 Alternative 3 |
| VisualElement 滑入动画 API（style.translate）行为与预期不符 | LOW | MEDIUM | 提前在独立测试场景验证动画 API |
| 叠加层 Z-order 在 URP Post-Processing（Bloom 等）之上/之下位置不符合预期 | MEDIUM | LOW | 真机测试确认；调整 Sort Order 修复 |

## Performance Implications

| Metric | Without Overlay | With Overlay Open | Budget |
|--------|----------------|-------------------|--------|
| CPU (frame time) | ~12ms | ~12.5ms（额外 UI layout pass） | 16.6ms |
| Draw Calls | ~150 | ~155（叠加层 UI batch） | 200 |
| Memory | 基准 | + ~2MB（PanelSettings 资产） | TBD |
| GPU (fillrate) | 基准 | + 叠加层 panel fillrate（70% 屏幕面积） | 60fps |

## Migration Plan

现有系统（ADR-0001 场景架构）无需迁移——此 ADR 是新增功能。

实施步骤：
1. 在 Unity Editor 创建两个 PanelSettings 资产：`StarMapCameraSpace.asset`、`StarMapScreenOverlay.asset`
2. 实现 `StarMapOverlayController`，订阅 `ViewLayerManager.OnViewLayerChanged`
3. 在 `StarMapScene` 的 UIDocument 组件上配置默认使用 `StarMapCameraSpace.asset`
4. 实现叠加层滑入/滑出动画（使用 `style.translate`）
5. 在真机上验证 Engine Compatibility 中列出的两项行为
6. 在 ViewLayerManager 中实现 OPENING_OVERLAY / CLOSING_OVERLAY 序列（ADR-0001 规则 S-8）

**Rollback plan**: 若 ScreenOverlay 模式不可行，将 StarMapScene UI 迁移至 MasterScene 常驻（Alternative 3），接受场景所有权规则的例外处理。

## Validation Criteria

- [ ] 叠加层打开时，Unity Profiler 中 Camera Culling Pass 数量不增加（仍为 1 个）
- [ ] 叠加层打开/关闭切换耗时 ≤ 1 帧（无卡顿）
- [ ] 叠加层内触屏滚动、双指缩放正常响应
- [ ] 驾驶舱物理（飞船惯性）在叠加层打开期间继续运行
- [ ] 全屏切换遮罩（Sort Order = 100）始终在叠加层之上渲染
- [ ] ReduceMotion = true 时，滑入/滑出动画替换为即时显示/隐藏

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `design/gdd/dual-perspective-switching.md` | 双视角切换 | 规则 S-8：COCKPIT 状态下可触发叠加层，StarMapScene UI 以叠加层渲染；驾驶舱摄像机继续运行 | ScreenOverlay 渲染模式下不依赖 StarMap Camera，驾驶舱摄像机不受影响 |
| `design/gdd/dual-perspective-switching.md` | 双视角切换 | 规则 S-8：叠加层打开期间 `_isSwitching = false` | 叠加层切换不设置 `_isSwitching`，与摄像机/场景切换序列完全分离 |
| `design/gdd/dual-perspective-switching.md` | 双视角切换 | UI-5：「开叠加层」按钮，可交互条件 ViewLayer == COCKPIT AND !_isSwitching | ViewLayerManager 状态机判定；不在此 ADR 中实现 |
| `design/ux/perspective-switch.md` | UX | 叠加层面板从右侧滑入（300ms）；ReduceMotion 下即时显示 | 由 StarMapOverlayController 的动画逻辑实现 |

## Related

- ADR-0001: 场景管理架构（StarMapScene 常驻 + Camera 管理规则 S-3）
- ADR-0002: 事件通信架构（`OnViewLayerChanged` 事件驱动模式切换）
- ADR-0012: SimClock 架构（并列的新增系统，不相互依赖）
