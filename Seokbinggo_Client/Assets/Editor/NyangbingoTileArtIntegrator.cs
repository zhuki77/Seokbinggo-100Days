using System;
using System.Collections.Generic;
using System.Linq;
using Nyangbingo.Data;
using Nyangbingo.World;
using UnityEditor;
using UnityEditor.U2D.Aseprite;
using UnityEditor.U2D.Sprites;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Nyangbingo.Editor
{
    /// <summary>
    /// 아트팀의 Aseprite 원본을 기존 월드 Tile 에셋에 연결한다.
    /// Tile 에셋 자체를 다시 만들지 않으므로 씬과 TilemapRenderer가 가진 GUID 참조는 유지된다.
    /// </summary>
    public static class NyangbingoTileArtIntegrator
    {
        private const float PixelsPerUnit = 16f;
        private const string ArtFolder = "Assets/Art/Tiles";
        private const string TileFolder = "Assets/Tiles/Temp";
        private const string CharacterArtFolder = "Assets/Art/Characters";
        private const string CharacterArtCatalogPath =
            "Assets/Art/Characters/CharacterArtCatalog.asset";
        private const string ItemArtFolder = "Assets/Art/Items";
        private const string ItemArtCatalogPath = "Assets/Art/Items/ItemArtCatalog.asset";
        private const string EnvironmentArtFolder = "Assets/Art/Backgrounds";
        private const string EnvironmentArtCatalogPath =
            "Assets/Art/Backgrounds/EnvironmentArtCatalog.asset";
        private const string GameplayArtFolder = "Assets/Art/Gameplay";
        private const string GameplayArtCatalogPath = "Assets/Art/Gameplay/GameplayArtCatalog.asset";
        private const string ImugiElectricAttackFile = "imugi_electric_attack.aseprite";
        private const string BuildingArtFolder = "Assets/Art/Buildings";
        private const string BuildingArtCatalogPath = "Assets/Art/Buildings/BuildingArtCatalog.asset";
        private const string BuildingPreviewScenePath = "Assets/Scenes/BuildingArtPreview.unity";
        private const string DecorationArtFolder = "Assets/Art/Decorations";
        private const string DecorationArtCatalogPath =
            "Assets/Art/Decorations/WorldDecorationArtCatalog.asset";

        private static readonly string[] IceAltarArtFiles =
        {
            "t_altar_0.aseprite", "t_altar_1.aseprite", "t_altar_2.aseprite", "t_altar_3.aseprite"
        };

        private static readonly IReadOnlyDictionary<string, string> TileArtFiles =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["rope"] = "rope.aseprite",
                ["bedrock"] = "bedrock.aseprite",
                ["bg_dirt"] = "t_bg_dirt.aseprite",
                ["bg_stone"] = "t_bg_stone.aseprite",
                ["bg_deep"] = "t_bg_deep.aseprite",
                ["coal"] = "coal.aseprite",
                ["copper_ore"] = "copper_ore.aseprite",
                ["clay"] = "clay.aseprite",
                ["dirt"] = "dirt.aseprite",
                ["frost_essence"] = "frost_essence.aseprite",
                ["cold_wave_ore"] = "cold_wave_ore.aseprite",
                ["seonge_ore"] = "seonge_ore.aseprite",
                ["oyster_mushroom"] = "oyster_mushroom_block.png",
                ["shiitake"] = "shiitake_block.png",
                ["ice_root"] = "ice_root.aseprite",
                ["seogi"] = "seogi.aseprite",
                ["ice_lake"] = "ice_lake.aseprite",
                ["ice_shard"] = "ice_shard.aseprite",
                ["icesteel_ore"] = "icesteel_ore.aseprite",
                ["iron_ore"] = "iron_ore.aseprite",
                ["ruin_wall"] = "ruin_wall.aseprite",
                ["ice_altar"] = "t_altar.aseprite",
                ["stone"] = "stone.aseprite",
                ["stone_mid"] = "stone_mid.aseprite",
                ["stone_deep"] = "stone_deep.aseprite"
            };

        private static readonly IReadOnlyDictionary<string, string> CharacterArtFiles =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["player"] = "player_frostclaw.aseprite",
                ["club"] = "club.aseprite",
                ["bulgasari"] = "bulgasari.aseprite",
                ["yakwang"] = "yakwang.aseprite",
                ["eoduksini"] = "eoduksini.aseprite",
                ["gangcheol"] = "gangcheol.aseprite",
                ["gangcheol_body"] = "gangcheol_body.aseprite",
                ["gangcheol_pre_tail"] = "gangcheol_mid_body_canvas.png",
                ["gangcheol_post_tail"] = "gangcheol_last_body_canvas.png",
                ["gangcheol_hand"] = "gangcheol_hand_canvas.png",
                ["king_dokkaebi"] = "king_dokkaebi.aseprite",
                ["mother_bulgasari"] = "mother_bulgasari.aseprite",
                ["gangcheol_boss"] = "gangcheol.aseprite",
                ["imugi"] = "imugi_head2.aseprite",
                ["imugi_body"] = "imugi_body.aseprite",
                ["imugi_pre_tail"] = "imugi_mid_body_canvas.png",
                ["imugi_post_tail"] = "imugi_last_body_canvas.png",
                ["gaekgwi"] = "gaekgwi.aseprite",
                ["magpie"] = "magpie.aseprite"
            };

        private static readonly IReadOnlyDictionary<string, string> GaekgwiEffectArtFiles =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["dash"] = "gaekgwi_dash_horizontal.aseprite",
                ["directional"] = "gaekgwi_dash_directional.aseprite",
                ["impact"] = "gaekgwi_cold.aseprite"
            };

        private static readonly IReadOnlyDictionary<string, string> ItemArtFiles =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["vault_seal"] = "DeliveredFinal/vault_seal.aseprite",
                ["rope"] = "Assets/Art/Tiles/rope.aseprite",
                ["yokai_tear"] = "Assets/Art/UI/yokai_tear_balance.aseprite",
                ["dokkaebi_fire_tower"] = "DeliveredFinal/dokkaebi_fire_tower_icon.aseprite",
                ["haetae_statue"] = "DeliveredFinal/haetae_statue_icon.aseprite",
                ["door"] = "DeliveredFinal/door_icon.aseprite",
                ["singijeon_cart"] = "DeliveredFinal/singijeon_cart_icon.aseprite",
                ["yeouiju_shard"] = "DeliveredFinal/yeouiju_shard.aseprite",
                ["tiger_gait"] = "DeliveredFinal/tiger_gait.aseprite",
                ["ssireum_knot"] = "DeliveredFinal/ssireum_knot.aseprite",
                ["skate_pad"] = "DeliveredFinal/skate_pad.aseprite",
                ["perfect_core"] = "DeliveredFinal/perfect_core.aseprite",
                ["old_key"] = "DeliveredFinal/old_key.aseprite",
                ["minhwa_ink"] = "DeliveredFinal/minhwa_ink.aseprite",
                ["magpie_bell"] = "DeliveredFinal/magpie_bell.aseprite",
                ["iron_appetite"] = "DeliveredFinal/iron_appetite.aseprite",
                ["gate_mark"] = "DeliveredFinal/gate_mark.aseprite",
                ["frost_map"] = "DeliveredFinal/frost_map.aseprite",
                ["dry_trace"] = "DeliveredFinal/dry_trace.aseprite",
                ["drought_heart_shard"] = "DeliveredFinal/drought_heart_shard.aseprite",
                ["clay_hand"] = "DeliveredFinal/clay_hand.aseprite",
                ["altar_echo"] = "DeliveredFinal/altar_echo.aseprite",
                ["bare_claw"] = "bare_claw.aseprite",
                ["iron_claw"] = "iron_claw.aseprite",
                ["icesteel_claw"] = "icesteel_claw.aseprite",
                ["dokkaebi_club"] = "dokkaebi_club.aseprite",
                ["cheolseon"] = "cheolseon.aseprite",
                ["clay"] = "clay.aseprite",
                ["drought_heart"] = "drought_heart.aseprite",
                ["frostclaw_gauntlet"] = "frostclaw_gauntlet.aseprite",
                ["iron_forge_core"] = "iron_forge_core.aseprite",
                ["hapjukseon"] = "hapjukseon.aseprite",
                ["copper_ingot"] = "copper_ingot.aseprite",
                ["icesteel_ingot"] = "icesteel_ingot.aseprite",
                ["iron_ingot"] = "iron_ingot.aseprite",
                ["water_jar"] = "water_jar.aseprite",
                ["straw_helm"] = "straw_helm.aseprite",
                ["straw_armor"] = "straw_armor.aseprite",
                ["straw_boots"] = "straw_boots.aseprite",
                ["iron_helm"] = "iron_helm.aseprite",
                ["iron_armor"] = "iron_armor.aseprite",
                ["iron_boots"] = "iron_boots.aseprite",
                ["icesteel_helm"] = "icesteel_helm.aseprite",
                ["icesteel_armor"] = "icesteel_armor.aseprite",
                ["icesteel_boots"] = "icesteel_boots.aseprite",
                ["bell_norigae"] = "bell_norigae.aseprite",
                ["wind_daenggi"] = "wind_daenggi.aseprite",
                ["tiger_eye_bead"] = "tiger_eye_bead.aseprite",
                ["ice_heart_norigae"] = "ice_heart_norigae.aseprite",
                ["bokjumeoni"] = "bokjumeoni.aseprite",
                ["dokkaebi_gamtu"] = "dokkaebi_gamtu.aseprite",
                ["ssireum_satba"] = "ssireum_satba.aseprite",
                ["iron_bait_pile"] = "iron_bait_pile.aseprite",
                ["ice_altar_offering"] = "ice_altar_offering.aseprite",
                ["drought_talisman"] = "drought_talisman.aseprite",
                ["catnip"] = "catnip.aseprite",
                // Confirmed requested icons from the final art delivery; retain the source Aseprite files.
                ["jangdok"] = "DeliveredFinal/jangdok_icon.aseprite",
                ["ice_jar"] = "DeliveredFinal/ice_jar_icon.aseprite",
                ["oyster_mushroom"] = "DeliveredFinal/oyster_mushroom.aseprite",
                ["shiitake"] = "DeliveredFinal/shiitake.aseprite",
                ["smithy"] = "DeliveredFinal/smithy.aseprite",
                ["tal_frost"] = "DeliveredFinal/tal_frost.aseprite",
                ["tal_hide"] = "DeliveredFinal/tal_hide.aseprite",
                ["tal_return"] = "DeliveredFinal/tal_return.aseprite",
                ["tal_stride"] = "DeliveredFinal/tal_stride.aseprite",
                ["tal_waypoint"] = "DeliveredFinal/tal_waypoint_icon.aseprite",
                ["cold_wave_armor"] = "DeliveredFinal/cold_wave_armor.aseprite",
                ["cold_wave_boots"] = "DeliveredFinal/cold_wave_boots.aseprite",
                ["cold_wave_helm"] = "DeliveredFinal/cold_wave_helm.aseprite",
                ["ice_root_armor"] = "DeliveredFinal/ice_root_armor.aseprite",
                ["ice_root_boots"] = "DeliveredFinal/ice_root_boots.aseprite",
                ["ice_root_helm"] = "DeliveredFinal/ice_root_helm.aseprite",
                ["seonge_armor"] = "DeliveredFinal/seonge_armor.aseprite",
                ["seonge_boots"] = "DeliveredFinal/seonge_boots.aseprite",
                ["seonge_helm"] = "DeliveredFinal/seonge_helm.aseprite",
                ["blaze_yeokrin"] = "DeliveredFinal/blaze_yeokrin.aseprite",
                ["cold_wave_ingot"] = "DeliveredFinal/cold_wave_ingot.aseprite",
                ["eop_scale_mat"] = "DeliveredFinal/eop_scale_mat.aseprite",
                ["eop_scale"] = "DeliveredFinal/eop_scale_mat.aseprite",
                ["ice_root_bundle"] = "DeliveredFinal/ice_root_bundle.aseprite",
                ["perfect_heart"] = "DeliveredFinal/perfect_heart.aseprite",
                ["sangun_talon"] = "DeliveredFinal/sangun_talon.aseprite",
                ["seonge_ingot"] = "DeliveredFinal/seonge_ingot.aseprite",
                ["three_horn_mat"] = "DeliveredFinal/three_horn_mat.aseprite",
                ["three_horn"] = "DeliveredFinal/three_horn_mat.aseprite",
                ["arrow_supply"] = "DeliveredFinal/arrow_supply.aseprite",
                ["cold_wave_tower"] = "DeliveredFinal/cold_wave_tower_icon.aseprite",
                ["frost_bell_rope"] = "DeliveredFinal/frost_bell_rope.aseprite",
                ["gong_tower"] = "DeliveredFinal/gong_tower_icon.aseprite",
                ["ice_trap"] = "DeliveredFinal/ice_trap.aseprite",
                ["scarecrow"] = "DeliveredFinal/scarecrow_icon.aseprite",
                ["seonge_tower"] = "DeliveredFinal/seonge_tower_icon.aseprite",
                ["cold_wave_ore"] = "DeliveredFinal/cold_wave_ore.aseprite",
                ["seonge_ore"] = "Assets/Art/Tiles/seonge_ore.aseprite",
                ["ice_root"] = "DeliveredFinal/ice_root.aseprite",
                ["eop_summon"] = "DeliveredFinal/eop_summon.aseprite",
                ["jigwi_summon"] = "DeliveredFinal/jigwi_summon.aseprite",
                ["samdugumi_summon"] = "DeliveredFinal/samdugumi_summon.aseprite",
                ["baekjung_bundle"] = "DeliveredFinal/baekjung_bundle.aseprite",
                ["cold_wave_fan"] = "DeliveredFinal/cold_wave_fan.aseprite",
                ["first_frost_claw"] = "DeliveredFinal/first_frost_claw.aseprite",
                ["gakgung"] = "DeliveredFinal/gakgung.aseprite",
                ["ice_root_bow"] = "DeliveredFinal/ice_root_bow.aseprite",
                ["ice_root_whipfan"] = "DeliveredFinal/ice_root_whipfan.aseprite",
                ["jigwi_ash"] = "DeliveredFinal/jigwi_ash.aseprite",
                ["jigwi_ember_mat"] = "DeliveredFinal/jigwi_ember_mat.aseprite",
                ["jigwi_ember"] = "DeliveredFinal/jigwi_ember_mat.aseprite",
                ["yeongno_mask_mat"] = "DeliveredFinal/yeongno_mask_mat.aseprite",
                ["yeongno_mask"] = "DeliveredFinal/yeongno_mask_mat.aseprite",
                ["singijeon_sondae"] = "DeliveredFinal/singijeon_sondae.aseprite",
                ["cold_wave_singijeon"] = "DeliveredFinal/cold_wave_singijeon.aseprite",
                ["cold_wave_battery"] = "DeliveredFinal/cold_wave_battery_icon.aseprite",
                ["ice_root_battery"] = "DeliveredFinal/ice_root_battery_icon.aseprite",
                ["plaster_doll"] = "DeliveredFinal/plaster_doll_icon.aseprite",
                ["seogi"] = "DeliveredFinal/seogi.aseprite",
                ["perfect_claw"] = "DeliveredFinal/perfect_claw.aseprite",
                ["sangun_claw"] = "DeliveredFinal/sangun_claw.aseprite",
                ["sangun_whisker"] = "DeliveredFinal/sangun_whisker.aseprite",
                ["seolpungseon"] = "DeliveredFinal/seolpungseon.aseprite",
                ["seonge_fan"] = "DeliveredFinal/seonge_fan.aseprite",
                ["seonge_gakgung"] = "DeliveredFinal/seonge_gakgung.aseprite",
                ["straw_sling"] = "DeliveredFinal/straw_sling.aseprite",
                ["yeongno_tooth"] = "DeliveredFinal/yeongno_tooth.aseprite",
                ["yeouiju_claw"] = "DeliveredFinal/yeouiju_claw.aseprite",
                // v29: wallpaper has no dedicated icon art. Use the delivered upper-layer background tile.
                ["wallpaper"] = "Assets/Art/Tiles/t_bg_dirt.aseprite"
            };

        private static readonly IReadOnlyDictionary<string, string> BuildingArtFiles =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["rope"] = "Assets/Art/Tiles/rope.aseprite",
                ["workbench"] = "workbench.aseprite",
                ["furnace"] = "furnace.aseprite",
                ["blast_furnace"] = "blast_furnace.aseprite",
                ["smithy"] = "smithy.aseprite",
                ["arrow_supply"] = "arrow_supply.aseprite",
                ["gong_tower"] = "gong_tower.aseprite",
                ["scarecrow"] = "scarecrow.aseprite",
                ["tal_waypoint"] = "tal_waypoint.aseprite",
                ["water_jar"] = "Assets/Art/Items/water_jar.aseprite",
                ["frost_bell_rope"] = "Assets/Art/Items/DeliveredFinal/frost_bell_rope.aseprite",
                ["seonge_tower"] = "Assets/Art/Items/DeliveredFinal/seonge_tower.aseprite",
                ["cold_wave_tower"] = "Assets/Art/Items/DeliveredFinal/cold_wave_tower.aseprite",
                ["ice_trap"] = "Assets/Art/Items/DeliveredFinal/ice_trap.aseprite",
                ["ice_anvil"] = "ice_anvil.aseprite",
                ["lantern"] = "lantern.aseprite",
                ["frost_lantern"] = "frost_lantern.aseprite",
                ["saekdong_lantern"] = "saekdong_lantern.aseprite",
                ["sieve"] = "sieve.aseprite",
                ["iron_sieve"] = "iron_sieve.aseprite",
                ["haetae_statue"] = "haetae_statue.aseprite",
                ["nest_bed"] = "nest_bed.aseprite",
                ["magpie_nest"] = "magpie_nest.aseprite",
                ["bell_rope"] = "bell_rope.aseprite",
                ["iron_bell_rope"] = "iron_bell_rope.aseprite",
                ["insul_wall"] = "insul_wall.aseprite",
                ["door"] = "door.aseprite",
                ["jangdok"] = "jangdok.aseprite",
                ["ice_core"] = "ice_core.aseprite",
                ["iron_insul_wall"] = "iron_insul_wall.aseprite",
                ["cold_device"] = "cold_device.aseprite",
                ["chest"] = "chest.aseprite",
                ["dokkaebi_fire_tower"] = "dokkaebi_fire_tower.aseprite",
                ["singijeon_cart"] = "singijeon_cart.aseprite",
                ["ice_root_battery"] = "ice_root_battery.aseprite",
                ["cold_wave_battery"] = "cold_wave_battery.aseprite",
                ["plaster_doll"] = "plaster_doll.aseprite",
                ["ice_crystal_cooler"] = "ice_crystal_cooler.aseprite",
                ["cold_wave_core"] = "cold_wave_core.aseprite",
                ["ice_jar"] = "ice_jar.aseprite",
                ["straw_insul"] = "straw_insul.aseprite",
                ["clay_plaster"] = "clay_plaster.aseprite",
                ["munpungji"] = "munpungji.aseprite",
                ["minhwa_scroll"] = "minhwa_scroll.aseprite",
                ["onggi_pot"] = "onggi_pot.aseprite",
                ["wind_chime"] = "wind_chime.aseprite",
                ["saekdong_cushion"] = "saekdong_cushion.aseprite",
                ["roof"] = "roof.aseprite",
                ["jukbuin"] = "Assets/Art/Items/jukbuin.aseprite",
                ["daebal"] = "Assets/Art/Items/daebal.aseprite",
                // Placement preview and placed-object fallback reuse the same delivered background-wall tile.
                ["wallpaper"] = "Assets/Art/Tiles/t_bg_dirt.aseprite"
            };

        private static readonly IReadOnlyDictionary<string, string> DecorationArtFiles =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["grass"] = "grass.aseprite",
                ["grass_dry"] = "grass_dry.aseprite",
                ["hemp"] = "hemp.aseprite",
                ["tree_0"] = "tree.aseprite",
                // The final tree_down/tree_up files are sections, not complete tree variants.
                // Keep seeded variant IDs stable while displaying the complete delivered tree.
                ["tree_1"] = "tree.aseprite",
                ["tree_2"] = "tree.aseprite",
                ["ruin_pillar"] = "ruin_pillar.aseprite",
                ["ruin_rebar"] = "ruin_rebar.aseprite"
            };

        [MenuItem("Nyangbingo/Art/Apply Tile Art")]
        public static void ApplyTileArt()
        {
            var failures = new List<string>();
            var appliedCount = 0;

            foreach (var pair in TileArtFiles)
            {
                var tileId = pair.Key;
                var artPath = $"{ArtFolder}/{pair.Value}";
                var tilePath = $"{TileFolder}/{tileId}.asset";

                // These delivered mushroom files contain four cels across a one-cell canvas.
                // Use the exact canvas export instead of the importer's oversized cel sprite.
                var configured = artPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                    ? ConfigureCharacterPngImporter(artPath, failures)
                    : ConfigureAsepriteImporter(artPath, failures);
                if (!configured)
                {
                    continue;
                }

                var sprites = AssetDatabase.LoadAllAssetsAtPath(artPath)
                    .OfType<Sprite>()
                    .OrderByDescending(sprite => sprite.rect.width * sprite.rect.height)
                    .ThenBy(sprite => sprite.name, StringComparer.Ordinal)
                    .ToArray();

                if (sprites.Length == 0)
                {
                    failures.Add($"{tileId}: Sprite를 불러오지 못했습니다. ({artPath})");
                    continue;
                }

                var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
                if (tile == null)
                {
                    failures.Add($"{tileId}: 기존 Tile 에셋을 찾지 못했습니다. ({tilePath})");
                    continue;
                }

                Undo.RecordObject(tile, "Apply Nyangbingo Tile Art");
                tile.sprite = sprites[0];
                EditorUtility.SetDirty(tile);
                appliedCount++;
            }

            appliedCount += ApplyIceAltarQuadrantArt(failures);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (failures.Count > 0)
            {
                Debug.LogError(
                    $"[Nyangbingo] Tile art integration failed: {appliedCount}/{TileArtFiles.Count + 4} applied.\n- " +
                    string.Join("\n- ", failures));
                return;
            }

            Debug.Log(
                $"[Nyangbingo] Tile art integration completed: {appliedCount}/{TileArtFiles.Count + 4}, " +
                $"PPU={PixelsPerUnit:0}, existing Tile asset GUIDs preserved.");
        }

        private static int ApplyIceAltarQuadrantArt(ICollection<string> failures)
        {
            var quadrantTiles = new TileBase[IceAltarArtFiles.Length];
            var applied = 0;
            for (var index = 0; index < IceAltarArtFiles.Length; index++)
            {
                var artPath = $"{ArtFolder}/{IceAltarArtFiles[index]}";
                if (!ConfigureAsepriteImporter(artPath, failures)) continue;
                var sprite = FindDefaultSprite(artPath);
                if (sprite == null)
                {
                    failures.Add($"ice_altar quadrant {index}: Sprite missing ({artPath})");
                    continue;
                }

                var tilePath = $"{TileFolder}/ice_altar_{index}.asset";
                var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
                if (tile == null)
                {
                    tile = ScriptableObject.CreateInstance<Tile>();
                    tile.name = $"ice_altar_{index}";
                    AssetDatabase.CreateAsset(tile, tilePath);
                }
                tile.sprite = sprite;
                tile.colliderType = Tile.ColliderType.Grid;
                EditorUtility.SetDirty(tile);
                quadrantTiles[index] = tile;
                applied++;
            }

            var renderer = UnityEngine.Object.FindAnyObjectByType<Nyangbingo.World.TilemapRenderer>();
            if (renderer != null && applied == IceAltarArtFiles.Length)
            {
                Undo.RecordObject(renderer, "Apply Ice Altar Quadrant Art");
                renderer.SetIceAltarQuadrantTilesForEditorSetup(quadrantTiles);
                EditorUtility.SetDirty(renderer);
                if (renderer.gameObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(renderer.gameObject.scene);
            }
            return applied;
        }

        [MenuItem("Nyangbingo/Art/Validate Tile Art")]
        public static void ValidateTileArt()
        {
            var failures = new List<string>();

            foreach (var pair in TileArtFiles)
            {
                var tilePath = $"{TileFolder}/{pair.Key}.asset";
                var expectedArtPath = $"{ArtFolder}/{pair.Value}";
                var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);

                if (tile == null)
                {
                    failures.Add($"{pair.Key}: Tile 에셋 누락");
                    continue;
                }

                if (tile.sprite == null)
                {
                    failures.Add($"{pair.Key}: Sprite 참조 누락");
                    continue;
                }

                var actualArtPath = AssetDatabase.GetAssetPath(tile.sprite);
                if (!string.Equals(actualArtPath, expectedArtPath, StringComparison.Ordinal))
                {
                    failures.Add($"{pair.Key}: 예상 '{expectedArtPath}', 실제 '{actualArtPath}'");
                }
            }


            for (var index = 0; index < IceAltarArtFiles.Length; index++)
            {
                var tilePath = $"{TileFolder}/ice_altar_{index}.asset";
                var expectedArtPath = $"{ArtFolder}/{IceAltarArtFiles[index]}";
                var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
                if (tile?.sprite == null)
                    failures.Add($"ice_altar quadrant {index}: Tile or Sprite missing ({tilePath})");
                else if (!string.Equals(AssetDatabase.GetAssetPath(tile.sprite), expectedArtPath,
                             StringComparison.Ordinal))
                    failures.Add($"ice_altar quadrant {index}: expected '{expectedArtPath}', " +
                                 $"actual '{AssetDatabase.GetAssetPath(tile.sprite)}'");
            }

            if (failures.Count > 0)
            {
                Debug.LogError(
                    $"[Nyangbingo] Tile art validation failed: {failures.Count} issue(s).\n- " +
                    string.Join("\n- ", failures));
                return;
            }

            Debug.Log($"[Nyangbingo] Tile art validation passed: " +
                      $"{TileArtFiles.Count + IceAltarArtFiles.Length}/" +
                      $"{TileArtFiles.Count + IceAltarArtFiles.Length}.");
        }

        [MenuItem("Nyangbingo/Art/Apply Character Art")]
        public static void ApplyCharacterArt()
        {
            var failures = new List<string>();
            var sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);

            foreach (var pair in CharacterArtFiles)
            {
                var artPath = $"{CharacterArtFolder}/{pair.Value}";
                var imported = string.Equals(
                    System.IO.Path.GetExtension(artPath), ".png", StringComparison.OrdinalIgnoreCase)
                    ? ConfigureCharacterPngImporter(artPath, failures)
                    : ConfigureAsepriteImporter(artPath, failures);
                if (!imported) continue;

                var sprite = FindDefaultSprite(artPath);
                if (sprite == null)
                {
                    failures.Add($"{pair.Key}: 기본 Sprite를 불러오지 못했습니다. ({artPath})");
                    continue;
                }

                sprites[pair.Key] = sprite;
            }

            foreach (var pair in GaekgwiEffectArtFiles)
                ConfigureAsepriteImporter($"{CharacterArtFolder}/{pair.Value}", failures);
            var ropeArtPath = $"{CharacterArtFolder}/player_rope.aseprite";
            ConfigureAsepriteImporter(ropeArtPath, failures);
            var ropeFrames = FindLongestAnimationFrames(ropeArtPath);
            if (ropeFrames.Count != 4) failures.Add("player_rope: 로프 애니메이션 4프레임 필요");
            var imugiElectricFrames = LoadImugiElectricAttackFrames(failures);

            if (failures.Count > 0)
            {
                Debug.LogError(
                    $"[Nyangbingo] Character art integration failed: {sprites.Count}/{CharacterArtFiles.Count} imported.\n- " +
                    string.Join("\n- ", failures));
                return;
            }

            var catalog = AssetDatabase.LoadAssetAtPath<CharacterArtCatalog>(CharacterArtCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CharacterArtCatalog>();
                AssetDatabase.CreateAsset(catalog, CharacterArtCatalogPath);
            }

            var serializedCatalog = new SerializedObject(catalog);
            var entries = serializedCatalog.FindProperty("entries");
            entries.arraySize = CharacterArtFiles.Count;
            var index = 0;
            foreach (var pair in CharacterArtFiles)
            {
                var entry = entries.GetArrayElementAtIndex(index++);
                entry.FindPropertyRelative("id").stringValue = pair.Key;
                entry.FindPropertyRelative("sprite").objectReferenceValue = sprites[pair.Key];
                entry.FindPropertyRelative("sourceFacesRight").boolValue =
                    string.Equals(pair.Key, "mother_bulgasari", StringComparison.Ordinal) ||
                    string.Equals(pair.Key, "gaekgwi", StringComparison.Ordinal);
                var artPath = $"{CharacterArtFolder}/{pair.Value}";
                var idleTag = string.Equals(pair.Key, "imugi", StringComparison.Ordinal) ||
                              string.Equals(pair.Key, "gangcheol", StringComparison.Ordinal) ||
                              string.Equals(pair.Key, "gangcheol_boss", StringComparison.Ordinal)
                    ? "default"
                    : "idle";
                var specialTag = string.Equals(pair.Key, "imugi", StringComparison.Ordinal)
                    ? "marble"
                    : string.Equals(pair.Key, "gaekgwi", StringComparison.Ordinal)
                        ? "dash"
                    : "skill";
                var idleFrames = string.Equals(pair.Key, "magpie", StringComparison.Ordinal)
                    ? FindNamedSpriteFrames(
                        artPath,
                        "Frame_0",
                        "Frame_1",
                        "Frame_2",
                        "Frame_3")
                    : FindAnimationFrames(artPath, idleTag);
                // The final Imugi head is a single static frame without animation tags.
                if (pair.Key == "imugi" && idleFrames.Count == 0)
                    idleFrames = new[] { sprites[pair.Key] };
                var isMagpie = string.Equals(pair.Key, "magpie", StringComparison.Ordinal);
                SetSpriteArray(entry.FindPropertyRelative("idleFrames"),
                    isMagpie && idleFrames.Count > 0
                        ? new[] { idleFrames[0] }
                        : idleFrames);
                SetSpriteArray(entry.FindPropertyRelative("walkFrames"),
                    isMagpie
                        ? idleFrames.Skip(1).Take(2).ToArray()
                        : FindAnimationFrames(artPath, "walk"));
                SetSpriteArray(entry.FindPropertyRelative("jumpFrames"),
                    FindAnimationFrames(artPath, "jump"));
                SetSpriteArray(entry.FindPropertyRelative("fallFrames"),
                    FindAnimationFrames(artPath, "fall"));
                SetSpriteArray(entry.FindPropertyRelative("landFrames"),
                    FindAnimationFrames(artPath, "up"));
                SetSpriteArray(entry.FindPropertyRelative("ropeFrames"),
                    pair.Key == "player" ? ropeFrames : Array.Empty<Sprite>());
                SetSpriteArray(entry.FindPropertyRelative("attackFrames"),
                    isMagpie && idleFrames.Count > 3
                        ? new[] { idleFrames[3] }
                        : FindAnimationFrames(artPath, "attack"));
                SetSpriteArray(entry.FindPropertyRelative("hitFrames"),
                    FindAnimationFrames(artPath, "hit"));
                SetSpriteArray(entry.FindPropertyRelative("deathFrames"),
                    FindAnimationFrames(artPath, "die"));
                SetSpriteArray(entry.FindPropertyRelative("fleeFrames"),
                    FindAnimationFrames(artPath, "flee"));
                var specialFrames = string.Equals(
                    pair.Key, "king_dokkaebi", StringComparison.Ordinal)
                    ? FindNamedSpriteFrames(
                        artPath,
                        "Frame_12",
                        "Frame_13",
                        "Frame_14",
                        "Frame_15",
                        "Frame_16")
                    : FindAnimationFrames(artPath, specialTag);
                SetSpriteArray(entry.FindPropertyRelative("specialFrames"), specialFrames);
                if (string.Equals(pair.Key, "gaekgwi", StringComparison.Ordinal))
                {
                    SetSpriteArray(entry.FindPropertyRelative("dashEffectFrames"),
                        FindLongestAnimationFrames(
                            $"{CharacterArtFolder}/{GaekgwiEffectArtFiles["dash"]}"));
                    SetSpriteArray(entry.FindPropertyRelative("impactEffectFrames"),
                        FindLongestAnimationFrames(
                            $"{CharacterArtFolder}/{GaekgwiEffectArtFiles["impact"]}"));
                }
                else
                {
                    SetSpriteArray(entry.FindPropertyRelative("dashEffectFrames"), Array.Empty<Sprite>());
                    SetSpriteArray(entry.FindPropertyRelative("impactEffectFrames"), Array.Empty<Sprite>());
                }
            }
            serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            SaveImugiElectricAttackFrames(imugiElectricFrames);
            AssetDatabase.SaveAssets();

            Debug.Log(
                $"[Nyangbingo] Character art integration completed: {sprites.Count}/{CharacterArtFiles.Count}, " +
                $"Imugi electric {imugiElectricFrames.Count}/7, PPU={PixelsPerUnit:0}. " +
                "Recreate MainGame scene to wire the catalog.");
        }

        [MenuItem("Nyangbingo/Art/Validate Character Art")]
        public static void ValidateCharacterArt()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CharacterArtCatalog>(CharacterArtCatalogPath);
            var failures = new List<string>();
            if (catalog == null)
            {
                failures.Add($"캐릭터 아트 카탈로그 누락: {CharacterArtCatalogPath}");
            }
            else
            {
                foreach (var pair in CharacterArtFiles)
                {
                    var sprite = catalog.FindSprite(pair.Key);
                    var expectedPath = $"{CharacterArtFolder}/{pair.Value}";
                    if (sprite == null)
                    {
                        failures.Add($"{pair.Key}: Sprite 참조 누락");
                    }
                    else if (!string.Equals(AssetDatabase.GetAssetPath(sprite), expectedPath,
                                 StringComparison.Ordinal))
                    {
                        failures.Add(
                            $"{pair.Key}: 예상 '{expectedPath}', 실제 '{AssetDatabase.GetAssetPath(sprite)}'");
                    }

                    var entry = catalog.Find(pair.Key);
                    ValidateAnimationFrames(pair.Key, entry, failures);
                }
            }
            var gameplayCatalog =
                AssetDatabase.LoadAssetAtPath<GameplayArtCatalog>(GameplayArtCatalogPath);
            if (gameplayCatalog == null || gameplayCatalog.ImugiElectricAttackFrames.Count != 7)
                failures.Add(
                    $"imugi electric: expected 7 frames, actual=" +
                    $"{gameplayCatalog?.ImugiElectricAttackFrames.Count ?? 0}");

            if (failures.Count > 0)
            {
                Debug.LogError(
                    $"[Nyangbingo] Character art validation failed: {failures.Count} issue(s).\n- " +
                    string.Join("\n- ", failures));
                return;
            }

            Debug.Log(
                $"[Nyangbingo] Character art and animation validation passed: " +
                $"{CharacterArtFiles.Count}/{CharacterArtFiles.Count}, Imugi electric 7/7.");
        }

        [MenuItem("Nyangbingo/Art/Apply Item Art")]
        public static void ApplyItemArt()
        {
            var failures = new List<string>();
            var sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            foreach (var pair in ItemArtFiles)
            {
                var artPath = ResolveArtPath(ItemArtFolder, pair.Value);
                if (!ConfigureAsepriteImporter(artPath, failures)) continue;
                var sprite = FindDefaultSprite(artPath);
                if (sprite == null)
                    failures.Add($"{pair.Key}: 기본 Sprite를 불러오지 못했습니다. ({artPath})");
                else
                    sprites[pair.Key] = sprite;
            }

            if (failures.Count > 0)
            {
                Debug.LogError(
                    $"[Nyangbingo] Item art integration failed: {sprites.Count}/{ItemArtFiles.Count}.\n- " +
                    string.Join("\n- ", failures));
                return;
            }

            var catalog = AssetDatabase.LoadAssetAtPath<ItemArtCatalog>(ItemArtCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ItemArtCatalog>();
                AssetDatabase.CreateAsset(catalog, ItemArtCatalogPath);
            }

            var serializedCatalog = new SerializedObject(catalog);
            var entries = serializedCatalog.FindProperty("entries");
            foreach (var pair in ItemArtFiles)
            {
                var entry = FindOrAddItemEntry(entries, pair.Key);
                entry.FindPropertyRelative("id").stringValue = pair.Key;
                entry.FindPropertyRelative("sprite").objectReferenceValue = sprites[pair.Key];
            }
            serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Nyangbingo] Item art integration completed: {sprites.Count}/{ItemArtFiles.Count}.");
        }

        private static SerializedProperty FindOrAddItemEntry(SerializedProperty entries, string id)
        {
            for (var index = 0; index < entries.arraySize; index++)
            {
                var entry = entries.GetArrayElementAtIndex(index);
                if (entry.FindPropertyRelative("id").stringValue == id) return entry;
            }

            entries.InsertArrayElementAtIndex(entries.arraySize);
            return entries.GetArrayElementAtIndex(entries.arraySize - 1);
        }

        [MenuItem("Nyangbingo/Art/Validate Item Art")]
        public static void ValidateItemArt()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ItemArtCatalog>(ItemArtCatalogPath);
            var failures = new List<string>();
            if (catalog == null)
            {
                failures.Add($"아이템 아트 카탈로그 누락: {ItemArtCatalogPath}");
            }
            else
            {
                foreach (var pair in ItemArtFiles)
                {
                    var sprite = catalog.FindSprite(pair.Key);
                    var expectedPath = ResolveArtPath(ItemArtFolder, pair.Value);
                    if (sprite == null)
                        failures.Add($"{pair.Key}: Sprite 참조 누락");
                    else if (!string.Equals(AssetDatabase.GetAssetPath(sprite), expectedPath,
                                 StringComparison.Ordinal))
                        failures.Add(
                            $"{pair.Key}: 예상 '{expectedPath}', 실제 '{AssetDatabase.GetAssetPath(sprite)}'");
                }
            }

            if (failures.Count > 0)
            {
                Debug.LogError(
                    $"[Nyangbingo] Item art validation failed: {failures.Count} issue(s).\n- " +
                    string.Join("\n- ", failures));
                return;
            }

            Debug.Log($"[Nyangbingo] Item art validation passed: {ItemArtFiles.Count}/{ItemArtFiles.Count}.");
        }

        [MenuItem("Nyangbingo/Art/Apply Environment Art")]
        public static void ApplyEnvironmentArt()
        {
            var failures = new List<string>();
            var distantPath = $"{EnvironmentArtFolder}/distant_view.png";
            var cloudsPath = $"{EnvironmentArtFolder}/clouds.png";
            var undergroundPath = $"{EnvironmentArtFolder}/underground.png";
            var titleBackgroundPath = $"{EnvironmentArtFolder}/keyvisual-day.png";
            var titlePath = $"{EnvironmentArtFolder}/title.aseprite";
            ConfigurePngImporter(distantPath, failures);
            ConfigurePngImporter(cloudsPath, failures);
            ConfigurePngImporter(undergroundPath, failures);
            ConfigurePngImporter(titleBackgroundPath, failures);
            ConfigureAsepriteImporter(titlePath, failures);

            var distant = AssetDatabase.LoadAssetAtPath<Sprite>(distantPath);
            var clouds = AssetDatabase.LoadAssetAtPath<Sprite>(cloudsPath);
            var underground = AssetDatabase.LoadAssetAtPath<Sprite>(undergroundPath);
            var titleBackground = AssetDatabase.LoadAssetAtPath<Sprite>(titleBackgroundPath);
            var titleFrames = FindAnimationFrames(titlePath, "title_on");
            if (distant == null) failures.Add("원경 Sprite 누락");
            if (clouds == null) failures.Add("구름 Sprite 누락");
            if (underground == null) failures.Add("지하 배경 Sprite 누락");
            if (titleBackground == null) failures.Add("타이틀 키비주얼 Sprite 누락");
            if (titleFrames.Count < 10) failures.Add($"title_on 프레임 부족 ({titleFrames.Count}/10)");
            if (failures.Count > 0)
            {
                Debug.LogError("[Nyangbingo] Environment art integration failed.\n- " +
                               string.Join("\n- ", failures));
                return;
            }

            var catalog = AssetDatabase.LoadAssetAtPath<EnvironmentArtCatalog>(EnvironmentArtCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<EnvironmentArtCatalog>();
                AssetDatabase.CreateAsset(catalog, EnvironmentArtCatalogPath);
            }
            var serializedCatalog = new SerializedObject(catalog);
            serializedCatalog.FindProperty("distantView").objectReferenceValue = distant;
            serializedCatalog.FindProperty("clouds").objectReferenceValue = clouds;
            serializedCatalog.FindProperty("underground").objectReferenceValue = underground;
            serializedCatalog.FindProperty("titleBackground").objectReferenceValue = titleBackground;
            SetSpriteArray(serializedCatalog.FindProperty("titleFrames"), titleFrames);
            serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            if (!NyangbingoMainGameSceneCreator.TryRefreshEnvironmentArtInMainGameScene(
                    catalog, out var refreshSummary))
            {
                Debug.LogWarning("[Nyangbingo] Environment art catalog saved, but MainGame parallax " +
                                 $"refresh skipped: {refreshSummary}. HUD 배선은 유지됩니다.");
            }

            Debug.Log("[Nyangbingo] Environment art integration completed: sky 2/2, underground 1/1, " +
                      "title background 1/1, title 10/10. Parallax refreshed without rebuilding MainGame scene.");
        }

        [MenuItem("Nyangbingo/Art/Validate Environment Art")]
        public static void ValidateEnvironmentArt()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<EnvironmentArtCatalog>(EnvironmentArtCatalogPath);
            var valid = catalog != null && catalog.DistantView != null && catalog.Clouds != null &&
                        catalog.Underground != null && catalog.TitleBackground != null &&
                        catalog.TitleFrames.Count >= 10 && catalog.HasDayNightSurfaceSet;
            if (!valid)
            {
                Debug.LogError("[Nyangbingo] Environment art validation failed: " +
                                "day/night surface set, title background, title logo, or preserved underground reference is missing.");
                return;
            }
            Debug.Log("[Nyangbingo] Environment art validation passed: day/night surface 10/10, " +
                      "legacy sky 2/2, underground 1/1, title background 1/1, title 10/10.");
        }

        // 기획 대응을 확인한 전용 원본 경로만 전달한다. 일부 미제공이면 기존 연결을 보존한다.
        public static void ApplyS1S3GuideArt(string arrowPath, string leakPath, string repairPath, string successPath)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("S1~S3 아트 연결은 비플레이 상태에서만 가능합니다.");
            var catalog = AssetDatabase.LoadAssetAtPath<GameplayArtCatalog>(GameplayArtCatalogPath);
            if (catalog == null) throw new InvalidOperationException("GameplayArtCatalog 없음");
            var failures = new List<string>();
            var paths = new[] { arrowPath, leakPath, repairPath, successPath };
            var fields = new[] { "goalDirectionArrow", "sealLeakMarkerFrames", "sealRepairCheck", "sealSuccessFrames" };
            var selected = new IReadOnlyList<Sprite>[paths.Length];
            for (var i = 0; i < paths.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(paths[i])) continue;
                if (!paths[i].StartsWith("Assets/", StringComparison.Ordinal))
                {
                    failures.Add($"{fields[i]}는 Assets/ 아래의 전용 원본 경로가 필요합니다.");
                    continue;
                }
                if (!ConfigureAsepriteImporter(paths[i], failures)) continue;
                var frames = FindLongestAnimationFrames(paths[i]);
                var valid = i == 1 ? frames.Count > 0 : i == 3 ? frames.Count >= 3 && frames.Count <= 4 : frames.Count == 1;
                if (!valid || frames.Any(frame => frame == null))
                    failures.Add($"{fields[i]} 프레임 수 오류: {frames.Count} ({paths[i]})");
                else selected[i] = frames;
            }
            if (failures.Count > 0) throw new InvalidOperationException(string.Join("\n", failures));
            var serialized = new SerializedObject(catalog);
            for (var i = 0; i < selected.Length; i++)
            {
                if (selected[i] == null) continue;
                if (i == 0 || i == 2) serialized.FindProperty(fields[i]).objectReferenceValue = selected[i][0];
                else SetSpriteArray(serialized.FindProperty(fields[i]), selected[i]);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        public static void ApplyDeliveredMisc1Art()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("전달 아트 연결은 비플레이 상태에서만 가능합니다.");
            var catalog = AssetDatabase.LoadAssetAtPath<GameplayArtCatalog>(GameplayArtCatalogPath);
            if (catalog == null) throw new InvalidOperationException("GameplayArtCatalog 없음");
            var failures = new List<string>();
            var names = new[] { "seal_success_wave", "result_continue", "gaekgwi_boss_bar",
                "singijeon_sondae_attack", "cold_wave_singijeon_attack", "ice_root_whipfan_attack" };
            var counts = new[] { 10, 1, 1, 8, 9, 7 };
            var frames = new IReadOnlyList<Sprite>[names.Length];
            for (var index = 0; index < names.Length; index++)
            {
                ConfigureAsepriteImporter($"{GameplayArtFolder}/DeliveredMisc1/{names[index]}.aseprite", failures);
                frames[index] = LoadDeliveredMiscFrames(names[index], counts[index], failures);
            }
            if (failures.Count > 0) throw new InvalidOperationException(string.Join("\n", failures));
            var serialized = new SerializedObject(catalog);
            SetSpriteArray(serialized.FindProperty("sealSuccessFrames"), frames[0]);
            serialized.FindProperty("resultContinue").objectReferenceValue = frames[1][0];
            serialized.FindProperty("bossHealthGaekgwi").objectReferenceValue = frames[2][0];
            var weapons = serialized.FindProperty("weaponAttackAnimations");
            var ids = new[] { "singijeon_sondae", "cold_wave_singijeon", "ice_root_whipfan" };
            for (var index = 0; index < ids.Length; index++)
            {
                var entryIndex = -1;
                for (var existing = 0; existing < weapons.arraySize; existing++)
                    if (weapons.GetArrayElementAtIndex(existing).FindPropertyRelative("itemId").stringValue == ids[index])
                        entryIndex = existing;
                if (entryIndex < 0) { entryIndex = weapons.arraySize; weapons.arraySize++; }
                var entry = weapons.GetArrayElementAtIndex(entryIndex);
                entry.FindPropertyRelative("itemId").stringValue = ids[index];
                SetSpriteArray(entry.FindPropertyRelative("frames"), frames[index + 3]);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        private static IReadOnlyList<Sprite> LoadDeliveredMiscFrames(string name, int count, ICollection<string> failures)
        {
            var frames = new List<Sprite>();
            for (var index = 0; index < count; index++)
            {
                var path = $"{GameplayArtFolder}/DeliveredMisc1/{name}_frame_{index:00}.png";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) { failures.Add($"PNG importer 없음: {path}"); continue; }
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = PixelsPerUnit;
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
                if (name == "singijeon_sondae_attack" || name == "cold_wave_singijeon_attack" ||
                    name == "ice_root_whipfan_attack")
                    AlignWeaponAttackPivot(importer);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) failures.Add($"Sprite 없음: {path}");
                else frames.Add(sprite);
            }
            return frames;
        }

        private static void AlignWeaponAttackPivot(TextureImporter importer)
        {
            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            if (provider == null)
                throw new InvalidOperationException($"Sprite data provider 없음: {importer.assetPath}");
            provider.InitSpriteEditorDataProvider();
            var editable = provider.GetDataProvider<ISpriteFrameEditCapability>();
            if (editable == null || !editable.GetEditCapability().HasCapability(EEditCapability.EditPivot))
                throw new InvalidOperationException($"공격 피벗 편집 불가: {importer.assetPath}");
            var rects = provider.GetSpriteRects();
            if (rects.Length != 1)
                throw new InvalidOperationException($"공격 PNG는 단일 Sprite여야 합니다: {importer.assetPath}");
            // These 32x32 canvases retain the feet at the bottom edge, like the
            // existing Aseprite attack art. Keep the physics root and visual scale unchanged.
            var pivot = new Vector2(.5f, 0f);
            if (rects[0].pivot == pivot && rects[0].alignment == SpriteAlignment.Custom) return;
            rects[0].alignment = SpriteAlignment.Custom;
            rects[0].pivot = pivot;
            provider.SetSpriteRects(rects);
            provider.Apply();
            importer.SaveAndReimport();
        }

        public static void ApplyDeliveredUiArt()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("UI 아트 연결은 비플레이 상태에서만 가능합니다.");
            const string folder = "Assets/Art/Gameplay/DeliveredUi/";
            var failures = new List<string>();
            var fieldFiles = new Dictionary<string, string>
            {
                ["goalDirectionArrow"] = "ui_raid_arrow.aseprite",
                ["sealRepairCheck"] = "check.aseprite",
                ["yokaiDamageIcon"] = "yokai_damage.aseprite",
                ["burnStatusIcon"] = "burn_status.aseprite",
                ["sealLeakStatusIcon"] = "seal_leak_icon.aseprite",
                ["sealLeakStaticMarker"] = "seal_leak_marker.aseprite"
            };
            var sprites = new Dictionary<string, Sprite>();
            foreach (var pair in fieldFiles)
            {
                var path = folder + pair.Value;
                ConfigureAsepriteImporter(path, failures);
                var frames = FindLongestAnimationFrames(path);
                if (frames.Count != 1) failures.Add($"{pair.Value}: 1프레임 필요, 실제 {frames.Count}");
                else sprites[pair.Key] = frames[0];
            }
            var effectPath = folder + "seal_leak_effect.aseprite";
            ConfigureAsepriteImporter(effectPath, failures);
            // Aseprite trims transparent cel margins. Preserve the delivered 16x16
            // canvas so uGUI does not enlarge each small animation cel differently.
            var effectFrames = new List<Sprite>();
            for (var index = 0; index < 6; index++)
            {
                var path = folder + $"seal_leak_frame_{index}.png";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) { failures.Add($"{path}: TextureImporter 없음"); continue; }
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = PixelsPerUnit;
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null) effectFrames.Add(sprite);
                else failures.Add($"{path}: Sprite 없음");
            }
            if (effectFrames.Count != 6) failures.Add($"찬바람 누출: 6프레임 필요, 실제 {effectFrames.Count}");
            var vaultPath = ItemArtFolder + "/DeliveredFinal/vault_seal.aseprite";
            ConfigureAsepriteImporter(vaultPath, failures);
            var vault = FindDefaultSprite(vaultPath);
            if (vault == null) failures.Add("창고 봉인 Sprite 없음");
            // Import unselected variants too, but never silently change the chosen check.
            ConfigureAsepriteImporter(folder + "check2.aseprite", failures);
            ConfigureAsepriteImporter(folder + "yokai_damage_alt.aseprite", failures);
            var gameplay = AssetDatabase.LoadAssetAtPath<GameplayArtCatalog>(GameplayArtCatalogPath);
            var items = AssetDatabase.LoadAssetAtPath<ItemArtCatalog>(ItemArtCatalogPath);
            if (gameplay == null || items == null) failures.Add("아트 카탈로그 없음");
            if (failures.Count > 0) throw new InvalidOperationException(string.Join("\n", failures));
            var serialized = new SerializedObject(gameplay);
            foreach (var pair in sprites) serialized.FindProperty(pair.Key).objectReferenceValue = pair.Value;
            SetSpriteArray(serialized.FindProperty("sealLeakMarkerFrames"), effectFrames);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var itemSerialized = new SerializedObject(items);
            var entry = FindOrAddItemEntry(itemSerialized.FindProperty("entries"), "vault_seal");
            entry.FindPropertyRelative("id").stringValue = "vault_seal";
            entry.FindPropertyRelative("sprite").objectReferenceValue = vault;
            itemSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(gameplay);
            EditorUtility.SetDirty(items);
            AssetDatabase.SaveAssets();
        }

        public static void ApplyFanWindArt()
        {
            var failures = new List<string>();
            var path = $"{GameplayArtFolder}/fan_wind.aseprite";
            ConfigureAsepriteImporter(path, failures);
            var frames = FindLongestAnimationFrames(path);
            if (frames.Count != 5) failures.Add($"부채 바람 5프레임 필요, 실제 {frames.Count}");
            var catalog = AssetDatabase.LoadAssetAtPath<GameplayArtCatalog>(GameplayArtCatalogPath);
            if (catalog == null) failures.Add("GameplayArtCatalog 없음");
            if (failures.Count > 0) throw new InvalidOperationException(string.Join("\n", failures));
            var serialized = new SerializedObject(catalog);
            SetSpriteArray(serialized.FindProperty("fanWindFrames"), frames);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        // 공격 원본 수신분만 갱신한다. 다른 캐릭터·로프·월드 아트의 연결은 보존한다.
        public static void ApplyPlayerWeaponArt()
        {
            var failures = new List<string>();
            var ids = new[] { "dokkaebi_club", "gakgung", "ice_root_bow", "seonge_gakgung",
                "straw_sling", "hapjukseon", "cheolseon", "seolpungseon", "seonge_fan", "cold_wave_fan",
                "singijeon_sondae", "cold_wave_singijeon", "ice_root_whipfan" };
            var expectedCounts = new[] { 6, 6, 9, 6, 7, 5, 5, 5, 5, 5, 8, 9, 7 };
            var attackFrames = new List<IReadOnlyList<Sprite>>();
            for (var index = 0; index < ids.Length; index++)
            {
                if (index >= 10)
                {
                    attackFrames.Add(LoadDeliveredMiscFrames(ids[index] + "_attack", expectedCounts[index], failures));
                    continue;
                }
                var path = $"{GameplayArtFolder}/WeaponAttacks/{ids[index]}_attack.aseprite";
                ConfigureAsepriteImporter(path, failures);
                var frames = FindLongestAnimationFrames(path);
                if (frames.Count != expectedCounts[index])
                    failures.Add($"{ids[index]}: {expectedCounts[index]}프레임 필요, 실제 {frames.Count}");
                attackFrames.Add(frames);
            }
            var playerPath = $"{CharacterArtFolder}/player_frostclaw.aseprite";
            ConfigureAsepriteImporter(playerPath, failures);
            var tags = new[] { "idle", "walk", "jump", "die", "attack" };
            var fields = new[] { "idleFrames", "walkFrames", "jumpFrames", "deathFrames", "attackFrames" };
            var playerFrames = tags.Select(tag => FindAnimationFrames(playerPath, tag)).ToArray();
            for (var i = 0; i < tags.Length; i++)
                if (playerFrames[i].Count != (i == 4 ? 5 : 6)) failures.Add($"player {tags[i]} 프레임 수 불일치");
            var arrowPath = $"{GameplayArtFolder}/arrow_projectile.aseprite";
            var stonePath = $"{GameplayArtFolder}/sling_stone_projectile.aseprite";
            ConfigureAsepriteImporter(arrowPath, failures);
            ConfigureAsepriteImporter(stonePath, failures);
            var arrowFrames = FindLongestAnimationFrames(arrowPath);
            var stoneFrames = FindLongestAnimationFrames(stonePath);
            if (arrowFrames.Count != 1 || stoneFrames.Count != 1) failures.Add("투사체 각 1프레임 필요");
            var characterCatalog = AssetDatabase.LoadAssetAtPath<CharacterArtCatalog>(CharacterArtCatalogPath);
            var gameplayCatalog = AssetDatabase.LoadAssetAtPath<GameplayArtCatalog>(GameplayArtCatalogPath);
            if (characterCatalog == null || gameplayCatalog == null) failures.Add("기존 아트 카탈로그 없음");
            if (failures.Count > 0) throw new InvalidOperationException(string.Join("\n", failures));
            var characters = new SerializedObject(characterCatalog);
            var entries = characters.FindProperty("entries");
            SerializedProperty player = null;
            for (var i = 0; i < entries.arraySize; i++)
            {
                var candidate = entries.GetArrayElementAtIndex(i);
                if (candidate.FindPropertyRelative("id").stringValue == "player") player = candidate;
            }
            if (player == null) throw new InvalidOperationException("player 카탈로그 항목 없음");
            player.FindPropertyRelative("sprite").objectReferenceValue = playerFrames[0][0];
            for (var i = 0; i < fields.Length; i++)
                SetSpriteArray(player.FindPropertyRelative(fields[i]), playerFrames[i]);
            characters.ApplyModifiedPropertiesWithoutUndo();
            var gameplay = new SerializedObject(gameplayCatalog);
            var weapons = gameplay.FindProperty("weaponAttackAnimations");
            weapons.arraySize = ids.Length;
            for (var i = 0; i < ids.Length; i++)
            {
                var weapon = weapons.GetArrayElementAtIndex(i);
                weapon.FindPropertyRelative("itemId").stringValue = ids[i];
                SetSpriteArray(weapon.FindPropertyRelative("frames"), attackFrames[i]);
            }
            gameplay.FindProperty("arrowProjectile").objectReferenceValue = arrowFrames[0];
            gameplay.FindProperty("slingStoneProjectile").objectReferenceValue = stoneFrames[0];
            gameplay.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(characterCatalog);
            EditorUtility.SetDirty(gameplayCatalog);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Nyangbingo/Art/Apply Combat and Temperature Art")]
        public static void ApplyCombatAndTemperatureArt()
        {
            var failures = new List<string>();
            var temperaturePath = $"{GameplayArtFolder}/temperature.aseprite";
            var attackPath = $"{GameplayArtFolder}/player_attack.aseprite";
            var miningPath = $"{GameplayArtFolder}/mining_crack.aseprite";
            var warningPath = $"{GameplayArtFolder}/boss_warning.aseprite";
            var gangcheoriFirePath = $"{GameplayArtFolder}/gangcheori_special_fire.aseprite";
            var playerFireHitPath = $"{GameplayArtFolder}/player_fire_hit.aseprite";
            var projectilePath = $"{GameplayArtFolder}/blue_projectile.aseprite";
            ConfigureAsepriteImporter(temperaturePath, failures);
            ConfigureAsepriteImporter(attackPath, failures);
            ConfigureAsepriteImporter(miningPath, failures);
            ConfigureAsepriteImporter(warningPath, failures);
            ConfigureAsepriteImporter(gangcheoriFirePath, failures);
            ConfigureAsepriteImporter(playerFireHitPath, failures);
            ConfigureAsepriteImporter(projectilePath, failures);
            var temperatureFrames = FindLongestAnimationFrames(temperaturePath);
            var attackFrames = FindLongestAnimationFrames(attackPath);
            var miningFrames = FindLongestAnimationFrames(miningPath);
            var warningFrames = FindLongestAnimationFrames(warningPath);
            var gangcheoriFireFrames = FindLongestAnimationFrames(gangcheoriFirePath);
            var playerFireHitFrames = FindLongestAnimationFrames(playerFireHitPath);
            var imugiElectricFrames = LoadImugiElectricAttackFrames(failures);
            var projectileFrames = FindLongestAnimationFrames(projectilePath);
            if (temperatureFrames.Count == 0) failures.Add("온도 HUD Sprite 프레임이 없습니다.");
            if (attackFrames.Count == 0) failures.Add("서리발톱 공격 이펙트 Sprite 프레임이 없습니다.");
            if (miningFrames.Count == 0) failures.Add("채굴 균열 Sprite 프레임이 없습니다.");
            if (warningFrames.Count == 0) failures.Add("보스 경고 Sprite 프레임이 없습니다.");
            if (projectileFrames.Count == 0) failures.Add("파란 투사체 Sprite 프레임이 없습니다.");
            if (gangcheoriFireFrames.Count == 0)
                failures.Add("Gangcheori special fire effect frames are missing.");
            if (playerFireHitFrames.Count == 0)
                failures.Add("Player fire-hit effect frames are missing.");
            if (failures.Count > 0)
            {
                Debug.LogError("[Nyangbingo] Combat/temperature art integration failed.\n- " +
                               string.Join("\n- ", failures));
                return;
            }

            var catalog = AssetDatabase.LoadAssetAtPath<GameplayArtCatalog>(GameplayArtCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<GameplayArtCatalog>();
                AssetDatabase.CreateAsset(catalog, GameplayArtCatalogPath);
            }
            var serializedCatalog = new SerializedObject(catalog);
            SetSpriteArray(serializedCatalog.FindProperty("temperatureFrames"), temperatureFrames);
            SetSpriteArray(serializedCatalog.FindProperty("playerAttackFrames"), attackFrames);
            SetSpriteArray(serializedCatalog.FindProperty("miningCrackFrames"), miningFrames);
            SetSpriteArray(serializedCatalog.FindProperty("bossWarningFrames"), warningFrames);
            SetSpriteArray(serializedCatalog.FindProperty("gangcheoriSpecialFireFrames"),
                gangcheoriFireFrames);
            SetSpriteArray(serializedCatalog.FindProperty("playerFireHitFrames"),
                playerFireHitFrames);
            SetSpriteArray(serializedCatalog.FindProperty("imugiElectricAttackFrames"),
                imugiElectricFrames);
            SetSpriteArray(serializedCatalog.FindProperty("blueProjectileFrames"), projectileFrames);
            serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Nyangbingo] Combat/temperature art integration completed: " +
                      $"temperature {temperatureFrames.Count}, attack {attackFrames.Count}, " +
                      $"mining {miningFrames.Count}, warning {warningFrames.Count}, " +
                      $"Gangcheori fire {gangcheoriFireFrames.Count}, " +
                      $"player fire hit {playerFireHitFrames.Count}, " +
                      $"Imugi electric {imugiElectricFrames.Count}, " +
                      $"projectile {projectileFrames.Count}. Catalog updated without rebuilding MainGame scene.");
        }

        [MenuItem("Nyangbingo/Art/Apply Building Art")]
        public static void ApplyBuildingArt()
        {
            var failures = new List<string>();
            var framesByFile = new Dictionary<string, IReadOnlyList<Sprite>>(StringComparer.Ordinal);
            foreach (var file in BuildingArtFiles.Values.Distinct(StringComparer.Ordinal))
            {
                var path = ResolveArtPath(BuildingArtFolder, file);
                ConfigureAsepriteImporter(path, failures);
                var attackBuilding = IsAttackBuildingFile(file);
                var frames = file == "door.aseprite"
                    ? FindNamedSpriteFrames(path, "Frame_0", "Frame_1", "Frame_2", "Frame_3", "Frame_4", "Frame_5")
                    : attackBuilding ? FindNamedSpriteFrames(path, "Frame_0")
                    : FindLongestAnimationFrames(path);
                if (attackBuilding && FindNamedSpriteFrames(path, "Frame_1", "Frame_2", "Frame_3").Count != 3)
                    failures.Add($"{file}: 공격 프레임 1~3이 필요합니다.");
                if (file == "door.aseprite" && frames.Count != 6)
                    failures.Add($"{file}: 개폐 프레임은 0~5 전체가 필요합니다 (현재 {frames.Count}).");
                if (frames.Count == 0) failures.Add($"{file}: Sprite 프레임이 없습니다.");
                else framesByFile[file] = frames;
            }
            if (failures.Count > 0)
            {
                Debug.LogError("[Nyangbingo] Building art integration failed.\n- " +
                               string.Join("\n- ", failures));
                return;
            }

            var catalog = AssetDatabase.LoadAssetAtPath<BuildingArtCatalog>(BuildingArtCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<BuildingArtCatalog>();
                AssetDatabase.CreateAsset(catalog, BuildingArtCatalogPath);
            }
            var serializedCatalog = new SerializedObject(catalog);
            var entries = serializedCatalog.FindProperty("entries");
            entries.arraySize = BuildingArtFiles.Count;
            var index = 0;
            foreach (var pair in BuildingArtFiles)
            {
                var entry = entries.GetArrayElementAtIndex(index++);
                entry.FindPropertyRelative("id").stringValue = pair.Key;
                SetSpriteArray(entry.FindPropertyRelative("frames"), framesByFile[pair.Value]);
                SetSpriteArray(entry.FindPropertyRelative("attackFrames"),
                    IsAttackBuildingFile(pair.Value)
                        ? FindNamedSpriteFrames(ResolveArtPath(BuildingArtFolder, pair.Value), "Frame_1", "Frame_2", "Frame_3")
                        : Array.Empty<Sprite>());
            }
            serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Nyangbingo] Building art integration completed: " +
                      $"{BuildingArtFiles.Count} IDs / {framesByFile.Count} art files. " +
                      "Catalog updated without rebuilding MainGame scene.");
        }

        [MenuItem("Nyangbingo/Art/Apply World Decoration Art")]
        public static void ApplyWorldDecorationArt()
        {
            var failures = new List<string>();
            var framesByFile = new Dictionary<string, IReadOnlyList<Sprite>>(StringComparer.Ordinal);
            foreach (var file in DecorationArtFiles.Values.Distinct(StringComparer.Ordinal))
            {
                var path = $"{DecorationArtFolder}/{file}";
                ConfigureAsepriteImporter(path, failures);
                var frames = FindLongestAnimationFrames(path);
                if (frames.Count == 0) failures.Add($"{file}: Sprite 프레임이 없습니다.");
                else framesByFile[file] = frames;
            }
            if (failures.Count > 0)
            {
                Debug.LogError("[Nyangbingo] World decoration art integration failed.\n- " +
                               string.Join("\n- ", failures));
                return;
            }

            var catalog = AssetDatabase.LoadAssetAtPath<WorldDecorationArtCatalog>(DecorationArtCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<WorldDecorationArtCatalog>();
                AssetDatabase.CreateAsset(catalog, DecorationArtCatalogPath);
            }
            var serializedCatalog = new SerializedObject(catalog);
            var entries = serializedCatalog.FindProperty("entries");
            entries.arraySize = DecorationArtFiles.Count;
            var index = 0;
            foreach (var pair in DecorationArtFiles)
            {
                var entry = entries.GetArrayElementAtIndex(index++);
                entry.FindPropertyRelative("id").stringValue = pair.Key;
                SetSpriteArray(entry.FindPropertyRelative("frames"), framesByFile[pair.Value]);
            }
            serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Nyangbingo] World decoration art integration completed: " +
                      $"{DecorationArtFiles.Count} decorations. Catalog updated without rebuilding MainGame scene.");
        }

        [MenuItem("Nyangbingo/Art/Validate World Decoration Art")]
        public static void ValidateWorldDecorationArt()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<WorldDecorationArtCatalog>(DecorationArtCatalogPath);
            var failures = DecorationArtFiles.Keys
                .Where(id => catalog?.Find(id)?.Sprite == null)
                .ToArray();
            if (failures.Length > 0)
            {
                Debug.LogError("[Nyangbingo] World decoration art validation failed: " +
                               string.Join(", ", failures));
                return;
            }
            Debug.Log($"[Nyangbingo] World decoration art validation passed: {DecorationArtFiles.Count}.");
        }

        [MenuItem("Nyangbingo/Art/Validate Building Art")]
        public static void ValidateBuildingArt()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BuildingArtCatalog>(BuildingArtCatalogPath);
            var failures = new List<string>();
            foreach (var pair in BuildingArtFiles)
            {
                var art = catalog?.Find(pair.Key);
                var expectedPath = ResolveArtPath(BuildingArtFolder, pair.Value);
                if (art?.Sprite == null)
                    failures.Add($"{pair.Key}: Sprite reference missing");
                else if (!string.Equals(AssetDatabase.GetAssetPath(art.Sprite), expectedPath,
                             StringComparison.Ordinal))
                    failures.Add(
                        $"{pair.Key}: expected '{expectedPath}', actual '{AssetDatabase.GetAssetPath(art.Sprite)}'");
                if (IsAttackBuildingFile(pair.Value) && !HasCurrentAttackFrames(art, expectedPath))
                    failures.Add($"{pair.Key}: 기본 1프레임/발사 3프레임 연결 불일치");
            }
            if (failures.Count > 0)
            {
                Debug.LogError("[Nyangbingo] Building art validation failed:\n- " +
                               string.Join("\n- ", failures));
                return;
            }
            Debug.Log($"[Nyangbingo] Building art validation passed: {BuildingArtFiles.Count} IDs.");
        }

        public static bool IsBuildingArtCurrent(BuildingArtCatalog catalog)
        {
            if (catalog == null) return false;
            var doorFrames = catalog.Find("door")?.Frames;
            if (doorFrames == null || doorFrames.Count != 6) return false;
            for (var index = 0; index < 6; index++)
                if (doorFrames[index] == null || doorFrames[index].name != $"Frame_{index}") return false;
            foreach (var pair in BuildingArtFiles)
            {
                var sprite = catalog.Find(pair.Key)?.Sprite;
                if (sprite == null ||
                    !string.Equals(AssetDatabase.GetAssetPath(sprite),
                        ResolveArtPath(BuildingArtFolder, pair.Value), StringComparison.Ordinal))
                    return false;
                if (IsAttackBuildingFile(pair.Value) &&
                    !HasCurrentAttackFrames(catalog.Find(pair.Key), ResolveArtPath(BuildingArtFolder, pair.Value)))
                    return false;
            }
            return true;
        }

        private static bool HasCurrentAttackFrames(BuildingArtCatalog.Entry entry, string path)
        {
            if (entry == null || entry.Frames.Count != 1 || entry.Sprite == null ||
                entry.Sprite.name != "Frame_0" || entry.AttackFrames.Count != 3) return false;
            for (var index = 0; index < 3; index++)
                if (entry.AttackFrames[index] == null || entry.AttackFrames[index].name != $"Frame_{index + 1}" ||
                    AssetDatabase.GetAssetPath(entry.AttackFrames[index]) != path) return false;
            return true;
        }

        [MenuItem("Nyangbingo/Art/Create Building Art Preview Scene")]
        public static void CreateBuildingArtPreviewScene()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BuildingArtCatalog>(BuildingArtCatalogPath);
            var previewIds = new[]
            {
                "lantern", "sieve", "haetae_statue", "ice_core", "dokkaebi_fire_tower", "cold_wave_core"
            };
            if (catalog == null || previewIds.Any(id => catalog.Find(id)?.Sprite == null))
            {
                Debug.LogError("[Nyangbingo] Building preview failed: Apply Building Art를 먼저 실행하세요.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 4f;
            camera.backgroundColor = new Color(.06f, .08f, .11f);
            cameraObject.AddComponent<AudioListener>();

            for (var index = 0; index < previewIds.Length; index++)
            {
                var id = previewIds[index];
                var art = catalog.Find(id);
                var preview = new GameObject(id);
                preview.transform.position = new Vector3((index - 2.5f) * 2f, .4f, 0f);
                var renderer = preview.AddComponent<SpriteRenderer>();
                renderer.sprite = art.Sprite;
                renderer.sortingOrder = 1;
                preview.AddComponent<RuntimeBuildingSpriteAnimator>().Configure(art.Frames);
                var labelObject = new GameObject("Label");
                labelObject.transform.SetParent(preview.transform, false);
                labelObject.transform.localPosition = new Vector3(0f, -1.35f, 0f);
                var label = labelObject.AddComponent<TextMesh>();
                label.text = id;
                label.anchor = TextAnchor.MiddleCenter;
                label.alignment = TextAlignment.Center;
                label.characterSize = .12f;
                label.fontSize = 32;
                label.color = Color.white;
            }
            EditorSceneManager.SaveScene(scene, BuildingPreviewScenePath);
            Debug.Log("[Nyangbingo] Building art preview scene created: " + BuildingPreviewScenePath);
        }

        [MenuItem("Nyangbingo/Art/Validate Combat and Temperature Art")]
        public static void ValidateCombatAndTemperatureArt()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameplayArtCatalog>(GameplayArtCatalogPath);
            if (catalog == null ||
                catalog.TemperatureFrames.Count == 0 ||
                catalog.PlayerAttackFrames.Count == 0 ||
                catalog.MiningCrackFrames.Count == 0 ||
                catalog.BossWarningFrames.Count == 0 ||
                catalog.GangcheoriSpecialFireFrames.Count != 4 ||
                catalog.PlayerFireHitFrames.Count != 3 ||
                catalog.ImugiElectricAttackFrames.Count != 7 ||
                catalog.BlueProjectileFrames.Count == 0)
            {
                Debug.LogError("[Nyangbingo] Combat/temperature art validation failed: catalog or frames missing.");
                return;
            }
            Debug.Log($"[Nyangbingo] Combat/temperature art validation passed: " +
                      $"temperature {catalog.TemperatureFrames.Count}, attack {catalog.PlayerAttackFrames.Count}, " +
                      $"mining crack {catalog.MiningCrackFrames.Count}, " +
                      $"boss warning {catalog.BossWarningFrames.Count}, " +
                      $"Gangcheori fire {catalog.GangcheoriSpecialFireFrames.Count}/4, " +
                      $"player fire hit {catalog.PlayerFireHitFrames.Count}/3, " +
                      $"Imugi electric {catalog.ImugiElectricAttackFrames.Count}/7, " +
                      $"projectile {catalog.BlueProjectileFrames.Count}.");
        }

        private static void ValidateAnimationFrames(string id, CharacterArtCatalog.Entry entry,
            ICollection<string> failures)
        {
            if (entry == null) return;
            switch (id)
            {
                case "player":
                    RequireFrames(id, "idle", entry.IdleFrames, 6, failures);
                    RequireFrames(id, "walk", entry.WalkFrames, 6, failures);
                    RequireFrames(id, "jump", entry.JumpFrames, 6, failures);
                    // Final delivery has no fall/up clips; runtime already falls back to jump/no landing action.
                    RequireFrames(id, "attack", entry.AttackFrames, 5, failures);
                    RequireFrames(id, "die", entry.DeathFrames, 6, failures);
                    break;
                case "club":
                    RequireFrames(id, "idle", entry.IdleFrames, 3, failures);
                    RequireFrames(id, "walk", entry.WalkFrames, 3, failures);
                    RequireFrames(id, "attack", entry.AttackFrames, 4, failures);
                    RequireFrames(id, "hit", entry.HitFrames, 1, failures);
                    break;
                case "bulgasari":
                    RequireFrames(id, "idle", entry.IdleFrames, 3, failures);
                    RequireFrames(id, "walk", entry.WalkFrames, 5, failures);
                    RequireFrames(id, "attack", entry.AttackFrames, 2, failures);
                    RequireFrames(id, "hit", entry.HitFrames, 1, failures);
                    break;
                case "yakwang":
                    RequireFrames(id, "idle", entry.IdleFrames, 3, failures);
                    RequireFrames(id, "flee", entry.FleeFrames, 2, failures);
                    break;
                case "eoduksini":
                    RequireFrames(id, "walk", entry.WalkFrames, 3, failures);
                    RequireFrames(id, "attack", entry.AttackFrames, 1, failures);
                    break;
                case "king_dokkaebi":
                    RequireFrames(id, "idle", entry.IdleFrames, 3, failures);
                    RequireFrames(id, "walk", entry.WalkFrames, 4, failures);
                    RequireFrames(id, "attack", entry.AttackFrames, 3, failures);
                    RequireFrames(id, "skill", entry.SpecialFrames, 5, failures);
                    break;
                case "mother_bulgasari":
                    RequireFrames(id, "idle", entry.IdleFrames, 3, failures);
                    RequireFrames(id, "walk", entry.WalkFrames, 4, failures);
                    RequireFrames(id, "attack", entry.AttackFrames, 2, failures);
                    break;
                case "imugi":
                    RequireFrames(id, "static head", entry.IdleFrames, 1, failures);
                    break;
                case "gaekgwi":
                    RequireFrames(id, "idle", entry.IdleFrames, 3, failures);
                    RequireFrames(id, "dash", entry.SpecialFrames, 3, failures);
                    RequireFrames(id, "attack", entry.AttackFrames, 3, failures);
                    RequireFrames(id, "dash effect", entry.DashEffectFrames, 1, failures);
                    RequireFrames(id, "impact effect", entry.ImpactEffectFrames, 1, failures);
                    break;
                case "magpie":
                    RequireFrames(id, "idle", entry.IdleFrames, 1, failures);
                    RequireFrames(id, "flight", entry.WalkFrames, 2, failures);
                    RequireFrames(id, "pickup", entry.AttackFrames, 1, failures);
                    break;
            }
        }

        private static void RequireFrames(string id, string tag, IReadOnlyList<Sprite> frames,
            int minimum, ICollection<string> failures)
        {
            var count = frames?.Count ?? 0;
            if (count < minimum)
                failures.Add($"{id}: '{tag}' 프레임 부족 ({count}/{minimum})");
        }

        private static Sprite FindDefaultSprite(string artPath)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(artPath);
            if (IsAttackBuildingFile(System.IO.Path.GetFileName(artPath)))
                return assets.OfType<Sprite>().FirstOrDefault(sprite => sprite.name == "Frame_0");
            var idleClip = assets.OfType<AnimationClip>()
                .FirstOrDefault(clip => string.Equals(clip.name, "idle", StringComparison.OrdinalIgnoreCase));
            // Final boss heads use a default tag instead of idle.
            if (idleClip == null)
                idleClip = assets.OfType<AnimationClip>()
                    .FirstOrDefault(clip => string.Equals(clip.name, "default", StringComparison.OrdinalIgnoreCase));
            if (idleClip != null)
            {
                foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(idleClip))
                {
                    var keyframes = AnimationUtility.GetObjectReferenceCurve(idleClip, binding);
                    if (keyframes != null && keyframes.Length > 0 && keyframes[0].value is Sprite sprite)
                        return sprite;
                }
            }

            return assets.OfType<Sprite>()
                .OrderBy(sprite => sprite.name, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        private static string ResolveArtPath(string defaultFolder, string fileOrPath) =>
            fileOrPath.StartsWith("Assets/", StringComparison.Ordinal)
                ? fileOrPath
                : $"{defaultFolder}/{fileOrPath}";

        private static IReadOnlyList<Sprite> FindAnimationFrames(string artPath, string tag)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(artPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(candidate =>
                    string.Equals(candidate.name, tag, StringComparison.OrdinalIgnoreCase) ||
                    candidate.name.EndsWith($"_{tag}", StringComparison.OrdinalIgnoreCase));
            if (clip == null) return Array.Empty<Sprite>();

            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                if (!string.Equals(binding.propertyName, "m_Sprite", StringComparison.Ordinal)) continue;
                var frames = AnimationUtility.GetObjectReferenceCurve(clip, binding)
                    .Select(keyframe => keyframe.value)
                    .OfType<Sprite>()
                    .ToList();
                while (frames.Count > 1 && frames[frames.Count - 1] == frames[frames.Count - 2])
                    frames.RemoveAt(frames.Count - 1);
                if (frames.Count > 0) return frames;
            }

            return Array.Empty<Sprite>();
        }

        private static IReadOnlyList<Sprite> FindNamedSpriteFrames(
            string artPath, params string[] frameNames)
        {
            if (frameNames == null || frameNames.Length == 0) return Array.Empty<Sprite>();
            var spritesByName = AssetDatabase.LoadAllAssetsAtPath(artPath)
                .OfType<Sprite>()
                .GroupBy(sprite => sprite.name, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var frames = new List<Sprite>(frameNames.Length);
            for (var index = 0; index < frameNames.Length; index++)
                if (spritesByName.TryGetValue(frameNames[index], out var sprite))
                    frames.Add(sprite);
            return frames;
        }

        private static bool IsAttackBuildingFile(string file) =>
            file == "singijeon_cart.aseprite" || file == "ice_root_battery.aseprite" ||
            file == "cold_wave_battery.aseprite";

        private static IReadOnlyList<Sprite> FindLongestAnimationFrames(string artPath)
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(artPath).OfType<AnimationClip>();
            IReadOnlyList<Sprite> longest = Array.Empty<Sprite>();
            foreach (var clip in clips)
            {
                foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                {
                    if (!string.Equals(binding.propertyName, "m_Sprite", StringComparison.Ordinal)) continue;
                    var frames = AnimationUtility.GetObjectReferenceCurve(clip, binding)
                        .Select(keyframe => keyframe.value).OfType<Sprite>().ToList();
                    while (frames.Count > 1 && frames[^1] == frames[^2]) frames.RemoveAt(frames.Count - 1);
                    if (frames.Count > longest.Count) longest = frames;
                }
            }
            if (longest.Count > 0) return longest;
            return AssetDatabase.LoadAllAssetsAtPath(artPath).OfType<Sprite>()
                .OrderBy(sprite => sprite.name, StringComparer.Ordinal).ToArray();
        }

        private static IReadOnlyList<Sprite> LoadImugiElectricAttackFrames(
            ICollection<string> failures)
        {
            var path = $"{GameplayArtFolder}/{ImugiElectricAttackFile}";
            if (!ConfigureAsepriteImporter(path, failures)) return Array.Empty<Sprite>();
            var frames = FindLongestAnimationFrames(path);
            if (frames.Count != 7)
                failures.Add($"Imugi electric attack frames must be 7, actual={frames.Count}.");
            return frames;
        }

        private static void SaveImugiElectricAttackFrames(IReadOnlyList<Sprite> frames)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameplayArtCatalog>(GameplayArtCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<GameplayArtCatalog>();
                AssetDatabase.CreateAsset(catalog, GameplayArtCatalogPath);
            }
            var serializedCatalog = new SerializedObject(catalog);
            SetSpriteArray(
                serializedCatalog.FindProperty("imugiElectricAttackFrames"), frames);
            serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static void SetSpriteArray(SerializedProperty property, IReadOnlyList<Sprite> sprites)
        {
            property.arraySize = sprites?.Count ?? 0;
            for (var i = 0; i < property.arraySize; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        }

        internal static bool ConfigureAsepriteImporter(string artPath, ICollection<string> failures)
        {
            var importer = AssetImporter.GetAtPath(artPath) as AsepriteImporter;
            if (importer == null)
            {
                // Newly copied art has no importer until its first import. Existing Aseprite
                // assets must not be force-imported because the scripted importer can otherwise
                // run twice during one integration pass and report an inconsistent result.
                AssetDatabase.ImportAsset(
                    artPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                importer = AssetImporter.GetAtPath(artPath) as AsepriteImporter;
            }

            if (importer == null)
            {
                failures.Add($"AsepriteImporter를 찾지 못했습니다. ({artPath})");
                return false;
            }

            var settingsChanged = importer.textureType != TextureImporterType.Sprite ||
                                  !Mathf.Approximately(importer.spritePixelsPerUnit, PixelsPerUnit) ||
                                  importer.filterMode != FilterMode.Point ||
                                  importer.wrapMode != TextureWrapMode.Clamp ||
                                  importer.mipmapEnabled;
            if (settingsChanged)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = PixelsPerUnit;
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            return true;
        }

        private static bool ConfigurePngImporter(string artPath, ICollection<string> failures)
        {
            AssetDatabase.ImportAsset(artPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            if (!(AssetImporter.GetAtPath(artPath) is TextureImporter importer))
            {
                failures.Add($"TextureImporter를 찾지 못했습니다. ({artPath})");
                return false;
            }
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            return true;
        }

        private static bool ConfigureCharacterPngImporter(string artPath, ICollection<string> failures)
        {
            AssetDatabase.ImportAsset(
                artPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            if (!(AssetImporter.GetAtPath(artPath) is TextureImporter importer))
            {
                failures.Add($"TextureImporter를 찾지 못했습니다. ({artPath})");
                return false;
            }

            var settingsChanged = importer.textureType != TextureImporterType.Sprite ||
                                  importer.spriteImportMode != SpriteImportMode.Single ||
                                  !Mathf.Approximately(importer.spritePixelsPerUnit, PixelsPerUnit) ||
                                  importer.filterMode != FilterMode.Point ||
                                  importer.wrapMode != TextureWrapMode.Clamp ||
                                  importer.mipmapEnabled ||
                                  !importer.alphaIsTransparency;
            if (settingsChanged)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = PixelsPerUnit;
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            return true;
        }
    }
}
