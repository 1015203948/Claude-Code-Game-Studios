using UnityEngine;

namespace Gameplay
{
    [CreateAssetMenu(fileName = "Module_Weapon_T1", menuName = "Starchain/Equipment/Weapon")]
    public class EquipmentModule : ScriptableObject
    {
        [Header("Identity")]
        public string ModuleId;
        public SlotType SlotType;
        public ModuleTier Tier;

        [Header("Attributes")]
        [Tooltip("武器伤害加成")]
        public float Damage;
        [Tooltip("引擎速度加成")]
        public float Speed;
        [Tooltip("护盾容量加成")]
        public float Shield;
        [Tooltip("货舱容量加成")]
        public float Cargo;

        [Header("Visuals")]
        public Sprite Icon;
    }
}