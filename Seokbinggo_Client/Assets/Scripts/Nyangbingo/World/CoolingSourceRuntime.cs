using System;
using System.Collections.Generic;
using System.Linq;
using Nyangbingo.Core;
using Nyangbingo.Data;
using UnityEngine;

namespace Nyangbingo.World
{
    public readonly struct CoolingSourceSnapshot
    {
        public CoolingSourceSnapshot(string objectId, string definitionId, float remainingGameSeconds)
        {
            ObjectId = objectId;
            DefinitionId = definitionId;
            RemainingGameSeconds = remainingGameSeconds;
        }

        public string ObjectId { get; }
        public string DefinitionId { get; }
        public float RemainingGameSeconds { get; }
    }

    /// <summary>
    /// 설치형 냉기원의 데이터 기반 수명과 상한을 관리한다. 시간은 중앙 game-seconds Tick만 소비하며,
    /// 월드 좌표·밀폐 영역 계산은 소유하지 않는다.
    /// </summary>
    public sealed class CoolingSourceRuntime : IGameSecondsTickable
    {
        private enum LifetimeKind { Consumable, Fueled, Permanent }

        private sealed class Entry
        {
            public string ObjectId;
            public string DefinitionId;
            public LifetimeKind Lifetime;
            public float CapPercent;
            public float RemainingGameSeconds;

            public bool IsActive => Lifetime == LifetimeKind.Permanent || RemainingGameSeconds > 0f;
        }

        public const string WaterJarId = "water_jar";
        public const string IceJarId = "ice_jar";
        public const string IceStorageId = "ice_core";
        public const string IceCrystalCoolerId = "ice_crystal_cooler";

        private readonly Dictionary<string, Entry> entries =
            new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly List<string> expired = new List<string>();
        private readonly float waterJarDuration;
        private readonly float iceJarFuelDuration;
        private readonly float waterJarCap;
        private readonly float iceJarCap;
        private readonly float iceStorageCap;
        private readonly float iceCrystalCoolerCap;

        public CoolingSourceRuntime(GameDataCatalog catalog)
        {
            waterJarDuration = Positive(Read(catalog, "muldanji_duration", 180f), 180f);
            iceJarFuelDuration = Positive(Read(catalog, "icejar_fuel_sec", 300f), 300f);
            waterJarCap = Percent(Read(catalog, "cold_cap_muldanji", 25f));
            iceJarCap = Percent(Read(catalog, "cold_cap_icejar", 50f));
            iceStorageCap = Percent(Read(catalog, "cold_cap_icestorage", 100f));
            iceCrystalCoolerCap = Percent(Read(catalog, "cold_cap_frostcooler", 100f));
        }

        public int Count => entries.Count;
        public int ActiveCount => entries.Values.Count(entry => entry.IsActive);
        public bool IsActive => ActiveCount > 0;
        public float CoolingCapPercent => entries.Values
            .Where(entry => entry.IsActive)
            .Select(entry => entry.CapPercent)
            .DefaultIfEmpty(0f)
            .Max();
        public event Action<string> ConsumableExpired;

        public bool TryRegister(string objectId, string definitionId, bool startFueled = false)
        {
            if (!TryCreate(objectId, definitionId, out var entry) || entries.ContainsKey(objectId)) return false;
            if (entry.Lifetime == LifetimeKind.Consumable) entry.RemainingGameSeconds = waterJarDuration;
            else if (entry.Lifetime == LifetimeKind.Fueled && startFueled)
                entry.RemainingGameSeconds = iceJarFuelDuration;
            entries.Add(objectId, entry);
            return true;
        }

        public bool TryRestore(string objectId, string definitionId, float remainingGameSeconds)
        {
            if (!IsFinite(remainingGameSeconds) || remainingGameSeconds < 0f ||
                !TryCreate(objectId, definitionId, out var entry) || entries.ContainsKey(objectId)) return false;
            if (entry.Lifetime != LifetimeKind.Permanent)
                entry.RemainingGameSeconds = remainingGameSeconds;
            entries.Add(objectId, entry);
            return true;
        }

        public bool TryAddIceFuel(string objectId, int units = 1)
        {
            if (units <= 0 || !entries.TryGetValue(objectId, out var entry) ||
                entry.Lifetime != LifetimeKind.Fueled) return false;
            var added = iceJarFuelDuration * units;
            if (!IsFinite(added) || entry.RemainingGameSeconds > float.MaxValue - added) return false;
            entry.RemainingGameSeconds += added;
            return true;
        }

