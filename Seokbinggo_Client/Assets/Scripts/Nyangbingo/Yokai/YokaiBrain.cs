using System.Collections.Generic;
using Nyangbingo.Combat;
using Nyangbingo.Bosses;
using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using Nyangbingo.Save;
using Nyangbingo.World;
using UnityEngine;

namespace Nyangbingo.Yokai
{
    public interface IYokaiTarget { Transform TargetTransform { get; } void DamageWall(float amount); }
    public interface IYokaiStealthTarget { bool IsHiddenFromAggro { get; } }
    public interface IYokaiInfiltrationTarget
    {
        bool TryRecordInfiltration(YokaiDefinition definition);
    }
    public interface IYokaiCombatTarget { bool TryApplyContactDamage(int amount); }
    public interface IYokaiLootTarget { bool TryStealGroundLoot(); bool TryStealInventory(int maxSlots, int maxAmount); }
    public interface IYokaiTheftReceiptSource { IReadOnlyList<ItemAmount> TakeStolenItems(); }
    public interface IWallMaterialTarget { YokaiWallMaterial WallMaterial { get; } }
    public interface IYokaiBarrierTarget
    {
        bool TryFindBlockingWall(Vector3 attackerPosition, Vector3 approachDirection,
            float searchRange,
            out Vector3Int wallCell, out YokaiWallMaterial material);
        bool TryDamageBlockingWall(Vector3Int wallCell, float amount);
    }

    [RequireComponent(typeof(Health))]
    public sealed class YokaiBrain : MonoBehaviour, IGameSecondsTickable
    {
        private const float ContactAttackIntervalGameSeconds = 1f;
        public const float WallAttackIntervalGameSeconds = 1f;
        private const float AttackRangeTolerance = .05f;
        public const float BossEncounterPausedAlphaMultiplier = .35f;
        public const float GaekgwiTelegraphSeconds = 1f;
        public const float GaekgwiDashDistanceTiles = 3f;
        public const float GaekgwiDashSpeedMultiplier = 2.5f;
        public const int GaekgwiDashAnimationFrameCount = 5;
        public const float GaekgwiCooldownSeconds = 12f;
        public const int GaekgwiWailDamage = 8;
        public const float GaekgwiWailKnockbackTiles = 1f;
        public const float GaekgwiWailHalfExtentTiles = 1.5f;
        private enum State { Approach, AttackWall, StealLoot, Retreat, DawnFlee }
        private enum GaekgwiPatternState { Cooldown, Telegraph, Dash }
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
        private bool wasInFrostBellRopeRange;
        private float frostBellReapplyCooldown;
        private bool bossEncounterPaused;
        private WorldMobPhysicsBody physicsBody;
        private RuntimeCharacterSpriteAnimator characterAnimator;
        private bool pausedCharacterAnimatorWasEnabled;
        private GaekgwiPatternState gaekgwiPatternState;
        private float gaekgwiCooldownRemaining;
        private float gaekgwiTelegraphRemaining;
        private float gaekgwiDashRemaining;
        private float gaekgwiDashElapsed;
        private Vector2 gaekgwiDashDirection;
        private GangcheoriBreathController gangcheoriBreath;
        private bool useAggroRadius;
        private bool isAggroed;
        private bool infiltrationRecorded;
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
        public bool UsesAggroRadius => useAggroRadius;
        public bool IsAggroed => isAggroed;
        public bool HasRecordedInfiltration => infiltrationRecorded;
        public float GaekgwiCooldownRemaining => gaekgwiCooldownRemaining;
        public float GaekgwiTelegraphRemaining => gaekgwiTelegraphRemaining;
        public float GaekgwiDashRemaining => gaekgwiDashRemaining;
        public float GaekgwiDashNormalizedTime
        {
            get
            {
                var duration = ResolveGaekgwiDashDuration();
                return duration > Mathf.Epsilon
                    ? Mathf.Clamp01(gaekgwiDashElapsed / duration)
                    : 1f;
            }
        }
        public bool IsGaekgwiPatternActive => gaekgwiPatternState != GaekgwiPatternState.Cooldown;
        public event System.Action Bloomed;
        public event System.Action Attacked;
        public event System.Action GaekgwiTelegraphStarted;
        public event System.Action GaekgwiDashStarted;
        public event System.Action GaekgwiWailTriggered;
        public event System.Action<YokaiDefinition> DawnFleeStarted;
        public event System.Action<YokaiDefinition> FledOffscreen;

