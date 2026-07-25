using System;
using System.Collections.Generic;
using Nyangbingo.Combat;
using Nyangbingo.Core;
using Nyangbingo.Inventory;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>
    /// v31.1 HP recovery contract. All timing uses central game seconds so the
    /// development speed toggle affects recovery consistently with the rest of the simulation.
    /// </summary>
    public sealed class PlayerHealthRecoveryService : IGameSecondsTickable, IDisposable
    {
        public const string CatnipItemId = "catnip";

        private readonly Inventory.Inventory inventory;
        private readonly Health health;
        private readonly float regenDelaySeconds;
        private readonly float regenPerSecond;
        private readonly int catnipHealAmount;
        private float secondsSinceDamage;
        private float fractionalHealing;
        private bool disposed;

        public PlayerHealthRecoveryService(Inventory.Inventory playerInventory, Health playerHealth,
            float delaySeconds, float ratePerSecond, int catnipHeal)
        {
            inventory = playerInventory ?? throw new ArgumentNullException(nameof(playerInventory));
            health = playerHealth ?? throw new ArgumentNullException(nameof(playerHealth));
            if (!IsFinitePositive(delaySeconds)) throw new ArgumentOutOfRangeException(nameof(delaySeconds));
            if (!IsFinitePositive(ratePerSecond)) throw new ArgumentOutOfRangeException(nameof(ratePerSecond));
            if (catnipHeal <= 0) throw new ArgumentOutOfRangeException(nameof(catnipHeal));

            regenDelaySeconds = delaySeconds;
            regenPerSecond = ratePerSecond;
            catnipHealAmount = catnipHeal;
            health.Damaged += HandleDamaged;
        }

        public Health Health => health;
        public float RegenDelaySeconds => regenDelaySeconds;
        public float RegenPerSecond => regenPerSecond;
        public int CatnipHealAmount => catnipHealAmount;
        public float SecondsSinceDamage => secondsSinceDamage;
        public bool CanUseCatnip => !disposed && !health.IsDead && health.Current < health.MaxHealth &&
                                    inventory.Has(CatnipItemId, 1);

        public void Tick(float deltaGameSeconds)
        {
            if (disposed || health.IsDead || deltaGameSeconds <= 0f ||
                float.IsNaN(deltaGameSeconds) || float.IsInfinity(deltaGameSeconds))
                return;

            var previousElapsed = secondsSinceDamage;
            secondsSinceDamage = Mathf.Min(float.MaxValue, secondsSinceDamage + deltaGameSeconds);
            if (health.Current >= health.MaxHealth)
            {
                fractionalHealing = 0f;
                return;
            }

            var eligibleSeconds = Mathf.Max(0f, secondsSinceDamage - Mathf.Max(previousElapsed, regenDelaySeconds));
            if (eligibleSeconds <= 0f) return;
            fractionalHealing += eligibleSeconds * regenPerSecond;
            var wholeHealth = Mathf.FloorToInt(fractionalHealing);
            if (wholeHealth <= 0) return;
            var restored = health.Heal(wholeHealth);
            fractionalHealing = health.Current >= health.MaxHealth
                ? 0f
                : Mathf.Max(0f, fractionalHealing - restored);
        }

        public bool TryUseCatnip(out int restoredHealth)
        {
            restoredHealth = 0;
            if (!CanUseCatnip || !inventory.TryRemove(CatnipItemId, 1)) return false;
            restoredHealth = health.Heal(catnipHealAmount);
            if (restoredHealth > 0) return true;

            inventory.TryAdd(CatnipItemId, 1);
            return false;
        }

        public void ResetAfterRestore()
        {
            secondsSinceDamage = 0f;
            fractionalHealing = 0f;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            health.Damaged -= HandleDamaged;
        }

        private void HandleDamaged(DamageTag _, int amount)
        {
            if (amount <= 0) return;
            secondsSinceDamage = 0f;
            fractionalHealing = 0f;
        }

        private static bool IsFinitePositive(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>
    /// Applies the authoritative day-curve surface fire as continuous environmental damage.
    /// Original generated surface heights define surface exposure, so mining or placing a
    /// stray block cannot move the underground safety boundary.
    /// </summary>
    public sealed class PlayerDayHeatDamageService : IGameSecondsTickable, IDisposable
    {
        private const float PaceDeficitThreshold = 20f;
        private const float PacePenaltyMultiplier = 1.5f;

        private readonly Health health;
        private readonly Transform player;
        private readonly DayNightService timeService;
        private readonly WorldSessionController session;
        private readonly SealSystem sealSystem;
        private readonly int penaltyStartDay;
        private float fractionalDamage;
        private bool disposed;

        public PlayerDayHeatDamageService(Health playerHealth, Transform playerTransform,
            DayNightService clock, WorldSessionController worldSession, SealSystem seals,
            int firstPenaltyDay)
        {
            health = playerHealth ?? throw new ArgumentNullException(nameof(playerHealth));
            player = playerTransform ?? throw new ArgumentNullException(nameof(playerTransform));
            timeService = clock ?? throw new ArgumentNullException(nameof(clock));
            session = worldSession ?? throw new ArgumentNullException(nameof(worldSession));
            sealSystem = seals ?? throw new ArgumentNullException(nameof(seals));
            if (firstPenaltyDay <= 0) throw new ArgumentOutOfRangeException(nameof(firstPenaltyDay));
            penaltyStartDay = firstPenaltyDay;
            health.Died += ResetExposure;
        }

        public void Tick(float deltaGameSeconds)
        {
            if (disposed || deltaGameSeconds <= 0f || float.IsNaN(deltaGameSeconds) ||
                float.IsInfinity(deltaGameSeconds))
                return;
            if (health.IsDead || timeService.IsNight || !session.HasWorld ||
                !IsSurfaceExposed(player.position, session.LastResult.surfaceHeights))
            {
                fractionalDamage = 0f;
                return;
            }

            var curve = timeService.CurrentDayCurve;
            if (curve == null) return;
            var rate = CalculateDamagePerSecond(
                curve.DayFireDamagePerSecond,
                curve.PaceSealPercent,
                sealSystem.TemperaturePercent,
                timeService.Day,
                penaltyStartDay);
            if (rate <= 0f)
            {
                fractionalDamage = 0f;
                return;
            }

            var effectiveRate = rate * health.DamageTakenMultiplier * health.FireDamageMultiplier;
            if (float.IsNaN(effectiveRate) || effectiveRate <= 0f)
            {
                fractionalDamage = 0f;
                return;
            }
            fractionalDamage += effectiveRate * deltaGameSeconds;
            var wholeDamage = fractionalDamage >= int.MaxValue
                ? int.MaxValue
                : Mathf.FloorToInt(fractionalDamage);
            if (wholeDamage <= 0) return;
            fractionalDamage = Mathf.Max(0f, fractionalDamage - wholeDamage);
            health.ApplyResolvedDamage(wholeDamage, DamageTag.Fire);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            health.Died -= ResetExposure;
            fractionalDamage = 0f;
        }

        public static bool IsSurfaceExposed(Vector2 position, IReadOnlyList<int> surfaceHeights)
        {
            return WorldExposureRules.TryIsSurfaceExposed(
                       position, surfaceHeights, out var exposed) &&
                   exposed;
        }

        public static float CalculateDamagePerSecond(float baseRate, float pacePercent,
            float currentTemperaturePercent, int day, int firstPenaltyDay)
        {
            if (float.IsNaN(baseRate) || float.IsInfinity(baseRate) || baseRate <= 0f)
                return 0f;
            var penalty = day >= firstPenaltyDay &&
                          pacePercent - currentTemperaturePercent >= PaceDeficitThreshold;
            return baseRate * (penalty ? PacePenaltyMultiplier : 1f);
        }

        private void ResetExposure() => fractionalDamage = 0f;
    }
}
