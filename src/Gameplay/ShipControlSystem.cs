// MIT License - Copyright (c) 2026 Game Studios
namespace Game.Gameplay {
    using UnityEngine;
    using Game.Inputs;
    using Game.Channels;
    using Game.Data;

    /// <summary>
    /// Cockpit physics controller — applies thrust and turn forces to Rigidbody
    /// based on DualJoystickInput output.
    ///
    /// Physics implementation (Story 019):
    /// - P-1: AddForce thrust in transform.forward direction
    /// - P-2: Soft speed clamp via opposing AddForce when speed > SHIP_MAX_SPEED
    /// - P-3: NO direct velocity assignment — forces only
    /// - P-4: MoveRotation turn (NOT angularVelocity direct assignment)
    /// - P-5: angularVelocity zeroed at start of FixedUpdate
    /// - P-6: Y position locked to FLIGHT_PLANE_Y
    ///
    /// Input processing (Story 020):
    /// - C-2: Dead zone DEAD_ZONE=0.08
    /// - C-4: Aim assist steer_total = steer_left + 0.5 × steer_right
    ///
    /// Soft-lock implementation (Story 021):
    /// - L-1: SoftLockTarget = nearest enemy within LOCK_RANGE (80m)
    /// - L-2: Target persists until out of range or destroyed
    /// - L-3: FireRequested when aimAngle ≤ FIRE_ANGLE_THRESHOLD (15°)
    ///
    /// State transitions (Story 023):
    /// - S-1: → IN_COCKPIT: cache params, reset soft-lock, enable input, THIRD_PERSON camera
    /// - S-2: IN_COCKPIT → IN_COMBAT: no cleanup, controls continue
    /// - S-3: IN_COCKPIT → DOCKED: disable input, clear soft-lock, preserve velocity
    /// - S-4: → DESTROYED: full cleanup + velocity=zero + isKinematic=true
    /// </summary>
    public class ShipControlSystem : MonoBehaviour {
        // ─── Serialized Fields ───────────────────────────────
        [SerializeField] private ShipStateChannel  _shipStateChannel;
        [SerializeField] private ViewLayerChannel  _viewLayerChannel;
        [SerializeField] private DualJoystickInput _dualJoystick;
        [SerializeField] private CameraRig _cameraRig;

        [Header("Physics (reference values — override via ShipDataModel at runtime)")]
        [SerializeField] private float _thrustPower = 15f;  // m/s²
        [SerializeField] private float _turnSpeed   = 120f; // deg/s

        [Header("Soft-lock stub")]
        [SerializeField] private float _fireAngleThreshold = 15f; // degrees — L-3: fire threshold
        [SerializeField] private float _fireRate           = 1f;  // seconds

        // ─── Physics Constants (Story 019) ─────────────────
        private const float SHIP_MAX_SPEED  = 12f; // m/s
        private const float FLIGHT_PLANE_Y  = 0f;
        private const float SPEED_CLAMP_FORCE = 50f; // opposing force magnitude

        // ─── Input Processing Constants (Story 020) ───────
        private const float DEAD_ZONE        = 0.08f;  // C-2: input dead zone
        private const float AIM_ASSIST_COEFF = 0.5f;   // C-4: right stick contribution

        // ─── Soft-lock Constants (Story 021) ──────────────
        private const float LOCK_RANGE = 80f; // meters — L-1: range for enemy acquisition

        // ─── Public Output ──────────────────────────────────
        public Transform SoftLockTarget => _softLockTarget;
        public float    WeaponCooldown => _weaponCooldown;

        /// <summary>Fired when aim angle enters the fire threshold. CombatSystem subscribes.</summary>
        public event System.Action FireRequested;

        /// <summary>Fired every frame with the current aim angle. HUD subscribes.</summary>
        public event System.Action<float> OnAimAngleChanged;

        /// <summary>Fired when soft-lock target is lost (S-3, S-4). HUD subscribes to hide lock indicator.</summary>
        public event System.Action OnLockLost;

        // ─── Private State ─────────────────────────────────
        private Rigidbody _rb;
        private Transform _softLockTarget;
        private float    _weaponCooldown;
        private float    _aimAngle;
        private bool     _inputEnabled;
        private Vector2  _lastThrustInput;
        private Vector2  _lastAimInput;
        private float    _cachedThrustPower;
        private float    _cachedTurnSpeed;
        private string   _activeShipId;

        // ─── Lifecycle ─────────────────────────────────────

        private void Awake() {
            _rb = GetComponent<Rigidbody>();
            Debug.Assert(_rb != null, "[ShipControlSystem] Rigidbody required on same GameObject");
        }

