# Dev B 인수인계 — 2026-09-13

작성일: 2026-09-13  
브랜치: `main`  
원격: `https://github.com/zhuki77/Seokbinggo-100Days.git`  
Unity 프로젝트: `Seokbinggo_Client`  
진행 표기 정본: `Assets/Data/CSV/content-status.csv`  
데이터 정본: **CSV → SO**. 문서와 CSV가 다르면 **CSV가 맞다.**

> 이 문서가 Dev B **잔여 작업의 단일 진입점**이다. 구문서(`V46_*`, `DEV_B_TO_DEV_A_*`)는 참고만.

---

## 0. 한 줄 요약

Dev B에서 **코드로 닫을 수 있던 큰 런타임 덩어리**는 반영·플레이 확인까지 끝났다.  
남은 것은 대부분 **오너/기획 게이트(수치·대개편)** 와 **v70 대량 CSV 대개편**, 그리고 인수 후 **회귀 한 번 더**이다.

| 구분 | 상태 |
|------|------|
| 후기 보스·기믹/진화·T4~T6 방어·도감 17 | ✅ |
| 작물 zone·까치 가이드·서리 원석·버섯 회복 | ✅ |
| 부적 5·시작 특성 4 (선택 UI·세이브 schema 27) | ✅ |
| mineral freshness / Mineral Tiers 재임포트→manifest | ✅ |
| 도감 레거시 8칸 Error / Arial 폰트 예외 | ✅ 수정 |
| 특성 선택 UI 화면 오버플로 | ✅ 중앙 280×236 카드 |
| `day-curve-ext` 55~100 | ⏳ 오너 수치 |
| v70 accessories/globals/terrain-spawn 등 | ⏳ 대개편 |
| opening 일러 3장 | ⏳ 미착수 |

---

## 1. 인수인이 바로 할 일

1. `git pull origin main` (이 인수인계 push 이후)  
2. Unity 컴파일 대기  
3. **`Nyangbingo` → `Run Dev B Integration Regression Tests`** → **`65/65` Pass**  
4. **`Validate Product Data Freshness`** Pass  
5. (선택) Title → 새 게임 → 특성 선택 카드가 화면 안인지 / 도감 열림 / Arial Error 없음

### Freshness

- 파일: `ProjectSettings/NyangbingoDataImportManifest.txt`  
- mineral: `mineral-tiers.csv|18|A29B3FF2…`  
- Mineral만 고치면 메뉴 **`Reimport Mineral Tiers CSV`** (카탈로그+manifest 자동) 또는 **`Reimport v72 Data Bundle`**

### 무시해도 되는 경고

- `TilemapRenderer` 이름 = Unity 빌트인 충돌 (`Nyangbingo.World.TilemapRenderer`)  
- Input Manager deprecation  
- Unity Connect / 토큰 네트워크 메시지  
- 레거시 도감 격자 비활성 Log: 통합 제작 UI가 **17장** 담당 (Error 아님)

### 커밋/푸시에 넣지 말 것

- `Packages/com.coplaydev.unity-mcp/`, `.cursor/`  
- `ProjectSettings/TimeManager.asset`, `Packages/packages-lock.json`  
- `Title.unity` / `MineralTiers/*.asset` **EOL만** 바뀐 더티

---

## 2. 이번 구간 완료 기능 (코드 기준)

### 2-1. 전투·보스·장비

- 후기 보스: 지귀 / 강철이 불꽃 / 산군 / 삼두구미 / 업구렁이 / 영노 / 강철이 완전체 등  
- 기믹·진화 발톱/부채/활, 유틸·화력 터렛, T4~T6 방어  
- 도감 17 · frostclaw 사망 프레임 6

### 2-2. 서리·원석

- `FrostSpreadService.OreOf`: ≥3 `cold_wave_ore`, ≥2 `ice_root`, else `seonge_ore`  
- `mineral-tiers` **18행** (frost 3종 `freq=0`)

### 2-3. 작물·까치·버섯

- `CropRules` zone01~10, 심기·밴드 회복  
- `MagpieGuideRules` `fly_to_goal` / 복귀 6초, Goal Badge 유도  
- `mushroom_heal` 15/30/50 연동 (status 구현됨, CSV `[가안]` 라벨은 잔존 가능)

### 2-4. 부적·시작 특성

- 부적 5: `TalismanRuntime` (이미 런타임) + status 구현됨  
- 특성 4: `TraitRules` / `TraitRuntime`  
  - melee +10% **보스 제외** · ranged 시작 `straw_sling` · labor 채굴크리 +0.05 · cool 낮 상승 ×0.9  
  - 세이브 `selectedTraitId`, schema **27**  
  - 새 게임 시 `GameShellScreen.TraitSelect` (컴팩트 카드 UI)

### 2-5. UI·품질 수정

- Codex: 씬 구 8칸 ≠ 17 → 레거시 컨트롤러 비활성, 제작 UI 도감 담당  
- Crafting 도감 폰트: `LegacyRuntime.ttf` (Arial 예외 제거)

### 2-6. 회귀

- `NyangbingoDevBIntegrationRegressionTests` → **65/65** (특성 계약 포함)

---

