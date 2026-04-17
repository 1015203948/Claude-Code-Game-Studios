using NUnit.Framework;
using UnityEngine;
using Gameplay;

[TestFixture]
public class EquipmentModule_Test
{
    [Test]
    public void module_stores_all_properties()
    {
        var module = ScriptableObject.CreateInstance<EquipmentModule>();
        module.ModuleId = "weapon_t2_heavy_laser";
        module.SlotType = SlotType.Weapon;
        module.Tier = ModuleTier.T2;
        module.Damage = 15f;
        module.Speed = 0f;
        module.Shield = 0f;
        module.Cargo = 0f;

        Assert.AreEqual("weapon_t2_heavy_laser", module.ModuleId);
        Assert.AreEqual(SlotType.Weapon, module.SlotType);
        Assert.AreEqual(ModuleTier.T2, module.Tier);
        Assert.AreEqual(15f, module.Damage);
    }

    [Test]
    public void engine_module_has_speed_bonus_no_damage()
    {
        var module = ScriptableObject.CreateInstance<EquipmentModule>();
        module.ModuleId = "engine_t1_standard";
        module.SlotType = SlotType.Engine;
        module.Tier = ModuleTier.T1;
        module.Damage = 0f;
        module.Speed = 5f;
        module.Shield = 0f;
        module.Cargo = 0f;

        Assert.AreEqual(0f, module.Damage);
        Assert.AreEqual(5f, module.Speed);
    }
}