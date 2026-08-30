using Nyangbingo.Combat;
using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.World;
using Nyangbingo.Yokai;
using UnityEngine;

namespace Nyangbingo.Bosses
{
    public interface IBossCombatTarget : IYokaiCombatTarget
    {
        bool TryApplyBossSpecialDamage(int amount, DamageTag tag, Vector2 knockback,
            DamageDelivery delivery = DamageDelivery.Direct,
            bool showFireHitEffect = true);
    }

    [RequireComponent(typeof(Health))]
    public sealed class BossCombatController : MonoBehaviour, IGameSecondsTickable
    {
        private const float ContactAttackIntervalGameSeconds = 1f;
        private const float ContactRange = 1.05f;
        private const float GoblinChiefContactRange = ContactRange * 2f;
        private const float MotherBulgasariContactRange = ContactRange * 2f;
        private const float MoveSpeedTilesPerGameSecond = 1.25f;
        private const float RangeTolerance = .05f;
        private const float MotherBulgasariNoseForwardRatio = .85f;
        private const float MotherBulgasariNoseHeightRatio = .32f;
        private const float WarningArtGap = .3f;
        private const float GoblinChiefAttackForwardRatio = 1.15f;
        private const float GoblinChiefAttackHeightRatio = .42f;
        private const float GangcheoriBreathForwardRatio = 1f;
        private const float GangcheoriBreathHeightRatio = .5f;
        private const float GangcheoriFireVerticalOffset = -.25f;
        private const float ImugiElectricForwardRatio = 1f;
        private const float ImugiElectricHeightRatio = .5f;
        private const float GoblinChiefWarningGap = .75f;
        private const float GoblinChiefThrowClearance = .75f;
        private const float GoblinChiefSpecialAnimationLeadSeconds = .35f;
        private const int GoblinChiefSpecialImpactFrameIndex = 3;
        private const float MotherBulgasariSpecialAttackLeadSeconds = .1f;
        private const int MotherBulgasariSpecialImpactFrameIndex = 1;
        private const float ImugiLandingPhaseHealthRatio = .66f;
        private const float ImugiLakePhaseHealthRatio = .33f;
        private const float ImugiLandingHalfExtentTiles = 1.5f;
        private const int ImugiPhaseDamage = 8;
        private const float ImugiLandingKnockbackTiles = 2f;
        private const int ImugiLakePulseCount = 2;
        private const float ImugiLakePulseIntervalSeconds = .5f;
        private const float SpecialEffectFrameSeconds = .1f;
        private static readonly Vector2 GoblinChiefWarningWorldSize = new Vector2(1.8f, 1.1f);

        private enum ImugiPhaseAttack
        {
            None,
            LandingDischarge,
            LakePulse
        }

        [SerializeField] private BossDefinition definition;
        [SerializeField] private MonoBehaviour targetComponent;

        private Transform targetTransform;
        private IBossCombatTarget combatTarget;
        private MeshRenderer telegraphRenderer;
        private MeshFilter telegraphFilter;
        private Mesh telegraphMesh;
        private Material telegraphMaterial;
        private SpriteRenderer warningRenderer;
        private Transform warningTransform;
        private Vector2 warningMaximumSpriteSize;
        private SpriteRenderer specialEffectRenderer;
        private Transform specialEffectTransform;
        private RuntimeBuildingSpriteAnimator specialEffectAnimator;
        private System.Collections.Generic.IReadOnlyList<Sprite> specialEffectFrames;
        private float specialEffectWorldLength;
        private Vector2 specialEffectMaximumWorldSize;
        private float specialEffectPlaybackRemaining;
        private Health health;
        private Vector2 lockedAim = Vector2.down;
        private Vector2 specialOriginLocalOffset;
        private float contactAttackRemaining;
        private float specialCooldownRemaining;
        private float telegraphRemaining;
        private float activeSpecialRemaining;
        private float specialTickRemaining;
        private bool telegraphing;
        private bool specialActive;
        private bool specialAnimationStarted;
        private bool specialTickAnimationStarted;
        private bool specialDefenseApplied;
        private bool imugiLandingPhaseTriggered;
        private bool imugiLakePhaseTriggered;
        private ImugiPhaseAttack imugiPhaseAttack;
        private int imugiLakePulsesRemaining;
        private float openingDodgeRemaining;
        private WorldMobPhysicsBody physicsBody;
        private RuntimeCharacterSpriteAnimator characterAnimator;

        public BossDefinition Definition => definition;
        public bool IsTelegraphing => telegraphing;
        public bool IsSpecialActive => specialActive;
        public float SpecialCooldownRemaining => specialCooldownRemaining;
        public event System.Action Attacked;
        public event System.Action SpecialAnimationStarted;
        public event System.Action SpecialStarted;

        public void ConfigureWarningArt(GameplayArtCatalog artCatalog)
        {
            ConfigureBossSpecialEffect(artCatalog);
            var frames = artCatalog?.BossWarningFrames;
            if (frames == null || frames.Count == 0) return;
            var warning = new GameObject("SpecialWarningArt");
            warning.transform.SetParent(transform, false);
            warning.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            warningTransform = warning.transform;
            warningRenderer = warning.AddComponent<SpriteRenderer>();
            warningRenderer.sortingOrder = 16;
            warning.AddComponent<RuntimeBuildingSpriteAnimator>().Configure(frames);
            warningMaximumSpriteSize = Vector2.zero;
            for (var index = 0; index < frames.Count; index++)
            {
                var frame = frames[index];
                if (frame == null) continue;
                warningMaximumSpriteSize.x =
                    Mathf.Max(warningMaximumSpriteSize.x, frame.bounds.size.x);
                warningMaximumSpriteSize.y =
                    Mathf.Max(warningMaximumSpriteSize.y, frame.bounds.size.y);
            }
            warningRenderer.enabled = telegraphing || specialActive;
        }

