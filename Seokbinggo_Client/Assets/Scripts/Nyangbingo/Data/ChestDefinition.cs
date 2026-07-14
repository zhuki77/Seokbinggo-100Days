using UnityEngine;

namespace Nyangbingo.Data
{
    [CreateAssetMenu(menuName = "Nyangbingo/Data/Chest Reward")]
    public sealed class ChestDefinition : ScriptableObject
    {
        [SerializeField] private ItemAmount[] rewards;
        public ItemAmount[] Rewards => rewards;
        public static ChestDefinition CreateRuntime(ItemAmount[] value)
        {
            var definition = CreateInstance<ChestDefinition>(); definition.rewards = value; return definition;
        }
    }
}
