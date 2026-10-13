using UnityEngine;

namespace Nyangbingo.UI
{
    /// <summary>DOS이야기 볼드체. 16px 비트맵 글꼴이다.</summary>
    public static class UiTextFont
    {
        private const string ResourcePath = "Fonts/DOSIyagiBoldface";
        private static Font source;
        private static bool hooked;

        public static Font Source
        {
            get
            {
                EnsureHook();
                if (source != null) return source;
                source = Resources.Load<Font>(ResourcePath);
                if (source == null)
                    source = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                ApplyPointFilter(source);
                return source;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            EnsureHook();
            ApplyPointFilter(Source);
        }

        private static void EnsureHook()
        {
            if (hooked) return;
            hooked = true;
            Font.textureRebuilt += ApplyPointFilter;
        }

        private static void ApplyPointFilter(Font font)
        {
            if (font == null) return;
            var material = font.material;
            if (material != null && material.mainTexture != null)
                material.mainTexture.filterMode = FilterMode.Point;
        }
    }
}
