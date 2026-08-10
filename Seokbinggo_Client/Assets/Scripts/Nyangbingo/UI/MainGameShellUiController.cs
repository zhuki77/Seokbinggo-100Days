using System;
using Nyangbingo.Audio;
using Nyangbingo.Bosses;
using Nyangbingo.Data;
using Nyangbingo.Save;
using Nyangbingo.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Text;

namespace Nyangbingo.UI
{
    /// <summary>
    /// MainGame.unity 전용 버튼 배선/연출. 타이틀 관련 부분은 Title.unity의 TitleUiController로 분리되었다.
    /// 부팅 시 MainGameLaunchRequest(Continue/NewGame/DemoLoad)를 읽어 셸을 곧바로 Gameplay로 진입시킨다.
    /// </summary>
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
        [SerializeField] private Button resultTitleButton;
        [SerializeField] private Text statusText;
        [SerializeField] private GameplayArtCatalog gameplayArtCatalog;

        private GameDataCatalog gameDataCatalog;
        private BossManager bossManager;
        private Text resultHeaderText;
        private Text resultSummaryText;
        private Text resultTeaserText;
        private Image bgmSpeakerImage;
        private Image sfxSpeakerImage;
        private Button pauseSaveButton;
        private RectTransform pauseHoverIndicator;

        public int BoundSaveSlotCount => saveButtons?.Length ?? 0;
        public bool IsInitialized { get; private set; }

        /// <summary>NyangbingoMainGameSceneCreator(에디터 씬 생성 도구) 전용 빌드타임 배선 진입점.</summary>
        public void ConfigureForScene(GameShellController shellController, MainGameSaveCoordinator coordinator,
            SaveManager saves, NyangbingoAudioService audio, DayNightService clock, MainGameCodexController codexUi,
            Button resume, Button[] saveSlotButtons, Button[] loadSlotButtons, Button settings, Button returnTitle,
            Button applySettings, Button backSettings, Slider bgm, Slider sfx, Toggle fullscreen,
            Button confirm, Button cancel, Text confirmationLabel, Button resultTitle, Text status,
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
            resultTitleButton = resultTitle;
            statusText = status;
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

            LoadingOverlayRequest.Begin();

            audioService.Initialize();
            shell.ConfigureForRuntime(audioService, saveCoordinator.CaptureSnapshot(), Application.isMobilePlatform);
            shell.TitleRequested += HandleTitleRequested;
            gameDataCatalog = FindAnyObjectByType<MainGameBootstrap>()?.GameDataCatalog;
            bossManager = FindAnyObjectByType<BossManager>();
            timeService.Dawn += HandleMvpDawn;
            ConfigurePauseMenuLayout();
            BindButtons();
            BuildResultView();
            ConfigureSettingsMenuLayout();
            bgmSlider.value = audioService.BgmVolume;
            sfxSlider.value = audioService.SfxVolume;
            fullscreenToggle.isOn = Screen.fullScreen;
            if (gameplayArtCatalog == null)
                Debug.LogError("[Nyangbingo] MainGameShellUiController: gameplayArtCatalog 배선이 비어 있습니다.");
            ApplyDeliveredShellArt();

            if (!TryResolveLaunchSave(out var launchSave))
            {
                LoadingOverlayRequest.MarkReady();
                SceneTransitionRequest.Begin("Title");
                return;
            }

            MainGameLaunchRequest.Reset();
            shell.EnterGameplay(launchSave);
            Time.timeScale = 1f;
            SetStatus(string.Empty);
            IsInitialized = true;
            LoadingOverlayRequest.MarkReady();
            Debug.Log("[Nyangbingo] MainGameShellUiController: 일시정지 4항목·현재 슬롯 저장·설정 셸 연결 완료.");
        }

        private bool TryResolveLaunchSave(out SaveGame launchSave)
        {
            switch (MainGameLaunchRequest.RequestedMode)
            {
                case MainGameLaunchRequest.Mode.NewGame:
                    saveManager.DeleteAll();
                    launchSave = CreateFreshInitialSave();
                    if (launchSave != null) return true;
                    Debug.LogError("[Nyangbingo] 새 게임 생성 실패 — 타이틀로 복귀합니다.");
                    return false;
                case MainGameLaunchRequest.Mode.DemoLoad:
                    if (saveManager.TryLoad(MainGameLaunchRequest.SaveSlot, out var demo) &&
                        saveCoordinator.TryApplyDemoSnapshot(demo))
                    {
                        launchSave = demo;
                        return true;
                    }
                    Debug.LogError("[Nyangbingo] 데모 세이브 적용 실패 — 타이틀로 복귀합니다.");
                    launchSave = null;
                    return false;
                default:
                    if (saveCoordinator.TryLoad(MainGameLaunchRequest.SaveSlot))
                    {
                        launchSave = saveCoordinator.CaptureSnapshot();
                        return launchSave != null;
                    }
                    Debug.LogError(
                        "[Nyangbingo] 저장 데이터 복원 실패 — 타이틀로 복귀합니다. " +
                        "최근 맵 생성 변경(가로 1.5배·중간층 동굴 등) 이후에는 구 세이브가 호환되지 않을 수 있습니다. '새 게임'으로 시작하세요.");
                    launchSave = null;
                    return false;
            }
        }

