using System.Collections.Generic;
using Nyangbingo.Data;

namespace Nyangbingo.Inventory
{
    public sealed class ChestProgress
    {
        private readonly HashSet<string> opened = new HashSet<string>();
        public bool IsOpened(string chestId) => opened.Contains(chestId);
        public bool TryOpen(string chestId, ChestDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(chestId) || definition == null || !opened.Add(chestId)) return false;
            foreach (var reward in definition.Rewards)
                ItemAcquisition.Request(reward.item, reward.amount);
            return true;
        }
        public List<string> Export() => new List<string>(opened);
        public void Import(IEnumerable<string> ids)
        {
            opened.Clear();
            if (ids == null) return;
            foreach (var id in ids) if (!string.IsNullOrWhiteSpace(id)) opened.Add(id);
        }
    }
}
