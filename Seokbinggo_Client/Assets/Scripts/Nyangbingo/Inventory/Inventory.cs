using System;
using System.Collections.Generic;
using Nyangbingo.Core;
using Nyangbingo.Data;

namespace Nyangbingo.Inventory
{
    [Serializable]
    public struct InventorySlot { public string itemId; public int amount; }

    public sealed class Inventory
    {
        public const int SlotCount = 50;
        private readonly List<InventorySlot> slots;
        private readonly Func<string, ItemDefinition> findItem;
        public event Action Changed;
        public IReadOnlyList<InventorySlot> Slots => slots;
        public int Capacity => slots.Count;
        public bool IsEmpty => slots.TrueForAll(slot => string.IsNullOrEmpty(slot.itemId));

        public Inventory(Func<string, ItemDefinition> findItem, int slotCount = SlotCount)
        {
            this.findItem = findItem ?? throw new ArgumentNullException(nameof(findItem));
            if (slotCount <= 0) throw new ArgumentOutOfRangeException(nameof(slotCount));
            slots = new List<InventorySlot>(slotCount);
            for (var i = 0; i < slotCount; i++) slots.Add(default);
        }

        public int Count(string itemId)
        {
            long total = 0;
            foreach (var slot in slots)
            {
                if (slot.itemId != itemId) continue;
                total += slot.amount;
                if (total >= int.MaxValue) return int.MaxValue;
            }
            return (int)total;
        }

        public bool Has(string itemId, int amount) => amount > 0 && Count(itemId) >= amount;

        public bool TryAdd(string itemId, int amount)
        {
            var item = findItem(itemId);
            if (item == null || item.MaxStack <= 0 || amount <= 0 || CapacityFor(itemId, item.MaxStack) < amount)
                return false;
            for (var i = 0; i < slots.Count && amount > 0; i++)
            {
                var slot = slots[i];
                if (slot.itemId != itemId || slot.amount >= item.MaxStack) continue;
                var added = Math.Min(amount, item.MaxStack - slot.amount);
                slot.amount += added; amount -= added; slots[i] = slot;
            }
            for (var i = 0; i < slots.Count && amount > 0; i++)
            {
                if (!string.IsNullOrEmpty(slots[i].itemId)) continue;
                var added = Math.Min(amount, item.MaxStack);
                slots[i] = new InventorySlot { itemId = itemId, amount = added }; amount -= added;
            }
            Changed?.Invoke(); return true;
        }

        public bool TryRemove(string itemId, int amount)
        {
            if (!Has(itemId, amount) || amount <= 0) return false;
            for (var i = slots.Count - 1; i >= 0 && amount > 0; i--)
            {
                var slot = slots[i]; if (slot.itemId != itemId) continue;
                var removed = Math.Min(amount, slot.amount); slot.amount -= removed; amount -= removed;
                if (slot.amount == 0) slot.itemId = string.Empty; slots[i] = slot;
            }
            Changed?.Invoke(); return true;
        }

        public List<InventorySlot> Export() => new List<InventorySlot>(slots);
        public void Import(IEnumerable<InventorySlot> saved)
        {
            TryImport(saved);
        }

        public bool TryImport(IEnumerable<InventorySlot> saved)
        {
            if (!TryBuildImport(saved, out var restored)) return false;
            slots.Clear();
            slots.AddRange(restored);
            Changed?.Invoke();
            return true;
        }

        public bool CanImport(IEnumerable<InventorySlot> saved) => TryBuildImport(saved, out _);

        public bool TryTransferSlotTo(int slotIndex, Inventory target)
        {
            if (target == null || ReferenceEquals(this, target) || slotIndex < 0 || slotIndex >= slots.Count)
                return false;
            var slot = slots[slotIndex];
            if (string.IsNullOrEmpty(slot.itemId) || slot.amount <= 0 || !target.TryAdd(slot.itemId, slot.amount))
                return false;
            slots[slotIndex] = default;
            Changed?.Invoke();
            return true;
        }

        private bool TryBuildImport(IEnumerable<InventorySlot> saved, out List<InventorySlot> restored)
        {
            restored = null;
            if (saved == null) return false;
            var candidate = new List<InventorySlot>(Capacity);
            foreach (var slot in saved)
            {
                if (candidate.Count >= Capacity) return false;
                if (string.IsNullOrEmpty(slot.itemId))
                {
                    if (slot.amount != 0) return false;
                    candidate.Add(default);
                    continue;
                }

                var item = findItem(slot.itemId);
                if (item == null || slot.amount <= 0 || slot.amount > item.MaxStack) return false;
                candidate.Add(slot);
            }

            while (candidate.Count < Capacity) candidate.Add(default);
            restored = candidate;
            return true;
        }

        private long CapacityFor(string itemId, int maxStack)
        {
            long capacity = 0;
            foreach (var slot in slots)
            {
                if (slot.itemId == itemId) capacity += Math.Max(0L, (long)maxStack - slot.amount);
                else if (string.IsNullOrEmpty(slot.itemId)) capacity += maxStack;
            }
            return capacity;
        }
    }

