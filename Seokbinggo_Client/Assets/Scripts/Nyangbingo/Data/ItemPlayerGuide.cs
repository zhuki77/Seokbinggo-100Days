using System.Linq;
using Nyangbingo.Core;

namespace Nyangbingo.Data
{
    /// <summary>Player-facing copy based on 냥빙고_아이템_분류표_20261013.xlsx; never displays raw design notes.</summary>
    public static class ItemPlayerGuide
    {
        public static string Describe(ItemDefinition item) => item?.Id switch
        {
            "workbench" => "기본 도구와 건축물을 만드는 첫 제작대입니다.",
            "furnace" => "광석을 주괴로 제련하고 무쇠 장비를 만들 수 있습니다.",
            "blast_furnace" => "얼음강철 원석을 주괴로 제련하는 시설입니다.",
            "ice_anvil" => "얼음강철 장비와 냉기 설비, 부적을 만드는 제작대입니다.",
            "smithy" => "더 강력한 장비와 방어 설비를 만드는 제작대입니다.",
            "insul_wall" => "기지를 둘러싸는 기본 벽입니다. 지붕과 닫힌 문을 함께 설치하면 밀폐 공간을 만들 수 있습니다.",
            "door" => "밀폐 공간의 출입문입니다. 출입한 뒤에는 문을 닫아 냉기가 새지 않게 하세요.",
            "roof" => "기지 위쪽을 막는 지붕입니다. 벽과 문을 연결해 밀폐 공간을 완성하세요.",
            "iron_insul_wall" => "차열벽보다 튼튼하며 불가사리가 갉아먹지 못하는 벽입니다.",
            "wallpaper" => "밀폐 공간의 배경에 바르는 벽지입니다. 구역 전체를 바르면 냉기원이 더 오래 유지됩니다.",
            "rope" => "벽에 매달아 오르내리는 로프입니다. W/S로 이동하고 A/D 또는 Space로 내려올 수 있습니다.",
            "jangdok" => "아이템을 보관하는 창고입니다. 얼음을 넣고 냉기를 유지해 보관물을 지키세요. 부서지면 안의 아이템이 떨어집니다.",
            "ice_core" => "주변을 차갑게 만드는 냉기 거점입니다. 잠자는 방과는 분리해 너무 추워지지 않게 하세요.",
            "cold_device" => "요괴 눈물로 만드는 냉기 설비입니다. 기지의 더위를 식히는 데 사용합니다.",
            "ice_crystal_cooler" => "기지를 식히는 냉각 설비입니다. 제작하려면 강철이가 남기는 여의주가 필요합니다.",
            "cold_wave_core" => "맵 전체의 폭염 단계를 한 단계 낮춥니다. 맵에 하나만 설치할 수 있습니다.",
            "water_jar" => "180초 동안 냉기를 내는 일회용 냉기원입니다.",
            "ice_jar" => "연료를 넣어 계속 사용할 수 있는 냉기원입니다.",
            "straw_insul" => "벽에 붙이는 단열재입니다. 체온을 더 잘 식힐 수 있도록 방을 꾸밀 때 사용하세요.",
            "clay_plaster" => "차열벽에 발라 벽을 더 튼튼하게 만듭니다.",
            "munpungji" => "단열 문에 붙여 문을 열었을 때 냉기가 새는 것을 막습니다.",
            "lantern" => "주변을 밝히고 어둑시니를 막는 설치형 등불입니다. 석탄을 연료로 사용합니다.",
            "sieve" => "주변의 야광귀를 멈춰 세워 아이템을 훔치지 못하게 합니다.",
            "haetae_statue" => "주변에서 받는 햇볕 화상 피해를 절반으로 줄여 줍니다.",
            "nest_bed" => "부활 위치를 지정하고 잠을 자서 낮과 밤을 넘길 수 있습니다. 너무 추운 곳에서는 잠들 수 없습니다.",
            "magpie_nest" => "까치 벗이 머무는 둥지입니다. 맵에 하나만 설치할 수 있습니다.",
            "bell_rope" => "주변에 요괴가 접근하면 방울을 울려 알려 줍니다.",
            "iron_bell_rope" => "요괴의 접근을 알리고 들어온 요괴를 한 번 느리게 만듭니다.",
            "iron_sieve" => "체를 강화한 방어 설비입니다. 야광귀의 절도를 막는 데 사용합니다.",
            "frost_lantern" => "등불을 강화한 조명입니다. 석탄 대신 서리정수를 연료로 사용합니다.",
            "jukbuin" => "보금자리 가까이에 두면 자연 체력 회복이 빨라집니다.",
            "daebal" => "주변에서 받는 낮의 햇볕 화상 피해를 줄여 줍니다.",
            "dokkaebi_fire_tower" => "가까운 적을 따라가는 불을 쏘아 기지를 지키는 자동 방어 설비입니다.",
            "tal_waypoint" => "설치한 지점과 석빙고 사이를 오갈 수 있게 해 주는 부적입니다.",
            "minhwa_scroll" => "기지를 꾸미는 민화 족자입니다. 별도의 능력 효과는 없습니다.",
            "saekdong_lantern" => "기지를 꾸미는 등롱입니다. 일반 등불과 달리 주변을 밝히지는 않습니다.",
            "onggi_pot" => "기지를 꾸미는 화분입니다. 별도의 능력 효과는 없습니다.",
            "wind_chime" => "기지를 꾸미는 풍경입니다. 별도의 능력 효과는 없습니다.",
            "saekdong_cushion" => "보금자리의 겉모습을 바꾸는 방석입니다. 별도의 능력 효과는 없습니다.",
            "frost_bell_rope" => "요괴의 접근을 알리고 들어온 요괴에게 서리 효과를 줍니다.",
            "singijeon_cart" => "적을 관통하는 화살을 일직선으로 쏘는 자동 방어 설비입니다.",
            "seonge_tower" => "공격에 맞은 적을 점점 느리게 만드는 방어 설비입니다.",
            "cold_wave_tower" => "적을 따라가는 불을 빠르게 쏘는 방어 설비입니다.",
            "ice_root_battery" => "냉기 도관에 연결하면 연료 없이 작동하는 방어 설비입니다.",
            "cold_wave_battery" => "부채꼴 범위의 적을 공격하는 방어 설비입니다.",
            "scarecrow" => "잠시 요괴의 공격을 대신 끌어당기는 미끼입니다. 적을 공격하지는 않습니다.",
            "ice_trap" => "밟은 요괴를 잠깐 멈춰 세우는 함정입니다.",
            "arrow_supply" => "활 계열 무기에 사용할 탄약을 자동으로 보급합니다.",
            "plaster_doll" => "근처의 손상된 벽을 자동으로 수리합니다. 적을 공격하지는 않습니다.",
            "gong_tower" => "징 소리로 요괴를 원하는 곳으로 유인합니다. 적을 공격하지는 않습니다.",
            "ice_shard" => "사용하면 올라간 체온을 즉시 식혀 줍니다. 얼음 항아리와 한기부를 만드는 데도 사용합니다.",
            "catnip" => "사용하면 체력을 25 회복합니다. 체력이 줄었을 때 응급 회복용으로 챙겨 두세요.",
            "oyster_mushroom" => "먹으면 체력을 15 회복합니다. 차갑게 보관하면 더 오래 보관할 수 있습니다.",
            "shiitake" => "먹으면 체력을 30 회복합니다. 차갑게 보관하면 더 오래 보관할 수 있습니다.",
            "seogi" => "먹으면 체력을 50 회복하는 귀한 버섯입니다. 차갑게 보관해 두세요.",
            "tal_return" => "사용하면 석빙고 코어로 즉시 귀환합니다.",
            "tal_stride" => "사용하면 60초 동안 이동 속도가 50% 빨라집니다.",
            "tal_hide" => "사용하면 30초 동안 요괴가 플레이어를 인식하지 못합니다.",
            "tal_frost" => "사용하면 120초 동안 저체온으로 인한 체온 하강을 막습니다.",
            "ssireum_satba" => "사용하면 도깨비 대장을 불러냅니다. 전투를 준비한 뒤 사용하세요.",
            "ice_altar_offering" => "강철이를 한 번 쓰러뜨린 뒤 다시 불러내는 데 사용합니다.",
            "iron_bait_pile" => "사용하면 어미 불가사리를 불러냅니다. 전투를 준비한 뒤 사용하세요.",
            "jigwi_summon" => "사용하면 지귀를 불러냅니다. 전투를 준비한 뒤 사용하세요.",
            "samdugumi_summon" => "사용하면 삼두구미를 불러냅니다. 전투를 준비한 뒤 사용하세요.",
            "eop_summon" => "사용하면 업구렁이를 불러냅니다. 전투를 준비한 뒤 사용하세요.",
            "wood" => "작업대, 등불, 로프 등 다양한 도구와 건축물을 만드는 기본 재료입니다.",
            "hemp_stalk" => "부적, 짚 장비와 단열재를 만드는 데 사용하는 재료입니다.",
            "rebar" => "폐허에서 얻는 건축 재료입니다. 차열 지붕을 만드는 데 사용합니다.",
            "dirt" => "작업대와 초반 건축물을 만드는 데 사용하는 기본 재료입니다.",
            "stone" => "작업대와 제련 시설을 만드는 기본 재료입니다. 짚 투석기의 탄약으로도 사용합니다.",
            "coal" => "등불을 밝히거나 화로와 용광로에서 광석을 녹이는 연료입니다.",
            "clay" => "용광로와 점토 미장을 만드는 데 사용하는 재료입니다.",
            "iron_ore" => "화로에서 녹이면 철 주괴를 얻을 수 있습니다.",
            "copper_ore" => "화로에서 녹이면 구리 주괴를 얻을 수 있습니다.",
            "icesteel_ore" => "용광로에서 녹이면 얼음강철 주괴를 얻을 수 있습니다. 채굴하려면 무쇠 발톱이 필요합니다.",
            "frost_essence" => "서리 등불의 연료이며 한기부와 이정표 부적을 만드는 귀한 재료입니다.",
            "iron_ingot" => "무쇠 장비와 무쇠 발톱, 얼음 모루를 만드는 데 사용하는 금속입니다.",
            "copper_ingot" => "방울 금줄과 얼음 모루 등을 만드는 데 사용하는 금속입니다.",
            "icesteel_ingot" => "얼음강철 장비와 얼음 모루를 만드는 데 사용하는 금속입니다.",
            "yokai_tear" => "요괴가 남기는 눈물입니다. 냉기 장치, 부적과 소환물을 만드는 데 사용합니다.",
            "club_shard" => "도깨비 방망이를 만드는 재료입니다. 조각 세 개를 모아 보세요.",
            "iron_scale" => "무쇠 차열벽을 만드는 튼튼한 재료입니다.",
            "stolen_bundle" => "야광귀가 훔쳐 간 물건을 담은 보따리입니다. 훔친 물건과 추가 보상을 돌려받을 수 있습니다.",
            "shadow_shard" => "어둑시니가 남기는 재료입니다.",
            "gangcheol_scale" => "서리발톱 장갑을 만드는 데 필요한 단단한 비늘입니다.",
            "dokkaebi_fire_essence" => "도깨비불 등탑을 만드는 데 필요한 재료입니다.",
            "yeouiju" => "빙정 냉각로를 만드는 데 필요한 귀한 재료입니다.",
            "drought_heart" => "맵의 더위를 낮추는 한파 코어를 만드는 데 필요한 재료입니다.",
            "seonge_ore" => "성에 주괴를 만드는 데 사용하는 광석입니다.",
            "ice_root" => "얼음뿌리 다발을 만드는 데 사용하는 재료입니다.",
            "cold_wave_ore" => "한파석 주괴를 만드는 데 사용하는 광석입니다.",
            "seonge_ingot" => "성에 장비와 방어 설비를 만드는 데 사용하는 금속입니다.",
            "ice_root_bundle" => "얼음뿌리 장비와 도관 포대를 만드는 데 사용하는 재료입니다.",
            "cold_wave_ingot" => "한파 장비와 방어 설비를 만드는 데 사용하는 금속입니다.",
            "iron_forge_core" => "신기전 화차를 만드는 데 필요한 재료입니다.",
            "jigwi_ember_mat" => "성에 등탑을 만드는 데 필요한 재료입니다.",
            "blaze_yeokrin" => "한파 코어를 강화하는 데 필요한 재료입니다.",
            "sangun_talon" => "산군 발톱을 만드는 데 필요한 재료입니다.",
            "three_horn_mat" => "도관 포대를 만드는 데 필요한 재료입니다.",
            "eop_scale_mat" => "한파 등탑과 한파 코어를 강화하는 데 필요한 재료입니다.",
            "yeongno_mask_mat" => "한파 신기전을 만드는 데 필요한 재료입니다.",
            "perfect_heart" => "완전체 발톱을 만드는 데 필요한 재료입니다.",
            _ => "제작이나 모험에 사용할 수 있는 아이템입니다."
        };

