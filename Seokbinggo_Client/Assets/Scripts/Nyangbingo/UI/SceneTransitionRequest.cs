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
    }
}
