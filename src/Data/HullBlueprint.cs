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
        public HullType HullType;
        public SlotType[] SlotConfiguration;  // 槽位类型列表，顺序固定

        [Header("Base Attributes")]
        public float BaseSpeed;
        public float BaseHull;
        public float BaseWeaponDamage;
        public float BaseShield;
        public float BaseCargo;

        [Header("Visuals")]
        public GameObject Prefab3D;  // 3D 模型预制体（装备 UI 用）
    }
}
