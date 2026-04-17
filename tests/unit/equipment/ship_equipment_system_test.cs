using UnityEngine;
using Game.Data;
using Gameplay;
using NUnit.Framework;

[TestFixture]
public class ShipEquipmentSystem_Test
{
    private ShipEquipmentSystem _sys;

    [SetUp] public void SetUp()
    {
        _sys = new GameObject("EQ").AddComponent<ShipEquipmentSystem>();
    }
    [TearDown] public void TearDown() { Object.DestroyImmediate(_sys.gameObject); }

    [Test]
    public void openForShip_loads_correct_hullBlueprint()
    {
        var ship = CreateFighterShip();
        _sys.OpenForShip(ship);

        Assert.AreEqual(HullType.Fighter, _sys.CurrentHullType);
        Assert.AreEqual(3, _sys.SlotCount);
    }

    [Test]
    public void getModuleForSlot_returns_equipped_or_null()
    {
        var ship = CreateFighterShip();
        var weapon = CreateModule(SlotType.Weapon, ModuleTier.T1);
        ship.EquipModule(weapon);
        _sys.OpenForShip(ship);

        Assert.AreEqual(weapon, _sys.GetEquippedForSlot(0));
        Assert.IsNull(_sys.GetEquippedForSlot(1)); // Engine slot empty
    }

    private ShipDataModel CreateFighterShip()
    {
        var bp = ScriptableObject.CreateInstance<HullBlueprint>();
        bp.HullType = HullType.Fighter;
        bp.SlotConfiguration = new[] { SlotType.Weapon, SlotType.Engine, SlotType.Shield };
        bp.BaseWeaponDamage = 10f; bp.BaseSpeed = 5f; bp.BaseShield = 20f;
        return new ShipDataModel("ship-1", "test_v1", true, bp,
            ScriptableObject.CreateInstance<ShipStateChannel>());
    }

    private EquipmentModule CreateModule(SlotType slot, ModuleTier tier)
    {
        var m = ScriptableObject.CreateInstance<EquipmentModule>();
        m.ModuleId = slot + "_" + tier; m.SlotType = slot; m.Tier = tier;
        return m;
    }
}