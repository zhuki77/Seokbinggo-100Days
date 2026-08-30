using System;
using System.Collections.Generic;
using System.Linq;
using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using Nyangbingo.Save;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>
    /// 메인 플레이 세션의 실제 설치물·연료 상태. 공식 seal-whitelist 정책을 적용한 O(1) 셀 조회와
    /// 얼음 코어 위치를 절대 방온 시스템에 공급한다.
    /// </summary>
    [DefaultExecutionOrder(-80)]
    [RequireComponent(typeof(MainGameBootstrap))]
    public sealed class MainGameEnvironmentState : MonoBehaviour, ISealBarrierRegistry,
        IGameSecondsTickable
    {
        public const string MagpieNestDefinitionId = "magpie_nest";
        public const string StrawInsulationDefinitionId = "straw_insul";
        public const string ClayPlasterDefinitionId = "clay_plaster";
        public const string DoorPaperDefinitionId = "munpungji";
        public const string ColdWaveCoreDefinitionId = "cold_wave_core";
        public const string JukbuinDefinitionId = "jukbuin";
        public const string NestBedDefinitionId = "nest_bed";
        public const int StrawInsulationPieceCap = 6;
        private const float JukbuinNestRadiusSquared = 4f;

        private sealed class Entry
        {
            public PlacedObjectRecord Record;
            public Vector3Int Cell;
            public bool BarrierActive;
            public bool CoolingActive;
        }

        [SerializeField] private GameDataCatalog gameDataCatalog;
        [SerializeField] private MainGameBootstrap bootstrap;
        [SerializeField] private BuildingArtCatalog buildingArtCatalog;

        private readonly Dictionary<string, Entry> byObjectId =
            new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly Dictionary<Vector3Int, Entry> byCell = new Dictionary<Vector3Int, Entry>();
        private readonly Dictionary<string, GameObject> visualsByObjectId =
            new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private readonly HashSet<Vector3Int> tileDoorCells = new HashSet<Vector3Int>();
        private SealBoundaryPolicy boundaryPolicy;
        private CoolingSourceRuntime coolingSources;
        private float wallpaperDurationMultiplier = 1.25f;
        private bool suppressTileDoorSync;
        private float strawInsulationBonusPerPiece = .05f;

        public int PlacedObjectCount => byObjectId.Count;
        public int ActiveCoolingSourceCount { get; private set; }
        public int HeatStageReduction => byObjectId.Values.Any(entry =>
            entry.Record.definitionId == ColdWaveCoreDefinitionId) ? 1 : 0;
        public BuildingArtCatalog BuildingArtCatalog => buildingArtCatalog;
        public TileService TileService => bootstrap?.TileService;
        public bool IsInitialized { get; private set; }

        public const string DoorDefinitionId = "door";
        public const float OpenDoorVisualAlpha = .45f;

        public void ConfigureForScene(GameDataCatalog catalog, MainGameBootstrap mainBootstrap,
            BuildingArtCatalog artCatalog = null)
        {
            gameDataCatalog = catalog;
            bootstrap = mainBootstrap;
            buildingArtCatalog = artCatalog;
        }

        private void Start()
        {
            Initialize();
        }

        public bool Initialize()
        {
            if (IsInitialized) return true;
            bootstrap ??= GetComponent<MainGameBootstrap>();
            if (gameDataCatalog == null || bootstrap == null || !bootstrap.InitializeServices())
            {
                Debug.LogError("[Nyangbingo] MainGameEnvironmentState: GameDataCatalog 또는 MainGameBootstrap " +
                               "배선이 준비되지 않았습니다.");
                return false;
            }

            boundaryPolicy = new SealBoundaryPolicy(gameDataCatalog.SealWhitelist);
            if (!boundaryPolicy.IsValid)
            {
                Debug.LogError("[Nyangbingo] MainGameEnvironmentState: 공식 seal-whitelist 정책이 유효하지 않습니다.");
                return false;
            }

            RegisterBoundaryTileArt();

            bootstrap.Session.ConfigureSealExtensions(this);
            coolingSources = new CoolingSourceRuntime(gameDataCatalog);
            var wallpaperBonus = gameDataCatalog.FindGlobal("wallpaper_coldsource_bonus");
            if (wallpaperBonus != null && wallpaperBonus.TryGetFloat(out var bonusPercent) &&
                !float.IsNaN(bonusPercent) && !float.IsInfinity(bonusPercent))
                wallpaperDurationMultiplier = 1f + Mathf.Max(0f, bonusPercent) * .01f;
            var strawBonus = gameDataCatalog.FindGlobal("insul_straw_bonus");
            if (strawBonus != null && strawBonus.TryGetFloat(out var insulationBonus) &&
                !float.IsNaN(insulationBonus) && !float.IsInfinity(insulationBonus))
                strawInsulationBonusPerPiece = Mathf.Max(0f, insulationBonus);
            coolingSources.ConsumableExpired += HandleConsumableExpired;
            bootstrap.TickDriver.Register(this);
            GameEvents.OnTilePlaced += HandleTilePlaced;
            GameEvents.OnTileBroken += HandleTileBroken;
            GameEvents.OnTileBroken += HandleAttachmentSupportBroken;
            bootstrap.WorldReady += HandleWorldReady;
            bootstrap.WorldReady += BindWallHealthRuntime;
            BindWallHealthRuntime();
            IsInitialized = true;
            if (bootstrap.TileService != null) SyncTileDoorsFromWorld();
            Debug.Log("[Nyangbingo] MainGameEnvironmentState: 공식 설치물 경계와 냉기원 상태를 " +
                      "메인 SealSystem에 연결 완료.");
            return true;
        }

        private void RegisterBoundaryTileArt()
        {
            var renderer = bootstrap?.WorldRenderer;
            if (renderer == null || buildingArtCatalog == null) return;
            var boundaryIds = new[] { "insul_wall", "iron_insul_wall", "door", "roof" };
            for (var index = 0; index < boundaryIds.Length; index++)
            {
                var entry = buildingArtCatalog.Find(boundaryIds[index]);
                if (entry?.Sprite == null) continue;
                // 문은 닫힘 프레임을 타일 기본 스프라이트로 쓴다(카탈로그 Frames[0]=닫힘).
                var sprite = string.Equals(boundaryIds[index], DoorDefinitionId, StringComparison.Ordinal)
                    ? ResolveDoorSprite(entry, open: false) ?? entry.Sprite
                    : entry.Sprite;
                renderer.RegisterRuntimeForegroundTile(boundaryIds[index], sprite);
            }

            // 1x2 문 위칸: 비주얼은 아래 door 타일이 담당, 위는 충돌·밀폐만.
            renderer.RegisterRuntimeColliderOnlyForegroundTile(TileService.DoorTopElementType);
        }

        public bool IsRecognizedBarrier(Vector3Int cell) =>
            byCell.TryGetValue(cell, out var entry) && entry.BarrierActive &&
            boundaryPolicy != null && boundaryPolicy.SealsPlacedElement(entry.Record.definitionId);

        public bool HasPlacedDefinitionAtCell(Vector3Int cell, string definitionId) =>
            !string.IsNullOrWhiteSpace(definitionId) &&
            byCell.TryGetValue(cell, out var entry) &&
            string.Equals(entry.Record.definitionId, definitionId, StringComparison.Ordinal);

        public bool TryPlace(PlacedObjectRecord record, bool barrierActive = true, bool coolingActive = false)
        {
            if (!IsInitialized && !Initialize()) return false;
            if (!IsValid(record)) return false;

            var cell = CellFrom(record.position);
            if (IsInsulationAttachment(record.definitionId) &&
                !CanPlaceInsulationAt(record.definitionId, cell))
                return false;
            if (byObjectId.ContainsKey(record.objectId) ||
                IsGlobalSingletonDefinition(record.definitionId) &&
                byObjectId.Values.Any(existing =>
                    existing.Record.definitionId == record.definitionId))
                return false;

            var entry = new Entry
            {
                Record = record,
                Cell = cell,
                BarrierActive = barrierActive && boundaryPolicy.SealsPlacedElement(record.definitionId),
                CoolingActive = coolingActive && !CoolingSourceRuntime.IsCoolingDefinition(record.definitionId)
            };
            if (!IsInsulationAttachment(record.definitionId))
                TrySnapFloorPlacedObjectToTerrain(entry);
            if (byCell.ContainsKey(entry.Cell)) return false;
            if (CoolingSourceRuntime.IsCoolingDefinition(record.definitionId) &&
                !coolingSources.TryRegister(record.objectId, record.definitionId, coolingActive)) return false;
            byObjectId.Add(record.objectId, entry);
            byCell.Add(entry.Cell, entry);
            CreateVisual(entry);
            RecomputeCoolingAndInvalidate();
            GameEvents.RaisePlacedObjectBuilt(record.definitionId);
            return true;
        }

        public bool TryRemove(string objectId)
        {
            if (string.IsNullOrWhiteSpace(objectId) || !byObjectId.TryGetValue(objectId, out var entry)) return false;
            byObjectId.Remove(objectId);
            byCell.Remove(entry.Cell);
            var head = entry.Cell + Vector3Int.up;
            if (byCell.TryGetValue(head, out var headEntry) && ReferenceEquals(headEntry, entry))
                byCell.Remove(head);
            tileDoorCells.Remove(entry.Cell);
            tileDoorCells.Remove(head);
            coolingSources?.Remove(objectId);
            if (visualsByObjectId.TryGetValue(objectId, out var visual))
            {
                visualsByObjectId.Remove(objectId);
                if (visual != null) Destroy(visual);
            }
            RecomputeCoolingAndInvalidate();
            return true;
        }

        public bool SetBarrierActive(string objectId, bool active)
        {
            if (!byObjectId.TryGetValue(objectId, out var entry) ||
                !boundaryPolicy.SealsPlacedElement(entry.Record.definitionId)) return false;
            var changed = entry.BarrierActive != active;
            entry.BarrierActive = active;
            // 타일 문 개폐는 BarrierActive가 이미 맞춰진 뒤에도 오버레이를 반드시 갱신해야 한다.
            if (string.Equals(entry.Record.definitionId, DoorDefinitionId, StringComparison.Ordinal))
                RefreshDoorVisual(objectId, active);
            else if (changed)
                RefreshDoorVisual(objectId, active);
            if (changed) InvalidateSeal();
            return true;
        }

        /// <summary>
        /// 단열 문(설치물·전경 타일) 개폐. BarrierActive=true는 닫힘(밀폐 인정), false는 개방(밀폐 미인정).
        /// 전경 타일 문은 1x2(door+door_top)를 함께 치우거나 복구하고, 열린 모습은 반투명 오버레이로 남긴다.
        /// </summary>
        public bool TryToggleInsulationDoor(string objectId, out bool nowOpen)
        {
            nowOpen = false;
            if (!byObjectId.TryGetValue(objectId, out var entry) ||
                !string.Equals(entry.Record.definitionId, DoorDefinitionId, StringComparison.Ordinal))
                return false;

            var nextClosed = !entry.BarrierActive;
            if (tileDoorCells.Contains(entry.Cell))
            {
                var tileService = bootstrap?.TileService;
                if (tileService == null) return false;
                suppressTileDoorSync = true;
                try
                {
                    if (nextClosed)
                    {
                        if (!tileService.TryRestoreForeground(entry.Cell, DoorDefinitionId)) return false;
                    }
                    else if (!tileService.TryClearForegroundWithoutDrop(entry.Cell, raiseBrokenEvent: false))
                        return false;
                }
                finally
                {
                    suppressTileDoorSync = false;
                }
            }

            if (!SetBarrierActive(objectId, nextClosed)) return false;
            nowOpen = !nextClosed;
            return true;
        }

        public static string TileDoorObjectId(Vector3Int cell) =>
            $"tile_door_{cell.x}_{cell.y}";

        public bool TryRegisterTileDoor(Vector3Int cell, bool closed = true)
        {
            if (!IsInitialized && !Initialize()) return false;
            // door_top 클릭/이벤트가 와도 기준 칸은 아래 door 셀이다.
            var tileService = bootstrap?.TileService;
            if (tileService != null)
            {
                var tile = tileService.GetTile(cell);
                if (string.Equals(tile.elementType, TileService.DoorTopElementType, StringComparison.Ordinal))
                    cell = cell + Vector3Int.down;
            }

            var objectId = TileDoorObjectId(cell);
            var head = cell + Vector3Int.up;
            if (byObjectId.ContainsKey(objectId))
            {
                tileDoorCells.Add(cell);
                tileDoorCells.Add(head);
                if (byObjectId.TryGetValue(objectId, out var existing) && !byCell.ContainsKey(head))
                    byCell[head] = existing;
                return true;
            }

            if (byCell.ContainsKey(cell)) return false;
            if (byCell.ContainsKey(head)) return false;
            var record = new PlacedObjectRecord
            {
                objectId = objectId,
                definitionId = DoorDefinitionId,
                position = new Vector2(cell.x + .5f, cell.y + .5f),
                rotationDegrees = 0f
            };
            var entry = new Entry
            {
                Record = record,
                Cell = cell,
                BarrierActive = closed && boundaryPolicy.SealsPlacedElement(DoorDefinitionId),
                CoolingActive = false
            };
            byObjectId.Add(objectId, entry);
            byCell.Add(cell, entry);
            byCell.Add(head, entry);
            tileDoorCells.Add(cell);
            tileDoorCells.Add(head);
            InvalidateSeal();
            return true;
        }

        public bool TryUnregisterTileDoor(Vector3Int cell)
        {
            var tileService = bootstrap?.TileService;
            if (tileService != null)
            {
                var tile = tileService.GetTile(cell);
                if (string.Equals(tile.elementType, TileService.DoorTopElementType, StringComparison.Ordinal) ||
                    (tile.IsAir && tileDoorCells.Contains(cell + Vector3Int.down)))
                    cell = cell + Vector3Int.down;
            }

            var objectId = TileDoorObjectId(cell);
            var head = cell + Vector3Int.up;
            tileDoorCells.Remove(cell);
            tileDoorCells.Remove(head);
            if (byCell.TryGetValue(head, out var headEntry) &&
                headEntry != null &&
                string.Equals(headEntry.Record.objectId, objectId, StringComparison.Ordinal))
                byCell.Remove(head);
            return TryRemove(objectId);
        }

        private void HandleWorldReady() => SyncTileDoorsFromWorld();

        private void HandleTilePlaced(Vector3Int cell)
        {
            if (suppressTileDoorSync || bootstrap?.TileService == null) return;
            var tile = bootstrap.TileService.GetTile(cell);
            if (string.Equals(tile.elementType, DoorDefinitionId, StringComparison.Ordinal))
            {
                bootstrap.TileService.TryEnsureDoorTop(cell);
                TryRegisterTileDoor(cell, closed: true);
            }
            else if (string.Equals(tile.elementType, TileService.DoorTopElementType, StringComparison.Ordinal))
                TryRegisterTileDoor(cell + Vector3Int.down, closed: true);
            else if (tileDoorCells.Contains(cell) && tile.IsAir == false)
                TryUnregisterTileDoor(cell);
        }

        private void HandleTileBroken(Vector3Int cell)
        {
            if (suppressTileDoorSync) return;
            if (!tileDoorCells.Contains(cell)) return;
            TryUnregisterTileDoor(cell);
        }

        private void SyncTileDoorsFromWorld()
        {
            var tileService = bootstrap?.TileService;
            if (tileService == null) return;
            for (var x = 0; x < tileService.Width; x++)
            for (var y = 0; y < tileService.Height; y++)
            {
                var cell = new Vector3Int(x, y, 0);
                var tile = tileService.GetTile(cell);
                if (!string.Equals(tile.elementType, DoorDefinitionId, StringComparison.Ordinal)) continue;
                tileService.TryEnsureDoorTop(cell);
                TryRegisterTileDoor(cell, closed: true);
            }
        }

        public bool TryGetBarrierActive(string objectId, out bool active)
        {
            active = false;
            if (string.IsNullOrWhiteSpace(objectId) || !byObjectId.TryGetValue(objectId, out var entry))
                return false;
            active = entry.BarrierActive;
            return true;
        }

        public bool SetCoolingActive(string objectId, bool active)
        {
            if (!byObjectId.TryGetValue(objectId, out var entry)) return false;
            if (CoolingSourceRuntime.IsCoolingDefinition(entry.Record.definitionId)) return false;
            if (entry.CoolingActive == active) return true;
            entry.CoolingActive = active;
            RecomputeCoolingAndInvalidate();
            return true;
        }

        public bool TryAddIceJarFuel(string objectId, int units = 1)
        {
            if (coolingSources == null || !byObjectId.ContainsKey(objectId) ||
                !coolingSources.TryAddIceFuel(objectId, units)) return false;
            RecomputeCoolingAndInvalidate();
            return true;
        }

        public bool TryGetCoolingRemaining(string objectId, out float remainingGameSeconds)
        {
            remainingGameSeconds = 0f;
            return coolingSources != null &&
                   coolingSources.TryGetRemaining(objectId, out remainingGameSeconds);
        }

        public bool TryGetCoolingStatus(string objectId, out float remainingGameSeconds, out bool active)
        {
            remainingGameSeconds = 0f;
            active = false;
            return coolingSources != null &&
                   coolingSources.TryGetStatus(objectId, out remainingGameSeconds, out active);
        }

        public bool TryGetNearestPlacedObject(Vector2 origin, float radius, out PlacedObjectRecord record) =>
            TryGetNearestPlacedObject(origin, radius, preferNear: origin, out record);

        /// <summary>
        /// 플레이어 사거리(<paramref name="radius"/>) 안 설치물 중, 조준점(<paramref name="preferNear"/>)에
        /// 가장 가까운 것을 고른다. 범위가 겹칠 때 마우스 우선에 쓴다.
        /// </summary>
        public bool TryGetNearestPlacedObject(Vector2 origin, float radius, Vector2 preferNear,
            out PlacedObjectRecord record)
        {
            record = default;
            if (!IsFinite(origin.x) || !IsFinite(origin.y) || !IsFinite(radius) || radius < 0f ||
                !IsFinite(preferNear.x) || !IsFinite(preferNear.y))
                return false;
            var found = false;
            var reachSq = radius * radius;
            var bestAimDistance = float.PositiveInfinity;
            foreach (var entry in byObjectId.Values)
            {
                var toPlayer = (entry.Record.position - origin).sqrMagnitude;
                if (toPlayer > reachSq) continue;
                var toAim = (entry.Record.position - preferNear).sqrMagnitude;
                if (toAim > bestAimDistance || found && Mathf.Approximately(toAim, bestAimDistance) &&
                    string.CompareOrdinal(entry.Record.objectId, record.objectId) >= 0)
                    continue;
                found = true;
                bestAimDistance = toAim;
                record = entry.Record;
            }
            return found;
        }

        /// <summary>
        /// 좌클릭 회수용. 마우스 칸의 설치물을 우선하고, 스프라이트가 옆·아래 칸으로 넘어간 경우에도
        /// 비주얼 bounds 안의 설치물을 고른다(주변 지형 채굴로 새지 않게).
        /// </summary>
        public bool TryResolvePlacedObjectMiningTarget(Vector2 playerPosition, Vector2? mouseWorld,
            float reach, out PlacedObjectRecord record)
            => TryResolvePlacedObjectMiningTarget(playerPosition, mouseWorld, reach, out record, out _);

        public bool TryResolvePlacedObjectMiningTarget(Vector2 playerPosition, Vector2? mouseWorld,
            float reach, out PlacedObjectRecord record, out Vector3Int hitCell)
        {
            record = default;
            hitCell = default;
            if (!mouseWorld.HasValue ||
                !IsFinite(playerPosition.x) || !IsFinite(playerPosition.y) ||
                !IsFinite(reach) || reach <= 0f)
                return false;

            var mouse = mouseWorld.Value;
            if (!IsFinite(mouse.x) || !IsFinite(mouse.y)) return false;

            var reachSq = reach * reach;
            var mouseCell = bootstrap?.TileService != null
                ? bootstrap.TileService.WorldToCell(mouse)
                : CellFrom(mouse);
            if (byCell.TryGetValue(mouseCell, out var entry) && entry != null &&
                (entry.Record.position - playerPosition).sqrMagnitude <= reachSq)
            {
                record = entry.Record;
                hitCell = mouseCell;
                return true;
            }

            Entry best = null;
            var bestDistance = float.PositiveInfinity;
            var bestHit = default(Vector3Int);
            foreach (var candidate in byObjectId.Values)
            {
                if (candidate == null ||
                    (candidate.Record.position - playerPosition).sqrMagnitude > reachSq)
                    continue;
                if (!TryGetPlacedObjectSpriteBounds(candidate.Record.objectId, out var bounds) ||
                    !MainGameWorldDecorationRenderer.ContainsWorldPointXY(bounds, mouse))
                    continue;
                var distance = ((Vector2)bounds.center - mouse).sqrMagnitude;
                if (distance >= bestDistance) continue;
                best = candidate;
                bestDistance = distance;
                bestHit = ResolveOccupiedHitCell(candidate, mouseCell);
            }

            if (best == null) return false;
            record = best.Record;
            hitCell = bestHit;
            return true;
        }

        /// <summary>
        /// 설치 미리보기용. 기존 설치물 스프라이트 위면 그 설치물 칸으로 고정한다.
        /// </summary>
        public bool TryResolvePlacementCellUnderPlacedObject(Vector2 worldPosition, out Vector3Int cell)
        {
            cell = default;
            if (!IsFinite(worldPosition.x) || !IsFinite(worldPosition.y)) return false;

            Entry best = null;
            var bestArea = float.PositiveInfinity;
            var bestDistance = float.PositiveInfinity;
            foreach (var candidate in byObjectId.Values)
            {
                if (candidate == null ||
                    !TryGetPlacedObjectSpriteBounds(candidate.Record.objectId, out var bounds) ||
                    !MainGameWorldDecorationRenderer.ContainsWorldPointXY(bounds, worldPosition))
                    continue;
                var area = bounds.size.x * bounds.size.y;
                var distance = ((Vector2)bounds.center - worldPosition).sqrMagnitude;
                const float areaEpsilon = .0001f;
                if (area + areaEpsilon < bestArea ||
                    Mathf.Abs(area - bestArea) <= areaEpsilon && distance < bestDistance)
                {
                    best = candidate;
                    bestArea = area;
                    bestDistance = distance;
                }
            }

            if (best == null) return false;
            var mouseCell = bootstrap?.TileService != null
                ? bootstrap.TileService.WorldToCell(worldPosition)
                : CellFrom(worldPosition);
            cell = ResolveOccupiedHitCell(best, mouseCell);
            return true;
        }

        private bool TryGetPlacedObjectSpriteBounds(string objectId, out Bounds bounds)
        {
            bounds = default;
            if (string.IsNullOrWhiteSpace(objectId) ||
                !visualsByObjectId.TryGetValue(objectId, out var visual) || visual == null)
                return false;
            var renderer = visual.GetComponentInChildren<SpriteRenderer>();
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                return false;
            bounds = renderer.bounds;
            return true;
        }

        private static Vector3Int ResolveOccupiedHitCell(Entry entry, Vector3Int mouseCell)
        {
            if (entry == null) return mouseCell;
            if (mouseCell == entry.Cell || mouseCell == entry.Cell + Vector3Int.up)
                return mouseCell;
            return entry.Cell;
        }

        public List<PlacedObjectRecord> ExportPlacedObjects() => byObjectId.Values
            .Select(entry => entry.Record)
            .OrderBy(record => record.objectId, StringComparer.Ordinal)
            .ToList();

        public void CopyPlacedObjectPositions(string definitionId, List<Vector2> results, bool append = false)
        {
            if (results == null) return;
            if (!append) results.Clear();
            if (string.IsNullOrWhiteSpace(definitionId)) return;
            foreach (var entry in byObjectId.Values)
                if (entry.Record.definitionId == definitionId) results.Add(entry.Record.position);
        }

        /// <summary>
        /// v72 A-1: 현재 배치된 모든 얼음 저장고 코어 셀을 복사한다. 방 온도는 과거 냉기원 상한이 아니라
        /// 이 목록의 각 코어가 대상 셀을 덮는지 검사해 냉각량을 가산한다.
        /// </summary>
        public void CopyIceCoreCells(List<Vector3Int> results)
        {
            if (results == null) return;
            results.Clear();
            foreach (var entry in byObjectId.Values)
                if (entry.Record.definitionId == CoolingSourceRuntime.IceStorageId)
                    results.Add(entry.Cell);
        }

        public List<CoolingSourceStateRecord> ExportCoolingSources() => coolingSources?.ExportSnapshots()
            .Select(snapshot => new CoolingSourceStateRecord
            {
                objectId = snapshot.ObjectId,
                definitionId = snapshot.DefinitionId,
                remainingGameSeconds = snapshot.RemainingGameSeconds
            })
            .ToList() ?? new List<CoolingSourceStateRecord>();

        public bool TryGetVisual(string objectId, out GameObject visual)
        {
            visual = null;
            return !string.IsNullOrWhiteSpace(objectId) &&
                   visualsByObjectId.TryGetValue(objectId, out visual) && visual != null;
        }

        public bool CanPlaceAt(Vector2 position)
        {
            if (!IsFinite(position.x) || !IsFinite(position.y)) return false;
            var cell = CellFrom(position);
            var tileService = bootstrap?.TileService;
            var head = cell + Vector3Int.up;
            var ground = cell + Vector3Int.down;
            var decorations = GetComponent<MainGameWorldDecorationRenderer>();
            return !byCell.ContainsKey(cell) && tileService != null &&
                   cell.x >= 0 && ground.y >= 0 && cell.x < tileService.Width && head.y < tileService.Height &&
                   tileService.GetTile(cell).IsAir && tileService.GetTile(head).IsAir &&
                   (decorations == null ||
                    !decorations.IsForegroundPlacementBlocked(cell) &&
                    !decorations.IsForegroundPlacementBlocked(head)) &&
                   !tileService.GetTile(ground).IsAir;
        }

        public bool CanPlaceDefinitionAt(string definitionId, Vector2 position)
        {
            if (string.IsNullOrWhiteSpace(definitionId) ||
                !IsFinite(position.x) || !IsFinite(position.y))
                return false;
            if (IsGlobalSingletonDefinition(definitionId) &&
                byObjectId.Values.Any(entry => entry.Record.definitionId == definitionId))
                return false;
            return IsInsulationAttachment(definitionId)
                ? CanPlaceInsulationAt(definitionId, CellFrom(position))
                : CanPlaceAt(position);
        }

        public float ResolveTemperatureRecoveryMultiplier(Vector2 position, SealSystem seals)
        {
            if (seals == null || !IsFinite(position.x) || !IsFinite(position.y) ||
                !seals.TryGetDebugRegion(CellFrom(position), out var isSealed, out _,
                    out var interiorCells, out var boundaryCells) || !isSealed)
                return 1f;

            var insulationDefinitionIds = byObjectId.Values
                .Where(entry =>
                    (entry.Record.definitionId == StrawInsulationDefinitionId ||
                     entry.Record.definitionId == ClayPlasterDefinitionId) &&
                    boundaryCells.Contains(entry.Cell))
                .Select(entry => entry.Record.definitionId);
            var globalSettings = gameDataCatalog != null
                ? new GlobalSettings(gameDataCatalog.Globals)
                : null;
            var panelBonus = InsulationPanels.TotalFromDefinitions(insulationDefinitionIds, globalSettings);
            var hasIceCrystalCooler = byObjectId.Values.Any(entry =>
                entry.Record.definitionId == CoolingSourceRuntime.IceCrystalCoolerId &&
                interiorCells.Contains(entry.Cell));
            var multiplier = CalculateSealedRecoveryMultiplier(
                panelBonus, hasIceCrystalCooler);
            var tileService = bootstrap?.TileService;
            var hasUnpaperedOpenDoor = tileService != null && boundaryCells.Any(cell =>
                tileService.IsDoorOpen(cell) &&
                (!byCell.TryGetValue(cell, out var attachment) ||
                 attachment.Record.definitionId != DoorPaperDefinitionId));
            return CalculateDoorAdjustedRecoveryMultiplier(
                multiplier, hasUnpaperedOpenDoor);
        }

        public static float CalculateStrawInsulationRecoveryMultiplier(
            int attachedPieces, float bonusPerPiece)
        {
            if (attachedPieces <= 0 || bonusPerPiece <= 0f ||
                float.IsNaN(bonusPerPiece) || float.IsInfinity(bonusPerPiece))
                return 1f;
            return 1f + Mathf.Min(attachedPieces, StrawInsulationPieceCap) * bonusPerPiece;
        }

        public static float CalculateSealedRecoveryMultiplier(
            int attachedStrawPieces, float strawBonusPerPiece, bool hasIceCrystalCooler)
        {
            var insulationMultiplier = CalculateStrawInsulationRecoveryMultiplier(
                attachedStrawPieces, strawBonusPerPiece);
            return CalculateSealedRecoveryMultiplier(
                Mathf.Clamp01(Mathf.Max(0f, insulationMultiplier - 1f)), hasIceCrystalCooler);
        }

        public static float CalculateSealedRecoveryMultiplier(
            float insulationPanelBonus, bool hasIceCrystalCooler)
        {
            var insulationMultiplier = 1f + Mathf.Max(0f, insulationPanelBonus);
            return insulationMultiplier * (hasIceCrystalCooler ? 2f : 1f);
        }

        public static float CalculateDoorAdjustedRecoveryMultiplier(
            float sealedRecoveryMultiplier, bool hasUnpaperedOpenDoor)
        {
            if (sealedRecoveryMultiplier <= 0f ||
                float.IsNaN(sealedRecoveryMultiplier) ||
                float.IsInfinity(sealedRecoveryMultiplier))
                return 0f;
            return hasUnpaperedOpenDoor ? 0f : sealedRecoveryMultiplier;
        }

        public float ResolveJukbuinRegenMultiplier(Vector2 playerPosition)
        {
            if (!IsFinite(playerPosition.x) || !IsFinite(playerPosition.y)) return 1f;
            Vector2? bedPosition = null;
            foreach (var entry in byObjectId.Values)
            {
                if (entry.Record.definitionId != NestBedDefinitionId) continue;
                if ((entry.Record.position - playerPosition).sqrMagnitude > JukbuinNestRadiusSquared)
                    continue;
                bedPosition = entry.Record.position;
                break;
            }
            if (!bedPosition.HasValue) return 1f;
            var hasJukbuin = byObjectId.Values.Any(entry =>
                entry.Record.definitionId == JukbuinDefinitionId &&
                (entry.Record.position - bedPosition.Value).sqrMagnitude <= JukbuinNestRadiusSquared);
            if (!hasJukbuin) return 1f;
            var definition = gameDataCatalog?.FindGlobal("jukbuin_regen_mult");
            return definition != null && definition.TryGetFloat(out var multiplier) && multiplier > 0f
                ? multiplier
                : 1.5f;
        }

        public bool HasPlacedObjectWithin(string definitionId, Vector2 position, float radius)
        {
            if (string.IsNullOrWhiteSpace(definitionId) || !IsFinite(position.x) || !IsFinite(position.y) ||
                !IsFinite(radius) || radius < 0f) return false;
            var radiusSquared = radius * radius;
            return byObjectId.Values.Any(entry => entry.Record.definitionId == definitionId &&
                (entry.Record.position - position).sqrMagnitude <= radiusSquared);
        }

        public bool TryGetNearestPlacedObjectPosition(string definitionId, Vector2 origin, out Vector2 position)
        {
            position = default;
            if (string.IsNullOrWhiteSpace(definitionId) || !IsFinite(origin.x) || !IsFinite(origin.y)) return false;
            var found = false;
            var bestDistance = float.PositiveInfinity;
            foreach (var entry in byObjectId.Values)
            {
                if (entry.Record.definitionId != definitionId) continue;
                var distance = (entry.Record.position - origin).sqrMagnitude;
                if (found && distance >= bestDistance) continue;
                found = true;
                bestDistance = distance;
                position = entry.Record.position;
            }
            return found;
        }

        public bool TryRestorePlacedObjects(IEnumerable<PlacedObjectRecord> records) =>
            TryRestorePlacedObjects(records, null);

        public bool TryRestorePlacedObjects(IEnumerable<PlacedObjectRecord> records,
            IEnumerable<CoolingSourceStateRecord> coolingStateRecords)
        {
            if (!IsInitialized && !Initialize()) return false;
            if (records == null) return false;

            var restoredById = new Dictionary<string, Entry>(StringComparer.Ordinal);
            var restoredByCell = new Dictionary<Vector3Int, Entry>();
            var restoredCooling = new CoolingSourceRuntime(gameDataCatalog);
            var restoredMagpieNestCount = 0;
            var restoredColdWaveCoreCount = 0;
            var coolingStateById = new Dictionary<string, CoolingSourceStateRecord>(StringComparer.Ordinal);
            if (coolingStateRecords != null)
            {
                foreach (var state in coolingStateRecords)
                {
                    if (string.IsNullOrWhiteSpace(state.objectId) ||
                        coolingStateById.ContainsKey(state.objectId)) return false;
                    coolingStateById.Add(state.objectId, state);
                }
            }
            foreach (var record in records)
            {
                if (!IsValid(record)) return false;
                if (record.definitionId == MagpieNestDefinitionId &&
                    ++restoredMagpieNestCount > 1)
                    return false;
                if (record.definitionId == ColdWaveCoreDefinitionId &&
                    ++restoredColdWaveCoreCount > 1)
                    return false;
                var cell = CellFrom(record.position);
                if (IsInsulationAttachment(record.definitionId) &&
                    !CanPlaceInsulationAt(record.definitionId, cell, checkRuntimeOccupancy: false))
                    return false;
                if (restoredById.ContainsKey(record.objectId) || restoredByCell.ContainsKey(cell)) return false;
                var entry = new Entry
                {
                    Record = record,
                    Cell = cell,
                    BarrierActive = boundaryPolicy.SealsPlacedElement(record.definitionId),
                    CoolingActive = false
                };
                if (!IsInsulationAttachment(record.definitionId))
                    TrySnapFloorPlacedObjectToTerrain(entry);
                restoredById.Add(record.objectId, entry);
                restoredByCell.Add(entry.Cell, entry);

                if (!CoolingSourceRuntime.IsCoolingDefinition(record.definitionId)) continue;
                if (coolingStateById.TryGetValue(record.objectId, out var state))
                {
                    if (state.definitionId != record.definitionId ||
                        !restoredCooling.TryRestore(record.objectId, record.definitionId,
                            state.remainingGameSeconds)) return false;
                    coolingStateById.Remove(record.objectId);
                }
                else if (!restoredCooling.TryRegister(record.objectId, record.definitionId)) return false;
            }
            if (coolingStateById.Count != 0) return false;

            byObjectId.Clear();
            byCell.Clear();
            ClearVisuals();
            foreach (var pair in restoredById) byObjectId.Add(pair.Key, pair.Value);
            foreach (var pair in restoredByCell) byCell.Add(pair.Key, pair.Value);
            coolingSources.ConsumableExpired -= HandleConsumableExpired;
            coolingSources = restoredCooling;
            coolingSources.ConsumableExpired += HandleConsumableExpired;
            foreach (var entry in byObjectId.Values) CreateVisual(entry);
            RecomputeCoolingAndInvalidate();
            return true;
        }

        private void RecomputeCoolingAndInvalidate()
        {
            ActiveCoolingSourceCount = byObjectId.Values.Count(entry => entry.CoolingActive) +
                                       (coolingSources?.ActiveCount ?? 0);
            var iceStorage = byObjectId.Values
                .Where(entry => entry.Record.definitionId == CoolingSourceRuntime.IceStorageId)
                .OrderBy(entry => entry.Record.objectId, StringComparer.Ordinal)
                .FirstOrDefault();
            var sealSystem = bootstrap?.SealSystem;
            if (iceStorage != null)
                sealSystem?.SetSealCoreCell(iceStorage.Cell);
            else
                sealSystem?.ClearSealCoreCell();
            InvalidateSeal();
        }

        public void Tick(float deltaGameSeconds)
        {
            if (coolingSources == null) return;
            var beforeCount = coolingSources.ActiveCount;
            coolingSources.Tick(deltaGameSeconds, ResolveCoolingDurationMultiplier());
            if (beforeCount != coolingSources.ActiveCount)
                RecomputeCoolingAndInvalidate();
        }

        private void HandleConsumableExpired(string objectId) => TryRemove(objectId);

        private void HandleAttachmentSupportBroken(Vector3Int cell)
        {
            if (!byCell.TryGetValue(cell, out var entry) ||
                !IsInsulationAttachment(entry.Record.definitionId))
                return;
            var definitionId = entry.Record.definitionId;
            if (!TryRemove(entry.Record.objectId)) return;
            var item = gameDataCatalog?.FindItem(definitionId);
            if (item != null) ItemAcquisition.Request(item, 1);
        }

        private float ResolveCoolingDurationMultiplier()
        {
            var sealSystem = bootstrap?.SealSystem;
            var coreCell = sealSystem?.SealCoreCell;
            var coverage = bootstrap?.Session?.WallpaperCoverage;
            return coreCell.HasValue && coverage?.IsCoverageComplete(coreCell.Value) == true
                ? wallpaperDurationMultiplier
                : 1f;
        }

        private void InvalidateSeal() => bootstrap?.SealSystem?.InvalidateAll();

        private void BindWallHealthRuntime()
        {
            bootstrap?.TileService?.SetClayPlasterResolver(cell =>
                HasPlacedDefinitionAtCell(cell, ClayPlasterDefinitionId));
        }

        private void CreateVisual(Entry entry)
        {
            var art = buildingArtCatalog?.Find(entry.Record.definitionId);
            if (visualsByObjectId.ContainsKey(entry.Record.objectId)) return;
            var visual = new GameObject($"Placed_{entry.Record.objectId}");
            visual.transform.SetParent(transform, false);
            visual.transform.position = entry.Record.position;
            visual.transform.rotation = Quaternion.Euler(0f, 0f, entry.Record.rotationDegrees);
            var artObject = new GameObject("Art");
            artObject.transform.SetParent(visual.transform, false);
            var renderer = artObject.AddComponent<SpriteRenderer>();
            var isDoor = string.Equals(entry.Record.definitionId, DoorDefinitionId, StringComparison.Ordinal);
            var sortingOrder = IsInsulationAttachment(entry.Record.definitionId) ? 13 : 12;
            renderer.sortingOrder = sortingOrder;
            if (art?.Sprite != null)
            {
                // 문은 개폐 프레임 시트가 있어 루프 애니를 붙이면 닫힘/열림이 깜빡인다.
                renderer.sprite = isDoor
                    ? ResolveDoorSprite(art, open: false) ?? art.Sprite
                    : art.Sprite;
                if (!isDoor && art.Frames != null && art.Frames.Count > 0)
                    artObject.AddComponent<RuntimeBuildingSpriteAnimator>().Configure(art.Frames);
            }
            else
                RuntimePlaceholderVisual.Configure(
                    renderer, new Color(.55f, .85f, 1f), .75f, sortingOrder);
            if (IsInsulationAttachment(entry.Record.definitionId))
                TileService?.AlignSpriteBoundsToCellBase(renderer, entry.Cell);
            else
                AlignPlacedFloorVisual(renderer, entry);
            SnapPlacedVisualRoot(visual, renderer, entry);
            visualsByObjectId.Add(entry.Record.objectId, visual);
            if (isDoor)
                RefreshDoorVisual(entry.Record.objectId, entry.BarrierActive);
        }

        private void RefreshDoorVisual(string objectId, bool barrierActive)
        {
            if (!byObjectId.TryGetValue(objectId, out var entry)) return;
            var isTileDoor = tileDoorCells.Contains(entry.Cell);

            if (isTileDoor)
            {
                if (barrierActive)
                {
                    HideTileDoorOpenVisual(objectId);
                    return;
                }

                ShowTileDoorOpenVisual(entry);
                return;
            }

            if (!visualsByObjectId.TryGetValue(objectId, out var visual) || visual == null) return;
            var leftoverAnimator = visual.GetComponent<RuntimeBuildingSpriteAnimator>();
            if (leftoverAnimator != null) Destroy(leftoverAnimator);
            var renderer = visual.GetComponent<SpriteRenderer>();
            if (renderer == null) return;
            var sprite = ResolveDoorSprite(buildingArtCatalog?.Find(DoorDefinitionId), open: !barrierActive);
            if (sprite != null) renderer.sprite = sprite;
            var color = renderer.color;
            color.a = barrierActive ? 1f : OpenDoorVisualAlpha;
            renderer.color = color;
        }

        private void ShowTileDoorOpenVisual(Entry entry)
        {
            if (entry == null) return;
            // 닫힌 door 타일과 동일: 하단 피벗을 tileAnchor(셀 중심)에 둔다.
            var worldPosition = bootstrap?.WorldRenderer != null
                ? bootstrap.WorldRenderer.GetTilePivotWorld(entry.Cell)
                : new Vector3(entry.Cell.x + .5f, entry.Cell.y + .5f, 0f);
            if (!visualsByObjectId.TryGetValue(entry.Record.objectId, out var visual) || visual == null)
            {
                visual = new GameObject($"OpenDoor_{entry.Record.objectId}");
                visual.transform.SetParent(transform, false);
                visual.transform.position = worldPosition;
                visual.AddComponent<SpriteRenderer>().sortingOrder = 13;
                visualsByObjectId[entry.Record.objectId] = visual;
            }

            // 이전 빌드에서 붙인 루프 애니메이터가 있으면 제거한다.
            var leftoverAnimator = visual.GetComponent<RuntimeBuildingSpriteAnimator>();
            if (leftoverAnimator != null) Destroy(leftoverAnimator);

            visual.SetActive(true);
            visual.transform.position = worldPosition;
            var spriteRenderer = visual.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null) return;
            // 문은 개폐 프레임이 들어 있어 루프 애니메이션하면 깜빡인다. 열린 모습은 마지막 프레임 고정.
            var openSprite = ResolveDoorSprite(buildingArtCatalog?.Find(DoorDefinitionId), open: true);
            if (openSprite != null) spriteRenderer.sprite = openSprite;
            else RuntimePlaceholderVisual.Configure(spriteRenderer, new Color(.55f, .85f, 1f), .75f, 13);
            TileService?.AlignSpriteBoundsToCellBase(spriteRenderer, entry.Cell);
            var color = spriteRenderer.color;
            color.a = OpenDoorVisualAlpha;
            spriteRenderer.color = color;
        }

        private void HideTileDoorOpenVisual(string objectId)
        {
            if (!visualsByObjectId.TryGetValue(objectId, out var visual) || visual == null) return;
            visual.SetActive(false);
        }

        private static Sprite ResolveDoorSprite(BuildingArtCatalog.Entry art, bool open)
        {
            if (art == null || art.Frames == null || art.Frames.Count == 0) return null;
            if (!open) return art.Frames[0];
            return art.Frames[art.Frames.Count - 1];
        }

        private void ClearVisuals()
        {
            foreach (var visual in visualsByObjectId.Values)
                if (visual != null) Destroy(visual);
            visualsByObjectId.Clear();
        }

        private Vector3Int CellFrom(Vector2 position) => TileService != null
            ? TileService.WorldToCell(position)
            : new Vector3Int(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y), 0);

        private bool TrySnapFloorPlacedObjectToTerrain(Entry entry)
        {
            var tileService = TileService;
            if (tileService == null || entry == null) return false;
            var columnX = Mathf.Clamp(
                Mathf.FloorToInt(entry.Record.position.x), 0, tileService.Width - 1);
            var preferredY = entry.Cell.y;
            for (var y = preferredY + 4; y >= preferredY - 12; y--)
            {
                if (!TryResolveFloorPlacementCell(tileService, columnX, y, out var placementCell))
                    continue;
                var bounds = tileService.GetCellWorldBounds(placementCell);
                entry.Cell = placementCell;
                var record = entry.Record;
                record.position = new Vector2(bounds.center.x, bounds.center.y);
                entry.Record = record;
                return true;
            }
            return false;
        }

        private static bool TryResolveFloorPlacementCell(
            TileService tileService, int columnX, int candidateY, out Vector3Int placementCell)
        {
            placementCell = new Vector3Int(columnX, candidateY, 0);
            if (tileService == null || !tileService.InBounds(placementCell)) return false;
            var head = placementCell + Vector3Int.up;
            var ground = placementCell + Vector3Int.down;
            if (!tileService.InBounds(head) || !tileService.InBounds(ground)) return false;
            return tileService.GetTile(placementCell).IsAir &&
                   tileService.GetTile(head).IsAir &&
                   !tileService.GetTile(ground).IsAir;
        }

        private void AlignPlacedFloorVisual(SpriteRenderer renderer, Entry entry)
        {
            if (renderer == null || TileService == null) return;
            TileService.AlignSpriteBoundsToCellBase(renderer, entry.Cell);
        }

        private static void SnapPlacedVisualRoot(GameObject root, SpriteRenderer renderer, Entry entry)
        {
            if (root == null || renderer == null || entry == null) return;
            var alignedWorldPosition = renderer.transform.position;
            root.transform.position = alignedWorldPosition;
            renderer.transform.localPosition = Vector3.zero;
            var record = entry.Record;
            record.position = alignedWorldPosition;
            entry.Record = record;
        }

        private bool CanPlaceInsulationAt(string definitionId, Vector3Int cell,
            bool checkRuntimeOccupancy = true)
        {
            var tileService = bootstrap?.TileService;
            if (tileService == null || !tileService.InBounds(cell) ||
                checkRuntimeOccupancy && byCell.ContainsKey(cell))
                return false;
            var targetId = TileIdAlias.ToCanonical(tileService.GetTile(cell).elementType);
            switch (definitionId)
            {
                case StrawInsulationDefinitionId:
                    return targetId == "insul_wall" || targetId == "iron_insul_wall" ||
                           targetId == "roof";
                case ClayPlasterDefinitionId:
                    return targetId == "insul_wall";
                case DoorPaperDefinitionId:
                    return targetId == "door";
                default:
                    return false;
            }
        }

        private static bool IsInsulationAttachment(string definitionId) =>
            definitionId == StrawInsulationDefinitionId ||
            definitionId == ClayPlasterDefinitionId ||
            definitionId == DoorPaperDefinitionId;

        private static bool IsGlobalSingletonDefinition(string definitionId) =>
            definitionId == MagpieNestDefinitionId ||
            definitionId == ColdWaveCoreDefinitionId;

        private static bool IsValid(PlacedObjectRecord record) =>
            !string.IsNullOrWhiteSpace(record.objectId) &&
            !string.IsNullOrWhiteSpace(record.definitionId) &&
            IsFinite(record.position.x) && IsFinite(record.position.y) && IsFinite(record.rotationDegrees);

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private void OnDestroy()
        {
            GameEvents.OnTileBroken -= HandleAttachmentSupportBroken;
            if (bootstrap != null) bootstrap.WorldReady -= BindWallHealthRuntime;
            bootstrap?.TileService?.SetClayPlasterResolver(null);
            bootstrap?.TickDriver?.Unregister(this);
            GameEvents.OnTilePlaced -= HandleTilePlaced;
            GameEvents.OnTileBroken -= HandleTileBroken;
            if (bootstrap != null) bootstrap.WorldReady -= HandleWorldReady;
            if (coolingSources != null) coolingSources.ConsumableExpired -= HandleConsumableExpired;
        }
    }
}
