using System;
using Nyangbingo.Data;

namespace Nyangbingo.Inventory
{
    public static class ItemAcquisition
    {
        public static event Action<ItemDefinition, int> Requested;
        public static void Request(ItemDefinition item, int amount)
        {
            if (item != null && amount > 0) Requested?.Invoke(item, amount);
        }
    }
}
