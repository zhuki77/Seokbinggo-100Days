#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using Nyangbingo.UI;
using Nyangbingo.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Bag = Nyangbingo.Inventory.Inventory;

/// <summary>Opt-in isolated QA. World right-clicks and UI drags go through the real input handlers.</summary>
[DefaultExecutionOrder(-1000)]
public sealed class StorageDragLivePointerReplay : BaseInput
{
    private Vector2 pointer;
    private bool down, held, up, secondary, escape;
    private bool running;
    private StandaloneInputModule module;
    private BaseInput previousInput;
    private bool previousBackground, previousSimulated;
    private float previousTime;
    private Rigidbody2D body;
    public override Vector2 mousePosition => pointer;
    public override bool mousePresent => true;
    public override Vector2 mouseScrollDelta => Vector2.zero;
    public override bool touchSupported => false;
    public override bool GetMouseButtonDown(int button) => button == 0 && down;
    public override bool GetMouseButton(int button) => button == 0 && held;
    public override bool GetMouseButtonUp(int button) => button == 0 && up;
    public override float GetAxisRaw(string name) => 0f;
    public override bool GetButtonDown(string name) => false;

    public void Begin()
    {
        module = EventSystem.current.GetComponent<StandaloneInputModule>();
        previousInput = module.inputOverride;
        previousBackground = Application.runInBackground;
        previousTime = Time.timeScale;
        module.inputOverride = this;
        Application.runInBackground = true;
        GameplayInput.BeginReplay();
        running = true;
        StartCoroutine(Replay());
    }

    private void Update()
    {
        if (running) GameplayInput.Sample(0f, pointer, held, secondary,
            escape ? new[] { KeyCode.Escape } : Array.Empty<KeyCode>());
    }

