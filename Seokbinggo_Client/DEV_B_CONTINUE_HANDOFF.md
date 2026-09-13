# 개발 B 잔여 — 짧은 포인터 (2026-09-13)

상세 **개발 A 인수**는 → [`DEV_A_CONTINUE_HANDOFF.md`](./DEV_A_CONTINUE_HANDOFF.md)  
**개발 B 잔여·완료 요약**은 팀 디코 인수 메시지를 정본으로 쓴다.

## 지금 우선순위 (기획 v82 회신 — 순서 유지)

1. **악세사리 9종** (상자 6 + 데모 보스 3) — `accessories.csv` 중 `magpie_bell`만 구현됨, 나머지 수정예정
2. **지난 전달서 3건** — D-카운터 · 엔딩 화면 · 난이도 자리
3. **스모크 테스트**
4. *(그 다음)* 표시 층 제안 v82 — 아래

## 표시 층 제안 v82 (지시 아님 · 우선순위 뒤)

| ID | 내용 | 승인 | B 액션 |
|----|------|:----:|--------|
| B-c | 저체온 원인 아이콘 (`RoomTempPresentation` 재사용) | 🟢 | 위 1~3 끝난 뒤 OK |
| B-d | 도감 빈 문구(`[작성 대기`) 표시 가드 — imugi 포함 5종 | 🟢 | 위 1~3 끝난 뒤 OK |
| B-a | 폭염 단계 상승 1회 강조 연출 (무텍스트) | 🟡 | 오너 승인 대기 |
| B-b | 침대 스킵 확인 팝업 (아이콘·숫자) | 🟡 | 오너 승인 대기 |
| B-e | 3일차 이후 목표 안내 | 🔴 | 설계 논의 — 지금 할 일 없음 |

정본 키트: `nyangbingo-kit-data-v82.zip` · `verify.py` 통과분.

## 기획 대기 답 — `items.csv` vs `seokbinggo_s1`~`s6`

**한 줄:** `seokbinggo_s1`~`s6`는 **인벤토리 아이템이 아니라** 석빙고 단계 모듈이다 → 정본/유니티 모두 **`modules.csv`만** 쓰고 `items.csv`에 넣지 않는다.  
(현재 클라 `items.csv`에도 해당 6행 없음 · `modules.csv` + Modules SO만 존재.)

## B 잔여 (한줄)

| 우선 | 내용 |
|------|------|
| 오너 | `day-curve-ext` 55~100 수치 확정 |
| 대개편 | globals / terrain-spawn 70 / accessories / crafting-tree / day-curve 1~30 |
| 소량 | early 보스 3, yokai-stats, opening 일러(아트는 A), items 아티팩트·재료 |
| status | `content-status.csv` 정본 |

## B 완료 후 push된 커밋 (참고)

- `e1b02bf` frost·작물·까치  
- `f06cfe9` mineral freshness·버섯 status  
- `907b99c` 시작 특성·UI·A 인수 문서  
- `c1d9f19` A/B 인수 문서 분리  

회귀: **65/65** · schema **27**
