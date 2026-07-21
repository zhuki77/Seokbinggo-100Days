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
    /// v29 하단 타일 팔레트. 50칸 인벤토리는 보관 전용이며, 보유 중인 설치 가능 항목만
    /// 화면 하단의 동적 가로 목록으로 노출한다. 전경 타일·설치물·벽지 배치를 한 팔레트에서
    /// 처리하고, 벽지는 오른쪽 끝에 고정한다.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class MainGameTilePaletteController : MonoBehaviour
    {
        public const bool ProductHudNarrativeTextEnabled = false;

        private sealed class SlotView
        {
            public string ItemId;
            public Button Button;
            public Image Icon;
            public Text Amount;
        }

        public const float MaxScreenWidthRatio = .5f;
        public const float PaletteLogicalWidth = 240f;
        public const float PaletteLogicalHeight = 34f;
        public const float SlotPixelSize = 27f;
        public const string WallpaperItemId = "wallpaper";
        public const KeyCode RangeToggleKey = KeyCode.R;

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
        private GameObject paletteRoot;
        private RectTransform content;
        private GameShellController shell;
        private Camera worldCamera;
        private MainGameEnvironmentState environmentState;
        private WorldRangeOverlayRenderer rangeOverlayRenderer;
        private Text rangeToggleStatusText;
        private SpriteRenderer foregroundPreview;
        private string selectedItemId = string.Empty;
        private string foregroundPlacementItemId = string.Empty;
        private Vector3Int foregroundPlacementCell;
        private bool foregroundPlacementValid;
        private bool rangeOverlaysVisible;
        private float rangeToggleStatusUntil;
        private bool initialized;

        public static bool BlocksGameplayInput => foregroundPlacementActive;
        public static bool ConsumedEscapeThisFrame => escapeConsumedFrame == Time.frameCount;
        public bool IsInitialized => initialized;
        public bool IsForegroundPlacementActive => foregroundPreview != null;
        public int VisibleSlotCount => paletteItemIds.Count;
        public string SelectedItemId => selectedItemId;

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
            if (initialized) RefreshPalette();
        }

        public static bool RequiresDevATileIntegration(string itemId) => false;

        public static bool SupportsPalettePlacement(string itemId) =>
            TileService.SupportsForegroundPlacement(itemId) || IsWallpaper(itemId);

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
                    selectedItemId = string.Empty;
                    RefreshSlotVisuals();
                    return false;
                }
                CancelForegroundPlacement(clearSelection: false);
            }
            RefreshSlotVisuals();
            return true;
        }

        private void Start()
        {
            if (gameDataCatalog == null) gameDataCatalog = FindAnyObjectByType<MainGameBootstrap>()?.GameDataCatalog;
            bootstrap ??= FindAnyObjectByType<MainGameBootstrap>();
            runtimeServices ??= FindAnyObjectByType<MainGameRuntimeServices>();
            placementRuntime ??= FindAnyObjectByType<MainGameTurretRuntime>();
            environmentState = FindAnyObjectByType<MainGameEnvironmentState>();
            shell = FindAnyObjectByType<GameShellController>();
            worldCamera = Camera.main;
            if (gameDataCatalog == null || bootstrap == null || runtimeServices == null ||
                !runtimeServices.Initialize() || runtimeServices.PlayerInventory == null)
            {
                Debug.LogError("[Nyangbingo] MainGameTilePaletteController: 팔레트 데이터 배선이 올바르지 않습니다.");
                enabled = false;
                return;
            }

            BuildPaletteUi();
            var overlayObject = new GameObject("TilePaletteRangeOverlay");
            overlayObject.transform.SetParent(transform, false);
            rangeOverlayRenderer = overlayObject.AddComponent<WorldRangeOverlayRenderer>();
            rangeOverlayRenderer.SetVisible(false);
            runtimeServices.PlayerInventory.Changed += RefreshPalette;
            initialized = true;
            RefreshPalette();
            Debug.Log("[Nyangbingo] 하단 타일 팔레트 연결 완료: 동적 보유 슬롯·설치물 미리보기·전경 블록 설치.");
        }

        private void Update()
        {
            if (!initialized) return;
            var gameplayVisible = (shell == null || shell.Screen == GameShellScreen.Gameplay) &&
                                  !MainGameCraftingUiController.BlocksGameplayInput;
            if (paletteRoot != null && paletteRoot.activeSelf != gameplayVisible)
                paletteRoot.SetActive(gameplayVisible);

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

            var pointerOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (gameplayVisible && !pointerOverUi && Input.GetMouseButtonDown(1) &&
                (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) &&
                TryRemoveWallpaperAtPointer())
                return;

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
            paletteRoot = new GameObject("TilePalette", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            paletteRoot.transform.SetParent(transform, false);
            var rootRect = (RectTransform)paletteRoot.transform;
            rootRect.anchorMin = rootRect.anchorMax = rootRect.pivot = new Vector2(.5f, 0f);
            rootRect.anchoredPosition = new Vector2(0f, 5f);
            rootRect.sizeDelta = new Vector2(PaletteLogicalWidth, PaletteLogicalHeight);
            var rootImage = paletteRoot.GetComponent<Image>();
            rootImage.color = new Color(.025f, .04f, .065f, .9f);

            var viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewportObject.transform.SetParent(paletteRoot.transform, false);
            var viewport = (RectTransform)viewportObject.transform;
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(3f, 3f);
            viewport.offsetMax = new Vector2(-3f, -3f);

            var contentObject = new GameObject("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup),
                typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewport, false);
            content = (RectTransform)contentObject.transform;
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0f, .5f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, SlotPixelSize);
            var layout = contentObject.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 2f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var fitter = contentObject.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            var scroll = paletteRoot.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = SlotPixelSize;

            var statusObject = new GameObject("RangeToggleStatus", typeof(RectTransform), typeof(Text));
            statusObject.transform.SetParent(transform, false);
            var statusRect = (RectTransform)statusObject.transform;
            statusRect.anchorMin = statusRect.anchorMax = statusRect.pivot = new Vector2(.5f, 0f);
            statusRect.anchoredPosition = new Vector2(0f, 42f);
            statusRect.sizeDelta = new Vector2(PaletteLogicalWidth, 18f);
            rangeToggleStatusText = statusObject.GetComponent<Text>();
            rangeToggleStatusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            rangeToggleStatusText.fontSize = 9;
            rangeToggleStatusText.alignment = TextAnchor.MiddleCenter;
            rangeToggleStatusText.color = Color.white;
            rangeToggleStatusText.raycastTarget = false;
            rangeToggleStatusText.gameObject.SetActive(false);
        }

        private void RefreshPalette()
        {
            if (content == null || runtimeServices?.PlayerInventory == null) return;
            var nextIds = CollectOwnedPaletteItemIds();
            var structureChanged = nextIds.Count != paletteItemIds.Count;
            if (!structureChanged)
                for (var index = 0; index < nextIds.Count; index++)
                    if (!string.Equals(nextIds[index], paletteItemIds[index], StringComparison.Ordinal))
                    {
                        structureChanged = true;
                        break;
                    }

            if (structureChanged)
            {
                paletteItemIds.Clear();
                paletteItemIds.AddRange(nextIds);
                RebuildSlots();
            }
            else RefreshSlotVisuals();

            if (!string.IsNullOrEmpty(foregroundPlacementItemId) &&
                runtimeServices.PlayerInventory.Count(foregroundPlacementItemId) <= 0)
                CancelForegroundPlacement();
        }

        private List<string> CollectOwnedPaletteItemIds()
        {
            var results = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var slot in runtimeServices.PlayerInventory.Slots)
            {
                if (string.IsNullOrEmpty(slot.itemId) || slot.amount <= 0 || !seen.Add(slot.itemId)) continue;
                var item = gameDataCatalog.FindItem(slot.itemId);
                if (item == null || item.MvpScope == ItemMvpScope.B) continue;
                var foreground = SupportsPalettePlacement(item.Id);
                var product = MainGameCraftingUiController.IsInventoryItemPlaceable(item, gameDataCatalog.Recipes);
                if (foreground || product) results.Add(item.Id);
            }
            // 기획 v26: 벽지는 기존 배경 타일 팔레트의 오른쪽 끝 아이콘이다.
            if (results.Remove(WallpaperItemId)) results.Add(WallpaperItemId);
            return results;
        }

        private void RebuildSlots()
        {
            foreach (var view in slotViews)
                if (view?.Button != null) Destroy(view.Button.gameObject);
            slotViews.Clear();
            foreach (var itemId in paletteItemIds)
            {
                var capturedId = itemId;
                var slotObject = new GameObject($"Slot_{itemId}", typeof(RectTransform), typeof(Image), typeof(Button));
                slotObject.transform.SetParent(content, false);
                ((RectTransform)slotObject.transform).sizeDelta = new Vector2(SlotPixelSize, SlotPixelSize);
                var background = slotObject.GetComponent<Image>();
                var button = slotObject.GetComponent<Button>();
                button.targetGraphic = background;
                button.onClick.AddListener(() => SelectPaletteItem(capturedId));

                var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconObject.transform.SetParent(slotObject.transform, false);
                var iconRect = (RectTransform)iconObject.transform;
                iconRect.anchorMin = Vector2.zero;
                iconRect.anchorMax = Vector2.one;
                iconRect.offsetMin = new Vector2(3f, 3f);
                iconRect.offsetMax = new Vector2(-3f, -3f);
                var icon = iconObject.GetComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;

                var amountObject = new GameObject("Amount", typeof(RectTransform), typeof(Text));
                amountObject.transform.SetParent(slotObject.transform, false);
                var amountRect = (RectTransform)amountObject.transform;
                amountRect.anchorMin = Vector2.zero;
                amountRect.anchorMax = Vector2.one;
                amountRect.offsetMin = new Vector2(1f, 0f);
                amountRect.offsetMax = new Vector2(-2f, -1f);
                var amount = amountObject.GetComponent<Text>();
                amount.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                amount.fontSize = 8;
                amount.alignment = TextAnchor.LowerRight;
                amount.color = Color.white;
                amount.raycastTarget = false;
                amount.horizontalOverflow = HorizontalWrapMode.Overflow;

                slotViews.Add(new SlotView { ItemId = itemId, Button = button, Icon = icon, Amount = amount });
            }
            RefreshSlotVisuals();
        }

        private void RefreshSlotVisuals()
        {
            foreach (var view in slotViews)
            {
                if (view?.Button == null) continue;
                var selected = string.Equals(view.ItemId, selectedItemId, StringComparison.Ordinal);
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
                view.Icon.sprite = itemArtCatalog?.FindSprite(view.ItemId);
                view.Icon.enabled = view.Icon.sprite != null;
                view.Icon.color = IsWallpaper(view.ItemId) && IsWallpaperCoverageComplete()
                    ? new Color(.45f, .9f, 1f, 1f)
                    : Color.white;
                view.Amount.text = runtimeServices.PlayerInventory.Count(view.ItemId).ToString();
            }
        }

        private void SelectPaletteItem(string itemId)
        {
            TryBeginPlacement(itemId);
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
            foregroundPlacementCell = new Vector3Int(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y), 0);
            foregroundPreview.transform.position = new Vector3(foregroundPlacementCell.x + .5f,
                foregroundPlacementCell.y + .5f, 0f);
            var tileService = bootstrap?.TileService;
            foregroundPlacementValid = tileService != null &&
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
            if (!foregroundPlacementValid) return;
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
            if (clearSelection) selectedItemId = string.Empty;
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
            var cell = new Vector3Int(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y), 0);
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
                rangeOverlays.Add(new WorldRangeOverlay(
                    new Vector2(coreCell.Value.x + .5f, coreCell.Value.y + .5f),
                    rx, ry, WorldRangeShape.AxisAlignedRect));
            }
            rangeOverlayRenderer.SetVisible(true);
            rangeOverlayRenderer.Render(rangeOverlays);
            return rangeOverlays.Count;
        }

        private void ShowRangeToggleStatus(int visibleRangeCount)
        {
            if (rangeToggleStatusText == null) return;
            rangeToggleStatusText.text = !rangeOverlaysVisible
                ? "R  ·  ○"
                : visibleRangeCount > 0
                    ? $"R  ·  ●  ·  {visibleRangeCount}"
                    : "R  ·  ●  ·  0";
            rangeToggleStatusText.gameObject.SetActive(true);
            rangeToggleStatusUntil = Time.unscaledTime + 2f;
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
