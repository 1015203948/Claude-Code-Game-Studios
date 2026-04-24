using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Game.Data;
using Game.Gameplay;

namespace Game.Effects {
    /// <summary>
    /// Object pool for explosion particle effects.
    /// Subscribes to HealthSystem.OnShipDying to spawn explosions at death locations.
    /// Also triggers camera shake on explosion.
    /// </summary>
    public class ExplosionPool : MonoBehaviour
    {
        public static ExplosionPool Instance { get; private set; }

        [Header("Config")]
        [SerializeField] private int _poolSize = 5;
        [SerializeField] private float _explosionDuration = 2f;
        [SerializeField] private float _shakeIntensity = 0.8f;
        [SerializeField] private float _shakeDuration = 0.5f;

        [Header("Prefab Fallback")]
        [SerializeField] private GameObject _explosionPrefab;

        private readonly Queue<GameObject> _pool = new Queue<GameObject>();

        private void Awake()
        {
            if (Instance != null && Instance != this) {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            PreWarm();
        }

        internal static void ResetInstanceForTest() => Instance = null;

        private void OnEnable()
        {
            if (HealthSystem.Instance != null) {
                HealthSystem.Instance.OnShipDying += OnShipDying;
            }
        }

        private void OnDisable()
        {
            if (HealthSystem.Instance != null) {
                HealthSystem.Instance.OnShipDying -= OnShipDying;
            }
        }

        /// <summary>
        /// Spawns an explosion at the given world position.
        /// </summary>
        public void SpawnExplosion(Vector3 position)
        {
            GameObject go = GetFromPool();
            if (go == null) return;

            go.transform.position = position;
            go.SetActive(true);

            // Trigger camera shake
            var cameraRig = FindObjectOfType<Game.Scene.CameraRig>();
            if (cameraRig != null) {
                cameraRig.AddShake(_shakeIntensity, _shakeDuration);
            }

            // Auto-return to pool after duration
            StartCoroutine(ReturnAfterDelay(go, _explosionDuration));
        }

        private void OnShipDying(string instanceId)
        {
            // Find the ship's world position from GameDataManager or registry
            var ship = GameDataManager.Instance?.GetShip(instanceId);
            if (ship == null) return;

            // Try to find the GameObject by instance ID (enemy ships have it)
            // For player ship, use the player transform
            Vector3 position = Vector3.zero;
            var allShips = FindObjectsOfType<EnemyAIController>();
            foreach (var ctrl in allShips) {
                if (ctrl.InstanceId == instanceId) {
                    position = ctrl.transform.position;
                    break;
                }
            }

            if (position != Vector3.zero) {
                SpawnExplosion(position);
            }
        }

        private void PreWarm()
        {
            for (int i = 0; i < _poolSize; i++) {
                var go = CreateExplosionObject();
                go.SetActive(false);
                _pool.Enqueue(go);
            }
        }

        private GameObject GetFromPool()
        {
            if (_pool.Count > 0) {
                return _pool.Dequeue();
            }
            // Pool exhausted — create on the fly
            var go = CreateExplosionObject();
            return go;
        }

        private void ReturnToPool(GameObject go)
        {
            go.SetActive(false);
            _pool.Enqueue(go);
        }

        private IEnumerator ReturnAfterDelay(GameObject go, float delay)
        {
            yield return new WaitForSeconds(delay);
            ReturnToPool(go);
        }

        private GameObject CreateExplosionObject()
        {
            if (_explosionPrefab != null) {
                return Instantiate(_explosionPrefab);
            }

            // Fallback: create a simple explosion with particle system + point light
            var go = new GameObject("Explosion");
            go.transform.SetParent(transform);

            // Particle system — simple burst
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 15f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.8f, 0.2f, 1f),
                new Color(1f, 0.3f, 0f, 0f));
            main.gravityModifier = 0.5f;
            main.maxParticles = 30;
            main.playOnAwake = false;
            main.stopAction = ParticleSystemStopAction.None;

            // Emission — burst
            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0f, new ParticleSystem.MinMaxCurve(15, 25), 1, 0.2f)
            });

            // Shape — sphere
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;

            // Point light flash
            var light = go.AddComponent<Light>();
            light.intensity = 3f;
            light.range = 15f;
            light.color = new Color(1f, 0.6f, 0.2f);

            return go;
        }
    }
}
