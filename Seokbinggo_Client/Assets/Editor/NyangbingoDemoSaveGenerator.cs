using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nyangbingo.Bosses;
using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using Nyangbingo.Save;
using Nyangbingo.World;
using UnityEditor;
using UnityEngine;

namespace Nyangbingo.Editor
{
    public static class NyangbingoDemoSaveGenerator
    {
        private const int RequestedSeed = 12345;
        private const float NightStartGameSeconds = 900f;
        private const string CatalogPath = "Assets/Data/SO/GameDataCatalog.asset";
        private const string ConfigPath = "Assets/Data/SO/WorldGenerationConfig.asset";
        private const string OutputPath = "Assets/StreamingAssets/DemoSaves";

        private readonly struct DemoProfile
        {
            public DemoProfile(int day, int roomWidth, int roomHeight, float sealPercent)
            {
                Day = day;
                RoomWidth = roomWidth;
                RoomHeight = roomHeight;
                SealPercent = sealPercent;
            }

            public int Day { get; }
            public int RoomWidth { get; }
            public int RoomHeight { get; }
            public float SealPercent { get; }
        }

        [MenuItem("Tools/Nyangbingo/Generate Official Demo Saves")]
        public static void GenerateOfficialDemoSaves()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameDataCatalog>(CatalogPath);
            var config = AssetDatabase.LoadAssetAtPath<WorldGenerationConfig>(ConfigPath);
            if (catalog == null || config == null || !catalog.IsValid)
                throw new InvalidOperationException("Official GameDataCatalog or WorldGenerationConfig is unavailable.");

            var profiles = new[]
            {
                new DemoProfile(1, 6, 4, 10f),
                new DemoProfile(15, 12, 10, 50f),
                new DemoProfile(30, 20, 12, 100f)
            };
            Directory.CreateDirectory(OutputPath);
            foreach (var profile in profiles)
            {
                var generator = new MapGenerator(config);
                var world = generator.GenerateDetailed(RequestedSeed);
                if (!world.passedValidation)
                    throw new InvalidOperationException($"Demo day {profile.Day}: fixed world seed failed validation.");
                if (!TryBuildRoom(world, profile.RoomWidth, profile.RoomHeight, out var changes, out var roomCenter))
                    throw new InvalidOperationException($"Demo day {profile.Day}: no deterministic sealed room was found.");

                var save = BuildSave(catalog, world, profile, changes, roomCenter);
                ValidateSave(catalog, config, profile, save, roomCenter);
                File.WriteAllText(Path.Combine(OutputPath, $"day-{profile.Day}.json"),
                    JsonUtility.ToJson(save, true));
            }

            AssetDatabase.Refresh();
            Debug.Log("[Nyangbingo] Official demo saves generated: day 1/15/30, " +
                      $"requestedSeed={RequestedSeed}, acceptedSeed={new MapGenerator(config).GenerateDetailed(RequestedSeed).acceptedSeed}.");
        }