        private void ConfigureBossSpecialEffect(GameplayArtCatalog artCatalog)
        {
            if (definition == null ||
                (definition.Kind != BossKind.Gangcheori && definition.Kind != BossKind.Imugi)) return;
            var frames = definition.Kind == BossKind.Imugi
                ? artCatalog?.ImugiElectricAttackFrames
                : artCatalog?.GangcheoriSpecialFireFrames;
            if (frames == null || frames.Count == 0) return;
            specialEffectFrames = frames;
            var effect = new GameObject(
                definition.Kind == BossKind.Imugi
                    ? "ImugiElectricAttack"
                    : "GangcheoriSpecialFire");
            effect.transform.SetParent(transform, false);
            specialEffectTransform = effect.transform;
            specialEffectRenderer = effect.AddComponent<SpriteRenderer>();
            specialEffectRenderer.sortingOrder = 16;
            specialEffectAnimator = effect.AddComponent<RuntimeBuildingSpriteAnimator>();
            specialEffectAnimator.Configure(frames, SpecialEffectFrameSeconds);
            specialEffectWorldLength = 0f;
            specialEffectMaximumWorldSize = Vector2.zero;
            for (var index = 0; index < frames.Count; index++)
            {
                var frame = frames[index];
                if (frame == null) continue;
                specialEffectWorldLength =
                    Mathf.Max(specialEffectWorldLength, frame.bounds.size.x);
                specialEffectMaximumWorldSize.x =
                    Mathf.Max(specialEffectMaximumWorldSize.x, frame.bounds.size.x);
                specialEffectMaximumWorldSize.y =
                    Mathf.Max(specialEffectMaximumWorldSize.y, frame.bounds.size.y);
            }
            specialEffectRenderer.enabled = false;
            specialEffectPlaybackRemaining = 0f;
        }

        public void BindCharacterAnimator(RuntimeCharacterSpriteAnimator animator)
        {
            characterAnimator = animator;
            RefreshWarningPosition();
        }

        public bool ConfigureForRuntime(BossDefinition value, MonoBehaviour target, GameDataCatalog catalog = null)
        {
            definition = value;
            targetComponent = target;
            targetTransform = target != null ? target.transform : null;
            combatTarget = target as IBossCombatTarget;
            health = GetComponent<Health>();
            physicsBody = GetComponent<WorldMobPhysicsBody>();
            characterAnimator = GetComponentInChildren<RuntimeCharacterSpriteAnimator>();
            if (definition == null || targetTransform == null || combatTarget == null || health == null)
                return false;

            openingDodgeRemaining = 0f;
            health.SetDamageTakenMultiplier(1f);
            if (catalog != null &&
                BossDodgeRules.TryGetOpeningDodgeSeconds(catalog, definition.Id, out var dodgeSeconds))
            {
                openingDodgeRemaining = dodgeSeconds;
                health.SetDamageTakenMultiplier(0f);
            }

            contactAttackRemaining = 0f;
            specialCooldownRemaining = Mathf.Max(0f, definition.SpecialCooldownSeconds);
            telegraphRemaining = 0f;
            activeSpecialRemaining = 0f;
            specialTickRemaining = 0f;
            telegraphing = false;
            specialActive = false;
            specialAnimationStarted = false;
            specialTickAnimationStarted = false;
            imugiLandingPhaseTriggered = false;
            imugiLakePhaseTriggered = false;
            imugiPhaseAttack = ImugiPhaseAttack.None;
            imugiLakePulsesRemaining = 0;
            specialEffectPlaybackRemaining = 0f;
            EnsureTelegraphRenderer();
            SetTelegraphVisible(false);
            return true;
        }

