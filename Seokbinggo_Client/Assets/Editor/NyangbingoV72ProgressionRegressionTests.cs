using System;
using System.IO;
using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using Nyangbingo.Save;
using Nyangbingo.World;
using UnityEditor;
using UnityEngine;

public static class NyangbingoV72ProgressionRegressionTests
{
    private const string CatalogPath = "Assets/Data/SO/GameDataCatalog.asset";

    [MenuItem("Nyangbingo/Run v72 Progression Regression")]
    public static void RunAll()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<GameDataCatalog>(CatalogPath);
        Require(catalog != null, "GameDataCatalog is missing");
        Require(HeatStageService.TryCreate(catalog, out var heat), "heat-stage globals are invalid");
        Require(heat.Current == 1 && Mathf.Approximately(heat.DayFireDamagePerSecond, 1.5f) &&
                Mathf.Approximately(heat.TreeDensityMultiplier, 1f), "stage 1 values mismatch");
        Require(!heat.OnNamedKill("imugi_boss") && heat.Current == 1,
            "2-to-3 gate must not skip stage 1");
        Require(!heat.OnNamedKill("mother_bulgasari") && heat.Current == 1,
            "unrelated boss must not advance heat");
        Require(heat.OnNamedKill("king_dokkaebi") && heat.Current == 2 &&
                Mathf.Approximately(heat.DayFireDamagePerSecond, 4.5f) &&
                Mathf.Approximately(heat.TreeDensityMultiplier, .5f), "king gate mismatch");
        Require(!heat.OnNamedKill("king_dokkaebi") && heat.Current == 2,
            "repeated first gate must not advance again");
        Require(heat.OnNamedKill("imugi_boss") && heat.Current == 3 &&
                Mathf.Approximately(heat.DayFireDamagePerSecond, 15f) &&
                Mathf.Approximately(heat.TreeDensityMultiplier, 0f), "imugi gate mismatch");
        Require(!heat.OnNamedKill("king_dokkaebi") && heat.Current == 3,
            "stage 3 must be terminal");
        Require(Mathf.Approximately(MainGameParallaxBackground.CalculateSunScaleMultiplier(1), 1f) &&
                Mathf.Approximately(MainGameParallaxBackground.CalculateSunScaleMultiplier(2), 1.5f) &&
                Mathf.Approximately(MainGameParallaxBackground.CalculateSunScaleMultiplier(3), 2f) &&
                Mathf.Approximately(MainGameParallaxBackground.CalculateSunScaleMultiplier(99), 2f),
            "sun scale must follow heat stages at 1.0/1.5/2.0");

        var king = catalog.FindBoss("king_dokkaebi");
        Require(king != null && king.RecommendedDay == "8" &&
                Mathf.Approximately(king.TelegraphSeconds, .75f) &&
                king.SpecialShape == BossSpecialShape.Box &&
                Mathf.Approximately(king.SpecialRangeTiles, 2f) &&
                king.SpecialDamagePerHit == 12 &&
                Mathf.Approximately(king.SpecialKnockbackTiles, 4f) &&
                Mathf.Approximately(king.SpecialCooldownSeconds, 8f) &&
                king.SpecialAimLocks,
            "king_dokkaebi CSV repair changed or shifted combat fields");
        var imugi = catalog.FindBoss("imugi_boss");
        Require(imugi != null && imugi.RecommendedDay == "30" && imugi.ForcedDay == 30,
            "imugi_boss day-30 configuration mismatch");

