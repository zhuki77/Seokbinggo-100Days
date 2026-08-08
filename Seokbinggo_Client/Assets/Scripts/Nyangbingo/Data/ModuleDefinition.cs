using System;
using UnityEngine;

namespace Nyangbingo.Data
{
    public enum ModulePriority
    {
        P0,
        P1,
        P2
    }

    [CreateAssetMenu(menuName = "Nyangbingo/Data/Module")]
    public sealed class ModuleDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private ItemDefinition item;
        [TextArea][SerializeField] private string role;
        [SerializeField] private ItemAmount[] materials = Array.Empty<ItemAmount>();
        [Min(0f)][SerializeField] private float buildTimeSeconds;
        [SerializeField] private ModulePriority priority;

        public string Id => id;
        public string DisplayName => displayName;
        public ItemDefinition Item => item;
        public string Role => role;
        public ItemAmount[] Materials => materials ?? Array.Empty<ItemAmount>();
        public float BuildTimeSeconds => buildTimeSeconds;
        public ModulePriority Priority => priority;

        public static ModuleDefinition CreateRuntime(string moduleId, string name, ItemDefinition moduleItem,
            string moduleRole, ItemAmount[] requiredMaterials, float buildSeconds, ModulePriority modulePriority)
        {
            var definition = CreateInstance<ModuleDefinition>();
            definition.id = moduleId;
            definition.displayName = name;
            definition.item = moduleItem;
            definition.role = moduleRole;
            definition.materials = requiredMaterials ?? Array.Empty<ItemAmount>();
            definition.buildTimeSeconds = buildSeconds;
            definition.priority = modulePriority;
            return definition;
        }
    }
}
