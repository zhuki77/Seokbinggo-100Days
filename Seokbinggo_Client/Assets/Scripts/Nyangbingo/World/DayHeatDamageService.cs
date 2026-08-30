using System;
using Nyangbingo.Combat;
using Nyangbingo.Core;
using Nyangbingo.Data;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>v72 A-5: 네임드 처치 기반 폭염 단계의 낮 지상 화상을 연속 환경 피해로 적용한다.</summary>
    public sealed class DayHeatDamageService : IGameSecondsTickable, IDisposable
    {
        private readonly Health health;
        private readonly Transform player;
        private readonly DayNightService timeService;
        private readonly WorldSessionController session;
        private readonly HeatStageService heatStage;
        private readonly GameDataCatalog catalog;
        private readonly MainGameEnvironmentState environmentState;
        private float fractionalDamage;
        private bool disposed;

        public DayHeatDamageService(Health playerHealth, Transform playerTransform,
            DayNightService clock, WorldSessionController worldSession, HeatStageService stages,
            GameDataCatalog data, MainGameEnvironmentState environment = null)
        {
            health = playerHealth ?? throw new ArgumentNullException(nameof(playerHealth));
            player = playerTransform ?? throw new ArgumentNullException(nameof(playerTransform));
            timeService = clock ?? throw new ArgumentNullException(nameof(clock));
            session = worldSession ?? throw new ArgumentNullException(nameof(worldSession));
            heatStage = stages ?? throw new ArgumentNullException(nameof(stages));
            catalog = data;
            environmentState = environment;
            health.Died += ResetExposure;
        }

        public void Tick(float deltaGameSeconds)
        {
            if (disposed || deltaGameSeconds <= 0f || float.IsNaN(deltaGameSeconds) ||
                float.IsInfinity(deltaGameSeconds)) return;
            if (health.IsDead || timeService.IsNight || !session.HasWorld ||
                !WorldExposureRules.TryIsSurfaceExposed(
                    player.position, session.LastResult.surfaceHeights, out var exposed) || !exposed)
            {
                fractionalDamage = 0f;
                return;
            }

            DayCurveCombatRules.ResolveHeatStageModifiers(
                catalog, timeService.Day, environmentState?.HeatStageReduction ?? 0,
                out var reduction, out var escalation);
            var rate = heatStage.ResolveDayFireDamagePerSecond(timeService.Day, reduction, escalation);
            if (rate <= 0f) return;
            var effectiveRate = rate * health.DamageTakenMultiplier * health.FireDamageMultiplier;
            if (float.IsNaN(effectiveRate) || float.IsInfinity(effectiveRate) || effectiveRate <= 0f) return;
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

        private void ResetExposure() => fractionalDamage = 0f;
    }
}
