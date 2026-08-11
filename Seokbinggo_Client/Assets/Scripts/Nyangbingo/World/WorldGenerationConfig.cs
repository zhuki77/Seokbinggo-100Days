using System;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>
    /// WorldGenerator의 모든 튜닝 값. 절대 MapGenerator 코드 안에 숫자를 박아넣지 않고 전부 이 인스펙터/SO를 통해 제어한다.
    /// 기본값은 GDD 정본(개발 가이드②, 4 시스템, 6-4/6-12 밸런스 표)의 v17 확정 수치를 그대로 반영한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Nyangbingo/World/World Generation Config", fileName = "WorldGenerationConfig")]
    public sealed class WorldGenerationConfig : ScriptableObject
    {
        [Header("맵 크기 (기본 600×160 = 가로 400의 1.5배)")]
        [Min(16)][SerializeField] private int mapWidth = 600;
        [Min(16)][SerializeField] private int mapHeight = 160;

        [Header("지형 — Pass 1 (1D 펄린)")]
        // A-10(v26 정본 교정): globals.csv surface_y=20(맵 상단 기준 하늘 두께), 맵 높이 160 →
        // 지표(최상단 고체 행) = 160 - 1 - 20 = 139 → 139/160 = 0.86875. 절대 임의 조정 금지(변경 금지 값).
        [Range(0f, 1f)][SerializeField] private float surfaceBaseHeightRatio = 0.86875f;
        // globals.csv: "1D 펄린 진폭 +-6" — surface_y=20과 함께 정본 확정값.
        [Min(0f)][SerializeField] private float surfaceNoiseAmplitude = 6f;
        [Min(0.0001f)][SerializeField] private float surfaceNoiseFrequency = 0.02f;
        // A-10: globals.csv layer_t1_depth=45(변경 금지) / layer_t2_depth=90 / layer_t3_depth=135 /
        // bedrock_depth=140 → 두께로 환산하면 45 / (90-45)=45 / (140-135)=5. 예전 40/55/4(v17)를
        // "45 / 45 / 45 / 5" 정본으로 교정한다. mineral-tiers.csv의 depth_min/depth_max(1~45/46~90/91~135)와
        // 반드시 같은 기준(지표에서 내려간 깊이)을 쓴다 — PlaceOreVeins가 이 값들과 별도 산식을 쓰면 안 된다.
        [Min(1)][SerializeField] private int upperLayerThickness = 45;
        [Min(1)][SerializeField] private int middleLayerThickness = 45;
        [Min(1)][SerializeField] private int bedrockThickness = 5;
        [Range(0f, 1f)][SerializeField] private float upperDirtRatio = 0.64f; // 흙45:돌25 ≈ 64:36 (6-4 표)
        // 지표(포함)부터 이 깊이까지는 석탄·돌·점토 등 T1 광물/돌 필을 넣지 않는다(지상 노출 금지).
        [Min(1)][SerializeField] private int surfaceMineralBanDepth = 3;

        [Header("동굴 — Pass 2 (2D 펄린 임계값, 상층10%→심층25%)")]
        [Min(0.0001f)][SerializeField] private float caveNoiseFrequency = 0.08f;
        [Range(0f, 1f)][SerializeField] private float caveChanceUpper = 0.10f;
        [Range(0f, 1f)][SerializeField] private float caveChanceDeep = 0.25f;
        // v27: 공동 하나(연결 성분)의 최대 세로 길이. globals.csv cave_max_height 정본(가안 12 —
        // 화면 세로 34타일의 약 1/3). 지표 관통 거대 수직 구멍 방지. 시작 입구·좁은 연결 통로는 Pass 4 이후
        // 별도 경로라 이 한도의 적용 대상이 아니다.
        [Min(1)][SerializeField] private int caveMaxHeight = 12;
        // v27 Top Safety Zone: 지표(surfaceY) 포함 직하단 몇 칸을 펄린 공동에서 제외(기본 2 = 지표+1칸).
        // 기획 surface_y(맵 상단 하늘 두께)와 별개로, 열별 표면 껍질을 절대 안전지대로 둔다.
        [Min(1)][SerializeField] private int caveSurfaceCrustThickness = 2;

        // 펄린 소형 공동과 별개 — PostProcess(Hard Cut) 이후, 중간층에만 띄엄띄엄 개척.
        [Header("동굴 — 중간층 공동 (시드, PostProcess 이후, 띄엄띄엄)")]
        [Min(0)][SerializeField] private int largeCavernCountMin = 5;
        [Min(0)][SerializeField] private int largeCavernCountMax = 8;
        [Min(4)][SerializeField] private int largeCavernWidth = 30;
        [Min(4)][SerializeField] private int largeCavernHeight = 20;
        // 공동 bounding box 사이 최소 가로 간격(가장자리 기준).
        [Min(0)][SerializeField] private int largeCavernMinEdgeGap = 40;
        // 공동 천장(타원 상단)이 crust 봉인선보다 최소 이만큼 더 깊게(중간층 배치의 보조 가드).
        [Min(0)][SerializeField] private int largeCavernMinDepthBelowCrust = 8;
        [Min(0)][SerializeField] private int largeCavernMarginFromSpawn = 32;
        [Min(0)][SerializeField] private int largeCavernMarginFromAltar = 28;
        [Min(1)][SerializeField] private int largeCavernMaxPlacementAttempts = 120;

        // A-10: depthMin/depthMax는 Assets/Data/CSV/mineral-tiers.csv의 depth_min/depth_max를 그대로 옮긴
        // 값이다(T1 1~45 · T2 46~90 · T3 91~135). CSV가 정본이므로 두 값이 어긋나면 이 배열을 CSV에 맞춰
        // 고친다(회귀 테스트가 CSV와의 불일치를 감지한다) — 반대로 CSV를 여기 맞춰 고치지 않는다.
        [Header("자원 — Pass 3 (클러스터 광맥, mineral-tiers.csv 빈도/깊이 정본)")]
        [SerializeField]
        private OreVeinProfile[] oreVeins =
        {
            // 상층 돌은 노이즈 필이 아니라 광맥 클러스터로만 배치(지표 ban depth 아래).
            new OreVeinProfile { elementType = WorldTileTypes.Stone, layer = WorldLayer.Upper, hardness = 1, frequencyPer100Tiles = 25f, minClusterSize = 4, maxClusterSize = 8, depthMin = 1, depthMax = 45 },
            new OreVeinProfile { elementType = WorldTileTypes.Coal, layer = WorldLayer.Upper, hardness = 1, frequencyPer100Tiles = 8f, minClusterSize = 3, maxClusterSize = 6, depthMin = 1, depthMax = 45 },
            new OreVeinProfile { elementType = WorldTileTypes.Clay, layer = WorldLayer.Upper, hardness = 1, frequencyPer100Tiles = 10f, minClusterSize = 3, maxClusterSize = 6, depthMin = 1, depthMax = 45 },
            new OreVeinProfile { elementType = WorldTileTypes.IronOre, layer = WorldLayer.Middle, hardness = 2, frequencyPer100Tiles = 18f, minClusterSize = 3, maxClusterSize = 6, depthMin = 46, depthMax = 90 },
            new OreVeinProfile { elementType = WorldTileTypes.CopperOre, layer = WorldLayer.Middle, hardness = 2, frequencyPer100Tiles = 12f, minClusterSize = 3, maxClusterSize = 6, depthMin = 46, depthMax = 90 },
            new OreVeinProfile { elementType = WorldTileTypes.IceShard, layer = WorldLayer.Middle, hardness = 2, frequencyPer100Tiles = 10f, minClusterSize = 3, maxClusterSize = 6, depthMin = 46, depthMax = 90 },
            new OreVeinProfile { elementType = WorldTileTypes.IceSteelOre, layer = WorldLayer.Deep, hardness = 3, frequencyPer100Tiles = 12f, minClusterSize = 3, maxClusterSize = 6, depthMin = 91, depthMax = 135 },
            new OreVeinProfile { elementType = WorldTileTypes.FrostEssence, layer = WorldLayer.Deep, hardness = 3, frequencyPer100Tiles = 4f, minClusterSize = 3, maxClusterSize = 6, depthMin = 91, depthMax = 135 }
        };

        [Header("구조물 — Pass 4 : 반지하 알코브(스폰)")]
        [Range(0f, 1f)][SerializeField] private float spawnColumnRatio = 0.5f;
        // v28: player_jump_height_tiles(3.5)보다 얕게 — 입구 낙하 후 점프 탈출 가능. 공기 칸 수(지표 포함).
        [Min(1)][SerializeField] private int spawnEntranceDepthTiles = 3;
        [Min(3)][SerializeField] private int alcoveWidth = 5;
        [Min(3)][SerializeField] private int alcoveHeight = 4;

        [Header("구조물 — Pass 4 : 지상 폐허")]
        [Min(0)][SerializeField] private int ruinCountMin = 2;
        [Min(0)][SerializeField] private int ruinCountMax = 3;
        [Min(1)][SerializeField] private int ruinWidth = 3;
        [Min(1)][SerializeField] private int ruinHeight = 2;

        [Header("구조물 — Pass 4 : 심층 얼음호수 + 이무기 제단")]
        [Range(0f, 1f)][SerializeField] private float deepAltarColumnRatio = 0.75f;
        [Min(2)][SerializeField] private int altarSize = 2;
        [Range(1, 3)][SerializeField] private int altarHardness = 3;
        [Min(2)][SerializeField] private int iceLakeWidth = 6;
        [Min(2)][SerializeField] private int iceLakeHeight = 3;

        [Header("구조물 — Pass 4 : 보물 상자 (대형 동굴당 0~2, 지상 배치 없음)")]
        // 레거시 지역별 카운트(폐허/상·중·심). 동굴 배치로 전환 후 기본 0 — DataMenu 호환용으로만 유지.
        [Min(0)][SerializeField] private int chestCountRuins = 0;
        [Min(0)][SerializeField] private int chestCountUpper = 0;
        [Min(0)][SerializeField] private int chestCountMiddle = 0;
        [Min(0)][SerializeField] private int chestCountDeep = 0;
        [Min(0)][SerializeField] private int chestPerCavernMin = 0;
        [Min(0)][SerializeField] private int chestPerCavernMax = 2;
        [SerializeField] private string chestIdPrefix = "chest_";

        [Header("검증/리롤 (개발 가이드② §5, 실패 시 seed+1)")]
        [Min(1)][SerializeField] private int maxRerollAttempts = 200;
        [Min(1)][SerializeField] private int onboardingSearchRadius = 18;
        [Min(1)][SerializeField] private int onboardingRequiredDirt = 8;   // recipes.csv workbench: dirt:8
        [Min(1)][SerializeField] private int onboardingRequiredStone = 12; // recipes.csv workbench: stone:12

        public int MapWidth => mapWidth;
        public int MapHeight => mapHeight;

        public float SurfaceBaseHeightRatio => surfaceBaseHeightRatio;
        public float SurfaceNoiseAmplitude => surfaceNoiseAmplitude;
        public float SurfaceNoiseFrequency => surfaceNoiseFrequency;
        public int UpperLayerThickness => upperLayerThickness;
        public int MiddleLayerThickness => middleLayerThickness;
        public int BedrockThickness => bedrockThickness;
        public float UpperDirtRatio => upperDirtRatio;
        public int SurfaceMineralBanDepth => Mathf.Max(1, surfaceMineralBanDepth);

        public float CaveNoiseFrequency => caveNoiseFrequency;
        public float CaveChanceUpper => caveChanceUpper;
        public float CaveChanceDeep => caveChanceDeep;
        public int CaveMaxHeight => caveMaxHeight;
        public int CaveSurfaceCrustThickness => caveSurfaceCrustThickness;

        public int LargeCavernCountMin => Mathf.Min(largeCavernCountMin, largeCavernCountMax);
        public int LargeCavernCountMax => Mathf.Max(largeCavernCountMin, largeCavernCountMax);
        public int LargeCavernWidth => largeCavernWidth;
        public int LargeCavernHeight => largeCavernHeight;
        public int LargeCavernMinEdgeGap => largeCavernMinEdgeGap;
        public int LargeCavernMinDepthBelowCrust => largeCavernMinDepthBelowCrust;
        public int LargeCavernMarginFromSpawn => largeCavernMarginFromSpawn;
        public int LargeCavernMarginFromAltar => largeCavernMarginFromAltar;
        public int LargeCavernMaxPlacementAttempts => Mathf.Max(1, largeCavernMaxPlacementAttempts);

        public OreVeinProfile[] OreVeins => oreVeins ?? Array.Empty<OreVeinProfile>();

        public float SpawnColumnRatio => spawnColumnRatio;
        public int SpawnEntranceDepthTiles => Mathf.Max(1, spawnEntranceDepthTiles);

        /// <summary>공식 스폰 입구/스폰 열 하단 y(포함). 깊이는 <see cref="SpawnEntranceDepthTiles"/>칸.</summary>
        public int GetSpawnEntranceOfficialBottom(int surfaceY) =>
            Mathf.Max(BedrockThickness, surfaceY - (SpawnEntranceDepthTiles - 1));

        public int AlcoveWidth => alcoveWidth;
        public int AlcoveHeight => alcoveHeight;

        public int RuinCountMin => Mathf.Min(ruinCountMin, ruinCountMax);
        public int RuinCountMax => Mathf.Max(ruinCountMin, ruinCountMax);
        public int RuinWidth => ruinWidth;
        public int RuinHeight => ruinHeight;

        public float DeepAltarColumnRatio => deepAltarColumnRatio;
        public int AltarSize => altarSize;
        public int AltarHardness => altarHardness;
        public int IceLakeWidth => iceLakeWidth;
        public int IceLakeHeight => iceLakeHeight;

        public int ChestCountRuins => chestCountRuins;
        public int ChestCountUpper => chestCountUpper;
        public int ChestCountMiddle => chestCountMiddle;
        public int ChestCountDeep => chestCountDeep;
        public int ChestPerCavernMin => Mathf.Min(chestPerCavernMin, chestPerCavernMax);
        public int ChestPerCavernMax => Mathf.Max(chestPerCavernMin, chestPerCavernMax);
        /// <summary>레거시 지역 합 + 동굴 상한(리스트 용량·경고용). 실제 개수는 시드·동굴 수에 따라 달라진다.</summary>
        public int TotalChestCount =>
            chestCountRuins + chestCountUpper + chestCountMiddle + chestCountDeep +
            LargeCavernCountMax * ChestPerCavernMax;
        public string ChestIdPrefix => string.IsNullOrEmpty(chestIdPrefix) ? "chest_" : chestIdPrefix;

        public int MaxRerollAttempts => maxRerollAttempts;
        public int OnboardingSearchRadius => onboardingSearchRadius;
        public int OnboardingRequiredDirt => onboardingRequiredDirt;
        public int OnboardingRequiredStone => onboardingRequiredStone;

        /// <summary>인스펙터 에셋 없이 순수 C# 컨텍스트(테스트/툴)에서 쓸 기본 설정.</summary>
        public static WorldGenerationConfig CreateDefault() => CreateInstance<WorldGenerationConfig>();
    }
}
