using System;
using System.Collections.Generic;
using Nyangbingo.Data;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>
    /// v72 A-6: 제단 보스 격파 횟수에 따라 지하를 아래에서 위로 pending 표시하고,
    /// 공기와 맞닿은 자연 타일을 처음 건드릴 때만 서리 광물로 확정한다.
    /// 경계암은 어떤 단계에서도 변경하지 않는다.
    /// </summary>
    public sealed class FrostSpreadService
    {
        private static readonly HashSet<string> NaturalFillTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            WorldTileTypes.Dirt,
            WorldTileTypes.Stone,
            WorldTileTypes.StoneMid,
            WorldTileTypes.StoneDeep,
            WorldTileTypes.Clay,
            WorldTileTypes.Coal
        };

        private readonly HashSet<Vector2Int> pendingCells = new HashSet<Vector2Int>();
        private readonly int stepCount;
        private readonly int undergroundDepthTiles;
        private int altarClears;
        private bool firstFrostNotified;

        public FrostSpreadService(GameDataCatalog catalog = null)
        {
            stepCount = Mathf.Max(1, ReadInt(catalog, GlobalKeys.FrostStepCount, 10));
            undergroundDepthTiles = Mathf.Max(1, ReadInt(
                catalog, GlobalKeys.LayerT3Depth, WorldGenerationConfig.UndergroundDepthMinTiles));
        }

        public int AltarClears => altarClears;
        public int StepCount => stepCount;
        public int PendingCount => pendingCells.Count;
        public float BandFromNorm => CalculateBandFromNorm(altarClears, stepCount);

        public event Action FirstFrostRevealed;

        public bool OnAltarBossClear(TileService tiles = null)
        {
            if (altarClears >= stepCount) return false;
            altarClears++;
            if (tiles != null) MarkPendingBand(tiles);
            return true;
        }

        private void MarkPendingBand(TileService tiles)
        {
            var from = BandFromNorm;
            for (var x = 0; x < tiles.Width; x++)
            {
                var surfaceY = tiles.FindSurfaceNaturalY(x);
                if (surfaceY < 0) continue;
                for (var y = 0; y < tiles.Height; y++)
                {
                    var depthNorm = Mathf.Clamp01((surfaceY - y) / (float)undergroundDepthTiles);
                    if (depthNorm < from) continue;
                    var cell = new Vector3Int(x, y, 0);
                    var tile = tiles.GetTile(cell);
                    if (tile.IsAir || !tile.isNaturalTerrain ||
                        string.Equals(tile.elementType, WorldTileTypes.Bedrock, StringComparison.Ordinal) ||
                        !NaturalFillTypes.Contains(tile.elementType))
                        continue;
                    pendingCells.Add(new Vector2Int(x, y));
                }
            }
        }

        public void MarkPending(Vector2Int cell) => pendingCells.Add(cell);
        public bool IsPending(Vector2Int cell) => pendingCells.Contains(cell);

        public bool TryLazyReveal(Vector2Int cell, bool isAirAdjacent, out string oreType)
        {
            oreType = null;
            if (!pendingCells.Contains(cell) || !isAirAdjacent) return false;
            pendingCells.Remove(cell);
            oreType = OreOf(altarClears);
            NotifyFirstFrostIfNeeded();
            return true;
        }

        /// <summary>공기 인접 pending 셀이면 광물로 확정한다. 실제 채굴은 다음 타격에서 수행한다.</summary>
        public bool TryRevealOnInteract(TileService tiles, Vector3Int cell)
        {
            if (tiles == null || !tiles.InBounds(cell)) return false;
            var key = new Vector2Int(cell.x, cell.y);
            if (!IsPending(key) || !tiles.IsAirAdjacent(cell)) return false;
            var oreType = OreOf(altarClears);
            var hardness = ResolveOreHardness(oreType);
            if (!tiles.TrySetForegroundElement(cell, oreType, hardness)) return false;
            pendingCells.Remove(key);
            NotifyFirstFrostIfNeeded();
            return true;
        }

        public static float CalculateBandFromNorm(int clears, int steps = 10) =>
            1f - Mathf.Clamp(clears, 0, Mathf.Max(1, steps)) / (float)Mathf.Max(1, steps);

        public static bool IsInFrostBand(float depthNorm, int clears, int steps = 10) =>
            clears > 0 && Mathf.Clamp01(depthNorm) >= CalculateBandFromNorm(clears, steps);

        public bool RestoreAltarClears(int value)
        {
            if (value < 0 || value > stepCount) return false;
            altarClears = value;
            return true;
        }

        public List<string> ExportPendingCells()
        {
            var list = new List<string>(pendingCells.Count);
            foreach (var cell in pendingCells)
                list.Add($"{cell.x},{cell.y}");
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        public void RestorePendingCells(IEnumerable<string> encoded)
        {
            pendingCells.Clear();
            if (encoded == null) return;
            foreach (var token in encoded)
            {
                if (string.IsNullOrWhiteSpace(token)) continue;
                var parts = token.Split(',');
                if (parts.Length != 2 || !int.TryParse(parts[0], out var x) ||
                    !int.TryParse(parts[1], out var y))
                    continue;
                pendingCells.Add(new Vector2Int(x, y));
            }
        }

        private void NotifyFirstFrostIfNeeded()
        {
            if (firstFrostNotified) return;
            firstFrostNotified = true;
            FirstFrostRevealed?.Invoke();
        }

        private static int ResolveOreHardness(string oreType) =>
            string.Equals(oreType, WorldTileTypes.IceSteelOre, StringComparison.Ordinal) ? 3 : 2;

        private static string OreOf(int stage) =>
            stage >= 3 ? WorldTileTypes.IceSteelOre :
            stage >= 2 ? WorldTileTypes.IronOre :
            WorldTileTypes.CopperOre;

        private static int ReadInt(GameDataCatalog catalog, string key, int fallback)
        {
            var definition = catalog?.FindGlobal(key);
            return definition != null && definition.TryGetInt(out var value) ? value : fallback;
        }
    }
}
