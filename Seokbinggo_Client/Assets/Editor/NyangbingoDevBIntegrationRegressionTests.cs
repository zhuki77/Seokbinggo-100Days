using System;
using System.Collections;
using System.Reflection;
using Nyangbingo.Combat;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using Nyangbingo.Save;
using Nyangbingo.UI;
using Nyangbingo.World;
using UnityEditor;
using UnityEngine;

public static class NyangbingoDevBIntegrationRegressionTests
{
    private const BindingFlags InstanceMembers =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [MenuItem("Nyangbingo/Run Dev B Integration Regression Tests")]
    public static void RunAll()
    {
        TestIceStorageSealCoreLifecycle();
        TestV29InventoryLayoutContract();
        TestV29InventoryArtBindings();
        TestTilePaletteContract();
        TestWallpaperCoolingDurationMultiplier();
        TestWallpaperRemovalDropContract();
        TestDayNightCountdownFormatting();
        TestBossHealthArtMapping();
        TestNarrativeFreeProductHudContract();
        TestWorldCellCoordinateContract();
        TestDemoSafeSpawnRestorePolicy();
        TestLatestProductFlowContracts();
        TestPlayerPhysicsIntegrationContract();
        TestSurfaceCameraCompositionContract();
        TestMeleeArcAttackPhysicsQueryContract();
        TestWorldMobPhysicsContract();
        TestWorldDropVisualSurfaceOffset();
        TestTreeVegetationVisualOffset();
        TestBossPausedYokaiVisibilityContract();
        TestPlayerDeathAnimationContract();
        TestDeliveredShellGlyphArtContract();
        TestCraftAndPlacementActionsRemainIndependent();
        TestMissingTileEdgeOverlayRemainsDisabled();
        TestDetailedDynamicSaveSchema();
        Debug.Log("[Nyangbingo] Dev B integration regression tests passed (24/24).");
    }

