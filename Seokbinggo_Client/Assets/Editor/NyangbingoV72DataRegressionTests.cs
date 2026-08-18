using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nyangbingo.Data;
using UnityEditor;
using UnityEngine;

public static class NyangbingoV72DataRegressionTests
{
    [MenuItem("Nyangbingo/Run v72 Data Regression")]
    public static void RunAll()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<GameDataCatalog>("Assets/Data/SO/GameDataCatalog.asset");
        Require(catalog != null && catalog.IsValid, "GameDataCatalog is missing or invalid");
        Require(catalog.Zones.Count == 10, $"zones count {catalog.Zones.Count}");
        Require(catalog.TerrainSpawns.Count == 70, $"terrain-spawn count {catalog.TerrainSpawns.Count}");
        Require(catalog.Talismans.Count == 5, $"talismans count {catalog.Talismans.Count}");
        Require(catalog.CodexEntries.Count == 17, $"codex count {catalog.CodexEntries.Count}");
        Require(catalog.Traits.Count == 4, $"traits count {catalog.Traits.Count}");
        Require(catalog.Crops.Count == 10, $"crops count {catalog.Crops.Count}");

        var zones = catalog.Zones.OrderBy(value => value.Order).ToArray();
        for (var i = 0; i < zones.Length; i++)
        {
            Require(zones[i].Order == i + 1 && zones[i].DistanceNormalizedFrom < zones[i].DistanceNormalizedTo,
                $"zone order/range {zones[i].Id}");
            if (i > 0) Require(Mathf.Approximately(zones[i - 1].DistanceNormalizedTo,
                zones[i].DistanceNormalizedFrom), $"zone normalized gap {zones[i].Id}");
        }
        Require(Mathf.Approximately(zones[0].DistanceNormalizedFrom, 0f) &&
                Mathf.Approximately(zones[zones.Length - 1].DistanceNormalizedTo, 1f),
            "zone normalized coverage 0..1");

        foreach (var group in catalog.TerrainSpawns.GroupBy(value => value.TerrainId))
        {
            Require(group.Count() == 7, $"terrain {group.Key} row count");
            var sum = group.Sum(value => value.Weight);
            Require(sum == 0 || sum == 100, $"terrain {group.Key} weight sum {sum}");
        }
        Require(catalog.TerrainSpawns.Select(value => value.TerrainId).Distinct().Count() == 10,
            "terrain table count");

        var dayRows = NyangbingoCsvUtility.ReadRows("Assets/Data/CSV/day-curve.csv");
        Require(dayRows.Count == 30 && !dayRows[0].ContainsKey("heat_stage") &&
                !dayRows[0].ContainsKey("day_fire_dmg_per_sec") &&
                !dayRows[0].ContainsKey("spawn_mult"), "day-curve v72 removed columns");
        var day15 = catalog.FindDayCurve(15);
        Require(day15 != null && day15.NightYokaiCount == 4 && day15.EffectiveSpawnCount == 12 &&
                Mathf.Approximately(day15.DropMultiplier, 3f) &&
                Mathf.Approximately(day15.SpawnMultiplier, 3f), "day 15 drop_mult migration");

        var club = catalog.FindYokai("club");
        var imugi = catalog.FindYokai("imugi");
        Require(club != null && Mathf.Approximately(club.AggroRadius, 8f) && club.BodyTiles == 1 &&
                !club.UsesArenaBody && !string.IsNullOrWhiteSpace(club.AggroNote), "club aggro/body schema");
        Require(imugi != null && Mathf.Approximately(imugi.AggroRadius, 0f) &&
                imugi.UsesArenaBody && imugi.BodyTiles == 0, "imugi arena body schema");

        var brightness = catalog.FindGlobal(GlobalKeys.DayBrightnessByStage)?.Value?.Split('/');
        Require(catalog.FindGlobal(GlobalKeys.HeatStageCount)?.Value == "3" &&
                brightness != null && brightness.Length == 3, "3-stage brightness schema");

        Require(catalog.FindTalisman("tal_frost")?.Materials.Count == 2,
            "frost talisman material parse");
        Require(catalog.FindTrait("trait_ranged")?.StartItemId == "straw_sling",
            "starting trait data");
        Require(catalog.FindCrop("zone10:catnip")?.HealHitPoints == 40,
            "zone10 crop data");
        Require(NyangbingoCsvUtility.ReadRows("Assets/Data/CSV/content-status.csv").Count == 858,
            "content-status editor reference row count");
        Require(catalog.Globals.Count == 253 && catalog.IdMigrations.Count == 28 &&
                catalog.FindGlobal("wave_mult_target")?.Value == "hp_only",
            "v79 globals and ID migration contract");
        ValidateBossSemanticGuard();

        Debug.Log("[Nyangbingo] v79 data regression passed: 10/70/5/17/4/10 + 858 rows + 253 globals + 28 migrations; " +
                  "malformed boss combat columns rejected.");
    }

    private static void ValidateBossSemanticGuard()
    {
        var rows = NyangbingoCsvUtility.ReadRows("Assets/Data/CSV/bosses.csv");
        var king = rows.Single(row => row["id"] == "king_dokkaebi");
        NyangbingoV24DataValidator.ValidateBossCombatRow(king);

        var shifted = new Dictionary<string, string>(king, StringComparer.Ordinal)
        {
            ["tele_sec"] = "T1 rush 613 sec exceeds night 540 sec)",
            ["shape"] = "0.75",
            ["range_tiles"] = "Box",
            ["special_dmg_per_hit"] = string.Empty
        };
        var rejected = false;
        try
        {
            NyangbingoV24DataValidator.ValidateBossCombatRow(shifted);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Require(rejected, "shifted king_dokkaebi combat columns passed semantic validation");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("[Nyangbingo] v72 data regression failed: " + message);
    }
}
