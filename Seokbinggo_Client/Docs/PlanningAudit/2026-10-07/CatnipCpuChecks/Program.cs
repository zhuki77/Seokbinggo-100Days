using System; using System.Collections.Generic;
class Program {
static void Main() {
var tiles=new TileData[12,12]; var surfaces=new int[12];Array.Fill(surfaces,9);
for(int x=0;x<12;x++)for(int y=0;y<12;y++)tiles[x,y]=new TileData{BlocksMovement=true,isNaturalTerrain=true};
void Air(int x,int y,bool bg=true,string id="air"){tiles[x,y]=new TileData{IsAir=id=="air",HasNaturalBackground=bg,elementType=id};}
Air(1,10,false); Air(2,5); Air(4,5); Air(5,5); Air(7,5,false);Air(8,5,false);
Air(4,8);Air(5,8); Air(9,2);Air(10,2,true,"mushroom");Air(11,2);
var mask=BuildNaturalCatnipSites(new WorldGenerationResult{tiles=tiles,surfaceHeights=surfaces},2);
void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
Check(mask[1,10],"surface preserved"); Check(!mask[2,5],"isolated pocket rejected");
Check(mask[4,5]&&mask[5,5],"connected cave floor allowed");Check(!mask[7,5]&&!mask[8,5],"no natural background rejected");
Check(!mask[4,8]&&!mask[5,8],"surface crust rejected");Check(mask[9,2]&&mask[11,2]&&!mask[10,2],"mushroom connects cave without overlap");
}
        public static bool[,] BuildNaturalCatnipSites(WorldGenerationResult result, int crust)
        {
            var tiles = result.tiles;
            var width = tiles.GetLength(0);
            var height = tiles.GetLength(1);
            var allowed = new bool[width, height];
            var visited = new bool[width, height];
            var queue = new Queue<Vector2Int>();
            var floors = new List<Vector2Int>();
            var directions = new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };
            // Mushrooms are already placed in the baseline; they remain part of their
            // cave's passable space but cannot themselves be occupied by catnip.
            bool CaveAir(int x, int y) => x >= 0 && x < width && y > 0 && y < height &&
                (tiles[x, y].IsAir || WorldTileTypes.IsPassableMushroom(tiles[x, y].elementType)) &&
                tiles[x, y].HasNaturalBackground && y <= result.surfaceHeights[x] - crust;

            for (var x = 0; x < width; x++)
                for (var y = 1; y < height; y++)
                {
                    // Outdoor surface sites retain their existing eligibility.
                    if (tiles[x, y].IsAir && y > result.surfaceHeights[x]) allowed[x, y] = true;
                    if (visited[x, y] || !CaveAir(x, y)) continue;
                    floors.Clear();
                    visited[x, y] = true;
                    queue.Enqueue(new Vector2Int(x, y));
                    while (queue.Count > 0)
                    {
                        var cell = queue.Dequeue();
                        var floor = tiles[cell.x, cell.y - 1];
                        var space = CaveAir(cell.x, cell.y + 1) ||
                            !CaveAir(cell.x - 1, cell.y) || !CaveAir(cell.x + 1, cell.y);
                        if (floor.BlocksMovement && floor.isNaturalTerrain && space &&
                            floor.elementType != WorldTileTypes.IceAltar && floor.elementType != WorldTileTypes.IceLake)
                            floors.Add(cell);
                        foreach (var direction in directions)
                        {
                            var next = cell + direction;
                            if (!CaveAir(next.x, next.y) || visited[next.x, next.y]) continue;
                            visited[next.x, next.y] = true;
                            queue.Enqueue(next);
                        }
                    }
                    // Like mushrooms, reject isolated pockets with fewer than two floors.
                    if (floors.Count < 2) continue;
                    foreach (var cell in floors)
                        if (tiles[cell.x, cell.y].IsAir) allowed[cell.x, cell.y] = true;
                }
            return allowed;
        }

}
struct TileData{public bool IsAir,HasNaturalBackground,BlocksMovement,isNaturalTerrain;public string elementType;}
struct WorldGenerationResult{public TileData[,] tiles;public int[] surfaceHeights;}
static class WorldTileTypes{public const string IceAltar="altar",IceLake="lake";public static bool IsPassableMushroom(string id)=>id=="mushroom";}
struct Vector2Int{public int x,y;public Vector2Int(int x,int y){this.x=x;this.y=y;}public static Vector2Int left=>new(-1,0);public static Vector2Int right=>new(1,0);public static Vector2Int up=>new(0,1);public static Vector2Int down=>new(0,-1);public static Vector2Int operator +(Vector2Int a,Vector2Int b)=>new(a.x+b.x,a.y+b.y);}
