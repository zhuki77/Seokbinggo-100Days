using Nyangbingo.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Bag = Nyangbingo.Inventory.Inventory;

namespace Nyangbingo.UI
{
    public sealed partial class MainGameCraftingUiController
    {
        private const string StorageDragHelp = "드래그: 이동 · Shift+클릭: 빠른 이동\n우클릭: 절반/1개 · E/ESC: 닫기";
        private InventorySlotDrag storageDrag;
        private RectTransform storageHudCounter;
        private bool IsDraggingItem => IsDraggingEquipment || storageDrag?.IsActive == true;
        private const float StorageHeaderHeight = 14f;
        private static readonly Vector2 StorageGridSize = new Vector2(
            HudInventoryColumns * HudInventoryCellSize + (HudInventoryColumns - 1) * HudInventoryCellGap,
            5 * HudInventoryCellSize + 4 * HudInventoryCellGap);
        private static readonly Vector2 StoragePopupSize = StorageGridSize + new Vector2(0f, StorageHeaderHeight);

        private bool TryGetOpenStorage(out Bag storage)
        {
            storage = null;
            if (!open || !HasStorageMode) return false;
            return chestProgress != null ? chestProgress.TryGetContents(chestId, out storage) :
                runtimeServices?.JangdokStorage != null &&
                runtimeServices.JangdokStorage.TryGet(storageObjectId, out storage);
        }

        private void WireStorageDragSlots()
        {
            for (var index = 0; index < storageLabels.Length; index++)
            {
                var captured = index;
                var button = storageLabels[index].transform.parent.GetComponent<Button>();
                var drag = button.GetComponent<InventoryEquipmentDragSource>() ??
                           button.gameObject.AddComponent<InventoryEquipmentDragSource>();
                drag.Configure(pointer =>
                {
                    if (TryGetOpenStorage(out var storage))
                        BeginStorageItemDrag(storage, captured, button.gameObject, pointer);
                }, CancelEquipmentDrag);
                WireEquipmentDrop(button, pointer =>
                {
                    if (TryGetOpenStorage(out var storage)) DropStorageItem(storage, captured, pointer);
                });
            }
            for (var index = 0; index < storagePlayerLabels.Length; index++)
            {
                var captured = index;
                WireEquipmentDrop(storagePlayerLabels[index].transform.parent.GetComponent<Button>(),
                    pointer => DropStorageItem(runtimeServices.PlayerInventory, captured, pointer));
            }
        }

        private void BeginStorageItemDrag(Bag sourceInventory, int index, GameObject source, PointerEventData pointer)
        {
            if (runtimeServices?.IsInitialized != true || HasHeldInventoryItem ||
                source == null || !source.activeInHierarchy || !TryGetOpenStorage(out var storage) ||
                (shell != null && shell.Screen != GameShellScreen.Gameplay) ||
                sourceInventory != storage && sourceInventory != runtimeServices.PlayerInventory) return;
            CancelEquipmentDrag();
            storageDrag ??= new InventorySlotDrag();
            if (!storageDrag.Begin(sourceInventory, index)) return;
            equipmentDragSource = source;
            equipmentDragPointerId = pointer.pointerId;
            if (sourceInventory == runtimeServices.PlayerInventory)
            {
                equipmentDrag ??= new EquipmentInventoryDrag(sourceInventory, gameDataCatalog.FindEquipment);
                equipmentDrag.Begin(index);
            }
            if (turretRuntime != null && turretRuntime.IsPlacementPreviewActive) turretRuntime.CancelPlacementPreview();
            message = string.Empty;
            RefreshEquipmentVisual();
        }

        private void DropStorageItem(Bag target, int index, PointerEventData pointer)
        {
            if (storageDrag?.IsActive != true || pointer.pointerDrag != equipmentDragSource ||
                pointer.pointerId != equipmentDragPointerId) return;
            var source = storageDrag.Source;
            if (equipmentDragSource == null || !equipmentDragSource.activeInHierarchy ||
                !TryGetOpenStorage(out var storage) ||
                (shell != null && shell.Screen != GameShellScreen.Gameplay) ||
                source != storage && source != runtimeServices.PlayerInventory ||
                target != storage && target != runtimeServices.PlayerInventory ||
                target == null || index < 0 || index >= target.Capacity)
            {
                CancelEquipmentDrag();
                return;
            }
            // A cross-container swap must protect worn gear on either side.
            var movingWornItem = source != target &&
                (source == runtimeServices.PlayerInventory && runtimeServices.IsEquippedItem(storageDrag.Preview.itemId) ||
                 target == runtimeServices.PlayerInventory && runtimeServices.IsEquippedItem(target.Slots[index].itemId));
            var moved = !movingWornItem && storageDrag.TryDrop(target, index);
            CancelEquipmentDrag();
            message = movingWornItem ? "장착 중인 장비는 먼저 해제해주세요." :
                moved ? "아이템을 이동했습니다." : "이 칸에는 옮길 수 없습니다. 빈 칸에 놓아주세요.";
            Refresh();
        }

