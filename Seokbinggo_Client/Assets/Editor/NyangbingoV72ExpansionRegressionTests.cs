using System;
using System.Collections.Generic;
using System.Linq;
using Nyangbingo.Bosses;
using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.UI;
using Nyangbingo.World;
using UnityEditor;
using UnityEngine;

public static class NyangbingoV72ExpansionRegressionTests
{
    private const string CatalogPath = "Assets/Data/SO/GameDataCatalog.asset";
    private const string ConfigPath = "Assets/Data/SO/WorldGenerationConfig.asset";

    [MenuItem("Nyangbingo/Run v72 Expansion Regression")]
    public static void RunAll()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<GameDataCatalog>(CatalogPath);
        var config = AssetDatabase.LoadAssetAtPath<WorldGenerationConfig>(ConfigPath);
        Require(catalog != null && config != null, "catalog or world config missing");
        Require(catalog.Items.Count == 174 && catalog.Recipes.Count == 95 &&
                catalog.Globals.Count == 240 && catalog.Equipment.Count == 44 &&
                catalog.CombatProfiles.Count == 18 && catalog.Bosses.Count == 10 &&
                catalog.DayCurves.Count == 30 && catalog.Talismans.Count == 5,
            "v72 union catalog counts mismatch");

        ValidateLatestGlobals(catalog);
        ValidateBoundaryIceRock(catalog, config);
        ValidateCombat(catalog);
        ValidateRecipes(catalog);
        ValidateBosses(catalog);
        ValidateExpansionGate(catalog);
        ValidateDemoContract();

