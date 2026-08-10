using Nyangbingo.Data;
using Nyangbingo.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Title↔MainGame 전환 중 잠깐 떠 있는 Loading.unity를 재현 가능하게 생성한다.</summary>
public static class NyangbingoLoadingSceneCreator
{
    private const string ScenePath = "Assets/Scenes/Loading.unity";
    private const string GameplayArtCatalogPath = "Assets/Art/Gameplay/GameplayArtCatalog.asset";

    [MenuItem("Nyangbingo/Loading/Create or Update Loading Scene")]
    public static void CreateOrUpdate()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.backgroundColor = new Color(.02f, .035f, .05f, 1f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        cameraObject.AddComponent<AudioListener>();

        var canvasObject = new GameObject("LoadingCanvas", typeof(RectTransform));
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = LoadingSceneController.CanvasSortingOrder;
        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasObject.AddComponent<GraphicRaycaster>();

        var backdrop = new GameObject("Backdrop", typeof(RectTransform));
        backdrop.transform.SetParent(canvasObject.transform, false);
        var backdropRect = backdrop.GetComponent<RectTransform>();
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;
        backdrop.AddComponent<Image>().color = new Color(.02f, .035f, .05f, 1f);

        var loadingArtObject = new GameObject("LoadingArt", typeof(RectTransform));
        loadingArtObject.transform.SetParent(canvasObject.transform, false);
        var loadingArtImage = loadingArtObject.AddComponent<Image>();
        loadingArtImage.preserveAspect = true;
        loadingArtImage.raycastTarget = false;
        var loadingArtRect = loadingArtImage.rectTransform;
        loadingArtRect.anchorMin = loadingArtRect.anchorMax = loadingArtRect.pivot = new Vector2(.5f, .5f);
        loadingArtRect.anchoredPosition = new Vector2(0f, 80f);
        loadingArtRect.sizeDelta = new Vector2(400f, 225f);

        var statusObject = new GameObject("Status", typeof(RectTransform));
        statusObject.transform.SetParent(canvasObject.transform, false);
        var statusText = statusObject.AddComponent<Text>();
        statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        statusText.fontSize = 28;
        statusText.alignment = TextAnchor.MiddleCenter;
        statusText.color = new Color(.94f, .96f, 1f, 1f);
        statusText.text = "불러오는 중...";
        var statusRect = statusText.rectTransform;
        statusRect.anchorMin = statusRect.anchorMax = statusRect.pivot = new Vector2(.5f, .5f);
        statusRect.anchoredPosition = new Vector2(0f, -60f);
        statusRect.sizeDelta = new Vector2(600f, 60f);

        var barBackground = new GameObject("ProgressBarBackground", typeof(RectTransform));
        barBackground.transform.SetParent(canvasObject.transform, false);
        var barBackgroundRect = barBackground.GetComponent<RectTransform>();
        barBackgroundRect.anchorMin = barBackgroundRect.anchorMax = barBackgroundRect.pivot = new Vector2(.5f, .5f);
        barBackgroundRect.anchoredPosition = new Vector2(0f, -120f);
        barBackgroundRect.sizeDelta = new Vector2(480f, 24f);
        barBackground.AddComponent<Image>().color = new Color(.12f, .15f, .19f, 1f);

        var barFill = new GameObject("ProgressBarFill", typeof(RectTransform));
        barFill.transform.SetParent(barBackground.transform, false);
        var barFillRect = barFill.GetComponent<RectTransform>();
        barFillRect.anchorMin = Vector2.zero;
        barFillRect.anchorMax = Vector2.one;
        barFillRect.offsetMin = Vector2.zero;
        barFillRect.offsetMax = Vector2.zero;
        var fillImage = barFill.AddComponent<Image>();
        fillImage.color = new Color(.28f, .78f, 1f, 1f);
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillAmount = 0f;

        var controller = canvasObject.AddComponent<LoadingSceneController>();
        var serialized = new SerializedObject(controller);
        serialized.FindProperty("progressFillImage").objectReferenceValue = fillImage;
        serialized.FindProperty("statusText").objectReferenceValue = statusText;
        serialized.FindProperty("loadingArtImage").objectReferenceValue = loadingArtImage;
        serialized.FindProperty("gameplayArtCatalog").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<GameplayArtCatalog>(GameplayArtCatalogPath);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        NyangbingoSceneBuildSettings.SyncBuildSettings();
        AssetDatabase.SaveAssets();
        Debug.Log("[Nyangbingo] Loading.unity 생성/갱신 완료.");
    }
}
