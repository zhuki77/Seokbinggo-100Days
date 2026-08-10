using Nyangbingo.World;
using UnityEngine;

namespace Nyangbingo.Debugging
{
    /// <summary>
    /// 임시 채굴/건설 입력 테스트 컨트롤러. 좌클릭 = 마우스 아래 전경 타일 채굴, 우클릭 = 같은 칸에
    /// placeElementType 설치. 실제 플레이어 입력/장비(발톱 티어) 시스템이 준비되면 폐기하고
    /// TileService만 그대로 재사용한다.
    /// </summary>
    public sealed class PlayerMiningController : MonoBehaviour
    {
        [SerializeField] private MapGeneratorTestHarness harness;

        [Header("발톱 티어(1~3) — MapGenerator/TileData의 hardness와 동일 스케일")]
        [Range(1, 3)][SerializeField] private int toolTier = 1;

        [SerializeField] private string placeElementType = WorldTileTypes.Dirt;

        private void Update()
        {
            var tileService = harness != null ? harness.TileService : null;
            if (tileService == null || Camera.main == null) return;

            if (Input.GetMouseButtonDown(0)) TryMine(tileService);
            else if (Input.GetMouseButtonDown(1)) TryInteract(tileService);
        }

        /// <summary>
        /// 우클릭은 두 가지 역할을 겸한다 — 가리킨 칸에 미개봉 상자가 있으면 먼저 그 상자를 열고,
        /// 없으면(혹은 이미 열려 있으면) 기존처럼 블록을 설치한다. 상자는 TileData 배열에 존재하지
        /// 않는 별도 메타데이터라, 채굴 판정과 달리 "상자 우선 조회 → 실패 시 설치" 순서가 자연스럽다.
        /// </summary>
        private void TryInteract(TileService tileService)
        {
            var cell = WorldCellUnderMouse(tileService);
            if (harness != null && harness.TryOpenChest(cell)) return;
            TryPlace(tileService, cell);
        }

        private void TryMine(TileService tileService)
        {
            var cell = WorldCellUnderMouse(tileService);
            if (tileService.TryBreakForeground(cell, toolTier, out var itemId, out var amount))
            {
                var dropText = string.IsNullOrEmpty(itemId) ? "드랍 없음(카탈로그 미연결)" : $"{itemId} x{amount}";
                Debug.Log($"[Nyangbingo] 채굴 성공 {cell} — 드랍: {dropText}");
            }
            else
            {
                var tile = tileService.GetTile(cell);
                Debug.Log($"[Nyangbingo] 채굴 실패 {cell} — 타일 경도 {tile.hardness}, 현재 발톱 티어 {toolTier}");
            }
        }

        private void TryPlace(TileService tileService, Vector3Int cell)
        {
            if (tileService.TryPlaceForeground(cell, placeElementType))
                Debug.Log($"[Nyangbingo] 설치 성공 {cell} — {placeElementType}");
            else
                Debug.Log($"[Nyangbingo] 설치 실패 {cell} — 이미 막혀 있거나 범위 밖입니다.");
        }

        private static Vector3Int WorldCellUnderMouse(TileService tileService)
        {
            var worldPoint = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            return tileService.WorldToCell(worldPoint);
        }
    }
}
