using System;
using Nyangbingo.Data;
using UnityEngine;

namespace Nyangbingo.Combat
{
    /// <summary>
    /// 선(부채) T3~T6 진화 — 서리 부채·채찍선 끌어당김·한파 부채 광역.
    /// </summary>
    public static class EvolvedFanCombatRules
    {
        public const string SeongeFanId = FanItemIds.SeongeFan;
        public const string IceRootWhipfanId = FanItemIds.IceRootWhipfan;
        public const string ColdWaveFanId = FanItemIds.ColdWaveFan;

        public static bool IsFanAbilityWeapon(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId)) return false;
            return string.Equals(profileId, FanItemIds.Hapjukseon, StringComparison.Ordinal) ||
                   string.Equals(profileId, FanItemIds.Cheolseon, StringComparison.Ordinal) ||
                   string.Equals(profileId, FanItemIds.Seolpungseon, StringComparison.Ordinal) ||
                   string.Equals(profileId, SeongeFanId, StringComparison.Ordinal) ||
                   string.Equals(profileId, IceRootWhipfanId, StringComparison.Ordinal) ||
                   string.Equals(profileId, ColdWaveFanId, StringComparison.Ordinal);
        }

        public static bool IsPullFan(CombatProfileDefinition profile) =>
            profile != null &&
            string.Equals(profile.Id, IceRootWhipfanId, StringComparison.Ordinal);

        public static bool IsPullFanId(string profileId) =>
            string.Equals(profileId, IceRootWhipfanId, StringComparison.Ordinal);

        public static Vector2 ResolveDisplacement(Vector2 awayFromAttacker, float magnitude,
            CombatProfileDefinition profile)
        {
            if (awayFromAttacker.sqrMagnitude <= Mathf.Epsilon || magnitude <= 0f)
                return Vector2.zero;
            var direction = awayFromAttacker.normalized;
            if (IsPullFan(profile))
                direction = -direction;
            return direction * magnitude;
        }

        public static float ResolveAbilityKnockback(CombatProfileDefinition profile)
        {
            if (profile != null &&
                !float.IsNaN(profile.KnockbackTiles) &&
                !float.IsInfinity(profile.KnockbackTiles) &&
                profile.KnockbackTiles > 0f)
                return profile.KnockbackTiles;
            return WireSnareAbility.Knockback;
        }
    }
}
