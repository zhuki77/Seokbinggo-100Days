using System;
using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using Nyangbingo.UI;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>
    /// v72 A-10 침공 상태. 파도와 무관하며, 코어에 실제 도달한 비보스 요괴만
    /// 눈물 수×0.5℃를 누적한다. 누적 열은 다음 낮부터 얼음 조각 1개/1℃로 제거한다.
    /// </summary>
    public sealed class InvasionService : IDisposable
    {
        public const string DefaultRecoolItemId = "ice_shard";

        private readonly DayNightService time;
        private readonly Inventory.Inventory inventory;
        private readonly int periodDays;
        private readonly int offsetDays;
        private readonly float risePerTear;
        private readonly string recoolItemId;
        private readonly int recoolItemsPerDegree;
        private bool disposed;

        public InvasionService(
            GameDataCatalog catalog, DayNightService timeService, Inventory.Inventory playerInventory)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            time = timeService ?? throw new ArgumentNullException(nameof(timeService));
            inventory = playerInventory ?? throw new ArgumentNullException(nameof(playerInventory));
            periodDays = ReadPositiveInt(catalog, GlobalKeys.InvasionPeriodDays,
                InvasionScheduleRules.DefaultPeriodDays);
            offsetDays = ReadPositiveInt(catalog, GlobalKeys.InvasionOffsetDays,
                InvasionScheduleRules.DefaultOffsetDays);
            risePerTear = ReadPositiveFloat(catalog, GlobalKeys.InvasionTemperatureRisePerTear, .5f);
            if (!TryParseRecoolCost(catalog.FindGlobal(GlobalKeys.RecoolCost)?.Value,
                    out recoolItemId, out recoolItemsPerDegree))
                throw new InvalidOperationException("recool_cost는 item_id:positive_int 형식이어야 합니다.");
            if (!string.Equals(catalog.FindGlobal(GlobalKeys.InvasionTemperatureRiseSource)?.Value,
                    "yokai-stats.csv:tears", StringComparison.Ordinal) ||
                !string.Equals(catalog.FindGlobal(GlobalKeys.RecoolWhen)?.Value,
                    "next_day", StringComparison.Ordinal) ||
                !string.Equals(catalog.FindGlobal(GlobalKeys.RecoolStation)?.Value,
                    "ice_core", StringComparison.Ordinal))
                throw new InvalidOperationException("v72 침공 온도/재냉각 단일 출처 globals가 잘못되었습니다.");
            GameEvents.OnDayStart += HandleDayStart;
        }

        public float TemperatureRiseCelsius { get; private set; }
        public int RecoolAvailableDay { get; private set; }
        public int LastInfiltrationDay { get; private set; }
        public bool HasPendingRecool => TemperatureRiseCelsius > .0001f;
        public bool CanRecoolNow => HasPendingRecool && !time.IsNight &&
                                     RecoolAvailableDay > 0 && time.Day >= RecoolAvailableDay;
        public bool IsCurrentInvasionNight => time.IsNight &&
            InvasionScheduleRules.IsInvasionNight(time.Day, periodDays, offsetDays);

        public bool RecordInfiltration(YokaiDefinition definition)
        {
            if (definition == null || definition.UsesArenaBody || !IsCurrentInvasionNight ||
                definition.TearDrop <= 0)
                return false;
            var added = definition.TearDrop * risePerTear;
            if (added <= 0f || float.IsNaN(added) || float.IsInfinity(added)) return false;
            TemperatureRiseCelsius += added;
            LastInfiltrationDay = time.Day;
            RecoolAvailableDay = Mathf.Max(RecoolAvailableDay, time.Day + 1);
            return true;
        }

        public bool TryRecool(out int itemsSpent, out float cooledDegrees, out string reason)
        {
            itemsSpent = 0;
            cooledDegrees = 0f;
            if (!HasPendingRecool)
            {
                reason = "복구할 침공 온도 상승이 없습니다.";
                return false;
            }
            if (!CanRecoolNow)
            {
                reason = $"재냉각은 {RecoolAvailableDay}일차 낮부터 가능합니다.";
                return false;
            }

            var degrees = Mathf.CeilToInt(TemperatureRiseCelsius);
            var required = checked(degrees * recoolItemsPerDegree);
            if (!inventory.TryRemove(recoolItemId, required))
            {
                reason = $"재냉각 재료 부족: {recoolItemId} ×{required}";
                return false;
            }

            itemsSpent = required;
            cooledDegrees = TemperatureRiseCelsius;
            TemperatureRiseCelsius = 0f;
            RecoolAvailableDay = 0;
            reason = $"얼음 조각 {required}개로 침공 열기 {cooledDegrees:0.#}℃를 복구했습니다.";
            return true;
        }

        public bool Restore(float temperatureRise, int recoolAvailableDay, int lastInfiltrationDay)
        {
            if (temperatureRise < 0f || float.IsNaN(temperatureRise) ||
                float.IsInfinity(temperatureRise) || recoolAvailableDay < 0 ||
                lastInfiltrationDay < 0)
                return false;
            TemperatureRiseCelsius = temperatureRise;
            RecoolAvailableDay = temperatureRise > .0001f ? recoolAvailableDay : 0;
            LastInfiltrationDay = lastInfiltrationDay;
            return !HasPendingRecool || RecoolAvailableDay > 0;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            GameEvents.OnDayStart -= HandleDayStart;
        }

        private void HandleDayStart()
        {
            if (HasPendingRecool && RecoolAvailableDay <= 0)
                RecoolAvailableDay = Mathf.Max(1, LastInfiltrationDay + 1);
        }

        public static bool TryParseRecoolCost(string raw, out string itemId, out int amount)
        {
            itemId = string.Empty;
            amount = 0;
            if (string.IsNullOrWhiteSpace(raw)) return false;
            var split = raw.Split(':');
            if (split.Length != 2 || string.IsNullOrWhiteSpace(split[0]) ||
                !int.TryParse(split[1], out amount) || amount <= 0)
                return false;
            itemId = split[0].Trim();
            return true;
        }

        private static int ReadPositiveInt(GameDataCatalog catalog, string key, int fallback)
        {
            var definition = catalog.FindGlobal(key);
            return definition != null && definition.TryGetInt(out var value) && value > 0
                ? value
                : fallback;
        }

        private static float ReadPositiveFloat(GameDataCatalog catalog, string key, float fallback)
        {
            var definition = catalog.FindGlobal(key);
            return definition != null && definition.TryGetFloat(out var value) && value > 0f
                ? value
                : fallback;
        }
    }
}
