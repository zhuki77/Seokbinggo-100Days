using System;
using Nyangbingo.Data;

namespace Nyangbingo.Bosses
{
    /// <summary>
    /// globals boss_dodge_sec_curve — 보스 10종 전투 시작 회피(딜 불가) 구간.
    /// Curve index follows bosses.csv recommended_day order, not GameDataCatalog list order.
    /// </summary>
    public static class BossDodgeRules
    {
        public const string DodgeCurveGlobalKey = "boss_dodge_sec_curve";

        private static readonly string[] DodgeCurveBossOrder =
        {
            "king_dokkaebi",
            "mother_bulgasari",
            "imugi_boss",
            "jigwi",
            "gangcheol_blaze",
            "sangun",
            "samdugumi",
            "eop_guryeongi",
            "yeongno",
            "gangcheol_perfect",
        };

        public static bool TryGetOpeningDodgeSeconds(GameDataCatalog catalog, string bossId, out float seconds)
        {
            seconds = 0f;
            if (catalog == null || string.IsNullOrWhiteSpace(bossId)) return false;
            var curve = catalog.FindGlobal(DodgeCurveGlobalKey)?.Value;
            if (string.IsNullOrWhiteSpace(curve)) return false;
            var parts = curve.Split('/');
            if (parts.Length == 0) return false;
            var index = Array.IndexOf(DodgeCurveBossOrder, bossId);
            if (index < 0 || index >= parts.Length ||
                !float.TryParse(parts[index], out seconds) || seconds < 0f)
                return false;
            return seconds > 0f;
        }
    }
}
