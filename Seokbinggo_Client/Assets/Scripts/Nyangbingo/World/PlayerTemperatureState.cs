using System;
using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using UnityEngine;

namespace Nyangbingo.World
{
    public sealed class PlayerTemperatureState : IGameSecondsTickable
    {
        private readonly DayNightService timeService;
        private readonly SealSystem sealSystem;
        private readonly EquipmentSystem equipmentSystem;
        private readonly MainGameEnvironmentState environmentState;
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
            EquipmentSystem equipment, MainGameEnvironmentState environment = null)
        {
            timeService = clock ?? throw new ArgumentNullException(nameof(clock));
            sealSystem = seals ?? throw new ArgumentNullException(nameof(seals));
            equipmentSystem = equipment;
            environmentState = environment;
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
            var safe = timeService.IsNight || trackedTransform != null && EffectiveCoolingPercent > 0f &&
                sealSystem.IsInsideSealedArea(trackedTransform.position);
            var delta = safe
                ? -fallSafe * recoveryMultiplier * deltaGameSeconds
                : risePerStage * Mathf.Max(1, timeService.CurrentDayCurve?.HeatStage ?? 1) *
                  DayRiseMultiplier() * deltaGameSeconds;
            Set(Current + delta);
        }

        public bool Restore(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < minimum || value > maximum) return false;
            Set(value);
            return true;
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
