using Nyangbingo.Combat;
using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.World;
using UnityEngine;

namespace Nyangbingo.Yokai
{
    public interface IYokaiTarget { Transform TargetTransform { get; } void DamageWall(float amount); }
    public interface IYokaiCombatTarget { bool TryApplyContactDamage(int amount); }
    public interface IYokaiLootTarget { bool TryStealGroundLoot(); bool TryStealInventory(int maxSlots, int maxAmount); }
    public interface IWallMaterialTarget { YokaiWallMaterial WallMaterial { get; } }

    [RequireComponent(typeof(Health))]
    public sealed class YokaiBrain : MonoBehaviour, IGameSecondsTickable
    {
        private const float ContactAttackIntervalGameSeconds = 1f;
        private const float AttackRangeTolerance = .05f;
        private enum State { Approach, AttackWall, StealLoot, Retreat, DawnFlee }
        [SerializeField] private YokaiDefinition definition;
        [SerializeField] private MonoBehaviour gameSecondsSourceComponent;
        [SerializeField] private Renderer visibilityRenderer;
        [SerializeField] private float wallAttackRange = 1f;
        [SerializeField] private float retreatSpeedMultiplier = .5f;
        private IYokaiTarget target;
        private IYokaiCounterSource counterSource;
        private bool hasExplicitCounterSource;
        private State state;
        private float sieveStopRemaining;
        private float sieveCooldownRemaining;
        private float lanternPauseRemaining;
        private float bloomCooldownRemaining;
        private Health health;
        private IGameSecondsSource gameSecondsSource;
        private float lastGameSeconds;
        private bool hasGameSecondsSample;
        private YokaiSpawnTrack spawnTrack;
        private Vector3 dawnFleeDirection;
        private bool hasFledOffscreen;
        private float contactAttackRemaining;
        private float frostSlowFraction;
        private float frostSlowRemaining;
        private bool bossEncounterPaused;
        private WorldMobPhysicsBody physicsBody;
        private RuntimeCharacterSpriteAnimator characterAnimator;
        private SpriteRenderer[] pausedRenderers = System.Array.Empty<SpriteRenderer>();
        private Color[] pausedRendererColors = System.Array.Empty<Color>();
        public YokaiDefinition Definition => definition;
        public YokaiSpawnTrack SpawnTrack => spawnTrack;
        public bool IsDawnFleeing => state == State.DawnFlee;
        public float SieveStopRemaining => sieveStopRemaining;
        public float SieveCooldownRemaining => sieveCooldownRemaining;
        public float LanternPauseRemaining => lanternPauseRemaining;
        public float BloomCooldownRemaining => bloomCooldownRemaining;
        public bool IsInLanternRange => (counterSource ?? target as IYokaiCounterSource)?.IsInLanternRange == true;
        public float FrostSlowRemaining => frostSlowRemaining;
        public float FrostSpeedMultiplier => CalculateFrostSpeedMultiplier(frostSlowFraction, frostSlowRemaining);
        public bool IsBossEncounterPaused => bossEncounterPaused;
        public event System.Action Bloomed;
        public event System.Action Attacked;
        public event System.Action<YokaiDefinition> DawnFleeStarted;
        public event System.Action<YokaiDefinition> FledOffscreen;

        private void Awake()
        {
            health = GetComponent<Health>();
            physicsBody = GetComponent<WorldMobPhysicsBody>();
            characterAnimator = GetComponentInChildren<RuntimeCharacterSpriteAnimator>();
            gameSecondsSource = gameSecondsSourceComponent as IGameSecondsSource;
            if (visibilityRenderer == null) visibilityRenderer = GetComponentInChildren<Renderer>();
        }

        private void OnEnable()
        {
            ResetGameSecondsSample();
            GameEvents.OnDawnWarning += HandleDawnWarning;
        }

        private void OnDisable() => GameEvents.OnDawnWarning -= HandleDawnWarning;

