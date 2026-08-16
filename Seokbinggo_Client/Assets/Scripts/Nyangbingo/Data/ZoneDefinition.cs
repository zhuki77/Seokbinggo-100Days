using UnityEngine;

namespace Nyangbingo.Data
{
    [CreateAssetMenu(menuName = "Nyangbingo/Data/Zone")]
    public sealed class ZoneDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private int order;
        [SerializeField] private int distanceTilesFrom;
        [SerializeField] private int distanceTilesTo;
        [SerializeField] private float distanceNormalizedFrom;
        [SerializeField] private float distanceNormalizedTo;
        [SerializeField] private int tier;
        [SerializeField] private string altarId;
        [SerializeField] private string bossId;
        [SerializeField] private string bossDisplayName;
        [SerializeField] private string bossDay;
        [SerializeField] private int bossHitPoints;
        [SerializeField] private string bossSummonType;
        [SerializeField] private float treeDensityMultiplier;
        [TextArea][SerializeField] private string note;
        [SerializeField] private int heatStage;
        [SerializeField] private string gateRole;
        public string Id => id;
        public int Order => order;
        public int DistanceTilesFrom => distanceTilesFrom;
        public int DistanceTilesTo => distanceTilesTo;
        public float DistanceNormalizedFrom => distanceNormalizedFrom;
        public float DistanceNormalizedTo => distanceNormalizedTo;
        public int Tier => tier;
        public string AltarId => altarId ?? string.Empty;
        public string BossId => bossId ?? string.Empty;
        public string BossDisplayName => bossDisplayName ?? string.Empty;
        public string BossDay => bossDay ?? string.Empty;
        public int BossHitPoints => bossHitPoints;
        public string BossSummonType => bossSummonType ?? string.Empty;
        public float TreeDensityMultiplier => treeDensityMultiplier;
        public string Note => note ?? string.Empty;
        public int HeatStage => heatStage;
        public string GateRole => gateRole ?? string.Empty;
    }
}
