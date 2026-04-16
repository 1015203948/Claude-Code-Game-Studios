using UnityEngine;

namespace Game.Channels {
    [CreateAssetMenu(menuName = "Channels/SimRateChangedChannel")]
    public class SimRateChangedChannel : GameEvent<float> { }
}
