using System;
using System.Collections.Generic;
using System.Linq;
using Nyangbingo.Data;
using Nyangbingo.Core;
using Nyangbingo.Save;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>v86 S2. 밀폐 진단 상태만 발행하며 월드·문·저장 데이터를 변경하지 않는다.</summary>
    public sealed class MainGameBuildingGuide : IDisposable
    {
        // Grid bounds already describe the actual leak cell. A global lift points at
        // the wall above the opening; only a two-cell door needs half-cell centering.
        public const float LeakVisualYOffset = 0f;

        public static float GetLeakVisualYOffset(SealSystem seal, TileService tiles, Vector3Int cell) =>
            tiles != null && seal != null && seal.IsDoorCell(cell)
                ? tiles.GetCellWorldBounds(cell).size.y * .5f : LeakVisualYOffset;
        private readonly Dictionary<Vector3Int, RepairTarget> repairTargets =
            new Dictionary<Vector3Int, RepairTarget>();
        private readonly struct RepairTarget
        {
            public readonly bool WasBoundary;
            public readonly float VisualYOffset;
            public RepairTarget(bool wasBoundary, float visualYOffset)
            { WasBoundary = wasBoundary; VisualYOffset = visualYOffset; }
        }
        private readonly MainGameBootstrap bootstrap;
        private readonly MainGameEnvironmentState environment;
        private readonly MainGameGoalTracker goals;
        private readonly MainGameRuntimeServices runtime;
        private TileService damageTiles;
        private readonly HashSet<Vector3Int> invasionBroken = new HashSet<Vector3Int>();
        private bool miningVisible, miningMineable, heatBaseline;
        private Vector3Int miningCell;
        private int miningRevision = -1;
        private Vector3Int? miningCore;
        private SealSystem miningSeal;
        private float previousHeat;
        public bool MiningSealBoundary { get; private set; }
        public Vector3Int MiningCell => miningCell;
        public IReadOnlyCollection<Vector3Int> DamagedCells => invasionBroken;
        public int LastBaseAttackDay { get; private set; }
        public int LastCompletedRecoveryDay { get; private set; }
        public float HeatRemaining => runtime?.Invasion?.TemperatureRiseCelsius ?? 0f;
        public int StorageChecked { get; private set; }
        public int StorageMet { get; private set; }
        public bool StorageReady => StorageChecked > 0 && StorageChecked == StorageMet;
        public bool ShowRecoveryChecklist { get; private set; }
        private int revision = -1, restoreVersion = -1;
        private SealSystem previousSeal;
        private Vector3Int? previousCore;
        private bool baseline;
        public Vector3Int? Core { get; private set; }
        public bool IsSealed { get; private set; }
        public IReadOnlyList<Vector3Int> MissingCells { get; private set; } = Array.Empty<Vector3Int>();
        public IReadOnlyList<Vector3Int> OpenDoors { get; private set; } = Array.Empty<Vector3Int>();
        public event Action<Vector3Int, float> Repaired;
        public event Action SealedNow;
        public event Action<string> Fired;
        public string PlacementBlock { get; private set; }
        public Vector3Int PlacementCell { get; private set; }
        public Vector2Int OutlineInnerSize { get; }

        public MainGameBuildingGuide(GameDataCatalog data, MainGameBootstrap mainBootstrap,
            MainGameEnvironmentState state, MainGameGoalTracker tracker, MainGameRuntimeServices services = null)
        {
            bootstrap = mainBootstrap; environment = state; goals = tracker;
            runtime = services;
            var size = data.FindGlobal("shelter_outline_inner")?.Value?.Split('x');
            if (size == null || size.Length != 2 || !int.TryParse(size[0], out var width) ||
                !int.TryParse(size[1], out var height) || width < 2 || height < 2)
                throw new InvalidOperationException("Invalid shelter_outline_inner.");
            OutlineInnerSize = new Vector2Int(width, height);
            if (runtime != null) GameEvents.OnMiningTargetChanged += HandleMiningTarget;
        }

        private void HandleMiningTarget(Vector3Int cell, bool visible, bool mineable)
        {
            if (cell != miningCell || visible != miningVisible || mineable != miningMineable) miningRevision = -1;
            miningCell = cell; miningVisible = visible; miningMineable = mineable;
            RefreshMiningWarning();
        }

        public void RefreshMiningWarning(Vector2 origin)
        {
            if (goals.IsSuspended || bootstrap.Session?.HasWorld != true || environment == null ||
                !environment.IsInitialized || bootstrap.TileService == null || bootstrap.SealSystem == null)
            { MiningSealBoundary = false; miningRevision = -1; return; }
            Core = environment.TryGetNearestPlacedObjectPosition("ice_core", origin, out var position)
                ? bootstrap.TileService.WorldToCell(position) : (Vector3Int?)null;
            RefreshMiningWarning();
        }

        private void RefreshMiningWarning()
        {
            var seal = bootstrap.SealSystem;
            if (seal == null || goals.IsSuspended)
            { MiningSealBoundary = false; miningRevision = -1; return; }
            if (miningRevision == seal.Revision && miningCore == Core && miningSeal == seal) return;
            // The cell and its result are updated together. No previous-cell warning is reused.
            MiningSealBoundary = miningVisible && miningMineable && Core.HasValue &&
                seal.WouldBreakCoreSeal(Core.Value, miningCell);
            miningRevision = seal.Revision; miningCore = Core; miningSeal = seal;
        }

        private void BindDamageTiles()
        {
            if (damageTiles == bootstrap.TileService) return;
            if (damageTiles != null) damageTiles.WallDestroyedByDamage -= HandleWallDestroyed;
            damageTiles = bootstrap.TileService;
            if (damageTiles != null) damageTiles.WallDestroyedByDamage += HandleWallDestroyed;
        }

        private void HandleWallDestroyed(Vector3Int anchor, int height)
        {
            if (goals.IsSuspended || runtime?.Invasion?.IsCurrentInvasionNight != true || !Core.HasValue ||
                bootstrap.SealSystem?.IsInCoreWindow(Core.Value, anchor) != true) return;
            for (var i = 0; i < height; i++) invasionBroken.Add(anchor + Vector3Int.up * i);
            LastBaseAttackDay = bootstrap.TimeService.Day;
        }

        private void RefreshMaintenance()
        {
            BindDamageTiles();
            var seal = bootstrap.SealSystem;
            invasionBroken.RemoveWhere(cell => seal.IsSealBoundaryCell(cell));
            RefreshMiningWarning();
            if (heatBaseline && previousHeat > .0001f && HeatRemaining <= .0001f)
                Fired?.Invoke("invasion_heat_cleared");
            previousHeat = HeatRemaining; heatBaseline = true;
            StorageChecked = StorageMet = 0;
            if (Core.HasValue && runtime?.StorageTemperature != null)
                foreach (var record in environment.ExportPlacedObjects())
                {
                    if (record.definitionId != "jangdok" || !seal.IsInCoreWindow(Core.Value,
                            bootstrap.TileService.WorldToCell(record.position)) ||
                        !runtime.StorageTemperature.TryGetCondition(record.objectId, out var condition) ||
                        !condition.HasRequirement) continue;
                    StorageChecked++;
                    if (condition.Met) StorageMet++;
                }
            var cycleDay = runtime?.Invasion?.LastFinishedInvasionDay ?? 0;
            var recoveryActive = Core.HasValue && cycleDay > LastCompletedRecoveryDay &&
                                 !bootstrap.TimeService.IsNight;
            if (recoveryActive && HeatRemaining <= .0001f && invasionBroken.Count == 0 && StorageReady)
            {
                LastCompletedRecoveryDay = cycleDay;
                recoveryActive = false;
            }
            ShowRecoveryChecklist = recoveryActive;
        }

        public void CaptureMaintenance(SaveGame save)
        {
            if (bootstrap.SealSystem != null)
                invasionBroken.RemoveWhere(cell => bootstrap.SealSystem.IsSealBoundaryCell(cell));
            save.invasionBrokenCells = invasionBroken.OrderBy(c => c.x).ThenBy(c => c.y).ToList();
            save.invasionLastBaseAttackDay = LastBaseAttackDay;
            save.invasionLastCompletedRecoveryDay = LastCompletedRecoveryDay;
        }

        public bool RestoreMaintenance(SaveGame save)
        {
            if (save.invasionBrokenCells == null || save.invasionBrokenCells.Any(c => !bootstrap.TileService.InBounds(c)) ||
                save.invasionLastBaseAttackDay < 0 || save.invasionLastCompletedRecoveryDay < 0) return false;
            invasionBroken.Clear();
            foreach (var cell in save.invasionBrokenCells) invasionBroken.Add(cell);
            LastBaseAttackDay = save.invasionLastBaseAttackDay;
            LastCompletedRecoveryDay = save.invasionLastCompletedRecoveryDay;
            ShowRecoveryChecklist = false;
            heatBaseline = miningVisible = MiningSealBoundary = false;
            miningRevision = revision = -1;
            BindDamageTiles();
            return true;
        }

        public void Dispose()
        {
            GameEvents.OnMiningTargetChanged -= HandleMiningTarget;
            if (damageTiles != null) damageTiles.WallDestroyedByDamage -= HandleWallDestroyed;
        }

        public bool IsSealElement(Vector3Int cell, string block) =>
            bootstrap.SealSystem?.IsSealElement(cell, block) == true;

        // S2의 밀폐 재료 경고는 벽으로 오인하기 쉬운 직접 놓은 흙·돌에만 적용한다.
        // 제작대·냉각 장치·꾸미기 등의 설치 가능 색과 밀폐 판정을 혼동하지 않는다.
        public bool HasNonSealTerrainWarning(Vector3Int cell, string block) =>
            (block == "dirt" || block == "stone" || block == "stone_mid" || block == "stone_deep") &&
            !IsSealElement(cell, block);

        public void SetPlacement(string block, Vector3Int cell) { PlacementBlock = block; PlacementCell = cell; }
        public bool IsActive(string state) => state switch
        {
            "core_unsealed" => Core.HasValue && !IsSealed,
            "seal_blocked_by_open_door" => OpenDoors.Count > 0,
            "placing_non_seal_block" => HasNonSealTerrainWarning(PlacementCell, PlacementBlock),
            "mining_seal_boundary" => MiningSealBoundary,
            "base_damaged" => invasionBroken.Count > 0,
            _ => false
        };

        public void Evaluate(Vector2 origin)
        {
            if (goals.IsSuspended || bootstrap.Session?.HasWorld != true || environment == null ||
                !environment.IsInitialized || bootstrap.TileService == null || bootstrap.SealSystem == null)
                return;
            var seal = bootstrap.SealSystem;
            Core = environment.TryGetNearestPlacedObjectPosition("ice_core", origin, out var position)
                ? bootstrap.TileService.WorldToCell(position) : (Vector3Int?)null;
            RefreshMaintenance();
            var reset = previousSeal != seal || restoreVersion != goals.RestoreVersion;
            var coreChanged = previousCore != Core;
            var firstCoreInstalled = baseline && !previousCore.HasValue && Core.HasValue;
            if (!reset && !coreChanged && revision == seal.Revision) return;
            if (reset) baseline = false;
            var previousOpenDoors = OpenDoors;
            var wasSealed = IsSealed;
            IsSealed = Core.HasValue && seal.IsCoreWindowSealed(Core.Value);
            var open = new List<Vector3Int>();
            var footprint = new HashSet<Vector3Int>();
            if (Core.HasValue && !IsSealed)
            {
                foreach (var record in environment.ExportPlacedObjects())
                {
                    if (record.definitionId != "door" ||
                        !environment.TryGetBarrierActive(record.objectId, out var closed) || closed) continue;
                    var cell = bootstrap.TileService.WorldToCell(record.position);
                    open.Add(cell); footprint.Add(cell); footprint.Add(cell + Vector3Int.up);
                }
                // 열린 문을 전부 닫아도 여전히 새는 방은 '문만 닫으면 해결'로 오인하지 않는다.
                var blocking = seal.GetBlockingOpenDoorCells(Core.Value, footprint);
                open.RemoveAll(cell => !blocking.Contains(cell) && !blocking.Contains(cell + Vector3Int.up));
                open.Sort((a, b) => (a - Core.Value).sqrMagnitude.CompareTo((b - Core.Value).sqrMagnitude));
            }
            MissingCells = Core.HasValue
                ? seal.GetMissingBoundaryCells(Core.Value) : Array.Empty<Vector3Int>();
            OpenDoors = open;
            previousCore = Core; previousSeal = seal; revision = seal.Revision;
            restoreVersion = goals.RestoreVersion;
            if (baseline && Core.HasValue && !coreChanged)
            {
                var repairedCells = new HashSet<Vector3Int>();
                var repairedPositions = new HashSet<Vector3>();
                foreach (var pair in repairTargets)
                {
                    if (pair.Value.WasBoundary || !seal.IsSealBoundaryCell(pair.Key)) continue;
                    repairedCells.Add(pair.Key);
                    var center = bootstrap.TileService.GetCellWorldBounds(pair.Key).center +
                        Vector3.up * pair.Value.VisualYOffset;
                    if (repairedPositions.Add(center))
                        Repaired?.Invoke(pair.Key, pair.Value.VisualYOffset);
                }
                foreach (var cell in previousOpenDoors)
                    if (!repairedCells.Contains(cell) && !repairedCells.Contains(cell + Vector3Int.up) &&
                        !OpenDoors.Contains(cell) && seal.IsSealBoundaryCell(cell))
                        Repaired?.Invoke(cell, 0f);
            }
            CaptureRepairTargets(seal);
            if (baseline && (!coreChanged || firstCoreInstalled) && !wasSealed && IsSealed)
            { SealedNow?.Invoke(); Fired?.Invoke("core_sealed_now"); }
            baseline = true;
        }

        private void CaptureRepairTargets(SealSystem seal)
        {
            repairTargets.Clear();
            foreach (var cell in MissingCells)
            {
                var tiles = bootstrap.TileService;
                var visualCenter = tiles.GetCellWorldBounds(cell).center +
                    Vector3.up * GetLeakVisualYOffset(seal, tiles, cell);
                // Track both the diagnostic cell and the cell under its visible marker.
                // Presentation offsets must not make a repair at the shown location invisible.
                CaptureRepairTarget(seal, cell, visualCenter);
                CaptureRepairTarget(seal, tiles.WorldToCell(visualCenter), visualCenter);
            }
        }

        private void CaptureRepairTarget(SealSystem seal, Vector3Int cell, Vector3 visualCenter)
        {
            var tiles = bootstrap.TileService;
            if (!tiles.InBounds(cell)) return;
            repairTargets[cell] = new RepairTarget(seal.IsSealBoundaryCell(cell),
                visualCenter.y - tiles.GetCellWorldBounds(cell).center.y);
        }
    }
}
