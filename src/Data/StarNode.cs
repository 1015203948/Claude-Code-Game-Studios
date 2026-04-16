using UnityEngine;
using System.Collections.Generic;

namespace Game.Data {
    [System.Serializable]
    public class StarNode {
        public string Id { get; }
        public string DisplayName { get; }
        public Vector2 Position { get; }
        public NodeType NodeType { get; }
        public OwnershipState Ownership { get; set; }
        public FogState FogState { get; set; }
        public string DockedFleetId { get; set; } // nullable

        /// <summary>True if this node has a shipyard (enables ship construction).</summary>
        public bool HasShipyard { get; set; }

        /// <summary>Shipyard tier level. 0 = no shipyard. 1+ = shipyard active.</summary>
        public int ShipyardTier { get; set; }

        /// <summary>All buildings on this node.</summary>
        public List<BuildingInstance> Buildings { get; } = new List<BuildingInstance>();

        public StarNode(string id, string displayName, Vector2 position, NodeType nodeType) {
            Id = id;
            DisplayName = displayName;
            Position = position;
            NodeType = nodeType;
            Ownership = OwnershipState.NEUTRAL;
            FogState = FogState.UNEXPLORED;
            DockedFleetId = null;
            HasShipyard = false;
            ShipyardTier = 0;
        }

        /// <summary>Adds a building instance to this node.</summary>
        public void AddBuilding(BuildingInstance building)
        {
            Buildings.Add(building);
        }
    }
}