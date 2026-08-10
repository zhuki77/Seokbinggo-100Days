using System;
using System.Collections.Generic;
using System.Linq;
using Nyangbingo.World;
using UnityEngine;

namespace Nyangbingo.Save
{
    /// <summary>
    /// 메인 세션의 통합 저장 공급자. 기존 검증된 개별 SaveAdapter를 한 순서로 조립하고 같은 공급자를
    /// 수동 저장·로드와 새벽 자동 저장이 공유한다. 플레이어·보스 객체는 B-05 배선 후 추가한다.
    /// </summary>
    [DefaultExecutionOrder(-70)]
    [RequireComponent(typeof(SaveManager), typeof(DawnAutoSave))]
    public sealed class MainGameSaveCoordinator : MonoBehaviour, ISaveSnapshotProvider
    {
        public const string FurnaceStationId = "furnace";
        public const string FoundryStationId = "blast_furnace";

        [SerializeField] private MainGameBootstrap bootstrap;
        [SerializeField] private MainGameRuntimeServices runtimeServices;
        [SerializeField] private MainGameEnvironmentState environmentState;
        [SerializeField] private MainGameTurretRuntime turretRuntime;
        [SerializeField] private MainGameEncounterCoordinator encounterCoordinator;
        [SerializeField] private MainGameWorldDropRuntime worldDropRuntime;
        [SerializeField] private MainGameWorldDecorationRenderer worldDecorationRenderer;
        [SerializeField] private DayNightService timeService;
        [SerializeField] private SaveManager saveManager;
        [SerializeField] private DawnAutoSave dawnAutoSave;
        [Range(0, SaveManager.SlotCount - 1)][SerializeField] private int autoSaveSlot;
        [SerializeField] private bool validateRoundTripInEditor = true;
        private MainGameProgressTracker progressTracker;

        public bool IsInitialized { get; private set; }
        public MainGameProgressTracker ProgressTracker => progressTracker;

        public void ConfigureForScene(
            MainGameBootstrap mainBootstrap,
            MainGameRuntimeServices services,
            MainGameEnvironmentState environment,
            MainGameTurretRuntime turrets,
            MainGameEncounterCoordinator encounters,
            DayNightService clock,
            SaveManager manager,
            DawnAutoSave autoSave,
            int slot)
        {
            bootstrap = mainBootstrap;
            runtimeServices = services;
            environmentState = environment;
            turretRuntime = turrets;
            encounterCoordinator = encounters;
            timeService = clock;
            saveManager = manager;
            dawnAutoSave = autoSave;
            autoSaveSlot = Mathf.Clamp(slot, 0, SaveManager.SlotCount - 1);
        }

        private void Start()
        {
            Initialize();
        }

