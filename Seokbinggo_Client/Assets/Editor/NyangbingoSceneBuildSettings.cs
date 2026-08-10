using System.IO;
using System.Linq;
using UnityEditor;

/// <summary>Title→Loading→MainGame 고정 순서로 Build Settings를 동기화하는 공용 헬퍼.</summary>
internal static class NyangbingoSceneBuildSettings
{
    public const string TitleScenePath = "Assets/Scenes/Title.unity";
    public const string LoadingScenePath = "Assets/Scenes/Loading.unity";
    public const string MainGameScenePath = "Assets/Scenes/MainGame.unity";

    private static readonly string[] CanonicalOrder = { TitleScenePath, LoadingScenePath, MainGameScenePath };

    public static void SyncBuildSettings()
    {
        var existingOthers = EditorBuildSettings.scenes
            .Where(entry => !CanonicalOrder.Contains(entry.path))
            .ToArray();
        var canonical = CanonicalOrder
            .Where(File.Exists)
            .Select(path => new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = canonical.Concat(existingOthers).ToArray();
    }
}
