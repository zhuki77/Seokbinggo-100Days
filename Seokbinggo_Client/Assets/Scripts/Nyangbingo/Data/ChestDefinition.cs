using System;
using Nyangbingo.Core;
using UnityEngine;

namespace Nyangbingo.Data
{
    [CreateAssetMenu(menuName = "Nyangbingo/Data/Chest Reward")]
    public sealed class ChestDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private ChestRegion region;
        [Min(1)][SerializeField] private int spawnCount = 1;
        [SerializeField] private ItemAmount[] rewards = Array.Empty<ItemAmount>();
        [SerializeField] private EquipmentDefinition[] equipmentPool = Array.Empty<EquipmentDefinition>();
        [TextArea][SerializeField] private string note;
        public string Id => id;
        public ChestRegion Region => region;
        public int SpawnCount => spawnCount;
        public ItemAmount[] Rewards => rewards ?? Array.Empty<ItemAmount>();
        public EquipmentDefinition[] EquipmentPool => equipmentPool ?? Array.Empty<EquipmentDefinition>();
        public string Note => note;
        public static ChestDefinition CreateRuntime(ItemAmount[] value)
        {
            var definition = CreateInstance<ChestDefinition>();
            definition.rewards = value ?? Array.Empty<ItemAmount>();
            return definition;
        }

        public static ChestDefinition CreateRuntime(string chestId, ChestRegion chestRegion,
            EquipmentDefinition[] equipmentRewards, ItemAmount[] itemRewards = null, int count = 1,
            string description = null)
        {
            var definition = CreateInstance<ChestDefinition>();
            definition.id = chestId;
            definition.region = chestRegion;
            definition.spawnCount = count;
            definition.equipmentPool = equipmentRewards ?? Array.Empty<EquipmentDefinition>();
            definition.rewards = itemRewards ?? Array.Empty<ItemAmount>();
            definition.note = description ?? string.Empty;
            return definition;
        }
    }
}
