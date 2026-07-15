# 개발 B → 개발 A 전체 인수인계 명세서

## 0. 문서 목적과 기준

이 문서는 개발 A가 이전 대화나 과거 인수인계 문서 없이도 현재 `main`의 개발 B 기능을 이해하고, 메인 게임 씬에 연결한 뒤 후속 개발을 진행할 수 있도록 작성한 단일 기준 문서다.

- 저장소: `zhuki77/Seokbinggo-100Days`
- Unity 프로젝트: `Seokbinggo_Client`
- 반영 PR: `#5 feat: Dev B 데이터 파이프라인 및 게임플레이 시스템 확장`
- `main` 병합 커밋: `78842321c6cd0b779d01891891aab4afcd38b589`
- PR에 포함된 기능군 커밋:
  - `93f22d8` 데이터 파이프라인
  - `184341a` 경제·전투·요괴·보스 런타임
  - `6fbfb76` 구조화 세이브
  - `272eb78` DevBTest 회귀 테스트
- 인수인계 시점 검증 결과:
  - Unity `DevBTest` 씬 전체 실행 시 오류 로그 없음
  - `Assembly-CSharp.csproj` 빌드: 경고 0, 오류 0
  - `Assembly-CSharp-Editor.csproj` 빌드: 경고 0, 오류 0

개발 A는 최신 `main`에서 작업을 시작해야 한다. 이 문서에 적힌 클래스와 에셋이 보이지 않으면 먼저 위 병합 커밋이 현재 작업 기준에 포함되어 있는지 확인한다.

---

## 1. 절대 지켜야 하는 공통 규칙

1. 모든 게임 진행 타이머는 현실 초나 `Time.timeScale`이 아니라 `TimeManager.gameSeconds`를 기준으로 한다.
2. `Tick(float gameSeconds)` 형태의 API에는 누적값이 아니라 직전 프레임 이후의 게임 시간 증가량을 전달한다.
3. `IGameSecondsSource.GameSeconds`는 누적 게임 시간 값이다. `YokaiBrain`은 이 누적값의 차이를 내부에서 계산한다.
4. CSV가 원본 데이터이고 ScriptableObject는 생성 결과물이다. 데이터 수정은 원칙적으로 CSV에서 시작한다.
5. 생성된 ScriptableObject의 ID와 파일명을 런타임 중 임의로 바꾸지 않는다. 세이브 데이터가 문자열 ID를 사용한다.
6. `haetae_statue` 레시피 재료는 반드시 `copper_ingot`이다. 존재하지 않는 `copper`로 되돌리지 않는다.
7. 정적 이벤트를 구독한 객체는 반드시 수명 종료 시 구독을 해제한다. `IDisposable` 바인딩은 반드시 `Dispose()`한다.
8. 요괴 처치, 보스 시작·처치, 백중일 시작 이벤트는 개발 B 코드가 이미 발행한다. 개발 A가 같은 이벤트를 중복 발행하면 안 된다.
9. 월드 상자는 정확히 20개를 결정론적으로 생성해야 한다. ID, 좌표, 열림 상태가 같은 시드에서 재현되어야 한다.
10. 전투 근접 판정은 이미 `Physics2D.OverlapBoxAll` 기반이다. 다시 `OverlapCircle` 기반으로 바꾸지 않는다.
11. 아트·애니메이션·실제 프리팹은 아직 완성 전이므로 데이터와 런타임 로직을 유지한 채 참조만 교체한다.
12. 기존 Dev B 검증을 삭제하거나 약화하지 않는다. 통합 후에도 `DevBTest`를 회귀 기준으로 유지한다.

---

## 2. 현재 구현 범위와 아직 연결되지 않은 범위

### 구현 완료

- CSV 검증 및 CSV → ScriptableObject 리임포트
- 통합 `GameDataCatalog`
- 12칸 인벤토리, 아이템 획득, 인벤토리 초과 보상 보관
- 장비 소유 컬렉션, 장착 슬롯, 능력치 집계
- 즉시 제작, 시간 제작, 레시피 해금
- 화로·주물소 제련 대기열 및 완성품 수령함
- 유틸리티 사용 및 게임 시간 쿨다운
- 결정론적 상자 액세서리 보상과 1회 열기
- 방어력·피해 전달 유형·넉백·보스 넉백 면역
- OverlapBox 근접 공격, 철사 올가미, 포탑, 유도 투사체 풀
- 요괴 이동·벽 공격·특수 카운터 규칙·드롭
- 보스 소환·강제 조우·보상·새벽 종료
- 백중일 스케줄·웨이브·일반 스폰 정지·보상 배율
- 3슬롯 JSON 세이브, 스키마 마이그레이션, 원자적 파일 교체
- 플레이어·시간·활성 보스·인벤토리·장비·제작·제련·월드·상자·포탑·도감·보스 기록 저장 구조
- 79개 `DevBTestBootstrap` 테스트 메서드

### 개발 A가 메인 게임에 연결해야 하는 부분

- 실제 `TimeManager`를 Dev B 시간 인터페이스에 연결
- 낮/밤/새벽 경고/타일/봉인 이벤트 발행
- 실제 월드 생성기와 타일 변경 기록 연결
- 정확히 20개의 결정론적 상자 생성 및 `IChestSource` 구현
- 일반 요괴 스폰 정지·재개 제어기 구현
- 요괴 및 보스 프리팹 팩토리 연결
- 요괴가 공격할 실제 벽·기지·드롭·인벤토리 대상 어댑터 구현
- 실제 설치물과 `CounterAura`/포탑 연결
- 실제 플레이어 입력·UI·애니메이션·사운드 연결
- 실제 메인 씬의 구성 루트와 `ISaveSnapshotProvider` 구현
- 월드 로드 순서와 세이브 복원 조립

