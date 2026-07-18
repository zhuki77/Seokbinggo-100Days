using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nyangbingo.Data
{
    [CreateAssetMenu(menuName = "Nyangbingo/Data/Environment Art Catalog")]
    public sealed class EnvironmentArtCatalog : ScriptableObject
    {
        [SerializeField] private Sprite distantView;
        [SerializeField] private Sprite clouds;
        [SerializeField] private Sprite underground;
        [SerializeField] private Sprite[] titleFrames = Array.Empty<Sprite>();

        public Sprite DistantView => distantView;
        public Sprite Clouds => clouds;
        public Sprite Underground => underground;
        public IReadOnlyList<Sprite> TitleFrames => titleFrames ?? Array.Empty<Sprite>();
    }
}
