using System;
using System.Linq;
using System.Collections.Generic;
using Nyangbingo.Core;
using Nyangbingo.Data;

namespace Nyangbingo.Inventory
{
    public sealed class EquipmentCollection
    {
        private readonly HashSet<string> ownedIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly Func<string, EquipmentDefinition> findEquipment;
        private readonly Inventory inventory;
        private readonly Inventory cursor;

        public EquipmentCollection(Func<string, EquipmentDefinition> findEquipment,
            Inventory inventory = null, Inventory cursor = null)
        {
            this.findEquipment = findEquipment ?? throw new ArgumentNullException(nameof(findEquipment));
            this.inventory = inventory;
            this.cursor = cursor;
        }

        public int Count => inventory != null ? Export().Count : ownedIds.Count;
        public Func<IEnumerable<string>> EquippedIds { get; set; }
        public event Action<EquipmentDefinition> Added;

        public bool Contains(string equipmentId) => !string.IsNullOrWhiteSpace(equipmentId) &&
            (inventory != null ? inventory.Count(equipmentId) + (cursor?.Count(equipmentId) ?? 0) > 0 ||
             EquippedIds?.Invoke()?.Contains(equipmentId) == true :
                ownedIds.Contains(equipmentId));

        public bool TryAdd(EquipmentDefinition definition)
        {
            if (inventory != null)
            {
                if (definition == null || findEquipment(definition.Id) != definition ||
                    Contains(definition.Id) || !inventory.TryAdd(definition.Id, 1)) return false;
                Added?.Invoke(definition);
                return true;
            }
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id) ||
                findEquipment(definition.Id) != definition || !ownedIds.Add(definition.Id)) return false;
            Added?.Invoke(definition);
            return true;
        }

        public List<string> Export()
        {
            var ids = new HashSet<string>(ownedIds);
            if (inventory != null)
            {
                ids.Clear();
                foreach (var slot in inventory.Slots)
                    if (slot.amount > 0 && findEquipment(slot.itemId) != null) ids.Add(slot.itemId);
                if (cursor != null)
                    foreach (var slot in cursor.Slots)
                        if (slot.amount > 0 && findEquipment(slot.itemId) != null) ids.Add(slot.itemId);
            }
            if (EquippedIds != null)
                foreach (var id in EquippedIds()) if (!string.IsNullOrEmpty(id)) ids.Add(id);
            var result = new List<string>(ids);
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        public bool TryImport(IEnumerable<string> equipmentIds)
        {
            if (equipmentIds == null) return false;
            var restored = new HashSet<string>(StringComparer.Ordinal);
            foreach (var equipmentId in equipmentIds)
                if (string.IsNullOrWhiteSpace(equipmentId) || findEquipment(equipmentId)?.Id != equipmentId ||
                    !restored.Add(equipmentId))
                    return false;

            ownedIds.Clear();
            foreach (var equipmentId in restored) ownedIds.Add(equipmentId);
            return true;
        }
    }

    public sealed class EquipmentAcquisitionBinding : IDisposable
    {
        private readonly EquipmentCollection collection;
        private bool disposed;

        public EquipmentAcquisitionBinding(EquipmentCollection collection)
        {
            this.collection = collection ?? throw new ArgumentNullException(nameof(collection));
            EquipmentAcquisition.Requested += HandleRequested;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            EquipmentAcquisition.Requested -= HandleRequested;
        }

        private void HandleRequested(EquipmentDefinition definition)
        {
            if (!disposed) collection.TryAdd(definition);
        }
    }

    public sealed class EquipmentSystem
    {
        private readonly Inventory inventory;
        public EquipmentSystem(Inventory playerInventory = null) => inventory = playerInventory;
        private readonly Dictionary<EquipmentSlot, EquipmentDefinition> equipped = new Dictionary<EquipmentSlot, EquipmentDefinition>();
        public event Action Changed;
        public int TotalDefense
        {
            get
            {
                long total = 0;
                foreach (var pair in equipped)
                    if (pair.Value != null)
                        total = Math.Min(int.MaxValue, total + Math.Max(0, pair.Value.Defense));
                return (int)total;
            }
        }
        public bool TryEquip(EquipmentDefinition item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Id) || item.IsAccessory || (item.Slot != EquipmentSlot.Head &&
                item.Slot != EquipmentSlot.Body && item.Slot != EquipmentSlot.Feet)) return false;
            return TryEquipInSlot(item, item.Slot);
        }
        public bool TryEquipAccessory(EquipmentDefinition item, int accessoryIndex)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Id) || !item.IsAccessory ||
                (item.Slot != EquipmentSlot.AccessoryOne && item.Slot != EquipmentSlot.AccessoryTwo) ||
                accessoryIndex < 0 || accessoryIndex > 1 || ContainsId(item.Id)) return false;
            return TryEquipInSlot(item, accessoryIndex == 0 ? EquipmentSlot.AccessoryOne : EquipmentSlot.AccessoryTwo);
        }
        public EquipmentDefinition Get(EquipmentSlot slot) => equipped.TryGetValue(slot, out var item) ? item : null;
        public static bool CanEquipInSlot(EquipmentDefinition item, EquipmentSlot slot)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Id)) return false;
            if (slot == EquipmentSlot.AccessoryOne || slot == EquipmentSlot.AccessoryTwo)
                return item.IsAccessory && (item.Slot == EquipmentSlot.AccessoryOne ||
                                            item.Slot == EquipmentSlot.AccessoryTwo);
            return !item.IsAccessory && item.Slot == slot &&
                   (slot == EquipmentSlot.Head || slot == EquipmentSlot.Body || slot == EquipmentSlot.Feet);
        }

        // Inventory-bound equipment consumes one bag item and returns the previous
        // item atomically. Moving worn accessories swaps their equipped slots.
        public bool TryEquipInSlot(EquipmentDefinition item, EquipmentSlot slot, int sourceIndex = -1)
        {
            if (!CanEquipInSlot(item, slot)) return false;
            if (Get(slot) == item) return true;
            EquipmentSlot? previousSlot = null;
            foreach (var pair in equipped)
                if (pair.Value != null && pair.Value.Id == item.Id) previousSlot = pair.Key;
            if (inventory != null && !previousSlot.HasValue)
                return inventory.TryExchangeEquipment(item.Id, Get(slot)?.Id, () =>
                {
                    equipped[slot] = item;
                    Changed?.Invoke();
                    GameEvents.RaiseEquipmentEquipped();
                }, sourceIndex);
            if (inventory != null && previousSlot.HasValue)
            {
                var old = Get(slot);
                equipped.Remove(previousSlot.Value);
                equipped[slot] = item;
                if (old != null) equipped[previousSlot.Value] = old;
            }
            else
            {
                if (previousSlot.HasValue) equipped.Remove(previousSlot.Value);
                equipped[slot] = item;
            }
            Changed?.Invoke();
            GameEvents.RaiseEquipmentEquipped();
            return true;
        }
        public bool ColdOk(int roomTemperatureC)
        {
            foreach (var slot in new[] { EquipmentSlot.Head, EquipmentSlot.Body, EquipmentSlot.Feet })
                if (equipped.TryGetValue(slot, out var item) && item != null && !item.ColdOk(roomTemperatureC))
                    return false;
            return true;
        }
        public bool TryUnequip(EquipmentSlot slot, int targetIndex = -1)
        {
            var item = Get(slot);
            if (item == null) return false;
            if (inventory != null)
                return inventory.TryExchangeEquipment(null, item.Id, () =>
                {
                    equipped.Remove(slot);
                    Changed?.Invoke();
                    GameEvents.RaiseEquipmentUnequipped();
                }, targetIndex: targetIndex);
            if (!equipped.Remove(slot)) return false;
            Changed?.Invoke();
            GameEvents.RaiseEquipmentUnequipped();
            return true;
        }
        public Dictionary<EquipmentSlot, EquipmentDefinition> Export() => new Dictionary<EquipmentSlot, EquipmentDefinition>(equipped);
        public bool CanImport(IReadOnlyDictionary<EquipmentSlot, EquipmentDefinition> saved)
        {
            if (saved == null) return false;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in saved)
            {
                var item = pair.Value;
                if (item == null || string.IsNullOrWhiteSpace(item.Id) || !ids.Add(item.Id)) return false;
                var accessorySlot = pair.Key == EquipmentSlot.AccessoryOne || pair.Key == EquipmentSlot.AccessoryTwo;
                var accessoryDefinition = item.Slot == EquipmentSlot.AccessoryOne || item.Slot == EquipmentSlot.AccessoryTwo;
                if (accessorySlot)
                {
                    if (!item.IsAccessory || !accessoryDefinition) return false;
                }
                else if (item.IsAccessory || item.Slot != pair.Key) return false;
            }
            return true;
        }
        public bool TryImport(IReadOnlyDictionary<EquipmentSlot, EquipmentDefinition> saved)
        {
            if (!CanImport(saved)) return false;

            equipped.Clear();
            foreach (var pair in saved) equipped.Add(pair.Key, pair.Value);
            Changed?.Invoke();
            return true;
        }
        public void Clear()
        {
            if (equipped.Count == 0) return;
            equipped.Clear();
            Changed?.Invoke();
        }
        private bool ContainsId(string id) { foreach (var pair in equipped) if (pair.Value != null && pair.Value.Id == id) return true; return false; }
    }
}
