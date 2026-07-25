using Nyangbingo.Save;
using Nyangbingo.Core;
using Nyangbingo.Crafting;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using Nyangbingo.Combat;
using Nyangbingo.Yokai;
using Nyangbingo.Bosses;
using Nyangbingo.Audio;
using Nyangbingo.UI;
using Nyangbingo.World;
using UnityEngine;

namespace Nyangbingo.Debugging
{
    public sealed class DevBTestBootstrap : MonoBehaviour
    {
        [SerializeField] private RecipeDefinition importedTimedRecipe;
        [SerializeField] private DayEventDefinition importedBaekjungEvent;
        [SerializeField] private YokaiDefinition importedClubGoblin;
        [SerializeField] private GameDataCatalog gameDataCatalog;

        private void Start()
        {
            Debug.Log("[Nyangbingo] Dev B test scene ready: inventory, crafting, combat, yokai, boss, and save modules can be wired here.");
            TestCoreGameEventsHub();
            TestInventoryLargeStackOverflowGuard();
            TestV29InventoryCapacityAndLegacyPadding();
            TestV29InventoryOwnedPlaceablePolicy();
            TestV28ActiveSlotToggleAndSave();
            TestPortableLanternActiveFuelAndSave();
            var wood = ItemDefinition.CreateRuntime("wood", "나무");
            var stone = ItemDefinition.CreateRuntime("stone", "돌");
            var workbench = ItemDefinition.CreateRuntime("workbench", "작업대", 1);
            var inventory = new Nyangbingo.Inventory.Inventory(id => id == wood.Id ? wood : id == stone.Id ? stone : id == workbench.Id ? workbench : null);
            inventory.TryAdd(wood.Id, 8); inventory.TryAdd(stone.Id, 12);
            var recipe = RecipeDefinition.CreateRuntime("workbench", CraftingStation.None,
                new[] { new ItemAmount { item = wood, amount = 8 }, new ItemAmount { item = stone, amount = 12 } },
                new ItemAmount { item = workbench, amount = 1 });
            var crafted = new CraftingService(inventory).TryCraft(recipe, CraftingStation.None);
            if (!crafted || inventory.Count(workbench.Id) != 1) Debug.LogError("[Nyangbingo] Crafting test failed.");

            inventory.TryAdd(wood.Id, 2);
            var timedRecipe = RecipeDefinition.CreateRuntime("timed_workbench", CraftingStation.None,
                new[] { new ItemAmount { item = wood, amount = 2 } }, new ItemAmount { item = workbench, amount = 1 }, 3f);
            var craftingProcess = new CraftingProcess(new CraftingService(inventory));
            var startedCraft = craftingProcess.TryStart(timedRecipe, CraftingStation.None);
            var completedEarly = craftingProcess.Tick(2f);
            var completedOnTime = craftingProcess.Tick(1f);
            if (startedCraft && !completedEarly && completedOnTime && inventory.Count(workbench.Id) == 2)
                Debug.Log("[Nyangbingo] Timed crafting completed using game seconds.");
            else Debug.LogError("[Nyangbingo] Timed crafting test failed.");

            TestImportedTimedCrafting();
            TestTimedCraftingFullInventoryProtection();
            TestCraftingRecipeValidation();
            TestCraftingProcessSaveRoundTrip();
            TestRecipeBookSaveRoundTrip();
            TestImportedSmeltingStationRules();
            TestSmeltingSharedInputFuelTransaction();
            TestSmeltingRestoreValidation();
            TestGameDataCatalog();
            TestImportedV26DataContract();
            TestCoolingSourceRuntimeAndSaveContract();
            TestPlayerHealthRecoveryContract();
            TestDeathTearPouchRuntimeContract();
            TestImportedModules();
            TestImportedMineralTiers();
            TestImportedSealWhitelist();
            TestImportedIdMigrations();
            TestImportedDayCurve();
            TestImportedGlobals();
            TestJangdokStorageContract();
            TestImportedCombatProfiles();
            TestSideScrollerMovementContract();
            TestTimedMiningPresentationContract();
            TestIceSteelClawAbilitiesContract();
            TestGoalBadgeProgressContract();
            TestGameDataCatalogInvalidEntryRejection();
            TestImportedBossDefinitions();
            TestBossCombatRuntime();
            TestMotherBulgasariAirborneSpecialRuntime();
            TestImugiPhaseSpecialRuntime();
            TestBossStartValidation();
            TestBossSummonPaymentTransaction();
            TestBossSummonAndForcedEncounterRules();
            TestForcedBossEncounterSaveRoundTrip();
            TestForcedBossEncounterDuplicateRestoreRejection();
            TestImportedBossGuaranteedRewardFlow();
            TestBossRewardFullInventoryRetention();
            TestPendingItemAcquisitionSaveRoundTrip();
            TestBossRecordSaveFlow();
            TestBossRecordSaveValidation();
            TestYokaiCodexKillSaveBinding();
            TestImportedYokaiCodexPresentation();
            TestAudioEventRoutingAndRuntimePool();
            TestGameShellTitlePauseSettingsAndResult();
            TestProductCraftingStationDefinitionContract();
            TestImportedAccessoryStatsAndTheftProtection();
            TestImportedArmorStatsRecipesAndSetBonus();
            TestEquipmentCollectionSaveRoundTrip();
            TestEquipmentDefinitionIdentityAndSlotValidation();
            TestEquipmentStatInvalidNumericGuard();
            TestEquipmentTotalDefenseOverflowGuard();
            TestImportedChestRewardPools();

            var equipment = new EquipmentSystem();
            var helmet = EquipmentDefinition.CreateRuntime("test_helmet", EquipmentSlot.Head, false, 3, .05f, .15f, -.2f);
            var boots = EquipmentDefinition.CreateRuntime("test_boots", EquipmentSlot.AccessoryOne, true, 1, .1f, .2f, -.2f, .1f, true);
            equipment.TryEquip(helmet); equipment.TryEquipAccessory(boots, 0);
            var stats = new StatSheet(); stats.Recalculate(equipment);
            if (stats.Defense == 4 && Mathf.Approximately(stats.MovementMultiplier, 1.15f) && Mathf.Approximately(stats.MiningCriticalChance, .25f) &&
                Mathf.Approximately(stats.TemperatureRiseModifier, -.35f) && Mathf.Approximately(stats.FireDamageModifier, .1f) && stats.HasDoubleJump)
                Debug.Log("[Nyangbingo] Equipment stat aggregation and limits completed.");
            else Debug.LogError("[Nyangbingo] Equipment stat test failed.");

            var save = gameObject.AddComponent<SaveManager>();
            var sample = new SaveGame { seed = 100, day = 1, inventory = inventory.Export() };
            save.Save(0, sample);
            if (!save.TryLoad(0, out var loaded) || loaded.inventory.Count != Nyangbingo.Inventory.Inventory.SlotCount)
                Debug.LogError("[Nyangbingo] Save test failed.");
            else Debug.Log("[Nyangbingo] Item acquisition, crafting, and save round-trip completed.");
            TestSaveJsonSchemaRejection();
            TestRegularEncounterSaveRoundTrip();
            TestV24SaveIdMigration();
            TestSaveManagerInputValidation(save);
            TestSaveManagerAtomicReplacement(save);

            var yokaiKilledEventCount = 0;
            var bossSummonedEventCount = 0;
            var bossDefeatedEventCount = 0;
            YokaiDefinition killedYokaiDefinition = null;
            BossDefinition summonedBossDefinition = null;
            BossDefinition defeatedBossDefinition = null;
            System.Action<YokaiDefinition> onYokaiKilled = definition =>
            {
                yokaiKilledEventCount++;
                killedYokaiDefinition = definition;
            };
            System.Action<BossDefinition> onBossSummoned = definition =>
            {
                bossSummonedEventCount++;
                summonedBossDefinition = definition;
            };
            System.Action<BossDefinition> onBossDefeated = definition =>
            {
                bossDefeatedEventCount++;
                defeatedBossDefinition = definition;
            };

            GameEvents.OnYokaiKilled += onYokaiKilled;
            GameEvents.OnBossSummoned += onBossSummoned;
            GameEvents.OnBossDefeated += onBossDefeated;
            try
            {
                var yokaiDefinition = YokaiDefinition.CreateRuntime(YokaiKind.ClubGoblin, 10, 2f, 8, 10f,
                    new[] { new ItemAmount { item = wood, amount = 1 } });
                var yokai = new GameObject("TemporaryYokai");
                var health = yokai.AddComponent<Health>();
                health.ConfigureForRuntime(yokaiDefinition.HitPoints);
                var loot = yokai.AddComponent<YokaiLoot>();
                loot.ConfigureForRuntime(yokaiDefinition);
                var droppedWood = 0;
                loot.Dropped += (item, amount) => { if (item == wood) droppedWood += amount; };
                health.ApplyDamage(10, DamageTag.Melee);
                if (health.IsDead && droppedWood == 1) Debug.Log("[Nyangbingo] Combat damage and yokai loot completed.");
                else Debug.LogError("[Nyangbingo] Combat or yokai loot test failed.");
                Destroy(yokai);

                var testTime = gameObject.AddComponent<DevBTestTimeSource>();
                var testSpawner = gameObject.AddComponent<DevBTestSpawnController>();
                var bossManager = gameObject.AddComponent<BossManager>();
                bossManager.ConfigureForRuntime(testTime, testSpawner);
                var bossDefinition = BossDefinition.CreateRuntime("king_dokkaebi", YokaiKind.ClubGoblin, workbench,
                    new[] { new ItemAmount { item = wood, amount = 2 } });
                var bossObject = new GameObject("TemporaryBoss");
                var bossBody = bossObject.AddComponent<Rigidbody2D>();
                bossBody.gravityScale = 0f;
                var bossHealth = bossObject.AddComponent<Health>();
                bossHealth.ConfigureForRuntime(20);
                var bossDefeated = false;
                bossManager.BossEnded += (_, defeated) => bossDefeated = defeated;
                var started = bossManager.TryStart(bossDefinition, bossHealth);
                var bossAcceptedKnockback = bossHealth.TryApplyKnockback(Vector2.right * 2f);
                bossHealth.ApplyDamage(20, DamageTag.Melee);
                if (started && bossDefeated && testSpawner.IsRegularSpawning)
                    Debug.Log("[Nyangbingo] Boss start, spawn pause, defeat, and spawn resume completed.");
                else Debug.LogError("[Nyangbingo] Boss flow test failed.");

                if (started && bossHealth.IsKnockbackImmune && !bossAcceptedKnockback && bossBody.linearVelocity == Vector2.zero)
                    Debug.Log("[Nyangbingo] Boss knockback immunity completed.");
                else Debug.LogError("[Nyangbingo] Boss knockback immunity test failed.");

                if (yokaiKilledEventCount == 1 && killedYokaiDefinition == yokaiDefinition &&
                    bossSummonedEventCount == 1 && summonedBossDefinition == bossDefinition &&
                    bossDefeatedEventCount == 1 && defeatedBossDefinition == bossDefinition)
                    Debug.Log("[Nyangbingo] GameEvents yokai kill and boss lifecycle completed.");
                else Debug.LogError("[Nyangbingo] GameEvents yokai kill or boss lifecycle test failed.");
                Destroy(bossObject);
            }
            finally
            {
                GameEvents.OnYokaiKilled -= onYokaiKilled;
                GameEvents.OnBossSummoned -= onBossSummoned;
                GameEvents.OnBossDefeated -= onBossDefeated;
            }

            TestOverlapBoxAttack();
            TestOverlapBoxInvalidNumericGuard();
            TestDefenseAndDamageDelivery();
            TestCombatInvalidNumericGuard();
            TestHealthRuntimeReconfigurationReset();
            TestWireSnareAbility();
            TestYagwanggwiTheftRules();
            TestImportedYokaiSpawnTracksAndDawnFlee();
            TestYokaiDefinitionHealthInitialization();
            TestYokaiGameSecondsBinding();
            TestSummonedBossFieldYokaiFreezePolicy();
            TestYokaiReconfigurationClockReset();
            TestYokaiApproachOvershootGuard();
            TestSieveStopTiming();
            TestSieveDamageMultiplierApplication();
            TestYokaiTargetReplacementStateReset();
            TestYokaiAttackRangeRevalidation();
            TestDeadYokaiStopsActing();
            TestCounterDurationLargeTickConsumption();
            TestEoduksiniLanternReaction();
            TestBulgasariWallRule();
            TestCounterAuraSensor();
            TestHaetaeAndBellAuraEffects();
            TestTurretTargetingAndFuel();
            TestTurretInvalidConfigurationGuard();
            TestHomingProjectilePool();
            TestHomingProjectileInvalidConfigurationGuard();
            TestInvalidGameSecondsGuardSweep();
            TestGaekgwiPatternRuntime();
            TestImportedBaekjungSchedule();
            TestBaekjungWaveSpawnRequests();
            TestBaekjungRegularSpawnPauseResume();
            TestBaekjungRewardMultipliers();
            TestImportedYokaiLootWithBaekjungRewards();
            TestYokaiLootInvalidRandomRejection();
            TestBaekjungTimeBinding();
            TestBaekjungSaveStateRoundTrip();
            TestProgressionSaveRoundTrip();
            TestProgressionRestoreInventoryPrevalidation();
            TestProgressionRestoreEquipmentPrevalidation();
            TestWorldChestAndTurretSaveRoundTrip();
            TestWorldRecordCapturePrevalidation();
            TestDuplicateTurretFuelRestoreRejection();
            TestLegacyTurretFuelReplacementRestore();
            TestPlayerTimeAndBossSaveRoundTrip();
            TestPlayerBossSaveInvalidPositionRejection();
            TestPlayerBossSaveSpawnFailureRollback();

            inventory.TryAdd(wood.Id, 2); inventory.TryAdd(stone.Id, 1);
            var smeltingRecipe = SmeltingDefinition.CreateRuntime("test_smelting",
                new ItemAmount { item = wood, amount = 2 }, new ItemAmount { item = stone, amount = 1 },
                new ItemAmount { item = workbench, amount = 1 }, 1f);
            var smelting = new SmeltingStation(inventory, smeltingRecipe.StationKind,
                smeltingRecipe.BatchCapacity);
            if (smelting.TryStart(smeltingRecipe) && smelting.Tick(1f) && smelting.Completed.Count == 1 && smelting.TryCollect(0) && inventory.Count(workbench.Id) >= 2)
                Debug.Log("[Nyangbingo] Smelting completed.");
            else Debug.LogError("[Nyangbingo] Smelting test failed.");

            var chest = new ChestProgress(gameDataCatalog.FindItem);
            var chestDefinition = ChestDefinition.CreateRuntime(new[] { new ItemAmount { item = wood, amount = 1 } });
            if (chest.TryOpen("test-chest", chestDefinition) && !chest.TryOpen("test-chest", chestDefinition))
                Debug.Log("[Nyangbingo] Chest single-open protection completed.");
            else Debug.LogError("[Nyangbingo] Chest test failed.");

            var utilities = new UtilityService(); var fanUsed = false;
            utilities.FanUsed += _ => fanUsed = true;
            if (utilities.TryUse(UtilityDefinition.CreateRuntime(UtilityKind.Hapjukseon, 3f)) && fanUsed)
                Debug.Log("[Nyangbingo] Utility event completed.");
            else Debug.LogError("[Nyangbingo] Utility test failed.");

            TestImportedUtilities();
            TestImportedFanIdAndRecipeContract();
            TestUtilityCooldownSaveRoundTrip();
        }

        private void TestCoreGameEventsHub()
        {
            var dayStartCount = 0;
            var nightStartCount = 0;
            var dawnWarningCount = 0;
            var sealChangedCount = 0;
            var placedPosition = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
            var brokenPosition = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
            var miningResultPosition = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
            string miningResultItem = null;
            var miningResultAmount = 0;
            var miningResultCritical = false;
            System.Action onDayStart = () => dayStartCount++;
            System.Action onNightStart = () => nightStartCount++;
            System.Action onDawnWarning = () => dawnWarningCount++;
            System.Action onSealChanged = () => sealChangedCount++;
            System.Action<Vector3Int> onTilePlaced = position => placedPosition = position;
            System.Action<Vector3Int> onTileBroken = position => brokenPosition = position;
            System.Action<Vector3Int, string, int, bool> onMiningResult = (position, item, amount, critical) =>
            {
                miningResultPosition = position;
                miningResultItem = item;
                miningResultAmount = amount;
                miningResultCritical = critical;
            };
            var expectedPlaced = new Vector3Int(3, -4, 0);
            var expectedBroken = new Vector3Int(-7, 8, 1);
            var expectedMining = new Vector3Int(5, 6, 0);

            GameEvents.OnDayStart += onDayStart;
            GameEvents.OnNightStart += onNightStart;
            GameEvents.OnDawnWarning += onDawnWarning;
            GameEvents.OnSealChanged += onSealChanged;
            GameEvents.OnTilePlaced += onTilePlaced;
            GameEvents.OnTileBroken += onTileBroken;
            GameEvents.OnMiningResult += onMiningResult;
            try
            {
                GameEvents.RaiseDayStart();
                GameEvents.RaiseNightStart();
                GameEvents.RaiseDawnWarning();
                GameEvents.RaiseSealChanged();
                GameEvents.RaiseTilePlaced(expectedPlaced);
                GameEvents.RaiseTileBroken(expectedBroken);
                GameEvents.RaiseMiningResult(expectedMining, "돌", 2, true);
            }
            finally
            {
                GameEvents.OnDayStart -= onDayStart;
                GameEvents.OnNightStart -= onNightStart;
                GameEvents.OnDawnWarning -= onDawnWarning;
                GameEvents.OnSealChanged -= onSealChanged;
                GameEvents.OnTilePlaced -= onTilePlaced;
                GameEvents.OnTileBroken -= onTileBroken;
                GameEvents.OnMiningResult -= onMiningResult;
            }

            GameEvents.RaiseDayStart();
            GameEvents.RaiseNightStart();
            GameEvents.RaiseDawnWarning();
            GameEvents.RaiseSealChanged();
            GameEvents.RaiseTilePlaced(Vector3Int.zero);
            GameEvents.RaiseTileBroken(Vector3Int.zero);
            GameEvents.RaiseMiningResult(Vector3Int.zero, "흙", 1, false);

            if (dayStartCount == 1 && nightStartCount == 1 && dawnWarningCount == 1 && sealChangedCount == 1 &&
                placedPosition == expectedPlaced && brokenPosition == expectedBroken &&
                miningResultPosition == expectedMining && miningResultItem == "돌" && miningResultAmount == 2 &&
                miningResultCritical)
                Debug.Log("[Nyangbingo] Core GameEvents payload delivery and static unsubscription completed.");
            else Debug.LogError("[Nyangbingo] Core GameEvents hub test failed.");
        }

        private void TestInventoryLargeStackOverflowGuard()
        {
            var item = ItemDefinition.CreateRuntime("inventory_large_stack", "Inventory Large Stack", int.MaxValue);
            var inventory = new Nyangbingo.Inventory.Inventory(id => id == item.Id ? item : null);
            var filledFirstSlot = inventory.TryAdd(item.Id, int.MaxValue);
            var usedSecondSlot = inventory.TryAdd(item.Id, 1);
            var countSaturated = inventory.Count(item.Id) == int.MaxValue && inventory.Has(item.Id, int.MaxValue);
            var removedLargeAmount = inventory.TryRemove(item.Id, int.MaxValue) && inventory.Count(item.Id) == 1;
            var removedRemainder = inventory.TryRemove(item.Id, 1) && inventory.Count(item.Id) == 0;
            var bodyOnlyItem = ItemDefinition.CreateRuntime("body_only", "Body Only", 0,
                ItemCategory.Tool, ItemMvpScope.A, "Non-inventory definition");
            var bodyOnlyInventory = new Nyangbingo.Inventory.Inventory(
                id => id == bodyOnlyItem.Id ? bodyOnlyItem : null);
            var bodyOnlyRejected = !bodyOnlyItem.IsInventoryItem &&
                                   !bodyOnlyInventory.TryAdd(bodyOnlyItem.Id, 1) &&
                                   !bodyOnlyInventory.CanImport(new[]
                                   {
                                       new InventorySlot { itemId = bodyOnlyItem.Id, amount = 1 }
                                   });

            if (filledFirstSlot && usedSecondSlot && countSaturated && removedLargeAmount && removedRemainder &&
                bodyOnlyRejected)
                Debug.Log("[Nyangbingo] Inventory large-stack, non-inventory item, overflow, and removal guard completed.");
            else Debug.LogError("[Nyangbingo] Inventory large-stack overflow guard test failed.");
        }

        private void TestJangdokStorageContract()
        {
            var wood = gameDataCatalog.FindItem("wood");
            var player = new Nyangbingo.Inventory.Inventory(gameDataCatalog.FindItem,
                Nyangbingo.Inventory.Inventory.SlotCount);
            var storageRuntime = new JangdokStorageRuntime(gameDataCatalog.FindItem,
                JangdokStorageRuntime.SlotCount);
            const string objectId = "jangdok_test_01";
            var registered = wood != null && storageRuntime.TryRegister(objectId) && player.TryAdd(wood.Id, 7) &&
                             player.TryTransferSlotTo(0,
                                 storageRuntime.TryGet(objectId, out var storage) ? storage : null);
            var exported = storageRuntime.Export();
            var restoredRuntime = new JangdokStorageRuntime(gameDataCatalog.FindItem,
                JangdokStorageRuntime.SlotCount);
            var restored = restoredRuntime.TryRestore(exported, new[] { objectId }) &&
                           restoredRuntime.TryGet(objectId, out var restoredStorage) &&
                           restoredStorage.Capacity == 40 && restoredStorage.Count(wood?.Id) == 7 &&
                           !restoredRuntime.CanRecover(objectId) && restoredStorage.TryTransferSlotTo(0, player) &&
                           restoredRuntime.CanRecover(objectId) && restoredRuntime.TryRemoveEmpty(objectId);
            var duplicateRejected = exported.Count == 1 &&
                                    !new JangdokStorageRuntime(gameDataCatalog.FindItem,
                                        JangdokStorageRuntime.SlotCount).TryRestore(
                                        new[] { exported[0], exported[0] }, new[] { objectId });
            if (registered && restored && duplicateRejected && player.Count(wood?.Id) == 7)
                Debug.Log("[Nyangbingo] v29 jangdok uses 40 independent slots, transfers atomically, blocks non-empty recovery, and saves by placed-object ID.");
            else Debug.LogError("[Nyangbingo] v29 jangdok storage contract test failed.");
        }

        private void TestV29InventoryCapacityAndLegacyPadding()
        {
            var item = ItemDefinition.CreateRuntime("inventory_v29_item", "v29 인벤토리 테스트", 99);
            var inventory = new Nyangbingo.Inventory.Inventory(id => id == item.Id ? item : null);
            var legacySlots = new System.Collections.Generic.List<InventorySlot>();
            legacySlots.Add(new InventorySlot { itemId = item.Id, amount = 7 });
            while (legacySlots.Count < 12) legacySlots.Add(default);

            var legacyPadded = inventory.TryImport(legacySlots) &&
                               inventory.Capacity == Nyangbingo.Inventory.Inventory.SlotCount &&
                               inventory.Slots.Count == 50 && inventory.Count(item.Id) == 7 &&
                               string.IsNullOrEmpty(inventory.Slots[49].itemId);
            var tooManySlots = new System.Collections.Generic.List<InventorySlot>(inventory.Export()) { default };
            var overflowRejected = !inventory.TryImport(tooManySlots);
            var officialGlobal = gameDataCatalog?.FindGlobal(GlobalKeys.InventorySlots);
            var officialValue = 0;
            var globalMatches = officialGlobal != null && officialGlobal.TryGetInt(out officialValue) &&
                                officialValue == 50;

            if (legacyPadded && overflowRejected && globalMatches)
                Debug.Log("[Nyangbingo] v29 inventory uses 50 slots and pads legacy 12-slot saves with empty slots.");
            else Debug.LogError("[Nyangbingo] v29 inventory capacity or legacy padding test failed.");
        }

        private void TestV29InventoryOwnedPlaceablePolicy()
        {
            var roof = gameDataCatalog?.FindItem("roof");
            var roofRecipe = gameDataCatalog?.FindRecipe("roof");
            var wood = gameDataCatalog?.FindItem("wood");
            var dirt = gameDataCatalog?.FindItem(WorldTileTypes.Dirt);
            var scopeBProduct = gameDataCatalog?.FindItem("singijeon_cart");
            var orphanPlaceable = ItemDefinition.CreateRuntime("orphan_placeable", "레시피 없는 설치물", 1,
                ItemCategory.Placeable, ItemMvpScope.A);
            var recipes = gameDataCatalog?.Recipes;

            var stationRequiredToCraft = roofRecipe != null &&
                                         roofRecipe.Station != CraftingStation.None &&
                                         !MainGameCraftingUiController.IsRecipeVisibleAtStation(
                                             roofRecipe.Station, CraftingStation.None);
            var ownedProductCanBePlacedAwayFromStation =
                MainGameCraftingUiController.IsInventoryItemPlaceable(roof, recipes);
            var paletteProductDoesNotRequireRecipeLookup =
                MainGameCraftingUiController.IsInventoryItemPlaceable(roof, null);
            var minedTerrainCanBeReplaced =
                MainGameCraftingUiController.IsInventoryItemPlaceable(dirt, recipes);
            var invalidProductsRejected =
                !MainGameCraftingUiController.IsInventoryItemPlaceable(wood, recipes) &&
                !MainGameCraftingUiController.IsInventoryItemPlaceable(scopeBProduct, recipes) &&
                !MainGameCraftingUiController.IsInventoryItemPlaceable(orphanPlaceable, recipes);

            if (stationRequiredToCraft && ownedProductCanBePlacedAwayFromStation &&
                paletteProductDoesNotRequireRecipeLookup && minedTerrainCanBeReplaced && invalidProductsRejected)
                Debug.Log("[Nyangbingo] v29 inventory-owned products and mined foreground tiles enter placement without a nearby station, while non-placeable resources, scope-B products, and unknown placeables stay blocked.");
            else Debug.LogError("[Nyangbingo] v29 inventory-owned placeable policy test failed.");
        }

        private void TestV28ActiveSlotToggleAndSave()
        {
            var club = ItemDefinition.CreateRuntime("dokkaebi_club", "도깨비 방망이", 1,
                ItemCategory.Weapon, ItemMvpScope.A);
            var fan = ItemDefinition.CreateRuntime("hapjukseon", "합죽선", 1,
                ItemCategory.Weapon, ItemMvpScope.A);
            ItemDefinition Find(string id) => id == club.Id ? club : id == fan.Id ? fan : null;

            var inventory = new Nyangbingo.Inventory.Inventory(Find);
            inventory.TryAdd(club.Id, 1);
            inventory.TryAdd(fan.Id, 1);
            var activeSlot = new ActiveSlotSystem(inventory, Find);
            var clubEquipped = activeSlot.TryEquip(club.Id) && inventory.Count(club.Id) == 0 &&
                               activeSlot.ResolveCombatProfileId("bare_claw") == club.Id;
            var clawToggled = activeSlot.Toggle() && !activeSlot.IsUsingEquippedItem &&
                              activeSlot.ResolveCombatProfileId("bare_claw") == "bare_claw";
            var fanEquipped = activeSlot.TryEquip(fan.Id) && inventory.Count(club.Id) == 1 &&
                              inventory.Count(fan.Id) == 0 && activeSlot.IsUsingEquippedItem;

            var save = new SaveGame { inventory = inventory.Export() };
            var captured = ActiveSlotSaveAdapter.Capture(save, activeSlot);
            var loaded = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(save));
            loaded.NormalizeAfterLoad();
            var restoredInventory = new Nyangbingo.Inventory.Inventory(Find);
            var restoredSlot = new ActiveSlotSystem(restoredInventory, Find);
            var restored = restoredInventory.TryImport(loaded.inventory) &&
                           ActiveSlotSaveAdapter.Restore(loaded, restoredSlot) &&
                           restoredSlot.EquippedItemId == fan.Id && restoredSlot.IsUsingEquippedItem &&
                           restoredInventory.Count(club.Id) == 1 && restoredInventory.Count(fan.Id) == 0;

            var legacy = JsonUtility.FromJson<SaveGame>("{\"schemaVersion\":14}");
            legacy.NormalizeAfterLoad();
            var legacySlot = new ActiveSlotSystem(new Nyangbingo.Inventory.Inventory(Find), Find);
            var legacyDefaultsToClaw = ActiveSlotSaveAdapter.Restore(legacy, legacySlot) &&
                                       !legacySlot.HasEquippedItem && !legacySlot.IsUsingEquippedItem;

            if (clubEquipped && clawToggled && fanEquipped && captured && restored && legacyDefaultsToClaw)
                Debug.Log("[Nyangbingo] v28 active slot equips one weapon/tool, toggles Q state, " +
                          "keeps mining claw-independent, and saves with legacy empty-slot fallback.");
            else Debug.LogError("[Nyangbingo] v28 active slot toggle or save test failed.");
        }

        private void TestPortableLanternActiveFuelAndSave()
        {
            var lantern = ItemDefinition.CreateRuntime(PortableLanternRuntime.LanternItemId, "휴대용 등불", 1,
                ItemCategory.Tool, ItemMvpScope.A);
            var coal = ItemDefinition.CreateRuntime(PortableLanternRuntime.FuelItemId, "석탄", 99,
                ItemCategory.Material, ItemMvpScope.A);
            ItemDefinition Find(string id) => id == lantern.Id ? lantern : id == coal.Id ? coal : null;

            var inventory = new Nyangbingo.Inventory.Inventory(Find);
            inventory.TryAdd(lantern.Id, 1);
            inventory.TryAdd(coal.Id, 2);
            var activeSlot = new ActiveSlotSystem(inventory, Find);
            var portable = new PortableLanternRuntime(inventory, activeSlot, 3f);

            var equippedWithoutFuelIsDark = activeSlot.TryEquip(lantern.Id) && !portable.IsLit;
            var fueled = portable.TryAddFuel() && inventory.Count(coal.Id) == 1 && portable.IsLit &&
                         Mathf.Approximately(portable.FuelRemainingSeconds, 270f);
            portable.Tick(10f);
            var activeConsumption = Mathf.Approximately(portable.FuelRemainingSeconds, 260f);
            var toggledToClaw = activeSlot.Toggle() && !portable.IsLit;
            portable.Tick(100f);
            var pausedWhileClawActive = Mathf.Approximately(portable.FuelRemainingSeconds, 260f);
            activeSlot.Toggle();
            portable.Tick(60f);

            var save = new SaveGame { inventory = inventory.Export() };
            var captured = ActiveSlotSaveAdapter.Capture(save, activeSlot) &&
                           PortableLanternSaveAdapter.Capture(save, portable);
            var loaded = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(save));
            loaded.NormalizeAfterLoad();
            var restoredInventory = new Nyangbingo.Inventory.Inventory(Find);
            var restoredSlot = new ActiveSlotSystem(restoredInventory, Find);
            var restoredPortable = new PortableLanternRuntime(restoredInventory, restoredSlot, 3f);
            var restored = restoredInventory.TryImport(loaded.inventory) &&
                           ActiveSlotSaveAdapter.Restore(loaded, restoredSlot) &&
                           PortableLanternSaveAdapter.Restore(loaded, restoredPortable) &&
                           restoredPortable.IsLit &&
                           Mathf.Approximately(restoredPortable.FuelRemainingSeconds, 200f);
            restoredPortable.Tick(200f);
            var exhaustsCleanly = !restoredPortable.IsLit &&
                                  Mathf.Approximately(restoredPortable.FuelRemainingSeconds, 0f) &&
                                  !restoredPortable.TryRestore(float.NaN) &&
                                  !restoredPortable.TryRestore(-1f);

            var legacy = JsonUtility.FromJson<SaveGame>("{\"schemaVersion\":14}");
            legacy.NormalizeAfterLoad();
            var legacyInventory = new Nyangbingo.Inventory.Inventory(Find);
            var legacySlot = new ActiveSlotSystem(legacyInventory, Find);
            var legacyPortable = new PortableLanternRuntime(legacyInventory, legacySlot, 3f);
            var legacyDefaultsEmpty = PortableLanternSaveAdapter.Restore(legacy, legacyPortable) &&
                                      Mathf.Approximately(legacyPortable.FuelRemainingSeconds, 0f);

            portable.Dispose();
            restoredPortable.Dispose();
            legacyPortable.Dispose();

            if (equippedWithoutFuelIsDark && fueled && activeConsumption && toggledToClaw &&
                pausedWhileClawActive && captured && restored && exhaustsCleanly && legacyDefaultsEmpty)
                Debug.Log("[Nyangbingo] Portable lantern lights radius 3 only while active, burns coal for 270 game-seconds, pauses on claw, and saves fuel.");
            else Debug.LogError("[Nyangbingo] Portable lantern active-slot, fuel, or save test failed.");
        }

        private void TestSaveJsonSchemaRejection()
        {
            var emptyRejected = !SaveManager.TryDeserialize("   ", out _);
            var futureRejected = !SaveManager.TryDeserialize(
                $"{{\"schemaVersion\":{SaveGame.CurrentSchemaVersion + 1}}}", out _);
            var legacyAccepted = SaveManager.TryDeserialize("{\"schemaVersion\":5}", out var legacy) &&
                                 legacy != null && legacy.schemaVersion == SaveGame.CurrentSchemaVersion &&
                                 legacy.inventory != null && legacy.dogam != null &&
                                 legacy.pendingItemAcquisitions != null;

            if (emptyRejected && futureRejected && legacyAccepted)
                Debug.Log("[Nyangbingo] Save JSON future-schema rejection and legacy normalization completed.");
            else Debug.LogError("[Nyangbingo] Save JSON schema validation test failed.");
        }

        private void TestRegularEncounterSaveRoundTrip()
        {
            var source = new SaveGame
            {
                regularEncounter = new RegularEncounterStateRecord
                {
                    hasValue = true,
                    day = 7,
                    isNight = true,
                    discardRegularForCurrentNight = true,
                    remainingRegularYokaiIds = new System.Collections.Generic.List<string>()
                }
            };
            var roundTripSucceeded = SaveManager.TryDeserialize(JsonUtility.ToJson(source), out var restored);
            var suppressionPreserved = roundTripSucceeded && restored.regularEncounter.hasValue &&
                                       restored.regularEncounter.day == 7 && restored.regularEncounter.isNight &&
                                       restored.regularEncounter.discardRegularForCurrentNight &&
                                       restored.regularEncounter.remainingRegularYokaiIds.Count == 0;
            var legacySucceeded = SaveManager.TryDeserialize("{\"schemaVersion\":10}", out var legacy);
            var legacyUsesFallback = legacySucceeded && legacy.regularEncounter != null &&
                                     !legacy.regularEncounter.hasValue &&
                                     legacy.regularEncounter.remainingRegularYokaiIds.Count == 0;

            if (suppressionPreserved && legacyUsesFallback)
                Debug.Log("[Nyangbingo] Regular yokai encounter suppression save round-trip completed.");
            else
                Debug.LogError("[Nyangbingo] Regular yokai encounter suppression save round-trip test failed.");
        }

        private void TestV24SaveIdMigration()
        {
            var legacy = new SaveGame { schemaVersion = 7 };
            legacy.inventory.Add(new InventorySlot { itemId = "fox_rain_charm", amount = 99 });
            legacy.inventory.Add(new InventorySlot { itemId = "yokai_tears", amount = 98 });
            while (legacy.inventory.Count < Nyangbingo.Inventory.Inventory.SlotCount)
                legacy.inventory.Add(new InventorySlot { itemId = "migration_blocker", amount = 99 });
            legacy.unlockedRecipes.Add("ice_steel_claws");
            legacy.unlockedRecipes.Add("fox_rain_charm");
            legacy.placedObjects.Add("foundry");
            legacy.placedObjectRecords.Add(new PlacedObjectRecord { objectId = "legacy_scale", definitionId = "reverse_scale" });
            legacy.equipment.Add(new EquipmentRecord { slot = EquipmentSlot.AccessoryOne.ToString(), equipmentId = "tiger_eye_orb" });
            legacy.ownedEquipmentIds.Add("wind_ribbon");
            legacy.pendingItemAcquisitions.Add(new PendingItemRecord { itemId = "club_fragment", amount = 1 });
            legacy.activeCrafting = new CraftingProcessRecord { active = true, recipeId = "ice_steel_claws", remainingGameSeconds = 2f };
            legacy.smelting.Add(new SmeltingRecord { stationId = "foundry", recipeId = "smelt_ice_steel", isActive = true });
            legacy.smeltingOutputs.Add(new SmeltingOutputRecord { stationId = "foundry", itemId = "ice_steel_ingot", amount = 1 });
            legacy.bossRecords.Add(new BossRecord { bossId = "goblin_chief", count = 1, firstDay = 8 });
            legacy.forcedBossEncounters.Add(new ForcedBossEncounterRecord { bossId = "goblin_chief", triggered = true });
            legacy.dogam.Add(new CodexRecord { yokaiId = "gangcheori", kills = 4 });
            legacy.dogam.Add(new CodexRecord { yokaiId = "gangcheol", kills = 6 });
            legacy.dogam.Add(new CodexRecord { yokaiId = "club_goblin", kills = 1 });
            legacy.dogam.Add(new CodexRecord { yokaiId = "yagwanggwi", kills = 2 });
            legacy.utilityCooldowns.Add(new UtilityCooldownRecord
            {
                kind = "FoxRainCharm",
                remainingGameSeconds = 10f
            });
            legacy.activeBoss = new ActiveBossStateRecord { active = true, bossId = "gangcheori", currentHealth = 100 };

            legacy.NormalizeAfterLoad();

            long tearTotal = 0;
            var hasFoxRainCharm = false;
            for (var i = 0; i < legacy.inventory.Count; i++)
            {
                var slot = legacy.inventory[i];
                if (slot.itemId == "yokai_tear") tearTotal += slot.amount;
                if (slot.itemId == "fox_rain_charm") hasFoxRainCharm = true;
            }
            var pendingClubShard = false;
            for (var i = 0; i < legacy.pendingItemAcquisitions.Count; i++)
            {
                var record = legacy.pendingItemAcquisitions[i];
                if (record.itemId == "yokai_tear") tearTotal += record.amount;
                if (record.itemId == "club_shard" && record.amount == 1) pendingClubShard = true;
                if (record.itemId == "fox_rain_charm") hasFoxRainCharm = true;
            }
            var bossMergeMatches = legacy.bossRecords.Count == 1;
            for (var i = 0; i < legacy.bossRecords.Count; i++)
            {
                var record = legacy.bossRecords[i];
                if (record.bossId == "king_dokkaebi") bossMergeMatches &= record.count == 1;
                else bossMergeMatches = false;
            }
            var forcedMergeMatches = legacy.forcedBossEncounters.Count == 1 &&
                                     legacy.forcedBossEncounters[0].bossId == "king_dokkaebi" &&
                                     legacy.forcedBossEncounters[0].triggered;
            var codexMatches = legacy.dogam.Count == 3;
            for (var i = 0; i < legacy.dogam.Count; i++)
            {
                var record = legacy.dogam[i];
                if (record.yokaiId == "gangcheol") codexMatches &= record.kills == 10;
                else if (record.yokaiId == "club") codexMatches &= record.kills == 1;
                else if (record.yokaiId == "yakwang") codexMatches &= record.kills == 2;
                else codexMatches = false;
            }
            var itemFieldsMatch = legacy.unlockedRecipes.Count == 1 && legacy.unlockedRecipes[0] == "icesteel_claw" &&
                                  legacy.placedObjects.Count == 1 && legacy.placedObjects[0] == "blast_furnace" &&
                                  legacy.placedObjectRecords.Count == 1 && legacy.placedObjectRecords[0].definitionId == "gangcheol_scale" &&
                                  legacy.equipment.Count == 1 && legacy.equipment[0].equipmentId == "tiger_eye_bead" &&
                                  legacy.ownedEquipmentIds.Count == 1 && legacy.ownedEquipmentIds[0] == "wind_daenggi" &&
                                  pendingClubShard && legacy.activeCrafting.recipeId == "icesteel_claw";
            var smeltingMatches = legacy.smelting.Count == 1 && legacy.smelting[0].stationId == "blast_furnace" &&
                                  legacy.smelting[0].recipeId == "smelt_icesteel" && legacy.smeltingOutputs.Count == 1 &&
                                  legacy.smeltingOutputs[0].stationId == "blast_furnace" &&
                                  legacy.smeltingOutputs[0].itemId == "icesteel_ingot";
            var removedStateMatches = !hasFoxRainCharm && legacy.utilityCooldowns.Count == 0 &&
                                      legacy.activeBoss != null && !legacy.activeBoss.active;

            if (legacy.schemaVersion == SaveGame.CurrentSchemaVersion && tearTotal == 395 &&
                bossMergeMatches && forcedMergeMatches && codexMatches && itemFieldsMatch && smeltingMatches &&
                removedStateMatches)
                Debug.Log("[Nyangbingo] v24.1 save ID migration, collision merge, and fox-rain refund completed.");
            else Debug.LogError("[Nyangbingo] v24.1 save ID migration test failed.");
        }

        private void TestSaveManagerInputValidation(SaveManager saveManager)
        {
            var nullRejected = false;
            var futureSchemaRejected = false;
            var negativeSlotRejected = false;
            var upperSlotRejected = false;
            try
            {
                saveManager.Save(0, null);
            }
            catch (System.ArgumentNullException)
            {
                nullRejected = true;
            }

            try
            {
                saveManager.Save(0, new SaveGame { schemaVersion = SaveGame.CurrentSchemaVersion + 1 });
            }
            catch (System.ArgumentException)
            {
                futureSchemaRejected = true;
            }

            try
            {
                saveManager.TryLoad(-1, out _);
            }
            catch (System.ArgumentOutOfRangeException)
            {
                negativeSlotRejected = true;
            }

            try
            {
                saveManager.Delete(SaveManager.SlotCount);
            }
            catch (System.ArgumentOutOfRangeException)
            {
                upperSlotRejected = true;
            }

            if (nullRejected && futureSchemaRejected && negativeSlotRejected && upperSlotRejected)
                Debug.Log("[Nyangbingo] Save manager null, future-schema, and slot-boundary validation completed.");
            else Debug.LogError("[Nyangbingo] Save manager input validation test failed.");
        }

        private void TestSaveManagerAtomicReplacement(SaveManager saveManager)
        {
            const int slot = 0;
            var first = new SaveGame { seed = 101, day = 2 };
            var replacement = new SaveGame { seed = 202, day = 3 };
            saveManager.Save(slot, first);
            saveManager.Save(slot, replacement);
            var loadedReplacement = saveManager.TryLoad(slot, out var loaded) && loaded != null &&
                                    loaded.seed == replacement.seed && loaded.day == replacement.day;
            var temporaryPath = System.IO.Path.Combine(Application.persistentDataPath,
                $"nyangbingo-save-{slot}.json.tmp");

            if (loadedReplacement && !System.IO.File.Exists(temporaryPath))
                Debug.Log("[Nyangbingo] Save manager atomic replacement and temporary-file cleanup completed.");
            else Debug.LogError("[Nyangbingo] Save manager atomic replacement test failed.");
        }

        private void TestGameDataCatalog()
        {
            if (gameDataCatalog == null)
            {
                Debug.LogError("[Nyangbingo] Game data catalog reference is missing.");
                return;
            }

            var valid = gameDataCatalog.IsValid &&
                        ValidateCatalogEntries(gameDataCatalog.Items, value => value.Id, gameDataCatalog.FindItem) &&
                        ValidateCatalogEntries(gameDataCatalog.Recipes, value => value.Id, gameDataCatalog.FindRecipe) &&
                        ValidateCatalogEntries(gameDataCatalog.Modules, value => value.Id, gameDataCatalog.FindModule) &&
                        ValidateCatalogEntries(gameDataCatalog.MineralTiers, value => value.Id,
                            gameDataCatalog.FindMineralTier) &&
                        ValidateCatalogEntries(gameDataCatalog.SealWhitelist, value => value.Element,
                            gameDataCatalog.FindSealRule) &&
                        ValidateCatalogEntries(gameDataCatalog.IdMigrations, value => value.Key,
                            key =>
                            {
                                var separator = key.IndexOf(':');
                                return separator > 0 && System.Enum.TryParse(key.Substring(0, separator), out IdMigrationDomain domain)
                                    ? gameDataCatalog.FindIdMigration(domain, key.Substring(separator + 1)) : null;
                            }) &&
                        ValidateCatalogEntries(gameDataCatalog.DayCurves, value => value.Id,
                            id => int.TryParse(id, out var day) ? gameDataCatalog.FindDayCurve(day) : null) &&
                        ValidateCatalogEntries(gameDataCatalog.Globals, value => value.Key,
                            gameDataCatalog.FindGlobal) &&
                        ValidateCatalogEntries(gameDataCatalog.Smelting, value => value.Id, gameDataCatalog.FindSmelting) &&
                        ValidateCatalogEntries(gameDataCatalog.Equipment, value => value.Id, gameDataCatalog.FindEquipment) &&
                        ValidateCatalogEntries(gameDataCatalog.Utilities, value => value.Id, gameDataCatalog.FindUtility) &&
                        ValidateCatalogEntries(gameDataCatalog.CombatProfiles, value => value.Id, gameDataCatalog.FindCombatProfile) &&
                        ValidateCatalogEntries(gameDataCatalog.Yokai, value => value.Id, gameDataCatalog.FindYokai) &&
                        ValidateCatalogEntries(gameDataCatalog.Bosses, value => value.Id, gameDataCatalog.FindBoss) &&
                        ValidateCatalogEntries(gameDataCatalog.Chests, value => value.Id, gameDataCatalog.FindChest) &&
                        ValidateCatalogEntries(gameDataCatalog.DayEvents, value => value.Id, gameDataCatalog.FindDayEvent) &&
                        importedTimedRecipe != null && gameDataCatalog.FindRecipe(importedTimedRecipe.Id) == importedTimedRecipe &&
                        importedBaekjungEvent != null && gameDataCatalog.FindDayEvent(importedBaekjungEvent.Id) == importedBaekjungEvent &&
                        importedClubGoblin != null && gameDataCatalog.FindYokai(importedClubGoblin.Id) == importedClubGoblin &&
                        gameDataCatalog.FindItem("__missing__") == null &&
                        gameDataCatalog.FindRecipe("__missing__") == null &&
                        gameDataCatalog.FindModule("__missing__") == null &&
                        gameDataCatalog.FindMineralTier("__missing__") == null &&
                        gameDataCatalog.FindSealRule("__missing__") == null &&
                        gameDataCatalog.FindIdMigration(IdMigrationDomain.Item, "__missing__") == null &&
                        gameDataCatalog.FindDayCurve(0) == null && gameDataCatalog.FindDayCurve(31) == null &&
                        gameDataCatalog.FindGlobal("__missing__") == null &&
                        gameDataCatalog.FindSmelting("__missing__") == null &&
                        gameDataCatalog.FindEquipment("__missing__") == null &&
                        gameDataCatalog.FindUtility("__missing__") == null &&
                        gameDataCatalog.FindCombatProfile("__missing__") == null &&
                        gameDataCatalog.FindYokai("__missing__") == null &&
                        gameDataCatalog.FindBoss("__missing__") == null &&
                        gameDataCatalog.FindChest("__missing__") == null &&
                        gameDataCatalog.FindDayEvent("__missing__") == null;

            if (valid)
                Debug.Log($"[Nyangbingo] Game data catalog ID lookup completed: {gameDataCatalog.Items.Count} items, " +
                          $"{gameDataCatalog.Recipes.Count} recipes, {gameDataCatalog.Modules.Count} modules, " +
                          $"{gameDataCatalog.MineralTiers.Count} mineral tiers, " +
                          $"{gameDataCatalog.SealWhitelist.Count} seal rules, " +
                          $"{gameDataCatalog.IdMigrations.Count} ID migrations, " +
                          $"{gameDataCatalog.DayCurves.Count} day curves, " +
                          $"{gameDataCatalog.Globals.Count} globals, " +
                          $"{gameDataCatalog.Smelting.Count} smelting, " +
                          $"{gameDataCatalog.Equipment.Count} equipment, {gameDataCatalog.Utilities.Count} utilities, " +
                          $"{gameDataCatalog.CombatProfiles.Count} combat profiles, " +
                          $"{gameDataCatalog.Yokai.Count} yokai, {gameDataCatalog.Bosses.Count} bosses, " +
                          $"{gameDataCatalog.Chests.Count} chests, " +
                          $"{gameDataCatalog.DayEvents.Count} day events.");
            else
                Debug.LogError("[Nyangbingo] Game data catalog ID lookup test failed.");
        }

        private void TestImportedV26DataContract()
        {
            if (gameDataCatalog == null)
            {
                Debug.LogError("[Nyangbingo] v26 data contract catalog reference is missing.");
                return;
            }

            var wallpaperItem = gameDataCatalog.FindItem("wallpaper");
            var wallpaperRecipe = gameDataCatalog.FindRecipe("wallpaper");
            var wallpaperRule = gameDataCatalog.FindSealRule("배경벽(벽지)");
            var coverage = gameDataCatalog.FindGlobal("wallpaper_coverage");
            var durationBonus = gameDataCatalog.FindGlobal("wallpaper_coldsource_bonus");
            var removeRule = gameDataCatalog.FindGlobal("wallpaper_remove_rule");
            var scopeACount = 0;
            var scopeBCount = 0;
            var productVisibleCount = 0;
            var productScopeBLeak = false;
            for (var index = 0; index < gameDataCatalog.Recipes.Count; index++)
            {
                var recipe = gameDataCatalog.Recipes[index];
                if (recipe == null) continue;
                if (recipe.MvpScope == ItemMvpScope.A) scopeACount++;
                else if (recipe.MvpScope == ItemMvpScope.B) scopeBCount++;
                if (MainGameCraftingUiController.ShouldShowRecipe(recipe, true))
                {
                    productVisibleCount++;
                    productScopeBLeak |= recipe.MvpScope == ItemMvpScope.B;
                }
            }

            var valid = gameDataCatalog.Items.Count == 86 && gameDataCatalog.Recipes.Count == 53 &&
                        gameDataCatalog.Globals.Count == 100 && gameDataCatalog.SealWhitelist.Count == 23 &&
                        scopeACount == 51 && scopeBCount == 2 && productVisibleCount == 51 &&
                        !productScopeBLeak &&
                        wallpaperItem != null && wallpaperItem.Category == ItemCategory.Placeable &&
                        wallpaperItem.MvpScope == ItemMvpScope.A &&
                        wallpaperRecipe != null && wallpaperRecipe.Station == CraftingStation.Workbench &&
                        wallpaperRecipe.Output.item == wallpaperItem && wallpaperRecipe.Output.amount == 16 &&
                        wallpaperRecipe.DurationSeconds == 5f && wallpaperRecipe.Type == RecipeType.Placeable &&
                        wallpaperRecipe.MvpScope == ItemMvpScope.A &&
                        RecipeHasIngredient(wallpaperRecipe, "clay", 3) &&
                        RecipeHasIngredient(wallpaperRecipe, "wood", 5) &&
                        wallpaperRule != null && !wallpaperRule.Seals &&
                        coverage != null && coverage.Value == "100" &&
                        durationBonus != null && durationBonus.Value == "25" &&
                        removeRule != null && removeRule.Value == "restore_original" &&
                        IsScopeBRecipe("iron_bait_pile") && IsScopeBRecipe("singijeon_cart") &&
                        !IsScopeBRecipe("ice_altar_offering") && !IsScopeBRecipe("ice_crystal_cooler");

            if (valid)
                Debug.Log("[Nyangbingo] v34.1 data contract completed: 86 items, 53 recipes (A51/B2), " +
                          "wallpaper output 16, 100 globals, and 23 seal rules.");
            else
                Debug.LogError("[Nyangbingo] v29 data contract test failed.");
        }

        private bool IsScopeBRecipe(string id)
        {
            var item = gameDataCatalog.FindItem(id);
            var recipe = gameDataCatalog.FindRecipe(id);
            return item != null && item.MvpScope == ItemMvpScope.B && recipe != null &&
                   recipe.Output.item == item && recipe.Output.amount == 1 && recipe.MvpScope == ItemMvpScope.B;
        }

        private void TestCoolingSourceRuntimeAndSaveContract()
        {
            if (gameDataCatalog == null)
            {
                Debug.LogError("[Nyangbingo] Cooling source runtime catalog reference is missing.");
                return;
            }

            var runtime = new CoolingSourceRuntime(gameDataCatalog);
            var expiredCount = 0;
            runtime.ConsumableExpired += _ => expiredCount++;
            var registered = runtime.TryRegister("water_1", CoolingSourceRuntime.WaterJarId) &&
                             runtime.TryRegister("ice_jar_1", CoolingSourceRuntime.IceJarId, true);
            var initial = registered && runtime.Count == 2 && runtime.ActiveCount == 2 &&
                          Mathf.Approximately(runtime.CoolingCapPercent, 50f);
            runtime.Tick(180f);
            var waterExpired = expiredCount == 1 && runtime.Count == 1 && runtime.ActiveCount == 1 &&
                               runtime.TryGetRemaining("ice_jar_1", out var iceRemaining) &&
                               Mathf.Approximately(iceRemaining, 120f);
            runtime.Tick(120f);
            var fuelExhausted = runtime.Count == 1 && runtime.ActiveCount == 0 &&
                                Mathf.Approximately(runtime.CoolingCapPercent, 0f);
            var refueled = runtime.TryAddIceFuel("ice_jar_1", 2) &&
                           runtime.TryGetRemaining("ice_jar_1", out iceRemaining) &&
                           Mathf.Approximately(iceRemaining, 600f) && runtime.ActiveCount == 1 &&
                           Mathf.Approximately(runtime.CoolingCapPercent, 50f);
            var productStatus = runtime.TryGetStatus("ice_jar_1", out var statusRemaining,
                                    out var statusCap, out var statusActive) && statusActive &&
                                Mathf.Approximately(statusRemaining, 600f) &&
                                Mathf.Approximately(statusCap, 50f);
            var permanent = runtime.TryRegister("storage_1", CoolingSourceRuntime.IceStorageId) &&
                            runtime.TryRegister("cooler_1", CoolingSourceRuntime.IceCrystalCoolerId) &&
                            runtime.ActiveCount == 3 && Mathf.Approximately(runtime.CoolingCapPercent, 100f);

            var save = new SaveGame();
            var snapshots = runtime.ExportSnapshots();
            for (var index = 0; index < snapshots.Count; index++)
            {
                var snapshot = snapshots[index];
                save.coolingSources.Add(new CoolingSourceStateRecord
                {
                    objectId = snapshot.ObjectId,
                    definitionId = snapshot.DefinitionId,
                    remainingGameSeconds = snapshot.RemainingGameSeconds
                });
            }
            var serialized = JsonUtility.ToJson(save);
            var loaded = JsonUtility.FromJson<SaveGame>(serialized);
            loaded.NormalizeAfterLoad();
            var restored = new CoolingSourceRuntime(gameDataCatalog);
            var restoredAll = loaded.schemaVersion == SaveGame.CurrentSchemaVersion &&
                              loaded.coolingSources.Count == 3;
            for (var index = 0; index < loaded.coolingSources.Count; index++)
            {
                var state = loaded.coolingSources[index];
                restoredAll &= restored.TryRestore(
                    state.objectId, state.definitionId, state.remainingGameSeconds);
            }
            restoredAll &= restored.Count == 3 && restored.ActiveCount == 3 &&
                           Mathf.Approximately(restored.CoolingCapPercent, 100f) &&
                           restored.TryGetRemaining("ice_jar_1", out var restoredFuel) &&
                           Mathf.Approximately(restoredFuel, 600f);

            var invalidGuard = !runtime.TryAddIceFuel("ice_jar_1", 0) &&
                               !runtime.TryAddIceFuel("storage_1", 1) &&
                               !runtime.TryRestore("bad", CoolingSourceRuntime.WaterJarId, float.NaN);

            if (initial && waterExpired && fuelExhausted && refueled && productStatus && permanent &&
                restoredAll && invalidGuard)
                Debug.Log("[Nyangbingo] Cooling sources completed: water jar 180 seconds, ice jar 300-second fuel, " +
                          "25/50/100% caps, permanent sources, and versioned save round-trip.");
            else
                Debug.LogError("[Nyangbingo] Cooling source lifetime, cap, or save contract test failed.");
        }

        private void TestPlayerHealthRecoveryContract()
        {
            if (gameDataCatalog == null)
            {
                Debug.LogError("[Nyangbingo] Player health recovery catalog reference is missing.");
                return;
            }

            var inventory = new Inventory.Inventory(gameDataCatalog.FindItem);
            var healthObject = new GameObject("TemporaryPlayerHealth");
            var health = healthObject.AddComponent<Health>();
            health.ConfigureForRuntime(100);
            var recovery = new PlayerHealthRecoveryService(inventory, health, 10f, 1f, 25);
            var valid = inventory.TryAdd(PlayerHealthRecoveryService.CatnipItemId, 2);

            health.ApplyDamage(50, DamageTag.Melee);
            recovery.Tick(9f);
            valid &= health.Current == 50;
            recovery.Tick(1f);
            valid &= health.Current == 50;
            recovery.Tick(1f);
            valid &= health.Current == 51;

            health.ApplyDamage(10, DamageTag.Melee);
            recovery.Tick(10f);
            valid &= health.Current == 41;
            recovery.Tick(2f);
            valid &= health.Current == 43;
            valid &= recovery.TryUseCatnip(out var restored) && restored == 25 &&
                     health.Current == 68 && inventory.Count(PlayerHealthRecoveryService.CatnipItemId) == 1;
            health.Heal(100);
            valid &= !recovery.TryUseCatnip(out _) &&
                     inventory.Count(PlayerHealthRecoveryService.CatnipItemId) == 1;

            recovery.Dispose();
            Destroy(healthObject);

            if (valid)
                Debug.Log("[Nyangbingo] Player HP recovery completed: 10-game-second damage reset, " +
                          "1 HP/game-second natural regeneration, and catnip +25 consumption.");
            else
                Debug.LogError("[Nyangbingo] Player HP recovery or catnip contract test failed.");
        }

        private void TestDeathTearPouchRuntimeContract()
        {
            if (gameDataCatalog == null)
            {
                Debug.LogError("[Nyangbingo] Death tear pouch runtime catalog reference is missing.");
                return;
            }

            var tear = gameDataCatalog.FindItem(DeathTearPouchRuntime.TearItemId);
            var timeObject = new GameObject("TemporaryDeathPouchTimeSource");
            var time = timeObject.AddComponent<DayNightService>();
            var valid = tear != null && time.ConfigureOfficialData(gameDataCatalog) &&
                        time.RestoreTimeState(5, 0f, false);
            var inventory = new Nyangbingo.Inventory.Inventory(
                id => id == DeathTearPouchRuntime.TearItemId ? tear : null);
            valid &= inventory.TryAdd(DeathTearPouchRuntime.TearItemId, 24);
            var runtime = new DeathTearPouchRuntime(inventory, time);
            var firstDrop = runtime.DropTwentyPercent(new Vector2(2f, 3f));
            valid &= firstDrop == 4 && inventory.Count(DeathTearPouchRuntime.TearItemId) == 20 &&
                     runtime.Active.Count == 1 && runtime.Active[0].expireOnDay == 7;
            valid &= inventory.TryAdd(DeathTearPouchRuntime.TearItemId, 5);
            var secondDrop = runtime.DropTwentyPercent(new Vector2(8f, 9f));
            valid &= secondDrop == 5 && inventory.Count(DeathTearPouchRuntime.TearItemId) == 20 &&
                     runtime.Active.Count == 2;

            var save = new SaveGame { deathTearPouches = runtime.Export() };
            var loaded = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(save));
            loaded?.NormalizeAfterLoad();
            var restoredInventory = new Nyangbingo.Inventory.Inventory(
                id => id == DeathTearPouchRuntime.TearItemId ? tear : null);
            var restored = new DeathTearPouchRuntime(restoredInventory, time);
            valid &= loaded != null && loaded.schemaVersion == SaveGame.CurrentSchemaVersion &&
                     restored.Restore(loaded.deathTearPouches) && restored.Active.Count == 2 &&
                     restored.TryCollectWithin(new Vector2(2f, 3f), .1f) &&
                     restoredInventory.Count(DeathTearPouchRuntime.TearItemId) == 4 && restored.Active.Count == 1;

            time.Tick(time.CycleLengthSeconds);
            valid &= time.Day == 6 && restored.Active.Count == 1;
            time.Tick(time.CycleLengthSeconds);
            valid &= time.Day == 7 && restored.Active.Count == 0;

            restored.Dispose();
            runtime.Dispose();
            Destroy(timeObject);
            if (valid)
                Debug.Log("[Nyangbingo] Death and respawn tear penalty completed: floor 20%, one pouch per death, " +
                          "proximity recovery, D+1 night expiry, and schema v13 save round-trip.");
            else
                Debug.LogError("[Nyangbingo] Death tear drop, recovery, expiry, or save contract test failed.");
        }

        private static bool RecipeHasIngredient(RecipeDefinition recipe, string itemId, int amount)
        {
            if (recipe?.Ingredients == null) return false;
            for (var index = 0; index < recipe.Ingredients.Length; index++)
            {
                var ingredient = recipe.Ingredients[index];
                if (ingredient.item != null && ingredient.item.Id == itemId && ingredient.amount == amount)
                    return true;
            }
            return false;
        }

        private void TestImportedModules()
        {
            if (gameDataCatalog == null)
            {
                Debug.LogError("[Nyangbingo] Imported module catalog reference is missing.");
                return;
            }

            var ids = new[] { "insul_wall", "door", "roof", "jangdok", "ice_core" };
            var names = new[] { "차열벽", "단열 문", "차열 지붕", "장독 창고", "얼음 저장고" };
            var roles = new[]
            {
                "기본 방벽 HP600", "통행+flood-fill 밀폐 인정", "상부 밀폐", "보관함 40슬롯", "체온 회복 거점(코어)"
            };
            var buildTimes = new[] { 5f, 30f, 20f, 45f, 60f };
            var priorities = new[]
            {
                ModulePriority.P0, ModulePriority.P0, ModulePriority.P0, ModulePriority.P1, ModulePriority.P0
            };
            var materials = new[]
            {
                new[] { "stone:2", "dirt:1" },
                new[] { "wood:6", "hemp_stalk:4", "ice_shard:4" },
                new[] { "stone:6", "rebar:3", "hemp_stalk:2" },
                new[] { "dirt:10", "stone:8", "wood:6" },
                new[] { "ice_shard:20", "stone:10", "iron_ingot:2" }
            };

            var valid = gameDataCatalog.Modules.Count == ids.Length;
            for (var i = 0; i < ids.Length; i++)
            {
                var definition = gameDataCatalog.FindModule(ids[i]);
                var recipe = gameDataCatalog.FindRecipe(ids[i]);
                valid &= definition != null && definition.Id == ids[i] && definition.DisplayName == names[i] &&
                         definition.Item == gameDataCatalog.FindItem(ids[i]) && definition.Role == roles[i] &&
                         Mathf.Approximately(definition.BuildTimeSeconds, buildTimes[i]) &&
                         definition.Priority == priorities[i] && MatchesItemAmounts(definition.Materials, materials[i]) &&
                         recipe != null && recipe.Output.item == definition.Item &&
                         Mathf.Approximately(recipe.DurationSeconds, definition.BuildTimeSeconds) &&
                         MatchesItemAmounts(recipe.Ingredients, materials[i]);
            }

            if (valid)
                Debug.Log("[Nyangbingo] Official five module definitions, recipes, priorities, and materials completed.");
            else Debug.LogError("[Nyangbingo] Imported module definition test failed.");
        }

        private void TestImportedMineralTiers()
        {
            if (gameDataCatalog == null)
            {
                Debug.LogError("[Nyangbingo] Imported mineral tier catalog reference is missing.");
                return;
            }

            var ids = new[]
            {
                "wood", "hemp_stalk", "rebar", "dirt", "stone", "coal", "iron_ore", "copper_ore",
                "ice_shard", "icesteel_ore", "frost_essence", "clay"
            };
            var layers = new[]
            {
                MineralLayer.SurfaceNight, MineralLayer.SurfaceNight, MineralLayer.SurfaceRuinNight,
                MineralLayer.UndergroundUpper, MineralLayer.UndergroundUpper, MineralLayer.UndergroundUpper,
                MineralLayer.UndergroundMiddle, MineralLayer.UndergroundMiddle, MineralLayer.UndergroundMiddle,
                MineralLayer.UndergroundDeep, MineralLayer.UndergroundDeep, MineralLayer.UndergroundUpper
            };
            var minimumDepths = new[] { 0, 0, 0, 1, 1, 1, 46, 46, 46, 91, 91, 1 };
            var maximumDepths = new[] { 0, 0, 0, 45, 45, 45, 90, 90, 90, 135, 135, 45 };
            var minimumClawTiers = new[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 1 };
            var gates = new[]
            {
                MiningGateType.None, MiningGateType.None, MiningGateType.None, MiningGateType.None,
                MiningGateType.None, MiningGateType.None, MiningGateType.Soft, MiningGateType.Soft,
                MiningGateType.None, MiningGateType.Hard, MiningGateType.Hard, MiningGateType.None
            };
            var tierOne = new[] { 1.5f, .5f, 2f, 1f, 1f, 1.6f, 3f, 3f, 2f, -1f, -1f, 1.2f };
            var tierTwo = new[] { .75f, .25f, 1f, .5f, .5f, .8f, 1.5f, 1.5f, 1f, 4f, 6f, .6f };
            var tierThree = new[] { .4f, .25f, .5f, .25f, .25f, .4f, .75f, .75f, .5f, 2f, 3f, .3f };
            var frequencies = new[] { 8f, 10f, 6f, 45f, 25f, 8f, 18f, 12f, 10f, 12f, 4f, 10f };

            var valid = gameDataCatalog.MineralTiers.Count == ids.Length;
            for (var i = 0; i < ids.Length; i++)
            {
                var definition = gameDataCatalog.FindMineralTier(ids[i]);
                valid &= definition != null && definition.Id == ids[i] &&
                         definition.Resource == gameDataCatalog.FindItem(ids[i]) &&
                         !string.IsNullOrWhiteSpace(definition.DisplayName) &&
                         !string.IsNullOrWhiteSpace(definition.UsageDescription) &&
                         !string.IsNullOrWhiteSpace(definition.GateDescription) &&
                         definition.Layer == layers[i] && definition.MinimumDepth == minimumDepths[i] &&
                         definition.MaximumDepth == maximumDepths[i] &&
                         definition.MinimumClawTier == minimumClawTiers[i] && definition.GateType == gates[i] &&
                         Mathf.Approximately(definition.ClawTierOneSeconds, tierOne[i]) &&
                         Mathf.Approximately(definition.ClawTierTwoSeconds, tierTwo[i]) &&
                         Mathf.Approximately(definition.ClawTierThreeSeconds, tierThree[i]) &&
                         Mathf.Approximately(definition.FrequencyPerHundredTiles, frequencies[i]) &&
                         Mathf.Approximately(definition.MiningSecondsForClawTier(1),
                             tierOne[i] > 0f ? tierOne[i] : -1f) &&
                         Mathf.Approximately(definition.MiningSecondsForClawTier(2), tierTwo[i]) &&
                         Mathf.Approximately(definition.MiningSecondsForClawTier(3), tierThree[i]) &&
                         !definition.CanMineWithClawTier(0) && !definition.CanMineWithClawTier(4);
            }

            var worldConfig = WorldGenerationConfig.CreateDefault();
            var veinProfilesMatched = 0;
            var profiles = worldConfig.OreVeins;
            for (var i = 0; i < profiles.Length; i++)
            {
                var definition = gameDataCatalog.FindMineralTier(profiles[i].elementType);
                if (definition != null &&
                    Mathf.Approximately(definition.FrequencyPerHundredTiles, profiles[i].frequencyPer100Tiles))
                    veinProfilesMatched++;
            }
            valid &= profiles.Length == 7 && veinProfilesMatched == profiles.Length;
            Destroy(worldConfig);

            if (valid)
                Debug.Log("[Nyangbingo] Official 12 mineral tiers, claw mining times, gates, and vein frequencies completed.");
            else Debug.LogError("[Nyangbingo] Imported mineral tier definition test failed.");
        }

        private void TestImportedSealWhitelist()
        {
            if (gameDataCatalog == null)
            {
                Debug.LogError("[Nyangbingo] Imported seal whitelist catalog reference is missing.");
                return;
            }

            var whitelist = gameDataCatalog.SealWhitelist;
            var sealingRuleCount = 0;
            for (var i = 0; i < whitelist.Count; i++)
                if (whitelist[i] != null && whitelist[i].Seals) sealingRuleCount++;

            var insulWall = gameDataCatalog.FindSealRule("차열벽");
            var naturalTerrain = gameDataCatalog.FindSealRule(SealBoundaryPolicy.NaturalTerrainElement);
            var tunnelAir = gameDataCatalog.FindSealRule(SealBoundaryPolicy.AirElement);
            var wallpaperBackground = gameDataCatalog.FindSealRule("배경벽(벽지)");
            var policy = new SealBoundaryPolicy(whitelist);
            var rulesValid = whitelist.Count == 23 && sealingRuleCount == 7 && policy.IsValid &&
                             insulWall != null && insulWall.Seals && naturalTerrain != null && naturalTerrain.Seals &&
                             tunnelAir != null && !tunnelAir.Seals &&
                             wallpaperBackground != null && !wallpaperBackground.Seals;

            var sealedTiles = CreateSealTestRoom("insul_wall");
            var sealedService = new TileService(sealedTiles, null, gameDataCatalog, 1);
            var sealedSystem = new SealSystem(sealedService, whitelist);
            var artificialRoomSealed = sealedSystem.IsWatchPointSealed(new Vector3Int(1, 1, 0));
            sealedSystem.Dispose();

            var leakingTiles = CreateSealTestRoom("insul_wall");
            leakingTiles[1, 2].elementType = "ice_core";
            var leakingService = new TileService(leakingTiles, null, gameDataCatalog, 1);
            var leakingSystem = new SealSystem(leakingService, whitelist);
            var storageDoesNotSeal = !leakingSystem.IsWatchPointSealed(new Vector3Int(1, 1, 0));
            leakingSystem.Dispose();

            var attachmentTile = new TileData
            {
                hardness = 1,
                isNaturalTerrain = false,
                elementType = "straw_insul"
            };
            var attachmentDoesNotSealAlone = !policy.Seals(attachmentTile);

            if (rulesValid && artificialRoomSealed && storageDoesNotSeal && attachmentDoesNotSealAlone)
                Debug.Log("[Nyangbingo] Official 23 seal whitelist rules, wallpaper background, and artificial boundary policy completed.");
            else Debug.LogError("[Nyangbingo] Imported seal whitelist or boundary policy test failed.");
        }

        private static TileData[,] CreateSealTestRoom(string boundaryElement)
        {
            var tiles = new TileData[3, 3];
            for (var x = 0; x < 3; x++)
            for (var y = 0; y < 3; y++)
                tiles[x, y] = new TileData
                {
                    hardness = 1,
                    isNaturalTerrain = false,
                    elementType = boundaryElement
                };
            tiles[1, 1] = TileData.CreateAir();
            return tiles;
        }

        private void TestImportedIdMigrations()
        {
            if (gameDataCatalog == null)
            {
                Debug.LogError("[Nyangbingo] Imported ID migration catalog reference is missing.");
                return;
            }

            IdMigrationPolicy policy;
            try
            {
                policy = IdMigrationRuntime.LoadOfficial();
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[Nyangbingo] ID migration runtime manifest load failed: {exception.Message}");
                return;
            }

            var definitions = gameDataCatalog.IdMigrations;
            var renameCount = 0;
            var refundCount = 0;
            var itemCount = 0;
            var yokaiCount = 0;
            var bossCount = 0;
            var smeltingCount = 0;
            var valid = definitions.Count == 26 && policy.IsValid && policy.RuleCount == 26;
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition == null) { valid = false; continue; }
                if (definition.Action == IdMigrationAction.Rename) renameCount++;
                else if (definition.Action == IdMigrationAction.RemoveRefund) refundCount++;
                switch (definition.Domain)
                {
                    case IdMigrationDomain.Item: itemCount++; break;
                    case IdMigrationDomain.Yokai: yokaiCount++; break;
                    case IdMigrationDomain.Boss: bossCount++; break;
                    case IdMigrationDomain.Smelting: smeltingCount++; break;
                }

                var runtimeDefinition = policy.Find(definition.Domain, definition.LegacyId);
                valid &= runtimeDefinition == definition &&
                         policy.Migrate(definition.Domain, definition.LegacyId) ==
                         (definition.Action == IdMigrationAction.RemoveRefund ? string.Empty : definition.NewId);
            }

            var refund = policy.Find(IdMigrationDomain.Item, "fox_rain_charm");
            valid &= renameCount == 25 && refundCount == 1 && itemCount == 21 && yokaiCount == 3 &&
                     bossCount == 1 && smeltingCount == 1 && refund != null &&
                     refund.RefundItemId == "yokai_tear" && refund.RefundAmount == 3 &&
                     policy.Migrate(IdMigrationDomain.Yokai, "gangcheori") == "gangcheol" &&
                     policy.Migrate(IdMigrationDomain.Boss, "gangcheori") == "gangcheori" &&
                     policy.Migrate(IdMigrationDomain.Item, "current_id") == "current_id";

            if (valid)
                Debug.Log("[Nyangbingo] Official v34 ID migrations and runtime save manifest completed.");
            else Debug.LogError("[Nyangbingo] Imported ID migration manifest test failed.");
        }

        private void TestImportedDayCurve()
        {
            if (gameDataCatalog == null)
            {
                Debug.LogError("[Nyangbingo] Imported day curve catalog reference is missing.");
                return;
            }

            var curves = gameDataCatalog.DayCurves;
            var valid = curves.Count == 30;
            var previousWallDamage = 0f;
            for (var day = 1; day <= 30; day++)
            {
                var curve = gameDataCatalog.FindDayCurve(day);
                var expectedHeatStage = day <= 10 ? 1 : day <= 20 ? 2 : 3;
                var expectedFireDamage = expectedHeatStage * 1.5f;
                var expectedNightCount = Mathf.Min(8, 1 + (day - 1) / 4);
                var expectedSealPace = Mathf.RoundToInt(day * 100f / 30f);
                valid &= curve != null && curve.Day == day && curve.HeatStage == expectedHeatStage &&
                         Mathf.Approximately(curve.DayFireDamagePerSecond, expectedFireDamage) &&
                         curve.NightYokaiCount == expectedNightCount && curve.PaceMineralTier == expectedHeatStage &&
                         Mathf.Approximately(curve.PaceSealPercent, expectedSealPace) &&
                         curve.EffectiveSpawnCount == Mathf.RoundToInt(curve.NightYokaiCount * curve.SpawnMultiplier) &&
                         curve.MaxActive == curve.EffectiveSpawnCount && curve.YokaiWallDamage >= previousWallDamage &&
                         curve.DropMultiplier >= 0f;
                if (curve != null)
                {
                    previousWallDamage = curve.YokaiWallDamage;
                    var composition = curve.SpawnComposition;
                    for (var index = 0; index < composition.Length; index++)
                        valid &= composition[index].amount > 0;
                }
            }

            var dayOne = gameDataCatalog.FindDayCurve(1);
            var baekjung = gameDataCatalog.FindDayCurve(15);
            var daySeventeen = gameDataCatalog.FindDayCurve(17);
            var thiefRaid = gameDataCatalog.FindDayCurve(18);
            var dayNineteen = gameDataCatalog.FindDayCurve(19);
            var dayTwenty = gameDataCatalog.FindDayCurve(20);
            var ironSiege = gameDataCatalog.FindDayCurve(23);
            var shadowNight = gameDataCatalog.FindDayCurve(27);
            var finalDay = gameDataCatalog.FindDayCurve(30);
            valid &= dayOne != null && dayOne.SpawnAmount(YokaiKind.ClubGoblin) == 1 &&
                     baekjung != null && baekjung.EventId == "baekjung" && baekjung.MaxActive == 12 &&
                     Mathf.Approximately(baekjung.SpawnMultiplier, 3f) &&
                     baekjung.SpawnAmount(YokaiKind.ClubGoblin) == 3 &&
                     baekjung.SpawnAmount(YokaiKind.Bulgasari) == 2 &&
                     baekjung.SpawnAmount(YokaiKind.Yagwanggwi) == 6 &&
                     baekjung.SpawnAmount(YokaiKind.Gaekgwi) == 1 &&
                     daySeventeen != null && daySeventeen.SpawnComposition.Length == 3 &&
                     daySeventeen.SpawnAmount(YokaiKind.ClubGoblin) == 0 &&
                     thiefRaid != null && thiefRaid.SpawnAmount(YokaiKind.Yagwanggwi) == 3 &&
                     thiefRaid.SpawnAmount(YokaiKind.Bulgasari) == 1 &&
                     thiefRaid.SpawnAmount(YokaiKind.Eoduksini) == 1 &&
                     dayNineteen != null && dayNineteen.SpawnComposition.Length == 3 &&
                     dayNineteen.SpawnAmount(YokaiKind.ClubGoblin) == 0 &&
                     dayTwenty != null && dayTwenty.SpawnAmount(YokaiKind.ClubGoblin) == 1 &&
                     dayTwenty.SpawnAmount(YokaiKind.Bulgasari) == 2 &&
                     dayTwenty.SpawnAmount(YokaiKind.Eoduksini) == 2 &&
                     ironSiege != null && ironSiege.SpawnAmount(YokaiKind.Bulgasari) == 3 &&
                     ironSiege.SpawnAmount(YokaiKind.ClubGoblin) == 1 &&
                     ironSiege.SpawnAmount(YokaiKind.Eoduksini) == 2 &&
                     shadowNight != null && shadowNight.SpawnAmount(YokaiKind.Eoduksini) == 3 &&
                     shadowNight.SpawnAmount(YokaiKind.Yagwanggwi) == 2 &&
                     shadowNight.SpawnAmount(YokaiKind.ClubGoblin) == 1 &&
                     shadowNight.SpawnAmount(YokaiKind.Bulgasari) == 1 &&
                     finalDay != null && finalDay.EventId == "imugi_boss" &&
                     finalDay.PaceSealPercent == 100f && finalDay.SpawnAmount(YokaiKind.Imugi) == 1 &&
                     gameDataCatalog.FindDayEvent(baekjung.EventId)?.Day == 15 &&
                     gameDataCatalog.FindBoss(finalDay.EventId) != null;

            var timeObject = new GameObject("TemporaryDayCurveTimeSource");
            var time = timeObject.AddComponent<DayNightService>();
            var dayOneBound = time.ConfigureDayCurve(gameDataCatalog) && time.CurrentDayCurve == dayOne;
            var dayFifteenBound = time.RestoreTimeState(15, 0f, false) && time.CurrentDayCurve == baekjung;
            var dayThirtyBound = time.RestoreTimeState(30, 0f, false) && time.CurrentDayCurve == finalDay;
            valid &= dayOneBound && dayFifteenBound && dayThirtyBound;
            Destroy(timeObject);

            if (valid)
                Debug.Log("[Nyangbingo] Official 30-day curve, spawn plans, milestones, and time binding completed.");
            else Debug.LogError("[Nyangbingo] Imported day curve or time binding test failed.");
        }

        private void TestImportedGlobals()
        {
            if (gameDataCatalog == null)
            {
                Debug.LogError("[Nyangbingo] Imported globals catalog reference is missing.");
                return;
            }

            var definitions = gameDataCatalog.Globals;
            var settings = new GlobalSettings(definitions);
            var numericCount = 0;
            var boolCount = 0;
            for (var i = 0; i < definitions.Count; i++)
            {
                if (definitions[i].TryGetFloat(out _)) numericCount++;
                if (definitions[i].TryGetBool(out _)) boolCount++;
            }

            var valid = definitions.Count == 100 && settings.IsValid && settings.Count == 100 &&
                        numericCount == 81 && boolCount == 2 &&
                        ResidentYokaiRules.TryCreate(definitions, out _) &&
                        settings.TryGetFloat(GlobalKeys.DayLengthSeconds, out var dayLength) &&
                        settings.TryGetFloat(GlobalKeys.NightLengthSeconds, out var nightLength) &&
                        settings.TryGetFloat(GlobalKeys.DayTotalSeconds, out var totalLength) &&
                        Mathf.Approximately(dayLength, 900f) && Mathf.Approximately(nightLength, 540f) &&
                        Mathf.Approximately(dayLength + nightLength, totalLength) &&
                        settings.TryGetInt(GlobalKeys.MvpDays, out var mvpDays) && mvpDays == 30 &&
                        settings.TryGetInt(GlobalKeys.TotalDays, out var totalDays) && totalDays == 100 &&
                        settings.TryGetBool(GlobalKeys.StartAtNight, out var startsAtNight) && startsAtNight &&
                        settings.TryGetInt(GlobalKeys.BaekjungDay, out var baekjungDay) && baekjungDay == 15 &&
                        settings.TryGetInt(GlobalKeys.SealWindowRadiusX, out var radiusX) && radiusX == 28 &&
                        settings.TryGetInt(GlobalKeys.SealWindowRadiusY, out var radiusY) && radiusY == 12 &&
                        settings.TryGetInt(GlobalKeys.SealCap, out var sealCap) &&
                        sealCap == (2 * radiusX + 1) * (2 * radiusY + 1) &&
                        settings.TryGetInt(GlobalKeys.SealTargetCells, out var targetCells) && targetCells == 240 &&
                        settings.GetString(GlobalKeys.BossSavePolicy) == "no_serialize" &&
                        settings.GetString(GlobalKeys.BaekjungWaveOverflow) == "queue_until_dawn" &&
                        settings.TryGetFloat("wallpaper_coverage", out var wallpaperCoverage) &&
                        Mathf.Approximately(wallpaperCoverage, 100f) &&
                        settings.TryGetFloat("wallpaper_coldsource_bonus", out var wallpaperColdsourceBonus) &&
                        Mathf.Approximately(wallpaperColdsourceBonus, 25f) &&
                        settings.GetString("wallpaper_remove_rule") == "restore_original" &&
                        settings.GetString(GlobalKeys.BossFieldYokai) == "freeze_resume" &&
                        settings.TryGetInt(GlobalKeys.CaveMaxHeight, out var caveMaxHeight) && caveMaxHeight == 12 &&
                        settings.GetString(GlobalKeys.FurnitureMvpScope) == "B" &&
                        settings.TryGetInt(GlobalKeys.InventorySlots, out var inventorySlots) && inventorySlots == 50 &&
                        settings.GetString(GlobalKeys.ActiveSlotRule) == "weapon_or_tool_1" &&
                        settings.TryGetInt(GlobalKeys.JangdokStorageSlots, out var jangdokSlots) && jangdokSlots == 40;

            var timeObject = new GameObject("TemporaryOfficialGlobalsTimeSource");
            var time = timeObject.AddComponent<DayNightService>();
            var configured = time.ConfigureOfficialData(gameDataCatalog);
            var officialStart = configured && time.Day == 1 && time.IsNight &&
                                Mathf.Approximately(time.DayDurationSeconds, 900f) &&
                                Mathf.Approximately(time.NightDurationSeconds, 540f) &&
                                Mathf.Approximately(time.CycleLengthSeconds, 1440f) &&
                                Mathf.Approximately(time.TimeOfDayGameSeconds, 900f) &&
                                Mathf.Approximately(time.SecondsUntilNextTransition, 540f) &&
                                time.MvpContentDayLimit == 30 && time.SurvivalDayLimit == 100 &&
                                time.CurrentDayCurve?.Day == 1;
            time.Tick(540f);
            var firstNightLengthCorrect = time.Day == 2 && !time.IsNight &&
                                          Mathf.Approximately(time.TimeOfDayGameSeconds, 0f);
            valid &= officialStart && firstNightLengthCorrect;
            Destroy(timeObject);

            if (valid)
                Debug.Log("[Nyangbingo] Official v27 plus v28/v29 overlay globals, v26 wallpaper contracts, D-100/MVP-30 separation, and 540-second first night completed.");
            else Debug.LogError("[Nyangbingo] Imported globals or runtime binding test failed.");
        }

        private void TestImportedCombatProfiles()
        {
            if (gameDataCatalog == null)
            {
                Debug.LogError("[Nyangbingo] Imported combat profile catalog reference is missing.");
                return;
            }

            var ids = new[]
            {
                "bare_claw", "iron_claw", "icesteel_claw", "dokkaebi_club",
                FanItemIds.Cheolseon, "frostclaw_gauntlet", FanItemIds.Hapjukseon
            };
            var tiers = new[] { "1", "2", "3", "W1", "E1", "W2", "U1" };
            var damages = new[] { 15, 24, 42, 20, 18, 60, 0 };
            var attacksPerSecond = new[] { 1.5f, 1.5f, 1.5f, 1.5f, 1.5f, 1.5f, 0f };
            var dps = new[] { 22.5f, 36f, 63f, 30f, 27f, 90f, 0f };
            var knockbacks = new[] { .5f, 1f, 1.5f, 2f, 1.5f, 1.5f, 2f };
            var ranges = new[] { 1.5f, 1.5f, 2f, 2f, 2f, 2f, 2f };
            var arcs = new[] { 90f, 90f, 120f, 90f, 90f, 120f, 90f };
            var profilesMatch = gameDataCatalog.CombatProfiles.Count == ids.Length;
            for (var i = 0; i < ids.Length; i++)
            {
                var profile = gameDataCatalog.FindCombatProfile(ids[i]);
                profilesMatch &= profile != null && profile.Id == ids[i] && profile.Tier == tiers[i] &&
                                 profile.HasBasicAttack == (i != ids.Length - 1) &&
                                 profile.AttackDamage == damages[i] &&
                                 Mathf.Approximately(profile.AttacksPerSecond, attacksPerSecond[i]) &&
                                 Mathf.Approximately(profile.DamagePerSecond, dps[i]) &&
                                 Mathf.Approximately(profile.KnockbackTiles, knockbacks[i]) &&
                                 Mathf.Approximately(profile.RangeTiles, ranges[i]) &&
                                 Mathf.Approximately(profile.ArcDegrees, arcs[i]) && profile.MultiTarget &&
                                 profile.HitsWalls == (ids[i] != FanItemIds.Cheolseon && ids[i] != FanItemIds.Hapjukseon) &&
                                 gameDataCatalog.FindItem(ids[i]) != null;
            }

            var attacker = new GameObject("TemporaryImportedCombatProfileAttacker");
            var attack = attacker.AddComponent<MeleeArcAttack>();
            var firstTarget = new GameObject("TemporaryImportedCombatProfileTargetA");
            firstTarget.transform.position = Vector3.right;
            firstTarget.AddComponent<BoxCollider2D>();
            var firstHealth = firstTarget.AddComponent<Health>();
            firstHealth.ConfigureForRuntime(100);
            var secondTarget = new GameObject("TemporaryImportedCombatProfileTargetB");
            secondTarget.transform.position = new Vector3(1.5f, .2f, 0f);
            secondTarget.AddComponent<BoxCollider2D>();
            var secondHealth = secondTarget.AddComponent<Health>();
            secondHealth.ConfigureForRuntime(100);
            Physics2D.SyncTransforms();

            var hapjukseonConfigured = attack.ConfigureForRuntime(attacker.transform, Physics2D.AllLayers,
                gameDataCatalog.FindCombatProfile(FanItemIds.Hapjukseon));
            attack.Strike(Vector2.right);
            var noBasicAttack = firstHealth.Current == 100 && secondHealth.Current == 100 && !attack.HitsWalls;
            var cheolseonConfigured = attack.ConfigureForRuntime(attacker.transform, Physics2D.AllLayers,
                gameDataCatalog.FindCombatProfile(FanItemIds.Cheolseon));
            attack.Strike(Vector2.right);
            var cheolseonSwing = firstHealth.Current == 82 && secondHealth.Current == 82 && !attack.HitsWalls;

            firstHealth.RestoreCurrent(100);
            secondHealth.RestoreCurrent(100);
            var emptyMaskFallbackConfigured = attack.ConfigureForRuntime(attacker.transform, default,
                gameDataCatalog.FindCombatProfile("bare_claw"));
            attack.Strike(Vector2.right);
            var emptyMaskFallbackDamaged = firstHealth.Current == 85 && secondHealth.Current == 85 &&
                                           attack.LastHitCount == 2;
            var healthBar = firstTarget.AddComponent<RuntimeWorldHealthBar>();
            healthBar.ConfigureForRuntime(firstHealth, null);
            var healthBarMatches = Mathf.Approximately(healthBar.FillRatio, .85f) &&
                                   firstTarget.transform.Find("HealthBar") != null;
            var bossHealthRatioMatches =
                Mathf.Approximately(MainGameHudController.CalculateHealthRatio(450, 900), .5f) &&
                Mathf.Approximately(MainGameHudController.CalculateHealthRatio(-1, 900), 0f) &&
                Mathf.Approximately(MainGameHudController.CalculateHealthRatio(901, 900), 1f) &&
                Mathf.Approximately(MainGameHudController.CalculateHealthRatio(10, 0), 0f);
            var miningCriticalMatches =
                Mathf.Approximately(MainGamePlayerController.CalculateMiningCriticalChance(.15f, .1f), .25f) &&
                Mathf.Approximately(MainGamePlayerController.CalculateMiningCriticalChance(.15f, .2f), .25f) &&
                Mathf.Approximately(MainGamePlayerController.CalculateMiningCriticalChance(float.NaN, .1f), .1f) &&
                Mathf.Approximately(MainGamePlayerController.CalculateMiningCriticalChance(-1f, 0f), 0f);

            if (profilesMatch && hapjukseonConfigured && noBasicAttack && cheolseonConfigured && cheolseonSwing &&
                emptyMaskFallbackConfigured && emptyMaskFallbackDamaged && healthBarMatches && bossHealthRatioMatches &&
                miningCriticalMatches)
                Debug.Log("[Nyangbingo] Official combat, product mining, 25% critical cap, world HP, and boss HUD HP contracts completed.");
            else
                Debug.LogError("[Nyangbingo] Imported weapon or claw combat profile test failed.");

            Destroy(attacker);
            Destroy(firstTarget);
            Destroy(secondTarget);
        }

        private static void TestSideScrollerMovementContract()
        {
            var horizontalOnly =
                Mathf.Approximately(MainGamePlayerController.CalculateHorizontalVelocity(1f, 3f), 3f) &&
                Mathf.Approximately(MainGamePlayerController.CalculateHorizontalVelocity(-2f, 3f), -3f) &&
                Mathf.Approximately(MainGamePlayerController.CalculateHorizontalVelocity(float.NaN, 3f), 0f);
            var gravityMatches =
                Mathf.Approximately(MainGamePlayerController.ApplyGravity(0f, 20f, 14f, .5f), -10f) &&
                Mathf.Approximately(MainGamePlayerController.ApplyGravity(-10f, 20f, 14f, .5f), -14f) &&
                Mathf.Approximately(MainGamePlayerController.ApplyGravity(float.NaN, 20f, 14f, .5f), -10f);
            var doubleJumpMatches = Mathf.Approximately(
                MainGamePlayerController.CalculateJumpVelocityForHeightRatio(10f, .8f),
                10f * Mathf.Sqrt(.8f));

            if (horizontalOnly && gravityMatches && doubleJumpMatches)
                Debug.Log("[Nyangbingo] Side-scroller horizontal movement, gravity, ground jump, and 80% double-jump contracts completed.");
            else Debug.LogError("[Nyangbingo] Side-scroller movement contract failed.");
        }

        private void TestTimedMiningPresentationContract()
        {
            var mappingMatches =
                MainGamePlayerController.ResolveMiningDefinitionId(WorldTileTypes.StoneMid) ==
                WorldTileTypes.Stone &&
                MainGamePlayerController.ResolveMiningDefinitionId(WorldTileTypes.StoneDeep) ==
                WorldTileTypes.Stone &&
                MainGamePlayerController.ResolveMiningDefinitionId(WorldTileTypes.RuinWall) ==
                WorldTileTypes.Stone &&
                MainGamePlayerController.ResolveMiningDefinitionId(WorldTileTypes.IceLake) ==
                WorldTileTypes.IceShard &&
                MainGamePlayerController.ResolveMiningDefinitionId(WorldTileTypes.IronOre) ==
                WorldTileTypes.IronOre;
            var progressMatches =
                Mathf.Approximately(MainGamePlayerController.CalculateMiningProgress(.25f, 1f), .25f) &&
                Mathf.Approximately(MainGamePlayerController.CalculateMiningProgress(2f, 1f), 1f) &&
                Mathf.Approximately(MainGamePlayerController.CalculateMiningProgress(-1f, 1f), 0f) &&
                Mathf.Approximately(MainGamePlayerController.CalculateMiningProgress(1f, 0f), 0f);
            var officialTimesMatch = gameDataCatalog != null &&
                Mathf.Approximately(gameDataCatalog.FindMineralTier(WorldTileTypes.Dirt)?
                    .MiningSecondsForClawTier(1) ?? -1f, 1f) &&
                Mathf.Approximately(gameDataCatalog.FindMineralTier(WorldTileTypes.IronOre)?
                    .MiningSecondsForClawTier(1) ?? -1f, 3f) &&
                Mathf.Approximately(gameDataCatalog.FindMineralTier(WorldTileTypes.IceSteelOre)?
                    .MiningSecondsForClawTier(1) ?? 0f, -1f) &&
                Mathf.Approximately(gameDataCatalog.FindMineralTier(WorldTileTypes.IceSteelOre)?
                    .MiningSecondsForClawTier(2) ?? -1f, 4f);

            if (mappingMatches && progressMatches && officialTimesMatch)
                Debug.Log("[Nyangbingo] Timed mining uses official claw seconds, hard gates, hold progress, and three-stage crack presentation.");
            else Debug.LogError("[Nyangbingo] Timed mining presentation contract failed.");
        }

        private void TestIceSteelClawAbilitiesContract()
        {
            var slowDefinition = gameDataCatalog?.FindGlobal("claw_t3_slow");
            var slowFraction = 0f;
            var slowConfigured = slowDefinition != null && slowDefinition.TryGetFloat(out slowFraction) &&
                                 Mathf.Approximately(slowFraction, .3f);
            var frostMathMatches =
                Mathf.Approximately(YokaiBrain.CalculateFrostSpeedMultiplier(.3f, 2f), .7f) &&
                Mathf.Approximately(YokaiBrain.CalculateFrostAdjustedActionSeconds(1f, .3f, 2f), .7f) &&
                Mathf.Approximately(YokaiBrain.CalculateFrostAdjustedActionSeconds(1f, .3f, .5f), .85f) &&
                Mathf.Approximately(YokaiBrain.CalculateFrostSpeedMultiplier(.3f, 0f), 1f);
            var primary = new Vector3Int(10, 20, 0);
            var wideMiningMatches =
                MainGamePlayerController.ResolveWideMiningCompanionCell(primary, 21f) == primary + Vector3Int.down &&
                MainGamePlayerController.ResolveWideMiningCompanionCell(primary, 19f) == primary + Vector3Int.up &&
                MainGamePlayerController.ResolveWideMiningCompanionCell(primary, float.NaN) == primary + Vector3Int.up;

            var attacker = new GameObject("TemporaryIceSteelClawAttacker");
            var attack = attacker.AddComponent<MeleeArcAttack>();
            var target = new GameObject("TemporaryIceSteelClawTarget");
            target.transform.position = Vector3.right;
            target.AddComponent<BoxCollider2D>();
            var health = target.AddComponent<Health>();
            health.ConfigureForRuntime(50);
            var brain = target.AddComponent<YokaiBrain>();
            var definition = YokaiDefinition.CreateRuntime(
                YokaiKind.ClubGoblin, 50, 2f, 5, 1f, System.Array.Empty<ItemAmount>());
            brain.ConfigureForRuntime(definition, null);
            var profileConfigured = attack.ConfigureForRuntime(
                attacker.transform, Physics2D.AllLayers, gameDataCatalog?.FindCombatProfile("icesteel_claw"));
            attack.ConfigureFrostSlow(slowConfigured ? slowFraction : 0f, 2f);
            Physics2D.SyncTransforms();
            attack.Strike(Vector2.right);
            var frostApplied = profileConfigured && health.Current == 8 &&
                               Mathf.Approximately(brain.FrostSlowRemaining, 2f) &&
                               Mathf.Approximately(brain.FrostSpeedMultiplier, .7f);
            brain.Tick(1f);
            frostApplied &= Mathf.Approximately(brain.FrostSlowRemaining, 1f);
            var brainRecord = new YokaiStateRecord();
            brain.CaptureSaveState(brainRecord);
            var restoredTarget = new GameObject("TemporaryRestoredIceSteelClawTarget");
            restoredTarget.AddComponent<Health>();
            var restoredBrain = restoredTarget.AddComponent<YokaiBrain>();
            restoredBrain.ConfigureForRuntime(definition, null);
            var brainStateRestored = restoredBrain.RestoreSaveState(brainRecord) &&
                                     Mathf.Approximately(restoredBrain.FrostSlowRemaining, 1f) &&
                                     Mathf.Approximately(restoredBrain.FrostSpeedMultiplier, .7f);

            Destroy(attacker);
            Destroy(target);
            Destroy(restoredTarget);
            Destroy(definition);

            if (slowConfigured && frostMathMatches && wideMiningMatches && frostApplied && brainStateRestored)
                Debug.Log("[Nyangbingo] T3 ice-steel claw applies 30% frost slow for 2 seconds and mines two vertical tiles in one-tile time.");
            else Debug.LogError("[Nyangbingo] T3 ice-steel claw ability contract failed.");
        }

        private void TestGoalBadgeProgressContract()
        {
            var timeObject = new GameObject("TemporaryGoalBadgeTime");
            var time = timeObject.AddComponent<DevBTestTimeSource>();
            time.Day = 1;
            time.IsNight = true;
            var wallCount = 0;
            var visibleDays = 0;
            var globalsMatch = gameDataCatalog?.FindGlobal(GlobalKeys.BadgeWallCount)?.TryGetInt(out wallCount) == true &&
                               gameDataCatalog.FindGlobal(GlobalKeys.BadgeWindowDays)?.TryGetInt(out visibleDays) == true &&
                               wallCount == 1 && visibleDays == 3;
            var completionEvents = 0;
            void CountCompletion() => completionEvents++;
            GameEvents.OnGoalBadgeCompleted += CountCompletion;
            var progress = new GoalBadgeProgress(time, wallCount, visibleDays);
            var valid = false;
            try
            {
                var workbench = gameDataCatalog?.FindRecipe(GoalBadgeProgress.WorkbenchId);
                GameEvents.RaiseCraftingCompleted(workbench);
                var firstBadgeMatches = progress.WorkbenchCrafted && !progress.InsulationWallPlaced &&
                                        !progress.FurnaceBuilt && progress.IsVisible;
                GameEvents.RaisePlacedObjectBuilt(GoalBadgeProgress.InsulationWallId);
                GameEvents.RaisePlacedObjectBuilt(GoalBadgeProgress.FurnaceId);
                var completedMatches = progress.AllCompleted && !progress.IsVisible && completionEvents == 3;
                var captured = progress.Capture();
                var save = new SaveGame { goalBadges = captured };
                var loaded = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(save));
                loaded.NormalizeAfterLoad();

                progress.Dispose();
                time.Day = 2;
                var restored = new GoalBadgeProgress(time, wallCount, visibleDays);
                var restoredMatches = restored.Restore(loaded.goalBadges) && restored.AllCompleted &&
                                      !restored.IsVisible && restored.Capture().dismissed;
                restored.Dispose();

                var expiryTimeObject = new GameObject("TemporaryGoalBadgeExpiryTime");
                var expiryTime = expiryTimeObject.AddComponent<DevBTestTimeSource>();
                expiryTime.Day = visibleDays;
                expiryTime.IsNight = true;
                var expiry = new GoalBadgeProgress(expiryTime, wallCount, visibleDays);
                var visibleThroughThirdNight = expiry.IsVisible;
                expiryTime.Day = visibleDays + 1;
                expiryTime.IsNight = false;
                expiryTime.RaiseDawn();
                var expiresAtFourthDay = !expiry.IsVisible && expiry.Capture().dismissed;
                expiry.Dispose();
                Destroy(expiryTimeObject);

                valid = globalsMatch && workbench != null && firstBadgeMatches && completedMatches && restoredMatches &&
                        visibleThroughThirdNight && expiresAtFourthDay &&
                        loaded.schemaVersion == SaveGame.CurrentSchemaVersion;
            }
            finally
            {
                progress.Dispose();
                GameEvents.OnGoalBadgeCompleted -= CountCompletion;
                Destroy(timeObject);
            }

            if (valid)
                Debug.Log("[Nyangbingo] First-three-day workbench, insulation-wall, and furnace goal badges complete, persist, and dismiss by the official rule.");
            else Debug.LogError("[Nyangbingo] Goal badge progress or save contract failed.");
        }

        private static void TestProductCraftingStationDefinitionContract()
        {
            var valid = MainGameBossSummonUiController.DefinitionIdForStation(CraftingStation.Workbench) ==
                        "workbench" &&
                        MainGameBossSummonUiController.DefinitionIdForStation(CraftingStation.Furnace) ==
                        "furnace" &&
                        MainGameBossSummonUiController.DefinitionIdForStation(CraftingStation.IceAnvil) ==
                        "ice_anvil" &&
                        MainGameBossSummonUiController.DefinitionIdForStation(CraftingStation.Foundry) ==
                        "blast_furnace" &&
                        string.IsNullOrEmpty(MainGameBossSummonUiController.DefinitionIdForStation(
                            CraftingStation.None)) &&
                        MainGameBossSummonUiController.StationForDefinitionId("workbench") ==
                        CraftingStation.Workbench &&
                        MainGameBossSummonUiController.StationForDefinitionId("furnace") ==
                        CraftingStation.Furnace &&
                        MainGameBossSummonUiController.StationForDefinitionId("ice_anvil") ==
                        CraftingStation.IceAnvil &&
                        MainGameBossSummonUiController.StationForDefinitionId("blast_furnace") ==
                        CraftingStation.Foundry &&
                        MainGameBossSummonUiController.StationForDefinitionId("unknown") ==
                        CraftingStation.None &&
                        !MainGameCraftingUiController.IsSmeltingStation(CraftingStation.Workbench) &&
                        !MainGameCraftingUiController.IsSmeltingStation(CraftingStation.IceAnvil) &&
                        MainGameCraftingUiController.IsSmeltingStation(CraftingStation.Furnace) &&
                        MainGameCraftingUiController.IsSmeltingStation(CraftingStation.Foundry) &&
                        MainGameCraftingUiController.UnifiedTabCount == 4 &&
                        MainGameCraftingUiController.UnifiedTabLabel(0) == "채집" &&
                        MainGameCraftingUiController.UnifiedTabLabel(1) == "제작" &&
                        MainGameCraftingUiController.UnifiedTabLabel(2) == "장비" &&
                        MainGameCraftingUiController.UnifiedTabLabel(3) == "도감" &&
                        MainGameCraftingUiController.InventoryGridColumns == 10 &&
                        MainGameCraftingUiController.InventoryGridRows == 5 &&
                        Mathf.Approximately(MainGameCraftingUiController.InventorySlotPixelSize, 27f) &&
                        MainGameCraftingUiController.UsesIconOnlyCraftingList &&
                        MainGameBossSummonUiController.DebugShortcutHelpKey == KeyCode.F5 &&
                        MainGameBossSummonUiController.DebugShortcutHelpPanelSize.x <= 480f &&
                        MainGameBossSummonUiController.DebugShortcutHelpPanelSize.y <= 270f &&
                        !MainGameHudController.ProductHudNarrativeTextEnabled &&
                        !MainGameTurretRuntime.ProductHudNarrativeTextEnabled &&
                        !MainGameTilePaletteController.ProductHudNarrativeTextEnabled &&
                        string.IsNullOrEmpty(MainGameCraftingUiController.UnifiedTabLabel(4)) &&
                        MainGameCraftingUiController.IsRecipeVisibleAtStation(
                            CraftingStation.None, CraftingStation.None) &&
                        MainGameCraftingUiController.IsRecipeVisibleAtStation(
                            CraftingStation.None, CraftingStation.Furnace) &&
                        MainGameCraftingUiController.IsRecipeVisibleAtStation(
                            CraftingStation.Workbench, CraftingStation.Workbench) &&
                        !MainGameCraftingUiController.IsRecipeVisibleAtStation(
                            CraftingStation.Workbench, CraftingStation.None) &&
                        !MainGameCraftingUiController.IsRecipeVisibleAtStation(
                            CraftingStation.IceAnvil, CraftingStation.Furnace);

            if (valid)
                Debug.Log("[Nyangbingo] v29 unified tabs and nearby-station recipe filtering match the official contract; placed furnaces route E to crafting-mode smelting.");
            else Debug.LogError("[Nyangbingo] Product crafting station definition contract failed.");
        }

        private void TestGameDataCatalogInvalidEntryRejection()
        {
            var first = ItemDefinition.CreateRuntime("catalog_duplicate", "Catalog Duplicate A");
            var second = ItemDefinition.CreateRuntime("catalog_duplicate", "Catalog Duplicate B");
            var duplicateCatalog = GameDataCatalog.CreateRuntime(first, second);
            var duplicateRejected = !duplicateCatalog.IsValid && duplicateCatalog.FindItem(first.Id) == null;

            var blank = ItemDefinition.CreateRuntime(" ", "Catalog Blank");
            var blankCatalog = GameDataCatalog.CreateRuntime(blank);
            var blankRejected = !blankCatalog.IsValid && blankCatalog.FindItem(" ") == null;

            var nullCatalog = GameDataCatalog.CreateRuntime(first, null);
            var nullRejected = !nullCatalog.IsValid && nullCatalog.FindItem(first.Id) == null;

            if (duplicateRejected && blankRejected && nullRejected)
                Debug.Log("[Nyangbingo] Game data catalog duplicate, blank-ID, and null-entry rejection completed.");
            else Debug.LogError("[Nyangbingo] Game data catalog invalid-entry rejection test failed.");
        }

        private static bool ValidateCatalogEntries<T>(
            System.Collections.Generic.IReadOnlyList<T> entries,
            System.Func<T, string> getId,
            System.Func<string, T> find)
            where T : UnityEngine.Object
        {
            if (entries == null || entries.Count == 0) return false;

            var ids = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null) return false;
                var id = getId(entry);
                if (string.IsNullOrWhiteSpace(id) || !ids.Add(id) || find(id) != entry) return false;
            }

            return true;
        }

        private void TestImportedBossDefinitions()
        {
            if (gameDataCatalog == null)
            {
                Debug.LogError("[Nyangbingo] Imported boss catalog reference is missing.");
                return;
            }

            var goblinChief = gameDataCatalog.FindBoss("king_dokkaebi");
            var motherBulgasari = gameDataCatalog.FindBoss("mother_bulgasari");
            var imugi = gameDataCatalog.FindBoss("imugi_boss");

            var valid = MatchesBossDefinition(goblinChief, BossKind.GoblinChief, 13800, 300f,
                            "ssireum_satba", CraftingStation.Workbench, false, 0,
                            20f, 20f, 20f, 12, 0.75f, BossSpecialShape.Box, 2f, 0f, 12, 0f, 0f, 4f, 8f,
                            false, true, ItemMvpScope.A,
                            new[] { "club_shard:1", "hemp_stalk:10", "wood:5" },
                            "yokai_tear:3", "dokkaebi_fire_essence:1", "club_shard:2") &&
                        MatchesBossDefinition(motherBulgasari, BossKind.MotherBulgasari, 6500, 180.6f,
                            "iron_bait_pile", CraftingStation.Furnace, false, 0,
                            24f, 48f, 0f, 14, 1f, BossSpecialShape.Cone, 4f, 60f, 10, 2f, 1f, 0f, 6f,
                            true, true, ItemMvpScope.B,
                            new[] { "iron_ingot:10", "iron_scale:3", "coal:5" },
                            "yokai_tear:4", "iron_forge_core:1", "iron_scale:4") &&
                        MatchesBossDefinition(imugi, BossKind.Imugi, 16000, 219f,
                            "ice_altar_offering", CraftingStation.IceAnvil, true, 30,
                            40f, 40f, 40f, 18, 1.5f, BossSpecialShape.Box, 3f, 0f, 18, 0f, 0f, 3f, 12f,
                            false, true, ItemMvpScope.A,
                            new[] { "icesteel_ingot:2", "frost_essence:2", "ice_shard:10", "yokai_tear:30" },
                            "yokai_tear:8", "yeouiju:1");

            if (valid)
                Debug.Log("[Nyangbingo] Imported boss extended combat, summon materials, and drops.csv rewards completed.");
            else
                Debug.LogError("[Nyangbingo] Imported boss definition test failed.");
        }

        private void TestBossCombatRuntime()
        {
            var definition = gameDataCatalog != null ? gameDataCatalog.FindBoss("king_dokkaebi") : null;
            var bossObject = new GameObject("TemporaryBossCombatRuntime");
            var targetObject = new GameObject("TemporaryBossCombatTarget");
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
                var combat = bossObject.AddComponent<BossCombatController>();

                bossObject.transform.position = Vector3.zero;
                targetBody.position = Vector2.right * 6f;
                targetObject.transform.position = targetBody.position;
                var configured = combat.ConfigureForRuntime(definition, target);
                combat.Tick(1f);
                var approached = Mathf.Approximately(bossObject.transform.position.x, 1.25f) &&
                                 targetHealth.Current == 100;

                var expandedContactPosition = (Vector2)bossObject.transform.position + Vector2.right * 2f;
                targetBody.position = expandedContactPosition;
                targetObject.transform.position = expandedContactPosition;
                combat.Tick(.01f);
                var expandedContactHit = definition != null &&
                                         targetHealth.Current == 100 - definition.ContactDamage;

                var nearPosition = (Vector2)bossObject.transform.position + Vector2.right;
                targetBody.position = nearPosition;
                targetObject.transform.position = nearPosition;
                combat.Tick(definition != null ? definition.SpecialCooldownSeconds : 0f);
                var telegraphed = combat.IsTelegraphing;
                combat.Tick(definition != null ? definition.TelegraphSeconds : 0f);
                var expectedKnockbackX = nearPosition.x - (definition != null ? definition.SpecialKnockbackTiles : 0f);
                var specialHit = definition != null &&
                                 targetHealth.Current ==
                                 100 - definition.ContactDamage - definition.SpecialDamagePerHit &&
                                 !combat.IsTelegraphing && !combat.IsSpecialActive &&
                                 combat.SpecialCooldownRemaining > 0f &&
                                 Mathf.Abs(targetBody.position.x - expectedKnockbackX) <= .001f;

                if (configured && approached && expandedContactHit && telegraphed && specialHit)
                    Debug.Log("[Nyangbingo] Boss combat approach, scaled contact range, exact telegraph, CSV special damage, knockback, and cooldown completed.");
                else Debug.LogError($"[Nyangbingo] Boss combat runtime test failed: configured={configured}, " +
                                    $"approached={approached}, expandedContactHit={expandedContactHit}, " +
                                    $"telegraphed={telegraphed}, specialHit={specialHit}, " +
                                    $"hp={targetHealth.Current}, bodyX={targetBody.position.x:0.###}, " +
                                    $"expectedX={expectedKnockbackX:0.###}.");
            }
            finally
            {
                Destroy(bossObject);
                Destroy(targetObject);
            }
        }

        private void TestMotherBulgasariAirborneSpecialRuntime()
        {
            var definition = gameDataCatalog != null
                ? gameDataCatalog.FindBoss("mother_bulgasari")
                : null;
            var bossObject = new GameObject("TemporaryMotherBulgasariCombatRuntime");
            var targetObject = new GameObject("TemporaryMotherBulgasariCombatTarget");
            try
            {
                var bossHealth = bossObject.AddComponent<Health>();
                bossHealth.ConfigureForRuntime(definition != null ? definition.HitPoints : 1);
                var targetBody = targetObject.AddComponent<Rigidbody2D>();
                targetBody.bodyType = RigidbodyType2D.Kinematic;
                targetBody.gravityScale = 0f;
                var targetCollider = targetObject.AddComponent<BoxCollider2D>();
                targetCollider.size = Vector2.one;
                var targetHealth = targetObject.AddComponent<Health>();
                targetHealth.ConfigureForRuntime(100);
                var target = targetObject.AddComponent<MainGameRaidTarget>();
                var combat = bossObject.AddComponent<BossCombatController>();

                bossObject.transform.position = Vector3.zero;
                targetBody.position = Vector2.right * 2f;
                targetObject.transform.position = targetBody.position;
                var configured = combat.ConfigureForRuntime(definition, target);
                combat.Tick(.01f);
                var expandedContactHit = definition != null &&
                                         targetHealth.Current == 100 - definition.ContactDamage;

                targetBody.position = Vector2.right * 4.25f;
                targetObject.transform.position = targetBody.position;
                combat.Tick(definition != null ? definition.SpecialCooldownSeconds : 0f);
                var recognizedAtSpecialRange = combat.IsTelegraphing;
                combat.Tick(definition != null ? definition.TelegraphSeconds : 0f);
                combat.Tick(definition != null ? definition.SpecialTickSeconds : 0f);

                var airborneHit = definition != null &&
                                  targetHealth.Current ==
                                  100 - definition.ContactDamage - definition.SpecialDamagePerHit &&
                                  Mathf.Approximately(targetBody.position.x, 4.25f) &&
                                  Mathf.Approximately(targetBody.position.y,
                                      definition.SpecialKnockbackTiles);
                if (configured && expandedContactHit && recognizedAtSpecialRange && airborneHit)
                    Debug.Log("[Nyangbingo] Mother Bulgasari scaled contact range, special recognition range, " +
                              "and airborne knockback completed.");
                else
                    Debug.LogError($"[Nyangbingo] Mother Bulgasari airborne test failed: " +
                                   $"configured={configured}, expandedContactHit={expandedContactHit}, " +
                                   $"recognizedAtSpecialRange={recognizedAtSpecialRange}, hp={targetHealth.Current}, " +
                                   $"position={targetBody.position}.");
            }
            finally
            {
                Destroy(bossObject);
                Destroy(targetObject);
            }
        }

        private void TestBossStartValidation()
        {
            var summonItem = ItemDefinition.CreateRuntime("boss_start_validation_item", "Boss Start Validation Item");
            var definition = BossDefinition.CreateRuntime("boss_start_validation", BossKind.GoblinChief,
                summonItem, System.Array.Empty<ItemAmount>(), 5);
            var timeSource = gameObject.AddComponent<DevBTestTimeSource>();
            var spawnController = gameObject.AddComponent<DevBTestSpawnController>();
            var manager = gameObject.AddComponent<BossManager>();
            manager.ConfigureForRuntime(timeSource, spawnController);
            var startCount = 0;
            manager.BossStarted += _ => startCount++;

            var deadBossObject = new GameObject("TemporaryDeadBossStartValidation");
            var deadBossHealth = deadBossObject.AddComponent<Health>();
            deadBossHealth.ConfigureForRuntime(1);
            deadBossHealth.ApplyDamage(1, DamageTag.Melee);
            var deadRejected = !manager.TryStart(definition, deadBossHealth);

            var liveBossObject = new GameObject("TemporaryLiveBossStartValidation");
            var liveBossHealth = liveBossObject.AddComponent<Health>();
            liveBossHealth.ConfigureForRuntime(5);
            var invalidTimesRejected = !manager.TryStart(definition, liveBossHealth, -1f) &&
                                       !manager.TryStart(definition, liveBossHealth, float.NaN) &&
                                       !manager.TryStart(definition, liveBossHealth, float.PositiveInfinity);
            var rejectedStateUnchanged = !manager.IsBossActive && spawnController.IsRegularSpawning &&
                                         !liveBossHealth.IsKnockbackImmune && startCount == 0;
            var validStarted = manager.TryStart(definition, liveBossHealth, 12f) && manager.IsBossActive &&
                               !spawnController.IsRegularSpawning && liveBossHealth.IsKnockbackImmune && startCount == 1;
            liveBossHealth.ApplyDamage(5, DamageTag.Melee);
            var validEnded = !manager.IsBossActive && spawnController.IsRegularSpawning;

            if (deadRejected && invalidTimesRejected && rejectedStateUnchanged && validStarted && validEnded)
                Debug.Log("[Nyangbingo] Boss dead-health and invalid summon-time rejection completed.");
            else Debug.LogError("[Nyangbingo] Boss start validation test failed.");

            Destroy(deadBossObject);
            Destroy(liveBossObject);
            Destroy(timeSource);
            Destroy(spawnController);
            Destroy(manager);
        }

        private void TestBossSummonPaymentTransaction()
        {
            var summonItem = ItemDefinition.CreateRuntime("boss_payment_item", "Boss Payment Item");
            var definition = BossDefinition.CreateRuntime("boss_payment_validation", BossKind.GoblinChief,
                summonItem, System.Array.Empty<ItemAmount>(), 5);
            var inventory = new Nyangbingo.Inventory.Inventory(id => id == summonItem.Id ? summonItem : null);
            inventory.TryAdd(summonItem.Id, 1);
            var timeSource = gameObject.AddComponent<DevBTestTimeSource>();
            var spawnController = gameObject.AddComponent<DevBTestSpawnController>();
            var manager = gameObject.AddComponent<BossManager>();
            manager.ConfigureForRuntime(timeSource, spawnController);
            var service = new BossSummonService(inventory, manager);

            var deadBossObject = new GameObject("TemporaryBossPaymentDeadBoss");
            var deadBossHealth = deadBossObject.AddComponent<Health>();
            deadBossHealth.ConfigureForRuntime(1);
            deadBossHealth.ApplyDamage(1, DamageTag.Melee);
            var failedStartRestoredPayment = !service.TryConsumeAndStart(definition, deadBossHealth) &&
                                             inventory.Count(summonItem.Id) == 1 && !manager.IsBossActive;

            var liveBossObject = new GameObject("TemporaryBossPaymentLiveBoss");
            var liveBossHealth = liveBossObject.AddComponent<Health>();
            liveBossHealth.ConfigureForRuntime(5);
            var paymentCountAtStartEvent = -1;
            manager.BossStarted += _ => paymentCountAtStartEvent = inventory.Count(summonItem.Id);
            var paidStartSucceeded = service.TryConsumeAndStart(definition, liveBossHealth) &&
                                     inventory.Count(summonItem.Id) == 0 && paymentCountAtStartEvent == 0 &&
                                     manager.IsBossActive;
            liveBossHealth.ApplyDamage(5, DamageTag.Melee);
            var endedNormally = !manager.IsBossActive && spawnController.IsRegularSpawning;

            if (failedStartRestoredPayment && paidStartSucceeded && endedNormally)
                Debug.Log("[Nyangbingo] Boss summon payment reservation and rollback completed.");
            else Debug.LogError("[Nyangbingo] Boss summon payment transaction test failed.");

            Destroy(deadBossObject);
            Destroy(liveBossObject);
            Destroy(timeSource);
            Destroy(spawnController);
            Destroy(manager);
        }

        private static bool MatchesBossDefinition(BossDefinition definition, BossKind kind, int hitPoints,
            float combatSeconds, string summonItemId, CraftingStation summonStation, bool requiresDeepAltar,
            int forcedDay, float wallDamageDefault, float wallDamageIce, float wallDamageIronWall,
            int contactDamage, float telegraphSeconds, BossSpecialShape specialShape, float specialRange,
            float specialArc, int specialDamage, float specialDuration, float specialTick, float specialKnockback,
            float specialCooldown, bool fireTag, bool aimLock, ItemMvpScope mvpScope,
            string[] expectedMaterials, params string[] expectedDrops)
        {
            if (definition == null || definition.Kind != kind || definition.HitPoints != hitPoints ||
                !Mathf.Approximately(definition.ExpectedCombatSeconds, combatSeconds) ||
                definition.SummonItem == null || definition.SummonItem.Id != summonItemId ||
                definition.SummonStation != summonStation || string.IsNullOrWhiteSpace(definition.DisplayName) ||
                string.IsNullOrWhiteSpace(definition.RecommendedDay) ||
                definition.RequiresDeepAltar != requiresDeepAltar || definition.ForcedDay != forcedDay ||
                !Mathf.Approximately(definition.WallDamageDefault, wallDamageDefault) ||
                !Mathf.Approximately(definition.WallDamageIce, wallDamageIce) ||
                !Mathf.Approximately(definition.WallDamageIronWall, wallDamageIronWall) ||
                definition.ContactDamage != contactDamage || string.IsNullOrWhiteSpace(definition.SpecialDescription) ||
                !Mathf.Approximately(definition.TelegraphSeconds, telegraphSeconds) ||
                definition.SpecialShape != specialShape ||
                !Mathf.Approximately(definition.SpecialRangeTiles, specialRange) ||
                !Mathf.Approximately(definition.SpecialArcDegrees, specialArc) ||
                definition.SpecialDamagePerHit != specialDamage ||
                !Mathf.Approximately(definition.SpecialDurationSeconds, specialDuration) ||
                !Mathf.Approximately(definition.SpecialTickSeconds, specialTick) ||
                !Mathf.Approximately(definition.SpecialKnockbackTiles, specialKnockback) ||
                !Mathf.Approximately(definition.SpecialCooldownSeconds, specialCooldown) ||
                definition.SpecialHasFireTag != fireTag || definition.SpecialAimLocks != aimLock ||
                definition.MvpScope != mvpScope ||
                !MatchesItemAmounts(definition.SummonMaterials, expectedMaterials) ||
                definition.GuaranteedDrops == null || definition.GuaranteedDrops.Length != expectedDrops.Length)
                return false;

            return MatchesItemAmounts(definition.GuaranteedDrops, expectedDrops);
        }

        private static bool MatchesItemAmounts(ItemAmount[] actual, string[] expected)
        {
            if (actual == null || expected == null || actual.Length != expected.Length) return false;
            var unmatched = new System.Collections.Generic.HashSet<string>(expected,
                System.StringComparer.Ordinal);
            if (unmatched.Count != expected.Length) return false;
            for (var i = 0; i < actual.Length; i++)
            {
                var entry = actual[i];
                if (entry.item == null || entry.amount <= 0 ||
                    !unmatched.Remove($"{entry.item.Id}:{entry.amount}"))
                    return false;
            }
            return unmatched.Count == 0;
        }

        private void TestBossSummonAndForcedEncounterRules()
        {
            var goblinChief = gameDataCatalog != null ? gameDataCatalog.FindBoss("king_dokkaebi") : null;
            var imugi = gameDataCatalog != null ? gameDataCatalog.FindBoss("imugi_boss") : null;
            if (goblinChief == null || imugi == null)
            {
                Debug.LogError("[Nyangbingo] Boss summon rule definitions are missing.");
                return;
            }

            var inventory = new Nyangbingo.Inventory.Inventory(gameDataCatalog.FindItem);
            inventory.TryAdd(goblinChief.SummonItem.Id, 1);
            inventory.TryAdd(imugi.SummonItem.Id, 1);
            var timeSource = gameObject.AddComponent<DevBTestTimeSource>();
            var regularSpawner = gameObject.AddComponent<DevBTestSpawnController>();
            var bossManager = gameObject.AddComponent<BossManager>();
            bossManager.ConfigureForRuntime(timeSource, regularSpawner);
            var summonSite = new DevBTestBossSummonSite();
            var summonService = new BossSummonService(inventory, bossManager, summonSite);

            var daytimeBossObject = new GameObject("TemporaryDaytimeSummonBoss");
            var daytimeHealth = daytimeBossObject.AddComponent<Health>();
            daytimeHealth.ConfigureForRuntime(goblinChief.HitPoints);
            timeSource.IsNight = false;
            var daytimeRejected = !summonService.TryConsumeAndStart(goblinChief, daytimeHealth) &&
                                  inventory.Count(goblinChief.SummonItem.Id) == 1 && !bossManager.IsBossActive;

            var imugiObject = new GameObject("TemporaryImugiSummonBoss");
            var imugiHealth = imugiObject.AddComponent<Health>();
            imugiHealth.ConfigureForRuntime(imugi.HitPoints);
            timeSource.IsNight = true;
            summonSite.IsDeepAltar = false;
            var altarRejected = !summonService.TryConsumeAndStart(imugi, imugiHealth) &&
                                inventory.Count(imugi.SummonItem.Id) == 1 && !bossManager.IsBossActive;
            summonSite.IsDeepAltar = true;
            var altarAccepted = summonService.TryConsumeAndStart(imugi, imugiHealth) &&
                                inventory.Count(imugi.SummonItem.Id) == 0 && bossManager.ActiveDefinition == imugi &&
                                !regularSpawner.IsRegularSpawning;
            imugiHealth.ApplyDamage(imugi.HitPoints, DamageTag.Melee);
            var imugiEnded = !bossManager.IsBossActive && regularSpawner.IsRegularSpawning;

            var forcedSpawner = new DevBTestForcedBossSpawnController();
            ForcedBossEncounterBinding forcedBinding = null;
            var forcedRuleValid = false;
            try
            {
                timeSource.Day = 29;
                timeSource.IsNight = true;
                forcedBinding = new ForcedBossEncounterBinding(imugi, timeSource, bossManager, forcedSpawner);
                GameEvents.RaiseNightStart();
                var earlyRejected = forcedSpawner.SpawnCount == 0 && !forcedBinding.HasTriggered;

                timeSource.Day = 30;
                timeSource.IsNight = false;
                GameEvents.RaiseNightStart();
                var daytimeForcedRejected = forcedSpawner.SpawnCount == 0 && !forcedBinding.HasTriggered;

                timeSource.IsNight = true;
                GameEvents.RaiseNightStart();
                var forcedStarted = forcedSpawner.SpawnCount == 1 && forcedBinding.HasTriggered &&
                                    bossManager.ActiveDefinition == imugi && !regularSpawner.IsRegularSpawning;
                GameEvents.RaiseNightStart();
                var duplicateRejected = forcedSpawner.SpawnCount == 1;
                forcedSpawner.LastSpawnedHealth.ApplyDamage(imugi.HitPoints, DamageTag.Melee);
                var forcedEnded = !bossManager.IsBossActive && regularSpawner.IsRegularSpawning;

                forcedBinding.Dispose();
                forcedBinding.RestoreTriggered(false);
                GameEvents.RaiseNightStart();
                var unsubscribed = forcedSpawner.SpawnCount == 1;
                forcedRuleValid = earlyRejected && daytimeForcedRejected && forcedStarted && duplicateRejected &&
                                  forcedEnded && unsubscribed;
            }
            finally
            {
                forcedBinding?.Dispose();
            }

            if (daytimeRejected && altarRejected && altarAccepted && imugiEnded && forcedRuleValid)
                Debug.Log("[Nyangbingo] Boss night summon, Imugi deep altar, and day-30 forced encounter completed.");
            else
                Debug.LogError("[Nyangbingo] Boss summon or forced encounter rule test failed.");

            Destroy(daytimeBossObject);
            Destroy(imugiObject);
            if (forcedSpawner.LastSpawnedHealth != null) Destroy(forcedSpawner.LastSpawnedHealth.gameObject);
            Destroy(bossManager);
            Destroy(regularSpawner);
            Destroy(timeSource);
        }

        private void TestForcedBossEncounterSaveRoundTrip()
        {
            var imugi = gameDataCatalog != null ? gameDataCatalog.FindBoss("imugi_boss") : null;
            if (imugi == null)
            {
                Debug.LogError("[Nyangbingo] Forced boss save definition is missing.");
                return;
            }

            ForcedBossEncounterBinding sourceBinding = null;
            ForcedBossEncounterBinding restoredBinding = null;
            ForcedBossEncounterBinding legacyBinding = null;
            ForcedBossEncounterBinding emptyLegacyBinding = null;
            try
            {
                sourceBinding = new ForcedBossEncounterBinding(imugi, null, null, null, true);
                var save = new SaveGame();
                ForcedBossEncounterSaveAdapter.Capture(save, imugi, sourceBinding);
                var loaded = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(save));
                if (loaded != null) loaded.NormalizeAfterLoad();

                restoredBinding = new ForcedBossEncounterBinding(imugi, null, null, null);
                var restored = ForcedBossEncounterSaveAdapter.Restore(loaded, imugi, restoredBinding);

                var legacy = JsonUtility.FromJson<SaveGame>(
                    "{\"schemaVersion\":1,\"bossRecords\":[{\"bossId\":\"imugi_boss\",\"count\":1,\"firstDay\":30}],\"forcedBossEncounters\":null}");
                if (legacy != null) legacy.NormalizeAfterLoad();
                legacyBinding = new ForcedBossEncounterBinding(imugi, null, null, null);
                var legacyRestored = ForcedBossEncounterSaveAdapter.Restore(
                    legacy, imugi, legacyBinding);

                var emptyLegacy = JsonUtility.FromJson<SaveGame>(
                    "{\"schemaVersion\":1,\"bossRecords\":[],\"forcedBossEncounters\":null}");
                if (emptyLegacy != null) emptyLegacy.NormalizeAfterLoad();
                emptyLegacyBinding = new ForcedBossEncounterBinding(imugi, null, null, null);
                var emptyLegacyRestored = ForcedBossEncounterSaveAdapter.Restore(
                    emptyLegacy, imugi, emptyLegacyBinding);

                if (restored && loaded != null && loaded.schemaVersion == SaveGame.CurrentSchemaVersion &&
                    loaded.forcedBossEncounters.Count == 1 && restoredBinding.HasTriggered &&
                    legacyRestored && legacy != null && legacy.schemaVersion == SaveGame.CurrentSchemaVersion &&
                    legacyBinding.HasTriggered && emptyLegacyRestored && emptyLegacy != null &&
                    emptyLegacy.schemaVersion == SaveGame.CurrentSchemaVersion && !emptyLegacyBinding.HasTriggered)
                    Debug.Log("[Nyangbingo] Forced boss encounter structured save and v1 migration completed.");
                else
                    Debug.LogError("[Nyangbingo] Forced boss encounter save round-trip test failed.");
            }
            finally
            {
                sourceBinding?.Dispose();
                restoredBinding?.Dispose();
                legacyBinding?.Dispose();
                emptyLegacyBinding?.Dispose();
            }
        }

        private void TestImugiPhaseSpecialRuntime()
        {
            var definition = gameDataCatalog != null
                ? gameDataCatalog.FindBoss("imugi_boss")
                : null;
            var bossObject = new GameObject("TemporaryImugiPhaseCombatRuntime");
            var targetObject = new GameObject("TemporaryImugiPhaseCombatTarget");
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
                var combat = bossObject.AddComponent<BossCombatController>();

                bossObject.transform.position = Vector3.zero;
                targetBody.position = new Vector2(1.4f, 0f);
                targetObject.transform.position = targetBody.position;
                var configured = combat.ConfigureForRuntime(definition, target);

                if (definition != null)
                    bossHealth.ApplyDamage(
                        Mathf.FloorToInt(definition.HitPoints * .34f) + 1,
                        DamageTag.Melee);
                combat.Tick(.01f);
                var landingTelegraphed = combat.IsTelegraphing;
                combat.Tick(definition != null ? definition.TelegraphSeconds : 0f);
                var landingHit = targetHealth.Current == 92 &&
                                 Mathf.Approximately(targetBody.position.x, 3.4f);

                if (definition != null)
                {
                    var belowLakePhase = Mathf.CeilToInt(definition.HitPoints * .33f) - 1;
                    bossHealth.ApplyDamage(
                        Mathf.Max(0, bossHealth.Current - belowLakePhase),
                        DamageTag.Melee);
                }
                targetBody.position = new Vector2(40f, 0f);
                targetObject.transform.position = targetBody.position;
                combat.Tick(.01f);
                var lakeTelegraphed = combat.IsTelegraphing;
                combat.Tick(definition != null ? definition.TelegraphSeconds : 0f);
                combat.Tick(.5f);
                var lakePulsedTwice = targetHealth.Current == 76 &&
                                     Mathf.Approximately(targetBody.position.x, 40f) &&
                                     !combat.IsSpecialActive;

                if (configured && landingTelegraphed && landingHit &&
                    lakeTelegraphed && lakePulsedTwice)
                    Debug.Log("[Nyangbingo] Imugi 66% landing discharge and 33% whole-lake two-pulse phases completed.");
                else
                    Debug.LogError($"[Nyangbingo] Imugi phase special test failed: " +
                                   $"configured={configured}, landingTelegraphed={landingTelegraphed}, " +
                                   $"landingHit={landingHit}, lakeTelegraphed={lakeTelegraphed}, " +
                                   $"lakePulsedTwice={lakePulsedTwice}, hp={targetHealth.Current}, " +
                                   $"position={targetBody.position}.");
            }
            finally
            {
                Destroy(bossObject);
                Destroy(targetObject);
            }
        }

        private void TestForcedBossEncounterDuplicateRestoreRejection()
        {
            var summonItem = ItemDefinition.CreateRuntime("forced_duplicate_item", "Forced Duplicate Item");
            var definition = BossDefinition.CreateRuntime("forced_duplicate_boss", BossKind.Gangcheori,
                summonItem, System.Array.Empty<ItemAmount>(), 1, 0f, false, 30);
            var duplicateForced = new SaveGame
            {
                forcedBossEncounters = new System.Collections.Generic.List<ForcedBossEncounterRecord>
                {
                    new ForcedBossEncounterRecord { bossId = definition.Id, triggered = false },
                    new ForcedBossEncounterRecord { bossId = definition.Id, triggered = true }
                }
            };
            var binding = new ForcedBossEncounterBinding(definition, null, null, null, true);
            var duplicateForcedRejected = !ForcedBossEncounterSaveAdapter.Restore(
                duplicateForced, definition, binding) && binding.HasTriggered;

            var duplicateLegacy = new SaveGame
            {
                bossRecords = new System.Collections.Generic.List<BossRecord>
                {
                    new BossRecord { bossId = definition.Id, count = 1, firstDay = 30 },
                    new BossRecord { bossId = definition.Id, count = 2, firstDay = 30 }
                }
            };
            var duplicateLegacyRejected = !ForcedBossEncounterSaveAdapter.Restore(
                duplicateLegacy, definition, binding) && binding.HasTriggered;
            binding.Dispose();

            if (duplicateForcedRejected && duplicateLegacyRejected)
                Debug.Log("[Nyangbingo] Forced boss duplicate encounter-record rejection completed.");
            else Debug.LogError("[Nyangbingo] Forced boss duplicate encounter-record test failed.");
        }

        private void TestImportedBossGuaranteedRewardFlow()
        {
            var bossDefinition = gameDataCatalog != null ? gameDataCatalog.FindBoss("king_dokkaebi") : null;
            var tear = gameDataCatalog != null ? gameDataCatalog.FindItem("yokai_tear") : null;
            var signature = gameDataCatalog != null ? gameDataCatalog.FindItem("dokkaebi_fire_essence") : null;
            var extra = gameDataCatalog != null ? gameDataCatalog.FindItem("club_shard") : null;
            if (bossDefinition == null || bossDefinition.GuaranteedDrops == null ||
                bossDefinition.GuaranteedDrops.Length != 3 || tear == null || signature == null || extra == null)
            {
                Debug.LogError("[Nyangbingo] Imported boss reward definition is missing.");
                return;
            }

            var inventory = new Nyangbingo.Inventory.Inventory(gameDataCatalog.FindItem);
            var timeSource = gameObject.AddComponent<DevBTestTimeSource>();
            timeSource.IsNight = true;
            var regularSpawner = gameObject.AddComponent<DevBTestSpawnController>();
            var bossManager = gameObject.AddComponent<BossManager>();
            bossManager.ConfigureForRuntime(timeSource, regularSpawner);
            var receiver = gameObject.AddComponent<BossRewardReceiver>();
            receiver.ConfigureForRuntime(bossManager);

            var receiverEventCount = 0;
            receiver.RewardGranted += (_, __) => receiverEventCount++;

            var acquisitionCount = 0;
            System.Action<ItemDefinition, int> onAcquisition = (item, amount) =>
            {
                acquisitionCount++;
                inventory.TryAdd(item.Id, amount);
            };
            ItemAcquisition.Requested += onAcquisition;

            var defeatedBossObject = new GameObject("TemporaryRewardBoss");
            var fledBossObject = new GameObject("TemporaryFledRewardBoss");
            try
            {
                var defeatedHealth = defeatedBossObject.AddComponent<Health>();
                defeatedHealth.ConfigureForRuntime(bossDefinition.HitPoints);
                var defeatedStarted = bossManager.TryStart(bossDefinition, defeatedHealth);
                defeatedHealth.ApplyDamage(bossDefinition.HitPoints, DamageTag.Melee);
                var defeatRewarded = defeatedStarted && receiverEventCount == 3 && acquisitionCount == 3 &&
                                     inventory.Count(tear.Id) == 3 && inventory.Count(signature.Id) == 1 &&
                                     inventory.Count(extra.Id) == 2 &&
                                     !bossManager.IsBossActive && regularSpawner.IsRegularSpawning;

                var fledHealth = fledBossObject.AddComponent<Health>();
                fledHealth.ConfigureForRuntime(bossDefinition.HitPoints);
                var fledStarted = bossManager.TryStart(bossDefinition, fledHealth);
                timeSource.RaiseDawn();
                var fleeNotRewarded = fledStarted && receiverEventCount == 3 && acquisitionCount == 3 &&
                                      inventory.Count(tear.Id) == 3 && inventory.Count(signature.Id) == 1 &&
                                      inventory.Count(extra.Id) == 2 &&
                                      !bossManager.IsBossActive && regularSpawner.IsRegularSpawning;

                if (defeatRewarded && fleeNotRewarded)
                    Debug.Log("[Nyangbingo] drops.csv boss reward bundle and dawn-flee exclusion completed.");
                else
                    Debug.LogError("[Nyangbingo] Imported boss guaranteed reward flow test failed.");
            }
            finally
            {
                ItemAcquisition.Requested -= onAcquisition;
                receiver.ConfigureForRuntime(null);
                Destroy(defeatedBossObject);
                Destroy(fledBossObject);
                Destroy(receiver);
                Destroy(bossManager);
                Destroy(regularSpawner);
                Destroy(timeSource);
            }
        }

        private void TestBossRewardFullInventoryRetention()
        {
            var blocker = ItemDefinition.CreateRuntime("boss_reward_blocker", "Boss Reward Blocker", 1);
            var reward = ItemDefinition.CreateRuntime("boss_reward_pending", "Boss Reward Pending", 1);
            var inventory = new Nyangbingo.Inventory.Inventory(id =>
                id == blocker.Id ? blocker : id == reward.Id ? reward : null);
            inventory.TryAdd(blocker.Id, Nyangbingo.Inventory.Inventory.SlotCount);
            var inventoryObject = new GameObject("TemporaryBossRewardInventoryRuntime");
            var inventoryRuntime = inventoryObject.AddComponent<InventoryRuntime>();
            inventoryRuntime.ConfigureForRuntime(inventory);

            var definition = BossDefinition.CreateRuntime("boss_reward_pending_validation", BossKind.GoblinChief,
                blocker, new[] { new ItemAmount { item = reward, amount = 1 } }, 1);
            var timeSource = gameObject.AddComponent<DevBTestTimeSource>();
            var spawnController = gameObject.AddComponent<DevBTestSpawnController>();
            var manager = gameObject.AddComponent<BossManager>();
            manager.ConfigureForRuntime(timeSource, spawnController);
            var receiver = gameObject.AddComponent<BossRewardReceiver>();
            receiver.ConfigureForRuntime(manager);
            var bossObject = new GameObject("TemporaryBossRewardPendingBoss");
            var bossHealth = bossObject.AddComponent<Health>();
            bossHealth.ConfigureForRuntime(1);

            var started = manager.TryStart(definition, bossHealth);
            bossHealth.ApplyDamage(1, DamageTag.Melee);
            var retainedWhenFull = started && inventory.Count(reward.Id) == 0 &&
                                   inventoryRuntime.Pending.Count == 1 &&
                                   inventoryRuntime.Pending[0].item == reward &&
                                   inventoryRuntime.Pending[0].amount == 1;
            var spaceFreed = inventory.TryRemove(blocker.Id, 1);
            var collected = inventoryRuntime.TryCollectPending(0);

            if (retainedWhenFull && spaceFreed && collected && inventoryRuntime.Pending.Count == 0 &&
                inventory.Count(reward.Id) == 1)
                Debug.Log("[Nyangbingo] Boss reward full-inventory retention and collection completed.");
            else Debug.LogError("[Nyangbingo] Boss reward full-inventory retention test failed.");

            receiver.ConfigureForRuntime(null);
            inventoryRuntime.enabled = false;
            Destroy(bossObject);
            Destroy(receiver);
            Destroy(manager);
            Destroy(spawnController);
            Destroy(timeSource);
            Destroy(inventoryObject);
        }

        private void TestPendingItemAcquisitionSaveRoundTrip()
        {
            var blocker = ItemDefinition.CreateRuntime("pending_save_blocker", "Pending Save Blocker", 1);
            var reward = ItemDefinition.CreateRuntime("pending_save_reward", "Pending Save Reward", 1);
            ItemDefinition FindItem(string id) => id == blocker.Id ? blocker : id == reward.Id ? reward : null;
            var inventory = new Nyangbingo.Inventory.Inventory(FindItem);
            inventory.TryAdd(blocker.Id, Nyangbingo.Inventory.Inventory.SlotCount);

            var sourceObject = new GameObject("TemporaryPendingSaveSource");
            var source = sourceObject.AddComponent<InventoryRuntime>();
            source.ConfigureForRuntime(inventory);
            source.Receive(reward, 1);
            var save = new SaveGame();
            var captured = PendingItemAcquisitionSaveAdapter.Capture(save, source);
            var loaded = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(save));

            var restoredObject = new GameObject("TemporaryPendingSaveRestored");
            var restored = restoredObject.AddComponent<InventoryRuntime>();
            restored.ConfigureForRuntime(inventory);
            var restoredState = PendingItemAcquisitionSaveAdapter.Restore(loaded, restored, FindItem);
            var roundTripMatches = restored.Pending.Count == 1 && restored.Pending[0].item == reward &&
                                   restored.Pending[0].amount == 1;

            var corrupt = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(save));
            corrupt.pendingItemAcquisitions[0] = new PendingItemRecord { itemId = "missing_item", amount = 1 };
            var corruptRejectedWithoutMutation = !PendingItemAcquisitionSaveAdapter.Restore(corrupt, restored, FindItem) &&
                                                 restored.Pending.Count == 1 && restored.Pending[0].item == reward;
            var legacy = JsonUtility.FromJson<SaveGame>(
                "{\"schemaVersion\":5,\"pendingItemAcquisitions\":null}");
            var legacyObject = new GameObject("TemporaryPendingSaveLegacy");
            var legacyRuntime = legacyObject.AddComponent<InventoryRuntime>();
            legacyRuntime.ConfigureForRuntime(inventory);
            var legacyRestored = PendingItemAcquisitionSaveAdapter.Restore(legacy, legacyRuntime, FindItem) &&
                                 legacy.schemaVersion == SaveGame.CurrentSchemaVersion && legacyRuntime.Pending.Count == 0;

            inventory.TryRemove(blocker.Id, 1);
            var collectedAfterRestore = restored.TryCollectPending(0) && inventory.Count(reward.Id) == 1;

            if (captured && loaded != null && loaded.schemaVersion == SaveGame.CurrentSchemaVersion &&
                restoredState && roundTripMatches && corruptRejectedWithoutMutation && legacyRestored &&
                collectedAfterRestore)
                Debug.Log("[Nyangbingo] Pending item acquisition structured save round-trip completed.");
            else Debug.LogError("[Nyangbingo] Pending item acquisition save round-trip test failed.");

            source.enabled = false;
            restored.enabled = false;
            legacyRuntime.enabled = false;
            Destroy(sourceObject);
            Destroy(restoredObject);
            Destroy(legacyObject);
        }

        private void TestBossRecordSaveFlow()
        {
            var bossDefinition = gameDataCatalog != null ? gameDataCatalog.FindBoss("king_dokkaebi") : null;
            if (bossDefinition == null)
            {
                Debug.LogError("[Nyangbingo] Boss record definition is missing.");
                return;
            }

            var save = new SaveGame();
            var timeSource = gameObject.AddComponent<DevBTestTimeSource>();
            timeSource.Day = 8;
            timeSource.IsNight = true;
            var regularSpawner = gameObject.AddComponent<DevBTestSpawnController>();
            var bossManager = gameObject.AddComponent<BossManager>();
            bossManager.ConfigureForRuntime(timeSource, regularSpawner);
            var binding = new BossRecordBinding(save, timeSource, bossManager, gameDataCatalog.FindBoss);
            var firstBossObject = new GameObject("TemporaryFirstRecordedBoss");
            var secondBossObject = new GameObject("TemporarySecondRecordedBoss");
            var fledBossObject = new GameObject("TemporaryUnrecordedFledBoss");
            var disposedBossObject = new GameObject("TemporaryDisposedRecordBoss");

            try
            {
                var firstHealth = firstBossObject.AddComponent<Health>();
                firstHealth.ConfigureForRuntime(bossDefinition.HitPoints);
                var firstStarted = bossManager.TryStart(bossDefinition, firstHealth);
                firstHealth.ApplyDamage(bossDefinition.HitPoints, DamageTag.Melee);

                timeSource.Day = 10;
                var secondHealth = secondBossObject.AddComponent<Health>();
                secondHealth.ConfigureForRuntime(bossDefinition.HitPoints);
                var secondStarted = bossManager.TryStart(bossDefinition, secondHealth);
                secondHealth.ApplyDamage(bossDefinition.HitPoints, DamageTag.Melee);

                var fledHealth = fledBossObject.AddComponent<Health>();
                fledHealth.ConfigureForRuntime(bossDefinition.HitPoints);
                var fledStarted = bossManager.TryStart(bossDefinition, fledHealth);
                timeSource.RaiseDawn();

                var loaded = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(save));
                if (loaded != null) loaded.NormalizeAfterLoad();
                var recorded = loaded != null && loaded.bossRecords.Count == 1 &&
                               loaded.bossRecords[0].bossId == bossDefinition.Id &&
                               loaded.bossRecords[0].count == 2 && loaded.bossRecords[0].firstDay == 8;

                binding.Dispose();
                timeSource.Day = 11;
                timeSource.IsNight = true;
                var disposedHealth = disposedBossObject.AddComponent<Health>();
                disposedHealth.ConfigureForRuntime(bossDefinition.HitPoints);
                var disposedStarted = bossManager.TryStart(bossDefinition, disposedHealth);
                disposedHealth.ApplyDamage(bossDefinition.HitPoints, DamageTag.Melee);
                var unsubscribed = save.bossRecords.Count == 1 && save.bossRecords[0].count == 2;

                if (firstStarted && secondStarted && fledStarted && disposedStarted && recorded && unsubscribed)
                    Debug.Log("[Nyangbingo] Boss defeat count, first-day record, dawn exclusion, and save round-trip completed.");
                else
                    Debug.LogError("[Nyangbingo] Boss record save flow test failed.");
            }
            finally
            {
                binding.Dispose();
                Destroy(firstBossObject);
                Destroy(secondBossObject);
                Destroy(fledBossObject);
                Destroy(disposedBossObject);
                Destroy(bossManager);
                Destroy(regularSpawner);
                Destroy(timeSource);
            }
        }

        private void TestBossRecordSaveValidation()
        {
            var summonItem = ItemDefinition.CreateRuntime("boss_record_validation_item", "Boss Record Validation Item");
            var alpha = BossDefinition.CreateRuntime("boss_record_alpha", BossKind.GoblinChief, summonItem,
                System.Array.Empty<ItemAmount>(), 1);
            var beta = BossDefinition.CreateRuntime("boss_record_beta", BossKind.MotherBulgasari, summonItem,
                System.Array.Empty<ItemAmount>(), 1);
            BossDefinition FindBoss(string id) => id == alpha.Id ? alpha : id == beta.Id ? beta : null;
            var save = new SaveGame
            {
                bossRecords = new System.Collections.Generic.List<BossRecord>
                {
                    new BossRecord { bossId = beta.Id, count = 1, firstDay = 2 },
                    new BossRecord { bossId = alpha.Id, count = int.MaxValue, firstDay = 1 }
                }
            };
            var validAndSorted = BossRecordSaveAdapter.Validate(save, FindBoss) &&
                                 save.bossRecords[0].bossId == alpha.Id && save.bossRecords[1].bossId == beta.Id;

            var timeSource = gameObject.AddComponent<DevBTestTimeSource>();
            var spawnController = gameObject.AddComponent<DevBTestSpawnController>();
            var manager = gameObject.AddComponent<BossManager>();
            manager.ConfigureForRuntime(timeSource, spawnController);
            var binding = new BossRecordBinding(save, timeSource, manager, FindBoss);
            var bossObject = new GameObject("TemporaryBossRecordOverflowBoss");
            var health = bossObject.AddComponent<Health>();
            health.ConfigureForRuntime(1);
            var started = manager.TryStart(alpha, health);
            health.ApplyDamage(1, DamageTag.Melee);
            binding.Dispose();
            var overflowPrevented = save.bossRecords[0].count == int.MaxValue;

            var duplicate = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(save));
            duplicate.bossRecords.Add(new BossRecord { bossId = alpha.Id, count = 1, firstDay = 1 });
            var duplicateRejected = !BossRecordSaveAdapter.Validate(duplicate, FindBoss);
            var invalidCount = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(save));
            invalidCount.bossRecords[0] = new BossRecord { bossId = alpha.Id, count = -1, firstDay = 1 };
            var invalidCountRejected = !BossRecordSaveAdapter.Validate(invalidCount, FindBoss);
            var unknown = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(save));
            unknown.bossRecords[0] = new BossRecord { bossId = "missing_boss", count = 1, firstDay = 1 };
            var unknownRejected = !BossRecordSaveAdapter.Validate(unknown, FindBoss);

            if (validAndSorted && started && overflowPrevented && duplicateRejected &&
                invalidCountRejected && unknownRejected)
                Debug.Log("[Nyangbingo] Boss record validation, sorting, and overflow guard completed.");
            else Debug.LogError("[Nyangbingo] Boss record validation test failed.");

            Destroy(bossObject);
            Destroy(manager);
            Destroy(spawnController);
            Destroy(timeSource);
        }

        private void TestYokaiCodexKillSaveBinding()
        {
            var clubGoblin = YokaiDefinition.CreateRuntime(YokaiKind.ClubGoblin, 10, 1f, 1, 1f,
                System.Array.Empty<ItemAmount>());
            var bulgasari = YokaiDefinition.CreateRuntime(YokaiKind.Bulgasari, 10, 1f, 1, 1f,
                System.Array.Empty<ItemAmount>());
            YokaiDefinition FindYokai(string id) =>
                id == clubGoblin.Id ? clubGoblin : id == bulgasari.Id ? bulgasari : null;
            var save = new SaveGame
            {
                dogam = new System.Collections.Generic.List<CodexRecord>
                {
                    new CodexRecord { yokaiId = clubGoblin.Id, kills = 2 }
                }
            };

            var binding = new YokaiCodexBinding(save, FindYokai);
            GameEvents.RaiseYokaiKilled(clubGoblin);
            GameEvents.RaiseYokaiKilled(clubGoblin);
            GameEvents.RaiseYokaiKilled(bulgasari);
            binding.Dispose();
            GameEvents.RaiseYokaiKilled(clubGoblin);

            var loaded = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(save));
            var loadedValid = YokaiCodexSaveAdapter.Validate(loaded, FindYokai);
            var countsMatch = loaded != null && loaded.dogam.Count == 2 &&
                              loaded.dogam[0].yokaiId == bulgasari.Id && loaded.dogam[0].kills == 1 &&
                              loaded.dogam[1].yokaiId == clubGoblin.Id && loaded.dogam[1].kills == 4;

            var corrupt = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(save));
            corrupt.dogam.Add(new CodexRecord { yokaiId = clubGoblin.Id, kills = 1 });
            var duplicateRejected = !YokaiCodexSaveAdapter.Validate(corrupt, FindYokai);
            var legacy = JsonUtility.FromJson<SaveGame>("{\"schemaVersion\":5,\"dogam\":null}");
            var legacyRestored = YokaiCodexSaveAdapter.Validate(legacy, FindYokai) &&
                                 legacy.schemaVersion == SaveGame.CurrentSchemaVersion && legacy.dogam.Count == 0;

            if (loadedValid && countsMatch && duplicateRejected && legacyRestored)
                Debug.Log("[Nyangbingo] Yokai codex kill event and structured save binding completed.");
            else Debug.LogError("[Nyangbingo] Yokai codex save binding test failed.");
        }

        private void TestImportedYokaiCodexPresentation()
        {
            if (gameDataCatalog == null)
            {
                Debug.LogError("[Nyangbingo] Imported yokai codex catalog reference is missing.");
                return;
            }

            var save = new SaveGame
            {
                dogam = new System.Collections.Generic.List<CodexRecord>
                {
                    new CodexRecord { yokaiId = "club", kills = 2 },
                    new CodexRecord { yokaiId = "gangcheol", kills = 1 }
                },
                bossRecords = new System.Collections.Generic.List<BossRecord>
                {
                    new BossRecord { bossId = "king_dokkaebi", count = 3, firstDay = 9 },
                    new BossRecord { bossId = "imugi_boss", count = 1, firstDay = 30 }
                }
            };

            YokaiCodexPresentationModel model;
            try { model = new YokaiCodexPresentationModel(gameDataCatalog, save); }
            catch (System.Exception exception)
            {
                Debug.LogError($"[Nyangbingo] Yokai codex presentation construction failed: {exception.Message}");
                return;
            }

            YokaiCodexCard FindCard(string id)
            {
                for (var i = 0; i < model.Cards.Count; i++)
                    if (model.Cards[i].EntryId == id) return model.Cards[i];
                return null;
            }

            var club = FindCard("club");
            var yagwanggwi = FindCard("yakwang");
            var gangcheol = FindCard("gangcheol");
            var imugi = FindCard("imugi");
            var gaekgwi = FindCard("gaekgwi");
            var chief = FindCard("king_dokkaebi");
            var imugiDuplicateRemoved = FindCard("imugi_boss") == null;
            var layoutMatches = model.Cards.Count == YokaiCodexPresentationModel.ExpectedCardCount &&
                                YokaiCodexPresentationModel.GridColumns == 3 &&
                                YokaiCodexPresentationModel.GridCardSize == new Vector2(72f, 96f) &&
                                YokaiCodexPresentationModel.EnlargedCardSize == new Vector2(192f, 256f);
            var recordsMatch = club != null && club.IsUnlocked && club.KillCount == 2 &&
                               chief != null && chief.IsBoss && chief.KillCount == 3 && chief.FirstKillDay == 9 &&
                               gangcheol != null && gangcheol.KillCount == 1 &&
                               imugi != null && imugi.KillCount == 1 && imugi.FirstKillDay == 30 &&
                               gaekgwi != null;
            var lockedHidden = yagwanggwi != null && !yagwanggwi.IsUnlocked && yagwanggwi.UsesInkSilhouette &&
                               yagwanggwi.DisplayName == "?" && yagwanggwi.SourceText == string.Empty;
            var lockedEnlarged = model.TryTapCard("yakwang") && model.HasEnlargedCard &&
                                 !model.TryTapCard("yakwang") && !model.IsBackVisible;
            model.TapOutside();
            var unlockedFlipped = model.TryTapCard("club") && model.TryTapCard("club") && model.IsBackVisible &&
                                  model.SelectedCard.SourceText.Contains("씨름담");
            model.TapOutside();
            var outsideReturnedToGrid = !model.HasEnlargedCard && !model.IsBackVisible;

            save.dogam.Add(new CodexRecord { yokaiId = "yakwang", kills = 1 });
            model.Refresh();
            yagwanggwi = FindCard("yakwang");
            var refreshUnlocked = yagwanggwi != null && yagwanggwi.IsUnlocked &&
                                  yagwanggwi.DisplayName == "야광귀" &&
                                  yagwanggwi.SourceText.Contains("동국세시기");

            if (layoutMatches && recordsMatch && lockedHidden && lockedEnlarged && unlockedFlipped &&
                outsideReturnedToGrid && refreshUnlocked && imugiDuplicateRemoved)
                Debug.Log("[Nyangbingo] Yokai codex v34 nine-card unlock, merge, enlarge, flip, and source presentation completed.");
            else Debug.LogError("[Nyangbingo] Yokai codex presentation test failed.");
        }

        private void TestAudioEventRoutingAndRuntimePool()
        {
            var cues = new System.Collections.Generic.List<AudioCue>();
            var music = new System.Collections.Generic.List<MusicTrack>();
            var percussion = new System.Collections.Generic.List<bool>();
            var router = new AudioEventRouter();
            router.CueRequested += cues.Add;
            router.MusicRequested += music.Add;
            router.BaekjungPercussionRequested += percussion.Add;

            var yokai = YokaiDefinition.CreateRuntime(YokaiKind.ClubGoblin, 1, 1f, 1, 1f,
                System.Array.Empty<ItemAmount>());
            var boss = BossDefinition.CreateRuntime("audio_test_boss", BossKind.GoblinChief, null,
                System.Array.Empty<ItemAmount>());

            GameEvents.RaiseDayStart();
            GameEvents.RaiseNightStart();
            GameEvents.RaiseBaekjungStart();
            GameEvents.RaiseMiningImpact(MiningImpactSurface.Dirt);
            GameEvents.RaiseMiningImpact(MiningImpactSurface.Mineral);
            GameEvents.RaiseTileBroken(Vector3Int.zero);
            GameEvents.RaiseItemAcquired();
            GameEvents.RaisePlayerDamaged();
            GameEvents.RaiseYokaiDamaged();
            GameEvents.RaiseYokaiKilled(yokai);
            GameEvents.RaiseMiningCritical();
            GameEvents.RaiseCraftingCompleted();
            GameEvents.RaiseChestOpened();
            GameEvents.RaiseWallDamaged();
            GameEvents.RaiseBossSummoned(boss);
            GameEvents.RaiseBossFled();
            GameEvents.RaiseEoduksiniBloomed();
            GameEvents.RaisePlayerHeatPanting();
            GameEvents.RaiseGoalBadgeCompleted();
            GameEvents.RaiseBaekjungEnd();

            var uniqueCues = new System.Collections.Generic.HashSet<AudioCue>(cues);
            var allCueContractsCovered = uniqueCues.Count == AudioEventRouter.P1CueCount + AudioEventRouter.P2CueCount &&
                                         cues.Count == uniqueCues.Count + 1;
            var musicFlowMatches = music.Count == 4 && music[0] == MusicTrack.Day &&
                                   music[1] == MusicTrack.Night && music[2] == MusicTrack.Boss &&
                                   music[3] == MusicTrack.Night;
            var percussionFlowMatches = percussion.Count == 5 && !percussion[0] && percussion[1] &&
                                        !percussion[2] && percussion[3] && !percussion[4];

            var cueCountBeforeDispose = cues.Count;
            router.Dispose();
            GameEvents.RaiseMiningCritical();
            var disposedCleanly = cues.Count == cueCountBeforeDispose;

            var listenerObject = FindAnyObjectByType<AudioListener>() == null
                ? new GameObject("TemporaryDevBTestAudioListener", typeof(AudioListener))
                : null;
            var audioObject = new GameObject("TemporaryNyangbingoAudioService");
            var service = audioObject.AddComponent<NyangbingoAudioService>();
            var poolMatches = audioObject.GetComponentsInChildren<AudioSource>(true).Length ==
                              NyangbingoAudioService.SfxChannelCount + 3;
            var volumesMatch = service.TrySetBusVolumes(.5f, .25f) &&
                               Mathf.Approximately(service.BgmVolume, .5f) &&
                               Mathf.Approximately(service.SfxVolume, .25f) &&
                               !service.TrySetBusVolumes(float.NaN, 1f) &&
                               Mathf.Approximately(NyangbingoAudioService.NormalizedToDecibels(0f), -80f) &&
                               Mathf.Approximately(NyangbingoAudioService.NormalizedToDecibels(1f), 0f) &&
                               Mathf.Approximately(NyangbingoAudioService.CrossfadeSeconds, 2f);
            DestroyImmediate(audioObject);
            if (listenerObject != null) Destroy(listenerObject);

            if (allCueContractsCovered && musicFlowMatches && percussionFlowMatches && disposedCleanly &&
                poolMatches && volumesMatch)
                Debug.Log("[Nyangbingo] Audio P1/P2 routing, BGM variation, two-bus settings, and eight-channel pool completed.");
            else Debug.LogError("[Nyangbingo] Audio event routing or runtime pool test failed.");
        }

        private void TestGameShellTitlePauseSettingsAndResult()
        {
            var originalTimeScale = Time.timeScale;
            var originalFullscreen = Screen.fullScreen;
            var shellObject = new GameObject("TemporaryGameShell");
            var saveManager = shellObject.AddComponent<SaveManager>();
            var audioService = shellObject.AddComponent<NyangbingoAudioService>();
            var timeSource = shellObject.AddComponent<DevBTestTimeSource>();
            var shell = shellObject.AddComponent<GameShellController>();
            var activeSave = new SaveGame
            {
                day = 30,
                sealPct = 87.5f,
                modulesDone = new System.Collections.Generic.List<string>
                {
                    "insulated_wall", "insulated_door", "insulated_roof", "jar_storage", "ice_storage"
                },
                dogam = new System.Collections.Generic.List<CodexRecord>
                {
                    new CodexRecord { yokaiId = "club", kills = 2 },
                    new CodexRecord { yokaiId = "yakwang", kills = 3 }
                },
                bossRecords = new System.Collections.Generic.List<BossRecord>
                {
                    new BossRecord { bossId = "imugi_boss", count = 1, firstDay = 30 }
                },
                stats = new RunStatsRecord { minedTiles = 17, deaths = 2 }
            };
            shell.ConfigureForRuntime(saveManager, audioService, timeSource, activeSave, false, true);

            var trackedSave = new SaveGame { stats = new RunStatsRecord { minedTiles = int.MaxValue - 1, deaths = 0 } };
            var statsBinding = new RunStatsBinding(trackedSave);
            GameEvents.RaiseTileBroken(Vector3Int.zero);
            GameEvents.RaiseTileBroken(Vector3Int.one);
            GameEvents.RaisePlayerDied();
            statsBinding.Dispose();
            GameEvents.RaisePlayerDied();
            var statsTracked = trackedSave.stats.minedTiles == int.MaxValue && trackedSave.stats.deaths == 1;
            var statsRoundTrip = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(trackedSave));
            statsRoundTrip.NormalizeAfterLoad();
            var statsSaved = statsRoundTrip.schemaVersion == SaveGame.CurrentSchemaVersion &&
                             statsRoundTrip.stats.minedTiles == int.MaxValue && statsRoundTrip.stats.deaths == 1;

            saveManager.Save(GameShellController.AutoSaveSlot, new SaveGame { day = 15 });
            shell.RefreshTitle();
            var titleMatches = shell.Title.CanContinue && shell.Title.LatestSlot >= 0 &&
                               shell.Title.DaysUntilBaegilHeat >= 70 && shell.Title.DaysUntilBaegilHeat <= 100 &&
                               GameShellController.FormatTitleCountdown(shell.Title.DaysUntilBaegilHeat) == "D-86" &&
                               shell.Title.ShowsDemoSaves && shell.Title.ShowsQuit && shell.CanShowFullscreenToggle;

            var newGameSlot = -1;
            shell.NewGameRequested += slot => newGameSlot = slot;
            shell.RequestNewGame();
            var newGameRequested = shell.Screen == GameShellScreen.Title &&
                                   newGameSlot == GameShellController.AutoSaveSlot;
            shell.EnterGameplay(new SaveGame { day = 1 });
            var newGameStarted = newGameRequested && shell.Screen == GameShellScreen.Gameplay;

            audioService.EnsureAudiblePlayback(MusicTrack.Boss);
            var bossTrackStarted = audioService.CurrentTrack == MusicTrack.Boss;
            var paused = shell.OpenPause() && shell.Screen == GameShellScreen.Pause &&
                         Mathf.Approximately(Time.timeScale, 0f);
            var settingsOpened = shell.OpenSettings() && shell.Screen == GameShellScreen.Settings;
            var settingsPreviewed = audioService.TryPreviewBusVolumes(.7f, .5f) &&
                                    audioService.CurrentTrack == MusicTrack.Boss;
            var settingsApplied = shell.TryApplySettings(.6f, .4f, originalFullscreen) &&
                                  Mathf.Approximately(audioService.BgmVolume, .6f) &&
                                  Mathf.Approximately(audioService.SfxVolume, .4f) &&
                                  audioService.CurrentTrack == MusicTrack.Boss;
            var settingsClosed = shell.CloseSettings() && shell.Screen == GameShellScreen.Pause;
            var returnWarning = shell.RequestReturnToTitle() && shell.Screen == GameShellScreen.Confirmation &&
                                shell.PendingConfirmation == GameShellConfirmation.ReturnToTitle;
            var cancelToPause = shell.CancelConfirmation() && shell.Screen == GameShellScreen.Pause &&
                                Mathf.Approximately(Time.timeScale, 0f);
            var resumed = shell.ResumeGameplay() && shell.Screen == GameShellScreen.Gameplay &&
                          Time.timeScale > 0f && audioService.CurrentTrack == MusicTrack.Boss;

            shell.ConfigureForRuntime(saveManager, audioService, timeSource, activeSave, false, true);
            var gameplayStarted = shell.TryContinue();
            shell.ConfigureForRuntime(saveManager, audioService, timeSource, activeSave, false, true);
            shell.RequestNewGame();
            shell.ConfigureForRuntime(saveManager, audioService, timeSource, activeSave, false, true);
            shell.TryContinue();
            shell.ConfigureForRuntime(saveManager, audioService, timeSource, activeSave, false, true);
            var endingPolicy = GameShellController.ShouldEndDemoAtDawn(31, 30) &&
                               !GameShellController.ShouldEndDemoAtDawn(30, 30) &&
                               !GameShellController.ShouldEndDemoAtDawn(32, 30) &&
                               !GameShellController.ShouldEndDemoAtDawn(31, 100);
            shell.ShowResult(activeSave);
            var result = shell.Result;
            var resultMatches = shell.Screen == GameShellScreen.Result && result != null &&
                                Mathf.Approximately(result.SealPercentage, 87.5f) &&
                                result.CompletedModuleIds.Count == 5 && result.ImugiDefeated &&
                                result.YokaiKills == 5 && result.MinedTiles == 17 && result.Deaths == 2 &&
                                DemoResultState.Teaser == "D-70 — 백일폭염까지" &&
                                Mathf.Approximately(Time.timeScale, 0f);
            var resultSingleExit = shell.ReturnFromResultToTitle() && shell.Screen == GameShellScreen.Title &&
                                   Mathf.Approximately(Time.timeScale, 0f);
            var demoGuard = !shell.RequestDemoSave(14) && shell.RequestDemoSave(15) &&
                            shell.PendingConfirmation == GameShellConfirmation.LoadDemoSave &&
                            shell.CancelConfirmation() && shell.Screen == GameShellScreen.Title;
            var quitRequested = false;
            shell.QuitRequested += () => quitRequested = true;
            var desktopQuit = shell.RequestQuit() && quitRequested;

            Time.timeScale = originalTimeScale;
            Screen.fullScreen = originalFullscreen;
            DestroyImmediate(shellObject);

            if (statsTracked && statsSaved && titleMatches && newGameStarted && bossTrackStarted &&
                paused && settingsOpened && settingsPreviewed && settingsApplied && settingsClosed &&
                returnWarning && cancelToPause && resumed && gameplayStarted && endingPolicy && resultMatches &&
                resultSingleExit && demoGuard && desktopQuit)
                Debug.Log("[Nyangbingo] Game shell title, pause, settings, confirmation, and D30 result flow completed.");
            else Debug.LogError("[Nyangbingo] Game shell flow test failed.");
        }

        private void TestImportedAccessoryStatsAndTheftProtection()
        {
            if (gameDataCatalog == null)
            {
                Debug.LogError("[Nyangbingo] Imported accessory catalog reference is missing.");
                return;
            }

            var bellCharm = gameDataCatalog.FindEquipment("bell_norigae");
            var iceHeart = gameDataCatalog.FindEquipment("ice_heart_norigae");
            var luckyPouch = gameDataCatalog.FindEquipment("bokjumeoni");
            var windRibbon = gameDataCatalog.FindEquipment("wind_daenggi");
            var tigerEye = gameDataCatalog.FindEquipment("tiger_eye_bead");
            var goblinHat = gameDataCatalog.FindEquipment("dokkaebi_gamtu");
            var yagwanggwi = gameDataCatalog.FindYokai("yakwang");
            if (bellCharm == null || iceHeart == null || luckyPouch == null || windRibbon == null ||
                tigerEye == null || goblinHat == null || yagwanggwi == null)
            {
                Debug.LogError("[Nyangbingo] Imported accessory or Yagwanggwi definition is missing.");
                return;
            }

            var definitionsMatch = bellCharm.IsAccessory && bellCharm.GrantsDoubleJump &&
                                   Mathf.Approximately(bellCharm.DoubleJumpHeightRatio, .8f) &&
                                   iceHeart.IsAccessory && Mathf.Approximately(iceHeart.TemperatureRiseModifier, -.15f) &&
                                   luckyPouch.IsAccessory && Mathf.Approximately(luckyPouch.MiningCriticalBonus, .1f) &&
                                   windRibbon.IsAccessory && windRibbon.MovementBonus > 0f &&
                                   tigerEye.IsAccessory && tigerEye.VisionRadiusBonus > 0f &&
                                   goblinHat.IsAccessory && goblinHat.BlocksInventoryTheft;

            var combatEquipment = new EquipmentSystem();
            var windEquipped = combatEquipment.TryEquipAccessory(windRibbon, 0);
            var hatEquipped = combatEquipment.TryEquipAccessory(goblinHat, 1);
            var invalidThirdSlotRejected = !combatEquipment.TryEquipAccessory(tigerEye, 2);
            var combatStats = new StatSheet();
            combatStats.Recalculate(combatEquipment);
            var combatStatsMatch = Mathf.Approximately(combatStats.MovementMultiplier, 1f + windRibbon.MovementBonus) &&
                                   combatStats.BlocksInventoryTheft && Mathf.Approximately(combatStats.VisionRadiusBonus, 0f);

            var explorationEquipment = new EquipmentSystem();
            explorationEquipment.TryEquipAccessory(tigerEye, 0);
            explorationEquipment.TryEquipAccessory(bellCharm, 1);
            var explorationStats = new StatSheet();
            explorationStats.Recalculate(explorationEquipment);
            var explorationStatsMatch = Mathf.Approximately(explorationStats.VisionRadiusBonus, tigerEye.VisionRadiusBonus) &&
                                        explorationStats.HasDoubleJump &&
                                        Mathf.Approximately(explorationStats.DoubleJumpHeightRatio, .8f) &&
                                        !explorationStats.BlocksInventoryTheft;

            var targetObject = new GameObject("TemporaryImportedGamtuTarget");
            targetObject.transform.position = Vector3.right * .5f;
            var target = targetObject.AddComponent<DevBTestYokaiTarget>();
            target.IsInventoryTheftBlocked = combatStats.BlocksInventoryTheft;
            var thiefObject = new GameObject("TemporaryImportedGamtuThief");
            var brain = thiefObject.AddComponent<YokaiBrain>();
            brain.ConfigureForRuntime(yagwanggwi, target);
            brain.Tick(0f);
            brain.Tick(1f);
            var theftBlocked = target.InventoryStealCount == 0;

            if (definitionsMatch && windEquipped && hatEquipped && invalidThirdSlotRejected &&
                combatStatsMatch && explorationStatsMatch && theftBlocked)
                Debug.Log("[Nyangbingo] Six imported accessories, vision stat, and gamtu theft protection completed.");
            else
                Debug.LogError("[Nyangbingo] Imported accessory stat or theft protection test failed.");

            Destroy(targetObject);
            Destroy(thiefObject);
        }

        private void TestImportedArmorStatsRecipesAndSetBonus()
        {
            if (gameDataCatalog == null)
            {
                Debug.LogError("[Nyangbingo] Imported armor catalog reference is missing.");
                return;
            }

            var ids = new[]
            {
                "straw_helm", "straw_armor", "straw_boots",
                "iron_helm", "iron_armor", "iron_boots",
                "icesteel_helm", "icesteel_armor", "icesteel_boots"
            };
            var slots = new[]
            {
                EquipmentSlot.Head, EquipmentSlot.Body, EquipmentSlot.Feet,
                EquipmentSlot.Head, EquipmentSlot.Body, EquipmentSlot.Feet,
                EquipmentSlot.Head, EquipmentSlot.Body, EquipmentSlot.Feet
            };
            var defenses = new[] { 1, 1, 1, 2, 3, 1, 3, 4, 3 };
            var armor = new EquipmentDefinition[ids.Length];
            var definitionsMatch = true;
            for (var i = 0; i < ids.Length; i++)
            {
                armor[i] = gameDataCatalog.FindEquipment(ids[i]);
                var item = gameDataCatalog.FindItem(ids[i]);
                definitionsMatch &= armor[i] != null && !armor[i].IsAccessory && armor[i].Slot == slots[i] &&
                                    armor[i].Defense == defenses[i] && item != null && item.MaxStack == 1;
            }

            definitionsMatch &= string.IsNullOrWhiteSpace(armor[0]?.SetId) &&
                                string.IsNullOrWhiteSpace(armor[3]?.SetId) &&
                                armor[6] != null && armor[6].SetId == "seolhanpung" &&
                                Mathf.Approximately(armor[6].SetTemperatureRiseModifier, -.20f) &&
                                Mathf.Approximately(armor[6].SetFireDamageModifier, -.25f);

            var recipesMatch =
                ArmorRecipeMatches("straw_helm", CraftingStation.Workbench, 10f,
                    new[] { "hemp_stalk", "wood" }, new[] { 4, 2 }) &&
                ArmorRecipeMatches("straw_armor", CraftingStation.Workbench, 10f,
                    new[] { "hemp_stalk", "wood" }, new[] { 6, 4 }) &&
                ArmorRecipeMatches("straw_boots", CraftingStation.Workbench, 10f,
                    new[] { "hemp_stalk", "wood" }, new[] { 3, 2 }) &&
                ArmorRecipeMatches("iron_helm", CraftingStation.Furnace, 30f,
                    new[] { "iron_ingot", "copper_ingot" }, new[] { 1, 1 }) &&
                ArmorRecipeMatches("iron_armor", CraftingStation.Furnace, 30f,
                    new[] { "iron_ingot", "copper_ingot" }, new[] { 3, 1 }) &&
                ArmorRecipeMatches("iron_boots", CraftingStation.Furnace, 30f,
                    new[] { "iron_ingot", "copper_ingot" }, new[] { 1, 1 }) &&
                ArmorRecipeMatches("icesteel_helm", CraftingStation.IceAnvil, 60f,
                    new[] { "icesteel_ingot" }, new[] { 2 }) &&
                ArmorRecipeMatches("icesteel_armor", CraftingStation.IceAnvil, 60f,
                    new[] { "icesteel_ingot" }, new[] { 4 }) &&
                ArmorRecipeMatches("icesteel_boots", CraftingStation.IceAnvil, 60f,
                    new[] { "icesteel_ingot" }, new[] { 2 });

            var strawEquipment = new EquipmentSystem();
            var strawEquipped = strawEquipment.TryEquip(armor[0]) && strawEquipment.TryEquip(armor[1]) &&
                                strawEquipment.TryEquip(armor[2]);
            var strawStats = new StatSheet();
            strawStats.Recalculate(strawEquipment);

            var ironEquipment = new EquipmentSystem();
            var ironEquipped = ironEquipment.TryEquip(armor[3]) && ironEquipment.TryEquip(armor[4]) &&
                               ironEquipment.TryEquip(armor[5]);
            var ironStats = new StatSheet();
            ironStats.Recalculate(ironEquipment);

            var iceEquipment = new EquipmentSystem();
            var partialEquipped = iceEquipment.TryEquip(armor[6]) && iceEquipment.TryEquip(armor[7]);
            var partialStats = new StatSheet();
            partialStats.Recalculate(iceEquipment);
            var fullEquipped = iceEquipment.TryEquip(armor[8]);
            var fullStats = new StatSheet();
            fullStats.Recalculate(iceEquipment);
            var iceHeart = gameDataCatalog.FindEquipment("ice_heart_norigae");
            var heartEquipped = iceEquipment.TryEquipAccessory(iceHeart, 0);
            var combinedStats = new StatSheet();
            combinedStats.Recalculate(iceEquipment);

            var statsMatch = strawEquipped && strawStats.Defense == 3 &&
                             Mathf.Approximately(strawStats.TemperatureRiseModifier, 0f) &&
                             Mathf.Approximately(strawStats.FireDamageModifier, 0f) &&
                             ironEquipped && ironStats.Defense == 6 &&
                             partialEquipped && partialStats.Defense == 7 &&
                             Mathf.Approximately(partialStats.TemperatureRiseModifier, 0f) &&
                             Mathf.Approximately(partialStats.FireDamageModifier, 0f) &&
                             fullEquipped && fullStats.Defense == 10 &&
                             Mathf.Approximately(fullStats.TemperatureRiseModifier, -.20f) &&
                             Mathf.Approximately(fullStats.FireDamageModifier, -.25f) &&
                             heartEquipped && Mathf.Approximately(combinedStats.TemperatureRiseModifier, -.35f) &&
                             Mathf.Approximately(combinedStats.FireDamageModifier, -.25f);

            if (definitionsMatch && recipesMatch && statsMatch)
                Debug.Log("[Nyangbingo] Nine imported armor pieces, recipes, and Seolhanpung set bonus completed.");
            else
                Debug.LogError("[Nyangbingo] Imported armor, recipe, or set-bonus test failed.");
        }

        private bool ArmorRecipeMatches(string id, CraftingStation station, float duration,
            string[] ingredientIds, int[] ingredientAmounts)
        {
            var recipe = gameDataCatalog.FindRecipe(id);
            if (recipe == null || recipe.Station != station || !Mathf.Approximately(recipe.DurationSeconds, duration) ||
                recipe.Output.item == null || recipe.Output.item.Id != id || recipe.Output.amount != 1 ||
                recipe.Ingredients == null || recipe.Ingredients.Length != ingredientIds.Length ||
                ingredientIds.Length != ingredientAmounts.Length) return false;
            for (var i = 0; i < ingredientIds.Length; i++)
                if (recipe.Ingredients[i].item == null || recipe.Ingredients[i].item.Id != ingredientIds[i] ||
                    recipe.Ingredients[i].amount != ingredientAmounts[i]) return false;
            return true;
        }

        private void TestEquipmentCollectionSaveRoundTrip()
        {
            var bellCharm = gameDataCatalog != null ? gameDataCatalog.FindEquipment("bell_norigae") : null;
            var goblinHat = gameDataCatalog != null ? gameDataCatalog.FindEquipment("dokkaebi_gamtu") : null;
            var tigerEye = gameDataCatalog != null ? gameDataCatalog.FindEquipment("tiger_eye_bead") : null;
            if (bellCharm == null || goblinHat == null || tigerEye == null)
            {
                Debug.LogError("[Nyangbingo] Equipment collection definitions are missing.");
                return;
            }

            var collection = new EquipmentCollection(gameDataCatalog.FindEquipment);
            var addedCount = 0;
            collection.Added += _ => addedCount++;
            var binding = new EquipmentAcquisitionBinding(collection);
            try
            {
                EquipmentAcquisition.Request(bellCharm);
                EquipmentAcquisition.Request(bellCharm);
                EquipmentAcquisition.Request(goblinHat);
                var acquisitionMatches = collection.Count == 2 && addedCount == 2 &&
                                         collection.Contains(bellCharm.Id) && collection.Contains(goblinHat.Id);

                var save = new SaveGame();
                var captured = EquipmentCollectionSaveAdapter.Capture(save, collection);
                var loaded = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(save));
                if (loaded != null) loaded.NormalizeAfterLoad();
                var restoredCollection = new EquipmentCollection(gameDataCatalog.FindEquipment);
                var restored = EquipmentCollectionSaveAdapter.Restore(loaded, restoredCollection);
                var roundTripMatches = restored && restoredCollection.Count == 2 &&
                                       restoredCollection.Contains(bellCharm.Id) && restoredCollection.Contains(goblinHat.Id);

                var corruptSave = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(save));
                if (corruptSave != null)
                {
                    corruptSave.NormalizeAfterLoad();
                    corruptSave.ownedEquipmentIds.Add("__missing_equipment__");
                }
                var corruptRejected = !EquipmentCollectionSaveAdapter.Restore(corruptSave,
                    new EquipmentCollection(gameDataCatalog.FindEquipment));

                var legacy = JsonUtility.FromJson<SaveGame>("{\"schemaVersion\":2,\"ownedEquipmentIds\":null}");
                if (legacy != null) legacy.NormalizeAfterLoad();
                var legacyCollection = new EquipmentCollection(gameDataCatalog.FindEquipment);
                var legacyRestored = EquipmentCollectionSaveAdapter.Restore(legacy, legacyCollection) &&
                                     legacy.schemaVersion == SaveGame.CurrentSchemaVersion && legacyCollection.Count == 0;

                binding.Dispose();
                EquipmentAcquisition.Request(tigerEye);
                var unsubscribed = collection.Count == 2 && !collection.Contains(tigerEye.Id);

                if (acquisitionMatches && captured && loaded != null &&
                    loaded.schemaVersion == SaveGame.CurrentSchemaVersion && roundTripMatches &&
                    corruptRejected && legacyRestored && unsubscribed)
                    Debug.Log("[Nyangbingo] Equipment acquisition collection, structured save, and v2 migration completed.");
                else
                    Debug.LogError("[Nyangbingo] Equipment collection save round-trip test failed.");
            }
            finally
            {
                binding.Dispose();
            }
        }

        private void TestEquipmentDefinitionIdentityAndSlotValidation()
        {
            var requested = EquipmentDefinition.CreateRuntime("equipment_identity_requested",
                EquipmentSlot.Head, false, 1);
            var mismatched = EquipmentDefinition.CreateRuntime("equipment_identity_mismatched",
                EquipmentSlot.Head, false, 1);
            var invalidCollection = new EquipmentCollection(id => id == requested.Id ? mismatched : null);
            var mismatchedResolverRejected = !invalidCollection.TryImport(new[] { requested.Id }) &&
                                             invalidCollection.Count == 0;

            var validCollection = new EquipmentCollection(id => id == requested.Id ? requested : null);
            var validResolverAccepted = validCollection.TryImport(new[] { requested.Id }) &&
                                        validCollection.Contains(requested.Id);
            var malformedArmor = EquipmentDefinition.CreateRuntime("equipment_invalid_armor_slot",
                EquipmentSlot.AccessoryOne, false, 2);
            var equipment = new EquipmentSystem();
            var invalidArmorSlotRejected = !equipment.TryEquip(malformedArmor) &&
                                           equipment.Get(EquipmentSlot.AccessoryOne) == null;
            var validArmorAccepted = equipment.TryEquip(requested) && equipment.Get(EquipmentSlot.Head) == requested;

            if (mismatchedResolverRejected && validResolverAccepted && invalidArmorSlotRejected && validArmorAccepted)
                Debug.Log("[Nyangbingo] Equipment resolver identity and armor-slot validation completed.");
            else Debug.LogError("[Nyangbingo] Equipment identity or slot validation test failed.");
        }

        private void TestEquipmentStatInvalidNumericGuard()
        {
            var valid = EquipmentDefinition.CreateRuntime("stat_guard_valid",
                EquipmentSlot.Head, false, 2, .1f, .1f, -.1f, .2f, false, 3f);
            var malformed = EquipmentDefinition.CreateRuntime("stat_guard_malformed",
                EquipmentSlot.AccessoryOne, true, int.MaxValue, float.NaN, float.PositiveInfinity,
                float.NaN, float.NegativeInfinity, false, float.NaN);
            var equipment = new EquipmentSystem();
            var equipped = equipment.TryEquip(valid) && equipment.TryEquipAccessory(malformed, 0);

            var stats = new StatSheet();
            stats.Recalculate(equipment);
            var nullStats = new StatSheet();
            nullStats.Recalculate(null);

            if (equipped && stats.Defense == int.MaxValue &&
                Mathf.Approximately(stats.MovementMultiplier, 1.1f) &&
                Mathf.Approximately(stats.MiningCriticalChance, .1f) &&
                Mathf.Approximately(stats.TemperatureRiseModifier, -.1f) &&
                Mathf.Approximately(stats.FireDamageModifier, .2f) &&
                Mathf.Approximately(stats.VisionRadiusBonus, 3f) &&
                nullStats.Defense == 0 && Mathf.Approximately(nullStats.MovementMultiplier, 1f))
                Debug.Log("[Nyangbingo] Equipment stat invalid numeric and overflow guard completed.");
            else Debug.LogError("[Nyangbingo] Equipment stat invalid numeric guard test failed.");
        }

        private void TestEquipmentTotalDefenseOverflowGuard()
        {
            var head = EquipmentDefinition.CreateRuntime("total_defense_guard_head",
                EquipmentSlot.Head, false, int.MaxValue);
            var body = EquipmentDefinition.CreateRuntime("total_defense_guard_body",
                EquipmentSlot.Body, false, int.MaxValue);
            var feet = EquipmentDefinition.CreateRuntime("total_defense_guard_feet",
                EquipmentSlot.Feet, false, -100);
            var equipment = new EquipmentSystem();
            var equipped = equipment.TryEquip(head) && equipment.TryEquip(body) && equipment.TryEquip(feet);

            if (equipped && equipment.TotalDefense == int.MaxValue)
                Debug.Log("[Nyangbingo] Equipment total defense overflow and negative-value guard completed.");
            else Debug.LogError("[Nyangbingo] Equipment total defense guard test failed.");
        }

        private void TestImportedChestRewardPools()
        {
            if (gameDataCatalog == null)
            {
                Debug.LogError("[Nyangbingo] Imported chest catalog reference is missing.");
                return;
            }

            var ruins = gameDataCatalog.FindChest("ruins_chest");
            var upper = gameDataCatalog.FindChest("upper_chest");
            var middle = gameDataCatalog.FindChest("middle_chest");
            var deep = gameDataCatalog.FindChest("deep_chest");
            var poolsMatch = MatchesChestPool(ruins, ChestRegion.Ruins, 4,
                                 new[] { "wind_daenggi", "dokkaebi_gamtu" },
                                 new[] { "rebar", "hemp_stalk", "catnip" }, new[] { 4, 6, 2 }) &&
                             MatchesChestPool(upper, ChestRegion.Upper, 6,
                                 new[] { "bell_norigae", "bokjumeoni" },
                                 new[] { "coal", "clay", "catnip" }, new[] { 4, 5, 3 }) &&
                             MatchesChestPool(middle, ChestRegion.Middle, 6,
                                 new[] { "wind_daenggi", "bokjumeoni", "ice_heart_norigae", "tiger_eye_bead" },
                                 new[] { "iron_ore", "ice_shard" }, new[] { 5, 4 }) &&
                             MatchesChestPool(deep, ChestRegion.Deep, 4,
                                 new[] { "tiger_eye_bead", "ice_heart_norigae", "bell_norigae", "dokkaebi_gamtu" },
                                 new[] { "icesteel_ore", "frost_essence" }, new[] { 2, 1 });

            const int worldSeed = 100;
            const string chestId = "chest_deep_00";
            var selected = ChestRewardSelector.SelectEquipment(worldSeed, chestId, deep);
            var repeatedSelection = ChestRewardSelector.SelectEquipment(worldSeed, chestId, deep);
            var deterministic = selected != null && selected == repeatedSelection &&
                                System.Array.IndexOf(deep.EquipmentPool, selected) >= 0;

            var progress = new ChestProgress(gameDataCatalog.FindItem);
            var opened = progress.TryOpen(chestId, deep, worldSeed);
            var duplicateRejected = !progress.TryOpen(chestId, deep, worldSeed);
            var rewardMatches = opened && duplicateRejected && progress.IsOpened(chestId) &&
                                progress.TryGetContents(chestId, out var chestStorage) &&
                                chestStorage.Count(selected.Id) == 1 &&
                                chestStorage.Count("icesteel_ore") == 2 &&
                                chestStorage.Count("frost_essence") == 1;

            if (poolsMatch && deterministic && rewardMatches)
                Debug.Log("[Nyangbingo] Official chest rewards remain sealed in deterministic loot storage until collected.");
            else
                Debug.LogError("[Nyangbingo] Imported chest reward pool test failed.");
        }

        private static bool MatchesChestPool(ChestDefinition definition, ChestRegion region, int spawnCount,
            string[] equipmentIds, string[] rewardIds, int[] rewardAmounts)
        {
            if (definition == null || definition.Region != region || definition.SpawnCount != spawnCount ||
                definition.EquipmentPool.Length != equipmentIds.Length ||
                definition.Rewards.Length != rewardIds.Length || rewardIds.Length != rewardAmounts.Length) return false;

            var uniqueIds = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
            for (var i = 0; i < equipmentIds.Length; i++)
            {
                var equipment = definition.EquipmentPool[i];
                if (equipment == null || !equipment.IsAccessory || equipment.Id != equipmentIds[i] ||
                    !uniqueIds.Add(equipment.Id)) return false;
            }
            for (var i = 0; i < rewardIds.Length; i++)
                if (definition.Rewards[i].item == null || definition.Rewards[i].item.Id != rewardIds[i] ||
                    definition.Rewards[i].amount != rewardAmounts[i]) return false;
            return true;
        }

        private void TestImportedUtilities()
        {
            if (gameDataCatalog == null)
            {
                Debug.LogError("[Nyangbingo] Imported utility catalog reference is missing.");
                return;
            }

            var hapjukseon = gameDataCatalog.FindUtility("hapjukseon");
            var bellRope = gameDataCatalog.FindUtility("bell_rope");
            var bellRopeRecipe = gameDataCatalog.FindRecipe("bell_rope");
            var ironBellRopeRecipe = gameDataCatalog.FindRecipe("iron_bell_rope");
            var hapjukseonItem = gameDataCatalog.FindItem("hapjukseon");
            var bellRopeItem = gameDataCatalog.FindItem("bell_rope");
            if (hapjukseon == null || bellRope == null || hapjukseonItem == null || bellRopeItem == null)
            {
                Debug.LogError("[Nyangbingo] Imported utility or matching item definitions are missing.");
                return;
            }

            var fanValue = -1f;
            var alarmValue = -1f;
            var fanUseCount = 0;
            var alarmUseCount = 0;
            var inventory = new Nyangbingo.Inventory.Inventory(gameDataCatalog.FindItem);
            inventory.TryAdd(hapjukseonItem.Id, 1);
            inventory.TryAdd(bellRopeItem.Id, 1);
            var service = new UtilityService(inventory);
            var bellService = new UtilityService(inventory);
            var installedBellService = new UtilityService();
            service.FanUsed += value => { fanValue = value; fanUseCount++; };
            bellService.AlarmPlaced += value => { alarmValue = value; alarmUseCount++; };
            var installedAlarmCount = 0;
            installedBellService.AlarmPlaced += _ => installedAlarmCount++;

            var dataMatches = hapjukseon.Kind == UtilityKind.Hapjukseon &&
                               hapjukseon.CooldownSeconds > 0f && hapjukseon.Value > 0f && !hapjukseon.Consumable &&
                               bellRope.Kind == UtilityKind.BellRope &&
                               Mathf.Approximately(bellRope.CooldownSeconds, 4f) &&
                               Mathf.Approximately(bellRope.Value, 10f) && !bellRope.Consumable &&
                               MainGameCraftingUiController.IsProductPlaceableRecipe(bellRopeRecipe) &&
                               MainGameCraftingUiController.IsProductPlaceableRecipe(ironBellRopeRecipe);

            var firstFanUse = service.TryUse(hapjukseon);
            var immediateFanBlocked = !service.TryUse(hapjukseon);
            var firstBellUse = bellService.TryUse(bellRope);
            var immediateBellBlocked = !bellService.TryUse(bellRope);
            var bellCooldownBoundaryStep = bellRope.CooldownSeconds * .1f;
            bellService.Tick(bellRope.CooldownSeconds - bellCooldownBoundaryStep);
            var earlyBellBlocked = !bellService.TryUse(bellRope) && Mathf.Approximately(
                bellService.GetCooldownRemaining(UtilityKind.BellRope), bellCooldownBoundaryStep);
            bellService.Tick(bellCooldownBoundaryStep);
            var bellReadyAtCooldown = bellService.TryUse(bellRope);
            var installedAlarmTriggered = installedBellService.TryTriggerInstalledBellRope(bellRope);
            var installedAlarmBlocked = !installedBellService.TryTriggerInstalledBellRope(bellRope);
            installedBellService.Tick(bellRope.CooldownSeconds);
            var installedAlarmReady = installedBellService.TryTriggerInstalledBellRope(bellRope) &&
                                      installedAlarmCount == 2;
            var cooldownBoundaryStep = hapjukseon.CooldownSeconds * .1f;
            service.Tick(hapjukseon.CooldownSeconds - cooldownBoundaryStep);
            var earlyFanBlocked = !service.TryUse(hapjukseon) &&
                                  Mathf.Approximately(service.GetCooldownRemaining(UtilityKind.Hapjukseon), cooldownBoundaryStep);
            service.Tick(cooldownBoundaryStep);
            var fanReadyAtCooldown = service.TryUse(hapjukseon);
            var eventsMatch = fanUseCount == 2 && Mathf.Approximately(fanValue, hapjukseon.Value) &&
                               alarmUseCount == 2 && Mathf.Approximately(alarmValue, bellRope.Value);
            var cooldownMatches = firstFanUse && immediateFanBlocked && firstBellUse && immediateBellBlocked &&
                                   earlyBellBlocked && bellReadyAtCooldown && earlyFanBlocked && fanReadyAtCooldown;
            var installedAlarmMatches = installedAlarmTriggered && installedAlarmBlocked && installedAlarmReady;
            var alertDirectionMatches =
                MainGameHudController.IsViewportPointVisible(new Vector3(.5f, .5f, 1f)) &&
                !MainGameHudController.IsViewportPointVisible(new Vector3(1.1f, .5f, 1f)) &&
                Mathf.Approximately(MainGameHudController.CalculateEdgeViewportPosition(
                    new Vector3(-1f, .5f, 1f)).x, .06f) &&
                MainGameHudController.DirectionGlyph(Vector2.left) == "◀" &&
                MainGameHudController.DirectionGlyph(Vector2.up) == "▲";
            var inventoryMatches = inventory.Count(hapjukseonItem.Id) == 1 &&
                                   inventory.Count(bellRopeItem.Id) == 1;

            if (dataMatches && eventsMatch)
                Debug.Log("[Nyangbingo] Imported utility data lookup and effect events completed.");
            else
                Debug.LogError("[Nyangbingo] Imported utility data or effect event test failed.");

            if (cooldownMatches)
                Debug.Log("[Nyangbingo] Utility independent game-seconds cooldown boundary completed.");
            else
                Debug.LogError("[Nyangbingo] Utility game-seconds cooldown test failed.");

            if (installedAlarmMatches && alertDirectionMatches)
                Debug.Log("[Nyangbingo] Bell-rope installed alarm uses the 4-second cooldown and clamps offscreen threat direction to the HUD edge.");
            else
                Debug.LogError("[Nyangbingo] Bell-rope installed alarm or HUD direction test failed.");

            if (inventoryMatches)
                Debug.Log("[Nyangbingo] Utility inventory ownership and non-consumable preservation completed.");
            else
                Debug.LogError("[Nyangbingo] Utility inventory consumption test failed.");
        }

        private void TestImportedFanIdAndRecipeContract()
        {
            if (gameDataCatalog == null)
            {
                Debug.LogError("[Nyangbingo] Imported fan item catalog reference is missing.");
                return;
            }

            var hapjukseon = gameDataCatalog.FindItem(FanItemIds.Hapjukseon);
            var cheolseon = gameDataCatalog.FindItem(FanItemIds.Cheolseon);
            var utility = gameDataCatalog.FindUtility(FanItemIds.Hapjukseon);
            var idsMatch = FanItemIds.LegacyFoldingFan == "folding_fan" &&
                           FanItemIds.Hapjukseon == "hapjukseon" && FanItemIds.Cheolseon == "cheolseon" &&
                           hapjukseon != null && hapjukseon.DisplayName == "합죽선" && hapjukseon.MaxStack == 1 &&
                           cheolseon != null && cheolseon.DisplayName == "철선" && cheolseon.MaxStack == 1 &&
                           gameDataCatalog.FindItem(FanItemIds.LegacyFoldingFan) == null &&
                           gameDataCatalog.FindRecipe(FanItemIds.LegacyFoldingFan) == null;
            var utilityMatch = utility != null && utility.Kind == UtilityKind.Hapjukseon &&
                               Mathf.Approximately(utility.CooldownSeconds, 3f) &&
                               Mathf.Approximately(utility.Value, 2f) && !utility.Consumable &&
                               gameDataCatalog.FindUtility(FanItemIds.Cheolseon) == null;
            var recipesMatch =
                ArmorRecipeMatches(FanItemIds.Hapjukseon, CraftingStation.Workbench, 10f,
                    new[] { "hemp_stalk", "wood" }, new[] { 6, 3 }) &&
                ArmorRecipeMatches(FanItemIds.Cheolseon, CraftingStation.Furnace, 30f,
                    new[] { FanItemIds.Hapjukseon, "iron_ingot", "iron_scale" }, new[] { 1, 2, 1 });
            var evolutionRecipe = gameDataCatalog.FindRecipe(FanItemIds.Cheolseon);
            var evolutionInventory = new Nyangbingo.Inventory.Inventory(gameDataCatalog.FindItem);
            evolutionInventory.TryAdd(FanItemIds.Hapjukseon, 1);
            evolutionInventory.TryAdd("iron_ingot", 2);
            evolutionInventory.TryAdd("iron_scale", 1);
            var evolved = new CraftingService(evolutionInventory).TryCraft(evolutionRecipe, CraftingStation.Furnace) &&
                          evolutionInventory.Count(FanItemIds.Hapjukseon) == 0 &&
                          evolutionInventory.Count("iron_ingot") == 0 && evolutionInventory.Count("iron_scale") == 0 &&
                          evolutionInventory.Count(FanItemIds.Cheolseon) == 1;

            if (idsMatch && utilityMatch && recipesMatch && evolved)
                Debug.Log("[Nyangbingo] Hapjukseon and Cheolseon IDs, utility data, and evolution recipes completed.");
            else
                Debug.LogError("[Nyangbingo] Hapjukseon or Cheolseon ID and recipe contract test failed.");
        }

        private void TestUtilityCooldownSaveRoundTrip()
        {
            var hapjukseon = gameDataCatalog != null ? gameDataCatalog.FindUtility("hapjukseon") : null;
            var hapjukseonItem = gameDataCatalog != null ? gameDataCatalog.FindItem("hapjukseon") : null;
            if (hapjukseon == null || hapjukseonItem == null)
            {
                Debug.LogError("[Nyangbingo] Utility cooldown save definitions are missing.");
                return;
            }

            var sourceInventory = new Nyangbingo.Inventory.Inventory(gameDataCatalog.FindItem);
            sourceInventory.TryAdd(hapjukseonItem.Id, 1);
            var sourceService = new UtilityService(sourceInventory);
            var started = sourceService.TryUse(hapjukseon);
            sourceService.Tick(hapjukseon.CooldownSeconds * .25f);

            var save = new SaveGame();
            var captured = UtilityCooldownSaveAdapter.Capture(save, sourceService);
            var loaded = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(save));
            if (loaded != null) loaded.NormalizeAfterLoad();

            var restoredInventory = new Nyangbingo.Inventory.Inventory(gameDataCatalog.FindItem);
            restoredInventory.TryAdd(hapjukseonItem.Id, 1);
            var restoredService = new UtilityService(restoredInventory);
            var restored = UtilityCooldownSaveAdapter.Restore(loaded, restoredService);
            var expectedRemaining = hapjukseon.CooldownSeconds * .75f;
            var boundaryStep = expectedRemaining * .01f;
            var remainingRestored = Mathf.Approximately(
                restoredService.GetCooldownRemaining(UtilityKind.Hapjukseon), expectedRemaining);
            restoredService.Tick(expectedRemaining - boundaryStep);
            var blockedBeforeBoundary = !restoredService.TryUse(hapjukseon);
            restoredService.Tick(boundaryStep);
            var readyAtBoundary = restoredService.TryUse(hapjukseon) && restoredInventory.Count(hapjukseonItem.Id) == 1;

            var corrupt = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(save));
            if (corrupt != null)
            {
                corrupt.NormalizeAfterLoad();
                corrupt.utilityCooldowns[0] = new UtilityCooldownRecord
                {
                    kind = UtilityKind.Hapjukseon.ToString(),
                    remainingGameSeconds = -1f
                };
            }
            var corruptRejected = !UtilityCooldownSaveAdapter.Restore(corrupt, sourceService) &&
                                  Mathf.Approximately(sourceService.GetCooldownRemaining(UtilityKind.Hapjukseon), expectedRemaining);

            var legacy = JsonUtility.FromJson<SaveGame>("{\"schemaVersion\":3,\"utilityCooldowns\":null}");
            if (legacy != null) legacy.NormalizeAfterLoad();
            var legacyService = new UtilityService();
            var legacyRestored = UtilityCooldownSaveAdapter.Restore(legacy, legacyService) &&
                                 legacy.schemaVersion == SaveGame.CurrentSchemaVersion &&
                                 Mathf.Approximately(legacyService.GetCooldownRemaining(UtilityKind.Hapjukseon), 0f);

            var renamedLegacy = new SaveGame { schemaVersion = 6 };
            renamedLegacy.inventory.Add(new InventorySlot { itemId = "folding_fan", amount = 1 });
            renamedLegacy.utilityCooldowns.Add(new UtilityCooldownRecord
            {
                kind = "FoldingFan",
                remainingGameSeconds = expectedRemaining
            });
            renamedLegacy.NormalizeAfterLoad();
            var renamedLegacyService = new UtilityService();
            var renamedLegacyRestored = renamedLegacy.schemaVersion == SaveGame.CurrentSchemaVersion &&
                                        renamedLegacy.inventory.Count == 1 &&
                                        renamedLegacy.inventory[0].itemId == hapjukseonItem.Id &&
                                        UtilityCooldownSaveAdapter.Restore(renamedLegacy, renamedLegacyService) &&
                                        Mathf.Approximately(
                                            renamedLegacyService.GetCooldownRemaining(UtilityKind.Hapjukseon),
                                            expectedRemaining);

            if (started && captured && loaded != null && loaded.schemaVersion == SaveGame.CurrentSchemaVersion &&
                restored && remainingRestored && blockedBeforeBoundary && readyAtBoundary &&
                corruptRejected && legacyRestored && renamedLegacyRestored)
                Debug.Log("[Nyangbingo] Utility game-seconds cooldown structured save and v3 migration completed.");
            else
                Debug.LogError("[Nyangbingo] Utility cooldown save round-trip test failed.");
        }

        private void TestImportedSmeltingStationRules()
        {
            var smeltIron = gameDataCatalog != null ? gameDataCatalog.FindSmelting("smelt_iron") : null;
            var smeltCopper = gameDataCatalog != null ? gameDataCatalog.FindSmelting("smelt_copper") : null;
            var smeltIceSteel = gameDataCatalog != null ? gameDataCatalog.FindSmelting("smelt_icesteel") : null;
            if (smeltIron == null || smeltCopper == null || smeltIceSteel == null)
            {
                Debug.LogError("[Nyangbingo] Imported smelting station definitions are missing.");
                return;
            }

            var inventory = new Nyangbingo.Inventory.Inventory(gameDataCatalog.FindItem);
            inventory.TryAdd(smeltIron.Input.item.Id, 20);
            inventory.TryAdd(smeltIceSteel.Input.item.Id, 20);
            inventory.TryAdd(smeltIron.Fuel.item.Id, 20);
            var furnace = new SmeltingStation(inventory, smeltIron.StationKind, smeltIron.BatchCapacity);
            var foundry = new SmeltingStation(inventory, smeltIceSteel.StationKind, smeltIceSteel.BatchCapacity);

            var definitionsMatch = smeltIron.StationKind == SmeltingStationKind.Furnace &&
                                   smeltCopper.StationKind == SmeltingStationKind.Furnace &&
                                   smeltIceSteel.StationKind == SmeltingStationKind.Foundry &&
                                   smeltIron.BatchCapacity > 0 &&
                                   smeltIron.BatchCapacity == smeltCopper.BatchCapacity &&
                                   smeltIceSteel.BatchCapacity > 0;
            var crossStationRejected = !furnace.TryStart(smeltIceSteel) && !foundry.TryStart(smeltIron);

            var furnaceAccepted = true;
            for (var i = 0; i < smeltIron.BatchCapacity; i++) furnaceAccepted &= furnace.TryStart(smeltIron);
            var furnaceOverflowRejected = !furnace.TryStart(smeltIron);

            var foundryAccepted = true;
            for (var i = 0; i < smeltIceSteel.BatchCapacity; i++) foundryAccepted &= foundry.TryStart(smeltIceSteel);
            var foundryOverflowRejected = !foundry.TryStart(smeltIceSteel);

            var capacitiesMatch = furnace.QueueCapacity == smeltIron.BatchCapacity && furnace.IsSmelting &&
                                  furnace.Queue.Count == smeltIron.BatchCapacity - 1 &&
                                  foundry.QueueCapacity == smeltIceSteel.BatchCapacity && foundry.IsSmelting &&
                                  foundry.Queue.Count == smeltIceSteel.BatchCapacity - 1;

            if (definitionsMatch && crossStationRejected && furnaceAccepted && furnaceOverflowRejected &&
                foundryAccepted && foundryOverflowRejected && capacitiesMatch)
                Debug.Log("[Nyangbingo] Imported smelting station types and data-driven batch capacities completed.");
            else
                Debug.LogError("[Nyangbingo] Imported smelting station or capacity test failed.");

            var overflowInventory = new Nyangbingo.Inventory.Inventory(gameDataCatalog.FindItem);
            overflowInventory.TryAdd(smeltIron.Input.item.Id, 10);
            overflowInventory.TryAdd(smeltIron.Fuel.item.Id, 10);
            var overflowFurnace = new SmeltingStation(overflowInventory, smeltIron.StationKind,
                smeltIron.BatchCapacity);
            var threeQueued = overflowFurnace.TryStart(smeltIron) && overflowFurnace.TryStart(smeltIron) &&
                              overflowFurnace.TryStart(smeltIron);
            var invalidTicksRejected = !overflowFurnace.Tick(-1f) && !overflowFurnace.Tick(0f) &&
                                       !overflowFurnace.Tick(float.NaN) && !overflowFurnace.Tick(float.PositiveInfinity) &&
                                       Mathf.Approximately(overflowFurnace.RemainingSeconds, smeltIron.DurationSeconds) &&
                                       overflowFurnace.Completed.Count == 0;
            var overflowCompleted = overflowFurnace.Tick(smeltIron.DurationSeconds * 2.25f) &&
                                    overflowFurnace.Completed.Count == 2 &&
                                    overflowFurnace.Active == smeltIron && overflowFurnace.Queue.Count == 0 &&
                                    Mathf.Approximately(overflowFurnace.RemainingSeconds,
                                        smeltIron.DurationSeconds * .75f);

            if (threeQueued && invalidTicksRejected && overflowCompleted)
                Debug.Log("[Nyangbingo] Smelting invalid game-seconds rejection and queued overflow completed.");
            else
                Debug.LogError("[Nyangbingo] Smelting timer input or overflow test failed.");
        }

        private void TestSmeltingSharedInputFuelTransaction()
        {
            var resource = ItemDefinition.CreateRuntime("smelting_shared_resource", "Smelting Shared Resource");
            var output = ItemDefinition.CreateRuntime("smelting_shared_output", "Smelting Shared Output");
            var inventory = new Nyangbingo.Inventory.Inventory(id =>
                id == resource.Id ? resource : id == output.Id ? output : null);
            var definition = SmeltingDefinition.CreateRuntime("smelting_shared_recipe",
                new ItemAmount { item = resource, amount = 1 },
                new ItemAmount { item = resource, amount = 1 },
                new ItemAmount { item = output, amount = 1 }, 1f);
            var station = new SmeltingStation(inventory, definition.StationKind, definition.BatchCapacity);

            inventory.TryAdd(resource.Id, 1);
            var insufficientRejected = !station.TryStart(definition) && inventory.Count(resource.Id) == 1 &&
                                       !station.IsSmelting && station.Queue.Count == 0;
            inventory.TryAdd(resource.Id, 1);
            var exactAmountAccepted = station.TryStart(definition) && inventory.Count(resource.Id) == 0 &&
                                      station.Active == definition && station.Queue.Count == 0;

            if (insufficientRejected && exactAmountAccepted)
                Debug.Log("[Nyangbingo] Smelting shared input-fuel atomic consumption completed.");
            else Debug.LogError("[Nyangbingo] Smelting shared input-fuel transaction test failed.");
        }

        private void TestSmeltingRestoreValidation()
        {
            var input = ItemDefinition.CreateRuntime("smelting_restore_input", "Smelting Restore Input");
            var fuel = ItemDefinition.CreateRuntime("smelting_restore_fuel", "Smelting Restore Fuel");
            var output = ItemDefinition.CreateRuntime("smelting_restore_output", "Smelting Restore Output");
            var inventory = new Nyangbingo.Inventory.Inventory(id =>
                id == input.Id ? input : id == fuel.Id ? fuel : id == output.Id ? output : null);
            var valid = SmeltingDefinition.CreateRuntime("smelting_restore_valid",
                new ItemAmount { item = input, amount = 1 }, new ItemAmount { item = fuel, amount = 1 },
                new ItemAmount { item = output, amount = 1 }, 2f);
            var malformed = SmeltingDefinition.CreateRuntime("smelting_restore_malformed",
                new ItemAmount { item = input, amount = 1 }, new ItemAmount { item = fuel, amount = 1 },
                new ItemAmount { item = output, amount = 1 }, float.NaN);
            var station = new SmeltingStation(inventory, valid.StationKind, valid.BatchCapacity);
            var noQueue = System.Array.Empty<SmeltingDefinition>();
            var noOutputs = System.Array.Empty<ItemAmount>();

            var excessiveRemainingRejected = !station.RestoreState(valid, 3f, noQueue, noOutputs);
            var orphanRemainingRejected = !station.RestoreState(null, 1f, noQueue, noOutputs);
            var malformedDefinitionRejected = !station.RestoreState(malformed, 1f, noQueue, noOutputs);
            var completedStateRestored = station.RestoreState(valid, 0f, noQueue, noOutputs);
            var completedOnNextTick = station.Tick(.01f) && !station.IsSmelting &&
                                      station.Completed.Count == 1 && station.Completed[0].item == output &&
                                      station.Completed[0].amount == 1;

            if (excessiveRemainingRejected && orphanRemainingRejected && malformedDefinitionRejected &&
                completedStateRestored && completedOnNextTick)
                Debug.Log("[Nyangbingo] Smelting strict restore validation completed.");
            else Debug.LogError("[Nyangbingo] Smelting restore validation test failed.");
        }

        private void TestImportedTimedCrafting()
        {
            if (importedTimedRecipe == null || importedTimedRecipe.Output.item == null || importedTimedRecipe.DurationSeconds <= 0f)
            {
                Debug.LogError("[Nyangbingo] Imported timed recipe is missing or invalid.");
                return;
            }

            var items = new System.Collections.Generic.Dictionary<string, ItemDefinition>();
            items[importedTimedRecipe.Output.item.Id] = importedTimedRecipe.Output.item;
            foreach (var ingredient in importedTimedRecipe.Ingredients)
            {
                if (ingredient.item == null || ingredient.amount <= 0)
                {
                    Debug.LogError("[Nyangbingo] Imported timed recipe has an invalid ingredient.");
                    return;
                }
                items[ingredient.item.Id] = ingredient.item;
            }

            var inventory = new Nyangbingo.Inventory.Inventory(id => items.TryGetValue(id, out var item) ? item : null);
            foreach (var ingredient in importedTimedRecipe.Ingredients)
                if (!inventory.TryAdd(ingredient.item.Id, ingredient.amount))
                {
                    Debug.LogError("[Nyangbingo] Imported timed recipe ingredients could not be prepared.");
                    return;
                }

            var process = new CraftingProcess(new CraftingService(inventory));
            var halfDuration = importedTimedRecipe.DurationSeconds * .5f;
            var started = process.TryStart(importedTimedRecipe, importedTimedRecipe.Station);
            var invalidTicksRejected = !process.Tick(-1f) && !process.Tick(0f) && !process.Tick(float.NaN) &&
                                       !process.Tick(float.PositiveInfinity) &&
                                       Mathf.Approximately(process.RemainingSeconds, importedTimedRecipe.DurationSeconds);
            var completedEarly = process.Tick(halfDuration);
            var completedOnTime = process.Tick(importedTimedRecipe.DurationSeconds - halfDuration);
            if (started && invalidTicksRejected && !completedEarly && completedOnTime &&
                inventory.Count(importedTimedRecipe.Output.item.Id) == importedTimedRecipe.Output.amount)
                Debug.Log("[Nyangbingo] Imported CSV recipe timed crafting completed.");
            else Debug.LogError("[Nyangbingo] Imported CSV recipe timed crafting test failed.");

            if (invalidTicksRejected)
                Debug.Log("[Nyangbingo] Crafting invalid game-seconds rejection completed.");
            else Debug.LogError("[Nyangbingo] Crafting timer input validation test failed.");
        }

        private void TestTimedCraftingFullInventoryProtection()
        {
            var ingredient = ItemDefinition.CreateRuntime("crafting_capacity_ingredient", "Crafting Capacity Ingredient", 1);
            var output = ItemDefinition.CreateRuntime("crafting_capacity_output", "Crafting Capacity Output", 1);
            var blocker = ItemDefinition.CreateRuntime("crafting_capacity_blocker", "Crafting Capacity Blocker", 1);
            var inventory = new Nyangbingo.Inventory.Inventory(id =>
                id == ingredient.Id ? ingredient : id == output.Id ? output : id == blocker.Id ? blocker : null);
            inventory.TryAdd(ingredient.Id, 1);
            inventory.TryAdd(blocker.Id, Nyangbingo.Inventory.Inventory.SlotCount - 1);

            var recipe = RecipeDefinition.CreateRuntime("crafting_capacity_recipe", CraftingStation.None,
                new[] { new ItemAmount { item = ingredient, amount = 1 } },
                new ItemAmount { item = output, amount = 1 }, 1f);
            var process = new CraftingProcess(new CraftingService(inventory));
            var started = process.TryStart(recipe, CraftingStation.None);
            var inventoryFilledDuringCraft = inventory.TryAdd(blocker.Id, 1);
            var completedWithoutSpace = process.Tick(1f);
            var outputWasRetained = !completedWithoutSpace && process.IsCrafting &&
                                    Mathf.Approximately(process.RemainingSeconds, 0f) && inventory.Count(output.Id) == 0;
            var spaceFreed = inventory.TryRemove(blocker.Id, 1);
            var completedAfterSpace = process.Tick(.01f);

            if (started && inventoryFilledDuringCraft && outputWasRetained && spaceFreed && completedAfterSpace &&
                !process.IsCrafting && inventory.Count(output.Id) == 1)
                Debug.Log("[Nyangbingo] Timed crafting full-inventory output retention completed.");
            else Debug.LogError("[Nyangbingo] Timed crafting full-inventory output retention test failed.");
        }

        private void TestCraftingRecipeValidation()
        {
            var ingredient = ItemDefinition.CreateRuntime("crafting_validation_ingredient", "Crafting Validation Ingredient");
            var output = ItemDefinition.CreateRuntime("crafting_validation_output", "Crafting Validation Output");
            var inventory = new Nyangbingo.Inventory.Inventory(id =>
                id == ingredient.Id ? ingredient : id == output.Id ? output : null);
            inventory.TryAdd(ingredient.Id, 1);
            var service = new CraftingService(inventory);
            var process = new CraftingProcess(service);

            var duplicateIngredientRecipe = RecipeDefinition.CreateRuntime("duplicate_ingredient", CraftingStation.None,
                new[]
                {
                    new ItemAmount { item = ingredient, amount = 1 },
                    new ItemAmount { item = ingredient, amount = 1 }
                }, new ItemAmount { item = output, amount = 1 }, 1f);
            var zeroOutputRecipe = RecipeDefinition.CreateRuntime("zero_output", CraftingStation.None,
                new[] { new ItemAmount { item = ingredient, amount = 1 } },
                new ItemAmount { item = output, amount = 0 });
            var invalidIngredientRecipe = RecipeDefinition.CreateRuntime("invalid_ingredient", CraftingStation.None,
                new[] { new ItemAmount { item = ingredient, amount = 0 } },
                new ItemAmount { item = output, amount = 1 });
            var nullIngredientsRecipe = RecipeDefinition.CreateRuntime("null_ingredients", CraftingStation.None, null,
                new ItemAmount { item = output, amount = 1 });
            var nanDurationRecipe = RecipeDefinition.CreateRuntime("nan_duration", CraftingStation.None,
                new[] { new ItemAmount { item = ingredient, amount = 1 } },
                new ItemAmount { item = output, amount = 1 }, float.NaN);
            var infiniteDurationRecipe = RecipeDefinition.CreateRuntime("infinite_duration", CraftingStation.None,
                new[] { new ItemAmount { item = ingredient, amount = 1 } },
                new ItemAmount { item = output, amount = 1 }, float.PositiveInfinity);

            var rejected = !service.CanCraft(duplicateIngredientRecipe, CraftingStation.None) &&
                           !service.TryCraft(duplicateIngredientRecipe, CraftingStation.None) &&
                           !process.TryStart(duplicateIngredientRecipe, CraftingStation.None) &&
                           !service.TryCraft(zeroOutputRecipe, CraftingStation.None) &&
                           !service.TryCraft(invalidIngredientRecipe, CraftingStation.None) &&
                           !service.TryCraft(nullIngredientsRecipe, CraftingStation.None) &&
                           !service.TryCraft(nanDurationRecipe, CraftingStation.None) &&
                           !service.TryCraft(infiniteDurationRecipe, CraftingStation.None) &&
                           !process.TryStart(nanDurationRecipe, CraftingStation.None) &&
                           !process.TryStart(infiniteDurationRecipe, CraftingStation.None);

            if (rejected && inventory.Count(ingredient.Id) == 1 && inventory.Count(output.Id) == 0 && !process.IsCrafting)
                Debug.Log("[Nyangbingo] Crafting malformed recipe and duplicate ingredient rejection completed.");
            else Debug.LogError("[Nyangbingo] Crafting recipe validation test failed.");
        }

        private void TestCraftingProcessSaveRoundTrip()
        {
            var ingredient = ItemDefinition.CreateRuntime("crafting_save_ingredient", "Crafting Save Ingredient");
            var output = ItemDefinition.CreateRuntime("crafting_save_output", "Crafting Save Output");
            var recipe = RecipeDefinition.CreateRuntime("crafting_save_recipe", CraftingStation.Workbench,
                new[] { new ItemAmount { item = ingredient, amount = 1 } },
                new ItemAmount { item = output, amount = 1 }, 3f);
            ItemDefinition FindItem(string id) => id == ingredient.Id ? ingredient : id == output.Id ? output : null;

            var inventory = new Nyangbingo.Inventory.Inventory(FindItem);
            inventory.TryAdd(ingredient.Id, 1);
            var source = new CraftingProcess(new CraftingService(inventory));
            var started = source.TryStart(recipe, CraftingStation.Workbench);
            source.Tick(1f);
            var save = new SaveGame();
            var captured = CraftingProcessSaveAdapter.Capture(save, source);
            var loaded = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(save));

            var restored = new CraftingProcess(new CraftingService(inventory));
            var restoredState = CraftingProcessSaveAdapter.Restore(loaded, restored,
                id => id == recipe.Id ? recipe : null);
            var restoredProgress = restored.IsCrafting && restored.Active == recipe &&
                                   Mathf.Approximately(restored.RemainingSeconds, 2f);
            var completedEarly = restored.Tick(1.99f);
            var completedOnTime = restored.Tick(.01f);

            var corrupt = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(save));
            corrupt.activeCrafting.remainingGameSeconds = 4f;
            var corruptRejected = !CraftingProcessSaveAdapter.Restore(corrupt,
                new CraftingProcess(new CraftingService(inventory)), id => id == recipe.Id ? recipe : null);
            var legacy = JsonUtility.FromJson<SaveGame>("{\"schemaVersion\":4,\"activeCrafting\":null}");
            var legacyProcess = new CraftingProcess(new CraftingService(inventory));
            var legacyRestored = CraftingProcessSaveAdapter.Restore(legacy, legacyProcess,
                                     id => id == recipe.Id ? recipe : null) &&
                                 legacy.schemaVersion == SaveGame.CurrentSchemaVersion && !legacyProcess.IsCrafting;

            if (started && captured && loaded != null && loaded.schemaVersion == SaveGame.CurrentSchemaVersion &&
                restoredState && restoredProgress && !completedEarly && completedOnTime &&
                inventory.Count(ingredient.Id) == 0 && inventory.Count(output.Id) == 1 &&
                corruptRejected && legacyRestored)
                Debug.Log("[Nyangbingo] Timed crafting structured save round-trip completed.");
            else Debug.LogError("[Nyangbingo] Timed crafting structured save round-trip test failed.");
        }

        private void TestRecipeBookSaveRoundTrip()
        {
            var output = ItemDefinition.CreateRuntime("recipe_book_output", "Recipe Book Output");
            var alpha = RecipeDefinition.CreateRuntime("recipe_alpha", CraftingStation.None,
                System.Array.Empty<ItemAmount>(), new ItemAmount { item = output, amount = 1 });
            var beta = RecipeDefinition.CreateRuntime("recipe_beta", CraftingStation.None,
                System.Array.Empty<ItemAmount>(), new ItemAmount { item = output, amount = 1 });
            var locked = RecipeDefinition.CreateRuntime("recipe_locked", CraftingStation.None,
                System.Array.Empty<ItemAmount>(), new ItemAmount { item = output, amount = 1 });
            RecipeDefinition FindRecipe(string id) =>
                id == alpha.Id ? alpha : id == beta.Id ? beta : id == locked.Id ? locked : null;

            var source = new RecipeBook();
            source.Unlock(beta.Id);
            source.Unlock(alpha.Id);
            var save = new SaveGame();
            var captured = RecipeBookSaveAdapter.Capture(save, source);
            var deterministicOrder = save.unlockedRecipes.Count == 2 &&
                                     save.unlockedRecipes[0] == alpha.Id && save.unlockedRecipes[1] == beta.Id;
            var loaded = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(save));
            var restoredBook = new RecipeBook();
            var restored = RecipeBookSaveAdapter.Restore(loaded, restoredBook, FindRecipe);

            var corrupt = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(save));
            corrupt.unlockedRecipes.Add(alpha.Id);
            var unchangedBook = new RecipeBook();
            unchangedBook.Unlock(locked.Id);
            var duplicateRejected = !RecipeBookSaveAdapter.Restore(corrupt, unchangedBook, FindRecipe) &&
                                    unchangedBook.IsUnlocked(locked) && !unchangedBook.IsUnlocked(alpha);
            var unknown = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(save));
            unknown.unlockedRecipes[0] = "missing_recipe";
            var unknownRejected = !RecipeBookSaveAdapter.Restore(unknown, new RecipeBook(), FindRecipe);

            if (captured && deterministicOrder && loaded != null && restored &&
                restoredBook.IsUnlocked(alpha) && restoredBook.IsUnlocked(beta) && !restoredBook.IsUnlocked(locked) &&
                duplicateRejected && unknownRejected)
                Debug.Log("[Nyangbingo] Recipe book unlock structured save and validation completed.");
            else Debug.LogError("[Nyangbingo] Recipe book save round-trip test failed.");
        }

        private void TestGaekgwiPatternRuntime()
        {
            var definition = YokaiDefinition.CreateRuntime(YokaiKind.Gaekgwi, 3240, 2f, 16, 24f,
                System.Array.Empty<ItemAmount>());
            var targetObject = new GameObject("TemporaryGaekgwiTarget");
            targetObject.transform.position = Vector3.right * 4f;
            var target = targetObject.AddComponent<DevBTestGaekgwiTarget>();
            var yokaiObject = new GameObject("TemporaryGaekgwi");
            var brain = yokaiObject.AddComponent<YokaiBrain>();
            var telegraphCount = 0;
            var dashCount = 0;
            var wailCount = 0;
            brain.GaekgwiTelegraphStarted += () => telegraphCount++;
            brain.GaekgwiDashStarted += () => dashCount++;
            brain.GaekgwiWailTriggered += () => wailCount++;
            brain.ConfigureForRuntime(definition, target);

            for (var second = 0; second < 12; second++) brain.Tick(1f);
            var cooldownExpiredWhileUsingNormalApproach =
                Mathf.Approximately(brain.GaekgwiCooldownRemaining, 0f) &&
                Mathf.Approximately(yokaiObject.transform.position.x, 3f) &&
                telegraphCount == 0;
            brain.Tick(0f);
            var telegraphStarted = telegraphCount == 1 && brain.IsGaekgwiPatternActive &&
                                   Mathf.Approximately(brain.GaekgwiTelegraphRemaining, 1f);
            brain.Tick(.999f);
            var telegraphHeldForFullSecond = dashCount == 0 &&
                                             brain.GaekgwiTelegraphRemaining > 0f;
            brain.Tick(.001f);
            var dashStarted = dashCount == 1 && brain.GaekgwiDashRemaining > 0f;
            targetObject.transform.position = Vector3.right * 6f;
            brain.Tick(.12f);
            var frameOnePosition = yokaiObject.transform.position.x;
            brain.Tick(.12f);
            var frameTwoPosition = yokaiObject.transform.position.x;
            brain.Tick(.12f);
            var frameThreePosition = yokaiObject.transform.position.x;
            brain.Tick(.12f);
            var reachedFinalFrame = Mathf.Approximately(
                yokaiObject.transform.position.x, 6f) && wailCount == 0;
            brain.Tick(.12f);
            var frameMotionMatches =
                Mathf.Approximately(frameOnePosition, 3f + 3f * (30f / 68.5f)) &&
                Mathf.Approximately(frameTwoPosition, 3f + 3f * (56.5f / 68.5f)) &&
                Mathf.Approximately(frameThreePosition, 3f + 3f * (65f / 68.5f));
            var landedAtTarget = reachedFinalFrame &&
                                 Mathf.Approximately(yokaiObject.transform.position.x, 6f);
            var wailAppliedOnce = wailCount == 1 && target.SpecialHitCount == 1 &&
                                  target.LastDamage == YokaiBrain.GaekgwiWailDamage &&
                                  target.LastDamageTag == DamageTag.Ice &&
                                  target.LastKnockback == Vector2.right;
            var record = new YokaiStateRecord();
            brain.CaptureSaveState(record);
            var patternSaveCaptured = record.gaekgwiPatternInitialized &&
                                      record.gaekgwiPatternState == 0 &&
                                      Mathf.Approximately(record.gaekgwiCooldownRemaining,
                                          YokaiBrain.GaekgwiCooldownSeconds);
            brain.Tick(1f);
            var noRepeatedWail = target.SpecialHitCount == 1 && wailCount == 1;
            var distanceGuard = Mathf.Approximately(
                YokaiBrain.CalculateGaekgwiDashDistance(),
                YokaiBrain.GaekgwiDashDistanceTiles) &&
                                Mathf.Approximately(
                                    YokaiBrain.CalculateGaekgwiDashProgress(.2f),
                                    30f / 68.5f) &&
                                Mathf.Approximately(
                                    YokaiBrain.CalculateGaekgwiDashProgress(.8f), 1f);

            if (cooldownExpiredWhileUsingNormalApproach && telegraphStarted &&
                telegraphHeldForFullSecond && dashStarted && frameMotionMatches && landedAtTarget &&
                wailAppliedOnce && patternSaveCaptured && noRepeatedWail && distanceGuard)
                Debug.Log("[Nyangbingo] Gaekgwi telegraph, dash, landing wail, cooldown, and save state completed.");
            else Debug.LogError("[Nyangbingo] Gaekgwi v34 combat pattern test failed.");

            Destroy(targetObject);
            Destroy(yokaiObject);
            Destroy(definition);
        }

        private void TestImportedBaekjungSchedule()
        {
            if (importedBaekjungEvent == null)
            {
                Debug.LogError("[Nyangbingo] Imported Baekjung day event is missing.");
                return;
            }

            var scheduler = new BaekjungScheduler(new[] { importedBaekjungEvent });
            var baekjungStartCount = 0;
            var dispatchedWaves = new System.Collections.Generic.List<int>();
            System.Action onBaekjungStart = () => baekjungStartCount++;
            System.Action<DayEventDefinition, int> onWaveReady = (definition, waveIndex) =>
            {
                if (definition == importedBaekjungEvent) dispatchedWaves.Add(waveIndex);
            };

            GameEvents.OnBaekjungStart += onBaekjungStart;
            scheduler.WaveReady += onWaveReady;
            try
            {
                var rejectedWrongDay = !scheduler.TryStartNight(importedBaekjungEvent.Day - 1);
                var started = scheduler.TryStartNight(importedBaekjungEvent.Day);
                var firstWaveAtNightStart = dispatchedWaves.Count == 1 && dispatchedWaves[0] == 0;
                scheduler.Tick(149f);
                var secondWaveWasEarly = dispatchedWaves.Count != 1;
                scheduler.Tick(1f);
                var secondWaveOnTime = dispatchedWaves.Count == 2 && dispatchedWaves[1] == 1;
                scheduler.Tick(149f);
                var thirdWaveWasEarly = dispatchedWaves.Count != 2;
                scheduler.Tick(1f);
                var thirdWaveOnTime = dispatchedWaves.Count == 3 && dispatchedWaves[2] == 2;
                var duplicateStartRejected = !scheduler.TryStartNight(importedBaekjungEvent.Day);

                var composition = importedBaekjungEvent.Composition;
                var matchesPlan = importedBaekjungEvent.Id == "baekjung" && importedBaekjungEvent.Day == 15 &&
                    importedBaekjungEvent.MaxActive == 12 && importedBaekjungEvent.WaveOffsets.Length == 3 &&
                    Mathf.Approximately(importedBaekjungEvent.WaveOffsets[0], 0f) &&
                    Mathf.Approximately(importedBaekjungEvent.WaveOffsets[1], 150f) &&
                    Mathf.Approximately(importedBaekjungEvent.WaveOffsets[2], 300f) &&
                    composition.Length == 4 && composition[0].kind == YokaiKind.ClubGoblin && composition[0].amount == 3 &&
                    composition[1].kind == YokaiKind.Bulgasari && composition[1].amount == 2 &&
                    composition[2].kind == YokaiKind.Yagwanggwi && composition[2].amount == 6 &&
                    composition[3].kind == YokaiKind.Gaekgwi && composition[3].amount == 1 &&
                    Mathf.Approximately(importedBaekjungEvent.TearMultiplier, 1.5f) &&
                    Mathf.Approximately(importedBaekjungEvent.SignatureMultiplier, 2f);

                if (rejectedWrongDay && started && firstWaveAtNightStart && !secondWaveWasEarly && secondWaveOnTime &&
                    !thirdWaveWasEarly && thirdWaveOnTime && duplicateStartRejected && baekjungStartCount == 1 &&
                    scheduler.IsScheduleComplete && matchesPlan)
                    Debug.Log("[Nyangbingo] Imported Baekjung event and game-seconds wave schedule completed.");
                else Debug.LogError("[Nyangbingo] Imported Baekjung event or wave schedule test failed.");
            }
            finally
            {
                scheduler.WaveReady -= onWaveReady;
                GameEvents.OnBaekjungStart -= onBaekjungStart;
            }
        }

        private void TestBaekjungWaveSpawnRequests()
        {
            if (importedBaekjungEvent == null)
            {
                Debug.LogError("[Nyangbingo] Baekjung wave spawn request test asset is missing.");
                return;
            }

            var controller = new DevBTestBaekjungSpawnController();
            var scheduler = new BaekjungScheduler(new[] { importedBaekjungEvent });
            var waveSpawner = new BaekjungWaveSpawner(scheduler, controller);
            var capController = new DevBTestBaekjungSpawnController();
            var capScheduler = new BaekjungScheduler(new[] { importedBaekjungEvent });
            var cappedWaveSpawner = new BaekjungWaveSpawner(capScheduler, capController);
            var dawnController = new DevBTestBaekjungSpawnController();
            var dawnScheduler = new BaekjungScheduler(new[] { importedBaekjungEvent });
            var dawnWaveSpawner = new BaekjungWaveSpawner(dawnScheduler, dawnController);
            BaekjungWaveSpawner restoredQueueSpawner = null;
            try
            {
                var started = scheduler.TryStartNight(importedBaekjungEvent.Day);
                controller.DefeatAll();
                scheduler.Tick(150f);
                controller.DefeatAll();
                scheduler.Tick(150f);

                var composition = importedBaekjungEvent.Composition;
                var expectedTotal = 0;
                for (var groupIndex = 0; groupIndex < composition.Length; groupIndex++)
                    expectedTotal += Mathf.Max(0, composition[groupIndex].amount);

                var waveCount = importedBaekjungEvent.WaveOffsets.Length;
                var allWavesMatch = controller.Records.Count == expectedTotal && waveCount > 0;
                for (var groupIndex = 0; groupIndex < composition.Length; groupIndex++)
                {
                    var kindSpawnCount = 0;
                    for (var recordIndex = 0; recordIndex < controller.Records.Count; recordIndex++)
                        if (controller.Records[recordIndex].Kind == composition[groupIndex].kind) kindSpawnCount++;
                    allWavesMatch &= kindSpawnCount == composition[groupIndex].amount;
                }
                allWavesMatch &= controller.Count(YokaiKind.Gaekgwi, 1) == 1 &&
                                 controller.Count(YokaiKind.Gaekgwi, 0) == 0 &&
                                 controller.Count(YokaiKind.Gaekgwi, 2) == 0;

                var firstWaveCount = waveCount > 0 ? expectedTotal / waveCount : 0;
                capController.SeedResident(50);
                capController.SeedActive(importedBaekjungEvent.MaxActive - 1);
                var cappedStarted = capScheduler.TryStartNight(importedBaekjungEvent.Day);
                var maxActiveRespected = capController.ActiveRaidCount == importedBaekjungEvent.MaxActive &&
                    capController.ResidentCount == 50 && capController.Records.Count == 1 &&
                    cappedWaveSpawner.PendingCount == firstWaveCount - 1;
                var capturedPendingKinds = cappedWaveSpawner.CapturePendingKinds();
                var restoredQueueScheduler = new BaekjungScheduler(new[] { importedBaekjungEvent });
                var restoredQueueController = new DevBTestBaekjungSpawnController();
                restoredQueueController.SeedActive(importedBaekjungEvent.MaxActive);
                var schedulerStateRestored =
                    restoredQueueScheduler.RestoreState(capScheduler.CaptureState());
                restoredQueueSpawner = new BaekjungWaveSpawner(
                    restoredQueueScheduler, restoredQueueController);
                var pendingDefinitions = new System.Collections.Generic.List<YokaiDefinition>();
                for (var pendingIndex = 0; pendingIndex < capturedPendingKinds.Count; pendingIndex++)
                {
                    var pendingKind = capturedPendingKinds[pendingIndex];
                    YokaiDefinition pendingDefinition = null;
                    for (var yokaiIndex = 0; yokaiIndex < gameDataCatalog.Yokai.Count; yokaiIndex++)
                    {
                        var candidate = gameDataCatalog.Yokai[yokaiIndex];
                        if (candidate != null && candidate.Kind == pendingKind)
                        {
                            pendingDefinition = candidate;
                            break;
                        }
                    }
                    pendingDefinitions.Add(pendingDefinition);
                }
                var pendingQueueRestored = schedulerStateRestored &&
                    restoredQueueSpawner.RestorePendingDefinitions(pendingDefinitions) &&
                    restoredQueueSpawner.PendingCount == capturedPendingKinds.Count &&
                    restoredQueueController.Records.Count == 0;
                capController.DefeatRaid(1);
                var singleSlotRetriedAutomatically = capController.ActiveRaidCount == importedBaekjungEvent.MaxActive &&
                    capController.Records.Count == 2 && cappedWaveSpawner.PendingCount == firstWaveCount - 2;
                capController.DefeatAll();
                cappedWaveSpawner.RetryPending();
                var overflowRetriedWithoutDuplicates = capController.Records.Count == firstWaveCount &&
                    cappedWaveSpawner.PendingCount == 0;

                dawnController.SeedResident(100);
                dawnController.SeedActive(importedBaekjungEvent.MaxActive);
                var dawnStarted = dawnScheduler.TryStartNight(importedBaekjungEvent.Day);
                var queuedBeforeDawn = dawnWaveSpawner.PendingCount == firstWaveCount &&
                                       dawnController.Records.Count == 0;
                GameEvents.RaiseDawnWarning();
                var discardedAtFleeTime = dawnWaveSpawner.PendingCount == 0 &&
                                          dawnWaveSpawner.DiscardedAtDawnCount == firstWaveCount;
                dawnController.DefeatAll();
                dawnScheduler.Tick(300f);
                var noSpawnAfterDawnWarning = dawnController.Records.Count == 0 &&
                                              dawnWaveSpawner.PendingCount == 0 &&
                                              dawnController.ResidentCount == 100;
                var dawnEnded = dawnScheduler.TryEndAtDawn();

                if (started && scheduler.IsScheduleComplete && allWavesMatch && cappedStarted && maxActiveRespected &&
                    pendingQueueRestored && singleSlotRetriedAutomatically &&
                    overflowRetriedWithoutDuplicates && dawnStarted &&
                    queuedBeforeDawn && discardedAtFleeTime && noSpawnAfterDawnWarning && dawnEnded)
                    Debug.Log("[Nyangbingo] Baekjung raid-only cap, automatic overflow queue, and dawn discard completed.");
                else Debug.LogError("[Nyangbingo] Baekjung wave spawn request test failed.");
            }
            finally
            {
                waveSpawner.Dispose();
                cappedWaveSpawner.Dispose();
                dawnWaveSpawner.Dispose();
                restoredQueueSpawner?.Dispose();
            }
        }

        private void TestBaekjungRegularSpawnPauseResume()
        {
            if (importedBaekjungEvent == null)
            {
                Debug.LogError("[Nyangbingo] Baekjung regular spawn gate test asset is missing.");
                return;
            }

            var regularSpawnController = gameObject.AddComponent<DevBTestSpawnController>();
            var scheduler = new BaekjungScheduler(new[] { importedBaekjungEvent });
            var gate = new BaekjungRegularSpawnGate(scheduler, regularSpawnController);
            var restoredRegularController = gameObject.AddComponent<DevBTestSpawnController>();
            BaekjungRegularSpawnGate restoredGate = null;
            var endedCount = 0;
            System.Action<DayEventDefinition> onEnded = _ => endedCount++;
            scheduler.Ended += onEnded;
            try
            {
                var wrongDayRejected = !scheduler.TryStartNight(importedBaekjungEvent.Day - 1) &&
                    regularSpawnController.IsRegularSpawning;
                var started = scheduler.TryStartNight(importedBaekjungEvent.Day);
                var pausedAtStart = !regularSpawnController.IsRegularSpawning && scheduler.IsActive;
                var restoredScheduler = new BaekjungScheduler(new[] { importedBaekjungEvent });
                var restoredActiveState = restoredScheduler.RestoreState(scheduler.CaptureState());
                restoredGate = new BaekjungRegularSpawnGate(restoredScheduler, restoredRegularController);
                var restoredGatePaused = restoredActiveState &&
                    !restoredRegularController.IsRegularSpawning;
                var restoredGateResumed = restoredScheduler.TryEndAtDawn() &&
                    restoredRegularController.IsRegularSpawning;
                var ended = scheduler.TryEndAtDawn();
                var resumedAtDawn = regularSpawnController.IsRegularSpawning && scheduler.HasEnded && !scheduler.IsActive;
                var duplicateEndRejected = !scheduler.TryEndAtDawn();
                scheduler.Tick(300f);
                var noWavesAfterDawn = scheduler.DispatchedWaveCount == 1;

                if (wrongDayRejected && started && pausedAtStart && restoredGatePaused &&
                    restoredGateResumed && ended && resumedAtDawn && duplicateEndRejected &&
                    endedCount == 1 && noWavesAfterDawn)
                    Debug.Log("[Nyangbingo] Baekjung regular spawn pause and dawn resume completed.");
                else Debug.LogError("[Nyangbingo] Baekjung regular spawn gate test failed.");
            }
            finally
            {
                scheduler.Ended -= onEnded;
                gate.Dispose();
                restoredGate?.Dispose();
                Destroy(regularSpawnController);
                Destroy(restoredRegularController);
            }
        }

        private void TestBaekjungRewardMultipliers()
        {
            if (importedBaekjungEvent == null)
            {
                Debug.LogError("[Nyangbingo] Baekjung reward multiplier test asset is missing.");
                return;
            }

            var rewards = new BaekjungRewardRules(importedBaekjungEvent);
            var firstSingleTear = rewards.ScaleTearAmount(1);
            var secondSingleTear = rewards.ScaleTearAmount(1);
            var twoTears = rewards.ScaleTearAmount(2);
            var quarterSignatureChance = rewards.ScaleSignatureChance(.25f);
            var halfSignatureChance = rewards.ScaleSignatureChance(.5f);
            var cappedSignatureChance = rewards.ScaleSignatureChance(1f);

            if (firstSingleTear == 1 && secondSingleTear == 2 && twoTears == 3 &&
                Mathf.Approximately(rewards.TearRemainder, 0f) &&
                Mathf.Approximately(quarterSignatureChance, .5f) &&
                Mathf.Approximately(halfSignatureChance, 1f) &&
                Mathf.Approximately(cappedSignatureChance, 1f) &&
                rewards.ScaleTearAmount(0) == 0 && Mathf.Approximately(rewards.ScaleSignatureChance(0f), 0f))
                Debug.Log("[Nyangbingo] Baekjung tear and signature reward multipliers completed.");
            else Debug.LogError("[Nyangbingo] Baekjung reward multiplier test failed.");
        }

        private void TestImportedYokaiLootWithBaekjungRewards()
        {
            if (importedClubGoblin == null || importedBaekjungEvent == null ||
                importedClubGoblin.TearItem == null || importedClubGoblin.SignatureItem == null)
            {
                Debug.LogError("[Nyangbingo] Imported yokai loot test data is missing.");
                return;
            }

            var bulgasari = gameDataCatalog.FindYokai("bulgasari");
            var yagwanggwi = gameDataCatalog.FindYokai("yakwang");
            var eoduksini = gameDataCatalog.FindYokai("eoduksini");
            var conditionalStatsMatch = bulgasari != null && yagwanggwi != null && eoduksini != null &&
                bulgasari.WallDamageFor(YokaiWallMaterial.Ice) >
                bulgasari.WallDamageFor(YokaiWallMaterial.Default) &&
                Mathf.Approximately(bulgasari.WallDamageFor(YokaiWallMaterial.IronHeatWall), 0f) &&
                yagwanggwi.StealSlots > 0 && yagwanggwi.StealMaxItems > 0 &&
                eoduksini.ContactDamageNoLantern > eoduksini.ContactDamage &&
                eoduksini.DamageTakenCondition == YokaiDamageTakenCondition.LanternRadius &&
                eoduksini.DamageTakenMultiplier > 1f;

            var rewardRules = new BaekjungRewardRules(importedBaekjungEvent);
            var random = new DevBTestLootRandomSource(.3f, .5f);
            var tearCount = 0;
            var signatureCount = 0;
            var unexpectedDropCount = 0;

            for (var killIndex = 0; killIndex < 2; killIndex++)
            {
                var yokaiObject = new GameObject($"TemporaryImportedYokai{killIndex}");
                var health = yokaiObject.AddComponent<Health>();
                health.ConfigureForRuntime(importedClubGoblin.HitPoints);
                var loot = yokaiObject.AddComponent<YokaiLoot>();
                loot.ConfigureForRuntime(importedClubGoblin, random, rewardRules);
                loot.Dropped += (item, amount) =>
                {
                    if (item == importedClubGoblin.TearItem) tearCount += amount;
                    else if (item == importedClubGoblin.SignatureItem) signatureCount += amount;
                    else unexpectedDropCount += amount;
                };
                health.ApplyDamage(importedClubGoblin.HitPoints, DamageTag.Melee);
                Destroy(yokaiObject);
            }

            if (importedClubGoblin.Id == "club" && importedClubGoblin.TearDrop == 1 &&
                Mathf.Approximately(importedClubGoblin.SignatureChance, .25f) &&
                tearCount == 3 && signatureCount == 1 && unexpectedDropCount == 0 && random.CallCount == 2 &&
                conditionalStatsMatch)
                Debug.Log("[Nyangbingo] Imported yokai conditional stats, loot, and Baekjung reward flow completed.");
            else Debug.LogError("[Nyangbingo] Imported yokai loot or Baekjung reward flow test failed.");
        }

        private void TestYokaiLootInvalidRandomRejection()
        {
            if (importedClubGoblin == null || importedClubGoblin.SignatureItem == null ||
                importedClubGoblin.SignatureChance <= 0f)
            {
                Debug.LogError("[Nyangbingo] Yokai invalid loot-roll test data is missing.");
                return;
            }

            var random = new DevBTestLootRandomSource(-1f, 2f, float.NaN, float.PositiveInfinity);
            var signatureDrops = 0;
            for (var i = 0; i < 4; i++)
            {
                var yokai = new GameObject($"TemporaryInvalidLootRollYokai{i}");
                var health = yokai.AddComponent<Health>();
                health.ConfigureForRuntime(importedClubGoblin.HitPoints);
                var loot = yokai.AddComponent<YokaiLoot>();
                loot.ConfigureForRuntime(importedClubGoblin, random);
                loot.Dropped += (item, amount) =>
                {
                    if (item == importedClubGoblin.SignatureItem) signatureDrops += amount;
                };
                health.ApplyDamage(importedClubGoblin.HitPoints, DamageTag.Melee);
                Destroy(yokai);
            }

            if (signatureDrops == 0 && random.CallCount == 4)
                Debug.Log("[Nyangbingo] Yokai loot invalid random-roll rejection completed.");
            else Debug.LogError("[Nyangbingo] Yokai loot invalid random-roll test failed.");
        }

        private void TestBaekjungTimeBinding()
        {
            if (importedBaekjungEvent == null)
            {
                Debug.LogError("[Nyangbingo] Baekjung time binding test asset is missing.");
                return;
            }

            var timeSource = gameObject.AddComponent<DevBTestTimeSource>();
            timeSource.Day = importedBaekjungEvent.Day;
            timeSource.IsNight = false;
            var scheduler = new BaekjungScheduler(new[] { importedBaekjungEvent });
            var binding = new BaekjungTimeBinding(timeSource, scheduler);
            try
            {
                GameEvents.RaiseNightStart();
                var ignoredWhileDay = !scheduler.HasStarted;
                timeSource.IsNight = true;
                GameEvents.RaiseNightStart();
                var startedAtNight = scheduler.IsActive && scheduler.DispatchedWaveCount == 1;
                binding.Tick(149f);
                var secondWaveWasEarly = scheduler.DispatchedWaveCount != 1;
                binding.Tick(1f);
                var secondWaveOnTime = scheduler.DispatchedWaveCount == 2;
                timeSource.RaiseDawn();
                binding.Tick(150f);
                var endedAtDawn = scheduler.HasEnded && !scheduler.IsActive && scheduler.DispatchedWaveCount == 2;

                var loadedAtNightScheduler = new BaekjungScheduler(new[] { importedBaekjungEvent });
                var loadedAtNightBinding = new BaekjungTimeBinding(timeSource, loadedAtNightScheduler);
                var loadedAtNightStartedImmediately = loadedAtNightScheduler.IsActive &&
                    loadedAtNightScheduler.DispatchedWaveCount == 1;
                loadedAtNightBinding.Dispose();

                if (ignoredWhileDay && startedAtNight && !secondWaveWasEarly && secondWaveOnTime &&
                    endedAtDawn && loadedAtNightStartedImmediately)
                    Debug.Log("[Nyangbingo] Baekjung night, game-seconds tick, dawn, and load-time binding completed.");
                else Debug.LogError("[Nyangbingo] Baekjung time binding test failed.");
            }
            finally
            {
                binding.Dispose();
                Destroy(timeSource);
            }
        }

        private void TestBaekjungSaveStateRoundTrip()
        {
            if (importedBaekjungEvent == null)
            {
                Debug.LogError("[Nyangbingo] Baekjung save state test asset is missing.");
                return;
            }

            var sourceScheduler = new BaekjungScheduler(new[] { importedBaekjungEvent });
            var sourceRewards = new BaekjungRewardRules(importedBaekjungEvent);
            sourceScheduler.TryStartNight(importedBaekjungEvent.Day);
            sourceScheduler.Tick(150f);
            sourceRewards.ScaleTearAmount(1);

            var sourceSave = new SaveGame
            {
                day = importedBaekjungEvent.Day,
                timeOfDaySec = 150f,
                baekjungProgress = sourceScheduler.CaptureState(),
                baekjungTearRemainder = sourceRewards.TearRemainder
            };
            var loadedSave = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(sourceSave));
            if (loadedSave != null) loadedSave.NormalizeAfterLoad();
            var legacySave = JsonUtility.FromJson<SaveGame>("{\"day\":15,\"inventory\":null,\"baekjungProgress\":null}");
            if (legacySave != null) legacySave.NormalizeAfterLoad();
            var restoredScheduler = new BaekjungScheduler(new[] { importedBaekjungEvent });
            var restoredRewards = new BaekjungRewardRules(importedBaekjungEvent);
            var restored = loadedSave != null && restoredScheduler.RestoreState(loadedSave.baekjungProgress);
            if (loadedSave != null) restoredRewards.RestoreTearRemainder(loadedSave.baekjungTearRemainder);

            var resumedWaveCount = 0;
            var baekjungRestartCount = 0;
            restoredScheduler.WaveReady += (_, __) => resumedWaveCount++;
            System.Action onBaekjungStart = () => baekjungRestartCount++;
            GameEvents.OnBaekjungStart += onBaekjungStart;
            var timeSource = gameObject.AddComponent<DevBTestTimeSource>();
            timeSource.Day = importedBaekjungEvent.Day;
            timeSource.IsNight = true;
            var binding = new BaekjungTimeBinding(timeSource, restoredScheduler);
            try
            {
                binding.Tick(149f);
                var finalWaveWasEarly = resumedWaveCount != 0;
                binding.Tick(1f);
                var resumedOnlyFinalWave = resumedWaveCount == 1 && restoredScheduler.DispatchedWaveCount == 3;
                var carriedTearRemainder = restoredRewards.ScaleTearAmount(1) == 2 &&
                    Mathf.Approximately(restoredRewards.TearRemainder, 0f);

                if (restored && loadedSave.schemaVersion == SaveGame.CurrentSchemaVersion &&
                    legacySave != null && legacySave.schemaVersion == SaveGame.CurrentSchemaVersion &&
                    legacySave.inventory != null && legacySave.baekjungProgress != null &&
                    !finalWaveWasEarly && resumedOnlyFinalWave && baekjungRestartCount == 0 &&
                    restoredScheduler.IsScheduleComplete && carriedTearRemainder)
                    Debug.Log("[Nyangbingo] Versioned Baekjung progress and reward save round-trip completed.");
                else Debug.LogError("[Nyangbingo] Baekjung save state round-trip test failed.");
            }
            finally
            {
                binding.Dispose();
                GameEvents.OnBaekjungStart -= onBaekjungStart;
                Destroy(timeSource);
            }
        }

        private void TestProgressionSaveRoundTrip()
        {
            var wood = ItemDefinition.CreateRuntime("save_wood", "Save Wood");
            var fuel = ItemDefinition.CreateRuntime("save_fuel", "Save Fuel");
            var ingot = ItemDefinition.CreateRuntime("save_ingot", "Save Ingot");
            var items = new System.Collections.Generic.Dictionary<string, ItemDefinition>
            {
                [wood.Id] = wood,
                [fuel.Id] = fuel,
                [ingot.Id] = ingot
            };
            var inventory = new Nyangbingo.Inventory.Inventory(id => items.TryGetValue(id, out var item) ? item : null);
            inventory.TryAdd(wood.Id, 10);
            inventory.TryAdd(fuel.Id, 4);

            var helmet = EquipmentDefinition.CreateRuntime("save_helmet", EquipmentSlot.Head, false, 3);
            var charm = EquipmentDefinition.CreateRuntime("save_charm", EquipmentSlot.AccessoryOne, true, 0);
            var equipmentDefinitions = new System.Collections.Generic.Dictionary<string, EquipmentDefinition>
            {
                [helmet.Id] = helmet,
                [charm.Id] = charm
            };
            var equipment = new EquipmentSystem();
            equipment.TryEquip(helmet);
            equipment.TryEquipAccessory(charm, 1);

            var smeltingDefinition = SmeltingDefinition.CreateRuntime("save_smelting",
                new ItemAmount { item = wood, amount = 2 }, new ItemAmount { item = fuel, amount = 1 },
                new ItemAmount { item = ingot, amount = 1 }, 1f, capacity: 3);
            var smelting = new SmeltingStation(inventory, smeltingDefinition.StationKind,
                smeltingDefinition.BatchCapacity);
            smelting.TryStart(smeltingDefinition);
            smelting.TryStart(smeltingDefinition);
            smelting.Tick(1f);
            smelting.Tick(.4f);
            smelting.TryStart(smeltingDefinition);

            var save = new SaveGame();
            ProgressionSaveAdapter.Capture(save, inventory, equipment, "test_furnace", smelting);
            var loaded = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(save));
            if (loaded != null) loaded.NormalizeAfterLoad();
            var legacy = JsonUtility.FromJson<SaveGame>(
                "{\"schemaVersion\":0,\"smelting\":[{\"stationId\":\"legacy_furnace\",\"recipeId\":\"save_smelting\",\"remainingSeconds\":0.5}]}");
            if (legacy != null) legacy.NormalizeAfterLoad();

            var restoredInventory = new Nyangbingo.Inventory.Inventory(id => items.TryGetValue(id, out var item) ? item : null);
            var restoredEquipment = new EquipmentSystem();
            var restoredSmelting = new SmeltingStation(restoredInventory, smeltingDefinition.StationKind,
                smeltingDefinition.BatchCapacity);
            var restored = ProgressionSaveAdapter.Restore(loaded, restoredInventory, restoredEquipment,
                id => equipmentDefinitions.TryGetValue(id, out var definition) ? definition : null,
                "test_furnace", restoredSmelting,
                id => id == smeltingDefinition.Id ? smeltingDefinition : null,
                id => items.TryGetValue(id, out var item) ? item : null);

            var inventoryMatches = restoredInventory.Count(wood.Id) == 4 && restoredInventory.Count(fuel.Id) == 1;
            var equipmentMatches = restoredEquipment.Get(EquipmentSlot.Head) == helmet &&
                restoredEquipment.Get(EquipmentSlot.AccessoryTwo) == charm;
            var smeltingMatches = restoredSmelting.Active == smeltingDefinition &&
                Mathf.Approximately(restoredSmelting.RemainingSeconds, .6f) &&
                restoredSmelting.Queue.Count == 1 && restoredSmelting.Queue[0] == smeltingDefinition &&
                restoredSmelting.Completed.Count == 1 && restoredSmelting.Completed[0].item == ingot &&
                restoredSmelting.Completed[0].amount == 1;
            var legacyMigrated = legacy != null && legacy.schemaVersion == SaveGame.CurrentSchemaVersion &&
                legacy.smelting.Count == 1 && legacy.smelting[0].isActive && legacy.smelting[0].queueIndex == -1;
            var collected = restoredSmelting.TryCollect(0);
            var collectedMatches = collected && restoredInventory.Count(ingot.Id) == 1;

            if (restored && inventoryMatches && equipmentMatches && smeltingMatches &&
                legacyMigrated && collectedMatches)
                Debug.Log("[Nyangbingo] Inventory, equipment, and smelting structured save round-trip completed.");
            else Debug.LogError($"[Nyangbingo] Progression structured save round-trip test failed. " +
                $"restored={restored}, inventory={inventoryMatches}, equipment={equipmentMatches}, " +
                $"smelting={smeltingMatches}, legacy={legacyMigrated}, collected={collectedMatches}, " +
                $"remaining={restoredSmelting.RemainingSeconds}, queue={restoredSmelting.Queue.Count}, " +
                $"outputs={restoredSmelting.Completed.Count}.");
        }

        private void TestProgressionRestoreInventoryPrevalidation()
        {
            var marker = ItemDefinition.CreateRuntime("progression_prevalidation_marker", "Progression Marker", 1);
            var input = ItemDefinition.CreateRuntime("progression_prevalidation_input", "Progression Input");
            var fuel = ItemDefinition.CreateRuntime("progression_prevalidation_fuel", "Progression Fuel");
            var output = ItemDefinition.CreateRuntime("progression_prevalidation_output", "Progression Output");
            ItemDefinition FindItem(string id) =>
                id == marker.Id ? marker : id == input.Id ? input : id == fuel.Id ? fuel : id == output.Id ? output : null;
            var inventory = new Nyangbingo.Inventory.Inventory(FindItem);
            inventory.TryAdd(marker.Id, 1);
            var originalHelmet = EquipmentDefinition.CreateRuntime("progression_original_helmet",
                EquipmentSlot.Head, false, 1);
            var replacementHelmet = EquipmentDefinition.CreateRuntime("progression_replacement_helmet",
                EquipmentSlot.Head, false, 2);
            var equipment = new EquipmentSystem();
            equipment.TryEquip(originalHelmet);
            var smeltingDefinition = SmeltingDefinition.CreateRuntime("progression_prevalidation_smelting",
                new ItemAmount { item = input, amount = 1 }, new ItemAmount { item = fuel, amount = 1 },
                new ItemAmount { item = output, amount = 1 }, 2f);
            var smelting = new SmeltingStation(inventory, smeltingDefinition.StationKind,
                smeltingDefinition.BatchCapacity);
            var save = new SaveGame
            {
                inventory = new System.Collections.Generic.List<InventorySlot>
                {
                    new InventorySlot { itemId = "missing_item", amount = 1 }
                },
                equipment = new System.Collections.Generic.List<EquipmentRecord>
                {
                    new EquipmentRecord { slot = EquipmentSlot.Head.ToString(), equipmentId = replacementHelmet.Id }
                },
                smelting = new System.Collections.Generic.List<SmeltingRecord>
                {
                    new SmeltingRecord
                    {
                        stationId = "prevalidation_furnace",
                        recipeId = smeltingDefinition.Id,
                        remainingSeconds = 1f,
                        isActive = true,
                        queueIndex = -1
                    }
                }
            };

            var restored = ProgressionSaveAdapter.Restore(save, inventory, equipment,
                id => id == originalHelmet.Id ? originalHelmet : id == replacementHelmet.Id ? replacementHelmet : null,
                "prevalidation_furnace", smelting,
                id => id == smeltingDefinition.Id ? smeltingDefinition : null, FindItem);
            var stateUnchanged = inventory.Count(marker.Id) == 1 && inventory.Count(output.Id) == 0 &&
                                 equipment.Get(EquipmentSlot.Head) == originalHelmet && !smelting.IsSmelting &&
                                 smelting.Queue.Count == 0 && smelting.Completed.Count == 0;

            if (!restored && stateUnchanged)
                Debug.Log("[Nyangbingo] Progression invalid inventory prevalidation completed.");
            else Debug.LogError("[Nyangbingo] Progression inventory prevalidation test failed.");
        }

        private void TestProgressionRestoreEquipmentPrevalidation()
        {
            var marker = ItemDefinition.CreateRuntime("equipment_prevalidation_marker", "Equipment Marker", 1);
            var input = ItemDefinition.CreateRuntime("equipment_prevalidation_input", "Equipment Input");
            var fuel = ItemDefinition.CreateRuntime("equipment_prevalidation_fuel", "Equipment Fuel");
            var output = ItemDefinition.CreateRuntime("equipment_prevalidation_output", "Equipment Output");
            ItemDefinition FindItem(string id) => id == marker.Id ? marker : id == input.Id ? input :
                id == fuel.Id ? fuel : id == output.Id ? output : null;
            var inventory = new Nyangbingo.Inventory.Inventory(FindItem);
            inventory.TryAdd(marker.Id, 1);

            var originalHelmet = EquipmentDefinition.CreateRuntime("equipment_prevalidation_original",
                EquipmentSlot.Head, false, 1);
            var mismatchedDefinition = EquipmentDefinition.CreateRuntime("equipment_prevalidation_resolved_mismatch",
                EquipmentSlot.Head, false, 2);
            var malformedAccessory = EquipmentDefinition.CreateRuntime("equipment_prevalidation_bad_accessory",
                EquipmentSlot.Head, true);
            var equipment = new EquipmentSystem();
            equipment.TryEquip(originalHelmet);
            var malformedSlotRejected = !equipment.TryImport(
                new System.Collections.Generic.Dictionary<EquipmentSlot, EquipmentDefinition>
                {
                    [EquipmentSlot.AccessoryOne] = malformedAccessory
                }) && equipment.Get(EquipmentSlot.Head) == originalHelmet;

            var smeltingDefinition = SmeltingDefinition.CreateRuntime("equipment_prevalidation_smelting",
                new ItemAmount { item = input, amount = 1 }, new ItemAmount { item = fuel, amount = 1 },
                new ItemAmount { item = output, amount = 1 }, 2f);
            var smelting = new SmeltingStation(inventory, smeltingDefinition.StationKind,
                smeltingDefinition.BatchCapacity);
            var save = new SaveGame
            {
                inventory = inventory.Export(),
                equipment = new System.Collections.Generic.List<EquipmentRecord>
                {
                    new EquipmentRecord
                    {
                        slot = EquipmentSlot.Head.ToString(),
                        equipmentId = "equipment_prevalidation_requested"
                    }
                },
                smelting = new System.Collections.Generic.List<SmeltingRecord>
                {
                    new SmeltingRecord
                    {
                        stationId = "equipment_prevalidation_furnace",
                        recipeId = smeltingDefinition.Id,
                        remainingSeconds = 1f,
                        isActive = true,
                        queueIndex = -1
                    }
                }
            };

            var restored = ProgressionSaveAdapter.Restore(save, inventory, equipment,
                id => id == "equipment_prevalidation_requested" ? mismatchedDefinition : null,
                "equipment_prevalidation_furnace", smelting,
                id => id == smeltingDefinition.Id ? smeltingDefinition : null, FindItem);
            var stateUnchanged = inventory.Count(marker.Id) == 1 &&
                                 equipment.Get(EquipmentSlot.Head) == originalHelmet && !smelting.IsSmelting &&
                                 smelting.Queue.Count == 0 && smelting.Completed.Count == 0;

            if (malformedSlotRejected && !restored && stateUnchanged)
                Debug.Log("[Nyangbingo] Progression equipment identity, slot, and atomic prevalidation completed.");
            else Debug.LogError("[Nyangbingo] Progression equipment prevalidation test failed.");
        }

        private void TestWorldChestAndTurretSaveRoundTrip()
        {
            var chestSource = new DevBTestChestSource(20);
            var chestProgress = new ChestProgress(gameDataCatalog.FindItem);
            chestProgress.Import(new[] { "chest_03", "chest_17" });
            var tileChanges = new[]
            {
                new TileChangeRecord { x = 3, y = 4, z = 0, tileId = "stone_wall", placed = true },
                new TileChangeRecord { x = -2, y = 7, z = 0, tileId = "dirt", placed = false }
            };
            var placedObjects = new[]
            {
                new PlacedObjectRecord
                {
                    objectId = "turret_01", definitionId = "shingijeon_turret",
                    position = new Vector2(8f, 2f), rotationDegrees = 90f
                }
            };

            var turretObject = new GameObject("TemporarySaveTurret");
            var turret = new TurretController(turretObject.transform, () => System.Array.Empty<Health>(),
                .2f, 1f, 10f, 4, 270f);
            turret.AddFuel(2);
            turret.Tick(123.5f);

            var save = new SaveGame();
            var worldCaptured = WorldSaveAdapter.CaptureWorld(save, tileChanges, placedObjects, chestSource, chestProgress);
            var turretCaptured = WorldSaveAdapter.CaptureTurretFuel(save, "turret_01", turret);
            var loaded = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(save));
            if (loaded != null) loaded.NormalizeAfterLoad();

            var restoredChestProgress = new ChestProgress(gameDataCatalog.FindItem);
            var restoredTurretObject = new GameObject("TemporaryRestoredSaveTurret");
            var restoredTurret = new TurretController(restoredTurretObject.transform, () => System.Array.Empty<Health>(),
                .2f, 1f, 10f, 4, 270f);
            var chestsRestored = WorldSaveAdapter.RestoreChests(loaded, new DevBTestChestSource(20), restoredChestProgress);
            var turretRestored = WorldSaveAdapter.RestoreTurretFuel(loaded, "turret_01", restoredTurret);
            var wrongCountCaptureRejected = !WorldSaveAdapter.CaptureWorld(new SaveGame(), tileChanges, placedObjects,
                new DevBTestChestSource(19), new ChestProgress());
            var wrongCountRestoreRejected = !WorldSaveAdapter.RestoreChests(loaded,
                new DevBTestChestSource(19), new ChestProgress());
            var changedCoordinatesRejected = !WorldSaveAdapter.RestoreChests(loaded,
                new DevBTestChestSource(20, 1f), new ChestProgress());
            var legacyLoaded = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(save));
            legacyLoaded.schemaVersion = SaveGame.CurrentSchemaVersion - 1;
            var legacyChestProgress = new ChestProgress(gameDataCatalog.FindItem);
            var legacyCoordinatesMigrated = WorldSaveAdapter.RestoreChests(legacyLoaded,
                new DevBTestChestSource(20, 1f), legacyChestProgress) &&
                legacyChestProgress.IsOpened("chest_03") && legacyChestProgress.IsOpened("chest_17") &&
                Mathf.Approximately(legacyLoaded.chests[0].position.x,
                    new DevBTestChestSource(20, 1f).GetChestPosition("chest_00").x);

            var worldMatches = loaded != null && loaded.tileChanges.Count == 2 &&
                loaded.tileChanges[0].placed && loaded.tileChanges[0].tileId == "stone_wall" &&
                !loaded.tileChanges[1].placed && loaded.tileChanges[1].x == -2 &&
                loaded.placedObjectRecords.Count == 1 && loaded.placedObjectRecords[0].objectId == "turret_01";
            var chestsMatch = loaded != null && loaded.chests.Count == 20 &&
                restoredChestProgress.IsOpened("chest_03") && restoredChestProgress.IsOpened("chest_17") &&
                !restoredChestProgress.IsOpened("chest_04");
            var turretMatches = Mathf.Approximately(restoredTurret.FuelRemaining, 416.5f);

            if (worldCaptured && turretCaptured && chestsRestored && turretRestored &&
                worldMatches && chestsMatch && turretMatches)
                Debug.Log("[Nyangbingo] World changes, 20 chests, and turret fuel structured save round-trip completed.");
            else Debug.LogError("[Nyangbingo] World, chest, or turret structured save round-trip test failed.");

            if (wrongCountCaptureRejected && wrongCountRestoreRejected && changedCoordinatesRejected &&
                legacyCoordinatesMigrated)
                Debug.Log("[Nyangbingo] Exactly-20 chest count, current-save coordinate validation, and legacy coordinate migration completed.");
            else Debug.LogError("[Nyangbingo] Chest count or deterministic coordinate validation test failed.");

            Destroy(turretObject);
            Destroy(restoredTurretObject);
        }

        private void TestWorldRecordCapturePrevalidation()
        {
            var save = new SaveGame
            {
                tileChanges = new System.Collections.Generic.List<TileChangeRecord>
                {
                    new TileChangeRecord { x = 99, y = 99, z = 0, tileId = "existing_tile", placed = true }
                },
                placedObjectRecords = new System.Collections.Generic.List<PlacedObjectRecord>
                {
                    new PlacedObjectRecord
                    {
                        objectId = "existing_object",
                        definitionId = "existing_definition",
                        position = Vector2.one
                    }
                }
            };
            var orderedTileHistory = new[]
            {
                new TileChangeRecord { x = 1, y = 2, z = 0, tileId = "dirt", placed = true },
                new TileChangeRecord { x = 1, y = 2, z = 0, tileId = "dirt", placed = false }
            };
            var validObjects = new[]
            {
                new PlacedObjectRecord
                {
                    objectId = "valid_object",
                    definitionId = "turret",
                    position = Vector2.zero,
                    rotationDegrees = 0f
                }
            };
            var invalidObjects = new[]
            {
                new PlacedObjectRecord
                {
                    objectId = "invalid_object",
                    definitionId = "turret",
                    position = new Vector2(float.NaN, 0f),
                    rotationDegrees = 0f
                }
            };
            var orderedHistoryAccepted = WorldSaveAdapter.CaptureWorld(new SaveGame(), orderedTileHistory, validObjects,
                new DevBTestChestSource(20), new ChestProgress());
            var rejected = !WorldSaveAdapter.CaptureWorld(save, orderedTileHistory, invalidObjects,
                new DevBTestChestSource(20), new ChestProgress());
            var previousSnapshotPreserved = save.tileChanges.Count == 1 && save.tileChanges[0].x == 99 &&
                                            save.placedObjectRecords.Count == 1 &&
                                            save.placedObjectRecords[0].objectId == "existing_object";
            var invalidLoadedSave = new SaveGame
            {
                tileChanges = new System.Collections.Generic.List<TileChangeRecord>(orderedTileHistory),
                placedObjectRecords = new System.Collections.Generic.List<PlacedObjectRecord>(invalidObjects)
            };
            var loadedRejected = !WorldSaveAdapter.ValidateWorldRecords(invalidLoadedSave);

            if (orderedHistoryAccepted && rejected && previousSnapshotPreserved && loadedRejected)
                Debug.Log("[Nyangbingo] Ordered world tile history and placed-object record prevalidation completed.");
            else Debug.LogError("[Nyangbingo] World record prevalidation test failed.");
        }

        private void TestDuplicateTurretFuelRestoreRejection()
        {
            var turretObject = new GameObject("TemporaryDuplicateFuelTurret");
            var turret = new TurretController(turretObject.transform, () => System.Array.Empty<Health>(),
                .2f, 1f, 10f, 1, 10f);
            turret.AddFuel(1);
            var save = new SaveGame
            {
                turretFuel = new System.Collections.Generic.List<TurretFuelRecord>
                {
                    new TurretFuelRecord
                    {
                        objectId = "duplicate_turret",
                        remainingGameSeconds = 5f,
                        storesGameSeconds = true
                    },
                    new TurretFuelRecord
                    {
                        objectId = "duplicate_turret",
                        remainingGameSeconds = 6f,
                        storesGameSeconds = true
                    }
                }
            };

            var duplicateRejected = !WorldSaveAdapter.RestoreTurretFuel(save, "duplicate_turret", turret);
            var fuelPreserved = Mathf.Approximately(turret.FuelRemaining, 10f);
            var missingRejected = !WorldSaveAdapter.RestoreTurretFuel(save, "missing_turret", turret) &&
                                  Mathf.Approximately(turret.FuelRemaining, 10f);

            if (duplicateRejected && fuelPreserved && missingRejected)
                Debug.Log("[Nyangbingo] Duplicate turret fuel record rejection completed.");
            else Debug.LogError("[Nyangbingo] Duplicate turret fuel restore test failed.");

            Destroy(turretObject);
        }

        private void TestLegacyTurretFuelReplacementRestore()
        {
            var turretObject = new GameObject("TemporaryLegacyFuelTurret");
            var turret = new TurretController(turretObject.transform, () => System.Array.Empty<Health>(),
                .2f, 1f, 10f, 1, 10f);
            turret.AddFuel(1);
            var twoUnits = new SaveGame
            {
                turretFuel = new System.Collections.Generic.List<TurretFuelRecord>
                {
                    new TurretFuelRecord { objectId = "legacy_turret", fuel = 2, storesGameSeconds = false }
                }
            };
            var replaced = WorldSaveAdapter.RestoreTurretFuel(twoUnits, "legacy_turret", turret) &&
                           Mathf.Approximately(turret.FuelRemaining, 20f);

            var zeroUnits = new SaveGame
            {
                turretFuel = new System.Collections.Generic.List<TurretFuelRecord>
                {
                    new TurretFuelRecord { objectId = "legacy_turret", fuel = 0, storesGameSeconds = false }
                }
            };
            var cleared = WorldSaveAdapter.RestoreTurretFuel(zeroUnits, "legacy_turret", turret) &&
                          Mathf.Approximately(turret.FuelRemaining, 0f) && !turret.IsPowered;

            if (replaced && cleared)
                Debug.Log("[Nyangbingo] Legacy turret fuel replacement restore completed.");
            else Debug.LogError("[Nyangbingo] Legacy turret fuel replacement test failed.");

            Destroy(turretObject);
        }

        private void TestPlayerTimeAndBossSaveRoundTrip()
        {
            var playerObject = new GameObject("TemporarySavePlayer");
            playerObject.transform.position = new Vector3(4f, 5f, 0f);
            var playerHealth = playerObject.AddComponent<Health>();
            playerHealth.ConfigureForRuntime(20, 2);
            playerHealth.ApplyDamage(9, DamageTag.Melee);

            var timeSource = gameObject.AddComponent<DevBTestTimeSource>();
            timeSource.Day = 15;
            timeSource.IsNight = true;
            timeSource.TimeOfDayGameSeconds = 222f;
            var spawnController = gameObject.AddComponent<DevBTestSpawnController>();
            var bossManager = gameObject.AddComponent<BossManager>();
            bossManager.ConfigureForRuntime(timeSource, spawnController);
            var summonItem = ItemDefinition.CreateRuntime("save_summon", "Save Summon");
            var bossDefinition = BossDefinition.CreateRuntime("save_boss", YokaiKind.ClubGoblin, summonItem,
                System.Array.Empty<ItemAmount>());
            var bossObject = new GameObject("TemporarySaveBoss");
            bossObject.transform.position = new Vector3(12f, 3f, 0f);
            var bossHealth = bossObject.AddComponent<Health>();
            bossHealth.ConfigureForRuntime(50);
            bossManager.TryStart(bossDefinition, bossHealth, 200f);
            bossHealth.ApplyDamage(17, DamageTag.Melee);

            var save = new SaveGame();
            var captured = PlayerTimeBossSaveAdapter.Capture(save, playerObject.transform, playerHealth, timeSource, bossManager);
            var serialized = JsonUtility.ToJson(save);
            SaveManager.TryDeserialize(serialized, out var loaded);

            var restoredPlayerObject = new GameObject("TemporaryRestoredSavePlayer");
            var restoredPlayerHealth = restoredPlayerObject.AddComponent<Health>();
            restoredPlayerHealth.ConfigureForRuntime(1, 2);
            var restoredTimeSource = gameObject.AddComponent<DevBTestTimeSource>();
            restoredTimeSource.Day = 1;
            restoredTimeSource.IsNight = false;
            var restoredSpawnController = gameObject.AddComponent<DevBTestSpawnController>();
            var restoredBossManager = gameObject.AddComponent<BossManager>();
            restoredBossManager.ConfigureForRuntime(restoredTimeSource, restoredSpawnController);
            try
            {
                var restored = PlayerTimeBossSaveAdapter.Restore(loaded, restoredPlayerObject.transform,
                    restoredPlayerHealth, restoredTimeSource, restoredBossManager);

                var playerMatches = restoredPlayerObject.transform.position == playerObject.transform.position &&
                    restoredPlayerHealth.MaxHealth == 20 && restoredPlayerHealth.Current == 13 && restoredPlayerHealth.Defense == 2;
                var timeMatches = restoredTimeSource.Day == 15 && restoredTimeSource.IsNight &&
                    Mathf.Approximately(restoredTimeSource.TimeOfDayGameSeconds, 222f);
                var bossOmitted = !serialized.Contains("\"activeBoss\"") && !loaded.activeBoss.active &&
                                  !restoredBossManager.IsBossActive && restoredSpawnController.IsRegularSpawning;
                playerHealth.ApplyDamage(int.MaxValue, DamageTag.Melee);
                var deadCaptureRejected = !PlayerTimeBossSaveAdapter.Capture(
                    new SaveGame(), playerObject.transform, playerHealth, timeSource, bossManager);

                if (captured && restored && playerMatches && timeMatches && bossOmitted && deadCaptureRejected)
                    Debug.Log("[Nyangbingo] Player/time save omits active boss and restores without boss spawn completed.");
                else Debug.LogError("[Nyangbingo] Active boss no-serialize capture or restore test failed.");
            }
            finally
            {
                Destroy(playerObject);
                Destroy(bossObject);
                Destroy(restoredPlayerObject);
                Destroy(timeSource);
                Destroy(spawnController);
                Destroy(bossManager);
                Destroy(restoredTimeSource);
                Destroy(restoredSpawnController);
                Destroy(restoredBossManager);
            }
        }

        private void TestPlayerBossSaveInvalidPositionRejection()
        {
            const string legacyJson = "{\"schemaVersion\":8,\"day\":9,\"playerState\":{" +
                "\"hasValue\":true,\"position\":{\"x\":2,\"y\":0,\"z\":0},\"currentHealth\":7,\"maxHealth\":10}," +
                "\"timeState\":{\"hasValue\":true,\"day\":9,\"timeOfDayGameSeconds\":100,\"isNight\":true}," +
                "\"activeBoss\":{\"active\":true,\"bossId\":\"removed_legacy_boss\"," +
                "\"position\":{\"x\":999,\"y\":999,\"z\":0},\"currentHealth\":8,\"maxHealth\":10," +
                "\"summonedAtGameSeconds\":20}}";
            var player = new GameObject("TemporaryLegacyBossSavePlayer");
            var playerHealth = player.AddComponent<Health>();
            playerHealth.ConfigureForRuntime(1);
            var timeSource = gameObject.AddComponent<DevBTestTimeSource>();
            var spawnController = gameObject.AddComponent<DevBTestSpawnController>();
            var bossManager = gameObject.AddComponent<BossManager>();
            bossManager.ConfigureForRuntime(timeSource, spawnController);
            var deserialized = SaveManager.TryDeserialize(legacyJson, out var migrated);
            var restored = deserialized && PlayerTimeBossSaveAdapter.Restore(migrated, player.transform, playerHealth,
                timeSource, bossManager);
            var legacyIgnored = restored && migrated.schemaVersion == SaveGame.CurrentSchemaVersion &&
                                !migrated.activeBoss.active &&
                                !JsonUtility.ToJson(migrated).Contains("\"activeBoss\"") &&
                                !bossManager.IsBossActive && player.transform.position == Vector3.right * 2f &&
                                playerHealth.MaxHealth == 10 && playerHealth.Current == 7 &&
                                timeSource.Day == 9 && timeSource.IsNight;

            if (legacyIgnored)
                Debug.Log("[Nyangbingo] Legacy activeBoss v8 payload ignore and current-schema migration completed.");
            else Debug.LogError("[Nyangbingo] Legacy activeBoss ignore migration test failed.");

            Destroy(player);
            Destroy(timeSource);
            Destroy(spawnController);
            Destroy(bossManager);
        }

        private void TestPlayerBossSaveSpawnFailureRollback()
        {
            var timeSource = gameObject.AddComponent<DevBTestTimeSource>();
            var spawnController = gameObject.AddComponent<DevBTestSpawnController>();
            var bossManager = gameObject.AddComponent<BossManager>();
            bossManager.ConfigureForRuntime(timeSource, spawnController);
            var bossItem = ItemDefinition.CreateRuntime("save_policy_boss_item", "Save Policy Boss Item");
            var bossDefinition = BossDefinition.CreateRuntime("save_policy_boss", BossKind.GoblinChief,
                bossItem, System.Array.Empty<ItemAmount>(), 10);
            var bossObject = new GameObject("TemporarySavePolicyBoss");
            var bossHealth = bossObject.AddComponent<Health>();
            bossHealth.ConfigureForRuntime(10);
            var bossStarted = bossManager.TryStart(bossDefinition, bossHealth);
            var saveManager = gameObject.AddComponent<SaveManager>();
            const int testSlot = 0;
            saveManager.Delete(testSlot);
            var snapshot = new SaveGame { seed = 913 };
            var manualBlocked = !saveManager.TrySaveManual(testSlot, snapshot, bossManager) &&
                                !saveManager.TryLoad(testSlot, out _);
            saveManager.SaveAtDawn(testSlot, snapshot);
            var dawnSaved = saveManager.TryLoad(testSlot, out var dawnLoaded) && dawnLoaded.seed == 913 &&
                            !dawnLoaded.activeBoss.active;
            saveManager.Delete(testSlot);
            bossHealth.ApplyDamage(10, DamageTag.Melee);
            var manualAllowedAfterBoss = !bossManager.IsBossActive &&
                                         saveManager.TrySaveManual(testSlot, snapshot, bossManager) &&
                                         saveManager.TryLoad(testSlot, out var manualLoaded) && manualLoaded.seed == 913;

            if (bossStarted && manualBlocked && dawnSaved && manualAllowedAfterBoss)
                Debug.Log("[Nyangbingo] Boss-active manual save lock and dawn autosave exception completed.");
            else Debug.LogError("[Nyangbingo] Boss save policy entry-point test failed.");

            saveManager.Delete(testSlot);
            Destroy(bossObject);
            Destroy(timeSource);
            Destroy(spawnController);
            Destroy(bossManager);
            Destroy(saveManager);
        }

        private void TestOverlapBoxAttack()
        {
            var attacker = new GameObject("TemporaryOverlapBoxAttacker");
            var attack = attacker.AddComponent<MeleeArcAttack>();
            attack.ConfigureForRuntime(attacker.transform, Physics2D.AllLayers, 2f, 100f, 5, 0f);

            var frontTarget = new GameObject("TemporaryFrontTarget");
            frontTarget.transform.position = Vector3.right;
            frontTarget.AddComponent<BoxCollider2D>();
            var frontHealth = frontTarget.AddComponent<Health>();
            frontHealth.ConfigureForRuntime(5);

            var rearTarget = new GameObject("TemporaryRearTarget");
            rearTarget.transform.position = Vector3.left;
            rearTarget.AddComponent<BoxCollider2D>();
            var rearHealth = rearTarget.AddComponent<Health>();
            rearHealth.ConfigureForRuntime(5);

            Physics2D.SyncTransforms();
            attack.Strike(Vector2.right);
            if (frontHealth.IsDead && rearHealth.Current == 5)
                Debug.Log("[Nyangbingo] OverlapBox forward melee attack completed.");
            else Debug.LogError("[Nyangbingo] OverlapBox forward melee attack test failed.");

            Destroy(attacker);
            Destroy(frontTarget);
            Destroy(rearTarget);
        }

        private void TestOverlapBoxInvalidNumericGuard()
        {
            var attacker = new GameObject("TemporaryOverlapBoxNumericGuardAttacker");
            var attack = attacker.AddComponent<MeleeArcAttack>();
            attack.ConfigureForRuntime(attacker.transform, Physics2D.AllLayers, 2f, 100f, 3, 1f);
            attack.ConfigureForRuntime(attacker.transform, Physics2D.AllLayers, float.NaN,
                float.PositiveInfinity, 3, float.NaN);

            var target = new GameObject("TemporaryOverlapBoxNumericGuardTarget");
            target.transform.position = Vector3.right;
            target.AddComponent<BoxCollider2D>();
            var health = target.AddComponent<Health>();
            health.ConfigureForRuntime(10);

            Physics2D.SyncTransforms();
            attack.Strike(new Vector2(float.NaN, 0f));
            attack.Strike(new Vector2(float.PositiveInfinity, 0f));
            var invalidDirectionsIgnored = health.Current == 10;
            attack.Strike(Vector2.right);
            var previousValidConfigurationPreserved = health.Current == 7;

            if (invalidDirectionsIgnored && previousValidConfigurationPreserved)
                Debug.Log("[Nyangbingo] OverlapBox invalid numeric input guard completed.");
            else Debug.LogError("[Nyangbingo] OverlapBox invalid numeric input guard test failed.");

            Destroy(attacker);
            Destroy(target);
        }

        private void TestDefenseAndDamageDelivery()
        {
            var armoredObject = new GameObject("TemporaryArmoredTarget");
            var armoredHealth = armoredObject.AddComponent<Health>();
            armoredHealth.ConfigureForRuntime(10, 4);
            var reportedDirectDamage = 0;
            armoredHealth.Damaged += (_, amount) => reportedDirectDamage = amount;
            armoredHealth.ApplyDamage(2, DamageTag.Melee);

            var dotObject = new GameObject("TemporaryDotTarget");
            var dotHealth = dotObject.AddComponent<Health>();
            dotHealth.ConfigureForRuntime(10, 4);
            var reportedDotDamage = 0;
            dotHealth.Damaged += (_, amount) => reportedDotDamage = amount;
            dotHealth.ApplyDamage(5, DamageTag.Fire, DamageDelivery.DamageOverTime);

            if (armoredHealth.Current == 9 && reportedDirectDamage == 1 &&
                dotHealth.Current == 5 && reportedDotDamage == 5)
                Debug.Log("[Nyangbingo] Defense minimum damage and DOT bypass completed.");
            else Debug.LogError("[Nyangbingo] Defense or DOT damage test failed.");

            Destroy(armoredObject);
            Destroy(dotObject);
        }

        private void TestCombatInvalidNumericGuard()
        {
            var target = new GameObject("TemporaryCombatNumericGuardTarget");
            var body = target.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            var health = target.AddComponent<Health>();
            health.ConfigureForRuntime(10);
            health.SetDamageTakenMultiplier(.5f);
            health.SetFireDamageMultiplier(.5f);
            health.SetDamageTakenMultiplier(float.NaN);
            health.SetFireDamageMultiplier(float.PositiveInfinity);

            var nanKnockbackRejected = !health.TryApplyKnockback(new Vector2(float.NaN, 1f));
            var infiniteKnockbackRejected = !health.TryApplyKnockback(new Vector2(1f, float.PositiveInfinity));
            health.ApplyDamage(4, DamageTag.Fire);
            var validMultipliersPreserved = health.Current == 9 &&
                                            Mathf.Approximately(health.DamageTakenMultiplier, .5f) &&
                                            Mathf.Approximately(health.FireDamageMultiplier, .5f);
            var velocityUnchanged = body.linearVelocity == Vector2.zero;

            if (nanKnockbackRejected && infiniteKnockbackRejected && validMultipliersPreserved && velocityUnchanged)
                Debug.Log("[Nyangbingo] Combat invalid multiplier and knockback rejection completed.");
            else Debug.LogError("[Nyangbingo] Combat invalid numeric guard test failed.");

            Destroy(target);
        }

        private void TestHealthRuntimeReconfigurationReset()
        {
            var target = new GameObject("TemporaryHealthReconfigurationTarget");
            var health = target.AddComponent<Health>();
            health.ConfigureForRuntime(10, 3);
            health.SetKnockbackImmune(true);
            health.SetDamageTakenMultiplier(.5f);
            health.SetFireDamageMultiplier(.5f);
            health.ApplyDamage(4, DamageTag.Fire);
            health.ConfigureForRuntime(20, 2);

            if (health.MaxHealth == 20 && health.Current == 20 && health.Defense == 2 &&
                !health.IsKnockbackImmune && Mathf.Approximately(health.DamageTakenMultiplier, 1f) &&
                Mathf.Approximately(health.FireDamageMultiplier, 1f))
                Debug.Log("[Nyangbingo] Health runtime reconfiguration transient-state reset completed.");
            else Debug.LogError("[Nyangbingo] Health runtime reconfiguration reset test failed.");

            Destroy(target);
        }

        private void TestWireSnareAbility()
        {
            var attacker = new GameObject("TemporaryWireSnareAttacker");
            var attack = attacker.AddComponent<MeleeArcAttack>();
            attack.ConfigureForRuntime(attacker.transform, Physics2D.AllLayers, 2f, 100f, 99, 99f);
            var ability = new WireSnareAbility(attack);

            var target = new GameObject("TemporaryWireSnareTarget");
            target.transform.position = Vector3.right;
            target.AddComponent<BoxCollider2D>();
            var targetBody = target.AddComponent<Rigidbody2D>();
            targetBody.gravityScale = 0f;
            var targetHealth = target.AddComponent<Health>();
            targetHealth.ConfigureForRuntime(10);

            Physics2D.SyncTransforms();
            var invalidDirectionRejected = !ability.TryUse(new Vector2(float.NaN, 0f)) && ability.IsReady;
            var firstUse = ability.TryUse(Vector2.right);
            var blockedImmediately = !ability.TryUse(Vector2.right);
            ability.Tick(2.9f);
            var blockedEarly = !ability.TryUse(Vector2.right);
            ability.Tick(.1f);
            var secondUse = ability.TryUse(Vector2.right);

            if (invalidDirectionRejected && firstUse && blockedImmediately && blockedEarly && secondUse &&
                targetHealth.Current == 2 && targetBody.linearVelocity.x > 0f)
                Debug.Log("[Nyangbingo] Wire snare game-second cooldown, damage, and knockback completed.");
            else Debug.LogError("[Nyangbingo] Wire snare ability test failed.");

            Destroy(attacker);
            Destroy(target);
        }

        private void TestYagwanggwiTheftRules()
        {
            var definition = YokaiDefinition.CreateRuntime(YokaiKind.Yagwanggwi, 10, 3.5f, 12, 0f,
                new ItemAmount[0], inventoryStealSlots: 1, inventoryStealMaxItems: 10);

            var groundTargetObject = new GameObject("TemporaryGroundLootTarget");
            groundTargetObject.transform.position = Vector3.right * .5f;
            var groundTarget = groundTargetObject.AddComponent<DevBTestYokaiTarget>();
            groundTarget.HasGroundLoot = true;
            var groundThief = new GameObject("TemporaryGroundThief");
            var groundBrain = groundThief.AddComponent<YokaiBrain>();
            groundBrain.ConfigureForRuntime(definition, groundTarget);
            groundBrain.Tick(0f);
            groundBrain.Tick(0f);

            var inventoryTargetObject = new GameObject("TemporaryInventoryTarget");
            inventoryTargetObject.transform.position = Vector3.right * .5f;
            var inventoryTarget = inventoryTargetObject.AddComponent<DevBTestYokaiTarget>();
            var inventoryThief = new GameObject("TemporaryInventoryThief");
            var inventoryBrain = inventoryThief.AddComponent<YokaiBrain>();
            inventoryBrain.ConfigureForRuntime(definition, inventoryTarget);
            inventoryBrain.Tick(0f);
            inventoryBrain.Tick(0f);

            var protectedTargetObject = new GameObject("TemporaryGamtuTarget");
            protectedTargetObject.transform.position = Vector3.right * .5f;
            var protectedTarget = protectedTargetObject.AddComponent<DevBTestYokaiTarget>();
            protectedTarget.IsInventoryTheftBlocked = true;
            var blockedThief = new GameObject("TemporaryBlockedThief");
            var blockedBrain = blockedThief.AddComponent<YokaiBrain>();
            blockedBrain.ConfigureForRuntime(definition, protectedTarget);
            blockedBrain.Tick(0f);
            blockedBrain.Tick(1f);

            if (groundTarget.GroundLootStealCount == 1 && groundTarget.InventoryStealCount == 0 &&
                inventoryTarget.InventoryStealCount == 1 &&
                inventoryTarget.LastInventoryStealSlots == definition.StealSlots &&
                inventoryTarget.LastInventoryStealLimit == definition.StealMaxItems &&
                protectedTarget.InventoryStealCount == 0)
                Debug.Log("[Nyangbingo] Yagwanggwi loot priority and gamtu protection completed.");
            else Debug.LogError("[Nyangbingo] Yagwanggwi theft rule test failed.");

            Destroy(groundTargetObject);
            Destroy(groundThief);
            Destroy(inventoryTargetObject);
            Destroy(inventoryThief);
            Destroy(protectedTargetObject);
            Destroy(blockedThief);
        }

        private void TestImportedYokaiSpawnTracksAndDawnFlee()
        {
            var club = gameDataCatalog != null ? gameDataCatalog.FindYokai("club") : null;
            var eoduksini = gameDataCatalog != null ? gameDataCatalog.FindYokai("eoduksini") : null;
            if (club == null || eoduksini == null)
            {
                Debug.LogError("[Nyangbingo] Imported yokai spawn-track definitions are missing.");
                return;
            }

            var dataMatches = club.SpawnTracks == YokaiSpawnTrack.Raid && club.RaidFleesAtDawn &&
                              eoduksini.SpawnTracks == (YokaiSpawnTrack.Raid | YokaiSpawnTrack.Resident) &&
                              eoduksini.RaidFleesAtDawn &&
                              eoduksini.SupportsSpawnTrack(YokaiSpawnTrack.Raid) &&
                              eoduksini.SupportsSpawnTrack(YokaiSpawnTrack.Resident);
            var targetObject = new GameObject("TemporaryDawnFleeTarget");
            targetObject.transform.position = Vector3.right * .5f;
            var target = targetObject.AddComponent<DevBTestYokaiTarget>();

            var raidObject = new GameObject("TemporaryDawnFleeRaidYokai");
            var raidHealth = raidObject.AddComponent<Health>();
            var raidBrain = raidObject.AddComponent<YokaiBrain>();
            var raidLoot = raidObject.AddComponent<YokaiLoot>();
            raidBrain.ConfigureForRuntime(club, target, instanceSpawnTrack: YokaiSpawnTrack.Raid);
            raidLoot.ConfigureForRuntime(club, new DevBTestLootRandomSource(1f));
            raidBrain.Tick(0f);
            raidBrain.Tick(1f);
            var wallDamageBeforeDawn = target.WallDamageReceived;
            var droppedAmount = 0;
            var killedCount = 0;
            var fleeStartedCount = 0;
            raidLoot.Dropped += (_, amount) => droppedAmount += amount;
            raidBrain.DawnFleeStarted += _ => fleeStartedCount++;
            System.Action<YokaiDefinition> onKilled = _ => killedCount++;
            GameEvents.OnYokaiKilled += onKilled;
            try
            {
                GameEvents.RaiseDawnWarning();
                raidBrain.Tick(2f);
                var raidFledAtHalfSpeedWithoutAttacking = raidBrain.IsDawnFleeing && fleeStartedCount == 1 &&
                    Mathf.Approximately(target.WallDamageReceived, wallDamageBeforeDawn) &&
                    Mathf.Approximately(raidObject.transform.position.x, -2f);
                raidHealth.ApplyDamage(club.HitPoints, DamageTag.Melee);
                var killedWhileFleeingRewarded = raidHealth.IsDead && killedCount == 1 && droppedAmount > 0;

                var residentObject = new GameObject("TemporaryDawnResidentEoduksini");
                var residentBrain = residentObject.AddComponent<YokaiBrain>();
                residentBrain.ConfigureForRuntime(eoduksini, target,
                    instanceSpawnTrack: YokaiSpawnTrack.Resident);
                residentBrain.Tick(0f);
                var wallDamageBeforeResidentDawn = target.WallDamageReceived;
                GameEvents.RaiseDawnWarning();
                residentBrain.Tick(1f);
                var residentStayedAndAttacked = !residentBrain.IsDawnFleeing &&
                    Mathf.Approximately(residentObject.transform.position.x, 0f) &&
                    target.WallDamageReceived > wallDamageBeforeResidentDawn;

                var offscreenObject = new GameObject("TemporaryDawnOffscreenYokai");
                var offscreenBrain = offscreenObject.AddComponent<YokaiBrain>();
                var offscreenLoot = offscreenObject.AddComponent<YokaiLoot>();
                offscreenBrain.ConfigureForRuntime(club, target, instanceSpawnTrack: YokaiSpawnTrack.Raid);
                offscreenLoot.ConfigureForRuntime(club, new DevBTestLootRandomSource(0f));
                var offscreenDropAmount = 0;
                var offscreenFledCount = 0;
                offscreenLoot.Dropped += (_, amount) => offscreenDropAmount += amount;
                offscreenBrain.FledOffscreen += _ => offscreenFledCount++;
                GameEvents.RaiseDawnWarning();
                var offscreenDespawned = offscreenBrain.TryDespawnIfOffscreen(false) &&
                                         offscreenFledCount == 1 && offscreenDropAmount == 0 && killedCount == 1;

                if (dataMatches && raidFledAtHalfSpeedWithoutAttacking && killedWhileFleeingRewarded &&
                    residentStayedAndAttacked && offscreenDespawned)
                    Debug.Log("[Nyangbingo] Yokai raid/resident spawn tracks, dawn flee, and reward boundary completed.");
                else
                    Debug.LogError("[Nyangbingo] Yokai spawn-track or dawn-flee test failed.");

                Destroy(residentObject);
                Destroy(offscreenObject);
            }
            finally
            {
                GameEvents.OnYokaiKilled -= onKilled;
            }

            Destroy(targetObject);
            Destroy(raidObject);
        }

        private void TestYokaiDefinitionHealthInitialization()
        {
            var definition = YokaiDefinition.CreateRuntime(YokaiKind.ClubGoblin, 37, 1f, 1, 1f,
                System.Array.Empty<ItemAmount>());
            var targetObject = new GameObject("TemporaryYokaiHealthTarget");
            targetObject.transform.position = Vector3.right * 10f;
            var target = targetObject.AddComponent<DevBTestYokaiTarget>();
            var yokai = new GameObject("TemporaryYokaiHealthInitialization");
            var brain = yokai.AddComponent<YokaiBrain>();
            var health = yokai.GetComponent<Health>();
            brain.ConfigureForRuntime(definition, target);
            var initializedFromDefinition = health.MaxHealth == 37 && health.Current == 37;
            health.ApplyDamage(7, DamageTag.Melee);
            brain.ConfigureForRuntime(definition, target);
            var resetForReuse = health.MaxHealth == 37 && health.Current == 37 && !health.IsDead;

            if (initializedFromDefinition && resetForReuse)
                Debug.Log("[Nyangbingo] Yokai definition health initialization and reuse reset completed.");
            else Debug.LogError("[Nyangbingo] Yokai definition health initialization test failed.");

            Destroy(targetObject);
            Destroy(yokai);
        }

        private void TestYokaiGameSecondsBinding()
        {
            var definition = YokaiDefinition.CreateRuntime(YokaiKind.ClubGoblin, 10, 2f, 8, 10f, new ItemAmount[0]);
            var targetObject = new GameObject("TemporaryGameSecondsTarget");
            targetObject.transform.position = Vector3.right * 10f;
            var target = targetObject.AddComponent<DevBTestYokaiTarget>();
            var yokaiObject = new GameObject("TemporaryGameSecondsYokai");
            yokaiObject.AddComponent<Health>().ConfigureForRuntime(10);
            var brain = yokaiObject.AddComponent<YokaiBrain>();
            brain.ConfigureForRuntime(definition, target);
            var clock = gameObject.AddComponent<DevBTestGameSecondsSource>();
            clock.GameSeconds = 100f;
            brain.SetGameSecondsSource(clock);

            brain.TickFromGameClock();
            var noInitialCatchUp = yokaiObject.transform.position == Vector3.zero;
            clock.GameSeconds = 103f;
            brain.TickFromGameClock();
            var movedByThreeGameSeconds = Mathf.Approximately(yokaiObject.transform.position.x, 6f);
            brain.TickFromGameClock();
            var pausedWithFrozenClock = Mathf.Approximately(yokaiObject.transform.position.x, 6f);
            clock.GameSeconds = 1f;
            brain.TickFromGameClock();
            var ignoredClockReset = Mathf.Approximately(yokaiObject.transform.position.x, 6f);
            clock.GameSeconds = 3f;
            brain.TickFromGameClock();
            var resumedFromResetClock = Mathf.Approximately(yokaiObject.transform.position.x, 9f);

            if (noInitialCatchUp && movedByThreeGameSeconds && pausedWithFrozenClock &&
                ignoredClockReset && resumedFromResetClock)
                Debug.Log("[Nyangbingo] Yokai AI game-seconds clock binding completed.");
            else Debug.LogError("[Nyangbingo] Yokai AI game-seconds clock binding test failed.");

            Destroy(targetObject);
            Destroy(yokaiObject);
            Destroy(clock);
        }

        private void TestSummonedBossFieldYokaiFreezePolicy()
        {
            var definition = YokaiDefinition.CreateRuntime(YokaiKind.ClubGoblin, 10, 2f, 8, 10f,
                System.Array.Empty<ItemAmount>());
            var targetObject = new GameObject("TemporaryBossFreezeTarget");
            targetObject.transform.position = Vector3.right * 10f;
            var target = targetObject.AddComponent<DevBTestYokaiTarget>();
            var yokaiObject = new GameObject("TemporaryBossFrozenYokai");
            var renderer = yokaiObject.AddComponent<SpriteRenderer>();
            renderer.color = new Color(1f, 1f, 1f, .8f);
            var brain = yokaiObject.AddComponent<YokaiBrain>();
            brain.ConfigureForRuntime(definition, target);
            brain.Tick(1f);
            var beforePause = yokaiObject.transform.position;
            var paused = brain.SetBossEncounterPaused(true);
            brain.Tick(2f);
            var stayedVisibleAndStill = yokaiObject.activeSelf && brain.IsBossEncounterPaused &&
                                        yokaiObject.transform.position == beforePause &&
                                        Mathf.Approximately(renderer.color.a, 0f);
            var resumed = brain.SetBossEncounterPaused(false);
            brain.Tick(1f);
            var resumedSameState = !brain.IsBossEncounterPaused &&
                                   yokaiObject.transform.position.x > beforePause.x &&
                                   Mathf.Approximately(renderer.color.a, .8f);
            var policyMatches = MainGameEncounterCoordinator.ShouldPauseFieldYokaiForBoss(false) &&
                                !MainGameEncounterCoordinator.ShouldPauseFieldYokaiForBoss(true);

            if (paused && stayedVisibleAndStill && resumed && resumedSameState && policyMatches)
                Debug.Log("[Nyangbingo] Summoned bosses freeze visible field yokai and resume them; day-30 invasion bosses coexist without freezing them.");
            else Debug.LogError("[Nyangbingo] Summoned-boss field-yokai freeze policy test failed.");

            Destroy(targetObject);
            Destroy(yokaiObject);
            Destroy(definition);
        }

        private void TestYokaiReconfigurationClockReset()
        {
            var definition = YokaiDefinition.CreateRuntime(YokaiKind.ClubGoblin, 10, 2f, 1, 1f,
                System.Array.Empty<ItemAmount>());
            var targetObject = new GameObject("TemporaryReconfiguredYokaiTarget");
            targetObject.transform.position = Vector3.right * 10f;
            var target = targetObject.AddComponent<DevBTestYokaiTarget>();
            var yokai = new GameObject("TemporaryReconfiguredYokai");
            var brain = yokai.AddComponent<YokaiBrain>();
            var clock = gameObject.AddComponent<DevBTestGameSecondsSource>();
            clock.GameSeconds = 100f;
            brain.SetGameSecondsSource(clock);
            brain.ConfigureForRuntime(definition, target);

            clock.GameSeconds = 200f;
            brain.ConfigureForRuntime(definition, target);
            brain.TickFromGameClock();
            var noInactiveCatchUp = yokai.transform.position == Vector3.zero;
            clock.GameSeconds = 201f;
            brain.TickFromGameClock();
            var resumedFromNewSample = Mathf.Approximately(yokai.transform.position.x, 2f);

            if (noInactiveCatchUp && resumedFromNewSample)
                Debug.Log("[Nyangbingo] Yokai reconfiguration game-clock sample reset completed.");
            else Debug.LogError("[Nyangbingo] Yokai reconfiguration clock reset test failed.");

            Destroy(targetObject);
            Destroy(yokai);
            Destroy(clock);
        }

        private void TestYokaiApproachOvershootGuard()
        {
            var targetObject = new GameObject("TemporaryYokaiOvershootTarget");
            targetObject.transform.position = Vector3.right * 10f;
            var target = targetObject.AddComponent<DevBTestYokaiTarget>();

            var yokaiObject = new GameObject("TemporaryYokaiOvershootGuard");
            var brain = yokaiObject.AddComponent<YokaiBrain>();
            brain.ConfigureForRuntime(
                YokaiDefinition.CreateRuntime(YokaiKind.ClubGoblin, 10, 2f, 1, 1f, System.Array.Empty<ItemAmount>()),
                target);
            brain.Tick(100f);
            var stoppedAtAttackRange = Mathf.Approximately(yokaiObject.transform.position.x, 9f);

            var invalidSpeedObject = new GameObject("TemporaryYokaiInvalidSpeedGuard");
            var invalidSpeedBrain = invalidSpeedObject.AddComponent<YokaiBrain>();
            invalidSpeedBrain.ConfigureForRuntime(
                YokaiDefinition.CreateRuntime(YokaiKind.ClubGoblin, 10, float.NaN, 1, 1f,
                    System.Array.Empty<ItemAmount>()), target);
            invalidSpeedBrain.Tick(1f);
            var invalidSpeedIgnored = invalidSpeedObject.transform.position == Vector3.zero;

            if (stoppedAtAttackRange && invalidSpeedIgnored)
                Debug.Log("[Nyangbingo] Yokai approach overshoot and invalid speed guard completed.");
            else Debug.LogError("[Nyangbingo] Yokai approach movement guard test failed.");

            Destroy(targetObject);
            Destroy(yokaiObject);
            Destroy(invalidSpeedObject);
        }

        private void TestSieveStopTiming()
        {
            var definition = YokaiDefinition.CreateRuntime(YokaiKind.Yagwanggwi, 10, 3.5f, 12, 0f, new ItemAmount[0]);
            var targetObject = new GameObject("TemporarySieveTarget");
            targetObject.transform.position = Vector3.right * 2f;
            var target = targetObject.AddComponent<DevBTestYokaiTarget>();
            target.IsInSieveRange = true;
            target.SieveStopSeconds = 12f;
            target.SieveCooldownSeconds = 30f;

            var yokai = new GameObject("TemporarySieveStoppedYagwanggwi");
            var brain = yokai.AddComponent<YokaiBrain>();
            brain.ConfigureForRuntime(definition, target);
            brain.Tick(0f);
            brain.Tick(11.9f);
            var remainedStoppedEarly = yokai.transform.position.sqrMagnitude < .000001f && brain.SieveStopRemaining > 0f;
            brain.Tick(.1f);
            var stoppedForFullDuration = yokai.transform.position.sqrMagnitude < .000001f && brain.SieveStopRemaining == 0f;
            brain.Tick(1f);
            var movedDuringCooldown = yokai.transform.position != Vector3.zero && brain.SieveCooldownRemaining > 0f;

            yokai.transform.position = Vector3.zero;
            for (var i = 0; i < 17; i++) brain.Tick(1f);
            var reactivatedAfterCooldown = brain.SieveStopRemaining > 0f && brain.SieveCooldownRemaining > 0f;

            if (remainedStoppedEarly && stoppedForFullDuration && movedDuringCooldown && reactivatedAfterCooldown)
                Debug.Log("[Nyangbingo] Sieve 12-second stop and 30-second cooldown completed.");
            else Debug.LogError("[Nyangbingo] Sieve stop timing test failed.");

            Destroy(targetObject);
            Destroy(yokai);
        }

        private void TestSieveDamageMultiplierApplication()
        {
            var definition = YokaiDefinition.CreateRuntime(YokaiKind.Yagwanggwi, 20, 1f, 1, 0f,
                System.Array.Empty<ItemAmount>());
            var targetObject = new GameObject("TemporarySieveDamageTarget");
            targetObject.transform.position = Vector3.right * 10f;
            var target = targetObject.AddComponent<DevBTestYokaiTarget>();
            target.IsInSieveRange = true;
            target.SieveDamageMultiplier = 1.5f;

            var yokai = new GameObject("TemporarySieveDamageYagwanggwi");
            var health = yokai.AddComponent<Health>();
            health.ConfigureForRuntime(20);
            var brain = yokai.AddComponent<YokaiBrain>();
            brain.ConfigureForRuntime(definition, target);
            brain.Tick(0f);
            health.ApplyDamage(4, DamageTag.Melee);
            var appliedInsideSieve = health.Current == 14 &&
                                     Mathf.Approximately(health.DamageTakenMultiplier, 1.5f);

            target.IsInSieveRange = false;
            brain.Tick(0f);
            health.ApplyDamage(4, DamageTag.Melee);
            var restoredOutsideSieve = health.Current == 10 &&
                                       Mathf.Approximately(health.DamageTakenMultiplier, 1f);

            if (appliedInsideSieve && restoredOutsideSieve)
                Debug.Log("[Nyangbingo] Sieve Yagwanggwi damage multiplier enter-exit completed.");
            else Debug.LogError("[Nyangbingo] Sieve damage multiplier application test failed.");

            Destroy(targetObject);
            Destroy(yokai);
        }

        private void TestYokaiTargetReplacementStateReset()
        {
            var definition = YokaiDefinition.CreateRuntime(YokaiKind.Yagwanggwi, 20, 1f, 1, 3f,
                System.Array.Empty<ItemAmount>());
            var firstTargetObject = new GameObject("TemporaryYokaiFirstTarget");
            firstTargetObject.transform.position = Vector3.right * .5f;
            var firstTarget = firstTargetObject.AddComponent<DevBTestYokaiTarget>();
            firstTarget.IsInSieveRange = true;
            firstTarget.SieveDamageMultiplier = 2f;

            var secondTargetObject = new GameObject("TemporaryYokaiReplacementTarget");
            secondTargetObject.transform.position = Vector3.right * 10f;
            var secondTarget = secondTargetObject.AddComponent<DevBTestYokaiTarget>();
            secondTarget.IsInventoryTheftBlocked = true;

            var yokai = new GameObject("TemporaryYokaiTargetReplacement");
            var health = yokai.AddComponent<Health>();
            health.ConfigureForRuntime(20);
            var brain = yokai.AddComponent<YokaiBrain>();
            brain.ConfigureForRuntime(definition, firstTarget);
            brain.Tick(0f);
            var firstCounterApplied = Mathf.Approximately(health.DamageTakenMultiplier, 2f);

            brain.SetTarget(secondTarget);
            brain.Tick(1f);
            var replacementCounterApplied = Mathf.Approximately(health.DamageTakenMultiplier, 1f);
            var approachedReplacement = Mathf.Approximately(yokai.transform.position.x, 1f);
            var didNotAttackFromOldState = Mathf.Approximately(secondTarget.WallDamageReceived, 0f);

            if (firstCounterApplied && replacementCounterApplied && approachedReplacement && didNotAttackFromOldState)
                Debug.Log("[Nyangbingo] Yokai target replacement counter and state reset completed.");
            else Debug.LogError("[Nyangbingo] Yokai target replacement test failed.");

            Destroy(firstTargetObject);
            Destroy(secondTargetObject);
            Destroy(yokai);
        }

        private void TestYokaiAttackRangeRevalidation()
        {
            var definition = YokaiDefinition.CreateRuntime(YokaiKind.ClubGoblin, 10, 1f, 1, 3f,
                System.Array.Empty<ItemAmount>());
            var targetObject = new GameObject("TemporaryMovingWallTarget");
            targetObject.transform.position = Vector3.right * .5f;
            var target = targetObject.AddComponent<DevBTestYokaiTarget>();
            var yokai = new GameObject("TemporaryAttackRangeRevalidationYokai");
            var brain = yokai.AddComponent<YokaiBrain>();
            brain.ConfigureForRuntime(definition, target);
            brain.Tick(0f);

            targetObject.transform.position = Vector3.right * 10f;
            brain.Tick(1f);
            var didNotDamageAtRange = Mathf.Approximately(target.WallDamageReceived, 0f);
            var resumedApproach = Mathf.Approximately(yokai.transform.position.x, 1f);

            if (didNotDamageAtRange && resumedApproach)
                Debug.Log("[Nyangbingo] Yokai wall attack range revalidation completed.");
            else Debug.LogError("[Nyangbingo] Yokai wall attack range revalidation test failed.");

            Destroy(targetObject);
            Destroy(yokai);
        }

        private void TestDeadYokaiStopsActing()
        {
            var definition = YokaiDefinition.CreateRuntime(YokaiKind.ClubGoblin, 10, 2f, 1, 3f,
                System.Array.Empty<ItemAmount>());
            var targetObject = new GameObject("TemporaryDeadYokaiWallTarget");
            targetObject.transform.position = Vector3.right * .5f;
            var target = targetObject.AddComponent<DevBTestYokaiTarget>();
            var yokai = new GameObject("TemporaryDeadYokaiActionGuard");
            var health = yokai.AddComponent<Health>();
            health.ConfigureForRuntime(10);
            var brain = yokai.AddComponent<YokaiBrain>();
            brain.ConfigureForRuntime(definition, target);
            brain.Tick(0f);
            health.ApplyDamage(10, DamageTag.Melee);
            brain.Tick(1f);

            if (health.IsDead && Mathf.Approximately(target.WallDamageReceived, 0f) &&
                yokai.transform.position == Vector3.zero)
                Debug.Log("[Nyangbingo] Dead Yokai immediate AI action stop completed.");
            else Debug.LogError("[Nyangbingo] Dead Yokai AI stop test failed.");

            Destroy(targetObject);
            Destroy(yokai);
        }

        private void TestCounterDurationLargeTickConsumption()
        {
            var definition = YokaiDefinition.CreateRuntime(YokaiKind.Yagwanggwi, 10, 2f, 1, 0f,
                System.Array.Empty<ItemAmount>());
            var targetObject = new GameObject("TemporaryLargeTickSieveTarget");
            targetObject.transform.position = Vector3.right * 10f;
            var target = targetObject.AddComponent<DevBTestYokaiTarget>();
            target.IsInSieveRange = true;
            target.SieveStopSeconds = 12f;
            target.SieveCooldownSeconds = 30f;
            target.SieveDamageMultiplier = float.PositiveInfinity;

            var yokai = new GameObject("TemporaryLargeTickSieveYokai");
            var health = yokai.AddComponent<Health>();
            health.ConfigureForRuntime(10);
            var brain = yokai.AddComponent<YokaiBrain>();
            brain.ConfigureForRuntime(definition, target);
            brain.Tick(20f);

            var consumedOnlyStopDuration = Mathf.Approximately(brain.SieveStopRemaining, 0f) &&
                                           Mathf.Approximately(brain.SieveCooldownRemaining, 10f) &&
                                           Mathf.Approximately(yokai.transform.position.x, 9f);
            var invalidMultiplierFallback = Mathf.Approximately(
                YokaiSpecialRules.DamageTakenMultiplier(definition, target), 1f);

            if (consumedOnlyStopDuration && invalidMultiplierFallback)
                Debug.Log("[Nyangbingo] Yokai counter duration large-tick consumption completed.");
            else Debug.LogError("[Nyangbingo] Yokai counter duration large-tick test failed.");

            Destroy(targetObject);
            Destroy(yokai);
        }

        private void TestEoduksiniLanternReaction()
        {
            var definition = YokaiDefinition.CreateRuntime(YokaiKind.Eoduksini, 20, 2.5f, 14, 24f,
                new ItemAmount[0], noLanternContact: 21, takenMultiplier: 2f,
                takenCondition: YokaiDamageTakenCondition.LanternRadius);
            var targetObject = new GameObject("TemporaryLanternTarget");
            targetObject.transform.position = Vector3.right * 2f;
            var target = targetObject.AddComponent<DevBTestYokaiTarget>();
            target.IsInLanternRange = true;
            target.EoduksiniLanternPauseSeconds = 6f;
            target.EoduksiniBloomCooldownSeconds = 12f;
            target.EoduksiniLanternDamageMultiplier = 9f;

            var yokai = new GameObject("TemporaryEoduksini");
            var brain = yokai.AddComponent<YokaiBrain>();
            var health = yokai.GetComponent<Health>();
            health.ConfigureForRuntime(20);
            var bloomCount = 0;
            brain.Bloomed += () => bloomCount++;
            brain.ConfigureForRuntime(definition, target);
            brain.Tick(0f);
            health.ApplyDamage(3, DamageTag.Melee);
            brain.Tick(5.9f);
            var pausedEarly = yokai.transform.position.sqrMagnitude < .000001f && brain.LanternPauseRemaining > 0f;
            brain.Tick(.1f);
            var pausedForFullDuration = yokai.transform.position.sqrMagnitude < .000001f && brain.LanternPauseRemaining == 0f;
            brain.Tick(1f);
            var movedDuringCooldown = yokai.transform.position.sqrMagnitude > .000001f && brain.BloomCooldownRemaining > 0f;

            yokai.transform.position = Vector3.zero;
            for (var i = 0; i < 5; i++) brain.Tick(1f);
            var reactivatedAfterCooldown = bloomCount == 2 && brain.LanternPauseRemaining > 0f;
            target.IsInLanternRange = false;
            brain.Tick(0f);
            health.ApplyDamage(3, DamageTag.Melee);

            var visualObject = new GameObject("Visual");
            visualObject.transform.SetParent(yokai.transform, false);
            var visualRenderer = visualObject.AddComponent<SpriteRenderer>();
            var presentation = visualObject.AddComponent<RuntimeEoduksiniVisual>();
            presentation.ConfigureForRuntime(brain, visualRenderer);
            presentation.TickPresentation(10f);
            var darkPresentation = Mathf.Approximately(presentation.CurrentScale, 2f) &&
                                   Mathf.Approximately(visualRenderer.color.a, .7f) &&
                                   yokai.transform.localScale == Vector3.one;
            target.IsInLanternRange = true;
            presentation.TickPresentation(10f);
            var lanternPresentation = Mathf.Approximately(presentation.CurrentScale, .6f) &&
                                      Mathf.Approximately(presentation.CurrentBloom, 1f) &&
                                      Mathf.Approximately(visualRenderer.color.a, 1f) &&
                                      yokai.transform.localScale == Vector3.one;

            if (pausedEarly && pausedForFullDuration && movedDuringCooldown && reactivatedAfterCooldown &&
                health.Current == 11 && Mathf.Approximately(health.DamageTakenMultiplier, 1f) &&
                darkPresentation && lanternPresentation)
                Debug.Log("[Nyangbingo] Eoduksini lantern damage, pause, bloom cooldown, and visual response completed.");
            else Debug.LogError("[Nyangbingo] Eoduksini lantern reaction test failed.");

            Destroy(targetObject);
            Destroy(yokai);
        }

        private void TestBulgasariWallRule()
        {
            var definition = YokaiDefinition.CreateRuntime(YokaiKind.Bulgasari, 20, 1f, 10, 16f,
                new ItemAmount[0], wallDpsIce: 32f, wallDpsIronWall: 0f);

            var normalWallObject = new GameObject("TemporaryNormalWall");
            normalWallObject.transform.position = Vector3.right * .5f;
            var normalWall = normalWallObject.AddComponent<DevBTestYokaiTarget>();
            var normalBulgasari = new GameObject("TemporaryNormalWallBulgasari");
            var normalBrain = normalBulgasari.AddComponent<YokaiBrain>();
            normalBrain.ConfigureForRuntime(definition, normalWall);
            normalBrain.Tick(0f);
            normalBrain.Tick(1f);

            var iceWallObject = new GameObject("TemporaryIceWall");
            iceWallObject.transform.position = Vector3.right * .5f;
            var iceWall = iceWallObject.AddComponent<DevBTestYokaiTarget>();
            iceWall.WallMaterial = YokaiWallMaterial.Ice;
            var iceWallBulgasari = new GameObject("TemporaryIceWallBulgasari");
            var iceBrain = iceWallBulgasari.AddComponent<YokaiBrain>();
            iceBrain.ConfigureForRuntime(definition, iceWall);
            iceBrain.Tick(0f);
            iceBrain.Tick(1f);

            var protectedWallObject = new GameObject("TemporaryIronHeatWall");
            protectedWallObject.transform.position = Vector3.right * .5f;
            var protectedWall = protectedWallObject.AddComponent<DevBTestYokaiTarget>();
            protectedWall.WallMaterial = YokaiWallMaterial.IronHeatWall;
            var protectedWallBulgasari = new GameObject("TemporaryProtectedWallBulgasari");
            var protectedBrain = protectedWallBulgasari.AddComponent<YokaiBrain>();
            protectedBrain.ConfigureForRuntime(definition, protectedWall);
            protectedBrain.Tick(0f);
            protectedBrain.Tick(1f);

            if (Mathf.Approximately(normalWall.WallDamageReceived,
                    definition.WallDamageFor(YokaiWallMaterial.Default)) &&
                Mathf.Approximately(iceWall.WallDamageReceived,
                    definition.WallDamageFor(YokaiWallMaterial.Ice)) &&
                Mathf.Approximately(protectedWall.WallDamageReceived,
                    definition.WallDamageFor(YokaiWallMaterial.IronHeatWall)))
                Debug.Log("[Nyangbingo] Bulgasari data-driven default, ice, and iron-wall damage completed.");
            else Debug.LogError("[Nyangbingo] Bulgasari wall rule test failed.");

            Destroy(normalWallObject);
            Destroy(normalBulgasari);
            Destroy(iceWallObject);
            Destroy(iceWallBulgasari);
            Destroy(protectedWallObject);
            Destroy(protectedWallBulgasari);
        }

        private void TestCounterAuraSensor()
        {
            var lanternObject = new GameObject("TemporaryLanternAura");
            var lantern = lanternObject.AddComponent<CounterAura>();
            lantern.ConfigureForRuntime(CounterAuraKind.Lantern, 6f, 2f, 6f, 12f);

            var sieveObject = new GameObject("TemporarySieveAura");
            var sieve = sieveObject.AddComponent<CounterAura>();
            sieve.ConfigureForRuntime(CounterAuraKind.Sieve, 4f, 1.5f, 12f, 30f);

            var observed = new GameObject("TemporaryAuraObservedYokai");
            var sensor = new CounterAuraSensor(observed.transform, new[] { lantern, sieve });
            observed.transform.position = Vector3.right * 5f;
            var lanternOnly = sensor.IsInLanternRange && !sensor.IsInSieveRange &&
                Mathf.Approximately(sensor.EoduksiniLanternDamageMultiplier, 2f);

            observed.transform.position = Vector3.right * 3f;
            var bothAuras = sensor.IsInLanternRange && sensor.IsInSieveRange &&
                Mathf.Approximately(sensor.SieveDamageMultiplier, 1.5f) &&
                Mathf.Approximately(sensor.SieveStopSeconds, 12f) &&
                Mathf.Approximately(sensor.SieveCooldownSeconds, 30f);

            observed.transform.position = Vector3.right * 7f;
            var outsideAuras = !sensor.IsInLanternRange && !sensor.IsInSieveRange;

            if (lanternOnly && bothAuras && outsideAuras)
                Debug.Log("[Nyangbingo] CounterAura lantern and sieve sensor completed.");
            else Debug.LogError("[Nyangbingo] CounterAura sensor test failed.");

            Destroy(lanternObject);
            Destroy(sieveObject);
            Destroy(observed);
        }

        private void TestHaetaeAndBellAuraEffects()
        {
            var haetaeObject = new GameObject("TemporaryHaetaeAura");
            var haetae = haetaeObject.AddComponent<CounterAura>();
            haetae.ConfigureForRuntime(CounterAuraKind.Haetae, 8f, .5f, 0f, 0f);

            var bellObject = new GameObject("TemporaryBellAura");
            var bell = bellObject.AddComponent<CounterAura>();
            bell.ConfigureForRuntime(CounterAuraKind.BellRope, 10f, 0f, 0f, 0f);

            var protectedObject = new GameObject("TemporaryAuraProtectedTarget");
            var health = protectedObject.AddComponent<Health>();
            health.ConfigureForRuntime(20);
            var sensor = new CounterAuraSensor(protectedObject.transform, new[] { haetae, bell });
            var effects = new CounterAuraEffects(sensor, health);
            var alarmCount = 0;
            effects.AlarmRaised += () => alarmCount++;

            protectedObject.transform.position = Vector3.right * 9f;
            effects.Refresh();
            health.ApplyDamage(4, DamageTag.Fire, DamageDelivery.DamageOverTime);
            effects.Refresh();

            protectedObject.transform.position = Vector3.right * 7f;
            effects.Refresh();
            health.ApplyDamage(4, DamageTag.Fire, DamageDelivery.DamageOverTime);
            health.ApplyDamage(4, DamageTag.Melee);

            protectedObject.transform.position = Vector3.right * 11f;
            effects.Refresh();
            protectedObject.transform.position = Vector3.right * 9f;
            effects.Refresh();

            if (health.Current == 10 && alarmCount == 2 && Mathf.Approximately(health.FireDamageMultiplier, 1f))
                Debug.Log("[Nyangbingo] Haetae fire reduction and bell entry alarm completed.");
            else Debug.LogError("[Nyangbingo] Haetae or bell aura effect test failed.");

            Destroy(haetaeObject);
            Destroy(bellObject);
            Destroy(protectedObject);
        }

        private void TestTurretTargetingAndFuel()
        {
            var turretObject = new GameObject("TemporaryTurret");
            var nearTargetObject = new GameObject("TemporaryNearTurretTarget");
            nearTargetObject.transform.position = Vector3.right * 2f;
            var nearHealth = nearTargetObject.AddComponent<Health>();
            nearHealth.ConfigureForRuntime(20);
            var farTargetObject = new GameObject("TemporaryFarTurretTarget");
            farTargetObject.transform.position = Vector3.right * 5f;
            var farHealth = farTargetObject.AddComponent<Health>();
            farHealth.ConfigureForRuntime(20);

            var targets = new System.Collections.Generic.List<Health> { farHealth, nearHealth };
            var providerCallCount = 0;
            var turret = new TurretController(turretObject.transform, () =>
            {
                providerCallCount++;
                return targets;
            }, .2f, 1f, 6f, 3, 270f);
            var firedCount = 0;
            turret.Fired += (target, damage) =>
            {
                firedCount++;
                target.ApplyDamage(damage, DamageTag.Fire);
            };
            var fueled = turret.AddFuel(1);

            turret.Tick(.1f);
            var selectedNearest = turret.CurrentTarget == nearHealth && nearHealth.Current == 17 && firedCount == 1;
            turret.Tick(.1f);
            var heldTargetBeforeRetarget = providerCallCount == 1;
            turret.Tick(.1f);
            var retargetedAtInterval = providerCallCount == 2;

            nearTargetObject.transform.position = Vector3.right * 10f;
            turret.Tick(.01f);
            var replacedInvalidTarget = turret.CurrentTarget == farHealth && providerCallCount == 3;

            targets.Clear();
            turret.Tick(turret.FuelRemaining);
            var fuelExpired = !turret.IsPowered && Mathf.Approximately(turret.FuelRemaining, 0f);

            if (fueled && selectedNearest && heldTargetBeforeRetarget && retargetedAtInterval && replacedInvalidTarget && fuelExpired)
                Debug.Log("[Nyangbingo] Turret targeting, 0.2-second retarget, and game-second fuel completed.");
            else Debug.LogError("[Nyangbingo] Turret targeting or fuel test failed.");

            Destroy(turretObject);
            Destroy(nearTargetObject);
            Destroy(farTargetObject);
        }

        private void TestTurretInvalidConfigurationGuard()
        {
            var turretObject = new GameObject("TemporaryInvalidConfigurationTurret");
            var invalidConfiguration = new TurretController(turretObject.transform,
                () => System.Array.Empty<Health>(), float.NaN, float.PositiveInfinity,
                float.PositiveInfinity, 0, float.NaN);
            var invalidFuelRejected = !invalidConfiguration.AddFuel(1) && !invalidConfiguration.IsPowered &&
                                      Mathf.Approximately(invalidConfiguration.FuelRemaining, 0f);

            var overflowConfiguration = new TurretController(turretObject.transform,
                () => System.Array.Empty<Health>(), .2f, 1f, 6f, 1, float.MaxValue);
            var overflowingFuelRejected = !overflowConfiguration.AddFuel(2) && !overflowConfiguration.IsPowered &&
                                          Mathf.Approximately(overflowConfiguration.FuelRemaining, 0f);
            var invalidRestoreRejected = !overflowConfiguration.RestoreFuelSeconds(-1f) &&
                                         !overflowConfiguration.RestoreFuelSeconds(float.NaN) &&
                                         !overflowConfiguration.RestoreFuelSeconds(float.PositiveInfinity) &&
                                         Mathf.Approximately(overflowConfiguration.FuelRemaining, 0f);

            if (invalidFuelRejected && overflowingFuelRejected && invalidRestoreRejected)
                Debug.Log("[Nyangbingo] Turret invalid configuration and fuel overflow rejection completed.");
            else Debug.LogError("[Nyangbingo] Turret invalid configuration guard test failed.");

            Destroy(turretObject);
        }

        private void TestHomingProjectilePool()
        {
            var targetObject = new GameObject("TemporaryHomingProjectileTarget");
            targetObject.transform.position = Vector3.right * 2f;
            var health = targetObject.AddComponent<Health>();
            health.ConfigureForRuntime(10);

            var pool = new HomingProjectilePool(1);
            var firstProjectile = pool.Spawn(Vector2.zero, health, 4, DamageTag.Fire, 1f, .05f);
            var noEarlyHit = pool.Tick(1f) == 0 && health.Current == 10 && firstProjectile.IsActive;
            var firstHit = pool.Tick(1f) == 1 && health.Current == 6 && !firstProjectile.IsActive;

            var reusedProjectile = pool.Spawn(Vector2.zero, health, 4, DamageTag.Fire, 1f, .05f);
            var reusedFromPool = ReferenceEquals(firstProjectile, reusedProjectile) && pool.CreatedCount == 1;
            var secondHit = pool.Tick(2f) == 1 && health.Current == 2 && pool.ActiveCount == 0;

            if (noEarlyHit && firstHit && reusedFromPool && secondHit)
                Debug.Log("[Nyangbingo] Collider-free homing projectile and pool reuse completed.");
            else Debug.LogError("[Nyangbingo] Homing projectile pool test failed.");

            Destroy(targetObject);
        }

        private void TestHomingProjectileInvalidConfigurationGuard()
        {
            var targetObject = new GameObject("TemporaryInvalidHomingProjectileTarget");
            targetObject.transform.position = Vector3.right * 2f;
            var health = targetObject.AddComponent<Health>();
            health.ConfigureForRuntime(10);
            var pool = new HomingProjectilePool(1);

            var invalidStart = pool.Spawn(new Vector2(float.NaN, 0f), health, 2, DamageTag.Fire, 1f, .1f);
            var invalidSpeed = pool.Spawn(Vector2.zero, health, 2, DamageTag.Fire, float.NaN, .1f);
            var invalidArrival = pool.Spawn(Vector2.zero, health, 2, DamageTag.Fire, 1f, float.PositiveInfinity);
            var invalidLaunchesRejected = !invalidStart.IsActive && !invalidSpeed.IsActive &&
                                          !invalidArrival.IsActive && pool.ActiveCount == 0 && health.Current == 10;

            var moving = pool.Spawn(Vector2.zero, health, 2, DamageTag.Fire, 1f, .1f);
            var poolRecovered = moving.IsActive && pool.Tick(2f) == 1 && !moving.IsActive &&
                                pool.ActiveCount == 0 && health.Current == 8;

            if (invalidLaunchesRejected && poolRecovered)
                Debug.Log("[Nyangbingo] Homing projectile invalid launch rejection and pool recovery completed.");
            else Debug.LogError("[Nyangbingo] Homing projectile invalid configuration guard test failed.");

            Destroy(targetObject);
        }

        private void TestInvalidGameSecondsGuardSweep()
        {
            var attackerObject = new GameObject("TemporaryInvalidTimeAttacker");
            var attack = attackerObject.AddComponent<MeleeArcAttack>();
            attack.ConfigureForRuntime(attackerObject.transform, 0, 1f, 1f, 1, 0f);
            var snare = new WireSnareAbility(attack);
            var snareStarted = snare.TryUse(Vector2.right);
            snare.Tick(-1f);
            snare.Tick(float.NaN);
            snare.Tick(float.PositiveInfinity);
            var snareUnchanged = Mathf.Approximately(snare.RemainingCooldown, 3f);

            var turretObject = new GameObject("TemporaryInvalidTimeTurret");
            var turret = new TurretController(turretObject.transform, () => System.Array.Empty<Health>(),
                .2f, 1f, 10f, 1, 10f);
            turret.AddFuel(1);
            turret.Tick(-1f);
            turret.Tick(float.NaN);
            turret.Tick(float.PositiveInfinity);
            var turretUnchanged = Mathf.Approximately(turret.FuelRemaining, 10f);

            var projectileTargetObject = new GameObject("TemporaryInvalidTimeProjectileTarget");
            projectileTargetObject.transform.position = Vector3.right * 10f;
            var projectileHealth = projectileTargetObject.AddComponent<Health>();
            projectileHealth.ConfigureForRuntime(10);
            var projectilePool = new HomingProjectilePool(1);
            var projectile = projectilePool.Spawn(Vector2.zero, projectileHealth, 1, DamageTag.Fire, 1f, .1f);
            var invalidProjectileHits = projectilePool.Tick(-1f) + projectilePool.Tick(float.NaN) +
                                        projectilePool.Tick(float.PositiveInfinity);
            var projectileUnchanged = invalidProjectileHits == 0 && projectile.IsActive &&
                                      projectile.Position == Vector2.zero && projectileHealth.Current == 10;

            var yokaiTargetObject = new GameObject("TemporaryInvalidTimeYokaiTarget");
            yokaiTargetObject.transform.position = Vector3.right * 10f;
            var yokaiTarget = yokaiTargetObject.AddComponent<DevBTestYokaiTarget>();
            var yokaiObject = new GameObject("TemporaryInvalidTimeYokai");
            var yokaiBrain = yokaiObject.AddComponent<YokaiBrain>();
            yokaiBrain.ConfigureForRuntime(
                YokaiDefinition.CreateRuntime(YokaiKind.ClubGoblin, 10, 2f, 1, 1f, System.Array.Empty<ItemAmount>()),
                yokaiTarget);
            yokaiBrain.Tick(-1f);
            yokaiBrain.Tick(float.NaN);
            yokaiBrain.Tick(float.PositiveInfinity);
            var yokaiUnchanged = yokaiObject.transform.position == Vector3.zero;

            var baekjungUnchanged = false;
            if (importedBaekjungEvent != null)
            {
                var scheduler = new BaekjungScheduler(new[] { importedBaekjungEvent });
                var waveCount = 0;
                scheduler.WaveReady += (_, __) => waveCount++;
                var started = scheduler.TryStartNight(importedBaekjungEvent.Day);
                scheduler.Tick(-1f);
                scheduler.Tick(float.NaN);
                scheduler.Tick(float.PositiveInfinity);
                var state = scheduler.CaptureState();
                baekjungUnchanged = started && waveCount == 1 && state.nextWaveIndex == 1 &&
                                    Mathf.Approximately(state.elapsedGameSeconds, 0f);
            }

            if (snareStarted && snareUnchanged && turretUnchanged && projectileUnchanged &&
                yokaiUnchanged && baekjungUnchanged)
                Debug.Log("[Nyangbingo] Dev B game-seconds NaN, infinity, and negative guard sweep completed.");
            else
                Debug.LogError("[Nyangbingo] Dev B game-seconds invalid input guard test failed.");

            Destroy(attackerObject);
            Destroy(turretObject);
            Destroy(projectileTargetObject);
            Destroy(yokaiTargetObject);
            Destroy(yokaiObject);
        }
    }
}
