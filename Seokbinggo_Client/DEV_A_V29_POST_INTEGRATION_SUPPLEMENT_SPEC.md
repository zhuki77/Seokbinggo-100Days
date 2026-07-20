# 개발 A 추가 작업 명세서 — v29 통합 이후 보충

작성일: 2026-07-21
적용 대상: `DEV_A_V26_SUPPLEMENT_SPEC.md`의 A-16~A-20 이후
목적: 2026-07-20~21 MainGame 통합과 회귀 검증 중 새로 확인된 월드 측 후속 계약만 전달

이 문서는 기존 v26 추가 명세를 대체하지 않습니다. 기존 문서와 충돌하는 정본·데이터 수치와 아래에서 명시적으로 수정한 범위에만 이 문서를 우선합니다.

---

## 0. 정본·데이터·현재 검증 상태 갱신

### 0.1 정본 우선순위 수정

기존 문서의 v26 정본 기준을 다음으로 갱신합니다.

1. 최신 전체 기획서 `ExportBlock-26_07_20_23_20`
2. 공식 `nyangbingo-kit-data-v27` CSV
3. `ExportBlock-v28v29`, `ExportBlock-v29`의 수동 반영 지시
4. 이전 버전에서 명시적으로 유지된 규칙

현재 프로젝트에는 v29 기준 **86개 아이템, 54개 제작법, 89개 전역값, 23개 밀폐 규칙**이 반영되어 있습니다. 주요 저장 규격은 다음과 같습니다.

- `SaveGame.CurrentSchemaVersion = 16`
- 플레이어 소지품 50슬롯(10×5)
- 장독 보관함 40슬롯
- 기존 12슬롯 세이브는 50슬롯으로 패딩하는 호환 정책 유지

개발 A는 과거 문서의 스키마 6, 소지품 12슬롯, v26 전역값 83개를 현재 정본으로 되돌리거나 생성 SO로 덮어쓰면 안 됩니다. 월드 저장 필드를 변경할 때도 개발 B 소유 저장 필드를 보존해야 합니다.

### 0.2 현재 회귀 기준

2026-07-21 기준 확인 결과:

- `Nyangbingo/Run Dev A Regression Tests`: 9/9 통과
- `Nyangbingo/Run Dev B Integration Regression Tests`: 4/4 통과
- MainGame 새 게임·UI·저장·이어하기 수동 회귀 통과
- 런타임·에디터 C# 빌드 경고/오류 0

현재 9/9 성공은 기존 월드 생성·상자·타일 복원·밀폐·시간·저장 왕복·지층·먹선 검사를 의미합니다. 아래 배경벽 저장, 정식 Tilemap Collider, 안전 스폰 검사는 아직 9개 항목에 포함되어 있지 않으므로 별도 완료 근거가 아닙니다.

---

## A-21. 전경 물리 표면을 단일 정본으로 제공

### 기존 A-17에 추가하는 현재 임시 상태

플레이어뿐 아니라 월드 드랍도 정식 Tilemap Collider가 없어 `TileService.GetTile`을 직접 조회하는 임시 충돌을 사용합니다.

- 플레이어: `MainGamePlayerController.MoveWithTileCollision`
- 월드 드랍: `MainGameWorldDropRuntime.MoveWithTileCollision`

두 구현 모두 논리 셀을 `[x, x+1] × [y, y+1]`로 가정합니다. 실제 Tilemap의 원점·셀 중심·타일 앵커·스프라이트 피벗과 이 가정이 어긋나면 아이템이 지표면에 조금 묻히거나 떠 보입니다. 지표면 아래로 끝없이 떨어지던 문제는 임시 타일 질의로 막았지만, 화면과 물리 표면의 미세한 불일치는 남아 있습니다.

### 요구사항

1. 전경 Tilemap에 `TilemapCollider2D`를 구성하고 400×160 월드에 적합한 `CompositeCollider2D` + 정적 `Rigidbody2D` 조합을 제공합니다.
2. 배경 Tilemap과 먹선·장식 Tilemap에는 Collider를 붙이지 않습니다.
3. 다음 좌표 계약을 명시적으로 고정합니다.
   - 논리 셀 `(x, y)`의 월드 경계와 중심
   - Grid/Tilemap 원점과 Cell Size
   - Tile Anchor
   - 전경 Collider 표면의 월드 좌표
