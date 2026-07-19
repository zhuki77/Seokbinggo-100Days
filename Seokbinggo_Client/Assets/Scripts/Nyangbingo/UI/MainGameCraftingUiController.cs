using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Nyangbingo.Core;
using Nyangbingo.Crafting;
using Nyangbingo.Data;
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
        private enum Page { Inventory, Crafting, Smelting, Equipment }

        [SerializeField] private GameDataCatalog gameDataCatalog;
        [SerializeField] private MainGameRuntimeServices runtimeServices;
        [SerializeField] private MainGameBossSummonUiController stationSource;
        [SerializeField] private GameShellController shell;
        [SerializeField] private MainGameTurretRuntime turretRuntime;

        private readonly List<RecipeDefinition> visibleRecipes = new List<RecipeDefinition>();
        private readonly List<SmeltingDefinition> smeltingRecipes = new List<SmeltingDefinition>();
        private readonly List<EquipmentDefinition> ownedEquipment = new List<EquipmentDefinition>();
        private GameObject panel;
        private Text titleText;
        private Text detailsText;
        private Text messageText;
        private ScrollRect detailsScrollRect;
        private RectTransform detailsViewportRect;
        private RectTransform detailsScrollbarRect;
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
        private static int openControllerCount;
        private static int escapeConsumedFrame = -1;

        public static bool BlocksGameplayInput => openControllerCount > 0;
        public static bool ConsumedEscapeThisFrame => escapeConsumedFrame == Time.frameCount;
        public bool IsOpen => open;
        public int VisibleRecipeCount => visibleRecipes.Count;

        public void ConfigureForScene(GameDataCatalog catalog, MainGameRuntimeServices services,
            MainGameBossSummonUiController craftingStationSource)
        {
            gameDataCatalog = catalog;
            runtimeServices = services;
            stationSource = craftingStationSource;
        }

        private void Start()
        {
            if (shell == null) shell = FindAnyObjectByType<GameShellController>();
            if (turretRuntime == null) turretRuntime = FindAnyObjectByType<MainGameTurretRuntime>();
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
                .OrderBy(recipe => recipe.Id, StringComparer.Ordinal));
            smeltingRecipes.AddRange(gameDataCatalog.Smelting
                .Where(definition => definition != null)
                .OrderBy(definition => definition.StationKind)
                .ThenBy(definition => definition.Id, StringComparer.Ordinal));
            BuildUi();
            runtimeServices.PlayerInventory.Changed += Refresh;
            runtimeServices.EquipmentSystem.Changed += Refresh;
            runtimeServices.EquipmentCollection.Added += HandleEquipmentAdded;
            if (turretRuntime != null) turretRuntime.BuildStateChanged += Refresh;
            initialized = true;
            SetOpen(false);
            Refresh();
            Debug.Log($"[Nyangbingo] MainGame crafting/smelting UI ready " +
                      $"(recipes={visibleRecipes.Count}, hiddenScopeB={hideScopeB}, smelting={smeltingRecipes.Count}).");
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

            if (TryHandlePageHotkey()) return;
            if (!open) return;
            if (page != Page.Inventory)
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) SelectRelative(-1);
                if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) SelectRelative(1);
                if (Input.GetKeyDown(KeyCode.E)) TryPrimaryAction();
