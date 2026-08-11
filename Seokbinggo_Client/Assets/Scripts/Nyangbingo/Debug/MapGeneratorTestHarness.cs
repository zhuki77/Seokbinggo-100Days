using System.Collections.Generic;
using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using Nyangbingo.Save;
using Nyangbingo.World;
using UnityEngine;

namespace Nyangbingo.Debugging
{
    /// <summary>
    /// 개발 A 임시 시각화 하네스. 메인 게임(Tilemap/타일 팔레트)에 연결하기 전, MapGenerator가 실제로
    /// 만들어내는 맵을 눈으로 확인하기 위한 디버그 전용 컴포넌트다. 실제 렌더링 파이프라인이 준비되면 폐기한다.
    /// </summary>
    public sealed class MapGeneratorTestHarness : MonoBehaviour
    {
        [SerializeField] private WorldGenerationConfig config;
        [SerializeField] private int seed = 12345;
        [SerializeField] private bool logLegend = true;

        [Header("실제 Tilemap 렌더링 (비워두면 디버그 텍스처 프리뷰로 폴백)")]
        [SerializeField] private TilemapRenderer tilemapRenderer;

        [Header("선택: 채굴/상자 테스트용 (비워두면 아이템 드랍·상자 보상 없이 파괴/설치만 동작)")]
        [SerializeField] private GameDataCatalog catalog;

        [Header("5단계: 세이브/로드 테스트 (F5 저장 / F9 로드)")]
        [SerializeField] private bool enableSaveLoadHotkeys = true;
        [SerializeField] private SaveManager saveManager;
        [Range(0, SaveManager.SlotCount - 1)][SerializeField] private int saveSlot = 0;

        [Header("6단계: 낮/밤 사이클 + 요괴 스폰 쿼리 테스트")]
        [SerializeField] private DayNightService dayNightService;
        [SerializeField] private bool logDayNightTransitions = true;
        [Tooltip("밤이 시작될 때 GetValidSpawnPositions를 한 번 호출해 스폰 후보를 보라색 마커로 표시한다(스폰 지점 기준).")]
        [SerializeField] private bool showSpawnQueryDemoAtNight = true;
        [Min(0)][SerializeField] private int spawnQueryMinRange = 4;
        [Min(1)][SerializeField] private int spawnQueryMaxRange = 12;

        [Header("7단계: 새벽 자동 저장(DawnAutoSave) 배선 — DEV_B_TO_DEV_A_HANDOFF.md §11.4")]
        [SerializeField] private bool enableDawnAutoSave = true;
        [SerializeField] private WorldSessionSaveProviderAdapter saveProviderAdapter;
        [SerializeField] private DawnAutoSave dawnAutoSave;

        [Header("8단계: 중앙 game seconds Tick 드라이버 (A-04) — 제작/제련/AI 등 Dev B 소비자가 연결하는 지점")]
        [SerializeField] private bool enableCentralTickDriver = true;
        [SerializeField] private CentralTickDriver tickDriver;
        [Tooltip("Tick 드라이버가 실제로 누적값을 relay하는지 눈으로 확인하기 위한 Dev A 자체 테스트 소비자.")]
        [SerializeField] private bool registerTickProbe = true;
        private DevATickProbe tickProbe;

        private const int MaxSpawnQueryMarkers = 40;

        private WorldSessionController session;
        private readonly List<GameObject> chestMarkers = new List<GameObject>();
        private readonly List<GameObject> spawnQueryMarkers = new List<GameObject>();

        /// <summary>WorldSessionController가 소유한 살아있는 타일 상태. PlayerMiningController 등이 이 인스턴스로 채굴/건설한다.</summary>
        public TileService TileService => session?.TileService;

        /// <summary>WorldSessionController와 함께 생성된다. SealSystemDebugView 등이 이 인스턴스로 밀폐 상태를 조회/시각화한다.</summary>
        public SealSystem SealSystem => session?.SealSystem;

        /// <summary>개발 B가 제작/제련/AI 등 Tick(float deltaGameSeconds) 소비자를 등록·해제하는 진입점(A-04).</summary>
        public IGameSecondsTickDriver TickDriver => tickDriver;

