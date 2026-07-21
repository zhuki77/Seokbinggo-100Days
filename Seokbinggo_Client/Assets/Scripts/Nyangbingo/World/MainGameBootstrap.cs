using System;
using System.Collections.Generic;
using Nyangbingo.Core;
using Nyangbingo.Data;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>
    /// 실제 플레이 씬의 월드 세션 진입점. 씬에 배치된 시간 서비스와 중앙 Tick 드라이버를 하나의
    /// <see cref="WorldSessionController"/>에 묶고, 신규 월드가 준비된 뒤에만 외부 소비자에게 공개한다.
    /// 디버그 하네스에 의존하지 않으므로 이후 저장·스폰·UI 시스템은 이 컴포넌트의 안정적인 접근자를
    /// 기준으로 단계적으로 연결할 수 있다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(DayNightService), typeof(CentralTickDriver))]
    public sealed class MainGameBootstrap : MonoBehaviour
    {
        [Header("정본 데이터")]
        [SerializeField] private WorldGenerationConfig worldConfig;
        [SerializeField] private GameDataCatalog gameDataCatalog;

        [Header("씬 서비스")]
        [SerializeField] private TilemapRenderer tilemapRenderer;
        [SerializeField] private DayNightService dayNightService;
        [SerializeField] private CentralTickDriver tickDriver;

        [Header("초기 월드")]
        [SerializeField] private bool startWorldOnStart = true;
        [SerializeField] private int initialSeed = 12345;

        private WorldSessionController session;
        private bool servicesInitialized;

        public WorldSessionController Session => session;
        public TileService TileService => session?.TileService;
        public SealSystem SealSystem => session?.SealSystem;
        public TilemapRenderer WorldRenderer => tilemapRenderer;
        public DayNightService TimeService => dayNightService;
        public GameDataCatalog GameDataCatalog => gameDataCatalog;
        public IGameSecondsTickDriver TickDriver => tickDriver;
        public bool IsWorldReady => session?.HasWorld == true;

        /// <summary>신규 생성 또는 로드가 끝나 모든 라이브 참조가 최신 상태가 된 직후 발행한다.</summary>
        public event Action WorldReady;

        /// <summary>
        /// 에디터의 재현 가능한 씬 생성 도구가 필수 참조를 직접 확정할 때 사용하는 진입점.
        /// SerializedObject를 통한 에셋 참조 대입이 씬 저장 전에 소실되는 경우를 피하기 위해 실제
        /// 직렬화 필드에 바로 대입한다. 호출자는 이후 SetDirty와 SaveScene을 수행해야 한다.
        /// </summary>
        public void ConfigureForScene(
            WorldGenerationConfig config,
            GameDataCatalog catalog,
            TilemapRenderer renderer,
            DayNightService timeService,
            CentralTickDriver centralTickDriver,
            bool autoStart,
            int seed)
        {
            worldConfig = config;
            gameDataCatalog = catalog;
            tilemapRenderer = renderer;
            dayNightService = timeService;
            tickDriver = centralTickDriver;
            startWorldOnStart = autoStart;
            initialSeed = seed;
        }

        private void Start()
        {
            if (startWorldOnStart && !IsWorldReady)
                StartNewWorld(initialSeed);
        }

        /// <summary>
        /// 공식 데이터, 시간 서비스, 중앙 Tick 드라이버와 월드 세션을 정확히 한 번 연결한다.
        /// 필수 참조가 빠졌거나 공식 globals/day-curve 적용에 실패하면 부분 초기화를 라이브 월드로
        /// 승격하지 않고 false를 반환한다.
        /// </summary>
        public bool InitializeServices()
        {
            if (servicesInitialized) return true;

            dayNightService ??= GetComponent<DayNightService>();
            tickDriver ??= GetComponent<CentralTickDriver>();

            var missing = new List<string>();
            if (worldConfig == null) missing.Add(nameof(worldConfig));
            if (gameDataCatalog == null) missing.Add(nameof(gameDataCatalog));
            if (tilemapRenderer == null) missing.Add(nameof(tilemapRenderer));
            if (dayNightService == null) missing.Add(nameof(dayNightService));
            if (tickDriver == null) missing.Add(nameof(tickDriver));

            if (missing.Count > 0)
            {
                Debug.LogError($"[Nyangbingo] MainGameBootstrap: 필수 배선 누락 — {string.Join(", ", missing)}.");
                return false;
            }

            if (!dayNightService.ConfigureOfficialData(gameDataCatalog))
            {
                Debug.LogError("[Nyangbingo] MainGameBootstrap: 공식 globals/day-curve 데이터를 시간 서비스에 " +
                               "적용하지 못했습니다.");
                return false;
            }

            tickDriver.Configure(dayNightService);

            session = new WorldSessionController(worldConfig, tilemapRenderer, gameDataCatalog);
            session.BindTimeService(dayNightService);
            session.BindTickDriver(tickDriver);
            session.WorldLoaded += HandleWorldLoaded;
            servicesInitialized = true;

            Debug.Log("[Nyangbingo] MainGameBootstrap: 월드 세션·공식 시간·중앙 Tick 배선 완료.");
            return true;
        }

        public bool StartNewWorld(int seed)
        {
            if (!InitializeServices()) return false;

            try
            {
                session.StartNewWorld(seed);
                return true;
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogError($"[Nyangbingo] MainGameBootstrap: 신규 월드 시작 실패. {exception.Message}");
                return false;
            }
        }

        private void HandleWorldLoaded()
        {
            Debug.Log($"[Nyangbingo] MainGameBootstrap: 월드 준비 완료 " +
                      $"(seed={session.Seed}, day={dayNightService.Day}, tickDriver={tickDriver != null}).");
            WorldReady?.Invoke();
        }

        private void OnDestroy()
        {
            if (session != null)
            {
                session.WorldLoaded -= HandleWorldLoaded;
                session.Dispose();
                session = null;
            }

            servicesInitialized = false;
        }
    }
}
