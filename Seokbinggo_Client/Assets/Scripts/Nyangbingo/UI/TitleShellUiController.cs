using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Nyangbingo.Audio;
using Nyangbingo.Save;
using Nyangbingo.World;
using UnityEngine;
using UnityEngine.UI;

namespace Nyangbingo.UI
{
    /// <summary>
    /// Title 씬 전용 셸. 월드 없이 메뉴만 다루고 MainGame으로 Intent를 넘긴다.
    /// </summary>
    [DefaultExecutionOrder(-55)]
    public sealed class TitleShellUiController : MonoBehaviour
    {
        [SerializeField] private GameShellController shell;
        [SerializeField] private SaveManager saveManager;
        [SerializeField] private NyangbingoAudioService audioService;
        [SerializeField] private Button titleContinueButton;
        [SerializeField] private Button titleNewGameButton;
        [SerializeField] private Button titleQuitButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Text confirmationText;
        [SerializeField] private Text statusText;
        [SerializeField] private EnvironmentArtCatalog environmentArtCatalog;
        [SerializeField] private GameplayArtCatalog gameplayArtCatalog;
        [SerializeField] private CharacterArtCatalog characterArtCatalog;

        private readonly List<Button> demoSaveButtons = new List<Button>();
        private Text titleDayCounterText;
        private RuntimePixelGlyphPresenter titleDayCounterGlyphs;
        private GameObject titlePlayerArtRoot;
        private GameObject shellLoadingOverlay;
        private Image shellLoadingImage;
        private Sprite[] shellLoadingFrames = Array.Empty<Sprite>();
        private bool shellLoadingTransitionActive;

        public void ConfigureForScene(GameShellController shellController, SaveManager saves,
            NyangbingoAudioService audio, Button continueGame, Button newGame, Button quit,
            Button confirm, Button cancel, Text confirmationLabel, Text status,
            EnvironmentArtCatalog environmentArt = null, GameplayArtCatalog gameplayArt = null,
            CharacterArtCatalog characterArt = null)
        {
            shell = shellController;
            saveManager = saves;
            audioService = audio;
            titleContinueButton = continueGame;
            titleNewGameButton = newGame;
            titleQuitButton = quit;
            confirmButton = confirm;
            cancelButton = cancel;
            confirmationText = confirmationLabel;
            statusText = status;
            environmentArtCatalog = environmentArt;
            gameplayArtCatalog = gameplayArt;
            characterArtCatalog = characterArt;
        }

        private void Start()
        {
            if (shell == null || saveManager == null || audioService == null ||
                titleContinueButton == null || titleNewGameButton == null || titleQuitButton == null)
            {
                Debug.LogError("[Nyangbingo] TitleShellUiController: Title 셸 필수 배선이 올바르지 않습니다.");
                enabled = false;
                return;
            }

            audioService.Initialize();
            audioService.EnsureAudiblePlayback(MusicTrack.Title);
            shell.ConfigureForRuntime(saveManager, audioService, null, null,
                Application.isMobilePlatform, Debug.isDebugBuild || Application.isEditor);
            shell.NewGameRequested += HandleNewGameRequested;
            shell.ContinueRequested += HandleContinueRequested;
            shell.DemoSaveRequested += HandleDemoSaveRequested;
            BindButtons();
            CreateDemoSaveButtons();
            ConfigureTitleMenuLayout();
            ResolveShellArtCatalogs();
            ApplyDeliveredShellArt();
            EnsureShellLoadingOverlay();
            shell.EnterTitle();
            RefreshTitleControls();
            SetStatus(string.Empty);
            if (GameSceneFlow.RevealLoadingAfterLoad)
            {
                GameSceneFlow.ConsumePending(out _, out _, out _);
                StartCoroutine(PlayShellLoadingRevealOnly());
            }

            Debug.Log("[Nyangbingo] TitleShellUiController: Title 씬 셸 연결 완료.");
        }

