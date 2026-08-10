using System;
using System.Collections.Generic;
using System.Linq;
using Nyangbingo.Data;
using Nyangbingo.World;
using UnityEditor;
using UnityEditor.U2D.Aseprite;
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
                ["bedrock"] = "bedrock.aseprite",
                ["bg_dirt"] = "t_bg_dirt.aseprite",
                ["bg_stone"] = "t_bg_stone.aseprite",
                ["bg_deep"] = "t_bg_deep.aseprite",
                ["coal"] = "coal.aseprite",
                ["copper_ore"] = "copper_ore.aseprite",
                ["clay"] = "clay.aseprite",
                ["dirt"] = "dirt.aseprite",
                ["frost_essence"] = "frost_essence.aseprite",
                ["ice_lake"] = "ice_lake.aseprite",
                ["ice_shard"] = "ice_shard.aseprite",
                ["icesteel_ore"] = "icesteel_ore.aseprite",
                ["iron_ore"] = "iron_ore.aseprite",
                ["ruin_wall"] = "ruin_wall.aseprite",
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
                ["gangcheol_body"] = "gangcheol_body.png",
                // The delivered Gangcheol filenames are reversed by physical role:
                // post_tail is the larger proximal piece and pre_tail is the smaller tip.
                ["gangcheol_pre_tail"] = "gangcheol_post_tail.aseprite",
                ["gangcheol_post_tail"] = "gangcheol_pre_tail.aseprite",
                ["king_dokkaebi"] = "king_dokkaebi.aseprite",
                ["mother_bulgasari"] = "mother_bulgasari.aseprite",
                ["gangcheol_boss"] = "gangcheol.aseprite",
                ["imugi"] = "imugi_head2.aseprite",
                ["imugi_body"] = "imugi_body.aseprite",
                ["imugi_pre_tail"] = "imugi_pre_tail.aseprite",
                ["imugi_post_tail"] = "imugi_post_tail.aseprite",
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
                // v29: wallpaper has no dedicated icon art. Use the delivered upper-layer background tile.
                ["wallpaper"] = "Assets/Art/Tiles/t_bg_dirt.aseprite"
            };

        private static readonly IReadOnlyDictionary<string, string> BuildingArtFiles =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["workbench"] = "workbench.aseprite",
                ["furnace"] = "furnace.aseprite",
                ["blast_furnace"] = "blast_furnace.aseprite",
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
                ["tree_1"] = "tree_0.aseprite",
                ["tree_2"] = "tree_1.aseprite",
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

                if (!ConfigureAsepriteImporter(artPath, failures))
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
                var idleTag = string.Equals(pair.Key, "imugi", StringComparison.Ordinal)
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
                var isMagpie = string.Equals(pair.Key, "magpie", StringComparison.Ordinal);
                SetSpriteArray(entry.FindPropertyRelative("idleFrames"),
                    isMagpie && idleFrames.Count > 0
                        ? new[] { idleFrames[0] }
                        : idleFrames);
                SetSpriteArray(entry.FindPropertyRelative("walkFrames"),
                    isMagpie
                        ? idleFrames.Skip(1).Take(2).ToArray()
                        : FindAnimationFrames(artPath, "walk"));
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
            NyangbingoMainGameSceneCreator.CreateOrUpdate(catalog);
            Debug.Log("[Nyangbingo] Environment art integration completed: sky 2/2, underground 1/1, " +
                      "title background 1/1, title 10/10. MainGame scene updated.");
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
            NyangbingoMainGameSceneCreator.CreateOrUpdate();
            Debug.Log($"[Nyangbingo] Combat/temperature art integration completed: " +
                      $"temperature {temperatureFrames.Count}, attack {attackFrames.Count}, " +
                      $"mining {miningFrames.Count}, warning {warningFrames.Count}, " +
                      $"Gangcheori fire {gangcheoriFireFrames.Count}, " +
                      $"player fire hit {playerFireHitFrames.Count}, " +
                      $"Imugi electric {imugiElectricFrames.Count}, " +
                      $"projectile {projectileFrames.Count}. MainGame scene updated.");
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
                var frames = FindLongestAnimationFrames(path);
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
            NyangbingoMainGameSceneCreator.CreateOrUpdate();
            Debug.Log($"[Nyangbingo] World decoration art integration completed: " +
                      $"{DecorationArtFiles.Count} decorations. MainGame scene updated.");
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
            foreach (var pair in BuildingArtFiles)
            {
                var sprite = catalog.Find(pair.Key)?.Sprite;
                if (sprite == null ||
                    !string.Equals(AssetDatabase.GetAssetPath(sprite),
                        ResolveArtPath(BuildingArtFolder, pair.Value), StringComparison.Ordinal))
                    return false;
            }
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
                    RequireFrames(id, "idle", entry.IdleFrames, 2, failures);
                    RequireFrames(id, "walk", entry.WalkFrames, 4, failures);
                    RequireFrames(id, "attack", entry.AttackFrames, 2, failures);
                    RequireFrames(id, "hit", entry.HitFrames, 1, failures);
                    RequireFrames(id, "die", entry.DeathFrames, 2, failures);
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
                    RequireFrames(id, "default", entry.IdleFrames, 2, failures);
                    RequireFrames(id, "marble", entry.SpecialFrames, 2, failures);
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
            var idleClip = assets.OfType<AnimationClip>()
                .FirstOrDefault(clip => string.Equals(clip.name, "idle", StringComparison.OrdinalIgnoreCase));
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
