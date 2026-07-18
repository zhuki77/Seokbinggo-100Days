using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nyangbingo.Data
{
    [CreateAssetMenu(menuName = "Nyangbingo/Data/Item Art Catalog")]
    public sealed class ItemArtCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string id;
            [SerializeField] private Sprite sprite;

            public string Id => id;
            public Sprite Sprite => sprite;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();
        private Dictionary<string, Sprite> spriteById;

        public Sprite FindSprite(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureIndex();
            return spriteById.TryGetValue(id, out var sprite) ? sprite : null;
        }

        private void OnEnable() => spriteById = null;
        private void OnValidate() => spriteById = null;

        private void EnsureIndex()
        {
            if (spriteById != null) return;
            spriteById = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            foreach (var entry in entries ?? Array.Empty<Entry>())
            {
                if (entry == null || string.IsNullOrEmpty(entry.Id) || entry.Sprite == null) continue;
                spriteById[entry.Id] = entry.Sprite;
            }
        }
    }
}
