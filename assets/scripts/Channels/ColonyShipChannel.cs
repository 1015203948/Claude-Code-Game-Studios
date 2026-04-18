using UnityEngine;

namespace Game.Channels {
    [CreateAssetMenu(menuName = "Channels/ColonyShipChannel")]
    public class ColonyShipChannel : GameEvent<(string shipInstanceId, string nodeId)> { }
}