        public bool Initialize()
        {
            if (IsInitialized) return true;
            bootstrap ??= GetComponent<MainGameBootstrap>();
            runtimeServices ??= GetComponent<MainGameRuntimeServices>();
            environmentState ??= GetComponent<MainGameEnvironmentState>();
            turretRuntime ??= GetComponent<MainGameTurretRuntime>();
            encounterCoordinator ??= GetComponent<MainGameEncounterCoordinator>();
            worldDropRuntime ??= FindAnyObjectByType<MainGameWorldDropRuntime>();
            worldDecorationRenderer ??= GetComponent<MainGameWorldDecorationRenderer>();
            timeService ??= GetComponent<DayNightService>();
            saveManager ??= GetComponent<SaveManager>();
            dawnAutoSave ??= GetComponent<DawnAutoSave>();

            if (bootstrap == null || runtimeServices == null || environmentState == null || turretRuntime == null ||
                encounterCoordinator == null || worldDecorationRenderer == null ||
                timeService == null || saveManager == null || dawnAutoSave == null ||
                !bootstrap.InitializeServices() || !runtimeServices.Initialize() || !environmentState.Initialize() ||
                !encounterCoordinator.Initialize())
            {
                Debug.LogError("[Nyangbingo] MainGameSaveCoordinator: 통합 저장 필수 서비스가 준비되지 않았습니다.");
                return false;
            }

            progressTracker = new MainGameProgressTracker(GetCatalog(), timeService,
                encounterCoordinator.BossManager);
            dawnAutoSave.Configure(saveManager, timeService, this, autoSaveSlot);
            IsInitialized = true;
            var startupSnapshot = CaptureSnapshot();
            if (startupSnapshot == null)
            {
                IsInitialized = false;
                Debug.LogError("[Nyangbingo] MainGameSaveCoordinator: 시작 시 통합 스냅샷 캡처 검증에 실패했습니다.");
                return false;
            }
#if UNITY_EDITOR
            if (validateRoundTripInEditor && !ValidateInMemoryRoundTrip(startupSnapshot))
            {
                IsInitialized = false;
                Debug.LogError("[Nyangbingo] MainGameSaveCoordinator: 에디터 메모리 내 저장·복원 왕복 검증에 실패했습니다.");
                return false;
            }
#endif
            Debug.Log($"[Nyangbingo] MainGameSaveCoordinator: 월드·시간·인벤토리·장비·제작·제련·" +
                      $"유틸리티·설치물 통합 스냅샷과 새벽 자동 저장(슬롯 {autoSaveSlot}) 연결 완료 " +
                      $"(schema={startupSnapshot.schemaVersion}, day={startupSnapshot.day}, " +
                      $"inventorySlots={startupSnapshot.inventory.Count}, smeltingStations=2, " +
                      $"pendingRewards={startupSnapshot.pendingItemAcquisitions.Count}, " +
                      $"codexEntries={startupSnapshot.dogam.Count}, bossRecords={startupSnapshot.bossRecords.Count}, " +
                      $"minedTiles={startupSnapshot.stats.minedTiles}, deaths={startupSnapshot.stats.deaths}, " +
                      $"player={startupSnapshot.playerState.hasValue}, " +
                      $"forcedBosses={startupSnapshot.forcedBossEncounters.Count}, " +
                      $"baekjung={startupSnapshot.baekjungProgress != null})." );
            return true;
        }

        private bool ValidateInMemoryRoundTrip(SaveGame before)
        {
            if (before == null) return false;
            var beforeJson = JsonUtility.ToJson(before);
            // Round-trip validation must reproduce the captured snapshot byte-for-byte. The
            // startup player may not have completed its first physics placement yet, so applying
            // the product safe-spawn repair here would intentionally change the snapshot and make
            // an otherwise valid serialization round trip fail.
            if (!ApplySnapshot(before, false))
            {
                Debug.LogError("[Nyangbingo] MainGameSaveCoordinator: editor round-trip apply stage failed.");
                return false;
            }
            var after = CaptureSnapshot();
            if (after == null)
            {
                Debug.LogError("[Nyangbingo] MainGameSaveCoordinator: editor round-trip recapture stage failed.");
                return false;
            }
            var afterJson = JsonUtility.ToJson(after);
            if (beforeJson != afterJson)
            {
                var difference = FirstDifferenceIndex(beforeJson, afterJson);
                Debug.LogError($"[Nyangbingo] MainGameSaveCoordinator: editor round-trip JSON mismatch " +
                               $"(index={difference}, before={JsonContext(beforeJson, difference)}, " +
                               $"after={JsonContext(afterJson, difference)}).");
                return false;
            }
            Debug.Log("[Nyangbingo] MainGameSaveCoordinator: 파일 쓰기 없는 통합 저장·복원 왕복 검증 완료.");
            return true;
        }

        private static int FirstDifferenceIndex(string left, string right)
        {
            var count = Math.Min(left?.Length ?? 0, right?.Length ?? 0);
            for (var index = 0; index < count; index++)
                if (left[index] != right[index]) return index;
            return count;
        }

        private static string JsonContext(string json, int index)
        {
            if (string.IsNullOrEmpty(json)) return "<empty>";
            var start = Mathf.Clamp(index - 40, 0, json.Length);
            var length = Mathf.Min(100, json.Length - start);
            return json.Substring(start, length);
        }

