# ADR-0019: Ship HUD Architecture

**Status:** Accepted

**Last Updated:** 2026-04-17

## Context

ShipHUD 是驾驶舱视角的态势感知显示层（Situational Awareness Display）。它不拥有任何游戏状态，只订阅游戏系统广播的事件和数据，供玩家实时了解自身状态。

**实现代码：** `src/UI/ShipHUD.cs`
**对应 TR：** TR-hud-001、TR-hud-002、TR-hud-003

---

## Decision

### Component Architecture

ShipHUD 是一个 MonoBehaviour，挂载于 Canvas 上的独立 GameObject。它通过 UGUI（不是 UI Toolkit）实现，使用 Image + Text + CanvasGroup 组合。

```
ShipHUD (MonoBehaviour)
├── Hull Bar:        Image (filled) + Text label
├── Speed Indicator: Text
├── Weapon Cooldown:  Image (filled) + Text label
├── Combat Indicator: Text + CanvasGroup (fade)
└── Soft-Lock Reticle: RectTransform + Image
```

### Serialized Fields（由 Editor 绑定）

| Field | Type | Description |
|-------|------|-------------|
| `_hullBarFill` | Image | 血条填充（Filled 类型） |
| `_hullBarLabel` | Text | 血量数值标签 |
| `_speedLabel` | Text | 速度数值显示 |
| `_cooldownFill` | Image | 冷却条填充 |
| `_cooldownLabel` | Text | 冷却状态文字 |
| `_combatIndicatorText` | Text | COMBAT IN / VICTORY / DEFEAT |
| `_combatIndicatorCanvasGroup` | CanvasGroup | 战斗指示器透明度控制 |
| `_reticleRect` | RectTransform | 软锁定准星 |
| `_hudCamera` | Camera | 用于 WorldToScreenPoint 投影 |
| `_reticleSize` | float | 准星大小（默认 40px） |
| `_viewLayerChannel` | ViewLayerChannel | 视角切换事件 |
| `_shipStateChannel` | ShipStateChannel | 飞船状态切换事件 |

### Channel Subscriptions（OnEnable/OnDisable 配对）

| Event | Handler | Action |
|-------|---------|--------|
| `HealthSystem.OnHullChanged` | `OnHullChanged` | 更新血条 fillAmount 和颜色 |
| `ShipControlSystem.OnAimAngleChanged` | `OnAimAngleChanged` | 预留（未直接渲染） |
| `ShipControlSystem.OnLockLost` | `OnLockLost` | 隐藏软锁定准星 |
| `ShipControlSystem.FireRequested` | `OnFireRequested` | 重置冷却条为 0 |
| `CombatChannel` (Tier 1 SO) | `OnCombatStateChanged` | 显示 COMBAT IN / VICTORY / DEFEAT |
| `ViewLayerChannel` | `OnViewLayerChanged` | COCKPIT 显示，STARMAP 隐藏 |
| `ShipStateChannel` | `OnShipStateChanged` | 记录活跃飞船 ID，刷新血量 |

### Per-Frame Polling（Update）

| Data Source | Method | Display |
|-------------|--------|--------|
| `ShipControlSystem.GetHorizontalSpeed()` | `UpdateSpeedDisplay` | 速度 m/s |
| `CombatSystem.FireCooldownProgress` | `UpdateCooldownDisplay` | 冷却进度 + 颜色 |
| `ShipControlSystem.SoftLockTarget` | `UpdateReticlePosition` | 准星跟随目标 |

### View Layer Visibility

```
OnViewLayerChanged(ViewLayer):
  COCKPIT              → gameObject.SetActive(true)
  COCKPIT_WITH_OVERLAY → gameObject.SetActive(true)
  STARMAP              → gameObject.SetActive(false)
```

### Color Constants

| Element | Condition | Color |
|---------|-----------|-------|
| Hull bar | ratio > 50% | `#33E666` (0.2, 0.9, 0.4) |
| Hull bar | 25% < ratio ≤ 50% | `#FF991A` (warning) |
| Hull bar | ratio ≤ 25% | `#FF2626` (critical) |
| Cooldown bar | progress ≥ 1.0 | `#00FFAA` (ready) |
| Cooldown bar | progress < 1.0 | `#808080` (charging) |