        public void SetTarget(IYokaiTarget value)
        {
            target = value;
            if (!hasExplicitCounterSource) counterSource = value as IYokaiCounterSource;
            if (state != State.Retreat && state != State.DawnFlee) state = State.Approach;
        }
        public void ConfigureForRuntime(YokaiDefinition value, IYokaiTarget targetValue,
            IYokaiCounterSource counters = null, YokaiSpawnTrack instanceSpawnTrack = YokaiSpawnTrack.Raid)
        {
            definition = value;
            target = targetValue;
            spawnTrack = definition != null &&
                         (instanceSpawnTrack == YokaiSpawnTrack.Raid || instanceSpawnTrack == YokaiSpawnTrack.Resident) &&
                         definition.SupportsSpawnTrack(instanceSpawnTrack)
                ? instanceSpawnTrack
                : YokaiSpawnTrack.None;
            hasExplicitCounterSource = counters != null;
            counterSource = counters ?? targetValue as IYokaiCounterSource;
            state = State.Approach;
            sieveStopRemaining = 0f;
            sieveCooldownRemaining = 0f;
            lanternPauseRemaining = 0f;
            bloomCooldownRemaining = 0f;
            dawnFleeDirection = Vector3.zero;
            hasFledOffscreen = false;
            contactAttackRemaining = 0f;
            frostSlowFraction = 0f;
            frostSlowRemaining = 0f;
            SetBossEncounterPaused(false);
            health = GetComponent<Health>();
            physicsBody = GetComponent<WorldMobPhysicsBody>();
            characterAnimator = GetComponentInChildren<RuntimeCharacterSpriteAnimator>();
            if (health != null)
            {
                if (definition != null) health.ConfigureForRuntime(definition.HitPoints);
                health.SetDamageTakenMultiplier(1f);
            }
            ResetGameSecondsSample();
        }
        public void BeginRetreat()
        {
            if (state != State.DawnFlee) state = State.Retreat;
        }

        public bool BeginDawnFlee()
        {
            if (state == State.DawnFlee || definition == null || spawnTrack != YokaiSpawnTrack.Raid ||
                !definition.RaidFleesAtDawn) return false;
            if (health == null) health = GetComponent<Health>();
            if (health != null && health.IsDead) return false;

            var targetPosition = target?.TargetTransform != null ? target.TargetTransform.position : transform.position;
            var away = transform.position - targetPosition;
            dawnFleeDirection = IsFinite(away) && away.sqrMagnitude > Mathf.Epsilon
                ? away.normalized
                : Vector3.left;
            state = State.DawnFlee;
            DawnFleeStarted?.Invoke(definition);
            return true;
        }

        public bool TryDespawnIfOffscreen(bool isVisible)
        {
            if (state != State.DawnFlee || isVisible || hasFledOffscreen) return false;
            hasFledOffscreen = true;
            FledOffscreen?.Invoke(definition);
            Destroy(gameObject);
            return true;
        }

        public void SetGameSecondsSource(IGameSecondsSource source)
        {
            gameSecondsSource = source;
            ResetGameSecondsSample();
        }

        public bool SetBossEncounterPaused(bool paused)
        {
            if (bossEncounterPaused == paused) return true;
            if (physicsBody == null) physicsBody = GetComponent<WorldMobPhysicsBody>();
            physicsBody?.SetEncounterPaused(paused);
            if (paused)
            {
                pausedRenderers = GetComponentsInChildren<SpriteRenderer>(true);
                pausedRendererColors = new Color[pausedRenderers.Length];
                for (var index = 0; index < pausedRenderers.Length; index++)
                {
                    var renderer = pausedRenderers[index];
                    if (renderer == null) continue;
                    pausedRendererColors[index] = renderer.color;
                    var color = renderer.color;
                    color.a *= .45f;
                    renderer.color = color;
                }
            }
            else
            {
                for (var index = 0; index < pausedRenderers.Length && index < pausedRendererColors.Length; index++)
                    if (pausedRenderers[index] != null) pausedRenderers[index].color = pausedRendererColors[index];
                pausedRenderers = System.Array.Empty<SpriteRenderer>();
                pausedRendererColors = System.Array.Empty<Color>();
            }
            bossEncounterPaused = paused;
            ResetGameSecondsSample();
            return true;
        }

