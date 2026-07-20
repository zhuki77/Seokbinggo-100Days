using System;
using System.Collections;
using System.Reflection;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using Nyangbingo.Save;
using Nyangbingo.UI;
using Nyangbingo.World;
using UnityEditor;
using UnityEngine;

public static class NyangbingoDevBIntegrationRegressionTests
{
    private const BindingFlags InstanceMembers =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [MenuItem("Nyangbingo/Run Dev B Integration Regression Tests")]
    public static void RunAll()
    {
        TestIceStorageSealCoreLifecycle();
        TestV29InventoryLayoutContract();
        TestV29InventoryArtBindings();
        Debug.Log("[Nyangbingo] Dev B integration regression tests passed (3/3).");
    }

    private static void TestV29InventoryLayoutContract()
    {
        Require(Inventory.SlotCount == 50,
            $"v29 inventory capacity must be 50 slots (actual {Inventory.SlotCount}).");
        Require(MainGameCraftingUiController.InventoryGridColumns == 10,
            $"v29 inventory grid must have 10 columns (actual {MainGameCraftingUiController.InventoryGridColumns}).");
        Require(MainGameCraftingUiController.InventoryGridRows == 5,
            $"v29 inventory grid must have 5 rows (actual {MainGameCraftingUiController.InventoryGridRows}).");
        Require(Mathf.Approximately(MainGameCraftingUiController.InventorySlotPixelSize, 27f),
            $"v29 inventory slot art must render at 27 px (actual {MainGameCraftingUiController.InventorySlotPixelSize}).");

        var inventory = new Inventory(_ => null);
        Require(inventory.Capacity == 50 && inventory.Slots.Count == 50,
            $"A default runtime inventory must allocate 50 slots (actual {inventory.Capacity}).");
    }

    private static void TestV29InventoryArtBindings()
    {
        const string catalogPath = "Assets/Art/Gameplay/GameplayArtCatalog.asset";
        var catalog = AssetDatabase.LoadAssetAtPath<GameplayArtCatalog>(catalogPath);
        Require(catalog != null, $"Gameplay art catalog not found: {catalogPath}");

        Require(catalog.InventoryPanel != null, "v29 inventory panel art is not bound.");
        Require(catalog.InventorySlot != null, "v29 inventory slot art is not bound.");
        Require(catalog.InventorySlotSelected != null, "v29 selected inventory slot art is not bound.");
        Require(catalog.InventorySlotTopSelected != null, "v29 top selected inventory slot art is not bound.");
        Require(catalog.EquipmentCharacter != null, "v29 equipment character art is not bound.");
        Require(catalog.EquipmentHeadSlot != null, "v29 equipment head slot art is not bound.");
        Require(catalog.EquipmentBodySlot != null && catalog.EquipmentBodySlotSelected != null,
            "v29 equipment body slot art is incomplete.");
        Require(catalog.EquipmentFeetSlot != null && catalog.EquipmentFeetSlotSelected != null,
            "v29 equipment feet slot art is incomplete.");
        Require(catalog.EquipmentAccessorySlot != null && catalog.EquipmentAccessorySlotSelected != null,
            "v29 equipment accessory slot art is incomplete.");
        Require(catalog.ActiveItemSlot != null && catalog.ActiveItemSlotSelected != null,
            "v29 active item slot art is incomplete.");
        Require(catalog.PlayerVitalsFrames.Count == 12,
            $"v1 player vitals art must expose 12 frames (actual {catalog.PlayerVitalsFrames.Count}).");
        for (var index = 0; index < catalog.PlayerVitalsFrames.Count; index++)
            Require(catalog.PlayerVitalsFrames[index] != null,
                $"v1 player vitals frame {index} is not bound.");
    }

    private static void TestIceStorageSealCoreLifecycle()
    {
        var host = new GameObject("DevB_SealCoreLifecycleTest");
        var rendererHost = new GameObject("DevB_SealCoreLifecycleRenderer");
        var config = WorldGenerationConfig.CreateDefault();
        WorldSessionController session = null;

        try
        {
            var bootstrap = host.AddComponent<MainGameBootstrap>();
            var environment = host.AddComponent<MainGameEnvironmentState>();
            var renderer = rendererHost.AddComponent<TilemapRenderer>();
            session = new WorldSessionController(config, renderer, null);
            var sealSystem = new SealSystem(new TileService(new TileData[5, 5], null, null, 1));

            SetField(session, "sealSystem", sealSystem);
            SetField(bootstrap, "session", session);
            SetField(environment, "bootstrap", bootstrap);

            var entries = (IDictionary)GetField(environment, "byObjectId");
            var entryType = typeof(MainGameEnvironmentState).GetNestedType("Entry", BindingFlags.NonPublic)
                            ?? throw new InvalidOperationException("MainGameEnvironmentState.Entry type not found.");
            var entry = Activator.CreateInstance(entryType)
                        ?? throw new InvalidOperationException("MainGameEnvironmentState.Entry could not be created.");
            var coreCell = new Vector3Int(2, 2, 0);
            SetField(entry, "Record", new PlacedObjectRecord
            {
                objectId = "core_1",
                definitionId = CoolingSourceRuntime.IceStorageId,
                position = new Vector2(coreCell.x, coreCell.y)
            });
            SetField(entry, "Cell", coreCell);
            entries.Add("core_1", entry);

            Invoke(environment, "RecomputeCoolingAndInvalidate");
            Require(sealSystem.HasSealCoreCell && sealSystem.SealCoreCell == coreCell,
                "Ice storage placement did not set the seal core cell.");

            entries.Clear();
            Invoke(environment, "RecomputeCoolingAndInvalidate");
            Require(!sealSystem.HasSealCoreCell && !sealSystem.SealCoreCell.HasValue,
                "Removing the last ice storage did not clear the seal core cell.");
        }
        finally
        {
            session?.Dispose();
            UnityEngine.Object.DestroyImmediate(config);
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(rendererHost);
        }
    }

    private static object GetField(object target, string name) =>
        target.GetType().GetField(name, InstanceMembers)?.GetValue(target)
        ?? throw new MissingFieldException(target.GetType().FullName, name);

    private static void SetField(object target, string name, object value)
    {
        var field = target.GetType().GetField(name, InstanceMembers)
                    ?? throw new MissingFieldException(target.GetType().FullName, name);
        field.SetValue(target, value);
    }

    private static void Invoke(object target, string name)
    {
        var method = target.GetType().GetMethod(name, InstanceMembers)
                     ?? throw new MissingMethodException(target.GetType().FullName, name);
        method.Invoke(target, null);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