    private static void TestBossPausedYokaiVisibilityContract()
    {
        var root = new GameObject("BossPausedYokaiVisibility", typeof(SpriteRenderer));
        try
        {
            var renderer = root.GetComponent<SpriteRenderer>();
            var original = new Color(.2f, .4f, .6f, .7f);
            renderer.color = original;
            var brain = root.AddComponent<Nyangbingo.Yokai.YokaiBrain>();

            Require(brain.SetBossEncounterPaused(true) && Mathf.Approximately(renderer.color.a, 0f),
                "Field yokai paused by a boss encounter must be completely invisible.");
            Require(brain.SetBossEncounterPaused(false) && renderer.color == original,
                "Field yokai visibility and tint must be restored exactly after the boss encounter.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void TestDetailedDynamicSaveSchema()
    {
        var save = new SaveGame
        {
            sealPct = 73.5f,
            baekjungTearRemainder = .5f,
            regularEncounter = new RegularEncounterStateRecord
            {
                hasValue = true,
                day = 15,
                isNight = true,
                usesDetailedYokaiState = true,
                activeYokai = new System.Collections.Generic.List<YokaiStateRecord>
                {
                    new YokaiStateRecord
                    {
                        instanceId = "yokai_7",
                        yokaiId = "club",
                        position = new Vector3(12.5f, 8.5f, 0f),
                        velocity = new Vector2(1.5f, -2f),
                        currentHealth = 17,
                        maxHealth = 30,
                        raid = true,
                        behaviorState = 2,
                        contactAttackRemaining = .4f,
                        frostSlowFraction = .25f,
                        frostSlowRemaining = 2f
                    }
                },
                pendingRegularYokaiIds = new System.Collections.Generic.List<string> { "club" },
                pendingRaidYokaiIds = new System.Collections.Generic.List<string> { "club" }
            },
            worldDrops = new System.Collections.Generic.List<WorldDropStateRecord>
            {
                new WorldDropStateRecord
                {
                    itemId = "stone",
                    amount = 3,
                    position = new Vector2(4.25f, 6.5f),
                    velocity = new Vector2(-1f, 2f),
                    pickupDelay = .2f
                }
            }
        };

        Require(SaveManager.TryDeserialize(JsonUtility.ToJson(save), out var loaded),
            "Detailed dynamic save JSON must deserialize.");
        Require(loaded.schemaVersion == 17 && loaded.regularEncounter.usesDetailedYokaiState &&
                loaded.regularEncounter.activeYokai.Count == 1 &&
                loaded.regularEncounter.activeYokai[0].instanceId == "yokai_7" &&
                loaded.regularEncounter.activeYokai[0].position == new Vector3(12.5f, 8.5f, 0f) &&
                loaded.regularEncounter.activeYokai[0].velocity == new Vector2(1.5f, -2f) &&
                loaded.regularEncounter.activeYokai[0].currentHealth == 17 &&
                loaded.regularEncounter.activeYokai[0].raid &&
                loaded.regularEncounter.pendingRegularYokaiIds.Count == 1 &&
                loaded.regularEncounter.pendingRaidYokaiIds.Count == 1,
            "Detailed yokai identity, position, HP, track, and queues must survive JSON.");
        Require(loaded.worldDrops.Count == 1 && loaded.worldDrops[0].itemId == "stone" &&
                loaded.worldDrops[0].amount == 3 &&
                loaded.worldDrops[0].position == new Vector2(4.25f, 6.5f) &&
                loaded.worldDrops[0].velocity == new Vector2(-1f, 2f) &&
                Mathf.Approximately(loaded.worldDrops[0].pickupDelay, .2f),
            "World-drop item, amount, transform, velocity, and pickup delay must survive JSON.");
        Require(Mathf.Approximately(loaded.sealPct, 73.5f) &&
                Mathf.Approximately(loaded.baekjungTearRemainder, .5f),
            "Seal percentage and Baekjung reward remainder must survive JSON.");
        Require(SaveManager.TryDeserialize("{\"schemaVersion\":16}", out var legacy) &&
                legacy.schemaVersion == SaveGame.CurrentSchemaVersion &&
                legacy.worldDrops != null && legacy.worldDrops.Count == 0 &&
                legacy.regularEncounter != null &&
                !legacy.regularEncounter.usesDetailedYokaiState &&
                legacy.regularEncounter.activeYokai != null &&
                legacy.regularEncounter.pendingRegularYokaiIds != null &&
                legacy.regularEncounter.pendingRaidYokaiIds != null,
            "Schema 16 saves must migrate to empty dynamic lists and the legacy encounter fallback.");
    }

    private static void TestSurfaceCameraCompositionContract()
    {
        const float undergroundThreshold = 123.2f;
        const float orthographicSize = MainGamePlayerController.GameplayCameraOrthographicSize;

        Require(Mathf.Approximately(orthographicSize, 8f),
            "The runtime gameplay camera must keep the requested close-up framing.");
        Require(Mathf.Approximately(
                MainGamePlayerController.CalculateSurfaceCameraVerticalOffset(
                    undergroundThreshold + 8f, undergroundThreshold, orthographicSize),
                4f),
            "At the surface, the camera must move up by half its orthographic size so terrain " +
            "occupies one quarter rather than one half of the viewport.");
        Require(Mathf.Approximately(
                MainGamePlayerController.CalculateSurfaceCameraVerticalOffset(
                    undergroundThreshold + 4f, undergroundThreshold, orthographicSize),
                2f),
            "The surface camera offset must blend out smoothly through the eight-tile transition.");
        Require(Mathf.Approximately(
                MainGamePlayerController.CalculateSurfaceCameraVerticalOffset(
                    undergroundThreshold, undergroundThreshold, orthographicSize),
                0f),
            "Underground camera framing must remain centered on the player.");

        var playerControllerSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/World/MainGamePlayerController.cs");
        Require(playerControllerSource.Contains("SnapCameraToPlayer();") &&
                playerControllerSource.Contains("Time.deltaTime") &&
                !playerControllerSource.Contains("cameraFollowSharpness * Time.unscaledDeltaTime"),
            "The camera must snap to its initial gameplay target and remain frozen during paused loading.");
    }

    private static void TestDeliveredShellGlyphArtContract()
    {
        Require(SaveManager.SlotCount == 1,
            "The product shell must expose exactly one save slot.");
        var catalog = AssetDatabase.LoadAssetAtPath<GameplayArtCatalog>(
            "Assets/Art/Gameplay/GameplayArtCatalog.asset");
        var environmentCatalog = AssetDatabase.LoadAssetAtPath<EnvironmentArtCatalog>(
            "Assets/Art/Backgrounds/EnvironmentArtCatalog.asset");
        Require(catalog != null &&
                catalog.ShellNumberGlyphs.Count == RuntimePixelGlyphPresenter.ExpectedGlyphCount &&
                catalog.ShellTitleLogo != null && catalog.ShellContinue != null &&
                catalog.ShellResume != null && catalog.ShellSave != null &&
                catalog.ShellReturnTitle != null && catalog.ShellApply != null &&
                catalog.ShellBack != null && catalog.ShellBgmLabel != null &&
                catalog.ShellSfxLabel != null && catalog.ShellPauseTitle != null &&
                catalog.ShellPauseIcon != null && catalog.ShellPlayIcon != null &&
                catalog.ShellCheckOn != null && catalog.ShellCheckOff != null,
            "The delivered title, pause, settings, and numeric shell art must be fully catalog-bound.");
        Require(catalog.ShellLoadingSheet != null &&
                catalog.ShellLoadingSheet.texture.width == 3200 &&
                catalog.ShellLoadingSheet.texture.height == 1440 &&
                MainGameShellUiController.ShellLoadingFrameCount == 17 &&
                Mathf.Approximately(MainGameShellUiController.ShellLoadingDurationSeconds, 2.2f),
            "The delivered logo tear loading animation must keep its optimized 5x4 sheet and timing.");
        var loadingDuration = 0f;
        for (var index = 0; index < MainGameShellUiController.ShellLoadingFrameCount; index++)
            loadingDuration += MainGameShellUiController.ShellLoadingFrameDurationSeconds(index);
        Require(Mathf.Approximately(loadingDuration,
                MainGameShellUiController.ShellLoadingDurationSeconds),
            "The shell loading frame timings must add up to the declared transition duration.");
        Require(Mathf.Approximately(
                    GameShellController.ResolveTimeScaleAfterLoading(GameShellScreen.Gameplay), 1f) &&
                Mathf.Approximately(
                    GameShellController.ResolveTimeScaleAfterLoading(GameShellScreen.Title), 0f),
            "Loading completion must always resume gameplay and keep title screens paused.");
        var gameShellSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/UI/GameShellController.cs");
        Require(!gameShellSource.Contains("ReplaceSlotOne") &&
                !gameShellSource.Contains("HasSave(AutoSaveSlot)") &&
                !gameShellSource.Contains("ShowGameplay();\n            ContinueRequested") &&
                gameShellSource.Contains("NewGameRequested?.Invoke(AutoSaveSlot)"),
            "The single-slot Start button must immediately request a clean new game.");
        Require(RuntimePixelGlyphPresenter.GlyphIndex('D') == 0 &&
                RuntimePixelGlyphPresenter.GlyphIndex('-') == 1 &&
                RuntimePixelGlyphPresenter.GlyphIndex(':') == 2 &&
                RuntimePixelGlyphPresenter.GlyphIndex('0') == 3 &&
                RuntimePixelGlyphPresenter.GlyphIndex('9') == 12,
            "D-day and clock characters must map to the delivered glyph catalog order.");
        Require(environmentCatalog != null && environmentCatalog.TitleBackground != null &&
                environmentCatalog.TitleBackground.texture.width == 1920 &&
                environmentCatalog.TitleBackground.texture.height == 1080,
            "The title screen must use the delivered 1920x1080 title key visual.");

        var shellSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/UI/MainGameShellUiController.cs");
        Require(shellSource.Contains("ConfigurePauseHoverIndicator") &&
                shellSource.Contains("EventTriggerType.PointerEnter") &&
                shellSource.Contains("checkmark.rectTransform.sizeDelta = offSize") &&
                shellSource.Contains("SetStatus(string.Empty)") &&
                shellSource.Contains("pauseHoverIndicator.gameObject.SetActive") &&
                shellSource.Contains("BeginShellLoadingTransition") &&
                shellSource.Contains("StabilizeGameplayCamera();") &&
                shellSource.Contains("shellLoadingImage.sprite = shellLoadingFrames[0]") &&
                shellSource.Contains("yield return PlayShellLoadingReveal()") &&
                shellSource.Contains("revealLoadingAfterReload = true") &&
                shellSource.Contains("shell.RestoreTimeScaleAfterLoading()") &&
                shellSource.Contains("saveManager.DeleteAll()") &&
                shellSource.Contains("discardSaveAfterReload = true") &&
                shellSource.Contains("MainGameBootstrap.RequestFreshWorldForNextScene(previousSeed)") &&
                shellSource.Contains("CreateFreshInitialSave()") &&
                shellSource.Contains("saveManager.Save(GameShellController.AutoSaveSlot, initialSnapshot)") &&
                shellSource.Contains("LoadScene(SceneManager.GetActiveScene().name)") &&
                shellSource.IndexOf("completion?.Invoke();", shellSource.IndexOf(
                    "private IEnumerator PlayShellLoadingTransition", StringComparison.Ordinal),
                    StringComparison.Ordinal) <
                shellSource.IndexOf("shellLoadingOverlay.SetActive(false);", shellSource.IndexOf(
                    "private IEnumerator PlayShellLoadingTransition", StringComparison.Ordinal),
                    StringComparison.Ordinal) &&
                shellSource.Contains("WaitForSecondsRealtime") &&
                shellSource.Contains("new Vector2(-112f, 82f)") &&
                shellSource.Contains("new Vector2(96f, 96f)") &&
                shellSource.Contains("new Vector2(176f, 97f)"),
            "Shell art and loading must remain wired, with loading frozen before the post-load tear reveal.");

        var hudSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/UI/MainGameHudController.cs");
        Require(hudSource.Contains("playerHealthGlyphs.SetText(displayedHealth)") &&
                hudSource.Contains("playerTemperatureGlyphs.SetText(displayedTemperature)") &&
                hudSource.Contains("-dayClockGlyphs.RenderedWidth * .5f"),
            "Player vitals and the day/night icon must stay aligned to the delivered number art.");

        var root = new GameObject("DeliveredShellGlyphContract", typeof(RectTransform),
            typeof(RuntimePixelGlyphPresenter));
        try
        {
            var presenter = root.GetComponent<RuntimePixelGlyphPresenter>();
            presenter.ConfigureForRuntime(catalog.ShellNumberGlyphs);
            presenter.SetText("D-99");
            Require(presenter.DisplayedText == "D-99" && presenter.VisibleGlyphCount == 4,
                "The delivered glyph presenter must compose a D-day value without system-font text.");
            presenter.SetText("08:30");
            Require(presenter.DisplayedText == "08:30" && presenter.VisibleGlyphCount == 5,
                "The delivered glyph presenter must compose the day/night clock from the same number set.");
            presenter.SetText("100/100");
            Require(presenter.VisibleGlyphCount == 7 && presenter.RenderedWidth > 0f,
                "Player health must retain its current/maximum separator with delivered number art.");
            presenter.SetText("38.0");
            Require(presenter.VisibleGlyphCount == 4,
                "Player temperature must retain its decimal point with delivered number art.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void TestPlayerDeathAnimationContract()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<CharacterArtCatalog>(
            "Assets/Art/Characters/CharacterArtCatalog.asset");
        var playerEntry = catalog != null ? catalog.Find("player") : null;
        Require(playerEntry != null && playerEntry.DeathFrames.Count == 2,
            "The delivered Frostclaw art must bind both frames from the 'die' Aseprite tag.");

        var root = new GameObject("PlayerDeathAnimationContract", typeof(SpriteRenderer),
            typeof(RuntimeCharacterSpriteAnimator));
        var idleTexture = new Texture2D(1, 1);
        var firstDeathTexture = new Texture2D(1, 1);
        var finalDeathTexture = new Texture2D(1, 1);
        var idle = Sprite.Create(idleTexture, new Rect(0f, 0f, 1f, 1f), Vector2.zero);
        var firstDeath = Sprite.Create(firstDeathTexture, new Rect(0f, 0f, 1f, 1f), Vector2.zero);
        var finalDeath = Sprite.Create(finalDeathTexture, new Rect(0f, 0f, 1f, 1f), Vector2.zero);
        try
        {
            var entry = new CharacterArtCatalog.Entry();
            typeof(CharacterArtCatalog.Entry).GetField("sprite", InstanceMembers)?.SetValue(entry, idle);
            typeof(CharacterArtCatalog.Entry).GetField("idleFrames", InstanceMembers)
                ?.SetValue(entry, new[] { idle });
            typeof(CharacterArtCatalog.Entry).GetField("deathFrames", InstanceMembers)
                ?.SetValue(entry, new[] { firstDeath, finalDeath });

            var animator = root.GetComponent<RuntimeCharacterSpriteAnimator>();
            var renderer = root.GetComponent<SpriteRenderer>();
            animator.Configure(entry, 0);
            animator.PlayDeath();
            Require(renderer.sprite == firstDeath,
                "Player death playback must start from the first delivered death frame.");

            var tick = typeof(RuntimeCharacterSpriteAnimator).GetMethod("TickFrames", InstanceMembers);
            tick?.Invoke(animator, new object[] { .11f });
            Require(renderer.sprite == finalDeath,
                "Player death playback must advance to the final delivered death frame.");
            tick?.Invoke(animator, new object[] { 2f });
            Require(renderer.sprite == finalDeath,
                "Player death playback must hold its final frame instead of looping back to idle.");

            animator.ResetToIdle();
            Require(renderer.sprite == idle,
                "Respawn must restore the player idle frame while the screen is faded out.");

            var playerSource = System.IO.File.ReadAllText(
                "Assets/Scripts/Nyangbingo/World/MainGamePlayerController.cs");
            Require(playerSource.Contains("ResolveSafeSurfaceRespawn(preferredRespawnPosition)") &&
                    playerSource.Contains("resolver.TryResolveSafeSurfaceSpawn(preferredCellX"),
                "Death respawn must resolve the nest or initial spawn column onto a safe world surface.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(idle);
            UnityEngine.Object.DestroyImmediate(firstDeath);
            UnityEngine.Object.DestroyImmediate(finalDeath);
            UnityEngine.Object.DestroyImmediate(idleTexture);
            UnityEngine.Object.DestroyImmediate(firstDeathTexture);
            UnityEngine.Object.DestroyImmediate(finalDeathTexture);
        }
    }

    private static void TestMeleeArcAttackPhysicsQueryContract()
    {
        var root = new GameObject("MeleeArcPhysicsQueryContract");
        try
        {
            var attacker = new GameObject("PlayerAttacker", typeof(Health), typeof(MeleeArcAttack));
            attacker.transform.SetParent(root.transform, false);
            var attackerHealth = attacker.GetComponent<Health>();
            attackerHealth.ConfigureForRuntime(100);

            var selfHurtbox = new GameObject("PlayerHurtbox", typeof(BoxCollider2D));
            selfHurtbox.transform.SetParent(attacker.transform, false);
            selfHurtbox.transform.localPosition = new Vector3(.25f, 0f, 0f);
            selfHurtbox.GetComponent<BoxCollider2D>().isTrigger = true;

            var yokai = new GameObject("GroundYokai", typeof(Health), typeof(BoxCollider2D));
            yokai.transform.SetParent(root.transform, false);
            yokai.transform.position = new Vector3(.75f, 0f, 0f);
            var yokaiHealth = yokai.GetComponent<Health>();
            yokaiHealth.ConfigureForRuntime(100);
            var yokaiDamageEvents = 0;
            yokaiHealth.Damaged += (_, __) => yokaiDamageEvents++;

            var yokaiHurtbox = new GameObject("GroundYokaiHurtbox", typeof(BoxCollider2D));
            yokaiHurtbox.transform.SetParent(yokai.transform, false);
            yokaiHurtbox.transform.localPosition = new Vector3(.05f, 0f, 0f);
            yokaiHurtbox.GetComponent<BoxCollider2D>().isTrigger = true;

            var boss = new GameObject("BossTarget", typeof(Health), typeof(CircleCollider2D));
            boss.transform.SetParent(root.transform, false);
            boss.transform.position = new Vector3(2.8f, .1f, 0f);
            boss.GetComponent<CircleCollider2D>().radius = .2f;
            var bossHealth = boss.GetComponent<Health>();
            bossHealth.ConfigureForRuntime(200);
            var bossDamageEvents = 0;
            bossHealth.Damaged += (_, __) => bossDamageEvents++;
            var bossSpriteEdge = new GameObject("BossSpriteEdgeHurtbox", typeof(BoxCollider2D));
            bossSpriteEdge.transform.SetParent(boss.transform, false);
            bossSpriteEdge.transform.localPosition = new Vector3(-1f, 0f, 0f);
            bossSpriteEdge.GetComponent<BoxCollider2D>().size = new Vector2(.4f, 1f);
            bossSpriteEdge.GetComponent<BoxCollider2D>().isTrigger = true;

            var rearTarget = new GameObject("RearTarget", typeof(Health), typeof(BoxCollider2D));
            rearTarget.transform.SetParent(root.transform, false);
            rearTarget.transform.position = new Vector3(-.6f, 0f, 0f);
            var rearHealth = rearTarget.GetComponent<Health>();
            rearHealth.ConfigureForRuntime(100);

            var distantTarget = new GameObject("DistantTarget", typeof(Health), typeof(BoxCollider2D));
            distantTarget.transform.SetParent(root.transform, false);
            distantTarget.transform.position = new Vector3(3f, 0f, 0f);
            var distantHealth = distantTarget.GetComponent<Health>();
            distantHealth.ConfigureForRuntime(100);

            var attack = attacker.GetComponent<MeleeArcAttack>();
            attack.ConfigureForRuntime(attacker.transform, Physics2D.AllLayers,
                attackRange: 2f, attackArc: 120f, attackDamage: 10, attackKnockback: 0f);
            Physics2D.SyncTransforms();
            attack.Strike(Vector2.right);

            Require(attackerHealth.Current == 100 && yokaiHealth.Current == 90 && bossHealth.Current == 190 &&
                    rearHealth.Current == 100 && distantHealth.Current == 100,
                "A melee swing must damage only forward in-range yokai and boss targets, never self or excluded targets.");
            Require(attack.LastHitCount == 2 && yokaiDamageEvents == 1 && bossDamageEvents == 1,
                "A boss sprite edge hurtbox must take one melee hit even when its movement core is out of range.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void TestPlayerPhysicsIntegrationContract()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<GameDataCatalog>(
            "Assets/Data/SO/GameDataCatalog.asset");
        Require(catalog != null,
            "The product GameDataCatalog must exist for the merged player physics contract.");
        Require(PlayerMovementPhysics.TryLoadFromCatalog(catalog, out var physics),
            "The merged player controller must load its jump and gravity values from the product catalog.");

        var playerObject = new GameObject("PlayerPhysicsIntegrationContract",
            typeof(Rigidbody2D), typeof(CircleCollider2D));
        try
        {
            var body = playerObject.GetComponent<Rigidbody2D>();
            var playerCollider = playerObject.GetComponent<CircleCollider2D>();
            MainGamePlayerController.ConfigurePhysicsBody(body, playerCollider);

            Require(body.bodyType == RigidbodyType2D.Dynamic &&
                    Mathf.Approximately(body.gravityScale, 0f) && body.freezeRotation &&
                    body.collisionDetectionMode == CollisionDetectionMode2D.Continuous &&
                    body.interpolation == RigidbodyInterpolation2D.Interpolate &&
                    !playerCollider.isTrigger && Mathf.Approximately(playerCollider.radius, .38f),
                "The merged player must retain the official dynamic foreground-physics body contract.");

            const float fixedDeltaSeconds = .02f;
            var fullPeak = PlayerMovementPhysics.SimulatePeakJumpHeightTiles(physics, fixedDeltaSeconds);
            var shortPeak = PlayerMovementPhysics.SimulatePeakJumpHeightTiles(
                physics, fixedDeltaSeconds, holdFrames: 3);
            Require(fullPeak >= 3.1f && fullPeak <= 3.9f && shortPeak < fullPeak * .65f,
                "Full and released-early jumps must preserve the catalog-driven Terraria-like height split.");
            var airborneVelocity =
                MainGamePlayerController.CalculateBossAirborneVelocity(2f, physics.Gravity);
            var airbornePeak = airborneVelocity * airborneVelocity / (2f * physics.Gravity);
            Require(Mathf.Abs(airbornePeak - 2f) <= .001f,
                "A two-tile boss airborne request must resolve to a two-tile player launch apex.");

            var fallingVelocity = 0f;
            for (var step = 0; step < 240; step++)
                fallingVelocity = MainGamePlayerController.ApplyGravity(fallingVelocity,
                    physics.Gravity, physics.MaxFallSpeed, fixedDeltaSeconds);
            Require(Mathf.Approximately(fallingVelocity, -physics.MaxFallSpeed) &&
                    Mathf.Approximately(MainGamePlayerController.CalculateHorizontalVelocity(2f, 6f), 6f),
                "Dynamic player movement must clamp both terminal fall speed and horizontal input.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(playerObject);
        }
    }

    private static void TestMissingTileEdgeOverlayRemainsDisabled()
    {
        var host = new GameObject("NoRuntimeEdgeOverlayContract", typeof(Grid));
        try
        {
            var foregroundObject = new GameObject("Foreground",
                typeof(UnityEngine.Tilemaps.Tilemap), typeof(UnityEngine.Tilemaps.TilemapRenderer));
            foregroundObject.transform.SetParent(host.transform, false);
            var foreground = foregroundObject.GetComponent<UnityEngine.Tilemaps.Tilemap>();

            var worldRenderer = host.AddComponent<Nyangbingo.World.TilemapRenderer>();
            typeof(Nyangbingo.World.TilemapRenderer).GetField("foregroundTilemap", InstanceMembers)
                ?.SetValue(worldRenderer, foreground);
            worldRenderer.EnsureEdgeOverlayWiring();

            var overlay = typeof(Nyangbingo.World.TilemapRenderer)
                .GetField("edgeOverlayTilemap", InstanceMembers)?.GetValue(worldRenderer)
                as UnityEngine.Tilemaps.Tilemap;
            var shapes = typeof(Nyangbingo.World.TilemapRenderer)
                .GetField("edgeShapeTiles", InstanceMembers)?.GetValue(worldRenderer)
                as UnityEngine.Tilemaps.TileBase[];

            Require(overlay == null,
                "A scene with no edge-overlay art must not create a black runtime overlay.");
            Require(host.transform.Find("RuntimeEdgeOverlay") == null,
                "Missing edge-overlay wiring must remain disabled instead of adding a Tilemap.");
            Require(shapes != null && shapes.Length == TileEdgeOverlayResolver.ShapeCount,
                "The serialized edge-shape slots must keep their stable contract.");
            Require(Array.TrueForAll(shapes, shape => shape == null),
                "Disabled edge-overlay wiring must not allocate hidden one-pixel ink sprites.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    private static void TestCraftAndPlacementActionsRemainIndependent()
    {
        var source = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/UI/MainGameCraftingUiController.cs");
        Require(source.Contains("private void TryPlaceSelectedCraftingOutput()") &&
                source.Contains("collectButton.GetComponentInChildren<Text>().text = \"설치\"") &&
                source.Contains("primaryButton.GetComponentInChildren<Text>().text = \"E · 제작\"") &&
                !source.Contains("var isMissing = owned < ingredient.amount && !readyToPlace"),
            "Owned placeable products must expose a separate placement action without replacing or bypassing crafting requirements.");
    }

    private static void TestTreeVegetationVisualOffset()
    {
        var texture = new Texture2D(4, 8, TextureFormat.RGBA32, false);
        // Bottom-pivot sprite: feet sit on visible surface (logical top + drop visual offset).
        var bottomPivot = Sprite.Create(texture, new Rect(0, 0, 4, 8), new Vector2(.5f, 0f), 4f);
        // Center-pivot sprite: transform rises by extents so the visual foot still matches.
        var centerPivot = Sprite.Create(texture, new Rect(0, 0, 4, 8), new Vector2(.5f, .5f), 4f);
        const int surfaceY = 10;
        var visibleSurface = surfaceY + 1f + MainGameWorldDropRuntime.VisualSurfaceOffset;
        Require(Mathf.Approximately(
                MainGameWorldDecorationRenderer.ComputeSurfaceDecorationWorldY(surfaceY, bottomPivot),
                visibleSurface - bottomPivot.bounds.min.y),
            "Bottom-pivot vegetation must plant its sprite foot on the visible foreground surface.");
        Require(Mathf.Approximately(
                MainGameWorldDecorationRenderer.ComputeSurfaceDecorationWorldY(surfaceY, centerPivot),
                visibleSurface - centerPivot.bounds.min.y),
            "Center-pivot vegetation must raise the transform so the sprite foot still matches the surface.");
        Require(MainGameWorldDecorationRenderer.ComputeSurfaceDecorationWorldY(surfaceY, centerPivot) >
                MainGameWorldDecorationRenderer.ComputeSurfaceDecorationWorldY(surfaceY, bottomPivot),
            "Center-pivot plants must sit higher than bottom-pivot plants with the same visual foot line.");
        var source = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/World/MainGameWorldDecorationRenderer.cs");
        Require(source.Contains("ComputeSurfaceDecorationWorldY(surfaceY, art.Sprite)") &&
                !source.Contains("TreeVegetationVisualOffset"),
            "Surface vegetation must use pivot-aware placement instead of a fixed tree-only sink offset.");
    }

    private static void TestWorldDropVisualSurfaceOffset()
    {
        Require(Mathf.Approximately(MainGameWorldDropRuntime.VisualSurfaceOffset, .5f),
            "World-drop visuals must sit half a tile above the physics root to match the visible foreground surface.");
        Require(!MainGameWorldDropRuntime.DropToDropCollisionResponseEnabled,
            "World drops must not physically push one another after their initial reward fan-out.");
        var smallBatchDirection = MainGameWorldDropRuntime.CalculateLaunchDirection(0, 2);
        var largeBatchLeft = MainGameWorldDropRuntime.CalculateLaunchDirection(0, 12);
        var largeBatchRight = MainGameWorldDropRuntime.CalculateLaunchDirection(11, 12);
        Require(smallBatchDirection.x < 0f && largeBatchLeft.x < -.8f && largeBatchRight.x > .8f &&
                MainGameWorldDropRuntime.CalculateLaunchSpeed(12) >
                MainGameWorldDropRuntime.CalculateLaunchSpeed(2),
            "Larger reward batches must fan across both sides with greater launch speed.");
        var source = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/World/MainGameWorldDropRuntime.cs");
        Require(source.Contains("visual.transform.localPosition += Vector3.up * VisualSurfaceOffset"),
            "Delivered and placeholder item art must share the same surface-height correction.");
        Require(source.Contains("IgnoreCollisionWithExistingDrops(dropCollider)") &&
                source.Contains("Physics2D.IgnoreCollision(newDropCollider, existingCollider, true)"),
            "Every new world drop must ignore existing drop colliders while retaining terrain collision.");
        Require(source.Contains("WorldMobPhysicsBody.IgnoreCollisionWithActiveMobs(dropCollider)") &&
                source.Contains("GetComponentsInChildren<Collider2D>(true)"),
            "Drops must ignore every player, yokai, and boss collider while preserving position-based magnet pickup.");
    }

    private static void TestWorldMobPhysicsContract()
    {
        var globalsSource = System.IO.File.ReadAllText("Assets/Data/CSV/globals.csv");
        Require(!globalsSource.Contains("요괴 점프 추격 없어") &&
                globalsSource.Contains("지상형 요괴/보스의 1칸 추격 점프"),
            "Player jump data notes must not contradict the grounded yokai and boss step-jump contract.");

        Require(WorldMobPhysicsBody.ForYokai(Nyangbingo.Core.YokaiKind.ClubGoblin) ==
                    WorldMobLocomotion.Grounded &&
                WorldMobPhysicsBody.ForYokai(Nyangbingo.Core.YokaiKind.Bulgasari) ==
                    WorldMobLocomotion.Grounded &&
                WorldMobPhysicsBody.ForYokai(Nyangbingo.Core.YokaiKind.Yagwanggwi) ==
                    WorldMobLocomotion.Grounded &&
                WorldMobPhysicsBody.ForYokai(Nyangbingo.Core.YokaiKind.Eoduksini) ==
                    WorldMobLocomotion.Flying,
            "Ordinary yokai locomotion must match the latest ground/flying art and design contract.");
        Require(WorldMobPhysicsBody.ForBoss(Nyangbingo.Core.BossKind.GoblinChief) ==
                    WorldMobLocomotion.Grounded &&
                WorldMobPhysicsBody.ForBoss(Nyangbingo.Core.BossKind.MotherBulgasari) ==
                    WorldMobLocomotion.Grounded &&
                WorldMobPhysicsBody.ForBoss(Nyangbingo.Core.BossKind.Imugi) ==
                    WorldMobLocomotion.Flying &&
                WorldMobPhysicsBody.ForBoss(Nyangbingo.Core.BossKind.Gangcheori) ==
                    WorldMobLocomotion.Flying,
            "Boss locomotion must keep land bosses grounded and airborne dragons flying.");
        Require(Mathf.Approximately(WorldMobPhysicsBody.PhysicalRadiusForBoss(
                    Nyangbingo.Core.BossKind.GoblinChief), .65f) &&
                WorldMobPhysicsBody.PhysicalRadiusForBoss(Nyangbingo.Core.BossKind.Imugi) < .4f &&
                WorldMobPhysicsBody.PhysicalRadiusForBoss(Nyangbingo.Core.BossKind.Gangcheori) < .4f,
            "Flying bosses need a narrow movement core for one-cell passages while ground bosses retain their body radius.");
        Require(Mathf.Approximately(
                    WorldMobPhysicsBody.ColliderVerticalOffsetForBoss(
                        Nyangbingo.Core.BossKind.GoblinChief),
                    WorldMobPhysicsBody.PhysicalRadiusForBoss(
                        Nyangbingo.Core.BossKind.GoblinChief)) &&
                Mathf.Approximately(
                    WorldMobPhysicsBody.ColliderVerticalOffsetForBoss(
                        Nyangbingo.Core.BossKind.Imugi), 0f) &&
                WorldMobPhysicsBody.StepJumpVelocityForCollider(.65f) >
                WorldMobPhysicsBody.StepJumpVelocityForCollider(.42f),
            "Ground bosses must align their bottom-pivot art to the surface while flying bosses retain a centered movement core.");

        var groundObject = new GameObject("GroundMobPhysicsContract", typeof(CircleCollider2D),
            typeof(Rigidbody2D), typeof(WorldMobPhysicsBody));
        var flyingObject = new GameObject("FlyingMobPhysicsContract", typeof(CircleCollider2D),
            typeof(Rigidbody2D), typeof(WorldMobPhysicsBody));
        try
        {
            var ground = groundObject.GetComponent<WorldMobPhysicsBody>();
            var flying = flyingObject.GetComponent<WorldMobPhysicsBody>();
            ground.ConfigureForRuntime(WorldMobLocomotion.Grounded);
            flying.ConfigureForRuntime(WorldMobLocomotion.Flying);
            Require(groundObject.GetComponent<Rigidbody2D>().gravityScale > 0f &&
                    Mathf.Approximately(flyingObject.GetComponent<Rigidbody2D>().gravityScale, 0f) &&
                    !groundObject.GetComponent<Collider2D>().isTrigger &&
                    !flyingObject.GetComponent<Collider2D>().isTrigger &&
                    Mathf.Approximately(ground.NavigationOffset(new Vector2(2f, 3f)).y, 0f) &&
                    flying.NavigationOffset(new Vector2(2f, 3f)) == new Vector2(2f, 3f),
                "Ground mobs must use gravity and horizontal navigation; flying mobs must stay gravity-free while retaining solid collision.");
            Require(Physics2D.GetIgnoreCollision(groundObject.GetComponent<Collider2D>(),
                    flyingObject.GetComponent<Collider2D>()),
                "Yokai and bosses must ignore mutual physical response so faster mobs are never slowed by the mob ahead.");

            var stepCells = new TileData[8, 6];
            for (var x = 0; x < 8; x++)
                stepCells[x, 0] = new TileData { elementType = WorldTileTypes.Stone, hardness = 1 };
            stepCells[3, 1] = new TileData { elementType = WorldTileTypes.Stone, hardness = 1 };
            var stepTiles = new TileService(stepCells, null, null, 3);
            var groundBody = groundObject.GetComponent<Rigidbody2D>();
            groundObject.transform.position = new Vector3(2.5f, 1.5f, 0f);
            groundBody.position = new Vector2(2.5f, 1.5f);
            groundBody.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
            ground.ConfigureForRuntime(WorldMobLocomotion.Grounded, stepTiles);
            ground.Move(Vector2.right * .25f);
            Require(groundBody.linearVelocity.y > 8f,
                "A grounded yokai or boss must jump when pursuing across a clear one-tile step.");

            ground.SetEncounterPaused(true);
            Require(!groundObject.GetComponent<Rigidbody2D>().simulated,
                "Regular yokai paused for a boss encounter must leave physics simulation so they cannot block the boss.");
            ground.SetEncounterPaused(false);
            Require(groundObject.GetComponent<Rigidbody2D>().simulated,
                "Regular yokai must restore physics simulation when the boss encounter ends.");

            var passThroughPlayer = new GameObject("MobPassThroughPlayer", typeof(CircleCollider2D));
            var playerCollider = passThroughPlayer.GetComponent<CircleCollider2D>();
            ground.IgnoreCollisionWith(passThroughPlayer.transform);
            Require(Physics2D.GetIgnoreCollision(groundObject.GetComponent<Collider2D>(), playerCollider),
                "Player and mob colliders must ignore physical response so player movement cannot push yokai or bosses.");
            UnityEngine.Object.DestroyImmediate(passThroughPlayer);

            var navigationCells = new TileData[8, 6];
            for (var x = 0; x <= 5; x++)
                navigationCells[x, 2] = new TileData { elementType = WorldTileTypes.Stone, hardness = 1 };
            var navigationTiles = new TileService(navigationCells, null, null, 1);
            var flyingBody = flyingObject.GetComponent<Rigidbody2D>();
            flyingObject.transform.position = new Vector3(1.5f, 3.5f, 0f);
            flyingBody.position = new Vector2(1.5f, 3.5f);
            flyingBody.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
            flying.ConfigureForRuntime(WorldMobLocomotion.Flying, navigationTiles);
            var detourDirection = flying.NavigationDirection(new Vector2(0f, -2f));
            Require(detourDirection.x > .5f && Mathf.Abs(detourDirection.y) < .5f,
                "A flying yokai blocked by terrain must route toward the nearest opening instead of pushing into the direct wall.");

            var ledgeCells = new TileData[8, 6];
            for (var x = 2; x < 8; x++)
                ledgeCells[x, 2] = new TileData { elementType = WorldTileTypes.Stone, hardness = 1 };
            var ledgeTiles = new TileService(ledgeCells, null, null, 2);
            flyingObject.transform.position = new Vector3(1.5f, 3.05f, 0f);
            flyingBody.position = new Vector2(1.5f, 3.05f);
            flyingBody.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
            flying.ConfigureForRuntime(WorldMobLocomotion.Flying, ledgeTiles);
            var ledgeDirection = flying.NavigationDirection(new Vector2(2f, .45f));
            Require(ledgeDirection.y > .9f && Mathf.Abs(ledgeDirection.x) < .1f,
                "A flying yokai below a ledge must rise to its current cell center before turning across the ledge.");

            var separatedTargetObject = new GameObject("VerticallySeparatedYokaiTarget");
            var separatedTarget = separatedTargetObject.AddComponent<Nyangbingo.Debugging.DevBTestYokaiTarget>();
            separatedTargetObject.transform.position = new Vector3(1.5f, 3.5f, 0f);
            groundObject.transform.position = new Vector3(1.5f, 1.5f, 0f);
            groundBody.position = new Vector2(1.5f, 1.5f);
            groundBody.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
            ground.ConfigureForRuntime(WorldMobLocomotion.Grounded, navigationTiles);
            var separatedDefinition = YokaiDefinition.CreateRuntime(Nyangbingo.Core.YokaiKind.ClubGoblin,
                10, 1f, 1, 5f, Array.Empty<ItemAmount>());
            var separatedBrain = groundObject.AddComponent<Nyangbingo.Yokai.YokaiBrain>();
            separatedBrain.ConfigureForRuntime(separatedDefinition, separatedTarget);
            separatedBrain.Tick(1f);
            separatedBrain.Tick(1f);
            Require(Mathf.Approximately(separatedTarget.WallDamageReceived, 0f),
                "A grounded yokai must not attack a target on another vertical level or through foreground terrain.");
            UnityEngine.Object.DestroyImmediate(separatedTargetObject);
            UnityEngine.Object.DestroyImmediate(separatedDefinition);

            var encounterSource = System.IO.File.ReadAllText(
                "Assets/Scripts/Nyangbingo/World/MainGameEncounterCoordinator.cs");
            Require(encounterSource.Contains("AddComponent<WorldMobPhysicsBody>()") &&
                    encounterSource.Contains("WorldMobPhysicsBody.ForYokai(definition.Kind)") &&
                    encounterSource.Contains("WorldMobPhysicsBody.ForBoss(definition.Kind)") &&
                    encounterSource.Contains("bootstrap.TileService") &&
                    encounterSource.Contains("new GameObject(\"BossHurtbox\")") &&
                    encounterSource.Contains(
                        "locomotion == WorldMobLocomotion.Flying ? BossScale : 1f") &&
                    encounterSource.Contains("ConfigureDetachedHurtboxBody"),
                "MainGame encounter spawning must attach the shared world physics body to yokai and bosses.");
            var animatorSource = System.IO.File.ReadAllText(
                "Assets/Scripts/Nyangbingo/Yokai/YokaiBrain.cs");
            Require(animatorSource.Contains("characterAnimator?.SetMoving(true)") &&
                    animatorSource.Contains("characterAnimator?.SetFacing(ResolveFacingDirection(displacement))"),
                "Physics-driven yokai movement must explicitly keep walk and flight animation state active.");
            var physicsSource = System.IO.File.ReadAllText(
                "Assets/Scripts/Nyangbingo/World/WorldMobPhysicsBody.cs");
            Require(physicsSource.Contains("NavigationReversalHoldSeconds") &&
                    physicsSource.Contains("PathTargetCellTolerance") &&
                    physicsSource.Contains("targetActuallyCrossedBehind") &&
                    physicsSource.Contains("DirectPathConfirmationSeconds"),
                "Moving targets must not cause equal detours to alternate every frame, while real target crossings still reverse pursuit immediately.");
            Require(animatorSource.Contains("ResolveFacingDirection") &&
                    animatorSource.Contains("targetOffset.magnitude > 1.5f"),
                "A distant moving target must own horizontal facing so temporary detours do not visibly flip yokai every frame.");
            var imugiBodySource = System.IO.File.ReadAllText(
                "Assets/Scripts/Nyangbingo/World/RuntimeImugiBodyVisual.cs");
            Require(imugiBodySource.Contains("RigidbodyType2D.Kinematic") &&
                    imugiBodySource.Contains("EnsureDetachedBody") &&
                    imugiBodySource.Contains("segmentWorldPositions") &&
                    imugiBodySource.Contains("currentDistance > desiredDistance") &&
                    imugiBodySource.Contains("facing = Vector2.right"),
                "Imugi body hurtboxes must use detached kinematic bodies and follow the prior world-space trail instead of flipping instantly.");
            Require(encounterSource.Contains("definition.Kind == BossKind.Imugi") &&
                    encounterSource.Contains("characterAnimator.SetFacing(Vector2.right)"),
                "Imugi must spawn facing right with its body initialized behind the head.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(groundObject);
            UnityEngine.Object.DestroyImmediate(flyingObject);
        }
    }

    private static void TestLatestProductFlowContracts()
    {
        Require(GameShellController.ShouldEndDemoAtDawn(31, 30) &&
                !GameShellController.ShouldEndDemoAtDawn(30, 30) &&
                !GameShellController.ShouldEndDemoAtDawn(31, 0),
            "The day-30 demo must end at the following dawn regardless of the Gangcheol outcome.");

        var save = new SaveGame
        {
            sealPct = 87.5f,
            modulesDone = new System.Collections.Generic.List<string>
                { "module_a", "module_b", "module_a" },
            bossRecords = new System.Collections.Generic.List<BossRecord>
                { new BossRecord { bossId = "gangcheol_boss", count = 1, firstDay = 30 } },
            dogam = new System.Collections.Generic.List<CodexRecord>
                { new CodexRecord { yokaiId = "a", kills = 2 }, new CodexRecord { yokaiId = "b", kills = 3 } },
            stats = new RunStatsRecord { minedTiles = 41, deaths = 2 }
        };
        var result = GameShellController.BuildResult(save);
        Require(Mathf.Approximately(result.SealPercentage, 87.5f) &&
                result.CompletedModuleIds.Count == 2 && result.GangcheolDefeated &&
                result.YokaiKills == 5 && result.MinedTiles == 41 && result.Deaths == 2,
            "The demo result must include seal, unique modules, Gangcheol outcome, kills, mining, and deaths.");

        var shellSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/UI/MainGameShellUiController.cs");
        Require(shellSource.Contains("pauseSaveButton = saveButtons[0]") &&
                shellSource.Contains("pauseSaveButton.onClick.AddListener(SaveCurrentProgress)") &&
                shellSource.Contains("pauseSaveButton.interactable = bossManager == null || !bossManager.IsBossActive") &&
                shellSource.Contains("RemoveLegacySaveSlotObjects()") &&
                shellSource.Contains("loadButtons[index].gameObject.SetActive(false)"),
            "Pause must expose one current-slot save action, hide legacy load slots, and lock saving during bosses.");

        var craftingSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/UI/MainGameCraftingUiController.cs");
        Require(craftingSource.Contains("FindBossForSummonItem(item.Id)") &&
                craftingSource.Contains("CanUseSummonItem(item.Id") &&
                craftingSource.Contains("OpenSummonConfirmation(summonBoss)") &&
                craftingSource.Contains("stationSource.TryUseSummonItem(itemId)"),
            "Boss summon items must be selected in inventory, validated, confirmed, and consumed through product flow.");
    }

    private static void TestDemoSafeSpawnRestorePolicy()
    {
        Require(MainGameSaveCoordinator.ShouldResolveSafePlayerSpawn(true, true) &&
                MainGameSaveCoordinator.ShouldResolveSafePlayerSpawn(true, false),
            "Official demo restore must always recalculate the shared safe surface spawn.");
        Require(!MainGameSaveCoordinator.ShouldResolveSafePlayerSpawn(false, true) &&
                !MainGameSaveCoordinator.ShouldResolveSafePlayerSpawn(false, false),
            "Regular saves must retain their exact positions, including airborne or non-standing positions.");
        var shellSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/UI/MainGameShellUiController.cs");
        Require(shellSource.Contains("saveCoordinator.TryApplyDemoSnapshot(demo)"),
            "The title demo buttons must use the demo-specific safe-spawn restore path.");
    }

    private static void TestWorldCellCoordinateContract()
    {
        var gridObject = new GameObject("WorldCellCoordinateContract", typeof(Grid));
        var tilemapObject = new GameObject("Foreground", typeof(UnityEngine.Tilemaps.Tilemap),
            typeof(UnityEngine.Tilemaps.TilemapRenderer));
        tilemapObject.transform.SetParent(gridObject.transform, false);
        var backgroundObject = new GameObject("Background", typeof(UnityEngine.Tilemaps.Tilemap),
            typeof(UnityEngine.Tilemaps.TilemapRenderer));
        backgroundObject.transform.SetParent(gridObject.transform, false);
        gridObject.transform.position = new Vector3(3.25f, -2.5f, 0f);
        gridObject.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
        var tilemap = tilemapObject.GetComponent<UnityEngine.Tilemaps.Tilemap>();
        var renderer = gridObject.AddComponent<Nyangbingo.World.TilemapRenderer>();
        var foregroundField = typeof(Nyangbingo.World.TilemapRenderer)
            .GetField("foregroundTilemap", InstanceMembers);
        var backgroundField = typeof(Nyangbingo.World.TilemapRenderer)
            .GetField("backgroundTilemap", InstanceMembers);
        Require(foregroundField != null, "TilemapRenderer foreground binding field is missing.");
        Require(backgroundField != null, "TilemapRenderer background binding field is missing.");
        foregroundField.SetValue(renderer, tilemap);
        var backgroundTilemap = backgroundObject.GetComponent<UnityEngine.Tilemaps.Tilemap>();
        backgroundField.SetValue(renderer, backgroundTilemap);
        try
        {
            var cell = new Vector3Int(4, 7, 0);
            var center = renderer.GetCellCenterWorld(cell);
            Require(renderer.WorldToCell(center) == cell,
                "Tilemap cell center and world-to-cell conversion must be exact inverses.");
            var corners = new Vector3[4];
            renderer.GetCellWorldCorners(cell, corners);
            Require(Vector3.Distance((corners[0] + corners[2]) * .5f, center) < .0001f,
                "Seal marker corners must share the authoritative Tilemap cell center.");

            var foregroundTile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
            var backgroundTile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
            foregroundTile.colliderType = UnityEngine.Tilemaps.Tile.ColliderType.Grid;
            backgroundTile.colliderType = UnityEngine.Tilemaps.Tile.ColliderType.None;
            renderer.SetTileVisualsForEditorSetup(new[]
            {
                new Nyangbingo.World.TilemapRenderer.TileVisual
                    { elementType = WorldTileTypes.Dirt, tile = foregroundTile },
                new Nyangbingo.World.TilemapRenderer.TileVisual
                    { elementType = WorldTileTypes.BackgroundDirt, tile = backgroundTile }
            }, foregroundTile);
            renderer.RebuildLookupTable();
            renderer.EnsureForegroundCollision();
            tilemap.SetTile(cell, foregroundTile);
            backgroundTilemap.SetTile(cell, backgroundTile);
            renderer.NotifyForegroundCollisionDirty();
            var foregroundCollider = tilemap.GetComponent<CompositeCollider2D>();
            Require(foregroundCollider != null && foregroundCollider.OverlapPoint(center),
                "Foreground test tile must create physical collision before mining.");
            var tiles = new TileData[10, 10];
            tiles[cell.x, cell.y] = TileData.CreateNaturalWithBackground(
                WorldTileTypes.Dirt, 1, WorldTileTypes.BackgroundDirt);
            var tileService = new TileService(tiles, renderer, null, 1);
            Require(tileService.TryBreakForeground(cell, 1, out _, out _),
                "Foreground dirt test fixture could not be mined.");
            Require(tilemap.GetTile(cell) == null,
                "Mining must clear the authoritative foreground Tilemap cell.");
            Require(!foregroundCollider.OverlapPoint(center),
                "Mining must remove CompositeCollider geometry in the same frame.");
            Require(backgroundTilemap.GetTile(cell) != null,
                "Mining must retain the independent natural background wall.");
            UnityEngine.Object.DestroyImmediate(foregroundTile);
            UnityEngine.Object.DestroyImmediate(backgroundTile);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gridObject);
        }
    }

    private static void TestNarrativeFreeProductHudContract()
    {
        Require(!MainGameHudController.ProductHudNarrativeTextEnabled &&
                !MainGameTurretRuntime.ProductHudNarrativeTextEnabled &&
                !MainGameTilePaletteController.ProductHudNarrativeTextEnabled,
            "Product HUD controllers must keep narrative text disabled.");

        var hudSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/UI/MainGameHudController.cs");
        var turretSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/World/MainGameTurretRuntime.cs");
        var paletteSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/UI/MainGameTilePaletteController.cs");
        var playerSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/World/MainGamePlayerController.cs");
        Require(!hudSource.Contains("피격!") &&
                !hudSource.Contains("방울 금줄 경보 · 침입자 접근") &&
                !hudSource.Contains("sealText.text = $\"석빙고") &&
                !hudSource.Contains("제작 중 ·") &&
                !hudSource.Contains("bossStatusText.text = $\"HP"),
            "Narrative text was reintroduced into the product HUD.");
        Require(!turretSource.Contains("장독 창고 · 40슬롯") &&
                !turretSource.Contains("좌클릭 설치 · ESC/우클릭 취소"),
            "Narrative interaction instructions were reintroduced into the world HUD.");
        Require(!turretSource.Contains("TryPlace(record, barrierActive: false)"),
            "Whitelisted insulation modules must remain eligible to seal after product placement.");
        Require(!paletteSource.Contains("R · 반경 표시"),
            "Narrative range-toggle status was reintroduced into the tile palette HUD.");
        Require(playerSource.Contains("MainGameHudController.BlocksWorldPrimaryInput"),
            "Clicking the top-right seal thermometer must block claw attacks and mining input.");
        Require(playerSource.Contains(": TryOpenNearbyChest() ||") &&
                !System.Text.RegularExpressions.Regex.IsMatch(playerSource,
                    @"GetMouseButtonDown\(1\)[\s\S]{0,120}TryOpenNearbyChest"),
            "Chest interaction must remain on E while right-click stays exclusive to the fan ability.");
    }

    private static void TestBossHealthArtMapping()
    {
        Require(!MainGameHudController.ProductBossHealthTextEnabled &&
                MainGameHudController.BossHealthBarBelowClockY < 0f &&
                MainGameHudController.BossHealthBarBelowClockY >
                    -(MainGameHudController.DayCounterClockHeight +
                      MainGameHudController.DayCounterExpandedHeight) &&
                Mathf.Approximately(MainGameHudController.BossHealthBarWidth, 192f) &&
                Mathf.Approximately(MainGameHudController.BossHealthBarHeight, 48f) &&
                Mathf.Approximately(MainGameHudController.BossHealthSegmentHeight, 7.5f) &&
                Mathf.Approximately(MainGameHudController.BossHealthValueGlyphScale, .5f) &&
                Mathf.Approximately(MainGameHudController.BossHealthValueVerticalNudge, -.5f) &&
                MainGameHudController.FormatBossCurrentHealth(13800) == "13800" &&
                MainGameHudController.FormatBossCurrentHealth(-1) == "0" &&
                Mathf.Approximately(MainGameHudController.BossHealthContentVerticalOffset("king_dokkaebi"),
                    -4.125f) &&
                Mathf.Approximately(MainGameHudController.BossHealthContentVerticalOffset("mother_bulgasari"),
                    -6.75f) &&
                Mathf.Approximately(MainGameHudController.BossHealthContentVerticalOffset("imugi"), -6f) &&
                Mathf.Approximately(MainGameHudController.BossHealthContentVerticalOffset("gangcheol_boss"),
                    -7.5f) &&
                Mathf.Approximately(MainGameHudController.BossHealthValueScale("imugi"), .85f) &&
                Mathf.Approximately(MainGameHudController.BossHealthValueScale("king_dokkaebi"), 1f),
            "The illustrated boss health bar must use the enlarged upper-center HUD layout.");
        var worldHealthBarSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/World/MainGameEncounterCoordinator.cs");
        Require(!worldHealthBarSource.Contains("new GameObject(\"Value\")") &&
                !worldHealthBarSource.Contains("TextMesh valueText") &&
                !worldHealthBarSource.Contains("TextMesh valueShadow"),
            "Regular yokai health bars must not render current-health text.");
        Require(MainGameHudController.BossHealthArtRow("king_dokkaebi") == 0,
            "King Dokkaebi must use the first Unity texture row of the boss health sheet.");
        Require(MainGameHudController.BossHealthArtRow("mother_bulgasari") == 1,
            "Mother Bulgasari must use the second Unity texture row of the boss health sheet.");
        Require(MainGameHudController.BossHealthArtRow("imugi") == 2,
            "Imugi must use the third Unity texture row of the boss health sheet.");
        Require(MainGameHudController.BossHealthArtRow("gangcheol_boss") == 3,
            "Gangcheol must use the fourth Unity texture row of the boss health sheet.");
        Require(MainGameHudController.BossHealthArtRow("unknown") == -1,
            "Unknown bosses must not inherit another boss health frame.");

        var characterCatalog = AssetDatabase.LoadAssetAtPath<CharacterArtCatalog>(
            "Assets/Art/Characters/CharacterArtCatalog.asset");
        var kingEntry = characterCatalog != null ? characterCatalog.Find("king_dokkaebi") : null;
        Require(kingEntry != null && kingEntry.SpecialFrames.Count == 5 &&
                kingEntry.SpecialFrames[0] != null && kingEntry.SpecialFrames[0].name == "Frame_12" &&
                kingEntry.SpecialFrames[1] != null && kingEntry.SpecialFrames[1].name == "Frame_13" &&
                kingEntry.SpecialFrames[2] != null && kingEntry.SpecialFrames[2].name == "Frame_14" &&
                kingEntry.SpecialFrames[3] != null && kingEntry.SpecialFrames[3].name == "Frame_15" &&
                kingEntry.SpecialFrames[4] != null && kingEntry.SpecialFrames[4].name == "Frame_16",
            "King Dokkaebi special attacks must play the delivered Frame_12 through Frame_16 sequence.");

        var animatorObject =
            new GameObject("KingSpecialAnimationPriority", typeof(SpriteRenderer),
                typeof(RuntimeCharacterSpriteAnimator));
        try
        {
            var animator = animatorObject.GetComponent<RuntimeCharacterSpriteAnimator>();
            var renderer = animatorObject.GetComponent<SpriteRenderer>();
            animator.Configure(kingEntry, 0);
            typeof(RuntimeCharacterSpriteAnimator).GetMethod("PlaySpecial", InstanceMembers)
                ?.Invoke(animator, null);
            var specialOpeningFrame = renderer.sprite;
            animator.PlayAttack();
            Require(specialOpeningFrame != null && renderer.sprite == specialOpeningFrame &&
                    renderer.sprite.name == "Frame_12",
                "A contact attack event must not overwrite King Dokkaebi's active special animation.");
            animator.AlignActionImpactFrame(3);
            Require(renderer.sprite != null && renderer.sprite.name == "Frame_15",
                "King Dokkaebi's damaging special frame must align exactly with Frame_15.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(animatorObject);
        }

        var motherEntry =
            characterCatalog != null ? characterCatalog.Find("mother_bulgasari") : null;
        var motherAnimatorObject =
            new GameObject("MotherSpecialAttackImpact", typeof(SpriteRenderer),
                typeof(RuntimeCharacterSpriteAnimator));
        try
        {
            Require(motherEntry != null && motherEntry.AttackFrames.Count == 2 &&
                    motherEntry.AttackFrames[1] != null &&
                    motherEntry.AttackFrames[1].name == "Frame_8",
                "Mother Bulgasari's raised-nose attack pose must remain bound to Frame_8.");
            var animator = motherAnimatorObject.GetComponent<RuntimeCharacterSpriteAnimator>();
            var renderer = motherAnimatorObject.GetComponent<SpriteRenderer>();
            animator.Configure(motherEntry, 0);
            animator.PlayAttack();
            animator.AlignActionImpactFrame(1);
            Require(renderer.sprite != null && renderer.sprite.name == "Frame_8",
                "Each Mother Bulgasari special damage tick must align with the raised-nose frame.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(motherAnimatorObject);
        }

        var gangcheoriBody =
            characterCatalog != null ? characterCatalog.FindSprite("gangcheol_body") : null;
        Require(gangcheoriBody != null &&
                AssetDatabase.GetAssetPath(gangcheoriBody) ==
                "Assets/Art/Characters/gangcheol_body.png" &&
                gangcheoriBody.texture.width == 8 && gangcheoriBody.texture.height == 8,
            "Gangcheori must bind the delivered 8x8 body art from the latest resource package.");
        var encounterSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/World/MainGameEncounterCoordinator.cs");
        Require(encounterSource.Contains("RuntimeGangcheoriBodyVisual") &&
                encounterSource.Contains("FindSprite(\"gangcheol_body\")"),
            "The Gangcheori boss must compose its delivered body segments behind the head.");
        var gameplayCatalog = AssetDatabase.LoadAssetAtPath<GameplayArtCatalog>(
            "Assets/Art/Gameplay/GameplayArtCatalog.asset");
        Require(gameplayCatalog != null &&
                gameplayCatalog.GangcheoriSpecialFireFrames.Count == 4,
            "Gangcheori must bind all four delivered 0.1-second fire effect frames.");
        var bossCombatSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/Bosses/BossCombatController.cs");
        Require(bossCombatSource.Contains(
                    "SetTelegraphVisible(definition.Kind != BossKind.Gangcheori)") &&
                bossCombatSource.Contains(
                    "SetSpecialEffectVisible(definition.Kind == BossKind.Gangcheori)"),
            "Gangcheori's warning must hand off directly to the active fire effect.");
    }

    private static void TestDayNightCountdownFormatting()
    {
        Require(MainGameHudController.FormatRemainingTime(540f) == "09:00",
            "A full night must be displayed as 09:00.");
        Require(MainGameHudController.FormatRemainingTime(0.1f) == "00:01",
            "The countdown must use ceiling seconds so it does not show 00:00 before transition.");
        Require(MainGameHudController.FormatRemainingTime(-1f) == "00:00",
            "Negative remaining time must be clamped to 00:00.");
        var environment = AssetDatabase.LoadAssetAtPath<EnvironmentArtCatalog>(
            "Assets/Art/Backgrounds/EnvironmentArtCatalog.asset");
        Require(environment != null && environment.DayCounterScrollFrames.Count == 10,
            "The delivered 10-frame scroll animation must be bound to the in-game day counter.");
        Require(RuntimeDayCounterScrollPresenter.DeliveredPixelToLogicalScale > 0f &&
                RuntimeDayCounterScrollPresenter.DeliveredPixelToLogicalScale < 1f,
            "The day-counter scroll must be reduced from delivered pixel size without PPU inflation.");
        Require(MainGameHudController.DayCounterFontSize > MainGameHudController.DayCounterClockFontSize &&
                MainGameHudController.DayCounterExpandedHeight > 0f &&
                MainGameHudController.DayCounterClockHeight > 0f &&
                MainGameHudController.DayCounterClockGap >= 0f,
            "The clock and D-day scroll stack must retain valid delivered-art dimensions.");
        var clockPosition = Vector2.zero;
        var dayCounterPosition = MainGameHudController.ResolveDayCounterPositionBelowClock(clockPosition);
        Require(dayCounterPosition.y < clockPosition.y &&
                Mathf.Approximately(dayCounterPosition.y,
                    -(MainGameHudController.DayCounterClockHeight +
                      MainGameHudController.DayCounterClockGap)),
            "The clock must sit at the top with the D-day scroll immediately below it.");
        Require(MainGameHudController.SealDiagnosticHoldSeconds >= .5f &&
                MainGameHudController.FormatSealDelta(.4f) == "+0.4%" &&
                MainGameHudController.FormatSealDelta(-.4f) == "-0.4%",
            "Seal diagnostics must require a deliberate hold and show symbol-only percentage deltas.");
        Require(MainGameHudController.BaekjungDayCounterBorderPixels > 0f &&
                MainGameHudController.ShouldShowBaekjungDayCounterFeedback(true, false, false) &&
                !MainGameHudController.ShouldShowBaekjungDayCounterFeedback(true, true, false) &&
                !MainGameHudController.ShouldShowBaekjungDayCounterFeedback(true, false, true) &&
                !MainGameHudController.ShouldShowBaekjungDayCounterFeedback(false, false, false),
            "The Baekjung D-counter border must appear only before a boss is summoned.");
        Require(MainGameHudController.BossFleeRollSeconds > 0f &&
                Mathf.Approximately(MainGameHudController.CalculateBossFleeRollScale(
                    MainGameHudController.BossFleeRollSeconds, MainGameHudController.BossFleeRollSeconds), 1f) &&
                Mathf.Approximately(MainGameHudController.CalculateBossFleeRollScale(0f,
                    MainGameHudController.BossFleeRollSeconds), 0f),
            "A dawn-fleeing boss health scroll must roll from full width to zero width.");
        Require(MainGameHudController.ResolveDayNightClockFrameIndex(0f, 1440f, 6) == 5 &&
                MainGameHudController.ResolveDayNightClockFrameIndex(1439f, 1440f, 6) == 0 &&
                MainGameHudController.ResolveDayNightClockFrameIndex(0f, 0f, 6) == -1 &&
                MainGameHudController.ShouldShowNightSpawnLock(true, true, false) &&
                MainGameHudController.ShouldShowNightSpawnLock(true, false, true) &&
                !MainGameHudController.ShouldShowNightSpawnLock(true, false, false) &&
                !MainGameHudController.ShouldShowNightSpawnLock(false, true, true),
            "The six-frame day/night clock and narrative-free night spawn lock must follow runtime state.");
        var presenterSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/UI/RuntimeUiSpriteAnimator.cs");
        var hudSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/UI/MainGameHudController.cs");
        Require(presenterSource.Contains("IsFullyOpen => phase == PlaybackPhase.Holding") &&
                presenterSource.Contains("PlayDayChange(int daysRemaining)") &&
                presenterSource.Contains("PresentationCompleted?.Invoke()") &&
                hudSource.Contains("TimeService.Dawn += HandleDayCounterDawn") &&
                hudSource.Contains("scrollObject.SetActive(false)"),
            "The D-day scroll must stay hidden and play one open/show/close cycle only at dawn.");
        var shellSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/UI/MainGameShellUiController.cs");
        Require(!shellSource.Contains("animator.ConfigureForScene(environmentArtCatalog.TitleFrames"),
            "The day-counter scroll animation must not be reused as the title logo.");
    }

    private static void TestV29InventoryLayoutContract()
    {
        Require(Inventory.SlotCount == 50,
            $"v29 inventory capacity must be 50 slots (actual {Inventory.SlotCount}).");
        Require(MainGameCraftingUiController.InventoryGridColumns == 10,
            $"v29 inventory grid must have 10 columns (actual {MainGameCraftingUiController.InventoryGridColumns}).");
        Require(MainGameCraftingUiController.InventoryGridRows == 5,
            $"v29 inventory grid must have 5 rows (actual {MainGameCraftingUiController.InventoryGridRows}).");
        Require(Mathf.Approximately(MainGameCraftingUiController.InventorySlotPixelSize, 27f),
            $"v29 inventory slot art must render at 27 px (actual {MainGameCraftingUiController.InventorySlotPixelSize}).");
        Require(MainGameCraftingUiController.UsesIconOnlyCraftingList,
            "v28 crafting list must use icon and quantity presentation without narrative row text.");
        Require(MainGameBossSummonUiController.DebugShortcutHelpKey == KeyCode.F5,
            "MainGame Editor test shortcut help must be assigned to F5.");
        Require(MainGameCraftingUiController.UnifiedTabHotkey(0) == KeyCode.F1 &&
                MainGameCraftingUiController.UnifiedTabHotkey(1) == KeyCode.F2 &&
                MainGameCraftingUiController.UnifiedTabHotkey(2) == KeyCode.F3 &&
                MainGameCraftingUiController.UnifiedTabHotkey(3) == KeyCode.F4 &&
                MainGameCraftingUiController.UnifiedTabHotkey(4) == KeyCode.None,
            "The four unified panels must be assigned to F1 through F4.");
        Require(MainGameCraftingUiController.DebugGrantRequirementsKey == KeyCode.F5,
            "Crafting test grants must share modified F5 without reclaiming the F1-F4 product panel keys.");
        Require(MainGameBossSummonUiController.DebugShortcutHelpPanelSize.x <=
                    MainGameUiResolutionController.LogicalResolution.x &&
                MainGameBossSummonUiController.DebugShortcutHelpPanelSize.y <=
                    MainGameUiResolutionController.LogicalResolution.y &&
                MainGameBossSummonUiController.DebugShortcutHelpBodyFontSize <= 8,
            "The F5 help popup must use native 480x270 coordinates instead of legacy 1920x1080 sizing.");
        Require(MainGameCraftingUiController.SupportsDebugInstantCompletion,
            "The Editor must expose the crafting and smelting instant-completion test control.");

        var inventory = new Inventory(_ => null);
        Require(inventory.Capacity == 50 && inventory.Slots.Count == 50,
            $"A default runtime inventory must allocate 50 slots (actual {inventory.Capacity}).");
    }

    private static void TestV29InventoryArtBindings()
    {
        const string catalogPath = "Assets/Art/Gameplay/GameplayArtCatalog.asset";
        var catalog = AssetDatabase.LoadAssetAtPath<GameplayArtCatalog>(catalogPath);
        Require(catalog != null, $"Gameplay art catalog not found: {catalogPath}");

        Require(catalog.InventoryPanel != null, "v29 inventory panel art is not bound.");
        Require(catalog.InventorySlot != null, "v29 inventory slot art is not bound.");
        Require(catalog.InventorySlotSelected != null, "v29 selected inventory slot art is not bound.");
        Require(catalog.InventorySlotTopSelected != null, "v29 top selected inventory slot art is not bound.");
        Require(catalog.EquipmentCharacter != null, "v29 equipment character art is not bound.");
        Require(catalog.EquipmentHeadSlot != null, "v29 equipment head slot art is not bound.");
        Require(catalog.EquipmentBodySlot != null && catalog.EquipmentBodySlotSelected != null,
            "v29 equipment body slot art is incomplete.");
        Require(catalog.EquipmentFeetSlot != null && catalog.EquipmentFeetSlotSelected != null,
            "v29 equipment feet slot art is incomplete.");
        Require(catalog.EquipmentAccessorySlot != null && catalog.EquipmentAccessorySlotSelected != null,
            "v29 equipment accessory slot art is incomplete.");
        Require(catalog.ActiveItemSlot != null && catalog.ActiveItemSlotSelected != null,
            "v29 active item slot art is incomplete.");
        Require(catalog.PlayerVitalsFrames.Count == 12,
            $"v1 player vitals art must expose 12 frames (actual {catalog.PlayerVitalsFrames.Count}).");
        for (var index = 0; index < catalog.PlayerVitalsFrames.Count; index++)
            Require(catalog.PlayerVitalsFrames[index] != null,
                $"v1 player vitals frame {index} is not bound.");
        Require(catalog.TemperatureFrames.Count == 12,
            $"The seal thermometer must expose 12 color frames (actual {catalog.TemperatureFrames.Count}).");
    }

    private static void TestTilePaletteContract()
    {
        Require(Mathf.Approximately(MainGameTilePaletteController.MaxScreenWidthRatio, .5f),
            "The tile palette must stay within 50% of the screen width.");
        Require(Mathf.Approximately(MainGameTilePaletteController.PaletteLogicalWidth, 240f),
            "A 480 px logical canvas must use a 240 px tile palette.");
        Require(Mathf.Approximately(MainGameTilePaletteController.SlotPixelSize, 27f),
            "The tile palette must reuse the delivered 27 px inventory slot scale.");
        Require(MainGameTilePaletteController.ShortcutSlotCount == 8 &&
                MainGameTilePaletteController.ShortcutKeyForSlot(0) == KeyCode.Alpha1 &&
                MainGameTilePaletteController.ShortcutKeyForSlot(7) == KeyCode.Alpha8 &&
                MainGameTilePaletteController.ShortcutKeyForSlot(8) == KeyCode.None,
            "The eight visible tile-palette slots must be assigned to number keys 1 through 8.");
        var paletteSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/UI/MainGameTilePaletteController.cs");
        Require(paletteSource.Contains("TrySelectPaletteSlot(shortcutSlot)") &&
                 paletteSource.Contains("CollectHotbarSlotItemIds()") &&
                 paletteSource.Contains("SelectEmptySlot(slotIndex)"),
            "Number keys 1-8 must select inventory hotbar slots, including empty slots.");
        Require(!paletteSource.Contains("!MainGameShellUiController.IsLoadingTransitionActive"),
            "The tile palette must remain in the gameplay HUD beneath the shell loading overlay.");
        var shellSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/UI/MainGameShellUiController.cs");
        Require(MainGameShellUiController.ShellLoadingSortingOrder == 32700 &&
                shellSource.Contains("overlayCanvas.overrideSorting = true") &&
                shellSource.Contains("overlayCanvas.sortingOrder = ShellLoadingSortingOrder"),
            "The shell loading transition must use a dedicated topmost canvas independent of HUD creation order.");
        Require(TileService.SupportsForegroundPlacement(WorldTileTypes.Dirt) &&
                TileService.SupportsForegroundPlacement(WorldTileTypes.Stone) &&
                TileService.SupportsForegroundPlacement("insul_wall") &&
                TileService.SupportsForegroundPlacement("iron_insul_wall") &&
                TileService.SupportsForegroundPlacement("roof") &&
                TileService.SupportsForegroundPlacement("door") &&
                !TileService.SupportsForegroundPlacement(WorldTileTypes.Bedrock),
            "The tile palette foreground whitelist is not aligned with TileService placement policy.");
        var runtimeDirt = ItemDefinition.CreateRuntime(WorldTileTypes.Dirt, "Dirt", 99,
            ItemCategory.Material, ItemMvpScope.A);
        Require(MainGameCraftingUiController.IsInventoryItemPlaceable(runtimeDirt, null),
            "Mined foreground tiles must be placeable directly from the inventory without a recipe.");
        Require(!MainGameTilePaletteController.RequiresDevATileIntegration("wallpaper") &&
                !MainGameTilePaletteController.RequiresDevATileIntegration("insul_wall"),
            "The tile palette still blocks the merged Dev A wallpaper placement contract.");
        Require(MainGameTilePaletteController.SupportsPalettePlacement("wallpaper") &&
                MainGameTilePaletteController.SupportsPalettePlacement(WorldTileTypes.Dirt) &&
                MainGameTilePaletteController.SupportsPalettePlacement("insul_wall") &&
                MainGameTilePaletteController.SupportsPalettePlacement("roof") &&
                !MainGameTilePaletteController.SupportsPalettePlacement("workbench"),
            "The tile palette must route wallpaper and insulation boundary tiles, but not regular buildings.");
    }

    private static void TestIceStorageSealCoreLifecycle()
    {
        var host = new GameObject("DevB_SealCoreLifecycleTest");
        var rendererHost = new GameObject("DevB_SealCoreLifecycleRenderer");
        var config = WorldGenerationConfig.CreateDefault();
        WorldSessionController session = null;

        try
        {
            var bootstrap = host.AddComponent<MainGameBootstrap>();
            var environment = host.AddComponent<MainGameEnvironmentState>();
            var renderer = rendererHost.AddComponent<TilemapRenderer>();
            session = new WorldSessionController(config, renderer, null);
            var sealSystem = new SealSystem(new TileService(new TileData[5, 5], null, null, 1));

            SetField(session, "sealSystem", sealSystem);
            SetField(bootstrap, "session", session);
            SetField(environment, "bootstrap", bootstrap);

            var entries = (IDictionary)GetField(environment, "byObjectId");
            var entryType = typeof(MainGameEnvironmentState).GetNestedType("Entry", BindingFlags.NonPublic)
                            ?? throw new InvalidOperationException("MainGameEnvironmentState.Entry type not found.");
            var entry = Activator.CreateInstance(entryType)
                        ?? throw new InvalidOperationException("MainGameEnvironmentState.Entry could not be created.");
            var coreCell = new Vector3Int(2, 2, 0);
            SetField(entry, "Record", new PlacedObjectRecord
            {
                objectId = "core_1",
                definitionId = CoolingSourceRuntime.IceStorageId,
                position = new Vector2(coreCell.x, coreCell.y)
            });
            SetField(entry, "Cell", coreCell);
            entries.Add("core_1", entry);

            Invoke(environment, "RecomputeCoolingAndInvalidate");
            Require(sealSystem.HasSealCoreCell && sealSystem.SealCoreCell == coreCell,
                "Ice storage placement did not set the seal core cell.");

            entries.Clear();
            Invoke(environment, "RecomputeCoolingAndInvalidate");
            Require(!sealSystem.HasSealCoreCell && !sealSystem.SealCoreCell.HasValue,
                "Removing the last ice storage did not clear the seal core cell.");
        }
        finally
        {
            session?.Dispose();
            UnityEngine.Object.DestroyImmediate(config);
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(rendererHost);
        }
    }

    private static void TestWallpaperCoolingDurationMultiplier()
    {
        var runtime = new CoolingSourceRuntime(null);
        Require(runtime.TryRegister("water_jar_test", CoolingSourceRuntime.WaterJarId),
            "The wallpaper duration test could not register a water jar.");
        runtime.Tick(225f, 1.25f);
        Require(runtime.ActiveCount == 0 && !runtime.TryGetRemaining("water_jar_test", out _),
            "A 100% wallpaper-covered water jar must expire after exactly 225 seconds.");

        runtime = new CoolingSourceRuntime(null);
        Require(runtime.TryRegister("water_jar_control", CoolingSourceRuntime.WaterJarId),
            "The wallpaper duration control could not register a water jar.");
        runtime.Tick(180f);
        Require(runtime.ActiveCount == 0 && !runtime.TryGetRemaining("water_jar_control", out _),
            "An uncovered water jar must retain its exact 180-second duration.");
    }

    private static void TestWallpaperRemovalDropContract()
    {
        var wallpaper = ItemDefinition.CreateRuntime(WorldTileTypes.Wallpaper, "Wallpaper", 99,
            ItemCategory.Material, ItemMvpScope.A);
        var catalog = GameDataCatalog.CreateRuntime(wallpaper);
        var tiles = new TileData[1, 1];
        tiles[0, 0] = TileData.CreateAir();
        var service = new TileService(tiles, null, catalog, 1);
        ItemDefinition droppedItem = null;
        var droppedAmount = 0;
        var droppedPosition = Vector2.zero;

        void CaptureDrop(ItemDefinition item, int amount, Vector2 position)
        {
            droppedItem = item;
            droppedAmount = amount;
            droppedPosition = position;
        }

        WorldItemDropRequest.Requested += CaptureDrop;
        try
        {
            Require(service.TryPlaceWallpaper(Vector3Int.zero),
                "The wallpaper removal test could not place its wallpaper fixture.");
            Require(service.TryRemoveWallpaper(Vector3Int.zero),
                "A player-placed wallpaper could not be removed.");
            Require(droppedItem == wallpaper && droppedAmount == 1,
                "Removing wallpaper must return exactly one wallpaper item as a world drop.");
            Require(droppedPosition == new Vector2(.5f, .5f),
                $"The recovered wallpaper must drop at the removed cell center (actual {droppedPosition}).");
            Require(!service.GetBackgroundState(Vector3Int.zero).HasWallpaper,
                "Removing wallpaper did not restore the original background state.");
        }
        finally
        {
            WorldItemDropRequest.Requested -= CaptureDrop;
            UnityEngine.Object.DestroyImmediate(catalog);
            UnityEngine.Object.DestroyImmediate(wallpaper);
        }
    }

    private static object GetField(object target, string name) =>
        target.GetType().GetField(name, InstanceMembers)?.GetValue(target)
        ?? throw new MissingFieldException(target.GetType().FullName, name);

    private static void SetField(object target, string name, object value)
    {
        var field = target.GetType().GetField(name, InstanceMembers)
                    ?? throw new MissingFieldException(target.GetType().FullName, name);
        field.SetValue(target, value);
    }

    private static void Invoke(object target, string name)
    {
        var method = target.GetType().GetMethod(name, InstanceMembers)
                     ?? throw new MissingMethodException(target.GetType().FullName, name);
        method.Invoke(target, null);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
