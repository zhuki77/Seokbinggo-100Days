using System.Collections.Generic;
using Nyangbingo.Data;

namespace Nyangbingo.Inventory
{
    public sealed class ChestProgress
    {
        private readonly HashSet<string> opened = new HashSet<string>();
        public bool IsOpened(string chestId) => opened.Contains(chestId);
        public bool TryOpen(string chestId, ChestDefinition definition) => TryOpen(chestId, definition, 0);

        public bool TryOpen(string chestId, ChestDefinition definition, int worldSeed)
        {
            if (string.IsNullOrWhiteSpace(chestId) || definition == null) return false;
            var equipmentReward = ChestRewardSelector.SelectEquipment(worldSeed, chestId, definition);
            if (definition.EquipmentPool.Length > 0 && equipmentReward == null) return false;
            if (!opened.Add(chestId)) return false;
            foreach (var reward in definition.Rewards)
                ItemAcquisition.Request(reward.item, reward.amount);
            if (equipmentReward != null) EquipmentAcquisition.Request(equipmentReward);
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

    public static class ChestRewardSelector
    {
        public static EquipmentDefinition SelectEquipment(int worldSeed, string chestId, ChestDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(chestId) || definition == null || definition.EquipmentPool.Length == 0)
                return null;

            var hash = StableHash(worldSeed, chestId);
            hash = StableHash(unchecked((int)hash), definition.Id ?? string.Empty);
            var selected = definition.EquipmentPool[hash % (uint)definition.EquipmentPool.Length];
            return selected != null && selected.IsAccessory ? selected : null;
        }

        private static uint StableHash(int seed, string value)
        {
            unchecked
            {
                var hash = 2166136261u;
                var seedBits = (uint)seed;
                for (var shift = 0; shift < 32; shift += 8)
                {
                    hash ^= (byte)(seedBits >> shift);
                    hash *= 16777619u;
                }

                for (var i = 0; i < value.Length; i++)
                {
                    var character = value[i];
                    hash ^= (byte)character;
                    hash *= 16777619u;
                    hash ^= (byte)(character >> 8);
                    hash *= 16777619u;
                }
                return hash;
            }
        }
    }
}
