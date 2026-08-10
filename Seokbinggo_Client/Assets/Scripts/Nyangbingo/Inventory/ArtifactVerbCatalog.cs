using System;
using System.Collections.Generic;
using Nyangbingo.Core;
using Nyangbingo.Data;

namespace Nyangbingo.Inventory
{
    /// <summary>
    /// 아티팩트 id → VerbId. equipment.csv verb_id와 동일 키를 유지한다.
    /// </summary>
    public static class ArtifactVerbCatalog
    {
        private static readonly Dictionary<string, ArtifactVerbId> ByEquipmentId =
            new Dictionary<string, ArtifactVerbId>(StringComparer.Ordinal)
            {
                ["ssireum_knot"] = ArtifactVerbId.GrabKnockedTarget,
                ["iron_appetite"] = ArtifactVerbId.HearIronVein,
                ["yeouiju_shard"] = ArtifactVerbId.ExtendCoolerRadius,
                ["jigwi_ember"] = ArtifactVerbId.NoHeatInShade,
                ["drought_heart_shard"] = ArtifactVerbId.ReduceFlameAndHasten,
                ["tiger_gait"] = ArtifactVerbId.NoFirstStrike,
                ["three_horn"] = ArtifactVerbId.WalkWhileCharging,
                ["eop_scale"] = ArtifactVerbId.MaintainAfterShutdown,
                ["yeongno_mask"] = ArtifactVerbId.EscapeOnSwallow,
                ["perfect_core"] = ArtifactVerbId.RelocateColdCore,
                ["clay_hand"] = ArtifactVerbId.HalveClayCraftTime,
                ["skate_pad"] = ArtifactVerbId.TurnWhileSliding,
                ["magpie_bell"] = ArtifactVerbId.ExtendMagpieRadius,
                ["old_key"] = ArtifactVerbId.OpenStorageAnywhere,
                ["minhwa_ink"] = ArtifactVerbId.BonusTearOnCodex,
                ["vault_seal"] = ArtifactVerbId.FullDemolitionRecovery,
                ["gate_mark"] = ArtifactVerbId.IncreaseVisionDeep,
                ["dry_trace"] = ArtifactVerbId.ReduceFlameTag,
                ["altar_echo"] = ArtifactVerbId.ReduceOfferTears,
                ["frost_map"] = ArtifactVerbId.ShowDugPaths
            };

        public static IReadOnlyDictionary<string, ArtifactVerbId> All => ByEquipmentId;

        public static bool TryGetVerb(string equipmentId, out ArtifactVerbId verb)
        {
            if (string.IsNullOrWhiteSpace(equipmentId))
            {
                verb = ArtifactVerbId.None;
                return false;
            }

            return ByEquipmentId.TryGetValue(equipmentId, out verb);
        }

        /// <summary>CSV verb_id 파싱. 실패 시 None(예외 없음).</summary>
        public static ArtifactVerbId ParseVerb(string verbId) => ArtifactVerbParsing.ParseVerb(verbId);

        public static ArtifactActivationCondition ParseActivation(string condition) =>
            ArtifactVerbParsing.ParseActivation(condition);

        /// <summary>레거시 snake 동사 키 호환(회귀·문서).</summary>
        public static bool TryGetLegacyVerbKey(string equipmentId, out string verbKey)
        {
            verbKey = null;
            if (!TryGetVerb(equipmentId, out var verb) || verb == ArtifactVerbId.None) return false;
            verbKey = verb switch
            {
                ArtifactVerbId.GrabKnockedTarget => "hold_knockback",
                ArtifactVerbId.HearIronVein => "ore_echo",
                ArtifactVerbId.ExtendCoolerRadius => "cooler_boost",
                ArtifactVerbId.NoHeatInShade => "shade_temp_lock",
                ArtifactVerbId.ReduceFlameAndHasten => "day_flame_cut",
                ArtifactVerbId.NoFirstStrike => "surface_aggro_delay",
                ArtifactVerbId.WalkWhileCharging => "bow_walk_reload",
                ArtifactVerbId.MaintainAfterShutdown => "module_holdover",
                ArtifactVerbId.EscapeOnSwallow => "swallow_escape",
                ArtifactVerbId.RelocateColdCore => "core_relocate",
                ArtifactVerbId.HalveClayCraftTime => "clay_craft_half",
                ArtifactVerbId.TurnWhileSliding => "ice_steer",
                ArtifactVerbId.ExtendMagpieRadius => "magpie_radius",
                ArtifactVerbId.OpenStorageAnywhere => "remote_jangdok",
                ArtifactVerbId.BonusTearOnCodex => "codex_tear_bonus",
                ArtifactVerbId.FullDemolitionRecovery => "full_salvage",
                ArtifactVerbId.IncreaseVisionDeep => "deep_vision",
                ArtifactVerbId.ReduceFlameTag => "fire_resist",
                ArtifactVerbId.ReduceOfferTears => "altar_tear_discount",
                ArtifactVerbId.ShowDugPaths => "tunnel_edge_hint",
                _ => null
            };
            return !string.IsNullOrEmpty(verbKey);
        }
    }

    /// <summary>장착 중인 악세/아티팩트에서 동사·조건을 조회한다.</summary>
    public static class ArtifactRules
    {
        public static bool HasVerb(EquipmentSystem equipment, ArtifactVerbId verb)
        {
            if (equipment == null || verb == ArtifactVerbId.None) return false;
            return TryFindEquipped(equipment, verb, out _);
        }

        public static bool TryFindEquipped(
            EquipmentSystem equipment,
            ArtifactVerbId verb,
            out EquipmentDefinition definition)
        {
            definition = null;
            if (equipment == null || verb == ArtifactVerbId.None) return false;
            foreach (var pair in equipment.Export())
            {
                var item = pair.Value;
                if (item == null || !item.IsAccessory) continue;
                var resolved = item.VerbId != ArtifactVerbId.None
                    ? item.VerbId
                    : (ArtifactVerbCatalog.TryGetVerb(item.Id, out var mapped) ? mapped : ArtifactVerbId.None);
                if (resolved != verb) continue;
                definition = item;
                return true;
            }

            return false;
        }

        public static bool IsActivationMet(
            ArtifactActivationCondition condition,
            bool isDeep,
            bool isSurface,
            bool isDay)
        {
            return condition switch
            {
                ArtifactActivationCondition.None => true,
                ArtifactActivationCondition.Deep => isDeep,
                ArtifactActivationCondition.Surface => isSurface,
                ArtifactActivationCondition.DaySurface => isDay && isSurface,
                _ => false
            };
        }
    }
}
