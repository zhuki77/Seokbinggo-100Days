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
        [SerializeField] private float visionRadiusBonus;
        [SerializeField] private bool blocksInventoryTheft;
        public string Id => id;
        public EquipmentSlot Slot => slot;
        public bool IsAccessory => accessory;
        public int Defense => defense;
        public float MovementBonus => movementBonus;
        public float MiningCriticalBonus => miningCriticalBonus;
        public float TemperatureRiseModifier => temperatureRiseModifier;
        public float FireDamageModifier => fireDamageModifier;
        public bool GrantsDoubleJump => grantsDoubleJump;
        public float VisionRadiusBonus => visionRadiusBonus;
        public bool BlocksInventoryTheft => blocksInventoryTheft;

        public static EquipmentDefinition CreateRuntime(string itemId, EquipmentSlot equipmentSlot, bool isAccessory,
            int itemDefense = 0, float moveBonus = 0f, float miningCritical = 0f,
            float temperatureModifier = 0f, float fireModifier = 0f, bool doubleJump = false,
            float visionBonus = 0f, bool theftBlocked = false)
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
            definition.visionRadiusBonus = visionBonus;
            definition.blocksInventoryTheft = theftBlocked;
            return definition;
        }
    }
}
