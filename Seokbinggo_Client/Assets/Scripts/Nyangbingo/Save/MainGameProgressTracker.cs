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
        private bool disposed;

        public int YokaiEntryCount => state.dogam.Count;
        public int BossEntryCount => state.bossRecords.Count;
        public int MinedTiles => state.stats.minedTiles;
        public int Deaths => state.stats.deaths;
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
            return true;
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
    }
}