        private static SaveGame BuildSave(GameDataCatalog catalog, WorldGenerationResult world,
            DemoProfile profile, List<TileChangeRecord> changes, Vector2Int roomCenter)
        {
            var generatedSpawn = world.spawnPoint;
            var spawnTileService = new TileService(world.tiles, null, catalog, world.acceptedSeed);
            IWorldSafeSpawnResolver spawnResolver = spawnTileService;
            var playerSpawn = spawnResolver.TryResolveSafeSurfaceSpawn(
                generatedSpawn.x, .38f, out var safeSurfaceSpawn)
                ? safeSurfaceSpawn
                : new Vector2(generatedSpawn.x + .5f, generatedSpawn.y + .5f);
            var save = new SaveGame
            {
                seed = world.acceptedSeed,
                day = profile.Day,
                timeOfDaySec = NightStartGameSeconds,
                sealPct = profile.SealPercent,
                tileChanges = changes,
                playerState = new PlayerStateRecord
                {
                    hasValue = true,
                    position = new Vector3(playerSpawn.x, playerSpawn.y, 0f),
                    currentHealth = 100,
                    maxHealth = 100,
                    hasTemperature = true,
                    temperature = 40f
                },
                timeState = new TimeStateRecord
                {
                    hasValue = true,
                    day = profile.Day,
                    timeOfDayGameSeconds = NightStartGameSeconds,
                    isNight = true
                },
                regularEncounter = new RegularEncounterStateRecord
                {
                    hasValue = false,
                    day = profile.Day,
                    isNight = true
                },
                baekjungProgress = new BaekjungSchedulerState(),
                forcedBossEncounters = new List<ForcedBossEncounterRecord>
                {
                    new ForcedBossEncounterRecord { bossId = "imugi_boss", triggered = false }
                },
                chests = world.chests.Select(chest => new ChestStateRecord
                {
                    chestId = chest.id,
                    position = chest.position,
                    opened = false
                }).OrderBy(chest => chest.chestId, StringComparer.Ordinal).ToList()
            };

            save.placedObjectRecords.Add(new PlacedObjectRecord
            {
                objectId = $"demo_{profile.Day}_ice_core",
                definitionId = CoolingSourceRuntime.IceStorageId,
                position = new Vector2(roomCenter.x + .5f, roomCenter.y + .5f),
                rotationDegrees = 0f
            });
            save.coolingSources.Add(new CoolingSourceStateRecord
            {
                objectId = $"demo_{profile.Day}_ice_core",
                definitionId = CoolingSourceRuntime.IceStorageId,
                remainingGameSeconds = 0f
            });

            if (profile.Day == 1)
            {
                AddInventory(save, catalog, "insul_wall", 12);
            }
            else
            {
                AddInventory(save, catalog, "dokkaebi_club", 1);
                AddInventory(save, catalog, FanItemIds.Hapjukseon, 1);
                AddConfirmedDayFifteenEquipment(save, catalog);
                AddPlacedModule(save, "insul_wall", roomCenter + new Vector2Int(-2, 0));
                AddPlacedModule(save, "door", roomCenter + new Vector2Int(-1, 0));
                save.placedObjectRecords.Add(new PlacedObjectRecord
                {
                    objectId = $"demo_{profile.Day}_sieve",
                    definitionId = "sieve",
                    position = new Vector2(world.spawnPoint.x + 4.5f, world.spawnPoint.y + .5f),
                    rotationDegrees = 0f
                });
            }

            if (profile.Day == 30)
            {
                AddInventory(save, catalog, "ssireum_satba", 1);
                AddPlacedModule(save, "roof", roomCenter + new Vector2Int(1, 0));
                AddPlacedModule(save, "jangdok", roomCenter + new Vector2Int(2, 0));
            }

            save.modulesDone = catalog.Modules
                .Where(module => module != null && module.Item != null &&
                                 save.placedObjectRecords.Any(record => record.definitionId == module.Item.Id))
                .Select(module => module.Id)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();

            save.NormalizeAfterLoad();
            return save;
        }

        private static void AddPlacedModule(SaveGame save, string definitionId, Vector2Int cell)
        {
            save.placedObjectRecords.Add(new PlacedObjectRecord
            {
                objectId = $"demo_{save.day}_{definitionId}",
                definitionId = definitionId,
                position = new Vector2(cell.x + .5f, cell.y + .5f),
                rotationDegrees = 0f
            });
        }

        private static void AddConfirmedDayFifteenEquipment(SaveGame save, GameDataCatalog catalog)
        {
            AddEquipped(save, catalog, EquipmentSlot.Head, "iron_helm");
            AddEquipped(save, catalog, EquipmentSlot.Body, "iron_armor");
            AddEquipped(save, catalog, EquipmentSlot.Feet, "iron_boots");
            AddEquipped(save, catalog, EquipmentSlot.AccessoryOne, "wind_daenggi");
            AddEquipped(save, catalog, EquipmentSlot.AccessoryTwo, "dokkaebi_gamtu");
        }

        private static void AddEquipped(SaveGame save, GameDataCatalog catalog, EquipmentSlot slot, string id)
        {
            if (catalog.FindEquipment(id) == null || catalog.FindItem(id) == null)
                throw new InvalidOperationException($"Official equipment '{id}' is missing.");
            save.equipment.Add(new EquipmentRecord { slot = slot.ToString(), equipmentId = id });
            save.ownedEquipmentIds.Add(id);
        }

        private static void AddInventory(SaveGame save, GameDataCatalog catalog, string itemId, int amount)
        {
            var item = catalog.FindItem(itemId);
            if (item == null || amount <= 0 || amount > item.MaxStack)
                throw new InvalidOperationException($"Invalid official demo inventory entry '{itemId}:{amount}'.");
            save.inventory.Add(new InventorySlot { itemId = itemId, amount = amount });
        }

