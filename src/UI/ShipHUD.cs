using UnityEngine;
using Game.Channels;
using Game.Scene;
using Game.Data;

namespace Game.UI {
    /// <summary>
    /// Ship HUD — subscribes to ViewLayerChannel and ShipStateChannel.
    /// OnEnable/OnDisable subscription pairing per ADR-0002 ADV-01/ADV-02.
    /// </summary>
    public class ShipHUD : MonoBehaviour {
        [Header("Channel References")]
        [SerializeField] private ViewLayerChannel _viewLayerChannel;
        [SerializeField] private ShipStateChannel _shipStateChannel;

        private void OnEnable() {
            _viewLayerChannel.Subscribe(OnViewLayerChanged);
            _shipStateChannel.Subscribe(OnShipStateChanged);
        }

        private void OnDisable() {
            _viewLayerChannel.Unsubscribe(OnViewLayerChanged);
            _shipStateChannel.Unsubscribe(OnShipStateChanged);
        }

        private void OnViewLayerChanged(ViewLayer newLayer) {
            // Handle ViewLayer change — show/hide HUD elements
            // STARMAP: hide cockpit HUD
            // COCKPIT: show cockpit HUD
            // COCKPIT_WITH_OVERLAY: show HUD + overlay
        }

        private void OnShipStateChanged((string instanceId, ShipState newState) payload) {
            // Handle ship state change — update HUD display
            // e.g., show DESTROYED overlay, update shield bar
        }
    }
}
