using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Game.Channels;

namespace Game.Effects {
    /// <summary>
    /// Object pool for hit spark particle effects.
    /// Subscribes to WeaponFiredChannel — spawns sparks at hit point when weapon hits enemy.
    /// </summary>
    public class HitVFXPool : MonoBehaviour
    {
        public static HitVFXPool Instance { get; private set; }

        [Header("Config")]
        [SerializeField] private int _poolSize = 10;
        [SerializeField] private float _sparkDuration = 0.3f;

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
            if (WeaponFiredChannel.Instance != null) {
                WeaponFiredChannel.Instance.Subscribe(OnWeaponFired);
            }
        }

        private void OnDisable()
        {
            if (WeaponFiredChannel.Instance != null) {
                WeaponFiredChannel.Instance.Unsubscribe(OnWeaponFired);
            }
        }

        /// <summary>
        /// Spawns hit sparks at the given world position with a forward direction.
        /// </summary>
        public void SpawnHitSparks(Vector3 position, Vector3 normal)
        {
            GameObject go = GetFromPool();
            if (go == null) return;

            go.transform.position = position;
            go.transform.rotation = Quaternion.LookRotation(normal);
            go.SetActive(true);

            var ps = go.GetComponent<ParticleSystem>();
            if (ps != null) {
                ps.Play();
            }

            StartCoroutine(ReturnAfterDelay(go, _sparkDuration));
        }

        private void OnWeaponFired(WeaponFiredPayload payload)
        {
            if (!payload.Hit || !payload.HitPoint.HasValue) return;
            Vector3 normal = -payload.Direction;
            SpawnHitSparks(payload.HitPoint.Value, normal);
        }

        private void PreWarm()
        {
            for (int i = 0; i < _poolSize; i++) {
                var go = CreateSparkObject();
                go.SetActive(false);
                _pool.Enqueue(go);
            }
        }

        private GameObject GetFromPool()
        {
            if (_pool.Count > 0) return _pool.Dequeue();
            return CreateSparkObject();
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

        private GameObject CreateSparkObject()
        {
            var go = new GameObject("HitSpark");
            go.transform.SetParent(transform);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.2f;
            main.startLifetime = 0.2f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.87f, 0.27f, 1f),   // #FFDD44
                new Color(1f, 0.53f, 0f, 0f));       // #FF8800
            main.gravityModifier = 1f;
            main.maxParticles = 8;
            main.playOnAwake = false;
            main.scalingMode = ParticleSystemScalingMode.Local;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0f, new ParticleSystem.MinMaxCurve(4), 1, 0.05f)
            });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 30f;
            shape.radius = 0.1f;

            return go;
        }
    }
}