        private void Start()
        {
            if (config == null)
            {
                Debug.LogError("[Nyangbingo] MapGeneratorTestHarness: WorldGenerationConfig가 비어 있습니다.");
                return;
            }

            // "개발 A 보완 작업 명세서" §5: session.TimeService/TickDriver가 StartNewWorld 완료(=WorldLoaded
            // 발행) 시점에 이미 채워져 있어야 하므로, 세션을 만들기 전에 DayNightService/CentralTickDriver를
            // 먼저 준비한다.
            SetupDayNightCycle();
            SetupCentralTickDriver();

            if (tilemapRenderer != null)
            {
                session = new WorldSessionController(config, tilemapRenderer, catalog);
                session.BindTimeService(dayNightService);
                session.BindTickDriver(tickDriver);
                session.WorldLoaded += HandleWorldLoaded; // §5 항목 7 검증용 — 실제 배선 예시는 §2 문서 참고.

                WorldGenerationResult result;
                try
                {
                    result = session.StartNewWorld(seed);
                }
                catch (System.InvalidOperationException ex)
                {
                    // A-08: 검증 실패 월드를 조용히 시작하지 않는다 — 세션을 만들지 않고 명확한 오류만 남긴다.
                    Debug.LogError($"[Nyangbingo] MapGeneratorTestHarness: 월드 생성 검증에 실패해 씬을 시작하지 않습니다. {ex.Message}");
                    session.WorldLoaded -= HandleWorldLoaded;
                    session = null;
                    return;
                }
                LogGenerationSummary(result);
                BuildChestMarkers(result);
                BindSealSystemToDebugViews();
                if (catalog == null)
                    Debug.LogWarning("[Nyangbingo] MapGeneratorTestHarness: catalog가 비어 있어 채굴/상자 보상 없이 파괴·설치만 동작합니다.");
                FrameCamera(result.width, result.height);
            }
            else
            {
                // Tilemap 없이도 맵 모양은 확인할 수 있어야 하므로, 세이브/상자 개봉 없이 텍스처 프리뷰만 그린다.
                var generator = new MapGenerator(config);
                var result = generator.GenerateDetailed(seed);
                LogGenerationSummary(result);
                BuildPreviewSprite(result);
                FrameCamera(result.width, result.height);
            }

            if (enableSaveLoadHotkeys) EnsureSaveManager();

            SetupDawnAutoSave();

            if (logLegend) LogLegend();
        }

        /// <summary>
        /// §5 항목 7 배선 예시: 개발 B 시스템은 이 이벤트가 발행된 시점부터 session.TileService/SealSystem/
        /// TimeService/TickDriver를 안전하게 조회할 수 있다(그 이전 시점의 참조는 아직 없거나 이전 월드의 것).
        /// </summary>
        private void HandleWorldLoaded()
        {
            Debug.Log("[Nyangbingo] WorldLoaded 발행 — session.TileService/SealSystem/TimeService/TickDriver가 " +
                      "모두 최신 라이브 상태를 가리킵니다.");
        }

        private void Update()
        {
            if (!enableSaveLoadHotkeys || session == null) return;

            if (Input.GetKeyDown(KeyCode.F5)) SaveNow();
            else if (Input.GetKeyDown(KeyCode.F9)) LoadNow();
        }

        private void OnDestroy()
        {
            // SealSystem/GameEvents는 정적 이벤트를 구독하므로 반드시 해제한다.
            if (session != null) session.WorldLoaded -= HandleWorldLoaded;
            session?.Dispose();
            GameEvents.OnDayStart -= HandleDayStart;
            GameEvents.OnNightStart -= HandleNightStart;
            GameEvents.OnDawnWarning -= HandleDawnWarning;

            if (tickDriver != null && tickProbe != null) tickDriver.Unregister(tickProbe);
        }