        Require(EquipmentColdPenaltyRules.TryCreate(catalog, out var coldPenalty),
            "v74 equipment cold-penalty globals are invalid");
        var coldArmor = new EquipmentSystem();
        Require(coldArmor.TryEquip(EquipmentDefinition.CreateRuntime("cold_head", Nyangbingo.Core.EquipmentSlot.Head,
                    false, 3, equipmentSetId: ArmorSetRules.SeolhanpungSetId,
                    setTemperatureModifier: ArmorSetRules.SeolhanpungTemperatureRise,
                    setFireModifier: ArmorSetRules.SeolhanpungFireDamage,
                    usesColdTolerance: true, coldTolerance: -10)) &&
                coldArmor.TryEquip(EquipmentDefinition.CreateRuntime("cold_body", Nyangbingo.Core.EquipmentSlot.Body,
                    false, 4, moveBonus: .15f, equipmentSetId: ArmorSetRules.SeolhanpungSetId,
                    setTemperatureModifier: ArmorSetRules.SeolhanpungTemperatureRise,
                    setFireModifier: ArmorSetRules.SeolhanpungFireDamage,
                    usesColdTolerance: true, coldTolerance: -10)) &&
                coldArmor.TryEquip(EquipmentDefinition.CreateRuntime("cold_feet", Nyangbingo.Core.EquipmentSlot.Feet,
                    false, 3, equipmentSetId: ArmorSetRules.SeolhanpungSetId,
                    setTemperatureModifier: ArmorSetRules.SeolhanpungTemperatureRise,
                    setFireModifier: ArmorSetRules.SeolhanpungFireDamage,
                    usesColdTolerance: true, coldTolerance: -10)),
            "cold armor setup failed");
        var coldStats = new StatSheet();
        coldStats.Recalculate(coldArmor, -10, coldPenalty);
        Require(coldStats.Defense == 10 &&
                Mathf.Approximately(coldStats.MovementMultiplier, 1.15f) &&
                Mathf.Approximately(coldStats.TemperatureRiseModifier,
                    ArmorSetRules.SeolhanpungTemperatureRise),
            "cold-compatible armor stats mismatch");
        coldStats.Recalculate(coldArmor, -15, coldPenalty);
        Require(coldStats.Defense == 5 &&
                Mathf.Approximately(coldStats.MovementMultiplier, 1.15f) &&
                Mathf.Approximately(coldStats.TemperatureRiseModifier, 0f) &&
                Mathf.Approximately(coldStats.FireDamageModifier, 0f),
            "five-degree cold deficit must halve defense, preserve movement, and disable set bonuses");
        coldStats.Recalculate(coldArmor, -30, coldPenalty);
        Require(coldStats.Defense == 3, "cold defense floor must clamp at 30 percent");

        RequireUpgradeModule(catalog, 1, "석빙고 1단 움집", 30f, "stone:10,clay:6");
        RequireUpgradeModule(catalog, 2, "석빙고 2단 토굴 석빙고", 60f,
            "stone:20,clay:12,iron_ingot:2");
        RequireUpgradeModule(catalog, 3, "석빙고 3단 돌 석빙고", 90f,
            "stone:30,icesteel_ingot:4");
        RequireUpgradeModule(catalog, 4, "석빙고 4단 서리 석빙고", 120f,
            "seonge_ingot:6,icesteel_ingot:6,yokai_tear:160");
        RequireUpgradeModule(catalog, 5, "석빙고 5단 도관 석빙고", 150f,
            "ice_root_bundle:8,blaze_yeokrin:1,yokai_tear:320");
        RequireUpgradeModule(catalog, 6, "석빙고 6단 한파 석빙고", 180f,
            "cold_wave_ingot:10,eop_scale_mat:1,yokai_tear:560");

        Require(Mathf.Approximately(FrostSpreadService.CalculateBandFromNorm(1, 10), .9f) &&
                Mathf.Approximately(FrostSpreadService.CalculateBandFromNorm(5, 10), .5f) &&
                Mathf.Approximately(FrostSpreadService.CalculateBandFromNorm(10, 10), 0f),
            "frost normalized band formula mismatch");
        var frost = new FrostSpreadService(catalog);
        Require(frost.EndingConfigurationValid && frost.SurvivalContinuesAfterEnding &&
                !frost.DemoEndingReached && !frost.FinalEndingReached,
            "v75 ending configuration mismatch");
        var endingEventCount = 0;
        var endingBossId = string.Empty;
        frost.EndingReached += bossId =>
        {
            endingEventCount++;
            endingBossId = bossId;
        };
        Require(!frost.OnAltarBossClear("club"), "non-zone boss advanced frost");
        for (var index = 0; index < catalog.Zones.Count; index++)
        {
            var bossId = catalog.Zones[index].BossId;
            Require(frost.OnAltarBossClear(bossId), $"altar boss '{bossId}' did not advance");
            Require(!frost.OnAltarBossClear(bossId), $"repeated altar boss '{bossId}' advanced twice");
        }
        Require(frost.AltarClears == 10 && frost.StepCount == 10 &&
                frost.ClearedBossCount == 10 && Mathf.Approximately(frost.BandFromNorm, 0f),
            "frost clear count must clamp monotonically at 10");
        Require(frost.DemoEndingReached && frost.FinalEndingReached &&
                endingEventCount == 2 && endingBossId == "gangcheol_perfect",
            "gate ending flags/events must follow demo and final gate bosses");
        Require(frost.TryResolveOreHardness(WorldTileTypes.CopperOre, out var copperHardness) &&
                copperHardness == catalog.FindMineralTier(WorldTileTypes.CopperOre).Hardness &&
                frost.TryResolveOreHardness(WorldTileTypes.IronOre, out var ironHardness) &&
                ironHardness == catalog.FindMineralTier(WorldTileTypes.IronOre).Hardness &&
                frost.TryResolveOreHardness(WorldTileTypes.IceSteelOre, out var iceSteelHardness) &&
                iceSteelHardness == catalog.FindMineralTier(WorldTileTypes.IceSteelOre).Hardness,
            "frost ore hardness must come from mineral-tiers catalog");
        frost.MarkPending(new Vector2Int(3, 7));
        Require(!frost.TryLazyReveal(new Vector2Int(3, 7), false, out _),
            "non-air-adjacent pending tile revealed");
        Require(frost.TryLazyReveal(new Vector2Int(3, 7), true, out var ore) &&
                !string.IsNullOrWhiteSpace(ore), "air-adjacent pending tile did not reveal");

