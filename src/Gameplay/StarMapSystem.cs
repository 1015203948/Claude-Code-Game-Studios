using UnityEngine;
using Game.Channels;
using Game.Data;

namespace Game.Gameplay {
    /// <summary>
    /// StarMap system — manages node ownership updates in response to combat events.
    /// Lives in MasterScene (StarMapScene context).
    ///
    /// Story 008: Subscribes to CombatChannel and updates node Ownership:
    /// - RaiseVictory → node ownership becomes PLAYER
    /// - RaiseDefeat  → node ownership becomes ENEMY
    /// - RaiseBegin   → combat started (logged only)
    /// </summary>
    public class StarMapSystem : MonoBehaviour
    {
        public static StarMapSystem Instance { get; private set; }

        [SerializeField] private CombatChannel _combatChannel;

        // ─── Singleton ────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            if (_combatChannel != null) {
                _combatChannel.Subscribe(OnCombatPayload);
            }
        }

        private void OnDisable()
        {
            if (_combatChannel != null) {
                _combatChannel.Unsubscribe(OnCombatPayload);
            }
        }

        // ─── Combat Event Handler ────────────────────────────

        private void OnCombatPayload(CombatPayload payload)
        {
            // Begin events don't affect ownership — only Victory/Defeat do
            if (payload.Result == CombatResult.Begin) return;

            var node = GetNode(payload.NodeId);
            if (node == null) return;

            switch (payload.Result) {
                case CombatResult.Victory:
                    // AC-3: Victory → PLAYER ownership
                    node.Ownership = OwnershipState.PLAYER;
                    Debug.Log($"[StarMapSystem] Victory at {payload.NodeId} → PLAYER");
                    break;

                case CombatResult.Defeat:
                    // AC-4: Defeat → ENEMY ownership
                    node.Ownership = OwnershipState.ENEMY;
                    Debug.Log($"[StarMapSystem] Defeat at {payload.NodeId} → ENEMY");
                    break;
            }
        }

        // ─── Helpers ───────────────────────────────────────

        private StarNode GetNode(string nodeId)
        {
            return GameDataManager.Instance?.GetStarMapData()?.GetNode(nodeId);
        }
    }
}
