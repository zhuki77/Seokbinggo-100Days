using System;
using UnityEngine;
using UnityEngine.UI;

namespace Nyangbingo.UI
{
    [RequireComponent(typeof(Image))]
    public sealed class RuntimeUiSpriteAnimator : MonoBehaviour
    {
        [SerializeField] private Sprite[] frames = Array.Empty<Sprite>();
        [Min(.01f)][SerializeField] private float frameSeconds = .1f;
        private Image image;
        private int frameIndex;
        private float remaining;

        public void ConfigureForScene(Sprite[] animationFrames, float secondsPerFrame)
        {
            frames = animationFrames ?? Array.Empty<Sprite>();
            frameSeconds = Mathf.Max(.01f, secondsPerFrame);
            image = GetComponent<Image>();
            if (image != null && frames.Length > 0) image.sprite = frames[0];
        }

        private void Awake()
        {
            image = GetComponent<Image>();
            remaining = frameSeconds;
            if (image != null && frames.Length > 0) image.sprite = frames[0];
        }

        private void Update()
        {
            if (image == null || frames.Length <= 1) return;
            remaining -= Time.unscaledDeltaTime;
            while (remaining <= 0f)
            {
                frameIndex = (frameIndex + 1) % frames.Length;
                image.sprite = frames[frameIndex];
                remaining += frameSeconds;
            }
        }
    }
}
