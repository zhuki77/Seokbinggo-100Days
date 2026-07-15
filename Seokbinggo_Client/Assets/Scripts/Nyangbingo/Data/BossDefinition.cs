using Nyangbingo.Core;
using UnityEngine;

namespace Nyangbingo.Data
{
    [CreateAssetMenu(menuName = "Nyangbingo/Data/Boss")]
    public sealed class BossDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private BossKind kind;
        [SerializeField] private int hitPoints;
        [SerializeField] private float expectedCombatSeconds;
        [SerializeField] private ItemDefinition summonItem;
        [SerializeField] private bool requiresDeepAltar;
        [SerializeField] private int forcedDay;
        [SerializeField] private ItemAmount[] guaranteedDrops;
        public string Id => id;
        public BossKind Kind => kind;
        public int HitPoints => hitPoints;
        public float ExpectedCombatSeconds => expectedCombatSeconds;
        public ItemDefinition SummonItem => summonItem;
        public bool RequiresDeepAltar => requiresDeepAltar;
        public int ForcedDay => forcedDay;
        public ItemAmount[] GuaranteedDrops => guaranteedDrops;

        public static BossDefinition CreateRuntime(string bossId, YokaiKind kind, ItemDefinition summon, ItemAmount[] rewards)
        {
            var bossKind = BossKind.GoblinChief;
            switch (kind)
            {
                case YokaiKind.Bulgasari: bossKind = BossKind.MotherBulgasari; break;
                case YokaiKind.Gangcheori: bossKind = BossKind.Gangcheori; break;
            }

            return CreateRuntime(bossId, bossKind, summon, rewards);
        }

        public static BossDefinition CreateRuntime(string bossId, BossKind bossKind, ItemDefinition summon, ItemAmount[] rewards,
            int maxHitPoints = 1, float combatSeconds = 0f, bool deepAltarRequired = false, int fixedDay = 0)
        {
            var definition = CreateInstance<BossDefinition>();
            definition.id = bossId;
            definition.kind = bossKind;
            definition.hitPoints = Mathf.Max(1, maxHitPoints);
            definition.expectedCombatSeconds = Mathf.Max(0f, combatSeconds);
            definition.summonItem = summon;
            definition.requiresDeepAltar = deepAltarRequired;
            definition.forcedDay = Mathf.Max(0, fixedDay);
            definition.guaranteedDrops = rewards;
            return definition;
        }
    }
}
