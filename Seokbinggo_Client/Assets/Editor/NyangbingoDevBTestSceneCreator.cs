using Nyangbingo.Debugging;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class NyangbingoDevBTestSceneCreator
{
    [MenuItem("Nyangbingo/Create Dev B Test Scene")]
    private static void Create()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var bootstrap = new GameObject("DevBTestBootstrap");
        bootstrap.AddComponent<DevBTestBootstrap>();
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/DevBTest.unity");
        EditorGUIUtility.PingObject(bootstrap);
    }
}
