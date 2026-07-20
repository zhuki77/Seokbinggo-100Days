using System;
using System.Collections.Generic;
using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using Nyangbingo.Save;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Nyangbingo.World
{
    /// <summary>
    /// 실제 채굴/건설 시스템. MapGenerator가 만든 TileData[,]를 "살아있는" 월드 상태로 소유하고,
    /// TilemapRenderer가 들고 있는 전경/배경 Tilemap을 셀 단위로 갱신한다.
    ///
    /// 규칙(개발 가이드②/④ 정본):
    ///  - 파괴 가능 여부: <c>toolTier &gt;= tile.hardness</c> (발톱 티어 ≥ 경도). 빙암/이무기 제단은 티어 무관 파괴불가.
    ///  - 파괴 시 배경벽(isUndergroundDecor=true)이 새로 드러난다 — 테라리아식 2중 구조 유지.
    ///  - 설치된 타일은 항상 isNaturalTerrain=false (SealSystem이 밀폐 벽으로 인정하지 않음, v15 QA 경고 반영).
    ///  - GameEvents.OnTilePlaced/OnTileBroken은 여기서만 발행한다 (초기 월드 생성 시에는 발행하지 않음, 성능 규칙).
    ///  - 변경 로그는 좌표별로 최신 상태만 유지한다 — WorldSaveAdapter가 (x,y,z) 중복을 거부하기 때문.
    /// </summary>
    public sealed class TileService : ITileDiffSource
    {
        private readonly TileData[,] tiles;
        private TilemapRenderer renderer;
        private readonly GameDataCatalog catalog;
        private readonly int seed;

        private readonly List<TileChangeRecord> changeLog = new List<TileChangeRecord>();
        private readonly Dictionary<Vector3Int, int> changeIndexByCell = new Dictionary<Vector3Int, int>();

        /// <summary>파괴해도 티어와 무관하게 절대 부서지지 않는 elementType (빙암, 이무기 제단).</summary>
        private static readonly HashSet<string> IndestructibleElementTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            WorldTileTypes.Bedrock,
            WorldTileTypes.IceAltar
        };

        /// <summary>월드 생성 전용 elementType(예: 심층암/폐허벽)이 파괴됐을 때 실제로 드랍할 items.csv ID.</summary>
        private static readonly Dictionary<string, string> DropItemOverrides = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { WorldTileTypes.StoneMid, WorldTileTypes.Stone },
            { WorldTileTypes.StoneDeep, WorldTileTypes.Stone },
            { WorldTileTypes.RuinWall, WorldTileTypes.Stone },
            { WorldTileTypes.IceLake, WorldTileTypes.IceShard }
        };

        /// <summary>전경 블록이 파괴됐을 때 새로 드러나는 배경벽 elementType.</summary>
        private static readonly Dictionary<string, string> BackgroundOverrides = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { WorldTileTypes.Dirt, WorldTileTypes.BackgroundDirt },
            { WorldTileTypes.Clay, WorldTileTypes.BackgroundDirt },
            { WorldTileTypes.Coal, WorldTileTypes.BackgroundDirt },
            { WorldTileTypes.Stone, WorldTileTypes.BackgroundStone },
            { WorldTileTypes.StoneMid, WorldTileTypes.BackgroundStone },
            { WorldTileTypes.IronOre, WorldTileTypes.BackgroundStone },
            { WorldTileTypes.CopperOre, WorldTileTypes.BackgroundStone },
            { WorldTileTypes.IceShard, WorldTileTypes.BackgroundStone },
            { WorldTileTypes.RuinWall, WorldTileTypes.BackgroundStone },
            { WorldTileTypes.StoneDeep, WorldTileTypes.BackgroundDeep },
            { WorldTileTypes.IceSteelOre, WorldTileTypes.BackgroundDeep },
            { WorldTileTypes.FrostEssence, WorldTileTypes.BackgroundDeep },
            { WorldTileTypes.IceLake, WorldTileTypes.BackgroundDeep }
        };

        /// <summary>플레이어가 다시 설치할 때 부여할 경도(자연 생성 시 배정되던 값과 동일하게 맞춘다).</summary>
        private static readonly Dictionary<string, int> PlacementHardness = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { WorldTileTypes.Dirt, 1 }, { WorldTileTypes.Clay, 1 }, { WorldTileTypes.Coal, 1 },
            { WorldTileTypes.Stone, 2 }, { WorldTileTypes.StoneMid, 2 }, { WorldTileTypes.IronOre, 2 },
            { WorldTileTypes.CopperOre, 2 }, { WorldTileTypes.IceShard, 2 }, { WorldTileTypes.RuinWall, 2 },
            { WorldTileTypes.StoneDeep, 3 }, { WorldTileTypes.IceSteelOre, 3 }, { WorldTileTypes.FrostEssence, 3 }
        };

        public int Width { get; }
        public int Height { get; }

        /// <param name="tiles">MapGenerator.GenerateDetailed(seed).tiles — 이 서비스가 이후 소유·변경하는 살아있는 배열.</param>
        /// <param name="renderer">실제 화면 갱신에 쓸 Tilemap 컴포넌트. null이면 데이터만 바꾸고 화면은 갱신하지 않는다(테스트용).</param>
        /// <param name="catalog">아이템 드랍 조회용. null이면 드랍 없이 파괴/설치만 수행한다(카탈로그 연결 전 임시 테스트용).</param>
        /// <param name="seed">ITileDiffSource.Seed로 노출할 확정 시드(WorldGenerationResult.acceptedSeed 권장).</param>
        public TileService(TileData[,] tiles, TilemapRenderer renderer, GameDataCatalog catalog, int seed)
        {
            this.tiles = tiles ?? throw new ArgumentNullException(nameof(tiles));
            this.renderer = renderer;
            this.catalog = catalog;
            this.seed = seed;
            Width = tiles.GetLength(0);
            Height = tiles.GetLength(1);
        }

        /// <summary>
        /// A-06: 월드 로드를 트랜잭션화하기 위한 진입점. 검증/재생(RestoreTileChanges) 동안에는 renderer를
        /// null로 두어 화면에 아무것도 그리지 않다가(생성자 파라미터 renderer=null), 모든 검증이 끝나 라이브
        /// 상태로 확정된 뒤에야 이 메서드로 실제 렌더러를 연결한다. 그 시점부터의 채굴/설치(TryBreakForeground/
        /// TryPlaceForeground)는 정상적으로 화면에 반영된다.
        /// </summary>
        public void BindRenderer(TilemapRenderer newRenderer) => renderer = newRenderer;

        public bool InBounds(Vector3Int cell) => cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Height;

        public TileData GetTile(Vector3Int cell) => InBounds(cell) ? tiles[cell.x, cell.y] : default;

        /// <summary>
        /// 전경 타일 파괴(채굴). toolTier가 타일 경도 이상이어야 성공한다. 성공 시:
        /// 데이터 갱신 → Tilemap 갱신(전경 비움, 배경벽 노출) → 아이템 드랍(ItemAcquisition) →
        /// 세이브용 변경 로그 기록 → GameEvents.OnTileBroken 발행, 순서로 진행한다.
        /// </summary>
        public bool TryBreakForeground(Vector3Int cell, int toolTier, out string droppedItemId, out int droppedAmount)
        {
            droppedItemId = null;
            droppedAmount = 0;

            if (!InBounds(cell)) return false;

            var current = tiles[cell.x, cell.y];
            if (current.IsAir) return false; // 파괴할 전경이 없음(이미 빈 칸/동굴).
            if (IndestructibleElementTypes.Contains(current.elementType)) return false; // 빙암/제단은 절대 파괴불가.
            if (toolTier < current.hardness) return false; // 장비 티어가 경도보다 낮으면 파괴 실패.

            var minedElementType = current.elementType;
            GameEvents.RaiseMiningImpact(minedElementType == WorldTileTypes.Dirt ||
                                          minedElementType == WorldTileTypes.Clay
                ? MiningImpactSurface.Dirt
                : MiningImpactSurface.Mineral);
            var backgroundElementType = ResolveBackgroundFor(minedElementType);

            tiles[cell.x, cell.y] = TileData.CreateCaveAir(backgroundElementType);
            ApplyForegroundVisual(cell, null);
            ApplyBackgroundVisual(cell, backgroundElementType);
            RefreshEdgeOverlayAround(cell); // A-14: 이 칸이 사라지며 이웃들의 노출면이 바뀔 수 있다.

            if (TryResolveDrop(minedElementType, out var item, out var amount))
            {
                ItemAcquisition.Request(item, amount);
                droppedItemId = item.Id;
                droppedAmount = amount;
            }

            RecordChange(cell, minedElementType, placed: false);
            GameEvents.RaiseTileBroken(cell);
            return true;
        }

        /// <summary>
        /// 전경 타일 설치(건설). 대상 칸이 비어 있어야(hardness&lt;=0) 성공한다. consumeFrom을 주면
        /// 인벤토리에서 elementType 1개를 소비하고, 소비에 실패하면 설치도 실패한다(원자적).
        /// 설치된 타일은 항상 isNaturalTerrain=false — SealSystem이 밀폐 벽으로 인정하지 않는다.
        /// </summary>
        public bool TryPlaceForeground(Vector3Int cell, string elementType, Nyangbingo.Inventory.Inventory consumeFrom = null, int hardnessOverride = -1)
        {
            if (string.IsNullOrEmpty(elementType) || !InBounds(cell)) return false;

            var current = tiles[cell.x, cell.y];
            if (!current.IsAir) return false; // 이미 막혀 있는 칸에는 설치할 수 없음.

            if (consumeFrom != null && !consumeFrom.TryRemove(elementType, 1)) return false;

            var hardness = hardnessOverride > 0 ? hardnessOverride : ResolvePlacementHardness(elementType);
            tiles[cell.x, cell.y] = new TileData
            {
                hardness = hardness,
                isNaturalTerrain = false,
                elementType = elementType,
                isUndergroundDecor = false
            };

            ApplyForegroundVisual(cell, elementType);
            RefreshEdgeOverlayAround(cell); // A-14: 새로 막힌 칸 때문에 이웃의 노출면이 줄어들 수 있다.

            RecordChange(cell, elementType, placed: true);
            GameEvents.RaiseTilePlaced(cell);
            return true;
        }

        /// <summary>WorldSaveAdapter.CaptureWorld(save, tileService.GetTileChangeRecords(), ...)로 그대로 넘길 수 있는 세이브 뷰.</summary>
        public IReadOnlyList<TileChangeRecord> GetTileChangeRecords() => changeLog;

        /// <summary>
        /// 세이브 로드 파이프라인(§11.6) 전용: 방금 같은 시드로 깨끗하게 재생성된 초기 타일 배열 위에,
        /// 저장된 변경 이력을 그대로 재생(replay)해 사용자가 채굴/설치했던 최종 상태로 되돌린다.
        /// TryBreakForeground/TryPlaceForeground와 동일한 파생 규칙(배경벽 노출, 설치 경도)을 그대로 쓰지만,
        /// 수백~수천 건이 한꺼번에 몰릴 수 있는 로드 경로라 GameEvents.OnTileBroken/OnTilePlaced는 발행하지
        /// 않는다(초기 월드 생성과 같은 성능 규칙). 대신 호출자가 복원이 끝난 뒤 SealSystem.InvalidateAll()로
        /// 밀폐 캐시를 한 번에 갱신해야 한다. 복원한 이력은 changeLog에도 다시 채워, 이후 재저장 시 diff가
        /// 유실되지 않게 한다(이 메서드를 호출하기 전의 changeLog는 전부 버려진다 — 새로 만든 TileService에서만 호출할 것).
        /// </summary>
        /// <summary>
        /// A-06/A-08 필수 검증: 좌표 범위, 알려진 tileId(WorldTileTypes.AllElementTypes), 보호 타일
        /// (빙암/이무기 제단)을 잘못 덮어쓰는지, 그리고 기록된 tileId가 방금 재생성한 배열의 실제 원본
        /// 타일과 일치하는지(파괴 기록의 경우)를 전부 확인한 뒤에만 배열을 변형한다. 하나라도 위반하면
        /// 그 즉시 false를 반환한다 — 호출자(WorldSessionController.LoadSnapshot)는 이 인스턴스를 애초에
        /// renderer=null로 만들어 두었다가 이 메서드가 true를 반환한 뒤에만 라이브 상태로 승격시키므로,
        /// 실패 시 화면(Tilemap)은 물리적으로 한 칸도 바뀌지 않는다.
        /// </summary>
        public bool RestoreTileChanges(IEnumerable<TileChangeRecord> records)
        {
            if (records == null) return false;

            changeLog.Clear();
            changeIndexByCell.Clear();

            foreach (var record in records)
            {
                if (string.IsNullOrWhiteSpace(record.tileId)) return false;
                if (!WorldTileTypes.AllElementTypes.Contains(record.tileId)) return false; // 알 수 없는 타일 ID 거부.

                var cell = new Vector3Int(record.x, record.y, record.z);
                if (!InBounds(cell)) return false;

                var original = tiles[cell.x, cell.y];

                if (record.placed)
                {
                    // 실제 플레이 중 TryPlaceForeground도 빈 칸에만 성공한다 — 재생도 같은 규칙을 지켜야
                    // 보호 타일(빙암/이무기 제단) 위에 무언가가 "설치"되는 손상된 기록을 걸러낼 수 있다.
                    if (!original.IsAir) return false;
                    if (!PlacementHardness.ContainsKey(record.tileId)) return false; // 플레이어가 실제로 설치 가능한 종류만 인정.

                    tiles[cell.x, cell.y] = new TileData
                    {
                        hardness = ResolvePlacementHardness(record.tileId),
                        isNaturalTerrain = false,
                        elementType = record.tileId,
                        isUndergroundDecor = false
                    };
                    ApplyForegroundVisual(cell, record.tileId);
                    RefreshEdgeOverlayAround(cell);
                }
                else
                {
                    // 파괴 재생: 원본이 실제로 파괴 가능했어야 하고(빙암/제단이면 안 됨), 기록된 tileId가
                    // 방금 결정론적으로 재생성된 원본 타일과 정확히 일치해야 한다 — 시드가 같으면 항상 같은
                    // 결과가 나오므로, 불일치는 저장 데이터가 손상됐거나 다른 시드/룰에서 만들어졌다는 뜻이다.
                    if (original.IsAir) return false;
                    if (IndestructibleElementTypes.Contains(original.elementType)) return false;
                    if (!string.Equals(original.elementType, record.tileId, StringComparison.Ordinal)) return false;

                    var background = ResolveBackgroundFor(record.tileId);
                    tiles[cell.x, cell.y] = TileData.CreateCaveAir(background);
                    ApplyForegroundVisual(cell, null);
                    ApplyBackgroundVisual(cell, background);
                    RefreshEdgeOverlayAround(cell);
                }

                RecordChange(cell, record.tileId, record.placed);
            }

            return true;
        }

        /// <summary>
        /// 요괴 AI 스폰 시스템(§9)이 밤에 안전한 바닥 좌표를 찾을 때 쓰는 순수 조회 API — 타일을 바꾸거나
        /// 이벤트를 발행하지 않는다. center로부터 유클리드 거리로 [minRange, maxRange] 범위(고리형) 안에서,
        /// 아래 3조건을 모두 만족하는 좌표만 반환한다:
        ///  1) 해당 칸과 바로 윗칸(y+1)이 모두 공기(hardness &lt;= 0) — 요괴 몸통이 벽에 끼지 않음.
        ///  2) 발밑(y-1)은 고체(hardness &gt; 0) — 공중에 뜬 채로 스폰되지 않음.
        ///  3) 세 칸 모두 맵 범위 안(경계 밖은 후보에서 제외).
        /// maxRange가 크면 후보 칸이 많아질 수 있으므로, 호출 빈도가 높은 실시간 루프보다는 스폰 시도
        /// 시점에 한 번씩 호출하는 용도로 설계했다.
        /// </summary>
        public List<Vector3Int> GetValidSpawnPositions(Vector3Int center, int minRange, int maxRange)
        {
            var results = new List<Vector3Int>();
            if (minRange < 0 || maxRange < minRange) return results;

            var minX = Mathf.Max(0, center.x - maxRange);
            var maxX = Mathf.Min(Width - 1, center.x + maxRange);
            var minY = Mathf.Max(0, center.y - maxRange);
            var maxY = Mathf.Min(Height - 1, center.y + maxRange);
            var minRangeSquared = minRange * minRange;
            var maxRangeSquared = maxRange * maxRange;

            for (var x = minX; x <= maxX; x++)
            {
                var dx = x - center.x;
                for (var y = minY; y <= maxY; y++)
                {
                    var dy = y - center.y;
                    var distanceSquared = dx * dx + dy * dy;
                    if (distanceSquared < minRangeSquared || distanceSquared > maxRangeSquared) continue;

                    var candidate = new Vector3Int(x, y, center.z);
                    if (IsSafeGroundSpawn(candidate)) results.Add(candidate);
                }
            }

            return results;
        }

        private bool IsSafeGroundSpawn(Vector3Int cell)
        {
            var ground = new Vector3Int(cell.x, cell.y - 1, cell.z);
            var head = new Vector3Int(cell.x, cell.y + 1, cell.z);
            if (!InBounds(ground) || !InBounds(cell) || !InBounds(head)) return false;

            return GetTile(cell).IsAir && GetTile(head).IsAir && !GetTile(ground).IsAir;
        }

        // ------------------------------------------------------------------
        // ITileDiffSource — WorldContracts.cs 계약. WorldSaveAdapter는 구조화된
        // TileChangeRecord 리스트(GetTileChangeRecords)를 직접 쓰지만, 이 계약도 유지보수 편의를 위해 채워둔다.
        // ------------------------------------------------------------------
        public int Seed => seed;

        public IReadOnlyList<string> ExportTileDiff()
        {
            var lines = new List<string>(changeLog.Count);
            foreach (var record in changeLog)
                lines.Add($"{record.x},{record.y},{record.z},{record.tileId},{(record.placed ? 1 : 0)}");
            return lines;
        }

        private bool TryResolveDrop(string minedElementType, out ItemDefinition item, out int amount)
        {
            item = null;
            amount = 0;
            if (catalog == null) return false;

            var dropId = DropItemOverrides.TryGetValue(minedElementType, out var mapped) ? mapped : minedElementType;
            if (string.IsNullOrEmpty(dropId)) return false;

            item = catalog.FindItem(dropId);
            if (item == null) return false;

            amount = 1;
            return true;
        }

        private static string ResolveBackgroundFor(string minedElementType) =>
            BackgroundOverrides.TryGetValue(minedElementType, out var background) ? background : WorldTileTypes.BackgroundStone;

        private static int ResolvePlacementHardness(string elementType) =>
            PlacementHardness.TryGetValue(elementType, out var hardness) ? hardness : 1;

        private void ApplyForegroundVisual(Vector3Int cell, string elementType)
        {
            if (renderer == null || renderer.Foreground == null) return;
            TileBase tileBase = null;
            if (!string.IsNullOrEmpty(elementType)) renderer.TryGetTileBase(elementType, out tileBase);
            renderer.Foreground.SetTile(cell, tileBase);
        }

        private void ApplyBackgroundVisual(Vector3Int cell, string elementType)
        {
            if (renderer == null || renderer.Background == null) return;
            TileBase tileBase = null;
            if (!string.IsNullOrEmpty(elementType)) renderer.TryGetTileBase(elementType, out tileBase);
            renderer.Background.SetTile(cell, tileBase);
        }

        /// <summary>
        /// A-14: 셀 하나가 바뀌면 그 칸 자신과 상·하·좌·우 이웃, 딱 5칸의 노출면만 다시 계산해
        /// TilemapRenderer에 갱신을 요청한다. 월드 전체를 순회하지 않으므로 채굴/설치가 아무리 자주
        /// 일어나도 프레임당 비용은 항상 O(1)이다. renderer가 아직 연결되지 않은 로드 검증 단계
        /// (BindRenderer 이전)에서는 조용히 아무 것도 하지 않는다 — ApplyForegroundVisual과 동일한 규칙.
        /// </summary>
        private void RefreshEdgeOverlayAround(Vector3Int cell)
        {
            if (renderer == null) return;

            RefreshEdgeOverlayAt(cell);
            RefreshEdgeOverlayAt(new Vector3Int(cell.x, cell.y + 1, cell.z));
            RefreshEdgeOverlayAt(new Vector3Int(cell.x, cell.y - 1, cell.z));
            RefreshEdgeOverlayAt(new Vector3Int(cell.x - 1, cell.y, cell.z));
            RefreshEdgeOverlayAt(new Vector3Int(cell.x + 1, cell.y, cell.z));
        }

        private void RefreshEdgeOverlayAt(Vector3Int cell)
        {
            if (!InBounds(cell)) return;
            var mask = TileEdgeOverlayResolver.ComputeExposureMask(tiles, cell.x, cell.y, Width, Height);
            renderer.RefreshEdgeOverlay(cell, mask);
        }

        /// <summary>
        /// 같은 칸이 여러 번 바뀌어도 좌표당 최신 상태 하나만 남긴다.
        /// WorldSaveAdapter.CaptureWorld는 (x,y,z) 중복 좌표를 통째로 거부하기 때문에 필수적인 규칙이다.
        /// </summary>
        private void RecordChange(Vector3Int cell, string tileId, bool placed)
        {
            var record = new TileChangeRecord { x = cell.x, y = cell.y, z = cell.z, tileId = tileId, placed = placed };
            if (changeIndexByCell.TryGetValue(cell, out var index))
            {
                changeLog[index] = record;
            }
            else
            {
                changeIndexByCell[cell] = changeLog.Count;
                changeLog.Add(record);
            }
        }
    }
}
