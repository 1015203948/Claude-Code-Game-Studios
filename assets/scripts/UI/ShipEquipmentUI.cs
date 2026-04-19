using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Game.Data;
using Gameplay;

public class ShipEquipmentUI : MonoBehaviour
{
    [SerializeField] private GameObject modelViewport;
    [SerializeField] private Transform slotHighlightRoot;
    [SerializeField] private GameObject slotHighlightPrefab;
    [SerializeField] private ModuleSelectionPanel selectionPanel;

    public List<SlotHighlight> SlotHighlights { get; private set; } = new();

    public void RefreshSlotHighlights()
    {
        foreach (Transform child in slotHighlightRoot) Destroy(child.gameObject);
        SlotHighlights.Clear();

        var system = ShipEquipmentSystem.Instance;
        for (int i = 0; i < system.SlotCount; i++) {
            var slotType = system.GetSlotType(i);
            var go = Instantiate(slotHighlightPrefab, slotHighlightRoot);
            var highlight = go.GetComponent<SlotHighlight>();
            highlight.Init(i, slotType, GetColorForSlot(slotType), this);
            SlotHighlights.Add(highlight);
        }
    }

    private Color GetColorForSlot(SlotType type) => type switch
    {
        SlotType.Weapon => Color.red,
        SlotType.Engine => Color.blue,
        SlotType.Shield => Color.green,
        SlotType.Cargo  => Color.yellow,
        _               => Color.gray
    };

    public void OnSlotClicked(int slotIndex)
    {
        selectionPanel.OpenForSlot(slotIndex);
    }
}

public class SlotHighlight : MonoBehaviour
{
    [SerializeField] private Image background;
    private int slotIndex;

    public void Init(int index, SlotType type, Color color, ShipEquipmentUI owner)
    {
        slotIndex = index;
        if (background != null) background.color = color;
        var btn = GetComponent<Button>();
        if (btn != null) btn.onClick.AddListener(() => owner.OnSlotClicked(slotIndex));
    }
}
