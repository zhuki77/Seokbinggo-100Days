# 기획 ↔ 코드 갭 분석 (v46)

> 갱신: 2026-08-08 · 개발 명세서 ⑤(v46) 기준. A/B 분담은 폐기되어 **모듈 20종 전체**가 작업 범위입니다.

## 진행 스냅샷

| 모듈 | 상태 |
|---|---|
| DayLight (낮 밝기·불변식) | 완료 (`DayLight.cs` + Presentation) |
| Armor.effectiveDamage min1 | 완료 (`ArmorRules` + RaidTarget, globals) |
| WaveNight / night-waves.csv | 완료 (31일+ Encounter, `wave_threshold_sec` globals only) |
| Seokbinggo 6단계 | 완료 (승급·세이브 `seokbinggoStage`·모듈 동기화·터렛 슬롯) |
| Furniture 죽부인/대발 | 완료 (CSV A + Aura + regen) |
| Turret 슬롯 상한 | 완료 (stage + `turret_damage_slot_cap`) |
| Boss.dodgePhase | 완료 (회피 무적) |
| Insulation.total | 완료 (패널 티어 합산 → 체온 회복 배율) |
| FrostSpread / BedrockLayer | 완료 (pending 마킹·lazy reveal·y136~139 unseal) |
| EvolutionCraft / Smithy | 완료 (진화 헬퍼 + stage≥4 대장간 UI 잠금) |
| GimmickWeapon | 완료 (지급 진행도·세이브·보스/백중 훅; 일부 Notify는 API) |
| Artifact 20종 | 완료 (accessories 26 + VerbCatalog + accessories→equipment 임포트) |
| CSV kit 행수 | 완료 (items160/bosses10/acc26/craft90/eq24/drops17/combat18 + manifest) |

## MVP~v34 코어 (유지)

- 900/540, 새벽 180초, D-100, DawnAutoSave, Seal 처방 C, 인벤 50, 객귀/이무기/강철이 재배치 등

## Unity에서 남은 수동 1회

1. `Nyangbingo/Reimport v34 Data Bundle` (메뉴명 유지, 내부는 v46 카운트) → SO·카탈로그 재생성
2. `Nyangbingo/Run v46 Regression Tests`
3. `Nyangbingo/Validate Product Data Freshness`

## 열린 기획 판단 (문서 지도 §5 — 코드 외)

1. 아티팩트 전투 수치 0·동사만 — VerbCatalog로 훅만 준비, 동사 풀구현은 후속
2. AccessoryTwo 슬롯 — 코드/장비 UI에 2칸 공유 경로 있음
3. T4~T6 세트 효과 — equipment setId 미정(none)
