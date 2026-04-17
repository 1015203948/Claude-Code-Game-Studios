using NUnit.Framework;
using UnityEngine;
using Game.Data;
using Gameplay;

[TestFixture]
public class ModuleSelectionPanel_Test
{
    [Test]
    public void openForSlot_shows_only_matching_slotType()
    {
        var panel = new GameObject("Panel").AddComponent<ModuleSelectionPanel>();
        ShipDataModel.AddToInventory(CreateModule(SlotType.Weapon));
        ShipDataModel.AddToInventory(CreateModule(SlotType.Engine));

        var bp = ScriptableObject.CreateInstance<HullBlueprint>();
        bp.HullType = HullType.Fighter;
        bp.SlotConfiguration = new[] { SlotType.Weapon, SlotType.Engine, SlotType.Shield };
        var ship = new ShipDataModel("s1", "t1", true, bp,
            ScriptableObject.CreateInstance<ShipStateChannel>());
        ShipEquipmentSystem.Instance.OpenForShip(ship);

        panel.OpenForSlot(0); // Weapon slot = index 0

        Assert.AreEqual(1, panel.ModuleCount);
        Object.DestroyImmediate(panel.gameObject);
    }

    private EquipmentModule CreateModule(SlotType slot)
    {
        var m = ScriptableObject.CreateInstance<EquipmentModule>();
        m.ModuleId = "test_" + slot;
        m.SlotType = slot;
        m.Tier = ModuleTier.T1;
        return m;
    }
}