        /// <summary>
        /// 낮/밤 사이클 매니저를 씬에 준비하고(없으면 자동 부착), 다른 프로그래머가 참고할 수 있는 예시로
        /// GameEvents.OnDayStart/OnNightStart/OnDawnWarning를 직접 구독해 로그를 남긴다. 밤이 시작되면
        /// TileService.GetValidSpawnPositions 데모도 함께 실행해, AI 스폰 쿼리 API가 실제로 동작함을
        /// 시각적으로 확인할 수 있게 한다.
        /// </summary>
        private void SetupDayNightCycle()
        {
            if (dayNightService == null)
                dayNightService = GetComponent<DayNightService>() ?? gameObject.AddComponent<DayNightService>();

            GameEvents.OnDayStart += HandleDayStart;
            GameEvents.OnNightStart += HandleNightStart;
            GameEvents.OnDawnWarning += HandleDawnWarning;
        }

        private void EnsureSaveManager()
        {
            if (saveManager == null)
                saveManager = GetComponent<SaveManager>() ?? gameObject.AddComponent<SaveManager>();
        }

        /// <summary>
        /// A-04: 개발 B의 제작/제련/유틸리티/AI/전투 Tick 소비자가 연결할 공통 game seconds Tick 드라이버를
        /// 씬에 준비한다(없으면 자동 부착). 실제로 delta game seconds가 relay되는지 눈으로 확인할 수 있도록
        /// Dev A 자체 테스트 소비자(DevATickProbe)를 하나 등록해 누적값을 HUD/로그로 노출한다.
        /// </summary>
        private void SetupCentralTickDriver()
        {
            if (!enableCentralTickDriver) return;

            if (tickDriver == null)
                tickDriver = GetComponent<CentralTickDriver>() ?? gameObject.AddComponent<CentralTickDriver>();
            tickDriver.Configure(dayNightService);

            if (registerTickProbe)
            {
                tickProbe = new DevATickProbe();
                tickDriver.Register(tickProbe);
            }

            Debug.Log("[Nyangbingo] 중앙 game seconds Tick 드라이버 준비 완료 — IGameSecondsTickable 구현체를 " +
                      "harness.TickDriver.Register(...)로 등록하면 매 프레임 delta game seconds를 받습니다.");
        }

        /// <summary>
        /// DEV_B_TO_DEV_A_HANDOFF.md §11.4/§13 1단계 완료 기준("새벽 시... 자동 저장이 한 번만 실행")을
        /// 이 하네스 안에서 실제로 배선한다. WorldSessionController는 MonoBehaviour가 아니라 씬에 미리
        /// 인스펙터로 꽂아둘 수 없으므로, 세션이 만들어진 뒤 WorldSessionSaveProviderAdapter를 통해
        /// 코드로 연결한다 — F5/F9 수동 저장과 동일한 session/saveManager/saveSlot을 그대로 재사용해,
        /// 새벽 자동 저장과 수동 저장이 같은 슬롯을 놓고 서로 다른 상태를 남기지 않게 한다.
        /// </summary>
        private void SetupDawnAutoSave()
        {
            if (!enableDawnAutoSave || session == null) return;

            EnsureSaveManager();

            if (saveProviderAdapter == null)
                saveProviderAdapter = GetComponent<WorldSessionSaveProviderAdapter>() ?? gameObject.AddComponent<WorldSessionSaveProviderAdapter>();
            saveProviderAdapter.Configure(session);

            if (dawnAutoSave == null)
                dawnAutoSave = GetComponent<DawnAutoSave>() ?? gameObject.AddComponent<DawnAutoSave>();
            dawnAutoSave.Configure(saveManager, dayNightService, saveProviderAdapter, saveSlot);

            Debug.Log($"[Nyangbingo] 새벽 자동 저장 배선 완료 — 슬롯 {saveSlot}. 새벽(ITimeSource.Dawn)마다 자동 저장됩니다.");
        }

        private void HandleDayStart()
        {
            if (logDayNightTransitions)
                Debug.Log($"[Nyangbingo] 새벽 — Day {dayNightService.Day} 시작 (D-{dayNightService.DaysRemaining}).");
            ClearSpawnQueryMarkers();
        }

