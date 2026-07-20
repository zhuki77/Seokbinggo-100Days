using System;
using System.Collections.Generic;
using Nyangbingo.Core;
using Nyangbingo.Data;

namespace Nyangbingo.Inventory
{
    public static class ItemAcquisition
    {
        public sealed class CaptureScope : IDisposable
        {
            private bool disposed;
            internal readonly List<ItemAmount> Items = new List<ItemAmount>();
            public IReadOnlyList<ItemAmount> Captured => Items;

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                if (ReferenceEquals(activeCapture, this)) activeCapture = null;
            }
        }

        private static CaptureScope activeCapture;
        public static event Action<ItemDefinition, int> Requested;

        public static CaptureScope CaptureRequests()
        {
            if (activeCapture != null)
                throw new InvalidOperationException("Item acquisition capture cannot be nested.");
            activeCapture = new CaptureScope();
            return activeCapture;
        }

        public static void Request(ItemDefinition item, int amount)
        {
            if (item == null || amount <= 0) return;
            if (activeCapture != null)
            {
                activeCapture.Items.Add(new ItemAmount { item = item, amount = amount });
                return;
            }
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
