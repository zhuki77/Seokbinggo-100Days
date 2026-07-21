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

    [RequireComponent(typeof(Image))]
    public sealed class RuntimeDayCounterScrollPresenter : MonoBehaviour
    {
        public const float DeliveredPixelToLogicalScale = .82f;
        private enum PlaybackPhase { Opening, Holding, Closing }

        [Min(.01f)] [SerializeField] private float frameSeconds = .08f;
        private Sprite[] frames = Array.Empty<Sprite>();
        private Image image;
        private PlaybackPhase phase;
        private int frameIndex;
        private float remaining;
        private int pendingDaysRemaining;

        public int DisplayedDaysRemaining { get; private set; }
        public bool IsAnimating => phase != PlaybackPhase.Holding;
        public bool IsFullyOpen => phase == PlaybackPhase.Holding;

        public void ConfigureForRuntime(IReadOnlyList<Sprite> sourceFrames, int initialDaysRemaining)
        {
            image = GetComponent<Image>();
            var validFrames = new List<Sprite>();
            if (sourceFrames != null)
                for (var index = 0; index < sourceFrames.Count; index++)
                    if (sourceFrames[index] != null) validFrames.Add(sourceFrames[index]);
            frames = validFrames.ToArray();
            DisplayedDaysRemaining = pendingDaysRemaining = Mathf.Max(0, initialDaysRemaining);
            frameIndex = 0;
            phase = frames.Length > 1 ? PlaybackPhase.Opening : PlaybackPhase.Holding;
            remaining = frameSeconds;
            ApplyFrame();
        }

        public void SetDaysRemaining(int daysRemaining)
        {
            var clamped = Mathf.Max(0, daysRemaining);
            if (clamped == pendingDaysRemaining) return;
            pendingDaysRemaining = clamped;
            if (phase == PlaybackPhase.Holding)
            {
                phase = PlaybackPhase.Closing;
                frameIndex = Mathf.Max(0, frames.Length - 1);
                remaining = frameSeconds;
            }
        }

        private void Update()
        {
            if (frames.Length <= 1 || phase == PlaybackPhase.Holding) return;
            remaining -= Time.unscaledDeltaTime;
            while (remaining <= 0f && phase != PlaybackPhase.Holding)
            {
                if (phase == PlaybackPhase.Opening)
                {
                    if (frameIndex < frames.Length - 1) frameIndex++;
                    else if (DisplayedDaysRemaining != pendingDaysRemaining)
                        phase = PlaybackPhase.Closing;
                    else
                        phase = PlaybackPhase.Holding;
                }
                else
                {
                    if (frameIndex > 0) frameIndex--;
                    else
                    {
                        DisplayedDaysRemaining = pendingDaysRemaining;
                        phase = PlaybackPhase.Opening;
                    }
                }
                ApplyFrame();
                remaining += frameSeconds;
            }
        }

        private void ApplyFrame()
        {
            if (image == null || frames.Length == 0)
            {
                if (image != null) image.enabled = false;
                return;
            }
            image.enabled = true;
            image.sprite = frames[Mathf.Clamp(frameIndex, 0, frames.Length - 1)];
            // Aseprite sprites use 16 PPU while the HUD treats delivered pixels as logical UI pixels.
            // Image.SetNativeSize() divides by that PPU and inflates the scroll across most of the screen.
            image.rectTransform.sizeDelta = image.sprite.rect.size * DeliveredPixelToLogicalScale;
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
                // The delivered hover frame is the darkened variant. Reuse it for disabled
                // controls so unavailable actions remain visibly distinct without extra art.
                disabledSprite = frames[1]
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
