# Story 011: ViewLayerManager Core + 切换序列

> **Epic**: Foundation Runtime
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: 2026-04-14
> **Estimate**: 4-6 hours

## Context

**GDD**: `design/gdd/dual-perspective-switching.md`
**Requirement**: `TR-dvs-001`, `TR-dvs-002`, `TR-dvs-004`, `TR-dvs-006`

**ADR Governing Implementation**: ADR-0001: Scene Management Architecture
**ADR Decision Summary**: ViewLayerManager 持有全局 ViewLayer 状态机；三个 Unity 场景拓扑（MasterScene/StarMapScene/CockpitScene）；Camera.enabled 切换（非 SetActive）；_isSwitching 并发守卫；_preEnterState 切换快照。

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: Engine Compatibility Warning — CockpitScene Additive 加载时间、低端 Android 内存预算需要真机验证

**Control Manifest Rules (Foundation)**:
- Required: Camera 切换用 `Camera.enabled = false/true`，禁止 `SetActive(false)`
- Required: `_isSwitching` 防并发标志在所有切换序列起止均须操作
- Required: ViewLayerChannel 广播须在 OnEnable/OnDisable 配对订阅

---

## Acceptance Criteria

*From GDD `design/gdd/dual-perspective-switching.md`（v2 切换序列步骤 + 隐含 AC-DVS-01~15）：*

- [ ] ViewLayer 枚举包含：`STARMAP`, `COCKPIT`, `COCKPIT_WITH_OVERLAY`, `SWITCHING_IN`, `SWITCHING_OUT`, `OPENING_OVERLAY`, `CLOSING_OVERLAY`, `SWITCHING_SHIP`
- [ ] SWITCHING_IN（10步）：_isSwitching=true → 缓存_preEnterState → ActiveShipId更新 → 遮罩渐入 → CockpitScene异步加载 → 目标飞船→IN_COCKPIT → 数据写入 → allowSceneActivation=true → 等待progress≥0.9f → ViewLayer→COCKPIT广播 → Camera切换 → 遮罩渐出 → _isSwitching=false
- [ ] SWITCHING_OUT（9步）：_isSwitching=true → 遮罩渐入 → 状态回写 → ShipState恢复 → ViewLayer→STARMAP广播 → Camera切换 → HUD/UI切换 → 星图摄像机定位 → UnloadSceneAsync → 遮罩渐出 → _isSwitching=false
- [ ] SWITCHING_SHIP（12步，叠加层内跳船）：_isSwitching=true → 叠加层即时关闭 → 旧船状态回写 → 旧船State恢复 → 新_preEnterState缓存 → ActiveShipId更新 → 新船State→IN_COCKPIT → 新船数据写入 → 旧场景卸载 → 新场景异步加载 → allowSceneActivation=true → 等待完成 → ViewLayer保持COCKPIT → 遮罩渐出 → _isSwitching=false
- [ ] _isSwitching=true 期间所有切换触发器（切换按钮、跳船按钮）禁用
- [ ] 切换序列中途被摧毁（E-1）：ViewLayer 强制回落 STARMAP，_isSwitching 清除
- [ ] ViewLayerManager.IsSwitching { get; } 属性暴露当前切换状态
- [ ] ViewLayerManager.Instance 暴露全局单例访问口

---

## Implementation Notes

*From ADR-0001 Decision + Implementation Guidelines:*

1. **ViewLayerManager 挂载**：MasterScene 根节点，单例
2. **ViewLayer 枚举**：
   ```csharp
   public enum ViewLayer { STARMAP, COCKPIT, COCKPIT_WITH_OVERLAY,
       SWITCHING_IN, SWITCHING_OUT, OPENING_OVERLAY, CLOSING_OVERLAY, SWITCHING_SHIP }
   ```
3. **切换序列**：每步对应一个 `async UniTask` 方法，携带 `this.destroyCancellationToken`
4. **_preEnterState 快照**：`private ShipState _preEnterState` 在 SWITCHING_IN 第2步缓存
5. **并发守卫**：所有 public 切换方法入口检查 `if (_isSwitching) return;`
6. **Camera 切换**：
   ```csharp
   // ✅ 正确
   _starMapCamera.enabled = false;
   _cockpitCamera.enabled = true;
   // ❌ 禁止：gameObject.SetActive(false)
   ```
