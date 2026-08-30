using Nyangbingo.Data;
using Nyangbingo.Save;
using UnityEngine;
using UnityEngine.UI;

namespace Nyangbingo.UI
{
    [DefaultExecutionOrder(-50)]
    public sealed class MainGameCodexController : MonoBehaviour
    {
        [SerializeField] private MainGameSaveCoordinator saveCoordinator;
        [SerializeField] private GameObject panel;
        [SerializeField] private Button[] cardButtons = new Button[YokaiCodexPresentationModel.ExpectedCardCount];
        [SerializeField] private Text[] cardTexts = new Text[YokaiCodexPresentationModel.ExpectedCardCount];
        [SerializeField] private Text detailText;
        [SerializeField] private GameShellController gameShell;
        [SerializeField] private GameplayArtCatalog gameplayArtCatalog;
        [SerializeField] private CharacterArtCatalog characterArtCatalog;

        [SerializeField] private Image[] gridPortraits = new Image[YokaiCodexPresentationModel.ExpectedCardCount];
        [SerializeField] private GameObject expandedBackdrop;
        [SerializeField] private Image expandedCardBackground;
        [SerializeField] private Image expandedPortrait;
        [SerializeField] private Text expandedTitle;
        [SerializeField] private Text expandedFrontText;
        [SerializeField] private Text expandedBackText;
        [SerializeField] private Text expandedHintText;
        [SerializeField] private Button expandedCardButton;
        [SerializeField] private Button expandedBackdropButton;

        private YokaiCodexPresentationModel model;
        private float resumeTimeScale = 1f;
        private bool open;
        private bool unifiedPanelMode;

        public int BoundCardCount => cardButtons?.Length ?? 0;

        /// <summary>인스펙터에 수동 배선되는 납품 도감 확대 카드·아트 카탈로그가 살아 있는지.</summary>
        public bool HasDeliveredCodexBindings =>
            gameplayArtCatalog != null && characterArtCatalog != null &&
            expandedBackdrop != null && expandedPortrait != null && expandedTitle != null;
        public bool IsOpen => open;
        public void ConfigureGameShell(GameShellController value) => gameShell = value;
        public void UseUnifiedPanel()
        {
            unifiedPanelMode = true;
            if (open) SetOpen(false);
            else if (panel != null) panel.SetActive(false);
        }

        public void ConfigureForScene(MainGameSaveCoordinator coordinator, GameObject codexPanel,
            Button[] buttons, Text[] labels, Text details)
        {
            saveCoordinator = coordinator;
            panel = codexPanel;
            cardButtons = buttons;
            cardTexts = labels;
            detailText = details;
        }

        private void Start()
        {
            if (saveCoordinator == null || !saveCoordinator.Initialize() || saveCoordinator.ProgressTracker == null ||
                panel == null || detailText == null || cardButtons == null || cardTexts == null ||
                cardButtons.Length != YokaiCodexPresentationModel.ExpectedCardCount ||
                cardTexts.Length != YokaiCodexPresentationModel.ExpectedCardCount)
            {
                Debug.LogError("[Nyangbingo] MainGameCodexController: 도감 데이터 또는 8장 카드 UI 배선이 올바르지 않습니다.");
                enabled = false;
                return;
            }

            if (gameplayArtCatalog == null || characterArtCatalog == null)
                Debug.LogError("[Nyangbingo] MainGameCodexController: gameplayArtCatalog/characterArtCatalog 배선이 비어 있습니다.");

            model = saveCoordinator.ProgressTracker.CreateCodexPresentationModel();
            ConfigureNativeGrid();
            BuildExpandedView();
            for (var index = 0; index < cardButtons.Length; index++)
            {
                var capturedIndex = index;
                RuntimeUiButtonArt.ApplyCodexCard(cardButtons[index], gameplayArtCatalog);
                cardButtons[index].onClick.AddListener(() => HandleCardClicked(capturedIndex));
            }
            panel.SetActive(false);
            RefreshView();
            Debug.Log("[Nyangbingo] MainGameCodexController: 3열 8장 격자와 192x256 확대·뒤집기 도감 연결 완료.");
        }

        private void Update()
        {
            if (model == null || unifiedPanelMode) return;
            if ((Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) &&
                (open || gameShell == null ||
                                                  gameShell.Screen == GameShellScreen.Gameplay)) SetOpen(!open);
            else if (open && Input.GetKeyDown(KeyCode.Escape)) SetOpen(false);
        }

        private void SetOpen(bool value)
        {
            if (open == value || panel == null) return;
            open = value;
            if (open)
            {
                resumeTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
                Time.timeScale = 0f;
                model.Refresh();
                RefreshView();
            }
            else
            {
                model.TapOutside();
                Time.timeScale = resumeTimeScale;
            }
            panel.SetActive(open);
        }

        private void HandleCardClicked(int index)
        {
            if (!open || model == null || index < 0 || index >= model.Cards.Count) return;
            model.TryTapCard(model.Cards[index].EntryId);
            RefreshView();
        }

        private void HandleExpandedCardClicked()
        {
            if (!open || model == null || model.SelectedCard == null) return;
            model.TryFlipSelected();
            RefreshView();
        }