        public bool ApplyFrostSlow(float slowFraction, float durationSeconds)
        {
            if (float.IsNaN(slowFraction) || float.IsInfinity(slowFraction) || slowFraction <= 0f ||
                float.IsNaN(durationSeconds) || float.IsInfinity(durationSeconds) || durationSeconds <= 0f)
                return false;
            frostSlowFraction = Mathf.Max(frostSlowFraction, Mathf.Clamp01(slowFraction));
            frostSlowRemaining = Mathf.Max(frostSlowRemaining, durationSeconds);
            return true;
        }

        private void Update()
        {
            TickFromGameClock();
            if (state != State.DawnFlee) return;
            if (visibilityRenderer == null) visibilityRenderer = GetComponentInChildren<Renderer>();
            if (visibilityRenderer != null) TryDespawnIfOffscreen(visibilityRenderer.isVisible);
        }

        public void TickFromGameClock()
        {
            if (gameSecondsSource == null) return;
            var currentGameSeconds = gameSecondsSource.GameSeconds;
            if (float.IsNaN(currentGameSeconds) || float.IsInfinity(currentGameSeconds)) return;
            if (!hasGameSecondsSample)
            {
                lastGameSeconds = currentGameSeconds;
                hasGameSecondsSample = true;
                return;
            }

            var deltaGameSeconds = currentGameSeconds - lastGameSeconds;
            lastGameSeconds = currentGameSeconds;
            if (deltaGameSeconds > 0f) Tick(deltaGameSeconds);
        }

