using NUnit.Framework;
using UnityEngine;
using Game.Data;
using Gameplay;

[TestFixture]
public class LootTable_Test
{
    [Test]
    public void dropRoll_returns_null_on_empty_table()
    {
        var table = ScriptableObject.CreateInstance<ShipLootTable>();
        table.Entries = new System.Collections.Generic.List<LootEntry>();

        var result = table.RollDrop();
        Assert.IsNull(result);
    }

    [Test]
    public void dropRoll_uses_weight_distribution()
    {
        var table = ScriptableObject.CreateInstance<ShipLootTable>();
        table.Entries = new System.Collections.Generic.List<LootEntry>
        {
            new LootEntry { SlotType = SlotType.Weapon, Tier = ModuleTier.T1, DropWeight = 70f },
            new LootEntry { SlotType = SlotType.Weapon, Tier = ModuleTier.T2, DropWeight = 30f },
        };

        // 跑 100 次，验证 T2 出现次数在合理范围（20-40次）
        int t2Count = 0;
        for (int i = 0; i < 100; i++) {
            var result = table.RollDrop();
            if (result.HasValue && result.Value.Tier == ModuleTier.T2) t2Count++;
        }
        Assert.IsTrue(t2Count > 15, $"T2 should appear ~30 times, got {t2Count}");
        Assert.IsTrue(t2Count < 50, $"T2 should appear ~30 times, got {t2Count}");
    }
}