using System;
using System.Collections.Generic;
using System.Reflection;
using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using Nyangbingo.Save;
using Nyangbingo.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;
using UnityTilemapRenderer = UnityEngine.Tilemaps.TilemapRenderer;
using WorldTilemapRenderer = Nyangbingo.World.TilemapRenderer;
// UnityEngine.Tilemaps에도 동명의 TileData가 있어 using Nyangbingo.World와 충돌(CS0104)한다 — 명시적으로 고정.
using TileData = Nyangbingo.World.TileData;

/// <summary>
/// A-09: 개발 A 담당 범위(월드 생성/타일/밀폐/시간/세이브 로드)에 대한 저장소 반복 실행형 회귀
/// 스모크 테스트. Unity Test Framework(NUnit) 대신 에디터 메뉴 하나로 전체를 한 번에 실행하는
/// 형태를 택했다 — 프로젝트에 아직 별도 테스트 asmdef가 없어 도입 범위가 커지는 것을 피하면서도,
/// "저장소에서 반복 실행 가능 + 테스트별 성공/실패 로그 명확 + 한 번에 전체 실행" 요구사항은
/// 그대로 만족한다. `-executeMethod NyangbingoDevARegressionTests.RunAll -batchmode -quit`로도
/// CI에서 그대로 실행할 수 있다.
/// </summary>
public static class NyangbingoDevARegressionTests
{
    [MenuItem("Nyangbingo/Run Dev A Regression Tests")]
    public static void RunAll()
    {
        var config = WorldGenerationConfig.CreateDefault();
        var tests = new (string name, Action action)[]
        {
            ("결정론적 생성", () => TestDeterministicGeneration(config)),
            ("상자 분포", () => TestChestDistribution(config)),
            ("타일 변경 이력 원자성", () => TestTileRestoreAtomicity(config)),
            ("밀폐 시스템", TestSealSystem),
            ("낮/밤 전환", TestDayNightTransitions),
            ("월드 세션 라운드트립", () => TestWorldSessionRoundTrip(config)),
            // A-10: 지층 깊이 45/45/45/5 교정 + mineral-tiers.csv depth_min/depth_max 정합성.
            ("지층 깊이·광물 깊이 정합성", () => TestLayerDepthAndMineralRanges(config)),
            // A-11/A-12/A-13: SealSystem 57×25 코어 창, SetSealCoreCell API, 냉기원 상한 연동 회귀 13종.
            ("SealSystem 코어 창(57x25)", TestSealSystemCoreWindow),
            // A-14: 타일 노출면 먹선 오버레이 — 마스크→모양 테이블 완전성 + 변경 셀 국소 갱신 통합 검증.
            ("타일 노출면 먹선 오버레이", TestTileEdgeOverlay),
            // A-16/A-20: 배경·벽지 이중 상태, 도포율, 저장 라운드트립.
            ("배경·벽지 규칙", () => TestBackgroundAndWallpaper(config)),
            // A-17/A-20: 전경 충돌·렌더 동기화.
            ("전경 충돌·렌더 동기화", () => TestForegroundCollisionAndRender(config)),
            // A-18/A-20: 지표면 안전 스폰.
            ("지표면 안전 스폰", () => TestSafeSurfaceSpawn(config)),
            // A-22: 공용 안전 스폰 계약(IWorldSafeSpawnResolver).
            ("공용 안전 스폰 계약", () => TestWorldSafeSpawnResolver(config)),
            // A-25: 전경/배경 배치 계약.
            ("전경·배경 배치 계약", () => TestForegroundBackgroundPlacementContracts(config)),
            // A-26: 반경·밀폐 창 오버레이.
            ("반경·밀폐 창 오버레이", TestWorldRangeOverlayRenderer),
            // v27: 펄린 동굴 지표 관통 금지 + cave_max_height.
            ("동굴 지표 관통 금지", () => TestCaveSurfaceProtection(config)),
            // v7/v28: 테라리아급 점프·중력 globals 정본.
            ("플레이어 점프·중력", TestPlayerJumpPhysics),
            // v28: 지표 채굴 — 마우스 공기 칸 → 발밑 고체 보정.
            ("지표 채굴 타깃 보정", TestMiningCellSurfaceFallback),
        };

        var passed = 0;
        var failed = new List<string>();

        try
        {
            foreach (var (name, action) in tests)
            {
                try
                {
                    action();
                    passed++;
                }
                catch (Exception ex)
                {
                    failed.Add(name);
                    Debug.LogError($"[Nyangbingo] Dev A regression test 실패 — {name}: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }
        finally
        {
            Object.DestroyImmediate(config); // WorldGenerationConfig는 읽기 전용으로만 쓰이므로 전체 테스트에서 재사용해도 안전하다.
        }

        if (failed.Count == 0)
            Debug.Log($"[Nyangbingo] Dev A 회귀 테스트 전체 통과 ({passed}/{tests.Length}).");
        else
            Debug.LogError($"[Nyangbingo] Dev A 회귀 테스트 실패 {failed.Count}건 — {string.Join(", ", failed)} ({passed}/{tests.Length} 통과).");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    /// <summary>
    /// 일부 회귀 테스트는 "손상된 입력이 정상적으로 거부되는지"를 확인하기 위해 프로덕션 코드가 의도적으로
    /// 남기는 Debug.LogError를 그대로 유발한다(예: WorldSessionController.LoadSnapshot의 검증 실패 경로).
    /// 이런 로그는 실제 회귀가 아닌데도 콘솔에서 오류처럼 보여 매번 혼동을 준다 — action 실행 동안만 Unity
    /// 로거를 잠시 꺼서 콘솔을 깨끗하게 유지한다(예외가 나도 finally에서 반드시 복구).
    /// </summary>
    private static T RunWithSuppressedLogs<T>(Func<T> action)
    {
        var wasEnabled = Debug.unityLogger.logEnabled;
        Debug.unityLogger.logEnabled = false;
        try
        {
            return action();
        }
        finally
        {
            Debug.unityLogger.logEnabled = wasEnabled;
        }
    }

    /// <summary>
    /// 이 회귀 테스트는 [MenuItem]으로 실행되는 Edit 모드 코드다. Unity는 [ExecuteInEditMode]/[ExecuteAlways]가
    /// 없는 일반 MonoBehaviour의 Awake()/OnEnable()을 Edit 모드에서 자동으로 호출하지 않으므로(Play 모드 전용),
    /// AddComponent&lt;DayNightService&gt;()만으로는 day/isNight/timeOfDayGameSeconds가 전부 C# 기본값(0/false/0f)에
    /// 머문다 — day의 기본값(0)이 Awake()가 설정하는 값(1)과 달라 낮/밤 테스트가 틀리게 실패한다. 리플렉션으로
    /// private Awake()를 직접 호출해 실제 게임 실행 시와 동일한 초기화 상태를 재현한다.
    /// </summary>
    private static void InvokeAwake(DayNightService service)
    {
        var awake = typeof(DayNightService).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);
        awake?.Invoke(service, null);
    }

    // ------------------------------------------------------------------
    // 1) 결정론적 생성 — 같은 seed는 항상 같은 결과, 다른 seed는 실제로 다른 결과.
    // ------------------------------------------------------------------
    private static void TestDeterministicGeneration(WorldGenerationConfig config)
    {
        const int seed = 424242;

        var a = new MapGenerator(config).GenerateDetailed(seed);
        var b = new MapGenerator(config).GenerateDetailed(seed);
        Assert(a.acceptedSeed == b.acceptedSeed, "같은 seed인데 acceptedSeed가 다름");
        Assert(a.spawnPoint == b.spawnPoint, "같은 seed인데 스폰 위치가 다름");
        Assert(a.altarPosition == b.altarPosition, "같은 seed인데 제단 위치가 다름");
        Assert(TilesEqual(a.tiles, b.tiles, a.width, a.height), "같은 seed인데 타일 배열이 다름");

        var c = new MapGenerator(config).GenerateDetailed(seed + 999);
        Assert(!TilesEqual(a.tiles, c.tiles, a.width, a.height), "다른 seed인데 완전히 같은 타일 배열이 생성됨");

        Debug.Log("[Nyangbingo] Dev A deterministic generation test completed.");
    }

    private static bool TilesEqual(TileData[,] left, TileData[,] right, int width, int height)
    {
        for (var x = 0; x < width; x++)
        for (var y = 0; y < height; y++)
        {
            if (!string.Equals(left[x, y].elementType, right[x, y].elementType, StringComparison.Ordinal)) return false;
        }
        return true;
    }

    // ------------------------------------------------------------------
    // 2) 상자 분포 — 정확히 20개, 지역별 개수, 중복 ID 없음, seed로 결정론적.
    // ------------------------------------------------------------------
    private static void TestChestDistribution(WorldGenerationConfig config)
    {
        const int seed = 13579;

        var result = new MapGenerator(config).GenerateDetailed(seed);
        Assert(result.chests.Count == config.TotalChestCount, $"상자 개수 불일치: {result.chests.Count} != {config.TotalChestCount}");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var perRegion = new Dictionary<ChestRegion, int>();
        foreach (var chest in result.chests)
        {
            Assert(ids.Add(chest.id), $"중복된 상자 ID 발견: {chest.id}");
            Assert(result.tiles[chest.position.x, chest.position.y].IsAir,
                $"상자 셀에 전경 타일이 겹침: {chest.id} at {chest.position}");
            perRegion.TryGetValue(chest.region, out var count);
            perRegion[chest.region] = count + 1;
        }

        Assert(GetOrZero(perRegion, ChestRegion.Ruins) == config.ChestCountRuins, "폐허 상자 개수가 설정값과 다름");
        Assert(GetOrZero(perRegion, ChestRegion.Upper) == config.ChestCountUpper, "상층 상자 개수가 설정값과 다름");
        Assert(GetOrZero(perRegion, ChestRegion.Middle) == config.ChestCountMiddle, "중층 상자 개수가 설정값과 다름");
        Assert(GetOrZero(perRegion, ChestRegion.Deep) == config.ChestCountDeep, "심층 상자 개수가 설정값과 다름");

        var repeat = new MapGenerator(config).GenerateDetailed(seed);
        Assert(repeat.chests.Count == result.chests.Count, "같은 seed인데 상자 개수가 다름");
        for (var i = 0; i < result.chests.Count; i++)
        {
            Assert(result.chests[i].id == repeat.chests[i].id && result.chests[i].position == repeat.chests[i].position,
                $"같은 seed인데 상자 배치가 다름 (index {i})");
        }

        Debug.Log("[Nyangbingo] Dev A chest distribution test completed.");
    }

    private static int GetOrZero(Dictionary<ChestRegion, int> map, ChestRegion region) => map.TryGetValue(region, out var value) ? value : 0;

    // ------------------------------------------------------------------
    // A-10) 지층 깊이 45/45/45/5 정본 교정 + mineral-tiers.csv depth_min/depth_max 정합성.
    // ------------------------------------------------------------------
    private static void TestLayerDepthAndMineralRanges(WorldGenerationConfig config)
    {
        // 완료 조건 1: 설정 에셋(=CreateDefault()가 만드는 것과 동일한 인스펙터 기본값)이 정본 두께를 쓰는지.
        Assert(config.UpperLayerThickness == 45, $"T1(상층) 두께가 45가 아님(실제 {config.UpperLayerThickness}) — layer_t1_depth=45 위반");
        Assert(config.MiddleLayerThickness == 45, $"T2(중층) 두께가 45가 아님(실제 {config.MiddleLayerThickness}) — layer_t2_depth=90 위반");
        Assert(config.BedrockThickness == 5, $"경계암 두께가 5가 아님(실제 {config.BedrockThickness}) — bedrock_depth=140 위반");

        // 완료 조건 1(엄격 판정): CreateDefault()는 C# 필드 기본값만 반영하므로, 실제 프로젝트에 저장된
        // .asset 인스펙터 값이 코드 기본값과 따로 굳어(stale) 있어도 이 테스트만으로는 못 잡는다.
        // 실제 씬/게임이 참조하는 Assets/Data/SO/WorldGenerationConfig.asset을 직접 로드해 대조한다.
        var projectAsset = AssetDatabase.LoadAssetAtPath<WorldGenerationConfig>("Assets/Data/SO/WorldGenerationConfig.asset");
        Assert(projectAsset != null, "Assets/Data/SO/WorldGenerationConfig.asset을 찾을 수 없음");
        if (projectAsset != null)
        {
            Assert(projectAsset.UpperLayerThickness == config.UpperLayerThickness,
                $"프로젝트 에셋의 T1 두께({projectAsset.UpperLayerThickness})가 CreateDefault()({config.UpperLayerThickness})와 다름 — 에셋이 구버전 값으로 굳어있음");
            Assert(projectAsset.MiddleLayerThickness == config.MiddleLayerThickness,
                $"프로젝트 에셋의 T2 두께({projectAsset.MiddleLayerThickness})가 CreateDefault()({config.MiddleLayerThickness})와 다름 — 에셋이 구버전 값으로 굳어있음");
            Assert(projectAsset.BedrockThickness == config.BedrockThickness,
                $"프로젝트 에셋의 경계암 두께({projectAsset.BedrockThickness})가 CreateDefault()({config.BedrockThickness})와 다름 — 에셋이 구버전 값으로 굳어있음");
            Assert(Mathf.Approximately(projectAsset.SurfaceBaseHeightRatio, config.SurfaceBaseHeightRatio),
                $"프로젝트 에셋의 surfaceBaseHeightRatio({projectAsset.SurfaceBaseHeightRatio})가 CreateDefault()({config.SurfaceBaseHeightRatio})와 다름");
            Assert(projectAsset.OreVeins.Length == config.OreVeins.Length, "프로젝트 에셋의 OreVeins 개수가 CreateDefault()와 다름");
            for (var i = 0; i < Mathf.Min(projectAsset.OreVeins.Length, config.OreVeins.Length); i++)
            {
                var a = projectAsset.OreVeins[i];
                var b = config.OreVeins[i];
                Assert(a.elementType == b.elementType && a.depthMin == b.depthMin && a.depthMax == b.depthMax,
                    $"프로젝트 에셋의 OreVeins[{i}]({a.elementType}, {a.depthMin}~{a.depthMax})가 " +
                    $"CreateDefault()({b.elementType}, {b.depthMin}~{b.depthMax})와 다름 — 에셋이 depthMin/depthMax 없이 구버전으로 저장돼 있으면 0~0으로 굳어 레거시 레이어 배치로 몰래 폴백함");
            }
        }

        // 완료 조건 2: T1/T2/T3/경계암 경계 테스트 — surface_y=20(±노이즈 없는 평균선) 기준 정확한 경계에서
        // 레이어가 바뀌는지, 실제 생성 코드(ClassifyLayer)를 리플렉션으로 그대로 호출해 검증한다.
        var averageSurfaceY = Mathf.RoundToInt(config.MapHeight * config.SurfaceBaseHeightRatio);
        Assert(averageSurfaceY == 139, $"평균 지표 y가 139가 아님(실제 {averageSurfaceY}) — surface_y=20(맵 상단 기준) 위반");

        AssertClassifiedLayer(averageSurfaceY, averageSurfaceY, config, WorldLayer.Upper, "depth 1(지표 자신)");
        AssertClassifiedLayer(averageSurfaceY - 44, averageSurfaceY, config, WorldLayer.Upper, "depth 45(T1 최하단)");
        AssertClassifiedLayer(averageSurfaceY - 45, averageSurfaceY, config, WorldLayer.Middle, "depth 46(T2 최상단)");
        AssertClassifiedLayer(averageSurfaceY - 89, averageSurfaceY, config, WorldLayer.Middle, "depth 90(T2 최하단)");
        AssertClassifiedLayer(averageSurfaceY - 90, averageSurfaceY, config, WorldLayer.Deep, "depth 91(T3 최상단)");
        AssertClassifiedLayer(averageSurfaceY - 134, averageSurfaceY, config, WorldLayer.Deep, "depth 135(T3 최하단)");
        AssertClassifiedLayer(config.BedrockThickness - 1, averageSurfaceY, config, WorldLayer.Bedrock, "경계암(맵 최하단 5행)");
        AssertClassifiedLayer(config.BedrockThickness, averageSurfaceY, config, WorldLayer.Deep, "경계암 바로 위(심층 최하단)");

        // mineral-tiers.csv를 직접 읽어 WorldGenerationConfig.OreVeins의 depthMin/depthMax가 CSV와 완전히
        // 일치하는지 대조한다(완료 조건: 지층 수동 두께와 광물 CSV 깊이가 서로 다른 기준을 쓰지 않도록 통일 —
        // CSV가 정본이므로 두 값이 갈리면 WorldGenerationConfig 쪽을 CSV에 맞춰야 한다).
        var csvRows = NyangbingoCsvUtility.ReadRows("Assets/Data/CSV/mineral-tiers.csv");
        var depthByResourceId = new Dictionary<string, (int min, int max)>(StringComparer.Ordinal);
        foreach (var row in csvRows)
        {
            if (!row.TryGetValue("resource_id", out var id) || !row.TryGetValue("depth_min", out var minText) ||
                !row.TryGetValue("depth_max", out var maxText)) continue;
            if (!int.TryParse(minText, out var depthMin) || !int.TryParse(maxText, out var depthMax)) continue;
            depthByResourceId[id] = (depthMin, depthMax);
        }

        var profiles = config.OreVeins;
        var matched = 0;
        foreach (var profile in profiles)
        {
            Assert(depthByResourceId.TryGetValue(profile.elementType, out var csvDepth),
                $"'{profile.elementType}'가 mineral-tiers.csv에 없음 — resource_id 불일치");
            Assert(profile.depthMin == csvDepth.min && profile.depthMax == csvDepth.max,
                $"'{profile.elementType}' depthMin~depthMax({profile.depthMin}~{profile.depthMax})가 " +
                $"mineral-tiers.csv({csvDepth.min}~{csvDepth.max})와 다름");
            matched++;
        }
        Assert(matched == profiles.Length, $"WorldGenerationConfig.OreVeins 7종 중 {profiles.Length - matched}개가 CSV와 대조되지 않음");

        // 완료 조건 3: 실제로 생성된 월드에서 각 광물 타일이 자신의 depth_min~depth_max 밖에 배치되지 않았는지.
        const int seed = 987654;
        var result = new MapGenerator(config).GenerateDetailed(seed);
        Assert(result.surfaceHeights != null && result.surfaceHeights.Length == result.width,
            "GenerateDetailed 결과에 surfaceHeights가 채워지지 않음(A-10 테스트 지원용 필드)");

        var depthConstrainedTypes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var profile in profiles) if (profile.depthMax > 0) depthConstrainedTypes.Add(profile.elementType);

        var checkedTiles = 0;
        var violations = 0;
        for (var x = 0; x < result.width; x++)
        {
            var surfaceY = result.surfaceHeights[x];
            for (var y = 0; y < result.height; y++)
            {
                var elementType = result.tiles[x, y].elementType;
                if (!depthConstrainedTypes.Contains(elementType)) continue;
                if (!depthByResourceId.TryGetValue(elementType, out var range)) continue;

                checkedTiles++;
                var depth = surfaceY - y + 1;
                if (depth < range.min || depth > range.max) violations++;
            }
        }
        Assert(checkedTiles > 0, "테스트 시드에서 depth 제약이 걸린 광물 타일을 하나도 찾지 못함 — 픽스처 시드 교체 필요");
        Assert(violations == 0, $"depth_min~depth_max 범위를 벗어난 광물 타일 {violations}/{checkedTiles}개 발견");

        Debug.Log("[Nyangbingo] Dev A layer depth & mineral range test completed.");
    }

    private static void AssertClassifiedLayer(int y, int surfaceY, WorldGenerationConfig config, WorldLayer expected, string label)
    {
        var method = typeof(MapGenerator).GetMethod("ClassifyLayer", BindingFlags.NonPublic | BindingFlags.Static);
        var actual = (WorldLayer)method.Invoke(null, new object[] { y, surfaceY, config });
        Assert(actual == expected, $"레이어 경계 불일치({label}): y={y}, surfaceY={surfaceY} → 예상 {expected}, 실제 {actual}");
    }

    // ------------------------------------------------------------------
    // 3) 타일 변경 이력 저장/복원 — 정상 재생은 성공, 손상된 레코드는 원자적으로 실패(전체 거부).
    // ------------------------------------------------------------------
    private static void TestTileRestoreAtomicity(WorldGenerationConfig config)
    {
        const int seed = 555;

        var original = new MapGenerator(config).GenerateDetailed(seed);
        var liveService = new TileService(original.tiles, null, null, seed);

        var breakable = FindCellWithHardness(original.tiles, original.width, original.height);
        Assert(liveService.TryBreakForeground(breakable, 3, out _, out _), "테스트 픽스처: 파괴 가능한 칸을 찾지 못함");
        var records = liveService.GetTileChangeRecords();
        Assert(records.Count == 1, "파괴 후 변경 이력이 정확히 1건 기록되지 않음");

        // 정상 재생: 같은 seed로 다시 생성한 새 배열 위에 replay하면 성공하고, 파괴된 칸이 그대로 반영돼야 한다.
        var replay = new MapGenerator(config).GenerateDetailed(seed);
        var replayService = new TileService(replay.tiles, null, null, seed);
        Assert(replayService.RestoreTileChanges(records), "정상 타일 변경 이력 재생이 실패함");
        Assert(replay.tiles[breakable.x, breakable.y].IsAir, "재생 후에도 파괴된 칸이 원래 상태로 남아 있음");

        // 같은 셀의 설치 -> 제거는 마지막 제거 하나로 덮어쓰지 않고 순서대로 보존해야 한다.
        var roundTripWorld = new MapGenerator(config).GenerateDetailed(seed);
        var roundTripService = new TileService(roundTripWorld.tiles, null, null, seed);
        var roundTripCell = FindAirCellNearSpawn(roundTripService, roundTripWorld);
        Assert(roundTripService.TryPlaceForeground(roundTripCell, WorldTileTypes.Dirt),
            "설치-제거 이력 테스트용 dirt 설치 실패");
        Assert(roundTripService.TryBreakForeground(roundTripCell, 3, out _, out _),
            "설치-제거 이력 테스트용 dirt 제거 실패");
        var roundTripRecords = roundTripService.GetTileChangeRecords();
        Assert(roundTripRecords.Count == 2 && roundTripRecords[0].placed && !roundTripRecords[1].placed,
            "동일 셀 설치-제거 이력이 실행 순서대로 보존되지 않음");
        var roundTripReplay = new MapGenerator(config).GenerateDetailed(seed);
        var roundTripReplayService = new TileService(roundTripReplay.tiles, null, null, seed);
        Assert(roundTripReplayService.RestoreTileChanges(roundTripRecords) &&
               roundTripReplay.tiles[roundTripCell.x, roundTripCell.y].IsAir,
            "동일 셀 설치-제거 이력을 재생하지 못함");

        // 구버전은 위 두 기록을 마지막 제거 하나로 압축했다. 원본도 공기라면 상쇄된 이력으로 이관한다.
        var legacyCollapsedRoundTrip = new List<TileChangeRecord>
        {
            new TileChangeRecord
            {
                x = roundTripCell.x,
                y = roundTripCell.y,
                z = 0,
                tileId = WorldTileTypes.Dirt,
                placed = false
            }
        };
        var legacyRoundTripWorld = new MapGenerator(config).GenerateDetailed(seed);
        var legacyRoundTripService = new TileService(legacyRoundTripWorld.tiles, null, null, seed);
        Assert(legacyRoundTripService.RestoreTileChanges(legacyCollapsedRoundTrip) &&
               legacyRoundTripService.GetTileChangeRecords().Count == 0,
            "구버전의 상쇄된 설치-제거 기록을 공기 상태로 이관하지 못함");

        // 구 생성기에서 상자와 겹친 흙을 제거한 세이브는 새 생성기의 상자 셀이 이미 공기다.
        // 상자 좌표로 명시된 경우에만 이 제거 기록을 이미 적용된 상태로 이관한다.
        var chest = original.chests[0];
        var legacyChestRemoval = new List<TileChangeRecord>
        {
            new TileChangeRecord
            {
                x = chest.position.x,
                y = chest.position.y,
                z = 0,
                tileId = WorldTileTypes.Dirt,
                placed = false
            }
        };
        var strictChestAttempt = new MapGenerator(config).GenerateDetailed(seed);
        Assert(!new TileService(strictChestAttempt.tiles, null, null, seed)
                .RestoreTileChanges(legacyChestRemoval),
            "일반 복원은 이미 공기인 셀의 제거 기록을 허용하면 안 됨");
        var migratedChestAttempt = new MapGenerator(config).GenerateDetailed(seed);
        var migratedChestService = new TileService(migratedChestAttempt.tiles, null, null, seed);
        var allowedChestCells = new HashSet<Vector3Int>
        {
            new Vector3Int(chest.position.x, chest.position.y, 0)
        };
        Assert(migratedChestService.RestoreTileChanges(legacyChestRemoval, allowedChestCells),
            "구버전 상자 겹침 제거 기록을 현재 공기 셀로 이관하지 못함");
        Assert(migratedChestService.GetTileChangeRecords().Count == 1,
            "이관한 상자 제거 기록이 다음 저장을 위해 유지되지 않음");

        // 손상된 레코드(기반암 좌표에 "설치" 기록을 끼워넣음) — 보호 타일 위 설치는 항상 거부돼야 하고,
        // 그 앞의 정상 레코드까지 포함해 전체가 실패로 처리돼야 한다(원자성).
        var bedrockCell = FindCellWithElementType(original.tiles, original.width, original.height, WorldTileTypes.Bedrock);
        var corrupted = new List<TileChangeRecord>(records)
        {
            new TileChangeRecord { x = bedrockCell.x, y = bedrockCell.y, z = 0, tileId = WorldTileTypes.Bedrock, placed = true }
        };
        var corruptedAttempt = new MapGenerator(config).GenerateDetailed(seed);
        var corruptedService = new TileService(corruptedAttempt.tiles, null, null, seed);
        Assert(!corruptedService.RestoreTileChanges(corrupted), "보호 타일(빙암) 위에 설치하는 손상된 레코드가 거부되지 않음");

        // 범위 밖 좌표 레코드도 거부돼야 한다.
        var outOfBounds = new List<TileChangeRecord> { new TileChangeRecord { x = -1, y = -1, z = 0, tileId = WorldTileTypes.Dirt, placed = true } };
        var outOfBoundsAttempt = new MapGenerator(config).GenerateDetailed(seed);
        var outOfBoundsService = new TileService(outOfBoundsAttempt.tiles, null, null, seed);
        Assert(!outOfBoundsService.RestoreTileChanges(outOfBounds), "범위 밖 좌표 레코드가 거부되지 않음");

        // 알 수 없는 tileId도 거부돼야 한다.
        var unknownType = new List<TileChangeRecord> { new TileChangeRecord { x = breakable.x, y = breakable.y, z = 0, tileId = "definitely_not_a_real_tile", placed = false } };
        var unknownAttempt = new MapGenerator(config).GenerateDetailed(seed);
        var unknownService = new TileService(unknownAttempt.tiles, null, null, seed);
        Assert(!unknownService.RestoreTileChanges(unknownType), "알 수 없는 tileId 레코드가 거부되지 않음");

        Debug.Log("[Nyangbingo] Dev A tile restore atomicity test completed.");
    }

    private static Vector3Int FindCellWithHardness(TileData[,] tiles, int width, int height)
    {
        for (var x = 0; x < width; x++)
        for (var y = 0; y < height; y++)
        {
            var tile = tiles[x, y];
            if (tile.hardness > 0 && tile.elementType != WorldTileTypes.Bedrock && tile.elementType != WorldTileTypes.IceAltar)
                return new Vector3Int(x, y, 0);
        }
        throw new InvalidOperationException("테스트 픽스처: 파괴 가능한 타일을 찾지 못함");
    }

    private static Vector3Int FindCellWithElementType(TileData[,] tiles, int width, int height, string elementType)
    {
        for (var x = 0; x < width; x++)
        for (var y = 0; y < height; y++)
        {
            if (string.Equals(tiles[x, y].elementType, elementType, StringComparison.Ordinal)) return new Vector3Int(x, y, 0);
        }
        throw new InvalidOperationException($"테스트 픽스처: elementType '{elementType}' 타일을 찾지 못함");
    }

    // ------------------------------------------------------------------
    // 4) SealSystem — 완전 밀폐 판정, 누수 판정, 인공 타일 꼼수 방지, 냉기원 게이트, 밤 시작 재계산.
    // ------------------------------------------------------------------
    private static void TestSealSystem()
    {
        // 5x3 그리드: (1,1)이 유일한 내부 공기 칸이고, 사방이 자연석(natural terrain)으로 둘러싸여 있다.
        // (0,1)만 그리드의 실제 서쪽 가장자리(x=0)에 있어, 이 칸을 파괴하면 서쪽 이웃이 맵 밖(-1,1)이 되어
        // 반드시 누수(leak face)가 발생한다 — 배치를 다른 시드/생성 결과에 의존하지 않는 고정 픽스처다.
        const int width = 5, height = 3;
        var tiles = new TileData[width, height];
        for (var x = 0; x < width; x++)
        for (var y = 0; y < height; y++)
            tiles[x, y] = new TileData { elementType = WorldTileTypes.Stone, hardness = 2, isNaturalTerrain = true };

        var roomCell = new Vector3Int(1, 1, 0);
        var wallCell = new Vector3Int(0, 1, 0);
        tiles[roomCell.x, roomCell.y] = TileData.CreateCaveAir(WorldTileTypes.BackgroundStone);

        var tileService = new TileService(tiles, null, null, 1);
        var coolingProvider = new FakeCoolingSourceProvider { IsColdSourceActive = false };
        var sealSystem = new SealSystem(tileService, coolingSourceProvider: coolingProvider);
        try
        {
            var roomWorldPos = new Vector2(roomCell.x + 0.5f, roomCell.y + 0.5f);
            Assert(sealSystem.IsInsideSealedArea(roomWorldPos), "완전히 자연석으로 둘러싸인 방이 밀폐로 판정되지 않음");

            // LeakFaceCount/TemperaturePercent는 "주 관찰 지점"(primaryWatchPoint) 기준으로 계산되므로,
            // 단순 RegisterWatchPoint만으로는 두 값이 항상 0을 반환한다 — SetPrimaryWatchPoint로 등록해야 한다.
            sealSystem.SetPrimaryWatchPoint(roomCell);
            var lastSealed = true;
            var eventFireCount = 0;
            void OnWatchPointChanged(Vector3Int cell, bool sealedNow)
            {
                if (cell != roomCell) return;
                eventFireCount++;
                lastSealed = sealedNow;
            }
            sealSystem.WatchPointSealChanged += OnWatchPointChanged;

            // 벽 한 칸(맵 가장자리)을 파괴 → 맵 밖으로 뚫려 누수가 발생해야 한다.
            Assert(tileService.TryBreakForeground(wallCell, 3, out _, out _), "테스트 픽스처: 벽 파괴 실패");
            Assert(eventFireCount == 1, $"벽이 뚫렸는데 WatchPointSealChanged가 정확히 한 번 발행되지 않음(실제 {eventFireCount}회)");
            Assert(!lastSealed, "맵 밖으로 뚫렸는데 여전히 밀폐로 판정됨");
            Assert(!sealSystem.IsInsideSealedArea(roomWorldPos), "누수 발생 후에도 밀폐로 판정됨");
            Assert(sealSystem.LeakFaceCount > 0, "누수가 발생했는데 LeakFaceCount가 0임");

            // 인공 타일로 같은 자리를 다시 막아도 밀폐로 인정되면 안 된다(v15 QA 꼼수 방지).
            Assert(tileService.TryPlaceForeground(wallCell, WorldTileTypes.Stone), "테스트 픽스처: 인공 벽 설치 실패");
            Assert(!sealSystem.IsInsideSealedArea(roomWorldPos), "인공 타일로 막았는데 밀폐로 인정됨(v15 QA 꼼수 방지 위반)");

            // 밤 시작 트리거 — 캐시를 전부 지우고 관찰 지점을 재계산해도 여전히 인공 벽이라 밀폐가 아니어야 한다.
            GameEvents.RaiseNightStart();
            Assert(!sealSystem.IsWatchPointSealed(roomCell), "밤 시작 재계산 후에도 인공 벽이 밀폐로 인정됨");

            // 냉기원 비활성 상태에서는 완전 밀폐라도 TemperaturePercent가 0이어야 한다(방어 효과 없음).
            Assert(sealSystem.TemperaturePercent == 0f, "냉기원이 꺼져 있는데 TemperaturePercent > 0");
        }
        finally
        {
            sealSystem.Dispose();
        }

        Debug.Log("[Nyangbingo] Dev A seal system test completed.");
    }

    // ------------------------------------------------------------------
    // A-11/A-12/A-13) SealSystem 57×25 코어 창, SetSealCoreCell API, 냉기원 상한 연동 — 회귀 13종.
    // ------------------------------------------------------------------
    private static void TestSealSystemCoreWindow()
    {
        TestSealedRoomAreaProportionalTemperature();       // 항목 1
        TestSealedRoomAtOrAboveTargetCellsIsFullTemperature(); // 항목 2
        TestSingleLeakFaceZeroesTemperature();              // 항목 3
        TestLeakOutsideWindowIsDetected();                  // 항목 4
        TestNaturalTerrainAtWindowBoundarySealsNormally();  // 항목 5
        TestNoCoreCellYieldsZero();                         // 항목 6
        TestNoColdSourceYieldsZero();                       // 항목 7
        TestColdSourceCaps();                                // 항목 8/9/10
        TestHighestCapAmongMultipleSourcesApplies();        // 항목 11
        TestCacheRecalculatesOnTileChangeAndNightStart();   // 항목 12
        // 항목 13(저장/로드 후 인스턴스·관찰 지점·코어 위치 유지)은 실제 세션 경로가 필요해
        // "월드 세션 라운드트립" 테스트(TestWorldSessionRoundTrip) 안에서 함께 검증한다.

        Debug.Log("[Nyangbingo] Dev A seal system core window (57x25) test completed.");
    }

    private static TileData[,] BuildStoneField(int width, int height)
    {
        var tiles = new TileData[width, height];
        for (var x = 0; x < width; x++)
        for (var y = 0; y < height; y++)
            tiles[x, y] = TileData.CreateNaturalWithBackground(WorldTileTypes.Stone, 2, WorldTileTypes.BackgroundStone);
        return tiles;
    }

    private static void CarveRoom(TileData[,] tiles, RectInt room)
    {
        for (var x = room.xMin; x < room.xMax; x++)
        for (var y = room.yMin; y < room.yMax; y++)
            tiles[x, y] = TileData.CreateAir(); // A-16: 밀폐 방 내부도 전경·배경 모두 비움(벽지로 도포 가능).
    }

    // 항목 1: 완전히 밀폐된 작은 방(20칸, seal_target_cells=240 미달)의 면적 비례 온도.
    private static void TestSealedRoomAreaProportionalTemperature()
    {
        const int width = 9, height = 8;
        var room = new RectInt(2, 2, 5, 4); // 20칸.
        var tiles = BuildStoneField(width, height);
        CarveRoom(tiles, room);

        var tileService = new TileService(tiles, null, null, 1);
        var cooling = new FakeCoolingSourceProvider { CoolingCapPercent = 100f };
        var sealSystem = new SealSystem(tileService, coolingSourceProvider: cooling);
        try
        {
            sealSystem.SetSealCoreCell(new Vector3Int(room.x + room.width / 2, room.y + room.height / 2, 0));
            Assert(sealSystem.LeakFaceCount == 0, "완전히 밀폐된 방인데 LeakFaceCount가 0이 아님");

            var expected = 100f * (room.width * room.height) / 240f; // seal_target_cells 기본값 240.
            Assert(Mathf.Approximately(sealSystem.TemperaturePercent, expected),
                $"면적 비례 온도가 예상과 다름(실제 {sealSystem.TemperaturePercent}, 예상 {expected})");
        }
        finally { sealSystem.Dispose(); }
    }

    // 항목 2: 240칸 이상 밀폐 시 기본 온도(SealPercent) 100%.
    private static void TestSealedRoomAtOrAboveTargetCellsIsFullTemperature()
    {
        const int roomWidth = 20, roomHeight = 13; // 260칸 ≥ 240.
        const int width = roomWidth + 4, height = roomHeight + 4;
        var room = new RectInt(2, 2, roomWidth, roomHeight);
        var tiles = BuildStoneField(width, height);
        CarveRoom(tiles, room);

        var tileService = new TileService(tiles, null, null, 1);
        var cooling = new FakeCoolingSourceProvider { CoolingCapPercent = 100f };
        var sealSystem = new SealSystem(tileService, coolingSourceProvider: cooling);
        try
        {
            sealSystem.SetSealCoreCell(new Vector3Int(room.x + roomWidth / 2, room.y + roomHeight / 2, 0));
            Assert(sealSystem.LeakFaceCount == 0, "260칸 방인데 누출이 발생함 — 테스트 픽스처 오류");
            Assert(Mathf.Approximately(sealSystem.SealPercent, 1f), $"260칸(≥240) 밀폐인데 SealPercent가 1이 아님(실제 {sealSystem.SealPercent})");
            Assert(Mathf.Approximately(sealSystem.TemperaturePercent, 100f), $"260칸 밀폐 + 냉기원 100% 상한인데 TemperaturePercent가 100이 아님(실제 {sealSystem.TemperaturePercent})");
        }
        finally { sealSystem.Dispose(); }
    }

    // 항목 3: 누출면 1개 발생 시 0%.
    private static void TestSingleLeakFaceZeroesTemperature()
    {
        const int width = 9, height = 8;
        var room = new RectInt(2, 2, 5, 4);
        var tiles = BuildStoneField(width, height);
        CarveRoom(tiles, room);

        var tileService = new TileService(tiles, null, null, 1);
        var cooling = new FakeCoolingSourceProvider { CoolingCapPercent = 100f };
        var sealSystem = new SealSystem(tileService, coolingSourceProvider: cooling);
        try
        {
            var core = new Vector3Int(room.x + room.width / 2, room.y + room.height / 2, 0);
            sealSystem.SetSealCoreCell(core);
            Assert(sealSystem.TemperaturePercent > 0f, "누출 전인데 온도가 0임 — 테스트 픽스처 오류");

            // 경계 벽 하나를 인공(비자연) 벽으로 교체해 누출 1개를 만든다(v15 QA 꼼수 방지와 동일한 방식).
            var wallCell = new Vector3Int(room.xMin - 1, core.y, 0);
            Assert(tileService.TryBreakForeground(wallCell, 3, out _, out _), "테스트 픽스처: 벽 파괴 실패");
            Assert(tileService.TryPlaceForeground(wallCell, WorldTileTypes.Stone), "테스트 픽스처: 인공 벽 설치 실패");

            Assert(sealSystem.LeakFaceCount > 0, "인공 벽으로 막았는데 LeakFaceCount가 0임");
            Assert(sealSystem.TemperaturePercent == 0f, "누출면 1개 발생 후에도 TemperaturePercent > 0");
            Assert(sealSystem.SealPercent == 0f, "누출면 1개 발생 후에도 SealPercent > 0");
        }
        finally { sealSystem.Dispose(); }
    }

    // 항목 4: 57×25(테스트에서는 축소된 창) 밖으로 공기가 이어지면 누출 처리.
    private static void TestLeakOutsideWindowIsDetected()
    {
        const int rx = 3, ry = 2; // 창 크기 자체는 기본 57×25와 무관하게 같은 로직 — 픽스처를 작게 유지하기 위한 값.
        const int width = 20, height = 12;
        var tiles = BuildStoneField(width, height);

        var core = new Vector3Int(10, 6, 0);
        var room = new RectInt(core.x - 1, core.y - 1, 3, 3);
        CarveRoom(tiles, room);
        // 방의 오른쪽 벽을 뚫어 창 경계(core.x+rx)를 넘어 core.x+rx+1까지 이어지는 복도를 만든다.
        for (var x = room.xMax; x <= core.x + rx + 1; x++)
            tiles[x, core.y] = TileData.CreateCaveAir(WorldTileTypes.BackgroundStone);

        var tileService = new TileService(tiles, null, null, 1);
        var sealSystem = new SealSystem(tileService, sealWindowRadiusX: rx, sealWindowRadiusY: ry);
        try
        {
            sealSystem.SetSealCoreCell(core);
            Assert(sealSystem.LeakFaceCount > 0, "창 밖으로 이어지는 공기가 있는데 누출로 집계되지 않음");
            Assert(sealSystem.SealPercent == 0f, "창 밖 누출이 있는데 SealPercent > 0");
            Assert(sealSystem.TryGetCoreLeakCell(out var leakCell),
                "누출이 있는데 제품 진단 HUD가 사용할 대표 누출 셀을 얻지 못함");
            Assert(Mathf.Abs(leakCell.x - core.x) <= rx && Mathf.Abs(leakCell.y - core.y) <= ry,
                "대표 누출 셀이 코어 진단 창 밖을 가리킴");
            Assert(leakCell == new Vector3Int(room.xMax, core.y, 0),
                "대표 누출 셀은 먼 진단 창 경계가 아니라 실제로 막아야 할 통로 입구를 가리켜야 함");
        }
        finally { sealSystem.Dispose(); }
    }

    // 항목 5: 창 경계가 자연 지형으로 막혀 있으면(경계 자체가 창 가장자리에 있어도) 정상 밀폐.
    private static void TestNaturalTerrainAtWindowBoundarySealsNormally()
    {
        const int rx = 3, ry = 2;
        const int width = 20, height = 12;
        var tiles = BuildStoneField(width, height);

        var core = new Vector3Int(10, 6, 0);
        // 방 내부가 창 경계(core±rx, core±ry) 바로 안쪽까지 꽉 차 있고, 경계 자체는 자연 벽으로 막혀 있다.
        var room = new RectInt(core.x - rx + 1, core.y - ry + 1, 2 * rx - 1, 2 * ry - 1);
        CarveRoom(tiles, room);

        var tileService = new TileService(tiles, null, null, 1);
        var sealSystem = new SealSystem(tileService, sealWindowRadiusX: rx, sealWindowRadiusY: ry);
        try
        {
            sealSystem.SetSealCoreCell(core);
            Assert(sealSystem.LeakFaceCount == 0, "창 경계가 전부 자연 지형으로 막혀 있는데 누출로 집계됨");
            Assert(sealSystem.SealPercent > 0f, "정상 밀폐인데 SealPercent가 0임");
        }
        finally { sealSystem.Dispose(); }
    }

    // 항목 6: 코어 위치가 없으면 SealPercent/LeakFaceCount/TemperaturePercent 모두 안전하게 0.
    private static void TestNoCoreCellYieldsZero()
    {
        var tiles = BuildStoneField(5, 5);
        CarveRoom(tiles, new RectInt(1, 1, 3, 3));
        var tileService = new TileService(tiles, null, null, 1);
        var cooling = new FakeCoolingSourceProvider { CoolingCapPercent = 100f };
        var sealSystem = new SealSystem(tileService, coolingSourceProvider: cooling);
        try
        {
            Assert(!sealSystem.HasSealCoreCell, "코어를 설정하지 않았는데 HasSealCoreCell이 true임");
            Assert(sealSystem.SealPercent == 0f, "코어가 없는데 SealPercent > 0");
            Assert(sealSystem.LeakFaceCount == 0, "코어가 없는데 LeakFaceCount가 0이 아님(기본값이어야 함)");
            Assert(sealSystem.TemperaturePercent == 0f, "코어가 없는데 TemperaturePercent > 0");
        }
        finally { sealSystem.Dispose(); }
    }

    // 항목 7: 냉기원(Provider)이 연결되지 않았으면 완전 밀폐라도 0%(연결 안 됨=100%로 착각하면 안 됨).
    private static void TestNoColdSourceYieldsZero()
    {
        const int roomWidth = 20, roomHeight = 13;
        const int width = roomWidth + 4, height = roomHeight + 4;
        var room = new RectInt(2, 2, roomWidth, roomHeight);
        var tiles = BuildStoneField(width, height);
        CarveRoom(tiles, room);

        var tileService = new TileService(tiles, null, null, 1);
        var sealSystem = new SealSystem(tileService); // coolingSourceProvider 없음.
        try
        {
            sealSystem.SetSealCoreCell(new Vector3Int(room.x + roomWidth / 2, room.y + roomHeight / 2, 0));
            Assert(sealSystem.SealPercent > 0f, "밀폐는 됐는데 SealPercent가 0임 — 테스트 픽스처 오류");
            Assert(sealSystem.TemperaturePercent == 0f,
                "냉기원 Provider가 연결되지 않았는데 TemperaturePercent > 0 — 연결 안 됨을 100%로 착각");
        }
        finally { sealSystem.Dispose(); }
    }

    // 항목 8/9/10: 물단지 25% · 얼음 항아리 50% · 얼음 저장고/빙정 냉각로 100% 상한이 그대로 반영되는지.
    private static void TestColdSourceCaps()
    {
        AssertCap(25f, "물단지");
        AssertCap(50f, "얼음 항아리");
        AssertCap(100f, "얼음 저장고/빙정 냉각로");

        static void AssertCap(float cap, string label)
        {
            const int roomWidth = 20, roomHeight = 13;
            const int width = roomWidth + 4, height = roomHeight + 4;
            var room = new RectInt(2, 2, roomWidth, roomHeight);
            var tiles = BuildStoneField(width, height);
            CarveRoom(tiles, room);

            var tileService = new TileService(tiles, null, null, 1);
            var cooling = new FakeCoolingSourceProvider { CoolingCapPercent = cap };
            var sealSystem = new SealSystem(tileService, coolingSourceProvider: cooling);
            try
            {
                sealSystem.SetSealCoreCell(new Vector3Int(room.x + roomWidth / 2, room.y + roomHeight / 2, 0));
                Assert(Mathf.Approximately(sealSystem.SealPercent, 1f), $"{label} 테스트: 260칸 밀폐인데 SealPercent가 1이 아님");
                Assert(Mathf.Approximately(sealSystem.TemperaturePercent, cap),
                    $"{label} 상한({cap}%)이 그대로 반영되지 않음(실제 {sealSystem.TemperaturePercent})");
            }
            finally { sealSystem.Dispose(); }
        }
    }

    // 항목 11: 여러 냉기원이 가동 중이면 그중 최고 상한이 적용된다(Provider가 최고값을 공급).
    private static void TestHighestCapAmongMultipleSourcesApplies()
    {
        const int roomWidth = 20, roomHeight = 13;
        const int width = roomWidth + 4, height = roomHeight + 4;
        var room = new RectInt(2, 2, roomWidth, roomHeight);
        var tiles = BuildStoneField(width, height);
        CarveRoom(tiles, room);

        var tileService = new TileService(tiles, null, null, 1);
        var cooling = new FakeCoolingSourceProvider { CoolingCapPercent = 50f };
        var sealSystem = new SealSystem(tileService, coolingSourceProvider: cooling);
        try
        {
            sealSystem.SetSealCoreCell(new Vector3Int(room.x + roomWidth / 2, room.y + roomHeight / 2, 0));
            Assert(Mathf.Approximately(sealSystem.TemperaturePercent, 50f), "낮은 상한 하나만 있을 때 그 값이 반영되지 않음");

            cooling.CoolingCapPercent = 100f; // 더 강한 냉기원(얼음 저장고)이 추가로 가동됐다고 가정.
            Assert(Mathf.Approximately(sealSystem.TemperaturePercent, 100f), "더 높은 상한으로 바뀌었는데 즉시 반영되지 않음");
        }
        finally { sealSystem.Dispose(); }
    }

    // 항목 12: 타일 변경(TileService 경로) + 밤 시작 트리거 후 캐시가 재계산되는지.
    private static void TestCacheRecalculatesOnTileChangeAndNightStart()
    {
        const int width = 5, height = 3;
        var tiles = BuildStoneField(width, height);
        var core = new Vector3Int(1, 1, 0);
        tiles[core.x, core.y] = TileData.CreateCaveAir(WorldTileTypes.BackgroundStone);
        var wallCell = new Vector3Int(0, 1, 0);

        var tileService = new TileService(tiles, null, null, 1);
        var cooling = new FakeCoolingSourceProvider { CoolingCapPercent = 100f };
        var sealSystem = new SealSystem(tileService, coolingSourceProvider: cooling);
        try
        {
            sealSystem.SetSealCoreCell(core);
            Assert(sealSystem.TemperaturePercent > 0f, "밀폐된 방인데 온도가 0임 — 픽스처 오류");

            // TileService를 거치지 않고 배열을 직접 바꿔(OnTileBroken 미발행) 캐시가 즉시 무효화되지 않는지 확인한다.
            tiles[wallCell.x, wallCell.y] = TileData.CreateAir();
            Assert(sealSystem.TemperaturePercent > 0f,
                "이벤트 없이 배열만 바꿨는데 캐시가 즉시 재계산됨(캐시 무효화 조건이 타일 배치/파괴 이벤트여야 함)");

            GameEvents.RaiseNightStart(); // 요구사항 10: 밤 시작 시 캐시 전체 재계산.
            Assert(sealSystem.TemperaturePercent == 0f, "밤 시작 후에도 캐시가 재계산되지 않아 무너진 벽이 반영되지 않음");
        }
        finally { sealSystem.Dispose(); }
    }

    private sealed class FakeCoolingSourceProvider : ICoolingSourceProvider
    {
        public bool IsColdSourceActive { get; set; }

        /// <summary>A-12: 0~100 상한. 기본값 0 — "Provider가 있지만 아무 냉기원도 안 켜짐"과 동일한 안전한 기본.</summary>
        public float CoolingCapPercent { get; set; }
    }

    /// <summary>§5 항목 2 회귀용 최소 더블 — WorldSessionController.BindTickDriver가 참조를 그대로 보관/노출하는지만 확인한다.</summary>
    private sealed class FakeTickDriver : IGameSecondsTickDriver
    {
        public void Register(IGameSecondsTickable tickable) { }
        public void Unregister(IGameSecondsTickable tickable) { }
    }

    // ------------------------------------------------------------------
    // 5) 낮/밤 전환 — 900초 후 밤, 밤 시작 540초 후 새벽, 이벤트 순서, startAtNight 버그 회귀(A-02).
    // ------------------------------------------------------------------
    private static void TestDayNightTransitions()
    {
        var go = new GameObject("DevA_DayNightRegressionTest_Day");
        try
        {
            var dayNight = go.AddComponent<DayNightService>();
            InvokeAwake(dayNight); // 에디터(Edit 모드) 메뉴 실행이라 MonoBehaviour.Awake()가 자동 호출되지 않는다 — 직접 트리거.
            var order = new List<string>();
            void OnNight() => order.Add("Night");
            void OnWarn() => order.Add("Warn");
            void OnDay() => order.Add("Day");
            GameEvents.OnNightStart += OnNight;
            GameEvents.OnDawnWarning += OnWarn;
            GameEvents.OnDayStart += OnDay;
            try
            {
                dayNight.Tick(900f); // 기본값: 낮 900초.
                Assert(dayNight.IsNight, "900초 뒤에도 밤이 시작되지 않음");
                Assert(order.Count == 1 && order[0] == "Night", "OnNightStart 발행 횟수/순서가 예상과 다름");

                dayNight.Tick(540f); // 기본값: 밤 540초 — 정확히 이 시점에 새벽이어야 한다.
                Assert(!dayNight.IsNight, "밤 시작 후 540초가 지났는데도 새벽이 오지 않음");
                Assert(dayNight.Day == 2, "새벽 이후 Day가 정확히 1 증가하지 않음");
                Assert(order.Count == 3 && order[1] == "Warn" && order[2] == "Day",
                    $"Night→Warn→Day 순서가 지켜지지 않음(실제: {string.Join(",", order)})");
            }
            finally
            {
                GameEvents.OnNightStart -= OnNight;
                GameEvents.OnDawnWarning -= OnWarn;
                GameEvents.OnDayStart -= OnDay;
            }
        }
        finally
        {
            Object.DestroyImmediate(go);
        }

        // A-02 회귀: startAtNight == true로 시작하면 정확히 nightDurationSeconds(540초) 후에 새벽이 와야 한다
        // (예전 버그는 dayDurationSeconds + nightDurationSeconds = 1440초가 걸렸다).
        var nightGo = new GameObject("DevA_DayNightRegressionTest_Night");
        try
        {
            var nightService = nightGo.AddComponent<DayNightService>();
            var serialized = new SerializedObject(nightService);
            serialized.FindProperty("startAtNight").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // 에디터(Edit 모드) 메뉴 실행이라 SetActive/AddComponent만으로는 Awake()가 호출되지 않는다 — 필드를
            // 먼저 채운 뒤(위) 직접 Awake()를 트리거해야 startAtNight=true가 실제로 반영된다.
            InvokeAwake(nightService);

            Assert(nightService.IsNight, "startAtNight=true인데 Awake() 이후 IsNight=false로 초기화됨");

            var dawnCount = 0;
            void OnDayStart() => dawnCount++;
            GameEvents.OnDayStart += OnDayStart;
            try
            {
                nightService.Tick(540f);
                Assert(!nightService.IsNight, "startAtNight=true로 시작해 540초가 지났는데도 새벽이 오지 않음(A-02 회귀)");
                Assert(dawnCount == 1, $"새벽 이벤트가 정확히 한 번 발행되지 않음(실제 {dawnCount}회)");
            }
            finally
            {
                GameEvents.OnDayStart -= OnDayStart;
            }
        }
        finally
        {
            Object.DestroyImmediate(nightGo);
        }

        Debug.Log("[Nyangbingo] Dev A day/night transition test completed.");
    }

    // ------------------------------------------------------------------
    // 6) 월드 세션 라운드트립 — 저장→로드 성공 시 상태 복원 + SealSystem 인스턴스/관찰 지점 유지(A-07),
    //    손상된 저장 데이터 로드 시도는 라이브 상태를 전혀 바꾸지 않아야 한다(A-06 트랜잭션 보장).
    // ------------------------------------------------------------------
    private static void TestWorldSessionRoundTrip(WorldGenerationConfig config)
    {
        var rendererGo = new GameObject("DevA_WorldSessionRegressionTest_Renderer");
        var timeServiceGo = new GameObject("DevA_WorldSessionRegressionTest_TimeService");
        WorldSessionController session = null;
        try
        {
            var renderer = BuildMinimalRenderer(rendererGo);
            session = new WorldSessionController(config, renderer, null);

            // §5(개발 B와의 연결 계약) 항목 1/2/7 회귀: TimeService/TickDriver 바인딩과 WorldLoaded 발행.
            var timeService = timeServiceGo.AddComponent<DayNightService>();
            var tickDriver = new FakeTickDriver();
            session.BindTimeService(timeService);
            session.BindTickDriver(tickDriver);
            Assert(ReferenceEquals(session.TimeService, timeService), "BindTimeService 후 session.TimeService가 주입한 인스턴스와 다름");
            Assert(ReferenceEquals(session.TickDriver, tickDriver), "BindTickDriver 후 session.TickDriver가 주입한 인스턴스와 다름");

            var worldLoadedCount = 0;
            void OnWorldLoaded() => worldLoadedCount++;
            session.WorldLoaded += OnWorldLoaded;

            const int seed = 20260716;
            var result = session.StartNewWorld(seed);
            Assert(result.passedValidation, "테스트 시드가 월드 생성 검증을 통과하지 못함 — 픽스처 시드를 교체해야 함");
            Assert(worldLoadedCount == 1, $"StartNewWorld 성공 후 WorldLoaded가 정확히 한 번 발행되지 않음(실제 {worldLoadedCount}회)");

            var breakable = FindCellWithHardness(result.tiles, result.width, result.height);
            Assert(session.TileService.TryBreakForeground(breakable, 3, out _, out _), "라운드트립 테스트용 파괴 실패");

            var watchCell = new Vector3Int(result.spawnPoint.x, result.spawnPoint.y, 0);
            session.SealSystem.RegisterWatchPoint(watchCell);
            // A-13 항목 13: 코어 셀도 SealSystem 인스턴스/관찰 지점과 동일하게 저장·로드 후 유지돼야 한다.
            var coreCell = new Vector3Int(result.altarPosition.x, result.altarPosition.y, 0);
            session.SealSystem.SetSealCoreCell(coreCell);
            var sealSystemBeforeLoad = session.SealSystem;
            var watchPointEventCount = 0;
            void OnWatchPointChanged(Vector3Int _, bool __) => watchPointEventCount++;
            session.SealSystem.WatchPointSealChanged += OnWatchPointChanged;

            var save = new SaveGame();
            Assert(session.CaptureSnapshot(save), "CaptureSnapshot이 실패함");
            Assert(save.tileChanges.Count == 1, "캡처된 타일 변경 이력 개수가 예상과 다름");

            Assert(session.LoadSnapshot(save), "정상 저장 데이터의 로드가 실패함");
            Assert(ReferenceEquals(session.SealSystem, sealSystemBeforeLoad),
                "로드 후 SealSystem 인스턴스가 교체됨(A-07 위반 — Rebind가 아니라 재생성됨)");
            Assert(session.SealSystem.HasSealCoreCell && session.SealSystem.SealCoreCell == coreCell,
                "로드 후 SealSystem의 코어 셀이 유지되지 않음(A-13 항목 13)");
            Assert(session.TileService.GetTile(breakable).IsAir, "로드 후 이전에 파괴한 칸이 복원되지 않음");
            Assert(worldLoadedCount == 2, $"정상 LoadSnapshot 성공 후 WorldLoaded가 추가로 한 번 발행되지 않음(실제 누적 {worldLoadedCount}회)");
            Assert(ReferenceEquals(session.TimeService, timeService), "로드 후 session.TimeService 참조가 바뀜(§5 안정적 접근자 위반)");
            Assert(ReferenceEquals(session.TickDriver, tickDriver), "로드 후 session.TickDriver 참조가 바뀜(§5 안정적 접근자 위반)");

            // 관찰 지점 구독이 로드 후에도 살아있는지: 같은 칸을 다시 부수면(이미 부서진 칸이라 실패하므로)
            // 대신 인접 칸을 부숴 SealSystem이 여전히 정상 동작하는지(예외 없이) 확인한다.
            var anotherBreakable = FindDifferentCellWithHardness(session.TileService, result.width, result.height, breakable);
            session.TileService.TryBreakForeground(anotherBreakable, 3, out _, out _); // 예외가 나지 않으면 구독이 유효하다는 뜻.
            session.SealSystem.WatchPointSealChanged -= OnWatchPointChanged;

            // A-06 트랜잭션 회귀: 범위 밖 좌표를 끼워넣은 손상된 저장 데이터는 거부되고, 라이브 상태는
            // 물리적으로 전혀 바뀌지 않아야 한다.
            var corruptSave = new SaveGame();
            Assert(session.CaptureSnapshot(corruptSave), "손상 테스트용 캡처가 실패함");
            corruptSave.tileChanges.Add(new TileChangeRecord { x = -1, y = -1, z = 0, tileId = WorldTileTypes.Dirt, placed = true });

            var seedBeforeCorruptLoad = session.Seed;
            var tileServiceBeforeCorruptLoad = session.TileService;
            var sealSystemBeforeCorruptLoad = session.SealSystem;

            // 이 손상된 로드는 WorldSessionController.LoadSnapshot이 의도적으로 Debug.LogError를 남기고
            // false를 반환하는 "정상적으로 거부되는" 경로다(진짜 버그가 아님). 콘솔에 매번 오류처럼 보이는
            // 로그가 남아 실제 회귀와 혼동되지 않도록, 이 한 호출 동안만 로그 출력을 잠시 꺼둔다.
            var loadRejected = RunWithSuppressedLogs(() => !session.LoadSnapshot(corruptSave));
            Assert(loadRejected, "범위 밖 좌표를 담은 손상된 저장 데이터가 거부되지 않음");
            Assert(session.Seed == seedBeforeCorruptLoad, "손상된 로드 시도 후 seed가 바뀜 — 트랜잭션 보장 위반");
            Assert(ReferenceEquals(session.TileService, tileServiceBeforeCorruptLoad), "손상된 로드 시도 후 TileService가 교체됨 — 트랜잭션 보장 위반");
            Assert(ReferenceEquals(session.SealSystem, sealSystemBeforeCorruptLoad), "손상된 로드 시도 후 SealSystem이 교체됨 — 트랜잭션 보장 위반");
            Assert(worldLoadedCount == 2, $"손상된 로드 실패 후에도 WorldLoaded가 추가로 발행됨(실제 누적 {worldLoadedCount}회) — 부분 통지 금지 위반");

            session.WorldLoaded -= OnWorldLoaded;
        }
        finally
        {
            session?.Dispose();
            Object.DestroyImmediate(rendererGo);
            Object.DestroyImmediate(timeServiceGo);
        }

        Debug.Log("[Nyangbingo] Dev A world session round-trip test completed.");
    }

    private static Vector3Int FindDifferentCellWithHardness(TileService tileService, int width, int height, Vector3Int exclude)
    {
        for (var x = 0; x < width; x++)
        for (var y = 0; y < height; y++)
        {
            var cell = new Vector3Int(x, y, 0);
            if (cell == exclude) continue;
            var tile = tileService.GetTile(cell);
            if (tile.hardness > 0 && tile.elementType != WorldTileTypes.Bedrock && tile.elementType != WorldTileTypes.IceAltar)
                return cell;
        }
        throw new InvalidOperationException("테스트 픽스처: 두 번째로 파괴 가능한 칸을 찾지 못함");
    }

    private static WorldTilemapRenderer BuildMinimalRenderer(GameObject host)
    {
        host.AddComponent<Grid>();
        var renderer = host.AddComponent<WorldTilemapRenderer>();

        var foregroundObject = new GameObject("Foreground");
        foregroundObject.transform.SetParent(host.transform, false);
        var foregroundTilemap = foregroundObject.AddComponent<Tilemap>();
        foregroundObject.AddComponent<UnityTilemapRenderer>();

        var backgroundObject = new GameObject("Background");
        backgroundObject.transform.SetParent(host.transform, false);
        var backgroundTilemap = backgroundObject.AddComponent<Tilemap>();
        backgroundObject.AddComponent<UnityTilemapRenderer>();

        var serialized = new SerializedObject(renderer);
        serialized.FindProperty("foregroundTilemap").objectReferenceValue = foregroundTilemap;
        serialized.FindProperty("backgroundTilemap").objectReferenceValue = backgroundTilemap;
        // 회귀 테스트는 로직만 검증하고 시각 자료(tileVisuals)는 일부러 비워두므로, 매핑 누락 경고를 꺼서
        // 콘솔을 깨끗하게 유지한다 — 실제 게임 씬(DevAWorldTest)의 렌더러는 이 플래그가 항상 false다.
        serialized.FindProperty("suppressMissingTileWarning").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return renderer;
    }

    // ------------------------------------------------------------------
    // A-14: 타일 노출면 먹선 오버레이 — 마스크→모양 테이블 완전성 + TileService 국소 갱신 통합 검증.
    // ------------------------------------------------------------------
    private static void TestTileEdgeOverlay()
    {
        // 1) 마스크 → (모양, 회전) 테이블 완전성: None을 뺀 15가지 조합 전부가 유효한 모양(0~4)·회전(0~3)으로
        //    해석돼야 하고, None은 반드시 해석에 실패해야 한다(호출자가 오버레이 타일을 지워야 한다는 신호).
        Assert(!TileEdgeOverlayResolver.TryResolve(TileEdgeMask.None, out _, out _), "None 마스크가 해석되면 안 됨");
        for (var mask = 1; mask <= 15; mask++)
        {
            var edgeMask = (TileEdgeMask)mask;
            Assert(TileEdgeOverlayResolver.TryResolve(edgeMask, out var shapeIndex, out var rotationSteps),
                $"마스크 {edgeMask}가 해석되지 않음");
            Assert(shapeIndex >= 0 && shapeIndex < TileEdgeOverlayResolver.ShapeCount,
                $"마스크 {edgeMask}의 모양 인덱스가 범위 밖({shapeIndex})");
            Assert(rotationSteps >= 0 && rotationSteps <= 3, $"마스크 {edgeMask}의 회전 스텝이 범위 밖({rotationSteps})");
        }

        // 2) 통합 검증: 5x5 자연석 한가운데 칸만 파괴 → 정확히 4개 이웃만 각각 1면(직선) 노출로 갱신되고,
        //    그 외 칸(대각/외곽)은 여전히 먹선이 없어야 한다(월드 전체 재계산 금지). 재설치하면 원상 복구돼야 한다.
        const int width = 5, height = 5;
        var tiles = new TileData[width, height];
        for (var x = 0; x < width; x++)
        for (var y = 0; y < height; y++)
            tiles[x, y] = TileData.CreateNaturalWithBackground(WorldTileTypes.Stone, 2, WorldTileTypes.BackgroundStone);

        var hostGo = new GameObject("DevA_TileEdgeOverlayTest_Renderer");
        var shapeTiles = new Tile[TileEdgeOverlayResolver.ShapeCount];
        try
        {
            var renderer = BuildMinimalRenderer(hostGo);
            var edgeOverlayTilemap = WireEdgeOverlay(hostGo, renderer, shapeTiles);
            var tileService = new TileService(tiles, renderer, null, 2);

            var center = new Vector3Int(2, 2, 0);
            var top = new Vector3Int(2, 3, 0);
            var bottom = new Vector3Int(2, 1, 0);
            var left = new Vector3Int(1, 2, 0);
            var right = new Vector3Int(3, 2, 0);
            var farAway = new Vector3Int(0, 0, 0);

            Assert(edgeOverlayTilemap.GetTile(farAway) == null, "픽스처: 초기 상태에서 이미 먹선 타일이 그려져 있음");
            Assert(tileService.TryBreakForeground(center, 3, out _, out _), "테스트 픽스처: 중앙 칸 파괴 실패");

            Assert(edgeOverlayTilemap.GetTile(center) == null, "파괴된(공기) 칸 자신에는 먹선이 그려지면 안 됨");
            var straightTile = shapeTiles[TileEdgeOverlayResolver.ShapeStraight];
            Assert(ReferenceEquals(edgeOverlayTilemap.GetTile(top), straightTile), "위쪽 이웃이 1면(아래) 노출 직선 먹선으로 갱신되지 않음");
            Assert(ReferenceEquals(edgeOverlayTilemap.GetTile(bottom), straightTile), "아래쪽 이웃이 1면(위) 노출 직선 먹선으로 갱신되지 않음");
            Assert(ReferenceEquals(edgeOverlayTilemap.GetTile(left), straightTile), "왼쪽 이웃이 1면(오른쪽) 노출 직선 먹선으로 갱신되지 않음");
            Assert(ReferenceEquals(edgeOverlayTilemap.GetTile(right), straightTile), "오른쪽 이웃이 1면(왼쪽) 노출 직선 먹선으로 갱신되지 않음");
            Assert(edgeOverlayTilemap.GetTile(farAway) == null, "변경 셀과 무관한 먼 칸까지 재계산됨(월드 전체 재계산 금지 위반)");

            Assert(tileService.TryPlaceForeground(center, WorldTileTypes.Stone), "테스트 픽스처: 중앙 칸 재설치 실패");
            Assert(edgeOverlayTilemap.GetTile(top) == null, "재설치 후 위쪽 이웃의 먹선이 지워지지 않음");
            Assert(edgeOverlayTilemap.GetTile(bottom) == null, "재설치 후 아래쪽 이웃의 먹선이 지워지지 않음");
            Assert(edgeOverlayTilemap.GetTile(left) == null, "재설치 후 왼쪽 이웃의 먹선이 지워지지 않음");
            Assert(edgeOverlayTilemap.GetTile(right) == null, "재설치 후 오른쪽 이웃의 먹선이 지워지지 않음");
        }
        finally
        {
            Object.DestroyImmediate(hostGo);
            foreach (var tile in shapeTiles)
            {
                if (tile != null) Object.DestroyImmediate(tile);
            }
        }

        Debug.Log("[Nyangbingo] Dev A tile edge overlay test completed.");
    }

    // ------------------------------------------------------------------
    // A-16/A-20: 배경·벽지
    // ------------------------------------------------------------------
    private static void TestBackgroundAndWallpaper(WorldGenerationConfig config)
    {
        const int seed = 424242;
        var result = new MapGenerator(config).GenerateDetailed(seed);
        Assert(result.passedValidation, "배경 테스트용 시드가 검증을 통과하지 못함");

        // 1) 지하 자연 지형은 전경+자연 배경을 함께 가진다.
        var solidFound = false;
        var caveBackgroundFound = false;
        for (var x = 0; x < result.width; x++)
        {
            var surfaceY = result.surfaceHeights[x];
            for (var y = config.BedrockThickness; y <= surfaceY; y++)
            {
                var tile = result.tiles[x, y];
                if (!tile.IsAir && tile.isNaturalTerrain && tile.elementType != WorldTileTypes.Bedrock)
                {
                    Assert(tile.HasNaturalBackground,
                        $"지하 고체({tile.elementType})에 자연 배경이 없음 @({x},{y})");
                    solidFound = true;
                }
                else if (tile.IsAir && string.Equals(tile.elementType, WorldTileTypes.Air, StringComparison.Ordinal) &&
                         y < surfaceY)
                {
                    var upperBottom = surfaceY - config.UpperLayerThickness + 1;
                    var middleBottom = upperBottom - config.MiddleLayerThickness;
                    var expectedBackground = y >= upperBottom
                        ? WorldTileTypes.BackgroundDirt
                        : y >= middleBottom
                            ? WorldTileTypes.BackgroundStone
                            : WorldTileTypes.BackgroundDeep;

                    Assert(tile.HasBackground && tile.HasNaturalBackground,
                        $"지하 동굴에 자연 배경이 없음 @({x},{y})");
                    Assert(TileIdAlias.EqualsCanonical(tile.backgroundElementType, expectedBackground) &&
                           TileIdAlias.EqualsCanonical(tile.naturalBackgroundElementType, expectedBackground),
                        $"지하 동굴 배경 지층 불일치 @({x},{y}): expected={expectedBackground}, " +
                        $"actual={tile.backgroundElementType}/{tile.naturalBackgroundElementType}");
                    caveBackgroundFound = true;
                }
            }
        }
        Assert(solidFound, "지하 자연 고체 샘플을 찾지 못함");
        Assert(caveBackgroundFound, "자연 배경이 채워진 지하 동굴 샘플을 찾지 못함");

        // 3) 채굴은 전경만 제거하고 배경 유지
        var hostGo = new GameObject("DevA_BackgroundWallpaperTest");
        try
        {
            var renderer = BuildMinimalRenderer(hostGo);
            renderer.EnsureForegroundCollision();
            var tileService = new TileService(result.tiles, renderer, null, result.acceptedSeed);
            var cell = FindUndergroundNaturalSolid(tileService, result);
            var before = tileService.GetTile(cell);
            Assert(before.HasNaturalBackground, "채굴 픽스처 칸에 자연 배경이 없음");
            var bgBefore = before.backgroundElementType;
            Assert(tileService.TryBreakForeground(cell, 3, out _, out _), "채굴 실패");
            var after = tileService.GetTile(cell);
            Assert(after.IsAir, "채굴 후 전경이 비지 않음");
            Assert(after.HasBackground && after.backgroundElementType == bgBefore,
                "채굴 후 배경이 유지되지 않음");
            Assert(renderer.Foreground.GetTile(cell) == null, "채굴 후 전경 Tilemap이 비지 않음");

            // 4~6) 벽지 설치/도포율/제거 복원 — 작은 밀폐 방에서 검증
            var roomW = 12;
            var roomH = 12;
            var tiles = new TileData[roomW, roomH];
            for (var x = 0; x < roomW; x++)
            for (var y = 0; y < roomH; y++)
                tiles[x, y] = TileData.CreateNaturalWithBackground(WorldTileTypes.Stone, 2, WorldTileTypes.BackgroundStone);

            // 내부 10x10을 빈 배경 공기로 팜(벽지만으로 100% 도포 가능하게).
            for (var x = 1; x < roomW - 1; x++)
            for (var y = 1; y < roomH - 1; y++)
                tiles[x, y] = TileData.CreateAir();

            var roomService = new TileService(tiles, null, null, 1);
            var seal = new SealSystem(roomService, sealWindowRadiusX: 20, sealWindowRadiusY: 20, sealTargetCells: 100f);
            var coverage = new WallpaperCoverageService(roomService, seal);
            var core = new Vector3Int(roomW / 2, roomH / 2, 0);
            seal.SetSealCoreCell(core);
            var cooling = new FakeCoolingSourceProvider { CoolingCapPercent = 100f };
            seal.SetCoolingSourceProvider(cooling);

            Assert(seal.LeakFaceCount == 0 && seal.SealPercent > 0f, "벽지 테스트용 방이 밀폐되지 않음");
            Assert(!coverage.IsCoverageComplete(core), "빈 배경 방에서 도포 완료면 안 됨");
            Assert(coverage.GetCoveragePercent(core) < 100f, "빈 배경 방에서 도포율 100%면 안 됨");

            // 99%만 바름 — 완료 아님
            var interiorCount = 0;
            for (var x = 1; x < roomW - 1; x++)
            for (var y = 1; y < roomH - 1; y++)
                interiorCount++;
            var paintCount = interiorCount - 1;
            var painted = 0;
            for (var x = 1; x < roomW - 1 && painted < paintCount; x++)
            for (var y = 1; y < roomH - 1 && painted < paintCount; y++)
            {
                Assert(roomService.TryPlaceBackground(new Vector3Int(x, y, 0), WorldTileTypes.Wallpaper),
                    $"벽지 설치 실패 @({x},{y})");
                painted++;
            }
            coverage.Invalidate();
            Assert(!coverage.IsCoverageComplete(core), "99% 도포에서 완료 효과가 켜짐");
            Assert(coverage.GetCoveragePercent(core) < 100f, "99% 도포에서 도포율이 100%");

            // 마지막 1칸 — 100% 완료
            Vector3Int lastEmpty = default;
            var foundEmpty = false;
            for (var x = 1; x < roomW - 1 && !foundEmpty; x++)
            for (var y = 1; y < roomH - 1 && !foundEmpty; y++)
            {
                var c = new Vector3Int(x, y, 0);
                if (!roomService.GetTile(c).HasBackground)
                {
                    lastEmpty = c;
                    foundEmpty = true;
                }
            }
            Assert(foundEmpty, "마지막 빈 배경 칸을 찾지 못함");
            Assert(roomService.TryPlaceBackground(lastEmpty, WorldTileTypes.Wallpaper), "마지막 벽지 설치 실패");
            coverage.Invalidate();
            Assert(coverage.IsCoverageComplete(core), "100% 도포인데 완료가 아님");
            Assert(Mathf.Approximately(coverage.GetCoveragePercent(core), 100f), "100% 도포율 불일치");

            // 벽지는 밀폐율에 영향 없음
            var sealBefore = seal.SealPercent;
            Assert(Mathf.Approximately(seal.SealPercent, sealBefore), "벽지 설치가 SealPercent를 바꿈");

            // 제거 시 빈 배경 복원(원래 동굴/빈 칸)
            Assert(roomService.TryRemoveBackground(lastEmpty), "벽지 제거 실패");
            var lastCellBackgroundChanges = new List<TileChangeRecord>();
            foreach (var record in roomService.GetBackgroundChangeRecords())
                if (record.x == lastEmpty.x && record.y == lastEmpty.y)
                    lastCellBackgroundChanges.Add(record);
            Assert(lastCellBackgroundChanges.Count == 2 &&
                   lastCellBackgroundChanges[0].placed &&
                   !lastCellBackgroundChanges[1].placed,
                "동일 셀 벽지 설치-제거 이력이 실행 순서대로 보존되지 않음");
            Assert(!roomService.GetTile(lastEmpty).HasBackground, "벽지 제거 후 빈 배경이 복원되지 않음");

            // 지하 자연 배경 위에 벽지를 덮을 수 없음(빈 배경만) — 자연 bg가 있는 칸
            var naturalBgCell = new Vector3Int(0, 0, 0);
            Assert(roomService.GetTile(naturalBgCell).HasNaturalBackground, "벽 칸에 자연 배경이 없음");
            Assert(!roomService.TryPlaceBackground(naturalBgCell, WorldTileTypes.Wallpaper),
                "자연 배경이 있는 칸에 벽지 설치가 허용됨");

            // 7) 저장/로드 라운드트립 — 배경 이력 포함 + 구버전(배경 이력 null) 로드
            var sessionGo = new GameObject("DevA_BgSaveRoundTrip_Renderer");
            WorldSessionController session = null;
            try
            {
                var sessionRenderer = BuildMinimalRenderer(sessionGo);
                session = new WorldSessionController(config, sessionRenderer, null);
                var world = session.StartNewWorld(seed);
                Assert(world.passedValidation, "배경 세이브 라운드트립용 월드 생성 실패");
                Assert(session.WallpaperCoverage != null, "StartNewWorld 후 WallpaperCoverage가 null");

                var mineCell = FindUndergroundNaturalSolid(session.TileService, world);
                var naturalBg = session.TileService.GetTile(mineCell).backgroundElementType;
                Assert(session.TileService.TryBreakForeground(mineCell, 3, out _, out _), "라운드트립 채굴 실패");

                // 빈 배경 칸에 벽지(생성기 동굴)를 하나 찾아 설치
                var wallpaperCell = FindEmptyBackgroundAir(session.TileService, world);
                Assert(session.TileService.TryPlaceBackground(wallpaperCell, WorldTileTypes.Wallpaper),
                    "라운드트립 벽지 설치 실패");

                var save = new SaveGame();
                Assert(session.CaptureSnapshot(save), "배경 포함 CaptureSnapshot 실패");
                Assert(save.backgroundChanges != null && save.backgroundChanges.Count >= 1,
                    "backgroundChanges가 캡처되지 않음");
                Assert(session.LoadSnapshot(save), "배경 포함 LoadSnapshot 실패");
                Assert(session.TileService.GetTile(mineCell).IsAir, "로드 후 채굴 칸 전경 복원 실패");
                Assert(session.TileService.GetTile(mineCell).backgroundElementType == naturalBg,
                    "로드 후 채굴 칸 배경 유지 실패");
                Assert(session.TileService.GetTile(wallpaperCell).IsWallpaperBackground,
                    "로드 후 벽지 복원 실패");

                // 구버전 세이브: backgroundChanges = null → 로드 가능
                var legacy = new SaveGame();
                Assert(session.CaptureSnapshot(legacy), "레거시 캡처 실패");
                legacy.backgroundChanges = null;
                Assert(session.LoadSnapshot(legacy), "backgroundChanges=null 구버전 세이브 로드 실패");
            }
            finally
            {
                session?.Dispose();
                Object.DestroyImmediate(sessionGo);
            }

            coverage.Dispose();
            seal.Dispose();
        }
        finally
        {
            Object.DestroyImmediate(hostGo);
        }

        Debug.Log("[Nyangbingo] Dev A background/wallpaper test completed.");
    }

    // ------------------------------------------------------------------
    // A-17/A-20: 전경 충돌·렌더 동기화
    // ------------------------------------------------------------------
    private static void TestForegroundCollisionAndRender(WorldGenerationConfig config)
    {
        var hostGo = new GameObject("DevA_CollisionRenderTest");
        var dummyTiles = new List<Tile>();
        try
        {
            var renderer = BuildMinimalRenderer(hostGo);
            // 생성기가 쓰는 전경/배경 elementType 전부에 더미 Tile을 매핑한다.
            // (dirt/stone만 있으면 stone_mid 등 첫 지하 고체가 스프라이트 없이 렌더되어 실패한다.)
            WireDummyTileVisuals(renderer, dummyTiles, CollectRenderableElementTypes());
            renderer.EnsureForegroundCollision();

            Assert(renderer.Foreground.GetComponent<TilemapCollider2D>() != null, "전경 TilemapCollider2D 없음");
            Assert(renderer.Foreground.GetComponent<CompositeCollider2D>() != null, "전경 CompositeCollider2D 없음");
            Assert(renderer.Foreground.GetComponent<Rigidbody2D>() != null, "전경 Static Rigidbody2D 없음");
            Assert(renderer.Background.GetComponent<TilemapCollider2D>() == null, "배경에 TilemapCollider2D가 붙어 있음");
            Assert(renderer.Background.GetComponent<CompositeCollider2D>() == null, "배경에 CompositeCollider2D가 붙어 있음");

            const int seed = 515151;
            var result = new MapGenerator(config).GenerateDetailed(seed);
            Assert(result.passedValidation, "충돌 테스트용 월드 생성 실패");
            renderer.RenderWorld(result.tiles);

            var boundaryRoot = renderer.Foreground.transform.Find("RuntimeWorldBoundaries");
            Assert(boundaryRoot != null, "생성 월드 좌우 투명 경계 루트 없음");
            var leftBoundary = boundaryRoot.Find("WorldBoundaryLeft")?.GetComponent<BoxCollider2D>();
            var rightBoundary = boundaryRoot.Find("WorldBoundaryRight")?.GetComponent<BoxCollider2D>();
            Assert(leftBoundary != null && rightBoundary != null, "생성 월드 좌우 투명 경계 콜라이더 없음");

            var worldLeft = renderer.Foreground.CellToWorld(Vector3Int.zero).x;
            var worldRight = renderer.Foreground.CellToWorld(new Vector3Int(result.width, 0, 0)).x;
            Assert(!leftBoundary.isTrigger && !rightBoundary.isTrigger,
                "월드 좌우 경계는 플레이어를 물리적으로 막아야 함");
            Assert(Mathf.Abs(leftBoundary.bounds.max.x - worldLeft) < .01f,
                "왼쪽 투명벽이 생성 맵 왼쪽 끝과 맞지 않음");
            Assert(Mathf.Abs(rightBoundary.bounds.min.x - worldRight) < .01f,
                "오른쪽 투명벽이 생성 맵 오른쪽 끝과 맞지 않음");
            Assert(leftBoundary.bounds.size.y >= result.height * 3f - .01f &&
                   rightBoundary.bounds.size.y >= result.height * 3f - .01f,
                "좌우 투명벽은 맵 위아래로 우회할 수 없을 만큼 길어야 함");
            Assert(leftBoundary.GetComponent<SpriteRenderer>() == null &&
                   rightBoundary.GetComponent<SpriteRenderer>() == null,
                "월드 좌우 경계에는 보이는 렌더러가 없어야 함");

            var tileService = new TileService(result.tiles, renderer, null, result.acceptedSeed);
            var cell = FindUndergroundNaturalSolid(tileService, result, renderer);

            Assert(renderer.Foreground.GetTile(cell) != null, "파괴 전 전경 스프라이트가 없음");
            Assert(tileService.TryBreakForeground(cell, 3, out _, out _), "충돌 테스트 채굴 실패");
            Assert(tileService.GetTile(cell).IsAir, "채굴 후 TileData 전경이 비지 않음");
            Assert(renderer.Foreground.GetTile(cell) == null, "채굴 후 전경 스프라이트가 남음");
            Assert(tileService.GetTile(cell).HasBackground, "채굴 후 배경이 사라짐");

            Assert(tileService.TryPlaceForeground(cell, WorldTileTypes.Stone), "재설치 실패");
            Assert(!tileService.GetTile(cell).IsAir, "설치 후 TileData 전경이 없음");
            Assert(renderer.Foreground.GetTile(cell) != null, "설치 후 전경 스프라이트가 없음");

            // 배경 변경은 충돌(전경 타일)에 영향 없음
            var emptyBg = FindEmptyBackgroundAir(tileService, result);
            var fgBefore = renderer.Foreground.GetTile(emptyBg);
            Assert(tileService.TryPlaceBackground(emptyBg, WorldTileTypes.Wallpaper), "배경 설치 실패");
            Assert(ReferenceEquals(renderer.Foreground.GetTile(emptyBg), fgBefore),
                "배경 변경이 전경 Tilemap을 바꿈");
            Assert(tileService.TryRemoveBackground(emptyBg), "배경 제거 실패");

            // 연속 채굴 — 유령 전경 스프라이트 없음
            for (var i = 0; i < 5; i++)
            {
                var next = FindUndergroundNaturalSolid(tileService, result, renderer);
                Assert(tileService.TryBreakForeground(next, 3, out _, out _), $"연속 채굴 {i} 실패");
                Assert(renderer.Foreground.GetTile(next) == null, $"연속 채굴 후 유령 스프라이트 @ {next}");
            }
        }
        finally
        {
            Object.DestroyImmediate(hostGo);
            foreach (var tile in dummyTiles) Object.DestroyImmediate(tile);
        }

        Debug.Log("[Nyangbingo] Dev A foreground collision/render test completed.");
    }

    private static void WireDummyTileVisuals(WorldTilemapRenderer renderer, List<Tile> created, params string[] elementTypes)
    {
        var visuals = new WorldTilemapRenderer.TileVisual[elementTypes.Length];
        for (var i = 0; i < elementTypes.Length; i++)
        {
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = $"DevA_Dummy_{elementTypes[i]}";
            created.Add(tile);
            visuals[i] = new WorldTilemapRenderer.TileVisual { elementType = elementTypes[i], tile = tile };
        }

        renderer.SetTileVisualsForEditorSetup(visuals, null);
        renderer.RebuildLookupTable();
    }

    /// <summary>생성기·봉인·온보딩 보정이 쓰는 elementType을 테스트 더미 비주얼로 전부 배선.</summary>
    private static string[] CollectRenderableElementTypes()
    {
        var list = new List<string>();
        foreach (var id in WorldTileTypes.AllElementTypes)
        {
            if (string.Equals(id, WorldTileTypes.Air, StringComparison.Ordinal)) continue;
            list.Add(id);
        }
        return list.ToArray();
    }

    // ------------------------------------------------------------------
    // A-18/A-20: 지표면 안전 스폰
    // ------------------------------------------------------------------
    private static void TestSafeSurfaceSpawn(WorldGenerationConfig config)
    {
        var fixedSeeds = new[] { 1, 42, 100, 20260716, 987654 };
        foreach (var seed in fixedSeeds)
        {
            var result = new MapGenerator(config).GenerateDetailed(seed);
            Assert(result.passedValidation, $"고정 seed {seed}: 월드 검증 실패");
            AssertSafeSpawnResult(config, result);
        }

        // 최소 100개 연속 seed (생성 검증 통과분 기준 — 통과한 월드의 spawn이 안전해야 함)
        var checkedCount = 0;
        for (var seed = 1000; checkedCount < 100 && seed < 1000 + 400; seed++)
        {
            var result = new MapGenerator(config).GenerateDetailed(seed);
            if (!result.passedValidation) continue;
            AssertSafeSpawnResult(config, result);
            checkedCount++;
        }
        Assert(checkedCount >= 100, $"안전 스폰을 검증한 seed가 100개 미만(실제 {checkedCount})");

        // 결정론
        var a = new MapGenerator(config).GenerateDetailed(777);
        var b = new MapGenerator(config).GenerateDetailed(777);
        Assert(a.spawnPoint == b.spawnPoint, "같은 seed인데 spawnPoint가 다름");
        Assert(a.passedValidation && b.passedValidation, "결정론 스폰 테스트 시드 검증 실패");

        Debug.Log("[Nyangbingo] Dev A safe surface spawn test completed.");
    }

    // ------------------------------------------------------------------
    // A-22: IWorldSafeSpawnResolver 공용 계약
    // ------------------------------------------------------------------
    private static void TestWorldSafeSpawnResolver(WorldGenerationConfig config)
    {
        const float halfExtent = 0.38f;
        var result = new MapGenerator(config).GenerateDetailed(20260716);
        Assert(result.passedValidation, "안전 스폰 계약 테스트용 월드 검증 실패");

        var tileService = new TileService(result.tiles, null, null, result.acceptedSeed);
        IWorldSafeSpawnResolver resolver = tileService;

        Assert(resolver.TryResolveSafeSurfaceSpawn(result.spawnPoint.x, halfExtent, out var resolved),
            "TryResolveSafeSurfaceSpawn이 생성 spawn 근처에서 실패");
        Assert(resolver.IsSafeStandingPosition(resolved, halfExtent),
            "Resolve된 위치가 IsSafeStandingPosition=false");

        // 결정론
        Assert(resolver.TryResolveSafeSurfaceSpawn(result.spawnPoint.x, halfExtent, out var resolved2),
            "두 번째 Resolve 실패");
        Assert(resolved == resolved2, "동일 입력인데 Resolve 결과가 다름");

        // 정상 스폰 셀에 대응하는 월드는 안전 — 고체 내부는 불안전
        var solidWorld = new Vector2(result.spawnPoint.x + 0.5f, result.surfaceHeights[result.spawnPoint.x] + 0.5f);
        Assert(!resolver.IsSafeStandingPosition(solidWorld, halfExtent),
            "지표면 고체 내부 좌표가 안전으로 판정됨");

        // 월드 밖
        Assert(!resolver.IsSafeStandingPosition(new Vector2(-10f, -10f), halfExtent),
            "월드 밖 좌표가 안전으로 판정됨");

        // 수직 구멍 위(입구 열 깊은 공기) 입력이어도 Resolve는 안전 열로 교정
        var entranceX = Mathf.Clamp(result.spawnPoint.x - 1, 1, result.width - 2);
        Assert(resolver.TryResolveSafeSurfaceSpawn(entranceX, halfExtent, out var fromPit),
            "입구 열 preferred에서도 Resolve 실패");
        Assert(resolver.IsSafeStandingPosition(fromPit, halfExtent),
            "입구 열 Resolve 결과가 불안전");

        Debug.Log("[Nyangbingo] Dev A world safe spawn resolver test completed.");
    }

    // ------------------------------------------------------------------
    // A-25: 전경/배경 배치 계약
    // ------------------------------------------------------------------
    private static void TestForegroundBackgroundPlacementContracts(WorldGenerationConfig config)
    {
        var hostGo = new GameObject("DevA_PlacementContractTest");
        var dummyTiles = new List<Tile>();
        WorldSessionController session = null;
        try
        {
            var renderer = BuildMinimalRenderer(hostGo);
            WireDummyTileVisuals(renderer, dummyTiles, CollectRenderableElementTypes());
            var result = new MapGenerator(config).GenerateDetailed(42);
            Assert(result.passedValidation, "배치 계약 테스트용 월드 검증 실패");

            session = new WorldSessionController(config, renderer, null);
            session.StartNewWorld(42);
            var tiles = session.TileService;

            Assert(TileService.SupportsForegroundPlacement(WorldTileTypes.Dirt), "dirt가 전경 설치 가능이어야 함");
            Assert(TileService.SupportsForegroundPlacement(WorldTileTypes.Stone), "stone이 전경 설치 가능이어야 함");
            Assert(!TileService.SupportsForegroundPlacement(WorldTileTypes.Bedrock), "기반암은 전경 재설치 불가");
            Assert(!TileService.SupportsForegroundPlacement(WorldTileTypes.IceAltar), "제단은 전경 재설치 불가");
            Assert(!TileService.SupportsForegroundPlacement(WorldTileTypes.BackgroundDirt), "배경 ID는 전경 설치 불가");
            Assert(!TileService.SupportsForegroundPlacement(WorldTileTypes.Wallpaper), "벽지는 전경 설치 불가");

            var airCell = FindAirCellNearSpawn(tiles, session.LastResult);
            Assert(tiles.CanPlaceForeground(airCell, WorldTileTypes.Dirt), "공기 셀에 dirt 설치 가능해야 함");
            Func<Vector3Int, bool> placementBlocker = cell => cell == airCell;
            Func<Vector3Int, bool> secondPlacementBlocker = cell => cell == airCell;
            tiles.SetForegroundPlacementBlocker(placementBlocker);
            tiles.SetForegroundPlacementBlocker(secondPlacementBlocker);
            Assert(!tiles.CanPlaceForeground(airCell, WorldTileTypes.Dirt),
                "월드 오브젝트 점유 셀에는 전경 블럭을 설치할 수 없어야 함");
            Assert(!tiles.TryPlaceForeground(airCell, WorldTileTypes.Dirt),
                "월드 오브젝트 점유 셀의 직접 전경 설치도 거부해야 함");
            tiles.ClearForegroundPlacementBlocker(placementBlocker);
            Assert(!tiles.CanPlaceForeground(airCell, WorldTileTypes.Dirt),
                "한 점유 차단기를 해제해도 다른 점유 차단기는 유지되어야 함");
            tiles.ClearForegroundPlacementBlocker(secondPlacementBlocker);
            Assert(tiles.TryPlaceForeground(airCell, WorldTileTypes.Dirt), "dirt 설치 실패");
            Assert(!tiles.GetTile(airCell).IsAir, "설치 후 전경이 비어 있음");
            Assert(!tiles.CanPlaceForeground(airCell, WorldTileTypes.Dirt), "점유 셀에 재설치 가능으로 나옴");
            Assert(!tiles.TryPlaceForeground(airCell, WorldTileTypes.Dirt), "점유 셀 설치가 성공함");

            IBackgroundPlacementService bg = tiles;
            Assert(tiles.TryBreakForeground(airCell, 3, out _, out _), "설치 타일 채굴 실패");
            var wallpaperCell = FindEmptyBackgroundAir(tiles, session.LastResult);
            Assert(bg.CanPlaceWallpaper(wallpaperCell), "빈 배경 칸에 벽지 설치 불가 판정");
            Assert(bg.TryPlaceWallpaper(wallpaperCell), "벽지 설치 실패");
            var state = bg.GetBackgroundState(wallpaperCell);
            Assert(state.HasWallpaper, "벽지 설치 후 HasWallpaper=false");
            Assert(bg.TryRemoveWallpaper(wallpaperCell), "벽지 제거 실패");
            Assert(!bg.GetBackgroundState(wallpaperCell).HasWallpaper, "벽지 제거 후에도 HasWallpaper");

            Assert(session.BackgroundPlacement != null, "Session.BackgroundPlacement null");
            Assert(session.SafeSpawnResolver != null, "Session.SafeSpawnResolver null");

            Debug.Log("[Nyangbingo] Dev A placement contract test completed.");
        }
        finally
        {
            session?.Dispose();
            Object.DestroyImmediate(hostGo);
            foreach (var tile in dummyTiles) Object.DestroyImmediate(tile);
        }
    }

    private static Vector3Int FindAirCellNearSpawn(TileService tiles, WorldGenerationResult result)
    {
        var spawn = result.spawnPoint;
        for (var r = 1; r < 20; r++)
        {
            for (var dx = -r; dx <= r; dx++)
            for (var dy = -r; dy <= r; dy++)
            {
                var cell = new Vector3Int(spawn.x + dx, spawn.y + dy, 0);
                if (!tiles.InBounds(cell)) continue;
                if (tiles.GetTile(cell).IsAir) return cell;
            }
        }
        Assert(false, "스폰 근처 공기 셀을 찾지 못함");
        return default;
    }

    // ------------------------------------------------------------------
    // A-26: 반경·밀폐 창 오버레이
    // ------------------------------------------------------------------
    private static void TestWorldRangeOverlayRenderer()
    {
        var host = new GameObject("DevA_RangeOverlayTest");
        try
        {
            var renderer = host.AddComponent<WorldRangeOverlayRenderer>();
            IWorldRangeOverlayRenderer api = renderer;

            var overlays = new List<WorldRangeOverlay>
            {
                new WorldRangeOverlay(new Vector2(10.5f, 20.5f), 4f, WorldRangeShape.Circle),
                new WorldRangeOverlay(new Vector2(10.5f, 20.5f), 6f, WorldRangeShape.Circle),
                new WorldRangeOverlay(new Vector2(10.5f, 20.5f), 8f, WorldRangeShape.Circle),
                new WorldRangeOverlay(new Vector2(50.5f, 30.5f), 28f, 12f, WorldRangeShape.AxisAlignedRect)
            };

            api.Render(overlays);
            var lines = host.GetComponentsInChildren<LineRenderer>(true);
            Assert(lines.Length >= 4, $"오버레이 LineRenderer가 부족(실제 {lines.Length})");

            api.SetVisible(false);
            foreach (var line in lines)
            {
                if (line.enabled) Assert(false, "SetVisible(false) 후에도 LineRenderer가 켜져 있음");
            }

            api.SetVisible(true);
            api.Clear();
            api.Render(overlays);
            api.Clear();
            api.Render(overlays);
            var afterToggle = host.GetComponentsInChildren<LineRenderer>(true);
            Assert(afterToggle.Length == lines.Length,
                $"토글 반복 후 LineRenderer 수 증가(누수?) {lines.Length}→{afterToggle.Length}");

            Debug.Log("[Nyangbingo] Dev A world range overlay test completed.");
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    // ------------------------------------------------------------------
    // v27: 동굴 공동 지표 관통 금지 + cave_max_height
    // ------------------------------------------------------------------
    private static void TestCaveSurfaceProtection(WorldGenerationConfig config)
    {
        Assert(config.CaveMaxHeight == 12, $"CaveMaxHeight 가안 12가 아님(실제 {config.CaveMaxHeight})");

        var projectAsset = AssetDatabase.LoadAssetAtPath<WorldGenerationConfig>("Assets/Data/SO/WorldGenerationConfig.asset");
        Assert(projectAsset != null && projectAsset.CaveMaxHeight == 12,
            "프로젝트 WorldGenerationConfig.asset의 caveMaxHeight가 12가 아님");

        // globals.csv note 열에 쉼표가 있어 mergeUnquotedTrailingNote 필요(ReimportGlobals와 동일).
        var csvRows = NyangbingoCsvUtility.ReadRows("Assets/Data/CSV/globals.csv", mergeUnquotedTrailingNote: true);
        var found = false;
        foreach (var row in csvRows)
        {
            if (!row.TryGetValue("key", out var key) || key != "cave_max_height") continue;
            found = true;
            Assert(row.TryGetValue("value", out var value) && value == "12",
                $"globals.csv cave_max_height 값이 12가 아님(실제 {value})");
        }
        Assert(found, "globals.csv에 cave_max_height 행이 없음");

        // Pass 2 펄린 개척 + 최종과 동일한 PostProcess(보호 마스크 없음)로 공동 한도를 검증.
        // (실제 파이프라인에서는 PostProcess가 Pass 4 이후에 돌지만, 펄린 공동 자체 한도는 동일 로직.)
        var terrainMethod = typeof(MapGenerator).GetMethod("GenerateTerrain",
            BindingFlags.NonPublic | BindingFlags.Static);
        var carveMethod = typeof(MapGenerator).GetMethod("CarveCaves",
            BindingFlags.NonPublic | BindingFlags.Static);
        var postMethod = typeof(MapGenerator).GetMethod("PostProcessCaveCavities",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert(terrainMethod != null && carveMethod != null && postMethod != null,
            "GenerateTerrain/CarveCaves/PostProcessCaveCavities 리플렉션 실패");

        foreach (var seed in new[] { 1, 42, 100, 777, 20260719 })
        {
            var rngFieldSeed = seed;
            var grid = new TileData[config.MapWidth, config.MapHeight];
            var terrainRng = new System.Random(rngFieldSeed);
            var surfaceHeights = (int[])terrainMethod.Invoke(null, new object[] { grid, terrainRng, config });
            carveMethod.Invoke(null, new object[] { grid, surfaceHeights, new System.Random(rngFieldSeed ^ 0xC0FFEE), config });

            var protectedAir = new bool[config.MapWidth, config.MapHeight];
            postMethod.Invoke(null, new object[] { grid, surfaceHeights, config, protectedAir });

            AssertNoSurfaceBreakthrough(grid, surfaceHeights, config, seed);
            AssertNoOversizedCaveChamber(grid, surfaceHeights, config, seed);

            // seed 1 회귀: 과거 실패 구간(y 71~85, span 15)이 Hard Cut 후 분할되어야 한다.
            if (seed == 1)
            {
                AssertNoAirSpanInRange(grid, surfaceHeights, config, seed, 71, 85, config.CaveMaxHeight);
                AssertNoVerticalAirRunInRange(grid, config, seed, 71, 85, config.CaveMaxHeight);
            }
        }

        // 전체 파이프라인: 연결 통로가 맵 중앙에 거대 수직 우물(+자 3열·전깊이)을 만들지 않는지.
        foreach (var seed in new[] { 1, 42, 49, 100, 777 })
        {
            var result = new MapGenerator(config).GenerateDetailed(seed);
            Assert(result.passedValidation, $"seed {seed}: 연결 통로 변경 후 월드 검증 실패");
            AssertNoGiantConnectivityShaft(result, config);
            AssertNoConnectivityShaftInSurfaceBand(result, config);
            AssertSafeSpawnResult(config, result);
        }

        Debug.Log("[Nyangbingo] Dev A cave surface protection test completed.");
    }

    /// <summary>
    /// v7/v28: globals.csv 플레이어 점프·중력 — 테라리아급 약 3.5타일·가변 점프 컷.
    /// </summary>
    private static void TestPlayerJumpPhysics()
    {
        const string catalogPath = "Assets/Data/SO/GameDataCatalog.asset";
        var catalog = AssetDatabase.LoadAssetAtPath<GameDataCatalog>(catalogPath);
        Assert(catalog != null, "GameDataCatalog.asset 로드 실패");
        Assert(PlayerMovementPhysics.TryLoadFromCatalog(catalog, out var config),
            "player physics globals(player_jump_height_tiles 등 4키) 로드 실패");

        Assert(Mathf.Abs(config.JumpHeightTiles - 3.5f) < 0.01f,
            $"player_jump_height_tiles 기대 3.5, 실제 {config.JumpHeightTiles}");
        Assert(Mathf.Abs(config.Gravity - 32f) < 0.01f,
            $"player_gravity 기대 32, 실제 {config.Gravity}");
        Assert(Mathf.Abs(config.MaxFallSpeed - 12f) < 0.01f,
            $"player_max_fall_speed 기대 12, 실제 {config.MaxFallSpeed}");
        Assert(Mathf.Abs(config.JumpCutMultiplier - 0.5f) < 0.01f,
            $"player_jump_cut 기대 0.5, 실제 {config.JumpCutMultiplier}");

        var expectedV0 = Mathf.Sqrt(2f * config.Gravity * config.JumpHeightTiles);
        Assert(Mathf.Abs(config.JumpVelocity - expectedV0) < 0.05f,
            $"JumpVelocity 기대 {expectedV0}, 실제 {config.JumpVelocity}");

        const float fixedDt = 0.02f;
        var fullPeak = PlayerMovementPhysics.SimulatePeakJumpHeightTiles(config, fixedDt);
        Assert(fullPeak >= 3.1f && fullPeak <= 3.9f,
            $"풀 점프 최고 높이 기대 ~3.5타일, 실제 {fullPeak:F2}");

        var shortPeak = PlayerMovementPhysics.SimulatePeakJumpHeightTiles(config, fixedDt, holdFrames: 3);
        Assert(shortPeak < fullPeak * 0.65f,
            $"가변 점프(짧게 누름)가 너무 높음: short={shortPeak:F2}, full={fullPeak:F2}");

        var afterGravity = MainGamePlayerController.ApplyGravity(10f, config.Gravity, config.MaxFallSpeed, fixedDt);
        var expectedGravity = PlayerMovementPhysics.ApplyGravity(10f, config.Gravity, config.MaxFallSpeed, fixedDt);
        Assert(Mathf.Abs(afterGravity - expectedGravity) < 0.0001f,
            "MainGamePlayerController.ApplyGravity 위임 불일치");

        var worldConfig = WorldGenerationConfig.CreateDefault();
        try
        {
            Assert(worldConfig.SpawnEntranceDepthTiles < config.JumpHeightTiles,
                $"입구 깊이 {worldConfig.SpawnEntranceDepthTiles} >= 점프 {config.JumpHeightTiles} — 낙하 후 탈출 불가");
        }
        finally
        {
            Object.DestroyImmediate(worldConfig);
        }

        Debug.Log("[Nyangbingo] Dev A player jump physics test completed.");
    }

    /// <summary>채굴은 발톱 이펙트와 같은 8방향 직선 위의 전경 고체만 선택한다.</summary>
    private static void TestMiningCellSurfaceFallback()
    {
        var tiles = new TileData[8, 8];
        for (var x = 0; x < 8; x++)
        for (var y = 0; y < 8; y++)
            tiles[x, y] = TileData.CreateAir();

        const int groundX = 5;
        const int groundY = 5;
        const int airY = 6;
        tiles[groundX, groundY] = TileData.CreateNaturalWithBackground(
            WorldTileTypes.Stone, 1, WorldTileTypes.BackgroundDirt);
        tiles[groundX + 1, groundY] = TileData.CreateNaturalWithBackground(
            WorldTileTypes.Dirt, 1, WorldTileTypes.BackgroundDirt);

        var tileService = new TileService(tiles, renderer: null, catalog: null, seed: 42);
        var playerOrigin = new Vector2(groundX + .5f, airY + .5f);
        const float reach = 4f;

        // 공기 칸 클릭은 아래 고체로 보정하지 않는다 — 정확한 포인터 칸만 채굴.
        Assert(!MainGamePlayerController.TryPickMiningCell(tileService, playerOrigin,
                new Vector2(groundX + .2f, airY + .3f), Vector2.right, reach, out _),
            "공기 칸 클릭이 인접 고체로 스냅되면 안 됨");

        tiles[groundX + 1, airY] = TileData.CreateNaturalWithBackground(
            WorldTileTypes.Stone, 1, WorldTileTypes.BackgroundDirt);
        Assert(MainGamePlayerController.TryPickMiningCell(tileService, playerOrigin,
                new Vector2(groundX + 1.5f, airY + .5f), Vector2.right, reach, out var frontCell) &&
            frontCell.x == groundX + 1 && frontCell.y == airY,
            "수평 발톱은 플레이어 물리 원점과 같은 높이의 정면 블록을 채굴해야 함");

        Assert(MainGamePlayerController.TryPickMiningCell(tileService, playerOrigin,
                new Vector2(groundX + .5f, groundY + .5f), Vector2.down, reach, out var directCell) &&
            directCell.x == groundX && directCell.y == groundY,
            "고체 직접 클릭이 바뀌면 안 됨");

        var undergroundOrigin = new Vector2(3.5f, 3.5f);
        tiles[3, 3] = TileData.CreateNaturalWithBackground(
            WorldTileTypes.Stone, 1, WorldTileTypes.BackgroundDirt);
        Assert(MainGamePlayerController.TryPickMiningCell(tileService, undergroundOrigin,
                new Vector2(3.6f, 3.4f), Vector2.down, reach, out var wallCell) &&
            wallCell.x == 3 && wallCell.y == 3,
            "지하 벽 직접 클릭이 바뀌면 안 됨");

        const int upwardTwoY = airY + 2;
        tiles[groundX, upwardTwoY] = TileData.CreateNaturalWithBackground(
            WorldTileTypes.Stone, 1, WorldTileTypes.BackgroundDirt);
        tiles[groundX, airY + 1] = TileData.CreateNaturalWithBackground(
            WorldTileTypes.Dirt, 1, WorldTileTypes.BackgroundDirt);
        Assert(MainGamePlayerController.TryPickMiningCell(tileService, playerOrigin,
                new Vector2(groundX + .5f, upwardTwoY + .5f), Vector2.up, reach, out var upwardTwoCell) &&
            upwardTwoCell.x == groundX && upwardTwoCell.y == upwardTwoY,
            $"머리 위 2칸 채굴 실패 — 기대 ({groundX},{upwardTwoY}), 실제 {upwardTwoCell}");

        // 사거리 밖·옆 공기에서는 사이/인접 칸으로 대체하면 안 된다.
        const int farY = airY + 5;
        tiles[groundX, farY] = TileData.CreateNaturalWithBackground(
            WorldTileTypes.Stone, 1, WorldTileTypes.BackgroundDirt);
        Assert(!MainGamePlayerController.TryPickMiningCell(tileService, playerOrigin,
                new Vector2(groundX + .5f, farY + .5f), Vector2.up, reach, out _),
            "사거리 밖 포인터 고체는 더 가까운 블록으로 대체되면 안 됨");

        const int diagX = groundX + 1;
        const int diagY = airY + 1;
        tiles[diagX, diagY] = TileData.CreateNaturalWithBackground(
            WorldTileTypes.Stone, 1, WorldTileTypes.BackgroundDirt);
        tiles[diagX, diagY - 1] = TileData.CreateNaturalWithBackground(
            WorldTileTypes.Dirt, 1, WorldTileTypes.BackgroundDirt);
        Assert(!MainGamePlayerController.TryPickMiningCell(tileService, playerOrigin,
                new Vector2(diagX + 1.1f, diagY + .2f), Vector2.right, reach, out _),
            "대각선 옆 공기 클릭이 인접 고체로 스냅되면 안 됨");
        Assert(MainGamePlayerController.TryPickMiningCell(tileService, playerOrigin,
                new Vector2(diagX + .5f, diagY + .5f), Vector2.right, reach, out var diagonalCell) &&
            diagonalCell.x == diagX && diagonalCell.y == diagY,
            $"대각선 고체 직접 클릭 실패 — 기대 ({diagX},{diagY}), 실제 {diagonalCell}");

        Debug.Log("[Nyangbingo] Dev A mining cell surface fallback test completed.");
    }

    /// <summary>
    /// Pass 4b 연결 통로가 입구/스폰 열에서 지표 안전지대(crust / surface_y=20 밴드)까지
    /// 수직 관통하지 않는지 검증. 공식 얕은 입구(CarveSpawnEntrance, SpawnEntranceDepthTiles)만 허용.
    /// </summary>
    private static void AssertNoConnectivityShaftInSurfaceBand(WorldGenerationResult result, WorldGenerationConfig config)
    {
        var seed = result.acceptedSeed;
        var entranceX = Mathf.Clamp(result.spawnPoint.x - 1, 1, result.width - 2);
        var spawnX = result.spawnPoint.x;
        var crust = Mathf.Max(1, config.CaveSurfaceCrustThickness);

        var forbiddenMinYMethod = typeof(MapGenerator).GetMethod("GetConnectivityDigForbiddenMinY",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert(forbiddenMinYMethod != null, "GetConnectivityDigForbiddenMinY 리플렉션 실패");

        foreach (var columnX in new[] { entranceX, spawnX })
        {
            var colSurfaceY = result.surfaceHeights[columnX];
            var colOfficialBottom = config.GetSpawnEntranceOfficialBottom(colSurfaceY);
            var colEntranceDepth = colSurfaceY - colOfficialBottom + 1;
            Assert(colEntranceDepth <= config.SpawnEntranceDepthTiles,
                $"seed {seed}: 열 {columnX} 공식 입구 깊이 {colEntranceDepth} > SpawnEntranceDepthTiles {config.SpawnEntranceDepthTiles}");
            var colBandFloor = Mathf.Min(
                colSurfaceY - crust + 1,
                Mathf.Max(config.BedrockThickness, result.height - 20));

            // 지표 안전지대 안 공기는 공식 입구(SpawnEntranceDepthTiles) 구간만 허용.
            for (var y = colBandFloor; y <= colSurfaceY; y++)
            {
                if (!result.tiles[columnX, y].IsAir) continue;
                Assert(y >= colOfficialBottom,
                    $"seed {seed}: 열 {columnX} y={y} 공기가 지표 안전지대에 있음 — 연결 통로 수직 직통 재발");
            }

            // 공식 입구보다 깊게 이어지는 연속 공기 run이 과하면 직통 샤프트.
            var airRunBelowEntrance = 0;
            for (var y = colOfficialBottom - 1; y >= config.BedrockThickness; y--)
            {
                if (!result.tiles[columnX, y].IsAir) break;
                airRunBelowEntrance++;
            }
            Assert(airRunBelowEntrance <= config.CaveMaxHeight,
                $"seed {seed}: 열 {columnX} 입구 아래 연속 공기 {airRunBelowEntrance} > cave_max_height — 지표 관통 직통");

            var forbiddenMinY = (int)forbiddenMinYMethod.Invoke(null,
                new object[] { columnX, result.surfaceHeights, config });
            Assert(forbiddenMinY <= 15,
                $"seed {seed}: 연결 통로 금지 상한 y={forbiddenMinY} — 심층(y&lt;15)만 허용해야 함");
        }
    }

    /// <summary>
    /// 구 연결 샤프트(전깊이·다열 우물) 재발만 잡는다.
    /// 폐허/상층 상자가 지표 1칸만 비우는 경우는 허용(거대 싱크홀이 아님).
    /// </summary>
    private static void AssertNoGiantConnectivityShaft(WorldGenerationResult result, WorldGenerationConfig config)
    {
        var seed = result.acceptedSeed;
        var deepSpan = Mathf.Max(config.CaveMaxHeight * 2, 24);

        // A) 지표가 열려 있고 그 아래로 deepSpan 이상 연속 공기 → 진짜 싱크홀 열
        var surfaceSink = new bool[result.width];
        for (var x = 0; x < result.width; x++)
        {
            var surfaceY = result.surfaceHeights[x];
            if (!result.tiles[x, surfaceY].IsAir)
            {
                surfaceSink[x] = false;
                continue;
            }

            var airRun = 0;
            for (var y = surfaceY; y >= config.BedrockThickness; y--)
            {
                if (!result.tiles[x, y].IsAir) break;
                airRun++;
            }
            surfaceSink[x] = airRun >= deepSpan;
        }

        Assert(MaxConsecutiveTrue(surfaceSink) <= 1,
            $"seed {seed}: 지표 개방+장수직 싱크홀 열이 {MaxConsecutiveTrue(surfaceSink)}열 연속 — 거대 수직 구멍 재발");

        // B) 지표 바로 아래부터 긴 수직 공기가 3열 이상 나란히(구 +자 우물)
        var longShaft = new bool[result.width];
        for (var x = 0; x < result.width; x++)
        {
            var surfaceY = result.surfaceHeights[x];
            var airRun = 0;
            for (var y = surfaceY - 1; y >= config.BedrockThickness; y--)
            {
                if (!result.tiles[x, y].IsAir) break;
                airRun++;
            }
            longShaft[x] = airRun >= deepSpan;
        }

        Assert(MaxConsecutiveTrue(longShaft) <= 2,
            $"seed {seed}: 지표 근처 장수직 공기 열이 {MaxConsecutiveTrue(longShaft)}열 연속 — 거대 수직 우물(구 연결 샤프트) 재발");
    }

    private static int MaxConsecutiveTrue(bool[] flags)
    {
        var maxWidth = 0;
        var run = 0;
        for (var i = 0; i < flags.Length; i++)
        {
            if (flags[i])
            {
                run++;
                if (run > maxWidth) maxWidth = run;
            }
            else run = 0;
        }
        return maxWidth;
    }

    private static void AssertNoSurfaceBreakthrough(TileData[,] grid, int[] surfaceHeights,
        WorldGenerationConfig config, int seed)
    {
        var crust = Mathf.Max(1, config.CaveSurfaceCrustThickness);
        for (var x = 0; x < config.MapWidth; x++)
        {
            var surfaceY = surfaceHeights[x];
            var sealFrom = Mathf.Max(config.BedrockThickness, surfaceY - crust + 1);
            for (var y = sealFrom; y <= surfaceY; y++)
            {
                Assert(!grid[x, y].IsAir,
                    $"seed {seed}: Pass 2 이후 Top Safety Zone({x},{y})이 뚫림 — crust={crust}");
            }
        }
    }

    private static void AssertNoOversizedCaveChamber(TileData[,] grid, int[] surfaceHeights,
        WorldGenerationConfig config, int seed)
    {
        var width = config.MapWidth;
        var maxH = config.CaveMaxHeight;
        var visited = new bool[width, config.MapHeight];
        var queue = new Queue<Vector2Int>();
        // 8방: PostProcess와 동일 기준(대각선 포함 세로 연결).
        var neighbors = new[]
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
            new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
        };

        for (var x = 0; x < width; x++)
        {
            var surfaceY = surfaceHeights[x];
            for (var y = config.BedrockThickness; y <= surfaceY; y++)
            {
                if (!grid[x, y].IsAir || visited[x, y]) continue;

                queue.Clear();
                queue.Enqueue(new Vector2Int(x, y));
                visited[x, y] = true;
                var minY = y;
                var maxY = y;

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    if (current.y < minY) minY = current.y;
                    if (current.y > maxY) maxY = current.y;

                    foreach (var offset in neighbors)
                    {
                        var next = current + offset;
                        if (next.x < 0 || next.x >= width) continue;
                        if (next.y < config.BedrockThickness || next.y > surfaceHeights[next.x]) continue;
                        if (!grid[next.x, next.y].IsAir || visited[next.x, next.y]) continue;
                        visited[next.x, next.y] = true;
                        queue.Enqueue(next);
                    }
                }

                var span = maxY - minY + 1;
                Assert(span <= maxH,
                    $"seed {seed}: Pass 2 동굴 공동 세로 길이 {span} > cave_max_height {maxH} (y {minY}~{maxY})");
            }
        }
    }

    /// <summary>관심 y 구간에서 열별 연속 공기 run이 maxRun 이하여야 한다(seed 1 y71~85 직접 검증).</summary>
    private static void AssertNoVerticalAirRunInRange(TileData[,] grid, WorldGenerationConfig config,
        int seed, int rangeMinY, int rangeMaxY, int maxRun)
    {
        for (var x = 0; x < config.MapWidth; x++)
        {
            var run = 0;
            for (var y = rangeMaxY; y >= rangeMinY; y--)
            {
                if (y < config.BedrockThickness) break;
                if (grid[x, y].IsAir)
                {
                    run++;
                    if (run > maxRun)
                        throw new InvalidOperationException(
                            $"seed {seed}: 열 {x} y {rangeMinY}~{rangeMaxY} 연속 공기 run {run} > {maxRun}");
                }
                else run = 0;
            }
        }
    }

    /// <summary>특정 y 관심 구간에서 시작해도 8방 연결 공동 span이 maxSpan 이하여야 한다.</summary>
    private static void AssertNoAirSpanInRange(TileData[,] grid, int[] surfaceHeights,
        WorldGenerationConfig config, int seed, int rangeMinY, int rangeMaxY, int maxSpan)
    {
        var width = config.MapWidth;
        var visited = new bool[width, config.MapHeight];
        var queue = new Queue<Vector2Int>();
        var neighbors = new[]
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
            new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
        };

        for (var x = 0; x < width; x++)
        {
            for (var y = rangeMinY; y <= rangeMaxY; y++)
            {
                if (y < config.BedrockThickness || y > surfaceHeights[x]) continue;
                if (!grid[x, y].IsAir || visited[x, y]) continue;

                queue.Clear();
                queue.Enqueue(new Vector2Int(x, y));
                visited[x, y] = true;
                var minY = y;
                var maxY = y;

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    if (current.y < minY) minY = current.y;
                    if (current.y > maxY) maxY = current.y;
                    foreach (var offset in neighbors)
                    {
                        var next = current + offset;
                        if (next.x < 0 || next.x >= width) continue;
                        if (next.y < config.BedrockThickness || next.y > surfaceHeights[next.x]) continue;
                        if (!grid[next.x, next.y].IsAir || visited[next.x, next.y]) continue;
                        visited[next.x, next.y] = true;
                        queue.Enqueue(next);
                    }
                }

                var span = maxY - minY + 1;
                Assert(span <= maxSpan,
                    $"seed {seed}: 구간 검사 실패 — y {minY}~{maxY} span {span} > {maxSpan} (관심 구간 {rangeMinY}~{rangeMaxY})");
            }
        }
    }

    private static void AssertSafeSpawnResult(WorldGenerationConfig config, WorldGenerationResult result)
    {
        var seed = result.acceptedSeed;
        var spawn = result.spawnPoint;
        var surfaceY = result.surfaceHeights[Mathf.Clamp(spawn.x, 0, result.width - 1)];
        Assert(spawn.y == surfaceY + 1,
            $"seed {seed}: 스폰이 지표면 바로 위가 아님(spawn.y={spawn.y}, surfaceY={surfaceY})");

        var ground = result.tiles[spawn.x, surfaceY];
        Assert(!ground.IsAir && ground.isNaturalTerrain,
            $"seed {seed}: 스폰 발밑이 파괴되지 않은 자연 전경이 아님");
        Assert(result.tiles[spawn.x, spawn.y].IsAir, $"seed {seed}: 스폰 발 칸이 공기가 아님");
        Assert(spawn.y + 1 < result.height && result.tiles[spawn.x, spawn.y + 1].IsAir,
            $"seed {seed}: 스폰 머리 칸이 공기가 아님");

        // 중앙/지하 알코브 스폰 금지 — 지표 바로 위(+1)만 허용(발밑 고체는 위에서 이미 확인).
        Assert(spawn.y > surfaceY, $"seed {seed}: 스폰이 지표 이하(지하)에 있음");
    }

    private static Vector3Int FindUndergroundNaturalSolid(TileService tileService, WorldGenerationResult result,
        WorldTilemapRenderer renderer = null)
    {
        for (var x = 0; x < result.width; x++)
        {
            var surfaceY = result.surfaceHeights[x];
            for (var y = surfaceY - 2; y >= 5; y--)
            {
                var cell = new Vector3Int(x, y, 0);
                var tile = tileService.GetTile(cell);
                if (tile.IsAir || !tile.isNaturalTerrain || !tile.HasNaturalBackground) continue;
                if (tile.elementType == WorldTileTypes.Bedrock || tile.elementType == WorldTileTypes.IceAltar)
                    continue;
                // 렌더러가 있으면 전경 TileBase가 실제로 매핑된 칸만 채굴 픽스처로 사용.
                if (renderer != null && !renderer.TryGetTileBase(tile.elementType, out _))
                    continue;
                return cell;
            }
        }
        throw new InvalidOperationException("지하 자연 고체(배경 포함) 칸을 찾지 못함");
    }

    private static Vector3Int FindEmptyBackgroundAir(TileService tileService, WorldGenerationResult result)
    {
        for (var x = 0; x < result.width; x++)
        {
            var surfaceY = result.surfaceHeights[x];
            for (var y = surfaceY + 1; y < result.height; y++)
            {
                var cell = new Vector3Int(x, y, 0);
                var tile = tileService.GetTile(cell);
                if (tile.IsAir && !tile.HasBackground && !tile.HasNaturalBackground)
                    return cell;
            }
        }
        throw new InvalidOperationException("빈 배경 공기 칸을 찾지 못함");
    }

    /// <summary>테스트 전용: BuildMinimalRenderer가 만든 최소 렌더러에 A-14 먹선 오버레이 Tilemap과
    /// 더미 모양 타일 5장을 SerializedObject 경로로 배선한다(인스펙터 드래그앤드롭을 코드로 재현).</summary>
    private static Tilemap WireEdgeOverlay(GameObject host, WorldTilemapRenderer renderer, Tile[] shapeTilesOut)
    {
        var edgeOverlayObject = new GameObject("EdgeOverlay");
        edgeOverlayObject.transform.SetParent(host.transform, false);
        var edgeOverlayTilemap = edgeOverlayObject.AddComponent<Tilemap>();
        edgeOverlayObject.AddComponent<UnityTilemapRenderer>();

        for (var i = 0; i < shapeTilesOut.Length; i++)
        {
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = $"DevA_EdgeShape_{i}";
            shapeTilesOut[i] = tile;
        }

        var serialized = new SerializedObject(renderer);
        serialized.FindProperty("edgeOverlayTilemap").objectReferenceValue = edgeOverlayTilemap;
        var shapeTilesProperty = serialized.FindProperty("edgeShapeTiles");
        shapeTilesProperty.arraySize = shapeTilesOut.Length;
        for (var i = 0; i < shapeTilesOut.Length; i++)
            shapeTilesProperty.GetArrayElementAtIndex(i).objectReferenceValue = shapeTilesOut[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return edgeOverlayTilemap;
    }
}
