using System;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>
    /// A-14: 전경 타일의 상·하·좌·우 중 "공기 또는 배경(=전경 없음, <see cref="TileData.IsAir"/>)"과 맞닿은
    /// 방향을 나타내는 비트마스크. TileService가 셀 변경 시 계산해 TilemapRenderer의 먹선 오버레이에 넘긴다.
    /// </summary>
    [Flags]
    public enum TileEdgeMask
    {
        None = 0,
        Top = 1 << 0,
        Right = 1 << 1,
        Bottom = 1 << 2,
        Left = 1 << 3,
    }

    /// <summary>
    /// A-14 타일 노출면 먹선(edge ink-line) 오버레이 공용 로직.
    ///
    /// 설계 의도(재질별 47조각 오토타일 금지 요구사항 반영):
    ///  - 노출 방향 비트마스크(상/하/좌/우, 최대 15가지 조합)를 "모양 5종 × 90도 회전"만으로 전부 표현한다.
    ///    실제로 필요한 아트는 5장뿐이며(직선/모서리/관통/T자/고립), 나머지 회전 변형은 Tilemap의
    ///    인스턴스별 TRS 행렬(SetTransformMatrix)로 처리해 에셋을 추가로 만들지 않는다.
    ///  - 스프라이트는 재질(흙/돌/광물 등)과 무관한 공용 자산이다 — TilemapRenderer.edgeShapeTiles 배열
    ///    하나만 모든 지형에 재사용한다.
    ///  - 이 클래스는 순수 함수/조회 테이블만 제공하고 Tilemap API를 직접 다루지 않는다. 실제 셀 갱신은
    ///    TilemapRenderer.RefreshEdgeOverlay가 담당하고, "어떤 셀을 언제 다시 계산할지"는 TileService가
    ///    (변경된 셀 + 그 4방향 이웃만) 결정한다 — 월드 전체 재계산은 최초 1회(RenderWorld)뿐이다.
    /// </summary>
    public static class TileEdgeOverlayResolver
    {
        /// <summary>Dev B/아트팀에 전달할 공용 edge 스프라이트 인덱스. 이 순서와 개수(5장)가 그대로
        /// TilemapRenderer.edgeShapeTiles 배열 슬롯 순서다.</summary>
        public const int ShapeStraight = 0;  // asset id 제안: edge_straight  — 노출 1면. 기준 방향 Top, 회전으로 나머지 3방향 처리.
        public const int ShapeCorner = 1;    // asset id 제안: edge_corner    — 인접한 2면 노출(모서리). 기준 Top+Right.
        public const int ShapeThrough = 2;   // asset id 제안: edge_through  — 마주보는 2면 노출(관통). 기준 Top+Bottom(세로).
        public const int ShapeTJunction = 3; // asset id 제안: edge_tjunction — 3면 노출. 기준: Left만 막힌 형태(T+R+B).
        public const int ShapeIsolated = 4;  // asset id 제안: edge_isolated — 4면 모두 노출(고립 블록).
        public const int ShapeCount = 5;

        /// <summary>
        /// 지정한 셀(x,y)이 전경 타일을 가지고 있을 때, 상·하·좌·우 이웃 중 공기/배경(IsAir)인 방향의
        /// 비트를 켠 마스크를 만든다. 전경이 없는 칸(IsAir) 자체는 오버레이 대상이 아니므로 None을 반환한다.
        /// 맵 경계 밖은 노출로 취급하지 않는다(월드 테두리에 불필요한 먹선이 생기는 것을 방지).
        /// </summary>
        public static TileEdgeMask ComputeExposureMask(TileData[,] tiles, int x, int y, int width, int height)
        {
            if (tiles[x, y].IsAir) return TileEdgeMask.None;

            var mask = TileEdgeMask.None;
            if (IsExposedNeighbor(tiles, x, y + 1, width, height)) mask |= TileEdgeMask.Top;
            if (IsExposedNeighbor(tiles, x, y - 1, width, height)) mask |= TileEdgeMask.Bottom;
            if (IsExposedNeighbor(tiles, x - 1, y, width, height)) mask |= TileEdgeMask.Left;
            if (IsExposedNeighbor(tiles, x + 1, y, width, height)) mask |= TileEdgeMask.Right;
            return mask;
        }

        private static bool IsExposedNeighbor(TileData[,] tiles, int nx, int ny, int width, int height)
        {
            if (nx < 0 || nx >= width || ny < 0 || ny >= height) return false;
            return tiles[nx, ny].IsAir;
        }

        /// <summary>
        /// 노출 마스크를 (모양 인덱스, 시계방향 90도 회전 스텝 0~3)으로 변환한다. mask가 None이면 false —
        /// 호출자는 해당 셀의 오버레이 타일을 지워야 한다는 뜻이다.
        /// </summary>
        public static bool TryResolve(TileEdgeMask mask, out int shapeIndex, out int rotationSteps)
        {
            switch (mask)
            {
                // 1면 노출 — 직선. 기준(회전 0) = Top.
                case TileEdgeMask.Top: shapeIndex = ShapeStraight; rotationSteps = 0; return true;
                case TileEdgeMask.Right: shapeIndex = ShapeStraight; rotationSteps = 1; return true;
                case TileEdgeMask.Bottom: shapeIndex = ShapeStraight; rotationSteps = 2; return true;
                case TileEdgeMask.Left: shapeIndex = ShapeStraight; rotationSteps = 3; return true;

                // 인접한 2면 노출 — 모서리. 기준(회전 0) = Top+Right.
                case TileEdgeMask.Top | TileEdgeMask.Right: shapeIndex = ShapeCorner; rotationSteps = 0; return true;
                case TileEdgeMask.Right | TileEdgeMask.Bottom: shapeIndex = ShapeCorner; rotationSteps = 1; return true;
                case TileEdgeMask.Bottom | TileEdgeMask.Left: shapeIndex = ShapeCorner; rotationSteps = 2; return true;
                case TileEdgeMask.Left | TileEdgeMask.Top: shapeIndex = ShapeCorner; rotationSteps = 3; return true;

                // 마주보는 2면 노출 — 관통. 기준(회전 0) = Top+Bottom(세로).
                case TileEdgeMask.Top | TileEdgeMask.Bottom: shapeIndex = ShapeThrough; rotationSteps = 0; return true;
                case TileEdgeMask.Left | TileEdgeMask.Right: shapeIndex = ShapeThrough; rotationSteps = 1; return true;

                // 3면 노출 — T자. 기준(회전 0) = Left만 막힘(Top+Right+Bottom 노출).
                case TileEdgeMask.Top | TileEdgeMask.Right | TileEdgeMask.Bottom: shapeIndex = ShapeTJunction; rotationSteps = 0; return true;
                case TileEdgeMask.Right | TileEdgeMask.Bottom | TileEdgeMask.Left: shapeIndex = ShapeTJunction; rotationSteps = 1; return true;
                case TileEdgeMask.Bottom | TileEdgeMask.Left | TileEdgeMask.Top: shapeIndex = ShapeTJunction; rotationSteps = 2; return true;
                case TileEdgeMask.Left | TileEdgeMask.Top | TileEdgeMask.Right: shapeIndex = ShapeTJunction; rotationSteps = 3; return true;

                // 4면 모두 노출 — 고립 블록. 회전 무의미(0 고정).
                case TileEdgeMask.Top | TileEdgeMask.Right | TileEdgeMask.Bottom | TileEdgeMask.Left:
                    shapeIndex = ShapeIsolated; rotationSteps = 0; return true;

                default:
                    shapeIndex = -1;
                    rotationSteps = 0;
                    return false; // mask == None (또는 정의되지 않은 값).
            }
        }

        /// <summary>rotationSteps(0~3, 90도 단위 시계방향)를 Tilemap.SetTransformMatrix에 바로 넘길 수 있는
        /// 회전 행렬로 변환한다. 셀 중심(0.5, 0.5, 0) 기준으로 회전해야 타일 칸 안에서 제자리 회전한다.</summary>
        public static Matrix4x4 BuildRotationMatrix(int rotationSteps)
        {
            var rotation = Quaternion.Euler(0f, 0f, -90f * rotationSteps);
            var pivot = new Vector3(0.5f, 0.5f, 0f);
            return Matrix4x4.Translate(pivot) * Matrix4x4.Rotate(rotation) * Matrix4x4.Translate(-pivot);
        }
    }
}
