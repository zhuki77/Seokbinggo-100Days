using UnityEngine;

namespace Nyangbingo.Data
{
    /// <summary>
    /// day-curve-ext 확장 파라미터(variant_mult, heat_seep, ice_melt_dps)를 전투·체온·야외 얼음에 적용한다.
    /// ice_melt_dps 열 이름은 레거시이며, 실제 단위는 storage_melt_per_day와 동일한 일일 비율(0~1)이다.
    /// storage_melt_per_day(0.25) = 장독 빙결 미달 시, ice_melt_dps(0.15) = 지표 직사 노출 얼음.
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

        public static float ResolveOutdoorIceMeltPerDay(DayCurveDefinition curve)
        {
            if (curve == null || curve.Day <= 30) return 0f;
            var meltPerDay = curve.IceMeltDpsPerDay;
            if (meltPerDay <= 0f || float.IsNaN(meltPerDay) || float.IsInfinity(meltPerDay))
                return 0f;
            return Mathf.Clamp01(meltPerDay);
        }

        /// <summary>
        /// day-curve-ext 35일 앵커(처서) — 35~39일 낮 지표 더위 1단계 완화.
        /// </summary>
        public static int ResolveDayHeatStageReduction(GameDataCatalog catalog, int day)
        {
            if (catalog == null || day < 35 || day >= 40) return 0;
            return 1;
        }
    }
}
