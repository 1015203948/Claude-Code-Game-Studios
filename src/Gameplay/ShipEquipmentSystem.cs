using UnityEngine;
using Game.Data;
using Gameplay;

public class ShipEquipmentSystem : MonoBehaviour
{
    public static ShipEquipmentSystem Instance { get; private set; }

    private ShipDataModel _currentShip;
    private HullBlueprint _currentBlueprint;

    public HullType CurrentHullType => _currentBlueprint.HullType;
    public int SlotCount => _currentBlueprint.SlotConfiguration.Length;
    public ShipDataModel GetCurrentShip() => _currentShip;

    private void Awake() { Instance = this; }

    public void OpenForShip(ShipDataModel ship)
    {
        _currentShip = ship;
        _currentBlueprint = ship.HullBlueprint;
    }

    public SlotType GetSlotType(int slotIndex)
        => _currentBlueprint.SlotConfiguration[slotIndex];

    public EquipmentModule GetEquippedForSlot(int slotIndex)
    {
        var slotType = GetSlotType(slotIndex);
        return _currentShip.GetEquipped(slotType);
    }

    public void EquipToSlot(int slotIndex, EquipmentModule module)
    {
        var slotType = GetSlotType(slotIndex);
        if (module.SlotType != slotType) {
            Debug.LogWarning($"[EQ] Module {module.ModuleId} cannot go in slot {slotType}");
            return;
        }
        _currentShip.EquipModule(module);
    }

    public void UnequipSlot(int slotIndex)
    {
        var slotType = GetSlotType(slotIndex);
        _currentShip.UnequipModule(slotType);
    }
}