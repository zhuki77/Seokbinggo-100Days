using System;
using System.IO;
using System.Linq;
using Nyangbingo.Save;
using Nyangbingo.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PR #36 Test plan 중 에디터에서 자동 검증 가능한 항목을 한 번에 실행한다.
/// </summary>
public static class NyangbingoPrTestVerificationMenu
{
    private const string DemoSaveFolder = "Assets/StreamingAssets/DemoSaves";

    [MenuItem("Nyangbingo/Run PR Test Checklist")]
    public static void RunChecklist()
    {
        NyangbingoEditorVerifyLog.SummaryHeader("PR 테스트 체크리스트");

        var dataPassed = NyangbingoDataBuildGate.TryValidateCurrent(out var dataSummary);
        NyangbingoEditorVerifyLog.SummaryLine("Validate Product Data Freshness", dataPassed, dataSummary);

        var scenePassed = NyangbingoMainGameSceneCreator.TryValidate(out var sceneSummary);
        NyangbingoEditorVerifyLog.SummaryLine("Validate Main Game Scene (HUD 배선)", scenePassed, sceneSummary);

        var regressionPassed = NyangbingoProductVerificationMenu.TryRunDevBRegression(out var regressionSummary);
        NyangbingoEditorVerifyLog.SummaryLine("Run Dev B Integration Regression Tests", regressionPassed,
            regressionSummary);

        var audioPassed = NyangbingoAudioMixerIntegrator.TryValidate(out var audioSummary);
        NyangbingoEditorVerifyLog.SummaryLine("Validate Product Audio Mixer", audioPassed, audioSummary);

        var demoSavesPassed = TryValidateOfficialDemoSaves(out var demoSummary);
        NyangbingoEditorVerifyLog.SummaryLine("Official demo saves day 1/15/30", demoSavesPassed, demoSummary);

        var titlePassed = TryValidateTitleHandoff(out var titleSummary);
        NyangbingoEditorVerifyLog.SummaryLine("Title scene handoff wiring", titlePassed, titleSummary);

        var resultPassed = TryValidateResultScreenContract(out var resultSummary);
        NyangbingoEditorVerifyLog.SummaryLine("Demo result screen contract", resultPassed, resultSummary);

        var allPassed = dataPassed && scenePassed && regressionPassed && audioPassed &&
                        demoSavesPassed && titlePassed && resultPassed;
        NyangbingoEditorVerifyLog.SummaryFooter(allPassed);
    }

    private static bool TryValidateOfficialDemoSaves(out string summary)
    {
        var loaded = 0;
        foreach (var day in GameShellController.DemoSaveDays)
        {
            var path = $"{DemoSaveFolder}/day-{day}.json";
            if (!File.Exists(path))
            {
                summary = $"missing {path}";
                return false;
            }

            if (!SaveManager.TryDeserialize(File.ReadAllText(path), out var save))
            {
                summary = $"deserialize failed day-{day}";
                return false;
            }

            if (save.schemaVersion != SaveGame.CurrentSchemaVersion)
            {
                summary = $"day-{day} schema {save.schemaVersion} != {SaveGame.CurrentSchemaVersion}";
                return false;
            }

            if (!save.isOfficialDemo || save.day != day)
            {
                summary = $"day-{day} official flag or day mismatch";
                return false;
            }

            if (save.frostClearedBossIds == null)
            {
                summary = $"day-{day} frostClearedBossIds null";
                return false;
            }

            loaded++;
        }

        summary = $"{loaded}/{GameShellController.DemoSaveDays.Length} official demo saves";
        return loaded == GameShellController.DemoSaveDays.Length;
    }

