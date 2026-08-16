using System;
using System.Collections.Generic;
using Nyangbingo.Data;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>v72 A-7~A-10에서 여러 런타임이 공유하는 순수 산식.</summary>
    public static class WorldV72Rules
    {
        public const int DefaultZoneCount = 10;

        public static float BandRoundtripSeconds(
            float mapWidth, int zoneCount, float playerSpeed, int zoneOrder = 1)
        {
            if (!IsFinitePositive(mapWidth) || zoneCount <= 0 ||
                !IsFinitePositive(playerSpeed) || zoneOrder <= 0)
                return 0f;
            return 2f * (mapWidth * .5f / zoneCount) / playerSpeed * zoneOrder;
        }

        public static int BodyTilesForHitPoints(int hitPoints) =>
            hitPoints < 500 ? 1 : hitPoints < 2000 ? 2 : 3;

        public static bool ShouldTargetCore(Vector2 spawn, Vector2 core, float radius)
        {
            if (!IsFinite(spawn) || !IsFinite(core) || !IsFinitePositive(radius)) return false;
            return (spawn - core).sqrMagnitude <= radius * radius;
        }

        private static bool IsFinitePositive(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsFinite(Vector2 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y);
    }

    /// <summary>
    /// day-curve는 해금/수량을, terrain-spawn.csv는 위치를 소유한다. 이미 선택된 종을
    /// 후보 셀의 지형 가중치로 배치하므로 두 표가 서로 덮어쓰지 않는다.
    /// </summary>
    public static class TerrainSpawnRules
    {
        private const int RuinSearchRadius = 3;

        public static bool TryChooseCell(
            YokaiDefinition yokai,
            IReadOnlyList<Vector3Int> candidates,
            GameDataCatalog catalog,
            TileService tiles,
            WorldGenerationResult world,
            int deterministicSequence,
            out Vector3Int selected)
        {
            selected = default;
            if (yokai == null || candidates == null || candidates.Count == 0 ||
                catalog == null || tiles == null || world.surfaceHeights == null ||
                world.surfaceHeights.Length != tiles.Width)
                return false;

            var weighted = new List<(Vector3Int cell, int weight)>(candidates.Count);
            long total = 0;
            for (var index = 0; index < candidates.Count; index++)
            {
                var cell = candidates[index];
                var terrainId = ResolveTerrainId(cell, tiles, world.surfaceHeights,
                    ReadDepth(catalog, GlobalKeys.LayerT2Depth, 90),
                    ReadDepth(catalog, GlobalKeys.LayerT3Depth, 135));
                var rule = catalog.FindTerrainSpawn($"{terrainId}:{yokai.Id}");
                if (rule == null || !rule.Implemented || rule.Weight <= 0) continue;
                weighted.Add((cell, rule.Weight));
                total += rule.Weight;
            }

            if (weighted.Count == 0 || total <= 0) return false;
            var roll = (long)(uint)deterministicSequence % total;
            for (var index = 0; index < weighted.Count; index++)
            {
                if (roll < weighted[index].weight)
                {
                    selected = weighted[index].cell;
                    return true;
                }
                roll -= weighted[index].weight;
            }
            selected = weighted[weighted.Count - 1].cell;
            return true;
        }

        public static string ResolveTerrainId(
            Vector3Int cell, TileService tiles, int[] surfaceHeights,
            int middleDepthEnd = 90, int deepDepthEnd = 135)
        {
            if (tiles == null || surfaceHeights == null || surfaceHeights.Length == 0)
                return string.Empty;
            if (IsNearRuin(cell, tiles)) return "ruins";
            var x = Mathf.Clamp(cell.x, 0, surfaceHeights.Length - 1);
            var depth = surfaceHeights[x] - cell.y + 1;
            if (depth <= 0) return "surface_line";
            var upperDepthEnd = Mathf.Max(1, middleDepthEnd / 2);
            if (depth <= upperDepthEnd) return "layer_upper";
            if (depth <= middleDepthEnd) return "layer_mid";
            if (depth <= deepDepthEnd) return "layer_deep";
            return "layer_deep";
        }

        private static bool IsNearRuin(Vector3Int cell, TileService tiles)
        {
            for (var y = cell.y - RuinSearchRadius; y <= cell.y + RuinSearchRadius; y++)
            for (var x = cell.x - RuinSearchRadius; x <= cell.x + RuinSearchRadius; x++)
            {
                var candidate = new Vector3Int(x, y, 0);
                if (tiles.InBounds(candidate) &&
                    string.Equals(tiles.GetTile(candidate).elementType,
                        WorldTileTypes.RuinWall, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static int ReadDepth(GameDataCatalog catalog, string key, int fallback)
        {
            var definition = catalog.FindGlobal(key);
            return definition != null && definition.TryGetInt(out var value) && value > 0
                ? value
                : fallback;
        }
    }
}
