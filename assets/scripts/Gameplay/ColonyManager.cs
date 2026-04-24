using UnityEngine;
using System.IO;
using Game.Channels;
using Game.Data;
using GameplayBuildings = Game.Gameplay;
using GameplayShips = Game.Gameplay;

namespace Gameplay {
    /// <summary>
    /// Colony production system — drives resource accumulation tick loop.
    ///
    /// Tick driven by SimClock.DeltaTime (NOT Time.deltaTime).
    /// Per ADR-0012: strategy layer systems use SimClock.DeltaTime.
    ///
    /// Lives in StarMapScene. Tick interval: DeltaTime accumulates to ≥ 1.0s
    /// triggers one production calculation.
    /// </summary>
    public class ColonyManager : MonoBehaviour {
        public static ColonyManager Instance { get; private set; }

        [Header("Config")]
        [SerializeField] private ResourceConfig _resourceConfig;

        [Header("Channel References")]
        [SerializeField] private OnResourcesUpdatedChannel _onResourcesUpdatedChannel;
        [SerializeField] private ColonyShipChannel _colonyShipChannel;

        /// <summary>Current ore stockpile. Clamped to [0, ORE_CAP].</summary>
        public int OreCurrent { get; private set; }

        /// <summary>Current energy stockpile. No upper cap.</summary>
        public int EnergyCurrent { get; private set; }

        /// <summary>Number of active mines producing ore.</summary>
        public int ActiveMines { get; set; }

        /// <summary>Number of active shipyards consuming energy.</summary>
        public int ActiveShipyards { get; set; }

        private float _accumulator;

