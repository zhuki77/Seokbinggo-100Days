using System;
using System.Collections.Generic;
using Nyangbingo.Core;
using Nyangbingo.Data;

namespace Nyangbingo.Bosses
{
    public interface IBaekjungSpawnController
    {
        int ActiveRaidCount { get; }
        event Action RaidSlotAvailable;
        bool TrySpawn(YokaiKind kind, int waveIndex);
    }

    public sealed class BaekjungWaveSpawner : IDisposable
    {
        private readonly struct PendingSpawn
        {
            public PendingSpawn(DayEventDefinition definition, YokaiKind kind, int waveIndex)
            {
                Definition = definition;
                Kind = kind;
                WaveIndex = waveIndex;
            }

            public DayEventDefinition Definition { get; }
            public YokaiKind Kind { get; }
            public int WaveIndex { get; }
        }

        private readonly BaekjungScheduler scheduler;
        private readonly IBaekjungSpawnController spawnController;
        private readonly Queue<PendingSpawn> pending = new Queue<PendingSpawn>();
        private bool disposed;
        private bool acceptingSpawns;

        public BaekjungWaveSpawner(BaekjungScheduler scheduler, IBaekjungSpawnController spawnController)
        {
            this.scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            this.spawnController = spawnController ?? throw new ArgumentNullException(nameof(spawnController));
            acceptingSpawns = scheduler.IsActive;
            scheduler.Started += HandleStarted;
            scheduler.WaveReady += HandleWaveReady;
            scheduler.Ended += HandleEnded;
            spawnController.RaidSlotAvailable += HandleRaidSlotAvailable;
            GameEvents.OnDawnWarning += HandleDawnWarning;
        }

        public int PendingCount => pending.Count;
        public int DiscardedAtDawnCount { get; private set; }

        public List<YokaiKind> CapturePendingKinds()
        {
            var result = new List<YokaiKind>(pending.Count);
            foreach (var request in pending)
                result.Add(request.Kind);
            return result;
        }

        public bool RestorePendingDefinitions(IReadOnlyList<YokaiDefinition> definitions)
        {
            if (disposed || definitions == null) return false;
            if (!scheduler.IsActive)
                return definitions.Count == 0;

            var activeDefinition = scheduler.ActiveDefinition;
            var restored = new Queue<PendingSpawn>();
            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index];
                if (definition == null || !ContainsKind(activeDefinition, definition.Kind)) return false;
                restored.Enqueue(new PendingSpawn(activeDefinition, definition.Kind, -1));
            }

