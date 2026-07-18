using System.Collections.Generic;
using Nyangbingo.Data;
using UnityEngine;

namespace Nyangbingo.Inventory
{
    public sealed class InventoryRuntime : MonoBehaviour
    {
        [SerializeField] private GameDataCatalog catalog;
        private Inventory inventory;
        private readonly List<ItemAmount> pending = new List<ItemAmount>();
        public Inventory Model => inventory;
        public IReadOnlyList<ItemAmount> Pending => pending;

        private void Awake() => inventory = new Inventory(id => catalog == null ? null : catalog.FindItem(id));
        private void OnEnable() => ItemAcquisition.Requested += Receive;
        private void OnDisable() => ItemAcquisition.Requested -= Receive;
        public void ConfigureForScene(GameDataCatalog gameDataCatalog) => catalog = gameDataCatalog;
        public bool ConfigureForRuntime(Inventory model)
        {
            if (model == null) return false;
            inventory = model;
            return true;
        }
        public void Receive(ItemDefinition item, int amount)
        {
            if (inventory == null || item == null || amount <= 0) return;
            if (!inventory.TryAdd(item.Id, amount)) pending.Add(new ItemAmount { item = item, amount = amount });
        }
        public bool TryCollectPending(int index)
        {
            if (inventory == null || index < 0 || index >= pending.Count) return false;
            var reward = pending[index];
            if (!inventory.TryAdd(reward.item.Id, reward.amount)) return false;
            pending.RemoveAt(index);
            return true;
        }
        public List<ItemAmount> ExportPending() => new List<ItemAmount>(pending);
        public bool TryRestorePending(IEnumerable<ItemAmount> rewards)
        {
            if (rewards == null) return false;
            var restored = new List<ItemAmount>();
            foreach (var reward in rewards)
            {
                if (reward.item == null || reward.amount <= 0) return false;
                restored.Add(reward);
            }
            pending.Clear();
            pending.AddRange(restored);
            return true;
        }
    }
}
