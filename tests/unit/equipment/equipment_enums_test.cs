using NUnit.Framework;
using Gameplay;

[TestFixture]
public class EquipmentEnums_Test
{
    [Test]
    public void slotType_has_four_values()
    {
        var values = System.Enum.GetValues(typeof(SlotType));
        Assert.AreEqual(4, values.Length);
        Assert.IsTrue(System.Enum.IsDefined(typeof(SlotType), SlotType.Weapon));
        Assert.IsTrue(System.Enum.IsDefined(typeof(SlotType), SlotType.Engine));
        Assert.IsTrue(System.Enum.IsDefined(typeof(SlotType), SlotType.Shield));
        Assert.IsTrue(System.Enum.IsDefined(typeof(SlotType), SlotType.Cargo));
    }

    [Test]
    public void moduleTier_has_three_values_ordered()
    {
        var values = (ModuleTier[])System.Enum.GetValues(typeof(ModuleTier));
        Assert.AreEqual(3, values.Length);
        Assert.AreEqual(ModuleTier.T1, values[0]);
        Assert.AreEqual(ModuleTier.T2, values[1]);
        Assert.AreEqual(ModuleTier.T3, values[2]);
    }
}