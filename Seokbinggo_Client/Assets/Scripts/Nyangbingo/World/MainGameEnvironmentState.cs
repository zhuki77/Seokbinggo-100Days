using System;
using System.Collections.Generic;
using System.Linq;
using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.Save;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>
    /// 메인 플레이 세션의 실제 설치물·냉기원 상태. 공식 seal-whitelist 정책을 적용한 O(1) 셀 조회와
    /// 냉기원 가동 여부를 <see cref="WorldSessionController.ConfigureSealExtensions"/>에 공급한다.
    /// </summary>
    [DefaultExecutionOrder(-80)]
    [RequireComponent(typeof(MainGameBootstrap))]
    public sealed class MainGameEnvironmentState : MonoBehaviour, ISealBarrierRegistry, ICoolingSourceProvider,
        IGameSecondsTickable
    {
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
        private SealBoundaryPolicy boundaryPolicy;
        private CoolingSourceRuntime coolingSources;
        private float wallpaperDurationMultiplier = 1.25f;

        public bool IsColdSourceActive { get; private set; }
        public float CoolingCapPercent { get; private set; }
        public int PlacedObjectCount => byObjectId.Count;
        public int ActiveCoolingSourceCount { get; private set; }
        public BuildingArtCatalog BuildingArtCatalog => buildingArtCatalog;
        public bool IsInitialized { get; private set; }

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

            bootstrap.Session.ConfigureSealExtensions(this, this);
            coolingSources = new CoolingSourceRuntime(gameDataCatalog);
            var wallpaperBonus = gameDataCatalog.FindGlobal("wallpaper_coldsource_bonus");
            if (wallpaperBonus != null && wallpaperBonus.TryGetFloat(out var bonusPercent) &&
                !float.IsNaN(bonusPercent) && !float.IsInfinity(bonusPercent))
                wallpaperDurationMultiplier = 1f + Mathf.Max(0f, bonusPercent) * .01f;
            coolingSources.ConsumableExpired += HandleConsumableExpired;
            bootstrap.TickDriver.Register(this);
            IsInitialized = true;
            Debug.Log("[Nyangbingo] MainGameEnvironmentState: 공식 설치물 경계와 냉기원 상태를 " +
                      "메인 SealSystem에 연결 완료.");
            return true;
        }

        public bool IsRecognizedBarrier(Vector3Int cell) =>
            byCell.TryGetValue(cell, out var entry) && entry.BarrierActive &&
            boundaryPolicy != null && boundaryPolicy.SealsPlacedElement(entry.Record.definitionId);

        public bool TryPlace(PlacedObjectRecord record, bool barrierActive = true, bool coolingActive = false)
        {
            if (!IsInitialized && !Initialize()) return false;
            if (!IsValid(record)) return false;

            var cell = CellFrom(record.position);
            if (byObjectId.ContainsKey(record.objectId) || byCell.ContainsKey(cell)) return false;

            var entry = new Entry
            {
                Record = record,
                Cell = cell,
                BarrierActive = barrierActive && boundaryPolicy.SealsPlacedElement(record.definitionId),
                CoolingActive = coolingActive && !CoolingSourceRuntime.IsCoolingDefinition(record.definitionId)
            };
            if (CoolingSourceRuntime.IsCoolingDefinition(record.definitionId) &&
                !coolingSources.TryRegister(record.objectId, record.definitionId, coolingActive)) return false;
            byObjectId.Add(record.objectId, entry);
            byCell.Add(cell, entry);
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
            if (entry.BarrierActive == active) return true;
            entry.BarrierActive = active;
            InvalidateSeal();
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

        public bool TryGetCoolingStatus(string objectId, out float remainingGameSeconds,
            out float capPercent, out bool active)
        {
            remainingGameSeconds = 0f;
            capPercent = 0f;
            active = false;
            return coolingSources != null &&
                   coolingSources.TryGetStatus(objectId, out remainingGameSeconds, out capPercent, out active);
        }

        public bool TryGetNearestPlacedObject(Vector2 origin, float radius, out PlacedObjectRecord record)
        {
            record = default;
            if (!IsFinite(origin.x) || !IsFinite(origin.y) || !IsFinite(radius) || radius < 0f) return false;
            var found = false;
            var bestDistance = radius * radius;
            foreach (var entry in byObjectId.Values)
            {
                var distance = (entry.Record.position - origin).sqrMagnitude;
                if (distance > bestDistance || found && Mathf.Approximately(distance, bestDistance) &&
                    string.CompareOrdinal(entry.Record.objectId, record.objectId) >= 0) continue;
                found = true;
                bestDistance = distance;
                record = entry.Record;
            }
            return found;
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
            return !byCell.ContainsKey(cell) && tileService != null &&
                   cell.x >= 0 && ground.y >= 0 && cell.x < tileService.Width && head.y < tileService.Height &&
                   tileService.GetTile(cell).IsAir && tileService.GetTile(head).IsAir &&
                   !tileService.GetTile(ground).IsAir;
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
                var cell = CellFrom(record.position);
                if (restoredById.ContainsKey(record.objectId) || restoredByCell.ContainsKey(cell)) return false;
                var entry = new Entry
                {
                    Record = record,
                    Cell = cell,
                    BarrierActive = boundaryPolicy.SealsPlacedElement(record.definitionId),
                    CoolingActive = false
                };
                restoredById.Add(record.objectId, entry);
                restoredByCell.Add(cell, entry);

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
            IsColdSourceActive = ActiveCoolingSourceCount > 0;
            CoolingCapPercent = Mathf.Max(
                byObjectId.Values.Any(entry => entry.CoolingActive) ? 100f : 0f,
                coolingSources?.CoolingCapPercent ?? 0f);
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
            var beforeCap = coolingSources.CoolingCapPercent;
            coolingSources.Tick(deltaGameSeconds, ResolveCoolingDurationMultiplier());
            if (beforeCount != coolingSources.ActiveCount ||
                !Mathf.Approximately(beforeCap, coolingSources.CoolingCapPercent))
                RecomputeCoolingAndInvalidate();
        }

        private void HandleConsumableExpired(string objectId) => TryRemove(objectId);

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

        private void CreateVisual(Entry entry)
        {
            var art = buildingArtCatalog?.Find(entry.Record.definitionId);
            if (visualsByObjectId.ContainsKey(entry.Record.objectId)) return;
            var visual = new GameObject($"Placed_{entry.Record.objectId}");
            visual.transform.SetParent(transform, false);
            visual.transform.position = entry.Record.position;
            visual.transform.rotation = Quaternion.Euler(0f, 0f, entry.Record.rotationDegrees);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 12;
            if (art?.Sprite != null)
            {
                renderer.sprite = art.Sprite;
                visual.AddComponent<RuntimeBuildingSpriteAnimator>().Configure(art.Frames);
            }
            else
                RuntimePlaceholderVisual.Configure(renderer, new Color(.55f, .85f, 1f), .75f, 12);
            visualsByObjectId.Add(entry.Record.objectId, visual);
        }

        private void ClearVisuals()
        {
            foreach (var visual in visualsByObjectId.Values)
                if (visual != null) Destroy(visual);
            visualsByObjectId.Clear();
        }

        private static Vector3Int CellFrom(Vector2 position) =>
            new Vector3Int(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y), 0);

        private static bool IsValid(PlacedObjectRecord record) =>
            !string.IsNullOrWhiteSpace(record.objectId) &&
            !string.IsNullOrWhiteSpace(record.definitionId) &&
            IsFinite(record.position.x) && IsFinite(record.position.y) && IsFinite(record.rotationDegrees);

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private void OnDestroy()
        {
            bootstrap?.TickDriver?.Unregister(this);
            if (coolingSources != null) coolingSources.ConsumableExpired -= HandleConsumableExpired;
        }
    }
}
