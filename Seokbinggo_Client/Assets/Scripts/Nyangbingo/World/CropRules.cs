using System;
using Nyangbingo.Data;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>
    /// crops.csv 밴드별 캣닢 밀도·회복·심기. dist_norm 정본(|x-center|/halfWidth).
    /// </summary>
    public static class CropRules
    {
        public const string CatnipCropId = "catnip";
        public const string PlantableGlobalKey = "crop_plantable";

        public static string CropDefinitionId(string zoneId) =>
            string.IsNullOrWhiteSpace(zoneId) ? string.Empty : zoneId.Trim() + ":" + CatnipCropId;

        public static bool IsPlantableEnabled(GameDataCatalog catalog)
        {
            var definition = catalog?.FindGlobal(PlantableGlobalKey);
            return definition != null && definition.TryGetBool(out var value) && value;
        }

        public static bool TryResolveZone(GameDataCatalog catalog, float worldX, float mapCenterX,
            float halfMapWidth, out ZoneDefinition zone)
        {
            zone = null;
            if (catalog == null || halfMapWidth <= 0f ||
                float.IsNaN(worldX) || float.IsInfinity(worldX) ||
                float.IsNaN(mapCenterX) || float.IsInfinity(mapCenterX))
                return false;

            var distNorm = Mathf.Abs(worldX - mapCenterX) / halfMapWidth;
            var zones = catalog.Zones;
            for (var index = 0; index < zones.Count; index++)
            {
                var candidate = zones[index];
                if (candidate == null) continue;
                var from = candidate.DistanceNormalizedFrom;
                var to = candidate.DistanceNormalizedTo;
                var includeUpper = to >= .999f;
                if (includeUpper
                        ? distNorm >= from && distNorm <= to + .0001f
                        : distNorm >= from && distNorm < to)
                {
                    zone = candidate;
                    return true;
                }
            }

            return false;
        }

        public static CropDefinition FindCropForWorldX(GameDataCatalog catalog, float worldX,
            float mapCenterX, float halfMapWidth)
        {
            if (!TryResolveZone(catalog, worldX, mapCenterX, halfMapWidth, out var zone))
                return null;
            return catalog.FindCrop(CropDefinitionId(zone.Id));
        }

        public static bool IsWorldXInZoneBand(int worldX, ZoneDefinition zone, float mapCenterX,
            float halfMapWidth)
        {
            if (zone == null || halfMapWidth <= 0f) return false;
            var distNorm = Mathf.Abs(worldX - mapCenterX) / halfMapWidth;
            var from = zone.DistanceNormalizedFrom;
            var to = zone.DistanceNormalizedTo;
            return to >= .999f
                ? distNorm >= from && distNorm <= to + .0001f
                : distNorm >= from && distNorm < to;
        }

        public static int ResolveBandTargetCount(ZoneDefinition zone, CropDefinition crop,
            float halfMapWidth)
        {
            if (zone == null || crop == null || halfMapWidth <= 0f || crop.SpawnPerHundredTiles <= 0)
                return 0;
            var bandWidthTiles = Mathf.Max(0f,
                (zone.DistanceNormalizedTo - zone.DistanceNormalizedFrom) * halfMapWidth * 2f);
            return Mathf.Max(0, Mathf.RoundToInt(bandWidthTiles * crop.SpawnPerHundredTiles / 100f));
        }
    }
}
