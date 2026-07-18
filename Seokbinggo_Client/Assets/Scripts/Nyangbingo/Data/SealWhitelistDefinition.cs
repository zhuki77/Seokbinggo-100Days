using UnityEngine;

namespace Nyangbingo.Data
{
    [CreateAssetMenu(menuName = "Nyangbingo/Data/Seal Whitelist Rule")]
    public sealed class SealWhitelistDefinition : ScriptableObject
    {
        [SerializeField] private string element;
        [SerializeField] private bool seals;
        [TextArea][SerializeField] private string note;

        public string Element => element;
        public bool Seals => seals;
        public string Note => note;
    }
}