        private static bool TryBuildRoom(WorldGenerationResult world, int width, int height,
            out List<TileChangeRecord> changes, out Vector2Int center)
        {
            changes = null;
            center = default;
            var occupied = new HashSet<Vector2Int>(world.chests.Select(chest => chest.position))
            {
                world.altarPosition,
                world.spawnPoint
            };

            for (var y = 8; y + height + 1 < world.height - 16; y++)
            {
                for (var x = 16; x + width + 1 < world.width - 16; x++)
                {
                    if (!HasSolidNaturalBoundary(world, occupied, x, y, width, height)) continue;
                    var candidate = new List<TileChangeRecord>();
                    var valid = true;
                    for (var ix = x; ix < x + width && valid; ix++)
                    for (var iy = y; iy < y + height; iy++)
                    {
                        var tile = world.tiles[ix, iy];
                        if (tile.IsAir) continue;
                        if (!tile.isNaturalTerrain || tile.elementType == WorldTileTypes.Bedrock ||
                            tile.elementType == WorldTileTypes.IceAltar)
                        {
                            valid = false;
                            break;
                        }
                        candidate.Add(new TileChangeRecord
                        {
                            x = ix,
                            y = iy,
                            z = 0,
                            tileId = tile.elementType,
                            placed = false
                        });
                    }
                    if (!valid) continue;
                    changes = candidate;
                    center = new Vector2Int(x + width / 2, y + height / 2);
                    return true;
                }
            }
            return false;
        }

        private static bool HasSolidNaturalBoundary(WorldGenerationResult world, HashSet<Vector2Int> occupied,
            int x, int y, int width, int height)
        {
            for (var ix = x - 1; ix <= x + width; ix++)
            for (var iy = y - 1; iy <= y + height; iy++)
            {
                if (occupied.Contains(new Vector2Int(ix, iy))) return false;
                var boundary = ix == x - 1 || ix == x + width || iy == y - 1 || iy == y + height;
                if (!boundary) continue;
                var tile = world.tiles[ix, iy];
                if (tile.IsAir || !tile.isNaturalTerrain) return false;
            }
            return true;
        }

        private static void ValidateSave(GameDataCatalog catalog, WorldGenerationConfig config,
            DemoProfile profile, SaveGame save, Vector2Int roomCenter)
        {
            var json = JsonUtility.ToJson(save, true);
            if (!SaveManager.TryDeserialize(json, out var parsed) || parsed.seed != save.seed || parsed.day != profile.Day)
                throw new InvalidOperationException($"Demo day {profile.Day}: schema round-trip failed.");

            var validationGenerator = new MapGenerator(config);
            var validationWorld = validationGenerator.GenerateDetailed(save.seed);
            if (validationWorld.acceptedSeed != save.seed)
                throw new InvalidOperationException($"Demo day {profile.Day}: accepted seed is not reproducible.");
            var tileService = new TileService(validationWorld.tiles, null, catalog, save.seed);
            if (!tileService.RestoreTileChanges(save.tileChanges))
                throw new InvalidOperationException($"Demo day {profile.Day}: tile changes cannot be replayed.");
            if (!WorldSaveAdapter.RestoreChests(save, validationGenerator,
                    new ChestProgress(catalog.FindItem)))
                throw new InvalidOperationException($"Demo day {profile.Day}: chest state cannot be replayed.");

            IWorldSafeSpawnResolver spawnResolver = tileService;
            if (!spawnResolver.TryResolveSafeSurfaceSpawn(validationWorld.spawnPoint.x, .38f,
                    out var expectedPlayerSpawn) ||
                Vector2.Distance(save.playerState.position, expectedPlayerSpawn) > .001f)
                throw new InvalidOperationException(
                    $"Demo day {profile.Day}: player is not saved at the shared safe surface spawn.");

            var expectedModules = profile.Day >= 30 ? 5 : profile.Day >= 15 ? 3 : 1;
            if (save.modulesDone.Count != expectedModules || save.modulesDone.Any(moduleId =>
                    !save.placedObjectRecords.Any(record => record.definitionId == moduleId)))
                throw new InvalidOperationException(
                    $"Demo day {profile.Day}: installed module state is inconsistent.");

            using (var seals = new SealSystem(tileService, catalog.SealWhitelist))
            {
                seals.SetPrimaryWatchPoint(new Vector3Int(roomCenter.x, roomCenter.y, 0));
                var actual = seals.SealPercent * 100f;
                if (!Mathf.Approximately(actual, profile.SealPercent))
                    throw new InvalidOperationException(
                        $"Demo day {profile.Day}: expected seal {profile.SealPercent}%, got {actual}%.");
            }
        }
    }
}
