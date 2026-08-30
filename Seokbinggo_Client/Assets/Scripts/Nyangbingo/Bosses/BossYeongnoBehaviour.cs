using Nyangbingo.Combat;
using Nyangbingo.Core;
using Nyangbingo.World;
using UnityEngine;

namespace Nyangbingo.Bosses
{
    /// <summary>
    /// 영노 — 삼킴 중 무적, 내부 약점 3타 탈출 시 버스트 딜.
    /// </summary>
    public sealed class BossYeongnoBehaviour : MonoBehaviour, IGameSecondsTickable
    {
        public const int WeakPointBurstDamage = 78;

        private Health health;
        private BossCombatController combat;
        private MainGamePlayerController player;

        public void Configure(Transform playerTransform)
        {
            health = GetComponent<Health>();
            combat = GetComponent<BossCombatController>();
            player = playerTransform != null
                ? playerTransform.GetComponent<MainGamePlayerController>()
                : null;
            if (player != null)
                player.YeongnoSwallowEnded += OnYeongnoSwallowEnded;
        }

        public void Tick(float deltaGameSeconds)
        {
            if (health == null || health.IsDead) return;
            if (combat != null && combat.IsOpeningDodgeActive) return;

            if (player != null && player.IsSwallowedByYeongno)
                health.SetDamageTakenMultiplier(0f);
            else
                health.SetDamageTakenMultiplier(1f);
        }

        private void OnYeongnoSwallowEnded(bool escapedByBreakingWeakPoint)
        {
            if (health == null || health.IsDead || !escapedByBreakingWeakPoint ||
                combat != null && combat.IsOpeningDodgeActive)
                return;
            health.ApplyResolvedDamage(WeakPointBurstDamage, DamageTag.Melee);
        }

        private void OnDestroy()
        {
            if (player != null)
                player.YeongnoSwallowEnded -= OnYeongnoSwallowEnded;
        }
    }
}
