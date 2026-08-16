using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Nyangbingo.World
{
    /// <summary>
    /// MapGenerator가 만든 TileData[,]를 테라리아식 2중 Tilemap(전경/배경벽)에 일괄 렌더링한다.
    /// 순수 렌더링 전용 컴포넌트이며, 채굴/설치 등 게임플레이 변경은 이후 구현될 TileService가
    /// 이 컴포넌트가 들고 있는 Tilemap을 대상으로 직접 SetTile을 호출해 처리한다.
    ///
    /// 전경/배경 판정은 TileData 계약(<see cref="TileData"/>)을 그대로 따른다.
    ///  - 전경: hardness &gt; 0 인 칸만 elementType의 블록을 채운다.
    ///  - 배경: HasBackground인 칸의 backgroundElementType(고체 칸 뒤의 자연 배경 포함).
    /// A-17: EnsureForegroundCollision으로 전경 TilemapCollider2D+CompositeCollider2D를 구성한다.
    /// </summary>
    public sealed class TilemapRenderer : MonoBehaviour
    {
        [Serializable]
        public struct TileVisual
        {
            [Tooltip("WorldTileTypes 상수와 일치해야 하는 elementType ID (예: dirt, stone, bg_dirt)")]
            public string elementType;
            public TileBase tile;
        }

        [Header("2중 레이어 (테라리아식)")]
        [SerializeField] private Tilemap foregroundTilemap;
        [SerializeField] private Tilemap backgroundTilemap;

        [Header("A-14: 타일 노출면 먹선(edge) 오버레이 — 전경/배경과 별도의 3번째 레이어")]
        [Tooltip("먹선 오버레이 전용 Tilemap. foreground/background보다 위(정렬 순서상 더 앞)에 둬야 " +
                 "테두리 선이 타일 위에 덮여 보인다. 비워두면 A-14 오버레이 기능 전체가 조용히 비활성화된다 " +
                 "(기존 씬을 깨지 않기 위한 안전한 기본값).")]
        [SerializeField] private Tilemap edgeOverlayTilemap;

        [Tooltip("공용 edge 모양 5장(TileEdgeOverlayResolver.ShapeStraight/Corner/Through/TJunction/Isolated " +
                 "순서, 인덱스 0~4). 재질별로 다른 스프라이트를 쓰지 않고 모든 지형이 이 5장만 공유한다 — " +
                 "나머지 회전 방향은 Tilemap.SetTransformMatrix로 처리하므로 회전판을 따로 만들 필요 없다. " +
                 "아트 자산이 아직 없다면 이 배열을 비워둬도 컴파일/실행에는 문제없다(오버레이만 그려지지 않음). " +
                 "개발 B/아트팀에게 요청할 에셋 ID 제안: edge_straight, edge_corner, edge_through, " +
                 "edge_tjunction, edge_isolated.")]
        [SerializeField] private TileBase[] edgeShapeTiles = new TileBase[TileEdgeOverlayResolver.ShapeCount];

        [Header("elementType ↔ TileBase 매핑 (드래그앤드롭) — 1순위: 인스펙터 명시 매핑")]
        [SerializeField] private TileVisual[] tileVisuals = Array.Empty<TileVisual>();

        [Header("Ice altar 2x2 art (TL, TR, BL, BR)")]
        [Tooltip("t_altar_0~3 order. The map stores one ice_altar element type, so the renderer " +
                 "selects the correct quadrant from its 2x2 neighbours.")]
        [SerializeField] private TileBase[] iceAltarQuadrantTiles = new TileBase[4];

        [Tooltip("1순위(인스펙터 매핑)가 모두 실패했을 때 빈 칸(투명) 대신 그려줄 최종 대체 타일(2순위). 눈에 띄는 색(예: " +
                 "마젠타)의 더미 타일을 연결해두면 매핑 누락 칸이 '검은 화면'처럼 안 보이는 게 아니라 화면에 " +
                 "바로 도드라져서 원인을 즉시 알 수 있다. 비워두면 기존처럼 완전히 투명하게 처리된다.")]
        [SerializeField] private TileBase fallbackTile;

        [Tooltip("켜두면 매핑이 없는 elementType을 만나도 콘솔에 경고를 남기지 않는다. 시각 자료 없이 " +
                 "로직만 확인하는 회귀 테스트용 더미 렌더러에서만 true로 설정할 것 — 실제 게임 씬에서는 " +
                 "매핑 누락을 바로 알 수 있어야 하므로 항상 false로 둔다.")]
        [SerializeField] private bool suppressMissingTileWarning;

        public Tilemap Foreground => foregroundTilemap;
        public Tilemap Background => backgroundTilemap;
        public static readonly Vector3 TerrainVisualAnchor = new Vector3(.5f, 0f, 0f);

        /// <summary>
        /// Gameplay/world-visual coordinate contract. Callers must use the rendered foreground
        /// Tilemap instead of duplicating FloorToInt and x+0.5 assumptions. This keeps mining,
        /// placement, seal diagnostics and Tilemap art aligned if the Grid origin, scale or cell
        /// anchor changes later.
        ///
        /// 납품 지형 아트는 하단 피벗(0.5,0)이므로 tileAnchor도 <see cref="TerrainVisualAnchor"/>다.
        /// 게임플레이 셀 중심은 tileAnchor와 독립적으로 격자 코너에서 계산한다.
        /// </summary>
        public Vector3Int WorldToCell(Vector3 worldPosition)
        {
            if (foregroundTilemap == null) return Vector3Int.FloorToInt(worldPosition);

            var size = foregroundTilemap.cellSize;
            var anchor = foregroundTilemap.tileAnchor;
            // visualBottom = cellOrigin + anchor*size
            // X: 하단-중앙 피벗 → visualLeft = cellOrigin.x + (anchor.x - 0.5)*size.x
            var adjusted = new Vector3(
                worldPosition.x - (anchor.x - .5f) * size.x,
                worldPosition.y - anchor.y * size.y,
                worldPosition.z);
            return foregroundTilemap.WorldToCell(adjusted);
        }

        public Vector3 GetCellCenterWorld(Vector3Int cell)
        {
            if (foregroundTilemap == null)
                return new Vector3(cell.x + .5f, cell.y + .5f, cell.z);

            // Tilemap.GetCellCenterWorld follows Tilemap.tileAnchor. Delivered terrain uses a
            // bottom pivot, so the visual anchor is (0.5, 0) while gameplay still needs the
            // geometric cell center. Derive it from the Grid corners to keep rendering and
            // collision coordinates independent.
            var bottomLeft = foregroundTilemap.CellToWorld(cell);
            var topRight = foregroundTilemap.CellToWorld(
                cell + Vector3Int.right + Vector3Int.up);
            return (bottomLeft + topRight) * .5f;
        }

        /// <summary>
        /// 전경 타일 스프라이트 피벗이 놓이는 월드 좌표(tileAnchor 위치).
        /// </summary>
        public Vector3 GetTilePivotWorld(Vector3Int cell)
        {
            if (foregroundTilemap == null)
                return new Vector3(cell.x + DefaultTileAnchor.x, cell.y + DefaultTileAnchor.y, cell.z);

            var origin = foregroundTilemap.CellToWorld(cell);
            var size = foregroundTilemap.cellSize;
            var anchor = foregroundTilemap.tileAnchor;
            return origin + Vector3.Scale(anchor, size);
        }

        /// <summary>타일 비주얼 앵커(하단 피벗 아트 기준). OpenDoor·장식 정렬에 사용.</summary>
        public Vector3 GetCellVisualAnchorWorld(Vector3Int cell)
        {
            if (foregroundTilemap == null)
                return new Vector3(cell.x + .5f, cell.y, cell.z);
            return foregroundTilemap.LocalToWorld(
                foregroundTilemap.CellToLocalInterpolated(
                    (Vector3)cell + foregroundTilemap.tileAnchor));
        }

        public Bounds GetCellWorldBounds(Vector3Int cell)
        {
            var bottomLeft = foregroundTilemap != null
                ? foregroundTilemap.CellToWorld(cell)
                : new Vector3(cell.x, cell.y, cell.z);
            var topRight = foregroundTilemap != null
                ? foregroundTilemap.CellToWorld(cell + Vector3Int.right + Vector3Int.up)
                : new Vector3(cell.x + 1f, cell.y + 1f, cell.z);
            var bounds = new Bounds((bottomLeft + topRight) * .5f, Vector3.zero);
            bounds.Encapsulate(bottomLeft);
            bounds.Encapsulate(topRight);
            return bounds;
        }

        /// <summary>
        /// 하단 피벗·약 1칸 높이 지형 타일의 보이는 중심.
        /// tileAnchor(0.5,0.5)일 때 GetCellCenterWorld보다 0.5칸 위에 있다.
        /// </summary>
        public Vector3 GetTileVisualCenterWorld(Vector3Int cell)
        {
            var pivot = GetTilePivotWorld(cell);
            var sizeY = foregroundTilemap != null ? foregroundTilemap.cellSize.y : 1f;
            return pivot + new Vector3(0f, sizeY * .5f, 0f);
        }

        public bool HasForegroundTile(Vector3Int cell) => foregroundTilemap != null &&
                                                          foregroundTilemap.HasTile(cell);

        public bool HasForegroundCollision(Vector3Int cell)
        {
            if (foregroundComposite == null && foregroundTilemap != null)
                foregroundComposite = foregroundTilemap.GetComponent<CompositeCollider2D>();
            return foregroundComposite != null && foregroundComposite.enabled &&
                   foregroundComposite.OverlapPoint(GetCellCenterWorld(cell));
        }

        public void GetCellWorldCorners(Vector3Int cell, Vector3[] corners, float inset = 0f)
        {
            if (corners == null || corners.Length < 4)
                throw new ArgumentException("Four corner slots are required.", nameof(corners));

            if (foregroundTilemap != null)
            {
                corners[0] = foregroundTilemap.CellToWorld(cell);
                corners[1] = foregroundTilemap.CellToWorld(cell + Vector3Int.right);
                corners[2] = foregroundTilemap.CellToWorld(cell + Vector3Int.right + Vector3Int.up);
                corners[3] = foregroundTilemap.CellToWorld(cell + Vector3Int.up);
            }
            else
            {
                corners[0] = new Vector3(cell.x, cell.y, cell.z);
                corners[1] = new Vector3(cell.x + 1f, cell.y, cell.z);
                corners[2] = new Vector3(cell.x + 1f, cell.y + 1f, cell.z);
                corners[3] = new Vector3(cell.x, cell.y + 1f, cell.z);
            }

            if (inset <= 0f) return;
            var center = GetCellCenterWorld(cell);
            var factor = Mathf.Clamp01(1f - inset * 2f);
            for (var index = 0; index < 4; index++)
                corners[index] = Vector3.Lerp(center, corners[index], factor);
        }

        // 초기값을 빈 딕셔너리로 잡아둔다 — RebuildLookupTable()이 "성급한 0개 갱신"을 막느라 조기
        // 반환하는 경로를 타더라도 _lookup 자체는 항상 non-null이라, ResolveTile/TryGetTileBase가
        // NullReferenceException 없이 안전하게 동작한다.
        private Dictionary<string, TileBase> _lookup = new Dictionary<string, TileBase>();
        private readonly Dictionary<int, TileBase> _wallpaperTileByRow = new Dictionary<int, TileBase>();
        private readonly Dictionary<string, Tile> _runtimeTiles = new Dictionary<string, Tile>(StringComparer.Ordinal);
        private readonly List<Tile> _runtimeEdgeTiles = new List<Tile>();
        private readonly List<Sprite> _runtimeEdgeSprites = new List<Sprite>();
        private readonly List<Texture2D> _runtimeEdgeTextures = new List<Texture2D>();

        private CompositeCollider2D foregroundComposite;
        private TilemapCollider2D foregroundTilemapCollider;
        private GameObject runtimeEdgeOverlayObject;

        /// <summary>
        /// 지형/배경 aseprite는 하단 피벗(0.5,0). 렌더 앵커는 <see cref="TerrainVisualAnchor"/>,
        /// 게임플레이 셀 중심은 <see cref="GetCellCenterWorld"/>가 격자 기준으로 계산한다.
        /// 중앙 앵커가 필요한 별도 오버레이는 <see cref="DefaultTileAnchor"/>를 직접 지정한다.
        /// </summary>
        public static readonly Vector3 DefaultTileAnchor = new Vector3(.5f, .5f, 0f);

        private const int RuntimeEdgeTextureSize = 16;
        private static readonly Color32 RuntimeEdgeInkColor = new Color32(0x1A, 0x1A, 0x24, 0xFF);
        private static readonly TileEdgeMask[] RuntimeEdgeBaseMasks =
        {
            TileEdgeMask.Top,
            TileEdgeMask.Top | TileEdgeMask.Right,
            TileEdgeMask.Top | TileEdgeMask.Bottom,
            TileEdgeMask.Top | TileEdgeMask.Right | TileEdgeMask.Bottom,
            TileEdgeMask.Top | TileEdgeMask.Right | TileEdgeMask.Bottom | TileEdgeMask.Left,
        };

        private void Awake()
        {
            RebuildLookupTable();
            EnsureTileAnchors();
            EnsureForegroundCollision();
            EnsureEdgeOverlayWiring();
        }

        /// <summary>
        /// 납품 지형 스프라이트는 하단 피벗이므로 Tilemap visual anchor를 논리 셀 하단 가장자리에 둔다.
        /// 충돌·타겟팅은 기하 셀 bounds를 유지한다.
        /// </summary>
        public void EnsureWorldCoordinateContract()
        {
            if (foregroundTilemap != null)
                foregroundTilemap.tileAnchor = TerrainVisualAnchor;
            if (backgroundTilemap != null)
                backgroundTilemap.tileAnchor = TerrainVisualAnchor;
        }

        /// <summary>
        /// 전경·배경·엣지 오버레이에 월드 좌표 계약을 적용한다.
        /// </summary>
        public void EnsureTileAnchors()
        {
            EnsureWorldCoordinateContract();
            if (edgeOverlayTilemap != null)
                edgeOverlayTilemap.tileAnchor = TerrainVisualAnchor;
        }

        /// <summary>
        /// 에디터 자동화 스크립트(A-01 SetupDevATileAssets 등)가 tileVisuals/fallbackTile을 코드로 직접
        /// 설정할 때 쓰는 진입점. SerializedObject/SerializedProperty를 거치지 않고 이 클래스 내부에서
        /// 필드에 바로 대입하므로, Editor 직렬화 마샬링 타이밍에 좌우되지 않고 항상 확정적으로 반영된다.
        ///
        /// 주의: 여기서는 절대로 RebuildLookupTable()을 호출하지 않는다 — 호출자가 AssetDatabase 저장/
        /// SaveScene까지 전부 끝낸 뒤 딱 한 번만 명시적으로 호출해야, 디스크 반영이 끝나기도 전에 캐시가
        /// 먼저 굳어버리는(그리고 그 결과가 "0개 갱신"으로 로그에 찍히는) 시간차 문제를 피할 수 있다.
        /// 호출 후에는 EditorUtility.SetDirty(renderer) + 씬 저장을 별도로 해줘야 디스크에 영구 반영된다.
        /// </summary>
        public void SetTileVisualsForEditorSetup(TileVisual[] visuals, TileBase newFallbackTile)
        {
            tileVisuals = visuals ?? Array.Empty<TileVisual>();
            fallbackTile = newFallbackTile;
        }

        public void SetIceAltarQuadrantTilesForEditorSetup(TileBase[] tiles)
        {
            iceAltarQuadrantTiles = tiles != null && tiles.Length == 4
                ? tiles
                : new TileBase[4];
        }

        public bool HasIceAltarQuadrantArt => iceAltarQuadrantTiles != null &&
                                             iceAltarQuadrantTiles.Length == 4 &&
                                             Array.TrueForAll(iceAltarQuadrantTiles, tile => tile != null);

        /// <summary>
        /// tileVisuals(인스펙터 매핑)를 기준으로 조회용 딕셔너리를 처음부터 다시 만든다. 런타임에는
        /// Awake()가 자동으로 호출하므로 직접 부를 필요가 없지만, 에디터 스크립트가 tileVisuals를
        /// 코드로 갱신한 직후(예: SetupDevATileAssets) 실제로 몇 개가 유효하게 등록됐는지 즉시 확인하고
        /// 싶을 때, 또는 런타임에 tileVisuals를 동적으로 바꾼 뒤 캐시를 갱신하고 싶을 때 호출한다.
        /// </summary>
        public void RebuildLookupTable()
        {
            // tileVisuals가 아직 비어 있다면(=배선 대기 중) 굳이 0개짜리 테이블로 덮어써서 "굳혀"버리지
            // 않고 여기서 탈출한다. 기존에 이미 유효한 매핑이 캐싱돼 있었다면 그대로 보존되므로, 배선이
            // 아직 안 끝난 시점에 실수로 호출되어도 이전의 정상 캐시를 0개로 뭉개는 사고를 막을 수 있다.
            if (tileVisuals == null || tileVisuals.Length == 0)
            {
                Debug.LogWarning("[Nyangbingo] TilemapRenderer: tileVisuals가 비어 있어 룩업 테이블 갱신을 " +
                                 "보류합니다. (배선 대기 중)");
                return;
            }

            _lookup = new Dictionary<string, TileBase>(tileVisuals.Length);
            foreach (var visual in tileVisuals)
            {
                if (string.IsNullOrEmpty(visual.elementType) || visual.tile == null) continue;
                if (!_lookup.TryAdd(visual.elementType, visual.tile))
                {
                    Debug.LogWarning($"[Nyangbingo] TilemapRenderer: elementType '{visual.elementType}' 매핑이 " +
                                      "중복 등록되어 있습니다. 첫 번째 항목만 사용합니다.");
                }
            }
            Debug.Log($"[Nyangbingo] TilemapRenderer: 룩업 테이블이 {_lookup.Count}개의 타일로 갱신되었습니다.");
        }

        /// <summary>
        /// MapGenerator.Generate(seed)/GenerateDetailed(seed) 결과 배열을 전경·배경 Tilemap에
        /// 한 번에 그린다. 400x160(=64,000칸) 규모를 개별 SetTile 반복 없이 SetTilesBlock 두 번으로 처리한다.
        /// </summary>
        public void RenderWorld(TileData[,] tiles)
        {
            if (tiles == null)
            {
                Debug.LogError("[Nyangbingo] TilemapRenderer.RenderWorld: tiles가 null입니다.");
                return;
            }

            if (foregroundTilemap == null || backgroundTilemap == null)
            {
                Debug.LogError("[Nyangbingo] TilemapRenderer: foreground/background Tilemap이 인스펙터에 연결되지 않았습니다.");
                return;
            }

            if (_lookup == null) RebuildLookupTable();
            EnsureTileAnchors();
            EnsureEdgeOverlayWiring();

            var width = tiles.GetLength(0);
            var height = tiles.GetLength(1);
            if (width <= 0 || height <= 0) return;

            var foregroundBlock = new TileBase[width * height];
            var backgroundBlock = new TileBase[width * height];
            HashSet<string> missing = null;
            RebuildWallpaperRowLookup(tiles, width, height, ref missing);

            // Tilemap.SetTilesBlock은 배열을 x가 가장 빠르게, 그다음 y 순서로 읽는다 (index = y*width + x).
            for (var y = 0; y < height; y++)
            {
                var rowOffset = y * width;
                for (var x = 0; x < width; x++)
                {
                    var tile = tiles[x, y];
                    var index = rowOffset + x;

                    foregroundBlock[index] = tile.hardness > 0
                        ? ResolveForegroundTile(tiles, x, y, tile.elementType, ref missing)
                        : null;

                    // A-16: 전경 고체 뒤에도 자연 배경을 그린다. 동굴·하늘(빈 배경)은 null.
                    backgroundBlock[index] = tile.HasBackground
                        ? ResolveBackgroundTile(tile.backgroundElementType, y, ref missing)
                        : null;
                }
            }

            if (missing != null && missing.Count > 0 && !suppressMissingTileWarning)
            {
                var sb = new StringBuilder("[Nyangbingo] TilemapRenderer: 다음 elementType에 TileBase 매핑이 없어 빈 칸으로 처리했습니다: ");
                sb.Append(string.Join(", ", missing));
                Debug.LogWarning(sb.ToString());
            }

            var bounds = new BoundsInt(0, 0, 0, width, height, 1);

            EnsureForegroundCollision();

            foregroundTilemap.ClearAllTiles();
            backgroundTilemap.ClearAllTiles();
            foregroundTilemap.SetTilesBlock(bounds, foregroundBlock);
            backgroundTilemap.SetTilesBlock(bounds, backgroundBlock);
            foregroundTilemap.RefreshAllTiles();
            backgroundTilemap.RefreshAllTiles();
            NotifyForegroundCollisionDirty();
            EnsureWorldBoundaries(width, height);

            // A-14: 월드 전체 먹선 오버레이 최초 1회 계산. 이후에는 TileService가 변경된 셀 +
            // 그 4방향 이웃만 RefreshEdgeOverlay로 갱신하므로, 이 전체 순회는 월드 생성/로드당 딱 한 번뿐이다.
            RebuildEdgeOverlayForWorld(tiles);
        }

        private void EnsureWorldBoundaries(int width, int height)
        {
            if (foregroundTilemap == null || width <= 0 || height <= 0) return;
            var root = foregroundTilemap.transform.Find("RuntimeWorldBoundaries");
            if (root == null)
            {
                var rootObject = new GameObject("RuntimeWorldBoundaries");
                rootObject.transform.SetParent(foregroundTilemap.transform, false);
                root = rootObject.transform;
            }

            var worldLeft = foregroundTilemap.CellToWorld(Vector3Int.zero).x;
            var worldRight = foregroundTilemap.CellToWorld(new Vector3Int(width, 0, 0)).x;
            var worldBottom = foregroundTilemap.CellToWorld(Vector3Int.zero).y;
            var worldTop = foregroundTilemap.CellToWorld(new Vector3Int(0, height, 0)).y;
            var worldHeight = Mathf.Max(1f, worldTop - worldBottom);
            ConfigureBoundary(root, "WorldBoundaryLeft", worldLeft - .5f,
                worldBottom + worldHeight * .5f, worldHeight * 3f);
            ConfigureBoundary(root, "WorldBoundaryRight", worldRight + .5f,
                worldBottom + worldHeight * .5f, worldHeight * 3f);
        }

        private static void ConfigureBoundary(Transform root, string name, float centerX, float centerY,
            float height)
        {
            var child = root.Find(name);
            if (child == null)
            {
                var childObject = new GameObject(name);
                childObject.transform.SetParent(root, false);
                child = childObject.transform;
            }
            child.position = new Vector3(centerX, centerY, 0f);
            var collider = child.GetComponent<BoxCollider2D>();
            if (collider == null) collider = child.gameObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = false;
            collider.size = new Vector2(1f, height);
        }

        private TileBase ResolveForegroundTile(TileData[,] tiles, int x, int y, string elementType,
            ref HashSet<string> missing)
        {
            if (!string.Equals(elementType, WorldTileTypes.IceAltar, StringComparison.Ordinal) ||
                !HasIceAltarQuadrantArt)
                return ResolveTile(elementType, ref missing);

            var width = tiles.GetLength(0);
            var height = tiles.GetLength(1);
            var hasLeft = IsIceAltar(tiles, x - 1, y, width, height);
            var hasRight = IsIceAltar(tiles, x + 1, y, width, height);
            var hasDown = IsIceAltar(tiles, x, y - 1, width, height);
            var hasUp = IsIceAltar(tiles, x, y + 1, width, height);

            // Art order is 0=top-left, 1=top-right, 2=bottom-left, 3=bottom-right.
            var isRight = hasLeft && !hasRight;
            var isTop = hasDown && !hasUp;
            var quadrant = (isTop ? 0 : 2) + (isRight ? 1 : 0);
            return iceAltarQuadrantTiles[quadrant];
        }

        private static bool IsIceAltar(TileData[,] tiles, int x, int y, int width, int height) =>
            x >= 0 && x < width && y >= 0 && y < height &&
            string.Equals(tiles[x, y].elementType, WorldTileTypes.IceAltar, StringComparison.Ordinal);

        /// <summary>
        /// A-17/A-21: 전경 Tilemap에 TilemapCollider2D + CompositeCollider2D + Static Rigidbody2D를 구성한다.
        /// 배경 Tilemap에는 Collider를 붙이지 않는다.
        /// 좌표 계약: 논리 셀 (x,y)의 월드 AABB는 Grid/Tilemap 기본 Cell Size(1,1) 기준 [x,x+1]×[y,y+1],
        /// 중심 GetCellCenterWorld ≈ (x+0.5, y+0.5). tileAnchor 기본 (0.5,0.5) —
        /// 하단 피벗 아트는 논리 셀보다 0.5칸 위에 그려지며, 클릭은 WorldToCell이 보정한다.
        /// Collider는 타일 점유 셀 경계를 따르며 스프라이트 피벗에 의존하지 않는다.
        /// </summary>
        public void EnsureForegroundCollision()
        {
            if (foregroundTilemap == null) return;
            EnsureTileAnchors();

            var fgGo = foregroundTilemap.gameObject;
            foregroundTilemapCollider = fgGo.GetComponent<TilemapCollider2D>();
            if (foregroundTilemapCollider == null)
                foregroundTilemapCollider = fgGo.AddComponent<TilemapCollider2D>();
            foregroundTilemapCollider.compositeOperation = Collider2D.CompositeOperation.Merge;

            var body = fgGo.GetComponent<Rigidbody2D>();
            if (body == null) body = fgGo.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            body.simulated = true;

            foregroundComposite = fgGo.GetComponent<CompositeCollider2D>();
            if (foregroundComposite == null) foregroundComposite = fgGo.AddComponent<CompositeCollider2D>();
            foregroundComposite.geometryType = CompositeCollider2D.GeometryType.Polygons;

            // 배경에는 Collider를 두지 않는다.
            if (backgroundTilemap != null)
            {
                var bgCollider = backgroundTilemap.GetComponent<TilemapCollider2D>();
                if (bgCollider != null) DestroyComponentSafe(bgCollider);
                var bgComposite = backgroundTilemap.GetComponent<CompositeCollider2D>();
                if (bgComposite != null) DestroyComponentSafe(bgComposite);
                var bgBody = backgroundTilemap.GetComponent<Rigidbody2D>();
                if (bgBody != null) DestroyComponentSafe(bgBody);
            }
        }

        /// <summary>전경 타일 변경 후 CompositeCollider가 형상을 다시 합치도록 알린다.</summary>
        public void NotifyForegroundCollisionDirty()
        {
            if (foregroundTilemap == null) return;
            if (foregroundTilemapCollider == null)
                foregroundTilemapCollider = foregroundTilemap.GetComponent<TilemapCollider2D>();
            if (foregroundComposite == null) foregroundComposite = foregroundTilemap.GetComponent<CompositeCollider2D>();
            // SetTile is visual/data-immediate, but TilemapCollider2D otherwise batches its shape
            // changes until LateUpdate. Mining and movement happen in the same frame, so explicitly
            // process the Tilemap change before regenerating the Composite geometry. Toggling only
            // the Composite component rebuilds from stale TilemapCollider shapes and leaves an
            // invisible solid block behind.
            // UnityEngine.Object uses a custom null state for destroyed or missing components.
            // The C# null-conditional operator bypasses that check and can still invoke a native
            // property on a missing TilemapCollider2D, so use Unity's explicit null comparison.
            if (foregroundTilemapCollider != null && foregroundTilemapCollider.hasTilemapChanges)
                foregroundTilemapCollider.ProcessTilemapChanges();
            if (foregroundComposite != null)
                foregroundComposite.GenerateGeometry();
            Physics2D.SyncTransforms();
        }

        private static void DestroyComponentSafe(Component component)
        {
            if (component == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(component);
            else UnityEngine.Object.DestroyImmediate(component);
        }

        /// <summary>
        /// A-14: 월드 전체에 대해 먹선 오버레이를 처음부터 다시 계산한다. RenderWorld(최초 생성/로드 복원 후
        /// 1회)에서만 호출되며, 그 외에는 TileService.RefreshEdgeOverlayAround가 변경 셀 주변만 국소 갱신한다.
        /// edgeOverlayTilemap이 연결돼 있지 않으면(아트 미배선) 조용히 아무 것도 하지 않는다.
        /// </summary>
        public void RebuildEdgeOverlayForWorld(TileData[,] tiles)
        {
            EnsureEdgeOverlayWiring();
            if (edgeOverlayTilemap == null || tiles == null) return;

            var width = tiles.GetLength(0);
            var height = tiles.GetLength(1);
            edgeOverlayTilemap.ClearAllTiles();

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var mask = TileEdgeOverlayResolver.ComputeExposureMask(tiles, x, y, width, height);
                    if (mask == TileEdgeMask.None) continue;
                    RefreshEdgeOverlay(new Vector3Int(x, y, 0), mask);
                }
            }
        }

        /// <summary>
        /// A-14: 셀 하나의 먹선 오버레이만 갱신한다(변경 셀·인접 셀 국소 갱신용, TileService가 호출).
        /// mask가 None이면 해당 칸의 오버레이 타일을 지운다. edgeOverlayTilemap 미연결 또는 대응하는
        /// 모양 스프라이트가 배선되지 않았으면 조용히 무시한다(아트 자산 도착 전에도 컴파일·플레이 가능).
        /// </summary>
        public void RefreshEdgeOverlay(Vector3Int cell, TileEdgeMask mask)
        {
            if (edgeOverlayTilemap == null) return;

            if (mask == TileEdgeMask.None || !TileEdgeOverlayResolver.TryResolve(mask, out var shapeIndex, out var rotationSteps))
            {
                edgeOverlayTilemap.SetTile(cell, null);
                return;
            }

            var tile = (edgeShapeTiles != null && shapeIndex >= 0 && shapeIndex < edgeShapeTiles.Length)
                ? edgeShapeTiles[shapeIndex]
                : null;

            edgeOverlayTilemap.SetTile(cell, tile);
            if (tile != null) edgeOverlayTilemap.SetTransformMatrix(cell, TileEdgeOverlayResolver.BuildRotationMatrix(rotationSteps));
        }

        /// <summary>
        /// Edge overlays are optional legacy art. The current terrain sprites already contain their
        /// intended borders, so a scene without an explicitly assigned overlay must stay that way.
        /// Automatically creating a one-pixel ink overlay here produced black seams around caves.
        /// </summary>
        public void EnsureEdgeOverlayWiring()
        {
            if (foregroundTilemap == null || edgeOverlayTilemap == null) return;

            EnsureRuntimeEdgeShapeTiles();
        }

        private void EnsureRuntimeEdgeShapeTiles()
        {
            if (edgeShapeTiles == null || edgeShapeTiles.Length != TileEdgeOverlayResolver.ShapeCount)
            {
                var previous = edgeShapeTiles;
                edgeShapeTiles = new TileBase[TileEdgeOverlayResolver.ShapeCount];
                if (previous != null)
                    Array.Copy(previous, edgeShapeTiles, Mathf.Min(previous.Length, edgeShapeTiles.Length));
            }

            for (var index = 0; index < edgeShapeTiles.Length; index++)
            {
                if (edgeShapeTiles[index] != null) continue;
                edgeShapeTiles[index] = CreateRuntimeEdgeTile(RuntimeEdgeBaseMasks[index], index);
            }
        }

        private Tile CreateRuntimeEdgeTile(TileEdgeMask mask, int shapeIndex)
        {
            var texture = new Texture2D(RuntimeEdgeTextureSize, RuntimeEdgeTextureSize, TextureFormat.RGBA32, false)
            {
                name = $"RuntimeEdgeTexture_{shapeIndex}",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };

            var pixels = new Color32[RuntimeEdgeTextureSize * RuntimeEdgeTextureSize];
            if ((mask & TileEdgeMask.Top) != 0)
                DrawRuntimeEdgeHorizontal(pixels, RuntimeEdgeTextureSize - 1);
            if ((mask & TileEdgeMask.Bottom) != 0)
                DrawRuntimeEdgeHorizontal(pixels, 0);
            if ((mask & TileEdgeMask.Left) != 0)
                DrawRuntimeEdgeVertical(pixels, 0);
            if ((mask & TileEdgeMask.Right) != 0)
                DrawRuntimeEdgeVertical(pixels, RuntimeEdgeTextureSize - 1);
            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, RuntimeEdgeTextureSize, RuntimeEdgeTextureSize),
                new Vector2(.5f, .5f),
                RuntimeEdgeTextureSize,
                0,
                SpriteMeshType.FullRect);
            sprite.name = $"RuntimeEdgeSprite_{shapeIndex}";
            sprite.hideFlags = HideFlags.DontSave;

            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = $"RuntimeEdgeTile_{shapeIndex}";
            tile.sprite = sprite;
            tile.color = Color.white;
            tile.colliderType = Tile.ColliderType.None;
            tile.hideFlags = HideFlags.DontSave;

            _runtimeEdgeTextures.Add(texture);
            _runtimeEdgeSprites.Add(sprite);
            _runtimeEdgeTiles.Add(tile);
            return tile;
        }

        private static void DrawRuntimeEdgeHorizontal(Color32[] pixels, int y)
        {
            var offset = y * RuntimeEdgeTextureSize;
            for (var x = 0; x < RuntimeEdgeTextureSize; x++)
                pixels[offset + x] = RuntimeEdgeInkColor;
        }

        private static void DrawRuntimeEdgeVertical(Color32[] pixels, int x)
        {
            for (var y = 0; y < RuntimeEdgeTextureSize; y++)
                pixels[y * RuntimeEdgeTextureSize + x] = RuntimeEdgeInkColor;
        }

        /// <summary>이후 TileService(채굴)가 elementType → TileBase를 조회할 때도 재사용할 수 있게 공개한다.</summary>
        public bool TryGetTileBase(string elementType, out TileBase tile)
        {
            if (_lookup == null) RebuildLookupTable();
            if (string.IsNullOrEmpty(elementType))
            {
                tile = null;
                return false;
            }

            if (_lookup.TryGetValue(elementType, out tile) && tile != null) return true;

            // 깊이 정보가 없는 범용 조회의 안전 폴백. 실제 월드 렌더/설치는
            // TryGetWallpaperTileBase로 해당 행의 배경 3종 중 하나를 선택한다.
            if (string.Equals(elementType, WorldTileTypes.Wallpaper, StringComparison.Ordinal) &&
                _lookup.TryGetValue(WorldTileTypes.BackgroundDirt, out tile) && tile != null)
                return true;

            // A-16: t_bg_* ↔ bg_* 별칭.
            var canonical = TileIdAlias.ToCanonical(elementType);
            if (!string.Equals(canonical, elementType, StringComparison.Ordinal) &&
                _lookup.TryGetValue(canonical, out tile) && tile != null)
                return true;

            var visualFallback = ResourceVisualFallbackId(canonical);
            if (!string.IsNullOrEmpty(visualFallback) &&
                _lookup.TryGetValue(visualFallback, out tile) && tile != null)
                return true;

            tile = null;
            return false;
        }

        /// <summary>
        /// Registers delivered building art for product boundary tiles without requiring a duplicate Tile asset.
        /// The runtime Tile uses grid collision and participates in the same foreground Tilemap as terrain.
        /// </summary>
        public void RegisterRuntimeForegroundTile(string elementType, Sprite sprite)
        {
            if (string.IsNullOrWhiteSpace(elementType) || sprite == null) return;
            if (_runtimeTiles.TryGetValue(elementType, out var existing) && existing != null)
            {
                existing.sprite = sprite;
                _lookup[elementType] = existing;
                return;
            }

            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = $"Runtime_{elementType}";
            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.Grid;
            _runtimeTiles[elementType] = tile;
            _lookup[elementType] = tile;
        }

        /// <summary>투명 스프라이트 + Grid 충돌. 1x2 문 위칸처럼 비주얼은 아래 칸이 담당할 때 쓴다.</summary>
        public void RegisterRuntimeColliderOnlyForegroundTile(string elementType)
        {
            if (string.IsNullOrWhiteSpace(elementType)) return;
            if (_runtimeTiles.TryGetValue(elementType, out var existing) && existing != null)
            {
                _lookup[elementType] = existing;
                return;
            }

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = $"Runtime_{elementType}_Tex"
            };
            texture.SetPixel(0, 0, Color.clear);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            sprite.name = $"Runtime_{elementType}_Sprite";
            RegisterRuntimeForegroundTile(elementType, sprite);
        }

        private void OnDestroy()
        {
            foreach (var tile in _runtimeTiles.Values)
            {
                if (tile == null) continue;
                if (Application.isPlaying) Destroy(tile);
                else DestroyImmediate(tile);
            }
            _runtimeTiles.Clear();

            foreach (var tile in _runtimeEdgeTiles)
                DestroyRuntimeObject(tile);
            foreach (var sprite in _runtimeEdgeSprites)
                DestroyRuntimeObject(sprite);
            foreach (var texture in _runtimeEdgeTextures)
                DestroyRuntimeObject(texture);
            _runtimeEdgeTiles.Clear();
            _runtimeEdgeSprites.Clear();
            _runtimeEdgeTextures.Clear();

            if (runtimeEdgeOverlayObject != null)
                DestroyRuntimeObject(runtimeEdgeOverlayObject);
        }

        private static void DestroyRuntimeObject(UnityEngine.Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }

        /// <summary>벽지는 전용 아트 없이 해당 깊이의 t_bg_dirt/stone/deep를 재사용한다.</summary>
        public bool TryGetWallpaperTileBase(int cellY, out TileBase tile)
        {
            if (_wallpaperTileByRow.TryGetValue(cellY, out tile) && tile != null) return true;
            return _lookup.TryGetValue(WorldTileTypes.BackgroundDirt, out tile) && tile != null;
        }

        private TileBase ResolveBackgroundTile(string elementType, int cellY, ref HashSet<string> missing)
        {
            if (string.Equals(TileIdAlias.ToCanonical(elementType), WorldTileTypes.Wallpaper,
                    StringComparison.Ordinal) && TryGetWallpaperTileBase(cellY, out var wallpaperTile))
                return wallpaperTile;
            return ResolveTile(elementType, ref missing);
        }

        private void RebuildWallpaperRowLookup(TileData[,] tiles, int width, int height,
            ref HashSet<string> missing)
        {
            _wallpaperTileByRow.Clear();
            for (var y = 0; y < height; y++)
            {
                var visualId = WorldTileTypes.BackgroundDirt;
                for (var x = 0; x < width; x++)
                {
                    var naturalId = TileIdAlias.ToCanonical(tiles[x, y].naturalBackgroundElementType);
                    if (naturalId != WorldTileTypes.BackgroundDirt &&
                        naturalId != WorldTileTypes.BackgroundStone &&
                        naturalId != WorldTileTypes.BackgroundDeep) continue;
                    visualId = naturalId;
                    break;
                }

                var tile = ResolveTile(visualId, ref missing);
                if (tile != null) _wallpaperTileByRow[y] = tile;
            }
        }

        /// <summary>
        /// 2단계 안전장치: 1) 인스펙터 명시 매핑 → 2) 최종 fallbackTile.
        /// 모든 elementType은 인스펙터에 직접 등록되어 있어야 하며, 런타임 동적 로드는 하지 않는다.
        /// </summary>
        private TileBase ResolveTile(string elementType, ref HashSet<string> missing)
        {
            if (string.IsNullOrEmpty(elementType)) return null;
            elementType = TileIdAlias.ToCanonical(elementType);

            // 1순위: 인스펙터에 직접 드래그앤드롭으로 연결해둔 명시적 매핑.
            if (_lookup.TryGetValue(elementType, out var explicitTile) && explicitTile != null)
                return explicitTile;

            if (string.Equals(elementType, WorldTileTypes.Wallpaper, StringComparison.Ordinal) &&
                _lookup.TryGetValue(WorldTileTypes.BackgroundDirt, out var wallpaperFallback) &&
                wallpaperFallback != null)
                return wallpaperFallback;

            // v72 버섯 3종은 전용 아트 납품 전에도 채굴 노드가 투명해지지 않도록
            // 같은 지층의 기존 자원 타일을 임시 시각 폴백으로 사용한다. 데이터/채굴 ID는 원본을 유지한다.
            var visualFallback = ResourceVisualFallbackId(elementType);
            if (!string.IsNullOrEmpty(visualFallback) &&
                _lookup.TryGetValue(visualFallback, out var fallbackResourceTile) &&
                fallbackResourceTile != null)
                return fallbackResourceTile;

            // 2순위: 최종 폴백 타일(설정돼 있으면 화면에서 바로 눈에 띔), 없으면 투명 빈 칸.
            missing ??= new HashSet<string>();
            missing.Add(elementType);
            return fallbackTile;
        }

        public static string ResourceVisualFallbackId(string elementType) => elementType switch
        {
            WorldTileTypes.OysterMushroom => WorldTileTypes.Clay,
            WorldTileTypes.Shiitake => WorldTileTypes.IceShard,
            WorldTileTypes.Seogi => WorldTileTypes.FrostEssence,
            _ => null
        };

        /// <summary>
        /// 인스펙터에서 우클릭 → 실행하면 WorldTileTypes에 정의된 모든 elementType 슬롯을
        /// 자동으로 채워준다(TileBase는 비워둔 채). 타이핑 오타를 방지하기 위한 편의 기능.
        /// </summary>
        [ContextMenu("알려진 elementType 슬롯 전부 채우기")]
        private void PopulateKnownElementTypes()
        {
            var merged = new List<TileVisual>(tileVisuals);
            var added = MergeKnownElementTypes(merged);
            tileVisuals = merged.ToArray();
            Debug.Log($"[Nyangbingo] TilemapRenderer: elementType 슬롯 {added}개를 새로 채웠습니다. " +
                      "각 슬롯에 TileBase를 드래그해서 연결하세요.");
        }

        /// <summary>
        /// WorldTileTypes에 정의된 모든 elementType(Air 제외)을 대상 리스트에 병합한다.
        /// 이미 존재하는 elementType은 건드리지 않는다. 반환값은 새로 추가된 슬롯 수.
        /// Editor 스크립트(Assembly-CSharp-Editor)에서도 씬 자동 구성 시 재사용하므로 public이어야 한다.
        /// </summary>
        public static int MergeKnownElementTypes(List<TileVisual> target)
        {
            var knownTypes = new[]
            {
                WorldTileTypes.Dirt, WorldTileTypes.Stone, WorldTileTypes.Coal, WorldTileTypes.Clay,
                WorldTileTypes.OysterMushroom,
                WorldTileTypes.StoneMid, WorldTileTypes.IronOre, WorldTileTypes.CopperOre, WorldTileTypes.IceShard,
                WorldTileTypes.Shiitake,
                WorldTileTypes.StoneDeep, WorldTileTypes.IceSteelOre, WorldTileTypes.FrostEssence,
                WorldTileTypes.Seogi,
                WorldTileTypes.Bedrock, WorldTileTypes.RuinWall, WorldTileTypes.IceLake, WorldTileTypes.IceAltar,
                WorldTileTypes.BackgroundDirt, WorldTileTypes.BackgroundStone, WorldTileTypes.BackgroundDeep,
                WorldTileTypes.Wallpaper
            };

            var existing = new HashSet<string>();
            foreach (var visual in target)
            {
                if (!string.IsNullOrEmpty(visual.elementType)) existing.Add(visual.elementType);
            }

            var added = 0;
            foreach (var type in knownTypes)
            {
                if (existing.Contains(type)) continue;
                target.Add(new TileVisual { elementType = type, tile = null });
                added++;
            }

            return added;
        }
    }
}
