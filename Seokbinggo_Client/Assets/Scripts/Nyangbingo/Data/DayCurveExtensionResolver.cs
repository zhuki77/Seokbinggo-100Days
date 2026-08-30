using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nyangbingo.Data
{
    /// <summary>
    /// day-curve-ext.csv 앵커(31~100)를 MVP day 30 composition 위에 해석한다.
    /// 31일 이후 일반 야간 스폰이 끊기지 않도록 FindDayCurve에 연결된다.
    /// </summary>
    public static class DayCurveExtensionResolver
    {
        private static readonly Dictionary<int, DayCurveDefinition> ResolvedByDay = new();

        public static void ClearCache() => ResolvedByDay.Clear();

        public static DayCurveDefinition Resolve(GameDataCatalog catalog, int day)
        {
            if (catalog == null || day <= 30) return null;
            if (ResolvedByDay.TryGetValue(day, out var cached) && cached != null)
                return cached;

            var anchor = catalog.FindDayCurveExtensionAnchor(day);
            if (anchor == null) return null;
            if (anchor.Day == day)
            {
                ResolvedByDay[day] = anchor;
                return anchor;
            }

            var resolved = UnityEngine.Object.Instantiate(anchor);
            resolved.hideFlags = HideFlags.HideAndDontSave;
            resolved.name = $"day_curve_ext_{day}";
            resolved.Configure(
                day,
                anchor.HeatStage,
                anchor.DayFireDamagePerSecond,
                anchor.NightYokaiCount,
                anchor.YokaiWallDamage,
                anchor.PaceSealPercent,
                anchor.PaceMineralTier,
                anchor.MaxActive,
                anchor.SpawnComposition,
                anchor.SpawnMultiplier,
                anchor.DropMultiplier,
                anchor.EventId,
                anchor.VariantMultiplier,
                anchor.HeatSeepPercent,
                anchor.IceMeltDpsPerDay);
            ResolvedByDay[day] = resolved;
            return resolved;
        }
    }
}
