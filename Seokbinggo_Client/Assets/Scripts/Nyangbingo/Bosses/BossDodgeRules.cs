using System;
using Nyangbingo.Data;

namespace Nyangbingo.Bosses
{
    /// <summary>
    /// globals boss_dodge_sec_curve — 보스 10종 전투 시작 회피(딜 불가) 구간.
    /// </summary>
    public static class BossDodgeRules
    {
        public const string DodgeCurveGlobalKey = "boss_dodge_sec_curve";

        public static bool TryGetOpeningDodgeSeconds(GameDataCatalog catalog, string bossId, out float seconds)
        {
            seconds = 0f;
            if (catalog == null || string.IsNullOrWhiteSpace(bossId)) return false;
            var curve = catalog.FindGlobal(DodgeCurveGlobalKey)?.Value;
            if (string.IsNullOrWhiteSpace(curve)) return false;
            var parts = curve.Split('/');
            if (parts.Length == 0) return false;
            for (var index = 0; index < catalog.Bosses.Count; index++)
            {
                var definition = catalog.Bosses[index];
                if (definition == null || !string.Equals(definition.Id, bossId, StringComparison.Ordinal))
                    continue;
                if (index >= parts.Length || !float.TryParse(parts[index], out seconds) || seconds < 0f)
                    return false;
                return seconds > 0f;
            }

            return false;
        }
    }
}
