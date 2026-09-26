using System;
using Nyangbingo.Data;
using UnityEngine;

namespace Nyangbingo.Combat
{
    /// <summary>
    /// 발톱 T5/T6 진화 무기 전투 규칙 — 산군 발톱 3연격, 완전체 발톱 광역 할퀴기.
    /// </summary>
    public static class EvolvedClawCombatRules
    {
        public const string SangunClawId = "sangun_claw";
        public const string PerfectClawId = "perfect_claw";
        public const int SangunComboHitCount = 3;
        public const int PerfectClawMaxTargets = 8;

        public static bool IsSangunClaw(CombatProfileDefinition profile) =>
            profile != null &&
            string.Equals(profile.Id, SangunClawId, StringComparison.Ordinal);

        public static bool IsPerfectClaw(CombatProfileDefinition profile) =>
            profile != null &&
            string.Equals(profile.Id, PerfectClawId, StringComparison.Ordinal);

        public static bool IsEvolvedClaw(CombatProfileDefinition profile) =>
            IsSangunClaw(profile) || IsPerfectClaw(profile);

        public static int ResolveMaxTargets(CombatProfileDefinition profile)
        {
            if (profile == null) return 1;
            if (IsPerfectClaw(profile)) return PerfectClawMaxTargets;
            return profile.MaxTargets;
        }

        public static void SplitSangunComboDamage(int totalDamage, int hitIndex, out int hitDamage, out float knockbackTiles,
            float profileKnockbackTiles)
        {
            var safeTotal = Mathf.Max(1, totalDamage);
            var perHit = Mathf.Max(1, safeTotal / SangunComboHitCount);
            var remainder = safeTotal - perHit * SangunComboHitCount;
            var isFinisher = hitIndex >= SangunComboHitCount - 1;
            hitDamage = perHit + (isFinisher ? remainder : 0);
            knockbackTiles = isFinisher ? Mathf.Max(0f, profileKnockbackTiles) : 0f;
        }
    }
}
