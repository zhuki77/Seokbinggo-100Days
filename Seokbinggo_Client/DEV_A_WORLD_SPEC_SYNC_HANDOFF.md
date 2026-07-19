# 개발 A파트 — 월드 스펙 정합성 보완(A-10~A-15) 인수인계 문서

작성일: 2026-07-20
브랜치: `feat/deva-world-spec-sync` (기준: 최신 `main`, 직접 커밋 없음)
Unity 버전: `6000.5.3f1` (`ProjectSettings/ProjectVersion.txt` 기준, 변경 없음)
대상 문서: 「개발 A파트 최신 기획 정합성 보완 작업 명세서」A-10~A-15

> 이 문서는 A-10~A-15 작업 전용입니다. A-01~A-11(구 번호)까지의 이전 라운드 인수인계는
> `DEV_A_HANDOFF_REPORT.md`를 참고하세요. 두 문서는 서로 다른 작업 사이클을 다룹니다.

---

## 0. 요약

| ID | 제목 | 상태 |
|---|---|---|
| A-10 | 월드 지층 깊이 정본 교정(45/45/45/5) + mineral-tiers.csv depth 사용 | ✅ 완료 |
| A-11 | SealSystem 57×25 밀폐 창 + `SetSealCoreCell`/`ClearSealCoreCell` API | ✅ 완료 |
| A-12 | `ICoolingSourceProvider.CoolingCapPercent` 계약 확장 | ✅ 완료 |
| A-13 | SealSystem 회귀 테스트 13종 추가 | ✅ 완료 |
| A-14 | 타일 노출면 먹선(edge) 오버레이 렌더러 구조 | ✅ 완료(렌더러 구조·연결 슬롯, 아트 자산은 미배선) |
| A-15 | 월드 렌더링 최적화 점검(P1) | ✅ 점검 완료 — 개발 A 소유 코드에 추가 수정 불필요 |

기존 개발 A 회귀 테스트(6/6)는 그대로 유지되고, 위 작업으로 검증 항목이 **8개**로 늘어났습니다
(`Nyangbingo/Run Dev A Regression Tests` 메뉴 하나로 전부 실행).

---

## 1. A-10. 월드 지층 깊이 정본 교정

### 변경 내용

- `WorldGenerationConfig.cs`
  - `upperLayerThickness = 45`, `middleLayerThickness = 45`, `bedrockThickness = 5` (기존 40/55/4에서 교정).
  - `surfaceBaseHeightRatio = 0.86875f` — `globals.csv`의 `surface_y=20`과 일치하도록 재계산.
  - `OreVeinProfile`에 `depthMin`/`depthMax` 필드 추가, `mineral-tiers.csv`의 실제 값을 그대로 하드코딩
    (런타임 CSV 파싱 없이 결정론·성능 유지, 회귀 테스트가 CSV와의 정합성을 매번 검증).
- `MapGenerator.cs`
  - `GetDepthRange(surfaceHeights, depthMin, depthMax)` 헬퍼 추가 — "각 열의 지표에서 내려간 깊이" 기준으로
    y 범위를 계산(요구사항 5: 좌표 변환 방향은 유지, 내부 계산만 깊이 기준으로 통일).
  - `PlaceOreVeins`/`EstimateArea`가 `depthMax`가 지정된 프로파일은 `GetDepthRange`를, 그렇지 않으면 기존
    `GetLayerRange`(지층 기준)를 쓰도록 분기.
  - `WorldGenerationResult.surfaceHeights` 필드 추가(테스트가 실제 지표 배열을 검증할 수 있도록).

### 개발 B 영향

없음. `WorldGenerationConfig`/`CreateDefault()`/생성기 내부 구현만 바뀌었고, 공개 API 시그니처는 그대로입니다.
기존 상자 20개·유적·얼음 제단·보호 타일·저장된 타일 변경 이력 계약은 전혀 건드리지 않았습니다.

### 회귀 테스트

