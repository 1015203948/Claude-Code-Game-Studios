using UnityEngine;

namespace Game.Data {
    /// <summary>
    /// Layer 1 Config ScriptableObject — read-only data configured in Inspector.
    /// Lives at Assets/Data/Config/ResourceConfig.asset.
    /// </summary>
    [CreateAssetMenu(menuName = "Config/ResourceConfig")]
    public class ResourceConfig : ScriptableObject {
        [Header("Ore Settings")]
        [Tooltip("Maximum ore that can be stored. Clamp target for accumulation.")]
        public int ORE_CAP = 1000;

        [Header("Energy Settings")]
        [Tooltip("-1 = no cap")]
        public int ENERGY_CAP = -1;

        [Header("Production Rates (per second)")]
        [Tooltip("Ore produced per active mine per second.")]
        public int OrePerMine = 10;

        [Tooltip("Base energy output per colony per second (before mine/shipyard consumption).")]
        public int EnergyPerColony = 5;

        /// <summary>
        /// Pure function — returns true if current resources can afford the cost.
        /// Does NOT modify any runtime state.
        /// </summary>
        public static bool CanAfford(int currentOre, int currentEnergy, ResourceBundle cost) {
            return currentOre >= cost.Ore && currentEnergy >= cost.Energy;
        }

        /// <summary>
        /// Ore accumulation clamped to [0, ORE_CAP].
        /// </summary>
        public static int ClampOre(int oreDelta, int currentOre, int oreCap) {
            return Mathf.Clamp(currentOre + oreDelta, 0, oreCap);
        }
    }
}
