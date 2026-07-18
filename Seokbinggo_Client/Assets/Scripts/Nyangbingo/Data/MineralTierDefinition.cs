using UnityEngine;

namespace Nyangbingo.Data
{
    public enum MineralLayer
    {
        SurfaceNight,
        SurfaceRuinNight,
        UndergroundUpper,
        UndergroundMiddle,
        UndergroundDeep
    }

    public enum MiningGateType
    {
        None,
        Soft,
        Hard
    }

    [CreateAssetMenu(menuName = "Nyangbingo/Data/Mineral Tier")]
    public sealed class MineralTierDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private ItemDefinition resource;
        [SerializeField] private MineralLayer layer;
        [Min(0)][SerializeField] private int minimumDepth;
        [Min(0)][SerializeField] private int maximumDepth;
        [Range(1, 3)][SerializeField] private int minimumClawTier = 1;
        [SerializeField] private MiningGateType gateType;
        [SerializeField] private float clawTierOneSeconds;
        [SerializeField] private float clawTierTwoSeconds;
        [SerializeField] private float clawTierThreeSeconds;
        [Min(0f)][SerializeField] private float frequencyPerHundredTiles;
        [TextArea][SerializeField] private string usageDescription;
        [TextArea][SerializeField] private string gateDescription;

        public string Id => id;
        public string DisplayName => displayName;
        public ItemDefinition Resource => resource;
        public MineralLayer Layer => layer;
        public int MinimumDepth => minimumDepth;
        public int MaximumDepth => maximumDepth;
        public int MinimumClawTier => minimumClawTier;
        public MiningGateType GateType => gateType;
        public float ClawTierOneSeconds => clawTierOneSeconds;
        public float ClawTierTwoSeconds => clawTierTwoSeconds;
        public float ClawTierThreeSeconds => clawTierThreeSeconds;
        public float FrequencyPerHundredTiles => frequencyPerHundredTiles;
        public string UsageDescription => usageDescription;
        public string GateDescription => gateDescription;

        public float MiningSecondsForClawTier(int clawTier)
        {
            if (clawTier < minimumClawTier || clawTier < 1 || clawTier > 3) return -1f;
            var seconds = clawTier == 1 ? clawTierOneSeconds :
                clawTier == 2 ? clawTierTwoSeconds : clawTierThreeSeconds;
            return IsFinitePositive(seconds) ? seconds : -1f;
        }

        public bool CanMineWithClawTier(int clawTier) => MiningSecondsForClawTier(clawTier) > 0f;

        private static bool IsFinitePositive(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
