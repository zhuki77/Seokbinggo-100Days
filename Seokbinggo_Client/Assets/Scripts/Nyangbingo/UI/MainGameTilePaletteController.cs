using System;
using System.Collections.Generic;
using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Nyangbingo.UI
{
    /// <summary>
    /// v29 하단 핫바/타일 팔레트. 인벤토리 앞 8칸(키 1–8)을 고정 슬롯으로 보여 주며,
    /// 빈 칸도 선택 가능하다. 전경 타일·설치물·벽지 배치를 같은 슬롯에서 처리한다.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class MainGameTilePaletteController : MonoBehaviour
    {
        public const bool ProductHudNarrativeTextEnabled = false;

        private sealed class SlotView
        {
            public int SlotIndex;
            public string ItemId;
            public Button Button;
            public Image Icon;
            public Text Amount;
            public Text Shortcut;
        }

        public const float MaxScreenWidthRatio = .5f;
        public const float PaletteLogicalWidth = 240f;
        public const float PaletteLogicalHeight = 34f;
        public const float SlotPixelSize = 27f;
        public const float BottomStatusBaseY = 42f;
        public const float BottomStatusLineHeight = 18f;
        public const string WallpaperItemId = "wallpaper";
        public const KeyCode RangeToggleKey = KeyCode.R;
        public const int ShortcutSlotCount = 8;
        /// <summary>채굴과 동일 — globals <c>player_mining_reach_tiles</c> 기본값.</summary>
        public const float DefaultPlacementReachTiles = 4f;

        private static float placementReachTiles = DefaultPlacementReachTiles;
        /// <summary>전경 블록·설치물 공통 사거리. 런타임에 globals에서 갱신된다.</summary>
        public static float PlacementReachTiles => placementReachTiles;

        private static int escapeConsumedFrame = -1;
        private static bool foregroundPlacementActive;

        [SerializeField] private GameDataCatalog gameDataCatalog;
        [SerializeField] private MainGameBootstrap bootstrap;
        [SerializeField] private MainGameRuntimeServices runtimeServices;
        [SerializeField] private MainGameTurretRuntime placementRuntime;
        [SerializeField] private ItemArtCatalog itemArtCatalog;
        [SerializeField] private GameplayArtCatalog gameplayArtCatalog;

        private readonly List<string> paletteItemIds = new List<string>();
        private readonly List<SlotView> slotViews = new List<SlotView>();
        private readonly List<WorldRangeOverlay> rangeOverlays = new List<WorldRangeOverlay>();
        private readonly List<Vector2> rangePositions = new List<Vector2>();
        [SerializeField] private GameObject paletteRoot;
        [SerializeField] private RectTransform content;
        [SerializeField] private Text rangeToggleStatusText;
        [SerializeField] private Text interactionPromptText;
        private GameShellController shell;
        private Camera worldCamera;
        private Transform playerTransform;
        private MainGameEnvironmentState environmentState;
        private WorldRangeOverlayRenderer rangeOverlayRenderer;
        private SpriteRenderer foregroundPreview;
        private string selectedItemId = string.Empty;
        private int selectedSlotIndex = -1;
        private string foregroundPlacementItemId = string.Empty;
        private Vector3Int foregroundPlacementCell;
        private bool foregroundPlacementValid;
        private bool rangeOverlaysVisible;
        private float rangeToggleStatusUntil;
        private bool productPlacementWasActive;
        private bool initialized;

        public static bool BlocksGameplayInput => foregroundPlacementActive;
        public static bool ConsumedEscapeThisFrame => escapeConsumedFrame == Time.frameCount;
        public bool IsInitialized => initialized;
        public bool IsForegroundPlacementActive => foregroundPreview != null;
        /// <summary>핫바에서 전경 블록·벽지를 고른 동안 좌클릭 채굴/공격을 막는다.</summary>
        public bool ShouldBlockPrimaryForPlacement =>
            IsForegroundPlacementActive ||
            (!string.IsNullOrEmpty(selectedItemId) && SupportsPalettePlacement(selectedItemId));
        public int VisibleSlotCount => ShortcutSlotCount;
        public string SelectedItemId => selectedItemId;
        public int SelectedSlotIndex => selectedSlotIndex;

        public void ConfigureForScene(GameDataCatalog catalog, MainGameBootstrap mainBootstrap,
            MainGameRuntimeServices services, MainGameTurretRuntime productPlacement,
            ItemArtCatalog itemArt, GameplayArtCatalog gameplayArt)
        {
            gameDataCatalog = catalog;
            bootstrap = mainBootstrap;
            runtimeServices = services;
            placementRuntime = productPlacement;
            itemArtCatalog = itemArt;
            gameplayArtCatalog = gameplayArt;
            ResolvePlacementReachFromCatalog();
            if (initialized) RefreshPalette();
        }

        public static bool RequiresDevATileIntegration(string itemId) => false;

        public static bool SupportsPalettePlacement(string itemId) =>
            TileService.SupportsForegroundPlacement(itemId) || IsWallpaper(itemId);

        public static bool IsDirectUseHotbarItem(string itemId) =>
            string.Equals(itemId, PlayerHealthRecoveryService.CatnipItemId, StringComparison.Ordinal);

        public static bool IsHotbarSelectable(ItemDefinition item, IEnumerable<RecipeDefinition> recipes) =>
            item != null &&
            (IsDirectUseHotbarItem(item.Id) ||
             MainGameCraftingUiController.IsInventoryItemPlaceable(item, recipes));

        public static bool ShouldHighlightSlot(
            int slotIndex, int selectedIndex, string selectedItemId) =>
            !string.IsNullOrEmpty(selectedItemId) && slotIndex == selectedIndex;

        public static bool ShouldClearEndedProductSelection(
            bool wasActive, bool isActive, bool foregroundActive, string selectedItemId) =>
            wasActive && !isActive && !foregroundActive &&
            !IsDirectUseHotbarItem(selectedItemId);

        public bool TryBeginPlacement(string itemId)
        {
            if (!initialized || string.IsNullOrEmpty(itemId) ||
                runtimeServices?.PlayerInventory == null ||
                runtimeServices.PlayerInventory.Count(itemId) <= 0) return false;

            var item = gameDataCatalog?.FindItem(itemId);
            if (item == null || item.MvpScope == ItemMvpScope.B) return false;

            selectedItemId = itemId;
            placementRuntime?.CancelPlacementPreview();
            if (SupportsPalettePlacement(itemId))
            {
                BeginForegroundPlacement(itemId);
            }
            else
            {
                if (!MainGameCraftingUiController.IsInventoryItemPlaceable(item, gameDataCatalog.Recipes) ||
                    placementRuntime == null || !placementRuntime.BeginPlacementPreview(itemId))
                {
                    ClearSelectedSlot();
                    RefreshSlotVisuals();
                    return false;
                }
                CancelForegroundPlacement(clearSelection: false);
            }

            if (selectedSlotIndex < 0 || selectedSlotIndex >= paletteItemIds.Count ||
                !string.Equals(paletteItemIds[selectedSlotIndex], itemId, StringComparison.Ordinal))
            {
                for (var index = 0; index < paletteItemIds.Count; index++)
                {
                    if (!string.Equals(paletteItemIds[index], itemId, StringComparison.Ordinal)) continue;
                    selectedSlotIndex = index;
                    break;
                }
            }

            productPlacementWasActive = placementRuntime?.IsPlacementPreviewActive == true;
            RefreshSlotVisuals();
            return true;
        }

        public bool TrySelectPaletteSlot(int slotIndex)
        {
            if (!initialized || slotIndex < 0 || slotIndex >= ShortcutSlotCount) return false;
            RefreshHotbarSlotIds();

            // 이미 선택된 슬롯을 다시 누르면 선택 해제(빈손).
            if (selectedSlotIndex == slotIndex)
            {
                SelectBareHands();
                return true;
            }

            selectedSlotIndex = slotIndex;
            var itemId = paletteItemIds[slotIndex];
            if (string.IsNullOrEmpty(itemId))
            {
                SelectEmptySlot(slotIndex);
                return selectedSlotIndex == slotIndex;
            }

            if (IsDirectUseHotbarItem(itemId))
            {
                SelectDirectUseSlot(slotIndex, itemId);
                return true;
            }

            if (TryBeginPlacement(itemId))
            {
                selectedSlotIndex = slotIndex;
                RefreshSlotVisuals();
                return true;
            }

            var selectedItem = gameDataCatalog?.FindItem(itemId);
            ShowPaletteStatus(selectedItem != null
                ? $"{selectedItem.DisplayName}은(는) 퀵슬롯에서 선택할 수 없습니다."
                : "이 아이템은 퀵슬롯에서 사용할 수 없습니다.");
            // 설치 불가 아이템이거나 배치 실패여도 해당 칸 선택은 유지한다(빈손 배치).
            SelectEmptySlot(slotIndex);
            return selectedSlotIndex == slotIndex;
        }

        public static KeyCode ShortcutKeyForSlot(int slotIndex)
        {
            switch (slotIndex)
            {
                case 0: return KeyCode.Alpha1;
                case 1: return KeyCode.Alpha2;
                case 2: return KeyCode.Alpha3;
                case 3: return KeyCode.Alpha4;
                case 4: return KeyCode.Alpha5;
                case 5: return KeyCode.Alpha6;
                case 6: return KeyCode.Alpha7;
                case 7: return KeyCode.Alpha8;
                default: return KeyCode.None;
            }
        }

        private void Start()
        {
            if (gameDataCatalog == null) gameDataCatalog = FindAnyObjectByType<MainGameBootstrap>()?.GameDataCatalog;
            bootstrap ??= FindAnyObjectByType<MainGameBootstrap>();
            runtimeServices ??= FindAnyObjectByType<MainGameRuntimeServices>();
            placementRuntime ??= FindAnyObjectByType<MainGameTurretRuntime>();
            environmentState = FindAnyObjectByType<MainGameEnvironmentState>();
            playerTransform = FindAnyObjectByType<MainGamePlayerController>()?.transform;
            shell = FindAnyObjectByType<GameShellController>();
            worldCamera = Camera.main;
            if (gameDataCatalog == null || bootstrap == null || runtimeServices == null ||
                !runtimeServices.Initialize() || runtimeServices.PlayerInventory == null)
            {
                Debug.LogError("[Nyangbingo] MainGameTilePaletteController: 팔레트 데이터 배선이 올바르지 않습니다.");
                enabled = false;
                return;
            }

            ResolvePlacementReachFromCatalog();
            BuildPaletteUi();
            var overlayObject = new GameObject("TilePaletteRangeOverlay");
            overlayObject.transform.SetParent(transform, false);
            rangeOverlayRenderer = overlayObject.AddComponent<WorldRangeOverlayRenderer>();
            rangeOverlayRenderer.SetVisible(false);
            runtimeServices.PlayerInventory.Changed += RefreshPalette;
            initialized = true;
            RefreshPalette();
            Debug.Log("[Nyangbingo] 하단 핫바 연결 완료: 인벤 1–8칸·빈 칸 선택·설치물 미리보기·전경 블록 설치.");
        }

        private void Update()
        {
            if (!initialized) return;
            var gameplayVisible = (shell == null || shell.Screen == GameShellScreen.Gameplay) &&
                                  !MainGameCraftingUiController.BlocksGameplayInput;
            if (paletteRoot != null && paletteRoot.activeSelf != gameplayVisible)
                paletteRoot.SetActive(gameplayVisible);

            SynchronizeProductPlacementSelection();
            RefreshBottomStatusStacking();

            if (gameplayVisible && Time.timeScale > 0f && Input.GetKeyDown(RangeToggleKey))
            {
                rangeOverlaysVisible = !rangeOverlaysVisible;
                rangeOverlayRenderer?.SetVisible(rangeOverlaysVisible);
                var visibleRangeCount = RefreshRangeOverlays();
                ShowRangeToggleStatus(visibleRangeCount);
            }
            else if (rangeOverlaysVisible && gameplayVisible)
                RefreshRangeOverlays();
            else if (rangeOverlaysVisible && !gameplayVisible)
                rangeOverlayRenderer?.SetVisible(false);

            if (rangeToggleStatusText != null && rangeToggleStatusText.gameObject.activeSelf &&
                Time.unscaledTime >= rangeToggleStatusUntil)
                rangeToggleStatusText.gameObject.SetActive(false);

            if (gameplayVisible && Time.timeScale > 0f)
            {
                var shortcutSlot = ReadPaletteShortcutSlot();
                if (shortcutSlot >= 0)
                {
                    TrySelectPaletteSlot(shortcutSlot);
                    return;
                }
            }

            var pointerOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (gameplayVisible && !pointerOverUi && Input.GetMouseButtonDown(1) &&
                (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) &&
                TryRemoveWallpaperAtPointer())
                return;

            // 선택은 남아 있는데 미리보기만 끊긴 경우 복구(그렇지 않으면 좌클릭이 채굴로 간다).
            if (gameplayVisible && Time.timeScale > 0f &&
                !IsForegroundPlacementActive &&
                SupportsPalettePlacement(selectedItemId) &&
                runtimeServices?.PlayerInventory != null &&
                runtimeServices.PlayerInventory.Count(selectedItemId) > 0)
                BeginForegroundPlacement(selectedItemId);

            if (!IsForegroundPlacementActive) return;
            if (!gameplayVisible || Time.timeScale <= 0f)
            {
                CancelForegroundPlacement();
                return;
            }

            UpdateForegroundPreview();
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                escapeConsumedFrame = Time.frameCount;
                CancelForegroundPlacement();
                return;
            }

            if (!pointerOverUi && Input.GetMouseButtonDown(1))
            {
                if (TryRemoveHoveredWallpaper()) return;
                CancelForegroundPlacement();
                return;
            }
            if (!pointerOverUi && Input.GetMouseButtonDown(0)) ConfirmForegroundPlacement();
        }

        private void BuildPaletteUi()
        {
            if (paletteRoot == null || content == null || rangeToggleStatusText == null ||
                interactionPromptText == null)
            {
                Debug.LogError("[Nyangbingo] MainGameTilePaletteController: TilePalette 하이어라키가 인스펙터에 배선되지 않았습니다.");
                return;
            }
            interactionPromptText.text = string.Empty;
            placementRuntime?.BindInteractionStatus(interactionPromptText);
        }

        private void RefreshPalette()
        {
            if (content == null || runtimeServices?.PlayerInventory == null) return;
            var nextIds = CollectHotbarSlotItemIds();
            paletteItemIds.Clear();
            paletteItemIds.AddRange(nextIds);
            if (slotViews.Count != ShortcutSlotCount) RebuildSlots();
            else
            {
                for (var index = 0; index < slotViews.Count; index++)
                    slotViews[index].ItemId = paletteItemIds[index];
                RefreshSlotVisuals();
            }

            if (selectedSlotIndex >= 0 && selectedSlotIndex < paletteItemIds.Count &&
                !string.IsNullOrEmpty(selectedItemId) &&
                !string.Equals(paletteItemIds[selectedSlotIndex], selectedItemId, StringComparison.Ordinal))
            {
                ClearSelectedSlot();
                placementRuntime?.CancelPlacementPreview();
                CancelForegroundPlacement(clearSelection: false);
            }

            if (!string.IsNullOrEmpty(foregroundPlacementItemId) &&
                runtimeServices.PlayerInventory.Count(foregroundPlacementItemId) <= 0)
                CancelForegroundPlacement();
        }

        private void RefreshHotbarSlotIds()
        {
            var nextIds = CollectHotbarSlotItemIds();
            if (paletteItemIds.Count != nextIds.Count)
            {
                paletteItemIds.Clear();
                paletteItemIds.AddRange(nextIds);
            }
            else
                for (var index = 0; index < nextIds.Count; index++)
                    paletteItemIds[index] = nextIds[index];
        }

        private List<string> CollectHotbarSlotItemIds()
        {
            var results = new List<string>(ShortcutSlotCount);
            var slots = runtimeServices.PlayerInventory.Slots;
            for (var index = 0; index < ShortcutSlotCount; index++)
            {
                if (index >= slots.Count)
                {
                    results.Add(string.Empty);
                    continue;
                }

                var slot = slots[index];
                results.Add(string.IsNullOrEmpty(slot.itemId) || slot.amount <= 0
                    ? string.Empty
                    : slot.itemId);
            }

            return results;
        }

        private void RebuildSlots()
        {
            slotViews.Clear();
            for (var slotIndex = 0; slotIndex < ShortcutSlotCount; slotIndex++)
            {
                var itemId = slotIndex < paletteItemIds.Count ? paletteItemIds[slotIndex] : string.Empty;
                var capturedIndex = slotIndex;
                var slotTransform = content.Find($"Slot_{slotIndex + 1}");
                if (slotTransform == null)
                {
                    Debug.LogError($"[Nyangbingo] MainGameTilePaletteController: Slot_{slotIndex + 1} 하이어라키가 인스펙터에 배선되지 않았습니다.");
                    continue;
                }
                var button = slotTransform.GetComponent<Button>();
                button.onClick.AddListener(() => TrySelectPaletteSlot(capturedIndex));
                var icon = slotTransform.Find("Icon").GetComponent<Image>();
                var amount = slotTransform.Find("Amount").GetComponent<Text>();
                var shortcutTransform = slotTransform.Find("Shortcut");
                var shortcut = shortcutTransform != null ? shortcutTransform.GetComponent<Text>() : null;

                slotViews.Add(new SlotView
                {
                    SlotIndex = slotIndex,
                    ItemId = itemId,
                    Button = button,
                    Icon = icon,
                    Amount = amount,
                    Shortcut = shortcut
                });
            }
            RefreshSlotVisuals();
        }

        private static int ReadPaletteShortcutSlot()
        {
            for (var index = 0; index < ShortcutSlotCount; index++)
            {
                if (Input.GetKeyDown(ShortcutKeyForSlot(index)) ||
                    Input.GetKeyDown((KeyCode)((int)KeyCode.Keypad1 + index))) return index;
            }
            return -1;
        }

        private void RefreshSlotVisuals()
        {
            for (var index = 0; index < slotViews.Count; index++)
            {
                var view = slotViews[index];
                if (view?.Button == null) continue;
                if (index < paletteItemIds.Count) view.ItemId = paletteItemIds[index];
                var isEmpty = string.IsNullOrEmpty(view.ItemId);
                var selected = ShouldHighlightSlot(
                    view.SlotIndex, selectedSlotIndex, selectedItemId);
                var background = view.Button.targetGraphic as Image;
                if (background != null)
                {
                    background.sprite = selected
                        ? gameplayArtCatalog?.TilePaletteSlotSelected ?? gameplayArtCatalog?.InventorySlotSelected
                        : gameplayArtCatalog?.InventorySlot;
                    background.color = background.sprite != null
                        ? Color.white
                        : selected ? new Color(.24f, .55f, .8f, 1f) : new Color(.12f, .17f, .25f, 1f);
                }

                if (isEmpty)
                {
                    view.Icon.sprite = null;
                    view.Icon.enabled = false;
                    if (view.Amount != null) view.Amount.text = string.Empty;
                    continue;
                }

                view.Icon.sprite = itemArtCatalog?.FindSprite(view.ItemId);
                view.Icon.enabled = view.Icon.sprite != null;
                view.Icon.color = IsWallpaper(view.ItemId) && IsWallpaperCoverageComplete()
                    ? new Color(.45f, .9f, 1f, 1f)
                    : Color.white;
                if (view.Amount != null)
                {
                    view.Amount.alignment = TextAnchor.LowerRight;
                    view.Amount.fontSize = 8;
                    var slots = runtimeServices.PlayerInventory.Slots;
                    view.Amount.text = view.SlotIndex < slots.Count
                        ? slots[view.SlotIndex].amount.ToString()
                        : "0";
                }
            }
        }

        private void SelectEmptySlot(int slotIndex)
        {
            placementRuntime?.CancelPlacementPreview();
            CancelForegroundPlacement(clearSelection: false);
            selectedSlotIndex = slotIndex;
            selectedItemId = string.Empty;
            runtimeServices?.ActiveSlot?.SelectBareHands();
            RefreshSlotVisuals();
        }

        private void SelectDirectUseSlot(int slotIndex, string itemId)
        {
            placementRuntime?.CancelPlacementPreview();
            CancelForegroundPlacement(clearSelection: false);
            selectedSlotIndex = slotIndex;
            selectedItemId = itemId;
            runtimeServices?.ActiveSlot?.SelectBareHands();
            RefreshSlotVisuals();
        }

        public void SelectBareHands()
        {
            placementRuntime?.CancelPlacementPreview();
            CancelForegroundPlacement(clearSelection: false);
            ClearSelectedSlot();
            runtimeServices?.ActiveSlot?.SelectBareHands();
            RefreshSlotVisuals();
        }

        private void SynchronizeProductPlacementSelection()
        {
            var productPlacementActive = placementRuntime?.IsPlacementPreviewActive == true;
            if (ShouldClearEndedProductSelection(
                    productPlacementWasActive, productPlacementActive,
                    IsForegroundPlacementActive, selectedItemId))
            {
                ClearSelectedSlot();
                RefreshSlotVisuals();
            }
            productPlacementWasActive = productPlacementActive;
        }

        private void ClearSelectedSlot()
        {
            selectedItemId = string.Empty;
            selectedSlotIndex = -1;
        }

        private void BeginForegroundPlacement(string itemId)
        {
            CancelForegroundPlacement(clearSelection: false);
            foregroundPlacementItemId = itemId;
            foregroundPlacementActive = true;
            var previewObject = new GameObject($"{itemId}TilePlacementPreview");
            foregroundPreview = previewObject.AddComponent<SpriteRenderer>();
            foregroundPreview.sortingOrder = 31;
            foregroundPreview.sprite = itemArtCatalog?.FindSprite(itemId);
            if (foregroundPreview.sprite == null)
                RuntimePlaceholderVisual.Configure(foregroundPreview, Color.white, 1f, 31);
            UpdateForegroundPreview();
        }

        private void UpdateForegroundPreview()
        {
            if (foregroundPreview == null) return;
            var position = worldCamera != null
                ? worldCamera.ScreenToWorldPoint(Input.mousePosition)
                : Vector3.zero;
            var tileService = bootstrap?.TileService;
            foregroundPlacementCell = tileService != null
                ? tileService.WorldToCell(position)
                : new Vector3Int(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y), 0);
            var cellCenter = tileService != null
                ? tileService.GetCellCenterWorld(foregroundPlacementCell)
                : new Vector3(foregroundPlacementCell.x + .5f, foregroundPlacementCell.y + .5f, 0f);
            // 설치 타일과 동일: 셀 하단 기준 정렬. 중심에 맞추면 하단 피벗 아트가 반칸 위로 뜬다.
            foregroundPreview.transform.position = cellCenter;
            if (tileService != null)
                tileService.AlignSpriteBoundsToCellBase(foregroundPreview, foregroundPlacementCell);
            else
                foregroundPreview.transform.position = AlignSpriteBoundsToCellCenter(
                    foregroundPreview.sprite, cellCenter);
            var withinReach = playerTransform != null &&
                              IsWithinPlacementReach(
                                  playerTransform.position,
                                  tileService?.GetCellWorldBounds(foregroundPlacementCell) ??
                                  new Bounds(cellCenter, new Vector3(1f, 1f, 0f)),
                                  PlacementReachTiles);
            foregroundPlacementValid = tileService != null &&
                                       withinReach &&
                                       (IsWallpaper(foregroundPlacementItemId)
                                           ? bootstrap.Session?.BackgroundPlacement?.CanPlaceWallpaper(
                                               foregroundPlacementCell) == true
                                           : tileService.CanPlaceForeground(foregroundPlacementCell,
                                               foregroundPlacementItemId)) &&
                                       runtimeServices.PlayerInventory.Count(foregroundPlacementItemId) > 0;
            foregroundPreview.color = foregroundPlacementValid
                ? new Color(.35f, 1f, .75f, .65f)
                : new Color(1f, .25f, .25f, .65f);
        }

        private void ConfirmForegroundPlacement()
        {
            if (!foregroundPlacementValid)
            {
                var reachTileService = bootstrap?.TileService;
                var center = reachTileService != null
                    ? reachTileService.GetCellCenterWorld(foregroundPlacementCell)
                    : new Vector3(
                        foregroundPlacementCell.x + .5f,
                        foregroundPlacementCell.y + .5f,
                        0f);
                var withinReach = playerTransform != null &&
                                  IsWithinPlacementReach(
                                      playerTransform.position,
                                      reachTileService?.GetCellWorldBounds(foregroundPlacementCell) ??
                                      new Bounds(center, new Vector3(1f, 1f, 0f)),
                                      PlacementReachTiles);
                ShowPaletteStatus(withinReach
                    ? "붉은 위치에는 블럭을 설치할 수 없습니다."
                    : "설치 거리가 너무 멉니다.");
                return;
            }
            var tileService = bootstrap?.TileService;
            if (tileService == null) return;
            var placed = IsWallpaper(foregroundPlacementItemId)
                ? tileService.TryPlaceWallpaper(foregroundPlacementCell, runtimeServices.PlayerInventory)
                : tileService.TryPlaceForeground(foregroundPlacementCell, foregroundPlacementItemId,
                    runtimeServices.PlayerInventory);
            if (!placed) return;
            RefreshSlotVisuals();
            if (runtimeServices.PlayerInventory.Count(foregroundPlacementItemId) <= 0)
                CancelForegroundPlacement();
            else UpdateForegroundPreview();
        }

        private void CancelForegroundPlacement(bool clearSelection = true)
        {
            if (foregroundPreview != null) Destroy(foregroundPreview.gameObject);
            foregroundPreview = null;
            foregroundPlacementActive = false;
            foregroundPlacementItemId = string.Empty;
            foregroundPlacementValid = false;
            if (clearSelection) ClearSelectedSlot();
            RefreshSlotVisuals();
        }

        private static bool IsWallpaper(string itemId) =>
            string.Equals(itemId, WallpaperItemId, StringComparison.Ordinal);

        private bool TryRemoveHoveredWallpaper()
        {
            if (!IsWallpaper(foregroundPlacementItemId)) return false;
            var placement = bootstrap?.Session?.BackgroundPlacement;
            if (placement == null || !placement.GetBackgroundState(foregroundPlacementCell).HasWallpaper)
                return false;
            if (!placement.TryRemoveWallpaper(foregroundPlacementCell)) return false;
            UpdateForegroundPreview();
            RefreshSlotVisuals();
            return true;
        }

        private bool TryRemoveWallpaperAtPointer()
        {
            if (worldCamera == null) return false;
            var position = worldCamera.ScreenToWorldPoint(Input.mousePosition);
            var cell = bootstrap?.TileService != null
                ? bootstrap.TileService.WorldToCell(position)
                : new Vector3Int(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y), 0);
            var placement = bootstrap?.Session?.BackgroundPlacement;
            if (placement == null || !placement.GetBackgroundState(cell).HasWallpaper) return false;
            if (!placement.TryRemoveWallpaper(cell)) return false;
            RefreshSlotVisuals();
            return true;
        }

        private bool IsWallpaperCoverageComplete()
        {
            var coreCell = bootstrap?.SealSystem?.SealCoreCell;
            return coreCell.HasValue &&
                   bootstrap.Session?.WallpaperCoverage?.IsCoverageComplete(coreCell.Value) == true;
        }

        private int RefreshRangeOverlays()
        {
            if (rangeOverlayRenderer == null) return 0;
            if (!rangeOverlaysVisible)
            {
                rangeOverlayRenderer.Clear();
                return 0;
            }

            rangeOverlays.Clear();
            AppendCircularRanges("lantern", 6f);
            AppendCircularRanges("frost_lantern", 6f);
            AppendCircularRanges("sieve", 4f);
            AppendCircularRanges("iron_sieve", 4f);
            AppendCircularRanges("haetae_statue", 8f);

            var coreCell = bootstrap?.SealSystem?.SealCoreCell;
            if (coreCell.HasValue)
            {
                var rx = ReadGlobalFloat(GlobalKeys.SealWindowRadiusX, 28f);
                var ry = ReadGlobalFloat(GlobalKeys.SealWindowRadiusY, 12f);
                var coreCenter = bootstrap?.TileService != null
                    ? (Vector2)bootstrap.TileService.GetCellCenterWorld(coreCell.Value)
                    : new Vector2(coreCell.Value.x + .5f, coreCell.Value.y + .5f);
                rangeOverlays.Add(new WorldRangeOverlay(
                    coreCenter, rx, ry, WorldRangeShape.AxisAlignedRect));
            }
            rangeOverlayRenderer.SetVisible(true);
            rangeOverlayRenderer.Render(rangeOverlays);
            return rangeOverlays.Count;
        }

        private void ShowRangeToggleStatus(int visibleRangeCount)
        {
            ShowPaletteStatus(!rangeOverlaysVisible
                ? "R  ·  ○"
                : visibleRangeCount > 0
                    ? $"R  ·  ●  ·  {visibleRangeCount}"
                    : "R  ·  ●  ·  0");
        }

        private void ShowPaletteStatus(string message)
        {
            if (rangeToggleStatusText == null || string.IsNullOrWhiteSpace(message)) return;
            rangeToggleStatusText.text = message;
            rangeToggleStatusText.gameObject.SetActive(true);
            rangeToggleStatusUntil = Time.unscaledTime + 2f;
            RefreshBottomStatusStacking();
        }

        public static float ResolveBottomStatusY(bool interactionPromptVisible) =>
            BottomStatusBaseY + (interactionPromptVisible ? BottomStatusLineHeight : 0f);

        private void RefreshBottomStatusStacking()
        {
            if (rangeToggleStatusText == null) return;
            var rect = rangeToggleStatusText.rectTransform;
            var targetY = ResolveBottomStatusY(
                placementRuntime?.IsBottomInteractionPromptVisible == true);
            if (!Mathf.Approximately(rect.anchoredPosition.y, targetY))
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, targetY);
        }

        public static bool IsWithinPlacementReach(
            Vector2 playerPosition, Vector3Int cell, float reachTiles)
        {
            if (reachTiles <= 0f || float.IsNaN(reachTiles) || float.IsInfinity(reachTiles) ||
                float.IsNaN(playerPosition.x) || float.IsInfinity(playerPosition.x) ||
                float.IsNaN(playerPosition.y) || float.IsInfinity(playerPosition.y))
                return false;
            var closest = new Vector2(
                Mathf.Clamp(playerPosition.x, cell.x, cell.x + 1f),
                Mathf.Clamp(playerPosition.y, cell.y, cell.y + 1f));
            return (closest - playerPosition).sqrMagnitude <= reachTiles * reachTiles;
        }

        private void ResolvePlacementReachFromCatalog()
        {
            placementReachTiles = DefaultPlacementReachTiles;
            var definition = gameDataCatalog?.FindGlobal(GlobalKeys.PlayerMiningReachTiles);
            if (definition != null && definition.TryGetFloat(out var configured) &&
                !float.IsNaN(configured) && !float.IsInfinity(configured) && configured > 0f)
            {
                placementReachTiles = configured;
                return;
            }

            Debug.LogWarning(
                "[Nyangbingo] MainGameTilePaletteController: player_mining_reach_tiles missing; " +
                $"using default placement reach {DefaultPlacementReachTiles}.");
        }

        public static bool IsWithinPlacementReach(
            Vector2 playerPosition, Bounds cellBounds, float reachTiles)
        {
            if (reachTiles <= 0f || float.IsNaN(reachTiles) || float.IsInfinity(reachTiles) ||
                float.IsNaN(playerPosition.x) || float.IsInfinity(playerPosition.x) ||
                float.IsNaN(playerPosition.y) || float.IsInfinity(playerPosition.y))
                return false;
            var closest = cellBounds.ClosestPoint(playerPosition);
            return ((Vector2)closest - playerPosition).sqrMagnitude <= reachTiles * reachTiles;
        }

        public static Vector3 AlignSpriteBoundsToCellCenter(Sprite sprite, Vector3 cellCenter) =>
            sprite != null ? cellCenter - sprite.bounds.center : cellCenter;

        public static bool IsWithinPlacementReach(
            Vector2 playerPosition, Vector2 placementPosition, float reachTiles)
        {
            if (reachTiles <= 0f || float.IsNaN(reachTiles) || float.IsInfinity(reachTiles) ||
                float.IsNaN(playerPosition.x) || float.IsInfinity(playerPosition.x) ||
                float.IsNaN(playerPosition.y) || float.IsInfinity(playerPosition.y) ||
                float.IsNaN(placementPosition.x) || float.IsInfinity(placementPosition.x) ||
                float.IsNaN(placementPosition.y) || float.IsInfinity(placementPosition.y))
                return false;
            return (placementPosition - playerPosition).sqrMagnitude <= reachTiles * reachTiles;
        }

        private void AppendCircularRanges(string definitionId, float radius)
        {
            if (environmentState == null) return;
            environmentState.CopyPlacedObjectPositions(definitionId, rangePositions);
            for (var index = 0; index < rangePositions.Count; index++)
                rangeOverlays.Add(new WorldRangeOverlay(rangePositions[index], radius, WorldRangeShape.Circle));
        }

        private float ReadGlobalFloat(string key, float fallback)
        {
            var definition = gameDataCatalog?.FindGlobal(key);
            return definition != null && definition.TryGetFloat(out var value) && value > 0f
                ? value
                : fallback;
        }

        private void OnDestroy()
        {
            if (runtimeServices?.PlayerInventory != null)
                runtimeServices.PlayerInventory.Changed -= RefreshPalette;
            if (foregroundPreview != null) Destroy(foregroundPreview.gameObject);
            foregroundPlacementActive = false;
        }
    }
}