        private void HandleNightStart()
        {
            if (logDayNightTransitions)
                Debug.Log($"[Nyangbingo] 밤 시작 — Day {dayNightService.Day}.");
            if (showSpawnQueryDemoAtNight) ShowSpawnQueryDemo();
        }

        private void HandleDawnWarning()
        {
            if (logDayNightTransitions)
                Debug.Log($"[Nyangbingo] 새벽 경고 — 곧 Day {dayNightService.Day + 1}이 시작됩니다.");
        }

        /// <summary>
        /// TileService.GetValidSpawnPositions(§요괴 AI 스폰 쿼리)의 실제 동작 예시. 스폰 지점을 중심으로
        /// 유효 좌표를 조회해 보라색 마커로 표시한다 — 실제 AI 스포너는 이 좌표들 중 하나를 무작위로 골라
        /// 요괴를 배치하면 된다.
        /// </summary>
        private void ShowSpawnQueryDemo()
        {
            ClearSpawnQueryMarkers();
            if (session?.TileService == null) return;

            var spawn = session.LastResult.spawnPoint;
            var center = new Vector3Int(spawn.x, spawn.y, 0);
            var positions = session.TileService.GetValidSpawnPositions(center, spawnQueryMinRange, spawnQueryMaxRange);
            Debug.Log($"[Nyangbingo] 요괴 스폰 후보 {positions.Count}개 조회됨 (스폰 지점 기준 {spawnQueryMinRange}~{spawnQueryMaxRange} 범위).");

            var markerSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 2f);
            var shown = 0;
            foreach (var position in positions)
            {
                if (shown++ >= MaxSpawnQueryMarkers) break;
                var name = $"SpawnCandidate_{position.x}_{position.y}";
                var color = new Color(0.85f, 0.2f, 0.95f, 0.85f);
                spawnQueryMarkers.Add(PlaceMarker(name, new Vector2Int(position.x, position.y), color, markerSprite));
            }
        }

        private void ClearSpawnQueryMarkers()
        {
            foreach (var marker in spawnQueryMarkers) if (marker != null) Destroy(marker);
            spawnQueryMarkers.Clear();
        }

        /// <summary>
        /// 낮/밤 상태와 D-100 카운트다운을 화면에 바로 찍어주는 디버그 HUD. 배속 버튼으로 인스펙터를
        /// 열지 않고도 빠르게 밤/낮 전환을 재현해 이벤트 발행을 확인할 수 있다.
        /// </summary>
        private void OnGUI()
        {
            if (dayNightService == null) return;

            var style = new GUIStyle(GUI.skin.label) { fontSize = 16, normal = { textColor = Color.white } };
            var lines = new[]
            {
                $"D-{dayNightService.DaysRemaining} (Day {dayNightService.Day})",
                $"상태: {dayNightService.State} — 다음 전환까지 {dayNightService.SecondsUntilNextTransition:0.0}초",
                $"누적 GameSeconds: {dayNightService.GameSeconds:0.0}s",
                tickProbe != null
                    ? $"TickDriver 소비자 {tickDriver?.RegisteredCount ?? 0}명 — 프로브 누적 {tickProbe.AccumulatedGameSeconds:0.0}s ({tickProbe.TickCallCount}회)"
                    : "TickDriver 비활성"
            };

            GUI.Box(new Rect(10, 10, 340, 24 * lines.Length + 44), string.Empty);
            for (var i = 0; i < lines.Length; i++)
                GUI.Label(new Rect(20, 16 + i * 24, 320, 24), lines[i], style);

            var scaleRowY = 16 + lines.Length * 24 + 4;
            GUI.Label(new Rect(20, scaleRowY, 60, 24), $"{dayNightService.TimeScale:0.0}x", style);
            if (GUI.Button(new Rect(85, scaleRowY, 40, 24), "x1")) dayNightService.TimeScale = 1f;
            if (GUI.Button(new Rect(130, scaleRowY, 45, 24), "x10")) dayNightService.TimeScale = 10f;
            if (GUI.Button(new Rect(180, scaleRowY, 45, 24), "x50")) dayNightService.TimeScale = 50f;
            if (GUI.Button(new Rect(230, scaleRowY, 100, 24), dayNightService.TimeScale <= 0f ? "재생" : "일시정지"))
                dayNightService.TimeScale = dayNightService.TimeScale <= 0f ? 1f : 0f;
        }