        private void BuildStoragePopup()
        {
            var canvas = panel.GetComponentInParent<Canvas>()?.rootCanvas;
            if (canvas == null) return;
            storageHudCounter = FindAnyObjectByType<MainGameHudController>()?.TearBalanceRect;
            var root = (RectTransform)storageModeRoot.transform;
            root.SetParent(canvas.transform, false);
            PlaceTopLeft(root, 4f, 69f, StoragePopupSize.x, StoragePopupSize.y);
            var background = storageModeRoot.GetComponent<Image>() ?? storageModeRoot.AddComponent<Image>();
            // No large panel or border: the same individual cells as the HUD inventory.
            background.color = Color.clear;
            background.raycastTarget = true;
            if (storageModeRoot.TryGetComponent<Outline>(out var border)) border.enabled = false;
            // Keep the authored storage slots and the existing HUD inventory. No duplicate player grid.
            storageModeRoot.transform.Find("PlayerGrid")?.gameObject.SetActive(false);
            storageModeRoot.transform.Find("PlayerLabel")?.gameObject.SetActive(false);
            var grid = (RectTransform)storageModeRoot.transform.Find("StorageGrid");
            if (grid != null)
            {
                PlaceTopLeft(grid, 0f, StorageHeaderHeight, StorageGridSize.x, StorageGridSize.y);
                var layout = grid.GetComponent<GridLayoutGroup>();
                if (layout != null)
                {
                    layout.cellSize = Vector2.one * HudInventoryCellSize;
                    layout.spacing = Vector2.one * HudInventoryCellGap;
                    layout.padding = new RectOffset();
                    layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                    layout.constraintCount = HudInventoryColumns;
                    layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
                    layout.startAxis = GridLayoutGroup.Axis.Horizontal;
                    layout.childAlignment = TextAnchor.UpperLeft;
                }
                // The old full-grid atlas had different padding and stretched the cells.
                if (grid.TryGetComponent<Image>(out var gridImage)) gridImage.enabled = false;
            }
            foreach (var label in storageLabels)
            {
                if (label == null) continue;
                var button = label.transform.parent.GetComponent<Button>();
                var cellImage = button.GetComponent<Image>();
                cellImage.sprite = gameplayArtCatalog?.InventorySlot;
                cellImage.type = Image.Type.Simple;
                cellImage.preserveAspect = true;
                cellImage.color = cellImage.sprite != null ? Color.white : new Color(.12f, .17f, .25f, .96f);
                cellImage.enabled = cellImage.raycastTarget = true;
                button.targetGraphic = cellImage;
                button.transition = Selectable.Transition.None;
                if (inventoryGridLabels.Length > 0 && inventoryGridLabels[0] != null)
                {
                    label.font = inventoryGridLabels[0].font;
                    label.fontStyle = inventoryGridLabels[0].fontStyle;
                }
            }
            foreach (var icon in storageIcons)
                if (icon != null)
                {
                    var rect = icon.rectTransform;
                    rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = Vector2.one * (HudInventoryCellSize - 2f);
                    icon.preserveAspect = true;
                    icon.raycastTarget = false;
                }
            PlaceTopLeft(storageLabelText.rectTransform, 0f, 0f, StorageGridSize.x, 12f);
            storageLabelText.fontSize = 8;
            storageLabelText.alignment = TextAnchor.MiddleLeft;
            storageLabelText.raycastTarget = false;
            PlaceTopLeft(storageHintText.rectTransform, 0f, StoragePopupSize.y + 4f, StorageGridSize.x, 28f);
            storageHintText.fontSize = 6;
            storageHintText.alignment = TextAnchor.UpperLeft;
            storageHintText.horizontalOverflow = HorizontalWrapMode.Wrap;
            storageHintText.verticalOverflow = VerticalWrapMode.Truncate;
            storageHintText.raycastTarget = false;
        }

        // Top-left coordinates, independent of resolution; prefer below the unchanged inventory.
        public static Rect ResolveStoragePopupRect(Vector2 canvasSize, Rect inventory, Vector2 size)
        {
            const float margin = 4f;
            size.x = Mathf.Min(size.x, Mathf.Max(1f, canvasSize.x - margin * 2));
            size.y = Mathf.Min(size.y, Mathf.Max(1f, canvasSize.y - margin * 2));
            var position = new Vector2(inventory.xMin, inventory.yMax + 6f);
            if (position.y + size.y > canvasSize.y - margin)
                position = new Vector2(inventory.xMax + 6f, inventory.yMin);
            position.x = Mathf.Clamp(position.x, margin, Mathf.Max(margin, canvasSize.x - size.x - margin));
            position.y = Mathf.Clamp(position.y, margin, Mathf.Max(margin, canvasSize.y - size.y - margin));
            return new Rect(position, size);
        }

        private void PositionStoragePopup()
        {
            if (storageModeRoot == null || inventoryScrollRoot == null) return;
            var root = (RectTransform)storageModeRoot.transform;
            var canvas = (RectTransform)root.parent;
            var inventoryRect = (RectTransform)inventoryScrollRoot.transform;
            var min = canvas.InverseTransformPoint(inventoryRect.TransformPoint(inventoryRect.rect.min));
            var max = canvas.InverseTransformPoint(inventoryRect.TransformPoint(inventoryRect.rect.max));
            var inventory = new Rect(min.x - canvas.rect.xMin, canvas.rect.yMax - max.y,
                max.x - min.x, max.y - min.y);
            if (storageHudCounter != null && storageHudCounter.gameObject.activeInHierarchy)
            {
                var footerBottom = canvas.InverseTransformPoint(storageHudCounter.TransformPoint(storageHudCounter.rect.min));
                inventory.yMax = Mathf.Max(inventory.yMax, canvas.rect.yMax - footerBottom.y);
            }
            var size = StoragePopupSize;
            if (chestProgress == null) size.y += 32f; // Preserve the jar's cooling feedback.
            root.sizeDelta = size;
            var scale = Mathf.Min(1f, (canvas.rect.width - 8f) / size.x,
                (canvas.rect.height - 8f) / size.y);
            var placement = ResolveStoragePopupRect(canvas.rect.size, inventory, size * scale);
            root.anchoredPosition = new Vector2(placement.x, -placement.y);
            root.localScale = Vector3.one * scale;
        }
    }
}
