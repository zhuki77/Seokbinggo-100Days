using System;
using System.Collections.Generic;
using System.Linq;
using Nyangbingo.Crafting;
using Nyangbingo.Inventory;
using Nyangbingo.World;
using UnityEditor;
using UnityEngine;

/// <summary>P4 FrostSpread / EvolutionCraft / GimmickWeapon / InsulationPanels 계약 회귀.</summary>
public static class NyangbingoP4RegressionTests
{
    [MenuItem("Nyangbingo/Run P4 Regression Tests")]
    public static void RunAll()
    {
        try
        {
            Require(!EvolutionCraft.IsSmithyUnlocked(3), "smithy locked at stage 3");
            Require(EvolutionCraft.IsSmithyUnlocked(4), "smithy unlocked at stage 4");
            Require(EvolutionCraft.IsSmithyUnlocked(4) ==
                    SeokbinggoRules.IsSmithyUnlocked(4), "EvolutionCraft matches SeokbinggoRules");

            Require(EvolutionCraft.IsEvolutionRecipe("claw_t3", new[] { "claw_t3", "ice_steel_ingot" }),
                "evolution recipe: first material == source");
            Require(!EvolutionCraft.IsEvolutionRecipe("claw_t3", new[] { "ice_steel_ingot", "claw_t3" }),
                "non-evolution when source is not first material");

            var materials = new List<(string id, int count)>
            {
                ("claw_t3", 1),
                ("ice_steel_ingot", 2)
            };
            var bag = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["claw_t3"] = 1,
                ["ice_steel_ingot"] = 2
            };
            Require(EvolutionCraft.CanEvolve("claw_t3", materials,
                    (id, count) => bag.TryGetValue(id, out var have) && have >= count),
                "CanEvolve when materials present");
            string granted = null;
            Require(EvolutionCraft.TryEvolve("claw_t3", materials,
                    (id, count) => bag.TryGetValue(id, out var have) && have >= count,
                    (id, count) =>
                    {
                        if (!bag.TryGetValue(id, out var have) || have < count) return false;
                        bag[id] = have - count;
                        return true;
                    },
                    id => granted = id,
                    "claw_t4"),
                "TryEvolve consumes materials and grants result");
            Require(granted == "claw_t4", "TryEvolve result id");
            Require(bag["claw_t3"] == 0 && bag["ice_steel_ingot"] == 0, "TryEvolve emptied bag");

            var sixTiers = Enumerable.Repeat(1, 6).ToList();
            var sevenTiers = Enumerable.Repeat(1, 7).ToList();
            var capped = InsulationPanels.Total(sixTiers, null);
            var overCap = InsulationPanels.Total(sevenTiers, null);
            Require(Mathf.Approximately(capped, overCap), "InsulationPanels.Total respects 6-piece cap");
            Require(Mathf.Approximately(capped, Mathf.Clamp01(6f * InsulationPanels.DefaultStrawBonus)),
                "InsulationPanels six tier-1 sum");
            Require(InsulationPanels.TierForDefinition("clay_plaster") == 2, "clay plaster tier 2");
            Require(InsulationPanels.TierForDefinition("straw_insul") == 1, "straw insul tier 1");

            var band1 = FrostSpreadService.BandForClear(1);
            Require(band1.MinDepth == 91 && band1.MaxDepth == 135, "BandForClear(1) deep band");
            var band2 = FrostSpreadService.BandForClear(2);
            Require(band2.MinDepth == 46 && band2.MaxDepth == 135, "BandForClear(2) mid band");
            var band3 = FrostSpreadService.BandForClear(3);
            Require(band3.MinDepth == 136 && band3.MaxDepth == 140, "BandForClear(3) bedrock band");
            FrostSpreadService.UnsealBedrockLayer(null);

            var frost = new FrostSpreadService();
            frost.OnAltarClear(1);
            Require(frost.AltarClears == 1, "OnAltarClear sets stage 1");
            frost.MarkPending(new Vector2Int(3, 7));
            Require(frost.IsPending(new Vector2Int(3, 7)), "pending cell marked");
            Require(frost.TryLazyReveal(new Vector2Int(3, 7), true, out var ore) &&
                    ore == "copper_ore", "stage1 lazy reveal copper");
            Require(!frost.IsPending(new Vector2Int(3, 7)), "pending cleared after reveal");

            Require(Mathf.Approximately(GimmickWeapon.ScaleDamage(10f), 11f), "gimmick bonus 1.10");
            Require(Mathf.Approximately(GimmickWeapon.ScaleDamage(10f, 0f), 11f),
                "invalid bonus falls back to default");
            var progress = new GimmickWeaponProgress();
            Require(progress.TryGrant(GimmickWeaponProgress.FirstFrostClawId), "first grant succeeds");
            Require(progress.HasGranted(GimmickWeaponProgress.FirstFrostClawId), "granted tracked");
            Require(!progress.TryGrant(GimmickWeaponProgress.FirstFrostClawId), "second grant blocked");
            progress.NotifyBaekjungSurvived();
            Require(progress.HasGranted(GimmickWeaponProgress.BaekjungBundleId), "baekjung grant");
            progress.NotifyImugiCleared();
            Require(progress.HasGranted(GimmickWeaponProgress.YeouijuClawId), "imugi grant");

            Debug.Log("[Nyangbingo] P4 regression tests passed.");
        }
        catch (Exception exception)
        {
            Debug.LogError($"[Nyangbingo] P4 regression failed: {exception.Message}");
            throw;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException($"[Nyangbingo] P4 regression failed: {message}");
    }
}
