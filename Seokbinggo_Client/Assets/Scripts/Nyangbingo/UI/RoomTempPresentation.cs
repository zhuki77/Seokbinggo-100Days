using UnityEngine;

namespace Nyangbingo.UI
{
    /// <summary>
    /// B-UI-v71 B-1: 실온(℃) 표시·밴드색. 0~-4 온난 / -5~-9 냉장 / -10↓ 빙결.
    /// </summary>
    public static class RoomTempPresentation
    {
        public const int DefaultColdEnter = -5;
        public const int DefaultFrozenEnter = -10;

        public enum Band { Warm, Chilled, Frozen }

        public static Band ResolveBand(int celsius, int coldEnter = DefaultColdEnter,
            int frozenEnter = DefaultFrozenEnter)
        {
            if (celsius <= frozenEnter) return Band.Frozen;
            if (celsius <= coldEnter) return Band.Chilled;
            return Band.Warm;
        }

        public static string FormatCelsius(int celsius) => $"{celsius}℃";

        public static Color BandColor(Band band)
        {
            switch (band)
            {
                case Band.Chilled: return new Color(.45f, .72f, 1f, 1f);
                case Band.Frozen: return new Color(.18f, .42f, .82f, 1f);
                default: return new Color(.95f, .88f, .72f, 1f);
            }
        }

        public static bool ShouldWarnHypothermia(int celsius, int frozenEnter = DefaultFrozenEnter) =>
            celsius <= frozenEnter;
    }
}
