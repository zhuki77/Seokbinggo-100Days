using System.Collections.Generic;
using Nyangbingo.Core;
using Nyangbingo.Combat;
using Nyangbingo.Bosses;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using Nyangbingo.Save;
using Nyangbingo.World;
using UnityEngine;
using UnityEngine.UI;
using PlayerInventory = Nyangbingo.Inventory.Inventory;

namespace Nyangbingo.UI
{
    [DefaultExecutionOrder(-60)]
    public sealed class MainGameHudController : MonoBehaviour
    {
        private const float LegacyHudToLogicalScale = .25f;
        public const int LegacyInventoryBarSlotCount = 12;
        public const bool ProductHudNarrativeTextEnabled = false;
        public const bool ProductBossHealthTextEnabled = false;
        public const float BossHealthBarBelowClockY = -50f;
        public const float BossFleeRollSeconds = .45f;
        public const int DayCounterFontSize = 12;
        public const int DayCounterClockFontSize = 7;
        public const float DayCounterExpandedHeight = 32f;
        public const float DayCounterClockGap = 1f;
        public const float BaekjungDayCounterBorderPixels = 1f;
        public const float SealDiagnosticHoldSeconds = .6f;
        private const float SealLeakMarkerSeconds = 1.4f;
        private const float SealLeakMarkerVisualYOffset = .5f;
        private const float SealDeltaDisplaySeconds = 1.15f;
        [SerializeField] private GameDataCatalog gameDataCatalog;
        [SerializeField] private MainGameBootstrap bootstrap;
        [SerializeField] private MainGameRuntimeServices runtimeServices;
        [SerializeField] private Text temperatureText;
        [SerializeField] private Image temperatureArt;
        [SerializeField] private GameplayArtCatalog gameplayArtCatalog;
        [SerializeField] private EnvironmentArtCatalog environmentArtCatalog;
        [SerializeField] private Text sealText;
        [SerializeField] private Text dayText;
        [SerializeField] private Text clawText;
        [SerializeField] private Health playerHealth;
        [SerializeField] private Text playerHealthText;
        [SerializeField] private BossManager bossManager;
        [SerializeField] private Text bossStatusText;
        [SerializeField] private GameObject craftingProgressPanel;
        [SerializeField] private Text craftingProgressText;
        [SerializeField] private Image craftingProgressFill;
        [SerializeField] private GameObject deathPanel;
        [SerializeField] private MainGamePlayerController playerController;
        [SerializeField] private Text[] inventorySlotTexts = new Text[LegacyInventoryBarSlotCount];
        [SerializeField] private Image[] inventorySlotIcons = new Image[LegacyInventoryBarSlotCount];
        [SerializeField] private ItemArtCatalog itemArtCatalog;
        private PlayerInventory inventory;
        private GameObject bossHealthBarRoot;
        private float bossFleeRollRemaining;
        private readonly List<Image> bossHealthBarFills = new List<Image>();
        private readonly List<float> bossHealthSegmentWidths = new List<float>();
        private readonly List<RectTransform> bossHealthSegmentRects = new List<RectTransform>();
        private Image bossHealthPortrait;
        private Sprite runtimeBossHealthSprite;
        private string runtimeBossHealthSpriteId;
        private GameObject bossEntranceArtRoot;
        private Image bossEntranceArt;
        private float bossEntranceArtRemaining;
        private MainGameSaveCoordinator saveCoordinator;
        private GoalBadgeProgress goalBadgeProgress;
        private GameObject goalBadgeRoot;
        private readonly Image[] goalBadgeBackgrounds = new Image[3];
        private readonly GameObject[] goalBadgeChecks = new GameObject[3];
        private const string BellRopeId = "bell_rope";
        private const string IronBellRopeId = "iron_bell_rope";
        private const float PlayerDamageWarningSeconds = .28f;
        private const float BellWarningDisplaySeconds = 1.5f;
        private const float SaveIndicatorFrameSeconds = .12f;
        private const float StatusAnimationFrameSeconds = .35f;
        private MainGameEnvironmentState environmentState;
        private MainGameEncounterCoordinator encounterCoordinator;
        private UtilityDefinition bellRopeUtility;
        private Camera alertCamera;
        private RectTransform alertOverlayRoot;
        private Image alertOverlayTint;
        private Text alertOverlayText;
        private Image alertDangerIcon;
        private RectTransform alertDirectionMarker;
        private Image alertDirectionIcon;
        private Text alertDirectionText;
        private readonly List<Vector2> bellRopePositions = new List<Vector2>();
        private readonly List<Transform> activeThreats = new List<Transform>();
        private readonly HashSet<Transform> bellTargetsInside = new HashSet<Transform>();
        private readonly HashSet<Transform> nextBellTargetsInside = new HashSet<Transform>();
        private float damageWarningRemaining;
        private float bellWarningRemaining;
        private Vector2 bellWarningTargetPosition;
        private GameObject statusArtRoot;
        private Image playerVitalsArt;
        private Image playerHealthFill;
        private Image playerTemperatureFill;
        private RuntimePixelGlyphPresenter playerHealthGlyphs;
        private RuntimePixelGlyphPresenter playerTemperatureGlyphs;
        private Image tearBalanceArt;
        private Text tearBalanceText;
        private Image fuelGaugeArt;
        private Text fuelGaugeText;
        private Image saveIndicatorArt;
        private SaveManager hudSaveManager;
        private float saveIndicatorRemaining;
        private int lastTearBalance = -1;
        private float tearAnimationRemaining;
        private Vector2 dayTextDefaultPosition;
        private bool hasDayTextDefaultPosition;
        private RectTransform dayCounterScrollRect;
        private Vector2 dayCounterScrollDefaultPosition;
        private RuntimeDayCounterScrollPresenter dayCounterScrollPresenter;
        private RuntimePixelGlyphPresenter dayCounterGlyphs;
        private GameObject baekjungDayCounterBorder;
        private bool baekjungHudActive;
        private bool baekjungHudSuppressedForBoss;
        private Text dayClockText;
        private RuntimePixelGlyphPresenter dayClockGlyphs;
        private Image dayNightClockArt;
        private GameObject nightSpawnLockRoot;
        private Vector2 dayClockDefaultPosition;
        private Vector2 bossStatusDefaultPosition;
        private int bossStatusDefaultFontSize;
        private bool hasBossStatusDefaultLayout;
        private Canvas hudCanvas;
        private float sealDiagnosticHold;
        private bool sealDiagnosticTriggered;
        private LineRenderer sealLeakMarker;
        private Material sealLeakMarkerMaterial;
        private readonly Vector3[] sealLeakMarkerCorners = new Vector3[4];
        private float sealLeakMarkerRemaining;
        private Text sealDeltaText;
        private float sealDeltaRemaining;
        private float lastSealPercent;
        private bool hasLastSealPercent;
        private static MainGameHudController activeHud;

        public int BoundSlotCount => inventorySlotTexts?.Length ?? 0;
        public int BoundIconCount => inventorySlotIcons?.Length ?? 0;
        public ItemArtCatalog BoundItemArtCatalog => itemArtCatalog;
        public bool HasPlayerStatusBindings => playerHealth != null && playerHealthText != null && deathPanel != null;
        public bool HasCraftingProgressBindings => craftingProgressPanel != null && craftingProgressText != null &&
                                                   craftingProgressFill != null;
        public static bool BlocksWorldPrimaryInput => activeHud != null && activeHud.IsPointerOverSealGauge();

        public void ConfigureForScene(GameDataCatalog catalog, MainGameBootstrap mainBootstrap,
            MainGameRuntimeServices services, Text temperature, Text seal, Text day, Text claw, Text healthText,
            Health health, BossManager manager, Text bossText, GameObject playerDeathPanel, Text[] slots,
            Image[] icons, ItemArtCatalog artCatalog, Image temperatureImage, GameplayArtCatalog gameplayArt,
            GameObject craftingPanel, Text craftingText, Image craftingFill,
            EnvironmentArtCatalog environmentArt = null)
        {
            gameDataCatalog = catalog;
            bootstrap = mainBootstrap;
            runtimeServices = services;
            temperatureText = temperature;
            sealText = seal;
            dayText = day;
            clawText = claw;
            playerHealthText = healthText;
            playerHealth = health;
            bossManager = manager;
            bossStatusText = bossText;
            deathPanel = playerDeathPanel;
            inventorySlotTexts = slots;
            inventorySlotIcons = icons;
            itemArtCatalog = artCatalog;
            temperatureArt = temperatureImage;
            gameplayArtCatalog = gameplayArt;
            environmentArtCatalog = environmentArt;
            craftingProgressPanel = craftingPanel;
            craftingProgressText = craftingText;
            craftingProgressFill = craftingFill;
        }

