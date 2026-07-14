using Nyangbingo.Core;
using UnityEngine;

namespace Nyangbingo.Data
{
    [CreateAssetMenu(menuName = "Nyangbingo/Data/Boss")]
    public sealed class BossDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private YokaiKind yokaiKind;
        [SerializeField] private ItemDefinition summonItem;
        [SerializeField] private ItemAmount[] guaranteedDrops;
        public string Id => id;
        public YokaiKind YokaiKind => yokaiKind;
        public ItemDefinition SummonItem => summonItem;
        public ItemAmount[] GuaranteedDrops => guaranteedDrops;

        public static BossDefinition CreateRuntime(string bossId, YokaiKind kind, ItemDefinition summon, ItemAmount[] rewards)
        {
            var definition = CreateInstance<BossDefinition>();
            definition.id = bossId; definition.yokaiKind = kind; definition.summonItem = summon; definition.guaranteedDrops = rewards;
            return definition;
        }
    }
}
