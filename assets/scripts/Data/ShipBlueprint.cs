using UnityEngine;
using Game.Data;

namespace Game.Data {
    /// <summary>
    /// Layer 1 Config ScriptableObject — read-only blueprint data.
    /// Two assets: Assets/Data/Config/ShipBlueprint_generic_v1.asset,
    ///              Assets/Data/Config/ShipBlueprint_carrier_v1.asset
    /// </summary>
    [CreateAssetMenu(menuName = "Config/ShipBlueprint")]
    public class ShipBlueprint : ScriptableObject {
        [Header("Identity")]
        public string BlueprintId;

        [Header("Combat Stats")]
        public int MaxHull;
        public float ThrustPower;
        public float TurnSpeed; // degrees per second
        public int WeaponSlots;

        [Header("Build Cost")]
        public ResourceBundle BuildCost;

        [Header("Shipyard Requirement")]
        public int RequiredShipyardTier;

        [Header("Carrier Only")]
        [Tooltip("Null for non-carrier blueprints. Set to instance Id string for carrier ships.")]
        public string CarrierInstanceId; // nullable
        public int HangarCapacity;

        /// <summary>
        /// Validates blueprint has legal combat stats.
        /// </summary>
        public static bool IsValid(ShipBlueprint bp) {
            if (bp == null) return false;
            return bp.MaxHull > 0 && bp.ThrustPower > 0 && bp.TurnSpeed > 0 && bp.WeaponSlots >= 0;
        }
    }
}
