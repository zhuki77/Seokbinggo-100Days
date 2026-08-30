using System.Collections.Generic;
using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.World;
using UnityEngine;

namespace Nyangbingo.Yokai
{
    // World/installation systems implement these checks; Yokai AI never references art or scene objects directly.
    public interface IYokaiCounterSource
    {
        bool IsInLanternRange { get; }
        bool IsInSieveRange { get; }
        bool HasGroundLoot { get; }
        bool IsInventoryTheftBlocked { get; }
        float SieveStopSeconds { get; }
        float SieveCooldownSeconds { get; }
        float SieveDamageMultiplier { get; }
        float EoduksiniLanternPauseSeconds { get; }
        float EoduksiniBloomCooldownSeconds { get; }
        float EoduksiniLanternDamageMultiplier { get; }
    }

    public static class YokaiSpecialRules
    {
        public static int ContactDamage(YokaiDefinition definition, bool isInLanternRange) =>
            definition != null ? Mathf.Max(0, definition.ContactDamageFor(isInLanternRange)) : 0;

        public static float DamageTakenMultiplier(YokaiDefinition definition, IYokaiCounterSource counters)
        {
            if (definition == null) return 1f;
            if (definition.Kind == YokaiKind.Eoduksini)
            {
                var multiplier = definition.DamageTakenMultiplierFor(counters?.IsInLanternRange == true);
                return IsFinitePositive(multiplier) ? multiplier : 1f;
            }
            if (definition.Kind == YokaiKind.Yagwanggwi && counters != null && counters.IsInSieveRange)
                return IsFinitePositive(counters.SieveDamageMultiplier) ? counters.SieveDamageMultiplier : 1f;
            return 1f;
        }

        private static bool IsFinitePositive(float value)
            => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        public static bool ShouldStealGroundLoot(YokaiKind kind, IYokaiCounterSource counters)
            => kind == YokaiKind.Yagwanggwi && counters != null && counters.HasGroundLoot && !counters.IsInSieveRange;

        public static bool CanStealInventory(YokaiKind kind, IYokaiCounterSource counters)
            => kind == YokaiKind.Yagwanggwi && counters != null && !counters.IsInventoryTheftBlocked && !counters.IsInSieveRange;

        public static bool ShouldAttemptTheft(YokaiKind kind, IYokaiCounterSource counters)
            => ShouldStealGroundLoot(kind, counters) || CanStealInventory(kind, counters);

    }

    public sealed class CounterAura : MonoBehaviour
    {
        [SerializeField] private CounterAuraKind kind;
        [Min(0f)][SerializeField] private float radius;
        [Min(0f)][SerializeField] private float effectValue;
        [Min(0f)][SerializeField] private float durationSeconds;
        [Min(0f)][SerializeField] private float cooldownSeconds;
        public CounterAuraKind Kind => kind;
        public float Radius => radius;
        public float EffectValue => effectValue;
        public float DurationSeconds => durationSeconds;
        public float CooldownSeconds => cooldownSeconds;

        public void ConfigureForRuntime(CounterAuraKind auraKind, float auraRadius, float effect, float duration, float cooldown)
        {
            kind = auraKind;
            radius = SanitizeNonNegative(auraRadius);
            effectValue = SanitizeNonNegative(effect);
            durationSeconds = SanitizeNonNegative(duration);
            cooldownSeconds = SanitizeNonNegative(cooldown);
        }

        public bool Contains(Vector2 worldPosition)
            => !float.IsNaN(worldPosition.x) && !float.IsInfinity(worldPosition.x) &&
               !float.IsNaN(worldPosition.y) && !float.IsInfinity(worldPosition.y) &&
               ((Vector2)transform.position - worldPosition).sqrMagnitude <= radius * radius;

        private static float SanitizeNonNegative(float value)
            => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Max(0f, value);
    }

    public sealed class CounterAuraSensor : IYokaiCounterSource
    {
        private readonly Transform observedTransform;
        private readonly IReadOnlyList<CounterAura> auras;
        private readonly IYokaiCounterSource fallback;
        private readonly DayNightService dayNightService;

        public CounterAuraSensor(Transform observed, IReadOnlyList<CounterAura> auraList,
            IYokaiCounterSource fallbackSource = null, DayNightService dayNight = null)
        {
            observedTransform = observed;
            auras = auraList;
            fallback = fallbackSource;
            dayNightService = dayNight;
        }