        public void Tick(float deltaSeconds)
        {
            SetAnimationMoving(false);
            if (bossEncounterPaused || deltaSeconds < 0f || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) ||
                definition == null) return;
            if (health == null) health = GetComponent<Health>();
            if (health != null && health.IsDead) return;
            var actionSeconds = CalculateFrostAdjustedActionSeconds(
                deltaSeconds, frostSlowFraction, frostSlowRemaining);
            frostSlowRemaining = Mathf.Max(0f, frostSlowRemaining - deltaSeconds);
            if (frostSlowRemaining <= .0001f)
            {
                frostSlowRemaining = 0f;
                frostSlowFraction = 0f;
            }
            if (state == State.DawnFlee)
            {
                MoveRetreat(dawnFleeDirection, actionSeconds);
                return;
            }
            if (target == null || target.TargetTransform == null) return;
            var counters = counterSource ?? target as IYokaiCounterSource;
            if (sieveCooldownRemaining > 0f)
            {
                sieveCooldownRemaining = Mathf.Max(0f, sieveCooldownRemaining - deltaSeconds);
                if (sieveCooldownRemaining <= .0001f) sieveCooldownRemaining = 0f;
            }
            if (bloomCooldownRemaining > 0f)
            {
                bloomCooldownRemaining = Mathf.Max(0f, bloomCooldownRemaining - deltaSeconds);
                if (bloomCooldownRemaining <= .0001f) bloomCooldownRemaining = 0f;
            }
            if (health != null && (definition.Kind == Nyangbingo.Core.YokaiKind.Yagwanggwi ||
                                   definition.Kind == Nyangbingo.Core.YokaiKind.Eoduksini))
                health.SetDamageTakenMultiplier(YokaiSpecialRules.DamageTakenMultiplier(definition, counters));
            if (definition.Kind == Nyangbingo.Core.YokaiKind.Yagwanggwi)
            {
                if (sieveStopRemaining > 0f)
                {
                    var stoppedSeconds = Mathf.Min(sieveStopRemaining, actionSeconds);
                    sieveStopRemaining -= stoppedSeconds;
                    if (sieveStopRemaining <= .0001f) sieveStopRemaining = 0f;
                    actionSeconds -= stoppedSeconds;
                    if (actionSeconds <= .0001f) return;
                }
                var sieveStopSeconds = counters?.SieveStopSeconds ?? 0f;
                if (counters != null && counters.IsInSieveRange && sieveCooldownRemaining <= 0f &&
                    sieveStopSeconds > 0f && !float.IsNaN(sieveStopSeconds) && !float.IsInfinity(sieveStopSeconds))
                {
                    var sieveCooldownSeconds = counters.SieveCooldownSeconds;
                    if (sieveCooldownSeconds < 0f || float.IsNaN(sieveCooldownSeconds) ||
                        float.IsInfinity(sieveCooldownSeconds)) sieveCooldownSeconds = 0f;
                    var stoppedSeconds = Mathf.Min(sieveStopSeconds, actionSeconds);
                    sieveStopRemaining = Mathf.Max(0f, sieveStopSeconds - stoppedSeconds);
                    sieveCooldownRemaining = Mathf.Max(0f, sieveCooldownSeconds - actionSeconds);
                    actionSeconds -= stoppedSeconds;
                    if (sieveStopRemaining > 0f || actionSeconds <= .0001f) return;
                }
            }
            if (definition.Kind == Nyangbingo.Core.YokaiKind.Eoduksini)
            {
                if (lanternPauseRemaining > 0f)
                {
                    var pausedSeconds = Mathf.Min(lanternPauseRemaining, actionSeconds);
                    lanternPauseRemaining -= pausedSeconds;
                    if (lanternPauseRemaining <= .0001f) lanternPauseRemaining = 0f;
                    actionSeconds -= pausedSeconds;
                    if (actionSeconds <= .0001f) return;
                }
                var lanternPauseSeconds = counters?.EoduksiniLanternPauseSeconds ?? 0f;
                if (counters != null && counters.IsInLanternRange && bloomCooldownRemaining <= 0f &&
                    lanternPauseSeconds > 0f && !float.IsNaN(lanternPauseSeconds) &&
                    !float.IsInfinity(lanternPauseSeconds))
                {
                    var bloomCooldownSeconds = counters.EoduksiniBloomCooldownSeconds;
                    if (bloomCooldownSeconds < 0f || float.IsNaN(bloomCooldownSeconds) ||
                        float.IsInfinity(bloomCooldownSeconds)) bloomCooldownSeconds = 0f;
                    var pausedSeconds = Mathf.Min(lanternPauseSeconds, actionSeconds);
                    lanternPauseRemaining = Mathf.Max(0f, lanternPauseSeconds - pausedSeconds);
                    bloomCooldownRemaining = Mathf.Max(0f, bloomCooldownSeconds - actionSeconds);
                    Bloomed?.Invoke();
                    GameEvents.RaiseEoduksiniBloomed();
                    actionSeconds -= pausedSeconds;
                    if (lanternPauseRemaining > 0f || actionSeconds <= .0001f) return;
                }
            }
            var targetPosition = target.TargetTransform.position;
            var currentPosition = transform.position;
            if (!IsFinite(currentPosition) || !IsFinite(targetPosition)) return;
            var targetOffset = targetPosition - currentPosition;
            var navigationOffset = physicsBody != null
                ? (Vector3)physicsBody.NavigationOffset(targetOffset)
                : targetOffset;
            var navigationDistance = navigationOffset.magnitude;
            var direction = navigationDistance <= Mathf.Epsilon
                ? Vector3.zero
                : physicsBody != null
                    ? (Vector3)physicsBody.NavigationDirection(targetOffset)
                    : navigationOffset / navigationDistance;
            var attackRange = float.IsNaN(wallAttackRange) || float.IsInfinity(wallAttackRange)
                ? 1f
                : Mathf.Max(0f, wallAttackRange);
            contactAttackRemaining = Mathf.Max(0f, contactAttackRemaining - actionSeconds);
            switch (state)
            {
                case State.Approach:
                    if (YokaiSpecialRules.ShouldAttemptTheft(definition.Kind, counters)) state = State.StealLoot;
                    else if (CanAttackTarget(targetPosition, attackRange)) state = State.AttackWall;
                    else if (MoveTowardAttackRange(direction, navigationDistance, attackRange, actionSeconds))
                        state = State.AttackWall;
                    break;
                case State.StealLoot:
                    if (!CanAttackTarget(targetPosition, attackRange))
                        MoveTowardAttackRange(direction, navigationDistance, attackRange, actionSeconds);
                    else
                    {
                        var lootTarget = target as IYokaiLootTarget;
                        var stoleLoot = YokaiSpecialRules.ShouldStealGroundLoot(definition.Kind, counters) && lootTarget?.TryStealGroundLoot() == true;
                        if (!stoleLoot && YokaiSpecialRules.CanStealInventory(definition.Kind, counters) &&
                            definition.StealSlots > 0 && definition.StealMaxItems > 0)
                            stoleLoot = lootTarget?.TryStealInventory(
                                definition.StealSlots, definition.StealMaxItems) == true;
                        state = stoleLoot ? State.Retreat : State.Approach;
                    }
                    break;
                case State.AttackWall:
                    if (!CanAttackTarget(targetPosition, attackRange))
                    {
                        state = State.Approach;
                        MoveTowardAttackRange(direction, navigationDistance, attackRange, actionSeconds);
                        break;
                    }
                    if (YokaiSpecialRules.ShouldAttemptTheft(definition.Kind, counters))
                    {
                        state = State.StealLoot;
                        break;
                    }
                    var combatTarget = target as IYokaiCombatTarget;
                    if (combatTarget != null)
                    {
                        if (contactAttackRemaining <= .0001f && definition.ContactDamage > 0 &&
                            combatTarget.TryApplyContactDamage(definition.ContactDamage))
                        {
                            contactAttackRemaining = ContactAttackIntervalGameSeconds;
                            Attacked?.Invoke();
                        }
                        break;
                    }
                    var wall = target as IWallMaterialTarget;
                    var wallDamagePerSecond = definition.WallDamageFor(
                        wall?.WallMaterial ?? YokaiWallMaterial.Default);
                    if (wallDamagePerSecond > 0f && !float.IsNaN(wallDamagePerSecond) &&
                        !float.IsInfinity(wallDamagePerSecond))
                    {
                        var damage = wallDamagePerSecond * actionSeconds;
                        if (!float.IsNaN(damage) && !float.IsInfinity(damage))
                        {
                            target.DamageWall(damage);
                            Attacked?.Invoke();
                            GameEvents.RaiseWallDamaged();
                        }
                    }
                    break;
                case State.Retreat:
                    MoveRetreat(-direction, actionSeconds);
                    break;
            }
        }

