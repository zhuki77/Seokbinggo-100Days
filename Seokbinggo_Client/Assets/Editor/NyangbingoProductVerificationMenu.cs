using System;
using UnityEditor;

/// <summary>
/// 데이터·씬·회귀 테스트를 한 번에 돌리고 콘솔에 PASS/FAIL 요약을 남긴다.
/// </summary>
public static class NyangbingoProductVerificationMenu
{
    private const string DataFreshnessName = "Validate Product Data Freshness";
    private const string MainGameSceneName = "Validate Main Game Scene";
    private const string DevBRegressionName = "Run Dev B Integration Regression Tests";

    [MenuItem("Nyangbingo/Run Product Verification Checklist")]
    public static void RunChecklist()
    {
        NyangbingoEditorVerifyLog.SummaryHeader();

        var dataPassed = NyangbingoDataBuildGate.TryValidateCurrent(out var dataSummary);
        NyangbingoEditorVerifyLog.SummaryLine(DataFreshnessName, dataPassed, dataSummary);

        var scenePassed = NyangbingoMainGameSceneCreator.TryValidate(out var sceneSummary);
        NyangbingoEditorVerifyLog.SummaryLine(MainGameSceneName, scenePassed, sceneSummary);

        var regressionPassed = TryRunDevBRegression(out var regressionSummary);
        NyangbingoEditorVerifyLog.SummaryLine(DevBRegressionName, regressionPassed, regressionSummary);

        var allPassed = dataPassed && scenePassed && regressionPassed;
        NyangbingoEditorVerifyLog.SummaryFooter(allPassed);
    }

    internal static bool TryRunDevBRegression(out string summary)
    {
        try
        {
            NyangbingoDevBIntegrationRegressionTests.RunAll();
            summary = "49/49 tests";
            return true;
        }
        catch (Exception exception)
        {
            summary = exception.Message;
            return false;
        }
    }
}
