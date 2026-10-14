using System;
using System.Linq;
using System.IO;
using System.Reflection;
using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using Nyangbingo.Save;
using Nyangbingo.UI;
using Nyangbingo.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class NyangbingoEquipmentDragDropRegressionTests
{
    private static int assertions;
    private const string ManualArmed = "Nyangbingo.EquipmentDrag.QA.Armed";
    private const string PreviousSaveDirectory = "Nyangbingo.EquipmentDrag.QA.PreviousSaveDirectory";
    private const string SaveDirectory = "Nyangbingo.QA.SaveDirectory";

    [InitializeOnLoadMethod]
    private static void RegisterManualCleanup()
    {
        EditorApplication.playModeStateChanged -= RestoreManualSaveDirectory;
        EditorApplication.playModeStateChanged += RestoreManualSaveDirectory;
    }

    private static void RestoreManualSaveDirectory(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(ManualArmed, false)) return;
        SessionState.SetString(SaveDirectory, SessionState.GetString(PreviousSaveDirectory, ""));
        SessionState.SetBool(ManualArmed, false);
        Debug.Log("[EquipmentDragDrop] Isolated playtest ended; original save directory restored.");
    }

    [MenuItem("Nyangbingo/QA/Equipment Drag/Prepare Isolated Manual Playtest")]
    public static void PrepareManualPlaytest()
    {
        if (EditorApplication.isPlaying || SessionState.GetBool(ManualArmed, false))
            throw new InvalidOperationException("Stop the previous playtest first.");
        var directory = Path.Combine(Path.GetTempPath(), "NyangbingoEquipmentDrag-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.Copy(Path.Combine(Application.streamingAssetsPath, "DemoSaves/day-1.json"),
            Path.Combine(directory, "nyangbingo-save-0.json"));
        SessionState.SetString(PreviousSaveDirectory, SessionState.GetString(SaveDirectory, ""));
        SessionState.SetString(SaveDirectory, directory);
        SessionState.SetBool(ManualArmed, true);
        Debug.Log("[EquipmentDragDrop] Isolated save directory armed. Play Title and choose Continue. " + directory);
    }

    [MenuItem("Nyangbingo/QA/Equipment Drag/Stage Manual Equipment")]
    public static void StageManualEquipment()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool(ManualArmed, false))
            throw new InvalidOperationException("Use an isolated equipment playtest, never a player save.");
        var runtime = Object.FindAnyObjectByType<MainGameRuntimeServices>();
        var controller = Object.FindAnyObjectByType<MainGameCraftingUiController>();
        Require(runtime != null && runtime.IsInitialized && controller != null, "gameplay not ready");
        Object.FindAnyObjectByType<MainGameSaveCoordinator>().SuspendAutomaticSaving();
        Time.timeScale = 0f;
        var catalog = AssetDatabase.LoadAssetAtPath<GameDataCatalog>("Assets/Data/SO/GameDataCatalog.asset");
        var fixture = new Inventory(catalog.FindItem);
        foreach (var slot in new[] { EquipmentSlot.Head, EquipmentSlot.Body, EquipmentSlot.Feet })
            foreach (var item in catalog.Equipment.Where(e => e != null && !e.IsAccessory && e.Slot == slot).Take(slot == EquipmentSlot.Head ? 2 : 1))
                Require(fixture.TryAdd(item.Id, 1), "manual armor fixture");
        foreach (var item in catalog.Equipment.Where(e => e != null && e.IsAccessory).Take(2))
            Require(fixture.TryAdd(item.Id, 1), "manual accessory fixture");
        Require(fixture.TryAdd("dokkaebi_club", 1) && fixture.TryAdd("lantern", 1) && fixture.TryAdd("wood", 10), "manual tool fixture");
        runtime.EquipmentSystem.Clear();
        runtime.ActiveSlot.TryUnequip();
        Require(runtime.PlayerInventory.TryImport(fixture.Export()), "manual inventory import");
        var page = typeof(MainGameCraftingUiController).GetNestedType("Page", BindingFlags.NonPublic);
        Invoke(controller, "OpenPage", Enum.Parse(page, "Gathering"));
        // Trace real input separately from model tests, only in this isolated QA session.
        foreach (var source in Object.FindObjectsByType<InventoryEquipmentDragSource>(FindObjectsSortMode.None))
            Trace(source.gameObject, EventTriggerType.BeginDrag, "BEGIN");
        foreach (var button in (Button[])typeof(MainGameCraftingUiController)
                     .GetField("equipmentSlotButtons", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller))
            Trace(button.gameObject, EventTriggerType.Drop, "DROP");
        InspectManualEquipment();
    }

    private static void Trace(GameObject target, EventTriggerType eventType, string label)
    {
        var trigger = target.GetComponent<EventTrigger>();
        if (trigger == null) return;
        var entry = new EventTrigger.Entry { eventID = eventType };
        entry.callback.AddListener(data =>
        {
            var pointer = (PointerEventData)data;
            Debug.Log($"[EquipmentDragDrop] {label} target={target.name}, source={pointer.pointerDrag?.name}, pointer={pointer.pointerId}");
        });
        trigger.triggers.Add(entry);
    }

    [MenuItem("Nyangbingo/QA/Equipment Drag/Replay Live Pointer Drags")]
    public static void ReplayLivePointerDrags()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool(ManualArmed, false))
            throw new InvalidOperationException("Stage an isolated manual equipment playtest first.");
        if (Object.FindAnyObjectByType<EquipmentDragLivePointerReplay>() != null) return;
        new GameObject("EquipmentDragLivePointerReplay").AddComponent<EquipmentDragLivePointerReplay>().Begin();
    }

    [MenuItem("Nyangbingo/QA/Equipment Drag/Inspect Manual Equipment")]
    public static void InspectManualEquipment()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool(ManualArmed, false)) return;
        var runtime = Object.FindAnyObjectByType<MainGameRuntimeServices>();
        Debug.Log("[EquipmentDragDrop] UI STATE bag=" + Snapshot(runtime.PlayerInventory) +
                  "; equipment=" + string.Join(",", runtime.EquipmentSystem.Export().Select(p => p.Key + ":" + p.Value.Id)) +
                  "; active=" + runtime.ActiveSlot.EquippedItemId + "; cursor=" + Snapshot(runtime.InventoryCursor));
    }

    [MenuItem("Nyangbingo/Run Equipment Drag Drop Regression")]
    public static void RunAll()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run this isolated regression in Edit mode.");
        assertions = 0;
        var catalog = AssetDatabase.LoadAssetAtPath<GameDataCatalog>("Assets/Data/SO/GameDataCatalog.asset");
        Require(catalog != null, "catalog missing");
        TestCatalogSlots(catalog);
        TestReplacementAndCancellation(catalog);
        TestControllerEvents(catalog);
        // Existing click/cursor and ownership behaviour must continue to work.
        NyangbingoV72ExpansionRegressionTests.RunInventoryClickDropRegression();
        NyangbingoV72ExpansionRegressionTests.RunInventoryEquipmentOwnershipRegression();
        Debug.Log($"[EquipmentDragDrop] PASS: {assertions} assertions; catalog, replacement/cancellation, " +
                  "controller pointer events, and existing inventory/ownership regressions.");
    }

    private static void TestCatalogSlots(GameDataCatalog catalog)
    {
        var checkedEquipment = 0;
        foreach (var definition in catalog.Equipment.Where(e => e != null && catalog.FindItem(e.Id) != null))
        {
            var bag = new Inventory(catalog.FindItem, 1);
            Require(bag.TryAdd(definition.Id, 1), "fixture " + definition.Id);
            for (var target = 0; target < 6; target++)
            {
                var equipment = new EquipmentSystem();
                var active = new ActiveSlotSystem(bag, catalog.FindItem, true);
                var drag = new EquipmentInventoryDrag(bag, catalog.FindEquipment);
                var expected = target == 0 ? ActiveSlotSystem.IsAllowedItemId(definition.Id) :
                    definition.IsAccessory ? target == 4 || target == 5 :
                    definition.Slot == EquipmentSlot.Head ? target == 1 :
                    definition.Slot == EquipmentSlot.Body ? target == 2 :
                    definition.Slot == EquipmentSlot.Feet && target == 3;
                Require(drag.Begin(0) && drag.CanDrop(target) == expected, $"compatibility {definition.Id}/{target}");
                Require(drag.TryDrop(target, equipment, active) == expected, $"drop {definition.Id}/{target}");
                Require(!drag.IsActive && bag.Count(definition.Id) == 1, "drag lost/duplicated inventory");
                if (!expected) Require(equipment.Export().Count == 0 && !active.HasEquippedItem, "invalid changed equipment");
            }
            checkedEquipment++;
        }
        var weapons = catalog.Items.Where(i => i != null && ActiveSlotSystem.IsAllowedItemId(i.Id)).ToArray();
        foreach (var item in weapons)
        {
            var bag = new Inventory(catalog.FindItem, 1);
            Require(bag.TryAdd(item.Id, 1), "weapon fixture " + item.Id);
            var drag = new EquipmentInventoryDrag(bag, catalog.FindEquipment);
            var active = new ActiveSlotSystem(bag, catalog.FindItem, true);
            Require(drag.Begin(0) && drag.TryDrop(0, new EquipmentSystem(), active) &&
                    active.EquippedItemId == item.Id && bag.Count(item.Id) == 1, "weapon equip " + item.Id);
        }
        var claws = catalog.Items.Where(item => item != null && ClawSlotSystem.IsClawItem(item.Id)).ToArray();
        foreach (var item in claws)
        {
            var bag = new Inventory(catalog.FindItem, 2);
            Require(bag.TryAdd(item.Id, 1), "claw fixture " + item.Id);
            var drag = new EquipmentInventoryDrag(bag, catalog.FindEquipment);
            var claw = new ClawSlotSystem(bag, catalog.FindItem);
            var active = new ActiveSlotSystem(bag, catalog.FindItem, true);
            Require(drag.Begin(0) && drag.CanDrop(ClawSlotSystem.VisualSlotIndex) && !drag.CanDrop(0) &&
                    drag.TryDrop(ClawSlotSystem.VisualSlotIndex, new EquipmentSystem(), active, claw) &&
                    claw.EquippedItemId == item.Id && bag.Count(item.Id) == 0,
                "claw drag equip " + item.Id);
        }
        Require(checkedEquipment > 0 && weapons.Length > 0 && claws.Length > 0, "empty catalog test");
        Debug.Log($"[EquipmentDragDrop] Catalog: {checkedEquipment} equipment x 6 slots, {weapons.Length} weapons/tools.");
    }

    private static void TestReplacementAndCancellation(GameDataCatalog catalog)
    {
        var heads = catalog.Equipment.Where(e => e != null && !e.IsAccessory && e.Slot == EquipmentSlot.Head &&
                                                    catalog.FindItem(e.Id) != null).Take(2).ToArray();
        var charms = catalog.Equipment.Where(e => e != null && e.IsAccessory && catalog.FindItem(e.Id) != null).Take(2).ToArray();
        Require(heads.Length == 2 && charms.Length == 2, "replacement fixtures missing");
        var bag = new Inventory(catalog.FindItem, 6);
        foreach (var item in heads.Concat(charms)) Require(bag.TryAdd(item.Id, 1), "add " + item.Id);
        Require(bag.TryAdd("dokkaebi_club", 1) && bag.TryAdd("lantern", 1), "add tools");
        var equipment = new EquipmentSystem();
        var active = new ActiveSlotSystem(bag, catalog.FindItem, true);
        var drag = new EquipmentInventoryDrag(bag, catalog.FindEquipment);
        var before = Snapshot(bag);
        Require(drag.Begin(0) && drag.TryDrop(1, equipment, active), "equip first helmet");
        Require(drag.Begin(1) && drag.TryDrop(1, equipment, active) && equipment.Get(EquipmentSlot.Head) == heads[1],
            "full-bag helmet replacement");
        Require(Snapshot(bag) == before, "replacement changed bag");
        Require(drag.Begin(2) && drag.TryDrop(4, equipment, active), "accessory one");
        Require(drag.Begin(3) && drag.TryDrop(5, equipment, active), "accessory two");
        var events = 0;
        equipment.Changed += () => events++;
        Require(drag.Begin(2) && drag.TryDrop(5, equipment, active) &&
                equipment.Get(EquipmentSlot.AccessoryOne) == null && equipment.Get(EquipmentSlot.AccessoryTwo) == charms[0] &&
                events == 1, "accessory move must be atomic and nonduplicating");
        Require(drag.Begin(2) && drag.TryDrop(5, equipment, active) && events == 1, "same slot must not unequip");
        Require(drag.Begin(4) && drag.TryDrop(0, equipment, active) && drag.Begin(5) && drag.TryDrop(0, equipment, active),
            "full-bag weapon replacement");
        Require(active.EquippedItemId == "lantern" && Snapshot(bag) == before, "weapon replacement changed bag");
        active.SelectBareHands();
        Require(drag.Begin(5) && drag.TryDrop(0, equipment, active) && active.IsUsingEquippedItem, "redrop must activate tool");
        Require(drag.Begin(0), "cancel begin");
        drag.Cancel();
        Require(!drag.TryDrop(1, equipment, active) && Snapshot(bag) == before, "cancel was not safe");
        Require(drag.Begin(0) && !drag.TryDrop(2, equipment, active) && equipment.Get(EquipmentSlot.Head) == heads[1],
            "invalid drop changed equipped helmet");
        Require(drag.Begin(0) && !drag.CanDrop(-1) && !drag.TryDrop(6, equipment, active), "invalid slot bounds");
        Require(!drag.Begin(-1) && !drag.Begin(bag.Capacity), "source bounds");
        Require(drag.Begin(0) && bag.TryRemove(heads[0].Id, 1) && bag.TryAdd("wood", 1), "stale source fixture");
        Require(!drag.TryDrop(1, equipment, active) && equipment.Get(EquipmentSlot.Head) == heads[1], "stale source equipped");
        Require(!drag.Begin(0), "material accepted as equipment");
    }

    private static void TestControllerEvents(GameDataCatalog catalog)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        MainGameCraftingUiController controller = null;
        var root = new GameObject("EquipmentDragRegression");
        SceneManager.MoveGameObjectToScene(root, scene);
        try
        {
            var camera = Child(root, "Camera").AddComponent<Camera>();
            camera.enabled = false;
            Child(root, "Light").AddComponent<Light>().type = LightType.Directional;
            var eventSystem = Child(root, "EventSystem").AddComponent<EventSystem>();
            var runtime = Child(root, "Runtime").AddComponent<MainGameRuntimeServices>();
            var bag = new Inventory(catalog.FindItem);
            var cursor = new Inventory(catalog.FindItem, 1);
            var equipment = new EquipmentSystem();
            var active = new ActiveSlotSystem(bag, catalog.FindItem, true);
            var helmet = catalog.Equipment.First(e => e != null && !e.IsAccessory && e.Slot == EquipmentSlot.Head);
            Require(bag.TryAdd(helmet.Id, 1), "controller item");
            SetProperty(runtime, "PlayerInventory", bag);
            SetProperty(runtime, "InventoryCursor", cursor);
            SetProperty(runtime, "EquipmentSystem", equipment);
            SetProperty(runtime, "ActiveSlot", active);
            SetProperty(runtime, "IsInitialized", true);
            controller = Child(root, "Controller").AddComponent<MainGameCraftingUiController>();
            Set(controller, "runtimeServices", runtime);
            Set(controller, "gameDataCatalog", catalog);
            Set(controller, "open", true);
            var grid = Child(root, "InventoryGrid");
            var buttons = Enumerable.Range(0, Inventory.SlotCount).Select(i => Button(grid, "Slot" + i)).ToArray();
            Set(controller, "inventoryGridRoot", grid);
            Set(controller, "inventoryGridButtons", buttons);
            Invoke(controller, "BuildInventoryGrid");
            grid.SetActive(true);
            var visual = Child(root, "EquipmentVisual");
            var targets = Enumerable.Range(0, 6).Select(i => Button(visual, "Equipment" + i)).ToArray();
            Set(controller, "equipmentVisualRoot", visual);
            Set(controller, "equipmentCharacter", Child(visual, "Character").AddComponent<Image>());
            Set(controller, "equipmentSlotButtons", targets);
            Set(controller, "equipmentSlotBackgrounds", targets.Select(t => t.GetComponent<Image>()).ToArray());
            Invoke(controller, "BuildEquipmentVisual");
            visual.SetActive(true);
            var pointer = new PointerEventData(eventSystem) { button = PointerEventData.InputButton.Left,
                pointerId = -1, pointerDrag = buttons[0].gameObject, eligibleForClick = true };
            void Begin() => ExecuteEvents.Execute(buttons[0].gameObject, pointer, ExecuteEvents.beginDragHandler);
            void Drop(int index) => ExecuteEvents.Execute(targets[index].gameObject, pointer, ExecuteEvents.dropHandler);
            void End() => ExecuteEvents.Execute(buttons[0].gameObject, pointer, ExecuteEvents.endDragHandler);
            Begin();
            Require(!pointer.eligibleForClick && cursor.IsEmpty && bag.Count(helmet.Id) == 1, "drag fired pickup");
            Require(targets[1].interactable && targets[1].GetComponent<Image>().color.g == 1f &&
                    targets[2].GetComponent<Image>().color.g < 1f, "valid target feedback missing");
            Drop(2); End();
            Require(equipment.Get(EquipmentSlot.Head) == null && bag.Count(helmet.Id) == 1 && cursor.IsEmpty,
                "wrong target must leave inventory intact");
            Begin(); Drop(1); End();
            Require(equipment.Get(EquipmentSlot.Head) == helmet && bag.Count(helmet.Id) == 1 && cursor.IsEmpty,
                "controller valid drop failed");
            Require(targets[1].GetComponent<Image>().color.g != 1f ||
                    targets[1].GetComponent<Image>().color.r == 1f, "highlight not reset");
            equipment.Clear();
            Begin(); End(); Drop(1);
            Require(equipment.Get(EquipmentSlot.Head) == null, "outside drop did not cancel");
            Begin(); buttons[0].gameObject.SetActive(false); Drop(1);
            Require(equipment.Get(EquipmentSlot.Head) == null, "disabled source did not cancel");
            buttons[0].gameObject.SetActive(true);
            Begin(); Invoke(controller, "OnApplicationFocus", false); Drop(1);
            Require(equipment.Get(EquipmentSlot.Head) == null, "focus loss did not cancel");
            Begin(); pointer.pointerId = 99; Drop(1);
            Require(equipment.Get(EquipmentSlot.Head) == null, "foreign pointer equipped");
            pointer.pointerId = -1; End();
            pointer.button = PointerEventData.InputButton.Right; Begin(); Drop(1); End();
            Require(equipment.Get(EquipmentSlot.Head) == null, "right drag equipped");
            pointer.button = PointerEventData.InputButton.Left;
            ExecuteEvents.Execute(buttons[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Require(cursor.Count(helmet.Id) == 1, "existing click pickup broken");
            Begin(); Drop(1); End();
            Require(equipment.Get(EquipmentSlot.Head) == null && cursor.Count(helmet.Id) == 1, "held cursor conflict");
            Invoke(controller, "ReturnHeldInventoryItem");
            Require(cursor.IsEmpty && bag.Count(helmet.Id) == 1, "click cursor return broken");
            Begin(); Invoke(controller, "SetOpen", false); Drop(1);
            Require(equipment.Get(EquipmentSlot.Head) == null && bag.Count(helmet.Id) == 1, "close did not cancel");
            Begin(); Drop(1); End();
            Require(equipment.Get(EquipmentSlot.Head) == helmet && cursor.IsEmpty && bag.Count(helmet.Id) == 1,
                "always-visible inventory must also allow equipment drags with the tab panel closed");
        }
        finally
        {
            if (controller != null) Set(controller, "open", false);
            Object.DestroyImmediate(root);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static GameObject Child(GameObject parent, string name)
    {
        var child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(parent.transform, false);
        return child;
    }
    private static Button Button(GameObject parent, string name)
    {
        var child = Child(parent, name);
        child.AddComponent<Image>();
        return child.AddComponent<Button>();
    }
    private static string Snapshot(Inventory bag) => string.Join("|", bag.Slots.Select(s => $"{s.itemId}:{s.amount}"));
    private static void Set(object target, string name, object value) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void SetProperty(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
    private static object Invoke(object target, string name, params object[] args) => target.GetType()
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    private static void Require(bool condition, string reason)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException("[EquipmentDragDrop] " + reason);
    }
}
