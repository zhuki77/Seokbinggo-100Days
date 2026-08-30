using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

public static class NyangbingoV24DataValidator
{
    public static bool IsV24DataSet(string directory)
    {
        var itemsPath = Path.Combine(directory, "items.csv");
        var craftingPath = Path.Combine(directory, "crafting-tree.csv");
        var accessoriesPath = Path.Combine(directory, "accessories.csv");
        if (!File.Exists(itemsPath) || !File.Exists(craftingPath) || !File.Exists(accessoriesPath)) return false;
        var rows = NyangbingoCsvUtility.ReadRows(itemsPath);
        return rows.Count > 0 && rows[0].ContainsKey("name_ko") && rows[0].ContainsKey("category") &&
               rows[0].ContainsKey("max_stack") && rows[0].ContainsKey("mvp_scope");
    }

    public static string Validate(string directory)
    {
        if (!IsV24DataSet(directory))
            throw new InvalidDataException("The selected directory is not a v24 data set.");

        var items = Read(directory, "items.csv");
        var crafting = Read(directory, "crafting-tree.csv");
        var bosses = Read(directory, "bosses.csv");
        var chests = Read(directory, "chests.csv");
        var accessories = Read(directory, "accessories.csv");
        var smelting = Read(directory, "smelting.csv");
        var modules = Read(directory, "modules.csv");
        var combat = Read(directory, "player-combat.csv");
        var minerals = Read(directory, "mineral-tiers.csv");
        var yokai = Read(directory, "yokai-stats.csv");
        var drops = Read(directory, "drops.csv");
        var days = Read(directory, "day-curve.csv");
        var globals = Read(directory, "globals.csv");
        var migrationPath = Path.Combine(directory, "id-migration.csv");
        var isV241 = File.Exists(migrationPath);
        var migrations = isV241
            ? Read(directory, "id-migration.csv")
            : new List<Dictionary<string, string>>();
        var craftingExtension = isV241
            ? Read(directory, "crafting-ext.csv")
            : new List<Dictionary<string, string>>();

        var itemIds = BuildIdSet(items, "items.csv", "id");
        var bossIds = BuildIdSet(bosses, "bosses.csv", "id");
        var yokaiIds = BuildIdSet(yokai, "yokai-stats.csv", "id");
        var globalKeys = BuildIdSet(globals, "globals.csv", "key");
        var officialBossIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "king_dokkaebi",
            "mother_bulgasari",
            "imugi_boss",
            "jigwi",
            "gangcheol_blaze",
            "sangun",
            "samdugumi",
            "eop_guryeongi",
            "yeongno",
            "gangcheol_perfect"
        };
        var officialYokaiIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "club",
            "bulgasari",
            "yakwang",
            "eoduksini",
            "gaekgwi",
            "imugi",
            "gangcheol"
        };
        if (!bossIds.SetEquals(officialBossIds))
            throw new InvalidDataException(
                "bosses.csv must contain the ten v72 bosses from King Dokkaebi through Gangcheol Perfect.");
        if (!yokaiIds.SetEquals(officialYokaiIds))
            throw new InvalidDataException(
                "yokai-stats.csv must contain the seven v34 yokai.");
        if (bossIds.Overlaps(yokaiIds))
            throw new InvalidDataException("Boss and regular yokai IDs must not overlap.");
        var craftingById = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var hasOutputCount = crafting.Count > 0 && crafting[0].ContainsKey("output_count");
        var totalOutputCount = 0;
        var referenceCount = 0;
        foreach (var row in crafting)
        {
            var id = Value(row, "id", "crafting-tree.csv");
            craftingById.Add(id, row);
            var outputCount = hasOutputCount
                ? PositiveInt(Value(row, "output_count", "crafting-tree.csv"), "crafting-tree.csv", "output_count")
                : 1;
            totalOutputCount += outputCount;
            var type = Value(row, "type", "crafting-tree.csv");
            if (!IsCraftingType(type))
                throw new InvalidDataException($"crafting-tree.csv recipe '{id}' has unknown type '{type}'.");
            var scope = Value(row, "mvp_scope", "crafting-tree.csv");
            if (!string.Equals(scope, "A", StringComparison.Ordinal) &&
                !string.Equals(scope, "B", StringComparison.Ordinal))
                throw new InvalidDataException($"crafting-tree.csv recipe '{id}' has unknown mvp_scope '{scope}'.");
            RequireItem(itemIds, id, "crafting-tree.csv", "id");
            RequireOptionalItem(itemIds, Value(row, "station_id", "crafting-tree.csv"),
                "crafting-tree.csv", "station_id");
            referenceCount += 2 + ValidateItemPairs(itemIds, Value(row, "materials", "crafting-tree.csv"),
                "crafting-tree.csv", "materials").Count;
        }

        if (hasOutputCount && craftingById.TryGetValue("wallpaper", out var wallpaper))
        {
            if (PositiveInt(Value(wallpaper, "output_count", "crafting-tree.csv"),
                    "crafting-tree.csv", "output_count") != 16 ||
                !string.Equals(Value(wallpaper, "station_id", "crafting-tree.csv"), "workbench",
                    StringComparison.Ordinal) ||
                !PairsEqual(ParsePairs(Value(wallpaper, "materials", "crafting-tree.csv"),
                        "crafting-tree.csv", "materials"),
                    new Dictionary<string, int>(StringComparer.Ordinal) { { "clay", 3 }, { "wood", 5 } }))
                throw new InvalidDataException("crafting-tree.csv wallpaper recipe does not match the v26 contract.");
        }

        foreach (var row in bosses)
        {
            if (isV241)
            {
                Value(row, "drops_ko", "bosses.csv");
                if (row.ContainsKey("drops"))
                    throw new InvalidDataException("bosses.csv uses the obsolete machine-readable 'drops' column.");
            }
            ValidateBossCombatRow(row);
            var summonItemId = OptionalValue(row, "summon_item_id");
            var stationId = OptionalValue(row, "station_id");
            var materialText = OptionalValue(row, "summon_materials");
            if (string.IsNullOrWhiteSpace(summonItemId))
            {
                if (!string.IsNullOrWhiteSpace(stationId) || !string.IsNullOrWhiteSpace(materialText))
                    throw new InvalidDataException(
                        "A boss without a summon item cannot declare a station or summon materials.");
                continue;
            }
            RequireItem(itemIds, summonItemId, "bosses.csv", "summon_item_id");
            RequireOptionalItem(itemIds, stationId, "bosses.csv", "station_id");
            var bossMaterials = ValidateItemPairs(itemIds, materialText,
                "bosses.csv", "summon_materials");
            referenceCount += 2 + bossMaterials.Count;
            if (!craftingById.TryGetValue(summonItemId, out var recipe) ||
                !string.Equals(Value(recipe, "station_id", "crafting-tree.csv"), stationId,
                    StringComparison.Ordinal) ||
                !PairsEqual(bossMaterials, ParsePairs(Value(recipe, "materials", "crafting-tree.csv"),
                    "crafting-tree.csv", "materials")))
                throw new InvalidDataException($"Boss summon '{summonItemId}' does not match its crafting recipe.");
        }

        var accessoryIds = BuildIdSet(accessories, "accessories.csv", "id");
        foreach (var id in accessoryIds)
        {
            RequireItem(itemIds, id, "accessories.csv", "id");
            referenceCount++;
        }
        if (isV241) ValidateAccessoryStats(accessories);
        var chestAccessoryIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in chests)
        {
            foreach (var id in Value(row, "accessory_pool", "chests.csv").Split('|'))
            {
                RequireItem(itemIds, id, "chests.csv", "accessory_pool");
                chestAccessoryIds.Add(id);
                referenceCount++;
            }
            referenceCount += ValidateItemPairs(itemIds, Value(row, "bonus_items", "chests.csv"),
                "chests.csv", "bonus_items").Count;
        }
        var chestEligibleAccessoryIds = new HashSet<string>(
            accessories
                .Where(row =>
                {
                    var pools = Value(row, "pools", "accessories.csv");
                    return !pools.StartsWith("보스:", StringComparison.Ordinal) &&
                           !pools.StartsWith("랜드마크:", StringComparison.Ordinal);
                })
                .Select(row => Value(row, "id", "accessories.csv")),
            StringComparer.Ordinal);
        if (!chestAccessoryIds.SetEquals(chestEligibleAccessoryIds))
            throw new InvalidDataException(
                "Chest accessory pools must exactly cover the non-boss, non-landmark accessory IDs.");

        var stationBatchCaps = new Dictionary<string, int>(StringComparer.Ordinal);
        var smeltingIds = isV241
            ? BuildIdSet(smelting, "smelting.csv", "id")
            : new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in smelting)
        {
            var stationId = Value(row, "station_id", "smelting.csv");
            RequireItem(itemIds, stationId, "smelting.csv", "station_id");
            RequireItem(itemIds, Value(row, "output_id", "smelting.csv"), "smelting.csv", "output_id");
            RequireItem(itemIds, Value(row, "input_id", "smelting.csv"), "smelting.csv", "input_id");
            RequireItem(itemIds, "coal", "smelting.csv", "fuel_coal");
            referenceCount += 4;
            var batchCap = PositiveInt(Value(row, "batch_cap", "smelting.csv"), "smelting.csv", "batch_cap");
            if (stationBatchCaps.TryGetValue(stationId, out var existing) && existing != batchCap)
                throw new InvalidDataException($"Smelting station '{stationId}' has inconsistent batch_cap values.");
            stationBatchCaps[stationId] = batchCap;
        }

        foreach (var row in modules)
        {
            var moduleId = Value(row, "id", "modules.csv");
            var itemId = OptionalValue(row, "item_id");
            var isUpgradeModule = moduleId.StartsWith("seokbinggo_s", StringComparison.Ordinal);
            if (isUpgradeModule != string.IsNullOrWhiteSpace(itemId))
                throw new InvalidDataException(
                    $"modules.csv '{moduleId}' must {(isUpgradeModule ? "omit" : "declare")} item_id.");
            if (!isUpgradeModule)
            {
                RequireItem(itemIds, itemId, "modules.csv", "item_id");
                referenceCount++;
            }
            referenceCount += ValidateItemPairs(itemIds, Value(row, "materials", "modules.csv"),
                "modules.csv", "materials").Count;
        }
        foreach (var row in combat)
        {
            RequireItem(itemIds, Value(row, "item_id", "player-combat.csv"), "player-combat.csv", "item_id");
            referenceCount++;
        }
        foreach (var row in minerals)
        {
            RequireItem(itemIds, Value(row, "resource_id", "mineral-tiers.csv"),
                "mineral-tiers.csv", "resource_id");
            var hardness = PositiveInt(Value(row, "hardness", "mineral-tiers.csv"),
                "mineral-tiers.csv", "hardness");
            if (hardness > 3)
                throw new InvalidDataException("mineral-tiers.csv hardness must be between 1 and 3.");
            for (var tier = 1; tier <= 3; tier++)
            {
                var actual = Value(row, $"breakable_t{tier}", "mineral-tiers.csv");
                var expected = tier >= hardness ? "1" : "0";
                if (actual != expected)
                    throw new InvalidDataException(
                        $"mineral-tiers.csv breakable_t{tier} must match claw_tier >= hardness.");
            }
            referenceCount++;
        }

        foreach (var row in yokai)
        {
            var signatureItemId = OptionalValue(row, "sig_drop_id");
            if (!string.IsNullOrWhiteSpace(signatureItemId))
            {
                RequireItem(itemIds, signatureItemId, "yokai-stats.csv", "sig_drop_id");
                referenceCount++;
            }
        }
        var dropSourceKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in drops)
        {
            var sourceId = Value(row, "source_id", "drops.csv");
            var sourceType = Value(row, "source_type", "drops.csv");
            if (!dropSourceKeys.Add(sourceType + "\n" + sourceId))
                throw new InvalidDataException($"drops.csv has duplicate source '{sourceType}:{sourceId}'.");

            var tears = PositiveInt(Value(row, "tears", "drops.csv"), "drops.csv", "tears");
            var tearsBonus = NonNegativeInt(Value(row, "tears_bonus", "drops.csv"),
                "drops.csv", "tears_bonus");
            var signatureRate = FiniteNonNegative(Value(row, "sig_rate", "drops.csv"),
                "drops.csv", "sig_rate");
            var signatureCondition = Value(row, "sig_condition", "drops.csv");
            if (signatureRate > 1d)
                throw new InvalidDataException("drops.csv.sig_rate must be between 0 and 1.");

            if (isV241)
            {
                var sourceExists = string.Equals(sourceType, "yokai", StringComparison.Ordinal)
                    ? yokaiIds.Contains(sourceId)
                    : string.Equals(sourceType, "boss", StringComparison.Ordinal) && bossIds.Contains(sourceId);
                if (!sourceExists)
                    throw new InvalidDataException($"drops.csv has unknown {sourceType} source ID '{sourceId}'.");
                if (string.Equals(sourceType, "boss", StringComparison.Ordinal) &&
                    (tearsBonus != 0 || Math.Abs(signatureRate - 1d) > .0001d ||
                     !string.Equals(signatureCondition, "none", StringComparison.Ordinal)))
                    throw new InvalidDataException($"drops.csv boss '{sourceId}' must have fixed tears, " +
                                                   "a guaranteed signature drop, and no condition.");
                var extraDrops = ValidateOptionalItemPairs(itemIds, OptionalValue(row, "extra_drops"),
                    "drops.csv", "extra_drops");
                referenceCount += extraDrops.Count;
            }
            var signatureItemId = OptionalValue(row, "sig_drop_id");
            if (!string.IsNullOrWhiteSpace(signatureItemId))
            {
                RequireItem(itemIds, signatureItemId, "drops.csv", "sig_drop_id");
                referenceCount++;
            }
        }

        foreach (var row in days)
        {
            var composition = ParsePairs(Value(row, "spawn_composition", "day-curve.csv"),
                "day-curve.csv", "spawn_composition", allowZero: true);
            var total = 0;
            foreach (var pair in composition)
            {
                if (!yokaiIds.Contains(pair.Key))
                    throw new InvalidDataException($"day-curve.csv references unknown yokai ID '{pair.Key}'.");
                total += pair.Value;
            }
            var expected = NonNegativeInt(Value(row, "night_yokai_count", "day-curve.csv"),
                "day-curve.csv", "night_yokai_count") *
                FiniteNonNegative(Value(row, "drop_mult", "day-curve.csv"), "day-curve.csv", "drop_mult");
            if (Math.Abs(total - expected) > .0001d)
                throw new InvalidDataException($"day-curve.csv day {Value(row, "day", "day-curve.csv")} " +
                                               $"composition total {total} does not match {expected}.");
        }

        if (isV241)
        {
            ValidateCraftingExtension(craftingExtension, items);
            ValidateMigrations(migrations, itemIds, yokaiIds, bossIds, smeltingIds, globalKeys);
        }

        return $"{itemIds.Count} item IDs, {crafting.Count} recipes producing {totalOutputCount} items, " +
               $"{referenceCount} item references, " +
               $"{bosses.Count} boss recipes, {days.Count} day compositions" +
               (isV241 ? $", and {migrations.Count} ID migrations validated" : " validated");
    }

    private static List<Dictionary<string, string>> Read(string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);
        if (!File.Exists(path)) throw new FileNotFoundException($"Required v24 file is missing: {fileName}", path);
        return NyangbingoCsvUtility.ReadRows(path, mergeUnquotedTrailingNote: true);
    }

    private static HashSet<string> BuildIdSet(List<Dictionary<string, string>> rows, string file, string column)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var id = Value(row, column, file);
            if (!ids.Add(id)) throw new InvalidDataException($"{file} has duplicate {column} '{id}'.");
        }
        return ids;
    }

    private static Dictionary<string, int> ValidateItemPairs(HashSet<string> itemIds, string text,
        string file, string column)
    {
        var pairs = ParsePairs(text, file, column);
        foreach (var id in pairs.Keys) RequireItem(itemIds, id, file, column);
        return pairs;
    }

    private static Dictionary<string, int> ValidateOptionalItemPairs(HashSet<string> itemIds, string text,
        string file, string column)
    {
        if (string.IsNullOrWhiteSpace(text)) return new Dictionary<string, int>(StringComparer.Ordinal);
        return ValidateItemPairs(itemIds, text, file, column);
    }

    private static void ValidateAccessoryStats(List<Dictionary<string, string>> rows)
    {
        var numericColumns = new[]
        {
            "move_speed_bonus", "vision_radius_tiles", "day_temp_rise_mult", "mining_crit_bonus",
            "inventory_theft_immune", "double_jump", "double_jump_height_ratio"
        };
        foreach (var row in rows)
        {
            var id = Value(row, "id", "accessories.csv");
            Value(row, "effect_ko", "accessories.csv");
            for (var i = 0; i < numericColumns.Length; i++)
                _ = Finite(Value(row, numericColumns[i], "accessories.csv"),
                    "accessories.csv", numericColumns[i]);
            var theftImmune = BinaryInt(Value(row, "inventory_theft_immune", "accessories.csv"),
                "accessories.csv", "inventory_theft_immune");
            var doubleJump = BinaryInt(Value(row, "double_jump", "accessories.csv"),
                "accessories.csv", "double_jump");
            var jumpRatio = FiniteNonNegative(Value(row, "double_jump_height_ratio", "accessories.csv"),
                "accessories.csv", "double_jump_height_ratio");
            if ((doubleJump == 0 && jumpRatio != 0d) || (doubleJump == 1 && jumpRatio <= 0d))
                throw new InvalidDataException($"accessories.csv accessory '{id}' has inconsistent numeric effects.");
            if (theftImmune == 1 && doubleJump == 1)
                throw new InvalidDataException($"accessories.csv accessory '{id}' enables unrelated binary effects.");
        }
    }

    private static void ValidateCraftingExtension(List<Dictionary<string, string>> rows,
        List<Dictionary<string, string>> items)
    {
        var itemIds = BuildIdSet(items, "items.csv", "id");
        var itemScopes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in items)
            itemScopes[Value(item, "id", "items.csv")] = Value(item, "mvp_scope", "items.csv");

        var ids = BuildIdSet(rows, "crafting-ext.csv", "id");
        foreach (var row in rows)
        {
            var id = Value(row, "id", "crafting-ext.csv");
            if (itemIds.Contains(id) &&
                !string.Equals(itemScopes[id], "B", StringComparison.Ordinal))
                throw new InvalidDataException(
                    $"crafting-ext.csv output '{id}' must use items.csv mvp_scope 'B' when registered.");
            RequireItem(itemIds, Value(row, "station_id", "crafting-ext.csv"),
                "crafting-ext.csv", "station_id");
            ValidateItemPairs(itemIds, Value(row, "materials", "crafting-ext.csv"),
                "crafting-ext.csv", "materials");
            if (!string.Equals(Value(row, "scope", "crafting-ext.csv"), "ext", StringComparison.Ordinal))
                throw new InvalidDataException($"crafting-ext.csv output '{id}' must use scope 'ext'.");
        }
        if (ids.Count == 0) throw new InvalidDataException("crafting-ext.csv must contain extension recipes.");
    }

    private static void ValidateMigrations(List<Dictionary<string, string>> rows, HashSet<string> itemIds,
        HashSet<string> yokaiIds, HashSet<string> bossIds, HashSet<string> smeltingIds,
        HashSet<string> globalKeys)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var legacyId = Value(row, "legacy_id", "id-migration.csv");
            var domain = Value(row, "domain", "id-migration.csv");
            var action = Value(row, "action", "id-migration.csv");
            if (!keys.Add(legacyId + "\n" + domain))
                throw new InvalidDataException($"id-migration.csv has duplicate key ('{legacyId}', '{domain}').");
            var newId = OptionalValue(row, "new_id");
            if (string.Equals(action, "remove_refund", StringComparison.Ordinal))
            {
                if (!string.Equals(domain, "item", StringComparison.Ordinal) || !string.IsNullOrWhiteSpace(newId) ||
                    string.IsNullOrWhiteSpace(OptionalValue(row, "note")))
                    throw new InvalidDataException($"id-migration.csv removal '{legacyId}' has an invalid refund contract.");
                continue;
            }
            if (!string.Equals(action, "rename", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(newId))
                throw new InvalidDataException($"id-migration.csv migration '{legacyId}' has invalid action '{action}'.");
            HashSet<string> targets;
            if (string.Equals(domain, "item", StringComparison.Ordinal)) targets = itemIds;
            else if (string.Equals(domain, "yokai", StringComparison.Ordinal)) targets = yokaiIds;
            else if (string.Equals(domain, "boss", StringComparison.Ordinal)) targets = bossIds;
            else if (string.Equals(domain, "smelting", StringComparison.Ordinal)) targets = smeltingIds;
            else if (string.Equals(domain, "globals", StringComparison.Ordinal)) targets = globalKeys;
            else throw new InvalidDataException($"id-migration.csv uses unknown domain '{domain}'.");
            if (!targets.Contains(newId))
                throw new InvalidDataException($"id-migration.csv target '{newId}' is missing from {domain} master data.");
        }
    }

    private static Dictionary<string, int> ParsePairs(string text, string file, string column,
        bool allowZero = false)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidDataException($"{file}.{column} must not be blank.");
        var pairs = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var encoded in text.Split(','))
        {
            var parts = encoded.Split(':');
            var count = allowZero
                ? NonNegativeInt(parts.Length == 2 ? parts[1] : string.Empty, file, column)
                : PositiveInt(parts.Length == 2 ? parts[1] : string.Empty, file, column);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) ||
                !pairs.TryAdd(parts[0], count))
                throw new InvalidDataException($"{file}.{column} has invalid or duplicate pair '{encoded}'.");
        }
        return pairs;
    }

    private static bool PairsEqual(Dictionary<string, int> left, Dictionary<string, int> right)
    {
        if (left.Count != right.Count) return false;
        foreach (var pair in left)
            if (!right.TryGetValue(pair.Key, out var value) || value != pair.Value) return false;
        return true;
    }

    internal static void ValidateBossCombatRow(Dictionary<string, string> row)
    {
        var id = Value(row, "id", "bosses.csv");
        if (PositiveInt(Value(row, "recommended_day", "bosses.csv"),
                "bosses.csv", $"{id}.recommended_day") < 1 ||
            PositiveInt(Value(row, "hp", "bosses.csv"), "bosses.csv", $"{id}.hp") < 1)
            throw new InvalidDataException($"bosses.csv boss '{id}' has invalid day or HP.");

        FiniteNonNegative(Value(row, "wall_dps_default", "bosses.csv"),
            "bosses.csv", $"{id}.wall_dps_default");
        FiniteNonNegative(Value(row, "wall_dps_ice", "bosses.csv"),
            "bosses.csv", $"{id}.wall_dps_ice");
        FiniteNonNegative(Value(row, "wall_dps_iron_wall", "bosses.csv"),
            "bosses.csv", $"{id}.wall_dps_iron_wall");
        NonNegativeInt(Value(row, "contact_dmg", "bosses.csv"),
            "bosses.csv", $"{id}.contact_dmg");

        var telegraph = FiniteNonNegative(Value(row, "tele_sec", "bosses.csv"),
            "bosses.csv", $"{id}.tele_sec");
        var shape = Value(row, "shape", "bosses.csv");
        if (shape != "Box" && shape != "Cone" && shape != "Fan")
            throw new InvalidDataException(
                $"bosses.csv.{id}.shape must be Box, Cone, or Fan, but was '{shape}'.");
        var range = FiniteNonNegative(Value(row, "range_tiles", "bosses.csv"),
            "bosses.csv", $"{id}.range_tiles");
        var arc = FiniteNonNegative(Value(row, "arc_deg", "bosses.csv"),
            "bosses.csv", $"{id}.arc_deg");
        var damage = PositiveInt(Value(row, "special_dmg_per_hit", "bosses.csv"),
            "bosses.csv", $"{id}.special_dmg_per_hit");
        var duration = FiniteNonNegative(Value(row, "duration_sec", "bosses.csv"),
            "bosses.csv", $"{id}.duration_sec");
        var tick = FiniteNonNegative(Value(row, "tick_sec", "bosses.csv"),
            "bosses.csv", $"{id}.tick_sec");
        FiniteNonNegative(Value(row, "knockback_tiles", "bosses.csv"),
            "bosses.csv", $"{id}.knockback_tiles");
        var cooldown = FiniteNonNegative(Value(row, "cd_sec", "bosses.csv"),
            "bosses.csv", $"{id}.cd_sec");
        BinaryInt(Value(row, "fire_tag", "bosses.csv"), "bosses.csv", $"{id}.fire_tag");
        BinaryInt(Value(row, "aim_lock", "bosses.csv"), "bosses.csv", $"{id}.aim_lock");

        var scope = Value(row, "mvp_scope", "bosses.csv");
        var arena = Value(row, "arena_layer", "bosses.csv");
        var heatStage = PositiveInt(Value(row, "heat_stage", "bosses.csv"),
            "bosses.csv", $"{id}.heat_stage");
        if (telegraph < 0d || range <= 0d || arc > 180d || damage <= 0 || cooldown <= 0d ||
            (duration == 0d) != (tick == 0d) ||
            ((shape == "Cone" || shape == "Fan") && arc <= 0d) ||
            (scope != "A" && scope != "B") || arena != "surface" || heatStage > 3)
            throw new InvalidDataException($"bosses.csv boss '{id}' has invalid combat or scope semantics.");
    }

    private static void RequireOptionalItem(HashSet<string> itemIds, string id, string file, string column)
    {
        if (string.Equals(id, "none", StringComparison.Ordinal)) return;
        RequireItem(itemIds, id, file, column);
    }

    private static void RequireItem(HashSet<string> itemIds, string id, string file, string column)
    {
        if (!itemIds.Contains(id))
            throw new InvalidDataException($"{file}.{column} references unknown item ID '{id}'.");
    }

    private static string Value(Dictionary<string, string> row, string column, string file)
    {
        if (!row.TryGetValue(column, out var value) || string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException($"{file} is missing required value '{column}'.");
        return value;
    }

    private static string OptionalValue(Dictionary<string, string> row, string column)
        => row.TryGetValue(column, out var value) ? value : string.Empty;

    private static int BinaryInt(string text, string file, string column)
    {
        var value = NonNegativeInt(text, file, column);
        if (value > 1) throw new InvalidDataException($"{file}.{column} must be 0 or 1.");
        return value;
    }

    private static bool IsCraftingType(string value)
    {
        switch (value)
        {
            case "armor":
            case "claw":
            case "coldsource":
            case "cooling":
            case "deco":
            case "device":
            case "evo":
            case "insulation":
            case "module":
            case "placeable":
            case "smelt":
            case "station":
            case "summon":
            case "turret":
            case "util":
            case "wall":
            case "weapon":
                return true;
            default:
                return false;
        }
    }

    private static int PositiveInt(string text, string file, string column)
    {
        var value = NonNegativeInt(text, file, column);
        if (value <= 0) throw new InvalidDataException($"{file}.{column} must be positive.");
        return value;
    }

    private static int NonNegativeInt(string text, string file, string column)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < 0)
            throw new InvalidDataException($"{file}.{column} must be a non-negative integer.");
        return value;
    }

    private static double FiniteNonNegative(string text, string file, string column)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
            value < 0d || double.IsNaN(value) || double.IsInfinity(value))
            throw new InvalidDataException($"{file}.{column} must be a finite non-negative number.");
        return value;
    }

    private static double Finite(string text, string file, string column)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
            double.IsNaN(value) || double.IsInfinity(value))
            throw new InvalidDataException($"{file}.{column} must be a finite number.");
        return value;
    }
}