    private IEnumerator Replay()
    {
        try
        {
            var ui = FindAnyObjectByType<MainGameCraftingUiController>();
            var runtime = FindAnyObjectByType<MainGameRuntimeServices>();
            var shell = FindAnyObjectByType<GameShellController>();
            if (shell.Screen == GameShellScreen.TraitSelect)
            {
                var trait = FindObjectsByType<Button>(FindObjectsSortMode.None).First(b =>
                    b.gameObject.activeInHierarchy && b.name.StartsWith("Trait_", StringComparison.Ordinal));
                yield return Press(trait);
                held = false; up = true; yield return null; up = false; yield return null; yield return null;
            }
            Require(shell.Screen == GameShellScreen.Gameplay, "gameplay screen not ready: " + shell.Screen);
            var bootstrap = FindAnyObjectByType<MainGameBootstrap>();
            var player = FindAnyObjectByType<MainGamePlayerController>();
            var camera = Get<Camera>(player, "followCamera");
            Invoke(ui, "SetOpen", false);
            var bagButtons = Get<Button[]>(ui, "inventoryGridButtons");
            var equipmentButtons = Get<Button[]>(ui, "equipmentSlotButtons");
            var inventoryRect = (RectTransform)Get<GameObject>(ui, "inventoryScrollRoot").transform;
            var inventoryBefore = inventoryRect.anchoredPosition;
            var chest = bootstrap.Session.LastResult.chests.OrderBy(c =>
                ((Vector2)c.position - (Vector2)player.transform.position).sqrMagnitude).First();
            var cell = new Vector3Int(chest.position.x, chest.position.y);
            var chestWorld = bootstrap.TileService.GetCellCenterWorld(cell);
            body = player.GetComponent<Rigidbody2D>();
            previousSimulated = body.simulated;
            body.simulated = false;
            player.transform.position = chestWorld;
            camera.transform.position = new Vector3(chestWorld.x, chestWorld.y, camera.transform.position.z);
            Time.timeScale = 1f;
            for (var frame = 0; frame < 10; frame++) { pointer = camera.WorldToScreenPoint(chestWorld); yield return null; }
            secondary = true; yield return null; secondary = false; yield return null; yield return null;
            Require(ui.IsOpen && Get<string>(ui, "chestId") == chest.id,
                $"world right-click did not open pointed chest (screen={shell.Screen}, ui={GameplayInput.IsPointerOverUi()}, blocked={MainGameCraftingUiController.BlocksGameplayInput}, target={player.TryGetHighlightedInteractionTarget(out _, out _, out _)})");
            Require(bootstrap.Session.ChestProgress.TryGetContents(chest.id, out var storage), "chest contents missing");
            var catalog = Get<GameDataCatalog>(ui, "gameDataCatalog");
            var fixture = new Bag(catalog.FindItem, storage.Capacity);
            Require(fixture.TryAdd("wood", 12) && fixture.TryAdd("coal", 3) && fixture.TryAdd("catnip", 2), "storage fixture");
            Require(storage.TryImport(fixture.Export()), "fixture import");
            Invoke(ui, "Refresh");
            yield return null; yield return null;
            var storageButtons = Get<Text[]>(ui, "storageLabels").Select(t => t.transform.parent.GetComponent<Button>()).ToArray();
            var popup = Get<GameObject>(ui, "storageModeRoot");
            Require(popup.activeInHierarchy && !popup.transform.Find("PlayerGrid").gameObject.activeSelf,
                "popup must reuse the existing inventory");
            var slotArt = popup.transform.Find("StorageGrid").GetComponent<Image>();
            Require(!slotArt.enabled, "stretched full-grid art must not cover the individual cells");
            Require(Get<Text>(ui, "storageLabelText").text == "상자" &&
                    !Get<Text>(ui, "storageHintText").gameObject.activeSelf &&
                    popup.transform.Find("CloseStoragePopup") == null, "chest should show only its name and slots");
            Require(popup.GetComponent<Image>().color.a == 0f &&
                    (popup.GetComponent<Outline>() == null || !popup.GetComponent<Outline>().enabled),
                "chest interior must remain transparent");
            var borderFrame = popup.transform.Find("StorageFrame") as RectTransform;
            Require(borderFrame != null && borderFrame.childCount == 4 && borderFrame.GetComponent<Image>() == null,
                "chest needs only four outer border edges, without a filled panel");
            foreach (var edge in borderFrame.GetComponentsInChildren<Image>())
                Require(edge.enabled && edge.color.a == 1f && !edge.raycastTarget &&
                        Mathf.Approximately(Mathf.Min(edge.rectTransform.rect.width, edge.rectTransform.rect.height), 1f),
                    "border must be one pixel thick and must not intercept drags");
            var bagLayout = Get<GameObject>(ui, "inventoryGridRoot").GetComponent<GridLayoutGroup>();
            var storageLayout = popup.transform.Find("StorageGrid").GetComponent<GridLayoutGroup>();
            Require(storageLayout.cellSize == bagLayout.cellSize && storageLayout.spacing == bagLayout.spacing &&
                    storageLayout.constraintCount == bagLayout.constraintCount, "shared inventory grid dimensions");
            for (var index = 0; index < storageButtons.Length; index++)
            {
                var art = storageButtons[index].targetGraphic as Image;
                Require(art != null && art.enabled && art.sprite != null &&
                        art.sprite == (bagButtons[0].targetGraphic as Image).sprite && art.color == Color.white,
                    "storage cell must use the inventory slot art: " + index);
                Require(((RectTransform)storageButtons[index].transform).rect.size ==
                        ((RectTransform)bagButtons[0].transform).rect.size, "matching hit area: " + index);
                Require(Mathf.Abs(Centre(storageButtons[index]).x - Centre(bagButtons[index % bagLayout.constraintCount]).x) < .5f,
                    "storage columns must align with inventory: " + index);
            }
            Require(inventoryRect.anchoredPosition == inventoryBefore && inventoryRect.gameObject.activeInHierarchy,
                "inventory was moved or hidden");
            Require(!Get<GameObject>(ui, "craftWindowRoot").activeSelf, "craft tooltip leaked over storage");
            var corners = new Vector3[4];
            borderFrame.GetWorldCorners(corners);
            var canvas = popup.GetComponentInParent<Canvas>();
            var footer = FindAnyObjectByType<MainGameHudController>().TearBalanceRect;
            if (footer != null && footer.gameObject.activeInHierarchy)
            {
                var popupBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvas.transform, popup.transform);
                var footerBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvas.transform, footer);
                Require(!popupBounds.Intersects(footerBounds), "chest title overlaps the tear counter");
            }
            foreach (var corner in corners)
            {
                var screen = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, corner);
                Require(screen.x >= 0 && screen.y >= 0 && screen.x <= Screen.width && screen.y <= Screen.height, "popup clipped");
            }
            yield return Drag(storageButtons[0], bagButtons[9]);
            Require(storage.Slots[0].amount == 0 && runtime.PlayerInventory.Slots[9].amount == 12, "chest to bag");
            yield return Drag(bagButtons[9], storageButtons[3]);
            Require(storage.Slots[3].amount == 12 && runtime.PlayerInventory.Slots[9].amount == 0, "bag to exact chest slot");
            yield return Drag(storageButtons[1], bagButtons[8]);
            Require(storage.Slots[1].itemId == "wood" && runtime.PlayerInventory.Slots[8].itemId == "coal", "occupied swap");
            yield return Drag(storageButtons[1], bagButtons[9]);
            yield return Drag(storageButtons[3], bagButtons[9]);
            Require(runtime.PlayerInventory.Slots[9].amount == 22 && storage.Slots[3].amount == 0, "matching stack merge");
            var beforeCancel = Snapshot(runtime.PlayerInventory, storage);
            yield return DragTo(bagButtons[9], new Vector2(Screen.width * .65f, Screen.height * .35f));
            Require(Snapshot(runtime.PlayerInventory, storage) == beforeCancel && runtime.InventoryCursor.IsEmpty, "outside drop lost items");
            yield return Drag(bagButtons[0], equipmentButtons[1]);
            Require(runtime.EquipmentSystem.Get(EquipmentSlot.Head)?.Id == runtime.PlayerInventory.Slots[0].itemId, "gear drop inside storage UI");
            yield return Drag(bagButtons[0], storageButtons[4]);
            Require(Snapshot(runtime.PlayerInventory, storage) == beforeCancel, "worn equipment moved into storage");
            yield return Press(bagButtons[9]);
            pointer += new Vector2(35, -15); yield return null; yield return null;
            escape = true; yield return null; escape = false; yield return null;
            held = false; up = true; yield return null; up = false; yield return null;
            Require(ui.IsOpen && Snapshot(runtime.PlayerInventory, storage) == beforeCancel, "escape must cancel only the drag");
            escape = true; yield return null; escape = false; yield return null; yield return null;
            Require(!ui.IsOpen && !popup.activeSelf && runtime.InventoryCursor.IsEmpty, "popup close");
            pointer = camera.WorldToScreenPoint(chestWorld);
            secondary = true; yield return null; secondary = false; yield return null; yield return null;
            Require(ui.IsOpen && Get<string>(ui, "chestId") == chest.id && Snapshot(runtime.PlayerInventory, storage) == beforeCancel,
                "right-click reopen must preserve chest contents");
            // Exercise the last visible cell and drag again after the popup was reopened.
            yield return Drag(storageButtons[2], storageButtons[39]);
            Require(storage.Slots[39].itemId == "catnip" && storage.Slots[2].amount == 0, "last chest cell");
            yield return Drag(storageButtons[39], bagButtons[10]);
            yield return Drag(bagButtons[10], storageButtons[2]);
            Require(Snapshot(runtime.PlayerInventory, storage) == beforeCancel, "post-reopen drag round trip");
            Invoke(ui, "Refresh");
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/storage-drag-live.png"));
            ScreenCapture.CaptureScreenshot(output);
            Debug.Log("[StorageDragDrop] LIVE PASS: aligned inventory slot art and hit areas, transparent chest with outer frame, popup bounds, unchanged HUD, eleven drags (transfer/swap/merge/cancel/equipment/last cell), right-click open/reopen, Escape close and item conservation. " + output);
        }
        finally { Restore(); Destroy(gameObject); }
    }

    private IEnumerator Press(Button button)
    {
        pointer = Centre(button); yield return null; yield return null;
        down = held = true; yield return null; down = false;
    }
    private IEnumerator Drag(Button source, Button target) => DragTo(source, Centre(target));
    private IEnumerator DragTo(Button source, Vector2 end)
    {
        yield return Press(source);
        var start = pointer;
        for (var step = 1; step <= 12; step++) { pointer = Vector2.Lerp(start, end, step / 12f); yield return null; }
        yield return null; held = false; up = true; yield return null; up = false; yield return null; yield return null;
    }
    private static Vector2 Centre(Button button)
    {
        var rect = (RectTransform)button.transform;
        var canvas = button.GetComponentInParent<Canvas>().rootCanvas;
        return RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            rect.TransformPoint(rect.rect.center));
    }
    private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static object Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    private static string Snapshot(params Bag[] bags) => string.Join("|", bags.SelectMany(b => b.Slots).Select(s => JsonUtility.ToJson(s)));
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException("[StorageDragDrop] LIVE FAIL: " + message); }
    private void Restore()
    {
        if (!running) return;
        running = false;
        GameplayInput.EndReplay();
        if (module != null && module.inputOverride == this) module.inputOverride = previousInput;
        if (body != null) body.simulated = previousSimulated;
        Application.runInBackground = previousBackground;
        Time.timeScale = previousTime;
    }
    protected override void OnDisable() { Restore(); base.OnDisable(); }
}
#endif
