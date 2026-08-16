using System;
using System.Collections.Generic;
using System.Linq;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using UnityEngine;

namespace Nyangbingo.World
{
    public enum StorageTemperatureBand { Ambient, Chilled, Frozen }

    public readonly struct StorageDailyResult
    {
        public StorageDailyResult(int evaluatedContainers, int spoiledStacks, int meltedItems)
        {
            EvaluatedContainers = evaluatedContainers;
            SpoiledStacks = spoiledStacks;
            MeltedItems = meltedItems;
        }
        public int EvaluatedContainers { get; }
        public int SpoiledStacks { get; }
        public int MeltedItems { get; }
    }

    /// <summary>
    /// v72 A-12. 장독의 실제 셀 실온을 하루 1회 판정해 작물·버섯 신선도와 얼음류 개수를 갱신한다.
    /// 얼음 저장고는 별도 상자가 아니라 8x10 냉각 범위를 제공하므로 그 범위 안 장독이 빙결 보관함이 된다.
    /// </summary>
    public sealed class StorageTemperatureService : IDisposable
    {
        public const string OysterMushroomId = "oyster_mushroom";
        public const string ShiitakeId = "shiitake";
        public const string SeogiId = "seogi";
        public const string IceShardId = "ice_shard";
        public const string ThinIceId = "thin_ice";

        private readonly GameDataCatalog catalog;
        private readonly DayNightService time;
        private readonly RoomTempService roomTemperature;
        private readonly MainGameEnvironmentState environment;
        private readonly JangdokStorageRuntime storages;
        private readonly HashSet<string> chilledItemIds;
        private readonly float chilledMaximum;
        private readonly float frozenMaximum;
        private readonly float spoilPerDay;
        private readonly float meltPerDay;
        private bool disposed;

        public StorageTemperatureService(GameDataCatalog data, DayNightService timeService,
            RoomTempService roomTempService, MainGameEnvironmentState environmentState,
            JangdokStorageRuntime storageRuntime)
        {
            catalog = data ?? throw new ArgumentNullException(nameof(data));
            time = timeService ?? throw new ArgumentNullException(nameof(timeService));
            roomTemperature = roomTempService ?? throw new ArgumentNullException(nameof(roomTempService));
            environment = environmentState ?? throw new ArgumentNullException(nameof(environmentState));
            storages = storageRuntime ?? throw new ArgumentNullException(nameof(storageRuntime));
            if (!ReadBool(GlobalKeys.StorageTemperatureSystem) ||
                !TryReadFloat(GlobalKeys.StorageBandChilled, out chilledMaximum) ||
                !TryReadFloat(GlobalKeys.StorageBandFrozen, out frozenMaximum) ||
                !TryReadFloat(GlobalKeys.StorageSpoilPerDay, out spoilPerDay) ||
                !TryReadFloat(GlobalKeys.StorageMeltPerDay, out meltPerDay) ||
                frozenMaximum >= chilledMaximum || spoilPerDay <= 0f || spoilPerDay > 1f ||
                meltPerDay <= 0f || meltPerDay > 1f ||
                !IsValue(GlobalKeys.StorageBandAmbient, "none") ||
                !IsValue(GlobalKeys.StorageJangdokBand, "chilled") ||
                !IsValue(GlobalKeys.StorageIceCoreBand, "frozen") ||
                !IsValue(GlobalKeys.StorageGearBand, "none"))
                throw new InvalidOperationException("v72 보관 globals가 올바르지 않습니다.");

            chilledItemIds = new HashSet<string>(StringComparer.Ordinal)
            {
                OysterMushroomId, ShiitakeId, SeogiId
            };
            foreach (var crop in catalog.Crops)
                if (crop != null && !string.IsNullOrWhiteSpace(crop.CropId))
                    chilledItemIds.Add(crop.CropId);
            time.DailyTick += HandleDailyTick;
        }

        public float ChilledMaximum => chilledMaximum;
        public float FrozenMaximum => frozenMaximum;
        public float SpoilPerDay => spoilPerDay;
        public float MeltPerDay => meltPerDay;
        public StorageDailyResult LastDailyResult { get; private set; }

        public StorageTemperatureBand RequiredBand(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return StorageTemperatureBand.Ambient;
            if (itemId == IceShardId || itemId == ThinIceId) return StorageTemperatureBand.Frozen;
            return chilledItemIds.Contains(itemId)
                ? StorageTemperatureBand.Chilled
                : StorageTemperatureBand.Ambient;
        }

        public StorageTemperatureBand BandAt(float roomTemperatureCelsius) =>
            roomTemperatureCelsius <= frozenMaximum
                ? StorageTemperatureBand.Frozen
                : roomTemperatureCelsius <= chilledMaximum
                    ? StorageTemperatureBand.Chilled
                    : StorageTemperatureBand.Ambient;

