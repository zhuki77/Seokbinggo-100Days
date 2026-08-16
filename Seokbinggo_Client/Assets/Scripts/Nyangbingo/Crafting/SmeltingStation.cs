using Nyangbingo.Data;
using Nyangbingo.Inventory;
using Nyangbingo.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nyangbingo.Crafting
{
    public sealed class SmeltingStation : IGameSecondsTickable
    {
        private readonly Nyangbingo.Inventory.Inventory inventory;
        private readonly SmeltingStationKind stationKind;
        private readonly int queueCapacity;
        private readonly List<SmeltingDefinition> queued = new List<SmeltingDefinition>();
        private SmeltingDefinition active;
        private float remaining;
        private readonly List<ItemAmount> completed = new List<ItemAmount>();
        private readonly Func<Vector2, int> roomTemperatureProvider;
        private readonly bool strictTemperature;
        private readonly int frozenEnterCelsius;
        private Vector2 worldPosition;
        private bool hasWorldPosition;
        public bool IsSmelting => active != null;
        public SmeltingDefinition Active => active;
        public float RemainingSeconds => remaining;
        public SmeltingStationKind StationKind => stationKind;
        public int QueueCapacity => queueCapacity;
        public IReadOnlyList<ItemAmount> Completed => completed;
        public IReadOnlyList<SmeltingDefinition> Queue => queued;
        public bool IsTemperatureSuitable => !strictTemperature || !hasWorldPosition ||
            roomTemperatureProvider == null ||
            StationTemperatureRules.TempOk(stationKind,
                roomTemperatureProvider(worldPosition), frozenEnterCelsius);
        public SmeltingStation(Nyangbingo.Inventory.Inventory inventory,
            SmeltingStationKind stationKind, int batchCapacity,
            Func<Vector2, int> temperatureProvider = null, bool temperatureStrict = false,
            int frozenTemperature = -10)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            if (batchCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(batchCapacity));
            this.inventory = inventory;
            this.stationKind = stationKind;
            queueCapacity = batchCapacity;
            roomTemperatureProvider = temperatureProvider;
            strictTemperature = temperatureStrict;
            frozenEnterCelsius = frozenTemperature;
        }

        public bool SetWorldPosition(Vector2 position)
        {
            if (float.IsNaN(position.x) || float.IsInfinity(position.x) ||
                float.IsNaN(position.y) || float.IsInfinity(position.y)) return false;
            worldPosition = position;
            hasWorldPosition = true;
            return true;
        }

        public bool TryStart(SmeltingDefinition definition)
        {
            if (!IsTemperatureSuitable || !IsValidDefinition(definition) ||
                queued.Count + (active == null ? 0 : 1) >= queueCapacity) return false;
            var inputId = definition.Input.item.Id;
            var fuelId = definition.Fuel.item.Id;
            if (inputId == fuelId)
            {
                if (definition.Input.amount > int.MaxValue - definition.Fuel.amount) return false;
                var totalRequired = definition.Input.amount + definition.Fuel.amount;
                if (!inventory.TryRemove(inputId, totalRequired)) return false;
            }
            else
            {
                if (!inventory.Has(inputId, definition.Input.amount) || !inventory.Has(fuelId, definition.Fuel.amount) ||
                    !inventory.TryRemove(inputId, definition.Input.amount)) return false;
                if (!inventory.TryRemove(fuelId, definition.Fuel.amount))
                {
                    inventory.TryAdd(inputId, definition.Input.amount);
                    return false;
                }
            }
            queued.Add(definition);
            StartNextIfIdle();
            return true;
        }

        public bool Tick(float gameSeconds)
        {
            if (gameSeconds <= 0f || float.IsNaN(gameSeconds) || float.IsInfinity(gameSeconds)) return false;
            if (!IsTemperatureSuitable) return false;
            StartNextIfIdle();
            if (!IsSmelting) return false;

            var completedAny = false;
            var elapsed = gameSeconds;
            while (active != null && elapsed >= remaining)
            {
                elapsed -= remaining;
                completed.Add(active.Output);
                active = null;
                remaining = 0f;
                completedAny = true;
                StartNextIfIdle();
            }

            if (active != null && elapsed > 0f) remaining -= elapsed;
            return completedAny;
        }

        void IGameSecondsTickable.Tick(float deltaGameSeconds) => Tick(deltaGameSeconds);

        public bool TryCollect(int index)
        {
            if (index < 0 || index >= completed.Count) return false;
            var output = completed[index];
            if (!inventory.TryAdd(output.item.Id, output.amount)) return false;
            completed.RemoveAt(index); return true;
        }

        public bool RestoreState(SmeltingDefinition activeDefinition, float remainingSeconds,
            IEnumerable<SmeltingDefinition> queuedDefinitions, IEnumerable<ItemAmount> completedOutputs)
        {
            if (float.IsNaN(remainingSeconds) || float.IsInfinity(remainingSeconds) || remainingSeconds < 0f ||
                queuedDefinitions == null || completedOutputs == null) return false;

            var restoredQueue = new List<SmeltingDefinition>();
            foreach (var definition in queuedDefinitions)
            {
                if (!IsValidDefinition(definition)) return false;
                restoredQueue.Add(definition);
            }
            if (activeDefinition != null && (!IsValidDefinition(activeDefinition) ||
                remainingSeconds > activeDefinition.DurationSeconds)) return false;
            if (activeDefinition == null && remainingSeconds > 0f) return false;
            if (activeDefinition == null && restoredQueue.Count > 0) return false;
            if (restoredQueue.Count + (activeDefinition == null ? 0 : 1) > queueCapacity) return false;

            var restoredCompleted = new List<ItemAmount>();
            foreach (var output in completedOutputs)
            {
                if (output.item == null || output.amount <= 0) return false;
                restoredCompleted.Add(output);
            }

            active = activeDefinition;
            remaining = activeDefinition == null ? 0f : remainingSeconds;
            queued.Clear();
            queued.AddRange(restoredQueue);
            completed.Clear();
            completed.AddRange(restoredCompleted);
            return true;
        }

        private bool IsValidDefinition(SmeltingDefinition definition)
        {
            return definition != null && definition.StationKind == stationKind &&
                   definition.BatchCapacity == queueCapacity &&
                   definition.Input.item != null && definition.Fuel.item != null && definition.Output.item != null &&
                   definition.Input.amount > 0 && definition.Fuel.amount > 0 && definition.Output.amount > 0 &&
                   definition.DurationSeconds > 0f && !float.IsNaN(definition.DurationSeconds) &&
                   !float.IsInfinity(definition.DurationSeconds);
        }

        private void StartNextIfIdle()
        {
            if (active != null || queued.Count == 0) return;
            active = queued[0]; queued.RemoveAt(0); remaining = active.DurationSeconds;
        }
    }


    public static class StationTemperatureRules
    {
        /// <summary>v72 A-3: 제련 설치 설비는 빙결 구간에서 시작·진행을 멈춘다.</summary>
        public static bool TempOk(SmeltingStationKind stationKind, int roomTemperatureC,
            int frozenEnterCelsius = -10) => roomTemperatureC > frozenEnterCelsius;
    }
}
