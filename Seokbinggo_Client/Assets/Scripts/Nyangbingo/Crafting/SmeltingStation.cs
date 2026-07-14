using Nyangbingo.Data;
using Nyangbingo.Inventory;
using System.Collections.Generic;

namespace Nyangbingo.Crafting
{
    public sealed class SmeltingStation
    {
        private readonly Nyangbingo.Inventory.Inventory inventory;
        private SmeltingDefinition active;
        private float remaining;
        private readonly List<ItemAmount> completed = new List<ItemAmount>();
        public bool IsSmelting => active != null;
        public float RemainingSeconds => remaining;
        public IReadOnlyList<ItemAmount> Completed => completed;
        public SmeltingStation(Nyangbingo.Inventory.Inventory inventory) { this.inventory = inventory; }

        public bool TryStart(SmeltingDefinition definition)
        {
            if (definition == null || IsSmelting || definition.Input.item == null || definition.Fuel.item == null || definition.Output.item == null) return false;
            if (!inventory.Has(definition.Input.item.Id, definition.Input.amount) || !inventory.Has(definition.Fuel.item.Id, definition.Fuel.amount)) return false;
            inventory.TryRemove(definition.Input.item.Id, definition.Input.amount);
            inventory.TryRemove(definition.Fuel.item.Id, definition.Fuel.amount);
            active = definition; remaining = definition.DurationSeconds; return true;
        }

        public bool Tick(float gameSeconds)
        {
            if (!IsSmelting) return false;
            remaining -= gameSeconds;
            if (remaining > 0f) return false;
            if (!inventory.TryAdd(active.Output.item.Id, active.Output.amount)) completed.Add(active.Output);
            active = null; remaining = 0f; return true;
        }

        public bool TryCollect(int index)
        {
            if (index < 0 || index >= completed.Count) return false;
            var output = completed[index];
            if (!inventory.TryAdd(output.item.Id, output.amount)) return false;
            completed.RemoveAt(index); return true;
        }
    }
}
