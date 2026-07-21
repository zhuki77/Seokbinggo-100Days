using System;
using System.Collections.Generic;
using Nyangbingo.Data;
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

    /// <summary>Delivered pixel UI art adapter for runtime-created and scene-authored buttons.</summary>
    public static class RuntimeUiButtonArt
    {
        public static void Apply(Button button, GameplayArtCatalog catalog)
        {
            if (button == null || catalog == null || !(button.targetGraphic is Image image)) return;

            var rect = button.transform as RectTransform;
            var size = rect != null ? rect.rect.size : Vector2.zero;
            var aspect = size.y > .01f ? size.x / size.y : 1f;
            var frames = ClosestFrames(catalog, aspect);
            if (frames == null || frames.Count < 2 || frames[0] == null || frames[1] == null) return;

            image.sprite = frames[0];
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = Color.white;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                highlightedSprite = frames[1],
                pressedSprite = frames[1],
                selectedSprite = frames[1],
                disabledSprite = frames[0]
            };
        }

        public static void ApplyCodexCard(Button button, GameplayArtCatalog catalog)
        {
            if (button == null || catalog == null || catalog.CodexCard == null ||
                !(button.targetGraphic is Image image)) return;
            ApplyCodexCard(image, catalog);
            button.transition = Selectable.Transition.ColorTint;
        }

        public static void ApplyCodexCard(Image image, GameplayArtCatalog catalog)
        {
            if (image == null || catalog == null || catalog.CodexCard == null) return;
            image.sprite = catalog.CodexCard;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = Color.white;
        }

        private static IReadOnlyList<Sprite> ClosestFrames(GameplayArtCatalog catalog, float aspect)
        {
            if (aspect < 1.5f) return catalog.Button1x1Frames;
            if (aspect < 3f) return catalog.Button1x2Frames;
            if (aspect < 5f) return catalog.Button1x4Frames;
            return catalog.Button1x6Frames;
        }
    }
}
