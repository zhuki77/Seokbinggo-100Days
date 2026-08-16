using System;
using System.Collections.Generic;
using Nyangbingo.Core;
using Nyangbingo.Data;

namespace Nyangbingo.Inventory
{
    [Serializable]
    public struct InventorySlot
    {
        public string itemId;
        public int amount;
        /// <summary>v72 보관 상함이 적용된 스택만 true. 구 세이브의 false/0은 신선도 100%로 해석한다.</summary>
        public bool hasStorageCondition;
        [UnityEngine.Range(0f, 1f)] public float storageCondition01;
        /// <summary>얼음류 25%/일을 정수 개수에 정확히 누적하기 위한 0~1 미만 잔여량.</summary>
        [UnityEngine.Range(0f, 1f)] public float storageMeltRemainder;

        public float EffectiveStorageCondition => hasStorageCondition ? storageCondition01 : 1f;
    }

    public sealed class Inventory
    {
        public const int SlotCount = 50;
        private readonly List<InventorySlot> slots;
        private readonly Func<string, ItemDefinition> findItem;
        private readonly int reservedAutoFillSlotCount;
        private readonly Func<string, bool> canAutoFillReservedSlot;
        public event Action Changed;
        public IReadOnlyList<InventorySlot> Slots => slots;
        public int Capacity => slots.Count;
        public bool IsEmpty => slots.TrueForAll(slot => string.IsNullOrEmpty(slot.itemId));

        public Inventory(Func<string, ItemDefinition> findItem, int slotCount = SlotCount,
            int reservedAutoFillSlotCount = 0, Func<string, bool> canAutoFillReservedSlot = null)
        {
            this.findItem = findItem ?? throw new ArgumentNullException(nameof(findItem));
            if (slotCount <= 0) throw new ArgumentOutOfRangeException(nameof(slotCount));
            if (reservedAutoFillSlotCount < 0 || reservedAutoFillSlotCount > slotCount)
                throw new ArgumentOutOfRangeException(nameof(reservedAutoFillSlotCount));
            if (reservedAutoFillSlotCount > 0 && canAutoFillReservedSlot == null)
                throw new ArgumentNullException(nameof(canAutoFillReservedSlot));
            this.reservedAutoFillSlotCount = reservedAutoFillSlotCount;
            this.canAutoFillReservedSlot = canAutoFillReservedSlot;
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

        public ItemDefinition FindItem(string itemId) =>
            string.IsNullOrEmpty(itemId) ? null : findItem(itemId);

        public bool Has(string itemId, int amount) => amount > 0 && Count(itemId) >= amount;

        public bool TryAdd(string itemId, int amount)
            => TryAddWithStorageState(itemId, amount, false, 1f, 0f);

        public bool TryAddWithStorageState(string itemId, int amount, bool hasCondition,
            float condition01, float meltRemainder)
        {
            var item = findItem(itemId);
            if (item == null || item.MaxStack <= 0 || amount <= 0 ||
                !IsValidStorageState(hasCondition, condition01, meltRemainder) ||
                CapacityFor(itemId, item.MaxStack, hasCondition, condition01, meltRemainder) < amount)
                return false;
            var firstAutoFillSlot = FirstAutoFillSlot(itemId);
            for (var i = firstAutoFillSlot; i < slots.Count && amount > 0; i++)
            {
                var slot = slots[i];
                if (slot.itemId != itemId || slot.amount >= item.MaxStack ||
                    !HasSameStorageState(slot, hasCondition, condition01, meltRemainder)) continue;
                var added = Math.Min(amount, item.MaxStack - slot.amount);
                slot.amount += added; amount -= added; slots[i] = slot;
            }
            for (var i = firstAutoFillSlot; i < slots.Count && amount > 0; i++)
            {
                if (!string.IsNullOrEmpty(slots[i].itemId)) continue;
                var added = Math.Min(amount, item.MaxStack);
                slots[i] = new InventorySlot
                {
                    itemId = itemId,
                    amount = added,
                    hasStorageCondition = hasCondition,
                    storageCondition01 = hasCondition ? condition01 : 0f,
                    storageMeltRemainder = meltRemainder
                };
                amount -= added;
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
                if (slot.amount == 0) slot = default; slots[i] = slot;
            }
            Changed?.Invoke(); return true;
        }

        public bool TryRemoveOneWithStorageCondition(string itemId, out float condition01)
        {
            condition01 = 1f;
            if (string.IsNullOrWhiteSpace(itemId)) return false;
            for (var i = slots.Count - 1; i >= 0; i--)
            {
                var slot = slots[i];
                if (slot.itemId != itemId || slot.amount <= 0) continue;
                condition01 = slot.EffectiveStorageCondition;
                slot.amount--;
                slots[i] = slot.amount > 0 ? slot : default;
                Changed?.Invoke();
                return true;
            }
            return false;
        }

        public bool TryRemoveFromOccupiedSlots(int maximumSlots, int maximumAmount,
            out List<InventorySlot> removedStacks)
        {
            removedStacks = new List<InventorySlot>();
            if (maximumSlots <= 0 || maximumAmount <= 0) return false;
            var candidates = new List<int>();
            for (var index = 0; index < slots.Count; index++)
                if (!string.IsNullOrEmpty(slots[index].itemId) && slots[index].amount > 0)
                    candidates.Add(index);
            var removedAmount = 0;
            while (candidates.Count > 0 && removedStacks.Count < maximumSlots &&
                   removedAmount < maximumAmount)
            {
                var candidateIndex = UnityEngine.Random.Range(0, candidates.Count);
                var index = candidates[candidateIndex];
                candidates.RemoveAt(candidateIndex);
                var slot = slots[index];
                var removed = Math.Min(slot.amount, maximumAmount - removedAmount);
                removedStacks.Add(new InventorySlot
                {
                    itemId = slot.itemId,
                    amount = removed,
                    hasStorageCondition = slot.hasStorageCondition,
                    storageCondition01 = slot.storageCondition01,
                    storageMeltRemainder = slot.storageMeltRemainder
                });
                slot.amount -= removed;
                removedAmount += removed;
                if (slot.amount <= 0) slot = default;
                slots[index] = slot;
            }
            if (removedStacks.Count == 0) return false;
            Changed?.Invoke();
            return true;
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
            if (string.IsNullOrEmpty(slot.itemId) || slot.amount <= 0 ||
                !target.TryAddWithStorageState(slot.itemId, slot.amount,
                    slot.hasStorageCondition, slot.EffectiveStorageCondition,
                    slot.storageMeltRemainder))
                return false;
            slots[slotIndex] = default;
            Changed?.Invoke();
            return true;
        }

        public bool TrySwapSlots(int firstIndex, int secondIndex)
        {
            if (firstIndex < 0 || firstIndex >= slots.Count ||
                secondIndex < 0 || secondIndex >= slots.Count ||
                firstIndex == secondIndex)
                return false;
            var first = slots[firstIndex];
            slots[firstIndex] = slots[secondIndex];
            slots[secondIndex] = first;
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
                    if (slot.amount != 0 || slot.hasStorageCondition ||
                        slot.storageCondition01 != 0f || slot.storageMeltRemainder != 0f) return false;
                    candidate.Add(default);
                    continue;
                }

                var item = findItem(slot.itemId);
                if (item == null || slot.amount <= 0 || slot.amount > item.MaxStack ||
                    !IsValidStorageState(slot.hasStorageCondition,
                        slot.EffectiveStorageCondition, slot.storageMeltRemainder)) return false;
                candidate.Add(slot);
            }

