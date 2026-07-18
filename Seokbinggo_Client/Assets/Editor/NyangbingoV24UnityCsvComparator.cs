using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class NyangbingoV24UnityCsvComparator
{
    private const string UnityCsvAssetDirectory = "Assets/Data/CSV";

    private static readonly string[] OfficialV241Files =
    {
        "accessories.csv",
        "bosses.csv",
        "chests.csv",
        "crafting-ext.csv",
        "crafting-tree.csv",
        "day-curve.csv",
        "day-curve-ext.csv",
        "drops.csv",
        "globals.csv",
        "id-migration.csv",
        "items.csv",
        "mineral-tiers.csv",
        "modules.csv",
        "player-combat.csv",
        "seal-whitelist.csv",
        "smelting.csv",
        "yokai-stats.csv"
    };

    private static readonly CsvMapping[] Mappings =
    {
        new CsvMapping("accessories.csv", "equipment.csv", new[] { "id" }, new[] { "id" }, new[]
        {
            Field("move_speed_bonus", "movementBonus", ValueKind.Number),
            Field("vision_radius_tiles", "visionRadiusBonus", ValueKind.Number),
            Field("day_temp_rise_mult", "temperatureRiseModifier", ValueKind.Number),
            Field("mining_crit_bonus", "miningCriticalBonus", ValueKind.Number),
            Field("inventory_theft_immune", "blocksInventoryTheft", ValueKind.Boolean),
            Field("double_jump", "grantsDoubleJump", ValueKind.Boolean),
            Field("double_jump_height_ratio", "doubleJumpHeightRatio", ValueKind.Number)
        }, "name_ko", "effect_ko", "note"),
        new CsvMapping("bosses.csv", "bosses.csv", new[] { "id" }, new[] { "id" }, new[]
        {
            Field("summon_item_id", "summon_item_id", ValueKind.Text),
            Field("summon_materials", "summon_materials", ValueKind.ItemPairs),
            Field("station_id", "station_id", ValueKind.Station),
            Field("hp", "hp", ValueKind.Number),
            Field("wall_dps_default", "wall_dps_default", ValueKind.Number),
            Field("wall_dps_ice", "wall_dps_ice", ValueKind.Number),
            Field("wall_dps_iron_wall", "wall_dps_iron_wall", ValueKind.Number),
            Field("contact_dmg", "contact_dmg", ValueKind.Number),
            Field("tele_sec", "tele_sec", ValueKind.Number),
            Field("shape", "shape", ValueKind.Text),
            Field("range_tiles", "range_tiles", ValueKind.Number),
            Field("arc_deg", "arc_deg", ValueKind.Number),
            Field("special_dmg_per_hit", "special_dmg_per_hit", ValueKind.Number),
            Field("duration_sec", "duration_sec", ValueKind.Number),
            Field("tick_sec", "tick_sec", ValueKind.Number),
            Field("knockback_tiles", "knockback_tiles", ValueKind.Number),
            Field("cd_sec", "cd_sec", ValueKind.Number),
            Field("fire_tag", "fire_tag", ValueKind.Boolean),
            Field("aim_lock", "aim_lock", ValueKind.Boolean),
            Field("mvp_scope", "mvp_scope", ValueKind.Text)
        }, "name_ko", "summon_item_ko", "recommended_day", "special_ko", "drops_ko", "engage_sec_check"),
        new CsvMapping("chests.csv", "chests.csv", new[] { "pool" }, new[] { "pool" }, new[]
        {
            Field("count", "count", ValueKind.Number),
            Field("accessory_pool", "accessory_pool", ValueKind.ItemPairs),
            Field("bonus_items", "bonus_items", ValueKind.ItemPairs)
        }, "note"),
        new CsvMapping("crafting-tree.csv", "recipes.csv", new[] { "id" }, new[] { "id" }, new[]
        {
            Field("id", "output", ValueKind.Text),
            Field("station_id", "station", ValueKind.Station),
            Field("materials", "ingredients", ValueKind.ItemPairs),
            Field("craft_time_sec", "durationSeconds", ValueKind.Number)
        }, "item_ko", "note"),
        new CsvMapping("day-curve.csv", "day-curve.csv", new[] { "day" }, new[] { "day" }, new[]
        {
            Field("heat_stage", "heat_stage", ValueKind.Number),
            Field("day_fire_dmg_per_sec", "day_fire_dmg_per_sec", ValueKind.Number),
            Field("night_yokai_count", "night_yokai_count", ValueKind.Number),
            Field("yokai_wall_dmg", "yokai_wall_dmg", ValueKind.Number),
            Field("pace_seal_pct", "pace_seal_pct", ValueKind.Number),
            Field("pace_mineral_tier", "pace_mineral_tier", ValueKind.Number),
            Field("max_active", "max_active", ValueKind.Number),
            Field("spawn_composition", "spawn_composition", ValueKind.ItemPairs),
            Field("spawn_mult", "spawn_mult", ValueKind.Number),
            Field("drop_mult", "drop_mult", ValueKind.Number),
            Field("event_id", "event_id", ValueKind.Text)
        }),
        new CsvMapping("drops.csv", "drops.csv", new[] { "source_type", "source_id" },
            new[] { "source_type", "source_id" }, new[]
            {
                Field("tears", "tears", ValueKind.Number),
                Field("tears_bonus", "tears_bonus", ValueKind.Number),
                Field("sig_drop_id", "sig_drop_id", ValueKind.Text),
                Field("sig_rate", "sig_rate", ValueKind.Number),
                Field("sig_condition", "sig_condition", ValueKind.Text),
                Field("extra_drops", "extra_drops", ValueKind.ItemPairs),
                Field("sig_use", "sig_use", ValueKind.Text)
            }, "source_ko", "sig_drop_ko"),
        new CsvMapping("globals.csv", "globals.csv", new[] { "key" }, new[] { "key" }, new[]
        {
            Field("value", "value", ValueKind.Text),
            Field("unit", "unit", ValueKind.Text),
            Field("note", "note", ValueKind.Text)
        }),
        new CsvMapping("items.csv", "items.csv", new[] { "id" }, new[] { "id" }, new[]
        {
            Field("name_ko", "displayName", ValueKind.Text),
            Field("max_stack", "maxStack", ValueKind.Number)
        }, "note"),
        new CsvMapping("id-migration.csv", "id-migration.csv", new[] { "legacy_id", "domain" },
            new[] { "legacy_id", "domain" }, new[]
            {
                Field("new_id", "new_id", ValueKind.Text),
                Field("action", "action", ValueKind.Text),
                Field("note", "note", ValueKind.Text)
            }),
        new CsvMapping("modules.csv", "modules.csv", new[] { "id" }, new[] { "id" }, new[]
        {
            Field("name_ko", "name_ko", ValueKind.Text),
            Field("item_id", "item_id", ValueKind.Text),
            Field("role", "role", ValueKind.Text),
            Field("materials", "materials", ValueKind.ItemPairs),
            Field("build_time_sec", "build_time_sec", ValueKind.Number),
            Field("priority", "priority", ValueKind.Text)
        }),
        new CsvMapping("mineral-tiers.csv", "mineral-tiers.csv", new[] { "resource_id" },
            new[] { "resource_id" }, new[]
            {
                Field("name_ko", "name_ko", ValueKind.Text),
                Field("layer", "layer", ValueKind.Text),
                Field("depth_min", "depth_min", ValueKind.Number),
                Field("depth_max", "depth_max", ValueKind.Number),
                Field("min_claw_tier", "min_claw_tier", ValueKind.Number),
                Field("gate_type", "gate_type", ValueKind.Text),
                Field("claw_t1_sec", "claw_t1_sec", ValueKind.Number),
                Field("claw_t2_sec", "claw_t2_sec", ValueKind.Number),
                Field("claw_t3_sec", "claw_t3_sec", ValueKind.Number),
                Field("freq_per_100tiles", "freq_per_100tiles", ValueKind.Number),
                Field("use_ko", "use_ko", ValueKind.Text),
                Field("gate_ko", "gate_ko", ValueKind.Text)
            }),
        new CsvMapping("seal-whitelist.csv", "seal-whitelist.csv", new[] { "element" },
            new[] { "element" }, new[]
            {
                Field("seals", "seals", ValueKind.Boolean),
                Field("note", "note", ValueKind.Text)
            }),
        new CsvMapping("player-combat.csv", "player-combat.csv", new[] { "item_id" }, new[] { "item_id" }, new[]
        {
            Field("tier", "tier", ValueKind.Text),
            Field("attack_dmg", "attack_dmg", ValueKind.Number),
            Field("attacks_per_sec", "attacks_per_sec", ValueKind.Text),
            Field("dps", "dps", ValueKind.Number),
            Field("knockback_tiles", "knockback_tiles", ValueKind.Number),
            Field("range_tiles", "range_tiles", ValueKind.Number),
            Field("arc_deg", "arc_deg", ValueKind.Number),
            Field("multi_target", "multi_target", ValueKind.Boolean),
            Field("hits_walls", "hits_walls", ValueKind.Boolean)
        }, "name_ko", "verify_note"),
        new CsvMapping("smelting.csv", "smelting.csv", new[] { "id" }, new[] { "id" }, new[]
        {
            Field("station_id", "station", ValueKind.Station),
            Field("output_id", "output", ValueKind.Text),
            Field("input_id", "input", ValueKind.Text),
            Field("input_count", "inputAmount", ValueKind.Number),
            Field("fuel_coal", "fuelAmount", ValueKind.Number),
            Field("time_sec_per_ingot", "durationSeconds", ValueKind.Number),
            Field("batch_cap", "batchCapacity", ValueKind.Number)
        }, "output_ko", "note"),
        new CsvMapping("yokai-stats.csv", "yokai-stats.csv", new[] { "id" }, new[] { "id" }, new[]
        {
            Field("hp", "hp", ValueKind.Number),
            Field("move_tiles_per_sec", "move_tiles_per_sec", ValueKind.Number),
            Field("wall_dps_default", "wall_dps_default", ValueKind.Number),
            Field("wall_dps_ice", "wall_dps_ice", ValueKind.Number),
            Field("wall_dps_iron_wall", "wall_dps_iron_wall", ValueKind.Number),
            Field("contact_dmg", "contact_dmg", ValueKind.Number),
            Field("contact_dmg_no_lantern", "contact_dmg_no_lantern", ValueKind.Number),
            Field("dmg_taken_mult", "dmg_taken_mult", ValueKind.Number),
            Field("dmg_taken_condition", "dmg_taken_condition", ValueKind.Text),
            Field("steal_slots", "steal_slots", ValueKind.Number),
            Field("steal_max_items", "steal_max_items", ValueKind.Number),
            Field("tears", "tears", ValueKind.Number),
            Field("sig_drop_id", "sig_drop_id", ValueKind.Text),
            Field("sig_rate", "sig_rate", ValueKind.Number),
            Field("sig_condition", "sig_condition", ValueKind.Text),
            Field("spawn_track", "spawn_track", ValueKind.Text),
            Field("dawn_flee", "dawn_flee", ValueKind.Text)
        }, "name_ko", "appear_from", "note")
    };

    [MenuItem("Nyangbingo/Compare Official v24.1 With Unity CSV")]
    private static void CompareFromMenu()
    {
        var officialDirectory = EditorUtility.OpenFolderPanel("Select official v24.1 CSV folder", string.Empty,
            string.Empty);
        if (string.IsNullOrWhiteSpace(officialDirectory)) return;

        try
        {
            var unityDirectory = Path.Combine(Application.dataPath, "Data", "CSV");
            Debug.Log(Compare(officialDirectory, unityDirectory));
        }
        catch (Exception exception)
        {
            Debug.LogError($"[Nyangbingo] Official-to-Unity CSV comparison failed: {exception.Message}");
        }
    }

    public static string Compare(string officialDirectory, string unityDirectory)
    {
        if (string.IsNullOrWhiteSpace(officialDirectory) || !Directory.Exists(officialDirectory))
            throw new DirectoryNotFoundException("The official v24.1 CSV directory does not exist.");
        if (string.IsNullOrWhiteSpace(unityDirectory) || !Directory.Exists(unityDirectory))
            throw new DirectoryNotFoundException("The Unity CSV directory does not exist.");

        var validationSummary = NyangbingoV24DataValidator.Validate(officialDirectory);
        var officialCsvFiles = Directory.GetFiles(officialDirectory, "*.csv")
            .Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        var unityCsvFiles = Directory.GetFiles(unityDirectory, "*.csv")
            .Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal).ToArray();

        RequireExactManifest(officialCsvFiles);

        var builder = new StringBuilder();
        builder.AppendLine("[Nyangbingo] Official v24.1 -> Unity CSV comparison completed.");
        builder.AppendLine($"[Nyangbingo] Official validation: {validationSummary}.");
        builder.AppendLine($"[Nyangbingo] Inventory: official {officialCsvFiles.Length} CSV, Unity " +
                           $"{unityCsvFiles.Length} CSV, mapped {Mappings.Length} pairs.");

        builder.AppendLine("[Nyangbingo] Official manifest (rows, SHA-256):");
        for (var i = 0; i < officialCsvFiles.Length; i++)
            AppendManifestLine(builder, officialDirectory, officialCsvFiles[i]);

        builder.AppendLine("[Nyangbingo] Unity manifest (rows, SHA-256):");
        for (var i = 0; i < unityCsvFiles.Length; i++)
            AppendManifestLine(builder, unityDirectory, unityCsvFiles[i]);

        var mappedOfficial = new HashSet<string>(Mappings.Select(mapping => mapping.OfficialFile),
            StringComparer.Ordinal);
        var mappedUnity = new HashSet<string>(Mappings.Select(mapping => mapping.UnityFile),
            StringComparer.Ordinal);
        var officialOnly = officialCsvFiles.Where(file => !mappedOfficial.Contains(file)).ToArray();
        var unityOnly = unityCsvFiles.Where(file => !mappedUnity.Contains(file)).ToArray();
        builder.AppendLine($"[Nyangbingo] Not yet mapped from official: {JoinOrNone(officialOnly)}.");
        builder.AppendLine($"[Nyangbingo] Unity-only pipeline files: {JoinOrNone(unityOnly)}.");

        for (var i = 0; i < Mappings.Length; i++)
            AppendMappingComparison(builder, officialDirectory, unityDirectory, Mappings[i]);

        return builder.ToString().TrimEnd();
    }

    private static void RequireExactManifest(string[] actualFiles)
    {
        var missing = OfficialV241Files.Except(actualFiles, StringComparer.Ordinal).ToArray();
        var extra = actualFiles.Except(OfficialV241Files, StringComparer.Ordinal).ToArray();
        if (missing.Length > 0 || extra.Length > 0)
            throw new InvalidDataException($"Official v24.1 manifest mismatch. Missing: {JoinOrNone(missing)}; " +
                                           $"extra: {JoinOrNone(extra)}.");
    }

    private static void AppendManifestLine(StringBuilder builder, string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);
        var rowCount = NyangbingoCsvUtility.ReadRows(path, mergeUnquotedTrailingNote: true).Count;
        builder.AppendLine($"  - {fileName}: {rowCount} rows, {ComputeSha256(path)}");
    }

    private static void AppendMappingComparison(StringBuilder builder, string officialDirectory,
        string unityDirectory, CsvMapping mapping)
    {
        var officialPath = Path.Combine(officialDirectory, mapping.OfficialFile);
        var unityPath = Path.Combine(unityDirectory, mapping.UnityFile);
        if (!File.Exists(unityPath))
        {
            builder.AppendLine($"[Nyangbingo] MAP {mapping.OfficialFile} -> {mapping.UnityFile}: Unity file missing.");
            return;
        }

        var officialRows = NyangbingoCsvUtility.ReadRows(officialPath, mergeUnquotedTrailingNote: true);
        var unityRows = NyangbingoCsvUtility.ReadRows(unityPath,
            mergeUnquotedTrailingNote: mapping.UnityFile == "globals.csv");
        if (mapping.OfficialKeyColumns == null || mapping.UnityKeyColumns == null)
        {
            builder.AppendLine($"[Nyangbingo] MAP {mapping.OfficialFile} -> {mapping.UnityFile}: " +
                               $"rows {officialRows.Count}/{unityRows.Count}; schema requires a custom adapter.");
            return;
        }

        var officialByKey = BuildRowsByKey(officialRows, mapping.OfficialKeyColumns, mapping.OfficialFile);
        var unityByKey = BuildRowsByKey(unityRows, mapping.UnityKeyColumns, mapping.UnityFile);
        var missingKeys = officialByKey.Keys.Except(unityByKey.Keys, StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var extraKeys = unityByKey.Keys.Except(officialByKey.Keys, StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var officialHeaders = officialRows.Count > 0
            ? new HashSet<string>(officialRows[0].Keys, StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        var mappedOfficialColumns = new HashSet<string>(mapping.OfficialKeyColumns, StringComparer.Ordinal);
        for (var fieldIndex = 0; fieldIndex < mapping.Fields.Length; fieldIndex++)
            mappedOfficialColumns.Add(mapping.Fields[fieldIndex].OfficialColumn);
        mappedOfficialColumns.UnionWith(mapping.IgnoredOfficialColumns);
        var unmappedOfficialColumns = officialHeaders.Except(mappedOfficialColumns, StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var mismatchSamples = new List<string>();
        var valueMismatchCount = CountMappedValueMismatches(officialByKey, unityByKey, mapping.Fields,
            mismatchSamples);
        var mappedFieldNames = mapping.Fields.Select(field =>
            field.OfficialColumn + "->" + field.UnityColumn).ToArray();

        builder.AppendLine($"[Nyangbingo] MAP {mapping.OfficialFile} -> {mapping.UnityFile}: rows " +
                           $"{officialRows.Count}/{unityRows.Count}, missing IDs {missingKeys.Length}, " +
                           $"extra IDs {extraKeys.Length}, semantic mismatches {valueMismatchCount}.");
        builder.AppendLine($"  mapped fields: {JoinOrNone(mappedFieldNames)}");
        builder.AppendLine($"  official fields not integrated: {JoinOrNone(unmappedOfficialColumns)}");
        if (mismatchSamples.Count > 0)
            builder.AppendLine($"  mismatches: {string.Join(" | ", mismatchSamples)}");
        if (missingKeys.Length > 0) builder.AppendLine($"  missing: {string.Join(",", missingKeys)}");
        if (extraKeys.Length > 0) builder.AppendLine($"  extra: {string.Join(",", extraKeys)}");
    }

    private static Dictionary<string, Dictionary<string, string>> BuildRowsByKey(
        List<Dictionary<string, string>> rows, string[] columns, string fileName)
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var parts = new string[columns.Length];
            for (var columnIndex = 0; columnIndex < columns.Length; columnIndex++)
            {
                if (!row.TryGetValue(columns[columnIndex], out parts[columnIndex]) ||
                    string.IsNullOrWhiteSpace(parts[columnIndex]))
                    throw new InvalidDataException($"{fileName} row {rowIndex + 2} is missing key column " +
                                                   $"'{columns[columnIndex]}'.");
            }

            var key = string.Join(":", parts);
            if (!result.TryAdd(key, row))
                throw new InvalidDataException($"{fileName} has duplicate key '{key}'.");
        }
        return result;
    }

    private static int CountMappedValueMismatches(
        Dictionary<string, Dictionary<string, string>> officialByKey,
        Dictionary<string, Dictionary<string, string>> unityByKey, FieldMapping[] fields,
        List<string> mismatchSamples)
    {
        var mismatchCount = 0;
        foreach (var pair in officialByKey)
        {
            if (!unityByKey.TryGetValue(pair.Key, out var unityRow)) continue;
            for (var fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
            {
                var field = fields[fieldIndex];
                if (!pair.Value.TryGetValue(field.OfficialColumn, out var officialValue) ||
                    !unityRow.TryGetValue(field.UnityColumn, out var unityValue))
                {
                    mismatchCount++;
                    if (mismatchSamples.Count < 20)
                        mismatchSamples.Add($"{pair.Key}.{field.OfficialColumn}->{field.UnityColumn}: missing column");
                    continue;
                }
                var normalizedOfficial = NormalizeValue(officialValue, field.Kind);
                var normalizedUnity = NormalizeValue(unityValue, field.Kind);
                if (!string.Equals(normalizedOfficial, normalizedUnity, StringComparison.Ordinal))
                {
                    mismatchCount++;
                    if (mismatchSamples.Count < 20)
                        mismatchSamples.Add($"{pair.Key}.{field.OfficialColumn}->{field.UnityColumn}: " +
                                            $"'{officialValue}' <> '{unityValue}'");
                }
            }
        }
        return mismatchCount;
    }

    private static string NormalizeValue(string value, ValueKind kind)
    {
        value = (value ?? string.Empty).Trim();
        switch (kind)
        {
            case ValueKind.Number:
                return double.TryParse(value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var number)
                    ? number.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                    : value;
            case ValueKind.Boolean:
                if (value == "1") return "true";
                if (value == "0") return "false";
                return value.ToLowerInvariant();
            case ValueKind.Station:
                switch (value.ToLowerInvariant())
                {
                    case "none": return "None";
                    case "workbench": return "Workbench";
                    case "furnace": return "Furnace";
                    case "ice_anvil": return "IceAnvil";
                    case "blast_furnace": return "Foundry";
                    default: return value;
                }
            case ValueKind.ItemPairs:
                if (string.IsNullOrWhiteSpace(value)) return string.Empty;
                var pairs = value.Replace('|', ',').Split(',');
                Array.Sort(pairs, StringComparer.Ordinal);
                return string.Join(",", pairs);
            case ValueKind.EnumName:
                var words = value.Split('_');
                var enumName = new StringBuilder();
                for (var i = 0; i < words.Length; i++)
                    if (words[i].Length > 0)
                        enumName.Append(char.ToUpperInvariant(words[i][0])).Append(words[i].Substring(1));
                return enumName.ToString();
            default:
                return value;
        }
    }

    private static FieldMapping Field(string officialColumn, string unityColumn, ValueKind kind)
        => new FieldMapping(officialColumn, unityColumn, kind);

    private static string ComputeSha256(string path)
    {
        using (var stream = File.OpenRead(path))
        using (var sha256 = SHA256.Create())
            return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty);
    }

    private static string JoinOrNone(IEnumerable<string> values)
    {
        var array = values.ToArray();
        return array.Length == 0 ? "none" : string.Join(",", array);
    }

    private sealed class CsvMapping
    {
        public CsvMapping(string officialFile, string unityFile, string[] officialKeyColumns,
            string[] unityKeyColumns, FieldMapping[] fields = null, params string[] ignoredOfficialColumns)
        {
            OfficialFile = officialFile;
            UnityFile = unityFile;
            OfficialKeyColumns = officialKeyColumns;
            UnityKeyColumns = unityKeyColumns;
            Fields = fields ?? Array.Empty<FieldMapping>();
            IgnoredOfficialColumns = ignoredOfficialColumns ?? Array.Empty<string>();
        }

        public string OfficialFile { get; }
        public string UnityFile { get; }
        public string[] OfficialKeyColumns { get; }
        public string[] UnityKeyColumns { get; }
        public FieldMapping[] Fields { get; }
        public string[] IgnoredOfficialColumns { get; }
    }

    private sealed class FieldMapping
    {
        public FieldMapping(string officialColumn, string unityColumn, ValueKind kind)
        {
            OfficialColumn = officialColumn;
            UnityColumn = unityColumn;
            Kind = kind;
        }

        public string OfficialColumn { get; }
        public string UnityColumn { get; }
        public ValueKind Kind { get; }
    }

    private enum ValueKind
    {
        Text,
        Number,
        Boolean,
        Station,
        ItemPairs,
        EnumName
    }
}
