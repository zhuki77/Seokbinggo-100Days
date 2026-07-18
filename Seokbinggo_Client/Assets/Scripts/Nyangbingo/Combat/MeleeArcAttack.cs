using System.Collections.Generic;
using Nyangbingo.Core;
using Nyangbingo.Data;
using UnityEngine;

namespace Nyangbingo.Combat
{
    public sealed class MeleeArcAttack : MonoBehaviour
    {
        [SerializeField] private Transform origin;
        [SerializeField] private LayerMask targetLayers;
        [SerializeField] private float range = 1.5f;
        [Range(1, 180)][SerializeField] private float arcDegrees = 100f;
        [SerializeField] private int damage = 5;
        [SerializeField] private float knockback = .5f;
        [SerializeField] private CombatProfileDefinition combatProfile;
        [SerializeField] private ClawProfile clawProfile;
        public CombatProfileDefinition CombatProfile => combatProfile;
        public bool HitsWalls => combatProfile == null || combatProfile.HitsWalls;

        public void ConfigureForRuntime(Transform attackOrigin, LayerMask layers, float attackRange, float attackArc, int attackDamage, float attackKnockback)
        {
            origin = attackOrigin;
            targetLayers = layers;
            combatProfile = null;
            if (!float.IsNaN(attackRange) && !float.IsInfinity(attackRange)) range = Mathf.Max(0f, attackRange);
            if (!float.IsNaN(attackArc) && !float.IsInfinity(attackArc)) arcDegrees = Mathf.Clamp(attackArc, 1f, 180f);
            damage = Mathf.Max(1, attackDamage);
            if (!float.IsNaN(attackKnockback) && !float.IsInfinity(attackKnockback)) knockback = Mathf.Max(0f, attackKnockback);
        }

        public bool ConfigureForRuntime(Transform attackOrigin, LayerMask layers, CombatProfileDefinition profile)
        {
            if (profile == null || profile.RangeTiles <= 0f || float.IsNaN(profile.RangeTiles) ||
                float.IsInfinity(profile.RangeTiles) || profile.ArcDegrees < 1f || profile.ArcDegrees > 180f ||
                float.IsNaN(profile.ArcDegrees) || float.IsInfinity(profile.ArcDegrees)) return false;
            origin = attackOrigin;
            targetLayers = layers;
            combatProfile = profile;
            range = profile.RangeTiles;
            arcDegrees = profile.ArcDegrees;
            damage = profile.AttackDamage;
            knockback = profile.KnockbackTiles;
            return true;
        }

        public void Strike(Vector2 direction)
        {
            StrikeInternal(direction, false, 0, 0f);
        }

        public void Strike(Vector2 direction, int overrideDamage, float overrideKnockback)
        {
            StrikeInternal(direction, true, Mathf.Max(1, overrideDamage),
                float.IsNaN(overrideKnockback) || float.IsInfinity(overrideKnockback)
                    ? 0f
                    : Mathf.Max(0f, overrideKnockback));
        }

