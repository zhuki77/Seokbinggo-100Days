using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using Nyangbingo.Save;
using Nyangbingo.UI;
using Nyangbingo.World;
using Nyangbingo.Yokai;
using UnityEditor;
using UnityEngine;

public static class NyangbingoV72WorldInvasionRegressionTests
{
    private const string CatalogPath = "Assets/Data/SO/GameDataCatalog.asset";
    private const string ConfigPath = "Assets/Data/SO/WorldGenerationConfig.asset";

    [MenuItem("Nyangbingo/Run v72 World Invasion Regression")]
    public static void RunAll()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<GameDataCatalog>(CatalogPath);
        var config = AssetDatabase.LoadAssetAtPath<WorldGenerationConfig>(ConfigPath);
        Require(catalog != null && config != null, "catalog or world config missing");

        Require(ReadInt(catalog, GlobalKeys.MapWidthDefault) == 600 &&
                ReadInt(catalog, GlobalKeys.WorldHalfWidth) == 300 &&
                ReadInt(catalog, GlobalKeys.SpawnX) == 300 &&
                ReadInt(catalog, GlobalKeys.UndergroundDepthMinTiles) == 135,
            "world 600/300/135 globals mismatch");
        Require(config.MapWidth == 600 &&
                WorldGenerationConfig.UndergroundDepthMinTiles == 135 &&
                config.LargeCavernCountMin == 5 && config.LargeCavernCountMax == 8 &&
                config.LargeCavernWidth == 30 && config.LargeCavernHeight == 20 &&
                config.LargeCavernMinEdgeGap == 40 &&
                config.LargeCavernMinDepthBelowCrust == 8 &&
                config.LargeCavernMarginFromSpawn == 32 &&
                config.LargeCavernMarginFromAltar == 28,
            "world config does not match promoted cavern measurements");
        Require(Mathf.Approximately(
                WorldV72Rules.BandRoundtripSeconds(600f, 10, 3f), 20f) &&
                Mathf.Approximately(
                    WorldV72Rules.BandRoundtripSeconds(600f, 10, 3f, 10), 200f),
            "roundtrip distance formula mismatch");

        var generator = new MapGenerator(config, catalog);
        Require(generator.MineralProfiles.Count == config.OreVeins.Length,
            "runtime mineral profile count mismatch");
        foreach (var profile in generator.MineralProfiles)
        {
            var source = catalog.FindMineralTier(profile.elementType);
            Require(source != null &&
                    Mathf.Approximately(profile.frequencyPer100Tiles,
                        source.FrequencyPerHundredTiles) &&
                    profile.depthMin == source.MinimumDepth &&
                    profile.depthMax == source.MaximumDepth &&
                    profile.minClusterSize == 3 && profile.maxClusterSize == 6,
                $"ore profile is not CSV-backed: {profile.elementType}");
        }

        Require(catalog.TerrainSpawns.Count == 70, "terrain-spawn must contain 70 rows");
        var terrainGroups = catalog.TerrainSpawns.GroupBy(row => row.TerrainId).ToArray();
        Require(terrainGroups.Length == 10, "terrain-spawn must contain 10 terrains");
        Require(terrainGroups.Count(group => group.Any(row => row.Implemented)) == 5,
            "implemented terrain count must be 5");
        foreach (var group in terrainGroups)
        {
            Require(group.Count() == 7, $"terrain {group.Key} must contain 7 yokai rows");
            var implemented = group.Any(row => row.Implemented);
            Require(group.Sum(row => row.Weight) == (implemented ? 100 : 0),
                $"terrain weight sum mismatch: {group.Key}");
        }

        var expectedAggro = new Dictionary<string, float>(StringComparer.Ordinal)
        {
            ["yakwang"] = 5f,
            ["eoduksini"] = 7f,
            ["club"] = 8f,
            ["gaekgwi"] = 8f,
            ["gangcheol"] = 8f,
            ["bulgasari"] = 9f
        };
        foreach (var pair in expectedAggro)
        {
            var definition = catalog.FindYokai(pair.Key);
            Require(definition != null && Mathf.Approximately(definition.AggroRadius, pair.Value) &&
                    definition.AggroRadius < 10f,
                $"aggro radius mismatch: {pair.Key}");
            Require(definition.BodyTiles == WorldV72Rules.BodyTilesForHitPoints(definition.HitPoints),
                $"body size derivation mismatch: {pair.Key}");
        }
        Require(!YokaiBrain.IsWithinAggroRadius(new Vector3(8.01f, 0f), 8f) &&
                YokaiBrain.IsWithinAggroRadius(new Vector3(8f, 0f), 8f),
            "aggro boundary mismatch");
        Require(WorldV72Rules.ShouldTargetCore(Vector2.zero, new Vector2(28f, 0f), 28f) &&
                !WorldV72Rules.ShouldTargetCore(Vector2.zero, new Vector2(28.01f, 0f), 28f),
            "base vicinity radius mismatch");

