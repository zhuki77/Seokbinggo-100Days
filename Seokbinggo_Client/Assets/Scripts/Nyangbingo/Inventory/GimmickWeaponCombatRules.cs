using System;
using Nyangbingo.Data;
using Nyangbingo.World;

namespace Nyangbingo.Inventory
{
    /// <summary>
    /// v46 기믹 무기 장착·전투 프로필·보스 상성 훅.
    /// </summary>
    public static class GimmickWeaponCombatRules
    {
        private static readonly string[] AllIds =
        {
            GimmickWeaponProgress.FirstFrostClawId,
            GimmickWeaponProgress.BaekjungBundleId,
            GimmickWeaponProgress.YeouijuClawId,
            GimmickWeaponProgress.JigwiAshId,
            GimmickWeaponProgress.SangunWhiskerId,
            GimmickWeaponProgress.YeongnoToothId
        };

        public static bool IsGimmickWeaponId(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return false;
            for (var index = 0; index < AllIds.Length; index++)
            {
                if (string.Equals(itemId, AllIds[index], StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        public static bool IsActiveProfile(MainGamePlayerController player, string gimmickWeaponId) =>
            player != null &&
            !string.IsNullOrWhiteSpace(gimmickWeaponId) &&
            string.Equals(player.ActiveCombatProfileId, gimmickWeaponId, StringComparison.Ordinal);

        public static float ResolveBonus(GameDataCatalog catalog)
        {
            var bonus = GimmickWeapon.DefaultBonus;
            var definition = catalog?.FindGlobal(GlobalKeys.GimmickWeaponBonus);
            if (definition != null && definition.TryGetFloat(out var configuredBonus) &&
                configuredBonus > 0f && !float.IsNaN(configuredBonus) && !float.IsInfinity(configuredBonus))
                bonus = configuredBonus;
            return bonus;
        }

        public static CombatProfileDefinition CreateScaledProfile(
            string gimmickWeaponId, CombatProfileDefinition baseProfile, GameDataCatalog catalog)
        {
            if (string.IsNullOrWhiteSpace(gimmickWeaponId) || baseProfile == null) return null;
            var bonus = ResolveBonus(catalog);
            return CombatProfileDefinition.CreateRuntime(
                gimmickWeaponId,
                baseProfile.Tier,
                baseProfile.HasBasicAttack,
                GimmickWeapon.ScaleDamage(baseProfile.AttackDamage, bonus),
                baseProfile.AttacksPerSecond,
                GimmickWeapon.ScaleDamage(baseProfile.DamagePerSecond, bonus),
                baseProfile.KnockbackTiles,
                baseProfile.RangeTiles,
                baseProfile.ArcDegrees,
                baseProfile.MultiTarget,
                baseProfile.HitsWalls);
        }
    }
}
