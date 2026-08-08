# 기획 ↔ 코드 정밀 대조 검증 리포트 (Plan vs Code Gap Analysis)

> 작성일: 2026-07-16 · **재검증: 2026-08-08 (v46 continue)** · 작성자: 개발 A파트  
> 본 리포트는 자가 검증용이며, 아래 §0·§4는 2026-08-08 코드/`MainGame` 씬 기준으로 상태를 갱신했습니다.  
> §1~§3 본문의 7/16 원문 인용은 역사 기록으로 유지하되, **현재 구현 판정은 §0·§4를 우선**합니다.
>
> **⚠️ 2026-08-08 Notion 정렬**: 100일 정식판의 **현행 기획 정본은 Notion [개발 명세서 ⑤ (v46)](https://app.notion.com/p/8cfae468d16b8200b98d01b38164d4ad) + [문서 지도](https://app.notion.com/p/97bae468d16b831db3a201caf3c81515)** 이다.  
> 본 파일 §0~§5는 **초기 MVP/데모 층(낮밤·밀폐·상자·세이브)** 갭을 다룬다. **v46 모듈 20종·CSV 재임포트·파도 밤**은 [`V46_CONTINUE_HANDOFF.md`](V46_CONTINUE_HANDOFF.md) §2를 본다.  
> 수치 충돌 시 **CSV가 최종 정본** (문서 지도 규칙).
>
> **대조 대상 기획 폴더(당시)**: `ExportBlock-f9c1a2b3-5728-4090-ab2f-afd1682fe106-Part-1/` (Notion 내보내기, 07/13~07/16 일자별 문서 47종)
> **정본 판단 원칙**: 같은 항목에 대해 여러 문서가 다른 값을 말하면 (1) 결재 브리프/오너 승인 로그 → (2) 가장 최근 날짜 문서 → (3) GDD 본문(`1 개요`~`9 기술`) 순으로 우선순위를 매겼습니다.

---

## 0. 총평 (요약) — 2026-08-08 재검증

| 영역 | 상태 |
|---|---|
| 낮 900초 / 밤 540초 타이머 | ✅ **완전 일치** |
| `day++` 타이밍(새벽에만 증가) | ✅ **완전 일치** |
| `Time.timeScale` 금지 규칙 준수 | ✅ **완전 일치** (커스텀 `timeScale` 계수 방식) |
| **새벽 경고 리드타임** | ✅ **완료** — `DayNightService.dawnWarningLeadSeconds = 180` (씬도 180) |
| **D-100 HUD 카운터** | ✅ **완료** — `survivalDayLimit = 100`, HUD D-100 |
| **새벽 자동 저장(DawnAutoSave)** | ✅ **완료** — `MainGameSaveCoordinator` + `DawnAutoSave` MainGame 배선 |
| 밀폐 판정 — 화이트리스트 | ✅ **완료** — `SealBoundaryPolicy` + `MainGameEnvironmentState` 레지스트리 |
| **밀폐율(SealPercent) 산식** | ✅ **완료** — 처방 C (`region_cells / 240`) |
| 밀폐 재계산 트리거 | ✅ **완료** — 타일 이벤트 + `OnNightStart` → `InvalidateAll()` |
| 보물상자 20개(폐허4·상층6·중층6·심층4) | ✅ **완전 일치** |
| 상자 중복 개봉 방지("재출현 없음") | ✅ **완전 일치** |
| 세이브/로드 복원 순서 4단계 | ✅ **완전 일치** |
| 세이브 스키마(`openedChests`) | ✅ **문제 없음** |
| **단열 문 개폐 UX** | ✅ **완료** — placed `door` E 토글 (`TryToggleInsulationDoor` / `SetBarrierActive`) |
| **아트 최종 통합 (P3)** | 🟢 **열림** — `Assets/Tiles/Temp` 등 외부 납품 대기 |

아래부터 각 항목의 **원문 인용 + 코드 라인 대조**를 상세히 남깁니다.

---

## 1. 🌗 [시간 및 낮밤] 기획서 타임라인 명세 vs `DayNightService.cs`

### 1-1. 낮 900초 / 밤 540초 — ✅ 완전 일치

**기획 원문** (복수 문서 일치):

> `개발 가이드 ② 코어 시스템 (07 15)`: `bool IsDay => (gameSeconds % 1440f) < 900f;   // 낮 900s + 밤 540s = 1440s`
> `9 기술`: **낮밤 로직**: `TimeOfDayManager`로 **낮 900초/밤 540초** 사이클
> `회의록 — 확대 회의 (07 13)`: 근거 데이터: kit/data의 globals.csv(**낮900/밤540초**·30일·팀) — **6분(360초) 초안은 이 회의에서 폐기**

**코드 구현**:

```45:46:Seokbinggo_Client/Assets/Scripts/Nyangbingo/World/DayNightService.cs
[Min(1f)][SerializeField] private float dayDurationSeconds = 900f;
[Min(1f)][SerializeField] private float nightDurationSeconds = 540f;
```

→ 900/540이 정확히 상수로 반영되어 있습니다. 폐기된 6분(360초) 초안은 코드에 남아있지 않습니다.

### 1-2. `day++` 타이밍 — ✅ 완전 일치

**기획 원문**: `개발 가이드 ②`: "밤 시작 시 day는 유지 · **새벽에 `day++`**."

**코드 구현**: `Tick()`의 밤→낮 전환 분기에서만 `day++`가 실행되고, 낮→밤 전환 분기(`isNight = true`)에는 `day++`가 없습니다. 일치.

### 1-3. `Time.timeScale` 사용 금지 — ✅ 완전 일치

**기획 원문**: `개발 가이드 ①`: "`Time.timeScale`(유니티 전역 배속)은 애니·물리까지 왜곡하므로 **금지** — 게임 시간 계수 방식." / `개발 가이드 ②` 완료 기준: "**함정**: `Time.timeScale` 쓰지 말 것 — **speedFactor로**."

**코드 검증**: 프로젝트 전체(`Assets/Scripts/Nyangbingo`)에서 `Time.timeScale` 사용 0건 확인. 대신 `DayNightService`는 자체 `timeScale` 필드를 `Time.deltaTime`에 곱해 `Tick()`에 넘기는 방식(`speedFactor`와 동치)을 사용합니다.

```92:95:Seokbinggo_Client/Assets/Scripts/Nyangbingo/World/DayNightService.cs
private void Update()
{
    Tick(Time.deltaTime * timeScale);
}
```

→ 기획이 지적한 "함정"을 정확히 회피했습니다.

### 1-4. 배속(×4/×8/×20) 시뮬레이션 시 이벤트 스킵 여부 — ✅ 구조적으로 안전

**기획 원문**: `1 개요`: "개발빌드 배속키 ×8 토글" / `개발 가이드 ②` 완료 기준: "배속 치트키(**개발용 ×20**)로 3일 돌려 낮밤 로그·D-카운터 확인" / `6 밸런스`: 냥잠 가속 ×4.

**코드 구현**: `Tick(rawDeltaSeconds)`은 `while (remaining > 0f)` 루프로 낮→새벽경고→밤→새벽 경계를 **한 프레임에 여러 번 넘어가도** 순서대로 하나씩 소비하도록 설계되어 있습니다(경계까지의 거리(`toNight`, `toWarn`, `toDawn`)를 계산해 그만큼만 전진 후 다음 경계를 다시 검사). 따라서 ×8, ×20은 물론 그보다 큰 배속에서 한 프레임에 여러 사이클이 지나가도 `OnDawnWarning`/`OnNightStart`/`OnDayStart`가 스킵 없이 정확히 1번씩, 순서대로 발화됩니다. 이 구조는 배속 값에 의존하지 않으므로 ×8/×20 어떤 값이든 동일하게 안전합니다.

### 1-5. 🔴 **새벽 경고 리드타임 — 30초 vs 180초, 불일치**

**기획 원문 — 5개 독립 문서가 동일하게 "180초(3분) 전"을 명시**:

> `개발 가이드 ②`: "**새벽 180초 전 `OnDawnWarning`**(보스·요괴 도주 예고)."
> `개발 가이드 ①`: "`public static event Action OnDayStart, OnNightStart, OnDawnWarning; // 새벽 3분 전`"
> `개발 가이드 ③ 전투·요괴 AI`: "Flee | 맵 밖으로 도주(**새벽 3분 전**, 속도 50%)"
> `9 기술`: "**새벽 3분 전** 자동 경고 + 이동 속도 50% 감소로 도주."
> `아트 발주서 ⑤ UI·HUD`: "ui_warn_dawn | 새벽 경고 아이콘 | 닭 울음 아이콘(**보스 도주 3분 전**)"

**코드 구현**:

```38:38:Seokbinggo_Client/Assets/Scripts/Nyangbingo/World/DayNightService.cs
[Min(0f)][SerializeField] private float dawnWarningLeadSeconds = 30f;
```

→ **기본값이 30초로, 정본(180초)의 6분의 1밖에 안 됩니다.** 필드 자체는 인스펙터에서 조절 가능하지만, 씬에 아직 아무도 수동으로 180으로 고쳐놓지 않았기 때문에 지금 상태로 플레이하면 도깨비 Flee 로직(B파트 `YokaiBrain`)이 실제 스펙보다 훨씬 늦게, 훨씬 짧게 경고를 받게 됩니다. **개발 규칙(`개발 규칙 — Git·Unity`)의 "수치는 CSV/기획이 원본"** 원칙에도 위배됩니다.

> ⚠️ **참고**: `아트 발주서 ⑤`에는 이와는 별개로 "낮/밤 시계 UI가 **전환 15초 전** 점멸"이라는 문구도 있으나, 이는 `OnDawnWarning` 이벤트가 아니라 시계 UI 자체의 시각 효과이므로 혼동하지 말 것.

**조치 필요**: `dawnWarningLeadSeconds` 기본값을 `180f`로 수정(1줄 변경, 컴파일 영향 없음). 복귀 후 최우선 처리 권장.

### 1-6. 🔴 **새벽 자동 저장(DawnAutoSave) — 미연결**

**기획 원문**: `개발 가이드 ④`: "저장: 슬롯 3 + **새벽 오토세이브**. 보스전 중 수동 저장 잠금." / `개발 명세서 v9`: "슬롯 3+새벽 오토세이브. … 오토세이브는 새벽이라 자연 통과."

**B파트 계약 확인**: `DEV_B_TO_DEV_A_HANDOFF.md` §11.4에 이미 아래 계약이 명시되어 있습니다.

```663:672:Seokbinggo_Client/DEV_B_TO_DEV_A_HANDOFF.md
### 11.4 개발 A가 구현할 스냅샷 계약

public interface ISaveSnapshotProvider
{
    SaveGame CaptureSnapshot();
}

`DawnAutoSave`에 `SaveManager`, `ITimeSource` MonoBehaviour, `ISaveSnapshotProvider` MonoBehaviour를 연결하면 `ITimeSource.Dawn` 시 자동 저장한다.
```

**코드 대조**: `DayNightService`는 `ITimeSource`(`Dawn` 이벤트 포함)를 정확히 구현하고 있어 절반은 준비되어 있습니다.

```7:9:Seokbinggo_Client/Assets/Scripts/Nyangbingo/Core/WorldContracts.cs
public interface ITimeSource { int Day { get; } bool IsNight { get; } event System.Action Dawn; }
```

하지만 **`ISaveSnapshotProvider`를 구현하는 MonoBehaviour가 아직 없습니다.** `WorldSessionController`는 `SaveGame CaptureSnapshot()`이 아니라 `bool CaptureSnapshot(SaveGame save)`라는 다른 시그니처를 가진 **일반 클래스(`IDisposable`)**이지, MonoBehaviour도 아니고 `ISaveSnapshotProvider`도 구현하지 않습니다.

```67:67:Seokbinggo_Client/Assets/Scripts/Nyangbingo/World/WorldSessionController.cs
public bool CaptureSnapshot(SaveGame save)
```

→ **현재 새벽이 되어도 자동 저장은 실행되지 않습니다.** F5 수동 저장만 동작합니다. B파트 로직(`DawnAutoSave`)은 완성되어 있으나 A파트가 어댑터를 만들어 씬에 연결하지 않은 "연결 대기" 상태입니다. (자세한 내용은 §4-1)

### 1-7. 🟡 **D-30 vs D-100 — HUD 표기 의미 불일치**

**기획 원문 충돌**:

> `10 일정`(스케줄 요약): "HUD(체온·밀폐도·**D-30**·발톱 티어) 구현"
> `5 UI UX`(v15 QA-E, 07.15 — 더 최신 UI 정본): "상단 중앙 = **D-100 카운터**(태양 아이콘)" / "**D-100 카운트다운(대형 숫자)**"
> `1 개요`: "태양이 거대해져 **D-100 '백일폭염'**" — 30일 MVP 데모는 이 100일 세계관의 앞부분일 뿐이며 "30일 MVP 데모 엔딩 / 정식 100일 백일폭염"으로 별개 개념
> `개발 명세서 v9` 결과화면: 고정 티저 **"D-70"**

**코드 구현**:

```45:45:Seokbinggo_Client/Assets/Scripts/Nyangbingo/World/DayNightService.cs
[Min(1)][SerializeField] private int survivalDayLimit = 30;
```

```64:67:Seokbinggo_Client/Assets/Scripts/Nyangbingo/World/DayNightService.cs
public int SurvivalDayLimit => survivalDayLimit;
public int DaysRemaining => Mathf.Max(0, survivalDayLimit - day);
```

→ 코드는 **"30일 남음"을 세는 D-30 카운터**를 구현했습니다. `10 일정` 문서만 보면 정확히 일치하지만, **더 최신이고 더 상세한 UI 정본(`5 UI UX`, v15 QA-E)은 HUD가 실제로 표시해야 할 숫자를 D-100 계열(예: 진행에 따라 D-70 등으로 감소)로 못박고 있습니다.** 즉 `10 일정`의 "D-30"은 "MVP 데모가 30일 루프까지만 도는 개발 일정"을 가리키는 것으로 보이고, 실제 게임 내 카운터 숫자는 100에서 시작해 줄어드는 것이 최신 의도로 읽힙니다.

**판단**: 기획 문서 자체가 갈리는 지점이라 A파트 코드만의 결함은 아니지만, `SurvivalDayLimit`이라는 변수명/의미가 "생존 목표일=30"으로 굳어 있어 나중에 오너가 D-100으로 확정하면 이름과 표시 방식(라벨 "D-30" → "D-100"에서 시작해 카운트다운) 모두 바꿔야 합니다. **복귀 후 기획팀에 "D-30/D-100 중 최종 확정이 무엇인지" 질문 필요** — 코드는 이미 `survivalDayLimit`를 인스펙터 값으로 자유롭게 바꿀 수 있게 만들어져 있어, 숫자 자체보다는 UI 라벨링(0에서 시작 vs 100에서 시작)과 HUD 연동 방식만 확정되면 바로 대응 가능합니다.

---

## 2. 💚 [밀폐 및 실내 판정] 기획서 v15 QA 규칙 vs `SealSystem.cs`

### 2-1. 🟡 **"인공 벽 불인정" 규칙 — 화이트리스트 범위가 기획보다 좁음**

**중요 발견**: 기획 폴더 전체에서 **"인공 벽은 인정하지 않는다"라는 문장 자체는 존재하지 않습니다.** 오히려 v15 QA-F가 확정한 화이트리스트는 다음과 같이 **인공 구조물 3종 + 자연 지형**을 모두 인정합니다.

**기획 원문**:

> `4 시스템`: "| 차열벽 / 무쇠 차열벽 / 차열 지붕 | ○ | 기본 방벽 |" / "| **자연 지형(흙·돌·암반)** | ○ | **[v15 QA-F 신설 — 최대 판정 요소가 표에 없었다]** … '석빙고는 반지하가 정석'이라 자연 벽에 기대는 게 설계 의도"
> `개발 가이드 ②`: "**밀폐 타일 인정**: 차열벽(점토 미장·무쇠 포함)·차열 지붕·단열 문·**자연 지형**(흙·돌·암반)."
> `4 시스템`: "| 단열 문 | ○ (닫힘 상태) | 개방 중에는 밀폐 체온 보정이 일시 정지 — 판정 자체는 유지 |"
> `4 시스템`: "| 얼음 저장고·장독 창고·화로 등 설치물 | ✕ (구조물은 벽이 아님) | 모듈이라도 '벽'으로 세지 않는다 |"

**코드 구현**:

```275:275:Seokbinggo_Client/Assets/Scripts/Nyangbingo/World/SealSystem.cs
if (!neighborTile.isNaturalTerrain) escaped = true; // 인공 타일 = 밀폐를 깨뜨리는 틈새.
```

```290:290:Seokbinggo_Client/Assets/Scripts/Nyangbingo/World/SealSystem.cs
region.isSealed = !escaped && boundaryWalls.Count > 0 && naturalWallCount == boundaryWalls.Count;
```

→ 지금 코드는 **`isNaturalTerrain == true`인 타일만** 벽으로 인정합니다. 이는 애초에 사용자님이 처음 요청하신 "인공 타일은 밀폐를 깨트리는 틈새로 취급"이라는 지시와는 정확히 일치하지만, **기획 원문(v15 QA-F 화이트리스트)의 최종 결론과는 다릅니다.** 정본은 자연 지형뿐 아니라 **차열벽/차열 지붕/단열 문도 벽으로 인정**해야 합니다.

**구조적 원인**: `MapGenerator.TileData`에는 `isNaturalTerrain`(bool) 하나만 있고, 차열벽·차열 지붕·단열 문은 애초에 **Dev A의 `TileData[,]` 그리드가 아니라 Dev B의 설치물(모듈/오브젝트) 시스템**에 속할 가능성이 큽니다(현재 프로젝트에 해당 프리팹/컴포넌트가 아직 없어 확인 불가). 즉 이 문제는 단순 코드 한 줄 수정이 아니라, **"차열벽이 TileData 그리드 안의 특수 elementType으로 배치되는가, 아니면 SealSystem이 별도의 설치물 목록을 두 번째 데이터 소스로 조회해야 하는가"**를 B파트와 합의해야 하는 설계 이슈입니다. (§4-2 참고)

### 2-2. 🔴 **밀폐율(SealPercent) 산식 — 구 비율식 vs 처방 C, 불일치**

**기획 원문 — 결재로 확정된 최종 산식(처방 C, 처방 B는 기각)**:

> `결재 브리프 v2 — 밀폐도 사태 (07 15)`: "| **C** | **신설 — B 대체** ✅ 확정 | `sealPct = (leak==0) ? 100 × min(1, region_cells / 240) : 0` — **밀폐가 안 됐으면 0%**, 밀폐됐으면 방을 넓힌 만큼 오른다."
> `4 시스템`(v17 최종): "석빙고 온도 산식(v17 — 처방 A·C 오너 승인 2026-07-15): `온도% = (냉기원 가동 && leak_faces == 0) ? 100 × min(1, region_cells / 240) : 0`"
> `개발 가이드 ②`(구 산식, v15 QA-F가 결함으로 지적): "`sealPct = boundary_sealed / boundary_total × 100`" — "⚠️ [v15 QA-F] 이 산식에는 알려진 결함이 있다 — 비율이라 구멍 1칸이 분모에서 1/N로 희석된다(완벽한 기지+구멍 1칸 = 97.2%)."

**코드 구현**:

```289:289:Seokbinggo_Client/Assets/Scripts/Nyangbingo/World/SealSystem.cs
region.sealPercent = boundaryWalls.Count == 0 ? 0f : (float)naturalWallCount / boundaryWalls.Count;
```

→ 코드가 구현한 산식은 **`자연 벽 수 / 전체 경계 벽 수`의 비율**입니다. 이는 기획서가 "v15 QA-F 결함"으로 명시적으로 지적하며 폐기한 **구(舊) 산식과 동일한 계열**이며, 최종 채택된 **처방 C(방 크기 비례, `region_cells/240`)와는 완전히 다른 식**입니다.

**영향 범위**: `ISealSource.SealPercent`는 B파트 `TemperatureSystem`/HUD 밀폐도 게이지가 직접 읽는 값입니다(`개발 가이드 ④` HUD 바인딩 표: "밀폐도 게이지 | SealSystem.sealPct | OnSealChanged"). 지금 상태로 연동하면 **HUD에 표시되는 밀폐도 수치 자체가 기획 스펙과 다른 값**이 됩니다. `isSealed`(불리언, 완전 밀폐 여부)는 "구멍 1칸=escaped=true"로 처리되어 처방 C의 "leak==0" 게이트와 결과적으로 유사하게 동작하지만, **연속값인 `sealPercent`는 방 크기 기반이 아니므로 반드시 수정이 필요**합니다.

**추가로 코드에 없는 것**: 정본 산식은 `냉기원 가동 && leak_faces == 0`이라는 AND 조건을 가지지만, 코드에는 "냉기원(아이스박스류) 가동 여부"를 확인하는 로직이 전혀 없습니다. 이는 B파트 온도 시스템의 상태이므로 A파트가 임의로 넣을 수 없고, **SealSystem이 외부에서 "냉기원 가동 여부"를 주입받는 인터페이스가 필요**합니다.

### 2-3. 🟡 밀폐 판정 아키텍처 — "코어(냉기원) 창" vs "임의 관찰 지점", 개념 차이

**기획 원문**: `결재 브리프 v2`: "| A | 밀폐 창 ✅ 확정 | '하늘' 개념 폐기 → **코어 중심 직사각 창 RX=28·RY=12**(창 57×25). 캡이 유도값 1,425가 되어 캡 도달 자체가 구조적으로 불가능."

**코드 구현**: `SealSystem`은 "코어(얼음 저장고)"라는 고정 앵커 개념이 없고, 임의의 `Vector3Int` 셀을 "관찰 지점(watch point)"으로 등록해 그 지점에서부터 Flood Fill을 수행합니다(캡은 `maxFillCells`, 기본값은 고정 셀 수 상한이며 RX×RY 사각창 방식이 아님).

→ 이는 **틀린 것이 아니라 더 범용적인 설계**입니다(플레이어 위치·마우스 디버그 등 여러 지점을 동시에 볼 수 있음). 다만 기획이 가정하는 "베이스에는 코어가 하나뿐이고 그 주변 57×25 창만 본다"는 전제와는 다르므로, **성능 캡의 안전 마진(정본은 구조적으로 캡 도달 불가능, 코드는 `maxFillCells` 도달 시 `escaped=true`로 처리)**의 의미가 다릅니다. 기능적 문제는 없으나 설계 문서와 아키텍처 설명이 불일치하므로 인수인계 문서에 명시할 필요가 있습니다.

### 2-4. 🟡 재계산 트리거 — 타일 이벤트는 O, 밤 시작 트리거는 누락

**기획 원문**: `개발 가이드 ②`: "`RecalcSeal()`: 호출 시점 = **타일 배치/파괴 직후 + 밤 시작**. 매 프레임 금지!" / "**함정**: 재계산 트리거를 이벤트(OnTilePlaced/Broken)로만." / "문 개폐는 `RecalcSeal` 트리거가 아니다."

**코드 구현**:

```72:73:Seokbinggo_Client/Assets/Scripts/Nyangbingo/World/SealSystem.cs
GameEvents.OnTileBroken += HandleTileChanged;
GameEvents.OnTilePlaced += HandleTileChanged;
```

→ 타일 배치/파괴 트리거는 정확히 구현되어 있고, 문 개폐를 트리거로 쓰지 않은 것도 기획과 일치합니다. 하지만 **`GameEvents.OnNightStart` 구독이 없어 "밤 시작 시 재계산"이 빠져 있습니다.** 현재는 밤이 되어도 자동으로 seal 캐시가 재계산되지 않고, 다음 타일 변경 이벤트가 발생할 때까지 예전 캐시를 그대로 씁니다. 매 프레임 재계산 금지라는 성능 규칙은 잘 지켰지만, 밤 시작 트리거 1건이 빠져 있는 부분 일치 상태입니다.

### 2-5. ✅ Flood Fill 알고리즘(BFS, 상하좌우) — 일치

**기획 원문**: `개발 가이드 ②`: "코어에서 공기를 타고 번지다가, 경계가 전부 '밀폐 타일'이면 밀폐된 것" — 상하좌우 이웃 검사 + `queue.popleft()`(BFS, v15 QA-F가 `pop()`=DFS 버그를 지적).

**코드 확인**: `SealSystem`의 Flood Fill은 `Queue<Vector3Int>`(FIFO, `Dequeue()`)를 사용하는 정상 BFS이며, 상/하/좌/우 4방향만 검사합니다. 일치.

---

## 3. 💾 [세이브/로드] 기획 동기화 절차 vs `WorldSessionController.cs`

### 3-1. ✅ 복원 순서 4단계 — 완전 일치 (추상 단계를 구체 기술로 세분화)

**기획 원문**: `개발 가이드 ④`: "로드 순서: **시드로 맵 재생성 → tilemapDiff 덮어쓰기 → 오브젝트 복원 → 시스템 값 주입**."

**코드 구현**:

```19:22:Seokbinggo_Client/Assets/Scripts/Nyangbingo/World/WorldSessionController.cs
///  2) TileService.RestoreTileChanges로 타일 변경 이력을 그대로 재생해 타일맵을 복원한다.
///  3) WorldSaveAdapter.RestoreChests로 20개 상자의 열림 상태를 복원한다
///  4) 타일맵 렌더러를 갱신하고, SealSystem.InvalidateAll()로 밀폐 캐시를 초기화한다.
```

```95:128:Seokbinggo_Client/Assets/Scripts/Nyangbingo/World/WorldSessionController.cs
// (1) 시드로 재생성 → loadedTileService.RestoreTileChanges(save.tileChanges) → (2)
// Nyangbingo.Save.WorldSaveAdapter.RestoreChests(...) → (3)
// renderer.RenderWorld(result.tiles) → sealSystem.InvalidateAll() → (4)
```

→ 기획의 "오브젝트 복원"은 코드에서 "상자 상태 복원"으로, "시스템 값 주입"은 "렌더러 갱신 + 밀폐 캐시 무효화" **두 개의 구체적 기술 단계로 세분화**되어 구현되어 있습니다. 이는 기획이 추상적으로 표현한 부분을 실제 엔진 레벨에서 필요한 만큼 더 상세하게 구현한 것으로, 스펙과 충돌하지 않습니다.

### 3-2. ✅ 보물상자 20개(폐허4·상층6·중층6·심층4) — 완전 일치

**기획 원문**: `6 밸런스`: "③ 상자표(WorldGenerator ④구조물 패스 배치 · 맵당 고정 20개 · 재출현 없음)" / `4 시스템`: "보물 상자(v8) | 지상 폐허 4 · 상층 6 · 중층 6 · 심층 4 = 맵당 20개"

**코드 구현**:

```62:66:Seokbinggo_Client/Assets/Scripts/Nyangbingo/World/WorldGenerationConfig.cs
[Header("구조물 — Pass 4 : 보물 상자 (6-12 표: 폐허4·상층6·중층6·심층4)")]
[Min(0)][SerializeField] private int chestCountRuins = 4;
[Min(0)][SerializeField] private int chestCountUpper = 6;
[Min(0)][SerializeField] private int chestCountMiddle = 6;
[Min(0)][SerializeField] private int chestCountDeep = 4;
```

실제 배포된 `Assets/Data/SO/WorldGenerationConfig.asset`에도 `chestCountRuins: 4 / chestCountUpper: 6 / chestCountMiddle: 6 / chestCountDeep: 4`로 저장되어 있음을 직접 확인했습니다. 정확히 일치합니다.

### 3-3. ✅ 상자 중복 개봉 방지("재출현 없음") — 완전 일치

**기획 원문**: `개발 가이드 ④`/`4 시스템`/`6 밸런스`: "**재출현 없음**(개봉 좌표를 diff에 기록)." (기획서에 "1회만"이라는 표현은 없으나 "재출현 없음"이 동일한 의도)

**코드 구현**: `ChestProgress.TryOpen(id)`는 이미 열린 상자에 대해 `IsOpened(id)`를 검사해 거부하고, `WorldSaveAdapter.CaptureWorld`가 `chestProgress.IsOpened(chestId)`를 기준으로 `save.chests`/`save.openedChestIds`에 영구 기록합니다. 로드 시 이 상태가 그대로 복원되므로 재출현이 발생하지 않습니다. 일치.

### 3-4. ✅ 세이브 스키마 표기 차이(`openedChests`) — 실질적 문제 없음

**기획 문서 간 충돌 발견** (조사 결과): `개발 가이드 ④`(07.15) JSON 예시에는 `openedChests` 필드가 있지만, `개발 명세서 v9`의 "스키마 v5" 목록에는 해당 필드가 빠져 있습니다.

**코드 확인**: 실제 `SaveGame.cs`는 이미 두 가지 표현을 **모두** 가지고 있어 문서 간 표기 차이를 흡수하고 있습니다.

```730:745:Seokbinggo_Client/Assets/Scripts/Nyangbingo/Save/SaveGame.cs
save.chests.Clear();
save.openedChestIds.Clear();
...
save.chests.Add(new ChestStateRecord { chestId = chestId, position = ..., opened = opened });
if (opened) save.openedChestIds.Add(chestId);
```

→ `chests`(전체 상자의 위치+열림 상태, 명세서 v5 스타일)와 `openedChestIds`(열린 것만 모은 목록, 가이드④ 스타일)를 동시에 기록하므로, 기획 문서 간의 표기 불일치가 코드 레벨에서는 이미 해소되어 있습니다. 조치 불필요.

### 3-5. 참고: 손상된 세이브 처리 — 기획에 없는 방어 로직을 코드가 추가로 갖춤

기획 폴더 전체에서 "세이브 손상/검증 실패 시 동작"에 대한 규정을 찾지 못했습니다. 반면 코드는 `WorldSaveAdapter.ValidateWorldRecords`/`TryValidateWorldRecords`로 좌표 중복·NaN·빈 ID 등을 사전 검증해 잘못된 세이브의 로드를 거부하고 기존 상태를 유지합니다. 기획보다 더 견고한 안전장치이므로 문제 없음(오히려 장점).

### 3-6. 참고: F5/F9 핫키 — 기획에 없는 디버그 전용 기능, 문제 없음

기획 폴더에 F5/F9 세이브·로드 핫키는 없습니다(배속 토글·일시정지 키만 언급). `MapGeneratorTestHarness.cs`의 F5/F9는 명백히 **디버그 하니스 전용 기능**으로 문서화되어 있으므로 기획과 충돌하지 않습니다.

---

## 4. 갭 추적 — 2026-08-08 재검증 결과

### 4-1. ✅ [P0] 새벽 자동 저장(DawnAutoSave) — DONE

- `MainGameSaveCoordinator`가 `ISaveSnapshotProvider`를 구현하고 `DawnAutoSave.Configure(...)`로 연결.
- MainGame 씬에 `MainGameSaveCoordinator` + `DawnAutoSave` 배선됨.
- 테스트/하니스용 얇은 어댑터: `WorldSessionSaveProviderAdapter`.

### 4-2. ✅ [P0] `dawnWarningLeadSeconds` 180 — DONE

- `DayNightService` 기본값 `180f`, MainGame 씬 직렬화 값도 180.

### 4-3. ✅ [P1] SealPercent 처방 C — DONE

- `SealSystem`: `leak_faces==0 ? min(1, region_cells/seal_target_cells) : 0` (기본 분모 240).
- 냉기원은 `TemperaturePercent` + `ICoolingSourceProvider`로 분리.

### 4-4. ✅ [P1] 차열 화이트리스트 — DONE

- `SealBoundaryPolicy` + SO `SealWhitelist` + `MainGameEnvironmentState` 레지스트리.
- 제품 설치물 `insul_wall` / `door` / `roof` 등 인정.

### 4-5. ✅ [P2] `OnNightStart` 재계산 — DONE

- `SealSystem`이 `GameEvents.OnNightStart` 구독 → `InvalidateAll()`.

### 4-6. ✅ [P2] D-100 HUD — DONE

- `survivalDayLimit = 100`, HUD D-100 표기.

### 4-7. ✅ [P2] 제작·제련 틱 연동 — DONE

- MainGame `CentralTickDriver` + `MainGameRuntimeServices`에 Furnace/Foundry/CraftingProcess 등록.

### 4-8. ✅ [P3] HUD/인벤 바인딩 — DONE (슬롯 수는 v29=50으로 확장)

- 체온·밀폐·D-100·발톱 HUD 바인딩 완료. 기획 초안 12슬롯 → 런타임 50슬롯은 제품 확정 수치.

### 4-9. 🟢 [P3] 아트 리소스 최종 통합 — OPEN

- `Assets/Tiles/Temp` 등 임시 타일·외부 납품 아트 교체 대기. 코드 갭이 아니라 에셋 갭.

### 4-10. ✅ [잔여] 단열 문 개폐 UX — v46에서 채움

- placed object `door`에 대해 E → `SetBarrierActive` 토글.
- 열림(`BarrierActive=false`): 밀폐 미인정. 닫힘: 밀폐 인정.
- 전경 타일 `door` elementType 개폐 상태는 별도 필드가 없어 이번 범위 제외(제품 경로는 placed object).

---

## 5. 결론 (2026-08-08)

- 7/16 리포트에서 열었던 P0~P2 수치·배선 갭은 **현재 코드/씬에서 모두 닫혔습니다.**
- 남은 열림은 주로 **아트 최종 통합(P3)** 과 발표용 Development 빌드 단축키 재빌드 확인입니다.
- 역사적 상세 인용(§1~§3)은 보관용으로 유지합니다. 작업 우선순위는 이 §4를 따르세요.

---

## 6. Notion v46 (100일 정식판) — 2026-08-08 정렬 요약

정본: [개발 명세서 ⑤](https://app.notion.com/p/8cfae468d16b8200b98d01b38164d4ad) · [문서 지도](https://app.notion.com/p/97bae468d16b831db3a201caf3c81515)

| 층 | 상태 |
|----|------|
| MVP 코어 (§0~§4) | ✅ 대체로 닫힘 |
| v46 CSV (items 160, bosses 10, modules 11, night-waves 15 등) | 🔴 로컬 `Assets/Data/CSV`는 데모 규모 (items≈86, bosses≈4, modules≈5, **night-waves 없음**) |
| v46 모듈 20종 (WaveNight·FrostSpread·대장간·진화·DayLight assert 등) | 🔴 대부분 미착수 / 부분만 존재 |
| 문서 지도 열린 판단 3건 (아티팩트 equipment·AccessoryTwo 데이터·T4~T6 세트) | 🟡 AccessoryTwo는 코드에 있음, 데이터·세트는 열림 |

상세 표·다음 순서는 **`V46_CONTINUE_HANDOFF.md`** 에 둔다. 파트 A/B는 Notion상 파일 소유 표기이며 저장소는 통합 `main`이다.
