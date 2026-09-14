# 개발 B 잔여 — v83 인수 포인터 (2026-09-15)

정본 지시서: 첨부 `개발B_인수_지시서_v83` (기획)  
기준: **`main` @ `4044c55`+** · repo `zhuki77/Seokbinggo-100Days`  
A 인수 → [`DEV_A_CONTINUE_HANDOFF.md`](./DEV_A_CONTINUE_HANDOFF.md)

## 지금 할 일 (v83 · T1→T2→T3)

| | 상태 | 내용 |
|---|---|---|
| **T1** | ⏳ Play 필요 | 스모크 체크리스트 — 실패 시 **고치기 전 회신** |
| **T2** | ✅ 코드 반영 | 회귀 `Run()` 개수 자동 집계 → 실행 시 **`68/68`** |
| **T3** | ✅ 채움 | [`냥빙고_개발B_globals연결대조_v83_채움.csv`](./냥빙고_개발B_globals연결대조_v83_채움.csv) — **코드 수정 없음** |

## ⛔ 손대지 말 것 (v83)

- v70 대개편 454행 데이터 입력 / `content-status` 일괄 수정
- `day-curve-ext` 55~100 · 서리광석 freq=0 · terrain 미구현 지형 가중치
- `altar_*` 가안 · B-a/B-b/B-e · opening 일러(A)

## 확정 답

`seokbinggo_s1~s6` = **modules만** · 결과 제목 **`이무기 격파`** · 버섯 15/30/50

## 인수 직후

```
git pull → Unity 컴파일 → Run Dev B Integration Regression Tests → Validate Product Data Freshness
```

잡음 커밋 금지: MineralTiers EOL · Title · TimeManager · packages-lock · `.cursor` · unity-mcp