        private void StrikeInternal(Vector2 direction, bool useOverride, int overrideDamage, float overrideKnockback)
        {
            if (float.IsNaN(direction.x) || float.IsInfinity(direction.x) ||
                float.IsNaN(direction.y) || float.IsInfinity(direction.y) ||
                direction.sqrMagnitude <= Mathf.Epsilon) return;
            if (!useOverride && combatProfile != null && !combatProfile.HasBasicAttack) return;
            direction.Normalize();
            Vector2 center = origin == null ? (Vector2)transform.position : (Vector2)origin.position;
            if (range <= 0f || float.IsNaN(range) || float.IsInfinity(range)) return;
            var activeHeight = range;
            var activeDamage = Mathf.Max(1, damage);
            var activeKnockback = float.IsNaN(knockback) || float.IsInfinity(knockback) ? 0f : Mathf.Max(0f, knockback);
            if (useOverride)
            {
                activeDamage = overrideDamage;
                activeKnockback = overrideKnockback;
            }
            else if (combatProfile != null)
            {
                activeDamage = Mathf.Max(1, combatProfile.AttackDamage);
                if (!float.IsNaN(combatProfile.KnockbackTiles) && !float.IsInfinity(combatProfile.KnockbackTiles))
                    activeKnockback = Mathf.Max(0f, combatProfile.KnockbackTiles);
            }
            else if (clawProfile != null)
            {
                if (clawProfile.VerticalReach > 0f && !float.IsNaN(clawProfile.VerticalReach) &&
                    !float.IsInfinity(clawProfile.VerticalReach)) activeHeight = range * clawProfile.VerticalReach;
                activeDamage = Mathf.Max(1, clawProfile.Damage);
                if (!float.IsNaN(clawProfile.Knockback) && !float.IsInfinity(clawProfile.Knockback))
                    activeKnockback = Mathf.Max(0f, clawProfile.Knockback);
            }
            if (float.IsNaN(activeHeight) || float.IsInfinity(activeHeight)) activeHeight = range;
            var activeArc = float.IsNaN(arcDegrees) || float.IsInfinity(arcDegrees)
                ? 100f
                : Mathf.Clamp(arcDegrees, 1f, 180f);
            var queryCenter = center + direction * (range * .5f);
            var querySize = new Vector2(range, activeHeight);
            var queryAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            var damagedTargets = new HashSet<Health>();
            foreach (var hit in Physics2D.OverlapBoxAll(queryCenter, querySize, queryAngle, targetLayers))
            {
                var toTarget = ((Vector2)hit.transform.position - center).normalized;
                if (Vector2.Angle(direction, toTarget) > activeArc * .5f) continue;
                var health = hit.GetComponentInParent<Health>();
                if (health == null || !damagedTargets.Add(health)) continue;
                var healthBeforeDamage = health.Current;
                health.ApplyDamage(activeDamage, DamageTag.Melee);
                if (health.Current < healthBeforeDamage) GameEvents.RaiseYokaiDamaged();
                health.TryApplyKnockback(toTarget * activeKnockback);
                if (combatProfile != null && !combatProfile.MultiTarget) break;
            }
        }
    }

    public sealed class WireSnareAbility
    {
        public const float CooldownSeconds = 3f;
        public const int Damage = 4;
        public const float Knockback = 2f;
        private readonly MeleeArcAttack attack;
        private float remainingCooldown;
        public float RemainingCooldown => remainingCooldown;
        public bool IsReady => remainingCooldown <= 0f;

        public WireSnareAbility(MeleeArcAttack attack)
        {
            this.attack = attack;
        }

        public bool TryUse(Vector2 direction)
        {
            if (attack == null || !IsReady || float.IsNaN(direction.x) || float.IsInfinity(direction.x) ||
                float.IsNaN(direction.y) || float.IsInfinity(direction.y) ||
                direction.sqrMagnitude <= Mathf.Epsilon) return false;
            attack.Strike(direction, Damage, Knockback);
            remainingCooldown = CooldownSeconds;
            return true;
        }

        public void Tick(float gameSeconds)
        {
            if (gameSeconds <= 0f || float.IsNaN(gameSeconds) || float.IsInfinity(gameSeconds) || IsReady) return;
            remainingCooldown = Mathf.Max(0f, remainingCooldown - gameSeconds);
        }
    }

    public sealed class TurretController
    {
        private readonly Transform turretTransform;
        private readonly System.Func<IReadOnlyList<Health>> targetProvider;
        private readonly float retargetIntervalSeconds;
        private readonly float fireIntervalSeconds;
        private readonly float range;
        private readonly int damage;
        private readonly float fuelSecondsPerUnit;
        private float retargetRemaining;
        private float fireRemaining;
        private float fuelRemaining;
        public Health CurrentTarget { get; private set; }
        public float FuelRemaining => fuelRemaining;
        public bool IsPowered => fuelRemaining > 0f;
        public event System.Action<Health, int> Fired;

