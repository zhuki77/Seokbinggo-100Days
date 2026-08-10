using System.Collections.Generic;
using System.Linq;
using Nyangbingo.Audio;
using Nyangbingo.Data;
using Nyangbingo.Save;
using Nyangbingo.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>타이틀 화면 전용 Title.unity를 재현 가능하게 생성하고 필수 배선을 검증한다.</summary>
public static class NyangbingoTitleSceneCreator
{
    private const string ScenePath = NyangbingoSceneBuildSettings.TitleScenePath;
    private const string CharacterArtCatalogPath = "Assets/Art/Characters/CharacterArtCatalog.asset";
    private const string EnvironmentArtCatalogPath = "Assets/Art/Backgrounds/EnvironmentArtCatalog.asset";
    private const string GameplayArtCatalogPath = "Assets/Art/Gameplay/GameplayArtCatalog.asset";

    [MenuItem("Nyangbingo/Title/Create or Update Title Scene")]
    public static void CreateOrUpdate()
    {
        var characterArtCatalog = AssetDatabase.LoadAssetAtPath<CharacterArtCatalog>(CharacterArtCatalogPath);
        var environmentArtCatalog = AssetDatabase.LoadAssetAtPath<EnvironmentArtCatalog>(EnvironmentArtCatalogPath);
        var gameplayArtCatalog = AssetDatabase.LoadAssetAtPath<GameplayArtCatalog>(GameplayArtCatalogPath);
        if (environmentArtCatalog == null || gameplayArtCatalog == null)
        {
            Debug.LogError("[Nyangbingo] Title 씬 생성 실패: EnvironmentArtCatalog 또는 GameplayArtCatalog " +
                           "에셋을 찾을 수 없습니다. 기존 Title 씬은 덮어쓰지 않습니다.");
            return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.backgroundColor = new Color(.02f, .035f, .05f, 1f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        cameraObject.AddComponent<AudioListener>();

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
        canvasObject.AddComponent<MainGameUiResolutionController>();

        var titlePanel = CreateOverlayPanel(canvasObject.transform, "TitlePanel", new Color(.02f, .035f, .05f, 1f));
        CreateMenuText(titlePanel.transform, "Title", "100일의 냥빙고", new Vector2(0f, 180f),
            new Vector2(620f, 80f), 48);
        var titleContinue = CreateMenuButton(titlePanel.transform, "Continue", "이어하기",
            new Vector2(0f, 60f), new Vector2(300f, 58f));
        var titleNew = CreateMenuButton(titlePanel.transform, "NewGame", "새 게임",
            new Vector2(0f, -10f), new Vector2(300f, 58f));
        var titleQuit = CreateMenuButton(titlePanel.transform, "Quit", "게임 종료",
            new Vector2(0f, -80f), new Vector2(300f, 58f));

        var demoButtons = new List<Button>();
        for (var index = 0; index < GameShellController.DemoSaveDays.Length; index++)
        {
            var day = GameShellController.DemoSaveDays[index];
            var demoButton = CreateMenuButton(titlePanel.transform, $"DemoSaveDay{day}", $"{day}일차 데모",
                new Vector2(-648f + index * 200f, -328f), new Vector2(184f, 60f));
            demoButtons.Add(demoButton);
        }

        var confirmationPanel = CreateOverlayPanel(titlePanel.transform, "ConfirmationPanel",
            new Color(.06f, .04f, .05f, 1f));
        var confirmationText = CreateMenuText(confirmationPanel.transform, "Message", "확인하시겠습니까?",
            new Vector2(0f, 70f), new Vector2(720f, 120f), 26);
        var confirmButton = CreateMenuButton(confirmationPanel.transform, "Confirm", "확인",
            new Vector2(-110f, -65f), new Vector2(190f, 52f));
        var cancelButton = CreateMenuButton(confirmationPanel.transform, "Cancel", "취소",
            new Vector2(110f, -65f), new Vector2(190f, 52f));

        var statusText = CreateMenuText(titlePanel.transform, "Status", "", new Vector2(0f, -140f),
            new Vector2(760f, 48f), 20);

        var saveManager = canvasObject.AddComponent<SaveManager>();
        var audioService = canvasObject.AddComponent<NyangbingoAudioService>();
        ConfigureAudioMixer(audioService);

        var shell = canvasObject.AddComponent<TitleShellController>();
        var shellUi = canvasObject.AddComponent<TitleUiController>();
        shellUi.ConfigureForScene(shell, saveManager, audioService, titleContinue, titleNew, titleQuit,
            demoButtons, confirmationPanel, confirmButton, cancelButton, confirmationText, statusText,
            environmentArtCatalog, gameplayArtCatalog, characterArtCatalog);

        EditorUtility.SetDirty(saveManager);
        EditorUtility.SetDirty(audioService);
        EditorUtility.SetDirty(shell);
        EditorUtility.SetDirty(shellUi);

        EditorSceneManager.SaveScene(scene, ScenePath);
        NyangbingoSceneBuildSettings.SyncBuildSettings();
        AssetDatabase.SaveAssets();
        EditorApplication.delayCall += Validate;
        Selection.activeGameObject = canvasObject;
    }

    [MenuItem("Nyangbingo/Title/Validate Title Scene")]
    public static void Validate()
    {
        var scene = SceneManager.GetActiveScene().path == ScenePath
            ? SceneManager.GetActiveScene()
            : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var shell = Object.FindAnyObjectByType<TitleShellController>();
        var shellUi = Object.FindAnyObjectByType<TitleUiController>();
        var eventSystem = Object.FindAnyObjectByType<EventSystem>();
        var camera = Camera.main;
        var buildScene = EditorBuildSettings.scenes.FirstOrDefault(entry => entry.path == ScenePath);

        var missingBindings = new List<string>();
        void Require(bool condition, string name) { if (!condition) missingBindings.Add(name); }
        Require(scene.IsValid(), "scene");
        Require(shell != null, "TitleShellController");
        Require(shellUi != null, "TitleUiController");
        Require(eventSystem != null, "EventSystem");
        Require(camera != null, "Main Camera");
        Require(buildScene != null && buildScene.enabled, "Build Settings");

        if (missingBindings.Count == 0)
            Debug.Log("[Nyangbingo] Title 씬 검증 완료: TitleShellController, TitleUiController, " +
                      "EventSystem, Main Camera, Build Settings 배선 정상.");
        else
            Debug.LogError("[Nyangbingo] Title 씬 검증 실패: 누락 Binding=" +
                           $"[{string.Join(", ", missingBindings)}]. Create or Update Title Scene을 다시 실행하세요.");
    }

    private static void ConfigureAudioMixer(NyangbingoAudioService audioService)
    {
        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(NyangbingoAudioMixerIntegrator.MixerPath);
        if (mixer == null || audioService == null) return;
        var bgm = mixer.FindMatchingGroups(NyangbingoAudioMixerIntegrator.BgmGroupName)
            .FirstOrDefault(group => group.name == NyangbingoAudioMixerIntegrator.BgmGroupName);
        var sfx = mixer.FindMatchingGroups(NyangbingoAudioMixerIntegrator.SfxGroupName)
            .FirstOrDefault(group => group.name == NyangbingoAudioMixerIntegrator.SfxGroupName);
        var serialized = new SerializedObject(audioService);
        serialized.FindProperty("audioMixer").objectReferenceValue = mixer;
        serialized.FindProperty("bgmOutput").objectReferenceValue = bgm;
        serialized.FindProperty("sfxOutput").objectReferenceValue = sfx;
        serialized.ApplyModifiedPropertiesWithoutUndo();
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
        ConfigureText(text, TextAnchor.MiddleCenter, fontSize);
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
        var rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var image = buttonObject.AddComponent<Image>();
        image.color = new Color(.17f, .22f, .28f, 1f);
        var button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        var labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(buttonObject.transform, false);
        var text = labelObject.AddComponent<Text>();
        ConfigureText(text, TextAnchor.MiddleCenter, 21);
        text.text = label;
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
        return button;
    }

    private static void ConfigureText(Text text, TextAnchor alignment, int fontSize)
    {
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = new Color(.94f, .96f, 1f, 1f);
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
    }
}
