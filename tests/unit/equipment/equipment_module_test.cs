using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Gameplay;

[TestFixture]
public class EquipmentModule_Test
{
    private List<EquipmentModule> _created = new List<EquipmentModule>();

    [TearDown]
    public void TearDown()
    {
        foreach (var obj in _created) {
            if (obj != null) Object.DestroyImmediate(obj);
        }
        _created.Clear();
    }

    [Test]
    public void module_stores_all_properties()
    {
        var m = ScriptableObject.CreateInstance<EquipmentModule>();
        m.ModuleId = "weapon_t2_heavy_laser";
        m.SlotType = SlotType.Weapon;
        m.Tier = ModuleTier.T2;
        m.Damage = 15f;
        m.Speed = 0f;
        m.Shield = 0f;
        m.Cargo = 0f;
        _created.Add(m);

        Assert.AreEqual("weapon_t2_heavy_laser", m.ModuleId);
        Assert.AreEqual(SlotType.Weapon, m.SlotType);
        Assert.AreEqual(ModuleTier.T2, m.Tier);
        Assert.AreEqual(15f, m.Damage);
    }

    [Test]
    public void engine_module_has_speed_bonus_no_damage()
    {
        var m = ScriptableObject.CreateInstance<EquipmentModule>();
        m.ModuleId = "engine_t1_standard";
        m.SlotType = SlotType.Engine;
        m.Tier = ModuleTier.T1;
        m.Damage = 0f;
        m.Speed = 5f;
        m.Shield = 0f;
        m.Cargo = 0f;
        _created.Add(m);

        Assert.AreEqual(0f, m.Damage);
        Assert.AreEqual(5f, m.Speed);
    }
}