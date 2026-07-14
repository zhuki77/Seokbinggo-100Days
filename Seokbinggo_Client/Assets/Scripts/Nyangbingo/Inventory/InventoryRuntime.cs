using Nyangbingo.Data;
using UnityEngine;

namespace Nyangbingo.Inventory
{
    public sealed class InventoryRuntime : MonoBehaviour
    {
        [SerializeField] private GameDataCatalog catalog;
        private Inventory inventory;
        public Inventory Model => inventory;

        private void Awake() => inventory = new Inventory(id => catalog == null ? null : catalog.FindItem(id));
        private void OnEnable() => ItemAcquisition.Requested += Receive;
        private void OnDisable() => ItemAcquisition.Requested -= Receive;
        public void Receive(ItemDefinition item, int amount)
        {
            if (inventory != null) inventory.TryAdd(item.Id, amount);
        }
    }
}
