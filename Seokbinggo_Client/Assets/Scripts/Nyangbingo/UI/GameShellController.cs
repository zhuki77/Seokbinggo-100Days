using System;
using System.Collections.Generic;
using Nyangbingo.Audio;
using Nyangbingo.Save;
using UnityEngine;

namespace Nyangbingo.UI
{
    public enum GameShellScreen { Gameplay, Pause, Settings, Result, Confirmation }
    public enum GameShellConfirmation { None, ReturnToTitle }

    public sealed class DemoResultState
    {
        public const string Teaser = "D-70 — 백일폭염까지";
        public float SealPercentage { get; internal set; }
        public IReadOnlyList<string> CompletedModuleIds { get; internal set; }
        public bool ImugiDefeated { get; internal set; }
        public int YokaiKills { get; internal set; }
        public int MinedTiles { get; internal set; }
        public int Deaths { get; internal set; }
    }

    /// <summary>
    /// MainGame.unity 전용 상태 머신. Title 화면은 Title.unity의 TitleShellController가 담당하며,
    /// 이 컨트롤러는 Gameplay/Pause/Settings/Result/Confirmation(타이틀로 복귀)만 관리한다.
    /// </summary>
    public sealed class GameShellController : MonoBehaviour
    {
        public const int AutoSaveSlot = 0;
        public const int DemoEndDay = 30;
        public static readonly int[] DemoSaveDays = { 1, 15, 30 };

        [SerializeField] private NyangbingoAudioService audioService;
        [SerializeField] private GameObject pauseCanvas;
        [SerializeField] private GameObject resultCanvas;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject confirmationPanel;

        private SaveGame activeSave;
        private float resumeTimeScale = 1f;
        private bool isMobile;

        public GameShellScreen Screen { get; private set; } = GameShellScreen.Gameplay;
        public GameShellConfirmation PendingConfirmation { get; private set; }
        public int ActiveSaveSlot { get; private set; } = AutoSaveSlot;
        public DemoResultState Result { get; private set; }
        public bool CanShowFullscreenToggle => !isMobile;
        public bool IsOfficialDemo => activeSave != null && activeSave.isOfficialDemo;

        public event Action TitleRequested;

        private void Awake()
        {
            isMobile = Application.isMobilePlatform;
            ApplyViewState();
        }

        public void ConfigureForRuntime(NyangbingoAudioService audio, SaveGame currentSave, bool mobile)
        {
            audioService = audio;
            activeSave = currentSave;
            isMobile = mobile;
            ApplyViewState();
        }

        public void ConfigureViews(GameObject pause, GameObject result, GameObject settings, GameObject confirmation)
        {
            pauseCanvas = pause;
            resultCanvas = result;
            settingsPanel = settings;
            confirmationPanel = confirmation;
            ApplyViewState();
        }

        public void EnterGameplay(SaveGame currentSave)
        {
            activeSave = currentSave ?? activeSave ?? new SaveGame { day = 1 };
            ActiveSaveSlot = MainGameLaunchRequest.SaveSlot;
            ShowGameplay(false);
        }

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
            ShowGameplay(true);
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
            if (PendingConfirmation != GameShellConfirmation.ReturnToTitle) return false;
            ClearConfirmation();
            TitleRequested?.Invoke();
            return true;
        }

        public bool CancelConfirmation()
        {
            if (Screen != GameShellScreen.Confirmation) return false;
            ClearConfirmation();
            SetScreen(GameShellScreen.Pause);
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
            var imugiDefeated = false;
            for (var i = 0; i < save.bossRecords.Count; i++)
                if (save.bossRecords[i].bossId == "imugi_boss" && save.bossRecords[i].count > 0)
                    imugiDefeated = true;

            return new DemoResultState
            {
                SealPercentage = Mathf.Clamp(save.sealPct, 0f, 100f),
                CompletedModuleIds = modules,
                ImugiDefeated = imugiDefeated,
                YokaiKills = Math.Max(0, kills),
                MinedTiles = save.stats.minedTiles,
                Deaths = save.stats.deaths
            };
        }

        /// <summary>
        /// v34 정본: 데모는 30일차 이무기의 격퇴 여부가 아니라 30일차 밤이 끝난 새벽에 종료한다.
        /// DayNightService.Dawn은 날짜를 먼저 증가시킨 뒤 발행되므로 새 날짜가 MVP 제한일+1인지 검사한다.
        /// </summary>
        public static bool ShouldEndDemoAtDawn(bool isOfficialDemo, int newDay, int mvpDayLimit) =>
            isOfficialDemo && mvpDayLimit > 0 && newDay == mvpDayLimit + 1;

        private void ShowGameplay(bool preserveCurrentMusic)
        {
            Time.timeScale = resumeTimeScale > 0f ? resumeTimeScale : 1f;
            SetScreen(GameShellScreen.Gameplay);
            if (preserveCurrentMusic)
                audioService?.EnsureAudiblePlayback();
            else
                audioService?.EnsureAudiblePlayback(MusicTrack.Day);
        }

        private void OpenConfirmation(GameShellConfirmation confirmation)
        {
            PendingConfirmation = confirmation;
            SetScreen(GameShellScreen.Confirmation);
        }

        private void ClearConfirmation()
        {
            PendingConfirmation = GameShellConfirmation.None;
        }

        private void SetScreen(GameShellScreen screen)
        {
            Screen = screen;
            ApplyViewState();
        }

        private void ApplyViewState()
        {
            if (pauseCanvas != null) pauseCanvas.SetActive(Screen == GameShellScreen.Pause ||
                                                          Screen == GameShellScreen.Settings ||
                                                          Screen == GameShellScreen.Confirmation);
            if (resultCanvas != null) resultCanvas.SetActive(Screen == GameShellScreen.Result);
            if (settingsPanel != null) settingsPanel.SetActive(Screen == GameShellScreen.Settings);
            if (confirmationPanel != null) confirmationPanel.SetActive(Screen == GameShellScreen.Confirmation);
        }
    }
}
