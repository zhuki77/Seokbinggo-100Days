using System;
using System.Collections.Generic;
using Nyangbingo.Audio;
using Nyangbingo.Core;
using Nyangbingo.Save;
using UnityEngine;

namespace Nyangbingo.UI
{
    public enum GameShellScreen { Title, Gameplay, Pause, Settings, Result, Confirmation }
    public enum GameShellConfirmation { None, ReturnToTitle, LoadDemoSave }

    public sealed class TitleShellState
    {
        public bool CanContinue { get; internal set; }
        public int LatestSlot { get; internal set; } = -1;
        public int DaysUntilBaegilHeat { get; internal set; } = 100;
        public bool ShowsDemoSaves { get; internal set; }
        public bool ShowsQuit { get; internal set; }
    }

    public sealed class DemoResultState
    {
        public const string Teaser = "D-70 — 백일폭염까지";
        public float SealPercentage { get; internal set; }
        public IReadOnlyList<string> CompletedModuleIds { get; internal set; }
        public bool GangcheolDefeated { get; internal set; }
        public int YokaiKills { get; internal set; }
        public int MinedTiles { get; internal set; }
        public int Deaths { get; internal set; }
    }

    public sealed class GameShellController : MonoBehaviour
    {
        public const int AutoSaveSlot = 0;
        public const int DemoEndDay = 30;
        public static readonly int[] DemoSaveDays = { 1, 15, 30 };

        [SerializeField] private SaveManager saveManager;
        [SerializeField] private NyangbingoAudioService audioService;
        [SerializeField] private MonoBehaviour timeSourceComponent;
        [SerializeField] private GameObject titleCanvas;
        [SerializeField] private GameObject pauseCanvas;
        [SerializeField] private GameObject resultCanvas;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject confirmationPanel;

        private ITimeSource timeSource;
        private SaveGame activeSave;
        private float resumeTimeScale = 1f;
        private bool isMobile;
        private bool developmentMenu;
        private int pendingDemoDay;

        public GameShellScreen Screen { get; private set; } = GameShellScreen.Title;
        public GameShellConfirmation PendingConfirmation { get; private set; }
        public int PendingDemoDay => pendingDemoDay;
        public int ActiveSaveSlot { get; private set; } = AutoSaveSlot;
        public TitleShellState Title { get; } = new TitleShellState();
        public DemoResultState Result { get; private set; }
        public bool CanShowFullscreenToggle => !isMobile;

        public event Action<int> NewGameRequested;
        public event Action<int, SaveGame> ContinueRequested;
        public event Action<SaveGame> DemoSaveRequested;
        public event Action TitleRequested;
        public event Action QuitRequested;

        private void Awake()
        {
            timeSource = timeSourceComponent as ITimeSource;
            isMobile = Application.isMobilePlatform;
            developmentMenu = Debug.isDebugBuild || Application.isEditor;
            RefreshTitle();
            ApplyViewState();
        }

        public void ConfigureForRuntime(SaveManager saves, NyangbingoAudioService audio, ITimeSource time,
            SaveGame currentSave, bool mobile, bool showDevelopmentMenu)
        {
            saveManager = saves;
            audioService = audio;
            timeSource = time;
            activeSave = currentSave;
            isMobile = mobile;
            developmentMenu = showDevelopmentMenu;
            RefreshTitle();
            ApplyViewState();
        }

        public void ConfigureViews(GameObject title, GameObject pause, GameObject result, GameObject settings,
            GameObject confirmation)
        {
            titleCanvas = title;
            pauseCanvas = pause;
            resultCanvas = result;
            settingsPanel = settings;
            confirmationPanel = confirmation;
            ApplyViewState();
        }

        public void EnterGameplay(SaveGame currentSave)
        {
            activeSave = currentSave ?? activeSave ?? new SaveGame { day = 1 };
            ShowGameplay();
        }

        public void EnterTitle() => ShowTitle();

        public static float ResolveTimeScaleAfterLoading(GameShellScreen screen) =>
            screen == GameShellScreen.Gameplay ? 1f : 0f;

