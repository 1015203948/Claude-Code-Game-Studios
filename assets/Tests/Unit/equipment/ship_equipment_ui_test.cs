using NUnit.Framework;
using UnityEngine;
using Game.Data;
using Gameplay;
using System.Reflection;

[TestFixture]
public class ShipEquipmentUI_Test
{
    [SetUp]
    public void SetUp()
    {
        ShipDataModel.ClearInventory();
        if (ShipEquipmentSystem.Instance != null)
            Object.DestroyImmediate(ShipEquipmentSystem.Instance.gameObject);
        ShipEquipmentSystem.ResetInstanceForTest();
    }

    [TearDown]
    public void TearDown()
    {
        ShipDataModel.ClearInventory();
        if (ShipEquipmentSystem.Instance != null)
            Object.DestroyImmediate(ShipEquipmentSystem.Instance.gameObject);
        ShipEquipmentSystem.ResetInstanceForTest();
    }

    [Test]
    public void highlightSlots_shows_correct_count()
    {
        var sysGo = new GameObject("EQ");
        var sys = sysGo.AddComponent<ShipEquipmentSystem>();
        // Workaround: Unity EditMode static field may not be set by Awake
        ShipEquipmentSystem.SetInstanceForTest(sys);

        var uiGo = new GameObject("SEUI");
        var ui = uiGo.AddComponent<ShipEquipmentUI>();

        // Inject serialized fields
        var highlightRoot = new GameObject("HighlightRoot");
        highlightRoot.transform.SetParent(uiGo.transform);
        SetField(ui, "slotHighlightRoot", highlightRoot.transform);

        var highlightPrefab = new GameObject("SlotHighlightPrefab");
        highlightPrefab.AddComponent<SlotHighlight>();
        SetField(ui, "slotHighlightPrefab", highlightPrefab);

        var bp = ScriptableObject.CreateInstance<HullBlueprint>();
        bp.HullType = HullType.Fighter;
        bp.SlotConfiguration = new[] { SlotType.Weapon, SlotType.Engine, SlotType.Shield };
        var ship = new ShipDataModel("s1", "t1", true, bp,
            ScriptableObject.CreateInstance<ShipStateChannel>());

        sys.OpenForShip(ship);
        ui.RefreshSlotHighlights();

        Assert.AreEqual(3, ui.SlotHighlights.Count);

        Object.DestroyImmediate(highlightPrefab);
        Object.DestroyImmediate(uiGo);
        Object.DestroyImmediate(sysGo);
    }

    private static void SetField(object obj, string name, object value)
    {
        var field = obj.GetType().GetField(name,
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"Field '{name}' not found");
        field.SetValue(obj, value);
    }
}
