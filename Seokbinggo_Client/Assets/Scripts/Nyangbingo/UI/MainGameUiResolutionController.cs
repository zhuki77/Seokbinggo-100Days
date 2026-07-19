using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Nyangbingo.UI
{
    /// <summary>
    /// 기획 정본 UI 논리 해상도 480x270을 적용한다. 기존 MainGame UI는 1920x1080
    /// 좌표로 제작되어 있으므로 0.25 호환 루트 아래로 이동해 현재 화면 크기를 보존한다.
    /// 신규 UI는 Canvas 직속에 480x270 좌표로 배치하고, 기존 UI는 순차적으로 이관한다.
    /// </summary>
    [DefaultExecutionOrder(-65)]
    public sealed class MainGameUiResolutionController : MonoBehaviour
    {
        public static readonly Vector2 LogicalResolution = new Vector2(480f, 270f);
        private static readonly Vector2 LegacyResolution = new Vector2(1920f, 1080f);
        private const float LegacyToNativeScale = .25f;
        private static readonly HashSet<string> NativeHudNames = new HashSet<string>
        {
            "TemperatureArt", "Temperature", "ClawTier", "PlayerHealth", "DayCounter",
            "BossStatus", "BossSummonStatus", "TurretInteractionStatus", "SealTemperature",
            "CraftingProgress", "TurretBuildOpen", "Inventory12"
        };
        private static readonly HashSet<string> NativeOverlayNames = new HashSet<string>
        {
            "TitlePanel", "PausePanel", "DeathPanel", "ResultPanel", "CodexPanel", "TurretBuildPanel"
        };

        private Canvas targetCanvas;
        private RectTransform nativeHudRoot;
        private RectTransform nativeOverlayRoot;
        private RectTransform legacyRoot;
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

            var canvasTransform = targetCanvas.transform;
            var existingChildren = new List<Transform>(canvasTransform.childCount);
            for (var index = 0; index < canvasTransform.childCount; index++)
                existingChildren.Add(canvasTransform.GetChild(index));

            var legacyObject = new GameObject("Legacy1920UiRoot", typeof(RectTransform));
            legacyRoot = (RectTransform)legacyObject.transform;
            legacyRoot.SetParent(canvasTransform, false);
            legacyRoot.anchorMin = legacyRoot.anchorMax = legacyRoot.pivot = new Vector2(.5f, .5f);
            legacyRoot.anchoredPosition = Vector2.zero;
            legacyRoot.sizeDelta = LegacyResolution;
            legacyRoot.localScale = Vector3.one * LegacyToNativeScale;

            foreach (var child in existingChildren)
                child.SetParent(legacyRoot, false);

            nativeHudRoot = CreateFullCanvasRoot("Native480HudRoot", canvasTransform);
            nativeHudRoot.SetSiblingIndex(0);
            legacyRoot.SetSiblingIndex(1);
            var nativeHudCount = MigrateNativeHud();
            nativeOverlayRoot = CreateFullCanvasRoot("Native480OverlayRoot", canvasTransform);
            var nativeOverlayCount = MigrateNativeOverlays();

            var scaler = targetCanvas.GetComponent<CanvasScaler>() ?? targetCanvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = LogicalResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            targetCanvas.pixelPerfect = true;
            initialized = true;

            Debug.Log($"[Nyangbingo] MainGame UI logical resolution ready " +
                      $"({LogicalResolution.x:0}x{LogicalResolution.y:0}, nativeHud={nativeHudCount}, " +
                      $"nativeOverlays={nativeOverlayCount}, legacyOverlays={legacyRoot.childCount}).");
        }

        private void LateUpdate()
        {
            if (nativeOverlayRoot == null || !HasActiveChild(nativeOverlayRoot)) return;
            if (nativeOverlayRoot.GetSiblingIndex() != nativeOverlayRoot.parent.childCount - 1)
                nativeOverlayRoot.SetAsLastSibling();
        }

        private int MigrateNativeHud()
        {
            var candidates = new List<Transform>(legacyRoot.childCount);
            for (var index = 0; index < legacyRoot.childCount; index++)
                candidates.Add(legacyRoot.GetChild(index));

            var migrated = 0;
            foreach (var candidate in candidates)
            {
                if (!NativeHudNames.Contains(candidate.name) || !(candidate is RectTransform rect)) continue;
                var anchoredPosition = rect.anchoredPosition * LegacyToNativeScale;
                var sizeDelta = rect.sizeDelta;
                var localScale = rect.localScale * LegacyToNativeScale;
                rect.SetParent(nativeHudRoot, false);
                rect.anchoredPosition = anchoredPosition;
                rect.sizeDelta = sizeDelta;
                rect.localScale = localScale;
                migrated++;
            }
            return migrated;
        }

        private int MigrateNativeOverlays()
        {
            var candidates = new List<Transform>(legacyRoot.childCount);
            for (var index = 0; index < legacyRoot.childCount; index++)
                candidates.Add(legacyRoot.GetChild(index));

            var migrated = 0;
            foreach (var candidate in candidates)
            {
                if (!NativeOverlayNames.Contains(candidate.name) || !(candidate is RectTransform rect)) continue;
                rect.SetParent(nativeOverlayRoot, false);
                ScaleLayoutHierarchy(rect);
                migrated++;
            }
            return migrated;
        }

        private static void ScaleLayoutHierarchy(RectTransform root)
        {
            var rects = root.GetComponentsInChildren<RectTransform>(true);
            foreach (var rect in rects)
            {
                rect.anchoredPosition *= LegacyToNativeScale;
                rect.sizeDelta *= LegacyToNativeScale;

                var text = rect.GetComponent<Text>();
                if (text != null)
                {
                    text.fontSize = ScalePixelValue(text.fontSize);
                    text.resizeTextMinSize = ScalePixelValue(text.resizeTextMinSize);
                    text.resizeTextMaxSize = ScalePixelValue(text.resizeTextMaxSize);
                }

                var shadow = rect.GetComponent<Shadow>();
                if (shadow != null) shadow.effectDistance *= LegacyToNativeScale;

                var grid = rect.GetComponent<GridLayoutGroup>();
                if (grid != null)
                {
                    grid.padding = ScaleRectOffset(grid.padding);
                    grid.cellSize *= LegacyToNativeScale;
                    grid.spacing *= LegacyToNativeScale;
                }

                var linear = rect.GetComponent<HorizontalOrVerticalLayoutGroup>();
                if (linear != null)
                {
                    linear.padding = ScaleRectOffset(linear.padding);
                    linear.spacing *= LegacyToNativeScale;
                }

                var element = rect.GetComponent<LayoutElement>();
                if (element != null)
                {
                    element.minWidth = ScaleOptionalLayoutValue(element.minWidth);
                    element.minHeight = ScaleOptionalLayoutValue(element.minHeight);
                    element.preferredWidth = ScaleOptionalLayoutValue(element.preferredWidth);
                    element.preferredHeight = ScaleOptionalLayoutValue(element.preferredHeight);
                    element.flexibleWidth = ScaleOptionalLayoutValue(element.flexibleWidth);
                    element.flexibleHeight = ScaleOptionalLayoutValue(element.flexibleHeight);
                }
            }
        }

        private static int ScalePixelValue(int value) =>
            value <= 0 ? value : Mathf.Max(1, Mathf.RoundToInt(value * LegacyToNativeScale));

        private static float ScaleOptionalLayoutValue(float value) =>
            value < 0f ? value : value * LegacyToNativeScale;

        private static RectOffset ScaleRectOffset(RectOffset value) => new RectOffset(
            ScalePixelValue(value.left), ScalePixelValue(value.right),
            ScalePixelValue(value.top), ScalePixelValue(value.bottom));

        private static bool HasActiveChild(RectTransform root)
        {
            for (var index = 0; index < root.childCount; index++)
                if (root.GetChild(index).gameObject.activeInHierarchy) return true;
            return false;
        }

        private static RectTransform CreateFullCanvasRoot(string objectName, Transform parent)
        {
            var rootObject = new GameObject(objectName, typeof(RectTransform));
            var root = (RectTransform)rootObject.transform;
            root.SetParent(parent, false);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.pivot = new Vector2(.5f, .5f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = Vector2.zero;
            return root;
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
