using System;
using System.Collections.Generic;
using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.Save;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Nyangbingo.World
{
    /// <summary>월드 생성 결과 위에 판정 없는 식생·유적 장식만 결정론적으로 배치한다.</summary>
    [DefaultExecutionOrder(-50)]
    public sealed class MainGameWorldDecorationRenderer : MonoBehaviour
    {
        private const int DecorationSeedSalt = 0x4E59414E;
        private const int HempSeedSalt = 0x48454D50;
        public const string HempItemId = "hemp_stalk";
        public const string WoodItemId = "wood";
        public const string RebarItemId = "rebar";
        [SerializeField] private MainGameBootstrap bootstrap;
        [SerializeField] private WorldDecorationArtCatalog artCatalog;
        [SerializeField] private ItemArtCatalog itemArtCatalog;
        private Transform decorationRoot;
        // Legacy surface-cover fields remain only for clearing old runtime instances during
        // hot reload. New worlds no longer create this non-interactive grass layer.
        private GameObject groundCoverRoot;
        private Tilemap surfaceGroundCoverTilemap;
        private Tile grassSurfaceTile;
        private Tile dryGrassSurfaceTile;
        private int surfaceCoverCount;
        private readonly Dictionary<string, SpriteRenderer> chestRenderers =
            new Dictionary<string, SpriteRenderer>(StringComparer.Ordinal);
        private readonly HashSet<Vector3Int> chestCells = new HashSet<Vector3Int>();
        private readonly Dictionary<Transform, Vector3Int> decorationSupportCells =
            new Dictionary<Transform, Vector3Int>();
        private readonly HashSet<Vector3Int> surfaceDecorationSupportCells =
            new HashSet<Vector3Int>();
        private readonly Dictionary<string, CatnipPatch> catnipPatches =
            new Dictionary<string, CatnipPatch>(StringComparer.Ordinal);
        private readonly Dictionary<string, HempPatch> hempPatches =
            new Dictionary<string, HempPatch>(StringComparer.Ordinal);
        private readonly Dictionary<string, TreePatch> treePatches =
            new Dictionary<string, TreePatch>(StringComparer.Ordinal);
        private readonly Dictionary<string, RebarPatch> rebarPatches =
            new Dictionary<string, RebarPatch>(StringComparer.Ordinal);

        public int DecorationCount => decorationRoot != null ? decorationRoot.childCount : 0;
        public int ChestCount => chestRenderers.Count;
        public int CatnipPatchCount => catnipPatches.Count;
        public int HempPatchCount => hempPatches.Count;
        public int TreePatchCount => treePatches.Count;
        public int RebarPatchCount => rebarPatches.Count;

        private sealed class CatnipPatch
        {
            public string Id;
            public Vector3Int SupportCell;
            public SpriteRenderer Renderer;
            public int HarvestedDay;
        }

        private sealed class HempPatch
        {
            public string Id;
            public Vector3Int SupportCell;
            public SpriteRenderer Renderer;
            public bool Harvested;
        }

        private sealed class TreePatch
        {
            public string Id;
            public Vector3Int SupportCell;
            public SpriteRenderer Renderer;
            public bool Harvested;
        }

        private sealed class RebarPatch
        {
            public string Id;
            public Vector3Int SupportCell;
            public SpriteRenderer Renderer;
            public bool Harvested;
        }

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
            GameEvents.OnDayStart += RefreshCatnipAvailability;
            bootstrap.TileService?.SetForegroundPlacementBlocker(IsForegroundPlacementBlocked);
            if (bootstrap.IsWorldReady) Rebuild();
        }

        public bool IsForegroundPlacementBlocked(Vector3Int cell)
        {
            if (chestCells.Contains(cell)) return true;
            foreach (var patch in catnipPatches.Values)
                if (patch.SupportCell + Vector3Int.up == cell && IsCatnipAvailable(patch))
                    return true;
            foreach (var patch in hempPatches.Values)
                if (patch.SupportCell + Vector3Int.up == cell && IsHempAvailable(patch))
                    return true;
            foreach (var tree in treePatches.Values)
                if (IsTreeAvailable(tree) &&
                    (tree.SupportCell + Vector3Int.up == cell ||
                     tree.SupportCell + Vector3Int.up * 2 == cell))
                    return true;
            foreach (var rebar in rebarPatches.Values)
                if (rebar.SupportCell == cell && IsRebarAvailable(rebar))
                    return true;
            return false;
        }

        private void Rebuild()
        {
            bootstrap?.TileService?.SetForegroundPlacementBlocker(IsForegroundPlacementBlocked);
            Clear();
            var session = bootstrap?.Session;
            var result = session != null ? session.LastResult : default;
            var tiles = result.tiles;
            if (tiles == null) return;

            decorationRoot = new GameObject("WorldDecorations").transform;
            decorationRoot.SetParent(transform, false);
            var random = new System.Random(result.acceptedSeed ^ DecorationSeedSalt);
            PlaceSurfaceDecorations(result, random);
            PlaceRuinDecorations(result, random);
            PlaceCatnipPatches(result, random);
            PlaceHempPatches(result, new System.Random(result.acceptedSeed ^ HempSeedSalt));
            PlaceChests(result);
            Debug.Log($"[Nyangbingo] World decorations rendered: objects={DecorationCount}, " +
                      $"catnip={CatnipPatchCount}, hemp={HempPatchCount}, chests={ChestCount} " +
                      $"(seed={result.acceptedSeed}).");
        }

        private void PlaceCatnipPatches(WorldGenerationResult result, System.Random random)
        {
            var spawnDensity = ReadPositiveGlobal("catnip_spawn_per_100tiles", 6f);
            var width = result.tiles.GetLength(0);
            var targetCount = Mathf.Max(1, Mathf.RoundToInt(width * spawnDensity / 100f));
            var occupied = new HashSet<Vector3Int>();
            for (var index = 0; index < targetCount; index++)
            {
                var preferRuins = (index & 1) == 0;
                if (!TryFindCatnipSupport(result, random, preferRuins, occupied, out var support) &&
                    !TryFindCatnipSupport(result, random, !preferRuins, occupied, out support))
                    continue;
                occupied.Add(support);
                CreateCatnipPatch($"catnip_{index:00}", support, random.Next(2) == 0);
            }
        }

        private bool TryFindCatnipSupport(WorldGenerationResult result, System.Random random, bool ruins,
            ISet<Vector3Int> occupied, out Vector3Int support)
        {
            support = default;
            var tiles = result.tiles;
            var width = tiles.GetLength(0);
            var height = tiles.GetLength(1);
            var attempts = Mathf.Max(100, width * 3);
            for (var attempt = 0; attempt < attempts; attempt++)
            {
                var x = random.Next(2, width - 2);
                if (Mathf.Abs(x - result.spawnPoint.x) <= 5 || Mathf.Abs(x - result.altarPosition.x) <= 3)
                    continue;
                if (ruins)
                {
                    var startY = random.Next(1, height - 2);
                    for (var offset = 0; offset < height - 3; offset++)
                    {
                        var y = 1 + (startY + offset) % (height - 3);
                        if (!string.Equals(tiles[x, y].elementType, WorldTileTypes.RuinWall,
                                StringComparison.Ordinal) ||
                            !tiles[x, y + 1].IsAir)
                            continue;
                        var candidate = new Vector3Int(x, y, 0);
                        if (occupied.Contains(candidate) || IsChestPlantCell(result, candidate) ||
                            IsNearSurfaceDecoration(candidate, 2f)) continue;
                        support = candidate;
                        return true;
                    }
                    continue;
                }

                var surfaceY = FindSurface(tiles, x, height);
                if (surfaceY < 2) continue;
                var lowerY = Mathf.Max(1, surfaceY - 44);
                var start = random.Next(lowerY, surfaceY);
                for (var offset = 0; offset < surfaceY - lowerY; offset++)
                {
                    var y = lowerY + (start - lowerY + offset) % (surfaceY - lowerY);
                    if (tiles[x, y].IsAir || !tiles[x, y + 1].IsAir) continue;
                    var candidate = new Vector3Int(x, y, 0);
                    if (occupied.Contains(candidate) || IsChestPlantCell(result, candidate) ||
                        IsNearSurfaceDecoration(candidate, 2f)) continue;
                    support = candidate;
                    return true;
                }
            }
            return false;
        }

        private static bool IsChestPlantCell(WorldGenerationResult result, Vector3Int supportCell)
        {
            if (result.chests == null) return false;
            var plantCell = supportCell + Vector3Int.up;
            for (var i = 0; i < result.chests.Count; i++)
            {
                var chest = result.chests[i];
                if (chest.position.x == plantCell.x && chest.position.y == plantCell.y)
                    return true;
            }
            return false;
        }

        private void CreateCatnipPatch(string id, Vector3Int supportCell, bool flipX)
        {
            var visual = new GameObject($"Catnip_{id}");
            visual.transform.SetParent(decorationRoot, false);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = itemArtCatalog?.FindSprite(PlayerHealthRecoveryService.CatnipItemId);
            renderer.flipX = flipX;
            renderer.sortingOrder = 3;
            if (renderer.sprite == null)
                RuntimePlaceholderVisual.Configure(renderer, new Color(.32f, .72f, .24f), .45f, 3);
            AlignSurfaceVisual(renderer, supportCell);
            catnipPatches.Add(id, new CatnipPatch
            {
                Id = id,
                SupportCell = supportCell,
                Renderer = renderer
            });
            visual.SetActive(HasSolidRuntimeSupport(supportCell));
        }

        private bool HasSolidRuntimeSupport(Vector3Int supportCell) =>
            bootstrap?.TileService?.GetTile(supportCell).IsAir == false;

        private void PlaceHempPatches(WorldGenerationResult result, System.Random random)
        {
            var definition = bootstrap?.GameDataCatalog?.FindMineralTier(HempItemId);
            var density = definition != null && definition.FrequencyPerHundredTiles > 0f
                ? definition.FrequencyPerHundredTiles
                : 10f;
            var tiles = result.tiles;
            var width = tiles.GetLength(0);
            var height = tiles.GetLength(1);
            var targetCount = Mathf.Max(1, Mathf.RoundToInt(width * density / 100f));
            var occupiedColumns = new HashSet<int>();
            var attempts = Mathf.Max(width * 4, targetCount * 12);
            for (var attempt = 0; attempt < attempts && hempPatches.Count < targetCount; attempt++)
            {
                var x = random.Next(2, width - 2);
                if (occupiedColumns.Contains(x) ||
                    Mathf.Abs(x - result.spawnPoint.x) <= 5 ||
                    Mathf.Abs(x - result.altarPosition.x) <= 3)
                    continue;
                var surfaceY = FindSurface(tiles, x, height);
                if (surfaceY < 0 || surfaceY + 1 >= height ||
                    !tiles[x, surfaceY].isNaturalTerrain || !tiles[x, surfaceY + 1].IsAir)
                    continue;
                var supportCell = new Vector3Int(x, surfaceY, 0);
                if (IsNearCatnipPatch(supportCell, 2f) ||
                    IsNearSurfaceDecoration(supportCell, 2f))
                    continue;
                occupiedColumns.Add(x);
                CreateHempPatch($"hemp_{hempPatches.Count:00}",
                    supportCell, random.Next(2) == 0);
            }
        }

        private bool IsNearCatnipPatch(Vector3Int supportCell, float minimumDistance)
        {
            var minimumDistanceSquared = minimumDistance * minimumDistance;
            foreach (var patch in catnipPatches.Values)
            {
                var deltaX = patch.SupportCell.x - supportCell.x;
                var deltaY = patch.SupportCell.y - supportCell.y;
                if (deltaX * deltaX + deltaY * deltaY <= minimumDistanceSquared)
                    return true;
            }
            return false;
        }

        private bool IsNearSurfaceDecoration(Vector3Int supportCell, float minimumDistance)
        {
            var minimumDistanceSquared = minimumDistance * minimumDistance;
            foreach (var decorationCell in surfaceDecorationSupportCells)
            {
                var deltaX = decorationCell.x - supportCell.x;
                var deltaY = decorationCell.y - supportCell.y;
                if (deltaX * deltaX + deltaY * deltaY <= minimumDistanceSquared)
                    return true;
            }
            return false;
        }

        private void CreateHempPatch(string id, Vector3Int supportCell, bool flipX)
        {
            var visual = new GameObject($"Hemp_{id}");
            visual.transform.SetParent(decorationRoot, false);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = artCatalog.Find("hemp")?.Sprite;
            renderer.flipX = flipX;
            renderer.sortingOrder = 3;
            if (renderer.sprite == null)
                RuntimePlaceholderVisual.Configure(renderer, new Color(.36f, .66f, .18f), .45f, 3);
            AlignSurfaceVisual(renderer, supportCell);
            hempPatches.Add(id, new HempPatch
            {
                Id = id,
                SupportCell = supportCell,
                Renderer = renderer
            });
            visual.SetActive(HasSolidRuntimeSupport(supportCell));
        }

        public bool TryHarvestCatnip(Vector2 playerPosition, float radius,
            Nyangbingo.Inventory.Inventory inventory, out int harvested)
        {
            harvested = 0;
            if (inventory == null || radius <= 0f) return false;
            CatnipPatch nearest = null;
            var nearestDistance = radius * radius;
            foreach (var patch in catnipPatches.Values)
            {
                if (!IsCatnipAvailable(patch)) continue;
                var position = patch.Renderer != null
                    ? (Vector2)patch.Renderer.transform.position
                    : new Vector2(patch.SupportCell.x + .5f, patch.SupportCell.y + 1f);
                var distance = (position - playerPosition).sqrMagnitude;
                if (distance > nearestDistance) continue;
                nearest = patch;
                nearestDistance = distance;
            }
            if (nearest == null || !inventory.TryAdd(PlayerHealthRecoveryService.CatnipItemId, 1)) return false;
            nearest.HarvestedDay = Mathf.Max(1, bootstrap?.TimeService?.Day ?? 1);
            if (nearest.Renderer != null) nearest.Renderer.gameObject.SetActive(false);
            harvested = 1;
            return true;
        }

        public bool TryHarvestHemp(Vector2 playerPosition, float radius,
            Nyangbingo.Inventory.Inventory inventory, out int harvested)
        {
            harvested = 0;
            if (inventory == null || radius <= 0f) return false;
            HempPatch nearest = null;
            var nearestDistance = radius * radius;
            foreach (var patch in hempPatches.Values)
            {
                if (!IsHempAvailable(patch)) continue;
                var position = patch.Renderer != null
                    ? (Vector2)patch.Renderer.transform.position
                    : new Vector2(patch.SupportCell.x + .5f, patch.SupportCell.y + 1f);
                var distance = (position - playerPosition).sqrMagnitude;
                if (distance > nearestDistance) continue;
                nearest = patch;
                nearestDistance = distance;
            }
            if (nearest == null || !inventory.TryAdd(HempItemId, 1)) return false;
            nearest.Harvested = true;
            if (nearest.Renderer != null) nearest.Renderer.gameObject.SetActive(false);
            harvested = 1;
            return true;
        }

        public List<CatnipPatchStateRecord> ExportCatnipPatches()
        {
            var records = new List<CatnipPatchStateRecord>();
            foreach (var patch in catnipPatches.Values)
                if (patch.HarvestedDay > 0)
                    records.Add(new CatnipPatchStateRecord
                    {
                        patchId = patch.Id,
                        harvestedDay = patch.HarvestedDay
                    });
            records.Sort((left, right) => string.CompareOrdinal(left.patchId, right.patchId));
            return records;
        }

        public List<HempPatchStateRecord> ExportHempPatches()
        {
            var records = new List<HempPatchStateRecord>();
            foreach (var patch in hempPatches.Values)
                if (patch.Harvested)
                    records.Add(new HempPatchStateRecord
                    {
                        patchId = patch.Id,
                        harvested = true
                    });
            records.Sort((left, right) => string.CompareOrdinal(left.patchId, right.patchId));
            return records;
        }

        public List<TreeHarvestStateRecord> ExportHarvestedTrees()
        {
            var records = new List<TreeHarvestStateRecord>();
            foreach (var tree in treePatches.Values)
                if (tree.Harvested)
                    records.Add(new TreeHarvestStateRecord
                    {
                        treeId = tree.Id,
                        harvested = true
                    });
            records.Sort((left, right) => string.CompareOrdinal(left.treeId, right.treeId));
            return records;
        }

        public List<RebarHarvestStateRecord> ExportHarvestedRebar()
        {
            var records = new List<RebarHarvestStateRecord>();
            foreach (var rebar in rebarPatches.Values)
                if (rebar.Harvested)
                    records.Add(new RebarHarvestStateRecord
                    {
                        rebarId = rebar.Id,
                        harvested = true
                    });
            records.Sort((left, right) => string.CompareOrdinal(left.rebarId, right.rebarId));
            return records;
        }

        public bool RestoreCatnipPatches(IEnumerable<CatnipPatchStateRecord> records)
        {
            if (records == null) return false;
            var restored = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var record in records)
                if (string.IsNullOrWhiteSpace(record.patchId) || record.harvestedDay <= 0 ||
                    !catnipPatches.ContainsKey(record.patchId) ||
                    !restored.TryAdd(record.patchId, record.harvestedDay))
                    return false;
            foreach (var patch in catnipPatches.Values)
                patch.HarvestedDay = restored.TryGetValue(patch.Id, out var day) ? day : 0;
            RefreshCatnipAvailability();
            return true;
        }

        public bool RestoreHempPatches(IEnumerable<HempPatchStateRecord> records)
        {
            if (records == null) return false;
            var restored = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in records)
                if (!record.harvested || string.IsNullOrWhiteSpace(record.patchId) ||
                    !hempPatches.ContainsKey(record.patchId) || !restored.Add(record.patchId))
                    return false;
            foreach (var patch in hempPatches.Values)
            {
                patch.Harvested = restored.Contains(patch.Id);
                if (patch.Renderer != null)
                    patch.Renderer.gameObject.SetActive(IsHempAvailable(patch));
            }
            return true;
        }

        public bool RestoreHarvestedTrees(IEnumerable<TreeHarvestStateRecord> records)
        {
            if (records == null) return false;
            var restored = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in records)
                if (!record.harvested || string.IsNullOrWhiteSpace(record.treeId) ||
                    !IsValidPersistedCoordinateDecorationId(record.treeId, "tree_", treePatches) ||
                    !restored.Add(record.treeId))
                    return false;
            foreach (var tree in treePatches.Values)
            {
                tree.Harvested = restored.Contains(tree.Id);
                if (tree.Renderer != null)
                    tree.Renderer.gameObject.SetActive(IsTreeAvailable(tree));
            }
            return true;
        }

        public bool RestoreHarvestedRebar(IEnumerable<RebarHarvestStateRecord> records)
        {
            if (records == null) return false;
            var restored = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in records)
                if (!record.harvested || string.IsNullOrWhiteSpace(record.rebarId) ||
                    !IsValidPersistedCoordinateDecorationId(record.rebarId, "rebar_", rebarPatches) ||
                    !restored.Add(record.rebarId))
                    return false;
            foreach (var rebar in rebarPatches.Values)
            {
                rebar.Harvested = restored.Contains(rebar.Id);
                if (rebar.Renderer != null)
                    rebar.Renderer.gameObject.SetActive(IsRebarAvailable(rebar));
            }
            return true;
        }

        private bool IsValidPersistedCoordinateDecorationId<TPatch>(
            string id,
            string prefix,
            IReadOnlyDictionary<string, TPatch> currentlyGenerated)
        {
            if (currentlyGenerated.ContainsKey(id)) return true;

            // Tile diffs are replayed before decorations are rebuilt. Mining a decoration's
            // supporting terrain can therefore remove it from the rebuilt runtime dictionary,
            // while its harvested record still legitimately exists in the save. Accept only
            // well-formed coordinate IDs inside the current world; arbitrary unknown IDs remain
            // rejected as corrupt data.
            if (!id.StartsWith(prefix, StringComparison.Ordinal)) return false;
            var coordinateText = id.Substring(prefix.Length);
            var separator = coordinateText.IndexOf('_');
            if (separator <= 0 || separator >= coordinateText.Length - 1 ||
                coordinateText.IndexOf('_', separator + 1) >= 0 ||
                !int.TryParse(coordinateText.Substring(0, separator), out var x) ||
                !int.TryParse(coordinateText.Substring(separator + 1), out var y))
                return false;

            var tiles = bootstrap?.Session?.LastResult.tiles;
            return tiles != null &&
                   x >= 0 && x < tiles.GetLength(0) &&
                   y >= 0 && y < tiles.GetLength(1);
        }

        public bool TryResolveTreeMiningTarget(Vector2 playerPosition, Vector2 direction, float reach,
            out string treeId, out Vector3Int hitCell)
        {
            treeId = string.Empty;
            hitCell = default;
            if (reach <= 0f || direction.sqrMagnitude <= Mathf.Epsilon) return false;
            var ray = new Ray(playerPosition, direction.normalized);
            var nearestDistance = reach;
            TreePatch nearest = null;
            foreach (var tree in treePatches.Values)
            {
                if (!IsTreeAvailable(tree) || tree.Renderer == null) continue;
                // A delivered surface tree occupies the two logical cells immediately above
                // its support. Use those cells instead of the sprite's trimmed bounds so both
                // the lower trunk and upper crown respond consistently to the claw ray.
                for (var height = 1; height <= 2; height++)
                {
                    var candidateCell = tree.SupportCell + Vector3Int.up * height;
                    var bounds = bootstrap?.TileService?.GetCellWorldBounds(candidateCell) ??
                                 new Bounds(
                                     new Vector3(
                                         candidateCell.x + .5f,
                                         candidateCell.y + .5f,
                                         0f),
                                     new Vector3(1f, 1f, 1f));
                    if (!bounds.IntersectRay(ray, out var distance) ||
                        distance < 0f || distance > nearestDistance)
                        continue;
                    nearest = tree;
                    hitCell = candidateCell;
                    nearestDistance = distance;
                }
            }
            if (nearest == null) return false;
            treeId = nearest.Id;
            return true;
        }

        public bool TryHarvestTree(string treeId, out Vector3Int supportCell, out Vector2 dropPosition)
        {
            supportCell = default;
            dropPosition = default;
            if (string.IsNullOrWhiteSpace(treeId) ||
                !treePatches.TryGetValue(treeId, out var tree) || !IsTreeAvailable(tree))
                return false;
            supportCell = tree.SupportCell;
            dropPosition = tree.Renderer != null
                ? (Vector2)tree.Renderer.transform.position
                : new Vector2(supportCell.x + .5f, supportCell.y + 1f);
            tree.Harvested = true;
            if (tree.Renderer != null) tree.Renderer.gameObject.SetActive(false);
            return true;
        }

        public bool TryResolveRebarMiningTarget(Vector2 playerPosition, Vector2 direction, float reach,
            out string rebarId, out Vector3Int hitCell)
        {
            rebarId = string.Empty;
            hitCell = default;
            if (reach <= 0f || direction.sqrMagnitude <= Mathf.Epsilon) return false;
            var ray = new Ray(playerPosition, direction.normalized);
            var nearestDistance = reach;
            RebarPatch nearest = null;
            foreach (var rebar in rebarPatches.Values)
            {
                if (!IsRebarAvailable(rebar) || rebar.Renderer == null) continue;
                var cell = rebar.SupportCell;
                var bounds = bootstrap?.TileService?.GetCellWorldBounds(cell) ??
                             new Bounds(
                                 new Vector3(cell.x + .5f, cell.y + .5f, 0f),
                                 new Vector3(1f, 1f, 1f));
                if (!bounds.IntersectRay(ray, out var distance) ||
                    distance < 0f || distance > nearestDistance)
                    continue;
                nearest = rebar;
                hitCell = cell;
                nearestDistance = distance;
            }
            if (nearest == null) return false;
            rebarId = nearest.Id;
            return true;
        }

        public bool TryHarvestRebar(string rebarId, out Vector2 dropPosition)
        {
            dropPosition = default;
            if (string.IsNullOrWhiteSpace(rebarId) ||
                !rebarPatches.TryGetValue(rebarId, out var rebar) || !IsRebarAvailable(rebar))
                return false;
            dropPosition = rebar.Renderer != null
                ? (Vector2)rebar.Renderer.transform.position
                : new Vector2(rebar.SupportCell.x + .5f, rebar.SupportCell.y + .5f);
            rebar.Harvested = true;
            if (rebar.Renderer != null) rebar.Renderer.gameObject.SetActive(false);
            return true;
        }

        private void RefreshCatnipAvailability()
        {
            var currentDay = Mathf.Max(1, bootstrap?.TimeService?.Day ?? 1);
            var respawnDays = Mathf.Max(1, Mathf.RoundToInt(ReadPositiveGlobal("catnip_respawn_days", 2f)));
            foreach (var patch in catnipPatches.Values)
            {
                if (patch.HarvestedDay > 0 && currentDay - patch.HarvestedDay >= respawnDays)
                    patch.HarvestedDay = 0;
                if (patch.Renderer != null)
                    patch.Renderer.gameObject.SetActive(IsCatnipAvailable(patch));
            }
        }

        private bool IsCatnipAvailable(CatnipPatch patch) =>
            patch != null && patch.HarvestedDay == 0 &&
            bootstrap?.TileService?.GetTile(patch.SupportCell).IsAir == false &&
            bootstrap?.TileService?.GetTile(patch.SupportCell + Vector3Int.up).IsAir == true;

        private bool IsHempAvailable(HempPatch patch) =>
            patch != null && !patch.Harvested &&
            bootstrap?.TileService?.GetTile(patch.SupportCell).IsAir == false;

        private bool IsTreeAvailable(TreePatch tree) =>
            tree != null && !tree.Harvested &&
            bootstrap?.TileService?.GetTile(tree.SupportCell).IsAir == false;

        private bool IsRebarAvailable(RebarPatch rebar) =>
            rebar != null && !rebar.Harvested &&
            bootstrap?.TileService?.GetTile(rebar.SupportCell).IsAir == false;

        private float ReadPositiveGlobal(string key, float fallback)
        {
            var definition = bootstrap?.GameDataCatalog?.FindGlobal(key);
            return definition != null && definition.TryGetFloat(out var value) && value > 0f
                ? value
                : fallback;
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
                var chestCell = new Vector3Int(chest.position.x, chest.position.y, 0);
                chestCells.Add(chestCell);
                var visual = new GameObject($"Chest_{chest.id}");
                visual.transform.SetParent(decorationRoot, false);
                var renderer = visual.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = 11;
                var opened = progress?.IsOpened(chest.id) == true;
                if (closedSprite != null)
                    renderer.sprite = opened ? openSprite : closedSprite;
                else
                    RuntimePlaceholderVisual.Configure(renderer,
                        opened ? new Color(.35f, .35f, .4f) : new Color(.85f, .58f, .16f), .75f, 11);
                AlignVisualToCellBase(renderer, chestCell);
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

            // 월드 전경 Grid와 같은 좌표 공간을 쓰도록 부모를 맞춘다.
            var worldGrid = bootstrap?.WorldRenderer != null
                ? bootstrap.WorldRenderer.GetComponentInParent<Grid>()
                : null;
            groundCoverRoot = new GameObject("SurfaceGroundCoverGrid");
            if (worldGrid != null)
            {
                groundCoverRoot.transform.SetParent(worldGrid.transform, false);
            }
            else
            {
                groundCoverRoot.AddComponent<Grid>();
                groundCoverRoot.transform.SetParent(transform, false);
            }

            var layer = new GameObject("SurfaceGroundCover", typeof(Tilemap),
                typeof(UnityEngine.Tilemaps.TilemapRenderer));
            layer.transform.SetParent(groundCoverRoot.transform, false);
            var tilemap = layer.GetComponent<Tilemap>();
            surfaceGroundCoverTilemap = tilemap;
            // 전경 타일과 동일한 기본 앵커를 유지한다. (0.5,0으로 바꾸면 풀이 반 칸 내려가 보인다.)
            if (bootstrap?.WorldRenderer?.Foreground != null)
                tilemap.tileAnchor = bootstrap.WorldRenderer.Foreground.tileAnchor;
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
            // The shared event is also raised when wallpaper is removed. Decorations only
            // lose their support when the foreground at this cell is actually gone.
            if (HasSolidRuntimeSupport(cell)) return;
            ClearGroundCoverAt(cell);
            // 지표 흙을 파면 그 위 공기 칸에 걸쳐 보이던 풀도 같이 제거한다.
            ClearGroundCoverAt(cell + Vector3Int.up);
            foreach (var tree in treePatches.Values)
            {
                if (tree.SupportCell != cell || tree.Harvested) continue;
                var dropPosition = tree.Renderer != null
                    ? (Vector2)tree.Renderer.transform.position
                    : (Vector2)(bootstrap?.TileService?.GetCellVisualAnchorWorld(
                        cell + Vector3Int.up) ??
                                new Vector3(cell.x + .5f, cell.y + 1f, 0f));
                tree.Harvested = true;
                if (tree.Renderer != null) tree.Renderer.gameObject.SetActive(false);
                WorldItemDropRequest.Request(
                    bootstrap?.GameDataCatalog?.FindItem(WoodItemId), 1, dropPosition);
            }
            foreach (var rebar in rebarPatches.Values)
            {
                if (rebar.SupportCell != cell || rebar.Harvested) continue;
                var dropPosition = rebar.Renderer != null
                    ? (Vector2)rebar.Renderer.transform.position
                    : (Vector2)(bootstrap?.TileService?.GetCellVisualAnchorWorld(cell) ??
                                new Vector3(cell.x + .5f, cell.y, 0f));
                rebar.Harvested = true;
                if (rebar.Renderer != null) rebar.Renderer.gameObject.SetActive(false);
                WorldItemDropRequest.Request(
                    bootstrap?.GameDataCatalog?.FindItem(RebarItemId), 1, dropPosition);
            }
            RemoveDecorationsSupportedBy(cell);
            foreach (var patch in catnipPatches.Values)
            {
                if (patch.SupportCell != cell) continue;
                // OnTileBroken is raised after the support foreground became air, so querying
                // IsCatnipAvailable here would always fail. HarvestedDay is the pre-break
                // maturity state and prevents duplicate drops from an already harvested patch.
                var shouldDrop = patch.HarvestedDay == 0;
                var dropPosition = patch.Renderer != null
                    ? (Vector2)patch.Renderer.transform.position
                    : (Vector2)(bootstrap?.TileService?.GetCellVisualAnchorWorld(
                        cell + Vector3Int.up) ??
                                new Vector3(cell.x + .5f, cell.y + 1f, 0f));
                patch.HarvestedDay = Mathf.Max(1, bootstrap?.TimeService?.Day ?? 1);
                if (patch.Renderer != null) patch.Renderer.gameObject.SetActive(false);
                if (shouldDrop)
                    WorldItemDropRequest.Request(
                        bootstrap?.GameDataCatalog?.FindItem(PlayerHealthRecoveryService.CatnipItemId),
                        1, dropPosition);
            }
            foreach (var patch in hempPatches.Values)
            {
                if (patch.SupportCell != cell) continue;
                var shouldDrop = !patch.Harvested;
                var dropPosition = patch.Renderer != null
                    ? (Vector2)patch.Renderer.transform.position
                    : (Vector2)(bootstrap?.TileService?.GetCellVisualAnchorWorld(
                        cell + Vector3Int.up) ??
                                new Vector3(cell.x + .5f, cell.y + 1f, 0f));
                patch.Harvested = true;
                if (patch.Renderer != null) patch.Renderer.gameObject.SetActive(false);
                if (shouldDrop)
                    WorldItemDropRequest.Request(
                        bootstrap?.GameDataCatalog?.FindItem(HempItemId), 1, dropPosition);
            }
            // A harvested catnip patch may be covered by a player block. If that block is
            // later mined after the respawn delay, reveal the patch immediately instead of
            // waiting for another day transition.
            RefreshCatnipAvailability();
        }

        private void ClearGroundCoverAt(Vector3Int cell)
        {
            if (surfaceGroundCoverTilemap == null) return;
            if (surfaceGroundCoverTilemap.GetTile(cell) == null) return;
            surfaceGroundCoverTilemap.SetTile(cell, null);
            surfaceGroundCoverTilemap.RefreshTile(cell);
        }

        private void RemoveDecorationsSupportedBy(Vector3Int supportCell)
        {
            if (decorationSupportCells.Count == 0) return;
            var doomed = new List<Transform>();
            foreach (var pair in decorationSupportCells)
            {
                if (pair.Key == null) continue;
                if (pair.Value.x == supportCell.x && pair.Value.y == supportCell.y)
                    doomed.Add(pair.Key);
            }

            for (var index = 0; index < doomed.Count; index++)
            {
                var visual = doomed[index];
                decorationSupportCells.Remove(visual);
                if (visual == null) continue;
                // Disable immediately so a tree cannot remain visible or collidable until
                // Unity processes the deferred Destroy at the end of the frame.
                visual.gameObject.SetActive(false);
                Destroy(visual.gameObject);
            }
        }

        private void PlaceSurfaceDecorations(WorldGenerationResult result, System.Random random)
        {
            var width = result.tiles.GetLength(0);
            var definition = bootstrap?.GameDataCatalog?.FindMineralTier(WoodItemId);
            var density = definition != null && definition.FrequencyPerHundredTiles > 0f
                ? definition.FrequencyPerHundredTiles
                : 8f;
            var targetCount = Mathf.Max(1, Mathf.RoundToInt(width * density / 100f));
            var occupiedColumns = new HashSet<int>();
            var attempts = Mathf.Max(width * 5, targetCount * 16);
            for (var attempt = 0; attempt < attempts && treePatches.Count < targetCount; attempt++)
            {
                var x = random.Next(2, width - 2);
                TrySpawnSurface(result, random, occupiedColumns, x, $"tree_{random.Next(3)}");
            }
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
            var supportCell = new Vector3Int(x, surfaceY, 0);
            // Reserve deterministic tree positions even when a loaded tile diff means the
            // tree itself is no longer rendered. Hemp IDs must not shift across save/load.
            surfaceDecorationSupportCells.Add(supportCell);
            occupiedColumns.Add(x);
            var treeId = $"tree_{supportCell.x}_{supportCell.y}";
            var tree = new TreePatch
            {
                Id = treeId,
                SupportCell = supportCell
            };
            treePatches.Add(treeId, tree);
            // LastResult contains the deterministic seed layout. TileService also includes
            // loaded/mined diffs, so it is authoritative when decorations are rebuilt.
            if (bootstrap?.TileService?.GetTile(supportCell).IsAir != false) return;

            var art = artCatalog.Find(id);
            if (art?.Sprite == null) return;
            // 전경 타일 비주얼 윗면(드롭과 동일 +0.5)에 스프라이트 하단(피벗 무관)을 맞춘다.
            tree.Renderer = Spawn(id, art, Vector2.zero,
                random.Next(2) == 0, supportCell);
            AlignSurfaceVisual(tree.Renderer, supportCell);
        }

        /// <summary>
        /// 표면 식생 Y: 논리 지면 윗변(surfaceY+1) + 전경 비주얼 보정 − bounds.min.y(피벗→하단).
        /// </summary>
        public static float ComputeSurfaceDecorationWorldY(int surfaceY, Sprite sprite)
        {
            var visibleSurfaceY = surfaceY + 1f + MainGameWorldDropRuntime.VisualSurfaceOffset;
            if (sprite == null) return visibleSurfaceY;
            return visibleSurfaceY - sprite.bounds.min.y;
        }

        private void AlignSurfaceVisual(SpriteRenderer spriteRenderer, Vector3Int supportCell) =>
            AlignVisualToCellBase(spriteRenderer, supportCell + Vector3Int.up);

        private void AlignVisualToCellBase(SpriteRenderer spriteRenderer, Vector3Int cell)
        {
            if (spriteRenderer == null) return;
            var tileService = bootstrap?.TileService;
            if (tileService != null)
            {
                tileService.AlignSpriteBoundsToCellBase(spriteRenderer, cell);
                return;
            }

            var spriteBounds = spriteRenderer.bounds;
            spriteRenderer.transform.position += new Vector3(
                cell.x + .5f - spriteBounds.center.x,
                cell.y - spriteBounds.min.y,
                0f);
        }

        private void PlaceRuinDecorations(WorldGenerationResult result, System.Random random)
        {
            var tiles = result.tiles;
            var width = tiles.GetLength(0);
            var height = tiles.GetLength(1);
            var visited = new bool[width, height];
            var rebarCandidates = new List<Vector2Int>();
            var pillarCells = new HashSet<Vector2Int>();
            for (var y = 1; y < height - 1; y++)
            for (var x = 1; x < width - 1; x++)
            {
                if (visited[x, y] || !IsRuinWall(tiles, x, y)) continue;
                var cells = CollectConnectedRuin(tiles, x, y, visited);
                if (cells.Count == 0) continue;

                var pillarCell = cells[0];
                foreach (var cell in cells)
                {
                    if (cell.x < pillarCell.x || cell.x == pillarCell.x && cell.y < pillarCell.y)
                        pillarCell = cell;
                    if (IsExposedRuinCell(tiles, cell.x, cell.y))
                        rebarCandidates.Add(cell);
                }
                pillarCells.Add(pillarCell);
                SpawnRuinDecoration("ruin_pillar", pillarCell, random.Next(2) == 0);
            }

            rebarCandidates.RemoveAll(cell => pillarCells.Contains(cell));
            for (var index = rebarCandidates.Count - 1; index > 0; index--)
            {
                var swapIndex = random.Next(index + 1);
                (rebarCandidates[index], rebarCandidates[swapIndex]) =
                    (rebarCandidates[swapIndex], rebarCandidates[index]);
            }
            var definition = bootstrap?.GameDataCatalog?.FindMineralTier(RebarItemId);
            var density = definition != null && definition.FrequencyPerHundredTiles > 0f
                ? definition.FrequencyPerHundredTiles
                : 6f;
            var targetCount = Mathf.Min(rebarCandidates.Count,
                Mathf.Max(1, Mathf.RoundToInt(width * density / 100f)));
            for (var index = 0; index < targetCount; index++)
                SpawnRebarPatch(rebarCandidates[index], random.Next(2) == 0);
        }

        private void SpawnRuinDecoration(string id, Vector2Int cell, bool flipX)
        {
            var supportCell = new Vector3Int(cell.x, cell.y, 0);
            if (!HasSolidRuntimeSupport(supportCell)) return;
            var art = artCatalog.Find(id);
            if (art?.Sprite != null)
                Spawn(id, art,
                    bootstrap?.TileService?.GetCellVisualAnchorWorld(supportCell) ??
                    new Vector3(cell.x + .5f, cell.y, 0f), flipX,
                    supportCell);
        }

        private void SpawnRebarPatch(Vector2Int cell, bool flipX)
        {
            var supportCell = new Vector3Int(cell.x, cell.y, 0);
            var id = $"rebar_{cell.x}_{cell.y}";
            var patch = new RebarPatch
            {
                Id = id,
                SupportCell = supportCell
            };
            rebarPatches.Add(id, patch);
            if (!HasSolidRuntimeSupport(supportCell)) return;
            var art = artCatalog.Find("ruin_rebar");
            if (art?.Sprite == null) return;
            patch.Renderer = Spawn("ruin_rebar", art,
                bootstrap?.TileService?.GetCellVisualAnchorWorld(supportCell) ??
                new Vector3(cell.x + .5f, cell.y, 0f), flipX, supportCell);
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

        private static bool IsExposedRuinCell(TileData[,] tiles, int x, int y) =>
            tiles[x + 1, y].IsAir || tiles[x - 1, y].IsAir ||
            tiles[x, y + 1].IsAir || tiles[x, y - 1].IsAir;

        private SpriteRenderer Spawn(string id, WorldDecorationArtCatalog.Entry art, Vector2 position, bool flipX,
            Vector3Int supportCell)
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
            decorationSupportCells[visual.transform] = supportCell;
            return renderer;
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
            chestCells.Clear();
            decorationSupportCells.Clear();
            surfaceDecorationSupportCells.Clear();
            catnipPatches.Clear();
            hempPatches.Clear();
            treePatches.Clear();
            rebarPatches.Clear();
        }

        private void OnDestroy()
        {
            if (bootstrap != null) bootstrap.WorldReady -= Rebuild;
            bootstrap?.TileService?.ClearForegroundPlacementBlocker(IsForegroundPlacementBlocked);
            GameEvents.OnTileBroken -= HandleTileBroken;
            GameEvents.OnDayStart -= RefreshCatnipAvailability;
        }
    }
}