        public void Tick(float deltaGameSeconds)
        {
            SetAnimationMoving(false);
            if (!IsFinite(deltaGameSeconds) || deltaGameSeconds < 0f || definition == null ||
                targetTransform == null || combatTarget == null || health == null || health.IsDead) return;
            TickOpeningDodge(deltaGameSeconds);
            TickSpecialEffect(deltaGameSeconds);

            contactAttackRemaining = Mathf.Max(0f, contactAttackRemaining - deltaGameSeconds);
            if (specialActive)
            {
                TickActiveSpecial(deltaGameSeconds);
                return;
            }
            if (telegraphing)
            {
                TickTelegraph(deltaGameSeconds);
                return;
            }
            if (TryBeginImugiPhaseAttack()) return;

            specialCooldownRemaining = Mathf.Max(0f, specialCooldownRemaining - deltaGameSeconds);
            var targetOffset = (Vector2)(targetTransform.position - transform.position);
            if (!IsFinite(targetOffset)) return;
            var distance = targetOffset.magnitude;
            var navigationOffset = physicsBody != null ? physicsBody.NavigationOffset(targetOffset) : targetOffset;
            var navigationDistance = navigationOffset.magnitude;
            var contactRange = ResolveContactRange();
            var contactDistance = physicsBody != null ? navigationDistance : distance;
            var direction = navigationDistance > Mathf.Epsilon
                ? physicsBody != null ? physicsBody.NavigationDirection(targetOffset) : navigationOffset / navigationDistance
                : lockedAim;
            var hasAttackLine = physicsBody == null || physicsBody.HasClearAttackLine(targetTransform.position);
            var recognitionOriginLocalOffset = ResolveSpecialOriginLocalOffset(direction);
            var recognitionOriginWorld =
                (Vector2)transform.TransformPoint(recognitionOriginLocalOffset);
            var specialTargetOffset = (Vector2)targetTransform.position - recognitionOriginWorld;

            if (specialCooldownRemaining <= .0001f &&
                IsInsideSpecialRecognitionRange(specialTargetOffset, direction, recognitionOriginWorld) &&
                hasAttackLine)
            {
                BeginTelegraph(direction);
                return;
            }

            if (contactDistance > contactRange + RangeTolerance || !hasAttackLine)
            {
                var travel = Mathf.Min(MoveSpeedTilesPerGameSecond * deltaGameSeconds,
                    Mathf.Max(0f, navigationDistance - contactRange));
                var actualTravel = physicsBody != null
                    ? physicsBody.Move(direction * travel)
                    : MoveWithoutPhysics(direction * travel);
                if (actualTravel > Mathf.Epsilon) SetAnimationMovement(direction);
                targetOffset = (Vector2)(targetTransform.position - transform.position);
                distance = targetOffset.magnitude;
                navigationOffset = physicsBody != null ? physicsBody.NavigationOffset(targetOffset) : targetOffset;
                navigationDistance = navigationOffset.magnitude;
                contactDistance = physicsBody != null ? navigationDistance : distance;
                hasAttackLine = physicsBody == null || physicsBody.HasClearAttackLine(targetTransform.position);
            }

            if (contactDistance <= contactRange + RangeTolerance && hasAttackLine && contactAttackRemaining <= .0001f &&
                definition.ContactDamage > 0 && combatTarget.TryApplyContactDamage(definition.ContactDamage))
            {
                contactAttackRemaining = ContactAttackIntervalGameSeconds;
                Attacked?.Invoke();
            }
        }

        private void TickOpeningDodge(float deltaGameSeconds)
        {
            if (openingDodgeRemaining <= 0f || health == null) return;
            openingDodgeRemaining = Mathf.Max(0f, openingDodgeRemaining - deltaGameSeconds);
            if (openingDodgeRemaining > 0f) return;
            health.SetDamageTakenMultiplier(1f);
        }

        private float ResolveContactRange() =>
            definition != null
                ? definition.Kind switch
                {
                    BossKind.GoblinChief => GoblinChiefContactRange,
                    BossKind.MotherBulgasari => MotherBulgasariContactRange,
                    _ => ContactRange
                }
                : ContactRange;

        private void BeginTelegraph(Vector2 aim)
        {
            lockedAim = aim.sqrMagnitude > Mathf.Epsilon ? aim.normalized : Vector2.down;
            if (characterAnimator == null)
                characterAnimator = GetComponentInChildren<RuntimeCharacterSpriteAnimator>();
            characterAnimator?.SetFacing(lockedAim);
            specialOriginLocalOffset = ResolveSpecialOriginLocalOffset(lockedAim);
            telegraphing = true;
            specialAnimationStarted = false;
            specialDefenseApplied = false;
            telegraphRemaining = Mathf.Max(0f, definition.TelegraphSeconds);
            SetTelegraphVisible(true);
            RefreshTelegraphVisual();
            TryStartSpecialAnimation();
            if (telegraphRemaining <= .0001f) ActivateSpecial();
        }

        private bool TryBeginImugiPhaseAttack()
        {
            if (definition == null || definition.Kind != BossKind.Imugi || health == null ||
                health.IsDead || health.MaxHealth <= 0) return false;

            var healthRatio = health.Current / (float)health.MaxHealth;
            if (!imugiLandingPhaseTriggered && healthRatio < ImugiLandingPhaseHealthRatio)
            {
                imugiLandingPhaseTriggered = true;
                imugiPhaseAttack = ImugiPhaseAttack.LandingDischarge;
            }
            else if (!imugiLakePhaseTriggered && healthRatio < ImugiLakePhaseHealthRatio)
            {
                imugiLakePhaseTriggered = true;
                imugiPhaseAttack = ImugiPhaseAttack.LakePulse;
            }
            else return false;

            var targetOffset = (Vector2)(targetTransform.position - transform.position);
            BeginTelegraph(targetOffset.sqrMagnitude > Mathf.Epsilon
                ? targetOffset.normalized
                : Vector2.right);
            return true;
        }

        private void TickTelegraph(float deltaGameSeconds)
        {
            if (!definition.SpecialAimLocks)
            {
                var aim = (Vector2)(targetTransform.position - transform.position);
                if (IsFinite(aim) && aim.sqrMagnitude > Mathf.Epsilon) lockedAim = aim.normalized;
                RefreshTelegraphVisual();
            }
            telegraphRemaining = Mathf.Max(0f, telegraphRemaining - deltaGameSeconds);
            TryStartSpecialAnimation();
            if (telegraphRemaining <= .0001f) ActivateSpecial();
        }

