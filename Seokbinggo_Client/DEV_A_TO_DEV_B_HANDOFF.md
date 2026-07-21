# 개발 A → 개발 B 인수인계

작성일: 2026-07-21  
브랜치: `feat/deva-world-spec-sync`  
기준 커밋:
- `3ce99a2` — Dev A 월드 스펙 동기화 + v29 보충 계약(A-21~A-26)
- `c38336b` — MainGame 채굴 타깃 수정(마우스 칸·사거리 정합)

관련 문서:
- `DEV_A_WORLD_SPEC_SYNC_HANDOFF.md` — A-10~A-20 / v27 동굴
- `DEV_A_V29_SUPPLEMENT_HANDOFF.md` — A-21~A-26 계약 상세

Unity: `6000.5.3f1` (`ProjectSettings/ProjectVersion.txt`)

---

## 0. 한 줄 요약

개발 A 월드·타일·스폰·배치 **계약과 회귀는 완료**되었습니다.  
MainGame에서 **흙·돌 채굴은 확인됨**.  
**흙·돌 재설치 팔레트 / Collider 전환 이동 / 반경·밀폐 창 입력 / 전투**는 개발 B 통합 범위입니다.

---

## 1. 개발 A 완료 범위

### 소유 영역 (이번 브랜치)

- `WorldGenerationConfig`, `MapGenerator`, `WorldGenerationResult`
- `TileData`, `TileService`, `TilemapRenderer`
- 전경/배경 Tilemap·Collider 구성 (`EnsureForegroundCollision`)
- 배경·벽지 상태 및 월드 저장/복원 (`SaveGame.backgroundChanges`)
- 안전 스폰 판정·해결 (`IWorldSafeSpawnResolver`)
- 전경/배경 배치 가능 판정·원자적 설치 (`SupportsForegroundPlacement` / `IBackgroundPlacementService`)
- 반경·밀폐 창 월드 오버레이 렌더러 (`WorldRangeOverlayRenderer`) — **렌더 API만**, 입력 미연결
- `NyangbingoDevARegressionTests` (**18/18**)

### 회귀

메뉴: **Nyangbingo / Run Dev A Regression Tests**  
성공 로그: `Dev A 회귀 테스트 전체 통과 (18/18)`

### 예외적으로 건드린 Dev B 파일

| 파일 | 이유 |
|------|------|
| `MainGamePlayerController.cs` | MainGame 수동 검증 중 채굴 타깃 버그 수정 (`c38336b`) + v7 플레이어 점프·중력 globals 연동 |

수정 요지:
- 채굴 사거리 1.1 → **1.5** (공격 사거리와 정합)
- **마우스 아래 칸**이 사거리 안이면 그 칸 우선 채굴
- 채굴 진행에 `Time.deltaTime` 사용 (DayNight TimeScale과 분리)

**v7 플레이어 물리(점프·중력)** — 정본 미정 항목을 Dev A가 globals 가안으로 등재:
- `PlayerMovementPhysics.cs` — 중력·가변 점프 컷·시뮬레이션
- `globals.csv` 4키: `player_jump_height_tiles`(3.5), `player_gravity`(32), `player_max_fall_speed`(12), `player_jump_cut`(0.5)
- 테라리아급 체감: 풀 점프 약 **3.5타일**, 점프 키를 빨리 떼면 낮은 점프 (`FixedUpdate` 매 틱 컷)
- 입력: `W` / `↑` / `Space` (누르고 있으면 상승 유지, 떼면 컷)
- 스폰 옆 입구 깊이: `WorldGenerationConfig.spawnEntranceDepthTiles` = **3** (점프 3.5보다 얕게 — 낙하 후 탈출 가능)

전투·요괴 피격·인벤 UI 등은 개발 B 영역으로 남깁니다.

---

## 2. MainGame 수동 검증 결과 (개발 A)

| 항목 | 결과 | 비고 |
|------|------|------|
| 새 게임 / 데모 지표면 스폰 | 확인 권장 유지 | 타이틀 → 새 게임 / 1·15·30일 데모 |
| 흙·돌 채굴 → 인벤 획득 | ✅ 확인 | 마우스 타일 위 좌클릭 **유지** ~1초 |
| 점프 높이·가변 점프 | 확인 권장 | `W`/`Space` — 약 3.5타일·짧게 누르면 낮게 |
| 흙·돌 팔레트 재설치 | ❌ 미배선 | 우클릭 ≠ 재설치 (아래 §3) |
| Collider 전환 후 낙하·비관통 | 보류 | 아직 `MoveWithTileCollision` 사용 중 |
| 반경 4/6/8·밀폐 창 정렬 | 보류 | `R`/팔레트 ↔ `IWorldRangeOverlayRenderer` 미연결 |
| 지상 요괴 피격 | 수정함 | `hits`=맞은 대상 수. 요괴에 Kinematic RB+트리거 허트박스, OverlapBox `useTriggers` 명시 |

---

## 3. 조작 참고 (지금 MainGame 기준)

