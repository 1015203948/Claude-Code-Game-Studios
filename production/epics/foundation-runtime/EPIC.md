# Epic: Foundation Runtime

> **Layer**: Foundation
> **Architecture Modules**: 场景管理 · 输入系统 · 叠加渲染
> **GDD**: `design/gdd/dual-perspective-switching.md` · `design/gdd/ship-control-system.md`
> **Governing ADRs**: ADR-0001 · ADR-0003 · ADR-0007
> **Control Manifest Version**: 2026-04-14
> **Status**: Ready
> **Stories**: 6 stories created — see table below

## Stories

| # | Story | Type | Status | ADR | Test Evidence |
|---|-------|------|--------|-----|---------------|
| 011 | ViewLayerManager Core + 切换序列 | Logic | Ready | ADR-0001 | `tests/unit/scene/viewlayer_manager_test.cs` |
| 012 | PanelSettings SO 资产创建 | Config/Data | Ready | ADR-0007 | smoke check |
| 013 | StarMapOverlayController + 叠加序列 | Integration | Ready | ADR-0007 | `tests/integration/scene/overlay_controller_test.cs` |
| 014 | ShipInputManager ActionMap 切换 | Integration | Ready | ADR-0003 | `tests/integration/input/ship_input_manager_test.cs` |
| 015 | DualJoystickInput 触控追踪 + 死区 | Logic | Ready | ADR-0003 | `tests/unit/input/dual_joystick_test.cs` |
| 016 | ShipControlSystem 驾驶舱物理集成 | Integration | Ready | ADR-0003 | `tests/integration/input/ship_control_system_test.cs` |

---

## Overview

本 Epic 实现游戏运行时的 Foundation 层核心系统：场景加载/切换、输入路由、驾驶舱内叠加渲染。依赖 Epic A（foundation-infrastructure）提供的 SO Channel、GameDataManager 和 SimClock。

**三个模块紧密耦合，必须按依赖顺序实现：**
1. 场景管理（基础，其他两个依赖它）
2. 输入系统（依赖场景管理的 ViewLayerChannel）
3. 叠加渲染（依赖场景管理的 ViewLayerChanged 事件）

---

## Module 1: 场景管理

### Owns
- `ViewLayerManager`（MasterScene 单例）
- 三个 Unity 场景拓扑：MasterScene、StarMapScene（常驻）、CockpitScene（Additive 按需）
- `_isSwitching` 并发守卫标志
- `_preEnterState` 切换快照

### Exposes
- `ViewLayerManager.Instance.SwitchTo(ViewLayer target)` — 场景切换入口
- `ViewLayerChannel`（SO Channel）— `OnViewLayerChanged(ViewLayer)` 广播
- `ViewLayerManager.IsSwitching { get; }` — 当前是否正在切换

### ViewLayer States
```
STARMAP
  ├─ SWITCHING_IN ──────────────► COCKPIT
  │                                       │
  │   ◄── SWITCHING_OUT ◄────────────────┤
  │                                       │
  │                              OPENING_OVERLAY
  │                                       │
  │                           COCKPIT_WITH_OVERLAY
  │                              │              │
  │                    CLOSING_OVERLAY   SWITCHING_SHIP
  │                              │              │
  └─────── SWITCHING_OUT ◄───────┴──────────────┘
```

### Switching Sequences

**SWITCHING_IN（10 步）：**
1. `_isSwitching = true`，禁用切换按钮
2. 缓存 `_preEnterState`
3. `ActiveShipId = 目标飞船 id`
4. 全屏遮罩渐入 300ms；星图 UI 交互区域立即禁用
5. CockpitScene 异步加载（`allowSceneActivation = false`）
6. 目标飞船 `ShipState → IN_COCKPIT`
7. 飞船数据写入 CockpitScene
8. `allowSceneActivation = true`；等待 progress ≥ 0.9f
9. `ViewLayer → COCKPIT`；广播 `OnViewLayerChanged(COCKPIT)`
10. 星图摄像机 `enabled = false`；驾驶舱摄像机 `enabled = true`；遮罩渐出；`_isSwitching = false`

