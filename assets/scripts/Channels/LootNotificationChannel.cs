using UnityEngine;

namespace Game.Channels {
    /// <summary>
    /// Channel for loot/reward UI notifications.
    /// </summary>
    [CreateAssetMenu(menuName = "Channels/LootNotificationChannel")]
    public class LootNotificationChannel : GameEvent<string> {
        public static LootNotificationChannel Instance { get; private set; }

        private void OnEnable() {
            if (Instance != null && Instance != this) {
                Debug.LogWarning("[LootNotificationChannel] Duplicate instance detected.");
            }
            Instance = this;
        }

        internal static void ResetInstanceForTest() => Instance = null;
    }
}