        public SaveGame CaptureSnapshot()
        {
            if (!IsInitialized && !Initialize()) return CaptureFailed("initialization");
            var save = new SaveGame();
            if (!bootstrap.Session.CaptureSnapshot(save)) return CaptureFailed("world session");
            save.doorStates = bootstrap.TileService.ExportDoorStates();

            save.placedObjectRecords = environmentState.ExportPlacedObjects();
            var catalog = GetCatalog();
            save.modulesDone = catalog.Modules
                .Where(module => module != null && module.Item != null &&
                                 !SeokbinggoRules.IsUpgradeModuleId(module.Id) &&
                                 save.placedObjectRecords.Any(record =>
                                     record.definitionId == module.Item.Id))
                .Select(module => module.Id)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
            save.seokbinggoStage = runtimeServices.Seokbinggo?.Stage ?? 0;
            save.altarClears = runtimeServices.FrostSpread?.AltarClears ?? 0;
            save.gimmickWeaponsGranted = runtimeServices.GimmickWeapons?.Export() ?? new List<string>();
            save.frostPendingCells = runtimeServices.FrostSpread?.ExportPendingCells() ?? new List<string>();
            save.coolingSources = environmentState.ExportCoolingSources();
            save.jangdokStorages = runtimeServices.JangdokStorage.Export();
            save.deathTearPouches = runtimeServices.DeathTearPouches.Export();
            save.worldDrops = ResolveWorldDropRuntime()?.Export() ?? new List<WorldDropStateRecord>();
            if (runtimeServices.MagpieCompanion != null &&
                !runtimeServices.MagpieCompanion.Capture(save))
                return CaptureFailed("magpie companion");
            save.catnipPatches = worldDecorationRenderer.ExportCatnipPatches();
            save.hempPatches = worldDecorationRenderer.ExportHempPatches();
            save.harvestedTrees = worldDecorationRenderer.ExportHarvestedTrees();
            save.harvestedRebar = worldDecorationRenderer.ExportHarvestedRebar();
            save.sealPct = Mathf.Clamp01(bootstrap.SealSystem.SealPercent) * 100f;
            if (!turretRuntime.CaptureProgress(save)) return CaptureFailed("turret progress");
            if (!PlayerTimeBossSaveAdapter.Capture(save, encounterCoordinator.PlayerTransform,
                    encounterCoordinator.PlayerHealth, timeService, encounterCoordinator.BossManager))
                return CaptureFailed("player, time, and boss state");
            save.playerState.hasTemperature = true;
            save.playerState.temperature = runtimeServices.PlayerTemperature.Current;

            ProgressionSaveAdapter.Capture(save, runtimeServices.PlayerInventory,
                runtimeServices.EquipmentSystem, FurnaceStationId, runtimeServices.Furnace);
            ProgressionSaveAdapter.Capture(save, runtimeServices.PlayerInventory,
                runtimeServices.EquipmentSystem, FoundryStationId, runtimeServices.Foundry);

            if (!EquipmentCollectionSaveAdapter.Capture(save, runtimeServices.EquipmentCollection))
                return CaptureFailed("equipment collection");
            if (!ActiveSlotSaveAdapter.Capture(save, runtimeServices.ActiveSlot))
                return CaptureFailed("active slot");
            if (!PortableLanternSaveAdapter.Capture(save, runtimeServices.PortableLantern))
                return CaptureFailed("portable lantern");
            if (!RecipeBookSaveAdapter.Capture(save, runtimeServices.RecipeBook))
                return CaptureFailed("recipe book");
            if (!CraftingProcessSaveAdapter.Capture(save, runtimeServices.CraftingProcess))
                return CaptureFailed("crafting process");
            if (!UtilityCooldownSaveAdapter.Capture(save, runtimeServices.UtilityService))
                return CaptureFailed("utility cooldown");
            if (!PendingItemAcquisitionSaveAdapter.Capture(save, runtimeServices.InventoryRuntime))
                return CaptureFailed("pending item acquisition");
            if (!progressTracker.CaptureTo(save))
                return CaptureFailed("progress tracker");
            if (!encounterCoordinator.CaptureProgress(save))
                return CaptureFailed("encounter progress");

            save.NormalizeAfterLoad();
            return save;
        }

