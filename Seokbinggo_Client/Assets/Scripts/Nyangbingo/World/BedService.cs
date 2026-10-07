using System;
using Nyangbingo.Data;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>v72 A-11. 기존 보금자리의 온난 취침·양방향 낮밤 스킵 규칙.</summary>
    public sealed class BedService
    {
        public const string DefaultBedItemId = "nest_bed";

        private readonly DayNightService time;
        private readonly RoomTempService roomTemperature;
        private readonly InvasionService invasion;
        private readonly DayEventDefinition baekjung;
        private readonly float minimumSleepTemperature;
        private readonly Func<bool> naturalRecoveryReady;

        public BedService(GameDataCatalog catalog, DayNightService timeService,
            RoomTempService roomTempService, InvasionService invasionService,
            Func<bool> recoveryReady = null)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            time = timeService ?? throw new ArgumentNullException(nameof(timeService));
            roomTemperature = roomTempService ?? throw new ArgumentNullException(nameof(roomTempService));
            invasion = invasionService ?? throw new ArgumentNullException(nameof(invasionService));
            baekjung = catalog.FindDayEvent("baekjung");
            naturalRecoveryReady = recoveryReady;

            if (!ReadBool(catalog, GlobalKeys.BedEnabled) ||
                !ReadBool(catalog, GlobalKeys.BedLockedOnInvasion) ||
                !ReadBool(catalog, GlobalKeys.BedSkipAppliesDailyTick) ||
                !string.Equals(catalog.FindGlobal(GlobalKeys.BedItemId)?.Value,
                    DefaultBedItemId, StringComparison.Ordinal) ||
                !string.Equals(catalog.FindGlobal(GlobalKeys.BedSkipMode)?.Value,
                    "next_phase", StringComparison.Ordinal) ||
                catalog.FindGlobal(GlobalKeys.BedSleepRoomTempMax) is not { } temperature ||
                !temperature.TryGetFloat(out minimumSleepTemperature) ||
                float.IsNaN(minimumSleepTemperature) || float.IsInfinity(minimumSleepTemperature))
                throw new InvalidOperationException("v72 침대 globals가 올바르지 않습니다.");
        }

        public float MinimumSleepTemperature => minimumSleepTemperature;
        public event Action<float> Slept;
        public event Action<float> SleepDeniedCold;
        public void ReportDeniedSleep(float roomTemperature)
        {
            if (!CanSleepAtTemperature(roomTemperature, minimumSleepTemperature))
                SleepDeniedCold?.Invoke(minimumSleepTemperature);
        }

        public bool CanSleep(Vector2 bedPosition, out float roomTemperatureCelsius, out string reason)
        {
            roomTemperatureCelsius = roomTemperature.ResolveExact(bedPosition);
            if (!CanSleepAtTemperature(roomTemperatureCelsius, minimumSleepTemperature))
            {
                reason = $"침실이 너무 춥습니다 · 실온 {roomTemperatureCelsius:0.#}℃ / 필요 {minimumSleepTemperature:0.#}℃ 이상";
                return false;
            }
            if (invasion.IsCurrentInvasionNight)
            {
                reason = "예고된 요괴 침공 밤에는 잠들 수 없습니다.";
                return false;
            }
            // 침공과 동일하게 해당 밤 전체를 제한한다. 이어하기 직후에도 일정으로 판정한다.
            if (time.IsNight && baekjung != null && time.Day == baekjung.Day)
            {
                reason = "백중날 밤에는 잠들 수 없습니다.";
                return false;
            }
            if (naturalRecoveryReady != null && !naturalRecoveryReady())
            {
                reason = "피해를 받은 뒤에는 쉴 수 없습니다. 자연회복이 시작될 때까지 기다리세요.";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        public static bool CanSleepAtTemperature(float roomTemperatureCelsius,
            float minimumTemperature = -4f) =>
            !float.IsNaN(roomTemperatureCelsius) && !float.IsInfinity(roomTemperatureCelsius) &&
            !float.IsNaN(minimumTemperature) && !float.IsInfinity(minimumTemperature) &&
            roomTemperatureCelsius >= minimumTemperature;

        public bool TrySleep(Vector2 bedPosition, out string message)
        {
            if (!CanSleep(bedPosition, out var roomTemperatureCelsius, out message))
            {
                ReportDeniedSleep(roomTemperatureCelsius);
                return false;
            }
            var skippedNight = time.IsNight;
            if (!time.AdvanceToNextPhase())
            {
                message = "다음 낮밤 경계로 시간을 넘길 수 없습니다.";
                return false;
            }
            message = skippedNight
                ? $"보금자리 취침 · 실온 {roomTemperatureCelsius:0.#}℃ · {time.Day}일차 새벽"
                : $"보금자리 취침 · 실온 {roomTemperatureCelsius:0.#}℃ · {time.Day}일차 해질녘";
            Slept?.Invoke(roomTemperatureCelsius);
            return true;
        }

        private static bool ReadBool(GameDataCatalog catalog, string key) =>
            catalog.FindGlobal(key) is { } value && value.TryGetBool(out var parsed) && parsed;
    }
}
