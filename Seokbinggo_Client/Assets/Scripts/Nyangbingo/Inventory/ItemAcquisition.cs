using System;
using Nyangbingo.Core;
using Nyangbingo.Data;

namespace Nyangbingo.Inventory
{
    public static class ItemAcquisition
    {
        public static event Action<ItemDefinition, int> Requested;
        public static void Request(ItemDefinition item, int amount)
        {
            if (item == null || amount <= 0) return;
            Requested?.Invoke(item, amount);
            GameEvents.RaiseItemAcquired();
        }
    }

    public static class EquipmentAcquisition
    {
        public static event Action<EquipmentDefinition> Requested;
        public static void Request(EquipmentDefinition equipment)
        {
            if (equipment != null) Requested?.Invoke(equipment);
        }
    }
}