            pending.Clear();
            while (restored.Count > 0) pending.Enqueue(restored.Dequeue());
            acceptingSpawns = true;
            TryFlushPending();
            return true;
        }

        public void RetryPending()
        {
            if (!disposed && acceptingSpawns) TryFlushPending();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            scheduler.Started -= HandleStarted;
            scheduler.WaveReady -= HandleWaveReady;
            scheduler.Ended -= HandleEnded;
            spawnController.RaidSlotAvailable -= HandleRaidSlotAvailable;
            GameEvents.OnDawnWarning -= HandleDawnWarning;
            pending.Clear();
        }

        private void HandleStarted(DayEventDefinition definition)
        {
            pending.Clear();
            DiscardedAtDawnCount = 0;
            acceptingSpawns = true;
        }

        private void HandleWaveReady(DayEventDefinition definition, int waveIndex)
        {
            if (!acceptingSpawns) return;
            var composition = definition.Composition;
            var waveCount = definition.WaveOffsets.Length;
            if (waveCount <= 0 || waveIndex < 0 || waveIndex >= waveCount) return;

            var waveComposition = BuildWaveComposition(composition, waveCount, waveIndex);
            for (var index = 0; index < waveComposition.Count; index++)
                pending.Enqueue(new PendingSpawn(definition, waveComposition[index], waveIndex));

            TryFlushPending();
        }

        public static IReadOnlyList<YokaiKind> BuildWaveComposition(
            IReadOnlyList<YokaiSpawnAmount> composition, int waveCount, int waveIndex)
        {
            if (composition == null || waveCount <= 0 || waveIndex < 0 || waveIndex >= waveCount)
                return Array.Empty<YokaiKind>();

            var fixedKinds = new List<YokaiKind>[waveCount];
            var waves = new List<YokaiKind>[waveCount];
            var ordinaryKinds = new List<YokaiKind>();
            var totalSpawnCount = 0;
            for (var index = 0; index < waveCount; index++)
            {
                fixedKinds[index] = new List<YokaiKind>();
                waves[index] = new List<YokaiKind>();
            }

            for (var groupIndex = 0; groupIndex < composition.Count; groupIndex++)
            {
                var group = composition[groupIndex];
                var amount = Math.Max(0, group.amount);
                totalSpawnCount += amount;
                var fixedWaveIndex = FixedWaveIndex(group.kind, waveCount);
                for (var spawnIndex = 0; spawnIndex < amount; spawnIndex++)
                {
                    if (fixedWaveIndex >= 0)
                        fixedKinds[fixedWaveIndex].Add(group.kind);
                    else
                        ordinaryKinds.Add(group.kind);
                }
            }

            var ordinaryIndex = 0;
            for (var currentWave = 0; currentWave < waveCount; currentWave++)
            {
                var targetSize =
                    totalSpawnCount * (currentWave + 1) / waveCount -
                    totalSpawnCount * currentWave / waveCount;
                var ordinaryCapacity = Math.Max(0, targetSize - fixedKinds[currentWave].Count);
                for (var count = 0;
                     count < ordinaryCapacity && ordinaryIndex < ordinaryKinds.Count;
                     count++)
                    waves[currentWave].Add(ordinaryKinds[ordinaryIndex++]);
                waves[currentWave].AddRange(fixedKinds[currentWave]);
            }

            // Malformed future data must never silently lose a spawn. Valid v34 data never
            // reaches this fallback because the fixed second-wave Gaekgwi fits its four slots.
            while (ordinaryIndex < ordinaryKinds.Count)
            {
                var smallestWave = 0;
                for (var currentWave = 1; currentWave < waveCount; currentWave++)
                    if (waves[currentWave].Count < waves[smallestWave].Count)
                        smallestWave = currentWave;
                waves[smallestWave].Add(ordinaryKinds[ordinaryIndex++]);
            }

            return waves[waveIndex];
        }

        private static int FixedWaveIndex(YokaiKind kind, int waveCount)
        {
            // v34: the Baekjung midboss must enter with the second wave at +150 game-seconds.
            return kind == YokaiKind.Gaekgwi && waveCount >= 2 ? 1 : -1;
        }

        private void TryFlushPending()
        {
            while (pending.Count > 0)
            {
                var request = pending.Peek();
                if (spawnController.ActiveRaidCount >= request.Definition.MaxActive ||
                    !spawnController.TrySpawn(request.Kind, request.WaveIndex))
                    return;
                pending.Dequeue();
            }
        }

        private void HandleRaidSlotAvailable()
        {
            if (!disposed && acceptingSpawns) TryFlushPending();
        }

        private void HandleDawnWarning()
        {
            if (!disposed && scheduler.IsActive) DiscardPendingAtDawn();
        }

        private void HandleEnded(DayEventDefinition definition) => DiscardPendingAtDawn();

        private void DiscardPendingAtDawn()
        {
            acceptingSpawns = false;
            DiscardedAtDawnCount += pending.Count;
            pending.Clear();
        }

        private static bool ContainsKind(DayEventDefinition definition, YokaiKind kind)
        {
            if (definition == null) return false;
            var composition = definition.Composition;
            for (var index = 0; index < composition.Length; index++)
                if (composition[index].kind == kind && composition[index].amount > 0)
                    return true;
            return false;
        }
    }

    public sealed class BaekjungRegularSpawnGate : IDisposable
    {
        private readonly BaekjungScheduler scheduler;
        private readonly IRegularSpawnController regularSpawnController;
        private bool isPaused;
        private bool disposed;

        public BaekjungRegularSpawnGate(BaekjungScheduler scheduler, IRegularSpawnController regularSpawnController)
        {
            this.scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            this.regularSpawnController = regularSpawnController ?? throw new ArgumentNullException(nameof(regularSpawnController));
            scheduler.Started += HandleStarted;
            scheduler.Ended += HandleEnded;
            if (scheduler.IsActive)
            {
                regularSpawnController.SetRegularSpawning(false);
                isPaused = true;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            scheduler.Started -= HandleStarted;
            scheduler.Ended -= HandleEnded;
            ResumeRegularSpawning();
        }

        private void HandleStarted(DayEventDefinition definition)
        {
            regularSpawnController.SetRegularSpawning(false);
            isPaused = true;
        }

        private void HandleEnded(DayEventDefinition definition) => ResumeRegularSpawning();

        private void ResumeRegularSpawning()
        {
            if (!isPaused) return;
            isPaused = false;
            regularSpawnController.SetRegularSpawning(true);
        }
    }
}
