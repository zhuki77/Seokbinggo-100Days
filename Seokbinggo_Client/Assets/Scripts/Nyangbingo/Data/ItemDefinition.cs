using UnityEngine;

namespace Nyangbingo.Data
{
    public enum ItemCategory
    {
        Unspecified,
        Resource,
        Material,
        Tool,
        Weapon,
        Equipment,
        Station,
        Placeable,
        Summon
    }

    public enum ItemMvpScope { Unspecified, A, B }

    public static class FanItemIds
    {
        public const string LegacyFoldingFan = "folding_fan";
        public const string Hapjukseon = "hapjukseon";
        public const string Cheolseon = "cheolseon";
        public const string Seolpungseon = "seolpungseon";
    }

    public static class BellRopeItemIds
    {
        public const string FrostBellRope = "frost_bell_rope";
    }

    [CreateAssetMenu(menuName = "Nyangbingo/Data/Item")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [Min(0)][SerializeField] private int maxStack = 99;
        [SerializeField] private ItemCategory category;
        [SerializeField] private ItemMvpScope mvpScope;
        [TextArea][SerializeField] private string note;

        public string Id => id;
        public string DisplayName => displayName;
        public int MaxStack => maxStack;
        public ItemCategory Category => category;
        public ItemMvpScope MvpScope => mvpScope;
        public string Note => note;
        public bool IsInventoryItem => maxStack > 0;

        public static ItemDefinition CreateRuntime(string itemId, string name, int stack = 99,
            ItemCategory itemCategory = ItemCategory.Unspecified,
            ItemMvpScope scope = ItemMvpScope.Unspecified, string description = null)
        {
            var item = CreateInstance<ItemDefinition>();
            item.id = itemId;
            item.displayName = name;
            item.maxStack = stack;
            item.category = itemCategory;
            item.mvpScope = scope;
            item.note = description ?? string.Empty;
            return item;
        }
    }
}
