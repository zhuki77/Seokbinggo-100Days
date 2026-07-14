using System;
using Nyangbingo.Combat;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using UnityEngine;

namespace Nyangbingo.Yokai
{
    public sealed class YokaiLoot : MonoBehaviour
    {
        [SerializeField] private YokaiDefinition definition;
        [SerializeField] private Health health;
        public event Action<ItemDefinition, int> Dropped;

        public void ConfigureForRuntime(YokaiDefinition value) => definition = value;

        private void Reset() => health = GetComponent<Health>();
        private void OnEnable()
        {
            if (health == null) health = GetComponent<Health>();
            if (health != null) health.Died += DropAll;
        }
        private void OnDisable()
        {
            if (health != null) health.Died -= DropAll;
        }
        private void DropAll()
        {
            if (definition == null) return;
            foreach (var drop in definition.Drops)
                if (drop.item != null && drop.amount > 0)
                {
                    Dropped?.Invoke(drop.item, drop.amount);
                    ItemAcquisition.Request(drop.item, drop.amount);
                }
        }
    }
}
