using Nyangbingo.Data;
using Nyangbingo.Yokai;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>
    /// 기지 반경 안에서 태어난 요괴의 코어 공격 앵커. 플레이어 Health/인벤토리는 노출하지 않고
    /// 성벽 파괴만 기존 권위 경로에 위임하며, 경로가 열린 뒤 코어 도달을 침투로 기록한다.
    /// </summary>
    public sealed class MainGameCoreRaidTarget : MonoBehaviour, IYokaiTarget,
        IWallMaterialTarget, IYokaiBarrierTarget, IYokaiInfiltrationTarget
    {
        private MainGameRaidTarget wallAuthority;
        private InvasionService invasion;

        public Transform TargetTransform => transform;
        public YokaiWallMaterial WallMaterial => wallAuthority != null
            ? wallAuthority.WallMaterial
            : YokaiWallMaterial.Ice;

        public void Configure(MainGameRaidTarget authority, InvasionService invasionService)
        {
            wallAuthority = authority;
            invasion = invasionService;
        }

        public void SetCorePosition(TileService tiles, Vector3Int coreCell)
        {
            transform.position = tiles != null
                ? tiles.GetCellCenterWorld(coreCell)
                : new Vector3(coreCell.x + .5f, coreCell.y + .5f, 0f);
        }

        public void DamageWall(float amount) => wallAuthority?.DamageWall(amount);

        public bool TryFindBlockingWall(Vector3 attackerPosition, Vector3 approachDirection,
            float searchRange, out Vector3Int wallCell, out YokaiWallMaterial material)
        {
            wallCell = default;
            material = YokaiWallMaterial.Default;
            return wallAuthority != null && wallAuthority.TryFindBlockingWall(
                attackerPosition, approachDirection, searchRange, out wallCell, out material);
        }

        public bool TryDamageBlockingWall(Vector3Int wallCell, float amount) =>
            wallAuthority != null && wallAuthority.TryDamageBlockingWall(wallCell, amount);

        public bool TryRecordInfiltration(YokaiDefinition definition) =>
            invasion?.RecordInfiltration(definition) == true;
    }
}