### Combat Indicator Fade

- 显示时 alpha = 1.0
- 持续 `COMBAT_INDICATOR_DURATION = 2.0` 秒后 alpha = 0
- 使用 `Time.deltaTime` 线性衰减（frame-rate independent）

### Soft-Lock Reticle

- 当 `ShipControlSystem.SoftLockTarget != null` 时显示
- 使用 `_hudCamera.WorldToScreenPoint()` 将 3D 位置投影到屏幕坐标
- 如果目标在相机背后（z < 0）则隐藏
- 准星大小固定 `_reticleSize = 40px`

---

## Interface Contracts

### Consumed Events

| Event | Signature | When Fired |
|-------|-----------|------------|
| `OnHullChanged` | `(string instanceId, float currentHull, float maxHull)` | Hull 值变化时 |
| `OnAimAngleChanged` | `(float angleDegrees)` | 瞄准角变化时（未使用） |
| `OnLockLost` | `()` | 软锁定丢失时 |
| `FireRequested` | `()` | 开火请求时 |
| `CombatChannel` | `(CombatPayload)` | 战斗状态变化时 |
| `ViewLayerChanged` | `(ViewLayer)` | 视角切换时 |
| `ShipStateChanged` | `((string instanceId, ShipState newState))` | 飞船状态切换时 |

### Queried Systems（每帧轮询）

| System | Method | Return |
|--------|---------|--------|
| `ShipControlSystem.Instance.GetHorizontalSpeed()` | `UpdateSpeedDisplay` | `float` m/s |
| `CombatSystem.Instance.FireCooldownProgress` | `UpdateCooldownDisplay` | `float [0..1]` |
| `ShipControlSystem.Instance.SoftLockTarget` | `UpdateReticlePosition` | `Transform or null` |
| `HealthSystem.Instance.GetHullRatio(instanceId)` | `RefreshHullDisplay` | `float [0..1]` |

### Provided（无）

ShipHUD 是纯显示组件，不广播任何事件。

---

## Consequences

- HUD 永远不修改游戏状态，所有数据通过事件或轮询从其他系统读取
- 每帧轮询是必要的（速度、冷却、准星位置都需要即时更新），但通过 channel 订阅避免了状态耦合
- ViewLayer 可见性由 `gameObject.SetActive` 控制，而非 Canvas render mode 切换
- Combat indicator 使用 CanvasGroup.alpha 实现 2 秒淡出，而非 Destroy/DelayCall

---

## Implementation Notes

- 使用 UGUI 而非 UI Toolkit，因为 ADR-0007 选择 UGUI 作为驾驶舱 HUD 渲染层
- `ShipHUD` 依赖于 `Game` 程序集中的系统（HealthSystem、ShipControlSystem、CombatSystem），自身位于 `Game.UI` 命名空间
- 测试文件：`tests/unit/ui/ship_hud_test.cs` — 覆盖血条阈值、冷却公式、战斗指示器淡出、ViewLayer 可见性

---

## ADR Dependencies

- **ADR-0002**（Event Communication）：ShipHUD 遵循 OnEnable/OnDisable 配对订阅规则
- **ADR-0007**（Overlay Rendering）：UGUI 驾驶舱 HUD 优先于 UI Toolkit
- **ADR-0014**（Health System）：`OnHullChanged` 事件由 HealthSystem 广播
- **ADR-0018**（Ship Control System）：速度、瞄准角、软锁定目标均来自 ShipControlSystem

---

## GDD Requirements Addressed

| GDD | Requirement |
|-----|-------------|
| `ship-hud.md` | Hull bar: HealthSystem.HullRatio subscription |
| `ship-hud.md` | Weapon cooldown display synced to _fireTimer |
| `ship-hud.md` | Soft-lock reticle via OnLockAcquired/OnLockLost |
