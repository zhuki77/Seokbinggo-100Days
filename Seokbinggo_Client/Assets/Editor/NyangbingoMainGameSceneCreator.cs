using System.Collections.Generic;
using System.Linq;
using Nyangbingo.Data;
using Nyangbingo.Audio;
using Nyangbingo.Bosses;
using Nyangbingo.Combat;
using Nyangbingo.Inventory;
using Nyangbingo.Save;
using Nyangbingo.UI;
using Nyangbingo.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using UnityTilemapRenderer = UnityEngine.Tilemaps.TilemapRenderer;
using WorldTilemapRenderer = Nyangbingo.World.TilemapRenderer;

/// <summary>제품용 메인 플레이 씬을 재현 가능하게 생성하고 B-08 필수 배선을 검증한다.</summary>
public static class NyangbingoMainGameSceneCreator
{
    private const string ScenePath = NyangbingoSceneBuildSettings.MainGameScenePath;
    private const string ConfigPath = "Assets/Data/SO/WorldGenerationConfig.asset";
    private const string CatalogPath = "Assets/Data/SO/GameDataCatalog.asset";
    private const string CharacterArtCatalogPath = "Assets/Art/Characters/CharacterArtCatalog.asset";
    private const string ItemArtCatalogPath = "Assets/Art/Items/ItemArtCatalog.asset";
    private const string EnvironmentArtCatalogPath = "Assets/Art/Backgrounds/EnvironmentArtCatalog.asset";
    private const string GameplayArtCatalogPath = "Assets/Art/Gameplay/GameplayArtCatalog.asset";
    private const string BuildingArtCatalogPath = "Assets/Art/Buildings/BuildingArtCatalog.asset";
    private const string DecorationArtCatalogPath = "Assets/Art/Decorations/WorldDecorationArtCatalog.asset";
    private const string TempTileFolder = "Assets/Tiles/Temp";

    [MenuItem("Nyangbingo/Main Game/Create or Update Main Game Scene")]
    public static void CreateOrUpdate() => CreateOrUpdate(null);

