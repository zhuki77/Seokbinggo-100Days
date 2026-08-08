# V46 Continue 인수인계 — Notion 정본 정렬

작성일: 2026-08-08  
브랜치: `main`  
Unity: `6000.5.3f1`

## 정본 링크 (에이전트는 여기서 시작)

| 문서 | URL | 역할 |
|------|-----|------|
| 허브 | https://app.notion.com/p/5c2ae468d16b827aa979018a863a119c | 기획 루트 |
| **문서 지도 (v46)** | https://app.notion.com/p/97bae468d16b831db3a201caf3c81515 | 현행/폐기 단일 진입점 |
| **개발 명세서 ⑤ (v46)** | https://app.notion.com/p/8cfae468d16b8200b98d01b38164d4ad | 모듈 20종·밤 조합·CSV 델타 |
| 눈물 원장 재검산 v46 | https://app.notion.com/p/729ae468d16b82dc8f2c01fb685a90c7 | 경제 검산 |
| 로컬 갭(구 MVP) | `PLAN_VS_CODE_GAP_ANALYSIS.md` | 7/16 갭 → 8/8 재검증(P0~P2 닫힘). **100일 v46 범위는 아래 §2** |

> **규칙 (문서 지도)**: 현행에 없는 문서는 참고일 뿐. 문서와 CSV가 다르면 **CSV가 맞다.**  
> **코드**: 개발 A/B 파트 분리는 Notion상 파일 소유 표기일 뿐, 저장소는 **합쳐진 `main` 한 제품**.

---

## 0. 한 줄 요약

Notion **v46 = 100일 정식판 개발 명세서**이고, 현재 로컬 `Assets/Data/CSV`·런타임은 **대략 30일 MVP/데모 규모**에 가깝다.  
이전에 닫은 항목(새벽 180초·DawnAutoSave·밀폐 처방 C·문 개폐·Development 단축키)은 **MVP 층에서는 유효**하고, **P1 WaveNight·P2 석빙고/Smithy/터렛 슬롯**은 kit 없이 착수됨. **제품 진입 씬은 `Title` → `MainGame` 분리**. **v46 전량 CSV(P0)·DayLight(P3)·Frost/Evolution(P4)** 는 아직 대기.

---

## 1. 이번 라운드까지 로컬에서 끝낸 것 (MVP 층)

| 항목 | 상태 |
|------|------|
| DawnAutoSave / 새벽 경고 180초 / SealPercent 처방 C / OnNightStart / D-100 / 화이트리스트 | ✅ (갭 분석 §4 DONE) |
| 나무 피벗 배치 / 채굴 먹선 제거(B PR) / 풀 오버레이 제거 | ✅ |
| Development 빌드 J·F5 단축키 | ✅ 코드 · 미푸시 · Test 재빌드 확인 필요 |
| 단열 문 E 개폐 (`TryToggleInsulationDoor`) | ✅ |
| Dev B 회귀 27/27 (P1 WaveNight + P2 Seokbinggo) | ✅ 로컬 |

---

## 2. Notion v46 대비 — 로컬 실측 갭 (2026-08-08)

### 2-1. CSV 행수 (정본 문서 지도 vs `Assets/Data/CSV`)

| CSV | Notion v46 정본 | 로컬 실측(헤더 제외 대략) | 판정 |
|-----|-----------------|---------------------------|------|
| items.csv | 160 | 86 | ⬆️ 임포트 필요 |
| crafting-tree.csv | 90 | 54 | ⬆️ |
| bosses.csv | 10 | 4 | ⬆️ |
| accessories.csv | 26 | *(파일 없음/미반영)* | ⬆️ |
| globals.csv | 110~112 | 93 | ⬆️ |
| modules.csv | 11 (석빙고 s1~s6 포함) | **11** (기존 5 + s1~s6 수동) | ✅ 임시 · kit zip 오면 대조 |
| player-combat.csv | 18 | 7 | ⬆️ |
| drops.csv | 17 | 9 | ⬆️ |
| equipment.csv | 24 | 15 | ⬆️ |
| night-waves.csv | 15 (v45 신설) | **15** (Notion 표로 재구성, 2026-08-08) | ✅ 임시 · kit zip 오면 대조 |
| day-curve.csv | 30 | 30 | ✅ |
| seal-whitelist.csv | 23 | 23 | ✅ |
| yokai-stats.csv | 7 | 5 | ⬆️ 확인 |

> 리포에 Notion이 말하는 `kit/data/` 폴더는 **없음**. Unity CSV는 `Seokbinggo_Client/Assets/Data/CSV/`만 존재.

### 2-2. 모듈 20종 (명세서 ⑤)

