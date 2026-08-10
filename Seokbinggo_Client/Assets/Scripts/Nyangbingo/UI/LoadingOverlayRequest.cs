using UnityEngine.SceneManagement;

namespace Nyangbingo.UI
{
    /// <summary>
    /// 이미 활성화된 씬 위에 Loading.unity를 Additive로 얹어 그 씬 자신의 초기화 구간을 가리기 위한
    /// 정적 요청. SceneTransitionRequest와 달리 대상 씬 전환은 하지 않으며, 현재 씬이 준비를 마치고
    /// MarkReady를 호출하면 LoadingSceneController가 Loading.unity만 언로드한다.
    /// </summary>
    public static class LoadingOverlayRequest
    {
        public static bool IsReady { get; private set; }

        public static void Begin()
        {
            IsReady = false;
            SceneManager.LoadScene(SceneTransitionRequest.LoadingSceneName, LoadSceneMode.Additive);
        }

        public static void MarkReady() => IsReady = true;
    }
}