핵심적으로 Dev B는 도메인 로직과 검증을 제공하며, 개발 A는 월드·시간·씬 오브젝트를 이 계약에 연결한다.

---

## 3. 주요 경로

| 구분 | 경로 |
|---|---|
| CSV 원본 | `Assets/Data/CSV` |
| 생성 SO | `Assets/Data/SO` |
| 통합 카탈로그 | `Assets/Data/SO/GameDataCatalog.asset` |
| 데이터 정의 | `Assets/Scripts/Nyangbingo/Data` |
| 공통 열거형·이벤트·계약 | `Assets/Scripts/Nyangbingo/Core` |
| 인벤토리·장비·상자 | `Assets/Scripts/Nyangbingo/Inventory` |
| 제작·제련 | `Assets/Scripts/Nyangbingo/Crafting` |
| 전투·유틸리티·포탑 | `Assets/Scripts/Nyangbingo/Combat` |
| 요괴 AI·드롭·카운터 | `Assets/Scripts/Nyangbingo/Yokai` |
| 보스·백중일 | `Assets/Scripts/Nyangbingo/Bosses` |
| 세이브 | `Assets/Scripts/Nyangbingo/Save/SaveGame.cs` |
| 에디터 임포터 | `Assets/Editor/NyangbingoDataMenu.cs` |
| CSV 파서 | `Assets/Editor/NyangbingoCsvUtility.cs` |
| 회귀 테스트 씬 | `Assets/Scenes/DevBTest.unity` |
| 테스트 진입점 | `Assets/Scripts/Nyangbingo/Debug/DevBTestBootstrap.cs` |
| 테스트용 계약 구현 | `Assets/Scripts/Nyangbingo/Debug/DevBTestFakes.cs` |

---

## 4. 데이터 파이프라인

### 4.1 현재 CSV와 생성 수량

| CSV | 생성 폴더 | 현재 수량 |
|---|---|---:|
| `items.csv` | `SO/Items` | 43 |
| `recipes.csv` | `SO/Recipes` | 11 |
| `smelting.csv` | `SO/Smelting` | 3 |
| `equipment.csv` | `SO/Equipment` | 6 |
| `utilities.csv` | `SO/Utilities` | 3 |
| `yokai-stats.csv` | `SO/Yokai` | 5 |
| `bosses.csv` | `SO/Bosses` | 4 |
| `chests.csv` | `SO/Chests` | 4 |
| `day-events.csv` | `SO/DayEvents` | 1 |

`GameDataCatalog.asset`까지 포함하면 현재 SO는 총 81개다.

### 4.2 Unity 메뉴

- `Nyangbingo/Validate CSV Data`
- `Nyangbingo/Reimport Items CSV`
- `Nyangbingo/Reimport Recipes CSV`
- `Nyangbingo/Reimport Smelting CSV`
- `Nyangbingo/Reimport Equipment CSV`
- `Nyangbingo/Reimport Utilities CSV`
- `Nyangbingo/Reimport Bosses CSV`
- `Nyangbingo/Reimport Chests CSV`
- `Nyangbingo/Reimport Day Events CSV`
- `Nyangbingo/Reimport Yokai Stats CSV`
- `Nyangbingo/Rebuild Game Data Catalog`
- `Nyangbingo/Create Dev B Test Scene`

CSV를 변경했을 때만 리임포트한다. 안전한 전체 순서는 다음과 같다.

1. `Validate CSV Data`
2. Items
3. Equipment
4. Recipes
5. Smelting
6. Utilities
7. Yokai Stats
8. Bosses
9. Chests
10. Day Events
11. `Rebuild Game Data Catalog`

아이템 참조를 가진 데이터가 많으므로 Items를 먼저 생성해야 한다. Chests는 Equipment를, Yokai와 Bosses는 Items를 선행 요구한다.

### 4.3 `GameDataCatalog`

`GameDataCatalog`는 다음 조회 API를 제공한다.

- `FindItem(id)`
- `FindRecipe(id)`
- `FindSmelting(id)`
- `FindEquipment(id)`
- `FindUtility(id)`
- `FindYokai(id)`
- `FindBoss(id)`
- `FindChest(id)`
- `FindDayEvent(id)`
- `IsValid`

빈 ID, 중복 ID, null 에셋이 하나라도 있으면 카탈로그가 유효하지 않다. 메인 씬 시작 시 `catalog != null && catalog.IsValid`를 선검증하고, 실패하면 플레이를 계속하지 말고 명확한 오류를 출력한다.

### 4.4 현재 핵심 데이터

- 제련:
  - `smelt_iron`: 화로, 철광석 2 + 석탄 1 → 철 주괴 1, 20초
  - `smelt_copper`: 화로, 구리 광석 2 + 석탄 1 → 구리 주괴 1, 20초
  - `smelt_ice_steel`: 주물소, 얼음강철 원석 2 + 석탄 2 → 얼음강철 주괴 1, 45초
- 요괴:
  - `club_goblin`, `bulgasari`, `yagwanggwi`, `eoduksini`, `gangcheori`
- 보스:
  - `goblin_chief`
  - `mother_bulgasari`
  - `imugi`: 깊은 제단 필요
  - `gangcheori`: 30일 밤 강제 조우
- 백중일:
  - ID `baekjung`, 15일, 최대 활성 12
  - 웨이브 오프셋 `0 / 150 / 300` gameSeconds
  - 각 웨이브 구성 `ClubGoblin 3 / Bulgasari 2 / Yagwanggwi 7`
  - 요괴의 눈물 배율 1.5, 고유 드롭 확률 배율 2

---

## 5. 시간 계약과 이벤트 허브

### 5.1 개발 A가 구현할 시간 인터페이스

`Assets/Scripts/Nyangbingo/Core/WorldContracts.cs`

