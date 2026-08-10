# V46 세션 인수인계 — 상대(main) 이후 작업분

작성일: 2026-08-10  
작성자 작업 브랜치: `feat/audio-bgm-reupload`  
기준 main(상대 작업 반영): `origin/main` @ PR #30 (`fix/taeyo-wall-routing-and-test-build`) 이후  
Unity: `6000.5.3f1`  
프로젝트: `Seokbinggo_Client/`

> **한 줄**: 상대가 `main`에 올린 작업 **이후**, 이 쪽에서 한 것은 **Title↔MainGame 씬 분리 + v46 실행 큐 P0a~P5(kit 없이)** 이다.  
> 상세 큐/갭 표는 `V46_CONTINUE_HANDOFF.md`를 함께 본다. **이 문서가 “내가 뭘 넣었는지”의 단일 진입점**이다.

---

## 0. 상대 작업과의 경계

| 구분 | 내용 |
|------|------|
| 상대(main에 이미 있음) | PR #30 벽 라우팅·테스트 빌드 등 (`taeyoung0204` 머지) |
| 이 세션/브랜치에서 추가 | 아래 §1~§6 전부 |
| 아직 안 함 | **P0 kit CSV 전량**, 아티팩트 **동사 효과 Apply 20종**, 일부 A모듈(Armor/Furniture/Boss dodge 등) |

작업 트리 정본 브랜치는 이 인수인계 시점 기준 **`main`(본 문서와 함께 반영된 상태)** 이다.  
병렬 브랜치 `feat/v46-nyangbingo-continue`는 참고용이며, 충돌 시 **이 main 반영분을 우선**한다.

---

## 1. 제품 진입: Title → MainGame 씬 분리

| 항목 | 상태 | 비고 |
|------|------|------|
| `Title` 씬 + Build Settings 순서 | ✅ | Title → MainGame |
| `TitleShellUiController` / `GameSceneFlow` | ✅ | 새 게임·로드·타이틀 복귀 |
| Title 버튼 레이아웃·크롬 스케일 | ✅ | 라벨 스프라이트 기준 `TitleChromeScale` |
| `MainGameShellUiController.IsLoadingTransitionActive` | ✅ | `internal set` (전환 연동) |

**확인**: Play → Title만 뜸 → 새 게임 → MainGame. Pause → 타이틀로 → Title.

---

## 2. v46 실행 큐 결과

| ID | 작업 | 상태 | 핵심 산출물 |
|----|------|------|-------------|
| P0 | kit CSV 전량 | ⏸ 보류 | zip/`kit/data` 없음 |
| P0a/b | night-waves + WaveNightRules | ✅ | Builtin 15행 가능 |
| **P1** | WaveNight Encounter 배선 | ✅ | `WaveNightRules` / `WaveNightController` · day≥31 |
| **P2** | 석빙고 s1~s6 · Smithy · 터렛 캡 | ✅ | `SeokbinggoUpgradeService` · stage≥4 대장간 |
| **P3** | DayLight | ✅ | `DayLight.cs` · Presentation 연동 · `day_brightness_by_stage` |
| **P4** | Frost / Bedrock / Evolution / Gimmick | ✅ | 아래 §3 |
| **P5** | 아티팩트 A안 · AccessoryTwo · 한파 세트 | ✅ | 아래 §4 |

하드코딩 금지 4곳 → globals: `wave_advance_sec` · 방어 min1 · 터렛 슬롯 · 낮 밝기 곡선.

---

## 3. P4 요약 (Frost / Gimmick / Evolution)

- `FrostSpreadService`: 이무기 격퇴 시 altar clear → pending 대역 / 채굴 1타 광물 확정 / 3회 시 경계암 개방  
- `TileService`: `FindSurfaceNaturalY` · `IsAirAdjacent` · `TrySetForegroundElement` · 채굴 훅  
- `GimmickWeapon` / `GimmickWeaponProgress`: 첫 서리·백중·이무기(+ id 기반 내습)  
- `EvolutionCraft`: 진화 판정 헬퍼 (대장간 UI 잠금은 기존 stage≥4)  
- `InsulationPanels` + `gimmick_weapon_bonus`  
- 세이브 **schema 19**: `altarClears` · `frostPendingCells` · `gimmickWeaponsGranted`

**주의**: 기믹 무기 아이템 행은 P0 CSV 의존. SO 없으면 granted만 기록.

---

## 4. P5 요약 (Notion 열린 판단 3건 — 종결안 반영)

