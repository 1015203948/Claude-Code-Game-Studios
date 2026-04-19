using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Game.Scene {
    /// <summary>
    /// Full-screen black mask for scene transitions.
    /// Requires: GameObject with CanvasGroup (full-screen black background), Sort Order = 100.
    /// When AccessibilitySettings.ReduceMotion is true, transitions are instant (no animation).
    /// </summary>
    public class TransitionMask : Game.UI.TransitionMask {
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
        public override async Task FadeInAsync(float duration, CancellationToken ct) {
            if (_canvasGroup == null) return;

            if (AccessibilitySettings.ReduceMotion) {
                _canvasGroup.alpha = 1f;
                return;
            }

            float startAlpha = _canvasGroup.alpha;
            float elapsed = 0f;

            while (elapsed < duration) {
                ct.ThrowIfCancellationRequested();
                await Task.Yield();
                elapsed += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, elapsed / duration);
            }

            _canvasGroup.alpha = 1f;
        }

        /// <summary>
        /// Fade mask to transparent.
        /// </summary>
        /// <param name="duration">Fade duration in seconds.</param>
        /// <param name="ct">Cancellation token.</param>
        public override async Task FadeOutAsync(float duration, CancellationToken ct) {
            if (_canvasGroup == null) return;

            if (AccessibilitySettings.ReduceMotion) {
                _canvasGroup.alpha = 0f;
                return;
            }

            float startAlpha = _canvasGroup.alpha;
            float elapsed = 0f;

            while (elapsed < duration) {
                ct.ThrowIfCancellationRequested();
                await Task.Yield();
                elapsed += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / duration);
            }

            _canvasGroup.alpha = 0f;
        }
    }
}
