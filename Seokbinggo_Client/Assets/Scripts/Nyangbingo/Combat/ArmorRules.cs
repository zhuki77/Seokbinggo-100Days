using UnityEngine;

namespace Nyangbingo.Combat
{
    /// <summary>
    /// v40/v46 방어 차감. 실피해는 항상 최소 1 — 방어 합이 접촉 피해를 넘어도 무적이 되지 않는다.
    /// </summary>
    public static class ArmorRules
    {
        public const int DefaultDefenseSumCap = 19;

        public static int EffectiveDamage(int contact, int defSum, int defenseSumCap = DefaultDefenseSumCap)
        {
            if (contact <= 0) return 0;
            var cappedDefense = Mathf.Clamp(defSum, 0, Mathf.Max(0, defenseSumCap));
            return Mathf.Max(1, contact - cappedDefense);
        }
    }
}
