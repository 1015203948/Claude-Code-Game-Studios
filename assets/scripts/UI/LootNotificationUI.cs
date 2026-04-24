using UnityEngine;
using System.Collections;
using Game.Channels;

namespace Game.UI {
    /// <summary>
    /// Loot/reward notification popup UI.
    /// Subscribes to LootNotificationChannel — shows a floating notification at screen bottom center.
    /// Also subscribes to CombatChannel for victory rewards display.
    /// </summary>
    public class LootNotificationUI : MonoBehaviour
    {
        public static LootNotificationUI Instance { get; private set; }

        [Header("Config")]
        [SerializeField] private float _displayDuration = 2f;
        [SerializeField] private float _fadeDuration = 0.5f;

        [Header("UI Elements")]
        [SerializeField] private GameObject _notificationPrefab;

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
            if (LootNotificationChannel.Instance != null) {
                LootNotificationChannel.Instance.Subscribe(ShowNotification);
            }
        }

        private void OnDisable()
        {
            if (LootNotificationChannel.Instance != null) {
                LootNotificationChannel.Instance.Unsubscribe(ShowNotification);
            }
        }

        /// <summary>
        /// Shows a notification with the given message.
        /// </summary>
        public void ShowNotification(string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            GameObject go;
            if (_notificationPrefab != null) {
                go = Instantiate(_notificationPrefab, transform);
            } else {
                go = CreateFallbackNotification(message);
            }
            go.SetActive(true);
            StartCoroutine(DisplayAndFade(go, _displayDuration, _fadeDuration));
        }

        private IEnumerator DisplayAndFade(GameObject go, float displayDuration, float fadeDuration)
        {
            // Wait for display
            yield return new WaitForSeconds(displayDuration);

            // Fade out
            var textMeshes = go.GetComponentsInChildren<TextMesh>();
            var renderers = go.GetComponentsInChildren<Renderer>();
            float elapsed = 0f;

            while (elapsed < fadeDuration) {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeDuration;

                foreach (var tm in textMeshes) {
                    Color c = tm.color;
                    c.a = 1f - t;
                    tm.color = c;
                }
                foreach (var r in renderers) {
                    foreach (var mat in r.materials) {
                        Color c = mat.color;
                        c.a = 1f - t;
                        mat.color = c;
                    }
                }

                yield return null;
            }

            if (Application.isPlaying) {
                Destroy(go);
            }
        }

        private GameObject CreateFallbackNotification(string message)
        {
            var go = new GameObject("LootNotification");
            go.transform.SetParent(transform, false);

            var tm = go.AddComponent<TextMesh>();
            tm.text = message;
            tm.fontSize = 16;
            tm.characterSize = 0.3f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = new Color(1f, 0.87f, 0.27f); // gold

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null) {
                renderer.sortingOrder = 2000;
            }

            return go;
        }
    }
}
