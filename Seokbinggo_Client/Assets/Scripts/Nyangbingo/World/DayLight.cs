using System;
using System.Globalization;
using Nyangbingo.Data;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>
    /// v46 주광 강도 헬퍼. 낮 색은 #FFB080 고정, 강도만 더위 단계 커브(globals)를 탄다.
    /// 밤·지하·백중 강도는 이 커브와 분리된 상수다(밝기 누수 방지).
    /// </summary>
    public static class DayLight
    {
        public const float NightIntensity = 0.55f;
        public const float UndergroundIntensity = 0.35f;
        public const float BaekjungIntensity = 0.55f;

        public static readonly Color DayColor = new Color(1f, 176f / 255f, 128f / 255f, 1f);

        private static readonly float[] DefaultBrightnessByStage =
        {
            1.00f, 1.05f, 1.10f, 1.15f, 1.20f, 1.25f, 1.30f, 1.35f, 1.40f, 1.45f
        };

        private static bool invariantsChecked;

        public static float IntensityFor(
            int day, bool isDay, bool isSurface, bool isBaekjung, GlobalSettings settings)
        {
            if (!isDay)
                return isBaekjung ? BaekjungIntensity : NightIntensity;
            if (!isSurface)
                return UndergroundIntensity;

            var period = 10;
            if (settings != null &&
                settings.TryGetInt(GlobalKeys.HeatStagePeriod, out var configuredPeriod) &&
                configuredPeriod > 0)
                period = configuredPeriod;

            var safeDay = Mathf.Max(1, day);
            var stage = Mathf.Min(10, (safeDay - 1) / period + 1);
            var curve = ResolveBrightnessCurve(settings);
            var index = Mathf.Clamp(stage - 1, 0, curve.Length - 1);
            return curve[index];
        }

        public static void AssertInvariants()
        {
            if (invariantsChecked) return;
            invariantsChecked = true;

            const float epsilon = 0.0001f;
            if (Mathf.Abs(NightIntensity - 0.55f) > epsilon ||
                Mathf.Abs(UndergroundIntensity - 0.35f) > epsilon ||
                Mathf.Abs(BaekjungIntensity - 0.55f) > epsilon)
            {
                Debug.LogError(
                    "DayLight invariants broken: Night/Underground/Baekjung intensity constants mismatch.");
            }

            if (!ColorsMatch(DayColor, new Color(1f, 176f / 255f, 128f / 255f, 1f), epsilon))
                Debug.LogError("DayLight invariants broken: DayColor must be fixed #FFB080.");

            if (DefaultBrightnessByStage.Length != 10)
            {
                throw new InvalidOperationException(
                    "DayLight default brightness curve must have exactly 10 stages.");
            }
        }

        private static float[] ResolveBrightnessCurve(GlobalSettings settings)
        {
            var raw = settings?.GetString(GlobalKeys.DayBrightnessByStage);
            if (string.IsNullOrWhiteSpace(raw))
                return DefaultBrightnessByStage;

            var separators = new[] { '/', ',' };
            var parts = raw.Split(separators, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return DefaultBrightnessByStage;

            var curve = new float[parts.Length];
            for (var i = 0; i < parts.Length; i++)
            {
                if (!float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture,
                        out var value) ||
                    float.IsNaN(value) || float.IsInfinity(value))
                    return DefaultBrightnessByStage;
                curve[i] = value;
            }

            return curve.Length > 0 ? curve : DefaultBrightnessByStage;
        }

        private static bool ColorsMatch(Color a, Color b, float epsilon) =>
            Mathf.Abs(a.r - b.r) <= epsilon &&
            Mathf.Abs(a.g - b.g) <= epsilon &&
            Mathf.Abs(a.b - b.b) <= epsilon &&
            Mathf.Abs(a.a - b.a) <= epsilon;
    }
}
