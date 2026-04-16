using System;

namespace Game.Data {

    /// <summary>
    /// Building types available for construction on colony nodes.
    /// Each type has a fixed cost and production modifier.
    /// </summary>
    public enum BuildingType
    {
        BasicMine,
        Shipyard,
        ShipyardUpgrade,
    }

    /// <summary>
    /// Represents a single building instance on a colony node.
    /// Produced by BuildingSystem.RequestBuild().
    /// </summary>
    [Serializable]
    public class BuildingInstance
    {
        public string InstanceId;
        public BuildingType BuildingType;
        public string NodeId;
        public bool IsActive;

        public BuildingInstance(string instanceId, BuildingType type, string nodeId)
        {
            InstanceId = instanceId;
            BuildingType = type;
            NodeId = nodeId;
            IsActive = true;
        }
    }

    /// <summary>
    /// Build costs for each building type.
    /// </summary>
    public static class BuildingCosts
    {
        public const int BASIC_MINE_ORE = 50;
        public const int BASIC_MINE_ENERGY = 20;
        public const int SHIPYARD_ORE = 80;
        public const int SHIPYARD_ENERGY = 40;
        public const int SHIPYARD_UPGRADE_ORE = 100;
        public const int SHIPYARD_UPGRADE_ENERGY = 50;

        public static (int ore, int energy) GetCost(BuildingType type) => type switch
        {
            BuildingType.BasicMine => (BASIC_MINE_ORE, BASIC_MINE_ENERGY),
            BuildingType.Shipyard => (SHIPYARD_ORE, SHIPYARD_ENERGY),
            BuildingType.ShipyardUpgrade => (SHIPYARD_UPGRADE_ORE, SHIPYARD_UPGRADE_ENERGY),
            _ => (0, 0),
        };
    }

    /// <summary>
    /// Production deltas per building type per second.
    /// </summary>
    public static class BuildingProduction
    {
        public const int BASIC_MINE_ORE_PER_SEC = 10;
        public const int BASIC_MINE_ENERGY_PER_SEC = -2;
        public const int SHIPYARD_ENERGY_PER_SEC = -3;

        public static (int ore, int energy) GetDelta(BuildingType type) => type switch
        {
            BuildingType.BasicMine => (BASIC_MINE_ORE_PER_SEC, BASIC_MINE_ENERGY_PER_SEC),
            BuildingType.Shipyard => (0, SHIPYARD_ENERGY_PER_SEC),
            BuildingType.ShipyardUpgrade => (0, 0), // no production change
            _ => (0, 0),
        };
    }
}