        public bool IsAtRisk(string itemId, float roomTemperatureCelsius) =>
            BandAt(roomTemperatureCelsius) < RequiredBand(itemId);

        public bool TryGetStatus(string objectId, out float roomTemperatureCelsius,
            out StorageTemperatureBand band)
        {
            roomTemperatureCelsius = 0f;
            band = StorageTemperatureBand.Ambient;
            if (string.IsNullOrWhiteSpace(objectId)) return false;
            var placed = environment.ExportPlacedObjects().FirstOrDefault(record =>
                string.Equals(record.objectId, objectId, StringComparison.Ordinal) &&
                string.Equals(record.definitionId, JangdokStorageRuntime.DefinitionId,
                    StringComparison.Ordinal));
            if (string.IsNullOrWhiteSpace(placed.objectId)) return false;
            roomTemperatureCelsius = roomTemperature.ResolveExact(placed.position);
            band = BandAt(roomTemperatureCelsius);
            return true;
        }

        public StorageDailyResult ApplyDailyTick()
        {
            var byId = environment.ExportPlacedObjects()
                .Where(record => record.definitionId == JangdokStorageRuntime.DefinitionId)
                .ToDictionary(record => record.objectId, record => record, StringComparer.Ordinal);
            var evaluated = 0;
            var spoiled = 0;
            var melted = 0;
            foreach (var record in storages.Export())
            {
                if (record == null || !byId.TryGetValue(record.objectId, out var placed) ||
                    !storages.TryGet(record.objectId, out var storage)) continue;
                evaluated++;
                var temperature = roomTemperature.ResolveExact(placed.position);
                var slots = storage.Export();
                var changed = false;
                for (var index = 0; index < slots.Count; index++)
                {
                    var slot = slots[index];
                    if (string.IsNullOrEmpty(slot.itemId) || slot.amount <= 0) continue;
                    var requirement = RequiredBand(slot.itemId);
                    if (requirement == StorageTemperatureBand.Chilled && temperature > chilledMaximum)
                    {
                        var condition = ApplyFoodSpoilage(slot.EffectiveStorageCondition, spoilPerDay);
                        if (!slot.hasStorageCondition ||
                            !Mathf.Approximately(slot.storageCondition01, condition))
                        {
                            slot.hasStorageCondition = true;
                            slot.storageCondition01 = condition;
                            slots[index] = slot;
                            changed = true;
                            spoiled++;
                        }
                    }
                    else if (requirement == StorageTemperatureBand.Frozen && temperature > frozenMaximum)
                    {
                        var wholeLoss = CalculateIceMelt(slot.amount, slot.storageMeltRemainder,
                            meltPerDay, out var remainingAmount, out var remainingFraction);
                        slot.storageMeltRemainder = remainingFraction;
                        slot.amount = remainingAmount;
                        slots[index] = slot.amount > 0 ? slot : default;
                        changed = true;
                        melted += wholeLoss;
                    }
                }
                if (changed && !storage.TryImport(slots))
                    throw new InvalidOperationException($"보관 상태 갱신 실패: {record.objectId}");
            }
            LastDailyResult = new StorageDailyResult(evaluated, spoiled, melted);
            return LastDailyResult;
        }

        public static float ApplyFoodSpoilage(float condition01, float lossPerDay) =>
            Mathf.Clamp01(condition01 - Mathf.Clamp01(lossPerDay));

        public static int CalculateIceMelt(int amount, float carriedFraction, float lossPerDay,
            out int remainingAmount, out float remainingFraction)
        {
            amount = Mathf.Max(0, amount);
            carriedFraction = Mathf.Clamp(carriedFraction, 0f, .999999f);
            var exactLoss = amount * Mathf.Clamp01(lossPerDay) + carriedFraction;
            var wholeLoss = Mathf.Min(amount, Mathf.FloorToInt(exactLoss + .00001f));
            remainingAmount = amount - wholeLoss;
            remainingFraction = remainingAmount > 0
                ? Mathf.Clamp(exactLoss - wholeLoss, 0f, .999999f)
                : 0f;
            return wholeLoss;
        }

        public static string BandIcon(StorageTemperatureBand band) => band switch
        {
            StorageTemperatureBand.Frozen => "🧊빙결",
            StorageTemperatureBand.Chilled => "❄냉장",
            _ => "상온"
        };

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            time.DailyTick -= HandleDailyTick;
        }

        private void HandleDailyTick() => ApplyDailyTick();

        private bool ReadBool(string key) =>
            catalog.FindGlobal(key) is { } value && value.TryGetBool(out var parsed) && parsed;
        private bool TryReadFloat(string key, out float result)
        {
            result = 0f;
            var value = catalog.FindGlobal(key);
            return value != null && value.TryGetFloat(out result);
        }
        private bool IsValue(string key, string expected) =>
            string.Equals(catalog.FindGlobal(key)?.Value, expected, StringComparison.Ordinal);
    }
}