        private void Awake()
        {
            health = GetComponent<Health>();
            physicsBody = GetComponent<WorldMobPhysicsBody>();
            characterAnimator = GetComponentInChildren<RuntimeCharacterSpriteAnimator>();
            gangcheoriBreath = GetComponent<GangcheoriBreathController>();
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
            IYokaiCounterSource counters = null, YokaiSpawnTrack instanceSpawnTrack = YokaiSpawnTrack.Raid,
            bool gateByAggroRadius = false, bool startEngaged = true, int? hitPointsOverride = null)
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
            ResetGaekgwiPattern();
            SetBossEncounterPaused(false);
            health = GetComponent<Health>();
            physicsBody = GetComponent<WorldMobPhysicsBody>();
            characterAnimator = GetComponentInChildren<RuntimeCharacterSpriteAnimator>();
            gangcheoriBreath = GetComponent<GangcheoriBreathController>();
            useAggroRadius = gateByAggroRadius;
            isAggroed = !gateByAggroRadius || startEngaged;
            infiltrationRecorded = false;
            if (health != null)
            {
                if (definition != null)
                    health.ConfigureForRuntime(hitPointsOverride ?? definition.HitPoints);
                health.SetDamageTakenMultiplier(1f);
            }
            ResetGameSecondsSample();
        }

        public void BindGangcheoriBreath(GangcheoriBreathController controller) =>
            gangcheoriBreath = controller;

        public void CaptureSaveState(YokaiStateRecord record)
        {
            if (record == null) throw new System.ArgumentNullException(nameof(record));
            record.behaviorState = (int)state;
            record.sieveStopRemaining = sieveStopRemaining;
            record.sieveCooldownRemaining = sieveCooldownRemaining;
            record.lanternPauseRemaining = lanternPauseRemaining;
            record.bloomCooldownRemaining = bloomCooldownRemaining;
            record.dawnFleeDirection = dawnFleeDirection;
            record.contactAttackRemaining = contactAttackRemaining;
            record.frostSlowFraction = frostSlowFraction;
            record.frostSlowRemaining = frostSlowRemaining;
            record.gaekgwiPatternInitialized = definition != null &&
                                               definition.Kind == YokaiKind.Gaekgwi;
            record.gaekgwiPatternState = (int)gaekgwiPatternState;
            record.gaekgwiCooldownRemaining = gaekgwiCooldownRemaining;
            record.gaekgwiTelegraphRemaining = gaekgwiTelegraphRemaining;
            record.gaekgwiDashRemaining = gaekgwiDashRemaining;
            record.gaekgwiDashDirection = gaekgwiDashDirection;
            record.hasAggroState = true;
            record.usesAggroRadius = useAggroRadius;
            record.isAggroed = isAggroed;
            record.infiltrationRecorded = infiltrationRecorded;
            record.stolenItems = GetComponent<YokaiLoot>()?.CaptureStolenItems() ??
                                 new List<InventorySlot>();
        }

