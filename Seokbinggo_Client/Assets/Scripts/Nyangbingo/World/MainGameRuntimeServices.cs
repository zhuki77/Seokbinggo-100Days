using System.Collections.Generic;
using System.Linq;
using Nyangbingo.Combat;
using Nyangbingo.Core;
using Nyangbingo.Crafting;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
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

        private readonly HashSet<IGameSecondsTickable> registered = new HashSet<IGameSecondsTickable>();

        public Inventory.Inventory PlayerInventory { get; private set; }
        public InventoryRuntime InventoryRuntime => inventoryRuntime;
        public CraftingService CraftingService { get; private set; }
        public CraftingProcess CraftingProcess { get; private set; }
        public UtilityService UtilityService { get; private set; }
        public EquipmentSystem EquipmentSystem { get; private set; }
        public EquipmentCollection EquipmentCollection { get; private set; }
        public ActiveSlotSystem ActiveSlot { get; private set; }
        public PortableLanternRuntime PortableLantern { get; private set; }
        public RecipeBook RecipeBook { get; private set; }
        public SmeltingStation Furnace { get; private set; }
        public SmeltingStation Foundry { get; private set; }
        public PlayerTemperatureState PlayerTemperature { get; private set; }
        public NapService NapService { get; private set; }
        public DeathTearPouchRuntime DeathTearPouches { get; private set; }
        public JangdokStorageRuntime JangdokStorage { get; private set; }
        public int RegisteredConsumerCount => registered.Count;
        public bool IsInitialized { get; private set; }
        private EquipmentAcquisitionBinding equipmentAcquisitionBinding;

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

            PlayerInventory = new Inventory.Inventory(gameDataCatalog.FindItem, inventorySlots);
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
            Furnace = new SmeltingStation(PlayerInventory, SmeltingStationKind.Furnace, furnaceCapacity);
            Foundry = new SmeltingStation(PlayerInventory, SmeltingStationKind.Foundry, foundryCapacity);
            PlayerTemperature = new PlayerTemperatureState(gameDataCatalog, bootstrap.TimeService,
                bootstrap.SealSystem, EquipmentSystem, environmentState);
            NapService = new NapService(gameDataCatalog, bootstrap.TimeService,
                multiplier => PlayerTemperature.SetRecoveryMultiplier(multiplier));
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

            Register(CraftingProcess);
            Register(UtilityService);
            Register(Furnace);
            Register(Foundry);
            Register(PlayerTemperature);
            Register(PortableLantern);
            IsInitialized = registered.Count == 6;

            if (IsInitialized)
            {
                Debug.Log($"[Nyangbingo] MainGameRuntimeServices: {PlayerInventory.Capacity}슬롯 인벤토리와 제작·유틸리티·" +
                          $"화로({furnaceCapacity})·용광로({foundryCapacity})·체온·휴대용 등불 Tick 소비자 6개 등록 완료.");
            }
            if (IsInitialized)
                Debug.Log($"[Nyangbingo] MainGameRuntimeServices: ItemAcquisition receiver 1개가 " +
                          $"{PlayerInventory.Capacity}칸 인벤토리에 연결되었습니다.");
            return IsInitialized;
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
            PortableLantern?.Dispose();
            PortableLantern = null;
            NapService?.Dispose();
            NapService = null;
            DeathTearPouches?.Dispose();
            DeathTearPouches = null;
            JangdokStorage = null;
            equipmentAcquisitionBinding?.Dispose();
            equipmentAcquisitionBinding = null;
            if (bootstrap?.TickDriver != null)
            {
                foreach (var consumer in registered)
                    bootstrap.TickDriver.Unregister(consumer);
            }
            registered.Clear();
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
