using System;
using Nyangbingo.Core;
using Nyangbingo.Data;
using UnityEngine;

namespace Nyangbingo.World
{
    public enum NapWakeReason { Manual, PlayerDamaged, WallDamaged, YokaiApproached, NightStarted, PlayerDied, Disposed }

    /// <summary>
    /// 냥잠의 시간 배속과 체온 회복 배수를 소유한다. Unity Time.timeScale은 사용하지 않으며,
    /// 실제 보금자리·밀폐·입력 판정은 MainGamePlayerController가 공급한다.
    /// </summary>
    public sealed class NapService : IDisposable
    {
        public const float MinimumDaySecondsToStart = 60f;

        private readonly DayNightService clock;
        private readonly Action<float> setTemperatureRecoveryMultiplier;
        private readonly float napTimeScale;
        private readonly float napTemperatureMultiplier;
        private readonly float jukbuinMultiplier;
        private float previousTimeScale = 1f;
        private bool disposed;

        public NapService(GameDataCatalog catalog, DayNightService timeService,
            Action<float> recoveryMultiplierSetter)
        {
            clock = timeService ?? throw new ArgumentNullException(nameof(timeService));
            setTemperatureRecoveryMultiplier = recoveryMultiplierSetter ??
                                               throw new ArgumentNullException(nameof(recoveryMultiplierSetter));
            napTimeScale = Positive(Read(catalog, "nap_timescale", 4f), 4f);
            napTemperatureMultiplier = Positive(Read(catalog, "nap_temp_mult", 3f), 3f);
            jukbuinMultiplier = Positive(Read(catalog, "jukbuin_nap_mult", 1.5f), 1.5f);
            GameEvents.OnNightStart += HandleNightStarted;
        }

        public bool IsNapping { get; private set; }
        public float ActiveTemperatureMultiplier { get; private set; } = 1f;
        public NapWakeReason LastWakeReason { get; private set; }
        public event Action<bool> StateChanged;

        public bool CanStart(bool isOnNestBed, bool isInsideSealedArea) =>
            !disposed && !IsNapping && isOnNestBed && isInsideSealedArea && !clock.IsNight &&
            clock.TimeScale > 0f && clock.SecondsUntilNextTransition >= MinimumDaySecondsToStart;

        public bool TryStart(bool isOnNestBed, bool isInsideSealedArea, bool hasNearbyJukbuin = false)
        {
            if (!CanStart(isOnNestBed, isInsideSealedArea)) return false;
            previousTimeScale = clock.TimeScale;
            ActiveTemperatureMultiplier = napTemperatureMultiplier *
                                          (hasNearbyJukbuin ? jukbuinMultiplier : 1f);
            if (!IsFinite(ActiveTemperatureMultiplier) || ActiveTemperatureMultiplier <= 0f)
            {
                ActiveTemperatureMultiplier = 1f;
                return false;
            }

            IsNapping = true;
            clock.TimeScale = napTimeScale;
            setTemperatureRecoveryMultiplier(ActiveTemperatureMultiplier);
            GameEvents.RaiseNapStarted();
            StateChanged?.Invoke(true);
            return true;
        }

        public bool Wake(NapWakeReason reason)
        {
            if (!IsNapping) return false;
            IsNapping = false;
            LastWakeReason = reason;
            ActiveTemperatureMultiplier = 1f;
            setTemperatureRecoveryMultiplier(1f);
            clock.TimeScale = previousTimeScale > 0f && IsFinite(previousTimeScale) ? previousTimeScale : 1f;
            StateChanged?.Invoke(false);
            return true;
        }

        private void HandleNightStarted() => Wake(NapWakeReason.NightStarted);

        public void Dispose()
        {
            if (disposed) return;
            Wake(NapWakeReason.Disposed);
            GameEvents.OnNightStart -= HandleNightStarted;
            disposed = true;
        }

        private static float Read(GameDataCatalog catalog, string key, float fallback)
        {
            var definition = catalog?.FindGlobal(key);
            return definition != null && definition.TryGetFloat(out var value) ? value : fallback;
        }

        private static float Positive(float value, float fallback) => IsFinite(value) && value > 0f ? value : fallback;
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
