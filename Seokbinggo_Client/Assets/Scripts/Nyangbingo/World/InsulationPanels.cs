using System;
using System.Collections.Generic;
using Nyangbingo.Data;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>
    /// v36/v46 단열재 패널 합산. 장당 티어 보너스를 더하고 상한 6장.
    /// 벽지(전부 or 0)와 달리 부분 합산이 가능하다.
    /// </summary>
    public static class InsulationPanels
    {
        public const int DefaultCap = 6;
        public const float DefaultStrawBonus = 0.05f;
        public const string StrawInsulationId = "straw_insul";
        public const string ClayPlasterId = "clay_plaster";

        public static int TierForDefinition(string definitionId)
        {
            if (string.Equals(definitionId, ClayPlasterId, StringComparison.Ordinal))
                return 2;
            if (string.Equals(definitionId, StrawInsulationId, StringComparison.Ordinal))
                return 1;
            return 1;
        }

        public static float TotalFromDefinitions(IEnumerable<string> definitionIds, GlobalSettings settings = null)
        {
            if (definitionIds == null) return 0f;
            var tiers = new List<int>();
            foreach (var id in definitionIds)
            {
                if (string.IsNullOrWhiteSpace(id)) continue;
                if (!string.Equals(id, StrawInsulationId, StringComparison.Ordinal) &&
                    !string.Equals(id, ClayPlasterId, StringComparison.Ordinal))
                    continue;
                tiers.Add(TierForDefinition(id));
            }
            return Total(tiers, settings);
        }

        public static float Total(IReadOnlyList<int> panelTiers, GlobalSettings settings = null)
        {
            if (panelTiers == null || panelTiers.Count == 0) return 0f;
            var cap = DefaultCap;
            var straw = DefaultStrawBonus;
            if (settings != null &&
                settings.TryGetFloat(GlobalKeys.InsulStrawBonus, out var configured) &&
                configured > 0f)
                straw = configured;

            // 티어 보너스: 1=짚, 2=점토/얼음강철 계열 확장 자리. globals 세분화 전엔 straw*tier 근사.
            var sum = 0f;
            var count = 0;
            for (var i = 0; i < panelTiers.Count && count < cap; i++)
            {
                var tier = Mathf.Max(1, panelTiers[i]);
                sum += straw * tier;
                count++;
            }

            return Mathf.Clamp01(sum);
        }
    }
}
