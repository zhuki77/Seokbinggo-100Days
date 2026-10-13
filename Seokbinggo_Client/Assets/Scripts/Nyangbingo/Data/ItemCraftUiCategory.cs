using System.Collections.Generic;

namespace Nyangbingo.Data
{
    /// <summary>
    /// 왼쪽 아래 제작 창 분류. 2026-10-13 분류표 순서: 설치, 사용, 장비, 재료.
    /// </summary>
    public static class ItemCraftUiCategory
    {
        public const int Place = 0;
        public const int Use = 1;
        public const int Equip = 2;
        public const int Material = 3;
        public static readonly string[] Labels = { "설치", "사용", "장비", "재료" };

        private static readonly Dictionary<string, int> byId = new Dictionary<string, int>
        {
            { "workbench", 0 },
            { "furnace", 0 },
            { "blast_furnace", 0 },
            { "ice_anvil", 0 },
            { "smithy", 0 },
            { "insul_wall", 0 },
            { "door", 0 },
            { "roof", 0 },
            { "iron_insul_wall", 0 },
            { "wallpaper", 0 },
            { "rope", 0 },
            { "jangdok", 0 },
            { "ice_core", 0 },
            { "cold_device", 0 },
            { "ice_crystal_cooler", 0 },
            { "cold_wave_core", 0 },
            { "water_jar", 0 },
            { "ice_jar", 0 },
            { "straw_insul", 0 },
            { "clay_plaster", 0 },
            { "munpungji", 0 },
            { "lantern", 0 },
            { "sieve", 0 },
            { "haetae_statue", 0 },
            { "nest_bed", 0 },
            { "magpie_nest", 0 },
            { "bell_rope", 0 },
            { "iron_bell_rope", 0 },
            { "iron_sieve", 0 },
            { "frost_lantern", 0 },
            { "jukbuin", 0 },
            { "daebal", 0 },
            { "dokkaebi_fire_tower", 0 },
            { "tal_waypoint", 0 },
            { "minhwa_scroll", 0 },
            { "saekdong_lantern", 0 },
            { "onggi_pot", 0 },
            { "wind_chime", 0 },
            { "saekdong_cushion", 0 },
            { "frost_bell_rope", 0 },
            { "singijeon_cart", 0 },
            { "seonge_tower", 0 },
            { "cold_wave_tower", 0 },
            { "ice_root_battery", 0 },
            { "cold_wave_battery", 0 },
            { "scarecrow", 0 },
            { "ice_trap", 0 },
            { "arrow_supply", 0 },
            { "plaster_doll", 0 },
            { "gong_tower", 0 },
            { "ice_shard", 1 },
            { "catnip", 1 },
            { "oyster_mushroom", 1 },
            { "shiitake", 1 },
            { "seogi", 1 },
            { "tal_return", 1 },
            { "tal_stride", 1 },
            { "tal_hide", 1 },
            { "tal_frost", 1 },
            { "ssireum_satba", 1 },
            { "ice_altar_offering", 1 },
            { "iron_bait_pile", 1 },
            { "jigwi_summon", 1 },
            { "samdugumi_summon", 1 },
            { "eop_summon", 1 },
            { "bare_claw", 2 },
            { "iron_claw", 2 },
            { "icesteel_claw", 2 },
            { "dokkaebi_club", 2 },
            { "frostclaw_gauntlet", 2 },
            { "hapjukseon", 2 },
            { "cheolseon", 2 },
            { "straw_helm", 2 },
            { "straw_armor", 2 },
            { "straw_boots", 2 },
            { "iron_helm", 2 },
            { "iron_armor", 2 },
            { "iron_boots", 2 },
            { "icesteel_helm", 2 },
            { "icesteel_armor", 2 },
            { "icesteel_boots", 2 },
            { "bell_norigae", 2 },
            { "wind_daenggi", 2 },
            { "tiger_eye_bead", 2 },
            { "ice_heart_norigae", 2 },
            { "bokjumeoni", 2 },
            { "dokkaebi_gamtu", 2 },
            { "straw_sling", 2 },
            { "gakgung", 2 },
            { "singijeon_sondae", 2 },
            { "seonge_gakgung", 2 },
            { "ice_root_bow", 2 },
            { "cold_wave_singijeon", 2 },
            { "seonge_fan", 2 },
            { "ice_root_whipfan", 2 },
            { "cold_wave_fan", 2 },
            { "sangun_claw", 2 },
            { "perfect_claw", 2 },
            { "seolpungseon", 2 },
            { "seonge_helm", 2 },
            { "seonge_armor", 2 },
            { "seonge_boots", 2 },
            { "ice_root_helm", 2 },
            { "ice_root_armor", 2 },
            { "ice_root_boots", 2 },
            { "cold_wave_helm", 2 },
            { "cold_wave_armor", 2 },
            { "cold_wave_boots", 2 },
            { "ssireum_knot", 2 },
            { "iron_appetite", 2 },
            { "yeouiju_shard", 2 },
            { "jigwi_ember", 2 },
            { "drought_heart_shard", 2 },
            { "tiger_gait", 2 },
            { "three_horn", 2 },
            { "eop_scale", 2 },
            { "yeongno_mask", 2 },
            { "perfect_core", 2 },
            { "clay_hand", 2 },
            { "skate_pad", 2 },
            { "magpie_bell", 2 },
            { "old_key", 2 },
            { "minhwa_ink", 2 },
            { "vault_seal", 2 },
            { "gate_mark", 2 },
            { "dry_trace", 2 },
            { "altar_echo", 2 },
            { "frost_map", 2 },
            { "first_frost_claw", 2 },
            { "baekjung_bundle", 2 },
            { "yeouiju_claw", 2 },
            { "jigwi_ash", 2 },
            { "sangun_whisker", 2 },
            { "yeongno_tooth", 2 },
            { "wood", 3 },
            { "hemp_stalk", 3 },
            { "rebar", 3 },
            { "dirt", 3 },
            { "stone", 3 },
            { "coal", 3 },
            { "clay", 3 },
            { "iron_ore", 3 },
            { "copper_ore", 3 },
            { "icesteel_ore", 3 },
            { "frost_essence", 3 },
            { "iron_ingot", 3 },
            { "copper_ingot", 3 },
            { "icesteel_ingot", 3 },
            { "yokai_tear", 3 },
            { "club_shard", 3 },
            { "iron_scale", 3 },
            { "stolen_bundle", 3 },
            { "shadow_shard", 3 },
            { "gangcheol_scale", 3 },
            { "dokkaebi_fire_essence", 3 },
            { "yeouiju", 3 },
            { "drought_heart", 3 },
            { "seonge_ore", 3 },
            { "ice_root", 3 },
            { "cold_wave_ore", 3 },
            { "seonge_ingot", 3 },
            { "ice_root_bundle", 3 },
            { "cold_wave_ingot", 3 },
            { "iron_forge_core", 3 },
            { "jigwi_ember_mat", 3 },
            { "blaze_yeokrin", 3 },
            { "sangun_talon", 3 },
            { "three_horn_mat", 3 },
            { "eop_scale_mat", 3 },
            { "yeongno_mask_mat", 3 },
            { "perfect_heart", 3 }
        };

        public static int IndexOf(ItemDefinition item)
        {
            if (item != null && !string.IsNullOrEmpty(item.Id) &&
                byId.TryGetValue(item.Id, out var listed))
                return listed;
            switch (item != null ? item.Category : ItemCategory.Unspecified)
            {
                case ItemCategory.Placeable:
                case ItemCategory.Station:
                    return Place;
                case ItemCategory.Summon:
                    return Use;
                case ItemCategory.Tool:
                case ItemCategory.Weapon:
                case ItemCategory.Equipment:
                    return Equip;
                default:
                    return Material;
            }
        }
    }
}