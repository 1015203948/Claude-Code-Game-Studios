// MIT License - Copyright (c) 2026 Game Studios
/// <summary>
/// MasterScene input router — subscribes to ViewLayerChannel and mutually
/// exclusively switches between StarMapActions and CockpitActions ActionMaps.
/// EnhancedTouchSupport lifecycle is owned exclusively by this class (ADR-0003 R-1).
/// </summary>
namespace Game.Inputs {
    using System;
    using UnityEngine;
    using UnityEngine.InputSystem;
    using UnityEngine.InputSystem.EnhancedTouch;
    using Game.Scene;
    using Game.Channels;

    public class ShipInputManager : MonoBehaviour {
        // ─── Serialized Fields ────────────────────────────────────────────────
        [SerializeField] private ViewLayerChannel _viewLayerChannel;
        [SerializeField] private ShipInputChannel _shipInputChannel;

        // ─── Private State ───────────────────────────────────────────────────
        private PlayerInputActions _controls;

        // ─── Lifecycle ───────────────────────────────────────────────────────

        private void Awake() {
            _controls = new PlayerInputActions();
        }

        private void OnEnable() {
            Debug.Assert(_viewLayerChannel != null,
                "[ShipInputManager] ViewLayerChannel not wired!");

            // MANDATORY: OnEnable/OnDisable pairing for subscription (ADR-0002)
            _viewLayerChannel.Subscribe(OnViewLayerChanged);

            // MANDATORY: EnhancedTouchSupport unique ownership (ADR-0003 R-1)
            // No other MonoBehaviour may call EnhancedTouchSupport.Enable/Disable.
            EnhancedTouchSupport.Enable();

            // Default initial state: StarMap active, Cockpit dormant
            _controls.StarMapActions.Enable();
            _controls.CockpitActions.Disable();
        }

        private void OnDisable() {
            // MANDATORY: OnEnable/OnDisable pairing — unsubscribe matches subscribe
            _viewLayerChannel.Unsubscribe(OnViewLayerChanged);

            // MANDATORY: EnhancedTouchSupport unique ownership — Disable matches Enable
            EnhancedTouchSupport.Disable();

            // Disable all maps to leave no action maps active on teardown
            _controls.Disable();
        }

        // ─── Event Handler ────────────────────────────────────────────────────

        private void OnViewLayerChanged(ViewLayer layer) {
            SetActiveMap(layer);
        }

        // ─── ActionMap Switching ─────────────────────────────────────────────

        /// <summary>
        /// Switches ActionMaps based on the given ViewLayer.
        /// STARMAP  → StarMapActions enabled, CockpitActions disabled
        /// COCKPIT  → StarMapActions disabled, CockpitActions enabled
        /// All others (SWITCHING_*, OVERLAY, COCKPIT_WITH_OVERLAY) → both disabled
        /// </summary>
        private void SetActiveMap(ViewLayer layer) {
            switch (layer) {
                case ViewLayer.STARMAP:
                    _controls.CockpitActions.Disable();
                    _controls.StarMapActions.Enable();
                    break;

                case ViewLayer.COCKPIT:
                    _controls.StarMapActions.Disable();
                    _controls.CockpitActions.Enable();
                    break;

                default:
                    // Covers: SWITCHING_IN, SWITCHING_OUT, SWITCHING_SHIP,
                    //         OPENING_OVERLAY, CLOSING_OVERLAY, COCKPIT_WITH_OVERLAY
                    _controls.StarMapActions.Disable();
                    _controls.CockpitActions.Disable();
                    break;
            }
        }

        // ─── Public Accessors (for consumers like DualJoystickInput) ──────────

        /// <summary>Returns the StarMapActions instance for external consumers.</summary>
        public InputActionAsset Asset => _controls;

        /// <summary>Returns true if StarMapActions is currently enabled.</summary>
        public bool IsStarMapActive => _controls.StarMapActions.enabled;

        /// <summary>Returns true if CockpitActions is currently enabled.</summary>
        public bool IsCockpitActive => _controls.CockpitActions.enabled;
    }
}
