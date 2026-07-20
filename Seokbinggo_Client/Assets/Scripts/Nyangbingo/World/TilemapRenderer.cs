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

        [Tooltip("1순위(인스펙터 매핑)에 없는 elementType을 만나면 이 폴더 아래에서 " +
                 "Resources.Load<TileBase>(\"{이 값}/{elementType}\")로 한 번 더 찾아본다(2순위, 선택 사항). " +
                 "예: 값이 'Tiles'이고 elementType이 'dirt'면 'Assets/Resources/Tiles/dirt.asset'을 찾는다. " +
                 "비워두면 이 단계를 건너뛰고 곧장 폴백 타일로 넘어간다.")]
        [SerializeField] private string resourcesFallbackFolder = "Tiles";

        [Tooltip("1·2순위 모두 실패했을 때 빈 칸(투명) 대신 그려줄 최종 대체 타일(3순위). 눈에 띄는 색(예: " +
                 "마젠타)의 더미 타일을 연결해두면 매핑 누락 칸이 '검은 화면'처럼 안 보이는 게 아니라 화면에 " +
                 "바로 도드라져서 원인을 즉시 알 수 있다. 비워두면 기존처럼 완전히 투명하게 처리된다.")]
        [SerializeField] private TileBase fallbackTile;

        [Tooltip("켜두면 매핑이 없는 elementType을 만나도 콘솔에 경고를 남기지 않는다. 시각 자료 없이 " +
                 "로직만 확인하는 회귀 테스트용 더미 렌더러에서만 true로 설정할 것 — 실제 게임 씬에서는 " +
                 "매핑 누락을 바로 알 수 있어야 하므로 항상 false로 둔다.")]
        [SerializeField] private bool suppressMissingTileWarning;

        public Tilemap Foreground => foregroundTilemap;
        public Tilemap Background => backgroundTilemap;

        // 초기값을 빈 딕셔너리로 잡아둔다 — RebuildLookupTable()이 "성급한 0개 갱신"을 막느라 조기
        // 반환하는 경로를 타더라도 _lookup 자체는 항상 non-null이라, ResolveTile/TryGetTileBase가
        // NullReferenceException 없이 안전하게 동작한다.
        private Dictionary<string, TileBase> _lookup = new Dictionary<string, TileBase>();
        private readonly HashSet<string> _resourceLoadWarnings = new HashSet<string>();

        private CompositeCollider2D foregroundComposite;

        private void Awake()
        {
            RebuildLookupTable();
            EnsureForegroundCollision();
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

            var width = tiles.GetLength(0);
            var height = tiles.GetLength(1);
            if (width <= 0 || height <= 0) return;

            var foregroundBlock = new TileBase[width * height];
            var backgroundBlock = new TileBase[width * height];
            HashSet<string> missing = null;

            // Tilemap.SetTilesBlock은 배열을 x가 가장 빠르게, 그다음 y 순서로 읽는다 (index = y*width + x).
            for (var y = 0; y < height; y++)
            {
                var rowOffset = y * width;
                for (var x = 0; x < width; x++)
                {
                    var tile = tiles[x, y];
                    var index = rowOffset + x;

                    foregroundBlock[index] = tile.hardness > 0
                        ? ResolveTile(tile.elementType, ref missing)
                        : null;

                    // A-16: 전경 고체 뒤에도 자연 배경을 그린다. 동굴·하늘(빈 배경)은 null.
                    backgroundBlock[index] = tile.HasBackground
                        ? ResolveTile(tile.backgroundElementType, ref missing)
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

            // A-14: 월드 전체 먹선 오버레이 최초 1회 계산. 이후에는 TileService가 변경된 셀 +
            // 그 4방향 이웃만 RefreshEdgeOverlay로 갱신하므로, 이 전체 순회는 월드 생성/로드당 딱 한 번뿐이다.
            RebuildEdgeOverlayForWorld(tiles);
        }

        /// <summary>
        /// A-17/A-21: 전경 Tilemap에 TilemapCollider2D + CompositeCollider2D + Static Rigidbody2D를 구성한다.
        /// 배경 Tilemap에는 Collider를 붙이지 않는다.
        /// 좌표 계약: 논리 셀 (x,y)의 월드 AABB는 Grid/Tilemap 기본 Cell Size(1,1) 기준 [x,x+1]×[y,y+1],
        /// 중심 GetCellCenterWorld ≈ (x+0.5, y+0.5). Tile Anchor 기본 (0.5,0.5) — 아트 피벗이 바뀌어도
        /// Collider는 타일 점유 셀 경계를 따르며 스프라이트 피벗에 의존하지 않는다.
        /// </summary>
        public void EnsureForegroundCollision()
        {
            if (foregroundTilemap == null) return;

            var fgGo = foregroundTilemap.gameObject;
            var tilemapCollider = fgGo.GetComponent<TilemapCollider2D>();
            if (tilemapCollider == null) tilemapCollider = fgGo.AddComponent<TilemapCollider2D>();
            tilemapCollider.usedByComposite = true;

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
            if (foregroundComposite == null) foregroundComposite = foregroundTilemap.GetComponent<CompositeCollider2D>();
            // TilemapCollider2D(usedByComposite)는 SetTile/RefreshTile 후 Composite가 자동 갱신된다.
            // 대량 변경(RenderWorld) 뒤에는 컴포넌트를 한번 껐다 켜 합성 메시를 확정한다.
            if (foregroundComposite == null) return;
            foregroundComposite.enabled = false;
            foregroundComposite.enabled = true;
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

            // A-16: t_bg_* ↔ bg_* 별칭.
            var canonical = TileIdAlias.ToCanonical(elementType);
            if (!string.Equals(canonical, elementType, StringComparison.Ordinal) &&
                _lookup.TryGetValue(canonical, out tile) && tile != null)
                return true;

            tile = null;
            return false;
        }

        /// <summary>
        /// 3단계 안전장치: 1) 인스펙터 명시 매핑 → 2) Resources.Load 동적 폴백(선택) → 3) 최종 fallbackTile.
        /// 1순위가 항상 우선이므로, 인스펙터에 등록해둔 타일이 있으면 Resources 폴더 내용과 무관하게 그걸 쓴다.
        /// </summary>
        private TileBase ResolveTile(string elementType, ref HashSet<string> missing)
        {
            if (string.IsNullOrEmpty(elementType)) return null;
            elementType = TileIdAlias.ToCanonical(elementType);

            // 1순위: 인스펙터에 직접 드래그앤드롭으로 연결해둔 명시적 매핑.
            if (_lookup.TryGetValue(elementType, out var explicitTile) && explicitTile != null)
                return explicitTile;

            // 2순위: Resources.Load 동적 폴백(선택 사항) — 파일명이 elementType과 정확히 일치해야 한다.
            // suppressMissingTileWarning이 켜진 더미 렌더러(회귀 테스트 등)에서는 애초에 시도할 필요가
            // 없는 진단용 기능이므로 함께 건너뛴다.
            if (!string.IsNullOrEmpty(resourcesFallbackFolder) && !suppressMissingTileWarning)
            {
                var resourcePath = $"{resourcesFallbackFolder}/{elementType}";
                var loaded = Resources.Load<TileBase>(resourcePath);
                if (loaded != null)
                {
                    _lookup[elementType] = loaded; // 다음부터는 1순위 캐시로 바로 히트하게 저장.
                    Debug.Log($"[Nyangbingo] TilemapRenderer: '{elementType}' 인스펙터 매핑이 없어 " +
                              $"Resources.Load(\"{resourcePath}\")로 대신 찾았습니다. 가능하면 인스펙터에 " +
                              "직접 등록해두는 것을 권장합니다(1순위가 더 안전함).");
                    return loaded;
                }
                // RenderWorld는 같은 elementType을 수천 셀에서 조회할 수 있다. 누락 진단은 타입마다
                // 한 번만 남기고, 최종 누락 목록은 RenderWorld 끝의 집계 경고로 다시 제공한다.
                if (_resourceLoadWarnings.Add(elementType))
                {
                    Debug.LogWarning($"[Nyangbingo] TilemapRenderer: [Resources/{resourcePath}] 로드 실패! " +
                                     $"인스펙터 매핑도 없고 'Assets/Resources/{resourcePath}.asset' 경로에도 " +
                                     "TileBase 에셋이 없습니다.");
                }
            }

            // 3순위: 최종 폴백 타일(설정돼 있으면 화면에서 바로 눈에 띔), 없으면 투명 빈 칸.
            missing ??= new HashSet<string>();
            missing.Add(elementType);
            return fallbackTile;
        }

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
                WorldTileTypes.StoneMid, WorldTileTypes.IronOre, WorldTileTypes.CopperOre, WorldTileTypes.IceShard,
                WorldTileTypes.StoneDeep, WorldTileTypes.IceSteelOre, WorldTileTypes.FrostEssence,
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
