// MIT License - Copyright (c) 2026 Game Studios
namespace Game.Scene {
    using System;
    using System.Collections;
    using UnityEngine;
    using UnityEngine.UIElements;
    using Game.Channels;

    /// <summary>
    /// Manages the cockpit overlay star map — switches UIDocument panelSettings
    /// between CameraSpace and ScreenSpaceOverlay, drives slide-in/out animation.
    /// ADR-0007: No second Camera for overlay (FORBIDDEN).
    /// Animations use style.translate (NOT VisualElement.transform.position).
    /// _isSwitching remains unchanged during overlay operations.
    /// </summary>
    public class StarMapOverlayController : MonoBehaviour {
        // ─── Serialized Fields ──────────────────────────────
        [SerializeField] private ViewLayerChannel _viewLayerChannel;
        [SerializeField] private UIDocument _starMapDocument;

        [Header("PanelSettings (wire in Unity Inspector — create assets manually)")]
        [SerializeField] private PanelSettings _cameraSpaceSettings;
        [SerializeField] private PanelSettings _screenOverlaySettings;

        [Header("Overlay Panel (root VisualElement of the overlay panel)")]
        [SerializeField] private VisualElement _overlayPanel;

        [Header("Animation Timing")]
        [SerializeField] private float _slideInDuration  = 0.30f;
        [SerializeField] private float _slideOutDuration = 0.20f;

        // ─── Constants ────────────────────────────────────
        private const float FALLBACK_PANEL_WIDTH = 500f;

        // ─── State ───────────────────────────────────────
        private bool _isOverlayOpen;
#pragma warning disable CS0414
        private bool _isAnimating;
#pragma warning restore CS0414
        private Coroutine _activeCoroutine;

        // ─── Unity Lifecycle ─────────────────────────────────

        private void OnEnable() {
            _viewLayerChannel.Subscribe(OnViewLayerChanged);
            EnsureOverlayHidden();
        }

        private void OnDisable() {
            _viewLayerChannel.Unsubscribe(OnViewLayerChanged);
        }

        // ─── Event Handler ─────────────────────────────────

        private void OnViewLayerChanged(ViewLayer layer) {
            if (layer == ViewLayer.COCKPIT_WITH_OVERLAY) {
                ShowOverlay();
            } else {
                HideOverlay();
            }
        }

        // ─── Public API ─────────────────────────────────

        public void ShowOverlay() {
            if (_isOverlayOpen) return;

            float panelWidth = GetPanelWidth();

            if (panelWidth > 0f) {
                SlideIn(panelWidth);
            } else {
                // No animation needed
                _overlayPanel.style.left = 0;
                _isOverlayOpen = true;
                _isAnimating = false;
            }
        }

        public void HideOverlay() {
            if (!_isOverlayOpen) return;

            float panelWidth = GetPanelWidth();

            if (panelWidth > 0f) {
                SlideOut(panelWidth);
            } else {
                // Hide immediately
                _overlayPanel.style.left = -FALLBACK_PANEL_WIDTH;
                _isOverlayOpen = false;
                _isAnimating = false;
            }
        }

        private void EnsureOverlayHidden() {
            if (_overlayPanel != null) {
                _overlayPanel.style.left = -FALLBACK_PANEL_WIDTH;
            }
            _isOverlayOpen = false;
            _isAnimating = false;
        }

        private float GetPanelWidth() {
            if (_overlayPanel == null) return FALLBACK_PANEL_WIDTH;
            float w = _overlayPanel.resolvedStyle.width;
            return w > 0f ? w : FALLBACK_PANEL_WIDTH;
        }

        private void SlideIn(float panelWidth) {
            if (AccessibilitySettings.ReduceMotion) {
                _overlayPanel.style.left = 0;
                _isOverlayOpen = true;
                return;
            }

            _isAnimating = true;
            _activeCoroutine = StartCoroutine(SlideInCoroutine(panelWidth, _slideInDuration));
        }

        private void SlideOut(float panelWidth) {
            if (AccessibilitySettings.ReduceMotion) {
                _overlayPanel.style.left = -panelWidth;
                _isOverlayOpen = false;
                _isAnimating = false;
                return;
            }

            _isAnimating = true;
            _activeCoroutine = StartCoroutine(SlideOutCoroutine(panelWidth, _slideOutDuration));
        }

        private IEnumerator SlideInCoroutine(float panelWidth, float duration) {
            // Set initial position: panelWidth pixels to the right
            _overlayPanel.style.left = panelWidth;

            float elapsed = 0f;
            while (elapsed < duration) {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float x = Mathf.Lerp(panelWidth, 0f, t);
                _overlayPanel.style.left = -x; // Negative because we slide from right
                yield return null;
            }

            _overlayPanel.style.left = 0;
            _isOverlayOpen = true;
            _isAnimating = false;
            _activeCoroutine = null;
        }

        private IEnumerator SlideOutCoroutine(float panelWidth, float duration) {
            float startX = 0;
            float elapsed = 0f;
            while (elapsed < duration) {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float x = Mathf.Lerp(startX, -panelWidth, t);
                _overlayPanel.style.left = x;
                yield return null;
            }

            _overlayPanel.style.left = -panelWidth;
            _isOverlayOpen = false;
            _isAnimating = false;
            _activeCoroutine = null;
        }
    }
}