`NyangbingoDevARegressionTests.TestLayerDepthAndMineralRanges`:
- `WorldGenerationConfig`/`CreateDefault()` 두께 값이 45/45/45/5로 일치하는지.
- 리플렉션으로 `MapGenerator.ClassifyLayer`를 호출해 T1/T2/T3/경계암 경계(45/90/135/140)가 정확한지.
- `OreVeinProfile`의 `depthMin`/`depthMax`가 `mineral-tiers.csv`와 일치하는지.
- 실제 생성된 월드에서 각 광물이 자신의 `depth_min~depth_max` 밖에 배치되지 않는지.

---

## 2. A-11. SealSystem 57×25 밀폐 창

### 변경 내용 (`SealSystem.cs`)

- 생성자에 `sealWindowRadiusX`/`sealWindowRadiusY`/`sealTargetCells` 파라미터 추가(기본값 28/12/240 —
  카탈로그가 없는 단위 테스트용 안전망일 뿐, 실제 세션은 항상 `globals.csv` 값을 주입).
- `WindowCellCap`(= `(2rx+1)×(2ry+1)`, 기본 1425) — 하드코딩 3000 대신 창 크기에서 동적 계산.
- 신규 공개 API:

  ```csharp
  void SetSealCoreCell(Vector3Int cell);   // 개발 B가 석빙고 코어(얼음 저장고 등) 좌표를 전달
  void ClearSealCoreCell();                // 코어 해제 → 온도 0%로 안전 복귀
  bool HasSealCoreCell { get; }
  Vector3Int? SealCoreCell { get; }
  ```

- `SetPrimaryWatchPoint(cell)`는 `SetSealCoreCell(cell)`의 별칭으로 유지(기존 호출부 호환).
- `SealPercent`/`LeakFaceCount`/`TemperaturePercent`는 이제 코어 셀 기준 57×25 창 안에서만 계산됩니다
  (`ComputeCoreWindowRegion`). 코어가 없으면 세 값 모두 즉시 0을 반환합니다(요구사항 4).
- 창 밖으로 이어지는 공기 면은 `leak_faces`로 집계됩니다(요구사항 5).
- 경계 인정 기준은 기존과 동일합니다: 자연 지형(`isNaturalTerrain`) + `ISealBarrierRegistry`가 인정하는
  설치물만(요구사항 6) — 문 개폐는 `ISealBarrierRegistry` 구현체(개발 B)가 알아서 처리하고, `SealSystem`은
  밀폐 판정 자체를 문 상태로 바꾸지 않습니다(요구사항 9).
- 기존 캐시 무효화 조건(`HandleTileChanged`, `OnNightStart` → `InvalidateAll`)은 그대로 유지하면서, 코어
  창에 영향이 있는 변경일 때만 `coreRegionCache`도 함께 무효화합니다.
- 범용 관찰 지점 API(`RegisterWatchPoint`/`IsWatchPointSealed`/`IsInsideSealedArea`/`TryGetDebugRegion`)는
  창과 무관한 기존 무제한 Flood Fill(`maxFillCells` 기준)을 그대로 유지합니다 — 플레이어 은신처 판정,
  `MainGamePlayerController`의 냥잠 보너스, 개발 B의 B-08 검증이 이 동작에 의존하기 때문입니다.

### 확정 산식 반영 위치

`TemperaturePercent`(§3에서 설명, A-12와 함께) — `SealPercent`는 별도로 `region_cells/seal_target_cells`
비율만 반환합니다(0~1 소수).

### 개발 B 연결 필요 사항

1. 석빙고 코어(얼음 저장고 등)를 배치/철거하는 코드에서 `sealSystem.SetSealCoreCell(cell)` /
   `ClearSealCoreCell()`을 호출해야 합니다. **플레이어 위치가 아니라 실제 설치 좌표**를 넘겨야 합니다.
2. `WorldSessionController.CreateSealSystem()`이 `globals.csv`의 `seal_window_rx`/`seal_window_ry`/
   `seal_target_cells`를 자동으로 읽어 주입하므로, 개발 B는 별도 설정 없이 `session.SealSystem`을 그대로
   쓰면 됩니다.

---