        /// <summary>
        /// 상자 우클릭 등에서 사용할 개봉 진입점. PlayerMiningController가 이 메서드를 통해서만 상자를
        /// 열게 해서, 실제 보상 지급 규칙(§7.2)이 이 하네스 밖에서 재구현되지 않게 한다.
        /// </summary>
        public bool TryOpenChest(Vector3Int cell)
        {
            if (session == null) return false;

            if (!session.TryOpenChestAt(cell, out var chestId, out var definition))
            {
                Debug.Log($"[Nyangbingo] 상자 개봉 실패 {cell} — 해당 칸에 미개봉 상자가 없거나 카탈로그 미연결.");
                return false;
            }

            // ChestProgress.TryOpen이 이미 보상을 지급했다 — ChestRewardSelector는 순수 함수(부작용 없음)라
            // 로그용으로 같은 (seed, chestId, definition)을 다시 넣어도 실제로 지급된 것과 항상 같은 결과가 나온다.
            var itemSummary = definition.Rewards.Length == 0
                ? "없음"
                : string.Join(", ", System.Array.ConvertAll(definition.Rewards, reward => $"{reward.item?.Id}x{reward.amount}"));
            var equipment = ChestRewardSelector.SelectEquipment(session.Seed, chestId, definition);
            Debug.Log($"[Nyangbingo] 상자 개봉 성공 {chestId} — 아이템: {itemSummary}, 액세서리: {(equipment != null ? equipment.Id : "없음")}");
            RefreshChestMarkerColor(chestId);
            return true;
        }

        private void SaveNow()
        {
            var save = new SaveGame();
            if (session.CaptureSnapshot(save))
            {
                saveManager.Save(saveSlot, save);
                Debug.Log($"[Nyangbingo] 저장 완료 — 슬롯 {saveSlot}, seed {save.seed}, 타일 변경 {save.tileChanges.Count}건, " +
                          $"열린 상자 {save.openedChestIds.Count}/{save.chests.Count}개.");
            }
            else
            {
                Debug.LogError("[Nyangbingo] 저장 실패 — WorldSaveAdapter.CaptureWorld 검증에 실패했습니다.");
            }
        }

        private void LoadNow()
        {
            if (!saveManager.TryLoad(saveSlot, out var save))
            {
                Debug.LogWarning($"[Nyangbingo] 로드 실패 — 슬롯 {saveSlot}에 세이브 파일이 없거나 손상되었습니다.");
                return;
            }

            if (!session.LoadSnapshot(save))
            {
                Debug.LogError("[Nyangbingo] 로드 실패 — 월드 복원 검증에 실패해 이전 상태를 그대로 유지합니다.");
                return;
            }

            BindSealSystemToDebugViews();
            BuildChestMarkers(session.LastResult);
            Debug.Log($"[Nyangbingo] 로드 완료 — 슬롯 {saveSlot}, seed {save.seed}, 타일 변경 {save.tileChanges.Count}건 재생, " +
                      $"열린 상자 {save.openedChestIds.Count}/{save.chests.Count}개 복원.");
        }

        /// <summary>
        /// 씬에 있는 SealSystemDebugView 중 이 하네스를 참조하는 것들에게 방금 만든 SealSystem 인스턴스를
        /// 즉시 주입한다. SealSystemDebugView.Update()가 매 프레임 harness.SealSystem을 다시 조회하므로
        /// 없어도 다음 프레임이면 저절로 채워지지만, 초기화(및 로드로 인스턴스가 교체된) 첫 프레임부터
        /// 확실한 참조를 갖도록 하는 안전장치다.
        /// </summary>
        private void BindSealSystemToDebugViews()
        {
            foreach (var debugView in FindObjectsByType<SealSystemDebugView>())
            {
                if (debugView.Harness == this) debugView.BindSealSystem(SealSystem);
            }
        }

