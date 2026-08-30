using System.Collections.Generic;
using Nyangbingo.Combat;
using Nyangbingo.Core;
using Nyangbingo.Yokai;
using UnityEngine;

namespace Nyangbingo.Bosses
{
    public enum SamdugumiCounterPhase
    {
        Lantern,
        Body,
        Knockback
    }

    /// <summary>
    /// 삼두구미 — 머리 3개가 등불·체·넉백 카운터를 순환. 각 카운터를 1회씩 깨면 이후 정상 딜.
    /// </summary>
    public sealed class BossSamdugumiBehaviour : MonoBehaviour, IGameSecondsTickable
    {
        public const int RequiredCounterBreaks = 3;
        public const int KnockbackCounterDamage = 20;

        private Health health;
        private BossCombatController combat;
        private CounterAuraSensor counterSensor;
        private SamdugumiCounterPhase phase = SamdugumiCounterPhase.Lantern;
        private int countersCleared;
        private bool allCountersCleared;
        private bool applyingKnockbackCounterDamage;

        public SamdugumiCounterPhase CurrentPhase => phase;
        public int CountersCleared => countersCleared;
        public bool AllCountersCleared => allCountersCleared;

        public void Configure(IReadOnlyList<CounterAura> auras, IYokaiCounterSource fallback)
        {
            health = GetComponent<Health>();
            combat = GetComponent<BossCombatController>();
            counterSensor = new CounterAuraSensor(transform, auras, fallback);
            if (health != null) health.Damaged += OnDamaged;
        }

        public void Tick(float deltaGameSeconds)
        {
            if (health == null || health.IsDead || allCountersCleared || combat == null ||
                combat.IsOpeningDodgeActive)
                return;
            UpdateVulnerabilityMultiplier();
        }

        public void NotifyKnockbackReceived(float knockbackTiles)
        {
            if (health == null || health.IsDead || allCountersCleared ||
                phase != SamdugumiCounterPhase.Knockback || knockbackTiles <= 0f ||
                combat != null && combat.IsOpeningDodgeActive)
                return;
            applyingKnockbackCounterDamage = true;
            try
            {
                health.ApplyResolvedDamage(KnockbackCounterDamage, DamageTag.Melee);
            }
            finally
            {
                applyingKnockbackCounterDamage = false;
            }
            AdvanceCounter();
        }

        private void UpdateVulnerabilityMultiplier()
        {
            switch (phase)
            {
                case SamdugumiCounterPhase.Lantern:
                    health.SetDamageTakenMultiplier(counterSensor.IsInLanternRange ? 1f : 0f);
                    break;
                case SamdugumiCounterPhase.Body:
                    health.SetDamageTakenMultiplier(1f);
                    break;
                case SamdugumiCounterPhase.Knockback:
                    health.SetDamageTakenMultiplier(0f);
                    break;
            }
        }

        private void OnDamaged(DamageTag tag, int amount)
        {
            if (health == null || allCountersCleared || amount <= 0 ||
                combat != null && combat.IsOpeningDodgeActive)
                return;

            if (phase == SamdugumiCounterPhase.Knockback)
            {
                if (!applyingKnockbackCounterDamage)
                    health.Heal(amount);
                return;
            }

            if (phase == SamdugumiCounterPhase.Body && tag != DamageTag.Melee)
            {
                health.Heal(amount);
                return;
            }

            if (phase == SamdugumiCounterPhase.Lantern && !counterSensor.IsInLanternRange)
            {
                health.Heal(amount);
                return;
            }

            AdvanceCounter();
        }

        private void AdvanceCounter()
        {
            countersCleared++;
            if (countersCleared >= RequiredCounterBreaks)
            {
                allCountersCleared = true;
                health.SetDamageTakenMultiplier(1f);
                return;
            }

            phase = phase switch
            {
                SamdugumiCounterPhase.Lantern => SamdugumiCounterPhase.Body,
                SamdugumiCounterPhase.Body => SamdugumiCounterPhase.Knockback,
                _ => SamdugumiCounterPhase.Lantern
            };
            UpdateVulnerabilityMultiplier();
        }

        private void OnDestroy()
        {
            if (health != null) health.Damaged -= OnDamaged;
        }
    }
}
