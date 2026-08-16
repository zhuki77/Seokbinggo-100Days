using System;
using System.Collections.Generic;
using System.Linq;
using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using Nyangbingo.Save;
using Nyangbingo.World;
using UnityEditor;
using UnityEngine;

public static class NyangbingoV72BedStorageTalismanTurretRegressionTests
{
    private const string CatalogPath = "Assets/Data/SO/GameDataCatalog.asset";
    private const string ConfigPath = "Assets/Data/SO/WorldGenerationConfig.asset";

    [MenuItem("Nyangbingo/Run v72 Bed Storage Talisman Turret Regression")]
    public static void RunAll()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<GameDataCatalog>(CatalogPath);
        var config = AssetDatabase.LoadAssetAtPath<WorldGenerationConfig>(ConfigPath);
        Require(catalog != null && config != null, "catalog or world config missing");
        Require(catalog.Items.Count == 174 && catalog.Recipes.Count == 95 &&
                catalog.MineralTiers.Count == 15 && catalog.Globals.Count == 240 &&
                catalog.Talismans.Count == 5,
            "step7 catalog counts mismatch");

        ValidateBedAndDailyTick(catalog);
        ValidateStorage(catalog);
        ValidateMushrooms(catalog, config);
        ValidateTalismans(catalog);
        ValidateTurrets(catalog);
        ValidateSaveRoundTrip();

