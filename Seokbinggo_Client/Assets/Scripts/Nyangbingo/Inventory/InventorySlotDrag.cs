namespace Nyangbingo.Inventory
{
    /// <summary>Non-owning storage drag; escape, focus loss and invalid drops are lossless.</summary>
    public sealed class InventorySlotDrag
    {
        public Inventory Source { get; private set; }
        public int SourceIndex { get; private set; } = -1;
        public InventorySlot Preview { get; private set; }
        public bool IsActive => Source != null;

        public bool Begin(Inventory source, int index)
        {
            Cancel();
            if (source == null || index < 0 || index >= source.Capacity || source.Slots[index].amount <= 0)
                return false;
            Source = source;
            SourceIndex = index;
            Preview = source.Slots[index];
            return true;
        }

        public bool TryDrop(Inventory target, int index)
        {
            try { return IsActive && Source.TryDropSlotTo(SourceIndex, target, index, Preview); }
            finally { Cancel(); }
        }

        public void Cancel() { Source = null; SourceIndex = -1; Preview = default; }
    }
}
