using System;
using System.Collections.Generic;
using System.Linq;
using Nyangbingo.Combat;
using Nyangbingo.Core;
using Nyangbingo.Crafting;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using Nyangbingo.Save;
using Nyangbingo.UI;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>
    /// 메인 플레이 세션이 소유하는 순수 C# 런타임 서비스 묶음. 제작·제련·유틸리티처럼
    /// MonoBehaviour가 아닌 시간 소비자를 생성하고 <see cref="MainGameBootstrap.TickDriver"/>에 등록한다.
    /// 스폰 시 생성되는 AI·전투 소비자는 Register/Unregister를 통해 같은 중앙 시계를 사용한다.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    [RequireComponent(typeof(MainGameBootstrap), typeof(InventoryRuntime))]
    public sealed class MainGameRuntimeServices : MonoBehaviour
    {
        [SerializeField] private GameDataCatalog gameDataCatalog;
        [SerializeField] private MainGameBootstrap bootstrap;
        [SerializeField] private InventoryRuntime inventoryRuntime;
        [SerializeField] private MainGameEnvironmentState environmentState;

        [Header("Player Health Recovery")]
        [Tooltip("켜면 globals.csv의 hp_regen_delay/rate 대신 아래 인스펙터 값을 사용합니다.")]
        [SerializeField] private bool overridePlayerHealthRecovery;
        [Tooltip("마지막 피격 후 자연 회복이 시작되기까지의 게임 시간(초)입니다.")]
        [Min(.01f)]
        [SerializeField] private float playerHealthRegenDelaySeconds = 10f;
        [Tooltip("자연 회복이 시작된 뒤 게임 시간 1초당 회복하는 HP입니다.")]
        [Min(.01f)]
        [SerializeField] private float playerHealthRegenPerSecond = 1f;

        private readonly HashSet<IGameSecondsTickable> registered = new HashSet<IGameSecondsTickable>();

        public Inventory.Inventory PlayerInventory { get; private set; }
        public InventoryRuntime InventoryRuntime => inventoryRuntime;
        public CraftingService CraftingService { get; private set; }
        public CraftingProcess CraftingProcess { get; private set; }
        public UtilityService UtilityService { get; private set; }
        public EquipmentSystem EquipmentSystem { get; private set; }
        public EquipmentColdPenaltyRules EquipmentColdPenalty { get; private set; }
        public EquipmentCollection EquipmentCollection { get; private set; }
        public ActiveSlotSystem ActiveSlot { get; private set; }
        public PortableLanternRuntime PortableLantern { get; private set; }
        public RecipeBook RecipeBook { get; private set; }
        public SmeltingStation Furnace { get; private set; }
        public SmeltingStation Foundry { get; private set; }
        public PlayerTemperatureState PlayerTemperature { get; private set; }
        public HeatStageService HeatStage { get; private set; }
        public InvasionService Invasion { get; private set; }
        public RoomTempService RoomTemperature { get; private set; }
        public BedService Bed { get; private set; }
        public PlayerHealthRecoveryService PlayerHealthRecovery { get; private set; }
        public DayHeatDamageService DayHeatDamage { get; private set; }
        public MagpieCompanionRuntime MagpieCompanion { get; private set; }
        public DeathTearPouchRuntime DeathTearPouches { get; private set; }
        public JangdokStorageRuntime JangdokStorage { get; private set; }
        public StorageTemperatureService StorageTemperature { get; private set; }
        public OutdoorIceMeltService OutdoorIceMelt { get; private set; }
        public TalismanRuntime Talismans { get; private set; }
        public SeokbinggoUpgradeService Seokbinggo { get; private set; }
        public FrostSpreadService FrostSpread { get; private set; }
        public GimmickWeaponProgress GimmickWeapons { get; private set; }
        public ArtifactVerbRuntime ArtifactVerbs { get; private set; }
        public ArtifactModuleHoldover ModuleHoldover { get; private set; }
        public int RegisteredConsumerCount => registered.Count;
        public bool IsInitialized { get; private set; }
        private EquipmentAcquisitionBinding equipmentAcquisitionBinding;
        private bool worldLoadedHooked;

        public void ConfigureForScene(GameDataCatalog catalog, MainGameBootstrap mainBootstrap,
            InventoryRuntime itemReceiver)
        {
            gameDataCatalog = catalog;
            bootstrap = mainBootstrap;
            inventoryRuntime = itemReceiver;
        }

        private void Start()
        {
            Initialize();
        }

        public bool Initialize()
        {
            if (IsInitialized) return true;
            bootstrap ??= GetComponent<MainGameBootstrap>();
            inventoryRuntime ??= GetComponent<InventoryRuntime>();
            environmentState ??= GetComponent<MainGameEnvironmentState>();
            if (gameDataCatalog == null || bootstrap == null || inventoryRuntime == null ||
                !bootstrap.InitializeServices())
            {
                Debug.LogError("[Nyangbingo] MainGameRuntimeServices: GameDataCatalog 또는 MainGameBootstrap " +
                               "배선이 준비되지 않았습니다.");
                return false;
            }

            var furnaceDefinitions = gameDataCatalog.Smelting
                .Where(definition => definition != null && definition.StationKind == SmeltingStationKind.Furnace)
                .ToArray();
            var foundryDefinitions = gameDataCatalog.Smelting
                .Where(definition => definition != null && definition.StationKind == SmeltingStationKind.Foundry)
                .ToArray();
            if (!TryGetSharedCapacity(furnaceDefinitions, out var furnaceCapacity) ||
                !TryGetSharedCapacity(foundryDefinitions, out var foundryCapacity))
            {
                Debug.LogError("[Nyangbingo] MainGameRuntimeServices: 제련소 종류별 batchCapacity가 없거나 " +
                               "서로 일치하지 않습니다.");
                return false;
            }

            var inventoryDefinition = gameDataCatalog.FindGlobal(GlobalKeys.InventorySlots);
            if (inventoryDefinition == null || !inventoryDefinition.TryGetInt(out var inventorySlots) ||
                inventorySlots != Inventory.Inventory.SlotCount)
            {
                Debug.LogError($"[Nyangbingo] MainGameRuntimeServices: inventory_slots는 " +
                               $"{Inventory.Inventory.SlotCount}이어야 합니다.");
                return false;
            }

            PlayerInventory = new Inventory.Inventory(
                gameDataCatalog.FindItem,
                inventorySlots,
                MainGameCraftingUiController.InventoryHotbarSlotCount,
                itemId => MainGameTilePaletteController.IsHotbarSelectable(
                    gameDataCatalog.FindItem(itemId), gameDataCatalog.Recipes,
                    bootstrap.TimeService?.Day ?? 1));
            if (!inventoryRuntime.ConfigureForRuntime(PlayerInventory))
            {
                Debug.LogError("[Nyangbingo] MainGameRuntimeServices: ItemAcquisition receiver 연결에 실패했습니다.");
                PlayerInventory = null;
                return false;
            }
            CraftingService = new CraftingService(PlayerInventory);
            CraftingProcess = new CraftingProcess(CraftingService);
            UtilityService = new UtilityService(PlayerInventory);
            EquipmentSystem = new EquipmentSystem();
            if (!EquipmentColdPenaltyRules.TryCreate(gameDataCatalog, out var equipmentColdPenalty))
            {
                Debug.LogError("[Nyangbingo] MainGameRuntimeServices: v74 equipment cold-penalty globals are invalid.");
                return false;
            }
            EquipmentColdPenalty = equipmentColdPenalty;
            EquipmentCollection = new EquipmentCollection(gameDataCatalog.FindEquipment);
            ActiveSlot = new ActiveSlotSystem(PlayerInventory, gameDataCatalog.FindItem);
            var lanternRadiusDefinition = gameDataCatalog.FindGlobal(GlobalKeys.PortableLanternRadius);
            if (lanternRadiusDefinition == null || !lanternRadiusDefinition.TryGetFloat(out var lanternRadius) ||
                lanternRadius <= 0f)
            {
                Debug.LogError("[Nyangbingo] MainGameRuntimeServices: portable_lantern_radius가 올바르지 않습니다.");
                return false;
            }
            PortableLantern = new PortableLanternRuntime(PlayerInventory, ActiveSlot, lanternRadius);
            RecipeBook = new RecipeBook();
            equipmentAcquisitionBinding = new EquipmentAcquisitionBinding(EquipmentCollection);
            if (!HeatStageService.TryCreate(gameDataCatalog, out var heatStage))
            {
                Debug.LogError("[Nyangbingo] MainGameRuntimeServices: v72 heat-stage globals are invalid.");
                return false;
            }
            HeatStage = heatStage;
            try
            {
                Invasion = new InvasionService(
                    gameDataCatalog, bootstrap.TimeService, PlayerInventory);
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[Nyangbingo] MainGameRuntimeServices: v72 invasion globals are invalid: " +
                               exception.Message);
                return false;
            }
            RoomTemperature = new RoomTempService(gameDataCatalog, bootstrap.SealSystem,
                HeatStage, environmentState, bootstrap.Session, Invasion);
            try
            {
                Bed = new BedService(gameDataCatalog, bootstrap.TimeService, RoomTemperature, Invasion);
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[Nyangbingo] MainGameRuntimeServices: v72 bed globals are invalid: " +
                               exception.Message);
                return false;
            }
            var stationTemperatureStrict = ReadBoolGlobal(GlobalKeys.StationTemperatureStrict, true);
            Furnace = new SmeltingStation(PlayerInventory, SmeltingStationKind.Furnace, furnaceCapacity,
                position => RoomTemperature.Resolve(position), stationTemperatureStrict,
                RoomTemperature.FrozenEnterCelsius);
            Foundry = new SmeltingStation(PlayerInventory, SmeltingStationKind.Foundry, foundryCapacity,
                position => RoomTemperature.Resolve(position), stationTemperatureStrict,
                RoomTemperature.FrozenEnterCelsius);
            try
            {
                Talismans = new TalismanRuntime(gameDataCatalog, PlayerInventory, environmentState);
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[Nyangbingo] MainGameRuntimeServices: v72 talisman data is invalid: " +
                               exception.Message);
                return false;
            }
            PlayerTemperature = new PlayerTemperatureState(gameDataCatalog, bootstrap.TimeService,
                bootstrap.SealSystem, EquipmentSystem, environmentState, bootstrap.Session,
                RoomTemperature, HeatStage, () => Talismans?.SuppressesHypothermia == true);
            DeathTearPouches = new DeathTearPouchRuntime(PlayerInventory, bootstrap.TimeService);
            var jangdokDefinition = gameDataCatalog.FindGlobal(GlobalKeys.JangdokStorageSlots);
            if (jangdokDefinition == null || !jangdokDefinition.TryGetInt(out var jangdokSlots) ||
                jangdokSlots != JangdokStorageRuntime.SlotCount)
            {
                Debug.LogError($"[Nyangbingo] MainGameRuntimeServices: jangdok_storage_slots는 " +
                               $"{JangdokStorageRuntime.SlotCount}이어야 합니다.");
                return false;
            }
            JangdokStorage = new JangdokStorageRuntime(gameDataCatalog.FindItem, jangdokSlots);
            try
            {
                StorageTemperature = new StorageTemperatureService(
                    gameDataCatalog, bootstrap.TimeService, RoomTemperature,
                    environmentState, JangdokStorage);
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[Nyangbingo] MainGameRuntimeServices: v72 storage globals are invalid: " +
                               exception.Message);
                return false;
            }
            OutdoorIceMelt = new OutdoorIceMeltService(
                gameDataCatalog,
                bootstrap.TimeService,
                bootstrap,
                () => PlayerInventory,
                () => bootstrap.GetComponentInChildren<MainGameWorldDropRuntime>());
            Seokbinggo = new SeokbinggoUpgradeService(gameDataCatalog, () => PlayerInventory);
            FrostSpread = new FrostSpreadService(gameDataCatalog);
            if (!FrostSpread.EndingConfigurationValid)
            {
                Debug.LogError("[Nyangbingo] MainGameRuntimeServices: v75 gate-ending globals are invalid.");
                return false;
            }
            GimmickWeapons = new GimmickWeaponProgress(gameDataCatalog.FindItem);
            ArtifactVerbs = new ArtifactVerbRuntime();
            ModuleHoldover = new ArtifactModuleHoldover();
            FrostSpread.FirstFrostRevealed += HandleFirstFrostRevealed;
            GameEvents.OnBaekjungEnd += HandleGimmickBaekjungSurvived;
            GameEvents.OnBossDefeated += HandleBossDefeated;
            GameEvents.OnDayStart += HandleArtifactDayStart;

            Register(CraftingProcess);
            Register(UtilityService);
            Register(Furnace);
            Register(Foundry);
            Register(PlayerTemperature);
            Register(PortableLantern);
            Register(Talismans);
            Register(ModuleHoldover);
            IsInitialized = registered.Count == 8;

            if (IsInitialized)
            {
                BindFrostSpreadToWorld();
                if (bootstrap != null && !worldLoadedHooked)
                {
                    bootstrap.WorldReady += BindFrostSpreadToWorld;
                    worldLoadedHooked = true;
                }
                GameEvents.OnYokaiKilled += HandleRecipeUnlockYokaiKilled;
                YokaiCodexBinding.CodexEntryChanged += HandleCodexEntryChanged;
                Debug.Log($"[Nyangbingo] MainGameRuntimeServices: {PlayerInventory.Capacity}슬롯 인벤토리와 제작·유틸리티·" +
                          $"화로({furnaceCapacity})·용광로({foundryCapacity})·체온·등불·부적·모듈유지 Tick 소비자 8개 등록 완료.");
            }
            if (IsInitialized)
                Debug.Log($"[Nyangbingo] MainGameRuntimeServices: ItemAcquisition receiver 1개가 " +
                          $"{PlayerInventory.Capacity}칸 인벤토리에 연결되었습니다.");
            return IsInitialized;
        }

        public bool BindPlayerHealth(Health health)
        {
            if (!IsInitialized || health == null) return false;
            PlayerTemperature?.BindHealth(health);
            BindArtifactPlayerHooks(health.transform);
            if (PlayerHealthRecovery?.Health == health) return true;
            if (!TryReadPositiveGlobal("hp_regen_delay", out var regenDelay) ||
                !TryReadPositiveGlobal("hp_regen_rate", out var regenRate) ||
                !TryReadPositiveGlobal("catnip_heal", out var catnipHeal) ||
                catnipHeal > int.MaxValue || !Mathf.Approximately(catnipHeal, Mathf.Round(catnipHeal)) ||
                !TryReadMushroomHealing(out var mushroomHealing))
            {
                Debug.LogError("[Nyangbingo] MainGameRuntimeServices: HP recovery globals are invalid.");
                return false;
            }
            if (overridePlayerHealthRecovery)
            {
                if (!IsFinitePositive(playerHealthRegenDelaySeconds) ||
                    !IsFinitePositive(playerHealthRegenPerSecond))
                {
                    Debug.LogError("[Nyangbingo] MainGameRuntimeServices: Inspector HP recovery override values must be positive.");
                    return false;
                }
                regenDelay = playerHealthRegenDelaySeconds;
                regenRate = playerHealthRegenPerSecond;
            }

            if (PlayerHealthRecovery != null)
            {
                Unregister(PlayerHealthRecovery);
                PlayerHealthRecovery.Dispose();
            }
            if (DayHeatDamage != null)
            {
                Unregister(DayHeatDamage);
                DayHeatDamage.Dispose();
            }
            PlayerHealthRecovery = new PlayerHealthRecoveryService(
                PlayerInventory, health, regenDelay, regenRate, Mathf.RoundToInt(catnipHeal), mushroomHealing);
            PlayerHealthRecovery.SetRegenMultiplierProvider(() =>
                environmentState != null
                    ? environmentState.ResolveJukbuinRegenMultiplier(health.transform.position)
                    : 1f);
            DayHeatDamage = new DayHeatDamageService(
                health, health.transform, bootstrap.TimeService, bootstrap.Session, HeatStage,
                gameDataCatalog, environmentState);
            if (Register(PlayerHealthRecovery) && Register(DayHeatDamage)) return true;

            Unregister(PlayerHealthRecovery);
            PlayerHealthRecovery.Dispose();
            PlayerHealthRecovery = null;
            Unregister(DayHeatDamage);
            DayHeatDamage.Dispose();
            DayHeatDamage = null;
            return false;
        }

        private static bool IsFinitePositive(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private bool TryReadMushroomHealing(out IReadOnlyDictionary<string, int> values)
        {
            values = null;
            var raw = gameDataCatalog?.FindGlobal(GlobalKeys.MushroomHeal)?.Value;
            var parts = raw?.Split('/');
            if (parts == null || parts.Length != 3 ||
                !int.TryParse(parts[0], out var oyster) || oyster <= 0 ||
                !int.TryParse(parts[1], out var shiitake) || shiitake <= oyster ||
                !int.TryParse(parts[2], out var seogi) || seogi <= shiitake)
                return false;
            values = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [StorageTemperatureService.OysterMushroomId] = oyster,
                [StorageTemperatureService.ShiitakeId] = shiitake,
                [StorageTemperatureService.SeogiId] = seogi
            };
            return true;
        }

        private bool ReadBoolGlobal(string key, bool fallback)
        {
            var definition = gameDataCatalog?.FindGlobal(key);
            return definition != null && definition.TryGetBool(out var value) ? value : fallback;
        }

        public bool BindMagpieCompanion(Transform player, MainGameWorldDropRuntime worldDrops,
            CharacterArtCatalog characterArtCatalog = null)
        {
            if (!IsInitialized || player == null || worldDrops == null ||
                environmentState == null || bootstrap?.TimeService == null ||
                bootstrap.SealSystem == null)
                return false;
            if (MagpieCompanion != null) return true;
            try
            {
                MagpieCompanion = new MagpieCompanionRuntime(
                    gameDataCatalog, PlayerInventory, environmentState, worldDrops,
                    player, bootstrap.TimeService, bootstrap.SealSystem, characterArtCatalog);
                MagpieCompanion.ConfigureArtifactRadius(() =>
                {
                    if (ArtifactVerbs == null || EquipmentSystem == null) return 1f;
                    var context = ArtifactActivationContextFactory.Build(
                        bootstrap.TileService, player.position, bootstrap.TimeService);
                    return ArtifactVerbs.ResolveMagpieRadiusMultiplier(EquipmentSystem, context);
                });
                if (Register(MagpieCompanion)) return true;
                MagpieCompanion.Dispose();
                MagpieCompanion = null;
                return false;
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[Nyangbingo] MainGameRuntimeServices: magpie runtime binding failed: {exception.Message}");
                MagpieCompanion = null;
                return false;
            }
        }

        /// <summary>스폰 시 생기는 AI·전투 소비자를 동일한 session.TickDriver에 연결한다.</summary>
        public bool Register(IGameSecondsTickable consumer)
        {
            if (consumer == null || bootstrap?.TickDriver == null || !registered.Add(consumer)) return false;
            bootstrap.TickDriver.Register(consumer);
            return true;
        }

        public bool Unregister(IGameSecondsTickable consumer)
        {
            if (consumer == null || !registered.Remove(consumer)) return false;
            bootstrap?.TickDriver?.Unregister(consumer);
            return true;
        }

        private void OnDestroy()
        {
            IsInitialized = false;
            if (FrostSpread != null)
                FrostSpread.FirstFrostRevealed -= HandleFirstFrostRevealed;
            GameEvents.OnBaekjungEnd -= HandleGimmickBaekjungSurvived;
            GameEvents.OnBossDefeated -= HandleBossDefeated;
            GameEvents.OnDayStart -= HandleArtifactDayStart;
            GameEvents.OnYokaiKilled -= HandleRecipeUnlockYokaiKilled;
            YokaiCodexBinding.CodexEntryChanged -= HandleCodexEntryChanged;
            if (bootstrap != null && worldLoadedHooked)
            {
                bootstrap.WorldReady -= BindFrostSpreadToWorld;
                worldLoadedHooked = false;
            }
            if (bootstrap?.TileService != null && ReferenceEquals(bootstrap.TileService.FrostSpread, FrostSpread))
                bootstrap.TileService.FrostSpread = null;
            PortableLantern?.Dispose();
            PortableLantern = null;
            PlayerHealthRecovery?.Dispose();
            PlayerHealthRecovery = null;
            DayHeatDamage?.Dispose();
            DayHeatDamage = null;
            if (MagpieCompanion != null)
            {
                Unregister(MagpieCompanion);
                MagpieCompanion.Dispose();
                MagpieCompanion = null;
            }
            DeathTearPouches?.Dispose();
            DeathTearPouches = null;
            StorageTemperature?.Dispose();
            StorageTemperature = null;
            OutdoorIceMelt?.Dispose();
            OutdoorIceMelt = null;
            JangdokStorage = null;
            Talismans = null;
            Seokbinggo = null;
            FrostSpread = null;
            Invasion?.Dispose();
            Invasion = null;
            Bed = null;
            HeatStage = null;
            EquipmentColdPenalty = null;
            GimmickWeapons = null;
            ArtifactVerbs = null;
            ModuleHoldover = null;
            equipmentAcquisitionBinding?.Dispose();
            equipmentAcquisitionBinding = null;
            if (bootstrap?.TickDriver != null)
            {
                foreach (var consumer in registered)
                    bootstrap.TickDriver.Unregister(consumer);
            }
            registered.Clear();
        }

        private void BindFrostSpreadToWorld()
        {
            if (FrostSpread == null || bootstrap?.TileService == null) return;
            bootstrap.TileService.FrostSpread = FrostSpread;
        }

        private void HandleFirstFrostRevealed()
        {
            GameEvents.RaiseFrostMineralRevealed();
            GimmickWeapons?.NotifyFirstFrost();
        }

        private void HandleGimmickBaekjungSurvived() => GimmickWeapons?.NotifyBaekjungSurvived();

        private void HandleArtifactDayStart() => ArtifactVerbs?.ResetDailyUses();

        private void HandleBossDefeated(BossDefinition definition)
        {
            HeatStage?.OnNamedKill(definition?.Id);
            GimmickWeapons?.NotifyBossDefeated(definition);
            if (definition == null || FrostSpread == null) return;
            FrostSpread.OnAltarBossClear(definition.Id, bootstrap?.TileService);
        }

        private void HandleRecipeUnlockYokaiKilled(YokaiDefinition definition)
        {
            if (definition == null || definition.Kind != YokaiKind.Gangcheori || RecipeBook == null) return;
            var recipe = gameDataCatalog?.FindRecipe(RecipeUnlockPolicy.GangcheoriUnlockRecipeId);
            if (recipe == null)
            {
                Debug.LogError("[Nyangbingo] MainGameRuntimeServices: 강철이 처치 해금 레시피가 없습니다.");
                return;
            }
            if (RecipeBook.IsUnlocked(recipe)) return;
            RecipeBook.Unlock(recipe.Id);
            Debug.Log($"[Nyangbingo] 강철이 최초 처치로 제작법을 해금했습니다: {recipe.Output.item.DisplayName}.");
        }

        private void HandleCodexEntryChanged(YokaiDefinition definition, bool isFirstEntry)
        {
            if (!isFirstEntry || definition == null || ArtifactVerbs == null ||
                EquipmentSystem == null || PlayerInventory == null || bootstrap?.TimeService == null)
                return;
            var context = ArtifactActivationContextFactory.Build(
                bootstrap.TileService, Vector2.zero, bootstrap.TimeService);
            var bonus = ArtifactVerbs.ResolveCodexTearBonus(EquipmentSystem, context);
            if (bonus <= 0f) return;
            var amount = Mathf.Max(1, Mathf.RoundToInt(bonus));
            if (!PlayerInventory.TryAdd(ArtifactVerbRuntime.CodexTearItemId, amount)) return;
            Debug.Log($"[Nyangbingo] Artifact minhwa_ink granted codex tear bonus x{amount} ({definition.Id}).");
        }

        private void BindArtifactPlayerHooks(Transform playerTransform)
        {
            if (playerTransform == null) return;
            PlayerTemperature?.ConfigureShadeHeatSuppressor(() =>
            {
                if (ArtifactVerbs == null || EquipmentSystem == null || bootstrap?.TimeService == null ||
                    bootstrap.Session?.HasWorld != true)
                    return false;
                var context = ArtifactActivationContextFactory.Build(
                    bootstrap.TileService, playerTransform.position, bootstrap.TimeService);
                if (!ArtifactVerbs.LocksShadeTemperature(EquipmentSystem, context)) return false;
                return WorldExposureRules.TryIsSurfaceExposed(
                           playerTransform.position,
                           bootstrap.Session.LastResult.surfaceHeights,
                           out var exposed) &&
                       !exposed;
            });
            environmentState?.ConfigureIceCrystalCoolerRadiusProvider(() =>
            {
                if (ArtifactVerbs == null || EquipmentSystem == null || bootstrap?.TimeService == null)
                    return ArtifactVerbRuntime.CoolerBaseRadiusTiles;
                var context = ArtifactActivationContextFactory.Build(
                    bootstrap.TileService, playerTransform.position, bootstrap.TimeService);
                return ArtifactVerbs.ResolveCoolerRadiusTiles(EquipmentSystem, context);
            });
            environmentState?.ConfigureModuleHoldoverProvider(() =>
            {
                if (ArtifactVerbs == null || EquipmentSystem == null || bootstrap?.TimeService == null)
                    return false;
                var context = ArtifactActivationContextFactory.Build(
                    bootstrap.TileService, playerTransform.position, bootstrap.TimeService);
                return ArtifactVerbs.MaintainsModuleAfterShutdown(EquipmentSystem, context);
            });
            StorageTemperature?.ConfigureIceMeltMultiplierProvider(() =>
            {
                if (ArtifactVerbs == null || EquipmentSystem == null || bootstrap?.TimeService == null)
                    return 1f;
                var context = ArtifactActivationContextFactory.Build(
                    bootstrap.TileService, playerTransform.position, bootstrap.TimeService);
                return ArtifactVerbs.ResolveIceMeltMultiplier(EquipmentSystem, context);
            });
        }

        private bool TryReadPositiveGlobal(string key, out float value)
        {
            value = 0f;
            var definition = gameDataCatalog?.FindGlobal(key);
            return definition != null && definition.TryGetFloat(out value) && value > 0f &&
                   !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool TryGetSharedCapacity(IReadOnlyList<SmeltingDefinition> definitions, out int capacity)
        {
            capacity = 0;
            if (definitions == null || definitions.Count == 0) return false;
            capacity = definitions[0].BatchCapacity;
            if (capacity <= 0) return false;
            for (var index = 1; index < definitions.Count; index++)
                if (definitions[index].BatchCapacity != capacity) return false;
            return true;
        }
    }
}