4. `TileService.TryBreakForeground`와 `TryPlaceForeground` 성공 프레임에 데이터·화면·Collider가 함께 갱신되어야 합니다.
5. 월드 전체 Collider를 매번 재생성하지 않고 변경 셀에 필요한 범위만 갱신합니다.
6. 저장/로드 후 복원된 전경과 Collider가 완전히 일치해야 합니다.
7. 플레이어와 월드 드랍이 동일한 전경 물리 표면을 사용해야 합니다. 서로 다른 셀 반올림 규칙이나 별도 지표면 높이 보정을 두면 안 됩니다.
8. 타일 아트의 캔버스 크기나 피벗이 바뀌어도 물리 표면이 달라지면 안 됩니다.

### 개발 B 인계 경계

개발 A는 다음 파일의 임시 이동 코드를 직접 삭제하거나 수정하지 않습니다.

- `Assets/Scripts/Nyangbingo/World/MainGamePlayerController.cs`
- `Assets/Scripts/Nyangbingo/World/MainGameWorldDropRuntime.cs`

대신 다음을 전달합니다.

- MainGame에 필요한 Collider 컴포넌트와 설정값
- 전경 Collider가 준비되었는지 확인할 수 있는 계약 또는 배선 방법
- 레이어·Physics Material·충돌 매트릭스
- 좌표 계약과 마이그레이션 주의점

개발 B가 통합 시 두 임시 `MoveWithTileCollision` 경로를 제거하고 Rigidbody2D 기반 이동·낙하로 전환합니다.

### 회귀 테스트

1. 지표면·계단·지하 바닥에서 플레이어 발과 아이템 하단이 같은 표면에 정렬
2. 아이템이 바닥에 묻히거나 공중에 뜨거나 정지 후 혼자 미끄러지지 않음
3. 여러 보상을 한 위치에서 퍼뜨려도 모두 가장 가까운 실제 전경 위에 안착
4. 채굴한 구멍으로는 떨어지고, 남아 있는 전경을 관통하지 않음
5. 타일 파괴·설치 직후 유령 Collider 또는 보이지 않는 바닥이 남지 않음
6. 저장·로드 뒤 동일 위치에서 같은 충돌 결과

---

## A-22. 안전 스폰을 월드 공용 계약으로 확장

기존 A-18의 “새 게임 안전 지표면 스폰” 요구는 아직 완료되지 않았습니다. 현재도 다음 개발 B 임시 경로가 사용됩니다.

- 새 게임: `MainGamePlayerController.TryFindTemporarySafeSurfaceSpawn`
- 데모 세이브 생성·검증: `NyangbingoDemoSaveGenerator`가 같은 임시 함수를 호출

### 추가 요구사항

1. `WorldGenerationResult.spawnPoint` 자체가 기존 A-18 안전 조건을 만족해야 합니다.
2. 월드 측에 임의 좌표가 현재 전경 기준으로 안전한지 검사하고, 필요하면 결정론적인 대체 지표면을 반환하는 공용 계약을 제공합니다.
3. 이 공용 계약은 다음 경우에 함께 사용할 수 있어야 합니다.
   - 새 게임
   - 1일·15일·30일 데모 세이브 생성
   - 구버전 또는 손상 직전 세이브의 플레이어 위치가 고체 내부·낙하 구멍·월드 밖인 경우
4. 저장된 정상 플레이어 위치는 임의로 재배치하지 않습니다. 안전성 검증 실패 때만 대체 위치를 사용합니다.
5. 같은 seed와 같은 입력 좌표는 항상 같은 결과를 반환해야 합니다.
6. 중앙의 큰 수직 구멍이 의도된 월드 지형이더라도 최초 스폰과 데모 스폰은 그 낙하 경로를 피해야 합니다.

### 권장 계약 예시

실제 이름은 달라도 되지만 같은 정보를 제공해야 합니다.

```csharp
public interface IWorldSafeSpawnResolver
{
    bool IsSafeStandingPosition(Vector2 worldPosition, float actorHalfExtent);
    bool TryResolveSafeSurfaceSpawn(int preferredCellX, float actorHalfExtent, out Vector2 worldPosition);
}
```

