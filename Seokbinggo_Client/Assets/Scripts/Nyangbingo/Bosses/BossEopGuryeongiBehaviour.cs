using Nyangbingo.Combat;
using Nyangbingo.Core;
using Nyangbingo.World;
using UnityEngine;

namespace Nyangbingo.Bosses
{
    /// <summary>
    /// 업구렁이 — 기지 안에서 모듈을 하나씩 정지하고, 복구(재가동) 중에는 딜 0.
    /// </summary>
    public sealed class BossEopGuryeongiBehaviour : MonoBehaviour, IGameSecondsTickable
    {
        public const float ModuleShutdownIntervalSeconds = 14f;
        public const float RecoveryImmunitySeconds = 6f;

        private Health health;
        private BossCombatController combat;
        private MainGameTurretRuntime turretRuntime;
        private float shutdownCooldown;
        private float recoveryRemaining;

        public float RecoveryRemaining => recoveryRemaining;
        public bool IsRecovering => recoveryRemaining > 0f;

        public void Configure(MainGameTurretRuntime turrets)
        {
            health = GetComponent<Health>();
            combat = GetComponent<BossCombatController>();
            turretRuntime = turrets;
            shutdownCooldown = ModuleShutdownIntervalSeconds;
        }

        public void Tick(float deltaGameSeconds)
        {
            if (health == null || health.IsDead || deltaGameSeconds <= 0f) return;

            if (recoveryRemaining > 0f)
            {
                recoveryRemaining = Mathf.Max(0f, recoveryRemaining - deltaGameSeconds);
                if (combat == null || !combat.IsOpeningDodgeActive)
                    health.SetDamageTakenMultiplier(0f);
                if (recoveryRemaining <= 0f && (combat == null || !combat.IsOpeningDodgeActive))
                    health.SetDamageTakenMultiplier(1f);
                return;
            }

            if (combat != null && combat.IsOpeningDodgeActive) return;

            health.SetDamageTakenMultiplier(1f);
            shutdownCooldown = Mathf.Max(0f, shutdownCooldown - deltaGameSeconds);
            if (shutdownCooldown > 0f) return;

            if (turretRuntime != null && turretRuntime.TrySuspendNextModuleForEop(out _))
            {
                recoveryRemaining = RecoveryImmunitySeconds;
                health.SetDamageTakenMultiplier(0f);
            }

            shutdownCooldown = ModuleShutdownIntervalSeconds;
        }

        private void OnDestroy()
        {
            turretRuntime?.ClearEopModuleSuspensions();
        }
    }
}