**SWITCHING_OUT（9 步）：**
1. `_isSwitching = true`，禁用切换按钮
2. 全屏遮罩渐入 300ms；飞船 HUD 交互区域立即禁用
3. 飞船最终状态回写 ShipDataModel
4. ActiveShip `ShipState → _preEnterState`
5. `ViewLayer → STARMAP`；广播 `OnViewLayerChanged(STARMAP)`
6. 驾驶舱摄像机 `enabled = false`；星图摄像机 `enabled = true`
7. 飞船 HUD Canvas `enabled = false`；星图 UI `style.display = Flex`
8. 星图摄像机定位至记录位置（无动画，直接到位）
9. `SceneManager.UnloadSceneAsync("CockpitScene")`；遮罩渐出；`_isSwitching = false`

**SWITCHING_SHIP（12 步）：**
1. 玩家在叠加星图内选中目标飞船 → 「立即传送」
2. `_isSwitching = true`；叠加层关闭（即时）；全屏遮罩渐入 300ms
3. 旧船数据回写 ShipDataModel
4. 旧船 `ShipState → _preEnterState`
5. 缓存目标飞船 ShipState 为新的 `_preEnterState`
6. `ActiveShipId = 目标飞船 id`；目标飞船 `ShipState → IN_COCKPIT`
7. 目标飞船数据写入新 CockpitScene 加载参数
8. 旧 CockpitScene 异步卸载
9. 新 CockpitScene 异步加载（`allowSceneActivation = false`）
10. 等待旧场景卸载完成 AND 新场景 progress ≥ 0.9f
11. `allowSceneActivation = true`；等待激活完成
12. 全屏遮罩渐出；`_isSwitching = false`（ViewLayer 保持 COCKPIT）

### Key Constraints
- **Camera 切换**：`Camera.enabled = false/true`，禁止 `SetActive(false)`
- **预加载策略**：`COCKPIT_PRELOAD_TRIGGER = ON_SHIP_SELECT`（点击飞船时预加载 CockpitScene 至 90%）
- **遮罩颜色**：`#000000`，Sort Order = 100
- **ReduceMotion**：`AccessibilitySettings.ReduceMotion = true` 时遮罩即时切黑/切透明

### Governing ADR
- **ADR-0001**（场景管理架构）

---

## Module 2: 输入系统

### Owns
- `ShipInputManager`（MasterScene）
- `DualJoystickInput`（触控输入逻辑）
- `Assets/Data/Inputs/game.inputactions`（Unity InputActionAsset）

### ActionMap Configuration

| ActionMap | 用途 | 绑定 |
|-----------|------|------|
| `StarMapActions` | 星图视图输入 | 节点点击、缩放、拖拽 |
| `CockpitActions` | 驾驶舱输入 | 左摇杆（推力/转向）、右摇杆（瞄准） |

### Input Routing Logic

```
ViewLayerChannel.OnViewLayerChanged(ViewLayer layer)
  → ShipInputManager.SetActiveMap(layer)
    → if layer == STARMAP:       StarMapActions.Enable(),  CockpitActions.Disable()
    → if layer == COCKPIT:      StarMapActions.Disable(), CockpitActions.Enable()
    → if SWITCHING_* or OVERLAY: StarMapActions.Disable(), CockpitActions.Disable()
```

### Virtual Joystick Implementation

- **左半屏**：Thrust joystick（前进 + 左/右转向）
- **右半屏**：Aim joystick（瞄准辅助）
- **Dead zone**：`normalized = Clamp01((Abs(offset) - 0.08f) / 0.92f)`
- **fingerId 追踪**：用 `Finger.id`（对象引用），不用 `finger.index`
- **初始位置**：以手指落下位置为摇杆中心（`touch.position`），不用 `touch.delta`

### Key Constraints
- **仅此处调用 EnhancedTouchSupport**：禁止其他 MonoBehaviour 调用 `Enable()` / `Disable()`
- **InputActionAsset 单一资产**：禁止拆分多个 `.inputactions` 文件
- **ShipInputChannel**：每帧快照当前输入状态，供 `ShipController` 消费

### Governing ADR
- **ADR-0003**（输入系统架构）— Engine Risk: **HIGH**（New Input System post-cutoff）

---

## Module 3: 叠加渲染

### Owns
- `StarMapOverlayController`（StarMapScene 内）
- 两个 `PanelSettings` SO 资产：`StarMapCameraSpace.asset`、`StarMapScreenOverlay.asset`
- 叠加层面板 VisualElement（从 UXML 查询）

### PanelSettings Assets

| 资产 | renderMode | sortingOrder | 用途 |
|------|-----------|-------------|------|
| `StarMapCameraSpace.asset` | CameraSpace（StarMap Camera 绑定） | 0 | 正常星图视图 |
| `StarMapScreenOverlay.asset` | ScreenSpaceOverlay | 20 | COCKPIT_WITH_OVERLAY 叠加层 |