개발 A가 계약과 구현을 제공하면 개발 B가 `TryFindTemporarySafeSurfaceSpawn` 및 데모 생성기의 직접 타일 순회 의존성을 제거합니다.

### 회귀 테스트

1. 고정 seed 목록과 최소 100개 연속 seed에서 생성 `spawnPoint` 안전 조건 충족
2. 1일·15일·30일 데모 저장 위치가 모두 공용 안전 판정 통과
3. 월드 밖·고체 내부·수직 구멍 위 입력은 안전 위치로 교정
4. 정상 저장 위치는 교정하지 않음
5. 동일 seed 결정론 유지

---

## A-23. 배경벽·벽지 구현 상태 명확화

기존 A-16은 완료로 간주하면 안 됩니다. 현재 프로젝트에는 전경/배경 Tilemap 렌더링과 `bg_dirt`·`bg_stone`·`bg_deep` 아트 연결은 있으나, 다음 정식 런타임 계약은 확인되지 않습니다.

- 자연 배경과 플레이어 벽지를 구분하는 독립 저장 상태
- 벽지 설치·제거와 원래 배경 복원
- 100% 도포율 계산 공급자
- 배경 변경 이력의 원자적 저장·로드

현재 `TileData`는 단일 `elementType`으로 전경 또는 배경 표현을 겸하고, 채굴 시 광물 종류에서 배경 종류를 추론합니다. 이는 화면 표시에는 사용할 수 있지만 “원래 자연 배경 / 빈 배경 / 플레이어 벽지”를 완전히 구분하는 A-16 저장 정본으로는 부족합니다.

### 수정 요구사항

1. 기존 A-16의 전경·배경 독립 상태와 `IWallpaperCoverageSource` 동등 계약을 구현합니다.
2. 내부 ID는 현재 코드 호환을 위해 `bg_*`를 유지해도 되지만 공식 `t_bg_*`와의 별칭·저장 마이그레이션 정책을 문서화합니다.
3. 벽지는 비충돌·비밀폐 경계이며 `SealPercent`, `TemperaturePercent`, `leak_faces`를 변경하지 않습니다.
4. 100% 도포 효과는 개발 B가 물단지·얼음 항아리 지속시간에만 적용할 수 있도록 월드 측에서 도포 완료 상태만 공급합니다.
5. 구버전 저장처럼 배경 변경 이력이 없는 데이터도 로드 가능해야 합니다.

### 회귀 테스트

기존 A-20의 배경·벽지 7개 항목을 실제 `Nyangbingo/Run Dev A Regression Tests`에 추가하고 전체 성공 로그의 분모를 갱신합니다.

---

## A-24. 최종 인수인계 조건 갱신

개발 A PR에는 다음을 한글로 기록합니다.

- A-21~A-26 각각의 완료 여부
- 새로 추가·변경한 공용 인터페이스 전문
- 전경 Collider의 컴포넌트·레이어·Material·좌표 설정값
- 배경 데이터 구조와 `bg_*`/`t_bg_*` 호환 정책
- 저장 스키마 변경 여부와 스키마 16 호환 결과
- 개발 B가 제거해야 할 임시 코드의 정확한 메서드명
- 새로 늘어난 Dev A 회귀 테스트 수와 전체 통과 로그
- 새 게임, 데모 3종, 저장·로드 검증 결과

완료 후 개발 B가 수행할 통합 순서는 다음과 같습니다.

1. `MainGamePlayerController.MoveWithTileCollision` 임시 경로 제거
2. `MainGameWorldDropRuntime.MoveWithTileCollision` 임시 경로 제거
3. `TryFindTemporarySafeSurfaceSpawn`과 데모 생성기의 직접 타일 순회 제거
4. Rigidbody2D/Collider 기반 플레이어·드랍 배선
5. 벽지 도포율을 냉기원 지속시간 보너스에 연결
6. 하단 타일 팔레트의 벽지 항목을 개발 A 배경 배치 계약에 연결
7. 반경·밀폐 창 오버레이를 팔레트 토글과 PC `R` 입력에 연결
8. Dev A 회귀 → Dev B 통합 회귀 → MainGame 새 게임·데모·저장·로드 → 제품 빌드 순서로 검증

