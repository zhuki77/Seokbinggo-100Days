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
        [Header("Surface day/night layers")]
        [SerializeField] private Sprite daySky;
        [SerializeField] private Sprite dayRearClouds;
        [SerializeField] private Sprite dayMountains;
        [SerializeField] private Sprite dayFrontClouds;
        [SerializeField] private Sprite sun;
        [SerializeField] private Sprite nightSky;
        [SerializeField] private Sprite nightRearClouds;
        [SerializeField] private Sprite nightMountains;
        [SerializeField] private Sprite nightFrontClouds;
        [SerializeField] private Sprite moon;
        [SerializeField] private Sprite titleBackground;
        [SerializeField] private Sprite[] titleFrames = Array.Empty<Sprite>();

        public Sprite DistantView => distantView;
        public Sprite Clouds => clouds;
        public Sprite Underground => underground;
        public Sprite DaySky => daySky;
        public Sprite DayRearClouds => dayRearClouds;
        public Sprite DayMountains => dayMountains;
        public Sprite DayFrontClouds => dayFrontClouds;
        public Sprite Sun => sun;
        public Sprite NightSky => nightSky;
        public Sprite NightRearClouds => nightRearClouds;
        public Sprite NightMountains => nightMountains;
        public Sprite NightFrontClouds => nightFrontClouds;
        public Sprite Moon => moon;
        public bool HasDayNightSurfaceSet => daySky != null && dayRearClouds != null && dayMountains != null &&
                                             dayFrontClouds != null && sun != null && nightSky != null &&
                                             nightRearClouds != null && nightMountains != null &&
                                             nightFrontClouds != null && moon != null;
        public Sprite TitleBackground => titleBackground;
        public IReadOnlyList<Sprite> DayCounterScrollFrames => titleFrames ?? Array.Empty<Sprite>();
        // Serialized under the original delivery name for asset compatibility.
        public IReadOnlyList<Sprite> TitleFrames => titleFrames ?? Array.Empty<Sprite>();
    }
}
