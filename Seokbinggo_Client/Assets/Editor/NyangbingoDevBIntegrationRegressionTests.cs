using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Nyangbingo.Combat;
using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using Nyangbingo.Save;
using Nyangbingo.UI;
using Nyangbingo.World;
using Nyangbingo.Yokai;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

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
        TestImugiPhaseCombatContract();
        TestWorldDropVisualSurfaceOffset();
        TestTreeVegetationVisualOffset();
        TestBossPausedYokaiVisibilityContract();
        TestPlayerDeathAnimationContract();
        TestDeliveredShellGlyphArtContract();
        TestCraftAndPlacementActionsRemainIndependent();
        TestRecipeProgressionUnlockContract();
        TestMissingTileEdgeOverlayRemainsDisabled();
        TestDetailedDynamicSaveSchema();
        TestResidentEliteContract();
        TestSealPaceWallDamageContract();
        TestDestructibleWallHealthContract();
        TestStrawInsulationContract();
        TestInstalledCounterAuraContract();
        TestColdWaveCoreContract();
        TestIceCrystalCoolerRecoveryContract();
        TestFrostLanternRuntimeContract();
        TestDoorAndDoorPaperContract();
        TestChestLootInterfaceContract();
        TestProductAudioMixerContract();
        TestAudioSettingsPersistenceContract();
        TestWindowsBuildSeparationContract();
        TestQuickSlotConsumableContract();
        TestMultiHitDefenseContract();
        TestForcedInvasionSpawnCapContract();
        TestBaekjungWaveCompositionContract();
        TestDaySurfaceFireContract();
        TestUndergroundTemperatureRecoveryContract();
        TestPlayerFireMitigationContract();
        TestPlayerVisionBonusContract();
        TestYagwangRuntimeTheftContract();
        Debug.Log("[Nyangbingo] Dev B integration regression tests passed (48/48).");
    }

    private static void TestChestLootInterfaceContract()
    {
        var resource = ItemDefinition.CreateRuntime("chest_loot_resource", "Chest Loot Resource", 99);
        var accessoryItem = ItemDefinition.CreateRuntime("chest_loot_accessory", "Chest Loot Accessory", 1);
        var accessory = EquipmentDefinition.CreateRuntime(
            accessoryItem.Id, EquipmentSlot.AccessoryOne, true);
        var definition = ChestDefinition.CreateRuntime(
            "chest_loot_test", ChestRegion.Deep, new[] { accessory },
            new[] { new ItemAmount { item = resource, amount = 3 } });
        ItemDefinition FindItem(string id) =>
            id == resource.Id ? resource : id == accessoryItem.Id ? accessoryItem : null;

        try
        {
            var progress = new ChestProgress(FindItem);
            Require(progress.TryOpen("chest_loot_instance", definition, 73) &&
                    progress.TryGetContents("chest_loot_instance", out var storage) &&
                    storage.Count(resource.Id) == 3 && storage.Count(accessoryItem.Id) == 1,
                "Opening a natural chest must seal its deterministic rewards in chest storage.");

            var restored = new ChestProgress(FindItem);
            var saved = new System.Collections.Generic.Dictionary<string,
                System.Collections.Generic.List<InventorySlot>>
            {
                ["chest_loot_instance"] = progress.ExportContents("chest_loot_instance")
            };
            Require(restored.TryImport(new[] { "chest_loot_instance" }, saved),
                "Natural-chest contents must accept a structured save restore.");
            Require(restored.TryGetContents("chest_loot_instance", out var restoredStorage) &&
                    restoredStorage.Count(resource.Id) == 3 &&
                    restoredStorage.Count(accessoryItem.Id) == 1,
                "Uncollected natural-chest contents must survive a structured save round-trip.");
            var playerInventory = new Nyangbingo.Inventory.Inventory(FindItem);
            Require(playerInventory.TryAdd(resource.Id, 2) &&
                    playerInventory.TryTransferSlotTo(0, restoredStorage) &&
                    restoredStorage.Count(resource.Id) == 5 &&
                    restoredStorage.TryTransferSlotTo(0, playerInventory) &&
                    playerInventory.Count(resource.Id) == 5,
                "Natural chests must support the same bidirectional stack transfer as Jangdok storage.");

            var playerSource = System.IO.File.ReadAllText(
                "Assets/Scripts/Nyangbingo/World/MainGamePlayerController.cs");
            var uiSource = System.IO.File.ReadAllText(
                "Assets/Scripts/Nyangbingo/UI/MainGameCraftingUiController.cs");
            Require(playerSource.Contains("TryOpenChest(session.ChestProgress, chestId)") &&
                    playerSource.Contains("TryPeekChestAt") &&
                    uiSource.Contains("자연 상자에 보관했습니다.") &&
                    uiSource.Contains("!runtimeServices.EquipmentCollection.Contains(equipment.Id)") &&
                    uiSource.Contains("EquipmentCollection.TryAdd(equipment)"),
                "Chest interaction must reopen a bidirectional storage UI and keep duplicate accessories transferable.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(definition);
            UnityEngine.Object.DestroyImmediate(accessory);
            UnityEngine.Object.DestroyImmediate(accessoryItem);
            UnityEngine.Object.DestroyImmediate(resource);
        }
    }

    private static void TestYagwangRuntimeTheftContract()
    {
        var item = ItemDefinition.CreateRuntime("theft_test_item", "Theft Test", 99);
        var inventory = new Nyangbingo.Inventory.Inventory(
            id => id == item.Id ? item : null);
        var equipment = new EquipmentSystem();
        var gamtu = EquipmentDefinition.CreateRuntime(
            "theft_test_gamtu", EquipmentSlot.AccessoryOne, true,
            theftBlocked: true);
        var targetObject = new GameObject("TemporaryYagwangRuntimeTarget");
        try
        {
            Require(inventory.TryAdd(item.Id, 12) &&
                    equipment.TryEquipAccessory(gamtu, 0),
                "Yagwang runtime theft test setup must be valid.");
            var target = targetObject.AddComponent<MainGameRaidTarget>();
            target.ConfigureTheftRuntime(inventory, equipment, null);
            Require(target.IsInventoryTheftBlocked &&
                    !target.TryStealInventory(1, 10) &&
                    inventory.Count(item.Id) == 12,
                "Dokkaebi gamtu must block live Yagwang inventory theft.");

            Require(equipment.TryUnequip(EquipmentSlot.AccessoryOne) &&
                    target.TryStealInventory(1, 10) &&
                    inventory.Count(item.Id) == 2,
                "Unprotected Yagwang theft must remove at most one slot and ten items.");
            var stolen = target.TakeStolenItems();
            Require(stolen.Count == 1 && stolen[0].item == item &&
                    stolen[0].amount == 10,
                "Each successful Yagwang must retain its exact stolen stack for recovery on death.");

            var encounterSource = System.IO.File.ReadAllText(
                "Assets/Scripts/Nyangbingo/World/MainGameEncounterCoordinator.cs");
            var playerSource = System.IO.File.ReadAllText(
                "Assets/Scripts/Nyangbingo/World/MainGamePlayerController.cs");
            var lootSource = System.IO.File.ReadAllText(
                "Assets/Scripts/Nyangbingo/Yokai/YokaiLoot.cs");
            var importedYagwang = AssetDatabase.LoadAssetAtPath<YokaiDefinition>(
                "Assets/Data/SO/Yokai/yakwang.asset");
            Require(encounterSource.Contains("targetCounters") &&
                    encounterSource.Contains("ActiveCounterAuras, targetCounters") &&
                    playerSource.Contains("ConfigureTheftRuntime") &&
                    lootSource.Contains("RecordStolenItems") &&
                    lootSource.Contains("definition.TearBonus") &&
                    importedYagwang != null && importedYagwang.TearBonus == 2 &&
                    importedYagwang.SignatureCondition ==
                    YokaiSignatureCondition.StealSuccess,
                "Spawned Yagwang must receive live theft state and return conditional v34 rewards.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(targetObject);
            UnityEngine.Object.DestroyImmediate(item);
            UnityEngine.Object.DestroyImmediate(gamtu);
        }
    }

    private static void TestPlayerVisionBonusContract()
    {
        Require(Mathf.Approximately(
                    MainGamePlayerController.CalculatePersonalVisionRadius(3f, 3f),
                    6f) &&
                Mathf.Approximately(
                    MainGamePlayerController.CalculatePersonalVisionRadius(0f, 3f),
                    3f) &&
                Mathf.Approximately(
                    MainGamePlayerController.CalculatePersonalVisionRadius(
                        float.NaN, float.PositiveInfinity),
                    0f),
            "The tiger-eye bead must add its finite personal-vision radius to carried light.");

        var source = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/World/MainGamePlayerController.cs");
        Require(source.Contains("new GameObject(\"PersonalVisionLight\")") &&
                source.Contains("statSheet.VisionRadiusBonus") &&
                source.Contains("127f / 255f, 227f / 255f, 195f / 255f"),
            "Tiger-eye vision must use a personal cyan light without an installed-lantern combat tag.");
    }

    private static void TestPlayerFireMitigationContract()
    {
        Require(Mathf.Approximately(
                    MainGamePlayerController.CalculateFireDamageMultiplier(-.25f, .5f),
                    .375f) &&
                Mathf.Approximately(
                    MainGamePlayerController.CalculateFireDamageMultiplier(0f, 1f),
                    1f) &&
                Mathf.Approximately(
                    MainGamePlayerController.CalculateFireDamageMultiplier(
                        float.NaN, float.PositiveInfinity),
                    1f),
            "Equipment and Haetae fire reductions must combine multiplicatively and remain finite.");

        var source = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/World/MainGamePlayerController.cs");
        Require(source.Contains("placedObjectInteractions.ActiveCounterAuras") &&
                source.Contains("playerCounterAuraSensor?.FireDamageMultiplier") &&
                source.Contains("statSheet.FireDamageModifier") &&
                source.Contains("health.SetFireDamageMultiplier"),
            "The player must continuously resolve equipment and placed Haetae fire mitigation.");
    }

    private static void TestUndergroundTemperatureRecoveryContract()
    {
        var surfaceHeights = new[] { 10, 12 };
        Require(WorldExposureRules.TryIsSurfaceExposed(
                    new Vector2(.5f, 11.5f), surfaceHeights, out var surfaceExposed) &&
                surfaceExposed &&
                WorldExposureRules.TryIsSurfaceExposed(
                    new Vector2(.5f, 10.9f), surfaceHeights, out var undergroundExposed) &&
                !undergroundExposed &&
                !WorldExposureRules.TryIsSurfaceExposed(
                    new Vector2(2.5f, 20f), surfaceHeights, out _),
            "Generated surface heights must provide one shared, bounds-safe exposure rule.");

        var source = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/World/PlayerTemperatureState.cs");
        Require(source.Contains("timeService.IsNight || IsUnderground()") &&
                source.Contains("worldSession.LastResult.surfaceHeights") &&
                source.Contains("-fallSafe * recoveryMultiplier"),
            "Player temperature must cool during daytime underground exploration.");
    }

    private static void TestDaySurfaceFireContract()
    {
        var surfaceHeights = new[] { 10, 12 };
        Require(PlayerDayHeatDamageService.IsSurfaceExposed(
                    new Vector2(.5f, 11.5f), surfaceHeights) &&
                !PlayerDayHeatDamageService.IsSurfaceExposed(
                    new Vector2(.5f, 10.9f), surfaceHeights) &&
                !PlayerDayHeatDamageService.IsSurfaceExposed(
                    new Vector2(2.5f, 20f), surfaceHeights),
            "Day fire must affect only valid world columns above their generated surface.");
        Require(Mathf.Approximately(
                    PlayerDayHeatDamageService.CalculateDamagePerSecond(
                        3f, 50f, 30f, 10, 4), 4.5f) &&
                Mathf.Approximately(
                    PlayerDayHeatDamageService.CalculateDamagePerSecond(
                        3f, 50f, 31f, 10, 4), 3f) &&
                Mathf.Approximately(
                    PlayerDayHeatDamageService.CalculateDamagePerSecond(
                        3f, 50f, 0f, 3, 4), 3f),
            "Day fire must gain exactly 50 percent only after the official 20-point pace deficit gate.");

        var healthObject = new GameObject("ResolvedEnvironmentalDamageContract");
        try
        {
            var health = healthObject.AddComponent<Health>();
            health.ConfigureForRuntime(100, 20);
            health.ApplyResolvedDamage(3, DamageTag.Fire);
            Require(health.Current == 97,
                "Continuous day fire must bypass per-hit defense after fractional damage is resolved.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(healthObject);
        }
    }

    private static void TestBaekjungWaveCompositionContract()
    {
        var composition = new[]
        {
            new YokaiSpawnAmount { kind = YokaiKind.ClubGoblin, amount = 3 },
            new YokaiSpawnAmount { kind = YokaiKind.Bulgasari, amount = 2 },
            new YokaiSpawnAmount { kind = YokaiKind.Yagwanggwi, amount = 6 },
            new YokaiSpawnAmount { kind = YokaiKind.Gaekgwi, amount = 1 }
        };
        var waves = Enumerable.Range(0, 3)
            .Select(index => Nyangbingo.Bosses.BaekjungWaveSpawner.BuildWaveComposition(
                composition, 3, index).ToArray())
            .ToArray();
        var flattened = waves.SelectMany(wave => wave).ToArray();

        Require(waves.All(wave => wave.Length == 4) &&
                !waves[0].Contains(YokaiKind.Gaekgwi) &&
                waves[1].Count(kind => kind == YokaiKind.Gaekgwi) == 1 &&
                !waves[2].Contains(YokaiKind.Gaekgwi),
            "Baekjung must spawn exactly four yokai per wave with Gaekgwi in the second wave.");
        Require(flattened.Length == 12 &&
                flattened.Count(kind => kind == YokaiKind.ClubGoblin) == 3 &&
                flattened.Count(kind => kind == YokaiKind.Bulgasari) == 2 &&
                flattened.Count(kind => kind == YokaiKind.Yagwanggwi) == 6 &&
                flattened.Count(kind => kind == YokaiKind.Gaekgwi) == 1,
            "Baekjung wave balancing must preserve the complete official 12-yokai composition.");
    }

    private static void TestForcedInvasionSpawnCapContract()
    {
        Require(MainGameEncounterCoordinator.ResolveRegularSpawnCap(8, true) == 7 &&
                MainGameEncounterCoordinator.ResolveRegularSpawnCap(8, false) == 8 &&
                MainGameEncounterCoordinator.ResolveRegularSpawnCap(0, true) == 0,
            "A forced invasion boss must consume one slot from the nightly active cap.");
        Require(MainGameEncounterCoordinator.TryMapForcedBossToCompositionKind(
                    BossKind.Imugi, out var compositionKind) &&
                compositionKind == YokaiKind.Imugi &&
                !MainGameEncounterCoordinator.TryMapForcedBossToCompositionKind(
                    BossKind.GoblinChief, out _),
            "Only the v34 forced Imugi boss may replace its matching day-curve composition entry.");

        var source = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/World/MainGameEncounterCoordinator.cs");
        Require(source.Contains("amount = Math.Max(0, amount - 1)") &&
                source.Contains("TryGetForcedInvasionCompositionKind") &&
                source.Contains("ResolveRegularSpawnCap(currentDayCurve.MaxActive"),
            "Day 30 must queue seven regular yokai and create Imugi only through the forced-boss path.");
    }

    private static void TestMultiHitDefenseContract()
    {
        var targetObject = new GameObject("MultiHitDefenseContract");
        try
        {
            var health = targetObject.AddComponent<Health>();
            health.ConfigureForRuntime(100, 3);
            health.ApplyDamage(10, DamageTag.Fire, DamageDelivery.Direct);
            health.ApplyDamage(10, DamageTag.Fire, DamageDelivery.DamageOverTime);
            Require(health.Current == 83,
                "Defense must reduce only the first tick of one multi-hit attack.");

            var bossSource = System.IO.File.ReadAllText(
                "Assets/Scripts/Nyangbingo/Bosses/BossCombatController.cs");
            var targetSource = System.IO.File.ReadAllText(
                "Assets/Scripts/Nyangbingo/World/MainGameRaidTarget.cs");
            Require(bossSource.Contains("specialDefenseApplied = false") &&
                    bossSource.Contains("specialDefenseApplied") &&
                    bossSource.Contains("DamageDelivery.DamageOverTime") &&
                    bossSource.Contains("if (applied) specialDefenseApplied = true") &&
                    targetSource.Contains("health.ApplyDamage(amount, tag, delivery)"),
                "Boss multi-hit specials must switch to defense-bypassing delivery after the first actual hit.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(targetObject);
        }
    }

    private static void TestQuickSlotConsumableContract()
    {
        Require(MainGameTilePaletteController.ShouldHighlightSlot(2, 2, "dirt") &&
                !MainGameTilePaletteController.ShouldHighlightSlot(2, 2, string.Empty) &&
                !MainGameTilePaletteController.ShouldHighlightSlot(2, 1, "dirt"),
            "A quick-slot border must render only while that slot has an active selection.");
        Require(MainGameTilePaletteController.ShouldClearEndedProductSelection(
                    true, false, false, "workbench") &&
                !MainGameTilePaletteController.ShouldClearEndedProductSelection(
                    true, false, false, PlayerHealthRecoveryService.CatnipItemId) &&
                !MainGameTilePaletteController.ShouldClearEndedProductSelection(
                    true, true, false, "workbench"),
            "Ending or cancelling a product placement must clear its quick-slot selection without cancelling direct-use items.");
        Require(Mathf.Approximately(
                    PlayerTemperatureState.CalculateCooledTemperature(40f, 0f, 10f), 30f) &&
                Mathf.Approximately(
                    PlayerTemperatureState.CalculateCooledTemperature(6f, 0f, 10f), 0f),
            "Ice shard cooling must reduce temperature by 10 without crossing the minimum.");

        var playerSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/World/MainGamePlayerController.cs");
        Require(playerSource.Contains("TryUseSelectedIceShard() ||") &&
                playerSource.Contains("tilePalette.SelectedItemId != IceShardItemId") &&
                playerSource.Contains("inventory.TryRemove(IceShardItemId, 1)") &&
                playerSource.Contains("iceShardTemperatureRelief"),
            "Selecting an ice shard in the quick slot and pressing E must consume it for immediate cooling.");
    }

    private static void TestAudioSettingsPersistenceContract()
    {
        var hadBgm = PlayerPrefs.HasKey(Nyangbingo.Audio.NyangbingoAudioService.BgmVolumePreferenceKey);
        var hadSfx = PlayerPrefs.HasKey(Nyangbingo.Audio.NyangbingoAudioService.SfxVolumePreferenceKey);
        var originalBgm = PlayerPrefs.GetFloat(
            Nyangbingo.Audio.NyangbingoAudioService.BgmVolumePreferenceKey, 1f);
        var originalSfx = PlayerPrefs.GetFloat(
            Nyangbingo.Audio.NyangbingoAudioService.SfxVolumePreferenceKey, 1f);
        GameObject first = null;
        GameObject restored = null;
        try
        {
            PlayerPrefs.DeleteKey(Nyangbingo.Audio.NyangbingoAudioService.BgmVolumePreferenceKey);
            PlayerPrefs.DeleteKey(Nyangbingo.Audio.NyangbingoAudioService.SfxVolumePreferenceKey);
            first = new GameObject("AudioSettingsPersistenceFirst");
            var firstService = first.AddComponent<Nyangbingo.Audio.NyangbingoAudioService>();
            Require(firstService.TrySetBusVolumes(0f, .35f),
                "The audio service must accept an intentional BGM mute.");
            firstService.EnsureAudiblePlayback();
            Require(Mathf.Approximately(firstService.BgmVolume, 0f) &&
                    Mathf.Approximately(firstService.SfxVolume, .35f),
                "Playback recovery must never reset intentional volume settings.");
            Require(firstService.TryPreviewBusVolumes(.25f, .45f) &&
                    Mathf.Approximately(firstService.BgmVolume, .25f) &&
                    Mathf.Approximately(firstService.SfxVolume, .45f),
                "Settings sliders must preview BGM and SFX volume changes without restarting playback.");
            Require(Mathf.Approximately(
                        Nyangbingo.Audio.NyangbingoAudioService.CalculateEffectiveOutputVolume(1f),
                        .2f) &&
                    Mathf.Approximately(
                        Nyangbingo.Audio.NyangbingoAudioService.CalculateEffectiveOutputVolume(0f),
                        0f) &&
                    Mathf.Approximately(
                        Nyangbingo.Audio.NyangbingoAudioService.CalculateSourceVolume(0f, true),
                        1f) &&
                    Mathf.Approximately(
                        Nyangbingo.Audio.NyangbingoAudioService.CalculateSourceVolume(.5f, false),
                        .5f) &&
                    Mathf.Approximately(
                        Nyangbingo.Audio.NyangbingoAudioService.ResolveCueGain(
                            Nyangbingo.Audio.AudioCue.WallDamaged),
                        Nyangbingo.Audio.NyangbingoAudioService.WallDamagedCueGain) &&
                    Nyangbingo.Audio.NyangbingoAudioService.WallDamagedCueGain < 1f,
                "The final listener gain must reduce every BGM/SFX path without changing saved slider values.");
            firstService.Shutdown();
            UnityEngine.Object.DestroyImmediate(first);
            first = null;

            restored = new GameObject("AudioSettingsPersistenceRestored");
            var restoredService =
                restored.AddComponent<Nyangbingo.Audio.NyangbingoAudioService>();
            restoredService.Initialize();
            Require(Mathf.Approximately(restoredService.BgmVolume, 0f) &&
                    Mathf.Approximately(restoredService.SfxVolume, .35f),
                "A new audio service must restore the applied settings rather than an unconfirmed preview.");
            var audioSource = System.IO.File.ReadAllText(
                "Assets/Scripts/Nyangbingo/Audio/NyangbingoAudioService.cs");
            var shellSource = System.IO.File.ReadAllText(
                "Assets/Scripts/Nyangbingo/UI/GameShellController.cs");
            Require(audioSource.Contains("if (source.clip == clip && source.isPlaying)") &&
                    audioSource.Contains("public void Shutdown()") &&
                    audioSource.Contains("EnsureSfxSourcePool()") &&
                    audioSource.Contains("audioHost.SetParent(transform, false)") &&
                    shellSource.Contains("ShowGameplay(true)") &&
                    shellSource.Contains("audioService?.EnsureAudiblePlayback();"),
                "Pause resume must preserve the active BGM track and playback position.");
        }
        finally
        {
            if (first != null)
            {
                first.GetComponent<Nyangbingo.Audio.NyangbingoAudioService>()?.Shutdown();
                UnityEngine.Object.DestroyImmediate(first);
            }
            if (restored != null)
            {
                restored.GetComponent<Nyangbingo.Audio.NyangbingoAudioService>()?.Shutdown();
                UnityEngine.Object.DestroyImmediate(restored);
            }
            if (hadBgm)
                PlayerPrefs.SetFloat(
                    Nyangbingo.Audio.NyangbingoAudioService.BgmVolumePreferenceKey, originalBgm);
            else
                PlayerPrefs.DeleteKey(
                    Nyangbingo.Audio.NyangbingoAudioService.BgmVolumePreferenceKey);
            if (hadSfx)
                PlayerPrefs.SetFloat(
                    Nyangbingo.Audio.NyangbingoAudioService.SfxVolumePreferenceKey, originalSfx);
            else
                PlayerPrefs.DeleteKey(
                    Nyangbingo.Audio.NyangbingoAudioService.SfxVolumePreferenceKey);
            PlayerPrefs.Save();
        }
    }

    private static void TestWindowsBuildSeparationContract()
    {
        Require(NyangbingoTestBuildMenu.ProductBuildOptions == BuildOptions.None,
            "The Windows product player must never include development or debugging flags.");
        Require((NyangbingoTestBuildMenu.TestBuildOptions & BuildOptions.Development) != 0 &&
                (NyangbingoTestBuildMenu.TestBuildOptions & BuildOptions.AllowDebugging) != 0,
            "The Windows test player must retain explicit development and script-debugging flags.");
        Require(NyangbingoTestBuildMenu.ProductExecutableName !=
                NyangbingoTestBuildMenu.TestExecutableName,
            "Product and test Windows players must use separate executable names.");
        var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).ToArray();
        Require(scenes.Length == 1 &&
                scenes[0].path == NyangbingoTestBuildMenu.ProductScenePath,
            "The product build must contain only the current MainGame scene.");
        var buildSource = System.IO.File.ReadAllText(
            "Assets/Editor/NyangbingoTestBuildMenu.cs");
        Require(buildSource.Contains("RemoveNonShippingArtifacts(projectRoot, outputDirectory)") &&
                buildSource.Contains("*DoNotShip*") &&
                buildSource.Contains("Validate Windows Product Build Artifacts") &&
                buildSource.Contains("NyangbingoDataBuildGate.TryValidateCurrent"),
            "The product build must remove and validate non-shipping debug artifacts.");
        Require(NyangbingoDataBuildGate.TryValidateCurrent(out var dataSummary),
            $"The product build data freshness gate must accept the current successful v34 import: {dataSummary}");
        Require(NyangbingoDataBuildGate.ManifestEntriesMatch(
                    new[] { "b.csv|1|BB", "a.csv|1|AA" },
                    new[] { "a.csv|1|AA", "b.csv|1|BB" }) &&
                !NyangbingoDataBuildGate.ManifestEntriesMatch(
                    new[] { "a.csv|1|AA" },
                    new[] { "a.csv|1|AB" }),
            "The product data manifest must ignore entry ordering but reject any row-count or hash change.");
        var dataMenuSource = System.IO.File.ReadAllText(
            "Assets/Editor/NyangbingoDataMenu.cs");
        Require(dataMenuSource.Contains("Application.logMessageReceived += captureImportError") &&
                dataMenuSource.Contains("NyangbingoDataBuildGate.WriteCurrentManifest()") &&
                dataMenuSource.Contains("if (importHadErrors)"),
            "Only an error-free v34 reimport may refresh the product data manifest.");
        Require(buildSource.Contains(".Replace(\"\\r\\n\", \"\\n\")") &&
                buildSource.Contains(".Replace('\\r', '\\n')"),
            "The product data manifest must normalize CSV line endings before hashing.");
        var testShortcutSources = new[]
        {
            "Assets/Scripts/Nyangbingo/UI/MainGameBossSummonUiController.cs",
            "Assets/Scripts/Nyangbingo/UI/MainGameCraftingUiController.cs",
            "Assets/Scripts/Nyangbingo/World/MainGameEncounterCoordinator.cs",
            "Assets/Scripts/Nyangbingo/World/MainGamePlayerController.cs",
            "Assets/Scripts/Nyangbingo/World/MagpieCompanionRuntime.cs",
            "Assets/Scripts/Nyangbingo/World/MainGameEffectPresenter.cs",
            "Assets/Scripts/Nyangbingo/World/MainGameTurretRuntime.cs"
        };
        Require(testShortcutSources.All(path =>
                    System.IO.File.ReadAllText(path)
                        .Contains("#if UNITY_EDITOR || DEVELOPMENT_BUILD")) &&
                System.IO.File.ReadAllText(testShortcutSources[0])
                    .Contains("DebugShortcutHelpKey = KeyCode.F5"),
            "The Development Build must retain the F5 help and every product test shortcut while the release player compiles them out.");
    }

    private static void TestProductAudioMixerContract()
    {
        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(
            NyangbingoAudioMixerIntegrator.MixerPath);
        Require(mixer != null,
            "The product must provide a checked-in audio mixer.");
        Require(mixer.FindMatchingGroups(NyangbingoAudioMixerIntegrator.BgmGroupName)
                    .Any(group => group.name == NyangbingoAudioMixerIntegrator.BgmGroupName) &&
                mixer.FindMatchingGroups(NyangbingoAudioMixerIntegrator.SfxGroupName)
                    .Any(group => group.name == NyangbingoAudioMixerIntegrator.SfxGroupName),
            "The product audio mixer must separate BGM and SFX buses.");
        Require(mixer.GetFloat(Nyangbingo.Audio.NyangbingoAudioService.BgmVolumeParameter, out _) &&
                mixer.GetFloat(Nyangbingo.Audio.NyangbingoAudioService.SfxVolumeParameter, out _),
            "The product audio mixer must expose independent BGM and SFX volume parameters.");
        var sceneSource = System.IO.File.ReadAllText(
            "Assets/Editor/NyangbingoMainGameSceneCreator.cs");
        Require(sceneSource.Contains("ConfigureAudioMixer(audioService)") &&
                sceneSource.Contains("bgmOutput") &&
                sceneSource.Contains("sfxOutput"),
            "MainGame scene creation must route runtime audio sources through the product mixer.");
    }

    private static void TestImugiPhaseCombatContract()
    {
        var definition = AssetDatabase.LoadAssetAtPath<BossDefinition>(
            "Assets/Data/SO/Bosses/imugi_boss.asset");
        var bossObject = new GameObject("ImugiPhaseCombatContract");
        var targetObject = new GameObject("ImugiPhaseCombatContractTarget");
        try
        {
            var bossHealth = bossObject.AddComponent<Health>();
            bossHealth.ConfigureForRuntime(definition != null ? definition.HitPoints : 1);
            var targetBody = targetObject.AddComponent<Rigidbody2D>();
            targetBody.bodyType = RigidbodyType2D.Kinematic;
            targetBody.gravityScale = 0f;
            var targetHealth = targetObject.AddComponent<Health>();
            targetHealth.ConfigureForRuntime(100);
            var target = targetObject.AddComponent<MainGameRaidTarget>();
            var combat = bossObject.AddComponent<Nyangbingo.Bosses.BossCombatController>();

            Require(definition != null && combat.ConfigureForRuntime(definition, target),
                "The imported Imugi boss must configure its phase combat runtime.");
            var characterCatalog = AssetDatabase.LoadAssetAtPath<CharacterArtCatalog>(
                "Assets/Art/Characters/CharacterArtCatalog.asset");
            var imugiArt = characterCatalog != null ? characterCatalog.Find("imugi") : null;
            var visualObject = new GameObject("Visual", typeof(SpriteRenderer),
                typeof(RuntimeCharacterSpriteAnimator));
            visualObject.transform.SetParent(bossObject.transform, false);
            var headRenderer = visualObject.GetComponent<SpriteRenderer>();
            var animator = visualObject.GetComponent<RuntimeCharacterSpriteAnimator>();
            animator.Configure(imugiArt, 15);
            var headCenter = headRenderer.bounds.center;
            visualObject.transform.position -=
                new Vector3(headCenter.x, headCenter.y, 0f);
            combat.BindCharacterAnimator(animator);
            var gameplayCatalog = AssetDatabase.LoadAssetAtPath<GameplayArtCatalog>(
                "Assets/Art/Gameplay/GameplayArtCatalog.asset");
            combat.ConfigureWarningArt(gameplayCatalog);

            var targetPosition = new Vector2(
                headRenderer.bounds.max.x + definition.SpecialRangeTiles - .2f,
                headRenderer.bounds.center.y);
            targetBody.position = targetPosition;
            targetObject.transform.position = targetPosition;
            combat.Tick(definition.SpecialCooldownSeconds);
            Require(combat.IsTelegraphing,
                "Imugi must recognize the player across the same forward box used by its special attack.");
            combat.Tick(definition.TelegraphSeconds);
            var electricEffect = bossObject.transform.Find("ImugiElectricAttack");
            var maximumEffectSize = Vector2.zero;
            if (gameplayCatalog != null)
                for (var index = 0;
                     index < gameplayCatalog.ImugiElectricAttackFrames.Count;
                     index++)
                {
                    var frame = gameplayCatalog.ImugiElectricAttackFrames[index];
                    if (frame == null) continue;
                    maximumEffectSize.x = Mathf.Max(maximumEffectSize.x, frame.bounds.size.x);
                    maximumEffectSize.y = Mathf.Max(maximumEffectSize.y, frame.bounds.size.y);
                }
            var expectedEffectCenter = new Vector2(
                headRenderer.bounds.max.x + definition.SpecialRangeTiles * .5f,
                headRenderer.bounds.center.y);
            Require(targetHealth.Current == 82 &&
                    electricEffect != null &&
                    Mathf.Approximately(
                        electricEffect.position.x, expectedEffectCenter.x) &&
                    Mathf.Approximately(
                        electricEffect.position.y,
                        expectedEffectCenter.y - definition.SpecialRangeTiles * .5f) &&
                    Mathf.Approximately(
                        electricEffect.lossyScale.x * maximumEffectSize.x,
                        definition.SpecialRangeTiles) &&
                    Mathf.Approximately(
                        electricEffect.lossyScale.y * maximumEffectSize.y,
                        definition.SpecialRangeTiles),
                "Imugi lightning, damage, and recognition must share the same 3x3 area in front of its head.");
            var specialAreaMethod = typeof(Nyangbingo.Bosses.BossCombatController).GetMethod(
                "IsInsideSpecialArea",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(Vector2), typeof(Vector2) },
                null);
            Require(specialAreaMethod != null &&
                    (bool)specialAreaMethod.Invoke(
                        combat, new object[] { new Vector2(2.9f, 1.4f), Vector2.right }) &&
                    !(bool)specialAreaMethod.Invoke(
                        combat, new object[] { new Vector2(2.9f, 1.6f), Vector2.right }),
                "Imugi's Box damage must include its visible corners without extending beyond the 3x3 effect.");

            targetBody.position = new Vector2(1.4f, 0f);
            targetObject.transform.position = targetBody.position;
            bossHealth.ApplyDamage(Mathf.FloorToInt(definition.HitPoints * .34f) + 1,
                Nyangbingo.Core.DamageTag.Melee);
            combat.Tick(.01f);
            Require(combat.IsTelegraphing,
                "Imugi must telegraph the 3x3 landing discharge after crossing 66% health.");
            combat.Tick(definition.TelegraphSeconds);
            Require(targetHealth.Current == 74 &&
                    Mathf.Approximately(targetBody.position.x, 3.4f),
                "Imugi's 3x3 landing discharge must deal 8 damage and knock back 2 tiles.");

            var belowLakePhase = Mathf.CeilToInt(definition.HitPoints * .33f) - 1;
            bossHealth.ApplyDamage(Mathf.Max(0, bossHealth.Current - belowLakePhase),
                Nyangbingo.Core.DamageTag.Melee);
            targetBody.position = new Vector2(40f, 0f);
            targetObject.transform.position = targetBody.position;
            combat.Tick(.01f);
            Require(combat.IsTelegraphing,
                "Imugi must telegraph the whole-lake pulse after crossing 33% health.");
            combat.Tick(definition.TelegraphSeconds);
            combat.Tick(.5f);
            Require(targetHealth.Current == 58 &&
                    Mathf.Approximately(targetBody.position.x, 40f) &&
                    !combat.IsSpecialActive,
                "Imugi's whole-lake phase must deal two 8-damage pulses without hidden shock or knockback.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(bossObject);
            UnityEngine.Object.DestroyImmediate(targetObject);
        }
    }

    private static void TestBossPausedYokaiVisibilityContract()
    {
        var root = new GameObject(
            "BossPausedYokaiVisibility",
            typeof(SpriteRenderer),
            typeof(RuntimeCharacterSpriteAnimator));
        try
        {
            var renderer = root.GetComponent<SpriteRenderer>();
            var animator = root.GetComponent<RuntimeCharacterSpriteAnimator>();
            var original = new Color(.2f, .4f, .6f, .7f);
            renderer.color = original;
            var brain = root.AddComponent<Nyangbingo.Yokai.YokaiBrain>();

            Require(brain.SetBossEncounterPaused(true) &&
                    Mathf.Approximately(
                        renderer.color.a,
                        original.a * Nyangbingo.Yokai.YokaiBrain
                            .BossEncounterPausedAlphaMultiplier) &&
                    !animator.enabled,
                "A summoned-boss encounter must visibly freeze existing yokai at a translucent alpha instead of hiding them.");
            Require(brain.SetBossEncounterPaused(false) &&
                    renderer.color == original &&
                    animator.enabled,
                "Field yokai visibility and animation state must be restored exactly after the summoned-boss encounter.");
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
            magpieJoined = true,
            magpieKillCount = 30,
            magpieBaekjungSurvived = true,
            magpieNestPosition = new Vector2(8.5f, 10.5f),
            magpieStorage = new System.Collections.Generic.List<InventorySlot>
            {
                new InventorySlot { itemId = "stone", amount = 2 }
            },
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
                pendingRaidYokaiIds = new System.Collections.Generic.List<string> { "club" },
                residentLastKilledDays =
                    new System.Collections.Generic.List<ResidentYokaiDayRecord>
                    {
                        new ResidentYokaiDayRecord
                            { yokaiId = "eoduksini", lastKilledDay = 16 },
                        new ResidentYokaiDayRecord
                            { yokaiId = "gangcheol", lastKilledDay = 18 }
                    }
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
        Require(loaded.schemaVersion == SaveGame.CurrentSchemaVersion &&
                loaded.regularEncounter.usesDetailedYokaiState &&
                loaded.regularEncounter.activeYokai.Count == 1 &&
                loaded.regularEncounter.activeYokai[0].instanceId == "yokai_7" &&
                loaded.regularEncounter.activeYokai[0].position == new Vector3(12.5f, 8.5f, 0f) &&
                loaded.regularEncounter.activeYokai[0].velocity == new Vector2(1.5f, -2f) &&
                loaded.regularEncounter.activeYokai[0].currentHealth == 17 &&
                loaded.regularEncounter.activeYokai[0].raid &&
                 loaded.regularEncounter.pendingRegularYokaiIds.Count == 1 &&
                 loaded.regularEncounter.pendingRaidYokaiIds.Count == 1 &&
                 loaded.regularEncounter.residentLastKilledDays.Count == 2 &&
                 loaded.regularEncounter.residentLastKilledDays[0].lastKilledDay == 16 &&
                 loaded.regularEncounter.residentLastKilledDays[1].lastKilledDay == 18,
            "Detailed yokai identity, position, HP, track, and queues must survive JSON.");
        Require(loaded.worldDrops.Count == 1 && loaded.worldDrops[0].itemId == "stone" &&
                loaded.worldDrops[0].amount == 3 &&
                loaded.worldDrops[0].position == new Vector2(4.25f, 6.5f) &&
                loaded.worldDrops[0].velocity == new Vector2(-1f, 2f) &&
                Mathf.Approximately(loaded.worldDrops[0].pickupDelay, .2f),
            "World-drop item, amount, transform, velocity, and pickup delay must survive JSON.");
        Require(Mathf.Approximately(loaded.sealPct, 73.5f) &&
                Mathf.Approximately(loaded.baekjungTearRemainder, .5f) &&
                loaded.magpieJoined && loaded.magpieKillCount == 30 &&
                loaded.magpieBaekjungSurvived &&
                loaded.magpieNestPosition == new Vector2(8.5f, 10.5f) &&
                loaded.magpieStorage.Count == 1 &&
                loaded.magpieStorage[0].itemId == "stone" &&
                loaded.magpieStorage[0].amount == 2,
            "Seal, Baekjung, and persistent magpie progression/storage must survive JSON.");
        Require(SaveManager.TryDeserialize("{\"schemaVersion\":16}", out var legacy) &&
                legacy.schemaVersion == SaveGame.CurrentSchemaVersion &&
                legacy.worldDrops != null && legacy.worldDrops.Count == 0 &&
                legacy.regularEncounter != null &&
                !legacy.regularEncounter.usesDetailedYokaiState &&
                legacy.regularEncounter.activeYokai != null &&
                 legacy.regularEncounter.pendingRegularYokaiIds != null &&
                 legacy.regularEncounter.pendingRaidYokaiIds != null &&
                 legacy.regularEncounter.residentLastKilledDays != null &&
                 legacy.regularEncounter.residentLastKilledDays.Count == 0 &&
                legacy.magpieKillCount == 0 &&
                legacy.magpieStorage != null && legacy.magpieStorage.Count == 0,
            "Schema 16 saves must migrate to empty dynamic lists and the legacy encounter fallback.");

        var magpieSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/World/MagpieCompanionRuntime.cs");
        var environmentSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/World/MainGameEnvironmentState.cs");
        var artIntegratorSource = System.IO.File.ReadAllText(
            "Assets/Editor/NyangbingoTileArtIntegrator.cs");
        var magpiePlayerSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/World/MainGamePlayerController.cs");
        Require(magpieSource.Contains("GameEvents.OnYokaiKilled") &&
                magpieSource.Contains("GameEvents.OnBaekjungEnd") &&
                magpieSource.Contains("GameEvents.OnDayStart") &&
                magpieSource.Contains("magpie_magnet_radius") &&
                magpieSource.Contains("magpie_magnet_interval") &&
                magpieSource.Contains("TryFindNearestStack") &&
                magpieSource.Contains("TryCollectStack") &&
                magpieSource.Contains("collectionTarget.position + DropVisualOffset") &&
                magpieSource.Contains("visualAnimator?.PlayAttack()") &&
                magpieSource.Contains("visualAnimator?.SetMoving(!seatedAtNest)") &&
                magpieSource.Contains("dayFollowSide = horizontalMovement > 0f ? -1f : 1f") &&
                !magpieSource.Contains("(current - target).sqrMagnitude > 64f") &&
                magpieSource.Contains("sealSystem.IsInsideSealedArea") &&
                environmentSource.Contains("restoredMagpieNestCount > 1") &&
                environmentSource.Contains("IsGlobalSingletonDefinition(record.definitionId)") &&
                environmentSource.Contains(
                    "definitionId == MagpieNestDefinitionId") &&
                artIntegratorSource.Contains("[\"magpie\"] = \"magpie.aseprite\"") &&
                artIntegratorSource.Contains("\"Frame_0\"") &&
                artIntegratorSource.Contains("\"Frame_3\"") &&
                artIntegratorSource.Contains("new[] { idleFrames[0] }") &&
                artIntegratorSource.Contains("idleFrames.Skip(1).Take(2).ToArray()") &&
                artIntegratorSource.Contains("new[] { idleFrames[3] }") &&
                artIntegratorSource.Contains("RequireFrames(id, \"flight\", entry.WalkFrames, 2") &&
                magpieSource.Contains("new GameObject(\"MagpieCompanion\")") &&
                magpieSource.Contains("NestPerchOffset") &&
                magpieSource.Contains("ResolveDayFollowOffset") &&
                magpieSource.Contains("ToggleEditorTestOverride") &&
                magpiePlayerSource.Contains("Input.GetKeyDown(KeyCode.M)"),
            "The v34 magpie must join at dawn and collect one world-drop stack through the official sealed-nest rules.");

        var validateYokaiState = typeof(MainGameEncounterCoordinator).GetMethod(
            "ValidateYokaiBrainState",
            BindingFlags.Static | BindingFlags.NonPublic);
        var validGaekgwiState = new YokaiStateRecord
        {
            gaekgwiPatternInitialized = true,
            gaekgwiPatternState = 2,
            gaekgwiDashRemaining =
                YokaiBrain.GaekgwiDashDistanceTiles * (1f - 30f / 68.5f),
            gaekgwiDashDirection = Vector2.right
        };
        var invalidGaekgwiState = JsonUtility.FromJson<YokaiStateRecord>(
            JsonUtility.ToJson(validGaekgwiState));
        invalidGaekgwiState.gaekgwiDashRemaining =
            YokaiBrain.GaekgwiDashDistanceTiles + 1f;
        Require(validateYokaiState != null &&
                (bool)validateYokaiState.Invoke(null, new object[] { validGaekgwiState }) &&
                !(bool)validateYokaiState.Invoke(null, new object[] { invalidGaekgwiState }),
            "Detailed encounter restore must reject corrupt Gaekgwi pattern progress before mutating the world.");

        var gaekgwiDefinition = AssetDatabase.LoadAssetAtPath<YokaiDefinition>(
            "Assets/Data/SO/Yokai/gaekgwi.asset");
        var gaekgwiObject = new GameObject("GaekgwiMidDashRestoreContract");
        try
        {
            gaekgwiObject.AddComponent<Health>();
            var gaekgwiBrain = gaekgwiObject.AddComponent<YokaiBrain>();
            gaekgwiBrain.ConfigureForRuntime(gaekgwiDefinition, null);
            Require(gaekgwiBrain.RestoreSaveState(validGaekgwiState) &&
                    Mathf.Approximately(gaekgwiBrain.GaekgwiDashNormalizedTime, .2f),
                "A Gaekgwi saved after dash frame one must resume from the same animation progress.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gaekgwiObject);
        }
    }

    private static void TestResidentEliteContract()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<GameDataCatalog>(
            "Assets/Data/SO/GameDataCatalog.asset");
        var eoduksini = catalog?.FindYokai("eoduksini");
        var gangcheori = catalog?.FindYokai("gangcheol");
        Require(catalog != null && catalog.Globals.Count == 100 &&
                ResidentYokaiRules.TryCreate(catalog.Globals, out var rules) &&
                rules.MaxPerSpecies == 1 &&
                rules.MinPlayerDistance == 24 &&
                rules.MinBetweenDistance == 12 &&
                rules.MinDepth == 91 && rules.MaxDepth == 135,
            "The v34.1 catalog must expose the six confirmed resident-elite globals.");
        Require(eoduksini != null &&
                eoduksini.SupportsSpawnTrack(YokaiSpawnTrack.Resident) &&
                gangcheori != null &&
                gangcheori.SupportsSpawnTrack(YokaiSpawnTrack.Resident),
            "Eoduksini and Gangcheori must both use the resident encounter track.");
        Require(Mathf.Approximately(
                    gangcheori.WallDamageFor(YokaiWallMaterial.Default), 0f) &&
                Mathf.Approximately(
                    gangcheori.WallDamageFor(YokaiWallMaterial.Ice), 0f) &&
                Mathf.Approximately(
                    gangcheori.WallDamageFor(YokaiWallMaterial.IronHeatWall), 0f),
            "Resident Gangcheori must preserve the v34.1 zero wall-DPS contract for every material.");
        Require(YokaiSpecialRules.ContactDamage(eoduksini, true) == 14 &&
                YokaiSpecialRules.ContactDamage(eoduksini, false) == 21,
            "Eoduksini contact damage must use 14 inside lantern light and 21 outside it.");
        Require(!MainGameEncounterCoordinator.ShouldSpawnResident(15, 16, 0, 0, 1) &&
                MainGameEncounterCoordinator.ShouldSpawnResident(16, 16, 0, 0, 1) &&
                !MainGameEncounterCoordinator.ShouldSpawnResident(16, 16, 16, 0, 1) &&
                MainGameEncounterCoordinator.ShouldSpawnResident(17, 16, 16, 0, 1) &&
                !MainGameEncounterCoordinator.ShouldSpawnResident(18, 18, 0, 1, 1),
            "Resident elites must first appear on their confirmed dawn, respawn only on a later day, " +
            "and remain capped at one per species.");
        Require(MainGameEncounterCoordinator.IsResidentDepth(150, 60) &&
                MainGameEncounterCoordinator.IsResidentDepth(150, 16) &&
                !MainGameEncounterCoordinator.IsResidentDepth(150, 15) &&
                !MainGameEncounterCoordinator.IsResidentDepth(150, 61),
            "Resident spawn cells must remain within surface-relative depth 91 through 135.");
        Require(GangcheoriBreathController.Damage == 18 &&
                Mathf.Approximately(GangcheoriBreathController.TelegraphSeconds, 1.5f) &&
                Mathf.Approximately(GangcheoriBreathController.RangeTiles, 3f) &&
                Mathf.Approximately(GangcheoriBreathController.ArcDegrees, 60f) &&
                Mathf.Approximately(GangcheoriBreathController.KnockbackTiles, 2f) &&
                Mathf.Approximately(GangcheoriBreathController.CooldownSeconds, 12f),
            "Gangcheori breath must be one immediate 18-damage fire hit with the confirmed geometry.");
    }

    private static void TestSealPaceWallDamageContract()
    {
        Require(Mathf.Approximately(
                    MainGameRaidTarget.CalculatePaceAdjustedWallDamage(
                        10f, 50f, 10f, 8, 4),
                    13f) &&
                Mathf.Approximately(
                    MainGameRaidTarget.CalculatePaceAdjustedWallDamage(
                        10f, 50f, 10.01f, 8, 4),
                    10f) &&
                Mathf.Approximately(
                    MainGameRaidTarget.CalculatePaceAdjustedWallDamage(
                        10f, 50f, 0f, 3, 4),
                    10f) &&
                Mathf.Approximately(
                    MainGameRaidTarget.CalculatePaceAdjustedWallDamage(
                        10f, 50f, 50f, 8, 4),
                    10f),
            "Yokai wall damage must gain exactly 30% only at a 40-point seal pace deficit " +
            "on or after the configured penalty day.");
    }

    private static void TestDestructibleWallHealthContract()
    {
        var tiles = new TileData[6, 4];
        var service = new TileService(tiles, null, null, 17);
        var wallCell = new Vector3Int(2, 1, 0);
        var placedWallEvents = 0;
        Action<string> countPlacedWall = id =>
        {
            if (id == GoalBadgeProgress.InsulationWallId) placedWallEvents++;
        };
        GameEvents.OnPlacedObjectBuilt += countPlacedWall;
        var placed = false;
        try
        {
            placed = service.TryPlaceForeground(wallCell, "insul_wall");
        }
        finally
        {
            GameEvents.OnPlacedObjectBuilt -= countPlacedWall;
        }
        Require(placed && placedWallEvents == 1,
            "A foreground insulation wall placement must complete the official first-wall badge.");
        Require(Mathf.Approximately(
                    MainGamePlayerController.ResolveTileMiningSeconds(
                        null, "insul_wall", 1),
                    MainGamePlayerController.InsulationWallBareClawMiningSeconds) &&
                service.GetTile(wallCell).hardness == 1,
            "A basic insulation wall must remain removable by the T1 bare claw in three seconds.");
        Require(
                service.TryFindDamageableWall(
                    new Vector2(1.5f, 1.5f), Vector2.right, 1f,
                    out var foundCell, out var material) &&
                foundCell == wallCell && material == YokaiWallMaterial.Default,
            "A yokai must resolve an adjacent insulation wall between itself and the player.");
        Require(
                service.TryFindDamageableWall(
                    new Vector2(3.5f, 1.5f), Vector2.left, 1f,
                    out foundCell, out material) &&
                foundCell == wallCell && material == YokaiWallMaterial.Default,
            "A yokai must attack the wall on its selected route even when that route points away from the player's direct bearing.");
        var upperWallCell = wallCell + Vector3Int.up;
        Require(service.TryPlaceForeground(upperWallCell, "insul_wall") &&
                service.TryFindDamageableWall(
                    new Vector2(1.5f, 1.5f), Vector2.right, 1f,
                    out foundCell, out material) &&
                foundCell == wallCell && material == YokaiWallMaterial.Default,
            "A grounded yokai must resolve the lower attack cell of a two-tile player wall instead of treating it as a step.");
        var yokaiBrainSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/Yokai/YokaiBrain.cs");
        Require(yokaiBrainSource.Contains(
                    "var wallApproachDirection = direction.sqrMagnitude") &&
                yokaiBrainSource.Contains(
                    "currentPosition, wallApproachDirection, attackRange") &&
                yokaiBrainSource.Contains(
                    "var blockingWallDamage = foundBlockingWall") &&
                !yokaiBrainSource.Contains("var isRoutingAcrossAnotherFloor"),
            "Wall attacks must follow the selected route at every height while zero-DPS yokai remain in pursuit.");
        var durabilityEvents = 0;
        var durabilityCell = default(Vector3Int);
        var durabilityCurrent = 0f;
        var durabilityMaximum = 0f;
        Action<Vector3Int, float, float, bool> captureDurability =
            (cell, current, maximum, destroyedWall) =>
            {
                durabilityEvents++;
                durabilityCell = cell;
                durabilityCurrent = current;
                durabilityMaximum = maximum;
            };
        GameEvents.OnWallDurabilityChanged += captureDurability;
        var damaged = false;
        var applied = 0f;
        var destroyed = false;
        try
        {
            damaged = service.TryDamageWall(
                wallCell, 125f, out applied, out destroyed);
        }
        finally
        {
            GameEvents.OnWallDurabilityChanged -= captureDurability;
        }
        Require(damaged &&
                Mathf.Approximately(applied, 125f) && !destroyed &&
                Mathf.Approximately(service.GetWallRemainingHitPoints(wallCell), 475f) &&
                durabilityEvents == 1 && durabilityCell == wallCell &&
                Mathf.Approximately(durabilityCurrent, 475f) &&
                Mathf.Approximately(durabilityMaximum, 600f),
            "A regular insulation wall must retain data-driven partial damage and publish its current durability.");

        var records = service.ExportWallDamage();
        var restoredTiles = new TileData[6, 4];
        var restored = new TileService(restoredTiles, null, null, 17);
        Require(restored.TryPlaceForeground(wallCell, "insul_wall") &&
                restored.RestoreWallDamage(records) &&
                Mathf.Approximately(restored.GetWallRemainingHitPoints(wallCell), 475f),
            "Partial wall damage must survive a structured save round-trip.");
        Require(restored.TryDamageWall(wallCell, 475f, out applied, out destroyed) &&
                destroyed && Mathf.Approximately(applied, 475f) &&
                restored.GetTile(wallCell).IsAir &&
                restored.ExportWallDamage().Count == 0,
            "A wall reduced to zero HP must become air and clear its partial-damage record.");

        var upgradedTiles = new TileData[6, 4];
        var upgraded = new TileService(upgradedTiles, null, null, 18);
        Require(upgraded.TryPlaceForeground(wallCell, "insul_wall"),
            "The clay-plaster wall test requires a placed insulation wall.");
        upgraded.SetClayPlasterResolver(cell => cell == wallCell);
        Require(Mathf.Approximately(
                upgraded.GetWallRemainingHitPoints(wallCell),
                TileService.DefaultClayPlasteredWallHitPoints),
            "Clay plaster must raise the supported insulation wall to 750 HP.");

        var cadenceTargetObject = new GameObject("WallAttackCadenceTarget");
        var cadenceYokaiObject = new GameObject("WallAttackCadenceYokai");
        var cadenceDefinition = YokaiDefinition.CreateRuntime(
            YokaiKind.ClubGoblin, 10, 1f, 1, 5f, Array.Empty<ItemAmount>());
        var wallSfxEvents = 0;
        Action countWallSfx = () => wallSfxEvents++;
        try
        {
            cadenceTargetObject.transform.position = Vector3.right * .5f;
            var cadenceTarget =
                cadenceTargetObject.AddComponent<Nyangbingo.Debugging.DevBTestYokaiTarget>();
            var cadenceBrain = cadenceYokaiObject.AddComponent<YokaiBrain>();
            cadenceBrain.ConfigureForRuntime(cadenceDefinition, cadenceTarget);
            cadenceBrain.Tick(0f);
            GameEvents.OnWallDamaged += countWallSfx;
            cadenceBrain.Tick(.1f);
            cadenceBrain.Tick(.5f);
            Require(Mathf.Approximately(cadenceTarget.WallDamageReceived, 5f) &&
                    wallSfxEvents == 1,
                "Wall damage and SFX must fire once per attack cycle instead of every game tick.");
            cadenceBrain.Tick(.5f);
            Require(Mathf.Approximately(cadenceTarget.WallDamageReceived, 10f) &&
                    wallSfxEvents == 2,
                "A sustained wall attack must retain its configured DPS at the one-second cadence.");
        }
        finally
        {
            GameEvents.OnWallDamaged -= countWallSfx;
            UnityEngine.Object.DestroyImmediate(cadenceYokaiObject);
            UnityEngine.Object.DestroyImmediate(cadenceTargetObject);
            UnityEngine.Object.DestroyImmediate(cadenceDefinition);
        }
    }

    private static void TestStrawInsulationContract()
    {
        Require(Mathf.Approximately(
                    MainGameEnvironmentState.CalculateStrawInsulationRecoveryMultiplier(
                        1, .05f),
                    1.05f) &&
                Mathf.Approximately(
                    MainGameEnvironmentState.CalculateStrawInsulationRecoveryMultiplier(
                        6, .05f),
                    1.3f) &&
                Mathf.Approximately(
                    MainGameEnvironmentState.CalculateStrawInsulationRecoveryMultiplier(
                        99, .05f),
                    1.3f) &&
                Mathf.Approximately(
                    MainGameEnvironmentState.CalculateStrawInsulationRecoveryMultiplier(
                        6, float.NaN),
                    1f),
            "Straw insulation must add five percent temperature recovery per attached piece " +
            "and remain capped at six pieces.");

        var environmentSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/World/MainGameEnvironmentState.cs");
        var temperatureSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/World/PlayerTemperatureState.cs");
        var placementSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/World/MainGameTurretRuntime.cs");
        Require(environmentSource.Contains("boundaryCells.Contains(entry.Cell)") &&
                environmentSource.Contains("GameEvents.OnTileBroken += HandleAttachmentSupportBroken") &&
                temperatureSource.Contains("ResolveTemperatureRecoveryMultiplier(") &&
                placementSource.Contains("CanPlaceDefinitionAt("),
            "Straw insulation must attach to a recognized room boundary, survive placed-object " +
            "save data, and affect the live temperature recovery path.");
    }

    private static void TestInstalledCounterAuraContract()
    {
        Require(MainGameTurretRuntime.TryGetPassiveCounterAuraConfiguration(
                    MainGameTurretRuntime.SieveItemId,
                    out var sieveKind, out var sieveRadius, out var sieveEffect,
                    out var sieveDuration, out var sieveCooldown) &&
                sieveKind == CounterAuraKind.Sieve &&
                Mathf.Approximately(sieveRadius, 4f) &&
                Mathf.Approximately(sieveEffect, 1.5f) &&
                Mathf.Approximately(sieveDuration, 12f) &&
                Mathf.Approximately(sieveCooldown, 30f),
            "An installed sieve must stop Yagwanggwi for 12 seconds, apply 1.5x damage, " +
            "and use the confirmed four-tile radius and 30-second cooldown.");
        Require(MainGameTurretRuntime.TryGetPassiveCounterAuraConfiguration(
                    MainGameTurretRuntime.HaetaeStatueItemId,
                    out var haetaeKind, out var haetaeRadius, out var haetaeEffect,
                    out _, out _) &&
                haetaeKind == CounterAuraKind.Haetae &&
                Mathf.Approximately(haetaeRadius, 8f) &&
                Mathf.Approximately(haetaeEffect, .5f),
            "An installed Haetae statue must halve fire damage in its eight-tile radius.");
        Require(MainGameTurretRuntime.TryGetPassiveCounterAuraConfiguration(
                    MainGameTurretRuntime.BellRopeItemId,
                    out var bellKind, out var bellRadius, out _, out _, out var bellCooldown) &&
                bellKind == CounterAuraKind.BellRope &&
                Mathf.Approximately(bellRadius, 10f) &&
                Mathf.Approximately(bellCooldown, 4f) &&
                MainGameTurretRuntime.TryGetPassiveCounterAuraConfiguration(
                    MainGameTurretRuntime.IronSieveItemId,
                    out var ironSieveKind, out _, out _, out _, out _) &&
                ironSieveKind == CounterAuraKind.Sieve &&
                !MainGameTurretRuntime.TryGetPassiveCounterAuraConfiguration(
                    "not_a_counter", out _, out _, out _, out _, out _),
            "Installed bell ropes and evolved counter items must retain their confirmed base aura " +
            "without accepting unknown placeables.");

        var source = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/World/MainGameTurretRuntime.cs");
        Require(source.Contains("TryRegisterPlacedCounterAura(record.objectId, record.definitionId)") &&
                source.Contains("activeCounterAuras.Add(aura)") &&
                source.Contains("RemovePassiveCounterAura(record.objectId)"),
            "Placed counter auras must register on placement and load, and unregister on recovery.");
    }

    private static void TestColdWaveCoreContract()
    {
        Require(PlayerTemperatureState.CalculateEffectiveHeatStage(4, 1) == 3 &&
                PlayerTemperatureState.CalculateEffectiveHeatStage(3, 1) == 2 &&
                PlayerTemperatureState.CalculateEffectiveHeatStage(1, 1) == 1 &&
                PlayerTemperatureState.CalculateEffectiveHeatStage(0, 1) == 1 &&
                PlayerTemperatureState.CalculateEffectiveHeatStage(3, 0) == 3,
            "A placed cold-wave core must reduce daytime heat by exactly one stage " +
            "without allowing the effective stage to fall below one.");

        var environmentSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/World/MainGameEnvironmentState.cs");
        var temperatureSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/World/PlayerTemperatureState.cs");
        Require(environmentSource.Contains(
                    "public const string ColdWaveCoreDefinitionId = \"cold_wave_core\"") &&
                environmentSource.Contains("IsGlobalSingletonDefinition(record.definitionId)") &&
                environmentSource.Contains("restoredColdWaveCoreCount") &&
                temperatureSource.Contains("environmentState?.HeatStageReduction ?? 0"),
            "The cold-wave core must be globally limited to one installation, reject duplicate " +
            "save data before restore, and affect the live daytime temperature path.");
    }

    private static void TestIceCrystalCoolerRecoveryContract()
    {
        Require(Mathf.Approximately(
                    MainGameEnvironmentState.CalculateSealedRecoveryMultiplier(
                        0, .05f, true),
                    2f) &&
                Mathf.Approximately(
                    MainGameEnvironmentState.CalculateSealedRecoveryMultiplier(
                        6, .05f, true),
                    2.6f) &&
                Mathf.Approximately(
                    MainGameEnvironmentState.CalculateSealedRecoveryMultiplier(
                        6, .05f, false),
                    1.3f),
            "One ice-crystal cooler must double recovery inside its sealed region without " +
            "stacking, while retaining the straw-insulation multiplier.");

        var environmentSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/World/MainGameEnvironmentState.cs");
        Require(environmentSource.Contains(
                    "entry.Record.definitionId == CoolingSourceRuntime.IceCrystalCoolerId") &&
                environmentSource.Contains("interiorCells.Contains(entry.Cell)") &&
                environmentSource.Contains("hasIceCrystalCooler ? 2f : 1f"),
            "The live recovery path must require the installed ice-crystal cooler to share " +
            "the player's sealed interior.");
    }

    private static void TestFrostLanternRuntimeContract()
    {
        Require(MainGameTurretRuntime.IsInstalledLanternDefinition(
                    MainGameTurretRuntime.LanternItemId) &&
                MainGameTurretRuntime.IsInstalledLanternDefinition(
                    MainGameTurretRuntime.FrostLanternItemId) &&
                !MainGameTurretRuntime.IsInstalledLanternDefinition("saekdong_lantern") &&
                MainGameTurretRuntime.FuelItemForInstalledLantern(
                    MainGameTurretRuntime.LanternItemId) == "coal" &&
                MainGameTurretRuntime.FuelItemForInstalledLantern(
                    MainGameTurretRuntime.FrostLanternItemId) == "frost_essence",
            "The evolved frost lantern must retain the installed six-tile lantern runtime " +
            "while replacing coal fuel with frost essence.");

        var source = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/World/MainGameTurretRuntime.cs");
        Require(source.Contains("IsInstalledLanternDefinition(record.definitionId)") &&
                source.Contains("TryRegisterPlacedLantern(") &&
                source.Contains("FrostLanternFuelSecondsKey") &&
                source.Contains("remainingGameSeconds = entry.FuelRemaining"),
            "Frost lantern placement, interaction, and save restore must share the official " +
            "installed-lantern lifecycle without treating the cosmetic lantern as a counter.");
    }

    private static void TestDoorAndDoorPaperContract()
    {
        var tiles = new TileData[3, 3];
        tiles[1, 1] = new TileData
        {
            elementType = "door",
            hardness = 1,
            isNaturalTerrain = false
        };
        var service = new TileService(tiles, null, null, 1);
        Require(service.TryToggleNearestDoor(
                    new Vector2(1.5f, 1.5f), .6f, out var opened) &&
                opened && service.IsDoorOpen(new Vector3Int(1, 1, 0)),
            "E interaction must open a nearby insulated door while preserving its world tile.");

        var exported = service.ExportDoorStates();
        var restored = new TileService(tiles, null, null, 1);
        Require(exported.Count == 1 && exported[0].isOpen &&
                restored.RestoreDoorStates(exported) &&
                restored.IsDoorOpen(new Vector3Int(1, 1, 0)) &&
                restored.TryToggleNearestDoor(
                    new Vector2(1.5f, 1.5f), .6f, out var closed) &&
                !closed && !restored.IsDoorOpen(new Vector3Int(1, 1, 0)),
            "Open insulated-door state must survive save restore and remain toggleable.");

        Require(Mathf.Approximately(
                    MainGameEnvironmentState.CalculateDoorAdjustedRecoveryMultiplier(
                        2.6f, false),
                    2.6f) &&
                Mathf.Approximately(
                    MainGameEnvironmentState.CalculateDoorAdjustedRecoveryMultiplier(
                        2.6f, true),
                    0f),
            "An open door must pause sealed temperature recovery unless that same door has paper.");

        var playerSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/World/MainGamePlayerController.cs");
        var saveSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/Save/MainGameSaveCoordinator.cs");
        var environmentSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/World/MainGameEnvironmentState.cs");
        Require(playerSource.Contains("TryToggleNearbyDoor()") &&
                saveSource.Contains("ExportDoorStates()") &&
                saveSource.Contains("RestoreDoorStates(save.doorStates)") &&
                environmentSource.Contains(
                    "attachment.Record.definitionId != DoorPaperDefinitionId"),
            "Door input, persistence, and the attached door-paper exception must all use " +
            "the live product paths.");
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

        const float viewHeight = orthographicSize * 2f;
        const float viewWidth = viewHeight * 16f / 9f;
        var backgroundLayout = MainGameParallaxBackground.CalculateSurfaceCanvasCoverTransform(
            viewWidth, viewHeight, 20f, 10f);
        Require(Mathf.Approximately(backgroundLayout.x, 1.6f) &&
                20f * backgroundLayout.x >= viewWidth &&
                10f * backgroundLayout.x >= viewHeight &&
                Mathf.Approximately(backgroundLayout.y, -orthographicSize),
            "The 2:1 surface background must cover the entire 16:9 gameplay viewport without " +
            "exposing a bottom gap when the player jumps.");

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
                shellSource.Contains("SpriteMeshType.FullRect, Vector4.zero, false") &&
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
            Require(playerSource.Contains("LockDeathPhysics();") &&
                    playerSource.Contains("body.linearVelocity = Vector2.zero;") &&
                    playerSource.Contains("body.simulated = false;") &&
                    playerSource.Contains("RestoreDeathPhysics();") &&
                    playerSource.Contains("body.simulated = bodySimulationBeforeDeath;"),
                "Player death must stop and suspend body physics until the respawn fade completes.");
            Require(playerSource.Contains("CancelAttackFeedback();") &&
                    playerSource.Contains("attackIndicatorRemaining = 0f;") &&
                    playerSource.Contains("attackIndicatorFrameRemaining = 0f;") &&
                    playerSource.Contains("attackIndicator.enabled = false;"),
                "Player death must cancel a claw effect instead of pausing it until respawn.");
            var attackSpriteCenter = new Vector2(0f, .65f);
            var eightDirections = new[]
            {
                Vector2.right,
                new Vector2(1f, 1f),
                Vector2.up,
                new Vector2(-1f, 1f),
                Vector2.left,
                new Vector2(-1f, -1f),
                Vector2.down,
                new Vector2(1f, -1f)
            };
            for (var index = 0; index < eightDirections.Length; index++)
            {
                var direction =
                    MainGamePlayerController.SnapAttackFeedbackDirection(eightDirections[index]);
                var localPosition = MainGamePlayerController.CalculateAttackFeedbackLocalPosition(
                    direction, attackSpriteCenter);
                var angle =
                    MainGamePlayerController.CalculateAttackFeedbackRotationDegrees(direction);
                var renderedCenter = localPosition +
                                     (Vector2)(Quaternion.Euler(0f, 0f, angle) *
                                               (Vector3)attackSpriteCenter);
                var expectedCenter = Vector2.up * .65f + direction * .85f;
                Require(Vector2.Distance(renderedCenter, expectedCenter) <= .0001f,
                    $"Claw effect direction {index} must occupy its matching slot around the player.");
            }
            Require(Mathf.Abs(Mathf.DeltaAngle(
                        MainGamePlayerController.CalculateAttackFeedbackRotationDegrees(Vector2.right),
                        -90f)) <= .0001f &&
                    Mathf.Abs(Mathf.DeltaAngle(
                        MainGamePlayerController.CalculateAttackFeedbackRotationDegrees(Vector2.up),
                        0f)) <= .0001f &&
                    Mathf.Abs(Mathf.DeltaAngle(
                        MainGamePlayerController.CalculateAttackFeedbackRotationDegrees(Vector2.left),
                        90f)) <= .0001f &&
                    Mathf.Abs(Mathf.DeltaAngle(
                        MainGamePlayerController.CalculateAttackFeedbackRotationDegrees(Vector2.down),
                        180f)) <= .0001f,
                "All eight claw directions must share the corrected clockwise art orientation.");
            Require(playerSource.Contains("attackIndicator.flipY = true;") &&
                    playerSource.Contains(
                        "if (attackIndicator.flipY) renderedSpriteCenter.y = -renderedSpriteCenter.y;"),
                "The rotated claw art must use its source Y axis for the requested screen-space horizontal mirror.");
            var asymmetricCenter = new Vector2(.1f, .65f);
            var mirroredCenter = new Vector2(asymmetricCenter.x, -asymmetricCenter.y);
            var mirroredLocalPosition =
                MainGamePlayerController.CalculateAttackFeedbackLocalPosition(
                    Vector2.right, asymmetricCenter, mirroredCenter);
            var mirroredRenderedCenter = mirroredLocalPosition +
                                         (Vector2)(Quaternion.Euler(0f, 0f, -90f) *
                                                   (Vector3)mirroredCenter);
            Require(Vector2.Distance(
                        mirroredRenderedCenter,
                        Vector2.up * .65f +
                        Vector2.right * (.85f + asymmetricCenter.x)) <= .0001f,
                "Mirroring the claw must not move its visible center away from the attack slot.");
            var shortFrameCenter = new Vector2(0f, .1f);
            var shortFrameLocalPosition =
                MainGamePlayerController.CalculateAttackFeedbackLocalPosition(
                    Vector2.right, shortFrameCenter, new Vector2(0f, -.1f));
            var shortFrameRenderedCenter = shortFrameLocalPosition +
                                           (Vector2)(Quaternion.Euler(0f, 0f, -90f) *
                                                     new Vector3(0f, -.1f));
            Require(Mathf.Approximately(shortFrameRenderedCenter.y, .65f),
                "Every claw frame must stay at the fixed hand-height origin regardless of its trimmed pivot.");
            Require(playerSource.Contains(
                        "attack.Strike(SnapAttackFeedbackDirection(facing))") &&
                    playerSource.Contains("var attackOrigin = playerOrigin;") &&
                    playerSource.Contains("AttackFeedbackOriginHeight") &&
                    !playerSource.Contains("MiningCellPickOffsets"),
                "Claw combat and mining must share the physics origin while art keeps a presentation-only height offset.");
            Require(playerSource.Contains("frames.Count - 1") &&
                    playerSource.Contains("attackIndicatorFrameIndex--") &&
                    playerSource.Contains("attackIndicatorFrameIndex > 0"),
                "The claw effect must animate from its outer strokes toward the central X frame.");
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
        var combatCatalog = AssetDatabase.LoadAssetAtPath<GameDataCatalog>(
            "Assets/Data/SO/GameDataCatalog.asset");
        Require(MainGamePlayerController.ResolveFanAbilityDamage(FanItemIds.Hapjukseon) == 0 &&
                MainGamePlayerController.ResolveFanAbilityDamage(FanItemIds.Cheolseon) == 4 &&
                WireSnareAbility.HapjukseonDamage == 0 &&
                WireSnareAbility.CheolseonDamage == 4,
            "Hapjukseon must push without damage while Cheolseon retains its four-damage fan ability.");
        Require(combatCatalog != null &&
                !MainGamePlayerController.AllowsPlayerBasicAttack(
                    combatCatalog.FindCombatProfile(FanItemIds.Hapjukseon)) &&
                MainGamePlayerController.AllowsPlayerBasicAttack(
                    combatCatalog.FindCombatProfile(FanItemIds.Cheolseon)) &&
                Mathf.Approximately(
                    combatCatalog.FindCombatProfile(FanItemIds.Cheolseon).KnockbackTiles, 1.5f) &&
                MainGamePlayerController.AllowsPlayerBasicAttack(
                    combatCatalog.FindCombatProfile("dokkaebi_club")),
            "Hapjukseon must remain right-click-only while Cheolseon keeps its 1.5-tile left-click knockback.");
        var upwardDiagonalTiles = new TileData[6, 6];
        upwardDiagonalTiles[3, 2] = new TileData
            { elementType = WorldTileTypes.Stone, hardness = 1 };
        upwardDiagonalTiles[3, 3] = new TileData
            { elementType = WorldTileTypes.Stone, hardness = 1 };
        var upwardDiagonalService = new TileService(upwardDiagonalTiles, null, null, 1);
        Require(MainGamePlayerController.TryPickMiningCell(
                    upwardDiagonalService, new Vector2(2.9f, 2.4f),
                    new Vector2(3.5f, 3.5f), new Vector2(.6f, 1.1f),
                    1.5f, out var upwardDiagonalCell) &&
                upwardDiagonalCell == new Vector3Int(3, 3, 0),
            "A cursor over an upper-diagonal tile must not be intercepted by the nearer side tile.");
        Require(MainGamePlayerController.TryPickMiningCell(
                    upwardDiagonalService, new Vector2(2.9f, 2.4f),
                    null, new Vector2(.6f, 1.1f),
                    1.5f, out var fallbackDiagonalCell) &&
                fallbackDiagonalCell == new Vector3Int(3, 3, 0),
            "Eight-direction mining over air must prefer the intended diagonal neighbor before ray order.");

        var downwardDiagonalTiles = new TileData[6, 6];
        downwardDiagonalTiles[3, 3] = new TileData
            { elementType = WorldTileTypes.Stone, hardness = 1 };
        downwardDiagonalTiles[3, 2] = new TileData
            { elementType = WorldTileTypes.Stone, hardness = 1 };
        var downwardDiagonalService = new TileService(downwardDiagonalTiles, null, null, 2);
        Require(MainGamePlayerController.TryPickMiningCell(
                    downwardDiagonalService, new Vector2(2.9f, 3.6f),
                    new Vector2(3.5f, 2.5f), new Vector2(.6f, -1.1f),
                    1.5f, out var downwardDiagonalCell) &&
                downwardDiagonalCell == new Vector3Int(3, 2, 0),
            "A cursor over a lower-diagonal tile must not be intercepted by the player's nearer support-side tile.");

        const int isolatedPhysicsLayer = 31;
        var isolatedPhysicsMask = (LayerMask)(1 << isolatedPhysicsLayer);
        var root = new GameObject("MeleeArcPhysicsQueryContract");
        try
        {
            var attacker = new GameObject("PlayerAttacker", typeof(Health), typeof(MeleeArcAttack));
            attacker.layer = isolatedPhysicsLayer;
            attacker.transform.SetParent(root.transform, false);
            var attackerHealth = attacker.GetComponent<Health>();
            attackerHealth.ConfigureForRuntime(100);

            var selfHurtbox = new GameObject("PlayerHurtbox", typeof(BoxCollider2D));
            selfHurtbox.layer = isolatedPhysicsLayer;
            selfHurtbox.transform.SetParent(attacker.transform, false);
            selfHurtbox.transform.localPosition = new Vector3(.25f, 0f, 0f);
            selfHurtbox.GetComponent<BoxCollider2D>().isTrigger = true;

            var yokai = new GameObject("GroundYokai", typeof(Health), typeof(BoxCollider2D));
            yokai.layer = isolatedPhysicsLayer;
            yokai.transform.SetParent(root.transform, false);
            yokai.transform.position = new Vector3(.75f, 0f, 0f);
            var yokaiHealth = yokai.GetComponent<Health>();
            yokaiHealth.ConfigureForRuntime(100);
            var yokaiDamageEvents = 0;
            yokaiHealth.Damaged += (_, __) => yokaiDamageEvents++;

            var yokaiHurtbox = new GameObject("GroundYokaiHurtbox", typeof(BoxCollider2D));
            yokaiHurtbox.layer = isolatedPhysicsLayer;
            yokaiHurtbox.transform.SetParent(yokai.transform, false);
            yokaiHurtbox.transform.localPosition = new Vector3(.05f, 0f, 0f);
            yokaiHurtbox.GetComponent<BoxCollider2D>().isTrigger = true;

            var boss = new GameObject("BossTarget", typeof(Health), typeof(CircleCollider2D));
            boss.layer = isolatedPhysicsLayer;
            boss.transform.SetParent(root.transform, false);
            boss.transform.position = new Vector3(2.8f, .1f, 0f);
            boss.GetComponent<CircleCollider2D>().radius = .2f;
            var bossHealth = boss.GetComponent<Health>();
            bossHealth.ConfigureForRuntime(200);
            var bossDamageEvents = 0;
            bossHealth.Damaged += (_, __) => bossDamageEvents++;
            var bossSpriteEdge = new GameObject("BossSpriteEdgeHurtbox", typeof(BoxCollider2D));
            bossSpriteEdge.layer = isolatedPhysicsLayer;
            bossSpriteEdge.transform.SetParent(boss.transform, false);
            bossSpriteEdge.transform.localPosition = new Vector3(-1f, 0f, 0f);
            bossSpriteEdge.GetComponent<BoxCollider2D>().size = new Vector2(.4f, 1f);
            bossSpriteEdge.GetComponent<BoxCollider2D>().isTrigger = true;

            var rearTarget = new GameObject("RearTarget", typeof(Health), typeof(BoxCollider2D));
            rearTarget.layer = isolatedPhysicsLayer;
            rearTarget.transform.SetParent(root.transform, false);
            rearTarget.transform.position = new Vector3(-.6f, 0f, 0f);
            var rearHealth = rearTarget.GetComponent<Health>();
            rearHealth.ConfigureForRuntime(100);

            var distantTarget = new GameObject("DistantTarget", typeof(Health), typeof(BoxCollider2D));
            distantTarget.layer = isolatedPhysicsLayer;
            distantTarget.transform.SetParent(root.transform, false);
            distantTarget.transform.position = new Vector3(3f, 0f, 0f);
            var distantHealth = distantTarget.GetComponent<Health>();
            distantHealth.ConfigureForRuntime(100);

            var attack = attacker.GetComponent<MeleeArcAttack>();
            attack.ConfigureForRuntime(attacker.transform, isolatedPhysicsMask,
                attackRange: 2f, attackArc: 120f, attackDamage: 10, attackKnockback: 0f);
            Physics2D.SyncTransforms();
            attack.Strike(Vector2.right);

            Require(attackerHealth.Current == 100 && yokaiHealth.Current == 90 && bossHealth.Current == 190 &&
                    rearHealth.Current == 100 && distantHealth.Current == 100,
                "A melee swing must damage only forward in-range yokai and boss targets, never self or excluded targets.");
            Require(attack.LastHitCount == 2 && yokaiDamageEvents == 1 && bossDamageEvents == 1,
                $"The isolated melee query must report exactly two targets and one damage event per target. " +
                $"hits={attack.LastHitCount}, yokaiEvents={yokaiDamageEvents}, bossEvents={bossDamageEvents}.");

            yokaiHealth.ConfigureForRuntime(100);
            bossHealth.ConfigureForRuntime(200);
            attack.Strike(Vector2.right, WireSnareAbility.HapjukseonDamage,
                WireSnareAbility.Knockback);
            Require(attack.LastHitCount == 2 && yokaiHealth.Current == 100 && bossHealth.Current == 200 &&
                    yokaiDamageEvents == 1 && bossDamageEvents == 1,
                "A zero-damage Hapjukseon swing must still resolve its targets without applying damage.");
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
        Require(!PlayerMovementPhysics.TryLoadFromCatalog(catalog, out _),
            "The v34 product catalog must not retain the removed player-physics globals.");
        var physics = PlayerMovementPhysics.CreateDefault();
        Require(Mathf.Approximately(physics.JumpHeightTiles, PlayerMovementPhysics.DefaultJumpHeightTiles) &&
                Mathf.Approximately(physics.Gravity, PlayerMovementPhysics.DefaultGravity) &&
                Mathf.Approximately(physics.MaxFallSpeed, PlayerMovementPhysics.DefaultMaxFallSpeed) &&
                Mathf.Approximately(physics.JumpCutMultiplier, PlayerMovementPhysics.DefaultJumpCut),
            "The merged player controller must use the v34 code-owned movement defaults.");

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

            var centeredPlayerBounds = new Bounds(new Vector3(.5f, .5f), new Vector3(.76f, .76f));
            var edgeStraddlingBounds = new Bounds(new Vector3(.95f, .5f), new Vector3(.2f, .76f));
            Require(MainGamePlayerController.BoundsOverlapCell(
                        centeredPlayerBounds, Vector3.zero, new Vector3(1f, 1f)) &&
                    !MainGamePlayerController.BoundsOverlapCell(
                        centeredPlayerBounds, new Vector3(1f, 0f), new Vector3(2f, 1f)) &&
                    MainGamePlayerController.BoundsOverlapCell(
                        edgeStraddlingBounds, Vector3.zero, new Vector3(1f, 1f)) &&
                    MainGamePlayerController.BoundsOverlapCell(
                        edgeStraddlingBounds, new Vector3(1f, 0f), new Vector3(2f, 1f)),
                "Foreground placement must reject every cell actually overlapped by the player " +
                "without treating boundary-only contact as penetration.");

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
            var fallBounceVelocity = MainGamePlayerController.CalculateBossAirborneVelocity(
                MainGamePlayerController.FallDamageBounceHeightTiles, physics.Gravity);
            var fallBouncePeak = fallBounceVelocity * fallBounceVelocity / (2f * physics.Gravity);
            Require(Mathf.Approximately(MainGamePlayerController.FallDamageBounceHeightTiles, .5f) &&
                    Mathf.Abs(fallBouncePeak - .5f) <= .001f,
                "A surviving fall-damage landing must bounce the player approximately half a tile.");

            var fallingVelocity = 0f;
            for (var step = 0; step < 240; step++)
                fallingVelocity = MainGamePlayerController.ApplyGravity(fallingVelocity,
                    physics.Gravity, physics.MaxFallSpeed, fixedDeltaSeconds);
            Require(Mathf.Approximately(fallingVelocity, -physics.MaxFallSpeed) &&
                    Mathf.Approximately(MainGamePlayerController.CalculateHorizontalVelocity(2f, 6f), 6f),
                "Dynamic player movement must clamp both terminal fall speed and horizontal input.");

            var threshold = catalog.FindGlobal("fall_damage_threshold_tiles");
            var perTile = catalog.FindGlobal("fall_damage_per_tile");
            var thresholdTiles = 0f;
            var damagePerTile = 0f;
            Require(threshold != null && threshold.TryGetFloat(out thresholdTiles) &&
                    Mathf.Approximately(thresholdTiles, 7f) &&
                    perTile != null && perTile.TryGetFloat(out damagePerTile) &&
                    Mathf.Approximately(damagePerTile, .5f),
                "The merged player controller must load the v34 fall-damage globals.");
            Require(Mathf.Approximately(
                        MainGamePlayerController.CalculateFallDamage(6f, thresholdTiles, damagePerTile), 0f) &&
                    Mathf.Approximately(
                        MainGamePlayerController.CalculateFallDamage(7f, thresholdTiles, damagePerTile), 3.5f) &&
                    Mathf.Approximately(
                        MainGamePlayerController.CalculateFallDamage(20f, thresholdTiles, damagePerTile), 10f) &&
                    Mathf.Approximately(
                        MainGamePlayerController.CalculateFallDamage(135f, thresholdTiles, damagePerTile), 67.5f) &&
                    MainGamePlayerController.CalculateAppliedFallDamage(
                        7f, thresholdTiles, damagePerTile) == 4,
                "Fall damage must be zero through six tiles, then use the full fall height at 0.5 HP per tile.");

            var fallHealthObject = new GameObject("FallDamageIgnoresArmor", typeof(Health));
            try
            {
                var fallHealth = fallHealthObject.GetComponent<Health>();
                fallHealth.ConfigureForRuntime(100, 99);
                fallHealth.ApplyDamage(
                    MainGamePlayerController.CalculateAppliedFallDamage(
                        20f, thresholdTiles, damagePerTile),
                    Nyangbingo.Core.DamageTag.Fall,
                    Nyangbingo.Core.DamageDelivery.Environmental);
                Require(fallHealth.Current == 90,
                    "Terrain fall damage must bypass equipment defense.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fallHealthObject);
            }

            var playerSource = System.IO.File.ReadAllText(
                "Assets/Scripts/Nyangbingo/World/MainGamePlayerController.cs");
            Require(playerSource.Contains("BeginFallTracking") &&
                    playerSource.Contains("ResolveFallLanding") &&
                    playerSource.Contains("DamageTag.Fall, DamageDelivery.Environmental") &&
                    playerSource.Contains(
                        "FallDamageBounceHeightTiles, gravityAcceleration") &&
                    playerSource.Contains("var jumpHeld = fallDamageBounceAscending ||") &&
                    playerSource.Contains(
                        "body.linearVelocity = new Vector2(body.linearVelocity.x, verticalVelocity)") &&
                    playerSource.Contains("BeginFallTracking(landingWorldY)") &&
                    playerSource.Contains("bootstrap.WorldReady += RebindForegroundPlacementBlocker") &&
                    playerSource.Contains("SetForegroundPlacementBlocker(") &&
                    playerSource.Contains("playerCollider.bounds"),
                "Ground jumps, double jumps, ledge falls, and landings must remain connected to fall tracking.");
            var effectSource = System.IO.File.ReadAllText(
                "Assets/Scripts/Nyangbingo/World/MainGameEffectPresenter.cs");
            Require(effectSource.Contains("CreatePlayerDamagePopup(amount)") &&
                    effectSource.Contains("new GameObject(\"PlayerDamagePopup\")"),
                "Player fall damage must reuse the numeric world damage popup presentation.");
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
        Require(source.Contains("AlignSurfaceVisual(tree.Renderer, supportCell)") &&
                source.Contains("AlignSurfaceVisual(renderer, supportCell)") &&
                source.Contains("AlignVisualToCellBase(renderer, chestCell)") &&
                !source.Contains("TreeVegetationVisualOffset"),
            "Surface vegetation and chests must use shared pivot-aware cell-base placement instead of fixed offsets.");
        Require(!source.Contains("PlaceSurfaceGroundCover(result, random)") &&
                source.Contains("renderer.sprite = artCatalog.Find(\"hemp\")?.Sprite;") &&
                source.Contains(".BoundItemArtCatalog?.FindSprite(PlayerHealthRecoveryService.CatnipItemId)") &&
                source.Contains("FindMineralTier(HempItemId)") &&
                source.Contains("TryHarvestHemp(") &&
                source.Contains("ExportHempPatches()") &&
                source.Contains("IsNearSurfaceDecoration(supportCell, 2f)") &&
                source.Contains("surfaceDecorationSupportCells.Add(supportCell);") &&
                source.Contains("WorldItemDropRequest.Request(") &&
                source.Contains("bootstrap?.GameDataCatalog?.FindItem(HempItemId), 1, dropPosition") &&
                !source.Contains("PlacePlantPatch("),
            "Catnip must use its delivered icon while natural hemp avoids trees, remains harvestable, and drops when support is mined.");
        var playerSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/World/MainGamePlayerController.cs");
        var saveSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/Save/MainGameSaveCoordinator.cs");
        var tileServiceSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/World/TileService.cs");
        var mapGeneratorSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/World/MapGenerator.cs");
        Require(playerSource.Contains("TryUseSelectedCatnip() ||") &&
                playerSource.Contains("tilePalette.SelectedItemId != PlayerHealthRecoveryService.CatnipItemId") &&
                playerSource.Contains("recovery.TryUseCatnip(out var restoredHealth)") &&
                playerSource.Contains("TryHarvestNearbyCatnip() ||") &&
                playerSource.Contains("TryHarvestNearbyHemp() ||") &&
                source.Contains("var shouldDrop = patch.HarvestedDay == 0;") &&
                source.Contains("FindItem(PlayerHealthRecoveryService.CatnipItemId)") &&
                saveSource.Contains("save.hempPatches = worldDecorationRenderer.ExportHempPatches();") &&
                saveSource.Contains("worldDecorationRenderer.RestoreHempPatches(save.hempPatches)"),
            "E interaction must use selected hotbar catnip, while mined support drops live catnip and preserves independent hemp harvesting.");
        Require(source.Contains("decorationSupportCells[visual.transform] = supportCell;") &&
                source.Contains("RemoveDecorationsSupportedBy(cell);") &&
                source.Contains("visual.gameObject.SetActive(false);") &&
                source.Contains("bootstrap?.TileService?.GetTile(supportCell).IsAir != false"),
            "Trees must disappear with their supporting terrain and stay absent when a mined world is rebuilt.");
        Require(source.Contains("FindMineralTier(WoodItemId)") &&
                source.Contains("TryResolveTreeMiningTarget(") &&
                source.Contains("TryHarvestTree(") &&
                source.Contains("ExportHarvestedTrees()") &&
                source.Contains("for (var height = 1; height <= 2; height++)") &&
                source.Contains("candidateCell = tree.SupportCell + Vector3Int.up * height") &&
                source.Contains("bootstrap?.GameDataCatalog?.FindItem(WoodItemId), 1, dropPosition") &&
                playerSource.Contains("TryTickTreeMining(") &&
                playerSource.Contains("FindMineralTier(MainGameWorldDecorationRenderer.WoodItemId)") &&
                playerSource.Contains("CompleteTreeMining(") &&
                playerSource.Contains("CompleteTreeMining(miningTreeId, miningCell, clawTier)") &&
                saveSource.Contains("save.harvestedTrees = worldDecorationRenderer.ExportHarvestedTrees();") &&
                saveSource.Contains("worldDecorationRenderer.RestoreHarvestedTrees(save.harvestedTrees)") &&
                source.Contains("IsValidPersistedCoordinateDecorationId(record.treeId, \"tree_\", treePatches)"),
            "Both tree cells must mine along the claw direction, show progress at the hit cell, drop wood with lost support, and preserve harvested state.");
        Require(source.Contains("FindMineralTier(RebarItemId)") &&
                source.Contains("FrequencyPerHundredTiles") &&
                source.Contains("SpawnRebarPatch(") &&
                source.Contains("TryResolveRebarMiningTarget(") &&
                source.Contains("TryHarvestRebar(") &&
                source.Contains("ExportHarvestedRebar()") &&
                source.Contains("bootstrap?.GameDataCatalog?.FindItem(RebarItemId), 1, dropPosition") &&
                playerSource.Contains("TryTickRebarMining(") &&
                playerSource.Contains("FindMineralTier(MainGameWorldDecorationRenderer.RebarItemId)") &&
                playerSource.Contains("CompleteRebarMining(") &&
                saveSource.Contains("save.harvestedRebar = worldDecorationRenderer.ExportHarvestedRebar();") &&
                saveSource.Contains("worldDecorationRenderer.RestoreHarvestedRebar(save.harvestedRebar)") &&
                source.Contains("IsValidPersistedCoordinateDecorationId(record.rebarId, \"rebar_\", rebarPatches)") &&
                saveSource.Contains("RestoreStage(\"harvested rebar\""),
            "Exposed ruins must generate, mine, drop, and persist rebar from the v34 resource row.");
        Require(source.Contains("IsForegroundPlacementBlocked(Vector3Int cell)") &&
                source.Contains("chestCells.Contains(cell)") &&
                source.Contains("patch.SupportCell + Vector3Int.up == cell && IsCatnipAvailable(patch)") &&
                source.Contains("IsChestPlantCell(result, candidate)") &&
                source.Contains("GetTile(patch.SupportCell + Vector3Int.up).IsAir == true") &&
                source.Contains("RefreshCatnipAvailability();") &&
                source.Contains("tree.SupportCell + Vector3Int.up * 2 == cell") &&
                tileServiceSource.Contains("SetForegroundPlacementBlocker(") &&
                tileServiceSource.Contains("!IsForegroundPlacementBlocked(cell)") &&
                mapGeneratorSource.Contains("EnsureChestCellsHaveNoForeground(grid, structures.chests)") &&
                mapGeneratorSource.Contains("protectedAir[chest.position.x, chest.position.y] = true"),
            "Chests and live natural resources must reserve occupied cells, while harvested catnip allows placement and cannot respawn through that block.");
    }

    private static void TestWorldDropVisualSurfaceOffset()
    {
        Require(Mathf.Approximately(MainGameWorldDropRuntime.VisualSurfaceOffset, 0f),
            "World-drop visuals must share the corrected logical foreground surface without a legacy half-tile lift.");
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
        Require(!globalsSource.Contains("요괴 점프 추격 없어"),
            "The v34 globals notes must not contradict the code-owned grounded yokai and boss step-jump contract.");

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
            typeof(Rigidbody2D), typeof(WorldMobPhysicsBody), typeof(Health));
        var flyingObject = new GameObject("FlyingMobPhysicsContract", typeof(CircleCollider2D),
            typeof(Rigidbody2D), typeof(WorldMobPhysicsBody), typeof(Health));
        GameObject movementObstacle = null;
        try
        {
            var ground = groundObject.GetComponent<WorldMobPhysicsBody>();
            var flying = flyingObject.GetComponent<WorldMobPhysicsBody>();
            var groundCollider = groundObject.GetComponent<CircleCollider2D>();
            groundCollider.radius = .42f;
            var groundRenderer = groundObject.AddComponent<SpriteRenderer>();
            var visualTexture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            var bottomPivotSprite = Sprite.Create(
                visualTexture, new Rect(0f, 0f, 8f, 8f), new Vector2(.5f, 0f), 8f);
            groundRenderer.sprite = bottomPivotSprite;
            Require(Mathf.Approximately(
                    RuntimeCharacterSpriteAnimator.CalculateGroundedVisualLocalY(
                        groundCollider, groundRenderer), -.42f),
                "A bottom-pivot grounded character visual must attach to the movement collider's lower edge.");
            ground.ConfigureForRuntime(WorldMobLocomotion.Grounded);
            flying.ConfigureForRuntime(WorldMobLocomotion.Flying);
            Require(groundObject.GetComponent<Rigidbody2D>().bodyType == RigidbodyType2D.Dynamic &&
                    flyingObject.GetComponent<Rigidbody2D>().bodyType == RigidbodyType2D.Kinematic &&
                    groundObject.GetComponent<Rigidbody2D>().gravityScale > 0f &&
                    Mathf.Approximately(flyingObject.GetComponent<Rigidbody2D>().gravityScale, 0f) &&
                    !groundObject.GetComponent<Collider2D>().isTrigger &&
                    !flyingObject.GetComponent<Collider2D>().isTrigger &&
                    ground.NavigationOffset(new Vector2(2f, 3f)) == new Vector2(2f, 3f) &&
                    flying.NavigationOffset(new Vector2(2f, 3f)) == new Vector2(2f, 3f),
                "Both locomotion types must retain vertical target separation for navigation distance, while grounded movement remains horizontal.");
            Require(Physics2D.GetIgnoreCollision(groundObject.GetComponent<Collider2D>(),
                    flyingObject.GetComponent<Collider2D>()),
                "Yokai and bosses must ignore mutual physical response so faster mobs are never slowed by the mob ahead.");
            var groundBody = groundObject.GetComponent<Rigidbody2D>();
            var flyingKnockbackBody = flyingObject.GetComponent<Rigidbody2D>();
            // Keep the physics-only contract outside the loaded gameplay world's colliders.
            // Running this menu test during Play Mode previously put the temporary bodies at
            // world origin, so terrain depenetration was incorrectly counted as knockback.
            var knockbackTestOrigin = new Vector2(10000f, 10000f);
            groundBody.position = knockbackTestOrigin;
            flyingKnockbackBody.position = knockbackTestOrigin + Vector2.right * 4f;
            Physics2D.SyncTransforms();
            Require(groundObject.GetComponent<Health>().TryApplyKnockback(Vector2.right * .5f) &&
                    flyingObject.GetComponent<Health>().TryApplyKnockback(Vector2.right * .5f) &&
                    Mathf.Approximately(groundBody.position.x, knockbackTestOrigin.x) &&
                    Mathf.Approximately(flyingKnockbackBody.position.x, knockbackTestOrigin.x + 4f) &&
                    ground.IsKnockbackActive && flying.IsKnockbackActive,
                "Fan knockback must begin as a timed displacement instead of teleporting the yokai.");
            var halfKnockbackSeconds = WorldMobPhysicsBody.KnockbackDurationSeconds * .5f;
            ground.TickKnockback(halfKnockbackSeconds);
            flying.TickKnockback(halfKnockbackSeconds);
            Require(groundBody.position.x > knockbackTestOrigin.x &&
                    groundBody.position.x < knockbackTestOrigin.x + .5f &&
                    flyingKnockbackBody.position.x > knockbackTestOrigin.x + 4f &&
                    flyingKnockbackBody.position.x < knockbackTestOrigin.x + 4.5f,
                "Timed fan knockback must visibly interpolate before reaching its destination.");
            ground.TickKnockback(WorldMobPhysicsBody.KnockbackDurationSeconds);
            flying.TickKnockback(WorldMobPhysicsBody.KnockbackDurationSeconds);
            Require(Mathf.Approximately(groundBody.position.x, knockbackTestOrigin.x + .5f) &&
                    Mathf.Approximately(flyingKnockbackBody.position.x, knockbackTestOrigin.x + 4.5f) &&
                    !ground.IsKnockbackActive && !flying.IsKnockbackActive,
                "Fan knockback must finish at the requested collision-safe distance for both locomotion types.");
            var ledgeSlide = WorldMobPhysicsBody.ProjectAlongCollisionSurface(
                new Vector2(1f, 1f), Vector2.left);
            Require(Mathf.Approximately(ledgeSlide.x, 0f) && ledgeSlide.y > .9f,
                "A flying yokai hitting a tile corner diagonally must keep the surface-parallel movement needed to rise over it.");

            movementObstacle = new GameObject(
                "MobKnockbackOverlapRecovery", typeof(BoxCollider2D));
            var overlapTestOrigin = knockbackTestOrigin + new Vector2(0f, 10f);
            movementObstacle.transform.position =
                overlapTestOrigin + new Vector2(2.5f, 3.5f);
            movementObstacle.GetComponent<BoxCollider2D>().size = Vector2.one;
            flyingKnockbackBody.position = overlapTestOrigin + new Vector2(1.65f, 3.5f);
            Physics2D.SyncTransforms();
            flying.ConfigureForRuntime(WorldMobLocomotion.Flying);
            Require(flying.TryApplyKnockback(Vector2.left * .5f),
                "A flying yokai touching a ledge must still accept fan knockback.");
            flying.TickKnockback(WorldMobPhysicsBody.KnockbackDurationSeconds);
            Require(flyingKnockbackBody.position.x < overlapTestOrigin.x + 1.15f &&
                    !flying.IsKnockbackActive,
                "A fan must push a flying yokai out of a shallow terrain overlap instead of leaving it frozen in the ledge.");
            UnityEngine.Object.DestroyImmediate(movementObstacle);
            movementObstacle = null;

            var spawnCells = new TileData[12, 10];
            for (var x = 0; x < 12; x++)
            for (var y = 0; y <= 5; y++)
                spawnCells[x, y] = new TileData
                {
                    elementType = WorldTileTypes.Stone,
                    hardness = 1,
                    isNaturalTerrain = true
                };
            for (var x = 2; x <= 9; x++)
            {
                spawnCells[x, 2] = TileData.CreateCaveAir(WorldTileTypes.BackgroundStone);
                spawnCells[x, 3] = TileData.CreateCaveAir(WorldTileTypes.BackgroundStone);
            }
            var spawnTiles = new TileService(spawnCells, null, null, 5);
            var unrestrictedSpawns = spawnTiles.GetValidSpawnPositions(
                new Vector3Int(6, 3, 0), 0, 20);
            var surfaceSpawns = spawnTiles.GetValidSurfaceSpawnPositions(
                new Vector3Int(6, 3, 0), 0, 20);
            Require(unrestrictedSpawns.Any(cell => cell.y == 2) &&
                    surfaceSpawns.Count > 0 && surfaceSpawns.All(cell => cell.y == 6),
                "Grounded encounter spawns must select the top natural surface instead of a valid cave floor.");

            var stepCells = new TileData[8, 6];
            for (var x = 0; x < 8; x++)
                stepCells[x, 0] = new TileData { elementType = WorldTileTypes.Stone, hardness = 1 };
            stepCells[3, 1] = new TileData { elementType = WorldTileTypes.Stone, hardness = 1 };
            var stepTiles = new TileService(stepCells, null, null, 3);
            groundObject.transform.position = new Vector3(2.5f, 1.5f, 0f);
            groundBody.position = new Vector2(2.5f, 1.5f);
            groundBody.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
            ground.ConfigureForRuntime(WorldMobLocomotion.Grounded, stepTiles);
            ground.Move(Vector2.right * .25f);
            Require(groundBody.linearVelocity.y > 8f,
                "A grounded yokai or boss must jump when pursuing across a clear one-tile step.");

            var tallWallCells = new TileData[8, 6];
            for (var x = 0; x < 8; x++)
                tallWallCells[x, 0] = new TileData { elementType = WorldTileTypes.Stone, hardness = 1 };
            tallWallCells[3, 1] = new TileData { elementType = WorldTileTypes.Stone, hardness = 1 };
            tallWallCells[3, 2] = new TileData { elementType = WorldTileTypes.Stone, hardness = 1 };
            groundObject.transform.position = new Vector3(2.5f, 1.5f, 0f);
            groundBody.position = new Vector2(2.5f, 1.5f);
            groundBody.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
            ground.ConfigureForRuntime(WorldMobLocomotion.Grounded,
                new TileService(tallWallCells, null, null, 4));
            ground.Move(Vector2.right * .25f);
            Require(groundBody.linearVelocity.y > 8f,
                "A grounded yokai blocked by a wall at least two tiles high must still attempt a jump.");

            var verticalRouteCells = new TileData[9, 8];
            for (var x = 0; x < 9; x++)
                verticalRouteCells[x, 0] =
                    new TileData { elementType = WorldTileTypes.Stone, hardness = 1 };
            verticalRouteCells[4, 1] =
                new TileData { elementType = WorldTileTypes.Stone, hardness = 1 };
            verticalRouteCells[5, 2] =
                new TileData { elementType = WorldTileTypes.Stone, hardness = 1 };
            for (var x = 1; x <= 4; x++)
                verticalRouteCells[x, 3] =
                    new TileData { elementType = WorldTileTypes.Stone, hardness = 1 };
            groundObject.transform.position = new Vector3(2.5f, 1.5f, 0f);
            groundBody.position = new Vector2(2.5f, 1.5f);
            groundBody.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
            ground.ConfigureForRuntime(
                WorldMobLocomotion.Grounded,
                new TileService(verticalRouteCells, null, null, 5));
            var verticalRouteDirection = ground.NavigationDirection(new Vector2(0f, 3f));
            Require(verticalRouteDirection.x > .9f &&
                    Mathf.Approximately(verticalRouteDirection.y, 0f) &&
                    ground.HasTraversableGroundRoute &&
                    ground.NavigationFacingDirection.x > .9f,
                "A grounded yokai sharing the target's X coordinate on another level must route toward the reachable staircase instead of stopping.");
            groundObject.transform.position = new Vector3(3.8f, 2.15f, 0f);
            groundBody.position = new Vector2(3.8f, 2.15f);
            Physics2D.SyncTransforms();
            var airborneStepDirection = ground.NavigationDirection(
                new Vector2(2.5f - groundBody.position.x, 4.5f - groundBody.position.y));
            Require(Vector2.Dot(verticalRouteDirection, airborneStepDirection) > .9f,
                "A grounded yokai crossing a step waypoint while airborne must keep its route direction instead of turning back toward the crossed cell center.");

            var dropRouteCells = new TileData[9, 8];
            for (var x = 0; x < 9; x++)
                dropRouteCells[x, 0] =
                    new TileData { elementType = WorldTileTypes.Stone, hardness = 1 };
            for (var x = 1; x <= 5; x++)
                dropRouteCells[x, 4] =
                    new TileData { elementType = WorldTileTypes.Stone, hardness = 1 };
            groundObject.transform.position = new Vector3(3.5f, 5.5f, 0f);
            groundBody.position = new Vector2(3.5f, 5.5f);
            groundBody.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
            ground.ConfigureForRuntime(
                WorldMobLocomotion.Grounded,
                new TileService(dropRouteCells, null, null, 7));
            var dropRouteDirection = ground.NavigationDirection(new Vector2(0f, -4f));
            var retainedDropRouteDirection =
                ground.NavigationDirection(new Vector2(.5f, -4f));
            Require(Mathf.Abs(dropRouteDirection.x) > .9f &&
                    Mathf.Approximately(dropRouteDirection.y, 0f) &&
                    Vector2.Dot(dropRouteDirection, retainedDropRouteDirection) > .9f &&
                    ground.HasTraversableGroundRoute,
                "A grounded yokai above a same-X target must keep one platform route toward a reachable ledge instead of alternating equal exits.");
            var dropEdgeX = dropRouteDirection.x > 0f ? 6.1f : .9f;
            groundObject.transform.position = new Vector3(dropEdgeX, 5.5f, 0f);
            groundBody.position = new Vector2(dropEdgeX, 5.5f);
            Physics2D.SyncTransforms();
            var committedDropDirection = ground.NavigationDirection(
                new Vector2(3.5f - dropEdgeX, -4f));
            Require(Vector2.Dot(dropRouteDirection, committedDropDirection) > .9f,
                "A grounded yokai crossing into the air cell above its landing must keep the committed drop direction until gravity completes the fall.");

            groundBody.linearVelocity = new Vector2(1.25f, 2.5f);
            ground.SetEncounterPaused(true);
            Require(!groundObject.GetComponent<Rigidbody2D>().simulated &&
                    groundBody.linearVelocity == Vector2.zero,
                "Regular yokai paused for a summoned-boss encounter must freeze outside physics simulation so they cannot block the boss.");
            ground.SetEncounterPaused(false);
            Require(groundObject.GetComponent<Rigidbody2D>().simulated &&
                    groundBody.linearVelocity == new Vector2(1.25f, 2.5f),
                "Regular yokai must resume the exact pre-pause velocity when the summoned-boss encounter ends.");

            var passThroughPlayer = new GameObject("MobPassThroughPlayer", typeof(CircleCollider2D));
            var playerCollider = passThroughPlayer.GetComponent<CircleCollider2D>();
            var compositeHurtboxObject =
                new GameObject("CompositeMobHurtbox", typeof(BoxCollider2D));
            compositeHurtboxObject.transform.SetParent(groundObject.transform, false);
            var compositeHurtbox = compositeHurtboxObject.GetComponent<BoxCollider2D>();
            compositeHurtbox.isTrigger = true;
            ground.IgnoreCollisionWith(passThroughPlayer.transform);
            Require(Physics2D.GetIgnoreCollision(groundObject.GetComponent<Collider2D>(), playerCollider) &&
                    Physics2D.GetIgnoreCollision(compositeHurtbox, playerCollider),
                "Player and every composite mob collider must ignore physical response so movement cannot push yokai or bosses.");
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
            Require(detourDirection.x > .5f && Mathf.Abs(detourDirection.y) < .5f &&
                    flying.NavigationFacingDirection.x > .9f,
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

            var playerBarrierCells = new TileData[5, 4];
            var basicWallCell = new Vector3Int(1, 1, 0);
            var ironWallCell = new Vector3Int(2, 1, 0);
            var naturalWallCell = new Vector3Int(3, 1, 0);
            playerBarrierCells[basicWallCell.x, basicWallCell.y] =
                new TileData { elementType = "insul_wall", hardness = 1 };
            playerBarrierCells[ironWallCell.x, ironWallCell.y] =
                new TileData { elementType = "iron_insul_wall", hardness = 2 };
            playerBarrierCells[naturalWallCell.x, naturalWallCell.y] =
                new TileData { elementType = WorldTileTypes.Stone, hardness = 1 };
            var playerBarrierTiles = new TileService(playerBarrierCells, null, null, 3);
            System.Func<YokaiWallMaterial, bool> regularWallDamage =
                material => material != YokaiWallMaterial.IronHeatWall;
            ground.ConfigureForRuntime(
                WorldMobLocomotion.Grounded, playerBarrierTiles, regularWallDamage);
            var groundRoutesThroughBreakableWall =
                ground.IsNavigationPassableCell(basicWallCell) &&
                !ground.IsNavigationPassableCell(ironWallCell) &&
                !ground.IsNavigationPassableCell(naturalWallCell);
            flying.ConfigureForRuntime(
                WorldMobLocomotion.Flying, playerBarrierTiles, regularWallDamage);
            Require(groundRoutesThroughBreakableWall &&
                    flying.IsNavigationPassableCell(basicWallCell) &&
                    !flying.IsNavigationPassableCell(ironWallCell) &&
                    !flying.IsNavigationPassableCell(naturalWallCell),
                "Grounded and flying yokai navigation must ignore only player barriers that the current yokai can actually destroy.");

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
                    encounterSource.Contains("GetValidSurfaceSpawnPositions(") &&
                    encounterSource.Contains("locomotion == WorldMobLocomotion.Grounded") &&
                    encounterSource.Contains("bootstrap.TileService") &&
                    encounterSource.Contains("new GameObject(\"BossHurtbox\")") &&
                    encounterSource.Contains("var movementColliderScale = BossScale;") &&
                    !encounterSource.Contains(
                        "locomotion == WorldMobLocomotion.Flying ? BossScale : 1f") &&
                    encounterSource.Contains("ConfigureDetachedHurtboxBody") &&
                    encounterSource.Contains("usesGroundedVisualRoot") &&
                    encounterSource.Contains("CalculateGroundedVisualLocalY") &&
                    !encounterSource.Contains("GroundBossVisualLift") &&
                    encounterSource.Contains(
                        "Body and tail hurtboxes are created after the movement core"),
                "MainGame encounter spawning must attach shared physics and ground every bottom-pivot visual at its collider foot.");
            var animatorSource = System.IO.File.ReadAllText(
                "Assets/Scripts/Nyangbingo/Yokai/YokaiBrain.cs");
            Require(animatorSource.Contains("characterAnimator?.SetMoving(true)") &&
                    animatorSource.Contains("physicsBody.NavigationFacingDirection") &&
                    animatorSource.Contains("characterAnimator?.SetFacing(facingMovement)") &&
                    !animatorSource.Contains("ResolveStableGroundFacing"),
                "Physics-driven yokai movement must keep animation active and face the selected route segment instead of collision displacement.");
            var physicsSource = System.IO.File.ReadAllText(
                "Assets/Scripts/Nyangbingo/World/WorldMobPhysicsBody.cs");
            Require(physicsSource.Contains("NavigationReversalHoldSeconds") &&
                    physicsSource.Contains("PathTargetCellTolerance") &&
                    physicsSource.Contains("groundPath[groundPathIndex]") &&
                    physicsSource.Contains("groundPath.Reverse()") &&
                    physicsSource.Contains("AdvanceGroundPath(startCell)") &&
                    physicsSource.Contains("groundDropCommitted") &&
                    physicsSource.Contains("Step jumps are also briefly airborne") &&
                    physicsSource.Contains("WithNavigationFacing") &&
                    physicsSource.Contains("Facing follows the selected route segment") &&
                    physicsSource.Contains("GroundRouteHeuristic") &&
                    physicsSource.Contains("TryAddGroundRouteNode") &&
                    physicsSource.Contains("targetActuallyCrossedBehind") &&
                    physicsSource.Contains("DirectPathConfirmationSeconds") &&
                    physicsSource.Contains(
                        "transform.GetComponentsInChildren<Collider2D>(true)") &&
                    physicsSource.Contains("var forwardProbe = (Vector2)bounds.center") &&
                    physicsSource.Contains(
                        "attachedCollider.bounds.min.y - GroundProbeDepth"),
                "Moving targets must not cause equal detours to alternate every frame, while real target crossings still reverse pursuit immediately.");
            Require(animatorSource.Contains("var hasClearAttackLine = physicsBody == null") &&
                    animatorSource.Contains("physicsBody?.HasTraversableGroundRoute == true") &&
                    physicsSource.Contains("NavigationFacingDirection"),
                "All yokai must keep routing across blocked floors without playing wall attacks and face their selected ground or flying route.");
            var imugiBodySource = System.IO.File.ReadAllText(
                "Assets/Scripts/Nyangbingo/World/RuntimeImugiBodyVisual.cs");
            Require(imugiBodySource.Contains("RigidbodyType2D.Kinematic") &&
                    imugiBodySource.Contains("EnsureDetachedBody") &&
                    imugiBodySource.Contains("segmentWorldPositions") &&
                    imugiBodySource.Contains("currentDistance > desiredDistance") &&
                    imugiBodySource.Contains("facing = Vector2.right"),
                "Imugi body hurtboxes must use detached kinematic bodies and follow the prior world-space trail instead of flipping instantly.");
            Require(encounterSource.Contains("definition.Kind == BossKind.Imugi") &&
                    encounterSource.Contains(
                        "definition.Kind == BossKind.Imugi ? \"imugi\" : definition.Id") &&
                    encounterSource.Contains("characterAnimator.SetFacing(Vector2.right)") &&
                    encounterSource.Contains("FindSprite(\"imugi_body\")") &&
                    encounterSource.Contains("FindSprite(\"imugi_pre_tail\")") &&
                    encounterSource.Contains("FindSprite(\"imugi_post_tail\")"),
                "The v34 Imugi boss must resolve its delivered head, body, pre-tail, and post-tail art and spawn facing right.");
            UnityEngine.Object.DestroyImmediate(bottomPivotSprite);
            UnityEngine.Object.DestroyImmediate(visualTexture);
        }
        finally
        {
            if (movementObstacle != null)
                UnityEngine.Object.DestroyImmediate(movementObstacle);
            UnityEngine.Object.DestroyImmediate(groundObject);
            UnityEngine.Object.DestroyImmediate(flyingObject);
        }
    }

    private static void TestLatestProductFlowContracts()
    {
        Require(GameShellController.ShouldEndDemoAtDawn(31, 30) &&
                !GameShellController.ShouldEndDemoAtDawn(30, 30) &&
                !GameShellController.ShouldEndDemoAtDawn(31, 0),
            "The day-30 demo must end at the following dawn regardless of the Imugi outcome.");

        var save = new SaveGame
        {
            sealPct = 87.5f,
            modulesDone = new System.Collections.Generic.List<string>
                { "module_a", "module_b", "module_a" },
            bossRecords = new System.Collections.Generic.List<BossRecord>
                { new BossRecord { bossId = "imugi_boss", count = 1, firstDay = 30 } },
            dogam = new System.Collections.Generic.List<CodexRecord>
                { new CodexRecord { yokaiId = "a", kills = 2 }, new CodexRecord { yokaiId = "b", kills = 3 } },
            stats = new RunStatsRecord { minedTiles = 41, deaths = 2 }
        };
        var result = GameShellController.BuildResult(save);
        Require(Mathf.Approximately(result.SealPercentage, 87.5f) &&
                result.CompletedModuleIds.Count == 2 && result.ImugiDefeated &&
                result.YokaiKills == 5 && result.MinedTiles == 41 && result.Deaths == 2,
            "The demo result must include seal, unique modules, Imugi outcome, kills, mining, and deaths.");

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
        var selectableEquipmentItem = ItemDefinition.CreateRuntime(
            "equipment_click_item", "Clickable Equipment", 1,
            ItemCategory.Equipment, ItemMvpScope.A);
        var selectableEquipment = EquipmentDefinition.CreateRuntime(
            selectableEquipmentItem.Id, EquipmentSlot.Head, false);
        Require(MainGameCraftingUiController.ResolveEquipmentEntryIndex(
                    selectableEquipmentItem.Id,
                    new[] { selectableEquipmentItem },
                    new[] { selectableEquipment }) == 0 &&
                MainGameCraftingUiController.ResolveEquipmentEntryIndex(
                    selectableEquipment.Id,
                    Array.Empty<ItemDefinition>(),
                    new[] { selectableEquipment }) == 0 &&
                MainGameCraftingUiController.ResolveEquipmentEntryIndex(
                    "missing", new[] { selectableEquipmentItem },
                    new[] { selectableEquipment }) == -1 &&
                craftingSource.Contains(
                    "button.onClick.AddListener(() => SelectEquipmentVisualSlot(capturedIndex))"),
            "Clicking a populated equipment visual slot must select its matching equipment entry.");
        UnityEngine.Object.DestroyImmediate(selectableEquipment);
        UnityEngine.Object.DestroyImmediate(selectableEquipmentItem);
        var codexSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/UI/MainGameCodexController.cs");
        var sceneCreatorSource = System.IO.File.ReadAllText(
            "Assets/Editor/NyangbingoMainGameSceneCreator.cs");
        Require(!craftingSource.Contains("4/ESC 닫기") &&
                !codexSource.Contains("4/ESC 닫기") &&
                !sceneCreatorSource.Contains("4/ESC 닫기"),
            "The codex title must not duplicate the close shortcut already shown by the bottom button.");
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
        GameObject alignedVisual = null;
        Texture2D alignmentTexture = null;
        Sprite alignmentSprite = null;
        try
        {
            renderer.EnsureWorldCoordinateContract();
            Require(tilemap.tileAnchor == Nyangbingo.World.TilemapRenderer.TerrainVisualAnchor &&
                    backgroundTilemap.tileAnchor ==
                    Nyangbingo.World.TilemapRenderer.TerrainVisualAnchor,
                "Bottom-pivot terrain art must render from the lower edge of its logical cell.");
            var cell = new Vector3Int(4, 7, 0);
            var center = renderer.GetCellCenterWorld(cell);
            var visualAnchor = renderer.GetCellVisualAnchorWorld(cell);
            Require(renderer.WorldToCell(center) == cell,
                "Tilemap cell center and world-to-cell conversion must be exact inverses.");
            var corners = new Vector3[4];
            renderer.GetCellWorldCorners(cell, corners);
            Require(Vector3.Distance((corners[0] + corners[2]) * .5f, center) < .0001f,
                "Seal marker corners must share the authoritative Tilemap cell center.");
            Require(Vector3.Distance((corners[0] + corners[1]) * .5f, visualAnchor) < .0001f,
                "Bottom-pivot tile effects must share the rendered terrain's lower visual anchor.");
            Require(Vector3.Distance(tilemap.GetCellCenterWorld(cell), center) > .1f,
                "Gameplay cell centers must not inherit the bottom-pivot terrain visual anchor.");
            var coordinateService = new TileService(new TileData[10, 10], renderer, null, 1);
            var serviceBounds = coordinateService.GetCellWorldBounds(cell);
            Require(Vector3.Distance(serviceBounds.center, center) < .0001f,
                "Mining and placement reach must use the same transformed Grid bounds as collision.");
            alignedVisual = new GameObject("BottomPivotCellVisual", typeof(SpriteRenderer));
            alignedVisual.transform.position = center;
            alignmentTexture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            alignmentSprite = Sprite.Create(
                alignmentTexture, new Rect(0f, 0f, 8f, 8f), new Vector2(.5f, 0f), 8f);
            var alignedRenderer = alignedVisual.GetComponent<SpriteRenderer>();
            alignedRenderer.sprite = alignmentSprite;
            coordinateService.AlignSpriteBoundsToCellBase(alignedRenderer, cell);
            Require(Mathf.Abs(alignedRenderer.bounds.center.x - serviceBounds.center.x) < .0001f &&
                    Mathf.Abs(alignedRenderer.bounds.min.y - serviceBounds.min.y) < .0001f,
                "Bottom-pivot world art must center horizontally and sit on the logical cell base.");
            var effectSource = System.IO.File.ReadAllText(
                "Assets/Scripts/Nyangbingo/World/MainGameEffectPresenter.cs");
            var playerSource = System.IO.File.ReadAllText(
                "Assets/Scripts/Nyangbingo/World/MainGamePlayerController.cs");
            var environmentSource = System.IO.File.ReadAllText(
                "Assets/Scripts/Nyangbingo/World/MainGameEnvironmentState.cs");
            var placementSource = System.IO.File.ReadAllText(
                "Assets/Scripts/Nyangbingo/World/MainGameTurretRuntime.cs");
            var decorationSource = System.IO.File.ReadAllText(
                "Assets/Scripts/Nyangbingo/World/MainGameWorldDecorationRenderer.cs");
            Require(effectSource.Contains(
                        "miningProgressRenderer.transform.position = CellVisualAnchor(cell)") &&
                    effectSource.Contains("miningEffect.transform.position = visualAnchor") &&
                    effectSource.Contains(
                        "playerTransform.GetComponentInChildren<RuntimeCharacterSpriteAnimator>()") &&
                    playerSource.Contains("new GameObject(\"Visual\")") &&
                    playerSource.Contains("CalculateGroundedVisualLocalY"),
                "Player art, claw effects, and mining cracks must follow the same grounded visual-anchor contract.");
            Require(environmentSource.Contains("new GameObject(\"Art\")") &&
                    environmentSource.Contains("AlignSpriteBoundsToCellBase(renderer, entry.Cell)") &&
                    placementSource.Contains("placementPreviewVisual") &&
                    placementSource.Contains("GetCellCenterWorld(placementCell)") &&
                    decorationSource.Contains("AlignSurfaceVisual(renderer, supportCell)") &&
                    decorationSource.Contains("AlignVisualToCellBase(renderer, chestCell)"),
                "Placed objects, previews, chests, and harvestable decorations must share the cell-base visual contract.");

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
            if (alignedVisual != null) UnityEngine.Object.DestroyImmediate(alignedVisual);
            if (alignmentSprite != null) UnityEngine.Object.DestroyImmediate(alignmentSprite);
            if (alignmentTexture != null) UnityEngine.Object.DestroyImmediate(alignmentTexture);
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
        Require(MainGameTurretRuntime.NearbyInteractionPrompt ==
                    "E · 상호작용    Shift+E · 회수" &&
                paletteSource.Contains("\"PlacedObjectInteractionPrompt\"") &&
                paletteSource.Contains("placementRuntime?.BindInteractionStatus(interactionPromptText)") &&
                Mathf.Approximately(
                    MainGameTilePaletteController.ResolveBottomStatusY(false),
                    MainGameTilePaletteController.BottomStatusBaseY) &&
                Mathf.Approximately(
                    MainGameTilePaletteController.ResolveBottomStatusY(true),
                    MainGameTilePaletteController.BottomStatusBaseY +
                    MainGameTilePaletteController.BottomStatusLineHeight),
            "Nearby placed-object controls must use the readable bottom prompt and stack older hotbar feedback one line above it.");
        Require(!turretSource.Contains("TryPlace(record, barrierActive: false)"),
            "Whitelisted insulation modules must remain eligible to seal after product placement.");
        Require(!turretSource.Contains("설치 거리가 너무 멉니다 · 최대") &&
                !paletteSource.Contains("설치 거리가 너무 멉니다 · 최대"),
            "Placement-distance feedback must not append the numeric tile limit.");
        Require(!paletteSource.Contains("R · 반경 표시"),
            "Narrative range-toggle status was reintroduced into the tile palette HUD.");
        Require(playerSource.Contains("MainGameHudController.BlocksWorldPrimaryInput"),
            "Clicking the top-right seal thermometer must block claw attacks and mining input.");
        Require(!playerSource.Contains("miningAllowedByLastSwing") &&
                System.Text.RegularExpressions.Regex.IsMatch(playerSource,
                    @"if \(attackCooldown <= 0f\)\s*TryBasicAttack\(\);\s*//[\s\S]{0,180}TickMining\(\);"),
            "A successful claw hit must not reset or suppress mining held on the same primary input.");
        Require(System.Text.RegularExpressions.Regex.IsMatch(playerSource,
                    @"Input\.GetKeyDown\(KeyCode\.E\)[\s\S]{0,600}TryOpenNearbyChest\(\)") &&
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
                Mathf.Approximately(MainGameHudController.BossHealthContentVerticalOffset("imugi_boss"), -6f) &&
                Mathf.Approximately(MainGameHudController.BossHealthValueScale("imugi_boss"), .85f) &&
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
        Require(MainGameHudController.BossHealthArtRow("imugi_boss") == 2,
            "Imugi must use the third Unity texture row of the boss health sheet.");
        Require(MainGameHudController.BossHealthArtRow("unknown") == -1,
            "Unknown bosses must not inherit another boss health frame.");
        var motherDefinition = AssetDatabase.LoadAssetAtPath<BossDefinition>(
            "Assets/Data/SO/Bosses/mother_bulgasari.asset");
        var motherBossCombatSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/Bosses/BossCombatController.cs");
        var motherRaidTargetSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/World/MainGameRaidTarget.cs");
        var motherEffectPresenterSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/World/MainGameEffectPresenter.cs");
        Require(motherDefinition != null &&
                Mathf.Approximately(motherDefinition.SpecialKnockbackTiles, 4f) &&
                motherDefinition.SpecialHasFireTag &&
                motherBossCombatSource.Contains(
                    "definition == null || definition.Kind != BossKind.MotherBulgasari") &&
                motherRaidTargetSource.Contains(
                    "MainGameEffectPresenter.BeginSuppressPlayerFireHitEffect()") &&
                motherEffectPresenterSource.Contains("suppressPlayerFireHitEffectDepth <= 0"),
            "Mother Bulgasari's fire-tagged special must launch the player four tiles without playing the head fire effect.");
        var bossHudSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/UI/MainGameHudController.cs");
        Require(bossHudSource.Contains("SpriteMeshType.FullRect, Vector4.zero, false"),
            "Runtime boss health crops must not request unreadable texture physics outlines.");
        Require(Mathf.Approximately(MainGameHudController.BossEntranceFlashDuration, 1.2f) &&
                MainGameHudController.BossEntranceFlashColor(.05f).a > 0f &&
                Mathf.Approximately(MainGameHudController.BossEntranceFlashColor(.15f).a, 0f) &&
                MainGameHudController.BossEntranceFlashColor(.25f).a > 0f &&
                Mathf.Approximately(MainGameHudController.BossEntranceFlashColor(.4f).a, 0f) &&
                MainGameHudController.BossEntranceFlashColor(.55f).a > 0f &&
                !bossHudSource.Contains("gameplayArtCatalog?.BossWarningLarge") &&
                !bossHudSource.Contains("gameplayArtCatalog?.BossWarningSmall") &&
                bossHudSource.Contains("entranceRect.anchorMin = Vector2.zero") &&
                bossHudSource.Contains("entranceRect.anchorMax = Vector2.one"),
            "Boss entrances must use a full-screen irregular horror flicker instead of the legacy wind art.");

        var characterCatalog = AssetDatabase.LoadAssetAtPath<CharacterArtCatalog>(
            "Assets/Art/Characters/CharacterArtCatalog.asset");
        var imugiEntry = characterCatalog != null ? characterCatalog.Find("imugi") : null;
        Require(imugiEntry?.Sprite != null &&
                AssetDatabase.GetAssetPath(imugiEntry.Sprite) ==
                "Assets/Art/Characters/imugi_head2.aseprite",
            "The v34 Imugi boss must replace its old head with the delivered imugi_head2 art.");
        var imugiBody = characterCatalog != null ? characterCatalog.FindSprite("imugi_body") : null;
        var imugiPreTail =
            characterCatalog != null ? characterCatalog.FindSprite("imugi_pre_tail") : null;
        var imugiPostTail =
            characterCatalog != null ? characterCatalog.FindSprite("imugi_post_tail") : null;
        Require(imugiBody != null && imugiPreTail != null && imugiPostTail != null &&
                AssetDatabase.GetAssetPath(imugiPreTail) ==
                "Assets/Art/Characters/imugi_pre_tail.aseprite" &&
                AssetDatabase.GetAssetPath(imugiPostTail) ==
                "Assets/Art/Characters/imugi_post_tail.aseprite",
            "Imugi must bind its delivered body, pre-tail, and post-tail art.");
        var imugiTailObject = new GameObject("ImugiTailCompositionContract");
        try
        {
            imugiTailObject.AddComponent<RuntimeImugiBodyVisual>().Configure(
                imugiBody, imugiPreTail, imugiPostTail, 0);
            var lastBody = imugiTailObject.transform.Find("Body_10");
            var preTail = imugiTailObject.transform.Find("PreTail");
            var postTail = imugiTailObject.transform.Find("PostTail");
            Require(lastBody != null && preTail != null && postTail != null &&
                    lastBody.position.x > preTail.position.x &&
                    preTail.position.x > postTail.position.x &&
                    lastBody.position.x - preTail.position.x >= 1.05f &&
                    Mathf.Abs(Mathf.DeltaAngle(lastBody.localEulerAngles.z, 90f)) < .01f &&
                    Mathf.Abs(Mathf.DeltaAngle(preTail.localEulerAngles.z, 0f)) < .01f &&
                    lastBody.GetComponent<SpriteRenderer>().sortingOrder <
                    preTail.GetComponent<SpriteRenderer>().sortingOrder &&
                    preTail.GetComponent<SpriteRenderer>().sortingOrder <
                    postTail.GetComponent<SpriteRenderer>().sortingOrder &&
                    imugiTailObject.transform.Find("Body_1")
                        .GetComponent<SpriteRenderer>().sortingOrder <
                    lastBody.GetComponent<SpriteRenderer>().sortingOrder &&
                    preTail.GetComponent<SpriteRenderer>().flipX &&
                    postTail.GetComponent<SpriteRenderer>().flipX &&
                    Mathf.Approximately(
                        preTail.GetComponent<SpriteRenderer>().sprite.rect.width,
                        imugiPreTail.rect.width * .5f) &&
                    Mathf.Approximately(
                        postTail.GetComponent<SpriteRenderer>().sprite.rect.width,
                        imugiPostTail.rect.width * .5f) &&
                    Mathf.Approximately(
                        preTail.GetComponent<SpriteRenderer>().sprite.rect.x,
                        imugiPreTail.rect.x + imugiPreTail.rect.width * .5f) &&
                    Mathf.Approximately(
                        postTail.GetComponent<SpriteRenderer>().sprite.rect.x,
                        imugiPostTail.rect.x),
                "Imugi must crop the larger right half for pre-tail and the smaller left half for post-tail before applying direction.");
            var imugiBodySource = System.IO.File.ReadAllText(
                "Assets/Scripts/Nyangbingo/World/RuntimeImugiBodyVisual.cs");
            Require(imugiBodySource.Contains(
                        "new Vector2(Mathf.Sign(tailDirection.x), 0f)") &&
                    imugiBodySource.Contains(
                        "new Vector2(0f, Mathf.Sign(tailDirection.y))"),
                "Imugi's rectangular tail pieces must snap to their rendered cardinal axis so diagonal trail bends cannot overlap the body.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(imugiTailObject);
        }
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
        var gangcheoriPreTail =
            characterCatalog != null ? characterCatalog.FindSprite("gangcheol_pre_tail") : null;
        var gangcheoriPostTail =
            characterCatalog != null ? characterCatalog.FindSprite("gangcheol_post_tail") : null;
        Require(gangcheoriPreTail != null && gangcheoriPostTail != null &&
                AssetDatabase.GetAssetPath(gangcheoriPreTail) ==
                "Assets/Art/Characters/gangcheol_post_tail.aseprite" &&
                AssetDatabase.GetAssetPath(gangcheoriPostTail) ==
                "Assets/Art/Characters/gangcheol_pre_tail.aseprite",
            "Gangcheori must correct the delivered reversed labels so its larger tail piece precedes its smaller tip.");
        var gangcheoriTailObject = new GameObject("GangcheoriTailCompositionContract");
        var gangcheoriHead = gangcheoriTailObject.AddComponent<SpriteRenderer>();
        try
        {
            gangcheoriTailObject.AddComponent<RuntimeGangcheoriBodyVisual>().Configure(
                gangcheoriBody, gangcheoriPreTail, gangcheoriPostTail, gangcheoriHead, 0);
            var lastBody = gangcheoriTailObject.transform.Find("GangcheoriBody_5");
            var preTail = gangcheoriTailObject.transform.Find("GangcheoriPreTail");
            var postTail = gangcheoriTailObject.transform.Find("GangcheoriPostTail");
            Require(lastBody != null && preTail != null && postTail != null &&
                    lastBody.localPosition.x < preTail.localPosition.x &&
                    preTail.localPosition.x < postTail.localPosition.x &&
                    Mathf.Abs(Mathf.DeltaAngle(lastBody.localEulerAngles.z, 90f)) < .01f &&
                    Mathf.Abs(Mathf.DeltaAngle(preTail.localEulerAngles.z, 0f)) < .01f &&
                    lastBody.GetComponent<SpriteRenderer>().sortingOrder <
                    preTail.GetComponent<SpriteRenderer>().sortingOrder &&
                    preTail.GetComponent<SpriteRenderer>().sortingOrder <
                    postTail.GetComponent<SpriteRenderer>().sortingOrder &&
                    gangcheoriTailObject.transform.Find("GangcheoriBody_1")
                        .GetComponent<SpriteRenderer>().sortingOrder <
                    lastBody.GetComponent<SpriteRenderer>().sortingOrder &&
                    !preTail.GetComponent<SpriteRenderer>().flipX &&
                    !postTail.GetComponent<SpriteRenderer>().flipX &&
                    Mathf.Approximately(
                        preTail.GetComponent<SpriteRenderer>().sprite.rect.width,
                        gangcheoriPreTail.rect.width) &&
                    Mathf.Approximately(
                        postTail.GetComponent<SpriteRenderer>().sprite.rect.width,
                        gangcheoriPostTail.rect.width),
                "Gangcheori tail order must be body, pre-tail, post-tail without incorrectly cropping its complete sprites.");
            var firstBody = gangcheoriTailObject.transform.Find("GangcheoriBody_1");
            gangcheoriTailObject.transform.position += Vector3.down;
            typeof(RuntimeGangcheoriBodyVisual).GetMethod("LateUpdate", InstanceMembers)
                ?.Invoke(gangcheoriTailObject.GetComponent<RuntimeGangcheoriBodyVisual>(), null);
            Require(firstBody != null &&
                    firstBody.position.y < lastBody.position.y &&
                    Mathf.Abs(Mathf.DeltaAngle(firstBody.localEulerAngles.z, 0f)) < .01f,
                "Gangcheori body segments must bend through the head's prior world-space trail during vertical movement.");
            var gangcheoriBodySource = System.IO.File.ReadAllText(
                "Assets/Scripts/Nyangbingo/World/RuntimeImugiBodyVisual.cs");
            Require(gangcheoriBodySource.Contains(
                        "public sealed class RuntimeGangcheoriBodyVisual") &&
                    gangcheoriBodySource.Contains(
                        "private readonly Vector2[] segmentWorldPositions") &&
                    gangcheoriBodySource.Contains(
                        "segmentWorldPositions[index] - predecessor"),
                "Gangcheori must use the same distance-constrained world-space trail model as Imugi.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gangcheoriTailObject);
        }
        var encounterSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/World/MainGameEncounterCoordinator.cs");
        Require(encounterSource.Contains("definition.Kind == YokaiKind.Gangcheori") &&
                encounterSource.Contains("yokaiObject.AddComponent<RuntimeGangcheoriBodyVisual>()") &&
                encounterSource.Contains("AddComponent<GangcheoriBreathController>()") &&
                encounterSource.Contains("FindSprite(\"gangcheol_body\")") &&
                encounterSource.Contains("FindSprite(\"gangcheol_pre_tail\")") &&
                encounterSource.Contains("FindSprite(\"gangcheol_post_tail\")"),
            "The v34 resident Gangcheori yokai must compose its delivered body, pre-tail, and post-tail behind the head.");
        Require(Mathf.Approximately(GangcheoriBreathController.TelegraphSeconds, 1.5f) &&
                Mathf.Approximately(GangcheoriBreathController.RangeTiles, 3f) &&
                Mathf.Approximately(GangcheoriBreathController.ArcDegrees, 60f) &&
                Mathf.Approximately(GangcheoriBreathController.KnockbackTiles, 2f) &&
                Mathf.Approximately(GangcheoriBreathController.CooldownSeconds, 12f) &&
                Mathf.Approximately(
                    GangcheoriBreathController.EffectWorldSize.x,
                    GangcheoriBreathController.RangeTiles) &&
                Mathf.Approximately(
                    GangcheoriBreathController.EffectWorldSize.y,
                    GangcheoriBreathController.RangeTiles * 2f *
                    Mathf.Tan(GangcheoriBreathController.ArcDegrees * .5f * Mathf.Deg2Rad)) &&
                GangcheoriBreathController.IsInsideBreathCone(
                    new Vector2(2.5f, 0f), Vector2.right) &&
                !GangcheoriBreathController.IsInsideBreathCone(
                    new Vector2(2.5f, 2.5f), Vector2.right),
            "The v34 resident Gangcheori must retain its reduced 3-tile telegraphed fire breath.");
        var shortcutHelpSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/UI/MainGameBossSummonUiController.cs");
        Require(encounterSource.Contains(
                    "YokaiKind.Gangcheori, \"Alt+F12\", \"Gangcheori\"") &&
                encounterSource.Contains(
                    "YokaiKind.Gaekgwi, \"Alt+Shift+F12\", \"Gaekgwi\"") &&
                encounterSource.Contains("ResolveInstanceSpawnTrack(definition)") &&
                encounterSource.Contains(
                    "brain.ConfigureForRuntime(definition, raidTarget, counters, instanceSpawnTrack)") &&
                shortcutHelpSource.Contains("Alt+F12  강철이 소환") &&
                shortcutHelpSource.Contains("Alt+Shift+F12  객귀 소환"),
            "Editor shortcuts must immediately spawn resident Gangcheori and raid Gaekgwi using their actual spawn tracks.");
        var gameplayCatalog = AssetDatabase.LoadAssetAtPath<GameplayArtCatalog>(
            "Assets/Art/Gameplay/GameplayArtCatalog.asset");
        Require(gameplayCatalog != null &&
                gameplayCatalog.GangcheoriSpecialFireFrames.Count == 4,
            "Gangcheori must bind all four delivered 0.1-second fire effect frames.");
        Require(characterCatalog?.Find("gaekgwi")?.SourceFacesRight == true,
            "The delivered Gaekgwi source frames face right and must not be mirrored against movement.");
        Require(characterCatalog?.Find("club")?.SourceFacesRight == false,
            "The delivered Club Goblin source frames face left and must visually follow its grounded route.");
        Require(!RuntimeCharacterSpriteAnimator.ShouldFlipX(false, -1f) &&
                RuntimeCharacterSpriteAnimator.ShouldFlipX(false, 1f),
            "A left-facing Club Goblin source must remain unflipped while walking left and flip exactly once while walking right.");
        Require(gameplayCatalog != null &&
                gameplayCatalog.PlayerFireHitFrames.Count == 3,
            "Fire damage must bind all three delivered player head effect frames.");
        var itemArtCatalog = AssetDatabase.LoadAssetAtPath<ItemArtCatalog>(
            "Assets/Art/Items/ItemArtCatalog.asset");
        var catnipSprite = itemArtCatalog != null ? itemArtCatalog.FindSprite("catnip") : null;
        Require(catnipSprite != null &&
                AssetDatabase.GetAssetPath(catnipSprite) == "Assets/Art/Items/catnip.aseprite",
            "The catnip item must bind the newly delivered catnip icon.");
        Require(gameplayCatalog != null &&
                gameplayCatalog.ImugiElectricAttackFrames.Count == 7,
            "Imugi must bind all seven delivered electric attack frames.");
        var bossCombatSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/Bosses/BossCombatController.cs");
        Require(bossCombatSource.Contains(
                    "SetTelegraphVisible(definition.Kind != BossKind.Gangcheori)") &&
                bossCombatSource.Contains(
                    "SetSpecialEffectVisible(definition.Kind == BossKind.Gangcheori)") &&
                bossCombatSource.Contains("PlayImugiSpecialEffect()"),
            "Delivered Gangcheori and Imugi special effects must remain wired to boss combat impacts.");
        var effectPresenterSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/World/MainGameEffectPresenter.cs");
            Require(effectPresenterSource.Contains(
                        "if (tag == DamageTag.Fire && playerFireHitEffect != null)") &&
                    effectPresenterSource.Contains(
                        "playerFireHitEffect.Play(PlayerFireHitDurationSeconds)") &&
                    Mathf.Approximately(
                        MainGameEffectPresenter.PlayerFireHitDurationSeconds, .8f) &&
                    MainGameEffectPresenter.PlayerFireHitHeadOffset > 0f &&
                    MainGameEffectPresenter.PlayerFireHitFallbackSortingOrder < 20 &&
                    effectPresenterSource.Contains("playerRenderer.sortingOrder - 1") &&
                    effectPresenterSource.Contains(
                        "fireRenderer.sortingLayerID = playerRenderer.sortingLayerID"),
                "Delivered player fire-hit art must play behind the player's head only for fire damage.");
    }

    private static void TestDayNightCountdownFormatting()
    {
        Require(MainGameHudController.FormatRemainingTime(540f) == "09:00",
            "A full night must be displayed as 09:00.");
        Require(MainGameHudController.FormatRemainingTime(0.1f) == "00:01",
            "The countdown must use ceiling seconds so it does not show 00:00 before transition.");
        Require(MainGameHudController.FormatRemainingTime(-1f) == "00:00",
            "Negative remaining time must be clamped to 00:00.");
        Require(DayNightService.CalculateDaysRemaining(100, 0) == 100 &&
                DayNightService.CalculateDaysRemaining(100, 1) == 99 &&
                DayNightService.CalculateDaysRemaining(100, 15) == 85 &&
                DayNightService.CalculateDaysRemaining(100, 100) == 0 &&
                DayNightService.CalculateDaysRemaining(100, 101) == 0,
            "Title and in-game HUD must share one D-day calculation without a one-day offset.");
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
        Require(MainGameHudController.GoalBadgeDayNightRhythmHint.Contains("낮") &&
                MainGameHudController.GoalBadgeDayNightRhythmHint.Contains("밤") &&
                MainGameHudController.GoalBadgeDayNightRhythmHint.Contains("방어"),
            "The early goal badges must explain the day preparation and night defense rhythm in one line.");
        Require(MainGameHudController.BossFleeRollSeconds > 0f &&
                Mathf.Approximately(MainGameHudController.CalculateBossFleeRollScale(
                    MainGameHudController.BossFleeRollSeconds, MainGameHudController.BossFleeRollSeconds), 1f) &&
                Mathf.Approximately(MainGameHudController.CalculateBossFleeRollScale(0f,
                    MainGameHudController.BossFleeRollSeconds), 0f),
            "A dawn-fleeing boss health scroll must roll from full width to zero width.");
        Require(MainGameHudController.ResolveDayNightClockFrameIndex(0f, 1440f, 6) == 5 &&
                MainGameHudController.ResolveDayNightClockFrameIndex(1439f, 1440f, 6) == 0 &&
                MainGameHudController.ResolveDayNightClockFrameIndex(0f, 0f, 6) == -1 &&
                MainGameHudController.IsSunsetWarningWindow(false, 60f) &&
                MainGameHudController.IsSunsetWarningWindow(false, 0f) &&
                !MainGameHudController.IsSunsetWarningWindow(false, 60.01f) &&
                !MainGameHudController.IsSunsetWarningWindow(true, 30f) &&
                MainGameHudController.IsSunsetWarningBrightPhase(0f) &&
                !MainGameHudController.IsSunsetWarningBrightPhase(.5f) &&
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
                presenterSource.Contains("public void SetColor(Color color)") &&
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
        Require(MainGameCraftingUiController.InventoryHotbarSlotCount == 8 &&
                MainGameCraftingUiController.UnifiedTabLabel(0) == "인벤토리",
            "The inventory must distinguish its first eight hotbar-linked slots and label F1 as inventory.");
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
        Require(MainGameCraftingUiController.CanToggleCraftingSmelting(CraftingStation.Furnace) &&
                MainGameCraftingUiController.CanToggleCraftingSmelting(CraftingStation.Foundry) &&
                !MainGameCraftingUiController.CanToggleCraftingSmelting(CraftingStation.Workbench),
            "Furnaces and foundries must expose both their crafting and smelting routes.");
        var craftingUiSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/UI/MainGameCraftingUiController.cs");
        Require(craftingUiSource.Contains("F2 · 제작/제련") &&
                craftingUiSource.Contains("showingSmelting = !showingSmelting") &&
                craftingUiSource.Contains("F2 제작/제련 전환"),
            "F2 must visibly toggle both ways between crafting and smelting at a shared station.");
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

        var hotbarItem = ItemDefinition.CreateRuntime("hotbar_route_test", "Hotbar Route Test");
        var inventoryOnlyItem = ItemDefinition.CreateRuntime("inventory_route_test", "Inventory Route Test");
        ItemDefinition FindRoutingItem(string id) =>
            id == hotbarItem.Id ? hotbarItem : id == inventoryOnlyItem.Id ? inventoryOnlyItem : null;
        var routedInventory = new Inventory(
            FindRoutingItem, 12, MainGameCraftingUiController.InventoryHotbarSlotCount,
            itemId => itemId == hotbarItem.Id);
        Require(routedInventory.TryAdd(inventoryOnlyItem.Id, 1) &&
                routedInventory.Slots.Take(MainGameCraftingUiController.InventoryHotbarSlotCount)
                    .All(slot => string.IsNullOrEmpty(slot.itemId)) &&
                routedInventory.Slots[MainGameCraftingUiController.InventoryHotbarSlotCount].itemId ==
                    inventoryOnlyItem.Id,
            "Items that cannot be selected on the hotbar must skip its first eight slots during acquisition.");
        Require(routedInventory.TryAdd(hotbarItem.Id, 1) &&
                routedInventory.Slots[0].itemId == hotbarItem.Id,
            "Hotbar-selectable items must still auto-fill the first eight inventory slots.");
        UnityEngine.Object.DestroyImmediate(hotbarItem);
        UnityEngine.Object.DestroyImmediate(inventoryOnlyItem);
    }

    private static void TestRecipeProgressionUnlockContract()
    {
        var regular = RecipeDefinition.CreateRuntime(
            "recipe_progression_regular", CraftingStation.Workbench,
            new ItemAmount[0], default);
        var altar = RecipeDefinition.CreateRuntime(
            Nyangbingo.Crafting.RecipeUnlockPolicy.GangcheoriUnlockRecipeId,
            CraftingStation.IceAnvil, new ItemAmount[0], default);
        try
        {
            var book = new Nyangbingo.Crafting.RecipeBook();
            Require(Nyangbingo.Crafting.RecipeUnlockPolicy.IsUnlocked(regular, book) &&
                    !Nyangbingo.Crafting.RecipeUnlockPolicy.IsUnlocked(altar, book),
                "Only the v34 ice-altar offering recipe must remain hidden before the first Gangcheori kill.");
            book.Unlock(altar.Id);
            Require(Nyangbingo.Crafting.RecipeUnlockPolicy.IsUnlocked(altar, book),
                "The v34 ice-altar offering recipe must become permanently available after unlock.");

            var runtimeSource = System.IO.File.ReadAllText(
                "Assets/Scripts/Nyangbingo/World/MainGameRuntimeServices.cs");
            var saveSource = System.IO.File.ReadAllText(
                "Assets/Scripts/Nyangbingo/Save/MainGameSaveCoordinator.cs");
            Require(runtimeSource.Contains("GameEvents.OnYokaiKilled += HandleRecipeUnlockYokaiKilled") &&
                    runtimeSource.Contains("definition.Kind != YokaiKind.Gangcheori") &&
                    saveSource.Contains("RestoreStage(\"recipe progression\"") &&
                    saveSource.Contains("record.kills <= 0"),
                "Gangcheori recipe progression must unlock live and recover from pre-unlock save data.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(regular);
            UnityEngine.Object.DestroyImmediate(altar);
        }
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
        Require(Mathf.Approximately(MainGameTilePaletteController.PlacementReachTiles, 1.5f) &&
                MainGameTilePaletteController.IsWithinPlacementReach(
                    new Vector2(.5f, .5f), new Vector3Int(1, 0, 0), 1.5f) &&
                !MainGameTilePaletteController.IsWithinPlacementReach(
                    new Vector2(.5f, .5f), new Vector3Int(3, 0, 0), 1.5f),
            "Foreground placement must use the same 1.5-tile reach as mining.");
        Require(MainGameTilePaletteController.IsDirectUseHotbarItem(
                    PlayerHealthRecoveryService.CatnipItemId) &&
                !MainGameTilePaletteController.IsDirectUseHotbarItem(WorldTileTypes.Dirt),
            "Catnip must remain selected as a direct-use hotbar item instead of entering placement.");
        var paletteSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/UI/MainGameTilePaletteController.cs");
        Require(paletteSource.Contains("TrySelectPaletteSlot(shortcutSlot)") &&
                 paletteSource.Contains("CollectHotbarSlotItemIds()") &&
                 paletteSource.Contains("SelectEmptySlot(slotIndex)") &&
                 paletteSource.Contains("SelectDirectUseSlot(slotIndex, itemId)"),
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
        var runtimeStone = ItemDefinition.CreateRuntime(WorldTileTypes.Stone, "Stone", 99,
            ItemCategory.Material, ItemMvpScope.A);
        ItemDefinition FindItem(string id) =>
            id == runtimeDirt.Id ? runtimeDirt : id == runtimeStone.Id ? runtimeStone : null;
        var inventory = new Nyangbingo.Inventory.Inventory(FindItem, 10);
        Require(inventory.TryAdd(runtimeDirt.Id, 2) &&
                inventory.TryAdd(runtimeStone.Id, 3) &&
                inventory.TrySwapSlots(1, 7) &&
                inventory.Slots[1].amount == 0 &&
                inventory.Slots[7].itemId == runtimeStone.Id &&
                inventory.Slots[7].amount == 3 &&
                !inventory.TrySwapSlots(7, 7),
            "Shift-click inventory reordering must move stacks into and out of the first eight hotbar slots.");
        var craftingSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/UI/MainGameCraftingUiController.cs");
        var playerSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Nyangbingo/World/MainGamePlayerController.cs");
        Require(craftingSource.Contains("TrySwapSlots(sourceIndex, index)") &&
                craftingSource.Contains("Shift+다른 슬롯 클릭: 위치 교환") &&
                !craftingSource.Contains("앞 8칸은 퀵슬롯") &&
                craftingSource.Contains("\"HotbarShortcut\"") &&
                craftingSource.Contains("shortcut.text = (index + 1).ToString()") &&
                craftingSource.Contains("shortcutRect.anchorMin = Vector2.zero") &&
                craftingSource.Contains("shortcutRect.anchorMax = Vector2.one") &&
                !craftingSource.Contains("gameplayArtCatalog?.InventorySlotTopSelected") &&
                craftingSource.Contains("F1 · 인벤토리") &&
                paletteSource.Contains("설치 거리가 너무 멉니다") &&
                !paletteSource.Contains("설치 거리가 너무 멉니다 · 최대") &&
                paletteSource.Contains("퀵슬롯에서 선택할 수 없습니다") &&
                !paletteSource.Contains("퀵슬롯에서 설치할 수 없습니다") &&
                playerSource.Contains("채굴 도구 등급 부족") &&
                playerSource.Contains("RaiseMiningTargetChanged"),
            "Hotbar reordering, placement failures, mining target highlight, and hardness feedback must be visible.");
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