    public static void CreateOrUpdate(EnvironmentArtCatalog environmentArtOverride)
    {
        AssetDatabase.ImportAsset(EnvironmentArtCatalogPath,
            ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        var config = AssetDatabase.LoadAssetAtPath<WorldGenerationConfig>(ConfigPath);
        var catalog = AssetDatabase.LoadAssetAtPath<GameDataCatalog>(CatalogPath);
        var characterArtCatalog = AssetDatabase.LoadAssetAtPath<CharacterArtCatalog>(CharacterArtCatalogPath);
        var itemArtCatalog = AssetDatabase.LoadAssetAtPath<ItemArtCatalog>(ItemArtCatalogPath);
        var gameplayArtCatalog = AssetDatabase.LoadAssetAtPath<GameplayArtCatalog>(GameplayArtCatalogPath);
        var buildingArtCatalog = AssetDatabase.LoadAssetAtPath<BuildingArtCatalog>(BuildingArtCatalogPath);
        var decorationArtCatalog = AssetDatabase.LoadAssetAtPath<WorldDecorationArtCatalog>(DecorationArtCatalogPath);
        var environmentArtCatalog = environmentArtOverride != null
            ? environmentArtOverride
            : AssetDatabase.LoadAssetAtPath<EnvironmentArtCatalog>(EnvironmentArtCatalogPath);
        if (config == null || catalog == null || environmentArtCatalog == null)
        {
            Debug.LogError("[Nyangbingo] MainGame 씬 생성 실패: WorldGenerationConfig 또는 " +
                           "GameDataCatalog, EnvironmentArtCatalog 에셋을 찾을 수 없습니다. " +
                           "기존 MainGame 씬은 덮어쓰지 않습니다.");
            return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateCamera(config, environmentArtCatalog);
        var worldRenderer = CreateWorldTilemaps();
        CreateBootstrap(config, catalog, characterArtCatalog, itemArtCatalog, gameplayArtCatalog, buildingArtCatalog,
            decorationArtCatalog,
            environmentArtCatalog, worldRenderer);

        EditorSceneManager.SaveScene(scene, ScenePath);
        NyangbingoSceneBuildSettings.SyncBuildSettings();
        AssetDatabase.SaveAssets();

        var reopenedScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EnsureParallaxBackground(config, environmentArtCatalog);
        EditorSceneManager.SaveScene(reopenedScene, ScenePath);
        EditorApplication.delayCall += Validate;
        Selection.activeGameObject = GameObject.Find("GameBootstrap");
    }

    [MenuItem("Nyangbingo/Main Game/Validate Main Game Scene")]
    public static void Validate()
    {
        var scene = SceneManager.GetActiveScene().path == ScenePath
            ? SceneManager.GetActiveScene()
            : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var bootstrap = Object.FindAnyObjectByType<MainGameBootstrap>();
        var worldRenderer = Object.FindAnyObjectByType<WorldTilemapRenderer>();
        var dayNight = Object.FindAnyObjectByType<DayNightService>();
        var tickDriver = Object.FindAnyObjectByType<CentralTickDriver>();
        var runtimeServices = Object.FindAnyObjectByType<MainGameRuntimeServices>();
        var inventoryRuntimes = Object.FindObjectsByType<InventoryRuntime>();
        var environmentState = Object.FindAnyObjectByType<MainGameEnvironmentState>();
        var saveCoordinator = Object.FindAnyObjectByType<MainGameSaveCoordinator>();
        var shellUi = Object.FindAnyObjectByType<MainGameShellUiController>();
        var bossSummonUi = Object.FindAnyObjectByType<MainGameBossSummonUiController>();
        var encounterCoordinator = Object.FindAnyObjectByType<MainGameEncounterCoordinator>();
        var turretRuntime = Object.FindAnyObjectByType<MainGameTurretRuntime>();
        var turretBuildUi = Object.FindAnyObjectByType<MainGameTurretBuildUiController>();
        var raidTarget = Object.FindAnyObjectByType<MainGameRaidTarget>();
        var playerController = Object.FindAnyObjectByType<MainGamePlayerController>();
        var bossManager = Object.FindAnyObjectByType<BossManager>();
        var bossRewardReceiver = Object.FindAnyObjectByType<BossRewardReceiver>();
        var hud = Object.FindAnyObjectByType<MainGameHudController>();
        var codex = Object.FindAnyObjectByType<MainGameCodexController>();
        var eventSystem = Object.FindAnyObjectByType<EventSystem>();
        var camera = Camera.main;
        var parallaxBackground = camera != null ? camera.GetComponent<MainGameParallaxBackground>() : null;
        var buildScene = EditorBuildSettings.scenes.FirstOrDefault(entry => entry.path == ScenePath);
        var knownVisuals = new List<WorldTilemapRenderer.TileVisual>();
        WorldTilemapRenderer.MergeKnownElementTypes(knownVisuals);
        // Edit Mode에서는 Awake가 실행되지 않아 런타임 전용 룩업 캐시가 빈 상태일 수 있다.
        // 씬에 직렬화된 TileVisual 참조를 기준으로 캐시를 먼저 복구한 뒤 실제 누락만 검증한다.
        worldRenderer?.RebuildLookupTable();
        var missingTileIds = worldRenderer == null
            ? knownVisuals.Select(visual => visual.elementType).ToArray()
            : knownVisuals
                .Where(visual => !worldRenderer.TryGetTileBase(visual.elementType, out var tile) || tile == null)
                .Select(visual => visual.elementType)
                .ToArray();

        var missingBindings = new List<string>();
        void Require(bool condition, string name) { if (!condition) missingBindings.Add(name); }
        Require(scene.IsValid(), "scene");
        Require(bootstrap != null, "MainGameBootstrap");
        Require(worldRenderer != null, "TilemapRenderer");
        Require(worldRenderer?.Foreground != null, "TilemapRenderer.Foreground");
        Require(worldRenderer?.Background != null, "TilemapRenderer.Background");
        Require(worldRenderer != null && worldRenderer.HasIceAltarQuadrantArt,
            "TilemapRenderer ice altar 2x2 art");
        Require(dayNight != null, "DayNightService");
        Require(tickDriver != null, "CentralTickDriver");
        Require(runtimeServices != null, "MainGameRuntimeServices");
        Require(inventoryRuntimes.Length == 1, "InventoryRuntime count=1");
        Require(runtimeServices != null && inventoryRuntimes.Length == 1 &&
                runtimeServices.InventoryRuntime == inventoryRuntimes[0], "RuntimeServices.InventoryRuntime");
        Require(environmentState != null, "MainGameEnvironmentState");
        Require(saveCoordinator != null, "MainGameSaveCoordinator");
        Require(shellUi != null, "MainGameShellUiController");
        Require(shellUi != null && shellUi.BoundSaveSlotCount == SaveManager.SlotCount, "Shell save slots");
        Require(bossSummonUi != null, "MainGameBossSummonUiController");
        Require(bossSummonUi != null && bossSummonUi.HasSceneBindings, "Boss summon UI bindings");
        Require(encounterCoordinator != null, "MainGameEncounterCoordinator");
        Require(turretRuntime != null && turretRuntime.HasSceneBindings, "MainGameTurretRuntime bindings");
        Require(turretBuildUi != null && turretBuildUi.HasSceneBindings, "Turret build UI bindings");
        Require(raidTarget != null, "MainGameRaidTarget");
        Require(playerController != null, "MainGamePlayerController");
        Require(bossManager != null, "BossManager");
        Require(bossRewardReceiver != null, "BossRewardReceiver");
        Require(hud != null, "MainGameHudController");
        Require(codex != null, "MainGameCodexController");
        Require(eventSystem != null, "EventSystem");
        Require(playerController != null && playerController.GetComponent<Rigidbody2D>() != null,
            "Player Rigidbody2D");
        Require(playerController != null && playerController.GetComponent<CircleCollider2D>() != null,
            "Player CircleCollider2D");
        Require(playerController != null && playerController.GetComponent<MeleeArcAttack>() != null,
            "Player MeleeArcAttack");
        Require(hud != null && hud.HasPlayerStatusBindings, "HUD player status bindings");
        Require(hud != null && hud.HasCraftingProgressBindings, "HUD crafting progress bindings");
        Require(codex != null && codex.BoundCardCount == YokaiCodexPresentationModel.ExpectedCardCount,
            "Codex cards");
        Require(hud != null && hud.BoundSlotCount == MainGameHudController.LegacyInventoryBarSlotCount,
            "HUD legacy inventory-bar slots");
        Require(hud != null && hud.BoundIconCount == MainGameHudController.LegacyInventoryBarSlotCount,
            "HUD legacy inventory-bar icons");
        Require(camera != null, "Main Camera");
        Require(parallaxBackground != null, "MainGameParallaxBackground");
        Require(parallaxBackground != null && parallaxBackground.HasConfiguredArt,
            "MainGameParallaxBackground art layers");
        Require(buildScene != null && buildScene.enabled, "Build Settings");
        var valid = missingBindings.Count == 0 && missingTileIds.Length == 0;

        if (valid)
        {
            Debug.Log("[Nyangbingo] MainGame B-08 검증 완료: 제품 부트스트랩, 월드 Tilemap, " +
                      "DayNightService, CentralTickDriver, Main Camera, Build Settings 배선 정상.");
        }
        else
        {
            Debug.LogError("[Nyangbingo] MainGame B-08 검증 실패: 필수 컴포넌트 또는 Build Settings " +
                           $"배선이 누락됐습니다. 누락 Binding=[{string.Join(", ", missingBindings)}], " +
                           $"누락 TileBase=[{string.Join(", ", missingTileIds)}]. " +
                           "Create or Update Main Game Scene을 다시 실행하세요.");
        }
    }

    private static void CreateCamera(WorldGenerationConfig config, EnvironmentArtCatalog environmentArtCatalog)
    {
        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(
            config.MapWidth * 0.5f,
            config.MapHeight * 0.82f,
            -10f);

        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = MainGamePlayerController.GameplayCameraOrthographicSize;
        camera.backgroundColor = new Color(0.035f, 0.045f, 0.07f, 1f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        cameraObject.AddComponent<AudioListener>();
        ConfigureParallaxBackground(cameraObject, config, environmentArtCatalog);
    }

    private static void EnsureParallaxBackground(WorldGenerationConfig config,
        EnvironmentArtCatalog environmentArtCatalog)
    {
        var camera = Camera.main;
        if (camera == null) return;
        ConfigureParallaxBackground(camera.gameObject, config, environmentArtCatalog);
    }

    private static void ConfigureParallaxBackground(GameObject cameraObject, WorldGenerationConfig config,
        EnvironmentArtCatalog environmentArtCatalog)
    {
        var background = cameraObject.GetComponent<MainGameParallaxBackground>() ??
                         cameraObject.AddComponent<MainGameParallaxBackground>();
        var undergroundThreshold = config.MapHeight * config.SurfaceBaseHeightRatio - 8f;
        background.ConfigureForScene(environmentArtCatalog, undergroundThreshold);
        EditorUtility.SetDirty(background);
    }

    private static WorldTilemapRenderer CreateWorldTilemaps()
    {
        var root = new GameObject("WorldTilemap");
        root.AddComponent<Grid>();

        var background = CreateTilemapLayer(root.transform, "Background", -1);
        var foreground = CreateTilemapLayer(root.transform, "Foreground", 0);
        var worldRenderer = root.AddComponent<WorldTilemapRenderer>();

        var visuals = new List<WorldTilemapRenderer.TileVisual>();
        WorldTilemapRenderer.MergeKnownElementTypes(visuals);
        for (var index = 0; index < visuals.Count; index++)
        {
            var visual = visuals[index];
            visual.tile = AssetDatabase.LoadAssetAtPath<TileBase>(
                $"{TempTileFolder}/{visual.elementType}.asset");
            visuals[index] = visual;
        }

        var fallback = AssetDatabase.LoadAssetAtPath<TileBase>($"{TempTileFolder}/_unknown_fallback.asset");
        worldRenderer.SetTileVisualsForEditorSetup(visuals.ToArray(), fallback);
        var altarQuadrants = Enumerable.Range(0, 4)
            .Select(index => AssetDatabase.LoadAssetAtPath<TileBase>(
                $"{TempTileFolder}/ice_altar_{index}.asset"))
            .ToArray();
        worldRenderer.SetIceAltarQuadrantTilesForEditorSetup(altarQuadrants);

        var serializedRenderer = new SerializedObject(worldRenderer);
        serializedRenderer.FindProperty("foregroundTilemap").objectReferenceValue = foreground;
        serializedRenderer.FindProperty("backgroundTilemap").objectReferenceValue = background;
        serializedRenderer.ApplyModifiedPropertiesWithoutUndo();
        worldRenderer.RebuildLookupTable();
        EditorUtility.SetDirty(worldRenderer);
        return worldRenderer;
    }

    private static void CreateBootstrap(
        WorldGenerationConfig config,
        GameDataCatalog catalog,
        CharacterArtCatalog characterArtCatalog,
        ItemArtCatalog itemArtCatalog,
        GameplayArtCatalog gameplayArtCatalog,
        BuildingArtCatalog buildingArtCatalog,
        WorldDecorationArtCatalog decorationArtCatalog,
        EnvironmentArtCatalog environmentArtCatalog,
        WorldTilemapRenderer worldRenderer)
    {
        var root = new GameObject("GameBootstrap");
        var dayNight = root.AddComponent<DayNightService>();
        var tickDriver = root.AddComponent<CentralTickDriver>();
        var bootstrap = root.AddComponent<MainGameBootstrap>();
        var inventoryRuntime = root.AddComponent<InventoryRuntime>();
        var runtimeServices = root.AddComponent<MainGameRuntimeServices>();
        var environmentState = root.AddComponent<MainGameEnvironmentState>();
        var decorationRenderer = root.AddComponent<MainGameWorldDecorationRenderer>();
        var effectPresenter = root.AddComponent<MainGameEffectPresenter>();
        var turretRuntime = root.AddComponent<MainGameTurretRuntime>();
        var saveManager = root.AddComponent<SaveManager>();
        var dawnAutoSave = root.AddComponent<DawnAutoSave>();
        var saveCoordinator = root.AddComponent<MainGameSaveCoordinator>();
        var audioService = root.AddComponent<NyangbingoAudioService>();
        ConfigureAudioMixer(audioService);
        var bossManager = root.AddComponent<BossManager>();
        var bossRewardReceiver = root.AddComponent<BossRewardReceiver>();
        var encounterCoordinator = root.AddComponent<MainGameEncounterCoordinator>();
        var raidTargetObject = new GameObject("Player");
        raidTargetObject.transform.SetParent(root.transform, false);
        var raidTarget = raidTargetObject.AddComponent<MainGameRaidTarget>();
        var playerController = raidTargetObject.AddComponent<MainGamePlayerController>();
        raidTargetObject.GetComponent<Health>().ConfigureForRuntime(100);

        dayNight.ConfigureOfficialData(catalog);
        bootstrap.ConfigureForScene(
            config,
            catalog,
            worldRenderer,
            dayNight,
            tickDriver,
            autoStart: true,
            seed: 12345);
        inventoryRuntime.ConfigureForScene(catalog);
        runtimeServices.ConfigureForScene(catalog, bootstrap, inventoryRuntime);
        environmentState.ConfigureForScene(catalog, bootstrap, buildingArtCatalog);
        decorationRenderer.ConfigureForScene(bootstrap, decorationArtCatalog);
        effectPresenter.ConfigureForScene(gameplayArtCatalog, raidTarget.transform);
        turretRuntime.ConfigureForScene(catalog, runtimeServices, environmentState, gameplayArtCatalog,
            buildingArtCatalog, playerController);
        saveCoordinator.ConfigureForScene(
            bootstrap,
            runtimeServices,
            environmentState,
            turretRuntime,
            encounterCoordinator,
            dayNight,
            saveManager,
            dawnAutoSave,
            slot: 0);
        encounterCoordinator.ConfigureForScene(catalog, bootstrap, runtimeServices, bossManager, raidTarget,
            characterArtCatalog, gameplayArtCatalog);
        bossRewardReceiver.ConfigureForRuntime(bossManager);
        playerController.ConfigureForScene(catalog, bootstrap, runtimeServices, Camera.main, characterArtCatalog,
            gameplayArtCatalog);
        var hud = CreateHud(catalog, itemArtCatalog, gameplayArtCatalog, environmentArtCatalog, bootstrap, runtimeServices,
            playerController, saveCoordinator, saveManager, dayNight, audioService);

        EditorUtility.SetDirty(dayNight);
        EditorUtility.SetDirty(tickDriver);
        EditorUtility.SetDirty(bootstrap);
        EditorUtility.SetDirty(inventoryRuntime);
        EditorUtility.SetDirty(runtimeServices);
        EditorUtility.SetDirty(environmentState);
        EditorUtility.SetDirty(decorationRenderer);
        EditorUtility.SetDirty(effectPresenter);
        EditorUtility.SetDirty(turretRuntime);
        EditorUtility.SetDirty(saveManager);
        EditorUtility.SetDirty(dawnAutoSave);
        EditorUtility.SetDirty(saveCoordinator);
        EditorUtility.SetDirty(audioService);
        EditorUtility.SetDirty(bossManager);
        EditorUtility.SetDirty(bossRewardReceiver);
        EditorUtility.SetDirty(encounterCoordinator);
        EditorUtility.SetDirty(raidTarget);
        EditorUtility.SetDirty(playerController);
        EditorUtility.SetDirty(hud);
    }

    private static void ConfigureAudioMixer(NyangbingoAudioService audioService)
    {
        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(
            NyangbingoAudioMixerIntegrator.MixerPath);
        if (mixer == null || audioService == null) return;
        var bgm = mixer.FindMatchingGroups(NyangbingoAudioMixerIntegrator.BgmGroupName)
            .FirstOrDefault(group => group.name == NyangbingoAudioMixerIntegrator.BgmGroupName);
        var sfx = mixer.FindMatchingGroups(NyangbingoAudioMixerIntegrator.SfxGroupName)
            .FirstOrDefault(group => group.name == NyangbingoAudioMixerIntegrator.SfxGroupName);
        var serialized = new SerializedObject(audioService);
        serialized.FindProperty("audioMixer").objectReferenceValue = mixer;
        serialized.FindProperty("bgmOutput").objectReferenceValue = bgm;
        serialized.FindProperty("sfxOutput").objectReferenceValue = sfx;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static MainGameHudController CreateHud(GameDataCatalog catalog, ItemArtCatalog itemArtCatalog,
        GameplayArtCatalog gameplayArtCatalog,
        EnvironmentArtCatalog environmentArtCatalog, MainGameBootstrap bootstrap,
        MainGameRuntimeServices runtimeServices, MainGamePlayerController playerController,
        MainGameSaveCoordinator saveCoordinator, SaveManager saveManager, DayNightService dayNight,
        NyangbingoAudioService audioService)
    {
        var eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();

        var canvasObject = new GameObject("GameplayHUD", typeof(RectTransform));
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasObject.AddComponent<GraphicRaycaster>();

        var temperatureArt = CreateHudImage(canvasObject.transform, "TemperatureArt",
            new Vector2(24f, -18f), new Vector2(64f, 64f));
        var temperature = CreateHudText(canvasObject.transform, "Temperature", TextAnchor.UpperLeft,
            new Vector2(96f, -22f), new Vector2(300f, 48f), 28);
        var claw = CreateHudText(canvasObject.transform, "ClawTier", TextAnchor.UpperLeft,
            new Vector2(24f, -72f), new Vector2(300f, 48f), 24);
        var playerHealth = CreateHudText(canvasObject.transform, "PlayerHealth", TextAnchor.UpperLeft,
            new Vector2(24f, -116f), new Vector2(300f, 48f), 24);
        var day = CreateHudText(canvasObject.transform, "DayCounter", TextAnchor.UpperCenter,
            new Vector2(0f, -22f), new Vector2(300f, 60f), 38);
        var bossStatus = CreateHudText(canvasObject.transform, "BossStatus", TextAnchor.UpperCenter,
            new Vector2(0f, -82f), new Vector2(760f, 54f), 26);
        var bossSummonStatus = CreateHudText(canvasObject.transform, "BossSummonStatus", TextAnchor.UpperCenter,
            new Vector2(0f, -132f), new Vector2(1280f, 44f), 19);
        var turretInteractionStatus = CreateHudText(canvasObject.transform, "TurretInteractionStatus",
            TextAnchor.UpperCenter, new Vector2(0f, -174f), new Vector2(1000f, 40f), 19);
        var seal = CreateHudText(canvasObject.transform, "SealTemperature", TextAnchor.UpperRight,
            new Vector2(-24f, -22f), new Vector2(340f, 48f), 28);

        var craftingProgressPanel = new GameObject("CraftingProgress", typeof(RectTransform));
        craftingProgressPanel.transform.SetParent(canvasObject.transform, false);
        var craftingRect = craftingProgressPanel.GetComponent<RectTransform>();
        craftingRect.anchorMin = craftingRect.anchorMax = craftingRect.pivot = new Vector2(.5f, 0f);
        craftingRect.anchoredPosition = new Vector2(0f, 184f);
        craftingRect.sizeDelta = new Vector2(560f, 72f);
        var craftingBackground = craftingProgressPanel.AddComponent<Image>();
        craftingBackground.color = new Color(.025f, .04f, .065f, .9f);
        var craftingText = CreateHudText(craftingProgressPanel.transform, "Label", TextAnchor.UpperCenter,
            new Vector2(0f, -8f), new Vector2(520f, 34f), 21);
        var barBackgroundObject = new GameObject("BarBackground", typeof(RectTransform));
        barBackgroundObject.transform.SetParent(craftingProgressPanel.transform, false);
        var barBackgroundRect = barBackgroundObject.GetComponent<RectTransform>();
        barBackgroundRect.anchorMin = barBackgroundRect.anchorMax = barBackgroundRect.pivot =
            new Vector2(.5f, 0f);
        barBackgroundRect.anchoredPosition = new Vector2(0f, 10f);
        barBackgroundRect.sizeDelta = new Vector2(520f, 16f);
        var barBackground = barBackgroundObject.AddComponent<Image>();
        barBackground.color = new Color(.12f, .15f, .19f, 1f);
        var barFillObject = new GameObject("Fill", typeof(RectTransform));
        barFillObject.transform.SetParent(barBackgroundObject.transform, false);
        var barFillRect = barFillObject.GetComponent<RectTransform>();
        barFillRect.anchorMin = barFillRect.anchorMax = barFillRect.pivot = new Vector2(0f, .5f);
        barFillRect.anchoredPosition = new Vector2(2f, 0f);
        barFillRect.sizeDelta = new Vector2(0f, 12f);
        var craftingFill = barFillObject.AddComponent<Image>();
        craftingFill.color = new Color(.28f, .78f, 1f, 1f);
        craftingFill.type = Image.Type.Simple;
        craftingProgressPanel.SetActive(false);

        var turretBuildOpen = CreateMenuButton(canvasObject.transform, "TurretBuildOpen", "건축 [V]",
            new Vector2(800f, -330f), new Vector2(180f, 52f));
        var turretBuildPanel = new GameObject("TurretBuildPanel", typeof(RectTransform));
        turretBuildPanel.transform.SetParent(canvasObject.transform, false);
        var turretBuildRect = turretBuildPanel.GetComponent<RectTransform>();
        turretBuildRect.anchorMin = turretBuildRect.anchorMax = turretBuildRect.pivot = new Vector2(.5f, .5f);
        turretBuildRect.anchoredPosition = new Vector2(650f, 0f);
        turretBuildRect.sizeDelta = new Vector2(500f, 500f);
        var turretBuildBackground = turretBuildPanel.AddComponent<Image>();
        turretBuildBackground.color = new Color(.025f, .04f, .065f, .96f);
        CreateMenuText(turretBuildPanel.transform, "Title", "제작 · 건축", new Vector2(0f, 215f),
            new Vector2(440f, 50f), 30);
        var turretBuildDetails = CreateMenuText(turretBuildPanel.transform, "Details", "",
            new Vector2(0f, 65f), new Vector2(440f, 240f), 20);
        turretBuildDetails.alignment = TextAnchor.UpperLeft;
        var turretCraftButton = CreateMenuButton(turretBuildPanel.transform, "Craft", "작업대에서 제작",
            new Vector2(-115f, -105f), new Vector2(210f, 52f));
        var turretPreviewButton = CreateMenuButton(turretBuildPanel.transform, "Preview", "설치 미리보기",
            new Vector2(115f, -105f), new Vector2(210f, 52f));
        var turretConfirmButton = CreateMenuButton(turretBuildPanel.transform, "Confirm", "설치 확정",
            new Vector2(-115f, -175f), new Vector2(210f, 52f));
        var turretCancelButton = CreateMenuButton(turretBuildPanel.transform, "Cancel", "취소 / 닫기",
            new Vector2(115f, -175f), new Vector2(210f, 52f));
        turretBuildPanel.SetActive(false);

        var inventoryPanel = new GameObject("Inventory12", typeof(RectTransform));
        inventoryPanel.transform.SetParent(canvasObject.transform, false);
        var panelRect = inventoryPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(.5f, 0f);
        panelRect.anchorMax = new Vector2(.5f, 0f);
        panelRect.pivot = new Vector2(.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 24f);
        panelRect.sizeDelta = new Vector2(1050f, 150f);
        var panelImage = inventoryPanel.AddComponent<Image>();
        panelImage.color = new Color(.03f, .05f, .08f, .78f);
        var grid = inventoryPanel.AddComponent<GridLayoutGroup>();
        grid.padding = new RectOffset(12, 12, 12, 12);
        grid.cellSize = new Vector2(161f, 57f);
        grid.spacing = new Vector2(10f, 10f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 6;

        var slots = new Text[MainGameHudController.LegacyInventoryBarSlotCount];
        var slotIcons = new Image[MainGameHudController.LegacyInventoryBarSlotCount];
        for (var index = 0; index < slots.Length; index++)
        {
            var slotObject = new GameObject($"Slot_{index + 1:00}", typeof(RectTransform));
            slotObject.transform.SetParent(inventoryPanel.transform, false);
            var background = slotObject.AddComponent<Image>();
            background.color = new Color(.12f, .16f, .2f, .92f);
            var iconObject = new GameObject("Icon", typeof(RectTransform));
            iconObject.transform.SetParent(slotObject.transform, false);
            slotIcons[index] = iconObject.AddComponent<Image>();
            slotIcons[index].preserveAspect = true;
            slotIcons[index].raycastTarget = false;
            slotIcons[index].enabled = false;
            var iconRect = slotIcons[index].rectTransform;
            iconRect.anchorMin = new Vector2(0f, .5f);
            iconRect.anchorMax = new Vector2(0f, .5f);
            iconRect.pivot = new Vector2(0f, .5f);
            iconRect.anchoredPosition = new Vector2(5f, 0f);
            iconRect.sizeDelta = new Vector2(48f, 48f);
            var labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(slotObject.transform, false);
            slots[index] = labelObject.AddComponent<Text>();
            ConfigureText(slots[index], TextAnchor.MiddleLeft, 16);
            var labelRect = slots[index].rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(57f, 2f);
            labelRect.offsetMax = new Vector2(-4f, -2f);
        }

        var deathPanel = new GameObject("DeathPanel", typeof(RectTransform));
        deathPanel.transform.SetParent(canvasObject.transform, false);
        var deathRect = deathPanel.GetComponent<RectTransform>();
        deathRect.anchorMin = Vector2.zero;
        deathRect.anchorMax = Vector2.one;
        deathRect.offsetMin = Vector2.zero;
        deathRect.offsetMax = Vector2.zero;
        var deathBackground = deathPanel.AddComponent<Image>();
        deathBackground.color = new Color(.05f, .02f, .03f, .82f);
        var deathLabelObject = new GameObject("Label", typeof(RectTransform));
        deathLabelObject.transform.SetParent(deathPanel.transform, false);
        var deathLabel = deathLabelObject.AddComponent<Text>();
        ConfigureText(deathLabel, TextAnchor.MiddleCenter, 42);
        deathLabel.text = "사망\nR 키로 현재 월드 재시작";
        deathLabel.rectTransform.anchorMin = Vector2.zero;
        deathLabel.rectTransform.anchorMax = Vector2.one;
        deathLabel.rectTransform.offsetMin = Vector2.zero;
        deathLabel.rectTransform.offsetMax = Vector2.zero;
        deathPanel.SetActive(false);

        var codexPanel = new GameObject("CodexPanel", typeof(RectTransform));
        codexPanel.transform.SetParent(canvasObject.transform, false);
        var codexRect = codexPanel.GetComponent<RectTransform>();
        codexRect.anchorMin = Vector2.zero;
        codexRect.anchorMax = Vector2.one;
        codexRect.offsetMin = Vector2.zero;
        codexRect.offsetMax = Vector2.zero;
        var codexBackdrop = codexPanel.AddComponent<Image>();
        codexBackdrop.color = new Color(.015f, .025f, .035f, .94f);

        var codexWindow = new GameObject("Window", typeof(RectTransform));
        codexWindow.transform.SetParent(codexPanel.transform, false);
        var windowRect = codexWindow.GetComponent<RectTransform>();
        windowRect.anchorMin = windowRect.anchorMax = windowRect.pivot = new Vector2(.5f, .5f);
        windowRect.sizeDelta = new Vector2(900f, 620f);
        var windowImage = codexWindow.AddComponent<Image>();
        windowImage.color = new Color(.08f, .11f, .14f, 1f);

        var codexTitle = CreateHudText(codexWindow.transform, "Title", TextAnchor.UpperCenter,
            new Vector2(0f, -24f), new Vector2(500f, 52f), 34);
        codexTitle.text = "요괴 도감";

        var gridObject = new GameObject("CardGrid", typeof(RectTransform));
        gridObject.transform.SetParent(codexWindow.transform, false);
        var gridRect = gridObject.GetComponent<RectTransform>();
        gridRect.anchorMin = gridRect.anchorMax = gridRect.pivot = new Vector2(0f, 1f);
        gridRect.anchoredPosition = new Vector2(40f, -100f);
        gridRect.sizeDelta = new Vector2(250f, 430f);
        var codexGrid = gridObject.AddComponent<GridLayoutGroup>();
        codexGrid.cellSize = YokaiCodexPresentationModel.GridCardSize;
        codexGrid.spacing = new Vector2(10f, 10f);
        codexGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        codexGrid.constraintCount = YokaiCodexPresentationModel.GridColumns;

        var cardButtons = new Button[YokaiCodexPresentationModel.ExpectedCardCount];
        var cardTexts = new Text[YokaiCodexPresentationModel.ExpectedCardCount];
        for (var index = 0; index < cardButtons.Length; index++)
        {
            var cardObject = new GameObject($"Card_{index + 1:00}", typeof(RectTransform));
            cardObject.transform.SetParent(gridObject.transform, false);
            var cardImage = cardObject.AddComponent<Image>();
            cardImage.color = new Color(.17f, .21f, .25f, 1f);
            cardButtons[index] = cardObject.AddComponent<Button>();
            var labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(cardObject.transform, false);
            cardTexts[index] = labelObject.AddComponent<Text>();
            ConfigureText(cardTexts[index], TextAnchor.MiddleCenter, 13);
            cardTexts[index].rectTransform.anchorMin = Vector2.zero;
            cardTexts[index].rectTransform.anchorMax = Vector2.one;
            cardTexts[index].rectTransform.offsetMin = new Vector2(3f, 3f);
            cardTexts[index].rectTransform.offsetMax = new Vector2(-3f, -3f);
        }

        var detailText = CreateHudText(codexWindow.transform, "Details", TextAnchor.UpperLeft,
            new Vector2(330f, -105f), new Vector2(520f, 410f), 22);
        var detailRect = detailText.rectTransform;
        detailRect.anchorMin = detailRect.anchorMax = detailRect.pivot = new Vector2(0f, 1f);
        detailRect.anchoredPosition = new Vector2(330f, -105f);

        var codexController = canvasObject.AddComponent<MainGameCodexController>();
        codexController.ConfigureForScene(saveCoordinator, codexPanel, cardButtons, cardTexts, detailText);
        codexPanel.SetActive(false);

        var pausePanel = CreateOverlayPanel(canvasObject.transform, "PausePanel", new Color(.025f, .035f, .05f, .94f));
        CreateMenuText(pausePanel.transform, "Title", "일시정지", new Vector2(0f, 250f),
            new Vector2(500f, 68f), 40);
        var resumeButton = CreateMenuButton(pausePanel.transform, "Resume", "계속하기",
            new Vector2(0f, 96f), new Vector2(280f, 52f));
        var saveButtons = new Button[SaveManager.SlotCount];
        var loadButtons = new Button[SaveManager.SlotCount];
        for (var index = 0; index < SaveManager.SlotCount; index++)
        {
            saveButtons[index] = CreateMenuButton(pausePanel.transform, $"Save_{index + 1}",
                index == 0 ? "저장" : $"구형 저장 {index + 1}",
                new Vector2(0f, 32f), new Vector2(280f, 52f));
            loadButtons[index] = CreateMenuButton(pausePanel.transform, $"Load_{index + 1}", $"불러오기 {index + 1}",
                new Vector2(-220f + index * 220f, 25f), new Vector2(190f, 50f));
            if (index > 0) saveButtons[index].gameObject.SetActive(false);
            loadButtons[index].gameObject.SetActive(false);
        }
        var settingsButton = CreateMenuButton(pausePanel.transform, "Settings", "설정",
            new Vector2(0f, -32f), new Vector2(280f, 52f));
        var returnTitleButton = CreateMenuButton(pausePanel.transform, "ReturnTitle", "타이틀로",
            new Vector2(0f, -96f), new Vector2(280f, 52f));
        var statusText = CreateMenuText(pausePanel.transform, "Status", "", new Vector2(0f, -230f),
            new Vector2(760f, 48f), 20);

        var settingsPanel = CreateOverlayPanel(pausePanel.transform, "SettingsPanel", new Color(.05f, .07f, .09f, 1f));
        CreateMenuText(settingsPanel.transform, "Title", "설정", new Vector2(0f, 210f),
            new Vector2(400f, 62f), 38);
        CreateMenuText(settingsPanel.transform, "BgmLabel", "BGM", new Vector2(-260f, 95f),
            new Vector2(120f, 42f), 22);
        var bgmSlider = CreateMenuSlider(settingsPanel.transform, "BgmVolume", new Vector2(40f, 95f));
        CreateMenuText(settingsPanel.transform, "SfxLabel", "SFX", new Vector2(-260f, 25f),
            new Vector2(120f, 42f), 22);
        var sfxSlider = CreateMenuSlider(settingsPanel.transform, "SfxVolume", new Vector2(40f, 25f));
        var fullscreenToggle = CreateMenuToggle(settingsPanel.transform, "Fullscreen", "전체 화면",
            new Vector2(0f, -55f));
        var applySettings = CreateMenuButton(settingsPanel.transform, "Apply", "적용",
            new Vector2(-100f, -150f), new Vector2(180f, 50f));
        var backSettings = CreateMenuButton(settingsPanel.transform, "Back", "뒤로",
            new Vector2(100f, -150f), new Vector2(180f, 50f));

        var confirmationPanel = CreateOverlayPanel(pausePanel.transform, "ConfirmationPanel",
            new Color(.06f, .04f, .05f, 1f));
        var confirmationText = CreateMenuText(confirmationPanel.transform, "Message", "확인하시겠습니까?",
            new Vector2(0f, 70f), new Vector2(720f, 120f), 26);
        var confirmButton = CreateMenuButton(confirmationPanel.transform, "Confirm", "확인",
            new Vector2(-110f, -65f), new Vector2(190f, 52f));
        var cancelButton = CreateMenuButton(confirmationPanel.transform, "Cancel", "취소",
            new Vector2(110f, -65f), new Vector2(190f, 52f));

        var resultPanel = CreateOverlayPanel(canvasObject.transform, "ResultPanel", new Color(.02f, .035f, .05f, .98f));
        CreateMenuText(resultPanel.transform, "Result", "30일 체험 종료", new Vector2(0f, 80f),
            new Vector2(600f, 80f), 42);
        var resultTitle = CreateMenuButton(resultPanel.transform, "ReturnTitle", "타이틀로",
            new Vector2(0f, -30f), new Vector2(260f, 54f));

        var shell = canvasObject.AddComponent<GameShellController>();
        shell.ConfigureViews(pausePanel, resultPanel, settingsPanel, confirmationPanel);
        codexController.ConfigureGameShell(shell);
        var shellUi = canvasObject.AddComponent<MainGameShellUiController>();
        shellUi.ConfigureForScene(shell, saveCoordinator, saveManager, audioService, dayNight, codexController,
            resumeButton, saveButtons, loadButtons, settingsButton, returnTitleButton, applySettings, backSettings,
            bgmSlider, sfxSlider, fullscreenToggle, confirmButton, cancelButton, confirmationText,
            resultTitle, statusText, gameplayArtCatalog);

        var bossSummonUi = canvasObject.AddComponent<MainGameBossSummonUiController>();
        bossSummonUi.ConfigureForScene(catalog, bootstrap, runtimeServices,
            Object.FindAnyObjectByType<MainGameEncounterCoordinator>(),
            Object.FindAnyObjectByType<MainGameRaidTarget>(), bossSummonStatus,
            Object.FindAnyObjectByType<MainGameEnvironmentState>());
        Object.FindAnyObjectByType<MainGameTurretRuntime>()?.BindInteractionStatus(turretInteractionStatus);
        var turretBuildUi = canvasObject.AddComponent<MainGameTurretBuildUiController>();
        turretBuildUi.ConfigureForScene(Object.FindAnyObjectByType<MainGameTurretRuntime>(), turretBuildPanel,
            turretBuildDetails, turretBuildOpen, turretCraftButton, turretPreviewButton, turretConfirmButton,
            turretCancelButton);

        var hud = canvasObject.AddComponent<MainGameHudController>();
        var tilePalette = canvasObject.AddComponent<MainGameTilePaletteController>();
        tilePalette.ConfigureForScene(catalog, bootstrap, runtimeServices,
            Object.FindAnyObjectByType<MainGameTurretRuntime>(), itemArtCatalog, gameplayArtCatalog);
        hud.ConfigureForScene(catalog, bootstrap, runtimeServices, temperature, seal, day, claw, playerHealth,
            playerController.GetComponent<Health>(), Object.FindAnyObjectByType<BossManager>(), bossStatus,
            deathPanel, slots, slotIcons, itemArtCatalog, temperatureArt, gameplayArtCatalog,
            craftingProgressPanel, craftingText, craftingFill, environmentArtCatalog);
        inventoryPanel.SetActive(false);
        turretBuildOpen.gameObject.SetActive(false);
        EditorUtility.SetDirty(codexController);
        EditorUtility.SetDirty(shell);
        EditorUtility.SetDirty(shellUi);
        EditorUtility.SetDirty(bossSummonUi);
        EditorUtility.SetDirty(turretBuildUi);
        EditorUtility.SetDirty(tilePalette);
        return hud;
    }

    private static Image CreateHudImage(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var imageObject = new GameObject(name, typeof(RectTransform));
        imageObject.transform.SetParent(parent, false);
        var image = imageObject.AddComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.enabled = false;
        var rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return image;
    }

    private static GameObject CreateOverlayPanel(Transform parent, string name, Color color)
    {
        var panel = new GameObject(name, typeof(RectTransform));
        panel.transform.SetParent(parent, false);
        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        panel.AddComponent<Image>().color = color;
        return panel;
    }

    private static Text CreateMenuText(Transform parent, string name, string value, Vector2 position,
        Vector2 size, int fontSize)
    {
        var textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        var text = textObject.AddComponent<Text>();
        ConfigureText(text, TextAnchor.MiddleCenter, fontSize);
        text.text = value;
        var rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return text;
    }

    private static Button CreateMenuButton(Transform parent, string name, string label, Vector2 position,
        Vector2 size)
    {
        var buttonObject = new GameObject(name, typeof(RectTransform));
        buttonObject.transform.SetParent(parent, false);
        var rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var image = buttonObject.AddComponent<Image>();
        image.color = new Color(.17f, .22f, .28f, 1f);
        var button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        var labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(buttonObject.transform, false);
        var text = labelObject.AddComponent<Text>();
        ConfigureText(text, TextAnchor.MiddleCenter, 21);
        text.text = label;
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
        return button;
    }

    private static Slider CreateMenuSlider(Transform parent, string name, Vector2 position)
    {
        var sliderObject = new GameObject(name, typeof(RectTransform));
        sliderObject.transform.SetParent(parent, false);
        var rect = sliderObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(420f, 36f);

        var backgroundObject = new GameObject("Background", typeof(RectTransform));
        backgroundObject.transform.SetParent(sliderObject.transform, false);
        var backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0f, .35f);
        backgroundRect.anchorMax = new Vector2(1f, .65f);
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        backgroundObject.AddComponent<Image>().color = new Color(.1f, .13f, .17f, 1f);

        var fillObject = new GameObject("Fill", typeof(RectTransform));
        fillObject.transform.SetParent(sliderObject.transform, false);
        var fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, .35f);
        fillRect.anchorMax = new Vector2(1f, .65f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        fillObject.AddComponent<Image>().color = new Color(.25f, .75f, .95f, 1f);

        var handleAreaObject = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleAreaObject.transform.SetParent(sliderObject.transform, false);
        var handleAreaRect = handleAreaObject.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(13f, 0f);
        handleAreaRect.offsetMax = new Vector2(-13f, 0f);

        var handleObject = new GameObject("Handle", typeof(RectTransform));
        handleObject.transform.SetParent(handleAreaObject.transform, false);
        var handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(26f, 34f);
        var handleImage = handleObject.AddComponent<Image>();
        handleImage.color = new Color(.92f, .95f, 1f, 1f);

        var slider = sliderObject.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;
        return slider;
    }

    private static Toggle CreateMenuToggle(Transform parent, string name, string label, Vector2 position)
    {
        var toggleObject = new GameObject(name, typeof(RectTransform));
        toggleObject.transform.SetParent(parent, false);
        var rect = toggleObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(260f, 44f);

        var backgroundObject = new GameObject("Background", typeof(RectTransform));
        backgroundObject.transform.SetParent(toggleObject.transform, false);
        var backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = backgroundRect.anchorMax = backgroundRect.pivot = new Vector2(0f, .5f);
        backgroundRect.anchoredPosition = new Vector2(4f, 0f);
        backgroundRect.sizeDelta = new Vector2(34f, 34f);
        var backgroundImage = backgroundObject.AddComponent<Image>();
        backgroundImage.color = new Color(.12f, .16f, .2f, 1f);

        var checkObject = new GameObject("Checkmark", typeof(RectTransform));
        checkObject.transform.SetParent(backgroundObject.transform, false);
        var checkRect = checkObject.GetComponent<RectTransform>();
        checkRect.anchorMin = new Vector2(.2f, .2f);
        checkRect.anchorMax = new Vector2(.8f, .8f);
        checkRect.offsetMin = Vector2.zero;
        checkRect.offsetMax = Vector2.zero;
        var checkImage = checkObject.AddComponent<Image>();
        checkImage.color = new Color(.25f, .8f, .95f, 1f);

        var labelText = CreateMenuText(toggleObject.transform, "Label", label, new Vector2(55f, 0f),
            new Vector2(180f, 42f), 21);
        labelText.alignment = TextAnchor.MiddleLeft;
        var toggle = toggleObject.AddComponent<Toggle>();
        toggle.targetGraphic = backgroundImage;
        toggle.graphic = checkImage;
        return toggle;
    }

    private static Text CreateHudText(Transform parent, string name, TextAnchor alignment,
        Vector2 anchoredPosition, Vector2 size, int fontSize)
    {
        var textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        var text = textObject.AddComponent<Text>();
        ConfigureText(text, alignment, fontSize);
        var rect = text.rectTransform;
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
        if (alignment == TextAnchor.UpperLeft)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        }
        else if (alignment == TextAnchor.UpperRight)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
        }
        else
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 1f);
        }
        return text;
    }

    private static void ConfigureText(Text text, TextAnchor alignment, int fontSize)
    {
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = new Color(.94f, .96f, 1f, 1f);
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
    }

    private static Tilemap CreateTilemapLayer(Transform parent, string name, int sortingOrder)
    {
        var layer = new GameObject(name);
        layer.transform.SetParent(parent, false);
        var tilemap = layer.AddComponent<Tilemap>();
        tilemap.tileAnchor = WorldTilemapRenderer.TerrainVisualAnchor;
        var renderer = layer.AddComponent<UnityTilemapRenderer>();
        renderer.sortingOrder = sortingOrder;
        return tilemap;
    }

}
