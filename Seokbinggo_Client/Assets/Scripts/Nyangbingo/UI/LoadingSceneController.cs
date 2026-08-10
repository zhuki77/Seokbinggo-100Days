using System;
using System.Collections;
using System.Collections.Generic;
using Nyangbingo.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Nyangbingo.UI
{
    /// <summary>
    /// Loading.unity 전용. 기존 셸의 픽셀아트 로고 티어 애니메이션(5x4, 17프레임)을 반복 재생하며 두 가지
    /// 모드로 동작한다.
    /// - 전환 모드: SceneTransitionRequest.TargetSceneName이 지정된 경우, 그 씬을 LoadSceneMode.Single로
    ///   비동기 로드한다. Single 씬이 활성화되는 순간 이전 씬과 이 Loading 씬이 함께 자동 언로드된다.
    /// - 오버레이 모드: 대상 씬이 없으면 LoadingOverlayRequest.IsReady를 기다렸다가 이 씬만 언로드한다.
    ///   이미 활성화된 씬(예: MainGame)이 자신의 초기화 구간을 가리는 용도로 사용한다.
    /// </summary>
    public sealed class LoadingSceneController : MonoBehaviour
    {
        /// <summary>Title/MainGame의 오버레이 Canvas보다 항상 위에 그려지도록 강제하는 정렬 순서.</summary>
        public const int CanvasSortingOrder = 32700;

        public const int FrameCount = 17;
        private static readonly float[] FrameDurations =
        {
            .05f, .05f, .05f, .5f, .05f, .05f, .05f, .5f,
            .1f, .1f, .1f, .1f, .1f, .1f, .1f, .1f, .1f
        };

        private static readonly int IsEndLoading = Animator.StringToHash("IsEndLoading");

        [SerializeField] private Image loadingArtImage;
        [SerializeField] private GameplayArtCatalog gameplayArtCatalog;
        [SerializeField] private Animator loadingAnimator;

        private IReadOnlyList<Sprite> frames = System.Array.Empty<Sprite>();

        private void Start()
        {
            var target = SceneTransitionRequest.TargetSceneName;
            SceneTransitionRequest.ClearTarget();

            BindFrames();
            if (frames.Count == FrameCount) StartCoroutine(PlayFrameLoop());
            StartCoroutine(string.IsNullOrEmpty(target) ? WaitForOverlayReady() : LoadTarget(target));
        }

        private void BindFrames()
        {
            frames = gameplayArtCatalog != null ? gameplayArtCatalog.ShellLoadingFrames : System.Array.Empty<Sprite>();
            if (frames.Count != FrameCount || loadingArtImage == null)
            {
                Debug.LogError("[Nyangbingo] Shell loading sheet must be pre-sliced into 17 frames in the Inspector.");
                return;
            }
            loadingArtImage.sprite = frames[0];
        }

        private IEnumerator PlayFrameLoop()
        {
            while (true)
            {
                for (var index = 0; index < frames.Count; index++)
                {
                    loadingArtImage.sprite = frames[index];
                    yield return new WaitForSecondsRealtime(FrameDurations[index]);
                }
            }
        }

        private const string EndLoadingStateName = "EndLoading";

        private IEnumerator LoadTarget(string target)
        {
            var operation = SceneManager.LoadSceneAsync(target, LoadSceneMode.Single);
            operation.allowSceneActivation = false;
            yield return PlayLoadingCompletion(() => operation.progress >= .9f);
            operation.allowSceneActivation = true;
        }

        private IEnumerator WaitForOverlayReady()
        {
            yield return PlayLoadingCompletion(() => LoadingOverlayRequest.IsReady);
            SceneManager.UnloadSceneAsync(SceneTransitionRequest.LoadingSceneName);
        }

        // 준비 신호(isReady)와 WhileLoading 최소 1회 재생을 함께 기다린 뒤 EndLoading을 재생한다.
        // Additive 오버레이처럼 준비가 거의 즉시 끝나는 경우에도 로딩 화면이 뚝 끊기지 않도록 한다.
        private IEnumerator PlayLoadingCompletion(Func<bool> isReady)
        {
            while (!isReady() ||
                   (loadingAnimator != null && loadingAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f))
            {
                yield return null;
            }

            if (loadingAnimator == null) yield break;
            loadingAnimator.SetTrigger(IsEndLoading);
            yield return WaitForEndLoadingAnimation();
        }

        private IEnumerator WaitForEndLoadingAnimation()
        {
            while (!loadingAnimator.GetCurrentAnimatorStateInfo(0).IsName(EndLoadingStateName))
                yield return null;
            while (loadingAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
                yield return null;
        }
    }
}
