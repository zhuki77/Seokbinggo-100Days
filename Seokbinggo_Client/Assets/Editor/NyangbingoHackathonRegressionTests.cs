using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Nyangbingo.Save;
using Nyangbingo.Core;
using Nyangbingo.UI;
using Nyangbingo.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class NyangbingoHackathonRegressionTests
{
    [MenuItem("Nyangbingo/QA/Diagnose Cooling Temperature Connection (3 passes)")]
    public static void DiagnoseCoolingTemperatureConnection()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop gameplay before the isolated cooling diagnostic.");
        var folder = Path.Combine(Path.GetTempPath(), "NyangbingoCoolingQA-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var rows = new System.Collections.Generic.List<string>
        {
            "SYNTHETIC SERVICE DIAGNOSTIC. No normal crafting, placement UI, save/reload, or novice proof.",
            "Uses current catalog and RoomTempService/SealSystem with synthetic terrain and reflected placed records.",
            "No player save or production scene is changed. cold_device intended balance remains unspecified.",
            "pass,room,record,coolingFlag,centerC,minInsideC,lastInsideC,outsideXC,outsideYC,coreCount"
        };
        try
        {
            for (var pass = 1; pass <= 3; pass++) TestCoolingTemperatureConnection(pass, rows);
            rows.Add("DIAGNOSTIC COMPLETED: cold_device contributes zero room-temperature change in these fixtures; this is a finding, not feature acceptance.");
            File.WriteAllLines(Path.Combine(folder, "result.txt"), rows);
            Debug.Log("[CoolingConnectionQA] COMPLETED 3/3. Output: " + folder);
        }
        catch (Exception ex)
        {
            rows.Add("DIAGNOSTIC FAILED: " + ex);
            File.WriteAllLines(Path.Combine(folder, "result.txt"), rows);
            throw;
        }
    }

    private static void TestCoolingTemperatureConnection(int pass, System.Collections.Generic.List<string> rows)
    {
        Require(MainGameHudController.ResolveShelterNextRecipe(false, false) == "workbench", "first shelter step is workbench");
        Require(MainGameHudController.ResolveShelterNextRecipe(true, false) == "furnace", "placed workbench leads to furnace");
        Require(MainGameHudController.ResolveShelterNextRecipe(false, true) == "ice_core", "existing furnace skips obsolete prerequisite");
        Require(MainGameHudController.ResolveShelterNextRecipe(true, true) == "ice_core", "furnace leads to core");
        var catalog = AssetDatabase.LoadAssetAtPath<Nyangbingo.Data.GameDataCatalog>("Assets/Data/SO/GameDataCatalog.asset");
        Require(catalog != null, "current catalog exists");
        Require(!CoolingSourceRuntime.IsCoolingDefinition("cold_device"), "cold_device is absent from cooling-source registration");
        Require(CoolingSourceRuntime.IsCoolingDefinition("ice_core"), "ice_core is a registered cooling definition");
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var host = new GameObject("Isolated cooling temperature fixture");
            SceneManager.MoveGameObjectToScene(host, scene);
            var environment = host.AddComponent<MainGameEnvironmentState>();
            var records = (IDictionary)typeof(MainGameEnvironmentState)
                .GetField("byObjectId", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(environment);
            var entryType = typeof(MainGameEnvironmentState).GetNestedType("Entry", BindingFlags.NonPublic);
            var core = new Vector3Int(16, 16, 0);
            void Put(string id, string definition, Vector3Int cell, bool active)
            {
                var entry = Activator.CreateInstance(entryType);
                entryType.GetField("Record").SetValue(entry, new PlacedObjectRecord
                {
                    objectId = id, definitionId = definition, position = new Vector2(cell.x + .5f, cell.y + .5f)
                });
                entryType.GetField("Cell").SetValue(entry, cell);
                entryType.GetField("CoolingActive").SetValue(entry, active);
                records[id] = entry;
            }
            foreach (var sealedRoom in new[] { false, true })
            {
                var tiles = new TileData[32, 32];
                for (var x = 0; x < 32; x++)
                for (var y = 0; y < 32; y++)
                {
                    var wall = sealedRoom && ((x == 10 || x == 22) && y >= 9 && y <= 23 ||
                                              (y == 9 || y == 23) && x >= 10 && x <= 22);
                    tiles[x, y] = TileData.CreateNatural(wall ? WorldTileTypes.Stone : WorldTileTypes.Air, wall ? 1 : 0);
                }
                var tileService = new TileService(tiles, null, null, 12345);
                using var seal = new SealSystem(tileService, catalog.SealWhitelist);
                Require(seal.IsCoreWindowSealed(core) == sealedRoom, "fixture has expected seal state");
                var temperature = new RoomTempService(catalog, seal, null, environment);
                foreach (var definition in new[] { "none", "cold_device", "ice_core", "both" })
                foreach (var active in new[] { false, true })
                {
                    records.Clear();
                    if (definition == "cold_device" || definition == "ice_core") Put("target", definition, core, active);
                    if (definition == "both")
                    {
                        Put("core", "ice_core", core, active);
                        Put("device", "cold_device", core + Vector3Int.right, active);
                    }
                    var cells = new System.Collections.Generic.List<Vector3Int>();
                    environment.CopyIceCoreCells(cells);
                    var center = temperature.ResolveExact(core);
                    var minInside = temperature.ResolveExact(core + new Vector3Int(-4, -5, 0));
                    var lastInside = temperature.ResolveExact(core + new Vector3Int(3, 4, 0));
                    var outsideX = temperature.ResolveExact(core + new Vector3Int(4, 0, 0));
                    var outsideY = temperature.ResolveExact(core + new Vector3Int(0, 5, 0));
                    rows.Add(FormattableString.Invariant($"{pass},{(sealedRoom ? "sealed" : "open")},{definition},{active},{center},{minInside},{lastInside},{outsideX},{outsideY},{cells.Count}"));
                    var hasCore = definition == "ice_core" || definition == "both";
                    var inspection = temperature.InspectShelter((Vector3)core + new Vector3(.5f, .5f));
                    Require(inspection.HasCore == hasCore, "shelter guide does not mistake cold_device for core");
                    if (hasCore)
                    {
                        Require(inspection.InRange && inspection.Sealed == sealedRoom,
                            "shelter guide uses actual range and seal state");
                        Require(inspection.CoreDelta == (sealedRoom ? -10 : -5), "guide uses actual core contribution");
                        Require(!temperature.InspectShelter((Vector3)core + new Vector3(4.5f, .5f)).InRange,
                            "guide distinguishes outside cooling range");
                        Require(!sealedRoom || !inspection.HasLeak, "sealed guide has no leak marker");
                    }
                    var expected = hasCore ? (sealedRoom ? -10f : -5f) : 0f;
                    Require(Mathf.Approximately(center, expected) && Mathf.Approximately(minInside, expected) &&
                            Mathf.Approximately(lastInside, expected), "observed current cooling contribution");
                    Require(Mathf.Approximately(outsideX, 0f) && Mathf.Approximately(outsideY, 0f), "core range excludes right and top boundary");
                    Require(cells.Count == (hasCore ? 1 : 0), "only ice_core supplies room-temperature cells");
                }
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [MenuItem("Nyangbingo/QA/Run Station Range Regression (3 passes)")]
    public static void RunStationRanges()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run isolated range regression outside play mode.");
        for (var pass = 1; pass <= 3; pass++)
        {
            TestStationRanges();
            Debug.Log($"[StationRangeRegression] PASS {pass}/3: five station types, default/opened distance boundaries, competing station, missing records. Isolated synthetic query test, not gameplay.");
        }
    }

    private static void TestStationRanges()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var host = new GameObject("Isolated station range fixture");
            SceneManager.MoveGameObjectToScene(host, scene);
            var environment = host.AddComponent<MainGameEnvironmentState>();
            var player = host.AddComponent<MainGameRaidTarget>();
            var source = host.AddComponent<MainGameBossSummonUiController>();
            var ui = host.AddComponent<MainGameCraftingUiController>();
            void Set(object target, string field, object value) => target.GetType()
                .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
            Set(source, "initialized", true);
            Set(source, "environmentState", environment);
            Set(source, "playerTarget", player);
            Set(ui, "stationSource", source);
            var records = (IDictionary)typeof(MainGameEnvironmentState)
                .GetField("byObjectId", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(environment);
            var entryType = typeof(MainGameEnvironmentState).GetNestedType("Entry", BindingFlags.NonPublic);
            void Put(string id, CraftingStation station, float x)
            {
                var entry = Activator.CreateInstance(entryType);
                entryType.GetField("Record").SetValue(entry, new PlacedObjectRecord
                {
                    objectId = id, definitionId = MainGameBossSummonUiController.DefinitionIdForStation(station),
                    position = new Vector2(x, 0f)
                });
                records[id] = entry;
            }
            CraftingStation Resolve() => (CraftingStation)typeof(MainGameCraftingUiController)
                .GetMethod("NearbyStation", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(ui, null);
            foreach (var station in new[] { CraftingStation.Workbench, CraftingStation.Furnace,
                         CraftingStation.IceAnvil, CraftingStation.Foundry, CraftingStation.Smithy })
            {
                records.Clear();
                Set(ui, "openedStation", station);
                foreach (var distance in new[] { 0f, 1.5f, 1.5001f, 2.5f, 2.5001f, 10f })
                {
                    Put("target", station, distance);
                    Require(source.TryGetNearbyCraftingStationPosition(station, out _) == (distance <= 1.5f), "default distance " + station + " " + distance);
                    Require(source.TryGetNearbyCraftingStationPosition(station, out _, MainGameTurretRuntime.InteractionRange) == (distance <= 2.5f), "opened distance " + station + " " + distance);
                    Require((Resolve() == station) == (distance <= 2.5f), "UI selected station boundary " + station + " " + distance);
                }
                Put("target", station, 2f);
                Put("other", station == CraftingStation.Workbench ? CraftingStation.Furnace : CraftingStation.Workbench, .1f);
                Require(Resolve() == station, "opened station wins over a closer different station");
                records.Remove("target");
                Require(!source.TryGetNearbyCraftingStationPosition(station, out _, 2.5f), "removed target unavailable");
                Require(Resolve() != station, "UI does not retain removed station availability");
            }
            records.Clear();
            Require(Resolve() == CraftingStation.None, "no stations means unavailable");
            Require(!source.TryGetNearbyCraftingStationPosition(CraftingStation.None, out _, 2.5f), "None never available");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [MenuItem("Nyangbingo/Run Hackathon Regression Tests")]
    public static void RunAll()
    {
        for (var pass = 1; pass <= 3; pass++)
        {
            TestIronEcho();
            TestDemoArchive();
            Debug.Log($"[HackathonRegression] PASS {pass}/3: iron echo and demo archive");
        }
    }

    private static void TestIronEcho()
    {
        var tiles = new TileData[24, 24];
        var origin = new Vector3Int(12, 12, 0);
        foreach (var offset in new[] { Vector3Int.right * 3, Vector3Int.left * 3,
                     Vector3Int.up * 3, Vector3Int.down * 3 })
        {
            for (var x = 0; x < 24; x++)
            for (var y = 0; y < 24; y++)
                tiles[x, y] = TileData.CreateNatural(WorldTileTypes.Air, 0);
            tiles[12, 12] = TileData.CreateNatural(WorldTileTypes.IronOre, 1);
            var neighbor = origin + offset;
            tiles[neighbor.x, neighbor.y] = TileData.CreateNatural(WorldTileTypes.IronOre, 1);
            var service = new TileService(tiles, null, null, 12345);
            var material = service.GetTile(origin).elementType;
            Require(service.TryBreakForeground(origin, 1, out _, out _), "iron break succeeds");
            Require(service.GetTile(origin).IsAir, "source cell cleared before echo");
            Require(MainGamePlayerController.TryFindIronVeinDirection(service, origin, material,
                out var direction), "echo survives source removal");
            var expected = offset.x > 0 ? "동쪽" : offset.x < 0 ? "서쪽" : offset.y > 0 ? "북쪽" : "남쪽";
            Require(direction == expected, "echo direction");
            Require(!MainGamePlayerController.TryFindIronVeinDirection(service, origin,
                WorldTileTypes.Stone, out _), "non iron does not echo");
            tiles[neighbor.x, neighbor.y] = tiles[neighbor.x, neighbor.y].WithoutForeground();
            Require(!MainGamePlayerController.TryFindIronVeinDirection(service, origin, material,
                out _), "no remaining iron means no echo");
            tiles[12, 12] = TileData.CreateNatural(WorldTileTypes.IronOre, 1);
            Require(!MainGamePlayerController.TryFindIronVeinDirection(service, origin, material,
                out _), "source is never its own echo target");
        }
    }

    private static void TestDemoArchive()
    {
        // Never instantiate SaveManager or write Application.persistentDataPath in this test.
        var folder = Path.Combine(Path.GetTempPath(), "NyangbingoArchiveTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, "save.json");
            File.WriteAllText(path, "original-player-save");
            File.WriteAllText(path + ".bak", "older-player-save");
            var archive = typeof(SaveManager).GetMethod("ArchiveBeforeDemoLoad",
                BindingFlags.NonPublic | BindingFlags.Static);
            Require(archive != null, "archive method exists");
            archive.Invoke(null, new object[] { path });
            archive.Invoke(null, new object[] { path });
            var copies = Directory.GetFiles(folder, "*.before-demo-*");
            Require(copies.Length == 4, "repeated rehearsals keep distinct backups");
            Require(copies.Count(p => File.ReadAllText(p) == "original-player-save") == 2,
                "primary copies retained");
            Require(copies.Count(p => File.ReadAllText(p) == "older-player-save") == 2,
                "backup copies retained");
            Require(File.ReadAllText(path) == "original-player-save", "archive leaves primary untouched");
            Require(typeof(SaveManager).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance) == null,
                "no destructive startup slot cleanup");
        }
        finally
        {
            // Only files created inside this unique test directory are removed.
            foreach (var file in Directory.GetFiles(folder)) File.Delete(file);
            Directory.Delete(folder);
        }
    }

    private static void Require(bool value, string label)
    {
        if (!value) throw new InvalidOperationException("[HackathonRegression] FAIL: " + label);
    }
}
