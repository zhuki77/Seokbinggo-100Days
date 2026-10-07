using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Nyangbingo.Core;

namespace Nyangbingo.Data
{
    // Read the imported catalog; do not maintain a second material-to-source table.
    public static class MaterialSourceGuide
    {
        public static string Describe(GameDataCatalog catalog, string itemId)
        {
            if (catalog == null || string.IsNullOrEmpty(itemId)) return "획득처를 확인할 수 없어요.";
            var lines = new List<string>();
            foreach (var mineral in catalog.MineralTiers.Where(m => m != null && m.Resource?.Id == itemId))
                lines.Add($"채집 · {LayerLabel(mineral.Layer)} · 깊이 {mineral.MinimumDepth}~{mineral.MaximumDepth}칸\n" +
                          $"필요 발톱 T{Math.Max(mineral.MinimumClawTier, mineral.Hardness)} (경도 {mineral.Hardness})");

            foreach (var yokai in catalog.Yokai.Where(y => y != null))
            {
                var habitat = HabitatLabel(yokai.SpawnTracks);
                if (yokai.SignatureItem?.Id == itemId && yokai.SignatureChance > 0f)
                    lines.Add($"요괴 드롭 · {yokai.DisplayName} · {habitat}\n" +
                              ChanceLabel(yokai.SignatureChance) +
                              (yokai.SignatureCondition == YokaiSignatureCondition.StealSuccess
                                  ? " · 절도 성공 후 처치 시" : string.Empty));
                if ((yokai.TearItem?.Id == itemId && yokai.TearDrop > 0) ||
                    yokai.Drops.Any(d => d.item?.Id == itemId && d.amount > 0))
                    lines.Add($"요괴 드롭 · {yokai.DisplayName} · {habitat} · 확정");
            }
            foreach (var boss in catalog.Bosses.Where(b => b != null &&
                         b.GuaranteedDrops.Any(d => d.item?.Id == itemId && d.amount > 0)))
                lines.Add($"보스 드롭 · {boss.DisplayName} · 격파 시 확정");

            foreach (var recipe in catalog.Recipes.Where(r => r != null && r.Output.item?.Id == itemId &&
                         r.MvpScope != ItemMvpScope.B))
                lines.Add($"제작 · {StationLabel(recipe.Station)}\n재료: " +
                          string.Join(" · ", (recipe.Ingredients ?? Array.Empty<ItemAmount>()).Select(AmountLabel)));
            foreach (var smelting in catalog.Smelting.Where(s => s != null && s.Output.item?.Id == itemId))
                lines.Add($"제련 · {(smelting.StationKind == SmeltingStationKind.Foundry ? "용광로" : "화로")}\n" +
                          $"재료: {AmountLabel(smelting.Input)} · 연료: {AmountLabel(smelting.Fuel)}");

            var terrains = catalog.TerrainSpawns.Where(t => t != null && t.Implemented &&
                    t.TerrainResourceIds.Contains(itemId)).Select(t => t.TerrainDisplayName)
                .Where(n => !string.IsNullOrEmpty(n)).Distinct().ToArray();
            var crops = catalog.Crops.Where(c => c != null && c.CropId == itemId && c.SpawnPerHundredTiles > 0).ToArray();
            foreach (var crop in crops)
            {
                var zone = catalog.FindZone(crop.ZoneId);
                lines.Add($"작물 채집 · 밴드 {crop.Order}" + (zone == null ? string.Empty :
                              $" · 거점에서 {zone.DistanceTilesFrom}~{zone.DistanceTilesTo}칸") +
                          (terrains.Length == 0 ? string.Empty : $"\n지형: {string.Join(" · ", terrains)}"));
            }
            if (crops.Length == 0 && terrains.Length > 0)
                lines.Add($"채집 지형 · {string.Join(" · ", terrains)}");
            return lines.Count == 0 ? "현재 데이터에 획득처가 등록되어 있지 않아요." :
                string.Join("\n\n", lines.Distinct());
        }

        public static string ChanceLabel(float chance) => chance >= 1f ? "확정" :
            $"기본 확률 {(chance * 100f).ToString("0.#", CultureInfo.InvariantCulture)}% · 매번 나오지 않음";

        private static string AmountLabel(ItemAmount amount) =>
            amount.item == null ? "—" : $"{amount.item.DisplayName} ×{amount.amount}";

        public static string StationLabel(CraftingStation station) => station switch
        {
            CraftingStation.None => "손 제작", CraftingStation.Workbench => "작업대",
            CraftingStation.Furnace => "화로", CraftingStation.IceAnvil => "얼음 모루",
            CraftingStation.Foundry => "용광로", CraftingStation.Smithy => "대장간",
            _ => station.ToString()
        };

        private static string HabitatLabel(YokaiSpawnTrack tracks) =>
            (tracks & (YokaiSpawnTrack.Raid | YokaiSpawnTrack.Resident)) ==
            (YokaiSpawnTrack.Raid | YokaiSpawnTrack.Resident) ? "밤 / 상주" :
            (tracks & YokaiSpawnTrack.Resident) != 0 ? "상주" :
            (tracks & YokaiSpawnTrack.Raid) != 0 ? "밤" : "등장 조건 확인 필요";

        private static string LayerLabel(MineralLayer layer) => layer switch
        {
            MineralLayer.SurfaceNight => "지상(밤)", MineralLayer.SurfaceRuinNight => "지상 폐허(밤)",
            MineralLayer.UndergroundUpper => "지하 상층", MineralLayer.UndergroundMiddle => "지하 중층",
            MineralLayer.UndergroundDeep => "지하 심층", _ => layer.ToString()
        };
    }
}
