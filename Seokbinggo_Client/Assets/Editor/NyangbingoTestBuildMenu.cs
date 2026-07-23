using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
public static class NyangbingoTestBuildMenu
{
    private const string RequestFileName = "CodexWindowsTestBuild.request";

    static NyangbingoTestBuildMenu()
    {
        EditorApplication.delayCall += TryRunRequestedBuild;
    }

    [MenuItem("Nyangbingo/Build Windows Test Player")]
    public static void BuildWindowsTestPlayer()
    {
        var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ??
                          throw new InvalidOperationException("Project root could not be resolved.");
        var outputDirectory = Path.Combine(projectRoot, "Builds", "Test");
        var outputPath = Path.Combine(outputDirectory, "Nyangbingo_Test.exe");
        Directory.CreateDirectory(outputDirectory);

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/MainGame.unity" },
            locationPathName = outputPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development | BuildOptions.AllowDebugging
        });

        var summary = report.summary;
        File.WriteAllText(
            Path.Combine(outputDirectory, "build-result.txt"),
            $"result={summary.result}{Environment.NewLine}" +
            $"errors={summary.totalErrors}{Environment.NewLine}" +
            $"warnings={summary.totalWarnings}{Environment.NewLine}" +
            $"size={summary.totalSize}{Environment.NewLine}" +
            $"duration={summary.totalTime}{Environment.NewLine}");

        if (summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException(
                $"Windows test build failed: {summary.result}, " +
                $"{summary.totalErrors} errors, {summary.totalWarnings} warnings.");

        Debug.Log($"[Nyangbingo] Windows test build completed: {outputPath}");
    }

    private static void TryRunRequestedBuild()
    {
        var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrWhiteSpace(projectRoot)) return;

        var requestPath = Path.Combine(projectRoot, "Temp", RequestFileName);
        if (!File.Exists(requestPath)) return;

        File.Delete(requestPath);
        BuildWindowsTestPlayer();
    }
}
