using Nyangbingo.Core;
using UnityEngine;

namespace Nyangbingo.Data
{
    [CreateAssetMenu(menuName = "Nyangbingo/Data/Yokai")]
    public sealed class YokaiDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private YokaiKind kind;
        [SerializeField] private int hitPoints;
        [SerializeField] private float moveSpeed;
        [SerializeField] private int contactDamage;
        [SerializeField] private float wallDamagePerSecond;
        [SerializeField] private ItemDefinition tearItem;
        [Min(0)][SerializeField] private int tearDrop;
        [SerializeField] private ItemDefinition signatureItem;
        [Range(0f, 1f)][SerializeField] private float signatureChance;
        [SerializeField] private ItemAmount[] drops;
        public string Id => id;
        public YokaiKind Kind => kind;
        public int HitPoints => hitPoints;
        public float MoveSpeed => moveSpeed;
        public int ContactDamage => contactDamage;
        public float WallDamagePerSecond => wallDamagePerSecond;
        public ItemDefinition TearItem => tearItem;
        public int TearDrop => tearDrop;
        public ItemDefinition SignatureItem => signatureItem;
        public float SignatureChance => signatureChance;
        public ItemAmount[] Drops => drops ?? System.Array.Empty<ItemAmount>();

        public static YokaiDefinition CreateRuntime(YokaiKind value, int hp, float speed, int contact, float wallDps, ItemAmount[] loot)
        {
            var definition = CreateInstance<YokaiDefinition>();
            definition.id = value.ToString();
            definition.kind = value; definition.hitPoints = hp; definition.moveSpeed = speed;
            definition.contactDamage = contact; definition.wallDamagePerSecond = wallDps; definition.drops = loot;
            return definition;
        }
    }

}