        private void LogGenerationSummary(WorldGenerationResult result)
        {
            Debug.Log($"[Nyangbingo] WorldGen 결과 — seed 요청:{result.requestedSeed} 확정:{result.acceptedSeed}, " +
                      $"리롤 {result.rerollAttempts}회, 검증 통과:{result.passedValidation}, 상자 {result.chests.Count}개, " +
                      $"스폰({result.spawnPoint.x},{result.spawnPoint.y}), 제단({result.altarPosition.x},{result.altarPosition.y})");

            if (result.chests.Count > config.TotalChestCount)
                Debug.LogWarning($"[Nyangbingo] 상자 개수가 상한({config.TotalChestCount})을 초과합니다: {result.chests.Count}개.");
            else
                Debug.Log($"[Nyangbingo] 상자 {result.chests.Count}개 (동굴당 0~{config.ChestPerCavernMax}, 상한 {config.TotalChestCount}).");
        }

        /// <summary>실제 Tilemap 위에는 색상 텍스처 대신, 스폰/제단/상자 위치만 작은 색점으로 얹어서 표시한다.
        /// 이미 개봉된 상자는 회색으로, 미개봉 상자는 노란색으로 구분해 로드 후 상태가 유지됐는지 한눈에 보이게 한다.</summary>
        private void BuildChestMarkers(WorldGenerationResult result)
        {
            foreach (var marker in chestMarkers) if (marker != null) Destroy(marker);
            chestMarkers.Clear();

            var markerSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 2f);

            foreach (var chest in result.chests)
            {
                var opened = session.ChestProgress.IsOpened(chest.id);
                var color = opened ? new Color(0.5f, 0.5f, 0.5f, 0.6f) : new Color(1f, 0.95f, 0.2f);
                chestMarkers.Add(PlaceMarker($"Chest_{chest.id}", chest.position, color, markerSprite));
            }

