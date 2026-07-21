using System;
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
            timeService ??= GetComponent<DayNightService>();
            saveManager ??= GetComponent<SaveManager>();
            dawnAutoSave ??= GetComponent<DawnAutoSave>();

            if (bootstrap == null || runtimeServices == null || environmentState == null || turretRuntime == null ||
                encounterCoordinator == null ||
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
            if (!ApplySnapshot(before, false, false)) return false;
            var after = CaptureSnapshot();
            if (after == null || beforeJson != JsonUtility.ToJson(after)) return false;
            Debug.Log("[Nyangbingo] MainGameSaveCoordinator: 파일 쓰기 없는 통합 저장·복원 왕복 검증 완료.");
            return true;
        }

        public SaveGame CaptureSnapshot()
        {
            if (!IsInitialized && !Initialize()) return null;
            var save = new SaveGame();
            if (!bootstrap.Session.CaptureSnapshot(save)) return null;

            save.placedObjectRecords = environmentState.ExportPlacedObjects();
            var catalog = GetCatalog();
            save.modulesDone = catalog.Modules
                .Where(module => module != null && module.Item != null &&
                                 save.placedObjectRecords.Any(record =>
                                     record.definitionId == module.Item.Id))
                .Select(module => module.Id)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
            save.coolingSources = environmentState.ExportCoolingSources();
            save.jangdokStorages = runtimeServices.JangdokStorage.Export();
            save.deathTearPouches = runtimeServices.DeathTearPouches.Export();
            if (!turretRuntime.CaptureProgress(save)) return null;
            if (!PlayerTimeBossSaveAdapter.Capture(save, encounterCoordinator.PlayerTransform,
                    encounterCoordinator.PlayerHealth, timeService, encounterCoordinator.BossManager))
                return null;
            save.playerState.hasTemperature = true;
            save.playerState.temperature = runtimeServices.PlayerTemperature.Current;

            ProgressionSaveAdapter.Capture(save, runtimeServices.PlayerInventory,
                runtimeServices.EquipmentSystem, FurnaceStationId, runtimeServices.Furnace);
            ProgressionSaveAdapter.Capture(save, runtimeServices.PlayerInventory,
                runtimeServices.EquipmentSystem, FoundryStationId, runtimeServices.Foundry);

            if (!EquipmentCollectionSaveAdapter.Capture(save, runtimeServices.EquipmentCollection) ||
                !ActiveSlotSaveAdapter.Capture(save, runtimeServices.ActiveSlot) ||
                !PortableLanternSaveAdapter.Capture(save, runtimeServices.PortableLantern) ||
                !RecipeBookSaveAdapter.Capture(save, runtimeServices.RecipeBook) ||
                !CraftingProcessSaveAdapter.Capture(save, runtimeServices.CraftingProcess) ||
                !UtilityCooldownSaveAdapter.Capture(save, runtimeServices.UtilityService) ||
                !PendingItemAcquisitionSaveAdapter.Capture(save, runtimeServices.InventoryRuntime) ||
                !progressTracker.CaptureTo(save) ||
                !encounterCoordinator.CaptureProgress(save))
                return null;

            save.NormalizeAfterLoad();
            return save;
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
            if (ApplySnapshot(save, forceSafeSurfaceSpawn, true)) return true;
            // A rollback must restore the exact captured state, not reinterpret its position.
            ApplySnapshot(rollback, false, false);
            return false;
        }

        private bool ApplySnapshot(SaveGame save, bool forceSafeSurfaceSpawn,
            bool repairUnsafeSavedPosition)
        {
            if (save == null || !encounterCoordinator.BeginRestore()) return false;
            var succeeded = false;
            try
            {
                save.NormalizeAfterLoad();
                succeeded = save.timeState.hasValue &&
                bootstrap.Session.LoadSnapshot(save) &&
                PreparePlayerSpawnForRestore(save, forceSafeSurfaceSpawn, repairUnsafeSavedPosition) &&
                PlayerTimeBossSaveAdapter.Restore(save, encounterCoordinator.PlayerTransform,
                    encounterCoordinator.PlayerHealth, timeService, encounterCoordinator.BossManager) &&
                ProgressionSaveAdapter.Restore(save, runtimeServices.PlayerInventory,
                    runtimeServices.EquipmentSystem, FindEquipment,
                    FurnaceStationId, runtimeServices.Furnace, FindSmelting, FindItem) &&
                ProgressionSaveAdapter.Restore(save, runtimeServices.PlayerInventory,
                    runtimeServices.EquipmentSystem, FindEquipment,
                    FoundryStationId, runtimeServices.Foundry, FindSmelting, FindItem) &&
                EquipmentCollectionSaveAdapter.Restore(save, runtimeServices.EquipmentCollection) &&
                ActiveSlotSaveAdapter.Restore(save, runtimeServices.ActiveSlot) &&
                PortableLanternSaveAdapter.Restore(save, runtimeServices.PortableLantern) &&
                RecipeBookSaveAdapter.Restore(save, runtimeServices.RecipeBook, FindRecipe) &&
                CraftingProcessSaveAdapter.Restore(save, runtimeServices.CraftingProcess, FindRecipe) &&
                UtilityCooldownSaveAdapter.Restore(save, runtimeServices.UtilityService) &&
                PendingItemAcquisitionSaveAdapter.Restore(save, runtimeServices.InventoryRuntime, FindItem) &&
                progressTracker.RestoreFrom(save) &&
                (!save.playerState.hasTemperature ||
                 runtimeServices.PlayerTemperature.Restore(save.playerState.temperature)) &&
                runtimeServices.DeathTearPouches.Restore(save.deathTearPouches) &&
                encounterCoordinator.RestoreProgress(save) &&
                 environmentState.TryRestorePlacedObjects(save.placedObjectRecords, save.coolingSources) &&
                 runtimeServices.JangdokStorage.TryRestore(save.jangdokStorages,
                     save.placedObjectRecords
                         .Where(record => record.definitionId == Nyangbingo.Inventory.JangdokStorageRuntime.DefinitionId)
                         .Select(record => record.objectId)) &&
                 turretRuntime.RestoreProgress(save);
                return succeeded;
            }
            finally
            {
                encounterCoordinator.EndRestore(succeeded);
            }
        }

        public static bool ShouldResolveSafePlayerSpawn(bool forceSafeSurfaceSpawn,
            bool savedPositionIsSafe) => forceSafeSurfaceSpawn || !savedPositionIsSafe;

        private bool PreparePlayerSpawnForRestore(SaveGame save, bool forceSafeSurfaceSpawn,
            bool repairUnsafeSavedPosition)
        {
            var resolver = bootstrap?.Session?.SafeSpawnResolver;
            var player = encounterCoordinator?.PlayerTransform;
            if (save?.playerState == null || !save.playerState.hasValue || resolver == null || player == null)
                return false;

            var halfExtent = .38f;
            var circle = player.GetComponent<CircleCollider2D>();
            if (circle != null)
                halfExtent = Mathf.Max(.05f, circle.radius * Mathf.Abs(player.lossyScale.y));

            var savedPosition = (Vector2)save.playerState.position;
            if (!forceSafeSurfaceSpawn && !repairUnsafeSavedPosition) return true;
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
        private Nyangbingo.Data.EquipmentDefinition FindEquipment(string id) =>
            GetCatalog()?.FindEquipment(id);
        private Nyangbingo.Data.RecipeDefinition FindRecipe(string id) =>
            GetCatalog()?.FindRecipe(id);
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
