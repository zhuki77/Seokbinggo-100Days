using Nyangbingo.Data;

namespace Nyangbingo.Core
{
    /// <summary>v72의 MVP(A)와 31일 이후 확장(B)을 같은 카탈로그에서 안전하게 전환한다.</summary>
    public static class ExpansionProgressionRules
    {
        public const int ExpansionStartDay = 31;

        public static bool IsScopeAvailable(ItemMvpScope scope, int day) =>
            scope != ItemMvpScope.B || day >= ExpansionStartDay;

        public static bool ShouldHideScopeB(string policy, int day) =>
            string.Equals(policy, "hidden", System.StringComparison.OrdinalIgnoreCase) &&
            day < ExpansionStartDay;
    }
}