        public void RestoreTimeScaleAfterLoading()
        {
            if (Screen == GameShellScreen.Gameplay)
            {
                resumeTimeScale = 1f;
            }

            Time.timeScale = ResolveTimeScaleAfterLoading(Screen);
        }

        public void RefreshTitle()
        {
            Title.ShowsDemoSaves = developmentMenu;
            Title.ShowsQuit = !isMobile;
            var slot = -1;
            SaveGame latest = null;
            Title.CanContinue = saveManager != null && saveManager.TryLoadLatest(out slot, out latest);
            Title.LatestSlot = Title.CanContinue ? slot : -1;
            Title.DaysUntilBaegilHeat = Title.CanContinue ? Mathf.Max(0, 101 - latest.day) : 100;
        }

        public static string FormatTitleCountdown(int daysUntilBaegilHeat) =>
            $"D-{Mathf.Max(0, daysUntilBaegilHeat)}";

        public bool TryContinue()
        {
            if (saveManager == null || !saveManager.TryLoadLatest(out var slot, out var loaded)) return false;
            ActiveSaveSlot = slot;
            activeSave = loaded;
            ContinueRequested?.Invoke(slot, loaded);
            return true;
        }

        public void RequestNewGame()
        {
            if (Screen != GameShellScreen.Title) return;
            BeginNewGame();
        }

        public bool RequestDemoSave(int day)
        {
            if (Screen != GameShellScreen.Title || !developmentMenu || Array.IndexOf(DemoSaveDays, day) < 0)
                return false;
            pendingDemoDay = day;
            OpenConfirmation(GameShellConfirmation.LoadDemoSave);
            return true;
        }

        public bool OpenPause()
        {
            if (Screen != GameShellScreen.Gameplay) return false;
            resumeTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            SetScreen(GameShellScreen.Pause);
            return true;
        }

        public bool ResumeGameplay()
        {
            if (Screen != GameShellScreen.Pause) return false;
            ShowGameplay();
            return true;
        }

        public bool OpenSettings()
        {
            if (Screen != GameShellScreen.Pause) return false;
            SetScreen(GameShellScreen.Settings);
            return true;
        }

        public bool CloseSettings()
        {
            if (Screen != GameShellScreen.Settings) return false;
            SetScreen(GameShellScreen.Pause);
            return true;
        }

        public bool TryApplySettings(float bgmVolume, float sfxVolume, bool fullscreen)
        {
            if (Screen != GameShellScreen.Settings || audioService == null ||
                !audioService.TrySetBusVolumes(bgmVolume, sfxVolume)) return false;
            if (!isMobile) UnityEngine.Screen.fullScreen = fullscreen;
            return true;
        }

        public bool RequestReturnToTitle()
        {
            if (Screen != GameShellScreen.Pause && Screen != GameShellScreen.Settings) return false;
            OpenConfirmation(GameShellConfirmation.ReturnToTitle);
            return true;
        }

        public bool Confirm()
        {
            switch (PendingConfirmation)
            {
                case GameShellConfirmation.ReturnToTitle:
                    ClearConfirmation();
                    ShowTitle();
                    TitleRequested?.Invoke();
                    return true;
                case GameShellConfirmation.LoadDemoSave:
                    var day = pendingDemoDay;
                    ClearConfirmation();
                    if (saveManager == null || !saveManager.TryCopyDemoToAutoSave(day, out var demo))
                    {
                        ShowTitle();
                        return false;
                    }
                    ActiveSaveSlot = AutoSaveSlot;
                    activeSave = demo;
                    DemoSaveRequested?.Invoke(demo);
                    return true;
                default: return false;
            }
        }

        public bool CancelConfirmation()
        {
            if (Screen != GameShellScreen.Confirmation) return false;
            ClearConfirmation();
            SetScreen(resumeTimeScale <= 0f ? GameShellScreen.Title : GameShellScreen.Pause);
            return true;
        }

        public bool RequestQuit()
        {
            if (Screen != GameShellScreen.Title || isMobile) return false;
            QuitRequested?.Invoke();
#if !UNITY_EDITOR
            Application.Quit();
#endif
            return true;
        }

