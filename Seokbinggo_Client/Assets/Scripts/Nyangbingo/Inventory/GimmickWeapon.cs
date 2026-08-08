using System;

namespace Nyangbingo.Inventory
{
    /// <summary>
    /// v38/v46 기믹 무기. 제작·일반 드랍이 아니라 기믹 조건 충족 시에만 지급.
    /// DPS = 동시 티어 무기 × gimmick_weapon_bonus(기본 1.10).
    /// </summary>
    public static class GimmickWeapon
    {
        public const float DefaultBonus = 1.10f;

        public static bool TryGrant(
            string weaponId,
            Func<string, bool> gimmickDone,
            Func<string, bool> alreadyOwned,
            Action<string> grant)
        {
            if (string.IsNullOrWhiteSpace(weaponId) || gimmickDone == null ||
                alreadyOwned == null || grant == null)
                return false;
            if (!gimmickDone(weaponId) || alreadyOwned(weaponId)) return false;
            grant(weaponId);
            return true;
        }

        public static float ScaleDamage(float baseDamage, float bonus = DefaultBonus)
        {
            if (baseDamage <= 0f) return 0f;
            if (float.IsNaN(bonus) || float.IsInfinity(bonus) || bonus <= 0f) bonus = DefaultBonus;
            return baseDamage * bonus;
        }
    }
}
