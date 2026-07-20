using System;
using System.Collections;
using System.Reflection;
using Nyangbingo.Save;
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
        Debug.Log("[Nyangbingo] Dev B integration regression tests passed (1/1).");
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