## 3. content-status 스냅샷 (2026-09-13)

| status | 행수 |
|--------|-----:|
| 구현됨 | 386 |
| 추가예정 | 240 |
| 수정예정 | 235 |

### 추가예정 (파일별)

| 파일 | 건수 | 메모 |
|------|-----:|------|
| globals.csv | 134 | v70 대개편 대기 |
| terrain-spawn.csv | 70 | |
| items.csv | 26 | 아티팩트·보스 재료 계열 |
| day-curve-ext.csv | 10 | **55~100** 오너 확정 필요 |

### 수정예정 (파일별 상위)

| 파일 | 건수 | 메모 |
|------|-----:|------|
| globals.csv | 98 | v63~ 대개편 |
| crafting-tree.csv | 52 | |
| day-curve.csv | 30 | 1~30일 |
| accessories.csv | 25 | `magpie_bell`만 구현됨 |
| equipment.csv | 15 | |
| yokai-stats.csv | 7 | |
| day-curve-ext.csv | 5 | **31~50** |
| bosses.csv | 3 | `king_dokkaebi`, `mother_bulgasari`, `imugi_boss` |

---

## 4. 주요 경로

| 주제 | 경로 |
|------|------|
| 인수인계 (본 문서) | `Seokbinggo_Client/DEV_B_CONTINUE_HANDOFF.md` |
| 서리 원석 | `World/FrostSpreadService.cs`, `Data/CSV/mineral-tiers.csv` |
| 작물 | `World/CropRules.cs`, `MainGameWorldDecorationRenderer.cs` |
| 까치 | `MagpieGuideRules.cs`, `MagpieCompanionRuntime.cs` |
| 특성 | `World/TraitRules.cs`, `World/TraitRuntime.cs`, `UI/MainGameShellUiController.cs` |
| 부적 | `World/TalismanRuntime.cs` |
| 세이브 | `Save/SaveGame.cs` (schema 27), `MainGameSaveCoordinator.cs` |
| 도감 | `UI/MainGameCraftingUiController.cs` (17장), `MainGameCodexController.cs` (레거시 위임) |
| status | `Data/CSV/content-status.csv` |
| 매니페스트 | `ProjectSettings/NyangbingoDataImportManifest.txt` |
| 회귀 | `Editor/NyangbingoDevBIntegrationRegressionTests.cs` |

---

## 5. 남은 작업 — 우선순위

### P0 (인수 직후)

1. `65/65` 회귀 + Freshness Pass 재확인  
2. Play 스모크: 특성 선택 · 도감 · 서리 원석 · 캣닢 · 까치 · 버섯 · 보스 1종  

### P1 (오너·기획 게이트 — 수치 발명 금지)

- `day-curve-ext` **55~100** 확정 후 CSV·임포트·status  
- 버섯 `[가안]` 밸런스 최종 OK 여부  
- early 보스 3 · accessories 25 · globals/terrain-spawn **대개편 범위** 지정  

### P2 (대량 대개편 — 티켓 단위)

- globals 추가+수정  
- terrain-spawn 70  
- crafting-tree / day-curve / accessories / equipment  
- items 아티팩트·재료, opening 일러 3장 (`opening_illust_pages=3`)  

### P3 (품질)

- `TilemapRenderer` rename (선택)  
- Input System 이전 (강제 아님)  

---

## 6. 규칙 (절대)

1. 타이머는 `TimeManager.gameSeconds` / Tick에는 **증가량**.  
2. 파도·방어 min1·터렛 캡·낮 밝기 = **globals만** (하드코딩 금지).  
3. CSV가 원본. SO는 생성물.  
4. 근접 특성은 **보스에 적용하지 않음** (`trait_melee_boss_exempt`).  
5. Dev B 회귀를 약화·삭제하지 말 것.  

---

## 7. 후속 개발자 복붙 프롬프트

```
Seokbinggo_Client Dev B 이어서.
먼저 Seokbinggo_Client/DEV_B_CONTINUE_HANDOFF.md 를 읽고 main을 pull한 뒤 진행해.

끝난 것: 후기 보스·기믹/진화·T4~T6·도감17·작물·까치·frost 원석·버섯·부적5·시작특성4(schema27)·
회귀 65/65·mineral freshness·도감/Arial/특성UI 수정.
남음: day-curve-ext 55~100(오너), v70 대개편 버킷, opening 일러, Play 스모크.
CSV 정본, content-status로 표기. MCP/TimeManager/packages-lock/Title EOL은 커밋하지 마.
하드코딩 금지: 파도·방어 min1·터렛 캡·낮 밝기 = globals만. melee 특성은 보스 제외.
```

---

## 8. 인수 체크리스트

- [ ] `main` pull 후 최신 커밋 확인  
- [ ] Dev B Integration Regression **65/65** Pass  
- [ ] Validate Product Data Freshness Pass  
- [ ] 새 게임 → 특성 선택 UI 화면 안  
- [ ] 도감 열림 · Arial Error 없음  
- [ ] 다음 티켓: day-curve-ext vs v70 버킷 합의  

---

*갱신 시: 날짜 · push된 커밋 해시 · status 건수 · 회귀 N/N 를 함께 고친다.*