        private void Start()
        {
            activeHud = this;
            if (bootstrap == null || runtimeServices == null || gameDataCatalog == null ||
                !runtimeServices.Initialize())
            {
                Debug.LogError("[Nyangbingo] MainGameHudController: HUD 데이터 배선이 올바르지 않습니다.");
                enabled = false;
                return;
            }
            inventory = runtimeServices.PlayerInventory;
            environmentState = FindAnyObjectByType<MainGameEnvironmentState>();
            encounterCoordinator = FindAnyObjectByType<MainGameEncounterCoordinator>();
            bellRopeUtility = gameDataCatalog.FindUtility(BellRopeId);
            alertCamera = Camera.main;
            HideLegacyInventoryBar();
            playerController ??= FindAnyObjectByType<MainGamePlayerController>();
            hudCanvas = GetComponent<Canvas>() ?? GetComponentInParent<Canvas>();
            if (hudCanvas != null)
            {
                var uiResolution = hudCanvas.GetComponent<MainGameUiResolutionController>() ??
                                   hudCanvas.gameObject.AddComponent<MainGameUiResolutionController>();
                uiResolution.ConfigureForRuntime(hudCanvas);
            }
            var mainCamera = Camera.main;
            if (mainCamera != null)
            {
                var presentation = mainCamera.GetComponent<MainGamePresentationController>() ??
                                   mainCamera.gameObject.AddComponent<MainGamePresentationController>();
                presentation.ConfigureForRuntime(bootstrap.TimeService,
                    mainCamera.GetComponent<MainGameParallaxBackground>());
            }
            var craftingUi = GetComponent<MainGameCraftingUiController>() ??
                              gameObject.AddComponent<MainGameCraftingUiController>();
            craftingUi.ConfigureForScene(gameDataCatalog, runtimeServices,
                FindAnyObjectByType<MainGameBossSummonUiController>(), itemArtCatalog, gameplayArtCatalog);
            var tilePalette = GetComponent<MainGameTilePaletteController>() ??
                              gameObject.AddComponent<MainGameTilePaletteController>();
            tilePalette.ConfigureForScene(gameDataCatalog, bootstrap, runtimeServices,
                FindAnyObjectByType<MainGameTurretRuntime>(), itemArtCatalog, gameplayArtCatalog);
            inventory.Changed += RefreshInventory;
            if (playerHealth != null)
            {
                playerHealth.Damaged += HandlePlayerDamaged;
                playerHealth.Died += HandlePlayerDied;
            }
            GameEvents.OnSealChanged += RefreshStatus;
            GameEvents.OnTilePlaced += HandleSealAffectingPlacement;
            GameEvents.OnPlacedObjectBuilt += HandleSealAffectingPlacement;
            if (deathPanel != null)
            {
                var deathLabel = deathPanel.GetComponentInChildren<Text>(true);
                if (deathLabel != null) deathLabel.text = "사망\n보금자리로 돌아가는 중…";
                deathPanel.SetActive(playerController != null ? playerController.IsDead : playerHealth != null && playerHealth.IsDead);
            }
            BuildBossHealthBar();
            if (dayText != null)
            {
                dayText.verticalOverflow = VerticalWrapMode.Overflow;
                dayText.horizontalOverflow = HorizontalWrapMode.Overflow;
                dayText.alignment = TextAnchor.MiddleCenter;
                dayText.fontSize = DayCounterFontSize;
                dayText.color = new Color32(0x3A, 0x26, 0x30, 0xFF);
                var dayRect = dayText.rectTransform;
                dayRect.localScale = Vector3.one;
                dayRect.sizeDelta = new Vector2(96f, DayCounterExpandedHeight);
                dayTextDefaultPosition = dayText.rectTransform.anchoredPosition;
                hasDayTextDefaultPosition = true;
            }
            BuildDayCounterScroll();
            encounterCoordinator = FindAnyObjectByType<MainGameEncounterCoordinator>();
            baekjungHudActive = encounterCoordinator?.BaekjungScheduler?.IsActive == true;
            baekjungHudSuppressedForBoss = bossManager?.IsBossActive == true;
            GameEvents.OnBaekjungStart += HandleBaekjungStarted;
            GameEvents.OnBaekjungEnd += HandleBaekjungEnded;
            if (bossManager != null)
            {
                bossManager.BossStarted += HandleBossStarted;
                bossManager.BossEnded += HandleBossEnded;
            }
            BuildGoalBadges();
            BuildStatusArtHud();
            BuildSealFeedbackHud();
            lastSealPercent = bootstrap.SealSystem?.SealPercent ?? 0f;
            hasLastSealPercent = bootstrap.SealSystem != null;
            BuildAlertOverlay();
            RefreshInventory();
            RefreshStatus();
            Debug.Log("[Nyangbingo] MainGameHudController: 체온·석빙고 온도·D-100·발톱 티어 HUD와 " +
                      "v29 50슬롯 통합 인벤토리 연결 완료.");
        }

        private void HideLegacyInventoryBar()
        {
            if (inventorySlotTexts == null) return;
            for (var index = 0; index < inventorySlotTexts.Length; index++)
            {
                var label = inventorySlotTexts[index];
                if (label == null) continue;
                var legacyRoot = label.transform.parent?.parent;
                if (legacyRoot != null) legacyRoot.gameObject.SetActive(false);
                break;
            }
        }

        private void LateUpdate()
        {
            damageWarningRemaining = Mathf.Max(0f, damageWarningRemaining - Time.unscaledDeltaTime);
            bellWarningRemaining = Mathf.Max(0f, bellWarningRemaining - Time.unscaledDeltaTime);
            bossEntranceArtRemaining = Mathf.Max(0f, bossEntranceArtRemaining - Time.unscaledDeltaTime);
            bossFleeRollRemaining = Mathf.Max(0f, bossFleeRollRemaining - Time.unscaledDeltaTime);
            saveIndicatorRemaining = Mathf.Max(0f, saveIndicatorRemaining - Time.unscaledDeltaTime);
            tearAnimationRemaining = Mathf.Max(0f, tearAnimationRemaining - Time.unscaledDeltaTime);
            sealLeakMarkerRemaining = Mathf.Max(0f, sealLeakMarkerRemaining - Time.unscaledDeltaTime);
            sealDeltaRemaining = Mathf.Max(0f, sealDeltaRemaining - Time.unscaledDeltaTime);
            UpdateSealDiagnosticInput();
            RefreshSealFeedbackVisuals();
            if (bossEntranceArtRoot != null && bossEntranceArtRemaining <= 0f)
                bossEntranceArtRoot.SetActive(false);
            RefreshBellRopeDetection();
            RefreshAlertOverlay();
            RefreshStatus();
            SynchronizeSealPercentBaseline();
            if (deathPanel != null && playerController != null && deathPanel.activeSelf != playerController.IsDead)
                deathPanel.SetActive(playerController.IsDead);
        }

        private void RefreshStatus()
        {
            if (bootstrap == null || runtimeServices == null) return;
            var displayedTemperature = $"{runtimeServices.PlayerTemperature.Current:0.0}";
            if (playerTemperatureGlyphs != null)
                playerTemperatureGlyphs.SetText(displayedTemperature);
            else if (temperatureText != null)
                temperatureText.text = displayedTemperature;
            RefreshTemperatureArt();
            if (sealText != null)
            {
                sealText.text = string.Empty;
                sealText.gameObject.SetActive(false);
            }
            if (dayText != null)
            {
                dayCounterScrollPresenter?.SetDaysRemaining(bootstrap.TimeService.DaysRemaining);
                var displayedDays = dayCounterScrollPresenter != null
                    ? dayCounterScrollPresenter.DisplayedDaysRemaining
                    : bootstrap.TimeService.DaysRemaining;
                var counterVisible = dayCounterScrollPresenter == null || dayCounterScrollPresenter.IsFullyOpen;
                if (dayCounterGlyphs != null)
                {
                    dayText.text = string.Empty;
                    dayText.enabled = false;
                    dayCounterGlyphs.SetText($"D-{displayedDays}");
                    dayCounterGlyphs.SetVisible(counterVisible);
                }
                else
                {
                    dayText.text = $"D-{displayedDays}";
                    dayText.enabled = counterVisible;
                }
                if (dayClockText != null)
                {
                    var clock = FormatCycleCountdown(bootstrap.TimeService);
                    if (dayClockGlyphs != null)
                    {
                        dayClockText.text = string.Empty;
                        dayClockText.enabled = false;
                        dayClockGlyphs.SetText(clock);
                    }
                    else dayClockText.text = clock;
                }
                RefreshDayNightClockArt();
                RefreshBaekjungDayCounterFeedback();
            }
            if (clawText != null) clawText.text = $"T{ResolveClawTier()}";
            if (playerHealthText != null && playerHealth != null)
            {
                var displayedHealth = $"{playerHealth.Current}/{playerHealth.MaxHealth}";
                if (playerHealthGlyphs != null)
                    playerHealthGlyphs.SetText(displayedHealth);
                else playerHealthText.text = displayedHealth;
            }
            RefreshBossStatus();
            RefreshCraftingProgress();
            RefreshGoalBadges();
            RefreshStatusArtHud();
        }

