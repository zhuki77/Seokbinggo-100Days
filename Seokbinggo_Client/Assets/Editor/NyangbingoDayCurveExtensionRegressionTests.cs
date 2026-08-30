using Nyangbingo.Data;
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
