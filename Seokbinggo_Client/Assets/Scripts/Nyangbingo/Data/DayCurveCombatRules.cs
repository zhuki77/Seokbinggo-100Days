using UnityEngine;

namespace Nyangbingo.Data
{
    /// <summary>
    /// day-curve-ext 확장 파라미터(variant_mult, heat_seep)를 전투·체온 런타임에 적용한다.
    /// </summary>
    public static class DayCurveCombatRules
    {
        public static bool UsesVariantHpMultiplier(GameDataCatalog catalog) =>
            catalog != null &&
            string.Equals(catalog.FindGlobal("wave_mult_target")?.Value, "hp_only",
                System.StringComparison.Ordinal);

        public static int ResolveYokaiHitPoints(
            GameDataCatalog catalog, DayCurveDefinition curve, int baseHitPoints)
        {
            if (baseHitPoints <= 0 || curve == null || !UsesVariantHpMultiplier(catalog))
                return baseHitPoints;
            var multiplier = curve.VariantMultiplier;
            if (multiplier <= 1f || float.IsNaN(multiplier) || float.IsInfinity(multiplier))
                return baseHitPoints;
            return Mathf.Max(1, Mathf.RoundToInt(baseHitPoints * multiplier));
        }

        public static float ApplyHeatSeepPenalty(
            float recoveryMultiplier, GameDataCatalog catalog, DayCurveDefinition curve)
        {
            if (recoveryMultiplier <= 0f || curve == null || curve.Day <= 30)
                return recoveryMultiplier;
            var seepPercent = curve.HeatSeepPercent;
            if (seepPercent <= 0f || float.IsNaN(seepPercent) || float.IsInfinity(seepPercent))
                return recoveryMultiplier;
            return recoveryMultiplier * Mathf.Clamp01(1f - seepPercent / 100f);
        }
    }
}
