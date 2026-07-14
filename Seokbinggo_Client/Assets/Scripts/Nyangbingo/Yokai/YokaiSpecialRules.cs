using Nyangbingo.Core;

namespace Nyangbingo.Yokai
{
    // World/installation systems implement these checks; Yokai AI never references art or scene objects directly.
    public interface IYokaiCounterSource
    {
        bool IsInLanternRange { get; }
        bool IsInSieveRange { get; }
        bool HasGroundLoot { get; }
    }

    public static class YokaiSpecialRules
    {
        public static float DamageTakenMultiplier(YokaiKind kind, IYokaiCounterSource counters)
        {
            if (kind == YokaiKind.Eoduksini && counters != null && counters.IsInLanternRange) return 2f;
            if (kind == YokaiKind.Yagwanggwi && counters != null && counters.IsInSieveRange) return 1.5f;
            return 1f;
        }

        public static bool ShouldStealGroundLoot(YokaiKind kind, IYokaiCounterSource counters)
            => kind == YokaiKind.Yagwanggwi && counters != null && counters.HasGroundLoot && !counters.IsInSieveRange;

        public static bool IsFireImmuneWall(YokaiKind kind, bool isIronHeatWall)
            => kind == YokaiKind.Bulgasari && isIronHeatWall;
    }
}
