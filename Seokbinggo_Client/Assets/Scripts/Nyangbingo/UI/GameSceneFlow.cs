using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nyangbingo.UI
{
    /// <summary>
    /// Title ↔ MainGame 씬 전환 Intent. 정적 브리지로 씬 경계를 넘긴다.
    /// </summary>
    public static class GameSceneFlow
    {
        public const string TitleSceneName = "Title";
        public const string MainGameSceneName = "MainGame";

        public enum IntentKind
        {
            None,
            NewGame,
            Continue,
            DemoDay
        }

        public static IntentKind PendingKind { get; private set; }
        public static int PendingSlot { get; private set; } = -1;
        public static int PendingDemoDay { get; private set; }
        public static bool RevealLoadingAfterLoad { get; private set; }

        public static void RequestNewGame(bool revealLoading = true)
        {
            PendingKind = IntentKind.NewGame;
            PendingSlot = GameShellController.AutoSaveSlot;
            PendingDemoDay = 0;
            RevealLoadingAfterLoad = revealLoading;
        }

        public static void RequestContinue(int slot, bool revealLoading = true)
        {
            PendingKind = IntentKind.Continue;
            PendingSlot = slot;
            PendingDemoDay = 0;
            RevealLoadingAfterLoad = revealLoading;
        }

        public static void RequestDemoDay(int day, bool revealLoading = true)
        {
            PendingKind = IntentKind.DemoDay;
            PendingSlot = GameShellController.AutoSaveSlot;
            PendingDemoDay = day;
            RevealLoadingAfterLoad = revealLoading;
        }

        public static IntentKind ConsumePending(out int slot, out int demoDay, out bool revealLoading)
        {
            var kind = PendingKind;
            slot = PendingSlot;
            demoDay = PendingDemoDay;
            revealLoading = RevealLoadingAfterLoad;
            PendingKind = IntentKind.None;
            PendingSlot = -1;
            PendingDemoDay = 0;
            RevealLoadingAfterLoad = false;
            return kind;
        }

        public static void GoToTitle(bool revealLoading = true)
        {
            PendingKind = IntentKind.None;
            PendingSlot = -1;
            PendingDemoDay = 0;
            RevealLoadingAfterLoad = revealLoading;
            Time.timeScale = 1f;
            SceneManager.LoadScene(TitleSceneName);
        }

        public static void GoToMainGame()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(MainGameSceneName);
        }
    }
}
