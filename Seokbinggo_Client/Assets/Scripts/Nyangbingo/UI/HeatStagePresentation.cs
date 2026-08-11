using UnityEngine;

namespace Nyangbingo.UI
{
    /// <summary>
    /// B-UI-v71: D-100 카운터 자리를 폭염 단계(1/2/3)로 대체할 때 쓰는 표시·역산 헬퍼.
    /// day-curve에 단계가 있으면 그쪽이 정본이고, 없으면 period=10 근사(1~10→1, 11~20→2, 21+→3).
    /// </summary>
    public static class HeatStagePresentation
    {
        public const int StageCount = 3;
        public const int DefaultPeriodDays = 10;

        public static int ResolveForDay(int day, int periodDays = DefaultPeriodDays)
        {
            if (day < 1) day = 1;
            if (periodDays < 1) periodDays = DefaultPeriodDays;
            return Mathf.Clamp((day - 1) / periodDays + 1, 1, StageCount);
        }

        public static string FormatBadge(int heatStage) =>
            Mathf.Clamp(heatStage, 1, StageCount).ToString();
    }
}
