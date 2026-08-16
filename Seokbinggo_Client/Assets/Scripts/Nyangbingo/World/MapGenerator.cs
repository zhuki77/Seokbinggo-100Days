using System;
using System.Collections.Generic;
using Nyangbingo.Core;
using Nyangbingo.Data;
using UnityEngine;

namespace Nyangbingo.World
{
    // ------------------------------------------------------------------
    // Tile metadata (GDD 기획 가이드 ②/④ 정본 + v15 QA-F/v17 반영)
    //
    // TileService(경도 판정)와 SealSystem(밀폐 판정)이 이 구조체를 그대로 읽는다.
    // isNaturalTerrain이 false로 잘못 채워지면 SealSystem이 흙/돌/암반을
    // 밀폐 벽으로 인정하지 못해 맵의 대부분에서 판정이 뒤집힌다 (v15 QA-F 경고).
    // ------------------------------------------------------------------

    public enum WorldLayer { Surface, Upper, Middle, Deep, Bedrock }

    /// <summary>
    /// items.csv / mineral-tiers.csv 정본 ID와 매칭되는 타일 종류 문자열 상수.
    /// 코드 곳곳에 매직 스트링을 흩뿌리지 않기 위한 단일 출처.
    /// </summary>
    public static class WorldTileTypes
    {
        public const string Air = "air";

        // 상층 (T1)
        public const string Dirt = "dirt";
        public const string Stone = "stone";
        public const string Coal = "coal";
        public const string Clay = "clay";
        public const string OysterMushroom = "oyster_mushroom";

        // 중층 (T2)
        public const string StoneMid = "stone_mid";
        public const string IronOre = "iron_ore";
        public const string CopperOre = "copper_ore";
        public const string IceShard = "ice_shard";
        public const string Shiitake = "shiitake";

        // 심층 (T3)
        public const string StoneDeep = "stone_deep";
        public const string IceSteelOre = "icesteel_ore";
        public const string FrostEssence = "frost_essence";
        public const string Seogi = "seogi";

        // 구조물 / 최하단
        public const string Bedrock = "bedrock";
        public const string RuinWall = "ruin_wall";
        public const string IceLake = "ice_lake";
        public const string IceAltar = "ice_altar";

        // 배경벽 (채굴 불가 · 장식용 · 테라리아식 2중 구조). 런타임 저장 ID는 bg_* 유지.
        public const string BackgroundDirt = "bg_dirt";
        public const string BackgroundStone = "bg_stone";
        public const string BackgroundDeep = "bg_deep";

        // 공식 기획 ID(v26) — TileIdAlias가 bg_*와 동일 타일로 해석한다(단순 문자열 교체 마이그레이션 금지).
        public const string OfficialBackgroundDirt = "t_bg_dirt";
        public const string OfficialBackgroundStone = "t_bg_stone";
        public const string OfficialBackgroundDeep = "t_bg_deep";

        /// <summary>플레이어 설치 벽지(items.csv wallpaper). seals=0 — 밀폐 경계로 인정하지 않는다.</summary>
        public const string Wallpaper = "wallpaper";

        /// <summary>
        /// Air를 제외한 모든 알려진 elementType(전경 + 배경벽 + 벽지 + 공식 배경 ID 별칭).
        /// 세이브 로드 시 타일 변경 이력의 tileId가 이 목록에 없으면 손상된(또는 알 수 없는) 데이터로 간주해
        /// 거부한다(A-06/A-08 — 문자열을 직접 이곳저곳에서 비교하지 않고 단일 출처로 검증).
        /// </summary>
        public static readonly HashSet<string> AllElementTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            Dirt, Stone, Coal, Clay, OysterMushroom,
            StoneMid, IronOre, CopperOre, IceShard, Shiitake,
            StoneDeep, IceSteelOre, FrostEssence, Seogi,
            Bedrock, RuinWall, IceLake, IceAltar,
            BackgroundDirt, BackgroundStone, BackgroundDeep,
            OfficialBackgroundDirt, OfficialBackgroundStone, OfficialBackgroundDeep,
            Wallpaper,
            // 플레이어 설치 전경(차열 경계). door_top은 1x2 문 위칸 충돌 전용.
            "insul_wall", "iron_insul_wall", "roof", "door", "door_top"
        };

