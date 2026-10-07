using Nyangbingo.Core;
using UnityEngine;
namespace UnityEngine {
 public record struct Vector3Int(int x,int y,int z=0) {public static Vector3Int up=>new(0,1);public static Vector3Int down=>new(0,-1);public static Vector3Int left=>new(-1,0);public static Vector3Int right=>new(1,0);public int sqrMagnitude=>x*x+y*y+z*z;public static Vector3Int operator+(Vector3Int a,Vector3Int b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);public static Vector3Int operator-(Vector3Int a,Vector3Int b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);public static Vector3Int operator*(Vector3Int a,int b)=>new(a.x*b,a.y*b,a.z*b);}
 public record struct Vector2Int(int x,int y);
 public record struct Vector2(float x,float y){public static implicit operator Vector2(Vector3 v)=>new(v.x,v.y);}
 public record struct Vector3(float x,float y,float z=0){public static Vector3 up=>new(0,1);public static Vector3 operator+(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);public static Vector3 operator*(Vector3 a,float b)=>new(a.x*b,a.y*b,a.z*b);}
 public struct Bounds{public Vector3 center,size;}
 public static class Mathf {public static int Max(int a,int b)=>Math.Max(a,b);public static float Max(float a,float b)=>Math.Max(a,b);public static float Min(float a,float b)=>Math.Min(a,b);public static int Abs(int a)=>Math.Abs(a);}
}
namespace Nyangbingo.Core {
 public interface ISealSource{float SealPercent{get;}bool IsInsideSealedArea(Vector2 p);}
 public interface ISealBarrierRegistry{bool IsRecognizedBarrier(Vector3Int c);}
 public interface ISealDoorRegistry{bool TryGetDoor(Vector3Int c,out Vector3Int anchor,out bool closed);}

 public static class TileIdAlias{public static string ToCanonical(string s)=>s;}
 public static class GameEvents {
 public static event Action<Vector3Int> OnTileBroken,OnTilePlaced;
 public static event Action OnNightStart;
 public static event Action<Vector3Int,bool,bool> OnMiningTargetChanged;
 public static void RaiseSealChanged(){}
 public static void Broken(Vector3Int c)=>OnTileBroken?.Invoke(c);
 public static void Placed(Vector3Int c)=>OnTilePlaced?.Invoke(c);
 public static void Aim(Vector3Int c,bool v=true,bool m=true)=>OnMiningTargetChanged?.Invoke(c,v,m);
 }
}
namespace Nyangbingo.Data {
 public class SealWhitelistDefinition{public string Element;public bool Seals;}
 public class GlobalDefinition{public string Value;}
 public class GameDataCatalog{public GlobalDefinition FindGlobal(string s)=>new(){Value="3x3"};}
}
namespace Nyangbingo.Save {public class SaveGame {public List<Vector3Int> invasionBrokenCells=new();public int invasionLastBaseAttackDay,invasionLastCompletedRecoveryDay;}}
namespace Nyangbingo.World {
 public struct TileData{public string elementType;public bool isNaturalTerrain;public bool IsAir=>string.IsNullOrEmpty(elementType);}

 public class TileService {
 private TileData[,] tiles=new TileData[30,30];
 public const string DoorTopElementType="door_top";
 public event Action<Vector3Int,int> WallDestroyedByDamage;
 public static bool IsDoorFootprintElement(string s)=>s=="door"||s=="door_top";
 public bool InBounds(Vector3Int c)=>c.x>=0&&c.y>=0&&c.x<30&&c.y<30;
 public int TileReadCount;
 public TileData GetTile(Vector3Int c){TileReadCount++;return tiles[c.x,c.y];}
 public Vector3Int WorldToCell(Vector2 p)=>new((int)p.x,(int)p.y);
 public Bounds GetCellWorldBounds(Vector3Int c)=>new(){center=new(c.x+.5f,c.y+.5f),size=new(1,1)};
 public void Set(Vector3Int c,string s,bool natural=true){tiles[c.x,c.y]=new(){elementType=s,isNaturalTerrain=natural};GameEvents.Placed(c);}
 public void DamageDestroy(Vector3Int c,int height=1){for(var i=0;i<height;i++){var p=c+Vector3Int.up*i;tiles[p.x,p.y]=default;GameEvents.Broken(p);}WallDestroyedByDamage?.Invoke(c,height);}
 }
 public class MainGameBootstrap{public TileService TileService;public SealSystem SealSystem;public SessionState Session=new();public TimeState TimeService=new();}
 public class SessionState{public bool HasWorld=true;}
 public class TimeState{public int Day=6;public bool IsNight=true;}
 public class PlacedRecord{public string objectId,definitionId;public Vector2 position;}
 public class MainGameEnvironmentState{public bool IsInitialized=true;public Vector2 Core=new(10,10);public List<PlacedRecord> Objects=new();public bool TryGetNearestPlacedObjectPosition(string id,Vector2 origin,out Vector2 p){p=Core;return true;}public List<PlacedRecord> ExportPlacedObjects()=>Objects;public bool TryGetBarrierActive(string id,out bool closed){closed=true;return true;}}
 public class MainGameGoalTracker{public bool IsSuspended;public int RestoreVersion;}
 public class InvasionService{public bool IsCurrentInvasionNight=true;public float TemperatureRiseCelsius;public int LastInfiltrationDay,LastFinishedInvasionDay;}
 public class StorageConditionState{public bool HasRequirement=true,Met;}
 public class StorageTemperatureService{public StorageConditionState Condition=new();public bool TryGetCondition(string id,out StorageConditionState c){c=Condition;return true;}}
 public class MainGameRuntimeServices{public InvasionService Invasion=new();public StorageTemperatureService StorageTemperature=new();}
}
