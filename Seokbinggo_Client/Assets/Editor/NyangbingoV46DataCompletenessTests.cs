using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>v46 P0 CSV 행수·핵심 키 존재 여부를 로컬 CSV 기준으로 검증한다. v72 이후 night-waves.csv는 삭제된 것이 정상이다.</summary>
public static class NyangbingoV46DataCompletenessTests
{
    private static string CsvDirectory =>
        Path.Combine(Application.dataPath, "Data", "CSV");

    private static readonly (string file, int minimumRows)[] ExpectedRowCounts =
    {
        ("items.csv", 160),
        ("crafting-tree.csv", 90),
        ("bosses.csv", 10),
        ("accessories.csv", 26),
        ("equipment.csv", 44),
        ("globals.csv", 110),
        ("drops.csv", 17),
        ("player-combat.csv", 19),
        ("yokai-stats.csv", 7),
        ("modules.csv", 11),
        ("day-curve.csv", 30),
        ("seal-whitelist.csv", 23)
    };

    private static readonly string[] RequiredGlobalKeys =
    {
        "jukbuin_regen_mult",
        "daebal_flame_cut",
        "daebal_radius",
        "boss_dodge_sec_curve"
    };

    [MenuItem("Nyangbingo/Run V46 Data Completeness Tests")]
    public static void RunAll()
    {
        try
        {
            foreach (var expectation in ExpectedRowCounts)
            {
                var path = Path.Combine(CsvDirectory, expectation.file);
                Require(File.Exists(path), $"missing CSV: {expectation.file}");
                var mergeNote = string.Equals(expectation.file, "globals.csv", StringComparison.Ordinal);
                var rows = NyangbingoCsvUtility.ReadRows(path, mergeNote);
                Require(rows.Count >= expectation.minimumRows,
                    $"{expectation.file} has {rows.Count} rows (need >= {expectation.minimumRows})");
            }

            var globalsPath = Path.Combine(CsvDirectory, "globals.csv");
            var globals = NyangbingoCsvUtility.ReadRows(globalsPath, mergeUnquotedTrailingNote: true);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in globals)
            {
                if (row.TryGetValue("key", out var key) && !string.IsNullOrWhiteSpace(key))
                    keys.Add(key);
            }
            foreach (var requiredKey in RequiredGlobalKeys)
                Require(keys.Contains(requiredKey), $"globals.csv missing key '{requiredKey}'");

            var legacyNightWaves = Path.Combine(CsvDirectory, "night-waves.csv");
            Require(!File.Exists(legacyNightWaves),
                "night-waves.csv must stay deleted in v72 (wave night uses day-curve + invasion globals)");

            Debug.Log("[Nyangbingo] V46 data completeness tests passed.");
        }
        catch (Exception exception)
        {
            Debug.LogError($"[Nyangbingo] V46 data completeness failed: {exception.Message}");
            throw;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException($"[Nyangbingo] V46 data completeness failed: {message}");
    }
}
