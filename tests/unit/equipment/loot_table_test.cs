using NUnit.Framework;
using UnityEngine;
using Game.Data;
using Gameplay;
using System.Collections.Generic;

[TestFixture]
public class LootTable_Test
{
    private List<ShipLootTable> _created = new List<ShipLootTable>();

    [TearDown]
    public void TearDown()
    {
        foreach (var obj in _created) {
            if (obj != null) Object.DestroyImmediate(obj);
        }
        _created.Clear();
    }

    [Test]
    public void dropRoll_returns_null_on_empty_table()
    {
        var table = ScriptableObject.CreateInstance<ShipLootTable>();
        _created.Add(table);
        table.Entries = new System.Collections.Generic.List<LootEntry>();

        var result = table.RollDrop();
        Assert.IsNull(result);
    }

    [Test]
    public void dropRoll_uses_weight_distribution()
    {
        var table = ScriptableObject.CreateInstance<ShipLootTable>();
        _created.Add(table);
        table.Entries = new System.Collections.Generic.List<LootEntry>
        {
            new LootEntry { SlotType = SlotType.Weapon, Tier = ModuleTier.T1, DropWeight = 70f },
            new LootEntry { SlotType = SlotType.Weapon, Tier = ModuleTier.T2, DropWeight = 30f },
        };

        int t2Count = 0;
        for (int i = 0; i < 100; i++) {
            var result = table.RollDrop();
            if (result.HasValue && result.Value.Tier == ModuleTier.T2) t2Count++;
        }
        Assert.IsTrue(t2Count > 15, $"T2 should appear ~30 times, got {t2Count}");
        Assert.IsTrue(t2Count < 50, $"T2 should appear ~30 times, got {t2Count}");
    }
}
