using UnityEngine;
using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.Bosses;

namespace Nyangbingo.Bosses { public interface IRegularSpawnController {void SetRegularSpawning(bool enabled);} }

namespace UnityEngine {
    public struct Vector2 {
        public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;}
        public static Vector2 one=>new(1,1);
        public float sqrMagnitude=>x*x+y*y;
        public static Vector2 operator +(Vector2 a,Vector2 b)=>new(a.x+b.x,a.y+b.y);
        public static Vector2 operator -(Vector2 a,Vector2 b)=>new(a.x-b.x,a.y-b.y);
        public static Vector2 operator *(Vector2 a,float b)=>new(a.x*b,a.y*b);
    }
    public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}}
    public struct Vector3Int {
        public int x,y,z;public Vector3Int(int x,int y,int z){this.x=x;this.y=y;this.z=z;}
        public static Vector3Int down=>new(0,-1,0);
        public static Vector3Int operator +(Vector3Int a,Vector3Int b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);
    }
    public struct Bounds {public Vector3 center,min,max;}
    public static class Mathf { public static float Clamp(float v,float lo,float hi)=>Math.Clamp(v,lo,hi); }
}
namespace Nyangbingo.Core {
    public enum YokaiKind {ClubGoblin,Bulgasari,Yagwanggwi,Gaekgwi}
    public interface IGameSecondsTickable {void Tick(float seconds);}
    public interface ITimeSource {int Day{get;}bool IsNight{get;}event Action Dawn;}
    public static class GameEvents {
        public static event Action OnNightStart,OnDawnWarning;
        public static void RaiseNightStart()=>OnNightStart?.Invoke();
        public static void RaiseDawnWarning()=>OnDawnWarning?.Invoke();
        public static void RaiseBaekjungStart(){} public static void RaiseBaekjungEnd(){}
    }
}
namespace Nyangbingo.Data {
    public enum YokaiSpawnTrack {Raid}
    public struct YokaiSpawnAmount {public YokaiKind kind;public int amount;}
    public class DayEventDefinition {
        public string Id="baekjung";public int Day=15,MaxActive=12;
        public float[] WaveOffsets={0,150,300}; public YokaiSpawnAmount[] Composition=Array.Empty<YokaiSpawnAmount>();
    }
    public class YokaiDefinition {public YokaiKind Kind;public bool SupportsSpawnTrack(YokaiSpawnTrack track)=>true;}
    public class DayCurveDefinition {public YokaiSpawnAmount[] SpawnComposition=Array.Empty<YokaiSpawnAmount>();}
    public class GameDataCatalog {
        public List<DayEventDefinition> DayEvents=new(); public List<YokaiDefinition> Yokai=new();
        public DayCurveDefinition Curve=new();public DayCurveDefinition FindDayCurve(int day)=>Curve;
    }
}
namespace Nyangbingo.World {
    public class DayNightService:ITimeSource {
        public int Day{get;set;}=15;public bool IsNight{get;set;}
        public float TimeOfDayGameSeconds{get;set;}public float DayDurationSeconds=>900;
        public event Action Dawn;public void EndNight(){IsNight=false;Day++;Dawn?.Invoke();}
    }
}
class Bootstrap {public Nyangbingo.World.DayNightService TimeService=new();}
partial class EncounterFixture : IRegularSpawnController, IBaekjungSpawnController {
    public Bootstrap bootstrap=new();public GameDataCatalog gameDataCatalog=new();
    public BaekjungScheduler baekjungScheduler;
    public bool regularSpawningEnabled=true,discardRegularForCurrentNight;
    DayCurveDefinition currentDayCurve; readonly Queue<YokaiDefinition> pendingRegular=new();
    public readonly List<YokaiKind> Ordinary=new(),Raids=new();
    public int ActiveRaidCount=>Raids.Count;public event Action RaidSlotAvailable;
    public bool TrySpawn(YokaiKind kind,int waveIndex){Raids.Add(kind);return true;}
    public void SetRegularSpawning(bool enabled){regularSpawningEnabled=enabled;if(IsRegularSpawningEnabled)TryFillRegularSlots();}
    void ClearPendingRegular()=>pendingRegular.Clear();
    void EnqueuePendingRegular(YokaiDefinition d)=>pendingRegular.Enqueue(d);
    void TryFillRegularSlots(){if(!IsRegularSpawningEnabled)return;while(pendingRegular.Count>0)Ordinary.Add(pendingRegular.Dequeue().Kind);}
    bool TryGetForcedInvasionCompositionKind(int day,out YokaiKind kind){kind=default;return false;}
}
struct TileData {public bool BlocksMovement;}
partial class TileFixture {
    public HashSet<(int,int)> Solid=new(),OpenDoors=new();
    bool InBounds(Vector3Int p)=>p.x>=0&&p.x<20&&p.y>=0&&p.y<20;
    TileData GetTile(Vector3Int p)=>new(){BlocksMovement=Solid.Contains((p.x,p.y))};
    bool IsDoorOpen(Vector3Int p)=>OpenDoors.Contains((p.x,p.y));
    Bounds GetCellWorldBounds(Vector3Int p)=>new(){min=new(p.x,p.y,p.z),max=new(p.x+1,p.y+1,p.z),center=new(p.x+.5f,p.y+.5f,p.z)};
    Vector3Int WorldToCell(Vector2 p)=>new((int)Math.Floor(p.x),(int)Math.Floor(p.y),0);
}
