using Nyangbingo.Audio;
using Nyangbingo.Save;
using Nyangbingo.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
            BindButtons();
            bgmSlider.value = audioService.BgmVolume;
            sfxSlider.value = audioService.SfxVolume;
            fullscreenToggle.isOn = Screen.fullScreen;
            shell.EnterGameplay(saveCoordinator.CaptureSnapshot());
            SetStatus("Esc: 일시정지 · Tab: 도감");
            IsInitialized = true;
            Debug.Log("[Nyangbingo] MainGameShellUiController: 일시정지·3슬롯 저장/로드·설정·타이틀 셸 연결 완료.");
        }

        private void Update()
        {
            if (!IsInitialized) return;
            if (Input.GetKeyDown(KeyCode.Escape) && (codex == null || !codex.IsOpen))
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
                confirmationText.text = shell.PendingConfirmation == GameShellConfirmation.ReturnToTitle
                    ? "타이틀로 돌아갈까요? 저장하지 않은 진행은 사라집니다."
                    : "기존 자동 저장을 지우고 새 게임을 시작할까요?";
        }

        private void BindButtons()
        {
            resumeButton.onClick.AddListener(() => shell.ResumeGameplay());
            settingsButton.onClick.AddListener(OpenSettings);
            returnTitleButton.onClick.AddListener(() => shell.RequestReturnToTitle());
            settingsApplyButton.onClick.AddListener(ApplySettings);
            settingsBackButton.onClick.AddListener(() => shell.CloseSettings());
            confirmButton.onClick.AddListener(() => shell.Confirm());
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
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void HandleContinueRequested(int slot, SaveGame _) => saveCoordinator.TryLoad(slot);
        private void HandleDemoSaveRequested(SaveGame _) => saveCoordinator.TryLoad(GameShellController.AutoSaveSlot);
        private void HandleTitleRequested() => Time.timeScale = 0f;
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
            Time.timeScale = 1f;
        }
    }
}
