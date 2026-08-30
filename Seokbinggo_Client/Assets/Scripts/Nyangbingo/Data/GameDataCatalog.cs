using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Nyangbingo.Data
{
    [CreateAssetMenu(menuName = "Nyangbingo/Data/Game Data Catalog")]
    public sealed class GameDataCatalog : ScriptableObject
    {
        [SerializeField] private ItemDefinition[] items = Array.Empty<ItemDefinition>();
        [SerializeField] private RecipeDefinition[] recipes = Array.Empty<RecipeDefinition>();
        [SerializeField] private ModuleDefinition[] modules = Array.Empty<ModuleDefinition>();
        [SerializeField] private MineralTierDefinition[] mineralTiers = Array.Empty<MineralTierDefinition>();
        [SerializeField] private SealWhitelistDefinition[] sealWhitelist = Array.Empty<SealWhitelistDefinition>();
        [SerializeField] private IdMigrationDefinition[] idMigrations = Array.Empty<IdMigrationDefinition>();
        [SerializeField] private DayCurveDefinition[] dayCurves = Array.Empty<DayCurveDefinition>();
        [SerializeField] private DayCurveDefinition[] dayCurveExtensions = Array.Empty<DayCurveDefinition>();
        [SerializeField] private GlobalDefinition[] globals = Array.Empty<GlobalDefinition>();
        [SerializeField] private SmeltingDefinition[] smelting = Array.Empty<SmeltingDefinition>();
        [SerializeField] private EquipmentDefinition[] equipment = Array.Empty<EquipmentDefinition>();
        [SerializeField] private UtilityDefinition[] utilities = Array.Empty<UtilityDefinition>();
        [SerializeField] private CombatProfileDefinition[] combatProfiles = Array.Empty<CombatProfileDefinition>();
        [SerializeField] private YokaiDefinition[] yokai = Array.Empty<YokaiDefinition>();
        [SerializeField] private BossDefinition[] bosses = Array.Empty<BossDefinition>();
        [SerializeField] private ChestDefinition[] chests = Array.Empty<ChestDefinition>();
        [SerializeField] private DayEventDefinition[] dayEvents = Array.Empty<DayEventDefinition>();
        [SerializeField] private ZoneDefinition[] zones = Array.Empty<ZoneDefinition>();
        [SerializeField] private TerrainSpawnDefinition[] terrainSpawns = Array.Empty<TerrainSpawnDefinition>();
        [SerializeField] private TalismanDefinition[] talismans = Array.Empty<TalismanDefinition>();
        [SerializeField] private CodexEntryDefinition[] codexEntries = Array.Empty<CodexEntryDefinition>();
        [SerializeField] private TraitDefinition[] traits = Array.Empty<TraitDefinition>();
        [SerializeField] private CropDefinition[] crops = Array.Empty<CropDefinition>();

        private Dictionary<string, ItemDefinition> itemsById;
        private Dictionary<string, RecipeDefinition> recipesById;
        private Dictionary<string, ModuleDefinition> modulesById;
        private Dictionary<string, MineralTierDefinition> mineralTiersById;
        private Dictionary<string, SealWhitelistDefinition> sealWhitelistByElement;
        private Dictionary<string, IdMigrationDefinition> idMigrationsByKey;
        private Dictionary<string, DayCurveDefinition> dayCurvesByDay;
        private List<DayCurveDefinition> sortedDayCurveExtensions;
        private Dictionary<string, GlobalDefinition> globalsByKey;
        private Dictionary<string, SmeltingDefinition> smeltingById;
        private Dictionary<string, EquipmentDefinition> equipmentById;
        private Dictionary<string, UtilityDefinition> utilitiesById;
        private Dictionary<string, CombatProfileDefinition> combatProfilesById;
        private Dictionary<string, YokaiDefinition> yokaiById;
        private Dictionary<string, BossDefinition> bossesById;
        private Dictionary<string, ChestDefinition> chestsById;
        private Dictionary<string, DayEventDefinition> dayEventsById;
        private Dictionary<string, ZoneDefinition> zonesById;
        private Dictionary<string, TerrainSpawnDefinition> terrainSpawnsById;
        private Dictionary<string, TalismanDefinition> talismansById;
        private Dictionary<string, CodexEntryDefinition> codexEntriesById;
        private Dictionary<string, TraitDefinition> traitsById;
        private Dictionary<string, CropDefinition> cropsById;
        private bool indexesValid;

        public IReadOnlyList<ItemDefinition> Items => items ?? Array.Empty<ItemDefinition>();
        public IReadOnlyList<RecipeDefinition> Recipes => recipes ?? Array.Empty<RecipeDefinition>();
        public IReadOnlyList<ModuleDefinition> Modules => modules ?? Array.Empty<ModuleDefinition>();
        public IReadOnlyList<MineralTierDefinition> MineralTiers => mineralTiers ?? Array.Empty<MineralTierDefinition>();
        public IReadOnlyList<SealWhitelistDefinition> SealWhitelist =>
            sealWhitelist ?? Array.Empty<SealWhitelistDefinition>();
        public IReadOnlyList<IdMigrationDefinition> IdMigrations =>
            idMigrations ?? Array.Empty<IdMigrationDefinition>();
        public IReadOnlyList<DayCurveDefinition> DayCurves => dayCurves ?? Array.Empty<DayCurveDefinition>();
        public IReadOnlyList<DayCurveDefinition> DayCurveExtensions =>
            dayCurveExtensions ?? Array.Empty<DayCurveDefinition>();
        public IReadOnlyList<GlobalDefinition> Globals => globals ?? Array.Empty<GlobalDefinition>();
        public IReadOnlyList<SmeltingDefinition> Smelting => smelting ?? Array.Empty<SmeltingDefinition>();
        public IReadOnlyList<EquipmentDefinition> Equipment => equipment ?? Array.Empty<EquipmentDefinition>();
        public IReadOnlyList<UtilityDefinition> Utilities => utilities ?? Array.Empty<UtilityDefinition>();
        public IReadOnlyList<CombatProfileDefinition> CombatProfiles => combatProfiles ?? Array.Empty<CombatProfileDefinition>();
        public IReadOnlyList<YokaiDefinition> Yokai => yokai ?? Array.Empty<YokaiDefinition>();
        public IReadOnlyList<BossDefinition> Bosses => bosses ?? Array.Empty<BossDefinition>();
        public IReadOnlyList<ChestDefinition> Chests => chests ?? Array.Empty<ChestDefinition>();
        public IReadOnlyList<DayEventDefinition> DayEvents => dayEvents ?? Array.Empty<DayEventDefinition>();
        public IReadOnlyList<ZoneDefinition> Zones => zones ?? Array.Empty<ZoneDefinition>();
        public IReadOnlyList<TerrainSpawnDefinition> TerrainSpawns =>
            terrainSpawns ?? Array.Empty<TerrainSpawnDefinition>();
        public IReadOnlyList<TalismanDefinition> Talismans => talismans ?? Array.Empty<TalismanDefinition>();
        public IReadOnlyList<CodexEntryDefinition> CodexEntries =>
            codexEntries ?? Array.Empty<CodexEntryDefinition>();
        public IReadOnlyList<TraitDefinition> Traits => traits ?? Array.Empty<TraitDefinition>();
        public IReadOnlyList<CropDefinition> Crops => crops ?? Array.Empty<CropDefinition>();
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

        public ModuleDefinition FindModule(string id)
        {
            EnsureIndex();
            return indexesValid && !string.IsNullOrEmpty(id) && modulesById.TryGetValue(id, out var definition)
                ? definition : null;
        }

        public MineralTierDefinition FindMineralTier(string id)
        {
            EnsureIndex();
            return indexesValid && !string.IsNullOrEmpty(id) &&
                   mineralTiersById.TryGetValue(id, out var definition) ? definition : null;
        }

        public SealWhitelistDefinition FindSealRule(string element)
        {
            EnsureIndex();
            return indexesValid && !string.IsNullOrEmpty(element) &&
                   sealWhitelistByElement.TryGetValue(element, out var definition) ? definition : null;
        }

        public IdMigrationDefinition FindIdMigration(IdMigrationDomain domain, string legacyId)
        {
            EnsureIndex();
            var key = $"{domain}:{legacyId}";
            return indexesValid && !string.IsNullOrEmpty(legacyId) &&
                   idMigrationsByKey.TryGetValue(key, out var definition) ? definition : null;
        }

        public DayCurveDefinition FindDayCurve(int day)
        {
            EnsureIndex();
            var key = day.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (indexesValid && day > 0 && day <= 30 && dayCurvesByDay.TryGetValue(key, out var definition))
                return definition;
            return DayCurveExtensionResolver.Resolve(this, day);
        }

        internal DayCurveDefinition FindDayCurveExtensionAnchor(int day)
        {
            EnsureIndex();
            if (!indexesValid || day <= 30 || sortedDayCurveExtensions == null ||
                sortedDayCurveExtensions.Count == 0)
                return null;

            DayCurveDefinition anchor = null;
            for (var index = 0; index < sortedDayCurveExtensions.Count; index++)
            {
                var candidate = sortedDayCurveExtensions[index];
                if (candidate == null || candidate.Day > day) break;
                anchor = candidate;
            }
            return anchor;
        }

        public GlobalDefinition FindGlobal(string key)
        {
            EnsureIndex();
            return indexesValid && !string.IsNullOrEmpty(key) &&
                   globalsByKey.TryGetValue(key, out var definition) ? definition : null;
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

        public CombatProfileDefinition FindCombatProfile(string id)
        {
            EnsureIndex();
            return indexesValid && !string.IsNullOrEmpty(id) && combatProfilesById.TryGetValue(id, out var definition)
                ? definition : null;
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

        public ZoneDefinition FindZone(string id)
        {
            EnsureIndex();
            return indexesValid && !string.IsNullOrEmpty(id) && zonesById.TryGetValue(id, out var value)
                ? value : null;
        }

        public TerrainSpawnDefinition FindTerrainSpawn(string id)
        {
            EnsureIndex();
            return indexesValid && !string.IsNullOrEmpty(id) && terrainSpawnsById.TryGetValue(id, out var value)
                ? value : null;
        }

        public TalismanDefinition FindTalisman(string id)
        {
            EnsureIndex();
            return indexesValid && !string.IsNullOrEmpty(id) && talismansById.TryGetValue(id, out var value)
                ? value : null;
        }

        public CodexEntryDefinition FindCodexEntry(string id)
        {
            EnsureIndex();
            return indexesValid && !string.IsNullOrEmpty(id) && codexEntriesById.TryGetValue(id, out var value)
                ? value : null;
        }

        public TraitDefinition FindTrait(string id)
        {
            EnsureIndex();
            return indexesValid && !string.IsNullOrEmpty(id) && traitsById.TryGetValue(id, out var value)
                ? value : null;
        }

        public CropDefinition FindCrop(string id)
        {
            EnsureIndex();
            return indexesValid && !string.IsNullOrEmpty(id) && cropsById.TryGetValue(id, out var value)
                ? value : null;
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
            modulesById = null;
            mineralTiersById = null;
            sealWhitelistByElement = null;
            idMigrationsByKey = null;
            dayCurvesByDay = null;
            sortedDayCurveExtensions = null;
            globalsByKey = null;
            smeltingById = null;
            equipmentById = null;
            utilitiesById = null;
            combatProfilesById = null;
            yokaiById = null;
            bossesById = null;
            chestsById = null;
            dayEventsById = null;
            zonesById = null;
            terrainSpawnsById = null;
            talismansById = null;
            codexEntriesById = null;
            traitsById = null;
            cropsById = null;
            indexesValid = false;
        }

        private void EnsureIndex()
        {
            if (itemsById != null) return;
            indexesValid = true;
            itemsById = BuildIndex(items, value => value.Id, out var itemsValid); indexesValid &= itemsValid;
            recipesById = BuildIndex(recipes, value => value.Id, out var recipesValid); indexesValid &= recipesValid;
            modulesById = BuildIndex(modules, value => value.Id, out var modulesValid); indexesValid &= modulesValid;
            mineralTiersById = BuildIndex(mineralTiers, value => value.Id, out var mineralTiersValid);
            indexesValid &= mineralTiersValid;
            sealWhitelistByElement = BuildIndex(sealWhitelist, value => value.Element, out var sealWhitelistValid);
            indexesValid &= sealWhitelistValid;
            idMigrationsByKey = BuildIndex(idMigrations, value => value.Key, out var idMigrationsValid);
            indexesValid &= idMigrationsValid;
            dayCurvesByDay = BuildIndex(dayCurves, value => value.Id, out var dayCurvesValid);
            indexesValid &= dayCurvesValid;
            sortedDayCurveExtensions = (dayCurveExtensions ?? Array.Empty<DayCurveDefinition>())
                .Where(value => value != null)
                .OrderBy(value => value.Day)
                .ToList();
            globalsByKey = BuildIndex(globals, value => value.Key, out var globalsValid);
            indexesValid &= globalsValid;
            smeltingById = BuildIndex(smelting, value => value.Id, out var smeltingValid); indexesValid &= smeltingValid;
            equipmentById = BuildIndex(equipment, value => value.Id, out var equipmentValid); indexesValid &= equipmentValid;
            utilitiesById = BuildIndex(utilities, value => value.Id, out var utilitiesValid); indexesValid &= utilitiesValid;
            combatProfilesById = BuildIndex(combatProfiles, value => value.Id, out var combatProfilesValid);
            indexesValid &= combatProfilesValid;
            yokaiById = BuildIndex(yokai, value => value.Id, out var yokaiValid); indexesValid &= yokaiValid;
            bossesById = BuildIndex(bosses, value => value.Id, out var bossesValid); indexesValid &= bossesValid;
            chestsById = BuildIndex(chests, value => value.Id, out var chestsValid); indexesValid &= chestsValid;
            dayEventsById = BuildIndex(dayEvents, value => value.Id, out var dayEventsValid); indexesValid &= dayEventsValid;
            zonesById = BuildIndex(zones, value => value.Id, out var zonesValid); indexesValid &= zonesValid;
            terrainSpawnsById = BuildIndex(terrainSpawns, value => value.Id, out var terrainSpawnsValid);
            indexesValid &= terrainSpawnsValid;
            talismansById = BuildIndex(talismans, value => value.Id, out var talismansValid);
            indexesValid &= talismansValid;
            codexEntriesById = BuildIndex(codexEntries, value => value.Id, out var codexEntriesValid);
            indexesValid &= codexEntriesValid;
            traitsById = BuildIndex(traits, value => value.Id, out var traitsValid); indexesValid &= traitsValid;
            cropsById = BuildIndex(crops, value => value.Id, out var cropsValid); indexesValid &= cropsValid;
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
