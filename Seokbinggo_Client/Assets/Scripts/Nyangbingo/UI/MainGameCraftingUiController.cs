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
    public sealed class MainGameCraftingUiController : MonoBehaviour
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
        private readonly List<EquipmentDefinition> ownedEquipment = new List<EquipmentDefinition>();
        [SerializeField] private GameObject panel;
        [SerializeField] private Text titleText;
        [SerializeField] private Text detailsText;
        private readonly List<Button> materialSourceButtons = new List<Button>();
        private readonly List<ItemDefinition> missingSourceItems = new List<ItemDefinition>();
        private string sourceRecipeId;
        private string sourceItemId;
        private string sourceDescription;
        [SerializeField] private Text messageText;
        [SerializeField] private ScrollRect detailsScrollRect;
        [SerializeField] private RectTransform detailsViewportRect;
        [SerializeField] private RectTransform detailsScrollbarRect;
        [SerializeField] private GameObject inventoryGridRoot;
        private GameObject craftingListRoot;
        private ScrollRect craftingListScrollRect;
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
        private UnityEngine.UI.Image inventoryCursorIcon;
        private UnityEngine.UI.Text inventoryCursorCount;
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
        private Text productionQueueHeader;
        private readonly List<Text> productionQueueLabels = new List<Text>();
        private readonly List<Button> productionQueueCancelButtons = new List<Button>();
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
        private int openedFrame = -1;

        public static bool BlocksGameplayInput => openControllerCount > 0 ||
            (cursorOwner != null && cursorOwner.HasHeldInventoryItem);
        public static bool BlocksPlayerMovement => openControllerCount > 0;
        public static bool ShowsInventoryPointer => cursorOwner != null &&
            (cursorOwner.HasHeldInventoryItem || cursorOwner.open &&
                (cursorOwner.page == Page.Gathering || cursorOwner.HasStorageMode));
        public static bool ConsumedEscapeThisFrame => escapeConsumedFrame == Time.frameCount;
        public bool IsOpen => open;

        /// <summary>인스펙터에 수동 배선되는 납품 제작·제련 패널 하이어라키가 살아 있는지.</summary>
        public bool HasDeliveredPanelBindings =>
            panel != null && titleText != null && tabButtons != null && tabButtons.Length == 4 &&
            tabButtons.All(button => button != null);
        public int VisibleRecipeCount => visibleRecipes.Count;
        public const int UnifiedTabCount = 4;
        public const int InventoryGridColumns = 10;
        public const int InventoryGridRows = 5;
        public const int InventoryHotbarSlotCount = 8;
        public const float InventorySlotPixelSize = 27f;
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
                case 2: return KeyCode.G;
                case 3: return KeyCode.J;
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
            if (!initialized) return;

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
            if (!open) return;
            if (page != Page.Codex)
            {
                if (page == Page.Crafting && !IsShowingSmeltingList)
                {
                    if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) SelectRelative(-1);
                    if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) SelectRelative(1);
                }
                else
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
                if (page == Page.Crafting && IsSmeltingStation(openedStation) &&
                    Input.GetKeyDown(KeyCode.Q))
                    TryToggleFurnaceCraftingSmeltingView();
                if (page == Page.Equipment && Input.GetKeyDown(KeyCode.Q)) ToggleActiveSlotFromEquipmentPage();
                if (openedFrame != Time.frameCount &&
                    Input.GetKeyDown(KeyCode.E))
                    TryPrimaryAction();
                if (page == Page.Equipment && Input.GetKeyDown(KeyCode.R)) TryRefuelPortableLantern();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if ((page == Page.Crafting || page == Page.Equipment) &&
                    DevelopmentShortcuts.IsPressed(DevelopmentShortcut.RecipeMaterials))
                    GrantSelectedRequirementsForEditorTest();
                if (page == Page.Crafting &&
                    DevelopmentShortcuts.IsPressed(DevelopmentShortcut.RecipeStation))
                    TeleportToRequiredStationForEditorTest();
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

            bindingProductionStation = true;
            OpenPage(Page.Crafting);
            bindingProductionStation = false;
            openedStation = station;
            openedStationObjectId = objectId;
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
                if (label != null) label.text = $"{UnifiedTabHotkey(index)} · {UnifiedTabLabel(index)}";
            }
            BuildDetailsScrollArea();
            BuildCraftingList();
            BuildInventoryGrid();
            BuildEquipmentVisual();
            BuildStorageUi();
            BuildCodexGrid();

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
            detailsScrollRect.verticalScrollbar = scrollbar;
            detailsScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        }

        private void BuildCraftingList()
        {
            craftingListRoot = CreateUiObject("CraftingRecipeViewport", panel.transform,
                new Vector2(178f, 134f), new Vector2(-130f, 11f));
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
                    selectedIndex = capturedIndex;
                    ClickInventorySlot(runtimeServices.PlayerInventory, capturedIndex, pointer);
                });
                var oldNumberHint = inventoryGridButtons[index].transform.Find("HotbarShortcut");
                if (oldNumberHint != null) oldNumberHint.gameObject.SetActive(false);
            }
            inventoryGridRoot.SetActive(false);
        }

        private void BuildEquipmentVisual()
        {
            if (equipmentVisualRoot == null || equipmentSlotButtons == null || equipmentSlotButtons.Length != 6)
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
            storageHintText.text = "좌클릭: 전체 집기/놓기 · 우클릭: 절반 집기/1개 놓기 · Shift+클릭: 전체/절반 즉시 이동 | E/ESC: 닫기";
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
            ClickInventorySlot(runtimeServices.PlayerInventory, index, pointer);
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
            if (source == runtimeServices.PlayerInventory &&
                runtimeServices.IsEquippedItem(source.Slots[index].itemId))
            {
                ShowMessage("장착 중인 장비는 옮길 수 없습니다. 먼저 장착을 해제해주세요.");
                return true;
            }
            message = source.TryTransferSlotTo(index, target, count)
                ? string.Empty : "이동할 보관 공간이 부족합니다.";
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
                var builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
            // Number keys belong to the hotbar. Modified keys are reserved for debug tools.
            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt) ||
                Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) return false;
            if (Input.GetKeyDown(KeyCode.I))
            {
                if ((shell == null || shell.Screen == GameShellScreen.Gameplay) && Time.timeScale > 0f)
                    TogglePage(Page.Gathering);
                return true;
            }
            var targetIndex = -1;
            for (var index = 0; index < UnifiedTabCount; index++)
            {
                if (!Input.GetKeyDown(UnifiedTabHotkey(index))) continue;
                targetIndex = index;
                break;
            }
            if (targetIndex < 0) return false;
            var target = (Page)targetIndex;

            if ((shell != null && shell.Screen != GameShellScreen.Gameplay) || Time.timeScale <= 0f) return true;
            TogglePage(target);
            return true;
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
                openedStation = stationSource != null ? stationSource.NearbyCraftingStation : CraftingStation.None;
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
            if (craftingListScrollRect == null) return;
            Canvas.ForceUpdateCanvases();
            craftingListScrollRect.StopMovement();
            craftingListScrollRect.verticalNormalizedPosition = 1f;
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
            selectedIndex = (selectedIndex + delta + count) % count;
            message = string.Empty;
            Refresh();
            ResetDetailsScroll();
            EnsureSelectedRecipeVisible();
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
            var heldBefore = runtimeServices.InventoryCursor.Slots[0];
            var clickedBefore = inventory.Slots[index];
            if (inventory != runtimeServices.PlayerInventory && heldBefore.amount > 0 &&
                runtimeServices.IsEquippedItem(heldBefore.itemId))
            {
                ShowMessage("장착 중인 장비는 옮길 수 없습니다. 먼저 장착을 해제해주세요.");
                return;
            }
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
            Refresh();
        }

        private void LateUpdate()
        {
            if (!initialized || runtimeServices?.InventoryCursor == null) return;
            cursorOwner = this;
            var visible = HasHeldInventoryItem &&
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
                    Vector2.zero, new Vector2(InventorySlotPixelSize, InventorySlotPixelSize));
                inventoryCursorIcon.raycastTarget = false;
                inventoryCursorCount = CreateText(inventoryCursorIcon.transform, "Count", 7,
                    TextAnchor.LowerRight, new Vector2(InventorySlotPixelSize, InventorySlotPixelSize), Vector2.zero);
                inventoryCursorCount.raycastTarget = false;
            }
            inventoryCursorIcon.gameObject.SetActive(true);
            inventoryCursorIcon.transform.SetAsLastSibling();
            var slot = runtimeServices.InventoryCursor.Slots[0];
            var item = gameDataCatalog.FindItem(slot.itemId);
            inventoryCursorIcon.sprite = itemArtCatalog?.FindSprite(slot.itemId);
            inventoryCursorIcon.enabled = inventoryCursorIcon.sprite != null;
            inventoryCursorIcon.color = inventoryCursorIcon.sprite != null ? Color.white : Color.clear;
            inventoryCursorCount.text = inventoryCursorIcon.sprite != null
                ? (slot.amount > 1 ? slot.amount.ToString() : string.Empty)
                : $"{item?.DisplayName} ×{slot.amount}";
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas.transform as RectTransform,
                    Input.mousePosition, camera, out var point))
                inventoryCursorIcon.rectTransform.localPosition = new Vector3(point.x, point.y, 0f);
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
                FindAnyObjectByType<MainGamePlayerController>()?.TryUseIceShardFromInventory(selectedIndex);
                Refresh();
                return;
            }
            if (PlayerHealthRecoveryService.IsSupportedHealingItemId(item.Id))
            {
                var recovery = runtimeServices?.PlayerHealthRecovery;
                if (recovery != null && recovery.TryUseHealingItem(item.Id, out var restoredHealth, selectedIndex))
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
                    runtimeServices.Talismans.TryUse(item.Id, out talismanMessage, selectedIndex))
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
            if (tilePalette != null && tilePalette.TryBeginPlacement(item.Id, selectedIndex))
                SetOpen(false);
            else if (!MainGameTilePaletteController.SupportsPalettePlacement(item.Id) &&
                     turretRuntime != null &&
                     turretRuntime.BeginPlacementPreview(item.Id, selectedIndex))
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
                if (panel != null) panel.SetActive(value);
                return;
            }
            open = value;
            if (!open)
            {
                storageObjectId = string.Empty;
                chestProgress = null;
                chestId = string.Empty;
                CancelSummonConfirmation();
            }
            if (!open && codexModel != null) codexModel.TapOutside();
            if (!open && codexExpandedBackdrop != null) codexExpandedBackdrop.SetActive(false);
            openControllerCount = Mathf.Max(0, openControllerCount + (open ? 1 : -1));
            if (panel != null) panel.SetActive(open);
            message = string.Empty;
            Refresh();
        }

        private void Refresh()
        {
            if (panel == null || !open || runtimeServices == null || !runtimeServices.IsInitialized) return;
            RefreshPageLayout();
            RefreshProductionQueue();
            if (HasStorageMode)
            {
                RefreshStorage();
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
        }

        private void RefreshPageLayout()
        {
            foreach (var button in materialSourceButtons) button.gameObject.SetActive(false);
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
                if (inventoryGridRoot != null) inventoryGridRoot.SetActive(false);
                if (equipmentVisualRoot != null) equipmentVisualRoot.SetActive(false);
                if (craftingListRoot != null) craftingListRoot.SetActive(false);
                if (codexGridRoot != null) codexGridRoot.SetActive(false);
                if (detailsViewportRect != null) detailsViewportRect.gameObject.SetActive(false);
                if (detailsScrollbarRect != null) detailsScrollbarRect.gameObject.SetActive(false);
                return;
            }
            foreach (var tab in tabButtons) if (tab != null) tab.gameObject.SetActive(true);
            var gathering = page == Page.Gathering;
            var equipment = page == Page.Equipment;
            var codex = page == Page.Codex;
            var recipeList = page == Page.Crafting && !IsShowingSmeltingList;
            var hasListActions = !gathering && !codex;
            previousButton.gameObject.SetActive(hasListActions && !recipeList);
            nextButton.gameObject.SetActive(hasListActions && !recipeList);
            primaryButton.gameObject.SetActive(!codex);
            var primaryRect = primaryButton.GetComponent<RectTransform>();
            primaryRect.anchoredPosition = new Vector2(110f, -91f);
            primaryRect.sizeDelta = new Vector2(240f, 22f);
            var collectRect = collectButton.GetComponent<RectTransform>();
            collectRect.anchoredPosition = new Vector2(-52f, -118f);
            collectRect.sizeDelta = new Vector2(130f, 20f);
            messageText.gameObject.SetActive(hasListActions);
            collectButton.gameObject.SetActive(false);
            debugCompleteButton.gameObject.SetActive(SupportsDebugInstantCompletion && page == Page.Crafting);
            if (inventoryGridRoot != null) inventoryGridRoot.SetActive(gathering);
            if (inventoryHintText != null) inventoryHintText.gameObject.SetActive(false);
            if (equipmentVisualRoot != null) equipmentVisualRoot.SetActive(equipment);
            if (craftingListRoot != null) craftingListRoot.SetActive(recipeList);
            if (codexGridRoot != null) codexGridRoot.SetActive(codex);
            if (detailsViewportRect != null) detailsViewportRect.gameObject.SetActive(!gathering && !codex);
            if (detailsScrollbarRect != null) detailsScrollbarRect.gameObject.SetActive(!gathering && !codex);

            if (!gathering && !codex)
            {
                detailsViewportRect.sizeDelta = recipeList
                    ? new Vector2(250f, 134f)
                    : equipment ? new Vector2(286f, 134f) : new Vector2(438f, 134f);
                detailsViewportRect.anchoredPosition = recipeList
                    ? new Vector2(89f, 11f)
                    : equipment ? new Vector2(73f, 11f) : new Vector2(-5f, 11f);
                detailsScrollbarRect.sizeDelta = new Vector2(6f, 134f);
                detailsScrollbarRect.anchoredPosition = new Vector2(222f, 11f);
                if (page == Page.Crafting)
                {
                    // Reserve a row for missing material selectors; retain the existing scroll content.
                    detailsViewportRect.sizeDelta = new Vector2(detailsViewportRect.sizeDelta.x, 44f);
                    detailsViewportRect.anchoredPosition += new Vector2(0f, 19f);
                    detailsScrollbarRect.sizeDelta = new Vector2(6f, 44f);
                    detailsScrollbarRect.anchoredPosition += new Vector2(0f, 19f);
                }
            }
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
            primaryButton.GetComponentInChildren<Text>().text = "E · 제작 예약";
            var primaryRect = primaryButton.GetComponent<RectTransform>();
            primaryRect.anchoredPosition = new Vector2(65f, -91f);
            primaryRect.sizeDelta = new Vector2(150f, 22f);
            collectButton.gameObject.SetActive(readyToPlace);
            if (readyToPlace)
            {
                var placementRect = collectButton.GetComponent<RectTransform>();
                placementRect.anchoredPosition = new Vector2(-100f, -91f);
                placementRect.sizeDelta = new Vector2(150f, 22f);
                collectButton.GetComponentInChildren<Text>().text = "설치";
                collectButton.interactable = true;
            }
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
                                   (readyToPlace ? "설치 버튼으로 배치 가능" : "제작 완료 후 설치 가능"));
            if (runtimeServices.CraftingProcess.IsCrafting)
                builder.AppendLine($"\n진행 중: {runtimeServices.CraftingProcess.Active.Output.item.DisplayName} " +
                                   $"{runtimeServices.CraftingProcess.RemainingSeconds:0.0}초");
            if (recipe.Id == "workbench")
                builder.AppendLine(readyToPlace
                    ? "\n다음: 설치 버튼 → 초록 위치에 우클릭"
                    : "\n흙·돌 블록에 좌클릭 유지로 채굴\n떨어진 재료 가까이 이동해 줍기");
            detailsText.text = builder.ToString();
            RefreshMaterialSources(recipe.Id, recipe.Ingredients);
        }

        private void RefreshStorage()
        {
            if (storageTemperatureTrack != null) storageTemperatureTrack.gameObject.SetActive(false);
            Nyangbingo.Inventory.Inventory storage;
            if (chestProgress != null)
            {
                titleText.text = "자연 상자 · 좌클릭 전체 · 우클릭 절반/1개";
                if (storageLabelText != null)
                    storageLabelText.text = $"상자 내용물 · {ChestProgress.StorageSlotCount}슬롯";
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
                        if (!string.IsNullOrEmpty(message)) storageHintText.text = message;
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
                label.text = count + warning + condition;
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
            primaryButton.GetComponentInChildren<Text>().text = "E · 제련 예약";
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
            RefreshMaterialSources(definition.Id, new[] { definition.Input, definition.Fuel });
        }

        private void RefreshMaterialSources(string recipeId, IEnumerable<ItemAmount> ingredients)
        {
            var missing = ingredients.Where(i => i.item != null && i.amount > 0)
                .GroupBy(i => i.item.Id).Where(g => runtimeServices.PlayerInventory.Count(g.Key) <
                    g.Sum(i => i.amount)).Select(g => g.First().item).ToArray();
            if (sourceRecipeId != recipeId || !missing.Any(item => item.Id == sourceItemId))
            {
                sourceRecipeId = recipeId;
                sourceItemId = null;
                sourceDescription = null;
            }
            missingSourceItems.Clear();
            missingSourceItems.AddRange(missing);
            var width = IsShowingSmeltingList ? 438f : 250f;
            var center = IsShowingSmeltingList ? -5f : 89f;
            for (var index = 0; index < missing.Length; index++)
            {
                if (index >= materialSourceButtons.Count)
                {
                    var capturedIndex = index;
                    materialSourceButtons.Add(CreateButton(panel.transform, $"MissingMaterialSource_{index}",
                        string.Empty, Vector2.zero, Vector2.zero, () => SelectMaterialSource(capturedIndex), false));
                }
                var button = materialSourceButtons[index];
                button.gameObject.SetActive(true);
                var rect = (RectTransform)button.transform;
                var slotWidth = width / missing.Length;
                rect.sizeDelta = new Vector2(slotWidth - 3f, 22f);
                rect.anchoredPosition = new Vector2(center - width / 2f + slotWidth * (index + .5f), 67f);
                var text = button.GetComponentInChildren<Text>();
                text.fontSize = 8;
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = 6;
                text.resizeTextMaxSize = 8;
                ((RectTransform)text.transform).sizeDelta = rect.sizeDelta;
                text.text = missing[index].DisplayName;
                var itemId = missing[index].Id;
                if (button.targetGraphic is Image image)
                    image.color = sourceItemId == itemId ? new Color(.24f, .46f, .68f, 1f) :
                        new Color(.32f, .2f, .2f, 1f);
            }
            if (string.IsNullOrEmpty(sourceItemId))
            {
                if (missing.Length > 0) detailsText.text += "\n\n상단의 부족한 재료를 누르면 획득처를 볼 수 있어요.";
                return;
            }
            detailsText.text = $"{gameDataCatalog.FindItem(sourceItemId)?.DisplayName} · 획득처\n\n" +
                               sourceDescription +
                               "\n\n같은 재료를 다시 누르면 제작 설명으로 돌아갑니다.";
        }

        private void SelectMaterialSource(int index)
        {
            if (!open || page != Page.Crafting || HasStorageMode ||
                index < 0 || index >= missingSourceItems.Count) return;
            var id = missingSourceItems[index].Id;
            sourceItemId = sourceItemId == id ? null : id;
            // Imported definitions stay fixed during play; resolve on selection, not every HUD frame.
            sourceDescription = sourceItemId == null ? null : MaterialSourceGuide.Describe(gameDataCatalog, sourceItemId);
            Refresh();
            ResetDetailsScroll();
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
            selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, inventory.Slots.Count - 1));
            var selectedItem = CurrentInventoryItem();
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
                : $"{selectedItem.DisplayName} ×{inventory.Slots[selectedIndex].amount}";
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
            primaryButton.interactable = canHeal || canCool || talismanItem || canSummon || canPlace;
            if (inventoryHintText != null)
            {
                inventoryHintText.gameObject.SetActive(true);
                inventoryHintText.text = isCatnip
                    ? "사용 즉시 HP를 25 회복합니다."
                    : canSummon
                    ? $"사용 시 {summonBoss.DisplayName} 소환 확인창이 열립니다."
                    : summonBoss != null
                        ? summonReason
                        : string.Empty;
            }

            for (var index = 0; index < inventoryGridLabels.Length; index++)
            {
                var label = inventoryGridLabels[index];
                var buttonImage = inventoryGridButtons[index]?.targetGraphic as Image;
                var icon = inventoryGridIcons[index];
                if (label == null) continue;
                if (buttonImage != null)
                {
                    buttonImage.sprite = index == selectedIndex
                        ? gameplayArtCatalog?.InventorySlotSelected
                        : gameplayArtCatalog?.InventorySlot;
                    buttonImage.color = buttonImage.sprite != null
                        ? Color.white
                        : index == selectedIndex
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

        private string FormatInventorySlotCount(ItemDefinition item, int amount)
        {
            if (item == null || amount <= 0) return string.Empty;
            if (BowCombatRules.IsBowWeaponId(item.Id))
                return (runtimeServices?.PlayerInventory?.Count(BowCombatRules.AmmoItemId) ?? 0).ToString();
            if (item.Category == ItemCategory.Weapon ||
                item.Category == ItemCategory.Tool && gameDataCatalog?.FindCombatProfile(item.Id) != null)
                return string.Empty;
            return amount.ToString();
        }

        private ItemDefinition CurrentInventoryItem()
        {
            var inventory = runtimeServices?.PlayerInventory;
            if (inventory == null || inventory.Slots.Count == 0) return null;
            var slot = inventory.Slots[Mathf.Clamp(selectedIndex, 0, inventory.Slots.Count - 1)];
            return string.IsNullOrEmpty(slot.itemId) ? null : gameDataCatalog.FindItem(slot.itemId);
        }

        private bool IsInventoryPlaceable(ItemDefinition item)
        {
            return IsInventoryItemPlaceable(item, visibleRecipes, CurrentDay);
        }

        private void RefreshEquipment()
        {
            RebuildOwnedEquipment();
            RefreshEquipmentVisual();
            collectButton.gameObject.SetActive(false);
            var activeSlotItem = CurrentActiveSlotItem();
            if (activeSlotItem != null)
            {
                var activeSlot = runtimeServices.ActiveSlot;
                var activeEquipped = activeSlot.EquippedItemId == activeSlotItem.Id;
                var portableLanternSelected = activeEquipped &&
                                              activeSlotItem.Id == PortableLanternRuntime.LanternItemId;
                collectButton.gameObject.SetActive(portableLanternSelected);
                if (portableLanternSelected)
                {
                    collectButton.GetComponentInChildren<Text>().text = "R · 석탄 1개 투입";
                    collectButton.interactable = runtimeServices.PlayerInventory.Has(
                        PortableLanternRuntime.FuelItemId, 1);
                }
                titleText.text = $"보유 장비 {selectedIndex + 1}/{CurrentEquipmentEntryCount()} · {activeSlotItem.DisplayName}";
                primaryButton.GetComponentInChildren<Text>().text = activeEquipped ? "E · 해제" : "E · 장착";
                primaryButton.interactable = true;
                detailsText.text =
                    (GimmickWeaponCombatRules.IsGimmickWeaponId(activeSlotItem.Id)
                        ? $"공격 피해 배율: 현재 발톱의 {GimmickWeaponCombatRules.ResolveBonus(gameDataCatalog):0%} (반올림)\n" +
                          "장착 후 활성 상태에서 적용 · 채굴 등급은 유지\n\n"
                        : string.Empty) +
                    $"부위: 무기·도구 1 · {(activeEquipped ? "장착 중" : "소지품")}" +
                    $"{(activeEquipped ? activeSlot.IsUsingEquippedItem ? " · 활성" : " · 맨 발톱 활성" : string.Empty)}\n" +
                    "Q: 맨 발톱 ↔ 장착물 전환 · F: 부채 스킬(지원 장비)\n" +
                    "채굴은 활성 상태와 관계없이 항상 현재 발톱 티어를 사용합니다.\n\n" +
                    (portableLanternSelected
                        ? $"휴대용 등불: {(runtimeServices.PortableLantern.IsLit ? "점등" : "소등")} · " +
                          $"연료 {Mathf.CeilToInt(runtimeServices.PortableLantern.FuelRemainingSeconds)}초 · " +
                          $"반경 {runtimeServices.PortableLantern.RadiusTiles:0.#}칸\n" +
                          "R 또는 아래 버튼: 석탄 1개 투입 (270초)\n\n"
                        : string.Empty) +
                    BuildEquippedSummary();
                return;
            }

            var equipment = CurrentEquipment();
            if (equipment == null)
            {
                titleText.text = "보유 장비 없음";
                detailsText.text = BuildEquippedSummary();
                primaryButton.GetComponentInChildren<Text>().text = "장착";
                primaryButton.interactable = false;
                return;
            }
            var equippedSlot = FindEquippedSlot(equipment);
            var equipped = equippedSlot.HasValue;
            var item = gameDataCatalog.FindItem(equipment.Id);
            titleText.text = $"보유 장비 {selectedIndex + 1}/{CurrentEquipmentEntryCount()} · {gameDataCatalog.ItemDisplayName(equipment.Id, "장비")}";
            primaryButton.GetComponentInChildren<Text>().text = equipped ? "E · 해제" : "E · 장착";
            primaryButton.interactable = true;
            var artifactVerb = equipment.VerbId;
            if (artifactVerb == ArtifactVerbId.None)
                ArtifactVerbCatalog.TryGetVerb(equipment.Id, out artifactVerb);
            if (artifactVerb == ArtifactVerbId.ExtendCoolerRadius)
            {
                // Describe the connected storage modifier; spatial cooler range is audited separately.
                detailsText.text =
                    "얼음 보관을 돕는 장신구\n" +
                    $"장착 효과: 얼음 자연 용해 속도 {ArtifactVerbRuntime.IceMeltSlowMultiplier:0%}\n" +
                    "소지만으로는 적용되지 않습니다. 장신구 칸에 장착하세요.\n" +
                    "공격력·방어력 증가 효과는 없습니다.\n\n" +
                    $"상태: {(equipped ? $"장착 중 ({EquipmentSlotLabel(equippedSlot.Value)})" : "미장착 · E로 장착")}\n\n" +
                    BuildEquippedSummary();
                return;
            }
            detailsText.text =
                $"부위: {EquipmentSlotLabel(equipment.Slot)} · {(equipped ? $"장착 중 ({EquipmentSlotLabel(equippedSlot.Value)})" : "미장착")}\n" +
                $"방어력: {equipment.Defense:+0;-0;0}\n" +
                $"이동: {equipment.MovementBonus:+0%;-0%;0%} · 채굴 치명타: {equipment.MiningCriticalBonus:+0%;-0%;0%}\n" +
                $"체온 상승: {equipment.TemperatureRiseModifier:+0%;-0%;0%} · 화염 피해: {equipment.FireDamageModifier:+0%;-0%;0%}\n" +
                $"시야: {equipment.VisionRadiusBonus:+0.#;-0.#;0} · 이단 점프: {(equipment.GrantsDoubleJump ? "O" : "-")}\n\n" +
                (equipment.SetId == ArmorSetRules.SeolhanpungSetId
                    ? "설한풍 3종 세트: 햇빛 피해 면역\n화염 피해 −25% · 낮 체온 상승 −20%\n내한 하한 미달 시 세트 효과 비활성\n\n" : string.Empty) +
                BuildEquippedSummary();
        }

        private void RefreshEquipmentVisual()
        {
            if (equipmentVisualRoot == null) return;
            if (equipmentAmmoCount != null)
            {
                var usesAmmo = BowCombatRules.IsBowWeaponId(EquippedItemIdForVisualSlot(0));
                equipmentAmmoCount.text = usesAmmo
                    ? runtimeServices.PlayerInventory.Count(BowCombatRules.AmmoItemId).ToString()
                    : string.Empty;
                equipmentAmmoCount.enabled = usesAmmo;
            }
            var selectedSlot = ResolveSelectedEquipmentVisualSlot();
            for (var index = 0; index < equipmentSlotBackgrounds.Length; index++)
            {
                var background = equipmentSlotBackgrounds[index];
                if (background == null) continue;
                background.sprite = EquipmentSlotSprite(index, index == selectedSlot);
                background.color = background.sprite != null
                    ? Color.white
                    : index == selectedSlot
                        ? new Color(.95f, .65f, .12f, 1f)
                        : new Color(.14f, .2f, .28f, 1f);
                var itemId = EquippedItemIdForVisualSlot(index);
                var button = equipmentSlotButtons[index];
                if (button != null) button.interactable = !string.IsNullOrEmpty(itemId);
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

        private int ResolveSelectedEquipmentVisualSlot()
        {
            if (CurrentActiveSlotItem() != null) return 0;
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
                default: return definition.IsAccessory ? 4 : -1;
            }
        }

        private string EquippedItemIdForVisualSlot(int index)
        {
            if (index == 0) return runtimeServices.ActiveSlot.EquippedItemId;
            var slot = index switch
            {
                1 => EquipmentSlot.Head,
                2 => EquipmentSlot.Body,
                3 => EquipmentSlot.Feet,
                4 => EquipmentSlot.AccessoryOne,
                5 => EquipmentSlot.AccessoryTwo,
                _ => EquipmentSlot.Head
            };
            return runtimeServices.EquipmentSystem.Get(slot)?.Id;
        }

        private void SelectEquipmentVisualSlot(int visualSlotIndex)
        {
            if (!open || page != Page.Equipment ||
                visualSlotIndex < 0 || visualSlotIndex >= equipmentSlotBackgrounds.Length)
                return;
            RebuildOwnedEquipment();
            var itemId = EquippedItemIdForVisualSlot(visualSlotIndex);
            var entryIndex = ResolveEquipmentEntryIndex(
                itemId, activeSlotItems, ownedEquipment);
            if (entryIndex < 0) return;
            selectedIndex = entryIndex;
            RefreshEquipment();
        }

        public static int ResolveEquipmentEntryIndex(
            string itemId, IReadOnlyList<ItemDefinition> activeItems,
            IReadOnlyList<EquipmentDefinition> equipment)
        {
            if (string.IsNullOrEmpty(itemId)) return -1;
            if (activeItems != null)
                for (var index = 0; index < activeItems.Count; index++)
                    if (string.Equals(activeItems[index]?.Id, itemId, StringComparison.Ordinal))
                        return index;
            if (equipment != null)
                for (var index = 0; index < equipment.Count; index++)
                    if (string.Equals(equipment[index]?.Id, itemId, StringComparison.Ordinal))
                        return (activeItems?.Count ?? 0) + index;
            return -1;
        }

        private Sprite EquipmentSlotSprite(int index, bool selected)
        {
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

        private EquipmentDefinition CurrentEquipment()
        {
            var equipmentIndex = selectedIndex - activeSlotItems.Count;
            if (ownedEquipment.Count == 0 || equipmentIndex < 0 || equipmentIndex >= ownedEquipment.Count)
                return null;
            return ownedEquipment[equipmentIndex];
        }

        private int CurrentEquipmentEntryCount() => activeSlotItems.Count + ownedEquipment.Count;

        private EquipmentSlot? FindEquippedSlot(EquipmentDefinition definition)
        {
            if (definition == null) return null;
            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
                if (runtimeServices.EquipmentSystem.Get(slot) == definition) return slot;
            return null;
        }

        private string BuildEquippedSummary()
        {
            var builder = new StringBuilder($"발톱 티어: T{ResolveClawTier()} (장비 칸과 별도)\n현재 장착 6칸:\n");
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

        private int ResolveClawTier()
        {
            if (runtimeServices.PlayerInventory.Has("icesteel_claw", 1)) return 3;
            if (runtimeServices.PlayerInventory.Has("iron_claw", 1)) return 2;
            return 1;
        }

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

        private CraftingStation NearbyStation() => openedStation == CraftingStation.None
            ? CraftingStation.None
            : stationSource != null && stationSource.TryGetCraftingStationIdentity(openedStation,
                openedStationObjectId, out _, out _) ? openedStation : CraftingStation.None;

        private StationQueueRecord ResolveProductionQueue()
        {
            if (bindingProductionStation || runtimeServices?.StationProduction == null) return null;
            var position = Vector2.zero;
            var id = StationProductionService.HandStationId;
            if (openedStation != CraftingStation.None)
            {
                if (stationSource == null || !stationSource.TryGetCraftingStationIdentity(openedStation,
                        openedStationObjectId, out id, out position)) return null;
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
            var visible = open && page == Page.Crafting && !HasStorageMode;
            if (productionQueueRoot == null && visible)
            {
                productionQueueRoot = CreateUiObject("ProductionQueue", panel.transform,
                    new Vector2(250f, 76f), new Vector2(89f, -36f));
                var background = productionQueueRoot.AddComponent<Image>();
                background.color = new Color(.035f, .055f, .08f, .95f);
                background.raycastTarget = true;
                productionQueueHeader = CreateText(productionQueueRoot.transform, "Header", 8,
                    TextAnchor.MiddleLeft, new Vector2(175f, 12f), new Vector2(-34f, 31f));
                productionQueueCollectButton = CreateButton(productionQueueRoot.transform, "Collect",
                    "회수", new Vector2(91f, 31f), new Vector2(58f, 12f), () =>
                    {
                        var queue = ResolveProductionQueue();
                        var count = runtimeServices.StationProduction.Collect(queue);
                        ShowMessage(count > 0 ? "완료품·반환 재료를 회수했습니다." : "인벤토리 공간이 부족합니다.");
                        Refresh();
                    }, false);
                productionQueueCollectButton.GetComponentInChildren<Text>().fontSize = 7;
                for (var i = 0; i < StationProductionService.WaitingCapacity + 1; i++)
                {
                    var capturedIndex = i;
                    var y = 18f - i * 12f;
                    productionQueueLabels.Add(CreateText(productionQueueRoot.transform, $"Job_{i}", 7,
                        TextAnchor.MiddleLeft, new Vector2(197f, 11f), new Vector2(-21f, y)));
                    var cancel = CreateButton(productionQueueRoot.transform, $"Cancel_{i}", "취소",
                        new Vector2(101f, y), new Vector2(36f, 11f), () =>
                        {
                            var queue = ResolveProductionQueue();
                            var displayedJob = displayedProductionJobs[capturedIndex];
                            var jobIndex = queue != null && displayedJob != null
                                ? queue.jobs.IndexOf(displayedJob) : -1;
                            if (runtimeServices.StationProduction.Cancel(queue, jobIndex))
                                ShowMessage(queue.returns.Count > 0
                                    ? "제작 취소 · 반환 대기분은 회수 버튼으로 받으세요."
                                    : "제작 취소 · 재료를 반환했습니다.");
                            Refresh();
                        }, false);
                    cancel.GetComponentInChildren<Text>().fontSize = 7;
                    productionQueueCancelButtons.Add(cancel);
                }
            }
            if (productionQueueRoot == null) return;
            productionQueueRoot.SetActive(visible);
            if (!visible) return;
            var state = ResolveProductionQueue();
            productionQueueHeader.text = state == null ? "제작대를 사용할 수 없습니다." :
                $"진행 {(state.jobs.Count > 0 ? 1 : 0)}/1 · 대기 {Mathf.Max(0, state.jobs.Count - 1)}/4 · 회수 {state.returns.Count}";
            productionQueueCollectButton.interactable = state?.returns.Count > 0;
            for (var i = 0; i < productionQueueLabels.Count; i++)
            {
                var hasJob = state != null && i < state.jobs.Count;
                displayedProductionJobs[i] = hasJob ? state.jobs[i] : null;
                productionQueueCancelButtons[i].gameObject.SetActive(hasJob);
                productionQueueLabels[i].text = hasJob
                    ? $"{(i == 0 ? "진행" : "대기 " + i)} · {runtimeServices.StationProduction.OutputName(state.jobs[i])}" +
                      (i == 0 ? $" · {state.jobs[i].remaining:0.0}초" : string.Empty)
                    : i == 0 ? "진행 중인 작업 없음" : $"대기 {i} · 비어 있음";
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void GrantSelectedRequirementsForEditorTest()
        {
            if (page == Page.Crafting && !IsShowingSmeltingList)
            {
                var recipe = CurrentRecipe();
                if (recipe != null && TryGrantItems(recipe.Ingredients))
                    ShowMessage($"F12 테스트 재료 지급: {recipe.Output.item.DisplayName}");
                else ShowMessage("F12 재료 지급 실패: 인벤토리 공간을 확인하세요.");
                return;
            }
            if (page == Page.Crafting && IsShowingSmeltingList)
            {
                var definition = CurrentSmelting();
                var requirements = definition == null ? null : new[] { definition.Input, definition.Fuel };
                if (requirements != null && TryGrantItems(requirements))
                    ShowMessage($"F12 테스트 재료·연료 지급: {definition.Output.item.DisplayName}");
                else ShowMessage("F12 제련 재료 지급 실패: 인벤토리 공간을 확인하세요.");
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
            if (page == Page.Crafting)
                return IsSmeltingStation(openedStation)
                    ? furnaceSmeltingView
                        ? "Q 제작 목록으로 전환 · A/D·←/→ 선택 · E 제련 · ESC 닫기"
                        : "Q 제련 목록으로 전환 · W/S·↑/↓ 선택 · E 제작 · ESC 닫기"
                    : "C 제작 탭 · ESC 닫기 · W/S·↑/↓ 선택 · E 실행";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return page == Page.Gathering
                ? "좌클릭 전체 · 우클릭 절반 집기/1개 놓기 · E 사용/설치 · ESC 닫기"
                : "Tab 인벤 · C 제작 · G 장비 · J 도감 · ESC 닫기 · E 실행";
#else
            return page == Page.Gathering
                ? "좌클릭 전체 · 우클릭 절반 집기/1개 놓기 · E 사용/설치 · ESC 닫기"
                : "Tab 인벤 · C 제작 · G 장비 · J 도감 · ESC 닫기 · E 실행";
#endif
        }

        private void RefreshTabButtons()
        {
            for (var index = 0; index < tabButtons.Length; index++)
            {
                var image = tabButtons[index]?.targetGraphic as Image;
                if (image != null)
                    image.color = index == (int)page
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
            result.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
            ReturnHeldInventoryItem();
            HideInventoryCursorPreview();
            if (cursorOwner == this) cursorOwner = null;
        }

        private void OnDestroy()
        {
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
