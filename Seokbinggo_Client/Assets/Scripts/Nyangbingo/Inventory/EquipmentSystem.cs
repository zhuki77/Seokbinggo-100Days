using System;
using System.Collections.Generic;
using Nyangbingo.Core;
using Nyangbingo.Data;

namespace Nyangbingo.Inventory
{
    public sealed class EquipmentCollection
    {
        private readonly HashSet<string> ownedIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly Func<string, EquipmentDefinition> findEquipment;

        public EquipmentCollection(Func<string, EquipmentDefinition> findEquipment)
        {
            this.findEquipment = findEquipment ?? throw new ArgumentNullException(nameof(findEquipment));
        }

        public int Count => ownedIds.Count;
        public event Action<EquipmentDefinition> Added;

        public bool Contains(string equipmentId) => !string.IsNullOrWhiteSpace(equipmentId) && ownedIds.Contains(equipmentId);

        public bool TryAdd(EquipmentDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id) ||
                findEquipment(definition.Id) != definition || !ownedIds.Add(definition.Id)) return false;
            Added?.Invoke(definition);
            return true;
        }

        public List<string> Export()
        {
            var result = new List<string>(ownedIds);
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
            equipped[item.Slot] = item;
            Changed?.Invoke();
            return true;
        }
        public bool TryEquipAccessory(EquipmentDefinition item, int accessoryIndex)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Id) || !item.IsAccessory ||
                (item.Slot != EquipmentSlot.AccessoryOne && item.Slot != EquipmentSlot.AccessoryTwo) ||
                accessoryIndex < 0 || accessoryIndex > 1 || ContainsId(item.Id)) return false;
            equipped[accessoryIndex == 0 ? EquipmentSlot.AccessoryOne : EquipmentSlot.AccessoryTwo] = item;
            Changed?.Invoke();
            return true;
        }
        public EquipmentDefinition Get(EquipmentSlot slot) => equipped.TryGetValue(slot, out var item) ? item : null;
        public bool TryUnequip(EquipmentSlot slot)
        {
            if (!equipped.Remove(slot)) return false;
            Changed?.Invoke();
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
