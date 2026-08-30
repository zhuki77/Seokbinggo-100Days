using Nyangbingo.Data;
using Nyangbingo.UI;
using Nyangbingo.World;
using UnityEditor;
using UnityEngine;

/// <summary>31~100일 day-curve-ext 앵커와 데모 이후 야간 스폰 계약.</summary>
public static class NyangbingoDayCurveExtensionRegressionTests
{
    private const string CatalogPath = "Assets/Data/SO/GameDataCatalog.asset";

    [MenuItem("Nyangbingo/Run Day Curve Extension Regression Tests")]
    public static void RunAll()
    {
        DayCurveExtensionResolver.ClearCache();
        var catalog = AssetDatabase.LoadAssetAtPath<GameDataCatalog>(CatalogPath);
        Require(catalog != null, "GameDataCatalog missing");
        Require(catalog.DayCurves.Count == 30, "MVP day curves must remain 30");
        Require(catalog.DayCurveExtensions.Count == 15, "day-curve-ext anchors must be 15");

        var day31 = catalog.FindDayCurve(31);
        var day33 = catalog.FindDayCurve(33);
        var day100 = catalog.FindDayCurve(100);
        Require(day31 != null && day33 != null && day100 != null,
            "FindDayCurve must resolve days 31~100 via extension anchors");
        Require(day31.NightYokaiCount == 8 && day31.MaxActive == 8 &&
                day31.EffectiveSpawnCount == 8,
            "day 31 must spawn eight yokai from extension composition");
        Require(Mathf.Approximately(day31.YokaiWallDamage, 18f) &&
                Mathf.Approximately(day100.YokaiWallDamage, 36f),
            "extension wall damage must follow day-curve-ext anchors");
        Require(day33.Day == 33 && Mathf.Approximately(day33.YokaiWallDamage, 18f),
            "inter-anchor days must inherit the previous anchor");
        Require(Mathf.Approximately(day31.VariantMultiplier, 1.25f) &&
                Mathf.Approximately(day45.VariantMultiplier, 1.5f) &&
                Mathf.Approximately(day100.VariantMultiplier, 2.75f),
            "variant_mult must follow day-curve-ext anchors");
        Require(Mathf.Approximately(day31.HeatSeepPercent, 15f) &&
                Mathf.Approximately(day45.HeatSeepPercent, 30f) &&
                Mathf.Approximately(day100.HeatSeepPercent, 100f),
            "heat_seep_pct must follow day-curve-ext anchors");
        Require(DayCurveCombatRules.UsesVariantHpMultiplier(catalog),
            "wave_mult_target must route variant_mult to HP only");
        Require(DayCurveCombatRules.ResolveYokaiHitPoints(catalog, day45, 100) == 150,
            "variant_mult must scale yokai HP on extension days");
        Require(Mathf.Approximately(
                DayCurveCombatRules.ApplyHeatSeepPenalty(1f, catalog, day45), 0.7f),
            "heat_seep must reduce recovery multiplier on extension days");
        Require(Mathf.Approximately(
                DayCurveCombatRules.ApplyHeatSeepPenalty(1f, catalog, catalog.FindDayCurve(30)), 1f),
            "heat_seep must not affect MVP days");
        Require(!GameShellController.ShouldEndDemoAtDay(31) &&
                !GameShellController.ShouldEndDemoAtDay(100),
            "date alone must not end the demo after day 30");

        Debug.Log("[Nyangbingo] Day curve extension regression passed.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new System.InvalidOperationException($"[Nyangbingo] Day curve extension regression failed: {message}");
    }
}