        private void ActivateSpecial()
        {
            telegraphing = false;
            if (definition.Kind != BossKind.MotherBulgasari) StartSpecialAnimation();
            SpecialStarted?.Invoke();
            if (imugiPhaseAttack == ImugiPhaseAttack.LandingDischarge)
            {
                ApplyImugiLandingDischarge();
                FinishSpecial();
                return;
            }
            if (imugiPhaseAttack == ImugiPhaseAttack.LakePulse)
            {
                specialActive = true;
                imugiLakePulsesRemaining = ImugiLakePulseCount;
                activeSpecialRemaining =
                    Mathf.Max(0f, (ImugiLakePulseCount - 1) * ImugiLakePulseIntervalSeconds);
                ApplyImugiLakePulse();
                imugiLakePulsesRemaining--;
                specialTickRemaining = ImugiLakePulseIntervalSeconds;
                SetTelegraphColor(new Color(.2f, .65f, 1f, .42f));
                SetTelegraphVisible(true);
                RefreshTelegraphVisual();
                if (imugiLakePulsesRemaining <= 0) FinishSpecial();
                return;
            }
            if (definition.Kind == BossKind.GoblinChief)
                characterAnimator?.AlignActionImpactFrame(GoblinChiefSpecialImpactFrameIndex);
            if (definition.SpecialDurationSeconds <= .0001f || definition.SpecialTickSeconds <= .0001f)
            {
                ApplySpecialHit();
                FinishSpecial();
                return;
            }
            specialActive = true;
            activeSpecialRemaining = definition.SpecialDurationSeconds;
            specialTickRemaining = definition.SpecialTickSeconds;
            specialTickAnimationStarted = false;
            SetTelegraphColor(new Color(1f, .08f, .02f, .46f));
            SetTelegraphVisible(definition.Kind != BossKind.Gangcheori);
            SetSpecialEffectVisible(definition.Kind == BossKind.Gangcheori);
        }

        private void TryStartSpecialAnimation()
        {
            if (definition == null || definition.Kind != BossKind.GoblinChief ||
                telegraphRemaining > GoblinChiefSpecialAnimationLeadSeconds) return;
            StartSpecialAnimation();
        }

        private void StartSpecialAnimation()
        {
            if (specialAnimationStarted) return;
            specialAnimationStarted = true;
            SpecialAnimationStarted?.Invoke();
        }

        private void TickActiveSpecial(float deltaGameSeconds)
        {
            if (imugiPhaseAttack == ImugiPhaseAttack.LakePulse)
            {
                TickImugiLakePulse(deltaGameSeconds);
                return;
            }

            var remainingStep = Mathf.Min(deltaGameSeconds, activeSpecialRemaining);
            activeSpecialRemaining = Mathf.Max(0f, activeSpecialRemaining - deltaGameSeconds);
            specialTickRemaining -= remainingStep;
            if (definition.Kind == BossKind.MotherBulgasari && !specialTickAnimationStarted &&
                specialTickRemaining <= MotherBulgasariSpecialAttackLeadSeconds)
            {
                characterAnimator?.PlayAttack();
                specialTickAnimationStarted = true;
            }
            while (specialTickRemaining <= .0001f)
            {
                if (definition.Kind == BossKind.MotherBulgasari)
                {
                    if (!specialTickAnimationStarted) characterAnimator?.PlayAttack();
                    characterAnimator?.AlignActionImpactFrame(
                        MotherBulgasariSpecialImpactFrameIndex);
                }
                ApplySpecialHit();
                specialTickRemaining += definition.SpecialTickSeconds;
                specialTickAnimationStarted = false;
                if (definition.SpecialTickSeconds <= .0001f) break;
            }
            if (activeSpecialRemaining <= .0001f) FinishSpecial();
        }

        private void TickImugiLakePulse(float deltaGameSeconds)
        {
            activeSpecialRemaining = Mathf.Max(0f, activeSpecialRemaining - deltaGameSeconds);
            specialTickRemaining -= deltaGameSeconds;
            while (imugiLakePulsesRemaining > 0 && specialTickRemaining <= .0001f)
            {
                ApplyImugiLakePulse();
                imugiLakePulsesRemaining--;
                specialTickRemaining += ImugiLakePulseIntervalSeconds;
            }
            if (imugiLakePulsesRemaining <= 0) FinishSpecial();
        }

        private void ApplyImugiLandingDischarge()
        {
            PlayImugiSpecialEffect();
            var targetOffset = (Vector2)targetTransform.position - (Vector2)transform.position;
            if (Mathf.Abs(targetOffset.x) > ImugiLandingHalfExtentTiles + RangeTolerance ||
                Mathf.Abs(targetOffset.y) > ImugiLandingHalfExtentTiles + RangeTolerance) return;
            var direction = targetOffset.sqrMagnitude > Mathf.Epsilon
                ? targetOffset.normalized
                : Vector2.up;
            TryApplySpecialDamage(ImugiPhaseDamage, DamageTag.Melee,
                direction * ImugiLandingKnockbackTiles);
        }

        private void ApplyImugiLakePulse()
        {
            PlayImugiSpecialEffect();
            TryApplySpecialDamage(ImugiPhaseDamage, DamageTag.Melee, Vector2.zero);
        }

        private void FinishSpecial()
        {
            var completedImugiPhaseAttack = imugiPhaseAttack;
            specialActive = false;
            activeSpecialRemaining = 0f;
            specialTickRemaining = 0f;
            specialCooldownRemaining = completedImugiPhaseAttack == ImugiPhaseAttack.None
                ? Mathf.Max(0f, definition.SpecialCooldownSeconds)
                : 0f;
            specialAnimationStarted = false;
            specialTickAnimationStarted = false;
            imugiPhaseAttack = ImugiPhaseAttack.None;
            imugiLakePulsesRemaining = 0;
            SetTelegraphVisible(false);
            if (definition == null || definition.Kind != BossKind.Imugi ||
                specialEffectPlaybackRemaining <= .0001f)
                SetSpecialEffectVisible(false);
        }

