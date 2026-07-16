using System.Collections.Generic;
using System.IO;
using Nyangbingo.Debugging;
using Nyangbingo.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;
// UnityEngine.Tilemaps.TilemapRenderer(유니티 내장 렌더링 컴포넌트)와
// Nyangbingo.World.TilemapRenderer(우리 데이터 바인딩 스크립트)가 이름이 같아 별칭으로 구분한다.
using UnityTilemapRenderer = UnityEngine.Tilemaps.TilemapRenderer;
using WorldTilemapRenderer = Nyangbingo.World.TilemapRenderer;

public static class NyangbingoDevAWorldTestSceneCreator
{
    private const string ConfigAssetPath = "Assets/Data/SO/WorldGenerationConfig.asset";
    private const string ScenePath = "Assets/Scenes/DevAWorldTest.unity";
    private const string TempSpriteFolder = "Assets/Sprites/Temp";
    private const string TempTileFolder = "Assets/Tiles/Temp";

    /// <summary>임시 타일 텍스처 한 변 픽셀 수. spritePixelsPerUnit도 이 값으로 맞춰서 1타일 = 1월드 유닛이 되게 한다.</summary>
    private const int TempTileTextureSize = 32;

    /// <summary>
    /// 18종 elementType(전경 15 + 배경벽 3) 임시 색상 팔레트. 전경은 불투명 단색, 배경벽은
    /// 어둡고 반투명(alpha 0.4)해서 채굴 시 "전경이 사라지고 배경이 드러나는" 변화가 눈에 확실히 보이게 한다.
    /// 실제 아트가 준비되면 Assets/Tiles/Temp의 각 Tile 에셋 sprite만 교체하면 된다.
    /// </summary>
    private static readonly (string elementType, Color color)[] TempTilePalette =
    {
        (WorldTileTypes.Dirt, new Color(0.55f, 0.35f, 0.17f, 1f)),
        (WorldTileTypes.Stone, new Color(0.55f, 0.55f, 0.58f, 1f)),
        (WorldTileTypes.Coal, new Color(0.12f, 0.12f, 0.13f, 1f)),
        (WorldTileTypes.Clay, new Color(0.72f, 0.45f, 0.28f, 1f)),
        (WorldTileTypes.StoneMid, new Color(0.40f, 0.42f, 0.48f, 1f)),
        (WorldTileTypes.IronOre, new Color(0.70f, 0.42f, 0.30f, 1f)),
        (WorldTileTypes.CopperOre, new Color(0.80f, 0.45f, 0.20f, 1f)),
        (WorldTileTypes.IceShard, new Color(0.65f, 0.90f, 0.95f, 1f)),
        (WorldTileTypes.StoneDeep, new Color(0.25f, 0.27f, 0.32f, 1f)),
        (WorldTileTypes.IceSteelOre, new Color(0.55f, 0.65f, 0.75f, 1f)),
        (WorldTileTypes.FrostEssence, new Color(0.70f, 0.92f, 1.00f, 1f)),
        (WorldTileTypes.Bedrock, new Color(0.10f, 0.08f, 0.12f, 1f)),
        (WorldTileTypes.RuinWall, new Color(0.45f, 0.50f, 0.40f, 1f)),
        (WorldTileTypes.IceLake, new Color(0.20f, 0.45f, 0.75f, 1f)),
        (WorldTileTypes.IceAltar, new Color(0.95f, 0.90f, 0.75f, 1f)),
        (WorldTileTypes.BackgroundDirt, new Color(0.30f, 0.18f, 0.08f, 0.4f)),
        (WorldTileTypes.BackgroundStone, new Color(0.25f, 0.25f, 0.27f, 0.4f)),
        (WorldTileTypes.BackgroundDeep, new Color(0.08f, 0.08f, 0.12f, 0.4f)),
    };

