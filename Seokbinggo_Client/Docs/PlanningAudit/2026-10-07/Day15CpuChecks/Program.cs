using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.Bosses;
using UnityEngine;

class Program {
    static int count;
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
    static void Main(){
        foreach(bool reversed in new[]{true,false}) {
            var c=new EncounterFixture();
            var composition=new[]{new YokaiSpawnAmount{kind=YokaiKind.ClubGoblin,amount=3},new YokaiSpawnAmount{kind=YokaiKind.Bulgasari,amount=2},new YokaiSpawnAmount{kind=YokaiKind.Yagwanggwi,amount=6},new YokaiSpawnAmount{kind=YokaiKind.Gaekgwi,amount=1}};
            c.gameDataCatalog.DayEvents.Add(new(){Composition=composition});
            c.gameDataCatalog.Curve.SpawnComposition=composition;
            c.gameDataCatalog.Yokai.AddRange(composition.Select(x=>new YokaiDefinition{Kind=x.kind}));
            c.baekjungScheduler=new(c.gameDataCatalog.DayEvents);
            using var spawner=new BaekjungWaveSpawner(c.baekjungScheduler,c);
            using var gate=new BaekjungRegularSpawnGate(c.baekjungScheduler,c);
            if(reversed)GameEvents.OnNightStart+=c.HandleNightStart;
            using var binding=new BaekjungTimeBinding(c.bootstrap.TimeService,c.baekjungScheduler);
            if(!reversed)GameEvents.OnNightStart+=c.HandleNightStart;
            c.bootstrap.TimeService.IsNight=true;GameEvents.RaiseNightStart();
            Check(c.Ordinary.Count==0,"no ordinary pool, reversed="+reversed);
            Check(c.Raids.Count==4&&!c.Raids.Contains(YokaiKind.Gaekgwi),"first wave excludes Gaekgwi");
            c.bootstrap.TimeService.TimeOfDayGameSeconds=1049;binding.Tick(149);
            Check(!c.Raids.Contains(YokaiKind.Gaekgwi),"no Gaekgwi at +149");
            c.bootstrap.TimeService.TimeOfDayGameSeconds=1050;binding.Tick(1);
            Check(c.Raids.Count==8&&c.Raids.Count(x=>x==YokaiKind.Gaekgwi)==1,"Gaekgwi exactly once at +150");
            c.bootstrap.TimeService.TimeOfDayGameSeconds=1200;binding.Tick(150);
            Check(c.Raids.Count==12&&c.Raids.Count(x=>x==YokaiKind.Gaekgwi)==1,"total 12 after wave three");
            c.bootstrap.TimeService.EndNight();
            Check(c.Ordinary.Count==0,"no deferred ordinary pool at dawn");
            GameEvents.OnNightStart-=c.HandleNightStart;
        }
        var tiles=new TileFixture();for(int x=0;x<20;x++)tiles.Solid.Add((x,5));
        foreach(var r in new[]{.42f,.84f,1.26f}) {
            Check(tiles.TryGetYokaiSpawnPosition(new(10,6,0),r,out var pos),"spawn fits radius "+r);
            Check(Math.Abs(pos.y-r-6.02f)<.00001f,"feet clear of floor "+r);
        }
        tiles.Solid.Add((10,8));
        Check(!tiles.TryGetYokaiSpawnPosition(new(10,6,0),1.26f,out _),"Gaekgwi rejects low ceiling");
        Check(tiles.TryGetYokaiSpawnPosition(new(10,6,0),.42f,out _),"small yokai fits same ceiling");
        tiles.Solid.Remove((10,8));tiles.Solid.Add((9,7));
        Check(!tiles.TryGetYokaiSpawnPosition(new(10,6,0),1.26f,out _),"reject adjacent wall intersection");
        Check(!tiles.TryGetYokaiSpawnPosition(new(0,6,0),1.26f,out _),"reject map edge intersection");
        Check(!tiles.TryGetYokaiSpawnPosition(new(10,6,0),float.NaN,out _),"reject invalid radius");
        Check(EncounterFixture.ShouldDiscardLegacyEarlyGaekgwi(true,false,YokaiKind.Gaekgwi,1),"discard legacy premature ordinary Gaekgwi");
        Check(!EncounterFixture.ShouldDiscardLegacyEarlyGaekgwi(true,true,YokaiKind.Gaekgwi,1),"preserve real raid Gaekgwi");
        Check(!EncounterFixture.ShouldDiscardLegacyEarlyGaekgwi(true,false,YokaiKind.Gaekgwi,2),"preserve post-wave save");
        Check(!EncounterFixture.ShouldDiscardLegacyEarlyGaekgwi(false,false,YokaiKind.Gaekgwi,1),"preserve non-event test spawn");
        Check(!EncounterFixture.ShouldDiscardLegacyEarlyGaekgwi(true,false,YokaiKind.Yagwanggwi,1),"preserve other enemies and stolen items");
        Console.WriteLine($"{count}/{count} CPU checks passed");
    }
}
