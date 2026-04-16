using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Game.Scene;
using Game.Channels;
using System;

namespace Game.Inputs {
    /// <summary>
    /// Dual virtual joystick touch input handler for cockpit.
    /// Uses EnhancedTouch.activeTouches to manually track finger IDs.
    /// Left half of screen = Thrust joystick (magnitude only, no reverse).
    /// Right half of screen = Aim joystick (full directional -1 to 1).
    /// Dead zone: 0.08f. Normalized formula: Clamp01((|offset| - 0.08f) / 0.92f).
    /// Only processes input when ViewLayer == COCKPIT.
    /// ShipInputChannel broadcasts results every frame.
    /// </summary>
    public class DualJoystickInput : MonoBehaviour {
        // ─── Public Output ───────────────────────────────────────────────────
        /// <summary>Thrust magnitude [0,1]. No reverse (always along transform.up).</summary>
        public Vector2 ThrustInput { get; private set; }

        /// <summary>Aim direction [-1,1] for both axes.</summary>
        public Vector2 AimInput { get; private set; }

        /// <summary>Raw left stick X [-1,1] before dead zone. Used by ShipControlSystem for steering blend (C-4).</summary>
        public float RawLeftStickX { get; private set; }

        // ─── Serialized Fields ──────────────────────────────────────────────
        [SerializeField] private ShipInputChannel _shipInputChannel;
        [SerializeField] private ViewLayerChannel _viewLayerChannel;

        // ─── Constants ─────────────────────────────────────────────────────
        private const float DEAD_ZONE = 0.08f;

        // ─── Private State ─────────────────────────────────────────────────
        private int _thrustFingerId = -1;
        private int _aimFingerId = -1;
        private Vector2 _thrustOrigin;
        private Vector2 _aimOrigin;
        private bool _isInCockpit;

        // ─── Lifecycle ──────────────────────────────────────────────────────
        private void OnEnable() {
            _viewLayerChannel.Subscribe(OnViewLayerChanged);
            // NOTE: EnhancedTouchSupport.Enable/Disable is exclusively owned by ShipInputManager (ADR-0003 R-1).
            // DualJoystickInput only reads EnhancedTouch.activeTouches — it does NOT call Enable/Disable.
        }

        private void OnDisable() {
            _viewLayerChannel.Unsubscribe(OnViewLayerChanged);
            // NOTE: EnhancedTouchSupport.Disable is exclusively owned by ShipInputManager (ADR-0003 R-1).
            ResetAllInputs();
        }

        private void OnViewLayerChanged(ViewLayer layer) {
            _isInCockpit = (layer == ViewLayer.COCKPIT);
            if (!_isInCockpit) {
                ResetAllInputs();
            }
        }

        private void Update() {
            if (!_isInCockpit) return;

            foreach (var touch in EnhancedTouch.Touch.activeTouches) {
                ProcessTouch(touch);
            }

            // Broadcast current state
            _shipInputChannel?.RaiseThrust(ThrustInput.magnitude);
            _shipInputChannel?.RaiseAim(AimInput);
        }

        // ─── Touch Processing ───────────────────────────────────────────────
        private void ProcessTouch(EnhancedTouch.Touch touch) {
            bool isLeftHalf = touch.screenPosition.x < Screen.width * 0.5f;

            if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began) {
                if (isLeftHalf && _thrustFingerId == -1) {
                    _thrustFingerId = touch.finger.index;
                    _thrustOrigin = touch.screenPosition;
                } else if (!isLeftHalf && _aimFingerId == -1) {
                    _aimFingerId = touch.finger.index;
                    _aimOrigin = touch.screenPosition;
                }
            } else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Moved ||
                       touch.phase == UnityEngine.InputSystem.TouchPhase.Stationary) {
                if (touch.finger.index == _thrustFingerId) {
                    UpdateThrust(touch);
                } else if (touch.finger.index == _aimFingerId) {
                    UpdateAim(touch);
                }
            } else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended ||
                       touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled) {
                if (touch.finger.index == _thrustFingerId) {
                    _thrustFingerId = -1;
                    ThrustInput = Vector2.zero;
                    RawLeftStickX = 0f;
                } else if (touch.finger.index == _aimFingerId) {
                    _aimFingerId = -1;
                    AimInput = Vector2.zero;
                }
            }
        }

        private void UpdateThrust(EnhancedTouch.Touch touch) {
            Vector2 delta = touch.screenPosition - _thrustOrigin;
            float magnitude = Normalize(delta.magnitude);
            ThrustInput = _thrustFingerId != -1 ? Vector2.up * magnitude : Vector2.zero;
            // C-4: expose raw left stick X for ShipControlSystem steering blend
            RawLeftStickX = _thrustFingerId != -1 ? Normalize(delta.x) : 0f;
        }

        private void UpdateAim(EnhancedTouch.Touch touch) {
            Vector2 delta = touch.screenPosition - _aimOrigin;
            if (_aimFingerId == -1) {
                AimInput = Vector2.zero;
                return;
            }
            AimInput = new Vector2(Normalize(delta.x), Normalize(delta.y));
        }

        // ─── Dead Zone Normalization ────────────────────────────────────────
        /// <summary>
        /// Normalizes a raw offset to [0,1] range after applying dead zone.
        /// Formula: Clamp01((|offset| - DEAD_ZONE) / (1 - DEAD_ZONE))
        /// </summary>
        private float Normalize(float offset) {
            return Mathf.Clamp01((Mathf.Abs(offset) - DEAD_ZONE) / (1f - DEAD_ZONE));
        }

        // ─── Helpers ──────────────────────────────────────────────────────

        /// <summary>
        /// Resets all finger tracking. Called by ShipControlSystem when DOCKED or DESTROYED.
        /// </summary>
        public void ResetFingerTracking() {
            _thrustFingerId = -1;
            _aimFingerId = -1;
        }

        private void ResetAllInputs() {
            _thrustFingerId = -1;
            _aimFingerId = -1;
            ThrustInput = Vector2.zero;
            RawLeftStickX = 0f;
            AimInput = Vector2.zero;
        }
    }
}
