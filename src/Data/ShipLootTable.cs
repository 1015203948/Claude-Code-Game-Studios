using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Gameplay;

namespace Game.Data
{
    [System.Serializable]
    public class LootEntry
    {
        public SlotType SlotType;
        public ModuleTier Tier;
        [Range(0f, 100f)] public float DropWeight;
    }

    [CreateAssetMenu(fileName = "LootTable_Standard", menuName = "Starchain/LootTable")]
    public class ShipLootTable : ScriptableObject
    {
        public List<LootEntry> Entries = new();

        public (SlotType, ModuleTier)? RollDrop()
        {
            if (Entries.Count == 0) return null;

            float totalWeight = Entries.Sum(e => e.DropWeight);
            float roll = Random.Range(0f, totalWeight);
            float cumulative = 0f;

            foreach (var entry in Entries) {
                cumulative += entry.DropWeight;
                if (roll <= cumulative) {
                    return (entry.SlotType, entry.Tier);
                }
            }
            return null;
        }
    }
}