        private void OnEnable() {
            _shipStateChannel.Subscribe(OnShipStateChanged);
            _viewLayerChannel?.Subscribe(OnViewLayerChanged);
        }

        private void OnDisable() {
            _shipStateChannel.Unsubscribe(OnShipStateChanged);
            _viewLayerChannel?.Unsubscribe(OnViewLayerChanged);
            _inputEnabled = false;
        }

        private void OnViewLayerChanged(Game.Scene.ViewLayer layer) {
            // No-op: view layer changes don't directly affect ship control state
            // Input routing is handled by ShipInputManager via ViewLayerChannel
        }

        // ─── ShipState Handler ──────────────────────────────

        private void OnShipStateChanged(string instanceId, ShipState newState) {
            // S-1: → IN_COCKPIT — cache params, reset soft-lock, enable input
            if (newState == ShipState.IN_COCKPIT) {
                _activeShipId = instanceId;
                var shipData = GameDataManager.Instance?.GetShip(instanceId);
                if (shipData != null) {
                    _cachedThrustPower = shipData.GetThrustPower();
                    _cachedTurnSpeed = shipData.GetTurnSpeed();
                }
                _softLockTarget = null;
                _aimAngle = 360f;
                _weaponCooldown = 0f;
                EnableInputListening();

                // S-1: Camera → THIRD_PERSON
                _cameraRig?.SwitchMode(CameraRig.CameraMode.THIRD_PERSON);
                return;
            }

            // S-2: IN_COCKPIT → IN_COMBAT — no cleanup, controls continue
            if (newState == ShipState.IN_COMBAT) {
                // No action — controls continue uninterrupted
                return;
            }

            // S-3: IN_COCKPIT → DOCKED — disable input, clear soft-lock, preserve velocity
            if (newState == ShipState.DOCKED) {
                DisableInputListening();
                ClearSoftLock();
                OnLockLost?.Invoke();
                ClearFingerIdTracking();
                // S-3: velocity NOT reset — preserved per AC-CTRL-05
                return;
            }

            // S-4: → DESTROYED — full cleanup
            if (newState == ShipState.DESTROYED) {
                DisableInputListening();
                ClearSoftLock();
                OnLockLost?.Invoke();
                ClearFingerIdTracking();

                // Full cleanup: zero velocity and set kinematic
                if (_rb != null) {
                    _rb.velocity = Vector3.zero;
                    _rb.angularVelocity = Vector3.zero;
                    _rb.isKinematic = true;
                }
                return;
            }
        }

        private void EnableInputListening() {
            _inputEnabled = true;
        }

        private void DisableInputListening() {
            _inputEnabled = false;
        }

        private void ClearSoftLock() {
            _softLockTarget = null;
            _aimAngle = 360f;
        }

        private void ClearFingerIdTracking() {
            // Notify DualJoystickInput to release any tracked finger IDs
            _dualJoystick?.ResetFingerTracking();
        }

        // ─── Physics Update (Story 019: P-1~P-6) ───────────

        private void FixedUpdate() {
            if (!_inputEnabled) return;

            // P-5: Zero angular velocity at start of frame
            _rb.angularVelocity = Vector3.zero;

            _lastThrustInput = _dualJoystick.ThrustInput;
            _lastAimInput    = _dualJoystick.AimInput;

            // P-1: Apply thrust in forward direction
            ApplyThrust();

            // P-2: Soft speed clamp via opposing AddForce
            ApplySpeedClamp();

            // P-4: Turn via MoveRotation (NOT angularVelocity direct assignment)
            ApplyTurn();
        }

        private void ApplyThrust() {
            // AC-CTRL-04: Only forward thrust (no reverse), no steering influence
            // P-1: AddForce in transform.forward
            float thrustPower = _cachedThrustPower > 0f ? _cachedThrustPower : _thrustPower;
            if (_lastThrustInput.magnitude > 0f) {
                _rb.AddForce(
                    transform.forward * thrustPower * _lastThrustInput.magnitude,
                    ForceMode.Force);
            }
        }

        private void ApplySpeedClamp() {
            // P-2: Soft speed clamp — opposing AddForce when over SHIP_MAX_SPEED
            // P-3: NO rb.velocity = ... — forces only
            Vector3 horizontalVel = new Vector3(_rb.velocity.x, 0f, _rb.velocity.z);
            float speed = horizontalVel.magnitude;

            if (speed > SHIP_MAX_SPEED) {
                Vector3 opposingDir = -horizontalVel.normalized;
                float excessRatio = (speed - SHIP_MAX_SPEED) / SHIP_MAX_SPEED;
                _rb.AddForce(opposingDir * SPEED_CLAMP_FORCE * excessRatio, ForceMode.Force);
            }
        }

