using UnityEngine;
using UnityEngine.UI;
using Game.Data;
using Gameplay;

namespace Game.UI {
    /// <summary>
    /// Inventory UI panel — displays all modules in the global inventory.
    /// Populate is called when the panel is opened/refreshed.
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Transform _contentRoot;      // Vertical Layout Group — parent for items
        [SerializeField] private GameObject _moduleItemPrefab; // Prefab with ModuleItem component

        /// <summary>
        /// Refresh the inventory list. Call when panel opens or inventory changes.
        /// </summary>
        public void Populate()
        {
            if (_contentRoot == null) {
                Debug.LogWarning("[InventoryUI] contentRoot is not assigned.");
                return;
            }

            // Clear old list items
            foreach (Transform child in _contentRoot) {
                Destroy(child.gameObject);
            }

            // Populate from static inventory
            foreach (var module in ShipDataModel.Inventory)
            {
                if (_moduleItemPrefab == null) {
                    Debug.LogWarning("[InventoryUI] moduleItemPrefab is not assigned.");
                    break;
                }
                var item = Instantiate(_moduleItemPrefab, _contentRoot);
                var slot = item.GetComponent<ModuleItem>();
                if (slot != null) {
                    slot.Init(module);
                }
            }
        }
    }
}