using System;
using Nyangbingo.Core;
using Nyangbingo.Data;

namespace Nyangbingo.Inventory
{
    /// <summary>A non-owning drag preview. Cancellation never removes or restores bag items.</summary>
    public sealed class EquipmentInventoryDrag
    {
        private readonly Inventory inventory;
        private readonly Func<string, EquipmentDefinition> findEquipment;
        public string ItemId { get; private set; }
        public int SourceIndex { get; private set; } = -1;
        public bool IsActive => SourceIndex >= 0;

        public EquipmentInventoryDrag(Inventory inventory, Func<string, EquipmentDefinition> findEquipment)
        {
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            this.findEquipment = findEquipment ?? throw new ArgumentNullException(nameof(findEquipment));
        }

        public bool Begin(int index)
        {
            Cancel();
            if (index < 0 || index >= inventory.Capacity) return false;
            var source = inventory.Slots[index];
            if (source.amount <= 0 || (!ActiveSlotSystem.IsAllowedItemId(source.itemId) &&
                                      findEquipment(source.itemId) == null)) return false;
            SourceIndex = index;
            ItemId = source.itemId;
            return true;
        }

        public bool CanDrop(int visualSlot)
        {
            if (!IsActive || inventory.Slots[SourceIndex].amount <= 0 ||
                inventory.Slots[SourceIndex].itemId != ItemId) return false;
            if (visualSlot == 0) return ActiveSlotSystem.IsAllowedItemId(ItemId);
            return TryGetEquipmentSlot(visualSlot, out var slot) &&
                   EquipmentSystem.CanEquipInSlot(findEquipment(ItemId), slot);
        }

        public bool TryDrop(int visualSlot, EquipmentSystem equipment, ActiveSlotSystem activeSlot)
        {
            try
            {
                if (!CanDrop(visualSlot)) return false;
                if (visualSlot == 0)
                {
                    if (activeSlot == null) return false;
                    if (activeSlot.EquippedItemId == ItemId)
                        return activeSlot.IsUsingEquippedItem || activeSlot.Toggle();
                    return activeSlot.TryEquip(ItemId, SourceIndex);
                }
                return equipment != null && TryGetEquipmentSlot(visualSlot, out var slot) &&
                       equipment.TryEquipInSlot(findEquipment(ItemId), slot, SourceIndex);
            }
            finally { Cancel(); }
        }

        public void Cancel()
        {
            SourceIndex = -1;
            ItemId = null;
        }

        // Matches the six authored EquipmentVisual buttons, not enum ordinal values.
        public static bool TryGetEquipmentSlot(int visualSlot, out EquipmentSlot slot)
        {
            switch (visualSlot)
            {
                case 1: slot = EquipmentSlot.Head; return true;
                case 2: slot = EquipmentSlot.Body; return true;
                case 3: slot = EquipmentSlot.Feet; return true;
                case 4: slot = EquipmentSlot.AccessoryOne; return true;
                case 5: slot = EquipmentSlot.AccessoryTwo; return true;
                default: slot = default; return false;
            }
        }
    }
}
