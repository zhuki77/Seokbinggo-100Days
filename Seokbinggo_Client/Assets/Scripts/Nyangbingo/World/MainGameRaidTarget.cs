using System;
using Nyangbingo.Data;
using Nyangbingo.Combat;
using Nyangbingo.Yokai;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>
    /// 최종 플레이어/성벽 프리팹이 들어오기 전까지 제품 씬의 요괴 AI가 공유하는 아트 비의존 표적이다.
    /// 누적 벽 피해는 디버그·HUD 연결 지점으로만 보관하며 실제 건축물 내구도 모델로 교체할 수 있다.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class MainGameRaidTarget : MonoBehaviour, IYokaiTarget, IWallMaterialTarget, IYokaiCombatTarget,
        Nyangbingo.Bosses.IBossCombatTarget
    {
        [SerializeField] private YokaiWallMaterial wallMaterial = YokaiWallMaterial.Ice;

        public Transform TargetTransform => transform;
        public YokaiWallMaterial WallMaterial => wallMaterial;
        public float AccumulatedWallDamage { get; private set; }
        public event Action<float> WallDamaged;

        public void DamageWall(float amount)
        {
            if (amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount)) return;
            AccumulatedWallDamage += amount;
            WallDamaged?.Invoke(amount);
        }

        public bool TryApplyContactDamage(int amount)
        {
            if (amount <= 0) return false;
            var health = GetComponent<Health>();
            if (health == null || health.IsDead) return false;
            var before = health.Current;
            health.ApplyDamage(amount, Nyangbingo.Core.DamageTag.Melee);
            if (health.Current >= before) return false;
            Nyangbingo.Core.GameEvents.RaisePlayerDamaged();
            return true;
        }

        public bool TryApplyBossSpecialDamage(int amount, Nyangbingo.Core.DamageTag tag, Vector2 knockback)
        {
            if (amount <= 0) return false;
            var health = GetComponent<Health>();
            if (health == null || health.IsDead) return false;
            var before = health.Current;
            health.ApplyDamage(amount, tag);
            if (health.Current >= before) return false;
            if (knockback.sqrMagnitude > Mathf.Epsilon)
            {
                var playerController = GetComponent<MainGamePlayerController>();
                if (playerController == null || !playerController.TryApplyBossKnockback(knockback))
                {
                    var body = GetComponent<Rigidbody2D>();
                    if (body != null && body.bodyType == RigidbodyType2D.Kinematic)
                        body.position += knockback;
                    else
                        health.TryApplyKnockback(knockback);
                }
            }
            Nyangbingo.Core.GameEvents.RaisePlayerDamaged();
            return true;
        }
    }
}
