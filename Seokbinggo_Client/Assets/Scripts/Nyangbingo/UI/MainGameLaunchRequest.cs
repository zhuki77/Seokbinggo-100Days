namespace Nyangbingo.UI
{
    /// <summary>
    /// Title.unity에서 MainGame.unity로 씬 전환을 요청할 때 "무엇을 로드할지"를 전달하는 정적 상태.
    /// MainGameBootstrap.RequestFreshWorldForNextScene과 동일한 패턴으로, 씬 리로드 사이에는
    /// DontDestroyOnLoad 대신 정적 필드로 의도만 넘긴다.
    /// </summary>
    public static class MainGameLaunchRequest
    {
        public enum Mode { Continue, NewGame, DemoLoad }

        public static Mode RequestedMode { get; set; } = Mode.Continue;
        public static int SaveSlot { get; set; } = GameShellController.AutoSaveSlot;

        public static void Reset()
        {
            RequestedMode = Mode.Continue;
            SaveSlot = GameShellController.AutoSaveSlot;
        }
    }
}