            PlaceMarker("Marker_Spawn", result.spawnPoint, new Color(0.2f, 1f, 0.3f), markerSprite);
            PlaceMarker("Marker_Altar", result.altarPosition, new Color(1f, 0.2f, 0.3f), markerSprite);
        }

        private void RefreshChestMarkerColor(string chestId)
        {
            var marker = transform.Find($"Chest_{chestId}");
            var spriteRenderer = marker != null ? marker.GetComponent<SpriteRenderer>() : null;
            if (spriteRenderer != null) spriteRenderer.color = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        }

        private GameObject PlaceMarker(string name, Vector2Int position, Color color, Sprite sprite)
        {
            var markerObject = new GameObject(name);
            markerObject.transform.SetParent(transform, false);
            markerObject.transform.position = new Vector3(position.x + 0.5f, position.y + 0.5f, -1f);
            var spriteRenderer = markerObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.color = color;
            spriteRenderer.sortingOrder = 10;
            return markerObject;
        }

        private void BuildPreviewSprite(WorldGenerationResult result)
        {
            var texture = new Texture2D(result.width, result.height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            for (var x = 0; x < result.width; x++)
            {
                for (var y = 0; y < result.height; y++)
                    texture.SetPixel(x, y, ColorFor(result.tiles[x, y]));
            }

            foreach (var chest in result.chests)
                PaintMarker(texture, chest.position, new Color(1f, 0.95f, 0.2f));

            PaintMarker(texture, result.spawnPoint, new Color(0.2f, 1f, 0.3f));
            PaintMarker(texture, result.altarPosition, new Color(1f, 0.2f, 0.3f));

            texture.Apply();

            var sprite = Sprite.Create(texture, new Rect(0f, 0f, result.width, result.height), new Vector2(0f, 0f), 1f);
            var previewObject = new GameObject("WorldPreview");
            previewObject.transform.SetParent(transform, false);
            var spriteRenderer = previewObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
        }

        private static void PaintMarker(Texture2D texture, Vector2Int center, Color color)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    var x = center.x + dx;
                    var y = center.y + dy;
                    if (x < 0 || x >= texture.width || y < 0 || y >= texture.height) continue;
                    texture.SetPixel(x, y, color);
                }
            }
        }

        private static void FrameCamera(int width, int height)
        {
            var targetCamera = Camera.main;
            if (targetCamera == null) return;
            targetCamera.orthographic = true;
            targetCamera.orthographicSize = height / 2f;
            targetCamera.transform.position = new Vector3(width / 2f, height / 2f, -10f);
        }

        private static Color ColorFor(TileData tile)
        {
            return tile.elementType switch
            {
                WorldTileTypes.Air => new Color(0.60f, 0.85f, 0.95f),
                WorldTileTypes.Dirt => new Color(0.55f, 0.35f, 0.17f),
                WorldTileTypes.Stone => new Color(0.50f, 0.50f, 0.52f),
                WorldTileTypes.Coal => new Color(0.08f, 0.08f, 0.08f),
                WorldTileTypes.Clay => new Color(0.72f, 0.45f, 0.20f),
                WorldTileTypes.StoneMid => new Color(0.40f, 0.42f, 0.48f),
                WorldTileTypes.IronOre => new Color(0.72f, 0.45f, 0.35f),
                WorldTileTypes.CopperOre => new Color(0.85f, 0.55f, 0.25f),
                WorldTileTypes.IceShard => new Color(0.60f, 0.90f, 0.95f),
                WorldTileTypes.StoneDeep => new Color(0.22f, 0.24f, 0.32f),
                WorldTileTypes.IceSteelOre => new Color(0.55f, 0.75f, 0.90f),
                WorldTileTypes.FrostEssence => new Color(0.85f, 0.95f, 1.00f),
                WorldTileTypes.Bedrock => new Color(0.05f, 0.05f, 0.08f),
                WorldTileTypes.RuinWall => new Color(0.55f, 0.40f, 0.60f),
                WorldTileTypes.IceLake => new Color(0.55f, 0.85f, 0.95f),
                WorldTileTypes.IceAltar => new Color(0.90f, 0.30f, 0.90f),
                WorldTileTypes.BackgroundDirt => new Color(0.35f, 0.22f, 0.12f),
                WorldTileTypes.BackgroundStone => new Color(0.25f, 0.25f, 0.27f),
                WorldTileTypes.BackgroundDeep => new Color(0.12f, 0.14f, 0.22f),
                _ => new Color(1f, 0f, 1f) // 매핑이 빠진 elementType은 마젠타로 즉시 눈에 띄게 표시한다.
            };
        }

        /// <summary>
        /// A-04 검증용 Dev A 자체 Tick 소비자. CentralTickDriver.Register(this)로 등록되어 매 프레임
        /// delta game seconds를 누적한다 — 누적값이 dayNightService.GameSeconds와 항상 같은 속도로
        /// 늘어나는지(=배속/정지가 정확히 반영되는지)를 HUD로 바로 확인할 수 있다.
        /// </summary>
        private sealed class DevATickProbe : Nyangbingo.Core.IGameSecondsTickable
        {
            public float AccumulatedGameSeconds { get; private set; }
            public int TickCallCount { get; private set; }

            public void Tick(float deltaGameSeconds)
            {
                AccumulatedGameSeconds += deltaGameSeconds;
                TickCallCount++;
            }
        }

        private static void LogLegend()
        {
            Debug.Log("[Nyangbingo] 범례 — 초록: 스폰(반지하 알코브), 빨강: 이무기 제단, 노랑: 미개봉 상자, 회색: 개봉된 상자, " +
                      "보라: 밤 시간대 요괴 스폰 후보(GetValidSpawnPositions 데모), " +
                      "하늘색: 개방 공중, 어두운 무채색: 동굴(배경벽), 갈색: 흙, 회색: 돌, 검정: 석탄, 주황: 점토, " +
                      "적갈색: 철광석, 금색: 구리광석, 청록: 얼음조각/서리류, 진남색: 심층암, 보라: 폐허, 마젠타: 미매핑 타일. " +
                      "F5: 저장, F9: 로드(세이브/상자 개봉 테스트). 좌상단 HUD: D-100/낮밤 상태/배속 조절.");
        }
    }
}
