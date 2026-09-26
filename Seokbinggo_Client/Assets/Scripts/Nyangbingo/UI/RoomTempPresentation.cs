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

        /// <summary>
        /// B-c: 실온이 저체온 임계(기본 −10) 이하일 때 체온 바 상태 아이콘을 켠다.
        /// </summary>
        public static bool ShouldShowHypothermiaStatusIcon(int roomCelsius,
            int frozenEnter = DefaultFrozenEnter) =>
            ShouldWarnHypothermia(roomCelsius, frozenEnter);

        /// <summary>
        /// B-c: 체온이 피해 시작점(기본 0)에 가까워지면 아이콘을 강조한다.
        /// </summary>
        public static bool ShouldEmphasizeHypothermiaStatusIcon(float bodyTemperature,
            float damageAtTemperature, float warnWindow = 10f) =>
            !float.IsNaN(bodyTemperature) && !float.IsInfinity(bodyTemperature) &&
            bodyTemperature <= damageAtTemperature + Mathf.Max(0f, warnWindow);
    }
}