```csharp
public interface IGameSecondsSource { float GameSeconds { get; } }
public interface ITimeSource
{
    int Day { get; }
    bool IsNight { get; }
    event Action Dawn;
}
public interface ISaveableTimeSource : ITimeSource
{
    float TimeOfDayGameSeconds { get; }
    bool RestoreTimeState(int day, float timeOfDayGameSeconds, bool isNight);
}
```

권장 방식은 실제 `TimeManager` 또는 얇은 어댑터 MonoBehaviour가 세 인터페이스를 구현하는 것이다.

- `GameSeconds`: 전체 플레이 동안 단조 증가하는 누적 게임 시간
- `TimeOfDayGameSeconds`: 현재 날짜 안에서의 게임 시간 위치
- `Dawn`: 실제 새벽 전환이 확정된 순간 한 번 호출
- 로드 시 `RestoreTimeState`는 이벤트를 임의로 중복 발행하지 말고 상태를 원자적으로 복원한다.

### 5.2 `GameEvents` 발행 책임

| 이벤트 | 발행 책임 |
|---|---|
| `OnDayStart` | 개발 A 시간 시스템 |
| `OnNightStart` | 개발 A 시간 시스템 |
| `OnDawnWarning` | 개발 A 시간 시스템 |
| `OnTilePlaced(Vector3Int)` | 개발 A 월드/건설 시스템 |
| `OnTileBroken(Vector3Int)` | 개발 A 월드/채굴 시스템 |
| `OnSealChanged` | 개발 A 봉인 시스템 |
| `OnYokaiKilled(YokaiDefinition)` | 이미 `YokaiLoot`가 발행 |
| `OnBossSummoned(BossDefinition)` | 이미 `BossManager.TryStart`가 발행 |
| `OnBossDefeated(BossDefinition)` | 이미 `BossManager`가 처치 시 발행 |
| `OnBaekjungStart` | 이미 `BaekjungScheduler`가 발행 |

`ITimeSource.Dawn`과 `GameEvents.OnDayStart`는 용도가 다르다. 새벽 전환 시 시간 시스템이 자신의 `Dawn`을 호출하고, 전역 알림으로 `GameEvents.RaiseDayStart()`도 한 번 발행한다. 밤 진입 시 `RaiseNightStart()`, 새벽 경고 시 `RaiseDawnWarning()`을 각각 한 번만 호출한다.

### 5.3 타이머 연결 규칙

- `YokaiBrain.SetGameSecondsSource(IGameSecondsSource)` 후에는 `Update()`가 누적값 차이를 계산한다.
- 다음 객체는 개발 A의 중앙 업데이트 루프에서 delta gameSeconds로 `Tick`해야 한다.
  - `CraftingProcess`
  - 각 `SmeltingStation`
  - `UtilityService`
  - `WireSnareAbility`
  - `TurretController`
  - `HomingProjectilePool`
  - `BaekjungTimeBinding`
- 음수, NaN, Infinity는 모두 거부되거나 무시된다.
- 정지 상태에서는 gameSeconds 증가량이 0이어야 하며 타이머도 진행되지 않아야 한다.

---

## 6. 인벤토리·장비·경제

### 6.1 인벤토리

- `Inventory.SlotCount = 12`
- 생성 시 `Func<string, ItemDefinition>` resolver가 필수다.
- `TryAdd`는 작업 전체가 들어갈 용량이 있을 때만 성공한다.
- `TryRemove`는 수량 전체가 있을 때만 성공한다.
- `TryImport`는 12칸, 유효 ID, 양수 수량, 최대 스택을 검증한 후 적용한다.
- 변경 시 `Changed` 이벤트가 발생한다.

메인 구성 예:

```csharp
var inventory = new Inventory(id => catalog.FindItem(id));
```

### 6.2 아이템 획득과 넘친 보상

모든 드롭과 보상은 가능하면 `ItemAcquisition.Request(item, amount)`를 사용한다.

- 활성화된 `InventoryRuntime`이 정적 이벤트를 구독한다.
- 인벤토리에 들어가지 않으면 `InventoryRuntime.Pending`에 보관한다.
- UI는 `TryCollectPending(index)`로 다시 수령하게 한다.
- 보스 보상, 요괴 드롭, 상자 아이템 보상이 이 경로를 사용한다.

메인 씬에서 `InventoryRuntime`을 비활성화하거나 중복 생성하면 보상이 유실되거나 중복 수령될 수 있다. 플레이 세션당 활성 수신자는 하나만 둔다.

### 6.3 장비 소유와 장착은 별도다

- `EquipmentCollection`: 플레이어가 소유한 장비 ID 집합
- `EquipmentSystem`: 현재 장착 슬롯
- `EquipmentAcquisitionBinding`: `EquipmentAcquisition.Request`를 소유 컬렉션에 연결

`EquipmentAcquisitionBinding`은 일반 C# 객체이며 반드시 저장해 두었다가 씬/세션 종료 시 `Dispose()`한다.

슬롯은 방어구 3개와 액세서리 2개다.

- 방어구: `Head`, `Body`, `Feet`
- 액세서리: `AccessoryOne`, `AccessoryTwo`
- 같은 액세서리 ID를 두 칸에 중복 장착할 수 없다.

현재 CSV에는 액세서리 6개가 있다. 방어구 데이터가 추가될 경우 CSV와 임포터 규칙을 유지한다.

### 6.4 능력치 집계

`StatSheet.Recalculate(EquipmentSystem)` 결과:

- `Defense`: 음수 방어력 무시, int overflow 방지
- `MovementMultiplier`: 기본 1 + 합산 보너스, 최저 0
- `MiningCriticalChance`: 0~25% 제한
- `TemperatureRiseModifier`: 최저 -35% 제한
- `FireDamageModifier`: 합산
- `HasDoubleJump`: 하나라도 부여하면 true
- `VisionRadiusBonus`: 최저 0
- `BlocksInventoryTheft`: 감투 등 하나라도 부여하면 true

