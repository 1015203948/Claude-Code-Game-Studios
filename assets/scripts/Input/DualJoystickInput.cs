// MIT License - Copyright (c) 2026 Game Studios
/// <summary>
/// Dual virtual joystick touch input handler for cockpit.
/// Uses Unity's built-in Input.touches to track finger IDs.
/// Left half of screen = Thrust joystick (magnitude only, no reverse).
/// Right half of screen = Aim joystick (full directional -1 to 1).
/// Dead zone: 0.08f. Normalized formula: Clamp01((|offset| - 0.08f) / 0.92f).
/// Only processes input when ViewLayer == COCKPIT.
/// ShipInputChannel broadcasts results every frame.
/// </summary>
namespace Game.Inputs {
    using UnityEngine;
    using Game.Scene;
    using Game.Channels;
    using System.Collections.Generic;

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
        private const float JOYSTICK_RADIUS = 100f; // max joystick radius in pixels

        // ─── Private State ─────────────────────────────────────────────────
        private int _thrustFingerId = -1;
        private int _aimFingerId = -1;
        private Vector2 _thrustOrigin;
        private Vector2 _aimOrigin;
        private bool _isInCockpit;

        // Track touches per frame to detect new/ended
        private List<int> _previousTouchIds = new List<int>();

        // ─── Lifecycle ──────────────────────────────────────────────────────
        private void OnEnable() {
            _viewLayerChannel.Subscribe(OnViewLayerChanged);
        }

        private void OnDisable() {
            _viewLayerChannel.Unsubscribe(OnViewLayerChanged);
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

            ProcessTouches();

            // Broadcast current state
            _shipInputChannel?.RaiseThrust(ThrustInput.magnitude);
            _shipInputChannel?.RaiseAim(AimInput);
        }

        // ─── Touch Processing ───────────────────────────────────────────────
        private void ProcessTouches() {
            var currentIds = new HashSet<int>();

            for (int i = 0; i < Input.touchCount; i++) {
                Touch touch = Input.GetTouch(i);
                currentIds.Add(touch.fingerId);

                bool isLeftHalf = touch.position.x < Screen.width * 0.5f;

                switch (touch.phase) {
                    case TouchPhase.Began:
                        if (isLeftHalf && _thrustFingerId == -1) {
                            _thrustFingerId = touch.fingerId;
                            _thrustOrigin = touch.position;
                        } else if (!isLeftHalf && _aimFingerId == -1) {
                            _aimFingerId = touch.fingerId;
                            _aimOrigin = touch.position;
                        }
                        break;

                    case TouchPhase.Moved:
                    case TouchPhase.Stationary:
                        if (touch.fingerId == _thrustFingerId) {
                            UpdateThrust(touch.position);
                        } else if (touch.fingerId == _aimFingerId) {
                            UpdateAim(touch.position);
                        }
                        break;

                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        if (touch.fingerId == _thrustFingerId) {
                            _thrustFingerId = -1;
                            ThrustInput = Vector2.zero;
                            RawLeftStickX = 0f;
                        } else if (touch.fingerId == _aimFingerId) {
                            _aimFingerId = -1;
                            AimInput = Vector2.zero;
                        }
                        break;
                }
            }

            // Check for touches that ended without proper phase detection
            _previousTouchIds.RemoveAll(id => currentIds.Contains(id));
            foreach (var id in _previousTouchIds) {
                if (id == _thrustFingerId) {
                    _thrustFingerId = -1;
                    ThrustInput = Vector2.zero;
                    RawLeftStickX = 0f;
                } else if (id == _aimFingerId) {
                    _aimFingerId = -1;
                    AimInput = Vector2.zero;
                }
            }

            _previousTouchIds.Clear();
            foreach (var id in currentIds) {
                _previousTouchIds.Add(id);
            }
        }

        private void UpdateThrust(Vector2 position) {
            Vector2 delta = position - _thrustOrigin;
            float magnitude = Mathf.Clamp01(delta.magnitude / JOYSTICK_RADIUS);
            magnitude = Normalize(magnitude);
            ThrustInput = Vector2.up * magnitude;
            // C-4: expose raw left stick X for ShipControlSystem steering blend
            RawLeftStickX = Normalize(delta.x);
        }

        private void UpdateAim(Vector2 position) {
            Vector2 delta = position - _aimOrigin;
            float normalizedX = Normalize(delta.x);
            float normalizedY = Normalize(delta.y);
            AimInput = new Vector2(normalizedX, normalizedY);
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
            _previousTouchIds.Clear();
        }
    }
}