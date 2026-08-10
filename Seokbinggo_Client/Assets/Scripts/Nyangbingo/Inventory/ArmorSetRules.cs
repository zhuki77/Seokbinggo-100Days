using System;
using Nyangbingo.Data;

namespace Nyangbingo.Inventory
{
    /// <summary>
    /// GDD: 세트 효과는 스코프별 최상위 티어만.
    /// 30일=seolhanpung(T3), 100일=hanpa(T6). T4/T5는 세트 없음.
    /// </summary>
    public static class ArmorSetRules
    {
        public const string SeolhanpungSetId = "seolhanpung";
        public const string HanpaSetId = "hanpa";

        public const float SeolhanpungTemperatureRise = -0.20f;
        public const float SeolhanpungFireDamage = -0.25f;
        public const float HanpaTemperatureRise = -0.40f;
        public const float HanpaFireDamage = -0.45f;

        /// <summary>얼음심장(-0.15)+한파(-0.40)까지 허용하는 합연산 하한.</summary>
        public const float TemperatureRiseFloor = -0.55f;

        public static bool IsKnownTopTierSet(string setId) =>
            string.Equals(setId, SeolhanpungSetId, StringComparison.Ordinal) ||
            string.Equals(setId, HanpaSetId, StringComparison.Ordinal);

        public static bool MatchesCanonicalBonuses(EquipmentDefinition piece)
        {
            if (piece == null || string.IsNullOrWhiteSpace(piece.SetId)) return false;
            if (string.Equals(piece.SetId, SeolhanpungSetId, StringComparison.Ordinal))
                return Approximately(piece.SetTemperatureRiseModifier, SeolhanpungTemperatureRise) &&
                       Approximately(piece.SetFireDamageModifier, SeolhanpungFireDamage);
            if (string.Equals(piece.SetId, HanpaSetId, StringComparison.Ordinal))
                return Approximately(piece.SetTemperatureRiseModifier, HanpaTemperatureRise) &&
                       Approximately(piece.SetFireDamageModifier, HanpaFireDamage);
            return false;
        }

        private static bool Approximately(float a, float b) => Math.Abs(a - b) <= 0.0001f;
    }
}
