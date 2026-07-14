using System;
using System.Collections.Generic;
using Nyangbingo.Data;

namespace Nyangbingo.Inventory
{
    [Serializable]
    public struct InventorySlot { public string itemId; public int amount; }

    public sealed class Inventory
    {
        public const int SlotCount = 12;
        private readonly List<InventorySlot> slots = new List<InventorySlot>(SlotCount);
        private readonly Func<string, ItemDefinition> findItem;
        public event Action Changed;
        public IReadOnlyList<InventorySlot> Slots => slots;

        public Inventory(Func<string, ItemDefinition> findItem)
        {
            this.findItem = findItem;
            for (var i = 0; i < SlotCount; i++) slots.Add(default);
        }

        public int Count(string itemId)
        {
            var total = 0;
            foreach (var slot in slots) if (slot.itemId == itemId) total += slot.amount;
            return total;
        }

        public bool Has(string itemId, int amount) => Count(itemId) >= amount;

        public bool TryAdd(string itemId, int amount)
        {
            var item = findItem(itemId);
            if (item == null || amount <= 0 || CapacityFor(itemId, item.MaxStack) < amount) return false;
            for (var i = 0; i < slots.Count && amount > 0; i++)
            {
                var slot = slots[i];
                if (slot.itemId != itemId || slot.amount >= item.MaxStack) continue;
                var added = Math.Min(amount, item.MaxStack - slot.amount);
                slot.amount += added; amount -= added; slots[i] = slot;
            }
            for (var i = 0; i < slots.Count && amount > 0; i++)
            {
                if (!string.IsNullOrEmpty(slots[i].itemId)) continue;
                var added = Math.Min(amount, item.MaxStack);
                slots[i] = new InventorySlot { itemId = itemId, amount = added }; amount -= added;
            }
            Changed?.Invoke(); return true;
        }

        public bool TryRemove(string itemId, int amount)
        {
            if (!Has(itemId, amount) || amount <= 0) return false;
            for (var i = slots.Count - 1; i >= 0 && amount > 0; i--)
            {
                var slot = slots[i]; if (slot.itemId != itemId) continue;
                var removed = Math.Min(amount, slot.amount); slot.amount -= removed; amount -= removed;
                if (slot.amount == 0) slot.itemId = string.Empty; slots[i] = slot;
            }
            Changed?.Invoke(); return true;
        }

        public List<InventorySlot> Export() => new List<InventorySlot>(slots);
        public void Import(IEnumerable<InventorySlot> saved)
        {
            slots.Clear(); foreach (var slot in saved) slots.Add(slot);
            while (slots.Count < SlotCount) slots.Add(default);
            if (slots.Count > SlotCount) slots.RemoveRange(SlotCount, slots.Count - SlotCount);
            Changed?.Invoke();
        }

        private int CapacityFor(string itemId, int maxStack)
        {
            var capacity = 0;
            foreach (var slot in slots) capacity += slot.itemId == itemId ? maxStack - slot.amount : string.IsNullOrEmpty(slot.itemId) ? maxStack : 0;
            return capacity;
        }
    }
}
