using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>
    /// 이무기 머리 뒤에 단일 몸통 타일을 반복 배치하는 장식 전용 비주얼이다.
    /// 전투 충돌과 피해 판정은 기존 보스 루트의 콜라이더를 그대로 사용한다.
    /// </summary>
    public sealed class RuntimeImugiBodyVisual : MonoBehaviour
    {
        private const int SegmentCount = 4;
        private const float SegmentSpacing = .68f;
        private const float WaveAmplitude = .1f;

        private readonly Transform[] segments = new Transform[SegmentCount];
        private Vector3 previousPosition;
        private Vector2 facing = Vector2.left;
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
                segments[index] = segmentObject.transform;
            }

            previousPosition = transform.position;
            configured = true;
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
            var perpendicular = new Vector2(-facing.y, facing.x);
            for (var index = 0; index < segments.Length; index++)
            {
                if (segments[index] == null) continue;
                var distance = SegmentSpacing * (index + 1);
                var wave = Mathf.Sin(Time.time * 5f - index * .8f) * WaveAmplitude;
                var localPosition = -facing * distance + perpendicular * wave;
                segments[index].localPosition = new Vector3(localPosition.x, localPosition.y, .01f);
            }
        }
    }
}
