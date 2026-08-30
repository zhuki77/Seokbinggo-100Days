using Nyangbingo.Inventory;
using Nyangbingo.UI;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>
    /// 서리 지도(frost_map) — 카메라 가장자리 방향의 판 통로(공기 타일) 힌트.
    /// </summary>
    public sealed class ArtifactTunnelEdgePresenter : MonoBehaviour
    {
        private const float ScanIntervalSeconds = .5f;
        private const float EdgeInsetTiles = 1.5f;
        private const int ScanRadiusCells = 24;

        [SerializeField] private MainGameBootstrap bootstrap;
        [SerializeField] private MainGameRuntimeServices runtimeServices;
        [SerializeField] private MainGamePlayerController playerController;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private MainGameBossSummonUiController interactionMessages;

        private float scanRemaining;
        private float messageCooldown;
        private string lastDirectionLabel = string.Empty;

        private void Awake()
        {
            bootstrap ??= FindAnyObjectByType<MainGameBootstrap>();
            runtimeServices ??= FindAnyObjectByType<MainGameRuntimeServices>();
            playerController ??= FindAnyObjectByType<MainGamePlayerController>();
            worldCamera ??= Camera.main;
            interactionMessages ??= FindAnyObjectByType<MainGameBossSummonUiController>();
        }

        private void Update()
        {
            scanRemaining -= Time.deltaTime;
            messageCooldown = Mathf.Max(0f, messageCooldown - Time.deltaTime);
            if (scanRemaining > 0f) return;
            scanRemaining = ScanIntervalSeconds;
            TryPresentEdgeHint();
        }

        private void TryPresentEdgeHint()
        {
            var player = playerController;
            var tileService = bootstrap?.TileService;
            var equipment = runtimeServices?.EquipmentSystem;
            var verbs = runtimeServices?.ArtifactVerbs;
            if (player == null || tileService == null || equipment == null || verbs == null ||
                worldCamera == null || !worldCamera.orthographic)
                return;
            var playerPosition = (Vector2)player.transform.position;
            var context = ArtifactActivationContextFactory.Build(
                tileService, playerPosition, bootstrap.TimeService);
            if (!verbs.ShowsDugPaths(equipment, context)) return;

            var cameraCenter = worldCamera.transform.position;
            var halfHeight = worldCamera.orthographicSize;
            var halfWidth = halfHeight * worldCamera.aspect;
            var bounds = new Rect(
                cameraCenter.x - halfWidth + EdgeInsetTiles,
                cameraCenter.y - halfHeight + EdgeInsetTiles,
                halfWidth * 2f - EdgeInsetTiles * 2f,
                halfHeight * 2f - EdgeInsetTiles * 2f);

            Vector2? bestDirection = null;
            var bestDistance = float.PositiveInfinity;
            var originCell = tileService.WorldToCell(playerPosition);
            for (var dx = -ScanRadiusCells; dx <= ScanRadiusCells; dx++)
            {
                for (var dy = -ScanRadiusCells; dy <= ScanRadiusCells; dy++)
                {
                    var cell = originCell + new Vector3Int(dx, dy, 0);
                    if (!tileService.InBounds(cell) || !tileService.GetTile(cell).IsAir) continue;
                    if (!IsDugCorridorCell(tileService, cell)) continue;
                    var world = (Vector2)tileService.GetCellCenterWorld(cell);
                    if (bounds.Contains(world)) continue;
                    var distance = (world - playerPosition).sqrMagnitude;
                    if (distance >= bestDistance) continue;
                    bestDistance = distance;
                    bestDirection = world - playerPosition;
                }
            }

            if (!bestDirection.HasValue || bestDirection.Value.sqrMagnitude <= Mathf.Epsilon) return;
            var label = DirectionLabel(bestDirection.Value);
            if (messageCooldown > 0f && label == lastDirectionLabel) return;
            lastDirectionLabel = label;
            messageCooldown = 3f;
            interactionMessages?.ShowExternalMessage($"판 통로 힌트 · {label}");
        }

        private static bool IsDugCorridorCell(TileService tileService, Vector3Int cell)
        {
            var neighbors = new[]
            {
                cell + Vector3Int.left, cell + Vector3Int.right,
                cell + Vector3Int.up, cell + Vector3Int.down
            };
            var solidNeighbors = 0;
            for (var index = 0; index < neighbors.Length; index++)
            {
                var neighbor = neighbors[index];
                if (!tileService.InBounds(neighbor)) continue;
                if (!tileService.GetTile(neighbor).IsAir) solidNeighbors++;
            }
            return solidNeighbors >= 2;
        }

        private static string DirectionLabel(Vector2 direction)
        {
            if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
                return direction.x >= 0f ? "동쪽 가장자리" : "서쪽 가장자리";
            return direction.y >= 0f ? "북쪽 가장자리" : "남쪽 가장자리";
        }
    }
}
