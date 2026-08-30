using Nyangbingo.Combat;
using Nyangbingo.Core;
using UnityEngine;

namespace Nyangbingo.Bosses
{
    /// <summary>
    /// 강철이 완전체 — 4페이즈, 페이즈당 39초 딜 구간 후 Fan 브레스 전환.
    /// </summary>
    public sealed class BossGangcheolPerfectBehaviour : MonoBehaviour, IGameSecondsTickable
    {
        public const int PhaseCount = 4;
        public const float DamageWindowSecondsPerPhase = 39f;
        public const float TransitionImmunitySeconds = 3f;

        private Health health;
        private BossCombatController combat;
        private int currentPhase;
        private float damageWindowRemaining;
        private float transitionRemaining;

        public int CurrentPhase => currentPhase;
        public float DamageWindowRemaining => damageWindowRemaining;
        public bool IsInTransition => transitionRemaining > 0f;

        public void Configure()
        {
            health = GetComponent<Health>();
            combat = GetComponent<BossCombatController>();
            currentPhase = 0;
            damageWindowRemaining = DamageWindowSecondsPerPhase;
            transitionRemaining = 0f;
        }

        public void Tick(float deltaGameSeconds)
        {
            if (health == null || health.IsDead || deltaGameSeconds <= 0f) return;
            if (combat != null && combat.IsOpeningDodgeActive) return;

            if (transitionRemaining > 0f)
            {
                transitionRemaining = Mathf.Max(0f, transitionRemaining - deltaGameSeconds);
                health.SetDamageTakenMultiplier(0f);
                if (transitionRemaining <= 0f)
                {
                    if (currentPhase < PhaseCount - 1)
                    {
                        currentPhase++;
                        damageWindowRemaining = DamageWindowSecondsPerPhase;
                    }
                    health.SetDamageTakenMultiplier(1f);
                }
                return;
            }

            if (combat != null && (combat.IsTelegraphing || combat.IsSpecialActive))
            {
                health.SetDamageTakenMultiplier(0f);
                return;
            }

            if (currentPhase >= PhaseCount - 1)
            {
                health.SetDamageTakenMultiplier(1f);
                return;
            }

            damageWindowRemaining = Mathf.Max(0f, damageWindowRemaining - deltaGameSeconds);
            health.SetDamageTakenMultiplier(1f);
            if (damageWindowRemaining <= 0f)
                BeginPhaseTransition();
        }

        private void BeginPhaseTransition()
        {
            transitionRemaining = TransitionImmunitySeconds;
            health.SetDamageTakenMultiplier(0f);
            combat?.TryBeginForcedSpecialTelegraph();
        }
    }
}