개발 A는 장비 변경 후 `StatSheet.Recalculate`을 호출하고 플레이어 이동, 채굴, 온도, 화염 피해, 점프, 카메라/시야, 야광귀 절도 방지에 결과를 적용한다.

### 6.5 제작

- `CraftingService.TryCraft`: `DurationSeconds == 0`인 즉시 제작에 사용
- `CraftingProcess.TryStart`: `DurationSeconds > 0`인 시간 제작에 사용
- 제작대 enum: `None`, `Workbench`, `Furnace`, `IceAnvil`, `Foundry`
- `RecipeBook`을 넘기면 해금된 레시피만 제작 가능
- 시간 제작은 시작할 때 재료를 소비한다.
- 완료 시 인벤토리가 가득 차면 제작 상태와 출력이 유지되며, 공간이 생긴 뒤 다음 유효 Tick에서 완료된다.

### 6.6 제련

- 화로 `SmeltingStationKind.Furnace`: 활성 작업 포함 최대 6
- 주물소 `SmeltingStationKind.Foundry`: 활성 작업 포함 최대 4
- 시작 시 원료와 연료를 원자적으로 소비한다.
- 완료품은 인벤토리에 자동 투입하지 않고 `Completed` 수령함에 쌓인다.
- `TryCollect(index)` 성공 시에만 인벤토리로 이동한다.
- 큰 delta가 들어오면 한 Tick에서 여러 작업을 순차 완료할 수 있다.
- 월드의 각 실제 제련 설비에는 영구적이고 유일한 `stationId`가 필요하다. 세이브 어댑터가 이 ID로 상태를 구분한다.

### 6.7 유틸리티

`UtilityService` 지원 종류:

- `FoldingFan`: `FanUsed(value)`
- `BellRope`: `AlarmPlaced(value)`
- `FoxRainCharm`: `FireBufferActivated(value)`

현재 데이터:

- 부채: 쿨다운 3초, 값 3, 비소모
- 방울 금줄: 쿨다운 0, 값 10, 비소모
- 여우비 부적: 쿨다운 0, 값 30, 소모

프로덕션에서는 반드시 플레이어 `Inventory`를 생성자에 넘긴다. 소모품은 인벤토리 없는 `UtilityService`에서 사용이 거부된다. 쿨다운은 delta gameSeconds로 Tick한다.

---

## 7. 상자와 월드 계약

### 7.1 개발 A 구현 인터페이스

```csharp
public interface IChestSource
{
    IReadOnlyList<string> ChestIds { get; }
    Vector2 GetChestPosition(string chestId);
}
```

요구 조건:

- 정확히 20개
- 모든 ID가 비어 있지 않고 유일함
- 모든 좌표가 finite이며 서로 다름
- 같은 월드 시드에서 같은 ID와 좌표 재현
- 로드 시 세이브 좌표와 생성 좌표가 일치해야 함

권장 인스턴스 ID 예: `chest_00` ~ `chest_19`. 단, 이미 월드 생성 규칙이 있다면 기존 ID를 유지하되 절대 실행마다 바뀌는 InstanceID나 랜덤 GUID를 쓰지 않는다.

### 7.2 열기 흐름

1. 월드 지역에 맞는 `ChestDefinition`을 카탈로그에서 선택한다.
2. `ChestProgress.TryOpen(instanceChestId, definition, worldSeed)` 호출.
3. 첫 호출만 성공한다.
4. 일반 아이템은 `ItemAcquisition`, 액세서리는 `EquipmentAcquisition`으로 전달된다.
5. 액세서리는 `worldSeed + chestId + definition.Id`의 안정 해시로 결정된다.

CSV의 상자 정의 ID는 지역별 보상 풀 ID이고, 월드의 20개 `instanceChestId`와 같은 개념이 아니다.

---

## 8. 전투 명세

### 8.1 `Health`

- 직접 피해: `max(1, 배율 적용 피해 - 방어력)`
- `DamageDelivery.DamageOverTime`과 `Structure`: 방어력 미적용
- 화염 태그는 `fireDamageMultiplier` 추가 적용
- 모든 피해는 `damageTakenMultiplier` 적용
- 사망 후 추가 피해 무시
- 이벤트: `Damaged(DamageTag, 실제 피해량)`, `Died`
- `ConfigureForRuntime`은 HP, 방어력, 넉백 면역, 피해 배율 등 임시 상태를 초기화한다.
- 보스는 `BossManager`가 활성화할 때 `SetKnockbackImmune(true)`를 적용한다.

벽/구조물 DOT를 `Health`로 표현한다면 반드시 `DamageDelivery.DamageOverTime` 또는 `Structure`를 전달한다. 요괴의 벽 공격은 현재 `IYokaiTarget.DamageWall(float)` 경로라 방어력과 독립적이다.

### 8.2 근접 공격

`MeleeArcAttack.Strike(direction)`은 전방 `Physics2D.OverlapBoxAll` 판정 후 각 `Health`를 한 번만 공격한다.

- `targetLayers`를 실제 적 레이어로 지정
- `origin`이 null이면 오브젝트 위치 사용
- 방향 0, NaN, Infinity는 거부
- `ClawProfile`을 지정하면 피해, 넉백, 세로 도달 범위를 사용

### 8.3 철사 올가미

`WireSnareAbility` 고정 규칙:

- 쿨다운 3 gameSeconds
- 피해 4
- 넉백 2
- 내부에서 `MeleeArcAttack`의 override 공격을 사용

입력 시스템은 `TryUse(direction)`을 호출하고 중앙 gameSeconds 루프가 `Tick(delta)`한다.

### 8.4 포탑과 유도 투사체

`TurretController`는 순수 C# 런타임 객체다.

