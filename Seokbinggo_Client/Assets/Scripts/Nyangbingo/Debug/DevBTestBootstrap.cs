using Nyangbingo.Save;
using Nyangbingo.Core;
using Nyangbingo.Crafting;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using Nyangbingo.Combat;
using Nyangbingo.Yokai;
using Nyangbingo.Bosses;
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
            TestGameDataCatalogInvalidEntryRejection();
            TestImportedBossDefinitions();
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
            TestImportedAccessoryStatsAndTheftProtection();
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
                var bossDefinition = BossDefinition.CreateRuntime("goblin_chief", YokaiKind.ClubGoblin, workbench,
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
            TestYokaiDefinitionHealthInitialization();
            TestYokaiGameSecondsBinding();
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
            var smelting = new SmeltingStation(inventory);
            var smeltingRecipe = SmeltingDefinition.CreateRuntime("test_smelting",
                new ItemAmount { item = wood, amount = 2 }, new ItemAmount { item = stone, amount = 1 },
                new ItemAmount { item = workbench, amount = 1 }, 1f);
            if (smelting.TryStart(smeltingRecipe) && smelting.Tick(1f) && smelting.Completed.Count == 1 && smelting.TryCollect(0) && inventory.Count(workbench.Id) >= 2)
                Debug.Log("[Nyangbingo] Smelting completed.");
            else Debug.LogError("[Nyangbingo] Smelting test failed.");

            var chest = new ChestProgress();
            var chestDefinition = ChestDefinition.CreateRuntime(new[] { new ItemAmount { item = wood, amount = 1 } });
            if (chest.TryOpen("test-chest", chestDefinition) && !chest.TryOpen("test-chest", chestDefinition))
                Debug.Log("[Nyangbingo] Chest single-open protection completed.");
            else Debug.LogError("[Nyangbingo] Chest test failed.");

            var utilities = new UtilityService(); var fanUsed = false;
            utilities.FanUsed += _ => fanUsed = true;
            if (utilities.TryUse(UtilityDefinition.CreateRuntime(UtilityKind.FoldingFan, 3f)) && fanUsed)
                Debug.Log("[Nyangbingo] Utility event completed.");
            else Debug.LogError("[Nyangbingo] Utility test failed.");

            TestImportedUtilities();
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
            System.Action onDayStart = () => dayStartCount++;
            System.Action onNightStart = () => nightStartCount++;
            System.Action onDawnWarning = () => dawnWarningCount++;
            System.Action onSealChanged = () => sealChangedCount++;
            System.Action<Vector3Int> onTilePlaced = position => placedPosition = position;
            System.Action<Vector3Int> onTileBroken = position => brokenPosition = position;
            var expectedPlaced = new Vector3Int(3, -4, 0);
            var expectedBroken = new Vector3Int(-7, 8, 1);

            GameEvents.OnDayStart += onDayStart;
            GameEvents.OnNightStart += onNightStart;
            GameEvents.OnDawnWarning += onDawnWarning;
            GameEvents.OnSealChanged += onSealChanged;
            GameEvents.OnTilePlaced += onTilePlaced;
            GameEvents.OnTileBroken += onTileBroken;
            try
            {
                GameEvents.RaiseDayStart();
                GameEvents.RaiseNightStart();
                GameEvents.RaiseDawnWarning();
                GameEvents.RaiseSealChanged();
                GameEvents.RaiseTilePlaced(expectedPlaced);
                GameEvents.RaiseTileBroken(expectedBroken);
            }
            finally
            {
                GameEvents.OnDayStart -= onDayStart;
                GameEvents.OnNightStart -= onNightStart;
                GameEvents.OnDawnWarning -= onDawnWarning;
                GameEvents.OnSealChanged -= onSealChanged;
                GameEvents.OnTilePlaced -= onTilePlaced;
                GameEvents.OnTileBroken -= onTileBroken;
            }

            GameEvents.RaiseDayStart();
            GameEvents.RaiseNightStart();
            GameEvents.RaiseDawnWarning();
            GameEvents.RaiseSealChanged();
            GameEvents.RaiseTilePlaced(Vector3Int.zero);
            GameEvents.RaiseTileBroken(Vector3Int.zero);

            if (dayStartCount == 1 && nightStartCount == 1 && dawnWarningCount == 1 && sealChangedCount == 1 &&
                placedPosition == expectedPlaced && brokenPosition == expectedBroken)
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

            if (filledFirstSlot && usedSecondSlot && countSaturated && removedLargeAmount && removedRemainder)
                Debug.Log("[Nyangbingo] Inventory large-stack capacity, count overflow, and removal guard completed.");
            else Debug.LogError("[Nyangbingo] Inventory large-stack overflow guard test failed.");
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
                        ValidateCatalogEntries(gameDataCatalog.Smelting, value => value.Id, gameDataCatalog.FindSmelting) &&
                        ValidateCatalogEntries(gameDataCatalog.Equipment, value => value.Id, gameDataCatalog.FindEquipment) &&
                        ValidateCatalogEntries(gameDataCatalog.Utilities, value => value.Id, gameDataCatalog.FindUtility) &&
                        ValidateCatalogEntries(gameDataCatalog.Yokai, value => value.Id, gameDataCatalog.FindYokai) &&
                        ValidateCatalogEntries(gameDataCatalog.Bosses, value => value.Id, gameDataCatalog.FindBoss) &&
                        ValidateCatalogEntries(gameDataCatalog.Chests, value => value.Id, gameDataCatalog.FindChest) &&
                        ValidateCatalogEntries(gameDataCatalog.DayEvents, value => value.Id, gameDataCatalog.FindDayEvent) &&
                        importedTimedRecipe != null && gameDataCatalog.FindRecipe(importedTimedRecipe.Id) == importedTimedRecipe &&
                        importedBaekjungEvent != null && gameDataCatalog.FindDayEvent(importedBaekjungEvent.Id) == importedBaekjungEvent &&
                        importedClubGoblin != null && gameDataCatalog.FindYokai(importedClubGoblin.Id) == importedClubGoblin &&
                        gameDataCatalog.FindItem("__missing__") == null &&
                        gameDataCatalog.FindRecipe("__missing__") == null &&
                        gameDataCatalog.FindSmelting("__missing__") == null &&
                        gameDataCatalog.FindEquipment("__missing__") == null &&
                        gameDataCatalog.FindUtility("__missing__") == null &&
                        gameDataCatalog.FindYokai("__missing__") == null &&
                        gameDataCatalog.FindBoss("__missing__") == null &&
                        gameDataCatalog.FindChest("__missing__") == null &&
                        gameDataCatalog.FindDayEvent("__missing__") == null;

            if (valid)
                Debug.Log($"[Nyangbingo] Game data catalog ID lookup completed: {gameDataCatalog.Items.Count} items, " +
                          $"{gameDataCatalog.Recipes.Count} recipes, {gameDataCatalog.Smelting.Count} smelting, " +
                          $"{gameDataCatalog.Equipment.Count} equipment, {gameDataCatalog.Utilities.Count} utilities, " +
                          $"{gameDataCatalog.Yokai.Count} yokai, {gameDataCatalog.Bosses.Count} bosses, " +
                          $"{gameDataCatalog.Chests.Count} chests, " +
                          $"{gameDataCatalog.DayEvents.Count} day events.");
            else
                Debug.LogError("[Nyangbingo] Game data catalog ID lookup test failed.");
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

            var goblinChief = gameDataCatalog.FindBoss("goblin_chief");
            var motherBulgasari = gameDataCatalog.FindBoss("mother_bulgasari");
            var imugi = gameDataCatalog.FindBoss("imugi");
            var gangcheori = gameDataCatalog.FindBoss("gangcheori");

            var valid = MatchesBossDefinition(goblinChief, BossKind.GoblinChief, 2400, 200f,
                            "wrestling_belt", false, 0, "goblin_fire_essence") &&
                        MatchesBossDefinition(motherBulgasari, BossKind.MotherBulgasari, 3000, 286f,
                            "iron_bait_pile", false, 0, "iron_furnace_heart") &&
                        MatchesBossDefinition(imugi, BossKind.Imugi, 6600, 220f,
                            "ice_altar_offering", true, 0, "yeouiju") &&
                        MatchesBossDefinition(gangcheori, BossKind.Gangcheori, 2000, 48f,
                            "drought_talisman", false, 30, "drought_heart");

            if (valid)
                Debug.Log("[Nyangbingo] Imported boss stats, summon conditions, and guaranteed drops completed.");
            else
                Debug.LogError("[Nyangbingo] Imported boss definition test failed.");
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
            float combatSeconds, string summonItemId, bool requiresDeepAltar, int forcedDay, string dropItemId)
        {
            if (definition == null || definition.Kind != kind || definition.HitPoints != hitPoints ||
                !Mathf.Approximately(definition.ExpectedCombatSeconds, combatSeconds) ||
                definition.SummonItem == null || definition.SummonItem.Id != summonItemId ||
                definition.RequiresDeepAltar != requiresDeepAltar || definition.ForcedDay != forcedDay ||
                definition.GuaranteedDrops == null || definition.GuaranteedDrops.Length != 1)
                return false;

            var drop = definition.GuaranteedDrops[0];
            return drop.item != null && drop.item.Id == dropItemId && drop.amount == 1;
        }

        private void TestBossSummonAndForcedEncounterRules()
        {
            var goblinChief = gameDataCatalog != null ? gameDataCatalog.FindBoss("goblin_chief") : null;
            var imugi = gameDataCatalog != null ? gameDataCatalog.FindBoss("imugi") : null;
            var gangcheori = gameDataCatalog != null ? gameDataCatalog.FindBoss("gangcheori") : null;
            if (goblinChief == null || imugi == null || gangcheori == null)
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
                forcedBinding = new ForcedBossEncounterBinding(gangcheori, timeSource, bossManager, forcedSpawner);
                GameEvents.RaiseNightStart();
                var earlyRejected = forcedSpawner.SpawnCount == 0 && !forcedBinding.HasTriggered;

                timeSource.Day = 30;
                timeSource.IsNight = false;
                GameEvents.RaiseNightStart();
                var daytimeForcedRejected = forcedSpawner.SpawnCount == 0 && !forcedBinding.HasTriggered;

                timeSource.IsNight = true;
                GameEvents.RaiseNightStart();
                var forcedStarted = forcedSpawner.SpawnCount == 1 && forcedBinding.HasTriggered &&
                                    bossManager.ActiveDefinition == gangcheori && !regularSpawner.IsRegularSpawning;
                GameEvents.RaiseNightStart();
                var duplicateRejected = forcedSpawner.SpawnCount == 1;
                forcedSpawner.LastSpawnedHealth.ApplyDamage(gangcheori.HitPoints, DamageTag.Melee);
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
            var gangcheori = gameDataCatalog != null ? gameDataCatalog.FindBoss("gangcheori") : null;
            if (gangcheori == null)
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
                sourceBinding = new ForcedBossEncounterBinding(gangcheori, null, null, null, true);
                var save = new SaveGame();
                ForcedBossEncounterSaveAdapter.Capture(save, gangcheori, sourceBinding);
                var loaded = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(save));
                if (loaded != null) loaded.NormalizeAfterLoad();

                restoredBinding = new ForcedBossEncounterBinding(gangcheori, null, null, null);
                var restored = ForcedBossEncounterSaveAdapter.Restore(loaded, gangcheori, restoredBinding);

                var legacy = JsonUtility.FromJson<SaveGame>(
                    "{\"schemaVersion\":1,\"bossRecords\":[{\"bossId\":\"gangcheori\",\"count\":1,\"firstDay\":30}],\"forcedBossEncounters\":null}");
                if (legacy != null) legacy.NormalizeAfterLoad();
                legacyBinding = new ForcedBossEncounterBinding(gangcheori, null, null, null);
                var legacyRestored = ForcedBossEncounterSaveAdapter.Restore(legacy, gangcheori, legacyBinding);

                var emptyLegacy = JsonUtility.FromJson<SaveGame>(
                    "{\"schemaVersion\":1,\"bossRecords\":[],\"forcedBossEncounters\":null}");
                if (emptyLegacy != null) emptyLegacy.NormalizeAfterLoad();
                emptyLegacyBinding = new ForcedBossEncounterBinding(gangcheori, null, null, null);
                var emptyLegacyRestored = ForcedBossEncounterSaveAdapter.Restore(emptyLegacy, gangcheori, emptyLegacyBinding);

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
            var bossDefinition = gameDataCatalog != null ? gameDataCatalog.FindBoss("goblin_chief") : null;
            if (bossDefinition == null || bossDefinition.GuaranteedDrops == null || bossDefinition.GuaranteedDrops.Length != 1)
            {
                Debug.LogError("[Nyangbingo] Imported boss reward definition is missing.");
                return;
            }

            var reward = bossDefinition.GuaranteedDrops[0];
            var inventory = new Nyangbingo.Inventory.Inventory(gameDataCatalog.FindItem);
            var timeSource = gameObject.AddComponent<DevBTestTimeSource>();
            timeSource.IsNight = true;
            var regularSpawner = gameObject.AddComponent<DevBTestSpawnController>();
            var bossManager = gameObject.AddComponent<BossManager>();
            bossManager.ConfigureForRuntime(timeSource, regularSpawner);
            var receiver = gameObject.AddComponent<BossRewardReceiver>();
            receiver.ConfigureForRuntime(bossManager);

            var receiverEventCount = 0;
            ItemDefinition grantedItem = null;
            var grantedAmount = 0;
            receiver.RewardGranted += (item, amount) =>
            {
                receiverEventCount++;
                grantedItem = item;
                grantedAmount = amount;
            };

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
                var defeatRewarded = defeatedStarted && receiverEventCount == 1 && acquisitionCount == 1 &&
                                     grantedItem == reward.item && grantedAmount == reward.amount &&
                                     inventory.Count(reward.item.Id) == reward.amount &&
                                     !bossManager.IsBossActive && regularSpawner.IsRegularSpawning;

                var fledHealth = fledBossObject.AddComponent<Health>();
                fledHealth.ConfigureForRuntime(bossDefinition.HitPoints);
                var fledStarted = bossManager.TryStart(bossDefinition, fledHealth);
                timeSource.RaiseDawn();
                var fleeNotRewarded = fledStarted && receiverEventCount == 1 && acquisitionCount == 1 &&
                                      inventory.Count(reward.item.Id) == reward.amount &&
                                      !bossManager.IsBossActive && regularSpawner.IsRegularSpawning;

                if (defeatRewarded && fleeNotRewarded)
                    Debug.Log("[Nyangbingo] Imported boss guaranteed reward and dawn-flee exclusion completed.");
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
            var bossDefinition = gameDataCatalog != null ? gameDataCatalog.FindBoss("goblin_chief") : null;
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

        private void TestImportedAccessoryStatsAndTheftProtection()
        {
            if (gameDataCatalog == null)
            {
                Debug.LogError("[Nyangbingo] Imported accessory catalog reference is missing.");
                return;
            }

            var bellCharm = gameDataCatalog.FindEquipment("bell_charm");
            var iceHeart = gameDataCatalog.FindEquipment("ice_heart_charm");
            var luckyPouch = gameDataCatalog.FindEquipment("lucky_pouch");
            var windRibbon = gameDataCatalog.FindEquipment("wind_ribbon");
            var tigerEye = gameDataCatalog.FindEquipment("tiger_eye_orb");
            var goblinHat = gameDataCatalog.FindEquipment("goblin_hat");
            var yagwanggwi = gameDataCatalog.FindYokai("yagwanggwi");
            if (bellCharm == null || iceHeart == null || luckyPouch == null || windRibbon == null ||
                tigerEye == null || goblinHat == null || yagwanggwi == null)
            {
                Debug.LogError("[Nyangbingo] Imported accessory or Yagwanggwi definition is missing.");
                return;
            }

            var definitionsMatch = bellCharm.IsAccessory && bellCharm.GrantsDoubleJump &&
                                   iceHeart.IsAccessory && Mathf.Approximately(iceHeart.TemperatureRiseModifier, -.15f) &&
                                   luckyPouch.IsAccessory && Mathf.Approximately(luckyPouch.MiningCriticalBonus, .1f) &&
                                   windRibbon.IsAccessory && Mathf.Approximately(windRibbon.MovementBonus, .1f) &&
                                   tigerEye.IsAccessory && Mathf.Approximately(tigerEye.VisionRadiusBonus, 2f) &&
                                   goblinHat.IsAccessory && goblinHat.BlocksInventoryTheft;

            var combatEquipment = new EquipmentSystem();
            var windEquipped = combatEquipment.TryEquipAccessory(windRibbon, 0);
            var hatEquipped = combatEquipment.TryEquipAccessory(goblinHat, 1);
            var invalidThirdSlotRejected = !combatEquipment.TryEquipAccessory(tigerEye, 2);
            var combatStats = new StatSheet();
            combatStats.Recalculate(combatEquipment);
            var combatStatsMatch = Mathf.Approximately(combatStats.MovementMultiplier, 1.1f) &&
                                   combatStats.BlocksInventoryTheft && Mathf.Approximately(combatStats.VisionRadiusBonus, 0f);

            var explorationEquipment = new EquipmentSystem();
            explorationEquipment.TryEquipAccessory(tigerEye, 0);
            explorationEquipment.TryEquipAccessory(bellCharm, 1);
            var explorationStats = new StatSheet();
            explorationStats.Recalculate(explorationEquipment);
            var explorationStatsMatch = Mathf.Approximately(explorationStats.VisionRadiusBonus, 2f) &&
                                        explorationStats.HasDoubleJump && !explorationStats.BlocksInventoryTheft;

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

        private void TestEquipmentCollectionSaveRoundTrip()
        {
            var bellCharm = gameDataCatalog != null ? gameDataCatalog.FindEquipment("bell_charm") : null;
            var goblinHat = gameDataCatalog != null ? gameDataCatalog.FindEquipment("goblin_hat") : null;
            var tigerEye = gameDataCatalog != null ? gameDataCatalog.FindEquipment("tiger_eye_orb") : null;
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
            var poolsMatch = MatchesChestPool(ruins, ChestRegion.Ruins, "bell_charm", "wind_ribbon") &&
                             MatchesChestPool(upper, ChestRegion.Upper, "bell_charm", "wind_ribbon", "lucky_pouch") &&
                             MatchesChestPool(middle, ChestRegion.Middle, "lucky_pouch", "tiger_eye_orb", "ice_heart_charm") &&
                             MatchesChestPool(deep, ChestRegion.Deep, "tiger_eye_orb", "ice_heart_charm", "goblin_hat");

            const int worldSeed = 100;
            const string chestId = "chest_deep_00";
            var selected = ChestRewardSelector.SelectEquipment(worldSeed, chestId, deep);
            var repeatedSelection = ChestRewardSelector.SelectEquipment(worldSeed, chestId, deep);
            var deterministic = selected != null && selected == repeatedSelection &&
                                System.Array.IndexOf(deep.EquipmentPool, selected) >= 0;

            var collection = new EquipmentCollection(gameDataCatalog.FindEquipment);
            var acquisitionBinding = new EquipmentAcquisitionBinding(collection);
            var itemRequestCount = 0;
            System.Action<ItemDefinition, int> onItemRequested = (_, __) => itemRequestCount++;
            ItemAcquisition.Requested += onItemRequested;
            try
            {
                var progress = new ChestProgress();
                var opened = progress.TryOpen(chestId, deep, worldSeed);
                var duplicateRejected = !progress.TryOpen(chestId, deep, worldSeed);
                var rewardMatches = opened && duplicateRejected && progress.IsOpened(chestId) &&
                                    collection.Count == 1 && collection.Contains(selected.Id) && itemRequestCount == 0;

                if (poolsMatch && deterministic && rewardMatches)
                    Debug.Log("[Nyangbingo] Imported chest pools and deterministic accessory acquisition completed.");
                else
                    Debug.LogError("[Nyangbingo] Imported chest reward pool test failed.");
            }
            finally
            {
                ItemAcquisition.Requested -= onItemRequested;
                acquisitionBinding.Dispose();
            }
        }

        private static bool MatchesChestPool(ChestDefinition definition, ChestRegion region, params string[] equipmentIds)
        {
            if (definition == null || definition.Region != region || definition.Rewards.Length != 0 ||
                definition.EquipmentPool.Length != equipmentIds.Length) return false;

            var uniqueIds = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
            for (var i = 0; i < equipmentIds.Length; i++)
            {
                var equipment = definition.EquipmentPool[i];
                if (equipment == null || !equipment.IsAccessory || equipment.Id != equipmentIds[i] ||
                    !uniqueIds.Add(equipment.Id)) return false;
            }
            return true;
        }

        private void TestImportedUtilities()
        {
            if (gameDataCatalog == null)
            {
                Debug.LogError("[Nyangbingo] Imported utility catalog reference is missing.");
                return;
            }

            var foldingFan = gameDataCatalog.FindUtility("folding_fan");
            var bellRope = gameDataCatalog.FindUtility("bell_rope");
            var foxRainCharm = gameDataCatalog.FindUtility("fox_rain_charm");
            var foldingFanItem = gameDataCatalog.FindItem("folding_fan");
            var bellRopeItem = gameDataCatalog.FindItem("bell_rope");
            var foxRainCharmItem = gameDataCatalog.FindItem("fox_rain_charm");
            if (foldingFan == null || bellRope == null || foxRainCharm == null ||
                foldingFanItem == null || bellRopeItem == null || foxRainCharmItem == null)
            {
                Debug.LogError("[Nyangbingo] Imported utility or matching item definitions are missing.");
                return;
            }

            var fanValue = -1f;
            var alarmValue = -1f;
            var fireBufferValue = -1f;
            var fanUseCount = 0;
            var fireBufferUseCount = 0;
            var inventory = new Nyangbingo.Inventory.Inventory(gameDataCatalog.FindItem);
            inventory.TryAdd(foldingFanItem.Id, 1);
            inventory.TryAdd(bellRopeItem.Id, 1);
            inventory.TryAdd(foxRainCharmItem.Id, 2);
            var service = new UtilityService(inventory);
            service.FanUsed += value => { fanValue = value; fanUseCount++; };
            service.AlarmPlaced += value => alarmValue = value;
            service.FireBufferActivated += value => { fireBufferValue = value; fireBufferUseCount++; };

            var dataMatches = foldingFan.Kind == UtilityKind.FoldingFan &&
                              Mathf.Approximately(foldingFan.CooldownSeconds, 3f) &&
                              Mathf.Approximately(foldingFan.Value, 3f) && !foldingFan.Consumable &&
                              bellRope.Kind == UtilityKind.BellRope &&
                              Mathf.Approximately(bellRope.CooldownSeconds, 0f) &&
                              Mathf.Approximately(bellRope.Value, 10f) && !bellRope.Consumable &&
                              foxRainCharm.Kind == UtilityKind.FoxRainCharm &&
                              Mathf.Approximately(foxRainCharm.CooldownSeconds, 0f) &&
                              Mathf.Approximately(foxRainCharm.Value, 30f) && foxRainCharm.Consumable;

            var firstFanUse = service.TryUse(foldingFan);
            var immediateFanBlocked = !service.TryUse(foldingFan);
            var bellIndependent = service.TryUse(bellRope);
            service.Tick(2.9f);
            var earlyFanBlocked = !service.TryUse(foldingFan) &&
                                  Mathf.Approximately(service.GetCooldownRemaining(UtilityKind.FoldingFan), .1f);
            service.Tick(.1f);
            var fanReadyAtThreeSeconds = service.TryUse(foldingFan);
            var firstFoxRainUse = service.TryUse(foxRainCharm);
            var secondFoxRainUse = service.TryUse(foxRainCharm);
            var missingFoxRainRejected = !service.TryUse(foxRainCharm);
            var eventsMatch = fanUseCount == 2 && Mathf.Approximately(fanValue, foldingFan.Value) &&
                              Mathf.Approximately(alarmValue, bellRope.Value) &&
                              fireBufferUseCount == 2 && Mathf.Approximately(fireBufferValue, foxRainCharm.Value);
            var cooldownMatches = firstFanUse && immediateFanBlocked && bellIndependent && earlyFanBlocked &&
                                  fanReadyAtThreeSeconds && firstFoxRainUse;
            var inventoryMatches = secondFoxRainUse && missingFoxRainRejected &&
                                   inventory.Count(foldingFanItem.Id) == 1 && inventory.Count(bellRopeItem.Id) == 1 &&
                                   inventory.Count(foxRainCharmItem.Id) == 0;

            if (dataMatches && eventsMatch)
                Debug.Log("[Nyangbingo] Imported utility data lookup and effect events completed.");
            else
                Debug.LogError("[Nyangbingo] Imported utility data or effect event test failed.");

            if (cooldownMatches)
                Debug.Log("[Nyangbingo] Utility independent game-seconds cooldown boundary completed.");
            else
                Debug.LogError("[Nyangbingo] Utility game-seconds cooldown test failed.");

            if (inventoryMatches)
                Debug.Log("[Nyangbingo] Utility inventory ownership and consumable-only removal completed.");
            else
                Debug.LogError("[Nyangbingo] Utility inventory consumption test failed.");
        }

        private void TestUtilityCooldownSaveRoundTrip()
        {
            var foldingFan = gameDataCatalog != null ? gameDataCatalog.FindUtility("folding_fan") : null;
            var foldingFanItem = gameDataCatalog != null ? gameDataCatalog.FindItem("folding_fan") : null;
            if (foldingFan == null || foldingFanItem == null)
            {
                Debug.LogError("[Nyangbingo] Utility cooldown save definitions are missing.");
                return;
            }

            var sourceInventory = new Nyangbingo.Inventory.Inventory(gameDataCatalog.FindItem);
            sourceInventory.TryAdd(foldingFanItem.Id, 1);
            var sourceService = new UtilityService(sourceInventory);
            var started = sourceService.TryUse(foldingFan);
            sourceService.Tick(1.25f);

            var save = new SaveGame();
            var captured = UtilityCooldownSaveAdapter.Capture(save, sourceService);
            var loaded = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(save));
            if (loaded != null) loaded.NormalizeAfterLoad();

            var restoredInventory = new Nyangbingo.Inventory.Inventory(gameDataCatalog.FindItem);
            restoredInventory.TryAdd(foldingFanItem.Id, 1);
            var restoredService = new UtilityService(restoredInventory);
            var restored = UtilityCooldownSaveAdapter.Restore(loaded, restoredService);
            var remainingRestored = Mathf.Approximately(
                restoredService.GetCooldownRemaining(UtilityKind.FoldingFan), 1.75f);
            restoredService.Tick(1.74f);
            var blockedBeforeBoundary = !restoredService.TryUse(foldingFan);
            restoredService.Tick(.01f);
            var readyAtBoundary = restoredService.TryUse(foldingFan) && restoredInventory.Count(foldingFanItem.Id) == 1;

            var corrupt = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(save));
            if (corrupt != null)
            {
                corrupt.NormalizeAfterLoad();
                corrupt.utilityCooldowns[0] = new UtilityCooldownRecord
                {
                    kind = UtilityKind.FoldingFan.ToString(),
                    remainingGameSeconds = -1f
                };
            }
            var corruptRejected = !UtilityCooldownSaveAdapter.Restore(corrupt, sourceService) &&
                                  Mathf.Approximately(sourceService.GetCooldownRemaining(UtilityKind.FoldingFan), 1.75f);

            var legacy = JsonUtility.FromJson<SaveGame>("{\"schemaVersion\":3,\"utilityCooldowns\":null}");
            if (legacy != null) legacy.NormalizeAfterLoad();
            var legacyService = new UtilityService();
            var legacyRestored = UtilityCooldownSaveAdapter.Restore(legacy, legacyService) &&
                                 legacy.schemaVersion == SaveGame.CurrentSchemaVersion &&
                                 Mathf.Approximately(legacyService.GetCooldownRemaining(UtilityKind.FoldingFan), 0f);

            if (started && captured && loaded != null && loaded.schemaVersion == SaveGame.CurrentSchemaVersion &&
                restored && remainingRestored && blockedBeforeBoundary && readyAtBoundary &&
                corruptRejected && legacyRestored)
                Debug.Log("[Nyangbingo] Utility game-seconds cooldown structured save and v3 migration completed.");
            else
                Debug.LogError("[Nyangbingo] Utility cooldown save round-trip test failed.");
        }

        private void TestImportedSmeltingStationRules()
        {
            var smeltIron = gameDataCatalog != null ? gameDataCatalog.FindSmelting("smelt_iron") : null;
            var smeltCopper = gameDataCatalog != null ? gameDataCatalog.FindSmelting("smelt_copper") : null;
            var smeltIceSteel = gameDataCatalog != null ? gameDataCatalog.FindSmelting("smelt_ice_steel") : null;
            if (smeltIron == null || smeltCopper == null || smeltIceSteel == null)
            {
                Debug.LogError("[Nyangbingo] Imported smelting station definitions are missing.");
                return;
            }

            var inventory = new Nyangbingo.Inventory.Inventory(gameDataCatalog.FindItem);
            inventory.TryAdd(smeltIron.Input.item.Id, 20);
            inventory.TryAdd(smeltIceSteel.Input.item.Id, 20);
            inventory.TryAdd(smeltIron.Fuel.item.Id, 20);
            var furnace = new SmeltingStation(inventory, SmeltingStationKind.Furnace);
            var foundry = new SmeltingStation(inventory, SmeltingStationKind.Foundry);

            var definitionsMatch = smeltIron.StationKind == SmeltingStationKind.Furnace &&
                                   smeltCopper.StationKind == SmeltingStationKind.Furnace &&
                                   smeltIceSteel.StationKind == SmeltingStationKind.Foundry;
            var crossStationRejected = !furnace.TryStart(smeltIceSteel) && !foundry.TryStart(smeltIron);

            var furnaceAccepted = true;
            for (var i = 0; i < 6; i++) furnaceAccepted &= furnace.TryStart(smeltIron);
            var furnaceSeventhRejected = !furnace.TryStart(smeltIron);

            var foundryAccepted = true;
            for (var i = 0; i < 4; i++) foundryAccepted &= foundry.TryStart(smeltIceSteel);
            var foundryFifthRejected = !foundry.TryStart(smeltIceSteel);

            var capacitiesMatch = furnace.QueueCapacity == 6 && furnace.IsSmelting && furnace.Queue.Count == 5 &&
                                  foundry.QueueCapacity == 4 && foundry.IsSmelting && foundry.Queue.Count == 3;

            if (definitionsMatch && crossStationRejected && furnaceAccepted && furnaceSeventhRejected &&
                foundryAccepted && foundryFifthRejected && capacitiesMatch)
                Debug.Log("[Nyangbingo] Imported smelting station types and furnace-6/foundry-4 capacities completed.");
            else
                Debug.LogError("[Nyangbingo] Imported smelting station or capacity test failed.");

            var overflowInventory = new Nyangbingo.Inventory.Inventory(gameDataCatalog.FindItem);
            overflowInventory.TryAdd(smeltIron.Input.item.Id, 10);
            overflowInventory.TryAdd(smeltIron.Fuel.item.Id, 10);
            var overflowFurnace = new SmeltingStation(overflowInventory, SmeltingStationKind.Furnace);
            var threeQueued = overflowFurnace.TryStart(smeltIron) && overflowFurnace.TryStart(smeltIron) &&
                              overflowFurnace.TryStart(smeltIron);
            var invalidTicksRejected = !overflowFurnace.Tick(-1f) && !overflowFurnace.Tick(0f) &&
                                       !overflowFurnace.Tick(float.NaN) && !overflowFurnace.Tick(float.PositiveInfinity) &&
                                       Mathf.Approximately(overflowFurnace.RemainingSeconds, 20f) &&
                                       overflowFurnace.Completed.Count == 0;
            var overflowCompleted = overflowFurnace.Tick(45f) && overflowFurnace.Completed.Count == 2 &&
                                    overflowFurnace.Active == smeltIron && overflowFurnace.Queue.Count == 0 &&
                                    Mathf.Approximately(overflowFurnace.RemainingSeconds, 15f);

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
            var station = new SmeltingStation(inventory);

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
            var station = new SmeltingStation(inventory);
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
                    composition.Length == 3 && composition[0].kind == YokaiKind.ClubGoblin && composition[0].amount == 3 &&
                    composition[1].kind == YokaiKind.Bulgasari && composition[1].amount == 2 &&
                    composition[2].kind == YokaiKind.Yagwanggwi && composition[2].amount == 7 &&
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
            try
            {
                var started = scheduler.TryStartNight(importedBaekjungEvent.Day);
                controller.DefeatAll();
                scheduler.Tick(150f);
                controller.DefeatAll();
                scheduler.Tick(150f);

                var allWavesMatch = controller.Records.Count == 36;
                for (var waveIndex = 0; waveIndex < 3; waveIndex++)
                    allWavesMatch &= controller.Count(YokaiKind.ClubGoblin, waveIndex) == 3 &&
                        controller.Count(YokaiKind.Bulgasari, waveIndex) == 2 &&
                        controller.Count(YokaiKind.Yagwanggwi, waveIndex) == 7;

                capController.SeedActive(10);
                var cappedStarted = capScheduler.TryStartNight(importedBaekjungEvent.Day);
                var maxActiveRespected = capController.ActiveCount == importedBaekjungEvent.MaxActive &&
                    capController.Records.Count == 2;

                if (started && scheduler.IsScheduleComplete && allWavesMatch && cappedStarted && maxActiveRespected)
                    Debug.Log("[Nyangbingo] Baekjung wave composition and max-active spawn requests completed.");
                else Debug.LogError("[Nyangbingo] Baekjung wave spawn request test failed.");
            }
            finally
            {
                waveSpawner.Dispose();
                cappedWaveSpawner.Dispose();
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
            var endedCount = 0;
            System.Action<DayEventDefinition> onEnded = _ => endedCount++;
            scheduler.Ended += onEnded;
            try
            {
                var wrongDayRejected = !scheduler.TryStartNight(importedBaekjungEvent.Day - 1) &&
                    regularSpawnController.IsRegularSpawning;
                var started = scheduler.TryStartNight(importedBaekjungEvent.Day);
                var pausedAtStart = !regularSpawnController.IsRegularSpawning && scheduler.IsActive;
                var ended = scheduler.TryEndAtDawn();
                var resumedAtDawn = regularSpawnController.IsRegularSpawning && scheduler.HasEnded && !scheduler.IsActive;
                var duplicateEndRejected = !scheduler.TryEndAtDawn();
                scheduler.Tick(300f);
                var noWavesAfterDawn = scheduler.DispatchedWaveCount == 1;

                if (wrongDayRejected && started && pausedAtStart && ended && resumedAtDawn && duplicateEndRejected &&
                    endedCount == 1 && noWavesAfterDawn)
                    Debug.Log("[Nyangbingo] Baekjung regular spawn pause and dawn resume completed.");
                else Debug.LogError("[Nyangbingo] Baekjung regular spawn gate test failed.");
            }
            finally
            {
                scheduler.Ended -= onEnded;
                gate.Dispose();
                Destroy(regularSpawnController);
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

            if (importedClubGoblin.Id == "club_goblin" && importedClubGoblin.TearDrop == 1 &&
                Mathf.Approximately(importedClubGoblin.SignatureChance, .25f) &&
                tearCount == 3 && signatureCount == 1 && unexpectedDropCount == 0 && random.CallCount == 2)
                Debug.Log("[Nyangbingo] Imported yokai loot and Baekjung reward flow completed.");
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
                new ItemAmount { item = ingot, amount = 1 }, 1f);
            var smelting = new SmeltingStation(inventory);
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
            var restoredSmelting = new SmeltingStation(restoredInventory);
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
            var smelting = new SmeltingStation(inventory);
            var smeltingDefinition = SmeltingDefinition.CreateRuntime("progression_prevalidation_smelting",
                new ItemAmount { item = input, amount = 1 }, new ItemAmount { item = fuel, amount = 1 },
                new ItemAmount { item = output, amount = 1 }, 2f);
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
            var smelting = new SmeltingStation(inventory);
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
            var chestProgress = new ChestProgress();
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

            var restoredChestProgress = new ChestProgress();
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

            if (wrongCountCaptureRejected && wrongCountRestoreRejected && changedCoordinatesRejected)
                Debug.Log("[Nyangbingo] Exactly-20 chest count and deterministic coordinate validation completed.");
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
            var duplicateTiles = new[]
            {
                new TileChangeRecord { x = 1, y = 2, z = 0, tileId = "stone", placed = true },
                new TileChangeRecord { x = 1, y = 2, z = 0, tileId = "dirt", placed = false }
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
            var rejected = !WorldSaveAdapter.CaptureWorld(save, duplicateTiles, invalidObjects,
                new DevBTestChestSource(20), new ChestProgress());
            var previousSnapshotPreserved = save.tileChanges.Count == 1 && save.tileChanges[0].x == 99 &&
                                            save.placedObjectRecords.Count == 1 &&
                                            save.placedObjectRecords[0].objectId == "existing_object";
            var invalidLoadedSave = new SaveGame
            {
                tileChanges = new System.Collections.Generic.List<TileChangeRecord>(duplicateTiles),
                placedObjectRecords = new System.Collections.Generic.List<PlacedObjectRecord>(invalidObjects)
            };
            var loadedRejected = !WorldSaveAdapter.ValidateWorldRecords(invalidLoadedSave);

            if (rejected && previousSnapshotPreserved && loadedRejected)
                Debug.Log("[Nyangbingo] World tile and placed-object record prevalidation completed.");
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
            var loaded = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(save));
            if (loaded != null) loaded.NormalizeAfterLoad();

            var restoredPlayerObject = new GameObject("TemporaryRestoredSavePlayer");
            var restoredPlayerHealth = restoredPlayerObject.AddComponent<Health>();
            restoredPlayerHealth.ConfigureForRuntime(1, 2);
            var restoredTimeSource = gameObject.AddComponent<DevBTestTimeSource>();
            restoredTimeSource.Day = 1;
            restoredTimeSource.IsNight = false;
            var restoredSpawnController = gameObject.AddComponent<DevBTestSpawnController>();
            var restoredBossManager = gameObject.AddComponent<BossManager>();
            restoredBossManager.ConfigureForRuntime(restoredTimeSource, restoredSpawnController);
            Health restoredBossHealth = null;
            var summonedEventCount = 0;
            var defeatedEventCount = 0;
            System.Action<BossDefinition> onSummoned = _ => summonedEventCount++;
            System.Action<BossDefinition> onDefeated = _ => defeatedEventCount++;
            GameEvents.OnBossSummoned += onSummoned;
            GameEvents.OnBossDefeated += onDefeated;
            try
            {
                var restored = PlayerTimeBossSaveAdapter.Restore(loaded, restoredPlayerObject.transform,
                    restoredPlayerHealth, restoredTimeSource, restoredBossManager,
                    id => id == bossDefinition.Id ? bossDefinition : null,
                    (_, maxHealth) =>
                    {
                        var restoredBossObject = new GameObject("TemporaryRestoredSaveBoss");
                        restoredBossHealth = restoredBossObject.AddComponent<Health>();
                        restoredBossHealth.ConfigureForRuntime(maxHealth);
                        return restoredBossHealth;
                    });

                var playerMatches = restoredPlayerObject.transform.position == playerObject.transform.position &&
                    restoredPlayerHealth.MaxHealth == 20 && restoredPlayerHealth.Current == 13 && restoredPlayerHealth.Defense == 2;
                var timeMatches = restoredTimeSource.Day == 15 && restoredTimeSource.IsNight &&
                    Mathf.Approximately(restoredTimeSource.TimeOfDayGameSeconds, 222f);
                var bossMatches = restoredBossManager.IsBossActive && restoredBossHealth != null &&
                    restoredBossHealth.MaxHealth == 50 && restoredBossHealth.Current == 33 &&
                    restoredBossHealth.transform.position == bossObject.transform.position &&
                    restoredBossHealth.IsKnockbackImmune && !restoredSpawnController.IsRegularSpawning &&
                    Mathf.Approximately(restoredBossManager.ActiveSummonedAtGameSeconds, 200f);
                if (restoredBossHealth != null) restoredBossHealth.ApplyDamage(restoredBossHealth.Current, DamageTag.Melee);
                var defeatFlowMatches = !restoredBossManager.IsBossActive && restoredSpawnController.IsRegularSpawning &&
                    defeatedEventCount == 1;

                if (captured && restored && playerMatches && timeMatches && bossMatches &&
                    summonedEventCount == 0 && defeatFlowMatches)
                    Debug.Log("[Nyangbingo] Player, time, and active boss structured save round-trip completed.");
                else Debug.LogError("[Nyangbingo] Player, time, or boss structured save round-trip test failed.");
            }
            finally
            {
                GameEvents.OnBossSummoned -= onSummoned;
                GameEvents.OnBossDefeated -= onDefeated;
                Destroy(playerObject);
                Destroy(bossObject);
                Destroy(restoredPlayerObject);
                if (restoredBossHealth != null) Destroy(restoredBossHealth.gameObject);
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
            var player = new GameObject("TemporaryInvalidPositionSavePlayer");
            player.transform.position = Vector3.right * 5f;
            var playerHealth = player.AddComponent<Health>();
            playerHealth.ConfigureForRuntime(10);
            var timeSource = gameObject.AddComponent<DevBTestTimeSource>();
            timeSource.Day = 3;
            timeSource.TimeOfDayGameSeconds = 40f;
            timeSource.IsNight = false;
            var spawnController = gameObject.AddComponent<DevBTestSpawnController>();
            var bossManager = gameObject.AddComponent<BossManager>();
            bossManager.ConfigureForRuntime(timeSource, spawnController);
            var bossItem = ItemDefinition.CreateRuntime("invalid_position_boss_item", "Invalid Position Boss Item");
            var bossDefinition = BossDefinition.CreateRuntime("invalid_position_boss", BossKind.GoblinChief,
                bossItem, System.Array.Empty<ItemAmount>(), 10);

            var save = new SaveGame
            {
                playerState = new PlayerStateRecord
                {
                    hasValue = true,
                    position = Vector3.right * 2f,
                    currentHealth = 10,
                    maxHealth = 10
                },
                timeState = new TimeStateRecord
                {
                    hasValue = true,
                    day = 9,
                    timeOfDayGameSeconds = 100f,
                    isNight = true
                },
                activeBoss = new ActiveBossStateRecord
                {
                    active = true,
                    bossId = bossDefinition.Id,
                    position = new Vector3(float.PositiveInfinity, 0f, 0f),
                    currentHealth = 10,
                    maxHealth = 10,
                    summonedAtGameSeconds = 20f
                }
            };
            var spawnCalls = 0;
            var rejected = !PlayerTimeBossSaveAdapter.Restore(save, player.transform, playerHealth, timeSource,
                bossManager, id => id == bossDefinition.Id ? bossDefinition : null, (_, __) =>
                {
                    spawnCalls++;
                    return null;
                });
            var stateUnchanged = player.transform.position == Vector3.right * 5f && timeSource.Day == 3 &&
                                 Mathf.Approximately(timeSource.TimeOfDayGameSeconds, 40f) &&
                                 !timeSource.IsNight && spawnCalls == 0 && !bossManager.IsBossActive;

            if (rejected && stateUnchanged)
                Debug.Log("[Nyangbingo] Player and boss invalid save-position prevalidation completed.");
            else Debug.LogError("[Nyangbingo] Player or boss invalid save-position validation test failed.");

            Destroy(player);
            Destroy(timeSource);
            Destroy(spawnController);
            Destroy(bossManager);
        }

        private void TestPlayerBossSaveSpawnFailureRollback()
        {
            var player = new GameObject("TemporaryBossSpawnFailurePlayer");
            player.transform.position = Vector3.right * 5f;
            var playerHealth = player.AddComponent<Health>();
            playerHealth.ConfigureForRuntime(20, 3);
            playerHealth.ApplyDamage(4, DamageTag.Melee);
            playerHealth.SetDamageTakenMultiplier(.5f);
            playerHealth.SetFireDamageMultiplier(.75f);
            playerHealth.SetKnockbackImmune(true);
            var timeSource = gameObject.AddComponent<DevBTestTimeSource>();
            timeSource.Day = 3;
            timeSource.TimeOfDayGameSeconds = 40f;
            timeSource.IsNight = false;
            var spawnController = gameObject.AddComponent<DevBTestSpawnController>();
            var bossManager = gameObject.AddComponent<BossManager>();
            bossManager.ConfigureForRuntime(timeSource, spawnController);
            var bossItem = ItemDefinition.CreateRuntime("spawn_failure_boss_item", "Spawn Failure Boss Item");
            var bossDefinition = BossDefinition.CreateRuntime("spawn_failure_boss", BossKind.GoblinChief,
                bossItem, System.Array.Empty<ItemAmount>(), 10);
            var save = new SaveGame
            {
                playerState = new PlayerStateRecord
                {
                    hasValue = true,
                    position = Vector3.right * 2f,
                    currentHealth = 7,
                    maxHealth = 10
                },
                timeState = new TimeStateRecord
                {
                    hasValue = true,
                    day = 9,
                    timeOfDayGameSeconds = 100f,
                    isNight = true
                },
                activeBoss = new ActiveBossStateRecord
                {
                    active = true,
                    bossId = bossDefinition.Id,
                    position = Vector3.right * 8f,
                    currentHealth = 8,
                    maxHealth = 10,
                    summonedAtGameSeconds = 20f
                }
            };

            var rejected = !PlayerTimeBossSaveAdapter.Restore(save, player.transform, playerHealth, timeSource,
                bossManager, id => id == bossDefinition.Id ? bossDefinition : null, (_, __) => null);
            var playerRolledBack = player.transform.position == Vector3.right * 5f &&
                                   playerHealth.MaxHealth == 20 && playerHealth.Current == 19 &&
                                   playerHealth.Defense == 3 && playerHealth.IsKnockbackImmune &&
                                   Mathf.Approximately(playerHealth.DamageTakenMultiplier, .5f) &&
                                   Mathf.Approximately(playerHealth.FireDamageMultiplier, .75f);
            var timeRolledBack = timeSource.Day == 3 && Mathf.Approximately(timeSource.TimeOfDayGameSeconds, 40f) &&
                                 !timeSource.IsNight && !bossManager.IsBossActive;

            if (rejected && playerRolledBack && timeRolledBack)
                Debug.Log("[Nyangbingo] Player and time rollback after boss spawn failure completed.");
            else Debug.LogError("[Nyangbingo] Boss spawn failure rollback test failed.");

            Destroy(player);
            Destroy(timeSource);
            Destroy(spawnController);
            Destroy(bossManager);
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
            var definition = YokaiDefinition.CreateRuntime(YokaiKind.Yagwanggwi, 10, 3.5f, 12, 0f, new ItemAmount[0]);

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
                inventoryTarget.InventoryStealCount == 1 && inventoryTarget.LastInventoryStealLimit == 10 &&
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
                YokaiSpecialRules.DamageTakenMultiplier(YokaiKind.Yagwanggwi, target), 1f);

            if (consumedOnlyStopDuration && invalidMultiplierFallback)
                Debug.Log("[Nyangbingo] Yokai counter duration large-tick consumption completed.");
            else Debug.LogError("[Nyangbingo] Yokai counter duration large-tick test failed.");

            Destroy(targetObject);
            Destroy(yokai);
        }

        private void TestEoduksiniLanternReaction()
        {
            var definition = YokaiDefinition.CreateRuntime(YokaiKind.Eoduksini, 20, 2.5f, 14, 24f, new ItemAmount[0]);
            var targetObject = new GameObject("TemporaryLanternTarget");
            targetObject.transform.position = Vector3.right * 2f;
            var target = targetObject.AddComponent<DevBTestYokaiTarget>();
            target.IsInLanternRange = true;
            target.EoduksiniLanternPauseSeconds = 6f;
            target.EoduksiniBloomCooldownSeconds = 12f;
            target.EoduksiniLanternDamageMultiplier = 2f;

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

            if (pausedEarly && pausedForFullDuration && movedDuringCooldown && reactivatedAfterCooldown &&
                health.Current == 11 && Mathf.Approximately(health.DamageTakenMultiplier, 1f))
                Debug.Log("[Nyangbingo] Eoduksini lantern damage, pause, and bloom cooldown completed.");
            else Debug.LogError("[Nyangbingo] Eoduksini lantern reaction test failed.");

            Destroy(targetObject);
            Destroy(yokai);
        }

        private void TestBulgasariWallRule()
        {
            var definition = YokaiDefinition.CreateRuntime(YokaiKind.Bulgasari, 20, 1f, 10, 16f, new ItemAmount[0]);

            var normalWallObject = new GameObject("TemporaryNormalWall");
            normalWallObject.transform.position = Vector3.right * .5f;
            var normalWall = normalWallObject.AddComponent<DevBTestYokaiTarget>();
            var normalBulgasari = new GameObject("TemporaryNormalWallBulgasari");
            var normalBrain = normalBulgasari.AddComponent<YokaiBrain>();
            normalBrain.ConfigureForRuntime(definition, normalWall);
            normalBrain.Tick(0f);
            normalBrain.Tick(1f);

            var protectedWallObject = new GameObject("TemporaryIronHeatWall");
            protectedWallObject.transform.position = Vector3.right * .5f;
            var protectedWall = protectedWallObject.AddComponent<DevBTestYokaiTarget>();
            protectedWall.IsIronHeatWall = true;
            var protectedWallBulgasari = new GameObject("TemporaryProtectedWallBulgasari");
            var protectedBrain = protectedWallBulgasari.AddComponent<YokaiBrain>();
            protectedBrain.ConfigureForRuntime(definition, protectedWall);
            protectedBrain.Tick(0f);
            protectedBrain.Tick(1f);

            if (Mathf.Approximately(normalWall.WallDamageReceived, 16f) &&
                Mathf.Approximately(protectedWall.WallDamageReceived, 0f))
                Debug.Log("[Nyangbingo] Bulgasari normal-wall damage and iron-wall protection completed.");
            else Debug.LogError("[Nyangbingo] Bulgasari wall rule test failed.");

            Destroy(normalWallObject);
            Destroy(normalBulgasari);
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