        private void SetSpecialEffectVisible(bool visible)
        {
            if (specialEffectRenderer == null) return;
            if (visible)
            {
                specialEffectAnimator?.Configure(specialEffectFrames, SpecialEffectFrameSeconds);
                RefreshSpecialEffectVisual();
            }
            specialEffectRenderer.enabled = visible;
        }

        private void PlayImugiSpecialEffect()
        {
            if (definition == null || definition.Kind != BossKind.Imugi ||
                specialEffectRenderer == null || specialEffectFrames == null ||
                specialEffectFrames.Count == 0) return;
            specialEffectPlaybackRemaining =
                specialEffectFrames.Count * SpecialEffectFrameSeconds;
            SetSpecialEffectVisible(true);
        }

        private void TickSpecialEffect(float deltaGameSeconds)
        {
            if (specialEffectPlaybackRemaining <= .0001f) return;
            specialEffectPlaybackRemaining =
                Mathf.Max(0f, specialEffectPlaybackRemaining - deltaGameSeconds);
            if (specialEffectPlaybackRemaining <= .0001f)
                SetSpecialEffectVisible(false);
        }

        private void RefreshSpecialEffectVisual()
        {
            if (specialEffectTransform == null) return;
            if (definition != null && definition.Kind == BossKind.Imugi)
            {
                var effectWorldSize = imugiPhaseAttack == ImugiPhaseAttack.LandingDischarge
                    ? ImugiLandingHalfExtentTiles * 2f
                    : Mathf.Max(.1f, definition.SpecialRangeTiles);
                var imugiRootScale = transform.lossyScale;
                specialEffectTransform.localScale = new Vector3(
                    effectWorldSize /
                    Mathf.Max(specialEffectMaximumWorldSize.x, Mathf.Epsilon) *
                    SafeInverseScale(imugiRootScale.x),
                    effectWorldSize /
                    Mathf.Max(specialEffectMaximumWorldSize.y, Mathf.Epsilon) *
                    SafeInverseScale(imugiRootScale.y),
                    1f);
                var effectCenter = ResolveImugiSpecialEffectCenter();
                // The delivered 64x48 lightning canvas is bottom-center anchored.
                specialEffectTransform.position =
                    effectCenter - Vector2.up * (effectWorldSize * .5f);
                specialEffectTransform.rotation = Quaternion.identity;
                return;
            }
            var aim = lockedAim.sqrMagnitude > Mathf.Epsilon ? lockedAim.normalized : Vector2.left;
            var rootScale = transform.lossyScale;
            specialEffectTransform.localScale = new Vector3(
                SafeInverseScale(rootScale.x),
                SafeInverseScale(rootScale.y),
                1f);
            specialEffectTransform.position =
                SpecialOriginWorld() +
                aim * (specialEffectWorldLength * .5f) +
                Vector2.up * GangcheoriFireVerticalOffset;
            specialEffectTransform.rotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
        }

        private void ApplySpecialHit()
        {
            if (definition != null && definition.Kind == BossKind.Imugi)
                PlayImugiSpecialEffect();
            if (!DoesSpecialAreaOverlapTarget(SpecialOriginWorld(), lockedAim)) return;
            var tag = definition.SpecialHasFireTag ? DamageTag.Fire : DamageTag.Melee;
            var knockback = ResolveSpecialKnockback(
                (Vector2)targetTransform.position - SpecialOriginWorld());
            TryApplySpecialDamage(definition.SpecialDamagePerHit, tag, knockback);
        }

        private bool TryApplySpecialDamage(int amount, DamageTag tag, Vector2 knockback)
        {
            var delivery = specialDefenseApplied
                ? DamageDelivery.DamageOverTime
                : DamageDelivery.Direct;
            var applied = combatTarget.TryApplyBossSpecialDamage(
                amount, tag, knockback, delivery,
                definition == null || definition.Kind != BossKind.MotherBulgasari);
            if (applied) specialDefenseApplied = true;
            return applied;
        }

        private Vector2 ResolveSpecialKnockback(Vector2 targetOffset)
        {
            if (definition != null && definition.Kind == BossKind.MotherBulgasari)
                return Vector2.up * Mathf.Max(0f, definition.SpecialKnockbackTiles);

            if (definition == null || definition.Kind != BossKind.GoblinChief)
            {
                var direction = targetOffset.sqrMagnitude > Mathf.Epsilon
                    ? targetOffset.normalized
                    : lockedAim;
                return direction * (definition != null ? definition.SpecialKnockbackTiles : 0f);
            }

            var targetWorld = (Vector2)targetTransform.position;
            var attackSign = Mathf.Abs(lockedAim.x) > RangeTolerance
                ? Mathf.Sign(lockedAim.x)
                : Mathf.Abs(targetOffset.x) > RangeTolerance
                    ? Mathf.Sign(targetOffset.x)
                    : 1f;
            var characterRenderer = CharacterRenderer();
            var backEdgeX = transform.position.x;
            if (characterRenderer != null && characterRenderer.sprite != null)
            {
                var bounds = characterRenderer.bounds;
                backEdgeX = attackSign > 0f ? bounds.min.x : bounds.max.x;
            }

            var destinationX = backEdgeX - attackSign * GoblinChiefThrowClearance;
            var requiredDistance = Mathf.Abs(targetWorld.x - destinationX);
            var throwDistance = Mathf.Max(definition.SpecialKnockbackTiles, requiredDistance);
            return Vector2.right * (-attackSign * throwDistance);
        }

        private bool IsInsideSpecialArea(Vector2 offset)
        {
            return IsInsideSpecialArea(offset, lockedAim);
        }

