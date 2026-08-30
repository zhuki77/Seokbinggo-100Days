using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly List<Func<Vector3Int, bool>> foregroundPlacementBlockers =
            new List<Func<Vector3Int, bool>>();
        private readonly HashSet<Vector3Int> openDoors = new HashSet<Vector3Int>();
        private readonly Dictionary<Vector3Int, GameObject> openDoorVisuals =
            new Dictionary<Vector3Int, GameObject>();
        private readonly Dictionary<Vector3Int, float> wallDamageTaken =
            new Dictionary<Vector3Int, float>();
        private Func<Vector3Int, bool> clayPlasterResolver;
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
            { WorldTileTypes.IceLake, WorldTileTypes.IceShard },
            { DoorTopElementType, DoorElementType }
        };

        private static readonly Dictionary<string, int> PlacementHardness = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { WorldTileTypes.Dirt, 1 }, { WorldTileTypes.Clay, 1 }, { WorldTileTypes.Coal, 1 },
            // The inventory "stone" item is the upper-layer T1 block. Player-placed stone must
            // remain removable with the default claw just like the natural block it came from.
            { WorldTileTypes.Stone, 1 }, { WorldTileTypes.StoneMid, 2 }, { WorldTileTypes.IronOre, 2 },
            { WorldTileTypes.CopperOre, 2 }, { WorldTileTypes.IceShard, 2 }, { WorldTileTypes.RuinWall, 2 },
            { WorldTileTypes.StoneDeep, 3 }, { WorldTileTypes.IceSteelOre, 3 }, { WorldTileTypes.FrostEssence, 3 },
            // Product insulation boundaries are foreground tiles, not floor-standing objects.
            // door는 비주얼 1x2(아래 칸), door_top은 위 칸 충돌·밀폐 전용(투명).
            { "insul_wall", 1 }, { DoorElementType, 1 }, { DoorTopElementType, 1 }, { "roof", 1 },
            { "iron_insul_wall", 2 }
        };

        public const string DoorElementType = "door";
        public const string DoorTopElementType = "door_top";
        public const int DoorHeightCells = 2;

        public const float DefaultInsulationWallHitPoints = 600f;
        public const float DefaultClayPlasteredWallHitPoints = 750f;
        public const float DefaultIronInsulationWallHitPoints = 900f;

        public int Width { get; }
        public int Height { get; }

        /// <summary>서리 확산 서비스. 월드 로드 후 RuntimeServices가 연결한다.</summary>
        public FrostSpreadService FrostSpread { get; set; }

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

        public void SetClayPlasterResolver(Func<Vector3Int, bool> resolver) =>
            clayPlasterResolver = resolver;

        public void SetForegroundPlacementBlocker(Func<Vector3Int, bool> blocker)
        {
            if (blocker != null && !foregroundPlacementBlockers.Contains(blocker))
                foregroundPlacementBlockers.Add(blocker);
        }

        public void ClearForegroundPlacementBlocker(Func<Vector3Int, bool> blocker)
        {
            if (blocker != null) foregroundPlacementBlockers.Remove(blocker);
        }

        public bool IsForegroundPlacementBlocked(Vector3Int cell)
        {
            for (var index = 0; index < foregroundPlacementBlockers.Count; index++)
                if (foregroundPlacementBlockers[index]?.Invoke(cell) == true)
                    return true;
            return false;
        }

        public Vector3Int WorldToCell(Vector2 worldPosition) => renderer != null
            ? renderer.WorldToCell(worldPosition)
            : new Vector3Int(Mathf.FloorToInt(worldPosition.x), Mathf.FloorToInt(worldPosition.y), 0);

        public Vector3 GetCellCenterWorld(Vector3Int cell) => renderer != null
            ? renderer.GetCellCenterWorld(cell)
            : new Vector3(cell.x + .5f, cell.y + .5f, cell.z);

        public Vector3 GetCellVisualAnchorWorld(Vector3Int cell) => renderer != null
            ? renderer.GetCellVisualAnchorWorld(cell)
            : new Vector3(cell.x + .5f, cell.y, cell.z);

        /// <summary>
        /// 전경 채굴 드롭의 물리 루트 위치. 원형 콜라이더 하단이 셀 바닥에 닿도록 한다.
        /// </summary>
        public Vector2 ResolveForegroundMiningDropWorldPosition(Vector3Int cell)
        {
            var bounds = GetCellWorldBounds(cell);
            return new Vector2(bounds.center.x,
                bounds.min.y + MainGameWorldDropRuntime.DropColliderRadius);
        }

        public void AlignSpriteBoundsToCellBase(SpriteRenderer spriteRenderer, Vector3Int cell)
        {
            if (spriteRenderer == null) return;
            var cellBounds = GetCellWorldBounds(cell);
            var spriteBounds = spriteRenderer.bounds;
            spriteRenderer.transform.position += new Vector3(
                cellBounds.center.x - spriteBounds.center.x,
                cellBounds.min.y - spriteBounds.min.y,
                0f);
        }

        public Bounds GetCellWorldBounds(Vector3Int cell) => renderer != null
            ? renderer.GetCellWorldBounds(cell)
            : new Bounds(
                new Vector3(cell.x + .5f, cell.y + .5f, cell.z),
                new Vector3(1f, 1f, 0f));

        public bool TryFindDamageableWall(Vector2 attackerPosition, Vector2 approachDirection,
            float searchRange, out Vector3Int wallCell, out YokaiWallMaterial material)
        {
            wallCell = default;
            material = YokaiWallMaterial.Default;
            if (!IsFinite(attackerPosition.x) || !IsFinite(attackerPosition.y) ||
                !IsFinite(approachDirection.x) || !IsFinite(approachDirection.y) ||
                !IsFinite(searchRange) || searchRange < 0f)
                return false;

            if (approachDirection.sqrMagnitude <= Mathf.Epsilon) return false;
            approachDirection.Normalize();

            var effectiveRange = searchRange + .75f;
            var extent = Mathf.CeilToInt(effectiveRange);
            var center = new Vector3Int(
                Mathf.FloorToInt(attackerPosition.x),
                Mathf.FloorToInt(attackerPosition.y), 0);
            var bestDistance = float.PositiveInfinity;
            var found = false;
            for (var y = center.y - extent; y <= center.y + extent; y++)
            for (var x = center.x - extent; x <= center.x + extent; x++)
            {
                var candidate = new Vector3Int(x, y, 0);
                if (!TryResolveWallMaterial(candidate, out var candidateMaterial)) continue;
                var offset = (Vector2)GetCellCenterWorld(candidate) - attackerPosition;
                var distance = offset.magnitude;
                if (distance > effectiveRange || distance <= Mathf.Epsilon ||
                    Vector2.Dot(offset / distance, approachDirection) < .25f ||
                    distance >= bestDistance)
                    continue;
                found = true;
                bestDistance = distance;
                wallCell = candidate;
                material = candidateMaterial;
            }
            return found;
        }

        public bool TryDamageWall(Vector3Int cell, float amount,
            out float appliedDamage, out bool destroyed)
        {
            appliedDamage = 0f;
            destroyed = false;
            if (!IsFinite(amount) || amount <= 0f ||
                !TryResolveWallMaterial(cell, out _))
                return false;

            var maximum = ResolveWallHitPoints(cell);
            wallDamageTaken.TryGetValue(cell, out var currentDamage);
            var remaining = Mathf.Max(0f, maximum - currentDamage);
            if (remaining <= Mathf.Epsilon)
            {
                wallDamageTaken.Remove(cell);
                destroyed = DestroyWallWithoutDrop(cell);
                return destroyed;
            }
            appliedDamage = Mathf.Min(amount, remaining);
            currentDamage += appliedDamage;
            if (currentDamage + .0001f < maximum)
            {
                wallDamageTaken[cell] = currentDamage;
                GameEvents.RaiseWallDurabilityChanged(
                    cell, maximum - currentDamage, maximum, false);
                return true;
            }

            wallDamageTaken.Remove(cell);
            destroyed = DestroyWallWithoutDrop(cell);
            if (destroyed)
                GameEvents.RaiseWallDurabilityChanged(cell, 0f, maximum, true);
            return destroyed;
        }

        public float GetWallRemainingHitPoints(Vector3Int cell)
        {
            if (!TryResolveWallMaterial(cell, out _)) return 0f;
            wallDamageTaken.TryGetValue(cell, out var damage);
            return Mathf.Max(0f, ResolveWallHitPoints(cell) - damage);
        }

        public List<WallDamageStateRecord> ExportWallDamage()
        {
            var records = new List<WallDamageStateRecord>(wallDamageTaken.Count);
            foreach (var pair in wallDamageTaken.OrderBy(entry => entry.Key.x)
                         .ThenBy(entry => entry.Key.y))
            {
                if (pair.Value <= 0f || !IsFinite(pair.Value) ||
                    !TryResolveWallMaterial(pair.Key, out _))
                    continue;
                records.Add(new WallDamageStateRecord
                {
                    x = pair.Key.x,
                    y = pair.Key.y,
                    damageTaken = pair.Value
                });
            }
            return records;
        }

        public bool RestoreWallDamage(IEnumerable<WallDamageStateRecord> records)
        {
            if (records == null) return true;
            var restored = new Dictionary<Vector3Int, float>();
            foreach (var record in records)
            {
                var cell = new Vector3Int(record.x, record.y, 0);
                if (!IsFinite(record.damageTaken) || record.damageTaken <= 0f ||
                    !TryResolveWallMaterial(cell, out _) ||
                    record.damageTaken >= ResolveMaximumPossibleWallHitPoints(cell) ||
                    restored.ContainsKey(cell))
                    return false;
                restored.Add(cell, record.damageTaken);
            }
            wallDamageTaken.Clear();
            foreach (var pair in restored) wallDamageTaken.Add(pair.Key, pair.Value);
            return true;
        }

        public bool IsDoorOpen(Vector3Int cell) => openDoors.Contains(ResolveDoorBaseForOpenState(cell));

        public bool TryGetDamageableWallMaterial(
            Vector3Int cell, out YokaiWallMaterial material) =>
            TryResolveWallMaterial(cell, out material);

        public bool TryToggleNearestDoor(Vector2 origin, float radius, out bool isOpen)
        {
            isOpen = false;
            if (!IsFinite(origin.x) || !IsFinite(origin.y) ||
                !IsFinite(radius) || radius < 0f)
                return false;
            var extent = Mathf.CeilToInt(radius);
            var center = WorldToCell(origin);
            var radiusSquared = radius * radius;
            var found = false;
            var nearest = default(Vector3Int);
            var nearestDistance = float.PositiveInfinity;
            for (var y = center.y - extent; y <= center.y + extent; y++)
            for (var x = center.x - extent; x <= center.x + extent; x++)
            {
                var cell = new Vector3Int(x, y, 0);
                if (!InBounds(cell)) continue;
                var element = TileIdAlias.ToCanonical(GetTile(cell).elementType);
                if (!IsDoorFootprintElement(element)) continue;
                var baseCell = ResolveDoorBaseCell(cell, element);
                var delta = (Vector2)GetCellCenterWorld(baseCell) - origin;
                var distance = delta.sqrMagnitude;
                if (distance > radiusSquared || found && distance >= nearestDistance)
                    continue;
                found = true;
                nearest = baseCell;
                nearestDistance = distance;
            }
            if (!found) return false;
            isOpen = !openDoors.Contains(nearest);
            if (isOpen) OpenDoor(nearest);
            else CloseDoor(nearest);
            return true;
        }

        public List<DoorStateRecord> ExportDoorStates() => openDoors
            .OrderBy(cell => cell.x)
            .ThenBy(cell => cell.y)
            .Select(cell => new DoorStateRecord
            {
                x = cell.x,
                y = cell.y,
                isOpen = true
            })
            .ToList();

        public bool RestoreDoorStates(IEnumerable<DoorStateRecord> records)
        {
            if (records == null) return false;
            var validated = new HashSet<Vector3Int>();
            foreach (var record in records)
            {
                if (!record.isOpen) continue;
                var cell = new Vector3Int(record.x, record.y, 0);
                if (!InBounds(cell) ||
                    TileIdAlias.ToCanonical(GetTile(cell).elementType) != DoorElementType ||
                    !validated.Add(cell))
                    return false;
            }
            foreach (var cell in openDoors.ToArray()) CloseDoor(cell);
            foreach (var cell in validated) OpenDoor(cell);
            return true;
        }

        public bool InBounds(Vector3Int cell) => cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Height;

        public TileData GetTile(Vector3Int cell) => InBounds(cell) ? tiles[cell.x, cell.y] : default;

        /// <summary>월드 좌표의 지표 대비 깊이. depth 1 = 해당 열 지표 타일.</summary>
        public bool TryGetSurfaceRelativeDepth(Vector2 worldPosition, out int depth)
        {
            depth = 0;
            if (float.IsNaN(worldPosition.x) || float.IsInfinity(worldPosition.x) ||
                float.IsNaN(worldPosition.y) || float.IsInfinity(worldPosition.y))
                return false;
            var surfaceY = FindSurfaceNaturalY(Mathf.FloorToInt(worldPosition.x));
            if (surfaceY < 0) return false;
            depth = surfaceY - Mathf.FloorToInt(worldPosition.y) + 1;
            return depth > 0;
        }

        /// <summary>열 x의 최상위 자연 고체 Y. 없으면 -1.</summary>
        public int FindSurfaceNaturalY(int x)
        {
            if (x < 0 || x >= Width || Height <= 1) return -1;
            for (var y = Height - 1; y >= 0; y--)
            {
                var tile = tiles[x, y];
                if (!tile.IsAir && tile.isNaturalTerrain) return y;
            }

            return -1;
        }

        public bool IsAirAdjacent(Vector3Int cell)
        {
            if (!InBounds(cell)) return false;
            var neighbors = new[]
            {
                cell + Vector3Int.left,
                cell + Vector3Int.right,
                cell + Vector3Int.up,
                cell + Vector3Int.down
            };
            for (var i = 0; i < neighbors.Length; i++)
            {
                if (!InBounds(neighbors[i])) continue;
                if (GetTile(neighbors[i]).IsAir) return true;
            }

            return false;
        }

        /// <summary>
        /// 전경 element만 교체(채굴/설치 아님). 서리 광물 확정·경계암 개방에 사용.
        /// </summary>
        public bool TrySetForegroundElement(Vector3Int cell, string elementType, int hardness)
        {
            if (!InBounds(cell) || string.IsNullOrEmpty(elementType)) return false;
            elementType = TileIdAlias.ToCanonical(elementType);
            var current = tiles[cell.x, cell.y];
            if (current.IsAir) return false;

            tiles[cell.x, cell.y] = new TileData
            {
                hardness = Mathf.Max(1, hardness),
                isNaturalTerrain = current.isNaturalTerrain,
                elementType = elementType,
                backgroundElementType = string.IsNullOrEmpty(current.backgroundElementType)
                    ? WorldTileTypes.Air
                    : current.backgroundElementType,
                naturalBackgroundElementType = string.IsNullOrEmpty(current.naturalBackgroundElementType)
                    ? WorldTileTypes.Air
                    : current.naturalBackgroundElementType
            };
            ApplyForegroundVisual(cell, elementType);
            RefreshEdgeOverlayAround(cell);
            renderer?.NotifyForegroundCollisionDirty();
            return true;
        }

        /// <summary>
        /// 전경 타일 파괴(채굴). 성공 시 전경만 비우고 기존 배경·자연 배경 기준은 유지한다(A-16).
        /// </summary>
        public bool TryBreakForeground(Vector3Int cell, int toolTier, out string droppedItemId, out int droppedAmount)
        {
            droppedItemId = null;
            droppedAmount = 0;

            if (!InBounds(cell)) return false;

            // 서리 pending: 공기 인접 시 광물로만 확정하고 이번 타격은 소비한다.
            if (FrostSpread != null && FrostSpread.TryRevealOnInteract(this, cell))
                return false;

            var current = tiles[cell.x, cell.y];
            if (current.IsAir) return false;
            if (IndestructibleElementTypes.Contains(current.elementType)) return false;
            if (toolTier < current.hardness) return false;

            if (IsDoorFootprintElement(current.elementType))
                return TryBreakDoorFootprint(ResolveDoorBaseCell(cell, current.elementType), toolTier,
                    out droppedItemId, out droppedAmount);

            var minedElementType = current.elementType;
            wallDamageTaken.Remove(cell);
            if (IsDoorOpen(cell)) RemoveOpenDoorState(ResolveDoorBaseForOpenState(cell));
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

#if UNITY_EDITOR
            if (renderer != null)
                Debug.Log($"[Nyangbingo] Mining cell cleared: cell={cell}, " +
                          $"dataAir={tiles[cell.x, cell.y].IsAir}, " +
                          $"foregroundTile={renderer.HasForegroundTile(cell)}, " +
                          $"foregroundCollision={renderer.HasForegroundCollision(cell)}");
#endif

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

            if (string.Equals(elementType, DoorElementType, StringComparison.Ordinal))
                return TryPlaceDoorFootprint(cell, consumeFrom, hardnessOverride);

            var current = tiles[cell.x, cell.y];
            if (!current.IsAir || IsForegroundPlacementBlocked(cell)) return false;

            if (consumeFrom != null && !consumeFrom.TryRemove(elementType, 1)) return false;

            wallDamageTaken.Remove(cell);
            WriteForegroundCell(cell, elementType, hardnessOverride);
            RecordChange(cell, elementType, placed: true);
            GameEvents.RaiseTilePlaced(cell);
            GameEvents.RaisePlacedObjectBuilt(elementType);
            return true;
        }

        /// <summary>드롭 없이 전경만 제거(단열 문 개폐용). raiseBroken=false면 OnTileBroken을 올리지 않는다.</summary>
        public bool TryClearForegroundWithoutDrop(Vector3Int cell, bool raiseBrokenEvent = true)
        {
            if (!InBounds(cell)) return false;
            var current = tiles[cell.x, cell.y];
            if (current.IsAir) return false;
            if (IndestructibleElementTypes.Contains(current.elementType)) return false;

            if (IsDoorFootprintElement(current.elementType))
                return TryClearDoorFootprint(ResolveDoorBaseCell(cell, current.elementType), raiseBrokenEvent);

            return ClearForegroundCell(cell, current.elementType, raiseBrokenEvent);
        }

        /// <summary>인벤 소비 없이 전경 복구(열린 단열 문 닫기).</summary>
        public bool TryRestoreForeground(Vector3Int cell, string elementType, int hardnessOverride = -1)
        {
            if (string.IsNullOrEmpty(elementType) || !InBounds(cell)) return false;
            elementType = TileIdAlias.ToCanonical(elementType);
            if (!SupportsForegroundPlacement(elementType)) return false;

            if (string.Equals(elementType, DoorElementType, StringComparison.Ordinal))
                return TryRestoreDoorFootprint(cell, hardnessOverride);

            var current = tiles[cell.x, cell.y];
            if (!current.IsAir) return false;

            WriteForegroundCell(cell, elementType, hardnessOverride);
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
            // door_top은 핫바 설치 대상이 아니다(문 설치 시 자동 생성).
            if (string.Equals(id, DoorTopElementType, StringComparison.Ordinal)) return false;
            return PlacementHardness.ContainsKey(id);
        }

        /// <summary>A-25: 대상 셀에 전경 설치 가능 여부(사전 판정, 인벤 미소비).</summary>
        public bool CanPlaceForeground(Vector3Int cell, string itemId)
        {
            if (!SupportsForegroundPlacement(itemId) || !InBounds(cell)) return false;
            itemId = TileIdAlias.ToCanonical(itemId);
            if (string.Equals(itemId, DoorElementType, StringComparison.Ordinal))
                return CanPlaceDoorFootprint(cell) && !IsForegroundPlacementBlocked(cell);
            return GetTile(cell).IsAir && !IsForegroundPlacementBlocked(cell);
        }

        public static bool IsDoorFootprintElement(string elementType) =>
            string.Equals(elementType, DoorElementType, StringComparison.Ordinal) ||
            string.Equals(elementType, DoorTopElementType, StringComparison.Ordinal);

        public static Vector3Int ResolveDoorBaseCell(Vector3Int cell, string elementType) =>
            string.Equals(elementType, DoorTopElementType, StringComparison.Ordinal)
                ? cell + Vector3Int.down
                : cell;

        public bool CanPlaceDoorFootprint(Vector3Int baseCell)
        {
            var head = baseCell + Vector3Int.up;
            return InBounds(baseCell) && InBounds(head) &&
                   GetTile(baseCell).IsAir && GetTile(head).IsAir &&
                   !IsForegroundPlacementBlocked(baseCell) &&
                   !IsForegroundPlacementBlocked(head);
        }

        /// <summary>이미 설치된 문 아래에 위칸 충돌이 없으면 투명 door_top을 보충한다.</summary>
        public bool TryEnsureDoorTop(Vector3Int baseCell)
        {
            if (!InBounds(baseCell)) return false;
            if (!string.Equals(GetTile(baseCell).elementType, DoorElementType, StringComparison.Ordinal))
                return false;
            var head = baseCell + Vector3Int.up;
            if (!InBounds(head)) return false;
            if (string.Equals(GetTile(head).elementType, DoorTopElementType, StringComparison.Ordinal))
                return true;
            if (!GetTile(head).IsAir) return false;
            WriteForegroundCell(head, DoorTopElementType, -1);
            RecordChange(head, DoorTopElementType, placed: true);
            GameEvents.RaiseTilePlaced(head);
            return true;
        }

        private bool TryPlaceDoorFootprint(Vector3Int baseCell, Nyangbingo.Inventory.Inventory consumeFrom,
            int hardnessOverride)
        {
            if (!CanPlaceDoorFootprint(baseCell)) return false;
            if (consumeFrom != null && !consumeFrom.TryRemove(DoorElementType, 1)) return false;

            wallDamageTaken.Remove(baseCell);
            wallDamageTaken.Remove(baseCell + Vector3Int.up);
            WriteForegroundCell(baseCell, DoorElementType, hardnessOverride);
            WriteForegroundCell(baseCell + Vector3Int.up, DoorTopElementType, hardnessOverride);
            RecordChange(baseCell, DoorElementType, placed: true);
            RecordChange(baseCell + Vector3Int.up, DoorTopElementType, placed: true);
            GameEvents.RaiseTilePlaced(baseCell);
            GameEvents.RaiseTilePlaced(baseCell + Vector3Int.up);
            return true;
        }

        private bool TryRestoreDoorFootprint(Vector3Int baseCell, int hardnessOverride)
        {
            if (!CanPlaceDoorFootprint(baseCell)) return false;
            WriteForegroundCell(baseCell, DoorElementType, hardnessOverride);
            WriteForegroundCell(baseCell + Vector3Int.up, DoorTopElementType, hardnessOverride);
            RecordChange(baseCell, DoorElementType, placed: true);
            RecordChange(baseCell + Vector3Int.up, DoorTopElementType, placed: true);
            GameEvents.RaiseTilePlaced(baseCell);
            GameEvents.RaiseTilePlaced(baseCell + Vector3Int.up);
            return true;
        }

        private bool TryClearDoorFootprint(Vector3Int baseCell, bool raiseBrokenEvent)
        {
            var head = baseCell + Vector3Int.up;
            var clearedAny = false;
            if (InBounds(baseCell))
            {
                var baseTile = GetTile(baseCell);
                if (IsDoorFootprintElement(baseTile.elementType))
                    clearedAny |= ClearForegroundCell(baseCell, baseTile.elementType, raiseBrokenEvent);
            }

            if (InBounds(head))
            {
                var headTile = GetTile(head);
                if (IsDoorFootprintElement(headTile.elementType))
                    clearedAny |= ClearForegroundCell(head, headTile.elementType, raiseBrokenEvent);
            }

            return clearedAny;
        }

        private bool TryBreakDoorFootprint(Vector3Int baseCell, int toolTier, out string droppedItemId,
            out int droppedAmount)
        {
            droppedItemId = null;
            droppedAmount = 0;
            if (!InBounds(baseCell)) return false;
            var baseTile = GetTile(baseCell);
            var head = baseCell + Vector3Int.up;
            var headTile = InBounds(head) ? GetTile(head) : default;
            if (!IsDoorFootprintElement(baseTile.elementType) &&
                !(InBounds(head) && IsDoorFootprintElement(headTile.elementType)))
                return false;

            var hardness = 0;
            if (IsDoorFootprintElement(baseTile.elementType)) hardness = Mathf.Max(hardness, baseTile.hardness);
            if (InBounds(head) && IsDoorFootprintElement(headTile.elementType))
                hardness = Mathf.Max(hardness, headTile.hardness);
            if (toolTier < hardness) return false;

            GameEvents.RaiseMiningImpact(MiningImpactSurface.Mineral);
            TryClearDoorFootprint(baseCell, raiseBrokenEvent: true);
            if (TryResolveDrop(DoorElementType, out var item, out var amount))
            {
                ItemAcquisition.Request(item, amount);
                droppedItemId = item.Id;
                droppedAmount = amount;
            }
            return true;
        }

        private void WriteForegroundCell(Vector3Int cell, string elementType, int hardnessOverride)
        {
            var current = tiles[cell.x, cell.y];
            var hardness = hardnessOverride > 0 ? hardnessOverride : ResolvePlacementHardness(elementType);
            tiles[cell.x, cell.y] = new TileData
            {
                hardness = hardness,
                isNaturalTerrain = false,
                elementType = elementType,
                backgroundElementType = string.IsNullOrEmpty(current.backgroundElementType)
                    ? WorldTileTypes.Air
                    : current.backgroundElementType,
                naturalBackgroundElementType = string.IsNullOrEmpty(current.naturalBackgroundElementType)
                    ? WorldTileTypes.Air
                    : current.naturalBackgroundElementType
            };
            ApplyForegroundVisual(cell, elementType);
            RefreshEdgeOverlayAround(cell);
            renderer?.NotifyForegroundCollisionDirty();
        }

        private bool ClearForegroundCell(Vector3Int cell, string clearedElementType, bool raiseBrokenEvent)
        {
            var current = tiles[cell.x, cell.y];
            if (current.IsAir) return false;
            var cleared = current.WithoutForeground();
            tiles[cell.x, cell.y] = cleared;
            ApplyForegroundVisual(cell, null);
            ApplyBackgroundVisual(cell, cleared.HasBackground ? cleared.backgroundElementType : null);
            RefreshEdgeOverlayAround(cell);
            renderer?.NotifyForegroundCollisionDirty();
            RecordChange(cell, clearedElementType, placed: false);
            if (raiseBrokenEvent) GameEvents.RaiseTileBroken(cell);
            return true;
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
                WorldItemDropRequest.Request(item, amount,
                    ResolveForegroundMiningDropWorldPosition(cell), cell);
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

        public bool RestoreTileChanges(IEnumerable<TileChangeRecord> records,
            ISet<Vector3Int> allowedAlreadyClearedCells = null,
            bool allowLegacyCollapsedPlacementRemovals = false)
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
                    if (original.IsAir)
                    {
                        if (allowedAlreadyClearedCells != null &&
                            allowedAlreadyClearedCells.Contains(cell))
                        {
                            RecordChange(cell, tileId, placed: false);
                            continue;
                        }

                        // 구버전은 플레이어 설치->제거를 마지막 제거 1건으로 압축했다.
                        // 현재 형식과 손상 레코드에는 이 추측을 적용하지 않고, 호출자가 실제
                        // 구 스키마임을 확인한 경우에만 상쇄된 무효 이력으로 버린다.
                        if (allowLegacyCollapsedPlacementRemovals && PlacementHardness.ContainsKey(tileId))
                            continue;

                        Debug.LogError($"[Nyangbingo] Tile restore removal expected '{tileId}' " +
                                       $"but found air at {cell}.");
                        return false;
                    }
                    if (IndestructibleElementTypes.Contains(original.elementType))
                    {
                        Debug.LogError($"[Nyangbingo] Tile restore attempted to remove protected " +
                                       $"'{original.elementType}' at {cell}.");
                        return false;
                    }
                    if (!string.Equals(original.elementType, tileId, StringComparison.Ordinal))
                    {
                        Debug.LogError($"[Nyangbingo] Tile restore removal mismatch at {cell}: " +
                                       $"saved='{tileId}', generated='{original.elementType}'.");
                        return false;
                    }

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

        public List<Vector3Int> GetValidSurfaceSpawnPositions(Vector3Int center, int minRange, int maxRange)
        {
            var results = new List<Vector3Int>();
            if (minRange < 0 || maxRange < minRange) return results;

            var minX = Mathf.Max(0, center.x - maxRange);
            var maxX = Mathf.Min(Width - 1, center.x + maxRange);
            var minRangeSquared = minRange * minRange;
            var maxRangeSquared = maxRange * maxRange;
            for (var x = minX; x <= maxX; x++)
            {
                var groundY = FindSurfaceNaturalY(x);
                if (groundY < 0) continue;
                var candidate = new Vector3Int(x, groundY + 1, center.z);
                var dx = candidate.x - center.x;
                var dy = candidate.y - center.y;
                var distanceSquared = dx * dx + dy * dy;
                if (distanceSquared < minRangeSquared || distanceSquared > maxRangeSquared ||
                    !IsSafeGroundSpawn(candidate)) continue;
                results.Add(candidate);
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
            // 같은 셀의 설치→제거도 순서대로 재생해야 한다. 마지막 상태로 압축하면
            // 원본 공기 셀에 "제거"만 남아 복원 시 무효 기록이 된다.
            changeIndexByCell[cell] = changeLog.Count;
            changeLog.Add(record);
        }

        private void RecordBackgroundChange(Vector3Int cell, string tileId, bool placed)
        {
            var record = new TileChangeRecord { x = cell.x, y = cell.y, z = cell.z, tileId = tileId, placed = placed };
            backgroundChangeIndexByCell[cell] = backgroundChangeLog.Count;
            backgroundChangeLog.Add(record);
        }
        private Vector3Int ResolveDoorBaseForOpenState(Vector3Int cell)
        {
            if (!InBounds(cell)) return cell;
            return ResolveDoorBaseCell(cell, GetTile(cell).elementType);
        }

        private void OpenDoor(Vector3Int cell)
        {
            cell = ResolveDoorBaseForOpenState(cell);
            if (!openDoors.Add(cell)) return;
            Sprite sprite = null;
            if (renderer?.Foreground != null)
                sprite = renderer.Foreground.GetSprite(cell);
            ApplyForegroundVisual(cell, null);
            var head = cell + Vector3Int.up;
            if (InBounds(head) &&
                string.Equals(GetTile(head).elementType, DoorTopElementType, StringComparison.Ordinal))
                ApplyForegroundVisual(head, null);
            if (sprite != null && renderer?.Foreground != null)
            {
                var visual = new GameObject($"OpenDoor_{cell.x}_{cell.y}");
                visual.transform.SetParent(renderer.Foreground.transform, false);
                visual.transform.position = renderer.GetCellVisualAnchorWorld(cell) +
                                            new Vector3(0f, .08f, 0f);
                visual.transform.rotation = Quaternion.Euler(0f, 0f, -90f);
                var spriteRenderer = visual.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = sprite;
                var tilemapRenderer =
                    renderer.Foreground.GetComponent<UnityEngine.Tilemaps.TilemapRenderer>();
                if (tilemapRenderer != null)
                {
                    spriteRenderer.sortingLayerID = tilemapRenderer.sortingLayerID;
                    spriteRenderer.sortingOrder = tilemapRenderer.sortingOrder + 1;
                }
                openDoorVisuals[cell] = visual;
            }
            renderer?.NotifyForegroundCollisionDirty();
        }

        private void CloseDoor(Vector3Int cell)
        {
            cell = ResolveDoorBaseForOpenState(cell);
            if (!openDoors.Remove(cell)) return;
            DestroyOpenDoorVisual(cell);
            ApplyForegroundVisual(cell, DoorElementType);
            var head = cell + Vector3Int.up;
            if (InBounds(head) &&
                string.Equals(GetTile(head).elementType, DoorTopElementType, StringComparison.Ordinal))
                ApplyForegroundVisual(head, DoorTopElementType);
            renderer?.NotifyForegroundCollisionDirty();
        }

        private void RemoveOpenDoorState(Vector3Int cell)
        {
            cell = ResolveDoorBaseForOpenState(cell);
            openDoors.Remove(cell);
            DestroyOpenDoorVisual(cell);
        }

        private void DestroyOpenDoorVisual(Vector3Int cell)
        {
            if (!openDoorVisuals.TryGetValue(cell, out var visual)) return;
            openDoorVisuals.Remove(cell);
            if (visual == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(visual);
            else UnityEngine.Object.DestroyImmediate(visual);
        }

        private bool TryResolveWallMaterial(Vector3Int cell, out YokaiWallMaterial material)
        {
            material = YokaiWallMaterial.Default;
            if (!InBounds(cell)) return false;
            var id = TileIdAlias.ToCanonical(tiles[cell.x, cell.y].elementType);
            if (string.Equals(id, "iron_insul_wall", StringComparison.Ordinal))
            {
                material = YokaiWallMaterial.IronHeatWall;
                return true;
            }
            if (IsDoorFootprintElement(id))
            {
                var baseCell = ResolveDoorBaseCell(cell, id);
                if (IsDoorOpen(baseCell)) return false;
                return true;
            }
            return string.Equals(id, "insul_wall", StringComparison.Ordinal) ||
                   string.Equals(id, "roof", StringComparison.Ordinal);
        }

        private float ResolveWallHitPoints(Vector3Int cell)
        {
            var id = TileIdAlias.ToCanonical(GetTile(cell).elementType);
            if (IsDoorFootprintElement(id))
                id = DoorElementType;
            if (string.Equals(id, "iron_insul_wall", StringComparison.Ordinal))
                return ReadPositiveGlobal("ice_storage_hp", DefaultIronInsulationWallHitPoints);
            if (string.Equals(id, "insul_wall", StringComparison.Ordinal) &&
                clayPlasterResolver?.Invoke(cell) == true)
                return ReadPositiveGlobal("insul_clay_wall_hp", DefaultClayPlasteredWallHitPoints);
            return ReadPositiveGlobal("ice_tile_hp", DefaultInsulationWallHitPoints);
        }

        private float ResolveMaximumPossibleWallHitPoints(Vector3Int cell)
        {
            var id = TileIdAlias.ToCanonical(GetTile(cell).elementType);
            if (IsDoorFootprintElement(id))
                id = DoorElementType;
            if (string.Equals(id, "iron_insul_wall", StringComparison.Ordinal))
                return ReadPositiveGlobal("ice_storage_hp", DefaultIronInsulationWallHitPoints);
            if (string.Equals(id, "insul_wall", StringComparison.Ordinal))
                return ReadPositiveGlobal("insul_clay_wall_hp", DefaultClayPlasteredWallHitPoints);
            return ReadPositiveGlobal("ice_tile_hp", DefaultInsulationWallHitPoints);
        }

        private float ReadPositiveGlobal(string key, float fallback)
        {
            var definition = catalog?.FindGlobal(key);
            return definition != null && definition.TryGetFloat(out var value) &&
                   IsFinite(value) && value > 0f
                ? value
                : fallback;
        }

        private bool DestroyWallWithoutDrop(Vector3Int cell)
        {
            if (!TryResolveWallMaterial(cell, out _)) return false;
            var current = tiles[cell.x, cell.y];
            if (IsDoorFootprintElement(current.elementType))
            {
                var baseCell = ResolveDoorBaseCell(cell, current.elementType);
                if (IsDoorOpen(baseCell)) RemoveOpenDoorState(baseCell);
                return TryClearDoorFootprint(baseCell, raiseBrokenEvent: true);
            }

            var destroyedId = current.elementType;
            if (IsDoorOpen(cell)) RemoveOpenDoorState(cell);
            var cleared = current.WithoutForeground();
            tiles[cell.x, cell.y] = cleared;
            ApplyForegroundVisual(cell, null);
            ApplyBackgroundVisual(cell, cleared.HasBackground ? cleared.backgroundElementType : null);
            RefreshEdgeOverlayAround(cell);
            renderer?.NotifyForegroundCollisionDirty();
            RecordChange(cell, destroyedId, placed: false);
            GameEvents.RaiseTileBroken(cell);
            return true;
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

    }
}
