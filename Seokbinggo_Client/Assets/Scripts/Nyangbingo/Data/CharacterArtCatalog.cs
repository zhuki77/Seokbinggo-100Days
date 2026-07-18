using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nyangbingo.Data
{
    [CreateAssetMenu(menuName = "Nyangbingo/Data/Character Art Catalog")]
    public sealed class CharacterArtCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string id;
            [SerializeField] private Sprite sprite;
            [SerializeField] private Sprite[] idleFrames = Array.Empty<Sprite>();
            [SerializeField] private Sprite[] walkFrames = Array.Empty<Sprite>();
            [SerializeField] private Sprite[] attackFrames = Array.Empty<Sprite>();
            [SerializeField] private Sprite[] hitFrames = Array.Empty<Sprite>();
            [SerializeField] private Sprite[] fleeFrames = Array.Empty<Sprite>();
            [SerializeField] private Sprite[] specialFrames = Array.Empty<Sprite>();
            [SerializeField] private bool sourceFacesRight;

            public string Id => id;
            public Sprite Sprite => sprite;
            public IReadOnlyList<Sprite> IdleFrames => idleFrames ?? Array.Empty<Sprite>();
            public IReadOnlyList<Sprite> WalkFrames => walkFrames ?? Array.Empty<Sprite>();
            public IReadOnlyList<Sprite> AttackFrames => attackFrames ?? Array.Empty<Sprite>();
            public IReadOnlyList<Sprite> HitFrames => hitFrames ?? Array.Empty<Sprite>();
            public IReadOnlyList<Sprite> FleeFrames => fleeFrames ?? Array.Empty<Sprite>();
            public IReadOnlyList<Sprite> SpecialFrames => specialFrames ?? Array.Empty<Sprite>();
            public bool SourceFacesRight => sourceFacesRight;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        private Dictionary<string, Entry> entryById;

        public Entry Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureIndex();
            return entryById.TryGetValue(id, out var entry) ? entry : null;
        }

        public Sprite FindSprite(string id)
        {
            return Find(id)?.Sprite;
        }

        private void OnEnable() => entryById = null;

        private void OnValidate() => entryById = null;

        private void EnsureIndex()
        {
            if (entryById != null) return;
            entryById = new Dictionary<string, Entry>(StringComparer.Ordinal);
            foreach (var entry in entries ?? Array.Empty<Entry>())
            {
                if (entry == null || string.IsNullOrEmpty(entry.Id) || entry.Sprite == null) continue;
                entryById[entry.Id] = entry;
            }
        }
    }
}
