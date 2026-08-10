using System;

namespace Nyangbingo.Core
{
    /// <summary>
    /// v46 아티팩트 동사. CSV <c>verb_id</c>와 enum 이름이 글자 그대로 같다(PascalCase).
    /// </summary>
    public enum ArtifactVerbId
    {
        None = 0,
        GrabKnockedTarget,
        HearIronVein,
        ExtendCoolerRadius,
        NoHeatInShade,
        ReduceFlameAndHasten,
        NoFirstStrike,
        WalkWhileCharging,
        MaintainAfterShutdown,
        EscapeOnSwallow,
        RelocateColdCore,
        HalveClayCraftTime,
        TurnWhileSliding,
        ExtendMagpieRadius,
        OpenStorageAnywhere,
        BonusTearOnCodex,
        FullDemolitionRecovery,
        IncreaseVisionDeep,
        ReduceFlameTag,
        ReduceOfferTears,
        ShowDugPaths
    }

    /// <summary>아티팩트 발동 조건. CSV <c>activation_condition</c>과 동일.</summary>
    public enum ArtifactActivationCondition
    {
        None = 0,
        Deep,
        Surface,
        DaySurface
    }

    public static class ArtifactVerbParsing
    {
        public static ArtifactVerbId ParseVerb(string verbId)
        {
            if (string.IsNullOrWhiteSpace(verbId)) return ArtifactVerbId.None;
            if (Enum.TryParse(verbId, false, out ArtifactVerbId parsed) &&
                Enum.IsDefined(typeof(ArtifactVerbId), parsed))
                return parsed;
            UnityEngine.Debug.LogError($"[Nyangbingo] 알 수 없는 verb_id: {verbId} — None으로 처리");
            return ArtifactVerbId.None;
        }

        public static ArtifactActivationCondition ParseActivation(string condition)
        {
            if (string.IsNullOrWhiteSpace(condition) ||
                string.Equals(condition, "None", StringComparison.OrdinalIgnoreCase))
                return ArtifactActivationCondition.None;
            if (Enum.TryParse(condition, true, out ArtifactActivationCondition parsed) &&
                Enum.IsDefined(typeof(ArtifactActivationCondition), parsed))
                return parsed;
            UnityEngine.Debug.LogError(
                $"[Nyangbingo] 알 수 없는 activation_condition: {condition} — None으로 처리");
            return ArtifactActivationCondition.None;
        }
    }
}
