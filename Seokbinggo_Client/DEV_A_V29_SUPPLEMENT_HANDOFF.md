# 개발 A — v29 보충 명세(A-21~A-26) 인수인계

작성일: 2026-07-21  
대상: 「개발 A 추가 작업 명세서 — v29 통합 이후 보충」  
브랜치 작업 범위: Dev A 월드 계약만 (Dev B 파일 미수정)

---

## 0. 완료 여부 요약

| ID | 제목 | 상태 | 비고 |
|----|------|------|------|
| A-21 | 전경 물리 표면(Collider) | ✅ 기존 유지 + 좌표 계약 문서화 | `EnsureForegroundCollision` 이미 존재 |
| A-22 | 공용 안전 스폰 계약 | ✅ 신규 | `IWorldSafeSpawnResolver` / `TileService` / Session 노출 |
| A-23 | 배경벽·벽지 | ✅ 기존 유지 + 호환 정책 문서화 | A-16 구현 유지, `IBackgroundPlacementService`로 배치 계약 보강 |
| A-24 | 인수인계 | ✅ 본 문서 | |
| A-25 | 하단 팔레트용 배치 계약 | ✅ 신규 | `SupportsForegroundPlacement` / `CanPlaceForeground` / 배경 배치 인터페이스 |
| A-26 | 반경·밀폐 창 오버레이 | ✅ 신규 | `WorldRangeOverlayRenderer` |

회귀: `Nyangbingo/Run Dev A Regression Tests` — **16항목** (기존 13 + 공용 안전 스폰 / 전경·배경 배치 / 반경 오버레이).

---

## 1. 신규·변경 공용 인터페이스

위치: `Assets/Scripts/Nyangbingo/Core/WorldContracts.cs`

### A-22 `IWorldSafeSpawnResolver`

```csharp
public interface IWorldSafeSpawnResolver
{
    bool IsSafeStandingPosition(Vector2 worldPosition, float actorHalfExtent);
    bool TryResolveSafeSurfaceSpawn(int preferredCellX, float actorHalfExtent, out Vector2 worldPosition);
}
```

- 구현: `TileService`
- 세션 노출: `WorldSessionController.SafeSpawnResolver`

### A-25 `IBackgroundPlacementService` + `BackgroundCellState`

```csharp
public readonly struct BackgroundCellState { /* CurrentBackgroundId, NaturalBackgroundId, HasWallpaper, HasNaturalBackground */ }

public interface IBackgroundPlacementService
{
    bool CanPlaceWallpaper(Vector3Int cell);
    bool TryPlaceWallpaper(Vector3Int cell);
    bool TryRemoveWallpaper(Vector3Int cell);
    BackgroundCellState GetBackgroundState(Vector3Int cell);
}
```

- 구현: `TileService` (`TryPlaceWallpaper(cell, Inventory)` 오버로드로 인벤 원자 소비 지원)
- 세션 노출: `WorldSessionController.BackgroundPlacement`

### A-25 전경 배치 (TileService 공개 메서드)

```csharp
bool SupportsForegroundPlacement(string itemId);
bool CanPlaceForeground(Vector3Int cell, string itemId);
bool TryPlaceForeground(...); // PlacementHardness 등록 ID만 허용하도록 강화
```

정본: `PlacementHardness` 키. 기반암·얼음 제단·`bg_*`/`wallpaper`는 전경 재설치 불가.

### A-26 오버레이

```csharp
public enum WorldRangeShape { Circle, AxisAlignedRect }
public readonly struct WorldRangeOverlay { Vector2 Center; float Radius; float SecondaryRadius; WorldRangeShape Shape; }
public interface IWorldRangeOverlayRenderer
{
    void SetVisible(bool visible);
    void Render(IReadOnlyList<WorldRangeOverlay> overlays);
    void Clear();
}
```

- 구현: `Assets/Scripts/Nyangbingo/World/WorldRangeOverlayRenderer.cs` (MonoBehaviour)
- Circle: `Radius` = 타일 반경  
- Rect: `Radius` = halfX(`seal_window_rx`), `SecondaryRadius` = halfY(`seal_window_ry`, 0이면 Radius와 동일)
- 입력(`R`/팔레트)은 읽지 않음. Collider·Seal·저장 무부작용.

---

## 2. A-21 전경 Collider · 좌표 계약

### 컴포넌트 (`TilemapRenderer.EnsureForegroundCollision`)

| 대상 | 구성 |
|------|------|
| 전경 Tilemap GO | `TilemapCollider2D` (`usedByComposite=true`) + `CompositeCollider2D` (Polygons) + `Rigidbody2D` Static |
| 배경 Tilemap | Collider/Composite/Body **제거** |
| 먹선·장식 | Collider 없음 |

