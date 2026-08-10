using System.Collections.Generic;
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

        private YokaiCodexPresentationModel model;
        private CharacterArtCatalog characterArtCatalog;
        private readonly Image[] gridPortraits = new Image[YokaiCodexPresentationModel.ExpectedCardCount];
        private GameObject expandedBackdrop;
        private Image expandedCardBackground;
        private Image expandedPortrait;
        private Text expandedTitle;
        private Text expandedFrontText;
        private Text expandedBackText;
        private Text expandedHintText;
        private Button expandedCardButton;
        private Button expandedBackdropButton;
        private float resumeTimeScale = 1f;
        private bool open;
        private bool unifiedPanelMode;

        public int BoundCardCount => cardButtons?.Length ?? 0;
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
            if (saveCoordinator == null)
            {
                Debug.LogError("[Nyangbingo] MainGameCodexController: SaveCoordinator 배선이 없습니다.");
                enabled = false;
                return;
            }

            if (!saveCoordinator.Initialize() || saveCoordinator.ProgressTracker == null)
            {
                Debug.LogError("[Nyangbingo] MainGameCodexController: 저장/진행 트랙커 초기화에 실패했습니다.");
                enabled = false;
                return;
            }

            EnsureCardUiBindings();
            if (panel == null || detailText == null || !HasCompleteCardBindings())
            {
                Debug.LogError(
                    "[Nyangbingo] MainGameCodexController: 도감 패널 또는 " +
                    $"{YokaiCodexPresentationModel.ExpectedCardCount}장 카드 UI 배선이 올바르지 않습니다 " +
                    $"(buttons={cardButtons?.Length ?? 0}, texts={cardTexts?.Length ?? 0}).");
                enabled = false;
                return;
            }

            try
            {
                model = saveCoordinator.ProgressTracker.CreateCodexPresentationModel();
            }
            catch (System.Exception exception)
            {
                Debug.LogError("[Nyangbingo] MainGameCodexController: 도감 모델 생성 실패 — " +
                               exception.Message);
                enabled = false;
                return;
            }

            ResolveCharacterArtCatalog();
            ResolveGameplayArtCatalog();
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
            Debug.Log(
                $"[Nyangbingo] MainGameCodexController: 3열 {YokaiCodexPresentationModel.ExpectedCardCount}장 " +
                "격자와 192x256 확대·뒤집기 도감 연결 완료.");
        }

        private bool HasCompleteCardBindings()
        {
            var expected = YokaiCodexPresentationModel.ExpectedCardCount;
            if (cardButtons == null || cardTexts == null ||
                cardButtons.Length != expected || cardTexts.Length != expected)
                return false;
            for (var index = 0; index < expected; index++)
                if (cardButtons[index] == null || cardTexts[index] == null)
                    return false;
            return true;
        }

        /// <summary>
        /// 구 씬은 8장 카드만 직렬화되어 있을 수 있다. CardGrid 자식을 모아 부족한 칸을 런타임에 보충한다.
        /// </summary>
        private void EnsureCardUiBindings()
        {
            if (panel == null) return;
            var window = panel.transform.Find("Window");
            var grid = window != null ? window.Find("CardGrid") as RectTransform : null;
            if (grid == null) return;

            var buttons = new List<Button>(YokaiCodexPresentationModel.ExpectedCardCount);
            var labels = new List<Text>(YokaiCodexPresentationModel.ExpectedCardCount);
            for (var index = 0; index < grid.childCount; index++)
            {
                var child = grid.GetChild(index);
                var button = child.GetComponent<Button>();
                var label = child.GetComponentInChildren<Text>(true);
                if (button == null || label == null) continue;
                buttons.Add(button);
                labels.Add(label);
            }

            var font = detailText != null && detailText.font != null
                ? detailText.font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            while (buttons.Count < YokaiCodexPresentationModel.ExpectedCardCount)
            {
                var cardIndex = buttons.Count;
                var cardObject = new GameObject($"Card_{cardIndex + 1:00}", typeof(RectTransform));
                cardObject.transform.SetParent(grid, false);
                var cardImage = cardObject.AddComponent<Image>();
                cardImage.color = new Color(.17f, .21f, .25f, 1f);
                var button = cardObject.AddComponent<Button>();
                var labelObject = new GameObject("Label", typeof(RectTransform));
                labelObject.transform.SetParent(cardObject.transform, false);
                var label = labelObject.AddComponent<Text>();
                label.font = font;
                label.fontSize = 13;
                label.alignment = TextAnchor.MiddleCenter;
                label.color = new Color(.94f, .96f, 1f, 1f);
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = new Vector2(3f, 3f);
                label.rectTransform.offsetMax = new Vector2(-3f, -3f);
                buttons.Add(button);
                labels.Add(label);
            }

            cardButtons = buttons.GetRange(0, YokaiCodexPresentationModel.ExpectedCardCount).ToArray();
            cardTexts = labels.GetRange(0, YokaiCodexPresentationModel.ExpectedCardCount).ToArray();
        }

        private void Update()
        {
            if (model == null || unifiedPanelMode) return;
            if (Input.GetKeyDown(KeyCode.F4) && (open || gameShell == null ||
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

        private void ResolveCharacterArtCatalog()
        {
            var loadedCatalogs = Resources.FindObjectsOfTypeAll<CharacterArtCatalog>();
            for (var index = 0; index < loadedCatalogs.Length; index++)
            {
                if (loadedCatalogs[index] == null) continue;
                characterArtCatalog = loadedCatalogs[index];
                if (loadedCatalogs[index].name == "CharacterArtCatalog") break;
            }
        }

        private void ResolveGameplayArtCatalog()
        {
            if (gameplayArtCatalog != null) return;
            var loadedCatalogs = Resources.FindObjectsOfTypeAll<GameplayArtCatalog>();
            for (var index = 0; index < loadedCatalogs.Length; index++)
            {
                if (loadedCatalogs[index] == null) continue;
                gameplayArtCatalog = loadedCatalogs[index];
                if (loadedCatalogs[index].name == "GameplayArtCatalog") break;
            }
        }

        private Sprite FindPortrait(string entryId) => characterArtCatalog != null
            ? characterArtCatalog.FindSprite(entryId)
            : null;

        private void ConfigureNativeGrid()
        {
            var window = panel.transform.Find("Window") as RectTransform;
            var grid = window != null ? window.Find("CardGrid") as RectTransform : null;
            if (window == null || grid == null)
            {
                Debug.LogError("[Nyangbingo] MainGameCodexController: 도감 Window 또는 CardGrid를 찾지 못했습니다.");
                return;
            }

            window.anchorMin = window.anchorMax = window.pivot = new Vector2(.5f, .5f);
            window.anchoredPosition = Vector2.zero;
            window.sizeDelta = new Vector2(470f, 260f);

            var title = window.Find("Title")?.GetComponent<Text>();
            if (title != null)
            {
                title.text = "요괴 도감  ·  카드 클릭 확대";
                title.fontSize = 14;
                title.alignment = TextAnchor.MiddleCenter;
                title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(.5f, 1f);
                title.rectTransform.pivot = new Vector2(.5f, 1f);
                title.rectTransform.anchoredPosition = new Vector2(0f, -3f);
                title.rectTransform.sizeDelta = new Vector2(420f, 20f);
            }

            detailText.gameObject.SetActive(false);
            var viewportObject = new GameObject("GridViewport", typeof(RectTransform), typeof(Image),
                typeof(RectMask2D), typeof(ScrollRect));
            viewportObject.transform.SetParent(window, false);
            var viewport = (RectTransform)viewportObject.transform;
            viewport.anchorMin = viewport.anchorMax = viewport.pivot = new Vector2(.5f, .5f);
            viewport.anchoredPosition = new Vector2(0f, -10f);
            viewport.sizeDelta = new Vector2(244f, 226f);
            var viewportImage = viewportObject.GetComponent<Image>();
            viewportImage.color = new Color(.025f, .035f, .045f, .72f);

            grid.SetParent(viewport, false);
            grid.anchorMin = grid.anchorMax = grid.pivot = new Vector2(.5f, 1f);
            grid.anchoredPosition = Vector2.zero;
            var rows = Mathf.CeilToInt((float)YokaiCodexPresentationModel.ExpectedCardCount /
                                       YokaiCodexPresentationModel.GridColumns);
            const float spacing = 8f;
            grid.sizeDelta = new Vector2(
                YokaiCodexPresentationModel.GridColumns * YokaiCodexPresentationModel.GridCardSize.x +
                (YokaiCodexPresentationModel.GridColumns - 1) * spacing,
                rows * YokaiCodexPresentationModel.GridCardSize.y + (rows - 1) * spacing);
            var layout = grid.GetComponent<GridLayoutGroup>();
            layout.cellSize = YokaiCodexPresentationModel.GridCardSize;
            layout.spacing = Vector2.one * spacing;
            layout.padding = new RectOffset();
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = YokaiCodexPresentationModel.GridColumns;

            var scroll = viewportObject.GetComponent<ScrollRect>();
            scroll.content = grid;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 18f;

            for (var index = 0; index < cardButtons.Length; index++)
            {
                var labelRect = cardTexts[index].rectTransform;
                labelRect.anchorMin = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(1f, .3f);
                labelRect.offsetMin = new Vector2(3f, 2f);
                labelRect.offsetMax = new Vector2(-3f, -1f);
                labelRect.SetAsLastSibling();

                var portraitObject = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
                portraitObject.transform.SetParent(cardButtons[index].transform, false);
                var portraitRect = (RectTransform)portraitObject.transform;
                portraitRect.anchorMin = new Vector2(.08f, .31f);
                portraitRect.anchorMax = new Vector2(.92f, .94f);
                portraitRect.offsetMin = portraitRect.offsetMax = Vector2.zero;
                gridPortraits[index] = portraitObject.GetComponent<Image>();
                gridPortraits[index].preserveAspect = true;
                gridPortraits[index].raycastTarget = false;
                labelRect.SetAsLastSibling();
            }
        }

        private void BuildExpandedView()
        {
            expandedBackdrop = new GameObject("ExpandedBackdrop", typeof(RectTransform), typeof(Image), typeof(Button));
            expandedBackdrop.transform.SetParent(panel.transform, false);
            Stretch((RectTransform)expandedBackdrop.transform);
            expandedBackdrop.GetComponent<Image>().color = new Color(.01f, .015f, .02f, .86f);
            expandedBackdropButton = expandedBackdrop.GetComponent<Button>();
            expandedBackdropButton.onClick.AddListener(HandleExpandedBackdropClicked);

            var cardObject = new GameObject("ExpandedCard", typeof(RectTransform), typeof(Image), typeof(Button));
            cardObject.transform.SetParent(expandedBackdrop.transform, false);
            var cardRect = (RectTransform)cardObject.transform;
            cardRect.anchorMin = cardRect.anchorMax = cardRect.pivot = new Vector2(.5f, .5f);
            cardRect.anchoredPosition = Vector2.zero;
            cardRect.sizeDelta = YokaiCodexPresentationModel.EnlargedCardSize;
            expandedCardBackground = cardObject.GetComponent<Image>();
            RuntimeUiButtonArt.ApplyCodexCard(expandedCardBackground, gameplayArtCatalog);
            expandedCardButton = cardObject.GetComponent<Button>();
            expandedCardButton.onClick.AddListener(HandleExpandedCardClicked);

            expandedTitle = CreateText(cardObject.transform, "Title", 15, TextAnchor.MiddleCenter,
                new Vector2(140f, 18f), new Vector2(0f, 86f));
            expandedPortrait = CreateImage(cardObject.transform, "Portrait",
                new Vector2(116f, 112f), new Vector2(0f, 16f));
            expandedFrontText = CreateText(cardObject.transform, "Front", 10, TextAnchor.UpperCenter,
                new Vector2(140f, 40f), new Vector2(0f, -61f));
            expandedBackText = CreateText(cardObject.transform, "Back", 10, TextAnchor.UpperLeft,
                new Vector2(140f, 168f), new Vector2(0f, -8f));
            expandedHintText = CreateText(cardObject.transform, "Hint", 8, TextAnchor.MiddleCenter,
                new Vector2(142f, 14f), new Vector2(0f, -113f));
            expandedHintText.color = new Color(.78f, .83f, .86f, 1f);
            expandedBackdrop.transform.SetAsLastSibling();
            expandedBackdrop.SetActive(false);
        }

        private static Text CreateText(Transform parent, string objectName, int fontSize, TextAnchor alignment,
            Vector2 size, Vector2 position)
        {
            var textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            var rect = (RectTransform)textObject.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(.94f, .95f, .92f, 1f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static Image CreateImage(Transform parent, string objectName, Vector2 size, Vector2 position)
        {
            var imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            var rect = (RectTransform)imageObject.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = imageObject.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
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
