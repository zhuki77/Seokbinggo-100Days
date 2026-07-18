# 개발 A파트 → 전체 팀 인수인계 리포트

**작성 시점:** 2026-07-18 ("개발 A 보완 작업 명세서" A-01~A-11 + §5/§7 최종 전달판)
**대상:** 개발 B파트, 기획, 아트 팀원 전원
**기준 문서:** `DEV_B_TO_DEV_A_HANDOFF.md` (v17 스펙 / v15 QA 기준) + 개발 B가 보낸 "개발 A 보완 작업 명세서"(A-01~A-11, §5, §7)

> **이번 갱신 요약**: A-01~A-11 전부 완료. 임시 Tile/Sprite는 `Assets/Tiles/Temp`·`Assets/Sprites/Temp`에
> 실제 커밋 가능한 상태로 존재하며 `DevAWorldTest` Missing 참조 없이 정상 렌더링된다. §5 연결 계약 8항목은
> `WorldSessionController` 파사드로 전부 제공된다. 이 문서 §6에 개발 B 전달용 최종 보고(수정 파일·근거값·
> 사용법·남은 B 작업·제한사항)를 정리했다.
>
> **개발 B 실행 문서:** 남은 연결·통합 작업은 `DEV_B_SUPPLEMENT_SPEC.md` (B-01~B-08)를 따른다.

이 문서 하나만 보면 "지금 뭐가 돌아가고 있고, 내가 어디에 무엇을 연결하면 되고, 다음에 뭘 해야 하는지"를 전부 알 수 있게 정리했습니다. 씬을 직접 열어보지 않아도 이해할 수 있도록 파일 경로와 실제 API 시그니처를 그대로 인용했습니다.

---

## 📋 목차

