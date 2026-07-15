using System;
using Nyangbingo.Core;
using UnityEngine;

namespace Nyangbingo.Combat
{
    public sealed class Health : MonoBehaviour
    {
        [SerializeField] private int maxHealth = 100;
        [Min(0)][SerializeField] private int defense;
        [SerializeField] private bool knockbackImmune;
        [Min(0f)][SerializeField] private float damageTakenMultiplier = 1f;
        [Min(0f)][SerializeField] private float fireDamageMultiplier = 1f;
        public int Current { get; private set; }
        public int MaxHealth => maxHealth;
        public int Defense => defense;
        public bool IsKnockbackImmune => knockbackImmune;
        public float DamageTakenMultiplier => damageTakenMultiplier;
        public float FireDamageMultiplier => fireDamageMultiplier;
        public bool IsDead => Current <= 0;
        public event Action<DamageTag, int> Damaged;
        public event Action Died;
        private void Awake() => Current = maxHealth;
        public void ConfigureForRuntime(int value, int defenseValue = 0)
        {
            maxHealth = Mathf.Max(1, value);
            defense = Mathf.Max(0, defenseValue);
            knockbackImmune = false;
            damageTakenMultiplier = 1f;
            fireDamageMultiplier = 1f;
            Current = maxHealth;
        }
        public void SetDefense(int value) => defense = Mathf.Max(0, value);
        public void SetKnockbackImmune(bool value) => knockbackImmune = value;
        public void SetDamageTakenMultiplier(float value)
        {
            if (!float.IsNaN(value) && !float.IsInfinity(value)) damageTakenMultiplier = Mathf.Max(0f, value);
        }
        public void SetFireDamageMultiplier(float value)
        {
            if (!float.IsNaN(value) && !float.IsInfinity(value)) fireDamageMultiplier = Mathf.Max(0f, value);
        }
        public bool RestoreCurrent(int value)
        {
            if (value < 0 || value > maxHealth) return false;
            Current = value;
            return true;
        }
        public bool TryApplyKnockback(Vector2 impulse)
        {
            if (knockbackImmune || float.IsNaN(impulse.x) || float.IsInfinity(impulse.x) ||
                float.IsNaN(impulse.y) || float.IsInfinity(impulse.y)) return false;
            var body = GetComponent<Rigidbody2D>();
            if (body == null) return false;
            body.AddForce(impulse, ForceMode2D.Impulse);
            return true;
        }
        public void ApplyDamage(int amount, DamageTag tag, DamageDelivery delivery = DamageDelivery.Direct)
        {
            if (IsDead || amount <= 0) return;
            var tagMultiplier = tag == DamageTag.Fire ? fireDamageMultiplier : 1f;
            var scaledAmount = amount * damageTakenMultiplier * tagMultiplier;
            if (float.IsNaN(scaledAmount) || scaledAmount <= 0f) return;
            var scaledDamage = float.IsPositiveInfinity(scaledAmount) || scaledAmount >= int.MaxValue
                ? int.MaxValue
                : Mathf.RoundToInt(scaledAmount);
            if (scaledDamage <= 0) return;
            var appliedDamage = delivery == DamageDelivery.Direct ? Mathf.Max(1, scaledDamage - defense) : scaledDamage;
            Current = Mathf.Max(0, Current - appliedDamage); Damaged?.Invoke(tag, appliedDamage);
            if (IsDead) Died?.Invoke();
        }
    }
}