        private void HandleExpandedBackdropClicked()
        {
            if (!open || model == null) return;
            model.TapOutside();
            RefreshView();
        }

        private void RefreshView()
        {
            if (model == null) return;
            for (var index = 0; index < cardTexts.Length; index++)
            {
                var card = model.Cards[index];
                cardTexts[index].text = card.IsUnlocked
                    ? $"{card.DisplayName}\n{(card.IsBoss ? "보스" : "요괴")} · 처치 {card.KillCount}"
                    : "?\n미확인";
                cardTexts[index].fontSize = 8;
                cardButtons[index].interactable = true;
                if (cardButtons[index].image != null)
                    cardButtons[index].image.color = card.IsUnlocked
                        ? new Color(.16f, .2f, .24f, 1f)
                        : new Color(.09f, .1f, .12f, 1f);

                var portrait = gridPortraits[index];
                if (portrait != null)
                {
                    portrait.sprite = FindPortrait(card.EntryId);
                    portrait.enabled = portrait.sprite != null;
                    portrait.color = card.UsesInkSilhouette
                        ? new Color(.025f, .03f, .035f, 1f)
                        : Color.white;
                }
            }

            var selected = model.SelectedCard;
            if (selected == null)
            {
                if (expandedBackdrop != null) expandedBackdrop.SetActive(false);
                return;
            }

            if (expandedBackdrop != null) expandedBackdrop.SetActive(true);
            expandedTitle.text = selected.IsUnlocked ? selected.DisplayName : "?";
            expandedPortrait.sprite = FindPortrait(selected.EntryId);
            expandedPortrait.enabled = expandedPortrait.sprite != null && !model.IsBackVisible;
            expandedPortrait.color = selected.UsesInkSilhouette
                ? new Color(.02f, .025f, .03f, 1f)
                : Color.white;

            if (!selected.IsUnlocked)
            {
                expandedCardBackground.color = new Color(.1f, .11f, .12f, 1f);
                expandedFrontText.gameObject.SetActive(true);
                expandedBackText.gameObject.SetActive(false);
                expandedFrontText.text = "미확인 개체\n처치 후 기록이 공개됩니다.";
                expandedHintText.text = "바깥 클릭 · 격자로 돌아가기";
                return;
            }

            expandedCardBackground.color = model.IsBackVisible
                ? new Color(.16f, .11f, .075f, 1f)
                : new Color(.12f, .16f, .19f, 1f);
            expandedFrontText.gameObject.SetActive(!model.IsBackVisible);
            expandedBackText.gameObject.SetActive(model.IsBackVisible);
            expandedFrontText.text = $"{(selected.IsBoss ? "보스" : "요괴")} · 처치 {selected.KillCount}" +
                                     (selected.FirstKillDay > 0 ? $"\n최초 처치 {selected.FirstKillDay}일" : string.Empty) +
                                     (string.IsNullOrWhiteSpace(selected.AppearanceHint)
                                         ? string.Empty
                                         : $"\n{selected.AppearanceHint}");
            expandedBackText.text = selected.SourceText;
            expandedHintText.text = model.IsBackVisible
                ? "카드 클릭 · 앞면 보기    |    바깥 클릭 · 격자"
                : "카드 클릭 · 전승 보기    |    바깥 클릭 · 격자";
        }

        private Sprite FindPortrait(string entryId) => characterArtCatalog != null
            ? characterArtCatalog.FindSprite(entryId)
            : null;

        private void ConfigureNativeGrid()
        {
            if (gridPortraits == null || gridPortraits.Length != YokaiCodexPresentationModel.ExpectedCardCount)
                Debug.LogError("[Nyangbingo] MainGameCodexController: 도감 격자 초상화 배선이 올바르지 않습니다.");
            detailText.gameObject.SetActive(false);
        }

        private void BuildExpandedView()
        {
            if (expandedBackdrop == null || expandedCardBackground == null || expandedCardButton == null ||
                expandedBackdropButton == null)
            {
                Debug.LogError("[Nyangbingo] MainGameCodexController: 확대 카드 하이어라키가 인스펙터에 배선되지 않았습니다.");
                return;
            }
            RuntimeUiButtonArt.ApplyCodexCard(expandedCardBackground, gameplayArtCatalog);
            expandedBackdropButton.onClick.AddListener(HandleExpandedBackdropClicked);
            expandedCardButton.onClick.AddListener(HandleExpandedCardClicked);
            expandedBackdrop.transform.SetAsLastSibling();
            expandedBackdrop.SetActive(false);
        }

        private void OnDestroy()
        {
            if (open) Time.timeScale = resumeTimeScale;
            if (expandedCardButton != null) expandedCardButton.onClick.RemoveListener(HandleExpandedCardClicked);
            if (expandedBackdropButton != null)
                expandedBackdropButton.onClick.RemoveListener(HandleExpandedBackdropClicked);
            if (cardButtons == null) return;
            for (var index = 0; index < cardButtons.Length; index++)
                if (cardButtons[index] != null) cardButtons[index].onClick.RemoveAllListeners();
        }
    }
}