파괴·설치 성공 시 `NotifyForegroundCollisionDirty()`로 Composite 재합성.

### 좌표 계약 (플레이어·드랍 공통)

- 논리 셀 `(x, y)` 월드 AABB: **`[x, x+1] × [y, y+1]`** (Cell Size 1×1 가정)
- 셀 중심: `(x+0.5, y+0.5)`
- 스폰 발 위치: 발밑 고체 셀 `groundY`의 윗면 `y = groundY+1`, 액터 중심 `y = groundY+1+actorHalfExtent`
- Tile Anchor 기본 (0.5, 0.5). **물리 표면은 타일 점유 셀 경계**를 따르며 스프라이트 피벗에 의존하지 않음.

### 레이어·Material

현재 런타임 코드는 전경 GO의 기존 Layer를 유지한다. MainGame Physics Matrix·Material은 씬/프로젝트 설정을 따른다. 변경이 필요하면 Dev B가 배선 시 전경 Tilemap 레이어만 지정하면 된다.

---

## 3. A-23 배경 데이터 · `bg_*` / `t_bg_*` 호환

- `TileData.backgroundElementType` / `naturalBackgroundElementType` 분리 유지
- 런타임·저장 ID: `bg_dirt` / `bg_stone` / `bg_deep` / `wallpaper`
- 공식 ID 별칭: `t_bg_*` → `TileIdAlias.ToCanonical`으로 `bg_*` 정규화
- 저장: `SaveGame.backgroundChanges` (스키마 16). 배경 이력이 없는 구세이브도 로드 가능(전경만 재생)
- 벽지 = 비충돌·비밀폐 (`SealPercent` 불변). 도포율은 `IWallpaperCoverageSource`

---

## 4. 저장 스키마

- **스키마 16 유지**. 이번 작업으로 SaveGame 필드/스키마 버전을 올리지 않음.
- 개발 B 소유 저장 필드 보존.

---

## 5. 개발 B가 제거·교체할 임시 코드

| 파일 | 메서드/경로 | 교체 |
|------|-------------|------|
| `MainGamePlayerController.cs` | `MoveWithTileCollision` | 전경 CompositeCollider + Rigidbody2D 이동 |
| `MainGameWorldDropRuntime.cs` | `MoveWithTileCollision` | 동일 전경 물리 표면 |
| `MainGamePlayerController.cs` | `TryFindSafeSurfaceSpawn` | `session.SafeSpawnResolver.TryResolveSafeSurfaceSpawn` |
| `NyangbingoDemoSaveGenerator` | 직접 타일 순회 스폰 | 동일 `IWorldSafeSpawnResolver` |
| 팔레트 | (배선만) | `SupportsForegroundPlacement` / `IBackgroundPlacementService` |
| 반경 토글 / `R` | (배선만) | `IWorldRangeOverlayRenderer.Render` — 등불6·체4·해태8·밀폐창 rx/ry |

Dev A는 위 Dev B 파일을 **수정하지 않았다**.

---

## 6. 회귀 테스트

메뉴: **Nyangbingo / Run Dev A Regression Tests**

신규 3개:

1. 공용 안전 스폰 계약  
2. 전경·배경 배치 계약  
3. 반경·밀폐 창 오버레이  

전체 분모: **16**. 콘솔에 `Dev A 회귀 테스트 전체 통과 (16/16)` 가 나오면 성공.

---

## 7. 개발 B 통합 순서 (명세 A-24)

1. `MainGamePlayerController.MoveWithTileCollision` 제거  
2. `MainGameWorldDropRuntime.MoveWithTileCollision` 제거  
3. `TryFindSafeSurfaceSpawn`·데모 생성기 직접 순회 제거 → `SafeSpawnResolver`  
4. Rigidbody2D/Collider 기반 플레이어·드랍 배선  
5. 벽지 도포율 → 냉기원 지속시간 보너스  
6. 하단 팔레트 벽지 ↔ `IBackgroundPlacementService`  
7. 반경·밀폐 창 오버레이 ↔ 팔레트 토글·`R`  
8. Dev A 회귀 → Dev B 통합 회귀 → MainGame 새 게임·데모·저장·로드 → 제품 빌드  

---

## 8. 수동 검증 권장 (MainGame)

- 새 게임 / 1·15·30일 데모 스폰이 지표면 안전 위치  
- 흙·돌 채굴 후 인벤·팔레트 재설치  
- 채굴 구멍 낙하·남은 전경 비관통 (Collider 전환 후)  
- 반경 4/6/8·밀폐 창이 타일 중심에 정렬  
