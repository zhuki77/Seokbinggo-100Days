using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nyangbingo.World
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class RuntimeBuildingSpriteAnimator : MonoBehaviour
    {
        [SerializeField] private Sprite[] frames = Array.Empty<Sprite>();
        [Min(.01f)][SerializeField] private float frameSeconds = .1f;
        private SpriteRenderer spriteRenderer;
        private int frameIndex;
        private float remaining;

        public void Configure(IReadOnlyList<Sprite> animationFrames, float secondsPerFrame = .1f)
        {
            frames = animationFrames == null ? Array.Empty<Sprite>() : Copy(animationFrames);
            frameSeconds = Mathf.Max(.01f, secondsPerFrame);
            spriteRenderer = GetComponent<SpriteRenderer>();
            frameIndex = 0;
            remaining = frameSeconds;
            if (spriteRenderer != null && frames.Length > 0) spriteRenderer.sprite = frames[0];
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            remaining = frameSeconds;
            if (spriteRenderer != null && frames.Length > 0) spriteRenderer.sprite = frames[0];
        }

        private void Update()
        {
            if (spriteRenderer == null || frames.Length <= 1) return;
            remaining -= Time.deltaTime;
            while (remaining <= 0f)
            {
                frameIndex = (frameIndex + 1) % frames.Length;
                spriteRenderer.sprite = frames[frameIndex];
                remaining += frameSeconds;
            }
        }

        private static Sprite[] Copy(IReadOnlyList<Sprite> source)
        {
            var copy = new Sprite[source.Count];
            for (var index = 0; index < copy.Length; index++) copy[index] = source[index];
            return copy;
        }
    }
}
