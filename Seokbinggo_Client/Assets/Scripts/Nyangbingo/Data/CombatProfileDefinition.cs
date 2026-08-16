using UnityEngine;

namespace Nyangbingo.Data
{
    [CreateAssetMenu(menuName = "Nyangbingo/Data/Combat Profile")]
    public sealed class CombatProfileDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string tier;
        [SerializeField] private bool hasBasicAttack = true;
        [Min(0f)][SerializeField] private float attackDamage;
        [Min(0f)][SerializeField] private float attacksPerSecond;
        [Min(0f)][SerializeField] private float damagePerSecond;
        [Min(0f)][SerializeField] private float knockbackTiles;
        [Min(0f)][SerializeField] private float rangeTiles;
        [Range(0f, 180f)][SerializeField] private float arcDegrees = 90f;
        [Min(1)][SerializeField] private int maxTargets = 1;
        [SerializeField] private bool hitsWalls;
        [TextArea][SerializeField] private string note;

        public string Id => id;
        public string Tier => tier;
        public bool HasBasicAttack => hasBasicAttack;
        public float AttackDamage => attackDamage;
        public float AttacksPerSecond => attacksPerSecond;
        public float DamagePerSecond => damagePerSecond;
        public float KnockbackTiles => knockbackTiles;
        public float RangeTiles => rangeTiles;
        public float ArcDegrees => arcDegrees;
        public int MaxTargets => Mathf.Max(1, maxTargets);
        public bool MultiTarget => MaxTargets > 1;
        public bool HitsWalls => hitsWalls;
        public string Note => note;

        public static CombatProfileDefinition CreateRuntime(string itemId, string profileTier, bool basicAttack,
            float damage, float attacksPerSecondValue, float dps, float knockback, float range, float arc,
            bool allowsMultipleTargets, bool allowsWallHits)
        {
            var definition = CreateInstance<CombatProfileDefinition>();
            definition.id = itemId;
            definition.tier = profileTier;
            definition.hasBasicAttack = basicAttack;
            definition.attackDamage = damage;
            definition.attacksPerSecond = attacksPerSecondValue;
            definition.damagePerSecond = dps;
            definition.knockbackTiles = knockback;
            definition.rangeTiles = range;
            definition.arcDegrees = arc;
            definition.maxTargets = allowsMultipleTargets ? int.MaxValue : 1;
            definition.hitsWalls = allowsWallHits;
            definition.note = string.Empty;
            return definition;
        }
    }
}
