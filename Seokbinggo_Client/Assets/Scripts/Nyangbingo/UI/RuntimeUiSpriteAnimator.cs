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

    /// <summary>
    /// Renders the delivered D-day and clock glyphs without falling back to a system font.
    /// Catalog order is D, dash, colon, then digits zero through nine.
    /// </summary>
    public sealed class RuntimePixelGlyphPresenter : MonoBehaviour
    {
        public const int ExpectedGlyphCount = 13;
        private const float SpacingPixels = 1f;

        private readonly List<Image> images = new List<Image>();
        private Sprite[] glyphs = Array.Empty<Sprite>();
        private RectTransform content;
        private float glyphScale = 1f;
        private string displayedText = string.Empty;

        public string DisplayedText => displayedText;
        public int VisibleGlyphCount => images.Count;
        public float RenderedWidth => content != null ? content.sizeDelta.x : 0f;

        public void ConfigureForRuntime(IReadOnlyList<Sprite> source, float scale = 1f)
        {
            glyphs = source != null && source.Count == ExpectedGlyphCount
                ? Copy(source)
                : Array.Empty<Sprite>();
            glyphScale = Mathf.Max(.1f, scale);
            EnsureContent();
            RefreshImages();
        }

        public void SetText(string value)
        {
            value ??= string.Empty;
            if (displayedText == value) return;
            displayedText = value;
            RefreshImages();
        }

        public void SetVisible(bool visible)
        {
            EnsureContent();
            content.gameObject.SetActive(visible && glyphs.Length == ExpectedGlyphCount);
        }

        public static int GlyphIndex(char character)
        {
            if (character == 'D' || character == 'd') return 0;
            if (character == '-') return 1;
            if (character == ':' || character == '.') return 2;
            return character >= '0' && character <= '9' ? 3 + character - '0' : -1;
        }

        private void EnsureContent()
        {
            if (content != null) return;
            var existing = transform.Find("PixelGlyphs") as RectTransform;
            if (existing != null)
            {
                content = existing;
                return;
            }
            var root = new GameObject("PixelGlyphs", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            content = (RectTransform)root.transform;
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f, .5f);
            content.anchoredPosition = Vector2.zero;
        }

        private void RefreshImages()
        {
            EnsureContent();
            foreach (var image in images)
                if (image != null)
                {
                    if (Application.isPlaying) Destroy(image.gameObject);
                    else DestroyImmediate(image.gameObject);
                }
            images.Clear();

            if (glyphs.Length != ExpectedGlyphCount)
            {
                content.gameObject.SetActive(false);
                return;
            }

            var characters = new List<char>();
            foreach (var character in displayedText)
            {
                var index = GlyphIndex(character);
                if (character == '/' || character == '.' ||
                    index >= 0 && index < glyphs.Length && glyphs[index] != null)
                    characters.Add(character);
            }

            var width = 0f;
            for (var index = 0; index < characters.Count; index++)
            {
                width += LayoutSize(characters[index]).x;
                if (index + 1 < characters.Count) width += SpacingPixels * glyphScale;
            }
            var cursor = -width * .5f;
            var maximumHeight = 0f;
            foreach (var character in characters)
            {
                var size = LayoutSize(character);
                var imageObject = new GameObject("Glyph", typeof(RectTransform), typeof(Image));
                imageObject.transform.SetParent(content, false);
                var image = imageObject.GetComponent<Image>();
                image.color = Color.white;
                image.preserveAspect = true;
                image.raycastTarget = false;
                var rect = image.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = new Vector2(cursor + size.x * .5f, 0f);
                if (character == '/')
                {
                    image.sprite = null;
                    image.preserveAspect = false;
                    rect.sizeDelta = new Vector2(1.5f, 11f) * glyphScale;
                    rect.localRotation = Quaternion.Euler(0f, 0f, -25f);
                }
                else if (character == '.')
                {
                    image.sprite = null;
                    image.preserveAspect = false;
                    rect.sizeDelta = new Vector2(2f, 2f) * glyphScale;
                    rect.anchoredPosition += Vector2.down * 5.5f * glyphScale;
                }
                else
                {
                    image.sprite = glyphs[GlyphIndex(character)];
                    rect.sizeDelta = size;
                }
                cursor += size.x + SpacingPixels * glyphScale;
                maximumHeight = Mathf.Max(maximumHeight, size.y);
                images.Add(image);
            }
            content.sizeDelta = new Vector2(width, maximumHeight);
            content.gameObject.SetActive(true);
        }

        private Vector2 LayoutSize(char character)
        {
            if (character == '/') return new Vector2(5f, 15f) * glyphScale;
            if (character == '.') return new Vector2(4f, 15f) * glyphScale;
            var index = GlyphIndex(character);
            return index >= 0 && index < glyphs.Length && glyphs[index] != null
                ? glyphs[index].rect.size * glyphScale
                : Vector2.zero;
        }

        private static Sprite[] Copy(IReadOnlyList<Sprite> source)
        {
            var result = new Sprite[source.Count];
            for (var index = 0; index < source.Count; index++) result[index] = source[index];
            return result;
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