        private void BindButtons()
        {
            if (confirmButton != null) confirmButton.onClick.AddListener(ConfirmPendingAction);
            if (cancelButton != null) cancelButton.onClick.AddListener(() => shell.CancelConfirmation());
            titleContinueButton.onClick.AddListener(() =>
                SetStatus(shell.TryContinue() ? "불러오는 중..." : "저장 파일이 없습니다."));
            titleNewGameButton.onClick.AddListener(() => shell.RequestNewGame());
            titleQuitButton.onClick.AddListener(() => shell.RequestQuit());
        }

        private void HandleNewGameRequested(int _)
        {
            BeginShellLoadingTransition(() =>
            {
                var previousSeed = 0;
                if (saveManager.TryLoad(GameShellController.AutoSaveSlot, out var previousSave))
                    previousSeed = previousSave.seed;
                saveManager.DeleteAll();
                MainGameBootstrap.RequestFreshWorldForNextScene(previousSeed);
                GameSceneFlow.RequestNewGame(revealLoading: true);
                GameSceneFlow.GoToMainGame();
            });
        }

        private void HandleContinueRequested(int slot, SaveGame _)
        {
            BeginShellLoadingTransition(() =>
            {
                GameSceneFlow.RequestContinue(slot, revealLoading: true);
                GameSceneFlow.GoToMainGame();
            });
        }

        private void HandleDemoSaveRequested(SaveGame _)
        {
            BeginShellLoadingTransition(() =>
            {
                // Confirm already copied the demo into the autosave slot.
                GameSceneFlow.RequestContinue(GameShellController.AutoSaveSlot, revealLoading: true);
                GameSceneFlow.GoToMainGame();
            });
        }

        private void ConfirmPendingAction()
        {
            var confirmation = shell.PendingConfirmation;
            var demoDay = shell.PendingDemoDay;
            if (shell.Confirm())
            {
                if (confirmation == GameShellConfirmation.LoadDemoSave)
                    SetStatus($"{demoDay}일차 데모를 불러오는 중...");
                return;
            }

            if (confirmation == GameShellConfirmation.LoadDemoSave)
                SetStatus($"{demoDay}일차 데모 세이브가 없거나 올바르지 않습니다.");
        }

        private void RefreshTitleControls()
        {
            var countdown = GameShellController.FormatTitleCountdown(shell.Title.DaysUntilBaegilHeat);
            if (titleDayCounterGlyphs != null)
                titleDayCounterGlyphs.SetText(countdown);
            else if (titleDayCounterText != null)
                titleDayCounterText.text = countdown;
            if (titlePlayerArtRoot != null) titlePlayerArtRoot.SetActive(true);
            if (titleContinueButton != null) titleContinueButton.interactable = shell.Title.CanContinue;
            if (titleQuitButton != null) titleQuitButton.gameObject.SetActive(shell.Title.ShowsQuit);
            for (var index = 0; index < demoSaveButtons.Count; index++)
            {
                var visible = shell.Title.ShowsDemoSaves;
                demoSaveButtons[index].gameObject.SetActive(visible);
                demoSaveButtons[index].interactable = visible &&
                    saveManager.HasDemoSave(GameShellController.DemoSaveDays[index]);
            }
        }

        private void ConfigureTitleMenuLayout()
        {
        }

        private void CreateDemoSaveButtons()
        {
            if (titleNewGameButton == null || titleQuitButton == null) return;
            var parent = titleNewGameButton.transform.parent;
            var quitRect = titleQuitButton.GetComponent<RectTransform>();
            if (parent == null || quitRect == null) return;

            for (var index = 0; index < GameShellController.DemoSaveDays.Length; index++)
            {
                var day = GameShellController.DemoSaveDays[index];
                var button = Object.Instantiate(titleNewGameButton, parent);
                button.name = $"DemoSaveDay{day}";
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() =>
                {
                    if (shell.RequestDemoSave(day))
                        SetStatus($"{day}일차 데모를 자동저장 슬롯에 복사합니다.");
                });
                var rect = button.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(-162f + index * 50f, -82f);
                rect.sizeDelta = new Vector2(46f, 15f);
                var label = button.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    label.text = $"{day}일차 데모";
                    label.fontSize = 8;
                }

