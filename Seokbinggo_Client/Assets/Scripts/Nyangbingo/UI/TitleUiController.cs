using System.Collections.Generic;
using System.Linq;
using Nyangbingo.Audio;
using Nyangbingo.Data;
using Nyangbingo.Save;
using Nyangbingo.World;
using UnityEngine;
using UnityEngine.UI;

namespace Nyangbingo.UI
{
    /// <summary>
    /// Title.unity 전용 버튼 배선/연출. MainGameShellUiController에서 타이틀 관련 부분만 분리했다.
    /// FindAnyObjectByType을 쓰지 않고, 모든 대상은 인스펙터에서 사전에 연결한다.
    /// demoSaveButtons는 GameShellController.DemoSaveDays(1/15/30일차)와 인덱스로 1:1 대응한다.
    /// </summary>
    public sealed class TitleUiController : MonoBehaviour
    {
        [SerializeField] private TitleShellController shell;
        [SerializeField] private SaveManager saveManager;
        [SerializeField] private NyangbingoAudioService audioService;
        [SerializeField] private Button titleContinueButton;
        [SerializeField] private Button titleNewGameButton;
        [SerializeField] private Button titleQuitButton;
        [SerializeField] private List<Button> demoSaveButtons = new List<Button>();
        [SerializeField] private GameObject confirmationPanel;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Text confirmationText;
        [SerializeField] private Text statusText;
        [SerializeField] private EnvironmentArtCatalog environmentArtCatalog;
        [SerializeField] private GameplayArtCatalog gameplayArtCatalog;
        [SerializeField] private CharacterArtCatalog characterArtCatalog;

        private Text titleDayCounterText;
        private RuntimePixelGlyphPresenter titleDayCounterGlyphs;
        private GameObject titlePlayerArtRoot;
        private Slider mapWidthSlider;
        private Text mapWidthLabel;
        private int selectedMapWidth = WorldGenerationConfig.DefaultMapWidth;
        private bool isInitialized;

        /// <summary>NyangbingoTitleSceneCreator(에디터 씬 생성 도구) 전용 빌드타임 배선 진입점.</summary>
        public void ConfigureForScene(TitleShellController shellController, SaveManager saves,
            NyangbingoAudioService audio, Button continueGame, Button newGame, Button quit,
            List<Button> demoButtons, GameObject confirmation, Button confirm, Button cancel,
            Text confirmationLabel, Text status, EnvironmentArtCatalog environmentArt,
            GameplayArtCatalog gameplayArt, CharacterArtCatalog characterArt)
        {
            shell = shellController;
            saveManager = saves;
            audioService = audio;
            titleContinueButton = continueGame;
            titleNewGameButton = newGame;
            titleQuitButton = quit;
            demoSaveButtons = demoButtons;
            confirmationPanel = confirmation;
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
                titleContinueButton == null || titleNewGameButton == null || titleQuitButton == null ||
                confirmButton == null || cancelButton == null || confirmationText == null ||
                demoSaveButtons.Count != GameShellController.DemoSaveDays.Length)
            {
                Debug.LogError("[Nyangbingo] TitleUiController: 타이틀 셸 필수 배선이 올바르지 않습니다.");
                enabled = false;
                return;
            }

            audioService.Initialize();
            audioService.EnsureAudiblePlayback(MusicTrack.Title);
            shell.ConfigureForRuntime(saveManager, Application.isMobilePlatform,
                Debug.isDebugBuild || Application.isEditor);
            shell.NewGameRequested += HandleNewGameRequested;
            shell.ContinueRequested += HandleContinueRequested;
            shell.DemoSaveRequested += HandleDemoSaveRequested;

            BindTitleButtons();
            EnsureMapWidthSlider();
            ConfigureTitleMenuLayout();
            ApplyDeliveredTitleArt();
            RefreshTitleControls();
            SetStatus(string.Empty);
            if (confirmationPanel != null) confirmationPanel.SetActive(false);
            isInitialized = true;
            Debug.Log("[Nyangbingo] TitleUiController: 타이틀 셸·맵 크기 슬라이더 연결 완료.");
        }

        private void Update()
        {
            if (!isInitialized) return;
            var confirming = shell.IsAwaitingDemoLoadConfirmation;
            if (confirmationPanel != null) confirmationPanel.SetActive(confirming);
            if (confirmationText != null && confirming)
                confirmationText.text = $"{shell.PendingDemoDay}일차 데모를 자동저장 슬롯에 복사할까요?";
            if (Input.GetKeyDown(KeyCode.Escape) && confirming)
                shell.CancelDemoLoad();
        }

        private void BindTitleButtons()
        {
            titleContinueButton.onClick.AddListener(() =>
                SetStatus(shell.TryContinue() ? "불러오는 중..." : "저장 파일이 없습니다."));
            titleNewGameButton.onClick.AddListener(() => shell.RequestNewGame());
            titleQuitButton.onClick.AddListener(() => shell.RequestQuit());
            confirmButton.onClick.AddListener(ConfirmDemoLoad);
            cancelButton.onClick.AddListener(() => shell.CancelDemoLoad());
            for (var index = 0; index < demoSaveButtons.Count; index++)
            {
                var day = GameShellController.DemoSaveDays[index];
                demoSaveButtons[index].onClick.AddListener(() => RequestDemoSave(day));
            }
        }

