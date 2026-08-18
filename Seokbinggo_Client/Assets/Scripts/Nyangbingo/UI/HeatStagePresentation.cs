using UnityEngine;

namespace Nyangbingo.UI
{
    /// <summary>
    /// B-UI-v72: 네임드 처치 기반 폭염 단계(1/2/3)를 표시한다.
    /// </summary>
    public static class HeatStagePresentation
    {
        public const int StageCount = 3;
        public static string FormatBadge(int heatStage) =>
            Mathf.Clamp(heatStage, 1, StageCount).ToString();
    }
}
