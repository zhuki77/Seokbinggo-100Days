using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Nyangbingo.Data;
using UnityEditor;
using UnityEngine;

public static class NyangbingoV86GuideImporter
{
    private const string Root = "Assets/Data/SO";
    private static readonly HashSet<string> CompletionStates = new HashSet<string>(StringComparer.Ordinal)
    {
        "station_installed", "item_count_ge", "module_installed", "core_sealed", "storage_contains",
        "storage_condition_met", "slept", "dawn_storage_result", "item_owned", "boss_defeated",
        "core_modules_complete"
    };

    [MenuItem("Nyangbingo/Reimport v86 Guide CSVs")]
    public static void Reimport()
    {
        var goals = NyangbingoCsvUtility.ReadRows("Assets/Data/CSV/goals.csv");
        var messages = NyangbingoCsvUtility.ReadRows("Assets/Data/CSV/guide-messages.csv");
        if (goals.Count != 15 || messages.Count != 38)
            throw new InvalidDataException("v86 + S8 goals/messages must contain 15/38 rows.");
        var messageIds = new HashSet<string>(messages.Select(row => Required(row, "id")), StringComparer.Ordinal);
        if (messageIds.Count != messages.Count ||
            goals.Select(row => Required(row, "id")).Distinct(StringComparer.Ordinal).Count() != goals.Count)
            throw new InvalidDataException("Duplicate guide or goal ID.");
        var itemIds = new HashSet<string>(NyangbingoCsvUtility.ReadRows("Assets/Data/CSV/items.csv")
            .Select(row => Required(row, "id")), StringComparer.Ordinal);
        var bossIds = new HashSet<string>(NyangbingoCsvUtility.ReadRows("Assets/Data/CSV/bosses.csv")
            .Select(row => Required(row, "id")), StringComparer.Ordinal);
        var orders = new HashSet<int>();
        foreach (var row in goals)
        {
            if (!CompletionStates.Contains(Required(row, "complete_state")) ||
                !orders.Add(Integer(row, "order", 1, goals.Count)) ||
                !messageIds.Contains(Required(row, "hint_msg")) ||
                !(itemIds.Contains(Required(row, "icon_ref")) || bossIds.Contains(row["icon_ref"])) ||
                (Required(row, "phase") != "P0" && row["phase"] != "P1") ||
                (Required(row, "track") != "auto" && row["track"] != "select"))
                throw new InvalidDataException("Invalid goal contract: " + row["id"]);
            foreach (var name in new[] { "name_ko", "complete_param", "guide_target", "mvp_scope", "note" })
                Required(row, name);
        }
        foreach (var row in messages)
        {
            Integer(row, "tier", 1, 4);
            foreach (var name in new[] { "trigger_state", "text_ko", "repeat", "source" })
                Required(row, name);
            // Cause fragments are embedded in dawn_lost and have no independent clear condition.
            if (row["repeat"] != "fragment") Required(row, "clear_when");
            if (!row.ContainsKey("clear_when") || !row.ContainsKey("vars") || !row.ContainsKey("note"))
                throw new InvalidDataException("Missing guide message columns.");
        }
        // Validate the whole input before editing any existing asset.
        foreach (var row in goals)
        {
            var asset = LoadOrCreate<GoalDefinition>("Goals", row["id"]);
            var so = new SerializedObject(asset);
            Set(so, "id", row["id"]); so.FindProperty("order").intValue = Integer(row, "order", 1, goals.Count);
            Set(so, "phase", row["phase"]); Set(so, "track", row["track"]);
            Set(so, "displayName", row["name_ko"]); Set(so, "iconRef", row["icon_ref"]);
            Set(so, "completeState", row["complete_state"]); Set(so, "completeParam", row["complete_param"]);
            Set(so, "guideTarget", row["guide_target"]); Set(so, "hintMessageId", row["hint_msg"]);
            Set(so, "mvpScope", row["mvp_scope"]);
            Set(so, "note", row["note"]); Save(so, asset);
        }
        foreach (var row in messages)
        {
            var asset = LoadOrCreate<GuideMessageDefinition>("GuideMessages", row["id"]);
            var so = new SerializedObject(asset);
            Set(so, "id", row["id"]); so.FindProperty("tier").intValue = Integer(row, "tier", 1, 4);
            Set(so, "triggerState", row["trigger_state"]); Set(so, "text", row["text_ko"]);
            Set(so, "variables", row["vars"]); Set(so, "clearWhen", row["clear_when"]);
            Set(so, "repeat", row["repeat"]); Set(so, "source", row["source"]); Set(so, "note", row["note"]);
            Save(so, asset);
        }
        Debug.Log($"[Nyangbingo] v86 guide import completed: {goals.Count} goals, {messages.Count} messages.");
    }

    private static string Required(Dictionary<string, string> row, string column)
    {
        if (!row.TryGetValue(column, out var value) || string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException("Missing guide value: " + column);
        return value.Trim();
    }

    private static int Integer(Dictionary<string, string> row, string column, int min, int max)
    {
        if (!int.TryParse(Required(row, column), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ||
            n < min || n > max) throw new InvalidDataException("Invalid guide integer: " + column);
        return n;
    }

    private static T LoadOrCreate<T>(string directory, string id) where T : ScriptableObject
    {
        if (id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new InvalidDataException("Unsafe guide ID: " + id);
        var folder = Root + "/" + directory;
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(Root, directory);
        var path = folder + "/" + id + ".asset";
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void Set(SerializedObject so, string field, string value) =>
        so.FindProperty(field).stringValue = value;

    private static void Save(SerializedObject so, UnityEngine.Object asset)
    {
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssetIfDirty(asset);
    }
}