- 생성자에 포탑 Transform과 현재 적 `Health` 목록 provider를 전달
- 가장 가까운 유효 대상을 선택
- 기획 기준 재탐색 간격은 0.2 gameSeconds
- 연료는 gameSeconds 단위로 저장
- `Fired(target, damage)` 이벤트에서 실제 투사체/이펙트를 생성

`HomingProjectilePool`은 Collider 없이 위치 보간으로 추적하고 도달 시 피해를 준다. 개발 A는 시각 프리팹을 별도로 매핑하되 풀의 논리 수명과 일치시킨다.

---

## 9. 요괴 AI와 카운터

### 9.1 실제 월드가 구현할 인터페이스

```csharp
public interface IYokaiTarget
{
    Transform TargetTransform { get; }
    void DamageWall(float amount);
}

public interface IYokaiLootTarget
{
    bool TryStealGroundLoot();
    bool TryStealInventory(int maxAmount);
}

public interface IWallMaterialTarget
{
    bool IsIronHeatWall { get; }
}
```

기지/벽 대상 어댑터 하나가 필요에 따라 세 인터페이스를 함께 구현할 수 있다. 테스트 예시는 `DevBTestYokaiTarget`을 참고한다.

### 9.2 `YokaiBrain` 연결

요괴 프리팹 필수 구성:

- `Health`
- `YokaiBrain`
- `YokaiLoot`
- 필요 시 `Rigidbody2D`, Collider, 시각/애니메이션 컴포넌트

스폰 후 권장 순서:

1. 카탈로그에서 `YokaiDefinition` 조회
2. `YokaiBrain.ConfigureForRuntime(definition, target, counterSource)`
3. `YokaiBrain.SetGameSecondsSource(timeAdapter)`
4. `YokaiLoot.ConfigureForRuntime(definition, randomSource, rewardPolicy)`

`ConfigureForRuntime`이 `Health`를 정의 HP로 초기화한다. 오브젝트 풀 재사용 시에도 반드시 다시 호출한다.

### 9.3 상태와 특수 규칙

- 공통: 목표 접근 → 사거리 안에서 벽 공격
- 목표가 멀어지면 공격 상태에서 다시 접근
- 한 Tick 이동이 공격 지점을 지나치지 않도록 제한
- 사망한 요괴는 즉시 AI 행동 중지
- 목표 교체 시 접근 상태와 카운터 참조를 갱신
- 야광귀:
  - 바닥 드롭을 먼저 훔침
  - 실패하면 인벤토리 최대 10개 절도 시도
  - `BlocksInventoryTheft`가 true면 인벤토리 절도 차단
  - 체 범위에서는 절도 차단, 정지와 받는 피해 배율 적용
- 불가사리:
  - 일반 벽 공격
  - `IWallMaterialTarget.IsIronHeatWall == true`인 철 가열 벽에는 피해를 주지 않음
- 어둑시니:
  - 등불 범위 진입 시 일시정지, `Bloomed` 이벤트, 받는 피해 배율 적용
- 도깨비와 강철이는 현재 공통 접근·벽 공격 로직 및 CSV 능력치를 사용한다.

### 9.4 카운터 오라

`CounterAura`는 설치물의 논리 반경이며 아트와 독립적이다. `CounterAuraSensor`가 관찰 대상 위치를 기준으로 오라를 조회한다.

Dev B 테스트에서 사용하는 기획 파라미터:

| 오라 | 반경 | 효과값 | 지속 | 쿨다운 |
|---|---:|---:|---:|---:|
| Lantern | 6 | 피해 배율 2 | 정지 6초 | 꽃 피움 12초 |
| Sieve | 4 | 피해 배율 1.5 | 정지 12초 | 재발동 30초 |
| Haetae | 8 | 화염 피해 배율 0.5 | 0 | 0 |
| BellRope | 10 | 0 | 0 | 0 |

실제 설치물 값이 별도 기획 데이터로 확정되면 하드코딩하지 말고 데이터화하되, 현재 검증값과 변경 이유를 함께 갱신한다.

`CounterAuraEffects.Refresh()`는 해태 화염 배율을 `Health`에 적용하고 방울 범위에 새로 진입한 순간 `AlarmRaised`를 한 번 발생시킨다.

### 9.5 드롭과 처치 이벤트

`YokaiLoot`는 사망 시 다음 순서로 보상을 발행한다.

1. `definition.Drops`
2. 요괴의 눈물
3. 고유 드롭 확률 판정
4. `GameEvents.RaiseYokaiKilled(definition)`

모든 실제 아이템은 `ItemAcquisition.Request`를 사용한다. 백중일 동안에는 `BaekjungRewardRules`를 `IYokaiRewardPolicy`로 넘긴다.

---

## 10. 보스와 백중일

### 10.1 일반 보스

개발 A가 구현할 인터페이스:

```csharp
public interface IRegularSpawnController
{
    void SetRegularSpawning(bool enabled);
}

public interface IBossSummonSite
{
    bool IsAtDeepAltar(BossDefinition definition);
}

public interface IForcedBossSpawnController
{
    Health SpawnBoss(BossDefinition definition);
}
```

`BossManager` 연결:

- `ConfigureForRuntime(ITimeSource, IRegularSpawnController)` 필수
- 밤에만 시작 가능
- 활성 보스는 한 마리만 가능
- 시작 시 일반 스폰 중지, 넉백 면역 적용
- 처치 시 `OnBossDefeated`, 보상, 일반 스폰 재개
- 새벽까지 생존하면 보스 오브젝트 제거, 처치 보상·처치 이벤트 없음, 일반 스폰 재개

`BossSummonService.TryConsumeAndStart`는 소환 아이템을 먼저 예약 소비하고 시작 실패 시 반환한다. 프리팹을 만든 호출자가 생성 오브젝트의 실패/수명을 책임진다.

