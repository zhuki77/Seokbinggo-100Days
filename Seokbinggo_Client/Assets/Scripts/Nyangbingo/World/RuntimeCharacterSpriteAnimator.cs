using System.Collections.Generic;
using Nyangbingo.Combat;
using Nyangbingo.Bosses;
using Nyangbingo.Data;
using Nyangbingo.Yokai;
using UnityEngine;

namespace Nyangbingo.World
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class RuntimeCharacterSpriteAnimator : MonoBehaviour
    {
        private const float FrameSeconds = .1f;
        private const float MovementThreshold = .000001f;

        private SpriteRenderer spriteRenderer;
        private CharacterArtCatalog.Entry entry;
        private Health health;
        private YokaiBrain yokaiBrain;
        private BossCombatController bossCombat;
        private IReadOnlyList<Sprite> activeFrames;
        private Sprite[] singleFrame;
        private Vector3 previousPosition;
        private int frameIndex;
        private float frameRemaining;
        private float actionRemaining;
        private bool holdFinalFrame;
        private bool deathLocked;
        private bool hasExplicitMovementState;
        private bool explicitlyMoving;
        private bool configured;

        public void Configure(CharacterArtCatalog.Entry artEntry, int sortingOrder)
        {
            entry = artEntry;
            spriteRenderer = GetComponent<SpriteRenderer>();
            health = GetComponent<Health>() ?? GetComponentInParent<Health>();
            RuntimePlaceholderVisual.ConfigureSprite(spriteRenderer, entry.Sprite, sortingOrder);
            singleFrame = entry.Sprite != null ? new[] { entry.Sprite } : System.Array.Empty<Sprite>();
            previousPosition = transform.position;
            configured = true;
            deathLocked = false;
            if (health != null) health.Damaged += HandleDamaged;
            PlayLoop(entry.IdleFrames);
        }

        public void SetFacing(Vector2 direction)
        {
            if (!configured || spriteRenderer == null || Mathf.Abs(direction.x) <= Mathf.Epsilon) return;
            spriteRenderer.flipX = entry.SourceFacesRight ? direction.x < 0f : direction.x > 0f;
        }

        public void SetMoving(bool moving)
        {
            hasExplicitMovementState = true;
            explicitlyMoving = moving;
        }

        public void PlayAttack()
        {
            PlayAction(entry?.AttackFrames);
        }

        public void PlayDeath()
        {
            if (!configured || entry == null || entry.DeathFrames.Count == 0) return;
            deathLocked = true;
            activeFrames = entry.DeathFrames;
            frameIndex = 0;
            frameRemaining = FrameSeconds;
            actionRemaining = 0f;
            holdFinalFrame = true;
            ApplyFrame();
        }

        public void ResetToIdle()
        {
            if (!configured || entry == null) return;
            deathLocked = false;
            PlayLoop(entry.IdleFrames);
        }

        public void Bind(YokaiBrain brain)
        {
            if (yokaiBrain != null) yokaiBrain.Attacked -= PlayAttack;
            yokaiBrain = brain;
            if (yokaiBrain != null) yokaiBrain.Attacked += PlayAttack;
        }

        public void Bind(BossCombatController combat)
        {
            if (bossCombat != null)
            {
                bossCombat.Attacked -= PlayAttack;
                bossCombat.SpecialStarted -= PlaySpecial;
            }
            bossCombat = combat;
            if (bossCombat != null)
            {
                bossCombat.Attacked += PlayAttack;
                bossCombat.SpecialStarted += PlaySpecial;
            }
        }

        private void PlaySpecial()
        {
            var frames = entry?.SpecialFrames;
            PlayAction(frames != null && frames.Count > 0 ? frames : entry?.AttackFrames);
        }

        private void Update()
        {
            if (!configured || entry == null) return;

            var delta = transform.position - previousPosition;
            previousPosition = transform.position;
            // Player movement supplies an explicit facing direction. Dynamic Rigidbody2D contact
            // correction can move the body by tiny alternating X deltas while idle, so only infer
            // facing from transform movement for actors that do not supply that explicit state.
            if (!hasExplicitMovementState && Mathf.Abs(delta.x) > Mathf.Epsilon)
                spriteRenderer.flipX = entry.SourceFacesRight ? delta.x < 0f : delta.x > 0f;

            if (deathLocked)
            {
                TickFrames(Time.deltaTime);
                return;
            }

            if (actionRemaining > 0f)
            {
                actionRemaining = Mathf.Max(0f, actionRemaining - Time.deltaTime);
                TickFrames(Time.deltaTime);
                return;
            }

            var moving = hasExplicitMovementState ? explicitlyMoving : delta.sqrMagnitude > MovementThreshold;
            var targetFrames = moving ? MovingFrames() : entry.IdleFrames;
            if (targetFrames == null || targetFrames.Count == 0) targetFrames = SingleFrame();
            if (!ReferenceEquals(activeFrames, targetFrames)) PlayLoop(targetFrames);
            TickFrames(Time.deltaTime);
        }

        private IReadOnlyList<Sprite> MovingFrames()
        {
            if (entry.WalkFrames.Count > 0) return entry.WalkFrames;
            if (entry.FleeFrames.Count > 0) return entry.FleeFrames;
            return entry.IdleFrames;
        }

        private IReadOnlyList<Sprite> SingleFrame()
        {
            return singleFrame;
        }

        private void PlayLoop(IReadOnlyList<Sprite> frames)
        {
            activeFrames = frames != null && frames.Count > 0 ? frames : SingleFrame();
            frameIndex = 0;
            frameRemaining = FrameSeconds;
            holdFinalFrame = false;
            ApplyFrame();
        }

        private void PlayAction(IReadOnlyList<Sprite> frames)
        {
            if (!configured || deathLocked || frames == null || frames.Count == 0) return;
            activeFrames = frames;
            frameIndex = 0;
            frameRemaining = FrameSeconds;
            actionRemaining = frames.Count * FrameSeconds;
            holdFinalFrame = false;
            ApplyFrame();
        }

        private void TickFrames(float deltaTime)
        {
            if (activeFrames == null || activeFrames.Count <= 1) return;
            frameRemaining -= Mathf.Max(0f, deltaTime);
            while (frameRemaining <= 0f)
            {
                if (holdFinalFrame && frameIndex >= activeFrames.Count - 1)
                {
                    frameRemaining = float.PositiveInfinity;
                    return;
                }
                frameIndex = holdFinalFrame
                    ? Mathf.Min(frameIndex + 1, activeFrames.Count - 1)
                    : (frameIndex + 1) % activeFrames.Count;
                frameRemaining += FrameSeconds;
                ApplyFrame();
            }
        }

        private void ApplyFrame()
        {
            if (spriteRenderer == null || activeFrames == null || activeFrames.Count == 0) return;
            spriteRenderer.sprite = activeFrames[Mathf.Clamp(frameIndex, 0, activeFrames.Count - 1)];
        }

        private void HandleDamaged(Nyangbingo.Core.DamageTag tag, int amount)
        {
            if (amount > 0) PlayAction(entry?.HitFrames);
        }

        private void OnDestroy()
        {
            if (health != null) health.Damaged -= HandleDamaged;
            if (yokaiBrain != null) yokaiBrain.Attacked -= PlayAttack;
            if (bossCombat != null)
            {
                bossCombat.Attacked -= PlayAttack;
                bossCombat.SpecialStarted -= PlaySpecial;
            }
        }
    }

    /// <summary>
    /// Eoduksini's lantern response is presentation-only. Scaling this child keeps the
    /// yokai root, collider, AI position, and health bar independent from the effect.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class RuntimeEoduksiniVisual : MonoBehaviour
    {
        private const float InitialScale = 1f;
        private const float DarkScale = 2f;
        private const float LanternScale = .6f;
        private const float DarkAlpha = .7f;
        private const float ScaleUnitsPerSecond = 1.4f;
        private const float ColorUnitsPerSecond = 2f;
        private const float BloomUnitsPerSecond = 2.5f;

        private static readonly Color DarkTint = new(.38f, .4f, .46f, DarkAlpha);
        private static readonly Color LanternTint = new(.82f, 1f, .98f, 1f);
        private static readonly Color CoreTint = new(95f / 255f, 224f / 255f, 208f / 255f, 0f);

        private YokaiBrain brain;
        private SpriteRenderer bodyRenderer;
        private SpriteRenderer coreRenderer;
        private RuntimeDamageFlash damageFlash;
        private float currentScale = InitialScale;
        private float bloomAmount;
        private bool configured;

        public float CurrentScale => currentScale;
        public float CurrentBloom => bloomAmount;

        public void ConfigureForRuntime(YokaiBrain targetBrain, SpriteRenderer targetRenderer)
        {
            Unbind();
            brain = targetBrain;
            bodyRenderer = targetRenderer != null ? targetRenderer : GetComponent<SpriteRenderer>();
            damageFlash = GetComponentInParent<RuntimeDamageFlash>();
            if (brain == null || bodyRenderer == null || brain.Definition == null ||
                brain.Definition.Kind != Nyangbingo.Core.YokaiKind.Eoduksini) return;

            BuildCore();
            currentScale = InitialScale;
            bloomAmount = 0f;
            transform.localScale = Vector3.one * currentScale;
            brain.Bloomed += HandleBloomed;
            configured = true;
            ApplyPresentation();
        }

        private void Update() => TickPresentation(Time.deltaTime);

        public void TickPresentation(float deltaSeconds)
        {
            if (!configured || brain == null || bodyRenderer == null || brain.IsBossEncounterPaused ||
                deltaSeconds < 0f || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds)) return;

            var inLanternRange = brain.IsInLanternRange;
            var targetScale = inLanternRange ? LanternScale : DarkScale;
            currentScale = Mathf.MoveTowards(currentScale, targetScale, ScaleUnitsPerSecond * deltaSeconds);
            bloomAmount = Mathf.MoveTowards(bloomAmount, inLanternRange ? 1f : 0f,
                BloomUnitsPerSecond * deltaSeconds);

            var targetColor = inLanternRange ? LanternTint : DarkTint;
            bodyRenderer.color = MoveColorTowards(bodyRenderer.color, targetColor, ColorUnitsPerSecond * deltaSeconds);
            damageFlash?.SetBaseColor(bodyRenderer.color);
            ApplyPresentation();
        }

        private void HandleBloomed()
        {
            // Restart the opening tween when the 12-second bloom cycle reactivates.
            bloomAmount = 0f;
        }

        private void BuildCore()
        {
            if (coreRenderer != null) return;
            var coreObject = new GameObject("LanternBloomCore");
            coreObject.transform.SetParent(transform, false);
            coreObject.transform.localPosition = new Vector3(0f, .02f, 0f);
            coreRenderer = coreObject.AddComponent<SpriteRenderer>();
            coreRenderer.sortingLayerID = bodyRenderer.sortingLayerID;
            coreRenderer.sortingOrder = bodyRenderer.sortingOrder + 1;
            coreRenderer.color = CoreTint;
        }

        private void ApplyPresentation()
        {
            transform.localScale = new Vector3(currentScale, currentScale, 1f);
            if (coreRenderer == null || bodyRenderer == null) return;
            coreRenderer.sprite = bodyRenderer.sprite;
            coreRenderer.flipX = bodyRenderer.flipX;
            var color = CoreTint;
            color.a = .85f * bloomAmount;
            coreRenderer.color = color;
            var coreScale = Mathf.Lerp(.18f, .5f, bloomAmount);
            coreRenderer.transform.localScale = new Vector3(coreScale, coreScale, 1f);
        }

        private static Color MoveColorTowards(Color current, Color target, float maxDelta)
        {
            return new Color(
                Mathf.MoveTowards(current.r, target.r, maxDelta),
                Mathf.MoveTowards(current.g, target.g, maxDelta),
                Mathf.MoveTowards(current.b, target.b, maxDelta),
                Mathf.MoveTowards(current.a, target.a, maxDelta));
        }

        private void Unbind()
        {
            if (brain != null) brain.Bloomed -= HandleBloomed;
            configured = false;
        }

        private void OnDestroy() => Unbind();
    }
}