        Require(InvasionScheduleRules.IsInvasionNight(6) &&
                InvasionScheduleRules.IsInvasionNight(16) &&
                InvasionScheduleRules.IsInvasionNight(26) &&
                !InvasionScheduleRules.IsInvasionNight(5) &&
                !InvasionScheduleRules.IsInvasionNight(15),
            "invasion 6+10n schedule mismatch");
        Require(WorldV72Rules.BodyTilesForHitPoints(499) == 1 &&
                WorldV72Rules.BodyTilesForHitPoints(500) == 2 &&
                WorldV72Rules.BodyTilesForHitPoints(1999) == 2 &&
                WorldV72Rules.BodyTilesForHitPoints(2000) == 3,
            "invasion body size boundaries mismatch");

        var clockObject = new GameObject("v72-invasion-regression-clock");
        try
        {
            var clock = clockObject.AddComponent<DayNightService>();
            Require(clock.ConfigureOfficialData(catalog) &&
                    clock.RestoreTimeState(6, clock.DayDurationSeconds, true),
                "invasion regression clock setup failed");
            var inventory = new Inventory(catalog.FindItem);
            using var invasion = new InvasionService(catalog, clock, inventory);
            var club = catalog.FindYokai("club");
            Require(invasion.RecordInfiltration(club) &&
                    Mathf.Approximately(invasion.TemperatureRiseCelsius, .5f) &&
                    invasion.RecoolAvailableDay == 7,
                "club infiltration temperature mismatch");
            Require(!invasion.TryRecool(out _, out _, out _),
                "night recool must be rejected");
            Require(clock.RestoreTimeState(7, 0f, false) && inventory.TryAdd("ice_shard", 1),
                "next-day recool setup failed");
            Require(invasion.TryRecool(out var spent, out var cooled, out _) && spent == 1 &&
                    Mathf.Approximately(cooled, .5f) &&
                    Mathf.Approximately(invasion.TemperatureRiseCelsius, 0f),
                "next-day recool mismatch");

            var save = new SaveGame
            {
                invasionTemperatureRise = 8f,
                invasionRecoolAvailableDay = 17,
                invasionLastInfiltrationDay = 16
            };
            var roundTrip = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(save));
            roundTrip.NormalizeAfterLoad();
            Require(Mathf.Approximately(roundTrip.invasionTemperatureRise, 8f) &&
                    roundTrip.invasionRecoolAvailableDay == 17 &&
                    roundTrip.invasionLastInfiltrationDay == 16,
                "invasion save round-trip mismatch");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(clockObject);
        }

        var cap = ReadInt(catalog, GlobalKeys.YokaiCap);
        Require(catalog.DayCurves.All(curve => curve != null && curve.MaxActive > 0 &&
                (curve.EventId == "baekjung" || curve.MaxActive <= cap)),
            "normal-night max_active exceeds preserved yokai cap");

        var encounterSource = File.ReadAllText(
            "Assets/Scripts/Nyangbingo/World/MainGameEncounterCoordinator.cs");
        Require(encounterSource.Contains("TerrainSpawnRules.TryChooseCell") &&
                encounterSource.Contains("ResolveSpawnTarget") &&
                encounterSource.Contains("gateByAggroRadius: usesAggroRadius"),
            "terrain/core/aggro runtime wiring missing");
        var saveSource = File.ReadAllText(
            "Assets/Scripts/Nyangbingo/Save/MainGameSaveCoordinator.cs");
        var placedObjectsRestoreIndex = saveSource.IndexOf("RestoreStage(\"placed objects\"", StringComparison.Ordinal);
        var encountersRestoreIndex = saveSource.IndexOf("RestoreStage(\"encounters\"", StringComparison.Ordinal);
        Require(placedObjectsRestoreIndex >= 0 && encountersRestoreIndex >= 0 &&
                placedObjectsRestoreIndex < encountersRestoreIndex,
            "placed objects must restore before yokai target selection");

        Debug.Log("[Nyangbingo] v72 world/invasion regression passed: world 600/135, " +
                  "CSV ore profiles, terrain 10x7, aggro 5/7/8/9, core radius 28, " +
                  "invasion 6+10n, body 1/2/3, temperature/recool/save.");
    }

        private static int ReadInt(GameDataCatalog catalog, string key)
        {
            var definition = catalog.FindGlobal(key);
            if (definition == null || !definition.TryGetInt(out var value))
                throw new InvalidOperationException($"missing integer global: {key}");
            return value;
        }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
