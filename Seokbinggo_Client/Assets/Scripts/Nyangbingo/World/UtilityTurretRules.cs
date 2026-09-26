using System;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>
    /// 유틸 터렛(허수아비·얼음 함정·화살 보급·미장이·징) — items.csv 노트 기준 상수.
    /// </summary>
    public static class UtilityTurretRules
    {
        public const string ScarecrowId = "scarecrow";
        public const string IceTrapId = "ice_trap";
        public const string ArrowSupplyId = "arrow_supply";
        public const string PlasterDollId = "plaster_doll";
        public const string GongTowerId = "gong_tower";

        public const float ScarecrowRadiusTiles = 6f;
        public const float ScarecrowAggroSeconds = 12f;
        public const float IceTrapRadiusTiles = 1.15f;
        public const float IceTrapFreezeFraction = 1f;
        public const float IceTrapFreezeSeconds = 1.25f;

        public const string ArrowSupplyFuelItemId = "coal";
        public const float ArrowSupplyFuelSecondsPerUnit = 30f;
        public const float ArrowSupplyIntervalSeconds = 6f;
        public const float ArrowSupplyRadiusTiles = 8f;
        public const int ArrowSupplyAmmoPerPulse = 1;

        public const float PlasterDollRadiusTiles = 5f;
        public const float PlasterDollHealIntervalSeconds = 2f;
        public const float PlasterDollHealAmount = 4f;

        public const float GongTowerRadiusTiles = 10f;
        public const float GongTowerAggroSeconds = 8f;
        public const float GongTowerPulseIntervalSeconds = 4f;

        private static readonly string[] UtilityIds =
        {
            ScarecrowId,
            IceTrapId,
            ArrowSupplyId,
            PlasterDollId,
            GongTowerId
        };

        public static bool IsUtilityTurretId(string definitionId)
        {
            if (string.IsNullOrWhiteSpace(definitionId)) return false;
            for (var index = 0; index < UtilityIds.Length; index++)
            {
                if (string.Equals(definitionId, UtilityIds[index], StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        public static bool HasCombatFieldEffect(string definitionId) =>
            string.Equals(definitionId, ScarecrowId, StringComparison.Ordinal) ||
            string.Equals(definitionId, IceTrapId, StringComparison.Ordinal) ||
            string.Equals(definitionId, ArrowSupplyId, StringComparison.Ordinal) ||
            string.Equals(definitionId, PlasterDollId, StringComparison.Ordinal) ||
            string.Equals(definitionId, GongTowerId, StringComparison.Ordinal);

        public static bool UsesFuelSlot(string definitionId) =>
            string.Equals(definitionId, ArrowSupplyId, StringComparison.Ordinal);

        public static bool IsInsideRadius(Vector2 origin, Vector2 point, float radiusTiles)
        {
            if (radiusTiles <= 0f || float.IsNaN(radiusTiles) || float.IsInfinity(radiusTiles))
                return false;
            return (point - origin).sqrMagnitude <= radiusTiles * radiusTiles;
        }
    }
}
