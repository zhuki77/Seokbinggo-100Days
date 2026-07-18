# v24.1 로드맵 1–14 최종 감사

- 감사일: 2026-07-18
- 작업 경로: `D:\workspace\GitHub\Seokbinggo-100Days\Seokbinggo_Client`
- 브랜치: `feat/taeyo-v23-data-alignment`
- 공식 데이터: `planner_v24_1_2230`
- Unity: `6000.5.3f1`

## 결론

로드맵의 시스템 구현과 자동 검증 기반은 대부분 완료되었지만, 현재 상태를 **14개 항목 전체가 실제 게임에 완전히 적용된 완성 상태**라고 볼 수는 없다.

- DevBTest 기준: 최신 세션의 108개 성공 신호, 실패/예외 없음
- 스크립트 컴파일 기준: 오류 0건, 기존 Dev-A API 폐기 예정 경고 2건
- 공식 CSV는 16종이 아니라 **17종**이다.
- Unity CSV 자동 대조기는 정상 동작하며 공식 17종, Unity 17종, 의미 매핑 15쌍을 검사한다.
- 메인 빌드 씬은 아직 `SampleScene` 한 개뿐이며, 새 게임 셸·오디오 서비스·도감 표현은 씬/프리팹에 배치되지 않았다.

## 항목별 판정

| 번호 | 항목 | 판정 | 근거 및 남은 일 |
|---:|---|---|---|
| 1 | CSV 목록·해시·행 수 | 완료 | 대조기가 공식 17종 전체의 행 수와 SHA-256을 출력한다. 기존의 “16종” 표현은 17종으로 정정한다. |
| 2 | 현재 Unity CSV 자동 대조 | 도구 완료 / MVP 정합성 완료 | 15개 의미 매핑과 공식 전용/Unity 전용 파일을 자동 보고한다. 미매핑 2종은 모두 31일 이후 확장 범위다. |
| 3 | 폐기·변경·신규 ID | 완료 | `V24_1_ID_ALIGNMENT_REPORT.md`와 저장 마이그레이션 코드로 정리·검증했다. |
| 4 | 작은 데이터 묶음부터 적용 | A/MVP 완료 | 공식 A 범위 아이템 22개와 레시피 27개를 추가해 카탈로그 83/49개를 구성했고 DevBTest를 통과했다. B 범위는 후속 범위로 유지한다. |
| 5 | 용광로 확정값 | 완료 | `blast_furnace` 제작 재료 `stone:20`, `clay:12`, `iron_ore:6`, 제작 시간 45초가 반영됐다. |
| 6 | 방어구 9종·액세서리 6종 | 완료 | `equipment.csv` 15행과 생성 SO, 스탯·저장 테스트가 존재한다. |
| 7 | 상자 풀·보너스 자원 | 완료 | 상자 4종과 20개 배치, 액세서리 풀·보너스 자원 흐름을 DevBTest로 검증했다. |
| 8 | 합죽선·철선 | 완료 | `hapjukseon`, `cheolseon` ID, 제작·전투·유틸리티·저장 마이그레이션을 검증했다. `folding_fan`은 레거시 마이그레이션/테스트에서만 유지한다. |
| 9 | 무기·발톱 파이프라인 | 완료 | 공식 7행 `player-combat.csv`, 생성 프로필, 공격 계약 테스트가 일치한다. |
| 10 | 보스 확장 CSV | 완료 | 보스 4종의 확장 필드와 드롭 매핑이 의미상 불일치 0건이다. |
| 11 | 요괴 spawn track·새벽 도주 | 완료 | 요괴 5종 데이터 의미상 불일치 0건이며 raid/resident·dawn-flee 테스트가 통과했다. |
| 12 | 백중 대기열 | 완료 | 총 12마리, 상한 초과 대기열, 새벽 폐기·일반 스폰 중지/재개를 검증했다. |
| 13 | 보스 저장 정책 마이그레이션 | 완료 | 전투 중 수동 저장 차단, 활성 보스 미저장, 새벽 자동 저장 예외와 레거시 마이그레이션을 검증했다. |
| 14 | 도감·사운드·게임 셸 | 코드 계약 완료 / 제품 통합 미완료 | 도감 8카드, 오디오 라우팅, 타이틀·일시정지·설정·결과 흐름은 DevBTest 통과. 실제 UI 프리팹/Canvas, 오디오 클립·Mixer, 데모 세이브가 없다. |

## 최신 공식-Unity CSV 비교 결과

### 의미상 일치한 매핑

- `accessories.csv -> equipment.csv`: 공식 액세서리 6종 일치, Unity 방어구 9종은 의도된 추가 행
- `bosses.csv -> bosses.csv`: 4/4, 불일치 0
- `chests.csv -> chests.csv`: 4/4, 불일치 0
- `drops.csv -> drops.csv`: 9/9, 불일치 0
- `globals.csv -> globals.csv`: 80/80, 불일치 0
- `player-combat.csv -> player-combat.csv`: 7/7, 불일치 0
- `smelting.csv -> smelting.csv`: 3/3, 불일치 0
- `yokai-stats.csv -> yokai-stats.csv`: 5/5, 불일치 0

