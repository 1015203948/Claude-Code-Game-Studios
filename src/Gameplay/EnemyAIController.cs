using UnityEngine;
using MyGame;
using Game.Channels;

namespace Game.Gameplay {
    /// <summary>
    /// Enemy AI controller component attached to each enemy ship instance.
    /// Manages per-instance AI state, physics query buffers, and lifecycle.
    /// Driven by EnemySystem.Update() which calls UpdateAI() on all active instances.
    /// Implements ADR-0015 Enemy System Architecture.
    /// </summary>
    public class EnemyAIController : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────
        // Enemy Data Model
        // ─────────────────────────────────────────────────────────────────

        public string InstanceId { get; private set; }
        public string BlueprintId { get; private set; }
        public float CurrentHull { get; set; }
        public float MaxHull { get; private set; }
        public EnemyAiState AiState { get; set; }
        public string TargetPlayerId { get; private set; }
        public float FireTimer { get; set; }
        public float RandomDelay { get; set; }
        public Vector3 SpawnPosition { get; private set; }

        // ─────────────────────────────────────────────────────────────────
        // Physics Query Buffers (zero GC)
        // ─────────────────────────────────────────────────────────────────

        // Per-instance buffers — NOT static (ADR-0015 intent: each enemy gets its own).
        // Zero GC: pre-allocated at construction, never reallocated in combat loop.
        private readonly Collider[] _playerQueryBuffer = new Collider[10];
        private readonly RaycastHit[] _fireHitBuffer = new RaycastHit[1];

        // ─────────────────────────────────────────────────────────────────
        // AI State Timers
        // ─────────────────────────────────────────────────────────────────

        private float _spawnTimer;
        private float _dyingTimer;

        // ─────────────────────────────────────────────────────────────────
        // Constants (from ADR-0015 GDD Tuning Knobs)
        // ─────────────────────────────────────────────────────────────────

        private const float ENEMY_MOVE_SPEED = 15f;        // m/s
        private const float ENEMY_TURN_SPEED = 90f;       // deg/s
        private const float FLANK_ENGAGE_RANGE = 80f;     // m
        private const float FLANK_OFFSET = 30f;          // m
        private const float FIRE_ANGLE_THRESHOLD = 15f;   // degrees
        private const float WEAPON_FIRE_RATE = 1.0f;     // shots/sec
        private const float WEAPON_RANGE = 200f;         // m
        private const float DYING_DURATION = 1.2f;       // seconds
        private const float SPAWN_RADIUS = 150f;         // m

        // ─────────────────────────────────────────────────────────────────
        // Cached References
        // ─────────────────────────────────────────────────────────────────

        private Vector3 _flankTarget;
        private int _playerLayerMask;

        // ─────────────────────────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────

        private void Awake() {
            _playerLayerMask = LayerMask.GetMask("PlayerShip");
        }

        private void Start() {
            // Track player death via HealthSystem (cockpit combat path)
            HealthSystem.Instance.OnShipDying += OnEnemyDying;
            // Track player death via ShipStateChannel (U-4 bypass path: ShipDataModel.Destroy)
            // This ensures enemy AI stops targeting when player ship is destroyed without
            // going through HealthSystem (e.g., FleetDispatch unattended combat defeat)
            if (ShipStateChannel.Instance != null) {
                ShipStateChannel.Instance.Subscribe(OnShipStateChanged);
            }
        }

        private void OnDestroy() {
            if (HealthSystem.Instance != null) {
                HealthSystem.Instance.OnShipDying -= OnEnemyDying;
            }
            if (ShipStateChannel.Instance != null) {
                ShipStateChannel.Instance.Unsubscribe(OnShipStateChanged);
            }
        }

