using UnityEngine;
using Game.Data;

namespace Game.Gameplay {

    /// <summary>
    /// Building system — manages buildings on colony nodes and their production output.
    /// Story 018: full implementation of RequestBuild, GetNodeProductionDelta, RefreshProductionCache.
    /// </summary>
    public class BuildingSystem : MonoBehaviour
    {
        public static BuildingSystem Instance { get; private set; }

        /// <summary>Fired when a building is successfully constructed.</summary>
        public event System.Action<string, BuildingType> OnBuildingConstructed;

        private void Awake()
        {
            if (Instance != null && Instance != this) {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>
        /// Requests to build a structure at a PLAYER-owned node.
        /// Atomic: deducts resources → creates building instance → refreshes cache.
        /// </summary>
        public BuildResult RequestBuild(string nodeId, BuildingType type)
        {
            // C-1: Verify node ownership
            var node = GetNodeById(nodeId);
            if (node == null || node.Ownership != OwnershipState.PLAYER) {
                Debug.Log($"[BuildingSystem] RequestBuild({nodeId}, {type}): NODE_NOT_PLAYER");
                return BuildResult.Failure("NODE_NOT_PLAYER");
            }

            // Get cost
            var (oreCost, energyCost) = BuildingCosts.GetCost(type);

            // C-2: Verify resources via ColonyManager
            if (!ColonyManager.Instance.CanAffordResources(oreCost, energyCost)) {
                Debug.Log($"[BuildingSystem] RequestBuild({nodeId}, {type}): INSUFFICIENT_RESOURCES");
                return BuildResult.Failure("INSUFFICIENT_RESOURCES");
            }

            // Atomic deduction
            ColonyManager.Instance.DeductResources(oreCost, energyCost);

            // Create building instance
            var instance = new BuildingInstance(
                $"bld_{Guid.NewGuid():N}",
                type,
                nodeId);

            // Add to node
            node.AddBuilding(instance);

            // C-3: Update ShipyardTier
            if (type == BuildingType.Shipyard) {
                node.ShipyardTier = 1;
                node.HasShipyard = true;
            } else if (type == BuildingType.ShipyardUpgrade) {
                node.ShipyardTier++;
            }

            // C-4: Refresh production cache
            RefreshProductionCache();

            Debug.Log($"[BuildingSystem] RequestBuild({nodeId}, {type}): SUCCESS");
            OnBuildingConstructed?.Invoke(nodeId, type);
            return BuildResult.Success();
        }

        /// <summary>
        /// Refreshes the production cache after a building change.
        /// Called automatically after RequestBuild.
        /// </summary>
        public void RefreshProductionCache()
        {
            // ColonyManager's tick loop re-reads GetNodeProductionDelta each tick,
            // so cache refresh is implicit. This method exists for explicit
            // call-sites that need to verify the cache is current.
            // Story 018: future optimization could add a cache dictionary here
        }

        /// <summary>
        /// Computes net production delta for a node based on its buildings.
        /// Called by ColonyManager.Tick() each second.
        /// </summary>
        public ProductionDelta GetNodeProductionDelta(string nodeId)
        {
            var node = GetNodeById(nodeId);
            if (node == null) return ProductionDelta.Zero;

            int totalOre = 0;
            int totalEnergy = 0;

            foreach (var building in node.Buildings) {
                if (!building.IsActive) continue;
                var (ore, energy) = BuildingProduction.GetDelta(building.BuildingType);
                totalOre += ore;
                totalEnergy += energy;
            }

            return new ProductionDelta(totalOre, totalEnergy);
        }

        /// <summary>
        /// Gets all node IDs currently owned by a given ownership state.
        /// </summary>
        public System.Collections.Generic.IEnumerable<string> GetNodesByOwner(OwnershipState ownership)
        {
            var map = GameDataManager.Instance?.GetStarMapData();
            if (map == null) yield break;

            foreach (var node in map.Nodes) {
                if (node.Ownership == ownership) {
                    yield return node.Id;
                }
            }
        }

        private StarNode GetNodeById(string nodeId)
        {
            var map = GameDataManager.Instance?.GetStarMapData();
            if (map == null) return null;
            foreach (var node in map.Nodes) {
                if (node.Id == nodeId) return node;
            }
            return null;
        }
    }

    /// <summary>
    /// Net production delta per second for a colony node.
    /// </summary>
    public readonly struct ProductionDelta
    {
        public readonly int OrePerSec;
        public readonly int EnergyPerSec;

        public ProductionDelta(int orePerSec, int energyPerSec)
        {
            OrePerSec = orePerSec;
            EnergyPerSec = energyPerSec;
        }

        public static ProductionDelta Zero => new ProductionDelta(0, 0);
    }

    /// <summary>
    /// Result of a building request.
    /// </summary>
    public readonly struct BuildResult
    {
        public bool Success { get; }
        public string FailReason { get; }

        public BuildResult(bool success, string failReason = null)
        {
            Success = success;
            FailReason = failReason;
        }

        public static BuildResult Success() => new BuildResult(true);
        public static BuildResult Failure(string reason) => new BuildResult(false, reason);
    }
}
