using System;
using System.IO;
using Nyangbingo.Data;
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

        Require(Mathf.Approximately(FrostSpreadService.CalculateBandFromNorm(1, 10), .9f) &&
                Mathf.Approximately(FrostSpreadService.CalculateBandFromNorm(5, 10), .5f) &&
                Mathf.Approximately(FrostSpreadService.CalculateBandFromNorm(10, 10), 0f),
            "frost normalized band formula mismatch");
        var frost = new FrostSpreadService(catalog);
        for (var index = 0; index < 10; index++)
            Require(frost.OnAltarBossClear(), $"altar clear {index + 1} did not advance");
        Require(frost.AltarClears == 10 && frost.StepCount == 10 &&
                Mathf.Approximately(frost.BandFromNorm, 0f) && !frost.OnAltarBossClear(),
            "frost clear count must clamp monotonically at 10");
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
            frostPendingCells = frost.ExportPendingCells()
        };
        var roundTrip = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(save));
        roundTrip.NormalizeAfterLoad();
        Require(roundTrip.heatStage == 3 && roundTrip.altarClears == 10,
            "heat/frost save round-trip mismatch");
        Require(heat.Restore(roundTrip.heatStage) && heat.Current == 3,
            "heat stage restore mismatch");
        var restoredFrost = new FrostSpreadService(catalog);
        Require(restoredFrost.RestoreAltarClears(roundTrip.altarClears),
            "frost clear restore mismatch");
        restoredFrost.RestorePendingCells(roundTrip.frostPendingCells);
        Require(restoredFrost.AltarClears == 10, "restored frost clear count mismatch");

        Debug.Log("[Nyangbingo] v72 progression regression passed: named heat 1/2/3, " +
                  "day fire 1.5/4.5/15, frost 10-step normalized band, permanent bedrock, save round-trip.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException($"[Nyangbingo] v72 progression regression failed: {message}");
    }
}
