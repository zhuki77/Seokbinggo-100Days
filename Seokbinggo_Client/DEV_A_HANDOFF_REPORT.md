# 개발 A파트 → 전체 팀 인수인계 리포트

**작성 시점:** 2026-07-16 (개발 A 3일 부재 전 최종 스냅샷)
**대상:** 개발 B파트, 기획, 아트 팀원 전원
**기준 문서:** `DEV_B_TO_DEV_A_HANDOFF.md` (v17 스펙 / v15 QA 기준)

이 문서 하나만 보면 "지금 뭐가 돌아가고 있고, 내가 어디에 무엇을 연결하면 되고, 다음에 뭘 해야 하는지"를 전부 알 수 있게 정리했습니다. 씬을 직접 열어보지 않아도 이해할 수 있도록 파일 경로와 실제 API 시그니처를 그대로 인용했습니다.

---

## 📋 목차

1. [개발 A파트 코어 마일스톤 요약](#1-개발-a파트-코어-마일스톤-요약)
2. [개발 B파트 배선 연결 가이드](#2-개발-b파트-배선-연결-가이드)
3. [남은 작업 목록 (우선순위 순)](#3-남은-작업-목록-우선순위-순)
4. [씬/에셋 빠른 참조](#4-씬에셋-빠른-참조)

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
| 핵심 규칙 | Flood Fill 기반. **`isNaturalTerrain == true`인 타일만 벽으로 인정** — 플레이어가 설치한 인공 타일은 "틈새"로 취급되어 밀폐가 깨짐 (v15 QA 꼼수 방지 규칙 100% 반영) |
| 최적화 | 셀→리전(Region) 캐시, 변경된 셀 주변만 무효화, 실제로 등록된 관찰 지점(watch point)만 재계산. Flood Fill 자체도 `maxFillCells`(기본 3000)로 크기 제한 |
| 이벤트 | §2.2에서 상세 설명 |
| 디버그 뷰 | `SealSystemDebugView.cs` — 마우스 위치 기준 초록(밀폐)/주황(실외)/맵 밖 전용 대형 마커를 Scene뷰(Gizmos)+Game뷰(런타임 스프라이트) 모두에서 확인 가능 |

### 1.5 원클릭 씬 세팅 자동화 — `NyangbingoDevAWorldTestSceneCreator.cs`

| 메뉴 | 동작 |
|---|---|
| `Nyangbingo/Setup Tilemap Rendering In Dev A Scene` | ① 없으면 테스트 씬 생성 → ② Foreground/Background Tilemap 생성 → ③ **18종 임시 타일 자동 생성**(전경 15종 불투명 단색 + 배경벽 3종 반투명 alpha 0.4 — 채굴 시 뒤가 뚫리는 게 눈에 보이도록) → ④ `TilemapRenderer` 슬롯에 전부 자동 매핑 → ⑤ `MapGeneratorTestHarness`/`PlayerMiningController`/`SealSystemDebugView` 전부 자동 연결·저장 |
| `Nyangbingo/Repair WorldGenerationConfig Asset` | `WorldGenerationConfig.asset`이 손상됐을 때 v17 정본 기본값으로 재생성 + 하니스 재연결 |
| `Nyangbingo/Create Dev A World Test Scene` | 빈 테스트 씬 + 기본 하니스 생성 |

> 임시 타일은 `Assets/Sprites/Temp`, `Assets/Tiles/Temp`에 있습니다. **아트가 준비되면 코드 수정 없이 각 `Tile` 에셋의 `sprite`만 교체**하면 됩니다 (§3.3).

### 1.6 세이브/로드 & 상자 개봉 — `WorldSessionController.cs`

| 항목 | 내용 |
|---|---|
| 위치 | `Assets/Scripts/Nyangbingo/World/WorldSessionController.cs` |
| 역할 | Dev A 월드 상태(TileService/SealSystem/ChestProgress)를 소유하고, Dev B의 `WorldSaveAdapter`를 그대로 소비하는 접착 계층 |
| 저장 (`F5`) | `CaptureSnapshot(save)` → `WorldSaveAdapter.CaptureWorld(save, tileChanges, ..., generator, chestProgress)` |
| 로드 (`F9`) | ① 저장된 seed로 월드 재생성 → ② `TileService.RestoreTileChanges`로 타일 diff 재생 → ③ `WorldSaveAdapter.RestoreChests`로 상자 상태 복원 → ④ `renderer.RenderWorld` 재호출 → ⑤ `SealSystem.InvalidateAll()`로 밀폐 캐시 초기화. 검증 실패 시 이전 라이브 상태를 그대로 유지(부분 복원 금지) |
| 상자 개봉 | `TryOpenChestAt(cell, out chestId, out definition)` → 내부적으로 `GameDataCatalog.FindChest` + `ChestProgress.TryOpen(id, def, seed)` 호출, 성공 시 세이브에 영구 반영 |
| 테스트 하니스 | `MapGeneratorTestHarness.cs`가 F5/F9 핫키 + 상자 마커(노랑=미개봉/회색=개봉)로 눈으로 확인 가능 |

### 1.7 낮/밤 사이클 & 요괴 스폰 쿼리 — `DayNightService.cs` / `TileService.cs` (신규, 최신)

| 항목 | 내용 |
|---|---|
| 위치 | `Assets/Scripts/Nyangbingo/World/DayNightService.cs` |
| 시간 규칙 | 낮 900초 / 밤 540초 실시간 타이머, `timeScale`(0=정지, 인스펙터/코드에서 즉시 조절) |
| 상태 | `public enum DayNightState { Day, Night }`, `State`/`IsNight`/`Day`/`DaysRemaining`(D-30) 프로퍼티 |
| 계약 구현 | `IGameSecondsSource`(`GameSeconds`), `ISaveableTimeSource`(`ITimeSource` 상속: `Day`, `IsNight`, `Dawn` 이벤트, `TimeOfDayGameSeconds`, `RestoreTimeState`) — **B파트 §5 시간 계약 그대로 구현** |
| 이벤트 발행 | 밤 시작 시 `GameEvents.RaiseNightStart()`, 새벽 경고(기본 30초 전) 시 `RaiseDawnWarning()`, 새벽에 `Dawn`(인스턴스)과 `RaiseDayStart()`(전역)를 각각 정확히 1회 |
| 안전성 | `timeScale`을 아주 높여 하루를 한 프레임에 건너뛰어도 경계를 하나씩 순서대로 통과 → 이벤트 스킵/중복 없음. 음수/NaN/Infinity delta는 전부 무시 |
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
dawnAutoSave.Configure(saveManager, dayNightService, snapshotProvider);   // ITimeSource (MonoBehaviour 참조로 연결)
```

- `ForcedBossEncounterBinding`, `BaekjungTimeBinding` 등 이미 `GameEvents.OnNightStart`/`OnDawnWarning`/`OnDayStart`를 구독하도록 설계된 컴포넌트들은 **새 이벤트를 발행할 필요 없이 그대로 반응**합니다. `DayNightService`가 이 3개 이벤트를 정확히 계약대로(§5.2, §5.3) 발행하기 때문입니다.
- ⚠️ 아직 안 된 것: `CraftingProcess`/`SmeltingStation`/`UtilityService` 등 `Tick(float gameSeconds)` 대상들에게 매 프레임 delta gameSeconds를 넣어주는 **중앙 업데이트 루프**는 개발 A의 다음 작업(§3.1)입니다. 지금은 `DayNightService.GameSeconds`가 누적값을 제공만 하고 있는 상태입니다.

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

- `ISealSource.SealPercent`(현재 주 관찰 지점의 밀폐율 0~1)와 `IsInsideSealedArea(Vector2)`는 계약 그대로 노출돼 있습니다.
- **꼼수 방지 규칙**: 플레이어가 인공 타일(`isNaturalTerrain=false`)로 구멍을 막아도 밀폐로 인정되지 않습니다. 자연 지형으로만 복구돼야 다시 밀폐됩니다. 이미 샌드박스에서 "인공 패치 후 여전히 미밀폐 / 자연 패치 후 밀폐" 두 케이스 모두 검증했습니다.

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

---

## 3. 남은 작업 목록 (우선순위 순)

### 3.1 제작 트리·제련 Tick 연동 (최우선)

`DEV_B_TO_DEV_A_HANDOFF.md` §5.3에 명시된 "개발 A의 중앙 업데이트 루프"를 아직 구현하지 않았습니다. 아래 대상들이 매 프레임 `Tick(delta gameSeconds)`를 받아야 합니다:

- `CraftingProcess`, `SmeltingStation`, `UtilityService`
- `WireSnareAbility`, `TurretController`, `HomingProjectilePool`
- `BaekjungTimeBinding`

구현 방향: 매 프레임 `dayNightService.GameSeconds`의 직전 프레임 대비 증가량(delta)을 계산해, 등록된 모든 대상에게 순서대로 전달하는 매니저(예: `CentralTickDriver`)를 하나 만들면 됩니다. `DayNightService.GameSeconds`는 이미 §1.7에서 검증된 누적값을 제공하고 있으니 그 위에 얹기만 하면 됩니다.

### 3.2 인벤토리 UI 및 HUD 통합

- 12슬롯 인벤토리를 실제 `Inventory`/`ItemAcquisition` 데이터에 바인딩.
- HUD: 체온(실내 온도 시스템), 밀폐도(`SealSystem.SealPercent`), **D-30**(`DayNightService.DaysRemaining` — 이미 계산되는 값, 지금은 디버그 `OnGUI`로만 노출돼 있음), 발톱 티어를 실제 UI 위젯으로 교체.

### 3.3 아트 리소스 최종 통합

- `Assets/Tiles/Temp`의 18종 임시 단색 타일(전경 15 + 배경벽 3)을 실제 광물/지하/도구 티어 아트로 교체. **코드 변경 불필요** — 각 `Tile` 에셋의 `sprite` 필드만 실제 아트로 바꾸면 `TilemapRenderer`가 그대로 반영합니다.
- 제작대/화로 등 설치물 프리팹도 실제 아트로 교체.

---

## 4. 씬/에셋 빠른 참조

| 항목 | 경로 |
|---|---|
| 개발 A 테스트 씬 | `Assets/Scenes/DevAWorldTest.unity` |
| B파트 회귀 테스트 씬 | `Assets/Scenes/DevBTest.unity` (건드리지 않음 — 계속 회귀 기준으로 유지) |
| 월드 생성 설정 | `Assets/Data/SO/WorldGenerationConfig.asset` |
| 임시 타일/스프라이트 | `Assets/Sprites/Temp`, `Assets/Tiles/Temp` |
| 월드 관련 스크립트 | `Assets/Scripts/Nyangbingo/World/*.cs` |
| 디버그/테스트 하니스 | `Assets/Scripts/Nyangbingo/Debug/*.cs` |
| 에디터 자동화 스크립트 | `Assets/Editor/NyangbingoDevAWorldTestSceneCreator.cs` |

**테스트 씬 조작법**: Play 후 좌클릭 채굴 / 우클릭 설치(또는 상자 개봉) / `F5` 저장 / `F9` 로드. 좌상단 HUD에서 D-30·낮밤 상태·배속을 확인·조절할 수 있고, 마우스 위치에 따라 초록(밀폐)/주황(실외)/맵 밖 전용 마커가 실시간으로 갱신됩니다.

---

궁금한 점이나 배선하다가 막히는 부분이 있으면, 이 문서에 인용된 파일의 XML 주석에 설계 이유와 B파트 계약 조항(§ 번호)이 함께 적혀 있으니 먼저 참고해 주세요. 3일 후 복귀해서 바로 이어받겠습니다 — 즐거운 작업 되세요! ❄️
