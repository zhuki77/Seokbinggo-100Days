using Nyangbingo.Core;
using UnityEngine;

namespace Nyangbingo.Data
{
    [System.Flags]
    public enum YokaiSpawnTrack { None = 0, Raid = 1, Resident = 2 }
    public enum YokaiWallMaterial { Default, Ice, IronHeatWall }
    public enum YokaiDamageTakenCondition { None, LanternRadius, StealOnly }
    public enum YokaiSignatureCondition { None, StealSuccess }

    [CreateAssetMenu(menuName = "Nyangbingo/Data/Yokai")]
    public sealed class YokaiDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private string appearanceHint;
        [TextArea][SerializeField] private string codexNote;
        [SerializeField] private YokaiKind kind;
        [SerializeField] private int hitPoints;
        [SerializeField] private float moveSpeed;
        [SerializeField] private int contactDamage;
        [SerializeField] private int contactDamageNoLantern;
        [SerializeField] private float wallDamageDefault;
        [SerializeField] private float wallDamageIce;
        [SerializeField] private float wallDamageIronWall;
        [Min(0f)][SerializeField] private float damageTakenMultiplier = 1f;
        [SerializeField] private YokaiDamageTakenCondition damageTakenCondition;
        [Min(0)][SerializeField] private int stealSlots;
        [Min(0)][SerializeField] private int stealMaxItems;
        [SerializeField] private ItemDefinition tearItem;
        [Min(0)][SerializeField] private int tearDrop;
        [Min(0)][SerializeField] private int tearBonus;
        [SerializeField] private ItemDefinition signatureItem;
        [Range(0f, 1f)][SerializeField] private float signatureChance;
        [SerializeField] private YokaiSignatureCondition signatureCondition;
        [SerializeField] private YokaiSpawnTrack spawnTracks = YokaiSpawnTrack.Raid;
        [SerializeField] private bool raidFleesAtDawn = true;
        [SerializeField] private ItemAmount[] drops;
        public string Id => id;
        public string DisplayName => displayName;
        public string AppearanceHint => appearanceHint;
        public string CodexNote => codexNote;
        public YokaiKind Kind => kind;
        public int HitPoints => hitPoints;
        public float MoveSpeed => moveSpeed;
        public int ContactDamage => contactDamage;
        public int ContactDamageNoLantern => contactDamageNoLantern;
        public float WallDamagePerSecond => wallDamageDefault;
        public float WallDamageIce => wallDamageIce;
        public float WallDamageIronWall => wallDamageIronWall;
        public float DamageTakenMultiplier => damageTakenMultiplier;
        public YokaiDamageTakenCondition DamageTakenCondition => damageTakenCondition;
        public int StealSlots => stealSlots;
        public int StealMaxItems => stealMaxItems;
        public ItemDefinition TearItem => tearItem;
        public int TearDrop => tearDrop;
        public int TearBonus => tearBonus;
        public ItemDefinition SignatureItem => signatureItem;
        public float SignatureChance => signatureChance;
        public YokaiSignatureCondition SignatureCondition => signatureCondition;
        public YokaiSpawnTrack SpawnTracks => spawnTracks;
        public bool RaidFleesAtDawn => raidFleesAtDawn;
        public ItemAmount[] Drops => drops ?? System.Array.Empty<ItemAmount>();

        public bool SupportsSpawnTrack(YokaiSpawnTrack track)
            => track != YokaiSpawnTrack.None && (spawnTracks & track) == track;

        public float WallDamageFor(YokaiWallMaterial material)
        {
            switch (material)
            {
                case YokaiWallMaterial.Ice: return wallDamageIce;
                case YokaiWallMaterial.IronHeatWall: return wallDamageIronWall;
                default: return wallDamageDefault;
            }
        }

        public int ContactDamageFor(bool isInLanternRange)
            => isInLanternRange ? contactDamage : contactDamageNoLantern;

        public float DamageTakenMultiplierFor(bool isInLanternRange)
            => damageTakenCondition == YokaiDamageTakenCondition.LanternRadius && isInLanternRange
                ? damageTakenMultiplier
                : 1f;

        public static YokaiDefinition CreateRuntime(YokaiKind value, int hp, float speed, int contact,
            float wallDps, ItemAmount[] loot, float wallDpsIce = -1f, float wallDpsIronWall = -1f,
            int noLanternContact = -1, float takenMultiplier = 1f,
            YokaiDamageTakenCondition takenCondition = YokaiDamageTakenCondition.None,
            int inventoryStealSlots = 0, int inventoryStealMaxItems = 0,
            YokaiSpawnTrack allowedSpawnTracks = YokaiSpawnTrack.Raid, bool fleeAtDawn = true,
            int successfulTheftTearBonus = 0,
            YokaiSignatureCondition signatureDropCondition = YokaiSignatureCondition.None)
        {
            var definition = CreateInstance<YokaiDefinition>();
            definition.id = value.ToString();
            definition.kind = value; definition.hitPoints = hp; definition.moveSpeed = speed;
            definition.contactDamage = contact;
            definition.contactDamageNoLantern = noLanternContact < 0 ? contact : noLanternContact;
            definition.wallDamageDefault = wallDps;
            definition.wallDamageIce = wallDpsIce < 0f ? wallDps : wallDpsIce;
            definition.wallDamageIronWall = wallDpsIronWall < 0f ? wallDps : wallDpsIronWall;
            definition.damageTakenMultiplier = takenMultiplier;
            definition.damageTakenCondition = takenCondition;
            definition.stealSlots = inventoryStealSlots;
            definition.stealMaxItems = inventoryStealMaxItems;
            definition.tearBonus = successfulTheftTearBonus;
            definition.signatureCondition = signatureDropCondition;
            definition.spawnTracks = allowedSpawnTracks;
            definition.raidFleesAtDawn = fleeAtDawn;
            definition.drops = loot;
            return definition;
        }
    }

}
