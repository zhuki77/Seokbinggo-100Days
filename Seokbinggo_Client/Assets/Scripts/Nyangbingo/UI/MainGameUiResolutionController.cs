using UnityEngine;
using UnityEngine.UI;

namespace Nyangbingo.UI
{
    /// <summary>
    /// 기획 정본 UI 논리 해상도 480x270을 적용한다. 모든 HUD/오버레이 UI는 하이어라키에
    /// 480x270 좌표로 빌드타임에 배치되어 있으므로, 이 컨트롤러는 CanvasScaler를
    /// 해당 기준 해상도로 맞추는 역할만 한다.
    /// </summary>
    [DefaultExecutionOrder(-65)]
    public sealed class MainGameUiResolutionController : MonoBehaviour
    {
        public static readonly Vector2 LogicalResolution = new Vector2(480f, 270f);

        private Canvas targetCanvas;
        private bool initialized;

        public bool IsInitialized => initialized;
        public Transform NativeRoot => targetCanvas != null ? targetCanvas.transform : transform;

        public void ConfigureForRuntime(Canvas canvas)
        {
            targetCanvas = canvas;
            Initialize();
        }

        private void Awake()
        {
            targetCanvas ??= GetComponent<Canvas>() ?? GetComponentInParent<Canvas>();
        }

        private void Start() => Initialize();

        private void Initialize()
        {
            if (initialized || targetCanvas == null) return;

            var scaler = targetCanvas.GetComponent<CanvasScaler>() ?? targetCanvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = LogicalResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            targetCanvas.pixelPerfect = true;
            initialized = true;

            Debug.Log($"[Nyangbingo] MainGame UI logical resolution ready " +
                      $"({LogicalResolution.x:0}x{LogicalResolution.y:0}).");
        }

        public static Transform ResolveNativeRoot(Transform context)
        {
            if (context == null) return null;
            var canvas = context.GetComponent<Canvas>() ?? context.GetComponentInParent<Canvas>();
            if (canvas == null) return context;
            var controller = canvas.GetComponent<MainGameUiResolutionController>();
            return controller != null && controller.IsInitialized ? controller.NativeRoot : canvas.transform;
        }
    }
}
