using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nyangbingo.Data
{
    [Serializable]
    public struct TalismanMaterial { public string itemId; public int amount; }

    [CreateAssetMenu(menuName = "Nyangbingo/Data/Talisman")]
    public sealed class TalismanDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private string form;
        [SerializeField] private string stationId;
        [SerializeField] private TalismanMaterial[] materials = Array.Empty<TalismanMaterial>();
        [TextArea][SerializeField] private string effect;
        [TextArea][SerializeField] private string rationale;
        [TextArea][SerializeField] private string note;
        public string Id => id;
        public string DisplayName => displayName ?? string.Empty;
        public string Form => form ?? string.Empty;
        public string StationId => stationId ?? string.Empty;
        public IReadOnlyList<TalismanMaterial> Materials => materials ?? Array.Empty<TalismanMaterial>();
        public string Effect => effect ?? string.Empty;
        public string Rationale => rationale ?? string.Empty;
        public string Note => note ?? string.Empty;
    }
}
