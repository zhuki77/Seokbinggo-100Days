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
        [SerializeField] private Text[] inventorySlotTexts = new Text[PlayerInventory.SlotCount];
        [SerializeField] private Image[] inventorySlotIcons = new Image[PlayerInventory.SlotCount];
        [SerializeField] private ItemArtCatalog itemArtCatalog;
        private PlayerInventory inventory;
        private GameObject bossHealthBarRoot;
        private Image bossHealthBarFill;
        private MainGameSaveCoordinator saveCoordinator;
        private GoalBadgeProgress goalBadgeProgress;
        private GameObject goalBadgeRoot;
        private readonly Image[] goalBadgeBackgrounds = new Image[3];
        private readonly GameObject[] goalBadgeChecks = new GameObject[3];

        public int BoundSlotCount => inventorySlotTexts?.Length ?? 0;
        public int BoundIconCount => inventorySlotIcons?.Length ?? 0;
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
                !runtimeServices.Initialize() || inventorySlotTexts == null ||
                inventorySlotTexts.Length != PlayerInventory.SlotCount || inventorySlotIcons == null ||
                inventorySlotIcons.Length != PlayerInventory.SlotCount)
            {
                Debug.LogError("[Nyangbingo] MainGameHudController: HUD 데이터 또는 12슬롯 참조가 올바르지 않습니다.");
                enabled = false;
                return;
            }
            inventory = runtimeServices.PlayerInventory;
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
                FindAnyObjectByType<MainGameBossSummonUiController>());
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
            BuildGoalBadges();
            RefreshInventory();
            RefreshStatus();
            Debug.Log("[Nyangbingo] MainGameHudController: 체온·석빙고 온도·D-100·발톱 티어·12슬롯 HUD 연결 완료.");
        }

        private void LateUpdate()
        {
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
            ResizeBossHealthBar(CalculateHealthRatio(health.Current, health.MaxHealth));

            var combat = health.GetComponent<BossCombatController>();
            var state = combat != null && combat.IsTelegraphing
                ? "  ·  특수공격 예고!"
                : combat != null && combat.IsSpecialActive
                    ? "  ·  특수공격 중"
                    : string.Empty;
            bossStatusText.text = $"{definition.DisplayName}  HP {health.Current}/{health.MaxHealth}{state}";
#if UNITY_EDITOR
            bossStatusText.text += "  ·  K 테스트 처치";
#endif
        }

        public static float CalculateHealthRatio(int current, int maximum) =>
            maximum <= 0 ? 0f : Mathf.Clamp01((float)current / maximum);

        private void BuildBossHealthBar()
        {
            if (bossStatusText == null || bossHealthBarRoot != null) return;
            var parent = bossStatusText.rectTransform;
            bossStatusText.alignment = TextAnchor.UpperCenter;

            bossHealthBarRoot = new GameObject("BossHealthBar", typeof(RectTransform));
            var rootRect = (RectTransform)bossHealthBarRoot.transform;
            rootRect.SetParent(parent, false);
            rootRect.anchorMin = rootRect.anchorMax = rootRect.pivot = new Vector2(.5f, 1f);
            rootRect.anchoredPosition = new Vector2(0f, -36f);
            rootRect.sizeDelta = new Vector2(720f, 14f);
            var background = bossHealthBarRoot.AddComponent<Image>();
            background.color = new Color(.055f, .035f, .04f, .94f);
            background.raycastTarget = false;

            var fillObject = new GameObject("Fill", typeof(RectTransform));
            fillObject.transform.SetParent(rootRect, false);
            bossHealthBarFill = fillObject.AddComponent<Image>();
            bossHealthBarFill.color = new Color(.82f, .18f, .14f, 1f);
            bossHealthBarFill.raycastTarget = false;
            ResizeBossHealthBar(1f);
            bossHealthBarRoot.SetActive(false);
        }

        private void ResizeBossHealthBar(float ratio)
        {
            if (bossHealthBarFill == null || bossHealthBarRoot == null) return;
            ratio = Mathf.Clamp01(ratio);
            var rootRect = (RectTransform)bossHealthBarRoot.transform;
            var fillRect = bossHealthBarFill.rectTransform;
            fillRect.anchorMin = fillRect.anchorMax = fillRect.pivot = new Vector2(0f, .5f);
            fillRect.anchoredPosition = new Vector2(2f, 0f);
            fillRect.sizeDelta = new Vector2(Mathf.Max(0f, (rootRect.rect.width - 4f) * ratio),
                Mathf.Max(0f, rootRect.rect.height - 4f));
            bossHealthBarFill.color = ratio > .5f
                ? new Color(.82f, .18f, .14f, 1f)
                : ratio > .25f
                    ? new Color(.95f, .5f, .12f, 1f)
                    : new Color(1f, .15f, .08f, 1f);
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

        private void HandlePlayerDamaged(DamageTag tag, int amount) => RefreshStatus();

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
            GameEvents.OnSealChanged -= RefreshStatus;
            if (goalBadgeProgress != null) goalBadgeProgress.Changed -= RefreshGoalBadges;
        }
    }
}