        public static string Acquisition(GameDataCatalog catalog, string id)
        {
            var recipes = catalog.Recipes.Where(r => r != null && r.Output.item?.Id == id).ToArray();
            var stations = recipes.Where(r => r.MvpScope != ItemMvpScope.B)
                .Select(r => MaterialSourceGuide.StationLabel(r.Station)).Distinct().ToArray();
            var smelters = catalog.Smelting.Where(s => s != null && s.Output.item?.Id == id)
                .Select(s => s.StationKind == SmeltingStationKind.Foundry ? "용광로" : "화로").Distinct().ToArray();
            var lines = new System.Collections.Generic.List<string>();
            if (stations.Length > 0) lines.Add("제작 가능: " + string.Join(" · ", stations));
            if (smelters.Length > 0) lines.Add("제련 가능: " + string.Join(" · ", smelters));
            if (lines.Count > 0) return string.Join("\n", lines);
            if (recipes.Length > 0) return "현재 제작할 수 없습니다.";
            return id switch
            {
                "wood" => "획득처: 지상 삼나무 채집",
                "hemp_stalk" => "획득처: 채집 혹은 지상 폐허의 상자",
                "rebar" => "획득처: 지상 폐허 채집 혹은 상자",
                "dirt" or "stone" => "획득처: 지하 상층",
                "coal" or "clay" => "획득처: 지하 상층 광맥 혹은 상자",
                "iron_ore" or "copper_ore" or "ice_shard" => "획득처: 지하 중층",
                "icesteel_ore" => "획득처: 지하 심층 (무쇠 발톱 필요)",
                "frost_essence" => "획득처: 지하 심층 (희귀)",
                "catnip" => "획득처: 캣닢 군락 · 지하 동굴 혹은 보물 상자",
                "oyster_mushroom" or "shiitake" => "획득처: 지하 동굴 채집",
                "seogi" => "획득처: 지하 동굴 채집 (희귀)",
                "yokai_tear" => "획득처: 요괴 처치",
                "club_shard" => "획득처: 방망이 도깨비 처치 (확률 드롭)",
                "iron_scale" => "획득처: 불가사리 처치 (확률 드롭)",
                "stolen_bundle" => "획득처: 물건을 훔친 야광귀 처치",
                "shadow_shard" => "획득처: 어둑시니 처치 (확률 드롭)",
                "gangcheol_scale" or "drought_heart" => "획득처: 이무기 처치",
                "dokkaebi_fire_essence" => "획득처: 도깨비 대장 처치",
                "yeouiju" => "획득처: 강철이 처치",
                "iron_forge_core" => "획득처: 어미 불가사리 처치",
                "jigwi_ember_mat" => "획득처: 지귀 처치",
                "blaze_yeokrin" => "획득처: 불볕 강철이 처치",
                "sangun_talon" => "획득처: 산군 처치",
                "three_horn_mat" => "획득처: 삼두구미 처치",
                "eop_scale_mat" => "획득처: 업구렁이 처치",
                "yeongno_mask_mat" => "획득처: 영노 처치",
                "perfect_heart" => "획득처: 강철이 완전체 처치",
                "seonge_ore" or "ice_root" or "cold_wave_ore" => "획득처: 현재 없음",
                _ => "획득처: 미확인"
            };
        }
    }
}
