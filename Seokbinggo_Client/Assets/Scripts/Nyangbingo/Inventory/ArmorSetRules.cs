using System;
using Nyangbingo.Data;

namespace Nyangbingo.Inventory
{
    /// <summary>
    /// GDD: 세트 효과는 스코프별 최상위 티어만.
    /// 30일=seolhanpung(T3), 100일=hanpa(T6). T4/T5는 세트 없음(방어만).
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

        public static readonly string[] SeongePieceIds =
            { "seonge_helm", "seonge_armor", "seonge_boots" };
        public static readonly string[] IceRootPieceIds =
            { "ice_root_helm", "ice_root_armor", "ice_root_boots" };
        public static readonly string[] ColdWavePieceIds =
            { "cold_wave_helm", "cold_wave_armor", "cold_wave_boots" };

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

        public static bool IsEvolutionArmorId(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return false;
            return ContainsId(SeongePieceIds, itemId) ||
                   ContainsId(IceRootPieceIds, itemId) ||
                   ContainsId(ColdWavePieceIds, itemId);
        }

        private static bool ContainsId(string[] ids, string itemId)
        {
            for (var index = 0; index < ids.Length; index++)
                if (string.Equals(ids[index], itemId, StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static bool Approximately(float a, float b) => Math.Abs(a - b) <= 0.0001f;
    }
}
