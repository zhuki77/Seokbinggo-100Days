# 개발 B파트 보완 작업 명세서

**작성 시점:** 2026-07-18  
**작성 기준:** 개발 A 보완 완료본 (`DEV_A_HANDOFF_REPORT.md` §2.5 / §6) + 기존 `DEV_B_TO_DEV_A_HANDOFF.md`  
**대상:** 개발 B파트  
**목적:** 개발 A가 제공한 월드·시간·밀폐·세션 계약 위에, B가 담당하는 게임플레이 시스템을 안전하게 연결·완성하기 위한 작업 정의서

> 이 문서만 보고도 “A가 무엇을 줬는지 / B가 무엇을 해야 하는지 / 어떻게 검증하는지”를 알 수 있게 작성했습니다.  
> API 세부 설명의 정본은 `DEV_A_HANDOFF_REPORT.md`이며, 본 명세서는 **B 작업 단위(B-01~)** 로 재구성한 실행 문서입니다.

---

## 목차

1. [역할 경계와 전제](#1-역할-경계와-전제)
2. [개발 A 인수인계 요약 (읽기 전용)](#2-개발-a-인수인계-요약-읽기-전용)
3. [§5 연결 계약 — B가 반드시 쓰는 API](#3-5-연결-계약--b가-반드시-쓰는-api)
4. [필수 보완 항목 (B-01~B-08)](#4-필수-보완-항목-b-01b-08)
5. [권장 작업 순서](#5-권장-작업-순서)
6. [검증 / 완료 조건](#6-검증--완료-조건)
7. [최종 전달물](#7-최종-전달물)
8. [알려진 제한사항 (A에서 이관)](#8-알려진-제한사항-a에서-이관)

---

## 1. 역할 경계와 전제

### 1.1 담당 범위

| 담당 | 범위 |
|---|---|
| 개발 A (완료) | 월드 생성/타일/밀폐/낮밤/중앙 Tick 드라이버/세션 파사드/`DevAWorldTest` |
| 개발 B (본 명세서) | 플레이어·인벤·장비·제작·제련·AI·전투·통합 세이브·메인 씬 통합·UI/HUD |

### 1.2 금지 / 권장

- **금지:** `MapGeneratorTestHarness`를 신규 프로덕션 코드에서 참조하지 말 것 (폐기 예정 디버그용).
- **권장:** 모든 월드/시간/밀폐 접근은 `WorldSessionController` 하나로만 한다.
- **금지:** 개발 A 월드 코어(`MapGenerator`, `SealSystem` 내부 공식, `DayNightService` 시간 모델)를 임의로 바꾸지 말 것. 필요하면 이슈로 요청.
- **권장:** B 시스템 파괴/재생성 시 `WorldLoaded`에서만 재바인딩한다. SealSystem 구독은 로드 후에도 유지되므로 중복 구독에 주의.

### 1.3 선행 확인 (작업 시작 전 5분)

1. `Assets/Scenes/DevAWorldTest.unity` Play → 지형 보임, Missing Tile 없음
2. `Nyangbingo/Run Dev A Regression Tests` → `6/6` 통과
3. `DEV_A_HANDOFF_REPORT.md` §2.5 / §6 훑어보기

---

## 2. 개발 A 인수인계 요약 (읽기 전용)

### 2.1 A가 완료한 것

| 항목 | 결과 |
|---|---|
| A-01 임시 Tile/Sprite | `Assets/Tiles/Temp`, `Assets/Sprites/Temp`에 실존 + 씬 연결 |
| A-02 startAtNight | 밤 시작 시 정확히 540초 후 새벽 |
| A-03 확정값 | 새벽 경고 **180초**, 생존 목표 **D-100** |
| A-04 Tick 드라이버 | `CentralTickDriver` + `IGameSecondsTickable` |
| A-05 SealSystem | 처방 C: `leak==0 ? min(1, cells/240) : 0` |
| A-06 원자적 로드 | 실패 시 라이브 상태 불변 |
| A-07 Seal Rebind | 로드 후에도 관찰점/이벤트 구독 유지 |
| A-08 생성 검증 | 실패 월드는 예외로 시작 차단 |
| A-09 회귀 테스트 | 에디터 메뉴 6종 |
| §5 세션 파사드 | `TimeService` / `TickDriver` / `WorldLoaded` |

### 2.2 A가 준비한 B 연결용 부품 (아직 메인 씬 미통합)

| 부품 | 위치 | 비고 |
|---|---|---|
| `WorldSessionController` | `World/WorldSessionController.cs` | B의 유일한 세션 진입점 |
| `CentralTickDriver` | `World/CentralTickDriver.cs` | delta game seconds 공급 |
| `WorldSessionSaveProviderAdapter` | `World/WorldSessionSaveProviderAdapter.cs` | 월드-only `ISaveSnapshotProvider` |
| `DawnAutoSave.Configure(...)` | `Save/SaveGame.cs` | 코드 배선용 4-파라미터 API |
| `ISealBarrierRegistry` / `ICoolingSourceProvider` | `Core/WorldContracts.cs` | B가 구현해 `ConfigureSealExtensions`로 주입 |
| 배선 예시 | `Debug/MapGeneratorTestHarness.cs` | **참고용** — 프로덕션에서 복사만 하고 의존하지 말 것 |

### 2.3 확정된 기획 수치 (B HUD/밸런스에 반영)

| 항목 | 값 | 코드 |
|---|---|---|
| 낮 | 900 game seconds | `DayNightService.dayDurationSeconds` |
| 밤 | 540 game seconds | `nightDurationSeconds` |
| 새벽 경고 | 밤 종료 **180초 전** | `dawnWarningLeadSeconds` |
| 생존 목표 | **D-100** | `survivalDayLimit`, `DaysRemaining` |
| 밀폐율 | `leak_faces==0 ? min(1, region_cells/240) : 0` | `SealSystem.SealPercent` |

이벤트 순서 (절대 변경하지 말 것):

```text
OnNightStart → OnDawnWarning → OnDayStart
(+ ITimeSource.Dawn 은 새벽=낮 시작 시점에 인스턴스 이벤트로 1회)
```

---

## 3. §5 연결 계약 — B가 반드시 쓰는 API

정본: `DEV_A_HANDOFF_REPORT.md` §2.5

| # | 할 일 | API |
|---|---|---|
| 1 | 시간 서비스 참조 | `session.BindTimeService(dayNight)` → `session.TimeService` |
| 2 | Tick 소비자 등록 | `session.BindTickDriver(driver)` → `session.TickDriver.Register(this)` |
| 3 | 누적 시간 조회 | `session.TimeService.GameSeconds` |
| 4 | 월드 캡처 | `session.CaptureSnapshot(save)` |
| 5 | 원자적 복원 | `session.LoadSnapshot(save)` — 실패 시 화면/서비스 불변 |
| 6 | 로드 후 참조 | `session.TileService`, `session.SealSystem` (Seal은 **같은 인스턴스**) |
| 7 | 월드 교체 감지 | `session.WorldLoaded += Handler` |
| 8 | 낮밤 이벤트 | `GameEvents.OnNightStart / OnDawnWarning / OnDayStart` |

### 최소 부트스트랩 (메인 씬)

```csharp
// 메인 씬 부트스트랩 — 1회
var session = new WorldSessionController(config, tilemapRenderer, catalog);
session.BindTimeService(dayNightService);
session.BindTickDriver(centralTickDriver);
session.ConfigureSealExtensions(barrierRegistry, coolingSourceProvider); // B 구현체
session.WorldLoaded += OnWorldReplaced;

try
{
    session.StartNewWorld(seed);
}
catch (InvalidOperationException ex)
{
    // A-08: 검증 실패 월드 — 유저에게 알리고 시작 중단
    Debug.LogError(ex.Message);
}

void OnWorldReplaced()
{
    // TileService는 교체될 수 있음. SealSystem은 Rebind로 동일 인스턴스.
    yokaiBrain.SetGameSecondsSource(session.TimeService);
    indoorTemperature.Bind(session.SealSystem);
    // Tick 소비자는 OnEnable에서 session.TickDriver.Register(this)
}
```

---

## 4. 필수 보완 항목 (B-01~B-08)

### B-01. 통합 `ISaveSnapshotProvider` 구현

#### 현재 상태

- A는 **월드 전용** `WorldSessionSaveProviderAdapter`만 제공 (`session.CaptureSnapshot` 래핑).
- 플레이어 위치/체력, 인벤토리, 장비, 진행도, 제작·제련 상태 등은 B 스냅샷에 포함되어야 한다.

#### 요구사항

1. `ISaveSnapshotProvider`를 구현하는 **통합 프로바이더**를 만든다.  
   시그니처: `SaveGame CaptureSnapshot();`
2. 내부에서 최소 다음을 채운다.
   - 월드: `session.CaptureSnapshot(save)` (또는 동일 필드 수동 채움)
   - 시간: `session.TimeService`의 day / isNight / timeOfDay / gameSeconds (기존 SaveGame 필드 규약 준수)
   - 플레이어·인벤·장비·퀘스트/진행도·상자 외 B 소유 상태
3. 로드 시:
   - 월드: `session.LoadSnapshot(save)` 성공 후에만
   - 시간: `session.TimeService.RestoreTimeState(...)` (이벤트 중복 발행 금지 — A 계약)
   - B 상태를 원자적으로 복원. 중간 실패 시 부분 적용 금지 정책을 문서화한다.
4. `DawnAutoSave`와 수동 저장(F5 등)이 **같은 프로바이더**를 쓰게 한다.

#### 검증

- 저장 → 변경 → 로드 후 월드/시간/인벤/장비가 저장 시점과 동일
- 손상된 세이브 로드 시 월드가 부분 갱신되지 않음 (`LoadSnapshot == false`)
- 새벽 자동 저장이 슬롯에 1회만 기록됨

---

### B-02. `DawnAutoSave` 메인 씬 실배선

#### 현재 상태

- API 준비됨: `DawnAutoSave.Configure(SaveManager, ITimeSource, ISaveSnapshotProvider, int slot)`
- DevA 하네스에만 예시 배선됨

#### 요구사항

1. 메인 게임 씬에 `SaveManager` + `DawnAutoSave` + (B-01) 통합 프로바이더 배치
2. `timeSource` = `session.TimeService` (`ITimeSource`/`ISaveableTimeSource`)
3. 구독/해제는 `OnEnable`/`OnDisable` 또는 `Configure`로 대칭 처리
4. 씬 재진입 시 Dawn 중복 구독이 없어야 한다

#### 검증

- 배속으로 새벽 통과 시 자동 저장 로그/파일 1회
- Play 중지 후 재시작해도 구독이 쌓이지 않음

---

### B-03. 중앙 Tick 소비자 연결

#### 현재 상태

- `CraftingProcess.Tick(float)` / `SmeltingStation` 등 **로직은 이미 Tick 형태**인 경우가 많음
- 아직 `IGameSecondsTickable` + `session.TickDriver`에 연결되어 있지 않음
- Unity `Update`/`Time.deltaTime`/`Time.timeScale`로 게임 시간을 돌리면 **안 됨**

#### 요구사항

다음(및 동등 시스템)을 `IGameSecondsTickable`로 감싸 `session.TickDriver`에 등록한다.

| 우선순위 | 대상 |
|---|---|
| P0 | `CraftingProcess`, `SmeltingStation`, `UtilityService` |
| P1 | `WireSnareAbility`, `TurretController`, `HomingProjectilePool` |
| P2 | 시간 기반 AI / 전투 효과, `BaekjungTimeBinding`(Tick 전환 시) |

등록 패턴:

```csharp
public sealed class SmeltingTickBridge : MonoBehaviour, IGameSecondsTickable
{
    [SerializeField] /* session 접근자 */ 
    private SmeltingStation station;

    private IGameSecondsTickDriver driver;

    private void OnEnable()
    {
        driver = /* session.TickDriver */;
        driver?.Register(this);
    }

    private void OnDisable() => driver?.Unregister(this);

    public void Tick(float deltaGameSeconds) => station.Tick(deltaGameSeconds);
}
```

규칙:

- 배속/정지는 `DayNightService.TimeScale`을 따른다 (드라이버가 GameSeconds delta를 relay)
- Destroy된 MonoBehaviour는 드라이버가 자동 제거하지만, `Unregister`는 여전히 호출할 것
- 한 프레임 이중 Tick 금지 (중복 Register 해도 HashSet이라 1회지만, 브리지를 두 개 만들면 안 됨)

#### 검증

- 배속 1: 실시간 ≈ game seconds
- 배속 변경 시 낮밤과 제작/제련이 **동일 비율**로 진행
- 일시정지(`TimeScale=0`) 시 제작/제련/낮밤 모두 정지
- 씬 재진입 후 Tick 호출 횟수가 비정상적으로 늘지 않음

---

### B-04. 밀폐·온도 시스템 실연결

#### 요구사항

1. `ISealBarrierRegistry` 구현  
   - 차열벽 / 차열 지붕 / **닫힌** 단열문 → `true`  
   - 문 열림 / 미인정 재료 → `false` (누수)
2. `ICoolingSourceProvider` 구현  
   - 활성 냉기원일 때만 `IsColdSourceActive == true`
3. `session.ConfigureSealExtensions(registry, cooling)`로 주입
4. 실내 온도 UI는:
   - 순수 밀폐: `SealPercent` (0~1)
   - 온도 게이지: `TemperaturePercent` (0~100, 냉기원 게이트 포함) — **혼용 금지**
5. 플레이어 위치는 `SetPrimaryWatchPoint`, 화로 등은 `RegisterWatchPoint` + `WatchPointSealChanged`

#### 검증

- 완전 밀폐 방 → 밀폐
- 한 칸 구멍 / 문 열림 → 누수(`SealPercent==0`)
- 냉기원 OFF → `TemperaturePercent==0` (밀폐여도)
- 밤 시작 후 타일 상태가 반영된 재계산
- 세이브/로드 후에도 구독자가 이벤트를 받음 (A-07)

---

### B-05. 요괴 AI / 보스 / 밤 스폰 연결

#### 요구사항

1. `GameEvents.OnNightStart`에서 `session.TileService.GetValidSpawnPositions(...)` 사용
2. `session.TimeService`를 `IGameSecondsSource` / `ITimeSource`로 주입  
   (`YokaiBrain.SetGameSecondsSource`, `BossManager.ConfigureForRuntime` 등 기존 API 유지)
3. `OnDawnWarning` / `OnDayStart` 기존 바인딩(`ForcedBossEncounterBinding`, `BaekjungTimeBinding`)이 깨지지 않게 유지
4. 월드 로드 후(`WorldLoaded`) TileService 참조를 갱신

#### 검증

- 밤마다 스폰 후보가 유효 좌표만 반환
- 큰 delta로 여러 경계를 넘어도 이벤트가 순서대로 1회씩
- 로드 직후 잘못된(이전 월드) TileService를 쓰지 않음

---

### B-06. 인벤토리 UI 및 HUD 통합

#### 요구사항

- 12슬롯 인벤 ↔ `Inventory` / `ItemAcquisition`
- HUD 표시:
  - 체온 (B 온도 시스템)
  - 밀폐도 (`SealPercent` 또는 %)
  - **D-100** (`session.TimeService.DaysRemaining`)
  - 발톱 티어
- DevA의 `OnGUI` 디버그 HUD를 프로덕션 UI로 대체

#### 검증

- 낮밤 진행에 따라 D-day가 감소
- 밀폐/누수 전환 시 HUD 밀폐도 갱신
- 인벤 변경이 세이브에 포함 (B-01과 연동)

---

### B-07. 전체 세이브 라운드트립

#### 요구사항

1. 새 게임 → 플레이(채굴/제작/밤 통과) → 저장 → 종료 → 로드
2. 월드 타일 diff + 상자 + 시간 + B 상태가 모두 일치
3. 실패 케이스:
   - 손상된 월드 레코드 → `LoadSnapshot` false, 화면 불변
   - 시간 필드 모순 → `RestoreTimeState` false 처리 정책 명시

#### 검증 체크리스트

- [ ] seed 동일
- [ ] 타일 변경 N건 재생
- [ ] 열린 상자 상태
- [ ] Day / IsNight / TimeOfDay
- [ ] 인벤·장비
- [ ] 제작/제련 진행도(해당 시)
- [ ] 로드 실패 시 라이브 불변

---

### B-08. 메인 게임 씬 통합

#### 요구사항

1. 메인 씬 부트스트랩에서 §3 최소 배선 수행
2. `DevAWorldTest` / `MapGeneratorTestHarness`에 의존하지 않음
3. `DevBTest` 회귀가 깨지지 않게 유지 (기존 계약 이벤트 시그니처 유지)
4. `StartNewWorld` 예외를 UI로 처리
5. (선택) 임시 타일 sprite를 아트로 교체 — 코드 변경 없이 `Assets/Tiles/Temp/*.asset`의 sprite만 교체

#### 검증

- 메인 씬만으로 신규 시작 / 저장 / 로드 / 밤낮 / 제작 Tick 가능
- Missing Script / Missing Reference 0
- DevBTest 기존 회귀 통과

---

## 5. 권장 작업 순서

```text
1) B-08 골격: 메인 씬에 WorldSessionController + DayNight + CentralTickDriver 부트스트랩
2) B-03: Crafting/Smelting Tick 연결 (가장 빨리 체감)
3) B-04: Seal/온도 확장 주입
4) B-05: 밤 스폰 / AI 시간 소스
5) B-01 + B-02: 통합 스냅샷 + DawnAutoSave
6) B-07: 라운드트립 검증
7) B-06: HUD/인벤 UI 마감
```

---

## 6. 검증 / 완료 조건

### 6.1 기능 완료 조건

- [ ] 메인 씬에서 Missing 참조 0
- [ ] Tick 소비자가 `Time.timeScale`이 아닌 `session.TickDriver`로만 진행
- [ ] 배속/정지 시 낮밤·제작·제련 동기
- [ ] 통합 세이브/로드 라운드트립 성공
- [ ] Dawn 자동 저장 1회/새벽
- [ ] 밀폐 화이트리스트·문 열림·냉기원 게이트 동작
- [ ] `WorldLoaded` 후 잘못된 구 참조 사용 없음
- [ ] Dev A 회귀 6/6 여전히 통과
- [ ] Dev B 기존 회귀(DevBTest) 통과
- [ ] 개발 A 월드 코어를 불필요하게 변경하지 않음

### 6.2 성공 로그 예시 (B가 남기면 좋은 문구)

```text
[Nyangbingo] Dev B bootstrap: session TimeService/TickDriver bound.
[Nyangbingo] Dev B Tick consumers registered: Crafting, Smelting, ...
[Nyangbingo] Dev B SealExtensions configured (barrier + cooling).
[Nyangbingo] Dev B unified snapshot capture ok — slot N.
[Nyangbingo] Dev B dawn autosave ok — slot N.
[Nyangbingo] Dev B save round-trip ok.
```

---

## 7. 최종 전달물

### 7.1 필수 전달 파일

- 수정·추가된 B 시스템 코드 (제작/제련/세이브/AI/UI 등)
- 메인 게임 씬 및 부트스트랩
- 통합 `ISaveSnapshotProvider` 구현체
- `ISealBarrierRegistry` / `ICoolingSourceProvider` 구현체
- 갱신된 테스트(또는 DevBTest 회귀 결과)
- 본 명세서 체크리스트 완료 표시 또는 `DEV_B_HANDOFF_REPORT.md` (작업 후 작성 권장)

### 7.2 최종 보고에 넣을 내용 (작업 완료 시)

```text
1. 수정한 파일 목록
2. B-01~B-08 항목별 완료/미완료
3. 통합 스냅샷에 포함되는 필드 목록
4. Tick에 연결된 소비자 목록
5. Seal 확장(문/벽/냉기원) 구현 요약
6. 메인 씬 부트스트랩 위치(파일/오브젝트명)
7. 세이브 라운드트립 검증 결과
8. DevA/DevB 회귀 테스트 결과
9. 개발 A API를 변경한 경우 그 이유와 diff
10. 남은 리스크 / 알려진 제한사항
```

---

## 8. 알려진 제한사항 (A에서 이관)

- 월드 생성 “20초 이내 자원”은 **접근성+개수** 검증이지 실제 채굴 시간 시뮬이 아니다.
- `StartNewWorld` 검증 실패 시 `InvalidOperationException` — 메인 씬에서 반드시 catch.
- `Assets/Tiles/Temp`는 단색 플레이스홀더 — 아트 교체는 Tile 에셋의 sprite만 교체.
- `SealSystem`에 레지스트리를 안 넣으면 자연 지형만 밀폐 벽으로 인정된다 (안전 기본값).
- `WorldSessionSaveProviderAdapter`만으로는 플레이어/인벤이 저장되지 않는다 → **B-01 필수**.

---

## 부록 A. 빠른 파일 맵

| 용도 | 경로 |
|---|---|
| A 인수인계 정본 | `DEV_A_HANDOFF_REPORT.md` |
| A←B 초기 계약서 | `DEV_B_TO_DEV_A_HANDOFF.md` |
| 세션 파사드 | `Assets/Scripts/Nyangbingo/World/WorldSessionController.cs` |
| Tick 드라이버 | `Assets/Scripts/Nyangbingo/World/CentralTickDriver.cs` |
| 계약 인터페이스 | `Assets/Scripts/Nyangbingo/Core/WorldContracts.cs` |
| DawnAutoSave / ISaveSnapshotProvider | `Assets/Scripts/Nyangbingo/Save/SaveGame.cs` |
| 월드-only 어댑터 | `Assets/Scripts/Nyangbingo/World/WorldSessionSaveProviderAdapter.cs` |
| A 테스트 씬 | `Assets/Scenes/DevAWorldTest.unity` |
| B 회귀 씬 | `Assets/Scenes/DevBTest.unity` |

## 부록 B. A 문서에서 그대로 가져올 섹션

개발 B 온보딩 시 아래만 순서대로 읽으면 충분합니다.

1. `DEV_A_HANDOFF_REPORT.md` §2.5 — 연결 계약 8항목  
2. `DEV_A_HANDOFF_REPORT.md` §6 — 최종 전달/확정값/제한사항  
3. 본 문서 §4 — B-01~B-08 실행  

---

작업 중 A API가 부족하면 월드 코어를 우회 패치하지 말고, 세션 파사드 확장 요청으로 공유해 주세요.  
개발 A 보완은 완료된 상태이므로, B는 **연결·통합·콘텐츠**에 집중하면 됩니다.
