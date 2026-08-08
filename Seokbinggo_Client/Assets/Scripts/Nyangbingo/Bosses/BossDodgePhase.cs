using System;
using System.Globalization;
using Nyangbingo.Data;
using UnityEngine;

namespace Nyangbingo.Bosses
{
    /// <summary>
    /// v38/v46 보스 회피 구간. 곡선 초 동안 보스 피해 배율 0.
    /// </summary>
    public static class BossDodgePhase
    {
        private static readonly float[] DefaultCurveSeconds =
        {
            20f, 30f, 40f, 60f, 70f, 80f, 90f, 100f, 110f, 120f
        };

        public static float DodgeSecondsForBossIndex(int bossIndexZeroBased, GlobalSettings settings)
        {
            var curve = ResolveCurve(settings);
            if (curve.Length == 0) return 0f;
            var index = Mathf.Clamp(bossIndexZeroBased, 0, curve.Length - 1);
            return Mathf.Max(0f, curve[index]);
        }

        public static bool IsInDodgeWindow(float fightElapsedSeconds, float dodgeWindowSeconds) =>
            dodgeWindowSeconds > 0f &&
            fightElapsedSeconds >= 0f &&
            fightElapsedSeconds < dodgeWindowSeconds;

        public static float DamageMultiplier(float fightElapsedSeconds, float dodgeWindowSeconds) =>
            IsInDodgeWindow(fightElapsedSeconds, dodgeWindowSeconds) ? 0f : 1f;

        private static float[] ResolveCurve(GlobalSettings settings)
        {
            var raw = settings?.GetString(GlobalKeys.BossDodgeSecCurve);
            if (string.IsNullOrWhiteSpace(raw)) return DefaultCurveSeconds;

            var parts = raw.Split(new[] { '/', ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return DefaultCurveSeconds;
            var curve = new float[parts.Length];
            for (var i = 0; i < parts.Length; i++)
            {
                if (!float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture,
                        out var value) ||
                    float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
                    return DefaultCurveSeconds;
                curve[i] = value;
            }

            return curve;
        }
    }
}
