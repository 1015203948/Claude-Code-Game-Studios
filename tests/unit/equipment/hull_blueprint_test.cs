using NUnit.Framework;
using UnityEngine;
using Gameplay;
using Game.Data;

[TestFixture]
public class HullBlueprint_Test
{
    [Test]
    public void fighter_has_3_slots()
    {
        var bp = ScriptableObject.CreateInstance<HullBlueprint>();
        bp.HullType = HullType.Fighter;
        bp.SlotConfiguration = new[] {
            SlotType.Weapon, SlotType.Engine, SlotType.Shield
        };

        Assert.AreEqual(3, bp.SlotConfiguration.Length);
        Assert.AreEqual(SlotType.Weapon, bp.SlotConfiguration[0]);
    }

    [Test]
    public void cruiser_has_cargo_slot()
    {
        var bp = ScriptableObject.CreateInstance<HullBlueprint>();
        bp.HullType = HullType.Cruiser;
        bp.SlotConfiguration = new[] {
            SlotType.Weapon, SlotType.Weapon,
            SlotType.Engine, SlotType.Shield, SlotType.Cargo
        };

        Assert.IsTrue(bp.SlotConfiguration.Contains(SlotType.Cargo));
    }
}
