using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Nyangbingo.Core;
using Nyangbingo.Combat;
using Nyangbingo.Crafting;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using Nyangbingo.Save;
using Nyangbingo.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using Input = Nyangbingo.Core.GameplayInput;

namespace Nyangbingo.UI
{
    /// <summary>
    /// MainGame의 범용 제작·제련 제품 UI. 공식 데이터는 전부 적재하되 crafting_b_ui 정책에 따라
    /// scope B 레시피만 표시 단계에서 제외한다.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed partial class MainGameCraftingUiController : MonoBehaviour
    {
        private enum Page { Gathering, Crafting, Equipment, Codex }

        public enum CraftingStationFilter
        {
            Workbench = 0,
            Furnace = 1,
            IceAnvil = 2,
            Foundry = 3,
            Smithy = 4,
            None = 5
        }

        public const int CraftingFilterCount = 6;
        private const string StorageItemMovedMessage = "아이템을 이동했습니다.";

        [SerializeField] private GameDataCatalog gameDataCatalog;
        [SerializeField] private MainGameRuntimeServices runtimeServices;
        [SerializeField] private MainGameBossSummonUiController stationSource;
        [SerializeField] private GameShellController shell;
        [SerializeField] private MainGameTurretRuntime turretRuntime;
        [SerializeField] private MainGameTilePaletteController tilePalette;
        [SerializeField] private ItemArtCatalog itemArtCatalog;
        [SerializeField] private GameplayArtCatalog gameplayArtCatalog;

        private readonly List<RecipeDefinition> visibleRecipes = new List<RecipeDefinition>();
        private readonly List<RecipeDefinition> filteredRecipes = new List<RecipeDefinition>();
        private readonly List<SmeltingDefinition> smeltingRecipes = new List<SmeltingDefinition>();
        private readonly List<ItemDefinition> activeSlotItems = new List<ItemDefinition>();
        private readonly List<ItemDefinition> clawItems = new List<ItemDefinition>();
        private readonly List<EquipmentDefinition> ownedEquipment = new List<EquipmentDefinition>();
        [SerializeField] private GameObject panel;
        [SerializeField] private Text titleText;
        [SerializeField] private Text detailsText;
        [SerializeField] private Text messageText;
        [SerializeField] private ScrollRect detailsScrollRect;
        [SerializeField] private RectTransform detailsViewportRect;
        [SerializeField] private RectTransform detailsScrollbarRect;
        [SerializeField] private GameObject inventoryGridRoot;
        private GameObject inventoryScrollRoot;
        private ScrollRect inventoryScrollRect;
        private GameObject craftingListRoot;
        private ScrollRect craftingListScrollRect;
        private GameObject craftWindowRoot;
        private bool sideHudVisible;
        private RectTransform tearBalanceRoot;
        private Image craftWindowOutputIcon;
        private string displayedCraftOutputId;
        private Image[] craftWindowIngredientIcons = new Image[3];
        private readonly Text[] craftWindowIngredientCounts = new Text[3];
        private Button[] craftWindowSlotButtons = Array.Empty<Button>();
        private Image[] craftWindowSlotIcons = Array.Empty<Image>();
        private Image[] craftWindowSlotMarks = Array.Empty<Image>();
        private readonly List<RecipeDefinition> craftWindowRecipes = new List<RecipeDefinition>();
        private int craftWindowIndex;
        private Image craftWindowFrame;
        private Button craftWindowActionButton;
        private Image craftWindowActionImage;
        private Button craftWindowToggleButton;
        private Button[] craftCategoryButtons = Array.Empty<Button>();
        private Text[] craftCategoryLabels = Array.Empty<Text>();
        private int craftCategoryIndex;
        private ScrollRect craftWindowScrollRect;
        private const int CraftWindowColumns = 3;
        private bool craftWindowPanelTintSaved;
        private Color craftWindowPanelColor;
        private Button[] craftingListButtons = Array.Empty<Button>();
        private Text[] craftingListLabels = Array.Empty<Text>();
        private Image[] craftingListOutputIcons = Array.Empty<Image>();
        private Text[] craftingListOutputCounts = Array.Empty<Text>();
        private Image[][] craftingListIngredientIcons = Array.Empty<Image[]>();
        private Text[][] craftingListIngredientCounts = Array.Empty<Text[]>();
        [SerializeField] private GameObject codexGridRoot;
        [SerializeField] private GameObject storageModeRoot;
        [SerializeField] private Text[] storagePlayerLabels = new Text[Nyangbingo.Inventory.Inventory.SlotCount];
        [SerializeField] private Text[] storageLabels = new Text[JangdokStorageRuntime.SlotCount];
        [SerializeField] private Image[] storagePlayerIcons = new Image[Nyangbingo.Inventory.Inventory.SlotCount];
        [SerializeField] private Image[] storageIcons = new Image[JangdokStorageRuntime.SlotCount];
        [SerializeField] private Text storageLabelText;
        [SerializeField] private Text storageHintText;
        private string storageObjectId = string.Empty;
        private float nextStorageRefresh;
        private Image storageTemperatureTrack, storageTemperatureCold, storageTemperatureCurrent, storageTemperatureRequired;
        private ChestProgress chestProgress;
        private string chestId = string.Empty;
        private bool HasStorageMode => !string.IsNullOrEmpty(storageObjectId) || chestProgress != null;
        [SerializeField] private Text[] inventoryGridLabels =
            new Text[Nyangbingo.Inventory.Inventory.SlotCount];
        [SerializeField] private Button[] inventoryGridButtons =
            new Button[Nyangbingo.Inventory.Inventory.SlotCount];
        [SerializeField] private Image[] inventoryGridIcons =
            new Image[Nyangbingo.Inventory.Inventory.SlotCount];
        [SerializeField] private GameObject equipmentVisualRoot;
        [SerializeField] private Image equipmentCharacter;
        [SerializeField] private Image[] equipmentSlotBackgrounds = new Image[6];
        private readonly Dictionary<Image, GameObject> slotSelectionBorders = new Dictionary<Image, GameObject>();
        [SerializeField] private Button[] equipmentSlotButtons = new Button[6];
        [SerializeField] private Image[] equipmentSlotIcons = new Image[6];
        [SerializeField] private Text[] equipmentSlotFallbackLabels = new Text[6];
        private Text equipmentAmmoCount;
        [SerializeField] private Button[] codexCardButtons = new Button[YokaiCodexPresentationModel.ExpectedCardCount];
        [SerializeField] private Text[] codexCardLabels = new Text[YokaiCodexPresentationModel.ExpectedCardCount];
        [SerializeField] private Image[] codexCardPortraits = new Image[YokaiCodexPresentationModel.ExpectedCardCount];
        [SerializeField] private GameObject codexExpandedBackdrop;
        [SerializeField] private Image codexExpandedCardBackground;
        [SerializeField] private Image codexExpandedPortrait;
        [SerializeField] private Text codexExpandedTitle;
        [SerializeField] private Text codexExpandedFrontText;
        [SerializeField] private Text codexExpandedBackText;
        [SerializeField] private Text codexExpandedHintText;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button primaryButton;
        [SerializeField] private Button collectButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button debugCompleteButton;
        [SerializeField] private GameObject summonConfirmationRoot;
        [SerializeField] private Text summonConfirmationText;
        [SerializeField] private Text inventoryHintText;
        private string pendingSummonItemId = string.Empty;
        [SerializeField] private Button[] tabButtons = new Button[4];
        private Page page;
        private int selectedIndex;
        private int inventorySelectedIndex;
        private GameObject descriptionRoot;
        private static readonly Vector2 DescriptionDefaultPosition = new Vector2(2f, 3f);
        private int descriptionDragPointerId = int.MinValue;
        private Vector2 descriptionDragStartPointer;
        private Vector2 descriptionDragStartPosition;
        private Text descriptionTitle;
        private Text descriptionBody;
        private ScrollRect descriptionScroll;
        private Scrollbar descriptionScrollbar;
        private string descriptionItemId;
        private bool descriptionIsMaterial;
        private readonly string[] displayedIngredientIds = new string[3];
        private readonly bool[] displayedIngredientMissing = new bool[3];
        private UnityEngine.UI.Image inventoryCursorIcon;
        private UnityEngine.UI.Text inventoryCursorCount;
        private EquipmentInventoryDrag equipmentDrag;
        private GameObject equipmentDragSource;
        private int equipmentDragPointerId;
        private string equipmentDropFeedback;
        private float equipmentDropFeedbackUntil;
        private bool IsDraggingEquipment => equipmentDrag?.IsActive == true;
        private int equippedDragVisualSlot = -1;
        private string equippedDragItemId;
        private bool IsDraggingEquippedItem => equippedDragVisualSlot >= 0;
        private static MainGameCraftingUiController cursorOwner;
        private bool HasHeldInventoryItem => runtimeServices?.InventoryCursor?.IsEmpty == false;

        private string message;
        private float messageUntil;
        private bool initialized;
        private bool open;
        private CraftingStationFilter craftingFilter = CraftingStationFilter.Workbench;
        private CraftingStation openedStation = CraftingStation.None;
        private string openedStationObjectId;
        private bool bindingProductionStation;
        private GameObject productionQueueRoot;
        private readonly List<Button> productionQueueSlots = new List<Button>();
        private readonly List<Image> productionQueueIcons = new List<Image>();
        private readonly List<Image> productionQueueTimers = new List<Image>();
        private Sprite productionQueueTimerSprite;
        private readonly StationJobRecord[] displayedProductionJobs =
            new StationJobRecord[StationProductionService.WaitingCapacity + 1];
        private Button productionQueueCollectButton;
        private bool furnaceSmeltingView;
        private bool IsShowingSmeltingList =>
            page == Page.Crafting && IsSmeltingStation(openedStation) && furnaceSmeltingView;
        private YokaiCodexPresentationModel codexModel;
        private CharacterArtCatalog characterArtCatalog;
        private static int openControllerCount;
        private static int escapeConsumedFrame = -1;
        private static int worldInteractionConsumedFrame = -1;
        public static bool ConsumedWorldInteractionThisFrame => worldInteractionConsumedFrame == Time.frameCount;
        public static bool TryInteractFromOpenCraftingWindow()
        {
            var owner = cursorOwner;
            if (owner == null || !owner.open || owner.page != Page.Crafting || owner.HasStorageMode) return false;
            worldInteractionConsumedFrame = Time.frameCount;
            owner.turretRuntime?.TryInteractFromCraftingUi(owner.openedStationObjectId, () =>
            {
                owner.SetOpen(false);
                return !owner.open;
            });
            return true;
        }
        private int openedFrame = -1;

        public static bool BlocksGameplayInput => openControllerCount > 0 ||
            (cursorOwner != null && (cursorOwner.HasHeldInventoryItem || cursorOwner.IsDraggingItem));
        public static bool BlocksPlayerMovement => openControllerCount > 1 ||
            openControllerCount > 0 && (cursorOwner == null || !cursorOwner.open ||
                cursorOwner.page != Page.Crafting || cursorOwner.HasStorageMode);
        // Crafting stays open beside normal world play. Other menus and a held
        // inventory stack still own gameplay input; UI clicks are filtered separately.
        public static bool BlocksWorldActions => BlocksPlayerMovement ||
            (cursorOwner != null && (cursorOwner.HasHeldInventoryItem || cursorOwner.IsDraggingItem));
        public static bool ShowsInventoryPointer => cursorOwner != null &&
            (cursorOwner.HasHeldInventoryItem || cursorOwner.IsDraggingItem || cursorOwner.open &&
                (cursorOwner.page == Page.Gathering || cursorOwner.HasStorageMode));
        public static bool ConsumedEscapeThisFrame => escapeConsumedFrame == Time.frameCount;
        public bool IsOpen => open;
        public static bool IsStorageWindowOpen => cursorOwner != null && cursorOwner.open && cursorOwner.HasStorageMode;

        /// <summary>인스펙터에 수동 배선되는 납품 제작·제련 패널 하이어라키가 살아 있는지.</summary>
        public bool HasDeliveredPanelBindings =>
            panel != null && titleText != null && tabButtons != null && tabButtons.Length == 4 &&
            tabButtons.All(button => button != null);
        public int VisibleRecipeCount => visibleRecipes.Count;
        public const int UnifiedTabCount = 4;
        public const int InventoryGridColumns = 10;
        public const int InventoryGridRows = 5;
        public const int InventoryHotbarSlotCount = 9;
        public const float InventorySlotPixelSize = 27f;
        private const float HudInventoryCellSize = 14f;
        private const float HudInventoryCellGap = 1f;
        private const int HudInventoryColumns = 8;
        public const float InventoryCursorPixelSize = 12f;
        public const bool UsesIconOnlyCraftingList = true;
        public const KeyCode DebugGrantRequirementsKey = KeyCode.F12;
        private int CurrentDay => FindAnyObjectByType<DayNightService>()?.Day ?? 1;

        public static bool SupportsDebugInstantCompletion
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return true;
#else
                return false;
#endif
            }
        }

        public static string UnifiedTabLabel(int index)
        {
            switch (index)
            {
                case 0: return "인벤토리";
                case 1: return "제작";
                case 2: return "장비";
                case 3: return "도감";
                default: return string.Empty;
            }
        }

        public static KeyCode UnifiedTabHotkey(int index)
        {
            switch (index)
            {
                case 0: return KeyCode.Tab;
                case 1: return KeyCode.C;
                default: return KeyCode.None;
            }
        }

        public void ConfigureForScene(GameDataCatalog catalog, MainGameRuntimeServices services,
            MainGameBossSummonUiController craftingStationSource, ItemArtCatalog itemArt = null,
            GameplayArtCatalog gameplayArt = null)
        {
            gameDataCatalog = catalog;
            runtimeServices = services;
            stationSource = craftingStationSource;
            itemArtCatalog = itemArt;
            gameplayArtCatalog = gameplayArt;
        }

        private void Start()
        {
            if (shell == null) shell = FindAnyObjectByType<GameShellController>();
            if (turretRuntime == null) turretRuntime = FindAnyObjectByType<MainGameTurretRuntime>();
            if (tilePalette == null) tilePalette = FindAnyObjectByType<MainGameTilePaletteController>();
            if (gameDataCatalog == null || runtimeServices == null || !runtimeServices.Initialize())
            {
                Debug.LogError("[Nyangbingo] MainGameCraftingUiController: 제작 UI 데이터 배선이 준비되지 않았습니다.");
                enabled = false;
                return;
            }

            var hideScopeB = RebuildVisibleRecipes();
            smeltingRecipes.AddRange(gameDataCatalog.Smelting
                .Where(definition => definition != null)
                .OrderBy(definition => definition.StationKind)
                .ThenBy(definition => definition.Id, StringComparer.Ordinal));
            BuildUi();
            var saveCoordinator = FindAnyObjectByType<MainGameSaveCoordinator>();
            if (saveCoordinator != null && saveCoordinator.ProgressTracker != null)
            {
                try
                {
                    codexModel = saveCoordinator.ProgressTracker.CreateCodexPresentationModel();
                }
                catch (System.Exception exception)
                {
                    Debug.LogError("[Nyangbingo] MainGameCraftingUiController: 도감 모델 생성 실패 — " +
                                   exception.Message);
                    codexModel = null;
                }
            }
            ResolveCharacterArtCatalog();
            FindAnyObjectByType<MainGameCodexController>()?.UseUnifiedPanel();
            runtimeServices.PlayerInventory.Changed += Refresh;
            runtimeServices.EquipmentSystem.Changed += Refresh;
            runtimeServices.EquipmentCollection.Added += HandleEquipmentAdded;
            runtimeServices.ActiveSlot.Changed += Refresh;
            if (runtimeServices.ClawSlot != null) runtimeServices.ClawSlot.Changed += Refresh;
            runtimeServices.PortableLantern.Changed += Refresh;
            runtimeServices.JangdokStorage.Changed += Refresh;
            runtimeServices.RecipeBook.Changed += Refresh;
            GameEvents.OnDayStart += HandleDayStart;
            if (turretRuntime != null) turretRuntime.BuildStateChanged += Refresh;
            initialized = true;
            cursorOwner = this;
            SetOpen(false);
            Refresh();
            Debug.Log($"[Nyangbingo] MainGame v29 unified UI ready " +
                      $"(tabs=4, recipes={visibleRecipes.Count}, hiddenScopeB={hideScopeB}, " +
                      $"contextualSmelting={smeltingRecipes.Count}, codex={codexModel?.Cards.Count ?? 0}).");
        }

        private void Update()
        {
            if (descriptionRoot != null && descriptionRoot.activeSelf && Time.timeScale > 0f &&
                Input.TryConsumeEscape())
            {
                HideDescription();
                escapeConsumedFrame = Time.frameCount;
                return;
            }
            if (!initialized) return;

            if (IsDraggingItem && Input.TryConsumeEscape())
            {
                escapeConsumedFrame = Time.frameCount;
                CancelEquipmentDrag();
                return;
            }

            if (HasHeldInventoryItem && Input.TryConsumeEscape())
            {
                escapeConsumedFrame = Time.frameCount;
                if (!ReturnHeldInventoryItem())
                {
                    if (!open) OpenPage(Page.Gathering);
                    ShowMessage("원래 보관함에 반환할 공간이 없습니다. 빈 슬롯에 놓아주세요.");
                }
                Refresh();
                return;
            }

            if (open && shell != null && shell.Screen != GameShellScreen.Gameplay)
            {
                SetOpen(false);
                return;
            }

            if (open && summonConfirmationRoot != null && summonConfirmationRoot.activeSelf)
            {
                if (Input.TryConsumeEscape())
                {
                    escapeConsumedFrame = Time.frameCount;
                    CancelSummonConfirmation();
                }
                else if (Input.GetMouseButtonDown(1)) ConfirmSummonItemUse();
                return;
            }

            if (open && Input.TryConsumeEscape())
            {
                escapeConsumedFrame = Time.frameCount;
                SetOpen(false);
                return;
            }

            if (open && HasStorageMode)
            {
                if (Time.unscaledTime >= nextStorageRefresh)
                {
                    runtimeServices.StorageTemperature?.RefreshConditions();
                    RefreshStorage();
                    nextStorageRefresh = Time.unscaledTime + .2f;
                }
                if (openedFrame != Time.frameCount && Input.GetKeyDown(KeyCode.E)) SetOpen(false);
                return;
            }

            if (TryHandlePageHotkey()) return;
            if (!open)
            {
                RefreshProductionQueue();
                return;
            }
            if (page != Page.Codex)
            {
                if (page != Page.Crafting)
                {
                    if (Input.GetKeyDown(KeyCode.LeftArrow) ||
                        page != Page.Gathering && Input.GetKeyDown(KeyCode.A)) SubmitNavigation(-1);
                    if (Input.GetKeyDown(KeyCode.RightArrow) ||
                        page != Page.Gathering && Input.GetKeyDown(KeyCode.D)) SubmitNavigation(1);
                }
                if (page == Page.Gathering &&
                    Input.GetKeyDown(KeyCode.UpArrow))
                    SelectRelative(-InventoryGridColumns);
                if (page == Page.Gathering &&
                    Input.GetKeyDown(KeyCode.DownArrow))
                    SelectRelative(InventoryGridColumns);
                if (page == Page.Equipment && Input.GetKeyDown(KeyCode.Q)) ToggleActiveSlotFromEquipmentPage();
                if (openedFrame != Time.frameCount &&
                    Input.GetKeyDown(KeyCode.E))
                {
                    if (page == Page.Crafting)
                    {
                        worldInteractionConsumedFrame = Time.frameCount;
                        turretRuntime?.TryInteractFromCraftingUi(openedStationObjectId, () =>
                        {
                            SetOpen(false);
                            return !open;
                        });
                    }
                    else TryPrimaryAction();
                }
                if (page == Page.Equipment && Input.GetKeyDown(KeyCode.R)) TryRefuelPortableLantern();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if ((page == Page.Crafting || page == Page.Equipment) &&
                    DevelopmentShortcuts.IsPressed(DevelopmentShortcut.RecipeMaterials))
                    GrantSelectedRequirementsForEditorTest();
                if (page == Page.Crafting &&
                    DevelopmentShortcuts.IsPressed(DevelopmentShortcut.RecipeStation))
                        TeleportToRequiredStationForEditorTest();
                if (page == Page.Crafting &&
                    DevelopmentShortcuts.IsPressed(DevelopmentShortcut.RecipeComplete))
                    TryCompleteCurrentProcessForDebug();
#endif
            }
            if (!string.IsNullOrEmpty(message) && Time.unscaledTime >= messageUntil) message = string.Empty;
            Refresh();
        }

        public static bool ShouldShowRecipe(RecipeDefinition recipe, bool hideScopeB) =>
            recipe != null && (!hideScopeB || recipe.MvpScope != ItemMvpScope.B);

        public static bool ShouldShowRecipe(RecipeDefinition recipe, bool hideScopeB, int currentDay) =>
            ShouldShowRecipe(recipe, hideScopeB);

        private bool RebuildVisibleRecipes()
        {
            visibleRecipes.Clear();
            // v86 B11: visibility follows the catalog policy independently of progression dates.
            var hideScopeB = string.Equals(gameDataCatalog.FindGlobal("crafting_b_ui")?.Value,
                "hidden", StringComparison.OrdinalIgnoreCase);
            visibleRecipes.AddRange(gameDataCatalog.Recipes
                .Where(recipe => ShouldShowRecipe(recipe, hideScopeB, CurrentDay))
                .OrderBy(recipe => recipe.Station)
                .ThenBy(recipe => recipe.Id, StringComparer.Ordinal));
            return hideScopeB;
        }

        private void HandleDayStart()
        {
            if (!initialized || gameDataCatalog == null) return;
            RebuildVisibleRecipes();
            RebuildFilteredRecipes();
            Refresh();
        }

        public static bool IsRecipeVisibleAtStation(CraftingStation requiredStation,
            CraftingStation nearbyStation) =>
            requiredStation == nearbyStation;

        public static bool IsSmithyRecipeAllowed(CraftingStation recipeStation, bool smithyUnlocked) =>
            recipeStation != CraftingStation.Smithy || smithyUnlocked;

        public static bool RecipeMatchesFilter(CraftingStation station, CraftingStationFilter filter) =>
            FilterForStation(station) == filter;

        public static CraftingStationFilter FilterForStation(CraftingStation station)
        {
            switch (station)
            {
                case CraftingStation.Workbench: return CraftingStationFilter.Workbench;
                case CraftingStation.Furnace: return CraftingStationFilter.Furnace;
                case CraftingStation.Foundry: return CraftingStationFilter.Foundry;
                case CraftingStation.IceAnvil: return CraftingStationFilter.IceAnvil;
                case CraftingStation.Smithy: return CraftingStationFilter.Smithy;
                default: return CraftingStationFilter.None;
            }
        }

        public static string CraftingFilterLabel(CraftingStationFilter filter)
        {
            switch (filter)
            {
                case CraftingStationFilter.Workbench: return "제작대";
                case CraftingStationFilter.Furnace: return "화로";
                case CraftingStationFilter.Foundry: return "용광로";
                case CraftingStationFilter.IceAnvil: return "얼음 모루";
                case CraftingStationFilter.Smithy: return "대장간";
                default: return "손 제작";
            }
        }

        private string CraftingFilterTitlePrefix() =>
            StationLabel(openedStation) + (IsSmeltingStation(openedStation)
                ? furnaceSmeltingView ? " · 제련" : " · 제작" : string.Empty);

        public bool TryOpenForStation(CraftingStation station, string objectId = null)
        {
            if (!initialized || station == CraftingStation.None ||
                shell != null && shell.Screen != GameShellScreen.Gameplay || Time.timeScale <= 0f) return false;
            if (stationSource == null || !stationSource.TryGetCraftingStationIdentity(station, objectId,
                    out var enteredId, out _)) return false;

            sideHudVisible = true;
            bindingProductionStation = true;
            OpenPage(Page.Crafting);
            bindingProductionStation = false;
            openedStation = station;
            openedStationObjectId = enteredId;
            ResolveProductionQueue();
            craftingFilter = FilterForStation(station);
            furnaceSmeltingView = false;
            RebuildFilteredRecipes();
            selectedIndex = FindFirstEntryForFilter();
            Refresh();
            ResetDetailsScroll();
            ResetCraftingListScroll();
            return true;
        }

        public bool TryOpenJangdok(string objectId)
        {
            if (!initialized || string.IsNullOrWhiteSpace(objectId) ||
                shell != null && shell.Screen != GameShellScreen.Gameplay || Time.timeScale <= 0f ||
                !runtimeServices.JangdokStorage.TryRegister(objectId)) return false;
            CancelEquipmentDrag();
            chestProgress = null;
            chestId = string.Empty;
            storageObjectId = objectId;
            selectedIndex = 0;
            SetOpen(true);
            Refresh();
            Debug.Log($"[Nyangbingo] Jangdok storage opened: id={objectId}, slots={JangdokStorageRuntime.SlotCount}.");
            return true;
        }

        public bool TryOpenRemoteJangdok(MainGameEnvironmentState environment)
        {
            if (!initialized || environment == null || runtimeServices?.JangdokStorage == null) return false;
            foreach (var record in environment.ExportPlacedObjects())
            {
                if (record.definitionId != JangdokStorageRuntime.DefinitionId) continue;
                if (TryOpenJangdok(record.objectId)) return true;
            }
            ShowMessage("열 수 있는 장독이 없습니다.");
            return false;
        }

        private float ResolveCraftDurationMultiplier(RecipeDefinition recipe)
        {
            if (recipe == null || runtimeServices?.ArtifactVerbs == null ||
                runtimeServices.EquipmentSystem == null)
                return 1f;
            var player = FindAnyObjectByType<MainGamePlayerController>();
            var bootstrap = FindAnyObjectByType<MainGameBootstrap>();
            var context = ArtifactActivationContextFactory.Build(
                bootstrap?.TileService,
                player != null ? player.transform.position : Vector2.zero,
                bootstrap?.TimeService);
            return runtimeServices.ArtifactVerbs.ResolveCraftDurationMultiplier(
                runtimeServices.EquipmentSystem, recipe, context);
        }

        public bool TryOpenChest(ChestProgress progress, string id)
        {
            if (!initialized || progress == null || string.IsNullOrWhiteSpace(id) ||
                shell != null && shell.Screen != GameShellScreen.Gameplay || Time.timeScale <= 0f ||
                !progress.TryGetContents(id, out _)) return false;
            CancelEquipmentDrag();
            storageObjectId = string.Empty;
            chestProgress = progress;
            chestId = id;
            selectedIndex = 0;
            SetOpen(true);
            Refresh();
            Debug.Log($"[Nyangbingo] Chest loot storage opened: id={id}, slots={ChestProgress.StorageSlotCount}.");
            return true;
        }

        public static bool IsSmeltingStation(CraftingStation station) =>
            station == CraftingStation.Furnace || station == CraftingStation.Foundry;

        public static bool CanToggleCraftingSmelting(CraftingStation station) =>
            IsSmeltingStation(station);

        public bool FurnaceSmeltingViewActive => furnaceSmeltingView;

        public bool TryToggleFurnaceCraftingSmeltingView()
        {
            if (page != Page.Crafting || !IsSmeltingStation(openedStation)) return false;
            furnaceSmeltingView = !furnaceSmeltingView;
            RebuildFilteredRecipes();
            selectedIndex = FindFirstEntryForFilter();
            message = string.Empty;
            Refresh();
            ResetDetailsScroll();
            ResetCraftingListScroll();
            return true;
        }

        private void ApplySketchWindowLayout()
        {
            var canvas = panel.GetComponentInParent<Canvas>()?.rootCanvas;
            if (canvas == null || inventoryGridRoot == null || equipmentVisualRoot == null) return;

            const float cell = HudInventoryCellSize;
            const float gap = HudInventoryCellGap;
            const int columns = HudInventoryColumns;
            const int visibleRows = 4;
            // 트랙(3px)·핸들(5px) 원본은 격자(14px) 옆에서 너무 가늘다.
            // preserveAspect를 끄고 막대 자체를 8px로 늘려 트랙과 핸들의 중심을 맞춘다.
            const float barWidth = 8f;
            const float barGap = 2f;
            var slotCount = inventoryGridButtons != null ? inventoryGridButtons.Length : 50;
            var contentRows = Mathf.Max(visibleRows, Mathf.CeilToInt(slotCount / (float)columns));
            var contentWidth = columns * cell + (columns - 1) * gap;
            var contentHeight = contentRows * cell + (contentRows - 1) * gap;
            var viewHeight = visibleRows * cell + (visibleRows - 1) * gap;
            var layout = inventoryGridRoot.GetComponent<GridLayoutGroup>();
            if (layout != null)
            {
                layout.padding = new RectOffset(0, 0, 0, 0);
                layout.childAlignment = TextAnchor.UpperLeft;
                layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
                layout.startAxis = GridLayoutGroup.Axis.Horizontal;
                layout.cellSize = new Vector2(cell, cell);
                layout.spacing = new Vector2(gap, gap);
                layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                layout.constraintCount = columns;
            }

            var grid = (RectTransform)inventoryGridRoot.transform;
            if (inventoryScrollRoot == null)
                BuildInventoryScrollWindow(canvas.transform, grid);
            var shell = (RectTransform)inventoryScrollRoot.transform;
            shell.SetParent(canvas.transform, false);
            shell.anchorMin = shell.anchorMax = new Vector2(0f, 1f);
            shell.pivot = new Vector2(0f, 1f);
            shell.anchoredPosition = new Vector2(4f, -4f);
            shell.sizeDelta = new Vector2(contentWidth + barGap + barWidth, viewHeight);
            var viewport = (RectTransform)shell.Find("InventoryViewport");
            viewport.sizeDelta = new Vector2(contentWidth, viewHeight);
            grid.anchorMin = grid.anchorMax = new Vector2(0f, 1f);
            grid.pivot = new Vector2(0f, 1f);
            grid.anchoredPosition = Vector2.zero;
            grid.localScale = Vector3.one;
            grid.sizeDelta = new Vector2(contentWidth, contentHeight);
            var bar = (RectTransform)shell.Find("InventoryScrollbar");
            bar.anchoredPosition = new Vector2(contentWidth + barGap, 0f);
            bar.sizeDelta = new Vector2(barWidth, viewHeight);
            if (bar.GetComponent<Image>() is Image trackImage)
                trackImage.preserveAspect = false;
            var handleImage = bar.Find("Handle")?.GetComponent<Image>();
            if (handleImage != null) handleImage.preserveAspect = false;
            shell.SetAsLastSibling();
            FitInventorySlotContents(cell);
            ApplySideHudVisibility();

            var equip = (RectTransform)equipmentVisualRoot.transform;
            equip.SetParent(canvas.transform, false);
            equip.anchorMin = equip.anchorMax = new Vector2(1f, 1f);
            equip.pivot = new Vector2(1f, 1f);
            // 체력 틀은 96×64, 위 여백 4. 발톱 칸이 틀 맨 위에 있으므로 틀 아래까지 내려야 드롭이 닿는다.
            equip.anchoredPosition = new Vector2(-4f, -74f);
            equip.localScale = new Vector3(.42f, .42f, 1f);
            equip.SetAsLastSibling();
            ApplySideHudVisibility();
            RefreshEquipmentVisual();

            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0f, 0f);
            panelRect.pivot = new Vector2(0f, 0f);
            panelRect.anchoredPosition = new Vector2(4f, 4f);
            panelRect.sizeDelta = new Vector2(220f, 100f);

            var tabPositions = new[]
            {
                new Vector2(-78f, 30f),
                new Vector2(-78f, 12f),
                new Vector2(-78f, -6f),
                new Vector2(-78f, -24f)
            };
            var tabLabels = new[] { "재료", "제작", "장비", "도감" };
            for (var index = 0; index < tabButtons.Length && index < tabPositions.Length; index++)
            {
                var rect = tabButtons[index].GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = tabPositions[index];
                rect.sizeDelta = new Vector2(56f, 16f);
                var label = tabButtons[index].GetComponentInChildren<Text>();
                if (label != null) label.text = tabLabels[index];
                tabButtons[index].gameObject.SetActive(false);
            }

            // 체력 틀은 위 여백 4 + 높이 64. 슬롯은 루트 위쪽보다 약 5px 아래에서 시작하므로
            // 루트를 74만큼 내려 칸이 틀과 겹치지 않게 한다.
            equip.anchoredPosition = new Vector2(-4f, -74f);
            var titleRect = titleText.rectTransform;
            titleRect.anchoredPosition = new Vector2(46f, 38f);
            titleRect.sizeDelta = new Vector2(110f, 14f);
        }

        private void FitInventorySlotContents(float cell)
        {
            if (inventoryGridButtons == null) return;
            var inner = Mathf.Max(1f, cell - 2f);
            foreach (var button in inventoryGridButtons)
            {
                if (button == null) continue;
                for (var index = 0; index < button.transform.childCount; index++)
                {
                    if (button.transform.GetChild(index) is not RectTransform child || child.name == "Label")
                        continue;
                    child.anchorMin = child.anchorMax = new Vector2(.5f, .5f);
                    child.pivot = new Vector2(.5f, .5f);
                    child.anchoredPosition = Vector2.zero;
                    child.sizeDelta = new Vector2(inner, inner);
                    if (child.GetComponent<Image>() is Image childImage)
                        childImage.preserveAspect = true;
                }
            }
        }

        private void BuildInventoryScrollWindow(Transform canvas, RectTransform grid)
        {
            inventoryScrollRoot = new GameObject("InventoryScroll", typeof(RectTransform));
            inventoryScrollRoot.transform.SetParent(canvas, false);

            var viewportObject = new GameObject("InventoryViewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            viewportObject.transform.SetParent(inventoryScrollRoot.transform, false);
            var viewport = (RectTransform)viewportObject.transform;
            viewport.anchorMin = viewport.anchorMax = new Vector2(0f, 1f);
            viewport.pivot = new Vector2(0f, 1f);
            viewport.anchoredPosition = Vector2.zero;
            var viewportImage = viewportObject.GetComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, .02f);
            viewportImage.raycastTarget = true;
            grid.SetParent(viewport, false);

            inventoryScrollRect = viewportObject.AddComponent<ScrollRect>();
            inventoryScrollRect.content = grid;
            inventoryScrollRect.viewport = viewport;
            inventoryScrollRect.horizontal = false;
            inventoryScrollRect.vertical = true;
            inventoryScrollRect.movementType = ScrollRect.MovementType.Clamped;
            inventoryScrollRect.scrollSensitivity = 16f;
            inventoryScrollRect.inertia = false;

            var barObject = new GameObject("InventoryScrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            barObject.transform.SetParent(inventoryScrollRoot.transform, false);
            var barRect = (RectTransform)barObject.transform;
            barRect.anchorMin = barRect.anchorMax = new Vector2(0f, 1f);
            barRect.pivot = new Vector2(0f, 1f);
            var track = barObject.GetComponent<Image>();
            track.raycastTarget = true;
            if (gameplayArtCatalog?.ScrollTrack != null)
            {
                track.sprite = gameplayArtCatalog.ScrollTrack;
                track.type = Image.Type.Simple;
                track.preserveAspect = false;
                track.color = Color.white;
            }
            else track.color = new Color(.15f, .22f, .3f, .95f);

            var handleObject = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleObject.transform.SetParent(barObject.transform, false);
            var handleRect = (RectTransform)handleObject.transform;
            handleRect.anchorMin = Vector2.zero;
            handleRect.anchorMax = Vector2.one;
            handleRect.offsetMin = Vector2.zero;
            handleRect.offsetMax = Vector2.zero;
            var handle = handleObject.GetComponent<Image>();
            handle.raycastTarget = true;
            if (gameplayArtCatalog?.ScrollHandle != null)
            {
                handle.sprite = gameplayArtCatalog.ScrollHandle;
                handle.type = Image.Type.Simple;
                handle.preserveAspect = false;
                handle.color = Color.white;
            }
            else handle.color = new Color(.75f, .85f, .95f, 1f);

            var scrollbar = barObject.GetComponent<Scrollbar>();
            scrollbar.handleRect = handleRect;
            scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            inventoryScrollRect.verticalScrollbar = scrollbar;
            inventoryScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        }

        private void BuildCraftWindow(Transform canvas)
        {
            const int columns = CraftWindowColumns;
            const int visibleRows = 3;
            craftWindowRoot = new GameObject("CraftWindow", typeof(RectTransform), typeof(Image));
            craftWindowRoot.transform.SetParent(canvas, false);
            var root = (RectTransform)craftWindowRoot.transform;
            root.anchorMin = root.anchorMax = new Vector2(0f, 0f);
            root.pivot = new Vector2(0f, 0f);
            root.anchoredPosition = new Vector2(4f, 4f);
            var frame = craftWindowRoot.GetComponent<Image>();
            craftWindowFrame = frame;
            var categoryBars = gameplayArtCatalog?.CraftCategoryBarFrames;
            var useCategoryArt = categoryBars != null && categoryBars.Count > 0 && categoryBars[0] != null;
            var topBars = gameplayArtCatalog?.CraftTopBarFrames;
            var frameSprite = useCategoryArt
                ? categoryBars[0]
                : topBars != null && topBars.Count > 0 ? topBars[0] : gameplayArtCatalog?.CraftPanel;
            var frameSize = frameSprite != null ? frameSprite.rect.size : new Vector2(108f, 78f);
            root.sizeDelta = frameSize;
            frame.sprite = frameSprite;
            frame.type = Image.Type.Simple;
            frame.preserveAspect = true;
            frame.color = frameSprite != null ? Color.white : new Color(.08f, .1f, .16f, .96f);
            frame.raycastTarget = true;

            craftWindowOutputIcon = CreateArtImage(root, "Output", null, Vector2.zero, new Vector2(22f, 22f));
            PlaceTopLeft(craftWindowOutputIcon.rectTransform, 9f, 9f, 22f, 22f);
            // PreserveAspect aligns unused space using the RectTransform pivot.
            // Use a centred pivot like the recipe grid, keeping the same preview bounds.
            craftWindowOutputIcon.rectTransform.pivot = Vector2.one * .5f;
            craftWindowOutputIcon.rectTransform.anchoredPosition = new Vector2(20f, -20f);
            craftWindowOutputIcon.preserveAspect = true;
            var outputButton = craftWindowOutputIcon.gameObject.AddComponent<Button>();
            outputButton.targetGraphic = craftWindowOutputIcon;
            outputButton.transition = Selectable.Transition.None;
            craftWindowOutputIcon.raycastTarget = true;
            outputButton.onClick.AddListener(() => ShowCraftOutputDescription(displayedCraftOutputId));
            WireInventoryPointer(outputButton, pointer =>
            {
                if (pointer.button == PointerEventData.InputButton.Right && outputButton.interactable)
                    ShowCraftOutputDescription(displayedCraftOutputId);
            });
            for (var index = 0; index < craftWindowIngredientIcons.Length; index++)
            {
                var icon = CreateArtImage(root, $"Ingredient_{index + 1}", null, Vector2.zero, new Vector2(8f, 8f));
                PlaceTopLeft(icon.rectTransform, 8f + index * 9f, 45f, 8f, 8f);
                icon.preserveAspect = true;
                var ingredientIndex = index;
                var ingredientButton = icon.gameObject.AddComponent<Button>();
                ingredientButton.targetGraphic = icon;
                ingredientButton.transition = Selectable.Transition.None;
                icon.raycastTarget = true;
                ingredientButton.onClick.AddListener(() => ShowIngredientDescription(ingredientIndex));
                WireInventoryPointer(ingredientButton, pointer =>
                {
                    // Button.onClick handles left clicks; handle right clicks here once.
                    if (pointer.button == PointerEventData.InputButton.Right && ingredientButton.interactable)
                        ShowIngredientDescription(ingredientIndex);
                });
                craftWindowIngredientIcons[index] = icon;
                var count = CreateText(icon.transform, "RequiredCount", 8,
                    TextAnchor.LowerRight, new Vector2(8f, 8f), Vector2.zero);
                count.raycastTarget = false;
                craftWindowIngredientCounts[index] = count;
            }

            var buttonFrames = gameplayArtCatalog?.CraftButtonFrames;
            var normal = buttonFrames != null && buttonFrames.Count > 0 ? buttonFrames[0] : null;
            var buttonSize = normal != null ? normal.rect.size : new Vector2(32f, 14f);
            var buttonObject = new GameObject("CraftButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(root, false);
            var buttonRect = (RectTransform)buttonObject.transform;
            // 왼쪽 칸(x=1~36) 가로 가운데. 세로는 위 칸 바닥(y=59)과 창 바닥(y=76) 사이 가운데.
            const float craftColumnX = 1f;
            const float craftColumnWidth = 36f;
            const float craftFooterTop = 59f;
            const float craftFooterBottom = 76f;
            PlaceTopLeft(buttonRect,
                craftColumnX + (craftColumnWidth - buttonSize.x) * .5f,
                craftFooterTop + (craftFooterBottom - craftFooterTop - buttonSize.y) * .5f + 1f,
                buttonSize.x, buttonSize.y);
            var buttonImage = buttonObject.GetComponent<Image>();
            buttonImage.sprite = normal;
            buttonImage.type = Image.Type.Simple;
            buttonImage.preserveAspect = true;
            buttonImage.color = normal != null ? Color.white : new Color(.45f, .42f, .62f, 1f);
            var craftButton = buttonObject.GetComponent<Button>();
            craftWindowActionButton = craftButton;
            craftWindowActionImage = buttonImage;
            craftButton.targetGraphic = buttonImage;
            if (normal != null && buttonFrames.Count > 1 && buttonFrames[1] != null)
            {
                var state = craftButton.spriteState;
                state.pressedSprite = buttonFrames[1];
                state.highlightedSprite = null;
                state.selectedSprite = null;
                craftButton.spriteState = state;
                craftButton.transition = Selectable.Transition.SpriteSwap;
            }
            craftButton.onClick.AddListener(OnCraftWindowAction);
            BuildCraftCategoryChrome(root);

            var slotSprite = gameplayArtCatalog?.CraftMidSlot;
            var cell = slotSprite != null ? slotSprite.rect.size : new Vector2(18f, 18f);
            const float holeX = 37f;
            const float holeY = 17f;
            const float holeW = 58f;
            const float holeH = 57f;
            var gridW = cell.x * columns;
            var gridH = cell.y * visibleRows;
            var gridX = holeX + Mathf.Max(0f, (holeW - gridW) * .5f);
            var gridY = holeY + Mathf.Max(0f, (holeH - gridH) * .5f);

            var viewportObject = new GameObject("RecipeViewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            viewportObject.transform.SetParent(root, false);
            var viewport = (RectTransform)viewportObject.transform;
            PlaceTopLeft(viewport, gridX, gridY, gridW, gridH);
            var viewportImage = viewportObject.GetComponent<Image>();
            viewportImage.color = new Color(24f / 255f, 31f / 255f, 47f / 255f, 1f);
            viewportImage.raycastTarget = true;

            var largestCraftList = visibleRecipes.GroupBy(recipe =>
                    (recipe.Station, Category: ItemCraftUiCategory.IndexOf(recipe.Output.item)))
                .Select(group => group.Count()).DefaultIfEmpty(0).Max();
            var largestSmeltingList = gameDataCatalog.Smelting.Where(recipe => recipe != null)
                .GroupBy(recipe => recipe.StationKind)
                .Select(group => group.Count()).DefaultIfEmpty(0).Max();
            var largestList = Mathf.Max(largestCraftList, largestSmeltingList);
            // Share one capacity across stations/categories, with exactly one spare full row.
            var slotCount = (Mathf.CeilToInt(largestList / (float)columns) + 1) * columns;
            var rows = Mathf.CeilToInt(slotCount / (float)columns);
            var contentObject = new GameObject("RecipeGrid", typeof(RectTransform), typeof(GridLayoutGroup));
            contentObject.transform.SetParent(viewport, false);
            var content = (RectTransform)contentObject.transform;
            PlaceTopLeft(content, 0f, 0f, gridW, rows * cell.y);
            var layout = contentObject.GetComponent<GridLayoutGroup>();
            layout.cellSize = cell;
            layout.spacing = Vector2.zero;
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            layout.startAxis = GridLayoutGroup.Axis.Horizontal;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = columns;

            var scroll = viewportObject.AddComponent<ScrollRect>();
            craftWindowScrollRect = scroll;
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 12f;
            scroll.inertia = false;

            var scrollSprite = gameplayArtCatalog?.CraftScroll;
            // 프레임 오른쪽 홈(108×78 스프라이트 기준). 홈 중심 x=101.5, 세로 y=16~74.
            // 손잡이 원본은 7×13이라 홈보다 가늘고, 비율 유지를 켜면 더 작게 그려진다.
            const float scrollGrooveCenterX = 101.5f;
            const float scrollGrooveY = 16f;
            const float scrollGrooveH = 58f;
            const float scrollWidth = 12f;
            var barObject = new GameObject("RecipeScrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            barObject.transform.SetParent(root, false);
            var barRect = (RectTransform)barObject.transform;
            PlaceTopLeft(barRect,
                scrollGrooveCenterX - scrollWidth * .5f,
                scrollGrooveY,
                scrollWidth,
                scrollGrooveH);
            var track = barObject.GetComponent<Image>();
            track.color = new Color(1f, 1f, 1f, 0f);
            track.raycastTarget = true;
            var handleObject = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleObject.transform.SetParent(barObject.transform, false);
            var handleRect = (RectTransform)handleObject.transform;
            handleRect.anchorMin = Vector2.zero;
            handleRect.anchorMax = Vector2.one;
            handleRect.offsetMin = Vector2.zero;
            handleRect.offsetMax = Vector2.zero;
            var handle = handleObject.GetComponent<Image>();
            handle.sprite = scrollSprite;
            handle.type = Image.Type.Simple;
            handle.preserveAspect = false;
            handle.color = handle.sprite != null ? Color.white : new Color(.75f, .8f, .9f, 1f);
            var scrollbar = barObject.GetComponent<Scrollbar>();
            scrollbar.handleRect = handleRect;
            scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            craftWindowSlotButtons = new Button[slotCount];
            craftWindowSlotIcons = new Image[slotCount];
            craftWindowSlotMarks = new Image[slotCount];
            for (var index = 0; index < slotCount; index++)
            {
                var captured = index;
                var slotObject = new GameObject($"RecipeSlot_{index + 1:00}", typeof(RectTransform), typeof(Image), typeof(Button));
                slotObject.transform.SetParent(content, false);
                var slotImage = slotObject.GetComponent<Image>();
                slotImage.color = new Color(1f, 1f, 1f, 0f);
                var slotButton = slotObject.GetComponent<Button>();
                slotButton.targetGraphic = slotImage;
                slotButton.onClick.AddListener(() => SelectCraftWindowSlot(captured));
                WireInventoryPointer(slotButton, pointer =>
                {
                    if (pointer.button == PointerEventData.InputButton.Right && slotButton.interactable)
                        SelectCraftWindowSlot(captured);
                });
                var mark = CreateArtImage(slotObject.transform, "Selected", slotSprite, Vector2.zero, cell);
                mark.preserveAspect = true;
                mark.color = Color.white;
                mark.enabled = false;
                var frameImage = CreateArtImage(slotObject.transform, "Frame", slotSprite, Vector2.zero, cell);
                frameImage.preserveAspect = true;
                frameImage.color = slotSprite != null ? Color.white : new Color(.12f, .16f, .24f, 1f);
                var icon = CreateArtImage(slotObject.transform, "Icon", null, Vector2.zero, new Vector2(12f, 12f));
                icon.preserveAspect = true;
                craftWindowSlotButtons[index] = slotButton;
                craftWindowSlotIcons[index] = icon;
                craftWindowSlotMarks[index] = mark;
            }
            if (craftWindowToggleButton != null) craftWindowToggleButton.transform.SetAsLastSibling();
            craftWindowRoot.SetActive(false);
        }

        private static void PlaceTopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
        }

        private void ApplyCraftWindowChrome(bool useArtWindow)
        {
            if (panel == null) return;
            var background = panel.GetComponent<Image>();
            if (background != null)
            {
                if (!craftWindowPanelTintSaved)
                {
                    craftWindowPanelColor = background.color;
                    craftWindowPanelTintSaved = true;
                }
                var color = craftWindowPanelColor;
                if (useArtWindow) color.a = 0f;
                background.color = color;
                background.raycastTarget = !useArtWindow;
            }
            if (!useArtWindow || craftWindowRoot == null) return;
            foreach (var tab in tabButtons)
                if (tab != null) tab.gameObject.SetActive(false);
            if (previousButton != null) previousButton.gameObject.SetActive(false);
            if (nextButton != null) nextButton.gameObject.SetActive(false);
            if (primaryButton != null) primaryButton.gameObject.SetActive(false);
            if (messageText != null) messageText.gameObject.SetActive(false);
            if (titleText != null) titleText.gameObject.SetActive(false);
            if (craftingListRoot != null) craftingListRoot.SetActive(false);
            if (detailsViewportRect != null) detailsViewportRect.gameObject.SetActive(false);
            if (detailsScrollbarRect != null) detailsScrollbarRect.gameObject.SetActive(false);
            if (debugCompleteButton != null) debugCompleteButton.gameObject.SetActive(false);
        }

        private void RefreshCraftWindow()
        {
            if (runtimeServices == null || !runtimeServices.IsInitialized || panel == null) return;
            if (craftWindowRoot == null)
            {
                if (gameplayArtCatalog == null) return;
                var canvas = panel.GetComponentInParent<Canvas>()?.rootCanvas;
                if (canvas == null) return;
                BuildCraftWindow(canvas.transform);
            }
            var smelting = IsShowingSmeltingList;
            var show = sideHudVisible && !HasStorageMode && (!open || page == Page.Crafting);
            var wasActive = craftWindowRoot.activeSelf;
            craftWindowRoot.SetActive(show);
            if (!show) return;
            if (!wasActive) craftWindowRoot.transform.SetAsLastSibling();
            ApplyCraftCategoryPresentation(smelting);
            if (!smelting && !(open && page == Page.Crafting)) RebuildCraftWindowRecipes();
            var recipes = open && page == Page.Crafting ? filteredRecipes : craftWindowRecipes;
            var selected = smelting
                ? (smeltingRecipes.Count == 0 ? -1 : Mathf.Clamp(selectedIndex, 0, smeltingRecipes.Count - 1))
                : CurrentCraftWindowIndex(recipes);
            var recipe = !smelting && selected >= 0 && selected < recipes.Count ? recipes[selected] : null;
            var smelt = smelting && selected >= 0 && selected < smeltingRecipes.Count
                ? smeltingRecipes[selected] : null;
            if (craftWindowOutputIcon != null)
            {
                var output = smelting ? smelt?.Output.item : recipe?.Output.item;
                displayedCraftOutputId = output?.Id;
                craftWindowOutputIcon.GetComponent<Button>().interactable = output != null;
                craftWindowOutputIcon.sprite = output != null ? itemArtCatalog?.FindSprite(output.Id) : null;
                craftWindowOutputIcon.enabled = craftWindowOutputIcon.sprite != null;
            }
            for (var index = 0; index < craftWindowIngredientIcons.Length; index++)
            {
                var icon = craftWindowIngredientIcons[index];
                if (icon == null) continue;
                ItemDefinition ingredientItem = null;
                var required = 0;
                if (smelting && smelt != null)
                {
                    ItemAmount amount = default;
                    if (index == 0) amount = smelt.Input;
                    else if (index == 1) amount = smelt.Fuel;
                    ingredientItem = amount.item;
                    required = amount.amount;
                }
                else if (recipe?.Ingredients != null && index < recipe.Ingredients.Length)
                {
                    ingredientItem = recipe.Ingredients[index].item;
                    required = recipe.Ingredients[index].amount;
                }
                icon.sprite = ingredientItem != null ? itemArtCatalog?.FindSprite(ingredientItem.Id) : null;
                icon.enabled = icon.sprite != null;
                var missing = ingredientItem != null && required > 0 && runtimeServices.PlayerInventory != null &&
                              runtimeServices.PlayerInventory.Count(ingredientItem.Id) < required;
                icon.color = missing ? new Color(.5f, .18f, .18f, 1f) : Color.white;
                displayedIngredientIds[index] = ingredientItem?.Id;
                displayedIngredientMissing[index] = missing;
                var ingredientButton = icon.GetComponent<Button>();
                if (ingredientButton != null) ingredientButton.interactable = missing;
                ShowCraftIngredientCount(icon, craftWindowIngredientCounts[index],
                    ingredientItem != null ? required : 0);
            }
            for (var index = 0; index < craftWindowSlotButtons.Length; index++)
            {
                var button = craftWindowSlotButtons[index];
                if (button == null) continue;
                ItemDefinition output = null;
                var hasEntry = false;
                if (smelting)
                {
                    hasEntry = index < smeltingRecipes.Count && smeltingRecipes[index] != null;
                    output = hasEntry ? smeltingRecipes[index].Output.item : null;
                }
                else
                {
                    var entry = index < recipes.Count ? recipes[index] : null;
                    hasEntry = entry != null;
                    output = entry?.Output.item;
                }
                button.interactable = hasEntry;
                var icon = craftWindowSlotIcons[index];
                if (icon != null)
                {
                    icon.sprite = output != null ? itemArtCatalog?.FindSprite(output.Id) : null;
                    icon.enabled = icon.sprite != null;
                }
                if (craftWindowSlotMarks[index] != null)
                    craftWindowSlotMarks[index].enabled = false;
                if (button.targetGraphic is Image background)
                    RefreshSlotSelectionBorder(background, hasEntry && index == selected);
            }
            if (open && page == Page.Crafting) HideCraftingTextChrome();
        }

        private void HideCraftingTextChrome()
        {
            if (previousButton != null) previousButton.gameObject.SetActive(false);
            if (nextButton != null) nextButton.gameObject.SetActive(false);
            if (primaryButton != null) primaryButton.gameObject.SetActive(false);
            if (collectButton != null) collectButton.gameObject.SetActive(false);
            if (messageText != null) messageText.gameObject.SetActive(false);
            if (titleText != null) titleText.gameObject.SetActive(false);
            if (craftingListRoot != null) craftingListRoot.SetActive(false);
            if (detailsViewportRect != null) detailsViewportRect.gameObject.SetActive(false);
            if (detailsScrollbarRect != null) detailsScrollbarRect.gameObject.SetActive(false);
            if (debugCompleteButton != null) debugCompleteButton.gameObject.SetActive(false);
        }

        private void RebuildCraftWindowRecipes()
        {
            craftWindowRecipes.Clear();
            if (visibleRecipes.Count == 0 && gameDataCatalog != null) RebuildVisibleRecipes();
            var nearby = open && page == Page.Crafting ? NearbyStation() : CraftingStation.None;
            var smithyUnlocked = runtimeServices?.Seokbinggo?.IsSmithyUnlocked == true;
            for (var index = 0; index < visibleRecipes.Count; index++)
            {
                var recipe = visibleRecipes[index];
                if (recipe == null) continue;
                if (!IsRecipeVisibleAtStation(recipe.Station, nearby)) continue;
                if (!IsSmithyRecipeAllowed(recipe.Station, smithyUnlocked)) continue;
                if (!RecipeMatchesCraftCategory(recipe)) continue;
                if (RecipeUnlockPolicy.IsUnlocked(recipe, runtimeServices.RecipeBook))
                    craftWindowRecipes.Add(recipe);
            }
            if (craftWindowRecipes.Count == 0) craftWindowIndex = 0;
            else craftWindowIndex = Mathf.Clamp(craftWindowIndex, 0, craftWindowRecipes.Count - 1);
        }

        private int CurrentCraftWindowIndex(IReadOnlyList<RecipeDefinition> recipes)
        {
            if (recipes == null || recipes.Count == 0) return -1;
            var index = open && page == Page.Crafting ? selectedIndex : craftWindowIndex;
            return Mathf.Clamp(index, 0, recipes.Count - 1);
        }

        private void SelectCraftWindowRecipe(int index)
        {
            var recipes = open && page == Page.Crafting ? filteredRecipes : craftWindowRecipes;
            if (index < 0 || index >= recipes.Count) return;
            if (open && page == Page.Crafting) selectedIndex = index;
            else craftWindowIndex = index;
            Refresh();
            EnsureCraftGridSelectionVisible();
        }

        private void EnsureCraftGridSelectionVisible()
        {
            if (craftWindowScrollRect == null || !craftWindowScrollRect.gameObject.activeInHierarchy) return;
            var index = open && page == Page.Crafting ? selectedIndex : craftWindowIndex;
            if (index < 0 || index >= craftWindowSlotButtons.Length) return;
            Canvas.ForceUpdateCanvases();
            var viewport = craftWindowScrollRect.viewport;
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, craftWindowSlotButtons[index].transform);
            var offset = bounds.max.y > viewport.rect.yMax ? viewport.rect.yMax - bounds.max.y
                : bounds.min.y < viewport.rect.yMin ? viewport.rect.yMin - bounds.min.y : 0f;
            craftWindowScrollRect.StopMovement();
            craftWindowScrollRect.content.anchoredPosition += new Vector2(0f, offset);
        }

        private void SelectCraftWindowSlot(int index)
        {
            if (IsShowingSmeltingList)
            {
                if (index < 0 || index >= smeltingRecipes.Count) return;
                selectedIndex = index;
                Refresh();
                return;
            }
            SelectCraftWindowRecipe(index);
        }

        private bool RecipeMatchesCraftCategory(RecipeDefinition recipe) =>
            ItemCraftUiCategory.IndexOf(recipe?.Output.item) == craftCategoryIndex;

        private void SelectCraftCategory(int index)
        {
            if (index < 0 || index >= ItemCraftUiCategory.Labels.Length) return;
            if (IsShowingSmeltingList) furnaceSmeltingView = false;
            craftCategoryIndex = index;
            craftWindowIndex = 0;
            selectedIndex = 0;
            if (open && page == Page.Crafting) RebuildFilteredRecipes();
            else RebuildCraftWindowRecipes();
            Refresh();
            ResetCraftingListScroll();
        }

        private void OnCraftWindowAction()
        {
            if (IsShowingSmeltingList) TrySmeltSelected();
            else TryCraftFromWindow();
        }

        private void OnCraftViewToggle()
        {
            var nearby = stationSource != null ? stationSource.NearbyCraftingStation : CraftingStation.None;
            if (page == Page.Crafting && IsSmeltingStation(openedStation) && furnaceSmeltingView)
            {
                furnaceSmeltingView = false;
                RebuildFilteredRecipes();
                if (open) selectedIndex = FindFirstEntryForFilter();
                else
                {
                    craftWindowIndex = 0;
                    RebuildCraftWindowRecipes();
                }
                message = string.Empty;
                Refresh();
                ResetCraftingListScroll();
                return;
            }
            if (!open || page != Page.Crafting)
            {
                if (!IsSmeltingStation(nearby) || !TryOpenForStation(nearby))
                {
                    ShowMessage("화로나 대장간 근처에서 제련으로 바꿀 수 있습니다.");
                    return;
                }
            }
            if (!TryToggleFurnaceCraftingSmeltingView())
                ShowMessage("화로나 대장간 근처에서 제련으로 바꿀 수 있습니다.");
        }

        private void BuildCraftCategoryChrome(RectTransform root)
        {
            var tabXs = new[] { 39f, 53f, 67f, 81f };
            craftCategoryButtons = new Button[tabXs.Length];
            craftCategoryLabels = Array.Empty<Text>();
            for (var index = 0; index < tabXs.Length; index++)
            {
                var captured = index;
                var buttonObject = new GameObject($"CraftCategory_{index}", typeof(RectTransform), typeof(Image), typeof(Button));
                buttonObject.transform.SetParent(root, false);
                PlaceTopLeft((RectTransform)buttonObject.transform, tabXs[index], 4f, 14f, 8f);
                var image = buttonObject.GetComponent<Image>();
                image.color = new Color(1f, 1f, 1f, 0f);
                var button = buttonObject.GetComponent<Button>();
                button.targetGraphic = image;
                button.onClick.AddListener(() => SelectCraftCategory(captured));
                craftCategoryButtons[index] = button;
            }

            var toggleFrames = gameplayArtCatalog?.CraftViewToggleFrames;
            var toggleNormal = toggleFrames != null && toggleFrames.Count > 0 ? toggleFrames[0] : null;
            var toggleSize = toggleNormal != null ? toggleNormal.rect.size : new Vector2(13f, 16f);
            var toggleObject = new GameObject("CraftViewToggle", typeof(RectTransform), typeof(Image), typeof(Button));
            toggleObject.transform.SetParent(root, false);
            // 오른쪽 테두리 픽셀까지 붙인다. 칸 안에만 두면 테두리와 한 픽셀 틈이 남는다.
            PlaceTopLeft((RectTransform)toggleObject.transform,
                root.sizeDelta.x - toggleSize.x + 1f, 1f, toggleSize.x, toggleSize.y);
            toggleObject.transform.SetAsLastSibling();
            var toggleImage = toggleObject.GetComponent<Image>();
            toggleImage.sprite = toggleNormal;
            toggleImage.type = Image.Type.Simple;
            toggleImage.preserveAspect = true;
            toggleImage.color = toggleNormal != null ? Color.white : new Color(.45f, .42f, .62f, 1f);
            craftWindowToggleButton = toggleObject.GetComponent<Button>();
            craftWindowToggleButton.targetGraphic = toggleImage;
            if (toggleNormal != null && toggleFrames.Count > 1 && toggleFrames[1] != null)
            {
                var state = craftWindowToggleButton.spriteState;
                state.pressedSprite = toggleFrames[1];
                state.highlightedSprite = toggleFrames[1];
                craftWindowToggleButton.spriteState = state;
                craftWindowToggleButton.transition = Selectable.Transition.SpriteSwap;
            }
            craftWindowToggleButton.onClick.AddListener(OnCraftViewToggle);
        }

        private void ApplyCraftCategoryPresentation(bool smelting)
        {
            if (craftWindowFrame != null)
            {
                Sprite frameSprite = null;
                if (smelting) frameSprite = gameplayArtCatalog?.CraftSmeltPanel;
                var bars = gameplayArtCatalog?.CraftCategoryBarFrames;
                if (!smelting && bars != null && craftCategoryIndex >= 0 && craftCategoryIndex < bars.Count)
                    frameSprite = bars[craftCategoryIndex];
                if (frameSprite != null)
                {
                    craftWindowFrame.sprite = frameSprite;
                    craftWindowFrame.color = Color.white;
                    var root = craftWindowFrame.rectTransform;
                    if (root.sizeDelta != frameSprite.rect.size) root.sizeDelta = frameSprite.rect.size;
                }
            }
            for (var index = 0; index < craftCategoryButtons.Length; index++)
            {
                if (craftCategoryButtons[index] != null)
                    craftCategoryButtons[index].gameObject.SetActive(!smelting);
                if (index < craftCategoryLabels.Length && craftCategoryLabels[index] != null)
                    craftCategoryLabels[index].color = index == craftCategoryIndex
                        ? new Color(.96f, .97f, 1f, 1f)
                        : new Color(.62f, .66f, .78f, 1f);
            }
            if (craftWindowToggleButton != null)
                craftWindowToggleButton.gameObject.SetActive(
                    open && page == Page.Crafting &&
                    (IsShowingSmeltingList || StationOffersCraftAndSmelt(openedStation)));
            var frames = smelting
                ? gameplayArtCatalog?.CraftSmeltButtonFrames
                : gameplayArtCatalog?.CraftButtonFrames;
            var normal = frames != null && frames.Count > 0 ? frames[0] : null;
            if (craftWindowActionImage == null || normal == null) return;
            craftWindowActionImage.sprite = normal;
            craftWindowActionImage.color = Color.white;
            if (craftWindowActionButton == null || frames.Count < 2 || frames[1] == null) return;
            var buttonState = craftWindowActionButton.spriteState;
            buttonState.pressedSprite = frames[1];
            buttonState.highlightedSprite = frames[1];
            craftWindowActionButton.spriteState = buttonState;
            craftWindowActionButton.transition = Selectable.Transition.SpriteSwap;
        }

        private void SetInventoryWindowActive(bool active)
        {
            var wasActive = inventoryScrollRoot != null && inventoryScrollRoot.activeSelf;
            if (inventoryScrollRoot != null) inventoryScrollRoot.SetActive(active);
            if (craftWindowRoot != null && !active) craftWindowRoot.SetActive(false);
            if (inventoryGridRoot != null) inventoryGridRoot.SetActive(active);
            if (!active || wasActive || inventoryScrollRect == null) return;
            inventoryScrollRect.StopMovement();
            inventoryScrollRect.verticalNormalizedPosition = 1f;
        }

        private void BuildUi()
        {
            if (panel == null || titleText == null || tabButtons == null || tabButtons.Length != 4)
            {
                Debug.LogError("[Nyangbingo] MainGameCraftingUiController: CraftingAndSmeltingPanel 하이어라키가 인스펙터에 배선되지 않았습니다.");
                return;
            }
            tabButtons[0].onClick.AddListener(() => TogglePage(Page.Gathering));
            // An opaque panel keeps the gameplay clock from showing through its title.
            var panelBackground = panel.GetComponent<Image>();
            if (panelBackground != null)
            {
                var backgroundColor = panelBackground.color;
                backgroundColor.a = 1f;
                panelBackground.color = backgroundColor;
            }
            tabButtons[1].onClick.AddListener(() => TogglePage(Page.Crafting));
            tabButtons[2].onClick.AddListener(() => TogglePage(Page.Equipment));
            tabButtons[3].onClick.AddListener(() => TogglePage(Page.Codex));
            for (var index = 0; index < tabButtons.Length; index++)
            {
                var label = tabButtons[index]?.GetComponentInChildren<Text>();
                if (label == null) continue;
                // 인벤토리·장비·도감에는 키 이름을 붙이지 않는다.
                var hotkey = UnifiedTabHotkey(index);
                label.text = hotkey == KeyCode.None
                    ? UnifiedTabLabel(index)
                    : $"{UnifiedTabHotkey(index)} · {UnifiedTabLabel(index)}";
            }
            BuildDetailsScrollArea();
            BuildCraftingList();
            BuildInventoryGrid();
            BuildEquipmentVisual();
            BuildStorageUi();
            BuildCodexGrid();
            ApplySketchWindowLayout();

            previousButton.onClick.AddListener(() => SelectRelative(-1));
            nextButton.onClick.AddListener(() => SelectRelative(1));
            primaryButton.onClick.AddListener(TryPrimaryAction);
            debugCompleteButton.onClick.AddListener(TryCompleteCurrentProcessForDebug);
            collectButton.onClick.AddListener(TrySecondaryAction);
            closeButton.onClick.AddListener(() => SetOpen(false));
            BuildCodexExpandedView();
            BuildSummonConfirmation();
        }

        private void BuildSummonConfirmation()
        {
            if (inventoryHintText == null || summonConfirmationRoot == null || summonConfirmationText == null)
            {
                Debug.LogError("[Nyangbingo] MainGameCraftingUiController: SummonConfirmation 하이어라키가 인스펙터에 배선되지 않았습니다.");
                return;
            }
            inventoryHintText.gameObject.SetActive(false);
            var card = summonConfirmationRoot.transform.Find("Card");
            card.Find("ConfirmSummon").GetComponent<Button>().onClick.AddListener(ConfirmSummonItemUse);
            card.Find("CancelSummon").GetComponent<Button>().onClick.AddListener(CancelSummonConfirmation);
            summonConfirmationRoot.SetActive(false);
        }

        private void BuildDetailsScrollArea()
        {
            if (detailsViewportRect == null || detailsText == null || detailsScrollRect == null ||
                detailsScrollbarRect == null)
            {
                Debug.LogError("[Nyangbingo] MainGameCraftingUiController: 상세설명 스크롤 하이어라키가 인스펙터에 배선되지 않았습니다.");
                return;
            }
            var scrollbar = detailsScrollbarRect.GetComponent<Scrollbar>();
            var handleRect = detailsScrollbarRect.Find("Handle") as RectTransform;
            scrollbar.handleRect = handleRect;
            scrollbar.targetGraphic = handleRect.GetComponent<Image>();
            var trackImage = detailsScrollbarRect.GetComponent<Image>();
            if (gameplayArtCatalog?.ScrollTrack != null && trackImage != null)
            {
                trackImage.sprite = gameplayArtCatalog.ScrollTrack;
                trackImage.type = Image.Type.Simple;
                trackImage.color = Color.white;
            }
            var handleImage = handleRect.GetComponent<Image>();
            if (gameplayArtCatalog?.ScrollHandle != null && handleImage != null)
            {
                handleImage.sprite = gameplayArtCatalog.ScrollHandle;
                handleImage.type = Image.Type.Simple;
                handleImage.color = Color.white;
            }
            detailsScrollRect.verticalScrollbar = scrollbar;
            detailsScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        }

        private void BuildCraftingList()
        {
            craftingListRoot = CreateUiObject("CraftingRecipeViewport", panel.transform,
                new Vector2(110f, 58f), new Vector2(46f, 14f));
            var viewport = (RectTransform)craftingListRoot.transform;
            var viewportImage = craftingListRoot.AddComponent<Image>();
            viewportImage.color = new Color(.025f, .035f, .045f, .72f);
            craftingListRoot.AddComponent<RectMask2D>();

            var contentObject = CreateUiObject("RecipeList", craftingListRoot.transform, Vector2.zero, Vector2.zero);
            var contentRect = (RectTransform)contentObject.transform;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;
            var layout = contentObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(3, 3, 3, 3);
            layout.spacing = 3f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = contentObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            craftingListButtons = new Button[visibleRecipes.Count];
            craftingListLabels = new Text[visibleRecipes.Count];
            craftingListOutputIcons = new Image[visibleRecipes.Count];
            craftingListOutputCounts = new Text[visibleRecipes.Count];
            craftingListIngredientIcons = new Image[visibleRecipes.Count][];
            craftingListIngredientCounts = new Text[visibleRecipes.Count][];
            var ingredientSlotCount = Mathf.Max(1, visibleRecipes.Count == 0
                ? 1
                : visibleRecipes.Max(recipe => recipe?.Ingredients?.Length ?? 0));
            var ingredientSpacing = Mathf.Min(28f, 104f / ingredientSlotCount);
            var ingredientIconSize = Mathf.Min(24f, ingredientSpacing - 2f);
            for (var index = 0; index < craftingListButtons.Length; index++)
            {
                var capturedIndex = index;
                var button = CreateButton(contentObject.transform, $"Recipe_{index + 1:00}", string.Empty,
                    Vector2.zero, new Vector2(168f, 34f), () => SelectCraftingRecipe(capturedIndex));
                var layoutElement = button.gameObject.AddComponent<LayoutElement>();
                layoutElement.preferredHeight = 34f;
                layoutElement.minHeight = 34f;
                var label = button.GetComponentInChildren<Text>();
                label.fontSize = 9;
                label.text = "▶";
                var labelRect = (RectTransform)label.transform;
                labelRect.sizeDelta = new Vector2(12f, 20f);
                labelRect.anchoredPosition = new Vector2(-43f, 0f);

                craftingListOutputIcons[index] = CreateArtImage(button.transform, "OutputIcon", null,
                    new Vector2(-67f, 0f), new Vector2(26f, 26f));
                craftingListOutputCounts[index] = CreateText(button.transform, "OutputCount", 7,
                    TextAnchor.LowerRight, new Vector2(28f, 13f), new Vector2(-61f, -8f));

                craftingListIngredientIcons[index] = new Image[ingredientSlotCount];
                craftingListIngredientCounts[index] = new Text[ingredientSlotCount];
                var ingredientStartX = -28f;
                for (var ingredientIndex = 0; ingredientIndex < ingredientSlotCount; ingredientIndex++)
                {
                    var x = ingredientStartX + ingredientSpacing * ingredientIndex;
                    craftingListIngredientIcons[index][ingredientIndex] = CreateArtImage(button.transform,
                        $"IngredientIcon_{ingredientIndex + 1:00}", null, new Vector2(x, 0f),
                        new Vector2(ingredientIconSize, ingredientIconSize));
                    craftingListIngredientCounts[index][ingredientIndex] = CreateText(button.transform,
                        $"IngredientCount_{ingredientIndex + 1:00}", 7, TextAnchor.LowerRight,
                        new Vector2(ingredientSpacing, 13f), new Vector2(x + 3f, -8f));
                }
                craftingListButtons[index] = button;
                craftingListLabels[index] = label;
            }

            craftingListScrollRect = craftingListRoot.AddComponent<ScrollRect>();
            craftingListScrollRect.content = contentRect;
            craftingListScrollRect.viewport = viewport;
            craftingListScrollRect.horizontal = false;
            craftingListScrollRect.vertical = true;
            craftingListScrollRect.movementType = ScrollRect.MovementType.Clamped;
            craftingListScrollRect.scrollSensitivity = 18f;
            craftingListRoot.SetActive(false);
        }

        private void SelectCraftingRecipe(int index)
        {
            if (!open || page != Page.Crafting || IsShowingSmeltingList ||
                index < 0 || index >= filteredRecipes.Count) return;
            selectedIndex = index;
            message = string.Empty;
            Refresh();
            ResetDetailsScroll();
        }

        private void BuildInventoryGrid()
        {
            if (inventoryGridRoot == null || inventoryGridButtons == null ||
                inventoryGridButtons.Length != Nyangbingo.Inventory.Inventory.SlotCount)
            {
                Debug.LogError("[Nyangbingo] MainGameCraftingUiController: InventoryGrid10x5 하이어라키가 인스펙터에 배선되지 않았습니다.");
                return;
            }
            for (var index = 0; index < inventoryGridButtons.Length; index++)
            {
                var capturedIndex = index;
                WireInventoryPointer(inventoryGridButtons[index], pointer =>
                {
                    inventorySelectedIndex = capturedIndex;
                    TransferPlayerSlot(capturedIndex, pointer);
                });
                WireEquipmentDragSource(inventoryGridButtons[index], capturedIndex);
                WireEquipmentDrop(inventoryGridButtons[index], pointer =>
                {
                    if (IsDraggingEquippedItem) DropEquippedItemToInventory(capturedIndex, pointer);
                    else DropStorageItem(runtimeServices.PlayerInventory, capturedIndex, pointer);
                });
                var oldNumberHint = inventoryGridButtons[index].transform.Find("HotbarShortcut");
                if (oldNumberHint != null) oldNumberHint.gameObject.SetActive(false);
            }
            inventoryGridRoot.SetActive(false);
        }

        private Sprite clawSlotSprite;

        private void BuildEquipmentVisual()
        {
            EnsureClawEquipmentSlot();
            if (equipmentVisualRoot == null || equipmentSlotButtons == null || equipmentSlotButtons.Length != 7)
            {
                Debug.LogError("[Nyangbingo] MainGameCraftingUiController: EquipmentVisual 하이어라키가 인스펙터에 배선되지 않았습니다.");
                return;
            }
            equipmentCharacter.sprite = gameplayArtCatalog?.EquipmentCharacter;
            equipmentCharacter.enabled = equipmentCharacter.sprite != null;
            for (var index = 0; index < equipmentSlotButtons.Length; index++)
            {
                var capturedIndex = index;
                equipmentSlotButtons[index].onClick.AddListener(() => SelectEquipmentVisualSlot(capturedIndex));
                WireInventoryPointer(equipmentSlotButtons[index], pointer =>
                {
                    if (TryShiftUnequipVisualSlot(capturedIndex, pointer)) return;
                    if (pointer.button == PointerEventData.InputButton.Right)
                        ShowEquipmentDescription(EquippedItemIdForVisualSlot(capturedIndex));
                });
                WireEquipmentDrop(equipmentSlotButtons[index], pointer => DropInventoryEquipment(capturedIndex, pointer));
                var dragSource = equipmentSlotButtons[index].GetComponent<InventoryEquipmentDragSource>() ??
                                 equipmentSlotButtons[index].gameObject.AddComponent<InventoryEquipmentDragSource>();
                dragSource.Configure(pointer => BeginEquippedItemDrag(capturedIndex,
                    equipmentSlotButtons[capturedIndex].gameObject, pointer), CancelEquipmentDrag);
                // ColorTint Transition의 SelectedColor가 클릭 후에도 남아 그림자처럼 보인다.
                // None으로 바꾸고 RefreshEquipmentVisual에서 직접 색을 지정한다.
                equipmentSlotButtons[index].transition = Selectable.Transition.None;
            }
            if (equipmentAmmoCount == null && equipmentSlotIcons[0] != null)
            {
                equipmentAmmoCount = CreateText(equipmentSlotIcons[0].transform, "AmmoCount", 8,
                    TextAnchor.LowerRight, new Vector2(26f, 12f), Vector2.zero);
                var inventoryCount = inventoryGridLabels.FirstOrDefault(label => label != null);
                if (inventoryCount?.font != null) equipmentAmmoCount.font = inventoryCount.font;
                equipmentAmmoCount.fontStyle = inventoryCount != null ? inventoryCount.fontStyle : FontStyle.Bold;
                equipmentAmmoCount.resizeTextForBestFit = false;
                equipmentAmmoCount.color = Color.white;
                var rect = equipmentAmmoCount.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
                rect.anchoredPosition = new Vector2(-1f, 1f);
                equipmentAmmoCount.raycastTarget = false;
                equipmentAmmoCount.supportRichText = false;
                equipmentAmmoCount.enabled = false;
            }
            equipmentVisualRoot.SetActive(false);
        }

        /// <summary>인형 머리 위, 좌우 칸 사이의 빈자리에 18px 발톱 칸을 둔다.</summary>
        private void EnsureClawEquipmentSlot()
        {
            if (equipmentSlotButtons == null || equipmentSlotButtons.Length != 6 || equipmentSlotButtons[4] == null)
                return;
            var clone = Instantiate(equipmentSlotButtons[4].gameObject, equipmentSlotButtons[4].transform.parent);
            clone.name = "EquipmentSlot_Claw";
            var rect = clone.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(0f, 52f);
            rect.sizeDelta = new Vector2(34f, 34f);
            rect.localScale = Vector3.one;
            clone.transform.SetAsLastSibling();
            var button = clone.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            var icon = clone.transform.Find("Icon")?.GetComponent<Image>();
            if (icon != null)
            {
                icon.sprite = null;
                icon.enabled = false;
            }
            var fallback = clone.transform.Find("FallbackLabel")?.GetComponent<Text>();
            if (fallback != null) fallback.text = string.Empty;
            Array.Resize(ref equipmentSlotButtons, 7);
            Array.Resize(ref equipmentSlotBackgrounds, 7);
            Array.Resize(ref equipmentSlotIcons, 7);
            Array.Resize(ref equipmentSlotFallbackLabels, 7);
            equipmentSlotButtons[ClawSlotSystem.VisualSlotIndex] = button;
            var background = clone.GetComponent<Image>();
            if (background != null) background.raycastTarget = true;
            equipmentSlotBackgrounds[ClawSlotSystem.VisualSlotIndex] = background;
            equipmentSlotIcons[ClawSlotSystem.VisualSlotIndex] = icon;
            equipmentSlotFallbackLabels[ClawSlotSystem.VisualSlotIndex] = fallback;
        }

        private Sprite ClawSlotArt()
        {
            if (clawSlotSprite != null) return clawSlotSprite;
            var sprites = Resources.LoadAll<Sprite>("UI/equipment_claw_slot");
            if (sprites != null && sprites.Length > 0) clawSlotSprite = sprites[0];
            return clawSlotSprite;
        }

        private void BuildStorageUi()
        {
            if (storageModeRoot == null || storageLabels == null || storagePlayerLabels == null)
            {
                Debug.LogError("[Nyangbingo] MainGameCraftingUiController: JangdokStorageMode 하이어라키가 인스펙터에 배선되지 않았습니다.");
                return;
            }
            storageLabelText.text = "장독 창고 · 40슬롯";

            WireTransferGrid(storageLabels, TransferStorageSlot);
            WireTransferGrid(storagePlayerLabels, TransferPlayerSlot);
            for (var index = 0; index < storagePlayerLabels.Length; index++)
                WireEquipmentDragSource(storagePlayerLabels[index].transform.parent.GetComponent<Button>(), index);
            WireStorageDragSlots();
            BuildStoragePopup();
            storageHintText.text = StorageDragHelp;
            storageModeRoot.SetActive(false);
        }

        private void WireTransferGrid(Text[] labels, Action<int, PointerEventData> clicked)
        {
            for (var index = 0; index < labels.Length; index++)
            {
                var captured = index;
                WireInventoryPointer(labels[index].transform.parent.GetComponent<Button>(),
                    pointer => clicked(captured, pointer));
            }
        }

        private void TransferPlayerSlot(int index, PointerEventData pointer)
        {
            if (TryShiftStorageTransfer(runtimeServices.PlayerInventory, index, pointer)) return;
            if (TryShiftEquipInventorySlot(index, pointer)) return;
            ClickInventorySlot(runtimeServices.PlayerInventory, index, pointer);
        }

        private bool IsQuickEquipmentClick(PointerEventData pointer) => !HasStorageMode &&
            pointer.button == PointerEventData.InputButton.Left &&
            (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));

        private bool CanQuickChangeEquipment => runtimeServices?.IsInitialized == true &&
            Time.timeScale > 0f && (shell == null || shell.Screen == GameShellScreen.Gameplay) &&
            !HasHeldInventoryItem && !IsDraggingItem;

        private bool TryShiftEquipInventorySlot(int index, PointerEventData pointer)
        {
            if (!IsQuickEquipmentClick(pointer)) return false;
            if (!CanQuickChangeEquipment) return true;
            var inventory = runtimeServices.PlayerInventory;
            if (index < 0 || index >= inventory.Capacity) return true;
            var source = inventory.Slots[index];
            if (source.amount <= 0) return false;
            var equipment = gameDataCatalog.FindEquipment(source.itemId);
            if (equipment == null && !ActiveSlotSystem.IsAllowedItemId(source.itemId) &&
                !ClawSlotSystem.IsClawItem(source.itemId)) return false;
            bool succeeded;
            if (ClawSlotSystem.IsClawItem(source.itemId))
            {
                var claw = runtimeServices.ClawSlot;
                succeeded = claw != null && (claw.EquippedItemId == source.itemId ||
                                              claw.TryEquip(source.itemId, index));
            }
            else if (ActiveSlotSystem.IsAllowedItemId(source.itemId))
            {
                var active = runtimeServices.ActiveSlot;
                succeeded = active.EquippedItemId == source.itemId
                    ? active.IsUsingEquippedItem || active.Toggle()
                    : active.TryEquip(source.itemId, index);
            }
            else
            {
                var system = runtimeServices.EquipmentSystem;
                var slot = equipment.Slot;
                if (equipment.IsAccessory)
                    slot = system.Get(EquipmentSlot.AccessoryOne) == null ? EquipmentSlot.AccessoryOne :
                        system.Get(EquipmentSlot.AccessoryTwo) == null ? EquipmentSlot.AccessoryTwo :
                        EquipmentSlot.AccessoryOne;
                // Spare copies must not move an already worn accessory between slots.
                succeeded = FindEquippedSlot(equipment).HasValue || system.TryEquipInSlot(equipment, slot, index);
            }
            ShowMessage(succeeded ? $"{gameDataCatalog.ItemDisplayName(source.itemId, "장비")} 장착 완료" :
                "장착할 수 없습니다. 인벤토리 공간을 확인하세요.");
                Refresh();
            return true;
        }

        private bool TryShiftUnequipVisualSlot(int visualSlot, PointerEventData pointer)
        {
            if (!IsQuickEquipmentClick(pointer)) return false;
            if (!CanQuickChangeEquipment) return true;
            var id = EquippedItemIdForVisualSlot(visualSlot);
            if (string.IsNullOrEmpty(id)) return true;
            var succeeded = visualSlot == 0 ? runtimeServices.ActiveSlot.TryUnequip() :
                visualSlot == ClawSlotSystem.VisualSlotIndex ? runtimeServices.ClawSlot != null &&
                    runtimeServices.ClawSlot.TryUnequip() :
                EquipmentInventoryDrag.TryGetEquipmentSlot(visualSlot, out var slot) &&
                runtimeServices.EquipmentSystem.TryUnequip(slot);
            ShowMessage(succeeded ? $"{gameDataCatalog.ItemDisplayName(id, "장비")} 장착 해제" :
                "인벤토리에 빈 공간이 없어 장비를 해제할 수 없습니다.");
            Refresh();
            return true;
        }

        private void TransferStorageSlot(int index, PointerEventData pointer)
        {
            Nyangbingo.Inventory.Inventory storage;
            if (chestProgress != null)
            {
                if (!chestProgress.TryGetContents(chestId, out storage)) return;
            }
            else if (!runtimeServices.JangdokStorage.TryGet(storageObjectId, out storage)) return;
            if (TryShiftStorageTransfer(storage, index, pointer)) return;
            ClickInventorySlot(storage, index, pointer);
        }

        private bool TryShiftStorageTransfer(Nyangbingo.Inventory.Inventory source, int index,
            PointerEventData pointer)
        {
            if (!HasStorageMode || !(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
                return false;
            if (!open || Time.timeScale <= 0f || shell != null && shell.Screen != GameShellScreen.Gameplay)
                return true;
            Nyangbingo.Inventory.Inventory storage;
            if (chestProgress != null)
            {
                if (!chestProgress.TryGetContents(chestId, out storage)) return true;
            }
            else if (!runtimeServices.JangdokStorage.TryGet(storageObjectId, out storage)) return true;
            if (index < 0 || index >= source.Capacity) return true;
            var count = source.Slots[index].amount;
            if (count <= 0) return true;
            if (pointer.button == PointerEventData.InputButton.Right) count = count / 2 + count % 2;
            var target = source == runtimeServices.PlayerInventory ? storage : runtimeServices.PlayerInventory;
            message = source.TryTransferSlotTo(index, target, count)
                ? StorageItemMovedMessage : "이동할 보관 공간이 부족합니다.";
                Refresh();
            return true;
        }

        private void BuildCodexGrid()
        {
            if (codexGridRoot == null)
            {
                Debug.LogError("[Nyangbingo] MainGameCraftingUiController: IntegratedCodexViewport 하이어라키가 인스펙터에 배선되지 않았습니다.");
                return;
            }
            EnsureCodexCardBindings();
            if (codexCardButtons == null ||
                codexCardButtons.Length != YokaiCodexPresentationModel.ExpectedCardCount)
            {
                Debug.LogError("[Nyangbingo] MainGameCraftingUiController: 도감 카드 슬롯을 17장으로 구성하지 못했습니다.");
                return;
            }
            for (var index = 0; index < codexCardButtons.Length; index++)
            {
                var capturedIndex = index;
                RuntimeUiButtonArt.ApplyCodexCard(codexCardButtons[index], gameplayArtCatalog);
                codexCardButtons[index].onClick.RemoveAllListeners();
                codexCardButtons[index].onClick.AddListener(() => SelectCodexCard(capturedIndex));
            }
            codexGridRoot.SetActive(false);
        }

        private void EnsureCodexCardBindings()
        {
            var needed = YokaiCodexPresentationModel.ExpectedCardCount;
            if (codexCardButtons != null && codexCardButtons.Length == needed &&
                codexCardLabels != null && codexCardLabels.Length == needed &&
                codexCardPortraits != null && codexCardPortraits.Length == needed)
            {
                ConfigureCodexScrolling();
                return;
            }

            for (var index = codexGridRoot.transform.childCount - 1; index >= 0; index--)
                Destroy(codexGridRoot.transform.GetChild(index).gameObject);

            var grid = codexGridRoot.GetComponent<GridLayoutGroup>();
            if (grid != null)
            {
            grid.cellSize = YokaiCodexPresentationModel.GridCardSize;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = YokaiCodexPresentationModel.GridColumns;
            }

            codexCardButtons = new Button[needed];
            codexCardLabels = new Text[needed];
            codexCardPortraits = new Image[needed];
            for (var index = 0; index < needed; index++)
            {
                var cardObject = new GameObject($"CodexCard_{index + 1:00}", typeof(RectTransform));
                cardObject.transform.SetParent(codexGridRoot.transform, false);
                var cardImage = cardObject.AddComponent<Image>();
                cardImage.color = new Color(.17f, .21f, .25f, 1f);
                codexCardButtons[index] = cardObject.AddComponent<Button>();
                var portraitObject = new GameObject("Portrait", typeof(RectTransform));
                portraitObject.transform.SetParent(cardObject.transform, false);
                var portrait = portraitObject.AddComponent<Image>();
                portrait.raycastTarget = false;
                var portraitRect = portrait.rectTransform;
                portraitRect.anchorMin = Vector2.zero;
                portraitRect.anchorMax = Vector2.one;
                portraitRect.offsetMin = new Vector2(4f, 18f);
                portraitRect.offsetMax = new Vector2(-4f, -4f);
                codexCardPortraits[index] = portrait;
                var labelObject = new GameObject("Label", typeof(RectTransform));
                labelObject.transform.SetParent(cardObject.transform, false);
                var label = labelObject.AddComponent<Text>();
                label.alignment = TextAnchor.LowerCenter;
                label.fontSize = 11;
                label.raycastTarget = false;
                var builtinFont = UiTextFont.Source;
                if (builtinFont != null)
                    label.font = builtinFont;
                label.rectTransform.anchorMin = new Vector2(0f, 0f);
                label.rectTransform.anchorMax = new Vector2(1f, 0f);
                label.rectTransform.pivot = new Vector2(0.5f, 0f);
                label.rectTransform.sizeDelta = new Vector2(0f, 18f);
                label.rectTransform.anchoredPosition = Vector2.zero;
                codexCardLabels[index] = label;
            }
            ConfigureCodexScrolling();
        }

        private void ConfigureCodexScrolling()
        {
            // The scene may contain a hand-positioned nine-card grid with no layout component.
            // Keep the authored viewport, but lay out every current card in scrollable content.
            var viewport = codexGridRoot.GetComponent<RectTransform>();
            var oldLayout = codexGridRoot.GetComponent<GridLayoutGroup>();
            if (oldLayout != null) oldLayout.enabled = false;
            var content = codexGridRoot.transform.Find("CodexScrollContent") as RectTransform;
            if (content == null)
            {
                content = new GameObject("CodexScrollContent", typeof(RectTransform)).GetComponent<RectTransform>();
                content.SetParent(viewport, false);
            }
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            var rows = Mathf.CeilToInt(codexCardButtons.Length / (float)YokaiCodexPresentationModel.GridColumns);
            content.sizeDelta = new Vector2(0f, 8f + rows * 96f + Mathf.Max(0, rows - 1) * 6f);
            var layout = content.GetComponent<GridLayoutGroup>() ?? content.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = YokaiCodexPresentationModel.GridCardSize;
            layout.spacing = new Vector2(6f, 6f);
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = YokaiCodexPresentationModel.GridColumns;
            for (var i = 0; i < codexCardButtons.Length; i++)
            {
                codexCardButtons[i].transform.SetParent(content, false);
                codexCardLabels[i].rectTransform.sizeDelta = new Vector2(0f, 28f);
                codexCardLabels[i].fontSize = 10;
                codexCardPortraits[i].rectTransform.offsetMin = new Vector2(4f, 30f);
            }
            if (codexGridRoot.GetComponent<RectMask2D>() == null) codexGridRoot.AddComponent<RectMask2D>();
            var hitArea = codexGridRoot.GetComponent<Image>() ?? codexGridRoot.AddComponent<Image>();
            hitArea.color = new Color(0f, 0f, 0f, .04f);
            hitArea.raycastTarget = true;
            var scroll = codexGridRoot.GetComponent<ScrollRect>() ?? codexGridRoot.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;
            scroll.verticalNormalizedPosition = 1f;
        }

        private void BuildCodexExpandedView()
        {
            if (codexExpandedBackdrop == null || codexExpandedCardBackground == null)
            {
                Debug.LogError("[Nyangbingo] MainGameCraftingUiController: CodexExpandedBackdrop 하이어라키가 인스펙터에 배선되지 않았습니다.");
                return;
            }
            codexExpandedBackdrop.GetComponent<Button>().onClick.AddListener(HandleCodexExpandedBackdropClicked);
            RuntimeUiButtonArt.ApplyCodexCard(codexExpandedCardBackground, gameplayArtCatalog);
            codexExpandedCardBackground.GetComponent<Button>().onClick.AddListener(HandleCodexExpandedCardClicked);
            codexExpandedBackdrop.SetActive(false);
        }

        private void SelectCodexCard(int index)
        {
            if (!open || page != Page.Codex || codexModel == null ||
                index < 0 || index >= codexModel.Cards.Count) return;
            selectedIndex = index;
            codexModel.TryTapCard(codexModel.Cards[index].EntryId);
            Refresh();
        }

        private void HandleCodexExpandedCardClicked()
        {
            if (!open || page != Page.Codex || codexModel == null || codexModel.SelectedCard == null) return;
            codexModel.TryFlipSelected();
            Refresh();
        }

        private void HandleCodexExpandedBackdropClicked()
        {
            if (!open || page != Page.Codex || codexModel == null) return;
            codexModel.TapOutside();
            Refresh();
        }

        private bool TryHandlePageHotkey()
        {
            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt) ||
                Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ||
                Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) return false;
            if (!Input.GetKeyDown(KeyCode.Tab)) return false;
            if ((shell != null && shell.Screen != GameShellScreen.Gameplay) || Time.timeScale <= 0f) return true;
            if (IsDraggingItem || HasHeldInventoryItem) return true;
            sideHudVisible = !sideHudVisible;
            if (sideHudVisible) RefreshCraftWindow();
            ApplySideHudVisibility();
            return true;
        }

        /// <summary>
        /// 왼쪽 위 인벤, 왼쪽 아래 제작, 오른쪽 위 장비, 인벤 아래 요괴 눈물을 함께 켜고 끈다.
        /// 장독·상자를 연 동안에는 인벤과 장비를 그대로 보여 준다.
        /// </summary>
        private void ApplySideHudVisibility()
        {
            var showCluster = sideHudVisible || HasStorageMode;
            if (inventoryScrollRoot != null) inventoryScrollRoot.SetActive(showCluster);
            if (inventoryGridRoot != null) inventoryGridRoot.SetActive(showCluster);
            if (equipmentVisualRoot != null) equipmentVisualRoot.SetActive(showCluster);
            if (!sideHudVisible && craftWindowRoot != null) craftWindowRoot.SetActive(false);
            if (tearBalanceRoot == null)
                tearBalanceRoot = FindAnyObjectByType<MainGameHudController>()?.TearBalanceRect;
            if (tearBalanceRoot != null) tearBalanceRoot.gameObject.SetActive(sideHudVisible);
        }

        private void TogglePage(Page target)
        {
            if (open && page == target)
            {
                SetOpen(false);
                return;
            }

            OpenPage(target);
        }

        private void OpenPage(Page target)
        {
            HideDescription();
            CancelEquipmentDrag();
            if (HasStorageMode && HasHeldInventoryItem && !ReturnHeldInventoryItem())
            {
                ShowMessage("원래 보관함에 반환할 공간이 없습니다. 빈 슬롯에 놓아주세요.");
                return;
            }
            HideInventoryCursorPreview();
            if (turretRuntime != null && turretRuntime.IsPlacementPreviewActive)
                turretRuntime.CancelPlacementPreview();
            if (target != Page.Codex && codexModel != null) codexModel.TapOutside();
            if (target != Page.Codex && codexExpandedBackdrop != null) codexExpandedBackdrop.SetActive(false);
            page = target;
            storageObjectId = string.Empty;
            chestProgress = null;
            chestId = string.Empty;
            if (!open)
            {
                openedStation = CraftingStation.None;
                openedStationObjectId = null;
            }
            craftingFilter = FilterForStation(openedStation);
            furnaceSmeltingView = false;
            selectedIndex = 0;
            if (page == Page.Crafting) RebuildFilteredRecipes();
            message = string.Empty;
            SetOpen(true);
            Refresh();
            ResetDetailsScroll();
            ResetCraftingListScroll();
        }

        private void ResetDetailsScroll()
        {
            if (detailsScrollRect == null) return;
            Canvas.ForceUpdateCanvases();
            detailsScrollRect.StopMovement();
            detailsScrollRect.verticalNormalizedPosition = 1f;
        }

        private void ResetCraftingListScroll()
        {
            if (craftingListScrollRect == null && craftWindowScrollRect == null) return;
            Canvas.ForceUpdateCanvases();
            if (craftingListScrollRect != null)
            {
            craftingListScrollRect.StopMovement();
            craftingListScrollRect.verticalNormalizedPosition = 1f;
            }
            if (craftWindowScrollRect != null)
            {
                craftWindowScrollRect.StopMovement();
                craftWindowScrollRect.verticalNormalizedPosition = 1f;
            }
        }

        private void EnsureSelectedRecipeVisible()
        {
            if (page != Page.Crafting || IsShowingSmeltingList || craftingListScrollRect == null ||
                selectedIndex < 0 || selectedIndex >= craftingListButtons.Length) return;
            var row = craftingListButtons[selectedIndex];
            if (row == null || !row.gameObject.activeInHierarchy) return;
            Canvas.ForceUpdateCanvases();
            var viewport = craftingListScrollRect.viewport;
            var content = craftingListScrollRect.content;
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, row.transform);
            var offset = bounds.max.y > viewport.rect.yMax ? viewport.rect.yMax - bounds.max.y
                : bounds.min.y < viewport.rect.yMin ? viewport.rect.yMin - bounds.min.y : 0f;
            craftingListScrollRect.StopMovement();
            content.anchoredPosition += new Vector2(0f, offset);
        }

        private void SelectRelative(int delta)
        {
            var count = CurrentEntryCount();
            if (count <= 0) return;
            if (page == Page.Gathering)
                inventorySelectedIndex = (inventorySelectedIndex + delta + count) % count;
            else
            selectedIndex = (selectedIndex + delta + count) % count;
            message = string.Empty;
            Refresh();
            ResetDetailsScroll();
            EnsureSelectedRecipeVisible();
        }

        private void WireEquipmentDragSource(Button button, int index)
        {
            var source = button.GetComponent<InventoryEquipmentDragSource>() ??
                         button.gameObject.AddComponent<InventoryEquipmentDragSource>();
            source.Configure(pointer => BeginEquipmentDrag(index, button.gameObject, pointer),
                CancelEquipmentDrag, TryDropEquipmentUnderPointer);
        }

        public static void WireEquipmentDrop(Button button, Action<PointerEventData> dropped)
        {
            var trigger = button.GetComponent<EventTrigger>() ?? button.gameObject.AddComponent<EventTrigger>();
            trigger.triggers ??= new List<EventTrigger.Entry>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.Drop };
            entry.callback.AddListener(data =>
            {
                if (data is PointerEventData pointer && pointer.button == PointerEventData.InputButton.Left)
                    dropped(pointer);
            });
            trigger.triggers.Add(entry);
        }

        private void BeginEquipmentDrag(int index, GameObject source, PointerEventData pointer)
        {
            if (HasStorageMode)
            {
                BeginStorageItemDrag(runtimeServices?.PlayerInventory, index, source, pointer);
                    return;
                }
            if (runtimeServices?.IsInitialized != true || HasHeldInventoryItem ||
                source == null || !source.activeInHierarchy || equipmentVisualRoot == null ||
                !equipmentVisualRoot.activeInHierarchy ||
                (shell != null && shell.Screen != GameShellScreen.Gameplay)) return;
            equipmentDrag ??= new EquipmentInventoryDrag(runtimeServices.PlayerInventory, gameDataCatalog.FindEquipment);
            if (!equipmentDrag.Begin(index)) return;
            equipmentDragSource = source;
            equipmentDragPointerId = pointer.pointerId;
            equipmentDropFeedback = null;
            inventorySelectedIndex = index;
            // 체력 틀이 위에 있으면 발톱 칸이 보여도 드롭이 틀에 먹힌다.
            equipmentVisualRoot.transform.SetAsLastSibling();
            if (turretRuntime != null && turretRuntime.IsPlacementPreviewActive) turretRuntime.CancelPlacementPreview();
            RefreshEquipmentVisual();
        }

        private void TryDropEquipmentUnderPointer(PointerEventData pointer)
        {
            if (!IsDraggingEquipment || pointer == null || equipmentSlotButtons == null ||
                pointer.pointerDrag != equipmentDragSource || pointer.pointerId != equipmentDragPointerId)
                return;
            var canvas = equipmentVisualRoot != null
                ? equipmentVisualRoot.GetComponentInParent<Canvas>()?.rootCanvas : null;
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera : null;
            for (var index = equipmentSlotButtons.Length - 1; index >= 0; index--)
            {
                var button = equipmentSlotButtons[index];
                if (button == null || !button.gameObject.activeInHierarchy || !equipmentDrag.CanDrop(index))
                    continue;
                if (!RectTransformUtility.RectangleContainsScreenPoint(
                        (RectTransform)button.transform, pointer.position, camera))
                    continue;
                DropInventoryEquipment(index, pointer);
                return;
            }
        }

        private void DropInventoryEquipment(int visualSlot, PointerEventData pointer)
        {
            if (!IsDraggingEquipment || pointer.pointerDrag != equipmentDragSource ||
                pointer.pointerId != equipmentDragPointerId) return;
            if (equipmentDragSource == null || !equipmentDragSource.activeInHierarchy ||
                equipmentVisualRoot == null || !equipmentVisualRoot.activeInHierarchy ||
                (shell != null && shell.Screen != GameShellScreen.Gameplay))
            {
                CancelEquipmentDrag();
                return;
            }
            var name = gameDataCatalog.ItemDisplayName(equipmentDrag.ItemId, "장비");
            var succeeded = equipmentDrag.TryDrop(visualSlot, runtimeServices.EquipmentSystem,
                runtimeServices.ActiveSlot, runtimeServices.ClawSlot);
            CancelEquipmentDrag();
            equipmentDropFeedback = succeeded ? "장착 완료" : "슬롯이 맞지 않음";
            equipmentDropFeedbackUntil = Time.unscaledTime + 3f;
            ShowMessage(succeeded ? $"{name} 장착 완료" : "이 칸에 장착할 수 없습니다. 맞는 장비 칸에 놓아주세요.");
        }

        private void BeginEquippedItemDrag(int visualSlot, GameObject source, PointerEventData pointer)
        {
            if (runtimeServices?.IsInitialized != true || HasHeldInventoryItem || HasStorageMode ||
                source == null || !source.activeInHierarchy || pointer.button != PointerEventData.InputButton.Left ||
                shell != null && shell.Screen != GameShellScreen.Gameplay) return;
            var id = EquippedItemIdForVisualSlot(visualSlot);
            if (string.IsNullOrEmpty(id)) return;
            CancelEquipmentDrag();
            equippedDragVisualSlot = visualSlot;
            equippedDragItemId = id;
            equipmentDragSource = source;
            equipmentDragPointerId = pointer.pointerId;
            if (turretRuntime != null && turretRuntime.IsPlacementPreviewActive) turretRuntime.CancelPlacementPreview();
        }

        private void DropEquippedItemToInventory(int targetIndex, PointerEventData pointer)
        {
            if (!IsDraggingEquippedItem || pointer.pointerDrag != equipmentDragSource ||
                pointer.pointerId != equipmentDragPointerId) return;
            var visualSlot = equippedDragVisualSlot;
            var id = equippedDragItemId;
            var inventory = runtimeServices.PlayerInventory;
            var valid = shell == null || shell.Screen == GameShellScreen.Gameplay;
            valid = valid && !HasStorageMode && equipmentDragSource != null &&
                    equipmentDragSource.activeInHierarchy && EquippedItemIdForVisualSlot(visualSlot) == id &&
                    targetIndex >= 0 && targetIndex < inventory.Capacity;
            valid = valid && inventory.Slots[targetIndex].amount <= 0;
            var succeeded = valid && (visualSlot == 0
                ? runtimeServices.ActiveSlot.TryUnequip(targetIndex)
                : visualSlot == ClawSlotSystem.VisualSlotIndex
                    ? runtimeServices.ClawSlot != null && runtimeServices.ClawSlot.TryUnequip(targetIndex)
                : EquipmentInventoryDrag.TryGetEquipmentSlot(visualSlot, out var slot) &&
                  runtimeServices.EquipmentSystem.TryUnequip(slot, targetIndex));
            CancelEquipmentDrag();
            if (succeeded) inventorySelectedIndex = targetIndex;
            ShowMessage(succeeded ? $"{gameDataCatalog.ItemDisplayName(id, "장비")} 장착 해제" :
                "빈 인벤토리 칸에 놓아주세요.");
            Refresh();
        }

        private void CancelEquipmentDrag()
        {
            equippedDragVisualSlot = -1;
            equippedDragItemId = null;
            equipmentDrag?.Cancel();
            storageDrag?.Cancel();
            equipmentDragSource = null;
            HideInventoryCursorPreview();
            RefreshEquipmentVisual();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) CancelEquipmentDrag();
        }

        public static void WireInventoryPointer(Button button, Action<PointerEventData> clicked)
        {
            var trigger = button.GetComponent<EventTrigger>() ?? button.gameObject.AddComponent<EventTrigger>();
            trigger.triggers ??= new List<EventTrigger.Entry>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            entry.callback.AddListener(data =>
            {
                if (data is PointerEventData pointer &&
                    (pointer.button == PointerEventData.InputButton.Left ||
                     pointer.button == PointerEventData.InputButton.Right)) clicked(pointer);
            });
            trigger.triggers.Add(entry);
            // EventTrigger handles IScrollHandler itself, so Unity stops bubbling the
            // wheel event here even when only a click callback was registered.
            if (!trigger.triggers.Exists(existing => existing.eventID == EventTriggerType.Scroll))
            {
                var scrollEntry = new EventTrigger.Entry { eventID = EventTriggerType.Scroll };
                scrollEntry.callback.AddListener(data =>
                {
                    if (data is not PointerEventData pointer) return;
                    var scroll = button.GetComponentInParent<ScrollRect>();
                    if (scroll != null && scroll.isActiveAndEnabled) scroll.OnScroll(pointer);
                });
                trigger.triggers.Add(scrollEntry);
            }
        }

        public static bool TryClickQuickslot(int index, PointerEventData pointer)
        {
            var owner = cursorOwner;
            if (owner == null || owner.runtimeServices?.InventoryCursor == null) return false;
            if (owner.TryShiftStorageTransfer(owner.runtimeServices.PlayerInventory, index, pointer)) return true;
            owner.ClickInventorySlot(owner.runtimeServices.PlayerInventory, index, pointer);
            return true;
        }

        private void ClickInventorySlot(Nyangbingo.Inventory.Inventory inventory, int index,
            PointerEventData pointer)
        {
            if (runtimeServices?.InventoryCursor == null ||
                (shell != null && shell.Screen != GameShellScreen.Gameplay)) return;
            if (turretRuntime != null && turretRuntime.IsPlacementPreviewActive)
                turretRuntime.CancelPlacementPreview();
                tilePalette?.SelectBareHands();
            message = string.Empty;
            if (index < 0 || index >= inventory.Capacity) return;
            if (inventory == runtimeServices.PlayerInventory &&
                pointer.button == PointerEventData.InputButton.Right &&
                (ShowEquipmentDescription(inventory.Slots[index].itemId) ||
                 !HasHeldInventoryItem && ShowGeneralItemDescription(inventory.Slots[index].itemId))) return;
            if (inventory == runtimeServices.PlayerInventory) inventorySelectedIndex = index;
            var heldBefore = runtimeServices.InventoryCursor.Slots[0];
            var clickedBefore = inventory.Slots[index];
            if (!inventory.TryClickSlot(index, runtimeServices.InventoryCursor,
                    pointer.button == PointerEventData.InputButton.Right)) return;
            if (runtimeServices.InventoryCursor.IsEmpty) runtimeServices.InventoryCursorOrigin = null;
            else if (heldBefore.amount <= 0 || heldBefore.itemId != clickedBefore.itemId &&
                     clickedBefore.amount > 0 && pointer.button == PointerEventData.InputButton.Left)
            {
                runtimeServices.InventoryCursorOrigin = new InventoryCursorOrigin
                {
                    kind = inventory == runtimeServices.PlayerInventory ? "player" :
                        chestProgress != null ? "chest" : "jangdok",
                    objectId = inventory == runtimeServices.PlayerInventory ? string.Empty :
                        chestProgress != null ? chestId : storageObjectId,
                    slotIndex = index,
                    remainder = inventory.Slots[index]
                };
            }
            if (HasStorageMode) message = StorageItemMovedMessage;
            Refresh();
        }

        private void LateUpdate()
        {
            if (!initialized || runtimeServices?.InventoryCursor == null) return;
            if (open && HasStorageMode) PositionStoragePopup();
            cursorOwner = this;
            var visible = (HasHeldInventoryItem || IsDraggingItem) &&
                          (shell == null || shell.Screen == GameShellScreen.Gameplay);
            if (!visible)
            {
                if (inventoryCursorIcon != null) inventoryCursorIcon.gameObject.SetActive(false);
                return;
            }
            var canvas = panel.GetComponentInParent<Canvas>().rootCanvas;
            if (inventoryCursorIcon == null)
            {
                inventoryCursorIcon = CreateArtImage(canvas.transform, "InventoryCursor", null,
                    Vector2.zero, new Vector2(InventoryCursorPixelSize, InventoryCursorPixelSize));
                inventoryCursorIcon.raycastTarget = false;
                inventoryCursorCount = CreateText(inventoryCursorIcon.transform, "Count", 6,
                    TextAnchor.LowerRight, new Vector2(InventoryCursorPixelSize, InventoryCursorPixelSize), Vector2.zero);
                inventoryCursorCount.raycastTarget = false;
            }
            inventoryCursorIcon.gameObject.SetActive(true);
            inventoryCursorIcon.transform.SetAsLastSibling();
            var slot = IsDraggingEquippedItem ? new InventorySlot { itemId = equippedDragItemId, amount = 1 }
                : storageDrag?.IsActive == true ? storageDrag.Preview : IsDraggingEquipment
                ? new InventorySlot { itemId = equipmentDrag.ItemId, amount = 1 }
                : runtimeServices.InventoryCursor.Slots[0];
            var item = gameDataCatalog.FindItem(slot.itemId);
            inventoryCursorIcon.sprite = itemArtCatalog?.FindSprite(slot.itemId);
            inventoryCursorIcon.enabled = inventoryCursorIcon.sprite != null;
            inventoryCursorIcon.color = inventoryCursorIcon.sprite != null ? Color.white : Color.clear;
            MatchCursorCountToInventory();
            inventoryCursorCount.text = inventoryCursorIcon.sprite != null
                ? slot.amount.ToString()
                : $"{item?.DisplayName} ×{slot.amount}";
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas.transform as RectTransform,
                    Input.mousePosition, camera, out var point))
                inventoryCursorIcon.rectTransform.localPosition = new Vector3(point.x, point.y, 0f);
        }

        private void MatchCursorCountToInventory() =>
            MatchCountToInventory(inventoryCursorIcon, inventoryCursorCount);

        /// <summary>
        /// 재료 개수는 볼드 글꼴 대신 숫자 그림을 쓴다. 글꼴 8과 9가 비슷하게 뭉개지지 않게 하고,
        /// 8×8 칸의 오른쪽 아래에 붙인다.
        /// </summary>
        private void ShowCraftIngredientCount(Image icon, Text legacyCount, int required)
        {
            if (legacyCount != null)
            {
                legacyCount.text = string.Empty;
                legacyCount.enabled = false;
            }
            if (icon == null) return;
            var presenter = CraftCountGlyphs(icon);
            var visible = icon.enabled && required > 0 && presenter != null;
            if (!visible)
            {
                if (presenter != null) presenter.SetVisible(false);
                return;
            }
            const float height = 5f;
            presenter.SetScale(height / 15f);
            presenter.SetText(required.ToString());
            presenter.SetColor(Color.white);
            presenter.SetVisible(true);
            var iconRect = icon.rectTransform;
            var width = Mathf.Max(height, presenter.RenderedWidth);
            var rect = presenter.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.localScale = Vector3.one;
            rect.anchoredPosition = new Vector2(
                iconRect.rect.width - width * .5f,
                -(iconRect.rect.height - height * .5f));
        }

        private RuntimePixelGlyphPresenter CraftCountGlyphs(Image icon)
        {
            var glyphs = gameplayArtCatalog?.ShellNumberGlyphs;
            if (glyphs == null || glyphs.Count != RuntimePixelGlyphPresenter.ExpectedGlyphCount) return null;
            var existing = icon.transform.Find("RequiredGlyphs");
            if (existing != null) return existing.GetComponent<RuntimePixelGlyphPresenter>();
            var root = new GameObject("RequiredGlyphs", typeof(RectTransform));
            root.transform.SetParent(icon.transform, false);
            var presenter = root.AddComponent<RuntimePixelGlyphPresenter>();
            presenter.ConfigureForRuntime(glyphs, 5f / 15f);
            return presenter;
        }

        private void MatchCountToInventory(Image targetIcon, Text targetCount)
        {
            if (targetIcon == null || targetCount == null) return;
            if (inventoryGridIcons == null || inventoryGridLabels == null ||
                inventoryGridIcons.Length == 0 || inventoryGridLabels.Length == 0) return;
            var sourceIcon = inventoryGridIcons[0];
            var sourceCount = inventoryGridLabels[0];
            if (sourceIcon == null || sourceCount == null) return;
            var iconRect = sourceIcon.rectTransform;
            var countRect = sourceCount.rectTransform;
            var cursorRect = targetIcon.rectTransform;
            var sourceHeight = iconRect.rect.height * Mathf.Abs(iconRect.lossyScale.y);
            var cursorHeight = cursorRect.rect.height * Mathf.Abs(cursorRect.lossyScale.y);
            if (sourceHeight <= Mathf.Epsilon || cursorHeight <= Mathf.Epsilon) return;
            var ratio = cursorHeight / sourceHeight;
            var textScale = ratio * Mathf.Abs(countRect.lossyScale.y) /
                Mathf.Max(.0001f, Mathf.Abs(cursorRect.lossyScale.y));
            targetCount.font = sourceCount.font;
            targetCount.fontStyle = sourceCount.fontStyle;
            targetCount.fontSize = sourceCount.fontSize;
            targetCount.resizeTextForBestFit = false;
            targetCount.alignment = TextAnchor.LowerRight;
            targetCount.color = sourceCount.color;
            targetCount.horizontalOverflow = HorizontalWrapMode.Overflow;
            targetCount.verticalOverflow = VerticalWrapMode.Overflow;
            // Copy the count's placement relative to its icon, scaled to the cursor icon.
            var labelRect = targetCount.rectTransform;
            labelRect.anchorMin = labelRect.anchorMax = Vector2.one * .5f;
            labelRect.pivot = countRect.pivot;
            labelRect.localScale = Vector3.one * textScale;
            labelRect.sizeDelta = countRect.rect.size;
            var localOffset = iconRect.InverseTransformPoint(countRect.position);
            var iconRatio = cursorRect.rect.height / Mathf.Max(.0001f, iconRect.rect.height);
            labelRect.anchoredPosition = new Vector2(localOffset.x, localOffset.y) * iconRatio;
        }

        private bool ReturnHeldInventoryItem()
        {
            var cursor = runtimeServices?.InventoryCursor;
            if (cursor == null || cursor.IsEmpty)
            {
                if (runtimeServices != null) runtimeServices.InventoryCursorOrigin = null;
                return true;
            }
            var origin = runtimeServices.InventoryCursorOrigin;
            var inventory = runtimeServices.PlayerInventory;
            if (origin?.kind == "jangdok")
            {
                // A destroyed container cannot receive items. Keep the held item safe in the player bag.
                if (!runtimeServices.JangdokStorage.TryGet(origin.objectId, out inventory))
                    inventory = runtimeServices.PlayerInventory;
            }
            else if (origin?.kind == "chest")
            {
                var progress = chestProgress ?? FindAnyObjectByType<MainGameBootstrap>()?.Session?.ChestProgress;
                if (progress == null || !progress.TryGetContents(origin.objectId, out inventory))
                    inventory = runtimeServices.PlayerInventory;
            }
            if (origin != null)
                inventory.TryReturnCursor(origin.slotIndex, cursor, origin.remainder);
            for (var index = 0; index < inventory.Capacity && !cursor.IsEmpty; index++)
            {
                var target = inventory.Slots[index];
                if (target.amount <= 0 || target.itemId == cursor.Slots[0].itemId)
                    inventory.TryClickSlot(index, cursor, false);
            }
            if (!cursor.IsEmpty) return false;
            runtimeServices.InventoryCursorOrigin = null;
            return true;
        }

        private void HideInventoryCursorPreview()
        {
            // Cursor contents are owned by the runtime and saved independently of this UI.
            if (inventoryCursorIcon != null) inventoryCursorIcon.gameObject.SetActive(false);
        }

        private int FindFirstEntryForFilter()
        {
            if (IsShowingSmeltingList) return 0;
            for (var index = 0; index < filteredRecipes.Count; index++)
                if (RecipeMatchesFilter(filteredRecipes[index].Station, craftingFilter)) return index;
            return 0;
        }

        private void RebuildFilteredRecipes()
        {
            filteredRecipes.Clear();
            smeltingRecipes.Clear();
            smeltingRecipes.AddRange(gameDataCatalog.Smelting.Where(definition => definition != null &&
                (openedStation == CraftingStation.Foundry && definition.StationKind == SmeltingStationKind.Foundry ||
                 openedStation == CraftingStation.Furnace && definition.StationKind == SmeltingStationKind.Furnace)));
            if (IsShowingSmeltingList)
            {
                selectedIndex = smeltingRecipes.Count > 0
                    ? Mathf.Clamp(selectedIndex, 0, smeltingRecipes.Count - 1)
                    : 0;
                return;
            }
            var smithyUnlocked = runtimeServices?.Seokbinggo?.IsSmithyUnlocked == true;
            for (var index = 0; index < visibleRecipes.Count; index++)
            {
                var recipe = visibleRecipes[index];
                if (recipe == null) continue;
                if (!IsRecipeVisibleAtStation(recipe.Station, openedStation)) continue;
                if (!IsSmithyRecipeAllowed(recipe.Station, smithyUnlocked)) continue;
                if (!RecipeMatchesCraftCategory(recipe)) continue;
                if (RecipeUnlockPolicy.IsUnlocked(recipe, runtimeServices.RecipeBook))
                    filteredRecipes.Add(recipe);
            }
            selectedIndex = filteredRecipes.Count > 0
                ? Mathf.Clamp(selectedIndex, 0, filteredRecipes.Count - 1)
                : 0;
        }

        private void TryPrimaryAction()
        {
            if (!open) return;
            switch (page)
            {
                case Page.Gathering: TryUseOrPlaceSelectedInventoryItem(); break;
                case Page.Crafting:
                    if (IsShowingSmeltingList) TrySmeltSelected();
                    else TryCraftSelected();
                    break;
                case Page.Equipment: TryToggleSelectedEquipment(); break;
            }
        }

        private void TryUseOrPlaceSelectedInventoryItem()
        {
            var item = CurrentInventoryItem();
            if (item == null) return;
            if (item.Id == WorldTileTypes.IceShard)
            {
                FindAnyObjectByType<MainGamePlayerController>()?.TryUseIceShardFromInventory(inventorySelectedIndex);
                Refresh();
                return;
            }
            if (PlayerHealthRecoveryService.IsSupportedHealingItemId(item.Id))
            {
                var recovery = runtimeServices?.PlayerHealthRecovery;
                if (recovery != null && recovery.TryUseHealingItem(item.Id, out var restoredHealth, inventorySelectedIndex))
                {
                    ShowMessage($"{item.DisplayName} 사용 · HP +{restoredHealth}");
                    Refresh();
                }
                else ShowMessage("HP가 가득 찼거나 회복 아이템을 사용할 수 없습니다.");
                return;
            }
            var talisman = gameDataCatalog?.FindTalisman(item.Id);
            if (talisman != null && TalismanRuntime.IsConsumableId(item.Id))
            {
                var talismanMessage = string.Empty;
                if (runtimeServices?.Talismans != null &&
                    runtimeServices.Talismans.TryUse(item.Id, out talismanMessage, inventorySelectedIndex))
                {
                    ShowMessage(talismanMessage);
                    Refresh();
                }
                else ShowMessage(string.IsNullOrEmpty(talismanMessage)
                    ? "현재 이 부적을 사용할 수 없습니다."
                    : talismanMessage);
                return;
            }
            var summonBoss = stationSource?.FindBossForSummonItem(item.Id);
            if (summonBoss != null)
            {
                if (!stationSource.CanUseSummonItem(item.Id, out _, out var reason))
                {
                    if (inventoryHintText != null) inventoryHintText.text = reason;
                    return;
                }
                OpenSummonConfirmation(summonBoss);
                return;
            }
            if (!IsInventoryPlaceable(item)) return;
            if (tilePalette == null) tilePalette = FindAnyObjectByType<MainGameTilePaletteController>();
            if (tilePalette != null && tilePalette.TryBeginPlacement(item.Id, inventorySelectedIndex))
                SetOpen(false);
            else if (!MainGameTilePaletteController.SupportsPalettePlacement(item.Id) &&
                     turretRuntime != null &&
                     turretRuntime.BeginPlacementPreview(item.Id, inventorySelectedIndex))
                SetOpen(false);
            else
                ShowMessage("설치 미리보기를 시작할 수 없습니다.");
        }

        private void OpenSummonConfirmation(BossDefinition definition)
        {
            if (definition?.SummonItem == null || summonConfirmationRoot == null) return;
            pendingSummonItemId = definition.SummonItem.Id;
            summonConfirmationText.text =
                $"{definition.SummonItem.DisplayName}을 사용해\n{definition.DisplayName}을 소환할까요?\n우클릭 확인 · Esc 취소";
            summonConfirmationRoot.SetActive(true);
            summonConfirmationRoot.transform.SetAsLastSibling();
        }

        private void ConfirmSummonItemUse()
        {
            if (string.IsNullOrEmpty(pendingSummonItemId) || stationSource == null) return;
            var itemId = pendingSummonItemId;
            CancelSummonConfirmation();
            if (stationSource.TryUseSummonItem(itemId)) SetOpen(false);
            else Refresh();
        }

        private void CancelSummonConfirmation()
        {
            pendingSummonItemId = string.Empty;
            if (summonConfirmationRoot != null) summonConfirmationRoot.SetActive(false);
        }

        private void TryCraftSelected()
        {
            var recipe = CurrentRecipe();
            if (recipe == null) { ShowMessage("표시할 제작법이 없습니다."); return; }
            if (!RecipeUnlockPolicy.IsUnlocked(recipe, runtimeServices.RecipeBook))
            { ShowMessage("아직 해금되지 않은 제작법입니다."); return; }
            var nearby = NearbyStation();
            if (recipe.Station != CraftingStation.None && recipe.Station != nearby)
            { ShowMessage($"{StationLabel(recipe.Station)} 근처에서 제작해야 합니다."); return; }
            if (recipe.Station == CraftingStation.Smithy &&
                runtimeServices?.Seokbinggo?.IsSmithyUnlocked != true)
            {
                ShowMessage("대장간은 석빙고 4단계 이상에서 해금됩니다.");
                return;
            }

            var missingMaterials = DescribeMissingMaterials(recipe, runtimeServices.PlayerInventory);
            if (!string.IsNullOrEmpty(missingMaterials))
            {
                ShowMessage(missingMaterials);
                return;
            }
            var requirementsValid = runtimeServices.CraftingService.CanCraft(recipe, recipe.Station);
            var queue = ResolveProductionQueue();
            if (!runtimeServices.StationProduction.CanEnqueue(queue))
            { ShowMessage("제작 대기열이 가득 찼거나 제작대를 사용할 수 없습니다."); return; }
            var succeeded = runtimeServices.StationProduction.TryEnqueue(
                queue, recipe, ResolveCraftDurationMultiplier(recipe));
            if (succeeded)
            {
                ShowMessage($"제작 예약: {recipe.Output.item.DisplayName}");
                Debug.Log($"[Nyangbingo] Product crafting accepted: {recipe.Id}, station={recipe.Station}.");
            }
            else ShowMessage(requirementsValid && recipe.DurationSeconds == 0f
                ? "완성품을 넣을 인벤토리 공간이 부족합니다. 소지품을 정리해 주세요."
                : "제작을 시작할 수 없습니다. 제작 조건을 확인해 주세요.");
        }

        private void TryCraftFromWindow()
        {
            if (open && page == Page.Crafting && !IsShowingSmeltingList)
            {
                TryCraftSelected();
                return;
            }
            if (craftWindowIndex < 0 || craftWindowIndex >= craftWindowRecipes.Count)
            {
                ShowMessage("표시할 제작법이 없습니다.");
                return;
            }
            var recipe = craftWindowRecipes[craftWindowIndex];
            var nearby = CraftingStation.None;
            var previousStation = openedStation;
            var previousId = openedStationObjectId;
            openedStation = recipe.Station == CraftingStation.None ? CraftingStation.None : nearby;
            openedStationObjectId = null;
            try
            {
                if (!RecipeUnlockPolicy.IsUnlocked(recipe, runtimeServices.RecipeBook))
                { ShowMessage("아직 해금되지 않은 제작법입니다."); return; }
                if (recipe.Station != CraftingStation.None && recipe.Station != nearby)
                { ShowMessage($"{StationLabel(recipe.Station)} 근처에서 제작해야 합니다."); return; }
                if (recipe.Station == CraftingStation.Smithy &&
                    runtimeServices?.Seokbinggo?.IsSmithyUnlocked != true)
                { ShowMessage("대장간은 석빙고 4단계 이상에서 해금됩니다."); return; }
                var missingMaterials = DescribeMissingMaterials(recipe, runtimeServices.PlayerInventory);
                if (!string.IsNullOrEmpty(missingMaterials))
                { ShowMessage(missingMaterials); return; }
                var requirementsValid = runtimeServices.CraftingService.CanCraft(recipe, recipe.Station);
                var queue = ResolveProductionQueue();
                if (!runtimeServices.StationProduction.CanEnqueue(queue))
                { ShowMessage("제작 대기열이 가득 찼거나 제작대를 사용할 수 없습니다."); return; }
                var succeeded = runtimeServices.StationProduction.TryEnqueue(
                    queue, recipe, ResolveCraftDurationMultiplier(recipe));
                ShowMessage(succeeded
                    ? $"제작 예약: {recipe.Output.item.DisplayName}"
                    : requirementsValid && recipe.DurationSeconds == 0f
                        ? "완성품을 넣을 인벤토리 공간이 부족합니다. 소지품을 정리해 주세요."
                        : "제작을 시작할 수 없습니다. 제작 조건을 확인해 주세요.");
            }
            finally
            {
                openedStation = previousStation;
                openedStationObjectId = previousId;
            }
        }

        public static string DescribeMissingMaterials(RecipeDefinition recipe,
            Nyangbingo.Inventory.Inventory inventory)
        {
            if (recipe?.Ingredients == null || inventory == null) return string.Empty;
            var totals = new Dictionary<string, long>();
            var names = new Dictionary<string, string>();
            foreach (var ingredient in recipe.Ingredients)
            {
                if (ingredient.item == null || ingredient.amount <= 0) return string.Empty;
                var id = ingredient.item.Id;
                totals.TryGetValue(id, out var amount);
                totals[id] = amount + ingredient.amount;
                names[id] = ingredient.item.DisplayName;
            }
            var missing = new List<string>();
            foreach (var requirement in totals)
            {
                var shortage = requirement.Value - inventory.Count(requirement.Key);
                if (shortage > 0) missing.Add($"{names[requirement.Key]} {shortage}개");
            }
            return missing.Count == 0 ? string.Empty : "재료 부족: " + string.Join(", ", missing);
        }

        private void TryPlaceSelectedCraftingOutput()
        {
            var recipe = CurrentRecipe();
            if (recipe == null || turretRuntime == null || !IsProductPlaceableRecipe(recipe, CurrentDay) ||
                turretRuntime.GetInventoryCount(recipe.Output.item.Id) <= 0) return;
            if (tilePalette == null) tilePalette = FindAnyObjectByType<MainGameTilePaletteController>();
            var beganPlacement = tilePalette != null && tilePalette.TryBeginPlacement(recipe.Output.item.Id);
            if (!beganPlacement) beganPlacement = turretRuntime.BeginPlacementPreview(recipe.Output.item.Id);
            if (beganPlacement) SetOpen(false);
            else ShowMessage("설치 미리보기를 시작할 수 없습니다.");
        }

        private void TrySmeltSelected()
        {
            var definition = CurrentSmelting();
            if (definition == null) { ShowMessage("표시할 제련법이 없습니다."); return; }
            var requiredStation = definition.StationKind == SmeltingStationKind.Foundry
                ? CraftingStation.Foundry
                : CraftingStation.Furnace;
            if (NearbyStation() != requiredStation)
            { ShowMessage($"{StationLabel(requiredStation)} 근처에서 제련해야 합니다."); return; }
            var queue = ResolveProductionQueue();
            if (!runtimeServices.StationProduction.CanSmelt(queue))
            {
                ShowMessage("빙결 구간에서는 제련 설비가 작동하지 않습니다.");
                return;
            }
            if (runtimeServices.StationProduction.TryEnqueue(queue, definition))
            {
                ShowMessage($"제련 대기열 추가: {definition.Output.item.DisplayName}");
                Debug.Log($"[Nyangbingo] Product smelting accepted: {definition.Id}, station={definition.StationKind}.");
            }
            else ShowMessage("재료·연료가 부족하거나 제련 대기열이 가득 찼습니다.");
        }

        private void TryCollectOutputs()
        {
            if (!open || page != Page.Crafting || !IsShowingSmeltingList) return;
            var definition = CurrentSmelting();
            if (definition == null) return;
            var station = definition.StationKind == SmeltingStationKind.Foundry
                ? runtimeServices.Foundry
                : runtimeServices.Furnace;
            var collected = runtimeServices.StationProduction.Collect(ResolveProductionQueue());
            while (station.Completed.Count > 0 && station.TryCollect(0)) collected++;
            if (collected > 0) GameEvents.RaiseCraftingCompleted();
            ShowMessage(collected > 0 ? $"완료품 {collected}묶음 회수" : "회수할 완료품이 없거나 인벤토리가 가득 찼습니다.");
        }

        private void TryCompleteCurrentProcessForDebug()
        {
            if (!SupportsDebugInstantCompletion || !open || page != Page.Crafting || runtimeServices == null)
                return;

            var queue = ResolveProductionQueue();
            if (queue != null && queue.jobs.Count > 0)
            {
                var job = queue.jobs[0];
                if (job.smelting && !runtimeServices.StationProduction.CanSmelt(queue))
                { ShowMessage("빙결 구간에서는 제련 설비가 작동하지 않습니다."); return; }
                job.remaining = 0f;
                runtimeServices.StationProduction.Tick(.001f);
                ShowMessage("[TEST] 현재 작업 완료");
                return;
            }

            if (!IsShowingSmeltingList)
            {
                var process = runtimeServices.CraftingProcess;
                if (process == null || !process.IsCrafting)
                {
                    ShowMessage("즉시 완료할 제작이 없습니다.");
                    return;
                }

                var outputName = process.Active.Output.item.DisplayName;
                var completed = process.Tick(Mathf.Max(process.RemainingSeconds, .001f));
                ShowMessage(completed
                    ? $"[TEST] 제작 즉시 완료: {outputName}"
                    : "[TEST] 인벤토리 공간이 부족해 완료품을 지급할 수 없습니다.");
                return;
            }

            var station = ResolveDisplayedSmeltingStation();
            if (station == null || !station.IsSmelting)
            {
                ShowMessage("즉시 완료할 제련이 없습니다.");
                return;
            }

            var smeltedName = station.Active.Output.item.DisplayName;
            var smeltingCompleted = station.Tick(Mathf.Max(station.RemainingSeconds, .001f));
            ShowMessage(smeltingCompleted
                ? $"[TEST] 제련 즉시 완료: {smeltedName} · 완료품 회수 가능"
                : "[TEST] 제련 즉시 완료에 실패했습니다.");
        }

        private SmeltingStation ResolveDisplayedSmeltingStation()
        {
            var definition = CurrentSmelting();
            if (definition != null)
                return definition.StationKind == SmeltingStationKind.Foundry
                    ? runtimeServices.Foundry
                    : runtimeServices.Furnace;

            var nearby = NearbyStation();
            if (nearby == CraftingStation.Foundry) return runtimeServices.Foundry;
            return nearby == CraftingStation.Furnace ? runtimeServices.Furnace : null;
        }

        private void TrySecondaryAction()
        {
            if (page == Page.Crafting && IsShowingSmeltingList) TryCollectOutputs();
            else if (page == Page.Crafting) TryPlaceSelectedCraftingOutput();
            else if (page == Page.Equipment) TryRefuelPortableLantern();
        }

        private void TryRefuelPortableLantern()
        {
            if (!open || page != Page.Equipment ||
                CurrentActiveSlotItem()?.Id != PortableLanternRuntime.LanternItemId ||
                runtimeServices.ActiveSlot.EquippedItemId != PortableLanternRuntime.LanternItemId) return;
            ShowMessage(runtimeServices.PortableLantern.TryAddFuel()
                ? $"휴대용 등불에 석탄 1개를 넣었습니다. 남은 연료: {Mathf.CeilToInt(runtimeServices.PortableLantern.FuelRemainingSeconds)}초"
                : "석탄이 부족합니다.");
        }

        private void ToggleActiveSlotFromEquipmentPage()
        {
            if (!open || page != Page.Equipment || runtimeServices?.ActiveSlot == null) return;
            if (!runtimeServices.ActiveSlot.Toggle()) return;
            ShowMessage(runtimeServices.ActiveSlot.IsUsingEquippedItem
                ? "활성 슬롯: 장착한 무기·도구"
                : "활성 슬롯: 맨 발톱(빈손)");
            Refresh();
        }

        private void SetOpen(bool value)
        {
            if (!value) HideDescription();
            if (!value) CancelEquipmentDrag();
            if (!value && !ReturnHeldInventoryItem() &&
                (shell == null || shell.Screen == GameShellScreen.Gameplay))
            {
                ShowMessage("원래 보관함에 반환할 공간이 없습니다. 빈 슬롯에 놓아주세요.");
                return;
            }
            if (!value || HasStorageMode) HideInventoryCursorPreview();
            if (value) openedFrame = Time.frameCount;
            if (open == value)
            {
                if (panel != null) panel.SetActive(value && !HasStorageMode);
                return;
            }
            open = value;
            if (!open)
            {
                openedStation = CraftingStation.None;
                openedStationObjectId = null;
                furnaceSmeltingView = false;
                craftingFilter = CraftingStationFilter.None;
                craftWindowIndex = 0;
                if (craftWindowToggleButton != null)
                    craftWindowToggleButton.gameObject.SetActive(false);
                storageObjectId = string.Empty;
                chestProgress = null;
                chestId = string.Empty;
                CancelSummonConfirmation();
            }
            if (!open && codexModel != null) codexModel.TapOutside();
            if (!open && codexExpandedBackdrop != null) codexExpandedBackdrop.SetActive(false);
            openControllerCount = Mathf.Max(0, openControllerCount + (open ? 1 : -1));
            if (panel != null) panel.SetActive(open && !HasStorageMode);
            message = string.Empty;
            Refresh();
        }

        private void Refresh()
        {
            if (panel == null || runtimeServices == null || !runtimeServices.IsInitialized) return;
            // Hide legacy material buttons that may remain after an editor script reload.
            foreach (Transform child in panel.transform)
                if (child.name.StartsWith("MissingMaterialSource_", StringComparison.Ordinal))
                    child.gameObject.SetActive(false);
            if (storageModeRoot != null) storageModeRoot.SetActive(open && HasStorageMode);
            // The inventory stays visible beside station UI, so inventory events
            // must update its slots regardless of the currently selected page.
            RefreshInventoryGridVisual();
            RefreshEquipmentVisual();
            if (!open)
            {
                RefreshCraftWindow();
                RefreshProductionQueue();
                ApplySideHudVisibility();
                return;
            }
            RefreshPageLayout();
            if (HasStorageMode)
            {
                RefreshProductionQueue();
                RefreshStorage();
                RefreshCraftWindow();
                ApplySideHudVisibility();
                return;
            }
            RefreshTabButtons();
            switch (page)
            {
                case Page.Gathering: RefreshInventory(); break;
                case Page.Crafting:
                    if (IsShowingSmeltingList) RefreshSmelting();
                    else RefreshCrafting();
                    break;
                case Page.Equipment: RefreshEquipment(); break;
                case Page.Codex: RefreshCodex(); break;
            }
            messageText.text = string.IsNullOrEmpty(message)
                ? DefaultHelpText()
                : message;
            RefreshCraftWindow();
            RefreshProductionQueue();
            ApplySideHudVisibility();
        }

        private void RefreshPageLayout()
        {
            var storageMode = HasStorageMode;
            if (storageModeRoot != null) storageModeRoot.SetActive(storageMode);
            if (storageMode)
            {
                foreach (var tab in tabButtons) if (tab != null) tab.gameObject.SetActive(false);
                previousButton.gameObject.SetActive(false);
                nextButton.gameObject.SetActive(false);
                primaryButton.gameObject.SetActive(false);
                collectButton.gameObject.SetActive(false);
                debugCompleteButton.gameObject.SetActive(false);
                messageText.gameObject.SetActive(false);
                SetInventoryWindowActive(true);
                if (equipmentVisualRoot != null) equipmentVisualRoot.SetActive(true);
                if (craftingListRoot != null) craftingListRoot.SetActive(false);
                if (codexGridRoot != null) codexGridRoot.SetActive(false);
                if (detailsViewportRect != null) detailsViewportRect.gameObject.SetActive(false);
                if (detailsScrollbarRect != null) detailsScrollbarRect.gameObject.SetActive(false);
                ApplyCraftWindowChrome(false);
                return;
            }
            foreach (var tab in tabButtons) if (tab != null) tab.gameObject.SetActive(false);
            var gathering = page == Page.Gathering;
            var equipment = page == Page.Equipment;
            var codex = page == Page.Codex;
            var recipeList = page == Page.Crafting && !IsShowingSmeltingList;
            var hasListActions = !gathering && !codex;
            previousButton.gameObject.SetActive(hasListActions && !recipeList);
            nextButton.gameObject.SetActive(hasListActions && !recipeList);
            primaryButton.gameObject.SetActive(!codex);
            var primaryRect = primaryButton.GetComponent<RectTransform>();
            primaryRect.anchoredPosition = new Vector2(48f, -34f);
            primaryRect.sizeDelta = new Vector2(100f, 16f);
            var collectRect = collectButton.GetComponent<RectTransform>();
            collectRect.anchoredPosition = new Vector2(-52f, -118f);
            collectRect.sizeDelta = new Vector2(130f, 20f);
            messageText.gameObject.SetActive(hasListActions);
            collectButton.gameObject.SetActive(false);
            debugCompleteButton.gameObject.SetActive(SupportsDebugInstantCompletion && page == Page.Crafting);
            SetInventoryWindowActive(true);
            if (inventoryHintText != null) inventoryHintText.gameObject.SetActive(false);
            if (equipmentVisualRoot != null) equipmentVisualRoot.SetActive(true);
            if (craftingListRoot != null) craftingListRoot.SetActive(recipeList);
            if (codexGridRoot != null) codexGridRoot.SetActive(codex);
            if (detailsViewportRect != null) detailsViewportRect.gameObject.SetActive(!gathering && !codex);
            if (detailsScrollbarRect != null) detailsScrollbarRect.gameObject.SetActive(!gathering && !codex);

            if (!gathering && !codex)
            {
                detailsViewportRect.sizeDelta = new Vector2(118f, 52f);
                detailsViewportRect.anchoredPosition = new Vector2(42f, 12f);
                detailsScrollbarRect.sizeDelta = new Vector2(6f, 52f);
                detailsScrollbarRect.anchoredPosition = new Vector2(104f, 12f);
            }
            ApplyCraftWindowChrome(page == Page.Crafting);
        }

        private void RefreshCrafting()
        {
            collectButton.gameObject.SetActive(false);
            debugCompleteButton.interactable = ResolveProductionQueue()?.jobs.Count > 0 ||
                                              runtimeServices.CraftingProcess?.IsCrafting == true;
            RebuildFilteredRecipes();
            RefreshCraftingList();
            var recipe = CurrentRecipe();
            if (recipe == null)
            {
                titleText.text = $"{CraftingFilterTitlePrefix()} · 해금된 제작법 없음";
                detailsText.text = "탭별 제작 목록입니다. 실행은 해당 설비 근처에서 가능합니다.";
                primaryButton.interactable = false;
                return;
            }
            var readyToPlace = turretRuntime != null && IsProductPlaceableRecipe(recipe, CurrentDay) &&
                               turretRuntime.GetInventoryCount(recipe.Output.item.Id) > 0;
            primaryButton.GetComponentInChildren<Text>().text = "제작 예약";
            titleText.text =
                $"{CraftingFilterTitlePrefix()} {selectedIndex + 1}/{filteredRecipes.Count} · {recipe.Output.item.DisplayName}";
            var stationOk = recipe.Station == CraftingStation.None || recipe.Station == NearbyStation();
            var canCraft = runtimeServices.StationProduction.CanEnqueue(ResolveProductionQueue()) && stationOk &&
                           RecipeUnlockPolicy.IsUnlocked(recipe, runtimeServices.RecipeBook) &&
                           runtimeServices.CraftingService.CanCraft(recipe, recipe.Station);
            primaryButton.interactable = canCraft;
            var builder = new StringBuilder();
            builder.AppendLine($"결과: {recipe.Output.item.DisplayName} ×{recipe.Output.amount}");
            builder.AppendLine($"제작대: {StationLabel(recipe.Station)} " +
                               (stationOk ? "(현재 사용 가능)" : "(근처로 이동 필요)"));
            builder.AppendLine($"시간: {recipe.DurationSeconds:0.#} 게임초");
            builder.AppendLine("재료:");
            foreach (var ingredient in recipe.Ingredients)
            {
                var owned = runtimeServices.PlayerInventory.Count(ingredient.item.Id);
                builder.AppendLine($"  · {ingredient.item.DisplayName} {owned}/{ingredient.amount}");
            }
            if (turretRuntime != null && IsProductPlaceableRecipe(recipe, CurrentDay))
                builder.AppendLine($"\n완성품 보유: {turretRuntime.GetInventoryCount(recipe.Output.item.Id)} · " +
                                   (readyToPlace ? "퀵슬롯에서 선택해 설치 가능" : "제작 완료 후 설치 가능"));
            if (runtimeServices.CraftingProcess.IsCrafting)
                builder.AppendLine($"\n진행 중: {runtimeServices.CraftingProcess.Active.Output.item.DisplayName} " +
                                   $"{runtimeServices.CraftingProcess.RemainingSeconds:0.0}초");
            if (recipe.Id == "workbench")
                builder.AppendLine(readyToPlace
                    ? "\n다음: 퀵슬롯에서 선택 → 초록 위치에 우클릭"
                    : "\n흙·돌 블록에 좌클릭 유지로 채굴\n떨어진 재료 가까이 이동해 줍기");
            detailsText.text = builder.ToString();
        }

        private void RefreshStorage()
        {
            // Chests only need their name and slots. Keep the cooling status for jars.
            if (storageHintText != null) storageHintText.gameObject.SetActive(chestProgress == null);
            if (storageHintText != null) storageHintText.text = string.IsNullOrEmpty(message) ? StorageDragHelp : message;
            if (storageTemperatureTrack != null) storageTemperatureTrack.gameObject.SetActive(false);
            Nyangbingo.Inventory.Inventory storage;
            if (chestProgress != null)
            {
                titleText.text = "자연 상자 · 좌클릭 전체 · 우클릭 절반/1개";
                if (storageLabelText != null)
                    storageLabelText.text = "상자";
                if (!chestProgress.TryGetContents(chestId, out storage))
                {
                    chestProgress = null;
                    chestId = string.Empty;
                    OpenPage(Page.Gathering);
                    SetOpen(false);
                    return;
                }
            }
            else if (!runtimeServices.JangdokStorage.TryGet(storageObjectId, out storage))
            {
                storageObjectId = string.Empty;
                OpenPage(Page.Gathering);
                SetOpen(false);
                return;
            }
            else
            {
                titleText.text = "장독 창고 · 좌클릭 전체 · 우클릭 절반/1개";
                var storageTemperature = runtimeServices.StorageTemperature;
                if (storageTemperature != null &&
                    storageTemperature.TryGetStatus(storageObjectId, out var temperature, out var band))
                {
                    if (storageLabelText != null)
                        storageLabelText.text = $"장독 창고 · {temperature:0.#}℃ · " +
                                                StorageTemperatureService.BandIcon(band);
                    if (storageHintText != null && storageTemperature.TryGetCondition(storageObjectId, out var condition))
                    {
                        var comparison = condition.Met ? "≤" : ">";
                        var values = $"{temperature:0.#}°C {comparison} {condition.RequiredTemperature:0.#}°C";
                        storageHintText.text = !condition.HasRequirement ? string.Empty :
                            condition.IceMeltProtected && condition.Met
                                ? gameDataCatalog.FindGuideMessage("storage_cooler")?.Text ?? string.Empty :
                            condition.HasIce && !condition.Met
                                ? (gameDataCatalog.FindGuideMessage("storage_below")?.Text ?? string.Empty)
                                    .Replace("{temp}", temperature.ToString("0.#"))
                                    .Replace("{req}", condition.RequiredTemperature.ToString("0.#"))
                                : condition.HasIce && condition.Met && storageTemperature.HasRecentSuccess(storageObjectId)
                                    ? gameDataCatalog.FindGuideMessage("storage_ok")?.Text ?? string.Empty
                                    : (condition.Met ? "❄ " : "") + values;
                        storageHintText.text = !string.IsNullOrEmpty(message) ? message :
                            StorageDragHelp + (string.IsNullOrEmpty(storageHintText.text) ? "" : "\n" + storageHintText.text);
                        RefreshStorageTemperatureBar(condition);
                    }
                    RefreshTransferSlots(storageLabels, storageIcons, storage.Slots,
                        itemId => storageTemperature.IsAtRisk(itemId, temperature, storageObjectId));
                    RefreshTransferSlots(storagePlayerLabels, storagePlayerIcons,
                        runtimeServices.PlayerInventory.Slots);
                    return;
                }
                if (storageLabelText != null)
                    storageLabelText.text = $"장독 창고 · {JangdokStorageRuntime.SlotCount}슬롯";
            }
            RefreshTransferSlots(storageLabels, storageIcons, storage.Slots);
            RefreshTransferSlots(storagePlayerLabels, storagePlayerIcons,
                runtimeServices.PlayerInventory.Slots);
        }

        private void RefreshStorageTemperatureBar(StorageConditionState condition)
        {
            if (storageLabelText == null || !condition.HasRequirement) return;
            if (storageTemperatureTrack == null)
            {
                storageTemperatureTrack = StorageBarPart("StorageTemperature", storageLabelText.transform,
                    new Color(.12f, .16f, .22f));
                var track = storageTemperatureTrack.rectTransform;
                track.anchorMin = track.anchorMax = new Vector2(0, 1);
                track.anchoredPosition = new Vector2(0, 2);
                storageTemperatureCold = StorageBarPart("ColdRange", track, new Color(.2f, .5f, .75f));
                storageTemperatureRequired = StorageBarPart("Required", track, new Color(.4f, .85f, 1f));
                storageTemperatureCurrent = StorageBarPart("Current", track, Color.white);
            }
            storageTemperatureTrack.gameObject.SetActive(true);
            var width = storageLabelText.rectTransform.rect.width;
            var service = runtimeServices.StorageTemperature;
            var margin = Mathf.Max(1f, service.ChilledMaximum - service.FrozenMaximum);
            var low = Mathf.Min(service.FrozenMaximum, condition.Temperature) - margin;
            var high = Mathf.Max(service.ChilledMaximum, condition.Temperature) + margin;
            var requiredX = width * Mathf.InverseLerp(low, high, condition.RequiredTemperature);
            var currentX = width * Mathf.InverseLerp(low, high, condition.Temperature);
            storageTemperatureTrack.rectTransform.sizeDelta = new Vector2(width, 3);
            storageTemperatureCold.rectTransform.sizeDelta = new Vector2(requiredX, 3);
            storageTemperatureRequired.rectTransform.sizeDelta = new Vector2(2, 7);
            storageTemperatureRequired.rectTransform.anchoredPosition = new Vector2(requiredX, 0);
            storageTemperatureCurrent.rectTransform.sizeDelta = new Vector2(1, 5);
            storageTemperatureCurrent.rectTransform.anchoredPosition = new Vector2(currentX, 0);
            storageTemperatureCurrent.color = condition.Met ? Color.white : new Color(1f, .85f, .2f);
        }

        private static Image StorageBarPart(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, .5f); rect.pivot = new Vector2(0, .5f);
            var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
            return image;
        }

        public static string BuildStorageRiskHint(float currentCelsius, int foodRiskCount, int iceRiskCount,
            float chilledMaximum, float frozenMaximum)
        {
            if (foodRiskCount <= 0 && iceRiskCount <= 0)
                return "보관 등급 충족 · 좌클릭: 전체 집기/놓기 · 우클릭: 절반 집기/1개 놓기 · E 또는 ESC: 닫기";
            var target = foodRiskCount > 0 && iceRiskCount > 0 ? "음식·얼음" : iceRiskCount > 0 ? "얼음" : "음식";
            var required = iceRiskCount > 0 ? frozenMaximum : chilledMaximum;
            return $"⚠ {target} {foodRiskCount + iceRiskCount}슬롯 위험 · 현재 {currentCelsius:0.#}℃ / 필요 {required:0.#}℃ 이하 · 코어 범위·밀폐 확인";
        }

        private void RefreshTransferSlots(Text[] labels, Image[] icons, IReadOnlyList<InventorySlot> slots,
            Func<string, bool> showWarning = null)
        {
            for (var index = 0; index < labels.Length; index++)
            {
                var label = labels[index];
                if (label == null) continue;
                var slot = index < slots.Count ? slots[index] : default;
                var item = string.IsNullOrEmpty(slot.itemId) ? null : gameDataCatalog.FindItem(slot.itemId);
                var icon = index < icons.Length ? icons[index] : null;
                if (icon != null)
                {
                    icon.sprite = item != null ? itemArtCatalog?.FindSprite(item.Id) : null;
                    icon.enabled = icon.sprite != null;
                }
                var count = item != null && slot.amount > 1 ? slot.amount.ToString() : string.Empty;
                var warning = item != null && showWarning?.Invoke(slot.itemId) == true ? "⚠" : string.Empty;
                var condition = item != null && slot.hasStorageCondition
                    ? $" {Mathf.RoundToInt(slot.EffectiveStorageCondition * 100f)}%"
                    : string.Empty;
                label.text = count;
                label.gameObject.SetActive(true);
                label.enabled = true;
                label.alignment = TextAnchor.LowerRight;
                label.fontSize = 8;
                label.resizeTextForBestFit = false;
                label.raycastTarget = false;
                label.color = Color.white;
                label.transform.SetAsLastSibling();
                var transferCountRect = label.rectTransform;
                transferCountRect.anchorMin = Vector2.zero;
                transferCountRect.anchorMax = Vector2.one;
                transferCountRect.offsetMin = new Vector2(1f, 1f);
                transferCountRect.offsetMax = new Vector2(-1f, -1f);
                var status = label.transform.parent.Find("StorageConditionStatus")?.GetComponent<Text>();
                var statusText = (warning + condition).Trim();
                if (status == null && statusText.Length > 0)
                    status = CreateText(label.transform.parent, "StorageConditionStatus", 6,
                        TextAnchor.UpperLeft, Vector2.zero, Vector2.zero);
                if (status != null)
                {
                    status.gameObject.SetActive(statusText.Length > 0);
                    status.text = statusText;
                    status.font = label.font;
                    status.fontStyle = label.fontStyle;
                    status.alignment = TextAnchor.UpperLeft;
                    status.raycastTarget = false;
                    status.resizeTextForBestFit = true;
                    status.resizeTextMinSize = 3;
                    status.resizeTextMaxSize = 6;
                    status.horizontalOverflow = HorizontalWrapMode.Wrap;
                    status.verticalOverflow = VerticalWrapMode.Truncate;
                    var statusRect = status.rectTransform;
                    statusRect.anchorMin = new Vector2(0f, 1f);
                    statusRect.anchorMax = Vector2.one;
                    statusRect.pivot = new Vector2(0f, 1f);
                    statusRect.anchoredPosition = new Vector2(-1f, 1f);
                    statusRect.sizeDelta = new Vector2(-2f, 6f);
                    status.transform.SetAsLastSibling();
                }
            }
        }

        private void RefreshCraftingList()
        {
            for (var index = 0; index < craftingListButtons.Length; index++)
            {
                var button = craftingListButtons[index];
                var label = craftingListLabels[index];
                if (button == null || label == null) continue;
                if (index >= filteredRecipes.Count)
                {
                    button.gameObject.SetActive(false);
                    continue;
                }

                button.gameObject.SetActive(true);
                var recipe = filteredRecipes[index];
                var outputIcon = craftingListOutputIcons[index];
                if (outputIcon != null)
                {
                    outputIcon.sprite = itemArtCatalog?.FindSprite(recipe.Output.item.Id);
                    outputIcon.enabled = outputIcon.sprite != null;
                }
                if (craftingListOutputCounts[index] != null)
                    craftingListOutputCounts[index].text = recipe.Output.amount > 1
                        ? recipe.Output.amount.ToString()
                        : string.Empty;

                var lacksMaterials = false;
                var ingredientIcons = craftingListIngredientIcons[index];
                var ingredientCounts = craftingListIngredientCounts[index];
                for (var ingredientIndex = 0; ingredientIndex < ingredientIcons.Length; ingredientIndex++)
                {
                    var hasIngredient = ingredientIndex < recipe.Ingredients.Length;
                    var ingredient = hasIngredient ? recipe.Ingredients[ingredientIndex] : default;
                    var icon = ingredientIcons[ingredientIndex];
                    var count = ingredientCounts[ingredientIndex];
                    if (!hasIngredient || ingredient.item == null)
                    {
                        if (icon != null) icon.enabled = false;
                        if (count != null) count.text = string.Empty;
                        continue;
                    }

                    var owned = runtimeServices.PlayerInventory.Count(ingredient.item.Id);
                    var isMissing = owned < ingredient.amount;
                    lacksMaterials |= isMissing;
                    if (icon != null)
                    {
                        icon.sprite = itemArtCatalog?.FindSprite(ingredient.item.Id);
                        icon.enabled = icon.sprite != null;
                        icon.color = isMissing ? new Color(.6f, .6f, .6f, 1f) : Color.white;
                    }
                    if (count != null)
                    {
                        count.text = $"{owned}/{ingredient.amount}";
                        count.color = isMissing ? new Color(1f, .32f, .32f, 1f) : new Color(.93f, .96f, 1f);
                    }
                }

                label.text = "▶";
                if (button.targetGraphic is Image image)
                    image.color = lacksMaterials
                        ? index == selectedIndex
                            ? new Color(.29f, .31f, .34f, 1f)
                            : new Color(.19f, .2f, .22f, 1f)
                        : index == selectedIndex
                            ? new Color(.24f, .46f, .68f, 1f)
                            : new Color(.16f, .24f, .34f, 1f);
            }
        }

        private void RefreshSmelting()
        {
            primaryButton.GetComponentInChildren<Text>().text = "제련 예약";
            collectButton.GetComponentInChildren<Text>().text = "완료품 회수";
            collectButton.gameObject.SetActive(true);
            debugCompleteButton.interactable = ResolveProductionQueue()?.jobs.Count > 0 ||
                                              ResolveDisplayedSmeltingStation()?.IsSmelting == true;
            var definition = CurrentSmelting();
            if (definition == null)
            {
                titleText.text = $"{CraftingFilterTitlePrefix()} · 표시 가능한 제련법 없음";
                detailsText.text = string.Empty;
                primaryButton.interactable = collectButton.interactable = false;
                return;
            }
            var requiredStation = definition.StationKind == SmeltingStationKind.Foundry
                ? CraftingStation.Foundry
                : CraftingStation.Furnace;
            var station = definition.StationKind == SmeltingStationKind.Foundry
                ? runtimeServices.Foundry
                : runtimeServices.Furnace;
            var stationOk = NearbyStation() == requiredStation;
            var production = ResolveProductionQueue();
            titleText.text =
                $"{CraftingFilterTitlePrefix()} {selectedIndex + 1}/{smeltingRecipes.Count} · {definition.Output.item.DisplayName}";
            primaryButton.interactable = stationOk && runtimeServices.StationProduction.CanEnqueue(production) &&
                                        runtimeServices.StationProduction.CanSmelt(production);
            collectButton.interactable = station.Completed.Count > 0 || production?.returns.Count > 0;
            detailsText.text =
                $"제련소: {StationLabel(requiredStation)} {(stationOk ? "(현재 사용 가능)" : "(근처로 이동 필요)")}\n" +
                $"재료: {definition.Input.item.DisplayName} " +
                $"{runtimeServices.PlayerInventory.Count(definition.Input.item.Id)}/{definition.Input.amount}\n" +
                $"연료: {definition.Fuel.item.DisplayName} " +
                $"{runtimeServices.PlayerInventory.Count(definition.Fuel.item.Id)}/{definition.Fuel.amount}\n" +
                $"결과: {definition.Output.item.DisplayName} ×{definition.Output.amount}\n" +
                $"시간: {definition.DurationSeconds:0.#} 게임초\n\n" +
                (runtimeServices.StationProduction.CanSmelt(production)
                    ? "아래 대기열에서 진행·취소·회수할 수 있습니다."
                    : "빙결 구간 · 제련 진행 일시 정지") +
                (station.IsSmelting ? $"\n이전 저장 제련: {station.RemainingSeconds:0.0}초" : string.Empty);
        }

        private void HideDescription()
        {
            descriptionDragPointerId = int.MinValue;
            if (descriptionRoot != null) descriptionRoot.SetActive(false);
            descriptionItemId = null;
        }

        private void ResetDescriptionPosition()
        {
            if (descriptionRoot == null) return;
            descriptionDragPointerId = int.MinValue;
            ((RectTransform)descriptionRoot.transform).anchoredPosition = DescriptionDefaultPosition;
        }

        private void BeginDescriptionDrag(PointerEventData pointer)
        {
            descriptionDragPointerId = int.MinValue;
            if (pointer.button != PointerEventData.InputButton.Left || descriptionRoot == null ||
                !descriptionRoot.activeSelf || Time.timeScale <= 0f ||
                shell != null && shell.Screen != GameShellScreen.Gameplay) return;
            var rect = (RectTransform)descriptionRoot.transform;
            if (rect.parent is not RectTransform parent ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, pointer.position,
                    pointer.pressEventCamera, out descriptionDragStartPointer)) return;
            descriptionDragStartPosition = rect.anchoredPosition;
            descriptionDragPointerId = pointer.pointerId;
            rect.SetAsLastSibling();
        }

        private void DragDescription(PointerEventData pointer)
        {
            if (descriptionDragPointerId != pointer.pointerId || descriptionRoot == null ||
                !descriptionRoot.activeSelf || Time.timeScale <= 0f ||
                shell != null && shell.Screen != GameShellScreen.Gameplay) return;
            var rect = (RectTransform)descriptionRoot.transform;
            if (rect.parent is not RectTransform parent ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, pointer.position,
                    pointer.pressEventCamera, out var localPointer)) return;
            var position = descriptionDragStartPosition + localPointer - descriptionDragStartPointer;
            // Keep the frame and its close button reachable within the canvas.
            var halfSize = Vector2.Scale(rect.rect.size, new Vector2(
                Mathf.Abs(rect.localScale.x), Mathf.Abs(rect.localScale.y))) * .5f;
            var limit = parent.rect.size * .5f - halfSize;
            position.x = Mathf.Clamp(position.x, -Mathf.Max(0f, limit.x), Mathf.Max(0f, limit.x));
            position.y = Mathf.Clamp(position.y, -Mathf.Max(0f, limit.y), Mathf.Max(0f, limit.y));
            rect.anchoredPosition = position;
        }

        private void ShowIngredientDescription(int index)
        {
            if (index < 0 || index >= displayedIngredientIds.Length ||
                !displayedIngredientMissing[index]) return;
            var id = displayedIngredientIds[index];
            var item = gameDataCatalog?.FindItem(id);
            if (item == null) return;
            ShowDescription(id, true, item.DisplayName + " · 획득처",
                MaterialSourceGuide.Describe(gameDataCatalog, id));
        }

        private bool ShowEquipmentDescription(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            var item = gameDataCatalog?.FindItem(id);
            var equipment = gameDataCatalog?.FindEquipment(id);
            if (item == null || equipment == null && !ActiveSlotSystem.IsAllowedItemId(id) &&
                item.Category != ItemCategory.Weapon && gameDataCatalog.FindCombatProfile(id) == null)
                return false;
            ShowDescription(id, false, item.DisplayName, BuildItemEquipmentDescription(item, equipment));
            return true;
        }

        private bool ShowGeneralItemDescription(string id)
        {
            var item = gameDataCatalog?.FindItem(id);
            if (item == null) return false;
            ShowDescription(id, false, item.DisplayName,
                ItemPlayerGuide.Describe(item) + "\n\n" + ItemPlayerGuide.Acquisition(gameDataCatalog, id),
                includeAcquisition: true);
            return true;
        }

        private void ShowCraftOutputDescription(string id)
        {
            var item = gameDataCatalog?.FindItem(id);
            if (item == null) return;
            var equipment = gameDataCatalog.FindEquipment(id);
            var isEquipment = equipment != null || ActiveSlotSystem.IsAllowedItemId(id) ||
                              item.Category == ItemCategory.Weapon || gameDataCatalog.FindCombatProfile(id) != null;
            ShowDescription(id, false, item.DisplayName, isEquipment
                ? BuildItemEquipmentDescription(item, equipment) : ItemPlayerGuide.Describe(item));
        }

        private string BuildItemEquipmentDescription(ItemDefinition item, EquipmentDefinition equipment)
        {
            // CSV notes contain design history, balance comparisons and internal IDs.
            // Player descriptions use the actual gameplay fields instead of those notes.
            var builder = new StringBuilder();
            if (equipment != null)
            {
                var equippedSlot = FindEquippedSlot(equipment);
                builder.AppendLine($"{EquipmentSlotLabel(equipment.Slot)} · " +
                    (equippedSlot.HasValue ? "장착 중" : "장착하지 않음"));
                if (equipment.Defense != 0) builder.AppendLine($"방어력 +{equipment.Defense}");
                AppendEquipmentBonus(builder, "이동 속도", equipment.MovementBonus);
                AppendEquipmentBonus(builder, "채굴 치명타 확률", equipment.MiningCriticalBonus);
                AppendEquipmentBonus(builder, "체온 상승 속도", equipment.TemperatureRiseModifier);
                AppendEquipmentBonus(builder, "받는 화염 피해", equipment.FireDamageModifier);
                if (equipment.VisionRadiusBonus != 0f)
                    builder.AppendLine($"시야 반경 {equipment.VisionRadiusBonus:+0.#;-0.#;0}칸");
                if (equipment.GrantsDoubleJump) builder.AppendLine("공중에서 한 번 더 점프할 수 있습니다.");
                if (equipment.BlocksInventoryTheft) builder.AppendLine("소지품을 훔쳐 가는 것을 막아줍니다.");
                var verb = equipment.VerbId;
                if (verb == ArtifactVerbId.None) ArtifactVerbCatalog.TryGetVerb(item.Id, out verb);
                var effect = DescribeArtifactEffect(verb);
                if (!string.IsNullOrEmpty(effect)) builder.AppendLine().AppendLine(effect);
                if (verb != ArtifactVerbId.None)
                {
                    var condition = equipment.ActivationCondition switch
                    {
                        ArtifactActivationCondition.Deep => "깊은 지하에서 효과가 발동합니다.",
                        ArtifactActivationCondition.Surface => "지상에서 효과가 발동합니다.",
                        ArtifactActivationCondition.DaySurface => "낮에 지상에 있을 때 효과가 발동합니다.",
                        _ => "장착해야 효과를 받을 수 있습니다."
                    };
                    builder.AppendLine(condition);
                    if (equipment.UsageLimitPerDay > 0)
                        builder.AppendLine($"하루에 {equipment.UsageLimitPerDay}번 사용할 수 있습니다.");
                }
                if (equipment.HasColdTolerance)
                    builder.AppendLine().AppendLine($"주변 온도가 {equipment.ColdToleranceC}℃보다 낮으면 방어력이 감소합니다.");
                if (equipment.SetId == ArmorSetRules.SeolhanpungSetId)
                    builder.AppendLine().AppendLine("설한풍 한벌옷 3종을 함께 입으면 햇빛 화상에 면역이 됩니다.")
                        .AppendLine("세트 효과: 받는 화염 피해 −25%, 낮 체온 상승 속도 −20%")
                        .AppendLine("장비가 견딜 수 있는 온도보다 추운 곳에서는 세트 효과가 꺼집니다.");
            }
            else
            {
                var isClaw = ClawSlotSystem.IsClawItem(item.Id);
                var equipped = isClaw
                    ? runtimeServices.ClawSlot?.EquippedItemId == item.Id
                    : runtimeServices.ActiveSlot?.EquippedItemId == item.Id;
                builder.AppendLine(isClaw
                    ? equipped ? "발톱 칸에 장착 중" : "발톱 칸에 장착하지 않음"
                    : equipped ? "장착 중" : "장착하지 않음");
                var profile = gameDataCatalog.FindCombatProfile(item.Id);
                var gimmick = GimmickWeaponCombatRules.IsGimmickWeaponId(item.Id);
                if (gimmick)
                    builder.AppendLine($"현재 발톱 공격력의 {GimmickWeaponCombatRules.ResolveBonus(gameDataCatalog):0%}로 공격합니다.");
                else if (profile?.HasBasicAttack == true)
                    builder.AppendLine($"공격력 {profile.AttackDamage:0.#} · 초당 {profile.AttacksPerSecond:0.##}회 공격");
                if (profile?.HasBasicAttack == true)
                {
                    builder.AppendLine($"사거리 {profile.RangeTiles:0.#}칸");
                    if (profile.KnockbackTiles > 0f && !gimmick)
                        builder.AppendLine($"적을 {profile.KnockbackTiles:0.#}칸 밀쳐냅니다.");
                }
                if (profile != null && EvolvedFanCombatRules.IsFanAbilityWeapon(item.Id))
                {
                    if (!profile.HasBasicAttack) builder.AppendLine("일반 공격 대신 F키로 사용합니다.");
                    builder.AppendLine(EvolvedFanCombatRules.IsPullFanId(item.Id)
                        ? "F: 앞에 있는 적을 끌어당깁니다." : "F: 앞에 있는 적을 밀쳐냅니다.");
                }
                if (profile != null && EvolvedClawCombatRules.IsSangunClaw(profile))
                    builder.AppendLine("세 번 연속 할퀴며, 마지막 공격은 적을 밀쳐냅니다.");
                if (profile != null && EvolvedClawCombatRules.IsPerfectClaw(profile))
                    builder.AppendLine("넓은 범위를 할퀴어 여러 적을 공격합니다.");
                if (item.Id == "icesteel_claw") builder.AppendLine("공격한 적을 서리로 느리게 만듭니다.");
                if (item.Id == "iron_claw") builder.AppendLine("채굴할 때 치명타가 발생할 확률이 높아집니다.");
                if (BowCombatRules.IsBowWeaponId(item.Id))
                {
                    var ammo = gameDataCatalog.ItemDisplayName(BowCombatRules.AmmoItemId, "탄약");
                    builder.AppendLine($"한 발마다 {ammo} 1개를 사용합니다.")
                        .AppendLine($"보유 탄약: {runtimeServices.PlayerInventory.Count(BowCombatRules.AmmoItemId)}개");
                }
                if (item.Id == PortableLanternRuntime.LanternItemId && runtimeServices.PortableLantern != null)
                    builder.AppendLine($"장착하면 주변 {runtimeServices.PortableLantern.RadiusTiles:0.#}칸을 밝힙니다.")
                        .AppendLine($"현재 {(runtimeServices.PortableLantern.IsLit ? "켜짐" : "꺼짐")} · 남은 연료 {Mathf.CeilToInt(runtimeServices.PortableLantern.FuelRemainingSeconds)}초")
                        .AppendLine("장비창에서 R키 또는 연료 버튼으로 석탄을 넣으세요.");
                builder.AppendLine().AppendLine(isClaw
                    ? "장비 인형의 발톱 칸에 끼워야 채굴과 공격에 쓰입니다."
                    : "장착한 뒤 Q키로 맨 발톱과 바꿔 사용할 수 있습니다.");
            }
            return builder.ToString().TrimEnd();
        }

        private static void AppendEquipmentBonus(StringBuilder builder, string label, float value)
        {
            if (value != 0f) builder.AppendLine($"{label} {value:+0%;-0%;0%}");
        }

        private static string DescribeArtifactEffect(ArtifactVerbId verb) => verb switch
        {
            ArtifactVerbId.GrabKnockedTarget => "밀쳐낸 적을 잠시 붙잡아 둡니다.",
            ArtifactVerbId.HearIronVein => "주변 철광맥을 찾는 데 도움을 줍니다.",
            ArtifactVerbId.ExtendCoolerRadius => "얼음 보관을 도와 자연적으로 녹는 속도를 늦춥니다.",
            ArtifactVerbId.NoHeatInShade => "그늘에서는 더위로 체온이 올라가지 않습니다.",
            ArtifactVerbId.ReduceFlameAndHasten => "받는 화염 피해를 줄이고 이동 속도를 높입니다.",
            ArtifactVerbId.NoFirstStrike => "먼저 공격하지 않으면 적이 선제공격하지 않게 해줍니다.",
            ArtifactVerbId.WalkWhileCharging => "원거리 무기를 준비하는 동안에도 걸을 수 있습니다.",
            ArtifactVerbId.MaintainAfterShutdown => "설비가 멈춘 뒤에도 효과가 잠시 유지됩니다.",
            ArtifactVerbId.EscapeOnSwallow => "영노에게 삼켜졌을 때 빠져나올 수 있습니다.",
            ArtifactVerbId.RelocateColdCore => "냉각 코어를 다른 자리로 옮길 수 있습니다.",
            ArtifactVerbId.HalveClayCraftTime => "점토 미장 제작에 걸리는 시간을 절반으로 줄입니다.",
            ArtifactVerbId.TurnWhileSliding => "얼음 위에서 미끄러지는 동안 방향을 바꾸기 쉬워집니다.",
            ArtifactVerbId.ExtendMagpieRadius => "까치가 더 넓은 범위에서 아이템을 찾습니다.",
            ArtifactVerbId.OpenStorageAnywhere => "멀리 떨어진 장독창고도 열 수 있습니다.",
            ArtifactVerbId.BonusTearOnCodex => "도감을 채울 때 요괴 눈물을 추가로 얻습니다.",
            ArtifactVerbId.FullDemolitionRecovery => "설치물을 철거할 때 재료를 온전히 회수합니다.",
            ArtifactVerbId.IncreaseVisionDeep => "깊은 지하에서 더 멀리 볼 수 있습니다.",
            ArtifactVerbId.ReduceFlameTag => "받는 화염 피해를 줄입니다.",
            ArtifactVerbId.ReduceOfferTears => "제단에 바칠 요괴 눈물이 줄어듭니다.",
            ArtifactVerbId.ShowDugPaths => "이미 파낸 길을 알아보기 쉽게 보여줍니다.",
            _ => string.Empty
        };

        private bool descriptionIncludesAcquisition;

        private void ShowDescription(string id, bool material, string title, string body,
            bool includeAcquisition = false)
        {
            if (Time.timeScale <= 0f || shell != null && shell.Screen != GameShellScreen.Gameplay) return;
            if (descriptionRoot != null && descriptionRoot.activeSelf &&
                descriptionItemId == id && descriptionIsMaterial == material &&
                descriptionIncludesAcquisition == includeAcquisition)
            {
                HideDescription();
                return;
            }
            if (descriptionRoot == null)
            {
                var canvas = panel.GetComponentInParent<Canvas>()?.rootCanvas;
                if (canvas == null || gameplayArtCatalog?.DescriptionFrame == null) return;
                // Screenshot box: x=552..1384, y=293..761 at 1920x1080.
                // Use the project's 480x270 logical coordinates, preserving 16:9 art.
                descriptionRoot = CreateUiObject("ItemDescriptionFrame", canvas.transform,
                    new Vector2(208f, 117f), DescriptionDefaultPosition);
                descriptionRoot.transform.localScale = Vector3.one * .85f;
                var frame = descriptionRoot.AddComponent<Image>();
                frame.sprite = gameplayArtCatalog.DescriptionFrame;
                frame.preserveAspect = true;
                frame.raycastTarget = true;
                descriptionTitle = CreateText(descriptionRoot.transform, "Title", 10,
                    TextAnchor.MiddleLeft, new Vector2(164f, 14f), new Vector2(-6f, 43f));
                var close = CreateButton(descriptionRoot.transform, "Close", "×",
                    new Vector2(93f, 45f), new Vector2(16f, 16f), HideDescription, false);
                ApplyDescriptionCornerButton(close, "UI/description_close");
                var scrollRoot = CreateUiObject("DescriptionScroll", descriptionRoot.transform,
                    new Vector2(176f, 64f), new Vector2(0f, -2f));
                var viewport = CreateUiObject("DescriptionViewport", scrollRoot.transform,
                    new Vector2(176f, 64f), Vector2.zero);
                viewport.AddComponent<Image>().color = Color.clear;
                viewport.AddComponent<RectMask2D>();
                descriptionBody = CreateText(viewport.transform, "Description", 8,
                    TextAnchor.UpperLeft, new Vector2(176f, 64f), Vector2.zero);
                descriptionBody.horizontalOverflow = HorizontalWrapMode.Wrap;
                descriptionBody.verticalOverflow = VerticalWrapMode.Overflow;
                var textRect = descriptionBody.rectTransform;
                textRect.anchorMin = new Vector2(0f, 1f);
                textRect.anchorMax = Vector2.one;
                textRect.pivot = new Vector2(.5f, 1f);
                textRect.anchoredPosition = Vector2.zero;
                textRect.sizeDelta = new Vector2(0f, 64f);
                descriptionScroll = scrollRoot.AddComponent<ScrollRect>();
                descriptionScroll.viewport = (RectTransform)viewport.transform;
                descriptionScroll.content = textRect;
                descriptionScroll.horizontal = false;
                descriptionScroll.vertical = true;
                descriptionScroll.movementType = ScrollRect.MovementType.Clamped;
                descriptionScroll.scrollSensitivity = 12f;
                var scrollbarRoot = CreateUiObject("DescriptionScrollbar", descriptionRoot.transform,
                    new Vector2(4f, 64f), new Vector2(93f, -2f));
                scrollbarRoot.AddComponent<Image>().color = new Color(.08f, .1f, .16f, 1f);
                descriptionScrollbar = scrollbarRoot.AddComponent<Scrollbar>();
                var slidingArea = CreateUiObject("SlidingArea", scrollbarRoot.transform,
                    new Vector2(4f, 64f), Vector2.zero);
                var handle = CreateUiObject("Handle", slidingArea.transform,
                    new Vector2(4f, 12f), Vector2.zero).AddComponent<Image>();
                handle.rectTransform.sizeDelta = Vector2.zero;
                handle.color = new Color(.46f, .55f, .7f, 1f);
                descriptionScrollbar.handleRect = handle.rectTransform;
                descriptionScrollbar.targetGraphic = handle;
                descriptionScrollbar.direction = Scrollbar.Direction.BottomToTop;
                descriptionScrollbar.numberOfSteps = 0;
                descriptionScroll.verticalScrollbar = descriptionScrollbar;
                descriptionScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
                var resetPosition = CreateButton(descriptionRoot.transform, "ResetPosition", "\u27F3",
                    new Vector2(86f, -40f), new Vector2(16f, 16f), ResetDescriptionPosition, false);
                if (!ApplyDescriptionCornerButton(resetPosition, "UI/description_reset"))
                {
                    var resetLabel = resetPosition.GetComponentInChildren<Text>();
                    resetLabel.font = Font.CreateDynamicFontFromOSFont(
                        new[] { "Segoe UI Symbol", "Arial Unicode MS", "Arial" }, 14);
                    resetLabel.fontSize = 12;
                    resetLabel.alignment = TextAnchor.MiddleCenter;
                    resetLabel.alignByGeometry = true;
                    var resetLabelRect = resetLabel.rectTransform;
                    resetLabelRect.anchorMin = Vector2.zero;
                    resetLabelRect.anchorMax = Vector2.one;
                    resetLabelRect.offsetMin = resetLabelRect.offsetMax = Vector2.zero;
                    resetLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
                    resetLabel.verticalOverflow = VerticalWrapMode.Overflow;
                }
                var dragHeader = CreateUiObject("DescriptionDragHeader", descriptionRoot.transform,
                    new Vector2(208f, 10f), new Vector2(0f, 53.5f));
                dragHeader.AddComponent<UnityEngine.UI.Image>().color = Color.clear;
                var dragTrigger = dragHeader.AddComponent<EventTrigger>();
                var beginDrag = new EventTrigger.Entry { eventID = EventTriggerType.BeginDrag };
                beginDrag.callback.AddListener(data => BeginDescriptionDrag((PointerEventData)data));
                dragTrigger.triggers.Add(beginDrag);
                var drag = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
                drag.callback.AddListener(data => DragDescription((PointerEventData)data));
                dragTrigger.triggers.Add(drag);
                var endDrag = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag };
                endDrag.callback.AddListener(_ => descriptionDragPointerId = int.MinValue);
                dragTrigger.triggers.Add(endDrag);
                close.transform.SetAsLastSibling();
            }
            descriptionItemId = id;
            descriptionIsMaterial = material;
            descriptionIncludesAcquisition = includeAcquisition;
            descriptionTitle.text = title;
            descriptionBody.text = body;
            descriptionRoot.SetActive(true);
            descriptionRoot.transform.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();
            descriptionBody.rectTransform.sizeDelta = new Vector2(0f,
                Mathf.Max(64f, descriptionBody.preferredHeight));
            descriptionScrollbar.gameObject.SetActive(descriptionBody.preferredHeight > 64f + .01f);
            descriptionScroll.StopMovement();
            descriptionScroll.verticalNormalizedPosition = 1f;
        }

        /// <summary>설명 창 모서리 버튼. 첫 장은 보통, 둘째 장은 누르고 있는 동안이다.</summary>
        private static bool ApplyDescriptionCornerButton(Button button, string resourcePath)
        {
            if (button == null || button.targetGraphic is not Image image) return false;
            var sprites = Resources.LoadAll<Sprite>(resourcePath);
            if (sprites == null || sprites.Length == 0 || sprites[0] == null) return false;
            image.sprite = sprites[0];
            image.color = Color.white;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            if (image.sprite.texture != null) image.sprite.texture.filterMode = FilterMode.Point;
            var label = button.GetComponentInChildren<Text>();
            if (label != null) label.gameObject.SetActive(false);
            if (sprites.Length > 1 && sprites[1] != null)
            {
                button.transition = Selectable.Transition.SpriteSwap;
                button.spriteState = new SpriteState
                {
                    pressedSprite = sprites[1],
                    disabledSprite = sprites[1]
                };
            }
            return true;
        }

        private RecipeDefinition CurrentRecipe() => filteredRecipes.Count == 0
            ? null
            : filteredRecipes[Mathf.Clamp(selectedIndex, 0, filteredRecipes.Count - 1)];

        public static bool IsProductPlaceableRecipe(RecipeDefinition recipe, int currentDay = 1)
        {
            if (recipe?.Output.item == null) return false;
            return recipe.Output.item.Category == ItemCategory.Placeable ||
                   recipe.Output.item.Category == ItemCategory.Station ||
                   recipe.Type == RecipeType.ColdSource || recipe.Type == RecipeType.Cooling ||
                   recipe.Type == RecipeType.Placeable || recipe.Type == RecipeType.Station ||
                   recipe.Type == RecipeType.Turret ||
                   string.Equals(recipe.Output.item.Id, CoolingSourceRuntime.IceStorageId,
                       StringComparison.Ordinal);
        }

        public static bool IsInventoryItemPlaceable(ItemDefinition item,
            IEnumerable<RecipeDefinition> recipes, int currentDay = 1)
        {
            if (item == null || item.Id == WorldTileTypes.IceShard) return false;
            if (MainGameTilePaletteController.SupportsPalettePlacement(item.Id)) return true;
            if (recipes == null) return false;
            return recipes.Any(recipe => recipe?.Output.item != null &&
                                         recipe.Output.item.Id == item.Id &&
                                         IsProductPlaceableRecipe(recipe, currentDay));
        }

        private SmeltingDefinition CurrentSmelting() => smeltingRecipes.Count == 0
            ? null
            : smeltingRecipes[Mathf.Clamp(selectedIndex, 0, smeltingRecipes.Count - 1)];

        private void RefreshInventory()
        {
            primaryButton.GetComponentInChildren<Text>().text = "인벤토리";
            primaryButton.interactable = false;
            collectButton.gameObject.SetActive(false);
            var inventory = runtimeServices.PlayerInventory;
            if (tilePalette == null) tilePalette = FindAnyObjectByType<MainGameTilePaletteController>();
            inventorySelectedIndex = Mathf.Clamp(inventorySelectedIndex, 0, Mathf.Max(0, inventory.Slots.Count - 1));
            var selectedItem = CurrentInventoryItem();
            var selectedEquipment = selectedItem != null &&
                (ActiveSlotSystem.IsAllowedItemId(selectedItem.Id) ||
                 ClawSlotSystem.IsClawItem(selectedItem.Id) ||
                 gameDataCatalog.FindEquipment(selectedItem.Id) != null);
            var summonBoss = selectedItem != null ? stationSource?.FindBossForSummonItem(selectedItem.Id) : null;
            var summonReason = string.Empty;
            var canSummon = summonBoss != null &&
                            stationSource.CanUseSummonItem(selectedItem.Id, out _, out summonReason);
            var canPlace = selectedItem != null && IsInventoryPlaceable(selectedItem) &&
                           inventory.Count(selectedItem.Id) > 0 &&
                           (MainGameTilePaletteController.SupportsPalettePlacement(selectedItem.Id)
                               ? tilePalette != null
                               : turretRuntime != null);
            var isCatnip = selectedItem?.Id == PlayerHealthRecoveryService.CatnipItemId;
            var canUseCatnip = isCatnip && runtimeServices.PlayerHealthRecovery?.CanUseCatnip == true;
            var healingItem = selectedItem != null && PlayerHealthRecoveryService.IsSupportedHealingItemId(selectedItem.Id);
            var canHeal = healingItem && runtimeServices.PlayerHealthRecovery?.CanUseHealingItem(selectedItem.Id) == true;
            var talismanItem = selectedItem != null && TalismanRuntime.IsConsumableId(selectedItem.Id);
            var iceShard = selectedItem?.Id == WorldTileTypes.IceShard;
            var canCool = iceShard && runtimeServices.PlayerTemperature.Current > runtimeServices.PlayerTemperature.Minimum;
            titleText.text = selectedItem == null
                ? string.Empty
                : $"{selectedItem.DisplayName} ×{inventory.Slots[inventorySelectedIndex].amount}";
            primaryButton.GetComponentInChildren<Text>().text = isCatnip
                ? canUseCatnip ? "E · 캣닢 사용 (HP +25)" : "HP가 가득 찼습니다"
                : summonBoss != null
                ? canSummon ? $"E · {selectedItem.DisplayName} 사용" : summonReason
                : canPlace ? $"E · {selectedItem.DisplayName} 설치 미리보기"
                : "설치 가능한 소지품을 선택하세요";
            if (!isCatnip && healingItem)
                primaryButton.GetComponentInChildren<Text>().text = canHeal
                    ? $"E · {selectedItem.DisplayName} 사용" : "HP가 가득 찼습니다";
            if (iceShard || talismanItem)
                primaryButton.GetComponentInChildren<Text>().text = $"E · {selectedItem.DisplayName} 사용";
            if (selectedEquipment)
                primaryButton.GetComponentInChildren<Text>().text = IsDraggingEquipment ? "초록 칸에 놓기" :
                    !string.IsNullOrEmpty(equipmentDropFeedback) && Time.unscaledTime < equipmentDropFeedbackUntil
                        ? equipmentDropFeedback : "드래그로 장착";
            primaryButton.interactable = canHeal || canCool || talismanItem || canSummon || canPlace;
            if (inventoryHintText != null)
            {
                inventoryHintText.gameObject.SetActive(true);
                inventoryHintText.text = !string.IsNullOrEmpty(message) ? message
                    : selectedEquipment ? "장착 가능한 칸은 초록색 · 장비는 가방에 보관됩니다."
                    : isCatnip
                    ? "사용 즉시 HP를 25 회복합니다."
                    : canSummon
                    ? $"사용 시 {summonBoss.DisplayName} 소환 확인창이 열립니다."
                    : summonBoss != null
                        ? summonReason
                        : string.Empty;
            }

            RefreshInventoryGridVisual();
        }

        private void RefreshInventoryGridVisual()
        {
            if (inventoryGridButtons == null || inventoryGridLabels == null ||
                runtimeServices?.PlayerInventory == null) return;
            var inventory = runtimeServices.PlayerInventory;
            for (var index = 0; index < inventoryGridLabels.Length; index++)
            {
                var label = inventoryGridLabels[index];
                var buttonImage = inventoryGridButtons[index]?.targetGraphic as Image;
                var icon = inventoryGridIcons != null && index < inventoryGridIcons.Length
                    ? inventoryGridIcons[index] : null;
                if (label == null) continue;
                if (buttonImage != null)
                {
                    buttonImage.sprite = gameplayArtCatalog?.InventorySlot;
                    RefreshSlotSelectionBorder(buttonImage, index == inventorySelectedIndex);
                    buttonImage.preserveAspect = true;
                    buttonImage.type = Image.Type.Simple;
                    buttonImage.color = buttonImage.sprite != null
                        ? Color.white
                        : index == inventorySelectedIndex
                            ? new Color(.22f, .42f, .62f, 1f)
                            : new Color(.12f, .17f, .25f, .96f);
                }
                if (index >= inventory.Slots.Count)
                {
                    label.text = string.Empty;
                    if (icon != null) icon.enabled = false;
                    continue;
                }

                var slot = inventory.Slots[index];
                var item = string.IsNullOrEmpty(slot.itemId) ? null : gameDataCatalog.FindItem(slot.itemId);
                label.text = FormatInventorySlotCount(item, slot.amount);
                label.gameObject.SetActive(true);
                label.enabled = true;
                label.alignment = TextAnchor.LowerRight;
                label.fontSize = 8;
                label.resizeTextForBestFit = false;
                label.raycastTarget = false;
                label.color = Color.white;
                label.transform.SetAsLastSibling();
                var countRect = label.rectTransform;
                countRect.anchorMin = Vector2.zero;
                countRect.anchorMax = Vector2.one;
                countRect.offsetMin = new Vector2(1f, 1f);
                countRect.offsetMax = new Vector2(-2f, -1f);
                if (icon != null)
                {
                    icon.sprite = item != null ? itemArtCatalog?.FindSprite(item.Id) : null;
                    icon.enabled = icon.sprite != null;
                    var dayLockedSummon = item != null && stationSource != null && !stationSource.IsNight &&
                                          stationSource.FindBossForSummonItem(item.Id) != null;
                    icon.color = dayLockedSummon ? new Color(.35f, .35f, .4f, .58f) : Color.white;
                }
            }
        }

        public static string FormatInventoryCount(ItemDefinition item, int amount,
            Nyangbingo.Inventory.Inventory inventory, GameDataCatalog catalog)
        {
            if (item == null || amount <= 0) return string.Empty;
            if (BowCombatRules.IsBowWeaponId(item.Id))
                return (inventory?.Count(BowCombatRules.AmmoItemId) ?? 0).ToString();
            if (item.Category == ItemCategory.Weapon ||
                item.Category == ItemCategory.Tool && catalog?.FindCombatProfile(item.Id) != null)
                return string.Empty;
            return amount.ToString();
        }

        private string FormatInventorySlotCount(ItemDefinition item, int amount) =>
            FormatInventoryCount(item, amount, runtimeServices?.PlayerInventory, gameDataCatalog);

        public static void MatchInventoryCountFont(Text target)
        {
            var labels = cursorOwner?.inventoryGridLabels;
            var source = labels != null && labels.Length > 0 ? labels[0] : null;
            target.font = source != null ? source.font : UiTextFont.Source;
            target.fontStyle = source != null ? source.fontStyle : FontStyle.Normal;
            target.fontSize = source != null ? source.fontSize : 8;
            target.resizeTextForBestFit = false;
            target.color = source != null ? source.color : Color.white;
            target.raycastTarget = false;
            target.horizontalOverflow = HorizontalWrapMode.Overflow;
            target.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private ItemDefinition CurrentInventoryItem()
        {
            var inventory = runtimeServices?.PlayerInventory;
            if (inventory == null || inventory.Slots.Count == 0) return null;
            var slot = inventory.Slots[Mathf.Clamp(inventorySelectedIndex, 0, inventory.Slots.Count - 1)];
            return string.IsNullOrEmpty(slot.itemId) ? null : gameDataCatalog.FindItem(slot.itemId);
        }

        private bool IsInventoryPlaceable(ItemDefinition item)
        {
            return IsInventoryItemPlaceable(item, visibleRecipes, CurrentDay);
        }

        private void RefreshEquipment()
        {
            collectButton.gameObject.SetActive(false);
            RebuildOwnedEquipment();
            var activeSlotItem = CurrentActiveSlotItem();
            var clawItem = activeSlotItem == null ? CurrentClawItem() : null;
            var equipment = activeSlotItem == null && clawItem == null ? CurrentEquipment() : null;
            var item = activeSlotItem ?? clawItem ??
                       (equipment != null ? gameDataCatalog.FindItem(equipment.Id) : null);
            if (item == null)
            {
                titleText.text = "보유 장비 없음";
                detailsText.text = BuildEquippedSummary();
                primaryButton.GetComponentInChildren<Text>().text = "장착";
                primaryButton.interactable = false;
                return;
            }
            var equipped = activeSlotItem != null
                ? runtimeServices.ActiveSlot.EquippedItemId == item.Id
                : clawItem != null
                    ? runtimeServices.ClawSlot?.EquippedItemId == item.Id
                    : FindEquippedSlot(equipment).HasValue;
            titleText.text = $"{item.DisplayName} · {(equipped ? "장착 중" : "장착하지 않음")}";
            primaryButton.GetComponentInChildren<Text>().text = equipped ? "E · 해제" : "E · 장착";
            primaryButton.interactable = true;
            if (equipped && item.Id == PortableLanternRuntime.LanternItemId)
            {
                collectButton.gameObject.SetActive(true);
                collectButton.GetComponentInChildren<Text>().text = "R · 석탄 1개 투입";
                collectButton.interactable = runtimeServices.PlayerInventory.Has(PortableLanternRuntime.FuelItemId, 1);
            }
            detailsText.text = BuildItemEquipmentDescription(item, equipment);
        }

        private void MatchEquipmentAmmoToInventory()
        {
            if (equipmentAmmoCount == null || equipmentSlotIcons[0] == null ||
                inventoryGridLabels == null || inventoryGridLabels.Length == 0 ||
                inventoryGridLabels[0] == null || inventoryGridIcons[0] == null) return;
            var source = inventoryGridLabels[0];
            var sourceIcon = inventoryGridIcons[0].rectTransform;
            var targetIcon = equipmentSlotIcons[0].rectTransform;
            if (targetIcon.parent is not RectTransform targetSlot) return;
            var iconRatio = targetIcon.rect.height / Mathf.Max(.0001f, sourceIcon.rect.height);
            var sourceScale = Mathf.Abs(source.rectTransform.lossyScale.y) /
                              Mathf.Max(.0001f, Mathf.Abs(sourceIcon.lossyScale.y));
            var labelRect = equipmentAmmoCount.rectTransform;
            labelRect.SetParent(targetSlot, false);
            labelRect.localScale = Vector3.one;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.pivot = Vector2.one * .5f;
            labelRect.offsetMin = new Vector2(1f, 1f) * iconRatio;
            labelRect.offsetMax = new Vector2(-2f, -1f) * iconRatio;
            equipmentAmmoCount.font = source.font;
            equipmentAmmoCount.fontStyle = source.fontStyle;
            equipmentAmmoCount.fontSize = Mathf.Max(1, Mathf.RoundToInt(source.fontSize * sourceScale * iconRatio));
            equipmentAmmoCount.resizeTextForBestFit = false;
            equipmentAmmoCount.alignment = TextAnchor.LowerRight;
            equipmentAmmoCount.color = source.color;
            equipmentAmmoCount.horizontalOverflow = HorizontalWrapMode.Overflow;
            equipmentAmmoCount.verticalOverflow = VerticalWrapMode.Overflow;
            equipmentAmmoCount.raycastTarget = false;
            labelRect.SetAsLastSibling();
        }

        private void RefreshEquipmentVisual()
        {
            if (equipmentVisualRoot == null || runtimeServices == null || !runtimeServices.IsInitialized) return;
            if (equipmentAmmoCount != null)
            {
                MatchEquipmentAmmoToInventory();
                var usesAmmo = BowCombatRules.IsBowWeaponId(EquippedItemIdForVisualSlot(0));
                equipmentAmmoCount.text = usesAmmo
                    ? runtimeServices.PlayerInventory.Count(BowCombatRules.AmmoItemId).ToString()
                    : string.Empty;
                equipmentAmmoCount.enabled = usesAmmo;
            }
            // 선택 테두리는 장비 탭을 연 동안에만 보여 준다.
            var selectedSlot = open && page == Page.Equipment
                ? ResolveSelectedEquipmentVisualSlot()
                : -1;
            for (var index = 0; index < equipmentSlotBackgrounds.Length; index++)
            {
                var background = equipmentSlotBackgrounds[index];
                if (background == null) continue;
                var itemId = EquippedItemIdForVisualSlot(index);
                // The body-part silhouette is baked into the empty equipment art.
                // Occupied slots use the plain inventory frame beneath the item.
                background.sprite = !string.IsNullOrEmpty(itemId)
                    ? gameplayArtCatalog?.InventorySlot
                    : EquipmentSlotSprite(index, false);
                background.color = background.sprite != null
                    ? Color.white
                        : new Color(.14f, .2f, .28f, 1f);
                var validDrop = IsDraggingEquipment && equipmentDrag.CanDrop(index);
                if (IsDraggingEquipment)
                    background.color = validDrop ? new Color(.45f, 1f, .55f, 1f) : new Color(.55f, .55f, .55f, 1f);
                RefreshSlotSelectionBorder(background, IsDraggingEquipment ? validDrop : index == selectedSlot);
                var button = equipmentSlotButtons[index];
                if (button != null) button.interactable = IsDraggingEquipment || !string.IsNullOrEmpty(itemId);
                var icon = equipmentSlotIcons[index];
                if (icon == null) continue;
                icon.sprite = itemArtCatalog?.FindSprite(itemId);
                icon.enabled = icon.sprite != null;
                var fallback = equipmentSlotFallbackLabels[index];
                if (fallback != null)
                {
                    fallback.text = icon.enabled ? string.Empty : EquipmentIconFallback(itemId);
                    fallback.enabled = !string.IsNullOrEmpty(fallback.text);
                }
            }
            if (equipmentCharacter != null)
            {
                equipmentCharacter.sprite = gameplayArtCatalog?.EquipmentCharacter;
                equipmentCharacter.enabled = equipmentCharacter.sprite != null;
            }
        }

        private void RefreshSlotSelectionBorder(Image background, bool selected)
        {
            if (!slotSelectionBorders.TryGetValue(background, out var border))
            {
                border = CreateUiObject("SelectionBorder", background.transform, Vector2.zero, Vector2.zero);
                var rect = (RectTransform)border.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                for (var side = 0; side < 4; side++)
                {
                    var image = CreateUiObject($"Edge_{side}", rect, Vector2.zero, Vector2.zero).AddComponent<Image>();
                    image.color = Color.white;
                    image.raycastTarget = false;
                    var edge = image.rectTransform;
                    var horizontal = side < 2;
                    edge.anchorMin = horizontal ? new Vector2(0f, side) : new Vector2(side - 2, 0f);
                    edge.anchorMax = horizontal ? new Vector2(1f, side) : new Vector2(side - 2, 1f);
                    edge.pivot = horizontal ? new Vector2(.5f, side) : new Vector2(side - 2, .5f);
                    edge.sizeDelta = horizontal ? new Vector2(0f, 1f) : new Vector2(1f, 0f);
                }
                slotSelectionBorders[background] = border;
            }
            border.SetActive(selected);
            border.transform.SetAsLastSibling();
        }

        private int ResolveSelectedEquipmentVisualSlot()
        {
            if (CurrentActiveSlotItem() != null) return 0;
            if (CurrentClawItem() != null) return ClawSlotSystem.VisualSlotIndex;
            var definition = CurrentEquipment();
            if (definition == null) return -1;
            var equippedSlot = FindEquippedSlot(definition);
            var slot = equippedSlot ?? definition.Slot;
            switch (slot)
            {
                case EquipmentSlot.Head: return 1;
                case EquipmentSlot.Body: return 2;
                case EquipmentSlot.Feet: return 3;
                case EquipmentSlot.AccessoryOne: return 4;
                case EquipmentSlot.AccessoryTwo: return 5;
                default:
                    if (runtimeServices?.ClawSlot != null &&
                        runtimeServices.ClawSlot.EquippedItemId == definition.Id)
                        return ClawSlotSystem.VisualSlotIndex;
                    return definition.IsAccessory ? 4 : -1;
            }
        }

        private string EquippedItemIdForVisualSlot(int index)
        {
            if (index == 0) return runtimeServices?.ActiveSlot?.EquippedItemId;
            if (index == ClawSlotSystem.VisualSlotIndex) return runtimeServices?.ClawSlot?.EquippedItemId;
            var slot = index switch
            {
                1 => EquipmentSlot.Head,
                2 => EquipmentSlot.Body,
                3 => EquipmentSlot.Feet,
                4 => EquipmentSlot.AccessoryOne,
                5 => EquipmentSlot.AccessoryTwo,
                _ => EquipmentSlot.Head
            };
            return runtimeServices?.EquipmentSystem?.Get(slot)?.Id;
        }

        private void SelectEquipmentVisualSlot(int visualSlotIndex)
        {
            if (!open || page != Page.Equipment ||
                visualSlotIndex < 0 || visualSlotIndex >= equipmentSlotBackgrounds.Length)
                return;
            RebuildOwnedEquipment();
            var itemId = EquippedItemIdForVisualSlot(visualSlotIndex);
            var entryIndex = ResolveEquipmentEntryIndex(
                itemId, activeSlotItems, ownedEquipment, clawItems);
            if (entryIndex < 0) return;
            selectedIndex = entryIndex;
            RefreshEquipment();
        }

        public static int ResolveEquipmentEntryIndex(
            string itemId, IReadOnlyList<ItemDefinition> activeItems,
            IReadOnlyList<EquipmentDefinition> equipment,
            IReadOnlyList<ItemDefinition> claws = null)
        {
            if (string.IsNullOrEmpty(itemId)) return -1;
            if (activeItems != null)
                for (var index = 0; index < activeItems.Count; index++)
                    if (string.Equals(activeItems[index]?.Id, itemId, StringComparison.Ordinal))
                        return index;
            var activeCount = activeItems?.Count ?? 0;
            if (claws != null)
                for (var index = 0; index < claws.Count; index++)
                    if (string.Equals(claws[index]?.Id, itemId, StringComparison.Ordinal))
                        return activeCount + index;
            var clawCount = claws?.Count ?? 0;
            if (equipment != null)
                for (var index = 0; index < equipment.Count; index++)
                    if (string.Equals(equipment[index]?.Id, itemId, StringComparison.Ordinal))
                        return activeCount + clawCount + index;
            return -1;
        }

        private Sprite EquipmentSlotSprite(int index, bool selected)
        {
            if (index == ClawSlotSystem.VisualSlotIndex) return ClawSlotArt();
            if (gameplayArtCatalog == null) return null;
            switch (index)
            {
                case 0: return selected ? gameplayArtCatalog.ActiveItemSlotSelected : gameplayArtCatalog.ActiveItemSlot;
                case 1: return selected
                    ? gameplayArtCatalog.EquipmentHeadSlotSelected ?? gameplayArtCatalog.InventorySlotSelected
                    : gameplayArtCatalog.EquipmentHeadSlot;
                case 2: return selected ? gameplayArtCatalog.EquipmentBodySlotSelected : gameplayArtCatalog.EquipmentBodySlot;
                case 3: return selected ? gameplayArtCatalog.EquipmentFeetSlotSelected : gameplayArtCatalog.EquipmentFeetSlot;
                default: return selected
                    ? gameplayArtCatalog.EquipmentAccessorySlotSelected
                    : gameplayArtCatalog.EquipmentAccessorySlot;
            }
        }

        private string EquipmentIconFallback(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return string.Empty;
            switch (itemId)
            {
                case "cheolseon": return "철선";
                case "hapjukseon": return "합죽";
                case "dokkaebi_club": return "방망";
                case PortableLanternRuntime.LanternItemId: return "등불";
            }
            var displayName = gameDataCatalog?.FindItem(itemId)?.DisplayName;
            if (string.IsNullOrEmpty(displayName)) return "?";
            return displayName.Length <= 2 ? displayName : displayName.Substring(0, 2);
        }

        private void RefreshCodex()
        {
            collectButton.gameObject.SetActive(false);
            titleText.text = "도감";
            detailsText.text = string.Empty;
            if (codexModel == null)
            {
                titleText.text = "진행 데이터를 불러올 수 없음";
                if (codexExpandedBackdrop != null) codexExpandedBackdrop.SetActive(false);
                for (var index = 0; index < codexCardButtons.Length; index++)
                    if (codexCardButtons[index] != null) codexCardButtons[index].interactable = false;
                return;
            }

            codexModel.Refresh();
            selectedIndex = codexModel.Cards.Count > 0
                ? Mathf.Clamp(selectedIndex, 0, codexModel.Cards.Count - 1)
                : 0;
            for (var index = 0; index < codexCardButtons.Length; index++)
            {
                var button = codexCardButtons[index];
                var label = codexCardLabels[index];
                var portrait = codexCardPortraits[index];
                if (button == null || label == null) continue;
                if (index >= codexModel.Cards.Count)
                {
                    button.interactable = false;
                    label.text = string.Empty;
                    if (portrait != null) portrait.enabled = false;
                    continue;
                }
                var card = codexModel.Cards[index];
                button.interactable = true;
                label.text = card.IsUnlocked
                    ? $"{card.DisplayName}\n{(card.IsBoss ? "보스" : "요괴")} · 처치 {card.KillCount}"
                    : "?\n미확인";
                if (button.targetGraphic is Image image)
                    image.color = codexModel.SelectedCard?.EntryId == card.EntryId
                        ? new Color(.24f, .46f, .68f, 1f)
                        : card.IsUnlocked
                            ? new Color(.16f, .24f, .34f, 1f)
                            : new Color(.08f, .11f, .15f, 1f);
                if (portrait != null)
                {
                    portrait.sprite = FindCodexPortrait(card.EntryId);
                    portrait.enabled = portrait.sprite != null;
                    portrait.color = card.UsesInkSilhouette
                        ? new Color(.025f, .03f, .035f, 1f)
                        : Color.white;
                }
            }

            if (codexModel.Cards.Count == 0)
            {
                titleText.text = "표시할 항목 없음";
                if (codexExpandedBackdrop != null) codexExpandedBackdrop.SetActive(false);
                return;
            }

            var selected = codexModel.SelectedCard;
            if (selected == null)
            {
                if (codexExpandedBackdrop != null) codexExpandedBackdrop.SetActive(false);
                return;
            }

            codexExpandedBackdrop.SetActive(true);
            codexExpandedBackdrop.transform.SetAsLastSibling();
            codexExpandedTitle.text = selected.IsUnlocked ? selected.DisplayName : "?";
            codexExpandedPortrait.sprite = FindCodexPortrait(selected.EntryId);
            codexExpandedPortrait.enabled = codexExpandedPortrait.sprite != null && !codexModel.IsBackVisible;
            codexExpandedPortrait.color = selected.UsesInkSilhouette
                ? new Color(.02f, .025f, .03f, 1f)
                : Color.white;
            if (!selected.IsUnlocked)
            {
                codexExpandedCardBackground.color = new Color(.1f, .11f, .12f, 1f);
                codexExpandedFrontText.gameObject.SetActive(true);
                codexExpandedBackText.gameObject.SetActive(false);
                codexExpandedFrontText.text = "미확인 개체\n처치 후 기록이 공개됩니다.";
                codexExpandedHintText.text = "바깥 클릭 · 격자로 돌아가기";
                return;
            }

            codexExpandedCardBackground.color = codexModel.IsBackVisible
                ? new Color(.16f, .11f, .075f, 1f)
                : new Color(.12f, .16f, .19f, 1f);
            codexExpandedFrontText.gameObject.SetActive(!codexModel.IsBackVisible);
            codexExpandedBackText.gameObject.SetActive(codexModel.IsBackVisible && selected.HasReadableBackText);
            codexExpandedFrontText.text = $"{(selected.IsBoss ? "보스" : "요괴")} · 처치 {selected.KillCount}" +
                                          (selected.FirstKillDay > 0
                                              ? $"\n최초 처치 {selected.FirstKillDay}일"
                                              : string.Empty) +
                                          (string.IsNullOrWhiteSpace(selected.AppearanceHint)
                                              ? string.Empty
                                              : $"\n{selected.AppearanceHint}");
            codexExpandedBackText.text = selected.HasReadableBackText ? selected.SourceText : string.Empty;
            if (!selected.HasReadableBackText)
                codexExpandedHintText.text = "바깥 클릭 · 격자로 돌아가기";
            else
            codexExpandedHintText.text = codexModel.IsBackVisible
                ? "카드 클릭 · 앞면 보기    |    바깥 클릭 · 격자"
                : "카드 클릭 · 전승 보기    |    바깥 클릭 · 격자";
        }

        private void ResolveCharacterArtCatalog()
        {
            var loadedCatalogs = Resources.FindObjectsOfTypeAll<CharacterArtCatalog>();
            for (var index = 0; index < loadedCatalogs.Length; index++)
            {
                if (loadedCatalogs[index] == null) continue;
                characterArtCatalog = loadedCatalogs[index];
                if (loadedCatalogs[index].name == "CharacterArtCatalog") break;
            }
        }

        private Sprite FindCodexPortrait(string entryId) => characterArtCatalog != null
            ? characterArtCatalog.FindSprite(entryId)
            : null;

        private void TryToggleSelectedEquipment()
        {
            var activeSlotItem = CurrentActiveSlotItem();
            if (activeSlotItem != null)
            {
                var activeSlot = runtimeServices.ActiveSlot;
                var succeeded = activeSlot.EquippedItemId == activeSlotItem.Id
                    ? activeSlot.TryUnequip()
                    : activeSlot.TryEquip(activeSlotItem.Id);
                ShowMessage(succeeded
                    ? activeSlot.EquippedItemId == activeSlotItem.Id
                        ? $"무기·도구 슬롯에 {activeSlotItem.DisplayName}을 장착했습니다."
                        : $"{activeSlotItem.DisplayName}을 소지품으로 옮겼습니다."
                    : "무기·도구 슬롯 변경에 실패했습니다. 인벤토리 공간을 확인하세요.");
                return;
            }

            var clawItem = CurrentClawItem();
            if (clawItem != null)
            {
                var claw = runtimeServices.ClawSlot;
                if (claw == null)
                {
                    ShowMessage("발톱 칸을 사용할 수 없습니다.");
                    return;
                }
                var succeeded = claw.EquippedItemId == clawItem.Id
                    ? claw.TryUnequip()
                    : claw.TryEquip(clawItem.Id);
                ShowMessage(succeeded
                    ? claw.EquippedItemId == clawItem.Id
                        ? $"발톱 칸에 {clawItem.DisplayName}을 장착했습니다."
                        : $"{clawItem.DisplayName}을 소지품으로 옮겼습니다."
                    : "발톱 칸 변경에 실패했습니다. 인벤토리 공간을 확인하세요.");
                return;
            }

            var equipment = CurrentEquipment();
            if (equipment == null) return;
            var equippedSlot = FindEquippedSlot(equipment);
            if (equippedSlot.HasValue)
            {
                ShowMessage(runtimeServices.EquipmentSystem.TryUnequip(equippedSlot.Value)
                    ? "장비를 해제했습니다."
                    : "장비 해제에 실패했습니다.");
                return;
            }

            bool equipped;
            if (equipment.IsAccessory)
            {
                var accessoryIndex = runtimeServices.EquipmentSystem.Get(EquipmentSlot.AccessoryOne) == null ? 0 :
                    runtimeServices.EquipmentSystem.Get(EquipmentSlot.AccessoryTwo) == null ? 1 : 0;
                equipped = runtimeServices.EquipmentSystem.TryEquipAccessory(equipment, accessoryIndex);
            }
            else equipped = runtimeServices.EquipmentSystem.TryEquip(equipment);
            ShowMessage(equipped ? "장비를 장착했습니다." : "장비 장착에 실패했습니다.");
        }

        private void RebuildOwnedEquipment()
        {
            runtimeServices?.PromoteInventoryEquipmentItems();
            activeSlotItems.Clear();
            var equippedActiveItemId = runtimeServices.ActiveSlot.EquippedItemId;
            foreach (var item in gameDataCatalog.Items)
            {
                if (item == null || !ActiveSlotSystem.IsAllowedItemId(item.Id)) continue;
                if (item.Id == equippedActiveItemId || runtimeServices.PlayerInventory.Has(item.Id, 1))
                    activeSlotItems.Add(item);
            }
            activeSlotItems.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));

            clawItems.Clear();
            var equippedClawId = runtimeServices.ClawSlot?.EquippedItemId;
            foreach (var item in gameDataCatalog.Items)
            {
                if (item == null || !ClawSlotSystem.IsClawItem(item.Id)) continue;
                if (item.Id == equippedClawId || runtimeServices.PlayerInventory.Has(item.Id, 1))
                    clawItems.Add(item);
            }
            clawItems.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));

            ownedEquipment.Clear();
            foreach (var id in runtimeServices.EquipmentCollection.Export())
            {
                var definition = gameDataCatalog.FindEquipment(id);
                if (definition != null) ownedEquipment.Add(definition);
            }
            ownedEquipment.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
            var count = CurrentEquipmentEntryCount();
            selectedIndex = count > 0 ? Mathf.Clamp(selectedIndex, 0, count - 1) : 0;
        }

        private ItemDefinition CurrentActiveSlotItem()
        {
            return selectedIndex >= 0 && selectedIndex < activeSlotItems.Count
                ? activeSlotItems[selectedIndex]
                : null;
        }

        private ItemDefinition CurrentClawItem()
        {
            var index = selectedIndex - activeSlotItems.Count;
            return index >= 0 && index < clawItems.Count ? clawItems[index] : null;
        }

        private EquipmentDefinition CurrentEquipment()
        {
            var equipmentIndex = selectedIndex - activeSlotItems.Count - clawItems.Count;
            if (ownedEquipment.Count == 0 || equipmentIndex < 0 || equipmentIndex >= ownedEquipment.Count)
                return null;
            return ownedEquipment[equipmentIndex];
        }

        private int CurrentEquipmentEntryCount() =>
            activeSlotItems.Count + clawItems.Count + ownedEquipment.Count;

        private EquipmentSlot? FindEquippedSlot(EquipmentDefinition definition)
        {
            if (definition == null) return null;
            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
                if (runtimeServices.EquipmentSystem.Get(slot) == definition) return slot;
            return null;
        }

        private string BuildEquippedSummary()
        {
            var builder = new StringBuilder($"발톱 채굴 등급: {ResolveClawTier()}\n현재 장착 장비:\n");
            var clawItem = gameDataCatalog.FindItem(runtimeServices.ClawSlot?.EquippedItemId);
            builder.AppendLine($"  · 발톱: {clawItem?.DisplayName ?? "맨 발톱"}");
            var activeItem = gameDataCatalog.FindItem(runtimeServices.ActiveSlot.EquippedItemId);
            builder.AppendLine($"  · 무기·도구: {activeItem?.DisplayName ?? "맨 발톱"}" +
                               (activeItem != null && !runtimeServices.ActiveSlot.IsUsingEquippedItem
                                   ? " (Q: 맨 발톱 활성)"
                                   : string.Empty));
            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
            {
                var definition = runtimeServices.EquipmentSystem.Get(slot);
                var item = definition != null ? gameDataCatalog.FindItem(definition.Id) : null;
                builder.AppendLine($"  · {EquipmentSlotLabel(slot)}: {item?.DisplayName ?? "-"}");
            }
            if (ArmorSetRules.GrantsSunlightImmunity(runtimeServices.EquipmentSystem,
                    runtimeServices.PlayerTemperature?.CurrentRoomTemperature ?? 0,
                    runtimeServices.EquipmentColdPenalty))
                builder.AppendLine("설한풍 세트 활성 · 햇빛 피해 면역");
            return builder.ToString();
        }

        private int ResolveClawTier() =>
            ClawSlotSystem.ResolveMiningTier(runtimeServices?.ClawSlot?.EquippedItemId);

        private int CurrentEntryCount()
        {
            switch (page)
            {
                case Page.Gathering: return runtimeServices?.PlayerInventory?.Slots.Count ?? 0;
                case Page.Crafting: return IsShowingSmeltingList ? smeltingRecipes.Count : filteredRecipes.Count;
                case Page.Equipment:
                    RebuildOwnedEquipment();
                    return CurrentEquipmentEntryCount();
                default: return 0;
            }
        }

        private static string EquipmentSlotLabel(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.Head: return "투구";
                case EquipmentSlot.Body: return "갑옷";
                case EquipmentSlot.Feet: return "신발";
                case EquipmentSlot.AccessoryOne: return "장신구 1";
                case EquipmentSlot.AccessoryTwo: return "장신구 2";
                default: return slot.ToString();
            }
        }

        private void HandleEquipmentAdded(EquipmentDefinition _) => Refresh();

        private bool StationOffersCraftAndSmelt(CraftingStation station)
        {
            if (station != CraftingStation.Furnace && station != CraftingStation.Foundry) return false;
            if (gameDataCatalog == null) return false;
            var smeltKind = station == CraftingStation.Foundry
                ? SmeltingStationKind.Foundry
                : SmeltingStationKind.Furnace;
            var hasSmelt = false;
            for (var index = 0; index < gameDataCatalog.Smelting.Count; index++)
            {
                var definition = gameDataCatalog.Smelting[index];
                if (definition != null && definition.StationKind == smeltKind)
                {
                    hasSmelt = true;
                    break;
                }
            }
            if (!hasSmelt) return false;
            if (visibleRecipes.Count == 0) RebuildVisibleRecipes();
            var smithyUnlocked = runtimeServices?.Seokbinggo?.IsSmithyUnlocked == true;
            for (var index = 0; index < visibleRecipes.Count; index++)
            {
                var recipe = visibleRecipes[index];
                if (recipe == null) continue;
                if (!IsRecipeVisibleAtStation(recipe.Station, station)) continue;
                if (!IsSmithyRecipeAllowed(recipe.Station, smithyUnlocked)) continue;
                if (RecipeUnlockPolicy.IsUnlocked(recipe, runtimeServices.RecipeBook)) return true;
            }
            return false;
        }

        private CraftingStation NearbyStation() => openedStation == CraftingStation.None
            ? CraftingStation.None
            : stationSource != null && stationSource.TryGetCraftingStationIdentity(openedStation,
                openedStationObjectId, out _, out _, requireInRange: false) ? openedStation : CraftingStation.None;

        private StationQueueRecord ResolveProductionQueue()
        {
            if (bindingProductionStation || runtimeServices?.StationProduction == null) return null;
            var position = Vector2.zero;
            var id = StationProductionService.HandStationId;
            if (openedStation != CraftingStation.None)
            {
                if (stationSource == null || !stationSource.TryGetCraftingStationIdentity(openedStation,
                        openedStationObjectId, out id, out position, requireInRange: false)) return null;
                openedStationObjectId = id;
            }
            var queue = runtimeServices.StationProduction.Get(id, openedStation, position);
            var legacySmelting = openedStation == CraftingStation.Foundry ? runtimeServices.Foundry :
                openedStation == CraftingStation.Furnace ? runtimeServices.Furnace : null;
            runtimeServices.StationProduction.AdoptLegacy(queue, runtimeServices.CraftingProcess, legacySmelting);
            return queue;
        }

        private void RefreshProductionQueue()
        {
            if (runtimeServices?.StationProduction == null || panel == null) return;
            var visible = !HasStorageMode && (!open || page == Page.Crafting);
            if (productionQueueRoot == null && visible)
            {
                var canvas = panel.GetComponentInParent<Canvas>()?.rootCanvas;
                if (canvas == null) return;
                productionQueueRoot = CreateUiObject("ProductionQueue", canvas.transform,
                    new Vector2(100f, 18f), Vector2.zero);
                var root = (RectTransform)productionQueueRoot.transform;
                root.anchorMin = root.anchorMax = root.pivot = Vector2.zero;
                var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                texture.SetPixel(0, 0, Color.white);
                texture.filterMode = FilterMode.Point;
                texture.Apply();
                productionQueueTimerSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * .5f);
                productionQueueCollectButton = CreateButton(root, "Collect", "회수",
                    new Vector2(22f, 29f), new Vector2(44f, 14f), CollectDisplayedProduction, false);
                var collectRect = (RectTransform)productionQueueCollectButton.transform;
                collectRect.anchorMin = collectRect.anchorMax = Vector2.zero;
                productionQueueCollectButton.GetComponentInChildren<Text>().fontSize = 7;
                for (var i = 0; i <= StationProductionService.WaitingCapacity; i++)
                {
                    var capturedIndex = i;
                    var button = CreateButton(root, $"Job_{i}", string.Empty,
                        new Vector2(9f + i * 20f, 9f), new Vector2(18f, 18f), () => { }, false);
                    var slotRect = (RectTransform)button.transform;
                    slotRect.anchorMin = slotRect.anchorMax = Vector2.zero;
                    button.GetComponent<Image>().color = new Color(.08f, .12f, .18f, 1f);
                    RefreshSlotSelectionBorder(button.GetComponent<Image>(), true);
                    var icon = CreateArtImage(button.transform, "Output", null,
                        Vector2.zero, new Vector2(14f, 14f));
                    var timer = CreateArtImage(button.transform, "Timer", productionQueueTimerSprite,
                        Vector2.zero, new Vector2(16f, 16f));
                    timer.type = Image.Type.Filled;
                    timer.fillMethod = Image.FillMethod.Radial360;
                    timer.fillOrigin = (int)Image.Origin360.Top;
                    timer.fillClockwise = true;
                    timer.color = new Color(.15f, .65f, 1f, .55f);
                    WireInventoryPointer(button, pointer =>
                    {
                        if (pointer.button != PointerEventData.InputButton.Right ||
                            shell != null && shell.Screen != GameShellScreen.Gameplay) return;
                        var queue = ResolveProductionQueue();
                        var job = displayedProductionJobs[capturedIndex];
                        var index = queue != null && job != null ? queue.jobs.IndexOf(job) : -1;
                        if (runtimeServices.StationProduction.Cancel(queue, index))
                            ShowMessage(queue.returns.Count > 0
                                ? "제작 취소 · 반환 대기분은 회수 버튼으로 받으세요."
                                : "제작 취소 · 재료를 반환했습니다.");
                        Refresh();
                    });
                    productionQueueSlots.Add(button);
                    productionQueueIcons.Add(icon);
                    productionQueueTimers.Add(timer);
                }
            }
            if (productionQueueRoot == null) return;
            productionQueueRoot.SetActive(visible);
            if (!visible) return;
            var queueRect = (RectTransform)productionQueueRoot.transform;
            var craftRect = craftWindowRoot != null ? (RectTransform)craftWindowRoot.transform : null;
            if (craftRect != null && queueRect.parent is RectTransform canvasRect)
            {
                var rightEdge = canvasRect.InverseTransformPoint(craftRect.TransformPoint(
                    new Vector3(craftRect.rect.xMax, craftRect.rect.yMin, 0f)));
                var thirdRowCenter = rightEdge.y + 9f;
                if (craftWindowScrollRect != null && craftWindowSlotButtons.Length > 0)
                {
                    var viewport = craftWindowScrollRect.viewport;
                    var rowHeight = ((RectTransform)craftWindowSlotButtons[0].transform).rect.height;
                    var center = canvasRect.InverseTransformPoint(viewport.TransformPoint(
                        new Vector3(viewport.rect.center.x, viewport.rect.yMax - rowHeight * 2.5f, 0f)));
                    thirdRowCenter = center.y;
                }
                // Slots are anchored to the queue's bottom-left, not its centre.
                queueRect.anchoredPosition = new Vector2(rightEdge.x - canvasRect.rect.xMin + 4f,
                    thirdRowCenter - canvasRect.rect.yMin - 9f);
            }
            else queueRect.anchoredPosition = new Vector2(116f, 4f);
            var state = ResolveProductionQueue();
            var hasReturns = state?.returns.Count > 0 ||
                IsShowingSmeltingList && ResolveDisplayedSmeltingStation()?.Completed.Count > 0;
            productionQueueCollectButton.gameObject.SetActive(hasReturns);
            productionQueueCollectButton.interactable = hasReturns;
            for (var i = 0; i < productionQueueSlots.Count; i++)
            {
                var job = state != null && i < state.jobs.Count ? state.jobs[i] : null;
                displayedProductionJobs[i] = job;
                productionQueueSlots[i].gameObject.SetActive(job != null);
                if (job == null) continue;
                var output = job.smelting ? gameDataCatalog.FindSmelting(job.recipeId)?.Output.item
                    : gameDataCatalog.FindRecipe(job.recipeId)?.Output.item;
                productionQueueIcons[i].sprite = output != null ? itemArtCatalog?.FindSprite(output.Id) : null;
                productionQueueIcons[i].enabled = productionQueueIcons[i].sprite != null;
                productionQueueTimers[i].enabled = i == 0;
                productionQueueTimers[i].fillAmount = job.duration > 0f
                    ? Mathf.Clamp01(1f - job.remaining / job.duration) : 0f;
            }
        }

        private void CollectDisplayedProduction()
        {
            if (shell != null && shell.Screen != GameShellScreen.Gameplay) return;
            if (IsShowingSmeltingList) { TryCollectOutputs(); return; }
            var count = runtimeServices.StationProduction.Collect(ResolveProductionQueue());
            ShowMessage(count > 0 ? "완료품·반환 재료를 회수했습니다." : "인벤토리 공간이 부족합니다.");
            Refresh();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void GrantSelectedRequirementsForEditorTest()
        {
            if (!(open && page == Page.Crafting) && !(open && page == Page.Equipment))
            {
                if (IsShowingSmeltingList)
                {
                    var windowSmelt = CurrentSmelting();
                    var windowNeeds = windowSmelt == null ? null : new[] { windowSmelt.Input, windowSmelt.Fuel };
                    if (windowNeeds != null && TryGrantItems(windowNeeds))
                        ShowMessage($"F12 테스트 재료·연료 지급: {windowSmelt.Output.item.DisplayName}");
                    else ShowMessage("F12 제련 재료 지급 실패: 인벤토리 공간을 확인하세요.");
                }
                else
                {
                    var windowRecipe = craftWindowIndex >= 0 && craftWindowIndex < craftWindowRecipes.Count
                        ? craftWindowRecipes[craftWindowIndex] : null;
                    if (windowRecipe != null && TryGrantItems(windowRecipe.Ingredients))
                        ShowMessage($"F12 테스트 재료 지급: {windowRecipe.Output.item.DisplayName}");
                    else ShowMessage("F12 재료 지급 실패: 인벤토리 공간을 확인하세요.");
                }
                Debug.Log("[Nyangbingo] " + message);
                return;
            }
            if (page == Page.Crafting && !IsShowingSmeltingList)
            {
                var recipe = CurrentRecipe();
                if (recipe != null && TryGrantItems(recipe.Ingredients))
                    ShowMessage($"F12 테스트 재료 지급: {recipe.Output.item.DisplayName}");
                else ShowMessage("F12 재료 지급 실패: 인벤토리 공간을 확인하세요.");
                Debug.Log("[Nyangbingo] " + message);
                return;
            }
            if (page == Page.Crafting && IsShowingSmeltingList)
            {
                var definition = CurrentSmelting();
                var requirements = definition == null ? null : new[] { definition.Input, definition.Fuel };
                if (requirements != null && TryGrantItems(requirements))
                    ShowMessage($"F12 테스트 재료·연료 지급: {definition.Output.item.DisplayName}");
                else ShowMessage("F12 제련 재료 지급 실패: 인벤토리 공간을 확인하세요.");
                Debug.Log("[Nyangbingo] " + message);
                return;
            }
            if (page == Page.Equipment)
            {
                var activeCandidate = gameDataCatalog.Items
                    .Where(item => item != null && ActiveSlotSystem.IsAllowedItemId(item.Id) &&
                                   item.Id != runtimeServices.ActiveSlot.EquippedItemId &&
                                   !runtimeServices.PlayerInventory.Has(item.Id, 1))
                    .OrderBy(item => item.Id, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (activeCandidate != null && runtimeServices.PlayerInventory.TryAdd(activeCandidate.Id, 1))
                {
                    ShowMessage($"F12 테스트 무기·도구 지급: {activeCandidate.DisplayName}");
                    Debug.Log("[Nyangbingo] " + message);
                    return;
                }

                var candidate = gameDataCatalog.Equipment
                    .Where(definition => definition != null &&
                                         !runtimeServices.EquipmentCollection.Contains(definition.Id))
                    .OrderBy(definition => definition.Id, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (candidate != null && runtimeServices.EquipmentCollection.TryAdd(candidate))
                    ShowMessage($"F12 테스트 장비 지급: {gameDataCatalog.ItemDisplayName(candidate.Id, "장비")}");
                else ShowMessage("F12 지급 가능한 새 장비가 없습니다.");
                Debug.Log("[Nyangbingo] " + message);
            }
        }

        private bool TryGrantItems(IEnumerable<ItemAmount> requirements)
        {
            if (requirements == null) return false;
            var granted = new List<ItemAmount>();
            foreach (var requirement in requirements)
            {
                if (requirement.item != null && requirement.amount > 0 &&
                    runtimeServices.PlayerInventory.TryAdd(requirement.item.Id, requirement.amount))
                {
                    granted.Add(requirement);
                    continue;
                }
                for (var index = granted.Count - 1; index >= 0; index--)
                    runtimeServices.PlayerInventory.TryRemove(granted[index].item.Id, granted[index].amount);
                return false;
            }
            return true;
        }

        private void TeleportToRequiredStationForEditorTest()
        {
            var station = CraftingStation.None;
            if (page == Page.Crafting && !IsShowingSmeltingList)
                station = CurrentRecipe()?.Station ?? CraftingStation.None;
            else if (page == Page.Crafting && IsShowingSmeltingList)
            {
                var definition = CurrentSmelting();
                if (definition != null)
                    station = definition.StationKind == SmeltingStationKind.Foundry
                        ? CraftingStation.Foundry
                        : CraftingStation.Furnace;
            }
            if (station == CraftingStation.None)
            { ShowMessage("선택 항목은 제작대 이동이 필요하지 않습니다."); return; }
            ShowMessage(stationSource != null && stationSource.TeleportToCraftingStationForEditorTest(station)
                ? $"Shift+F12 테스트 이동: {StationLabel(station)}"
                : "테스트 제작대 위치를 찾지 못했습니다.");
        }
#endif

        private void SubmitNavigation(int direction)
        {
            var button = direction < 0 ? previousButton : nextButton;
            if (button == null || !button.gameObject.activeInHierarchy)
            {
                // Inventory grid navigation has no previous/next buttons.
                SelectRelative(direction);
                return;
            }
            if (!button.IsInteractable()) return;
            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (eventSystem == null)
            {
                button.onClick.Invoke();
                return;
            }
            // Button's submit path invokes the existing click handler once, then plays
            // its Pressed transition and restores the state using unscaled time.
            button.OnSubmit(new UnityEngine.EventSystems.BaseEventData(eventSystem));
        }

        private string DefaultHelpText()
        {
            if (page == Page.Equipment)
                return "장비를 가방에서 맞는 칸으로 드래그해 장착 · E 장착/해제 · ESC 닫기";
            if (page == Page.Crafting)
                return IsSmeltingStation(openedStation)
                    ? furnaceSmeltingView
                        ? "전환 버튼 클릭: 제작 목록 · 클릭 선택·제련 · ESC 닫기"
                        : "전환 버튼 클릭: 제련 목록 · 클릭 선택·제작 · ESC 닫기"
                    : "C 제작 탭 · ESC 닫기 · 클릭 선택·제작";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return page == Page.Gathering
                ? "좌클릭 전체 · 우클릭 절반 집기/1개 놓기 · E 사용/설치 · ESC 닫기"
                : "C 제작 · ESC 닫기 · E 실행";
#else
            return page == Page.Gathering
                ? "좌클릭 전체 · 우클릭 절반 집기/1개 놓기 · E 사용/설치 · ESC 닫기"
                : "C 제작 · ESC 닫기 · E 실행";
#endif
        }

        private void RefreshTabButtons()
        {
            for (var index = 0; index < tabButtons.Length; index++)
            {
                var image = tabButtons[index]?.targetGraphic as Image;
                if (image == null) continue;
                var selected = index == (int)page;
                var blue = gameplayArtCatalog?.Button1x6BlueFrames;
                var normal = gameplayArtCatalog?.Button1x6Frames;
                if (blue != null && blue.Count > 0 && blue[0] != null &&
                    normal != null && normal.Count > 0 && normal[0] != null)
                {
                    image.sprite = selected ? blue[0] : normal[0];
                    image.color = Color.white;
                }
                else
                    image.color = selected
                        ? new Color(.22f, .42f, .62f, 1f)
                        : new Color(.16f, .24f, .34f, 1f);
            }
        }

        private void ShowMessage(string value)
        {
            message = value;
            messageUntil = Time.unscaledTime + 3f;
            Refresh();
        }

        private static string StationLabel(CraftingStation station)
        {
            switch (station)
            {
                case CraftingStation.None: return "손 제작";
                case CraftingStation.Workbench: return "작업대";
                case CraftingStation.Furnace: return "화로";
                case CraftingStation.IceAnvil: return "얼음 모루";
                case CraftingStation.Foundry: return "용광로";
                case CraftingStation.Smithy: return "대장간";
                default: return station.ToString();
            }
        }

        private static GameObject CreateUiObject(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var result = new GameObject(name, typeof(RectTransform));
            result.transform.SetParent(parent, false);
            var rect = (RectTransform)result.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return result;
        }

        private static Text CreateText(Transform parent, string name, int fontSize, TextAnchor anchor,
            Vector2 size, Vector2 position)
        {
            var result = CreateUiObject(name, parent, size, position).AddComponent<Text>();
            result.font = UiTextFont.Source;
            result.fontSize = fontSize;
            result.alignment = anchor;
            result.color = new Color(.93f, .96f, 1f);
            return result;
        }

        private static Image CreateArtImage(Transform parent, string name, Sprite sprite,
            Vector2 position, Vector2 size)
        {
            var image = CreateUiObject(name, parent, size, position).AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.enabled = sprite != null;
            return image;
        }

        private Button CreateButton(Transform parent, string name, string label, Vector2 position,
            Vector2 size, UnityEngine.Events.UnityAction action, bool applyDeliveredArt = true)
        {
            var buttonObject = CreateUiObject(name, parent, size, position);
            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(.16f, .24f, .34f, 1f);
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            if (applyDeliveredArt) RuntimeUiButtonArt.Apply(button, gameplayArtCatalog);
            var text = CreateText(buttonObject.transform, "Label", 10, TextAnchor.MiddleCenter, size, Vector2.zero);
            text.text = label;
            text.raycastTarget = false;
            return button;
        }

        private void OnDisable()
        {
            HideDescription();
            CancelEquipmentDrag();
            ReturnHeldInventoryItem();
            HideInventoryCursorPreview();
            if (cursorOwner == this) cursorOwner = null;
        }

        private void OnDestroy()
        {
            if (descriptionRoot != null) Destroy(descriptionRoot);
            if (productionQueueTimerSprite != null)
            {
                Destroy(productionQueueTimerSprite.texture);
                Destroy(productionQueueTimerSprite);
            }
            HideInventoryCursorPreview();
            if (cursorOwner == this) cursorOwner = null;
            if (inventoryCursorIcon != null) Destroy(inventoryCursorIcon.gameObject);
            if (open)
            {
                open = false;
                openControllerCount = Mathf.Max(0, openControllerCount - 1);
            }
            if (runtimeServices?.PlayerInventory != null)
                runtimeServices.PlayerInventory.Changed -= Refresh;
            if (runtimeServices?.EquipmentSystem != null)
                runtimeServices.EquipmentSystem.Changed -= Refresh;
            if (runtimeServices?.EquipmentCollection != null)
                runtimeServices.EquipmentCollection.Added -= HandleEquipmentAdded;
            if (runtimeServices?.ActiveSlot != null)
                runtimeServices.ActiveSlot.Changed -= Refresh;
            if (runtimeServices?.ClawSlot != null)
                runtimeServices.ClawSlot.Changed -= Refresh;
            if (runtimeServices?.PortableLantern != null)
                runtimeServices.PortableLantern.Changed -= Refresh;
            if (runtimeServices?.JangdokStorage != null)
                runtimeServices.JangdokStorage.Changed -= Refresh;
            if (runtimeServices?.RecipeBook != null)
                runtimeServices.RecipeBook.Changed -= Refresh;
            GameEvents.OnDayStart -= HandleDayStart;
            if (turretRuntime != null) turretRuntime.BuildStateChanged -= Refresh;
        }
    }
}
