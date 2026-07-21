using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Nyangbingo.Core;
using Nyangbingo.Crafting;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using Nyangbingo.Save;
using Nyangbingo.World;
using UnityEngine;
using UnityEngine.UI;

namespace Nyangbingo.UI
{
    /// <summary>
    /// MainGame의 범용 제작·제련 제품 UI. 공식 데이터는 전부 적재하되 crafting_b_ui 정책에 따라
    /// scope B 레시피만 표시 단계에서 제외한다.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public sealed class MainGameCraftingUiController : MonoBehaviour
    {
        private enum Page { Gathering, Crafting, Equipment, Codex }

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
        private GameObject panel;
        private Text titleText;
        private Text detailsText;
        private Text messageText;
        private ScrollRect detailsScrollRect;
        private RectTransform detailsViewportRect;
        private RectTransform detailsScrollbarRect;
        private GameObject inventoryGridRoot;
        private GameObject craftingListRoot;
        private ScrollRect craftingListScrollRect;
        private Button[] craftingListButtons = Array.Empty<Button>();
        private Text[] craftingListLabels = Array.Empty<Text>();
        private GameObject codexGridRoot;
        private GameObject storageModeRoot;
        private readonly Text[] storagePlayerLabels = new Text[Nyangbingo.Inventory.Inventory.SlotCount];
        private readonly Text[] storageLabels = new Text[JangdokStorageRuntime.SlotCount];
        private Text storageHintText;
        private string storageObjectId = string.Empty;
        private readonly Text[] inventoryGridLabels =
            new Text[Nyangbingo.Inventory.Inventory.SlotCount];
        private readonly Button[] inventoryGridButtons =
            new Button[Nyangbingo.Inventory.Inventory.SlotCount];
        private readonly Image[] inventoryGridIcons =
            new Image[Nyangbingo.Inventory.Inventory.SlotCount];
        private GameObject equipmentVisualRoot;
        private Image equipmentCharacter;
        private readonly Image[] equipmentSlotBackgrounds = new Image[6];
        private readonly Image[] equipmentSlotIcons = new Image[6];
        private readonly Text[] equipmentSlotFallbackLabels = new Text[6];
        private readonly Button[] codexCardButtons = new Button[YokaiCodexPresentationModel.ExpectedCardCount];
        private readonly Text[] codexCardLabels = new Text[YokaiCodexPresentationModel.ExpectedCardCount];
        private readonly Image[] codexCardPortraits = new Image[YokaiCodexPresentationModel.ExpectedCardCount];
        private GameObject codexExpandedBackdrop;
        private Image codexExpandedCardBackground;
        private Image codexExpandedPortrait;
        private Text codexExpandedTitle;
        private Text codexExpandedFrontText;
        private Text codexExpandedBackText;
        private Text codexExpandedHintText;
        private Button previousButton;
        private Button nextButton;
        private Button primaryButton;
        private Button collectButton;
        private readonly Button[] tabButtons = new Button[4];
        private Page page;
        private int selectedIndex;
        private string message;
        private float messageUntil;
        private bool initialized;
        private bool open;
        private bool showingSmelting;
        private YokaiCodexPresentationModel codexModel;
        private CharacterArtCatalog characterArtCatalog;
        private static int openControllerCount;
        private static int escapeConsumedFrame = -1;
        private int openedFrame = -1;

        public static bool BlocksGameplayInput => openControllerCount > 0;
        public static bool ConsumedEscapeThisFrame => escapeConsumedFrame == Time.frameCount;
        public bool IsOpen => open;
        public int VisibleRecipeCount => visibleRecipes.Count;
        public const int UnifiedTabCount = 4;
        public const int InventoryGridColumns = 10;
        public const int InventoryGridRows = 5;
        public const float InventorySlotPixelSize = 27f;

        public static string UnifiedTabLabel(int index)
        {
            switch (index)
            {
                case 0: return "채집";
                case 1: return "제작";
                case 2: return "장비";
                case 3: return "도감";
                default: return string.Empty;
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

            var hideScopeB = string.Equals(gameDataCatalog.FindGlobal("crafting_b_ui")?.Value,
                "hidden", StringComparison.OrdinalIgnoreCase);
            visibleRecipes.AddRange(gameDataCatalog.Recipes
                .Where(recipe => ShouldShowRecipe(recipe, hideScopeB))
                .OrderBy(recipe => recipe.Station)
                .ThenBy(recipe => recipe.Id, StringComparer.Ordinal));
            smeltingRecipes.AddRange(gameDataCatalog.Smelting
                .Where(definition => definition != null)
                .OrderBy(definition => definition.StationKind)
                .ThenBy(definition => definition.Id, StringComparer.Ordinal));
            BuildUi();
            var saveCoordinator = FindAnyObjectByType<MainGameSaveCoordinator>();
            codexModel = saveCoordinator?.ProgressTracker?.CreateCodexPresentationModel();
            ResolveCharacterArtCatalog();
            FindAnyObjectByType<MainGameCodexController>()?.UseUnifiedPanel();
            runtimeServices.PlayerInventory.Changed += Refresh;
            runtimeServices.EquipmentSystem.Changed += Refresh;
            runtimeServices.EquipmentCollection.Added += HandleEquipmentAdded;
            runtimeServices.ActiveSlot.Changed += Refresh;
            runtimeServices.PortableLantern.Changed += Refresh;
            runtimeServices.JangdokStorage.Changed += Refresh;
            if (turretRuntime != null) turretRuntime.BuildStateChanged += Refresh;
            initialized = true;
            SetOpen(false);
            Refresh();
            Debug.Log($"[Nyangbingo] MainGame v29 unified UI ready " +
                      $"(tabs=4, recipes={visibleRecipes.Count}, hiddenScopeB={hideScopeB}, " +
                      $"contextualSmelting={smeltingRecipes.Count}, codex={codexModel?.Cards.Count ?? 0}).");
        }

        private void Update()
        {
            if (!initialized) return;

            if (open && shell != null && shell.Screen != GameShellScreen.Gameplay)
            {
                SetOpen(false);
                return;
            }

            if (open && Input.GetKeyDown(KeyCode.Escape))
            {
                escapeConsumedFrame = Time.frameCount;
                SetOpen(false);
                return;
            }

            if (open && !string.IsNullOrEmpty(storageObjectId))
            {
                if (openedFrame != Time.frameCount && Input.GetKeyDown(KeyCode.E)) SetOpen(false);
                return;
            }

            if (TryHandlePageHotkey()) return;
            if (!open) return;
            if (page != Page.Codex)
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) SelectRelative(-1);
                if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) SelectRelative(1);
                if (page == Page.Gathering &&
                    (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)))
                    SelectRelative(-InventoryGridColumns);
                if (page == Page.Gathering &&
                    (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)))
                    SelectRelative(InventoryGridColumns);
                if (page == Page.Equipment && Input.GetKeyDown(KeyCode.Q)) ToggleActiveSlotFromEquipmentPage();
                if (openedFrame != Time.frameCount && Input.GetKeyDown(KeyCode.E)) TryPrimaryAction();
                if (page == Page.Equipment && Input.GetKeyDown(KeyCode.R)) TryRefuelPortableLantern();
