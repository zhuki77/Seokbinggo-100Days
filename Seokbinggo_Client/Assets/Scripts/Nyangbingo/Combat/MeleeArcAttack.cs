using Nyangbingo.Core;
using UnityEngine;

namespace Nyangbingo.Combat
{
    public sealed class MeleeArcAttack : MonoBehaviour
    {
        [SerializeField] private Transform origin;
        [SerializeField] private LayerMask targetLayers;
        [SerializeField] private float range = 1.5f;
        [Range(1, 180)][SerializeField] private float arcDegrees = 100f;
        [SerializeField] private int damage = 5;
        [SerializeField] private float knockback = .5f;

        public void Strike(Vector2 direction)
        {
            Vector2 center = origin == null ? (Vector2)transform.position : (Vector2)origin.position;
            foreach (var hit in Physics2D.OverlapCircleAll(center, range, targetLayers))
            {
                var toTarget = ((Vector2)hit.transform.position - center).normalized;
                if (Vector2.Angle(direction.normalized, toTarget) > arcDegrees * .5f) continue;
                hit.GetComponentInParent<Health>()?.ApplyDamage(damage, DamageTag.Melee);
                if (hit.attachedRigidbody != null) hit.attachedRigidbody.AddForce(toTarget * knockback, ForceMode2D.Impulse);
            }
        }
    }
}
