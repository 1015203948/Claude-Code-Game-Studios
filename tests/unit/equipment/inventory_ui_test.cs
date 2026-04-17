using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Game.Data;
using Gameplay;

[TestFixture]
public class InventoryUI_Test
{
    [TearDown]
    public void TearDown()
    {
        // 清理所有测试创建的 ScriptableObject
        ShipDataModel.ClearInventory();
    }

    [Test]
    public void populate_shows_all_inventory_modules()
    {
        var inv = new GameObject("InventoryUI").AddComponent<InventoryUI>();
        var module1 = CreateModule(SlotType.Weapon);
        var module2 = CreateModule(SlotType.Engine);
        ShipDataModel.AddToInventory(module1);
        ShipDataModel.AddToInventory(module2);

        inv.Populate();

        Assert.AreEqual(2, inv.transform.childCount);
        Object.DestroyImmediate(inv.gameObject);
    }

    private static EquipmentModule CreateModule(SlotType slot)
    {
        var m = ScriptableObject.CreateInstance<EquipmentModule>();
        m.ModuleId = "test_" + slot.ToString();
        m.SlotType = slot;
        m.Tier = ModuleTier.T1;
        return m;
    }
}