        public void ShowResult(SaveGame save)
        {
            activeSave = save ?? activeSave;
            Result = BuildResult(activeSave);
            Time.timeScale = 0f;
            SetScreen(GameShellScreen.Result);
        }

        public bool ReturnFromResultToTitle()
        {
            if (Screen != GameShellScreen.Result) return false;
            ShowTitle();
            TitleRequested?.Invoke();
            return true;
        }

        public static DemoResultState BuildResult(SaveGame save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            save.NormalizeAfterLoad();
            var modules = new List<string>();
            var uniqueModules = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < save.modulesDone.Count; i++)
                if (!string.IsNullOrWhiteSpace(save.modulesDone[i]) && uniqueModules.Add(save.modulesDone[i]))
                    modules.Add(save.modulesDone[i]);

            var kills = 0;
            for (var i = 0; i < save.dogam.Count; i++)
                kills = save.dogam[i].kills > int.MaxValue - kills ? int.MaxValue : kills + save.dogam[i].kills;
            var gangcheolDefeated = false;
            for (var i = 0; i < save.bossRecords.Count; i++)
                if (save.bossRecords[i].bossId == "gangcheol_boss" && save.bossRecords[i].count > 0)
                    gangcheolDefeated = true;

            return new DemoResultState
            {
                SealPercentage = Mathf.Clamp(save.sealPct, 0f, 100f),
                CompletedModuleIds = modules,
                GangcheolDefeated = gangcheolDefeated,
                YokaiKills = Math.Max(0, kills),
                MinedTiles = save.stats.minedTiles,
                Deaths = save.stats.deaths
            };
        }

        /// <summary>
        /// v14.1/v27 정본: 데모는 30일차 강철이의 격퇴 여부가 아니라 30일차 밤이 끝난 새벽에 종료한다.
        /// DayNightService.Dawn은 날짜를 먼저 증가시킨 뒤 발행되므로 새 날짜가 MVP 제한일+1인지 검사한다.
        /// </summary>
        public static bool ShouldEndDemoAtDawn(int newDay, int mvpDayLimit) =>
            mvpDayLimit > 0 && newDay == mvpDayLimit + 1;

        private void BeginNewGame()
        {
            ActiveSaveSlot = AutoSaveSlot;
            activeSave = new SaveGame { day = 1 };
            NewGameRequested?.Invoke(AutoSaveSlot);
        }

        private void ShowGameplay()
        {
            Time.timeScale = resumeTimeScale > 0f ? resumeTimeScale : 1f;
            SetScreen(GameShellScreen.Gameplay);
        }

        private void ShowTitle()
        {
            Time.timeScale = 0f;
            SetScreen(GameShellScreen.Title);
            RefreshTitle();
        }

        private void OpenConfirmation(GameShellConfirmation confirmation)
        {
            PendingConfirmation = confirmation;
            resumeTimeScale = Screen == GameShellScreen.Title ? 0f : resumeTimeScale;
            SetScreen(GameShellScreen.Confirmation);
        }

        private void ClearConfirmation()
        {
            PendingConfirmation = GameShellConfirmation.None;
            pendingDemoDay = 0;
        }

        private void SetScreen(GameShellScreen screen)
        {
            Screen = screen;
            ApplyViewState();
        }

        private void ApplyViewState()
        {
            if (titleCanvas != null) titleCanvas.SetActive(Screen == GameShellScreen.Title);
            if (pauseCanvas != null) pauseCanvas.SetActive(Screen == GameShellScreen.Pause ||
                                                          Screen == GameShellScreen.Settings ||
                                                          Screen == GameShellScreen.Confirmation);
            if (resultCanvas != null) resultCanvas.SetActive(Screen == GameShellScreen.Result);
            if (settingsPanel != null) settingsPanel.SetActive(Screen == GameShellScreen.Settings);
            if (confirmationPanel != null) confirmationPanel.SetActive(Screen == GameShellScreen.Confirmation);
        }
    }
}
