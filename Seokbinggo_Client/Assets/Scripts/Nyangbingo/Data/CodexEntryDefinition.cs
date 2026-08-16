using UnityEngine;

namespace Nyangbingo.Data
{
    [CreateAssetMenu(menuName = "Nyangbingo/Data/Codex Entry")]
    public sealed class CodexEntryDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string kind;
        [SerializeField] private string displayName;
        [SerializeField] private string source;
        [SerializeField] private string sourceVerification;
        [SerializeField] private string cardFrontAssetId;
        [TextArea][SerializeField] private string cardBackText;
        [SerializeField] private string portraitAssetId;
        [TextArea][SerializeField] private string note;
        public string Id => id;
        public string Kind => kind ?? string.Empty;
        public string DisplayName => displayName ?? string.Empty;
        public string Source => source ?? string.Empty;
        public string SourceVerification => sourceVerification ?? string.Empty;
        public string CardFrontAssetId => cardFrontAssetId ?? string.Empty;
        public string CardBackText => cardBackText ?? string.Empty;
        public string PortraitAssetId => portraitAssetId ?? string.Empty;
        public string Note => note ?? string.Empty;
    }
}