        private static SaveGame CaptureFailed(string stage)
        {
            Debug.LogError($"[Nyangbingo] MainGameSaveCoordinator: save capture failed at stage '{stage}'.");
            return null;
        }

        public bool SaveNow(int slot)
        {
            if (!IsInitialized && !Initialize()) return false;
            var snapshot = CaptureSnapshot();
            if (snapshot == null || slot < 0 || slot >= SaveManager.SlotCount) return false;
            saveManager.Save(slot, snapshot);
            return true;
        }

        public bool TryLoad(int slot)
        {
            if (!IsInitialized && !Initialize()) return false;
            if (slot < 0 || slot >= SaveManager.SlotCount || !saveManager.TryLoad(slot, out var save)) return false;
            return TryApplySnapshot(save);
        }

        public bool TryApplySnapshot(SaveGame save)
        {
            return TryApplySnapshotInternal(save, false);
        }

        public bool TryApplyDemoSnapshot(SaveGame save)
        {
            if (!TryApplySnapshotInternal(save, true)) return false;
            // The title flow copies the official demo into the autosave slot before applying it.
            // Persist the recalculated surface position so Continue cannot restore the stale,
            // generator-version-dependent coordinate if the player quits before dawn autosave.
            saveManager.Save(autoSaveSlot, save);
            return true;
        }

        private bool TryApplySnapshotInternal(SaveGame save, bool forceSafeSurfaceSpawn)
        {
            if (save == null || (!IsInitialized && !Initialize())) return false;
            var rollback = CaptureSnapshot();
            if (rollback == null) return false;
            if (ApplySnapshot(save, forceSafeSurfaceSpawn)) return true;
            // A rollback must restore the exact captured state, not reinterpret its position.
            ApplySnapshot(rollback, false);
            return false;
        }