## 3. A-12. 냉기원 상한 연동 계약 변경

### 변경 내용

- `WorldContracts.cs`: `ICoolingSourceProvider`에 `float CoolingCapPercent => 0f;`(C# 8 기본 인터페이스
  구현) 추가. 기존 `IsColdSourceActive`는 하위 호환을 위해 유지하되, `SealSystem`은 더 이상 이 값을
  최종 온도 계산에 쓰지 않습니다.
- `SealSystem.TemperaturePercent` 최종 산식:

  ```text
  leak_faces == 0
      ? min(coolingSourceProvider.CoolingCapPercent, 100 × min(1, region_cells / seal_target_cells))
      : 0
  ```

  Provider가 연결되지 않으면 `CoolingCapPercent`는 기본 구현값 0을 반환하므로 "냉기원 없음"과 동일하게
  0%가 됩니다(요구사항: Provider 미연결 ≠ 100%).

### 확정 상한값 (개발 B가 구현할 값)

| 냉기원 | 상한 |
|---|---|
| 없음 | 0% |
| 물단지 | 25% |
| 얼음 항아리 | 50% |
| 얼음 저장고 | 100% |
| 빙정 냉각로 | 100% |
| 여러 개 동시 가동 | 최고 상한 적용(개발 B가 `CoolingCapPercent` getter에서 `Max` 계산) |

### 개발 B 연결 필요 사항

`MainGameEnvironmentState`(개발 B 소유, `ICoolingSourceProvider` 구현체)를 확인해 보니 **이미
`public float CoolingCapPercent { get; private set; }`가 존재하고, `Mathf.Max(...)`로 현재 가동 중인
냉기원들의 상한 중 최고값을 계산해 대입하고 있었습니다.** 즉 개발 B가 이 계약을 선제적으로 구현해
둔 상태였고, 이번 A-12 작업은 `ICoolingSourceProvider` 인터페이스에 그 멤버를 정식으로 추가해
컴파일 타임 계약으로 확정한 것입니다 — 개발 B가 추가로 손댈 부분은 없습니다. `SealSystem`이 이제
`IsColdSourceActive` 단일 boolean 대신 이 `CoolingCapPercent` 값을 실제 최종 온도 산식에 사용합니다.

---

## 4. A-13. SealSystem 회귀 테스트 보강

`NyangbingoDevARegressionTests.TestSealSystemCoreWindow()`가 아래 13개 항목을 전부 검증합니다
(메서드 내부에서 순차적으로 호출):

1. `TestSealedRoomAreaProportionalTemperature` — 완전 밀폐 소형 방의 면적 비례 온도
2. `TestSealedRoomAtOrAboveTargetCellsIsFullTemperature` — 240칸 이상 밀폐 시 기본 온도 100%
3. `TestSingleLeakFaceZeroesTemperature` — 누출면 1개 발생 시 0%
4. `TestLeakOutsideWindowIsDetected` — 57×25 창 밖으로 이어지는 공기 = 누출
5. `TestNaturalTerrainAtWindowBoundarySealsNormally` — 창 경계가 자연 지형이면 정상 밀폐
6. `TestNoCoreCellYieldsZero` — 코어 미설정 시 0%
7. `TestNoColdSourceYieldsZero` — 냉기원 없음 시 0%
8~10. `TestColdSourceCaps` — 물단지 25% / 얼음 항아리 50% / 얼음 저장고·빙정 냉각로 100%
11. `TestHighestCapAmongMultipleSourcesApplies` — 여러 냉기원 중 최고 상한 적용
12. `TestCacheRecalculatesOnTileChangeAndNightStart` — 타일 변경·밤 시작 후 캐시 재계산
13. `TestWorldSessionRoundTrip` 내부에 추가된 코어 셀 저장/로드 유지 검증 — 저장/로드 후에도 `SealSystem`
    인스턴스·관찰 지점·`SealCoreCell`이 그대로 유지되는지(A-13 항목 13)

`FakeCoolingSourceProvider`에 `CoolingCapPercent` 세터를 추가해 8~11번 테스트에서 임의의 상한을 주입합니다.