        private SaveGame CreateFreshInitialSave()
        {
            var initialSnapshot = saveCoordinator.CaptureSnapshot();
            if (initialSnapshot == null)
            {
                Debug.LogError("[Nyangbingo] Failed to capture the fresh new-game snapshot.");
                return null;
            }

            saveManager.Save(GameShellController.AutoSaveSlot, initialSnapshot);
            Debug.Log($"[Nyangbingo] Fresh new-game save created " +
                      $"(seed={initialSnapshot.seed}, day={initialSnapshot.day}).");
            return initialSnapshot;
        }

        private void ApplyDeliveredShellArt()
        {
            if (gameplayArtCatalog == null) return;
            ApplyDeliveredButtonArt();
            ApplyButtonLabelArt(resumeButton, gameplayArtCatalog.ShellResume);
            ApplyButtonLabelArt(pauseSaveButton, gameplayArtCatalog.ShellSave);
            ApplyButtonLabelArt(settingsButton, gameplayArtCatalog.ShellSettings);
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
                bgmSlider.onValueChanged.AddListener(value =>
                {
                    RefreshSpeakerIcon(bgmSpeakerImage, value);
                    PreviewSettingsVolumes();
                });
            if (sfxSlider != null)
                sfxSlider.onValueChanged.AddListener(value =>
                {
                    RefreshSpeakerIcon(sfxSpeakerImage, value);
                    PreviewSettingsVolumes();
                });
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
            RuntimeUiButtonArt.Apply(resultTitleButton, gameplayArtCatalog);
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
                !SceneTransitionRequest.IsTransitionActive &&
                !MainGameBossSummonUiController.ConsumeEscapeIfDebugHelpOpen() &&
                !MainGameCraftingUiController.BlocksGameplayInput &&
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
                confirmationText.text = "타이틀로 돌아갈까요? 저장하지 않은 진행은 사라집니다.";
        }

        private void BindButtons()
        {
            resumeButton.onClick.AddListener(() => shell.ResumeGameplay());
            pauseSaveButton.onClick.AddListener(SaveCurrentProgress);
            settingsButton.onClick.AddListener(OpenSettings);
            returnTitleButton.onClick.AddListener(() => shell.RequestReturnToTitle());
            settingsApplyButton.onClick.AddListener(ApplySettings);
            settingsBackButton.onClick.AddListener(() => shell.CloseSettings());
            confirmButton.onClick.AddListener(() => shell.Confirm());
            cancelButton.onClick.AddListener(() => shell.CancelConfirmation());
            resultTitleButton.onClick.AddListener(() => shell.ReturnFromResultToTitle());
        }

        private void ConfigurePauseMenuLayout()
        {
            pauseSaveButton = saveButtons[0];
            RemoveLegacySaveSlotObjects();
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

        private void RemoveLegacySaveSlotObjects()
        {
            var pausePanel = pauseSaveButton != null ? pauseSaveButton.transform.parent : null;
            if (pausePanel == null) return;
            var legacyNames = new[] { "Save_2", "Save_3", "Load_2", "Load_3" };
            for (var index = 0; index < legacyNames.Length; index++)
            {
                var legacy = pausePanel.Find(legacyNames[index]);
                if (legacy == null) continue;
                legacy.gameObject.SetActive(false);
                Destroy(legacy.gameObject);
            }
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
            SetStatus(succeeded ? "설정 적용 완료" : "설정 적용 실패");
            if (succeeded) shell.CloseSettings();
        }

        private void PreviewSettingsVolumes()
        {
            if (audioService == null || bgmSlider == null || sfxSlider == null) return;
            audioService.TryPreviewBusVolumes(bgmSlider.value, sfxSlider.value);
        }

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
            builder.AppendLine(result.ImugiDefeated ? "✓ 이무기 격퇴" : "□ 이무기 도주");
            builder.AppendLine();
            builder.AppendLine($"요괴 처치 {result.YokaiKills}");
            builder.AppendLine($"채굴 타일 {result.MinedTiles}");
            builder.AppendLine($"사망 횟수 {result.Deaths}");
            resultSummaryText.text = builder.ToString().TrimEnd();
            resultTeaserText.text = DemoResultState.Teaser;
        }

        private void HandleTitleRequested()
        {
            Time.timeScale = 1f;
            SceneTransitionRequest.Begin("Title");
        }

        private void SetStatus(string value) { if (statusText != null) statusText.text = value; }

        private void OnDestroy()
        {
            if (shell != null) shell.TitleRequested -= HandleTitleRequested;
            if (timeService != null) timeService.Dawn -= HandleMvpDawn;
        }
    }
}