        private void RequestDemoSave(int day)
        {
            if (shell.RequestDemoSave(day))
                SetStatus($"{day}일차 데모를 자동저장 슬롯에 복사할까요?");
        }

        private void ConfirmDemoLoad()
        {
            var demoDay = shell.PendingDemoDay;
            if (shell.ConfirmDemoLoad())
                SetStatus($"{demoDay}일차 데모를 자동저장 슬롯에 복사합니다.");
            else
                SetStatus($"{demoDay}일차 데모 세이브가 없거나 올바르지 않습니다.");
        }

        private void RefreshTitleControls()
        {
            var badge = TitleShellController.FormatTitleHeatStage(shell.Title.DisplayedHeatStage);
            if (titleDayCounterGlyphs != null) titleDayCounterGlyphs.SetText(badge);
            else if (titleDayCounterText != null) titleDayCounterText.text = badge;
            if (titlePlayerArtRoot != null) titlePlayerArtRoot.SetActive(true);
            if (titleContinueButton != null) titleContinueButton.interactable = shell.Title.CanContinue;
            if (titleQuitButton != null) titleQuitButton.gameObject.SetActive(shell.Title.ShowsQuit);
            RefreshMapWidthLabel();
            for (var index = 0; index < demoSaveButtons.Count; index++)
            {
                var visible = shell.Title.ShowsDemoSaves;
                demoSaveButtons[index].gameObject.SetActive(visible);
                demoSaveButtons[index].interactable = visible &&
                    saveManager.HasDemoSave(GameShellController.DemoSaveDays[index]);
            }
        }

        private void SetStatus(string value)
        {
            if (statusText != null) statusText.text = value;
        }

        private void HandleNewGameRequested()
        {
            MainGameLaunchRequest.RequestedMode = MainGameLaunchRequest.Mode.NewGame;
            MainGameLaunchRequest.SaveSlot = GameShellController.AutoSaveSlot;
            var previousSeed = saveManager.TryLoad(GameShellController.AutoSaveSlot, out var previousSave)
                ? previousSave.seed
                : 0;
            MainGameBootstrap.RequestFreshWorldForNextScene(previousSeed, selectedMapWidth);
            SceneTransitionRequest.BeginDirect("MainGame");
        }

        private void HandleContinueRequested(int slot, SaveGame loadedSave)
        {
            MainGameLaunchRequest.RequestedMode = MainGameLaunchRequest.Mode.Continue;
            MainGameLaunchRequest.SaveSlot = slot;
            SceneTransitionRequest.BeginDirect("MainGame");
        }

        private void HandleDemoSaveRequested(SaveGame demo)
        {
            MainGameLaunchRequest.RequestedMode = MainGameLaunchRequest.Mode.DemoLoad;
            MainGameLaunchRequest.SaveSlot = GameShellController.AutoSaveSlot;
            SceneTransitionRequest.BeginDirect("MainGame");
        }

        private void ConfigureTitleMenuLayout()
        {
            ConfigureShellButton(titleContinueButton, new Vector2(-112f, 17f), new Vector2(150f, 27f));
            ConfigureShellButton(titleNewGameButton, new Vector2(-112f, -17f), new Vector2(150f, 27f));
            ConfigureShellButton(titleQuitButton, new Vector2(-112f, -51f), new Vector2(150f, 27f));
            for (var index = 0; index < demoSaveButtons.Count; index++)
                ConfigureShellButton(demoSaveButtons[index],
                    new Vector2(-162f + index * 50f, -82f), new Vector2(46f, 15f));
            if (mapWidthSlider != null)
            {
                var rect = mapWidthSlider.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(-112f, -100f);
                rect.sizeDelta = new Vector2(150f, 14f);
            }
            if (mapWidthLabel != null)
            {
                var labelRect = mapWidthLabel.rectTransform;
                labelRect.anchoredPosition = new Vector2(-112f, -114f);
                labelRect.sizeDelta = new Vector2(150f, 12f);
            }
        }

