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
        [SerializeField] private ItemAmount[] rewards = Array.Empty<ItemAmount>();
        [SerializeField] private EquipmentDefinition[] equipmentPool = Array.Empty<EquipmentDefinition>();
        public string Id => id;
        public ChestRegion Region => region;
        public ItemAmount[] Rewards => rewards ?? Array.Empty<ItemAmount>();
        public EquipmentDefinition[] EquipmentPool => equipmentPool ?? Array.Empty<EquipmentDefinition>();
        public static ChestDefinition CreateRuntime(ItemAmount[] value)
        {
            var definition = CreateInstance<ChestDefinition>();
            definition.rewards = value ?? Array.Empty<ItemAmount>();
            return definition;
        }

        public static ChestDefinition CreateRuntime(string chestId, ChestRegion chestRegion,
            EquipmentDefinition[] equipmentRewards, ItemAmount[] itemRewards = null)
        {
            var definition = CreateInstance<ChestDefinition>();
            definition.id = chestId;
            definition.region = chestRegion;
            definition.equipmentPool = equipmentRewards ?? Array.Empty<EquipmentDefinition>();
            definition.rewards = itemRewards ?? Array.Empty<ItemAmount>();
            return definition;
        }
    }
}
