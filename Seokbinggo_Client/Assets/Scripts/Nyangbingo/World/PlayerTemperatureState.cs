using System;
using System.Collections.Generic;
using Nyangbingo.Core;
using Nyangbingo.Combat;
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
        private readonly GameDataCatalog catalog;
        private readonly DayNightService timeService;
        private readonly SealSystem sealSystem;
        private readonly EquipmentSystem equipmentSystem;
        private readonly MainGameEnvironmentState environmentState;
        private readonly WorldSessionController worldSession;
        private readonly RoomTempService roomTemperature;
        private readonly HeatStageService heatStage;
        private readonly Func<bool> suppressHypothermiaFall;
        private readonly StatSheet statSheet = new StatSheet();
        private readonly float minimum;
        private readonly float maximum;
        private readonly float risePerStage;
        private readonly float fallSafe;
        private readonly float heatstrokeThreshold;
        private readonly float startingTemperature;
        private readonly float hypothermiaRoomTemp;
        private readonly float hypothermiaFallPerSecond;
        private readonly float hypothermiaDamageAtTemperature;
        private readonly float hypothermiaDamagePerSecond;
        private Transform trackedTransform;
        private Health trackedHealth;
        private bool heatstrokeRaised;
        private Func<bool> suppressDaySurfaceHeatRise;
        private float recoveryMultiplier = 1f;
        private float fractionalHypothermiaDamage;

        public PlayerTemperatureState(GameDataCatalog catalog, DayNightService clock, SealSystem seals,
            EquipmentSystem equipment, MainGameEnvironmentState environment = null,
            WorldSessionController session = null, RoomTempService roomTemp = null,
            HeatStageService stages = null, Func<bool> hypothermiaFallSuppressed = null)
        {
            this.catalog = catalog;
            timeService = clock ?? throw new ArgumentNullException(nameof(clock));
            sealSystem = seals ?? throw new ArgumentNullException(nameof(seals));
            equipmentSystem = equipment;
            environmentState = environment;
            worldSession = session;
            roomTemperature = roomTemp;
            heatStage = stages;
            suppressHypothermiaFall = hypothermiaFallSuppressed;
            minimum = Read(catalog, "temp_min", 0f);
            maximum = Mathf.Max(minimum, Read(catalog, "temp_max", 100f));
            risePerStage = Mathf.Max(0f, Read(catalog, "temp_rise_per_stage", .1f));
            fallSafe = Mathf.Max(0f, Read(catalog, "temp_fall_safe", .15f));
            heatstrokeThreshold = Mathf.Clamp(Read(catalog, "heatstroke_threshold", 80f), minimum, maximum);
            startingTemperature = Mathf.Clamp(Read(catalog, "temp_start", 40f), minimum, maximum);
            hypothermiaRoomTemp = Read(catalog, GlobalKeys.HypothermiaRoomTemp, -10f);
            hypothermiaFallPerSecond = Mathf.Max(0f,
                Read(catalog, GlobalKeys.HypothermiaFallPerSecond, .10f));
            hypothermiaDamageAtTemperature = Mathf.Clamp(
                Read(catalog, GlobalKeys.HypothermiaDamageAtTemperature, minimum), minimum, maximum);
            hypothermiaDamagePerSecond = Mathf.Max(0f,
                Read(catalog, GlobalKeys.HypothermiaDamagePerSecond, 2f));
            Current = startingTemperature;
        }

        public float Current { get; private set; }
        public float Minimum => minimum;
        public float Maximum => maximum;
        public float StartingTemperature => startingTemperature;
        public float Normalized => maximum <= minimum ? 0f : Mathf.InverseLerp(minimum, maximum, Current);
        public bool IsHeatstroke => Current > heatstrokeThreshold;
        public float RecoveryMultiplier => recoveryMultiplier;
        public event Action<float> Changed;
        public event Action ReachedMaximum;
        public event Action<int> RoomTemperatureChanged;

        public int CurrentRoomTemperature { get; private set; }
        public bool IsHypothermia => trackedTransform != null && roomTemperature != null &&
                                     CurrentRoomTemperature <= hypothermiaRoomTemp;

        public void SetTrackedTransform(Transform value) => trackedTransform = value;

        public void BindHealth(Health value)
        {
            trackedHealth = value;
            fractionalHypothermiaDamage = 0f;
        }

        public void ConfigureShadeHeatSuppressor(Func<bool> value) => suppressDaySurfaceHeatRise = value;

        public bool SetRecoveryMultiplier(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f) return false;
            recoveryMultiplier = value;
            return true;
        }

        public void Tick(float deltaGameSeconds)
        {
            if (deltaGameSeconds <= 0f || float.IsNaN(deltaGameSeconds) || float.IsInfinity(deltaGameSeconds)) return;
            var hypothermia = trackedTransform != null && roomTemperature != null;
            if (hypothermia)
            {
                var resolvedRoomTemperature = roomTemperature.Resolve(trackedTransform.position);
                if (resolvedRoomTemperature != CurrentRoomTemperature)
                {
                    CurrentRoomTemperature = resolvedRoomTemperature;
                    RoomTemperatureChanged?.Invoke(CurrentRoomTemperature);
                }
                hypothermia = CurrentRoomTemperature <= hypothermiaRoomTemp;
            }
            var safe = timeService.IsNight || IsUnderground() ||
                       trackedTransform != null && roomTemperature != null &&
                       CurrentRoomTemperature < 0;
            var insulationMultiplier = trackedTransform != null && environmentState != null
                ? environmentState.ResolveTemperatureRecoveryMultiplier(
                    trackedTransform.position, sealSystem)
                : 1f;
            var heatStageReduction = (environmentState?.HeatStageReduction ?? 0) +
                                     DayCurveCombatRules.ResolveDayHeatStageReduction(
                                         catalog, timeService.Day);
            var delta = hypothermia
                ? suppressHypothermiaFall?.Invoke() == true
                    ? 0f
                    : CalculateHypothermiaTemperatureDelta(
                        deltaGameSeconds, CurrentRoomTemperature, hypothermiaRoomTemp,
                        hypothermiaFallPerSecond)
                : safe
                    ? -fallSafe * recoveryMultiplier * insulationMultiplier * deltaGameSeconds
                    : suppressDaySurfaceHeatRise?.Invoke() == true
                        ? 0f
                        : risePerStage * CalculateEffectiveHeatStage(
                            heatStage?.Current ?? 1, heatStageReduction) *
                          DayRiseMultiplier() * deltaGameSeconds;
            Set(Current + delta);
            HypothermiaDamage(deltaGameSeconds, hypothermia);
        }

        private void HypothermiaDamage(float deltaGameSeconds, bool hypothermia)
        {
            if (!hypothermia || Current > hypothermiaDamageAtTemperature || trackedHealth == null ||
                trackedHealth.IsDead || hypothermiaDamagePerSecond <= 0f)
            {
                fractionalHypothermiaDamage = 0f;
                return;
            }

            var wholeDamage = AccumulateHypothermiaDamage(
                Current, hypothermiaDamageAtTemperature, hypothermiaDamagePerSecond,
                deltaGameSeconds, ref fractionalHypothermiaDamage);
            if (wholeDamage <= 0) return;
            trackedHealth.ApplyResolvedDamage(wholeDamage, DamageTag.Ice);
        }

        public static float CalculateHypothermiaTemperatureDelta(float deltaGameSeconds,
            float roomTemperatureC, float triggerTemperatureC, float fallPerSecond)
        {
            if (deltaGameSeconds <= 0f || float.IsNaN(deltaGameSeconds) || float.IsInfinity(deltaGameSeconds) ||
                fallPerSecond <= 0f || float.IsNaN(fallPerSecond) || float.IsInfinity(fallPerSecond) ||
                roomTemperatureC > triggerTemperatureC) return 0f;
            return -fallPerSecond * deltaGameSeconds;
        }

        public static int AccumulateHypothermiaDamage(float currentTemperature,
            float damageThreshold, float damagePerSecond, float deltaGameSeconds, ref float remainder)
        {
            if (currentTemperature > damageThreshold || damagePerSecond <= 0f || deltaGameSeconds <= 0f ||
                float.IsNaN(damagePerSecond) || float.IsInfinity(damagePerSecond) ||
                float.IsNaN(deltaGameSeconds) || float.IsInfinity(deltaGameSeconds))
            {
                remainder = 0f;
                return 0;
            }
            remainder += damagePerSecond * deltaGameSeconds;
            if (float.IsInfinity(remainder) || remainder >= int.MaxValue)
            {
                remainder = 0f;
                return int.MaxValue;
            }
            var wholeDamage = Mathf.FloorToInt(remainder);
            remainder -= wholeDamage;
            return wholeDamage;
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
