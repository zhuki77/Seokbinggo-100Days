using System.Collections;
using Nyangbingo.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Nyangbingo.UI
{
    /// <summary>
    /// Loading.unity 전용. SceneTransitionRequest.TargetSceneName을 LoadSceneMode.Single로 비동기 로드하며
    /// 기존 셸의 픽셀아트 로고 티어 애니메이션(5x4, 17프레임)을 로딩이 끝날 때까지 반복 재생한다.
    /// Single 모드 씬이 활성화되는 순간 이전 씬과 이 Loading 씬이 함께 자동 언로드되므로
    /// 별도 정리 코드가 필요 없다.
    /// </summary>
    public sealed class LoadingSceneController : MonoBehaviour
    {
        /// <summary>Title/MainGame의 오버레이 Canvas보다 항상 위에 그려지도록 강제하는 정렬 순서.</summary>
        public const int CanvasSortingOrder = 32700;

        public const int FrameCount = 17;
        private const int Columns = 5;
        private const int Rows = 4;
        private static readonly float[] FrameDurations =
        {
            .05f, .05f, .05f, .5f, .05f, .05f, .05f, .5f,
            .1f, .1f, .1f, .1f, .1f, .1f, .1f, .1f, .1f
        };

        [SerializeField] private Image progressFillImage;
        [SerializeField] private Text statusText;
        [SerializeField] private Image loadingArtImage;
        [SerializeField] private GameplayArtCatalog gameplayArtCatalog;

        private Sprite[] frames = System.Array.Empty<Sprite>();

        private void Start()
        {
            var target = SceneTransitionRequest.TargetSceneName;
            if (string.IsNullOrEmpty(target))
            {
                Debug.LogError("[Nyangbingo] LoadingSceneController: 전환 대상 씬이 지정되지 않았습니다.");
                return;
            }
            if (statusText != null) statusText.text = "불러오는 중...";
            BuildFrames();
            if (frames.Length == FrameCount) StartCoroutine(PlayFrameLoop());
            StartCoroutine(LoadTarget(target));
        }

        private void BuildFrames()
        {
            var sheet = gameplayArtCatalog != null ? gameplayArtCatalog.ShellLoadingSheet : null;
            if (sheet == null || loadingArtImage == null) return;
            var texture = sheet.texture;
            if (texture == null || texture.width % Columns != 0 || texture.height % Rows != 0)
            {
                Debug.LogError("[Nyangbingo] Shell loading sheet must use a 5x4 frame grid.");
                return;
            }

            var frameWidth = texture.width / Columns;
            var frameHeight = texture.height / Rows;
            frames = new Sprite[FrameCount];
            for (var index = 0; index < frames.Length; index++)
            {
                var column = index % Columns;
                var rowFromTop = index / Columns;
                var rect = new Rect(column * frameWidth,
                    texture.height - (rowFromTop + 1) * frameHeight, frameWidth, frameHeight);
                frames[index] = Sprite.Create(texture, rect, new Vector2(.5f, .5f),
                    100f, 0, SpriteMeshType.FullRect, Vector4.zero, false);
                frames[index].name = $"LoadingFrame_{index:00}";
            }
            loadingArtImage.sprite = frames[0];
        }

        private IEnumerator PlayFrameLoop()
        {
            while (true)
            {
                for (var index = 0; index < frames.Length; index++)
                {
                    loadingArtImage.sprite = frames[index];
                    yield return new WaitForSecondsRealtime(FrameDurations[index]);
                }
            }
        }

        private IEnumerator LoadTarget(string target)
        {
            var operation = SceneManager.LoadSceneAsync(target, LoadSceneMode.Single);
            operation.allowSceneActivation = false;
            while (operation.progress < 0.9f)
            {
                if (progressFillImage != null) progressFillImage.fillAmount = operation.progress / 0.9f;
                yield return null;
            }
            if (progressFillImage != null) progressFillImage.fillAmount = 1f;
            yield return null;
            operation.allowSceneActivation = true;
        }
    }
}
