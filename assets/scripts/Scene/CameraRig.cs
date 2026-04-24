using UnityEngine;

namespace Game.Scene {
    /// <summary>
    /// Cockpit camera rig — manages third-person SmoothDamp vs first-person hard-bind
    /// camera modes within the cockpit scene.
    ///
    /// Story 022:
    /// - V-1: THIRD_PERSON — SmoothDamp position (0.1s), SmoothDamp rotation (0.15s)
    /// - V-2: FIRST_PERSON — hard bind to CockpitAnchor (no smoothing)
    /// - V-3: Switch animation duration 0.3s
    /// - V-4: Input uninterrupted during transition
    ///
    /// Sprint 2: Added camera shake for combat feedback.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        // ─── Public Types ─────────────────────────────────────────
        public enum CameraMode { THIRD_PERSON, FIRST_PERSON }

        // ─── Public Output ───────────────────────────────────────
        public CameraMode Mode => _mode;

        // ─── Serialized Fields ───────────────────────────────────
        [SerializeField] private Camera _camera;
        [SerializeField] private Transform _targetShip;     // ship transform for third-person follow
        [SerializeField] private Transform _cockpitAnchor;  // first-person camera anchor

        // ─── Camera Parameters ───────────────────────────────────
        [Header("Third-Person Parameters")]
        [SerializeField] private Vector3 _thirdPersonOffset = new Vector3(0f, 8f, -22f);

        // ─── Constants (Story 022) ───────────────────────────────
        private const float CAMERA_SWITCH_DURATION = 0.3f;
        private const float POSITION_SMOOTH_TIME  = 0.1f;
        private const float ROTATION_SMOOTH_TIME  = 0.15f;

        // ─── Shake Parameters ────────────────────────────────────
        [Header("Shake")]
        private float _shakeIntensity;
        private float _shakeDuration;
        private float _shakeTimer;
        private float _shakeSeed;

        // ─── Private State ────────────────────────────────────────
        private CameraMode _mode = CameraMode.FIRST_PERSON;
        private CameraMode _targetMode;
        private bool _isTransitioning;
        private float _transitionProgress;
        private Vector3 _positionVelocity;
        private Quaternion _rotationVelocity;

        // ─── Lifecycle ───────────────────────────────────────────

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>
        /// Advances the camera rig by dt seconds.
        /// Exposed for unit testing — allows deterministic time control.
        /// </summary>
        public void Tick(float dt)
        {
            // Shake always runs (stacks with movement)
            Vector3 shakeOffset = UpdateShake(dt);

            if (_isTransitioning) {
                UpdateTransition(dt);
                return;
            }

            if (_mode == CameraMode.THIRD_PERSON) {
                UpdateThirdPerson(dt, shakeOffset);
            } else {
                // V-2: hard bind to cockpit anchor every frame
                ApplyFirstPerson(shakeOffset);
            }
        }

        // ─── Public API ─────────────────────────────────────────

        /// <summary>
        /// Switches camera to the specified mode.
        /// V-3: Transition takes 0.3s; input continues uninterrupted.
        /// </summary>
        public void SwitchMode(CameraMode newMode)
        {
            if (newMode == _mode) return;
            _targetMode = newMode;
            _transitionProgress = 0f;
            _isTransitioning = true;
        }

        /// <summary>
        /// Adds a camera shake effect. Stacks with existing shake.
        /// </summary>
        public void AddShake(float intensity, float duration)
        {
            _shakeIntensity = Mathf.Max(_shakeIntensity, intensity);
            _shakeDuration = Mathf.Max(_shakeDuration, duration);
            _shakeTimer = _shakeDuration;
            _shakeSeed = Random.value * 100f;
        }

        // ─── Shake System ──────────────────────────────────────

