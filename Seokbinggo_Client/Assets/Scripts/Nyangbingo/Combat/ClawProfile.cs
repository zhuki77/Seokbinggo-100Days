using UnityEngine;

namespace Nyangbingo.Combat
{
    [CreateAssetMenu(menuName = "Nyangbingo/Data/Claw Profile")]
    public sealed class ClawProfile : ScriptableObject
    {
        [Range(1, 3)][SerializeField] private int tier = 1;
        [SerializeField] private int damage = 5;
        [SerializeField] private float knockback = .5f;
        [Range(0f, 1f)][SerializeField] private float miningCriticalChance;
        [Range(0f, 1f)][SerializeField] private float frostSlowMultiplier = 1f;
        [SerializeField] private float frostSlowDuration;
        [SerializeField] private float verticalReach = 1f;
        public int Tier => tier;
        public int Damage => damage;
        public float Knockback => knockback;
        public float MiningCriticalChance => miningCriticalChance;
        public float FrostSlowMultiplier => frostSlowMultiplier;
        public float FrostSlowDuration => frostSlowDuration;
        public float VerticalReach => verticalReach;
    }
}