        public TurretController(Transform origin, System.Func<IReadOnlyList<Health>> targets, float retargetInterval,
            float fireInterval, float attackRange, int attackDamage, float secondsPerFuelUnit)
        {
            turretTransform = origin;
            targetProvider = targets;
            retargetIntervalSeconds = float.IsNaN(retargetInterval) || float.IsInfinity(retargetInterval)
                ? .0001f
                : Mathf.Max(.0001f, retargetInterval);
            fireIntervalSeconds = float.IsNaN(fireInterval) || float.IsInfinity(fireInterval)
                ? .0001f
                : Mathf.Max(.0001f, fireInterval);
            range = float.IsNaN(attackRange) || float.IsInfinity(attackRange) ? 0f : Mathf.Max(0f, attackRange);
            damage = Mathf.Max(1, attackDamage);
            fuelSecondsPerUnit = float.IsNaN(secondsPerFuelUnit) || float.IsInfinity(secondsPerFuelUnit)
                ? 0f
                : Mathf.Max(0f, secondsPerFuelUnit);
        }

        public bool AddFuel(int units)
        {
            if (units <= 0 || fuelSecondsPerUnit <= 0f) return false;
            var addedSeconds = (double)units * fuelSecondsPerUnit;
            if (double.IsNaN(addedSeconds) || double.IsInfinity(addedSeconds) ||
                addedSeconds > float.MaxValue - fuelRemaining) return false;
            fuelRemaining += (float)addedSeconds;
            return true;
        }

        public bool RestoreFuelSeconds(float gameSeconds)
        {
            if (float.IsNaN(gameSeconds) || float.IsInfinity(gameSeconds) || gameSeconds < 0f) return false;
            fuelRemaining = gameSeconds;
            return true;
        }

        public bool RestoreFuelUnits(int units)
        {
            if (units < 0) return false;
            if (units == 0)
            {
                fuelRemaining = 0f;
                return true;
            }
            if (fuelSecondsPerUnit <= 0f) return false;
            var restoredSeconds = (double)units * fuelSecondsPerUnit;
            if (double.IsNaN(restoredSeconds) || double.IsInfinity(restoredSeconds) || restoredSeconds > float.MaxValue)
                return false;
            fuelRemaining = (float)restoredSeconds;
            return true;
        }

        public void Tick(float gameSeconds)
        {
            if (gameSeconds <= 0f || float.IsNaN(gameSeconds) || float.IsInfinity(gameSeconds) ||
                !IsPowered || turretTransform == null) return;
            var activeSeconds = Mathf.Min(gameSeconds, fuelRemaining);
            fuelRemaining = Mathf.Max(0f, fuelRemaining - activeSeconds);

            retargetRemaining -= activeSeconds;
            if (!IsValidTarget(CurrentTarget) || retargetRemaining <= .0001f)
            {
                AcquireNearestTarget();
                retargetRemaining = retargetIntervalSeconds;
            }

            fireRemaining -= activeSeconds;
            while (IsValidTarget(CurrentTarget) && fireRemaining <= .0001f)
            {
                var firedTarget = CurrentTarget;
                Fired?.Invoke(firedTarget, damage);
                fireRemaining += fireIntervalSeconds;
            }
        }

        private void AcquireNearestTarget()
        {
            CurrentTarget = null;
            var targets = targetProvider?.Invoke();
            if (targets == null) return;
            var bestDistance = range * range;
            foreach (var candidate in targets)
            {
                if (candidate == null || candidate.IsDead) continue;
                var distance = ((Vector2)candidate.transform.position - (Vector2)turretTransform.position).sqrMagnitude;
                if (distance > bestDistance) continue;
                bestDistance = distance;
                CurrentTarget = candidate;
            }
        }

