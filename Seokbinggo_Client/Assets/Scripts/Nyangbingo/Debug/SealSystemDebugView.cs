using System.Collections.Generic;
using Nyangbingo.World;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Nyangbingo.Debugging
{
    /// <summary>
    /// SealSystem 시각화 전용 디버그 컴포넌트. 마우스 아래 칸을 SealSystem의 "주 관찰 지점"으로 실시간
    /// 추적하면서, 밀폐 여부에 따라 다른 색의 반투명 오버레이로 내부 공기 칸/경계벽 칸을 그려준다.
    ///
    /// 시각화는 두 경로를 동시에 사용한다:
    ///  - OnDrawGizmos(): Scene 뷰에서 즉시 확인 가능하고 밀폐율 텍스트 라벨까지 보여준다.
    ///  - 런타임 스프라이트 오버레이(Update에서 갱신): Scene 뷰 Gizmos 토글이나 Game 뷰 Gizmos 버튼 상태와
    ///    무관하게 Play 중 Game 뷰에서도 항상 보이도록 보장한다(Gizmos는 Game 뷰에서 기본적으로 꺼져 있음).
    ///
    /// 실제 플레이어 위치 추적이 준비되면 WorldCellUnderMouse() 대신 플레이어 셀을 넘기면 된다.
    /// </summary>
    public sealed class SealSystemDebugView : MonoBehaviour
    {
        [SerializeField] private MapGeneratorTestHarness harness;
        [SerializeField] private bool debugDrawEnabled = true;

        [Header("Game 뷰 보장 오버레이 (Scene 뷰 Gizmos 설정과 무관하게 항상 표시됨)")]
        [SerializeField] private bool useRuntimeOverlay = true;
        [SerializeField] private int maxOverlayCells = 1200;

        [Header("맵 밖 실패 마커 (축소된 카메라에서도 항상 눈에 띄도록 고정 크기 사용)")]
        [SerializeField] private float outOfBoundsMarkerScale = 3f;
        [SerializeField] private Color outOfBoundsColor = new Color(1f, 0.55f, 0.05f, 0.85f);

        /// <summary>MapGeneratorTestHarness가 SealSystem을 소유하고 있는지 확인/디버깅할 때 쓰는 참조.</summary>
        public MapGeneratorTestHarness Harness => harness;

        private SealSystem sealSystem;
        private Vector3Int lastHoverCell;
        private bool hasHoverCell;

        private Sprite overlaySprite;
        private readonly List<SpriteRenderer> overlayPool = new List<SpriteRenderer>();
        private int activeOverlayCount;

        /// <summary>
        /// MapGeneratorTestHarness.Start()가 SealSystem을 생성한 직후 즉시 호출해 주입한다. Update()의
        /// 매 프레임 폴링(harness.SealSystem 재조회)만으로도 결국 같은 값을 얻지만, 초기화 첫 프레임부터
        /// 확실한 참조를 갖도록 하는 안전장치다.
        /// </summary>
        public void BindSealSystem(SealSystem system) => sealSystem = system;

        private void Update()
        {
            sealSystem = harness != null ? harness.SealSystem : null;
            if (sealSystem == null || Camera.main == null)
            {
                hasHoverCell = false;
                HideAllOverlays();
                return;
            }

            var worldPoint = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            lastHoverCell = new Vector3Int(Mathf.FloorToInt(worldPoint.x), Mathf.FloorToInt(worldPoint.y), 0);
            hasHoverCell = true;
            sealSystem.SetPrimaryWatchPoint(lastHoverCell);

            RefreshRuntimeOverlay();
        }

        private void OnDisable() => HideAllOverlays();

        private void RefreshRuntimeOverlay()
        {
            if (!useRuntimeOverlay || !debugDrawEnabled)
            {
                HideAllOverlays();
                return;
            }

            // 맵 밖은 Flood Fill 결과(칸 목록) 크기에 좌우되는 일반 오버레이와 분리해서, 카메라가 400x160
            // 맵 전체를 비추도록 크게 축소돼 있어도 항상 눈에 띄는 고정 월드 크기 마커로 그린다.
            // (기존에는 맵 밖 실패도 한 칸(1유닛)짜리 옅은 마커로만 그려져서 축소된 화면에서 거의 안 보였다.)
            if (!sealSystem.IsInBounds(lastHoverCell))
            {
                ShowOutOfBoundsOverlay();
                return;
            }

            if (!sealSystem.TryGetDebugRegion(lastHoverCell, out var isSealed, out _, out var interiorCells, out var boundaryCells))
            {
                HideAllOverlays();
                return;
            }

            var interiorColor = isSealed ? new Color(0.2f, 1f, 0.3f, 0.45f) : new Color(1f, 0.6f, 0.1f, 0.35f);
            var boundaryColor = isSealed ? new Color(0.1f, 0.6f, 1f, 0.7f) : new Color(1f, 0.2f, 0.2f, 0.7f);

            var used = PlaceOverlayCells(interiorCells, interiorColor, 0.85f, 0);
            used = PlaceOverlayCells(boundaryCells, boundaryColor, 0.5f, used);

            for (var i = used; i < activeOverlayCount; i++) overlayPool[i].gameObject.SetActive(false);
            activeOverlayCount = used;
        }

        private void ShowOutOfBoundsOverlay()
        {
            var quad = GetOrCreateOverlayQuad(0);
            quad.transform.position = new Vector3(lastHoverCell.x + 0.5f, lastHoverCell.y + 0.5f, -0.5f);
            quad.transform.localScale = Vector3.one * outOfBoundsMarkerScale;
            quad.color = outOfBoundsColor;
            quad.gameObject.SetActive(true);

            for (var i = 1; i < activeOverlayCount; i++) overlayPool[i].gameObject.SetActive(false);
            activeOverlayCount = 1;
        }

        private int PlaceOverlayCells(IReadOnlyCollection<Vector3Int> cells, Color color, float scale, int startIndex)
        {
            var index = startIndex;
            foreach (var cell in cells)
            {
                if (index >= maxOverlayCells) break; // 아주 큰 개방 공간에서도 디버그 오버레이가 렉을 유발하지 않도록 상한선.

                var quad = GetOrCreateOverlayQuad(index);
                quad.transform.position = new Vector3(cell.x + 0.5f, cell.y + 0.5f, -0.5f);
                quad.transform.localScale = Vector3.one * scale;
                quad.color = color;
                quad.gameObject.SetActive(true);
                index++;
            }
            return index;
        }

        private SpriteRenderer GetOrCreateOverlayQuad(int index)
        {
            if (index < overlayPool.Count) return overlayPool[index];

            if (overlaySprite == null)
                overlaySprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 2f);

            var quadObject = new GameObject($"SealDebugOverlay_{index}");
            quadObject.transform.SetParent(transform, false);
            var spriteRenderer = quadObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = overlaySprite;
            spriteRenderer.sortingOrder = 5; // Tilemap(기본 0)보다는 위, 상자/스폰 마커(10)보다는 아래.
            overlayPool.Add(spriteRenderer);
            return spriteRenderer;
        }

        private void HideAllOverlays()
        {
            for (var i = 0; i < activeOverlayCount; i++) overlayPool[i].gameObject.SetActive(false);
            activeOverlayCount = 0;
        }

        private void OnDrawGizmos()
        {
            if (!debugDrawEnabled || !hasHoverCell || sealSystem == null) return;

            if (!sealSystem.IsInBounds(lastHoverCell))
            {
                Gizmos.color = outOfBoundsColor;
                Gizmos.DrawCube(new Vector3(lastHoverCell.x + 0.5f, lastHoverCell.y + 0.5f, 0f), Vector3.one * outOfBoundsMarkerScale);
#if UNITY_EDITOR
                Handles.Label(new Vector3(lastHoverCell.x + 0.5f, lastHoverCell.y + 1.5f, 0f), "맵 밖 (판정 불가)");
#endif
                return;
            }

            if (!sealSystem.TryGetDebugRegion(lastHoverCell, out var isSealed, out var sealPercent,
                    out var interiorCells, out var boundaryCells))
                return;

            var interiorColor = isSealed ? new Color(0.2f, 1f, 0.3f, 0.35f) : new Color(1f, 0.6f, 0.1f, 0.25f);
            Gizmos.color = interiorColor;
            foreach (var cell in interiorCells)
                Gizmos.DrawCube(new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f), Vector3.one * 0.9f);

            Gizmos.color = isSealed ? new Color(0.1f, 0.6f, 1f, 0.6f) : new Color(1f, 0.2f, 0.2f, 0.6f);
            foreach (var cell in boundaryCells)
                Gizmos.DrawWireCube(new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f), Vector3.one);

#if UNITY_EDITOR
            Handles.Label(new Vector3(lastHoverCell.x + 0.5f, lastHoverCell.y + 1.5f, 0f),
                $"{(isSealed ? "밀폐됨" : "미밀폐")} {sealPercent * 100f:0}%");
#endif
        }
    }
}
