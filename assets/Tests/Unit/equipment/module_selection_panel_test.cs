using NUnit.Framework;
using UnityEngine;
using Game.Data;
using Gameplay;
using System.Reflection;

[TestFixture]
public class ModuleSelectionPanel_Test
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
    public void openForSlot_shows_only_matching_slotType()
    {
        // Create ShipEquipmentSystem instance (needed by ModuleSelectionPanel.Refresh)
        var sysGo = new GameObject("ShipEQSys");
        var sys = sysGo.AddComponent<ShipEquipmentSystem>();
        // Workaround: Unity EditMode static field may not be set by Awake
        ShipEquipmentSystem.SetInstanceForTest(sys);

        var panelGo = new GameObject("Panel");
        var panel = panelGo.AddComponent<ModuleSelectionPanel>();

        // Inject serialized fields
        var contentRoot = new GameObject("ContentRoot");
        contentRoot.transform.SetParent(panelGo.transform);
        SetField(panel, "contentRoot", contentRoot.transform);

        var optionPrefab = new GameObject("ModuleOptionPrefab");
        optionPrefab.AddComponent<ModuleOption>();
        SetField(panel, "moduleOptionPrefab", optionPrefab);

        ShipDataModel.AddToInventory(CreateModule(SlotType.Weapon));
        ShipDataModel.AddToInventory(CreateModule(SlotType.Engine));

        var bp = ScriptableObject.CreateInstance<HullBlueprint>();
        bp.HullType = HullType.Fighter;
        bp.SlotConfiguration = new[] { SlotType.Weapon, SlotType.Engine, SlotType.Shield };
        var ship = new ShipDataModel("s1", "t1", true, bp,
            ScriptableObject.CreateInstance<ShipStateChannel>());

        sys.OpenForShip(ship);

        panel.OpenForSlot(0); // Weapon slot = index 0

        Assert.AreEqual(1, panel.ModuleCount);
        Object.DestroyImmediate(optionPrefab);
        Object.DestroyImmediate(panelGo);
        Object.DestroyImmediate(sysGo);
    }

    private static EquipmentModule CreateModule(SlotType slot)
    {
        var m = ScriptableObject.CreateInstance<EquipmentModule>();
        m.ModuleId = "test_" + slot;
        m.SlotType = slot;
        m.Tier = ModuleTier.T1;
        return m;
    }

    private static void SetField(object obj, string name, object value)
    {
        var field = obj.GetType().GetField(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"Field '{name}' not found");
        field.SetValue(obj, value);
    }
}
