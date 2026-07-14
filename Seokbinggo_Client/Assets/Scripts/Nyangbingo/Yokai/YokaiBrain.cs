using Nyangbingo.Combat;
using Nyangbingo.Data;
using UnityEngine;

namespace Nyangbingo.Yokai
{
    public interface IYokaiTarget { Transform TargetTransform { get; } void DamageWall(float amount); }
    public interface IYokaiLootTarget { bool TryStealGroundLoot(); }
    public interface IWallMaterialTarget { bool IsIronHeatWall { get; } }

    [RequireComponent(typeof(Health))]
    public sealed class YokaiBrain : MonoBehaviour
    {
        private enum State { Approach, AttackWall, StealLoot, Retreat }
        [SerializeField] private YokaiDefinition definition;
        [SerializeField] private float wallAttackRange = 1f;
        [SerializeField] private float retreatSpeedMultiplier = .5f;
        private IYokaiTarget target;
        private State state;
        public YokaiDefinition Definition => definition;

        public void SetTarget(IYokaiTarget value) => target = value;
        public void BeginRetreat() => state = State.Retreat;

        private void Update()
        {
            if (definition == null || target == null || target.TargetTransform == null) return;
            var counters = target as IYokaiCounterSource;
            if (definition.Kind == Nyangbingo.Core.YokaiKind.Yagwanggwi && counters != null && counters.IsInSieveRange) return;
            var direction = (target.TargetTransform.position - transform.position).normalized;
            switch (state)
            {
                case State.Approach:
                    if (YokaiSpecialRules.ShouldStealGroundLoot(definition.Kind, counters)) state = State.StealLoot;
                    else if (Vector2.Distance(transform.position, target.TargetTransform.position) <= wallAttackRange) state = State.AttackWall;
                    else transform.position += direction * definition.MoveSpeed * Time.deltaTime;
                    break;
                case State.StealLoot:
                    if (Vector2.Distance(transform.position, target.TargetTransform.position) > wallAttackRange)
                        transform.position += direction * definition.MoveSpeed * Time.deltaTime;
                    else if ((target as IYokaiLootTarget)?.TryStealGroundLoot() == true) state = State.Retreat;
                    else state = State.Approach;
                    break;
                case State.AttackWall:
                    var wall = target as IWallMaterialTarget;
                    var damage = YokaiSpecialRules.IsFireImmuneWall(definition.Kind, wall != null && wall.IsIronHeatWall)
                        ? 0f : definition.WallDamagePerSecond * Time.deltaTime;
                    target.DamageWall(damage);
                    break;
                case State.Retreat:
                    transform.position -= direction * definition.MoveSpeed * retreatSpeedMultiplier * Time.deltaTime;
                    break;
            }
        }
    }
}
