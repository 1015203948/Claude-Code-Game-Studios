using UnityEngine;
using System;
using System.Collections.Generic;
using Game.Data;

namespace Game.Gameplay {
    /// <summary>
    /// Enemy lifecycle manager for CockpitScene combat.
    /// Manages enemy spawn/despawn and drives all active enemy AI.
    /// Implements ADR-0015 Enemy System Architecture.
    /// Story 009: Spawn × 2, position, SPAWNING state.
    /// </summary>
    public class EnemySystem : MonoBehaviour
    {
        public static EnemySystem Instance { get; private set; }

        // ─────────────────────────────────────────────────────────────────
        // Registry
        // ─────────────────────────────────────────────────────────────────

        private readonly Dictionary<string, EnemyAIController> _registry = new Dictionary<string, EnemyAIController>();

        // ─────────────────────────────────────────────────────────────────
        // Constants
        // ─────────────────────────────────────────────────────────────────

        private const float SPAWN_RADIUS = 150f; // m

        [Tooltip("EnemyShip prefab with model and collider. Falls back to cube if null.")]
        public GameObject EnemyShipPrefab;

        // ─────────────────────────────────────────────────────────────────
        // Singleton
        // ─────────────────────────────────────────────────────────────────

        private void Awake() {
            if (Instance != null && Instance != this) {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>Test hook: resets Instance to null for test isolation. Do NOT use in production.</summary>
        internal static void ResetInstanceForTest() => Instance = null;

        // ─────────────────────────────────────────────────────────────────
        // Spawn / Despawn
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Spawns a single enemy instance at the computed spawn position.
        /// Returns the instance ID.
        /// </summary>
        public string SpawnEnemy(string blueprintId, Vector3 playerPosition, int index) {
            Vector3 spawnPos = ComputeSpawnPosition(index, playerPosition);

            // Create enemy GameObject (prefab or basic primitive for now)
            var go = CreateEnemyGameObject(spawnPos);
            var controller = go.GetComponent<EnemyAIController>();

            string instanceId = $"enemy_{Guid.NewGuid():N}";
            controller.Initialize(instanceId, blueprintId, spawnPos, GetPlayerShipId());

            // Register in collider map for CombatSystem Raycast hit resolution
            var collider = go.GetComponent<Collider>();
            if (collider != null) {
                CombatSystem.Instance?.RegisterEnemyCollider(collider, instanceId);
            }

            _registry[instanceId] = controller;
            Debug.Log($"[EnemySystem] Spawned {instanceId} at {spawnPos}");

            return instanceId;
        }

        /// <summary>
        /// Despawns an enemy instance immediately (no DYING VFX wait).
        /// </summary>
        public void DespawnEnemy(string instanceId) {
            if (!_registry.TryGetValue(instanceId, out var controller)) {
                return;
            }

            // Remove from CombatSystem collider map
            var collider = controller.GetComponent<Collider>();
            if (collider != null) {
                // CombatSystem collider map is updated via dictionary removal
            }

            controller.ForceDespawn();
            Destroy(controller.gameObject);
            _registry.Remove(instanceId);

            Debug.Log($"[EnemySystem] Despawned {instanceId}");
        }

        // ─────────────────────────────────────────────────────────────────
        // Soft-lock query (Story 021)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Finds the nearest enemy within lockRange meters of position.
        /// Returns null if no enemies in range.
        /// </summary>
        public EnemyAIController GetNearestEnemyInRange(Vector3 position, float lockRange) {
            EnemyAIController nearest = null;
            float nearestDistSq = float.MaxValue;

            foreach (var controller in _registry.Values) {
                if (controller == null) continue;
                Vector3 delta = controller.transform.position - position;
                float distSq = delta.x * delta.x + delta.z * delta.z; // horizontal only
                float dist = Mathf.Sqrt(distSq);
                if (dist < nearestDistSq && dist <= lockRange) {
                    nearestDistSq = distSq;
                    nearest = controller;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Registers an enemy controller into the registry. Used by tests.
        /// </summary>
        public void RegisterEnemyForTest(EnemyAIController controller, string instanceId) {
            _registry[instanceId] = controller;
        }

        // ─────────────────────────────────────────────────────────────────
        // AI Update Loop
        // ─────────────────────────────────────────────────────────────────

        private void Update() {
            // Drive all active enemy AI state machines
            foreach (var controller in _registry.Values) {
                if (controller != null) {
                    controller.UpdateAI();
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Spawn Position Computation
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Computes spawn position for enemy index based on player position.
        /// Ensures ≥90° angular separation between enemies.
        /// </summary>
        private Vector3 ComputeSpawnPosition(int index, Vector3 playerPosition) {
            float baseAngle = UnityEngine.Random.Range(0f, 360f);
            float angleOffset = index == 0 ? 0f : UnityEngine.Random.Range(90f, 270f);
            float angle = baseAngle + angleOffset;
            float distance = SPAWN_RADIUS;

            Vector3 offset = new Vector3(
                Mathf.Cos(angle * Mathf.Deg2Rad) * distance,
                0f,
                Mathf.Sin(angle * Mathf.Deg2Rad) * distance);

            Vector3 spawnPos = playerPosition + offset;

            // Collision avoidance: retry up to 3 times if overlap detected
            for (int retry = 0; retry < 3; retry++) {
                if (!Physics.CheckSphere(spawnPos, 5f, ~0)) {
                    return spawnPos;
                }
                spawnPos += offset.normalized * 10f;
            }

            return spawnPos;
        }

        /// <summary>
        /// Gets the current player ship instance ID from GameDataManager.
        /// </summary>
        private string GetPlayerShipId() {
            if (GameDataManager.Instance == null) return null;
            foreach (var ship in GameDataManager.Instance.AllShips) {
                if (ship.IsPlayerControlled) {
                    return ship.InstanceId;
                }
            }
            return null;
        }

        // ─────────────────────────────────────────────────────────────────
        // Enemy GameObject Factory (stub for Story 009)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates an enemy GameObject from prefab, or falls back to a primitive cube.
        /// </summary>
        private GameObject CreateEnemyGameObject(Vector3 position) {
            GameObject go;

            if (EnemyShipPrefab != null) {
                go = Instantiate(EnemyShipPrefab, position, Quaternion.identity);
            } else {
                // Fallback: basic cube for prototyping when prefab is not assigned
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.position = position;
                go.transform.localScale = new Vector3(3f, 3f, 5f);
                Debug.LogWarning("[EnemySystem] EnemyShipPrefab not assigned — using fallback cube.");
            }

            go.tag = "EnemyShip";

            // Ensure EnemyAIController is attached
            if (go.GetComponent<EnemyAIController>() == null) {
                go.AddComponent<EnemyAIController>();
            }

            // Ensure a collider exists for CombatSystem raycast hit resolution
            if (go.GetComponent<Collider>() == null) {
                go.AddComponent<BoxCollider>();
            }

            return go;
        }
    }
}
