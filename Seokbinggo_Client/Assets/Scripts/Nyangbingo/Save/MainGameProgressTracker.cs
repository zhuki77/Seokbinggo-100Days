using System;
using System.Collections.Generic;
using Nyangbingo.Bosses;
using Nyangbingo.Core;
using Nyangbingo.Data;

namespace Nyangbingo.Save
{
    /// <summary>
    /// MainGame 세션 동안 도감, 보스 기록, 채굴 수와 사망 수를 같은 SaveGame 인스턴스에 누적한다.
    /// 기존 검증된 이벤트 바인딩을 소유하며 통합 스냅샷에는 값만 깊은 복사한다.
    /// </summary>
    public sealed class MainGameProgressTracker : IDisposable
    {
        private readonly SaveGame state = new SaveGame();
        private readonly GameDataCatalog catalog;
        private readonly YokaiCodexBinding yokaiCodexBinding;
        private readonly BossRecordBinding bossRecordBinding;
        private readonly RunStatsBinding runStatsBinding;
        private readonly GoalBadgeProgress goalBadgeProgress;
        private bool disposed;

        public int YokaiEntryCount => state.dogam.Count;
        public int BossEntryCount => state.bossRecords.Count;
        public int MinedTiles => state.stats.minedTiles;
        public int Deaths => state.stats.deaths;
        public GoalBadgeProgress GoalBadges => goalBadgeProgress;
        public int TotalYokaiKills
        {
            get
            {
                long total = 0;
                for (var i = 0; i < state.dogam.Count; i++)
                    total = Math.Min(int.MaxValue, total + Math.Max(0, state.dogam[i].kills));
                return (int)total;
            }
        }

        public YokaiCodexPresentationModel CreateCodexPresentationModel()
        {
            if (disposed) throw new ObjectDisposedException(nameof(MainGameProgressTracker));
            return new YokaiCodexPresentationModel(catalog, state);
        }

        public MainGameProgressTracker(GameDataCatalog gameDataCatalog, ITimeSource timeSource,
            BossManager bossManager)
        {
            catalog = gameDataCatalog != null
                ? gameDataCatalog
                : throw new ArgumentNullException(nameof(gameDataCatalog));
            if (timeSource == null) throw new ArgumentNullException(nameof(timeSource));
            if (bossManager == null) throw new ArgumentNullException(nameof(bossManager));
            state.NormalizeAfterLoad();
            yokaiCodexBinding = new YokaiCodexBinding(state, catalog.FindYokai);
            bossRecordBinding = new BossRecordBinding(state, timeSource, bossManager, catalog.FindBoss);
            runStatsBinding = new RunStatsBinding(state);
            goalBadgeProgress = new GoalBadgeProgress(timeSource,
                ResolvePositiveGlobal(catalog, GlobalKeys.BadgeWallCount, 1),
                ResolvePositiveGlobal(catalog, GlobalKeys.BadgeWindowDays, 3));
            GameEvents.OnYokaiKilled += HandleProgressChanged;
            GameEvents.OnTileBroken += HandleProgressChanged;
            GameEvents.OnPlayerDied += HandleProgressChanged;
        }

        public bool CaptureTo(SaveGame target)
        {
            if (disposed || target == null ||
                !YokaiCodexSaveAdapter.Validate(state, catalog.FindYokai) ||
                !BossRecordSaveAdapter.Validate(state, catalog.FindBoss)) return false;
            state.NormalizeAfterLoad();
            target.NormalizeAfterLoad();
            target.dogam = new List<CodexRecord>(state.dogam);
            target.bossRecords = new List<BossRecord>(state.bossRecords);
            target.stats = new RunStatsRecord
            {
                minedTiles = state.stats.minedTiles,
                deaths = state.stats.deaths
            };
            target.goalBadges = goalBadgeProgress.Capture();
            return true;
        }

        public bool RestoreFrom(SaveGame source)
        {
            if (disposed || source == null) return false;
            source.NormalizeAfterLoad();
            if (!YokaiCodexSaveAdapter.Validate(source, catalog.FindYokai) ||
                !BossRecordSaveAdapter.Validate(source, catalog.FindBoss)) return false;

            state.dogam.Clear();
            state.dogam.AddRange(source.dogam);
            state.bossRecords.Clear();
            state.bossRecords.AddRange(source.bossRecords);
            state.stats.minedTiles = source.stats.minedTiles;
            state.stats.deaths = source.stats.deaths;
            return goalBadgeProgress.Restore(source.goalBadges);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            GameEvents.OnYokaiKilled -= HandleProgressChanged;
            GameEvents.OnTileBroken -= HandleProgressChanged;
            GameEvents.OnPlayerDied -= HandleProgressChanged;
            yokaiCodexBinding.Dispose();
            bossRecordBinding.Dispose();
            runStatsBinding.Dispose();
            goalBadgeProgress.Dispose();
        }