---

## A-25. v29 하단 타일 팔레트용 월드 배치 계약 보존

### 현재 개발 B 구현 상태

v29 정본의 구 12칸 핫바 대체 시스템으로 `MainGameTilePaletteController`가 추가되었습니다.

- 플레이어가 현재 보유한 설치 가능 타일·설치물만 하단에 동적으로 표시
- 제작대·화로 같은 제품 설치물은 기존 `MainGameTurretRuntime` 배치 경로 사용
- 채굴한 흙·돌 등 전경 타일은 `TileService.TryPlaceForeground` 경로 사용
- 인벤토리 채집 탭의 `E` 설치와 하단 팔레트 클릭이 동일한 배치 진입점 사용
- 전경 타일 설치 가능 여부는 제작법 유무가 아니라 `TileService.SupportsForegroundPlacement`로 판정
- `wallpaper`는 A-23 배경 배치 계약이 없으므로 현재 팔레트에서 의도적으로 제외

`TileService.SupportsForegroundPlacement`는 개발 B가 현재 `PlacementHardness` 정본을 외부에서 안전하게 조회하기 위해 추가한 최소 계약입니다. 개발 A가 `TileService`를 수정·교체할 때 이 메서드를 누락시키거나, 채굴 타일을 “제작법이 없다”는 이유로 배치 불가 처리하면 안 됩니다.

### 요구사항

1. 다음 정보와 동작을 유지하는 공용 계약을 제공합니다. 실제 메서드명은 달라도 됩니다.
   - 아이템 ID가 플레이어 재설치 가능한 전경 타일인지 판정
   - 대상 셀이 설치 가능한지 사전 판정
   - 전경 설치 실행과 성공/실패 결과 반환
2. 전경 설치 허용 목록은 월드 타일 정본에서 관리합니다. UI의 제작법·아이템 카테고리를 월드 배치 가능 여부의 정본으로 사용하지 않습니다.
3. 기반암, 얼음 제단, 자연 전용 타일, 배경 전용 ID는 전경 재설치 대상으로 노출하지 않습니다.
4. 설치 실패 시 인벤토리를 소비하지 않고, 성공한 경우에만 정확히 1개를 소비하는 원자성을 유지합니다.
5. 성공 프레임에 `TileData`, 전경 화면, A-21 Collider, 변경 이력과 저장 상태가 모두 일치해야 합니다.
6. 개발 A가 메서드 시그니처를 변경해야 한다면 기존 메서드를 바로 삭제하지 말고 대체 인터페이스와 마이그레이션 예시를 먼저 전달합니다.
7. 개발 A는 다음 개발 B 파일을 직접 수정하지 않습니다.
   - `MainGameTilePaletteController.cs`
   - `MainGameCraftingUiController.cs`
8. A-23 완료 시 벽지에 대해 다음과 동등한 배경 전용 계약을 함께 제공합니다.

```csharp
public interface IBackgroundPlacementService
{
    bool CanPlaceWallpaper(Vector3Int cell);
    bool TryPlaceWallpaper(Vector3Int cell);
    bool TryRemoveWallpaper(Vector3Int cell);
    BackgroundCellState GetBackgroundState(Vector3Int cell);
}
```

벽지의 인벤토리 소비는 전경과 동일하게 성공 시 1개, 실패 시 0개여야 합니다. 개발 B가 이 계약을 받은 뒤 팔레트 우측 끝 벽지 항목과 설치·제거 입력을 연결합니다.

### 회귀 테스트

1. 흙·돌을 채굴해 소지품에 넣은 뒤 인벤토리와 하단 팔레트 양쪽에서 재설치 가능
2. 점유 셀·월드 밖·금지 타일 설치 실패 시 수량 불변
3. 설치 직후 화면과 Collider가 동시에 생성되고 다시 채굴 가능
4. 저장·로드 뒤 재설치 타일과 인벤토리 수량 동일
5. 기반암·얼음 제단·배경 ID가 전경 팔레트 대상으로 판정되지 않음
6. 벽지 계약 완료 후 전경과 독립적으로 설치·제거·복원되고 Collider·밀폐율은 불변

---

## A-26. 반경 표시와 밀폐 창 월드 오버레이 인계