Notion: [열린 판단 3건 종결](https://app.notion.com/p/39bae468d16b8213b9b6013814a20d3b) · [아티팩트 A안](https://app.notion.com/p/acbae468d16b8298868c010e4b3f558c)

| # | 결정 | 구현 |
|---|------|------|
| ① | A안 `verb_id` (+ usage / activation) | `equipment.csv` 44행 · `EquipmentDefinition` 필드 · `ArtifactVerbId` / Catalog / Rules / Effect 채널 골격 |
| ② | AccessoryTwo = 4 | 기존 enum/UI/세이브 유지 · 회귀 확인 |
| ③ | T6만 세트 `hanpa` (−0.40 / −0.45). T4·T5 none. T3 설한풍 존치 | `ArmorSetRules` · StatSheet 체온 하한 −0.55 |

`accessories.csv`(26행) = 기획 원장. 런타임 정본 = `equipment.csv`.  
**미완**: 아티팩트 동사 **Apply 효과 20종** (스키마·조회만 됨).

---

## 5. 회귀 테스트 (Console only)

다이얼로그 없음. Console 로그만 본다.

| 메뉴 | 성공 로그 |
|------|-----------|
| `Nyangbingo/Run DayLight Regression Tests` | `DayLight regression tests passed.` |
| `Nyangbingo/Run P4 Regression Tests` | `P4 regression tests passed.` |
| `Nyangbingo/Run P5 Regression Tests` | `P5 regression tests passed.` |
| `Nyangbingo/Run Dev B Integration Regression Tests` | `27/27` 목표 |

실패 시 `Debug.LogError(... failed: ...)`.

데이터 재임포트(필요 시):
- `Nyangbingo/Reimport Equipment CSV`
- `Nyangbingo/Reimport Accessories As Equipment` (보조)

---

## 6. 주요 신규·변경 파일 (빠른 색인)

### 신규 스크립트
- World: `DayLight.cs`, `FrostSpreadService.cs`, `InsulationPanels.cs`, `WaveNightRules.cs`, `WaveNightController.cs`, `SeokbinggoRules.cs`, `SeokbinggoUpgradeService.cs`
- Inventory: `GimmickWeapon*.cs`, `ArtifactVerbCatalog.cs`, `ArtifactEffect*.cs`, `ArmorSetRules.cs`
- Crafting: `EvolutionCraft.cs`
- Core: `ArtifactVerbId.cs`
- UI: `TitleShellUiController.cs`, `GameSceneFlow.cs`
- Editor: `NyangbingoDayLight|P4|P5RegressionTests.cs`

### 데이터
- `Assets/Data/CSV/equipment.csv` (44) · `accessories.csv` (26) · `globals.csv` 키 추가
- Equipment SO 아티팩트 20 + T4~T6 갑옷 · Globals `day_brightness_by_stage` · `gimmick_weapon_bonus`
- `GameDataCatalog.asset` 장비 44 등록

### 문서
- `V46_CONTINUE_HANDOFF.md` — 큐/갭 표 (상시 갱신)
- **이 파일** — 상대 이후 작업 범위 인수인계

---

## 7. 다음 담당자가 할 일 (우선순위)

1. Unity에서 §5 회귀 4종 Console 확인  
2. Title → MainGame Play 스모크  
3. **P0**: kit zip 확보 후 CSV 전량 임포트 (items/bosses/crafting 등)  
4. 아티팩트 `ArtifactEffect` 자식 20종 + `ArtifactEffectChannel` 등록·배선  
5. (여유) Armor min1 globals · Furniture 대발 · Boss.dodgePhase · Insulation→체온 런타임 합산

---

## 8. 하지 말 것

- kit 없이 items/bosses를 손으로 대량 날림 (P0에서 덮어쓰기 예정)  
- `AccessoryTwo` enum 숫자 재배치 (세이브 깨짐)  
- T4/T5에 세트 보너스 추가 (GDD: 최상위만)  
- 회귀에 `EditorUtility.DisplayDialog` 재도입 (Console only 합의)

---

## 9. 관련 Notion

| 문서 | URL |
|------|-----|
| 문서 지도 v46 | https://app.notion.com/p/97bae468d16b831db3a201caf3c81515 |
| 개발 명세서 ⑤ | https://app.notion.com/p/8cfae468d16b8200b98d01b38164d4ad |
| 열린 판단 3건 종결 | https://app.notion.com/p/39bae468d16b8213b9b6013814a20d3b |
| 아티팩트 A안 | https://app.notion.com/p/acbae468d16b8298868c010e4b3f558c |
