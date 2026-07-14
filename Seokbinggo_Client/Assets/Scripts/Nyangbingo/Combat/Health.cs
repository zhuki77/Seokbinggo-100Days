using System;
using Nyangbingo.Core;
using UnityEngine;

namespace Nyangbingo.Combat
{
    public sealed class Health : MonoBehaviour
    {
        [SerializeField] private int maxHealth = 100;
        public int Current { get; private set; }
        public bool IsDead => Current <= 0;
        public event Action<DamageTag, int> Damaged;
        public event Action Died;
        private void Awake() => Current = maxHealth;
        public void ConfigureForRuntime(int value)
        {
            maxHealth = Mathf.Max(1, value);
            Current = maxHealth;
        }
        public void ApplyDamage(int amount, DamageTag tag)
        {
            if (IsDead || amount <= 0) return;
            Current = Mathf.Max(0, Current - amount); Damaged?.Invoke(tag, amount);
            if (IsDead) Died?.Invoke();
        }
    }
}
