using System;
using System.Collections.Generic;
using System.Globalization;
using Nyangbingo.Core;
using Nyangbingo.Data;

namespace Nyangbingo.World
{
    /// <summary>
    /// v46 WaveNight 순수 규칙 — 수치/표는 CSV·globals에서만 읽는다.
    /// </summary>
    public static class WaveNightRules
    {
        public const int RegularNightMaxWave = 2;
        public const int BigNightMaxWave = 5;

        public readonly struct WaveRow
        {
            public WaveRow(string band, int dayMin, int dayMax, int wave, float mult, string composition,
                int effectiveHp)
            {
                Band = band ?? string.Empty;
                DayMin = dayMin;
                DayMax = dayMax;
                Wave = wave;
                Mult = mult;
                Composition = composition ?? string.Empty;
                EffectiveHp = effectiveHp;
            }

            public string Band { get; }
            public int DayMin { get; }
            public int DayMax { get; }
            public int Wave { get; }
            public float Mult { get; }
            public string Composition { get; }
            public int EffectiveHp { get; }
        }

        public static bool UsesNightWaves(int day) => day >= 31;

        public static bool IsBigNight(int day, int period, int offset)
        {
            if (day < 31 || period <= 0) return false;
            var mod = day % period;
            if (mod < 0) mod += period;
            return mod == offset;
        }

        public static int MaxWaveForNight(bool isBigNight) =>
            isBigNight ? BigNightMaxWave : RegularNightMaxWave;

        public static string BandOf(int day)
        {
            if (day <= 50) return "T4";
            if (day <= 70) return "T5";
            return "T6";
        }

        public static int CurrentWave(float nightElapsedSec, int scoreWave, float advanceSec)
        {
            if (advanceSec <= 0f) advanceSec = 108f;
            var timeWave = (int)Math.Ceiling(Math.Max(0f, nightElapsedSec) / advanceSec);
            if (timeWave < 1) timeWave = 1;
            return Math.Max(Math.Max(1, scoreWave), timeWave);
        }

        public static float MultForWave(int wave) => 1f + 0.25f * Math.Max(0, wave - 1);

        public static float ApplyHpMult(float baseHp, float mult, string waveMultTarget)
        {
            if (!string.Equals(waveMultTarget, "hp_only", StringComparison.OrdinalIgnoreCase))
                return baseHp;
            return Math.Max(1f, baseHp * Math.Max(0f, mult));
        }

        public static int FreeSlots(int yokaiCap, int aliveCount) =>
            Math.Max(0, yokaiCap - Math.Max(0, aliveCount));

        public static bool TryFindRow(IReadOnlyList<WaveRow> rows, int day, int wave, out WaveRow row)
        {
            row = default;
            if (rows == null || day < 31 || wave < 1) return false;
            for (var i = 0; i < rows.Count; i++)
            {
                var candidate = rows[i];
                if (day < candidate.DayMin || day > candidate.DayMax) continue;
                if (candidate.Wave != wave) continue;
                row = candidate;
                return true;
            }

            return false;
        }

        public static DayCurveSpawnAmount[] ParseComposition(string composition)
        {
            if (string.IsNullOrWhiteSpace(composition)) return Array.Empty<DayCurveSpawnAmount>();
            var parts = composition.Split(',');
            var list = new List<DayCurveSpawnAmount>(parts.Length);
            for (var i = 0; i < parts.Length; i++)
            {
                var token = parts[i].Trim().Trim('"');
                if (token.Length == 0) continue;
                var colon = token.IndexOf(':');
                if (colon <= 0 || colon >= token.Length - 1) continue;
                var id = token.Substring(0, colon).Trim();
                if (!int.TryParse(token.Substring(colon + 1).Trim(), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out var amount) ||
                    amount <= 0)
                    continue;
                if (!TryParseYokaiId(id, out var kind)) continue;
                list.Add(new DayCurveSpawnAmount { kind = kind, amount = amount });
            }

            return list.ToArray();
        }

        public static bool TryParseYokaiId(string id, out YokaiKind kind)
        {
            switch (id)
            {
                case "club": kind = YokaiKind.ClubGoblin; return true;
                case "bulgasari": kind = YokaiKind.Bulgasari; return true;
                case "yakwang": kind = YokaiKind.Yagwanggwi; return true;
                case "eoduksini": kind = YokaiKind.Eoduksini; return true;
                case "gangcheol": kind = YokaiKind.Gangcheori; return true;
                default: kind = default; return false;
            }
        }

