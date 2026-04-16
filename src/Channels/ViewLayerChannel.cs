using UnityEngine;
using Game.Scene;

namespace Game.Channels {
    /// <summary>
    /// Tier 1 SO Channel for ViewLayer change broadcasts.
    /// Produced by ViewLayerManager (MasterScene).
    /// Consumed by StarMapUI, ShipHUD, ShipControlSystem.
    /// </summary>
    [CreateAssetMenu(menuName = "Channels/ViewLayerChannel")]
    public class ViewLayerChannel : GameEvent<ViewLayer> { }
}