---

## 5. A-14. 타일 노출면 먹선(edge) 오버레이

### 설계 개요

47조각 재질별 오토타일 대신, **재질과 무관한 공용 스프라이트 5장**만으로 모든 노출 패턴을 표현합니다.
상/하/좌/우 노출 여부를 4비트 마스크(최대 15가지 조합)로 표현하고, 이를 "모양 5종 × 90도 회전"으로
매핑합니다. 나머지 회전은 `Tilemap.SetTransformMatrix`로 처리하므로 회전판 에셋을 추가로 만들 필요가
없습니다.

| 모양 인덱스 | 제안 에셋 ID | 설명 | 기준(회전 0) |
|---|---|---|---|
| 0 | `edge_straight` | 1면 노출 — 직선 | Top |
| 1 | `edge_corner` | 인접한 2면 노출 — 모서리 | Top+Right |
| 2 | `edge_through` | 마주보는 2면 노출 — 관통 | Top+Bottom(세로) |
| 3 | `edge_tjunction` | 3면 노출 — T자 | Left만 막힘(Top+Right+Bottom) |
| 4 | `edge_isolated` | 4면 모두 노출 — 고립 블록 | 회전 무관 |

> 명세는 "인접한 두 방향 노출 시 모서리 조각"까지만 요구했지만, 실제 지형에는 3면·4면 노출(좁은 통로 끝,
> 고립된 1칸 블록)도 발생할 수 있어 위 표의 인덱스 3·4까지 함께 설계해 두었습니다. 아트가 5장 전부
> 준비되지 않았다면 `edgeShapeTiles` 배열에서 해당 인덱스만 비워두면 그 조합만 조용히 그려지지 않습니다
> (예외 없음).

### 신규/변경 파일

- **`World/TileEdgeOverlay.cs`(신규)**
  - `TileEdgeMask`(`[Flags]`: `Top/Right/Bottom/Left`) — 노출 방향 비트마스크.
  - `TileEdgeOverlayResolver`:
    - `ComputeExposureMask(tiles, x, y, width, height)` — 순수 함수. 4방향 이웃이 `TileData.IsAir`
      (공기 **또는** 배경 노출 상태, 둘 다 `hardness<=0`)인지로 마스크를 만든다. 맵 경계 밖은 노출로
      취급하지 않는다.
    - `TryResolve(mask, out shapeIndex, out rotationSteps)` — 마스크 → (모양, 회전) 변환. `None`이면 false.
    - `BuildRotationMatrix(rotationSteps)` — `Tilemap.SetTransformMatrix`에 바로 넘길 수 있는 셀 중심
      기준 회전 행렬.
- **`World/TilemapRenderer.cs`**
  - `edgeOverlayTilemap`(인스펙터 슬롯, 비워두면 A-14 기능 전체가 조용히 비활성화 — 기존 씬 안전).
  - `edgeShapeTiles`(`TileBase[5]`, 위 표의 순서/개수).
  - `RebuildEdgeOverlayForWorld(tiles)` — 월드 전체 초기 1회 계산(`RenderWorld` 끝에서 자동 호출).
  - `RefreshEdgeOverlay(cell, mask)` — 셀 하나만 갱신(국소 갱신 전용, 회전 행렬까지 함께 설정).
- **`World/TileService.cs`**
  - `TryBreakForeground`/`TryPlaceForeground`/`RestoreTileChanges`의 각 변경 지점에서
    `RefreshEdgeOverlayAround(cell)` 호출 — **변경 셀 + 상하좌우 이웃, 딱 5칸만** 재계산(월드 전체
    재계산 없음, 프레임당 O(1)). `renderer`가 아직 연결되지 않은 로드 검증 단계에서는 조용히 무시됩니다
    (`ApplyForegroundVisual`과 동일한 안전 규칙).

### 개발 B/아트 연결 필요 사항

1. `edge_straight`/`edge_corner`/`edge_through`/`edge_tjunction`/`edge_isolated` 5장의 스프라이트
   (또는 `Tile` 에셋)를 준비해, `TilemapRenderer` 인스펙터의 `edgeShapeTiles` 배열(순서: 위 표 인덱스
   0~4)에 드래그앤드롭으로 연결해 주세요.
