using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Nyangbingo.Data
{
    public static class GlobalKeys
    {
        public const string DayLengthSeconds = "day_length_sec";
        public const string NightLengthSeconds = "night_length_sec";
        public const string DayTotalSeconds = "day_total_sec";
        public const string MvpDays = "mvp_days";
        public const string TotalDays = "total_days";
        public const string StartAtNight = "start_at_night";
        public const string BaekjungDay = "baekjung_day";
        public const string SealWindowRadiusX = "seal_window_rx";
        public const string SealWindowRadiusY = "seal_window_ry";
        public const string SealCap = "seal_cap";
        public const string SealTargetCells = "seal_target_cells";
        public const string BossSavePolicy = "boss_save_policy";
        public const string BaekjungWaveOverflow = "baekjung_wave_overflow";
    }

    [CreateAssetMenu(menuName = "Nyangbingo/Data/Global Value")]
    public sealed class GlobalDefinition : ScriptableObject
    {
        [SerializeField] private string key;
        [SerializeField] private string value;
        [SerializeField] private string unit;
        [TextArea][SerializeField] private string note;

        public string Key => key;
        public string Value => value;
        public string Unit => unit;
        public string Note => note;

        public bool TryGetInt(out int result) =>
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

        public bool TryGetFloat(out float result) =>
            float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) &&
            !float.IsNaN(result) && !float.IsInfinity(result);

        public bool TryGetBool(out bool result)
        {
            if (unit != "bool") { result = false; return false; }
            if (value == "1") { result = true; return true; }
            if (value == "0") { result = false; return true; }
            result = false;
            return false;
        }
    }

    public sealed class GlobalSettings
    {
        private readonly Dictionary<string, GlobalDefinition> definitionsByKey =
            new Dictionary<string, GlobalDefinition>(StringComparer.Ordinal);

        public GlobalSettings(IReadOnlyList<GlobalDefinition> definitions)
        {
            IsValid = definitions != null && definitions.Count > 0;
            if (definitions == null) return;
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.Key) ||
                    string.IsNullOrWhiteSpace(definition.Value) || string.IsNullOrWhiteSpace(definition.Unit) ||
                    !definitionsByKey.TryAdd(definition.Key, definition))
                    IsValid = false;
            }
        }

        public bool IsValid { get; private set; }
        public int Count => definitionsByKey.Count;
        public GlobalDefinition Find(string key) =>
            !string.IsNullOrEmpty(key) && definitionsByKey.TryGetValue(key, out var definition) ? definition : null;
        public bool TryGetInt(string key, out int value)
        {
            var definition = Find(key);
            if (definition != null) return definition.TryGetInt(out value);
            value = default;
            return false;
        }
        public bool TryGetFloat(string key, out float value)
        {
            var definition = Find(key);
            if (definition != null) return definition.TryGetFloat(out value);
            value = default;
            return false;
        }
        public bool TryGetBool(string key, out bool value)
        {
            var definition = Find(key);
            if (definition != null) return definition.TryGetBool(out value);
            value = default;
            return false;
        }
        public string GetString(string key) => Find(key)?.Value;
    }
}
