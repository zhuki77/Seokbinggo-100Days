using System.Collections.Generic;
namespace Nyangbingo.Core {
 public enum CraftingStation {None,Workbench,Furnace,IceAnvil,Foundry,Smithy}
 public enum SmeltingStationKind {Furnace,Foundry}
}
namespace Nyangbingo.Data {
 public enum ItemMvpScope {Unspecified,A,B}
 public enum MineralLayer {SurfaceNight,SurfaceRuinNight,UndergroundUpper,UndergroundMiddle,UndergroundDeep}
 [System.Flags] public enum YokaiSpawnTrack {None=0,Raid=1,Resident=2}
 public enum YokaiSignatureCondition {None,StealSuccess}
 public class ItemDefinition { public string Id,DisplayName; }
 public struct ItemAmount {public ItemDefinition item;public int amount;}
 public class MineralTierDefinition {public ItemDefinition Resource;public MineralLayer Layer;public int MinimumDepth,MaximumDepth,MinimumClawTier,Hardness;}
 public class YokaiDefinition {public string DisplayName;public YokaiSpawnTrack SpawnTracks;public ItemDefinition SignatureItem,TearItem;public float SignatureChance;public YokaiSignatureCondition SignatureCondition;public int TearDrop;public ItemAmount[] Drops=System.Array.Empty<ItemAmount>();}
 public class BossDefinition {public string DisplayName;public ItemAmount[] GuaranteedDrops=System.Array.Empty<ItemAmount>();}
 public class RecipeDefinition {public ItemAmount Output;public ItemMvpScope MvpScope;public Nyangbingo.Core.CraftingStation Station;public ItemAmount[] Ingredients;}
 public class SmeltingDefinition {public ItemAmount Input,Fuel,Output;public Nyangbingo.Core.SmeltingStationKind StationKind;}
 public class CropDefinition {public string ZoneId,CropId;public int Order,SpawnPerHundredTiles;}
 public class ZoneDefinition {public string Id;public int DistanceTilesFrom,DistanceTilesTo;}
 public class TerrainSpawnDefinition {public bool Implemented;public string TerrainDisplayName;public string[] TerrainResourceIds=System.Array.Empty<string>();}
 public class GameDataCatalog {
 public List<MineralTierDefinition> MineralTiers=new();public List<YokaiDefinition> Yokai=new();public List<BossDefinition> Bosses=new();public List<RecipeDefinition> Recipes=new();public List<SmeltingDefinition> Smelting=new();public List<TerrainSpawnDefinition> TerrainSpawns=new();public List<CropDefinition> Crops=new();public List<ZoneDefinition> Zones=new();
 public ZoneDefinition FindZone(string id)=>Zones.Find(z=>z.Id==id);
 }
}