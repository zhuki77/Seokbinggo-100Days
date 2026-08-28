using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor.Sprites;
#endif

namespace Nyangbingo.World
{
    internal static class RuntimeTailSpriteCropper
    {
        private static readonly Dictionary<Sprite, Sprite> LeftHalfCache =
            new Dictionary<Sprite, Sprite>();
        private static readonly Dictionary<Sprite, Sprite> RightHalfCache =
            new Dictionary<Sprite, Sprite>();

        public static Sprite CropHorizontalHalf(Sprite source, bool rightHalf)
        {
            if (source == null) return null;
            var cache = rightHalf ? RightHalfCache : LeftHalfCache;
            if (cache.TryGetValue(source, out var cached)) return cached;

            var sourceRect = source.rect;
            var halfWidth = Mathf.Floor(sourceRect.width * .5f);
            if (halfWidth < 1f) return source;
            var cropRect = new Rect(
                rightHalf ? sourceRect.x + halfWidth : sourceRect.x,
                sourceRect.y,
                rightHalf ? sourceRect.width - halfWidth : halfWidth,
                sourceRect.height);
            var sprite = CreateCroppedSprite(source, cropRect, rightHalf);
            if (sprite == null) return source;
            cache[source] = sprite;
            return sprite;
        }

        private static Sprite CreateCroppedSprite(Sprite source, Rect cropRect, bool rightHalf)
        {
            var texture = source.texture;
            if (texture != null && texture.isReadable)
                return CreateNamedSubSprite(texture, source, cropRect, rightHalf);

            return CreateCroppedSpriteFromRenderCopy(source, cropRect, rightHalf);
        }

        /// <summary>
        /// Aseprite 임포트 텍스처는 isReadable=false인 경우가 많아 Sprite.Create가
        /// 실패한다. 전체 atlas를 RT로 복사한 뒤 원본 rect 좌표로 서브 스프라이트를 만든다.
        /// </summary>
        private static Sprite CreateCroppedSpriteFromRenderCopy(
            Sprite source, Rect cropRect, bool rightHalf)
        {
            var texture = source.texture;
            if (texture == null) return null;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                var readableAtlas = SpriteUtility.GetSpriteTexture(source, true);
                if (readableAtlas != null)
                    return CreateNamedSubSprite(readableAtlas, source, cropRect, rightHalf);
            }
#endif

