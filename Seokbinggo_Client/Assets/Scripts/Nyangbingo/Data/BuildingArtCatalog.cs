using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nyangbingo.Data
{
    [CreateAssetMenu(menuName = "Nyangbingo/Data/Building Art Catalog")]
    public sealed class BuildingArtCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string id;
            [SerializeField] private Sprite[] frames = Array.Empty<Sprite>();

            public string Id => id;
            public IReadOnlyList<Sprite> Frames => frames ?? Array.Empty<Sprite>();
            public Sprite Sprite => Frames.Count > 0 ? Frames[0] : null;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public Entry Find(string id)
        {
            if (string.IsNullOrEmpty(id) || entries == null) return null;
            foreach (var entry in entries)
                if (entry != null && string.Equals(entry.Id, id, StringComparison.Ordinal)) return entry;
            return null;
        }
    }
}
