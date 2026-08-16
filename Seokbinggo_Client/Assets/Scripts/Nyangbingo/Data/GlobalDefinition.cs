using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Nyangbingo.Data
{
    public static class GlobalKeys
    {
        public const string DayLengthSeconds = "day_length_sec";
        public const string NightLengthSeconds = "night_length_sec";
        public const string DayTotalSeconds = "day_total_sec";
        public const string MvpDays = "mvp_days";
        public const string TotalDays = "total_days";
        public const string DeadlineRemoved = "deadline_removed";
        public const string WinCondition = "win_condition";
        public const string StartAtNight = "start_at_night";
        public const string BaekjungDay = "baekjung_day";
        public const string SealWindowRadiusX = "seal_window_rx";
        public const string SealWindowRadiusY = "seal_window_ry";
        public const string SealCap = "seal_cap";
        public const string SealTargetCells = "seal_target_cells";
        public const string SealPenaltyStartDay = "seal_penalty_start_day";
        public const string BossSavePolicy = "boss_save_policy";
        public const string BaekjungWaveOverflow = "baekjung_wave_overflow";
        public const string BadgeWallCount = "badge_wall_count";
        public const string BadgeWindowDays = "badge_window_days";
        public const string BossFieldYokai = "boss_field_yokai";
        public const string CaveMaxHeight = "cave_max_height";
        public const string PlayerJumpHeightTiles = "player_jump_height_tiles";
        public const string PlayerMiningReachTiles = "player_mining_reach_tiles";
        public const string PlayerGravity = "player_gravity";
        public const string PlayerMaxFallSpeed = "player_max_fall_speed";
        public const string PlayerJumpCut = "player_jump_cut";
        public const string FurnitureMvpScope = "furniture_mvp_scope";
        public const string InventorySlots = "inventory_slots";
        public const string ActiveSlotRule = "active_slot_rule";
        public const string PortableLanternRadius = "portable_lantern_radius";
        public const string JangdokStorageSlots = "jangdok_storage_slots";
        public const string WaveMode = "wave_mode";
        public const string YokaiCap = "yokai_cap";
        public const string TurretSlotCap = "turret_slot_cap";
        public const string TurretDamageSlotCap = "turret_damage_slot_cap";
        public const string TurretSlotCapSource = "turret_slot_cap_source";
        public const string TurretTierEarly = "turret_tier_early";
        public const string TurretTierLate = "turret_tier_late";
        public const string TurretMidgameGap = "turret_midgame_gap";
        public const string TurretAutomationSeed = "turret_automation_seed";
        public const string EvolutionBenchT456 = "evolution_bench_t456";
        public const string HeatStagePeriod = "heat_stage_period";
        public const string HeatStageCount = "heat_stage_count";
        public const string HeatStageTrigger = "heat_stage_trigger";
        public const string HeatStageGateOneToTwo = "heat_stage_gate_1to2";
        public const string HeatStageGateTwoToThree = "heat_stage_gate_2to3";
        public const string DayFireDamageByStage = "day_fire_dmg_by_stage";
        public const string TreeDecayByStage = "tree_decay_by_stage";
        public const string DayBrightnessByStage = "day_brightness_by_stage";
        public const string InvasionPeriodDays = "invasion_period_days";
        public const string InvasionOffsetDays = "invasion_offset_days";
        public const string SunScaleTiesHeatStage = "sun_scale_ties_heat_stage";
        public const string InvasionAnnounce = "invasion_announce";
        public const string BedLockedOnInvasion = "bed_locked_on_invasion";
        public const string BedEnabled = "bed_enabled";
        public const string BedItemId = "bed_item_id";
        public const string BedSleepRoomTempMax = "bed_sleep_room_temp_max";
        public const string BedSkipMode = "bed_skip_mode";
        public const string BedSkipAppliesDailyTick = "bed_skip_applies_daily_tick";
        public const string StorageTemperatureSystem = "storage_temp_system";
        public const string StorageBandAmbient = "storage_band_ambient";
        public const string StorageBandChilled = "storage_band_chilled";
        public const string StorageBandFrozen = "storage_band_frozen";
        public const string StorageSpoilPerDay = "storage_spoil_per_day";
        public const string StorageMeltPerDay = "storage_melt_per_day";
        public const string StorageJangdokBand = "storage_jangdok_band";
        public const string StorageIceCoreBand = "storage_ice_core_band";
        public const string StorageGearBand = "storage_gear_band";
        public const string TalismanTable = "talisman_table";
        public const string TalismanCount = "talisman_count";
        public const string MushroomHeal = "mushroom_heal";
        public const string MushroomTable = "mushroom_table";
        public const string WorldHalfWidth = "world_half_width";
        public const string SpawnX = "spawn_x";
        public const string MapWidthDefault = "map_width_default";
        public const string ScalePolicy = "scale_policy";
        public const string UndergroundDepthMinTiles = "underground_depth_min_tiles";
        public const string NightBudgetModel = "night_budget_model";
        public const string BandRoundtripFormula = "band_roundtrip_formula";
        public const string BandRoundtripSecondsDefault = "band_roundtrip_sec_per_order_default";
        public const string TerrainSpawnTable = "terrain_spawn_table";
        public const string TerrainCount = "terrain_count";
        public const string TerrainImplemented = "terrain_implemented";
        public const string BaseVicinityRadius = "base_vicinity_radius";
        public const string YokaiAggroDefault = "yokai_aggro_default";
        public const string YokaiAggroMax = "yokai_aggro_max";
        public const string YokaiBodySizeRule = "yokai_body_size_rule";
        public const string WallBreachStopOnPath = "wall_breach_stop_on_path";
        public const string InvasionTemperatureRisePerTear = "invasion_temp_rise_per_yokai";
        public const string InvasionTemperatureRiseSource = "invasion_temp_rise_source";
        public const string RecoolCost = "recool_cost";
        public const string RecoolWhen = "recool_when";
        public const string RecoolStation = "recool_station";
        public const string OreVeinMode = "ore_vein_mode";
        public const string OreVeinClusterMin = "ore_vein_cluster_min";
        public const string OreVeinClusterMax = "ore_vein_cluster_max";
        public const string TileHardnessRule = "tile_hardness_rule";
        public const string TileHardnessTable = "tile_hardness_table";
        public const string RoomTempColdEnter = "room_temp_cold_enter";
        public const string RoomTempFrozenEnter = "room_temp_frozen_enter";
        public const string TempSystem = "temp_system";
        public const string DepthNormWarmEndStageOne = "depth_norm_warm_end_s1";
        public const string DepthNormColdEndStageOne = "depth_norm_cold_end_s1";
        public const string DepthNormShiftPerStage = "depth_norm_shift_per_stage";
        public const string DepthNormFrozenMinimum = "depth_norm_frozen_min";
        public const string CoreRangeWidth = "core_range_w";
        public const string CoreRangeHeight = "core_range_h";
        public const string CoreDeltaPlain = "core_delta_plain";
        public const string CoreDeltaInsulated = "core_delta_insulated";
        public const string CoreStackMode = "core_stack_mode";
        public const string SealRole = "seal_role";
        public const string HypothermiaRoomTemp = "hypothermia_room_temp";
        public const string HypothermiaFallPerSecond = "hypothermia_fall_per_sec";
        public const string HypothermiaDamageAtTemperature = "hypothermia_dmg_at_temp";
        public const string HypothermiaDamagePerSecond = "hypothermia_dmg_per_sec";
        public const string StationTemperatureStrict = "station_temp_strict";
        public const string FrostSpreadEnabled = "frost_spread_enabled";
        public const string FrostSpreadTrigger = "frost_spread_trigger";
        public const string FrostLazyReveal = "frost_lazy_reveal";
        public const string FrostFirstRevealReward = "frost_first_reveal_reward";
        public const string FrostSpreadVfx = "frost_spread_vfx";
        public const string BedrockPermanent = "bedrock_permanent";
        public const string FrostBedrockUnseal = "frost_bedrock_unseal";
        public const string FrostStepCount = "frost_step_count";
        public const string FrostBandNormFromFormula = "frost_band_norm_from_formula";
        public const string FrostBandNormTo = "frost_band_norm_to";
        public const string InsulStrawBonus = "insul_straw_bonus";
        public const string GimmickWeaponBonus = "gimmick_weapon_bonus";
        public const string LayerT2Depth = "layer_t2_depth";
        public const string LayerT3Depth = "layer_t3_depth";
        public const string BoundaryIceRockHardness = "boundary_ice_rock_hardness";
        public const string BossArenaLayer = "boss_arena_layer";
        public const string AltarCount = "altar_count";
        public const string ResidentMaxPerSpecies = "resident_max_per_species";
        public const string ResidentSpawnAt = "resident_spawn_at";
        public const string ResidentRespawnRule = "resident_respawn_rule";
        public const string ResidentMinPlayerDistance = "resident_min_player_distance";
        public const string ResidentMinBetweenDistance = "resident_min_between_distance";
        public const string ResidentSavePolicy = "resident_save_policy";
    }

    [CreateAssetMenu(menuName = "Nyangbingo/Data/Global Value")]
    public sealed class GlobalDefinition : ScriptableObject
    {
        [SerializeField] private string key;
        [SerializeField] private string value;
        [SerializeField] private string unit;
        [TextArea][SerializeField] private string note;

        public string Key => key;
        public string Value => value;
        public string Unit => unit;
        public string Note => note;

        public bool TryGetInt(out int result) =>
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

        public bool TryGetFloat(out float result) =>
            float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) &&
            !float.IsNaN(result) && !float.IsInfinity(result);

        public bool TryGetBool(out bool result)
        {
            if (unit != "bool") { result = false; return false; }
            if (value == "1") { result = true; return true; }
            if (value == "0") { result = false; return true; }
            result = false;
            return false;
        }
    }

    public sealed class GlobalSettings
    {
        private readonly Dictionary<string, GlobalDefinition> definitionsByKey =
            new Dictionary<string, GlobalDefinition>(StringComparer.Ordinal);

        public GlobalSettings(IReadOnlyList<GlobalDefinition> definitions)
        {
            IsValid = definitions != null && definitions.Count > 0;
            if (definitions == null) return;
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.Key) ||
                    string.IsNullOrWhiteSpace(definition.Value) || string.IsNullOrWhiteSpace(definition.Unit) ||
                    !definitionsByKey.TryAdd(definition.Key, definition))
                    IsValid = false;
            }
        }

        public bool IsValid { get; private set; }
        public int Count => definitionsByKey.Count;
        public GlobalDefinition Find(string key) =>
            !string.IsNullOrEmpty(key) && definitionsByKey.TryGetValue(key, out var definition) ? definition : null;
        public bool TryGetInt(string key, out int value)
        {
            var definition = Find(key);
            if (definition != null) return definition.TryGetInt(out value);
            value = default;
            return false;
        }
        public bool TryGetFloat(string key, out float value)
        {
            var definition = Find(key);
            if (definition != null) return definition.TryGetFloat(out value);
            value = default;
            return false;
        }
        public bool TryGetBool(string key, out bool value)
        {
            var definition = Find(key);
            if (definition != null) return definition.TryGetBool(out value);
            value = default;
            return false;
        }
        public string GetString(string key) => Find(key)?.Value;
    }

    /// <summary>v34.1 resident-elite rules shared by Eoduksini and Gangcheori.</summary>
    public sealed class ResidentYokaiRules
    {
        public const string DayDawn = "day_dawn";
        public const string NextDayDawn = "next_day_dawn";
        public const string LastKilledDay = "last_killed_day";

        private ResidentYokaiRules(int maxPerSpecies, int minPlayerDistance, int minBetweenDistance,
            int minDepth, int maxDepth)
        {
            MaxPerSpecies = maxPerSpecies;
            MinPlayerDistance = minPlayerDistance;
            MinBetweenDistance = minBetweenDistance;
            MinDepth = minDepth;
            MaxDepth = maxDepth;
        }

        public int MaxPerSpecies { get; }
        public int MinPlayerDistance { get; }
        public int MinBetweenDistance { get; }
        public int MinDepth { get; }
        public int MaxDepth { get; }

        public static ResidentYokaiRules CreateConfirmedV341Defaults() =>
            new ResidentYokaiRules(1, 24, 12, 91, 135);

        public static bool TryCreate(
            IReadOnlyList<GlobalDefinition> definitions, out ResidentYokaiRules rules)
        {
            rules = null;
            var settings = new GlobalSettings(definitions);
            if (!settings.IsValid ||
                !settings.TryGetInt(GlobalKeys.ResidentMaxPerSpecies, out var maxPerSpecies) ||
                !settings.TryGetInt(GlobalKeys.ResidentMinPlayerDistance, out var minPlayerDistance) ||
                !settings.TryGetInt(GlobalKeys.ResidentMinBetweenDistance, out var minBetweenDistance) ||
                !settings.TryGetInt(GlobalKeys.LayerT2Depth, out var layerT2Depth) ||
                !settings.TryGetInt(GlobalKeys.LayerT3Depth, out var layerT3Depth) ||
                maxPerSpecies != 1 || minPlayerDistance != 24 || minBetweenDistance != 12 ||
                layerT2Depth < 1 || layerT3Depth <= layerT2Depth ||
                settings.GetString(GlobalKeys.ResidentSpawnAt) != DayDawn ||
                settings.GetString(GlobalKeys.ResidentRespawnRule) != NextDayDawn ||
                settings.GetString(GlobalKeys.ResidentSavePolicy) != LastKilledDay)
                return false;

            rules = new ResidentYokaiRules(
                maxPerSpecies, minPlayerDistance, minBetweenDistance,
                layerT2Depth + 1, layerT3Depth);
            return true;
        }
    }
}