### 남은 데이터 차이

- `crafting-tree.csv -> recipes.csv`: 공식 53행 / Unity 49행, A 범위 누락 0개, B 범위 4개만 제외
- `items.csv -> items.csv`: 공식 85행 / Unity 83행, A 범위 누락 0개, B 범위 2개만 제외
- `modules.csv -> modules.csv`: 공식 5행 / Unity 5행, 불일치 0개. SO·카탈로그·DevBTest까지 연결 완료
- `mineral-tiers.csv -> mineral-tiers.csv`: 공식 12행 / Unity 12행, 불일치 0개. 발톱별 채굴 시간·게이트·광맥 빈도 검증 완료
- `seal-whitelist.csv -> seal-whitelist.csv`: 공식 22행 / Unity 22행, 불일치 0개. SO·카탈로그·인공 경계 SealSystem·DevBTest까지 연결 완료
- `id-migration.csv -> id-migration.csv`: 공식 27행 / Unity 27행, 불일치 0개. 런타임 매니페스트가 26개 rename과 여우비 부적 삭제·환불을 SaveGame에 직접 공급한다.
- `day-curve.csv -> day-curve.csv`: 공식 30행 / Unity 30행, 불일치 0개. 30일 스폰 조합·상한·더위·권장 진척·이벤트 ID를 카탈로그와 DayNightService에 연결했다.
- `globals.csv -> globals.csv`: 공식 80행 / Unity 80행, 불일치 0개. 타입 계약·카탈로그·DayNightService에 연결했고 첫날 밤 540초 경계를 검증했다.
- 감사 중 발견된 `ice_anvil` 설비 불일치는 공식 `blast_furnace`에 대응하는 `Foundry`로 수정했고, 재대조 결과 의미상 불일치 0건을 확인했다.

### 아직 공식 CSV와 직접 매핑되지 않은 2종

- `crafting-ext.csv`: 31일 이후 확장 범위
- `day-curve-ext.csv`: 31일 이후 확장 범위

Unity 전용 파이프라인 파일은 `day-events.csv`, `utilities.csv` 2종이다.

## 제품 통합 관점의 미완료 항목

### P0 — 메인 플레이 연결

1. 타이틀/일시정지/설정/결과 Canvas와 도감 UI를 실제 씬 또는 프리팹에 배치한다.
2. `NyangbingoAudioService`를 부트스트랩 씬에 배치하고 실제 AudioMixer·클립을 연결한다.
3. 실제 플레이 씬을 Build Settings에 추가한다. 현재는 `Assets/Scenes/SampleScene.unity`만 등록되어 있다.

### P1 — 콘텐츠 자산·배포 준비

1. 실제 BGM/SFX 자산, 2-bus AudioMixer, 라이선스 출처를 `audio/CREDITS.md`에 기록한다.
2. `Assets/StreamingAssets`에 1일·15일·30일 데모 세이브를 제공한다.
3. 타이틀·결과·도감 카드의 최종 아트와 애니메이션을 연결한다.
4. 공식 B 범위 아이템/레시피와 31일 이후 확장 CSV는 MVP 이후 범위로 분리 추적한다.

## 최신 검증 결과와 다음 순서

1. Unity 재가져오기 완료: 아이템 83개, 레시피 49개
2. 공식 CSV 재대조 완료: 공식 17종, Unity 17종, 15개 매핑 의미상 불일치 0건
3. DevBTest 완료: 성공 신호 108개, 실패·예외 0개
4. `modules.csv` 완료: 공식 5/5, 카탈로그 5개, 의미상 불일치 0건
5. `mineral-tiers.csv` 완료: 공식 12/12, 카탈로그 12개, 의미상 불일치 0건
6. `seal-whitelist.csv` 완료: 공식 22/22, 카탈로그 22개, 인공 차열벽·지붕·문 경계 판정과 비경계 설치물 검증 완료
7. `id-migration.csv` 완료: 공식 27/27, 카탈로그·런타임 매니페스트 27개, 저장 rename·충돌 병합·삭제 환불 검증 완료
8. `day-curve.csv` 완료: 공식 30/30, 카탈로그 30개, 15일 백중·30일 강철이 보스 이벤트와 현재 일차 바인딩 검증 완료
9. `globals.csv` 완료: 공식 80/80, 카탈로그 80개, 타입 계약과 540초 첫날 밤 런타임 바인딩 검증 완료
10. 공식 MVP CSV 직접 매핑 완료. 남은 `crafting-ext.csv`, `day-curve-ext.csv`는 31일 이후 범위다.