        public bool TryGetRemaining(string objectId, out float remainingGameSeconds)
        {
            remainingGameSeconds = 0f;
            if (string.IsNullOrWhiteSpace(objectId) || !entries.TryGetValue(objectId, out var entry)) return false;
            remainingGameSeconds = entry.Lifetime == LifetimeKind.Permanent
                ? float.PositiveInfinity
                : entry.RemainingGameSeconds;
            return true;
        }

        public bool TryGetStatus(string objectId, out float remainingGameSeconds,
            out float capPercent, out bool active)
        {
            remainingGameSeconds = 0f;
            capPercent = 0f;
            active = false;
            if (string.IsNullOrWhiteSpace(objectId) || !entries.TryGetValue(objectId, out var entry)) return false;
            remainingGameSeconds = entry.Lifetime == LifetimeKind.Permanent
                ? float.PositiveInfinity
                : entry.RemainingGameSeconds;
            capPercent = entry.CapPercent;
            active = entry.IsActive;
            return true;
        }

        public bool Remove(string objectId) =>
            !string.IsNullOrWhiteSpace(objectId) && entries.Remove(objectId);

        public void Clear() => entries.Clear();

        public IReadOnlyList<CoolingSourceSnapshot> ExportSnapshots() => entries.Values
            .OrderBy(entry => entry.ObjectId, StringComparer.Ordinal)
            .Select(entry => new CoolingSourceSnapshot(
                entry.ObjectId, entry.DefinitionId,
                entry.Lifetime == LifetimeKind.Permanent ? 0f : entry.RemainingGameSeconds))
            .ToArray();

        public void Tick(float deltaGameSeconds)
        {
            if (!IsFinite(deltaGameSeconds) || deltaGameSeconds <= 0f) return;
            expired.Clear();
            foreach (var entry in entries.Values)
            {
                if (entry.Lifetime == LifetimeKind.Permanent || entry.RemainingGameSeconds <= 0f) continue;
                entry.RemainingGameSeconds = Mathf.Max(0f, entry.RemainingGameSeconds - deltaGameSeconds);
                if (entry.Lifetime == LifetimeKind.Consumable && entry.RemainingGameSeconds <= 0f)
                    expired.Add(entry.ObjectId);
            }

            for (var index = 0; index < expired.Count; index++)
            {
                var objectId = expired[index];
                entries.Remove(objectId);
                ConsumableExpired?.Invoke(objectId);
            }
        }

        public static bool IsCoolingDefinition(string definitionId) =>
            definitionId == WaterJarId || definitionId == IceJarId || definitionId == IceStorageId ||
            definitionId == IceCrystalCoolerId;

        private bool TryCreate(string objectId, string definitionId, out Entry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(objectId) || string.IsNullOrWhiteSpace(definitionId)) return false;
            switch (definitionId)
            {
                case WaterJarId:
                    entry = Create(objectId, definitionId, LifetimeKind.Consumable, waterJarCap);
                    break;
                case IceJarId:
                    entry = Create(objectId, definitionId, LifetimeKind.Fueled, iceJarCap);
                    break;
                case IceStorageId:
                    entry = Create(objectId, definitionId, LifetimeKind.Permanent, iceStorageCap);
                    break;
                case IceCrystalCoolerId:
                    entry = Create(objectId, definitionId, LifetimeKind.Permanent, iceCrystalCoolerCap);
                    break;
            }
            return entry != null;
        }

        private static Entry Create(string objectId, string definitionId, LifetimeKind lifetime, float cap) =>
            new Entry
            {
                ObjectId = objectId,
                DefinitionId = definitionId,
                Lifetime = lifetime,
                CapPercent = cap
            };

        private static float Read(GameDataCatalog catalog, string key, float fallback)
        {
            var definition = catalog?.FindGlobal(key);
            return definition != null && definition.TryGetFloat(out var value) ? value : fallback;
        }

        private static float Positive(float value, float fallback) => IsFinite(value) && value > 0f ? value : fallback;
        private static float Percent(float value) => IsFinite(value) ? Mathf.Clamp(value, 0f, 100f) : 0f;
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