        Debug.Log("[Nyangbingo] v72 bed/storage/talisman/turret regression passed: " +
                  "bed -4C and next phase, daily spoil/melt, mushrooms 7/5/3, " +
                  "talismans 5 and durations, smithy singijeon, stage/damage caps, schema 25.");
    }

    private static void ValidateBedAndDailyTick(GameDataCatalog catalog)
    {
        Require(ReadBool(catalog, GlobalKeys.BedEnabled) &&
                ReadFloat(catalog, GlobalKeys.BedSleepRoomTempMax) == -4f &&
                catalog.FindGlobal(GlobalKeys.BedItemId)?.Value == BedService.DefaultBedItemId &&
                catalog.FindGlobal(GlobalKeys.BedSkipMode)?.Value == "next_phase",
            "bed globals mismatch");
        Require(BedService.CanSleepAtTemperature(-4f) &&
                !BedService.CanSleepAtTemperature(-4.001f) &&
                !BedService.CanSleepAtTemperature(float.NaN),
            "bed temperature boundary mismatch");

        var root = new GameObject("v72-step7-bed-clock");
        Action onNight = null;
        Action onDay = null;
        try
        {
            var clock = root.AddComponent<DayNightService>();
            Require(clock.ConfigureOfficialData(catalog) && clock.RestoreTimeState(3, 0f, false),
                "bed clock setup failed");
            var nightCount = 0;
            onNight = () => nightCount++;
            GameEvents.OnNightStart += onNight;
            Require(clock.AdvanceToNextPhase() && clock.IsNight && clock.Day == 3 && nightCount == 1,
                "day-to-night bed skip mismatch");

            var order = new List<string>();
            clock.DailyTick += () => order.Add("daily");
            clock.Dawn += () => order.Add("dawn");
            onDay = () => order.Add("day");
            GameEvents.OnDayStart += onDay;
            Require(clock.AdvanceToNextPhase() && !clock.IsNight && clock.Day == 4 &&
                    order.SequenceEqual(new[] { "daily", "dawn", "day" }),
                "night-to-dawn daily/save event order mismatch");
        }
        finally
        {
            if (onNight != null) GameEvents.OnNightStart -= onNight;
            if (onDay != null) GameEvents.OnDayStart -= onDay;
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void ValidateStorage(GameDataCatalog catalog)
    {
        Require(ReadBool(catalog, GlobalKeys.StorageTemperatureSystem) &&
                ReadFloat(catalog, GlobalKeys.StorageBandChilled) == -5f &&
                ReadFloat(catalog, GlobalKeys.StorageBandFrozen) == -10f &&
                Mathf.Approximately(ReadFloat(catalog, GlobalKeys.StorageSpoilPerDay), .1f) &&
                Mathf.Approximately(ReadFloat(catalog, GlobalKeys.StorageMeltPerDay), .25f),
            "storage globals mismatch");
        Require(Mathf.Approximately(StorageTemperatureService.ApplyFoodSpoilage(.55f, .1f), .45f) &&
                Mathf.Approximately(StorageTemperatureService.ApplyFoodSpoilage(.05f, .1f), 0f),
            "food spoilage must subtract 10 percentage points and clamp at zero");

        var amount = 1;
        var remainder = 0f;
        var melted = 0;
        for (var day = 0; day < 4; day++)
            melted += StorageTemperatureService.CalculateIceMelt(amount, remainder, .25f,
                out amount, out remainder);
        Require(melted == 1 && amount == 0 && Mathf.Approximately(remainder, 0f),
            "single ice item must disappear after four unsafe daily ticks");

        var inventory = new Inventory(catalog.FindItem, 4);
        Require(inventory.TryAddWithStorageState("oyster_mushroom", 2, true, .7f, 0f),
            "conditioned mushroom add failed");
        var exported = inventory.Export();
        var restored = new Inventory(catalog.FindItem, 4);
        Require(restored.TryImport(exported) && restored.Slots[0].hasStorageCondition &&
                Mathf.Approximately(restored.Slots[0].EffectiveStorageCondition, .7f),
            "inventory storage condition round-trip mismatch");
    }

    private static void ValidateMushrooms(GameDataCatalog catalog, WorldGenerationConfig config)
    {
        var expected = new Dictionary<string, (int minDepth, int maxDepth, int frequency)>
        {
            [WorldTileTypes.OysterMushroom] = (1, 45, 7),
            [WorldTileTypes.Shiitake] = (46, 90, 5),
            [WorldTileTypes.Seogi] = (91, 135, 3)
        };
        foreach (var pair in expected)
        {
            var item = catalog.FindItem(pair.Key);
            var mineral = catalog.FindMineralTier(pair.Key);
            var hasProfile = config.OreVeins.Any(row => row.elementType == pair.Key);
            Require(item != null && item.MaxStack == 99 && item.MvpScope == ItemMvpScope.A &&
                    mineral != null && mineral.Hardness == 1 &&
                    mineral.MinimumDepth == pair.Value.minDepth &&
                    mineral.MaximumDepth == pair.Value.maxDepth &&
                    Mathf.Approximately(mineral.FrequencyPerHundredTiles, pair.Value.frequency) &&
                    hasProfile,
                $"mushroom data/profile mismatch: {pair.Key}");
        }
        Require(TilemapRenderer.ResourceVisualFallbackId(WorldTileTypes.OysterMushroom) == WorldTileTypes.Clay &&
                TilemapRenderer.ResourceVisualFallbackId(WorldTileTypes.Shiitake) == WorldTileTypes.IceShard &&
                TilemapRenderer.ResourceVisualFallbackId(WorldTileTypes.Seogi) == WorldTileTypes.FrostEssence,
            "mushroom pre-art visual fallback mismatch");
    }

    private static void ValidateTalismans(GameDataCatalog catalog)
    {
        var expectedStations = new Dictionary<string, CraftingStation>(StringComparer.Ordinal)
        {
            [TalismanRuntime.ReturnId] = CraftingStation.Workbench,
            [TalismanRuntime.StrideId] = CraftingStation.Workbench,
            [TalismanRuntime.WaypointId] = CraftingStation.IceAnvil,
            [TalismanRuntime.HideId] = CraftingStation.Workbench,
            [TalismanRuntime.FrostId] = CraftingStation.IceAnvil
        };
        foreach (var pair in expectedStations)
        {
            var definition = catalog.FindTalisman(pair.Key);
            var item = catalog.FindItem(pair.Key);
            var recipe = catalog.FindRecipe(pair.Key);
            Require(definition != null && item != null && recipe != null &&
                    recipe.Station == pair.Value && recipe.Output.item == item &&
                    recipe.Output.amount == 1 && recipe.DurationSeconds == 0f &&
                    recipe.Ingredients.Length == definition.Materials.Count,
                $"talisman generated item/recipe mismatch: {pair.Key}");
            foreach (var material in definition.Materials)
                Require(recipe.Ingredients.Any(input => input.item != null &&
                        input.item.Id == material.itemId && input.amount == material.amount),
                    $"talisman material mismatch: {pair.Key}/{material.itemId}");
        }

        var root = new GameObject("v72-step7-talisman-runtime");
        try
        {
            var environment = root.AddComponent<MainGameEnvironmentState>();
            var inventory = new Inventory(catalog.FindItem, 8);
            var runtime = new TalismanRuntime(catalog, inventory, environment);
            runtime.BindPlayer(root.transform);
            Require(inventory.TryAdd(TalismanRuntime.StrideId, 1) &&
                    runtime.TryUse(TalismanRuntime.StrideId, out _) &&
                    Mathf.Approximately(runtime.MovementMultiplier, 1.5f) &&
                    inventory.Count(TalismanRuntime.StrideId) == 0,
                "stride talisman consumption/multiplier mismatch");
            Require(runtime.Restore(60f, 30f, 120f) && runtime.IgnoresYokaiAggro &&
                    runtime.SuppressesHypothermia,
                "talisman duration restore mismatch");
            runtime.Tick(30f);
            Require(Mathf.Approximately(runtime.StrideRemaining, 30f) &&
                    Mathf.Approximately(runtime.HideRemaining, 0f) &&
                    Mathf.Approximately(runtime.FrostRemaining, 90f),
                "talisman central game-seconds tick mismatch");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void ValidateTurrets(GameDataCatalog catalog)
    {
        Require(catalog.FindGlobal(GlobalKeys.TurretSlotCapSource)?.Value == "seokbinggo_stage" &&
                catalog.FindGlobal(GlobalKeys.TurretTierEarly)?.Value == SeokbinggoRules.EarlyTurretId &&
                catalog.FindGlobal(GlobalKeys.TurretTierLate)?.Value ==
                    "singijeon_cart|seonge_tower|cold_wave_tower" &&
                ReadBool(catalog, GlobalKeys.TurretMidgameGap),
            "turret progression globals mismatch");
        Require(Enumerable.Range(0, 7).All(stage => SeokbinggoRules.TurretSlotCap(stage) == stage) &&
                SeokbinggoRules.CanPlaceTurret(3, 2, true, 2, 3) &&
                !SeokbinggoRules.CanPlaceTurret(3, 3, false, 2, 3) &&
                !SeokbinggoRules.CanPlaceTurret(6, 2, true, 3, 3),
            "seokbinggo-stage turret slot/damage cap mismatch");
        Require(SeokbinggoRules.IsDamageTurret(SeokbinggoRules.EarlyTurretId) &&
                SeokbinggoRules.IsDamageTurret(SeokbinggoRules.SingijeonTurretId) &&
                SeokbinggoRules.IsDamageTurret(SeokbinggoRules.ColdWaveTurretId) &&
                SeokbinggoRules.IsUtilityTurret(SeokbinggoRules.SeongeTurretId),
            "turret role classification mismatch");
        Require(catalog.FindRecipe(SeokbinggoRules.EarlyTurretId)?.Station == CraftingStation.Workbench &&
                catalog.FindRecipe(SeokbinggoRules.SingijeonTurretId)?.Station == CraftingStation.Smithy,
            "early/late turret crafting station mismatch");
    }

    private static void ValidateSaveRoundTrip()
    {
        var save = new SaveGame
        {
            inventory = new List<InventorySlot>
            {
                new InventorySlot
                {
                    itemId = "shiitake", amount = 3, hasStorageCondition = true,
                    storageCondition01 = .4f, storageMeltRemainder = .75f
                }
            },
            talismanStrideRemaining = 45f,
            talismanHideRemaining = 20f,
            talismanFrostRemaining = 90f
        };
        var restored = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(save));
        restored.NormalizeAfterLoad();
        Require(restored.schemaVersion == SaveGame.CurrentSchemaVersion &&
                SaveGame.CurrentSchemaVersion == 25 && restored.inventory.Count == 1 &&
                restored.inventory[0].hasStorageCondition &&
                Mathf.Approximately(restored.inventory[0].storageCondition01, .4f) &&
                Mathf.Approximately(restored.inventory[0].storageMeltRemainder, .75f) &&
                Mathf.Approximately(restored.talismanStrideRemaining, 45f) &&
                Mathf.Approximately(restored.talismanHideRemaining, 20f) &&
                Mathf.Approximately(restored.talismanFrostRemaining, 90f),
            "schema 25 storage/talisman save round-trip mismatch");
    }

    private static bool ReadBool(GameDataCatalog catalog, string key)
    {
        var definition = catalog.FindGlobal(key);
        if (definition == null || !definition.TryGetBool(out var value))
            throw new InvalidOperationException($"missing boolean global: {key}");
        return value;
    }

    private static float ReadFloat(GameDataCatalog catalog, string key)
    {
        var definition = catalog.FindGlobal(key);
        if (definition == null || !definition.TryGetFloat(out var value))
            throw new InvalidOperationException($"missing float global: {key}");
        return value;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
