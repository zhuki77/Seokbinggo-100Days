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
        public string Id => id;
        public EquipmentSlot Slot => slot;
        public bool IsAccessory => accessory;
        public int Defense => defense;
    }
}
