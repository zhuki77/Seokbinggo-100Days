using System;
using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using Nyangbingo.Save;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>
    /// 개발 A의 월드 세션 오케스트레이터. 새 게임 시작, 저장 캡처, 로드 복원, 상자 개봉을 한 곳에서
    /// 관리한다. 타일 diff/상자 검증·저장 규칙 자체는 이미 Dev B가 <see cref="Nyangbingo.Save.WorldSaveAdapter"/>
    /// (Save/SaveGame.cs)로 구현·검증해 두었으므로 여기서 다시 구현하지 않고 그대로 소비한다 —
    /// 이 클래스의 역할은 "같은 시드로 월드를 결정론적으로 재생성 → TileService/SealSystem을 새로 구성 →
    /// Dev B 어댑터로 diff/상자 상태를 적용" 순서를 조립하는 접착 계층이다.
    ///
    /// 복원 순서(DEV_B_TO_DEV_A_HANDOFF.md §11.6, Development Part A 범위):
    ///  1) 저장된 seed로 월드를 깨끗하게 재생성한다.
    ///  2) TileService.RestoreTileChanges로 타일 변경 이력을 그대로 재생해 타일맵을 복원한다.
    ///  3) WorldSaveAdapter.RestoreChests로 20개 상자의 열림 상태를 복원한다
    ///     (이무기 제단은 파괴 불가 + 시드로만 결정되는 타일이라 재생성만으로 이미 원상 복구됨).
    ///  4) 타일맵 렌더러를 갱신하고, SealSystem.InvalidateAll()로 밀폐 캐시를 초기화한다.
    ///
    /// "개발 A 보완 작업 명세서" §5(개발 B와의 연결 계약) 대응: 이 클래스가 개발 B가 참조해야 하는
    /// 유일한 "안정적 세션 접근자"다 — <see cref="TileService"/>/<see cref="SealSystem"/> 외에도
    /// <see cref="TimeService"/>/<see cref="TickDriver"/>를 이 클래스에서 그대로 조회할 수 있고(내부
    /// 구현을 추측해 폐기 예정인 디버그 하네스를 뒤질 필요가 없다), <see cref="WorldLoaded"/> 이벤트로
    /// "월드 교체가 지금 막 끝났다"를 능동적으로 감지할 수 있다.
    /// </summary>
    public sealed class WorldSessionController : IDisposable
    {
        private readonly WorldGenerationConfig config;
        private readonly TilemapRenderer renderer;
        private readonly GameDataCatalog catalog;

        private MapGenerator generator;
        private TileService tileService;
        private SealSystem sealSystem;
        private ChestProgress chestProgress = new ChestProgress();
        private int seed;
        private bool disposed;

        // SealSystem이 새로 생성될 때마다(RebuildLiveSystems/LoadSnapshot) 재주입해야 하는 B파트 확장 지점.
        // WorldSessionController가 대신 들고 있다가, SealSystem 인스턴스가 바뀔 때마다 그대로 재연결한다.
        private ISealBarrierRegistry sealBarrierRegistry;
        private ICoolingSourceProvider coolingSourceProvider;

        public TileService TileService => tileService;
        public SealSystem SealSystem => sealSystem;
        public ChestProgress ChestProgress => chestProgress;
        public MapGenerator Generator => generator;
        public int Seed => seed;
        public WorldGenerationResult LastResult { get; private set; }
        public bool HasWorld => tileService != null;

        /// <summary>
        /// §5 항목 1/8: game seconds·낮/밤 이벤트의 단일 기준. <see cref="BindTimeService"/>로 세션 생성
        /// 이후 아무 때나 주입할 수 있고(널이어도 세션 자체는 정상 동작), 주입돼 있으면 개발 B가 이 프로퍼티
        /// 하나로 <see cref="IGameSecondsSource"/>/<see cref="ISaveableTimeSource"/> 양쪽 계약을 모두 얻는다.
        /// </summary>
        public DayNightService TimeService { get; private set; }

        /// <summary>§5 항목 2: 제작/제련/AI 등 delta game seconds 소비자를 등록·해제하는 진입점(A-04).</summary>
        public IGameSecondsTickDriver TickDriver { get; private set; }

        /// <summary>
        /// §5 항목 7: <see cref="StartNewWorld"/>(신규 시작) 또는 <see cref="LoadSnapshot"/>(로드)이 성공해
        /// 라이브 <see cref="TileService"/>/<see cref="SealSystem"/> 참조가 실제로 교체된 직후 정확히 한 번
        /// 발행된다. 구독 시점 이후의 교체만 통지하므로(과거 이벤트 재전송 없음), 세션 생성 직후 구독해도
        /// 첫 <see cref="StartNewWorld"/> 완료를 그대로 받는다. 실패한 시도는 라이브 상태를 바꾸지 않으므로
        /// 이 이벤트도 발행되지 않는다(A-06/A-08의 "부분 교체 금지" 원칙과 동일하게 대칭 보장).
        /// </summary>
        public event Action WorldLoaded;

        public WorldSessionController(WorldGenerationConfig config, TilemapRenderer renderer, GameDataCatalog catalog)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
            this.catalog = catalog; // 카탈로그 없이도(상자 보상 미연결) 채굴/밀폐 테스트는 가능해야 한다.
        }

        /// <summary>
        /// §5 항목 1: 세션이 소유하지 않는(씬에 별도로 존재하는 MonoBehaviour) DayNightService를 나중에
        /// 연결한다. null을 넘기면 아직 준비되지 않은 것으로 간주해 <see cref="TimeService"/>가 그대로 null을
        /// 반환한다 — 세션의 나머지 기능(월드 생성/세이브/로드)은 TimeService 없이도 완전히 동작한다.
        /// </summary>
        public void BindTimeService(DayNightService timeService) => TimeService = timeService;

        /// <summary>
        /// §5 항목 2: 세션이 소유하지 않는 CentralTickDriver를 나중에 연결한다. 위 <see cref="BindTimeService"/>와
        /// 동일한 이유로 null 허용.
        /// </summary>
        public void BindTickDriver(IGameSecondsTickDriver tickDriver) => TickDriver = tickDriver;

        /// <summary>
        /// 차열벽/차열 지붕/단열 문(밀폐 화이트리스트) 조회자와 냉기원 가동 상태 조회자를 연결한다. B파트
        /// 설치물/온도 시스템이 아직 준비되지 않았으면 둘 다 null로 두면 되고(SealSystem은 그 경우 기존
        /// 동작을 그대로 유지한다), 준비되면 이 메서드 한 번으로 현재 SealSystem에 즉시 반영되고, 이후
        /// 로드 등으로 SealSystem이 재생성돼도(RebuildLiveSystems/LoadSnapshot) 자동으로 다시 연결된다.
        /// </summary>
        public void ConfigureSealExtensions(ISealBarrierRegistry barrierRegistry, ICoolingSourceProvider coolingSourceProvider)
        {
            sealBarrierRegistry = barrierRegistry;
            this.coolingSourceProvider = coolingSourceProvider;
            ApplySealExtensions();
        }

        private void ApplySealExtensions()
        {
            if (sealSystem == null) return;
            sealSystem.SetBarrierRegistry(sealBarrierRegistry);
            sealSystem.SetCoolingSourceProvider(coolingSourceProvider);
        }

        /// <summary>
        /// 결정론적 4패스 생성을 처음부터 실행해 새 게임을 시작한다. A-08: MapGenerator가 이미
        /// WorldGenerationConfig.MaxRerollAttempts까지 seed+1로 재시도했는데도 검증(스폰 접근성/온보딩
        /// 자원/심층 연결/제단 도달성)을 통과하지 못했다면, 그 결과를 "정상 월드처럼 조용히" 라이브 상태로
        /// 승격시키지 않는다 — 렌더러/타일서비스/밀폐 시스템을 만들기 전에 예외로 시작을 중단해, 호출자가
        /// (Try/Catch로) 실패를 명확히 인지하고 처리하게 한다.
        /// </summary>
        /// <exception cref="InvalidOperationException">최대 재시도 후에도 월드 생성 검증에 실패한 경우.</exception>
        public WorldGenerationResult StartNewWorld(int requestedSeed)
        {
            generator = new MapGenerator(config);
            var result = generator.GenerateDetailed(requestedSeed);

            if (!result.passedValidation)
            {
                throw new InvalidOperationException(
                    $"[Nyangbingo] WorldSessionController: seed {requestedSeed} 기준 {result.rerollAttempts}회 재시도(최종 seed " +
                    $"{result.acceptedSeed})까지도 월드 생성 검증(스폰 접근성/온보딩 자원/심층 연결/제단 도달성)에 실패했습니다. " +
                    "WorldGenerationConfig 값 또는 시드를 확인하세요 — 이 월드는 라이브 상태로 시작되지 않습니다.");
            }

            seed = result.acceptedSeed; // 리롤이 있었을 수 있으므로 항상 확정 시드를 세이브 기준으로 삼는다.
            LastResult = result;

            renderer.RenderWorld(result.tiles);
            RebuildLiveSystems(result.tiles);
            chestProgress = new ChestProgress();
            WorldLoaded?.Invoke(); // §5 항목 7 — 라이브 참조 교체가 전부 끝난 뒤에만 통지한다.
            return result;
        }

        /// <summary>현재 라이브 상태(타일 diff + 상자 개봉 여부)를 save에 캡처한다.</summary>
        public bool CaptureSnapshot(SaveGame save)
        {
            if (save == null || !HasWorld) return false;
            save.seed = seed;
            return Nyangbingo.Save.WorldSaveAdapter.CaptureWorld(save, tileService.GetTileChangeRecords(),
                Array.Empty<PlacedObjectRecord>(), generator, chestProgress);
        }

        /// <summary>
        /// save.seed로 월드를 결정론적으로 재생성한 뒤, 저장된 타일 diff와 상자 상태를 그대로 재생해
        /// 로드 이전의 최종 모습으로 복원한다. A-06: 전체 과정을 트랜잭션처럼 처리한다 — 검증/재생 단계에서는
        /// renderer를 전혀 건드리지 않는 "임시" TileService(renderer=null)를 쓰고, 모든 단계가 성공했을 때만
        /// 라이브 참조(generator/tileService/chestProgress/seed/LastResult)를 교체한 뒤 실제 renderer를
        /// 연결하고 Tilemap을 딱 한 번 그린다. 중간에 하나라도 실패하면 즉시 false를 반환하고, 이전 라이브
        /// 상태와 화면(Tilemap)은 물리적으로 전혀 바뀌지 않는다(§11.6: "복원 실패를 무시하고 일부 상태로
        /// 게임을 시작하지 않는다").
        /// </summary>
        public bool LoadSnapshot(SaveGame save)
        {
            if (save == null) return false;
            save.NormalizeAfterLoad();
            if (!Nyangbingo.Save.WorldSaveAdapter.ValidateWorldRecords(save)) return false;

            var loadedGenerator = new MapGenerator(config);
            var result = loadedGenerator.GenerateDetailed(save.seed);
            if (result.acceptedSeed != save.seed)
            {
                // 저장된 시드가 더 이상 같은 검증 통과 맵을 재현하지 못한다 — 룰/시드가 바뀐 손상된 세이브다.
                Debug.LogError($"[Nyangbingo] WorldSessionController: seed {save.seed} 재생성 결과가 저장 시점과 달라 로드를 중단합니다.");
                return false;
            }

            // 1) 검증/재생 전용 TileService — renderer를 null로 둬서 이 단계에서는 화면(Tilemap)에 어떤 것도
            // 그리지 않는다. RestoreTileChanges 내부의 보호 타일/알려진 tileId/좌표 검증(TileService.cs) 중
            // 하나라도 실패하면 이 인스턴스와 result.tiles는 그냥 버려지고, 기존 라이브 상태·화면은 손끝 하나
            // 닿지 않는다.
            var loadedTileService = new TileService(result.tiles, null, catalog, result.acceptedSeed);
            if (!loadedTileService.RestoreTileChanges(save.tileChanges))
            {
                Debug.LogError("[Nyangbingo] WorldSessionController: 타일 변경 이력 재생에 실패해 로드를 중단합니다.");
                return false;
            }

            // 2) 이무기 제단은 파괴 불가 + 시드로만 결정되는 타일이라 재생성만으로 이미 원상 복구돼 있다.
            // 상자는 사용자 상호작용 결과(열림 여부)가 시드로 재현되지 않으므로 별도 복원이 필요하다.
            var loadedChestProgress = new ChestProgress();
            if (!Nyangbingo.Save.WorldSaveAdapter.RestoreChests(save, loadedGenerator, loadedChestProgress))
            {
                Debug.LogError("[Nyangbingo] WorldSessionController: 상자 상태 복원에 실패해 로드를 중단합니다.");
                return false;
            }

            // 여기까지 전부 성공했다 — 이제부터만 라이브 상태를 교체한다(부분 복원 금지).
            generator = loadedGenerator;
            tileService = loadedTileService;
            tileService.BindRenderer(renderer); // 검증 동안 꺼두었던 렌더러를 이제서야 연결한다.
            chestProgress = loadedChestProgress;
            seed = result.acceptedSeed;
            LastResult = result;

            // 3) 타일맵 렌더러 갱신 — diff가 이미 반영된 배열을 한 번에 SetTilesBlock으로 그린다.
            renderer.RenderWorld(result.tiles);

            // 4) SealSystem은 Dispose하지 않고 내부 TileService 참조만 교체한다(A-07) — 주 관찰 지점/고정
            // 관찰 지점, WatchPointSealChanged 구독자, 외부 시스템이 들고 있던 SealSystem 참조가 로드
            // 전후로 그대로 유지된다. 세션 최초 생성(sealSystem == null)일 때만 새로 만든다.
            if (sealSystem == null)
            {
                sealSystem = new SealSystem(tileService);
                ApplySealExtensions();
            }
            else
            {
                sealSystem.Rebind(tileService);
            }

            WorldLoaded?.Invoke(); // §5 항목 7 — 라이브 참조 교체가 전부 끝난 뒤에만 통지한다.
            return true;
        }

        /// <summary>
        /// 우클릭 등으로 특정 셀을 상자로 취급해 열어본다. 성공하면 chestId를 돌려주고, 지역(Ruins/Upper/
        /// Middle/Deep)에 해당하는 chests.csv 보상 풀(§7)에서 아이템/액세서리를 ChestProgress.TryOpen 경로로
        /// 지급한다. 카탈로그가 없거나 해당 지역 ChestDefinition을 못 찾으면 안전하게 실패로 처리한다.
        /// </summary>
        public bool TryOpenChestAt(Vector3Int cell, out string chestId, out ChestDefinition definition)
        {
            chestId = null;
            definition = null;
            if (!HasWorld || catalog == null) return false;
            if (!generator.TryGetChestIdAt(new Vector2Int(cell.x, cell.y), out var foundChestId)) return false;
            if (chestProgress.IsOpened(foundChestId)) return false;

            var region = generator.GetChestRegion(foundChestId);
            var found = catalog.FindChest(RegionCatalogId(region));
            if (found == null || !chestProgress.TryOpen(foundChestId, found, seed)) return false;

            chestId = foundChestId;
            definition = found;
            return true;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            sealSystem?.Dispose();
        }

        private void RebuildLiveSystems(TileData[,] tiles)
        {
            sealSystem?.Dispose();
            tileService = new TileService(tiles, renderer, catalog, seed);
            sealSystem = new SealSystem(tileService);
            ApplySealExtensions();
        }

        private static string RegionCatalogId(ChestRegion region) => region switch
        {
            ChestRegion.Ruins => "ruins_chest",
            ChestRegion.Upper => "upper_chest",
            ChestRegion.Middle => "middle_chest",
            _ => "deep_chest"
        };
    }
}
