using System;
using System.Collections.Generic;
using Nyangbingo.Combat;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using Nyangbingo.Core;
using Nyangbingo.World;
using UnityEngine;

namespace Nyangbingo.Yokai
{
    public interface IYokaiRewardPolicy
    {
        int ScaleTearAmount(int baseAmount);
        int ScaleDropAmount(int baseAmount);
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
        private readonly List<ItemAmount> stolenItems = new List<ItemAmount>();
        private bool theftSucceeded;
        public event Action<ItemDefinition, int> Dropped;

        public void ConfigureForRuntime(YokaiDefinition value, ILootRandomSource random = null, IYokaiRewardPolicy rewards = null)
        {
            definition = value;
            randomSource = random;
            rewardPolicy = rewards;
        }

        public void RecordStolenItems(IReadOnlyList<ItemAmount> items)
        {
            if (items == null || items.Count == 0) return;
            foreach (var stack in items)
                if (stack.item != null && stack.amount > 0)
                    stolenItems.Add(stack);
            theftSucceeded |= stolenItems.Count > 0;
        }

        public List<InventorySlot> CaptureStolenItems()
        {
            var result = new List<InventorySlot>(stolenItems.Count);
            foreach (var stack in stolenItems)
                if (stack.item != null && stack.amount > 0)
                    result.Add(new InventorySlot
                    {
                        itemId = stack.item.Id,
                        amount = stack.amount
                    });
            return result;
        }

        public bool RestoreStolenItems(IEnumerable<InventorySlot> items,
            Func<string, ItemDefinition> findItem)
        {
            stolenItems.Clear();
            theftSucceeded = false;
            if (items == null) return true;
            foreach (var stack in items)
            {
                var item = findItem?.Invoke(stack.itemId);
                if (item == null || stack.amount <= 0 || stack.amount > item.MaxStack)
                {
                    stolenItems.Clear();
                    return false;
                }
                stolenItems.Add(new ItemAmount { item = item, amount = stack.amount });
            }
            theftSucceeded = stolenItems.Count > 0;
            return true;
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
                    Grant(drop.item, ScaleDropAmount(drop.amount));

            var tearAmount = rewardPolicy != null
                ? rewardPolicy.ScaleTearAmount(definition.TearDrop)
                : definition.TearDrop;
            Grant(definition.TearItem, tearAmount);
            if (theftSucceeded && definition.Kind == YokaiKind.Yagwanggwi)
            {
                foreach (var stolen in stolenItems)
                    Grant(stolen.item, stolen.amount);
                Grant(definition.TearItem, definition.TearBonus);
            }

            var signatureChance = rewardPolicy != null
                ? rewardPolicy.ScaleSignatureChance(definition.SignatureChance)
                : Mathf.Clamp01(definition.SignatureChance);
            if (float.IsNaN(signatureChance) || float.IsInfinity(signatureChance)) signatureChance = 0f;
            signatureChance = Mathf.Clamp01(signatureChance);
            var signatureConditionSatisfied =
                definition.SignatureCondition != YokaiSignatureCondition.StealSuccess ||
                theftSucceeded;
            if (definition.SignatureItem != null && signatureChance > 0f &&
                signatureConditionSatisfied)
            {
                var roll = (randomSource ?? DefaultRandomSource).Next01();
                if (!float.IsNaN(roll) && !float.IsInfinity(roll) && roll >= 0f && roll <= 1f &&
                    roll < signatureChance) Grant(definition.SignatureItem, 1);
            }

            GameEvents.RaiseYokaiKilled(definition);
        }

        private int ScaleDropAmount(int baseAmount) =>
            rewardPolicy == null ? baseAmount : rewardPolicy.ScaleDropAmount(baseAmount);

        private void Grant(ItemDefinition item, int amount)
        {
            if (item == null || amount <= 0) return;
            Dropped?.Invoke(item, amount);
            WorldItemDropRequest.Request(item, amount, transform.position);
        }
    }
}
