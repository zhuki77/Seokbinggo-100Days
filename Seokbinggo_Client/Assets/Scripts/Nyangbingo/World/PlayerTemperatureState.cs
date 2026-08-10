using System;
using System.Collections.Generic;
using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using UnityEngine;

namespace Nyangbingo.World
{
    public static class WorldExposureRules
    {
        public static bool TryIsSurfaceExposed(Vector2 position,
            IReadOnlyList<int> surfaceHeights, out bool exposed)
        {
            exposed = false;
            if (surfaceHeights == null || surfaceHeights.Count == 0 ||
                float.IsNaN(position.x) || float.IsInfinity(position.x) ||
                float.IsNaN(position.y) || float.IsInfinity(position.y))
                return false;
            var column = Mathf.FloorToInt(position.x);
            if (column < 0 || column >= surfaceHeights.Count) return false;
            exposed = position.y > surfaceHeights[column] + 1f;
            return true;
        }
    }

    public sealed class PlayerTemperatureState : IGameSecondsTickable
    {
        private readonly DayNightService timeService;
        private readonly SealSystem sealSystem;
        private readonly EquipmentSystem equipmentSystem;
        private readonly MainGameEnvironmentState environmentState;
        private readonly WorldSessionController worldSession;
        private readonly StatSheet statSheet = new StatSheet();
        private readonly float minimum;
        private readonly float maximum;
        private readonly float risePerStage;
        private readonly float fallSafe;
        private readonly float heatstrokeThreshold;
        private readonly float startingTemperature;
        private Transform trackedTransform;
        private bool heatstrokeRaised;
        private float recoveryMultiplier = 1f;

        public PlayerTemperatureState(GameDataCatalog catalog, DayNightService clock, SealSystem seals,
            EquipmentSystem equipment, MainGameEnvironmentState environment = null,
            WorldSessionController session = null)
        {
            timeService = clock ?? throw new ArgumentNullException(nameof(clock));
            sealSystem = seals ?? throw new ArgumentNullException(nameof(seals));
            equipmentSystem = equipment;
            environmentState = environment;
            worldSession = session;
            minimum = Read(catalog, "temp_min", 0f);
            maximum = Mathf.Max(minimum, Read(catalog, "temp_max", 100f));
            risePerStage = Mathf.Max(0f, Read(catalog, "temp_rise_per_stage", .1f));
            fallSafe = Mathf.Max(0f, Read(catalog, "temp_fall_safe", .15f));
            heatstrokeThreshold = Mathf.Clamp(Read(catalog, "heatstroke_threshold", 80f), minimum, maximum);
            startingTemperature = Mathf.Clamp(Read(catalog, "temp_start", 40f), minimum, maximum);
            Current = startingTemperature;
        }

        public float Current { get; private set; }
        public float Minimum => minimum;
        public float Maximum => maximum;
        public float StartingTemperature => startingTemperature;
        public float Normalized => maximum <= minimum ? 0f : Mathf.InverseLerp(minimum, maximum, Current);
        public bool IsHeatstroke => Current > heatstrokeThreshold;
        public float RecoveryMultiplier => recoveryMultiplier;
        public float EffectiveCoolingPercent => Mathf.Min(
            sealSystem.TemperaturePercent,
            environmentState != null ? environmentState.CoolingCapPercent : 100f);
        public event Action<float> Changed;
        public event Action ReachedMaximum;

        public void SetTrackedTransform(Transform value) => trackedTransform = value;

        public bool SetRecoveryMultiplier(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f) return false;
            recoveryMultiplier = value;
            return true;
        }

        public void Tick(float deltaGameSeconds)
        {
            if (deltaGameSeconds <= 0f || float.IsNaN(deltaGameSeconds) || float.IsInfinity(deltaGameSeconds)) return;
            var safe = timeService.IsNight || IsUnderground() ||
                       trackedTransform != null && EffectiveCoolingPercent > 0f &&
                       sealSystem.IsInsideSealedArea(trackedTransform.position);
            var insulationMultiplier = trackedTransform != null && environmentState != null
                ? environmentState.ResolveTemperatureRecoveryMultiplier(
                    trackedTransform.position, sealSystem)
                : 1f;
            var delta = safe
                ? -fallSafe * recoveryMultiplier * insulationMultiplier * deltaGameSeconds
                : risePerStage * CalculateEffectiveHeatStage(
                    timeService.CurrentDayCurve?.HeatStage ?? 1,
                    environmentState?.HeatStageReduction ?? 0) *
                  DayRiseMultiplier() * deltaGameSeconds;
            Set(Current + delta);
        }

        public static int CalculateEffectiveHeatStage(int heatStage, int reduction)
        {
            return Mathf.Max(1, Mathf.Max(1, heatStage) - Mathf.Max(0, reduction));
        }

        private bool IsUnderground()
        {
            if (trackedTransform == null || worldSession?.HasWorld != true) return false;
            return WorldExposureRules.TryIsSurfaceExposed(
                       trackedTransform.position,
                       worldSession.LastResult.surfaceHeights,
                       out var exposed) &&
                   !exposed;
        }

        public bool Restore(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < minimum || value > maximum) return false;
            Set(value);
            return true;
        }

        public bool TryCoolImmediately(float amount, out float reducedTemperature)
        {
            reducedTemperature = 0f;
            if (amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount) ||
                Current <= minimum)
                return false;
            var previous = Current;
            Set(CalculateCooledTemperature(Current, minimum, amount));
            reducedTemperature = previous - Current;
            return reducedTemperature > 0f;
        }

        public static float CalculateCooledTemperature(float current, float minimum, float amount)
        {
            if (float.IsNaN(current) || float.IsInfinity(current) ||
                float.IsNaN(minimum) || float.IsInfinity(minimum) ||
                float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0f)
                return current;
            return Mathf.Max(minimum, current - amount);
        }

        private float DayRiseMultiplier()
        {
            statSheet.Recalculate(equipmentSystem);
            return Mathf.Clamp(1f + statSheet.TemperatureRiseModifier, .65f, 1f);
        }

        private void Set(float value)
        {
            var next = Mathf.Clamp(value, minimum, maximum);
            if (Mathf.Approximately(Current, next)) return;
            var previous = Current;
            Current = next;
            Changed?.Invoke(Current);
            if (previous < maximum && Mathf.Approximately(Current, maximum))
                ReachedMaximum?.Invoke();
            if (IsHeatstroke && !heatstrokeRaised)
            {
                heatstrokeRaised = true;
                GameEvents.RaisePlayerHeatPanting();
            }
            else if (!IsHeatstroke) heatstrokeRaised = false;
        }

        private static float Read(GameDataCatalog catalog, string key, float fallback)
        {
            var definition = catalog?.FindGlobal(key);
            return definition != null && definition.TryGetFloat(out var value) ? value : fallback;
        }
    }
}