                demoSaveButtons.Add(button);
            }

            quitRect.anchoredPosition = new Vector2(-112f, -51f);
        }

        private void ResolveShellArtCatalogs()
        {
            if (environmentArtCatalog == null)
                environmentArtCatalog = Resources.FindObjectsOfTypeAll<EnvironmentArtCatalog>()
                    .FirstOrDefault(catalog => catalog != null && catalog.name == "EnvironmentArtCatalog");
            if (gameplayArtCatalog == null)
                gameplayArtCatalog = Resources.FindObjectsOfTypeAll<GameplayArtCatalog>()
                    .FirstOrDefault(catalog => catalog != null && catalog.name == "GameplayArtCatalog");
            if (characterArtCatalog == null)
                characterArtCatalog = Resources.FindObjectsOfTypeAll<CharacterArtCatalog>()
                    .FirstOrDefault(catalog => catalog != null && catalog.name == "CharacterArtCatalog");
        }

        private void ApplyDeliveredShellArt()
        {
            EnsureTitleBackground();
            EnsureTitleLogo();
            EnsureTitleStatePresentation();
            if (gameplayArtCatalog == null) return;
            ApplyButtonLabelArt(titleContinueButton, gameplayArtCatalog.ShellContinue);
            ApplyButtonLabelArt(titleNewGameButton, gameplayArtCatalog.ShellStart);
        }

        private void EnsureTitleBackground()
        {
            if (titleNewGameButton == null || environmentArtCatalog?.TitleBackground == null) return;
            var titlePanel = titleNewGameButton.transform.parent as RectTransform;
            if (titlePanel == null) return;
            var backgroundTransform = titlePanel.Find("TitleBackground") as RectTransform;
            if (backgroundTransform == null)
            {
                var backgroundObject = new GameObject("TitleBackground", typeof(RectTransform), typeof(Image));
                backgroundObject.transform.SetParent(titlePanel, false);
                backgroundTransform = (RectTransform)backgroundObject.transform;
            }

            backgroundTransform.anchorMin = Vector2.zero;
            backgroundTransform.anchorMax = Vector2.one;
            backgroundTransform.offsetMin = Vector2.zero;
            backgroundTransform.offsetMax = Vector2.zero;
            var image = backgroundTransform.GetComponent<Image>();
            image.sprite = environmentArtCatalog.TitleBackground;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            var fitter = backgroundTransform.GetComponent<AspectRatioFitter>() ??
                         backgroundTransform.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = image.sprite.rect.width / image.sprite.rect.height;
            backgroundTransform.SetAsFirstSibling();
        }

        private void EnsureTitleLogo()
        {
            if (titleNewGameButton == null) return;
            var titlePanel = titleNewGameButton.transform.parent;
            if (titlePanel == null) return;
            var titleLabel = titlePanel.Find("Title")?.GetComponent<Text>();
            var deliveredLogo = gameplayArtCatalog?.ShellTitleLogo;
            if (titleLabel != null && deliveredLogo == null)
            {
                titleLabel.gameObject.SetActive(true);
                titleLabel.text = "100일의 냥빙고";
                titleLabel.fontSize = 20;
                titleLabel.alignment = TextAnchor.MiddleCenter;
                var labelRect = titleLabel.rectTransform;
                labelRect.anchorMin = labelRect.anchorMax = labelRect.pivot = new Vector2(.5f, .5f);
                labelRect.anchoredPosition = new Vector2(-112f, 88f);
                labelRect.sizeDelta = new Vector2(180f, 30f);
            }

            var titleArtTransform = titlePanel.Find("TitleArt") as RectTransform;
            if (deliveredLogo == null)
            {
                if (titleArtTransform != null) titleArtTransform.gameObject.SetActive(false);
                return;
            }

            if (titleLabel != null) titleLabel.gameObject.SetActive(false);
            if (titleArtTransform == null)
            {
                var titleArtObject = new GameObject("TitleArt", typeof(RectTransform), typeof(Image));
                titleArtObject.transform.SetParent(titlePanel, false);
                titleArtTransform = (RectTransform)titleArtObject.transform;
            }

            titleArtTransform.gameObject.SetActive(true);
            titleArtTransform.anchorMin = titleArtTransform.anchorMax = titleArtTransform.pivot =
                new Vector2(.5f, .5f);
            titleArtTransform.anchoredPosition = new Vector2(-112f, 82f);
            titleArtTransform.sizeDelta = new Vector2(96f, 96f);
            var titleImage = titleArtTransform.GetComponent<Image>() ??
                             titleArtTransform.gameObject.AddComponent<Image>();
            titleImage.sprite = deliveredLogo;
            titleImage.color = Color.white;
            titleImage.preserveAspect = true;
            titleImage.raycastTarget = false;
        }

        private void EnsureTitleStatePresentation()
        {
            if (titleNewGameButton == null) return;
            var titlePanel = titleNewGameButton.transform.parent;
            if (titlePanel == null) return;

            var counterTransform = titlePanel.Find("TitleDayCounter") as RectTransform;
            if (counterTransform == null)
            {
                var counterObject = new GameObject("TitleDayCounter", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Text));
                counterObject.transform.SetParent(titlePanel, false);
                counterTransform = (RectTransform)counterObject.transform;
            }

            counterTransform.anchorMin = counterTransform.anchorMax = counterTransform.pivot =
                new Vector2(.5f, .5f);
            counterTransform.anchoredPosition = new Vector2(176f, 97f);
            counterTransform.sizeDelta = new Vector2(84f, 36f);
            titleDayCounterText = counterTransform.GetComponent<Text>();
            var menuLabel = titleNewGameButton.GetComponentInChildren<Text>(true);
            titleDayCounterText.font = menuLabel != null ? menuLabel.font : titleDayCounterText.font;
            titleDayCounterText.fontSize = 22;
            titleDayCounterText.fontStyle = FontStyle.Bold;
            titleDayCounterText.alignment = TextAnchor.MiddleCenter;
            titleDayCounterText.color = Color.white;
            titleDayCounterText.raycastTarget = false;
            if (gameplayArtCatalog?.ShellNumberGlyphs.Count == RuntimePixelGlyphPresenter.ExpectedGlyphCount)
            {
                titleDayCounterText.text = string.Empty;
                titleDayCounterGlyphs = counterTransform.GetComponent<RuntimePixelGlyphPresenter>() ??
                                        counterTransform.gameObject.AddComponent<RuntimePixelGlyphPresenter>();
                titleDayCounterGlyphs.ConfigureForRuntime(gameplayArtCatalog.ShellNumberGlyphs);
            }

            var playerEntry = characterArtCatalog?.Find("player");
            if (playerEntry == null || playerEntry.IdleFrames.Count == 0) return;
            var playerTransform = titlePanel.Find("TitlePlayerArt") as RectTransform;
            if (playerTransform == null)
            {
                var playerObject = new GameObject("TitlePlayerArt", typeof(RectTransform), typeof(Image));
                playerObject.transform.SetParent(titlePanel, false);
                playerTransform = (RectTransform)playerObject.transform;
            }

            playerTransform.anchorMin = playerTransform.anchorMax = playerTransform.pivot = new Vector2(1f, 0f);
            playerTransform.anchoredPosition = new Vector2(-54f, 34f);
            playerTransform.sizeDelta = new Vector2(72f, 72f);
            var playerImage = playerTransform.GetComponent<Image>();
            playerImage.preserveAspect = true;
            playerImage.raycastTarget = false;
            var animator = playerTransform.GetComponent<RuntimeUiSpriteAnimator>() ??
                           playerTransform.gameObject.AddComponent<RuntimeUiSpriteAnimator>();
            animator.ConfigureForScene(playerEntry.IdleFrames.ToArray(), .35f);
            titlePlayerArtRoot = playerTransform.gameObject;

            var titleBackground = titlePanel.Find("TitleBackground");
            counterTransform.SetSiblingIndex(titleBackground != null ? 2 : 1);
            playerTransform.SetSiblingIndex(titleBackground != null ? 2 : 1);
        }

        private static void ApplyButtonLabelArt(Button button, Sprite sprite)
        {
            if (button == null || sprite == null) return;
            var label = button.GetComponentInChildren<Text>(true);
            if (label != null) label.gameObject.SetActive(false);
            var art = button.transform.Find("LabelArt") as RectTransform;
            if (art == null)
            {
                var artObject = new GameObject("LabelArt", typeof(RectTransform), typeof(Image));
                artObject.transform.SetParent(button.transform, false);
                art = (RectTransform)artObject.transform;
            }

            art.anchorMin = art.anchorMax = art.pivot = new Vector2(.5f, .5f);
            art.anchoredPosition = Vector2.zero;
            art.sizeDelta = new Vector2(120f, 28f);
            var image = art.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        private void EnsureShellLoadingOverlay()
        {
            if (shellLoadingOverlay != null) return;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            shellLoadingOverlay = new GameObject("ShellLoadingOverlay", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image));
            shellLoadingOverlay.transform.SetParent(canvas.transform, false);
            var overlayCanvas = shellLoadingOverlay.GetComponent<Canvas>();
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = MainGameShellUiController.ShellLoadingSortingOrder;
            var rect = (RectTransform)shellLoadingOverlay.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            shellLoadingImage = shellLoadingOverlay.GetComponent<Image>();
            shellLoadingImage.color = Color.black;
            shellLoadingImage.raycastTarget = true;
            shellLoadingOverlay.SetActive(false);
            BuildShellLoadingFrames();
        }

        private void BuildShellLoadingFrames()
        {
            // Frames are optional on Title; reveal uses solid black if unavailable.
            shellLoadingFrames = Array.Empty<Sprite>();
        }

        private void BeginShellLoadingTransition(Action completion)
        {
            if (shellLoadingTransitionActive)
            {
                completion?.Invoke();
                return;
            }

            StartCoroutine(PlayShellLoadingTransition(completion));
        }

        private IEnumerator PlayShellLoadingTransition(Action completion)
        {
            shellLoadingTransitionActive = true;
            MainGameShellUiController.IsLoadingTransitionActive = true;
            EnsureShellLoadingOverlay();
            if (shellLoadingOverlay != null)
            {
                shellLoadingOverlay.SetActive(true);
                if (shellLoadingImage != null)
                    shellLoadingImage.color = Color.black;
            }

            yield return new WaitForSecondsRealtime(.15f);
            completion?.Invoke();
            shellLoadingTransitionActive = false;
        }

        private IEnumerator PlayShellLoadingRevealOnly()
        {
            EnsureShellLoadingOverlay();
            if (shellLoadingOverlay != null) shellLoadingOverlay.SetActive(true);
            yield return new WaitForSecondsRealtime(.35f);
            if (shellLoadingOverlay != null) shellLoadingOverlay.SetActive(false);
            MainGameShellUiController.IsLoadingTransitionActive = false;
            shell.RestoreTimeScaleAfterLoading();
        }

        private void SetStatus(string value)
        {
            if (statusText != null) statusText.text = value;
        }

        private void OnDestroy()
        {
            MainGameShellUiController.IsLoadingTransitionActive = false;
            if (shell != null)
            {
                shell.NewGameRequested -= HandleNewGameRequested;
                shell.ContinueRequested -= HandleContinueRequested;
                shell.DemoSaveRequested -= HandleDemoSaveRequested;
            }
        }
    }
}
