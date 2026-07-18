using Nyangbingo.Core;
using Nyangbingo.Combat;
using Nyangbingo.Bosses;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using Nyangbingo.World;
using UnityEngine;
using UnityEngine.UI;
using PlayerInventory = Nyangbingo.Inventory.Inventory;

namespace Nyangbingo.UI
{
    [DefaultExecutionOrder(-60)]
    public sealed class MainGameHudController : MonoBehaviour
    {
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
        [SerializeField] private Text[] inventorySlotTexts = new Text[PlayerInventory.SlotCount];
        [SerializeField] private Image[] inventorySlotIcons = new Image[PlayerInventory.SlotCount];
        [SerializeField] private ItemArtCatalog itemArtCatalog;
        private PlayerInventory inventory;

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
            inventory.Changed += RefreshInventory;
            if (playerHealth != null)
            {
                playerHealth.Damaged += HandlePlayerDamaged;
                playerHealth.Died += HandlePlayerDied;
            }
            GameEvents.OnSealChanged += RefreshStatus;
            if (deathPanel != null) deathPanel.SetActive(playerHealth != null && playerHealth.IsDead);
            RefreshInventory();
            RefreshStatus();
            Debug.Log("[Nyangbingo] MainGameHudController: 체온·석빙고 온도·D-100·발톱 티어·12슬롯 HUD 연결 완료.");
        }

        private void LateUpdate() => RefreshStatus();

        private void RefreshStatus()
        {
            if (bootstrap == null || runtimeServices == null) return;
            if (temperatureText != null) temperatureText.text = $"체온 {runtimeServices.PlayerTemperature.Current:0.0}";
            RefreshTemperatureArt();
            if (sealText != null) sealText.text = $"석빙고 {bootstrap.SealSystem?.TemperaturePercent ?? 0f:0}%";
            if (dayText != null) dayText.text = $"D-{bootstrap.TimeService.DaysRemaining}";
            if (clawText != null) clawText.text = $"발톱 T{ResolveClawTier()}";
            if (playerHealthText != null && playerHealth != null)
                playerHealthText.text = $"HP {playerHealth.Current}/{playerHealth.MaxHealth}";
            RefreshBossStatus();
            RefreshCraftingProgress();
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
#if UNITY_EDITOR
                bossStatusText.text = bootstrap?.TimeService?.IsNight == true
                    ? "F8 도깨비 대왕  ·  Shift+F8 강철이 테스트"
                    : "보스는 밤에 출현";
#else
                bossStatusText.text = string.Empty;
#endif
                return;
            }

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
        }
    }
}
