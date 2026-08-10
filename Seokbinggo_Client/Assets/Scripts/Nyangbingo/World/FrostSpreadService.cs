using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>
    /// v36/v46 서리 확산 + 경계암 개방. 봉헌 격퇴 횟수에 따라 깊이 구간을 pending으로 표시하고,
    /// 공기와 맞닿은 타일을 처음 건드릴 때 광물을 확정한다(월드젠 재실행 없음).
    /// </summary>
    public sealed class FrostSpreadService
    {
        public readonly struct DepthBand
        {
            public DepthBand(int minDepth, int maxDepth)
            {
                MinDepth = minDepth;
                MaxDepth = maxDepth;
            }

            public int MinDepth { get; }
            public int MaxDepth { get; }
        }

        private static readonly DepthBand[] Bands =
        {
            new DepthBand(91, 135),   // 1차 심층
            new DepthBand(46, 135),   // 2차 중층 하단~
            new DepthBand(136, 140)   // 3차 경계암
        };

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
        private int altarClears;
        private bool firstFrostNotified;

        public int AltarClears => altarClears;
        public int PendingCount => pendingCells.Count;

        /// <summary>첫 서리 광물 확정 시 1회. 기믹 무기(first_frost_claw) 지급 훅.</summary>
        public event Action FirstFrostRevealed;

        public void OnAltarClear(int n) => OnAltarClear(n, null);

        public void OnAltarClear(int n, TileService tiles)
        {
            var clears = Mathf.Clamp(n, 1, Bands.Length);
            altarClears = Mathf.Max(altarClears, clears);
            if (tiles == null) return;

            var band = BandForClear(clears);
            for (var x = 0; x < tiles.Width; x++)
            {
                var surfaceY = tiles.FindSurfaceNaturalY(x);
                if (surfaceY < 0) continue;
                for (var y = 0; y < tiles.Height; y++)
                {
                    var depth = surfaceY - y + 1;
                    if (depth < band.MinDepth || depth > band.MaxDepth) continue;
                    var cell = new Vector3Int(x, y, 0);
                    var tile = tiles.GetTile(cell);
                    if (tile.IsAir) continue;
                    if (string.Equals(tile.elementType, WorldTileTypes.Bedrock, StringComparison.Ordinal) &&
                        y < 1)
                        continue;
                    if (!tile.isNaturalTerrain || !NaturalFillTypes.Contains(tile.elementType))
                        continue;
                    MarkPending(new Vector2Int(x, y));
                }
            }

            if (altarClears >= 3)
                UnsealBedrockLayer(tiles);
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

        /// <summary>
        /// 공기 인접 pending 셀이면 광물로 확정(타일 제거 없음). 다음 타격에서 채굴한다.
        /// </summary>
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

        public static DepthBand BandForClear(int n)
        {
            var index = Mathf.Clamp(n, 1, Bands.Length) - 1;
            return Bands[index];
        }

        /// <summary>
        /// 경계암 두께 5(y 0..4) 중 최하단(y=0)만 파괴 불가 유지.
        /// 상단 4행(y 1..4)을 stone_deep(hardness 3)으로 바꾼다.
        /// </summary>
        public static void UnsealBedrockLayer(TileService tiles, int bedrockDepth = 140)
        {
            if (tiles == null) return;
            _ = bedrockDepth;
            const int bedrockThickness = 5;
            for (var x = 0; x < tiles.Width; x++)
            for (var y = 1; y < bedrockThickness && y < tiles.Height; y++)
            {
                var cell = new Vector3Int(x, y, 0);
                var tile = tiles.GetTile(cell);
                if (!string.Equals(tile.elementType, WorldTileTypes.Bedrock, StringComparison.Ordinal))
                    continue;
                tiles.TrySetForegroundElement(cell, WorldTileTypes.StoneDeep, 3);
            }
        }

        public void SetAltarClears(int value) =>
            altarClears = Mathf.Clamp(value, 0, Bands.Length);

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
                if (parts.Length != 2) continue;
                if (!int.TryParse(parts[0], out var x) || !int.TryParse(parts[1], out var y))
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
    }
}
