# 개발 B 잔여 — 인수 포인터 (2026-09-14)

개발 A 인수 → [`DEV_A_CONTINUE_HANDOFF.md`](./DEV_A_CONTINUE_HANDOFF.md)  
기준 브랜치: **`main` @ `63bbe32`** · repo `zhuki77/Seokbinggo-100Days`  
status: 구현됨 **410** / 추가예정 **231** / 수정예정 **220** (`content-status.csv`)

## 이번에 마감한 것 (이어받을 분 기준 · 재작업 불필요)

| 묶음 | 내용 |
|------|------|
| 악세 9 | 상자 6 런타임 유지 + 데모 보스 3 드롭 (`ssireum_knot` / `iron_appetite` / `yeouiju_shard`) |
| 지난 3건 | D-카운터 off · 이무기 결과 화면 · 난이도 비활성 자리 — status `구현됨` |
| 표시층 🟢 | **B-c** 저체온 아이콘 · **B-d** 도감 `[작성 대기` 뒷면 가드 |
| 그 전 B | frost T4–T6 · 작물/까치 · 특성 4 · mineral freshness · 후기 보스 등 |

회귀: **`Nyangbingo` → Run Dev B Integration Regression Tests → 67/67** (목표)

## 넘길 잔여 (우선순위)

1. **Play 스모크** — 회귀 67/67 재실행 + 보스 악세 드롭·저체온 아이콘·도감 가드 수동 확인  
2. **오너 대기** — `day-curve-ext` 55~100 수치 · 표시층 **B-a**(폭염 강조) / **B-b**(침대 스킵 팝업)  
3. **설계 대기** — **B-e** 3일차 이후 목표 안내 (지금 할 일 없음)  
4. **v70 대개편 버킷** — 후기 악세 17 · globals · terrain-spawn · crafting-tree · day-curve 1~30 등  
5. **소량** — early 보스 잔여 · yokai-stats · opening 일러(아트는 A)

## 기획 확정 답

`seokbinggo_s1`~`s6` = **모듈만** (`modules.csv`). `items.csv`에 넣지 않음.

## 커밋 계열

`e1b02bf` → `f06cfe9` → `907b99c` → `c1d9f19` → `28759b0` → **`63bbe32`** (악세 드롭·B-c/B-d)

## 인수 직후 주의

- `git pull` 후 컴파일 → 회귀 67/67  
- MineralTiers EOL / Title / TimeManager / packages-lock / `.cursor` / unity-mcp **커밋하지 말 것**  
- 하드코딩 금지(파도·방어 min1·터렛 캡·낮 밝기 = globals) · melee 특성은 보스 제외
