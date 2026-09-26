using System;
using Nyangbingo.Bosses;
using Nyangbingo.Combat;
using Nyangbingo.Data;
using Nyangbingo.Yokai;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>
    /// traits.csv / globals 오너 승인값. 시작 1회 분기 — 성장 트리 아님.
    /// </summary>
    public static class TraitRules
    {
        public const string MeleeId = "trait_melee";
        public const string RangedId = "trait_ranged";
        public const string LaborId = "trait_labor";
        public const string CoolId = "trait_cool";
        public const string SelectAtStartKey = "trait_select_at_start";
        public const string MeleeBossExemptKey = "trait_melee_boss_exempt";
        public const string RangedStartItemId = "straw_sling";
        public const float MeleeYokaiDamageMultiplier = 1.1f;
        public const float LaborMiningCriticalBonus = .05f;
        public const float CoolDayTemperatureRiseMultiplier = .9f;

        public static readonly string[] AllIds =
        {
            MeleeId, RangedId, LaborId, CoolId
        };

        public static bool IsKnownId(string traitId)
        {
            if (string.IsNullOrWhiteSpace(traitId)) return false;
            for (var i = 0; i < AllIds.Length; i++)
                if (string.Equals(AllIds[i], traitId, StringComparison.Ordinal))
                    return true;
            return false;
        }

        public static bool ShouldSelectAtStart(GameDataCatalog catalog)
        {
            var definition = catalog?.FindGlobal(SelectAtStartKey);
            return definition == null || !definition.TryGetInt(out var flag) || flag != 0;
        }

        public static bool MeleeBossExempt(GameDataCatalog catalog)
        {
            var definition = catalog?.FindGlobal(MeleeBossExemptKey);
            return definition == null || !definition.TryGetInt(out var flag) || flag != 0;
        }

        public static int AdjustMeleeDamage(string traitId, GameDataCatalog catalog, Health target, int damage)
        {
            if (damage <= 0 || traitId != MeleeId || target == null) return damage;
            if (MeleeBossExempt(catalog) && target.GetComponentInParent<BossCombatController>() != null)
                return damage;
            if (target.GetComponentInParent<YokaiBrain>() == null) return damage;
            return Mathf.Max(1, Mathf.RoundToInt(damage * MeleeYokaiDamageMultiplier));
        }
    }
}
