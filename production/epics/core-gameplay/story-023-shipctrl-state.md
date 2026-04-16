# Story 023: ShipControlSystem — State Init/Cleanup (S-1~S-4)

> **Epic**: core-gameplay
> **Status**: Ready
> **Layer**: Core
> **Type**: State Machine
> **Manifest Version**: 2026-04-14

## Context

**GDD**: `design/gdd/ship-control-system.md`
**Requirement**: `TR-shipctrl-008`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0018: Ship Control System Architecture
**ADR Decision Summary**: 进入 IN_COCKPIT → 缓存 ThrustPower/TurnSpeed，重置 SoftLockTarget，激活输入监听；IN_COCKPIT → DOCKED → 停用输入监听，清空 SoftLockTarget，清空 fingerId，velocity=Vector3.zero；DESTROYED → 完整清理，isKinematic=true。

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: Rigidbody isKinematic API 在 Unity 6.3 无变化。

**Control Manifest Rules (this layer)**:
- Required: 输入订阅在 OnEnable/OnDisable 配对（ADR-0002）
- Forbidden: IN_COCKPIT → IN_COMBAT 转换不触发清理
- Guardrail: velocity=Vector3.zero 在 DOCKED 时不清（惯性保留）

---

## Acceptance Criteria

*From GDD `design/gdd/ship-control-system.md` S-1~S-4:*

- [ ] S-1: → IN_COCKPIT：缓存 ThrustPower/TurnSpeed；重置 SoftLockTarget；激活输入监听；CameraMode = THIRD_PERSON
- [ ] S-2: IN_COCKPIT → IN_COMBAT：不清空，操控继续运行
- [ ] S-3: IN_COCKPIT → DOCKED：停用输入监听；清空 SoftLockTarget；广播 OnLockLost；清空 fingerId 追踪；不重置 Rigidbody 速度
- [ ] S-4: → DESTROYED：执行完整 S-2 清理 + velocity=Vector3.zero + isKinematic=true

---

## Implementation Notes

*Derived from ADR-0018 Decision section:*

```csharp
// ShipControlSystem.cs
void OnShipStateChanged(string instanceId, ShipState newState) {
    if (instanceId != _playerInstanceId) return;

    switch (newState) {
        case ShipState.IN_COCKPIT:
            // S-1: 初始化
            _cachedThrust = ShipDataModel.GetThrustPower(instanceId);
            _cachedTurnSpeed = ShipDataModel.GetTurnSpeed(instanceId);
            _softLockTarget = null;
            _aimAngle = 360f;
            EnableInputListening();
            _cameraRig.SwitchMode(CameraMode.THIRD_PERSON);
            break;

        case ShipState.IN_COMBAT:
            // S-2: 不清理，操控继续
            break;

        case ShipState.DOCKED:
            // S-3: 清理输入，不重置速度
            DisableInputListening();
            ClearSoftLock();
            OnLockLost?.Invoke();
            ClearFingerIdTracking();
            // rb.velocity NOT reset (preserve momentum per S-3)
            break;

        case ShipState.DESTROYED:
            // S-4: 完整清理
            DisableInputListening();
            ClearSoftLock();
            OnLockLost?.Invoke();
            ClearFingerIdTracking();
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            break;
    }
}
```

订阅 ViewLayerChannel.OnViewLayerChanged 和 ShipStateChannel.OnShipStateChanged。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 019: FixedUpdate 物理循环
- Story 020: 输入处理细节

---

## QA Test Cases

*Written by qa-lead at story creation.*

- **AC-1**: IN_COCKPIT init caches values and resets state
  - Given: transitioning to IN_COCKPIT from DOCKED
  - When: OnShipStateChanged fires
  - Then: ThrustPower and TurnSpeed cached; SoftLockTarget = null; input listening enabled; CameraMode = THIRD_PERSON

- **AC-2**: IN_COMBAT transition does NOT clear
  - Given: in IN_COCKPIT with active SoftLockTarget and cached values; thrust is being applied
  - When: OnShipStateChanged fires with IN_COMBAT
  - Then: SoftLockTarget NOT cleared; cached values NOT reset; input continues

- **AC-3**: DOCKED cleanup preserves velocity
  - Given: in IN_COCKPIT; rb.velocity = (10, 0, 5)
  - When: OnShipStateChanged fires with DOCKED
  - Then: input listening disabled; SoftLockTarget cleared; fingerId tracking cleared; BUT rb.velocity preserved at (10, 0, 5)

- **AC-4**: DESTROYED full cleanup
  - Given: in IN_COCKPIT; rb.velocity = (10, 0, 5)
  - When: OnShipStateChanged fires with DESTROYED
  - Then: rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; rb.isKinematic = true; input disabled; SoftLockTarget cleared

---

## Test Evidence

**Story Type**: State Machine
**Required evidence**: `tests/unit/shipctrl/state_transition_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: foundation-runtime (ShipInputManager, ShipStateChannel); Story 019 (physics core); Story 020 (input processing); Story 021 (soft lock)
- Unlocks: Integration with ViewLayerManager (foundation-runtime)
