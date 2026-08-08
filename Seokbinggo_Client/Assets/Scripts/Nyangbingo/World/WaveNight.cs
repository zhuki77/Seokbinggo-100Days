using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>v45/v46 night-waves.csv 한 행.</summary>
    public readonly struct NightWaveRow
    {
        public NightWaveRow(
            string band, int tier, int wave, float mult, string composition,
            int effectiveHp, int dayMin, int dayMax)
        {
            Band = band ?? string.Empty;
            Tier = tier;
            Wave = wave;
            Mult = mult;
            Composition = composition ?? string.Empty;
            EffectiveHp = effectiveHp;
            DayMin = dayMin;
            DayMax = dayMax;
        }

        public string Band { get; }
        public int Tier { get; }
        public int Wave { get; }
        public float Mult { get; }
        public string Composition { get; }
        public int EffectiveHp { get; }
        public int DayMin { get; }
        public int DayMax { get; }
    }

    /// <summary>night-waves 행 집합. CSV/SO 로드 결과는 이 순수 C# 테이블로 보관한다.</summary>
    public sealed class NightWaveTable
    {
        private readonly List<NightWaveRow> rows;

        public NightWaveTable(IEnumerable<NightWaveRow> source)
        {
            rows = new List<NightWaveRow>();
            if (source == null) return;
            foreach (var row in source)
                rows.Add(row);
        }

        public IReadOnlyList<NightWaveRow> Rows => rows;

        public bool TryGet(string band, int wave, out NightWaveRow row)
        {
            for (var i = 0; i < rows.Count; i++)
            {
                if (!string.Equals(rows[i].Band, band, StringComparison.Ordinal) || rows[i].Wave != wave)
                    continue;
                row = rows[i];
                return true;
            }

            row = default;
            return false;
        }
    }

    /// <summary>v45/v46 확장 밤 웨이브(대밤) 규칙.</summary>
    public static class WaveNight
    {
        public const string BandT4 = "T4";
        public const string BandT5 = "T5";
        public const string BandT6 = "T6";

        public static string BandOf(int day)
        {
            if (day < 31) return null;
            if (day <= 50) return BandT4;
            if (day <= 70) return BandT5;
            if (day <= 100) return BandT6;
            return null;
        }

        public static bool IsBigNight(int day, int period = 10, int offset = 5) =>
            day >= 31 && period > 0 && day % period == offset;

        /// <param name="thresholdSec">globals <c>wave_threshold_sec</c>. 하드코딩 금지 — 호출부가 globals에서 읽는다.</param>
        public static int CurrentWave(float nightElapsedSec, int scoreWave, float thresholdSec)
        {
            if (thresholdSec <= 0f || float.IsNaN(thresholdSec) || float.IsInfinity(thresholdSec))
                return Mathf.Max(0, scoreWave);

            var elapsed = Mathf.Max(0f, nightElapsedSec);
            var timeWave = Mathf.CeilToInt(elapsed / thresholdSec);
            return Mathf.Max(scoreWave, timeWave);
        }

        public static Dictionary<string, int> CompositionFor(NightWaveTable table, int day, int wave)
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            var band = BandOf(day);
            if (table == null || string.IsNullOrEmpty(band) || wave < 1)
                return result;

            if (!table.TryGet(band, wave, out var row))
                return result;

            ParseComposition(row.Composition, result);
            return result;
        }

        public static IReadOnlyList<(string id, int count)> CompositionListFor(
            NightWaveTable table, int day, int wave)
        {
            var map = CompositionFor(table, day, wave);
            var list = new List<(string id, int count)>(map.Count);
            foreach (var pair in map)
                list.Add((pair.Key, pair.Value));
            return list;
        }

        /// <summary>hp_only 규칙: baseHp * (1 + 0.25 * (wave - 1)).</summary>
        public static float ApplyHpMult(float baseHp, int wave)
        {
            var safeWave = Mathf.Max(1, wave);
            return baseHp * (1f + 0.25f * (safeWave - 1));
        }

        public static void ParseComposition(string composition, IDictionary<string, int> into)
        {
            if (into == null || string.IsNullOrWhiteSpace(composition)) return;

            var parts = composition.Split(',');
            for (var i = 0; i < parts.Length; i++)
            {
                var token = parts[i].Trim();
                if (token.Length == 0) continue;
                var colon = token.IndexOf(':');
                if (colon <= 0 || colon >= token.Length - 1) continue;
                var id = token.Substring(0, colon).Trim();
                var countText = token.Substring(colon + 1).Trim();
                if (string.IsNullOrEmpty(id)) continue;
                if (!int.TryParse(countText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) ||
                    count <= 0)
                    continue;

                if (into.TryGetValue(id, out var existing))
                    into[id] = existing + count;
                else
                    into[id] = count;
            }
        }

        /// <summary>night-waves.csv v45/v46 정본 15행. SO 임포트 전에도 런타임이 동일 표를 쓸 수 있게 한다.</summary>
        public static NightWaveTable CreateCanonicalV46Table() =>
            new NightWaveTable(new[]
            {
                new NightWaveRow("T4", 4, 1, 1.00f, "club:4,bulgasari:2,yakwang:2", 2880, 31, 50),
                new NightWaveRow("T4", 4, 2, 1.25f, "club:3,bulgasari:2,yakwang:2,eoduksini:1", 4262, 31, 50),
                new NightWaveRow("T4", 4, 3, 1.50f, "club:2,bulgasari:3,yakwang:1,eoduksini:2", 6810, 31, 50),
                new NightWaveRow("T4", 4, 4, 1.75f, "club:1,bulgasari:3,yakwang:1,eoduksini:3", 8872, 31, 50),
                new NightWaveRow("T4", 4, 5, 2.00f, "bulgasari:4,eoduksini:4", 12400, 31, 50),
                new NightWaveRow("T5", 5, 1, 1.00f, "club:4,yakwang:2,eoduksini:2", 2980, 51, 70),
                new NightWaveRow("T5", 5, 2, 1.25f, "club:2,bulgasari:2,yakwang:2,eoduksini:2", 4925, 51, 70),
                new NightWaveRow("T5", 5, 3, 1.50f, "club:2,bulgasari:3,yakwang:1,eoduksini:2", 6810, 51, 70),
                new NightWaveRow("T5", 5, 4, 1.75f, "bulgasari:4,yakwang:1,eoduksini:3", 9712, 51, 70),
                new NightWaveRow("T5", 5, 5, 2.00f, "bulgasari:3,eoduksini:5", 12500, 51, 70),
                new NightWaveRow("T6", 6, 1, 1.00f, "club:2,bulgasari:1,yakwang:2,eoduksini:3", 3990, 71, 100),
                new NightWaveRow("T6", 6, 2, 1.25f, "club:2,yakwang:2,eoduksini:4", 5050, 71, 100),
                new NightWaveRow("T6", 6, 3, 1.50f, "club:2,yakwang:1,eoduksini:5", 7035, 71, 100),
                new NightWaveRow("T6", 6, 4, 1.75f, "bulgasari:2,yakwang:1,eoduksini:5", 9888, 71, 100),
                new NightWaveRow("T6", 6, 5, 2.00f, "eoduksini:8", 12800, 71, 100)
            });
    }
}