        private void HandleProgressChanged(YokaiDefinition _) => LogProgress();
        private void HandleProgressChanged(UnityEngine.Vector3Int _) => LogProgress();
        private void HandleProgressChanged() => LogProgress();

        private void LogProgress()
        {
            if (disposed) return;
            UnityEngine.Debug.Log($"[Nyangbingo] MainGameProgressTracker: progress updated " +
                                  $"(yokaiKills={TotalYokaiKills}, minedTiles={MinedTiles}, deaths={Deaths}).");
        }

        private static int ResolvePositiveGlobal(GameDataCatalog source, string key, int fallback)
        {
            var definition = source?.FindGlobal(key);
            return definition != null && definition.TryGetInt(out var value) && value > 0 ? value : fallback;
        }
    }

    public sealed class GoalBadgeProgress : IDisposable
    {
        public const string WorkbenchId = "workbench";
        public const string InsulationWallId = "insul_wall";
        public const string FurnaceId = "furnace";
        public const int DefaultLastVisibleDay = 3;

        private readonly ITimeSource timeSource;
        private readonly int requiredWallCount;
        private readonly int lastVisibleDay;
        private readonly GoalBadgeRecord state = new GoalBadgeRecord();
        private bool disposed;

        public bool WorkbenchCrafted => state.workbenchCrafted;
        public int InsulationWallsPlaced => state.insulationWallsPlaced;
        public int RequiredWallCount => requiredWallCount;
        public int LastVisibleDay => lastVisibleDay;
        public bool InsulationWallPlaced => InsulationWallsPlaced >= RequiredWallCount;
        public bool FurnaceBuilt => state.furnaceBuilt;
        public bool AllCompleted => WorkbenchCrafted && InsulationWallPlaced && FurnaceBuilt;
        public bool IsVisible => !state.dismissed && timeSource.Day <= lastVisibleDay && !AllCompleted;
        public event Action Changed;

        public GoalBadgeProgress(ITimeSource source, int wallCount = 1,
            int visibleThroughDay = DefaultLastVisibleDay)
        {
            timeSource = source ?? throw new ArgumentNullException(nameof(source));
            requiredWallCount = Math.Max(1, wallCount);
            lastVisibleDay = Math.Max(1, visibleThroughDay);
            GameEvents.OnRecipeCrafted += HandleRecipeCrafted;
            GameEvents.OnPlacedObjectBuilt += HandlePlacedObjectBuilt;
            timeSource.Dawn += HandleDawn;
            DismissIfExpired();
        }

        public GoalBadgeRecord Capture() => new GoalBadgeRecord
        {
            workbenchCrafted = state.workbenchCrafted,
            insulationWallsPlaced = state.insulationWallsPlaced,
            furnaceBuilt = state.furnaceBuilt,
            dismissed = state.dismissed || AllCompleted || timeSource.Day > LastVisibleDay
        };

        public bool Restore(GoalBadgeRecord record)
        {
            if (disposed || record == null) return false;
            state.workbenchCrafted = record.workbenchCrafted;
            state.insulationWallsPlaced = Math.Max(0, record.insulationWallsPlaced);
            state.furnaceBuilt = record.furnaceBuilt;
            state.dismissed = record.dismissed;
            DismissIfExpired();
            Changed?.Invoke();
            return true;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            GameEvents.OnRecipeCrafted -= HandleRecipeCrafted;
            GameEvents.OnPlacedObjectBuilt -= HandlePlacedObjectBuilt;
            timeSource.Dawn -= HandleDawn;
        }

        private void HandleRecipeCrafted(RecipeDefinition recipe)
        {
            if (disposed || recipe == null) return;
            var outputId = recipe.Output.item != null ? recipe.Output.item.Id : string.Empty;
            if (recipe.Id == WorkbenchId || outputId == WorkbenchId)
                Complete(ref state.workbenchCrafted);
        }

        private void HandlePlacedObjectBuilt(string definitionId)
        {
            if (disposed) return;
            if (definitionId == InsulationWallId)
            {
                if (state.dismissed || InsulationWallPlaced) return;
                state.insulationWallsPlaced = Math.Min(requiredWallCount, state.insulationWallsPlaced + 1);
                if (InsulationWallPlaced) GameEvents.RaiseGoalBadgeCompleted();
                if (AllCompleted) state.dismissed = true;
                Changed?.Invoke();
            }
            else if (definitionId == FurnaceId) Complete(ref state.furnaceBuilt);
        }

        private void Complete(ref bool badge)
        {
            if (badge || state.dismissed) return;
            badge = true;
            if (AllCompleted) state.dismissed = true;
            GameEvents.RaiseGoalBadgeCompleted();
            Changed?.Invoke();
        }

        private void HandleDawn()
        {
            if (DismissIfExpired()) Changed?.Invoke();
        }

        private bool DismissIfExpired()
        {
            if (state.dismissed || timeSource.Day <= lastVisibleDay && !AllCompleted) return false;
            state.dismissed = true;
            return true;
        }
    }
}