최신 정본은 하단 팔레트에 반경 표시 토글을 두고 다음 월드 범위를 일괄 표시하도록 규정합니다.

- 등불 반경 6타일
- 체 반경 4타일
- 해태 반경 8타일
- 밀폐 창 사각형 `seal_window_rx` × `seal_window_ry`

기획서의 아트 명세는 이 원·사각형을 고정 이미지가 아닌 **개발 A 런타임 코드**로 지정합니다. 반경과 밀폐 창 크기가 CSV/전역값에 따라 달라지므로 텍스처에 수치를 고정하면 안 됩니다.

### 담당 분리

- 개발 A: Grid/Tilemap 좌표에 정확히 맞는 원·사각형 월드 오버레이 렌더러와 공용 호출 계약 제공
- 개발 B: 하단 팔레트 토글 UI, PC `R` 입력, 현재 설치물 목록과 표시 반경 전달
- 아트: 신규 원형·사각형 텍스처 없음

### 요구사항

1. 입력을 직접 읽지 않는 표시 전용 컴포넌트/API를 제공합니다. 개발 A가 `R` 키나 팔레트 버튼을 직접 처리하지 않습니다.
2. 개발 B가 전달한 중심 좌표·반경·형상 목록을 월드 Grid와 동일한 좌표 계약으로 그립니다.
3. 원 반경과 밀폐 창 크기는 현재 CSV·전역값 또는 호출 인자에서 읽고 하드코딩하지 않습니다.
4. 오버레이는 Collider, 밀폐 판정, 공격 범위, 저장 데이터에 영향을 주지 않는 순수 시각 요소여야 합니다.
5. 카메라 이동·해상도 변경·픽셀 퍼펙트 설정에서도 타일 경계와 어긋나지 않아야 합니다.
6. 표시 해제 시 생성한 선·메시·머티리얼을 누수 없이 정리합니다.
7. 밀폐 창 사각형은 A-23의 실제 코어 및 `SealSystem` 좌표와 동일한 중심을 사용합니다.

### 권장 계약 예시

```csharp
public readonly struct WorldRangeOverlay
{
    public Vector2 Center;
    public float Radius;
    public WorldRangeShape Shape;
}

public interface IWorldRangeOverlayRenderer
{
    void SetVisible(bool visible);
    void Render(IReadOnlyList<WorldRangeOverlay> overlays);
    void Clear();
}
```

### 회귀 테스트

1. 반경 4·6·8 원이 실제 타일 중심에서 정확한 셀 수만큼 표시
2. 밀폐 창 사각형이 `SealSystem` 계산 창과 동일
3. 토글 반복·씬 재로드 후 오버레이 중복 또는 머티리얼 누수 없음
4. 설치물 추가·회수 후 다음 갱신에서 목록과 위치가 일치
5. 해상도·카메라 위치 변경에도 월드 타일과 정렬 유지

---

## 담당 경계

### 개발 A 우선 수정 영역

- `WorldGenerationConfig`, `MapGenerator`, `WorldGenerationResult`
- `TileData`, `TileService`, `TilemapRenderer`
- 전경/배경 Tilemap과 Collider 구성
- 배경·벽지 상태 및 월드 저장/복원
- 안전 스폰 판정·해결 계약
- 전경/배경 배치 가능 판정과 원자적 설치 계약
- 반경·밀폐 창 월드 오버레이 렌더러
- `NyangbingoDevARegressionTests`

### 개발 A가 직접 수정하지 않을 영역

- `MainGamePlayerController`
- `MainGameWorldDropRuntime`
- `NyangbingoDemoSaveGenerator`
- `MainGameEnvironmentState`, `MainGameRuntimeServices`
- 인벤토리·제작·제련·장비·도감 UI와 입력
- HUD, 전투 피드백, 아이템 아트와 자석 습득
- 하단 타일 팔레트 UI와 인벤토리 설치 입력
- `NyangbingoMainGameSceneCreator`
- `DevBTestBootstrap`, Dev B 통합 회귀 테스트
- 현재 v29 CSV와 생성 SO

MainGame 배선 변경이 필요하면 개발 B 파일을 직접 수정하지 말고, 컴파일 가능한 월드 계약과 필요한 컴포넌트·설정 목록을 먼저 전달합니다.
