using System;
using Nyangbingo.Bosses;
using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using UnityEditor;
using UnityEngine;

/// <summary>P5 아티팩트 A안 · AccessoryTwo · T6 한파 세트 계약 회귀.</summary>
public static class NyangbingoP5RegressionTests
{
    [MenuItem("Nyangbingo/Run P5 Regression Tests")]
    public static void RunAll()
    {
        try
        {
            Require((int)EquipmentSlot.AccessoryTwo == 4, "AccessoryTwo must remain enum value 4");
            Require(ArtifactVerbCatalog.All.Count == 20, "artifact catalog has 20 verbs");
            Require(ArtifactVerbCatalog.TryGetVerb("frost_map", out var frostVerb) &&
                    frostVerb == ArtifactVerbId.ShowDugPaths, "frost_map → ShowDugPaths");
            Require(ArtifactVerbCatalog.TryGetLegacyVerbKey("frost_map", out var legacy) &&
                    legacy == "tunnel_edge_hint", "legacy snake key for frost_map");
            Require(ArtifactVerbParsing.ParseVerb("WalkWhileCharging") == ArtifactVerbId.WalkWhileCharging,
                "ParseVerb PascalCase");
            Require(ArtifactVerbParsing.ParseVerb("") == ArtifactVerbId.None, "empty verb → None");
            Require(ArtifactVerbParsing.ParseActivation("DaySurface") ==
                    ArtifactActivationCondition.DaySurface, "ParseActivation DaySurface");
            Require(ArtifactVerbParsing.ParseActivation("None") == ArtifactActivationCondition.None,
                "ParseActivation None");

            Require(ArtifactRules.IsActivationMet(ArtifactActivationCondition.None, false, false, false),
                "None always met");
            Require(ArtifactRules.IsActivationMet(ArtifactActivationCondition.Deep, true, false, true),
                "Deep when deep");
            Require(!ArtifactRules.IsActivationMet(ArtifactActivationCondition.DaySurface, false, true, false),
                "DaySurface requires day");
            Require(ArtifactRules.IsActivationMet(ArtifactActivationCondition.DaySurface, false, true, true),
                "DaySurface when day+surface");

            var frost = EquipmentDefinition.CreateRuntime("frost_map", EquipmentSlot.AccessoryOne, true,
                artifactVerbId: "ShowDugPaths");
            var gear = new EquipmentSystem();
            Require(gear.TryEquipAccessory(frost, 0), "equip artifact into accessory one");
            Require(ArtifactRules.HasVerb(gear, ArtifactVerbId.ShowDugPaths), "HasVerb ShowDugPaths");
            Require(!ArtifactRules.HasVerb(gear, ArtifactVerbId.GrabKnockedTarget), "missing verb false");

            var mask = EquipmentDefinition.CreateRuntime("yeongno_mask", EquipmentSlot.AccessoryTwo, true,
                artifactVerbId: "EscapeOnSwallow", usageLimit: 1);
            Require(mask.UsageLimitPerDay == 1, "yeongno daily limit");
            Require(gear.TryEquipAccessory(mask, 1), "AccessoryTwo equip");
            Require(gear.Get(EquipmentSlot.AccessoryOne) != null &&
                    gear.Get(EquipmentSlot.AccessoryTwo) != null, "both accessory slots filled");

            Require(ArmorSetRules.IsKnownTopTierSet("seolhanpung") &&
                    ArmorSetRules.IsKnownTopTierSet("hanpa"), "known top-tier sets");
            Require(!ArmorSetRules.IsKnownTopTierSet("seonge"), "T4 has no set");

            var helm = EquipmentDefinition.CreateRuntime("cold_wave_helm", EquipmentSlot.Head, false,
                itemDefense: 6, equipmentSetId: ArmorSetRules.HanpaSetId,
                setTemperatureModifier: ArmorSetRules.HanpaTemperatureRise,
                setFireModifier: ArmorSetRules.HanpaFireDamage);
            var body = EquipmentDefinition.CreateRuntime("cold_wave_armor", EquipmentSlot.Body, false,
                itemDefense: 7, equipmentSetId: ArmorSetRules.HanpaSetId,
                setTemperatureModifier: ArmorSetRules.HanpaTemperatureRise,
                setFireModifier: ArmorSetRules.HanpaFireDamage);
            var boots = EquipmentDefinition.CreateRuntime("cold_wave_boots", EquipmentSlot.Feet, false,
                itemDefense: 6, equipmentSetId: ArmorSetRules.HanpaSetId,
                setTemperatureModifier: ArmorSetRules.HanpaTemperatureRise,
                setFireModifier: ArmorSetRules.HanpaFireDamage);
            Require(ArmorSetRules.MatchesCanonicalBonuses(helm), "hanpa helm canonical");

            var armor = new EquipmentSystem();
            Require(armor.TryEquip(helm) && armor.TryEquip(body) && armor.TryEquip(boots), "equip hanpa set");
            var sheet = new StatSheet();
            sheet.Recalculate(armor);
            Require(sheet.Defense == 19, "hanpa defense sum 6+7+6");
            Require(Mathf.Approximately(sheet.TemperatureRiseModifier, ArmorSetRules.HanpaTemperatureRise),
                "hanpa set temp -0.40");
            Require(Mathf.Approximately(sheet.FireDamageModifier, ArmorSetRules.HanpaFireDamage),
                "hanpa set fire -0.45");

            var heart = EquipmentDefinition.CreateRuntime("ice_heart_norigae", EquipmentSlot.AccessoryOne, true,
                temperatureModifier: -0.15f);
            Require(armor.TryEquipAccessory(heart, 0), "equip ice heart with hanpa");
            sheet.Recalculate(armor);
            Require(Mathf.Approximately(sheet.TemperatureRiseModifier, -0.55f),
                "ice heart + hanpa reaches -0.55 floor");

            var verbs = new ArtifactVerbRuntime();
            var gearForVerbs = new EquipmentSystem();
            var oldKey = EquipmentDefinition.CreateRuntime("old_key", EquipmentSlot.AccessoryOne, true,
                artifactVerbId: "OpenStorageAnywhere", usageLimit: 3);
            Require(gearForVerbs.TryEquipAccessory(oldKey, 0), "old_key equip");
            var context = new ArtifactActivationContext(false, true, true);
            Require(verbs.AllowsRemoteJangdok(gearForVerbs, context), "remote jangdok first use");
            Require(verbs.DailyUseCount("old_key") == 1, "old_key daily use tracked");
            verbs.ResetDailyUses();
            Require(verbs.DailyUseCount("old_key") == 0, "artifact daily reset");

            var drought = EquipmentDefinition.CreateRuntime("drought_heart_shard", EquipmentSlot.AccessoryOne,
                true, artifactVerbId: "ReduceFlameAndHasten", activation: "DaySurface");
            var daySurfaceGear = new EquipmentSystem();
            Require(daySurfaceGear.TryEquipAccessory(drought, 0), "drought equip");
            var daySurfaceContext = new ArtifactActivationContext(false, true, true);
            Require(Mathf.Approximately(verbs.ResolveDaySurfaceMoveBonus(
                daySurfaceGear, daySurfaceContext), ArtifactVerbRuntime.DaySurfaceMoveBonus),
                "drought hasten bonus");
            Require(Mathf.Approximately(verbs.ResolveCoolerRadiusTiles(daySurfaceGear, daySurfaceContext),
                ArtifactVerbRuntime.CoolerBaseRadiusTiles), "cooler base radius");
            var yeouiju = EquipmentDefinition.CreateRuntime("yeouiju_shard", EquipmentSlot.AccessoryOne, true,
                artifactVerbId: "ExtendCoolerRadius");
            var coolerGear = new EquipmentSystem();
            Require(coolerGear.TryEquipAccessory(yeouiju, 0), "yeouiju equip");
            Require(Mathf.Approximately(verbs.ResolveCoolerRadiusTiles(coolerGear, daySurfaceContext),
                ArtifactVerbRuntime.CoolerExtendedRadiusTiles), "yeouiju cooler radius");
            Require(Mathf.Approximately(verbs.ResolveIceMeltMultiplier(coolerGear, daySurfaceContext),
                ArtifactVerbRuntime.IceMeltSlowMultiplier), "yeouiju ice melt half rate");
            var altar = EquipmentDefinition.CreateRuntime("altar_echo", EquipmentSlot.AccessoryOne, true,
                artifactVerbId: "ReduceOfferTears");
            var altarGear = new EquipmentSystem();
            Require(altarGear.TryEquipAccessory(altar, 0), "altar_echo equip");
            Require(verbs.ResolveAltarTearCost(altarGear, daySurfaceContext, 30) == 20,
                "altar tear 30→20");

            var itemArt = AssetDatabase.LoadAssetAtPath<ItemArtCatalog>(
                "Assets/Art/Items/ItemArtCatalog.asset");
            Require(itemArt != null, "ItemArtCatalog missing");
            foreach (var artifactId in ArtifactVerbCatalog.All.Keys)
            {
                var sprite = itemArt.FindSprite(artifactId);
                Require(sprite != null,
                    $"artifact inventory icon missing: {artifactId} (run Nyangbingo/Bind Delivered Art Catalogs)");
            }

            var catalog = AssetDatabase.LoadAssetAtPath<GameDataCatalog>(
                "Assets/Data/SO/GameDataCatalog.asset");
            Require(catalog != null && catalog.Bosses.Count >= 1, "catalog bosses loaded");
            Require(BossDodgeRules.TryGetOpeningDodgeSeconds(
                        catalog, "king_dokkaebi", out var dodgeSeconds) &&
                    Mathf.Approximately(dodgeSeconds, 20f),
                "king_dokkaebi opening dodge is 20 seconds");

            Debug.Log("[Nyangbingo] P5 regression tests passed.");
        }
        catch (Exception exception)
        {
            Debug.LogError($"[Nyangbingo] P5 regression failed: {exception.Message}");
            throw;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException($"[Nyangbingo] P5 regression failed: {message}");
    }
}