        public static List<WaveRow> ParseCsv(string csvText)
        {
            var rows = new List<WaveRow>();
            if (string.IsNullOrWhiteSpace(csvText)) return rows;
            var lines = csvText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            if (lines.Length < 2) return rows;

            for (var lineIndex = 1; lineIndex < lines.Length; lineIndex++)
            {
                var line = lines[lineIndex].Trim();
                if (line.Length == 0) continue;
                if (!TryParseCsvLine(line, out var row)) continue;
                rows.Add(row);
            }

            return rows;
        }

        /// <summary>kit zip 없이 빌드해도 동작하도록 Notion v45 15행을 코드에 동봉한다. CSV가 있으면 CSV가 이긴다.</summary>
        public static List<WaveRow> BuiltinCanonicalRows() => new List<WaveRow>
        {
            new WaveRow("T4", 31, 50, 1, 1.00f, "club:4,bulgasari:2,yakwang:2", 2880),
            new WaveRow("T4", 31, 50, 2, 1.25f, "club:3,bulgasari:2,yakwang:2,eoduksini:1", 4262),
            new WaveRow("T4", 31, 50, 3, 1.50f, "club:2,bulgasari:3,yakwang:1,eoduksini:2", 6810),
            new WaveRow("T4", 31, 50, 4, 1.75f, "club:1,bulgasari:3,yakwang:1,eoduksini:3", 8872),
            new WaveRow("T4", 31, 50, 5, 2.00f, "bulgasari:4,eoduksini:4", 12400),
            new WaveRow("T5", 51, 70, 1, 1.00f, "club:4,yakwang:2,eoduksini:2", 2980),
            new WaveRow("T5", 51, 70, 2, 1.25f, "club:2,bulgasari:2,yakwang:2,eoduksini:2", 4925),
            new WaveRow("T5", 51, 70, 3, 1.50f, "club:2,bulgasari:3,yakwang:1,eoduksini:2", 6810),
            new WaveRow("T5", 51, 70, 4, 1.75f, "bulgasari:4,yakwang:1,eoduksini:3", 9712),
            new WaveRow("T5", 51, 70, 5, 2.00f, "bulgasari:3,eoduksini:5", 12500),
            new WaveRow("T6", 71, 100, 1, 1.00f, "club:2,bulgasari:1,yakwang:2,eoduksini:3", 3990),
            new WaveRow("T6", 71, 100, 2, 1.25f, "club:2,yakwang:2,eoduksini:4", 5050),
            new WaveRow("T6", 71, 100, 3, 1.50f, "club:2,yakwang:1,eoduksini:5", 7035),
            new WaveRow("T6", 71, 100, 4, 1.75f, "bulgasari:2,yakwang:1,eoduksini:5", 9888),
            new WaveRow("T6", 71, 100, 5, 2.00f, "eoduksini:8", 12800),
        };

        private static bool TryParseCsvLine(string line, out WaveRow row)
        {
            row = default;
            var fields = SplitCsvLine(line);
            if (fields.Count < 6) return false;
            if (!int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var dayMin) ||
                !int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var dayMax) ||
                !int.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var wave) ||
                !float.TryParse(fields[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var mult))
                return false;

            var effectiveHp = 0;
            if (fields.Count >= 7)
                int.TryParse(fields[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out effectiveHp);

            row = new WaveRow(fields[0], dayMin, dayMax, wave, mult, fields[5], effectiveHp);
            return true;
        }

        private static List<string> SplitCsvLine(string line)
        {
            var fields = new List<string>(8);
            var start = 0;
            var inQuotes = false;
            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (c == ',' && !inQuotes)
                {
                    fields.Add(Unquote(line.Substring(start, i - start)));
                    start = i + 1;
                }
            }

            fields.Add(Unquote(line.Substring(start)));
            return fields;
        }

        private static string Unquote(string value)
        {
            value = value?.Trim() ?? string.Empty;
            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
                return value.Substring(1, value.Length - 2);
            return value;
        }
    }
}