        private void MoveRetreat(Vector3 direction, float actionSeconds)
        {
            var moveSpeed = definition.MoveSpeed;
            var retreatMultiplier = retreatSpeedMultiplier;
            if (moveSpeed <= 0f || retreatMultiplier <= 0f || float.IsNaN(moveSpeed) ||
                float.IsInfinity(moveSpeed) || float.IsNaN(retreatMultiplier) ||
                float.IsInfinity(retreatMultiplier) || !IsFinite(direction)) return;
            var retreatDistance = moveSpeed * retreatMultiplier * actionSeconds;
            if (!float.IsNaN(retreatDistance) && !float.IsInfinity(retreatDistance))
                MoveBy(direction * retreatDistance);
        }

        private bool MoveTowardAttackRange(Vector3 direction, float distance, float attackRange, float actionSeconds)
        {
            var moveSpeed = definition.MoveSpeed;
            if (moveSpeed <= 0f || float.IsNaN(moveSpeed) || float.IsInfinity(moveSpeed)) return false;
            var travelDistance = moveSpeed * actionSeconds;
            if (float.IsNaN(travelDistance) || float.IsInfinity(travelDistance)) return false;
            var maximumDistance = Mathf.Max(0f, distance - attackRange);
            var movedDistance = Mathf.Min(travelDistance, maximumDistance);
            MoveBy(direction * movedDistance);
            return CanAttackTarget(target.TargetTransform.position, attackRange);
        }