`BossRewardReceiver`를 활성화하고 `ConfigureForRuntime(manager)`로 연결하면 확정 보상이 `ItemAcquisition` 경로로 지급된다.

### 10.2 강제 보스

`ForcedBossEncounterBinding`은 `GameEvents.OnNightStart`를 구독한다.

- 현재 `gangcheori` 보스는 `ForcedDay = 30`
- 정확히 30일 밤이고 아직 발생하지 않았을 때만 스폰
- 성공 후 `HasTriggered = true`
- 저장·복원은 `ForcedBossEncounterSaveAdapter`
- 로드가 이미 밤인 경우 필요하면 `TryStartForCurrentNight()`을 명시 호출
- 세션 종료 시 `Dispose()` 필수

### 10.3 백중일

개발 A가 구현할 인터페이스:

```csharp
public interface IBaekjungSpawnController
{
    int ActiveCount { get; }
    bool TrySpawn(YokaiKind kind, int waveIndex);
}
```

구성 객체:

- `BaekjungScheduler(catalog.DayEvents)`
- `BaekjungTimeBinding(timeSource, scheduler)`
- `BaekjungWaveSpawner(scheduler, spawnController)`
- `BaekjungRegularSpawnGate(scheduler, regularSpawnController)`
- 활성 이벤트 동안 `BaekjungRewardRules(scheduler.ActiveDefinition)`

동작:

- 15일 밤 시작
- 오프셋마다 전체 composition을 스폰 요청
- 활성 수가 `MaxActive`에 도달하면 해당 웨이브의 남은 요청을 중단
- 시작부터 새벽까지 일반 스폰 중지
- 새벽에 이벤트 종료 및 일반 스폰 재개
- 시작한 이벤트 ID를 저장해 같은 이벤트의 중복 시작을 막음

`BaekjungTimeBinding`, `BaekjungWaveSpawner`, `BaekjungRegularSpawnGate`는 모두 세션 종료 시 `Dispose()`한다.

---

## 11. 세이브 명세

### 11.1 파일과 버전

- 클래스: `SaveManager`
- 슬롯: 0~2, 총 3개
- 경로: `Application.persistentDataPath/nyangbingo-save-{slot}.json`
- 현재 스키마: `SaveGame.CurrentSchemaVersion = 6`
- 저장: 임시 파일 작성 후 교체하는 원자적 방식
- 로드: 손상 JSON과 미래 스키마 거부
- 구버전: `NormalizeAfterLoad()`로 누락 컬렉션 초기화 및 호환 필드 정규화

### 11.2 저장되는 주요 상태

- 월드 seed, day, time of day
- 12칸 인벤토리
- 해금 레시피
- 타일 변경과 설치 오브젝트
- 봉인, 모듈 완료, 요괴의 눈물 등 기존 진행 필드
- 보스 처치 횟수와 최초 처치일
- 강제 보스 발생 여부
- 요괴 도감 처치 수
- 장착 장비와 소유 장비 ID
- 유틸리티 쿨다운
- 인벤토리 초과 보상
- 활성 시간 제작
- 제련 활성 작업·대기열·완료 수령함
- 상자 ID·좌표·열림 상태
- 플레이어 위치·현재/최대 HP
- 시간 상태
- 활성 보스 ID·위치·HP·소환 gameSeconds
- 백중일 진행 상태와 눈물 소수 나머지
- 포탑 잔여 연료 gameSeconds

### 11.3 어댑터 목록

| 어댑터/바인딩 | 책임 |
|---|---|
| `ProgressionSaveAdapter` | 인벤토리, 장착 장비, 특정 stationId의 제련 |
| `EquipmentCollectionSaveAdapter` | 소유 장비 ID |
| `RecipeBookSaveAdapter` | 해금 레시피 |
| `CraftingProcessSaveAdapter` | 활성 시간 제작 |
| `UtilityCooldownSaveAdapter` | 유틸리티 쿨다운 |
| `PendingItemAcquisitionSaveAdapter` | 넘친 보상 |
| `WorldSaveAdapter` | 타일, 설치물, 20개 상자, 포탑 연료 |
| `PlayerTimeBossSaveAdapter` | 플레이어, 시간, 활성 보스 |
| `ForcedBossEncounterSaveAdapter` | 강제 보스 트리거 |
| `BossRecordBinding` | 보스 처치 기록 실시간 갱신 |
| `YokaiCodexBinding` | 요괴 처치 도감 실시간 갱신 |

백중일은 다음처럼 직접 저장·복원한다.

```csharp
save.baekjungProgress = scheduler.CaptureState();
save.baekjungTearRemainder = rewardRules.TearRemainder;

scheduler.RestoreState(save.baekjungProgress);
rewardRules.RestoreTearRemainder(save.baekjungTearRemainder);
```

### 11.4 개발 A가 구현할 스냅샷 계약

```csharp
public interface ISaveSnapshotProvider
{
    SaveGame CaptureSnapshot();
}
```

`DawnAutoSave`에 `SaveManager`, `ITimeSource` MonoBehaviour, `ISaveSnapshotProvider` MonoBehaviour를 연결하면 `ITimeSource.Dawn` 시 자동 저장한다.

### 11.5 권장 캡처 순서

1. 세션의 현재 `SaveGame`을 `NormalizeAfterLoad()`
2. 개발 A 소유 진행 필드와 seed 갱신
3. `ProgressionSaveAdapter.Capture`를 각 고유 stationId에 대해 호출
4. 장비 소유, 레시피 북, 시간 제작, 유틸리티 쿨다운, pending 보상 캡처
5. 타일 변경·설치물·20개 상자 캡처
6. 각 포탑 연료 캡처
7. 플레이어·시간·활성 보스 캡처
8. 각 강제 보스 바인딩 상태 캡처
9. 백중일 상태와 보상 나머지 캡처
10. `SaveManager.Save(slot, save)`