        private void BuildStatusArtHud()
        {
            if (statusArtRoot != null) return;
            if (temperatureArt != null)
            {
                var sealRect = temperatureArt.rectTransform;
                sealRect.SetParent(transform, false);
                sealRect.anchorMin = sealRect.anchorMax = sealRect.pivot = new Vector2(1f, 1f);
                sealRect.anchoredPosition = new Vector2(-8f, -8f);
                sealRect.sizeDelta = new Vector2(17f, 31f);
                sealRect.localScale = Vector3.one;
                temperatureArt.preserveAspect = true;
                // This gauge owns a long-press gesture. Keeping it raycastable makes the EventSystem
                // consume the primary pointer so the claw cannot attack or mine through the HUD.
                temperatureArt.raycastTarget = true;
            }

            statusArtRoot = new GameObject("PlayerStatusArt", typeof(RectTransform));
            statusArtRoot.transform.SetParent(transform, false);
            var root = (RectTransform)statusArtRoot.transform;
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = new Vector2(6f, -5f);
            root.sizeDelta = new Vector2(60f, 31f);

            playerHealthFill = CreateStatusImage(root, "HealthFill", null,
                new Vector2(14.5f, -2.5f), new Vector2(42f, 2.5f));
            playerHealthFill.rectTransform.pivot = new Vector2(0f, 1f);
            playerTemperatureFill = CreateStatusImage(root, "TemperatureFill", null,
                new Vector2(14.5f, -7.5f), new Vector2(42f, 2.5f));
            playerTemperatureFill.rectTransform.pivot = new Vector2(0f, 1f);
            playerVitalsArt = CreateStatusImage(root, "VitalsFrame", null, Vector2.zero, root.sizeDelta);

            ReparentStatusText(playerHealthText, root, new Vector2(18f, -11f), new Vector2(38f, 8f), 7);
            ReparentStatusText(temperatureText, root, new Vector2(18f, -20f), new Vector2(22f, 8f), 7);
            if (playerHealthText != null && temperatureText != null &&
                gameplayArtCatalog?.ShellNumberGlyphs.Count == RuntimePixelGlyphPresenter.ExpectedGlyphCount)
            {
                playerHealthText.text = string.Empty;
                playerHealthText.enabled = false;
                playerHealthGlyphs = playerHealthText.GetComponent<RuntimePixelGlyphPresenter>() ??
                                     playerHealthText.gameObject.AddComponent<RuntimePixelGlyphPresenter>();
                playerHealthGlyphs.ConfigureForRuntime(gameplayArtCatalog.ShellNumberGlyphs, .45f);

                temperatureText.text = string.Empty;
                temperatureText.enabled = false;
                playerTemperatureGlyphs = temperatureText.GetComponent<RuntimePixelGlyphPresenter>() ??
                                          temperatureText.gameObject.AddComponent<RuntimePixelGlyphPresenter>();
                playerTemperatureGlyphs.ConfigureForRuntime(gameplayArtCatalog.ShellNumberGlyphs, .45f);
            }

            var tearRoot = CreateStatusRoot(transform, "TearBalance", new Vector2(6f, -52f),
                new Vector2(31f, 16f));
            tearBalanceArt = CreateStatusImage(tearRoot, "Icon", null, Vector2.zero, new Vector2(9f, 15f));
            tearBalanceText = CreateStatusText(tearRoot, "Amount", new Vector2(11f, -3f),
                new Vector2(20f, 9f), 6);

            var fuelRoot = CreateStatusRoot(transform, "PortableLanternFuel", new Vector2(40f, -52f),
                new Vector2(30f, 16f));
            fuelGaugeArt = CreateStatusImage(fuelRoot, "Gauge", null, Vector2.zero, new Vector2(5f, 13f));
            fuelGaugeText = CreateStatusText(fuelRoot, "Remaining", new Vector2(7f, -3f),
                new Vector2(23f, 9f), 5);

            var saveRoot = CreateStatusRoot(transform, "SaveIndicator", new Vector2(-8f, -24f),
                new Vector2(16f, 16f), new Vector2(1f, 1f));
            saveIndicatorArt = CreateStatusImage(saveRoot, "Art", null, Vector2.zero, new Vector2(16f, 16f));
            saveIndicatorArt.enabled = false;

            if (goalBadgeRoot != null)
                ((RectTransform)goalBadgeRoot.transform).anchoredPosition = new Vector2(72f, -8f);
            if (clawText != null)
                clawText.rectTransform.anchoredPosition = new Vector2(6f, -38f);

            hudSaveManager = FindAnyObjectByType<SaveManager>();
            if (hudSaveManager != null) hudSaveManager.Saved += HandleSaved;
        }

        private void BuildSealFeedbackHud()
        {
            if (sealDeltaText != null) return;
            var deltaRoot = CreateStatusRoot(transform, "SealDeltaFeedback", new Vector2(-29f, -36f),
                new Vector2(44f, 12f), new Vector2(1f, 1f));
            sealDeltaText = CreateStatusText(deltaRoot, "Delta", Vector2.zero, deltaRoot.sizeDelta, 9);
            sealDeltaText.alignment = TextAnchor.MiddleRight;
            sealDeltaText.fontStyle = FontStyle.Bold;
            sealDeltaText.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, .9f);
            sealDeltaText.enabled = false;
        }

        private void UpdateSealDiagnosticInput()
        {
            if (temperatureArt == null || bootstrap?.SealSystem == null || Time.timeScale <= 0f)
            {
                ResetSealDiagnosticHold();
                return;
            }

            var pointerInside = IsPointerOverSealGauge();
            if (!Input.GetMouseButton(0) || !pointerInside)
            {
                ResetSealDiagnosticHold();
                return;
            }

            if (sealDiagnosticTriggered) return;
            sealDiagnosticHold += Time.unscaledDeltaTime;
            if (sealDiagnosticHold < SealDiagnosticHoldSeconds) return;
            sealDiagnosticTriggered = true;
            ShowRepresentativeSealLeak();
        }

        private bool IsPointerOverSealGauge()
        {
            if (temperatureArt == null || !temperatureArt.isActiveAndEnabled) return false;
            var eventCamera = hudCanvas != null && hudCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? hudCanvas.worldCamera
                : null;
            return RectTransformUtility.RectangleContainsScreenPoint(
                temperatureArt.rectTransform, Input.mousePosition, eventCamera);
        }

        private void ResetSealDiagnosticHold()
        {
            sealDiagnosticHold = 0f;
            sealDiagnosticTriggered = false;
        }

        private void ShowRepresentativeSealLeak()
        {
            if (bootstrap?.SealSystem == null || !bootstrap.SealSystem.TryGetCoreLeakCell(out var cell)) return;
            EnsureSealLeakMarker();
            bootstrap.WorldRenderer?.GetCellWorldCorners(cell, sealLeakMarkerCorners, .04f);
            if (bootstrap.WorldRenderer == null)
            {
                sealLeakMarkerCorners[0] = new Vector3(cell.x + .04f, cell.y + .04f, 0f);
                sealLeakMarkerCorners[1] = new Vector3(cell.x + .96f, cell.y + .04f, 0f);
                sealLeakMarkerCorners[2] = new Vector3(cell.x + .96f, cell.y + .96f, 0f);
                sealLeakMarkerCorners[3] = new Vector3(cell.x + .04f, cell.y + .96f, 0f);
            }
            sealLeakMarker.positionCount = 4;
            for (var index = 0; index < sealLeakMarkerCorners.Length; index++)
                sealLeakMarker.SetPosition(
                    index,
                    sealLeakMarkerCorners[index] + Vector3.up * SealLeakMarkerVisualYOffset);
            sealLeakMarkerRemaining = SealLeakMarkerSeconds;
            sealLeakMarker.enabled = true;
        }

        private void EnsureSealLeakMarker()
        {
            if (sealLeakMarker != null) return;
            var marker = new GameObject("SealLeakDiagnosticMarker");
            sealLeakMarker = marker.AddComponent<LineRenderer>();
            sealLeakMarker.useWorldSpace = true;
            sealLeakMarker.loop = true;
            sealLeakMarker.startWidth = .09f;
            sealLeakMarker.endWidth = .09f;
            sealLeakMarker.numCornerVertices = 2;
            sealLeakMarker.sortingOrder = 120;
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            sealLeakMarkerMaterial = new Material(shader);
            sealLeakMarker.sharedMaterial = sealLeakMarkerMaterial;
        }