2. `edgeOverlayTilemap` 슬롯에 전경/배경보다 위(정렬 순서상 더 앞)에 있는 새 `Tilemap`을 만들어 연결해
   주세요. 슬롯을 비워두면 기존 씬은 아무 영향 없이 그대로 동작합니다.
3. 코드 변경은 필요 없습니다 — 스프라이트 배선만으로 즉시 동작합니다.

### 회귀 테스트

`NyangbingoDevARegressionTests.TestTileEdgeOverlay`:
- 마스크 → (모양, 회전) 테이블이 1~15 전부를 유효한 값으로 해석하고, `None`은 해석에 실패하는지.
- 5×5 자연석 안에서 중앙 칸만 파괴하면 정확히 4개 이웃만 각각 1면(직선) 먹선으로 갱신되고, 그 외 칸은
  전혀 갱신되지 않는지(월드 전체 재계산 금지 회귀).
- 같은 칸을 재설치하면 이웃의 먹선이 전부 원상 복구되는지.

---

## 6. A-15. 월드 렌더링 최적화 점검(P1)

개발 A 소유 파일(`WorldGenerationConfig`/`MapGenerator`/`SealSystem`/`TileService`/`TilemapRenderer`/
`WorldSessionController`/`DayNightService`/`CentralTickDriver`)을 전부 재검토했습니다.

| 점검 항목 | 결과 |
|---|---|
| 카메라 밖 대규모 오브젝트 업데이트 제한 | 개발 A 소유 코드에는 `Update()`가 있는 대규모 오브젝트 순회가 없음(월드 생성기/타일 서비스/밀폐 시스템 모두 이벤트 기반) |
| 반복적인 전체 월드 순회 제거 | `TilemapRenderer.RenderWorld`(전경/배경/A-14 먹선 전체 계산)는 월드 생성·로드 시 **딱 1회**만 호출됨. 이후 채굴/설치는 A-14와 동일하게 변경 셀+이웃만 국소 갱신 |
| 타일 변경 시 국소 갱신 유지 | 기존부터 `TileService`가 `ApplyForegroundVisual`/`ApplyBackgroundVisual`만 갱신했고, 이번에 A-14 먹선 갱신도 같은 패턴으로 추가함 |
| `CullingGroup` 등 가시성 관리 | Tilemap은 Unity 렌더러가 자체적으로 카메라 컬링을 처리함. 몹/터렛/이펙트 등 개별 오브젝트 컬링은 개발 B 소유 영역(`MainGamePlayerController`/`YokaiBrain`/`MainGameTurretRuntime` 등)이라 이번 A 작업 범위에서 수정하지 않음 — 필요 시 개발 B가 검토 권장 |
| 400×160 월드 GC 할당 없음 | `DayNightService.Tick`/`CentralTickDriver.LateUpdate`는 재사용 리스트만 쓰고 매 프레임 할당이 없음(기존 설계 확인). A-14 국소 갱신도 `Vector3Int` 값 타입 5개 계산 + 딕셔너리 조회뿐이라 힙 할당 없음 |

**결론**: 개발 A 소유 범위에서는 A-15 요구사항을 만족하기 위한 추가 코드 변경이 필요하지 않았습니다
(이미 이전 라운드에서 국소 갱신/캐시 구조로 설계돼 있었고, 이번 A-14도 같은 원칙을 따랐습니다). 카메라
밖 몹/이펙트 업데이트 제한은 개발 B 소유 파일에 있어 이번 브랜치에서는 손대지 않았습니다.

---

## 7. 변경 파일 목록