        public static bool IsBackgroundId(string elementType)
        {
            var id = TileIdAlias.ToCanonical(elementType);
            return id == BackgroundDirt || id == BackgroundStone || id == BackgroundDeep || id == Wallpaper;
        }
    }

    /// <summary>
    /// A-16: 공식 기획 ID(t_bg_*)와 런타임 저장 ID(bg_*)를 동일 타일로 해석하는 별칭 테이블.
    /// 저장 파일의 기존 bg_* 문자열은 그대로 유효하며, 단순 일괄 교체는 하지 않는다.
    /// </summary>
    public static class TileIdAlias
    {
        public static string ToCanonical(string elementType)
        {
            if (string.IsNullOrEmpty(elementType)) return elementType;
            if (string.Equals(elementType, WorldTileTypes.OfficialBackgroundDirt, StringComparison.Ordinal))
                return WorldTileTypes.BackgroundDirt;
            if (string.Equals(elementType, WorldTileTypes.OfficialBackgroundStone, StringComparison.Ordinal))
                return WorldTileTypes.BackgroundStone;
            if (string.Equals(elementType, WorldTileTypes.OfficialBackgroundDeep, StringComparison.Ordinal))
                return WorldTileTypes.BackgroundDeep;
            return elementType;
        }

        public static bool EqualsCanonical(string a, string b) =>
            string.Equals(ToCanonical(a), ToCanonical(b), StringComparison.Ordinal);
    }

    /// <summary>
    /// WorldGenerator가 찍는 낱개 타일의 메타데이터.
    /// A-16: 전경(elementType/hardness)과 배경(backgroundElementType)을 한 셀에 동시에 보존한다.
    /// naturalBackgroundElementType은 벽지 제거 시 복원 기준(자연 배경 또는 빈 배경)이다.
    /// </summary>
    [Serializable]
    public struct TileData
    {
        /// <summary>경도 1~3. 발톱 티어 ≥ 경도일 때만 파괴 가능(TileService 판정).</summary>
        public int hardness;

        /// <summary>
        /// 자연 지형(흙·돌·암반 등) 여부. seal-whitelist.csv 기준 true여야 SealSystem이 밀폐 벽으로 인정한다.
        /// WorldGenerator가 생성하는 지형·광맥 타일은 전부 true, 플레이어 건설물/장식은 false.
        /// </summary>
        public bool isNaturalTerrain;

        /// <summary>전경 자원/타일 종류 ID. 공기면 <see cref="WorldTileTypes.Air"/>.</summary>
        public string elementType;

        /// <summary>현재 배경 ID(bg_*/wallpaper/air). 전경과 독립적으로 유지된다.</summary>
        public string backgroundElementType;

        /// <summary>벽지 제거 시 복원할 원래 자연 배경(또는 air). 동굴·하늘은 air.</summary>
        public string naturalBackgroundElementType;

        /// <summary>
        /// 레거시 호환: 배경이 비어 있지 않으면 true.
        /// 신규 코드는 <see cref="HasBackground"/>를 쓰는 것을 권장한다.
        /// </summary>
        public bool isUndergroundDecor
        {
            get => HasBackground;
            set
            {
                if (!value && !HasBackground) return;
                if (!value)
                {
                    backgroundElementType = WorldTileTypes.Air;
                }
            }
        }

        public bool HasBackground =>
            !string.IsNullOrEmpty(backgroundElementType) &&
            !string.Equals(backgroundElementType, WorldTileTypes.Air, StringComparison.Ordinal);

        public bool HasNaturalBackground =>
            !string.IsNullOrEmpty(naturalBackgroundElementType) &&
            !string.Equals(naturalBackgroundElementType, WorldTileTypes.Air, StringComparison.Ordinal);

        public bool IsWallpaperBackground =>
            string.Equals(TileIdAlias.ToCanonical(backgroundElementType), WorldTileTypes.Wallpaper, StringComparison.Ordinal);

        /// <summary>벽지 도포율 분자: 자연 지하 배경이 있거나 벽지가 깔린 칸.</summary>
        public bool CountsAsWallpaperCovered => HasNaturalBackground || IsWallpaperBackground;

        public static TileData CreateAir() => new TileData
        {
            hardness = 0,
            isNaturalTerrain = false,
            elementType = WorldTileTypes.Air,
            backgroundElementType = WorldTileTypes.Air,
            naturalBackgroundElementType = WorldTileTypes.Air
        };

        /// <summary>
        /// A-16: 전경만 비운 칸. 기존 배경/자연 배경 기준은 그대로 유지한다(채굴 규칙).
        /// backgroundElement가 주어지면(레거시 CreateCaveAir 호출) 그 값을 현재·자연 배경으로 깐다.
        /// </summary>
        public static TileData CreateCaveAir(string backgroundElement)
        {
            if (string.IsNullOrEmpty(backgroundElement) ||
                string.Equals(backgroundElement, WorldTileTypes.Air, StringComparison.Ordinal))
                return CreateAir();

            var bg = TileIdAlias.ToCanonical(backgroundElement);
            return new TileData
            {
                hardness = 0,
                isNaturalTerrain = false,
                elementType = WorldTileTypes.Air,
                backgroundElementType = bg,
                naturalBackgroundElementType = bg
            };
        }

        /// <summary>전경 자연 지형 + 해당 지층 자연 배경을 함께 생성(A-16).</summary>
        public static TileData CreateNaturalWithBackground(string elementId, int hardnessValue, string naturalBackground) =>
            new TileData
            {
                hardness = hardnessValue,
                isNaturalTerrain = true,
                elementType = elementId,
                backgroundElementType = string.IsNullOrEmpty(naturalBackground) ? WorldTileTypes.Air : TileIdAlias.ToCanonical(naturalBackground),
                naturalBackgroundElementType = string.IsNullOrEmpty(naturalBackground) ? WorldTileTypes.Air : TileIdAlias.ToCanonical(naturalBackground)
            };

        /// <summary>배경 없는 자연/구조물 전경(지상 폐허·제단 등).</summary>
        public static TileData CreateNatural(string elementId, int hardnessValue) =>
            CreateNaturalWithBackground(elementId, hardnessValue, WorldTileTypes.Air);

        /// <summary>전경만 제거하고 배경 필드는 유지한다(채굴).</summary>
        public TileData WithoutForeground() => new TileData
        {
            hardness = 0,
            isNaturalTerrain = false,
            elementType = WorldTileTypes.Air,
            backgroundElementType = string.IsNullOrEmpty(backgroundElementType) ? WorldTileTypes.Air : backgroundElementType,
            naturalBackgroundElementType = string.IsNullOrEmpty(naturalBackgroundElementType) ? WorldTileTypes.Air : naturalBackgroundElementType
        };

        /// <summary>벽지 제거 시 자연 배경(또는 빈 배경)으로 복원한다.</summary>
        public TileData WithBackgroundRestoredToNatural() => new TileData
        {
            hardness = hardness,
            isNaturalTerrain = isNaturalTerrain,
            elementType = elementType,
            backgroundElementType = string.IsNullOrEmpty(naturalBackgroundElementType) ? WorldTileTypes.Air : naturalBackgroundElementType,
            naturalBackgroundElementType = string.IsNullOrEmpty(naturalBackgroundElementType) ? WorldTileTypes.Air : naturalBackgroundElementType
        };

        /// <summary>전경에 파괴/채굴 대상이 없는(통행 가능한) 칸인지. 배경벽 유무와 무관하게 hardness로만 판별한다.</summary>
        public bool IsAir => hardness <= 0;
    }

    /// <summary>Pass 3 광맥(Vein) 하나의 배치 규칙. mineral-tiers.csv의 "빈도(개/100타일)"를 그대로 옮겨온다.</summary>
    [Serializable]
    public struct OreVeinProfile
    {
        public string elementType;

        /// <summary>배경벽 색상 선택 등 보조 분류용(레거시). 실제 배치 범위는 depthMin/depthMax를 우선한다.</summary>
        public WorldLayer layer;

        [Min(0f)] public float frequencyPer100Tiles;
        [Min(1)] public int minClusterSize;
        [Min(1)] public int maxClusterSize;

        /// <summary>
        /// A-10: mineral-tiers.csv의 depth_min/depth_max(지표에서 내려간 깊이, 1부터 시작)를 그대로 옮긴다.
        /// 0(둘 다 0)이면 <see cref="layer"/> 기반 레이어 두께 범위로 폴백한다(레거시 프로파일 호환).
        /// 좌표 변환: 각 열의 실제 지표(surfaceHeights[x])를 기준으로 depth 1 = surfaceHeights[x],
        /// depth d = surfaceHeights[x] - d + 1 — MapGenerator.GetDepthRange가 이 규칙을 그대로 구현한다.
        /// </summary>
        [Min(0)] public int depthMin;
        [Min(0)] public int depthMax;
    }

    /// <summary>Pass 4가 결정하는 상자 1개의 위치·식별 정보. 내용물은 ChestGen/GameDataCatalog에 위임한다.</summary>
    public struct ChestSpawnPoint
    {
        public string id;
        public Vector2Int position;
        public ChestRegion region;
    }

    /// <summary>Generate 한 번의 완전한 결과. TileData[,] 외의 배치 정보(스폰·제단·상자)를 함께 노출한다.</summary>
    public struct WorldGenerationResult
    {
        public int requestedSeed;
        public int acceptedSeed;
        public int rerollAttempts;
        public int width;
        public int height;
        public TileData[,] tiles;
        public Vector2Int spawnPoint;
        public Vector2Int altarPosition;
        public List<ChestSpawnPoint> chests;
        public bool passedValidation;

        /// <summary>
        /// A-10: 각 열(x)의 지표 y좌표(Pass 1 결과 그대로). 회귀 테스트가 "각 열의 지표로부터 내려간 깊이"
        /// 계약(지층 경계·광물 depth_min/depth_max)을 광물 CSV와 직접 대조 검증할 때 쓴다.
        /// </summary>
        public int[] surfaceHeights;
    }

    // WorldGenerationConfig ScriptableObject는 별도 파일 WorldGenerationConfig.cs (같은 Nyangbingo.World
    // 네임스페이스)에 정의돼 있다. 여기서 다시 정의하면 CS0101(중복 클래스) 컴파일 에러가 나서
    // 이 어셈블리의 모든 ScriptableObject/MonoBehaviour 에셋이 "Script: None"으로 깨지니 절대 합치지 말 것.

    /// <summary>
    /// 개발 A 담당 — 무작위 타일맵 생성 및 월드 구축.
    ///
    /// 계약:
    ///  - <see cref="Generate(int)"/>는 순수 함수형 진입점이다. 동일 seed → 동일 결과(타일 단위 재현),
    ///    씬/엔진 상태를 건드리지 않고 배열만 반환한다.
    ///  - 4패스(지형→동굴→자원→구조물)는 패스마다 독립된 System.Random(seed+N)을 쓴다 — 절대 하나의
    ///    Random 인스턴스를 여러 패스에서 공유하지 않는다(패스 순서가 바뀌어도 재현성이 안 깨지게).
    ///  - 생성 직후 4가지 검증(§5)을 통과하지 못하면 seed+1로 재시도한다(무한 루프 방지용 상한 있음).
    ///  - 초기 생성 단계에서는 GameEvents.OnTilePlaced/OnTileBroken을 발행하지 않는다 — 그건 플레이어의
    ///    실제 파괴/설치 액션부터 호출자가 발행할 몫이다(수만 타일 초기화 시 이벤트 폭주 방지).
    ///  - Pass 4에서 만든 20개 상자 배치는 <see cref="IChestSource"/>로 그대로 노출해 상자 연결(§7)에 쓴다.
    /// </summary>
    public sealed class MapGenerator : IChestSource
    {
        private static readonly Vector2Int[] FourNeighbors =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1)
        };

        // v27: 대각선·지그재그 포함 세로 연결 공동을 잡기 위한 8방.
        private static readonly Vector2Int[] EightNeighbors =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
            new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
        };

        private readonly WorldGenerationConfig config;
        private readonly IReadOnlyDictionary<string, int> mineralHardnessById;
        private readonly IReadOnlyList<OreVeinProfile> mineralProfiles;
        private readonly int boundaryIceRockHardness;

        private WorldGenerationResult lastResult;
        private Dictionary<string, ChestSpawnPoint> chestsById = new Dictionary<string, ChestSpawnPoint>(StringComparer.Ordinal);
        private List<string> chestIdsCache = new List<string>();

        public MapGenerator(WorldGenerationConfig generationConfig, GameDataCatalog catalog)
        {
            config = generationConfig != null ? generationConfig : WorldGenerationConfig.CreateDefault();
            if (catalog == null) throw new ArgumentNullException(nameof(catalog),
                "mineral-tiers.csv 경도 정본을 제공하는 GameDataCatalog가 필요합니다.");

            var hardnessById = new Dictionary<string, int>(StringComparer.Ordinal);
            var profiles = new List<OreVeinProfile>();
            var clusterMin = ReadPositiveInt(catalog, GlobalKeys.OreVeinClusterMin, 3);
            var clusterMax = ReadPositiveInt(catalog, GlobalKeys.OreVeinClusterMax, 6);
            var boundaryHardnessDefinition = catalog.FindGlobal(GlobalKeys.BoundaryIceRockHardness);
            if (boundaryHardnessDefinition == null ||
                !boundaryHardnessDefinition.TryGetInt(out boundaryIceRockHardness) ||
                boundaryIceRockHardness < 1 || boundaryIceRockHardness > 3)
                throw new InvalidOperationException(
                    "globals.boundary_ice_rock_hardness가 없거나 경도 1~3 범위를 벗어났습니다.");
            if (clusterMin > clusterMax)
                throw new InvalidOperationException("ore_vein_cluster_min/max 순서가 잘못되었습니다.");
            foreach (var profile in config.OreVeins)
            {
                var definition = catalog.FindMineralTier(profile.elementType);
                if (definition == null || definition.Hardness < 1 || definition.Hardness > 3 ||
                    definition.FrequencyPerHundredTiles < 0f ||
                    definition.MinimumDepth < 0 || definition.MaximumDepth < definition.MinimumDepth)
                    throw new InvalidOperationException(
                        $"mineral-tiers.csv 광물 정의가 없거나 잘못되었습니다: '{profile.elementType}'.");
                hardnessById.Add(profile.elementType, definition.Hardness);
                profiles.Add(new OreVeinProfile
                {
                    elementType = definition.Id,
                    layer = ToWorldLayer(definition.Layer),
                    frequencyPer100Tiles = definition.FrequencyPerHundredTiles,
                    minClusterSize = clusterMin,
                    maxClusterSize = clusterMax,
                    depthMin = definition.MinimumDepth,
                    depthMax = definition.MaximumDepth
                });
            }
            mineralHardnessById = hardnessById;
            mineralProfiles = profiles;
        }

        private static int ReadPositiveInt(GameDataCatalog catalog, string key, int fallback)
        {
            var value = catalog?.FindGlobal(key);
            return value != null && value.TryGetInt(out var parsed) && parsed > 0 ? parsed : fallback;
        }

        private static WorldLayer ToWorldLayer(MineralLayer layer)
        {
            switch (layer)
            {
                case MineralLayer.UndergroundUpper: return WorldLayer.Upper;
                case MineralLayer.UndergroundMiddle: return WorldLayer.Middle;
                case MineralLayer.UndergroundDeep: return WorldLayer.Deep;
                default:
                    throw new InvalidOperationException(
                        $"월드 광맥 프로파일에 지하가 아닌 mineral layer가 연결되었습니다: {layer}.");
            }
        }

        public WorldGenerationResult LastResult => lastResult;
        public Vector2Int SpawnPoint => lastResult.spawnPoint;
        public Vector2Int AltarPosition => lastResult.altarPosition;
        public int AcceptedSeed => lastResult.acceptedSeed;
        public IReadOnlyList<OreVeinProfile> MineralProfiles => mineralProfiles;

        // ------------------------------------------------------------
        // 단일 진입점 (요구된 시그니처): 부작용 없는 순수 함수형 진입점.
        // 내부적으로 GenerateDetailed를 호출하고 결과를 캐시해 IChestSource 등
        // 후속 연결에 쓴다 — 이 캐시는 계산 자체에 피드백되지 않으므로 결정론에 영향 없다.
        // ------------------------------------------------------------
        public TileData[,] Generate(int seed) => GenerateDetailed(seed).tiles;

        /// <summary>타일 배열뿐 아니라 스폰/제단/상자 배치까지 전부 담은 상세 결과를 반환한다.</summary>
        public WorldGenerationResult GenerateDetailed(int seed)
        {
            var attempt = 0;
            WorldGenerationResult result;

            while (true)
            {
                var candidateSeed = seed + attempt;
                result = GenerateSingleAttempt(
                    candidateSeed, config, mineralHardnessById, mineralProfiles, boundaryIceRockHardness);
                result.requestedSeed = seed;
                result.rerollAttempts = attempt;

                var passed = ValidateWorld(result, config);
                result.passedValidation = passed;

                if (passed || attempt >= config.MaxRerollAttempts)
                {
                    if (!passed)
                    {
                        Debug.LogWarning(
                            $"[MapGenerator] seed {seed} reached max reroll attempts ({config.MaxRerollAttempts}); " +
                            $"returning best-effort world with seed {candidateSeed} despite failed validation.");
                    }
                    break;
                }

                attempt++;
            }

            CacheResult(result);
            return result;
        }

        private void CacheResult(WorldGenerationResult result)
        {
            lastResult = result;
            chestsById = new Dictionary<string, ChestSpawnPoint>(StringComparer.Ordinal);
            chestIdsCache = new List<string>();

            if (result.chests == null) return;
            foreach (var chest in result.chests)
            {
                chestsById[chest.id] = chest;
                chestIdsCache.Add(chest.id);
            }
        }

        // ------------------------------------------------------------
        // IChestSource — Development A가 배치를 소유하고, 상자 내용/보상은 개발 B(ChestGen)에 위임.
        // ------------------------------------------------------------
        public IReadOnlyList<string> ChestIds => chestIdsCache;

        public Vector2 GetChestPosition(string chestId)
        {
            if (!string.IsNullOrEmpty(chestId) && chestsById.TryGetValue(chestId, out var chest))
                return chest.position;
            return Vector2.zero;
        }

        /// <summary>상자가 어느 지역(Ruins/Upper/Middle/Deep)에 배치됐는지 — 지역별 보상 풀 선택에 사용.</summary>
        public ChestRegion GetChestRegion(string chestId)
        {
            return !string.IsNullOrEmpty(chestId) && chestsById.TryGetValue(chestId, out var chest)
                ? chest.region
                : ChestRegion.Ruins;
        }

        /// <summary>IChestSource는 ID→좌표만 노출하므로(§7.1 계약 그대로 유지), 우클릭 좌표→상자ID 역조회는
        /// 여기 별도 공개 메서드로 추가한다. 상자 수는 시드·동굴에 따라 소량이라 선형 탐색으로도 충분하다.</summary>
        public bool TryGetChestIdAt(Vector2Int position, out string chestId)
        {
            foreach (var pair in chestsById)
            {
                if (pair.Value.position != position) continue;
                chestId = pair.Key;
                return true;
            }
            chestId = null;
            return false;
        }

        // ==============================================================
        // 한 번의 생성 시도 (4패스, 검증은 별도)
        // ==============================================================
        private static WorldGenerationResult GenerateSingleAttempt(int seed, WorldGenerationConfig config,
            IReadOnlyDictionary<string, int> hardnessById,
            IReadOnlyList<OreVeinProfile> profiles, int boundaryHardness)
        {
            var width = config.MapWidth;
            var height = config.MapHeight;
            var grid = new TileData[width, height];
            // Pass 4 입구 등 — protectedAir는 스폰 발밑 3×3·제단 최소만 (연결 통로 성역화 금지).
            var protectedAir = new bool[width, height];

            // 패스별로 완전히 분리된 RNG — 순서가 바뀌어도 재현성이 깨지지 않는다.
            var terrainRng = new System.Random(seed + 1);
            var caveRng = new System.Random(seed + 2);
            var resourceRng = new System.Random(seed + 3);
            var structureRng = new System.Random(seed + 4);
            var largeCavernRng = new System.Random(seed + 5);
            var cavernChestRng = new System.Random(seed + 6);

            // Pass 1 — 지형
            var surfaceHeights = GenerateTerrain(grid, terrainRng, config, boundaryHardness);
            // Pass 2 — 펄린 동굴(후처리 없음)
            CarveCaves(grid, surfaceHeights, caveRng, config);
            // Pass 3 — 광맥(돌·석탄 포함, 지표 ban depth 아래)
            PlaceOreVeins(grid, surfaceHeights, resourceRng, config, hardnessById, profiles);
            // Pass 4 — 구조물·스폰·얕은 입구 (상자는 대형 동굴 개척 이후)
            var structures = PlaceStructures(grid, surfaceHeights, structureRng, config, protectedAir);
            // Pass 4b — 심층 제단 접근(지표·crust 절대 미개척). 스폰 연결은 PostProcess 이후 4c에서 보장.
            CarveConnectivityShafts(grid, surfaceHeights, caveRng, config,
                structures.spawnPoint, structures.altarPosition);
            // Pass 최종 — Hard Fill/Hard Cut
            PostProcessCaveCavities(grid, surfaceHeights, config, protectedAir, boundaryHardness);
            // Hard Fill 후 공식 얕은 입구만 재개방
            ReopenSpawnEntrance(grid, surfaceHeights, structures.spawnPoint, config);
            // Pass 4b2 — 시드 기반 중간층 공동 + 동굴당 상자 0~2
            var caverns = CarveLargeCaverns(grid, surfaceHeights, largeCavernRng, config, protectedAir,
                structures.spawnPoint, structures.altarPosition);
            structures.chests = PlaceCavernChests(grid, surfaceHeights, caverns, cavernChestRng, config,
                protectedAir);
            // Pass 4c — v27: 지상→심층 인공 통로 Carve 금지. 지하 공동 간 최소 벽(≤3)만 천공.
            EnsureReachabilityByCavityBridges(grid, surfaceHeights, structures.spawnPoint,
                structures.altarPosition, config);
            EnsureOnboardingResourcesNearSpawn(grid, surfaceHeights, structures.spawnPoint, config);
            // A-18: 스폰 발밑 최종 보강
            ReinforceSafeSpawnFooting(grid, surfaceHeights, structures.spawnPoint, config);
            PopulateNaturalCaveBackgrounds(grid, surfaceHeights, config);
            EnsureChestCellsHaveNoForeground(grid, structures.chests);

            return new WorldGenerationResult
            {
                acceptedSeed = seed,
                width = width,
                height = height,
                tiles = grid,
                spawnPoint = structures.spawnPoint,
                altarPosition = structures.altarPosition,
                chests = structures.chests,
                surfaceHeights = surfaceHeights
            };
        }

        // ==============================================================
        // Pass 1 — 지형 (1D 펄린 높이라인 + 레이어 경계 + 최하단 경계암)
        // ==============================================================
        private static int[] GenerateTerrain(TileData[,] grid, System.Random rng, WorldGenerationConfig config,
            int boundaryHardness)
        {
            var width = config.MapWidth;
            var height = config.MapHeight;
            var surfaceHeights = new int[width];

            var heightNoiseOffset = (float)(rng.NextDouble() * 100000.0);

            var baseHeight = Mathf.RoundToInt(height * config.SurfaceBaseHeightRatio);
            var minSurfaceY = Mathf.Min(height - 1, config.BedrockThickness + config.MiddleLayerThickness + config.UpperLayerThickness);

            for (var x = 0; x < width; x++)
            {
                var noise = Mathf.PerlinNoise((x + heightNoiseOffset) * config.SurfaceNoiseFrequency, 0.5f);
                var offset = Mathf.RoundToInt((noise - 0.5f) * 2f * config.SurfaceNoiseAmplitude);
                var surfaceY = Mathf.Clamp(baseHeight + offset, minSurfaceY, height - 1);
                surfaceHeights[x] = surfaceY;

                for (var y = 0; y < height; y++)
                {
                    var layer = ClassifyLayer(y, surfaceY, config);
                    grid[x, y] = layer switch
                    {
                        // A-16: 하늘은 전경·배경 모두 비움. 상층은 흙 매트릭스 + Pass 3 돌/석탄 광맥.
                        WorldLayer.Surface => TileData.CreateAir(),
                        WorldLayer.Bedrock => TileData.CreateNaturalWithBackground(WorldTileTypes.Bedrock, 3, WorldTileTypes.BackgroundDeep),
                        WorldLayer.Upper => TileData.CreateNaturalWithBackground(
                            WorldTileTypes.Dirt, 1, WorldTileTypes.BackgroundDirt),
                        WorldLayer.Middle => TileData.CreateNaturalWithBackground(WorldTileTypes.StoneMid, 2, WorldTileTypes.BackgroundStone),
                        _ => TileData.CreateNaturalWithBackground(WorldTileTypes.StoneDeep, boundaryHardness,
                            WorldTileTypes.BackgroundDeep)
                    };
                }
            }

            return surfaceHeights;
        }

        /// <summary>지표에서 내려간 깊이(1=지표 타일). surfaceMineralBanDepth 이하면 광물/돌 금지.</summary>
        private static int DepthFromSurface(int y, int surfaceY) => surfaceY - y + 1;

        private static bool IsInSurfaceMineralBan(int y, int surfaceY, WorldGenerationConfig config) =>
            y <= surfaceY && DepthFromSurface(y, surfaceY) <= config.SurfaceMineralBanDepth;

        /// <summary>주어진 y좌표(0=최하단)가 해당 column에서 어떤 레이어에 속하는지 분류한다.</summary>
        private static WorldLayer ClassifyLayer(int y, int surfaceY, WorldGenerationConfig config)
        {
            if (y > surfaceY) return WorldLayer.Surface;
            if (y < config.BedrockThickness) return WorldLayer.Bedrock;

            var upperBottom = surfaceY - config.UpperLayerThickness + 1;
            if (y >= upperBottom) return WorldLayer.Upper;

            var middleBottom = upperBottom - config.MiddleLayerThickness;
            if (y >= middleBottom) return WorldLayer.Middle;

            return WorldLayer.Deep;
        }

        /// <summary>
        /// A-10: mineral-tiers.csv의 depth_min/depth_max를 실제 좌표로 변환한다("각 열의 지표로부터
        /// 내려간 깊이" 기준 — depth 1은 그 열의 지표 타일(surfaceHeights[x]) 자신). depthMax가 0이면
        /// (프로파일에 depth가 없는 레거시 데이터) 호출자가 <see cref="GetLayerRange"/>로 폴백해야 한다.
        /// </summary>
        private static (int low, int high) GetDepthRange(int x, int depthMin, int depthMax, int[] surfaceHeights, WorldGenerationConfig config)
        {
            var surfaceY = surfaceHeights[Mathf.Clamp(x, 0, surfaceHeights.Length - 1)];
            var deepFloor = config.BedrockThickness; // 경계암 바로 위부터가 채굴 가능한 최심층.
            var high = Mathf.Clamp(surfaceY - Mathf.Max(1, depthMin) + 1, deepFloor, surfaceY);
            var low = Mathf.Clamp(surfaceY - depthMax + 1, deepFloor, surfaceY);
            return low <= high ? (low, high) : (high, low);
        }

        /// <summary>해당 column에서 특정 레이어가 차지하는 y 범위(포함) — Pass3/Pass4가 좌표를 뽑을 때 사용.</summary>
        private static (int low, int high) GetLayerRange(int x, WorldLayer layer, int[] surfaceHeights, WorldGenerationConfig config)
        {
            var surfaceY = surfaceHeights[x];
            var upperBottom = surfaceY - config.UpperLayerThickness + 1;
            var middleBottom = upperBottom - config.MiddleLayerThickness;
            var deepBottom = config.BedrockThickness;

            switch (layer)
            {
                case WorldLayer.Upper:
                    return (Mathf.Max(upperBottom, deepBottom), surfaceY);
                case WorldLayer.Middle:
                    return (Mathf.Max(middleBottom, deepBottom), Mathf.Max(upperBottom - 1, deepBottom));
                case WorldLayer.Deep:
                    return (deepBottom, Mathf.Max(middleBottom - 1, deepBottom));
                case WorldLayer.Bedrock:
                    return (0, config.BedrockThickness - 1);
                default:
                    return (surfaceY + 1, config.MapHeight - 1);
            }
        }

        // ==============================================================
        // Pass 2 — 2D 펄린 동굴 개척만 (세로 한도·지표 봉인은 최후반 PostProcess)
        // ==============================================================
        private static void CarveCaves(TileData[,] grid, int[] surfaceHeights, System.Random rng, WorldGenerationConfig config)
        {
            var width = config.MapWidth;
            var crust = Mathf.Max(1, config.CaveSurfaceCrustThickness);
            var offsetX = (float)(rng.NextDouble() * 100000.0);
            var offsetY = (float)(rng.NextDouble() * 100000.0);

            for (var x = 0; x < width; x++)
            {
                var surfaceY = surfaceHeights[x];
                var carveMaxExclusive = surfaceY - crust + 1;
                if (carveMaxExclusive <= config.BedrockThickness) continue;

                for (var y = config.BedrockThickness; y < carveMaxExclusive; y++)
                {
                    var layer = ClassifyLayer(y, surfaceY, config);
                    if (layer == WorldLayer.Surface || layer == WorldLayer.Bedrock) continue;

                    var depthT = Mathf.InverseLerp(surfaceY, config.BedrockThickness, y);
                    var caveChance = Mathf.Lerp(config.CaveChanceUpper, config.CaveChanceDeep, depthT);
                    var noise = Mathf.PerlinNoise(
                        (x + offsetX) * config.CaveNoiseFrequency,
                        (y + offsetY) * config.CaveNoiseFrequency);
                    if (noise < caveChance)
                        grid[x, y] = TileData.CreateAir();
                }
            }
        }

        private struct LargeCavernPlacement
        {
            public int CenterX;
            public int CenterY;
            public float HalfW;
            public float HalfH;
            public int MinX;
            public int MaxX;
        }

        /// <summary>
        /// 시드 기반 중간층 공동(기본 ≈30×20)을 띄엄띄엄 배치하고 배치 정보를 반환한다.
        /// <see cref="PostProcessCaveCavities"/> Hard Cut 이후에만 호출해 cave_max_height에 잘리지 않게 한다.
        /// </summary>
        private static List<LargeCavernPlacement> CarveLargeCaverns(TileData[,] grid, int[] surfaceHeights,
            System.Random rng, WorldGenerationConfig config, bool[,] protectedAir,
            Vector2Int spawnPoint, Vector2Int altarPosition)
        {
            var placements = new List<LargeCavernPlacement>();
            var countMin = config.LargeCavernCountMin;
            var countMax = config.LargeCavernCountMax;
            if (countMax <= 0) return placements;

            var targetCount = rng.Next(countMin, countMax + 1);
            if (targetCount <= 0) return placements;

            var width = config.MapWidth;
            var cavernW = Mathf.Max(4, config.LargeCavernWidth);
            var cavernH = Mathf.Max(4, config.LargeCavernHeight);
            var halfW = cavernW * 0.5f;
            var halfH = cavernH * 0.5f;
            var edgeGap = Mathf.Max(0, config.LargeCavernMinEdgeGap);
            var crust = Mathf.Max(1, config.CaveSurfaceCrustThickness);
            var minDepth = Mathf.Max(0, config.LargeCavernMinDepthBelowCrust);
            var spawnMargin = Mathf.Max(0, config.LargeCavernMarginFromSpawn);
            var altarMargin = Mathf.Max(0, config.LargeCavernMarginFromAltar);
            var maxAttempts = config.LargeCavernMaxPlacementAttempts;

            var placedMinX = new int[targetCount];
            var placedMaxX = new int[targetCount];
            var placedCount = 0;
            var noiseOx = (float)(rng.NextDouble() * 100000.0);
            var noiseOy = (float)(rng.NextDouble() * 100000.0);

            for (var attempt = 0; attempt < maxAttempts && placedCount < targetCount; attempt++)
            {
                var centerX = rng.Next(cavernW / 2 + 2, Mathf.Max(cavernW / 2 + 3, width - cavernW / 2 - 2));
                var minX = centerX - cavernW / 2;
                var maxX = centerX + (cavernW - 1) / 2;
                if (minX < 1 || maxX >= width - 1) continue;

                if (OverlapsPlacedLargeCavern(minX, maxX, placedMinX, placedMaxX, placedCount, edgeGap))
                    continue;

                if (HorizontalDistanceToPoint(centerX, spawnPoint.x) < spawnMargin + cavernW / 2)
                    continue;
                if (HorizontalDistanceToPoint(centerX, altarPosition.x) < altarMargin + cavernW / 2)
                    continue;

                if (!TryGetMiddleLayerLargeCavernVerticalRange(surfaceHeights, config, minX, maxX, cavernH, crust,
                        minDepth, out var minCenterY, out var maxCenterY))
                    continue;

                var centerY = rng.Next(minCenterY, maxCenterY + 1);

                CarveLargeCavernEllipse(grid, surfaceHeights, config, protectedAir,
                    centerX, centerY, halfW, halfH, noiseOx, noiseOy);

                placedMinX[placedCount] = minX;
                placedMaxX[placedCount] = maxX;
                placements.Add(new LargeCavernPlacement
                {
                    CenterX = centerX,
                    CenterY = centerY,
                    HalfW = halfW,
                    HalfH = halfH,
                    MinX = minX,
                    MaxX = maxX
                });
                placedCount++;
            }

            return placements;
        }

        private static int HorizontalDistanceToPoint(int a, int b) => Mathf.Abs(a - b);

        private static bool OverlapsPlacedLargeCavern(int minX, int maxX, int[] placedMinX, int[] placedMaxX,
            int placedCount, int edgeGap)
        {
            for (var i = 0; i < placedCount; i++)
            {
                if (maxX + edgeGap < placedMinX[i]) continue;
                if (minX - edgeGap > placedMaxX[i]) continue;
                return true;
            }
            return false;
        }

        private static int GetLargeCavernMaxTopY(int[] surfaceHeights, int minX, int maxX, int crust, int minDepth)
        {
            var maxTop = int.MaxValue;
            for (var x = minX; x <= maxX; x++)
            {
                // crust 봉인선(포함) 위는 금지 → 천장 최대 = surfaceY - crust - minDepth
                var top = surfaceHeights[x] - crust - minDepth;
                if (top < maxTop) maxTop = top;
            }
            return maxTop;
        }

        /// <summary>공동 전체가 중간층 대역 안에 들어가도록 centerY 허용 범위를 구한다.</summary>
        private static bool TryGetMiddleLayerLargeCavernVerticalRange(int[] surfaceHeights,
            WorldGenerationConfig config, int minX, int maxX, int cavernH, int crust, int minDepth,
            out int minCenterY, out int maxCenterY)
        {
            minCenterY = 0;
            maxCenterY = -1;
            var halfH = cavernH / 2;
            var bandLow = int.MinValue;
            var bandHigh = int.MaxValue;

            for (var x = minX; x <= maxX; x++)
            {
                var (low, high) = GetLayerRange(x, WorldLayer.Middle, surfaceHeights, config);
                if (high < low) return false;
                if (low > bandLow) bandLow = low;
                if (high < bandHigh) bandHigh = high;
            }

            if (bandHigh < bandLow) return false;

            var maxTop = GetLargeCavernMaxTopY(surfaceHeights, minX, maxX, crust, minDepth);
            if (maxTop < bandHigh) bandHigh = maxTop;
            if (bandHigh < bandLow) return false;

            minCenterY = bandLow + halfH;
            maxCenterY = bandHigh - (cavernH - halfH);
            return maxCenterY >= minCenterY;
        }

        private static void CarveLargeCavernEllipse(TileData[,] grid, int[] surfaceHeights,
            WorldGenerationConfig config, bool[,] protectedAir,
            int centerX, int centerY, float halfW, float halfH, float noiseOx, float noiseOy)
        {
            var width = config.MapWidth;
            var crust = Mathf.Max(1, config.CaveSurfaceCrustThickness);
            var minX = Mathf.Max(0, Mathf.FloorToInt(centerX - halfW) - 1);
            var maxX = Mathf.Min(width - 1, Mathf.CeilToInt(centerX + halfW) + 1);
            var minY = Mathf.Max(config.BedrockThickness, Mathf.FloorToInt(centerY - halfH) - 1);
            var maxY = Mathf.Min(config.MapHeight - 1, Mathf.CeilToInt(centerY + halfH) + 1);
            var invHalfW = halfW > 0.001f ? 1f / halfW : 0f;
            var invHalfH = halfH > 0.001f ? 1f / halfH : 0f;

            for (var x = minX; x <= maxX; x++)
            {
                var surfaceY = surfaceHeights[x];
                var carveMaxY = surfaceY - crust; // 포함 상한
                for (var y = minY; y <= maxY; y++)
                {
                    if (y > carveMaxY || y < config.BedrockThickness) continue;
                    if (IsProtectedAirCell(x, y, protectedAir)) continue;

                    var existing = grid[x, y];
                    if (ShouldPreserveSpecialAir(existing)) continue;
                    if (string.Equals(existing.elementType, WorldTileTypes.IceAltar, StringComparison.Ordinal) ||
                        string.Equals(existing.elementType, WorldTileTypes.RuinWall, StringComparison.Ordinal) ||
                        string.Equals(existing.elementType, WorldTileTypes.Bedrock, StringComparison.Ordinal))
                        continue;

                    var nx = (x - centerX) * invHalfW;
                    var ny = (y - centerY) * invHalfH;
                    var edgeNoise = Mathf.PerlinNoise((x + noiseOx) * 0.11f, (y + noiseOy) * 0.11f);
                    var radius = 1f - 0.07f * edgeNoise;
                    if (nx * nx + ny * ny > radius * radius) continue;

                    grid[x, y] = TileData.CreateAir();
                }
            }
        }

        /// <summary>
        /// v27 후처리 — Pass 4·4b(구조물·연결 통로) 이후 최후반 Hard Fill/Hard Cut.
        /// protectedAir: 스폰 발밑 3×3·제단 최소만. Hard Cut은 span&gt;cave_max_height 시 protectedAir 무시.
        /// </summary>
        private static void PostProcessCaveCavities(TileData[,] grid, int[] surfaceHeights,
            WorldGenerationConfig config, bool[,] protectedAir, int boundaryHardness)
        {
            var width = config.MapWidth;
            var maxChamberHeight = Mathf.Max(1, config.CaveMaxHeight);
            var crust = Mathf.Max(1, config.CaveSurfaceCrustThickness);

            var visited = new bool[width, config.MapHeight];
            var queueX = new int[width * config.MapHeight];
            var queueY = new int[width * config.MapHeight];
            var compX = new int[width * config.MapHeight];
            var compY = new int[width * config.MapHeight];

            HardFillTopSafetyZone(grid, surfaceHeights, config, crust, protectedAir, boundaryHardness);
            HardFillPreferredSpawnSafetyZone(grid, surfaceHeights, config, crust, protectedAir,
                boundaryHardness);

            const int maxCutPasses = 128;
            for (var pass = 0; pass < maxCutPasses; pass++)
            {
                ClearVisited(visited, width, config.MapHeight);
                var cutAny = false;

                for (var x = 0; x < width; x++)
                {
                    var surfaceY = surfaceHeights[x];
                    for (var y = config.BedrockThickness; y <= surfaceY; y++)
                    {
                        if (!grid[x, y].IsAir || visited[x, y]) continue;
                        if (ShouldPreserveSpecialAir(grid[x, y])) continue;

                        var head = 0;
                        var tail = 0;
                        queueX[tail] = x;
                        queueY[tail] = y;
                        tail++;
                        visited[x, y] = true;

                        var compCount = 0;
                        var minX = x;
                        var maxX = x;
                        var minY = y;
                        var maxY = y;

                        while (head < tail)
                        {
                            var cx = queueX[head];
                            var cy = queueY[head];
                            head++;

                            compX[compCount] = cx;
                            compY[compCount] = cy;
                            compCount++;
                            if (cx < minX) minX = cx;
                            if (cx > maxX) maxX = cx;
                            if (cy < minY) minY = cy;
                            if (cy > maxY) maxY = cy;

                            for (var n = 0; n < EightNeighbors.Length; n++)
                            {
                                var nx = cx + EightNeighbors[n].x;
                                var ny = cy + EightNeighbors[n].y;
                                if ((uint)nx >= (uint)width || (uint)ny >= (uint)config.MapHeight) continue;
                                if (ny < config.BedrockThickness || ny > surfaceHeights[nx]) continue;
                                if (!grid[nx, ny].IsAir || visited[nx, ny]) continue;
                                if (ShouldPreserveSpecialAir(grid[nx, ny])) continue;

                                visited[nx, ny] = true;
                                queueX[tail] = nx;
                                queueY[tail] = ny;
                                tail++;
                            }
                        }

                        var span = maxY - minY + 1;
                        if (span <= maxChamberHeight) continue;

                        cutAny |= SealOversizedCavityComponent(grid, surfaceHeights, config, protectedAir,
                            minX, maxX, minY, maxY, maxChamberHeight, compCount, compX, compY,
                            boundaryHardness);
                    }
                }

                if (!cutAny) break;
            }

            HardFillTopSafetyZone(grid, surfaceHeights, config, crust, protectedAir, boundaryHardness);
            HardFillPreferredSpawnSafetyZone(grid, surfaceHeights, config, crust, protectedAir,
                boundaryHardness);
        }

        /// <summary>span &gt; cave_max_height 공동 — Y_mid 허리 밴드+3×3 봉인 후 하부 12칸만 유지.</summary>
        private static bool SealOversizedCavityComponent(TileData[,] grid, int[] surfaceHeights,
            WorldGenerationConfig config, bool[,] protectedAir,
            int minX, int maxX, int minY, int maxY, int maxChamberHeight,
            int compCount, int[] compX, int[] compY, int boundaryHardness)
        {
            var cutAny = false;
            var yMid = (minY + maxY) / 2;

            // 1) Y_mid 가로 밴드: 공동 x범위 전체 + 3×3 주변 봉인(대각선 우회 차단).
            for (var bx = minX; bx <= maxX; bx++)
            {
                if (!InBounds(bx, yMid, config.MapWidth, config.MapHeight)) continue;
                if (yMid > surfaceHeights[bx]) continue;
                if (ForceSealCavityAirCell(grid, surfaceHeights, bx, yMid, config, boundaryHardness))
                    cutAny = true;
                ForceSealNeighborhood3x3(grid, surfaceHeights, bx, yMid, config, boundaryHardness);
            }

            // 2) 공동 성분 칸 중 yMid 행 — 밴드 밖 x에 남은 허리도 메움.
            for (var i = 0; i < compCount; i++)
            {
                if (compY[i] != yMid) continue;
                if (ForceSealCavityAirCell(grid, surfaceHeights, compX[i], yMid, config,
                        boundaryHardness))
                    cutAny = true;
                ForceSealNeighborhood3x3(grid, surfaceHeights, compX[i], yMid, config,
                    boundaryHardness);
            }

            // 3) 하부 maxChamberHeight 유지 — 그 위 잔여 공기 제거(protectedAir·구역 보호 무시).
            var keepMaxY = minY + maxChamberHeight - 1;
            for (var i = 0; i < compCount; i++)
            {
                var cx = compX[i];
                var cy = compY[i];
                if (cy <= keepMaxY) continue;
                if (ForceSealCavityAirCell(grid, surfaceHeights, cx, cy, config, boundaryHardness))
                    cutAny = true;
                ForceSealNeighborhood3x3(grid, surfaceHeights, cx, cy, config, boundaryHardness);
            }

            return cutAny;
        }

        /// <summary>공식 보호 칸(스폰 발밑 3×3·제단 주변)만 Hard Fill 예외. 긴 수직 통로는 보호하지 않음.</summary>
        private static bool IsProtectedAirCell(int x, int y, bool[,] protectedAir) =>
            protectedAir != null && InBounds(x, y, protectedAir.GetLength(0), protectedAir.GetLength(1)) &&
            protectedAir[x, y];

        private static bool ShouldPreserveSpecialAir(in TileData tile)
        {
            if (!tile.IsAir) return false;
            return string.Equals(tile.elementType, WorldTileTypes.IceLake, StringComparison.Ordinal) ||
                   string.Equals(tile.elementType, WorldTileTypes.IceAltar, StringComparison.Ordinal);
        }

        /// <summary>Hard Cut 전용 — 얼음 호수/제단만 제외, protectedAir는 span 위반 시 무시.</summary>
        private static bool ForceSealCavityAirCell(TileData[,] grid, int[] surfaceHeights, int x, int y,
            WorldGenerationConfig config, int boundaryHardness)
        {
            if (!InBounds(x, y, config.MapWidth, config.MapHeight)) return false;
            if (!grid[x, y].IsAir) return false;
            if (ShouldPreserveSpecialAir(grid[x, y])) return false;

            SealCaveCellWithRock(grid, x, y, surfaceHeights[x], config, boundaryHardness);
            return true;
        }

        private static void ForceSealNeighborhood3x3(TileData[,] grid, int[] surfaceHeights, int centerX, int centerY,
            WorldGenerationConfig config, int boundaryHardness)
        {
            for (var dx = -1; dx <= 1; dx++)
            for (var dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                ForceSealCavityAirCell(grid, surfaceHeights, centerX + dx, centerY + dy, config,
                    boundaryHardness);
            }
        }

        private static bool TrySealCavityAirCell(TileData[,] grid, int[] surfaceHeights, int x, int y,
            WorldGenerationConfig config, bool[,] protectedAir, int boundaryHardness)
        {
            if (!InBounds(x, y, config.MapWidth, config.MapHeight)) return false;
            if (!grid[x, y].IsAir) return false;
            if (ShouldPreserveSpecialAir(grid[x, y])) return false;
            if (IsProtectedAirCell(x, y, protectedAir)) return false;

            SealCaveCellWithRock(grid, x, y, surfaceHeights[x], config, boundaryHardness);
            return true;
        }

        private static void SealNeighborhood3x3(TileData[,] grid, int[] surfaceHeights, int centerX, int centerY,
            WorldGenerationConfig config, bool[,] protectedAir, int boundaryHardness)
        {
            for (var dx = -1; dx <= 1; dx++)
            for (var dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                TrySealCavityAirCell(grid, surfaceHeights, centerX + dx, centerY + dy, config, protectedAir,
                    boundaryHardness);
            }
        }

        private static void HardFillTopSafetyZone(TileData[,] grid, int[] surfaceHeights,
            WorldGenerationConfig config, int crust, bool[,] protectedAir, int boundaryHardness)
        {
            var width = config.MapWidth;
            var height = config.MapHeight;
            var absoluteSurfaceBandMinY = Mathf.Max(0, height - 20);

            for (var x = 0; x < width; x++)
            {
                var surfaceY = surfaceHeights[x];
                var sealFrom = Mathf.Max(config.BedrockThickness, surfaceY - crust + 1);
                if (absoluteSurfaceBandMinY <= surfaceY)
                    sealFrom = Mathf.Min(sealFrom, Mathf.Max(config.BedrockThickness, absoluteSurfaceBandMinY));

                for (var y = sealFrom; y <= surfaceY; y++)
                {
                    if (!InBounds(x, y, width, height)) continue;
                    if (IsProtectedAirCell(x, y, protectedAir)) continue;
                    if (ShouldPreserveSpecialAir(grid[x, y])) continue;
                    if (!grid[x, y].IsAir) continue;
                    SealCaveCellWithRock(grid, x, y, surfaceY, config, boundaryHardness);
                }
            }
        }

        /// <summary>스폰 근처는 지표 crust만 메움. 보호 칸(발밑 3×3)만 예외.</summary>
        private static void HardFillPreferredSpawnSafetyZone(TileData[,] grid, int[] surfaceHeights,
            WorldGenerationConfig config, int crust, bool[,] protectedAir, int boundaryHardness)
        {
            var width = config.MapWidth;
            var preferredX = Mathf.Clamp(Mathf.RoundToInt(width * config.SpawnColumnRatio), 3, width - 4);

            for (var x = preferredX - 2; x <= preferredX + 2; x++)
            {
                if (x < 0 || x >= width) continue;
                var surfaceY = surfaceHeights[x];
                var sealFrom = Mathf.Max(config.BedrockThickness, surfaceY - crust + 1);
                for (var y = sealFrom; y <= surfaceY; y++)
                {
                    if (IsProtectedAirCell(x, y, protectedAir)) continue;
                    if (ShouldPreserveSpecialAir(grid[x, y])) continue;
                    if (!grid[x, y].IsAir) continue;
                    SealCaveCellWithRock(grid, x, y, surfaceY, config, boundaryHardness);
                }
            }
        }

        private static void ClearVisited(bool[,] visited, int width, int height)
        {
            for (var x = 0; x < width; x++)
            for (var y = 0; y < height; y++)
                visited[x, y] = false;
        }

        /// <summary>공동 단절용 — 해당 칸을 지층에 맞는 자연 전경+배경으로 복구.</summary>
        private static void SealCaveCellWithRock(TileData[,] grid, int x, int y, int surfaceY,
            WorldGenerationConfig config, int boundaryHardness)
        {
            var layer = ClassifyLayer(y, surfaceY, config);
            grid[x, y] = layer switch
            {
                WorldLayer.Upper => TileData.CreateNaturalWithBackground(
                    WorldTileTypes.Dirt, 1, WorldTileTypes.BackgroundDirt),
                WorldLayer.Middle => TileData.CreateNaturalWithBackground(
                    WorldTileTypes.StoneMid, 2, WorldTileTypes.BackgroundStone),
                WorldLayer.Deep => TileData.CreateNaturalWithBackground(
                    WorldTileTypes.StoneDeep, boundaryHardness, WorldTileTypes.BackgroundDeep),
                WorldLayer.Bedrock => TileData.CreateNaturalWithBackground(
                    WorldTileTypes.Bedrock, 3, WorldTileTypes.BackgroundDeep),
                _ => TileData.CreateNaturalWithBackground(
                    WorldTileTypes.Dirt, 1, WorldTileTypes.BackgroundDirt)
            };
        }

        private static string BackgroundElementFor(WorldLayer layer) => layer switch
        {
            WorldLayer.Upper => WorldTileTypes.BackgroundDirt,
            WorldLayer.Middle => WorldTileTypes.BackgroundStone,
            _ => WorldTileTypes.BackgroundDeep
        };

        /// <summary>
        /// 생성 과정에서 뚫린 지하 공기 칸에 지층별 자연 배경을 채운다.
        /// 기존 배경과 지상 공기는 유지하며, 채굴 후 남는 자연 배경과 같은 데이터를 사용한다.
        /// </summary>
        private static void PopulateNaturalCaveBackgrounds(TileData[,] grid, int[] surfaceHeights,
            WorldGenerationConfig config)
        {
            var width = grid.GetLength(0);
            var height = grid.GetLength(1);

            for (var x = 0; x < width; x++)
            {
                var surfaceY = surfaceHeights[x];
                for (var y = config.BedrockThickness; y < height; y++)
                {
                    var tile = grid[x, y];
                    if (!tile.IsAir || tile.HasBackground || tile.HasNaturalBackground)
                        continue;

                    var layer = ClassifyLayer(y, surfaceY, config);
                    if (layer == WorldLayer.Surface || layer == WorldLayer.Bedrock)
                        continue;

                    grid[x, y] = TileData.CreateCaveAir(BackgroundElementFor(layer));
                }
            }
        }

        /// <summary>
        /// 연결 통로(Pass 4b) 굴착 금지. 다음이면 절대 Air로 파지 않는다.
        /// 1) 절대 깊이 상한 y ≥ ConnectivityDeepMaxYExclusive(15) — 심층만 허용
        /// 2) globals surface_y=20 밴드(y ≥ height-20)
        /// 3) 열별 지표 crust(surfaceY - crust + 1 이상)
        /// </summary>
        private const int ConnectivityDeepMaxYExclusive = 15;

        private static int GetConnectivityDigForbiddenMinY(int x, int[] surfaceHeights, WorldGenerationConfig config)
        {
            var width = surfaceHeights.Length;
            var surfaceY = surfaceHeights[Mathf.Clamp(x, 0, width - 1)];
            var crust = Mathf.Max(1, config.CaveSurfaceCrustThickness);
            var crustFloor = surfaceY - crust + 1;
            var absoluteSkyBandFloor = Mathf.Max(config.BedrockThickness, config.MapHeight - 20);

            // 가장 엄격(가장 낮은) 금지 시작 y — 그 이상은 전부 금지.
            return Mathf.Min(ConnectivityDeepMaxYExclusive, crustFloor, absoluteSkyBandFloor);
        }

        private static bool IsConnectivityDigForbidden(int x, int y, int[] surfaceHeights, WorldGenerationConfig config)
        {
            if (y < config.BedrockThickness) return true;
            if (y >= ConnectivityDeepMaxYExclusive) return true;
            var width = surfaceHeights.Length;
            if (x < 0 || x >= width) return true;
            if (y > surfaceHeights[x]) return true;
            return y >= GetConnectivityDigForbiddenMinY(x, surfaceHeights, config);
        }

        /// <summary>
        /// Pass 4b — 심층(y&lt;15) 제단 접근만. 긴 일자 수평 Bridge / 장거리 NarrowTunnel 금지.
        /// 제단 주위 3×3 공기 + 기존 심층 공동까지 맨해튼 ≤5 최소 연결(또는 미세 지그재그).
        /// </summary>
        private static void CarveConnectivityShafts(TileData[,] grid, int[] surfaceHeights, System.Random rng,
            WorldGenerationConfig config, Vector2Int spawnPoint, Vector2Int altarPosition)
        {
            _ = rng;
            _ = spawnPoint;
            var width = config.MapWidth;
            var deepFloor = config.BedrockThickness + 2;
            var deepCeiling = ConnectivityDeepMaxYExclusive - 1; // 14
            if (deepCeiling < deepFloor) return;

            // 1) 제단 주위 최소 공간(3×3) — 제단 타일 자체는 유지.
            CarveDeepAltarLocalPocket(grid, surfaceHeights, altarPosition, config);

            // 2) 심층 밴드 안 기존 공기 공동이 가까우면 ≤5타일만 천공해 합류.
            if (TryLinkAltarToNearestDeepAir(grid, surfaceHeights, altarPosition, deepFloor, deepCeiling,
                    config, maxDist: 5))
                return;

            // 3) 주변에 공동이 없으면 짧은 미세 지그재그(총 굴착 ≤5)로 국소 공간만 확보.
            CarveDeepAltarMicroZigzag(grid, surfaceHeights, altarPosition, deepFloor, deepCeiling, config);
        }

        private static void CarveDeepAltarLocalPocket(TileData[,] grid, int[] surfaceHeights,
            Vector2Int altarPosition, WorldGenerationConfig config)
        {
            var width = config.MapWidth;
            var height = config.MapHeight;
            for (var dx = -1; dx <= 1; dx++)
            for (var dy = -1; dy <= 1; dy++)
            {
                var x = altarPosition.x + dx;
                var y = altarPosition.y + dy;
                if ((uint)x >= (uint)width || (uint)y >= (uint)height) continue;
                if (y >= ConnectivityDeepMaxYExclusive) continue;
                if (IsConnectivityDigForbidden(x, y, surfaceHeights, config)) continue;
                if (string.Equals(grid[x, y].elementType, WorldTileTypes.IceAltar, StringComparison.Ordinal))
                    continue;
                grid[x, y] = TileData.CreateAir();
            }
        }

        private static bool TryLinkAltarToNearestDeepAir(TileData[,] grid, int[] surfaceHeights,
            Vector2Int altarPosition, int deepFloor, int deepCeiling, WorldGenerationConfig config, int maxDist)
        {
            var width = config.MapWidth;
            var height = config.MapHeight;

            // 제단 인접 공기(방금 판 포켓 포함)를 출발점으로.
            var starts = new List<Vector2Int>(8);
            foreach (var offset in FourNeighbors)
            {
                var p = altarPosition + offset;
                if (!InBounds(p, width, height)) continue;
                if (!grid[p.x, p.y].IsAir) continue;
                if (p.y < deepFloor || p.y > deepCeiling) continue;
                starts.Add(p);
            }
            if (starts.Count == 0) return false;

            Vector2Int bestFrom = default;
            Vector2Int bestTo = default;
            var bestDist = int.MaxValue;

            for (var x = altarPosition.x - maxDist - 2; x <= altarPosition.x + maxDist + 2; x++)
            for (var y = deepFloor; y <= deepCeiling; y++)
            {
                if (!InBounds(x, y, width, height)) continue;
                if (!grid[x, y].IsAir) continue;
                if (IsConnectivityDigForbidden(x, y, surfaceHeights, config)) continue;
                // 이미 제단 포켓에 속한 공기는 스킵(다른 공동만).
                if (Mathf.Abs(x - altarPosition.x) <= 1 && Mathf.Abs(y - altarPosition.y) <= 1) continue;

                foreach (var from in starts)
                {
                    var dist = Mathf.Abs(x - from.x) + Mathf.Abs(y - from.y);
                    if (dist < 2 || dist > maxDist || dist >= bestDist) continue;
                    bestDist = dist;
                    bestFrom = from;
                    bestTo = new Vector2Int(x, y);
                }
            }

            if (bestDist == int.MaxValue) return false;
            CarveConnectivityShortZigzag(grid, surfaceHeights, bestFrom, bestTo, config);
            return true;
        }

        /// <summary>맨해튼 ≤5 구간을 1~2칸마다 y를 ±1로 흔들며 연결 — 긴 일자 수평 Line 금지.</summary>
        private static void CarveConnectivityShortZigzag(TileData[,] grid, int[] surfaceHeights,
            Vector2Int from, Vector2Int to, WorldGenerationConfig config)
        {
            var width = config.MapWidth;
            var current = from;
            CarveConnectivityTunnelCell(grid, surfaceHeights, current, config);
            var step = 0;
            var guard = 0;
            var limit = Mathf.Abs(to.x - from.x) + Mathf.Abs(to.y - from.y) + 8;

            while ((current.x != to.x || current.y != to.y) && guard++ < limit)
            {
                var dx = to.x - current.x;
                var dy = to.y - current.y;
                Vector2Int next;

                if (dx != 0)
                {
                    // 가로 진행 중 2칸마다 목표 y 쪽으로 1칸 꺾기(일자 흉터 완화).
                    if (step > 0 && step % 2 == 0 && dy != 0)
                        next = new Vector2Int(current.x, current.y + Math.Sign(dy));
                    else
                        next = new Vector2Int(current.x + Math.Sign(dx), current.y);
                }
                else if (dy != 0)
                {
                    next = new Vector2Int(current.x, current.y + Math.Sign(dy));
                }
                else break;

                if (IsConnectivityDigForbidden(next.x, next.y, surfaceHeights, config) ||
                    !InBounds(next, width, config.MapHeight))
                {
                    // 막히면 순수 축정렬로 폴백.
                    if (dx != 0)
                        next = new Vector2Int(current.x + Math.Sign(dx), current.y);
                    else
                        next = new Vector2Int(current.x, current.y + Math.Sign(dy));
                    if (IsConnectivityDigForbidden(next.x, next.y, surfaceHeights, config))
                        break;
                }

                current = next;
                CarveConnectivityTunnelCell(grid, surfaceHeights, current, config);
                step++;
            }
        }

        private static void CarveDeepAltarMicroZigzag(TileData[,] grid, int[] surfaceHeights,
            Vector2Int altarPosition, int deepFloor, int deepCeiling, WorldGenerationConfig config)
        {
            var width = config.MapWidth;
            // 제단에서 왼쪽/오른쪽으로 최대 5칸, 2칸마다 y ±1 흔들기.
            var dir = altarPosition.x > width / 2 ? -1 : 1;
            var y = Mathf.Clamp(altarPosition.y, deepFloor, deepCeiling);
            var x = altarPosition.x;
            for (var i = 1; i <= 5; i++)
            {
                x = Mathf.Clamp(x + dir, 2, width - 3);
                if (i % 2 == 0)
                {
                    var ny = Mathf.Clamp(y + ((i & 2) == 0 ? 1 : -1), deepFloor, deepCeiling);
                    if (!IsConnectivityDigForbidden(x, ny, surfaceHeights, config))
                        y = ny;
                }
                if (IsConnectivityDigForbidden(x, y, surfaceHeights, config)) break;
                if (string.Equals(grid[x, y].elementType, WorldTileTypes.IceAltar, StringComparison.Ordinal))
                    continue;
                grid[x, y] = TileData.CreateAir();
            }
        }

        /// <summary>
        /// v27 도달성 — 지상→심층 인공 계단/사선/미앤더 Carve 전면 금지.
        /// 1) 공식 얕은 입구 + 옆 열 사이드스텝만 (crust 미침범)
        /// 2) 입구 직하 플러그로 직통 샤프트 차단
        /// 3) 지하(crust 아래) 기존 공동끼리 맨해튼 ≤3 벽만 최소 천공
        /// 4) 심층 제단 인접 공기만 확보 (Pass 4b 심층 통로와 연계)
        /// </summary>
        private static void EnsureReachabilityByCavityBridges(TileData[,] grid, int[] surfaceHeights,
            Vector2Int spawnPoint, Vector2Int altarPosition, WorldGenerationConfig config)
        {
            var width = config.MapWidth;
            var height = config.MapHeight;
            var entranceX = Mathf.Clamp(spawnPoint.x - 1, 1, width - 2);
            var spawnX = spawnPoint.x;
            var deepFloor = config.BedrockThickness + 2;
            var digGuard = ReachabilityDigGuard.Create(entranceX, spawnX, surfaceHeights, config);
            SealEntranceColumnPlugs(grid, digGuard, config);

            // 공식 입구 바닥 → 옆 열로만 1~3칸 사이드스텝 (아래로 뚫지 않음).
            var sideX = Mathf.Clamp(entranceX + 2, 3, width - 4);
            if (sideX == entranceX || sideX == spawnX)
                sideX = Mathf.Clamp(entranceX - 2, 3, width - 4);

            var deckY = digGuard.EntranceOfficialBottom;
            if (deckY < deepFloor) deckY = deepFloor;
            var sideAnchor = CarveHorizontalReachabilityLink(grid, digGuard, entranceX, sideX, deckY,
                deepFloor, config);

            // 얕은 입구 바닥에서 2~3타일 안의 기존 공동만 최소 연결.
            TryLinkSideAnchorToNearestAir(grid, digGuard, sideAnchor, spawnPoint, config, maxDist: 3);

            // 지하 공동 간 얇은 벽(≤3)만 반복 천공 — 맨땅 장거리 Carve 없음.
            ConnectUndergroundCavitiesWithMinimalBridges(grid, digGuard, config, maxGap: 3, maxPasses: 400);

            // 심층 제단 인접 공기(y&lt;15) — 지표에서 내려오는 통로가 아님.
            foreach (var offset in FourNeighbors)
            {
                var cell = altarPosition + offset;
                if ((uint)cell.x >= (uint)width || (uint)cell.y >= (uint)height) continue;
                if (cell.y >= ConnectivityDeepMaxYExclusive) continue;
                if (digGuard.Forbidden(cell.x, cell.y)) continue;
                if (string.Equals(grid[cell.x, cell.y].elementType, WorldTileTypes.IceAltar, StringComparison.Ordinal))
                    continue;
                grid[cell.x, cell.y] = TileData.CreateAir();
            }
        }

        /// <summary>
        /// crust 아래 공기 공동을 라벨링한 뒤, 서로 다른 공동이 맨해튼 ≤ maxGap 이면
        /// 그 사이 암석만 L자로 최소 천공한다. 지상→심층 경로를 새로 만들지 않는다.
        /// </summary>
        private static void ConnectUndergroundCavitiesWithMinimalBridges(TileData[,] grid,
            ReachabilityDigGuard digGuard, WorldGenerationConfig config, int maxGap, int maxPasses)
        {
            for (var pass = 0; pass < maxPasses; pass++)
            {
                if (!TryPunchClosestCavityWall(grid, digGuard, config, maxGap))
                    break;
            }
        }

        private static bool TryPunchClosestCavityWall(TileData[,] grid, ReachabilityDigGuard digGuard,
            WorldGenerationConfig config, int maxGap)
        {
            var width = config.MapWidth;
            var height = config.MapHeight;
            var labels = new int[width, height];
            var airCells = new List<Vector2Int>(4096);
            var labelCount = 0;

            for (var x = 0; x < width; x++)
            {
                var surfaceY = digGuard.SurfaceHeights[x];
                for (var y = config.BedrockThickness; y <= surfaceY; y++)
                {
                    if (!grid[x, y].IsAir) continue;
                    // crust/지표 밴드 공기는 공동 네트워크에서 제외 (공식 입구와 분리).
                    if (IsSurfaceOrCrustDigForbidden(x, y, digGuard.SurfaceHeights, config)) continue;
                    if (labels[x, y] != 0) continue;

                    labelCount++;
                    var label = labelCount;
                    var q = new Queue<Vector2Int>();
                    var start = new Vector2Int(x, y);
                    q.Enqueue(start);
                    labels[x, y] = label;
                    airCells.Add(start);

                    while (q.Count > 0)
                    {
                        var c = q.Dequeue();
                        foreach (var offset in FourNeighbors)
                        {
                            var n = c + offset;
                            if (!InBounds(n, width, height)) continue;
                            if (labels[n.x, n.y] != 0) continue;
                            if (!grid[n.x, n.y].IsAir) continue;
                            if (IsSurfaceOrCrustDigForbidden(n.x, n.y, digGuard.SurfaceHeights, config)) continue;
                            labels[n.x, n.y] = label;
                            q.Enqueue(n);
                            airCells.Add(n);
                        }
                    }
                }
            }

            if (labelCount < 2) return false;

            Vector2Int bestFrom = default;
            Vector2Int bestTo = default;
            var bestScore = int.MaxValue;
            var found = false;

            // 각 공기 칸 주변 맨해튼 1..maxGap 에서 다른 라벨 공기 탐색.
            for (var i = 0; i < airCells.Count; i++)
            {
                var from = airCells[i];
                var fromLabel = labels[from.x, from.y];
                for (var dx = -maxGap; dx <= maxGap; dx++)
                for (var dy = -maxGap; dy <= maxGap; dy++)
                {
                    var dist = Mathf.Abs(dx) + Mathf.Abs(dy);
                    if (dist < 2 || dist > maxGap) continue; // 1이면 이미 같은 4연결 성분
                    var to = new Vector2Int(from.x + dx, from.y + dy);
                    if (!InBounds(to, width, height)) continue;
                    if (!grid[to.x, to.y].IsAir) continue;
                    if (IsSurfaceOrCrustDigForbidden(to.x, to.y, digGuard.SurfaceHeights, config)) continue;
                    var toLabel = labels[to.x, to.y];
                    if (toLabel == 0 || toLabel == fromLabel) continue;

                    // 가로 연결 우선(같은 점수면 짧은 것). 수직 장거리 흉터 억제.
                    var score = dist * 1000 + Mathf.Abs(dy) * 10 + Mathf.Abs(from.y + to.y);
                    if (score >= bestScore) continue;
                    // 경로상 굴착 칸이 digGuard에 막히면 스킵.
                    if (!CanCarveMinimalLBridge(digGuard, from, to)) continue;
                    bestScore = score;
                    bestFrom = from;
                    bestTo = to;
                    found = true;
                }
            }

            if (!found) return false;
            CarveMinimalWallBridge(grid, digGuard, bestFrom, bestTo, config);
            return true;
        }

        private static bool CanCarveMinimalLBridge(ReachabilityDigGuard digGuard, Vector2Int from, Vector2Int to)
        {
            var cornerA = new Vector2Int(to.x, from.y);
            var cornerB = new Vector2Int(from.x, to.y);
            if (!digGuard.Forbidden(cornerA.x, cornerA.y))
            {
                return !digGuard.Forbidden(from.x, from.y) && !digGuard.Forbidden(to.x, to.y);
            }
            return !digGuard.Forbidden(cornerB.x, cornerB.y) &&
                   !digGuard.Forbidden(from.x, from.y) && !digGuard.Forbidden(to.x, to.y);
        }

        /// <summary>공동 사이 얇은 벽만 판다. 헤드룸 확장 없이 경로 칸만 Air.</summary>
        private static void CarveMinimalWallBridge(TileData[,] grid, ReachabilityDigGuard digGuard,
            Vector2Int from, Vector2Int to, WorldGenerationConfig config)
        {
            var cornerA = new Vector2Int(to.x, from.y);
            var preferA = !digGuard.Forbidden(cornerA.x, cornerA.y);
            if (preferA)
            {
                CarveMinimalAxis(grid, digGuard, from, cornerA, config);
                CarveMinimalAxis(grid, digGuard, cornerA, to, config);
            }
            else
            {
                var cornerB = new Vector2Int(from.x, to.y);
                CarveMinimalAxis(grid, digGuard, from, cornerB, config);
                CarveMinimalAxis(grid, digGuard, cornerB, to, config);
            }
        }

        private static void CarveMinimalAxis(TileData[,] grid, ReachabilityDigGuard digGuard,
            Vector2Int from, Vector2Int to, WorldGenerationConfig config)
        {
            var current = from;
            CarveMinimalCell(grid, digGuard, current, config);
            var guard = 0;
            var limit = config.MapWidth + config.MapHeight;
            while ((current.x != to.x || current.y != to.y) && guard++ < limit)
            {
                var dx = to.x - current.x;
                var dy = to.y - current.y;
                if (dx != 0)
                    current = new Vector2Int(current.x + Math.Sign(dx), current.y);
                else if (dy != 0)
                    current = new Vector2Int(current.x, current.y + Math.Sign(dy));
                else break;
                if (digGuard.Forbidden(current.x, current.y)) break;
                CarveMinimalCell(grid, digGuard, current, config);
            }
        }

        private static void CarveMinimalCell(TileData[,] grid, ReachabilityDigGuard digGuard,
            Vector2Int point, WorldGenerationConfig config)
        {
            if (digGuard.Forbidden(point.x, point.y)) return;
            if (!InBounds(point, config.MapWidth, config.MapHeight)) return;
            if (string.Equals(grid[point.x, point.y].elementType, WorldTileTypes.IceAltar, StringComparison.Ordinal))
                return;
            grid[point.x, point.y] = TileData.CreateAir();
        }

        private static void TryLinkSideAnchorToNearestAir(TileData[,] grid, ReachabilityDigGuard digGuard,
            Vector2Int sideAnchor, Vector2Int spawnPoint, WorldGenerationConfig config, int maxDist)
        {
            var width = config.MapWidth;
            var height = config.MapHeight;
            var reachable = FloodFillTraversable(grid, spawnPoint, width, height);

            Vector2Int best = default;
            var bestDist = int.MaxValue;
            for (var x = sideAnchor.x - maxDist; x <= sideAnchor.x + maxDist; x++)
            for (var y = sideAnchor.y - maxDist; y <= sideAnchor.y + maxDist; y++)
            {
                if (!InBounds(x, y, width, height)) continue;
                if (IsSurfaceOrCrustDigForbidden(x, y, digGuard.SurfaceHeights, config)) continue;
                if (digGuard.Forbidden(x, y)) continue;
                if (!grid[x, y].IsAir) continue;
                var p = new Vector2Int(x, y);
                if (reachable.Contains(p)) continue;
                var dist = Mathf.Abs(x - sideAnchor.x) + Mathf.Abs(y - sideAnchor.y);
                if (dist < 2 || dist > maxDist || dist >= bestDist) continue;
                if (!CanCarveMinimalLBridge(digGuard, sideAnchor, p)) continue;
                bestDist = dist;
                best = p;
            }

            if (bestDist == int.MaxValue) return;
            CarveMinimalWallBridge(grid, digGuard, sideAnchor, best, config);
        }

        /// <summary>도달성 굴착 가드 — crust/지표 밴드 + 공식 입구·스폰 열 하단 보호.</summary>
        private readonly struct ReachabilityDigGuard
        {
            public readonly int EntranceX;
            public readonly int SpawnX;
            public readonly int EntranceOfficialBottom;
            public readonly int SpawnOfficialBottom;
            public readonly int[] SurfaceHeights;
            public readonly WorldGenerationConfig Config;

            private ReachabilityDigGuard(int entranceX, int spawnX, int entranceOfficialBottom,
                int spawnOfficialBottom, int[] surfaceHeights, WorldGenerationConfig config)
            {
                EntranceX = entranceX;
                SpawnX = spawnX;
                EntranceOfficialBottom = entranceOfficialBottom;
                SpawnOfficialBottom = spawnOfficialBottom;
                SurfaceHeights = surfaceHeights;
                Config = config;
            }

            public static ReachabilityDigGuard Create(int entranceX, int spawnX, int[] surfaceHeights,
                WorldGenerationConfig config)
            {
                var entranceBottom = config.GetSpawnEntranceOfficialBottom(surfaceHeights[entranceX]);
                var spawnBottom = config.GetSpawnEntranceOfficialBottom(surfaceHeights[spawnX]);
                return new ReachabilityDigGuard(entranceX, spawnX, entranceBottom, spawnBottom,
                    surfaceHeights, config);
            }

            public bool Forbidden(int x, int y)
            {
                if (IsSurfaceOrCrustDigForbidden(x, y, SurfaceHeights, Config)) return true;
                // 입구/스폰 열은 공식 바닥 직하만 고체 플러그로 유지(연속 공기 run 차단).
                // 열 전체를 막으면 심층 제단이 같은 열에 있을 때 도달성이 영원히 실패한다.
                if (x == EntranceX && IsEntranceColumnPlug(y, EntranceOfficialBottom, Config))
                    return true;
                if (x == SpawnX && IsEntranceColumnPlug(y, SpawnOfficialBottom, Config))
                    return true;
                return false;
            }

            /// <summary>공식 입구 바닥 바로 아래 1~3칸 — 지표 관통 직통 assert용 플러그.</summary>
            private static bool IsEntranceColumnPlug(int y, int officialBottom, WorldGenerationConfig config)
            {
                if (y >= officialBottom) return false;
                var plugDepth = Mathf.Max(1, Mathf.Min(3, config.CaveMaxHeight));
                return y >= officialBottom - plugDepth;
            }
        }

        private static void SealEntranceColumnPlugs(TileData[,] grid, ReachabilityDigGuard digGuard,
            WorldGenerationConfig config)
        {
            SealColumnPlugRange(grid, digGuard.EntranceX, digGuard.EntranceOfficialBottom, config);
            if (digGuard.SpawnX != digGuard.EntranceX)
                SealColumnPlugRange(grid, digGuard.SpawnX, digGuard.SpawnOfficialBottom, config);
        }

        private static void SealColumnPlugRange(TileData[,] grid, int columnX, int officialBottom,
            WorldGenerationConfig config)
        {
            var plugDepth = Mathf.Max(1, Mathf.Min(3, config.CaveMaxHeight));
            for (var y = officialBottom - 1; y >= officialBottom - plugDepth; y--)
            {
                if (y < config.BedrockThickness) break;
                if (!InBounds(columnX, y, config.MapWidth, config.MapHeight)) continue;
                if (string.Equals(grid[columnX, y].elementType, WorldTileTypes.IceAltar, StringComparison.Ordinal))
                    continue;
                ForceNaturalWall(grid, columnX, y, config.MapWidth, config.MapHeight,
                    WorldTileTypes.Dirt, 1, WorldTileTypes.BackgroundDirt);
            }
        }

        private static Vector2Int CarveHorizontalReachabilityLink(TileData[,] grid, ReachabilityDigGuard digGuard,
            int fromX, int toX, int startY, int deepFloor, WorldGenerationConfig config)
        {
            var step = fromX <= toX ? 1 : -1;
            var y = startY;
            var last = new Vector2Int(fromX, startY);
            // 사이드스텝은 가로만. 열 높이 차로 막히면 아주 짧게만 하강.
            var maxDrop = Mathf.Max(2, config.CaveSurfaceCrustThickness + 2);
            for (var x = fromX; x != toX + step; x += step)
            {
                if (digGuard.Forbidden(x, y))
                {
                    var dropped = false;
                    var dropFloor = Mathf.Max(deepFloor, y - maxDrop);
                    for (var tryY = y - 1; tryY >= dropFloor; tryY--)
                    {
                        if (digGuard.Forbidden(x, tryY)) continue;
                        if (x != fromX)
                        {
                            var prevX = x - step;
                            CarveMinimalAxis(grid, digGuard, new Vector2Int(prevX, y),
                                new Vector2Int(prevX, tryY), config);
                        }
                        y = tryY;
                        dropped = true;
                        break;
                    }
                    if (!dropped) continue;
                }

                var cell = new Vector2Int(x, y);
                CarveMinimalCell(grid, digGuard, cell, config);
                last = cell;
            }

            return last;
        }

        /// <summary>스폰 반경 내 온보딩용 흙/돌이 부족하면 인접 자연 고체를 보정.</summary>
        private static void EnsureOnboardingResourcesNearSpawn(TileData[,] grid, int[] surfaceHeights,
            Vector2Int spawn, WorldGenerationConfig config)
        {
            var width = config.MapWidth;
            var height = config.MapHeight;
            var radius = config.OnboardingSearchRadius;
            var reachable = FloodFillOnboardingMineable(grid, spawn, width, height, radius);

            var dirt = 0;
            var stone = 0;
            CountOnboarding(grid, width, height, spawn, radius, reachable, ref dirt, ref stone);
            if (dirt >= config.OnboardingRequiredDirt && stone >= config.OnboardingRequiredStone)
                return;

            for (var ring = 1; ring <= radius && (dirt < config.OnboardingRequiredDirt || stone < config.OnboardingRequiredStone); ring++)
            {
                for (var dx = -ring; dx <= ring; dx++)
                {
                    for (var dy = -ring; dy <= ring; dy++)
                    {
                        if (Mathf.Abs(dx) != ring && Mathf.Abs(dy) != ring) continue;
                        var x = spawn.x + dx;
                        if (x < 0 || x >= width) continue;
                        var localSurfaceY = surfaceHeights[x];
                        var y = localSurfaceY + dy;
                        if (!InBounds(x, y, width, height)) continue;
                        if (y > localSurfaceY) continue;
                        var tile = grid[x, y];
                        if (tile.IsAir || !tile.isNaturalTerrain) continue;
                        if (!reachable.Contains(new Vector2Int(x, y))) continue;

                        if (dirt < config.OnboardingRequiredDirt)
                        {
                            grid[x, y] = TileData.CreateNaturalWithBackground(
                                WorldTileTypes.Dirt, 1, WorldTileTypes.BackgroundDirt);
                            dirt++;
                        }
                        else if (stone < config.OnboardingRequiredStone)
                        {
                            // 지상 노출 금지 — ban depth 아래에서만 온보딩 돌 보정.
                            if (IsInSurfaceMineralBan(y, localSurfaceY, config)) continue;
                            grid[x, y] = TileData.CreateNaturalWithBackground(
                                WorldTileTypes.Stone, 1, WorldTileTypes.BackgroundDirt);
                            stone++;
                        }
                    }
                }
            }
        }

        private static void CountOnboarding(TileData[,] grid, int width, int height, Vector2Int spawn,
            int radius, HashSet<Vector2Int> reachable, ref int dirt, ref int stone)
        {
            for (var x = spawn.x - radius; x <= spawn.x + radius; x++)
            for (var y = spawn.y - radius; y <= spawn.y + radius; y++)
            {
                var point = new Vector2Int(x, y);
                if (!InBounds(point, width, height)) continue;
                if (!reachable.Contains(point)) continue;
                var tile = grid[x, y];
                if (string.Equals(tile.elementType, WorldTileTypes.Dirt, StringComparison.Ordinal)) dirt++;
                else if (string.Equals(tile.elementType, WorldTileTypes.Stone, StringComparison.Ordinal)) stone++;
            }
        }

        private static bool IsSurfaceOrCrustDigForbidden(int x, int y, int[] surfaceHeights, WorldGenerationConfig config)
        {
            if (y < config.BedrockThickness) return true;
            var width = surfaceHeights.Length;
            if (x < 0 || x >= width) return true;
            if (y > surfaceHeights[x]) return true;
            var crust = Mathf.Max(1, config.CaveSurfaceCrustThickness);
            if (y >= surfaceHeights[x] - crust + 1) return true;
            if (y >= config.MapHeight - 20) return true;
            return false;
        }

        private static void CarveConnectivityTunnelCell(TileData[,] grid, int[] surfaceHeights, Vector2Int point,
            WorldGenerationConfig config)
        {
            if (IsConnectivityDigForbidden(point.x, point.y, surfaceHeights, config))
                return;

            var width = config.MapWidth;
            var height = config.MapHeight;
            if (!InBounds(point, width, height)) return;

            var existing = grid[point.x, point.y];
            if (string.Equals(existing.elementType, WorldTileTypes.IceAltar, StringComparison.Ordinal))
                return;

            grid[point.x, point.y] = TileData.CreateAir();
        }

        /// <summary>
        /// 좁은 계단 통로. 연속 수직 스텝 ≤ cave_max_height, 지표 crust 미개척.
        /// 이동은 목표 맨해튼 거리를 줄이는 결정론적 greedy(RNG 비의존)로 제단 도달을 보장한다.
        /// </summary>
        private static void CarveNarrowTunnel(TileData[,] grid, int[] surfaceHeights, Vector2Int from, Vector2Int to,
            System.Random rng, WorldGenerationConfig config, bool[,] protectedAir)
        {
            _ = rng; // 호출 시그니처 유지(패스별 RNG 주입). 경로는 결정론 greedy.
            var width = config.MapWidth;
            var maxVertRun = Mathf.Max(2, config.CaveMaxHeight);
            var verticalStreak = 0;
            var current = from;
            CarveWalkableColumn(grid, surfaceHeights, current, config, protectedAir);

            var guard = 0;
            var guardLimit = (config.MapWidth + config.MapHeight) * 8;
            while ((current.x != to.x || current.y != to.y) && guard++ < guardLimit)
            {
                var dx = to.x - current.x;
                var dy = to.y - current.y;

                var moveHorizontal = false;
                if (dx != 0 && dy == 0)
                    moveHorizontal = true;
                else if (dx != 0 && dy != 0)
                {
                    // 세로 연속 한도 또는 가로가 더 멀면 가로 우선.
                    if (verticalStreak >= maxVertRun || Mathf.Abs(dx) >= Mathf.Abs(dy))
                        moveHorizontal = true;
                }
                else if (dx == 0 && dy != 0 && verticalStreak >= maxVertRun)
                {
                    // 목표 x에 도착한 뒤에도 세로 한도면 잠깐 옆으로 비틀었다가 복귀.
                    var jogDir = (guard & 1) == 0 ? 1 : -1;
                    var jogX = Mathf.Clamp(current.x + jogDir, 1, width - 2);
                    current = new Vector2Int(jogX, current.y);
                    CarveWalkableColumn(grid, surfaceHeights, current, config, protectedAir);
                    verticalStreak = 0;
                    continue;
                }

                current += moveHorizontal ? new Vector2Int(Math.Sign(dx), 0) : new Vector2Int(0, Math.Sign(dy));
                if (moveHorizontal) verticalStreak = 0;
                else verticalStreak++;

                CarveWalkableColumn(grid, surfaceHeights, current, config, protectedAir);
            }
        }

        private static void CarveWalkableColumn(TileData[,] grid, int[] surfaceHeights, Vector2Int feet,
            WorldGenerationConfig config, bool[,] protectedAir)
        {
            CarveTunnelCell(grid, surfaceHeights, feet, config, protectedAir);
            CarveTunnelCell(grid, surfaceHeights, feet + new Vector2Int(0, 1), config, protectedAir);
        }

        /// <summary>레거시 +자 터널(연결 통로 미사용).</summary>
        private static void CarveTunnel(TileData[,] grid, int[] surfaceHeights, Vector2Int from, Vector2Int to, System.Random rng, WorldGenerationConfig config)
        {
            var current = from;
            CarveTunnelPlus(grid, surfaceHeights, current, config);

            while (current.x != to.x || current.y != to.y)
            {
                var dx = to.x - current.x;
                var dy = to.y - current.y;

                var moveHorizontal = dx != 0 && (dy == 0 || rng.NextDouble() < 0.5);
                current += moveHorizontal ? new Vector2Int(Math.Sign(dx), 0) : new Vector2Int(0, Math.Sign(dy));
                CarveTunnelPlus(grid, surfaceHeights, current, config);
            }
        }

        private static void CarveTunnelPlus(TileData[,] grid, int[] surfaceHeights, Vector2Int center, WorldGenerationConfig config)
        {
            CarveTunnelCell(grid, surfaceHeights, center, config, null);
            foreach (var offset in FourNeighbors)
                CarveTunnelCell(grid, surfaceHeights, center + offset, config, null);
        }

        private static void CarveTunnelCell(TileData[,] grid, int[] surfaceHeights, Vector2Int point,
            WorldGenerationConfig config, bool[,] protectedAir)
        {
            _ = protectedAir;
            // 레거시 경로도 지표/심층 상한 가드 공유 — 지표면 관통 재발 방지.
            if (IsConnectivityDigForbidden(point.x, point.y, surfaceHeights, config))
                return;

            var width = config.MapWidth;
            var height = config.MapHeight;
            if (!InBounds(point, width, height)) return;
            if (point.y < config.BedrockThickness) return;

            var existing = grid[point.x, point.y];
            if (string.Equals(existing.elementType, WorldTileTypes.IceAltar, StringComparison.Ordinal))
                return;

            grid[point.x, point.y] = TileData.CreateAir();
        }

        // ==============================================================
        // Pass 3 — 자원 (mineral-tiers.csv 빈도 → 클러스터 광맥, 3~6타일)
        // ==============================================================
        private static void PlaceOreVeins(TileData[,] grid, int[] surfaceHeights, System.Random rng,
            WorldGenerationConfig config, IReadOnlyDictionary<string, int> hardnessById,
            IReadOnlyList<OreVeinProfile> profiles)
        {
            var width = config.MapWidth;
            var height = config.MapHeight;

            foreach (var profile in profiles)
            {
                if (!hardnessById.TryGetValue(profile.elementType, out var hardness))
                    throw new InvalidOperationException($"광물 경도 정의 누락: '{profile.elementType}'.");
                var hasExplicitDepth = profile.depthMax > 0;
                var averageClusterSize = Mathf.Max(1f, (profile.minClusterSize + profile.maxClusterSize) * 0.5f);
                var layerAreaTiles = EstimateArea(profile, surfaceHeights, config);
                var veinCount = Mathf.Max(0, Mathf.RoundToInt(layerAreaTiles * profile.frequencyPer100Tiles / 100f / averageClusterSize));

                for (var i = 0; i < veinCount; i++)
                {
                    var x = rng.Next(0, width);
                    var (low, high) = hasExplicitDepth
                        ? GetDepthRange(x, profile.depthMin, profile.depthMax, surfaceHeights, config)
                        : GetLayerRange(x, profile.layer, surfaceHeights, config);
                    if (high < low) continue;

                    // 지표 근처 ban — 시드도 금지 깊이 아래에서만 고른다.
                    var banFloor = surfaceHeights[x] - config.SurfaceMineralBanDepth;
                    if (high > banFloor) high = banFloor;
                    if (high < low) continue;

                    var seedY = rng.Next(low, high + 1);
                    if (grid[x, seedY].IsAir) continue;
                    if (IsInSurfaceMineralBan(seedY, surfaceHeights[x], config)) continue;

                    GrowVeinCluster(grid, new Vector2Int(x, seedY), width, height, profile, hardness,
                        surfaceHeights, config, rng);
                }
            }
        }

        private static int EstimateArea(OreVeinProfile profile, int[] surfaceHeights, WorldGenerationConfig config)
        {
            var width = config.MapWidth;
            var hasExplicitDepth = profile.depthMax > 0;
            var total = 0;
            for (var x = 0; x < width; x++)
            {
                var (low, high) = hasExplicitDepth
                    ? GetDepthRange(x, profile.depthMin, profile.depthMax, surfaceHeights, config)
                    : GetLayerRange(x, profile.layer, surfaceHeights, config);
                if (high >= low) total += high - low + 1;
            }
            return total;
        }

        /// <summary>
        /// A-10 회귀 수정: 지표면 높이는 열(x)마다 펄린 변동(±6)으로 다르므로, 깊이 기반 y 범위(low/high)를
        /// 씨드 열에서 딱 한 번만 계산해 클러스터 전체에 그대로 쓰면 안 된다 — 랜덤워크가 다른 열로 옮겨가면
        /// 그 열의 실제 depth_min~depth_max 범위와 어긋나 광물이 자기 깊이 범위 밖에 배치될 수 있다(회귀
        /// 테스트가 실제로 잡아낸 사례). 그래서 매 스텝마다 "현재 x 기준" 범위를 다시 계산해 y를 그 범위로
        /// 즉시 clamp한 뒤에만 배치한다 — 통일 규칙(요구사항 5: 각 열의 지표로부터 내려간 깊이)을 그대로 지킨다.
        /// </summary>
        private static void GrowVeinCluster(TileData[,] grid, Vector2Int seed, int width, int height,
            OreVeinProfile profile, int hardness, int[] surfaceHeights, WorldGenerationConfig config,
            System.Random rng)
        {
            var hasExplicitDepth = profile.depthMax > 0;
            var size = rng.Next(profile.minClusterSize, profile.maxClusterSize + 1);
            var current = seed;
            // 같은 방향을 이어가기 쉽게 해 방울보다 광맥처럼 보이게 한다.
            var direction = FourNeighbors[rng.Next(FourNeighbors.Length)];

            for (var i = 0; i < size; i++)
            {
                var (low, high) = hasExplicitDepth
                    ? GetDepthRange(current.x, profile.depthMin, profile.depthMax, surfaceHeights, config)
                    : GetLayerRange(current.x, profile.layer, surfaceHeights, config);

                var banFloor = surfaceHeights[Mathf.Clamp(current.x, 0, surfaceHeights.Length - 1)] -
                               config.SurfaceMineralBanDepth;
                if (high > banFloor) high = banFloor;

                if (high >= low)
                {
                    current.y = Mathf.Clamp(current.y, low, high);
                    if (InBounds(current, width, height) &&
                        !grid[current.x, current.y].IsAir &&
                        !IsInSurfaceMineralBan(current.y, surfaceHeights[current.x], config))
                    {
                        var existing = grid[current.x, current.y];
                        var naturalBg = existing.HasNaturalBackground
                            ? existing.naturalBackgroundElementType
                            : BackgroundElementFor(profile.layer);
                        grid[current.x, current.y] = TileData.CreateNaturalWithBackground(
                            profile.elementType, hardness, naturalBg);
                    }
                }

                // ~70% 직진, 가끔만 꺾어 기다란 광맥 형태를 만든다.
                if (rng.NextDouble() >= 0.7)
                    direction = FourNeighbors[rng.Next(FourNeighbors.Length)];
                current += direction;
                current.x = Mathf.Clamp(current.x, 0, width - 1);
            }
        }

        // ==============================================================
        // Pass 4 — 구조물 (반지하 알코브 · 지상 폐허 · 심층 얼음호수+제단)
        // 상자는 Pass 4b2 대형 동굴 개척 이후 PlaceCavernChests에서 배치한다.
        // ==============================================================
        private struct StructurePlacement
        {
            public Vector2Int spawnPoint;
            public Vector2Int altarPosition;
            public List<ChestSpawnPoint> chests;
        }

        private static StructurePlacement PlaceStructures(TileData[,] grid, int[] surfaceHeights, System.Random rng,
            WorldGenerationConfig config, bool[,] protectedAir)
        {
            var occupied = new HashSet<Vector2Int>();

            var spawnPoint = PlaceSafeSurfaceSpawn(grid, surfaceHeights, config, occupied);
            PlaceRuins(grid, surfaceHeights, rng, config, occupied, spawnPoint);
            var altarPosition = PlaceDeepAltarAndLake(grid, surfaceHeights, rng, config, occupied);

            // protectedAir: 스폰 발밑 3×3 + 제단 주변 최소만. 입구/연결 통로 전체 마스킹 금지.
            MarkSpawnFootingProtectedAir(surfaceHeights, spawnPoint, config, protectedAir);
            MarkAltarProtectedAir(altarPosition, config, protectedAir);

            return new StructurePlacement
            {
                spawnPoint = spawnPoint,
                altarPosition = altarPosition,
                chests = new List<ChestSpawnPoint>()
            };
        }

        /// <summary>스폰 발밑(지표 고체) 중심 3×3만 Hard Fill 예외로 표시.</summary>
        private static void MarkSpawnFootingProtectedAir(int[] surfaceHeights, Vector2Int spawn,
            WorldGenerationConfig config, bool[,] protectedAir)
        {
            if (protectedAir == null) return;
            var width = config.MapWidth;
            var height = config.MapHeight;
            var surfaceY = surfaceHeights[Mathf.Clamp(spawn.x, 0, width - 1)];
            for (var dx = -1; dx <= 1; dx++)
            for (var dy = -1; dy <= 1; dy++)
            {
                var x = spawn.x + dx;
                var y = surfaceY + dy;
                if (!InBounds(x, y, width, height)) continue;
                protectedAir[x, y] = true;
            }
        }

        /// <summary>제단 2×2 + 1칸 링만 보호(호수/제단 자체는 ShouldPreserveSpecialAir로도 보존).</summary>
        private static void MarkAltarProtectedAir(Vector2Int altarOrigin, WorldGenerationConfig config,
            bool[,] protectedAir)
        {
            if (protectedAir == null) return;
            var width = config.MapWidth;
            var height = config.MapHeight;
            var size = Mathf.Max(1, config.AltarSize);
            for (var x = altarOrigin.x - 1; x <= altarOrigin.x + size; x++)
            for (var y = altarOrigin.y - 1; y <= altarOrigin.y + size; y++)
            {
                if (!InBounds(x, y, width, height)) continue;
                protectedAir[x, y] = true;
            }
        }

        /// <summary>
        /// A-18: 안전 지표면 스폰. 발밑 자연 전경 고체 1칸, 발·머리 최소 2칸 공기,
        /// 중앙 싱크홀/월드 경계/보호 구조물 회피. 같은 seed면 항상 같은 좌표.
        /// 스폰 옆 1열에 얕은 입구를 뚫어 심층 연결 통로와 이어지게 한다(발밑은 뚫지 않음).
        /// </summary>
        private static Vector2Int PlaceSafeSurfaceSpawn(TileData[,] grid, int[] surfaceHeights,
            WorldGenerationConfig config, HashSet<Vector2Int> occupied)
        {
            var width = config.MapWidth;
            var height = config.MapHeight;
            var preferredX = Mathf.Clamp(Mathf.RoundToInt(width * config.SpawnColumnRatio), 3, width - 4);

            for (var dist = 0; dist < width; dist++)
            {
                int x;
                if (dist == 0) x = preferredX;
                else if (dist % 2 == 1) x = preferredX + (dist + 1) / 2;
                else x = preferredX - dist / 2;

                if (x < 3 || x >= width - 3) continue;
                if (!TryCommitSafeSurfaceSpawn(grid, surfaceHeights, x, width, height, config, occupied, out var spawn))
                    continue;
                return spawn;
            }

            var fallbackSurface = surfaceHeights[preferredX];
            var fallback = new Vector2Int(preferredX, Mathf.Clamp(fallbackSurface + 1, 0, height - 1));
            occupied.Add(fallback);
            CarveSpawnEntrance(grid, surfaceHeights, preferredX, config, occupied);
            return fallback;
        }

        private static bool TryCommitSafeSurfaceSpawn(TileData[,] grid, int[] surfaceHeights, int x,
            int width, int height, WorldGenerationConfig config, HashSet<Vector2Int> occupied,
            out Vector2Int spawn)
        {
            spawn = default;
            var surfaceY = surfaceHeights[x];
            var bodyY = surfaceY + 1;
            var headY = surfaceY + 2;
            if (bodyY >= height || headY >= height) return false;
            if (occupied.Contains(new Vector2Int(x, bodyY))) return false;

            var ground = grid[x, surfaceY];
            if (ground.IsAir || !ground.isNaturalTerrain) return false;
            if (!grid[x, bodyY].IsAir || !grid[x, headY].IsAir) return false;
            if (bodyY != surfaceY + 1) return false;

            spawn = new Vector2Int(x, bodyY);
            occupied.Add(spawn);
            CarveSpawnEntrance(grid, surfaceHeights, x, config, occupied);

            ForceNaturalWall(grid, x + 1, surfaceY, width, height, WorldTileTypes.Dirt, 1, WorldTileTypes.BackgroundDirt);
            return true;
        }

        /// <summary>스폰 옆 열에 얕은 입구만 개방. protectedAir 미표시(수직 통로 성역화 금지).</summary>
        private static void CarveSpawnEntrance(TileData[,] grid, int[] surfaceHeights, int spawnX,
            WorldGenerationConfig config, HashSet<Vector2Int> occupied)
        {
            var width = config.MapWidth;
            var entranceX = Mathf.Clamp(spawnX - 1, 1, width - 2);
            var surfaceY = surfaceHeights[entranceX];
            var bottom = config.GetSpawnEntranceOfficialBottom(surfaceY);
            for (var y = surfaceY; y >= bottom; y--)
            {
                if (!InBounds(entranceX, y, width, config.MapHeight)) continue;
                grid[entranceX, y] = TileData.CreateAir();
                occupied?.Add(new Vector2Int(entranceX, y));
            }
        }

        private static void ReopenSpawnEntrance(TileData[,] grid, int[] surfaceHeights, Vector2Int spawnPoint,
            WorldGenerationConfig config)
        {
            CarveSpawnEntrance(grid, surfaceHeights, spawnPoint.x, config, occupied: null);
        }

        private static void ForceNaturalWall(TileData[,] grid, int x, int y, int width, int height,
            string fallbackElement, int fallbackHardness, string naturalBackground = null)
        {
            if (!InBounds(x, y, width, height)) return;
            var tile = grid[x, y];
            var bg = string.IsNullOrEmpty(naturalBackground)
                ? (tile.HasNaturalBackground ? tile.naturalBackgroundElementType : WorldTileTypes.BackgroundDirt)
                : naturalBackground;
            if (tile.IsAir)
            {
                grid[x, y] = TileData.CreateNaturalWithBackground(fallbackElement, fallbackHardness, bg);
                return;
            }

            tile.isNaturalTerrain = true;
            if (string.IsNullOrEmpty(tile.elementType) || tile.elementType == WorldTileTypes.Air)
                tile.elementType = fallbackElement;
            if (tile.hardness <= 0) tile.hardness = fallbackHardness;
            if (!tile.HasNaturalBackground)
            {
                tile.naturalBackgroundElementType = bg;
                tile.backgroundElementType = bg;
            }
            grid[x, y] = tile;
        }

        /// <summary>지상 폐허 2~3개. 철근 채집 및 상자(Ruins 지역) 배치의 앵커가 된다.</summary>
        private static List<RectInt> PlaceRuins(TileData[,] grid, int[] surfaceHeights, System.Random rng,
            WorldGenerationConfig config, HashSet<Vector2Int> occupied, Vector2Int spawnPoint)
        {
            var width = config.MapWidth;
            var height = config.MapHeight;
            var footprints = new List<RectInt>();
            var count = rng.Next(config.RuinCountMin, config.RuinCountMax + 1);

            var margin = config.AlcoveWidth * 2;
            var attempts = 0;
            while (footprints.Count < count && attempts < count * 20)
            {
                attempts++;
                var x = rng.Next(margin, Mathf.Max(margin + 1, width - margin));
                // 맵 중앙뿐 아니라 실제 스폰 열 근처도 피한다(스폰 발밑 자연 전경 보호).
                if (Mathf.Abs(x - width / 2) < margin) continue;
                if (Mathf.Abs(x + config.RuinWidth / 2 - spawnPoint.x) < margin + config.RuinWidth) continue;

                var rect = new RectInt(x, 0, config.RuinWidth, config.RuinHeight);
                if (Overlaps(rect, footprints)) continue;

                var surfaceY = surfaceHeights[Mathf.Clamp(x, 0, width - 1)];
                var overlapsSpawn = false;
                for (var dx = 0; dx < config.RuinWidth && !overlapsSpawn; dx++)
                {
                    var cx = x + dx;
                    if (!InBounds(cx, 0, width, height)) continue;
                    var localSurfaceY = surfaceHeights[Mathf.Clamp(cx, 0, width - 1)];
                    for (var dy = 0; dy < config.RuinHeight; dy++)
                    {
                        var cy = localSurfaceY + dy;
                        if (occupied.Contains(new Vector2Int(cx, cy)) ||
                            (cx == spawnPoint.x && cy == localSurfaceY))
                        {
                            overlapsSpawn = true;
                            break;
                        }
                    }
                }
                if (overlapsSpawn) continue;

                for (var dx = 0; dx < config.RuinWidth; dx++)
                {
                    var cx = x + dx;
                    if (!InBounds(cx, 0, width, height)) continue;
                    var localSurfaceY = surfaceHeights[Mathf.Clamp(cx, 0, width - 1)];
                    for (var dy = 0; dy < config.RuinHeight; dy++)
                    {
                        var cy = localSurfaceY + dy;
                        if (!InBounds(cx, cy, width, height)) continue;
                        grid[cx, cy] = new TileData
                        {
                            hardness = 1,
                            isNaturalTerrain = false,
                            elementType = WorldTileTypes.RuinWall,
                            backgroundElementType = WorldTileTypes.Air,
                            naturalBackgroundElementType = WorldTileTypes.Air
                        };
                        // 폐허 잔해 칸은 occupied에 넣지 않는다 — Ruins 지역 상자가 잔해 속에 파묻힌 채
                        // 배치될 수 있도록 일부러 비워둔다(occupied는 상자·알코브·제단 간의 겹침만 막는다).
                    }
                }

                footprints.Add(new RectInt(x, surfaceY, config.RuinWidth, config.RuinHeight));
            }

            return footprints;
        }

        /// <summary>A-18: 스폰 발밑을 파괴되지 않은 자연 전경으로 강제 복구한다.</summary>
        private static void ReinforceSafeSpawnFooting(TileData[,] grid, int[] surfaceHeights, Vector2Int spawn,
            WorldGenerationConfig config)
        {
            if (!InBounds(spawn.x, 0, config.MapWidth, config.MapHeight)) return;
            var surfaceY = surfaceHeights[Mathf.Clamp(spawn.x, 0, config.MapWidth - 1)];
            if (surfaceY < 0 || surfaceY >= config.MapHeight) return;

            var ground = grid[spawn.x, surfaceY];
            if (!ground.IsAir && ground.isNaturalTerrain) return;

            grid[spawn.x, surfaceY] = TileData.CreateNaturalWithBackground(
                WorldTileTypes.Dirt, 1, WorldTileTypes.BackgroundDirt);
        }

        private static bool Overlaps(RectInt candidate, List<RectInt> existing)
        {
            foreach (var rect in existing)
            {
                if (Mathf.Abs(candidate.x - rect.x) < candidate.width + rect.width)
                    return true;
            }
            return false;
        }

        /// <summary>심층 얼음 호수 + 이무기 소환용 얼음 제단(2×2, 파괴 불가 취급 · hardness=altarHardness).</summary>
        private static Vector2Int PlaceDeepAltarAndLake(TileData[,] grid, int[] surfaceHeights, System.Random rng, WorldGenerationConfig config, HashSet<Vector2Int> occupied)
        {
            var width = config.MapWidth;
            var height = config.MapHeight;
            var altarX = Mathf.Clamp(Mathf.RoundToInt(width * config.DeepAltarColumnRatio), config.IceLakeWidth, width - config.IceLakeWidth - 1);

            var (deepLow, deepHigh) = GetLayerRange(altarX, WorldLayer.Deep, surfaceHeights, config);
            var baseY = Mathf.Clamp(deepLow + 2, deepLow, Mathf.Max(deepLow, deepHigh - config.IceLakeHeight));

            // 호수: 걸어 다닐 수 있는 얼음 표면(hardness 0, 비-자연 지형 — 밀폐 판정에 관여하지 않는다).
            for (var dx = 0; dx < config.IceLakeWidth; dx++)
            {
                var x = altarX + dx;
                for (var dy = 0; dy < config.IceLakeHeight; dy++)
                {
                    var y = baseY + dy;
                    if (!InBounds(x, y, width, height)) continue;
                    grid[x, y] = new TileData
                    {
                        hardness = 0,
                        isNaturalTerrain = false,
                        elementType = WorldTileTypes.IceLake,
                        backgroundElementType = WorldTileTypes.BackgroundDeep,
                        naturalBackgroundElementType = WorldTileTypes.BackgroundDeep
                    };
                    occupied.Add(new Vector2Int(x, y));
                }
            }

            // 제단: 호수 옆 2×2 고정 구조물.
            var altarOriginX = altarX + config.IceLakeWidth;
            var altarOriginY = baseY;
            for (var dx = 0; dx < config.AltarSize; dx++)
            {
                for (var dy = 0; dy < config.AltarSize; dy++)
                {
                    var x = altarOriginX + dx;
                    var y = altarOriginY + dy;
                    if (!InBounds(x, y, width, height)) continue;
                    grid[x, y] = new TileData
                    {
                        hardness = config.AltarHardness,
                        isNaturalTerrain = false,
                        elementType = WorldTileTypes.IceAltar,
                        backgroundElementType = WorldTileTypes.BackgroundDeep,
                        naturalBackgroundElementType = WorldTileTypes.BackgroundDeep
                    };
                    occupied.Add(new Vector2Int(x, y));
                }
            }

            return new Vector2Int(altarOriginX, altarOriginY);
        }

        /// <summary>
        /// 대형 동굴마다 0~2개의 상자를 바닥(공기 + 아래 고체)에 배치한다. 지표에는 두지 않는다.
        /// </summary>
        private static List<ChestSpawnPoint> PlaceCavernChests(TileData[,] grid, int[] surfaceHeights,
            List<LargeCavernPlacement> caverns, System.Random rng, WorldGenerationConfig config,
            bool[,] protectedAir)
        {
            var chests = new List<ChestSpawnPoint>();
            if (caverns == null || caverns.Count == 0) return chests;

            var width = config.MapWidth;
            var height = config.MapHeight;
            var crust = Mathf.Max(1, config.CaveSurfaceCrustThickness);
            var perMin = config.ChestPerCavernMin;
            var perMax = config.ChestPerCavernMax;
            var nextIndex = 0;
            var occupied = new HashSet<Vector2Int>();
            var floorCandidates = new List<Vector2Int>(64);

            foreach (var cavern in caverns)
            {
                var want = rng.Next(perMin, perMax + 1);
                if (want <= 0) continue;

                floorCandidates.Clear();
                var invHalfW = cavern.HalfW > 0.001f ? 1f / cavern.HalfW : 0f;
                var invHalfH = cavern.HalfH > 0.001f ? 1f / cavern.HalfH : 0f;
                var minY = Mathf.Max(config.BedrockThickness, Mathf.FloorToInt(cavern.CenterY - cavern.HalfH) - 1);
                var maxY = Mathf.Min(height - 1, Mathf.CeilToInt(cavern.CenterY + cavern.HalfH) + 1);

                for (var x = cavern.MinX; x <= cavern.MaxX; x++)
                {
                    if (x < 0 || x >= width) continue;
                    var surfaceY = surfaceHeights[x];
                    var carveMaxY = surfaceY - crust;
                    for (var y = minY; y <= maxY; y++)
                    {
                        if (y > carveMaxY || y < config.BedrockThickness) continue;
                        if (IsInSurfaceMineralBan(y, surfaceY, config)) continue;

                        var nx = (x - cavern.CenterX) * invHalfW;
                        var ny = (y - cavern.CenterY) * invHalfH;
                        if (nx * nx + ny * ny > 1.05f) continue;
                        if (!grid[x, y].IsAir) continue;
                        if (y - 1 < config.BedrockThickness || grid[x, y - 1].IsAir) continue;
                        if (ShouldPreserveSpecialAir(grid[x, y])) continue;
                        if (IsProtectedAirCell(x, y, protectedAir)) continue;

                        var cell = new Vector2Int(x, y);
                        if (occupied.Contains(cell)) continue;
                        floorCandidates.Add(cell);
                    }
                }

                // 결정론적 셔플
                for (var i = floorCandidates.Count - 1; i > 0; i--)
                {
                    var j = rng.Next(i + 1);
                    (floorCandidates[i], floorCandidates[j]) = (floorCandidates[j], floorCandidates[i]);
                }

                var placed = 0;
                for (var i = 0; i < floorCandidates.Count && placed < want; i++)
                {
                    var position = floorCandidates[i];
                    if (occupied.Contains(position)) continue;
                    occupied.Add(position);
                    if (InBounds(position, width, height))
                        protectedAir[position.x, position.y] = true;

                    chests.Add(new ChestSpawnPoint
                    {
                        id = config.ChestIdPrefix + nextIndex.ToString("00"),
                        position = position,
                        region = ChestRegion.Middle
                    });
                    nextIndex++;
                    placed++;
                }
            }

            return chests;
        }

        private static void EnsureChestCellsHaveNoForeground(TileData[,] grid,
            IEnumerable<ChestSpawnPoint> chests)
        {
            if (grid == null || chests == null) return;
            var width = grid.GetLength(0);
            var height = grid.GetLength(1);
            foreach (var chest in chests)
            {
                if (!InBounds(chest.position, width, height)) continue;
                grid[chest.position.x, chest.position.y] =
                    grid[chest.position.x, chest.position.y].WithoutForeground();
            }
        }

        // ==============================================================
        // Pass 5 (검증) — 실패 시 GenerateDetailed 루프가 seed+1로 재시도한다.
        // v27: 스폰→제단 전깊이 공기 직통을 요구하지 않는다.
        //      얕은 입구 접근 + 온보딩 + 심층 제단이 지하 공동/호수 네트워크에 있으면 통과.
        // ==============================================================
        private static bool ValidateWorld(WorldGenerationResult result, WorldGenerationConfig config)
        {
            var grid = result.tiles;
            var width = result.width;
            var height = result.height;
            var spawn = result.spawnPoint;

            var reachable = FloodFillTraversable(grid, spawn, width, height);

            if (!CheckSpawnToBaseConnectivity(reachable, spawn))
            {
                Debug.LogWarning($"[MapGenerator] validation fail seed={result.acceptedSeed}: spawn not reachable/air");
                return false;
            }
            if (!CheckOnboardingResources(grid, width, height, spawn, config, reachable))
            {
                Debug.LogWarning($"[MapGenerator] validation fail seed={result.acceptedSeed}: onboarding resources");
                return false;
            }
            if (!CheckShallowUndergroundAccess(reachable, spawn, result.surfaceHeights, config))
            {
                Debug.LogWarning($"[MapGenerator] validation fail seed={result.acceptedSeed}: shallow underground access");
                return false;
            }
            if (!CheckAltarUndergroundNetwork(grid, result.altarPosition, result.surfaceHeights, config))
            {
                Debug.LogWarning($"[MapGenerator] validation fail seed={result.acceptedSeed}: altar underground network");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 스폰에서 공식 입구를 통해 crust 아래(또는 입구 바닥) 공기까지 닿는지.
        /// 지상→심층 전깊이 직통은 요구하지 않는다.
        /// </summary>
        private static bool CheckShallowUndergroundAccess(HashSet<Vector2Int> reachable, Vector2Int spawn,
            int[] surfaceHeights, WorldGenerationConfig config)
        {
            var width = surfaceHeights.Length;
            var sx = Mathf.Clamp(spawn.x, 0, width - 1);
            var surfaceY = surfaceHeights[sx];
            var crust = Mathf.Max(1, config.CaveSurfaceCrustThickness);
            var belowCrust = surfaceY - crust;
            var entranceBottom = config.GetSpawnEntranceOfficialBottom(surfaceY);

            foreach (var cell in reachable)
            {
                if (cell.y <= belowCrust) return true;
                if (cell.y <= entranceBottom) return true;
            }
            return false;
        }

        /// <summary>
        /// 제단 인접 공기/호수에서 BFS — 심층에 의미 있는 지하 공동 네트워크가 있는지.
        /// 스폰에서 걸어와야 한다는 요구는 없다(플레이어 채굴로 합류).
        /// </summary>
        private static bool CheckAltarUndergroundNetwork(TileData[,] grid, Vector2Int altarPosition,
            int[] surfaceHeights, WorldGenerationConfig config)
        {
            var width = config.MapWidth;
            var height = config.MapHeight;
            var deepThreshold = config.BedrockThickness + Mathf.Max(1, config.MiddleLayerThickness / 4);

            Vector2Int? start = null;
            foreach (var offset in FourNeighbors)
            {
                var adj = altarPosition + offset;
                if (!InBounds(adj, width, height)) continue;
                if (!grid[adj.x, adj.y].IsAir) continue;
                start = adj;
                break;
            }
            if (start == null && grid[altarPosition.x, altarPosition.y].IsAir)
                start = altarPosition;
            if (start == null) return false;

            var visited = FloodFillTraversable(grid, start.Value, width, height);
            if (visited.Count < 8) return false;

            var reachedDeep = false;
            var belowCrustCount = 0;
            foreach (var cell in visited)
            {
                if (cell.y < deepThreshold) reachedDeep = true;
                if ((uint)cell.x < (uint)width &&
                    !IsSurfaceOrCrustDigForbidden(cell.x, cell.y, surfaceHeights, config))
                    belowCrustCount++;
            }

            return reachedDeep && belowCrustCount >= 8;
        }

        /// <summary>
        /// 1) 스폰 반경 안에서 "실제로 걸어가 채굴할 수 있는" 온보딩 재료(recipes.csv workbench: 흙8+돌12)가
        /// 충분한가. A-08: 반경 안의 총 개수가 아니라 스폰 공기에서 T1 발톱으로 순차 채굴 가능한
        /// 공기/흙/돌 연결 성분만 센다. 지표 광물 금지 3칸 때문에 돌은 공식 입구 공기에 바로 12개를
        /// 노출할 수 없으므로, 먼저 흙을 캐고 이어서 닿는 돌도 실제 온보딩 획득 가능 자원으로 인정한다.
        /// </summary>
        private static bool CheckOnboardingResources(TileData[,] grid, int width, int height, Vector2Int spawn,
            WorldGenerationConfig config, HashSet<Vector2Int> reachable)
        {
            var radius = config.OnboardingSearchRadius;
            var mineable = FloodFillOnboardingMineable(grid, spawn, width, height, radius);
            var dirtCount = 0;
            var stoneCount = 0;

            for (var x = spawn.x - radius; x <= spawn.x + radius; x++)
            {
                for (var y = spawn.y - radius; y <= spawn.y + radius; y++)
                {
                    var point = new Vector2Int(x, y);
                    if (!InBounds(point, width, height)) continue;
                    if (!mineable.Contains(point)) continue;

                    var tile = grid[x, y];
                    if (string.Equals(tile.elementType, WorldTileTypes.Dirt, StringComparison.Ordinal)) dirtCount++;
                    else if (string.Equals(tile.elementType, WorldTileTypes.Stone, StringComparison.Ordinal)) stoneCount++;
                }
            }

            return dirtCount >= config.OnboardingRequiredDirt && stoneCount >= config.OnboardingRequiredStone;
        }

        /// <summary>
        /// 스폰 반경 안에서 공기와 T1 채굴 가능한 온보딩 재료(흙/돌)를 순차적으로 통과하는 연결 성분.
        /// 광석·특수 구조물은 통과하지 않아 벽 너머 고립 자원을 세지 않는다.
        /// </summary>
        private static HashSet<Vector2Int> FloodFillOnboardingMineable(TileData[,] grid, Vector2Int spawn,
            int width, int height, int radius)
        {
            var visited = new HashSet<Vector2Int>();
            if (!InBounds(spawn, width, height) || !grid[spawn.x, spawn.y].IsAir) return visited;

            var queue = new Queue<Vector2Int>();
            queue.Enqueue(spawn);
            visited.Add(spawn);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var offset in FourNeighbors)
                {
                    var next = current + offset;
                    if (!InBounds(next, width, height) || visited.Contains(next)) continue;
                    if (Mathf.Abs(next.x - spawn.x) > radius || Mathf.Abs(next.y - spawn.y) > radius) continue;
                    var tile = grid[next.x, next.y];
                    if (!tile.IsAir &&
                        !string.Equals(tile.elementType, WorldTileTypes.Dirt, StringComparison.Ordinal) &&
                        !string.Equals(tile.elementType, WorldTileTypes.Stone, StringComparison.Ordinal))
                        continue;
                    visited.Add(next);
                    queue.Enqueue(next);
                }
            }

            return visited;
        }

        /// <summary>해당 칸의 4방향 인접 칸 중 하나라도 스폰에서 걸어갈 수 있는 공기 네트워크(reachable)에 속하면
        /// "그 자리에서 바로 채굴 가능"으로 간주한다.</summary>
        private static bool IsAdjacentToReachable(Vector2Int point, HashSet<Vector2Int> reachable, int width, int height)
        {
            foreach (var offset in FourNeighbors)
            {
                var neighbor = point + offset;
                if (InBounds(neighbor, width, height) && reachable.Contains(neighbor)) return true;
            }
            return false;
        }

        /// <summary>2) 스폰 지점(알코브 개방부)이 실제로 걸어 다닐 수 있는 공기 네트워크로 연결돼 있는가.</summary>
        private static bool CheckSpawnToBaseConnectivity(HashSet<Vector2Int> reachable, Vector2Int spawn) => reachable.Contains(spawn);

        /// <summary>스폰에서 시작해 "공기(하드니스 0)" 칸만 타고 갈 수 있는 전체 영역을 BFS로 구한다.</summary>
        private static HashSet<Vector2Int> FloodFillTraversable(TileData[,] grid, Vector2Int start, int width, int height)
        {
            var visited = new HashSet<Vector2Int>();
            if (!InBounds(start, width, height) || !grid[start.x, start.y].IsAir) return visited;

            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            visited.Add(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var offset in FourNeighbors)
                {
                    var next = current + offset;
                    if (!InBounds(next, width, height) || visited.Contains(next)) continue;
                    if (!grid[next.x, next.y].IsAir) continue;
                    visited.Add(next);
                    queue.Enqueue(next);
                }
            }

            return visited;
        }

        private static bool InBounds(Vector2Int point, int width, int height) => InBounds(point.x, point.y, width, height);
        private static bool InBounds(int x, int y, int width, int height) => x >= 0 && x < width && y >= 0 && y < height;
    }
}
