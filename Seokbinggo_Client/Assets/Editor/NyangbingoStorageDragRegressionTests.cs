using System;
using System.Collections.Generic;
using System.Linq;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using Nyangbingo.UI;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class NyangbingoStorageDragRegressionTests
{
    private static int assertions;

    [InitializeOnLoadMethod]
    private static void RegisterBackgroundCleanup()
    {
        EditorApplication.playModeStateChanged -= RestoreBackground;
        EditorApplication.playModeStateChanged += RestoreBackground;
    }

    private static void RestoreBackground(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool("StorageDrag.BackgroundArmed", false)) return;
        Application.runInBackground = SessionState.GetBool("StorageDrag.PreviousBackground", false);
        SessionState.SetBool("StorageDrag.BackgroundArmed", false);
    }

    [MenuItem("Nyangbingo/Run Storage Drag Regression")]
    public static void RunAll()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run in Edit mode.");
        assertions = 0;
        var catalog = AssetDatabase.LoadAssetAtPath<GameDataCatalog>("Assets/Data/SO/GameDataCatalog.asset");
        Inventory Bag(int capacity = 4) => new Inventory(catalog.FindItem, capacity);
        var source = Bag();
        var target = Bag();
        var drag = new InventorySlotDrag();
        Require(source.TryAdd("wood", 12) && target.TryAdd("coal", 3), "fixture");
        Require(drag.Begin(source, 0) && source.Count("wood") == 12, "begin must not remove items");
        Require(drag.TryDrop(target, 2) && source.IsEmpty && target.Slots[2].amount == 12, "exact target cell");
        Require(drag.Begin(target, 2) && drag.TryDrop(source, 3) && source.Slots[3].amount == 12, "reverse transfer");
        Require(drag.Begin(source, 3) && drag.TryDrop(target, 0), "occupied cell swap");
        Require(source.Slots[3].itemId == "coal" && target.Slots[0].itemId == "wood", "swap result");
        Require(drag.Begin(source, 3) && drag.TryDrop(source, 0) && source.Slots[0].itemId == "coal", "same bag reorder");
        var original = Snapshot(source);
        Require(drag.Begin(source, 0), "cancel begin"); drag.Cancel();
        Require(!drag.TryDrop(target, 1) && Snapshot(source) == original, "cancel is lossless");
        Require(!drag.Begin(source, -1) && !drag.Begin(source, 4) && !drag.Begin(null, 0), "bounds");
        Require(drag.Begin(source, 0) && !drag.TryDrop(source, 0) && Snapshot(source) == original, "same slot no-op");
        Require(drag.Begin(source, 0) && !drag.TryDrop(target, 99) && Snapshot(source) == original, "invalid target");
        Require(drag.Begin(source, 0) && source.TryRemove("coal", 1), "stale fixture");
        Require(!drag.TryDrop(target, 1) && source.Count("coal") == 2, "stale quantity rejected");

        var max = catalog.FindItem("wood").MaxStack;
        source = Bag(1); target = Bag(1);
        Require(source.TryAdd("wood", 6) && target.TryAdd("wood", max - 3), "merge fixture");
        var observedAtomic = 0;
        source.Changed += () => { Require(source.Count("wood") + target.Count("wood") == max + 3, "atomic source event"); observedAtomic++; };
        target.Changed += () => { Require(source.Count("wood") + target.Count("wood") == max + 3, "atomic target event"); observedAtomic++; };
        Require(drag.Begin(source, 0) && drag.TryDrop(target, 0), "partial merge");
        Require(source.Count("wood") == 3 && target.Count("wood") == max && observedAtomic == 2, "overflow returns to origin");
        Require(drag.Begin(source, 0) && !drag.TryDrop(target, 0), "full stack rejects");

        source = Bag(1); target = Bag(1);
        Require(source.TryAddWithStorageState("wood", 4, true, .5f, .5f) &&
                target.TryAddWithStorageState("wood", 3, true, .5f, .5f), "condition fixture");
        Require(drag.Begin(source, 0) && drag.TryDrop(target, 0), "melt merge");
        Require(source.IsEmpty && target.Slots[0].amount == 6 &&
                Mathf.Approximately(target.Slots[0].storageMeltRemainder, 0f) &&
                Mathf.Approximately(target.Slots[0].EffectiveStorageCondition, .5f), "freshness and melt conservation");
        source = Bag(1);
        Require(source.TryAddWithStorageState("wood", 2, true, .8f, 0f), "different condition fixture");
        Require(drag.Begin(source, 0) && !drag.TryDrop(target, 0) && source.Count("wood") == 2, "incompatible freshness rejects");

        source = Bag(20); target = Bag(20);
        Require(source.TryAdd("wood", 71) && source.TryAdd("coal", 39) && target.TryAdd("wood", 19), "random fixture");
        var random = new System.Random(141026);
        for (var i = 0; i < 1000; i++)
        {
            var from = random.Next(2) == 0 ? source : target;
            var to = random.Next(2) == 0 ? source : target;
            if (drag.Begin(from, random.Next(20))) drag.TryDrop(to, random.Next(20));
            Require(source.Count("wood") + target.Count("wood") == 90 &&
                    source.Count("coal") + target.Count("coal") == 39, "random conservation " + i);
        }
        foreach (var screen in new[] { new Vector2(428, 240), new Vector2(320, 180), new Vector2(640, 360), new Vector2(256, 240) })
        foreach (var size in new[] { new Vector2(119, 88), new Vector2(119, 120) })
        foreach (var inventory in new[] { new Rect(4, 4, 129, 59), new Rect(4, 4, 129, 80) })
        {
            var popup = MainGameCraftingUiController.ResolveStoragePopupRect(screen, inventory, size);
            Require(popup.xMin >= 4 && popup.yMin >= 4 && popup.xMax <= screen.x - 4 && popup.yMax <= screen.y - 4, "popup bounds " + screen);
            Require(!popup.Overlaps(inventory), "popup must not cover inventory " + screen);
            Require(popup.size == size, "slot grid must not stretch " + screen);
        }
        NyangbingoEquipmentDragDropRegressionTests.RunAll();
        Debug.Log($"[StorageDragDrop] PASS: {assertions} assertions; atomic transfer/swap/merge, cancellation, stale data, metadata, random conservation and layout; equipment regression also passed.");
    }

    [MenuItem("Nyangbingo/QA/Storage Drag/Continue Isolated Playtest")]
    public static void ContinueIsolated()
    {
        Require(EditorApplication.isPlaying && SessionState.GetBool("Nyangbingo.EquipmentDrag.QA.Armed", false), "arm an isolated playtest first");
        if (!SessionState.GetBool("StorageDrag.BackgroundArmed", false))
        {
            SessionState.SetBool("StorageDrag.PreviousBackground", Application.runInBackground);
            SessionState.SetBool("StorageDrag.BackgroundArmed", true);
        }
        Application.runInBackground = true;
        Require(Object.FindAnyObjectByType<TitleShellController>()?.TryContinue() == true, "title continue failed");
    }

    [MenuItem("Nyangbingo/QA/Storage Drag/Replay Live Input")]
    public static void Replay()
    {
        Require(EditorApplication.isPlaying && SessionState.GetBool("Nyangbingo.EquipmentDrag.QA.Armed", false), "isolated save required");
        if (Object.FindAnyObjectByType<StorageDragLivePointerReplay>() != null) return;
        NyangbingoEquipmentDragDropRegressionTests.StageManualEquipment();
        new GameObject("StorageDragLivePointerReplay").AddComponent<StorageDragLivePointerReplay>().Begin();
    }

    private static string Snapshot(Inventory bag) => string.Join("|", bag.Slots.Select(s => JsonUtility.ToJson(s)));
    private static void Require(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException("[StorageDragDrop] " + message);
    }
}
