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

    public void OnEnemyDestroyed(string enemyId)
    {
        if (LootTable == null) return;
        var (slotType, tier) = LootTable.RollDrop() ?? (SlotType.Weapon, ModuleTier.T1);
        var module = FindModuleInDatabase(slotType, tier);
        if (module != null) ShipDataModel.AddToInventory(module);
    }

    private EquipmentModule FindModuleInDatabase(SlotType slot, ModuleTier tier)
    {
        var all = Resources.LoadAll<EquipmentModule>("Data/Modules");
        return all.FirstOrDefault(m => m.SlotType == slot && m.Tier == tier);
    }
}