| 모듈 | 담당(문서) | 로컬 |
|------|------------|------|
| `DayLight.intensityFor` / `assertInvariants` | A | 부분 — `MainGamePresentationController.DayLighting`만. globals `day_brightness_by_stage`·assert **없음** |
| `Seokbinggo.upgrade` (s1~s6) | A | ✅ `SeokbinggoUpgradeService` + Builtin/CSV materials. ice_core E 승급 |
| `Insulation.total` (3티어 합산) | A | 부분(기존 단열/벽지) — v36 3티어 합산식 미확인 |
| `Armor.effectiveDamage` (min 1) | A | 전투 경로 존재 가능 — globals `armor_def_by_tier` 미확인 |
| `Furniture.applyModifier` (죽부인·대발) | A | 죽부인은 냥잠 배율만; **대발·반경 가구 효과 미흡** |
| `Boss.dodgePhase` | A | 미확인/미완 |
| `Yokai.clubCrack` | A | 기존 벽 DPS 경로 있을 수 있음 — v46 수치 재검증 필요 |
| `Turret.canPlace` (슬롯=석빙고 단계) | A | ✅ Stage 캡 + `turret_damage_slot_cap`(3) |
| `WaveNight.*` (조합·큰밤·파도·배율) | B | ✅ P1 |
| `FrostSpread.*` / `BedrockLayer.unseal` | B | **없음** |
| `EvolutionCraft` / `Smithy.isUnlocked` | B | ✅ `IsUnlocked`(stage≥4) 게이트. EvolutionCraft 전량은 P4 |
| `GimmickWeapon.check` | B | **없음** |

### 2-3. 문서 지도 «열린 판단 3건»

| # | 안건 | 로컬 |
|---|------|------|
| ① | 아티팩트 20종 → equipment 스키마 | accessories/아티팩트 CSV·장착 경로 미완 |
| ② | `AccessoryTwo` 슬롯 | **코드/enum/UI는 있음** — 데이터·밸런스 완성도 별도 |
| ③ | T4~T6 세트 효과 | equipment 15행 수준 — T4~T6 미확정 |

---

## 3. 실행 큐 (순서 고정 · 2026-08-08 착수)

| 순번 | ID | 작업 | 상태 | 비고 |
|------|-----|------|------|------|
| 0 | **P0** | `kit/data` 21종 CSV 전량 → `Assets/Data/CSV` 재임포트 + SO/매니페스트 | ⏸ **보류** | zip 없이 진행. 나중에 전량 덮어쓰기 |
| 0a | P0a | `night-waves.csv` 15행 + WaveNight globals 키 | ✅ 착수 | Notion v45 표로 재구성. 전량 kit와 합치면 덮어쓰기 |
| 0b | P0b | `WaveNightRules` 순수 규칙 + Dev B 계약 | ✅ 착수 | 스폰 런타임 배선은 P1 |
| 1 | **P1** | Encounter에 WaveNight 배선 (빈 슬롯만·HP-only·큰밤) | ✅ kit 없이 착수 | day≥31 · CSV 없으면 Builtin 15행 |
| 2 | **P2** | 석빙고 s1~s6 + `Smithy.isUnlocked` + `turret_slot_cap` | ✅ 착수 | Builtin materials + CSV 수동 행. kit zip 오면 modules 덮어쓰기 |
| 3 | **P3** | `DayLight.intensityFor` + `assertInvariants` | 대기 | `day_brightness_by_stage` P0 의존 |
| 4 | **P4** | FrostSpread / Bedrock / EvolutionCraft / GimmickWeapon | 대기 | crafting·items P0 의존 |
| 5 | **P5** | 열린 판단 ①~③ (아티팩트 스키마·AccessoryTwo·T4~T6 세트) | 대기 | 오너 결정 |

하드코딩 금지 4곳(명세서): 파도 108초(`wave_advance_sec`) · 방어 min1 · 터렛 슬롯 상한 · 낮 밝기 곡선 → **전부 globals**.

### P0 해제 방법
1. 최신 `nyangbingo-kit-data-*.zip`(또는 `kit/data` 폴더)을 리포에 두거나 경로를 알려준다  
2. `Assets/Data/CSV`에 전량 복사 → Unity `Nyangbingo` 데이터 임포트 메뉴 실행  
3. 행수: items 160 · crafting-tree 90 · bosses 10 · accessories 26 · modules 11 · globals ~110~112 · night-waves 15 · equipment 24 · player-combat 18 · drops 17

---

## 4. Unity 체크리스트 (지금 당장)

### 회귀
- [ ] Nyangbingo → Run Dev B Integration Regression Tests → `27/27`
- [ ] (여유) Run Dev A Regression Tests

### 씬 분리 (Title / MainGame)
- [ ] 에디터 포커스 시 Title 씬 자동 생성, 또는 `Nyangbingo/Main Game/Create or Update Title Scene`
- [ ] Build Settings: **Title → MainGame**
- [ ] Play → Title만 → 새 게임 → MainGame
- [ ] Pause → 타이틀로 → Title 씬

### 플레이 (MVP 층)
- [ ] F5 / J (Editor 또는 Development Test 빌드)
- [ ] 단열 문 E 개폐
- [ ] 나무·채굴 검은 선

### v46 착수 전
- [ ] 최신 `kit/data` CSV를 어디서 받을지(노션 첨부/별도 zip) 확정
- [ ] 임포트 후 items/bosses/modules/globals/night-waves 행수 문서와 일치 확인

---

## 5. 푸시·잡음

- 단축키·문 개폐·문서 정렬은 로컬 변경분 — 사용자 요청 시만 `main` 푸시  
- URP/ProjectSettings 잡음은 커밋하지 않음  
