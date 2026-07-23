using System;
using Nyangbingo.Audio;
using Nyangbingo.Bosses;
using Nyangbingo.Data;
using Nyangbingo.Save;
using Nyangbingo.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Nyangbingo.UI
{
    [DefaultExecutionOrder(-55)]
    public sealed class MainGameShellUiController : MonoBehaviour
    {
        [SerializeField] private GameShellController shell;
        [SerializeField] private MainGameSaveCoordinator saveCoordinator;
        [SerializeField] private SaveManager saveManager;
        [SerializeField] private NyangbingoAudioService audioService;
        [SerializeField] private DayNightService timeService;
        [SerializeField] private MainGameCodexController codex;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button[] saveButtons = new Button[SaveManager.SlotCount];
        [SerializeField] private Button[] loadButtons = new Button[SaveManager.SlotCount];
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button returnTitleButton;
        [SerializeField] private Button settingsApplyButton;
        [SerializeField] private Button settingsBackButton;
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Text confirmationText;
        [SerializeField] private Button titleContinueButton;
        [SerializeField] private Button titleNewGameButton;
        [SerializeField] private Button titleQuitButton;
        [SerializeField] private Button resultTitleButton;
        [SerializeField] private Text statusText;
        [SerializeField] private EnvironmentArtCatalog environmentArtCatalog;
        [SerializeField] private GameplayArtCatalog gameplayArtCatalog;
        [SerializeField] private CharacterArtCatalog characterArtCatalog;

        private readonly List<Button> demoSaveButtons = new List<Button>();
        private GameDataCatalog gameDataCatalog;
        private BossManager bossManager;
        private Text resultHeaderText;
        private Text resultSummaryText;
        private Text resultTeaserText;
        private Image bgmSpeakerImage;
        private Image sfxSpeakerImage;
        private Button pauseSaveButton;
        private Text titleDayCounterText;
        private RuntimePixelGlyphPresenter titleDayCounterGlyphs;
        private GameObject titlePlayerArtRoot;
        private RectTransform pauseHoverIndicator;
        private bool demoLoadApplied;
        private static bool enterGameplayAfterReload;

        public int BoundSaveSlotCount => saveButtons?.Length ?? 0;
        public bool IsInitialized { get; private set; }

        public void ConfigureForScene(GameShellController shellController, MainGameSaveCoordinator coordinator,
            SaveManager saves, NyangbingoAudioService audio, DayNightService clock, MainGameCodexController codexUi,
            Button resume, Button[] saveSlotButtons, Button[] loadSlotButtons, Button settings, Button returnTitle,
            Button applySettings, Button backSettings, Slider bgm, Slider sfx, Toggle fullscreen,
            Button confirm, Button cancel, Text confirmationLabel, Button continueGame, Button newGame,
            Button quit, Button resultTitle, Text status, EnvironmentArtCatalog environmentArt = null,
            GameplayArtCatalog gameplayArt = null)
        {
            shell = shellController;
            saveCoordinator = coordinator;
            saveManager = saves;
            audioService = audio;
            timeService = clock;
            codex = codexUi;
            resumeButton = resume;
            saveButtons = saveSlotButtons;
            loadButtons = loadSlotButtons;
            settingsButton = settings;
            returnTitleButton = returnTitle;
            settingsApplyButton = applySettings;
            settingsBackButton = backSettings;
            bgmSlider = bgm;
            sfxSlider = sfx;
            fullscreenToggle = fullscreen;
            confirmButton = confirm;
            cancelButton = cancel;
            confirmationText = confirmationLabel;
            titleContinueButton = continueGame;
            titleNewGameButton = newGame;
            titleQuitButton = quit;
            resultTitleButton = resultTitle;
            statusText = status;
            environmentArtCatalog = environmentArt;
            gameplayArtCatalog = gameplayArt;
        }

        private void Start()
        {
            if (shell == null || saveCoordinator == null || saveManager == null || audioService == null ||
                timeService == null || !saveCoordinator.Initialize() || saveButtons == null || loadButtons == null ||
                saveButtons.Length != SaveManager.SlotCount || loadButtons.Length != SaveManager.SlotCount)
            {
                Debug.LogError("[Nyangbingo] MainGameShellUiController: 게임 셸 필수 배선이 올바르지 않습니다.");
                enabled = false;
                return;
            }

            audioService.Initialize();
            audioService.EnsureAudiblePlayback();
            shell.ConfigureForRuntime(saveManager, audioService, timeService, saveCoordinator.CaptureSnapshot(),
                Application.isMobilePlatform, Debug.isDebugBuild || Application.isEditor);
            shell.NewGameRequested += HandleNewGameRequested;
            shell.ContinueRequested += HandleContinueRequested;
            shell.DemoSaveRequested += HandleDemoSaveRequested;
            shell.TitleRequested += HandleTitleRequested;
            gameDataCatalog = FindAnyObjectByType<MainGameBootstrap>()?.GameDataCatalog;
            bossManager = FindAnyObjectByType<BossManager>();
            timeService.Dawn += HandleMvpDawn;
            ConfigurePauseMenuLayout();
            BindButtons();
            BuildResultView();
            CreateDemoSaveButtons();
            ConfigureTitleMenuLayout();
            ConfigureSettingsMenuLayout();
            bgmSlider.value = audioService.BgmVolume;
            sfxSlider.value = audioService.SfxVolume;
            fullscreenToggle.isOn = Screen.fullScreen;
            ResolveShellArtCatalogs();
            ApplyDeliveredShellArt();
            var shouldEnterGameplay = enterGameplayAfterReload;
            enterGameplayAfterReload = false;
            if (shouldEnterGameplay) shell.EnterGameplay(saveCoordinator.CaptureSnapshot());
            else shell.EnterTitle();
            RefreshTitleControls();
            SetStatus(string.Empty);
            IsInitialized = true;
            Debug.Log("[Nyangbingo] MainGameShellUiController: 일시정지 4항목·현재 슬롯 저장·설정·타이틀 셸 연결 완료.");
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
            ApplyDeliveredButtonArt();
            ApplyButtonLabelArt(titleContinueButton, gameplayArtCatalog.ShellContinue);
            ApplyButtonLabelArt(titleNewGameButton, gameplayArtCatalog.ShellStart);
            ApplyButtonLabelArt(resumeButton, gameplayArtCatalog.ShellResume);
            ApplyButtonLabelArt(pauseSaveButton, gameplayArtCatalog.ShellSave);
            ApplyButtonLabelArt(settingsButton, gameplayArtCatalog.ShellSettings);
            ApplyButtonLabelArt(titleQuitButton, gameplayArtCatalog.ShellLeave);
            ApplyButtonLabelArt(returnTitleButton, gameplayArtCatalog.ShellReturnTitle);
            ApplyButtonLabelArt(resultTitleButton, gameplayArtCatalog.ShellReturnTitle);
            ApplyButtonLabelArt(settingsApplyButton, gameplayArtCatalog.ShellApply);
            ApplyButtonLabelArt(settingsBackButton, gameplayArtCatalog.ShellBack);
            ApplyShellTextArt(resumeButton?.transform.parent?.Find("Title"),
                gameplayArtCatalog.ShellPauseTitle, "DeliveredPauseTitle");
            ApplyShellTextArt(bgmSlider?.transform.parent?.Find("Title"),
                gameplayArtCatalog.ShellSettings, "DeliveredSettingsTitle");
            ApplyShellTextArt(bgmSlider?.transform.parent?.Find("BgmLabel"),
                gameplayArtCatalog.ShellBgmLabel, "DeliveredBgmLabel");
            ApplyShellTextArt(sfxSlider?.transform.parent?.Find("SfxLabel"),
                gameplayArtCatalog.ShellSfxLabel, "DeliveredSfxLabel");
            ApplyToggleArt(fullscreenToggle, gameplayArtCatalog.ShellCheckOff,
                gameplayArtCatalog.ShellCheckOn);
            ConfigurePauseHoverIndicator(gameplayArtCatalog.ShellPlayIcon);
            ApplyDecorationArt(resumeButton?.transform.parent?.Find("Title"),
                gameplayArtCatalog.ShellPauseIcon, "DeliveredPauseIcon", new Vector2(-35f, 0f));
            bgmSpeakerImage = ApplyVolumeSliderArt(bgmSlider);
            sfxSpeakerImage = ApplyVolumeSliderArt(sfxSlider);
            RefreshSpeakerIcon(bgmSpeakerImage, bgmSlider != null ? bgmSlider.value : 0f);
            RefreshSpeakerIcon(sfxSpeakerImage, sfxSlider != null ? sfxSlider.value : 0f);
            if (bgmSlider != null)
                bgmSlider.onValueChanged.AddListener(value => RefreshSpeakerIcon(bgmSpeakerImage, value));
            if (sfxSlider != null)
                sfxSlider.onValueChanged.AddListener(value => RefreshSpeakerIcon(sfxSpeakerImage, value));
        }

        private void ApplyDeliveredButtonArt()
        {
            RuntimeUiButtonArt.Apply(resumeButton, gameplayArtCatalog);
            RuntimeUiButtonArt.Apply(pauseSaveButton, gameplayArtCatalog);
            RuntimeUiButtonArt.Apply(settingsButton, gameplayArtCatalog);
            RuntimeUiButtonArt.Apply(returnTitleButton, gameplayArtCatalog);
            RuntimeUiButtonArt.Apply(settingsApplyButton, gameplayArtCatalog);
            RuntimeUiButtonArt.Apply(settingsBackButton, gameplayArtCatalog);
            RuntimeUiButtonArt.Apply(confirmButton, gameplayArtCatalog);
            RuntimeUiButtonArt.Apply(cancelButton, gameplayArtCatalog);
            RuntimeUiButtonArt.Apply(titleContinueButton, gameplayArtCatalog);
            RuntimeUiButtonArt.Apply(titleNewGameButton, gameplayArtCatalog);
            RuntimeUiButtonArt.Apply(titleQuitButton, gameplayArtCatalog);
            RuntimeUiButtonArt.Apply(resultTitleButton, gameplayArtCatalog);
            ApplyButtonArray(demoSaveButtons);
        }

        private void ApplyButtonArray(IEnumerable<Button> buttons)
        {
            if (buttons == null) return;
            foreach (var button in buttons) RuntimeUiButtonArt.Apply(button, gameplayArtCatalog);
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

        private static void ApplyShellTextArt(Transform target, Sprite sprite, string artName)
        {
            if (target == null || sprite == null) return;
            var label = target.GetComponent<Text>();
            if (label != null) label.enabled = false;
            var artTransform = target.Find(artName) as RectTransform;
            if (artTransform == null)
            {
                var artObject = new GameObject(artName, typeof(RectTransform), typeof(Image));
                artObject.transform.SetParent(target, false);
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

        private static void ApplyDecorationArt(Transform target, Sprite sprite, string artName, Vector2 position)
        {
            if (target == null || sprite == null) return;
            var artTransform = target.Find(artName) as RectTransform;
            if (artTransform == null)
            {
                var artObject = new GameObject(artName, typeof(RectTransform), typeof(Image));
                artObject.transform.SetParent(target, false);
                artTransform = (RectTransform)artObject.transform;
            }
            artTransform.anchorMin = artTransform.anchorMax = artTransform.pivot = new Vector2(.5f, .5f);
            artTransform.anchoredPosition = position;
            artTransform.sizeDelta = sprite.rect.size;
            var image = artTransform.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        private static void ApplyToggleArt(Toggle toggle, Sprite offSprite, Sprite onSprite)
        {
            if (toggle == null || offSprite == null || onSprite == null) return;
            var offSize = offSprite.rect.size;
            if (toggle.targetGraphic is Image background)
            {
                background.sprite = offSprite;
                background.color = Color.white;
                background.preserveAspect = true;
                background.rectTransform.sizeDelta = offSize;
            }
            if (toggle.graphic is Image checkmark)
            {
                checkmark.sprite = onSprite;
                checkmark.color = Color.white;
                checkmark.preserveAspect = true;
                checkmark.rectTransform.anchorMin = checkmark.rectTransform.anchorMax =
                    checkmark.rectTransform.pivot = new Vector2(.5f, .5f);
                checkmark.rectTransform.anchoredPosition = Vector2.zero;
                checkmark.rectTransform.sizeDelta = offSize;
            }
        }

        private void ConfigurePauseHoverIndicator(Sprite sprite)
        {
            var pausePanel = resumeButton != null ? resumeButton.transform.parent : null;
            if (pausePanel == null || sprite == null) return;

            pauseHoverIndicator = pausePanel.Find("DeliveredPauseSelectionArrow") as RectTransform;
            if (pauseHoverIndicator == null)
            {
                var indicatorObject = new GameObject("DeliveredPauseSelectionArrow",
                    typeof(RectTransform), typeof(Image));
                indicatorObject.transform.SetParent(pausePanel, false);
                pauseHoverIndicator = (RectTransform)indicatorObject.transform;
            }

            pauseHoverIndicator.anchorMin = pauseHoverIndicator.anchorMax = pauseHoverIndicator.pivot =
                new Vector2(.5f, .5f);
            pauseHoverIndicator.sizeDelta = sprite.rect.size;
            var indicatorImage = pauseHoverIndicator.GetComponent<Image>();
            indicatorImage.sprite = sprite;
            indicatorImage.color = Color.white;
            indicatorImage.preserveAspect = true;
            indicatorImage.raycastTarget = false;
            pauseHoverIndicator.SetAsLastSibling();

            BindPauseHoverTarget(resumeButton);
            BindPauseHoverTarget(pauseSaveButton);
            BindPauseHoverTarget(settingsButton);
            BindPauseHoverTarget(returnTitleButton);
            MovePauseHoverIndicator(resumeButton);
        }

        private void BindPauseHoverTarget(Button button)
        {
            if (button == null) return;
            var trigger = button.GetComponent<EventTrigger>() ?? button.gameObject.AddComponent<EventTrigger>();
            trigger.triggers ??= new List<EventTrigger.Entry>();
            AddPauseHoverTrigger(trigger, EventTriggerType.PointerEnter, button);
            AddPauseHoverTrigger(trigger, EventTriggerType.Select, button);
        }

        private void AddPauseHoverTrigger(EventTrigger trigger, EventTriggerType eventType, Button button)
        {
            var entry = new EventTrigger.Entry { eventID = eventType };
            entry.callback.AddListener(_ => MovePauseHoverIndicator(button));
            trigger.triggers.Add(entry);
        }

        private void MovePauseHoverIndicator(Button button)
        {
            if (pauseHoverIndicator == null || button == null) return;
            var buttonRect = button.GetComponent<RectTransform>();
            if (buttonRect == null) return;
            pauseHoverIndicator.anchoredPosition = new Vector2(
                buttonRect.anchoredPosition.x - buttonRect.sizeDelta.x * .5f + 20f,
                buttonRect.anchoredPosition.y);
            pauseHoverIndicator.SetAsLastSibling();
        }

        private Image ApplyVolumeSliderArt(Slider slider)
        {
            if (slider == null || gameplayArtCatalog.ShellVolumeBar == null ||
                gameplayArtCatalog.ShellVolumeHandle == null) return null;
            var sliderRect = slider.GetComponent<RectTransform>();
            var barSize = gameplayArtCatalog.ShellVolumeBar.rect.size;
            var handleSize = gameplayArtCatalog.ShellVolumeHandle.rect.size;
            sliderRect.sizeDelta = new Vector2(barSize.x, Mathf.Max(16f, handleSize.y));
            var background = slider.transform.Find("Background")?.GetComponent<Image>();
            if (background != null)
            {
                background.sprite = gameplayArtCatalog.ShellVolumeBar;
                background.color = Color.white;
                background.preserveAspect = true;
                var rect = background.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = barSize;
            }
            if (slider.fillRect != null)
            {
                slider.fillRect.gameObject.SetActive(false);
                slider.fillRect = null;
            }
            var handle = slider.handleRect?.GetComponent<Image>();
            if (handle != null)
            {
                var handleSlideArea = slider.transform.Find("Handle Slide Area") as RectTransform;
                if (handleSlideArea == null)
                {
                    var areaObject = new GameObject("Handle Slide Area", typeof(RectTransform));
                    areaObject.transform.SetParent(slider.transform, false);
                    handleSlideArea = (RectTransform)areaObject.transform;
                }
                handleSlideArea.anchorMin = Vector2.zero;
                handleSlideArea.anchorMax = Vector2.one;
                handleSlideArea.offsetMin = new Vector2(handleSize.x * .5f, 0f);
                handleSlideArea.offsetMax = new Vector2(handleSize.x * -.5f, 0f);
                handle.rectTransform.SetParent(handleSlideArea, false);
                handle.sprite = gameplayArtCatalog.ShellVolumeHandle;
                handle.color = Color.white;
                handle.preserveAspect = true;
                handle.rectTransform.sizeDelta = handleSize;
                slider.handleRect = handle.rectTransform;
            }
            var speakerTransform = slider.transform.Find("Speaker") as RectTransform;
            if (speakerTransform == null)
            {
                var speakerObject = new GameObject("Speaker", typeof(RectTransform), typeof(Image));
                speakerObject.transform.SetParent(slider.transform, false);
                speakerTransform = (RectTransform)speakerObject.transform;
            }
            speakerTransform.anchorMin = speakerTransform.anchorMax = speakerTransform.pivot = new Vector2(.5f, .5f);
            speakerTransform.anchoredPosition = new Vector2(-44f, 0f);
            speakerTransform.sizeDelta = new Vector2(16f, 16f);
            var speaker = speakerTransform.GetComponent<Image>();
            speaker.preserveAspect = true;
            speaker.raycastTarget = false;
            return speaker;
        }

        private void RefreshSpeakerIcon(Image image, float value)
        {
            if (image == null || gameplayArtCatalog == null) return;
            image.sprite = value <= .001f
                ? gameplayArtCatalog.ShellSpeakerMuted
                : value < .5f
                    ? gameplayArtCatalog.ShellSpeakerLow
                    : gameplayArtCatalog.ShellSpeakerHigh;
            image.enabled = image.sprite != null;
        }

        private void Update()
        {
            if (!IsInitialized) return;
            RefreshPauseControls();
            if (Input.GetKeyDown(KeyCode.Escape) &&
                !MainGameBossSummonUiController.ConsumeEscapeIfDebugHelpOpen() &&
                !MainGameCraftingUiController.ConsumedEscapeThisFrame &&
                !MainGameTurretRuntime.ConsumedEscapeThisFrame &&
                !MainGameTilePaletteController.ConsumedEscapeThisFrame &&
                (codex == null || !codex.IsOpen))
            {
                switch (shell.Screen)
                {
                    case GameShellScreen.Gameplay: shell.OpenPause(); break;
                    case GameShellScreen.Pause: shell.ResumeGameplay(); break;
                    case GameShellScreen.Settings: shell.CloseSettings(); break;
                    case GameShellScreen.Confirmation: shell.CancelConfirmation(); break;
                }
            }
            if (confirmationText != null && shell.Screen == GameShellScreen.Confirmation)
            {
                switch (shell.PendingConfirmation)
                {
                    case GameShellConfirmation.ReturnToTitle:
                        confirmationText.text = "타이틀로 돌아갈까요? 저장하지 않은 진행은 사라집니다.";
                        break;
                    case GameShellConfirmation.LoadDemoSave:
                        confirmationText.text = $"{shell.PendingDemoDay}일차 데모를 자동저장 슬롯에 복사할까요?";
                        break;
                    default:
                        confirmationText.text = "기존 자동 저장을 지우고 새 게임을 시작할까요?";
                        break;
                }
            }
        }

        private void BindButtons()
        {
            resumeButton.onClick.AddListener(() => shell.ResumeGameplay());
            pauseSaveButton.onClick.AddListener(SaveCurrentProgress);
            settingsButton.onClick.AddListener(OpenSettings);
            returnTitleButton.onClick.AddListener(() => shell.RequestReturnToTitle());
            settingsApplyButton.onClick.AddListener(ApplySettings);
            settingsBackButton.onClick.AddListener(() => shell.CloseSettings());
            confirmButton.onClick.AddListener(ConfirmPendingAction);
            cancelButton.onClick.AddListener(() => shell.CancelConfirmation());
            titleContinueButton.onClick.AddListener(() => SetStatus(shell.TryContinue() ? "불러오기 완료" : "저장 파일이 없습니다."));
            titleNewGameButton.onClick.AddListener(() => shell.RequestNewGame());
            titleQuitButton.onClick.AddListener(() => shell.RequestQuit());
            resultTitleButton.onClick.AddListener(() => shell.ReturnFromResultToTitle());
        }

        private void ConfigurePauseMenuLayout()
        {
            pauseSaveButton = saveButtons[0];
            SetButtonLabel(resumeButton, "계속");
            SetButtonLabel(pauseSaveButton, "저장");
            SetButtonLabel(settingsButton, "설정");
            SetButtonLabel(returnTitleButton, "타이틀로");

            ConfigurePauseCard();
            ConfigurePauseButton(resumeButton, 36f);
            ConfigurePauseButton(pauseSaveButton, 7f);
            ConfigurePauseButton(settingsButton, -22f);
            ConfigurePauseButton(returnTitleButton, -51f);

            for (var index = 1; index < saveButtons.Length; index++)
                if (saveButtons[index] != null) saveButtons[index].gameObject.SetActive(false);
            for (var index = 0; index < loadButtons.Length; index++)
                if (loadButtons[index] != null) loadButtons[index].gameObject.SetActive(false);
        }

        private static void ConfigurePauseButton(Button button, float y)
        {
            if (button == null) return;
            button.gameObject.SetActive(true);
            var rect = button.GetComponent<RectTransform>();
            if (rect == null) return;
            rect.anchoredPosition = new Vector2(0f, y);
            // PausePanel is migrated to the native 480x270 UI root before this controller starts.
            // Keep these values in native coordinates (legacy 280x52 scaled by 0.25).
            rect.sizeDelta = new Vector2(150f, 24f);
        }

        private void ConfigurePauseCard()
        {
            var pausePanel = resumeButton != null ? resumeButton.transform.parent : null;
            if (pausePanel == null) return;
            var card = pausePanel.Find("PauseMenuCard") as RectTransform;
            if (card == null)
            {
                var cardObject = new GameObject("PauseMenuCard", typeof(RectTransform), typeof(Image), typeof(Outline));
                cardObject.transform.SetParent(pausePanel, false);
                card = (RectTransform)cardObject.transform;
            }
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f);
            card.anchoredPosition = Vector2.zero;
            card.sizeDelta = new Vector2(182f, 204f);
            var image = card.GetComponent<Image>();
            image.color = new Color(.045f, .065f, .11f, .97f);
            image.raycastTarget = false;
            var outline = card.GetComponent<Outline>();
            outline.effectColor = new Color(.72f, .48f, .12f, .85f);
            outline.effectDistance = new Vector2(1f, -1f);
            card.SetAsFirstSibling();
            var title = pausePanel.Find("Title") as RectTransform;
            if (title != null)
            {
                title.anchoredPosition = new Vector2(0f, 76f);
                title.sizeDelta = new Vector2(160f, 24f);
            }

            // Keep the keyboard hint separate from the return-to-title button.
            // The legacy layout placed it at the same height as the last button.
            if (statusText != null)
            {
                var statusRect = statusText.rectTransform;
                statusRect.anchorMin = statusRect.anchorMax = statusRect.pivot = new Vector2(.5f, .5f);
                statusRect.anchoredPosition = new Vector2(0f, -83f);
                statusRect.sizeDelta = new Vector2(150f, 14f);
                statusText.alignment = TextAnchor.MiddleCenter;
                statusText.fontSize = 8;
                statusText.transform.SetAsLastSibling();
            }
        }

        private void ConfigureTitleMenuLayout()
        {
            ConfigureShellButton(titleContinueButton, new Vector2(-112f, 17f), new Vector2(150f, 27f));
            ConfigureShellButton(titleNewGameButton, new Vector2(-112f, -17f), new Vector2(150f, 27f));
            ConfigureShellButton(titleQuitButton, new Vector2(-112f, -51f), new Vector2(150f, 27f));
            for (var index = 0; index < demoSaveButtons.Count; index++)
                ConfigureShellButton(demoSaveButtons[index],
                    new Vector2(-162f + index * 50f, -82f), new Vector2(46f, 15f));
        }

        private void ConfigureSettingsMenuLayout()
        {
            var settingsPanel = bgmSlider != null ? bgmSlider.transform.parent : null;
            if (settingsPanel == null) return;
            var card = settingsPanel.Find("SettingsMenuCard") as RectTransform;
            if (card == null)
            {
                var cardObject = new GameObject("SettingsMenuCard", typeof(RectTransform), typeof(Image), typeof(Outline));
                cardObject.transform.SetParent(settingsPanel, false);
                card = (RectTransform)cardObject.transform;
            }
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f);
            card.anchoredPosition = Vector2.zero;
            card.sizeDelta = new Vector2(220f, 210f);
            var image = card.GetComponent<Image>();
            image.color = new Color(.045f, .065f, .11f, .98f);
            image.raycastTarget = false;
            var outline = card.GetComponent<Outline>();
            outline.effectColor = new Color(.72f, .48f, .12f, .85f);
            outline.effectDistance = new Vector2(1f, -1f);
            card.SetAsFirstSibling();

            ConfigureNamedRect(settingsPanel, "Title", new Vector2(0f, 78f), new Vector2(180f, 24f));
            ConfigureNamedRect(settingsPanel, "BgmLabel", new Vector2(-72f, 31f), new Vector2(54f, 18f));
            ConfigureNamedRect(settingsPanel, "BgmVolume", new Vector2(31f, 31f), new Vector2(96f, 18f));
            ConfigureNamedRect(settingsPanel, "SfxLabel", new Vector2(-72f, -5f), new Vector2(54f, 18f));
            ConfigureNamedRect(settingsPanel, "SfxVolume", new Vector2(31f, -5f), new Vector2(96f, 18f));
            ConfigureFullscreenToggleLayout(settingsPanel);
            ConfigureShellButton(settingsApplyButton, new Vector2(-43f, -78f), new Vector2(78f, 22f));
            ConfigureShellButton(settingsBackButton, new Vector2(43f, -78f), new Vector2(78f, 22f));
        }

        private static void ConfigureFullscreenToggleLayout(Transform settingsPanel)
        {
            var toggleRect = settingsPanel != null ? settingsPanel.Find("Fullscreen") as RectTransform : null;
            if (toggleRect == null) return;

            toggleRect.anchorMin = toggleRect.anchorMax = toggleRect.pivot = new Vector2(.5f, .5f);
            toggleRect.anchoredPosition = new Vector2(0f, -42f);
            toggleRect.sizeDelta = new Vector2(180f, 20f);

            var labelRect = toggleRect.Find("Label") as RectTransform;
            if (labelRect != null)
            {
                labelRect.anchorMin = labelRect.anchorMax = labelRect.pivot = new Vector2(.5f, .5f);
                labelRect.anchoredPosition = new Vector2(-72f, 0f);
                labelRect.sizeDelta = new Vector2(54f, 18f);
                var label = labelRect.GetComponent<Text>();
                if (label != null)
                {
                    label.alignment = TextAnchor.MiddleCenter;
                    label.fontSize = 6;
                }
            }

            var backgroundRect = toggleRect.Find("Background") as RectTransform;
            if (backgroundRect == null) return;
            backgroundRect.anchorMin = backgroundRect.anchorMax = backgroundRect.pivot = new Vector2(.5f, .5f);
            backgroundRect.anchoredPosition = new Vector2(18f, 0f);
            backgroundRect.sizeDelta = new Vector2(9f, 9f);
        }

        private static void ConfigureNamedRect(Transform parent, string childName, Vector2 position, Vector2 size)
        {
            var rect = parent != null ? parent.Find(childName) as RectTransform : null;
            if (rect == null) return;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void ConfigureShellButton(Button button, Vector2 position, Vector2 size)
        {
            if (button == null) return;
            var rect = button.GetComponent<RectTransform>();
            if (rect == null) return;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetButtonLabel(Button button, string label)
        {
            var text = button != null ? button.GetComponentInChildren<Text>(true) : null;
            if (text != null) text.text = label;
        }

        private void RefreshPauseControls()
        {
            if (pauseSaveButton != null)
                pauseSaveButton.interactable = bossManager == null || !bossManager.IsBossActive;
            if (pauseHoverIndicator != null)
                pauseHoverIndicator.gameObject.SetActive(shell != null && shell.Screen == GameShellScreen.Pause);

            // Settings is displayed on top of the paused gameplay view, so the pause-only
            // keyboard hint must be hidden until the player returns to the pause card.
            if (statusText != null)
                statusText.gameObject.SetActive(shell != null && shell.Screen == GameShellScreen.Pause);
        }

        private void CreateDemoSaveButtons()
        {
            if (titleNewGameButton == null || titleQuitButton == null) return;
            var parent = titleNewGameButton.transform.parent;
            var templateRect = titleNewGameButton.GetComponent<RectTransform>();
            var quitRect = titleQuitButton.GetComponent<RectTransform>();
            if (parent == null || templateRect == null || quitRect == null) return;

            for (var index = 0; index < GameShellController.DemoSaveDays.Length; index++)
            {
                var day = GameShellController.DemoSaveDays[index];
                var button = Instantiate(titleNewGameButton, parent);
                button.name = $"DemoSaveDay{day}";
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => RequestDemoSave(day));
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

        private void RequestDemoSave(int day)
        {
            if (shell.RequestDemoSave(day))
                SetStatus($"{day}일차 데모를 자동저장 슬롯에 복사합니다.");
        }

        private void ConfirmPendingAction()
        {
            var confirmation = shell.PendingConfirmation;
            var demoDay = shell.PendingDemoDay;
            if (confirmation == GameShellConfirmation.LoadDemoSave) demoLoadApplied = false;
            if (shell.Confirm())
            {
                if (confirmation == GameShellConfirmation.LoadDemoSave)
                {
                    if (demoLoadApplied) SetStatus($"{demoDay}일차 데모 불러오기 완료");
                    else
                    {
                        shell.EnterTitle();
                        SetStatus($"{demoDay}일차 데모 월드 복원 실패 · 데모 세이브를 다시 생성하세요.");
                    }
                }
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

        private void SaveCurrentProgress()
        {
            if (bossManager != null && bossManager.IsBossActive)
            {
                SetStatus("보스 전투 중에는 저장할 수 없습니다.");
                return;
            }
            var slot = shell.ActiveSaveSlot;
            var succeeded = saveCoordinator.SaveNow(slot);
            SetStatus(succeeded ? "저장 완료" : "저장에 실패했습니다.");
            shell.RefreshTitle();
        }

        private void OpenSettings()
        {
            bgmSlider.value = audioService.BgmVolume;
            sfxSlider.value = audioService.SfxVolume;
            fullscreenToggle.isOn = Screen.fullScreen;
            shell.OpenSettings();
        }

        private void ApplySettings()
        {
            var succeeded = shell.TryApplySettings(bgmSlider.value, sfxSlider.value, fullscreenToggle.isOn);
            if (succeeded) audioService?.EnsureAudiblePlayback();
            SetStatus(succeeded ? "설정 적용 완료" : "설정 적용 실패");
            if (succeeded) shell.CloseSettings();
        }

        private void HandleNewGameRequested(int slot)
        {
            if (saveManager.HasSave(slot)) saveManager.Delete(slot);
            enterGameplayAfterReload = true;
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void HandleContinueRequested(int slot, SaveGame _) => saveCoordinator.TryLoad(slot);
        private void HandleDemoSaveRequested(SaveGame demo) =>
            demoLoadApplied = saveCoordinator.TryApplyDemoSnapshot(demo);

        private void HandleMvpDawn()
        {
            if (!GameShellController.ShouldEndDemoAtDawn(timeService.Day,
                    timeService.MvpContentDayLimit)) return;
            var snapshot = saveCoordinator.CaptureSnapshot();
            if (snapshot == null)
            {
                Debug.LogError("[Nyangbingo] 30일차 결과 스냅샷 생성에 실패했습니다.");
                return;
            }
            shell.ShowResult(snapshot);
            RefreshResultView();
            Debug.Log("[Nyangbingo] 30일차 밤 종료 후 MVP 결과 화면을 표시했습니다.");
        }

        private void BuildResultView()
        {
            if (resultTitleButton == null || resultSummaryText != null) return;
            var panel = resultTitleButton.transform.parent as RectTransform;
            if (panel == null) return;
            resultHeaderText = panel.Find("Result")?.GetComponent<Text>();
            var buttonLabel = resultTitleButton.GetComponentInChildren<Text>(true);
            var font = buttonLabel != null ? buttonLabel.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            if (resultHeaderText != null)
            {
                resultHeaderText.text = "30일차 데모 종료";
                resultHeaderText.fontSize = 18;
                resultHeaderText.fontStyle = FontStyle.Bold;
                var headerRect = resultHeaderText.rectTransform;
                headerRect.anchoredPosition = new Vector2(0f, 98f);
                headerRect.sizeDelta = new Vector2(340f, 30f);
            }

            resultSummaryText = CreateResultText(panel, "Summary", font, 9, TextAnchor.UpperLeft,
                new Vector2(0f, 18f), new Vector2(340f, 126f));
            resultTeaserText = CreateResultText(panel, "Teaser", font, 15, TextAnchor.MiddleCenter,
                new Vector2(0f, -68f), new Vector2(240f, 26f));
            resultTeaserText.fontStyle = FontStyle.Bold;

            var buttonRect = resultTitleButton.GetComponent<RectTransform>();
            buttonRect.anchoredPosition = new Vector2(0f, -106f);
            buttonRect.sizeDelta = new Vector2(110f, 22f);
            if (buttonLabel != null)
            {
                buttonLabel.text = "타이틀로";
                buttonLabel.fontSize = 9;
            }
        }

        private static Text CreateResultText(Transform parent, string name, Font font, int fontSize,
            TextAnchor alignment, Vector2 position, Vector2 size)
        {
            var textObject = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)textObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = textObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(.94f, .96f, 1f, 1f);
            text.raycastTarget = false;
            return text;
        }

        private void RefreshResultView()
        {
            var result = shell.Result;
            if (result == null || resultSummaryText == null || resultTeaserText == null) return;
            var completed = new HashSet<string>(result.CompletedModuleIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            var modules = gameDataCatalog?.Modules;
            var totalModules = modules?.Count ?? 0;
            var installedModules = 0;
            if (modules != null)
                for (var index = 0; index < modules.Count; index++)
                    if (modules[index] != null && completed.Contains(modules[index].Id)) installedModules++;

            var builder = new StringBuilder();
            builder.AppendLine(result.SealPercentage >= 100f
                ? $"✓ 석빙고 온도 {result.SealPercentage:0.#}%"
                : $"□ 석빙고 온도 {result.SealPercentage:0.#}% / 100%");
            builder.AppendLine($"핵심 모듈 {installedModules}/{totalModules}");
            if (modules != null)
            {
                for (var index = 0; index < modules.Count; index++)
                {
                    var module = modules[index];
                    if (module == null) continue;
                    builder.Append(completed.Contains(module.Id) ? "✓ " : "□ ")
                        .AppendLine(module.DisplayName);
                }
            }
            builder.AppendLine(result.GangcheolDefeated ? "✓ 강철이 격퇴" : "□ 강철이 도주");
            builder.AppendLine();
            builder.AppendLine($"요괴 처치 {result.YokaiKills}");
            builder.AppendLine($"채굴 타일 {result.MinedTiles}");
            builder.AppendLine($"사망 횟수 {result.Deaths}");
            resultSummaryText.text = builder.ToString().TrimEnd();
            resultTeaserText.text = DemoResultState.Teaser;
        }

        private void HandleTitleRequested()
        {
            Time.timeScale = 0f;
            shell.RefreshTitle();
            RefreshTitleControls();
        }
        private void SetStatus(string value) { if (statusText != null) statusText.text = value; }

        private void OnDestroy()
        {
            if (shell != null)
            {
                shell.NewGameRequested -= HandleNewGameRequested;
                shell.ContinueRequested -= HandleContinueRequested;
                shell.DemoSaveRequested -= HandleDemoSaveRequested;
                shell.TitleRequested -= HandleTitleRequested;
            }
            if (timeService != null) timeService.Dawn -= HandleMvpDawn;
            Time.timeScale = 1f;
        }
    }
}
