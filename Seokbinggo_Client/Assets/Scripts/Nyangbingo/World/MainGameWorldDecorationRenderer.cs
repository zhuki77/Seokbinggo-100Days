using System;
using System.Collections.Generic;
using Nyangbingo.Core;
using Nyangbingo.Data;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Nyangbingo.World
{
    /// <summary>월드 생성 결과 위에 판정 없는 식생·유적 장식만 결정론적으로 배치한다.</summary>
    [DefaultExecutionOrder(-50)]
    public sealed class MainGameWorldDecorationRenderer : MonoBehaviour
    {
        private const int DecorationSeedSalt = 0x4E59414E;
        [SerializeField] private MainGameBootstrap bootstrap;
        [SerializeField] private WorldDecorationArtCatalog artCatalog;
        private Transform decorationRoot;
        private GameObject groundCoverRoot;
        private Tilemap surfaceGroundCoverTilemap;
        private Tile grassSurfaceTile;
        private Tile dryGrassSurfaceTile;
        private int surfaceCoverCount;
        private readonly Dictionary<string, SpriteRenderer> chestRenderers =
            new Dictionary<string, SpriteRenderer>(StringComparer.Ordinal);

        public int DecorationCount => decorationRoot != null ? decorationRoot.childCount : 0;
        public int ChestCount => chestRenderers.Count;

        public void ConfigureForScene(MainGameBootstrap mainBootstrap, WorldDecorationArtCatalog catalog)
        {
            bootstrap = mainBootstrap;
            artCatalog = catalog;
        }

        private void Start()
        {
            bootstrap ??= GetComponent<MainGameBootstrap>();
            if (bootstrap == null || artCatalog == null) return;
            bootstrap.WorldReady += Rebuild;
            GameEvents.OnTileBroken += HandleTileBroken;
            if (bootstrap.IsWorldReady) Rebuild();
        }

        private void Rebuild()
        {
            Clear();
            var session = bootstrap?.Session;
            var result = session != null ? session.LastResult : default;
            var tiles = result.tiles;
            if (tiles == null) return;

            decorationRoot = new GameObject("WorldDecorations").transform;
            decorationRoot.SetParent(transform, false);
            var random = new System.Random(result.acceptedSeed ^ DecorationSeedSalt);
            PlaceSurfaceGroundCover(result, random);
            PlaceSurfaceDecorations(result, random);
            PlaceRuinDecorations(result, random);
            PlaceChests(result);
            Debug.Log($"[Nyangbingo] World decorations rendered: surface={surfaceCoverCount}, " +
                      $"objects={DecorationCount}, chests={ChestCount} " +
                      $"(seed={result.acceptedSeed}).");
        }

        private void PlaceChests(WorldGenerationResult result)
        {
            if (result.chests == null) return;
            var buildingCatalog = GetComponent<MainGameEnvironmentState>()?.BuildingArtCatalog;
            var chestArt = buildingCatalog?.Find("chest");
            var closedSprite = chestArt?.Sprite;
            var openSprite = chestArt?.Frames.Count > 1 ? chestArt.Frames[1] : closedSprite;
            var progress = bootstrap?.Session?.ChestProgress;
            foreach (var chest in result.chests)
            {
                var visual = new GameObject($"Chest_{chest.id}");
                visual.transform.SetParent(decorationRoot, false);
                visual.transform.position = new Vector2(chest.position.x + .5f, chest.position.y + .5f);
                var renderer = visual.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = 11;
                var opened = progress?.IsOpened(chest.id) == true;
                if (closedSprite != null)
                    renderer.sprite = opened ? openSprite : closedSprite;
                else
                    RuntimePlaceholderVisual.Configure(renderer,
                        opened ? new Color(.35f, .35f, .4f) : new Color(.85f, .58f, .16f), .75f, 11);
                chestRenderers[chest.id] = renderer;
            }
        }

        public void MarkChestOpened(string chestId)
        {
            if (string.IsNullOrEmpty(chestId) || !chestRenderers.TryGetValue(chestId, out var renderer) ||
                renderer == null) return;
            var chestArt = GetComponent<MainGameEnvironmentState>()?.BuildingArtCatalog?.Find("chest");
            if (chestArt?.Frames.Count > 1)
                renderer.sprite = chestArt.Frames[1];
            else
                renderer.color = new Color(.35f, .35f, .4f);
        }

        private void PlaceSurfaceGroundCover(WorldGenerationResult result, System.Random random)
        {
            var grass = artCatalog.Find("grass")?.Sprite;
            var dryGrass = artCatalog.Find("grass_dry")?.Sprite;
            if (grass == null || dryGrass == null) return;

            groundCoverRoot = new GameObject("SurfaceGroundCoverGrid", typeof(Grid));
            groundCoverRoot.transform.SetParent(transform, false);
            var layer = new GameObject("SurfaceGroundCover", typeof(Tilemap),
                typeof(UnityEngine.Tilemaps.TilemapRenderer));
            layer.transform.SetParent(groundCoverRoot.transform, false);
            var tilemap = layer.GetComponent<Tilemap>();
            surfaceGroundCoverTilemap = tilemap;
            layer.GetComponent<UnityEngine.Tilemaps.TilemapRenderer>().sortingOrder = 1;
            grassSurfaceTile = CreateRuntimeTile("RuntimeGrassSurface", grass);
            dryGrassSurfaceTile = CreateRuntimeTile("RuntimeDryGrassSurface", dryGrass);

            var tiles = result.tiles;
            var width = tiles.GetLength(0);
            var height = tiles.GetLength(1);
            var dryPatchRemaining = 0;
            for (var x = 0; x < width; x++)
            {
                if (dryPatchRemaining <= 0 && random.Next(100) < 4)
                    dryPatchRemaining = random.Next(6, 15);
                var surfaceY = FindSurface(tiles, x, height);
                if (surfaceY < 0 || !tiles[x, surfaceY].isNaturalTerrain) continue;
                tilemap.SetTile(new Vector3Int(x, surfaceY, 0),
                    dryPatchRemaining > 0 ? dryGrassSurfaceTile : grassSurfaceTile);
                surfaceCoverCount++;
                if (dryPatchRemaining > 0) dryPatchRemaining--;
            }
        }

        private static Tile CreateRuntimeTile(string name, Sprite sprite)
        {
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = name;
            tile.sprite = sprite;
            tile.color = Color.white;
            tile.colliderType = Tile.ColliderType.None;
            return tile;
        }

        private void HandleTileBroken(Vector3Int cell)
        {
            if (surfaceGroundCoverTilemap == null) return;
            surfaceGroundCoverTilemap.SetTile(cell, null);
        }

        private void PlaceSurfaceDecorations(WorldGenerationResult result, System.Random random)
        {
            var width = result.tiles.GetLength(0);
            var occupiedColumns = new HashSet<int>();
            var anchorX = random.Next(10, 20);
            while (anchorX < width - 3)
            {
                var roll = random.Next(100);
                if (roll < 55)
                {
                    PlacePlantPatch(result, random, occupiedColumns, anchorX, "hemp", random.Next(1, 4), .88);
                }
                else
                {
                    var radius = random.Next(2, 4);
                    TrySpawnSurface(result, random, occupiedColumns, anchorX - radius,
                        $"tree_{random.Next(3)}");
                    if (random.Next(100) < 65)
                        TrySpawnSurface(result, random, occupiedColumns, anchorX,
                            $"tree_{random.Next(3)}");
                    TrySpawnSurface(result, random, occupiedColumns, anchorX + radius,
                        $"tree_{random.Next(3)}");
                }
                anchorX += random.Next(18, 33);
            }
        }

        private void PlacePlantPatch(WorldGenerationResult result, System.Random random,
            ISet<int> occupiedColumns, int centerX, string id, int radius, double density)
        {
            for (var x = centerX - radius; x <= centerX + radius; x++)
                if (random.NextDouble() <= density)
                    TrySpawnSurface(result, random, occupiedColumns, x, id);
        }

        private void TrySpawnSurface(WorldGenerationResult result, System.Random random,
            ISet<int> occupiedColumns, int x, string id)
        {
            var tiles = result.tiles;
            var width = tiles.GetLength(0);
            var height = tiles.GetLength(1);
            if (x < 2 || x >= width - 2 || occupiedColumns.Contains(x) ||
                Mathf.Abs(x - result.spawnPoint.x) <= 5 || Mathf.Abs(x - result.altarPosition.x) <= 3) return;
            var surfaceY = FindSurface(tiles, x, height);
            if (surfaceY < 0 || surfaceY + 1 >= height || !tiles[x, surfaceY].isNaturalTerrain ||
                tiles[x, surfaceY + 1].hardness > 0) return;

            var art = artCatalog.Find(id);
            if (art?.Sprite == null) return;
            var centerY = surfaceY + 1f + art.Sprite.bounds.extents.y;
            Spawn(id, art, new Vector2(x + .5f, centerY), random.Next(2) == 0);
            occupiedColumns.Add(x);
        }

        private void PlaceRuinDecorations(WorldGenerationResult result, System.Random random)
        {
            var tiles = result.tiles;
            var width = tiles.GetLength(0);
            var height = tiles.GetLength(1);
            var visited = new bool[width, height];
            for (var y = 1; y < height - 1; y++)
            for (var x = 1; x < width - 1; x++)
            {
                if (visited[x, y] || !IsRuinWall(tiles, x, y)) continue;
                var cells = CollectConnectedRuin(tiles, x, y, visited);
                if (cells.Count == 0) continue;

                var pillarCell = cells[0];
                var rebarCell = cells[0];
                foreach (var cell in cells)
                {
                    if (cell.x < pillarCell.x || cell.x == pillarCell.x && cell.y < pillarCell.y)
                        pillarCell = cell;
                    if (cell.x > rebarCell.x || cell.x == rebarCell.x && cell.y > rebarCell.y)
                        rebarCell = cell;
                }
                SpawnRuinDecoration("ruin_pillar", pillarCell, random.Next(2) == 0);
                SpawnRuinDecoration("ruin_rebar", rebarCell, random.Next(2) == 0);
            }
        }

        private void SpawnRuinDecoration(string id, Vector2Int cell, bool flipX)
        {
            var art = artCatalog.Find(id);
            if (art?.Sprite != null) Spawn(id, art, new Vector2(cell.x + .5f, cell.y + .5f), flipX);
        }

        private static List<Vector2Int> CollectConnectedRuin(TileData[,] tiles, int startX, int startY,
            bool[,] visited)
        {
            var width = tiles.GetLength(0);
            var height = tiles.GetLength(1);
            var cells = new List<Vector2Int>();
            var pending = new Queue<Vector2Int>();
            pending.Enqueue(new Vector2Int(startX, startY));
            visited[startX, startY] = true;
            while (pending.Count > 0)
            {
                var cell = pending.Dequeue();
                cells.Add(cell);
                TryEnqueue(cell.x + 1, cell.y);
                TryEnqueue(cell.x - 1, cell.y);
                TryEnqueue(cell.x, cell.y + 1);
                TryEnqueue(cell.x, cell.y - 1);
            }
            return cells;

            void TryEnqueue(int x, int y)
            {
                if (x < 0 || x >= width || y < 0 || y >= height || visited[x, y] ||
                    !IsRuinWall(tiles, x, y)) return;
                visited[x, y] = true;
                pending.Enqueue(new Vector2Int(x, y));
            }
        }

        private static bool IsRuinWall(TileData[,] tiles, int x, int y) =>
            string.Equals(tiles[x, y].elementType, WorldTileTypes.RuinWall, StringComparison.Ordinal);

        private void Spawn(string id, WorldDecorationArtCatalog.Entry art, Vector2 position, bool flipX)
        {
            var visual = new GameObject($"Decoration_{id}");
            visual.transform.SetParent(decorationRoot, false);
            visual.transform.position = position;
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = art.Sprite;
            renderer.flipX = flipX;
            renderer.sortingOrder = 2;
            if (art.Frames.Count > 1)
                visual.AddComponent<RuntimeBuildingSpriteAnimator>().Configure(art.Frames);
        }

        private static int FindSurface(TileData[,] tiles, int x, int height)
        {
            for (var y = height - 2; y >= 0; y--)
                if (tiles[x, y].hardness > 0) return y;
            return -1;
        }

        private void Clear()
        {
            if (decorationRoot != null) Destroy(decorationRoot.gameObject);
            if (groundCoverRoot != null) Destroy(groundCoverRoot);
            if (grassSurfaceTile != null) Destroy(grassSurfaceTile);
            if (dryGrassSurfaceTile != null) Destroy(dryGrassSurfaceTile);
            decorationRoot = null;
            groundCoverRoot = null;
            surfaceGroundCoverTilemap = null;
            grassSurfaceTile = null;
            dryGrassSurfaceTile = null;
            surfaceCoverCount = 0;
            chestRenderers.Clear();
        }

        private void OnDestroy()
        {
            if (bootstrap != null) bootstrap.WorldReady -= Rebuild;
            GameEvents.OnTileBroken -= HandleTileBroken;
        }
    }
}
