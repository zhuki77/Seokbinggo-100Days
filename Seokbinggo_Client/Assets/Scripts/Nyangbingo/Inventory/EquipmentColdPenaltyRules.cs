using System;
using Nyangbingo.Data;
using UnityEngine;

namespace Nyangbingo.Inventory
{
    /// <summary>v74 확정: 장비 내한 하한 미달 시 방어력만 낮추고 세트 효과는 끈다.</summary>
    public sealed class EquipmentColdPenaltyRules
    {
        private readonly float defenseFloor;
        private readonly float defenseLossPerDegree;

        private EquipmentColdPenaltyRules(float floor, float lossPerDegree)
        {
            defenseFloor = floor;
            defenseLossPerDegree = lossPerDegree;
        }

        public float DefenseFloor => defenseFloor;
        public float DefenseLossPerDegree => defenseLossPerDegree;

        public static bool TryCreate(GameDataCatalog catalog, out EquipmentColdPenaltyRules rules)
        {
            rules = null;
            if (catalog == null ||
                Read(catalog, "gear_mismatch_penalty") != "efficiency" ||
                string.IsNullOrWhiteSpace(Read(catalog, GlobalKeys.GearMismatchDefenseMultiplier)) ||
                Read(catalog, GlobalKeys.GearMismatchStatPenalty) != "none" ||
                Read(catalog, GlobalKeys.GearMismatchSetBonus) != "disabled" ||
                !TryReadFloat(catalog, GlobalKeys.GearMismatchDefenseFloor, out var floor) ||
                !TryReadFloat(catalog, GlobalKeys.GearMismatchDefensePerDegree, out var perDegree) ||
                floor <= 0f || floor > 1f || perDegree <= 0f || perDegree > 1f)
                return false;

            rules = new EquipmentColdPenaltyRules(floor, perDegree);
            return true;
        }

        public float DefenseMultiplier(EquipmentDefinition item, int roomTemperatureC)
        {
            if (item == null || !item.HasColdTolerance || item.ColdOk(roomTemperatureC)) return 1f;
            var degreeDeficit = Math.Max(0, item.ColdToleranceC - roomTemperatureC);
            return Mathf.Max(defenseFloor, 1f - defenseLossPerDegree * degreeDeficit);
        }

        public bool IsSetBonusActive(EquipmentDefinition head, EquipmentDefinition body,
            EquipmentDefinition feet, int roomTemperatureC) =>
            head == null || body == null || feet == null ||
            head.ColdOk(roomTemperatureC) && body.ColdOk(roomTemperatureC) && feet.ColdOk(roomTemperatureC);

        private static bool TryReadFloat(GameDataCatalog catalog, string key, out float value)
        {
            value = 0f;
            var definition = catalog.FindGlobal(key);
            return definition != null && definition.TryGetFloat(out value) &&
                   !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static string Read(GameDataCatalog catalog, string key) => catalog.FindGlobal(key)?.Value;
    }
}
