#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Nyangbingo.Core;
using Nyangbingo.UI;
using Nyangbingo.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Editor-only, opt-in input replay through the real raycaster and StandaloneInputModule.</summary>
public sealed class EquipmentDragLivePointerReplay : BaseInput
{
    private Vector2 position;
    private bool down, held, up;
    private StandaloneInputModule module;
    private BaseInput previousInput;
    private bool previousBackground;
    public override Vector2 mousePosition => position;
    public override bool mousePresent => true;
    public override Vector2 mouseScrollDelta => Vector2.zero;
    public override bool touchSupported => false;
    public override bool GetMouseButtonDown(int button) => button == 0 && down;
    public override bool GetMouseButton(int button) => button == 0 && held;
    public override bool GetMouseButtonUp(int button) => button == 0 && up;
    public override float GetAxisRaw(string axisName) => 0f;
    public override bool GetButtonDown(string buttonName) => false;

    public void Begin()
    {
        module = EventSystem.current.GetComponent<StandaloneInputModule>();
        previousInput = module.inputOverride;
        previousBackground = Application.runInBackground;
        Application.runInBackground = true;
        module.inputOverride = this;
        StartCoroutine(Replay());
    }

    private IEnumerator Replay()
    {
        try
        {
            var controller = FindAnyObjectByType<MainGameCraftingUiController>();
            var runtime = FindAnyObjectByType<MainGameRuntimeServices>();
            var sources = Buttons(controller, "inventoryGridButtons");
            var targets = Buttons(controller, "equipmentSlotButtons");
            var bagBefore = string.Join("|", runtime.PlayerInventory.Slots.Select(s => s.itemId + ":" + s.amount));
            var first = runtime.PlayerInventory.Slots[0].itemId;
            var second = runtime.PlayerInventory.Slots[1].itemId;
            yield return Drag(sources[0], targets[2]);
            if (runtime.EquipmentSystem.Get(EquipmentSlot.Head) != null) { Fail("invalid target"); yield break; }
            yield return Drag(sources[0], targets[1]);
            if (runtime.EquipmentSystem.Get(EquipmentSlot.Head)?.Id != first) { Fail("helmet equip"); yield break; }
            yield return Drag(sources[1], targets[1]);
            if (runtime.EquipmentSystem.Get(EquipmentSlot.Head)?.Id != second) { Fail("helmet replacement"); yield break; }
            yield return Drag(sources[4], targets[5]);
            if (runtime.EquipmentSystem.Get(EquipmentSlot.AccessoryTwo)?.Id != runtime.PlayerInventory.Slots[4].itemId)
                { Fail("explicit accessory two"); yield break; }
            yield return Drag(sources[5], targets[4]);
            yield return Drag(sources[4], targets[4]);
            if (runtime.EquipmentSystem.Get(EquipmentSlot.AccessoryOne)?.Id != runtime.PlayerInventory.Slots[4].itemId ||
                runtime.EquipmentSystem.Get(EquipmentSlot.AccessoryTwo) != null) { Fail("accessory move/replace"); yield break; }
            yield return Drag(sources[6], targets[0]);
            if (runtime.ActiveSlot.EquippedItemId != "dokkaebi_club") { Fail("weapon"); yield break; }
            yield return Drag(sources[7], targets[0]);
            if (runtime.ActiveSlot.EquippedItemId != "lantern") { Fail("tool replacement"); yield break; }
            yield return Drag(sources[2], targets[2]);
            yield return Drag(sources[3], targets[3]);
            if (runtime.EquipmentSystem.Get(EquipmentSlot.Body)?.Id != runtime.PlayerInventory.Slots[2].itemId ||
                runtime.EquipmentSystem.Get(EquipmentSlot.Feet)?.Id != runtime.PlayerInventory.Slots[3].itemId)
                { Fail("body/feet"); yield break; }
            var bagAfter = string.Join("|", runtime.PlayerInventory.Slots.Select(s => s.itemId + ":" + s.amount));
            if (bagBefore != bagAfter || !runtime.InventoryCursor.IsEmpty) { Fail("inventory conservation"); yield break; }
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/equipment-drag-live.png"));
            ScreenCapture.CaptureScreenshot(output);
            Debug.Log("[EquipmentDragDrop] LIVE POINTER PASS: 10 drags via actual scene raycasts; wrong slot, helmet " +
                      "replacement, accessory targeting/move, weapon/tool replacement, body/feet, unchanged bag and empty cursor. " + output);
        }
        finally
        {
            RestoreInput();
            Destroy(gameObject);
        }
    }

    private IEnumerator Drag(Button source, Button target)
    {
        position = Centre(source);
        yield return null;
        yield return null;
        down = held = true;
        yield return null;
        down = false;
        var start = position;
        var end = Centre(target);
        for (var step = 1; step <= 12; step++)
        {
            position = Vector2.Lerp(start, end, step / 12f);
            yield return null;
        }
        yield return null;
        held = false;
        up = true;
        yield return null;
        up = false;
        yield return null;
        yield return null;
    }

    private static Vector2 Centre(Button button)
    {
        var rect = (RectTransform)button.transform;
        var canvas = button.GetComponentInParent<Canvas>().rootCanvas;
        return RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            rect.TransformPoint(rect.rect.center));
    }
    private static Button[] Buttons(object controller, string field) => (Button[])controller.GetType()
        .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(controller);
    private static void Fail(string reason) => Debug.LogError("[EquipmentDragDrop] LIVE POINTER FAIL: " + reason);
    private void RestoreInput()
    {
        if (module != null && module.inputOverride == this) module.inputOverride = previousInput;
        Application.runInBackground = previousBackground;
    }
    protected override void OnDisable()
    {
        RestoreInput();
        base.OnDisable();
    }
}
#endif
