using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nyangbingo.Data;
using Nyangbingo.World;
using UnityEditor;
using UnityEngine;

public static class NyangbingoV72LegacyRemovalRegressionTests
{
    private const string GlobalsPath = "Assets/Data/CSV/globals.csv";
    private const string CatalogPath = "Assets/Data/SO/GameDataCatalog.asset";

    private static readonly string[] RemovedGlobalKeys =
    {
        "cold_source_required",
        "cold_cap_muldanji",
        "cold_cap_icejar",
        "cold_cap_icestorage",
        "cold_cap_frostcooler",
        "win_seal_pct",
        "night_wave_table",
        "wave_night_period",
        "wave_night_offset",
        "wave_advance_sec",
        "sun_scale_ties_dcounter",
        "tree_decay_by_day",
        "day_surface_reach_tiles_s10",
        "shade_gate_stage"
    };

    [MenuItem("Nyangbingo/Run v72 Legacy Removal Regression")]
    public static void RunAll()
    {
        var globals = ReadGlobals();
        foreach (var removedKey in RemovedGlobalKeys)
            Require(!globals.ContainsKey(removedKey), $"removed global remains: {removedKey}");
        Require(globals.TryGetValue(GlobalKeys.HeatStagePeriod, out var heatPeriod) && heatPeriod == "0",
            "heat_stage_period must remain as disabled value 0");
        Require(globals.TryGetValue(GlobalKeys.WaveMode, out var waveMode) && waveMode == "off",
            "wave_mode must be off");
        Require(globals.TryGetValue(GlobalKeys.InvasionPeriodDays, out var invasionPeriod) &&
                invasionPeriod == "10", "invasion period must be 10 days");
        Require(globals.TryGetValue(GlobalKeys.InvasionOffsetDays, out var invasionOffset) &&
                invasionOffset == "6", "invasion offset must be 6 days");
        Require(globals.TryGetValue("wave_mult_target", out var waveMultTarget) &&
                waveMultTarget == "hp_only", "v79 variant multiplier target must remain hp_only");
        Require(globals.TryGetValue(GlobalKeys.SunScaleTiesHeatStage, out var sunScale) && sunScale == "1",
            "sun scale must follow named heat stage");

        Require(!File.Exists("Assets/Data/CSV/night-waves.csv"), "night-waves.csv must be deleted");
        Require(!File.Exists("Assets/Scripts/Nyangbingo/World/WaveNightController.cs"),
            "WaveNightController must be deleted");
        Require(!File.Exists("Assets/Scripts/Nyangbingo/World/WaveNightRules.cs"),
            "WaveNightRules must be deleted");

        var catalog = AssetDatabase.LoadAssetAtPath<GameDataCatalog>(CatalogPath);
        Require(catalog != null, "GameDataCatalog asset is missing");
        foreach (var removedKey in RemovedGlobalKeys)
            Require(catalog.FindGlobal(removedKey) == null, $"catalog still contains removed global: {removedKey}");
        Require(catalog.FindGlobal(GlobalKeys.WaveMode)?.Value == "off", "catalog wave_mode mismatch");
        Require(catalog.FindGlobal(GlobalKeys.InvasionPeriodDays)?.Value == "10",
            "catalog invasion period mismatch");
        Require(catalog.FindGlobal(GlobalKeys.InvasionOffsetDays)?.Value == "6",
            "catalog invasion offset mismatch");
        Require(catalog.FindGlobal("wave_mult_target")?.Value == "hp_only",
            "catalog variant multiplier target mismatch");

        Require(catalog.DayCurves.Count == 30, "30-day demo curve count changed");
        Require(catalog.DayCurves.All(curve => curve != null && curve.MaxActive > 0),
            "night max_active must survive wave removal");
        Require(Mathf.Approximately(DayLight.IntensityFor(1, true, true, false, null), 1f) &&
                Mathf.Approximately(DayLight.IntensityFor(2, true, true, false, null), 1.28f) &&
                Mathf.Approximately(DayLight.IntensityFor(3, true, true, false, null), 1.55f),
            "daylight must be driven directly by named heat stage");

        Debug.Log("[Nyangbingo] v72 legacy removal regression passed: wave/cap/date branches removed; " +
                  "invasion 10/6 and max_active preserved.");
    }

    private static Dictionary<string, string> ReadGlobals()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var lines = File.ReadAllLines(GlobalsPath);
        for (var index = 1; index < lines.Length; index++)
        {
            if (string.IsNullOrWhiteSpace(lines[index])) continue;
            var fields = lines[index].Split(new[] { ',' }, 3);
            Require(fields.Length >= 2, $"invalid globals row {index + 1}");
            Require(result.TryAdd(fields[0].Trim(), fields[1].Trim()),
                $"duplicate globals key: {fields[0].Trim()}");
        }
        return result;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException($"[Nyangbingo] v72 legacy removal regression failed: {message}");
    }
}