        private void RefreshSealFeedbackVisuals()
        {
            if (sealLeakMarker != null)
            {
                sealLeakMarker.enabled = sealLeakMarkerRemaining > 0f;
                if (sealLeakMarker.enabled)
                {
                    var pulse = .55f + .45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 9f));
                    var color = new Color(0.15f, 1f, 1f, pulse);
                    sealLeakMarker.startColor = color;
                    sealLeakMarker.endColor = color;
                }
            }

            if (sealDeltaText == null) return;
            sealDeltaText.enabled = sealDeltaRemaining > 0f;
            if (!sealDeltaText.enabled) return;
            var colorValue = sealDeltaText.color;
            colorValue.a = Mathf.Clamp01(sealDeltaRemaining / .25f);
            sealDeltaText.color = colorValue;
        }

        private void HandleSealAffectingPlacement(Vector3Int _) => RefreshSealDeltaFeedback();

        private void HandleSealAffectingPlacement(string _) => RefreshSealDeltaFeedback();

        private void RefreshSealDeltaFeedback()
        {
            var sealSystem = bootstrap?.SealSystem;
            if (sealSystem == null) return;
            var current = sealSystem.SealPercent;
            if (!hasLastSealPercent)
            {
                lastSealPercent = current;
                hasLastSealPercent = true;
                return;
            }

            var delta = (current - lastSealPercent) * 100f;
            lastSealPercent = current;
            if (Mathf.Abs(delta) < .05f || sealDeltaText == null) return;
            sealDeltaText.text = FormatSealDelta(delta);
            sealDeltaText.color = delta > 0f
                ? new Color(0.3f, 1f, .75f, 1f)
                : new Color(1f, .4f, .4f, 1f);
            sealDeltaRemaining = SealDeltaDisplaySeconds;
        }

        public static string FormatSealDelta(float percentagePoints) =>
            $"{(percentagePoints >= 0f ? "+" : string.Empty)}{percentagePoints:0.0}%";

        private void SynchronizeSealPercentBaseline()
        {
            var sealSystem = bootstrap?.SealSystem;
            if (sealSystem == null) return;
            lastSealPercent = sealSystem.SealPercent;
            hasLastSealPercent = true;
        }

        private static RectTransform CreateStatusRoot(Transform parent, string name, Vector2 position,
            Vector2 size, Vector2? anchor = null)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            var fixedAnchor = anchor ?? new Vector2(0f, 1f);
            rect.anchorMin = rect.anchorMax = rect.pivot = fixedAnchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Image CreateStatusImage(Transform parent, string name, Sprite sprite,
            Vector2 position, Vector2 size)
        {
            var imageObject = new GameObject(name, typeof(RectTransform));
            imageObject.transform.SetParent(parent, false);
            var image = imageObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = sprite != null;
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return image;
        }

        private static Text CreateStatusText(Transform parent, string name, Vector2 position, Vector2 size,
            int fontSize)
        {
            var textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            var text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = Color.white;
            text.raycastTarget = false;
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return text;
        }

        private static void ReparentStatusText(Text text, Transform parent, Vector2 position, Vector2 size,
            int fontSize)
        {
            if (text == null) return;
            text.transform.SetParent(parent, false);
            text.transform.localScale = Vector3.one;
            text.transform.localRotation = Quaternion.identity;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleLeft;
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private void RefreshStatusArtHud()
        {
            if (playerVitalsArt == null || runtimeServices?.PlayerTemperature == null) return;
            var temperatureFrames = gameplayArtCatalog?.PlayerVitalsFrames;
            var temperatureBucket = ResolveTemperatureBucket(runtimeServices.PlayerTemperature.Normalized);
            if (temperatureFrames != null && temperatureFrames.Count >= 12)
            {
                var animationFrame = Mathf.FloorToInt(Time.unscaledTime / StatusAnimationFrameSeconds) & 1;
                playerVitalsArt.sprite = temperatureFrames[temperatureBucket * 2 + animationFrame];
                playerVitalsArt.enabled = playerVitalsArt.sprite != null;
            }
            else playerVitalsArt.enabled = false;

            var healthRatio = playerHealth != null ? CalculateHealthRatio(playerHealth.Current, playerHealth.MaxHealth) : 0f;
            playerHealthFill.color = VitalsTemperatureColor(temperatureBucket);
            playerHealthFill.rectTransform.sizeDelta = new Vector2(42f * healthRatio, 2.5f);
            playerHealthFill.enabled = healthRatio > 0f;

            var temperatureRatio = Mathf.Clamp01(runtimeServices.PlayerTemperature.Normalized);
            playerTemperatureFill.color = VitalsTemperatureColor(temperatureBucket);
            playerTemperatureFill.rectTransform.sizeDelta = new Vector2(42f * temperatureRatio, 2.5f);
            playerTemperatureFill.enabled = temperatureRatio > 0f;

            var tearFrames = gameplayArtCatalog?.YokaiTearBalanceFrames;
            var tearBalance = inventory?.Count(DeathTearPouchRuntime.TearItemId) ?? 0;
            if (lastTearBalance < 0)
                lastTearBalance = tearBalance;
            else if (tearBalance != lastTearBalance)
            {
                lastTearBalance = tearBalance;
                tearAnimationRemaining = (tearFrames?.Count ?? 0) * StatusAnimationFrameSeconds;
            }
            if (tearFrames != null && tearFrames.Count > 0)
            {
                var animationDuration = tearFrames.Count * StatusAnimationFrameSeconds;
                var elapsed = Mathf.Max(0f, animationDuration - tearAnimationRemaining);
                var index = tearAnimationRemaining > 0f
                    ? Mathf.Clamp(Mathf.FloorToInt(elapsed / StatusAnimationFrameSeconds), 0, tearFrames.Count - 1)
                    : 0;
                tearBalanceArt.sprite = tearFrames[index];
                tearBalanceArt.enabled = tearBalanceArt.sprite != null;
            }
            else tearBalanceArt.enabled = false;
            if (tearBalanceText != null)
                tearBalanceText.text = $"x{tearBalance}";

            var fuelFrames = gameplayArtCatalog?.FuelGaugeFrames;
            var showFuel = runtimeServices.ActiveSlot?.EquippedItemId == PortableLanternRuntime.LanternItemId;
            if (fuelGaugeArt != null)
            {
                fuelGaugeArt.gameObject.SetActive(showFuel);
                fuelGaugeText.gameObject.SetActive(showFuel);
                if (showFuel && fuelFrames != null && fuelFrames.Count > 0)
                {
                    var remaining = runtimeServices.PortableLantern.FuelRemainingSeconds;
                    var fuelRatio = Mathf.Clamp01(remaining / PortableLanternRuntime.FuelSecondsPerCoal);
                    var index = ResolveDescendingGaugeFrame(fuelRatio, fuelFrames.Count);
                    fuelGaugeArt.sprite = fuelFrames[index];
                    fuelGaugeArt.enabled = fuelGaugeArt.sprite != null;
                    fuelGaugeText.text = Mathf.CeilToInt(remaining).ToString();
                }
            }

            RefreshSaveIndicatorArt();
        }

        public static int ResolveTemperatureBucket(float normalizedTemperature) =>
            Mathf.Clamp(Mathf.RoundToInt((1f - Mathf.Clamp01(normalizedTemperature)) * 5f), 0, 5);

        public static int ResolveDescendingGaugeFrame(float normalizedValue, int frameCount) => frameCount <= 1
            ? 0
            : Mathf.Clamp(Mathf.RoundToInt((1f - Mathf.Clamp01(normalizedValue)) * (frameCount - 1)),
                0, frameCount - 1);

        private static Color VitalsTemperatureColor(int bucket)
        {
            var colors = new[]
            {
                new Color32(0x82, 0x21, 0x1d, 0xff), new Color32(0xe3, 0x78, 0x40, 0xff),
                new Color32(0xe8, 0xd2, 0x4b, 0xff), new Color32(0xd0, 0xcc, 0x32, 0xff),
                new Color32(0x55, 0xb6, 0x7d, 0xff), new Color32(0x00, 0xbf, 0xa3, 0xff)
            };
            return colors[Mathf.Clamp(bucket, 0, colors.Length - 1)];
        }

        private void HandleSaved(int _) => saveIndicatorRemaining =
            Mathf.Max(SaveIndicatorFrameSeconds, (gameplayArtCatalog?.SaveIndicatorFrames?.Count ?? 0) *
                                                   SaveIndicatorFrameSeconds);

        private void RefreshSaveIndicatorArt()
        {
            if (saveIndicatorArt == null) return;
            var frames = gameplayArtCatalog?.SaveIndicatorFrames;
            if (saveIndicatorRemaining <= 0f || frames == null || frames.Count == 0)
            {
                saveIndicatorArt.enabled = false;
                return;
            }
            var duration = frames.Count * SaveIndicatorFrameSeconds;
            var elapsed = Mathf.Max(0f, duration - saveIndicatorRemaining);
            var index = Mathf.Clamp(Mathf.FloorToInt(elapsed / SaveIndicatorFrameSeconds), 0, frames.Count - 1);
            saveIndicatorArt.sprite = frames[index];
            saveIndicatorArt.enabled = saveIndicatorArt.sprite != null;
        }

        private void BuildGoalBadges()
        {
            if (goalBadgeRoot != null) return;
            saveCoordinator = FindAnyObjectByType<MainGameSaveCoordinator>();
            goalBadgeProgress = saveCoordinator != null ? saveCoordinator.ProgressTracker?.GoalBadges : null;

            goalBadgeRoot = new GameObject("GoalBadges", typeof(RectTransform));
            goalBadgeRoot.transform.SetParent(transform, false);
            var rootRect = (RectTransform)goalBadgeRoot.transform;
            rootRect.anchorMin = rootRect.anchorMax = rootRect.pivot = new Vector2(0f, 1f);
            rootRect.anchoredPosition = ScaleLegacyHud(new Vector2(96f, -72f));
            rootRect.sizeDelta = ScaleLegacyHud(new Vector2(154f, 46f));
            if (clawText != null)
                clawText.rectTransform.anchoredPosition = ScaleLegacyHud(new Vector2(24f, -124f));
            if (playerHealthText != null)
                playerHealthText.rectTransform.anchoredPosition = ScaleLegacyHud(new Vector2(24f, -168f));

            for (var index = 0; index < goalBadgeBackgrounds.Length; index++)
            {
                var badge = new GameObject($"Badge_{index + 1}", typeof(RectTransform));
                badge.transform.SetParent(rootRect, false);
                var rect = (RectTransform)badge.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, .5f);
                rect.anchoredPosition = ScaleLegacyHud(new Vector2(index * 54f, 0f));
                rect.sizeDelta = ScaleLegacyHud(new Vector2(46f, 46f));
                var background = badge.AddComponent<Image>();
                background.raycastTarget = false;
                goalBadgeBackgrounds[index] = background;
                BuildGoalBadgeGlyph(rect, index);
                goalBadgeChecks[index] = BuildGoalBadgeCheck(rect);
            }

            if (goalBadgeProgress != null) goalBadgeProgress.Changed += RefreshGoalBadges;
            RefreshGoalBadges();
        }

        private static void BuildGoalBadgeGlyph(RectTransform parent, int index)
        {
            var ink = new Color(.88f, .92f, .94f, 1f);
            if (index == 0)
            {
                CreateBadgeShape(parent, "Top", ScaleLegacyHud(new Vector2(0f, 6f)),
                    ScaleLegacyHud(new Vector2(28f, 7f)), ink);
                CreateBadgeShape(parent, "LegLeft", ScaleLegacyHud(new Vector2(-9f, -6f)),
                    ScaleLegacyHud(new Vector2(5f, 18f)), ink);
                CreateBadgeShape(parent, "LegRight", ScaleLegacyHud(new Vector2(9f, -6f)),
                    ScaleLegacyHud(new Vector2(5f, 18f)), ink);
                return;
            }
            if (index == 1)
            {
                for (var row = 0; row < 3; row++)
                    for (var column = 0; column < 2; column++)
                        CreateBadgeShape(parent, $"Brick_{row}_{column}",
                            ScaleLegacyHud(new Vector2(
                                (column - .5f) * 15f + (row % 2 == 0 ? 0f : 4f), (row - 1) * 9f)),
                            ScaleLegacyHud(new Vector2(13f, 7f)), ink);
                return;
            }

            CreateBadgeShape(parent, "FurnaceBody", Vector2.zero,
                ScaleLegacyHud(new Vector2(28f, 31f)), ink);
            CreateBadgeShape(parent, "FurnaceOpening", ScaleLegacyHud(new Vector2(0f, -5f)),
                ScaleLegacyHud(new Vector2(13f, 12f)),
                new Color(.08f, .11f, .14f, 1f));
            CreateBadgeShape(parent, "FurnaceFire", ScaleLegacyHud(new Vector2(0f, -4f)),
                ScaleLegacyHud(new Vector2(7f, 7f)),
                new Color(1f, .48f, .14f, 1f));
        }

        private static GameObject BuildGoalBadgeCheck(RectTransform parent)
        {
            var root = new GameObject("Completed", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = ScaleLegacyHud(new Vector2(-2f, 2f));
            rect.sizeDelta = ScaleLegacyHud(new Vector2(20f, 20f));
            var color = new Color(.62f, 1f, .72f, 1f);
            var shortBar = CreateBadgeShape(rect, "Short", ScaleLegacyHud(new Vector2(-4f, -1f)),
                ScaleLegacyHud(new Vector2(4f, 11f)), color);
            shortBar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -42f);
            var longBar = CreateBadgeShape(rect, "Long", ScaleLegacyHud(new Vector2(3f, 1f)),
                ScaleLegacyHud(new Vector2(4f, 17f)), color);
            longBar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 42f);
            return root;
        }

        private static Image CreateBadgeShape(Transform parent, string name, Vector2 position, Vector2 size,
            Color color)
        {
            var shape = new GameObject(name, typeof(RectTransform));
            shape.transform.SetParent(parent, false);
            var image = shape.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return image;
        }

        private static Vector2 ScaleLegacyHud(Vector2 value) => value * LegacyHudToLogicalScale;

        private void BuildAlertOverlay()
        {
            if (alertOverlayRoot != null) return;
            var root = new GameObject("PriorityAlertOverlay", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            alertOverlayRoot = (RectTransform)root.transform;
            alertOverlayRoot.anchorMin = Vector2.zero;
            alertOverlayRoot.anchorMax = Vector2.one;
            alertOverlayRoot.offsetMin = Vector2.zero;
            alertOverlayRoot.offsetMax = Vector2.zero;
            alertOverlayRoot.SetAsLastSibling();

            alertOverlayTint = root.AddComponent<Image>();
            alertOverlayTint.raycastTarget = false;

            var labelObject = new GameObject("AlertText", typeof(RectTransform));
            labelObject.transform.SetParent(alertOverlayRoot, false);
            alertOverlayText = labelObject.AddComponent<Text>();
            alertOverlayText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            alertOverlayText.fontSize = 12;
            alertOverlayText.fontStyle = FontStyle.Bold;
            alertOverlayText.alignment = TextAnchor.MiddleCenter;
            alertOverlayText.raycastTarget = false;
            var labelRect = alertOverlayText.rectTransform;
            labelRect.anchorMin = labelRect.anchorMax = labelRect.pivot = new Vector2(.5f, 1f);
            labelRect.anchoredPosition = new Vector2(0f, -38f);
            labelRect.sizeDelta = new Vector2(280f, 24f);

            var dangerObject = new GameObject("BellRopeDangerIcon", typeof(RectTransform));
            dangerObject.transform.SetParent(alertOverlayRoot, false);
            alertDangerIcon = dangerObject.AddComponent<Image>();
            alertDangerIcon.sprite = gameplayArtCatalog?.DangerIcon;
            alertDangerIcon.preserveAspect = true;
            alertDangerIcon.raycastTarget = false;
            var dangerRect = alertDangerIcon.rectTransform;
            dangerRect.anchorMin = dangerRect.anchorMax = dangerRect.pivot = new Vector2(.5f, 1f);
            dangerRect.anchoredPosition = new Vector2(-112f, -38f);
            dangerRect.sizeDelta = new Vector2(16f, 16f);

            var markerObject = new GameObject("OffscreenThreatDirection", typeof(RectTransform));
            markerObject.transform.SetParent(alertOverlayRoot, false);
            alertDirectionMarker = (RectTransform)markerObject.transform;
            alertDirectionMarker.anchorMin = alertDirectionMarker.anchorMax = alertDirectionMarker.pivot =
                new Vector2(.5f, .5f);
            alertDirectionMarker.sizeDelta = new Vector2(18f, 18f);
            alertDirectionIcon = markerObject.AddComponent<Image>();
            alertDirectionIcon.sprite = gameplayArtCatalog?.DangerIcon;
            alertDirectionIcon.preserveAspect = true;
            alertDirectionIcon.raycastTarget = false;

            var fallbackObject = new GameObject("DirectionFallback", typeof(RectTransform));
            fallbackObject.transform.SetParent(alertDirectionMarker, false);
            alertDirectionText = fallbackObject.AddComponent<Text>();
            alertDirectionText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            alertDirectionText.fontSize = 16;
            alertDirectionText.fontStyle = FontStyle.Bold;
            alertDirectionText.alignment = TextAnchor.MiddleCenter;
            alertDirectionText.color = new Color(1f, .73f, .18f, 1f);
            alertDirectionText.raycastTarget = false;
            var fallbackRect = alertDirectionText.rectTransform;
            fallbackRect.anchorMin = Vector2.zero;
            fallbackRect.anchorMax = Vector2.one;
            fallbackRect.offsetMin = Vector2.zero;
            fallbackRect.offsetMax = Vector2.zero;
            root.SetActive(false);
        }

        private void RefreshBellRopeDetection()
        {
            if (environmentState == null || encounterCoordinator == null || bellRopeUtility == null ||
                runtimeServices?.UtilityService == null) return;

            environmentState.CopyPlacedObjectPositions(BellRopeId, bellRopePositions);
            environmentState.CopyPlacedObjectPositions(IronBellRopeId, bellRopePositions, append: true);
            encounterCoordinator.CopyActiveThreatTransforms(activeThreats);
            nextBellTargetsInside.Clear();
            var radius = Mathf.Max(0f, bellRopeUtility.Value);
            var radiusSquared = radius * radius;
            foreach (var threat in activeThreats)
            {
                if (threat == null) continue;
                var threatPosition = (Vector2)threat.position;
                var inside = false;
                foreach (var ropePosition in bellRopePositions)
                {
                    if ((ropePosition - threatPosition).sqrMagnitude > radiusSquared) continue;
                    inside = true;
                    break;
                }
                if (!inside) continue;
                nextBellTargetsInside.Add(threat);
                if (!bellTargetsInside.Contains(threat) &&
                    runtimeServices.UtilityService.TryTriggerInstalledBellRope(bellRopeUtility))
                {
                    bellWarningTargetPosition = threatPosition;
                    bellWarningRemaining = BellWarningDisplaySeconds;
                    Debug.Log($"[Nyangbingo] Bell-rope alarm: threat={threat.name}, radius={radius:0.#}, " +
                              $"cooldown={bellRopeUtility.CooldownSeconds:0.#}.");
                }
            }
            bellTargetsInside.Clear();
            bellTargetsInside.UnionWith(nextBellTargetsInside);
        }

        private void RefreshAlertOverlay()
        {
            if (alertOverlayRoot == null) return;
            if (damageWarningRemaining > 0f)
            {
                SetAlertOverlay(new Color(.72f, .03f, .02f, .2f), string.Empty,
                    new Color(1f, .75f, .72f), false);
                return;
            }
            if (bellWarningRemaining > 0f)
            {
                SetAlertOverlay(new Color(.72f, .34f, .02f, .1f), string.Empty,
                    new Color(1f, .82f, .28f), true);
                return;
            }
            if (runtimeServices?.NapService?.IsNapping == true)
            {
                SetAlertOverlay(new Color(.025f, .045f, .1f, .18f), string.Empty,
                    new Color(.65f, .75f, .95f), false);
                return;
            }
            alertOverlayRoot.gameObject.SetActive(false);
        }

        private void SetAlertOverlay(Color tint, string message, Color textColor, bool showThreatDirection)
        {
            alertOverlayRoot.gameObject.SetActive(true);
            alertOverlayRoot.SetAsLastSibling();
            alertOverlayTint.color = tint;
            alertOverlayText.text = string.Empty;
            alertOverlayText.color = textColor;
            var hasDangerArt = gameplayArtCatalog?.DangerIcon != null;
            alertDangerIcon.gameObject.SetActive(showThreatDirection && hasDangerArt);
            if (!showThreatDirection || alertCamera == null)
            {
                alertDirectionMarker.gameObject.SetActive(false);
                return;
            }

            var viewport = alertCamera.WorldToViewportPoint(bellWarningTargetPosition);
            if (IsViewportPointVisible(viewport))
            {
                alertDirectionMarker.gameObject.SetActive(false);
                return;
            }
            var edge = CalculateEdgeViewportPosition(viewport);
            alertDirectionMarker.gameObject.SetActive(true);
            alertDirectionMarker.anchoredPosition = new Vector2(
                (edge.x - .5f) * alertOverlayRoot.rect.width,
                (edge.y - .5f) * alertOverlayRoot.rect.height);
            alertDirectionIcon.gameObject.SetActive(hasDangerArt);
            alertDirectionText.gameObject.SetActive(!hasDangerArt);
            alertDirectionText.text = DirectionGlyph(edge - new Vector2(.5f, .5f));
        }

        public static bool IsViewportPointVisible(Vector3 viewport) =>
            viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;

        public static Vector2 CalculateEdgeViewportPosition(Vector3 viewport, float margin = .06f)
        {
            margin = Mathf.Clamp(margin, 0f, .49f);
            var direction = new Vector2(viewport.x - .5f, viewport.y - .5f);
            if (viewport.z < 0f) direction = -direction;
            if (direction.sqrMagnitude <= .000001f) direction = Vector2.up;
            var extent = .5f - margin;
            var scale = extent / Mathf.Max(Mathf.Abs(direction.x), Mathf.Abs(direction.y));
            return new Vector2(.5f, .5f) + direction * scale;
        }

        public static string DirectionGlyph(Vector2 direction)
        {
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y)) return direction.x < 0f ? "◀" : "▶";
            return direction.y < 0f ? "▼" : "▲";
        }

        private void RefreshGoalBadges()
        {
            if (goalBadgeRoot == null) return;
            if (goalBadgeProgress == null)
            {
                goalBadgeRoot.SetActive(false);
                return;
            }

            goalBadgeRoot.SetActive(goalBadgeProgress.IsVisible);
            if (!goalBadgeProgress.IsVisible) return;
            var completed = new[]
            {
                goalBadgeProgress.WorkbenchCrafted,
                goalBadgeProgress.InsulationWallPlaced,
                goalBadgeProgress.FurnaceBuilt
            };
            for (var index = 0; index < completed.Length; index++)
            {
                if (goalBadgeBackgrounds[index] != null)
                    goalBadgeBackgrounds[index].color = completed[index]
                        ? new Color(.08f, .32f, .23f, .94f)
                        : new Color(.08f, .12f, .16f, .88f);
                if (goalBadgeChecks[index] != null) goalBadgeChecks[index].SetActive(completed[index]);
            }
        }

        private void RefreshCraftingProgress()
        {
            if (craftingProgressPanel == null || craftingProgressText == null || craftingProgressFill == null) return;
            var process = runtimeServices?.CraftingProcess;
            var recipe = process?.Active;
            var active = process?.IsCrafting == true && recipe != null;
            craftingProgressPanel.SetActive(active);
            if (!active) return;

            var duration = Mathf.Max(.0001f, recipe.DurationSeconds);
            var remaining = Mathf.Clamp(process.RemainingSeconds, 0f, duration);
            var completion = Mathf.Clamp01(1f - remaining / duration);
            ResizeCraftingProgressFill(completion);
            craftingProgressText.text = remaining <= .0001f
                ? "!"
                : $"{remaining:0.0}";
            craftingProgressText.color = remaining <= .0001f
                ? new Color(1f, .4f, .35f, 1f)
                : Color.white;
        }

        private void ResizeCraftingProgressFill(float completion)
        {
            var fillRect = craftingProgressFill.rectTransform;
            var trackRect = fillRect.parent as RectTransform;
            if (trackRect == null) return;
            craftingProgressFill.type = Image.Type.Simple;
            fillRect.anchorMin = fillRect.anchorMax = fillRect.pivot = new Vector2(0f, .5f);
            fillRect.anchoredPosition = new Vector2(2f, 0f);
            fillRect.sizeDelta = new Vector2(
                Mathf.Max(0f, (trackRect.rect.width - 4f) * completion),
                Mathf.Max(0f, trackRect.rect.height - 4f));
        }

        private void RefreshTemperatureArt()
        {
            if (temperatureArt == null) return;
            var frames = gameplayArtCatalog?.TemperatureFrames;
            if (frames == null || frames.Count == 0)
            {
                temperatureArt.enabled = false;
                return;
            }

            var cooling = Mathf.Clamp01(runtimeServices.PlayerTemperature.EffectiveCoolingPercent / 100f);
            var index = Mathf.RoundToInt((1f - cooling) * (frames.Count - 1));
            temperatureArt.sprite = frames[Mathf.Clamp(index, 0, frames.Count - 1)];
            temperatureArt.enabled = temperatureArt.sprite != null;
        }

        private void RefreshBossStatus()
        {
            if (bossStatusText == null) return;
            var definition = bossManager != null ? bossManager.ActiveDefinition : null;
            var health = bossManager != null ? bossManager.ActiveHealth : null;
            if (definition == null || health == null)
            {
                if (bossHealthBarRoot != null)
                {
                    if (bossFleeRollRemaining > 0f && bossHealthBarRoot.activeSelf)
                    {
                        var rollScale = CalculateBossFleeRollScale(
                            bossFleeRollRemaining, BossFleeRollSeconds);
                        bossHealthBarRoot.transform.localScale = new Vector3(rollScale, 1f, 1f);
                    }
                    else
                    {
                        bossHealthBarRoot.transform.localScale = Vector3.one;
                        bossHealthBarRoot.SetActive(false);
                    }
                }
                RestoreDayCounterPosition();
#if UNITY_EDITOR
                RestoreBossStatusLayout();
                bossStatusText.text = string.Empty;
#else
                RestoreBossStatusLayout();
                bossStatusText.text = string.Empty;
#endif
                return;
            }

            RestoreBossStatusLayout();
            if (bossHealthBarRoot != null)
            {
                bossFleeRollRemaining = 0f;
                bossHealthBarRoot.transform.localScale = Vector3.one;
                bossHealthBarRoot.SetActive(true);
            }
            RestoreDayCounterPosition();
            if (bossHealthPortrait != null)
            {
                bossHealthPortrait.sprite = ResolveBossHealthArt(definition.Id);
                bossHealthPortrait.enabled = bossHealthPortrait.sprite != null;
            }
            ConfigureBossHealthVerticalLayout(definition.Id);
            ResizeBossHealthBar(CalculateHealthRatio(health.Current, health.MaxHealth));

            // The illustrated segmented bar communicates health without a separate number label.
            bossStatusText.text = string.Empty;
#if UNITY_EDITOR
            // Test controls are listed in the F5 debug shortcut popup.
#endif
        }

        public static float CalculateHealthRatio(int current, int maximum) =>
            maximum <= 0 ? 0f : Mathf.Clamp01((float)current / maximum);

        public static float CalculateBossFleeRollScale(float remainingSeconds, float durationSeconds) =>
            durationSeconds <= 0f ? 0f : Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01(remainingSeconds / durationSeconds));

        public static string FormatCycleCountdown(DayNightService timeService)
        {
            if (timeService == null) return string.Empty;
            return FormatRemainingTime(timeService.SecondsUntilNextTransition);
        }

        public static int ResolveDayNightClockFrameIndex(float timeOfDaySeconds,
            float cycleLengthSeconds, int frameCount)
        {
            if (frameCount <= 0 || cycleLengthSeconds <= 0f || float.IsNaN(timeOfDaySeconds) ||
                float.IsInfinity(timeOfDaySeconds)) return -1;
            var normalized = Mathf.Repeat(timeOfDaySeconds, cycleLengthSeconds) / cycleLengthSeconds;
            var chronologicalIndex = Mathf.Clamp(Mathf.FloorToInt(normalized * frameCount), 0, frameCount - 1);
            // Delivered clock sprites are authored from the end of the day toward the start.
            return frameCount - 1 - chronologicalIndex;
        }

        public static bool ShouldShowNightSpawnLock(bool isNight, bool bossActive, bool baekjungActive) =>
            isNight && (bossActive || baekjungActive);

        public static string FormatRemainingTime(float seconds)
        {
            var totalSeconds = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
        }

        private void RestoreDayCounterPosition()
        {
            if (dayText == null || !hasDayTextDefaultPosition) return;
            dayText.rectTransform.anchoredPosition = dayTextDefaultPosition;
            if (dayCounterScrollRect != null)
                dayCounterScrollRect.anchoredPosition = dayCounterScrollDefaultPosition;
            if (dayClockText != null) dayClockText.rectTransform.anchoredPosition = dayClockDefaultPosition;
        }

        private void BuildDayCounterScroll()
        {
            if (dayText == null || dayCounterScrollPresenter != null) return;
            if (environmentArtCatalog == null)
            {
                var catalogs = Resources.FindObjectsOfTypeAll<EnvironmentArtCatalog>();
                for (var index = 0; index < catalogs.Length; index++)
                    if (catalogs[index] != null && catalogs[index].name == "EnvironmentArtCatalog")
                    {
                        environmentArtCatalog = catalogs[index];
                        break;
                    }
            }
            if (gameplayArtCatalog == null)
            {
                var catalogs = Resources.FindObjectsOfTypeAll<GameplayArtCatalog>();
                for (var index = 0; index < catalogs.Length; index++)
                    if (catalogs[index] != null && catalogs[index].name == "GameplayArtCatalog")
                    {
                        gameplayArtCatalog = catalogs[index];
                        break;
                    }
            }
            var frames = environmentArtCatalog?.DayCounterScrollFrames;
            if (frames == null || frames.Count == 0) return;

            var scrollObject = new GameObject("DayCounterScroll", typeof(RectTransform), typeof(Image));
            scrollObject.transform.SetParent(dayText.transform.parent, false);
            dayCounterScrollRect = (RectTransform)scrollObject.transform;
            dayCounterScrollRect.anchorMin = dayCounterScrollRect.anchorMax = dayCounterScrollRect.pivot =
                new Vector2(.5f, 1f);
            dayCounterScrollDefaultPosition = dayTextDefaultPosition;
            dayCounterScrollRect.anchoredPosition = dayCounterScrollDefaultPosition;
            var image = scrollObject.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            baekjungDayCounterBorder = BuildBaekjungDayCounterBorder(scrollObject.transform);
            baekjungDayCounterBorder.SetActive(false);
            scrollObject.transform.SetSiblingIndex(dayText.transform.GetSiblingIndex());
            dayCounterScrollPresenter = scrollObject.AddComponent<RuntimeDayCounterScrollPresenter>();
            dayCounterScrollPresenter.ConfigureForRuntime(frames, bootstrap.TimeService.DaysRemaining);
            if (gameplayArtCatalog?.ShellNumberGlyphs.Count == RuntimePixelGlyphPresenter.ExpectedGlyphCount)
            {
                dayCounterGlyphs = dayText.GetComponent<RuntimePixelGlyphPresenter>() ??
                                   dayText.gameObject.AddComponent<RuntimePixelGlyphPresenter>();
                dayCounterGlyphs.ConfigureForRuntime(gameplayArtCatalog.ShellNumberGlyphs);
                dayText.text = string.Empty;
                dayText.enabled = false;
            }

            var clockObject = new GameObject("DayCycleClock", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Text));
            clockObject.transform.SetParent(dayText.transform.parent, false);
            dayClockText = clockObject.GetComponent<Text>();
            dayClockText.font = dayText.font;
            dayClockText.fontSize = DayCounterClockFontSize;
            dayClockText.alignment = TextAnchor.UpperCenter;
            dayClockText.color = new Color(.88f, .93f, 1f, 1f);
            dayClockText.raycastTarget = false;
            dayClockText.horizontalOverflow = HorizontalWrapMode.Overflow;
            dayClockText.verticalOverflow = VerticalWrapMode.Overflow;
            var clockRect = dayClockText.rectTransform;
            clockRect.anchorMin = clockRect.anchorMax = clockRect.pivot = new Vector2(.5f, 1f);
            dayClockDefaultPosition = dayCounterScrollDefaultPosition +
                                      Vector2.down * (DayCounterExpandedHeight + DayCounterClockGap);
            clockRect.anchoredPosition = dayClockDefaultPosition;
            clockRect.sizeDelta = new Vector2(96f, 10f);
            clockObject.transform.SetSiblingIndex(dayText.transform.GetSiblingIndex() + 1);
            if (gameplayArtCatalog?.ShellNumberGlyphs.Count == RuntimePixelGlyphPresenter.ExpectedGlyphCount)
            {
                dayClockGlyphs = clockObject.AddComponent<RuntimePixelGlyphPresenter>();
                dayClockGlyphs.ConfigureForRuntime(gameplayArtCatalog.ShellNumberGlyphs, .6f);
                dayClockText.text = string.Empty;
                dayClockText.enabled = false;
            }

            var clockArtObject = new GameObject("DayNightClockArt", typeof(RectTransform), typeof(Image));
            clockArtObject.transform.SetParent(dayText.transform.parent, false);
            dayNightClockArt = clockArtObject.GetComponent<Image>();
            dayNightClockArt.raycastTarget = false;
            dayNightClockArt.preserveAspect = true;
            var artRect = dayNightClockArt.rectTransform;
            artRect.anchorMin = artRect.anchorMax = artRect.pivot = new Vector2(.5f, 1f);
            artRect.anchoredPosition = dayClockDefaultPosition + new Vector2(-22f, 0f);
            artRect.sizeDelta = new Vector2(10f, 10f);
            clockArtObject.transform.SetSiblingIndex(dayText.transform.GetSiblingIndex() + 1);
            nightSpawnLockRoot = BuildNightSpawnLock(artRect);
            nightSpawnLockRoot.SetActive(false);
            RefreshDayNightClockArt();
        }

        private void RefreshDayNightClockArt()
        {
            if (dayNightClockArt == null || bootstrap?.TimeService == null) return;
            if (dayClockGlyphs != null)
            {
                var artRect = dayNightClockArt.rectTransform;
                artRect.anchoredPosition = dayClockDefaultPosition + new Vector2(
                    -dayClockGlyphs.RenderedWidth * .5f - artRect.sizeDelta.x * .5f - 1f, 0f);
            }
            var frames = gameplayArtCatalog?.DayNightClockFrames;
            var index = ResolveDayNightClockFrameIndex(bootstrap.TimeService.TimeOfDayGameSeconds,
                bootstrap.TimeService.CycleLengthSeconds, frames?.Count ?? 0);
            dayNightClockArt.sprite = index >= 0 ? frames[index] : null;
            dayNightClockArt.enabled = dayNightClockArt.sprite != null;
            if (nightSpawnLockRoot != null)
                nightSpawnLockRoot.SetActive(dayNightClockArt.enabled && ShouldShowNightSpawnLock(
                    bootstrap.TimeService.IsNight,
                    bossManager != null && bossManager.IsBossActive,
                    encounterCoordinator?.BaekjungScheduler?.IsActive == true));
        }

        private static GameObject BuildNightSpawnLock(RectTransform parent)
        {
            var root = new GameObject("NightSpawnLock", typeof(RectTransform));
            var rootRect = (RectTransform)root.transform;
            rootRect.SetParent(parent, false);
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            var lockColor = new Color32(0x49, 0x2D, 0x4D, 0xFF);
            AddNightSpawnLockPiece(rootRect, "Bar", Vector2.zero, new Vector2(12f, 2f), lockColor);
            AddNightSpawnLockPiece(rootRect, "LeftCap", new Vector2(-5f, 0f), new Vector2(2f, 6f), lockColor);
            AddNightSpawnLockPiece(rootRect, "RightCap", new Vector2(5f, 0f), new Vector2(2f, 6f), lockColor);
            return root;
        }

        private static void AddNightSpawnLockPiece(RectTransform parent, string name,
            Vector2 anchoredPosition, Vector2 size, Color color)
        {
            var piece = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)piece.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            var image = piece.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        public static bool ShouldShowBaekjungDayCounterFeedback(bool baekjungActive,
            bool suppressedForBoss, bool bossActive) =>
            baekjungActive && !suppressedForBoss && !bossActive;

        private void RefreshBaekjungDayCounterFeedback()
        {
            if (baekjungDayCounterBorder == null) return;
            if (!baekjungHudActive)
            {
                encounterCoordinator ??= FindAnyObjectByType<MainGameEncounterCoordinator>();
                baekjungHudActive = encounterCoordinator?.BaekjungScheduler?.IsActive == true;
            }
            baekjungDayCounterBorder.SetActive(ShouldShowBaekjungDayCounterFeedback(
                baekjungHudActive, baekjungHudSuppressedForBoss, bossManager?.IsBossActive == true));
        }

        private static GameObject BuildBaekjungDayCounterBorder(Transform parent)
        {
            var root = new GameObject("BaekjungDayCounterBorder", typeof(RectTransform));
            var rootRect = (RectTransform)root.transform;
            rootRect.SetParent(parent, false);
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            AddBaekjungBorderEdge(rootRect, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, BaekjungDayCounterBorderPixels));
            AddBaekjungBorderEdge(rootRect, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, BaekjungDayCounterBorderPixels));
            AddBaekjungBorderEdge(rootRect, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(BaekjungDayCounterBorderPixels, 0f));
            AddBaekjungBorderEdge(rootRect, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(BaekjungDayCounterBorderPixels, 0f));
            return root;
        }

        private static void AddBaekjungBorderEdge(RectTransform parent, string edgeName,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta)
        {
            var edge = new GameObject(edgeName, typeof(RectTransform), typeof(Image));
            var edgeRect = (RectTransform)edge.transform;
            edgeRect.SetParent(parent, false);
            edgeRect.anchorMin = anchorMin;
            edgeRect.anchorMax = anchorMax;
            edgeRect.pivot = (anchorMin + anchorMax) * .5f;
            edgeRect.anchoredPosition = Vector2.zero;
            edgeRect.sizeDelta = sizeDelta;
            var image = edge.GetComponent<Image>();
            image.color = new Color32(0x9B, 0x6D, 0xD6, 0xFF);
            image.raycastTarget = false;
        }

        private void BuildBossHealthBar()
        {
            if (bossStatusText == null || bossHealthBarRoot != null) return;
            bossStatusDefaultPosition = bossStatusText.rectTransform.anchoredPosition;
            bossStatusDefaultFontSize = bossStatusText.fontSize;
            hasBossStatusDefaultLayout = true;
            bossStatusText.alignment = TextAnchor.UpperCenter;

            bossHealthBarRoot = new GameObject("BossHealthBar", typeof(RectTransform));
            var rootRect = (RectTransform)bossHealthBarRoot.transform;
            var nativeRoot = MainGameUiResolutionController.ResolveNativeRoot(transform) ?? transform;
            rootRect.SetParent(nativeRoot, false);
            rootRect.anchorMin = rootRect.anchorMax = rootRect.pivot = new Vector2(.5f, 1f);
            rootRect.anchoredPosition = new Vector2(0f, BossHealthBarBelowClockY);
            rootRect.sizeDelta = new Vector2(128f, 32f);

            var segmentStarts = new[] { 13f, 32f, 51f, 80f, 99f };
            var segmentWidths = new[] { 16f, 16f, 26f, 16f, 16f };
            for (var index = 0; index < segmentStarts.Length; index++)
                BuildBossHealthSegment(rootRect, index, segmentStarts[index], segmentWidths[index]);
            var portraitObject = new GameObject("BossArt", typeof(RectTransform));
            portraitObject.transform.SetParent(rootRect, false);
            bossHealthPortrait = portraitObject.AddComponent<Image>();
            bossHealthPortrait.preserveAspect = false;
            bossHealthPortrait.raycastTarget = false;
            var portraitRect = bossHealthPortrait.rectTransform;
            portraitRect.anchorMin = portraitRect.anchorMax = portraitRect.pivot = new Vector2(.5f, .5f);
            portraitRect.anchoredPosition = Vector2.zero;
            portraitRect.sizeDelta = new Vector2(118f, 32f);

            ResizeBossHealthBar(1f);
            bossHealthBarRoot.SetActive(false);

            bossEntranceArtRoot = new GameObject("BossEntranceArt", typeof(RectTransform));
            var entranceRect = (RectTransform)bossEntranceArtRoot.transform;
            entranceRect.SetParent(nativeRoot, false);
            entranceRect.anchorMin = entranceRect.anchorMax = entranceRect.pivot = new Vector2(.5f, .5f);
            entranceRect.anchoredPosition = new Vector2(0f, 42f);
            entranceRect.sizeDelta = new Vector2(64f, 38f);
            bossEntranceArt = bossEntranceArtRoot.AddComponent<Image>();
            bossEntranceArt.preserveAspect = true;
            bossEntranceArt.raycastTarget = false;
            bossEntranceArtRoot.SetActive(false);
        }

        private void RestoreBossStatusLayout()
        {
            if (!hasBossStatusDefaultLayout || bossStatusText == null) return;
            bossStatusText.fontSize = bossStatusDefaultFontSize;
            bossStatusText.rectTransform.anchoredPosition = bossStatusDefaultPosition;
        }

        private void BuildBossHealthSegment(RectTransform parent, int index, float start, float width)
        {
            var backgroundObject = new GameObject($"Depleted_{index + 1}", typeof(RectTransform));
            backgroundObject.transform.SetParent(parent, false);
            var background = backgroundObject.AddComponent<Image>();
            background.color = new Color32(0x1A, 0x1A, 0x24, 0xFF);
            background.raycastTarget = false;
            ConfigureBossHealthSegmentRect(background.rectTransform, start, width);
            bossHealthSegmentRects.Add(background.rectTransform);

            var fillObject = new GameObject($"Fill_{index + 1}", typeof(RectTransform));
            fillObject.transform.SetParent(parent, false);
            var fill = fillObject.AddComponent<Image>();
            fill.color = new Color32(0x91, 0xDA, 0xA1, 0xFF);
            fill.raycastTarget = false;
            ConfigureBossHealthSegmentRect(fill.rectTransform, start, width);
            bossHealthSegmentRects.Add(fill.rectTransform);
            bossHealthBarFills.Add(fill);
            bossHealthSegmentWidths.Add(width);
        }

        private static void ConfigureBossHealthSegmentRect(RectTransform rect, float start, float width)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, .5f);
            rect.anchoredPosition = new Vector2(start, -1.5f);
            rect.sizeDelta = new Vector2(width, 5f);
        }

        private void ConfigureBossHealthVerticalLayout(string bossId)
        {
            var verticalOffset = bossId switch
            {
                "mother_bulgasari" => -4.5f,
                // These offsets follow the delivered frame artwork. After correcting the
                // Gangcheol/King row mapping, their previously calibrated offsets swap too.
                "king_dokkaebi" => -2.75f,
                "gangcheol_boss" => -5f,
                "imugi" => -4f,
                _ => -2.5f
            };
            for (var index = 0; index < bossHealthSegmentRects.Count; index++)
            {
                var rect = bossHealthSegmentRects[index];
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, verticalOffset);
            }
        }

        private Sprite ResolveBossHealthArt(string bossId)
        {
            if (runtimeBossHealthSprite != null && runtimeBossHealthSpriteId == bossId)
                return runtimeBossHealthSprite;

            // boss_health_frame is the authoritative four-row sheet. The per-boss Aseprite
            // assets contain the same sheet with different canvas offsets, so cropping those
            // imports again can select another boss after a reimport.
            var source = gameplayArtCatalog?.BossHealthFrame ?? BossHealthArtSource(bossId);
            if (source == null) return null;
            if (runtimeBossHealthSprite != null) Destroy(runtimeBossHealthSprite);

            var sourceRect = source.textureRect;
            float topStart;
            float topEnd;
            switch (BossHealthArtRow(bossId))
            {
                case 0: topStart = 0f; topEnd = 28f; break;
                case 1: topStart = 28f; topEnd = 60f; break;
                case 2: topStart = 60f; topEnd = 92f; break;
                case 3: topStart = 92f; topEnd = sourceRect.height; break;
                default: return null;
            }

            topStart = Mathf.Clamp(topStart, 0f, sourceRect.height);
            topEnd = Mathf.Clamp(topEnd, topStart + 1f, sourceRect.height);
            var croppedRect = new Rect(sourceRect.x,
                sourceRect.y + sourceRect.height - topEnd,
                sourceRect.width, topEnd - topStart);
            runtimeBossHealthSprite = Sprite.Create(source.texture, croppedRect, new Vector2(.5f, .5f),
                source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            runtimeBossHealthSprite.name = $"{bossId}_health_bar_runtime";
            runtimeBossHealthSpriteId = bossId;
            return runtimeBossHealthSprite;
        }

        public static int BossHealthArtRow(string bossId)
        {
            switch (bossId)
            {
                // Unity's Aseprite textureRect exposes the delivered vertical sheet from
                // bottom to top, so the runtime crop order is the reverse of the source view.
                case "king_dokkaebi": return 0;
                case "mother_bulgasari": return 1;
                case "imugi": return 2;
                case "gangcheol_boss": return 3;
                default: return -1;
            }
        }

        private Sprite BossHealthArtSource(string bossId)
        {
            switch (bossId)
            {
                case "gangcheol_boss": return gameplayArtCatalog?.BossHealthGangcheol;
                case "king_dokkaebi": return gameplayArtCatalog?.BossHealthKingDokkaebi;
                case "mother_bulgasari": return gameplayArtCatalog?.BossHealthMotherBulgasari;
                case "imugi": return gameplayArtCatalog?.BossHealthImugi;
                default: return null;
            }
        }

        private void HandleBossStarted(BossDefinition _)
        {
            bossFleeRollRemaining = 0f;
            if (bossHealthBarRoot != null) bossHealthBarRoot.transform.localScale = Vector3.one;
            if (baekjungHudActive) baekjungHudSuppressedForBoss = true;
            RefreshBaekjungDayCounterFeedback();
            if (bossEntranceArt == null) return;
            bossEntranceArt.sprite = gameplayArtCatalog?.BossWarningLarge ?? gameplayArtCatalog?.BossWarningSmall;
            bossEntranceArt.enabled = bossEntranceArt.sprite != null;
            bossEntranceArtRemaining = bossEntranceArt.enabled ? 1.2f : 0f;
            bossEntranceArtRoot?.SetActive(bossEntranceArt.enabled);
        }

        private void HandleBossEnded(BossDefinition _, bool defeated)
        {
            bossEntranceArtRemaining = 0f;
            bossEntranceArtRoot?.SetActive(false);
            bossFleeRollRemaining = defeated ? 0f : BossFleeRollSeconds;
            if (defeated && bossHealthBarRoot != null)
            {
                bossHealthBarRoot.transform.localScale = Vector3.one;
                bossHealthBarRoot.SetActive(false);
            }
        }

        private void HandleBaekjungStarted()
        {
            baekjungHudActive = true;
            baekjungHudSuppressedForBoss = false;
            RefreshBaekjungDayCounterFeedback();
        }

        private void HandleBaekjungEnded()
        {
            baekjungHudActive = false;
            baekjungHudSuppressedForBoss = false;
            RefreshBaekjungDayCounterFeedback();
        }

        private void ResizeBossHealthBar(float ratio)
        {
            if (bossHealthBarRoot == null || bossHealthBarFills.Count == 0) return;
            ratio = Mathf.Clamp01(ratio);
            var totalWidth = 0f;
            for (var index = 0; index < bossHealthSegmentWidths.Count; index++)
                totalWidth += bossHealthSegmentWidths[index];
            var remainingWidth = totalWidth * ratio;
            for (var index = 0; index < bossHealthBarFills.Count; index++)
            {
                var segmentWidth = bossHealthSegmentWidths[index];
                var visibleWidth = Mathf.Clamp(remainingWidth, 0f, segmentWidth);
                var fill = bossHealthBarFills[index];
                var fillRect = fill.rectTransform;
                fillRect.sizeDelta = new Vector2(visibleWidth, 5f);
                fill.enabled = visibleWidth > 0f;
                remainingWidth -= segmentWidth;
            }
        }

        private void RefreshInventory()
        {
            if (inventory == null || inventorySlotTexts == null) return;
            for (var index = 0; index < inventorySlotTexts.Length; index++)
            {
                var text = inventorySlotTexts[index];
                if (text == null) continue;
                var slot = inventory.Slots[index];
                var item = string.IsNullOrEmpty(slot.itemId) ? null : gameDataCatalog.FindItem(slot.itemId);
                text.text = item == null ? $"{index + 1}\n-" : $"{index + 1}\n{item.DisplayName} x{slot.amount}";
                var icon = inventorySlotIcons[index];
                if (icon == null) continue;
                icon.sprite = item != null ? itemArtCatalog?.FindSprite(item.Id) : null;
                icon.enabled = icon.sprite != null;
            }
        }

        private void HandlePlayerDamaged(DamageTag tag, int amount)
        {
            if (amount > 0) damageWarningRemaining = PlayerDamageWarningSeconds;
            RefreshStatus();
        }

        private void HandlePlayerDied()
        {
            RefreshStatus();
            if (deathPanel != null) deathPanel.SetActive(true);
        }

        private int ResolveClawTier()
        {
            if (inventory?.Has("icesteel_claw", 1) == true) return 3;
            if (inventory?.Has("iron_claw", 1) == true) return 2;
            return 1;
        }

        private void OnDestroy()
        {
            if (activeHud == this) activeHud = null;
            if (inventory != null) inventory.Changed -= RefreshInventory;
            if (playerHealth != null)
            {
                playerHealth.Damaged -= HandlePlayerDamaged;
                playerHealth.Died -= HandlePlayerDied;
            }
            if (bossManager != null)
            {
                bossManager.BossStarted -= HandleBossStarted;
                bossManager.BossEnded -= HandleBossEnded;
            }
            if (runtimeBossHealthSprite != null) Destroy(runtimeBossHealthSprite);
            GameEvents.OnSealChanged -= RefreshStatus;
            GameEvents.OnBaekjungStart -= HandleBaekjungStarted;
            GameEvents.OnBaekjungEnd -= HandleBaekjungEnded;
            GameEvents.OnTilePlaced -= HandleSealAffectingPlacement;
            GameEvents.OnPlacedObjectBuilt -= HandleSealAffectingPlacement;
            if (goalBadgeProgress != null) goalBadgeProgress.Changed -= RefreshGoalBadges;
            if (hudSaveManager != null) hudSaveManager.Saved -= HandleSaved;
            if (sealLeakMarker != null) Destroy(sealLeakMarker.gameObject);
            if (sealLeakMarkerMaterial != null) Destroy(sealLeakMarkerMaterial);
        }
    }
}
