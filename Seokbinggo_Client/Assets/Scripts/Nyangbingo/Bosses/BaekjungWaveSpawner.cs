using System;
using Nyangbingo.Core;
using Nyangbingo.Data;
using UnityEngine;

namespace Nyangbingo.Bosses
{
    public interface IBaekjungSpawnController
    {
        int ActiveCount { get; }
        bool TrySpawn(YokaiKind kind, int waveIndex);
    }

    public sealed class BaekjungWaveSpawner : IDisposable
    {
        private readonly BaekjungScheduler scheduler;
        private readonly IBaekjungSpawnController spawnController;
        private bool disposed;

        public BaekjungWaveSpawner(BaekjungScheduler scheduler, IBaekjungSpawnController spawnController)
        {
            this.scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            this.spawnController = spawnController ?? throw new ArgumentNullException(nameof(spawnController));
            scheduler.WaveReady += HandleWaveReady;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            scheduler.WaveReady -= HandleWaveReady;
        }

        private void HandleWaveReady(DayEventDefinition definition, int waveIndex)
        {
            var composition = definition.Composition;
            for (var groupIndex = 0; groupIndex < composition.Length; groupIndex++)
            {
                var group = composition[groupIndex];
                for (var spawnIndex = 0; spawnIndex < Mathf.Max(0, group.amount); spawnIndex++)
                {
                    if (spawnController.ActiveCount >= definition.MaxActive) return;
                    spawnController.TrySpawn(group.kind, waveIndex);
                }
            }
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
