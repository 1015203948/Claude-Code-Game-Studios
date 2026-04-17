using NUnit.Framework;
using UnityEngine;
using Game.Data;
using Gameplay;

[TestFixture]
public class ShipDataModel_Equipment_Test
{
    private ShipDataModel _ship;
    private HullBlueprint _fighterBp;
    private GameDataManager _gdm;

    [SetUp]
    public void SetUp()
    {
        _gdm = new GameDataManager();
        _fighterBp = ScriptableObject.CreateInstance<HullBlueprint>();
        _fighterBp.HullType = HullType.Fighter;
        _fighterBp.SlotConfiguration = new[] { SlotType.Weapon, SlotType.Engine, SlotType.Shield };
        _fighterBp.BaseWeaponDamage = 10f;
        _fighterBp.BaseSpeed = 5f;
        _fighterBp.BaseShield = 20f;
        _fighterBp.BaseCargo = 0f;
        _ship = new ShipDataModel("ship-1", "test_v1", true, _fighterBp,
            ScriptableObject.CreateInstance<ShipStateChannel>());
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_fighterBp);
    }

    [Test]
    public void equipModule_adds_to_slot()
    {
        var weapon = CreateModule("w1", SlotType.Weapon, ModuleTier.T1, damage: 10f);
        _ship.EquipModule(weapon);
        Assert.AreEqual(weapon, _ship.GetEquipped(SlotType.Weapon));
    }

    [Test]
    public void totalWeaponDamage_includes_base_plus_module()
    {
        var weapon = CreateModule("w2", SlotType.Weapon, ModuleTier.T2, damage: 15f);
        _ship.EquipModule(weapon);
        Assert.AreEqual(25f, _ship.TotalWeaponDamage); // 10 base + 15 module
    }

    [Test]
    public void unequipModule_removes_from_slot_returns_module()
    {
        var weapon = CreateModule("w1", SlotType.Weapon, ModuleTier.T1, damage: 10f);
        _ship.EquipModule(weapon);
        var result = _ship.UnequipModule(SlotType.Weapon);
        Assert.IsNull(_ship.GetEquipped(SlotType.Weapon));
        Assert.AreEqual(weapon, result);
    }

    [Test]
    public void replacing_module_returns_old_module()
    {
        var t1 = CreateModule("w1", SlotType.Weapon, ModuleTier.T1, damage: 10f);
        var t2 = CreateModule("w2", SlotType.Weapon, ModuleTier.T2, damage: 15f);
        _ship.EquipModule(t1);
        var old = _ship.EquipModule(t2);
        Assert.AreEqual(t1, old);
        Assert.AreEqual(25f, _ship.TotalWeaponDamage);
    }

    private EquipmentModule CreateModule(string id, SlotType slot, ModuleTier tier, float damage = 0, float speed = 0, float shield = 0, float cargo = 0)
    {
        var m = ScriptableObject.CreateInstance<EquipmentModule>();
        m.ModuleId = id;
        m.SlotType = slot;
        m.Tier = tier;
        m.Damage = damage;
        m.Speed = speed;
        m.Shield = shield;
        m.Cargo = cargo;
        return m;
    }
}