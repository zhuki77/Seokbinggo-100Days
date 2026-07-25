using System;
using System.Collections.Generic;
using Nyangbingo.Core;
using Nyangbingo.Data;

namespace Nyangbingo.Inventory
{
    public sealed class ChestProgress
    {
        public const int StorageSlotCount = 40;

        private readonly HashSet<string> opened = new HashSet<string>();
        private readonly Dictionary<string, Inventory> contents =
            new Dictionary<string, Inventory>(StringComparer.Ordinal);
        private readonly Func<string, ItemDefinition> findItem;

        public ChestProgress()
        {
        }

        public ChestProgress(Func<string, ItemDefinition> findItem)
        {
            this.findItem = findItem ?? throw new ArgumentNullException(nameof(findItem));
        }

        public bool IsOpened(string chestId) => opened.Contains(chestId);
        public bool TryGetContents(string chestId, out Inventory storage)
        {
            storage = null;
            return !string.IsNullOrWhiteSpace(chestId) &&
                   contents.TryGetValue(chestId, out storage);
        }

        public bool TryOpen(string chestId, ChestDefinition definition) => TryOpen(chestId, definition, 0);

        public bool TryOpen(string chestId, ChestDefinition definition, int worldSeed)
        {
            if (string.IsNullOrWhiteSpace(chestId) || definition == null || findItem == null ||
                opened.Contains(chestId)) return false;
            var equipmentReward = ChestRewardSelector.SelectEquipment(worldSeed, chestId, definition);
            if (definition.EquipmentPool.Length > 0 && equipmentReward == null) return false;

            var storage = new Inventory(findItem, StorageSlotCount);
            foreach (var reward in definition.Rewards)
                if (reward.item == null || findItem(reward.item.Id) != reward.item ||
                    !storage.TryAdd(reward.item.Id, reward.amount))
                    return false;
            if (equipmentReward != null &&
                (findItem(equipmentReward.Id) == null || !storage.TryAdd(equipmentReward.Id, 1)))
                return false;

            opened.Add(chestId);
            contents.Add(chestId, storage);
            GameEvents.RaiseChestOpened();
            return true;
        }

        public List<InventorySlot> ExportContents(string chestId) =>
            contents.TryGetValue(chestId, out var storage)
                ? storage.Export()
                : new List<InventorySlot>();

        public List<string> Export() => new List<string>(opened);

        public void Import(IEnumerable<string> ids)
        {
            opened.Clear();
            contents.Clear();
            if (ids == null) return;
            foreach (var id in ids)
            {
                if (string.IsNullOrWhiteSpace(id) || !opened.Add(id) || findItem == null) continue;
                contents.Add(id, new Inventory(findItem, StorageSlotCount));
            }
        }

        public bool TryImport(IEnumerable<string> ids,
            IReadOnlyDictionary<string, List<InventorySlot>> savedContents)
        {
            if (ids == null || savedContents == null || findItem == null) return false;
            var restoredOpened = new HashSet<string>(StringComparer.Ordinal);
            var restoredContents = new Dictionary<string, Inventory>(StringComparer.Ordinal);
            foreach (var id in ids)
            {
                if (string.IsNullOrWhiteSpace(id) || !restoredOpened.Add(id) ||
                    !savedContents.TryGetValue(id, out var slots))
                    return false;
                var storage = new Inventory(findItem, StorageSlotCount);
                if (!storage.TryImport(slots)) return false;
                restoredContents.Add(id, storage);
            }
            if (savedContents.Count != restoredOpened.Count) return false;

            opened.Clear();
            contents.Clear();
            foreach (var id in restoredOpened) opened.Add(id);
            foreach (var pair in restoredContents) contents.Add(pair.Key, pair.Value);
            return true;
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
