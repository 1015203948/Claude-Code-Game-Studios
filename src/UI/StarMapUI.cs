using UnityEngine;
using Game.Channels;
using Game.Scene;
using Game.Data;

namespace Game.UI {
    /// <summary>
    /// StarMap UI layer — subscribes to ViewLayerChannel and ShipStateChannel.
    /// OnEnable/OnDisable subscription pairing per ADR-0002 ADV-01/ADV-02.
    /// </summary>
    public class StarMapUI : MonoBehaviour {
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
            // Handle ViewLayer change — hide/show specific UI elements
            // This is a stub: full UI response implemented in Presentation layer
        }

        private void OnShipStateChanged((string instanceId, ShipState newState) payload) {
            // Handle ship state change — update ship indicators on star map
            // This is a stub: full UI response implemented in Presentation layer
        }
    }
}
