// tests/unit/equipment/loot_drop_system_test.cs
[TestFixture]
public class LootDropSystem_Test
{
    [Test]
    public void onEnemyDestroyed_adds_module_to_inventory()
    {
        var lds = new GameObject("LDS").AddComponent<LootDropSystem>();
        var lootTable = ScriptableObject.CreateInstance<ShipLootTable>();
        lootTable.Entries = new System.Collections.Generic.List<LootEntry>
        {
            new LootEntry { SlotType = SlotType.Weapon, Tier = ModuleTier.T1, DropWeight = 100f }
        };
        lds.LootTable = lootTable;

        var initialCount = ShipDataModel.Inventory.Count;
        lds.OnEnemyDestroyed("enemy-1");

        Assert.AreEqual(initialCount + 1, ShipDataModel.Inventory.Count);
        Object.DestroyImmediate(lds.gameObject);
    }
}