    private static bool TryValidateTitleHandoff(out string summary)
    {
        var enabledScenes = EditorBuildSettings.scenes
            .Where(entry => entry.enabled)
            .Select(entry => entry.path)
            .ToArray();
        if (enabledScenes.Length < 3 ||
            enabledScenes[0] != NyangbingoSceneBuildSettings.TitleScenePath ||
            enabledScenes[1] != NyangbingoSceneBuildSettings.LoadingScenePath ||
            enabledScenes[2] != NyangbingoSceneBuildSettings.MainGameScenePath)
        {
            summary = "build order must be Title → Loading → MainGame";
            return false;
        }

        var scene = EditorSceneManager.OpenScene(
            NyangbingoSceneBuildSettings.TitleScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            summary = "Title scene missing";
            return false;
        }

        var titleUi = UnityEngine.Object.FindAnyObjectByType<TitleUiController>();
        if (titleUi == null)
        {
            summary = "TitleUiController missing";
            return false;
        }

        var serialized = new SerializedObject(titleUi);
        if (serialized.FindProperty("titleNewGameButton").objectReferenceValue == null)
        {
            summary = "titleNewGameButton unassigned";
            return false;
        }

        var demoButtons = serialized.FindProperty("demoSaveButtons");
        if (demoButtons == null || demoButtons.arraySize != GameShellController.DemoSaveDays.Length)
        {
            summary = $"demoSaveButtons {demoButtons?.arraySize ?? 0}/" +
                      $"{GameShellController.DemoSaveDays.Length}";
            return false;
        }

        for (var index = 0; index < demoButtons.arraySize; index++)
        {
            var button = demoButtons.GetArrayElementAtIndex(index).objectReferenceValue as Button;
            if (button == null)
            {
                summary = $"demo save button {index} unassigned";
                return false;
            }
        }

        var shellSource = File.ReadAllText("Assets/Scripts/Nyangbingo/UI/TitleUiController.cs");
        if (!shellSource.Contains("SceneTransitionRequest.BeginDirect(\"MainGame\")"))
        {
            summary = "Title→MainGame transition not wired";
            return false;
        }

        var transitionSource = File.ReadAllText("Assets/Scripts/Nyangbingo/UI/SceneTransitionRequest.cs");
        if (!transitionSource.Contains("active.buildIndex") ||
            !transitionSource.Contains("BeginDirectTitle"))
        {
            summary = "Title return transition guard missing";
            return false;
        }

        summary = "Title/Loading/MainGame build order + new game + 3 demo buttons";
        return true;
    }

    private static bool TryValidateResultScreenContract(out string summary)
    {
        var shellSource = File.ReadAllText("Assets/Scripts/Nyangbingo/UI/MainGameShellUiController.cs");
        if (!shellSource.Contains("resultGoTitleButton") ||
            !shellSource.Contains("실온") ||
            !shellSource.Contains("계속 플레이"))
        {
            summary = "result UI wiring incomplete";
            return false;
        }

        var cold = GameShellController.BuildResult(new SaveGame { sealPct = 85f });
        var cool = GameShellController.BuildResult(new SaveGame { sealPct = 50f });
        var warm = GameShellController.BuildResult(new SaveGame { sealPct = 10f });
        if (cold.RoomTemperatureCelsius != -10 || cool.RoomTemperatureCelsius != -5 ||
            warm.RoomTemperatureCelsius != 0)
        {
            summary = "RoomTemperatureCelsius seal mapping wrong";
            return false;
        }

        var shellObject = new GameObject("PrResultShellContract");
        var previousTimeScale = Time.timeScale;
        try
        {
            var shell = shellObject.AddComponent<GameShellController>();
            shell.EnterGameplay(new SaveGame { day = 1 });
            var titleRequested = false;
            shell.TitleRequested += () => titleRequested = true;
            shell.ShowResult(new SaveGame { day = 12, sealPct = 72f });
            if (shell.Screen != GameShellScreen.Result || !Mathf.Approximately(Time.timeScale, 0f))
            {
                summary = "ShowResult must pause on result screen";
                return false;
            }

            if (!shell.ContinueFromResult() || shell.Screen != GameShellScreen.Gameplay ||
                Time.timeScale <= 0f || titleRequested)
            {
                summary = "ContinueFromResult must resume gameplay without title";
                return false;
            }
        }
        finally
        {
            Time.timeScale = previousTimeScale;
            UnityEngine.Object.DestroyImmediate(shellObject);
        }

        summary = "실온 3단 + ShowResult/ContinueFromResult";
        return true;
    }
}