### Overlay Sequence

**OPENING_OVERLAY（3 步）：**
1. `ViewLayer → COCKPIT_WITH_OVERLAY`；广播 `OnViewLayerChanged`
2. `UIDocument.panelSettings = ScreenOverlay.asset`；叠加面板 `style.display = Flex`；从右侧滑入 300ms
3. 驾驶舱触摸输入路由暂停（`ShipControlSystem` 暂停响应）

**CLOSING_OVERLAY（3 步）：**
1. 叠加面板向右侧滑出 200ms
2. 等待动画完成 → `panelSettings = CameraSpace.asset`；`style.display = DisplayStyle.None`
3. `ViewLayer → COCKPIT`；广播 `OnViewLayerChanged`；驾驶舱触摸输入恢复

### Key Constraints
- **禁止第二 Camera**：URP 会为每个 Camera 执行完整 Culling Pass
- **Sort Order 层级**（在 PanelSettings 资产中设置，不运行时修改）：
  - 驾驶舱 HUD = 10
  - 星图叠加层 = 20
  - 全屏切换遮罩 = 100
- **动画 API**：`VisualElement.transform` 已废弃（Unity 6.1）→ 用 `style.translate`
- **ReduceMotion**：`AccessibilitySettings.ReduceMotion = true` 时滑入/滑出替换为即时显示/隐藏
- **`_isSwitching` 不变**：叠加层操作不触发切换锁定

### Governing ADR
- **ADR-0007**（叠加渲染架构）— Engine Risk: **HIGH**（ScreenOverlay post-cutoff，需真机验证）

---

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-dvs-001 | 三场景 Additive 架构（MasterScene + StarMapScene 常驻 + CockpitScene 按需） | ADR-0001 ✅ |
| TR-dvs-002 | 切换时间 ≤ 1.0s；ON_SHIP_SELECT 预加载 | ADR-0001 ✅ |
| TR-dvs-004 | ViewLayer 状态机（STARMAP / COCKPIT / COCKPIT_WITH_OVERLAY） | ADR-0001 ✅ |
| TR-dvs-006 | ViewLayerChannel 跨场景广播 | ADR-0001 ✅ |
| TR-starmap-001 | 星图节点在驾驶舱期间持续更新（StarMapScene 常驻） | ADR-0001 ✅ |
| TR-dvs-007 | 双 ActionMap 切换；EnhancedTouch fingerId 追踪 | ADR-0003 ✅ |
| TR-dvs-008 | COCKPIT_WITH_OVERLAY ScreenOverlay 渲染；Sort Order 层级 | ADR-0007 ✅ |

---

## Engine Compatibility Warnings

⚠️ **HIGH RISK — 必须在真机验证：**

| ADR | 风险项 | 验证条件 |
|-----|--------|---------|
| ADR-0003 | `com.unity.inputsystem@1.11` + EnhancedTouch | Legacy `Input.*` 已弃用；New Input System 触控行为需验证 |
| ADR-0003 | `Finger.id` 对象引用追踪 | `finger.index` 不稳定；需验证手指追踪在多指场景下正确 |
| ADR-0007 | `UIDocument.panelSettings` 运行时赋值 | ScreenOverlay 模式下不触发 Camera Culling Pass |
| ADR-0007 | `style.translate` 动画 | `VisualElement.transform.position` 已废弃；验证滑动动画正确 |
| ADR-0007 | 叠加层 Z-order vs URP Post-Processing | 叠加层在 Post-Processing 之上/之下？真机确认 |

---

## Definition of Done

本 Epic 完成的条件：
- 所有 Story 实现、审查、`/story-done` 关闭
- `design/gdd/dual-perspective-switching.md` 全部 22 条 AC（AC-DVS-01 ~ AC-DVS-22）验证通过
- `design/gdd/ship-control-system.md` 全部验收标准验证通过
- `design/ux/perspective-switch.md` 全部 AC（AC-UX-PS-01 ~ AC-UX-PS-14）验证通过
- Logic 类 Story 有通过测试（`tests/unit/`）
- Integration 类 Story 有通过集成测试（`tests/integration/`）
- **真机验证**：上述 Engine Compatibility Warnings 中的每一项

---

## Next Step

Run `/create-stories foundation-runtime` to break this epic into implementable stories.
