using System;
using System.Collections.Generic;
using Nyangbingo.Core;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>
    /// 밀폐(실내) 판정 시스템. TileService가 들고 있는 실시간 TileData[,]를 기준으로, 특정 셀에서
    /// 사방으로 Flood Fill을 수행해 "완전히 인정된 벽으로만 둘러싸인 공간"인지 판정한다.
    ///
    /// 핵심 규칙(v15 QA-F 화이트리스트 + 결재 브리프 v2 처방 C 반영):
    ///  - 밀폐 벽으로 인정되는 타일은 자연 지형(isNaturalTerrain == true) + <see cref="ISealBarrierRegistry"/>가
    ///    인정하는 B파트 설치물(차열벽·차열 지붕·단열 문 등)이다. 레지스트리가 연결되지 않으면 자연 지형만 인정한다.
    ///  - 인정되지 않은 타일, 맵 경계 밖, 탐색 범위(maxFillCells) 초과는 모두 "누출면(leak face)"으로 집계되고,
    ///    누출면이 하나라도 있으면 그 방은 밀폐되지 않은 것으로 취급한다.
    ///  - <see cref="SealPercent"/>는 결재 브리프 v2(07-15 오너 승인) 처방 C를 그대로 따른다:
    ///    <c>leak_faces == 0 ? min(1, region_cells / 240) : 0</c> (0~1 소수 계약 유지 — ×100이 기획 문서의 "%"다).
    ///    구(舊) "자연 벽 수 / 전체 경계 벽 수" 비율식(v15 QA-F가 결함으로 지적, 구멍 1칸이 1/N로 희석됨)은 폐기했다.
    ///
    /// 성능: 매 타일 변경마다 전체 맵(400x160)을 다시 스캔하지 않는다. 이미 계산해 둔 리전(Region) 캐시를
    /// 셀 단위로 보관하고, 변경된 셀과 그 4방향 인접 셀에 걸쳐 있는 리전만 무효화한 뒤, 실제로 추적 중인
    /// "관찰 지점(watch point)"만 다시 계산한다. Flood Fill 자체도 maxFillCells로 크기가 제한된다.
    /// 밤 시작(OnNightStart) 시에도 매 프레임이 아니라 딱 한 번, 캐시 전체를 지우고 관찰 지점만 재계산한다.
    /// </summary>
    public sealed class SealSystem : ISealSource, IDisposable
    {
        private const int DefaultMaxFillCells = 3000;

        /// <summary>처방 C 분모(방 크기 상한). 결재 브리프 v2: "region_cells는 이미 계산 중(캡 체크가 씀)이라 신규 자료구조 0".</summary>
        private const float SealPercentRegionCellCap = 240f;

        /// <summary>Flood Fill 한 번의 결과. 내부 공기 칸과 그 경계벽 칸, 밀폐 여부/밀폐율을 담는다.</summary>
        private sealed class SealRegion
        {
            public bool isSealed;
            public float sealPercent;

            /// <summary>처방 C의 region_cells — 이 리전의 내부 공기 칸 수(=interiorAirCells.Count, 조회 편의용 캐시).</summary>
            public int regionCellCount;

            /// <summary>이 리전 경계에서 발견된 누출면(leak face) 개수. 0이면 처방 C의 leak_faces==0 조건을 만족한다.</summary>
            public int leakFaceCount;

            public HashSet<Vector3Int> interiorAirCells = new HashSet<Vector3Int>();
            public HashSet<Vector3Int> boundaryWallCells = new HashSet<Vector3Int>();

            /// <summary>캐시 무효화 판정에 쓰는 전체 셀(내부 공기 + 경계벽) 합집합.</summary>
            public HashSet<Vector3Int> AllCells()
            {
                var all = new HashSet<Vector3Int>(interiorAirCells);
                all.UnionWith(boundaryWallCells);
                return all;
            }
        }

        private static readonly Vector3Int[] FourNeighbors =
        {
            new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0), new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0)
        };

        private TileService tileService;
        private readonly int maxFillCells;

        /// <summary>B파트 설치물(차열벽/차열 지붕/단열 문) 화이트리스트 조회 — 없으면 자연 지형만 인정(기존 동작).</summary>
        private ISealBarrierRegistry barrierRegistry;

        /// <summary>B파트 온도 시스템의 냉기원 가동 상태 — 없으면 항상 가동 중으로 간주(§ TemperaturePercent 참고).</summary>
        private ICoolingSourceProvider coolingSourceProvider;

        /// <summary>셀 → 그 셀이 속한 리전. 같은 방(room)의 모든 칸이 같은 SealRegion 인스턴스를 가리키므로,
        /// 리전 내부의 다른 칸을 조회해도 캐시 히트로 처리된다(플레이어가 방 안에서 움직여도 재계산 없음).</summary>
        private readonly Dictionary<Vector3Int, SealRegion> regionByCell = new Dictionary<Vector3Int, SealRegion>();

        /// <summary>실시간으로 추적할 관찰 지점과 마지막으로 통보한 밀폐 상태. 여기 등록된 지점만
        /// 타일 변경 시 다시 계산되고 이벤트가 발행된다 — 맵 전체를 매번 스캔하지 않기 위한 핵심 최적화.</summary>
        private readonly Dictionary<Vector3Int, bool> watchPoints = new Dictionary<Vector3Int, bool>();

        private Vector3Int? primaryWatchPoint;

        /// <summary>
        /// GameEvents.OnSealChanged(무매개변수)는 이미 DevBTest 회귀 테스트가 구독 중인 기존 Dev B 계약이라
        /// 시그니처를 바꿀 수 없다. 어느 지점이, 어떤 상태로 바뀌었는지까지 필요한 소비자(실내 온도 시스템 등)는
        /// 이 이벤트로 받으면 된다. SealSystem은 두 이벤트를 항상 함께 발행한다.
        /// </summary>
        public event Action<Vector3Int, bool> WatchPointSealChanged;

        public SealSystem(TileService tileService, int maxFillCells = DefaultMaxFillCells,
            ISealBarrierRegistry barrierRegistry = null, ICoolingSourceProvider coolingSourceProvider = null)
        {
            this.tileService = tileService ?? throw new ArgumentNullException(nameof(tileService));
            this.maxFillCells = Mathf.Max(16, maxFillCells);
            this.barrierRegistry = barrierRegistry;
            this.coolingSourceProvider = coolingSourceProvider;

            GameEvents.OnTileBroken += HandleTileChanged;
            GameEvents.OnTilePlaced += HandleTileChanged;
            GameEvents.OnNightStart += HandleNightStart;
        }

        /// <summary>정적 이벤트 구독을 해제한다. 세션/씬 종료 시 반드시 호출해야 한다(개발 가이드 §1.7).</summary>
        public void Dispose()
        {
            GameEvents.OnTileBroken -= HandleTileChanged;
            GameEvents.OnTilePlaced -= HandleTileChanged;
            GameEvents.OnNightStart -= HandleNightStart;
        }

        // ------------------------------------------------------------------
        // 확장 지점 — B파트 설치물/온도 시스템이 준비되는 시점에 언제든 뒤늦게 연결할 수 있도록 생성자
        // 외에도 setter를 노출한다(WorldSessionController가 SealSystem을 재생성할 때 재주입할 수 있게).
        // ------------------------------------------------------------------

        /// <summary>차열벽/차열 지붕/단열 문 등 B파트 설치물을 밀폐 벽으로 인정할지 조회할 레지스트리를 연결한다.</summary>
        public void SetBarrierRegistry(ISealBarrierRegistry registry) => barrierRegistry = registry;

        /// <summary>B파트 온도 시스템의 냉기원 가동 상태 조회자를 연결한다(<see cref="TemperaturePercent"/>가 사용).</summary>
        public void SetCoolingSourceProvider(ICoolingSourceProvider provider) => coolingSourceProvider = provider;

        /// <summary>
        /// A-07: 월드 로드 등으로 살아있는 TileData[,]가 통째로 새 TileService로 바뀌었을 때, 이 SealSystem
        /// 인스턴스 자체는 Dispose하지 않고 내부 참조만 교체한다. 이렇게 하면 주 관찰 지점/고정 관찰 지점
        /// (watchPoints), <see cref="WatchPointSealChanged"/> 구독자, 외부 시스템이 들고 있던 이 SealSystem
        /// 참조가 로드 전후로 전부 그대로 유지된다. GameEvents 구독은 인스턴스 생성 시 한 번만 걸리는 정적
        /// 이벤트라 새 TileService와 무관하게 계속 유효하므로 재구독할 필요가 없다 — 오직 리전 캐시만
        /// 새 데이터 기준으로 다시 계산하면 된다.
        /// </summary>
        public void Rebind(TileService newTileService)
        {
            tileService = newTileService ?? throw new ArgumentNullException(nameof(newTileService));
            InvalidateAll(); // 캐시 전체 무효화 + 등록된 관찰 지점만 새 데이터로 재계산(상태가 바뀐 지점만 이벤트 발행).
        }

        // ------------------------------------------------------------------
        // ISealSource — WorldContracts.cs 계약. 실내 온도 시스템이 이 인터페이스로 밀폐 상태를 읽는다.
        // ------------------------------------------------------------------

        /// <summary>
        /// 현재 "주 관찰 지점"(보통 플레이어 위치)의 밀폐율(0~1, 처방 C). 지점이 없으면 0.
        /// ×100이 기획 문서가 말하는 "sealPct(%)"다. 냉기원 가동 여부와는 무관하게 순수 밀폐 상태만 반영한다 —
        /// 냉기원까지 반영한 값이 필요하면 <see cref="TemperaturePercent"/>를 쓴다.
        /// </summary>
        public float SealPercent => primaryWatchPoint.HasValue ? GetOrComputeRegion(primaryWatchPoint.Value).sealPercent : 0f;

        /// <summary>주 관찰 지점 경계에서 발견된 누출면(leak face) 개수. 0이면 완전 밀폐(처방 C의 leak_faces==0).</summary>
        public int LeakFaceCount => primaryWatchPoint.HasValue ? GetOrComputeRegion(primaryWatchPoint.Value).leakFaceCount : 0;

        /// <summary>
        /// "4 시스템"(v17 최종) 온도% 산식: <c>냉기원 가동 && leak_faces == 0 ? 처방 C 값(0~100) : 0</c>.
        /// <see cref="SealPercent"/>(밀폐율 자체, 결재 브리프 v2 처방 C)와는 별개의 값이므로 혼용하지 말 것 —
        /// SealPercent는 항상 냉기원과 무관한 순수 밀폐 상태이고, 이 값은 B파트 TemperatureSystem이 온도
        /// 게이지에 바로 쓸 수 있게 냉기원 게이트까지 얹은 편의값이다. 이 값은 0~100 스케일이다(SealPercent와
        /// 스케일이 다르니 주의). coolingSourceProvider가 연결돼 있지 않으면(B 시스템 연결 전) 항상 가동 중으로
        /// 간주해, 연동 전에도 leak_faces==0일 때 처방 C 값을 그대로 볼 수 있게 한다.
        /// </summary>
        public float TemperaturePercent
        {
            get
            {
                if (!primaryWatchPoint.HasValue) return 0f;
                var region = GetOrComputeRegion(primaryWatchPoint.Value);
                var coldSourceActive = coolingSourceProvider == null || coolingSourceProvider.IsColdSourceActive;
                return coldSourceActive && region.leakFaceCount == 0 ? region.sealPercent * 100f : 0f;
            }
        }

        /// <summary>임의의 월드 좌표가 완전히 밀폐된 실내 안에 있는지. 등록되지 않은 좌표도 즉석으로 계산(캐시됨)한다.</summary>
        public bool IsInsideSealedArea(Vector2 position)
        {
            var cell = new Vector3Int(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y), 0);
            return GetOrComputeRegion(cell).isSealed;
        }

        /// <summary>해당 셀이 맵 경계 안에 있는지. 맵 밖은 방(room) 개념이 성립하지 않는 확정 실패 상태라,
        /// 디버그 뷰가 Flood Fill 결과(칸 목록) 크기에 좌우되지 않는 별도의 확실한 실패 마커를 그릴 수 있게 노출한다.</summary>
        public bool IsInBounds(Vector3Int cell) => tileService.InBounds(cell);

        // ------------------------------------------------------------------
        // 관찰 지점 관리 — 실시간 추적이 필요한 위치(플레이어, 화로/제단 등)만 등록한다.
        // ------------------------------------------------------------------

        /// <summary>플레이어처럼 매 프레임 위치가 바뀌는 "주 관찰 지점"을 갱신한다. SealPercent가 이 지점을 기준으로 계산된다.</summary>
        public void SetPrimaryWatchPoint(Vector3Int cell)
        {
            if (primaryWatchPoint.HasValue && primaryWatchPoint.Value == cell) return;
            if (primaryWatchPoint.HasValue) UnregisterWatchPoint(primaryWatchPoint.Value);
            primaryWatchPoint = cell;
            RegisterWatchPoint(cell);
        }

        /// <summary>특정 좌표(예: 화로, 제단, 침대)를 실시간 감시 대상으로 등록한다. 이미 등록돼 있으면 무시한다.</summary>
        public void RegisterWatchPoint(Vector3Int cell)
        {
            if (watchPoints.ContainsKey(cell)) return;
            var region = GetOrComputeRegion(cell);
            watchPoints[cell] = region.isSealed;
        }

        public void UnregisterWatchPoint(Vector3Int cell) => watchPoints.Remove(cell);

        /// <summary>
        /// 세이브 로드처럼 TileService의 TileData[,]가 통째로 새로 만들어졌을 때(§11.6) 호출한다. 기존
        /// 리전 캐시는 이제 존재하지 않는 배열을 참조하므로 전부 버리고, 등록된 관찰 지점만 새 데이터로
        /// 다시 계산해 밀폐 상태가 바뀌었으면 정확히 한 번만 이벤트를 발행한다(수만 타일 diff를 재생하는
        /// 동안 타일 단위로 OnTileBroken/OnTilePlaced를 발행하지 않는 로드 경로와 짝을 이루는 설계).
        /// </summary>
        public void InvalidateAll()
        {
            regionByCell.Clear();
            RefreshWatchPoints();
        }

        public bool IsWatchPointSealed(Vector3Int cell) => watchPoints.TryGetValue(cell, out var sealedNow) ? sealedNow : GetOrComputeRegion(cell).isSealed;

        // ------------------------------------------------------------------
        // 디버그 시각화 보조 — 실제 그리기는 MonoBehaviour(SealSystemDebugView)가 담당하고,
        // 여기서는 순수 데이터(칸 목록)만 노출한다.
        // ------------------------------------------------------------------

        public bool TryGetDebugRegion(Vector3Int cell, out bool isSealed, out float sealPercent,
            out IReadOnlyCollection<Vector3Int> interiorCells, out IReadOnlyCollection<Vector3Int> boundaryCells)
        {
            var region = GetOrComputeRegion(cell);
            isSealed = region.isSealed;
            sealPercent = region.sealPercent;
            interiorCells = region.interiorAirCells;
            boundaryCells = region.boundaryWallCells;
            // 맵 밖(interiorAirCells에 대상 칸 자체가 채워짐)이나 벽을 직접 가리킨 경우(boundaryWallCells에 채워짐)도
            // "그릴 것이 있는 실패 상태"로 취급해야 한다 — 예전에는 interiorAirCells만 확인해서 이 두 실패 케이스가
            // 전부 "그릴 게 없음"으로 조용히 무시되어 디버그 뷰에 아무것도 표시되지 않는 버그가 있었다.
            return region.interiorAirCells.Count > 0 || region.boundaryWallCells.Count > 0;
        }

        // ------------------------------------------------------------------
        // 내부 구현
        // ------------------------------------------------------------------

        private void HandleTileChanged(Vector3Int changedCell)
        {
            if (regionByCell.Count == 0) return; // 캐시된 리전이 없으면 재계산할 것도 없다(전체 스캔 방지).

            var affected = new HashSet<SealRegion>();
            foreach (var probe in NeighborsAndSelf(changedCell))
            {
                if (regionByCell.TryGetValue(probe, out var region)) affected.Add(region);
            }

            if (affected.Count == 0) return;

            foreach (var region in affected)
            {
                foreach (var cell in region.AllCells()) regionByCell.Remove(cell);
            }

            RefreshWatchPoints();
        }

        /// <summary>
        /// 기획 정본(개발 가이드 ②/결재 브리프 v2) 재계산 트리거 = "타일 배치/파괴 직후 + 밤 시작". 밤 시작은
        /// 특정 셀 하나가 바뀐 게 아니라 임의의 관찰 지점 어디든 영향을 줄 수 있는 전역 트리거이므로,
        /// HandleTileChanged처럼 인접 셀만 무효화하지 않고 InvalidateAll()로 캐시 전체를 지운 뒤
        /// 등록된 관찰 지점만 다시 계산한다(매 프레임이 아니라 밤 시작 시 단 한 번뿐이라 성능 규칙에 안전하다).
        /// </summary>
        private void HandleNightStart() => InvalidateAll();

        private void RefreshWatchPoints()
        {
            if (watchPoints.Count == 0) return;

            // 순회 중 딕셔너리를 수정하지 않도록 키를 복사해 둔다.
            var cells = new List<Vector3Int>(watchPoints.Keys);
            foreach (var watchCell in cells)
            {
                var region = GetOrComputeRegion(watchCell);
                var previousSealed = watchPoints[watchCell];
                if (region.isSealed == previousSealed) continue;

                watchPoints[watchCell] = region.isSealed;
                GameEvents.RaiseSealChanged(); // 기존 Dev B 계약(무매개변수) — DevBTest 회귀 안정성을 위해 시그니처 유지.
                WatchPointSealChanged?.Invoke(watchCell, region.isSealed);
            }
        }

        private SealRegion GetOrComputeRegion(Vector3Int cell)
        {
            if (regionByCell.TryGetValue(cell, out var cached)) return cached;

            var region = ComputeRegion(cell);
            regionByCell[cell] = region;
            foreach (var interiorCell in region.interiorAirCells) regionByCell[interiorCell] = region;
            foreach (var wallCell in region.boundaryWallCells) regionByCell[wallCell] = region;
            return region;
        }

        private SealRegion ComputeRegion(Vector3Int start)
        {
            var region = new SealRegion();

            if (!tileService.InBounds(start))
            {
                // 맵 밖은 방(room) 개념이 성립하지 않는 확정 실패다. 디버그 뷰는 이 케이스를
                // IsInBounds()로 먼저 걸러내 줌 레벨과 무관하게 항상 눈에 띄는 전용 마커로 표시하므로,
                // 여기서는 빈 리전을 그대로 반환해 방 데이터로 오인되지 않게 한다.
                region.isSealed = false;
                region.sealPercent = 0f;
                return region;
            }

            if (!tileService.GetTile(start).IsAir)
            {
                // 벽(전경 타일)을 직접 가리킨 경우: "방(내부 공기)" 개념이 없으므로 그 칸 자체를 경계벽으로 표시해
                // 디버그 뷰가 최소한 "여긴 밀폐 대상이 아니라 벽"이라는 피드백을 줄 수 있게 한다.
                region.isSealed = false;
                region.sealPercent = 0f;
                region.boundaryWallCells.Add(start);
                return region;
            }

            var interior = new HashSet<Vector3Int> { start };
            var boundaryWalls = new HashSet<Vector3Int>();
            var queue = new Queue<Vector3Int>();
            queue.Enqueue(start);

            var leakFaceCount = 0;
            var cellBudgetExceeded = false;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                for (var i = 0; i < FourNeighbors.Length; i++)
                {
                    var neighbor = current + FourNeighbors[i];

                    if (!tileService.InBounds(neighbor))
                    {
                        leakFaceCount++; // 맵 경계 밖으로 뚫려 있으면 누출면 1개(실외로 새는 면).
                        continue;
                    }

                    if (interior.Contains(neighbor) || boundaryWalls.Contains(neighbor)) continue;

                    var neighborTile = tileService.GetTile(neighbor);
                    if (neighborTile.IsAir)
                    {
                        if (cellBudgetExceeded) { leakFaceCount++; continue; }

                        interior.Add(neighbor);
                        if (interior.Count > maxFillCells)
                        {
                            // 이 이상 확장하면 400x160 규모에서 렉을 유발할 수 있다 — 여기서부터는 "너무 커서 실외"로 간주.
                            cellBudgetExceeded = true;
                            leakFaceCount++;
                            continue;
                        }
                        queue.Enqueue(neighbor);
                    }
                    else
                    {
                        boundaryWalls.Add(neighbor);
                        // 화이트리스트: 자연 지형 + (있다면) B파트 설치물 레지스트리가 인정하는 구조물.
                        // 인정되지 않으면 그 면은 누출면이다(v15 QA-F: 인공 타일=틈새).
                        if (!IsRecognizedWall(neighborTile, neighbor)) leakFaceCount++;
                    }
                }
            }

            region.interiorAirCells = interior;
            region.boundaryWallCells = boundaryWalls;
            region.regionCellCount = interior.Count;
            region.leakFaceCount = leakFaceCount;

            // 처방 C(결재 브리프 v2, 07-15 오너 승인 · 처방 B는 밸런스 독립 반증으로 기각):
            // 밀폐가 안 됐으면 0%(이진 속성을 정직하게), 밀폐됐으면 방을 넓힌 만큼(최대 240칸) 오른다.
            region.sealPercent = leakFaceCount == 0 ? Mathf.Min(1f, region.regionCellCount / SealPercentRegionCellCap) : 0f;
            region.isSealed = leakFaceCount == 0 && boundaryWalls.Count > 0;

            return region;
        }

        /// <summary>
        /// v15 QA-F 화이트리스트: 자연 지형(흙·돌·암반) + B파트 설치물(차열벽/차열 지붕/단열 문 등).
        /// 설치물 데이터는 TileData 그리드가 아니라 B파트가 소유할 가능성이 크므로, SealSystem은 직접 알지
        /// 못하고 <see cref="barrierRegistry"/>에 위임한다 — 레지스트리가 연결되지 않으면 자연 지형만
        /// 인정하는 기존 동작을 그대로 유지한다(연결 전에도 컴파일/동작이 깨지지 않도록 하는 안전한 기본값).
        /// </summary>
        private bool IsRecognizedWall(TileData tile, Vector3Int cell) =>
            tile.isNaturalTerrain || (barrierRegistry != null && barrierRegistry.IsRecognizedBarrier(cell));

        private static IEnumerable<Vector3Int> NeighborsAndSelf(Vector3Int cell)
        {
            yield return cell;
            for (var i = 0; i < FourNeighbors.Length; i++) yield return cell + FourNeighbors[i];
        }
    }
}
