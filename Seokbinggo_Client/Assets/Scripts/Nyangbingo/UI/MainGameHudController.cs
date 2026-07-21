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
        [SerializeField] private GameDataCatalog gameDataCatalog;
        [SerializeField] private MainGameBootstrap bootstrap;
        [SerializeField] private MainGameRuntimeServices runtimeServices;
        [SerializeField] private Text temperatureText;
        [SerializeField] private Image temperatureArt;
        [SerializeField] private GameplayArtCatalog gameplayArtCatalog;
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

        public int BoundSlotCount => inventorySlotTexts?.Length ?? 0;
        public int BoundIconCount => inventorySlotIcons?.Length ?? 0;
        public ItemArtCatalog BoundItemArtCatalog => itemArtCatalog;
        public bool HasPlayerStatusBindings => playerHealth != null && playerHealthText != null && deathPanel != null;
        public bool HasCraftingProgressBindings => craftingProgressPanel != null && craftingProgressText != null &&
                                                   craftingProgressFill != null;

        public void ConfigureForScene(GameDataCatalog catalog, MainGameBootstrap mainBootstrap,
            MainGameRuntimeServices services, Text temperature, Text seal, Text day, Text claw, Text healthText,
            Health health, BossManager manager, Text bossText, GameObject playerDeathPanel, Text[] slots,
            Image[] icons, ItemArtCatalog artCatalog, Image temperatureImage, GameplayArtCatalog gameplayArt,
            GameObject craftingPanel, Text craftingText, Image craftingFill)
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
            craftingProgressPanel = craftingPanel;
            craftingProgressText = craftingText;
            craftingProgressFill = craftingFill;
        }

        private void Start()
        {
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
            var canvas = GetComponent<Canvas>() ?? GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                var uiResolution = canvas.GetComponent<MainGameUiResolutionController>() ??
                                   canvas.gameObject.AddComponent<MainGameUiResolutionController>();
                uiResolution.ConfigureForRuntime(canvas);
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
            if (deathPanel != null)
            {
                var deathLabel = deathPanel.GetComponentInChildren<Text>(true);
                if (deathLabel != null) deathLabel.text = "사망\n보금자리로 돌아가는 중…";
                deathPanel.SetActive(playerController != null ? playerController.IsDead : playerHealth != null && playerHealth.IsDead);
            }
            BuildBossHealthBar();
            if (dayText != null)
            {
                dayTextDefaultPosition = dayText.rectTransform.anchoredPosition;
                hasDayTextDefaultPosition = true;
            }
            if (bossManager != null)
            {
                bossManager.BossStarted += HandleBossStarted;
                bossManager.BossEnded += HandleBossEnded;
            }
            BuildGoalBadges();
            BuildStatusArtHud();
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
            saveIndicatorRemaining = Mathf.Max(0f, saveIndicatorRemaining - Time.unscaledDeltaTime);
            tearAnimationRemaining = Mathf.Max(0f, tearAnimationRemaining - Time.unscaledDeltaTime);
            if (bossEntranceArtRoot != null && bossEntranceArtRemaining <= 0f)
                bossEntranceArtRoot.SetActive(false);
            RefreshBellRopeDetection();
            RefreshAlertOverlay();
            RefreshStatus();
            if (deathPanel != null && playerController != null && deathPanel.activeSelf != playerController.IsDead)
                deathPanel.SetActive(playerController.IsDead);
        }

        private void RefreshStatus()
        {
            if (bootstrap == null || runtimeServices == null) return;
            if (temperatureText != null) temperatureText.text = $"체온 {runtimeServices.PlayerTemperature.Current:0.0}";
            RefreshTemperatureArt();
            if (sealText != null)
                sealText.text = $"석빙고 {runtimeServices.PlayerTemperature.EffectiveCoolingPercent:0}%";
            if (dayText != null) dayText.text = $"D-{bootstrap.TimeService.DaysRemaining}";
            if (clawText != null) clawText.text = $"발톱 T{ResolveClawTier()}";
            if (playerHealthText != null && playerHealth != null)
                playerHealthText.text = $"HP {playerHealth.Current}/{playerHealth.MaxHealth}";
            RefreshBossStatus();
            RefreshCraftingProgress();
            RefreshGoalBadges();
            RefreshStatusArtHud();
        }

        private void BuildStatusArtHud()
        {
            if (statusArtRoot != null) return;
            if (temperatureArt != null) temperatureArt.enabled = false;

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
            ReparentStatusText(temperatureText, root, new Vector2(18f, -20f), new Vector2(38f, 8f), 7);

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
                    fuelGaugeText.text = $"{Mathf.CeilToInt(remaining)}s";
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
                SetAlertOverlay(new Color(.72f, .03f, .02f, .2f), "피격!", new Color(1f, .75f, .72f), false);
                return;
            }
            if (bellWarningRemaining > 0f)
            {
                SetAlertOverlay(new Color(.72f, .34f, .02f, .1f), "방울 금줄 경보 · 침입자 접근",
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
            alertOverlayText.text = message;
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
            var itemName = recipe.Output.item != null ? recipe.Output.item.DisplayName : recipe.Id;
            craftingProgressText.text = remaining <= .0001f
                ? $"{itemName} 제작 완료 · 인벤토리 공간 대기"
                : $"제작 중 · {itemName}  {remaining:0.0}초";
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
            if (statusArtRoot != null)
            {
                temperatureArt.enabled = false;
                return;
            }
            var frames = gameplayArtCatalog?.TemperatureFrames;
            if (frames == null || frames.Count == 0)
            {
                temperatureArt.enabled = false;
                return;
            }

            var index = Mathf.RoundToInt(runtimeServices.PlayerTemperature.Normalized * (frames.Count - 1));
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
                if (bossHealthBarRoot != null) bossHealthBarRoot.SetActive(false);
                RestoreDayCounterPosition();
#if UNITY_EDITOR
                bossStatusText.text = bootstrap?.TimeService?.IsNight == true
                    ? "F8 도깨비 대왕  ·  Shift+F8 강철이 테스트"
                    : "보스는 밤에 출현";
#else
                bossStatusText.text = string.Empty;
#endif
                return;
            }

            if (bossHealthBarRoot != null) bossHealthBarRoot.SetActive(true);
            MoveDayCounterBelowBossBar();
            if (bossHealthPortrait != null)
            {
                bossHealthPortrait.sprite = ResolveBossHealthArt(definition.Id);
                bossHealthPortrait.enabled = bossHealthPortrait.sprite != null;
            }
            ConfigureBossHealthVerticalLayout(definition.Id);
            ResizeBossHealthBar(CalculateHealthRatio(health.Current, health.MaxHealth));

            // v15 QA-E 무텍스트 규칙: 보스 이름은 전용 먹선 초상/프레임이 대신한다.
            // 숫자는 허용되므로 체력 정보만 남긴다.
            bossStatusText.text = $"HP {health.Current}/{health.MaxHealth}";
#if UNITY_EDITOR
            bossStatusText.text += "  ·  K 테스트 처치";
#endif
        }

        public static float CalculateHealthRatio(int current, int maximum) =>
            maximum <= 0 ? 0f : Mathf.Clamp01((float)current / maximum);

        private void MoveDayCounterBelowBossBar()
        {
            if (dayText == null || !hasDayTextDefaultPosition) return;
            dayText.rectTransform.anchoredPosition = new Vector2(dayTextDefaultPosition.x, -58f);
        }

        private void RestoreDayCounterPosition()
        {
            if (dayText == null || !hasDayTextDefaultPosition) return;
            dayText.rectTransform.anchoredPosition = dayTextDefaultPosition;
        }

        private void BuildBossHealthBar()
        {
            if (bossStatusText == null || bossHealthBarRoot != null) return;
            bossStatusText.alignment = TextAnchor.UpperCenter;

            bossHealthBarRoot = new GameObject("BossHealthBar", typeof(RectTransform));
            var rootRect = (RectTransform)bossHealthBarRoot.transform;
            var nativeRoot = MainGameUiResolutionController.ResolveNativeRoot(transform) ?? transform;
            rootRect.SetParent(nativeRoot, false);
            rootRect.anchorMin = rootRect.anchorMax = rootRect.pivot = new Vector2(.5f, 1f);
            rootRect.anchoredPosition = new Vector2(0f, -22f);
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
                "king_dokkaebi" => -5f,
                "gangcheol_boss" => -2.75f,
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

            var source = BossHealthArtSource(bossId);
            if (source == null) return null;
            if (runtimeBossHealthSprite != null) Destroy(runtimeBossHealthSprite);

            var sourceRect = source.textureRect;
            float topStart;
            float topEnd;
            switch (bossId)
            {
                case "gangcheol_boss": topStart = 0f; topEnd = 28f; break;
                case "imugi": topStart = 28f; topEnd = 60f; break;
                case "mother_bulgasari": topStart = 60f; topEnd = 92f; break;
                case "king_dokkaebi": topStart = 92f; topEnd = sourceRect.height; break;
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
            if (goalBadgeProgress != null) goalBadgeProgress.Changed -= RefreshGoalBadges;
            if (hudSaveManager != null) hudSaveManager.Saved -= HandleSaved;
        }
    }
}
