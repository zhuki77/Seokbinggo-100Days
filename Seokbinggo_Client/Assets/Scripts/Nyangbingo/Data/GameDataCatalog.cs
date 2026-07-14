using System.Collections.Generic;
using UnityEngine;

namespace Nyangbingo.Data
{
    [CreateAssetMenu(menuName = "Nyangbingo/Data/Game Data Catalog")]
    public sealed class GameDataCatalog : ScriptableObject
    {
        [SerializeField] private ItemDefinition[] items;
        [SerializeField] private RecipeDefinition[] recipes;
        [SerializeField] private YokaiDefinition[] yokai;
        private Dictionary<string, ItemDefinition> itemsById;
        public IReadOnlyList<RecipeDefinition> Recipes => recipes;

        public ItemDefinition FindItem(string id)
        {
            EnsureIndex();
            return !string.IsNullOrEmpty(id) && itemsById.TryGetValue(id, out var item) ? item : null;
        }

        private void OnEnable() => itemsById = null;
        private void EnsureIndex()
        {
            if (itemsById != null) return;
            itemsById = new Dictionary<string, ItemDefinition>();
            foreach (var item in items)
                if (item != null && !string.IsNullOrWhiteSpace(item.Id)) itemsById[item.Id] = item;
        }
    }
}
