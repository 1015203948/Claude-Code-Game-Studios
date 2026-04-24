using UnityEngine;
using System.Collections;
using Game.Channels;

namespace Game.Effects {
    /// <summary>
    /// Damage number floating text manager.
    /// Subscribes to WeaponFiredChannel — spawns floating damage numbers at hit point when weapon hits.
    /// </summary>
    public class DamageNumberManager : MonoBehaviour
    {
        public static DamageNumberManager Instance { get; private set; }

        [Header("Config")]
        [SerializeField] private float _floatDuration = 1f;
        [SerializeField] private float _floatSpeed = 2f;
        [SerializeField] private float _fadeOutDelay = 0.6f;

        [Header("Prefab")]
        [SerializeField] private GameObject _damageNumberPrefab;

        private void Awake()
        {
            if (Instance != null && Instance != this) {
                Destroy(gameObject);
                return;
            }
            Instance = this;
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
        /// Spawns a floating damage number at the given world position.
        /// </summary>
        public void ShowDamage(float amount, Vector3 worldPosition)
        {
            GameObject go;
            if (_damageNumberPrefab != null) {
                go = Instantiate(_damageNumberPrefab, worldPosition, Quaternion.identity);
            } else {
                go = CreateFallbackNumber(worldPosition, amount);
            }
            go.SetActive(true);
            StartCoroutine(FloatAndFade(go, _floatDuration));
        }

        private void OnWeaponFired(WeaponFiredPayload payload)
        {
            if (!payload.Hit || !payload.HitPoint.HasValue) return;
            if (payload.Damage <= 0f) return;
            ShowDamage(payload.Damage, payload.HitPoint.Value + Vector3.up * 2f);
        }

        private IEnumerator FloatAndFade(GameObject go, float duration)
        {
            Vector3 startPos = go.transform.position;
            float elapsed = 0f;

            var textMeshes = go.GetComponentsInChildren<TextMesh>();
            var renderers = go.GetComponentsInChildren<Renderer>();

            while (elapsed < duration) {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                go.transform.position = startPos + Vector3.up * (_floatSpeed * t);

                if (t > _fadeOutDelay) {
                    float fadeT = (t - _fadeOutDelay) / (1f - _fadeOutDelay);
                    foreach (var tm in textMeshes) {
                        Color c = tm.color;
                        c.a = 1f - fadeT;
                        tm.color = c;
                    }
                    foreach (var r in renderers) {
                        foreach (var mat in r.materials) {
                            Color c = mat.color;
                            c.a = 1f - fadeT;
                            mat.color = c;
                        }
                    }
                }

                yield return null;
            }

            if (Application.isPlaying) {
                Destroy(go);
            }
        }

        private GameObject CreateFallbackNumber(Vector3 position, float amount)
        {
            var go = new GameObject("DamageNumber");
            go.transform.position = position;

            var tm = go.AddComponent<TextMesh>();
            tm.text = $"-{amount:F0}";
            tm.fontSize = 12;
            tm.characterSize = 0.5f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = Color.white;

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null) {
                renderer.sortingOrder = 1000;
            }

            go.AddComponent<BillboardRotation>();

            return go;
        }
    }

    /// <summary>
    /// Makes a transform face the main camera each frame.
    /// </summary>
    internal class BillboardRotation : MonoBehaviour
    {
        private void LateUpdate()
        {
            if (Camera.main != null) {
                transform.rotation = Camera.main.transform.rotation;
            }
        }
    }
}