        private bool ApplySnapshot(SaveGame save, bool forceSafeSurfaceSpawn)
        {
            if (save == null || !encounterCoordinator.BeginRestore()) return false;
            var succeeded = false;
            try
            {
                save.NormalizeAfterLoad();
                runtimeServices.BindPlayerHealth(encounterCoordinator.PlayerHealth);
                runtimeServices.PlayerHealthRecovery?.ResetAfterRestore();
                succeeded = RestoreStage("time state", () => save.timeState.hasValue) &&
                RestoreStage("world session", () => bootstrap.Session.LoadSnapshot(save)) &&
                RestoreStage("door states", () =>
                    bootstrap.TileService.RestoreDoorStates(save.doorStates)) &&
                RestoreStage("player spawn", () => PreparePlayerSpawnForRestore(save, forceSafeSurfaceSpawn)) &&
                RestoreStage("player/time/boss", () => PlayerTimeBossSaveAdapter.Restore(
                    save, encounterCoordinator.PlayerTransform,
                    encounterCoordinator.PlayerHealth, timeService, encounterCoordinator.BossManager)) &&
                RestoreStage("player transient state", ResetPlayerTransientState) &&
                RestoreStage("furnace progression", () => ProgressionSaveAdapter.Restore(
                    save, runtimeServices.PlayerInventory,
                    runtimeServices.EquipmentSystem, FindEquipment,
                    FurnaceStationId, runtimeServices.Furnace, FindSmelting, FindItem)) &&
                RestoreStage("foundry progression", () => ProgressionSaveAdapter.Restore(
                    save, runtimeServices.PlayerInventory,
                    runtimeServices.EquipmentSystem, FindEquipment,
                    FoundryStationId, runtimeServices.Foundry, FindSmelting, FindItem)) &&
                RestoreStage("equipment collection", () =>
                    EquipmentCollectionSaveAdapter.Restore(save, runtimeServices.EquipmentCollection)) &&
                RestoreStage("active slot", () =>
                    ActiveSlotSaveAdapter.Restore(save, runtimeServices.ActiveSlot)) &&
                RestoreStage("portable lantern", () =>
                    PortableLanternSaveAdapter.Restore(save, runtimeServices.PortableLantern)) &&
                RestoreStage("recipe book", () =>
                    RecipeBookSaveAdapter.Restore(save, runtimeServices.RecipeBook, FindRecipe)) &&
                RestoreStage("recipe progression", () => RestoreRecipeProgression(save)) &&
                RestoreStage("crafting process", () =>
                    CraftingProcessSaveAdapter.Restore(save, runtimeServices.CraftingProcess, FindRecipe)) &&
                RestoreStage("utility cooldowns", () =>
                    UtilityCooldownSaveAdapter.Restore(save, runtimeServices.UtilityService)) &&
                RestoreStage("pending item acquisitions", () =>
                    PendingItemAcquisitionSaveAdapter.Restore(save, runtimeServices.InventoryRuntime, FindItem)) &&
                RestoreStage("progress tracker", () => progressTracker.RestoreFrom(save)) &&
                RestoreStage("player temperature", () => !save.playerState.hasTemperature ||
                    runtimeServices.PlayerTemperature.Restore(save.playerState.temperature)) &&
                RestoreStage("death tear pouches", () =>
                    runtimeServices.DeathTearPouches.Restore(save.deathTearPouches)) &&
                RestoreStage("world drops", () => RestoreWorldDrops(save.worldDrops)) &&
                RestoreStage("seokbinggo stage", () => RestoreSeokbinggoStage(save)) &&
                RestoreStage("catnip patches", () =>
                    worldDecorationRenderer.RestoreCatnipPatches(save.catnipPatches)) &&
                RestoreStage("hemp patches", () =>
                    worldDecorationRenderer.RestoreHempPatches(save.hempPatches)) &&
                RestoreStage("harvested trees", () =>
                    worldDecorationRenderer.RestoreHarvestedTrees(save.harvestedTrees)) &&
                RestoreStage("harvested rebar", () =>
                    worldDecorationRenderer.RestoreHarvestedRebar(save.harvestedRebar)) &&
                RestoreStage("encounters", () => encounterCoordinator.RestoreProgress(save)) &&
                RestoreStage("placed objects", () =>
                    environmentState.TryRestorePlacedObjects(save.placedObjectRecords, save.coolingSources)) &&
                RestoreStage("magpie companion", () =>
                    runtimeServices.MagpieCompanion == null ||
                    runtimeServices.MagpieCompanion.Restore(save)) &&
                RestoreStage("jangdok storages", () =>
                    runtimeServices.JangdokStorage.TryRestore(save.jangdokStorages,
                     save.placedObjectRecords
                         .Where(record => record.definitionId == Nyangbingo.Inventory.JangdokStorageRuntime.DefinitionId)
                         .Select(record => record.objectId))) &&
                RestoreStage("turrets", () => turretRuntime.RestoreProgress(save));
                return succeeded;
            }
            finally
            {
                encounterCoordinator.EndRestore(succeeded);
            }
        }

        private static bool RestoreStage(string stage, Func<bool> restore)
        {
            var succeeded = restore != null && restore();
            if (!succeeded)
                Debug.LogError($"[Nyangbingo] Save restore failed at stage '{stage}'.");
            return succeeded;
        }

        public static bool ShouldResolveSafePlayerSpawn(bool forceSafeSurfaceSpawn,
            bool savedPositionIsSafe) => forceSafeSurfaceSpawn;

        private bool PreparePlayerSpawnForRestore(SaveGame save, bool forceSafeSurfaceSpawn)
        {
            var resolver = bootstrap?.Session?.SafeSpawnResolver;
            var player = encounterCoordinator?.PlayerTransform;
            if (save?.playerState == null || !save.playerState.hasValue || resolver == null || player == null)
                return false;

            // A regular save contains the authoritative player position. "Standing safely" is
            // intentionally stricter than "a valid saved position": a player can save while
            // airborne, on a slope, beside a mined tile, or at a collider sub-pixel offset.
            // Reinterpreting those valid coordinates as unsafe used to replace them with the
            // generated world's initial surface spawn. Only imported demo snapshots require
            // generator-version-dependent surface repair.
            if (!forceSafeSurfaceSpawn)
            {
                Debug.Log($"[Nyangbingo] Save restore preserving exact player position " +
                          $"({save.playerState.position}).");
                return true;
            }

            var halfExtent = .38f;
            var circle = player.GetComponent<CircleCollider2D>();
            if (circle != null)
                halfExtent = Mathf.Max(.05f, circle.radius * Mathf.Abs(player.lossyScale.y));

            var savedPosition = (Vector2)save.playerState.position;
            if (!ShouldResolveSafePlayerSpawn(forceSafeSurfaceSpawn,
                    resolver.IsSafeStandingPosition(savedPosition, halfExtent)))
                return true;

            var generatedSpawn = bootstrap.Session.LastResult.spawnPoint;
            if (!resolver.TryResolveSafeSurfaceSpawn(generatedSpawn.x, halfExtent, out var safeSpawn))
                return false;

            save.playerState.position = new Vector3(safeSpawn.x, safeSpawn.y, 0f);
            Debug.Log($"[Nyangbingo] Save restore safe surface spawn applied " +
                      $"(forced={forceSafeSurfaceSpawn}, generated={generatedSpawn}, player={safeSpawn}).");
            return true;
        }

