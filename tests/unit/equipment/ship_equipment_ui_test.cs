using NUnit.Framework;
using UnityEngine;
using Game.Data;
using Gameplay;

[TestFixture]
public class ShipEquipmentUI_Test
{
    [Test]
    public void highlightSlots_shows_correct_count()
    {
        var sysGo = new GameObject("EQ").AddComponent<ShipEquipmentSystem>();
        var ui = new GameObject("SEUI").AddComponent<ShipEquipmentUI>();

        var bp = ScriptableObject.CreateInstance<HullBlueprint>();
        bp.HullType = HullType.Fighter;
        bp.SlotConfiguration = new[] { SlotType.Weapon, SlotType.Engine, SlotType.Shield };
        var ship = new ShipDataModel("s1", "t1", true, bp,
            ScriptableObject.CreateInstance<ShipStateChannel>());

        ShipEquipmentSystem.Instance.OpenForShip(ship);
        ui.RefreshSlotHighlights();

        Assert.AreEqual(3, ui.SlotHighlights.Count);

        Object.DestroyImmediate(ui.gameObject);
        Object.DestroyImmediate(sysGo.gameObject);
    }
}