`BossRecordBinding`과 `YokaiCodexBinding`은 같은 세션 `SaveGame` 인스턴스를 실시간 갱신하므로 별도 재계산하지 않는다.

### 11.6 권장 복원 순서

1. `SaveManager.TryLoad`
2. `catalog.IsValid` 확인
3. 같은 seed로 월드를 먼저 결정론적으로 재생성
4. 타일 변경과 설치 오브젝트 검증·적용
5. 정확히 같은 20개 상자를 생성한 뒤 `WorldSaveAdapter.RestoreChests`
6. 인벤토리·장비·제작·제련·유틸리티·pending 보상 복원
7. 포탑 오브젝트를 복원한 뒤 각 objectId의 연료 복원
8. 백중일 스케줄과 나머지 복원
9. 강제 보스 트리거 복원
10. `PlayerTimeBossSaveAdapter.Restore`로 시간·플레이어·활성 보스 복원
11. 모든 상태 복원 뒤 이벤트 바인딩과 UI를 최종 새로고침

중요:

- resolver가 ID와 정확히 같은 SO 인스턴스를 반환해야 한다.
- 중복 ID, 잘못된 슬롯, 잘못된 좌표, NaN/Infinity, 잘못된 큐 인덱스는 복원이 실패한다.
- 복원 실패를 무시하고 일부 상태로 게임을 시작하지 않는다. 사용자에게 슬롯 오류를 알리고 새 게임/다른 슬롯을 선택하게 한다.
- `PlayerTimeBossSaveAdapter`는 보스 스폰 실패 시 플레이어와 시간을 롤백한다.
- 각 제련소 `stationId`, 설치물 `objectId`, 상자 ID는 저장 사이에 안정적이어야 한다.

---

## 12. 메인 씬 통합 권장 구조

개발 A는 하나의 구성 루트 MonoBehaviour를 두고 순수 C# 객체와 바인딩 수명을 중앙 관리하는 것이 안전하다.

### 필수 참조

- `GameDataCatalog.asset`
- 실제 TimeManager/시간 어댑터
- 플레이어 Transform과 `Health`
- `InventoryRuntime`
- `BossManager`
- `BossRewardReceiver`
- 일반 스폰 컨트롤러
- 보스/요괴 스폰 팩토리
- 월드 변경 저장 소스
- 20개 상자 소스
- `SaveManager`

### 세션 생성 순서

1. 카탈로그 유효성 확인
2. 시간·월드 어댑터 준비
3. 인벤토리, 장비 소유, 장착, RecipeBook, 제작, 제련, UtilityService 생성
4. `InventoryRuntime.ConfigureForRuntime(inventory)`
5. `EquipmentAcquisitionBinding` 생성
6. BossManager와 보상 수신기 연결
7. 백중일 scheduler와 세 바인딩 생성
8. 보스 기록·도감 기록 바인딩 생성
9. 로드 또는 새 게임 초기화
10. UI 구독 및 첫 화면 갱신

### 세션 종료 순서

다음 객체를 빠짐없이 Dispose한다.

- `EquipmentAcquisitionBinding`
- `ForcedBossEncounterBinding` 각각
- `BaekjungTimeBinding`
- `BaekjungWaveSpawner`
- `BaekjungRegularSpawnGate`
- `BossRecordBinding`
- `YokaiCodexBinding`

MonoBehaviour 이벤트는 `OnDisable`에서 해제되도록 구현되어 있지만, 순수 C# 바인딩은 개발 A의 구성 루트가 책임진다.

---

## 13. 개발 A의 구체적인 다음 작업 순서

### 1단계: 시간과 이벤트 연결

- 실제 TimeManager에 `IGameSecondsSource`, `ISaveableTimeSource` 어댑터 추가
- 낮 시작, 밤 시작, 새벽 경고를 `GameEvents`에 연결
- 새벽에는 `ITimeSource.Dawn`과 `OnDayStart`를 각각 정확히 한 번 발생
- Dev B Tick 대상에 delta gameSeconds 전달

완료 기준:

- 일시정지 중 Dev B 타이머가 진행되지 않음
- 밤 전환 시 강제 보스/백중일 구독이 한 번만 반응
- 새벽 시 보스 종료·백중일 종료·자동 저장이 한 번만 실행

### 2단계: 월드와 상자 연결

- 타일 배치/파괴 이벤트 발행
- 타일 변경과 설치 오브젝트를 `TileChangeRecord`, `PlacedObjectRecord`로 변환
- 시드 기반 상자 20개와 안정 ID 구현
- 지역별 ChestDefinition 연결

완료 기준:

- 같은 seed로 상자 ID/좌표가 동일
- 저장 후 열림 상태가 유지
- 열린 상자를 다시 열 수 없음

### 3단계: 요괴·보스 스폰 프리팹 연결

- 5종 요괴 프리팹에 Health/YokaiBrain/YokaiLoot 연결
- 실제 기지/벽 대상 어댑터 구현
- 일반 스폰 정지 컨트롤러 구현
- 4종 보스 프리팹과 팩토리 구현
- 이무기 깊은 제단 판정 구현
- 30일 강철이 강제 조우 연결

완료 기준:

- 보스 중 일반 스폰 정지, 처치/새벽 후 재개
- 보스 넉백 면역
- 처치 보상과 이벤트가 정확히 한 번 발생
- 새벽 도주 보스는 처치 보상 없음

### 4단계: 설치물·카운터·포탑 연결

- 등불, 체, 해태상, 방울 금줄에 CounterAura 연결
- 요괴별 sensor 연결
- 포탑 타겟 provider와 Fired 시각 투사체 연결
- 포탑 objectId와 연료 세이브 연결

### 5단계: 전체 세이브 조립

