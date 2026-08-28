using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Nyangbingo.Data;
using UnityEditor;
using UnityEngine;

public static class NyangbingoV72ContentImporter
{
    private const string CsvRoot = "Assets/Data/CSV";
    private const string SoRoot = "Assets/Data/SO";

    [MenuItem("Nyangbingo/Reimport v72 New Content CSVs")]
    public static void ReimportAllFromCommandLine()
    {
        try
        {
            RepairGeneratedAssetBindings();
            ImportZones();
            ImportTerrainSpawns();
            ImportTalismans();
            ImportCodex();
            ImportTraits();
            ImportCrops();
            ValidateContentStatus();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Nyangbingo] v72 new content CSV import completed: zones 10, terrain-spawn 70, " +
                      "talismans 5, codex 17, traits 4, crops 10, content-status 858.");
        }
        catch (Exception exception)
        {
            Debug.LogError($"[Nyangbingo] v72 new content CSV import failed: {exception.Message}\n{exception.StackTrace}");
        }
    }

    private static void RepairGeneratedAssetBindings()
    {
        const string probe = SoRoot + "/Crops/zone01_catnip.asset";
        if (!File.Exists(probe) || AssetDatabase.LoadAssetAtPath<CropDefinition>(probe) != null) return;
        foreach (var directory in new[] { "Zones", "TerrainSpawns", "Talismans", "Codex", "Traits", "Crops" })
            AssetDatabase.DeleteAsset(SoRoot + "/" + directory);
        AssetDatabase.Refresh();
    }

    private static void ImportZones()
    {
        var rows = Read("zones.csv", 10);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var id = Required(row, "zone_id");
            var order = Int(row, "order", 1);
            var from = Float(row, "dist_norm_from", 0f, 1f);
            var to = Float(row, "dist_norm_to", 0f, 1f);
            if (!ids.Add(id) || order != i + 1 || from >= to ||
                i > 0 && !Mathf.Approximately(from, Float(rows[i - 1], "dist_norm_to", 0f, 1f)))
                throw new InvalidDataException($"zones.csv row {i + 2} has invalid ID/order/normalized range.");

            var asset = LoadOrCreate<ZoneDefinition>("Zones", id);
            var so = new SerializedObject(asset);
            Set(so, "id", id); Set(so, "order", order);
            Set(so, "distanceTilesFrom", Int(row, "dist_tiles_from", 0));
            Set(so, "distanceTilesTo", Int(row, "dist_tiles_to", 1));
            Set(so, "distanceNormalizedFrom", from); Set(so, "distanceNormalizedTo", to);
            Set(so, "tier", Int(row, "tier", 1)); Set(so, "altarId", Required(row, "altar_id"));
            Set(so, "bossId", Required(row, "boss_id")); Set(so, "bossDisplayName", Required(row, "boss_ko"));
            Set(so, "bossDay", Required(row, "boss_day")); Set(so, "bossHitPoints", Int(row, "boss_hp", 1));
            Set(so, "bossSummonType", Required(row, "boss_summon_type"));
            Set(so, "treeDensityMultiplier", Float(row, "tree_density_mult", 0f));
            Set(so, "note", Required(row, "note")); Set(so, "heatStage", Int(row, "heat_stage", 1, 3));
            Set(so, "gateRole", Required(row, "gate_role")); Apply(so, asset);
        }
        if (!Mathf.Approximately(Float(rows[rows.Count - 1], "dist_norm_to", 0f, 1f), 1f))
            throw new InvalidDataException("zones.csv must cover normalized distance through 1.0.");
    }

    private static void ImportTerrainSpawns()
    {
        var rows = Read("terrain-spawn.csv", 70);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var weights = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var terrain = Required(row, "terrain_id");
            var yokai = Required(row, "yokai_id");
            var id = terrain + ":" + yokai;
            var weight = Int(row, "weight", 0);
            var resourceText = row["terrain_resources"].Trim();
            var resources = string.IsNullOrEmpty(resourceText) ? Array.Empty<string>() : resourceText.Split('|');
            if (!ids.Add(id) || resources.Length != Int(row, "resource_count", 0))
                throw new InvalidDataException($"terrain-spawn.csv has invalid duplicate/resources at {id}.");
            weights[terrain] = weights.TryGetValue(terrain, out var sum) ? sum + weight : weight;

            var asset = LoadOrCreate<TerrainSpawnDefinition>("TerrainSpawns", SafeFileId(id));
            var so = new SerializedObject(asset);
            Set(so, "id", id); Set(so, "terrainId", terrain);
            Set(so, "terrainDisplayName", Required(row, "terrain_ko")); Set(so, "yokaiId", yokai);
            Set(so, "yokaiDisplayName", Required(row, "yokai_ko")); Set(so, "weight", weight);
            Set(so, "implemented", Bool01(row, "implemented")); Set(so, "note", Required(row, "note"));
            SetStringArray(so.FindProperty("terrainResourceIds"), resources); Apply(so, asset);
        }
        if (weights.Count != 10 || weights.Any(pair => pair.Value != 0 && pair.Value != 100))
            throw new InvalidDataException("terrain-spawn.csv must contain 10 terrain tables whose weights total 0 or 100.");
    }

    private static void ImportTalismans()
    {
        var rows = Read("talismans.csv", 5);
        foreach (var row in rows)
        {
            var id = Required(row, "id");
            var encoded = Required(row, "materials").Split(',');
            var materials = new List<TalismanMaterial>();
            var itemIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var token in encoded)
            {
                var pair = token.Split(':');
                if (pair.Length != 2 || !itemIds.Add(pair[0]) ||
                    !int.TryParse(pair[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount) ||
                    amount <= 0)
                    throw new InvalidDataException($"talismans.csv '{id}' has invalid materials '{token}'.");
                materials.Add(new TalismanMaterial { itemId = pair[0], amount = amount });
            }

            var asset = LoadOrCreate<TalismanDefinition>("Talismans", id);
            var so = new SerializedObject(asset);
            Set(so, "id", id); Set(so, "displayName", Required(row, "name_ko"));
            Set(so, "form", Required(row, "form")); Set(so, "stationId", Required(row, "station_id"));
            var property = so.FindProperty("materials"); property.arraySize = materials.Count;
            for (var i = 0; i < materials.Count; i++)
            {
                var element = property.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("itemId").stringValue = materials[i].itemId;
                element.FindPropertyRelative("amount").intValue = materials[i].amount;
            }
            Set(so, "effect", Required(row, "effect_ko")); Set(so, "rationale", Required(row, "why_ko"));
            Set(so, "note", Required(row, "note")); Apply(so, asset);

            // talismans.csv is the single source for the five craftable inventory products.
            var isPlaceable = string.Equals(Required(row, "form"), "설치물", StringComparison.Ordinal);
            var item = LoadOrCreate<ItemDefinition>("Items", id);
            var itemSo = new SerializedObject(item);
            Set(itemSo, "id", id); Set(itemSo, "displayName", Required(row, "name_ko"));
            Set(itemSo, "maxStack", 99);
            itemSo.FindProperty("category").enumValueIndex = (int)(isPlaceable
                ? ItemCategory.Placeable : ItemCategory.Material);
            itemSo.FindProperty("mvpScope").enumValueIndex = (int)ItemMvpScope.A;
            Set(itemSo, "note", Required(row, "effect_ko")); Apply(itemSo, item);

            var ingredients = new ItemAmount[materials.Count];
            for (var i = 0; i < materials.Count; i++)
            {
                var materialItem = AssetDatabase.LoadAssetAtPath<ItemDefinition>(
                    $"{SoRoot}/Items/{materials[i].itemId}.asset");
                if (materialItem == null)
                    throw new InvalidDataException($"talismans.csv '{id}' material item is missing: {materials[i].itemId}.");
                ingredients[i] = new ItemAmount { item = materialItem, amount = materials[i].amount };
            }
            var station = Required(row, "station_id") switch
            {
                "workbench" => Nyangbingo.Core.CraftingStation.Workbench,
                "ice_anvil" => Nyangbingo.Core.CraftingStation.IceAnvil,
                _ => throw new InvalidDataException($"talismans.csv '{id}' has unknown station_id.")
            };
            var recipe = LoadOrCreate<RecipeDefinition>("Recipes", id);
            var recipeSo = new SerializedObject(recipe);
            Set(recipeSo, "id", id);
            recipeSo.FindProperty("station").enumValueIndex = (int)station;
            recipeSo.FindProperty("durationSeconds").floatValue = 0f;
            recipeSo.FindProperty("type").enumValueIndex = (int)(isPlaceable
                ? RecipeType.Placeable : RecipeType.Util);
            recipeSo.FindProperty("mvpScope").enumValueIndex = (int)ItemMvpScope.A;
            Set(recipeSo, "note", Required(row, "effect_ko"));
            var ingredientsProperty = recipeSo.FindProperty("ingredients");
            ingredientsProperty.arraySize = ingredients.Length;
            for (var i = 0; i < ingredients.Length; i++)
            {
                var element = ingredientsProperty.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("item").objectReferenceValue = ingredients[i].item;
                element.FindPropertyRelative("amount").intValue = ingredients[i].amount;
            }
            var output = recipeSo.FindProperty("output");
            output.FindPropertyRelative("item").objectReferenceValue = item;
            output.FindPropertyRelative("amount").intValue = 1;
            Apply(recipeSo, recipe);
        }
    }

    private static void ImportCodex()
    {
        var rows = Read("codex.csv", 17);
        foreach (var row in rows)
        {
            var id = Required(row, "id");
            var asset = LoadOrCreate<CodexEntryDefinition>("Codex", id);
            var so = new SerializedObject(asset);
            Set(so, "id", id); Set(so, "kind", Required(row, "kind"));
            Set(so, "displayName", Required(row, "name_ko")); Set(so, "source", Required(row, "source_ko"));
            Set(so, "sourceVerification", Required(row, "source_verified"));
            Set(so, "cardFrontAssetId", Required(row, "card_front_asset"));
            Set(so, "cardBackText", Required(row, "card_back_text_ko"));
            Set(so, "portraitAssetId", row["portrait_asset"]); Set(so, "note", Required(row, "note"));
            Apply(so, asset);
        }
    }

    private static void ImportTraits()
    {
        var rows = Read("traits.csv", 4);
        foreach (var row in rows)
        {
            var id = Required(row, "id");
            var asset = LoadOrCreate<TraitDefinition>("Traits", id);
            var so = new SerializedObject(asset);
            Set(so, "id", id); Set(so, "displayName", Required(row, "name_ko"));
            Set(so, "shortName", Required(row, "name_short")); Set(so, "hookField", Required(row, "hook_field"));
            Set(so, "hookSource", Required(row, "hook_source")); Set(so, "startItemId", row["start_item_id"]);
            Set(so, "effect", Required(row, "effect_value")); Set(so, "artAssetId", Required(row, "art_asset"));
            Set(so, "note", Required(row, "note")); Apply(so, asset);
        }
    }

    private static void ImportCrops()
    {
        var rows = Read("crops.csv", 10);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var zone = Required(row, "zone_id");
            var crop = Required(row, "crop_id");
            if (Int(row, "order", 1) != i + 1)
                throw new InvalidDataException($"crops.csv row {i + 2} has invalid order.");
            var id = zone + ":" + crop;
            var asset = LoadOrCreate<CropDefinition>("Crops", SafeFileId(id));
            var so = new SerializedObject(asset);
            Set(so, "id", id); Set(so, "zoneId", zone); Set(so, "order", i + 1);
            Set(so, "cropId", crop); Set(so, "displayName", Required(row, "crop_ko"));
            Set(so, "spawnPerHundredTiles", Int(row, "spawn_per_100tiles", 0));
            Set(so, "healHitPoints", Int(row, "heal_hp", 0));
            Set(so, "respawnDays", Int(row, "respawn_days", 1)); Set(so, "plantable", Bool01(row, "plantable"));
            Set(so, "riskNote", Required(row, "risk_note")); Set(so, "note", Required(row, "note"));
            Apply(so, asset);
        }
    }

    private static void ValidateContentStatus()
    {
        var rows = Read("content-status.csv", 858);
        var statuses = new HashSet<string>(new[] { "구현됨", "수정예정", "추가예정" }, StringComparer.Ordinal);
        if (rows.Any(row => string.IsNullOrWhiteSpace(row["file"]) ||
                            string.IsNullOrWhiteSpace(row["row_key"]) ||
                            string.IsNullOrWhiteSpace(row["row_id"]) ||
                            !statuses.Contains(row["status"])))
            throw new InvalidDataException("content-status.csv contains an invalid editor-reference row.");
    }

    private static List<Dictionary<string, string>> Read(string file, int expectedRows)
    {
        var path = Path.Combine(CsvRoot, file);
        var rows = NyangbingoCsvUtility.ReadRows(path);
        if (rows.Count != expectedRows)
            throw new InvalidDataException($"{file} must contain {expectedRows} rows, found {rows.Count}.");
        return rows;
    }

    private static T LoadOrCreate<T>(string directoryName, string fileId) where T : ScriptableObject
    {
        var directory = SoRoot + "/" + directoryName;
        EnsureFolder(directory);
        var path = directory + "/" + fileId + ".asset";
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void EnsureFolder(string path)
    {
        var parts = path.Split('/');
        var current = parts[0];
        for (var i = 1; i < parts.Length; i++)
        {
            var next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static string Required(Dictionary<string, string> row, string key)
    {
        if (!row.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException($"Required CSV value '{key}' is blank.");
        return value.Trim();
    }

    private static int Int(Dictionary<string, string> row, string key, int minimum, int maximum = int.MaxValue)
    {
        if (!int.TryParse(Required(row, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ||
            value < minimum || value > maximum)
            throw new InvalidDataException($"CSV value '{key}' is not an integer in [{minimum}, {maximum}].");
        return value;
    }

    private static float Float(Dictionary<string, string> row, string key, float minimum,
        float maximum = float.MaxValue)
    {
        if (!float.TryParse(Required(row, key), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
            float.IsNaN(value) || float.IsInfinity(value) || value < minimum || value > maximum)
            throw new InvalidDataException($"CSV value '{key}' is not finite in [{minimum}, {maximum}].");
        return value;
    }

    private static bool Bool01(Dictionary<string, string> row, string key)
    {
        var value = Required(row, key);
        if (value == "1") return true;
        if (value == "0") return false;
        throw new InvalidDataException($"CSV value '{key}' must be 0 or 1.");
    }

    private static string SafeFileId(string id) => id.Replace(':', '_');
    private static void Set(SerializedObject so, string property, string value) =>
        so.FindProperty(property).stringValue = value ?? string.Empty;
    private static void Set(SerializedObject so, string property, int value) =>
        so.FindProperty(property).intValue = value;
    private static void Set(SerializedObject so, string property, float value) =>
        so.FindProperty(property).floatValue = value;
    private static void Set(SerializedObject so, string property, bool value) =>
        so.FindProperty(property).boolValue = value;

    private static void SetStringArray(SerializedProperty property, IReadOnlyList<string> values)
    {
        property.arraySize = values.Count;
        for (var i = 0; i < values.Count; i++) property.GetArrayElementAtIndex(i).stringValue = values[i];
    }

    private static void Apply(SerializedObject serialized, UnityEngine.Object asset)
    {
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
    }
}
