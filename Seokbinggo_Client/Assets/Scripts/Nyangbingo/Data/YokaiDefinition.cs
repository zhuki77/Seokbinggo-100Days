using Nyangbingo.Core;
using UnityEngine;

namespace Nyangbingo.Data
{
    [CreateAssetMenu(menuName = "Nyangbingo/Data/Yokai")]
    public sealed class YokaiDefinition : ScriptableObject
    {
        [SerializeField] private YokaiKind kind;
        [SerializeField] private int hitPoints;
        [SerializeField] private float moveSpeed;
        [SerializeField] private int contactDamage;
        [SerializeField] private float wallDamagePerSecond;
        [SerializeField] private ItemAmount[] drops;
        public YokaiKind Kind => kind;
        public int HitPoints => hitPoints;
        public float MoveSpeed => moveSpeed;
        public int ContactDamage => contactDamage;
        public float WallDamagePerSecond => wallDamagePerSecond;
        public ItemAmount[] Drops => drops;

        public static YokaiDefinition CreateRuntime(YokaiKind value, int hp, float speed, int contact, float wallDps, ItemAmount[] loot)
        {
            var definition = CreateInstance<YokaiDefinition>();
            definition.kind = value; definition.hitPoints = hp; definition.moveSpeed = speed;
            definition.contactDamage = contact; definition.wallDamagePerSecond = wallDps; definition.drops = loot;
            return definition;
        }
    }
}
