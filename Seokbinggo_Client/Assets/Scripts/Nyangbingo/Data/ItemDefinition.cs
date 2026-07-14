using UnityEngine;

namespace Nyangbingo.Data
{
    [CreateAssetMenu(menuName = "Nyangbingo/Data/Item")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [Min(1)][SerializeField] private int maxStack = 99;
        public string Id => id;
        public string DisplayName => displayName;
        public int MaxStack => maxStack;

        public static ItemDefinition CreateRuntime(string itemId, string name, int stack = 99)
        {
            var item = CreateInstance<ItemDefinition>();
            item.id = itemId; item.displayName = name; item.maxStack = stack;
            return item;
        }
    }
}
