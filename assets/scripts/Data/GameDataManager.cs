using System;
using System.Collections.Generic;
using UnityEngine;
using Gameplay;
using Game.Gameplay.Fleet;

namespace Game.Data {
    /// <summary>
    /// Authoritative owner of all Layer 2 runtime state.
    /// Lives in MasterScene (ADR-0001 / ADR-0004).
    ///
    /// Responsibilities:
    ///   - Owns all ShipDataModel instances (registry)
    ///   - IN_COCKPIT mutual exclusion: tracks which ship is currently in cockpit
    ///   - Owns StarMapData reference
    ///   - Save/Load orchestration (Story 009)
    /// </summary>
    public class GameDataManager {
        public static GameDataManager Instance { get; private set; }

        private StarMapData _starMapData;
        private readonly Dictionary<string, ShipDataModel> _shipRegistry = new Dictionary<string, ShipDataModel>();

        /// <summary>
        /// The ship ID currently in IN_COCKPIT state. Null if no ship is in cockpit.
        /// Used for IN_COCKPIT mutual exclusion invariant.
        /// </summary>
        private string _activeCockpitShipId;

        // =====================================================================
        // Singleton
        // =====================================================================

        public GameDataManager() {
            if (Instance != null && Instance != this) {
                Debug.LogWarning("[GameDataManager] Duplicate GameDataManager detected.");
            }
            Instance = this;
        }

        /// <summary>Test hook: resets Instance to null for test isolation. Do NOT use in production.</summary>
        internal static void ResetInstanceForTest() => Instance = null;

        // =====================================================================
        // Ship Registry
        // =====================================================================

        /// <summary>
        /// Register a ship instance. Called when a ship is instantiated.
        /// </summary>
        public void RegisterShip(ShipDataModel ship) {
            if (ship == null || string.IsNullOrEmpty(ship.InstanceId)) {
                Debug.LogError("[GameDataManager] Cannot register null or empty-instanceId ship.");
                return;
            }
            _shipRegistry[ship.InstanceId] = ship;
        }

        /// <summary>
        /// Get a ship by instance ID.
        /// </summary>
        public ShipDataModel GetShip(string instanceId) {
            return _shipRegistry.TryGetValue(instanceId, out var ship) ? ship : null;
        }

        /// <summary>
        /// Get all registered ships.
        /// </summary>
        public IEnumerable<ShipDataModel> AllShips =>
            _shipRegistry.Values;

        // =====================================================================
        // IN_COCKPIT Mutual Exclusion (AC-SHIP-07)
        // =====================================================================

        /// <summary>
        /// Returns true if some ship (other than excludeShipId) is currently in IN_COCKPIT.
        /// Used by ShipDataModel.CanEnterCockpit() to enforce mutual exclusion.
        /// </summary>
        public static bool HasActiveCockpitShip(string excludeShipId = null) {
            if (Instance == null || Instance._activeCockpitShipId == null) return false;
            if (excludeShipId != null && Instance._activeCockpitShipId == excludeShipId) return false;
            return true;
        }

        /// <summary>
        /// Returns the instance ID of the ship currently in IN_COCKPIT, or null.
        /// </summary>
        public string GetActiveCockpitShipId() {
            return _activeCockpitShipId;
        }

        /// <summary>
        /// Called by ShipDataModel when entering IN_COCKPIT.
        /// </summary>
        internal void SetActiveCockpitShip(string shipInstanceId) {
            _activeCockpitShipId = shipInstanceId;
        }

        /// <summary>
        /// Called by ShipDataModel when leaving IN_COCKPIT.
        /// </summary>
        internal void ClearActiveCockpitShip(string shipInstanceId) {
            if (_activeCockpitShipId == shipInstanceId) {
                _activeCockpitShipId = null;
            }
        }

        // =====================================================================
        // StarMapData
        // =====================================================================

        public void SetStarMapData(StarMapData data) {
            _starMapData = data;
        }

        public StarMapData GetStarMapData() {
            return _starMapData;
        }

