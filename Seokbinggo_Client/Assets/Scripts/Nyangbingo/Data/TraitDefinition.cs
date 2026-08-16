using UnityEngine;

namespace Nyangbingo.Data
{
    [CreateAssetMenu(menuName = "Nyangbingo/Data/Starting Trait")]
    public sealed class TraitDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private string shortName;
        [SerializeField] private string hookField;
        [SerializeField] private string hookSource;
        [SerializeField] private string startItemId;
        [TextArea][SerializeField] private string effect;
        [SerializeField] private string artAssetId;
        [TextArea][SerializeField] private string note;
        public string Id => id;
        public string DisplayName => displayName ?? string.Empty;
        public string ShortName => shortName ?? string.Empty;
        public string HookField => hookField ?? string.Empty;
        public string HookSource => hookSource ?? string.Empty;
        public string StartItemId => startItemId ?? string.Empty;
        public string Effect => effect ?? string.Empty;
        public string ArtAssetId => artAssetId ?? string.Empty;
        public string Note => note ?? string.Empty;
    }
}