        private void OnShipStateChanged((string instanceId, ShipState newState) payload) {
            // U-4 bypass: when target player ship is destroyed via ShipDataModel.Destroy(),
            // ShipStateChannel broadcasts DESTROYED. Enemy AI transitions to DYING.
            if (payload.instanceId == TargetPlayerId
                && payload.newState == ShipState.DESTROYED
                && AiState != EnemyAiState.DYING) {
                AiState = EnemyAiState.DYING;
                _dyingTimer = 0f;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Initialization
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Initializes this enemy instance. Called by EnemySystem on spawn.
        /// </summary>
        public void Initialize(string instanceId, string blueprintId, Vector3 spawnPos, string targetPlayerId) {
            InstanceId = instanceId;
            BlueprintId = blueprintId;
            SpawnPosition = spawnPos;
            TargetPlayerId = targetPlayerId;

            // Initialize hull from blueprint (default 100 if no blueprint lookup)
            MaxHull = 100f;
            CurrentHull = MaxHull;

            AiState = EnemyAiState.SPAWNING;
            FireTimer = 0f;
            RandomDelay = Random.Range(3f, 5f);
            _spawnTimer = 0f;
            _dyingTimer = 0f;

            transform.position = spawnPos;
            transform.rotation = Quaternion.identity;
        }

        // ─────────────────────────────────────────────────────────────────
        // AI State Machine (called by EnemySystem.Update)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Drives the AI state machine for one frame.
        /// Called by EnemySystem.Update() for all active instances.
        /// </summary>
        public void UpdateAI() {
            switch (AiState) {
                case EnemyAiState.SPAWNING:
                    UpdateSpawning();
                    break;

                case EnemyAiState.APPROACHING:
                    UpdateApproach();
                    break;

                case EnemyAiState.FLANKING:
                    UpdateFlank();
                    break;

                case EnemyAiState.DYING:
                    UpdateDying();
                    break;
            }
        }

        private void UpdateSpawning() {
            // AC-1: SPAWNING is stationary — RandomDelay then transitions to APPROACHING
            _spawnTimer += global::Gameplay.SimClock.Instance != null
                ? global::Gameplay.SimClock.Instance.DeltaTime
                : Time.deltaTime;
            if (_spawnTimer >= RandomDelay) {
                AiState = EnemyAiState.APPROACHING;
            }
        }

        private void UpdateApproach() {
            // AC-2: APPROACHING — move in straight line toward player at ENEMY_MOVE_SPEED
            // Uses SimClock.DeltaTime for fast-forward correctness
            float dt = global::Gameplay.SimClock.Instance != null
                ? global::Gameplay.SimClock.Instance.DeltaTime
                : Time.deltaTime;

            Vector3 toPlayer = GetPlayerPosition() - transform.position;
            float distance = toPlayer.magnitude;

            // Turn toward player
            if (distance > 0.001f) {
                Quaternion targetRot = Quaternion.LookRotation(toPlayer.normalized);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, ENEMY_TURN_SPEED * dt);
            }

            // Move forward
            transform.position += transform.forward * ENEMY_MOVE_SPEED * dt;

            // Transition to FLANKING when within FLANK_ENGAGE_RANGE
            if (distance <= FLANK_ENGAGE_RANGE) {
                ComputeFlankingTarget();
                AiState = EnemyAiState.FLANKING;
            }
        }

        private void UpdateFlank() {
            // AC-3: FLANKING — move toward flank target, fire when aimAngle ≤ 15° + FireTimer ready
            // Uses SimClock.DeltaTime for fast-forward correctness
            float dt = global::Gameplay.SimClock.Instance != null
                ? global::Gameplay.SimClock.Instance.DeltaTime
                : Time.deltaTime;

            Vector3 toFlank = _flankTarget - transform.position;
            float distance = toFlank.magnitude;

            // Turn toward flank target
            if (distance > 0.001f) {
                Quaternion flankRot = Quaternion.LookRotation(toFlank.normalized);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, flankRot, ENEMY_TURN_SPEED * dt);
            }

            // Move forward
            transform.position += transform.forward * ENEMY_MOVE_SPEED * dt;

            // Accumulate fire timer
            FireTimer += dt;

            // Fire when aim angle is within threshold and cooldown elapsed
            if (FireTimer >= (1f / WEAPON_FIRE_RATE) && EvaluateAimAngle() <= FIRE_ANGLE_THRESHOLD) {
                FireRaycast();
                FireTimer = 0f;
            }
        }

        private void ComputeFlankingTarget() {
            // E-5: flank target is 30m behind player, offset ±5m on right/left
            Vector3 playerPos = GetPlayerPosition();
            Transform playerTransform = FindPlayerTransform();

            if (playerTransform != null) {
                Vector3 playerForward = playerTransform.forward;
                Vector3 playerRight = playerTransform.right;
                float offsetX = InstanceId.EndsWith("0") ? -5f : +5f;
                _flankTarget = playerPos + (-playerForward * FLANK_OFFSET) + (playerRight * offsetX);
            } else {
                // Fallback: position behind current player position
                _flankTarget = playerPos + new Vector3(0f, 0f, -FLANK_OFFSET);
            }
        }

