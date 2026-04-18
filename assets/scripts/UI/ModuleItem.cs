using UnityEngine;
using UnityEngine.UI;
using Game.Data;
using Gameplay;

namespace Game.UI {
    /// <summary>
    /// Single module entry in the inventory list.
    /// Attached to the prefab item spawned per inventory module.
    /// </summary>
    public class ModuleItem : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Text _nameText;
        [SerializeField] private Text _tierText;
        [SerializeField] private Text _slotText;

        /// <summary>
        /// Initialize this item with a module's data.
        /// </summary>
        public void Init(EquipmentModule module)
        {
            if (module == null) return;

            if (_nameText != null) _nameText.text = module.ModuleId;
            if (_tierText != null) _tierText.text = module.Tier.ToString();
            if (_slotText != null) _slotText.text = module.SlotType.ToString();
        }
    }
}