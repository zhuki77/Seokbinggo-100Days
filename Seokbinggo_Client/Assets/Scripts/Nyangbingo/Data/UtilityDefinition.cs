using Nyangbingo.Core;
using UnityEngine;

namespace Nyangbingo.Data
{
    [CreateAssetMenu(menuName = "Nyangbingo/Data/Utility")]
    public sealed class UtilityDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private UtilityKind kind;
        [SerializeField] private float cooldownSeconds;
        [SerializeField] private float value;
        [SerializeField] private bool consumable;
        public string Id => id;
        public UtilityKind Kind => kind;
        public float CooldownSeconds => cooldownSeconds;
        public float Value => value;
        public bool Consumable => consumable;
        public static UtilityDefinition CreateRuntime(UtilityKind utilityKind, float effectValue)
        {
            var definition = CreateInstance<UtilityDefinition>(); definition.kind = utilityKind; definition.value = effectValue; return definition;
        }
    }
}
