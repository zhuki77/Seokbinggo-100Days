using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nyangbingo.Data
{
    [CreateAssetMenu(menuName = "Nyangbingo/Data/Game Data Catalog")]
    public sealed class GameDataCatalog : ScriptableObject
    {
        [SerializeField] private ItemDefinition[] items = Array.Empty<ItemDefinition>();
        [SerializeField] private RecipeDefinition[] recipes = Array.Empty<RecipeDefinition>();
        [SerializeField] private SmeltingDefinition[] smelting = Array.Empty<SmeltingDefinition>();
        [SerializeField] private EquipmentDefinition[] equipment = Array.Empty<EquipmentDefinition>();
        [SerializeField] private UtilityDefinition[] utilities = Array.Empty<UtilityDefinition>();
        [SerializeField] private YokaiDefinition[] yokai = Array.Empty<YokaiDefinition>();
        [SerializeField] private BossDefinition[] bosses = Array.Empty<BossDefinition>();
        [SerializeField] private ChestDefinition[] chests = Array.Empty<ChestDefinition>();
        [SerializeField] private DayEventDefinition[] dayEvents = Array.Empty<DayEventDefinition>();

        private Dictionary<string, ItemDefinition> itemsById;
        private Dictionary<string, RecipeDefinition> recipesById;
        private Dictionary<string, SmeltingDefinition> smeltingById;
        private Dictionary<string, EquipmentDefinition> equipmentById;
        private Dictionary<string, UtilityDefinition> utilitiesById;
        private Dictionary<string, YokaiDefinition> yokaiById;
        private Dictionary<string, BossDefinition> bossesById;
        private Dictionary<string, ChestDefinition> chestsById;
        private Dictionary<string, DayEventDefinition> dayEventsById;
        private bool indexesValid;

        public IReadOnlyList<ItemDefinition> Items => items ?? Array.Empty<ItemDefinition>();
        public IReadOnlyList<RecipeDefinition> Recipes => recipes ?? Array.Empty<RecipeDefinition>();
        public IReadOnlyList<SmeltingDefinition> Smelting => smelting ?? Array.Empty<SmeltingDefinition>();
        public IReadOnlyList<EquipmentDefinition> Equipment => equipment ?? Array.Empty<EquipmentDefinition>();
        public IReadOnlyList<UtilityDefinition> Utilities => utilities ?? Array.Empty<UtilityDefinition>();
        public IReadOnlyList<YokaiDefinition> Yokai => yokai ?? Array.Empty<YokaiDefinition>();
        public IReadOnlyList<BossDefinition> Bosses => bosses ?? Array.Empty<BossDefinition>();
        public IReadOnlyList<ChestDefinition> Chests => chests ?? Array.Empty<ChestDefinition>();
        public IReadOnlyList<DayEventDefinition> DayEvents => dayEvents ?? Array.Empty<DayEventDefinition>();
        public bool IsValid { get { EnsureIndex(); return indexesValid; } }

        public ItemDefinition FindItem(string id)
        {
            EnsureIndex();
            return indexesValid && !string.IsNullOrEmpty(id) && itemsById.TryGetValue(id, out var item) ? item : null;
        }

        public RecipeDefinition FindRecipe(string id)
        {
            EnsureIndex();
            return indexesValid && !string.IsNullOrEmpty(id) && recipesById.TryGetValue(id, out var recipe) ? recipe : null;
        }

        public SmeltingDefinition FindSmelting(string id)
        {
            EnsureIndex();
            return indexesValid && !string.IsNullOrEmpty(id) && smeltingById.TryGetValue(id, out var definition) ? definition : null;
        }

        public EquipmentDefinition FindEquipment(string id)
        {
            EnsureIndex();
            return indexesValid && !string.IsNullOrEmpty(id) && equipmentById.TryGetValue(id, out var definition) ? definition : null;
        }

        public UtilityDefinition FindUtility(string id)
        {
            EnsureIndex();
            return indexesValid && !string.IsNullOrEmpty(id) && utilitiesById.TryGetValue(id, out var definition) ? definition : null;
        }

        public YokaiDefinition FindYokai(string id)
        {
            EnsureIndex();
            return indexesValid && !string.IsNullOrEmpty(id) && yokaiById.TryGetValue(id, out var definition) ? definition : null;
        }

        public BossDefinition FindBoss(string id)
        {
            EnsureIndex();
            return indexesValid && !string.IsNullOrEmpty(id) && bossesById.TryGetValue(id, out var definition) ? definition : null;
        }

        public ChestDefinition FindChest(string id)
        {
            EnsureIndex();
            return indexesValid && !string.IsNullOrEmpty(id) && chestsById.TryGetValue(id, out var definition) ? definition : null;
        }

        public DayEventDefinition FindDayEvent(string id)
        {
            EnsureIndex();
            return indexesValid && !string.IsNullOrEmpty(id) && dayEventsById.TryGetValue(id, out var definition) ? definition : null;
        }

        public static GameDataCatalog CreateRuntime(params ItemDefinition[] runtimeItems)
        {
            var catalog = CreateInstance<GameDataCatalog>();
            catalog.items = runtimeItems ?? Array.Empty<ItemDefinition>();
            catalog.ClearIndexes();
            return catalog;
        }

        private void OnEnable() => ClearIndexes();
        private void OnValidate() => ClearIndexes();

        private void ClearIndexes()
        {
            itemsById = null;
            recipesById = null;
            smeltingById = null;
            equipmentById = null;
            utilitiesById = null;
            yokaiById = null;
            bossesById = null;
            chestsById = null;
            dayEventsById = null;
            indexesValid = false;
        }

        private void EnsureIndex()
        {
            if (itemsById != null) return;
            indexesValid = true;
            itemsById = BuildIndex(items, value => value.Id, out var itemsValid); indexesValid &= itemsValid;
            recipesById = BuildIndex(recipes, value => value.Id, out var recipesValid); indexesValid &= recipesValid;
            smeltingById = BuildIndex(smelting, value => value.Id, out var smeltingValid); indexesValid &= smeltingValid;
            equipmentById = BuildIndex(equipment, value => value.Id, out var equipmentValid); indexesValid &= equipmentValid;
            utilitiesById = BuildIndex(utilities, value => value.Id, out var utilitiesValid); indexesValid &= utilitiesValid;
            yokaiById = BuildIndex(yokai, value => value.Id, out var yokaiValid); indexesValid &= yokaiValid;
            bossesById = BuildIndex(bosses, value => value.Id, out var bossesValid); indexesValid &= bossesValid;
            chestsById = BuildIndex(chests, value => value.Id, out var chestsValid); indexesValid &= chestsValid;
            dayEventsById = BuildIndex(dayEvents, value => value.Id, out var dayEventsValid); indexesValid &= dayEventsValid;
        }

        private static Dictionary<string, T> BuildIndex<T>(IEnumerable<T> values, Func<T, string> getId,
            out bool valid)
            where T : UnityEngine.Object
        {
            var index = new Dictionary<string, T>(StringComparer.Ordinal);
            valid = true;
            if (values == null) return index;

            foreach (var value in values)
            {
                if (value == null)
                {
                    valid = false;
                    continue;
                }
                var id = getId(value);
                if (string.IsNullOrWhiteSpace(id) || !index.TryAdd(id, value)) valid = false;
            }

            return index;
        }
    }
}