        private void ApplyTurn() {
            // C-4: Aim assist blend — steer_total = steer_left + AIM_ASSIST_COEFF * steer_right
            float steerLeft  = ApplyDeadZone(_dualJoystick.RawLeftStickX);
            float steerRight = ApplyDeadZone(_lastAimInput.x);
            float steer = steerLeft + AIM_ASSIST_COEFF * steerRight;

            if (Mathf.Abs(steer) < 0.01f) return;

            float turnSpeed = _cachedTurnSpeed > 0f ? _cachedTurnSpeed : _turnSpeed;
            float turnDegrees = turnSpeed * steer * Time.fixedDeltaTime;
            Quaternion turnRotation = Quaternion.Euler(0f, turnDegrees, 0f);
            _rb.MoveRotation(_rb.rotation * turnRotation);
        }

        /// <summary>
        /// C-2: Normalizes raw input to [0,1] or [-1,1] after dead zone.
        /// Returns 0 if |offset| lt DEAD_ZONE; otherwise returns sign * normalized magnitude.
        /// </summary>
        private float ApplyDeadZone(float offset) {
            if (Mathf.Abs(offset) < DEAD_ZONE) return 0f;
            return Mathf.Clamp01((Mathf.Abs(offset) - DEAD_ZONE) / (1f - DEAD_ZONE))
                   * Mathf.Sign(offset);
        }

        // ─── Flight Plane Lock (Story 019: P-6) ─────────────

        private void LateUpdate() {
            // P-6: Lock Y position to FLIGHT_PLANE_Y, zero Y velocity component
            if (_rb == null) return;

            Vector3 pos = _rb.position;
            if (Mathf.Abs(pos.y - FLIGHT_PLANE_Y) > 0.001f) {
                _rb.MovePosition(new Vector3(pos.x, FLIGHT_PLANE_Y, pos.z));
            }

            Vector3 vel = _rb.velocity;
            if (Mathf.Abs(vel.y) > 0.001f) {
                _rb.velocity = new Vector3(vel.x, 0f, vel.z);
            }
        }

        // ─── Soft-lock + Weapon Cooldown ─────────────────

        private void Update() {
            if (!_inputEnabled) return;

            // Weapon cooldown countdown
            if (_weaponCooldown > 0f) {
                _weaponCooldown -= Time.deltaTime;
            }

            // L-1/L-2: Update soft-lock target (stability rule)
            UpdateSoftLock();

            // L-3: Calculate aim angle and broadcast
            _aimAngle = CalculateAimAngle();
            OnAimAngleChanged?.Invoke(_aimAngle);

            // L-3: FireRequested when within threshold
            if (_aimAngle <= _fireAngleThreshold) {
                FireRequested?.Invoke();
            }

            // Auto-fire stub
            TryAutoFireStub();
        }

        private void UpdateSoftLock() {
            // L-2: Stability — only clear if target is gone or out of range
            if (_softLockTarget != null) {
                Vector3 delta = _softLockTarget.position - _rb.position;
                float dist = new Vector3(delta.x, 0f, delta.z).magnitude;
                bool outOfRange = dist > LOCK_RANGE;

                if (outOfRange) {
                    _softLockTarget = null;
                }
            }

            // L-1: Re-acquire if target is null
            if (_softLockTarget == null) {
                var enemy = EnemySystem.Instance?.GetNearestEnemyInRange(_rb.position, LOCK_RANGE);
                _softLockTarget = enemy?.transform;
            }
        }

        /// <summary>
        /// L-3: Computes angle between ship forward and direction to soft-lock target.
        /// Returns 360° if no target locked.
        /// </summary>
        private float CalculateAimAngle() {
            if (_softLockTarget == null) return 360f;
            Vector3 toEnemy = (_softLockTarget.position - _rb.position).normalized;
            return Vector3.Angle(transform.forward, toEnemy);
        }

        private void TryAutoFireStub() {
            // STUB: auto-fire only if soft-lock target exists
            if (_softLockTarget == null || _weaponCooldown > 0f) return;

            // Fire! (stub — no real combat system yet)
            _weaponCooldown = _fireRate;
        }

        // ─── Public API (for HUD consumers) ───────────────

        /// <summary>Current normalized thrust magnitude [0,1].</summary>
        public float GetThrustMagnitude() => _lastThrustInput.magnitude;

        /// <summary>Current aim direction [-1,1].</summary>
        public Vector2 GetAimDirection() => _lastAimInput;

        // ─── Internal physics access for tests ─────────────

        /// <summary>Exposes horizontal speed for test verification.</summary>
        public float GetHorizontalSpeed() {
            Vector3 h = _rb.velocity;
            return new Vector3(h.x, 0f, h.z).magnitude;
        }
    }
}
