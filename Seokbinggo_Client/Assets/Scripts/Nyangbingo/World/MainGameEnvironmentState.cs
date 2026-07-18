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
    public sealed class MainGameEnvironmentState : MonoBehaviour, ISealBarrierRegistry, ICoolingSourceProvider
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

        public bool IsColdSourceActive { get; private set; }
        public int PlacedObjectCount => byObjectId.Count;
        public int ActiveCoolingSourceCount { get; private set; }
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
                CoolingActive = coolingActive
            };
            byObjectId.Add(record.objectId, entry);
            byCell.Add(cell, entry);
            CreateVisual(entry);
            RecomputeCoolingAndInvalidate();
            return true;
        }

        public bool TryRemove(string objectId)
        {
            if (string.IsNullOrWhiteSpace(objectId) || !byObjectId.TryGetValue(objectId, out var entry)) return false;
            byObjectId.Remove(objectId);
            byCell.Remove(entry.Cell);
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
            if (entry.CoolingActive == active) return true;
            entry.CoolingActive = active;
            RecomputeCoolingAndInvalidate();
            return true;
        }

        public List<PlacedObjectRecord> ExportPlacedObjects() => byObjectId.Values
            .Select(entry => entry.Record)
            .OrderBy(record => record.objectId, StringComparer.Ordinal)
            .ToList();

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

        public bool TryRestorePlacedObjects(IEnumerable<PlacedObjectRecord> records)
        {
            if (!IsInitialized && !Initialize()) return false;
            if (records == null) return false;

            var restoredById = new Dictionary<string, Entry>(StringComparer.Ordinal);
            var restoredByCell = new Dictionary<Vector3Int, Entry>();
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
            }

            byObjectId.Clear();
            byCell.Clear();
            ClearVisuals();
            foreach (var pair in restoredById) byObjectId.Add(pair.Key, pair.Value);
            foreach (var pair in restoredByCell) byCell.Add(pair.Key, pair.Value);
            foreach (var entry in byObjectId.Values) CreateVisual(entry);
            RecomputeCoolingAndInvalidate();
            return true;
        }

        private void RecomputeCoolingAndInvalidate()
        {
            ActiveCoolingSourceCount = byObjectId.Values.Count(entry => entry.CoolingActive);
            IsColdSourceActive = ActiveCoolingSourceCount > 0;
            InvalidateSeal();
        }

        private void InvalidateSeal() => bootstrap?.SealSystem?.InvalidateAll();

        private void CreateVisual(Entry entry)
        {
            var art = buildingArtCatalog?.Find(entry.Record.definitionId);
            if (art?.Sprite == null || visualsByObjectId.ContainsKey(entry.Record.objectId)) return;
            var visual = new GameObject($"Placed_{entry.Record.objectId}");
            visual.transform.SetParent(transform, false);
            visual.transform.position = entry.Record.position;
            visual.transform.rotation = Quaternion.Euler(0f, 0f, entry.Record.rotationDegrees);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = art.Sprite;
            renderer.sortingOrder = 12;
            visual.AddComponent<RuntimeBuildingSpriteAnimator>().Configure(art.Frames);
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
    }
}
