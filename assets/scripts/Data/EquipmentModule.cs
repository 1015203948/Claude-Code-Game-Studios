using UnityEngine;
using Game.Gameplay;

namespace Gameplay
{
    /// <summary>
    /// 舰船装备模块数据资源，定义模块身份、属性加成和图标。
    /// </summary>
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
        [Tooltip("武器射速 (shots/sec)")]
        public float FireRate = 1f;
        [Tooltip("武器射程 (meters)")]
        public float Range = 200f;
        [Tooltip("伤害类型")]
        public DamageType DamageType = DamageType.Physical;
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