```text
Seokbinggo_Client/Assets/Scripts/Nyangbingo/World/WorldGenerationConfig.cs      (A-10)
Seokbinggo_Client/Assets/Scripts/Nyangbingo/World/MapGenerator.cs               (A-10)
Seokbinggo_Client/Assets/Scripts/Nyangbingo/World/SealSystem.cs                 (A-11/A-12)
Seokbinggo_Client/Assets/Scripts/Nyangbingo/Core/WorldContracts.cs              (A-12)
Seokbinggo_Client/Assets/Scripts/Nyangbingo/World/WorldSessionController.cs     (A-11, globals.csv 주입)
Seokbinggo_Client/Assets/Scripts/Nyangbingo/Debug/SealSystemDebugView.cs        (A-11, 코어 셀 단축키)
Seokbinggo_Client/Assets/Scripts/Nyangbingo/World/TileEdgeOverlay.cs            (A-14, 신규)
Seokbinggo_Client/Assets/Scripts/Nyangbingo/World/TilemapRenderer.cs            (A-14)
Seokbinggo_Client/Assets/Scripts/Nyangbingo/World/TileService.cs                (A-14)
Seokbinggo_Client/Assets/Editor/NyangbingoDevARegressionTests.cs                (A-10/A-13/A-14 테스트)
Seokbinggo_Client/DEV_A_WORLD_SPEC_SYNC_HANDOFF.md                              (이 문서)
```

## 8. 새/변경된 공용 인터페이스 요약

```csharp
// Nyangbingo.World.SealSystem — 개발 B가 새로 호출해야 하는 API (A-11)
void SetSealCoreCell(Vector3Int cell);
void ClearSealCoreCell();
bool HasSealCoreCell { get; }
Vector3Int? SealCoreCell { get; }

// Nyangbingo.Core.ICoolingSourceProvider — 개발 B가 구현해야 하는 계약 (A-12)
public interface ICoolingSourceProvider
{
    bool IsColdSourceActive { get; }
    float CoolingCapPercent => 0f; // 기본 구현 — 기존 구현체는 컴파일 안 깨짐, override 시 실제 상한 반환
}

// Nyangbingo.World.TileEdgeOverlayResolver / TileEdgeMask — 개발 A 내부용, 개발 B는 몰라도 됨 (A-14)
```

## 9. 회귀 테스트 실행 방법

Unity 에디터 메뉴: `Nyangbingo > Run Dev A Regression Tests`

| # | 테스트 | 신규 여부 |
|---|---|---|
| 1 | 결정론적 생성 | 기존 |
| 2 | 상자 분포 | 기존 |
| 3 | 타일 변경 이력 원자성 | 기존 |
| 4 | 밀폐 시스템 | 기존 |
| 5 | 낮/밤 전환 | 기존 |
| 6 | 월드 세션 라운드트립(+ A-13 항목13 코어 셀 유지) | 기존(검증 추가) |
| 7 | 지층 깊이·광물 깊이 정합성 | **신규(A-10)** |
| 8 | SealSystem 코어 창(57x25) — 내부 13개 하위 검증(A-13) | **신규(A-11/A-12/A-13)** |
| 9 | 타일 노출면 먹선 오버레이 | **신규(A-14)** |

전부 통과 시: `[Nyangbingo] Dev A 회귀 테스트 전체 통과 (9/9).`

> **참고**: 이번 세션의 코드 검토는 Unity 에디터가 설치되지 않은 환경에서 진행되어, 실제 Unity
> 컴파일러를 통한 최종 빌드 확인은 하지 못했습니다. 정적 분석(린터) 기준으로는 오류가 없으나,
> PR을 열기 전 Unity 에디터에서 위 메뉴를 한 번 실행해 실제 컴파일/테스트 통과를 재확인해 주세요.

---

## 10. 알려진 제한사항

- A-14 먹선 오버레이는 렌더러 구조와 연결 슬롯까지만 구현했습니다. 실제 아트 5장(§5 표)이 배선되기
  전까지는 화면에 아무 것도 그려지지 않지만, 데이터/이벤트 경로는 전부 동작합니다.
- A-15는 개발 A 소유 범위 안에서 "추가 수정 불필요"로 결론 내렸습니다. 몹/터렛/이펙트 등 개발 B 소유
  오브젝트의 카메라 컬링은 이번 브랜치에서 다루지 않았습니다.
