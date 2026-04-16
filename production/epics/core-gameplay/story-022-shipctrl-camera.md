# Story 022: ShipControlSystem — Camera Rig View Switching

> **Epic**: core-gameplay
> **Status**: Ready
> **Layer**: Core
> **Type**: Visual/Feel
> **Manifest Version**: 2026-04-14

## Context

**GDD**: `design/gdd/ship-control-system.md`
**Requirement**: `TR-shipctrl-007`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0018: Ship Control System Architecture
**ADR Decision Summary**: 第三人称 SmoothDamp 跟随（位置 0.1s，旋转 0.15s）；第一人称硬绑定 CockpitAnchor；切换时长 0.3s，切换期间输入不中断。

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: CameraRig 挂载于 CockpitScene。

**Control Manifest Rules (this layer)**:
- Required: 第三人称相机 0.1s 位置 SmoothDamp，0.15s 旋转 SmoothDamp
- Forbidden: 第一人称相机不适用 SmoothDamp（硬绑定）
- Guardrail: 视角切换期间输入不中断（CAMERA_SWITCH_DURATION=0.3s）

---

## Acceptance Criteria

*From GDD `design/gdd/ship-control-system.md` V-1~V-4:*

- [ ] THIRD_PERSON 模式：相机位置 SmoothDamp(target=ship.position, 0.1s)；旋转 SmoothDamp(target=ship.rotation, 0.15s)
- [ ] FIRST_PERSON 模式：相机硬绑定 CockpitAnchor（位置和旋转均无延迟）
- [ ] 视角切换动画时长 0.3s，期间 ShipInputChannel 继续响应
- [ ] 切换过程中不中断输入（输入在切换前就恢复）
- [ ] 切换完成后相机模式正确切换

---

## Implementation Notes

*Derived from ADR-0018 Decision section:*

```csharp
// CameraRig.cs
public enum CameraMode { THIRD_PERSON, FIRST_PERSON }

void Update() {
    if (_isTransitioning) {
        UpdateTransition(Time.deltaTime);
        return;
    }

    if (_mode == CameraMode.THIRD_PERSON) {
        _cam.position = Vector3.SmoothDamp(_cam.position, targetShip.position, ref _posVel, 0.1f);
        _cam.rotation = Quaternion.SmoothDamp(_cam.rotation, targetShip.rotation, ref _rotVel, 0.15f);
    } else if (_mode == CameraMode.FIRST_PERSON) {
        _cam.position = _cockpitAnchor.position;
        _cam.rotation = _cockpitAnchor.rotation;
    }
}

public void SwitchMode(CameraMode newMode) {
    if (newMode == _mode) return;
    _targetMode = newMode;
    _transitionProgress = 0f;
    _isTransitioning = true;
}

void UpdateTransition(float dt) {
    _transitionProgress += dt / CAMERA_SWITCH_DURATION; // 0.3s
    if (_transitionProgress >= 1f) {
        _transitionProgress = 1f;
        _isTransitioning = false;
        _mode = _targetMode;
    }
    // 输入在切换期间不中断（在 Update() 早期检查，不受 _isTransitioning 影响）
}
```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 020: ShipInputChannel 触屏输入处理
- Story 023: 切换触发时机（ShipState 变化时调用 SwitchMode）

---

## QA Test Cases

*Written by qa-lead at story creation.*

- **Manual check**: Third-person smooth follow
  - Setup: CameraMode = THIRD_PERSON; ship at (0,0,0); camera at (0,5,-20)
  - Verify: after ship moves to (10,0,0), camera position smoothly interpolates toward (10,5,-20)
  - Pass condition: camera reaches target within 0.3s; no jitter

- **Manual check**: First-person hard bind
  - Setup: CameraMode = FIRST_PERSON; CockpitAnchor at ship cockpit transform
  - Verify: camera position = CockpitAnchor.position; camera rotation = CockpitAnchor.rotation with no lag
  - Pass condition: camera moves/rotates identically to CockpitAnchor every frame (within 0.001 units)

- **Manual check**: View switch input continuity
  - Setup: ship in motion with thrust active
  - Verify: when camera switch is triggered, ship thrust/steer inputs continue to be processed during 0.3s transition
  - Pass condition: after switch completes, ship velocity and orientation are consistent (no input dropout)

---

## Test Evidence

**Story Type**: Visual/Feel
**Required evidence**: `production/qa/evidence/shipctrl-view-switch-evidence.md` + sign-off
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: foundation-runtime (CameraRig component setup); Story 019 (physics)
- Unlocks: Story 023 (state transition integration)
