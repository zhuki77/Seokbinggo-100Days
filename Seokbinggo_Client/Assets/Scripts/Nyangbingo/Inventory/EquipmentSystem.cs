using System.Collections.Generic;
using Nyangbingo.Core;
using Nyangbingo.Data;

namespace Nyangbingo.Inventory
{
    public sealed class EquipmentSystem
    {
        private readonly Dictionary<EquipmentSlot, EquipmentDefinition> equipped = new Dictionary<EquipmentSlot, EquipmentDefinition>();
        public int TotalDefense { get { var total = 0; foreach (var pair in equipped) if (pair.Value != null) total += pair.Value.Defense; return total; } }
        public bool TryEquip(EquipmentDefinition item)
        {
            if (item == null || item.IsAccessory) return false;
            equipped[item.Slot] = item; return true;
        }
        public bool TryEquipAccessory(EquipmentDefinition item, int accessoryIndex)
        {
            if (item == null || !item.IsAccessory || accessoryIndex < 0 || accessoryIndex > 1 || ContainsId(item.Id)) return false;
            equipped[accessoryIndex == 0 ? EquipmentSlot.AccessoryOne : EquipmentSlot.AccessoryTwo] = item;
            return true;
        }
        public EquipmentDefinition Get(EquipmentSlot slot) => equipped.TryGetValue(slot, out var item) ? item : null;
        public Dictionary<EquipmentSlot, EquipmentDefinition> Export() => new Dictionary<EquipmentSlot, EquipmentDefinition>(equipped);
        public void Clear() => equipped.Clear();
        private bool ContainsId(string id) { foreach (var pair in equipped) if (pair.Value != null && pair.Value.Id == id) return true; return false; }
    }
}