        private void EnsureMapWidthSlider()
        {
            if (titleNewGameButton == null || mapWidthSlider != null) return;
            var titlePanel = titleNewGameButton.transform.parent;
            if (titlePanel == null) return;

            var labelObject = new GameObject("MapWidthLabel", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(titlePanel, false);
            mapWidthLabel = labelObject.GetComponent<Text>();
            mapWidthLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mapWidthLabel.fontSize = 8;
            mapWidthLabel.alignment = TextAnchor.MiddleCenter;
            mapWidthLabel.color = new Color(.85f, .9f, .95f, .95f);
            mapWidthLabel.raycastTarget = false;

            var sliderObject = new GameObject("MapWidthSlider", typeof(RectTransform), typeof(Slider),
                typeof(Image));
            sliderObject.transform.SetParent(titlePanel, false);
            var background = sliderObject.GetComponent<Image>();
            background.color = new Color(.12f, .16f, .22f, .95f);
            mapWidthSlider = sliderObject.GetComponent<Slider>();
            mapWidthSlider.minValue = WorldGenerationConfig.MinMapWidth;
            mapWidthSlider.maxValue = WorldGenerationConfig.MaxMapWidth;
            mapWidthSlider.wholeNumbers = true;
            mapWidthSlider.value = selectedMapWidth;

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderObject.transform, false);
            var fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, .25f);
            fillAreaRect.anchorMax = new Vector2(1f, .75f);
            fillAreaRect.offsetMin = new Vector2(4f, 0f);
            fillAreaRect.offsetMax = new Vector2(-4f, 0f);
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
            fill.GetComponent<Image>().color = new Color(.35f, .7f, .95f, .9f);
            mapWidthSlider.fillRect = fillRect;

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sliderObject.transform, false);
            var handleAreaRect = handleArea.GetComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(4f, 0f);
            handleAreaRect.offsetMax = new Vector2(-4f, 0f);
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            var handleRect = handle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(10f, 0f);
            handle.GetComponent<Image>().color = Color.white;
            mapWidthSlider.handleRect = handleRect;
            mapWidthSlider.targetGraphic = handle.GetComponent<Image>();
            mapWidthSlider.onValueChanged.AddListener(OnMapWidthChanged);
            RefreshMapWidthLabel();
        }

        private void OnMapWidthChanged(float value)
        {
            selectedMapWidth = Mathf.Clamp(Mathf.RoundToInt(value),
                WorldGenerationConfig.MinMapWidth, WorldGenerationConfig.MaxMapWidth);
            RefreshMapWidthLabel();
        }

        private void RefreshMapWidthLabel()
        {
            if (mapWidthLabel == null) return;
            mapWidthLabel.text =
                $"맵 가로 {selectedMapWidth} (깊이≥{WorldGenerationConfig.UndergroundDepthMinTiles})";
        }

        private static void ConfigureShellButton(Button button, Vector2 position, Vector2 size)
        {
            if (button == null) return;
            var rect = button.GetComponent<RectTransform>();
            if (rect == null) return;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private void ApplyDeliveredTitleArt()
        {
            EnsureTitleBackground();
            EnsureTitleLogo();
            EnsureTitleStatePresentation();
            if (gameplayArtCatalog == null) return;
            RuntimeUiButtonArt.Apply(titleContinueButton, gameplayArtCatalog);
            RuntimeUiButtonArt.Apply(titleNewGameButton, gameplayArtCatalog);
            RuntimeUiButtonArt.Apply(titleQuitButton, gameplayArtCatalog);
            RuntimeUiButtonArt.Apply(confirmButton, gameplayArtCatalog);
            RuntimeUiButtonArt.Apply(cancelButton, gameplayArtCatalog);
            for (var index = 0; index < demoSaveButtons.Count; index++)
                RuntimeUiButtonArt.Apply(demoSaveButtons[index], gameplayArtCatalog);
            ApplyButtonLabelArt(titleContinueButton, gameplayArtCatalog.ShellContinue);
            ApplyButtonLabelArt(titleNewGameButton, gameplayArtCatalog.ShellStart);
            ApplyButtonLabelArt(titleQuitButton, gameplayArtCatalog.ShellLeave);
        }

        private static void ApplyButtonLabelArt(Button button, Sprite sprite)
        {
            if (button == null || sprite == null) return;
            var label = button.GetComponentInChildren<Text>(true);
            if (label != null) label.gameObject.SetActive(false);
            var artTransform = button.transform.Find("DeliveredArt") as RectTransform;
            if (artTransform == null)
            {
                var artObject = new GameObject("DeliveredArt", typeof(RectTransform), typeof(Image));
                artObject.transform.SetParent(button.transform, false);
                artTransform = (RectTransform)artObject.transform;
            }
            artTransform.anchorMin = artTransform.anchorMax = artTransform.pivot = new Vector2(.5f, .5f);
            artTransform.anchoredPosition = Vector2.zero;
            artTransform.sizeDelta = sprite.rect.size;
            var image = artTransform.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        private void EnsureTitleBackground()
        {
            if (titleNewGameButton == null) return;
            var titlePanel = titleNewGameButton.transform.parent;
            if (titlePanel == null) return;

            var panelImage = titlePanel.GetComponent<Image>();
            if (panelImage != null)
                panelImage.color = new Color(.02f, .035f, .05f, 1f);

            if (environmentArtCatalog == null || environmentArtCatalog.TitleBackground == null) return;
            var backgroundTransform = titlePanel.Find("TitleBackground") as RectTransform;
            if (backgroundTransform == null)
            {
                var backgroundObject = new GameObject("TitleBackground", typeof(RectTransform), typeof(Image),
                    typeof(AspectRatioFitter));
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

        private void OnDestroy()
        {
            if (shell != null)
            {
                shell.NewGameRequested -= HandleNewGameRequested;
                shell.ContinueRequested -= HandleContinueRequested;
                shell.DemoSaveRequested -= HandleDemoSaveRequested;
            }
        }
    }
}