        Debug.Log("[Nyangbingo] v72 expansion regression passed: catalog 174/95/240/44/18/10, " +
                  "boundary ice rock hardness 2, 31-day scope-B gate, ten surface bosses, " +
                  "five forced encounters, and 30-day demo saves contract.");
    }

    private static void ValidateLatestGlobals(GameDataCatalog catalog)
    {
        Require(ReadInt(catalog, GlobalKeys.DeadlineRemoved) == 1 &&
                catalog.FindGlobal(GlobalKeys.WinCondition)?.Value == "final_boss_kill" &&
                catalog.FindGlobal(GlobalKeys.BossArenaLayer)?.Value == "surface" &&
                ReadInt(catalog, GlobalKeys.AltarCount) == 10 &&
                catalog.FindGlobal(GlobalKeys.FurnitureMvpScope)?.Value == "A",
            "latest endless/surface/furniture globals mismatch");

        var removed = new[]
        {
            "cold_source_required", "cold_cap_muldanji", "cold_cap_icejar",
            "cold_cap_icestorage", "cold_cap_frostcooler", "win_seal_pct",
            "night_wave_table", "wave_night_period", "wave_night_offset", "wave_mult_target",
            "wave_advance_sec", "sun_scale_ties_dcounter", "tree_decay_by_day",
            "day_surface_reach_tiles_s10", "shade_gate_stage"
        };
        foreach (var key in removed)
            Require(catalog.FindGlobal(key) == null, $"removed legacy global still loaded: {key}");
    }

    private static void ValidateBoundaryIceRock(GameDataCatalog catalog, WorldGenerationConfig config)
    {
        Require(ReadInt(catalog, GlobalKeys.BoundaryIceRockHardness) == 2,
            "boundary ice rock global mismatch");
        var world = new MapGenerator(config, catalog).GenerateDetailed(987654);
        var foundDeep = false;
        var foundBedrock = false;
        for (var x = 0; x < world.width && (!foundDeep || !foundBedrock); x++)
        for (var y = 0; y < world.height && (!foundDeep || !foundBedrock); y++)
        {
            var tile = world.tiles[x, y];
            if (tile.elementType == WorldTileTypes.StoneDeep)
            {
                foundDeep = true;
                Require(tile.hardness == 2, "deep boundary ice rock did not read globals hardness 2");
            }
            else if (tile.elementType == WorldTileTypes.Bedrock)
            {
                foundBedrock = true;
                Require(tile.hardness == 3, "permanent bottom bedrock must remain protected hardness 3");
            }
        }
        Require(foundDeep && foundBedrock, "generated world did not contain deep rock and bedrock");
    }

    private static void ValidateCombat(GameDataCatalog catalog)
    {
        var expectedIds = new[]
        {
            "bare_claw", "iron_claw", "icesteel_claw", "dokkaebi_club", "cheolseon",
            "frostclaw_gauntlet", "hapjukseon", "straw_sling", "gakgung", "singijeon_sondae",
            "seonge_gakgung", "ice_root_bow", "cold_wave_singijeon", "seonge_fan",
            "ice_root_whipfan", "cold_wave_fan", "sangun_claw", "perfect_claw"
        };
        Require(expectedIds.All(id => catalog.FindCombatProfile(id) != null),
            "one or more v72 combat profiles missing");
        Require(Mathf.Approximately(catalog.FindCombatProfile("ice_root_bow").AttackDamage, 28.3f) &&
                catalog.FindCombatProfile("straw_sling").ArcDegrees == 0f &&
                catalog.FindCombatProfile("singijeon_sondae").MaxTargets == 3 &&
                catalog.FindCombatProfile("cold_wave_singijeon").MaxTargets == 5 &&
                Mathf.Approximately(catalog.FindCombatProfile("perfect_claw").DamagePerSecond, 99.2f),
            "v72 fractional/projectile/multi-target combat schema mismatch");
    }

    private static void ValidateRecipes(GameDataCatalog catalog)
    {
        foreach (var recipe in catalog.Recipes)
        {
            Require(recipe != null && recipe.Output.item != null && recipe.Output.amount > 0,
                "recipe output reference missing");
            Require(recipe.Ingredients.All(input => input.item != null && input.amount > 0),
                $"recipe input reference missing: {recipe.Id}");
        }
        var expansionIds = new[]
        {
            "jigwi_summon", "samdugumi_summon", "eop_summon", "singijeon_sondae",
            "seonge_tower", "cold_wave_tower", "sangun_claw", "perfect_claw"
        };
        Require(expansionIds.All(id => catalog.FindItem(id) != null && catalog.FindRecipe(id) != null),
            "key expansion item/recipe chain missing");
    }

    private static void ValidateBosses(GameDataCatalog catalog)
    {
        var expected = new Dictionary<string, (BossKind kind, int hp, int forcedDay)>(StringComparer.Ordinal)
        {
            ["king_dokkaebi"] = (BossKind.GoblinChief, 13800, 0),
            ["mother_bulgasari"] = (BossKind.MotherBulgasari, 10000, 0),
            ["imugi_boss"] = (BossKind.Imugi, 16000, 30),
            ["jigwi"] = (BossKind.Jigwi, 21500, 0),
            ["gangcheol_blaze"] = (BossKind.GangcheolBlaze, 21000, 50),
            ["sangun"] = (BossKind.Sangun, 22500, 60),
            ["samdugumi"] = (BossKind.Samdugumi, 22000, 0),
            ["eop_guryeongi"] = (BossKind.EopGuryeongi, 20500, 0),
            ["yeongno"] = (BossKind.Yeongno, 20000, 90),
            ["gangcheol_perfect"] = (BossKind.GangcheolPerfect, 22000, 100)
        };
        foreach (var pair in expected)
        {
            var boss = catalog.FindBoss(pair.Key);
            Require(boss != null && boss.Kind == pair.Value.kind && boss.HitPoints == pair.Value.hp &&
                    boss.ForcedDay == pair.Value.forcedDay && boss.ArenaLayer == "surface" &&
                    boss.HeatStage >= 1 && boss.HeatStage <= 3 && boss.GuaranteedDrops.Length >= 2,
                $"boss contract mismatch: {pair.Key}");
            if (pair.Value.forcedDay > 0 && pair.Key != "imugi_boss")
                Require(boss.SummonItem == null && boss.SummonMaterials.Length == 0,
                    $"forced boss must not require a summon item: {pair.Key}");
            else
                Require(boss.SummonItem != null && boss.SummonMaterials.Length > 0,
                    $"summoned boss chain missing: {pair.Key}");
        }
        Require(catalog.FindBoss("gangcheol_blaze").SpecialShape == BossSpecialShape.Fan &&
                catalog.FindBoss("gangcheol_perfect").SpecialShape == BossSpecialShape.Fan,
            "v72 fan boss shape missing");

        var root = new GameObject("v72-forced-boss-clock");
        try
        {
            var clock = root.AddComponent<DayNightService>();
            Require(clock.ConfigureOfficialData(catalog), "forced boss test clock setup failed");
            foreach (var day in new[] { 30, 50, 60, 90, 100 })
            {
                var boss = catalog.Bosses.Single(definition => definition.ForcedDay == day);
                Require(clock.RestoreTimeState(day, 900f, true) &&
                        BossEncounterRules.ShouldStartForcedEncounter(boss, clock, false),
                    $"forced encounter did not arm at day {day}");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void ValidateExpansionGate(GameDataCatalog catalog)
    {
        var bRecipe = catalog.FindRecipe("jigwi_summon");
        Require(bRecipe != null && bRecipe.MvpScope == ItemMvpScope.B &&
                !ExpansionProgressionRules.IsScopeAvailable(ItemMvpScope.B, 30) &&
                ExpansionProgressionRules.IsScopeAvailable(ItemMvpScope.B, 31) &&
                !MainGameCraftingUiController.ShouldShowRecipe(bRecipe, true, 30) &&
                MainGameCraftingUiController.ShouldShowRecipe(bRecipe, true, 31),
            "scope-B day 31 progression gate mismatch");
    }

    private static void ValidateDemoContract()
    {
        Require(GameShellController.DemoEndDay == 30 &&
                GameShellController.DemoSaveDays.SequenceEqual(new[] { 1, 15, 30 }) &&
                GameShellController.ShouldEndDemoAtDawn(true, 31, 30) &&
                !GameShellController.ShouldEndDemoAtDawn(true, 30, 30) &&
                !GameShellController.ShouldEndDemoAtDawn(false, 31, 30),
            "30-day judge demo contract mismatch");
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