            while (candidate.Count < Capacity) candidate.Add(default);
            restored = candidate;
            return true;
        }

        private long CapacityFor(string itemId, int maxStack, bool hasCondition,
            float condition01, float meltRemainder)
        {
            long capacity = 0;
            for (var index = FirstAutoFillSlot(itemId); index < slots.Count; index++)
            {
                var slot = slots[index];
                if (slot.itemId == itemId &&
                    HasSameStorageState(slot, hasCondition, condition01, meltRemainder))
                    capacity += Math.Max(0L, (long)maxStack - slot.amount);
                else if (string.IsNullOrEmpty(slot.itemId)) capacity += maxStack;
            }
            return capacity;
        }

        private int FirstAutoFillSlot(string itemId) =>
            reservedAutoFillSlotCount == 0 || canAutoFillReservedSlot(itemId)
                ? 0
                : reservedAutoFillSlotCount;

        private static bool IsValidStorageState(bool hasCondition, float condition01,
            float meltRemainder) =>
            !float.IsNaN(condition01) && !float.IsInfinity(condition01) &&
            !float.IsNaN(meltRemainder) && !float.IsInfinity(meltRemainder) &&
            (!hasCondition || condition01 >= 0f && condition01 <= 1f) &&
            meltRemainder >= 0f && meltRemainder < 1f;

        private static bool HasSameStorageState(InventorySlot slot, bool hasCondition,
            float condition01, float meltRemainder) =>
            slot.hasStorageCondition == hasCondition &&
            (!hasCondition || Math.Abs(slot.storageCondition01 - condition01) <= .0001f) &&
            Math.Abs(slot.storageMeltRemainder - meltRemainder) <= .0001f;
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
            // 장착물이 없어도 이미 맨 발톱(빈손) 상태이므로 성공으로 처리한다.
            if (!HasEquippedItem)
            {
                usingEquippedItem = false;
                Changed?.Invoke();
                return true;
            }

            usingEquippedItem = !usingEquippedItem;
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// 무기·도구를 쓰지 않는 빈손(맨 발톱) 상태로 만든다.
        /// 장착물은 유지한 채 비활성만 한다(인벤이 가득 차도 실패하지 않음).
        /// </summary>
        public bool SelectBareHands()
        {
            if (!HasEquippedItem)
            {
                if (usingEquippedItem)
                {
                    usingEquippedItem = false;
                    Changed?.Invoke();
                }
                return true;
            }

            if (!usingEquippedItem) return true;
            usingEquippedItem = false;
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