        // =====================================================================
        // Save / Load (Story 009)
        // =====================================================================

        /// <summary>
        /// Collect all runtime state into a SaveData struct.
        /// </summary>
        public SaveData Save() {
            var saveData = new SaveData {
                ActiveShipId = _activeCockpitShipId,
                SimRate = SimClock.Instance != null ? SimClock.Instance.SimRate : 1f,
                Ships = CollectShipData(),
                Nodes = CollectNodeData(),
                Dispatches = CollectDispatchData(),
                Timestamp = DateTime.UtcNow.ToString("o"),
            };
            return saveData;
        }

        /// <summary>
        /// Restore runtime state from a SaveData struct.
        /// </summary>
        public void Load(SaveData saveData) {
            // Restore SimRate (with default protection for invalid values)
            if (SimClock.Instance != null) {
                float rate = saveData.SimRate;
                if (!SimClock.IsValidRate(rate)) {
                    Debug.LogWarning($"[GameDataManager] Load: SimRate={rate} invalid, defaulting to 1.");
                    rate = 1f;
                }
                SimClock.Instance.SetRate(rate);
            }

            // Restore ship states
            RestoreShipData(saveData.Ships);

            // Restore node states
            RestoreNodeData(saveData.Nodes);

            // Restore dispatch orders
            RestoreDispatchData(saveData.Dispatches);
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private List<ShipSaveData> CollectShipData() {
            var list = new List<ShipSaveData>();
            foreach (var kvp in _shipRegistry) {
                var ship = kvp.Value;
                list.Add(new ShipSaveData {
                    InstanceId = ship.InstanceId,
                    BlueprintId = ship.BlueprintId,
                    CurrentHull = ship.CurrentHull,
                    State = ship.State,
                    Position = Vector3Save.FromVector3(Vector3.zero),
                    Rotation = QuaternionSave.FromQuaternion(Quaternion.identity),
                    DockedNodeId = ship.DockedNodeId,
                    CarrierInstanceId = ship.CarrierInstanceId,
                });
            }
            return list;
        }

        private List<NodeSaveData> CollectNodeData() {
            var list = new List<NodeSaveData>();
            if (_starMapData == null) return list;
            foreach (var node in _starMapData.Nodes) {
                list.Add(new NodeSaveData {
                    NodeId = node.Id,
                    Ownership = node.Ownership,
                    FogState = node.FogState,
                    DockedFleetId = node.DockedFleetId,
                });
            }
            return list;
        }

        private List<DispatchSaveData> CollectDispatchData() {
            var list = new List<DispatchSaveData>();
            var dispatch = FleetDispatchSystem.Instance;
            if (dispatch == null) return list;
            foreach (var order in dispatch.GetAllOrders()) {
                list.Add(new DispatchSaveData {
                    FleetId = order.OrderId,
                    FromNodeId = order.OriginNodeId,
                    ToNodeId = order.DestinationNodeId,
                    ElapsedSeconds = order.HopProgress,
                    TotalSeconds = FLEET_TRAVEL_TIME,
                });
            }
            return list;
        }

        private const float FLEET_TRAVEL_TIME = 3.0f;

        private void RestoreShipData(List<ShipSaveData> ships) {
            if (ships == null) return;
            foreach (var data in ships) {
                var ship = GetShip(data.InstanceId);
                if (ship == null) continue;
                ship.SetHull(data.CurrentHull);
                ship.DockedNodeId = data.DockedNodeId;
                if (data.State != ship.State) {
                    ship.SetState(data.State);
                }
            }
        }

        private void RestoreNodeData(List<NodeSaveData> nodes) {
            if (_starMapData == null || nodes == null) return;
            foreach (var data in nodes) {
                var node = _starMapData.GetNode(data.NodeId);
                if (node == null) continue;
                node.Ownership = data.Ownership;
                node.FogState = data.FogState;
                node.DockedFleetId = data.DockedFleetId;
            }
        }

        private void RestoreDispatchData(List<DispatchSaveData> dispatches) {
            // Stub: FleetDispatch
        }
    }
}
