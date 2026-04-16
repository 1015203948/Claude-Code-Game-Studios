using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace Game.Scene {
    /// <summary>
    /// Full-screen black mask for scene transitions.
    /// Requires: GameObject with CanvasGroup (full-screen black background), Sort Order = 100.
    /// When AccessibilitySettings.ReduceMotion is true, transitions are instant (no animation).
    /// </summary>
    public class TransitionMask : MonoBehaviour {
        [SerializeField]
        private CanvasGroup _canvasGroup;

        private void Awake() {
            if (_canvasGroup == null) {
                _canvasGroup = GetComponent<CanvasGroup>();
            }
        }

        /// <summary>
        /// Fade mask to opaque black.
        /// </summary>
        /// <param name="duration">Fade duration in seconds.</param>
        /// <param name="ct">Cancellation token.</param>
        public async UniTask FadeInAsync(float duration, CancellationToken ct) {
            if (_canvasGroup == null) return;

            if (AccessibilitySettings.ReduceMotion) {
                _canvasGroup.alpha = 1f;
                return;
            }

            float startAlpha = _canvasGroup.alpha;
            float elapsed = 0f;

            while (elapsed < duration) {
                ct.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, elapsed / duration);
                await UniTask.Yield();
            }

            _canvasGroup.alpha = 1f;
        }

        /// <summary>
        /// Fade mask to transparent.
        /// </summary>
        /// <param name="duration">Fade duration in seconds.</param>
        /// <param name="ct">Cancellation token.</param>
        public async UniTask FadeOutAsync(float duration, CancellationToken ct) {
            if (_canvasGroup == null) return;

            if (AccessibilitySettings.ReduceMotion) {
                _canvasGroup.alpha = 0f;
                return;
            }

            float startAlpha = _canvasGroup.alpha;
            float elapsed = 0f;

            while (elapsed < duration) {
                ct.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / duration);
                await UniTask.Yield();
            }

            _canvasGroup.alpha = 0f;
        }
    }
}
