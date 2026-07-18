using System;
using System.Collections.Generic;
using Nyangbingo.Core;
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

        // 중층 (T2)
        public const string StoneMid = "stone_mid";
        public const string IronOre = "iron_ore";
        public const string CopperOre = "copper_ore";
        public const string IceShard = "ice_shard";

        // 심층 (T3)
        public const string StoneDeep = "stone_deep";
        public const string IceSteelOre = "ice_steel_ore";
        public const string FrostEssence = "frost_essence";

        // 구조물 / 최하단
        public const string Bedrock = "bedrock";
        public const string RuinWall = "ruin_wall";
        public const string IceLake = "ice_lake";
        public const string IceAltar = "ice_altar";

        // 배경벽 (채굴 불가 · 장식용 · 테라리아식 2중 구조)
        public const string BackgroundDirt = "bg_dirt";
        public const string BackgroundStone = "bg_stone";
        public const string BackgroundDeep = "bg_deep";

        /// <summary>
        /// Air를 제외한 모든 알려진 elementType(전경 15종 + 배경벽 3종 = 18종, 임시 타일 팔레트와 정확히 대응).
        /// 세이브 로드 시 타일 변경 이력의 tileId가 이 목록에 없으면 손상된(또는 알 수 없는) 데이터로 간주해
        /// 거부한다(A-06/A-08 — 문자열을 직접 이곳저곳에서 비교하지 않고 단일 출처로 검증).
        /// </summary>
        public static readonly HashSet<string> AllElementTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            Dirt, Stone, Coal, Clay,
            StoneMid, IronOre, CopperOre, IceShard,
            StoneDeep, IceSteelOre, FrostEssence,
            Bedrock, RuinWall, IceLake, IceAltar,
            BackgroundDirt, BackgroundStone, BackgroundDeep
        };
    }

    /// <summary>
    /// WorldGenerator가 찍는 낱개 타일의 메타데이터.
    /// TileService/SealSystem이 그대로 참조하는 계약이므로 필드 의미를 바꾸지 말 것.
    /// </summary>
    [Serializable]
    public struct TileData
    {
        /// <summary>경도 1~3. 발톱 티어 ≥ 경도일 때만 파괴 가능(TileService 판정). 빙암은 3 고정.</summary>
        public int hardness;

        /// <summary>
        /// 자연 지형(흙·돌·암반 등) 여부. seal-whitelist.csv 기준 true여야 SealSystem이 밀폐 벽으로 인정한다.
        /// WorldGenerator가 생성하는 지형·광맥 타일은 전부 true, 플레이어 건설물/장식은 false.
        /// </summary>
        public bool isNaturalTerrain;

        /// <summary>자원/타일 종류 ID. <see cref="WorldTileTypes"/> 참조, items.csv/mineral-tiers.csv와 매칭.</summary>
        public string elementType;

        /// <summary>
        /// 배경벽(채굴 불가·장식용) 여부. 전경이 비어 있어도(hardness 0, elementType "air") 이 값이 true면
        /// 뒤에 배경벽 스프라이트가 깔려 있다는 뜻 — 테라리아식 2중 Tilemap 구조를 단일 배열로 표현한다.
        /// </summary>
        public bool isUndergroundDecor;

        public static TileData CreateAir() => new TileData
        {
            hardness = 0,
            isNaturalTerrain = false,
            elementType = WorldTileTypes.Air,
            isUndergroundDecor = false
        };

        /// <summary>
        /// 전경이 비어 있는(hardness 0) 지하 칸. elementType에는 뒤에 깔린 배경벽 종류(bg_dirt 등)를
        /// 담아 렌더링이 참조하게 하고, isUndergroundDecor=true로 "채굴 불가·장식용"임을 표시한다.
        /// 걷기/판정 목적의 "비어있음"은 elementType 문자열이 아니라 항상 hardness &lt;= 0으로 판별한다(<see cref="IsAir"/>).
        /// </summary>
        public static TileData CreateCaveAir(string backgroundElement) => new TileData
        {
            hardness = 0,
            isNaturalTerrain = false,
            elementType = string.IsNullOrEmpty(backgroundElement) ? WorldTileTypes.Air : backgroundElement,
            isUndergroundDecor = true
        };

        public static TileData CreateNatural(string elementId, int hardnessValue) => new TileData
        {
            hardness = hardnessValue,
            isNaturalTerrain = true,
            elementType = elementId,
            isUndergroundDecor = false
        };

        /// <summary>전경에 파괴/채굴 대상이 없는(통행 가능한) 칸인지. 배경벽 유무와 무관하게 hardness로만 판별한다.</summary>
        public bool IsAir => hardness <= 0;
    }

    /// <summary>Pass 3 광맥(Vein) 하나의 배치 규칙. mineral-tiers.csv의 "빈도(개/100타일)"를 그대로 옮겨온다.</summary>
    [Serializable]
    public struct OreVeinProfile
    {
        public string elementType;
        public WorldLayer layer;
        [Range(1, 3)] public int hardness;
        [Min(0f)] public float frequencyPer100Tiles;
        [Min(1)] public int minClusterSize;
        [Min(1)] public int maxClusterSize;
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

        private readonly WorldGenerationConfig config;

        private WorldGenerationResult lastResult;
        private Dictionary<string, ChestSpawnPoint> chestsById = new Dictionary<string, ChestSpawnPoint>(StringComparer.Ordinal);
        private List<string> chestIdsCache = new List<string>();

        public MapGenerator(WorldGenerationConfig generationConfig)
        {
            config = generationConfig != null ? generationConfig : WorldGenerationConfig.CreateDefault();
        }

        public WorldGenerationResult LastResult => lastResult;
        public Vector2Int SpawnPoint => lastResult.spawnPoint;
        public Vector2Int AltarPosition => lastResult.altarPosition;
        public int AcceptedSeed => lastResult.acceptedSeed;

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
                result = GenerateSingleAttempt(candidateSeed, config);
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
        /// 여기 별도 공개 메서드로 추가한다. 상자는 정확히 20개뿐이라 선형 탐색으로도 비용이 무시할 만하다.</summary>
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
        private static WorldGenerationResult GenerateSingleAttempt(int seed, WorldGenerationConfig config)
        {
            var width = config.MapWidth;
            var height = config.MapHeight;
            var grid = new TileData[width, height];

            // 패스별로 완전히 분리된 RNG — 순서가 바뀌어도 재현성이 깨지지 않는다.
            var terrainRng = new System.Random(seed + 1);
            var caveRng = new System.Random(seed + 2);
            var resourceRng = new System.Random(seed + 3);
            var structureRng = new System.Random(seed + 4);

            var surfaceHeights = GenerateTerrain(grid, terrainRng, config);   // Pass 1
            CarveCaves(grid, surfaceHeights, caveRng, config);                // Pass 2
            PlaceOreVeins(grid, surfaceHeights, resourceRng, config);         // Pass 3
            var structures = PlaceStructures(grid, surfaceHeights, structureRng, config); // Pass 4

            // Pass 2 안전망(연결성 확정 통로)은 반드시 Pass 4보다 나중에 뚫어야 한다 — PlaceStartAlcove가
            // 알코브 3면(좌/우/바닥)을 자연 지형으로 강제로 되메우는데, 그 바닥벽 행(y = bottom-1)이
            // 스폰 컬럼과 겹쳐서 먼저 뚫어둔 샤프트를 다시 막아버리는 버그가 있었다(등록 순서 문제).
            // 그래서 항상 마지막에 뚫어 "무조건 통과하는 확정 통로" 보장을 지킨다.
            CarveConnectivityShafts(grid, surfaceHeights, caveRng, config);

            return new WorldGenerationResult
            {
                acceptedSeed = seed,
                width = width,
                height = height,
                tiles = grid,
                spawnPoint = structures.spawnPoint,
                altarPosition = structures.altarPosition,
                chests = structures.chests
            };
        }

        // ==============================================================
        // Pass 1 — 지형 (1D 펄린 높이라인 + 레이어 경계 + 최하단 경계암)
        // ==============================================================
        private static int[] GenerateTerrain(TileData[,] grid, System.Random rng, WorldGenerationConfig config)
        {
            var width = config.MapWidth;
            var height = config.MapHeight;
            var surfaceHeights = new int[width];

            var heightNoiseOffset = (float)(rng.NextDouble() * 100000.0);
            var fillNoiseOffsetX = (float)(rng.NextDouble() * 100000.0);
            var fillNoiseOffsetY = (float)(rng.NextDouble() * 100000.0);

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
                        WorldLayer.Surface => TileData.CreateAir(),
                        WorldLayer.Bedrock => TileData.CreateNatural(WorldTileTypes.Bedrock, 3),
                        WorldLayer.Upper => TileData.CreateNatural(PickUpperFillElement(x, y, fillNoiseOffsetX, fillNoiseOffsetY, config), 1),
                        WorldLayer.Middle => TileData.CreateNatural(WorldTileTypes.StoneMid, 2),
                        _ => TileData.CreateNatural(WorldTileTypes.StoneDeep, 3)
                    };
                }
            }

            return surfaceHeights;
        }

        private static string PickUpperFillElement(int x, int y, float offsetX, float offsetY, WorldGenerationConfig config)
        {
            var noise = Mathf.PerlinNoise((x + offsetX) * 0.15f, (y + offsetY) * 0.15f);
            return noise < config.UpperDirtRatio ? WorldTileTypes.Dirt : WorldTileTypes.Stone;
        }

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
        // Pass 2 — 동굴 (2D 펄린 임계값, 깊이에 따라 상층 10% → 심층 25% 보간)
        // 뚫린 칸의 전경은 비지만(hardness 0), isUndergroundDecor = true로
        // "배경벽이 뒤에 깔려 있다"는 정보는 유지한다(테라리아식 2중 구조).
        // ==============================================================
        private static void CarveCaves(TileData[,] grid, int[] surfaceHeights, System.Random rng, WorldGenerationConfig config)
        {
            var width = config.MapWidth;
            var offsetX = (float)(rng.NextDouble() * 100000.0);
            var offsetY = (float)(rng.NextDouble() * 100000.0);

            for (var x = 0; x < width; x++)
            {
                var surfaceY = surfaceHeights[x];
                for (var y = config.BedrockThickness; y <= surfaceY; y++)
                {
                    var layer = ClassifyLayer(y, surfaceY, config);
                    if (layer == WorldLayer.Surface || layer == WorldLayer.Bedrock) continue;

                    var depthT = Mathf.InverseLerp(surfaceY, config.BedrockThickness, y);
                    var caveChance = Mathf.Lerp(config.CaveChanceUpper, config.CaveChanceDeep, depthT);

                    var noise = Mathf.PerlinNoise((x + offsetX) * config.CaveNoiseFrequency, (y + offsetY) * config.CaveNoiseFrequency);
                    if (noise < caveChance)
                        grid[x, y] = TileData.CreateCaveAir(BackgroundElementFor(layer));
                }
            }
        }

        private static string BackgroundElementFor(WorldLayer layer) => layer switch
        {
            WorldLayer.Upper => WorldTileTypes.BackgroundDirt,
            WorldLayer.Middle => WorldTileTypes.BackgroundStone,
            _ => WorldTileTypes.BackgroundDeep
        };

        /// <summary>
        /// 사이트 퍼콜레이션 임계값(2D grid ≈ 59.3%)보다 훨씬 낮은 공동률(10~25%)에서는 독립적인
        /// 펄린 동굴 블롭만으로 "스폰→심층→제단"이 실제로 이어질 확률이 매우 낮다. §5 검증(경로 연결성/
        /// 심층 연결/제단 도달)이 리롤 상한 안에서 통과하도록, 스폰 컬럼과 제단 컬럼을 잇는 확정 통로를
        /// Pass 2에서 쓰던 caveRng로 결정론적으로 뚫어준다. 나머지는 여전히 펄린 동굴이 채운다.
        ///
        /// 호출 순서 주의: 반드시 Pass 4(PlaceStructures) 이후에 호출해야 한다. PlaceStartAlcove가
        /// 알코브 좌/우/바닥 3면을 자연 지형으로 강제로 되메우는데, 그 바닥벽 행이 스폰 컬럼과 겹쳐서
        /// Pass 4보다 먼저 뚫으면 알코브가 자기 발밑 통로를 다시 막아버린다(실제로 겪었던 회귀 버그).
        /// </summary>
        private static void CarveConnectivityShafts(TileData[,] grid, int[] surfaceHeights, System.Random rng, WorldGenerationConfig config)
        {
            var width = config.MapWidth;
            var spawnX = Mathf.Clamp(Mathf.RoundToInt(width * config.SpawnColumnRatio), 1, width - 2);
            var altarX = Mathf.Clamp(Mathf.RoundToInt(width * config.DeepAltarColumnRatio), 1, width - 2);
            var deepFloor = config.BedrockThickness + 1;

            var shaftTop = new Vector2Int(spawnX, Mathf.Max(config.BedrockThickness, surfaceHeights[spawnX] - 1));
            var shaftBottom = new Vector2Int(spawnX, deepFloor);
            CarveTunnel(grid, surfaceHeights, shaftTop, shaftBottom, rng, config);

            var altarPoint = new Vector2Int(altarX, deepFloor);
            CarveTunnel(grid, surfaceHeights, shaftBottom, altarPoint, rng, config);
        }

        /// <summary>from→to를 맨해튼 거리만큼의 스텝으로 정확히 잇는 무작위 지그재그 통로. 매 스텝 거리가
        /// 엄격히 줄어들기 때문에 별도 반복 상한 없이 항상 종료가 보장된다.</summary>
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
            CarveTunnelCell(grid, surfaceHeights, center, config);
            foreach (var offset in FourNeighbors)
                CarveTunnelCell(grid, surfaceHeights, center + offset, config);
        }

        private static void CarveTunnelCell(TileData[,] grid, int[] surfaceHeights, Vector2Int point, WorldGenerationConfig config)
        {
            var width = config.MapWidth;
            var height = config.MapHeight;
            if (!InBounds(point, width, height)) return;
            if (point.y < config.BedrockThickness) return; // 최하단 경계암은 절대 뚫지 않는다.

            var surfaceY = surfaceHeights[Mathf.Clamp(point.x, 0, width - 1)];
            if (point.y > surfaceY) return; // 지표면 위(하늘)에는 구멍을 내지 않는다.

            var layer = ClassifyLayer(point.y, surfaceY, config);
            grid[point.x, point.y] = TileData.CreateCaveAir(BackgroundElementFor(layer));
        }

        // ==============================================================
        // Pass 3 — 자원 (mineral-tiers.csv 빈도 → 클러스터 광맥, 3~6타일)
        // ==============================================================
        private static void PlaceOreVeins(TileData[,] grid, int[] surfaceHeights, System.Random rng, WorldGenerationConfig config)
        {
            var width = config.MapWidth;
            var height = config.MapHeight;

            foreach (var profile in config.OreVeins)
            {
                var averageClusterSize = Mathf.Max(1f, (profile.minClusterSize + profile.maxClusterSize) * 0.5f);
                var layerAreaTiles = EstimateLayerArea(profile.layer, surfaceHeights, config);
                var veinCount = Mathf.Max(0, Mathf.RoundToInt(layerAreaTiles * profile.frequencyPer100Tiles / 100f / averageClusterSize));

                for (var i = 0; i < veinCount; i++)
                {
                    var x = rng.Next(0, width);
                    var (low, high) = GetLayerRange(x, profile.layer, surfaceHeights, config);
                    if (high < low) continue;

                    var seedY = rng.Next(low, high + 1);
                    if (grid[x, seedY].IsAir) continue; // 이미 동굴로 뚫린 자리는 건너뛴다 — 소량 손실은 허용.

                    GrowVeinCluster(grid, new Vector2Int(x, seedY), low, high, width, height, profile, rng);
                }
            }
        }

        private static int EstimateLayerArea(WorldLayer layer, int[] surfaceHeights, WorldGenerationConfig config)
        {
            var width = config.MapWidth;
            var total = 0;
            for (var x = 0; x < width; x++)
            {
                var (low, high) = GetLayerRange(x, layer, surfaceHeights, config);
                if (high >= low) total += high - low + 1;
            }
            return total;
        }

        private static void GrowVeinCluster(TileData[,] grid, Vector2Int seed, int layerLow, int layerHigh, int width, int height,
            OreVeinProfile profile, System.Random rng)
        {
            var size = rng.Next(profile.minClusterSize, profile.maxClusterSize + 1);
            var current = seed;

            for (var i = 0; i < size; i++)
            {
                if (InBounds(current, width, height) && !grid[current.x, current.y].IsAir)
                {
                    grid[current.x, current.y] = TileData.CreateNatural(profile.elementType, profile.hardness);
                }

                var direction = FourNeighbors[rng.Next(FourNeighbors.Length)];
                current += direction;
                current.x = Mathf.Clamp(current.x, 0, width - 1);
                current.y = Mathf.Clamp(current.y, layerLow, layerHigh);
            }
        }

        // ==============================================================
        // Pass 4 — 구조물 (반지하 알코브 · 지상 폐허 · 심층 얼음호수+제단 · 상자 20개)
        // ==============================================================
        private struct StructurePlacement
        {
            public Vector2Int spawnPoint;
            public Vector2Int altarPosition;
            public List<ChestSpawnPoint> chests;
        }

        private static StructurePlacement PlaceStructures(TileData[,] grid, int[] surfaceHeights, System.Random rng, WorldGenerationConfig config)
        {
            var occupied = new HashSet<Vector2Int>();

            var spawnPoint = PlaceStartAlcove(grid, surfaceHeights, config, occupied);
            var ruinFootprints = PlaceRuins(grid, surfaceHeights, rng, config, occupied);
            var altarPosition = PlaceDeepAltarAndLake(grid, surfaceHeights, rng, config, occupied);
            var chests = PlaceChests(grid, surfaceHeights, rng, config, occupied, ruinFootprints);

            return new StructurePlacement
            {
                spawnPoint = spawnPoint,
                altarPosition = altarPosition,
                chests = chests
            };
        }

        /// <summary>
        /// 시작점 = 반지하 알코브. 좌/우/바닥 3면을 자연 지형으로 강제하고 위쪽만 개방한다.
        /// v15 QA-H: 1일차 온도 완충 + "벽에 기대면 오른다"를 지형으로 가르치는 무텍스트 튜토리얼.
        /// </summary>
        private static Vector2Int PlaceStartAlcove(TileData[,] grid, int[] surfaceHeights, WorldGenerationConfig config, HashSet<Vector2Int> occupied)
        {
            var width = config.MapWidth;
            var height = config.MapHeight;
            var spawnX = Mathf.Clamp(Mathf.RoundToInt(width * config.SpawnColumnRatio), config.AlcoveWidth, width - config.AlcoveWidth - 1);
            var surfaceY = surfaceHeights[spawnX];

            var alcoveWidth = config.AlcoveWidth;
            var alcoveHeight = config.AlcoveHeight;
            var left = spawnX - alcoveWidth / 2;
            var right = left + alcoveWidth - 1;
            var top = surfaceY;
            var bottom = top - alcoveHeight + 1;

            for (var x = left; x <= right; x++)
            {
                for (var y = bottom; y <= top; y++)
                {
                    if (!InBounds(x, y, width, height)) continue;
                    grid[x, y] = TileData.CreateCaveAir(BackgroundElementFor(ClassifyLayer(y, surfaceY, config)));
                    occupied.Add(new Vector2Int(x, y));
                }
            }

            // 좌/우 벽면
            for (var y = bottom - 1; y <= top; y++)
            {
                ForceNaturalWall(grid, left - 1, y, width, height, WorldTileTypes.Dirt, 1);
                ForceNaturalWall(grid, right + 1, y, width, height, WorldTileTypes.Dirt, 1);
            }

            // 바닥면
            for (var x = left - 1; x <= right + 1; x++)
                ForceNaturalWall(grid, x, bottom - 1, width, height, WorldTileTypes.Stone, 1);

            // 위쪽(top)은 강제하지 않는다 — 지표면과 이어져 개방된 "반지하" 구조를 유지.
            var interiorY = Mathf.Clamp((top + bottom) / 2, bottom, top);
            return new Vector2Int(spawnX, interiorY);
        }

        private static void ForceNaturalWall(TileData[,] grid, int x, int y, int width, int height, string fallbackElement, int fallbackHardness)
        {
            if (!InBounds(x, y, width, height)) return;
            var tile = grid[x, y];
            if (tile.IsAir)
            {
                tile.elementType = fallbackElement;
                tile.hardness = fallbackHardness;
            }
            tile.isNaturalTerrain = true;
            tile.isUndergroundDecor = false;
            grid[x, y] = tile;
        }

        /// <summary>지상 폐허 2~3개. 철근 채집 및 상자(Ruins 지역) 배치의 앵커가 된다.</summary>
        private static List<RectInt> PlaceRuins(TileData[,] grid, int[] surfaceHeights, System.Random rng, WorldGenerationConfig config, HashSet<Vector2Int> occupied)
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
                if (Mathf.Abs(x - width / 2) < margin) continue; // 스폰 근처는 피한다.

                var rect = new RectInt(x, 0, config.RuinWidth, config.RuinHeight);
                if (Overlaps(rect, footprints)) continue;

                var surfaceY = surfaceHeights[Mathf.Clamp(x, 0, width - 1)];
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
                            isUndergroundDecor = false
                        };
                        // 폐허 잔해 칸은 occupied에 넣지 않는다 — Ruins 지역 상자가 잔해 속에 파묻힌 채
                        // 배치될 수 있도록 일부러 비워둔다(occupied는 상자·알코브·제단 간의 겹침만 막는다).
                    }
                }

                footprints.Add(new RectInt(x, surfaceY, config.RuinWidth, config.RuinHeight));
            }

            return footprints;
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
                        isUndergroundDecor = false
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
                        isUndergroundDecor = false
                    };
                    occupied.Add(new Vector2Int(x, y));
                }
            }

            return new Vector2Int(altarOriginX, altarOriginY);
        }

        /// <summary>정확히 (6-12 표 기본값 합계 = 20)개의 결정론적 상자를 지역별로 겹치지 않게 배치한다.</summary>
        private static List<ChestSpawnPoint> PlaceChests(TileData[,] grid, int[] surfaceHeights, System.Random rng, WorldGenerationConfig config,
            HashSet<Vector2Int> occupied, List<RectInt> ruinFootprints)
        {
            var width = config.MapWidth;
            var height = config.MapHeight;
            var chests = new List<ChestSpawnPoint>(config.TotalChestCount);
            var nextIndex = 0;

            PlaceChestsForRegion(ChestRegion.Ruins, config.ChestCountRuins, WorldLayer.Surface);
            PlaceChestsForRegion(ChestRegion.Upper, config.ChestCountUpper, WorldLayer.Upper);
            PlaceChestsForRegion(ChestRegion.Middle, config.ChestCountMiddle, WorldLayer.Middle);
            PlaceChestsForRegion(ChestRegion.Deep, config.ChestCountDeep, WorldLayer.Deep);

            // 안전망: 지형 굴곡 등의 이유로 특정 지역에서 목표 개수를 못 채웠다면, "정확히 TotalChestCount개"
            // 계약(Dev B 인수인계 §1-9)을 지키기 위해 맵 전역에서 남은 자리를 결정론적으로 채워 넣는다.
            var fallbackAttempts = 0;
            var maxFallbackAttempts = Mathf.Max(200, config.TotalChestCount * 200);
            while (chests.Count < config.TotalChestCount && fallbackAttempts < maxFallbackAttempts)
            {
                fallbackAttempts++;
                var x = rng.Next(0, width);
                var y = rng.Next(config.BedrockThickness, height);
                var position = new Vector2Int(x, y);
                if (occupied.Contains(position)) continue;

                var layer = ClassifyLayer(y, surfaceHeights[x], config);
                if (layer == WorldLayer.Bedrock) continue;

                if (!grid[position.x, position.y].IsAir)
                {
                    grid[position.x, position.y] = layer == WorldLayer.Surface
                        ? TileData.CreateAir()
                        : TileData.CreateCaveAir(BackgroundElementFor(layer));
                }

                occupied.Add(position);
                var region = layer switch
                {
                    WorldLayer.Surface => ChestRegion.Ruins,
                    WorldLayer.Upper => ChestRegion.Upper,
                    WorldLayer.Middle => ChestRegion.Middle,
                    _ => ChestRegion.Deep
                };
                chests.Add(new ChestSpawnPoint { id = config.ChestIdPrefix + nextIndex.ToString("00"), position = position, region = region });
                nextIndex++;
            }

            if (chests.Count < config.TotalChestCount)
            {
                Debug.LogWarning($"[MapGenerator] Only placed {chests.Count}/{config.TotalChestCount} chests — map is extremely cramped for the configured chest count.");
            }

            return chests;

            void PlaceChestsForRegion(ChestRegion region, int count, WorldLayer layer)
            {
                var placed = 0;
                var attempts = 0;
                var maxAttempts = Mathf.Max(count * 40, 40);

                while (placed < count && attempts < maxAttempts)
                {
                    attempts++;
                    var x = rng.Next(0, width);

                    int y;
                    if (region == ChestRegion.Ruins)
                    {
                        if (ruinFootprints.Count == 0) break;
                        var footprint = ruinFootprints[rng.Next(ruinFootprints.Count)];
                        x = footprint.x + rng.Next(0, Mathf.Max(1, footprint.width));
                        y = footprint.y;
                    }
                    else
                    {
                        var (low, high) = GetLayerRange(x, layer, surfaceHeights, config);
                        if (high < low) continue;
                        y = rng.Next(low, high + 1);
                    }

                    var position = new Vector2Int(x, y);
                    if (!InBounds(position, width, height) || occupied.Contains(position)) continue;

                    // 자리가 막혀 있으면 상자 한 칸만 파서 안치한다(지하 보물상자 관용).
                    if (!grid[position.x, position.y].IsAir)
                    {
                        grid[position.x, position.y] = region == ChestRegion.Ruins
                            ? TileData.CreateAir()
                            : TileData.CreateCaveAir(BackgroundElementFor(layer));
                    }

                    occupied.Add(position);
                    chests.Add(new ChestSpawnPoint
                    {
                        id = config.ChestIdPrefix + nextIndex.ToString("00"),
                        position = position,
                        region = region
                    });
                    nextIndex++;
                    placed++;
                }
            }
        }

        // ==============================================================
        // Pass 5 (검증) — 실패 시 GenerateDetailed 루프가 seed+1로 재시도한다.
        // ==============================================================
        private static bool ValidateWorld(WorldGenerationResult result, WorldGenerationConfig config)
        {
            var grid = result.tiles;
            var width = result.width;
            var height = result.height;
            var spawn = result.spawnPoint;

            var reachable = FloodFillTraversable(grid, spawn, width, height);

            if (!CheckSpawnToBaseConnectivity(reachable, spawn)) return false;
            if (!CheckOnboardingResources(grid, width, height, spawn, config, reachable)) return false;
            if (!CheckDeepLayerConnectivity(reachable, config)) return false;
            if (!CheckAltarReachability(reachable, result.altarPosition)) return false;

            return true;
        }

        /// <summary>
        /// 1) 스폰 반경 안에서 "실제로 걸어가 채굴할 수 있는" 온보딩 재료(recipes.csv workbench: 흙8+돌12)가
        /// 충분한가. A-08: 예전에는 반경 안의 총 개수만 셌기 때문에, 반경 안이라도 벽 너머(도달 불가능한
        /// 고립 포켓)에 있는 흙/돌까지 "확보 가능"으로 잘못 인정하는 결함이 있었다. 지금은 각 자원 후보 칸이
        /// <paramref name="reachable"/>(스폰에서 공기 칸만 타고 갈 수 있는 네트워크)에 실제로 인접해 있어야만
        /// 센다 — "채굴 소요 시간(초)"까지 시뮬레이션하지는 않지만, 최소한 "실제로 접근해서 채굴 가능한
        /// 위치인가"는 정직하게 검증한다(인수인계 문서 A-08/A-11에 이 정책을 그대로 기록할 것).
        /// </summary>
        private static bool CheckOnboardingResources(TileData[,] grid, int width, int height, Vector2Int spawn,
            WorldGenerationConfig config, HashSet<Vector2Int> reachable)
        {
            var radius = config.OnboardingSearchRadius;
            var dirtCount = 0;
            var stoneCount = 0;

            for (var x = spawn.x - radius; x <= spawn.x + radius; x++)
            {
                for (var y = spawn.y - radius; y <= spawn.y + radius; y++)
                {
                    var point = new Vector2Int(x, y);
                    if (!InBounds(point, width, height)) continue;
                    if (!IsAdjacentToReachable(point, reachable, width, height)) continue; // 걸어서 인접할 수 없으면 "확보 가능"이 아니다.

                    var tile = grid[x, y];
                    if (string.Equals(tile.elementType, WorldTileTypes.Dirt, StringComparison.Ordinal)) dirtCount++;
                    else if (string.Equals(tile.elementType, WorldTileTypes.Stone, StringComparison.Ordinal)) stoneCount++;
                }
            }

            return dirtCount >= config.OnboardingRequiredDirt && stoneCount >= config.OnboardingRequiredStone;
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

        /// <summary>3) 스폰에서 심층까지 자연 동굴로 이어지는 통로가 존재하는가.</summary>
        private static bool CheckDeepLayerConnectivity(HashSet<Vector2Int> reachable, WorldGenerationConfig config)
        {
            var deepThreshold = config.BedrockThickness + Mathf.Max(1, config.MiddleLayerThickness / 4);
            foreach (var cell in reachable)
            {
                if (cell.y < deepThreshold) return true;
            }
            return false;
        }

        /// <summary>4) 심층 얼음 제단(이무기 소환처)까지 도달 가능한가.</summary>
        private static bool CheckAltarReachability(HashSet<Vector2Int> reachable, Vector2Int altarPosition)
        {
            foreach (var offset in FourNeighbors)
            {
                var adjacent = altarPosition + offset;
                if (reachable.Contains(adjacent)) return true;
            }
            return reachable.Contains(altarPosition);
        }

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
