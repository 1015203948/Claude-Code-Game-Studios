using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using Game.Data;
using Gameplay;

public class ModuleSelectionPanel : MonoBehaviour
{
    [SerializeField] private Transform contentRoot;
    [SerializeField] private GameObject moduleOptionPrefab;
    [SerializeField] private Button closeButton;

    private int _currentSlotIndex;

    private void Awake()
    {
        closeButton.onClick.AddListener(() => gameObject.SetActive(false));
        gameObject.SetActive(false);
    }

    public void OpenForSlot(int slotIndex)
    {
        _currentSlotIndex = slotIndex;
        Refresh();
        gameObject.SetActive(true);
    }

    public int ModuleCount => contentRoot.childCount;

    private void Refresh()
    {
        foreach (Transform child in contentRoot) Destroy(child.gameObject);

        var slotType = ShipEquipmentSystem.Instance.GetSlotType(_currentSlotIndex);
        var eligible = ShipDataModel.Inventory
            .Where(m => m.SlotType == slotType);

        foreach (var module in eligible) {
            var go = Instantiate(moduleOptionPrefab, contentRoot);
            var btn = go.GetComponent<ModuleOption>();
            btn.Init(module, _ => SelectModule(module));
        }
    }

    private void SelectModule(EquipmentModule module)
    {
        ShipEquipmentSystem.Instance.EquipToSlot(_currentSlotIndex, module);
        ShipEquipmentSystem.Instance.OpenForShip(ShipEquipmentSystem.Instance.GetCurrentShip());
        gameObject.SetActive(false);
    }
}

public class ModuleOption : MonoBehaviour
{
    [SerializeField] private Text label;
    [SerializeField] private Image icon;
    private System.Action<EquipmentModule> _onSelect;
    private EquipmentModule _module;

    public void Init(EquipmentModule module, System.Action<EquipmentModule> onSelect)
    {
        _module = module;
        _onSelect = onSelect;
        label.text = $"{module.Tier} {module.SlotType}";
        icon.color = GetTierColor(module.Tier);
        GetComponent<Button>().onClick.AddListener(() => _onSelect(module));
    }

    private Color GetTierColor(ModuleTier tier) => tier switch
    {
        ModuleTier.T1 => Color.white,
        ModuleTier.T2 => Color.green,
        ModuleTier.T3 => Color.blue,
        _ => Color.gray
    };
}
