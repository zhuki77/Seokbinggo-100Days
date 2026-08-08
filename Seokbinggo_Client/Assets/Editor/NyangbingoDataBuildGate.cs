using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Nyangbingo.Data;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 제품 빌드가 마지막으로 성공한 v34 CSV 임포트와 동일한 데이터에서 만들어지는지 검증한다.
/// CSV 원본이 바뀌거나 생성된 카탈로그가 불완전하면 재임포트 전까지 제품 빌드를 차단한다.
/// </summary>
public static class NyangbingoDataBuildGate
{
    internal const string ManifestAssetPath =
        "ProjectSettings/NyangbingoDataImportManifest.txt";
    private const string ManifestVersion = "nyangbingo-data-import-manifest-v2";
    private const string CatalogAssetPath = "Assets/Data/SO/GameDataCatalog.asset";

    [MenuItem("Nyangbingo/Validate Product Data Freshness")]
    public static void ValidateFromMenu()
    {
        if (TryValidateCurrent(out var summary))
            Debug.Log($"[Nyangbingo] Product data freshness validation passed: {summary}");
        else
            Debug.LogError($"[Nyangbingo] Product data freshness validation failed: {summary}");
    }

    public static bool TryValidateCurrent(out string summary)
    {
        try
        {
            var csvDirectory = ResolveCsvDirectory();
            var crossFileSummary = NyangbingoV24DataValidator.Validate(csvDirectory);
            var expected = ReadManifest();
            var current = BuildManifestLines(csvDirectory);
            if (!ManifestEntriesMatch(expected, current))
            {
                var missingOrChanged = expected.Except(current, StringComparer.Ordinal).ToArray();
                var newOrChanged = current.Except(expected, StringComparer.Ordinal).ToArray();
                throw new InvalidDataException(
                    "CSV가 마지막 성공 임포트 상태와 다릅니다. " +
                    $"기존/변경 {JoinOrNone(missingOrChanged)}; 현재/변경 {JoinOrNone(newOrChanged)}. " +
                    "Nyangbingo/Reimport v34 Data Bundle을 먼저 실행하세요.");
            }

            ValidateGeneratedCatalog();
            summary = $"{crossFileSummary}; {current.Length} CSV manifest; generated catalog valid";
            return true;
        }
        catch (Exception exception)
        {
            summary = exception.Message;
            return false;
        }
    }

    internal static bool ManifestEntriesMatch(
        IEnumerable<string> expectedEntries,
        IEnumerable<string> currentEntries)
    {
        if (expectedEntries == null || currentEntries == null) return false;
        var expected = expectedEntries
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var current = currentEntries
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return expected.SequenceEqual(current, StringComparer.Ordinal);
    }

    public static void WriteCurrentManifest()
    {
        var csvDirectory = ResolveCsvDirectory();
        NyangbingoV24DataValidator.Validate(csvDirectory);
        ValidateGeneratedCatalog();
        var lines = BuildManifestLines(csvDirectory);
        var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ??
                          throw new DirectoryNotFoundException("Project root could not be resolved.");
        var path = Path.Combine(projectRoot,
            ManifestAssetPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path) ??
                                  throw new DirectoryNotFoundException(
                                      "Data import manifest directory could not be resolved."));
        File.WriteAllLines(path,
            new[] { ManifestVersion }.Concat(lines),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        Debug.Log($"[Nyangbingo] Product data import manifest recorded: " +
                  $"{lines.Length} CSV files, {ManifestAssetPath}.");
    }

    private static string ResolveCsvDirectory()
    {
        var directory = Path.Combine(Application.dataPath, "Data", "CSV");
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException($"CSV directory is missing: {directory}");
        return directory;
    }

