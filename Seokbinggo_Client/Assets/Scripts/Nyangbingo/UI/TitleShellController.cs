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

        public TitleShellState Title { get; } = new TitleShellState();

        public event Action NewGameRequested;
        public event Action<int, SaveGame> ContinueRequested;
        public event Action QuitRequested;

        private void Awake()
        {
            isMobile = Application.isMobilePlatform;
            RefreshTitle();
        }

        public void ConfigureForRuntime(SaveManager saves, bool mobile)
        {
            saveManager = saves;
            isMobile = mobile;
            RefreshTitle();
        }

        public void RefreshTitle()
        {
            Title.ShowsQuit = !isMobile;
            var slot = -1;
            SaveGame latest = null;
            Title.CanContinue = saveManager != null && saveManager.TryLoadLatest(out slot, out latest);
            Title.LatestSlot = Title.CanContinue ? slot : -1;
            if (Title.CanContinue && latest != null)
            {
                Title.DisplayedHeatStage = Mathf.Clamp(
                    latest.heatStage, 1, HeatStagePresentation.StageCount);
            }
            else
                Title.DisplayedHeatStage = 1;
        }

        public static string FormatTitleHeatStage(int heatStage) =>
            $"폭염 {HeatStagePresentation.FormatBadge(heatStage)}단계";

        public bool TryContinue()
        {
            if (saveManager == null || !saveManager.TryLoadLatest(out var slot, out var loaded)) return false;
            ContinueRequested?.Invoke(slot, loaded);
            return true;
        }

        public void RequestNewGame()
        {
            NewGameRequested?.Invoke();
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