    [Serializable]
    public sealed class JangdokStorageRecord
    {
        public string objectId = string.Empty;
        public List<InventorySlot> slots = new List<InventorySlot>();
    }

    /// <summary>v29 장독 창고의 설치물별 40슬롯 보관 상태를 관리한다.</summary>
    public sealed class JangdokStorageRuntime
    {
        public const string DefinitionId = "jangdok";
        public const int SlotCount = 40;

        private readonly Func<string, ItemDefinition> findItem;
        private readonly Dictionary<string, Inventory> byObjectId =
            new Dictionary<string, Inventory>(StringComparer.Ordinal);

        public event Action Changed;
        public int StorageCount => byObjectId.Count;

        public JangdokStorageRuntime(Func<string, ItemDefinition> itemResolver, int slotCount)
        {
            findItem = itemResolver ?? throw new ArgumentNullException(nameof(itemResolver));
            if (slotCount != SlotCount) throw new ArgumentOutOfRangeException(nameof(slotCount));
        }

        public bool TryRegister(string objectId)
        {
            if (string.IsNullOrWhiteSpace(objectId)) return false;
            if (byObjectId.ContainsKey(objectId)) return true;
            var storage = new Inventory(findItem, SlotCount);
            storage.Changed += HandleStorageChanged;
            byObjectId.Add(objectId, storage);
            Changed?.Invoke();
            return true;
        }

        public bool TryGet(string objectId, out Inventory storage)
        {
            storage = null;
            return !string.IsNullOrWhiteSpace(objectId) && byObjectId.TryGetValue(objectId, out storage);
        }

        public bool CanRecover(string objectId) => TryGet(objectId, out var storage) && storage.IsEmpty;

        public bool TryRemoveEmpty(string objectId)
        {
            if (!TryGet(objectId, out var storage) || !storage.IsEmpty) return false;
            storage.Changed -= HandleStorageChanged;
            byObjectId.Remove(objectId);
            Changed?.Invoke();
            return true;
        }

        public List<JangdokStorageRecord> Export()
        {
            var records = new List<JangdokStorageRecord>(byObjectId.Count);
            foreach (var pair in byObjectId)
                records.Add(new JangdokStorageRecord { objectId = pair.Key, slots = pair.Value.Export() });
            records.Sort((left, right) => string.CompareOrdinal(left.objectId, right.objectId));
            return records;
        }

        public bool TryRestore(IEnumerable<JangdokStorageRecord> records, IEnumerable<string> placedJangdokIds)
        {
            if (records == null || placedJangdokIds == null) return false;
            var validIds = new HashSet<string>(placedJangdokIds, StringComparer.Ordinal);
            foreach (var objectId in validIds)
                if (string.IsNullOrWhiteSpace(objectId)) return false;
            var restored = new Dictionary<string, Inventory>(StringComparer.Ordinal);
            foreach (var objectId in validIds)
                restored.Add(objectId, new Inventory(findItem, SlotCount));
            var restoredRecordIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in records)
            {
                if (record == null || string.IsNullOrWhiteSpace(record.objectId) || record.slots == null ||
                    !restoredRecordIds.Add(record.objectId) ||
                    !restored.TryGetValue(record.objectId, out var storage) || !storage.TryImport(record.slots))
                    return false;
            }

            foreach (var storage in byObjectId.Values) storage.Changed -= HandleStorageChanged;
            byObjectId.Clear();
            foreach (var pair in restored)
            {
                pair.Value.Changed += HandleStorageChanged;
                byObjectId.Add(pair.Key, pair.Value);
            }
            Changed?.Invoke();
            return true;
        }

