using System;
using Nyangbingo.Combat;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using Nyangbingo.Core;
using UnityEngine;

namespace Nyangbingo.Yokai
{
    public interface IYokaiRewardPolicy
    {
        int ScaleTearAmount(int baseAmount);
        float ScaleSignatureChance(float baseChance);
    }

    public interface ILootRandomSource
    {
        float Next01();
    }

    public sealed class YokaiLoot : MonoBehaviour
    {
        private sealed class UnityLootRandomSource : ILootRandomSource
        {
            public float Next01() => UnityEngine.Random.value;
        }

        private static readonly ILootRandomSource DefaultRandomSource = new UnityLootRandomSource();

        [SerializeField] private YokaiDefinition definition;
        [SerializeField] private Health health;
        private ILootRandomSource randomSource;
        private IYokaiRewardPolicy rewardPolicy;
        public event Action<ItemDefinition, int> Dropped;

        public void ConfigureForRuntime(YokaiDefinition value, ILootRandomSource random = null, IYokaiRewardPolicy rewards = null)
        {
            definition = value;
            randomSource = random;
            rewardPolicy = rewards;
        }

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
                    Grant(drop.item, drop.amount);

            var tearAmount = rewardPolicy != null
                ? rewardPolicy.ScaleTearAmount(definition.TearDrop)
                : definition.TearDrop;
            Grant(definition.TearItem, tearAmount);

            var signatureChance = rewardPolicy != null
                ? rewardPolicy.ScaleSignatureChance(definition.SignatureChance)
                : Mathf.Clamp01(definition.SignatureChance);
            if (float.IsNaN(signatureChance) || float.IsInfinity(signatureChance)) signatureChance = 0f;
            signatureChance = Mathf.Clamp01(signatureChance);
            if (definition.SignatureItem != null && signatureChance > 0f)
            {
                var roll = (randomSource ?? DefaultRandomSource).Next01();
                if (!float.IsNaN(roll) && !float.IsInfinity(roll) && roll >= 0f && roll <= 1f &&
                    roll < signatureChance) Grant(definition.SignatureItem, 1);
            }

            GameEvents.RaiseYokaiKilled(definition);
        }

        private void Grant(ItemDefinition item, int amount)
        {
            if (item == null || amount <= 0) return;
            Dropped?.Invoke(item, amount);
            ItemAcquisition.Request(item, amount);
        }
    }
}
