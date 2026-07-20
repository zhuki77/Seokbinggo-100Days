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
        ("water_jar", "Assets/Art/Buildings/jangdok.aseprite"),
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
        ("playerVitalsFrames", "Assets/Art/UI/player_vitals_combined.aseprite", 12)
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
        ("equipmentBodySlot", "Assets/Art/UI/Inventory/equipment_body_slot.aseprite"),
        ("equipmentBodySlotSelected", "Assets/Art/UI/Inventory/equipment_body_slot_selected.aseprite"),
        ("equipmentFeetSlot", "Assets/Art/UI/Inventory/equipment_feet_slot.aseprite"),
        ("equipmentFeetSlotSelected", "Assets/Art/UI/Inventory/equipment_feet_slot_selected.aseprite"),
        ("equipmentAccessorySlot", "Assets/Art/UI/Inventory/equipment_accessory_slot.aseprite"),
        ("equipmentAccessorySlotSelected", "Assets/Art/UI/Inventory/equipment_accessory_slot_selected.aseprite"),
        ("activeItemSlot", "Assets/Art/UI/Inventory/active_item_slot.aseprite"),
        ("activeItemSlotSelected", "Assets/Art/UI/Inventory/active_item_slot_selected.aseprite")
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
        Nyangbingo.Editor.NyangbingoTileArtIntegrator.ApplyBuildingArt();
        if (!TryBindItemCatalog() || !TryBindGameplayCatalog())
        {
            Debug.LogError("[Nyangbingo] 제공 아트 카탈로그 연결에 실패했습니다. 임포트 오류와 파일 경로를 확인하세요.");
            return;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Nyangbingo] 제공 아트 카탈로그 연결 완료 " +
                  $"(아이템/드롭 {ItemBindings.Length}, HUD 프레임 49, " +
                  $"HUD/보스/인벤토리 단일 스프라이트 {SpriteBindings.Length}).");
    }

    private static void BindOnFirstImport()
    {
        var itemCatalog = AssetDatabase.LoadAssetAtPath<ItemArtCatalog>(ItemCatalogPath);
        var gameplayCatalog = AssetDatabase.LoadAssetAtPath<GameplayArtCatalog>(GameplayCatalogPath);
        var buildingCatalog = AssetDatabase.LoadAssetAtPath<BuildingArtCatalog>(BuildingCatalogPath);
        var deepBackgroundTile = AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.Tile>(DeepBackgroundTilePath);
        var ruinWallTile = AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.Tile>(RuinWallTilePath);
        if (itemCatalog == null || gameplayCatalog == null || buildingCatalog == null) return;
        var itemSerialized = new SerializedObject(itemCatalog);
        var gameplaySerialized = new SerializedObject(gameplayCatalog);
        var deepBackgroundBound = deepBackgroundTile != null && deepBackgroundTile.sprite != null &&
                                  string.Equals(AssetDatabase.GetAssetPath(deepBackgroundTile.sprite),
                                      DeepBackgroundArtPath, StringComparison.Ordinal);
        var ruinWallBound = ruinWallTile != null && ruinWallTile.sprite != null &&
                            string.Equals(AssetDatabase.GetAssetPath(ruinWallTile.sprite),
                                RuinWallArtPath, StringComparison.Ordinal);
        if (ContainsItem(itemSerialized.FindProperty("entries"), "gangcheol_scale") &&
            ContainsItem(itemSerialized.FindProperty("entries"), "workbench") &&
            ContainsItem(itemSerialized.FindProperty("entries"), "coal") &&
            ContainsItem(itemSerialized.FindProperty("entries"), "yokai_tear") &&
            gameplaySerialized.FindProperty("dayCounterFrames")?.arraySize == 17 &&
            gameplaySerialized.FindProperty("inventorySlot")?.objectReferenceValue != null &&
            buildingCatalog.Find("roof")?.Sprite != null && deepBackgroundBound && ruinWallBound) return;
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
