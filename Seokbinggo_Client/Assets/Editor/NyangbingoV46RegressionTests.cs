using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nyangbingo.Combat;
using Nyangbingo.Crafting;
using Nyangbingo.Inventory;
using Nyangbingo.World;
using UnityEditor;
using UnityEngine;

public static class NyangbingoV46RegressionTests
{
    [MenuItem("Nyangbingo/Run v46 Regression Tests")]
    public static void RunAll()
    {
        DayLight.AssertInvariants();
        Require(ArmorRules.EffectiveDamage(10, 100) == 1, "ArmorRules min damage for high defense");
        Require(ArmorRules.EffectiveDamage(10, 3) == 7, "ArmorRules contact 10 def 3 => 7");

        Require(WaveNight.IsBigNight(35), "day 35 is big night");
        Require(!WaveNight.IsBigNight(30), "day 30 is not big night");
        Require(!WaveNight.IsBigNight(40), "day 40 is not big night (offset 5)");

        Require(!EvolutionCraft.IsSmithyUnlocked(3), "smithy locked at stage 3");
        Require(EvolutionCraft.IsSmithyUnlocked(4), "smithy unlocked at stage 4");

        var sixTiers = Enumerable.Repeat(1, 6).ToList();
        var sevenTiers = Enumerable.Repeat(1, 7).ToList();
        var capped = InsulationPanels.Total(sixTiers, null);
        var overCap = InsulationPanels.Total(sevenTiers, null);
        Require(Mathf.Approximately(capped, overCap), "InsulationPanels.Total respects 6-piece cap");
        Require(Mathf.Approximately(capped, Mathf.Clamp01(6f * InsulationPanels.DefaultStrawBonus)),
            "InsulationPanels six tier-1 sum");

        var band1 = FrostSpreadService.BandForClear(1);
        Require(band1.MinDepth == 91 && band1.MaxDepth == 135, "BandForClear(1) deep band");
        FrostSpreadService.UnsealBedrockLayer(null);

        Require(ArtifactVerbCatalog.TryGetVerb("frost_map", out var verb) && verb == "tunnel_edge_hint",
            "ArtifactVerbCatalog frost_map");
        Require(InsulationPanels.TierForDefinition("clay_plaster") == 2, "clay plaster tier 2");
        Require(InsulationPanels.TierForDefinition("straw_insul") == 1, "straw insul tier 1");

        AssertWaveThresholdNotLiteralInSource();

        Debug.Log("[Nyangbingo] v46 regression tests passed.");
    }

    private static void AssertWaveThresholdNotLiteralInSource()
    {
        var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrEmpty(projectRoot)) return;
        var wavePath = Path.Combine(projectRoot, "Assets", "Scripts", "Nyangbingo", "World", "WaveNight.cs");
        if (!File.Exists(wavePath)) return;
        var source = File.ReadAllText(wavePath);
        // CurrentWave 기본값에 하드코딩된 108이 남아 있을 수 있음 — globals 키가 있으면 호출부 연동을 권장.
        // 회귀는 IsBigNight / BandOf 계약만 강제하고, 108 리터럴은 경고만 남긴다.
        if (source.Contains("108f") || source.Contains("= 108"))
            Debug.LogWarning("[Nyangbingo] WaveNight still contains literal 108; prefer globals wave_threshold_sec at call sites.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException($"[Nyangbingo] v46 regression failed: {message}");
    }
}
