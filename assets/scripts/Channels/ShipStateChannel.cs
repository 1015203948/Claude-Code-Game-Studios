using UnityEngine;
using Game.Data;

namespace Game.Channels {
    /// <summary>
    /// Tier 1 SO Channel for ShipState change broadcasts.
    /// Produced by ShipDataModel (MasterScene).
    /// Consumed by StarMapUI, ShipHUD.
    /// </summary>
    [CreateAssetMenu(menuName = "Channels/ShipStateChannel")]
    public class ShipStateChannel : GameEvent<(string instanceId, ShipState newState)>
    {
        public static ShipStateChannel Instance { get; private set; }

        private void OnEnable() {
            if (Instance != null && Instance != this) {
                Debug.LogWarning("[ShipStateChannel] Duplicate instance detected.");
            }
            Instance = this;
        }
    }
}