    private static string[] ReadManifest()
    {
        var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ??
                          throw new DirectoryNotFoundException("Project root could not be resolved.");
        var path = Path.Combine(projectRoot,
            ManifestAssetPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
            throw new FileNotFoundException(
                "제품 데이터 임포트 기록이 없습니다. " +
                "Nyangbingo/Reimport v34 Data Bundle을 먼저 실행하세요.",
                path);
        var lines = File.ReadAllLines(path, Encoding.UTF8);
        if (lines.Length == 0 ||
            !string.Equals(lines[0].Trim(), ManifestVersion, StringComparison.Ordinal))
            throw new InvalidDataException(
                $"제품 데이터 임포트 기록 버전이 올바르지 않습니다: {ManifestAssetPath}");
        return lines.Skip(1)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Trim())
            .ToArray();
    }

    private static string[] BuildManifestLines(string csvDirectory)
    {
        return Directory.GetFiles(csvDirectory, "*.csv")
            .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
            .Select(path =>
            {
                var fileName = Path.GetFileName(path);
                var rows = NyangbingoCsvUtility.ReadRows(path,
                    mergeUnquotedTrailingNote:
                    string.Equals(fileName, "globals.csv", StringComparison.Ordinal));
                return $"{fileName}|{rows.Count}|{ComputeSha256(path)}";
            })
            .ToArray();
    }

    private static void ValidateGeneratedCatalog()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<GameDataCatalog>(CatalogAssetPath);
        if (catalog == null)
            throw new InvalidDataException(
                $"Generated game data catalog is missing: {CatalogAssetPath}");
        if (!catalog.IsValid)
            throw new InvalidDataException(
                "Generated game data catalog contains null, blank, or duplicate IDs.");

        var actual = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["items"] = catalog.Items.Count,
            ["recipes"] = catalog.Recipes.Count,
            ["modules"] = catalog.Modules.Count,
            ["mineral tiers"] = catalog.MineralTiers.Count,
            ["seal rules"] = catalog.SealWhitelist.Count,
            ["ID migrations"] = catalog.IdMigrations.Count,
            ["day curves"] = catalog.DayCurves.Count,
            ["globals"] = catalog.Globals.Count,
            ["smelting"] = catalog.Smelting.Count,
            ["equipment"] = catalog.Equipment.Count,
            ["utilities"] = catalog.Utilities.Count,
            ["combat profiles"] = catalog.CombatProfiles.Count,
            ["yokai"] = catalog.Yokai.Count,
            ["bosses"] = catalog.Bosses.Count,
            ["chests"] = catalog.Chests.Count,
            ["day events"] = catalog.DayEvents.Count
        };
        // v46 kit 정본. equipment = equipment.csv 24 + accessories 아티팩트 20(중복 악세 6은 skip).
        var expected = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["items"] = 160,
            ["recipes"] = 90,
            ["modules"] = 11,
            ["mineral tiers"] = 15,
            ["seal rules"] = 23,
            ["ID migrations"] = 26,
            ["day curves"] = 30,
            ["globals"] = 114,
            ["smelting"] = 3,
            ["equipment"] = 44,
            ["utilities"] = 2,
            ["combat profiles"] = 18,
            ["yokai"] = 7,
            ["bosses"] = 10,
            ["chests"] = 4,
            ["day events"] = 1
        };
        var mismatches = expected
            .Where(pair => !actual.TryGetValue(pair.Key, out var count) || count != pair.Value)
            .Select(pair =>
            {
                actual.TryGetValue(pair.Key, out var count);
                return $"{pair.Key} {count}/{pair.Value}";
            })
            .ToArray();
        if (mismatches.Length > 0)
            throw new InvalidDataException(
                $"Generated v46 catalog count mismatch: {string.Join(", ", mismatches)}.");
    }

    private static string ComputeSha256(string path)
    {
        var normalizedText = File.ReadAllText(path, Encoding.UTF8)
            .Replace("\r\n", "\n")
            .Replace('\r', '\n');
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
            .GetBytes(normalizedText);
        using (var sha256 = SHA256.Create())
            return BitConverter.ToString(sha256.ComputeHash(bytes))
                .Replace("-", string.Empty);
    }

    private static string JoinOrNone(IEnumerable<string> values)
    {
        var array = values.ToArray();
        return array.Length == 0 ? "none" : string.Join(",", array);
    }
}
