using System;
using System.Collections.Generic;
using Nyangbingo.Data;

namespace Nyangbingo.World
{
    public sealed class SealBoundaryPolicy
    {
        public const string NaturalTerrainElement = "자연 지형(흙·돌·암반)";
        public const string AirElement = "채굴 갱도(공기 칸)";

        private static readonly Dictionary<string, string> RuntimeElementRules =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "insul_wall", "차열벽" },
                { "iron_insul_wall", "무쇠 차열벽" },
                { "roof", "차열 지붕" },
                { "door", "단열 문" },
                { "ice_core", "얼음 저장고" },
                { "jangdok", "장독 창고" },
                { "workbench", "화로/용광로/얼음 모루/작업대" },
                { "furnace", "화로/용광로/얼음 모루/작업대" },
                { "blast_furnace", "화로/용광로/얼음 모루/작업대" },
                { "ice_anvil", "화로/용광로/얼음 모루/작업대" },
                { "minhwa_scroll", "민화 족자" },
                { "saekdong_lantern", "색동 등롱" },
                { "onggi_pot", "옹기 화분" },
                { "wind_chime", "풍경" },
                { "saekdong_cushion", "색동 방석" },
                { "nest_bed", "보금자리" },
                { "magpie_nest", "까치 둥지" },
                { "dokkaebi_fire_tower", "도깨비불 등탑/신기전 화차" },
                { "singijeon_cart", "도깨비불 등탑/신기전 화차" },
                { "haetae_statue", "해태 석상/체/등불" },
                { "sieve", "해태 석상/체/등불" },
                { "lantern", "해태 석상/체/등불" },
                { "ice_crystal_cooler", "빙정 냉각로/한파 코어/냉기 장치" },
                { "cold_wave_core", "빙정 냉각로/한파 코어/냉기 장치" },
                { "cold_device", "빙정 냉각로/한파 코어/냉기 장치" }
            };

        private readonly Dictionary<string, bool> sealsByElement;

        public SealBoundaryPolicy(IReadOnlyList<SealWhitelistDefinition> definitions)
        {
            sealsByElement = new Dictionary<string, bool>(StringComparer.Ordinal);
            IsValid = definitions != null && definitions.Count > 0;
            if (definitions == null) return;

            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.Element) ||
                    !sealsByElement.TryAdd(definition.Element, definition.Seals))
                    IsValid = false;
            }
        }

        public bool IsValid { get; }

        public bool Seals(TileData tile)
        {
            if (!IsValid) return tile.isNaturalTerrain;
            if (tile.IsAir) return Resolve(AirElement);
            if (tile.isNaturalTerrain) return Resolve(NaturalTerrainElement);

            // Attachments modify a base wall instead of sealing as standalone tiles.
            // 단열 문은 타일·설치물 모두 BarrierActive(닫힘)일 때만 밀폐한다.
            // 전경 door/door_top 타일 자체는 여기서 밀폐하지 않고 ISealBarrierRegistry에 맡긴다.
            if (string.Equals(tile.elementType, "door", StringComparison.Ordinal) ||
                string.Equals(tile.elementType, "door_top", StringComparison.Ordinal)) return false;
            return SealsPlacedElement(tile.elementType);
        }

        /// <summary>설치물 definitionId가 공식 차열 경계로 인정되는지 조회한다.</summary>
        public bool SealsPlacedElement(string definitionId) =>
            !string.IsNullOrEmpty(definitionId) &&
            RuntimeElementRules.TryGetValue(definitionId, out var officialElement) &&
            Resolve(officialElement);

        private bool Resolve(string element) =>
            sealsByElement.TryGetValue(element, out var seals) && seals;
    }
}
