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
        bool TryApplyBossSpecialDamage(int amount, DamageTag tag, Vector2 knockback);
    }

    [RequireComponent(typeof(Health))]
    public sealed class BossCombatController : MonoBehaviour, IGameSecondsTickable
    {
        private const float ContactAttackIntervalGameSeconds = 1f;
        private const float ContactRange = 1.05f;
        private const float MoveSpeedTilesPerGameSecond = 1.25f;
        private const float RangeTolerance = .05f;

        [SerializeField] private BossDefinition definition;
        [SerializeField] private MonoBehaviour targetComponent;

        private Transform targetTransform;
        private IBossCombatTarget combatTarget;
        private MeshRenderer telegraphRenderer;
        private MeshFilter telegraphFilter;
        private Mesh telegraphMesh;
        private Material telegraphMaterial;
        private SpriteRenderer warningRenderer;
        private Health health;
        private Vector2 lockedAim = Vector2.down;
        private float contactAttackRemaining;
        private float specialCooldownRemaining;
        private float telegraphRemaining;
        private float activeSpecialRemaining;
        private float specialTickRemaining;
        private bool telegraphing;
        private bool specialActive;
        private WorldMobPhysicsBody physicsBody;
        private RuntimeCharacterSpriteAnimator characterAnimator;

        public BossDefinition Definition => definition;
        public bool IsTelegraphing => telegraphing;
        public bool IsSpecialActive => specialActive;
        public float SpecialCooldownRemaining => specialCooldownRemaining;
        public event System.Action Attacked;
        public event System.Action SpecialStarted;

        public void ConfigureWarningArt(GameplayArtCatalog artCatalog)
        {
            var frames = artCatalog?.BossWarningFrames;
            if (frames == null || frames.Count == 0) return;
            var warning = new GameObject("SpecialWarningArt");
            warning.transform.SetParent(transform, false);
            warning.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            warningRenderer = warning.AddComponent<SpriteRenderer>();
            warningRenderer.sortingOrder = 16;
            warning.AddComponent<RuntimeBuildingSpriteAnimator>().Configure(frames);
            warningRenderer.enabled = telegraphing || specialActive;
        }

        public bool ConfigureForRuntime(BossDefinition value, MonoBehaviour target)
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

            contactAttackRemaining = 0f;
            specialCooldownRemaining = Mathf.Max(0f, definition.SpecialCooldownSeconds);
            telegraphRemaining = 0f;
            activeSpecialRemaining = 0f;
            specialTickRemaining = 0f;
            telegraphing = false;
            specialActive = false;
            EnsureTelegraphRenderer();
            SetTelegraphVisible(false);
            return true;
        }

        public void Tick(float deltaGameSeconds)
        {
            SetAnimationMoving(false);
            if (!IsFinite(deltaGameSeconds) || deltaGameSeconds < 0f || definition == null ||
                targetTransform == null || combatTarget == null || health == null || health.IsDead) return;

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

            specialCooldownRemaining = Mathf.Max(0f, specialCooldownRemaining - deltaGameSeconds);
            var targetOffset = (Vector2)(targetTransform.position - transform.position);
            if (!IsFinite(targetOffset)) return;
            var distance = targetOffset.magnitude;
            var navigationOffset = physicsBody != null ? physicsBody.NavigationOffset(targetOffset) : targetOffset;
            var navigationDistance = navigationOffset.magnitude;
            var direction = navigationDistance > Mathf.Epsilon
                ? physicsBody != null ? physicsBody.NavigationDirection(targetOffset) : navigationOffset / navigationDistance
                : lockedAim;
            var hasAttackLine = physicsBody == null || physicsBody.HasClearAttackLine(targetTransform.position);

            if (specialCooldownRemaining <= .0001f &&
                distance <= definition.SpecialRangeTiles + RangeTolerance && hasAttackLine)
            {
                BeginTelegraph(direction);
                return;
            }

            if (distance > ContactRange + RangeTolerance || !hasAttackLine)
            {
                var travel = Mathf.Min(MoveSpeedTilesPerGameSecond * deltaGameSeconds,
                    Mathf.Max(0f, navigationDistance - ContactRange));
                var actualTravel = physicsBody != null
                    ? physicsBody.Move(direction * travel)
                    : MoveWithoutPhysics(direction * travel);
                if (actualTravel > Mathf.Epsilon) SetAnimationMovement(direction);
                targetOffset = (Vector2)(targetTransform.position - transform.position);
                distance = targetOffset.magnitude;
                hasAttackLine = physicsBody == null || physicsBody.HasClearAttackLine(targetTransform.position);
            }

            if (distance <= ContactRange + RangeTolerance && hasAttackLine && contactAttackRemaining <= .0001f &&
                definition.ContactDamage > 0 && combatTarget.TryApplyContactDamage(definition.ContactDamage))
            {
                contactAttackRemaining = ContactAttackIntervalGameSeconds;
                Attacked?.Invoke();
            }
        }

        private void BeginTelegraph(Vector2 aim)
        {
            lockedAim = aim.sqrMagnitude > Mathf.Epsilon ? aim.normalized : Vector2.down;
            telegraphing = true;
            telegraphRemaining = Mathf.Max(0f, definition.TelegraphSeconds);
            RefreshTelegraphVisual();
            SetTelegraphVisible(true);
            if (telegraphRemaining <= .0001f) ActivateSpecial();
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
            if (telegraphRemaining <= .0001f) ActivateSpecial();
        }

        private void ActivateSpecial()
        {
            telegraphing = false;
            SpecialStarted?.Invoke();
            if (definition.SpecialDurationSeconds <= .0001f || definition.SpecialTickSeconds <= .0001f)
            {
                ApplySpecialHit();
                FinishSpecial();
                return;
            }
            specialActive = true;
            activeSpecialRemaining = definition.SpecialDurationSeconds;
            specialTickRemaining = definition.SpecialTickSeconds;
            SetTelegraphColor(new Color(1f, .08f, .02f, .46f));
            SetTelegraphVisible(true);
        }

        private void TickActiveSpecial(float deltaGameSeconds)
        {
            var remainingStep = Mathf.Min(deltaGameSeconds, activeSpecialRemaining);
            activeSpecialRemaining = Mathf.Max(0f, activeSpecialRemaining - deltaGameSeconds);
            specialTickRemaining -= remainingStep;
            while (specialTickRemaining <= .0001f)
            {
                ApplySpecialHit();
                specialTickRemaining += definition.SpecialTickSeconds;
                if (definition.SpecialTickSeconds <= .0001f) break;
            }
            if (activeSpecialRemaining <= .0001f) FinishSpecial();
        }

        private void FinishSpecial()
        {
            specialActive = false;
            activeSpecialRemaining = 0f;
            specialTickRemaining = 0f;
            specialCooldownRemaining = Mathf.Max(0f, definition.SpecialCooldownSeconds);
            SetTelegraphVisible(false);
        }

        private void ApplySpecialHit()
        {
            var offset = (Vector2)(targetTransform.position - transform.position);
            if (!IsInsideSpecialArea(offset)) return;
            var tag = definition.SpecialHasFireTag ? DamageTag.Fire : DamageTag.Melee;
            var knockbackDirection = offset.sqrMagnitude > Mathf.Epsilon ? offset.normalized : lockedAim;
            combatTarget.TryApplyBossSpecialDamage(definition.SpecialDamagePerHit, tag,
                knockbackDirection * definition.SpecialKnockbackTiles);
        }

        private bool IsInsideSpecialArea(Vector2 offset)
        {
            if (!IsFinite(offset)) return false;
            var range = Mathf.Max(0f, definition.SpecialRangeTiles) + RangeTolerance;
            if (offset.magnitude > range) return false;
            if (definition.SpecialShape == BossSpecialShape.Cone)
            {
                if (offset.sqrMagnitude <= Mathf.Epsilon) return true;
                return Vector2.Angle(lockedAim, offset) <= definition.SpecialArcDegrees * .5f + RangeTolerance;
            }

            var forward = Vector2.Dot(offset, lockedAim);
            var side = Mathf.Abs(Vector2.Dot(offset, new Vector2(-lockedAim.y, lockedAim.x)));
            return forward >= -RangeTolerance && forward <= range && side <= range * .5f;
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
            var range = Mathf.Max(.1f, definition.SpecialRangeTiles);
            if (definition.SpecialShape == BossSpecialShape.Cone)
                BuildConeMesh(range, definition.SpecialArcDegrees);
            else
                BuildBoxMesh(range, range * .5f);
            telegraphRenderer.transform.localPosition = Vector3.zero;
            telegraphRenderer.transform.localRotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(lockedAim.y, lockedAim.x) * Mathf.Rad2Deg);
            telegraphRenderer.transform.localScale = Vector3.one;
            SetTelegraphColor(new Color(1f, .2f, .05f, .32f));
        }

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

        private void OnDisable() => SetTelegraphVisible(false);

        private void OnDestroy()
        {
            if (telegraphMesh != null) Destroy(telegraphMesh);
            if (telegraphMaterial != null) Destroy(telegraphMaterial);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsFinite(Vector2 value) => IsFinite(value.x) && IsFinite(value.y);
    }
}