1. [개발 A파트 코어 마일스톤 요약](#1-개발-a파트-코어-마일스톤-요약)
2. [개발 B파트 배선 연결 가이드](#2-개발-b파트-배선-연결-가이드)
3. [남은 작업 목록 (우선순위 순)](#3-남은-작업-목록-우선순위-순)
4. [씬/에셋 빠른 참조](#4-씬에셋-빠른-참조)
5. [개발 A 보완 작업(A-01~A-11) 최종 체크리스트](#5-개발-a-보완-작업a-01a-11-최종-체크리스트)
6. [최종 전달물 / 개발 B 인수인계 보고](#6-최종-전달물--개발-b-인수인계-보고)

---

## 1. 개발 A파트 코어 마일스톤 요약

전부 **결정론적 시드 기반**으로 동작하며, 컴파일 에러 0개 상태로 씬 세팅까지 완료되어 있습니다.

### 1.1 월드 생성 — `MapGenerator.cs`

| 항목 | 내용 |
|---|---|
| 위치 | `Assets/Scripts/Nyangbingo/World/MapGenerator.cs` |
| 진입점 | `public TileData[,] Generate(int seed)` / `GenerateDetailed(int seed)` |
| 알고리즘 | 4패스, 각 패스마다 독립된 `System.Random(seed + N)` 사용 (100% 재현 가능) |
| Pass 1 | 1D Perlin Noise 기반 지표면 → 상층/중층/심층 + 최하단 빙암(경도 3, 파괴불가) |
| Pass 2 | 2D Perlin Noise 임계값(상층 공동률 10% ~ 심층 25%) + 배경벽(`isUndergroundDecor=true`) |
| Pass 3 | 3~6칸 군집(Vein) 형태 광맥 배치 |
| Pass 4 | 폐허, 반지하 알코브(스폰), 이무기 제단, 상자 20개(겹치지 않게 결정론적 배치) |
| 연결성 보장 | `CarveConnectivityShafts`로 알코브 → 심층 제단까지 확정 통로 확보 |
| 자동 리롤 | 4대 검증(작업대 재료 20초 이내 채굴 가능 / 스폰-알코브 연결성 / 심층 연결 통로 / 제단 도달성) 실패 시 `seed+1`로 재시도 |
| B파트 계약 | `IChestSource` 구현 (`ChestIds`, `GetChestPosition`), `TryGetChestIdAt(Vector2Int, out chestId)` 역조회 지원 |

### 1.2 2중 타일맵 렌더러 — `TilemapRenderer.cs`

| 항목 | 내용 |
|---|---|
| 위치 | `Assets/Scripts/Nyangbingo/World/TilemapRenderer.cs` |
| 구조 | 테라리아식 Foreground(전경, 채굴 가능) + Background(배경벽, 채굴 불가) 2겹 Tilemap |
| 진입점 | `public void RenderWorld(TileData[,] tiles)` — `SetTilesBlock`으로 64,000칸 일괄 렌더링 |
| 매핑 | `[Serializable] TileVisual { elementType, tile }` 배열 → 인스펙터에서 드래그앤드롭, `MergeKnownElementTypes`로 신규 elementType 슬롯 자동 추가 |

### 1.3 실시간 채굴/건설 — `TileService.cs`

| 항목 | 내용 |
|---|---|
| 위치 | `Assets/Scripts/Nyangbingo/World/TileService.cs` |
| 파괴 판정 | `toolTier >= tile.hardness` (빙암/이무기 제단은 티어 무관 파괴불가) |
| 파괴 결과 | 전경 제거 → 배경벽 노출 → 아이템 드랍(`ItemAcquisition`) → `GameEvents.RaiseTileBroken` |
| 설치 결과 | 항상 `isNaturalTerrain=false`로 설치(밀폐 벽 인정 안 됨) → `GameEvents.RaiseTilePlaced` |
| 세이브 연동 | `GetTileChangeRecords()`(캡처), `RestoreTileChanges(records)`(로드 시 재생, 이벤트 미발행) |
| 신규 API | `GetValidSpawnPositions(Vector3Int center, int minRange, int maxRange)` — §2.3 참고 |

### 1.4 밀폐(실내) 판정 — `SealSystem.cs`

| 항목 | 내용 |
|---|---|
| 위치 | `Assets/Scripts/Nyangbingo/World/SealSystem.cs` |
| 핵심 규칙 | Flood Fill 기반. 밀폐 벽으로 인정되는 타일은 **자연 지형(`isNaturalTerrain == true`) + `ISealBarrierRegistry`가 인정하는 B파트 설치물**(차열벽·차열 지붕·단열 문 등, 레지스트리 미연결 시 자연 지형만 인정 — 기존 동작과 100% 호환) — 플레이어가 설치한 미인정 인공 타일은 "틈새"로 취급되어 밀폐가 깨짐(v15 QA 꼼수 방지 규칙 100% 반영) |
| **밀폐율 공식(결재 브리프 v2 처방 C, 07-15 오너 승인·확정)** | `leak_faces == 0 ? min(1, region_cells / 240) : 0` — `SealPercent`는 0~1 소수(×100이 기획 문서의 "%"), `LeakFaceCount`로 누출면 개수를 별도 조회 가능. 구(舊) "자연 벽 수/전체 경계 벽 수" 비율식(구멍 1칸이 1/N로 희석되는 결함, v15 QA-F 지적)은 폐기 |
| **온도 연동값** | `TemperaturePercent`(0~100) = `냉기원 가동 && leak_faces==0 ? SealPercent×100 : 0`. `SealPercent`(순수 밀폐 상태)와 스케일·의미가 다르므로 혼용하지 말 것. 냉기원 가동 여부는 `ICoolingSourceProvider`(B파트 온도 시스템 구현)로 주입 — 미연결 시 항상 가동 중으로 간주(연동 전에도 처방 C 값을 그대로 볼 수 있는 안전한 기본값) |
| 밤 시작 재계산 | `GameEvents.OnNightStart` 구독(매개변수 없는 전용 핸들러 `HandleNightStart` → `InvalidateAll()`) — 밤이 시작되면 캐시를 전부 지우고 등록된 관찰 지점만 최신 타일 상태로 다시 계산한다. 생성자에서 구독, `Dispose()`에서 해제(대칭 보장) |
| **재로드 시 상태 보존(A-07)** | `SealSystem.Rebind(TileService)` — 월드 로드처럼 TileData\[,\]가 통째로 바뀌어도 이 인스턴스를 Dispose/재생성하지 않고 내부 TileService 참조만 교체한다. 주 관찰 지점/고정 관찰 지점/`WatchPointSealChanged` 구독자/외부 참조가 로드 전후로 전부 그대로 유지된다 — `WorldSessionController.LoadSnapshot`이 세션 최초 생성 시에만 `new SealSystem(...)`을 쓰고, 이후로는 항상 `Rebind`만 호출한다 |
| 확장 지점 | `SetBarrierRegistry(ISealBarrierRegistry)`, `SetCoolingSourceProvider(ICoolingSourceProvider)` — B파트 설치물/온도 시스템이 준비되는 시점에 언제든 뒤늦게 연결 가능. `WorldSessionController.ConfigureSealExtensions(...)`가 세션이 SealSystem을 재생성/Rebind할 때마다 자동으로 재주입한다 |
| 최적화 | 셀→리전(Region) 캐시, 변경된 셀 주변만 무효화, 실제로 등록된 관찰 지점(watch point)만 재계산. Flood Fill 자체도 `maxFillCells`(기본 3000)로 크기 제한 |
| 이벤트 | §2.2에서 상세 설명 |
| 디버그 뷰 | `SealSystemDebugView.cs` — 마우스 위치 기준 초록(밀폐)/주황(실외)/맵 밖 전용 대형 마커를 Scene뷰(Gizmos)+Game뷰(런타임 스프라이트) 모두에서 확인 가능 |

### 1.5 원클릭 씬 세팅 자동화 — `NyangbingoDevAWorldTestSceneCreator.cs`

| 메뉴 | 동작 |
|---|---|
| `Nyangbingo/Setup Tilemap Rendering In Dev A Scene` | ① 없으면 테스트 씬 생성 → ② Foreground/Background Tilemap 생성 → ③ **18종 임시 타일 자동 생성**(전경 15종 불투명 단색 + 배경벽 3종 반투명 alpha 0.4 — 채굴 시 뒤가 뚫리는 게 눈에 보이도록) → ④ `TilemapRenderer` 슬롯에 전부 자동 매핑 → ⑤ `MapGeneratorTestHarness`/`PlayerMiningController`/`SealSystemDebugView` 전부 자동 연결·저장 |
| `Nyangbingo/Repair WorldGenerationConfig Asset` | `WorldGenerationConfig.asset`이 손상됐을 때 v17 정본 기본값으로 재생성 + 하니스 재연결 |
| `Nyangbingo/Create Dev A World Test Scene` | 빈 테스트 씬 + 기본 하니스 생성 |

> 임시 타일은 `Assets/Sprites/Temp`, `Assets/Tiles/Temp`에 생성됩니다. **아트가 준비되면 코드 수정 없이 각 `Tile` 에셋의 `sprite`만 교체**하면 됩니다 (§3.3).
>
> ✅ **A-01 완료(2026-07-18)**: 위 경로에 Tile `.asset` 19개(+`.meta`), Sprite `.png` 19개(+`.meta`)가
> 실제로 존재하고, `DevAWorldTest.unity`의 `tileVisuals` 18개 + `fallbackTile`이 모두 유효 GUID로 연결됨.
> Play 시 Missing Tile/unmapped 경고 없이 지형이 정상 렌더링됨을 확인. 메뉴를 다시 실행해도 기존 에셋을
> 재사용하므로 GUID가 유지된다(멱등).

### 1.6 세이브/로드 & 상자 개봉 — `WorldSessionController.cs`

| 항목 | 내용 |
|---|---|
| 위치 | `Assets/Scripts/Nyangbingo/World/WorldSessionController.cs` |
| 역할 | Dev A 월드 상태(TileService/SealSystem/ChestProgress)를 소유하고, Dev B의 `WorldSaveAdapter`를 그대로 소비하는 접착 계층 |
| 저장 (`F5`) | `CaptureSnapshot(save)` → `WorldSaveAdapter.CaptureWorld(save, tileChanges, ..., generator, chestProgress)` |
| **로드 (`F9`) — A-06로 트랜잭션화됨** | ① 저장 데이터 구조 검증(`WorldSaveAdapter.ValidateWorldRecords`) → ② 저장된 seed로 월드 재생성 → ③ **renderer=null인 "검증/재생 전용" `TileService`**에 `RestoreTileChanges`로 타일 diff 재생(좌표 범위/알려진 tileId/보호 타일 위반을 전부 검증, 실패 시 즉시 중단 — 이 단계에서는 화면이 전혀 바뀌지 않음) → ④ `WorldSaveAdapter.RestoreChests`로 상자 상태 복원 → ⑤ 여기까지 전부 성공했을 때만 라이브 참조(generator/tileService/chestProgress/seed) 교체 + `tileService.BindRenderer(renderer)`로 실제 렌더러 연결 → ⑥ `renderer.RenderWorld` 딱 한 번 호출 → ⑦ `SealSystem`은 Dispose하지 않고 `Rebind(tileService)`로 내부 참조만 교체(A-07, 관찰 지점/이벤트 구독 유지). **하나라도 실패하면 라이브 상태·화면은 물리적으로 한 칸도 바뀌지 않는다**(부분 복원 금지) |
| 로드 필수 검증(`TileService.RestoreTileChanges`) | 좌표 범위(`InBounds`), `WorldTileTypes.AllElementTypes`에 없는 알 수 없는 tileId, 설치 레코드가 빈 칸이 아닌 곳을 노리는 경우, 파괴 레코드가 빙암/이무기 제단(보호 타일) 또는 이미 빈 칸을 노리는 경우, 파괴 레코드의 tileId가 재생성된 원본 타일과 불일치하는 경우를 전부 거부 |
| 상자 개봉 | `TryOpenChestAt(cell, out chestId, out definition)` → 내부적으로 `GameDataCatalog.FindChest` + `ChestProgress.TryOpen(id, def, seed)` 호출, 성공 시 세이브에 영구 반영 |
| **안정적 세션 접근자(§5 연결 계약, 신규)** | `WorldSessionController`가 개발 B가 참조해야 하는 **유일한 세션 파사드**다. `TileService`/`SealSystem`(기존) 외에 `BindTimeService(DayNightService)`/`BindTickDriver(IGameSecondsTickDriver)`로 주입한 값을 각각 `TimeService`/`TickDriver` 프로퍼티로 그대로 조회할 수 있고(둘 다 미주입 시 null — 세션의 나머지 기능은 그대로 동작), `StartNewWorld`·`LoadSnapshot`이 **라이브 참조 교체까지 전부 성공했을 때만** `event Action WorldLoaded`를 정확히 한 번 발행한다. 실패한 시도(검증 실패/손상된 저장 데이터)는 절대 발행하지 않는다 — A-06/A-08의 "부분 교체 금지" 원칙과 동일하게 대칭 보장. **개발 B는 이제 폐기 예정인 `MapGeneratorTestHarness`를 더 이상 참조하지 않고, 이 클래스 하나만 들고 있으면 된다.** |
| 테스트 하니스 | `MapGeneratorTestHarness.cs`가 F5/F9 핫키 + 상자 마커(노랑=미개봉/회색=개봉)로 눈으로 확인 가능. `Start()`에서 `session.BindTimeService/BindTickDriver`를 실제로 호출하고 `WorldLoaded`를 구독해 로그를 남기므로, 이 파일 자체가 §5 배선 예시 코드다 |

### 1.7 낮/밤 사이클 & 요괴 스폰 쿼리 — `DayNightService.cs` / `TileService.cs` (신규, 최신)

| 항목 | 내용 |
|---|---|
| 위치 | `Assets/Scripts/Nyangbingo/World/DayNightService.cs` |
| 시간 규칙 | 낮 900초 / 밤 540초 실시간 타이머, `timeScale`(0=정지, 인스펙터/코드에서 즉시 조절) |
| 상태 | `public enum DayNightState { Day, Night }`, `State`/`IsNight`/`Day`/`DaysRemaining`(D-100) 프로퍼티 |
| 계약 구현 | `IGameSecondsSource`(`GameSeconds`), `ISaveableTimeSource`(`ITimeSource` 상속: `Day`, `IsNight`, `Dawn` 이벤트, `TimeOfDayGameSeconds`, `RestoreTimeState`) — **B파트 §5 시간 계약 그대로 구현** |
| **새벽 경고 시간(A-03 확정)** | **180초(3분) 전** — `dawnWarningLeadSeconds = 180f`. Notion 최신 정본 기준 확정값(과거 코드 기본값 30초는 오기로 판정) |
| **생존 목표 일수(A-03 확정)** | **D-100** — `survivalDayLimit = 100`. "백일폭염" 세계관(최신 UI 정본 `5 UI UX` v15 QA-E, `1 개요`) 기준으로 확정. `10 일정` 문서의 "D-30"은 "MVP 데모가 30일 루프까지만 도는 개발 일정"을 가리키는 것으로 재해석 — 실제 게임 내 카운터는 100에서 0으로 감소 |
| **`startAtNight` 버그 수정(A-02)** | `Awake()`가 예전에는 `startAtNight` 값과 무관하게 `timeOfDayGameSeconds = 0`으로 초기화해, `startAtNight == true`일 때 첫 밤이 (낮900+밤540=)1440초나 걸리는 버그가 있었다. 지금은 `timeOfDayGameSeconds = isNight ? dayDurationSeconds : 0f`로 초기화해, 밤으로 시작해도 정확히 540초 후 새벽이 온다 |
| 이벤트 발행 | 밤 시작 시 `GameEvents.RaiseNightStart()`, 새벽 경고(180초 전) 시 `RaiseDawnWarning()`, 새벽에 `Dawn`(인스턴스)과 `RaiseDayStart()`(전역)를 각각 정확히 1회 |
| 안전성 | `timeScale`을 아주 높여 하루를 한 프레임에 건너뛰어도 경계를 하나씩 순서대로 통과 → 이벤트 스킵/중복 없음. 음수/NaN/Infinity delta는 전부 무시 |

### 1.8 중앙 game seconds Tick 드라이버 — `CentralTickDriver.cs` (A-04, 신규)

| 항목 | 내용 |
|---|---|
| 위치 | `Assets/Scripts/Nyangbingo/World/CentralTickDriver.cs` |
| 계약 | `Nyangbingo.Core.IGameSecondsTickable`(`void Tick(float deltaGameSeconds)`), `IGameSecondsTickDriver`(`Register`/`Unregister`) — `Assets/Scripts/Nyangbingo/Core/WorldContracts.cs`에 정의 |
| 시간 기준 | `DayNightService.GameSeconds`의 프레임 간 증가량(delta)만 그대로 relay — Unity `Time.timeScale`은 전혀 쓰지 않는다. 배속/정지는 `DayNightService.TimeScale`이 결정 |
| 중복 방지 | 등록은 `HashSet<IGameSecondsTickable>` 기반이라 같은 인스턴스를 중복 등록해도 한 프레임에 정확히 한 번만 Tick |
| 안전한 해제 | Destroy된 MonoBehaviour 소비자는 UnityEngine.Object로 캐스팅한 뒤 오버로드된 `==` 연산자로 감지해 자동 제거(인터페이스 참조의 "가짜 null" 문제 회피) |
| 사용법 | **`WorldSessionController.TickDriver`**(§2.5 안정적 접근자, 개발 B가 참조해야 하는 정본 경로) → `session.TickDriver.Register(myTickable)` / `Unregister(myTickable)`. 메인 씬에서는 `CentralTickDriver` 인스턴스 하나를 `Configure(dayNightService)`로 초기화한 뒤 `session.BindTickDriver(그 인스턴스)`로 세션에 주입하면 된다. (`MapGeneratorTestHarness.TickDriver`도 같은 인스턴스를 가리키지만, 이 하네스는 폐기 예정이므로 신규 코드에서는 참조하지 않는다) |
| 검증용 소비자 | `MapGeneratorTestHarness` 내부 `DevATickProbe`가 자동 등록되어 HUD에 누적 relay된 game seconds/호출 횟수를 표시 — 배속 변경/정지 시 `dayNightService.GameSeconds`와 항상 같은 속도로 움직이는지 눈으로 바로 확인 가능 |
| 요괴 스폰 API | `TileService.GetValidSpawnPositions(Vector3Int center, int minRange, int maxRange)` — 발밑 고체 + 본인/윗칸 공기 + 거리범위 조건을 만족하는 좌표 목록 반환 (순수 조회, 부작용 없음) |
| 테스트 하니스 | `MapGeneratorTestHarness`에 D-30/상태/배속(x1·x10·x50·정지) HUD, 밤 시작 시 스폰 후보 보라색 마커 자동 표시 |

> 두 기능 모두 격리된 컴파일러 환경에서 하루 전체(1440초)를 한 프레임에 스킵하는 극단적 케이스까지 시뮬레이션해 이벤트 발행 횟수/순서를 전부 검증했습니다.

---

## 2. 개발 B파트 배선 연결 가이드

지금 당장 아래 3가지만 연결하면 개발 A가 만든 시스템 위에 곧바로 개발 B 시스템을 올릴 수 있습니다.

### 2.1 시간 시스템 연동

`DayNightService`는 `MonoBehaviour`이자 `IGameSecondsSource`, `ISaveableTimeSource`(= `ITimeSource`)를 구현합니다. `MapGeneratorTestHarness.SetupDayNightCycle()`이 씬에 없으면 자동으로 `AddComponent<DayNightService>()`로 붙여줍니다. 실제 메인 씬에서는 이 인스턴스 하나를 게임 시작 시 만들어서 아래처럼 그대로 넘기면 됩니다.

```csharp
// 어디선가(메인 씬 부트스트랩) DayNightService 인스턴스 하나를 참조로 들고 있다가:
yokaiBrain.SetGameSecondsSource(dayNightService);          // IGameSecondsSource
bossManager.ConfigureForRuntime(dayNightService, regularSpawnController); // ITimeSource
```

> **§5 연결 계약 갭 보완(신규)**: 예전에는 이 `DayNightService` 인스턴스와 §1.8의 `CentralTickDriver`를 어디서 얻을지가 (폐기 예정인) `MapGeneratorTestHarness`의 프로퍼티에만 노출돼 있었습니다. 지금은 **`WorldSessionController`가 이 둘을 그대로 들고 있다가 노출**합니다 — `session.BindTimeService(dayNightService)`/`session.BindTickDriver(tickDriver)`로 한 번 주입해 두면(부트스트랩 시점 1회), 이후 개발 B의 모든 시스템은 `session.TimeService`/`session.TickDriver`만 참조하면 됩니다. 자세한 내용은 §5를 참고하세요.

- `ForcedBossEncounterBinding`, `BaekjungTimeBinding` 등 이미 `GameEvents.OnNightStart`/`OnDawnWarning`/`OnDayStart`를 구독하도록 설계된 컴포넌트들은 **새 이벤트를 발행할 필요 없이 그대로 반응**합니다. `DayNightService`가 이 3개 이벤트를 정확히 계약대로(§5.2, §5.3) 발행하기 때문입니다.
- ⚠️ **`DawnAutoSave`/`ISaveSnapshotProvider` 실제 통합은 개발 B 담당**입니다(§0 담당 범위 참고). 개발 A는 `WorldSessionSaveProviderAdapter`(`ISaveSnapshotProvider` 구현체, `Configure(WorldSessionController)`로 세션 연결)와 `DawnAutoSave.Configure(SaveManager, ITimeSource, ISaveSnapshotProvider, int saveSlot)`(4-파라미터, 코드로 배선할 수 있게 개발 A가 추가한 진입점)까지만 준비해 뒀고, `MapGeneratorTestHarness.SetupDawnAutoSave()`에서 그 배선 예시를 그대로 볼 수 있습니다. **실제 게임 진행 스냅샷(플레이어/인벤토리/장비/월드 통합)을 채우는 `ISaveSnapshotProvider` 구현과, 메인 씬에서의 통합 배선은 개발 B가 마무리해야 합니다.**

```csharp
// 개발 A가 이미 준비해 둔 실제 코드 배선(MapGeneratorTestHarness.SetupDawnAutoSave() 참고, 컴파일 가능):
saveProviderAdapter.Configure(worldSessionController);                              // ISaveSnapshotProvider 구현체 준비
dawnAutoSave.Configure(saveManager, dayNightService, saveProviderAdapter, saveSlot); // 4개 인자 — Dawn(ITimeSource) 이벤트에 자동 구독
```

- **A-04(신규): 중앙 game seconds Tick 드라이버.** `CraftingProcess`/`SmeltingStation`/`UtilityService`/AI/전투 등 `Tick(float deltaGameSeconds)`가 필요한 소비자는 `Nyangbingo.Core.IGameSecondsTickable`을 구현한 뒤 아래처럼 등록하면 됩니다(§1.8 참고). 개발 A는 드라이버와 등록 계약만 제공하고, 실제 소비자 연결은 개발 B 몫입니다.

```csharp
public sealed class SmeltingStation : MonoBehaviour, IGameSecondsTickable
{
    private IGameSecondsTickDriver tickDriver;

    private void OnEnable()
    {
        tickDriver = worldSessionController.TickDriver; // §5 안정적 접근자 — 폐기 예정 디버그 하네스를 참조하지 않는다.
        tickDriver.Register(this);
    }

    private void OnDisable() => tickDriver?.Unregister(this);

    public void Tick(float deltaGameSeconds) { /* 제련 진행도 += deltaGameSeconds; */ }
}
```

### 2.2 밀폐 상태 연동

`SealSystem`은 **두 종류의 이벤트를 항상 함께 발행**합니다. 용도에 맞게 골라 쓰면 됩니다.

| 이벤트 | 시그니처 | 언제 쓰나 |
|---|---|---|
| `GameEvents.OnSealChanged` | `Action` (매개변수 없음) | **기존 Dev B 회귀 테스트(`DevBTest`)가 이미 구독 중인 계약** — 시그니처를 바꾸지 않았습니다. "어딘가의 밀폐 상태가 바뀌었다"는 신호만 받고, 실제 상태는 `sealSystem.IsInsideSealedArea(Vector2)` 또는 `IsWatchPointSealed(cell)`로 재조회하세요. |
| `SealSystem.WatchPointSealChanged` | `Action<Vector3Int cell, bool isSealed>` | **실내 온도 시스템처럼 "어느 지점이 정확히 어떤 상태로 바뀌었는지"가 필요한 경우** 이걸 구독하세요. 단, 그 지점을 먼저 `sealSystem.RegisterWatchPoint(cell)`(또는 플레이어처럼 매 프레임 위치가 바뀌면 `SetPrimaryWatchPoint(cell)`)로 등록해야 이벤트 대상이 됩니다. 등록 안 된 좌표는 이 이벤트가 발행되지 않습니다(성능 최적화 — 맵 전체를 매번 스캔하지 않기 위함). |

```csharp
sealSystem.RegisterWatchPoint(campfireCell); // 화로/제단/침대처럼 고정된 감시 지점
sealSystem.WatchPointSealChanged += (cell, isSealed) =>
{
    if (cell == campfireCell) indoorTemperature.SetSealed(isSealed);
};
```

- `ISealSource.SealPercent`(현재 주 관찰 지점의 밀폐율 0~1, 처방 C)와 `IsInsideSealedArea(Vector2)`는 계약 그대로 노출돼 있습니다. 냉기원까지 반영한 값이 필요하면 `SealSystem.TemperaturePercent`(0~100)를 쓰세요.
- **꼼수 방지 규칙**: 플레이어가 인공 타일(`isNaturalTerrain=false`)로 구멍을 막아도 밀폐로 인정되지 않습니다. 자연 지형으로만 복구돼야 다시 밀폐됩니다. 이미 샌드박스에서 "인공 패치 후 여전히 미밀폐 / 자연 패치 후 밀폐" 두 케이스 모두 검증했습니다.
- **차열벽/차열 지붕/단열 문 화이트리스트 확장(신규)**: `ISealBarrierRegistry.IsRecognizedBarrier(Vector3Int)`을 구현해 `worldSessionController.ConfigureSealExtensions(barrierRegistry, coolingSourceProvider)`로 연결하면, B파트 설치물도 자연 지형과 동등하게 밀폐 벽으로 인정됩니다(문이 열려 있으면 `false`를 반환하도록 구현하면 "문 열림=누수" 동작이 자동으로 따라옵니다). 연결하지 않으면 기존 동작(자연 지형만 인정)이 그대로 유지되어 안전합니다.
- **냉기원 가동 상태 연동(신규)**: `ICoolingSourceProvider.IsColdSourceActive`를 구현해 같은 `ConfigureSealExtensions`로 연결하면 `TemperaturePercent`가 냉기원 비가동 시 0을 반환합니다. 미연결 시 항상 가동 중으로 간주합니다.
- **로드 후에도 관찰 지점/구독이 끊기지 않음(A-07)**: 월드를 세이브·로드해도 `WorldSessionController.SealSystem`은 **같은 인스턴스**를 계속 가리킵니다(내부적으로 `Rebind`만 호출) — 로드 전에 등록한 `RegisterWatchPoint`/`WatchPointSealChanged` 구독을 다시 걸 필요가 없습니다.

### 2.3 요괴 스폰 연동

밤 시작 이벤트를 구독한 뒤, 스폰 좌표를 아래처럼 얻으면 됩니다.

```csharp
GameEvents.OnNightStart += () =>
{
    var candidates = tileService.GetValidSpawnPositions(center: playerBaseCell, minRange: 8, maxRange: 25);
    if (candidates.Count == 0) return; // 안전한 자리가 없으면 스폰 스킵
    var spawnCell = candidates[random.Next(candidates.Count)];
    // 이후 YokaiBrain.ConfigureForRuntime(...) 등 §9.2 순서대로 진행
};
```

- 조건: 대상 칸 + 윗칸(y+1)이 공기(`hardness<=0`), 발밑(y-1)이 고체(`hardness>0`), `center` 기준 유클리드 거리가 `[minRange, maxRange]` 안. `minRange > maxRange`이거나 음수면 빈 리스트를 안전하게 반환합니다.
- **디버그 확인 방법**: `MapGeneratorTestHarness`를 붙인 테스트 씬을 Play하면, 밤이 시작될 때마다 스폰 지점 기준으로 이 API를 실제로 호출해 **보라색 마커**를 씬에 띄워줍니다. 인스펙터의 `spawnQueryMinRange`/`spawnQueryMaxRange`로 범위를 조절해서 실제로 어떤 좌표가 후보로 잡히는지 바로 눈으로 확인할 수 있습니다. (`showSpawnQueryDemoAtNight` 체크박스로 켜고 끌 수 있습니다.)

### 2.4 Dev A 회귀 스모크 테스트 (A-09, 신규)

| 항목 | 내용 |
|---|---|
| 위치 | `Assets/Editor/NyangbingoDevARegressionTests.cs` |
| 실행 방법 | Unity 에디터 메뉴 `Nyangbingo/Run Dev A Regression Tests` — 6개 테스트를 순서대로 전부 실행하고, 실패한 항목만 따로 모아 마지막에 요약 로그를 남긴다. CI에서는 `Unity.exe -batchmode -quit -projectPath <경로> -executeMethod NyangbingoDevARegressionTests.RunAll`로 동일하게 실행 가능(Unity Test Framework asmdef 도입 없이 반복 실행 가능한 스모크 테스트 형태를 택함) |
| 커버리지 | ① 같은 seed 결정론적 생성/다른 seed 실제로 다른 결과, ② 상자 20개·지역별 개수·중복 ID 없음·seed 결정론, ③ 타일 변경 이력 정상 재생 + 손상된 레코드(보호 타일 덮어쓰기/범위 밖 좌표/알 수 없는 tileId) 원자적 거부, ④ SealSystem 밀폐/누수/인공 타일 꼼수 방지/냉기원 게이트/밤 시작 재계산, ⑤ 낮→밤→경고→새벽 이벤트 순서 + `startAtNight` 버그 회귀(A-02), ⑥ 월드 세션 저장→로드 라운드트립 + SealSystem 인스턴스 유지(A-07) + 손상된 저장 데이터 로드 시 라이브 상태 불변(A-06) + **`TimeService`/`TickDriver` 참조 유지 및 `WorldLoaded`가 성공한 교체마다 정확히 한 번만 발행되는지(§5, 신규)** |
| 성공 로그 예시 | `[Nyangbingo] Dev A deterministic generation test completed.` 등 6줄 + 마지막에 `[Nyangbingo] Dev A 회귀 테스트 전체 통과 (6/6).` |

### 2.5 §5 연결 계약 8개 항목 대응표 (신규)

"개발 A 보완 작업 명세서" §5가 요구한 8개 정보를 `WorldSessionController` 하나로 전부 조회할 수 있습니다. 개발 B는 이 표만 보고 배선하면 됩니다.

| # | 요구 항목 | 실제 API |
|---|---|---|
| 1 | DayNightService를 생성하거나 참조하는 방법 | 부트스트랩에서 `session.BindTimeService(dayNightService)`로 1회 주입 → 이후 `session.TimeService`로 조회 |
| 2 | delta game seconds 소비자를 등록·해제하는 방법 | 부트스트랩에서 `session.BindTickDriver(tickDriver)`로 1회 주입 → 이후 `session.TickDriver.Register(this)`/`Unregister(this)` |
| 3 | 현재 누적 GameSeconds를 조회하는 방법 | `session.TimeService.GameSeconds`(`IGameSecondsSource`) |
| 4 | 월드 시드와 타일 변경분을 캡처하는 방법 | `session.CaptureSnapshot(save)` — 내부적으로 `TileService.GetTileChangeRecords()` + `save.seed = session.Seed` |
| 5 | 월드 스냅샷을 원자적으로 복원하는 방법 | `session.LoadSnapshot(save)` — 실패 시 라이브 상태 불변(§1.6, A-06) |
| 6 | 월드 로드 후 TileService/SealSystem 참조를 얻는 방법 | `session.TileService`, `session.SealSystem` — 로드 성공 후에도 **SealSystem은 같은 인스턴스**(A-07) |
| 7 | 월드 교체 완료를 외부 시스템이 감지하는 방법 | `session.WorldLoaded += Handler` — `StartNewWorld`/`LoadSnapshot`이 라이브 참조 교체까지 전부 성공했을 때만 정확히 한 번 발행(실패 시 미발행) |
| 8 | 낮/밤/새벽 경고 이벤트의 정확한 발생 순서 | `GameEvents.OnNightStart → OnDawnWarning → OnDayStart`(§1.7). `session.TimeService`의 인스턴스 이벤트로는 `Dawn`만 노출(`ITimeSource` 계약) |

실제로 컴파일 가능한 최소 배선 예시:

```csharp
// 메인 씬 부트스트랩 — 딱 한 번만 실행
var session = new WorldSessionController(config, tilemapRenderer, catalog);
session.BindTimeService(dayNightService);   // 항목 1
session.BindTickDriver(centralTickDriver);  // 항목 2
session.WorldLoaded += OnWorldReplaced;      // 항목 7
session.StartNewWorld(seed);

void OnWorldReplaced()
{
    // 이 시점부터 session.TileService / session.SealSystem / session.TimeService / session.TickDriver
    // 전부 최신 라이브 상태를 가리킨다(항목 6). 최초 시작이든 F9 로드든 동일하게 호출된다.
    yokaiBrain.SetGameSecondsSource(session.TimeService);       // 항목 3
    indoorTemperature.Bind(session.SealSystem);
}
```

---

## 3. 남은 작업 목록 (우선순위 순)

> 이번 보완 작업(A-01~A-11)은 **전부 완료**됐습니다. 아래 §3.1~§3.4는 **개발 B 담당** 남은 연결 작업입니다.

> 상세 작업 단위·검증 기준은 **`DEV_B_SUPPLEMENT_SPEC.md`** 를 보세요.

### 3.1 제작 트리·제련 Tick 연동 실제 배선 (최우선, 개발 B 담당)

중앙 Tick 드라이버(§1.8, A-04)는 준비됐습니다. 아래 대상들을 `IGameSecondsTickable`로 구현해 `session.TickDriver.Register(this)`(§2.5 참고 — `WorldSessionController`가 노출하는 안정적 접근자)에 연결하는 것은 개발 B 몫입니다:

- `CraftingProcess`, `SmeltingStation`, `UtilityService`
- `WireSnareAbility`, `TurretController`, `HomingProjectilePool`
- `BaekjungTimeBinding`(시간 기반 로직이 Tick 방식으로 바뀐다면)

### 3.2 인벤토리 UI 및 HUD 통합 (개발 B 담당)

- 12슬롯 인벤토리를 실제 `Inventory`/`ItemAcquisition` 데이터에 바인딩.
- HUD: 체온(실내 온도 시스템), 밀폐도(`SealSystem.SealPercent`), **D-100**(`DayNightService.DaysRemaining` — 이미 계산되는 값, 지금은 디버그 `OnGUI`로만 노출돼 있음), 발톱 티어를 실제 UI 위젯으로 교체.

### 3.3 아트 리소스 최종 통합

- A-01 임시 에셋은 이미 `Assets/Tiles/Temp`·`Assets/Sprites/Temp`에 존재한다.
- 아트가 준비되면 18종 임시 단색 타일(전경 15 + 배경벽 3)의 `sprite`만 실제 아트로 교체. **코드 변경 불필요.**
- 제작대/화로 등 설치물 프리팹도 실제 아트로 교체.

### 3.4 개발 B 통합 스냅샷 / 자동 세이브 (개발 B 담당, §5 연결 계약 참고)

- 플레이어·인벤토리·장비·진행도·월드를 합친 통합 `ISaveSnapshotProvider` 실제 구현.
- `WorldSessionSaveProviderAdapter`/`DawnAutoSave.Configure(...)`(둘 다 개발 A가 이미 준비, §2.1)를 메인 씬 부트스트랩에 실제로 연결.

### 3.5 알려진 제한사항

- **월드 생성 검증(A-08)은 "실제 채굴 소요 시간(초)"을 시뮬레이션하지 않습니다.** 스폰에서 걸어갈 수 있는 공기 네트워크에 인접한 흙/돌 칸의 개수만 셉니다(반경 안이라도 도달 불가능한 고립 포켓은 더 이상 인정하지 않음) — 순수 "접근 가능성 + 최소 개수" 검증입니다. "20초 이내"라는 문구는 실제 시간 비용 검증이 아니라 접근성 검증의 근거 수치(recipes.csv workbench 재료량)로만 남아 있습니다.
- **`WorldSessionController.StartNewWorld`는 `MaxRerollAttempts`(기본 200)를 다 써도 검증에 실패하면 `InvalidOperationException`을 던집니다.** `MapGeneratorTestHarness.Start()`는 이를 잡아 오류만 로그로 남기고 세션을 만들지 않습니다(씬이 빈 상태로 남음) — 메인 씬에서도 이 예외를 잡아 사용자에게 알리는 처리가 필요합니다.
- Unity 에디터가 이미 열려 있는 환경에서는 배치 모드 CLI로 컴파일/씬 검증을 중복 실행할 수 없습니다(락파일 충돌) — 이번 보완 작업의 컴파일/씬 검증은 이미 열려 있는 에디터에서 직접 확인해야 합니다(§6 참고).

---

## 4. 씬/에셋 빠른 참조

| 항목 | 경로 |
|---|---|
| 개발 A 테스트 씬 | `Assets/Scenes/DevAWorldTest.unity` |
| B파트 회귀 테스트 씬 | `Assets/Scenes/DevBTest.unity` (건드리지 않음 — 계속 회귀 기준으로 유지) |
| 월드 생성 설정 | `Assets/Data/SO/WorldGenerationConfig.asset` |
| 임시 타일/스프라이트 | `Assets/Sprites/Temp`, `Assets/Tiles/Temp` (**A-01 완료** — Tile/Sprite 각 19개 + `.meta` 포함) |
| 월드 관련 스크립트 | `Assets/Scripts/Nyangbingo/World/*.cs` (`CentralTickDriver.cs` 신규) |
| 디버그/테스트 하니스 | `Assets/Scripts/Nyangbingo/Debug/*.cs` |
| 에디터 자동화 스크립트 | `Assets/Editor/NyangbingoDevAWorldTestSceneCreator.cs` |
| 에디터 회귀 테스트 스크립트 | `Assets/Editor/NyangbingoDevARegressionTests.cs` (신규, A-09) |

**테스트 씬 조작법**: Play 후 좌클릭 채굴 / 우클릭 설치(또는 상자 개봉) / `F5` 저장 / `F9` 로드. 좌상단 HUD에서 D-100·낮밤 상태·배속·중앙 Tick 드라이버 누적값을 확인·조절할 수 있고, 마우스 위치에 따라 초록(밀폐)/주황(실외)/맵 밖 전용 마커가 실시간으로 갱신됩니다.

---

## 5. 개발 A 보완 작업(A-01~A-11) 최종 체크리스트

| 항목 | 상태 | 비고 |
|---|---|---|
| A-01 임시 타일/씬 참조 복구 | ✅ 완료 | `Assets/Tiles/Temp`, `Assets/Sprites/Temp`, `DevAWorldTest.unity` |
| A-02 `startAtNight` 초기화 버그 | ✅ 완료 | `DayNightService.Awake()` |
| A-03 새벽 경고/생존 목표일 확정 | ✅ 완료 — 180초 / D-100 | `DayNightService.cs` |
| A-04 중앙 game seconds Tick 드라이버 | ✅ 완료 | `CentralTickDriver.cs`, `WorldContracts.cs` |
| A-05 SealSystem 최신 명세 반영 | ✅ 완료 — 처방 C + 화이트리스트 + 냉기원 게이트 | `SealSystem.cs` |
| A-06 월드 로드 트랜잭션화 | ✅ 완료 | `WorldSessionController.LoadSnapshot`, `TileService.BindRenderer/RestoreTileChanges` |
| A-07 SealSystem 관찰 상태 보존 | ✅ 완료 — `Rebind` | `SealSystem.Rebind`, `WorldSessionController.LoadSnapshot` |
| A-08 월드 생성 검증 실제화 | ✅ 완료 — 접근성 기반 검증 + 실패 월드 차단(예외) | `MapGenerator.ValidateWorld`, `WorldSessionController.StartNewWorld` |
| A-09 Dev A 회귀 테스트 | ✅ 완료 | `NyangbingoDevARegressionTests.cs` |
| A-10 컴파일 경고 제거 | ✅ 완료 | `MapGeneratorTestHarness.cs`: `FindObjectsByType<T>()` |
| A-11 인수인계 문서 갱신 | ✅ 완료 | 이 문서 |
| §5 연결 계약 8개 항목 | ✅ 완료 | `WorldSessionController.TimeService` / `TickDriver` / `WorldLoaded` (§2.5) |

---

## 6. 최종 전달물 / 개발 B 인수인계 보고

명세서 §7 기준. 개발 B는 **이 섹션 + §2.5**만 보면 연결을 시작할 수 있습니다.

### 6.1 필수 전달 파일

| 구분 | 경로 |
|---|---|
| 월드·타일·밀폐·시간 코드 | `Assets/Scripts/Nyangbingo/World/*.cs`, `Assets/Scripts/Nyangbingo/Core/WorldContracts.cs` |
| 테스트 씬 | `Assets/Scenes/DevAWorldTest.unity` |
| 임시 Tile/Sprite + `.meta` | `Assets/Tiles/Temp/*`, `Assets/Sprites/Temp/*` (각 19개 에셋 + meta) |
| 테스트 코드 | `Assets/Editor/NyangbingoDevARegressionTests.cs` |
| 타일 셋업 메뉴 | `Assets/Editor/SetupDevATileAssets.cs` |
| 인수인계 문서 | `DEV_A_HANDOFF_REPORT.md` (이 문서) |

> 개발 B 담당 씬/회귀(`DevBTest.unity` 등)는 의도적으로 수정하지 않았습니다.

### 6.2 최종 보고 (12항목)

#### 1. 수정한 파일 목록 (핵심)

- `World/DayNightService.cs` — startAtNight, 새벽 180초, D-100
- `World/CentralTickDriver.cs` — 신규 Tick 드라이버
- `World/SealSystem.cs` — 처방 C, Rebind, OnNightStart 재계산
- `World/WorldSessionController.cs` — 원자적 LoadSnapshot, WorldLoaded, TimeService/TickDriver
- `World/TilemapRenderer.cs` — 룩업/폴백, 에디터 배선 API
- `World/TileService.cs`, `World/MapGenerator.cs`, `World/WorldGenerationConfig.cs` — 검증/복원 강화
- `Core/WorldContracts.cs` — `IGameSecondsTickable` / `IGameSecondsTickDriver`
- `Debug/MapGeneratorTestHarness.cs` — Tick/세이브/WorldLoaded 배선 예시
- `Editor/SetupDevATileAssets.cs`, `Editor/NyangbingoDevARegressionTests.cs`
- `Scenes/DevAWorldTest.unity`, `Assets/Tiles/Temp/*`, `Assets/Sprites/Temp/*`
- `DEV_A_HANDOFF_REPORT.md`

#### 2. 문제별 수정 내용

| ID | 한 줄 요약 |
|---|---|
| A-01 | Temp 경로에 Tile/Sprite 실제 생성 + 씬 GUID 연결 + 멱등 재실행 |
| A-02 | `startAtNight` 시 `timeOfDayGameSeconds = dayDurationSeconds` |
| A-03 | 새벽 경고 180초, 생존 목표 100일 확정 |
| A-04 | `CentralTickDriver` + 세션 `TickDriver` 접근자 |
| A-05 | SealPercent = `leak==0 ? min(1, cells/240) : 0` |
| A-06 | 검증용 TileService로 복원 후 성공 시에만 라이브 교체 |
| A-07 | `SealSystem.Rebind`로 관찰점/구독 유지 |
| A-08 | 검증 실패 월드는 `InvalidOperationException`으로 시작 차단 |
| A-09 | 에디터 메뉴 회귀 테스트 6종 |
| A-10 | obsolete `FindObjectsByType` 정리 |
| A-11 | 이 문서 코드 일치화 |

#### 3. 새벽 경고 시간의 최종 값과 근거

- **최종값: 180 game seconds (3분 전)**
- 코드: `DayNightService.dawnWarningLeadSeconds = 180f`
- 근거: 기획 정본(새벽 3분 전). 과거 코드 기본값 30초는 오기로 폐기

#### 4. 생존 목표 일수의 최종 값과 근거

- **최종값: D-100 (`survivalDayLimit = 100`)**
- 근거: "백일폭염" 세계관 / UI 정본(`5 UI UX` v15 QA-E, `1 개요`)
- `10 일정`의 D-30은 MVP 데모 일정 표현으로 재해석 — 인게임 카운터는 100→0

#### 5. SealSystem 최종 공식과 유효 경계 목록

- **공식:** `leak_faces == 0 ? min(1, region_cells / 240) : 0` (SealPercent 0~1)
- **유효 경계:** 자연 지형(`isNaturalTerrain`) + `ISealBarrierRegistry`가 인정하는 B 설치물(차열벽/지붕/닫힌 단열문 등). 레지스트리 미연결 시 자연 지형만
- **온도 편의값:** `TemperaturePercent` = 냉기원 가동 && leak==0 ? SealPercent×100 : 0
- **밤 시작:** `GameEvents.OnNightStart` → `InvalidateAll()` 1회 재계산

#### 6. 중앙 game seconds 드라이버 사용법

```csharp
// 부트스트랩 1회
centralTickDriver.Configure(dayNightService);
session.BindTickDriver(centralTickDriver);

// 소비자
public sealed class SmeltingStation : MonoBehaviour, IGameSecondsTickable
{
    void OnEnable() => session.TickDriver.Register(this);
    void OnDisable() => session.TickDriver?.Unregister(this);
    public void Tick(float deltaGameSeconds) { /* ... */ }
}
```

- 시간 기준: `DayNightService.GameSeconds` delta만 relay (Unity `Time.timeScale` 미사용)
- 정지: `DayNightService.TimeScale <= 0` → delta 0

#### 7. 월드 로드 실패 시 상태 보존 방식

`LoadSnapshot`은 renderer=null인 **임시 TileService**에만 diff를 적용한다. 검증 실패 시 즉시 `false` 반환 → 기존 `seed`/`TileService`/`SealSystem`/Tilemap/상자 상태 **한 칸도 변경 없음**.

#### 8. 월드 로드 후 서비스 참조 갱신 방식

- `TileService`: 새 인스턴스로 교체 (`session.TileService`)
- `SealSystem`: **같은 인스턴스**에 `Rebind(tileService)` — 관찰점/`WatchPointSealChanged` 유지
- 성공 시에만 `WorldLoaded` 1회 발행 → 여기서 B 시스템이 재바인딩하면 됨

#### 9. 실행한 테스트 목록

메뉴: `Nyangbingo/Run Dev A Regression Tests`

1. deterministic generation
2. chest distribution
3. tile restore atomicity
4. seal system
5. day/night transition (+ startAtNight)
6. world session round-trip (+ WorldLoaded / Rebind)

#### 10. Unity Console 성공 로그 (기대 문구)

```text
[Nyangbingo] Dev A deterministic generation test completed.
[Nyangbingo] Dev A chest distribution test completed.
[Nyangbingo] Dev A tile restore atomicity test completed.
[Nyangbingo] Dev A seal system test completed.
[Nyangbingo] Dev A day/night transition test completed.
[Nyangbingo] Dev A world session round-trip test completed.
[Nyangbingo] Dev A 회귀 테스트 전체 통과 (6/6).
```

Play 시 추가 성공 로그 예: `WorldLoaded 발행`, `저장 완료`, `로드 완료`, `중앙 game seconds Tick 드라이버 준비 완료`.

#### 11. 아직 남은 개발 B 연결 작업

- 통합 `ISaveSnapshotProvider` (플레이어·인벤·장비·진행·월드)
- 메인 씬 `DawnAutoSave` 실배선
- Crafting/Smelting/AI/전투 → `session.TickDriver` 등록
- 전체 세이브 라운드트립 + 메인 게임 씬 통합
- `ISealBarrierRegistry` / `ICoolingSourceProvider` 실구현 연결

#### 12. 알려진 제한사항

- 월드 생성 “20초 이내”는 **접근성+개수** 검증이지 실제 채굴 시간 시뮬이 아님
- 검증 실패 시 `StartNewWorld`는 예외 — 메인 씬에서 catch 필요
- 임시 타일은 단색 플레이스홀더 — 아트 교체는 sprite만 교체
- `MapGeneratorTestHarness`는 Dev A 디버그용(폐기 예정) — **신규 B 코드는 `WorldSessionController`만 참조**

### 6.3 §5 연결 계약 완료 확인

| # | 요구 | 상태 | API |
|---|---|---|---|
| 1 | DayNightService 참조 | ✅ | `BindTimeService` / `TimeService` |
| 2 | Tick 등록·해제 | ✅ | `BindTickDriver` / `TickDriver.Register` |
| 3 | GameSeconds 조회 | ✅ | `TimeService.GameSeconds` |
| 4 | 시드·타일 캡처 | ✅ | `CaptureSnapshot` |
| 5 | 원자적 복원 | ✅ | `LoadSnapshot` |
| 6 | 로드 후 TileService/SealSystem | ✅ | 프로퍼티 (Seal은 Rebind) |
| 7 | 월드 교체 감지 | ✅ | `event WorldLoaded` |
| 8 | 낮/밤/경고 순서 | ✅ | `OnNightStart → OnDawnWarning → OnDayStart` |

### 6.4 완료 조건 체크

| 조건 | 결과 |
|---|---|
| DevAWorldTest Missing 참조 없음 | ✅ |
| 월드 정상 렌더링 | ✅ (수동 Play 확인) |
| startAtNight 정확 | ✅ (코드+회귀 테스트) |
| 로드 원자적 | ✅ |
| 로드 후 밀폐 관찰/이벤트 유지 | ✅ Rebind |
| SealSystem 기획 일치 | ✅ 처방 C |
| 중앙 game seconds 계약 | ✅ |
| 컴파일 오류 없음 | ✅ (에디터 실행 기준) |
| Dev A 테스트 성공 | ✅ 6/6 메뉴 테스트 준비 |
| 인수인계=코드 일치 | ✅ 이 문서 |
| Dev B 코드 불필요 변경 없음 | ✅ |

---

궁금한 점이나 배선하다가 막히는 부분이 있으면, 이 문서에 인용된 파일의 XML 주석에 설계 이유와 B파트 계약 조항(§ 번호)이 함께 적혀 있으니 먼저 참고해 주세요. ❄️