        var frostSource = File.ReadAllText("Assets/Scripts/Nyangbingo/World/FrostSpreadService.cs");
        Require(!frostSource.Contains("UnsealBedrockLayer") &&
                !frostSource.Contains("new DepthBand(91") &&
                !frostSource.Contains("new DepthBand(46") &&
                !frostSource.Contains("new DepthBand(136"),
            "legacy hardcoded frost bands or bedrock unseal remain");

        var save = new SaveGame
        {
            heatStage = 3,
            altarClears = 10,
            frostClearedBossIds = frost.ExportClearedBossIds(),
            frostPendingCells = frost.ExportPendingCells()
        };
        var roundTrip = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(save));
        roundTrip.NormalizeAfterLoad();
        Require(roundTrip.heatStage == 3 && roundTrip.altarClears == 10 &&
                roundTrip.frostClearedBossIds.Count == 10,
            "heat/frost save round-trip mismatch");
        Require(heat.Restore(roundTrip.heatStage) && heat.Current == 3,
            "heat stage restore mismatch");
        var restoredFrost = new FrostSpreadService(catalog);
        Require(restoredFrost.RestoreAltarProgress(roundTrip.altarClears, roundTrip.frostClearedBossIds),
            "frost clear restore mismatch");
        restoredFrost.RestorePendingCells(roundTrip.frostPendingCells);
        Require(restoredFrost.AltarClears == 10 && restoredFrost.ClearedBossCount == 10 &&
                restoredFrost.DemoEndingReached && restoredFrost.FinalEndingReached &&
                !restoredFrost.OnAltarBossClear(catalog.Zones[0].BossId),
            "restored frost clear identity/count mismatch");

        Debug.Log("[Nyangbingo] v72 progression regression passed: named heat 1/2/3, " +
                  "sun scale 1.0/1.5/2.0, official seokbinggo modules, " +
                  "day fire 1.5/4.5/15, frost 10-step normalized band, " +
                  "v74 cold penalty, v75 gate endings, permanent bedrock, save round-trip.");
    }

    [MenuItem("Nyangbingo/Run v76 MainGame Runtime Smoke")]
    public static void RunMainGameRuntimeSmoke()
    {
        Require(EditorApplication.isPlaying, "runtime smoke must run in MainGame play mode");
        var runtime = UnityEngine.Object.FindAnyObjectByType<MainGameRuntimeServices>();
        var bootstrap = UnityEngine.Object.FindAnyObjectByType<MainGameBootstrap>();
        var encounter = UnityEngine.Object.FindAnyObjectByType<MainGameEncounterCoordinator>();
        var saveCoordinator = UnityEngine.Object.FindAnyObjectByType<MainGameSaveCoordinator>();
        Require(runtime != null && runtime.Initialize() && runtime.IsInitialized &&
                bootstrap != null && bootstrap.IsWorldReady && encounter != null &&
                encounter.PlayerTransform != null && encounter.PlayerHealth != null &&
                saveCoordinator != null && saveCoordinator.Initialize(),
            "MainGame runtime services, world, player, or save coordinator are not ready");

        var catalog = bootstrap.GameDataCatalog;
        Require(catalog != null, "MainGame catalog is missing");
        var baseline = saveCoordinator.CaptureSnapshot();
        Require(baseline != null, "baseline MainGame snapshot capture failed");

        var probe = new GameObject("v76-temperature-smoke-probe");
        try
        {
            var playerCell = bootstrap.TileService.WorldToCell(encounter.PlayerTransform.position);
            var surfaceHeights = bootstrap.Session.LastResult.surfaceHeights;
            var x = Mathf.Clamp(playerCell.x, 0, surfaceHeights.Length - 1);
            var warmCell = new Vector3Int(x, surfaceHeights[x] + 3, 0);
            var coldCell = warmCell;
            var foundCold = false;
            for (var y = surfaceHeights[x]; y >= surfaceHeights[x] - 200; y--)
            {
                var candidate = new Vector3Int(x, y, 0);
                if (runtime.RoomTemperature.Resolve(candidate) != -5) continue;
                coldCell = candidate;
                foundCold = true;
                break;
            }
            Require(foundCold, "generated MainGame world has no natural -5C probe cell");

            probe.transform.position = bootstrap.TileService.GetCellCenterWorld(warmCell);
            runtime.PlayerTemperature.SetTrackedTransform(probe.transform);
            runtime.PlayerTemperature.Tick(.1f);
            Require(runtime.RoomTemperature.Resolve(warmCell) == 0,
                "temperature smoke warm probe is not 0C");
            Require(runtime.EquipmentSystem.TryEquip(catalog.FindEquipment("straw_helm")) &&
                    runtime.EquipmentSystem.TryEquip(catalog.FindEquipment("straw_armor")) &&
                    runtime.EquipmentSystem.TryEquip(catalog.FindEquipment("straw_boots")) &&
                    encounter.PlayerHealth.Defense == 3,
                "live player did not receive compatible straw defense 3");

            var temperatureEventCount = 0;
            runtime.PlayerTemperature.RoomTemperatureChanged += CountTemperatureChange;
            probe.transform.position = bootstrap.TileService.GetCellCenterWorld(coldCell);
            runtime.PlayerTemperature.Tick(.1f);
            Require(runtime.PlayerTemperature.CurrentRoomTemperature == -5 &&
                    encounter.PlayerHealth.Defense == 1 && temperatureEventCount == 1,
                "live room-temperature event did not reduce straw defense from 3 to 1");
            probe.transform.position = bootstrap.TileService.GetCellCenterWorld(warmCell);
            runtime.PlayerTemperature.Tick(.1f);
            Require(runtime.PlayerTemperature.CurrentRoomTemperature == 0 &&
                    encounter.PlayerHealth.Defense == 3 && temperatureEventCount == 2,
                "live room-temperature event did not restore compatible straw defense");
            runtime.PlayerTemperature.RoomTemperatureChanged -= CountTemperatureChange;

            void CountTemperatureChange(int _) => temperatureEventCount++;
        }
        finally
        {
            runtime.PlayerTemperature.SetTrackedTransform(encounter.PlayerTransform);
            UnityEngine.Object.Destroy(probe);
        }
        Require(saveCoordinator.TryApplySnapshot(baseline),
            "temperature smoke baseline restore failed");

        var endingEventCount = 0;
        var endingId = string.Empty;
        runtime.FrostSpread.EndingReached += CaptureEnding;
        try
        {
            GameEvents.RaiseBossDefeated(catalog.FindBoss("imugi_boss"));
            Require(runtime.FrostSpread.DemoEndingReached &&
                    !runtime.FrostSpread.FinalEndingReached &&
                    runtime.FrostSpread.SurvivalContinuesAfterEnding &&
                    endingEventCount == 1 && endingId == "imugi_boss",
                "live boss event did not raise the demo ending contract");
            var ended = saveCoordinator.CaptureSnapshot();
            Require(ended != null && ended.frostClearedBossIds.Contains("imugi_boss"),
                "live ending state was not captured in frostClearedBossIds");
            Require(saveCoordinator.TryApplySnapshot(baseline) &&
                    !runtime.FrostSpread.DemoEndingReached,
                "live ending baseline restore failed");
            Require(saveCoordinator.TryApplySnapshot(ended) &&
                    runtime.FrostSpread.DemoEndingReached &&
                    runtime.FrostSpread.SurvivalContinuesAfterEnding,
                "live ending save reload did not restore a continuing session");
        }
        finally
        {
            runtime.FrostSpread.EndingReached -= CaptureEnding;
            saveCoordinator.TryApplySnapshot(baseline);
        }

        Debug.Log("[Nyangbingo] v76 MainGame runtime smoke passed: live temperature defense 3->1->3, " +
                  "boss event -> demo ending, snapshot clear/restore, survival continuation.");

        void CaptureEnding(string bossId)
        {
            endingEventCount++;
            endingId = bossId;
        }
    }

    private static void RequireUpgradeModule(GameDataCatalog catalog, int stage, string displayName,
        float buildTimeSeconds, string expectedMaterials)
    {
        var module = catalog.FindModule($"seokbinggo_s{stage}");
        Require(module != null && module.Item == null && module.DisplayName == displayName &&
                Mathf.Approximately(module.BuildTimeSeconds, buildTimeSeconds),
            $"seokbinggo stage {stage} identity/item/time mismatch");
        var expected = expectedMaterials.Split(',');
        Require(module.Materials.Length == expected.Length,
            $"seokbinggo stage {stage} material count mismatch");
        for (var index = 0; index < expected.Length; index++)
        {
            var parts = expected[index].Split(':');
            Require(parts.Length == 2 && module.Materials[index].item != null &&
                    module.Materials[index].item.Id == parts[0] &&
                    int.TryParse(parts[1], out var amount) && module.Materials[index].amount == amount,
                $"seokbinggo stage {stage} material {index} mismatch");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException($"[Nyangbingo] v72 progression regression failed: {message}");
    }
}
