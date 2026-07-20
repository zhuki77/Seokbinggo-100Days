using System;
using Nyangbingo.Audio;
using Nyangbingo.Bosses;
using Nyangbingo.Data;
using Nyangbingo.Save;
using Nyangbingo.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;
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

        private readonly List<Button> demoSaveButtons = new List<Button>();
        private GameDataCatalog gameDataCatalog;
        private BossManager bossManager;
        private Text resultHeaderText;
        private Text resultSummaryText;
        private Text resultTeaserText;
        private bool demoLoadApplied;
        private static bool enterGameplayAfterReload;

        public int BoundSaveSlotCount => saveButtons?.Length ?? 0;
        public bool IsInitialized { get; private set; }

        public void ConfigureForScene(GameShellController shellController, MainGameSaveCoordinator coordinator,
            SaveManager saves, NyangbingoAudioService audio, DayNightService clock, MainGameCodexController codexUi,
            Button resume, Button[] saveSlotButtons, Button[] loadSlotButtons, Button settings, Button returnTitle,
            Button applySettings, Button backSettings, Slider bgm, Slider sfx, Toggle fullscreen,
            Button confirm, Button cancel, Text confirmationLabel, Button continueGame, Button newGame,
            Button quit, Button resultTitle, Text status)
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
            shell.ConfigureForRuntime(saveManager, audioService, timeService, saveCoordinator.CaptureSnapshot(),
                Application.isMobilePlatform, Debug.isDebugBuild || Application.isEditor);
            shell.NewGameRequested += HandleNewGameRequested;
            shell.ContinueRequested += HandleContinueRequested;
            shell.DemoSaveRequested += HandleDemoSaveRequested;
            shell.TitleRequested += HandleTitleRequested;
            gameDataCatalog = FindAnyObjectByType<MainGameBootstrap>()?.GameDataCatalog;
            bossManager = FindAnyObjectByType<BossManager>();
            if (bossManager != null) bossManager.BossEnded += HandleBossEnded;
            BindButtons();
            BuildResultView();
            CreateDemoSaveButtons();
            bgmSlider.value = audioService.BgmVolume;
            sfxSlider.value = audioService.SfxVolume;
            fullscreenToggle.isOn = Screen.fullScreen;
            var shouldEnterGameplay = enterGameplayAfterReload;
            enterGameplayAfterReload = false;
            if (shouldEnterGameplay) shell.EnterGameplay(saveCoordinator.CaptureSnapshot());
            else shell.EnterTitle();
            RefreshTitleControls();
            SetStatus("Esc: 일시정지 · Tab: 도감");
            IsInitialized = true;
            Debug.Log("[Nyangbingo] MainGameShellUiController: 일시정지·3슬롯 저장/로드·설정·타이틀 셸 연결 완료.");
        }

        private void Update()
        {
            if (!IsInitialized) return;
            if (Input.GetKeyDown(KeyCode.Escape) &&
                !MainGameCraftingUiController.ConsumedEscapeThisFrame &&
                !MainGameTurretRuntime.ConsumedEscapeThisFrame &&
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
            for (var index = 0; index < SaveManager.SlotCount; index++)
            {
                var slot = index;
                saveButtons[index].onClick.AddListener(() => SaveSlot(slot));
                loadButtons[index].onClick.AddListener(() => LoadSlot(slot));
            }
        }

        private void CreateDemoSaveButtons()
        {
            if (titleNewGameButton == null || titleQuitButton == null) return;
            var parent = titleNewGameButton.transform.parent;
            var templateRect = titleNewGameButton.GetComponent<RectTransform>();
            var quitRect = titleQuitButton.GetComponent<RectTransform>();
            if (parent == null || templateRect == null || quitRect == null) return;

            var rowY = -42f;
            for (var index = 0; index < GameShellController.DemoSaveDays.Length; index++)
            {
                var day = GameShellController.DemoSaveDays[index];
                var button = Instantiate(titleNewGameButton, parent);
                button.name = $"DemoSaveDay{day}";
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => RequestDemoSave(day));
                var rect = button.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2((index - 1) * 82f, rowY);
                rect.sizeDelta = new Vector2(76f, 16f);
                var label = button.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    label.text = $"{day}일차 데모";
                    label.fontSize = 8;
                }
                demoSaveButtons.Add(button);
            }
            quitRect.anchoredPosition = new Vector2(0f, -68f);
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

        private void SaveSlot(int slot)
        {
            var succeeded = saveCoordinator.SaveNow(slot);
            SetStatus(succeeded ? $"슬롯 {slot + 1} 저장 완료" : "저장 실패: 보스 전투 중에는 저장할 수 없습니다.");
            shell.RefreshTitle();
        }

        private void LoadSlot(int slot)
        {
            if (!saveCoordinator.TryLoad(slot))
            {
                SetStatus($"슬롯 {slot + 1} 불러오기 실패");
                return;
            }
            shell.ResumeGameplay();
            SetStatus($"슬롯 {slot + 1} 불러오기 완료");
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

        private void HandleNewGameRequested(int slot)
        {
            if (saveManager.HasSave(slot)) saveManager.Delete(slot);
            enterGameplayAfterReload = true;
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void HandleContinueRequested(int slot, SaveGame _) => saveCoordinator.TryLoad(slot);
        private void HandleDemoSaveRequested(SaveGame demo) =>
            demoLoadApplied = saveCoordinator.TryApplySnapshot(demo);

        private void HandleBossEnded(BossDefinition definition, bool defeated)
        {
            if (definition == null || !GameShellController.ShouldEndDemo(timeService.Day,
                    timeService.MvpContentDayLimit, definition.Id, defeated)) return;
            var snapshot = saveCoordinator.CaptureSnapshot();
            if (snapshot == null)
            {
                Debug.LogError("[Nyangbingo] 30일차 결과 스냅샷 생성에 실패했습니다.");
                return;
            }
            shell.ShowResult(snapshot);
            RefreshResultView();
            Debug.Log("[Nyangbingo] 30일차 강철이 격퇴 후 MVP 결과 화면을 표시했습니다.");
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
            if (bossManager != null) bossManager.BossEnded -= HandleBossEnded;
            Time.timeScale = 1f;
        }
    }
}