        private bool IsValidTarget(Health target)
            => target != null && !target.IsDead &&
               ((Vector2)target.transform.position - (Vector2)turretTransform.position).sqrMagnitude <= range * range;
    }

    public sealed class HomingProjectile
    {
        private Health target;
        private int damage;
        private DamageTag damageTag;
        private float speed;
        private float arrivalDistance;
        public Vector2 Position { get; private set; }
        public bool IsActive { get; private set; }

        public void Activate(Vector2 startPosition, Health targetHealth, int attackDamage, DamageTag tag, float moveSpeed, float hitDistance)
        {
            Position = startPosition;
            target = targetHealth;
            damage = Mathf.Max(1, attackDamage);
            damageTag = tag;
            speed = moveSpeed;
            arrivalDistance = hitDistance;
            IsActive = target != null && !target.IsDead &&
                       !float.IsNaN(startPosition.x) && !float.IsInfinity(startPosition.x) &&
                       !float.IsNaN(startPosition.y) && !float.IsInfinity(startPosition.y) &&
                       moveSpeed > 0f && !float.IsNaN(moveSpeed) && !float.IsInfinity(moveSpeed) &&
                       hitDistance >= 0f && !float.IsNaN(hitDistance) && !float.IsInfinity(hitDistance);
            if (!IsActive) target = null;
        }

        public bool Tick(float gameSeconds)
        {
            if (!IsActive || gameSeconds <= 0f || float.IsNaN(gameSeconds) || float.IsInfinity(gameSeconds)) return false;
            if (target == null || target.IsDead)
            {
                Deactivate();
                return false;
            }

            var targetPosition = (Vector2)target.transform.position;
            if (float.IsNaN(targetPosition.x) || float.IsInfinity(targetPosition.x) ||
                float.IsNaN(targetPosition.y) || float.IsInfinity(targetPosition.y))
            {
                Deactivate();
                return false;
            }
            var toTarget = targetPosition - Position;
            var distance = toTarget.magnitude;
            var travelDistance = speed * gameSeconds;
            if (distance <= arrivalDistance || travelDistance >= distance)
            {
                Position = targetPosition;
                var healthBeforeDamage = target.Current;
                target.ApplyDamage(damage, damageTag);
                if (target.Current < healthBeforeDamage) GameEvents.RaiseYokaiDamaged();
                Deactivate();
                return true;
            }

            Position += toTarget / distance * travelDistance;
            return false;
        }

        private void Deactivate()
        {
            IsActive = false;
            target = null;
        }
    }

    public sealed class HomingProjectilePool
    {
        private readonly List<HomingProjectile> projectiles = new List<HomingProjectile>();
        public int CreatedCount => projectiles.Count;
        public int ActiveCount { get { var count = 0; foreach (var projectile in projectiles) if (projectile.IsActive) count++; return count; } }

        public HomingProjectilePool(int initialCapacity)
        {
            for (var i = 0; i < Mathf.Max(0, initialCapacity); i++) projectiles.Add(new HomingProjectile());
        }

        public HomingProjectile Spawn(Vector2 startPosition, Health target, int damage, DamageTag tag, float speed, float arrivalDistance)
        {
            HomingProjectile projectile = null;
            foreach (var candidate in projectiles)
                if (!candidate.IsActive)
                {
                    projectile = candidate;
                    break;
                }
            if (projectile == null)
            {
                projectile = new HomingProjectile();
                projectiles.Add(projectile);
            }
            projectile.Activate(startPosition, target, damage, tag, speed, arrivalDistance);
            return projectile;
        }

        public int Tick(float gameSeconds)
        {
            if (gameSeconds <= 0f || float.IsNaN(gameSeconds) || float.IsInfinity(gameSeconds)) return 0;
            var hitCount = 0;
            foreach (var projectile in projectiles)
                if (projectile.IsActive && projectile.Tick(gameSeconds)) hitCount++;
            return hitCount;
        }
    }
}
