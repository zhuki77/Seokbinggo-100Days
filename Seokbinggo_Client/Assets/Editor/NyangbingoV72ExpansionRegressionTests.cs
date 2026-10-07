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
        Require(catalog.Items.Count == 170 && catalog.Recipes.Count == 97 &&
                catalog.Globals.Count == 253 && catalog.Equipment.Count == 44 &&
                catalog.CombatProfiles.Count == 19 && catalog.Bosses.Count == 10 &&
                catalog.DayCurves.Count == 30 && catalog.Talismans.Count == 5,
            "v72 union catalog counts mismatch");

        ValidateLatestGlobals(catalog);
        ValidateBoundaryIceRock(catalog, config);
        ValidateCombat(catalog);
        ValidateRecipes(catalog);
        ValidateBosses(catalog);
        ValidateExpansionGate(catalog);
        ValidateDemoContract();
        ValidateDayCurveExtensions(catalog);
        ValidateCraftingExtension(catalog);

        Debug.Log("[Nyangbingo] v72 expansion regression passed: catalog 170/97/253/44/19/10, " +
                  "boundary ice rock hardness 2, 31-day scope-B gate, ten surface bosses, " +
                  "five forced encounters, and 30-day demo saves contract.");
    }

    private static void ValidateLatestGlobals(GameDataCatalog catalog)
    {
        Require(ReadInt(catalog, GlobalKeys.DeadlineRemoved) == 1 &&
                catalog.FindGlobal(GlobalKeys.WinCondition)?.Value == "gate_named_clear" &&
                catalog.FindGlobal(GlobalKeys.BossArenaLayer)?.Value == "surface" &&
                ReadInt(catalog, GlobalKeys.AltarCount) == 10 &&
                catalog.FindGlobal(GlobalKeys.FurnitureMvpScope)?.Value == "A",
            "latest endless/surface/furniture globals mismatch");

        var removed = new[]
        {
            "cold_source_required", "cold_cap_muldanji", "cold_cap_icejar",
            "cold_cap_icestorage", "cold_cap_frostcooler", "win_seal_pct",
            "night_wave_table", "wave_night_period", "wave_night_offset",
            "wave_advance_sec", "sun_scale_ties_dcounter", "tree_decay_by_day",
            "day_surface_reach_tiles_s10", "shade_gate_stage"
        };
        foreach (var key in removed)
            Require(catalog.FindGlobal(key) == null, $"removed legacy global still loaded: {key}");
        Require(catalog.FindGlobal("wave_mult_target")?.Value == "hp_only",
            "v79 variant HP multiplier target is missing");
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
            "bare_claw", "iron_claw", "icesteel_claw", "dokkaebi_club", "cheolseon", "seolpungseon",
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

    private static void ValidateCraftingExtension(GameDataCatalog catalog)
    {
        var seolpungseon = catalog.FindRecipe(FanItemIds.Seolpungseon);
        var frostBell = catalog.FindRecipe(BellRopeItemIds.FrostBellRope);
        Require(seolpungseon != null && frostBell != null &&
                seolpungseon.MvpScope == ItemMvpScope.B &&
                frostBell.MvpScope == ItemMvpScope.B &&
                seolpungseon.Station == CraftingStation.IceAnvil &&
                frostBell.Station == CraftingStation.IceAnvil &&
                seolpungseon.Ingredients[0].item.Id == FanItemIds.Cheolseon &&
                frostBell.Ingredients[0].item.Id == "iron_bell_rope" &&
                catalog.FindCombatProfile(FanItemIds.Seolpungseon) != null &&
                MainGameTurretRuntime.TryGetPassiveCounterAuraConfiguration(
                    BellRopeItemIds.FrostBellRope, out var kind, out var radius,
                    out var effect, out var duration, out var cooldown) &&
                kind == CounterAuraKind.FrostBellRope &&
                Mathf.Approximately(radius, 10f) &&
                Mathf.Approximately(effect, .3f) &&
                Mathf.Approximately(duration, 4f) &&
                Mathf.Approximately(cooldown, 12f),
            "crafting-ext seolpungseon/frost_bell_rope contract mismatch");
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
                !MainGameCraftingUiController.ShouldShowRecipe(bRecipe, true, 1) &&
                !MainGameCraftingUiController.ShouldShowRecipe(bRecipe, true, 30) &&
                !MainGameCraftingUiController.ShouldShowRecipe(bRecipe, true, 31) &&
                MainGameCraftingUiController.ShouldShowRecipe(bRecipe, false, 1),
            "v86 B11 hidden policy must hide B recipes independently of the separate boss-use date gate");
    }

    private static void ValidateDemoContract()
    {
        Require(GameShellController.DemoSaveDays.SequenceEqual(new[] { 1, 15, 30 }) &&
                !GameShellController.ShouldEndDemoAtDay(31) &&
                !GameShellController.ShouldEndDemoAtDay(100),
            "Date alone must not open the result screen on day 31 or day 100.");
    }

    private static void ValidateDayCurveExtensions(GameDataCatalog catalog)
    {
        DayCurveExtensionResolver.ClearCache();
        Require(catalog.DayCurveExtensions.Count == 15, "day-curve-ext anchor count mismatch");
        var day31 = catalog.FindDayCurve(31);
        var day100 = catalog.FindDayCurve(100);
        Require(day31 != null && day100 != null && day31.NightYokaiCount == 8,
            "post-demo nights must resolve extension day curves");
    }

    private static int ReadInt(GameDataCatalog catalog, string key)
    {
        var definition = catalog.FindGlobal(key);
        if (definition == null || !definition.TryGetInt(out var value))
            throw new InvalidOperationException($"missing integer global: {key}");
        return value;
    }

    [MenuItem("Nyangbingo/Run Station Production Queue Regression")]
    public static void RunStationQueueRegression()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<GameDataCatalog>(CatalogPath);
        var recipe = catalog.FindRecipe("wallpaper");
        Require(recipe != null && recipe.DurationSeconds > 0f, "timed crafting fixture missing");
        var inventory = new Nyangbingo.Inventory.Inventory(catalog.FindItem);
        foreach (var ingredient in recipe.Ingredients)
            Require(inventory.TryAdd(ingredient.item.Id, ingredient.amount * 20), "fixture materials failed");
        var exists = new HashSet<string> { "station-a", "station-b" };
        var service = new Nyangbingo.Crafting.StationProductionService(catalog, inventory,
            exists.Contains, _ => true);
        var a = service.Get("station-a", recipe.Station, Vector2.zero);
        var b = service.Get("station-b", recipe.Station, Vector2.right);
        for (var i = 0; i < 5; i++) Require(service.TryEnqueue(a, recipe), "active plus four waiting failed");
        var beforeRejected = JsonUtility.ToJson(new InventorySnapshot { slots = inventory.Export() });
        Require(!service.TryEnqueue(a, recipe) && beforeRejected ==
            JsonUtility.ToJson(new InventorySnapshot { slots = inventory.Export() }), "overflow consumed materials");
        Require(service.TryEnqueue(b, recipe), "stations share queue capacity");
        service.Tick(recipe.DurationSeconds * .5f);
        Require(Mathf.Approximately(a.jobs[0].remaining, b.jobs[0].remaining), "independent station tick failed");
        var firstId = recipe.Ingredients[0].item.Id;
        var beforeCancel = inventory.Count(firstId);
        Require(service.Cancel(a, 2) && a.jobs.Count == 4 &&
            inventory.Count(firstId) == beforeCancel + recipe.Ingredients[0].amount,
            "waiting cancellation failed to refund");
        var saved = service.Export();
        var restored = new Nyangbingo.Crafting.StationProductionService(catalog, inventory,
            exists.Contains, _ => true);
        Require(restored.Restore(saved), "queue restore failed");
        var restoredA = restored.Get("station-a", recipe.Station, Vector2.zero);
        var remaining = restoredA.jobs[0].remaining;
        saved[0].jobs.Clear();
        Require(restoredA.jobs.Count == 4 && Mathf.Approximately(remaining, recipe.DurationSeconds * .5f),
            "restore did not preserve timers or deep-copy state");
        Require(restored.Cancel(restoredA, 0) &&
            Mathf.Approximately(restoredA.jobs[0].remaining, recipe.DurationSeconds),
            "active cancellation failed to promote the next job");
        var beforeDestroyed = inventory.Count(firstId);
        exists.Remove("station-a");
        restored.Tick(.1f);
        Require(restoredA.jobs.Count == 0 && inventory.Count(firstId) ==
            beforeDestroyed + 3 * recipe.Ingredients[0].amount, "destroyed station lost materials");

        // Full inventory cancellation must retain refunds across save/restore until collection fits.
        var small = new Nyangbingo.Inventory.Inventory(catalog.FindItem, recipe.Ingredients.Length);
        foreach (var ingredient in recipe.Ingredients)
            Require(small.TryAdd(ingredient.item.Id, ingredient.amount), "small fixture failed");
        var fullService = new Nyangbingo.Crafting.StationProductionService(catalog, small, _ => true, _ => true);
        var full = fullService.Get("full", recipe.Station, Vector2.zero);
        Require(fullService.TryEnqueue(full, recipe), "small reservation failed");
        var filler = catalog.FindItem("dirt");
        Require(small.TryAdd(filler.Id, filler.MaxStack * small.Capacity), "fill fixture failed");
        Require(fullService.Cancel(full, 0) && full.jobs.Count == 0 && full.returns.Count > 0,
            "full inventory cancellation lost refunds");
        Require(fullService.Restore(fullService.Export()), "pending refund restore failed");
        full = fullService.Get("full", recipe.Station, Vector2.zero);
        Require(small.TryRemove(filler.Id, small.Count(filler.Id)) && fullService.Collect(full) > 0 &&
            full.returns.Count == 0, "pending refund could not be collected");
        Debug.Log("[Nyangbingo] Station queues: capacity, isolation, refunds, persistence and destruction passed.");
    }

    [MenuItem("Nyangbingo/Run Inventory Click Drop Regression")]
    public static void RunInventoryClickDropRegression()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<GameDataCatalog>(CatalogPath);
        var bag = new Nyangbingo.Inventory.Inventory(catalog.FindItem, 3);
        var storage = new Nyangbingo.Inventory.Inventory(catalog.FindItem, 2);
        var cursor = new Nyangbingo.Inventory.Inventory(catalog.FindItem, 1);
        Require(bag.TryAdd("wood", 9), "click fixture failed");
        bag.Changed += () => Require(bag.Count("wood") + storage.Count("wood") +
            cursor.Count("wood") == 9, "observer saw an incomplete transaction");
        Require(bag.TryClickSlot(0, cursor, true) && cursor.Count("wood") == 5 &&
            bag.Count("wood") == 4, "odd stack half pickup failed");
        Require(storage.TryClickSlot(0, cursor, true) && storage.Count("wood") == 1 &&
            cursor.Count("wood") == 4, "single placement failed");
        Require(storage.TryClickSlot(0, cursor, false) && storage.Count("wood") == 5 &&
            cursor.IsEmpty, "cross-container merge failed");
        Require(bag.TryClickSlot(0, cursor, false) && bag.IsEmpty &&
            storage.TryClickSlot(0, cursor, false) && storage.Count("wood") == 9,
            "whole stack merge failed");
        var restoredCursor = new Nyangbingo.Inventory.Inventory(catalog.FindItem, 1);
        Require(storage.TryClickSlot(0, cursor, true) &&
            restoredCursor.TryImport(cursor.Export()) && restoredCursor.Count("wood") == 5,
            "held stack persistence failed");
        Require(bag.TryAdd("stone", 1) && bag.TryClickSlot(0, cursor, false) &&
            cursor.Count("stone") == 1 && bag.Count("wood") == 5, "different ID swap failed");
        var max = catalog.FindItem("wood").MaxStack;
        var full = new Nyangbingo.Inventory.Inventory(catalog.FindItem, 1);
        var extra = new Nyangbingo.Inventory.Inventory(catalog.FindItem, 1);
        Require(full.TryAdd("wood", max - 1) && extra.TryAdd("wood", 3) &&
            full.TryClickSlot(0, extra, false) && full.Count("wood") == max &&
            extra.Count("wood") == 2 && !full.TryClickSlot(0, extra, true),
            "stack cap or remainder failed");
        var weapon = new Nyangbingo.Inventory.Inventory(catalog.FindItem, 1);
        var heldWeapon = new Nyangbingo.Inventory.Inventory(catalog.FindItem, 1);
        Require(weapon.TryAdd("iron_claw", 1) && heldWeapon.TryAdd("iron_claw", 1) &&
            !weapon.TryClickSlot(0, heldWeapon, false) && heldWeapon.Count("iron_claw") == 1,
            "equipment merged");
        var aged = new Nyangbingo.Inventory.Inventory(catalog.FindItem, 1);
        var fresh = new Nyangbingo.Inventory.Inventory(catalog.FindItem, 1);
        Require(aged.TryAddWithStorageState("wood", 2, true, .5f, .2f) &&
            fresh.TryAddWithStorageState("wood", 2, false, 1f, .3f) &&
            !aged.TryClickSlot(0, fresh, false) && !aged.TryClickSlot(0, fresh, true) &&
            aged.Count("wood") == 2 && fresh.Count("wood") == 2,
            "different storage states merged");
        var splitSource = new Nyangbingo.Inventory.Inventory(catalog.FindItem, 1);
        var destination = new Nyangbingo.Inventory.Inventory(catalog.FindItem, 2);
        Require(splitSource.TryAddWithStorageState("wood", 9, true, .5f, .36f), "shift fixture failed");
        splitSource.Changed += () => Require(splitSource.Count("wood") + destination.Count("wood") == 11,
            "shift observer saw an incomplete transaction");
        Require(destination.TryAddWithStorageState("wood", 2, false, 1f, 0f) &&
            splitSource.TryTransferSlotTo(0, destination, 5) &&
            splitSource.Count("wood") == 4 && destination.Slots[0].amount == 2 &&
            destination.Slots[1].amount == 5 &&
            Mathf.Approximately(destination.Slots[1].EffectiveStorageCondition, .5f) &&
            Mathf.Approximately(splitSource.Slots[0].storageMeltRemainder +
                destination.Slots[1].storageMeltRemainder, .36f),
            "shift half failed or merged incompatible freshness");
        Require(!splitSource.TryTransferSlotTo(0, destination) && splitSource.Count("wood") == 4,
            "failed shift consumed source");
        Require(destination.TryTransferSlotTo(1, splitSource, 5) == false,
            "shift merged different melt states without an empty slot");
        var same = new Nyangbingo.Inventory.Inventory(catalog.FindItem, 1);
        var sameHeld = new Nyangbingo.Inventory.Inventory(catalog.FindItem, 1);
        Require(same.TryAddWithStorageState("wood", 2, true, .5f, 0f) &&
            sameHeld.TryAddWithStorageState("wood", 2, true, .5f, 0f) &&
            same.TryClickSlot(0, sameHeld, false) && same.Count("wood") == 4 && sameHeld.IsEmpty,
            "matching storage states failed to merge");
        var originBag = new Nyangbingo.Inventory.Inventory(catalog.FindItem, 2);
        var originCursor = new Nyangbingo.Inventory.Inventory(catalog.FindItem, 1);
        Require(originBag.TryAddWithStorageState("wood", 9, true, .5f, .36f) &&
            originBag.TryClickSlot(0, originCursor, true), "origin pickup failed");
        var remainder = originBag.Slots[0];
        Require(originBag.TryReturnCursor(0, originCursor, remainder) && originCursor.IsEmpty &&
            originBag.Slots[0].amount == 9 &&
            Mathf.Approximately(originBag.Slots[0].storageMeltRemainder, .36f),
            "half-stack return failed to restore source");
        Require(originBag.TryClickSlot(0, originCursor, false) &&
            originBag.TryReturnCursor(0, originCursor, default) && originCursor.IsEmpty &&
            originBag.Slots[0].amount == 9, "whole-stack return failed");
        Require(originBag.TryClickSlot(0, originCursor, true) && originBag.TryAdd("stone", 1),
            "partial return fixture failed");
        remainder = originBag.Slots[0];
        Require(originBag.TryClickSlot(1, originCursor, false) &&
            !originBag.TryReturnCursor(0, originCursor, remainder) && originCursor.Count("stone") == 1,
            "unrelated origin stack overwritten");
        Debug.Log("[Nyangbingo] Inventory click/drop regression passed.");
    }

    [MenuItem("Nyangbingo/Run Production Goal Guide Regression")]
    public static void RunProductionGoalGuideRegression()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<GameDataCatalog>(CatalogPath);
        var bag = new Nyangbingo.Inventory.Inventory(catalog.FindItem);
        var furnace = catalog.Recipes.First(r => r.Output.item?.Id == "furnace");
        string Target(string id, int count = 1) =>
            Nyangbingo.World.ProductionGoalGuide.Resolve(catalog, bag, id, count, _ => true);
        Require(Target("furnace") == "nearest:" + furnace.Ingredients[0].item.Id,
            "missing furnace material not selected");
        foreach (var ingredient in furnace.Ingredients)
            Require(bag.TryAdd(ingredient.item.Id, ingredient.amount), "goal material fixture failed");
        Require(Target("furnace") == "station:workbench", "ready furnace still points to ore");
        Require(bag.TryRemove("copper_ore", 1) && Target("furnace") == "nearest:copper_ore",
            "new shortage did not replace station target");
        Require(bag.TryAdd("copper_ore", 1) && bag.TryAdd("furnace", 1) && Target("furnace") == null,
            "owned installation still requests materials");
        var smelt = catalog.Smelting.First(r => r.Output.item?.Id == "iron_ingot");
        var oreBag = new Nyangbingo.Inventory.Inventory(catalog.FindItem);
        Require(oreBag.TryAdd(smelt.Input.item.Id, smelt.Input.amount * 2), "smelt fixture failed");
        Require(Nyangbingo.World.ProductionGoalGuide.Resolve(catalog, oreBag, "iron_ingot",
            smelt.Output.amount * 2, _ => true) == "nearest:" + smelt.Fuel.item.Id,
            "smelting ignored missing fuel");
        Require(oreBag.TryAdd(smelt.Fuel.item.Id, smelt.Fuel.amount * 2) &&
            Nyangbingo.World.ProductionGoalGuide.Resolve(catalog, oreBag, "iron_ingot",
                smelt.Output.amount * 2, _ => true) == "station:furnace",
            "ready smelting did not point to furnace");
        foreach (var id in new[] { "ice_core", "iron_claw", "ice_anvil", "icesteel_claw", "workbench" })
        {
            var inventory = new Nyangbingo.Inventory.Inventory(catalog.FindItem);
            var recipe = catalog.Recipes.First(r => r.Output.item?.Id == id);
            foreach (var ingredient in recipe.Ingredients)
                Require(inventory.TryAdd(ingredient.item.Id, ingredient.amount), "multi-goal fixture failed");
            var expected = recipe.Station switch
            {
                Nyangbingo.Core.CraftingStation.Furnace => "station:furnace",
                Nyangbingo.Core.CraftingStation.Foundry => "station:blast_furnace",
                Nyangbingo.Core.CraftingStation.IceAnvil => "station:ice_anvil", _ => null
            };
            Require(Nyangbingo.World.ProductionGoalGuide.Resolve(catalog, inventory, id, 1, _ => true)
                == expected, "wrong station for production goal " + id);
        }
        Debug.Log("[Nyangbingo] Production goal guide regression passed.");
    }

    [MenuItem("Nyangbingo/Run Inventory Equipment Ownership Regression")]
    public static void RunInventoryEquipmentOwnershipRegression()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<GameDataCatalog>(CatalogPath);
        var inventory = new Nyangbingo.Inventory.Inventory(catalog.FindItem);
        var cursor = new Nyangbingo.Inventory.Inventory(catalog.FindItem, 1);
        var storage = new Nyangbingo.Inventory.Inventory(catalog.FindItem, 2);
        var collection = new Nyangbingo.Inventory.EquipmentCollection(catalog.FindEquipment, inventory, cursor);
        var equipment = catalog.Equipment.First(e => e != null && catalog.FindItem(e.Id) != null);
        Require(collection.TryAdd(equipment) && collection.Contains(equipment.Id) &&
            inventory.Count(equipment.Id) == 1, "equipment not represented in inventory");
        var slot = inventory.Export().FindIndex(i => i.itemId == equipment.Id);
        Require(inventory.TryClickSlot(slot, cursor, false) && collection.Contains(equipment.Id),
            "cursor pickup lost ownership");
        Require(storage.TryClickSlot(0, cursor, false) && !collection.Contains(equipment.Id),
            "stored equipment remains player-owned");
        Require(storage.TryTransferSlotTo(0, inventory) && collection.Contains(equipment.Id),
            "storage retrieval failed to restore ownership");
        var active = new Nyangbingo.Inventory.ActiveSlotSystem(inventory, catalog.FindItem, keepInInventory: true);
        Require(inventory.TryAdd("dokkaebi_club", 2), "weapon fixture failed");
        Require(active.TryEquip("dokkaebi_club") && inventory.Count("dokkaebi_club") == 2 &&
            active.TryUnequip() && inventory.Count("dokkaebi_club") == 2,
            "equipment toggle consumed or duplicated inventory items");
        Debug.Log("[Nyangbingo] Inventory equipment ownership regression passed.");
    }

    [MenuItem("Nyangbingo/Run Seolhanpung Sunlight Immunity Regression")]
    public static void RunSeolhanpungSunlightImmunityRegression()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<GameDataCatalog>(CatalogPath);
        Require(Nyangbingo.Inventory.EquipmentColdPenaltyRules.TryCreate(catalog, out var cold),
            "cold penalty fixture missing");
        var equipped = new Nyangbingo.Inventory.EquipmentSystem();
        var stats = new Nyangbingo.Inventory.StatSheet();
        Require(!Nyangbingo.Inventory.ArmorSetRules.GrantsSunlightImmunity(equipped, 20, cold),
            "bare player is immune");
        foreach (var id in new[] { "icesteel_helm", "icesteel_armor", "icesteel_boots" })
            Require(equipped.TryEquip(catalog.FindEquipment(id)), "set fixture missing");
        stats.Recalculate(equipped, 20, cold);
        Require(stats.SunlightImmune &&
            Mathf.Approximately(stats.FireDamageModifier, -.25f) &&
            Mathf.Approximately(stats.TemperatureRiseModifier, -.20f),
            "complete set immunity or existing modifiers failed");
        Require(Nyangbingo.Inventory.ArmorSetRules.GrantsSunlightImmunity(equipped, -10, cold) &&
            !Nyangbingo.Inventory.ArmorSetRules.GrantsSunlightImmunity(equipped, -11, cold),
            "immunity ignored existing set cold tolerance");
        Require(equipped.TryUnequip(Nyangbingo.Core.EquipmentSlot.Feet) &&
            !Nyangbingo.Inventory.ArmorSetRules.GrantsSunlightImmunity(equipped, 20, cold),
            "partial set is immune");
        Require(equipped.TryEquip(catalog.FindEquipment("iron_boots")) &&
            !Nyangbingo.Inventory.ArmorSetRules.GrantsSunlightImmunity(equipped, 20, cold),
            "mixed set is immune");
        stats.Recalculate(equipped, 20, cold);
        Require(!stats.SunlightImmune, "stat recalculation retained stale immunity");
        Debug.Log("[Nyangbingo] Seolhanpung sunlight immunity regression passed.");
    }

    [Serializable]
    private sealed class InventorySnapshot
    {
        public List<Nyangbingo.Inventory.InventorySlot> slots;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
