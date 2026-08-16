using Nyangbingo.Core;
using UnityEngine;

namespace Nyangbingo.Data
{
    [CreateAssetMenu(menuName = "Nyangbingo/Data/Equipment")]
    public sealed class EquipmentDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private EquipmentSlot slot;
        [SerializeField] private bool accessory;
        [SerializeField] private int defense;
        [SerializeField] private float movementBonus;
        [SerializeField] private float miningCriticalBonus;
        [SerializeField] private float temperatureRiseModifier;
        [SerializeField] private float fireDamageModifier;
        [SerializeField] private bool grantsDoubleJump;
        [SerializeField] private float doubleJumpHeightRatio;
        [SerializeField] private float visionRadiusBonus;
        [SerializeField] private bool blocksInventoryTheft;
        [SerializeField] private string setId;
        [SerializeField] private float setTemperatureRiseModifier;
        [SerializeField] private float setFireDamageModifier;
        [SerializeField] private string verbId;
        [SerializeField] private int usageLimitPerDay;
        [SerializeField] private string activationCondition = "None";
        [SerializeField] private bool hasColdTolerance;
        [SerializeField] private int coldToleranceC;
        [TextArea][SerializeField] private string coldNote;

        public string Id => id;
        public EquipmentSlot Slot => slot;
        public bool IsAccessory => accessory;
        public int Defense => defense;
        public float MovementBonus => movementBonus;
        public float MiningCriticalBonus => miningCriticalBonus;
        public float TemperatureRiseModifier => temperatureRiseModifier;
        public float FireDamageModifier => fireDamageModifier;
        public bool GrantsDoubleJump => grantsDoubleJump;
        public float DoubleJumpHeightRatio => doubleJumpHeightRatio;
        public float VisionRadiusBonus => visionRadiusBonus;
        public bool BlocksInventoryTheft => blocksInventoryTheft;
        public string SetId => setId;
        public float SetTemperatureRiseModifier => setTemperatureRiseModifier;
        public float SetFireDamageModifier => setFireDamageModifier;
        public string VerbIdRaw => verbId ?? string.Empty;
        public int UsageLimitPerDay => usageLimitPerDay;
        public bool HasColdTolerance => hasColdTolerance;
        public int ColdToleranceC => coldToleranceC;
        public string ColdNote => coldNote ?? string.Empty;
        public string ActivationConditionRaw =>
            string.IsNullOrWhiteSpace(activationCondition) ? "None" : activationCondition;

        public ArtifactVerbId VerbId => ArtifactVerbParsing.ParseVerb(verbId);

        public ArtifactActivationCondition ActivationCondition =>
            ArtifactVerbParsing.ParseActivation(activationCondition);

        /// <summary>v72 A-3: 내한 하한 미달은 장착을 막지 않고 효율 상태만 false로 보고한다.</summary>
        public bool ColdOk(int roomTemperatureC) => !hasColdTolerance || roomTemperatureC >= coldToleranceC;

        public static EquipmentDefinition CreateRuntime(string itemId, EquipmentSlot equipmentSlot, bool isAccessory,
            int itemDefense = 0, float moveBonus = 0f, float miningCritical = 0f,
            float temperatureModifier = 0f, float fireModifier = 0f, bool doubleJump = false,
            float visionBonus = 0f, bool theftBlocked = false, float doubleJumpRatio = 0f,
            string equipmentSetId = null, float setTemperatureModifier = 0f, float setFireModifier = 0f,
            string artifactVerbId = null, int usageLimit = 0, string activation = "None",
            bool usesColdTolerance = false, int coldTolerance = 0, string coldDescription = null)
        {
            var definition = CreateInstance<EquipmentDefinition>();
            definition.id = itemId;
            definition.slot = equipmentSlot;
            definition.accessory = isAccessory;
            definition.defense = itemDefense;
            definition.movementBonus = moveBonus;
            definition.miningCriticalBonus = miningCritical;
            definition.temperatureRiseModifier = temperatureModifier;
            definition.fireDamageModifier = fireModifier;
            definition.grantsDoubleJump = doubleJump;
            definition.doubleJumpHeightRatio = doubleJumpRatio;
            definition.visionRadiusBonus = visionBonus;
            definition.blocksInventoryTheft = theftBlocked;
            definition.setId = equipmentSetId ?? string.Empty;
            definition.setTemperatureRiseModifier = setTemperatureModifier;
            definition.setFireDamageModifier = setFireModifier;
            definition.verbId = artifactVerbId ?? string.Empty;
            definition.usageLimitPerDay = System.Math.Max(0, usageLimit);
            definition.activationCondition = string.IsNullOrWhiteSpace(activation) ? "None" : activation;
            definition.hasColdTolerance = usesColdTolerance;
            definition.coldToleranceC = coldTolerance;
            definition.coldNote = coldDescription ?? string.Empty;
            return definition;
        }
    }
}