#if UNITY_EDITOR
                if (Input.GetKeyDown(KeyCode.F4))
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

        public bool TryOpenForStation(CraftingStation station)
        {
            if (!initialized || station == CraftingStation.None ||
                shell != null && shell.Screen != GameShellScreen.Gameplay || Time.timeScale <= 0f) return false;

            var targetPage = IsSmeltingStation(station) ? Page.Smelting : Page.Crafting;
            OpenPage(targetPage);
            selectedIndex = FindFirstEntryForStation(station);
            Refresh();
            ResetDetailsScroll();
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
            tabButtons[0] = CreateButton(panel.transform, "InventoryTab", "1 · 인벤토리",
                new Vector2(-165f, 94f), new Vector2(108f, 20f), () => TogglePage(Page.Inventory));
            tabButtons[1] = CreateButton(panel.transform, "CraftingTab", "2 · 제작",
                new Vector2(-55f, 94f), new Vector2(108f, 20f), () => TogglePage(Page.Crafting));
            tabButtons[2] = CreateButton(panel.transform, "SmeltingTab", "3 · 제련",
                new Vector2(55f, 94f), new Vector2(108f, 20f), () => TogglePage(Page.Smelting));
            tabButtons[3] = CreateButton(panel.transform, "EquipmentTab", "4 · 장비",
                new Vector2(165f, 94f), new Vector2(108f, 20f), () => TogglePage(Page.Equipment));
            BuildDetailsScrollArea();
            messageText = CreateText(panel.transform, "Message", 9, TextAnchor.MiddleCenter,
                new Vector2(450f, 18f), new Vector2(0f, -68f));

            previousButton = CreateButton(panel.transform, "Previous", "◀ 이전", new Vector2(-165f, -91f),
                new Vector2(90f, 22f), () => SelectRelative(-1));
            nextButton = CreateButton(panel.transform, "Next", "다음 ▶", new Vector2(-70f, -91f),
                new Vector2(90f, 22f), () => SelectRelative(1));
            primaryButton = CreateButton(panel.transform, "Primary", "제작", new Vector2(110f, -91f),
                new Vector2(240f, 22f), TryPrimaryAction);
            collectButton = CreateButton(panel.transform, "Collect", "완료품 일괄 회수", new Vector2(-75f, -118f),
                new Vector2(160f, 20f), TryCollectOutputs);
            CreateButton(panel.transform, "Close", "ESC · 닫기", new Vector2(115f, -118f),
                new Vector2(200f, 20f), () => SetOpen(false));
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

        private bool TryHandlePageHotkey()
        {
            // GDD 5 UI/UX PC 조작 정본: 숫자 1~4로 통합 패널의 탭을 직접 선택한다.
            Page target;
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) target = Page.Inventory;
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) target = Page.Crafting;
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) target = Page.Smelting;
            else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) target = Page.Equipment;
            else return false;

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
            if (turretRuntime != null && turretRuntime.IsPlacementPreviewActive)
                turretRuntime.CancelPlacementPreview();
            page = target;
            selectedIndex = 0;
            message = string.Empty;
            SetOpen(true);
            Refresh();
            ResetDetailsScroll();
        }

        private void ResetDetailsScroll()
        {
            if (detailsScrollRect == null) return;
            Canvas.ForceUpdateCanvases();
            detailsScrollRect.StopMovement();
            detailsScrollRect.verticalNormalizedPosition = 1f;
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

        private int FindFirstEntryForStation(CraftingStation station)
        {
            if (page == Page.Crafting)
            {
                var index = visibleRecipes.FindIndex(recipe => recipe != null && recipe.Station == station);
                return index >= 0 ? index : 0;
            }

            if (page == Page.Smelting)
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

        private void TryPrimaryAction()
        {
            if (!open || runtimeServices?.NapService?.IsNapping == true)
            {
                ShowMessage("냥잠 중에는 제작·제련할 수 없습니다.");
                return;
            }
            switch (page)
            {
                case Page.Crafting: TryCraftSelected(); break;
                case Page.Smelting: TrySmeltSelected(); break;
                case Page.Equipment: TryToggleSelectedEquipment(); break;
            }
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
            if (!open || page != Page.Smelting) return;
            var definition = CurrentSmelting();
            if (definition == null) return;
            var station = definition.StationKind == SmeltingStationKind.Foundry
                ? runtimeServices.Foundry
                : runtimeServices.Furnace;
            var collected = 0;
            while (station.Completed.Count > 0 && station.TryCollect(0)) collected++;
            ShowMessage(collected > 0 ? $"완료품 {collected}묶음 회수" : "회수할 완료품이 없거나 인벤토리가 가득 찼습니다.");
        }

        private void SetOpen(bool value)
        {
            if (open == value)
            {
                if (panel != null) panel.SetActive(value);
                return;
            }
            open = value;
            openControllerCount = Mathf.Max(0, openControllerCount + (open ? 1 : -1));
            if (panel != null) panel.SetActive(open);
            message = string.Empty;
            Refresh();
        }

        private void Refresh()
        {
            if (panel == null || !open) return;
            RefreshPageLayout();
            RefreshTabButtons();
            switch (page)
            {
                case Page.Inventory: RefreshInventory(); break;
                case Page.Crafting: RefreshCrafting(); break;
                case Page.Smelting: RefreshSmelting(); break;
                case Page.Equipment: RefreshEquipment(); break;
            }
            messageText.text = string.IsNullOrEmpty(message)
                ? DefaultHelpText()
                : message;
        }

        private void RefreshPageLayout()
        {
            var inventoryOnly = page == Page.Inventory;
            previousButton.gameObject.SetActive(!inventoryOnly);
            nextButton.gameObject.SetActive(!inventoryOnly);
            primaryButton.gameObject.SetActive(!inventoryOnly);
            messageText.gameObject.SetActive(!inventoryOnly);

            var viewportHeight = inventoryOnly ? 184f : 134f;
            var viewportY = inventoryOnly ? -14f : 11f;
            detailsViewportRect.sizeDelta = new Vector2(438f, viewportHeight);
            detailsViewportRect.anchoredPosition = new Vector2(-5f, viewportY);
            detailsScrollbarRect.sizeDelta = new Vector2(6f, viewportHeight);
            detailsScrollbarRect.anchoredPosition = new Vector2(222f, viewportY);
        }

        private void RefreshCrafting()
        {
            collectButton.gameObject.SetActive(false);
            var recipe = CurrentRecipe();
            if (recipe == null)
            {
                titleText.text = "제작 · 표시 가능한 제작법 없음";
                detailsText.text = string.Empty;
                primaryButton.interactable = false;
                return;
            }
            var readyToPlace = turretRuntime != null && IsProductPlaceableRecipe(recipe) &&
                               turretRuntime.GetInventoryCount(recipe.Output.item.Id) > 0;
            primaryButton.GetComponentInChildren<Text>().text = readyToPlace ? "E · 설치 미리보기" : "E · 제작";
            titleText.text = $"제작 {selectedIndex + 1}/{visibleRecipes.Count} · {recipe.Output.item.DisplayName}";
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

        private void RefreshSmelting()
        {
            primaryButton.GetComponentInChildren<Text>().text = "E · 제련";
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
            titleText.text = $"제련 {selectedIndex + 1}/{smeltingRecipes.Count} · {definition.Output.item.DisplayName}";
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

        private RecipeDefinition CurrentRecipe() => visibleRecipes.Count == 0
            ? null
            : visibleRecipes[Mathf.Clamp(selectedIndex, 0, visibleRecipes.Count - 1)];

        private static bool IsProductPlaceableRecipe(RecipeDefinition recipe)
        {
            if (recipe?.Output.item == null || recipe.MvpScope == ItemMvpScope.B) return false;
            return recipe.Type == RecipeType.ColdSource || recipe.Type == RecipeType.Cooling ||
                   recipe.Type == RecipeType.Placeable || recipe.Type == RecipeType.Station ||
                   recipe.Type == RecipeType.Turret ||
                   string.Equals(recipe.Output.item.Id, CoolingSourceRuntime.IceStorageId,
                       StringComparison.Ordinal);
        }
        private SmeltingDefinition CurrentSmelting() => smeltingRecipes.Count == 0
            ? null
            : smeltingRecipes[Mathf.Clamp(selectedIndex, 0, smeltingRecipes.Count - 1)];

        private void RefreshInventory()
        {
            primaryButton.GetComponentInChildren<Text>().text = "인벤토리";
            primaryButton.interactable = false;
            collectButton.gameObject.SetActive(false);
            titleText.text = "인벤토리 · 12슬롯";
            var builder = new StringBuilder();
            for (var index = 0; index < runtimeServices.PlayerInventory.Slots.Count; index++)
            {
                var slot = runtimeServices.PlayerInventory.Slots[index];
                var item = string.IsNullOrEmpty(slot.itemId) ? null : gameDataCatalog.FindItem(slot.itemId);
                builder.AppendLine(item == null
                    ? $"{index + 1:00}. -"
                    : $"{index + 1:00}. {item.DisplayName} ×{slot.amount}");
            }
            detailsText.text = builder.ToString();
        }

        private void RefreshEquipment()
        {
            RebuildOwnedEquipment();
            collectButton.gameObject.SetActive(false);
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
            titleText.text = $"장비 {selectedIndex + 1}/{ownedEquipment.Count} · {item?.DisplayName ?? equipment.Id}";
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

        private void TryToggleSelectedEquipment()
        {
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
            ownedEquipment.Clear();
            foreach (var id in runtimeServices.EquipmentCollection.Export())
            {
                var definition = gameDataCatalog.FindEquipment(id);
                if (definition != null) ownedEquipment.Add(definition);
            }
            ownedEquipment.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
            if (ownedEquipment.Count > 0) selectedIndex = Mathf.Clamp(selectedIndex, 0, ownedEquipment.Count - 1);
        }

        private EquipmentDefinition CurrentEquipment() => ownedEquipment.Count == 0
            ? null
            : ownedEquipment[Mathf.Clamp(selectedIndex, 0, ownedEquipment.Count - 1)];

        private EquipmentSlot? FindEquippedSlot(EquipmentDefinition definition)
        {
            if (definition == null) return null;
            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
                if (runtimeServices.EquipmentSystem.Get(slot) == definition) return slot;
            return null;
        }

        private string BuildEquippedSummary()
        {
            var builder = new StringBuilder("현재 장착:\n");
            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
            {
                var definition = runtimeServices.EquipmentSystem.Get(slot);
                var item = definition != null ? gameDataCatalog.FindItem(definition.Id) : null;
                builder.AppendLine($"  · {EquipmentSlotLabel(slot)}: {item?.DisplayName ?? "-"}");
            }
            return builder.ToString();
        }

        private int CurrentEntryCount()
        {
            switch (page)
            {
                case Page.Crafting: return visibleRecipes.Count;
                case Page.Smelting: return smeltingRecipes.Count;
                case Page.Equipment:
                    RebuildOwnedEquipment();
                    return ownedEquipment.Count;
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
            if (page == Page.Crafting)
            {
                var recipe = CurrentRecipe();
                if (recipe != null && TryGrantItems(recipe.Ingredients))
                    ShowMessage($"F4 테스트 재료 지급: {recipe.Output.item.DisplayName}");
                else ShowMessage("F4 재료 지급 실패: 인벤토리 공간을 확인하세요.");
                return;
            }
            if (page == Page.Smelting)
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
            if (page == Page.Crafting) station = CurrentRecipe()?.Station ?? CraftingStation.None;
            else if (page == Page.Smelting)
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
                case CraftingStation.Furnace: return "용광로";
                case CraftingStation.IceAnvil: return "얼음 모루";
                case CraftingStation.Foundry: return "무쇠 용광로";
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
            if (turretRuntime != null) turretRuntime.BuildStateChanged -= Refresh;
        }
    }
}
