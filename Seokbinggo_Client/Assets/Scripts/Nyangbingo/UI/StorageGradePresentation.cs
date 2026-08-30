using Nyangbingo.Data;
using UnityEngine;

namespace Nyangbingo.UI
{
    /// <summary>
    /// B-UI-v71 B-3: 보관 컨테이너 실온 등급·슬롯 경고·상함/녹음 툴팁.
    /// A-12 정식 태그 전까지 아이템 id/카테고리 휴리스틱을 쓰고, globals가 있으면 임계·비율을 읽는다.
    /// </summary>
    public static class StorageGradePresentation
    {
        public const int DefaultChilledEnter = -5;
        public const int DefaultFrozenEnter = -10;
        public const float DefaultSpoilPerDay = .10f;
        public const float DefaultMeltPerDay = .25f;

        public enum RequiredBand
        {
            None,
            Chilled,
            Frozen
        }

        public static string BandLabel(RoomTempPresentation.Band band)
        {
            switch (band)
            {
                case RoomTempPresentation.Band.Chilled: return "냉장";
                case RoomTempPresentation.Band.Frozen: return "빙결";
                default: return "상온";
            }
        }

        public static string FormatContainerHeader(int celsius, RoomTempPresentation.Band band,
            string containerTitle) =>
            $"{containerTitle} · {RoomTempPresentation.FormatCelsius(celsius)} · {BandLabel(band)}";

        public static RequiredBand ResolveRequiredBand(ItemDefinition item)
        {
            if (item == null || string.IsNullOrEmpty(item.Id)) return RequiredBand.None;
            var id = item.Id;
            if (IsIceLike(id)) return RequiredBand.Frozen;
            if (IsCropOrMushroom(id)) return RequiredBand.Chilled;
            return RequiredBand.None;
        }

        public static bool IsBelowRequired(RoomTempPresentation.Band current, RequiredBand required)
        {
            if (required == RequiredBand.None) return false;
            if (required == RequiredBand.Frozen)
                return current != RoomTempPresentation.Band.Frozen;
            return current == RoomTempPresentation.Band.Warm;
        }

        public static string WarningGlyph => "!";

        public static string BuildSlotRiskTooltip(ItemDefinition item, RoomTempPresentation.Band current,
            float spoilPerDay = DefaultSpoilPerDay, float meltPerDay = DefaultMeltPerDay)
        {
            var required = ResolveRequiredBand(item);
            if (required == RequiredBand.None || !IsBelowRequired(current, required))
                return string.Empty;
            if (required == RequiredBand.Frozen)
                return $"하루 {Mathf.RoundToInt(meltPerDay * 100f)}% 녹음";
            return $"하루 {Mathf.RoundToInt(spoilPerDay * 100f)}% 상함";
        }

        public static float ReadSpoilPerDay(GameDataCatalog catalog) =>
            ReadRatio(catalog, "storage_spoil_per_day", DefaultSpoilPerDay);

        public static float ReadMeltPerDay(GameDataCatalog catalog) =>
            ReadRatio(catalog, "storage_melt_per_day", DefaultMeltPerDay);

        public static int ReadChilledEnter(GameDataCatalog catalog) =>
            ReadInt(catalog, "storage_band_chilled", DefaultChilledEnter);

        public static int ReadFrozenEnter(GameDataCatalog catalog) =>
            ReadInt(catalog, "storage_band_frozen", DefaultFrozenEnter);

        private static bool IsIceLike(string id) =>
            id == "ice_shard" ||
            id == "frost_essence" ||
            id == "icesteel_ore" ||
            id.StartsWith("ice_", System.StringComparison.Ordinal);

        private static bool IsCropOrMushroom(string id) =>
            id == "hemp_stalk" ||
            id == "catnip" ||
            id.Contains("mushroom") ||
            id.Contains("crop") ||
            id.EndsWith("_crop", System.StringComparison.Ordinal);

        private static float ReadRatio(GameDataCatalog catalog, string key, float fallback)
        {
            var definition = catalog?.FindGlobal(key);
            if (definition != null && definition.TryGetFloat(out var value) && value >= 0f && value <= 1f)
                return value;
            return fallback;
        }

        private static int ReadInt(GameDataCatalog catalog, string key, int fallback)
        {
            var definition = catalog?.FindGlobal(key);
            if (definition != null && definition.TryGetInt(out var value)) return value;
            return fallback;
        }
    }
}