        private bool IsInsideSpecialArea(Vector2 offset, Vector2 aim)
        {
            if (!IsFinite(offset)) return false;
            var range = Mathf.Max(0f, definition.SpecialRangeTiles) + RangeTolerance;
            if (definition.SpecialShape == BossSpecialShape.Cone ||
                definition.SpecialShape == BossSpecialShape.Fan)
            {
                if (offset.magnitude > range) return false;
                if (offset.sqrMagnitude <= Mathf.Epsilon) return true;
                return Vector2.Angle(aim, offset) <= definition.SpecialArcDegrees * .5f + RangeTolerance;
            }

            var forward = Vector2.Dot(offset, aim);
            var side = Mathf.Abs(Vector2.Dot(offset, new Vector2(-aim.y, aim.x)));
            return forward >= -RangeTolerance && forward <= range && side <= range * .5f;
        }

        private Vector2 ResolveImugiSpecialEffectCenter()
        {
            if (imugiPhaseAttack == ImugiPhaseAttack.LandingDischarge)
                return transform.position;
            if (imugiPhaseAttack == ImugiPhaseAttack.LakePulse)
                return targetTransform != null ? targetTransform.position : transform.position;

            var aim = lockedAim.sqrMagnitude > Mathf.Epsilon
                ? lockedAim.normalized
                : Vector2.right;
            var range = definition != null ? Mathf.Max(0f, definition.SpecialRangeTiles) : 0f;
            return SpecialOriginWorld() + aim * (range * .5f);
        }

        private bool IsInsideSpecialRecognitionRange(Vector2 offset, Vector2 aim, Vector2 origin)
        {
            if (definition != null &&
                (definition.Kind == BossKind.GoblinChief ||
                 definition.Kind == BossKind.MotherBulgasari))
            {
                if (!IsFinite(offset)) return false;
                var range = Mathf.Max(0f, definition.SpecialRangeTiles) + RangeTolerance;
                if (!TryGetTargetBounds(out var bounds))
                    return offset.magnitude <= range;
                var closest = new Vector2(
                    Mathf.Clamp(origin.x, bounds.min.x, bounds.max.x),
                    Mathf.Clamp(origin.y, bounds.min.y, bounds.max.y));
                return (closest - origin).sqrMagnitude <= range * range;
            }

            return DoesSpecialAreaOverlapTarget(origin, aim);
        }

        private bool DoesSpecialAreaOverlapTarget(Vector2 origin, Vector2 aim)
        {
            if (!TryGetTargetBounds(out var bounds))
                return IsInsideSpecialArea((Vector2)targetTransform.position - origin, aim);

            var min = (Vector2)bounds.min;
            var max = (Vector2)bounds.max;
            var corners = new[]
            {
                new Vector2(min.x, min.y),
                new Vector2(min.x, max.y),
                new Vector2(max.x, max.y),
                new Vector2(max.x, min.y),
                new Vector2(bounds.center.x, bounds.center.y)
            };
            for (var index = 0; index < corners.Length; index++)
                if (IsInsideSpecialArea(corners[index] - origin, aim))
                    return true;

            var polygon = BuildSpecialAreaPolygon(origin, aim);
            for (var index = 0; index < polygon.Length; index++)
            {
                var vertex = polygon[index];
                if (vertex.x >= min.x && vertex.x <= max.x &&
                    vertex.y >= min.y && vertex.y <= max.y)
                    return true;

                var next = polygon[(index + 1) % polygon.Length];
                if (SegmentIntersectsBounds(vertex, next, min, max))
                    return true;
            }
            return false;
        }

        private bool TryGetTargetBounds(out Bounds bounds)
        {
            bounds = default;
            var found = false;
            var spriteRenderers = targetTransform.GetComponentsInChildren<SpriteRenderer>(true);
            for (var index = 0; index < spriteRenderers.Length; index++)
            {
                var renderer = spriteRenderers[index];
                if (renderer == null || !renderer.enabled || renderer.sprite == null) continue;
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else bounds.Encapsulate(renderer.bounds);
            }
            if (found) return true;

            var colliders = targetTransform.GetComponentsInChildren<Collider2D>(true);
            for (var index = 0; index < colliders.Length; index++)
            {
                var collider = colliders[index];
                if (collider == null || !collider.enabled) continue;
                if (!found)
                {
                    bounds = collider.bounds;
                    found = true;
                }
                else bounds.Encapsulate(collider.bounds);
            }
            return found;
        }

        private Vector2[] BuildSpecialAreaPolygon(Vector2 origin, Vector2 aim)
        {
            const int arcSegments = 24;
            var range = Mathf.Max(.1f, definition.SpecialRangeTiles);
            var forward = aim.sqrMagnitude > Mathf.Epsilon ? aim.normalized : Vector2.right;
            var side = new Vector2(-forward.y, forward.x);

            if (definition.SpecialShape == BossSpecialShape.Cone ||
                definition.SpecialShape == BossSpecialShape.Fan)
            {
                var vertices = new Vector2[arcSegments + 2];
                vertices[0] = origin;
                var halfArc = Mathf.Clamp(definition.SpecialArcDegrees, 0f, 180f) * .5f;
                for (var index = 0; index <= arcSegments; index++)
                {
                    var angle = Mathf.Lerp(-halfArc, halfArc, index / (float)arcSegments) * Mathf.Deg2Rad;
                    vertices[index + 1] =
                        origin + forward * (Mathf.Cos(angle) * range) +
                        side * (Mathf.Sin(angle) * range);
                }
                return vertices;
            }

            var halfWidth = range * .5f;
            return new[]
            {
                origin - side * halfWidth,
                origin + forward * range - side * halfWidth,
                origin + forward * range + side * halfWidth,
                origin + side * halfWidth
            };
        }

