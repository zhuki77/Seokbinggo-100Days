using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
public static class NyangbingoTestBuildMenu
{
    private const string RequestFileName = "CodexWindowsTestBuild.request";
    internal const string ProductScenePath = NyangbingoSceneBuildSettings.MainGameScenePath;
    internal static readonly string[] ProductScenePaths =
    {
        NyangbingoSceneBuildSettings.TitleScenePath,
        NyangbingoSceneBuildSettings.LoadingScenePath,
        NyangbingoSceneBuildSettings.MainGameScenePath
    };
    internal const string ProductExecutableName = "Nyangbingo.exe";
    internal const string TestExecutableName = "Nyangbingo_Test.exe";

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
        var outputPath = Path.Combine(outputDirectory, TestExecutableName);
        Directory.CreateDirectory(outputDirectory);

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = ProductScenePaths,
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

    [MenuItem("Nyangbingo/Build Windows Product Player")]
    public static void BuildWindowsProductPlayer()
    {
        ValidateProductBuildSettings(throwOnFailure: true);
        var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ??
                          throw new InvalidOperationException("Project root could not be resolved.");
        var outputDirectory = Path.Combine(projectRoot, "Builds", "Windows");
        var outputPath = Path.Combine(outputDirectory, ProductExecutableName);
        Directory.CreateDirectory(outputDirectory);

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = ProductScenePaths,
            locationPathName = outputPath,
            target = BuildTarget.StandaloneWindows64,
            options = ProductBuildOptions
        });
        var summary = report.summary;
        if (summary.result != BuildResult.Succeeded)
        {
            WriteBuildResult(outputDirectory, "product-build-result.txt", report);
            throw new InvalidOperationException(
                $"Windows product build failed: {summary.result}, " +
                $"{summary.totalErrors} errors, {summary.totalWarnings} warnings.");
        }
        RemoveNonShippingArtifacts(projectRoot, outputDirectory);
        WriteBuildResult(outputDirectory, "product-build-result.txt", report);
        Debug.Log($"[Nyangbingo] Windows product build completed: {outputPath}");
    }

    [MenuItem("Nyangbingo/Validate Windows Product Build Settings")]
    public static void ValidateWindowsProductBuildSettings() =>
        ValidateProductBuildSettings(throwOnFailure: false);

    [MenuItem("Nyangbingo/Validate Windows Product Build Artifacts")]
    public static void ValidateWindowsProductBuildArtifacts()
    {
        var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ??
                          throw new InvalidOperationException("Project root could not be resolved.");
        var outputDirectory = Path.Combine(projectRoot, "Builds", "Windows");
        var resultPath = Path.Combine(outputDirectory, "product-build-result.txt");
        var failures = new System.Collections.Generic.List<string>();
        if (!File.Exists(Path.Combine(outputDirectory, ProductExecutableName)))
            failures.Add($"Product executable missing: {ProductExecutableName}");
        if (!Directory.Exists(Path.Combine(outputDirectory, "Nyangbingo_Data")))
            failures.Add("Product data directory missing: Nyangbingo_Data");
        var result = File.Exists(resultPath) ? File.ReadAllText(resultPath) : string.Empty;
        if (!result.Contains("result=Succeeded") ||
            !result.Contains("development=False") ||
            !result.Contains("allowDebugging=False"))
            failures.Add("Product build report is missing or contains development flags.");
        if (Directory.Exists(outputDirectory) &&
            Directory.EnumerateFileSystemEntries(outputDirectory, "*DoNotShip*",
                SearchOption.AllDirectories).Any())
            failures.Add("DoNotShip build artifacts remain in the product directory.");
        if (Directory.Exists(outputDirectory) &&
            Directory.EnumerateFiles(outputDirectory, "*.pdb", SearchOption.AllDirectories).Any())
            failures.Add("Debug symbol files remain in the product directory.");

        if (failures.Count > 0)
        {
            Debug.LogError("[Nyangbingo] Windows product build artifact validation failed:\n- " +
                           string.Join("\n- ", failures));
            return;
        }
        Debug.Log("[Nyangbingo] Windows product build artifact validation passed: " +
                  "runtime files present, non-development report, no DoNotShip/debug symbols.");
    }

    internal static BuildOptions ProductBuildOptions => BuildOptions.None;
    internal static BuildOptions TestBuildOptions =>
        BuildOptions.Development | BuildOptions.AllowDebugging;

    private static bool ValidateProductBuildSettings(bool throwOnFailure)
    {
        var enabledScenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
        var failures = new System.Collections.Generic.List<string>();
        if (!enabledScenes.SequenceEqual(ProductScenePaths, StringComparer.Ordinal))
            failures.Add("Build Settings must contain Title, Loading, and MainGame in canonical order.");
        foreach (var scenePath in ProductScenePaths)
            if (!File.Exists(scenePath)) failures.Add($"Product scene missing: {scenePath}");
        if (string.IsNullOrWhiteSpace(PlayerSettings.productName))
            failures.Add("PlayerSettings.productName is empty.");
        if (!NyangbingoDataBuildGate.TryValidateCurrent(out var dataValidationSummary))
            failures.Add($"Product data is stale or invalid: {dataValidationSummary}");

        if (failures.Count > 0)
        {
            var message = "[Nyangbingo] Windows product build validation failed:\n- " +
                          string.Join("\n- ", failures);
            if (throwOnFailure) throw new InvalidOperationException(message);
            Debug.LogError(message);
            return false;
        }

        if (string.Equals(PlayerSettings.companyName, "DefaultCompany", StringComparison.Ordinal) ||
            PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Standalone)
                .IndexOf("DefaultCompany", StringComparison.OrdinalIgnoreCase) >= 0)
            Debug.LogWarning("[Nyangbingo] Product identity still uses DefaultCompany. " +
                             "Set the confirmed team/company name before external distribution.");
        Debug.Log("[Nyangbingo] Windows product build validation passed: " +
                  "Title/Loading/MainGame, Windows x64, non-development flags, current v72 data manifest.");
        return true;
    }

    private static void WriteBuildResult(
        string outputDirectory, string fileName, BuildReport report)
    {
        var summary = report.summary;
        File.WriteAllText(
            Path.Combine(outputDirectory, fileName),
            $"result={summary.result}{Environment.NewLine}" +
            $"errors={summary.totalErrors}{Environment.NewLine}" +
            $"warnings={summary.totalWarnings}{Environment.NewLine}" +
            $"size={summary.totalSize}{Environment.NewLine}" +
            $"duration={summary.totalTime}{Environment.NewLine}" +
            $"development={summary.options.HasFlag(BuildOptions.Development)}{Environment.NewLine}" +
            $"allowDebugging={summary.options.HasFlag(BuildOptions.AllowDebugging)}{Environment.NewLine}");
    }

    private static void RemoveNonShippingArtifacts(string projectRoot, string outputDirectory)
    {
        var buildsRoot = Path.GetFullPath(Path.Combine(projectRoot, "Builds")) +
                         Path.DirectorySeparatorChar;
        var resolvedOutput = Path.GetFullPath(outputDirectory) + Path.DirectorySeparatorChar;
        if (!resolvedOutput.StartsWith(buildsRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Refusing to clean product output outside Builds: {resolvedOutput}");

        foreach (var directory in Directory.GetDirectories(
                     outputDirectory, "*DoNotShip*", SearchOption.TopDirectoryOnly))
            Directory.Delete(directory, recursive: true);
        var staleBuildLog = Path.Combine(outputDirectory, "unity-build.log");
        if (File.Exists(staleBuildLog)) File.Delete(staleBuildLog);
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