#if UNITY_EDITOR
                if (page != Page.Gathering && Input.GetKeyDown(KeyCode.F4))
                {
                    if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                        TeleportToRequiredStationForEditorTest();
                    else GrantSelectedRequirementsForEditorTest();
                }
#endif
            }
            if (!string.IsNullOrEmpty(message) && Time.unscaledTime >= messageUntil) message = string.Empty;
            Refresh();
        }

        public static bool ShouldShowRecipe(RecipeDefinition recipe, bool hideScopeB) =>
            recipe != null && (!hideScopeB || recipe.MvpScope != ItemMvpScope.B);

        public static bool IsRecipeVisibleAtStation(CraftingStation requiredStation,
            CraftingStation nearbyStation) =>
            requiredStation == CraftingStation.None || requiredStation == nearbyStation;

        public bool TryOpenForStation(CraftingStation station)
        {
            if (!initialized || station == CraftingStation.None ||
                shell != null && shell.Screen != GameShellScreen.Gameplay || Time.timeScale <= 0f) return false;

            OpenPage(Page.Crafting);
            showingSmelting = IsSmeltingStation(station);
            if (!showingSmelting) RebuildFilteredRecipes(station);
            selectedIndex = FindFirstEntryForStation(station);
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
            storageObjectId = objectId;
            selectedIndex = 0;
            SetOpen(true);
            Refresh();
            Debug.Log($"[Nyangbingo] Jangdok storage opened: id={objectId}, slots={JangdokStorageRuntime.SlotCount}.");
            return true;
        }

        public static bool IsSmeltingStation(CraftingStation station) =>
            station == CraftingStation.Furnace || station == CraftingStation.Foundry;

        private void BuildUi()
        {
            var uiRoot = MainGameUiResolutionController.ResolveNativeRoot(transform) ?? transform;
            panel = CreateUiObject("CraftingAndSmeltingPanel", uiRoot, new Vector2(470f, 260f), Vector2.zero);
            var background = panel.AddComponent<Image>();
            background.color = new Color(.035f, .05f, .075f, .96f);
            titleText = CreateText(panel.transform, "Title", 15, TextAnchor.MiddleCenter,
                new Vector2(450f, 20f), new Vector2(0f, 118f));
            tabButtons[0] = CreateButton(panel.transform, "GatheringTab", "1 · 채집",
                new Vector2(-165f, 94f), new Vector2(108f, 20f), () => TogglePage(Page.Gathering));
            tabButtons[1] = CreateButton(panel.transform, "CraftingTab", "2 · 제작",
                new Vector2(-55f, 94f), new Vector2(108f, 20f), () => TogglePage(Page.Crafting));
            tabButtons[2] = CreateButton(panel.transform, "EquipmentTab", "3 · 장비",
                new Vector2(55f, 94f), new Vector2(108f, 20f), () => TogglePage(Page.Equipment));
            tabButtons[3] = CreateButton(panel.transform, "CodexTab", "4 · 도감",
                new Vector2(165f, 94f), new Vector2(108f, 20f), () => TogglePage(Page.Codex));
            BuildDetailsScrollArea();
            BuildCraftingList();
            BuildInventoryGrid();
            BuildEquipmentVisual();
            BuildStorageUi();
            BuildCodexGrid();
            messageText = CreateText(panel.transform, "Message", 9, TextAnchor.MiddleCenter,
                new Vector2(450f, 18f), new Vector2(0f, -68f));

            previousButton = CreateButton(panel.transform, "Previous", "◀ 이전", new Vector2(-165f, -91f),
                new Vector2(90f, 22f), () => SelectRelative(-1));
            nextButton = CreateButton(panel.transform, "Next", "다음 ▶", new Vector2(-70f, -91f),
                new Vector2(90f, 22f), () => SelectRelative(1));
            primaryButton = CreateButton(panel.transform, "Primary", "제작", new Vector2(110f, -91f),
                new Vector2(240f, 22f), TryPrimaryAction);
            collectButton = CreateButton(panel.transform, "Collect", "완료품 일괄 회수", new Vector2(-75f, -118f),
                new Vector2(160f, 20f), TrySecondaryAction);
            CreateButton(panel.transform, "Close", "ESC · 닫기", new Vector2(115f, -118f),
                new Vector2(200f, 20f), () => SetOpen(false));
            BuildCodexExpandedView();
        }

        private void BuildDetailsScrollArea()
        {
            var viewportObject = CreateUiObject("DetailsViewport", panel.transform,
                new Vector2(438f, 134f), new Vector2(-5f, 11f));
            detailsViewportRect = (RectTransform)viewportObject.transform;
            var viewportRaycast = viewportObject.AddComponent<Image>();
            viewportRaycast.color = new Color(0f, 0f, 0f, 0f);
            viewportObject.AddComponent<RectMask2D>();

            var contentObject = CreateUiObject("Details", viewportObject.transform, Vector2.zero, Vector2.zero);
            var contentRect = (RectTransform)contentObject.transform;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;

            detailsText = contentObject.AddComponent<Text>();
            detailsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            detailsText.fontSize = 11;
            detailsText.lineSpacing = .95f;
            detailsText.alignment = TextAnchor.UpperLeft;
            detailsText.color = new Color(.93f, .96f, 1f);
            detailsText.horizontalOverflow = HorizontalWrapMode.Wrap;
            detailsText.verticalOverflow = VerticalWrapMode.Overflow;

            var fitter = contentObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            detailsScrollRect = viewportObject.AddComponent<ScrollRect>();
            detailsScrollRect.content = contentRect;
            detailsScrollRect.viewport = detailsViewportRect;
            detailsScrollRect.horizontal = false;
            detailsScrollRect.vertical = true;
            detailsScrollRect.movementType = ScrollRect.MovementType.Clamped;
            detailsScrollRect.scrollSensitivity = 14f;

            var scrollbarObject = CreateUiObject("DetailsScrollbar", panel.transform,
                new Vector2(6f, 134f), new Vector2(222f, 11f));
            detailsScrollbarRect = (RectTransform)scrollbarObject.transform;
            var scrollbarBackground = scrollbarObject.AddComponent<Image>();
            scrollbarBackground.color = new Color(.08f, .12f, .17f, .9f);
            var scrollbar = scrollbarObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            var handleObject = CreateUiObject("Handle", scrollbarObject.transform,
                new Vector2(5f, 36f), Vector2.zero);
            var handleRect = (RectTransform)handleObject.transform;
            var handleImage = handleObject.AddComponent<Image>();
            handleImage.color = new Color(.35f, .55f, .72f, 1f);
            scrollbar.handleRect = handleRect;
            scrollbar.targetGraphic = handleImage;
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
            for (var index = 0; index < craftingListButtons.Length; index++)
            {
                var capturedIndex = index;
                var button = CreateButton(contentObject.transform, $"Recipe_{index + 1:00}", string.Empty,
                    Vector2.zero, new Vector2(168f, 34f), () => SelectCraftingRecipe(capturedIndex));
                var layoutElement = button.gameObject.AddComponent<LayoutElement>();
                layoutElement.preferredHeight = 34f;
                layoutElement.minHeight = 34f;
                var label = button.GetComponentInChildren<Text>();
                label.fontSize = 8;
                label.supportRichText = true;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
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
            if (!open || page != Page.Crafting || showingSmelting ||
                index < 0 || index >= filteredRecipes.Count) return;
            selectedIndex = index;
            message = string.Empty;
            Refresh();
            ResetDetailsScroll();
        }

        private void BuildInventoryGrid()
        {
            inventoryGridRoot = CreateUiObject("InventoryGrid10x5", panel.transform,
                new Vector2(297f, 147f), new Vector2(0f, 3f));
            var grid = inventoryGridRoot.AddComponent<GridLayoutGroup>();
            grid.padding = new RectOffset();
            grid.cellSize = Vector2.one * InventorySlotPixelSize;
            grid.spacing = Vector2.one * 3f;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = InventoryGridColumns;

            for (var index = 0; index < inventoryGridLabels.Length; index++)
            {
                var capturedIndex = index;
                var button = CreateButton(inventoryGridRoot.transform,
                    $"InventorySlot_{index + 1:00}", string.Empty, Vector2.zero, grid.cellSize,
                    () => SelectInventorySlot(capturedIndex));
                inventoryGridButtons[index] = button;
                inventoryGridLabels[index] = button.GetComponentInChildren<Text>();
                inventoryGridLabels[index].fontSize = 8;
                inventoryGridLabels[index].fontStyle = FontStyle.Bold;
                inventoryGridLabels[index].alignment = TextAnchor.LowerRight;
                inventoryGridLabels[index].horizontalOverflow = HorizontalWrapMode.Overflow;
                inventoryGridLabels[index].verticalOverflow = VerticalWrapMode.Truncate;
                var labelRect = inventoryGridLabels[index].rectTransform;
                labelRect.offsetMin = new Vector2(2f, 1f);
                labelRect.offsetMax = new Vector2(-2f, -1f);
                var shadow = inventoryGridLabels[index].gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, .9f);
                shadow.effectDistance = Vector2.one;

                var iconObject = CreateUiObject("Icon", button.transform, new Vector2(19f, 19f), Vector2.zero);
                var icon = iconObject.AddComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                icon.enabled = false;
                iconObject.transform.SetAsFirstSibling();
                inventoryGridIcons[index] = icon;
            }

            inventoryGridRoot.SetActive(false);
        }

        private void BuildEquipmentVisual()
        {
            equipmentVisualRoot = CreateUiObject("EquipmentVisual", panel.transform,
                new Vector2(142f, 142f), new Vector2(-145f, 4f));
            equipmentCharacter = CreateArtImage(equipmentVisualRoot.transform, "Character",
                gameplayArtCatalog?.EquipmentCharacter, Vector2.zero, new Vector2(36f, 60f));
            var positions = new[]
            {
                new Vector2(48f, 43f), new Vector2(-48f, 43f), new Vector2(-48f, 0f),
                new Vector2(-48f, -43f), new Vector2(48f, 0f), new Vector2(48f, -43f)
            };
            for (var index = 0; index < equipmentSlotBackgrounds.Length; index++)
            {
                var slot = CreateUiObject($"EquipmentSlot_{index}", equipmentVisualRoot.transform,
                    new Vector2(34f, 34f), positions[index]);
                var background = slot.AddComponent<Image>();
                background.raycastTarget = false;
                equipmentSlotBackgrounds[index] = background;
                equipmentSlotIcons[index] = CreateArtImage(slot.transform, "Icon", null,
                    Vector2.zero, new Vector2(22f, 22f));
                equipmentSlotIcons[index].enabled = false;
                var fallback = CreateText(slot.transform, "FallbackLabel", 8, TextAnchor.MiddleCenter,
                    new Vector2(24f, 24f), Vector2.zero);
                fallback.fontStyle = FontStyle.Bold;
                fallback.raycastTarget = false;
                fallback.enabled = false;
                var shadow = fallback.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, .9f);
                shadow.effectDistance = Vector2.one;
                equipmentSlotFallbackLabels[index] = fallback;
            }
            equipmentVisualRoot.SetActive(false);
        }

        private void BuildStorageUi()
        {
            storageModeRoot = CreateUiObject("JangdokStorageMode", panel.transform,
                new Vector2(450f, 206f), new Vector2(0f, -2f));
            CreateText(storageModeRoot.transform, "StorageLabel", 9, TextAnchor.MiddleCenter,
                new Vector2(205f, 16f), new Vector2(-110f, 88f)).text = "장독 창고 · 40슬롯";
            CreateText(storageModeRoot.transform, "PlayerLabel", 9, TextAnchor.MiddleCenter,
                new Vector2(205f, 16f), new Vector2(110f, 88f)).text = "플레이어 · 50슬롯";

            BuildTransferGrid(storageModeRoot.transform, "StorageGrid", new Vector2(-110f, -4f),
                5, 8, storageLabels, TransferStorageSlot);
            BuildTransferGrid(storageModeRoot.transform, "PlayerGrid", new Vector2(110f, -4f),
                5, 10, storagePlayerLabels, TransferPlayerSlot);
            storageHintText = CreateText(storageModeRoot.transform, "Hint", 8, TextAnchor.MiddleCenter,
                new Vector2(430f, 16f), new Vector2(0f, -96f));
            storageHintText.text = "슬롯 클릭: 묶음 이동  |  E 또는 ESC: 닫기";
            storageModeRoot.SetActive(false);
        }

        private static void BuildTransferGrid(Transform parent, string name, Vector2 position, int columns,
            int rows, Text[] labels, Action<int> clicked)
        {
            var root = CreateUiObject(name, parent, new Vector2(205f, rows * 18f + (rows - 1) * 2f), position);
            var grid = root.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(39f, 18f);
            grid.spacing = new Vector2(2f, 2f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.childAlignment = TextAnchor.UpperLeft;
            for (var index = 0; index < labels.Length; index++)
            {
                var captured = index;
                var button = CreateButton(root.transform, $"Slot_{index + 1:00}", string.Empty,
                    Vector2.zero, grid.cellSize, () => clicked(captured));
                var label = button.GetComponentInChildren<Text>();
                label.fontSize = 6;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                labels[index] = label;
            }
        }

        private void TransferPlayerSlot(int index)
        {
            if (!runtimeServices.JangdokStorage.TryGet(storageObjectId, out var storage)) return;
            storageHintText.text = runtimeServices.PlayerInventory.TryTransferSlotTo(index, storage)
                ? "장독 창고에 보관했습니다."
                : "빈 슬롯이거나 장독 창고에 공간이 없습니다.";
            Refresh();
        }

        private void TransferStorageSlot(int index)
        {
            if (!runtimeServices.JangdokStorage.TryGet(storageObjectId, out var storage)) return;
            storageHintText.text = storage.TryTransferSlotTo(index, runtimeServices.PlayerInventory)
                ? "플레이어 인벤토리로 꺼냈습니다."
                : "빈 슬롯이거나 플레이어 인벤토리에 공간이 없습니다.";
            Refresh();
        }

        private void BuildCodexGrid()
        {
            codexGridRoot = CreateUiObject("IntegratedCodexViewport", panel.transform,
                new Vector2(244f, 184f), new Vector2(0f, -14f));
            var viewport = (RectTransform)codexGridRoot.transform;
            var viewportImage = codexGridRoot.AddComponent<Image>();
            viewportImage.color = new Color(.025f, .035f, .045f, .72f);
            codexGridRoot.AddComponent<RectMask2D>();

            var gridObject = CreateUiObject("CardGrid", codexGridRoot.transform, Vector2.zero, Vector2.zero);
            var gridRect = (RectTransform)gridObject.transform;
            gridRect.anchorMin = gridRect.anchorMax = gridRect.pivot = new Vector2(.5f, 1f);
            gridRect.anchoredPosition = Vector2.zero;
            var rows = Mathf.CeilToInt((float)YokaiCodexPresentationModel.ExpectedCardCount /
                                       YokaiCodexPresentationModel.GridColumns);
            const float spacing = 6f;
            gridRect.sizeDelta = new Vector2(
                YokaiCodexPresentationModel.GridColumns * YokaiCodexPresentationModel.GridCardSize.x +
                (YokaiCodexPresentationModel.GridColumns - 1) * spacing,
                rows * YokaiCodexPresentationModel.GridCardSize.y + (rows - 1) * spacing);
            var grid = gridObject.AddComponent<GridLayoutGroup>();
            grid.padding = new RectOffset();
            grid.cellSize = YokaiCodexPresentationModel.GridCardSize;
            grid.spacing = Vector2.one * spacing;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = YokaiCodexPresentationModel.GridColumns;

            var scroll = codexGridRoot.AddComponent<ScrollRect>();
            scroll.content = gridRect;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 18f;

            for (var index = 0; index < codexCardButtons.Length; index++)
            {
                var capturedIndex = index;
                var button = CreateButton(gridObject.transform, $"CodexCard_{index + 1:00}", "?",
                    Vector2.zero, grid.cellSize, () => SelectCodexCard(capturedIndex));
                var label = button.GetComponentInChildren<Text>();
                label.fontSize = 8;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                var labelRect = label.rectTransform;
                labelRect.anchorMin = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(1f, .3f);
                labelRect.offsetMin = new Vector2(3f, 2f);
                labelRect.offsetMax = new Vector2(-3f, -1f);

                var portraitObject = CreateUiObject("Portrait", button.transform, Vector2.zero, Vector2.zero);
                var portraitRect = (RectTransform)portraitObject.transform;
                portraitRect.anchorMin = new Vector2(.08f, .31f);
                portraitRect.anchorMax = new Vector2(.92f, .94f);
                portraitRect.offsetMin = portraitRect.offsetMax = Vector2.zero;
                var portrait = portraitObject.AddComponent<Image>();
                portrait.preserveAspect = true;
                portrait.raycastTarget = false;
                labelRect.SetAsLastSibling();
                codexCardButtons[index] = button;
                codexCardLabels[index] = label;
                codexCardPortraits[index] = portrait;
            }
            codexGridRoot.SetActive(false);
        }

        private void BuildCodexExpandedView()
        {
            codexExpandedBackdrop = CreateUiObject("CodexExpandedBackdrop", panel.transform,
                new Vector2(470f, 260f), Vector2.zero);
            var backdropImage = codexExpandedBackdrop.AddComponent<Image>();
            backdropImage.color = new Color(.01f, .015f, .02f, .9f);
            var backdropButton = codexExpandedBackdrop.AddComponent<Button>();
            backdropButton.targetGraphic = backdropImage;
            backdropButton.onClick.AddListener(HandleCodexExpandedBackdropClicked);

            var cardObject = CreateUiObject("CodexExpandedCard", codexExpandedBackdrop.transform,
                YokaiCodexPresentationModel.EnlargedCardSize, Vector2.zero);
            codexExpandedCardBackground = cardObject.AddComponent<Image>();
            var cardButton = cardObject.AddComponent<Button>();
            cardButton.targetGraphic = codexExpandedCardBackground;
            cardButton.onClick.AddListener(HandleCodexExpandedCardClicked);

            codexExpandedTitle = CreateText(cardObject.transform, "Title", 15, TextAnchor.MiddleCenter,
                new Vector2(176f, 24f), new Vector2(0f, 111f));
            var portraitObject = CreateUiObject("Portrait", cardObject.transform,
                new Vector2(170f, 150f), new Vector2(0f, 20f));
            codexExpandedPortrait = portraitObject.AddComponent<Image>();
            codexExpandedPortrait.preserveAspect = true;
            codexExpandedPortrait.raycastTarget = false;
            codexExpandedFrontText = CreateText(cardObject.transform, "Front", 10, TextAnchor.UpperCenter,
                new Vector2(176f, 54f), new Vector2(0f, -82f));
            codexExpandedBackText = CreateText(cardObject.transform, "Back", 10, TextAnchor.UpperLeft,
                new Vector2(168f, 190f), new Vector2(0f, -2f));
            codexExpandedHintText = CreateText(cardObject.transform, "Hint", 8, TextAnchor.MiddleCenter,
                new Vector2(184f, 16f), new Vector2(0f, -118f));
            codexExpandedHintText.color = new Color(.78f, .83f, .86f, 1f);
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
            // GDD 5 UI/UX PC 조작 정본: 숫자 1~4로 통합 패널의 탭을 직접 선택한다.
            Page target;
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) target = Page.Gathering;
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) target = Page.Crafting;
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) target = Page.Equipment;
            else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) target = Page.Codex;
            else return false;

            if ((shell != null && shell.Screen != GameShellScreen.Gameplay) || Time.timeScale <= 0f) return true;
            TogglePage(target);
            return true;
        }

        private void TogglePage(Page target)
        {
            if (open && page == target)
            {
                if (target == Page.Crafting && showingSmelting)
                {
                    showingSmelting = false;
                    selectedIndex = 0;
                    RebuildFilteredRecipes(NearbyStation());
                    Refresh();
                    ResetDetailsScroll();
                    ResetCraftingListScroll();
                    return;
                }
                SetOpen(false);
                return;
            }

            OpenPage(target);
        }

        private void OpenPage(Page target)
        {
            if (turretRuntime != null && turretRuntime.IsPlacementPreviewActive)
                turretRuntime.CancelPlacementPreview();
            if (target != Page.Codex && codexModel != null) codexModel.TapOutside();
            if (target != Page.Codex && codexExpandedBackdrop != null) codexExpandedBackdrop.SetActive(false);
            page = target;
            storageObjectId = string.Empty;
            showingSmelting = false;
            selectedIndex = 0;
            if (page == Page.Crafting) RebuildFilteredRecipes(NearbyStation());
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

        private void SelectRelative(int delta)
        {
            var count = CurrentEntryCount();
            if (count <= 0) return;
            selectedIndex = (selectedIndex + delta + count) % count;
            message = string.Empty;
            Refresh();
            ResetDetailsScroll();
        }

        private void SelectInventorySlot(int index)
        {
            if (!open || page != Page.Gathering || index < 0 ||
                index >= runtimeServices.PlayerInventory.Slots.Count) return;
            selectedIndex = index;
            message = string.Empty;
            Refresh();
        }

        private int FindFirstEntryForStation(CraftingStation station)
        {
            if (page == Page.Crafting && !showingSmelting)
            {
                var index = filteredRecipes.FindIndex(recipe => recipe != null && recipe.Station == station);
                return index >= 0 ? index : 0;
            }

            if (page == Page.Crafting && showingSmelting)
            {
                var requiredKind = station == CraftingStation.Foundry
                    ? SmeltingStationKind.Foundry
                    : SmeltingStationKind.Furnace;
                var index = smeltingRecipes.FindIndex(definition =>
                    definition != null && definition.StationKind == requiredKind);
                return index >= 0 ? index : 0;
            }

            return 0;
        }

        private void RebuildFilteredRecipes(CraftingStation nearbyStation)
        {
            filteredRecipes.Clear();
            for (var index = 0; index < visibleRecipes.Count; index++)
            {
                var recipe = visibleRecipes[index];
                if (recipe != null && IsRecipeVisibleAtStation(recipe.Station, nearbyStation))
                    filteredRecipes.Add(recipe);
            }
            selectedIndex = filteredRecipes.Count > 0
                ? Mathf.Clamp(selectedIndex, 0, filteredRecipes.Count - 1)
                : 0;
        }

        private void TryPrimaryAction()
        {
            if (!open || runtimeServices?.NapService?.IsNapping == true)
            {
                ShowMessage("냥잠 중에는 제작·제련할 수 없습니다.");
                return;
            }
            switch (page)
            {
                case Page.Gathering: TryPlaceSelectedInventoryItem(); break;
                case Page.Crafting:
                    if (showingSmelting) TrySmeltSelected();
                    else TryCraftSelected();
                    break;
                case Page.Equipment: TryToggleSelectedEquipment(); break;
            }
        }

        private void TryPlaceSelectedInventoryItem()
        {
            var item = CurrentInventoryItem();
            if (item == null || !IsInventoryPlaceable(item)) return;
            if (tilePalette == null) tilePalette = FindAnyObjectByType<MainGameTilePaletteController>();
            if (tilePalette != null && tilePalette.TryBeginPlacement(item.Id))
                SetOpen(false);
            else if (!MainGameTilePaletteController.SupportsPalettePlacement(item.Id) &&
                     turretRuntime != null &&
                     turretRuntime.BeginPlacementPreview(item.Id))
                SetOpen(false);
            else
                ShowMessage("설치 미리보기를 시작할 수 없습니다.");
        }

        private void TryCraftSelected()
        {
            var recipe = CurrentRecipe();
            if (recipe == null) { ShowMessage("표시할 제작법이 없습니다."); return; }
            if (turretRuntime != null && IsProductPlaceableRecipe(recipe) &&
                turretRuntime.GetInventoryCount(recipe.Output.item.Id) > 0)
            {
                if (turretRuntime.BeginPlacementPreview(recipe.Output.item.Id)) SetOpen(false);
                else ShowMessage("설치 미리보기를 시작할 수 없습니다.");
                return;
            }
            if (runtimeServices.CraftingProcess.IsCrafting)
            { ShowMessage("다른 제작이 진행 중입니다."); return; }
            var nearby = NearbyStation();
            if (recipe.Station != CraftingStation.None && recipe.Station != nearby)
            { ShowMessage($"{StationLabel(recipe.Station)} 근처에서 제작해야 합니다."); return; }

            var succeeded = recipe.DurationSeconds > 0f
                ? runtimeServices.CraftingProcess.TryStart(recipe, recipe.Station)
                : runtimeServices.CraftingService.TryCraft(recipe, recipe.Station);
            if (succeeded)
            {
                ShowMessage(recipe.DurationSeconds > 0f
                    ? $"제작 시작: {recipe.Output.item.DisplayName}"
                    : $"제작 완료: {recipe.Output.item.DisplayName} ×{recipe.Output.amount}");
                Debug.Log($"[Nyangbingo] Product crafting accepted: {recipe.Id}, station={recipe.Station}.");
            }
            else ShowMessage("재료 또는 인벤토리 공간이 부족합니다.");
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
            var station = definition.StationKind == SmeltingStationKind.Foundry
                ? runtimeServices.Foundry
                : runtimeServices.Furnace;
            if (station.TryStart(definition))
            {
                ShowMessage($"제련 대기열 추가: {definition.Output.item.DisplayName}");
                Debug.Log($"[Nyangbingo] Product smelting accepted: {definition.Id}, station={definition.StationKind}.");
            }
            else ShowMessage("재료·연료가 부족하거나 제련 대기열이 가득 찼습니다.");
        }

        private void TryCollectOutputs()
        {
            if (!open || page != Page.Crafting || !showingSmelting) return;
            var definition = CurrentSmelting();
            if (definition == null) return;
            var station = definition.StationKind == SmeltingStationKind.Foundry
                ? runtimeServices.Foundry
                : runtimeServices.Furnace;
            var collected = 0;
            while (station.Completed.Count > 0 && station.TryCollect(0)) collected++;
            ShowMessage(collected > 0 ? $"완료품 {collected}묶음 회수" : "회수할 완료품이 없거나 인벤토리가 가득 찼습니다.");
        }

        private void TrySecondaryAction()
        {
            if (page == Page.Crafting && showingSmelting) TryCollectOutputs();
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
            if (!open || page != Page.Equipment || runtimeServices?.ActiveSlot == null ||
                !runtimeServices.ActiveSlot.HasEquippedItem) return;
            if (!runtimeServices.ActiveSlot.Toggle()) return;
            ShowMessage(runtimeServices.ActiveSlot.IsUsingEquippedItem
                ? "활성 슬롯: 장착한 무기·도구"
                : "활성 슬롯: 맨 발톱");
            Refresh();
        }

        private void SetOpen(bool value)
        {
            if (value) openedFrame = Time.frameCount;
            if (open == value)
            {
                if (panel != null) panel.SetActive(value);
                return;
            }
            open = value;
            if (!open) storageObjectId = string.Empty;
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
            if (!string.IsNullOrEmpty(storageObjectId))
            {
                RefreshStorage();
                return;
            }
            RefreshTabButtons();
            switch (page)
            {
                case Page.Gathering: RefreshInventory(); break;
                case Page.Crafting:
                    if (showingSmelting) RefreshSmelting();
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
            var storageMode = !string.IsNullOrEmpty(storageObjectId);
            if (storageModeRoot != null) storageModeRoot.SetActive(storageMode);
            if (storageMode)
            {
                foreach (var tab in tabButtons) if (tab != null) tab.gameObject.SetActive(false);
                previousButton.gameObject.SetActive(false);
                nextButton.gameObject.SetActive(false);
                primaryButton.gameObject.SetActive(false);
                collectButton.gameObject.SetActive(false);
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
            var recipeList = page == Page.Crafting && !showingSmelting;
            var hasListActions = !gathering && !codex;
            previousButton.gameObject.SetActive(hasListActions && !recipeList);
            nextButton.gameObject.SetActive(hasListActions && !recipeList);
            primaryButton.gameObject.SetActive(!codex);
            messageText.gameObject.SetActive(hasListActions);
            collectButton.gameObject.SetActive(false);
            if (inventoryGridRoot != null) inventoryGridRoot.SetActive(gathering);
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
            }
        }

        private void RefreshCrafting()
        {
            collectButton.gameObject.SetActive(false);
            RebuildFilteredRecipes(NearbyStation());
            RefreshCraftingList();
            var recipe = CurrentRecipe();
            if (recipe == null)
            {
                titleText.text = "제작 · 현재 위치에서 해금된 제작법 없음";
                detailsText.text = "손 제작법 또는 근처 제작 시설이 해금한 제작법만 표시됩니다.";
                primaryButton.interactable = false;
                return;
            }
            var readyToPlace = turretRuntime != null && IsProductPlaceableRecipe(recipe) &&
                               turretRuntime.GetInventoryCount(recipe.Output.item.Id) > 0;
            primaryButton.GetComponentInChildren<Text>().text = readyToPlace ? "E · 설치 미리보기" : "E · 제작";
            titleText.text = $"제작 {selectedIndex + 1}/{filteredRecipes.Count} · {recipe.Output.item.DisplayName}";
            var stationOk = recipe.Station == CraftingStation.None || recipe.Station == NearbyStation();
            var canCraft = readyToPlace || (!runtimeServices.CraftingProcess.IsCrafting && stationOk &&
                                            runtimeServices.CraftingService.CanCraft(recipe, recipe.Station));
            primaryButton.interactable = canCraft && runtimeServices.NapService?.IsNapping != true;
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
            if (turretRuntime != null && IsProductPlaceableRecipe(recipe))
                builder.AppendLine($"\n완성품 보유: {turretRuntime.GetInventoryCount(recipe.Output.item.Id)} · " +
                                   (readyToPlace ? "E로 설치 모드 진입" : "제작 완료 후 설치 가능"));
            if (runtimeServices.CraftingProcess.IsCrafting)
                builder.AppendLine($"\n진행 중: {runtimeServices.CraftingProcess.Active.Output.item.DisplayName} " +
                                   $"{runtimeServices.CraftingProcess.RemainingSeconds:0.0}초");
            detailsText.text = builder.ToString();
        }

        private void RefreshStorage()
        {
            titleText.text = "장독 창고 · 클릭한 묶음 전체 이동";
            if (!runtimeServices.JangdokStorage.TryGet(storageObjectId, out var storage))
            {
                SetOpen(false);
                return;
            }
            RefreshTransferLabels(storageLabels, storage.Slots);
            RefreshTransferLabels(storagePlayerLabels, runtimeServices.PlayerInventory.Slots);
        }

        private void RefreshTransferLabels(Text[] labels, IReadOnlyList<InventorySlot> slots)
        {
            for (var index = 0; index < labels.Length; index++)
            {
                var label = labels[index];
                if (label == null) continue;
                var slot = index < slots.Count ? slots[index] : default;
                var item = string.IsNullOrEmpty(slot.itemId) ? null : gameDataCatalog.FindItem(slot.itemId);
                label.text = item == null ? "-" : $"{item.DisplayName}\n×{slot.amount}";
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
                var readyToPlace = turretRuntime != null && IsProductPlaceableRecipe(recipe) &&
                                   turretRuntime.GetInventoryCount(recipe.Output.item.Id) > 0;
                var missing = new List<string>();
                foreach (var ingredient in recipe.Ingredients)
                {
                    var owned = runtimeServices.PlayerInventory.Count(ingredient.item.Id);
                    if (owned < ingredient.amount)
                        missing.Add($"{ingredient.item.DisplayName} {owned}/{ingredient.amount}");
                }

                var lacksMaterials = missing.Count > 0 && !readyToPlace;
                label.text = readyToPlace
                    ? $"{recipe.Output.item.DisplayName}\n완성품 설치 가능"
                    : lacksMaterials
                        ? $"{recipe.Output.item.DisplayName}\n<color=#FF6B6B>{string.Join(" · ", missing)}</color>"
                        : $"{recipe.Output.item.DisplayName}\n제작 가능";
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
            primaryButton.GetComponentInChildren<Text>().text = "E · 제련";
            collectButton.GetComponentInChildren<Text>().text = "완료품 일괄 회수";
            collectButton.gameObject.SetActive(true);
            var definition = CurrentSmelting();
            if (definition == null)
            {
                titleText.text = "제련 · 표시 가능한 제련법 없음";
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
            titleText.text = $"제작 · 제련 {selectedIndex + 1}/{smeltingRecipes.Count} · {definition.Output.item.DisplayName}";
            primaryButton.interactable = stationOk && runtimeServices.NapService?.IsNapping != true;
            collectButton.interactable = station.Completed.Count > 0;
            detailsText.text =
                $"제련소: {StationLabel(requiredStation)} {(stationOk ? "(현재 사용 가능)" : "(근처로 이동 필요)")}\n" +
                $"재료: {definition.Input.item.DisplayName} " +
                $"{runtimeServices.PlayerInventory.Count(definition.Input.item.Id)}/{definition.Input.amount}\n" +
                $"연료: {definition.Fuel.item.DisplayName} " +
                $"{runtimeServices.PlayerInventory.Count(definition.Fuel.item.Id)}/{definition.Fuel.amount}\n" +
                $"결과: {definition.Output.item.DisplayName} ×{definition.Output.amount}\n" +
                $"시간: {definition.DurationSeconds:0.#} 게임초\n\n" +
                $"가동: {(station.IsSmelting ? $"{station.Active.Output.item.DisplayName} {station.RemainingSeconds:0.0}초" : "없음")}\n" +
                $"대기열: {station.Queue.Count}/{station.QueueCapacity - 1} · 완료품: {station.Completed.Count}묶음";
        }

        private RecipeDefinition CurrentRecipe() => filteredRecipes.Count == 0
            ? null
            : filteredRecipes[Mathf.Clamp(selectedIndex, 0, filteredRecipes.Count - 1)];

        public static bool IsProductPlaceableRecipe(RecipeDefinition recipe)
        {
            if (recipe?.Output.item == null || recipe.MvpScope == ItemMvpScope.B) return false;
            return recipe.Output.item.Category == ItemCategory.Placeable ||
                   recipe.Output.item.Category == ItemCategory.Station ||
                   recipe.Type == RecipeType.ColdSource || recipe.Type == RecipeType.Cooling ||
                   recipe.Type == RecipeType.Placeable || recipe.Type == RecipeType.Station ||
                   recipe.Type == RecipeType.Turret ||
                   string.Equals(recipe.Output.item.Id, CoolingSourceRuntime.IceStorageId,
                       StringComparison.Ordinal);
        }

        public static bool IsInventoryItemPlaceable(ItemDefinition item,
            IEnumerable<RecipeDefinition> recipes)
        {
            if (item == null || item.MvpScope == ItemMvpScope.B) return false;
            if (MainGameTilePaletteController.SupportsPalettePlacement(item.Id)) return true;
            if (recipes == null) return false;
            return recipes.Any(recipe => recipe?.Output.item != null &&
                                         recipe.Output.item.Id == item.Id &&
                                         IsProductPlaceableRecipe(recipe));
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
            var canPlace = selectedItem != null && IsInventoryPlaceable(selectedItem) &&
                           inventory.Count(selectedItem.Id) > 0 &&
                           (MainGameTilePaletteController.SupportsPalettePlacement(selectedItem.Id)
                               ? tilePalette != null
                               : turretRuntime != null);
            primaryButton.GetComponentInChildren<Text>().text = canPlace
                ? $"E · {selectedItem.DisplayName} 설치 미리보기"
                : "설치 가능한 소지품을 선택하세요";
            primaryButton.interactable = canPlace && runtimeServices.NapService?.IsNapping != true;
            titleText.text = $"채집 · 소지품 10×5 = {inventory.Capacity}슬롯";
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
                label.text = item == null ? string.Empty : slot.amount.ToString();
                if (icon != null)
                {
                    icon.sprite = item != null ? itemArtCatalog?.FindSprite(item.Id) : null;
                    icon.enabled = icon.sprite != null;
                }
            }
            titleText.text = selectedItem == null
                ? $"소지품 10×5 · {inventory.Capacity}슬롯"
                : $"소지품 10×5 · {selectedItem.DisplayName} ×{inventory.Count(selectedItem.Id)}";
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
            return IsInventoryItemPlaceable(item, visibleRecipes);
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
                titleText.text = $"장비 {selectedIndex + 1}/{CurrentEquipmentEntryCount()} · {activeSlotItem.DisplayName}";
                primaryButton.GetComponentInChildren<Text>().text = activeEquipped ? "E · 해제" : "E · 장착";
                primaryButton.interactable = true;
                detailsText.text =
                    $"부위: 무기·도구 1 · {(activeEquipped ? "장착 중" : "소지품")}" +
                    $"{(activeEquipped ? activeSlot.IsUsingEquippedItem ? " · 활성" : " · 맨 발톱 활성" : string.Empty)}\n" +
                    "Q: 맨 발톱 ↔ 장착물 전환\n" +
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
                titleText.text = "장비 · 보유 장비 없음";
                detailsText.text = BuildEquippedSummary();
                primaryButton.GetComponentInChildren<Text>().text = "장착";
                primaryButton.interactable = false;
                return;
            }
            var equippedSlot = FindEquippedSlot(equipment);
            var equipped = equippedSlot.HasValue;
            var item = gameDataCatalog.FindItem(equipment.Id);
            titleText.text = $"장비 {selectedIndex + 1}/{CurrentEquipmentEntryCount()} · {item?.DisplayName ?? equipment.Id}";
            primaryButton.GetComponentInChildren<Text>().text = equipped ? "E · 해제" : "E · 장착";
            primaryButton.interactable = true;
            detailsText.text =
                $"부위: {EquipmentSlotLabel(equipment.Slot)} · {(equipped ? $"장착 중 ({EquipmentSlotLabel(equippedSlot.Value)})" : "미장착")}\n" +
                $"방어력: {equipment.Defense:+0;-0;0}\n" +
                $"이동: {equipment.MovementBonus:+0%;-0%;0%} · 채굴 치명타: {equipment.MiningCriticalBonus:+0%;-0%;0%}\n" +
                $"체온 상승: {equipment.TemperatureRiseModifier:+0%;-0%;0%} · 화염 피해: {equipment.FireDamageModifier:+0%;-0%;0%}\n" +
                $"시야: {equipment.VisionRadiusBonus:+0.#;-0.#;0} · 이단 점프: {(equipment.GrantsDoubleJump ? "O" : "-")}\n\n" +
                BuildEquippedSummary();
        }

        private void RefreshEquipmentVisual()
        {
            if (equipmentVisualRoot == null) return;
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

        private Sprite EquipmentSlotSprite(int index, bool selected)
        {
            if (gameplayArtCatalog == null) return null;
            switch (index)
            {
                case 0: return selected ? gameplayArtCatalog.ActiveItemSlotSelected : gameplayArtCatalog.ActiveItemSlot;
                case 1: return selected ? gameplayArtCatalog.InventorySlotSelected : gameplayArtCatalog.EquipmentHeadSlot;
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
            titleText.text = "요괴 도감 · 카드 선택 · 4/ESC 닫기";
            detailsText.text = string.Empty;
            if (codexModel == null)
            {
                titleText.text = "도감 · 진행 데이터를 불러올 수 없음";
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
                titleText.text = "도감 · 표시할 항목 없음";
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
            codexExpandedBackText.gameObject.SetActive(codexModel.IsBackVisible);
            codexExpandedFrontText.text = $"{(selected.IsBoss ? "보스" : "요괴")} · 처치 {selected.KillCount}" +
                                          (selected.FirstKillDay > 0
                                              ? $"\n최초 처치 {selected.FirstKillDay}일"
                                              : string.Empty) +
                                          (string.IsNullOrWhiteSpace(selected.AppearanceHint)
                                              ? string.Empty
                                              : $"\n{selected.AppearanceHint}");
            codexExpandedBackText.text = selected.SourceText;
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

        private ItemDefinition CurrentActiveSlotItem() => selectedIndex >= 0 && selectedIndex < activeSlotItems.Count
            ? activeSlotItems[selectedIndex]
            : null;

        private EquipmentDefinition CurrentEquipment() => ownedEquipment.Count == 0 ||
                                                           selectedIndex < activeSlotItems.Count
            ? null
            : ownedEquipment[Mathf.Clamp(selectedIndex - activeSlotItems.Count, 0, ownedEquipment.Count - 1)];

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
                case Page.Crafting: return showingSmelting ? smeltingRecipes.Count : filteredRecipes.Count;
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

        private CraftingStation NearbyStation() => stationSource != null
            ? stationSource.NearbyCraftingStation
            : CraftingStation.None;

#if UNITY_EDITOR
        private void GrantSelectedRequirementsForEditorTest()
        {
            if (page == Page.Crafting && !showingSmelting)
            {
                var recipe = CurrentRecipe();
                if (recipe != null && TryGrantItems(recipe.Ingredients))
                    ShowMessage($"F4 테스트 재료 지급: {recipe.Output.item.DisplayName}");
                else ShowMessage("F4 재료 지급 실패: 인벤토리 공간을 확인하세요.");
                return;
            }
            if (page == Page.Crafting && showingSmelting)
            {
                var definition = CurrentSmelting();
                var requirements = definition == null ? null : new[] { definition.Input, definition.Fuel };
                if (requirements != null && TryGrantItems(requirements))
                    ShowMessage($"F4 테스트 재료·연료 지급: {definition.Output.item.DisplayName}");
                else ShowMessage("F4 제련 재료 지급 실패: 인벤토리 공간을 확인하세요.");
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
                    ShowMessage($"F4 테스트 무기·도구 지급: {activeCandidate.DisplayName}");
                    return;
                }

                var candidate = gameDataCatalog.Equipment
                    .Where(definition => definition != null &&
                                         !runtimeServices.EquipmentCollection.Contains(definition.Id))
                    .OrderBy(definition => definition.Id, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (candidate != null && runtimeServices.EquipmentCollection.TryAdd(candidate))
                    ShowMessage($"F4 테스트 장비 지급: {candidate.Id}");
                else ShowMessage("F4 지급 가능한 새 장비가 없습니다.");
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
            if (page == Page.Crafting && !showingSmelting)
                station = CurrentRecipe()?.Station ?? CraftingStation.None;
            else if (page == Page.Crafting && showingSmelting)
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
                ? $"Shift+F4 테스트 이동: {StationLabel(station)}"
                : "테스트 제작대 위치를 찾지 못했습니다.");
        }
#endif

        private static string DefaultHelpText()
        {
#if UNITY_EDITOR
            return "1~4 탭 · ESC 닫기 · A/D·←/→ 선택 · E 실행 · F4/Shift+F4 테스트";
#else
            return "1~4 탭 · ESC 닫기 · A/D·←/→ 선택 · E 실행";
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

        private static Button CreateButton(Transform parent, string name, string label, Vector2 position,
            Vector2 size, UnityEngine.Events.UnityAction action)
        {
            var buttonObject = CreateUiObject(name, parent, size, position);
            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(.16f, .24f, .34f, 1f);
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            var text = CreateText(buttonObject.transform, "Label", 10, TextAnchor.MiddleCenter, size, Vector2.zero);
            text.text = label;
            text.raycastTarget = false;
            return button;
        }

        private void OnDestroy()
        {
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
            if (turretRuntime != null) turretRuntime.BuildStateChanged -= Refresh;
        }
    }
}
