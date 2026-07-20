using System;
using Nyangbingo.Core;
using UnityEngine;

namespace Nyangbingo.Data
{
    [Serializable]
    public struct DayCurveSpawnAmount
    {
        public YokaiKind kind;
        [Min(1)] public int amount;
    }

    [CreateAssetMenu(menuName = "Nyangbingo/Data/Day Curve")]
    public sealed class DayCurveDefinition : ScriptableObject
    {
        [Min(1)][SerializeField] private int day;
        [Range(1, 3)][SerializeField] private int heatStage;
        [Min(0f)][SerializeField] private float dayFireDamagePerSecond;
        [Min(0)][SerializeField] private int nightYokaiCount;
        [Min(0f)][SerializeField] private float yokaiWallDamage;
        [Range(0f, 100f)][SerializeField] private float paceSealPercent;
        [Range(1, 3)][SerializeField] private int paceMineralTier;
        [Min(0)][SerializeField] private int maxActive;
        [SerializeField] private DayCurveSpawnAmount[] spawnComposition =
            Array.Empty<DayCurveSpawnAmount>();
        [Min(0f)][SerializeField] private float spawnMultiplier = 1f;
        [Min(0f)][SerializeField] private float dropMultiplier = 1f;
        [SerializeField] private string eventId;

        public string Id => day.ToString(System.Globalization.CultureInfo.InvariantCulture);
        public int Day => day;
        public int HeatStage => heatStage;
        public float DayFireDamagePerSecond => dayFireDamagePerSecond;
        public int NightYokaiCount => nightYokaiCount;
        public float YokaiWallDamage => yokaiWallDamage;
        public float PaceSealPercent => paceSealPercent;
        public int PaceMineralTier => paceMineralTier;
        public int MaxActive => maxActive;
        public DayCurveSpawnAmount[] SpawnComposition =>
            spawnComposition ?? Array.Empty<DayCurveSpawnAmount>();
        public float SpawnMultiplier => spawnMultiplier;
        public float DropMultiplier => dropMultiplier;
        public string EventId => eventId ?? string.Empty;

        public int EffectiveSpawnCount
        {
            get
            {
                var total = 0;
                var composition = SpawnComposition;
                for (var i = 0; i < composition.Length; i++) total += Math.Max(0, composition[i].amount);
                return total;
            }
        }

        public int SpawnAmount(YokaiKind kind)
        {
            var composition = SpawnComposition;
            for (var i = 0; i < composition.Length; i++)
                if (composition[i].kind == kind) return Math.Max(0, composition[i].amount);
            return 0;
        }
    }
}