| 입력 | 실제 동작 |
|------|-----------|
| 좌클릭 유지 | 공격 + (벽에 허공 휘두르기 시) 채굴. **지표**는 마우스가 공기 칸이어도 발밑 돌·흙으로 보정 |
| `W` / `↑` / `Space` | **점프** (키 누르는 동안 상승·떼면 낮은 점프) |
| 우클릭 | **미개봉 상자 개봉**(마우스/인접) → 없으면 **부채 액티브**. **타일 재설치 아님** |
| `E` | 근처 상자·설치물 상호작용 (상자 개봉 포함) |
| 제작 UI → 설치형 레시피 → E | 건물/설비 **설치 미리보기** (흙·돌 전경 재설치와 별개) |

흙·돌 전경 재설치는 개발 A가 `TileService` 계약을 준비해 두었고, **하단 팔레트 UI는 개발 B가 연결**해야 합니다.

---

## 4. 개발 B가 이어받을 계약 (복붙용)

### 안전 스폰

```csharp
session.SafeSpawnResolver.TryResolveSafeSurfaceSpawn(preferredCellX, actorHalfExtent, out var worldPos);
```

교체 대상: `MainGamePlayerController.TryFindSafeSurfaceSpawn` 직접 순회, 데모 세이브 생성기 직접 타일 순회.

### 전경 재설치 (흙·돌 등)

```csharp
tiles.SupportsForegroundPlacement(itemId);
tiles.CanPlaceForeground(cell, itemId);
tiles.TryPlaceForeground(cell, itemId, inventory); // 인벤 원자 소비 오버로드 있음
```

정본 키: `TileService` 내부 `PlacementHardness`  
불가: 기반암·얼음 제단·`bg_*` / `wallpaper`(전경 슬롯으로 설치 금지)

### 배경·벽지

```csharp
session.BackgroundPlacement.CanPlaceWallpaper(cell);
session.BackgroundPlacement.TryPlaceWallpaper(cell);
session.BackgroundPlacement.TryRemoveWallpaper(cell);
session.BackgroundPlacement.GetBackgroundState(cell);
```

벽지 ≠ 밀폐. 도포율은 `session.WallpaperCoverage` (`IWallpaperCoverageSource`).

### 반경·밀폐 창 오버레이

```csharp
// WorldRangeOverlayRenderer (IWorldRangeOverlayRenderer)
renderer.SetVisible(true);
renderer.Render(overlays); // Circle: 등불6·체4·해태8 / Rect: seal_window_rx·ry
renderer.Clear();
```

중심은 **타일 중심** `(cell+0.5, cell+0.5)` 권장. 입력 키/`R`은 개발 B가 붙입니다.

### 전경 Collider (이미 월드 쪽 구성됨)

- `TilemapRenderer.EnsureForegroundCollision`
- 논리 셀 AABB: `[x,x+1]×[y,y+1]`, 중심 `(x+0.5,y+0.5)`
- 타일 파괴/설치 후 `NotifyForegroundCollisionDirty()`

플레이어·드랍은 `MoveWithTileCollision` 제거 후 Rigidbody2D + 전경 Composite 표면으로 전환.

---

## 5. 개발 B 통합 체크리스트

1. [ ] `MoveWithTileCollision` 제거 (플레이어·월드 드랍)
2. [ ] 스폰 → `IWorldSafeSpawnResolver`
3. [ ] 하단 팔레트: 흙·돌 ↔ `TryPlaceForeground` / 벽지 ↔ `IBackgroundPlacementService`
4. [ ] 반경·밀폐 창 ↔ `IWorldRangeOverlayRenderer` (`R`/팔레트 토글)
5. [ ] 벽지 도포율 → 냉기원 지속시간 보너스
6. [ ] Dev A 회귀 18/18 → Dev B 통합 회귀 → MainGame 새 게임·데모·저장/로드 → 제품 빌드
7. [ ] (선택) 채굴 타깃 수정(`c38336b`)과 팔레트 배치 UX가 충돌하지 않는지 확인

---

## 6. 수동 검증 제출 양식 (개발 B 완료 후)

```text
MainGame 수동 검증 (날짜 / 브랜치 / 커밋)

1. 스폰: 새게임  / 1일  / 15일  / 30일  
2. 채굴·재설치(팔레트):  
3. Collider 낙하·비관통:  
4. 반경·밀폐 창 정렬:  

메모:
```

---

## 7. 문서·브랜치 위치

| 항목 | 값 |
|------|-----|
| 원격 | `https://github.com/zhuki77/Seokbinggo-100Days` |
| 브랜치 | `feat/deva-world-spec-sync` |
| 계약 상세 | `DEV_A_V29_SUPPLEMENT_HANDOFF.md` |
| A-10~A-20 / 동굴 | `DEV_A_WORLD_SPEC_SYNC_HANDOFF.md` |
| 본 인수인계 | `DEV_A_TO_DEV_B_HANDOFF.md` |

질문·불일치 시 개발 A 회귀 18항목을 먼저 재실행한 뒤, 실패 로그와 함께 공유해 주세요.