        public bool RestoreSaveState(YokaiStateRecord record)
        {
            if (record == null || record.behaviorState < (int)State.Approach ||
                record.behaviorState > (int)State.DawnFlee ||
                !IsFinite(record.dawnFleeDirection) ||
                !IsFiniteNonNegative(record.sieveStopRemaining) ||
                !IsFiniteNonNegative(record.sieveCooldownRemaining) ||
                !IsFiniteNonNegative(record.lanternPauseRemaining) ||
                !IsFiniteNonNegative(record.bloomCooldownRemaining) ||
                !IsFiniteNonNegative(record.contactAttackRemaining) ||
                !IsFiniteNonNegative(record.frostSlowRemaining) ||
                float.IsNaN(record.frostSlowFraction) || float.IsInfinity(record.frostSlowFraction) ||
                record.frostSlowFraction < 0f || record.frostSlowFraction > 1f ||
                record.gaekgwiPatternInitialized &&
                (record.gaekgwiPatternState < (int)GaekgwiPatternState.Cooldown ||
                 record.gaekgwiPatternState > (int)GaekgwiPatternState.Dash ||
                 !IsFiniteNonNegative(record.gaekgwiCooldownRemaining) ||
                 !IsFiniteNonNegative(record.gaekgwiTelegraphRemaining) ||
                 !IsFiniteNonNegative(record.gaekgwiDashRemaining) ||
                 record.gaekgwiDashRemaining > GaekgwiDashDistanceTiles + .0001f ||
                 !IsFinite(record.gaekgwiDashDirection)))
                return false;

            state = (State)record.behaviorState;
            sieveStopRemaining = record.sieveStopRemaining;
            sieveCooldownRemaining = record.sieveCooldownRemaining;
            lanternPauseRemaining = record.lanternPauseRemaining;
            bloomCooldownRemaining = record.bloomCooldownRemaining;
            dawnFleeDirection = record.dawnFleeDirection;
            contactAttackRemaining = record.contactAttackRemaining;
            frostSlowFraction = record.frostSlowFraction;
            frostSlowRemaining = record.frostSlowRemaining;
            if (definition != null && definition.Kind == YokaiKind.Gaekgwi &&
                record.gaekgwiPatternInitialized)
            {
                gaekgwiPatternState = (GaekgwiPatternState)record.gaekgwiPatternState;
                gaekgwiCooldownRemaining = record.gaekgwiCooldownRemaining;
                gaekgwiTelegraphRemaining = record.gaekgwiTelegraphRemaining;
                gaekgwiDashRemaining = record.gaekgwiDashRemaining;
                gaekgwiDashDirection = record.gaekgwiDashDirection;
                gaekgwiDashElapsed = gaekgwiPatternState == GaekgwiPatternState.Dash
                    ? ResolveGaekgwiDashDuration() *
                      CalculateGaekgwiDashNormalizedTimeFromProgress(
                          1f - Mathf.Clamp01(
                              gaekgwiDashRemaining / GaekgwiDashDistanceTiles))
                    : 0f;
            }
            else ResetGaekgwiPattern();
            hasFledOffscreen = false;
            if (record.hasAggroState)
            {
                useAggroRadius = record.usesAggroRadius;
                isAggroed = record.isAggroed || !useAggroRadius;
                infiltrationRecorded = record.infiltrationRecorded;
            }
            SetBossEncounterPaused(false);
            SetAnimationMoving(state != State.AttackWall);
            if (definition != null && definition.Kind == YokaiKind.Gaekgwi)
            {
                if (gaekgwiPatternState == GaekgwiPatternState.Telegraph)
                    GaekgwiTelegraphStarted?.Invoke();
                else if (gaekgwiPatternState == GaekgwiPatternState.Dash)
                    GaekgwiDashStarted?.Invoke();
            }
            ResetGameSecondsSample();
            return true;
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
            var theftFlee = state == State.Retreat &&
                             definition?.Kind == YokaiKind.Yagwanggwi;
            if (state != State.DawnFlee && !theftFlee ||
                isVisible || hasFledOffscreen) return false;
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
                if (characterAnimator == null)
                    characterAnimator = GetComponentInChildren<RuntimeCharacterSpriteAnimator>();
                pausedCharacterAnimatorWasEnabled =
                    characterAnimator != null && characterAnimator.enabled;
                if (characterAnimator != null) characterAnimator.enabled = false;
                pausedRenderers = GetComponentsInChildren<SpriteRenderer>(true);
                pausedRendererColors = new Color[pausedRenderers.Length];
                for (var index = 0; index < pausedRenderers.Length; index++)
                {
                    var renderer = pausedRenderers[index];
                    if (renderer == null) continue;
                    pausedRendererColors[index] = renderer.color;
                    var color = renderer.color;
                    color.a *= BossEncounterPausedAlphaMultiplier;
                    renderer.color = color;
                }
            }
            else
            {
                for (var index = 0; index < pausedRenderers.Length && index < pausedRendererColors.Length; index++)
                    if (pausedRenderers[index] != null) pausedRenderers[index].color = pausedRendererColors[index];
                pausedRenderers = System.Array.Empty<SpriteRenderer>();
                pausedRendererColors = System.Array.Empty<Color>();
                if (characterAnimator != null)
                    characterAnimator.enabled = pausedCharacterAnimatorWasEnabled;
                pausedCharacterAnimatorWasEnabled = false;
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
            if (state != State.DawnFlee &&
                (state != State.Retreat || definition?.Kind != YokaiKind.Yagwanggwi))
                return;
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
            if (counterSource is CounterAuraSensor auraSensor)
                auraSensor.TickFrostBellRope(
                    this, ref wasInFrostBellRopeRange, ref frostBellReapplyCooldown, deltaSeconds);
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
                MoveRetreat(dawnFleeDirection, actionSeconds, true);
                return;
            }
            if (target == null || target.TargetTransform == null) return;
            if (useAggroRadius && !isAggroed)
            {
                if (target is IYokaiStealthTarget stealth && stealth.IsHiddenFromAggro) return;
                var detectionOffset = target.TargetTransform.position - transform.position;
                if (!IsWithinAggroRadius(detectionOffset, definition.AggroRadius)) return;
                isAggroed = true;
            }
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
            if (definition.Kind == YokaiKind.Gangcheori &&
                gangcheoriBreath != null &&
                gangcheoriBreath.Tick(actionSeconds))
                return;
            if (definition.Kind == YokaiKind.Gaekgwi && TickGaekgwiPattern(actionSeconds))
                return;
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
            var barrierTarget = target as IYokaiBarrierTarget;
            var blockingWallCell = default(Vector3Int);
            var blockingWallMaterial = YokaiWallMaterial.Default;
            var wallApproachDirection = direction.sqrMagnitude > Mathf.Epsilon
                ? direction
                : targetOffset;
            // The selected route is authoritative even when a two-cell player wall makes
            // the grounded path graph look like a transition to another floor. Natural
            // terrain is excluded by the barrier target. A zero-DPS yokai must keep routing
            // instead of entering AttackWall forever.
            var foundBlockingWall = barrierTarget != null &&
                                    barrierTarget.TryFindBlockingWall(
                                        currentPosition, wallApproachDirection, attackRange,
                                        out blockingWallCell, out blockingWallMaterial);
            var blockingWallDamage = foundBlockingWall
                ? definition.WallDamageFor(blockingWallMaterial)
                : 0f;
            var hasBlockingWall = blockingWallDamage > 0f &&
                                  !float.IsNaN(blockingWallDamage) &&
                                  !float.IsInfinity(blockingWallDamage);
            contactAttackRemaining = Mathf.Max(0f, contactAttackRemaining - actionSeconds);
            switch (state)
            {
                case State.Approach:
                    if (YokaiSpecialRules.ShouldAttemptTheft(definition.Kind, counters)) state = State.StealLoot;
                    else if (hasBlockingWall) state = State.AttackWall;
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
                        if (stoleLoot && lootTarget is IYokaiTheftReceiptSource receiptSource)
                            GetComponent<YokaiLoot>()?.RecordStolenItems(
                                receiptSource.TakeStolenItems());
                        state = stoleLoot ? State.Retreat : State.Approach;
                    }
                    break;
                case State.AttackWall:
                    if (hasBlockingWall)
                    {
                        if (contactAttackRemaining <= .0001f)
                        {
                            var damage = blockingWallDamage * WallAttackIntervalGameSeconds;
                            if (!float.IsNaN(damage) && !float.IsInfinity(damage) &&
                                barrierTarget.TryDamageBlockingWall(blockingWallCell, damage))
                            {
                                contactAttackRemaining = WallAttackIntervalGameSeconds;
                                Attacked?.Invoke();
                                GameEvents.RaiseWallDamaged();
                            }
                        }
                        break;
                    }
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
                        var contactDamage = YokaiSpecialRules.ContactDamage(
                            definition, counters?.IsInLanternRange == true);
                        if (contactAttackRemaining <= .0001f && contactDamage > 0 &&
                            combatTarget.TryApplyContactDamage(contactDamage))
                        {
                            contactAttackRemaining = ContactAttackIntervalGameSeconds;
                            Attacked?.Invoke();
                        }
                        break;
                    }
                    if (!infiltrationRecorded && target is IYokaiInfiltrationTarget infiltration &&
                        infiltration.TryRecordInfiltration(definition))
                    {
                        infiltrationRecorded = true;
                        BeginRetreat();
                        break;
                    }
                    var wall = target as IWallMaterialTarget;
                    var legacyWallDamagePerSecond = definition.WallDamageFor(
                        wall?.WallMaterial ?? YokaiWallMaterial.Default);
                    if (legacyWallDamagePerSecond > 0f && !float.IsNaN(legacyWallDamagePerSecond) &&
                        !float.IsInfinity(legacyWallDamagePerSecond) &&
                        contactAttackRemaining <= .0001f)
                    {
                        var damage = legacyWallDamagePerSecond * WallAttackIntervalGameSeconds;
                        if (!float.IsNaN(damage) && !float.IsInfinity(damage))
                        {
                            target.DamageWall(damage);
                            contactAttackRemaining = WallAttackIntervalGameSeconds;
                            Attacked?.Invoke();
                            GameEvents.RaiseWallDamaged();
                        }
                    }
                    break;
                case State.Retreat:
                    MoveRetreat(-direction, actionSeconds, false);
                    break;
            }
        }

        private void MoveRetreat(Vector3 direction, float actionSeconds, bool dawnFlee)
        {
            var moveSpeed = definition.MoveSpeed;
            var retreatMultiplier = dawnFlee
                ? .5f
                : definition.Kind == YokaiKind.Yagwanggwi
                    ? 1f
                    : retreatSpeedMultiplier;
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
            // Straight-line range alone is not enough when the target is on another floor.
            // Without a clear line, keep following the platform route even when the vertical
            // separation happens to be shorter than the nominal contact range.
            var hasClearAttackLine = physicsBody == null ||
                                     physicsBody.HasClearAttackLine(
                                         target.TargetTransform.position);
            var maximumDistance = hasClearAttackLine
                ? Mathf.Max(0f, distance - attackRange)
                : travelDistance;
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

        public static bool IsWithinAggroRadius(Vector3 targetOffset, float radius)
        {
            if (!IsFinite(targetOffset) || radius <= 0f || float.IsNaN(radius) ||
                float.IsInfinity(radius)) return false;
            return targetOffset.sqrMagnitude <= radius * radius;
        }

        private float MoveBy(Vector3 displacement)
        {
            var movedDistance = physicsBody != null ? physicsBody.Move(displacement) : displacement.magnitude;
            if (physicsBody == null) transform.position += displacement;
            if (movedDistance > Mathf.Epsilon)
            {
                if (characterAnimator == null)
                    characterAnimator = GetComponentInChildren<RuntimeCharacterSpriteAnimator>();
                var routeFacing = physicsBody != null
                    ? physicsBody.NavigationFacingDirection
                    : Vector2.zero;
                var facingMovement = Mathf.Abs(routeFacing.x) > Mathf.Epsilon
                    ? (Vector3)routeFacing
                    : displacement;
                if (Mathf.Abs(facingMovement.x) > .005f)
                    characterAnimator?.SetFacing(facingMovement);
                characterAnimator?.SetMoving(true);
            }
            return movedDistance;
        }

        private bool TickGaekgwiPattern(float actionSeconds)
        {
            var remaining = Mathf.Max(0f, actionSeconds);
            if (gaekgwiPatternState == GaekgwiPatternState.Cooldown)
            {
                if (gaekgwiCooldownRemaining > 0f)
                {
                    gaekgwiCooldownRemaining = Mathf.Max(0f, gaekgwiCooldownRemaining - remaining);
                    return false;
                }
                BeginGaekgwiTelegraph();
            }

            if (gaekgwiPatternState == GaekgwiPatternState.Telegraph)
            {
                var elapsed = Mathf.Min(gaekgwiTelegraphRemaining, remaining);
                gaekgwiTelegraphRemaining = Mathf.Max(0f, gaekgwiTelegraphRemaining - elapsed);
                remaining = Mathf.Max(0f, remaining - elapsed);
                if (gaekgwiTelegraphRemaining > .0001f) return true;
                BeginGaekgwiDash();
            }

            if (gaekgwiPatternState != GaekgwiPatternState.Dash) return true;
            var dashDuration = ResolveGaekgwiDashDuration();
            if (dashDuration <= Mathf.Epsilon)
            {
                TriggerGaekgwiWail();
                return true;
            }

            var previousNormalizedTime = Mathf.Clamp01(gaekgwiDashElapsed / dashDuration);
            gaekgwiDashElapsed = Mathf.Min(
                dashDuration, gaekgwiDashElapsed + remaining);
            var normalizedTime = Mathf.Clamp01(gaekgwiDashElapsed / dashDuration);
            var previousProgress =
                CalculateGaekgwiDashProgress(previousNormalizedTime);
            var progress = CalculateGaekgwiDashProgress(normalizedTime);
            var requestedDistance = Mathf.Min(
                gaekgwiDashRemaining,
                GaekgwiDashDistanceTiles * Mathf.Max(0f, progress - previousProgress));
            if (requestedDistance > .0001f)
            {
                var movedDistance = MoveBy(gaekgwiDashDirection * requestedDistance);
                gaekgwiDashRemaining =
                    Mathf.Max(0f, gaekgwiDashRemaining - movedDistance);
                if (movedDistance + .0001f < requestedDistance)
                {
                    TriggerGaekgwiWail();
                    return true;
                }
            }
            if (gaekgwiDashElapsed >= dashDuration - .0001f)
                TriggerGaekgwiWail();
            return true;
        }

        private void BeginGaekgwiTelegraph()
        {
            var targetOffset = target.TargetTransform.position - transform.position;
            var horizontal = Mathf.Abs(targetOffset.x) > .0001f ? Mathf.Sign(targetOffset.x) : 1f;
            gaekgwiDashDirection = new Vector2(horizontal, 0f);
            gaekgwiPatternState = GaekgwiPatternState.Telegraph;
            gaekgwiCooldownRemaining = 0f;
            gaekgwiTelegraphRemaining = GaekgwiTelegraphSeconds;
            gaekgwiDashRemaining = CalculateGaekgwiDashDistance();
            gaekgwiDashElapsed = 0f;
            characterAnimator?.SetFacing(gaekgwiDashDirection);
            GaekgwiTelegraphStarted?.Invoke();
        }

        private void BeginGaekgwiDash()
        {
            gaekgwiPatternState = GaekgwiPatternState.Dash;
            gaekgwiTelegraphRemaining = 0f;
            characterAnimator?.SetFacing(gaekgwiDashDirection);
            GaekgwiDashStarted?.Invoke();
            if (gaekgwiDashRemaining <= .0001f) TriggerGaekgwiWail();
        }

        private void TriggerGaekgwiWail()
        {
            gaekgwiPatternState = GaekgwiPatternState.Cooldown;
            gaekgwiCooldownRemaining = GaekgwiCooldownSeconds;
            gaekgwiTelegraphRemaining = 0f;
            gaekgwiDashRemaining = 0f;
            gaekgwiDashElapsed = 0f;
            GaekgwiWailTriggered?.Invoke();
            Attacked?.Invoke();

            var offset = target.TargetTransform.position - transform.position;
            if (Mathf.Abs(offset.x) > GaekgwiWailHalfExtentTiles ||
                Mathf.Abs(offset.y) > GaekgwiWailHalfExtentTiles ||
                target is not IBossCombatTarget combatTarget) return;
            var horizontal = Mathf.Abs(offset.x) > .0001f
                ? Mathf.Sign(offset.x)
                : gaekgwiDashDirection.x;
            combatTarget.TryApplyBossSpecialDamage(GaekgwiWailDamage, DamageTag.Ice,
                new Vector2(horizontal * GaekgwiWailKnockbackTiles, 0f));
        }

        private void ResetGaekgwiPattern()
        {
            gaekgwiPatternState = GaekgwiPatternState.Cooldown;
            gaekgwiCooldownRemaining = GaekgwiCooldownSeconds;
            gaekgwiTelegraphRemaining = 0f;
            gaekgwiDashRemaining = 0f;
            gaekgwiDashElapsed = 0f;
            gaekgwiDashDirection = Vector2.right;
        }

        public static float CalculateGaekgwiDashDistance() => GaekgwiDashDistanceTiles;

        public static float CalculateGaekgwiDashProgress(float normalizedTime)
        {
            // Delivered 96 px canvas frame centers:
            // 15, 45, 71.5, 80, 83.5 px. Normalize their 68.5 px displacement
            // so the runtime root follows the same ease-out curve over exactly 3 tiles.
            var time = Mathf.Clamp01(normalizedTime);
            var scaled = time * GaekgwiDashAnimationFrameCount;
            var frame = Mathf.Min(
                Mathf.FloorToInt(scaled),
                GaekgwiDashAnimationFrameCount - 1);
            var frameFraction = scaled - frame;
            var start = GaekgwiDashFrameProgress(frame);
            var end = frame >= GaekgwiDashAnimationFrameCount - 1
                ? 1f
                : GaekgwiDashFrameProgress(frame + 1);
            return Mathf.Lerp(start, end, frameFraction);
        }

        private static float GaekgwiDashFrameProgress(int frame)
        {
            return frame switch
            {
                <= 0 => 0f,
                1 => 30f / 68.5f,
                2 => 56.5f / 68.5f,
                3 => 65f / 68.5f,
                _ => 1f
            };
        }

        private static float CalculateGaekgwiDashNormalizedTimeFromProgress(float progress)
        {
            var clamped = Mathf.Clamp01(progress);
            for (var frame = 0; frame < GaekgwiDashAnimationFrameCount; frame++)
            {
                var start = GaekgwiDashFrameProgress(frame);
                var end = frame >= GaekgwiDashAnimationFrameCount - 1
                    ? 1f
                    : GaekgwiDashFrameProgress(frame + 1);
                if (clamped > end && frame < GaekgwiDashAnimationFrameCount - 1)
                    continue;
                var fraction = end > start + Mathf.Epsilon
                    ? Mathf.InverseLerp(start, end, clamped)
                    : 0f;
                return (frame + fraction) / GaekgwiDashAnimationFrameCount;
            }
            return 1f;
        }

        private float ResolveGaekgwiDashDuration()
        {
            var speed = definition != null
                ? definition.MoveSpeed * GaekgwiDashSpeedMultiplier
                : 0f;
            return speed > Mathf.Epsilon && !float.IsNaN(speed) && !float.IsInfinity(speed)
                ? GaekgwiDashDistanceTiles / speed
                : 0f;
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

        private static bool IsFiniteNonNegative(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;

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

    /// <summary>
    /// v34 moved Gangcheori from the boss track to a resident elite. Its reduced breath
    /// therefore belongs to the yokai runtime rather than the boss-only combat controller.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class GangcheoriBreathController : MonoBehaviour
    {
        public const float TelegraphSeconds = 1.5f;
        public const float RangeTiles = 3f;
        public const float ArcDegrees = 60f;
        public const int Damage = 18;
        public const float KnockbackTiles = 2f;
        public const float CooldownSeconds = 12f;
        private const float EffectFrameSeconds = .1f;
        private const float RangeTolerance = .05f;

        private YokaiDefinition definition;
        private Transform targetTransform;
        private IBossCombatTarget combatTarget;
        private SpriteRenderer headRenderer;
        private RuntimeCharacterSpriteAnimator characterAnimator;
        private WorldMobPhysicsBody physicsBody;
        private Vector2 lockedAim = Vector2.left;
        private Vector2 lockedOrigin;
        private float cooldownRemaining;
        private float telegraphRemaining;
        private float effectRemaining;
        private bool telegraphing;
        private Mesh telegraphMesh;
        private MeshRenderer telegraphRenderer;
        private Material telegraphMaterial;
        private Transform effectTransform;
        private SpriteRenderer effectRenderer;
        private RuntimeBuildingSpriteAnimator effectAnimator;
        private System.Collections.Generic.IReadOnlyList<Sprite> effectFrames;
        private Vector2 effectMaximumSize;

        public bool IsTelegraphing => telegraphing;
        public float CooldownRemaining => cooldownRemaining;
        public static Vector2 EffectWorldSize => new Vector2(
            RangeTiles,
            RangeTiles * 2f * Mathf.Tan(ArcDegrees * .5f * Mathf.Deg2Rad));

        public bool ConfigureForRuntime(YokaiDefinition value, MonoBehaviour target,
            GameplayArtCatalog artCatalog, SpriteRenderer head)
        {
            definition = value;
            targetTransform = target != null ? target.transform : null;
            combatTarget = target as IBossCombatTarget;
            headRenderer = head;
            characterAnimator = head != null
                ? head.GetComponent<RuntimeCharacterSpriteAnimator>()
                : null;
            physicsBody = GetComponent<WorldMobPhysicsBody>();
            if (definition == null || definition.Kind != YokaiKind.Gangcheori ||
                targetTransform == null || combatTarget == null || headRenderer == null)
                return false;

            cooldownRemaining = 0f;
            telegraphRemaining = 0f;
            effectRemaining = 0f;
            telegraphing = false;
            CreateTelegraph();
            CreateEffect(artCatalog?.GangcheoriSpecialFireFrames);
            return true;
        }

        /// <summary>
        /// Returns true while the breath owns the elite's action and normal pursuit must pause.
        /// </summary>
        public bool Tick(float deltaGameSeconds)
        {
            if (!IsFinite(deltaGameSeconds) || deltaGameSeconds < 0f ||
                definition == null || targetTransform == null || combatTarget == null)
                return false;
            var health = GetComponent<Health>();
            if (health == null || health.IsDead) return false;

            TickEffect(deltaGameSeconds);
            if (telegraphing)
            {
                telegraphRemaining = Mathf.Max(0f, telegraphRemaining - deltaGameSeconds);
                RefreshTelegraph();
                if (telegraphRemaining <= .0001f) Fire();
                return true;
            }

            cooldownRemaining = Mathf.Max(0f, cooldownRemaining - deltaGameSeconds);
            if (cooldownRemaining > .0001f) return false;
            var aim = (Vector2)(targetTransform.position - transform.position);
            if (!IsFinite(aim) || aim.sqrMagnitude <= Mathf.Epsilon) return false;
            aim.Normalize();
            var origin = ResolveBreathOrigin(aim);
            var targetOffset = (Vector2)targetTransform.position - origin;
            if (!IsInsideBreathCone(targetOffset, aim) ||
                physicsBody != null && !physicsBody.HasClearAttackLine(targetTransform.position))
                return false;

            BeginTelegraph(origin, aim);
            return true;
        }

        public static bool IsInsideBreathCone(Vector2 offset, Vector2 aim)
        {
            if (!IsFinite(offset) || !IsFinite(aim) || aim.sqrMagnitude <= Mathf.Epsilon)
                return false;
            var distance = offset.magnitude;
            if (distance > RangeTiles + RangeTolerance) return false;
            if (distance <= Mathf.Epsilon) return true;
            return Vector2.Angle(aim, offset) <= ArcDegrees * .5f + RangeTolerance;
        }

        private void BeginTelegraph(Vector2 origin, Vector2 aim)
        {
            lockedOrigin = origin;
            lockedAim = aim.sqrMagnitude > Mathf.Epsilon ? aim.normalized : Vector2.left;
            telegraphRemaining = TelegraphSeconds;
            telegraphing = true;
            characterAnimator?.SetFacing(lockedAim);
            if (telegraphRenderer != null) telegraphRenderer.enabled = true;
            RefreshTelegraph();
        }

        private void Fire()
        {
            telegraphing = false;
            telegraphRemaining = 0f;
            cooldownRemaining = CooldownSeconds;
            if (telegraphRenderer != null) telegraphRenderer.enabled = false;
            characterAnimator?.PlayAttack();
            PlayEffect();

            var targetOffset = (Vector2)targetTransform.position - lockedOrigin;
            if (!IsInsideBreathCone(targetOffset, lockedAim)) return;
            combatTarget.TryApplyBossSpecialDamage(
                Damage,
                DamageTag.Fire,
                lockedAim * KnockbackTiles);
        }

        private Vector2 ResolveBreathOrigin(Vector2 aim)
        {
            if (headRenderer == null || headRenderer.sprite == null)
                return transform.position;
            var bounds = headRenderer.bounds;
            return bounds.center + new Vector3(
                aim.x * bounds.extents.x,
                aim.y * bounds.extents.y,
                0f);
        }

        private void CreateEffect(System.Collections.Generic.IReadOnlyList<Sprite> frames)
        {
            effectFrames = frames;
            if (frames == null || frames.Count == 0) return;
            var effectObject = new GameObject("GangcheoriSpecialFire");
            effectObject.transform.SetParent(transform, false);
            effectTransform = effectObject.transform;
            effectRenderer = effectObject.AddComponent<SpriteRenderer>();
            effectRenderer.sortingOrder = 16;
            effectAnimator = effectObject.AddComponent<RuntimeBuildingSpriteAnimator>();
            effectAnimator.Configure(frames, EffectFrameSeconds);
            effectMaximumSize = Vector2.zero;
            for (var index = 0; index < frames.Count; index++)
            {
                var frame = frames[index];
                if (frame == null) continue;
                effectMaximumSize.x =
                    Mathf.Max(effectMaximumSize.x, frame.bounds.size.x);
                effectMaximumSize.y =
                    Mathf.Max(effectMaximumSize.y, frame.bounds.size.y);
            }
            effectRenderer.enabled = false;
        }

        private void PlayEffect()
        {
            if (effectRenderer == null || effectTransform == null ||
                effectFrames == null || effectFrames.Count == 0) return;
            effectAnimator.Configure(effectFrames, EffectFrameSeconds);
            effectRemaining = effectFrames.Count * EffectFrameSeconds;
            effectRenderer.enabled = true;
            RefreshEffectVisual();
        }

        private void TickEffect(float deltaGameSeconds)
        {
            if (effectRemaining <= .0001f) return;
            RefreshEffectVisual();
            effectRemaining = Mathf.Max(0f, effectRemaining - deltaGameSeconds);
            if (effectRemaining <= .0001f && effectRenderer != null)
                effectRenderer.enabled = false;
        }

        private void RefreshEffectVisual()
        {
            if (effectTransform == null || effectRenderer == null ||
                effectRenderer.sprite == null) return;

            var aim = lockedAim.sqrMagnitude > Mathf.Epsilon
                ? lockedAim.normalized
                : Vector2.left;
            var desiredSize = EffectWorldSize;
            var rootScale = transform.lossyScale;
            effectTransform.localScale = new Vector3(
                desiredSize.x / Mathf.Max(effectMaximumSize.x, Mathf.Epsilon) /
                Mathf.Max(Mathf.Abs(rootScale.x), Mathf.Epsilon),
                desiredSize.y / Mathf.Max(effectMaximumSize.y, Mathf.Epsilon) /
                Mathf.Max(Mathf.Abs(rootScale.y), Mathf.Epsilon),
                1f);
            effectTransform.rotation = Quaternion.Euler(
                0f, 0f, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);

            // The delivered Aseprite frames use an off-canvas bottom pivot. Place the
            // rendered bounds, not that pivot, over the same 3-tile cone as the hit test.
            var desiredCenter = lockedOrigin + aim * (RangeTiles * .5f);
            var renderedCenterOffset = (Vector2)
                effectTransform.TransformVector(effectRenderer.sprite.bounds.center);
            effectTransform.position = desiredCenter - renderedCenterOffset;
        }

        private void CreateTelegraph()
        {
            var telegraphObject = new GameObject("GangcheoriBreathTelegraph");
            telegraphObject.transform.SetParent(transform, false);
            var filter = telegraphObject.AddComponent<MeshFilter>();
            telegraphRenderer = telegraphObject.AddComponent<MeshRenderer>();
            telegraphMesh = new Mesh { name = "GangcheoriBreathTelegraphMesh" };
            filter.sharedMesh = telegraphMesh;
            BuildConeMesh();
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null)
            {
                telegraphMaterial = new Material(shader)
                {
                    color = new Color(1f, .2f, .05f, .3f),
                    hideFlags = HideFlags.HideAndDontSave
                };
                telegraphRenderer.sharedMaterial = telegraphMaterial;
            }
            telegraphRenderer.sortingOrder = 14;
            telegraphRenderer.enabled = false;
        }

        private void BuildConeMesh()
        {
            const int segments = 16;
            var vertices = new Vector3[segments + 2];
            var triangles = new int[segments * 3];
            vertices[0] = Vector3.zero;
            for (var index = 0; index <= segments; index++)
            {
                var angle = Mathf.Lerp(-ArcDegrees * .5f, ArcDegrees * .5f,
                    index / (float)segments) * Mathf.Deg2Rad;
                vertices[index + 1] =
                    new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * RangeTiles;
                if (index >= segments) continue;
                var triangle = index * 3;
                triangles[triangle] = 0;
                triangles[triangle + 1] = index + 1;
                triangles[triangle + 2] = index + 2;
            }
            telegraphMesh.Clear();
            telegraphMesh.vertices = vertices;
            telegraphMesh.triangles = triangles;
            telegraphMesh.RecalculateBounds();
        }

        private void RefreshTelegraph()
        {
            if (telegraphRenderer == null) return;
            telegraphRenderer.transform.position = lockedOrigin;
            telegraphRenderer.transform.rotation = Quaternion.Euler(
                0f, 0f, Mathf.Atan2(lockedAim.y, lockedAim.x) * Mathf.Rad2Deg);
        }

        private void OnDestroy()
        {
            if (telegraphMesh != null) Destroy(telegraphMesh);
            if (telegraphMaterial != null) Destroy(telegraphMaterial);
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsFinite(Vector2 value) =>
            IsFinite(value.x) && IsFinite(value.y);
    }
}