    /// <summary>
    /// WorldGenerationConfig.asset의 Script 참조가 깨졌을 때(중복 클래스 컴파일 에러 등으로
    /// m_Script가 fileID 0으로 직렬화된 경우) 쓰는 복구 유틸리티.
    /// 기존 에셋을 삭제하고 v17 정본 기본값으로 새로 만든 뒤, 테스트 씬의
    /// MapGeneratorTestHarness.config 필드를 새 에셋으로 다시 연결한다.
    /// 에디터 메뉴로도, `-executeMethod`로도 호출 가능.
    /// </summary>
    [MenuItem("Nyangbingo/Repair WorldGenerationConfig Asset")]
    public static void RepairWorldGenerationConfigAsset()
    {
        EnsureFolder("Assets/Data/SO");

        if (AssetDatabase.LoadAssetAtPath<Object>(ConfigAssetPath) != null)
        {
            AssetDatabase.DeleteAsset(ConfigAssetPath);
            Debug.Log($"[Nyangbingo] 손상된 기존 에셋을 삭제했습니다: {ConfigAssetPath}");
        }

        AssetDatabase.Refresh();

        var config = ScriptableObject.CreateInstance<WorldGenerationConfig>();
        config.name = "WorldGenerationConfig";
        AssetDatabase.CreateAsset(config, ConfigAssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(ConfigAssetPath, ImportAssetOptions.ForceUpdate);

        var reloaded = AssetDatabase.LoadAssetAtPath<WorldGenerationConfig>(ConfigAssetPath);
        if (reloaded == null)
        {
            Debug.LogError("[Nyangbingo] WorldGenerationConfig.asset 재생성에 실패했습니다. MonoScript 바인딩을 확인하세요.");
            return;
        }
        Debug.Log($"[Nyangbingo] WorldGenerationConfig.asset을 v17 정본 기본값으로 새로 생성했습니다: {ConfigAssetPath} (mapWidth={reloaded.MapWidth}, mapHeight={reloaded.MapHeight}, totalChestCount={reloaded.TotalChestCount})");

        Scene scene;
        if (File.Exists(ScenePath))
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        else
        {
            EnsureFolder("Assets/Scenes");
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.backgroundColor = new Color(0.05f, 0.05f, 0.08f);
            cameraObject.AddComponent<AudioListener>();
        }

        var harness = Object.FindAnyObjectByType<MapGeneratorTestHarness>();
        if (harness == null)
        {
            var harnessObject = new GameObject("MapGeneratorTestHarness");
            harness = harnessObject.AddComponent<MapGeneratorTestHarness>();
        }

        var serializedHarness = new SerializedObject(harness);
        serializedHarness.FindProperty("config").objectReferenceValue = reloaded;
        if (serializedHarness.FindProperty("seed").intValue == 0)
        {
            serializedHarness.FindProperty("seed").intValue = 12345;
        }
        serializedHarness.FindProperty("logLegend").boolValue = true;
        serializedHarness.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(harness);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();

        Debug.Log($"[Nyangbingo] '{harness.gameObject.name}'의 config 필드를 새 WorldGenerationConfig.asset에 연결하고 씬을 저장했습니다: {ScenePath}. 이제 Play를 누르면 맵이 그려집니다.");
    }

    /// <summary>
    /// DevAWorldTest 씬에 "WorldTilemap"(Grid) → Foreground/Background Tilemap 2겹을 자동으로 만들고,
    /// Nyangbingo.World.TilemapRenderer를 붙여 알려진 elementType 슬롯을 전부 채운 뒤
    /// MapGeneratorTestHarness.tilemapRenderer에 연결한다. TileBase 에셋만 드래그해서 채우면 바로 Play 가능.
    /// </summary>
    [MenuItem("Nyangbingo/Setup Tilemap Rendering In Dev A Scene")]
    public static void SetupTilemapRendering()
    {
        Scene scene = File.Exists(ScenePath)
            ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
            : EditorSceneManager.GetSceneByPath(ScenePath);

        if (!scene.IsValid())
        {
            Create();
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        var worldTilemapObject = GameObject.Find("WorldTilemap");
        if (worldTilemapObject == null)
        {
            worldTilemapObject = new GameObject("WorldTilemap");
            worldTilemapObject.AddComponent<Grid>();
        }

        var foregroundTilemap = FindOrCreateTilemapLayer(worldTilemapObject.transform, "Foreground", 0);
        var backgroundTilemap = FindOrCreateTilemapLayer(worldTilemapObject.transform, "Background", -1);

        var worldRenderer = worldTilemapObject.GetComponent<WorldTilemapRenderer>();
        if (worldRenderer == null)
        {
            worldRenderer = worldTilemapObject.AddComponent<WorldTilemapRenderer>();
        }

        var serializedRenderer = new SerializedObject(worldRenderer);
        serializedRenderer.FindProperty("foregroundTilemap").objectReferenceValue = foregroundTilemap;
        serializedRenderer.FindProperty("backgroundTilemap").objectReferenceValue = backgroundTilemap;

        var tileVisualsProperty = serializedRenderer.FindProperty("tileVisuals");
        var existing = new List<WorldTilemapRenderer.TileVisual>();
        for (var i = 0; i < tileVisualsProperty.arraySize; i++)
        {
            var element = tileVisualsProperty.GetArrayElementAtIndex(i);
            existing.Add(new WorldTilemapRenderer.TileVisual
            {
                elementType = element.FindPropertyRelative("elementType").stringValue,
                tile = element.FindPropertyRelative("tile").objectReferenceValue as TileBase
            });
        }

        var addedCount = WorldTilemapRenderer.MergeKnownElementTypes(existing);

        var tempTiles = EnsureTempTileAssets();
        var autoAssignedCount = 0;
        for (var i = 0; i < existing.Count; i++)
        {
            var visual = existing[i];
            if (visual.tile != null) continue; // 이미 실제 아트(혹은 이전 임시 타일)가 연결된 슬롯은 건드리지 않는다.
            if (!tempTiles.TryGetValue(visual.elementType, out var tempTile)) continue;

            visual.tile = tempTile;
            existing[i] = visual;
            autoAssignedCount++;
        }

        tileVisualsProperty.arraySize = existing.Count;
        for (var i = 0; i < existing.Count; i++)
        {
            var element = tileVisualsProperty.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("elementType").stringValue = existing[i].elementType;
            element.FindPropertyRelative("tile").objectReferenceValue = existing[i].tile;
        }

        serializedRenderer.ApplyModifiedPropertiesWithoutUndo();

        var harness = Object.FindAnyObjectByType<MapGeneratorTestHarness>();
        if (harness == null)
        {
            var harnessObject = new GameObject("MapGeneratorTestHarness");
            harness = harnessObject.AddComponent<MapGeneratorTestHarness>();
            var config = AssetDatabase.LoadAssetAtPath<WorldGenerationConfig>(ConfigAssetPath);
            var serializedNewHarness = new SerializedObject(harness);
            serializedNewHarness.FindProperty("config").objectReferenceValue = config;
            serializedNewHarness.FindProperty("seed").intValue = 12345;
            serializedNewHarness.FindProperty("logLegend").boolValue = true;
            serializedNewHarness.ApplyModifiedPropertiesWithoutUndo();
        }

        var serializedHarness = new SerializedObject(harness);
        serializedHarness.FindProperty("tilemapRenderer").objectReferenceValue = worldRenderer;
        serializedHarness.ApplyModifiedPropertiesWithoutUndo();

        var miningController = Object.FindAnyObjectByType<PlayerMiningController>();
        if (miningController == null)
        {
            var cameraObject = GameObject.FindWithTag("MainCamera");
            var hostObject = cameraObject != null ? cameraObject : harness.gameObject;
            miningController = hostObject.AddComponent<PlayerMiningController>();
        }

        var serializedMining = new SerializedObject(miningController);
        serializedMining.FindProperty("harness").objectReferenceValue = harness;
        serializedMining.ApplyModifiedPropertiesWithoutUndo();

        var sealDebugView = Object.FindAnyObjectByType<SealSystemDebugView>();
        if (sealDebugView == null)
        {
            sealDebugView = miningController.gameObject.AddComponent<SealSystemDebugView>();
        }

        var serializedSealDebug = new SerializedObject(sealDebugView);
        serializedSealDebug.FindProperty("harness").objectReferenceValue = harness;
        serializedSealDebug.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(worldRenderer);
        EditorUtility.SetDirty(harness);
        EditorUtility.SetDirty(miningController);
        EditorUtility.SetDirty(sealDebugView);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        Debug.Log($"[Nyangbingo] Tilemap 렌더링 씬 구성 완료: 'WorldTilemap'(Grid) 아래 Foreground/Background Tilemap을 만들고 " +
                  $"elementType 슬롯 {existing.Count}개(신규 {addedCount}개)에 임시 타일 {autoAssignedCount}개를 자동 매핑한 뒤 " +
                  $"MapGeneratorTestHarness, PlayerMiningController, SealSystemDebugView('{sealDebugView.gameObject.name}')를 연결했습니다. " +
                  $"임시 스프라이트/타일은 {TempSpriteFolder}, {TempTileFolder}에 생성됩니다 — 실제 아트가 준비되면 " +
                  "각 Tile 에셋의 sprite만 교체하세요. 지금 바로 Play 후 좌클릭 채굴/우클릭 설치, 마우스 위치의 밀폐 판정 기즈모를 확인할 수 있습니다.");
    }

    /// <summary>
    /// 18종 elementType(전경 15 + 배경벽 3)에 대응하는 임시 단색 Sprite + Tile 에셋을 생성한다.
    /// 이미 Assets/Tiles/Temp에 같은 이름의 Tile 에셋이 있으면 건드리지 않고 그대로 재사용한다(멱등성 보장 —
    /// 사용자가 이미 실제 아트로 교체했거나 이전에 생성된 임시 타일을 이어서 쓰는 경우를 보호).
    /// </summary>
    private static Dictionary<string, TileBase> EnsureTempTileAssets()
    {
        EnsureFolder("Assets/Sprites");
        EnsureFolder(TempSpriteFolder);
        EnsureFolder("Assets/Tiles");
        EnsureFolder(TempTileFolder);

        var result = new Dictionary<string, TileBase>(TempTilePalette.Length);
        var createdCount = 0;

        foreach (var (elementType, color) in TempTilePalette)
        {
            var tilePath = $"{TempTileFolder}/Tile_{elementType}.asset";
            var existingTile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
            if (existingTile != null)
            {
                result[elementType] = existingTile;
                continue;
            }

            var sprite = CreateTempSolidColorSprite(elementType, color);
            if (sprite == null) continue;

            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.color = Color.white;
            tile.colliderType = Tile.ColliderType.None;
            AssetDatabase.CreateAsset(tile, tilePath);

            result[elementType] = tile;
            createdCount++;
        }

        if (createdCount > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.Log($"[Nyangbingo] 임시 타일 에셋 {createdCount}개를 새로 생성했습니다 ({TempTileFolder}, 스프라이트는 {TempSpriteFolder}). " +
                      "전경은 불투명 단색, 배경벽(bg_*)은 어둡고 반투명(alpha 0.4)해서 채굴 시 변화가 눈에 보입니다.");
        }

        return result;
    }

    /// <summary>단색 32x32 PNG를 만들어 Assets에 기록하고, Sprite로 임포트 설정을 맞춘 뒤 로드해서 반환한다.</summary>
    private static Sprite CreateTempSolidColorSprite(string elementType, Color color)
    {
        var spritePath = $"{TempSpriteFolder}/temp_{elementType}.png";

        var pixels = new Color32[TempTileTextureSize * TempTileTextureSize];
        Color32 color32 = color;
        for (var i = 0; i < pixels.Length; i++) pixels[i] = color32;

        var texture = new Texture2D(TempTileTextureSize, TempTileTextureSize, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point
        };
        texture.SetPixels32(pixels);
        texture.Apply();

        var pngBytes = texture.EncodeToPNG();
        Object.DestroyImmediate(texture);

        var projectRoot = Path.GetDirectoryName(Application.dataPath);
        if (projectRoot == null)
        {
            Debug.LogError("[Nyangbingo] 프로젝트 루트 경로를 찾을 수 없어 임시 스프라이트를 생성하지 못했습니다.");
            return null;
        }

        var absolutePath = Path.Combine(projectRoot, spritePath);
        File.WriteAllBytes(absolutePath, pngBytes);

        AssetDatabase.ImportAsset(spritePath, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(spritePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spritePixelsPerUnit = TempTileTextureSize;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
    }

    private static Tilemap FindOrCreateTilemapLayer(Transform parent, string name, int sortingOrder)
    {
        var existingChild = parent.Find(name);
        GameObject layerObject = existingChild != null ? existingChild.gameObject : new GameObject(name);
        if (existingChild == null) layerObject.transform.SetParent(parent, false);

        var tilemap = layerObject.GetComponent<Tilemap>();
        if (tilemap == null) tilemap = layerObject.AddComponent<Tilemap>();

        var renderer = layerObject.GetComponent<UnityTilemapRenderer>();
        if (renderer == null) renderer = layerObject.AddComponent<UnityTilemapRenderer>();
        renderer.sortingOrder = sortingOrder;

        return tilemap;
    }

    [MenuItem("Nyangbingo/Create Dev A World Test Scene")]
    private static void Create()
    {
        EnsureFolder("Assets/Data/SO");
        var config = AssetDatabase.LoadAssetAtPath<WorldGenerationConfig>(ConfigAssetPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<WorldGenerationConfig>();
            AssetDatabase.CreateAsset(config, ConfigAssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Nyangbingo] WorldGenerationConfig 기본 에셋을 생성했습니다: {ConfigAssetPath}");
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.backgroundColor = new Color(0.05f, 0.05f, 0.08f);
        cameraObject.AddComponent<AudioListener>();

        var harnessObject = new GameObject("MapGeneratorTestHarness");
        var harness = harnessObject.AddComponent<MapGeneratorTestHarness>();
        var serializedHarness = new SerializedObject(harness);
        serializedHarness.FindProperty("config").objectReferenceValue = config;
        serializedHarness.FindProperty("seed").intValue = 12345;
        serializedHarness.FindProperty("logLegend").boolValue = true;
        serializedHarness.ApplyModifiedPropertiesWithoutUndo();

        EnsureFolder("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorGUIUtility.PingObject(harnessObject);
        Debug.Log("[Nyangbingo] Dev A 월드 테스트 씬을 생성했습니다. Play를 누르면 맵 미리보기가 그려집니다.");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        var name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(parent)) return;
        AssetDatabase.CreateFolder(parent, name);
    }
}
