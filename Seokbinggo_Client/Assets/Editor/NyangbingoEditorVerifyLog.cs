using UnityEngine;

/// <summary>
/// 에디터 메뉴 검증·임포트 결과를 콘솔에서 한눈에 구분하기 위한 공통 로그 형식.
/// </summary>
internal static class NyangbingoEditorVerifyLog
{
    internal const string PassTag = "[PASS]";
    internal const string FailTag = "[FAIL]";

    internal static void Pass(string menuName, string detail = null)
    {
        var message = string.IsNullOrWhiteSpace(detail)
            ? $"[Nyangbingo] {PassTag} {menuName}"
            : $"[Nyangbingo] {PassTag} {menuName}: {detail}";
        Debug.Log(message);
    }

    internal static void Fail(string menuName, string detail)
    {
        Debug.LogError($"[Nyangbingo] {FailTag} {menuName}: {detail}");
    }

    internal static void SummaryHeader() =>
        Debug.Log("[Nyangbingo] ===== 제품 검증 체크리스트 =====");

    internal static void SummaryLine(string name, bool passed, string detail = null)
    {
        var tag = passed ? PassTag : FailTag;
        var message = string.IsNullOrWhiteSpace(detail)
            ? $"[Nyangbingo] {tag} {name}"
            : $"[Nyangbingo] {tag} {name} — {detail}";
        if (passed) Debug.Log(message);
        else Debug.LogError(message);
    }

    internal static void SummaryFooter(bool allPassed)
    {
        if (allPassed)
            Debug.Log("[Nyangbingo] ===== 체크리스트 전체 통과 =====");
        else
            Debug.LogError("[Nyangbingo] ===== 체크리스트 실패 — 위 [FAIL] 항목을 확인하세요 =====");
    }
}
