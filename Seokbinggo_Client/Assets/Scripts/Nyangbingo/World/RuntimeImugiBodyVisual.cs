using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>
    /// 이무기 머리 뒤에 단일 몸통 타일을 반복 배치하는 장식 전용 비주얼이다.
    /// 전투 충돌과 피해 판정은 기존 보스 루트의 콜라이더를 그대로 사용한다.
    /// </summary>
    public sealed class RuntimeImugiBodyVisual : MonoBehaviour
    {
        private const int SegmentCount = 10;
        private const float FirstSegmentOffset = .36f;
        private const float SegmentSpacing = .68f;
        private const float BodyVerticalOffset = 1f;
        private const float WaveAmplitude = .22f;
        private const float WaveSpeed = 5f;
        private const float WavePhaseOffset = 1.05f;
        private const float HorizontalBodyRotation = 90f;

        private readonly Transform[] segments = new Transform[SegmentCount];
        private readonly Vector2[] segmentWorldPositions = new Vector2[SegmentCount];
        private Vector3 previousPosition;
        private Vector2 facing = Vector2.right;
        private bool segmentPositionsInitialized;
        private bool configured;

        public void Configure(Sprite bodySprite, int sortingOrder)
        {
            if (bodySprite == null) return;
            for (var index = 0; index < segments.Length; index++)
            {
                var segmentObject = new GameObject($"Body_{index + 1}");
                segmentObject.transform.SetParent(transform, false);
                var renderer = segmentObject.AddComponent<SpriteRenderer>();
                RuntimePlaceholderVisual.ConfigureSprite(renderer, bodySprite, sortingOrder);
                segmentObject.AddComponent<RuntimeSpriteBoundsHurtbox>().Configure(renderer);
                segments[index] = segmentObject.transform;
            }

            previousPosition = transform.position;
            configured = true;
            InitializeSegmentPositions();
            RefreshSegments();
        }

        private void LateUpdate()
        {
            if (!configured) return;
            var delta = transform.position - previousPosition;
            previousPosition = transform.position;
            if (delta.sqrMagnitude > .000001f) facing = ((Vector2)delta).normalized;
            RefreshSegments();
        }

        private void RefreshSegments()
        {
            if (!segmentPositionsInitialized) InitializeSegmentPositions();
            var worldScale = Mathf.Max(
                Mathf.Abs(transform.lossyScale.x),
                Mathf.Abs(transform.lossyScale.y));
            var predecessor =
                (Vector2)transform.TransformPoint(Vector3.up * BodyVerticalOffset);
            for (var index = 0; index < segments.Length; index++)
            {
                if (segments[index] == null) continue;
                var desiredDistance =
                    (index == 0 ? FirstSegmentOffset : SegmentSpacing) * worldScale;
                var predecessorOffset = segmentWorldPositions[index] - predecessor;
                if (predecessorOffset.sqrMagnitude <= .000001f)
                    predecessorOffset = -facing * desiredDistance;
                var currentDistance = predecessorOffset.magnitude;
                if (currentDistance > desiredDistance)
                {
                    segmentWorldPositions[index] =
                        predecessor + predecessorOffset / currentDistance * desiredDistance;
                }

                var axis = predecessor - segmentWorldPositions[index];
                if (axis.sqrMagnitude <= .000001f) axis = facing;
                else axis.Normalize();
                var perpendicular = new Vector2(-axis.y, axis.x);
                var wave =
                    Mathf.Sin(Time.time * WaveSpeed - index * WavePhaseOffset) *
                    WaveAmplitude * worldScale;
                var visualPosition = segmentWorldPositions[index] + perpendicular * wave;
                segments[index].position =
                    new Vector3(visualPosition.x, visualPosition.y, transform.position.z + .01f);
                segments[index].localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    Mathf.Abs(axis.x) > Mathf.Abs(axis.y) ? HorizontalBodyRotation : 0f);
                predecessor = segmentWorldPositions[index];
            }
        }

        private void InitializeSegmentPositions()
        {
            var worldScale = Mathf.Max(
                Mathf.Abs(transform.lossyScale.x),
                Mathf.Abs(transform.lossyScale.y));
            var predecessor =
                (Vector2)transform.TransformPoint(Vector3.up * BodyVerticalOffset);
            for (var index = 0; index < segmentWorldPositions.Length; index++)
            {
                var distance =
                    (index == 0 ? FirstSegmentOffset : SegmentSpacing) * worldScale;
                segmentWorldPositions[index] = predecessor - facing * distance;
                predecessor = segmentWorldPositions[index];
            }
            segmentPositionsInitialized = true;
        }
    }

    /// <summary>
    /// Repeats the delivered Gangcheori body sprite behind the head while preserving
    /// the boss root, movement collider, and combat controller.
    /// </summary>
    public sealed class RuntimeGangcheoriBodyVisual : MonoBehaviour
    {
        private const int SegmentCount = 5;
        private const float FirstSegmentOffset = .48f;
        private const float SegmentSpacing = .42f;
        private const float BodyVerticalOffset = .25f;
        private const float WaveAmplitude = .1f;
        private const float WaveSpeed = 4.25f;
        private const float WavePhaseOffset = .8f;
        private const float HorizontalBodyRotation = 90f;

        private readonly Transform[] segments = new Transform[SegmentCount];
        private SpriteRenderer headRenderer;
        private bool configured;

        public void Configure(Sprite bodySprite, SpriteRenderer head, int sortingOrder)
        {
            if (bodySprite == null || head == null) return;
            headRenderer = head;
            for (var index = 0; index < segments.Length; index++)
            {
                var segmentObject = new GameObject($"GangcheoriBody_{index + 1}");
                segmentObject.transform.SetParent(transform, false);
                var renderer = segmentObject.AddComponent<SpriteRenderer>();
                RuntimePlaceholderVisual.ConfigureSprite(renderer, bodySprite, sortingOrder);
                segmentObject.AddComponent<RuntimeSpriteBoundsHurtbox>().Configure(renderer);
                segments[index] = segmentObject.transform;
            }

            configured = true;
            RefreshSegments();
        }

        private void LateUpdate()
        {
            if (configured) RefreshSegments();
        }

        private void RefreshSegments()
        {
            // The delivered head faces left before SpriteRenderer.flipX is applied.
            var facingSign = headRenderer != null && headRenderer.flipX ? 1f : -1f;
            for (var index = 0; index < segments.Length; index++)
            {
                if (segments[index] == null) continue;
                var distance = FirstSegmentOffset + SegmentSpacing * index;
                var wave =
                    Mathf.Sin(Time.time * WaveSpeed - index * WavePhaseOffset) * WaveAmplitude;
                segments[index].localPosition =
                    new Vector3(-facingSign * distance, BodyVerticalOffset + wave, .01f);
                segments[index].localRotation =
                    Quaternion.Euler(0f, 0f, HorizontalBodyRotation);
            }
        }
    }

    /// <summary>
    /// 현재 스프라이트 프레임의 로컬 경계를 따라가는 전투 전용 트리거 피격 영역.
    /// 이동용 콜라이더와 분리해 큰 보스의 외곽 스프라이트도 공격을 받을 수 있게 한다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
    public sealed class RuntimeSpriteBoundsHurtbox : MonoBehaviour
    {
        private SpriteRenderer targetRenderer;
        private BoxCollider2D hurtbox;
        private Rigidbody2D detachedBody;
        private Sprite observedSprite;

        public void Configure(SpriteRenderer renderer)
        {
            targetRenderer = renderer != null ? renderer : GetComponent<SpriteRenderer>();
            hurtbox = GetComponent<BoxCollider2D>();
            if (hurtbox == null) hurtbox = gameObject.AddComponent<BoxCollider2D>();
            hurtbox.isTrigger = true;
            EnsureDetachedBody();
            RefreshBounds();
        }

        private void Awake()
        {
            if (targetRenderer == null) targetRenderer = GetComponent<SpriteRenderer>();
            if (hurtbox == null) hurtbox = GetComponent<BoxCollider2D>();
            EnsureDetachedBody();
        }

        private void LateUpdate()
        {
            if (targetRenderer != null && targetRenderer.sprite != observedSprite)
                RefreshBounds();
        }

        public void RefreshBounds()
        {
            if (targetRenderer == null) targetRenderer = GetComponent<SpriteRenderer>();
            if (hurtbox == null)
            {
                hurtbox = GetComponent<BoxCollider2D>();
                if (hurtbox == null) hurtbox = gameObject.AddComponent<BoxCollider2D>();
                hurtbox.isTrigger = true;
            }

            observedSprite = targetRenderer != null ? targetRenderer.sprite : null;
            hurtbox.enabled = observedSprite != null;
            if (observedSprite == null) return;
            hurtbox.offset = observedSprite.bounds.center;
            hurtbox.size = new Vector2(
                Mathf.Max(.01f, observedSprite.bounds.size.x),
                Mathf.Max(.01f, observedSprite.bounds.size.y));
        }

        private void EnsureDetachedBody()
        {
            if (detachedBody == null) detachedBody = GetComponent<Rigidbody2D>();
            if (detachedBody == null) detachedBody = gameObject.AddComponent<Rigidbody2D>();
            detachedBody.bodyType = RigidbodyType2D.Kinematic;
            detachedBody.simulated = true;
            detachedBody.gravityScale = 0f;
            detachedBody.constraints = RigidbodyConstraints2D.FreezeRotation;
            detachedBody.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
            detachedBody.interpolation = RigidbodyInterpolation2D.None;
        }
    }
}
