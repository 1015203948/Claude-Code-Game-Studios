// MIT License - Copyright (c) 2026 Game Studios
namespace Game.Scene {
    using System;
    using System.Collections;
    using UnityEngine;
    using UnityEngine.UIElements;
    using UnityEngine.Accessibility;
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
        private bool _isAnimating;
        private Coroutine _activeCoroutine;

        // ─── Unity Lifecycle ─────────────────────────────────

        private void OnEnable() {
            _viewLayerChannel.Subscribe(OnViewLayerChanged);
            EnsureOverlayHidden();
        }

        private void OnDisable() {
            _viewLayerChannel.Unsubscribe(OnViewLayerChanged);
        }

        // ─── ViewLayer Event Handler ─────────────────────

        private void OnViewLayerChanged(ViewLayer layer) {
            switch (layer) {
                case ViewLayer.COCKPIT_WITH_OVERLAY:
                    if (!_isOverlayOpen && !_isAnimating) {
                        OpenOverlay();
                    }
                    break;
                case ViewLayer.COCKPIT:
                    if (_isOverlayOpen && !_isAnimating) {
                        CloseOverlay();
                    }
                    break;
                default:
                    break;
            }
        }

        // ─── Open Overlay ──────────────────────────────

        private void OpenOverlay() {
            _isAnimating = true;

            // Step 1: Switch to ScreenSpaceOverlay (instant)
            if (_screenOverlaySettings != null) {
                _starMapDocument.panelSettings = _screenOverlaySettings;
            }

            // Step 2: Make panel visible
            _overlayPanel.style.display = DisplayStyle.Flex;

            // Step 3: Determine panel width for animation start position
            float panelWidth = GetPanelWidth();

            // Step 4: Start slide-in from right
            if (panelWidth > 0f) {
                SlideIn(panelWidth);
            } else {
                _overlayPanel.style.translate = new StyleTranslate(
                    new TransformOffset(new Length(0), new Length(0)));
                _isOverlayOpen = true;
                _isAnimating = false;
            }
        }

        private float GetPanelWidth() {
            float w = _overlayPanel.resolvedStyle.width;
            return w > 0f ? w : FALLBACK_PANEL_WIDTH;
        }

        private void SlideIn(float panelWidth) {
            if (AccessibilitySettings.ReduceMotion) {
                _overlayPanel.style.translate = new StyleTranslate(
                    new TransformOffset(new Length(0), new Length(0)));
                _isOverlayOpen = true;
                _isAnimating = false;
                return;
            }

            _activeCoroutine = StartCoroutine(SlideInCoroutine(panelWidth, _slideInDuration));
        }

        private IEnumerator SlideInCoroutine(float panelWidth, float duration) {
            // Set initial position: panelWidth pixels to the right
            _overlayPanel.style.translate = new StyleTranslate(
                new TransformOffset(new Length(panelWidth, LengthUnit.Pixel), new Length(0)));

            float elapsed = 0f;
            while (elapsed < duration) {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float x = Mathf.Lerp(panelWidth, 0f, t);
                _overlayPanel.style.translate = new StyleTranslate(
                    new TransformOffset(new Length(x, LengthUnit.Pixel), new Length(0)));
                yield return null;
            }

            _overlayPanel.style.translate = new StyleTranslate(
                new TransformOffset(new Length(0), new Length(0)));
            _isOverlayOpen = true;
            _isAnimating = false;
            _activeCoroutine = null;
        }

        // ─── Close Overlay ─────────────────────────────

        private void CloseOverlay() {
            _isAnimating = true;
            float panelWidth = GetPanelWidth();
            SlideOut(panelWidth);
        }

        private void SlideOut(float panelWidth) {
            if (AccessibilitySettings.ReduceMotion) {
                _overlayPanel.style.display = DisplayStyle.None;
                RestoreCameraSpace();
                _isOverlayOpen = false;
                _isAnimating = false;
                return;
            }

            _activeCoroutine = StartCoroutine(SlideOutCoroutine(panelWidth, _slideOutDuration));
        }

        private IEnumerator SlideOutCoroutine(float panelWidth, float duration) {
            float elapsed = 0f;
            while (elapsed < duration) {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float x = Mathf.Lerp(0f, panelWidth, t);
                _overlayPanel.style.translate = new StyleTranslate(
                    new TransformOffset(new Length(x, LengthUnit.Pixel), new Length(0)));
                yield return null;
            }

            // Step 2: Hide panel
            _overlayPanel.style.display = DisplayStyle.None;

            // Step 3: Restore CameraSpace
            RestoreCameraSpace();

            _isOverlayOpen = false;
            _isAnimating = false;
            _activeCoroutine = null;
        }

        private void RestoreCameraSpace() {
            if (_cameraSpaceSettings != null) {
                _starMapDocument.panelSettings = _cameraSpaceSettings;
            }
        }

        private void EnsureOverlayHidden() {
            if (_overlayPanel != null) {
                _overlayPanel.style.display = DisplayStyle.None;
                _overlayPanel.style.translate = new StyleTranslate(
                    new TransformOffset(new Length(0), new Length(0)));
            }
            _isOverlayOpen = false;
            _isAnimating = false;
            _activeCoroutine = null;
        }

        // ─── Public API ───────────────────────────────

        /// <summary>Whether the overlay is currently visible.</summary>
        public bool IsOverlayOpen => _isOverlayOpen;

        /// <summary>Whether an open/close animation is currently running.</summary>
        public bool IsAnimating => _isAnimating;

        /// <summary>
        /// Called by StarMap UI when player selects a ship in overlay mode.
        /// Triggers SWITCHING_SHIP via ViewLayerManager.RequestSwitchShip.
        /// Full wiring in Story 011 / Unity Inspector.
        /// </summary>
        public void OnShipSelectedInOverlay(string shipId) {
            if (!_isOverlayOpen) return;
            // Actual ViewLayerManager.RequestSwitchShip(shipId) call
            // wired via SerializeField in Unity Inspector.
        }
    }
}