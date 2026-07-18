using System;
using System.Collections.Generic;
using System.Reflection;
using Nyangbingo.Core;
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

    private sealed class FakeCoolingSourceProvider : ICoolingSourceProvider
    {
        public bool IsColdSourceActive { get; set; }
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

            Assert(!session.LoadSnapshot(corruptSave), "범위 밖 좌표를 담은 손상된 저장 데이터가 거부되지 않음");
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
}
