using System;
using System.Collections;
using System.Reflection;
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
        Debug.Log("[Nyangbingo] Dev B integration regression tests passed (12/12).");
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
                MainGameSaveCoordinator.ShouldResolveSafePlayerSpawn(false, false),
            "Regular saves must retain valid positions and recover only invalid positions.");
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
    }

    private static void TestBossHealthArtMapping()
    {
        Require(!MainGameHudController.ProductBossHealthTextEnabled &&
                MainGameHudController.BossHealthBarBelowClockY <
                    -(MainGameHudController.DayCounterExpandedHeight + MainGameHudController.DayCounterClockGap),
            "Only the illustrated boss health bar must appear directly below the fixed clock.");
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
                MainGameHudController.DayCounterClockGap >= 0f,
            "The D-day number must stay inside the scroll while the clock sits immediately below it.");
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
        Require(presenterSource.Contains("IsFullyOpen => phase == PlaybackPhase.Holding"),
            "The D-day number must only be visible while the scroll is fully open.");
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
        Require(MainGameBossSummonUiController.DebugShortcutHelpKey == KeyCode.F1,
            "MainGame Editor test shortcut help must be assigned to F1.");
        Require(MainGameBossSummonUiController.DebugShortcutHelpPanelSize.x <=
                    MainGameUiResolutionController.LogicalResolution.x &&
                MainGameBossSummonUiController.DebugShortcutHelpPanelSize.y <=
                    MainGameUiResolutionController.LogicalResolution.y &&
                MainGameBossSummonUiController.DebugShortcutHelpBodyFontSize <= 8,
            "The F1 help popup must use native 480x270 coordinates instead of legacy 1920x1080 sizing.");
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