        private Vector3 UpdateShake(float dt)
        {
            if (_shakeTimer <= 0f) return Vector3.zero;

            _shakeTimer -= dt;
            float progress = 1f - Mathf.Clamp01(_shakeTimer / _shakeDuration);
            float decay = Mathf.Exp(-progress * 5f);
            float currentIntensity = _shakeIntensity * decay;

            float offsetX = (Mathf.PerlinNoise(_shakeSeed, 0f) - 0.5f) * 2f * currentIntensity;
            float offsetY = (Mathf.PerlinNoise(0f, _shakeSeed) - 0.5f) * 2f * currentIntensity;

            return new Vector3(offsetX, offsetY, 0f);
        }

        // ─── Third-Person Update ────────────────────────────────

        private void UpdateThirdPerson(float dt, Vector3 shakeOffset)
        {
            if (_targetShip == null) return;
            if (_camera == null) return;

            // V-1: SmoothDamp position — 0.1s time constant
            Vector3 targetPos = _targetShip.position + _thirdPersonOffset;
            _camera.transform.position = Vector3.SmoothDamp(
                _camera.transform.position,
                targetPos,
                ref _positionVelocity,
                POSITION_SMOOTH_TIME,
                Mathf.Infinity,
                dt);

            // V-1: Slerp rotation — framerate-independent via dt
            Quaternion targetRot = _targetShip.rotation;
            float rotationFactor = 1f - Mathf.Exp(-ROTATION_SMOOTH_TIME * 60f * dt);
            _camera.transform.rotation = Quaternion.Slerp(
                _camera.transform.rotation,
                targetRot,
                rotationFactor);

            _camera.transform.position += shakeOffset;
        }

        // ─── First-Person Hard Bind ─────────────────────────────

        private void ApplyFirstPerson(Vector3 shakeOffset)
        {
            if (_cockpitAnchor == null) return;
            if (_camera == null) return;

            // V-2: hard bind — no smoothing, direct copy
            _camera.transform.position = _cockpitAnchor.position + shakeOffset;
            _camera.transform.rotation = _cockpitAnchor.rotation;
        }

        // ─── Transition ─────────────────────────────────────────

        private void UpdateTransition(float dt)
        {
            // V-4: Input is NOT interrupted — this method only updates camera position,
            // it does NOT affect ShipInputChannel or any input processing.

            _transitionProgress += dt / CAMERA_SWITCH_DURATION;

            if (_transitionProgress >= 1f) {
                _transitionProgress = 1f;
                _isTransitioning = false;
                _mode = _targetMode;

                // Snap to target mode immediately on completion
                if (_mode == CameraMode.FIRST_PERSON) {
                    ApplyFirstPerson(UpdateShake(0f));
                }
                return;
            }

            // Interpolate toward target during transition
            // (the lerp here is visual smoothing during the 0.3s blend;
            // the actual hard switch happens at _transitionProgress >= 1.0)
            float t = _transitionProgress;
            Vector3 startPos = _camera != null ? _camera.transform.position : Vector3.zero;
            Quaternion startRot = _camera != null ? _camera.transform.rotation : Quaternion.identity;

            Vector3 targetPos;
            Quaternion targetRot;

            if (_targetMode == CameraMode.FIRST_PERSON) {
                targetPos = _cockpitAnchor != null ? _cockpitAnchor.position : startPos;
                targetRot = _cockpitAnchor != null ? _cockpitAnchor.rotation : startRot;
            } else {
                targetPos = _targetShip != null ? _targetShip.position + _thirdPersonOffset : startPos;
                targetRot = _targetShip != null ? _targetShip.rotation : startRot;
            }

            if (_camera != null) {
                _camera.transform.position = Vector3.Lerp(startPos, targetPos, t);
                _camera.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            }
        }

        // ─── Editor Debug ────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_targetShip != null) {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(_targetShip.position + _thirdPersonOffset, 0.5f);
            }
            if (_cockpitAnchor != null) {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(_cockpitAnchor.position, 0.3f);
            }
        }
#endif
    }
}
