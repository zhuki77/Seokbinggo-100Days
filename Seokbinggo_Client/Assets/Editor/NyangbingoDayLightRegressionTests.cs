using System;
using System.Reflection;
using Nyangbingo.Data;
using Nyangbingo.World;
using UnityEditor;
using UnityEngine;

/// <summary>P3 DayLight 계약 회귀 — 밤/지하 상수 분리 + stage 커브.</summary>
public static class NyangbingoDayLightRegressionTests
{
    private const BindingFlags InstanceFields =
        BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Nyangbingo/Run DayLight Regression Tests")]
    public static void RunAll()
    {
        try
        {
            DayLight.AssertInvariants();

            Require(Mathf.Approximately(DayLight.IntensityFor(1, false, true, false, null), 0.55f),
                "night intensity constant");
            Require(Mathf.Approximately(DayLight.IntensityFor(1, false, true, true, null), 0.55f),
                "baekjung intensity constant");
            Require(Mathf.Approximately(DayLight.IntensityFor(1, true, false, false, null), 0.35f),
                "underground intensity constant");
            Require(Mathf.Approximately(DayLight.IntensityFor(1, true, true, false, null), 1.00f),
                "day 1 stage1 brightness");
            Require(Mathf.Approximately(DayLight.IntensityFor(11, true, true, false, null), 1.05f),
                "day 11 stage2 brightness");
            Require(Mathf.Approximately(DayLight.IntensityFor(100, true, true, false, null), 1.45f),
                "day 100 stage10 brightness");
            Require(Mathf.Approximately(DayLight.IntensityFor(100, false, true, false, null), DayLight.NightIntensity),
                "late-day night still uses night constant");

            var settings = BuildBrightnessSettings("1.00/2.00/3.00/4.00/5.00/6.00/7.00/8.00/9.00/10.00");
            Require(settings != null && settings.IsValid, "test GlobalSettings must be valid");
            Require(Mathf.Approximately(DayLight.IntensityFor(1, true, true, false, settings), 1f),
                "globals curve stage1");
            Require(Mathf.Approximately(DayLight.IntensityFor(95, true, true, false, settings), 10f),
                "globals curve stage10");

            Debug.Log("[Nyangbingo] DayLight regression tests passed.");
        }
        catch (Exception exception)
        {
            Debug.LogError($"[Nyangbingo] DayLight regression failed: {exception.Message}");
            throw;
        }
    }

    private static GlobalSettings BuildBrightnessSettings(string curve)
    {
        var brightness = ScriptableObject.CreateInstance<GlobalDefinition>();
        SetDefinitionFields(brightness, GlobalKeys.DayBrightnessByStage, curve, "curve");

        var period = ScriptableObject.CreateInstance<GlobalDefinition>();
        SetDefinitionFields(period, GlobalKeys.HeatStagePeriod, "10", "day");

        return new GlobalSettings(new[] { brightness, period });
    }

    private static void SetDefinitionFields(GlobalDefinition definition, string key, string value, string unit)
    {
        var type = typeof(GlobalDefinition);
        type.GetField("key", InstanceFields)?.SetValue(definition, key);
        type.GetField("value", InstanceFields)?.SetValue(definition, value);
        type.GetField("unit", InstanceFields)?.SetValue(definition, unit);
        type.GetField("note", InstanceFields)?.SetValue(definition, "daylight-regression");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException($"[Nyangbingo] DayLight regression failed: {message}");
    }
}
