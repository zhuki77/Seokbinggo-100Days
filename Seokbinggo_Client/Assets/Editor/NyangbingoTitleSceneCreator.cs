using Nyangbingo.Audio;
using Nyangbingo.Data;
using Nyangbingo.Save;
using Nyangbingo.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>제품 타이틀 씬(월드 없음)을 생성하고 빌드 첫 씬으로 등록한다.</summary>
public static class NyangbingoTitleSceneCreator
{
    private const string ScenePath = "Assets/Scenes/Title.unity";
    private const string EnvironmentArtCatalogPath = "Assets/Art/Backgrounds/EnvironmentArtCatalog.asset";
    private const string GameplayArtCatalogPath = "Assets/Art/Gameplay/GameplayArtCatalog.asset";
    private const string CharacterArtCatalogPath = "Assets/Art/Characters/CharacterArtCatalog.asset";

    [InitializeOnLoadMethod]
    private static void EnsureTitleSceneOnLoad()
    {
        EditorApplication.delayCall += () =>
        {
            EnsureBuildSettingsQuiet();
            if (!System.IO.File.Exists(ScenePath))
            {
                CreateOrUpdate();
                return;
            }

            var sceneText = System.IO.File.ReadAllText(ScenePath);
            if (sceneText.IndexOf("TitleShellUiController", System.StringComparison.Ordinal) < 0 &&
                sceneText.IndexOf("TitleBootstrap", System.StringComparison.Ordinal) < 0)
                CreateOrUpdate();
        };
    }

    [MenuItem("Nyangbingo/Main Game/Create or Update Title Scene")]
    public static void CreateOrUpdate()
    {
        var environmentArt = AssetDatabase.LoadAssetAtPath<EnvironmentArtCatalog>(EnvironmentArtCatalogPath);
        var gameplayArt = AssetDatabase.LoadAssetAtPath<GameplayArtCatalog>(GameplayArtCatalogPath);
        var characterArt = AssetDatabase.LoadAssetAtPath<CharacterArtCatalog>(CharacterArtCatalogPath);
        if (environmentArt == null)
        {
            Debug.LogError("[Nyangbingo] Title 씬 생성 실패: EnvironmentArtCatalog를 찾을 수 없습니다.");
            return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateCamera();
        CreateTitleShell(environmentArt, gameplayArt, characterArt);
        EditorSceneManager.SaveScene(scene, ScenePath);
        EnsureBuildSettings();
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Debug.Log("[Nyangbingo] Title 씬 생성/갱신 완료: " + ScenePath);
    }

    private static void CreateCamera()
    {
        var cameraObject = new GameObject("Main Camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.02f, .035f, .05f, 1f);
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        cameraObject.tag = "MainCamera";
        cameraObject.AddComponent<AudioListener>();
    }

    private static void CreateTitleShell(EnvironmentArtCatalog environmentArt,
        GameplayArtCatalog gameplayArt, CharacterArtCatalog characterArt)
    {
        var root = new GameObject("TitleBootstrap");
        var saveManager = root.AddComponent<SaveManager>();
        var audioService = root.AddComponent<NyangbingoAudioService>();

        var eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();

        var canvasObject = new GameObject("TitleCanvas", typeof(RectTransform));
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasObject.AddComponent<GraphicRaycaster>();

        var titlePanel = CreateOverlayPanel(canvasObject.transform, "TitlePanel",
            new Color(.02f, .035f, .05f, 1f));
        // 좌표는 구 MainGame 타이틀 × TitleChromeScale(3).
        CreateMenuText(titlePanel.transform, "Title", "100일의 냥빙고", new Vector2(-336f, 264f),
            new Vector2(540f, 90f), 60);
        var titleContinue = CreateMenuButton(titlePanel.transform, "Continue", "이어하기",
            new Vector2(-336f, 51f), new Vector2(450f, 81f));
        var titleNew = CreateMenuButton(titlePanel.transform, "NewGame", "새 게임",
            new Vector2(-336f, -51f), new Vector2(450f, 81f));
        var titleQuit = CreateMenuButton(titlePanel.transform, "Quit", "게임 종료",
            new Vector2(-336f, -153f), new Vector2(450f, 81f));
        var statusText = CreateMenuText(titlePanel.transform, "Status", "", new Vector2(-336f, -330f),
            new Vector2(840f, 84f), 42);

        var confirmationPanel = CreateOverlayPanel(canvasObject.transform, "ConfirmationPanel",
            new Color(.06f, .04f, .05f, 1f));
        var confirmationText = CreateMenuText(confirmationPanel.transform, "Message", "확인하시겠습니까?",
            new Vector2(0f, 70f), new Vector2(720f, 120f), 26);
        var confirmButton = CreateMenuButton(confirmationPanel.transform, "Confirm", "확인",
            new Vector2(-110f, -65f), new Vector2(190f, 52f));
        var cancelButton = CreateMenuButton(confirmationPanel.transform, "Cancel", "취소",
            new Vector2(110f, -65f), new Vector2(190f, 52f));
        confirmationPanel.SetActive(false);

        var shell = canvasObject.AddComponent<GameShellController>();
        shell.ConfigureViews(titlePanel, null, null, null, confirmationPanel);
        var titleShell = canvasObject.AddComponent<TitleShellUiController>();
        titleShell.ConfigureForScene(shell, saveManager, audioService, titleContinue, titleNew, titleQuit,
            confirmButton, cancelButton, confirmationText, statusText, environmentArt, gameplayArt, characterArt);

        EditorUtility.SetDirty(shell);
        EditorUtility.SetDirty(titleShell);
        EditorUtility.SetDirty(saveManager);
        EditorUtility.SetDirty(audioService);
    }

    private static void EnsureBuildSettings()
    {
        EnsureBuildSettingsQuiet();
        Debug.Log("[Nyangbingo] Build settings: Title → MainGame.");
    }

    private static void EnsureBuildSettingsQuiet()
    {
        const string mainPath = "Assets/Scenes/MainGame.unity";
        var ordered = new System.Collections.Generic.List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(ScenePath, true),
            new EditorBuildSettingsScene(mainPath, true)
        };
        foreach (var entry in EditorBuildSettings.scenes)
        {
            if (entry.path == ScenePath || entry.path == mainPath) continue;
            ordered.Add(entry);
        }

        EditorBuildSettings.scenes = ordered.ToArray();
    }

    private static GameObject CreateOverlayPanel(Transform parent, string name, Color color)
    {
        var panel = new GameObject(name, typeof(RectTransform));
        panel.transform.SetParent(parent, false);
        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        panel.AddComponent<Image>().color = color;
        return panel;
    }

    private static Text CreateMenuText(Transform parent, string name, string value, Vector2 position,
        Vector2 size, int fontSize)
    {
        var textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        var text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(.94f, .96f, 1f, 1f);
        text.text = value;
        var rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return text;
    }

    private static Button CreateMenuButton(Transform parent, string name, string label, Vector2 position,
        Vector2 size)
    {
        var buttonObject = new GameObject(name, typeof(RectTransform));
        buttonObject.transform.SetParent(parent, false);
        var image = buttonObject.AddComponent<Image>();
        image.color = new Color(.14f, .2f, .28f, .96f);
        var button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        var rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        CreateMenuText(buttonObject.transform, "Label", label, Vector2.zero, size, 22);
        return button;
    }
}