        private static bool SegmentIntersectsBounds(Vector2 start, Vector2 end, Vector2 min, Vector2 max)
        {
            var delta = end - start;
            var enter = 0f;
            var exit = 1f;
            return ClipSegmentAxis(start.x, delta.x, min.x, max.x, ref enter, ref exit) &&
                   ClipSegmentAxis(start.y, delta.y, min.y, max.y, ref enter, ref exit);
        }

        private static bool ClipSegmentAxis(float start, float delta, float min, float max,
            ref float enter, ref float exit)
        {
            if (Mathf.Abs(delta) <= Mathf.Epsilon)
                return start >= min && start <= max;
            var first = (min - start) / delta;
            var second = (max - start) / delta;
            if (first > second) (first, second) = (second, first);
            enter = Mathf.Max(enter, first);
            exit = Mathf.Min(exit, second);
            return enter <= exit;
        }

        private void EnsureTelegraphRenderer()
        {
            if (telegraphRenderer != null) return;
            var visual = new GameObject("SpecialTelegraph");
            visual.transform.SetParent(transform, false);
            telegraphFilter = visual.AddComponent<MeshFilter>();
            telegraphRenderer = visual.AddComponent<MeshRenderer>();
            telegraphMesh = new Mesh { name = "NyangbingoBossSpecialTelegraph" };
            telegraphMesh.MarkDynamic();
            telegraphFilter.sharedMesh = telegraphMesh;
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null)
            {
                telegraphMaterial = new Material(shader)
                {
                    name = "NyangbingoBossSpecialTelegraphMaterial",
                    hideFlags = HideFlags.HideAndDontSave
                };
                telegraphRenderer.sharedMaterial = telegraphMaterial;
            }
            telegraphRenderer.sortingOrder = 14;
            SetTelegraphColor(new Color(1f, .2f, .05f, .32f));
        }

        private void RefreshTelegraphVisual()
        {
            if (telegraphRenderer == null || telegraphMesh == null) return;
            if (imugiPhaseAttack == ImugiPhaseAttack.LakePulse)
            {
                telegraphRenderer.enabled = false;
                return;
            }
            if (imugiPhaseAttack == ImugiPhaseAttack.LandingDischarge)
                BuildCenteredBoxMesh(ImugiLandingHalfExtentTiles);
            else
            {
                var range = Mathf.Max(.1f, definition.SpecialRangeTiles);
                if (definition.SpecialShape == BossSpecialShape.Cone ||
                    definition.SpecialShape == BossSpecialShape.Fan)
                    BuildConeMesh(range, definition.SpecialArcDegrees);
                else
                    BuildBoxMesh(range, range * .5f);
            }
            telegraphRenderer.transform.localPosition =
                imugiPhaseAttack == ImugiPhaseAttack.LandingDischarge
                    ? Vector3.zero
                    : new Vector3(specialOriginLocalOffset.x, specialOriginLocalOffset.y, 0f);
            telegraphRenderer.transform.localRotation =
                imugiPhaseAttack == ImugiPhaseAttack.LandingDischarge
                    ? Quaternion.identity
                    : Quaternion.Euler(0f, 0f,
                        Mathf.Atan2(lockedAim.y, lockedAim.x) * Mathf.Rad2Deg);
            var rootScale = transform.lossyScale;
            telegraphRenderer.transform.localScale = new Vector3(
                SafeInverseScale(rootScale.x),
                SafeInverseScale(rootScale.y),
                1f);
            SetTelegraphColor(imugiPhaseAttack == ImugiPhaseAttack.None
                ? new Color(1f, .2f, .05f, .32f)
                : new Color(.2f, .65f, 1f, .42f));
        }

        private Vector2 ResolveSpecialOriginLocalOffset(Vector2 aim)
        {
            if (definition == null ||
                (definition.Kind != BossKind.MotherBulgasari &&
                 definition.Kind != BossKind.GoblinChief &&
                 definition.Kind != BossKind.Gangcheori &&
                 definition.Kind != BossKind.Imugi))
                return Vector2.zero;

            var characterRenderer = CharacterRenderer();
            if (characterRenderer == null || characterRenderer.sprite == null)
                return Vector2.zero;

            var bounds = characterRenderer.bounds;
            var horizontalSign = Mathf.Abs(aim.x) > RangeTolerance
                ? Mathf.Sign(aim.x)
                : definition.Kind == BossKind.Imugi
                    ? characterRenderer.flipX ? 1f : -1f
                    : characterRenderer.flipX ? -1f : 1f;
            var forwardRatio = definition.Kind switch
            {
                BossKind.GoblinChief => GoblinChiefAttackForwardRatio,
                BossKind.Gangcheori => GangcheoriBreathForwardRatio,
                BossKind.Imugi => ImugiElectricForwardRatio,
                _ => MotherBulgasariNoseForwardRatio
            };
            var heightRatio = definition.Kind switch
            {
                BossKind.GoblinChief => GoblinChiefAttackHeightRatio,
                BossKind.Gangcheori => GangcheoriBreathHeightRatio,
                BossKind.Imugi => ImugiElectricHeightRatio,
                _ => MotherBulgasariNoseHeightRatio
            };
            var noseWorld = new Vector2(
                bounds.center.x + bounds.extents.x * forwardRatio * horizontalSign,
                bounds.min.y + bounds.size.y * heightRatio);
            return transform.InverseTransformPoint(noseWorld);
        }

