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
    ///  - 배경벽: isUndergroundDecor == true 인 칸만 elementType(bg_* 값)의 배경을 채운다.
    /// 두 조건은 상호 배타적이지 않게 설계돼 있으므로(전경이 비어야 배경 decor가 의미를 가짐) 그대로 각각 평가한다.
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

        [Header("elementType ↔ TileBase 매핑 (드래그앤드롭)")]
        [SerializeField] private TileVisual[] tileVisuals = Array.Empty<TileVisual>();

        public Tilemap Foreground => foregroundTilemap;
        public Tilemap Background => backgroundTilemap;

        private Dictionary<string, TileBase> _lookup;

        private void Awake()
        {
            BuildLookup();
        }

        private void BuildLookup()
        {
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

            if (_lookup == null) BuildLookup();

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

                    backgroundBlock[index] = tile.isUndergroundDecor
                        ? ResolveTile(tile.elementType, ref missing)
                        : null;
                }
            }

            if (missing != null && missing.Count > 0)
            {
                var sb = new StringBuilder("[Nyangbingo] TilemapRenderer: 다음 elementType에 TileBase 매핑이 없어 빈 칸으로 처리했습니다: ");
                sb.Append(string.Join(", ", missing));
                Debug.LogWarning(sb.ToString());
            }

            var bounds = new BoundsInt(0, 0, 0, width, height, 1);

            foregroundTilemap.ClearAllTiles();
            backgroundTilemap.ClearAllTiles();
            foregroundTilemap.SetTilesBlock(bounds, foregroundBlock);
            backgroundTilemap.SetTilesBlock(bounds, backgroundBlock);
            foregroundTilemap.RefreshAllTiles();
            backgroundTilemap.RefreshAllTiles();
        }

        /// <summary>이후 TileService(채굴)가 elementType → TileBase를 조회할 때도 재사용할 수 있게 공개한다.</summary>
        public bool TryGetTileBase(string elementType, out TileBase tile)
        {
            if (_lookup == null) BuildLookup();
            return _lookup.TryGetValue(elementType, out tile);
        }

        private TileBase ResolveTile(string elementType, ref HashSet<string> missing)
        {
            if (string.IsNullOrEmpty(elementType)) return null;
            if (_lookup.TryGetValue(elementType, out var found)) return found;

            missing ??= new HashSet<string>();
            missing.Add(elementType);
            return null;
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
                WorldTileTypes.BackgroundDirt, WorldTileTypes.BackgroundStone, WorldTileTypes.BackgroundDeep
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
