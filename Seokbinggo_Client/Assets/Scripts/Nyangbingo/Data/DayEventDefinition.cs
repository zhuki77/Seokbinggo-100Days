using System;
using Nyangbingo.Core;
using UnityEngine;

namespace Nyangbingo.Data
{
    [Serializable]
    public struct YokaiSpawnAmount
    {
        public YokaiKind kind;
        [Min(1)] public int amount;
    }

    [CreateAssetMenu(menuName = "Nyangbingo/Data/Day Event")]
    public sealed class DayEventDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [Min(1)][SerializeField] private int day;
        [Min(1)][SerializeField] private int maxActive;
        [SerializeField] private float[] waveOffsets;
        [SerializeField] private YokaiSpawnAmount[] composition;
        [Min(0f)][SerializeField] private float tearMultiplier = 1f;
        [Min(0f)][SerializeField] private float signatureMultiplier = 1f;

        public string Id => id;
        public int Day => day;
        public int MaxActive => maxActive;
        public float[] WaveOffsets => waveOffsets ?? Array.Empty<float>();
        public YokaiSpawnAmount[] Composition => composition ?? Array.Empty<YokaiSpawnAmount>();
        public float TearMultiplier => tearMultiplier;
        public float SignatureMultiplier => signatureMultiplier;
    }
}
