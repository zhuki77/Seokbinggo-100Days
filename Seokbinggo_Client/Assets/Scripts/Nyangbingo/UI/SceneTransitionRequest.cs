using UnityEngine.SceneManagement;

namespace Nyangbingo.UI
{
    /// <summary>
    /// Title.unity/MainGame.unity 사이의 전환을 Loading.unity를 경유해 수행하기 위한 정적 요청.
    /// MainGameBootstrap.RequestFreshWorldForNextScene과 동일하게 DontDestroyOnLoad 없이
    /// 정적 필드로 "다음에 무엇을 로드할지"만 넘긴다.
    /// </summary>
    public static class SceneTransitionRequest
    {
        public const string LoadingSceneName = "Loading";

        public static string TargetSceneName { get; private set; }

        public static void Begin(string targetSceneName)
        {
            TargetSceneName = targetSceneName;
            SceneManager.LoadScene(LoadingSceneName, LoadSceneMode.Additive);
        }

        /// <summary>
        /// Loading.unity를 거치지 않고 대상 씬으로 곧바로 전환한다. 대상 씬이 자체적으로
        /// LoadingOverlayRequest를 통해 자신의 초기화 구간을 가리는 경우(예: Title -> MainGame)에 쓰인다.
        /// </summary>
        public static void BeginDirect(string targetSceneName)
        {
            TargetSceneName = null;
            SceneManager.LoadScene(targetSceneName, LoadSceneMode.Single);
        }

        /// <summary>LoadingSceneController가 대상 씬 값을 소비한 뒤, 이후의 Loading 오버레이 로드와
        /// 혼동되지 않도록 초기화한다.</summary>
        public static void ClearTarget() => TargetSceneName = null;
    }
}
