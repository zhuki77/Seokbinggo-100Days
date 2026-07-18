using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nyangbingo.Data
{
    [CreateAssetMenu(menuName = "Nyangbingo/Data/Gameplay Art Catalog")]
    public sealed class GameplayArtCatalog : ScriptableObject
    {
        [SerializeField] private Sprite[] temperatureFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] playerAttackFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] napFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] miningCrackFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] bossWarningFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] blueProjectileFrames = Array.Empty<Sprite>();

        public IReadOnlyList<Sprite> TemperatureFrames => temperatureFrames ?? Array.Empty<Sprite>();
        public IReadOnlyList<Sprite> PlayerAttackFrames => playerAttackFrames ?? Array.Empty<Sprite>();
        public IReadOnlyList<Sprite> NapFrames => napFrames ?? Array.Empty<Sprite>();
        public IReadOnlyList<Sprite> MiningCrackFrames => miningCrackFrames ?? Array.Empty<Sprite>();
        public IReadOnlyList<Sprite> BossWarningFrames => bossWarningFrames ?? Array.Empty<Sprite>();
        public IReadOnlyList<Sprite> BlueProjectileFrames => blueProjectileFrames ?? Array.Empty<Sprite>();
    }
}
