using System;
using System.Collections.Generic;

namespace Nyangbingo.Inventory
{
    /// <summary>
    /// v46 아티팩트 20종 → 런타임 훅용 짧은 동사 키. 전체 게임플레이 구현은 별도.
    /// </summary>
    public static class ArtifactVerbCatalog
    {
        private static readonly Dictionary<string, string> Verbs =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ssireum_knot"] = "hold_knockback",
                ["iron_appetite"] = "ore_echo",
                ["yeouiju_shard"] = "cooler_boost",
                ["jigwi_ember"] = "shade_temp_lock",
                ["drought_heart_shard"] = "day_flame_cut",
                ["tiger_gait"] = "surface_aggro_delay",
                ["three_horn"] = "bow_walk_reload",
                ["eop_scale"] = "module_holdover",
                ["yeongno_mask"] = "swallow_escape",
                ["perfect_core"] = "core_relocate",
                ["clay_hand"] = "clay_craft_half",
                ["skate_pad"] = "ice_steer",
                ["magpie_bell"] = "magpie_radius",
                ["old_key"] = "remote_jangdok",
                ["minhwa_ink"] = "codex_tear_bonus",
                ["vault_seal"] = "full_salvage",
                ["gate_mark"] = "deep_vision",
                ["dry_trace"] = "fire_resist",
                ["altar_echo"] = "altar_tear_discount",
                ["frost_map"] = "tunnel_edge_hint"
            };

        public static bool TryGetVerb(string artifactId, out string verb)
        {
            if (string.IsNullOrWhiteSpace(artifactId))
            {
                verb = null;
                return false;
            }
            return Verbs.TryGetValue(artifactId, out verb);
        }

        public static IReadOnlyDictionary<string, string> All => Verbs;
    }
}