- 단일 세션 상태/스냅샷 구성 루트 구현
- 위 캡처·복원 순서 적용
- 손상 슬롯, 과거 스키마, 활성 보스 로드 실패 처리 UI 구현
- 새벽 자동 저장 연결

### 6단계: UI와 실제 플레이 흐름

- 인벤토리와 pending 수령함
- 장비 소유/장착/스탯
- 레시피 해금과 제작 진행도
- 제련 대기열/완성품 수령함
- 보스·백중일 경고와 진행 표시
- 세이브 슬롯 선택·오류 표시

---

## 14. DevBTest 사용법과 회귀 기준

1. Unity에서 `Assets/Scenes/DevBTest.unity`를 연다.
2. Console을 비운다.
3. Play한다.
4. 첫 로그가 다음인지 확인한다.

```text
[Nyangbingo] Dev B test scene ready: inventory, crafting, combat, yokai, boss, and save modules can be wired here.
```

5. 모든 테스트가 끝날 때까지 기다린다.
6. `[Nyangbingo] ... failed.` 또는 `LogError`, 컴파일 오류가 하나도 없어야 한다.

씬의 `DevBTestBootstrap`에는 다음 실제 임포트 에셋이 연결되어 있다.

- `SO/Recipes/furnace.asset`
- `SO/DayEvents/baekjung.asset`
- `SO/Yokai/club_goblin.asset`
- `SO/GameDataCatalog.asset`

현재 테스트 메서드는 79개이며 다음 영역을 포함한다.

- GameEvents 전체 허브 및 구독 해제
- 인벤토리 overflow/가져오기 검증
- 카탈로그 유효성
- 제작·제련·유틸리티 타이머와 세이브
- 장비 소유·장착·스탯·절도 방지
- 상자 결정론과 정확히 20개 검증
- 보스 소환 결제·보상·강제 조우·기록
- 요괴 도감과 드롭
- OverlapBox, 방어력, DOT, 올가미, 넉백 면역
- 요괴 특수 규칙과 counter aura
- 포탑·유도 투사체 풀
- 백중일 웨이브·스폰 정지·보상·세이브
- 플레이어·시간·활성 보스·월드 구조화 세이브
- 음수/NaN/Infinity gameSeconds 방어

메인 씬 통합 변경을 한 뒤에는 다음 순서로 검증한다.

1. Unity Console 컴파일 오류 0
2. `DevBTest` 전체 실행 오류 0
3. 메인 씬에서 해당 통합 기능 수동 검증
4. 새 게임 → 저장 → 앱 재시작 → 로드 검증

---

## 15. 알려진 제한과 후속 확장 시 주의점

- 메인 씬용 완성 프리팹, 아트, 애니메이션, 이펙트는 아직 연결되지 않았다.
- `DevBTestFakes`는 테스트 전용이며 메인 씬에 사용하지 않는다.
- `ClawProfile`은 현재 CSV 파이프라인 대상이 아니다. 실제 발톱 데이터가 확정되면 별도 CSV/SO 파이프라인을 추가할 수 있다.
- `CraftingProcess`는 현재 단일 활성 시간 제작 상태다. 여러 제작대가 동시에 독립 제작해야 한다면 영구 제작대 ID 기반 컬렉션으로 확장하고 세이브 스키마도 올려야 한다.
- 제련은 이미 stationId별 다중 설비 저장이 가능하지만 개발 A가 모든 설비에 안정 ID를 부여해야 한다.
- 백중일 웨이브의 `MaxActive` 도달로 잘린 요청은 현재 나중에 재큐잉되지 않는다. 기획이 잔여 스폰 보장을 요구하면 scheduler/spawner 상태와 세이브를 함께 확장해야 한다.
- Gangcheori 요괴의 별도 행동 패턴은 아직 공통 AI 수준이다. 추가 행동은 기존 gameSeconds 계약과 사망/범위/숫자 검증을 유지한다.
- 세이브 필드를 변경할 때는 `CurrentSchemaVersion`을 올리고 이전 버전 마이그레이션과 DevBTest를 함께 추가한다.
- `JsonUtility`는 Dictionary를 직접 저장하지 않으므로 현재처럼 직렬화 가능한 record 리스트를 사용한다.
- static 이벤트 허브는 도메인 재로드 설정에 영향을 받을 수 있으므로 모든 런타임 구독을 명시적으로 해제한다.

---

## 16. 개발 A 최종 인수 체크리스트

- [ ] 최신 `main`에 병합 커밋 `7884232`가 포함됨
- [ ] `GameDataCatalog.asset`이 메인 구성 루트에 연결되고 `IsValid == true`
- [ ] 실제 TimeManager가 Dev B 시간 인터페이스를 구현
- [ ] 모든 Tick이 delta gameSeconds를 사용
- [ ] 개발 A 소유 GameEvents가 전환당 한 번만 발행
- [ ] 요괴/보스/백중일 이벤트를 중복 발행하지 않음
- [ ] 인벤토리 수신자와 장비 획득 바인딩이 세션당 하나
- [ ] 순수 C# 바인딩을 모두 Dispose
- [ ] 20개 상자가 같은 seed에서 동일하게 생성
- [ ] 월드·제련소·포탑·상자의 영구 ID가 안정적
- [ ] 5종 요괴와 4종 보스 프리팹에 필수 컴포넌트 연결
- [ ] 일반 스폰 pause/resume 연결
- [ ] 상자, 요괴, 보스 보상이 pending 보관함까지 정상 전달
- [ ] 세이브 캡처·복원 순서 적용
- [ ] 새벽 자동 저장 연결
- [ ] Unity 컴파일 오류 0
- [ ] DevBTest 오류 로그 0
- [ ] 메인 씬 새 게임/저장/재실행/로드 성공

이 체크리스트까지 통과하면 개발 B의 독립 런타임 기능이 개발 A의 실제 월드·시간·씬 시스템에 정상적으로 통합된 상태로 본다.
