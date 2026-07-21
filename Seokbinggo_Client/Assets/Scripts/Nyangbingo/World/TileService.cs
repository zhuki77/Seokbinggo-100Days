using System;
using System.Collections.Generic;
using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using Nyangbingo.Save;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Nyangbingo.World
{
    /// <summary>
    /// 실제 채굴/건설 시스템. MapGenerator가 만든 TileData[,]를 "살아있는" 월드 상태로 소유하고,
    /// TilemapRenderer가 들고 있는 전경/배경 Tilemap을 셀 단위로 갱신한다.
    ///
    /// A-16 규칙:
    ///  - 채굴은 전경만 제거하고 기존 배경을 유지한다.
    ///  - 벽지는 빈 배경 칸에만 설치하며 충돌·밀폐에 영향을 주지 않는다.
    ///  - 전경 변경 이력(tileChanges)과 배경 변경 이력(backgroundChanges)을 분리한다.
    /// </summary>
    public sealed class TileService : ITileDiffSource, IWorldSafeSpawnResolver, IBackgroundPlacementService
    {
        private readonly TileData[,] tiles;
        private TilemapRenderer renderer;
        private readonly GameDataCatalog catalog;
        private readonly int seed;

        private readonly List<TileChangeRecord> changeLog = new List<TileChangeRecord>();
        private readonly Dictionary<Vector3Int, int> changeIndexByCell = new Dictionary<Vector3Int, int>();

        private readonly List<TileChangeRecord> backgroundChangeLog = new List<TileChangeRecord>();
        private readonly Dictionary<Vector3Int, int> backgroundChangeIndexByCell = new Dictionary<Vector3Int, int>();

        /// <summary>스폰 시 회피할 수직 공기 run 상한(타일). cave_max_height와 맞춤.</summary>
        private const int MaxSafeSpawnAirRunBelow = 12;

        private static readonly HashSet<string> IndestructibleElementTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            WorldTileTypes.Bedrock,
            WorldTileTypes.IceAltar
        };

        private static readonly Dictionary<string, string> DropItemOverrides = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { WorldTileTypes.StoneMid, WorldTileTypes.Stone },
            { WorldTileTypes.StoneDeep, WorldTileTypes.Stone },
            { WorldTileTypes.RuinWall, WorldTileTypes.Stone },
            { WorldTileTypes.IceLake, WorldTileTypes.IceShard }
        };

        private static readonly Dictionary<string, int> PlacementHardness = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { WorldTileTypes.Dirt, 1 }, { WorldTileTypes.Clay, 1 }, { WorldTileTypes.Coal, 1 },
            // The inventory "stone" item is the upper-layer T1 block. Player-placed stone must
            // remain removable with the default claw just like the natural block it came from.
            { WorldTileTypes.Stone, 1 }, { WorldTileTypes.StoneMid, 2 }, { WorldTileTypes.IronOre, 2 },
            { WorldTileTypes.CopperOre, 2 }, { WorldTileTypes.IceShard, 2 }, { WorldTileTypes.RuinWall, 2 },
            { WorldTileTypes.StoneDeep, 3 }, { WorldTileTypes.IceSteelOre, 3 }, { WorldTileTypes.FrostEssence, 3 }
        };

        public int Width { get; }
        public int Height { get; }

        public TileService(TileData[,] tiles, TilemapRenderer renderer, GameDataCatalog catalog, int seed)
        {
            this.tiles = tiles ?? throw new ArgumentNullException(nameof(tiles));
            this.renderer = renderer;
            this.catalog = catalog;
            this.seed = seed;
            Width = tiles.GetLength(0);
            Height = tiles.GetLength(1);
        }

        public void BindRenderer(TilemapRenderer newRenderer) => renderer = newRenderer;

        public bool InBounds(Vector3Int cell) => cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Height;

        public TileData GetTile(Vector3Int cell) => InBounds(cell) ? tiles[cell.x, cell.y] : default;

        /// <summary>
        /// 전경 타일 파괴(채굴). 성공 시 전경만 비우고 기존 배경·자연 배경 기준은 유지한다(A-16).
        /// </summary>
        public bool TryBreakForeground(Vector3Int cell, int toolTier, out string droppedItemId, out int droppedAmount)
        {
            droppedItemId = null;
            droppedAmount = 0;

            if (!InBounds(cell)) return false;

            var current = tiles[cell.x, cell.y];
            if (current.IsAir) return false;
            if (IndestructibleElementTypes.Contains(current.elementType)) return false;
            if (toolTier < current.hardness) return false;

            var minedElementType = current.elementType;
            GameEvents.RaiseMiningImpact(minedElementType == WorldTileTypes.Dirt ||
                                          minedElementType == WorldTileTypes.Clay
                ? MiningImpactSurface.Dirt
                : MiningImpactSurface.Mineral);

            var cleared = current.WithoutForeground();
            tiles[cell.x, cell.y] = cleared;
            ApplyForegroundVisual(cell, null);
            ApplyBackgroundVisual(cell, cleared.HasBackground ? cleared.backgroundElementType : null);
            RefreshEdgeOverlayAround(cell);
            renderer?.NotifyForegroundCollisionDirty();

            if (TryResolveDrop(minedElementType, out var item, out var amount))
            {
                ItemAcquisition.Request(item, amount);
                droppedItemId = item.Id;
                droppedAmount = amount;
            }

            RecordChange(cell, minedElementType, placed: false);
            GameEvents.RaiseTileBroken(cell);
            return true;
        }

        /// <summary>
        /// 전경 타일 설치. 배경 필드는 그대로 두고 전경만 덮는다.
        /// A-25: PlacementHardness에 등록된 재설치 가능 ID만 허용(기반암·제단·배경 ID 제외).
        /// </summary>
        public bool TryPlaceForeground(Vector3Int cell, string elementType, Nyangbingo.Inventory.Inventory consumeFrom = null, int hardnessOverride = -1)
        {
            if (string.IsNullOrEmpty(elementType) || !InBounds(cell)) return false;
            elementType = TileIdAlias.ToCanonical(elementType);
            if (!SupportsForegroundPlacement(elementType)) return false;

            var current = tiles[cell.x, cell.y];
            if (!current.IsAir) return false;

            if (consumeFrom != null && !consumeFrom.TryRemove(elementType, 1)) return false;

            var hardness = hardnessOverride > 0 ? hardnessOverride : ResolvePlacementHardness(elementType);
            tiles[cell.x, cell.y] = new TileData
            {
                hardness = hardness,
                isNaturalTerrain = false,
                elementType = elementType,
                backgroundElementType = string.IsNullOrEmpty(current.backgroundElementType) ? WorldTileTypes.Air : current.backgroundElementType,
                naturalBackgroundElementType = string.IsNullOrEmpty(current.naturalBackgroundElementType) ? WorldTileTypes.Air : current.naturalBackgroundElementType
            };

            ApplyForegroundVisual(cell, elementType);
            RefreshEdgeOverlayAround(cell);
            renderer?.NotifyForegroundCollisionDirty();

            RecordChange(cell, elementType, placed: true);
            GameEvents.RaiseTilePlaced(cell);
            return true;
        }

        /// <summary>A-25: 아이템 ID가 플레이어 재설치 가능 전경 타일인지(월드 정본).</summary>
        public static bool SupportsForegroundPlacement(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            var id = TileIdAlias.ToCanonical(itemId);
            if (IndestructibleElementTypes.Contains(id)) return false;
            if (WorldTileTypes.IsBackgroundId(id)) return false;
            return PlacementHardness.ContainsKey(id);
        }

        /// <summary>A-25: 대상 셀에 전경 설치 가능 여부(사전 판정, 인벤 미소비).</summary>
        public bool CanPlaceForeground(Vector3Int cell, string itemId)
        {
            if (!SupportsForegroundPlacement(itemId) || !InBounds(cell)) return false;
            return GetTile(cell).IsAir;
        }

        /// <summary>
        /// A-16: 빈 배경 칸에 벽지(또는 허용된 배경 ID)를 설치한다. 충돌 없음, 밀폐 경계 아님.
        /// </summary>
        public bool TryPlaceBackground(Vector3Int cell, string backgroundElementType, Nyangbingo.Inventory.Inventory consumeFrom = null)
        {
            if (string.IsNullOrEmpty(backgroundElementType) || !InBounds(cell)) return false;
            backgroundElementType = TileIdAlias.ToCanonical(backgroundElementType);
            if (!WorldTileTypes.IsBackgroundId(backgroundElementType)) return false;

            var current = tiles[cell.x, cell.y];
            if (current.HasBackground) return false; // 빈 배경 칸에만 설치.

            if (consumeFrom != null && !consumeFrom.TryRemove(backgroundElementType, 1)) return false;

            current.backgroundElementType = backgroundElementType;
            tiles[cell.x, cell.y] = current;
            ApplyBackgroundVisual(cell, backgroundElementType);
            RecordBackgroundChange(cell, backgroundElementType, placed: true);
            GameEvents.RaiseTilePlaced(cell);
            return true;
        }

        /// <summary>
        /// A-16: 벽지 제거 시 naturalBackground로 복원(지하 자연 배경 / 하늘·동굴 빈 배경)하고
        /// 제거한 벽지 1장을 해당 셀에 월드 드롭으로 반환한다.
        /// </summary>
        public bool TryRemoveBackground(Vector3Int cell)
        {
            if (!InBounds(cell)) return false;

            var current = tiles[cell.x, cell.y];
            if (!current.HasBackground) return false;
            // 자연 배경만 있고 벽지가 아닌 칸은 "제거" 대상이 아니다(플레이어 벽지만 제거).
            if (!current.IsWallpaperBackground) return false;

            var removedId = current.backgroundElementType;
            var restored = current.WithBackgroundRestoredToNatural();
            tiles[cell.x, cell.y] = restored;
            ApplyBackgroundVisual(cell, restored.HasBackground ? restored.backgroundElementType : null);
            RecordBackgroundChange(cell, removedId, placed: false);
            GameEvents.RaiseTileBroken(cell);
            if (TryResolveDrop(removedId, out var item, out var amount))
                WorldItemDropRequest.Request(item, amount, new Vector2(cell.x + .5f, cell.y + .5f));
            return true;
        }

        // --- A-25 IBackgroundPlacementService ---

        public bool CanPlaceWallpaper(Vector3Int cell)
        {
            if (!InBounds(cell)) return false;
            var current = tiles[cell.x, cell.y];
            return !current.HasBackground;
        }

        public bool TryPlaceWallpaper(Vector3Int cell) =>
            TryPlaceBackground(cell, WorldTileTypes.Wallpaper, consumeFrom: null);

        /// <summary>벽지 설치 + 인벤토리 원자 소비(성공 시 1개).</summary>
        public bool TryPlaceWallpaper(Vector3Int cell, Nyangbingo.Inventory.Inventory consumeFrom) =>
            TryPlaceBackground(cell, WorldTileTypes.Wallpaper, consumeFrom);

        public bool TryRemoveWallpaper(Vector3Int cell) => TryRemoveBackground(cell);

        public BackgroundCellState GetBackgroundState(Vector3Int cell)
        {
            if (!InBounds(cell)) return default;
            var t = tiles[cell.x, cell.y];
            return new BackgroundCellState(
                t.backgroundElementType ?? string.Empty,
                t.naturalBackgroundElementType ?? string.Empty,
                t.IsWallpaperBackground,
                t.HasNaturalBackground);
        }

        public IReadOnlyList<TileChangeRecord> GetTileChangeRecords() => changeLog;
        public IReadOnlyList<TileChangeRecord> GetBackgroundChangeRecords() => backgroundChangeLog;

        public bool RestoreTileChanges(IEnumerable<TileChangeRecord> records)
        {
            if (records == null) return false;

            changeLog.Clear();
            changeIndexByCell.Clear();

            foreach (var record in records)
            {
                if (string.IsNullOrWhiteSpace(record.tileId)) return false;
                var tileId = TileIdAlias.ToCanonical(record.tileId);
                if (!WorldTileTypes.AllElementTypes.Contains(record.tileId) &&
                    !WorldTileTypes.AllElementTypes.Contains(tileId)) return false;

                var cell = new Vector3Int(record.x, record.y, record.z);
                if (!InBounds(cell)) return false;

                var original = tiles[cell.x, cell.y];

                if (record.placed)
                {
                    if (!original.IsAir) return false;
                    if (!PlacementHardness.ContainsKey(tileId)) return false;

                    tiles[cell.x, cell.y] = new TileData
                    {
                        hardness = ResolvePlacementHardness(tileId),
                        isNaturalTerrain = false,
                        elementType = tileId,
                        backgroundElementType = string.IsNullOrEmpty(original.backgroundElementType) ? WorldTileTypes.Air : original.backgroundElementType,
                        naturalBackgroundElementType = string.IsNullOrEmpty(original.naturalBackgroundElementType) ? WorldTileTypes.Air : original.naturalBackgroundElementType
                    };
                    ApplyForegroundVisual(cell, tileId);
                    RefreshEdgeOverlayAround(cell);
                }
                else
                {
                    if (original.IsAir) return false;
                    if (IndestructibleElementTypes.Contains(original.elementType)) return false;
                    if (!string.Equals(original.elementType, tileId, StringComparison.Ordinal)) return false;

                    // A-16: 채굴 재생도 기존 배경을 유지한다(ResolveBackgroundFor로 새로 만들지 않음).
                    var cleared = original.WithoutForeground();
                    tiles[cell.x, cell.y] = cleared;
                    ApplyForegroundVisual(cell, null);
                    ApplyBackgroundVisual(cell, cleared.HasBackground ? cleared.backgroundElementType : null);
                    RefreshEdgeOverlayAround(cell);
                }

                RecordChange(cell, tileId, record.placed);
            }

            return true;
        }

        /// <summary>
        /// A-16: 배경 변경 이력 재생. 실패 시 false — 호출자는 라이브 부분 적용 금지.
        /// null/빈 목록은 구버전 세이브 호환으로 성공 처리한다.
        /// </summary>
        public bool RestoreBackgroundChanges(IEnumerable<TileChangeRecord> records)
        {
            if (records == null) return true; // 구버전 세이브: 배경 이력 없음 = 성공.

            backgroundChangeLog.Clear();
            backgroundChangeIndexByCell.Clear();

            foreach (var record in records)
            {
                if (string.IsNullOrWhiteSpace(record.tileId)) return false;
                var tileId = TileIdAlias.ToCanonical(record.tileId);
                if (!WorldTileTypes.IsBackgroundId(tileId)) return false;

                var cell = new Vector3Int(record.x, record.y, record.z);
                if (!InBounds(cell)) return false;

                var current = tiles[cell.x, cell.y];

                if (record.placed)
                {
                    if (current.HasBackground) return false;
                    current.backgroundElementType = tileId;
                    tiles[cell.x, cell.y] = current;
                    ApplyBackgroundVisual(cell, tileId);
                }
                else
                {
                    if (!current.IsWallpaperBackground) return false;
                    if (!TileIdAlias.EqualsCanonical(current.backgroundElementType, tileId)) return false;
                    var restored = current.WithBackgroundRestoredToNatural();
                    tiles[cell.x, cell.y] = restored;
                    ApplyBackgroundVisual(cell, restored.HasBackground ? restored.backgroundElementType : null);
                }

                RecordBackgroundChange(cell, tileId, record.placed);
            }

            return true;
        }

        public List<Vector3Int> GetValidSpawnPositions(Vector3Int center, int minRange, int maxRange)
        {
            var results = new List<Vector3Int>();
            if (minRange < 0 || maxRange < minRange) return results;

            var minX = Mathf.Max(0, center.x - maxRange);
            var maxX = Mathf.Min(Width - 1, center.x + maxRange);
            var minY = Mathf.Max(0, center.y - maxRange);
            var maxY = Mathf.Min(Height - 1, center.y + maxRange);
            var minRangeSquared = minRange * minRange;
            var maxRangeSquared = maxRange * maxRange;

            for (var x = minX; x <= maxX; x++)
            {
                var dx = x - center.x;
                for (var y = minY; y <= maxY; y++)
                {
                    var dy = y - center.y;
                    var distanceSquared = dx * dx + dy * dy;
                    if (distanceSquared < minRangeSquared || distanceSquared > maxRangeSquared) continue;

                    var candidate = new Vector3Int(x, y, center.z);
                    if (IsSafeGroundSpawn(candidate)) results.Add(candidate);
                }
            }

            return results;
        }

        private bool IsSafeGroundSpawn(Vector3Int cell)
        {
            var ground = new Vector3Int(cell.x, cell.y - 1, cell.z);
            var head = new Vector3Int(cell.x, cell.y + 1, cell.z);
            if (!InBounds(ground) || !InBounds(cell) || !InBounds(head)) return false;

            return GetTile(cell).IsAir && GetTile(head).IsAir && !GetTile(ground).IsAir;
        }

        // --- A-22 IWorldSafeSpawnResolver ---

        /// <summary>
        /// 논리 셀 계약: 셀 (x,y)의 월드 AABB는 [x,x+1]×[y,y+1], 중심 (x+0.5, y+0.5).
        /// 스폰 발은 발밑 고체 윗면(groundY+1)에 두고, 액터 중심은 그 위 actorHalfExtent.
        /// </summary>
        public bool IsSafeStandingPosition(Vector2 worldPosition, float actorHalfExtent)
        {
            var half = Mathf.Max(0.05f, actorHalfExtent);
            var cellX = Mathf.FloorToInt(worldPosition.x);
            var groundTop = worldPosition.y - half;
            var groundY = Mathf.FloorToInt(groundTop) - 1;
            var feetY = groundY + 1;
            if (!IsSafeStandingCells(cellX, groundY, feetY)) return false;
            // 미세한 위치 오차 허용: 발 높이가 고체 윗면 근처인지.
            return Mathf.Abs(groundTop - (groundY + 1f)) <= 0.35f;
        }

        public bool TryResolveSafeSurfaceSpawn(int preferredCellX, float actorHalfExtent, out Vector2 worldPosition)
        {
            worldPosition = default;
            if (Width <= 0 || Height <= 2) return false;
            var half = Mathf.Max(0.05f, actorHalfExtent);
            var centerX = Mathf.Clamp(preferredCellX, 0, Width - 1);

            for (var distance = 0; distance < Width; distance++)
            {
                if (TryResolveColumn(centerX + distance, half, out worldPosition)) return true;
                if (distance > 0 && TryResolveColumn(centerX - distance, half, out worldPosition)) return true;
            }

            return false;
        }

        private bool TryResolveColumn(int x, float actorHalfExtent, out Vector2 worldPosition)
        {
            worldPosition = default;
            if (x < 0 || x >= Width) return false;

            // 열 상단부터 내려가며 최상위 자연 고체(지표면)를 찾는다.
            for (var y = Height - 2; y >= 1; y--)
            {
                var ground = GetTile(new Vector3Int(x, y, 0));
                if (ground.IsAir || !ground.isNaturalTerrain) continue;

                var feetY = y + 1;
                if (!IsSafeStandingCells(x, y, feetY)) continue;
                if (HasHazardousAirShaftBelow(x, y)) continue;

                // 열 상단에서 찾은 첫 자연 고체 = 지표면. 지하 고체로 내려가지 않는다.
                worldPosition = new Vector2(x + 0.5f, y + 1f + actorHalfExtent);
                return true;
            }

            return false;
        }

        private bool IsSafeStandingCells(int cellX, int groundY, int feetY)
        {
            var ground = new Vector3Int(cellX, groundY, 0);
            var feet = new Vector3Int(cellX, feetY, 0);
            var head = new Vector3Int(cellX, feetY + 1, 0);
            if (!InBounds(ground) || !InBounds(feet) || !InBounds(head)) return false;
            if (GetTile(ground).IsAir) return false;
            if (!GetTile(feet).IsAir || !GetTile(head).IsAir) return false;
            return true;
        }

        /// <summary>발밑 고체 아래로 긴 연속 공기가 있으면 낙하 구멍으로 간주.</summary>
        private bool HasHazardousAirShaftBelow(int x, int groundY)
        {
            var airRun = 0;
            for (var y = groundY - 1; y >= 0; y--)
            {
                if (!GetTile(new Vector3Int(x, y, 0)).IsAir) break;
                airRun++;
                if (airRun > MaxSafeSpawnAirRunBelow) return true;
            }
            return false;
        }

        public int Seed => seed;

        public IReadOnlyList<string> ExportTileDiff()
        {
            var lines = new List<string>(changeLog.Count);
            foreach (var record in changeLog)
                lines.Add($"{record.x},{record.y},{record.z},{record.tileId},{(record.placed ? 1 : 0)}");
            return lines;
        }

        private bool TryResolveDrop(string minedElementType, out ItemDefinition item, out int amount)
        {
            item = null;
            amount = 0;
            if (catalog == null) return false;

            var dropId = DropItemOverrides.TryGetValue(minedElementType, out var mapped) ? mapped : minedElementType;
            if (string.IsNullOrEmpty(dropId)) return false;

            item = catalog.FindItem(dropId);
            if (item == null) return false;

            amount = 1;
            return true;
        }

        private static int ResolvePlacementHardness(string elementType) =>
            PlacementHardness.TryGetValue(elementType, out var hardness) ? hardness : 1;

        private void ApplyForegroundVisual(Vector3Int cell, string elementType)
        {
            if (renderer == null || renderer.Foreground == null) return;
            TileBase tileBase = null;
            if (!string.IsNullOrEmpty(elementType)) renderer.TryGetTileBase(elementType, out tileBase);
            renderer.Foreground.SetTile(cell, tileBase);
            renderer.Foreground.RefreshTile(cell);
        }

        private void ApplyBackgroundVisual(Vector3Int cell, string elementType)
        {
            if (renderer == null || renderer.Background == null) return;
            TileBase tileBase = null;
            if (!string.IsNullOrEmpty(elementType) &&
                !string.Equals(elementType, WorldTileTypes.Air, StringComparison.Ordinal))
            {
                if (string.Equals(TileIdAlias.ToCanonical(elementType), WorldTileTypes.Wallpaper,
                        StringComparison.Ordinal))
                    renderer.TryGetWallpaperTileBase(cell.y, out tileBase);
                else
                    renderer.TryGetTileBase(elementType, out tileBase);
            }
            renderer.Background.SetTile(cell, tileBase);
        }

        private void RefreshEdgeOverlayAround(Vector3Int cell)
        {
            if (renderer == null) return;

            RefreshEdgeOverlayAt(cell);
            RefreshEdgeOverlayAt(new Vector3Int(cell.x, cell.y + 1, cell.z));
            RefreshEdgeOverlayAt(new Vector3Int(cell.x, cell.y - 1, cell.z));
            RefreshEdgeOverlayAt(new Vector3Int(cell.x - 1, cell.y, cell.z));
            RefreshEdgeOverlayAt(new Vector3Int(cell.x + 1, cell.y, cell.z));
        }

        private void RefreshEdgeOverlayAt(Vector3Int cell)
        {
            if (!InBounds(cell)) return;
            var mask = TileEdgeOverlayResolver.ComputeExposureMask(tiles, cell.x, cell.y, Width, Height);
            renderer.RefreshEdgeOverlay(cell, mask);
        }

        private void RecordChange(Vector3Int cell, string tileId, bool placed)
        {
            var record = new TileChangeRecord { x = cell.x, y = cell.y, z = cell.z, tileId = tileId, placed = placed };
            if (changeIndexByCell.TryGetValue(cell, out var index))
            {
                changeLog[index] = record;
            }
            else
            {
                changeIndexByCell[cell] = changeLog.Count;
                changeLog.Add(record);
            }
        }

        private void RecordBackgroundChange(Vector3Int cell, string tileId, bool placed)
        {
            var record = new TileChangeRecord { x = cell.x, y = cell.y, z = cell.z, tileId = tileId, placed = placed };
            if (backgroundChangeIndexByCell.TryGetValue(cell, out var index))
            {
                backgroundChangeLog[index] = record;
            }
            else
            {
                backgroundChangeIndexByCell[cell] = backgroundChangeLog.Count;
                backgroundChangeLog.Add(record);
            }
        }
    }
}
