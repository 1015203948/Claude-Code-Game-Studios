using UnityEngine;
using Game.Data;

namespace Game.Channels {
    [CreateAssetMenu(menuName = "Channels/OnResourcesUpdatedChannel")]
    public class OnResourcesUpdatedChannel : GameEvent<ResourceSnapshot> { }
}