        private void HandleStorageChanged() => Changed?.Invoke();
    }

    /// <summary>
    /// v28 장비 탭의 무기·도구 1칸. 장착물은 소지품 인벤토리에서 분리되며 Q 토글 상태에 따라
    /// 전투 프로필로 사용된다. 채굴은 이 상태를 보지 않고 항상 발톱 티어를 사용한다.
    /// </summary>
    public sealed class ActiveSlotSystem
    {
        private static readonly HashSet<string> AllowedItemIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "dokkaebi_club", "hapjukseon", "cheolseon", "frostclaw_gauntlet", "lantern"
        };

        private readonly Inventory inventory;
        private readonly Func<string, ItemDefinition> findItem;
        private string equippedItemId = string.Empty;
        private bool usingEquippedItem;

        public event Action Changed;
        public string EquippedItemId => equippedItemId;
        public bool HasEquippedItem => !string.IsNullOrEmpty(equippedItemId);
        public bool IsUsingEquippedItem => HasEquippedItem && usingEquippedItem;

        public ActiveSlotSystem(Inventory playerInventory, Func<string, ItemDefinition> itemResolver)
        {
            inventory = playerInventory ?? throw new ArgumentNullException(nameof(playerInventory));
            findItem = itemResolver ?? throw new ArgumentNullException(nameof(itemResolver));
        }

        public static bool IsAllowedItemId(string itemId) =>
            !string.IsNullOrWhiteSpace(itemId) && AllowedItemIds.Contains(itemId);

        public bool TryEquip(string itemId)
        {
            if (!IsValidDefinition(itemId) || equippedItemId == itemId || !inventory.TryRemove(itemId, 1))
                return false;

            var previousItemId = equippedItemId;
            if (!string.IsNullOrEmpty(previousItemId) && !inventory.TryAdd(previousItemId, 1))
            {
                inventory.TryAdd(itemId, 1);
                return false;
            }

            equippedItemId = itemId;
            usingEquippedItem = true;
            Changed?.Invoke();
            return true;
        }

        public bool TryUnequip()
        {
            if (!HasEquippedItem || !inventory.TryAdd(equippedItemId, 1)) return false;
            equippedItemId = string.Empty;
            usingEquippedItem = false;
            Changed?.Invoke();
            return true;
        }

        public bool Toggle()
        {
            if (!HasEquippedItem) return false;
            usingEquippedItem = !usingEquippedItem;
            Changed?.Invoke();
            return true;
        }

        public string ResolveCombatProfileId(string clawProfileId) =>
            IsUsingEquippedItem ? equippedItemId : clawProfileId ?? string.Empty;

        public bool TryRestore(string itemId, bool useItem)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                equippedItemId = string.Empty;
                usingEquippedItem = false;
                Changed?.Invoke();
                return true;
            }
            if (!IsValidDefinition(itemId)) return false;
            equippedItemId = itemId;
            usingEquippedItem = useItem;
            Changed?.Invoke();
            return true;
        }

        private bool IsValidDefinition(string itemId)
        {
            var item = IsAllowedItemId(itemId) ? findItem(itemId) : null;
            return item != null && item.Id == itemId && item.IsInventoryItem;
        }
    }

    /// <summary>
    /// 휴대용 등불의 연료를 게임 시간으로 관리한다. 등불이 활성 슬롯에 장착되고 Q로 활성화된 동안에만
    /// 연료가 줄어들며, 설치형 등불의 전투/봉인 판정에는 참여하지 않는다.
    /// </summary>
    public sealed class PortableLanternRuntime : IGameSecondsTickable, IDisposable
    {
        public const string LanternItemId = "lantern";
        public const string FuelItemId = "coal";
        public const float FuelSecondsPerCoal = 270f;

        private readonly Inventory inventory;
        private readonly ActiveSlotSystem activeSlot;
        private float fuelRemainingSeconds;

        public event Action Changed;
        public float RadiusTiles { get; }
        public float FuelRemainingSeconds => fuelRemainingSeconds;
        public bool IsLit => fuelRemainingSeconds > 0f && activeSlot.IsUsingEquippedItem &&
                             activeSlot.EquippedItemId == LanternItemId;

        public PortableLanternRuntime(Inventory playerInventory, ActiveSlotSystem playerActiveSlot,
            float radiusTiles)
        {
            inventory = playerInventory ?? throw new ArgumentNullException(nameof(playerInventory));
            activeSlot = playerActiveSlot ?? throw new ArgumentNullException(nameof(playerActiveSlot));
            if (float.IsNaN(radiusTiles) || float.IsInfinity(radiusTiles) || radiusTiles <= 0f)
                throw new ArgumentOutOfRangeException(nameof(radiusTiles));
            RadiusTiles = radiusTiles;
            activeSlot.Changed += HandleActiveSlotChanged;
        }

        public bool TryAddFuel(int coalAmount = 1)
        {
            if (coalAmount <= 0) return false;
            var addedSeconds = (double)coalAmount * FuelSecondsPerCoal;
            if (addedSeconds > float.MaxValue - fuelRemainingSeconds ||
                !inventory.TryRemove(FuelItemId, coalAmount)) return false;
            fuelRemainingSeconds += (float)addedSeconds;
            Changed?.Invoke();
            return true;
        }

        public void Tick(float deltaGameSeconds)
        {
            if (!IsLit || deltaGameSeconds <= 0f || float.IsNaN(deltaGameSeconds) ||
                float.IsInfinity(deltaGameSeconds)) return;
            var wasLit = IsLit;
            fuelRemainingSeconds = Math.Max(0f, fuelRemainingSeconds - deltaGameSeconds);
            if (wasLit != IsLit) Changed?.Invoke();
        }

        public bool TryRestore(float remainingGameSeconds)
        {
            if (remainingGameSeconds < 0f || float.IsNaN(remainingGameSeconds) ||
                float.IsInfinity(remainingGameSeconds)) return false;
            fuelRemainingSeconds = remainingGameSeconds;
            Changed?.Invoke();
            return true;
        }

        public void Dispose() => activeSlot.Changed -= HandleActiveSlotChanged;

        private void HandleActiveSlotChanged() => Changed?.Invoke();
    }
}