        public bool IsInLanternRange => Find(CounterAuraKind.Lantern) != null || (fallback?.IsInLanternRange ?? false);
        public bool IsInSieveRange => Find(CounterAuraKind.Sieve) != null || (fallback?.IsInSieveRange ?? false);
        public bool IsInHaetaeRange => Find(CounterAuraKind.Haetae) != null;
        public bool IsInBellRopeRange =>
            Find(CounterAuraKind.BellRope) != null || Find(CounterAuraKind.FrostBellRope) != null;
        public float FireDamageMultiplier
        {
            get
            {
                var multiplier = Value(CounterAuraKind.Haetae, aura => aura.EffectValue, 1f);
                if (dayNightService == null || !dayNightService.IsNight)
                    multiplier *= Value(CounterAuraKind.Daebal, aura => aura.EffectValue, 1f);
                return multiplier;
            }
        }
        public bool HasGroundLoot => fallback?.HasGroundLoot ?? false;
        public bool IsInventoryTheftBlocked => fallback?.IsInventoryTheftBlocked ?? false;
        public float SieveStopSeconds => Value(CounterAuraKind.Sieve, aura => aura.DurationSeconds, fallback?.SieveStopSeconds ?? 0f);
        public float SieveCooldownSeconds => Value(CounterAuraKind.Sieve, aura => aura.CooldownSeconds, fallback?.SieveCooldownSeconds ?? 0f);
        public float SieveDamageMultiplier => Value(CounterAuraKind.Sieve, aura => aura.EffectValue, fallback?.SieveDamageMultiplier ?? 0f);
        public float EoduksiniLanternPauseSeconds => Value(CounterAuraKind.Lantern, aura => aura.DurationSeconds, fallback?.EoduksiniLanternPauseSeconds ?? 0f);
        public float EoduksiniBloomCooldownSeconds => Value(CounterAuraKind.Lantern, aura => aura.CooldownSeconds, fallback?.EoduksiniBloomCooldownSeconds ?? 0f);
        public float EoduksiniLanternDamageMultiplier => Value(CounterAuraKind.Lantern, aura => aura.EffectValue, fallback?.EoduksiniLanternDamageMultiplier ?? 0f);

        private CounterAura Find(CounterAuraKind kind)
        {
            if (observedTransform == null || auras == null) return null;
            foreach (var aura in auras)
                if (aura != null && aura.isActiveAndEnabled && aura.Kind == kind &&
                    aura.Contains(observedTransform.position)) return aura;
            return null;
        }

        private float Value(CounterAuraKind kind, System.Func<CounterAura, float> selector, float fallbackValue)
        {
            var aura = Find(kind);
            return aura == null ? fallbackValue : selector(aura);
        }

        public void TickFrostBellRope(YokaiBrain brain, ref bool wasInRange, ref float reapplyCooldown,
            float deltaSeconds)
        {
            if (brain == null) return;
            if (reapplyCooldown > 0f)
                reapplyCooldown = Mathf.Max(0f, reapplyCooldown - Mathf.Max(0f, deltaSeconds));
            var aura = Find(CounterAuraKind.FrostBellRope);
            var inRange = aura != null;
            if (inRange && !wasInRange && reapplyCooldown <= 0f &&
                brain.ApplyFrostSlow(aura.EffectValue, aura.DurationSeconds))
                reapplyCooldown = aura.CooldownSeconds;
            wasInRange = inRange;
        }
    }

    public sealed class CounterAuraEffects
    {
        private readonly CounterAuraSensor sensor;
        private readonly Nyangbingo.Combat.Health health;
        private bool wasInBellRopeRange;
        public event System.Action AlarmRaised;

        public CounterAuraEffects(CounterAuraSensor auraSensor, Nyangbingo.Combat.Health protectedHealth)
        {
            sensor = auraSensor;
            health = protectedHealth;
        }

        public void Refresh()
        {
            if (sensor == null) return;
            if (health != null) health.SetFireDamageMultiplier(sensor.FireDamageMultiplier);
            var isInBellRopeRange = sensor.IsInBellRopeRange;
            if (isInBellRopeRange && !wasInBellRopeRange) AlarmRaised?.Invoke();
            wasInBellRopeRange = isInBellRopeRange;
        }
    }
}