        private Vector2 SpecialOriginWorld() => transform.TransformPoint(specialOriginLocalOffset);

        private void RefreshWarningPosition()
        {
            if (warningTransform == null) return;
            var characterRenderer = CharacterRenderer();
            if (characterRenderer == null || characterRenderer.sprite == null ||
                warningRenderer == null || warningRenderer.sprite == null) return;

            warningTransform.localScale = Vector3.one;
            if (definition != null && definition.Kind == BossKind.GoblinChief)
            {
                var rootScale = transform.lossyScale;
                if (warningMaximumSpriteSize.x > Mathf.Epsilon &&
                    warningMaximumSpriteSize.y > Mathf.Epsilon)
                {
                    warningTransform.localScale = new Vector3(
                        GoblinChiefWarningWorldSize.x /
                        (warningMaximumSpriteSize.x * Mathf.Max(Mathf.Abs(rootScale.x), Mathf.Epsilon)),
                        GoblinChiefWarningWorldSize.y /
                        (warningMaximumSpriteSize.y * Mathf.Max(Mathf.Abs(rootScale.y), Mathf.Epsilon)),
                        1f);
                }
            }

            var characterBounds = characterRenderer.bounds;
            var warningBounds = warningRenderer.bounds;
            var gap = definition != null && definition.Kind == BossKind.GoblinChief
                ? GoblinChiefWarningGap
                : WarningArtGap;
            var desiredCenter = new Vector3(
                characterBounds.center.x,
                characterBounds.max.y + gap + warningBounds.extents.y,
                transform.position.z);
            warningTransform.position += desiredCenter - warningBounds.center;
        }

        private SpriteRenderer CharacterRenderer() =>
            characterAnimator != null
                ? characterAnimator.GetComponent<SpriteRenderer>()
                : null;

        private void LateUpdate()
        {
            if (warningRenderer != null && warningRenderer.enabled)
                RefreshWarningPosition();
        }

        private static float SafeInverseScale(float scale) =>
            Mathf.Abs(scale) > Mathf.Epsilon ? 1f / Mathf.Abs(scale) : 1f;

        private void BuildConeMesh(float range, float arcDegrees)
        {
            const int segments = 24;
            var vertices = new Vector3[segments + 2];
            var triangles = new int[segments * 3];
            vertices[0] = Vector3.zero;
            var halfArc = Mathf.Clamp(arcDegrees, 0f, 180f) * .5f;
            for (var index = 0; index <= segments; index++)
            {
                var angle = Mathf.Lerp(-halfArc, halfArc, index / (float)segments) * Mathf.Deg2Rad;
                vertices[index + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * range;
                if (index >= segments) continue;
                var triangleIndex = index * 3;
                triangles[triangleIndex] = 0;
                triangles[triangleIndex + 1] = index + 2;
                triangles[triangleIndex + 2] = index + 1;
            }
            ApplyTelegraphMesh(vertices, triangles);
        }

        private void BuildBoxMesh(float range, float halfWidth)
        {
            var vertices = new[]
            {
                new Vector3(0f, -halfWidth, 0f),
                new Vector3(range, -halfWidth, 0f),
                new Vector3(range, halfWidth, 0f),
                new Vector3(0f, halfWidth, 0f)
            };
            ApplyTelegraphMesh(vertices, new[] { 0, 2, 1, 0, 3, 2 });
        }

        private void BuildCenteredBoxMesh(float halfExtent)
        {
            var vertices = new[]
            {
                new Vector3(-halfExtent, -halfExtent, 0f),
                new Vector3(halfExtent, -halfExtent, 0f),
                new Vector3(halfExtent, halfExtent, 0f),
                new Vector3(-halfExtent, halfExtent, 0f)
            };
            ApplyTelegraphMesh(vertices, new[] { 0, 2, 1, 0, 3, 2 });
        }

        private void ApplyTelegraphMesh(Vector3[] vertices, int[] triangles)
        {
            telegraphMesh.Clear();
            telegraphMesh.vertices = vertices;
            telegraphMesh.triangles = triangles;
            telegraphMesh.RecalculateBounds();
        }

        private void SetTelegraphColor(Color color)
        {
            if (telegraphMaterial != null) telegraphMaterial.color = color;
        }

        private void SetTelegraphVisible(bool visible)
        {
            if (telegraphRenderer != null) telegraphRenderer.enabled = visible;
            if (warningRenderer != null) warningRenderer.enabled = visible;
        }

        private float MoveWithoutPhysics(Vector2 displacement)
        {
            transform.position += (Vector3)displacement;
            return displacement.magnitude;
        }

        private void SetAnimationMovement(Vector2 direction)
        {
            if (characterAnimator == null)
                characterAnimator = GetComponentInChildren<RuntimeCharacterSpriteAnimator>();
            characterAnimator?.SetFacing(ResolveFacingDirection(direction));
            characterAnimator?.SetMoving(true);
        }

        private Vector2 ResolveFacingDirection(Vector2 movement)
        {
            if (targetTransform == null) return movement;
            var targetOffset = (Vector2)(targetTransform.position - transform.position);
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

        private void OnDisable()
        {
            specialEffectPlaybackRemaining = 0f;
            SetTelegraphVisible(false);
            SetSpecialEffectVisible(false);
        }

        private void OnDestroy()
        {
            if (telegraphMesh != null) Destroy(telegraphMesh);
            if (telegraphMaterial != null) Destroy(telegraphMaterial);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsFinite(Vector2 value) => IsFinite(value.x) && IsFinite(value.y);
    }
}
