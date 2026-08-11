using System;
using Nyangbingo.Save;
using Nyangbingo.World;
using UnityEngine;

namespace Nyangbingo.UI
{
    public sealed class TitleShellState
    {
        public bool CanContinue { get; internal set; }
        public int LatestSlot { get; internal set; } = -1;
        public int DisplayedHeatStage { get; internal set; } = 1;
        public bool ShowsDemoSaves { get; internal set; }
        public bool ShowsQuit { get; internal set; }
    }

    /// <summary>
    /// Title.unity 전용 상태 머신. GameShellController에서 분리되어 나온 클래스로,
    /// MainGame 씬의 월드/저장 로직에는 접근하지 않고 저장 파일 유무만으로 타이틀 화면을 구성한다.
    /// </summary>
    public sealed class TitleShellController : MonoBehaviour
    {
        [SerializeField] private SaveManager saveManager;

        private bool isMobile;
        private bool developmentMenu;
        private int pendingDemoDay;

        public bool IsAwaitingDemoLoadConfirmation { get; private set; }
        public int PendingDemoDay => pendingDemoDay;
        public TitleShellState Title { get; } = new TitleShellState();

        public event Action NewGameRequested;
        public event Action<int, SaveGame> ContinueRequested;
        public event Action<SaveGame> DemoSaveRequested;
        public event Action QuitRequested;

        private void Awake()
        {
            isMobile = Application.isMobilePlatform;
            developmentMenu = Debug.isDebugBuild || Application.isEditor;
            RefreshTitle();
        }

        public void ConfigureForRuntime(SaveManager saves, bool mobile, bool showDevelopmentMenu)
        {
            saveManager = saves;
            isMobile = mobile;
            developmentMenu = showDevelopmentMenu;
            RefreshTitle();
        }

        public void RefreshTitle()
        {
            Title.ShowsDemoSaves = developmentMenu;
            Title.ShowsQuit = !isMobile;
            var slot = -1;
            SaveGame latest = null;
            Title.CanContinue = saveManager != null && saveManager.TryLoadLatest(out slot, out latest);
            Title.LatestSlot = Title.CanContinue ? slot : -1;
            if (Title.CanContinue && latest != null)
            {
                Title.DisplayedHeatStage = latest.heatStage > 0
                    ? Mathf.Clamp(latest.heatStage, 1, HeatStagePresentation.StageCount)
                    : HeatStagePresentation.ResolveForDay(latest.day);
            }
            else
                Title.DisplayedHeatStage = 1;
        }

        public static string FormatTitleCountdown(int daysUntilBaegilHeat) =>
            HeatStagePresentation.FormatBadge(HeatStagePresentation.ResolveForDay(
                DayNightService.DefaultSurvivalDayLimit - Mathf.Max(0, daysUntilBaegilHeat) + 1));

        public static string FormatTitleHeatStage(int heatStage) =>
            HeatStagePresentation.FormatBadge(heatStage);

        public bool TryContinue()
        {
            if (IsAwaitingDemoLoadConfirmation) return false;
            if (saveManager == null || !saveManager.TryLoadLatest(out var slot, out var loaded)) return false;
            ContinueRequested?.Invoke(slot, loaded);
            return true;
        }

        public void RequestNewGame()
        {
            if (IsAwaitingDemoLoadConfirmation) return;
            NewGameRequested?.Invoke();
        }

        public bool RequestDemoSave(int day)
        {
            if (IsAwaitingDemoLoadConfirmation || !developmentMenu ||
                Array.IndexOf(GameShellController.DemoSaveDays, day) < 0)
                return false;
            pendingDemoDay = day;
            IsAwaitingDemoLoadConfirmation = true;
            return true;
        }

        public bool ConfirmDemoLoad()
        {
            if (!IsAwaitingDemoLoadConfirmation) return false;
            var day = pendingDemoDay;
            IsAwaitingDemoLoadConfirmation = false;
            pendingDemoDay = 0;
            if (saveManager == null || !saveManager.TryCopyDemoToAutoSave(day, out var demo))
                return false;
            DemoSaveRequested?.Invoke(demo);
            return true;
        }

        public void CancelDemoLoad()
        {
            IsAwaitingDemoLoadConfirmation = false;
            pendingDemoDay = 0;
        }

        public bool RequestQuit()
        {
            if (isMobile) return false;
            QuitRequested?.Invoke();
#if !UNITY_EDITOR
            Application.Quit();
#endif
            return true;
        }
    }
}
