using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using Game.Data;
using Gameplay;

[TestFixture]
public class LootDropSystem_Test
{
    [SetUp]
    public void SetUp()
    {
        ShipDataModel.ClearInventory();
        if (LootDropSystem.Instance != null)
            Object.DestroyImmediate(LootDropSystem.Instance.gameObject);
        LootDropSystem.ResetInstanceForTest();
    }

    [TearDown]
    public void TearDown()
    {
        LootDropSystem.TestModuleFactory = null;
        ShipDataModel.ClearInventory();
        if (LootDropSystem.Instance != null)
            Object.DestroyImmediate(LootDropSystem.Instance.gameObject);
        LootDropSystem.ResetInstanceForTest();
    }

    [Test]
    public void onEnemyDestroyed_adds_module_to_inventory()
    {
        var lds = new GameObject("LDS").AddComponent<LootDropSystem>();
        var lootTable = ScriptableObject.CreateInstance<ShipLootTable>();
        lootTable.Entries = new List<LootEntry>
        {
            new LootEntry { SlotType = SlotType.Weapon, Tier = ModuleTier.T1, DropWeight = 100f }
        };
        lds.LootTable = lootTable;

        // Inject test module factory — Resources.LoadAll doesn't work in EditMode
        LootDropSystem.TestModuleFactory = (slot, tier) => {
            var m = ScriptableObject.CreateInstance<EquipmentModule>();
            m.ModuleId = $"test_{slot}_{tier}";
            m.SlotType = slot;
            m.Tier = tier;
            return m;
        };

        var initialCount = ShipDataModel.Inventory.Count;
        lds.OnEnemyDestroyed("enemy-1");

        Assert.AreEqual(initialCount + 1, ShipDataModel.Inventory.Count);
        Object.DestroyImmediate(lds.gameObject);
    }
}
