using System;
using System.Collections.Generic;

namespace Nyangbingo.World
{
    /// <summary>
    /// 석빙고 승급·대장간 해금·터렛 슬롯 순수 규칙 (v46).
    /// modules.csv가 있으면 재료는 카탈로그를 우선하고, 없으면 Builtin을 쓴다(kit zip 전).
    /// </summary>
    public static class SeokbinggoRules
    {
        public const int MaxStage = 6;
        public const int SmithyUnlockStage = 4;
        public const int DefaultDamageSlotCap = 3;
        public const string EarlyTurretId = "dokkaebi_fire_tower";
        public const string SingijeonTurretId = "singijeon_cart";
        public const string SeongeTurretId = "seonge_tower";
        public const string ColdWaveTurretId = "cold_wave_tower";
        public const string ModuleIdPrefix = "seokbinggo_s";
        public const string IceCoreDefinitionId = "ice_core";

        private static readonly string[] BuiltinMaterialsByNextStage =
        {
            null, // index 0 unused
            "stone:15,ice_shard:10,wood:8",
            "stone:25,ice_shard:20,iron_ingot:2,rebar:3",
            "stone:35,ice_shard:30,iron_ingot:4,copper_ingot:2",
            "yokai_tear:160,ice_shard:40,iron_ingot:6",
            "yokai_tear:320,ice_shard:50,icesteel_ingot:4",
            "yokai_tear:560,ice_shard:60,icesteel_ingot:8",
        };

        public static bool IsUpgradeModuleId(string moduleId) =>
            !string.IsNullOrEmpty(moduleId) &&
            moduleId.StartsWith(ModuleIdPrefix, StringComparison.Ordinal);

        public static string ModuleIdForNextStage(int currentStage)
        {
            if (currentStage < 0 || currentStage >= MaxStage) return null;
            return ModuleIdPrefix + (currentStage + 1);
        }

        public static bool IsSmithyUnlocked(int stage) => stage >= SmithyUnlockStage;

        public static int TurretSlotCap(int stage) => Math.Clamp(stage, 0, MaxStage);

        public static bool CanPlaceTurret(int stage, int activeCount, bool isDamageType,
            int damageActiveCount, int damageSlotCap)
        {
            if (activeCount < 0 || damageActiveCount < 0) return false;
            if (activeCount >= TurretSlotCap(stage)) return false;
            if (isDamageType && damageActiveCount >= Math.Max(0, damageSlotCap)) return false;
            return true;
        }

        public static bool IsDamageTurret(string definitionId) =>
            string.Equals(definitionId, EarlyTurretId, StringComparison.Ordinal) ||
            string.Equals(definitionId, SingijeonTurretId, StringComparison.Ordinal) ||
            string.Equals(definitionId, ColdWaveTurretId, StringComparison.Ordinal);

        public static bool IsUtilityTurret(string definitionId) =>
            string.Equals(definitionId, SeongeTurretId, StringComparison.Ordinal);

        public static bool IsKnownTurret(string definitionId) =>
            IsDamageTurret(definitionId) || IsUtilityTurret(definitionId);

        public static bool TryGetBuiltinMaterials(int nextStage, out IReadOnlyList<(string itemId, int amount)> materials)
        {
            materials = Array.Empty<(string, int)>();
            if (nextStage < 1 || nextStage > MaxStage) return false;
            return TryParseMaterials(BuiltinMaterialsByNextStage[nextStage], out materials);
        }

        public static bool TryParseMaterials(string csv, out IReadOnlyList<(string itemId, int amount)> materials)
        {
            materials = Array.Empty<(string, int)>();
            if (string.IsNullOrWhiteSpace(csv)) return false;
            var list = new List<(string, int)>();
            var parts = csv.Split(',');
            for (var i = 0; i < parts.Length; i++)
            {
                var token = parts[i].Trim();
                if (token.Length == 0) continue;
                var colon = token.LastIndexOf(':');
                if (colon <= 0 || colon >= token.Length - 1) return false;
                var id = token.Substring(0, colon).Trim();
                if (string.IsNullOrEmpty(id) ||
                    !int.TryParse(token.Substring(colon + 1).Trim(), out var amount) ||
                    amount <= 0)
                    return false;
                list.Add((id, amount));
            }

            if (list.Count == 0) return false;
            materials = list;
            return true;
        }

        /// <summary>s3(데모 승리선) materials에 눈물이 없어야 한다(v46 assert).</summary>
        public static bool AssertStage3HasNoTears(IReadOnlyList<(string itemId, int amount)> materials)
        {
            if (materials == null) return false;
            for (var i = 0; i < materials.Count; i++)
                if (string.Equals(materials[i].itemId, "yokai_tear", StringComparison.Ordinal))
                    return false;
            return true;
        }
    }
}