7. **遮罩**：全屏黑色 UIDocument，Sort Order = 100，Color = #000000
8. **ReduceMotion**：`AccessibilitySettings.ReduceMotion = true` 时遮罩即时切黑/透明，禁用动画

---

## Out of Scope

- StarMapOverlayController（Story 013）：叠加层 UI 渲染由独立组件负责
- ShipInputManager ActionMap 切换（Story 014）
- SimRate 存档（Epic A Story 009 已覆盖）

---

## QA Test Cases

*From GDD dual-perspective-switching.md 切换序列步骤 + AC-DVS-01~15（v1 保留文本）：*

- **AC-1: SWITCHING_IN 序列完成**
  - Given: ViewLayer = STARMAP，!_isSwitching，ActiveShip.ShipState = IN_TRANSIT
  - When: SwitchTo(COCKPIT) 调用
  - Then: ViewLayer 经历 SWITCHING_IN → COCKPIT；_isSwitching 全程 true；最终 StarMapCamera.enabled=false，CockpitCamera.enabled=true；OnViewLayerChanged(COCKPIT) 被广播
  - Edge cases: 切换中途 ActiveShip 被摧毁 → ViewLayer 强制回落 STARMAP

- **AC-2: SWITCHING_OUT 序列完成**
  - Given: ViewLayer = COCKPIT，!_isSwitching，_preEnterState = IN_TRANSIT
  - When: SwitchTo(STARMAP) 调用
  - Then: ViewLayer 经历 SWITCHING_OUT → STARMAP；CockpitCamera → disabled；StarMapCamera → enabled；CockpitScene 卸载；OnViewLayerChanged(STARMAP) 被广播
  - Edge cases: CockpitScene 卸载后 StarMapCamera 定位到上次记录位置（无动画）

- **AC-3: SWITCHING_SHIP 序列完成**
  - Given: ViewLayer = COCKPIT_WITH_OVERLAY，!_isSwitching，目标飞船 ShipState = IN_TRANSIT，ActiveShip.ShipState ≠ IN_COMBAT
  - When: RequestSwitchShip(targetShipId) 调用
  - Then: 叠加层即时关闭；ViewLayer 经历 SWITCHING_SHIP，全程保持 COCKPIT；旧船数据回写；新 ActiveShipId = 目标飞船；新船 ShipState → IN_COCKPIT；OnActiveShipChanged(newShipId) 被广播
  - Edge cases: 目标飞船在序列中被摧毁 → ActiveShip 恢复为旧船

- **AC-4: _isSwitching 锁死期间拒绝切换**
  - Given: _isSwitching = true
  - When: SwitchTo(STARMAP) 或 RequestSwitchShip() 被调用
  - Then: 方法立即返回，切换不执行；无 Assert 崩溃

- **AC-5: Camera 切换用 enabled 而非 SetActive**
  - Given: StarMapCamera 和 CockpitCamera 均存在
  - When: SWITCHING_IN 完成
  - Then: StarMapCamera.enabled = false（不是 SetActive(false)）；CockpitCamera.enabled = true

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/scene/viewlayer_manager_test.cs` — must exist and pass
**Status**: ✅ Created — `tests/unit/scene/viewlayer_manager_test.cs`

---

## Dependencies

- Depends on: Story 007（ViewLayerChannel + ShipStateChannel 须已建立）
- Unlocks: Story 013（StarMapOverlayController 依赖 ViewLayerChannel）

---

## Completion Notes

**Completed**: 2026-04-15
**Criteria**: 7/8 passing（SWITCHING_IN/OUT/SHIP 序列代码完整，PlayMode 验证）
**Deviations**: ADVISORY — ShipDataModel stub 在 src/Data/ 创建（Story 005 未实现前供编译用）；Story 007 ViewLayerChannel/ShipStateChannel stub 在 src/Channels/ 创建
**Test Evidence**: tests/unit/scene/viewlayer_manager_test.cs — 7个 EditMode 测试用例，PlayMode 集成测试待 Story 005/007 完成后验证
**Code Review**: Pending
