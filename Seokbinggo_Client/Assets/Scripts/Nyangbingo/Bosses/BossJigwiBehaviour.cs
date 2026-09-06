using System;
using System.Collections.Generic;
using Nyangbingo.Combat;
using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using Nyangbingo.World;
using UnityEngine;

namespace Nyangbingo.Bosses
{
    /// <summary>
    /// 지귀 — 서 있던 자리가 순차 발화. 이동하지 않으면 달군 바닥에 피해.
    /// </summary>
    public sealed class BossJigwiBehaviour : MonoBehaviour, IGameSecondsTickable
    {
        public const float EmberLifetimeSeconds = 6f;
        public const float EmberRadiusTiles = 1.05f;
        public const float EmberTickSeconds = 1f;
        public const int MaxEmbers = 8;
        public const float PositionSampleIntervalSeconds = 1f;
        public const float StationaryEmberDistanceTiles = 0.15f;

        private struct EmberZone
        {
            public Vector2 Center;
            public float LifetimeRemaining;
            public float TickRemaining;
        }

        private readonly List<EmberZone> embers = new List<EmberZone>();
        private Health health;
        private BossCombatController combat;
        private Transform playerTransform;
        private IBossCombatTarget playerTarget;
        private BossDefinition definition;
        private float sampleCooldown;
        private Vector2 lastSampledPosition;
        private bool hasSample;

        public int ActiveEmberCount => embers.Count;

        public void Configure(Transform player, BossDefinition bossDefinition = null)
        {
            health = GetComponent<Health>();
            combat = GetComponent<BossCombatController>();
            playerTransform = player;
            playerTarget = player as IBossCombatTarget;
            definition = bossDefinition ?? combat?.Definition;
            if (combat != null)
                combat.SpecialStarted += OnSpecialStarted;
        }

        public void Tick(float deltaGameSeconds)
        {
            if (health == null || health.IsDead || deltaGameSeconds <= 0f) return;
            SamplePosition(deltaGameSeconds);
            TickEmbers(deltaGameSeconds);
        }

        private void OnSpecialStarted() => QueueEmberAt(transform.position);

        private void SamplePosition(float deltaGameSeconds)
        {
            sampleCooldown = Mathf.Max(0f, sampleCooldown - deltaGameSeconds);
            if (sampleCooldown > 0f) return;

            sampleCooldown = PositionSampleIntervalSeconds;
            var position = (Vector2)transform.position;
            if (hasSample &&
                Vector2.Distance(position, lastSampledPosition) <= StationaryEmberDistanceTiles)
                QueueEmberAt(transform.position);

            lastSampledPosition = position;
            hasSample = true;
        }

        private void QueueEmberAt(Vector3 worldPosition)
        {
            if (embers.Count >= MaxEmbers)
                embers.RemoveAt(0);
            embers.Add(new EmberZone
            {
                Center = worldPosition,
                LifetimeRemaining = EmberLifetimeSeconds,
                TickRemaining = 0f
            });
        }

        private void TickEmbers(float deltaGameSeconds)
        {
            var tickDamage = definition != null ? definition.SpecialDamagePerHit : 12;
            for (var index = embers.Count - 1; index >= 0; index--)
            {
                var ember = embers[index];
                ember.LifetimeRemaining = Mathf.Max(0f, ember.LifetimeRemaining - deltaGameSeconds);
                ember.TickRemaining -= deltaGameSeconds;
                while (ember.TickRemaining <= 0f && ember.LifetimeRemaining > 0f)
                {
                    if (playerTransform != null && playerTarget != null &&
                        !HasJigwiAshImmunity() && IsPlayerInEmber(ember.Center))
                    {
                        playerTarget.TryApplyBossSpecialDamage(
                            tickDamage, DamageTag.Melee, Vector2.zero, showFireHitEffect: true);
                    }
                    ember.TickRemaining += EmberTickSeconds;
                }

                if (ember.LifetimeRemaining <= 0f)
                    embers.RemoveAt(index);
                else
                    embers[index] = ember;
            }
        }

        private bool IsPlayerInEmber(Vector2 center) =>
            playerTransform != null &&
            Vector2.Distance(center, playerTransform.position) <= EmberRadiusTiles;

        private bool HasJigwiAshImmunity()
        {
            var player = playerTransform != null
                ? playerTransform.GetComponent<MainGamePlayerController>()
                : null;
            return GimmickWeaponCombatRules.IsActiveProfile(player, GimmickWeaponProgress.JigwiAshId);
        }

        private void OnDestroy()
        {
            if (combat != null)
                combat.SpecialStarted -= OnSpecialStarted;
        }
    }
}
