using Nyangbingo.Data;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>
    /// v41/v46 기능 가구(죽부인·대발) 오라 수치. globals 키를 우선하고 없으면 정본 기본값을 쓴다.
    /// </summary>
    public static class FurnitureAuraService
    {
        public const float DefaultJukbuinRegenMult = 1.5f;
        public const float DefaultDaebalFlameCut = 0.25f;

        /// <summary>죽부인 보금자리 반경 내이면 자연재생 배수, 아니면 1.</summary>
        public static float RegenMultiplier(bool nearJukbuin, GlobalSettings settings = null)
        {
            if (!nearJukbuin) return 1f;
            if (settings != null &&
                settings.TryGetFloat(GlobalKeys.JukbuinRegenMult, out var mult) &&
                !float.IsNaN(mult) && !float.IsInfinity(mult) && mult > 0f)
                return mult;
            return DefaultJukbuinRegenMult;
        }

        /// <summary>대발·해태 화염 경감 중 최댓값(0~1 cut).</summary>
        public static float FlameCut(bool daebalInRange, float haetaeCut, GlobalSettings settings = null)
        {
            var daebalCut = 0f;
            if (daebalInRange)
            {
                if (settings != null &&
                    settings.TryGetFloat(GlobalKeys.DaebalFlameCut, out var cut) &&
                    !float.IsNaN(cut) && !float.IsInfinity(cut) && cut >= 0f)
                    daebalCut = cut;
                else
                    daebalCut = DefaultDaebalFlameCut;
            }

            var safeHaetae = float.IsNaN(haetaeCut) || float.IsInfinity(haetaeCut)
                ? 0f
                : Mathf.Max(0f, haetaeCut);
            return Mathf.Max(daebalCut, safeHaetae);
        }
    }
}
