using System;
using System.Collections.Generic;
using Nyangbingo.Core;
using Nyangbingo.Data;

namespace Nyangbingo.Bosses
{
    [Serializable]
    public sealed class BaekjungSchedulerState
    {
        public List<string> startedEventIds = new List<string>();
        public string activeEventId;
        public float elapsedGameSeconds;
        public int nextWaveIndex;
        public bool hasEnded;
    }

    public sealed class BaekjungScheduler
    {
        private readonly IReadOnlyList<DayEventDefinition> definitions;
        private readonly HashSet<string> startedEventIds = new HashSet<string>(StringComparer.Ordinal);
        private DayEventDefinition activeDefinition;
        private float elapsedSeconds;
        private int nextWaveIndex;
        private bool hasEnded;

        public BaekjungScheduler(IReadOnlyList<DayEventDefinition> definitions)
        {
            this.definitions = definitions ?? Array.Empty<DayEventDefinition>();
        }

        public event Action<DayEventDefinition, int> WaveReady;
        public event Action<DayEventDefinition> Started;
        public event Action<DayEventDefinition> Ended;

        public DayEventDefinition ActiveDefinition => activeDefinition;
        public float ElapsedSeconds => elapsedSeconds;
        public int DispatchedWaveCount => nextWaveIndex;
        public bool HasStarted => startedEventIds.Count > 0;
        public bool HasEnded => hasEnded;
        public bool IsActive => activeDefinition != null && !hasEnded;
        public bool IsScheduleComplete => HasStarted &&
            (activeDefinition == null || nextWaveIndex >= activeDefinition.WaveOffsets.Length);

        public bool TryStartNight(int day)
        {
            if (IsActive) return false;

            DayEventDefinition matchingDefinition = null;
            for (var i = 0; i < definitions.Count; i++)
            {
                var candidate = definitions[i];
                if (candidate != null && candidate.Day == day)
                {
                    matchingDefinition = candidate;
                    break;
                }
            }

            if (matchingDefinition == null) return false;
            var eventId = EventIdFor(matchingDefinition);
            if (!startedEventIds.Add(eventId)) return false;

            activeDefinition = matchingDefinition;
            elapsedSeconds = 0f;
            nextWaveIndex = 0;
            hasEnded = false;
            Started?.Invoke(activeDefinition);
            GameEvents.RaiseBaekjungStart();
            DispatchReadyWaves();
            return true;
        }

        public BaekjungSchedulerState CaptureState()
        {
            var state = new BaekjungSchedulerState
            {
                activeEventId = activeDefinition != null ? EventIdFor(activeDefinition) : string.Empty,
                elapsedGameSeconds = elapsedSeconds,
                nextWaveIndex = nextWaveIndex,
                hasEnded = hasEnded
            };
            state.startedEventIds.AddRange(startedEventIds);
            state.startedEventIds.Sort(StringComparer.Ordinal);
            return state;
        }

        public bool RestoreState(BaekjungSchedulerState state)
        {
            if (state == null || float.IsNaN(state.elapsedGameSeconds) || float.IsInfinity(state.elapsedGameSeconds))
                return false;

            DayEventDefinition restoredActive = null;
            if (!string.IsNullOrWhiteSpace(state.activeEventId))
            {
                for (var i = 0; i < definitions.Count; i++)
                {
                    var candidate = definitions[i];
                    if (candidate != null && EventIdFor(candidate) == state.activeEventId)
                    {
                        restoredActive = candidate;
                        break;
                    }
                }
                if (restoredActive == null) return false;
            }

            startedEventIds.Clear();
            if (state.startedEventIds != null)
                for (var i = 0; i < state.startedEventIds.Count; i++)
                    if (!string.IsNullOrWhiteSpace(state.startedEventIds[i]))
                        startedEventIds.Add(state.startedEventIds[i]);
            if (restoredActive != null) startedEventIds.Add(EventIdFor(restoredActive));

            activeDefinition = restoredActive;
            elapsedSeconds = Math.Max(0f, state.elapsedGameSeconds);
            nextWaveIndex = restoredActive == null
                ? 0
                : Math.Max(0, Math.Min(state.nextWaveIndex, restoredActive.WaveOffsets.Length));
            hasEnded = state.hasEnded;
            return true;
        }

        public void Tick(float gameSeconds)
        {
            if (!IsActive || IsScheduleComplete || gameSeconds <= 0f ||
                float.IsNaN(gameSeconds) || float.IsInfinity(gameSeconds)) return;
            elapsedSeconds += gameSeconds;
            DispatchReadyWaves();
        }

        public bool TryEndAtDawn()
        {
            if (!IsActive) return false;
            hasEnded = true;
            Ended?.Invoke(activeDefinition);
            return true;
        }

        private void DispatchReadyWaves()
        {
            var offsets = activeDefinition.WaveOffsets;
            while (nextWaveIndex < offsets.Length && elapsedSeconds >= offsets[nextWaveIndex])
            {
                var waveIndex = nextWaveIndex++;
                WaveReady?.Invoke(activeDefinition, waveIndex);
            }
        }

        private static string EventIdFor(DayEventDefinition definition) => string.IsNullOrWhiteSpace(definition.Id)
            ? definition.Day.ToString()
            : definition.Id;
    }

    public sealed class BaekjungTimeBinding : IDisposable
    {
        private readonly ITimeSource timeSource;
        private readonly BaekjungScheduler scheduler;
        private bool disposed;

        public BaekjungTimeBinding(ITimeSource timeSource, BaekjungScheduler scheduler)
        {
            this.timeSource = timeSource ?? throw new ArgumentNullException(nameof(timeSource));
            this.scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            GameEvents.OnNightStart += HandleNightStart;
            timeSource.Dawn += HandleDawn;
            HandleNightStart();
        }

        public void Tick(float gameSeconds)
        {
            if (!disposed) scheduler.Tick(gameSeconds);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            GameEvents.OnNightStart -= HandleNightStart;
            timeSource.Dawn -= HandleDawn;
        }

        private void HandleNightStart()
        {
            if (!disposed && timeSource.IsNight) scheduler.TryStartNight(timeSource.Day);
        }

        private void HandleDawn()
        {
            if (!disposed) scheduler.TryEndAtDawn();
        }
    }
}
