using Nyangbingo.Combat;
using Nyangbingo.World;
using UnityEngine;

namespace Nyangbingo.Bosses
{
    /// <summary>
    /// 산군 — 플레이어가 도망치면 추격, 정면으로 맞서면 물러선다(후퇴 중 딜 0).
    /// </summary>
    public sealed class BossSangunBehaviour : MonoBehaviour, IGameSecondsTickable
    {
        public const float RetreatMoveSpeedTilesPerGameSecond = 1.1f;
        public const float FleeVelocityThreshold = 0.35f;
        public const float ConfrontFacingDotThreshold = 0.45f;

        private Health health;
        private BossCombatController combat;
        private WorldMobPhysicsBody physicsBody;
        private Transform playerTransform;
        private bool isRetreating;

        public bool IsRetreating => isRetreating;

        public void Configure(Transform player)
        {
            health = GetComponent<Health>();
            combat = GetComponent<BossCombatController>();
            physicsBody = GetComponent<WorldMobPhysicsBody>();
            playerTransform = player;
        }

        public void Tick(float deltaGameSeconds)
        {
            if (health == null || health.IsDead || playerTransform == null) return;
            RefreshRetreatState();
            if (combat != null && combat.IsOpeningDodgeActive) return;
            if (isRetreating) health.SetDamageTakenMultiplier(0f);
            else health.SetDamageTakenMultiplier(1f);
        }

        public bool TryOverrideCombatTick(float deltaGameSeconds)
        {
            if (health == null || health.IsDead || playerTransform == null ||
                combat == null || combat.IsOpeningDodgeActive)
                return false;

            RefreshRetreatState();
            if (!isRetreating) return false;

            var awayFromPlayer = (Vector2)transform.position - (Vector2)playerTransform.position;
            if (awayFromPlayer.sqrMagnitude <= Mathf.Epsilon)
                awayFromPlayer = Vector2.left;
            var direction = awayFromPlayer.normalized;
            var travel = RetreatMoveSpeedTilesPerGameSecond * Mathf.Max(0f, deltaGameSeconds);
            if (physicsBody != null) physicsBody.Move(direction * travel);
            else transform.position += (Vector3)(direction * travel);
            combat.NotifyExternalMovement(direction);
            health.SetDamageTakenMultiplier(0f);
            return true;
        }

        private void RefreshRetreatState()
        {
            var toPlayer = (Vector2)playerTransform.position - (Vector2)transform.position;
            if (toPlayer.sqrMagnitude <= Mathf.Epsilon)
            {
                isRetreating = false;
                return;
            }

            var playerController = playerTransform.GetComponent<MainGamePlayerController>();
            var playerFacing = playerController != null
                ? playerController.HorizontalFacingDirection
                : playerTransform.localScale.x < 0f ? Vector2.left : Vector2.right;
            var playerBody = playerTransform.GetComponent<Rigidbody2D>();
            var velocity = playerBody != null ? playerBody.linearVelocity : Vector2.zero;
            var isFleeing = velocity.sqrMagnitude >= FleeVelocityThreshold * FleeVelocityThreshold &&
                            Vector2.Dot(velocity.normalized, toPlayer.normalized) > 0.35f;
            var isFacingBoss = Vector2.Dot(playerFacing, toPlayer.normalized) >= ConfrontFacingDotThreshold;
            isRetreating = isFacingBoss && !isFleeing;
        }
    }
}
