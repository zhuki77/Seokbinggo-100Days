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
            if (Mathf.Abs(delta.x) > Mathf.Epsilon)
                spriteRenderer.flipX = entry.SourceFacesRight ? delta.x < 0f : delta.x > 0f;

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
            ApplyFrame();
        }

        private void PlayAction(IReadOnlyList<Sprite> frames)
        {
            if (!configured || frames == null || frames.Count == 0) return;
            activeFrames = frames;
            frameIndex = 0;
            frameRemaining = FrameSeconds;
            actionRemaining = frames.Count * FrameSeconds;
            ApplyFrame();
        }

        private void TickFrames(float deltaTime)
        {
            if (activeFrames == null || activeFrames.Count <= 1) return;
            frameRemaining -= Mathf.Max(0f, deltaTime);
            while (frameRemaining <= 0f)
            {
                frameIndex = (frameIndex + 1) % activeFrames.Count;
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
}