        private Transform FindPlayerTransform() {
            // Try to find player ship transform via GameDataManager
            var ship = GameDataManager.Instance?.GetShip(TargetPlayerId);
            if (ship != null) {
                // Ship is a data model — find associated GameObject by tag
                var go = GameObject.FindGameObjectWithTag("PlayerShip");
                return go?.transform;
            }
            return null;
        }

        private void UpdateDying() {
            // Uses SimClock.DeltaTime for fast-forward correctness
            _dyingTimer += global::Gameplay.SimClock.Instance != null
                ? global::Gameplay.SimClock.Instance.DeltaTime
                : Time.deltaTime;
            if (_dyingTimer >= DYING_DURATION) {
                EnemySystem.Instance?.DespawnEnemy(InstanceId);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Health Integration
        // ─────────────────────────────────────────────────────────────────

        private void OnEnemyDying(string instanceId) {
            if (instanceId == InstanceId && AiState != EnemyAiState.DYING) {
                AiState = EnemyAiState.DYING;
                _dyingTimer = 0f;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Physics Queries (Story 011 / Story 010)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Gets the current player ship position using OverlapSphereNonAlloc.
        /// Zero GC: uses pre-allocated _playerQueryBuffer.
        /// If player collider overlaps with other colliders, retries up to 3 times
        /// by shifting 10m along the approach direction each time.
        /// </summary>
        public Vector3 GetPlayerPosition() {
            Vector3 searchCenter = transform.position;
            const int maxRetries = 3;
            const float retryOffset = 10f;

            for (int attempt = 0; attempt <= maxRetries; attempt++) {
                int count = Physics.OverlapSphereNonAlloc(
                    searchCenter,
                    FLANK_ENGAGE_RANGE * 2f,
                    _playerQueryBuffer,
                    _playerLayerMask);

                for (int i = 0; i < count; i++) {
                    if (_playerQueryBuffer[i].CompareTag("PlayerShip")) {
                        return _playerQueryBuffer[i].transform.position;
                    }
                }

                // No player found — shift along forward direction for retry
                searchCenter += transform.forward * retryOffset;
            }

            return Vector3.zero;
        }

        /// <summary>
        /// Fires a raycast toward the player. Used in FLANKING state (Story 010).
        /// Zero GC: uses pre-allocated _fireHitBuffer.
        /// If initial ray starts inside a collider, retries up to 3 times
        /// by shifting 10m along the fire direction each time.
        /// </summary>
        public void FireRaycast() {
            Vector3 fireOrigin = transform.position + transform.forward * 1f;
            const int maxRetries = 3;
            const float retryOffset = 10f;

            for (int attempt = 0; attempt <= maxRetries; attempt++) {
                int hitCount = Physics.RaycastNonAlloc(
                    fireOrigin,
                    transform.forward,
                    _fireHitBuffer,
                    WEAPON_RANGE,
                    _playerLayerMask);

                if (hitCount > 0) {
                    Collider hit = _fireHitBuffer[0].collider;
                    if (hit.CompareTag("PlayerShip") && HealthSystem.Instance != null) {
                        HealthSystem.Instance.ApplyDamage(TargetPlayerId, 8f, DamageType.Physical);
                        return; // Hit player — done
                    }
                    // Hit something but not player — retry further along
                    fireOrigin += transform.forward * retryOffset;
                } else {
                    // No hit at all — done
                    return;
                }
            }
        }

        /// <summary>
        /// Evaluates the current aim angle toward the player.
        /// </summary>
        public float EvaluateAimAngle() {
            Vector3 toPlayer = (GetPlayerPosition() - transform.position);
            if (toPlayer.sqrMagnitude < 0.001f) return float.MaxValue;
            return Vector3.Angle(transform.forward, toPlayer.normalized);
        }

        // ─────────────────────────────────────────────────────────────────
        // Force Despawn (for immediate cleanup)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Immediately despawns this enemy, skipping any DYING VFX wait.
        /// </summary>
        public void ForceDespawn() {
            AiState = EnemyAiState.DYING;
            _dyingTimer = DYING_DURATION; // skip VFX wait
        }
    }

    /// <summary>
    /// AI states for enemy ships. SPAWNING is the entry state.
    /// </summary>
    public enum EnemyAiState {
        SPAWNING,
        APPROACHING,
        FLANKING,
        DYING
    }
}