        private bool CanAttackTarget(Vector3 targetPosition, float attackRange)
        {
            var offset = targetPosition - transform.position;
            if (!IsFinite(offset) || !IsWithinAttackRange(offset.magnitude, attackRange)) return false;
            return physicsBody == null || physicsBody.HasClearAttackLine(targetPosition);
        }

        private float MoveBy(Vector3 displacement)
        {
            var movedDistance = physicsBody != null ? physicsBody.Move(displacement) : displacement.magnitude;
            if (physicsBody == null) transform.position += displacement;
            if (movedDistance > Mathf.Epsilon)
            {
                if (characterAnimator == null)
                    characterAnimator = GetComponentInChildren<RuntimeCharacterSpriteAnimator>();
                characterAnimator?.SetFacing(ResolveFacingDirection(displacement));
                characterAnimator?.SetMoving(true);
            }
            return movedDistance;
        }

        private Vector2 ResolveFacingDirection(Vector3 movement)
        {
            if (target?.TargetTransform == null) return movement;
            var targetOffset = target.TargetTransform.position - transform.position;
            if (targetOffset.magnitude > 1.5f && Mathf.Abs(targetOffset.x) > .35f)
                return targetOffset.x > 0f ? Vector2.right : Vector2.left;
            return movement;
        }

        private void SetAnimationMoving(bool moving)
        {
            if (characterAnimator == null)
                characterAnimator = GetComponentInChildren<RuntimeCharacterSpriteAnimator>();
            characterAnimator?.SetMoving(moving);
        }

        private static bool IsWithinAttackRange(float distance, float attackRange) =>
            distance <= attackRange + AttackRangeTolerance;

        public static float CalculateFrostSpeedMultiplier(float slowFraction, float remainingSeconds)
        {
            if (float.IsNaN(slowFraction) || float.IsInfinity(slowFraction) ||
                float.IsNaN(remainingSeconds) || float.IsInfinity(remainingSeconds) || remainingSeconds <= 0f)
                return 1f;
            return 1f - Mathf.Clamp01(slowFraction);
        }

        public static float CalculateFrostAdjustedActionSeconds(float deltaSeconds, float slowFraction,
            float remainingSeconds)
        {
            if (float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds <= 0f) return 0f;
            var slowedSeconds = float.IsNaN(remainingSeconds) || float.IsInfinity(remainingSeconds)
                ? 0f
                : Mathf.Min(deltaSeconds, Mathf.Max(0f, remainingSeconds));
            var normalSeconds = deltaSeconds - slowedSeconds;
            return slowedSeconds * CalculateFrostSpeedMultiplier(slowFraction, slowedSeconds) + normalSeconds;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private void ResetGameSecondsSample()
        {
            hasGameSecondsSample = false;
            if (gameSecondsSource == null) return;
            var currentGameSeconds = gameSecondsSource.GameSeconds;
            if (float.IsNaN(currentGameSeconds) || float.IsInfinity(currentGameSeconds)) return;
            lastGameSeconds = currentGameSeconds;
            hasGameSecondsSample = true;
        }

        private void HandleDawnWarning() => BeginDawnFlee();
    }
}
