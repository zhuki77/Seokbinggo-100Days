using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nyangbingo.UI
{
    /// <summary>
    /// Title ↔ MainGame 전환 요청. BeginDirect는 EventSystem.Update/Start 도중
    /// 동기 LoadScene을 쓰지 않고, DontDestroyOnLoad 러너가 다음 프레임에 Single 로드한다.
    /// (UI 클릭 중 동기 LoadScene은 EventSystem을 파괴해 MainGame이 검게 멈출 수 있음)
    /// </summary>
    public static class SceneTransitionRequest
    {
        public const string LoadingSceneName = "Loading";
        public const string TitleSceneName = "Title";
        public const string MainGameSceneName = "MainGame";
        public const int TitleBuildIndex = 0;

        public static string TargetSceneName { get; private set; }
        public static bool IsTransitionActive { get; private set; }

        public static void Begin(string targetSceneName)
        {
            TargetSceneName = targetSceneName;
            IsTransitionActive = true;
            if (IsLoadingSceneLoaded())
            {
                BeginDirect(targetSceneName);
                return;
            }
            SceneManager.LoadScene(LoadingSceneName, LoadSceneMode.Additive);
        }

        public static void BeginDirect(string targetSceneName)
        {
            if (string.IsNullOrWhiteSpace(targetSceneName))
            {
                Debug.LogError("[Nyangbingo] SceneTransitionRequest.BeginDirect: 대상 씬 이름이 비어 있습니다.");
                return;
            }

            TargetSceneName = null;
            IsTransitionActive = false;
            LoadingOverlayRequest.Reset();
            Time.timeScale = 1f;
            Debug.Log($"[Nyangbingo] SceneTransitionRequest: BeginDirect 예약 → {targetSceneName}");
            SceneTransitionRunner.EnqueueSingleLoad(targetSceneName);
        }

        public static void BeginDirectTitle() => BeginDirect(TitleSceneName);

        public static void ClearTarget() => TargetSceneName = null;

        public static void Complete() => IsTransitionActive = false;

        public static bool IsLoadingSceneLoaded()
        {
            var loading = SceneManager.GetSceneByName(LoadingSceneName);
            return loading.IsValid() && loading.isLoaded;
        }

        internal static int ResolveBuildIndex(string sceneName)
        {
            for (var i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                var path = SceneUtility.GetScenePathByBuildIndex(i);
                if (string.IsNullOrEmpty(path)) continue;
                var name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (string.Equals(name, sceneName, System.StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }
    }

    /// <summary>
    /// UI/Start 문맥 밖에서 Single 씬 로드를 수행하는 1회용 러너.
    /// WaitForEndOfFrame은 쓰지 않는다(렌더 없으면 영구 대기).
    /// </summary>
    internal sealed class SceneTransitionRunner : MonoBehaviour
    {
        private static SceneTransitionRunner instance;
        private string pendingSceneName;

        public static void EnqueueSingleLoad(string sceneName)
        {
            if (instance == null)
            {
                var host = new GameObject(nameof(SceneTransitionRunner));
                DontDestroyOnLoad(host);
                instance = host.AddComponent<SceneTransitionRunner>();
            }

            instance.pendingSceneName = sceneName;
            instance.StopAllCoroutines();
            instance.StartCoroutine(instance.LoadNextFrame());
        }

        /// <summary>로드가 반영되지 않을 때 무한 대기하지 않도록 두는 상한(프레임).</summary>
        private const int LoadTimeoutFrames = 300;

        private IEnumerator LoadNextFrame()
        {
            // EventSystem.Update / Start 스택이 완전히 끝난 뒤 로드.
            yield return null;

            var sceneName = pendingSceneName;
            pendingSceneName = null;
            if (string.IsNullOrEmpty(sceneName))
            {
                Cleanup();
                yield break;
            }

            Time.timeScale = 1f;
            var buildIndex = SceneTransitionRequest.ResolveBuildIndex(sceneName);
            if (buildIndex < 0)
            {
                Debug.LogError(
                    $"[Nyangbingo] SceneTransitionRunner: '{sceneName}' 씬이 Build Settings에 없어 전환에 " +
                    "실패했습니다. File > Build Settings에 Title/Loading/MainGame이 등록돼 있는지 확인하세요.");
                Cleanup();
                yield break;
            }

            Debug.Log(
                $"[Nyangbingo] SceneTransitionRunner: LoadScene Single buildIndex={buildIndex} ({sceneName})");
            SceneManager.LoadScene(buildIndex, LoadSceneMode.Single);

            // 로드가 실제로 반영될 때까지 러너를 살려 둔다. 여기서 곧바로 자신을 파괴하면
            // 코루틴이 끊겨 전환이 조용히 사라질 수 있다.
            for (var frame = 0; frame < LoadTimeoutFrames; frame++)
            {
                var active = SceneManager.GetActiveScene();
                if (active.buildIndex == buildIndex && active.isLoaded)
                {
                    Cleanup();
                    yield break;
                }
                yield return null;
            }

            Debug.LogError(
                $"[Nyangbingo] SceneTransitionRunner: '{sceneName}'(buildIndex={buildIndex}) 로드가 " +
                $"{LoadTimeoutFrames}프레임 안에 반영되지 않았습니다. 현재 활성 씬=" +
                $"'{SceneManager.GetActiveScene().name}'. Time.timeScale 또는 잔류 Additive 씬을 확인하세요.");
            Cleanup();
        }

        private void Cleanup()
        {
            if (instance == this) instance = null;
            Destroy(gameObject);
        }
    }
}
