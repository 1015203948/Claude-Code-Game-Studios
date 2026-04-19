using UnityEngine;

namespace Game.Channels {
    /// <summary>
    /// Combat result — emitted by CombatChannel on combat lifecycle.
    /// Begin = combat started (no ownership change).
    /// Victory / Defeat = combat ended.
    /// </summary>
    public enum CombatResult { Begin, Victory, Defeat }

    /// <summary>
    /// Payload for CombatChannel events.
    /// </summary>
    public readonly struct CombatPayload {
        public string NodeId { get; }
        public CombatResult Result { get; }

        public CombatPayload(string nodeId, CombatResult result) {
            NodeId = nodeId;
            Result = result;
        }
    }

    /// <summary>
    /// Combat system broadcast channel.
    /// Broadcasts node-scoped combat lifecycle events: begin, victory, defeat.
    /// Implements TR-combat-001 / TR-combat-006.
    /// </summary>
    [CreateAssetMenu(menuName = "Channels/CombatChannel")]
    public class CombatChannel : GameEvent<CombatPayload>
    {
        public static CombatChannel Instance { get; private set; }

        /// <summary>Broadcasts combat start at a node. No result — just node entry.</summary>
        public void RaiseBegin(string nodeId) => Raise(new CombatPayload(nodeId, CombatResult.Begin));

        /// <summary>Broadcasts combat victory at a node.</summary>
        public void RaiseVictory(string nodeId) => Raise(new CombatPayload(nodeId, CombatResult.Victory));

        /// <summary>Broadcasts combat defeat at a node.</summary>
        public void RaiseDefeat(string nodeId) => Raise(new CombatPayload(nodeId, CombatResult.Defeat));

        private void OnEnable() {
            if (Instance != null && Instance != this) {
                Debug.LogWarning("[CombatChannel] Duplicate instance detected.");
            }
            Instance = this;
        }

        /// <summary>Test hook: resets Instance to null for test isolation. Do NOT use in production.</summary>
        internal static void ResetInstanceForTest() => Instance = null;
    }
}
