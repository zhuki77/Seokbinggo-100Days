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
        public const int LegacyInventoryBarSlotCount = 12;
        public const bool ProductHudNarrativeTextEnabled = false;
        public const bool ProductBossHealthTextEnabled = false;
        public const float BossHealthBarBelowClockY = -18f;
        public const float BossHealthBarWidth = 192f;
        public const float BossHealthBarHeight = 48f;
        public const float BossHealthSegmentHeight = 7.5f;
        public const float BossHealthValueGlyphScale = .5f;
        public const float BossHealthValueVerticalNudge = -.5f;
        public const float BossFleeRollSeconds = .45f;
        public const float BossEntranceFlashDuration = 1.2f;
        public const int DayCounterFontSize = 12;
        public const int DayCounterClockFontSize = 7;
        public const float DayCounterExpandedHeight = 32f;
        public const float DayCounterClockHeight = 10f;
        public const float DayCounterClockGap = 1f;
        public const float SunsetWarningLeadSeconds = 60f;
        public const float SunsetWarningFlashesPerSecond = 2f;
        public const float BaekjungDayCounterBorderPixels = 1f;
        public const string GoalBadgeDayNightRhythmHint = "낮 · 채집/건설  |  밤 · 요괴 방어";
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
        [SerializeField] private GameObject bossHealthBarRoot;
        private float bossFleeRollRemaining;
        [SerializeField] private Image[] bossHealthBarFills = new Image[5];
        private static readonly float[] BossHealthSegmentWidths = { 24f, 24f, 39f, 24f, 24f };
        [SerializeField] private RectTransform[] bossHealthSegmentRects = new RectTransform[10];
        [SerializeField] private Image bossHealthPortrait;
        [SerializeField] private Text bossHealthValueText;
        private RuntimePixelGlyphPresenter bossHealthValueGlyphs;
        [SerializeField] private RectTransform bossHealthValueRect;
        private readonly Dictionary<string, Sprite> runtimeBossHealthSpriteCache = new Dictionary<string, Sprite>();
        [SerializeField] private GameObject bossEntranceFlashRoot;
        [SerializeField] private Image bossEntranceFlash;
        private float bossEntranceFlashRemaining;
        private MainGameSaveCoordinator saveCoordinator;
        private GoalBadgeProgress goalBadgeProgress;
        [SerializeField] private GameObject goalBadgeRoot;
        [SerializeField] private Text goalBadgeRhythmHint;
        [SerializeField] private Image[] goalBadgeBackgrounds = new Image[3];
        [SerializeField] private GameObject[] goalBadgeChecks = new GameObject[3];
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
        [SerializeField] private RectTransform alertOverlayRoot;
        [SerializeField] private Image alertOverlayTint;
        [SerializeField] private Text alertOverlayText;
        [SerializeField] private Image alertDangerIcon;
        [SerializeField] private RectTransform alertDirectionMarker;
        [SerializeField] private Image alertDirectionIcon;
        [SerializeField] private Text alertDirectionText;
        private readonly List<Vector2> bellRopePositions = new List<Vector2>();
        private readonly List<Transform> activeThreats = new List<Transform>();
        private readonly HashSet<Transform> bellTargetsInside = new HashSet<Transform>();
        private readonly HashSet<Transform> nextBellTargetsInside = new HashSet<Transform>();
        private float damageWarningRemaining;
        private float bellWarningRemaining;
        private Vector2 bellWarningTargetPosition;
        [SerializeField] private GameObject statusArtRoot;
        [SerializeField] private Image playerVitalsArt;
        [SerializeField] private Image playerHealthFill;
        [SerializeField] private Image playerTemperatureFill;
        private RuntimePixelGlyphPresenter playerHealthGlyphs;
        private RuntimePixelGlyphPresenter playerTemperatureGlyphs;
        [SerializeField] private Image tearBalanceArt;
        [SerializeField] private Text tearBalanceText;
        [SerializeField] private Image fuelGaugeArt;
        [SerializeField] private Text fuelGaugeText;
        [SerializeField] private Image saveIndicatorArt;
        private SaveManager hudSaveManager;
        private float saveIndicatorRemaining;
        private int lastTearBalance = -1;
        private float tearAnimationRemaining;
        private Vector2 dayTextDefaultPosition;
        private bool hasDayTextDefaultPosition;
        [SerializeField] private RectTransform dayCounterScrollRect;
        private Vector2 dayCounterScrollDefaultPosition;
        private RuntimeDayCounterScrollPresenter dayCounterScrollPresenter;
        private RuntimePixelGlyphPresenter dayCounterGlyphs;
        [SerializeField] private GameObject baekjungDayCounterBorder;
        private bool baekjungHudActive;
        private bool baekjungHudSuppressedForBoss;
        [SerializeField] private Text dayClockText;
        private RuntimePixelGlyphPresenter dayClockGlyphs;
        [SerializeField] private Image dayNightClockArt;
        [SerializeField] private GameObject nightSpawnLockRoot;
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
        [SerializeField] private Text sealDeltaText;
        private float sealDeltaRemaining;
        private float lastSealPercent;
        private bool hasLastSealPercent;
        private int invasionBannerDay;
        private bool hypothermiaCuePlayed;
        private static MainGameHudController activeHud;

        public static bool IsInvasionBedLocked(GameDataCatalog catalog, DayNightService timeService)
        {
            if (catalog == null || timeService == null) return false;
            var period = InvasionScheduleRules.ReadPeriod(catalog);
            var offset = InvasionScheduleRules.ReadOffset(catalog);
            var bedLock = InvasionScheduleRules.ReadBedLockEnabled(catalog);
            return InvasionScheduleRules.IsBedLocked(timeService.Day, timeService.IsNight, bedLock, period, offset);
        }

        public static string InvasionBedLockedMessage => InvasionScheduleRules.BedLockedMessage;

        public int BoundSlotCount => inventorySlotTexts?.Length ?? 0;
        public int BoundIconCount => inventorySlotIcons?.Length ?? 0;
        public ItemArtCatalog BoundItemArtCatalog => itemArtCatalog;
        public bool HasPlayerStatusBindings => playerHealth != null && playerHealthText != null && deathPanel != null;
        public bool HasCraftingProgressBindings => craftingProgressPanel != null && craftingProgressText != null &&
                                                   craftingProgressFill != null;
        public static bool BlocksWorldPrimaryInput => activeHud != null && activeHud.IsPointerOverSealGauge();

        public static Vector2 ResolveDayCounterPositionBelowClock(Vector2 clockPosition) =>
            clockPosition + Vector2.down * (DayCounterClockHeight + DayCounterClockGap);

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
                    mainCamera.GetComponent<MainGameParallaxBackground>(), runtimeServices.HeatStage);
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
            bootstrap.TimeService.Dawn += HandleDayCounterDawn;
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
            Debug.Log("[Nyangbingo] MainGameHudController: 체온·석빙고 온도·폭염 단계·발톱 티어 HUD와 " +
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
            bossEntranceFlashRemaining = Mathf.Max(0f,
                bossEntranceFlashRemaining - Time.unscaledDeltaTime);
            bossFleeRollRemaining = Mathf.Max(0f, bossFleeRollRemaining - Time.unscaledDeltaTime);
            saveIndicatorRemaining = Mathf.Max(0f, saveIndicatorRemaining - Time.unscaledDeltaTime);
            tearAnimationRemaining = Mathf.Max(0f, tearAnimationRemaining - Time.unscaledDeltaTime);
            sealLeakMarkerRemaining = Mathf.Max(0f, sealLeakMarkerRemaining - Time.unscaledDeltaTime);
            sealDeltaRemaining = Mathf.Max(0f, sealDeltaRemaining - Time.unscaledDeltaTime);
            UpdateSealDiagnosticInput();
            RefreshSealFeedbackVisuals();
            RefreshBossEntranceFlash();
            RefreshBellRopeDetection();
            RefreshAlertOverlay();
            RefreshStatus();
            SynchronizeSealPercentBaseline();
            RefreshInvasionAnnouncement();
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
            RefreshRoomTemperature();
            if (dayText != null)
            {
                var heatStage = ResolveDisplayedHeatStage();
                var badge = HeatStagePresentation.FormatBadge(heatStage);
                var counterVisible = dayCounterScrollPresenter == null || dayCounterScrollPresenter.IsFullyOpen;
                if (dayCounterGlyphs != null)
                {
                    dayText.text = string.Empty;
                    dayText.enabled = false;
                    // B-UI-v71: D-100 슬롯에 폭염 단계만 표시(태양 아이콘은 기존 시계/아트 유지).
                    dayCounterGlyphs.SetText(badge);
                    dayCounterGlyphs.SetVisible(counterVisible);
                }
                else
                {
                    dayText.text = badge;
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
                RefreshSunsetWarning();
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
            if (statusArtRoot == null)
            {
                Debug.LogError("[Nyangbingo] MainGameHudController: PlayerStatusArt 하이어라키가 인스펙터에 배선되지 않았습니다.");
                return;
            }

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

            if (saveIndicatorArt != null) saveIndicatorArt.enabled = false;

            hudSaveManager = FindAnyObjectByType<SaveManager>();
            if (hudSaveManager != null) hudSaveManager.Saved += HandleSaved;
        }

        private void BuildSealFeedbackHud()
        {
            if (sealDeltaText == null)
            {
                Debug.LogError("[Nyangbingo] MainGameHudController: SealDeltaFeedback 하이어라키가 인스펙터에 배선되지 않았습니다.");
                return;
            }
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
            playerHealthFill.color = new Color(.82f, .15f, .12f, 1f);
            playerHealthFill.rectTransform.sizeDelta = new Vector2(42f * healthRatio, 2.5f);
            playerHealthFill.enabled = healthRatio > 0f;

            var temperatureRatio = Mathf.Clamp01(runtimeServices.PlayerTemperature.Normalized);
            var hypothermiaBlink = IsHypothermiaWarningActive() &&
                                   IsSunsetWarningBrightPhase(Time.unscaledTime);
            playerTemperatureFill.color = hypothermiaBlink
                ? new Color(.35f, .72f, 1f, 1f)
                : VitalsTemperatureColor(temperatureBucket);
            playerTemperatureFill.rectTransform.sizeDelta = new Vector2(42f * temperatureRatio, 2.5f);
            playerTemperatureFill.enabled = temperatureRatio > 0f || hypothermiaBlink;

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
            if (goalBadgeRoot == null)
            {
                Debug.LogError("[Nyangbingo] MainGameHudController: GoalBadges 하이어라키가 인스펙터에 배선되지 않았습니다.");
                return;
            }
            saveCoordinator = FindAnyObjectByType<MainGameSaveCoordinator>();
            goalBadgeProgress = saveCoordinator != null ? saveCoordinator.ProgressTracker?.GoalBadges : null;

            if (goalBadgeProgress != null) goalBadgeProgress.Changed += RefreshGoalBadges;
            RefreshGoalBadges();
        }

        private void BuildAlertOverlay()
        {
            if (alertOverlayRoot == null)
            {
                Debug.LogError("[Nyangbingo] MainGameHudController: PriorityAlertOverlay 하이어라키가 인스펙터에 배선되지 않았습니다.");
                return;
            }
            alertOverlayRoot.SetAsLastSibling();
            if (alertDangerIcon != null) alertDangerIcon.sprite = gameplayArtCatalog?.DangerIcon;
            if (alertDirectionIcon != null) alertDirectionIcon.sprite = gameplayArtCatalog?.DangerIcon;
            alertOverlayRoot.gameObject.SetActive(false);
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
            if (IsHypothermiaWarningActive())
            {
                var pulse = IsSunsetWarningBrightPhase(Time.unscaledTime);
                var alpha = pulse ? .22f : .12f;
                SetAlertOverlay(new Color(.12f, .28f, .62f, alpha), string.Empty,
                    new Color(.72f, .88f, 1f), false);
                return;
            }
            if (ShouldShowInvasionBanner())
            {
                SetAlertOverlay(new Color(.42f, .08f, .06f, .14f),
                    InvasionScheduleRules.AnnouncementBannerText,
                    new Color(1f, .82f, .68f), false);
                return;
            }
            alertOverlayRoot.gameObject.SetActive(false);
        }

        private void SetAlertOverlay(Color tint, string message, Color textColor, bool showThreatDirection)
        {
            alertOverlayRoot.gameObject.SetActive(true);
            alertOverlayRoot.SetAsLastSibling();
            alertOverlayTint.color = tint;
            alertOverlayText.text = message ?? string.Empty;
            alertOverlayText.enabled = !string.IsNullOrEmpty(message);
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

            var roomService = runtimeServices?.RoomTemperature;
            if (roomService == null || playerController == null)
            {
                temperatureArt.enabled = false;
                return;
            }
            var celsius = roomService.Resolve(playerController.transform.position);
            var band = RoomTempPresentation.ResolveBand(
                celsius, roomService.ColdEnterCelsius, roomService.FrozenEnterCelsius);
            var normalizedBand = band == RoomTempPresentation.Band.Frozen
                ? 1f
                : band == RoomTempPresentation.Band.Chilled ? .5f : 0f;
            var index = Mathf.RoundToInt(normalizedBand * (frames.Count - 1));
            temperatureArt.sprite = frames[Mathf.Clamp(index, 0, frames.Count - 1)];
            temperatureArt.enabled = temperatureArt.sprite != null;
        }

        private void RefreshRoomTemperature()
        {
            if (sealText == null || runtimeServices?.RoomTemperature == null ||
                playerController == null) return;
            var roomService = runtimeServices.RoomTemperature;
            var celsius = roomService.Resolve(playerController.transform.position);
            var band = RoomTempPresentation.ResolveBand(celsius, roomService.ColdEnterCelsius,
                roomService.FrozenEnterCelsius);
            sealText.text = RoomTempPresentation.FormatCelsius(celsius);
            sealText.color = RoomTempPresentation.BandColor(band);
            sealText.gameObject.SetActive(true);
            if (RoomTempPresentation.ShouldWarnHypothermia(celsius, roomService.FrozenEnterCelsius) &&
                !hypothermiaCuePlayed)
            {
                hypothermiaCuePlayed = true;
                GameEvents.RaiseHypothermiaEntered();
            }
            else if (!RoomTempPresentation.ShouldWarnHypothermia(celsius, roomService.FrozenEnterCelsius))
                hypothermiaCuePlayed = false;
        }

        private bool IsHypothermiaWarningActive()
        {
            if (runtimeServices?.RoomTemperature == null || playerController == null) return false;
            var roomService = runtimeServices.RoomTemperature;
            var celsius = roomService.Resolve(playerController.transform.position);
            return RoomTempPresentation.ShouldWarnHypothermia(celsius, roomService.FrozenEnterCelsius);
        }

        private void RefreshInvasionAnnouncement()
        {
            var time = bootstrap?.TimeService;
            if (time == null || gameDataCatalog == null) return;
            if (time.Day != invasionBannerDay)
            {
                invasionBannerDay = time.Day;
                if (InvasionScheduleRules.ShouldShowAnnouncement(time.Day, time.IsNight,
                        InvasionScheduleRules.ReadAnnounceEnabled(gameDataCatalog),
                        InvasionScheduleRules.ReadPeriod(gameDataCatalog),
                        InvasionScheduleRules.ReadOffset(gameDataCatalog)))
                    GameEvents.RaiseInvasionAnnounced();
            }
        }

        private bool ShouldShowInvasionBanner()
        {
            var time = bootstrap?.TimeService;
            if (time == null || gameDataCatalog == null || time.IsNight) return false;
            return InvasionScheduleRules.ShouldShowAnnouncement(time.Day, time.IsNight,
                InvasionScheduleRules.ReadAnnounceEnabled(gameDataCatalog),
                InvasionScheduleRules.ReadPeriod(gameDataCatalog),
                InvasionScheduleRules.ReadOffset(gameDataCatalog));
        }

        private void RefreshBossStatus()
        {
            if (bossStatusText == null) return;
            var definition = bossManager != null ? bossManager.ActiveDefinition : null;
            var health = bossManager != null ? bossManager.ActiveHealth : null;
            var healthBarId = definition != null ? definition.Id : string.Empty;
            if ((definition == null || health == null) && encounterCoordinator != null &&
                encounterCoordinator.TryGetActiveGaekgwi(out var gaekgwi, out var gaekgwiHealth))
            {
                healthBarId = gaekgwi.Id;
                health = gaekgwiHealth;
            }
            if (string.IsNullOrEmpty(healthBarId) || health == null)
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
                bossHealthPortrait.sprite = ResolveBossHealthArt(healthBarId);
                bossHealthPortrait.enabled = bossHealthPortrait.sprite != null;
            }
            ConfigureBossHealthVerticalLayout(healthBarId);
            ResizeBossHealthBar(CalculateHealthRatio(health.Current, health.MaxHealth));
            var displayedBossHealth = FormatBossCurrentHealth(health.Current);
            if (bossHealthValueGlyphs != null)
                bossHealthValueGlyphs.SetText(displayedBossHealth);
            else if (bossHealthValueText != null)
                bossHealthValueText.text = displayedBossHealth;

            // Current HP is rendered inside the illustrated bar; keep the legacy external label empty.
            bossStatusText.text = string.Empty;
#if UNITY_EDITOR
            // Test controls are listed in the F5 debug shortcut popup.
#endif
        }

        public static float CalculateHealthRatio(int current, int maximum) =>
            maximum <= 0 ? 0f : Mathf.Clamp01((float)current / maximum);

        public static string FormatBossCurrentHealth(int current) => Mathf.Max(0, current).ToString();

        public static float CalculateBossFleeRollScale(float remainingSeconds, float durationSeconds) =>
            durationSeconds <= 0f ? 0f : Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01(remainingSeconds / durationSeconds));

        public static string FormatCycleCountdown(DayNightService timeService)
        {
            if (timeService == null) return string.Empty;
            return FormatRemainingTime(timeService.SecondsUntilNextTransition);
        }

        private int ResolveDisplayedHeatStage()
        {
            return runtimeServices?.HeatStage?.Current ?? 1;
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

        public static bool IsSunsetWarningWindow(bool isNight, float secondsUntilTransition)
        {
            if (isNight || float.IsNaN(secondsUntilTransition) ||
                float.IsInfinity(secondsUntilTransition))
                return false;
            return secondsUntilTransition >= 0f &&
                   secondsUntilTransition <= SunsetWarningLeadSeconds;
        }

        public static bool IsSunsetWarningBrightPhase(float gameSeconds)
        {
            if (float.IsNaN(gameSeconds) || float.IsInfinity(gameSeconds) || gameSeconds < 0f)
                return false;
            return Mathf.FloorToInt(gameSeconds * SunsetWarningFlashesPerSecond) % 2 == 0;
        }

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
            if (dayCounterScrollRect == null || dayClockText == null || dayNightClockArt == null)
            {
                Debug.LogError("[Nyangbingo] MainGameHudController: DayCounterScroll/Clock 하이어라키가 인스펙터에 배선되지 않았습니다.");
                return;
            }
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

            dayClockDefaultPosition = dayTextDefaultPosition;
            dayCounterScrollDefaultPosition = ResolveDayCounterPositionBelowClock(dayClockDefaultPosition);
            dayTextDefaultPosition = dayCounterScrollDefaultPosition;
            dayText.rectTransform.anchoredPosition = dayTextDefaultPosition;
            dayCounterScrollRect.anchoredPosition = dayCounterScrollDefaultPosition;
            if (baekjungDayCounterBorder != null) baekjungDayCounterBorder.SetActive(false);
            dayCounterScrollPresenter = dayCounterScrollRect.GetComponent<RuntimeDayCounterScrollPresenter>() ??
                                         dayCounterScrollRect.gameObject.AddComponent<RuntimeDayCounterScrollPresenter>();
            dayCounterScrollPresenter.ConfigureForRuntime(frames, bootstrap.TimeService.DaysRemaining);
            dayCounterScrollPresenter.PresentationCompleted += HandleDayCounterPresentationCompleted;
            if (gameplayArtCatalog?.ShellNumberGlyphs.Count == RuntimePixelGlyphPresenter.ExpectedGlyphCount)
            {
                dayCounterGlyphs = dayText.GetComponent<RuntimePixelGlyphPresenter>() ??
                                   dayText.gameObject.AddComponent<RuntimePixelGlyphPresenter>();
                dayCounterGlyphs.ConfigureForRuntime(gameplayArtCatalog.ShellNumberGlyphs);
                dayText.text = string.Empty;
                dayText.enabled = false;
            }

            dayClockText.rectTransform.anchoredPosition = dayClockDefaultPosition;
            if (gameplayArtCatalog?.ShellNumberGlyphs.Count == RuntimePixelGlyphPresenter.ExpectedGlyphCount)
            {
                dayClockGlyphs = dayClockText.GetComponent<RuntimePixelGlyphPresenter>() ??
                                  dayClockText.gameObject.AddComponent<RuntimePixelGlyphPresenter>();
                dayClockGlyphs.ConfigureForRuntime(gameplayArtCatalog.ShellNumberGlyphs, .6f);
                dayClockText.text = string.Empty;
                dayClockText.enabled = false;
            }

            dayNightClockArt.rectTransform.anchoredPosition = dayClockDefaultPosition + new Vector2(-22f, 0f);
            if (nightSpawnLockRoot != null) nightSpawnLockRoot.SetActive(false);
            RefreshDayNightClockArt();
            dayText.gameObject.SetActive(false);
            dayCounterScrollRect.gameObject.SetActive(false);
        }

        private void HandleDayCounterDawn()
        {
            if (dayCounterScrollPresenter == null || dayText == null || bootstrap?.TimeService == null) return;
            dayCounterScrollRect.gameObject.SetActive(true);
            dayText.gameObject.SetActive(true);
            dayCounterScrollPresenter.PlayDayChange(bootstrap.TimeService.DaysRemaining);
            RefreshStatus();
        }

        private void HandleDayCounterPresentationCompleted()
        {
            if (dayCounterGlyphs != null) dayCounterGlyphs.SetVisible(false);
            if (dayText != null) dayText.gameObject.SetActive(false);
            if (dayCounterScrollRect != null) dayCounterScrollRect.gameObject.SetActive(false);
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

        private void RefreshSunsetWarning()
        {
            var timeService = bootstrap?.TimeService;
            if (timeService == null || dayClockText == null) return;
            var warning = IsSunsetWarningWindow(
                timeService.IsNight, timeService.SecondsUntilNextTransition);
            var bright = warning && IsSunsetWarningBrightPhase(timeService.GameSeconds);
            var clockColor = !warning
                ? new Color(.88f, .93f, 1f, 1f)
                : bright
                    ? new Color(1f, .42f, .16f, 1f)
                    : new Color(1f, .68f, .34f, .5f);
            dayClockText.color = clockColor;
            dayClockGlyphs?.SetColor(clockColor);
            if (dayNightClockArt != null)
                dayNightClockArt.color = !warning
                    ? Color.white
                    : bright
                        ? new Color(1f, .42f, .16f, 1f)
                        : new Color(1f, .68f, .34f, .5f);
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

        private void BuildBossHealthBar()
        {
            if (bossStatusText == null) return;
            if (bossHealthBarRoot == null || bossHealthPortrait == null || bossHealthValueText == null)
            {
                Debug.LogError("[Nyangbingo] MainGameHudController: BossHealthBar 하이어라키가 인스펙터에 배선되지 않았습니다.");
                return;
            }
            bossStatusDefaultPosition = bossStatusText.rectTransform.anchoredPosition;
            bossStatusDefaultFontSize = bossStatusText.fontSize;
            hasBossStatusDefaultLayout = true;
            bossStatusText.alignment = TextAnchor.UpperCenter;

            var valueObject = bossHealthValueText.gameObject;
            if (gameplayArtCatalog?.ShellNumberGlyphs.Count == RuntimePixelGlyphPresenter.ExpectedGlyphCount)
            {
                bossHealthValueText.text = string.Empty;
                bossHealthValueText.enabled = false;
                bossHealthValueGlyphs = valueObject.GetComponent<RuntimePixelGlyphPresenter>() ??
                                        valueObject.AddComponent<RuntimePixelGlyphPresenter>();
                bossHealthValueGlyphs.ConfigureForRuntime(gameplayArtCatalog.ShellNumberGlyphs,
                    BossHealthValueGlyphScale);
            }

            ResizeBossHealthBar(1f);
            bossHealthBarRoot.SetActive(false);

            if (bossEntranceFlashRoot != null)
            {
                bossEntranceFlashRoot.transform.SetAsLastSibling();
                bossEntranceFlashRoot.SetActive(false);
            }
        }

        private void RestoreBossStatusLayout()
        {
            if (!hasBossStatusDefaultLayout || bossStatusText == null) return;
            bossStatusText.fontSize = bossStatusDefaultFontSize;
            bossStatusText.rectTransform.anchoredPosition = bossStatusDefaultPosition;
        }

        private void ConfigureBossHealthVerticalLayout(string bossId)
        {
            var verticalOffset = BossHealthContentVerticalOffset(bossId);
            for (var index = 0; index < bossHealthSegmentRects.Length; index++)
            {
                var rect = bossHealthSegmentRects[index];
                if (rect == null) continue;
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, verticalOffset);
            }
            if (bossHealthValueRect != null)
            {
                bossHealthValueRect.anchoredPosition =
                    new Vector2(0f, verticalOffset + BossHealthValueVerticalNudge);
                bossHealthValueRect.localScale = Vector3.one * BossHealthValueScale(bossId);
            }
        }

        public static float BossHealthValueScale(string bossId) => bossId == "imugi_boss" ? .85f : 1f;

        public static float BossHealthContentVerticalOffset(string bossId) =>
            bossId switch
            {
                "mother_bulgasari" => -6.75f,
                "king_dokkaebi" => -4.125f,
                "imugi_boss" => -6f,
                _ => -3.75f
            };

        private Sprite ResolveBossHealthArt(string bossId)
        {
            if (runtimeBossHealthSpriteCache.TryGetValue(bossId, out var cached) && cached != null)
                return cached;

            // boss_health_frame is the authoritative four-row sheet. The per-boss Aseprite
            // assets contain the same sheet with different canvas offsets, so cropping those
            // imports again can select another boss after a reimport.
            // Cropped once per bossId and cached for the component's lifetime (see cache
            // cleanup in OnDestroy) instead of re-cropping every time the active boss changes.
            var source = gameplayArtCatalog?.BossHealthFrame ?? BossHealthArtSource(bossId);
            if (source == null) return null;

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
            var cropped = Sprite.Create(source.texture, croppedRect, new Vector2(.5f, .5f),
                source.pixelsPerUnit, 0, SpriteMeshType.FullRect, Vector4.zero, false);
            cropped.name = $"{bossId}_health_bar_runtime";
            runtimeBossHealthSpriteCache[bossId] = cropped;
            return cropped;
        }

        public static int BossHealthArtRow(string bossId)
        {
            switch (bossId)
            {
                // Unity's Aseprite textureRect exposes the delivered vertical sheet from
                // bottom to top, so the runtime crop order is the reverse of the source view.
                case "king_dokkaebi": return 0;
                case "mother_bulgasari": return 1;
                case "imugi_boss": return 2;
                default: return -1;
            }
        }

        private Sprite BossHealthArtSource(string bossId)
        {
            switch (bossId)
            {
                case "king_dokkaebi": return gameplayArtCatalog?.BossHealthKingDokkaebi;
                case "mother_bulgasari": return gameplayArtCatalog?.BossHealthMotherBulgasari;
                case "imugi_boss": return gameplayArtCatalog?.BossHealthImugi;
                default: return null;
            }
        }

        private void HandleBossStarted(BossDefinition _)
        {
            bossFleeRollRemaining = 0f;
            if (bossHealthBarRoot != null) bossHealthBarRoot.transform.localScale = Vector3.one;
            if (baekjungHudActive) baekjungHudSuppressedForBoss = true;
            RefreshBaekjungDayCounterFeedback();
            if (bossEntranceFlash == null) return;
            bossEntranceFlashRemaining = BossEntranceFlashDuration;
            bossEntranceFlashRoot?.transform.SetAsLastSibling();
            bossEntranceFlashRoot?.SetActive(true);
            RefreshBossEntranceFlash();
        }

        private void HandleBossEnded(BossDefinition _, bool defeated)
        {
            bossEntranceFlashRemaining = 0f;
            bossEntranceFlashRoot?.SetActive(false);
            bossFleeRollRemaining = defeated ? 0f : BossFleeRollSeconds;
            if (defeated && bossHealthBarRoot != null)
            {
                bossHealthBarRoot.transform.localScale = Vector3.one;
                bossHealthBarRoot.SetActive(false);
            }
        }

        private void RefreshBossEntranceFlash()
        {
            if (bossEntranceFlashRoot == null || bossEntranceFlash == null) return;
            if (bossEntranceFlashRemaining <= 0f)
            {
                bossEntranceFlash.color = Color.clear;
                bossEntranceFlashRoot.SetActive(false);
                return;
            }

            var elapsed = BossEntranceFlashDuration - bossEntranceFlashRemaining;
            bossEntranceFlash.color = BossEntranceFlashColor(elapsed);
            bossEntranceFlashRoot.SetActive(bossEntranceFlash.color.a > 0f);
        }

        public static Color BossEntranceFlashColor(float elapsed)
        {
            if (elapsed < 0f || elapsed >= BossEntranceFlashDuration) return Color.clear;
            if (elapsed < .10f) return new Color(.08f, 0f, .015f, .78f);
            if (elapsed < .20f) return Color.clear;
            if (elapsed < .34f) return new Color(.12f, .005f, .02f, .58f);
            if (elapsed < .47f) return Color.clear;
            if (elapsed < .66f) return new Color(.035f, 0f, .01f, .82f);
            if (elapsed < .79f) return Color.clear;
            if (elapsed < .98f) return new Color(.1f, 0f, .025f, .62f);
            if (elapsed < 1.07f) return Color.clear;
            var fade = 1f - Mathf.InverseLerp(1.07f, BossEntranceFlashDuration, elapsed);
            return new Color(.025f, 0f, .008f, .48f * fade);
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
            if (bossHealthBarRoot == null || bossHealthBarFills.Length == 0) return;
            ratio = Mathf.Clamp01(ratio);
            var totalWidth = 0f;
            for (var index = 0; index < BossHealthSegmentWidths.Length; index++)
                totalWidth += BossHealthSegmentWidths[index];
            var remainingWidth = totalWidth * ratio;
            for (var index = 0; index < bossHealthBarFills.Length; index++)
            {
                var segmentWidth = BossHealthSegmentWidths[index];
                var visibleWidth = Mathf.Clamp(remainingWidth, 0f, segmentWidth);
                var fill = bossHealthBarFills[index];
                if (fill == null) continue;
                var fillRect = fill.rectTransform;
                fillRect.sizeDelta = new Vector2(visibleWidth, BossHealthSegmentHeight);
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
            foreach (var sprite in runtimeBossHealthSpriteCache.Values)
                if (sprite != null) Destroy(sprite);
            runtimeBossHealthSpriteCache.Clear();
            if (bootstrap?.TimeService != null) bootstrap.TimeService.Dawn -= HandleDayCounterDawn;
            if (dayCounterScrollPresenter != null)
                dayCounterScrollPresenter.PresentationCompleted -= HandleDayCounterPresentationCompleted;
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
