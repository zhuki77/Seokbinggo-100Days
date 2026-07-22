using System;
using System.Collections.Generic;
using System.Linq;
using Nyangbingo.Data;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class NyangbingoArt720Binder
{
    private const string ItemCatalogPath = "Assets/Art/Items/ItemArtCatalog.asset";
    private const string GameplayCatalogPath = "Assets/Art/Gameplay/GameplayArtCatalog.asset";
    private const string BuildingCatalogPath = "Assets/Art/Buildings/BuildingArtCatalog.asset";
    private const string EnvironmentCatalogPath = "Assets/Art/Backgrounds/EnvironmentArtCatalog.asset";
    private const string DeepBackgroundTilePath = "Assets/Tiles/Temp/bg_deep.asset";
    private const string DeepBackgroundArtPath = "Assets/Art/Tiles/t_bg_deep.aseprite";
    private const string RuinWallTilePath = "Assets/Tiles/Temp/ruin_wall.asset";
    private const string RuinWallArtPath = "Assets/Art/Tiles/ruin_wall.aseprite";

    // Item UI and world drops both use ItemArtCatalog. Delivered tile,
    // decoration, and building art therefore also needs an item preview entry.
    private static readonly (string id, string path)[] ItemBindings =
    {
        ("wood", "Assets/Art/Decorations/tree.aseprite"),
        ("hemp_stalk", "Assets/Art/Decorations/hemp.aseprite"),
        ("rebar", "Assets/Art/Decorations/ruin_rebar.aseprite"),
        ("dirt", "Assets/Art/Tiles/dirt.aseprite"),
        ("stone", "Assets/Art/Tiles/stone.aseprite"),
        ("coal", "Assets/Art/Tiles/coal.aseprite"),
        ("iron_ore", "Assets/Art/Tiles/iron_ore.aseprite"),
        ("copper_ore", "Assets/Art/Tiles/copper_ore.aseprite"),
        ("ice_shard", "Assets/Art/Tiles/ice_shard.aseprite"),
        ("icesteel_ore", "Assets/Art/Tiles/icesteel_ore.aseprite"),
        ("frost_essence", "Assets/Art/Tiles/frost_essence.aseprite"),
        ("clay", "Assets/Art/Items/clay.aseprite"),
        ("stone_mid", "Assets/Art/Tiles/stone_mid.aseprite"),
        ("bare_claw", "Assets/Art/Items/bare_claw.aseprite"),
        ("iron_claw", "Assets/Art/Items/iron_claw.aseprite"),
        ("icesteel_claw", "Assets/Art/Items/icesteel_claw.aseprite"),
        ("dokkaebi_club", "Assets/Art/Items/dokkaebi_club.aseprite"),
        ("cheolseon", "Assets/Art/Items/cheolseon.aseprite"),
        ("drought_heart", "Assets/Art/Items/drought_heart.aseprite"),
        ("frostclaw_gauntlet", "Assets/Art/Items/frostclaw_gauntlet.aseprite"),
        ("iron_forge_core", "Assets/Art/Items/iron_forge_core.aseprite"),
        ("hapjukseon", "Assets/Art/Items/hapjukseon.aseprite"),
        ("copper_ingot", "Assets/Art/Items/copper_ingot.aseprite"),
        ("icesteel_ingot", "Assets/Art/Items/icesteel_ingot.aseprite"),
        ("iron_ingot", "Assets/Art/Items/iron_ingot.aseprite"),
        ("water_jar", "Assets/Art/Items/water_jar.aseprite"),
        ("yokai_tear", "Assets/Art/UI/yokai_tear_balance.aseprite"),
        ("workbench", "Assets/Art/Buildings/workbench.aseprite"),
        ("furnace", "Assets/Art/Buildings/furnace.aseprite"),
        ("blast_furnace", "Assets/Art/Buildings/blast_furnace.aseprite"),
        ("ice_anvil", "Assets/Art/Buildings/ice_anvil.aseprite"),
        ("lantern", "Assets/Art/Buildings/lantern.aseprite"),
        ("sieve", "Assets/Art/Buildings/sieve.aseprite"),
        ("haetae_statue", "Assets/Art/Buildings/haetae_statue.aseprite"),
        ("nest_bed", "Assets/Art/Buildings/nest_bed.aseprite"),
        ("magpie_nest", "Assets/Art/Buildings/magpie_nest.aseprite"),
        ("bell_rope", "Assets/Art/Buildings/bell_rope.aseprite"),
        ("iron_bell_rope", "Assets/Art/Buildings/iron_bell_rope.aseprite"),
        ("iron_sieve", "Assets/Art/Buildings/iron_sieve.aseprite"),
        ("frost_lantern", "Assets/Art/Buildings/frost_lantern.aseprite"),
        ("insul_wall", "Assets/Art/Buildings/insul_wall.aseprite"),
        ("door", "Assets/Art/Buildings/door.aseprite"),
        ("roof", "Assets/Art/Buildings/roof.aseprite"),
        ("jangdok", "Assets/Art/Buildings/jangdok.aseprite"),
        ("ice_core", "Assets/Art/Buildings/ice_core.aseprite"),
        ("iron_insul_wall", "Assets/Art/Buildings/iron_insul_wall.aseprite"),
        ("cold_device", "Assets/Art/Buildings/cold_device.aseprite"),
        ("dokkaebi_fire_tower", "Assets/Art/Buildings/dokkaebi_fire_tower.aseprite"),
        ("singijeon_cart", "Assets/Art/Buildings/singijeon_cart.aseprite"),
        ("ice_crystal_cooler", "Assets/Art/Buildings/ice_crystal_cooler.aseprite"),
        ("cold_wave_core", "Assets/Art/Buildings/cold_wave_core.aseprite"),
        ("ice_jar", "Assets/Art/Buildings/ice_jar.aseprite"),
        ("straw_insul", "Assets/Art/Buildings/straw_insul.aseprite"),
        ("clay_plaster", "Assets/Art/Buildings/clay_plaster.aseprite"),
        ("munpungji", "Assets/Art/Buildings/munpungji.aseprite"),
        ("minhwa_scroll", "Assets/Art/Buildings/minhwa_scroll.aseprite"),
        ("saekdong_lantern", "Assets/Art/Buildings/saekdong_lantern.aseprite"),
        ("onggi_pot", "Assets/Art/Buildings/onggi_pot.aseprite"),
        ("wind_chime", "Assets/Art/Buildings/wind_chime.aseprite"),
        ("saekdong_cushion", "Assets/Art/Buildings/saekdong_cushion.aseprite"),
        ("club_shard", "Assets/Art/Items/club_shard.aseprite"),
        ("daebal", "Assets/Art/Items/daebal.aseprite"),
        ("dokkaebi_fire_essence", "Assets/Art/Items/dokkaebi_fire_essence.aseprite"),
        ("gangcheol_scale", "Assets/Art/Items/gangcheol_scale.aseprite"),
        ("iron_scale", "Assets/Art/Items/iron_scale.aseprite"),
        ("jukbuin", "Assets/Art/Items/jukbuin.aseprite"),
        ("shadow_shard", "Assets/Art/Items/shadow_shard.aseprite"),
        ("stolen_bundle", "Assets/Art/Items/stolen_bundle.aseprite"),
        ("yeouiju", "Assets/Art/Items/yeouiju.aseprite")
    };

    private static readonly (string property, string path, int expectedFrames)[] FrameBindings =
    {
        ("dayCounterFrames", "Assets/Art/UI/day_counter.aseprite", 17),
        ("dayNightClockFrames", "Assets/Art/UI/day_night_clock.aseprite", 6),
        ("yokaiTearBalanceFrames", "Assets/Art/UI/yokai_tear_balance.aseprite", 4),
        ("fuelGaugeFrames", "Assets/Art/UI/fuel_gauge.aseprite", 4),
        ("saveIndicatorFrames", "Assets/Art/UI/save_indicator.aseprite", 6),
        ("playerVitalsFrames", "Assets/Art/UI/player_vitals_combined.aseprite", 12),
        ("button1x1Frames", "Assets/Art/UI/Common/button_1x1.aseprite", 2),
        ("button1x2Frames", "Assets/Art/UI/Common/button_1x2.aseprite", 2),
        ("button1x4Frames", "Assets/Art/UI/Common/button_1x4.aseprite", 2),
        ("button1x6Frames", "Assets/Art/UI/Common/button_1x6.aseprite", 2),
        ("miningBreakFrames", "Assets/Art/Gameplay/mining_break.aseprite", 2),
        ("miningCriticalFrames", "Assets/Art/Gameplay/mining_critical.aseprite", 1)
    };

    // Catalog order consumed by RuntimePixelGlyphPresenter:
    // D, dash, colon, then digits zero through nine.
    private static readonly string[] ShellNumberGlyphPaths =
    {
        "Assets/Art/UI/Shell/glyph_d.aseprite",
        "Assets/Art/UI/Shell/glyph_dash.aseprite",
        "Assets/Art/UI/Shell/glyph_colon.aseprite",
        "Assets/Art/UI/Shell/digit_0.aseprite",
        "Assets/Art/UI/Shell/digit_1.aseprite",
        "Assets/Art/UI/Shell/digit_2.aseprite",
        "Assets/Art/UI/Shell/digit_3.aseprite",
        "Assets/Art/UI/Shell/digit_4.aseprite",
        "Assets/Art/UI/Shell/digit_5.aseprite",
        "Assets/Art/UI/Shell/digit_6.aseprite",
        "Assets/Art/UI/Shell/digit_7.aseprite",
        "Assets/Art/UI/Shell/digit_8.aseprite",
        "Assets/Art/UI/Shell/digit_9.aseprite"
    };

    private static readonly (string property, string path)[] SpriteBindings =
    {
        ("dangerIcon", "Assets/Art/UI/danger_icon.aseprite"),
        ("bossWarningLarge", "Assets/Art/UI/Boss/boss_warning_32.aseprite"),
        ("bossWarningSmall", "Assets/Art/UI/Boss/boss_warning_16.aseprite"),
        ("bossHealthFrame", "Assets/Art/UI/Boss/boss_health_frame.aseprite"),
        ("bossHealthGangcheol", "Assets/Art/UI/Boss/boss_health_gangcheol.aseprite"),
        ("bossHealthKingDokkaebi", "Assets/Art/UI/Boss/boss_health_king_dokkaebi.aseprite"),
        ("bossHealthMotherBulgasari", "Assets/Art/UI/Boss/boss_health_mother_bulgasari.aseprite"),
        ("bossHealthImugi", "Assets/Art/UI/Boss/boss_health_imugi.aseprite"),
        ("inventoryPanel", "Assets/Art/UI/Inventory/inventory_panel.aseprite"),
        ("inventorySlot", "Assets/Art/UI/Inventory/inventory_slot.aseprite"),
        ("inventorySlotSelected", "Assets/Art/UI/Inventory/inventory_slot_selected.aseprite"),
        ("inventorySlotTopSelected", "Assets/Art/UI/Inventory/inventory_slot_top_selected.aseprite"),
        ("equipmentCharacter", "Assets/Art/UI/Inventory/equipment_character.aseprite"),
        ("equipmentHeadSlot", "Assets/Art/UI/Inventory/equipment_head_slot.aseprite"),
        ("equipmentHeadSlotSelected", "Assets/Art/UI/Inventory/equipment_head_slot_selected.aseprite"),
        ("equipmentBodySlot", "Assets/Art/UI/Inventory/equipment_body_slot.aseprite"),
        ("equipmentBodySlotSelected", "Assets/Art/UI/Inventory/equipment_body_slot_selected.aseprite"),
        ("equipmentFeetSlot", "Assets/Art/UI/Inventory/equipment_feet_slot.aseprite"),
        ("equipmentFeetSlotSelected", "Assets/Art/UI/Inventory/equipment_feet_slot_selected.aseprite"),
        ("equipmentAccessorySlot", "Assets/Art/UI/Inventory/equipment_accessory_slot.aseprite"),
        ("equipmentAccessorySlotSelected", "Assets/Art/UI/Inventory/equipment_accessory_slot_selected.aseprite"),
        ("activeItemSlot", "Assets/Art/UI/Inventory/active_item_slot.aseprite"),
        ("activeItemSlotSelected", "Assets/Art/UI/Inventory/active_item_slot_selected.aseprite"),
        ("tilePaletteSlotSelected", "Assets/Art/UI/Inventory/tile_palette_slot_selected.aseprite"),
        ("jangdokStorageGrid", "Assets/Art/UI/Inventory/jangdok_storage_grid.aseprite"),
        ("codexCard", "Assets/Art/UI/Common/codex_card.aseprite"),
        ("shellTitleLogo", "Assets/Art/UI/Shell/title_logo.aseprite"),
        ("shellStart", "Assets/Art/UI/Shell/start.aseprite"),
        ("shellContinue", "Assets/Art/UI/Shell/continue.aseprite"),
        ("shellResume", "Assets/Art/UI/Shell/resume.aseprite"),
        ("shellSave", "Assets/Art/UI/Shell/save.aseprite"),
        ("shellSettings", "Assets/Art/UI/Shell/setting.aseprite"),
        ("shellLeave", "Assets/Art/UI/Shell/leave.aseprite"),
        ("shellReturnTitle", "Assets/Art/UI/Shell/return_title.aseprite"),
        ("shellApply", "Assets/Art/UI/Shell/apply.aseprite"),
        ("shellBack", "Assets/Art/UI/Shell/back.aseprite"),
        ("shellBgmLabel", "Assets/Art/UI/Shell/bgm_label.aseprite"),
        ("shellSfxLabel", "Assets/Art/UI/Shell/sfx_label.aseprite"),
        ("shellPauseTitle", "Assets/Art/UI/Shell/pause_title.aseprite"),
        ("shellPauseIcon", "Assets/Art/UI/Shell/pause_icon.aseprite"),
        ("shellPlayIcon", "Assets/Art/UI/Shell/play_icon.aseprite"),
        ("shellCheckOn", "Assets/Art/UI/Shell/check_on.aseprite"),
        ("shellCheckOff", "Assets/Art/UI/Shell/check_off.aseprite"),
        ("shellSpeakerHigh", "Assets/Art/UI/Shell/sp1.aseprite"),
        ("shellSpeakerLow", "Assets/Art/UI/Shell/sp2.aseprite"),
        ("shellSpeakerMuted", "Assets/Art/UI/Shell/sp3.aseprite"),
        ("shellVolumeBar", "Assets/Art/UI/Shell/spbar.aseprite"),
        ("shellVolumeHandle", "Assets/Art/UI/Shell/spbar2.aseprite")
    };

    private static readonly (string property, string path)[] EnvironmentBindings =
    {
        ("daySky", "Assets/Art/Backgrounds/day_sky.aseprite"),
        ("dayRearClouds", "Assets/Art/Backgrounds/day_rear_clouds.aseprite"),
        ("dayMountains", "Assets/Art/Backgrounds/day_mountains.aseprite"),
        ("dayFrontClouds", "Assets/Art/Backgrounds/day_front_clouds.aseprite"),
        ("sun", "Assets/Art/Backgrounds/sun.aseprite"),
        ("nightSky", "Assets/Art/Backgrounds/night_sky.aseprite"),
        ("nightRearClouds", "Assets/Art/Backgrounds/night_rear_clouds.aseprite"),
        ("nightMountains", "Assets/Art/Backgrounds/night_mountains.aseprite"),
        ("nightFrontClouds", "Assets/Art/Backgrounds/night_front_clouds.aseprite"),
        ("moon", "Assets/Art/Backgrounds/moon.aseprite")
    };

    static NyangbingoArt720Binder()
    {
        EditorApplication.delayCall += BindOnFirstImport;
    }

    [MenuItem("Nyangbingo/Bind Delivered Art Catalogs")]
    public static void BindDeliveredArt()
    {
        AssetDatabase.Refresh();
        Nyangbingo.Editor.NyangbingoTileArtIntegrator.ApplyTileArt();
        Nyangbingo.Editor.NyangbingoTileArtIntegrator.ApplyItemArt();
        Nyangbingo.Editor.NyangbingoTileArtIntegrator.ApplyBuildingArt();
        var importFailures = new List<string>();
        var deliveredPaths = FrameBindings.Select(binding => binding.path)
            .Concat(SpriteBindings.Select(binding => binding.path))
            .Concat(ShellNumberGlyphPaths)
            .Concat(EnvironmentBindings.Select(binding => binding.path))
            .Distinct(StringComparer.Ordinal);
        foreach (var path in deliveredPaths)
            Nyangbingo.Editor.NyangbingoTileArtIntegrator.ConfigureAsepriteImporter(
                path, importFailures);
        if (importFailures.Count > 0)
        {
            Debug.LogError("[Nyangbingo] Delivered UI/effect art importer configuration failed.\n- " +
                           string.Join("\n- ", importFailures));
            return;
        }
        if (!TryBindItemCatalog() || !TryBindGameplayCatalog() || !TryBindEnvironmentCatalog())
        {
            Debug.LogError("[Nyangbingo] 제공 아트 카탈로그 연결에 실패했습니다. 임포트 오류와 파일 경로를 확인하세요.");
            return;
        }

        AssetDatabase.SaveAssets();
        var frameCount = FrameBindings.Sum(binding => binding.expectedFrames);
        Debug.Log($"[Nyangbingo] 제공 아트 카탈로그 연결 완료 " +
                  $"(아이템/드롭 {ItemBindings.Length}, HUD/버튼 프레임 {frameCount}, " +
                  $"HUD/보스/인벤토리 단일 스프라이트 {SpriteBindings.Length}).");
    }

    [MenuItem("Nyangbingo/Art/Validate Shell UI Art")]
    public static void ValidateShellUiArt()
    {
        var gameplayCatalog = AssetDatabase.LoadAssetAtPath<GameplayArtCatalog>(GameplayCatalogPath);
        var environmentCatalog = AssetDatabase.LoadAssetAtPath<EnvironmentArtCatalog>(
            "Assets/Art/Backgrounds/EnvironmentArtCatalog.asset");
        var failures = new List<string>();
        if (gameplayCatalog == null)
        {
            failures.Add($"Gameplay art catalog missing: {GameplayCatalogPath}");
        }
        else
        {
            var serialized = new SerializedObject(gameplayCatalog);
            foreach (var binding in SpriteBindings.Where(binding =>
                         binding.path.StartsWith("Assets/Art/UI/Shell/", StringComparison.Ordinal)))
            {
                var sprite = serialized.FindProperty(binding.property)?.objectReferenceValue as Sprite;
                if (sprite == null)
                    failures.Add($"{binding.property}: Sprite reference missing");
                else if (!string.Equals(AssetDatabase.GetAssetPath(sprite), binding.path,
                             StringComparison.Ordinal))
                    failures.Add(
                        $"{binding.property}: expected '{binding.path}', actual '{AssetDatabase.GetAssetPath(sprite)}'");
            }
            foreach (var binding in FrameBindings.Where(binding =>
                         binding.path.StartsWith("Assets/Art/UI/Common/button_", StringComparison.Ordinal)))
            {
                var property = serialized.FindProperty(binding.property);
                if (property == null || property.arraySize != binding.expectedFrames)
                    failures.Add($"{binding.property}: expected {binding.expectedFrames} frames, " +
                                 $"actual {property?.arraySize ?? 0}");
            }
            var numberGlyphs = serialized.FindProperty("shellNumberGlyphs");
            if (numberGlyphs == null || numberGlyphs.arraySize != ShellNumberGlyphPaths.Length)
                failures.Add($"shellNumberGlyphs: expected {ShellNumberGlyphPaths.Length} glyphs, " +
                             $"actual {numberGlyphs?.arraySize ?? 0}");
            else
                for (var index = 0; index < ShellNumberGlyphPaths.Length; index++)
                {
                    var sprite = numberGlyphs.GetArrayElementAtIndex(index).objectReferenceValue as Sprite;
                    if (sprite == null || !string.Equals(AssetDatabase.GetAssetPath(sprite),
                            ShellNumberGlyphPaths[index], StringComparison.Ordinal))
                        failures.Add($"shellNumberGlyphs[{index}]: expected '{ShellNumberGlyphPaths[index]}'");
                }
            var codexCard = serialized.FindProperty("codexCard")?.objectReferenceValue as Sprite;
            if (codexCard == null || !string.Equals(AssetDatabase.GetAssetPath(codexCard),
                    "Assets/Art/UI/Common/codex_card.aseprite", StringComparison.Ordinal))
                failures.Add("codexCard: delivered Sprite reference missing");
        }
        if (environmentCatalog == null || environmentCatalog.DayCounterScrollFrames.Count != 10)
            failures.Add($"day-counter scroll: expected 10 frames, " +
                         $"actual {environmentCatalog?.DayCounterScrollFrames.Count ?? 0}");
        if (environmentCatalog == null || environmentCatalog.TitleBackground == null)
            failures.Add("title background: keyvisual-day Sprite reference missing");
        if (failures.Count > 0)
        {
            Debug.LogError("[Nyangbingo] Shell UI art validation failed:\n- " +
                           string.Join("\n- ", failures));
            return;
        }
        var shellIconCount = SpriteBindings.Count(binding =>
            binding.path.StartsWith("Assets/Art/UI/Shell/", StringComparison.Ordinal));
        Debug.Log("[Nyangbingo] Shell UI art validation passed: title background 1/1, day-counter scroll 10/10, " +
                  $"shell icons {shellIconCount}/{shellIconCount}, number glyphs " +
                  $"{ShellNumberGlyphPaths.Length}/{ShellNumberGlyphPaths.Length}, buttons 8/8, codex card 1/1.");
    }

    private static void BindOnFirstImport()
    {
        var itemCatalog = AssetDatabase.LoadAssetAtPath<ItemArtCatalog>(ItemCatalogPath);
        var gameplayCatalog = AssetDatabase.LoadAssetAtPath<GameplayArtCatalog>(GameplayCatalogPath);
        var buildingCatalog = AssetDatabase.LoadAssetAtPath<BuildingArtCatalog>(BuildingCatalogPath);
        var environmentCatalog = AssetDatabase.LoadAssetAtPath<EnvironmentArtCatalog>(EnvironmentCatalogPath);
        var deepBackgroundTile = AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.Tile>(DeepBackgroundTilePath);
        var ruinWallTile = AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.Tile>(RuinWallTilePath);
        if (itemCatalog == null || gameplayCatalog == null || buildingCatalog == null ||
            environmentCatalog == null) return;
        var itemSerialized = new SerializedObject(itemCatalog);
        var gameplaySerialized = new SerializedObject(gameplayCatalog);
        var environmentSerialized = new SerializedObject(environmentCatalog);
        var deepBackgroundBound = deepBackgroundTile != null && deepBackgroundTile.sprite != null &&
                                  string.Equals(AssetDatabase.GetAssetPath(deepBackgroundTile.sprite),
                                      DeepBackgroundArtPath, StringComparison.Ordinal);
        var ruinWallBound = ruinWallTile != null && ruinWallTile.sprite != null &&
                            string.Equals(AssetDatabase.GetAssetPath(ruinWallTile.sprite),
                                RuinWallArtPath, StringComparison.Ordinal);
        var otherCatalogsCurrent =
            ContainsItem(itemSerialized.FindProperty("entries"), "gangcheol_scale") &&
            ContainsItem(itemSerialized.FindProperty("entries"), "workbench") &&
            ContainsItem(itemSerialized.FindProperty("entries"), "coal") &&
            ContainsItem(itemSerialized.FindProperty("entries"), "yokai_tear") &&
            gameplaySerialized.FindProperty("dayCounterFrames")?.arraySize == 17 &&
            gameplaySerialized.FindProperty("inventorySlot")?.objectReferenceValue != null &&
            gameplaySerialized.FindProperty("shellStart")?.objectReferenceValue != null &&
            gameplaySerialized.FindProperty("shellNumberGlyphs")?.arraySize == ShellNumberGlyphPaths.Length &&
            SpriteBindings.Where(binding => binding.path.StartsWith("Assets/Art/UI/Shell/", StringComparison.Ordinal))
                .All(binding => gameplaySerialized.FindProperty(binding.property)?.objectReferenceValue != null) &&
            gameplaySerialized.FindProperty("shellSpeakerHigh")?.objectReferenceValue != null &&
            gameplaySerialized.FindProperty("shellSpeakerLow")?.objectReferenceValue != null &&
            gameplaySerialized.FindProperty("shellSpeakerMuted")?.objectReferenceValue != null &&
            gameplaySerialized.FindProperty("shellVolumeBar")?.objectReferenceValue != null &&
            gameplaySerialized.FindProperty("shellVolumeHandle")?.objectReferenceValue != null &&
            gameplaySerialized.FindProperty("button1x1Frames")?.arraySize == 2 &&
            gameplaySerialized.FindProperty("button1x2Frames")?.arraySize == 2 &&
            gameplaySerialized.FindProperty("button1x4Frames")?.arraySize == 2 &&
            gameplaySerialized.FindProperty("button1x6Frames")?.arraySize == 2 &&
            gameplaySerialized.FindProperty("codexCard")?.objectReferenceValue != null &&
            EnvironmentBindings.All(binding =>
                environmentSerialized.FindProperty(binding.property)?.objectReferenceValue != null) &&
            deepBackgroundBound && ruinWallBound;
        if (otherCatalogsCurrent &&
            Nyangbingo.Editor.NyangbingoTileArtIntegrator.IsBuildingArtCurrent(buildingCatalog)) return;
        if (otherCatalogsCurrent)
        {
            Nyangbingo.Editor.NyangbingoTileArtIntegrator.ApplyBuildingArt();
            return;
        }
        BindDeliveredArt();
    }

    private static bool TryBindItemCatalog()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<ItemArtCatalog>(ItemCatalogPath);
        if (catalog == null) return false;
        var serialized = new SerializedObject(catalog);
        var entries = serialized.FindProperty("entries");
        if (entries == null) return false;

        foreach (var binding in ItemBindings)
        {
            var sprite = LoadFirstSprite(binding.path);
            if (sprite == null) return false;
            var entry = FindOrAddItem(entries, binding.id);
            entry.FindPropertyRelative("id").stringValue = binding.id;
            entry.FindPropertyRelative("sprite").objectReferenceValue = sprite;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
        return true;
    }

    private static bool TryBindGameplayCatalog()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<GameplayArtCatalog>(GameplayCatalogPath);
        if (catalog == null) return false;
        var serialized = new SerializedObject(catalog);

        foreach (var binding in FrameBindings)
        {
            var frames = LoadSprites(binding.path);
            if (frames.Length != binding.expectedFrames) return false;
            var property = serialized.FindProperty(binding.property);
            if (property == null) return false;
            property.arraySize = frames.Length;
            for (var index = 0; index < frames.Length; index++)
                property.GetArrayElementAtIndex(index).objectReferenceValue = frames[index];
        }

        foreach (var binding in SpriteBindings)
        {
            var sprite = LoadFirstSprite(binding.path);
            var property = serialized.FindProperty(binding.property);
            if (sprite == null || property == null) return false;
            property.objectReferenceValue = sprite;
        }

        var numberGlyphs = serialized.FindProperty("shellNumberGlyphs");
        if (numberGlyphs == null) return false;
        numberGlyphs.arraySize = ShellNumberGlyphPaths.Length;
        for (var index = 0; index < ShellNumberGlyphPaths.Length; index++)
        {
            var sprite = LoadFirstSprite(ShellNumberGlyphPaths[index]);
            if (sprite == null) return false;
            numberGlyphs.GetArrayElementAtIndex(index).objectReferenceValue = sprite;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
        return true;
    }

    private static bool TryBindEnvironmentCatalog()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<EnvironmentArtCatalog>(EnvironmentCatalogPath);
        if (catalog == null) return false;
        var serialized = new SerializedObject(catalog);
        foreach (var binding in EnvironmentBindings)
        {
            var sprite = LoadFirstSprite(binding.path);
            var property = serialized.FindProperty(binding.property);
            if (sprite == null || property == null) return false;
            property.objectReferenceValue = sprite;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
        return true;
    }

    private static SerializedProperty FindOrAddItem(SerializedProperty entries, string id)
    {
        for (var index = 0; index < entries.arraySize; index++)
        {
            var entry = entries.GetArrayElementAtIndex(index);
            if (entry.FindPropertyRelative("id").stringValue == id) return entry;
        }

        entries.InsertArrayElementAtIndex(entries.arraySize);
        return entries.GetArrayElementAtIndex(entries.arraySize - 1);
    }

    private static bool ContainsItem(SerializedProperty entries, string id)
    {
        if (entries == null) return false;
        for (var index = 0; index < entries.arraySize; index++)
            if (entries.GetArrayElementAtIndex(index).FindPropertyRelative("id").stringValue == id) return true;
        return false;
    }

    private static Sprite LoadFirstSprite(string path) => LoadSprites(path).FirstOrDefault();

    private static Sprite[] LoadSprites(string path)
    {
        if (!System.IO.File.Exists(path)) return Array.Empty<Sprite>();
        return AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<Sprite>()
            .OrderBy(sprite => FrameIndex(sprite.name))
            .ThenBy(sprite => sprite.name, StringComparer.Ordinal)
            .ToArray();
    }

    private static int FrameIndex(string value)
    {
        if (string.IsNullOrEmpty(value)) return int.MaxValue;
        var separator = value.LastIndexOf('_');
        return separator >= 0 && int.TryParse(value.Substring(separator + 1), out var index)
            ? index
            : 0;
    }
}