        private void Awake() {
            if (Instance != null && Instance != this) {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>Test hook: resets Instance to null for test isolation. Do NOT use in production.</summary>
        internal static void ResetInstanceForTest() => Instance = null;

        private void Update() {
            // ✅ ADR-0012: Use SimClock.DeltaTime, NOT Time.deltaTime
            float dt = SimClock.Instance != null
                ? SimClock.Instance.DeltaTime
                : Time.unscaledDeltaTime;

            _accumulator += dt;

            if (_accumulator >= 1.0f) {
                _accumulator -= 1.0f;
                Tick();
            }
        }

        /// <summary>
        /// One production tick: calculate net ore/energy delta from all PLAYER-owned nodes.
        /// Stub production uses colony base rates until Story 018 (BuildingSystem).
        /// Called once per real second of simulation time.
        /// </summary>
        private void Tick() {
            // T-3: Get all PLAYER-owned nodes
            var playerNodes = GameplayBuildings.BuildingSystem.Instance?.GetNodesByOwner(OwnershipState.PLAYER);

            int totalOreDelta = 0;
            int totalEnergyDelta = 0;

            if (playerNodes != null) {
                foreach (var nodeId in playerNodes) {
                    var delta = GameplayBuildings.BuildingSystem.Instance.GetNodeProductionDelta(nodeId);
                    totalOreDelta += delta.OrePerSec;
                    totalEnergyDelta += delta.EnergyPerSec;
                }
            } else {
                // Stub: base production per PLAYER node until Story 018
                // (no buildings yet, so use colony base rates)
            }

            // T-4: Accumulate ore (clamped to [0, ORE_CAP])
            OreCurrent = Mathf.Clamp(OreCurrent + totalOreDelta, 0, GetOreCap());

            // T-5: Accumulate energy (floor 0, no upper cap)
            EnergyCurrent = Mathf.Max(0, EnergyCurrent + totalEnergyDelta);

            // T-6: Broadcast with deltas
            BroadcastResources(totalOreDelta, totalEnergyDelta);
        }

        private int GetOreCap() {
            return _resourceConfig != null ? _resourceConfig.ORE_CAP : 1000;
        }

        private void BroadcastResources(int oreDelta, int energyDelta) {
            if (_onResourcesUpdatedChannel != null) {
                var snapshot = new ResourceSnapshot(OreCurrent, EnergyCurrent, oreDelta, energyDelta);
                _onResourcesUpdatedChannel.Raise(snapshot);
            }
        }

        /// <summary>
        /// Initialize colony with starting resources.
        /// Call this when a new game starts.
        /// </summary>
        public void Initialize(int startingOre = 100, int startingEnergy = 50) {
            OreCurrent = startingOre;
            EnergyCurrent = startingEnergy;
            ActiveMines = 0;
            ActiveShipyards = 0;
            _accumulator = 0f;
            BroadcastResources(0, 0);
        }

        /// <summary>
        /// Called when a ship construction completes.
        /// Broadcasts ColonyShipChannel with ship instance ID and originating node ID.
        /// </summary>
        public void OnShipBuilt(string shipInstanceId, string nodeId) {
            _colonyShipChannel?.Raise((shipInstanceId, nodeId));
        }

        // ─────────────────────────────────────────────────────────────────
        // BuildShip (Story 017)
        // ─────────────────────────────────────────────────────────────────

        private const int SHIP_ORE_COST = 30;
        private const int SHIP_ENERGY_COST = 15;

        /// <summary>
        /// Attempts to build a ship at the given PLAYER-owned node with a shipyard.
        /// Atomic: deducts resources, creates ship, broadcasts OnShipBuilt.
        /// On failure (insufficient resources or no shipyard), rolls back resources.
        /// </summary>
        public BuildShipResult BuildShip(string nodeId)
        {
            // B-1: Verify node ownership
            var node = GetNodeById(nodeId);
            if (node == null || node.Ownership != OwnershipState.PLAYER) {
                Debug.Log($"[ColonyManager] BuildShip({nodeId}): NODE_NOT_PLAYER");
                return BuildShipResult.Failure("NODE_NOT_PLAYER");
            }

            // B-2: Verify shipyard exists
            if (!node.HasShipyard) {
                Debug.Log($"[ColonyManager] BuildShip({nodeId}): NO_SHIPYARD");
                return BuildShipResult.Failure("NO_SHIPYARD");
            }

            // B-3: Verify resources
            if (OreCurrent < SHIP_ORE_COST || EnergyCurrent < SHIP_ENERGY_COST) {
                Debug.Log($"[ColonyManager] BuildShip({nodeId}): INSUFFICIENT_RESOURCES (ore={OreCurrent}, energy={EnergyCurrent})");
                return BuildShipResult.Failure("INSUFFICIENT_RESOURCES");
            }

            // Snapshot for rollback
            int snapshotOre = OreCurrent;
            int snapshotEnergy = EnergyCurrent;

            // Atomic deduction
            OreCurrent -= SHIP_ORE_COST;
            EnergyCurrent -= SHIP_ENERGY_COST;
            BroadcastResources(-SHIP_ORE_COST, -SHIP_ENERGY_COST);

            // Create ship
            var shipResult = GameplayShips.ShipSystem.Instance?.CreateShip(nodeId);
            if (shipResult == null || !shipResult.Value.Success) {
                // Rollback
                OreCurrent = snapshotOre;
                EnergyCurrent = snapshotEnergy;
                BroadcastResources(0, 0); // broadcast current state after rollback
                string reason = shipResult?.FailReason ?? "SHIP_CREATION_FAILED";
                Debug.Log($"[ColonyManager] BuildShip({nodeId}): rollback — {reason}");
                return BuildShipResult.Failure(reason);
            }

            // Success
            Debug.Log($"[ColonyManager] BuildShip({nodeId}): SUCCESS — ship {shipResult.Value.InstanceId}");
            OnShipBuilt(shipResult.Value.InstanceId, nodeId);
            return new BuildShipResult(true, shipResult.Value.InstanceId);
        }

        /// <summary>
        /// Gets a StarNode by its ID from StarMapData.
        /// </summary>
        private StarNode GetNodeById(string nodeId)
        {
            var map = GameDataManager.Instance?.GetStarMapData();
            if (map == null) return null;
            foreach (var node in map.Nodes) {
                if (node.Id == nodeId) return node;
            }
            return null;
        }

        // ─────────────────────────────────────────────────────────────────
        // Resource interface for BuildingSystem (Story 018)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns true if the colony has at least the specified resources.
        /// </summary>
        public bool CanAffordResources(int ore, int energy)
        {
            return OreCurrent >= ore && EnergyCurrent >= energy;
        }

        /// <summary>
        /// Deducts resources from the colony. No validation.
        /// </summary>
        public void DeductResources(int ore, int energy)
        {
            OreCurrent = Mathf.Clamp(OreCurrent - ore, 0, GetOreCap());
            EnergyCurrent = Mathf.Max(0, EnergyCurrent - energy);
            BroadcastResources(-ore, -energy);
        }

        /// <summary>
        /// Adds resources to the colony (e.g., from combat rewards).
        /// Clamps ore to [0, ORE_CAP]. No upper cap on energy.
        /// Broadcasts resource update via channel.
        /// </summary>
        public void AddResources(int ore, int energy)
        {
            OreCurrent = Mathf.Clamp(OreCurrent + ore, 0, GetOreCap());
            EnergyCurrent = Mathf.Max(0, EnergyCurrent + energy);
            BroadcastResources(ore, energy);
        }

        // ─────────────────────────────────────────────────────────────────
        // Persistence (Phase 3)
        // ─────────────────────────────────────────────────────────────────

        private const string SAVE_FILE = "resources.json";

        /// <summary>
        /// Saves colony resources and node ownership to a JSON file.
        /// </summary>
        public void Save()
        {
            try {
                var data = new ColonySaveData(OreCurrent, EnergyCurrent);
                var map = GameDataManager.Instance?.GetStarMapData();
                if (map != null) {
                    var entries = new System.Collections.Generic.List<NodeOwnershipEntry>();
                    foreach (var node in map.Nodes) {
                        entries.Add(new NodeOwnershipEntry(node.Id, node.Ownership.ToString()));
                    }
                    data.NodeOwnershipEntries = entries.ToArray();
                }

                string json = JsonUtility.ToJson(data, true);
                string path = Path.Combine(Application.persistentDataPath, SAVE_FILE);
                File.WriteAllText(path, json);
                Debug.Log($"[ColonyManager] Saved: ore={OreCurrent}, energy={EnergyCurrent} to {path}");
            } catch (System.Exception e) {
                Debug.LogError($"[ColonyManager] Save failed: {e.Message}");
            }
        }

        /// <summary>
        /// Loads colony resources and node ownership from JSON file.
        /// Returns true if a save file was found and loaded.
        /// </summary>
        public bool Load()
        {
            try {
                string path = Path.Combine(Application.persistentDataPath, SAVE_FILE);
                if (!File.Exists(path)) {
                    Debug.Log("[ColonyManager] No save file found — using defaults.");
                    return false;
                }

                string json = File.ReadAllText(path);
                var data = JsonUtility.FromJson<ColonySaveData>(json);
                if (data == null) return false;

                OreCurrent = data.OreCurrent;
                EnergyCurrent = data.EnergyCurrent;

                // Restore node ownership
                var map = GameDataManager.Instance?.GetStarMapData();
                if (map != null && data.NodeOwnershipEntries != null) {
                    foreach (var entry in data.NodeOwnershipEntries) {
                        if (string.IsNullOrEmpty(entry.NodeId)) continue;
                        foreach (var node in map.Nodes) {
                            if (node.Id == entry.NodeId) {
                                if (System.Enum.TryParse<OwnershipState>(entry.Ownership, out var state)) {
                                    node.Ownership = state;
                                }
                                break;
                            }
                        }
                    }
                }

                BroadcastResources(0, 0);
                Debug.Log($"[ColonyManager] Loaded: ore={OreCurrent}, energy={EnergyCurrent} from {path}");
                return true;
            } catch (System.Exception e) {
                Debug.LogError($"[ColonyManager] Load failed: {e.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Result of a ship construction request.
    /// </summary>
    public readonly struct BuildShipResult
    {
        public bool Success { get; }
        public string InstanceId { get; }
        public string FailReason { get; }

        public BuildShipResult(bool success, string instanceId = null, string failReason = null)
        {
            Success = success;
            InstanceId = instanceId;
            FailReason = failReason;
        }

        public static BuildShipResult Failure(string reason) => new BuildShipResult(false, null, reason);
    }

    /// <summary>
    /// JSON-serializable save data for colony resources and node ownership.
    /// </summary>
    [System.Serializable]
    internal class ColonySaveData
    {
        public int OreCurrent;
        public int EnergyCurrent;
        public NodeOwnershipEntry[] NodeOwnershipEntries;

        // Runtime helper — not serialized
        [System.NonSerialized]
        public System.Collections.Generic.Dictionary<string, string> NodeOwnership =
            new System.Collections.Generic.Dictionary<string, string>();

        public ColonySaveData() { }

        public ColonySaveData(int ore, int energy)
        {
            OreCurrent = ore;
            EnergyCurrent = energy;
            NodeOwnershipEntries = new NodeOwnershipEntry[0];
        }
    }

    /// <summary>
    /// Serializable node ownership entry for JSON.
    /// </summary>
    [System.Serializable]
    internal class NodeOwnershipEntry
    {
        public string NodeId;
        public string Ownership;

        public NodeOwnershipEntry() { }

        public NodeOwnershipEntry(string nodeId, string ownership)
        {
            NodeId = nodeId;
            Ownership = ownership;
        }
    }
}