            var renderTarget = RenderTexture.GetTemporary(
                texture.width,
                texture.height,
                0,
                RenderTextureFormat.ARGB32);
            var previousTarget = RenderTexture.active;
            try
            {
                Graphics.Blit(texture, renderTarget);
                RenderTexture.active = renderTarget;
                var readableAtlas = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false)
                {
                    filterMode = texture.filterMode,
                    hideFlags = HideFlags.HideAndDontSave
                };
                readableAtlas.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), 0, 0);
                readableAtlas.Apply();
                return CreateNamedSubSprite(readableAtlas, source, cropRect, rightHalf);
            }
            finally
            {
                RenderTexture.active = previousTarget;
                RenderTexture.ReleaseTemporary(renderTarget);
            }
        }

        private static Sprite CreateNamedSubSprite(
            Texture2D atlas, Sprite source, Rect cropRect, bool rightHalf)
        {
            var sprite = Sprite.Create(
                atlas,
                cropRect,
                new Vector2(.5f, .5f),
                source.pixelsPerUnit,
                0,
                SpriteMeshType.FullRect,
                Vector4.zero,
                false);
            sprite.name = $"{source.name}_{(rightHalf ? "RightHalf" : "LeftHalf")}";
            return sprite;
        }
    }

    /// <summary>
    /// 이무기 머리 뒤에 단일 몸통 타일을 반복 배치하는 장식 전용 비주얼이다.
    /// 전투 충돌과 피해 판정은 기존 보스 루트의 콜라이더를 그대로 사용한다.
    /// </summary>
    public sealed class RuntimeImugiBodyVisual : MonoBehaviour
    {
        private const int BodySegmentCount = 10;
        private const int TailSegmentCount = 2;
        private const int SegmentCount = BodySegmentCount + TailSegmentCount;
        private const float FirstSegmentOffset = .36f;
        private const float SegmentSpacing = .68f;
        private const float PreTailSpacing = 1.0625f;
        private const float PostTailSpacing = .75f;
        private const float BodyVerticalOffset = 1f;
        private const float WaveAmplitude = .22f;
        private const float WaveSpeed = 5f;
        private const float WavePhaseOffset = 1.05f;
        private const float HorizontalBodyRotation = 90f;

        private readonly Transform[] segments = new Transform[SegmentCount];
        private readonly SpriteRenderer[] segmentRenderers = new SpriteRenderer[SegmentCount];
        private readonly Vector2[] segmentWorldPositions = new Vector2[SegmentCount];
        private Sprite preTailVisual;
        private Sprite postTailVisual;
        private Vector3 previousPosition;
        private Vector2 facing = Vector2.right;
        private bool segmentPositionsInitialized;
        private bool configured;

        public void Configure(Sprite bodySprite, Sprite preTailSprite, Sprite postTailSprite,
            int sortingOrder)
        {
            if (bodySprite == null) return;
            // Unity's Aseprite importer keeps the full 32px cel even though the
            // delivered 16px canvas selects only one half. The right half is the
            // larger pre-tail and the left half is the smaller post-tail.
            preTailVisual = RuntimeTailSpriteCropper.CropHorizontalHalf(
                preTailSprite, rightHalf: true);
            postTailVisual = RuntimeTailSpriteCropper.CropHorizontalHalf(
                postTailSprite, rightHalf: false);
            for (var index = 0; index < segments.Length; index++)
            {
                var sprite = index < BodySegmentCount
                    ? bodySprite
                    : index == BodySegmentCount
                        ? preTailVisual
                        : postTailVisual;
                if (sprite == null) continue;
                var segmentName = index < BodySegmentCount
                    ? $"Body_{index + 1}"
                    : index == BodySegmentCount
                        ? "PreTail"
                        : "PostTail";
                var segmentObject = new GameObject(segmentName);
                segmentObject.transform.SetParent(transform, false);
                var renderer = segmentObject.AddComponent<SpriteRenderer>();
                RuntimePlaceholderVisual.ConfigureSprite(
                    renderer,
                    sprite,
                    sortingOrder - (SegmentCount - 1 - index));
                segmentObject.AddComponent<RuntimeSpriteBoundsHurtbox>().Configure(renderer);
                segments[index] = segmentObject.transform;
                segmentRenderers[index] = renderer;
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
            var tailDirection = Vector2.zero;
            for (var index = 0; index < segments.Length; index++)
            {
                if (segments[index] == null) continue;
                var desiredDistance = SegmentDistance(index) * worldScale;
                if (index >= BodySegmentCount)
                {
                    if (tailDirection.sqrMagnitude <= .000001f)
                    {
                        tailDirection =
                            segmentWorldPositions[BodySegmentCount - 1] -
                            segmentWorldPositions[BodySegmentCount - 2];
                        if (tailDirection.sqrMagnitude <= .000001f)
                            tailDirection = -facing;
                        else
                            tailDirection.Normalize();
                        // Body links follow a smooth world-space trail, but the pixel-art
                        // pieces render only horizontally or vertically. Snap the tail
                        // continuation to that same display axis so a diagonal center
                        // offset cannot bury the rectangular pre-tail inside the body.
                        tailDirection = Mathf.Abs(tailDirection.x) >
                                        Mathf.Abs(tailDirection.y)
                            ? new Vector2(Mathf.Sign(tailDirection.x), 0f)
                            : new Vector2(0f, Mathf.Sign(tailDirection.y));
                    }
                    // The two delivered tail pieces form one tapered continuation.
                    // Keep them on the final body axis so the rope trail cannot fold
                    // pre-tail and post-tail back between larger body segments.
                    segmentWorldPositions[index] =
                        predecessor + tailDirection * desiredDistance;
                }
                else
                {
                    var predecessorOffset = segmentWorldPositions[index] - predecessor;
                    if (predecessorOffset.sqrMagnitude <= .000001f)
                        predecessorOffset = -facing * desiredDistance;
                    var currentDistance = predecessorOffset.magnitude;
                    if (currentDistance > desiredDistance)
                    {
                        segmentWorldPositions[index] =
                            predecessor + predecessorOffset / currentDistance * desiredDistance;
                    }
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
                var horizontal = Mathf.Abs(axis.x) > Mathf.Abs(axis.y);
                var isTail = index >= BodySegmentCount;
                segments[index].localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    isTail
                        ? horizontal ? 0f : -HorizontalBodyRotation
                        : horizontal ? HorizontalBodyRotation : 0f);
                if (isTail && segmentRenderers[index] != null)
                {
                    segmentRenderers[index].sprite =
                        index == BodySegmentCount ? preTailVisual : postTailVisual;
                    segmentRenderers[index].flipX =
                        horizontal ? axis.x > 0f : axis.y < 0f;
                }
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
                var distance = SegmentDistance(index) * worldScale;
                segmentWorldPositions[index] = predecessor - facing * distance;
                predecessor = segmentWorldPositions[index];
            }
            segmentPositionsInitialized = true;
        }

        private static float SegmentDistance(int index)
        {
            if (index == 0) return FirstSegmentOffset;
            if (index == BodySegmentCount) return PreTailSpacing;
            if (index > BodySegmentCount) return PostTailSpacing;
            return SegmentSpacing;
        }
    }

    /// <summary>
    /// Repeats the delivered Gangcheori body sprite behind the head while preserving
    /// the boss root, movement collider, and combat controller.
    /// </summary>
    public sealed class RuntimeGangcheoriBodyVisual : MonoBehaviour
    {
        private const int BodySegmentCount = 5;
        private const int TailSegmentCount = 2;
        private const int SegmentCount = BodySegmentCount + TailSegmentCount;
        private const float FirstSegmentOffset = .48f;
        private const float SegmentSpacing = .42f;
        private const float PreTailSpacing = .38f;
        private const float PostTailSpacing = .38f;
        private const float BodyVerticalOffset = .25f;
        private const float WaveAmplitude = .1f;
        private const float WaveSpeed = 4.25f;
        private const float WavePhaseOffset = .8f;
        private const float HorizontalBodyRotation = 90f;

        private readonly Transform[] segments = new Transform[SegmentCount];
        private readonly SpriteRenderer[] segmentRenderers = new SpriteRenderer[SegmentCount];
        private readonly Vector2[] segmentWorldPositions = new Vector2[SegmentCount];
        private Sprite preTailVisual;
        private Sprite postTailVisual;
        private SpriteRenderer headRenderer;
        private Vector3 previousPosition;
        private Vector2 facing = Vector2.left;
        private bool segmentPositionsInitialized;
        private bool configured;

        public void Configure(Sprite bodySprite, Sprite preTailSprite, Sprite postTailSprite,
            SpriteRenderer head, int sortingOrder)
        {
            if (bodySprite == null || head == null) return;
            headRenderer = head;
            // Gangcheol files contain one complete piece each, unlike Imugi's
            // oversized 32px Aseprite cells, so they must never be cropped.
            preTailVisual = preTailSprite;
            postTailVisual = postTailSprite;
            for (var index = 0; index < segments.Length; index++)
            {
                var sprite = index < BodySegmentCount
                    ? bodySprite
                    : index == BodySegmentCount
                        ? preTailVisual
                        : postTailVisual;
                if (sprite == null) continue;
                var segmentName = index < BodySegmentCount
                    ? $"GangcheoriBody_{index + 1}"
                    : index == BodySegmentCount
                        ? "GangcheoriPreTail"
                        : "GangcheoriPostTail";
                var segmentObject = new GameObject(segmentName);
                segmentObject.transform.SetParent(transform, false);
                var renderer = segmentObject.AddComponent<SpriteRenderer>();
                RuntimePlaceholderVisual.ConfigureSprite(
                    renderer,
                    sprite,
                    sortingOrder - (SegmentCount - 1 - index));
                segmentObject.AddComponent<RuntimeSpriteBoundsHurtbox>().Configure(renderer);
                segments[index] = segmentObject.transform;
                segmentRenderers[index] = renderer;
            }

            facing = headRenderer.flipX ? Vector2.right : Vector2.left;
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
            var tailDirection = Vector2.zero;
            for (var index = 0; index < segments.Length; index++)
            {
                if (segments[index] == null) continue;
                var desiredDistance = SegmentLinkDistance(index) * worldScale;
                if (index >= BodySegmentCount)
                {
                    if (tailDirection.sqrMagnitude <= .000001f)
                    {
                        tailDirection =
                            segmentWorldPositions[BodySegmentCount - 1] -
                            segmentWorldPositions[BodySegmentCount - 2];
                        if (tailDirection.sqrMagnitude <= .000001f)
                            tailDirection = -facing;
                        else
                            tailDirection.Normalize();
                    }
                    segmentWorldPositions[index] =
                        predecessor + tailDirection * desiredDistance;
                }
                else
                {
                    var predecessorOffset = segmentWorldPositions[index] - predecessor;
                    if (predecessorOffset.sqrMagnitude <= .000001f)
                        predecessorOffset = -facing * desiredDistance;
                    var currentDistance = predecessorOffset.magnitude;
                    if (currentDistance > desiredDistance)
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
                var horizontal = Mathf.Abs(axis.x) > Mathf.Abs(axis.y);
                var isTail = index >= BodySegmentCount;
                segments[index].localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    isTail
                        ? horizontal ? 0f : -HorizontalBodyRotation
                        : horizontal ? HorizontalBodyRotation : 0f);
                if (isTail && segmentRenderers[index] != null)
                {
                    segmentRenderers[index].sprite =
                        index == BodySegmentCount ? preTailVisual : postTailVisual;
                    segmentRenderers[index].flipX =
                        horizontal ? axis.x > 0f : axis.y < 0f;
                }
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
                var distance = SegmentLinkDistance(index) * worldScale;
                segmentWorldPositions[index] = predecessor - facing * distance;
                predecessor = segmentWorldPositions[index];
            }
            segmentPositionsInitialized = true;
        }

        private static float SegmentLinkDistance(int index)
        {
            if (index == 0) return FirstSegmentOffset;
            if (index == BodySegmentCount) return PreTailSpacing;
            if (index > BodySegmentCount) return PostTailSpacing;
            return SegmentSpacing;
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
