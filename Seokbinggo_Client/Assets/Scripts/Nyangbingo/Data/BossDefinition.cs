using Nyangbingo.Core;
using UnityEngine;

namespace Nyangbingo.Data
{
    public enum BossSpecialShape { Box, Cone }

    [CreateAssetMenu(menuName = "Nyangbingo/Data/Boss")]
    public sealed class BossDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private BossKind kind;
        [SerializeField] private int hitPoints;
        [SerializeField] private float expectedCombatSeconds;
        [SerializeField] private ItemDefinition summonItem;
        [SerializeField] private ItemAmount[] summonMaterials;
        [SerializeField] private CraftingStation summonStation;
        [SerializeField] private string recommendedDay;
        [SerializeField] private bool requiresDeepAltar;
        [SerializeField] private int forcedDay;
        [SerializeField] private ItemAmount[] guaranteedDrops;
        [SerializeField] private float wallDamageDefault;
        [SerializeField] private float wallDamageIce;
        [SerializeField] private float wallDamageIronWall;
        [SerializeField] private int contactDamage;
        [TextArea][SerializeField] private string specialDescription;
        [SerializeField] private float telegraphSeconds;
        [SerializeField] private BossSpecialShape specialShape;
        [SerializeField] private float specialRangeTiles;
        [SerializeField] private float specialArcDegrees;
        [SerializeField] private int specialDamagePerHit;
        [SerializeField] private float specialDurationSeconds;
        [SerializeField] private float specialTickSeconds;
        [SerializeField] private float specialKnockbackTiles;
        [SerializeField] private float specialCooldownSeconds;
        [SerializeField] private bool specialHasFireTag;
        [SerializeField] private bool specialAimLocks;
        [SerializeField] private ItemMvpScope mvpScope;
        public string Id => id;
        public string DisplayName => displayName;
        public BossKind Kind => kind;
        public int HitPoints => hitPoints;
        public float ExpectedCombatSeconds => expectedCombatSeconds;
        public ItemDefinition SummonItem => summonItem;
        public ItemAmount[] SummonMaterials => summonMaterials ?? System.Array.Empty<ItemAmount>();
        public CraftingStation SummonStation => summonStation;
        public string RecommendedDay => recommendedDay;
        public bool RequiresDeepAltar => requiresDeepAltar;
        public int ForcedDay => forcedDay;
        public ItemAmount[] GuaranteedDrops => guaranteedDrops ?? System.Array.Empty<ItemAmount>();
        public float WallDamageDefault => wallDamageDefault;
        public float WallDamageIce => wallDamageIce;
        public float WallDamageIronWall => wallDamageIronWall;
        public int ContactDamage => contactDamage;
        public string SpecialDescription => specialDescription;
        public float TelegraphSeconds => telegraphSeconds;
        public BossSpecialShape SpecialShape => specialShape;
        public float SpecialRangeTiles => specialRangeTiles;
        public float SpecialArcDegrees => specialArcDegrees;
        public int SpecialDamagePerHit => specialDamagePerHit;
        public float SpecialDurationSeconds => specialDurationSeconds;
        public float SpecialTickSeconds => specialTickSeconds;
        public float SpecialKnockbackTiles => specialKnockbackTiles;
        public float SpecialCooldownSeconds => specialCooldownSeconds;
        public bool SpecialHasFireTag => specialHasFireTag;
        public bool SpecialAimLocks => specialAimLocks;
        public ItemMvpScope MvpScope => mvpScope;

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
            definition.summonMaterials = System.Array.Empty<ItemAmount>();
            definition.requiresDeepAltar = deepAltarRequired;
            definition.forcedDay = Mathf.Max(0, fixedDay);
            definition.guaranteedDrops = rewards ?? System.Array.Empty<ItemAmount>();
            return definition;
        }
    }
}
