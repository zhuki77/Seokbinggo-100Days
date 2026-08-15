using Nyangbingo.Data;
using UnityEngine;

namespace Nyangbingo.UI
{
    /// <summary>
    /// B-UI-v71 B-2: 침공 일정(period/offset)과 예고·침대 잠금 판정.
    /// invasion_period_days=10 · invasion_offset_days=6 → 6·16·26…96일 밤 침공.
    /// </summary>
    public static class InvasionScheduleRules
    {
        public const int DefaultPeriodDays = 10;
        public const int DefaultOffsetDays = 6;
        public const int MaxScheduledInvasionDay = 96;
        public const string AnnouncementBannerText = "오늘 밤 침공 · 준비하세요";
        public const string BedLockedMessage = "침공 밤에는 잠을 잘 수 없습니다";

        public static bool IsInvasionNight(int day, int periodDays = DefaultPeriodDays,
            int offsetDays = DefaultOffsetDays)
        {
            if (day < offsetDays || day > MaxScheduledInvasionDay || periodDays <= 0) return false;
            var mod = day % periodDays;
            if (mod < 0) mod += periodDays;
            return mod == offsetDays % periodDays;
        }

        /// <summary>침공 전날 낮에 예고 배너를 띄운다.</summary>
        public static bool ShouldShowAnnouncement(int day, bool isNight, bool announceEnabled,
            int periodDays = DefaultPeriodDays, int offsetDays = DefaultOffsetDays)
        {
            if (!announceEnabled || isNight || day < 1) return false;
            return IsInvasionNight(day + 1, periodDays, offsetDays);
        }

        /// <summary>예고된 침공 밤에는 침대(낮잠/스킵)를 막는다.</summary>
        public static bool IsBedLocked(int day, bool isNight, bool bedLockEnabled,
            int periodDays = DefaultPeriodDays, int offsetDays = DefaultOffsetDays)
        {
            if (!bedLockEnabled || !isNight) return false;
            return IsInvasionNight(day, periodDays, offsetDays);
        }

        public static int ReadPeriod(GameDataCatalog catalog, int fallback = DefaultPeriodDays) =>
            ReadInt(catalog, "invasion_period_days", fallback);

        public static int ReadOffset(GameDataCatalog catalog, int fallback = DefaultOffsetDays) =>
            ReadInt(catalog, "invasion_offset_days", fallback);

        public static bool ReadAnnounceEnabled(GameDataCatalog catalog) =>
            ReadInt(catalog, "invasion_announce", 1) != 0;

        public static bool ReadBedLockEnabled(GameDataCatalog catalog) =>
            ReadInt(catalog, "bed_locked_on_invasion", 1) != 0;

        private static int ReadInt(GameDataCatalog catalog, string key, int fallback)
        {
            var definition = catalog?.FindGlobal(key);
            if (definition != null && definition.TryGetInt(out var value) && value > 0) return value;
            return Mathf.Max(1, fallback);
        }
    }
}
