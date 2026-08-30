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

        public static bool IsSupportedHealingItemId(string itemId) =>
            itemId == CatnipItemId ||
            itemId == StorageTemperatureService.OysterMushroomId ||
            itemId == StorageTemperatureService.ShiitakeId ||
            itemId == StorageTemperatureService.SeogiId;

        private readonly Inventory.Inventory inventory;
        private readonly Health health;
        private readonly float regenDelaySeconds;
        private readonly float regenPerSecond;
        private readonly int catnipHealAmount;
        private readonly Dictionary<string, int> itemHealing = new Dictionary<string, int>(StringComparer.Ordinal);
        private float secondsSinceDamage;
        private float fractionalHealing;
        private Func<float> regenMultiplierProvider;
        private bool disposed;

        public PlayerHealthRecoveryService(Inventory.Inventory playerInventory, Health playerHealth,
            float delaySeconds, float ratePerSecond, int catnipHeal,
            IReadOnlyDictionary<string, int> extraHealingItems = null)
        {
            inventory = playerInventory ?? throw new ArgumentNullException(nameof(playerInventory));
            health = playerHealth ?? throw new ArgumentNullException(nameof(playerHealth));
            if (!IsFinitePositive(delaySeconds)) throw new ArgumentOutOfRangeException(nameof(delaySeconds));
            if (!IsFinitePositive(ratePerSecond)) throw new ArgumentOutOfRangeException(nameof(ratePerSecond));
            if (catnipHeal <= 0) throw new ArgumentOutOfRangeException(nameof(catnipHeal));

            regenDelaySeconds = delaySeconds;
            regenPerSecond = ratePerSecond;
            catnipHealAmount = catnipHeal;
            itemHealing.Add(CatnipItemId, catnipHeal);
            if (extraHealingItems != null)
                foreach (var pair in extraHealingItems)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value <= 0 || itemHealing.ContainsKey(pair.Key))
                        throw new ArgumentException("회복 아이템 표가 올바르지 않습니다.", nameof(extraHealingItems));
                    itemHealing.Add(pair.Key, pair.Value);
                }
            health.Damaged += HandleDamaged;
        }

        public Health Health => health;
        public float RegenDelaySeconds => regenDelaySeconds;
        public float RegenPerSecond => regenPerSecond;
        public int CatnipHealAmount => catnipHealAmount;
        public float SecondsSinceDamage => secondsSinceDamage;
        public bool CanUseCatnip => !disposed && !health.IsDead && health.Current < health.MaxHealth &&
                                    inventory.Has(CatnipItemId, 1);
        public bool CanUseHealingItem(string itemId) =>
            !disposed && !health.IsDead && health.Current < health.MaxHealth &&
            !string.IsNullOrWhiteSpace(itemId) && itemHealing.ContainsKey(itemId) && inventory.Has(itemId, 1);
        public int BaseHealingFor(string itemId) =>
            !string.IsNullOrWhiteSpace(itemId) && itemHealing.TryGetValue(itemId, out var value) ? value : 0;

        public void SetRegenMultiplierProvider(Func<float> provider) => regenMultiplierProvider = provider;

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
            var regenMultiplier = regenMultiplierProvider != null ? regenMultiplierProvider() : 1f;
            if (float.IsNaN(regenMultiplier) || float.IsInfinity(regenMultiplier) || regenMultiplier < 0f)
                regenMultiplier = 1f;
            fractionalHealing += eligibleSeconds * regenPerSecond * regenMultiplier;
            var wholeHealth = Mathf.FloorToInt(fractionalHealing);
            if (wholeHealth <= 0) return;
            var restored = health.Heal(wholeHealth);
            fractionalHealing = health.Current >= health.MaxHealth
                ? 0f
                : Mathf.Max(0f, fractionalHealing - restored);
        }

        public bool TryUseCatnip(out int restoredHealth)
            => TryUseHealingItem(CatnipItemId, out restoredHealth);

        public bool TryUseHealingItem(string itemId, out int restoredHealth)
        {
            restoredHealth = 0;
            if (!CanUseHealingItem(itemId) ||
                !inventory.TryRemoveOneWithStorageCondition(itemId, out var condition)) return false;
            restoredHealth = health.Heal(Mathf.RoundToInt(BaseHealingFor(itemId) * Mathf.Clamp01(condition)));
            // 상해도가 0인 음식도 사라지지는 않지만 먹으면 회복 0인 소모품이다.
            return true;
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

}
