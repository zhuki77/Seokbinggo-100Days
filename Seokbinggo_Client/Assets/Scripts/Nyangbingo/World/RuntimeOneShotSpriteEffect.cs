using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nyangbingo.World
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class RuntimeOneShotSpriteEffect : MonoBehaviour
    {
        [SerializeField] private Sprite[] frames = Array.Empty<Sprite>();
        [Min(.01f)][SerializeField] private float frameSeconds = .1f;
        private SpriteRenderer spriteRenderer;
        private int frameIndex;
        private float frameRemaining;
        private float playRemaining;

        public bool IsPlaying => playRemaining > 0f;

        public void Configure(IReadOnlyList<Sprite> animationFrames, int sortingOrder)
        {
            frames = Copy(animationFrames);
            spriteRenderer = GetComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = sortingOrder;
            spriteRenderer.enabled = false;
            if (frames.Length > 0) spriteRenderer.sprite = frames[0];
        }

        public void Play(float minimumSeconds)
        {
            if (frames.Length == 0) return;
            spriteRenderer ??= GetComponent<SpriteRenderer>();
            frameIndex = 0;
            frameRemaining = frameSeconds;
            playRemaining = Mathf.Max(frameSeconds * frames.Length, minimumSeconds);
            spriteRenderer.sprite = frames[0];
            spriteRenderer.enabled = true;
        }

        private void Update()
        {
            if (!IsPlaying || spriteRenderer == null) return;
            var delta = Time.unscaledDeltaTime;
            playRemaining = Mathf.Max(0f, playRemaining - delta);
            frameRemaining -= delta;
            while (frameRemaining <= 0f && playRemaining > 0f)
            {
                frameIndex = (frameIndex + 1) % frames.Length;
                spriteRenderer.sprite = frames[frameIndex];
                frameRemaining += frameSeconds;
            }
            if (playRemaining <= 0f) spriteRenderer.enabled = false;
        }

        private static Sprite[] Copy(IReadOnlyList<Sprite> source)
        {
            if (source == null) return Array.Empty<Sprite>();
            var copy = new Sprite[source.Count];
            for (var index = 0; index < copy.Length; index++) copy[index] = source[index];
            return copy;
        }
    }
}
