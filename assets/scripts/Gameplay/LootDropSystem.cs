// src/Gameplay/LootDropSystem.cs
using UnityEngine;
using System.Linq;
using Game.Data;
using Gameplay;

public class LootDropSystem : MonoBehaviour
{
    public static LootDropSystem Instance { get; private set; }
    public ShipLootTable LootTable;

    private void Awake() { Instance = this; }

    /// <summary>Test hook: resets Instance to null for test isolation. Do NOT use in production.</summary>
    internal static void ResetInstanceForTest() => Instance = null;

    /// <summary>Test hook: inject module factory for EditMode test isolation. Do NOT use in production.</summary>
    internal static System.Func<SlotType, ModuleTier, EquipmentModule> TestModuleFactory { get; set; }

    public void OnEnemyDestroyed(string enemyId)
    {
        if (LootTable == null) return;
        var (slotType, tier) = LootTable.RollDrop() ?? (SlotType.Weapon, ModuleTier.T1);
        var module = FindModuleInDatabase(slotType, tier);
        if (module != null) ShipDataModel.AddToInventory(module);
    }

    private EquipmentModule FindModuleInDatabase(SlotType slot, ModuleTier tier)
    {
        if (TestModuleFactory != null) return TestModuleFactory(slot, tier);
        var all = Resources.LoadAll<EquipmentModule>("Data/Modules");
        return all.FirstOrDefault(m => m.SlotType == slot && m.Tier == tier);
    }
}