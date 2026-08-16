using UnityEngine;

namespace Nyangbingo.Data
{
    [CreateAssetMenu(menuName = "Nyangbingo/Data/Crop Zone")]
    public sealed class CropDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string zoneId;
        [SerializeField] private int order;
        [SerializeField] private string cropId;
        [SerializeField] private string displayName;
        [SerializeField] private int spawnPerHundredTiles;
        [SerializeField] private int healHitPoints;
        [SerializeField] private int respawnDays;
        [SerializeField] private bool plantable;
        [TextArea][SerializeField] private string riskNote;
        [TextArea][SerializeField] private string note;
        public string Id => id;
        public string ZoneId => zoneId ?? string.Empty;
        public int Order => order;
        public string CropId => cropId ?? string.Empty;
        public string DisplayName => displayName ?? string.Empty;
        public int SpawnPerHundredTiles => spawnPerHundredTiles;
        public int HealHitPoints => healHitPoints;
        public int RespawnDays => respawnDays;
        public bool Plantable => plantable;
        public string RiskNote => riskNote ?? string.Empty;
        public string Note => note ?? string.Empty;
    }
}
