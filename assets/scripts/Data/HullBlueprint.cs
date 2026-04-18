using UnityEngine;
using Gameplay;

namespace Game.Data
{
    public enum HullType
    {
        Fighter   = 0,
        Destroyer = 1,
        Cruiser   = 2
    }

    /// <summary>
    /// Layer 1 Config ScriptableObject — read-only hull blueprint data.
    /// Defines base attributes and slot configuration for each ship hull type.
    /// </summary>
    [CreateAssetMenu(fileName = "Hull_Fighter", menuName = "Starchain/HullBlueprint")]
    public class HullBlueprint : ScriptableObject
    {
        [Header("Identity")]
        /// <summary>船体类型：Fighter / Destroyer / Cruiser</summary>
        public HullType HullType;
        /// <summary>槽位类型数组，决定该船体可装备的模块类型和数量</summary>
        public SlotType[] SlotConfiguration;

        [Header("Base Attributes")]
        /// <summary>基础最高速度（无引擎加成）</summary>
        public float BaseSpeed;
        /// <summary>推力功率 (m/s²)</summary>
        public float ThrustPower = 15f;
        /// <summary>转向速度（度/秒）</summary>
        public float TurnSpeed = 120f;
        /// <summary>基础生命值</summary>
        public float BaseHull;
        /// <summary>基础武器伤害（无武器加成）</summary>
        public float BaseWeaponDamage;
        /// <summary>基础护盾容量</summary>
        public float BaseShield;
        /// <summary>基础货舱容量</summary>
        public float BaseCargo;

        [Header("Carrier Only")]
        /// <summary>Carrier instance ID for carrier-type ships. Null for non-carriers.</summary>
        public string CarrierInstanceId;

        [Header("Visuals")]
        /// <summary>3D 模型预制体，用于装备 UI 中的高亮显示</summary>
        public GameObject Prefab3D;

        /// <summary>Maximum hull points (alias for BaseHull for ShipDataModel compatibility).</summary>
        public float MaxHull => BaseHull;
    }
}
