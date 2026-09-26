using System;
using Nyangbingo.Data;

namespace Nyangbingo.Combat
{
    /// <summary>
    /// 활 계열 T1~T6 — 장착·탄약(돌)·좁은 사선 판정.
    /// </summary>
    public static class BowCombatRules
    {
        public const string StrawSlingId = "straw_sling";
        public const string GakgungId = "gakgung";
        public const string SingijeonSondaeId = "singijeon_sondae";
        public const string SeongeGakgungId = "seonge_gakgung";
        public const string IceRootBowId = "ice_root_bow";
        public const string ColdWaveSingijeonId = "cold_wave_singijeon";
        public const string AmmoItemId = "stone";
        public const float BeamHalfArcDegrees = 12f;

        private static readonly string[] AllIds =
        {
            StrawSlingId, GakgungId, SingijeonSondaeId,
            SeongeGakgungId, IceRootBowId, ColdWaveSingijeonId
        };

        public static bool IsBowWeaponId(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return false;
            for (var index = 0; index < AllIds.Length; index++)
            {
                if (string.Equals(itemId, AllIds[index], StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        public static bool IsBowProfile(CombatProfileDefinition profile) =>
            profile != null && IsBowWeaponId(profile.Id);

        public static bool TryConsumeAmmo(Nyangbingo.Inventory.Inventory inventory)
        {
            if (inventory == null) return false;
            return inventory.TryRemove(AmmoItemId, 1);
        }

        public static float ResolveHalfArcDegrees(float profileArcDegrees)
        {
            if (float.IsNaN(profileArcDegrees) || float.IsInfinity(profileArcDegrees))
                return BeamHalfArcDegrees;
            if (profileArcDegrees < 1f) return BeamHalfArcDegrees;
            return profileArcDegrees * .5f;
        }
    }
}
