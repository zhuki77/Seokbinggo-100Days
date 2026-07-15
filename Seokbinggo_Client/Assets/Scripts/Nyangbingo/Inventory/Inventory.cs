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
            long total = 0;
            foreach (var slot in slots)
            {
                if (slot.itemId != itemId) continue;
                total += slot.amount;
                if (total >= int.MaxValue) return int.MaxValue;
            }
            return (int)total;
        }

        public bool Has(string itemId, int amount) => amount > 0 && Count(itemId) >= amount;

        public bool TryAdd(string itemId, int amount)
        {
            var item = findItem(itemId);
            if (item == null || item.MaxStack <= 0 || amount <= 0 || CapacityFor(itemId, item.MaxStack) < amount)
                return false;
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
            TryImport(saved);
        }

        public bool TryImport(IEnumerable<InventorySlot> saved)
        {
            if (!TryBuildImport(saved, out var restored)) return false;
            slots.Clear();
            slots.AddRange(restored);
            Changed?.Invoke();
            return true;
        }

        public bool CanImport(IEnumerable<InventorySlot> saved) => TryBuildImport(saved, out _);

        private bool TryBuildImport(IEnumerable<InventorySlot> saved, out List<InventorySlot> restored)
        {
            restored = null;
            if (saved == null) return false;
            var candidate = new List<InventorySlot>(SlotCount);
            foreach (var slot in saved)
            {
                if (candidate.Count >= SlotCount) return false;
                if (string.IsNullOrEmpty(slot.itemId))
                {
                    if (slot.amount != 0) return false;
                    candidate.Add(default);
                    continue;
                }

                var item = findItem(slot.itemId);
                if (item == null || slot.amount <= 0 || slot.amount > item.MaxStack) return false;
                candidate.Add(slot);
            }

            while (candidate.Count < SlotCount) candidate.Add(default);
            restored = candidate;
            return true;
        }

        private long CapacityFor(string itemId, int maxStack)
        {
            long capacity = 0;
            foreach (var slot in slots)
            {
                if (slot.itemId == itemId) capacity += Math.Max(0L, (long)maxStack - slot.amount);
                else if (string.IsNullOrEmpty(slot.itemId)) capacity += maxStack;
            }
            return capacity;
        }
    }
}
