using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Game.Data;
using Game.UI;
using Gameplay;

[TestFixture]
public class InventoryUI_Test
{
    [TearDown]
    public void TearDown()
    {
        ShipDataModel.ClearInventory();
    }

    [Test]
    public void populate_shows_all_inventory_modules()
    {
        var invGo = new GameObject("InventoryUI");
        var inv = invGo.AddComponent<InventoryUI>();

        // Inject serialized fields — _contentRoot = inv.transform (items are direct children)
        SetField(inv, "_contentRoot", invGo.transform);

        var prefabGo = new GameObject("ModuleItemPrefab");
        prefabGo.AddComponent<ModuleItem>();
        SetField(inv, "_moduleItemPrefab", prefabGo);

        var module1 = CreateModule(SlotType.Weapon);
        var module2 = CreateModule(SlotType.Engine);
        ShipDataModel.AddToInventory(module1);
        ShipDataModel.AddToInventory(module2);

        inv.Populate();

        Assert.AreEqual(2, inv.transform.childCount);
        Object.DestroyImmediate(prefabGo);
        Object.DestroyImmediate(invGo);
    }

    private static EquipmentModule CreateModule(SlotType slot)
    {
        var m = ScriptableObject.CreateInstance<EquipmentModule>();
        m.ModuleId = "test_" + slot.ToString();
        m.SlotType = slot;
        m.Tier = ModuleTier.T1;
        return m;
    }

    private static void SetField(object obj, string name, object value)
    {
        var field = obj.GetType().GetField(name,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.IsNotNull(field, $"Field '{name}' not found");
        field.SetValue(obj, value);
    }
}
