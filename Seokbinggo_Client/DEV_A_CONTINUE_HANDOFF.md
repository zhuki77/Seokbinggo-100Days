# 개발 A 인수인계 — 2026-09-13 (B 최근 반영분)

작성일: 2026-09-13  
브랜치: `main` (`907b99c` 포함, origin 동기화됨)  
원격: `https://github.com/zhuki77/Seokbinggo-100Days.git`  
Unity: `Seokbinggo_Client`

> **이 문서는 개발 A용**이다. 씬·아트·UI 배선·플레이 스모크·연동 확인을 A가 이어갈 때 본다.  
> 개발 B **잔여 CSV/대개편**은 디코 인수 메시지 + `content-status.csv`를 본다.  
> 구문서 `DEV_B_TO_DEV_A_HANDOFF.md`는 초기 계약 참고용(수량·범위 구식).

---

## 0. 한 줄 요약

개발 B가 MainGame에 **이미 심어 둔 시스템**이 많다. A의 일은 “처음부터 만들기”가 아니라  
**아트·프리팹·입력 폴리시·씬 배선이 B 계약과 안 깨지게 유지·보강**하는 쪽이다.  
세이브 schema는 **27** (`selectedTraitId`).

---

## 1. A가 바로 할 일

1. `git pull origin main`  
2. Unity 컴파일  
3. (선택) `Nyangbingo` → **Run Dev B Integration Regression Tests** → **65/65** (깨뜨리지 말 것)  
4. Play 스모크 (아래 §4)

---

## 2. B가 넣어 둔 것 — A가 만질 때 주의

### 2-1. 새 게임 흐름

| 항목 | 위치 / 계약 |
|------|-------------|
| Title → MainGame | `MainGameLaunchRequest` (NewGame / Continue) |
| **시작 특성 선택** | 새 게임 직후 `GameShellScreen.TraitSelect` · 중앙 카드 UI (`MainGameShellUiController`) |
| 세이브 | `selectedTraitId` · schema **27** · 재선택 불가 |
| 특성 효과 | melee 요괴 +10%(**보스 제외**) · ranged `straw_sling` · labor 채굴크리 +0.05 · cool 낮 상승 ×0.9 |

A가 UI를 꾸밀 때: 특성 패널을 지우고 Gameplay로만 두면 **미선택 세이브**가 생긴다. 선택 완료 → `CompleteTraitSelect` 경로 유지.

### 2-2. 도감

- **17장** 정본. 씬에 남은 구 8칸 `MainGameCodexController`는 레거시로 **비활성**되고,  
  **제작 UI 통합 도감** (`MainGameCraftingUiController`)이 격자를 런타임 생성한다.  
- 폰트는 **`LegacyRuntime.ttf`** (Arial 금지 — Unity 6 예외).

### 2-3. 월드·채집·동반

| 기능 | 훅 |
|------|-----|
| 서리 원석 | `FrostSpreadService.OreOf` → `seonge_ore` / `ice_root` / `cold_wave_ore` |
| 작물 zone | `CropRules` + `MainGameWorldDecorationRenderer` (심기·밴드 회복) |
| 까치 가이드 | `MagpieGuideRules` + Goal Badge 다음 목표 |
| 버섯 회복 | E/퀵슬롯 회복 아이템 · `mushroom_heal` 15/30/50 |

월드 타일·드롭·상호작용 아트를 바꿀 때 **ID 문자열**을 바꾸지 말 것.

### 2-4. 전투·보스·장비 (이미 MainGame 연결됨)

- 후기 보스·기믹·진화 무기·T4~T6 방어·터렛은 런타임 존재.  
- 아트/애니만 교체할 때 `BossCombatController` / `MeleeArcAttack` 계약·회귀를 깨지 말 것.  
- `TilemapRenderer` 스크립트명 = Unity 빌트인 충돌 경고 → **이름 rename은 A/B 합의 후**.

### 2-5. 부적

- `TalismanRuntime` 5종 (귀환·축지·이정표·은신·한기) — 사용/세이브 연결됨.  
- HUD에 잔여 시간 표시를 넣는다면 A 작업 후보 (로직은 B에 있음).

---

## 3. A 작업 후보 (B 잔여와 구분)

우선순위는 팀 합의에 따르되, **A 성격**인 것만:

1. **Play 폴리시** — 특성 UI 아트·사운드, 도감 카드 아트, 보스/요괴 프리팹·애니 교체  
2. **opening 일러 3장** — globals `opening_illust_pages=3` (컷신 아님, 넘기는 정지 화면) — 아트+셸 화면  
3. **부적 잔여시간 HUD** (선택)  
4. Title/MainGame 연출·로딩 폴리시  
5. `TilemapRenderer` rename (경고 제거, 합의 후)

**A가 하지 않는 것 (B·기획):**  
`day-curve-ext` 55~100 수치, accessories/globals/terrain-spawn **v70 대개편**, mineral CSV 밸런스 발명.

---

## 4. Play 스모크 체크리스트 (A)

- [ ] Title → 새 게임 → 특성 4개 선택 카드가 **화면 안**  
- [ ] 선택 후 진행 · 세이브 · 이어하기 시 특성 유지  
- [ ] 도감 열림 · Arial Error 없음 · 카드 17장 규모  
- [ ] 캣닢 수확/심기 · 까치 가이드(낮)  
- [ ] 지하 버섯 사용 회복  
- [ ] 보스/요괴 1종 조우 (아트 없어도 로직 OK)

---

## 5. 절대 규칙 (A도 동일)

1. 게임 시간 = `gameSeconds` / Tick에는 **증가량**.  
2. 파도·방어 min1·터렛 캡·낮 밝기 = **globals만**.  
3. CSV가 원본. SO ID·파일명 임의 변경 금지.  
4. 요괴/보스 처치 이벤트 **중복 Raise 금지** (B가 발행).  
5. Dev B 회귀 65/65 **삭제·약화 금지**.  
6. melee 특성 **보스 제외** 유지.

---

## 6. 주요 경로 (A가 자주 열 파일)

| 주제 | 경로 |
|------|------|
| 셸·특성 UI | `UI/MainGameShellUiController.cs`, `UI/GameShellController.cs` |
| 도감(통합) | `UI/MainGameCraftingUiController.cs` |
| 도감(레거시) | `UI/MainGameCodexController.cs` |
| 플레이어 | `World/MainGamePlayerController.cs` |
| 런타임 서비스 | `World/MainGameRuntimeServices.cs` |
| 특성 | `World/TraitRules.cs`, `World/TraitRuntime.cs` |
| 세이브 | `Save/SaveGame.cs` (schema 27) |
| 회귀 | `Editor/NyangbingoDevBIntegrationRegressionTests.cs` |

---

## 7. A용 복붙 프롬프트

```
Seokbinggo_Client 개발 A 이어서.
먼저 Seokbinggo_Client/DEV_A_CONTINUE_HANDOFF.md 를 읽고 main pull.
B가 넣은 특성 선택·도감17·서리/작물/까치/버섯/부적/후기보스를 깨지 말고
아트·UI 폴리시·opening 일러·Play 스모크 위주로 진행해.
schema 27, LegacyRuntime.ttf, melee 특성 보스 제외, Dev B 회귀 65/65 유지.
v70 CSV 대개편·day-curve-ext 수치는 B/기획 담당.
```

---

*개발 B 잔여(day-curve-ext, v70 버킷)는 이 문서에 상세히 적지 않는다. 디코 B 인수 메시지와 `content-status.csv`를 본다.*
