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

        private YokaiCodexPresentationModel model;
        private float resumeTimeScale = 1f;
        private bool open;

        public int BoundCardCount => cardButtons?.Length ?? 0;
        public bool IsOpen => open;
        public void ConfigureGameShell(GameShellController value) => gameShell = value;

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

            model = saveCoordinator.ProgressTracker.CreateCodexPresentationModel();
            for (var index = 0; index < cardButtons.Length; index++)
            {
                var capturedIndex = index;
                cardButtons[index].onClick.AddListener(() => HandleCardClicked(capturedIndex));
            }
            panel.SetActive(false);
            RefreshView();
            Debug.Log("[Nyangbingo] MainGameCodexController: 8장 도감 UI 연결 완료 (Tab 열기/닫기).");
        }

        private void Update()
        {
            if (model == null) return;
            if (Input.GetKeyDown(KeyCode.Tab) && (open || gameShell == null ||
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

        private void RefreshView()
        {
            if (model == null) return;
            for (var index = 0; index < cardTexts.Length; index++)
            {
                var card = model.Cards[index];
                cardTexts[index].text = card.IsUnlocked
                    ? $"{card.DisplayName}\n{(card.IsBoss ? "보스" : "요괴")} · 처치 {card.KillCount}"
                    : "먹빛 실루엣\n미해금";
                cardButtons[index].interactable = true;
            }

            var selected = model.SelectedCard;
            if (selected == null)
            {
                detailText.text = "카드를 선택하세요.\n같은 카드를 다시 누르면 앞·뒤를 전환합니다.";
                return;
            }
            if (!selected.IsUnlocked)
            {
                detailText.text = "미해금 요괴\n처치 후 기록이 공개됩니다.";
                return;
            }
            detailText.text = model.IsBackVisible
                ? $"{selected.DisplayName}\n\n{selected.AppearanceHint}\n\n{selected.SourceText}"
                : $"{selected.DisplayName}\n\n처치 횟수: {selected.KillCount}" +
                  (selected.FirstKillDay > 0 ? $"\n최초 처치: {selected.FirstKillDay}일" : string.Empty);
        }

        private void OnDestroy()
        {
            if (open) Time.timeScale = resumeTimeScale;
            if (cardButtons == null) return;
            for (var index = 0; index < cardButtons.Length; index++)
                if (cardButtons[index] != null) cardButtons[index].onClick.RemoveAllListeners();
        }
    }
}
