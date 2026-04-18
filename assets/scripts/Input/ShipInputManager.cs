// MIT License - Copyright (c) 2026 Game Studios
/// <summary>
/// MasterScene input router — subscribes to ViewLayerChannel and manages
/// input state. Touch input is handled exclusively by DualJoystickInput.
/// This component manages view layer state transitions.
/// </summary>
namespace Game.Inputs {
    using UnityEngine;
    using Game.Scene;
    using Game.Channels;

    public class ShipInputManager : MonoBehaviour {
        // ─── Serialized Fields ───────────────────────────────────────────────
        [SerializeField] private ViewLayerChannel _viewLayerChannel;
        [SerializeField] private ShipInputChannel _shipInputChannel;

        // ─── Public State ───────────────────────────────────────────────────
        public bool IsInCockpit { get; private set; }

        // ─── Lifecycle ───────────────────────────────────────────────────────

        private void OnEnable() {
            if (_viewLayerChannel != null) {
                _viewLayerChannel.Subscribe(OnViewLayerChanged);
            }
        }

        private void OnDisable() {
            if (_viewLayerChannel != null) {
                _viewLayerChannel.Unsubscribe(OnViewLayerChanged);
            }
        }

        // ─── Event Handler ────────────────────────────────────────────────────

        private void OnViewLayerChanged(ViewLayer layer) {
            IsInCockpit = (layer == ViewLayer.COCKPIT);
        }
    }
}