        private Nyangbingo.Data.ItemDefinition FindItem(string id) =>
            GetCatalog()?.FindItem(id);

        private MainGameWorldDropRuntime ResolveWorldDropRuntime()
        {
            worldDropRuntime ??= FindAnyObjectByType<MainGameWorldDropRuntime>();
            return worldDropRuntime;
        }

        private bool RestoreWorldDrops(IReadOnlyList<WorldDropStateRecord> records)
        {
            var runtime = ResolveWorldDropRuntime();
            return runtime != null ? runtime.Restore(records, FindItem) : records != null && records.Count == 0;
        }

        private bool RestoreSeokbinggoStage(SaveGame save)
        {
            if (runtimeServices?.Seokbinggo == null) return false;
            runtimeServices.Seokbinggo.RestoreStage(save?.seokbinggoStage ?? 0);

            var frost = runtimeServices.FrostSpread;
            if (frost != null && save != null)
            {
                frost.SetAltarClears(save.altarClears);
                frost.RestorePendingCells(save.frostPendingCells);
                var tiles = bootstrap?.TileService;
                if (tiles != null)
                {
                    tiles.FrostSpread = frost;
                    if (save.altarClears >= 3)
                        FrostSpreadService.UnsealBedrockLayer(tiles);
                }
            }

            runtimeServices.GimmickWeapons?.Restore(save?.gimmickWeaponsGranted);
            return true;
        }

        private bool ResetPlayerTransientState()
        {
            var player = encounterCoordinator?.PlayerTransform;
            var controller = player != null ? player.GetComponent<MainGamePlayerController>() : null;
            return controller == null || controller.ResetTransientStateAfterSaveRestore();
        }
        private Nyangbingo.Data.EquipmentDefinition FindEquipment(string id) =>
            GetCatalog()?.FindEquipment(id);
        private Nyangbingo.Data.RecipeDefinition FindRecipe(string id) =>
            GetCatalog()?.FindRecipe(id);

        private bool RestoreRecipeProgression(SaveGame save)
        {
            if (save?.dogam == null || runtimeServices?.RecipeBook == null) return false;
            for (var index = 0; index < save.dogam.Count; index++)
            {
                var record = save.dogam[index];
                if (record.kills <= 0) continue;
                var yokai = GetCatalog()?.FindYokai(record.yokaiId);
                if (yokai == null || yokai.Kind != Nyangbingo.Core.YokaiKind.Gangcheori) continue;
                var recipe = FindRecipe(Nyangbingo.Crafting.RecipeUnlockPolicy.GangcheoriUnlockRecipeId);
                if (recipe == null) return false;
                runtimeServices.RecipeBook.Unlock(recipe.Id);
                break;
            }
            return true;
        }

        private Nyangbingo.Data.SmeltingDefinition FindSmelting(string id) =>
            GetCatalog()?.FindSmelting(id);

        private Nyangbingo.Data.GameDataCatalog GetCatalog() =>
            bootstrap != null ? bootstrap.GameDataCatalog : null;

        private void OnDestroy()
        {
            progressTracker?.Dispose();
            progressTracker = null;
            IsInitialized = false;
        }
    }
}
