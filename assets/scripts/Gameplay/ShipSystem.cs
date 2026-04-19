using UnityEngine;
using System;
using Game.Channels;
using Game.Data;

namespace Game.Gameplay {

    /// <summary>
    /// Ship lifecycle manager — creates and registers ship instances.
    /// CreateShip is called by ColonyManager.BuildShip() after resource deduction.
    /// Story 017 stub: creates ship with default stats.
    /// </summary>
    public class ShipSystem : MonoBehaviour
    {
        public static ShipSystem Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>Test hook: resets Instance to null for test isolation. Do NOT use in production.</summary>
        internal static void ResetInstanceForTest() => Instance = null;

        /// <summary>
        /// Creates a new ship instance docked at the given node.
        /// Story 017 stub: always succeeds with a generated instance ID.
        /// </summary>
        public CreateShipResult CreateShip(string nodeId)
        {
            string instanceId = $"ship_{Guid.NewGuid():N}";

            // Create ship data model using HullBlueprint
            var bp = ScriptableObject.CreateInstance<HullBlueprint>();
            bp.name = "colony_ship_v1";
            bp.BaseHull = 100f;
            bp.ThrustPower = 50f;
            bp.TurnSpeed = 90f;

            var channel = ScriptableObject.CreateInstance<ShipStateChannel>();
            var ship = new ShipDataModel(
                instanceId,
                bp.name,
                isPlayerControlled: true,
                bp,
                channel);

            ship.DockedNodeId = nodeId;
            ship.SetState(ShipState.DOCKED);

            GameDataManager.Instance?.RegisterShip(ship);

            Debug.Log($"[ShipSystem] Created ship {instanceId} at node {nodeId}");

            return new CreateShipResult(true, instanceId);
        }
    }

    /// <summary>
    /// Result of a CreateShip call.
    /// </summary>
    public readonly struct CreateShipResult
    {
        public bool Success { get; }
        public string InstanceId { get; }
        public string FailReason { get; }

        public CreateShipResult(bool success, string instanceId = null, string failReason = null)
        {
            Success = success;
            InstanceId = instanceId;
            FailReason = failReason;
        }

        public static CreateShipResult Failure(string reason) => new CreateShipResult(false, failReason: reason);
    }

    /// <summary>
    /// Result of a BuildShip call on ColonyManager.
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

        public static BuildShipResult Failure(string reason) => new BuildShipResult(false, failReason: reason);
    }
}
