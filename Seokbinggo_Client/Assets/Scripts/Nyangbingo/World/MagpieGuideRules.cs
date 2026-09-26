using System;
using Nyangbingo.Data;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>
    /// 까치 길잡이 — globals magpie_guide_mode / magpie_guide_return_sec.
    /// 목표 배지와 같은 목록을 쓰고, 줍기와 별도 주기로 목표↔플레이어를 왕복한다.
    /// </summary>
    public static class MagpieGuideRules
    {
        public const string ModeGlobalKey = "magpie_guide_mode";
        public const string ReturnSecondsGlobalKey = "magpie_guide_return_sec";
        public const string FlyToGoalMode = "fly_to_goal";
        public const float DefaultReturnSeconds = 6f;
        public const float GuideLeadTiles = 5f;

        public static bool IsFlyToGoalMode(GameDataCatalog catalog)
        {
            var definition = catalog?.FindGlobal(ModeGlobalKey);
            if (definition == null || string.IsNullOrWhiteSpace(definition.Value))
                return false;
            return string.Equals(definition.Value.Trim(), FlyToGoalMode, StringComparison.Ordinal);
        }

        public static float ResolveReturnSeconds(GameDataCatalog catalog)
        {
            var definition = catalog?.FindGlobal(ReturnSecondsGlobalKey);
            if (definition != null && definition.TryGetFloat(out var value) &&
                value > 0f && !float.IsNaN(value) && !float.IsInfinity(value))
                return value;
            return DefaultReturnSeconds;
        }

        public static Vector2 ResolveLeadPoint(Vector2 playerPosition, Vector2 goalPosition)
        {
            var delta = goalPosition - playerPosition;
            if (delta.sqrMagnitude <= .01f)
                return playerPosition + Vector2.up * .75f;
            var distance = delta.magnitude;
            var lead = Mathf.Min(GuideLeadTiles, distance);
            return playerPosition + delta * (lead / distance);
        }
    }
}
